namespace Aardvark.Rendering.Vulkan

open System
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Rendering.Vulkan
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open KHRSwapchain
open KHRSurface

#nowarn "9"
// #nowarn "51"

[<AutoOpen>]          
module ImageTrafoExtensions =
    type Box3i with
        member x.Transformed(t : ImageTrafo) =
            match t with
                | ImageTrafo.Identity -> 
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
        let handle = 
            native {
                let extent = VkExtent2D(size.X, size.Y)
                let surface = description.surface

                let colorUsage =
                    if description.samples = 1 then VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferDstBit
                    else VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferDstBit

                let! pInfo =
                    VkSwapchainCreateInfoKHR(
                        VkSwapchainCreateFlagsKHR.None,

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
                        VkCompositeAlphaFlagsKHR.OpaqueBit,
                        description.presentMode,
                        1u,
                        old
                    )

                let! pHandle = VkSwapchainKHR.Null
                VkRaw.vkCreateSwapchainKHR(device.Handle, pInfo, NativePtr.zero, pHandle)
                    |> check "could not create Swapchain"
                return !!pHandle
            }

        use token = device.Token

        let buffers = 
            native {
                let! pCount = 0u
                VkRaw.vkGetSwapchainImagesKHR(device.Handle, handle, pCount, NativePtr.zero)
                    |> check "could not get Swapchain Images"

                let imageHandles = Array.zeroCreate (int !!pCount)
                let! pImages = imageHandles
                VkRaw.vkGetSwapchainImagesKHR(device.Handle, handle, pCount, pImages)
                    |> check "could not get Swapchain Images"

                return imageHandles |> Array.map (fun handle ->
                    let image = 
                        new Image(
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
            }

        let colorView =
            let image = 
                device.CreateImage(
                    V3i(size.X, size.Y, 1), 1, 1, description.samples, 
                    TextureDimension.Texture2D, 
                    VkFormat.toTextureFormat description.colorFormat, 
                    VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit
                )
            // hacky-hack
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
                    token.Enqueue (Command.TransformLayout(image, VkImageLayout.DepthStencilAttachmentOptimal))

                    let view = device.CreateOutputImageView(image, 0, 1, 0, 1)

                    Some view
                | _ -> 
                    None

        let framebuffer =
            let attachments =
                match depthView with
                | Some depthView -> Map.ofList [ DefaultSemantic.Colors, colorView; DefaultSemantic.DepthStencil, depthView]
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
                        VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit
                    )
                // hacky-hack
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
                        v.Image.Dispose()
                    )
                    framebuffer.Dispose()
                )
                resolvedImage |> Option.iter Disposable.dispose
                buffers |> Array.iter Disposable.dispose

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

    member x.RenderFrame (render : Framebuffer -> unit) : bool =
        lock x (fun () ->
            if disposed <> 0 then 
                failf "cannot use disposed Swapchain"

            // the color-data is currently stored in colorView
            let mutable currentImage : Image = Unchecked.defaultof<_>
            let mutable backbuffer  : Image = Unchecked.defaultof<_>

            device.perform {
                update() 
                let framebuffer = framebuffer.Value
                let colorView = framebuffer.Attachments.[DefaultSemantic.Colors]
                let depthView = Map.tryFind DefaultSemantic.DepthStencil framebuffer.Attachments

                // resolve(renderView, resolveView)
                // blit(resolveView, transformView)

                // clear color & depth

                //do! Command.PerDevice(fun di ->
                //        let color = if di = 0 then C4f.Green else C4f.Blue
                //        Command.ClearColor(colorView.Image.[TextureAspect.Color], color)
                //    )

                do! Command.ClearColor(colorView.Image.[TextureAspect.Color], C4f.Black)
                match depthView with
                | Some v -> do! Command.ClearDepthStencil(v.Image.[TextureAspect.DepthStencil], 1.0, 0u)
                | _ -> ()

                // ensure that colorImage is ColorAttachmentOptimal
                do! Command.TransformLayout(colorView.Image, VkImageLayout.ColorAttachmentOptimal)

                // render the scene
                render framebuffer

                // the color-data is currently stored in colorView
                currentImage <- colorView.Image

                // if the colorView is multisampled we need to resolve it to a temporary 
                // single-sampled image (resolvedImage)
                match resolvedImage with
                | Some resolvedImage ->
                    // resolve multisamples
                    do! Command.TransformLayout(colorView.Image, VkImageLayout.TransferSrcOptimal)
                    do! Command.TransformLayout(resolvedImage, VkImageLayout.TransferDstOptimal)

                    if device.AllCount > 1u then
                        let size = framebuffer.Size
                        let range =
                            {
                                frMin = V2i.Zero
                                frMax = size - V2i.II
                                frLayers = Range1i(0,renderPass.LayerCount-1)
                            }
                        let ranges = range.Split(int device.AllCount)
                        do! Command.PerDevice (fun di ->
                            let myRange = ranges.[di]

                            Command.ResolveMultisamples(
                                colorView.Image.[TextureAspect.Color, 0, *],
                                V3i(myRange.frMin, 0),
                                resolvedImage.[TextureAspect.Color, 0, *],
                                V3i(myRange.frMin, 0),
                                V3i(V2i.II + myRange.frMax - myRange.frMin, 1)
                            )
                        )
                    else
                        do! Command.ResolveMultisamples(colorView.Image.[TextureAspect.Color, 0, *], resolvedImage.[TextureAspect.Color, 0, *])

                    // the color-data is now stored in the resolved image
                    currentImage <- resolvedImage
                | None ->
                    ()

                if device.AllCount > 1u then
                    //do! Command.TransformLayout(currentImage, VkImageLayout.TransferSrcOptimal)
                    do! Command.SyncPeersDefault(currentImage, VkImageLayout.TransferSrcOptimal)
                    //do! Command.TransformLayout(currentImage, VkImageLayout.TransferDstOptimal)
                else
                    do! Command.TransformLayout(currentImage, VkImageLayout.TransferSrcOptimal)

                // since the blit might include an ImageTrafo we need to compute
                // appropriate ranges
                let srcRange = Box3i(V3i(0,0,0), V3i(size.X - 1, size.Y - 1, 0))
                let dstRange = srcRange.Transformed description.blitTrafo

                // acquire a swapchain image for rendering
                let! acquire = x.AcquireNextImage
                if acquire = QueueCommandResult.Success then

                    // determine color and output images (may differ when using MSAA)
                    let outputIndex = int !currentBuffer
                    backbuffer <- buffers.[outputIndex].Image

                    // blit the current image to the final backbuffer using the ranges from above
                    //do! Command.TransformLayout(currentImage, VkImageLayout.TransferSrcOptimal)
                    do! Command.TransformLayout(backbuffer, VkImageLayout.TransferDstOptimal)
                    do! Command.Blit(
                            currentImage.[TextureAspect.Color, 0, *],
                            VkImageLayout.TransferSrcOptimal,
                            srcRange,
                            backbuffer.[TextureAspect.Color, 0, *],
                            VkImageLayout.TransferDstOptimal,
                            dstRange,
                            VkFilter.Nearest
                        )

                    // finally the backbuffer needs to be in layout PresentSrcKhr
                    do! Command.TransformLayout(backbuffer, VkImageLayout.PresentSrcKhr)

                    // present the backbuffer
                    let! present = x.Present
                    return present = QueueCommandResult.Success

                else
                    return false
            }
        )

    member x.Dispose() =
        let o = System.Threading.Interlocked.Exchange(&disposed, 1)
        if o = 0 then
            // delete old things
            VkRaw.vkDestroySwapchainKHR(device.Handle, handle, NativePtr.zero)
            
            framebuffer |> Option.iter (fun framebuffer ->
                framebuffer.Attachments |> Map.iter (fun _ v ->
                    v.Image.Dispose()
                )
                framebuffer.Dispose()
            )

            resolvedImage|> Option.iter Disposable.dispose
            buffers |> Array.iter Disposable.dispose

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

[<AbstractClass; Sealed; Extension>]
type DeviceSwapchainExtensions private() =

    [<Extension>]
    static member CreateSwapchain(this : Device, description : SwapchainDescription) =
        this |> Swapchain.create description
