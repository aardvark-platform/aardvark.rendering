namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Threading
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

type ImageView =
    class
        val mutable public Handle : VkImageView
        val mutable public Image : Image
        val mutable public ImageViewType : VkImageViewType
        val mutable public Format : VkFormat
        val mutable public ChannelMapping : VkComponentMapping
        val mutable public MipLevelRange : Range1i
        val mutable public ArrayRange : Range1i

        new(handle, image, viewType, format, cm, mip, arr) = { Handle = handle; Image = image; ImageViewType = viewType; Format = format; ChannelMapping = cm; MipLevelRange = mip; ArrayRange = arr }
    end


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkComponentMapping =
    type private C = VkComponentSwizzle
    let ofTextureFormat =
       
        let r = VkComponentSwizzle.R
        let g = VkComponentSwizzle.G
        let b = VkComponentSwizzle.B
        let a = VkComponentSwizzle.A
        let z = VkComponentSwizzle.Zero
        let i = VkComponentSwizzle.One

        let create a b c d = VkComponentMapping(a,b,c,d)

        lookupTable [
            TextureFormat.DualAlpha4Sgis, create r g z i
            TextureFormat.DualAlpha8Sgis, create r g z i
            TextureFormat.DualAlpha16Sgis, create r g z i
            TextureFormat.DualLuminance4Sgis, create r g z i
            TextureFormat.DualLuminance8Sgis, create r g z i
            TextureFormat.DualLuminance16Sgis, create r g z i
            TextureFormat.DualIntensity4Sgis, create r g z i
            TextureFormat.DualIntensity8Sgis, create r g z i
            TextureFormat.DualIntensity16Sgis, create r g z i
            TextureFormat.QuadAlpha4Sgis, create r g b a
            TextureFormat.QuadAlpha8Sgis, create r g b a
            TextureFormat.QuadLuminance4Sgis, create r g b a
            TextureFormat.QuadLuminance8Sgis, create r g b a
            TextureFormat.QuadIntensity4Sgis, create r g b a
            TextureFormat.QuadIntensity8Sgis, create r g b a
            TextureFormat.DepthComponent, create r r r i
            TextureFormat.Rgb, create r g b i
            TextureFormat.Rgba, create r g b a
            TextureFormat.Luminance, create r r r i
            TextureFormat.LuminanceAlpha, create r r r g
            TextureFormat.Rgb5, create r g b i
            TextureFormat.Rgb8, create r g b i
            TextureFormat.Rgb10, create r g b i
            TextureFormat.Rgb16, create r g b i
            TextureFormat.Rgba4, create r g b a
            TextureFormat.Rgb5A1, create r g b a
            TextureFormat.Rgba8, create r g b a
            TextureFormat.Rgb10A2, create r g b a
            TextureFormat.Rgba16, create r g b a
            TextureFormat.DepthComponent16, create r r r i
            TextureFormat.DepthComponent24, create r r r i
            TextureFormat.DepthComponent32, create r r r i
            TextureFormat.CompressedRg, create r g z i
            TextureFormat.R8, create r z z i
            TextureFormat.R16, create r z z i
            TextureFormat.Rg8, create r g z i
            TextureFormat.Rg16, create r g z i
            TextureFormat.R16f, create r z z i
            TextureFormat.R32f, create r z z i
            TextureFormat.Rg16f, create r g z i
            TextureFormat.Rg32f, create r g z i
            TextureFormat.R8i, create r z z i
            TextureFormat.R8ui, create r z z i
            TextureFormat.R16i, create r z z i
            TextureFormat.R16ui, create r z z i
            TextureFormat.R32i, create r z z i
            TextureFormat.R32ui, create r z z i
            TextureFormat.Rg8i, create r g z i
            TextureFormat.Rg8ui, create r g z i
            TextureFormat.Rg16i, create r g z i
            TextureFormat.Rg16ui, create r g z i
            TextureFormat.Rg32i, create r g z i
            TextureFormat.Rg32ui, create r g z i
            TextureFormat.RgbIccSgix, create r g b i
            TextureFormat.RgbaIccSgix, create r g b a
            TextureFormat.AlphaIccSgix, create z z z r
            TextureFormat.LuminanceIccSgix, create r r r i
            TextureFormat.IntensityIccSgix, create r r r i
            TextureFormat.LuminanceAlphaIccSgix, create r r r g
            TextureFormat.R5G6B5IccSgix, create r g b i
            TextureFormat.Alpha16IccSgix, create z z z r
            TextureFormat.Luminance16IccSgix, create r r r i
            TextureFormat.Intensity16IccSgix, create r r r i
            TextureFormat.CompressedRgb, create r g b i
            TextureFormat.CompressedRgba, create r g b a
            TextureFormat.DepthStencil, create r r r i
            TextureFormat.Rgba32f, create r g b a
            TextureFormat.Rgb32f, create r g b i
            TextureFormat.Rgba16f, create r g b a
            TextureFormat.Rgb16f, create r g b i
            TextureFormat.Depth24Stencil8, create r r r i
            TextureFormat.R11fG11fB10f, create b g r i
            TextureFormat.Rgb9E5, create b g r i
            TextureFormat.Srgb, create r g b i
            TextureFormat.Srgb8, create r g b i
            TextureFormat.SrgbAlpha, create r g b a
            TextureFormat.Srgb8Alpha8, create r g b a
            TextureFormat.SluminanceAlpha, create r r r g
            TextureFormat.Sluminance8Alpha8, create r r r g
            TextureFormat.Sluminance, create r r r i
            TextureFormat.Sluminance8, create r r r i
            TextureFormat.CompressedSrgb, create r g b i
            TextureFormat.CompressedSrgbAlpha, create r g b a
            TextureFormat.DepthComponent32f, create r r r i
            TextureFormat.Depth32fStencil8, create r r r i
            TextureFormat.Rgba32ui, create r g b a
            TextureFormat.Rgb32ui, create r g b i
            TextureFormat.Rgba16ui, create r g b a
            TextureFormat.Rgb16ui, create r g b i
            TextureFormat.Rgba8ui, create r g b a
            TextureFormat.Rgb8ui, create r g b i
            TextureFormat.Rgba32i, create r g b a
            TextureFormat.Rgb32i, create r g b i
            TextureFormat.Rgba16i, create r g b a
            TextureFormat.Rgb16i, create r g b i
            TextureFormat.Rgba8i, create r g b a
            TextureFormat.Rgb8i, create r g b i
            TextureFormat.Float32UnsignedInt248Rev, create r r r i
            TextureFormat.CompressedRgbaBptcUnorm, create r g b a
            TextureFormat.CompressedRgbBptcSignedFloat, create r g b a
            TextureFormat.CompressedRgbBptcUnsignedFloat, create r g b a
            TextureFormat.R8Snorm, create r z z i
            TextureFormat.Rg8Snorm, create r g z i
            TextureFormat.Rgb8Snorm, create r g b i
            TextureFormat.Rgba8Snorm, create r g b a
            TextureFormat.R16Snorm, create r z z i
            TextureFormat.Rg16Snorm, create r g z i
            TextureFormat.Rgb16Snorm, create r g b i
            TextureFormat.Rgba16Snorm, create r g b a
            TextureFormat.Rgb10A2ui, create r g b a
        ]

    let rgba =
        VkComponentMapping(
            VkComponentSwizzle.R,
            VkComponentSwizzle.G,
            VkComponentSwizzle.B, 
            VkComponentSwizzle.A
        )

