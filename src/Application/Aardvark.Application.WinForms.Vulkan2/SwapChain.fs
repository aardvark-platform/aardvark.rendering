namespace Aardvark.Application.WinForms.Vulkan

open System
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Windows.Forms
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Reflection
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

[<AutoOpen>]
module private EnumExtensions =

    type Command with
        static member PresentBarrier(img : Image) =
            if img.Layout = VkImageLayout.PresentSrcKhr then
                Command.Nop
            else
                { new Command() with
                    member x.Enqueue cmd =
                        let familyIndex = cmd.QueueFamily.Index
                        let mutable prePresentBarrier =
                            VkImageMemoryBarrier(
                                VkStructureType.ImageMemoryBarrier, 0n, 
                                VkAccessFlags.ColorAttachmentWriteBit,
                                VkAccessFlags.MemoryReadBit,
                                VkImageLayout.ColorAttachmentOptimal,
                                VkImageLayout.PresentSrcKhr,
                                uint32 familyIndex,
                                uint32 familyIndex,
                                img.Handle,
                                VkImageSubresourceRange(VkImageAspectFlags.ColorBit, 0u, 1u, 0u, 1u)
                            )

                        cmd.AppendCommand()
                        VkRaw.vkCmdPipelineBarrier(
                            cmd.Handle,
                            VkPipelineStageFlags.AllCommandsBit, 
                            VkPipelineStageFlags.BottomOfPipeBit, 
                            VkDependencyFlags.None, 
                            0u, NativePtr.zero, 
                            0u, NativePtr.zero, 
                            1u, &&prePresentBarrier
                        )

                        img.Layout <- VkImageLayout.PresentSrcKhr
                        Disposable.Empty
                }


