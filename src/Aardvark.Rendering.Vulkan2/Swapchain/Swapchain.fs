namespace Aardvark.Rendering.Vulkan

open System
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
                    member x.Compatible = QueueFlags.All
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


type Swapchain(device : Device, description : SwapchainDescription) =
    let surface = description.surface
    let renderPass = description.renderPass

    let recreate (old : VkSwapchainKHR) (size : V2i) =
        let extent = VkExtent2D(size.X, size.Y)
        let surface = description.surface
        let renderPass = description.renderPass

        let colorUsage =
            if description.samples = 1 then VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferDstBit
            else VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferDstBit

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
                        Unchecked.defaultof<_>,
                        VkImageLayout.Undefined
                    )
                device.CreateImageView(image, 0, 1, 0, 1, VkComponentMapping.Identity)

            )

        use token = device.Token
//        use cmd = device.GraphicsFamily.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
//        cmd.Begin CommandBufferUsage.OneTimeSubmit

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
                token.Enqueue (Command.TransformLayout(image, VkImageLayout.ColorAttachmentOptimal))

                let view = device.CreateImageView(image, 0, 1, 0, 1, VkComponentMapping.Identity)
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
                    token.Enqueue (Command.TransformLayout(image, VkImageLayout.DepthStencilAttachmentOptimal))

                    let view = device.CreateImageView(image, 0, 1, 0, 1, VkComponentMapping.Identity)

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
        token.Sync()

        handle, depthView, renderViews, colorView, framebuffers

    let mutable disposed = 0

    let mutable handle = VkSwapchainKHR.Null
    let mutable size = V2i.Zero
    let mutable depthView : Option<ImageView> = None
    let mutable renderViews : ImageView[] = Array.zeroCreate 0
    let mutable colorView : Option<ImageView> = None
    let mutable framebuffers : Framebuffer[] = Array.zeroCreate 0
    let currentBuffer = ref 0u

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
                currentBuffer := 0u

    static let presentCommand(handle : VkSwapchainKHR, currentBuffer : uint32)  =
        { new QueueCommand() with
            member x.Compatible = QueueFlags.Graphics
            member x.Enqueue(queue, waitFor) =
                lock queue (fun () -> 
                    let mutable handle = handle
                    let mutable currentBuffer = currentBuffer
                    let mutable result = VkResult.VkSuccess

                    let arr = List.toArray (waitFor |> List.map (fun s -> s.Handle))
                    arr |> NativePtr.withA (fun pWaitFor ->
                        let mutable info =
                            VkPresentInfoKHR(
                                VkStructureType.PresentInfoKHR, 0n, 
                                uint32 arr.Length, pWaitFor,
                                1u, &&handle,
                                &&currentBuffer,
                                &&result
                            )

                        VkRaw.vkQueuePresentKHR(queue.Handle, &&info) |> check "could not swap buffers"
                    )
                    queue.WaitIdle()
                )
                None, Disposable.Empty
        }

    static let acquireNextImage(handle : VkSwapchainKHR, currentBuffer : ref<uint32>) =
        { new QueueCommand() with
            member x.Compatible = QueueFlags.Graphics
            member x.Enqueue(queue, waitFor) =
                let sem = queue.Device.CreateSemaphore()
                let mutable c = !currentBuffer
                VkRaw.vkAcquireNextImageKHR(queue.Device.Handle, handle, ~~~0UL, sem.Handle, VkFence.Null, &&c)
                    |> check "could not acquire Swapchain Image"
                currentBuffer := c

                Some sem, Disposable.Empty
        }

    member x.Present = presentCommand(handle, !currentBuffer)
    member x.AcquireNextImage = acquireNextImage(handle, currentBuffer)

    member x.Size = update(); size
    member x.Description = description
    member x.Samples = description.samples

    member x.RenderFrame (render : Framebuffer -> 'a) =
        if disposed <> 0 then 
            failf "cannot use disposed Swapchain"

        device.perform {
            update()

            // acquire a swapchain image for rendering
            do! x.AcquireNextImage

            // determine color and output images (may differ when using MSAA)
            let outputIndex = int !currentBuffer
            let outputImage = renderViews.[outputIndex].Image

            let renderImage = 
                match colorView with
                    | Some view -> view.Image
                    | None -> renderViews.[outputIndex].Image


            let framebuffer =
                framebuffers.[outputIndex % framebuffers.Length]

            // clear color & depth
            do! Command.ClearColor(renderImage.[ImageAspect.Color], C4f.Black)
            match depthView with
                | Some v -> do! Command.ClearDepthStencil(v.Image.[ImageAspect.DepthStencil], 1.0, 0u)
                | _ -> ()

            // ensure that colorImage is ColorAttachmentOptimal
            do! Command.TransformLayout(renderImage, VkImageLayout.ColorAttachmentOptimal)

            // render the scene
            let res = render framebuffer

            // if colorView and outputView are not identical (MSAA) then
            // resolve colorView to outputView
            if renderImage <> outputImage then
                do! Command.TransformLayout(renderImage, VkImageLayout.TransferSrcOptimal)
                do! Command.TransformLayout(outputImage, VkImageLayout.TransferDstOptimal)
                do! Command.ResolveMultisamples(renderImage.[ImageAspect.Color, 0, *], outputImage.[ImageAspect.Color, 0, *])
       
            // finally the outputImage needs to be in layout PresentSrcKhr
            do! Command.TransformLayout(outputImage, VkImageLayout.PresentSrcKhr)

            // present the image
            do! x.Present

            return res
        }

    member x.Dispose() =
        let o = System.Threading.Interlocked.Exchange(&disposed, 1)
        if o = 0 then
            // delete old things
            VkRaw.vkDestroySwapchainKHR(device.Handle, handle, NativePtr.zero)
            framebuffers |> Array.iter device.Delete
            depthView |> Option.iter (fun view -> device.Delete view; device.Delete view.Image)
            renderViews |> Array.iter device.Delete
            colorView |> Option.iter (fun view -> device.Delete view; device.Delete view.Image)

            handle <- VkSwapchainKHR.Null
            size <- V2i.Zero
            depthView <- None
            renderViews <- Array.zeroCreate 0
            colorView <- None
            framebuffers <- Array.zeroCreate 0
            currentBuffer := 0u

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
