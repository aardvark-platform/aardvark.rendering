namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
#nowarn "9"
// #nowarn "51"


[<AutoOpen>]
module ImageViewCommandExtensions =
    
    type Command with

        static member SetDeviceMask(mask : uint32) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd) =
                    cmd.AppendCommand()
                    VkRaw.vkCmdSetDeviceMask(cmd.Handle, mask)
                    Disposable.Empty        
            }

        static member SyncPeersDefault(img : Image, dstLayout : VkImageLayout) =
            if img.PeerHandles.Length > 0 then
                let device = img.Device
                let arrayRange = Range1i(0, img.Count - 1)
                let ranges =
                    let range = 
                        { 
                            frMin = V2i.Zero; 
                            frMax = img.Size.XY - V2i.II
                            frLayers = arrayRange
                        }
                    range.Split(int device.AllCount)

                command {
                    do! Command.TransformLayout(img, VkImageLayout.TransferSrcOptimal)
                    let layers = arrayRange
                    let layerCount = 1 + layers.Max - layers.Min
                        
                    let aspect =
                        match VkFormat.toImageKind img.Format with
                            | ImageKind.Depth -> ImageAspect.Depth
                            | ImageKind.DepthStencil  -> ImageAspect.DepthStencil
                            | _ -> ImageAspect.Color 

                    let subResource = img.[aspect, 0]
                    let ranges =
                        ranges |> Array.map (fun { frMin = min; frMax = max; frLayers = layers} ->
                            layers, Box3i(V3i(min,0), V3i(max, 0))
                        )

                    do! Command.SyncPeers(subResource, ranges)
                    do! Command.TransformLayout(img, dstLayout)
                }
            else
                Command.nop


