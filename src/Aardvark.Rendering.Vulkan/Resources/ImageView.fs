namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

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
            member x.slice = x.ArrayRange.Min

        interface IFramebufferOutput with
            member x.Format = VkFormat.toTextureFormat x.Image.Format |> TextureFormat.toRenderbufferFormat
            member x.Samples = x.Image.Samples
            member x.Size = x.Image.Size.XY

        new(device : Device, handle : VkImageView, img, viewType, levelRange, arrayRange, resolved) = { inherit Resource<_>(device, handle); Image = img; ImageViewType = viewType; MipLevelRange = levelRange; ArrayRange = arrayRange; IsResolved = resolved }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ImageView =

    let private viewType (count : int) (isArray : bool) (dim : TextureDimension) =
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

    let createInputView (componentMapping : VkComponentMapping) (img : Image) (samplerType : ShaderSamplerType) (levelRange : Range1i) (arrayRange : Range1i) (device : Device) =
        let levels = 1 + levelRange.Max - levelRange.Min |> min img.MipMapLevels
        let slices = 1 + arrayRange.Max - arrayRange.Min |> min img.Count
        if levels < 1 then failf "cannot create image view with level-count: %A" levels
        if slices < 1 then failf "cannot create image view with slice-count: %A" levels

        let aspect = VkFormat.toShaderAspect img.Format


        let isResolved, img = 
            if samplerType.isMultisampled then
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

        let viewType = viewType slices samplerType.isArray samplerType.dimension
        let mutable info = 
            VkImageViewCreateInfo(
                VkStructureType.ImageViewCreateInfo, 0n,
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
        let mutable handle = VkImageView.Null
        VkRaw.vkCreateImageView(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "could not create image view"

        ImageView(device, handle, img, viewType, levelRange, arrayRange, isResolved)

    let createOutpuView (img : Image) (levelRange : Range1i) (arrayRange : Range1i) (device : Device) =
        let levels = 1 + levelRange.Max - levelRange.Min |> min img.MipMapLevels
        let slices = 1 + arrayRange.Max - arrayRange.Min |> min img.Count
        if levels < 1 then failf "cannot create image view with level-count: %A" levels
        if slices < 1 then failf "cannot create image view with slice-count: %A" levels

        let aspect = VkFormat.toShaderAspect img.Format

        let viewType = viewType slices (slices > 1) img.Dimension
        let mutable info = 
            VkImageViewCreateInfo(
                VkStructureType.ImageViewCreateInfo, 0n,
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
        let mutable handle = VkImageView.Null
        VkRaw.vkCreateImageView(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "could not create image view"

        ImageView(device, handle, img, viewType, levelRange, arrayRange, false)

    let delete (view : ImageView) (device : Device) =
        if device.Handle <> 0n && view.Handle.IsValid then
            VkRaw.vkDestroyImageView(device.Handle, view.Handle, NativePtr.zero)
            view.Handle <- VkImageView.Null

            if view.IsResolved then
                device.Delete view.Image

[<AbstractClass; Sealed; Extension>]
type ContextImageViewExtensions private() =

    [<Extension>]
    static member inline CreateInputImageView(this : Device, image : Image, samplerType : ShaderSamplerType, levelRange : Range1i, arrayRange : Range1i, comp : VkComponentMapping) =
        this |> ImageView.createInputView comp image samplerType levelRange arrayRange

    [<Extension>]
    static member inline CreateInputImageView(this : Device, image : Image, samplerType : ShaderSamplerType, baseLevel : int, levels : int, baseSlice : int, slices : int, comp : VkComponentMapping) =
        this |> ImageView.createInputView comp image samplerType (Range1i(baseLevel, baseLevel + levels - 1)) (Range1i(baseSlice, baseSlice + slices - 1))

    [<Extension>]
    static member inline CreateInputImageView(this : Device, image : Image, samplerType : ShaderSamplerType, levelRange : Range1i, comp : VkComponentMapping) =
        this |> ImageView.createInputView comp image samplerType levelRange (Range1i(0, image.Count - 1))

    [<Extension>]
    static member inline CreateInputImageView(this : Device, image : Image, samplerType : ShaderSamplerType, comp : VkComponentMapping) =
        this |> ImageView.createInputView comp image samplerType (Range1i(0, image.MipMapLevels - 1)) (Range1i(0, image.Count - 1))

    [<Extension>]
    static member inline CreateInputImageView(this : Device, image : Image, samplerType : ShaderSamplerType, level : int, slice : int, comp : VkComponentMapping) =
        this |> ImageView.createInputView comp image samplerType (Range1i(level, level)) (Range1i(slice, slice))


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