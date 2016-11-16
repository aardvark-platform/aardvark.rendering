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
                { new Command<unit>() with
                    member x.Enqueue _ = ()
                    member x.Dispose() = ()
                }
            else
                { new Command<unit>() with
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

                    member x.Dispose() = ()
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
                do! Command.ClearColor(image, C4f.Black)
                match depthView with
                    | Some v -> do! Command.ClearDepthStencil(v.Image, 1.0, 0u)
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

        let colorUsage, colorLayout =
            if description.samples = 1 then VkImageUsageFlags.ColorAttachmentBit, VkImageLayout.ColorAttachmentOptimal
            else VkImageUsageFlags.TransferDstBit, VkImageLayout.TransferDstOptimal


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
        
        let colorViews = 
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

        let depthView =
            match description.depthFormat with
                | Some depthFormat ->
                    let image = 
                        device.CreateImage(
                            V3i(size.X, size.Y, 1), 1, 1, 1, 
                            TextureDimension.Texture2D, 
                            VkFormat.toTextureFormat depthFormat, 
                            VkImageUsageFlags.DepthStencilAttachmentBit, 
                            VkImageLayout.DepthStencilAttachmentOptimal
                        )
                    // hacky-hack
                    image.Format <- depthFormat
                    image.ComponentMapping <- VkComponentMapping.Identity

                    let view = device.CreateImageView(image, 0, 1, 0, 1)

                    Some view
                | _ -> 
                    None

        let framebuffers =
            colorViews |> Array.map (fun colorView ->

                let attachments =
                    match depthView with
                        | Some depthView -> Map.ofList [ DefaultSemantic.Colors, colorView; DefaultSemantic.Depth, depthView]
                        | None -> Map.ofList [ DefaultSemantic.Colors, colorView ]

                device.CreateFramebuffer(renderPass, attachments)
            )

        handle, depthView, colorViews, framebuffers

    let mutable disposed = 0

    let mutable colorLayout = VkImageLayout.ColorAttachmentOptimal
    let mutable handle = VkSwapchainKHR.Null
    let mutable size = V2i.Zero
    let mutable depthView : Option<ImageView> = None
    let mutable colorViews : ImageView[] = Array.zeroCreate 0
    let mutable framebuffers : Framebuffer[] = Array.zeroCreate 0
    let mutable currentBuffer = 0u

    let update() =
        if surface.Handle.IsValid && disposed = 0 then
            let newSize = surface.Size
            if newSize <> size || handle.IsNull then
                // delete old things
                framebuffers |> Array.iter device.Delete
                depthView |> Option.iter (fun view -> device.Delete view; device.Delete view.Image)
                colorViews |> Array.iter device.Delete

                let (newHandle, newDepthView, newColorViews, newFramebuffers) = recreate handle newSize
                if handle.IsValid then VkRaw.vkDestroySwapchainKHR(device.Handle, handle, NativePtr.zero)

                handle <- newHandle
                size <- newSize
                depthView <- newDepthView
                colorViews <- newColorViews
                framebuffers <- newFramebuffers
                currentBuffer <- 0u
    member x.Size = update(); size
    member x.Description = description

    member x.RenderFrame (render : DeviceQueue -> Framebuffer -> 'a) =
        if disposed <> 0 then failf "cannot use disposed Swapchain"
        update()
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
                do! Command.ClearColor(image, C4f.Black)
                match depthView with
                    | Some v -> do! Command.ClearDepthStencil(v.Image, 1.0, 0u)
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
                    1u, &&handle,
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

    member x.Dispose() =
        let o = System.Threading.Interlocked.Exchange(&disposed, 1)
        if o = 0 then
            // delete old things
            framebuffers |> Array.iter device.Delete
            depthView |> Option.iter (fun view -> device.Delete view; device.Delete view.Image)
            colorViews |> Array.iter device.Delete
            VkRaw.vkDestroySwapchainKHR(device.Handle, handle, NativePtr.zero)

            handle <- VkSwapchainKHR.Null
            size <- V2i.Zero
            depthView <- None
            colorViews <- Array.zeroCreate 0
            framebuffers <- Array.zeroCreate 0
            currentBuffer <- 0u

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Swapchain =
    let create (desc : SwapchainDescription) (device : Device) =
        new Swapchain(device, desc)
//        let size = desc.surface.Size
//        let extent = VkExtent2D(size.X, size.Y)
//        let surface = desc.surface
//        let renderPass = desc.renderPass
//
//        let colorUsage, colorLayout =
//            if desc.samples = 1 then VkImageUsageFlags.ColorAttachmentBit, VkImageLayout.ColorAttachmentOptimal
//            else VkImageUsageFlags.TransferDstBit, VkImageLayout.TransferDstOptimal
//
//
//        let mutable info =
//            VkSwapchainCreateInfoKHR(
//                VkStructureType.SwapChainCreateInfoKHR, 0n, 
//                VkSwapchainCreateFlagsKHR.MinValue,
//
//                surface.Handle,
//                uint32 desc.buffers,
//                desc.colorFormat,
//
//                desc.colorSpace,
//                extent,
//
//                1u,
//                colorUsage,
//                VkSharingMode.Exclusive,
//                0u, NativePtr.zero,
//
//                desc.transform,
//                VkCompositeAlphaFlagBitsKHR.VkCompositeAlphaOpaqueBitKhr,
//                desc.presentMode,
//                1u, VkSwapchainKHR.Null
//            )
//
//        let mutable handle = VkSwapchainKHR.Null
//        VkRaw.vkCreateSwapchainKHR(device.Handle, &&info, NativePtr.zero, &&handle)
//            |> check "could not create Swapchain"
//        
//        let colorViews = 
//            let mutable count = 0u
//            VkRaw.vkGetSwapchainImagesKHR(device.Handle, handle, &&count, NativePtr.zero)
//                |> check "could not get Swapchain Images"
//
//            let imageHandles = Array.zeroCreate (int count)
//            imageHandles |> NativePtr.withA (fun pImages ->
//                VkRaw.vkGetSwapchainImagesKHR(device.Handle, handle, &&count, pImages)
//                    |> check "could not get Swapchain Images"
//            )
//
//            imageHandles |> Array.map (fun handle ->
//                let image = 
//                    Image(
//                        device,
//                        handle,
//                        V3i(size.X, size.Y, 1),
//                        1, 1, 1,
//                        TextureDimension.Texture2D,
//                        desc.colorFormat,
//                        VkComponentMapping.Identity,
//                        Unchecked.defaultof<_>,
//                        VkImageLayout.Undefined
//                    )
//                device.CreateImageView(image, 0, 1, 0, 1)
//
//            )
//
//        let depthView =
//            match desc.depthFormat with
//                | Some depthFormat ->
//                    let image = 
//                        device.CreateImage(
//                            V3i(size.X, size.Y, 1), 1, 1, 1, 
//                            TextureDimension.Texture2D, 
//                            VkFormat.toTextureFormat depthFormat, 
//                            VkImageUsageFlags.DepthStencilAttachmentBit, 
//                            VkImageLayout.DepthStencilAttachmentOptimal
//                        )
//                    // hacky-hack
//                    image.Format <- depthFormat
//                    image.ComponentMapping <- VkComponentMapping.Identity
//
//                    let view = device.CreateImageView(image, 0, 1, 0, 1)
//
//                    Some view
//                | _ -> 
//                    None
//
//        let framebuffers =
//            colorViews |> Array.map (fun colorView ->
//
//                let attachments =
//                    match depthView with
//                        | Some depthView -> Map.ofList [ DefaultSemantic.Colors, colorView; DefaultSemantic.Depth, depthView]
//                        | None -> Map.ofList [ DefaultSemantic.Colors, colorView ]
//
//                device.CreateFramebuffer(renderPass, attachments)
//            )
//
//        Swapchain(device, handle, desc, size, colorLayout, depthView, colorViews, framebuffers)

    let delete (chain : Swapchain) (device : Device) =
        chain.Dispose()
//        if chain.Handle.IsValid then
//            chain.DepthView |> Option.iter (fun view ->
//                device.Delete view.Image
//                device.Delete view
//            )
//            chain.ColorViews |> Array.iter device.Delete
//            chain.Framebuffers |> Array.iter device.Delete
//            VkRaw.vkDestroySwapchainKHR(device.Handle, chain.Handle, NativePtr.zero)
//            chain.Handle <- VkSwapchainKHR.Null


[<AbstractClass; Sealed; Extension>]
type DeviceSwapchainExtensions private() =

    [<Extension>]
    static member CreateSwapchain(this : Device, description : SwapchainDescription) =
        this |> Swapchain.create description

    [<Extension>]
    static member Delete(this : Device, chain : Swapchain) =
        this |> Swapchain.delete chain
