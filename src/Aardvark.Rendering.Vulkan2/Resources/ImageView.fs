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

        interface IBackendTextureOutputView with
            member x.texture = x.Image :> IBackendTexture
            member x.level = x.MipLevelRange.Min
            member x.slice = x.ArrayRange.Min

        interface IFramebufferOutput with
            member x.Format = VkFormat.toTextureFormat x.Image.Format |> TextureFormat.toRenderbufferFormat
            member x.Samples = x.Image.Samples
            member x.Size = x.Image.Size.XY

        new(device : Device, handle : VkImageView, img, viewType, levelRange, arrayRange) = { inherit Resource<_>(device, handle); Image = img; ImageViewType = viewType; MipLevelRange = levelRange; ArrayRange = arrayRange }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ImageView =

    let private viewType (imageType : TextureDimension) (count : int) =
        match imageType with
            | TextureDimension.Texture1D ->
                if count = 1 then VkImageViewType.D1d
                else VkImageViewType.D1dArray

            | TextureDimension.Texture2D ->
                if count = 1 then VkImageViewType.D2d
                else VkImageViewType.D2dArray

            | TextureDimension.Texture3D ->
                if count = 1 then VkImageViewType.D3d
                else failf "3d array textures not supported"

            | TextureDimension.TextureCube ->
                if count % 6 <> 0 then failf "ill-aligned cube-count %A" count
                if count = 6 then VkImageViewType.Cube
                else VkImageViewType.CubeArray

            | _ ->
                failf "invalid image type: %A" imageType

    let create (img : Image) (levelRange : Range1i) (arrayRange : Range1i) (device : Device) =
        let levels = 1 + levelRange.Max - levelRange.Min
        let slices = 1 + arrayRange.Max - arrayRange.Min
        if levels < 1 then failf "cannot create image view with level-count: %A" levels
        if slices < 1 then failf "cannot create image view with slice-count: %A" levels

        let aspect = VkFormat.toAspect img.Format

        let viewType = viewType img.Dimension slices
        let mutable info = 
            VkImageViewCreateInfo(
                VkStructureType.ImageViewCreateInfo, 0n,
                VkImageViewCreateFlags.MinValue,
                img.Handle,
                viewType, 
                img.Format,
                img.ComponentMapping,
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

        ImageView(device, handle, img, viewType, levelRange, arrayRange)

    let delete (view : ImageView) (device : Device) =
        if device.Handle <> 0n && view.Handle.IsValid then
            VkRaw.vkDestroyImageView(device.Handle, view.Handle, NativePtr.zero)
            view.Handle <- VkImageView.Null

[<AbstractClass; Sealed; Extension>]
type ContextImageViewExtensions private() =

    [<Extension>]
    static member inline CreateImageView(this : Device, image : Image, levelRange : Range1i, arrayRange : Range1i) =
        this |> ImageView.create image levelRange arrayRange

    [<Extension>]
    static member inline CreateImageView(this : Device, image : Image, baseLevel : int, levels : int, baseSlice : int, slices : int) =
        this |> ImageView.create image (Range1i(baseLevel, baseLevel + levels - 1)) (Range1i(baseSlice, baseSlice + slices - 1))

    [<Extension>]
    static member inline CreateImageView(this : Device, image : Image, levelRange : Range1i) =
        this |> ImageView.create image levelRange (Range1i(0, image.Count - 1))

    [<Extension>]
    static member inline CreateImageView(this : Device, image : Image) =
        this |> ImageView.create image (Range1i(0, image.MipMapLevels - 1)) (Range1i(0, image.Count - 1))

    [<Extension>]
    static member inline CreateImageView(this : Device, image : Image, level : int, slice : int) =
        this |> ImageView.create image (Range1i(level, level)) (Range1i(slice, slice))



    [<Extension>]
    static member inline Delete(this : Device, view : ImageView) =
        this |> ImageView.delete view