type OldSwapchain(device : Device, handle : VkSwapchainKHR, description : SwapchainDescription, size : V2i, colorLayout : VkImageLayout, depthView : Option<ImageView>, colorViews : ImageView[], framebuffers : Framebuffer[]) =
    inherit Resource<VkSwapchainKHR>(device, handle)


    let mutable currentBuffer = 0u

    member x.Size = size
    member x.Description = description
    member x.DepthView = depthView
    member x.ColorViews = colorViews
    member x.Framebuffers = framebuffers

    member x.RenderFrame (render : DeviceQueue -> Framebuffer -> 'a) =
        if x.Handle.IsNull then failf "cannot use disposed Swapchain"

        device.GraphicsFamily.UsingQueue(fun queue ->
            use sem = device.CreateSemaphore()

            VkRaw.vkAcquireNextImageKHR(device.Handle, handle, ~~~0UL, sem.Handle, VkFence.Null, &&currentBuffer)
                |> check "could not acquire Swapchain Image"

            queue.Wait(sem)

            let view = colorViews.[int currentBuffer]
            let image = view.Image
            
            let cmd0 = queue.Family.DefaultCommandPool.CreateCommandBuffer CommandBufferLevel.Primary
            cmd0.Begin(CommandBufferUsage.OneTimeSubmit)
            cmd0.enqueue {
                do! Command.ClearColor(image.[ImageAspect.Color, * , *], C4f.Black)
                match depthView with
                    | Some v -> do! Command.ClearDepthStencil(v.Image.[ImageAspect.DepthStencil, *, *], 1.0, 0u)
                    | _ -> ()
                do! Command.TransformLayout(image, colorLayout)
            }
            cmd0.End()
            queue.Start cmd0

            let res = render queue framebuffers.[int currentBuffer]
            
            let cmd1 = queue.Family.DefaultCommandPool.CreateCommandBuffer CommandBufferLevel.Primary
            cmd1.Begin CommandBufferUsage.OneTimeSubmit
            cmd1.Enqueue(Command.TransformLayout(image, VkImageLayout.PresentSrcKhr))
            cmd1.End()
            queue.Start cmd1

            let mutable result = VkResult.VkSuccess
            let mutable info =
                VkPresentInfoKHR(
                    VkStructureType.PresentInfoKHR,
                    0n, 
                    0u, NativePtr.zero,
                    1u, &&x.Handle,
                    &&currentBuffer,
                    &&result
                )

            VkRaw.vkQueuePresentKHR(queue.Handle, &&info)
                |> check "could not swap buffers"

            queue.WaitIdle()
            cmd0.Dispose()
            cmd1.Dispose()

            result 
                |> check "something went wrong with swap"

            res
        )

type Swapchain(device : Device, description : SwapchainDescription) =
    let surface = description.surface
    let renderPass = description.renderPass

    let recreate (old : VkSwapchainKHR) (size : V2i) =
        let extent = VkExtent2D(size.X, size.Y)
        let surface = description.surface
        let renderPass = description.renderPass

        let colorUsage =
            if description.samples = 1 then VkImageUsageFlags.ColorAttachmentBit
            else VkImageUsageFlags.ColorAttachmentBit

        let mutable info =
            VkSwapchainCreateInfoKHR(
                VkStructureType.SwapChainCreateInfoKHR, 0n, 
                VkSwapchainCreateFlagsKHR.MinValue,

                surface.Handle,
                uint32 description.buffers,
                description.colorFormat,

                description.colorSpace,
                extent,

                1u,
                colorUsage,
                VkSharingMode.Exclusive,
                0u, NativePtr.zero,

                description.transform,
                VkCompositeAlphaFlagBitsKHR.VkCompositeAlphaOpaqueBitKhr,
                description.presentMode,
                1u,
                old
            )

        let mutable handle = VkSwapchainKHR.Null
        VkRaw.vkCreateSwapchainKHR(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "could not create Swapchain"
        
        let renderViews = 
            let mutable count = 0u
            VkRaw.vkGetSwapchainImagesKHR(device.Handle, handle, &&count, NativePtr.zero)
                |> check "could not get Swapchain Images"

            let imageHandles = Array.zeroCreate (int count)
            imageHandles |> NativePtr.withA (fun pImages ->
                VkRaw.vkGetSwapchainImagesKHR(device.Handle, handle, &&count, pImages)
                    |> check "could not get Swapchain Images"
            )

            imageHandles |> Array.map (fun handle ->
                let image = 
                    Image(
                        device,
                        handle,
                        V3i(size.X, size.Y, 1),
                        1, 1, 1,
                        TextureDimension.Texture2D,
                        description.colorFormat,
                        VkComponentMapping.Identity,
                        Unchecked.defaultof<_>,
                        VkImageLayout.Undefined
                    )
                device.CreateImageView(image, 0, 1, 0, 1)

            )

        use cmd = device.GraphicsFamily.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        cmd.Begin CommandBufferUsage.OneTimeSubmit

        let colorView =
            if description.samples = 1 then
                None
            else
                let image = 
                    device.CreateImage(
                        V3i(size.X, size.Y, 1), 1, 1, description.samples, 
                        TextureDimension.Texture2D, 
                        VkFormat.toTextureFormat description.colorFormat, 
                        VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit
                    )
                // hacky-hack
                image.Format <- description.colorFormat
                image.ComponentMapping <- VkComponentMapping.Identity
                cmd.Enqueue (Command.TransformLayout(image, VkImageLayout.ColorAttachmentOptimal))

                let view = device.CreateImageView(image, 0, 1, 0, 1)
                Some view

        let depthView =
            match description.depthFormat with
                | Some depthFormat ->
                    let image = 
                        device.CreateImage(
                            V3i(size.X, size.Y, 1), 1, 1, description.samples, 
                            TextureDimension.Texture2D, 
                            VkFormat.toTextureFormat depthFormat, 
                            VkImageUsageFlags.DepthStencilAttachmentBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit
                        )
                    // hacky-hack
                    image.Format <- depthFormat
                    image.ComponentMapping <- VkComponentMapping.Identity
                    cmd.Enqueue (Command.TransformLayout(image, VkImageLayout.DepthStencilAttachmentOptimal))

                    let view = device.CreateImageView(image, 0, 1, 0, 1)

                    Some view
                | _ -> 
                    None

        let framebuffers =
            match colorView with
                | Some colorView ->
                    let attachments =
                        match depthView with
                            | Some depthView -> Map.ofList [ DefaultSemantic.Colors, colorView; DefaultSemantic.Depth, depthView]
                            | None -> Map.ofList [ DefaultSemantic.Colors, colorView ]

                    [| device.CreateFramebuffer(renderPass, attachments) |]
                | None ->
                    renderViews |> Array.map (fun colorView ->

                        let attachments =
                            match depthView with
                                | Some depthView -> Map.ofList [ DefaultSemantic.Colors, colorView; DefaultSemantic.Depth, depthView]
                                | None -> Map.ofList [ DefaultSemantic.Colors, colorView ]

                        device.CreateFramebuffer(renderPass, attachments)
                    )
        cmd.End()
        device.GraphicsFamily.RunSynchronously(cmd)

        handle, depthView, renderViews, colorView, framebuffers

    let mutable disposed = 0

    let mutable handle = VkSwapchainKHR.Null
    let mutable size = V2i.Zero
    let mutable depthView : Option<ImageView> = None
    let mutable renderViews : ImageView[] = Array.zeroCreate 0
    let mutable colorView : Option<ImageView> = None
    let mutable framebuffers : Framebuffer[] = Array.zeroCreate 0
    let mutable currentBuffer = 0u

    let update() =
        if surface.Handle.IsValid && disposed = 0 then
            let newSize = surface.Size
            if newSize <> size || handle.IsNull then
                // delete old things
                framebuffers |> Array.iter device.Delete
                depthView |> Option.iter (fun view -> device.Delete view; device.Delete view.Image)
                renderViews |> Array.iter device.Delete
                colorView |> Option.iter (fun view -> device.Delete view; device.Delete view.Image)

                let (newHandle, newDepthView, newRenderViews, newColorView, newFramebuffers) = recreate handle newSize
                if handle.IsValid then VkRaw.vkDestroySwapchainKHR(device.Handle, handle, NativePtr.zero)

                handle <- newHandle
                size <- newSize
                depthView <- newDepthView
                renderViews <- newRenderViews
                colorView <- newColorView
                framebuffers <- newFramebuffers
                currentBuffer <- 0u

    member x.Size = update(); size
    member x.Description = description
    member x.Samples = description.samples

    member x.RenderFrame (render : DeviceQueue -> Framebuffer -> 'a) =
        if disposed <> 0 then failf "cannot use disposed Swapchain"
        update()
        device.GraphicsFamily.UsingQueue(fun queue ->
            use sem = device.CreateSemaphore()

            VkRaw.vkAcquireNextImageKHR(device.Handle, handle, ~~~0UL, sem.Handle, VkFence.Null, &&currentBuffer)
                |> check "could not acquire Swapchain Image"

            queue.Wait(sem)

            let targetView = renderViews.[int currentBuffer]
            let targetImage = targetView.Image

            let index = int currentBuffer % framebuffers.Length

            let preRender  = queue.Family.DefaultCommandPool.CreateCommandBuffer CommandBufferLevel.Primary
            let postRender = queue.Family.DefaultCommandPool.CreateCommandBuffer CommandBufferLevel.Primary
            // clear the currently attached color/depth - views
            do  preRender.Begin(CommandBufferUsage.OneTimeSubmit)
                let currentColorAttachmentView = 
                    match colorView with
                        | Some view -> view
                        | None -> renderViews.[index]

                preRender.enqueue {
                    let color = currentColorAttachmentView.Image.[ImageAspect.Color]
                    do! Command.ClearColor(color, C4f.Black)
                    match depthView with
                        | Some v -> do! Command.ClearDepthStencil(v.Image.[ImageAspect.DepthStencil], 1.0, 0u)
                        | _ -> ()
                    do! Command.TransformLayout(currentColorAttachmentView.Image, VkImageLayout.ColorAttachmentOptimal)
                }
                preRender.End()
                queue.Start preRender

            let res = render queue framebuffers.[index]
            
            do  postRender.Begin CommandBufferUsage.OneTimeSubmit
                match colorView with
                    | Some colorView ->
                        postRender.enqueue {
                            do! Command.TransformLayout(colorView.Image, VkImageLayout.TransferSrcOptimal)
                            do! Command.TransformLayout(targetImage, VkImageLayout.TransferDstOptimal)
                            do! Command.ResolveMultisamples(colorView.Image.[ImageAspect.Color, 0, *], targetImage.[ImageAspect.Color, 0, *])
                        }
                    | None -> 
                        ()
                postRender.Enqueue(Command.TransformLayout(targetImage, VkImageLayout.PresentSrcKhr))
                postRender.End()
                queue.Start postRender

            let mutable result = VkResult.VkSuccess
            let mutable info =
                VkPresentInfoKHR(
                    VkStructureType.PresentInfoKHR,
                    0n, 
                    0u, NativePtr.zero,
                    1u, &&handle,
                    &&currentBuffer,
                    &&result
                )

            VkRaw.vkQueuePresentKHR(queue.Handle, &&info)
                |> check "could not swap buffers"

            queue.WaitIdle()
            preRender.Dispose()
            postRender.Dispose()

            result 
                |> check "something went wrong with swap"

            res
        )

    member x.Dispose() =
        let o = System.Threading.Interlocked.Exchange(&disposed, 1)
        if o = 0 then
            // delete old things
            framebuffers |> Array.iter device.Delete
            depthView |> Option.iter (fun view -> device.Delete view; device.Delete view.Image)
            renderViews |> Array.iter device.Delete
            colorView |> Option.iter (fun view -> device.Delete view; device.Delete view.Image)
            VkRaw.vkDestroySwapchainKHR(device.Handle, handle, NativePtr.zero)

            handle <- VkSwapchainKHR.Null
            size <- V2i.Zero
            depthView <- None
            renderViews <- Array.zeroCreate 0
            colorView <- None
            framebuffers <- Array.zeroCreate 0
            currentBuffer <- 0u

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Swapchain =
    let create (desc : SwapchainDescription) (device : Device) =
        new Swapchain(device, desc)

    let delete (chain : Swapchain) (device : Device) =
        chain.Dispose()


[<AbstractClass; Sealed; Extension>]
type DeviceSwapchainExtensions private() =

    [<Extension>]
    static member CreateSwapchain(this : Device, description : SwapchainDescription) =
        this |> Swapchain.create description

    [<Extension>]
    static member Delete(this : Device, chain : Swapchain) =
        this |> Swapchain.delete chain
