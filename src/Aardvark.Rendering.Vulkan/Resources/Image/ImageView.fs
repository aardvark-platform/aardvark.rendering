namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
#nowarn "9"
// #nowarn "51"

type ImageView =
    class
        inherit Resource<VkImageView>
        val public Image            : Image
        val public ImageViewType    : VkImageViewType
        val public Aspect           : TextureAspect
        val public MipLevelRange    : Range1i
        val public ArrayRange       : Range1i
        val public IsResolved       : bool

        override x.Destroy() =
            if x.Device.Handle <> 0n && x.Handle.IsValid then
                VkRaw.vkDestroyImageView(x.Device.Handle, x.Handle, NativePtr.zero)
                x.Handle <- VkImageView.Null

                if x.IsResolved then
                    x.Image.Dispose()


        interface IFramebufferOutput with
            member x.Runtime = x.Device.Runtime :> ITextureRuntime
            member x.Format = (x.Image :> IBackendTexture).Format
            member x.Samples = x.Image.Samples
            member x.Size = 
                let s = x.Image.Size
                let d = 1 <<< x.MipLevelRange.Min
                V2i(max 1 (s.X / d), max 1 (s.Y / d))

        interface ITextureRange with
            member x.Texture = x.Image :> IBackendTexture
            member x.Levels = x.MipLevelRange
            member x.Slices = x.ArrayRange
            member x.Aspect = x.Aspect

        interface ITextureLevel with
            member x.Level = x.MipLevelRange.Min
            member x.Size = 
                let s = x.Image.Size
                let d = 1 <<< x.MipLevelRange.Min
                V3i(max 1 (s.X / d), max 1 (s.Y / d), max 1 (s.Z / d))

        new(device : Device, handle : VkImageView, img, viewType, aspect, levelRange, arrayRange, resolved) =
            { inherit Resource<_>(device, handle);
                Image = img;
                ImageViewType = viewType;
                Aspect = aspect;
                MipLevelRange = levelRange;
                ArrayRange = arrayRange;
                IsResolved = resolved
            }
    end