type ImageView =
    class
        inherit Resource<VkImageView>
        val mutable public Image            : Image
        val mutable public ImageViewType    : VkImageViewType
        val mutable public MipLevelRange    : Range1i
        val mutable public ArrayRange       : Range1i
        val mutable public IsResolved       : bool

        interface IBackendTextureOutputView with
            member x.texture = x.Image :> IBackendTexture
            member x.level = x.MipLevelRange.Min
            member x.slices = x.ArrayRange

        interface IFramebufferOutput with
            member x.Runtime = x.Device.Runtime :> ITextureRuntime
            member x.Format = VkFormat.toTextureFormat x.Image.Format |> TextureFormat.toRenderbufferFormat
            member x.Samples = x.Image.Samples
            member x.Size = 
                let s = x.Image.Size
                let d = 1 <<< x.MipLevelRange.Min
                V2i(max 1 (s.X / d), max 1 (s.Y / d))

        interface ITextureRange with
            member x.Texture = x.Image :> IBackendTexture
            member x.Levels = x.MipLevelRange
            member x.Slices = x.ArrayRange
            member x.Aspect = 
                if VkFormat.hasDepth x.Image.Format then TextureAspect.Depth
                else TextureAspect.Color
                
        interface ITextureLevel with
            member x.Level = x.MipLevelRange.Min
            member x.Size = 
                let s = x.Image.Size
                let d = 1 <<< x.MipLevelRange.Min
                V3i(max 1 (s.X / d), max 1 (s.Y / d), max 1 (s.Z / d))

        new(device : Device, handle : VkImageView, img, viewType, levelRange, arrayRange, resolved) = { inherit Resource<_>(device, handle); Image = img; ImageViewType = viewType; MipLevelRange = levelRange; ArrayRange = arrayRange; IsResolved = resolved }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ImageView =

    let private viewType (count : int) (isArray : bool) (dim : FShade.SamplerDimension) =
        match dim with
            | FShade.SamplerDimension.Sampler1d ->
                if isArray then VkImageViewType.D1dArray
                else VkImageViewType.D1d

            | FShade.SamplerDimension.Sampler2d ->
                if isArray then VkImageViewType.D2dArray
                else VkImageViewType.D2d

            | FShade.SamplerDimension.Sampler3d ->
                if isArray then failf "3d array textures not supported"
                else VkImageViewType.D3d

            | FShade.SamplerDimension.SamplerCube ->
                if count % 6 <> 0 then failf "ill-aligned cube-count %A" count
                if isArray then VkImageViewType.CubeArray
                else VkImageViewType.Cube

            | _ ->
                failf "invalid view type: %A" (count, isArray, dim)

    let private viewTypeTex (count : int) (isArray : bool) (dim : TextureDimension) =
        match dim with
            | TextureDimension.Texture1D ->
                if isArray then VkImageViewType.D1dArray
                else VkImageViewType.D1d

            | TextureDimension.Texture2D ->
                if isArray then VkImageViewType.D2dArray
                else VkImageViewType.D2d

            | TextureDimension.Texture3D ->
                if isArray then failf "3d array textures not supported"
                else VkImageViewType.D3d

            | TextureDimension.TextureCube ->
                if count % 6 <> 0 then failf "ill-aligned cube-count %A" count
                if isArray then VkImageViewType.CubeArray
                else VkImageViewType.Cube

            | _ ->
                failf "invalid view type: %A" (count, isArray, dim)

    let createInputView (componentMapping : VkComponentMapping) (img : Image) (samplerType : FShade.GLSL.GLSLSamplerType) (levelRange : Range1i) (arrayRange : Range1i) (device : Device) =
        let levels = 1 + levelRange.Max - levelRange.Min |> min img.MipMapLevels
        let slices = 1 + arrayRange.Max - arrayRange.Min |> min img.Count
        if levels < 1 then failf "cannot create image view with level-count: %A" levels
        if slices < 1 then failf "cannot create image view with slice-count: %A" levels

        let aspect = VkFormat.toShaderAspect img.Format


        let isResolved, img = 
            if samplerType.isMS then
                if img.Samples = 1 then
                    failf "cannot use non-ms image as ms sampler"
                else
                    false, img
            else
                if img.Samples > 1 then
                    Log.line "resolve"
                    let temp = device.CreateImage(img.Size, levels, slices, 1, img.Dimension, VkFormat.toTextureFormat img.Format, VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit)

                    device.eventually {
                        do! Command.TransformLayout(temp, VkImageLayout.TransferDstOptimal)
                        do! Command.ResolveMultisamples(img.[ImageAspect.Color, 0], temp.[ImageAspect.Color, 0])
                        do! Command.TransformLayout(temp, VkImageLayout.ShaderReadOnlyOptimal)
                    }

                    true, temp
                else
                    false, img

        if img.PeerHandles.Length > 0 then
            device.eventually {
                do! Command.SyncPeersDefault(img, VkImageLayout.ShaderReadOnlyOptimal)
            }

        native {
            let viewType = viewType slices samplerType.isArray samplerType.dimension
            let! pInfo = 
                VkImageViewCreateInfo(
                    VkImageViewCreateFlags.MinValue,
                    img.Handle,
                    viewType, 
                    img.Format,
                    componentMapping,
                    VkImageSubresourceRange(
                        aspect, 
                        uint32 levelRange.Min,
                        uint32 levels,
                        uint32 arrayRange.Min,
                        uint32 slices
                    )
                )
            let! pHandle = VkImageView.Null
            VkRaw.vkCreateImageView(device.Handle, pInfo, NativePtr.zero, pHandle)
                |> check "could not create image view"

            return ImageView(device, !!pHandle, img, viewType, levelRange, arrayRange, isResolved)
        }
    let createStorageView (componentMapping : VkComponentMapping) (img : Image) (imageType : FShade.GLSL.GLSLImageType) (levelRange : Range1i) (arrayRange : Range1i) (device : Device) =
        let levels = 1 + levelRange.Max - levelRange.Min |> min img.MipMapLevels
        let slices = 1 + arrayRange.Max - arrayRange.Min |> min img.Count
        if levels < 1 then failf "cannot create image view with level-count: %A" levels
        if slices < 1 then failf "cannot create image view with slice-count: %A" levels

        let aspect = VkFormat.toShaderAspect img.Format


        let isResolved, img = 
            if imageType.isMS then
                if img.Samples = 1 then
                    failf "cannot use non-ms image as ms sampler"
                else
                    false, img
            else
                if img.Samples > 1 then
                    Log.line "resolve"
                    let temp = device.CreateImage(img.Size, levels, slices, 1, img.Dimension, VkFormat.toTextureFormat img.Format, VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit)

                    device.eventually {
                        do! Command.TransformLayout(temp, VkImageLayout.TransferDstOptimal)
                        do! Command.ResolveMultisamples(img.[ImageAspect.Color, 0], temp.[ImageAspect.Color, 0])
                        do! Command.TransformLayout(temp, VkImageLayout.ShaderReadOnlyOptimal)
                    }

                    true, temp
                else
                    false, img

        if img.PeerHandles.Length > 0 then
            device.eventually {
                do! Command.SyncPeersDefault(img, VkImageLayout.ShaderReadOnlyOptimal)
            }

        let viewType = viewType slices imageType.isArray imageType.dimension
        native {
            let! pInfo = 
                VkImageViewCreateInfo(
                    VkImageViewCreateFlags.MinValue,
                    img.Handle,
                    viewType, 
                    img.Format,
                    componentMapping,
                    VkImageSubresourceRange(
                        aspect, 
                        uint32 levelRange.Min,
                        uint32 levels,
                        uint32 arrayRange.Min,
                        uint32 slices
                    )
                )
            let! pHandle = VkImageView.Null
            VkRaw.vkCreateImageView(device.Handle, pInfo, NativePtr.zero, pHandle)
                |> check "could not create image view"

            return ImageView(device, !!pHandle, img, viewType, levelRange, arrayRange, isResolved)
        }

    let createOutpuView (img : Image) (levelRange : Range1i) (arrayRange : Range1i) (device : Device) =
        let levels = 1 + levelRange.Max - levelRange.Min |> min img.MipMapLevels
        let slices = 1 + arrayRange.Max - arrayRange.Min |> min img.Count
        if levels < 1 then failf "cannot create image view with level-count: %A" levels
        if slices < 1 then failf "cannot create image view with slice-count: %A" levels

        let aspect = VkFormat.toShaderAspect img.Format

        let viewType = viewTypeTex slices (slices > 1) img.Dimension
        native {
            let! pInfo = 
                VkImageViewCreateInfo(
                    VkImageViewCreateFlags.MinValue,
                    img.Handle,
                    viewType, 
                    img.Format,
                    VkComponentMapping.Identity,
                    VkImageSubresourceRange(
                        aspect, 
                        uint32 levelRange.Min,
                        uint32 levels,
                        uint32 arrayRange.Min,
                        uint32 slices
                    )
                )
            let! pHandle = VkImageView.Null
            VkRaw.vkCreateImageView(device.Handle, pInfo, NativePtr.zero, pHandle)
                |> check "could not create image view"

            return ImageView(device, !!pHandle, img, viewType, levelRange, arrayRange, false)
        }

    let delete (view : ImageView) (device : Device) =
        if device.Handle <> 0n && view.Handle.IsValid then
            VkRaw.vkDestroyImageView(device.Handle, view.Handle, NativePtr.zero)
            view.Handle <- VkImageView.Null

            if view.IsResolved then
                device.Delete view.Image