[<AbstractClass; Sealed; Extension>]
type ImageViewExtensions private() =

    [<Extension>]
    static member Delete(this : Context, view : ImageView) =
        if view.Handle.IsValid then
            VkRaw.vkDestroyImageView(this.Device.Handle, view.Handle, NativePtr.zero)
            view.Handle <- VkImageView.Null

    [<Extension>]
    static member CreateImageView(this : Context, image : Image, viewType : VkImageViewType,
                                  levels : Range1i, slices : Range1i, aspect : VkImageAspectFlags) =
        let mapping = VkComponentMapping.ofTextureFormat image.TextureFormat

        let range = 
            VkImageSubresourceRange(
                aspect,
                uint32 levels.Min, 
                uint32 levels.Size + 1u, 
                uint32 slices.Min, 
                uint32 slices.Size + 1u
            )

        let mutable info =
            VkImageViewCreateInfo(
                VkStructureType.ImageViewCreateInfo,
                0n, VkImageViewCreateFlags.MinValue,
                image.Handle,
                viewType, 
                image.Format,
                mapping,
                range
            )

        let mutable view = VkImageView.Null
        VkRaw.vkCreateImageView(this.Device.Handle, &&info, NativePtr.zero, &&view) |> check "vkCreateImageView"

        ImageView(view, image, VkImageViewType.D2d, image.Format, mapping, Range1i(0, image.MipMapLevels-1), Range1i(0, 0))

    [<Extension>]
    static member CreateImageView(this : Context, image : Image, viewType : VkImageViewType,
                                  levels : Range1i, slices : Range1i) =
        ImageViewExtensions.CreateImageView(
            this,
            image,
            viewType,
            levels,
            slices, 
            VkImageAspectFlags.ColorBit
        )

    [<Extension>]
    static member CreateImageView(this : Context, image : Image, viewType : VkImageViewType,
                                  levels : Range1i) =
        ImageViewExtensions.CreateImageView(
            this,
            image,
            viewType,
            levels,
            Range1i(0, image.ArraySize-1), 
            VkImageAspectFlags.ColorBit
        )

    [<Extension>]
    static member CreateImageView(this : Context, image : Image, viewType : VkImageViewType) =
        ImageViewExtensions.CreateImageView(
            this,
            image,
            viewType,
            Range1i(0, image.MipMapLevels-1),
            Range1i(0, image.ArraySize-1), 
            VkImageAspectFlags.ColorBit
        )

    [<Extension>]
    static member CreateImageView(this : Context, image : Image) =
        let viewType =
            match image.ImageType with
                | VkImageType.D1d ->
                    if image.ArraySize > 1 then VkImageViewType.D1dArray
                    else VkImageViewType.D1d
                | VkImageType.D2d ->
                    if image.ArraySize > 1 then VkImageViewType.D2dArray
                    else VkImageViewType.D2d

                | _ ->
                    VkImageViewType.D3d

        ImageViewExtensions.CreateImageView(
            this,
            image,
            viewType,
            Range1i(0, image.MipMapLevels-1),
            Range1i(0, image.ArraySize-1), 
            VkImageAspectFlags.ColorBit
        )


    [<Extension>]
    static member CreateImageOutputView(this : Context, image : Image, level : int, slice : int) =
        ImageViewExtensions.CreateImageView(
            this,
            image,
            VkImageViewType.D2d,
            Range1i(level, level),
            Range1i(slice, slice), 
            VkImageAspectFlags.ColorBit
        )

    [<Extension>]
    static member CreateImageOutputView(this : Context, image : Image, level : int) =
        ImageViewExtensions.CreateImageView(
            this,
            image,
            VkImageViewType.D2d,
            Range1i(level, level),
            Range1i(0, 0), 
            VkImageAspectFlags.ColorBit
        )

    [<Extension>]
    static member CreateImageOutputView(this : Context, image : Image) =
        ImageViewExtensions.CreateImageView(
            this,
            image,
            VkImageViewType.D2d,
            Range1i(0, 0),
            Range1i(0, 0), 
            VkImageAspectFlags.ColorBit
        )