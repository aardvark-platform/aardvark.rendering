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
open KHRSwapchain

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


[<AutoOpen>]          
module ImageTrafoExtensions =
    type Box3i with
        member x.Transformed(t : ImageTrafo) =
            match t with
                | ImageTrafo.Rot0 -> 
                    x

                | ImageTrafo.MirrorX -> 
                    Box3i(V3i(x.Max.X, x.Min.Y, x.Min.Z), V3i(x.Min.X, x.Max.Y, x.Max.Z))
                    
                | ImageTrafo.MirrorY -> 
                    Box3i(V3i(x.Min.X, x.Max.Y, x.Min.Z), V3i(x.Max.X, x.Min.Y, x.Max.Z))

                | _ ->
                    failwithf "box cannot be transformed using %A" t

type Swapchain(device : Device, description : SwapchainDescription) =
    let surface = description.surface
    let renderPass = description.renderPass

    let presentTrafo = VkSurfaceTransformFlagsKHR.ofImageTrafo description.presentTrafo

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

                presentTrafo,
                VkCompositeAlphaFlagsKHR.VkCompositeAlphaOpaqueBitKhr,
                description.presentMode,
                1u,
                old
            )

        let mutable handle = VkSwapchainKHR.Null
        VkRaw.vkCreateSwapchainKHR(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "could not create Swapchain"
        
        use token = device.Token

        let buffers = 
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
                device.CreateOutputImageView(image, 0, 1, 0, 1)

            )

        let colorView =
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
            
            let view = device.CreateOutputImageView(image, 0, 1, 0, 1)
            view

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

                    let view = device.CreateOutputImageView(image, 0, 1, 0, 1)

                    Some view
                | _ -> 
                    None

        let framebuffer =
            let attachments =
                match depthView with
                    | Some depthView -> Map.ofList [ DefaultSemantic.Colors, colorView; DefaultSemantic.Depth, depthView]
                    | None -> Map.ofList [ DefaultSemantic.Colors, colorView ]

            device.CreateFramebuffer(renderPass, attachments)

        let resolvedImage =
            if description.samples = 1 then
                None
            else
                let image = 
                    device.CreateImage(
                        V3i(size.X, size.Y, 1), 1, 1, 1, 
                        TextureDimension.Texture2D, 
                        VkFormat.toTextureFormat description.colorFormat, 
                        VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit
                    )
                // hacky-hack
                image.Format <- description.colorFormat
                token.Enqueue (Command.TransformLayout(image, VkImageLayout.TransferDstOptimal))
                //let view = device.CreateImageView(image, 0, 1, 0, 1, VkComponentMapping.Identity)
                Some image
                

        token.Sync()

        handle, buffers, framebuffer, resolvedImage

    let mutable disposed = 0

    let mutable handle = VkSwapchainKHR.Null
    let mutable size = V2i.Zero
    let mutable buffers : ImageView[] = Array.zeroCreate 0

    let mutable resolvedImage : Option<Image> = None
    let mutable framebuffer : Option<Framebuffer> = None
    let currentBuffer = ref 0u

    let update() =
        if surface.Handle.IsValid && disposed = 0 then
            let newSize = surface.Size
            if newSize <> size || handle.IsNull then
                // delete old things

                framebuffer |> Option.iter (fun framebuffer ->
                    framebuffer.Attachments |> Map.iter (fun _ v ->
                        device.Delete v.Image
                        device.Delete v
                    )
                    device.Delete framebuffer
                )
                resolvedImage|> Option.iter device.Delete
                buffers |> Array.iter device.Delete

                //handle, buffers, framebuffer, resolvedView
                let (newHandle, newBuffers, newFramebuffer, newResolvedImage) = recreate handle newSize
                if handle.IsValid then VkRaw.vkDestroySwapchainKHR(device.Handle, handle, NativePtr.zero)

                handle <- newHandle
                size <- newSize
                buffers <- newBuffers
                framebuffer <- Some newFramebuffer
                resolvedImage <- newResolvedImage
                currentBuffer := 0u


    member x.Present = QueueCommand.Present(handle, currentBuffer)
    member x.AcquireNextImage = QueueCommand.AcquireNextImage(handle, currentBuffer)

    member x.Size = update(); size
    member x.Description = description
    member x.Samples = description.samples

    member x.RenderFrame (render : Framebuffer -> 'a) =
        lock x (fun () ->
            if disposed <> 0 then 
                failf "cannot use disposed Swapchain"

            device.perform {
                update()

                // acquire a swapchain image for rendering
                do! x.AcquireNextImage

                // determine color and output images (may differ when using MSAA)
                let outputIndex = int !currentBuffer
                let backbuffer = buffers.[outputIndex].Image

                let framebuffer = framebuffer.Value
                let colorView = framebuffer.Attachments.[DefaultSemantic.Colors]
                let depthView = Map.tryFind DefaultSemantic.Depth framebuffer.Attachments

                // resolve(renderView, resolveView)
                // blit(resolveView, transformView)

                // clear color & depth
                do! Command.ClearColor(colorView.Image.[ImageAspect.Color], C4f.Black)
                match depthView with
                    | Some v -> do! Command.ClearDepthStencil(v.Image.[ImageAspect.DepthStencil], 1.0, 0u)
                    | _ -> ()

                // ensure that colorImage is ColorAttachmentOptimal
                do! Command.TransformLayout(colorView.Image, VkImageLayout.ColorAttachmentOptimal)

                // render the scene
                let res = render framebuffer

                // the color-data is currently stored in colorView
                let mutable currentImage = colorView.Image

                // if the colorView is multisampled we need to resolve it to a temporary 
                // single-sampled image (resolvedImage)
                match resolvedImage with
                    | Some resolvedImage ->
                        // resolve multisamples
                        do! Command.TransformLayout(colorView.Image, VkImageLayout.TransferSrcOptimal)
                        do! Command.TransformLayout(resolvedImage, VkImageLayout.TransferDstOptimal)
                        do! Command.ResolveMultisamples(colorView.Image.[ImageAspect.Color, 0, *], resolvedImage.[ImageAspect.Color, 0, *])

                        // the color-data is now stored in the resolved image
                        currentImage <- resolvedImage
                    | None ->
                        ()

                // since the blit might include an ImageTrafo we need to sompute
                // appropriate ranges
                let srcRange = Box3i(V3i(0,0,0), V3i(size.X - 1, size.Y - 1, 0))
                let dstRange = srcRange.Transformed description.blitTrafo

                // blit the current image to the final backbuffer using the ranges from above
                do! Command.TransformLayout(currentImage, VkImageLayout.TransferSrcOptimal)
                do! Command.TransformLayout(backbuffer, VkImageLayout.TransferDstOptimal)
                do! Command.Blit(
                        currentImage.[ImageAspect.Color, 0, *],
                        srcRange,
                        backbuffer.[ImageAspect.Color, 0, *],
                        dstRange,
                        VkFilter.Nearest
                    )

                // finally the backbuffer needs to be in layout PresentSrcKhr
                do! Command.TransformLayout(backbuffer, VkImageLayout.PresentSrcKhr)

                // present the backbuffer
                do! x.Present

                return res
            }
        )

    member x.Dispose() =
        let o = System.Threading.Interlocked.Exchange(&disposed, 1)
        if o = 0 then
            // delete old things
            VkRaw.vkDestroySwapchainKHR(device.Handle, handle, NativePtr.zero)


            framebuffer |> Option.iter (fun framebuffer ->
                framebuffer.Attachments |> Map.iter (fun _ v ->
                    device.Delete v.Image
                    device.Delete v
                )
                device.Delete framebuffer
            )

            resolvedImage|> Option.iter device.Delete
            buffers |> Array.iter device.Delete

            handle <- VkSwapchainKHR.Null
            size <- V2i.Zero
            framebuffer <- None
            buffers <- Array.zeroCreate 0
            resolvedImage <- None
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