[<AbstractClass; Sealed; Extension>]
type ContextImageViewExtensions private() =

    [<Extension>]
    static member inline CreateInputImageView(this : Device, image : Image, samplerType : FShade.GLSL.GLSLSamplerType, levelRange : Range1i, arrayRange : Range1i, comp : VkComponentMapping) =
        this |> ImageView.createInputView comp image samplerType levelRange arrayRange

    [<Extension>]
    static member inline CreateInputImageView(this : Device, image : Image, samplerType : FShade.GLSL.GLSLSamplerType, baseLevel : int, levels : int, baseSlice : int, slices : int, comp : VkComponentMapping) =
        this |> ImageView.createInputView comp image samplerType (Range1i(baseLevel, baseLevel + levels - 1)) (Range1i(baseSlice, baseSlice + slices - 1))

    [<Extension>]
    static member inline CreateInputImageView(this : Device, image : Image, samplerType : FShade.GLSL.GLSLSamplerType, levelRange : Range1i, comp : VkComponentMapping) =
        this |> ImageView.createInputView comp image samplerType levelRange (Range1i(0, image.Count - 1))

    [<Extension>]
    static member inline CreateInputImageView(this : Device, image : Image, samplerType : FShade.GLSL.GLSLSamplerType, comp : VkComponentMapping) =
        this |> ImageView.createInputView comp image samplerType (Range1i(0, image.MipMapLevels - 1)) (Range1i(0, image.Count - 1))

    [<Extension>]
    static member inline CreateInputImageView(this : Device, image : Image, samplerType : FShade.GLSL.GLSLSamplerType, level : int, slice : int, comp : VkComponentMapping) =
        this |> ImageView.createInputView comp image samplerType (Range1i(level, level)) (Range1i(slice, slice))



    [<Extension>]
    static member inline CreateStorageView(this : Device, image : Image, imageType : FShade.GLSL.GLSLImageType, levelRange : Range1i, arrayRange : Range1i, comp : VkComponentMapping) =
        this |> ImageView.createStorageView comp image imageType levelRange arrayRange

    [<Extension>]
    static member inline CreateStorageView(this : Device, image : Image, imageType : FShade.GLSL.GLSLImageType, baseLevel : int, levels : int, baseSlice : int, slices : int, comp : VkComponentMapping) =
        this |> ImageView.createStorageView comp image imageType (Range1i(baseLevel, baseLevel + levels - 1)) (Range1i(baseSlice, baseSlice + slices - 1))

    [<Extension>]
    static member inline CreateStorageView(this : Device, image : Image, imageType : FShade.GLSL.GLSLImageType, levelRange : Range1i, comp : VkComponentMapping) =
        this |> ImageView.createStorageView comp image imageType levelRange (Range1i(0, image.Count - 1))

    [<Extension>]
    static member inline CreateStorageView(this : Device, image : Image, imageType : FShade.GLSL.GLSLImageType, comp : VkComponentMapping) =
        this |> ImageView.createStorageView comp image imageType (Range1i(0, image.MipMapLevels - 1)) (Range1i(0, image.Count - 1))

    [<Extension>]
    static member inline CreateStorageView(this : Device, image : Image, imageType : FShade.GLSL.GLSLImageType, level : int, slice : int, comp : VkComponentMapping) =
        this |> ImageView.createStorageView comp image imageType (Range1i(level, level)) (Range1i(slice, slice))




    [<Extension>]
    static member inline CreateOutputImageView(this : Device, image : Image, levelRange : Range1i, arrayRange : Range1i) =
        this |> ImageView.createOutpuView image levelRange arrayRange

    [<Extension>]
    static member inline CreateOutputImageView(this : Device, image : Image, baseLevel : int, levels : int, baseSlice : int, slices : int) =
        this |> ImageView.createOutpuView image (Range1i(baseLevel, baseLevel + levels - 1)) (Range1i(baseSlice, baseSlice + slices - 1))

    [<Extension>]
    static member inline CreateOutputImageView(this : Device, image : Image, levelRange : Range1i) =
        this |> ImageView.createOutpuView image levelRange (Range1i(0, image.Count - 1))

    [<Extension>]
    static member inline CreateOutputImageView(this : Device, image : Image) =
        this |> ImageView.createOutpuView image (Range1i(0, image.MipMapLevels - 1)) (Range1i(0, image.Count - 1))

    [<Extension>]
    static member inline CreateOutputImageView(this : Device, image : Image, level : int, slice : int) =
        this |> ImageView.createOutpuView image (Range1i(level, level)) (Range1i(slice, slice))



    [<Extension>]
    static member inline Delete(this : Device, view : ImageView) =
        this |> ImageView.delete view