[<AutoOpen>]
module ImageViewCommandExtensions =

    type Command with

        static member TransformLayout(view : ImageView, target : VkImageLayout) =
            Command.TransformLayout(view.Image, view.MipLevelRange, view.ArrayRange, target)

        static member inline ClearColor(view : ImageView, aspect : TextureAspect, color : ^Color) =
            let levels = view.MipLevelRange
            let slices = view.ArrayRange
            Command.ClearColor(view.Image.[aspect, levels.Min .. levels.Max, slices.Min .. slices.Max], color)

        static member inline ClearDepthStencil(view : ImageView, aspect : TextureAspect, depth : ^Depth, stencil : ^Stencil) =
            let levels = view.MipLevelRange
            let slices = view.ArrayRange
            Command.ClearDepthStencil(view.Image.[aspect, levels.Min .. levels.Max, slices.Min .. slices.Max], depth, stencil)

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

    let private viewTypeTex (count : int) (dim : TextureDimension) =
        let isArray = count > 1

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
                if isArray then
                    if count % 6 <> 0 then failf "ill-aligned cube-count %A" count
                    if count / 6 > 1 then VkImageViewType.CubeArray
                    else VkImageViewType.Cube
                else
                    VkImageViewType.D2d

            | _ ->
                failf "invalid view type: %A" (count, isArray, dim)

    let createInputView (componentMapping : VkComponentMapping) (img : Image) (samplerType : FShade.GLSL.GLSLSamplerType) (levelRange : Range1i) (arrayRange : Range1i) (device : Device) =
        let levels = 1 + levelRange.Max - levelRange.Min |> min img.MipMapLevels
        let slices = 1 + arrayRange.Max - arrayRange.Min |> min img.Layers
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
                    let resolved = device.CreateImage(img.Size, levels, slices, 1, img.Dimension, VkFormat.toTextureFormat img.Format, VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit)

                    let srcLayout = img.Layout

                    device.eventually {
                        do! Command.TransformLayout(img, VkImageLayout.TransferSrcOptimal)
                        do! Command.TransformLayout(resolved, VkImageLayout.TransferDstOptimal)
                        do! Command.ResolveMultisamples(img.[TextureAspect.Color, 0], resolved.[TextureAspect.Color, 0])
                        do! Command.TransformLayout(resolved, VkImageLayout.ShaderReadOnlyOptimal)
                        do! Command.TransformLayout(img, srcLayout)
                    }

                    true, resolved
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
                    VkImageViewCreateFlags.None,
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

            return new ImageView(device, !!pHandle, img, viewType, VkImageAspectFlags.toTextureAspect aspect, levelRange, arrayRange, isResolved)
        }

    let createStorageView (componentMapping : VkComponentMapping) (img : Image) (imageType : FShade.GLSL.GLSLImageType) (levelRange : Range1i) (arrayRange : Range1i) (device : Device) =
        let levels = 1 + levelRange.Max - levelRange.Min |> min img.MipMapLevels
        let slices = 1 + arrayRange.Max - arrayRange.Min |> min img.Layers
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
                        do! Command.ResolveMultisamples(img.[TextureAspect.Color, 0], temp.[TextureAspect.Color, 0])
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
                    VkImageViewCreateFlags.None,
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

            return new ImageView(device, !!pHandle, img, viewType, VkImageAspectFlags.toTextureAspect aspect, levelRange, arrayRange, isResolved)
        }

    let createOutputView (img : Image) (levelRange : Range1i) (arrayRange : Range1i) (device : Device) =
        let levels = 1 + levelRange.Max - levelRange.Min |> min img.MipMapLevels
        let slices = 1 + arrayRange.Max - arrayRange.Min |> min img.Layers
        if levels < 1 then failf "cannot create image view with level-count: %A" levels
        if slices < 1 then failf "cannot create image view with slice-count: %A" levels

        let aspect = VkFormat.toShaderAspect img.Format

        let viewType = viewTypeTex slices img.Dimension
        native {
            let! pInfo = 
                VkImageViewCreateInfo(
                    VkImageViewCreateFlags.None,
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

            return new ImageView(device, !!pHandle, img, viewType, VkImageAspectFlags.toTextureAspect aspect, levelRange, arrayRange, false)
        }

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
        this |> ImageView.createInputView comp image samplerType levelRange (Range1i(0, image.Layers - 1))

    [<Extension>]
    static member inline CreateInputImageView(this : Device, image : Image, samplerType : FShade.GLSL.GLSLSamplerType, comp : VkComponentMapping) =
        this |> ImageView.createInputView comp image samplerType (Range1i(0, image.MipMapLevels - 1)) (Range1i(0, image.Layers - 1))

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
        this |> ImageView.createStorageView comp image imageType levelRange (Range1i(0, image.Layers - 1))

    [<Extension>]
    static member inline CreateStorageView(this : Device, image : Image, imageType : FShade.GLSL.GLSLImageType, comp : VkComponentMapping) =
        this |> ImageView.createStorageView comp image imageType (Range1i(0, image.MipMapLevels - 1)) (Range1i(0, image.Layers - 1))

    [<Extension>]
    static member inline CreateStorageView(this : Device, image : Image, imageType : FShade.GLSL.GLSLImageType, level : int, slice : int, comp : VkComponentMapping) =
        this |> ImageView.createStorageView comp image imageType (Range1i(level, level)) (Range1i(slice, slice))




    [<Extension>]
    static member inline CreateOutputImageView(this : Device, image : Image, levelRange : Range1i, arrayRange : Range1i) =
        this |> ImageView.createOutputView image levelRange arrayRange

    [<Extension>]
    static member inline CreateOutputImageView(this : Device, image : Image, baseLevel : int, levels : int, baseSlice : int, slices : int) =
        this |> ImageView.createOutputView image (Range1i(baseLevel, baseLevel + levels - 1)) (Range1i(baseSlice, baseSlice + slices - 1))

    [<Extension>]
    static member inline CreateOutputImageView(this : Device, image : Image, levelRange : Range1i) =
        this |> ImageView.createOutputView image levelRange (Range1i(0, image.Layers - 1))

    [<Extension>]
    static member inline CreateOutputImageView(this : Device, image : Image) =
        this |> ImageView.createOutputView image (Range1i(0, image.MipMapLevels - 1)) (Range1i(0, image.Layers - 1))

    [<Extension>]
    static member inline CreateOutputImageView(this : Device, image : Image, level : int, slice : int) =
        this |> ImageView.createOutputView image (Range1i(level, level)) (Range1i(slice, slice))