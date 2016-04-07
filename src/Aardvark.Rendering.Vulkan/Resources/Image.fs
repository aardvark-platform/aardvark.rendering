namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Threading
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

type Image =
    class 
        val mutable public Context : Context
        val mutable public Memory : deviceptr
        val mutable public Handle : VkImage

        val mutable public ImageType : VkImageType
        val mutable public Format : VkFormat
        val mutable public TextureFormat : TextureFormat

        val mutable public Size : V3i
        val mutable public MipMapLevels : int
        val mutable public ArraySize : int
        val mutable public Samples : int
        val mutable public Usage : VkImageUsageFlags
        val mutable public Layout : VkImageLayout

        new(ctx,mem,h,t,f,tf,s,m,a,sam,u,l) = { Context = ctx; Memory = mem; Handle = h; ImageType = t; Format = f; TextureFormat = tf; Size = s; MipMapLevels = m; ArraySize = a; Samples = sam; Usage = u; Layout = l }
    end

type ImageSubResource =
    struct
        val mutable public Image : Image
        val mutable public MipMapLevel : int
        val mutable public ArrayRange : Range1i
        val mutable public Offset : V3i


        member x.Size =
            match x.MipMapLevel with
                | 0 -> x.Image.Size - x.Offset
                | l -> (x.Image.Size / (1 <<< l)) - x.Offset

        new(img, mml, arr, off) = { Image = img; MipMapLevel = mml; ArrayRange = arr; Offset = off }
        new(img, mml, off) = { Image = img; MipMapLevel = mml; ArrayRange = Range1i(0,1); Offset = off }
        new(img, mml) = { Image = img; MipMapLevel = mml; ArrayRange = Range1i(0,1); Offset = V3i.Zero }
        new(img, mml, arr) = { Image = img; MipMapLevel = mml; ArrayRange = arr; Offset = V3i.Zero }
        new(img) = { Image = img; MipMapLevel = 0; ArrayRange = Range1i(0,1); Offset = V3i.Zero }

    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkFormat =
    let ofRenderbufferFormat =
        lookupTable [
            RenderbufferFormat.R3G3B2, VkFormat.Undefined
            RenderbufferFormat.Rgb4, VkFormat.Undefined
            RenderbufferFormat.Rgb5, VkFormat.Undefined
            RenderbufferFormat.Rgb10, VkFormat.Undefined
            RenderbufferFormat.Rgb12, VkFormat.Undefined
            RenderbufferFormat.Rgba2, VkFormat.Undefined
            RenderbufferFormat.Rgba12, VkFormat.Undefined
            RenderbufferFormat.StencilIndex1, VkFormat.Undefined
            RenderbufferFormat.StencilIndex4, VkFormat.Undefined
            RenderbufferFormat.StencilIndex8, VkFormat.Undefined
            RenderbufferFormat.StencilIndex16, VkFormat.Undefined

            RenderbufferFormat.DepthComponent, VkFormat.D16Unorm
            RenderbufferFormat.Rgb8, VkFormat.R8g8b8Unorm
            RenderbufferFormat.Rgb16, VkFormat.R16g16b16Unorm
            RenderbufferFormat.Rgba4, VkFormat.R4g4b4a4UnormPack16
            RenderbufferFormat.Rgba8, VkFormat.R8g8b8a8Unorm
            RenderbufferFormat.Rgb10A2, VkFormat.A2b10g10r10UnormPack32
            RenderbufferFormat.Rgba16, VkFormat.R16g16b16a16Unorm
            RenderbufferFormat.DepthComponent16, VkFormat.D16Unorm
            RenderbufferFormat.DepthComponent24, VkFormat.X8D24UnormPack32
            RenderbufferFormat.DepthComponent32, VkFormat.D32Sfloat
            RenderbufferFormat.R8, VkFormat.R8Unorm
            RenderbufferFormat.R16, VkFormat.R16Unorm
            RenderbufferFormat.Rg8, VkFormat.R8g8Unorm
            RenderbufferFormat.Rg16, VkFormat.R16g16Unorm
            RenderbufferFormat.R16f, VkFormat.R16Sfloat
            RenderbufferFormat.R32f, VkFormat.R32Sfloat
            RenderbufferFormat.Rg16f, VkFormat.R16g16Sfloat
            RenderbufferFormat.Rg32f, VkFormat.R32g32Sfloat
            RenderbufferFormat.R8i, VkFormat.R8Sint
            RenderbufferFormat.R8ui, VkFormat.R8Uint
            RenderbufferFormat.R16i, VkFormat.R16Sint
            RenderbufferFormat.R16ui, VkFormat.R16Uint
            RenderbufferFormat.R32i, VkFormat.R32Sint
            RenderbufferFormat.R32ui, VkFormat.R32Uint
            RenderbufferFormat.Rg8i, VkFormat.R8g8Sint
            RenderbufferFormat.Rg8ui, VkFormat.R8g8Uint
            RenderbufferFormat.Rg16i, VkFormat.R16g16Sint
            RenderbufferFormat.Rg16ui, VkFormat.R16g16Uint
            RenderbufferFormat.Rg32i, VkFormat.R32g32Sint
            RenderbufferFormat.Rg32ui, VkFormat.R32g32Uint
            RenderbufferFormat.DepthStencil, VkFormat.D24UnormS8Uint
            RenderbufferFormat.Rgba32f, VkFormat.R32g32b32a32Sfloat
            RenderbufferFormat.Rgb32f, VkFormat.R32g32b32Sfloat
            RenderbufferFormat.Rgba16f, VkFormat.R16g16b16a16Sfloat
            RenderbufferFormat.Rgb16f, VkFormat.R16g16b16Sfloat
            RenderbufferFormat.Depth24Stencil8, VkFormat.D24UnormS8Uint
            RenderbufferFormat.R11fG11fB10f, VkFormat.B10g11r11UfloatPack32
            RenderbufferFormat.Rgb9E5, VkFormat.E5b9g9r9UfloatPack32
            RenderbufferFormat.Srgb8, VkFormat.R8g8b8Srgb
            RenderbufferFormat.Srgb8Alpha8, VkFormat.R8g8b8a8Srgb
            RenderbufferFormat.DepthComponent32f, VkFormat.D32Sfloat
            RenderbufferFormat.Depth32fStencil8, VkFormat.D32SfloatS8Uint
            RenderbufferFormat.Rgba32ui, VkFormat.R32g32b32a32Uint
            RenderbufferFormat.Rgb32ui, VkFormat.R32g32b32Uint
            RenderbufferFormat.Rgba16ui, VkFormat.R16g16b16a16Uint
            RenderbufferFormat.Rgb16ui, VkFormat.R16g16b16Uint
            RenderbufferFormat.Rgba8ui, VkFormat.R8g8b8a8Uint
            RenderbufferFormat.Rgb8ui, VkFormat.R8g8b8Uint
            RenderbufferFormat.Rgba32i, VkFormat.R32g32b32a32Sint
            RenderbufferFormat.Rgb32i, VkFormat.R32g32b32Sint
            RenderbufferFormat.Rgba16i, VkFormat.R16g16b16a16Sint
            RenderbufferFormat.Rgb16i, VkFormat.R16g16b16Sint
            RenderbufferFormat.Rgba8i, VkFormat.R8g8b8a8Sint
            RenderbufferFormat.Rgb8i, VkFormat.R8g8b8Sint
            RenderbufferFormat.Rgb10A2ui, VkFormat.A2b10g10r10UintPack32
        ]

    let ofTextureFormat =
        lookupTable [
            TextureFormat.Alpha, VkFormat.R8Unorm
            TextureFormat.R3G3B2, VkFormat.Undefined
            TextureFormat.Rgb2Ext, VkFormat.Undefined
            TextureFormat.Rgb4, VkFormat.Undefined
            TextureFormat.Rgb12, VkFormat.Undefined
            TextureFormat.Rgba2, VkFormat.Undefined
            TextureFormat.Rgba12, VkFormat.Undefined
            TextureFormat.DualAlpha12Sgis, VkFormat.Undefined
            TextureFormat.DualLuminance12Sgis, VkFormat.Undefined
            TextureFormat.DualIntensity12Sgis, VkFormat.Undefined
            TextureFormat.DualLuminanceAlpha4Sgis, VkFormat.Undefined
            TextureFormat.DualLuminanceAlpha8Sgis, VkFormat.Undefined
            TextureFormat.CompressedRed, VkFormat.Undefined
            TextureFormat.CompressedRgbS3tcDxt1Ext, VkFormat.Undefined
            TextureFormat.CompressedRgbaS3tcDxt1Ext, VkFormat.Undefined
            TextureFormat.CompressedRgbaS3tcDxt3Ext, VkFormat.Undefined
            TextureFormat.CompressedRgbaS3tcDxt5Ext, VkFormat.Undefined
            TextureFormat.R5G6B5A8IccSgix, VkFormat.Undefined
            TextureFormat.Luminance16Alpha8IccSgix, VkFormat.Undefined
            TextureFormat.CompressedAlpha, VkFormat.Undefined
            TextureFormat.CompressedLuminance, VkFormat.Undefined
            TextureFormat.CompressedLuminanceAlpha, VkFormat.Undefined
            TextureFormat.CompressedIntensity, VkFormat.Undefined
            TextureFormat.CompressedSluminance, VkFormat.Undefined
            TextureFormat.CompressedSluminanceAlpha, VkFormat.Undefined
            TextureFormat.CompressedSrgbS3tcDxt1Ext, VkFormat.Undefined
            TextureFormat.CompressedSrgbAlphaS3tcDxt1Ext, VkFormat.Undefined
            TextureFormat.CompressedSrgbAlphaS3tcDxt3Ext, VkFormat.Undefined
            TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext, VkFormat.Undefined
            TextureFormat.CompressedRedRgtc1, VkFormat.Undefined
            TextureFormat.CompressedSignedRedRgtc1, VkFormat.Undefined
            TextureFormat.CompressedRgRgtc2, VkFormat.Undefined
            TextureFormat.CompressedSignedRgRgtc2, VkFormat.Undefined
            TextureFormat.One, VkFormat.Undefined
            TextureFormat.Two, VkFormat.Undefined
            TextureFormat.Three, VkFormat.Undefined
            TextureFormat.Four, VkFormat.Undefined

            TextureFormat.DualAlpha4Sgis, VkFormat.R4g4UnormPack8
            TextureFormat.DualAlpha8Sgis, VkFormat.R8g8Unorm
            TextureFormat.DualAlpha16Sgis, VkFormat.R16g16Unorm
            TextureFormat.DualLuminance4Sgis, VkFormat.R4g4UnormPack8
            TextureFormat.DualLuminance8Sgis, VkFormat.R8g8Unorm
            TextureFormat.DualLuminance16Sgis, VkFormat.R16g16Unorm
            TextureFormat.DualIntensity4Sgis, VkFormat.R4g4UnormPack8
            TextureFormat.DualIntensity8Sgis, VkFormat.R8g8Unorm
            TextureFormat.DualIntensity16Sgis, VkFormat.R16g16Unorm
            TextureFormat.QuadAlpha4Sgis, VkFormat.R4g4b4a4UnormPack16
            TextureFormat.QuadAlpha8Sgis, VkFormat.R8g8b8a8Unorm
            TextureFormat.QuadLuminance4Sgis, VkFormat.R4g4b4a4UnormPack16
            TextureFormat.QuadLuminance8Sgis, VkFormat.R8g8b8a8Unorm
            TextureFormat.QuadIntensity4Sgis, VkFormat.R4g4b4a4UnormPack16
            TextureFormat.QuadIntensity8Sgis, VkFormat.R8g8b8a8Unorm
            TextureFormat.DepthComponent, VkFormat.D16Unorm
            TextureFormat.Rgb, VkFormat.R8g8b8Unorm
            TextureFormat.Rgba, VkFormat.R8g8b8a8Unorm
            TextureFormat.Luminance, VkFormat.R8Unorm
            TextureFormat.LuminanceAlpha, VkFormat.R8g8Unorm
            TextureFormat.Rgb5, VkFormat.R5g5b5a1UnormPack16
            TextureFormat.Rgb8, VkFormat.R8g8b8Unorm
            TextureFormat.Rgb10, VkFormat.A2b10g10r10UnormPack32
            TextureFormat.Rgb16, VkFormat.R16g16b16Unorm
            TextureFormat.Rgba4, VkFormat.R4g4b4a4UnormPack16
            TextureFormat.Rgb5A1, VkFormat.R5g5b5a1UnormPack16
            TextureFormat.Rgba8, VkFormat.R8g8b8a8Unorm
            TextureFormat.Rgb10A2, VkFormat.A2b10g10r10UnormPack32
            TextureFormat.Rgba16, VkFormat.R16g16b16a16Unorm
            TextureFormat.DepthComponent16, VkFormat.D16Unorm
            TextureFormat.DepthComponent24, VkFormat.X8D24UnormPack32
            TextureFormat.DepthComponent32, VkFormat.D32Sfloat
            TextureFormat.CompressedRg, VkFormat.EacR11g11UnormBlock
            TextureFormat.R8, VkFormat.R8Unorm
            TextureFormat.R16, VkFormat.R16Unorm
            TextureFormat.Rg8, VkFormat.R8g8Unorm
            TextureFormat.Rg16, VkFormat.R16g16Unorm
            TextureFormat.R16f, VkFormat.R16Sfloat
            TextureFormat.R32f, VkFormat.R32Sfloat
            TextureFormat.Rg16f, VkFormat.R16g16Sfloat
            TextureFormat.Rg32f, VkFormat.R32g32Sfloat
            TextureFormat.R8i, VkFormat.R8Sint
            TextureFormat.R8ui, VkFormat.R8Uint
            TextureFormat.R16i, VkFormat.R16Sint
            TextureFormat.R16ui, VkFormat.R16Uint
            TextureFormat.R32i, VkFormat.R32Sint
            TextureFormat.R32ui, VkFormat.R32Uint
            TextureFormat.Rg8i, VkFormat.R8g8Sint
            TextureFormat.Rg8ui, VkFormat.R8g8Uint
            TextureFormat.Rg16i, VkFormat.R16g16Sint
            TextureFormat.Rg16ui, VkFormat.R16g16Uint
            TextureFormat.Rg32i, VkFormat.R32g32Sint
            TextureFormat.Rg32ui, VkFormat.R32g32Uint
            TextureFormat.RgbIccSgix, VkFormat.R8g8b8Unorm
            TextureFormat.RgbaIccSgix, VkFormat.R8g8b8a8Unorm
            TextureFormat.AlphaIccSgix, VkFormat.R8Unorm
            TextureFormat.LuminanceIccSgix, VkFormat.R8Unorm
            TextureFormat.IntensityIccSgix, VkFormat.R8Unorm
            TextureFormat.LuminanceAlphaIccSgix, VkFormat.R8g8Unorm
            TextureFormat.R5G6B5IccSgix, VkFormat.R5g6b5UnormPack16
            TextureFormat.Alpha16IccSgix, VkFormat.R16Unorm
            TextureFormat.Luminance16IccSgix, VkFormat.R16Unorm
            TextureFormat.Intensity16IccSgix, VkFormat.R16Unorm
            TextureFormat.CompressedRgb, VkFormat.Etc2R8g8b8UnormBlock
            TextureFormat.CompressedRgba, VkFormat.Etc2R8g8b8a8UnormBlock
            TextureFormat.DepthStencil, VkFormat.D24UnormS8Uint
            TextureFormat.Rgba32f, VkFormat.R32g32b32a32Sfloat
            TextureFormat.Rgb32f, VkFormat.R32g32b32Sfloat
            TextureFormat.Rgba16f, VkFormat.R16g16b16a16Sfloat
            TextureFormat.Rgb16f, VkFormat.R16g16b16Sfloat
            TextureFormat.Depth24Stencil8, VkFormat.D24UnormS8Uint
            TextureFormat.R11fG11fB10f, VkFormat.B10g11r11UfloatPack32
            TextureFormat.Rgb9E5, VkFormat.E5b9g9r9UfloatPack32
            TextureFormat.Srgb, VkFormat.R8g8b8Srgb
            TextureFormat.Srgb8, VkFormat.R8g8b8Srgb
            TextureFormat.SrgbAlpha, VkFormat.R8g8b8a8Srgb
            TextureFormat.Srgb8Alpha8, VkFormat.R8g8b8a8Srgb
            TextureFormat.SluminanceAlpha, VkFormat.R8g8Snorm
            TextureFormat.Sluminance8Alpha8, VkFormat.R8g8Snorm
            TextureFormat.Sluminance, VkFormat.R8Snorm
            TextureFormat.Sluminance8, VkFormat.R8Snorm
            TextureFormat.CompressedSrgb, VkFormat.Etc2R8g8b8SrgbBlock
            TextureFormat.CompressedSrgbAlpha, VkFormat.Etc2R8g8b8a8SrgbBlock
            TextureFormat.DepthComponent32f, VkFormat.D32Sfloat
            TextureFormat.Depth32fStencil8, VkFormat.D24UnormS8Uint
            TextureFormat.Rgba32ui, VkFormat.R32g32b32a32Uint
            TextureFormat.Rgb32ui, VkFormat.R32g32b32Uint
            TextureFormat.Rgba16ui, VkFormat.R16g16b16a16Uint
            TextureFormat.Rgb16ui, VkFormat.R16g16b16Uint
            TextureFormat.Rgba8ui, VkFormat.R8g8b8a8Uint
            TextureFormat.Rgb8ui, VkFormat.R8g8b8Uint
            TextureFormat.Rgba32i, VkFormat.R32g32b32a32Sint
            TextureFormat.Rgb32i, VkFormat.R32g32b32Sint
            TextureFormat.Rgba16i, VkFormat.R16g16b16a16Sint
            TextureFormat.Rgb16i, VkFormat.R16g16b16Sint
            TextureFormat.Rgba8i, VkFormat.R8g8b8a8Sint
            TextureFormat.Rgb8i, VkFormat.R8g8b8Sint
            TextureFormat.Float32UnsignedInt248Rev, VkFormat.D24UnormS8Uint
            TextureFormat.CompressedRgbaBptcUnorm, VkFormat.Etc2R8g8b8a8UnormBlock
            TextureFormat.CompressedRgbBptcSignedFloat, VkFormat.Bc6hSfloatBlock
            TextureFormat.CompressedRgbBptcUnsignedFloat, VkFormat.Bc6hUfloatBlock
            TextureFormat.R8Snorm, VkFormat.R8Snorm
            TextureFormat.Rg8Snorm, VkFormat.R8g8Snorm
            TextureFormat.Rgb8Snorm, VkFormat.R8g8b8Snorm
            TextureFormat.Rgba8Snorm, VkFormat.R8g8b8a8Snorm
            TextureFormat.R16Snorm, VkFormat.R16Snorm
            TextureFormat.Rg16Snorm, VkFormat.R16g16Snorm
            TextureFormat.Rgb16Snorm, VkFormat.R16g16b16Snorm
            TextureFormat.Rgba16Snorm, VkFormat.R16g16b16a16Snorm
            TextureFormat.Rgb10A2ui, VkFormat.A2b10g10r10UintPack32
        ]

    let toRenderbufferFormat =
        lookupTable [
//            VkFormat.Undefined, RenderbufferFormat.R3G3B2
//            VkFormat.Undefined, RenderbufferFormat.Rgb4
//            VkFormat.Undefined, RenderbufferFormat.Rgb5
//            VkFormat.Undefined, RenderbufferFormat.Rgb10
//            VkFormat.Undefined, RenderbufferFormat.Rgb12
//            VkFormat.Undefined, RenderbufferFormat.Rgba2
//            VkFormat.Undefined, RenderbufferFormat.Rgba12
//            VkFormat.Undefined, RenderbufferFormat.StencilIndex1
//            VkFormat.Undefined, RenderbufferFormat.StencilIndex4
//            VkFormat.Undefined, RenderbufferFormat.StencilIndex8
//            VkFormat.Undefined, RenderbufferFormat.StencilIndex16

            VkFormat.D16Unorm, RenderbufferFormat.DepthComponent
            VkFormat.R8g8b8Unorm, RenderbufferFormat.Rgb8
            VkFormat.R16g16b16Unorm, RenderbufferFormat.Rgb16
            VkFormat.R4g4b4a4UnormPack16, RenderbufferFormat.Rgba4
            VkFormat.R8g8b8a8Unorm, RenderbufferFormat.Rgba8
            VkFormat.A2b10g10r10UnormPack32, RenderbufferFormat.Rgb10A2
            VkFormat.R16g16b16a16Unorm, RenderbufferFormat.Rgba16
            VkFormat.D16Unorm, RenderbufferFormat.DepthComponent16
            VkFormat.X8D24UnormPack32, RenderbufferFormat.DepthComponent24
            VkFormat.D32Sfloat, RenderbufferFormat.DepthComponent32
            VkFormat.R8Unorm, RenderbufferFormat.R8
            VkFormat.R16Unorm, RenderbufferFormat.R16
            VkFormat.R8g8Unorm, RenderbufferFormat.Rg8
            VkFormat.R16g16Unorm, RenderbufferFormat.Rg16
            VkFormat.R16Sfloat, RenderbufferFormat.R16f
            VkFormat.R32Sfloat, RenderbufferFormat.R32f
            VkFormat.R16g16Sfloat, RenderbufferFormat.Rg16f
            VkFormat.R32g32Sfloat, RenderbufferFormat.Rg32f
            VkFormat.R8Sint, RenderbufferFormat.R8i
            VkFormat.R8Uint, RenderbufferFormat.R8ui
            VkFormat.R16Sint, RenderbufferFormat.R16i
            VkFormat.R16Uint, RenderbufferFormat.R16ui
            VkFormat.R32Sint, RenderbufferFormat.R32i
            VkFormat.R32Uint, RenderbufferFormat.R32ui
            VkFormat.R8g8Sint, RenderbufferFormat.Rg8i
            VkFormat.R8g8Uint, RenderbufferFormat.Rg8ui
            VkFormat.R16g16Sint, RenderbufferFormat.Rg16i
            VkFormat.R16g16Uint, RenderbufferFormat.Rg16ui
            VkFormat.R32g32Sint, RenderbufferFormat.Rg32i
            VkFormat.R32g32Uint, RenderbufferFormat.Rg32ui
            VkFormat.D24UnormS8Uint, RenderbufferFormat.DepthStencil
            VkFormat.R32g32b32a32Sfloat, RenderbufferFormat.Rgba32f
            VkFormat.R32g32b32Sfloat, RenderbufferFormat.Rgb32f
            VkFormat.R16g16b16a16Sfloat, RenderbufferFormat.Rgba16f
            VkFormat.R16g16b16Sfloat, RenderbufferFormat.Rgb16f
            VkFormat.D24UnormS8Uint, RenderbufferFormat.Depth24Stencil8
            VkFormat.B10g11r11UfloatPack32, RenderbufferFormat.R11fG11fB10f
            VkFormat.E5b9g9r9UfloatPack32, RenderbufferFormat.Rgb9E5
            VkFormat.R8g8b8Srgb, RenderbufferFormat.Srgb8
            VkFormat.R8g8b8a8Srgb, RenderbufferFormat.Srgb8Alpha8
            VkFormat.D32Sfloat, RenderbufferFormat.DepthComponent32f
            VkFormat.D32SfloatS8Uint, RenderbufferFormat.Depth32fStencil8
            VkFormat.R32g32b32a32Uint, RenderbufferFormat.Rgba32ui
            VkFormat.R32g32b32Uint, RenderbufferFormat.Rgb32ui
            VkFormat.R16g16b16a16Uint, RenderbufferFormat.Rgba16ui
            VkFormat.R16g16b16Uint, RenderbufferFormat.Rgb16ui
            VkFormat.R8g8b8a8Uint, RenderbufferFormat.Rgba8ui
            VkFormat.R8g8b8Uint, RenderbufferFormat.Rgb8ui
            VkFormat.R32g32b32a32Sint, RenderbufferFormat.Rgba32i
            VkFormat.R32g32b32Sint, RenderbufferFormat.Rgb32i
            VkFormat.R16g16b16a16Sint, RenderbufferFormat.Rgba16i
            VkFormat.R16g16b16Sint, RenderbufferFormat.Rgb16i
            VkFormat.R8g8b8a8Sint, RenderbufferFormat.Rgba8i
            VkFormat.R8g8b8Sint, RenderbufferFormat.Rgb8i
            VkFormat.A2b10g10r10UintPack32, RenderbufferFormat.Rgb10A2ui
        ]

    let toTextureFormat =
        lookupTable [
//            VkFormat.R8Unorm, TextureFormat.Alpha
//            VkFormat.Undefined, TextureFormat.R3G3B2
//            VkFormat.Undefined, TextureFormat.Rgb2Ext
//            VkFormat.Undefined, TextureFormat.Rgb4
//            VkFormat.Undefined, TextureFormat.Rgb12
//            VkFormat.Undefined, TextureFormat.Rgba2
//            VkFormat.Undefined, TextureFormat.Rgba12
//            VkFormat.Undefined, TextureFormat.DualAlpha12Sgis
//            VkFormat.Undefined, TextureFormat.DualLuminance12Sgis
//            VkFormat.Undefined, TextureFormat.DualIntensity12Sgis
//            VkFormat.Undefined, TextureFormat.DualLuminanceAlpha4Sgis
//            VkFormat.Undefined, TextureFormat.DualLuminanceAlpha8Sgis
//            VkFormat.Undefined, TextureFormat.CompressedRed
//            VkFormat.Undefined, TextureFormat.CompressedRgbS3tcDxt1Ext
//            VkFormat.Undefined, TextureFormat.CompressedRgbaS3tcDxt1Ext
//            VkFormat.Undefined, TextureFormat.CompressedRgbaS3tcDxt3Ext
//            VkFormat.Undefined, TextureFormat.CompressedRgbaS3tcDxt5Ext
//            VkFormat.Undefined, TextureFormat.R5G6B5A8IccSgix
//            VkFormat.Undefined, TextureFormat.Luminance16Alpha8IccSgix
//            VkFormat.Undefined, TextureFormat.CompressedAlpha
//            VkFormat.Undefined, TextureFormat.CompressedLuminance
//            VkFormat.Undefined, TextureFormat.CompressedLuminanceAlpha
//            VkFormat.Undefined, TextureFormat.CompressedIntensity
//            VkFormat.Undefined, TextureFormat.CompressedSluminance
//            VkFormat.Undefined, TextureFormat.CompressedSluminanceAlpha
//            VkFormat.Undefined, TextureFormat.CompressedSrgbS3tcDxt1Ext
//            VkFormat.Undefined, TextureFormat.CompressedSrgbAlphaS3tcDxt1Ext
//            VkFormat.Undefined, TextureFormat.CompressedSrgbAlphaS3tcDxt3Ext
//            VkFormat.Undefined, TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext
//            VkFormat.Undefined, TextureFormat.CompressedRedRgtc1
//            VkFormat.Undefined, TextureFormat.CompressedSignedRedRgtc1
//            VkFormat.Undefined, TextureFormat.CompressedRgRgtc2
//            VkFormat.Undefined, TextureFormat.CompressedSignedRgRgtc2
//            VkFormat.Undefined, TextureFormat.One
//            VkFormat.Undefined, TextureFormat.Two
//            VkFormat.Undefined, TextureFormat.Three
//            VkFormat.Undefined, TextureFormat.Four

            VkFormat.R4g4UnormPack8, TextureFormat.DualAlpha4Sgis
            VkFormat.R8g8Unorm, TextureFormat.DualAlpha8Sgis
            VkFormat.R16g16Unorm, TextureFormat.DualAlpha16Sgis
            VkFormat.R4g4UnormPack8, TextureFormat.DualLuminance4Sgis
            VkFormat.R8g8Unorm, TextureFormat.DualLuminance8Sgis
            VkFormat.R16g16Unorm, TextureFormat.DualLuminance16Sgis
            VkFormat.R4g4UnormPack8, TextureFormat.DualIntensity4Sgis
            VkFormat.R8g8Unorm, TextureFormat.DualIntensity8Sgis
            VkFormat.R16g16Unorm, TextureFormat.DualIntensity16Sgis
            VkFormat.R4g4b4a4UnormPack16, TextureFormat.QuadAlpha4Sgis
            VkFormat.R8g8b8a8Unorm, TextureFormat.QuadAlpha8Sgis
            VkFormat.R4g4b4a4UnormPack16, TextureFormat.QuadLuminance4Sgis
            VkFormat.R8g8b8a8Unorm, TextureFormat.QuadLuminance8Sgis
            VkFormat.R4g4b4a4UnormPack16, TextureFormat.QuadIntensity4Sgis
            VkFormat.R8g8b8a8Unorm, TextureFormat.QuadIntensity8Sgis
            VkFormat.D16Unorm, TextureFormat.DepthComponent
            VkFormat.R8g8b8Unorm, TextureFormat.Rgb
            VkFormat.R8g8b8a8Unorm, TextureFormat.Rgba
            VkFormat.R8Unorm, TextureFormat.Luminance
            VkFormat.R8g8Unorm, TextureFormat.LuminanceAlpha
            VkFormat.R5g5b5a1UnormPack16, TextureFormat.Rgb5
            VkFormat.R8g8b8Unorm, TextureFormat.Rgb8
            VkFormat.A2b10g10r10UnormPack32, TextureFormat.Rgb10
            VkFormat.R16g16b16Unorm, TextureFormat.Rgb16
            VkFormat.R4g4b4a4UnormPack16, TextureFormat.Rgba4
            VkFormat.R5g5b5a1UnormPack16, TextureFormat.Rgb5A1
            VkFormat.R8g8b8a8Unorm, TextureFormat.Rgba8
            VkFormat.A2b10g10r10UnormPack32, TextureFormat.Rgb10A2
            VkFormat.R16g16b16a16Unorm, TextureFormat.Rgba16
            VkFormat.D16Unorm, TextureFormat.DepthComponent16
            VkFormat.X8D24UnormPack32, TextureFormat.DepthComponent24
            VkFormat.D32Sfloat, TextureFormat.DepthComponent32
            VkFormat.EacR11g11UnormBlock, TextureFormat.CompressedRg
            VkFormat.R8Unorm, TextureFormat.R8
            VkFormat.R16Unorm, TextureFormat.R16
            VkFormat.R8g8Unorm, TextureFormat.Rg8
            VkFormat.R16g16Unorm, TextureFormat.Rg16
            VkFormat.R16Sfloat, TextureFormat.R16f
            VkFormat.R32Sfloat, TextureFormat.R32f
            VkFormat.R16g16Sfloat, TextureFormat.Rg16f
            VkFormat.R32g32Sfloat, TextureFormat.Rg32f
            VkFormat.R8Sint, TextureFormat.R8i
            VkFormat.R8Uint, TextureFormat.R8ui
            VkFormat.R16Sint, TextureFormat.R16i
            VkFormat.R16Uint, TextureFormat.R16ui
            VkFormat.R32Sint, TextureFormat.R32i
            VkFormat.R32Uint, TextureFormat.R32ui
            VkFormat.R8g8Sint, TextureFormat.Rg8i
            VkFormat.R8g8Uint, TextureFormat.Rg8ui
            VkFormat.R16g16Sint, TextureFormat.Rg16i
            VkFormat.R16g16Uint, TextureFormat.Rg16ui
            VkFormat.R32g32Sint, TextureFormat.Rg32i
            VkFormat.R32g32Uint, TextureFormat.Rg32ui
            VkFormat.R8g8b8Unorm, TextureFormat.RgbIccSgix
            VkFormat.R8g8b8a8Unorm, TextureFormat.RgbaIccSgix
            VkFormat.R8Unorm, TextureFormat.AlphaIccSgix
            VkFormat.R8Unorm, TextureFormat.LuminanceIccSgix
            VkFormat.R8Unorm, TextureFormat.IntensityIccSgix
            VkFormat.R8g8Unorm, TextureFormat.LuminanceAlphaIccSgix
            VkFormat.R5g6b5UnormPack16, TextureFormat.R5G6B5IccSgix
            VkFormat.R16Unorm, TextureFormat.Alpha16IccSgix
            VkFormat.R16Unorm, TextureFormat.Luminance16IccSgix
            VkFormat.R16Unorm, TextureFormat.Intensity16IccSgix
            VkFormat.Etc2R8g8b8UnormBlock, TextureFormat.CompressedRgb
            VkFormat.Etc2R8g8b8a8UnormBlock, TextureFormat.CompressedRgba
            VkFormat.D24UnormS8Uint, TextureFormat.DepthStencil
            VkFormat.R32g32b32a32Sfloat, TextureFormat.Rgba32f
            VkFormat.R32g32b32Sfloat, TextureFormat.Rgb32f
            VkFormat.R16g16b16a16Sfloat, TextureFormat.Rgba16f
            VkFormat.R16g16b16Sfloat, TextureFormat.Rgb16f
            VkFormat.D24UnormS8Uint, TextureFormat.Depth24Stencil8
            VkFormat.B10g11r11UfloatPack32, TextureFormat.R11fG11fB10f
            VkFormat.E5b9g9r9UfloatPack32, TextureFormat.Rgb9E5
            VkFormat.R8g8b8Srgb, TextureFormat.Srgb
            VkFormat.R8g8b8Srgb, TextureFormat.Srgb8
            VkFormat.R8g8b8a8Srgb, TextureFormat.SrgbAlpha
            VkFormat.R8g8b8a8Srgb, TextureFormat.Srgb8Alpha8
            VkFormat.R8g8Snorm, TextureFormat.SluminanceAlpha
            VkFormat.R8g8Snorm, TextureFormat.Sluminance8Alpha8
            VkFormat.R8Snorm, TextureFormat.Sluminance
            VkFormat.R8Snorm, TextureFormat.Sluminance8
            VkFormat.Etc2R8g8b8SrgbBlock, TextureFormat.CompressedSrgb
            VkFormat.Etc2R8g8b8a8SrgbBlock, TextureFormat.CompressedSrgbAlpha
            VkFormat.D32Sfloat, TextureFormat.DepthComponent32f
            VkFormat.D24UnormS8Uint, TextureFormat.Depth32fStencil8
            VkFormat.R32g32b32a32Uint, TextureFormat.Rgba32ui
            VkFormat.R32g32b32Uint, TextureFormat.Rgb32ui
            VkFormat.R16g16b16a16Uint, TextureFormat.Rgba16ui
            VkFormat.R16g16b16Uint, TextureFormat.Rgb16ui
            VkFormat.R8g8b8a8Uint, TextureFormat.Rgba8ui
            VkFormat.R8g8b8Uint, TextureFormat.Rgb8ui
            VkFormat.R32g32b32a32Sint, TextureFormat.Rgba32i
            VkFormat.R32g32b32Sint, TextureFormat.Rgb32i
            VkFormat.R16g16b16a16Sint, TextureFormat.Rgba16i
            VkFormat.R16g16b16Sint, TextureFormat.Rgb16i
            VkFormat.R8g8b8a8Sint, TextureFormat.Rgba8i
            VkFormat.R8g8b8Sint, TextureFormat.Rgb8i
            VkFormat.D24UnormS8Uint, TextureFormat.Float32UnsignedInt248Rev
            VkFormat.Etc2R8g8b8a8UnormBlock, TextureFormat.CompressedRgbaBptcUnorm
            VkFormat.Bc6hSfloatBlock, TextureFormat.CompressedRgbBptcSignedFloat
            VkFormat.Bc6hUfloatBlock, TextureFormat.CompressedRgbBptcUnsignedFloat
            VkFormat.R8Snorm, TextureFormat.R8Snorm
            VkFormat.R8g8Snorm, TextureFormat.Rg8Snorm
            VkFormat.R8g8b8Snorm, TextureFormat.Rgb8Snorm
            VkFormat.R8g8b8a8Snorm, TextureFormat.Rgba8Snorm
            VkFormat.R16Snorm, TextureFormat.R16Snorm
            VkFormat.R16g16Snorm, TextureFormat.Rg16Snorm
            VkFormat.R16g16b16Snorm, TextureFormat.Rgb16Snorm
            VkFormat.R16g16b16a16Snorm, TextureFormat.Rgba16Snorm
            VkFormat.A2b10g10r10UintPack32, TextureFormat.Rgb10A2ui
        ]

    let ofPixFormat (fmt : PixFormat) (textureParams : TextureParams) =
        TextureFormat.ofPixFormat fmt textureParams |> ofTextureFormat

    let toUploadFormat =
        lookupTable [
            PixFormat.ByteBGR, VkFormat.B8g8r8Unorm
            PixFormat.ByteBGRA, VkFormat.B8g8r8a8Unorm
            PixFormat.ByteBGRP, VkFormat.B8g8r8a8Unorm
            PixFormat.ByteBW, VkFormat.R8Unorm
            PixFormat.ByteGray, VkFormat.R8Unorm
            PixFormat.ByteRGB, VkFormat.R8g8b8Unorm
            PixFormat.ByteRGBA, VkFormat.R8g8b8a8Unorm
            PixFormat.ByteRGBP, VkFormat.R8g8b8a8Unorm


            PixFormat.DoubleBGR, VkFormat.Undefined
            PixFormat.DoubleBGRA, VkFormat.Undefined
            PixFormat.DoubleBGRP, VkFormat.Undefined
            PixFormat.DoubleGray, VkFormat.R64Sfloat
            PixFormat.DoubleRGB, VkFormat.R64g64b64Sfloat
            PixFormat.DoubleRGBA, VkFormat.R64g64b64a64Sfloat
            PixFormat.DoubleRGBP, VkFormat.R64g64b64a64Sfloat


            PixFormat.FloatBGR, VkFormat.Undefined
            PixFormat.FloatBGRA, VkFormat.Undefined
            PixFormat.FloatBGRP, VkFormat.Undefined
            PixFormat.FloatGray, VkFormat.R32Sfloat
            PixFormat.FloatRGB, VkFormat.R32g32b32Sfloat
            PixFormat.FloatRGBA, VkFormat.R32g32b32a32Sfloat
            PixFormat.FloatRGBP, VkFormat.R32g32b32a32Sfloat


            PixFormat.UIntBGR, VkFormat.Undefined
            PixFormat.UIntBGRA, VkFormat.Undefined
            PixFormat.UIntBGRP, VkFormat.Undefined
            PixFormat.UIntGray, VkFormat.R32Uint
            PixFormat.UIntRGB, VkFormat.R32g32b32Uint
            PixFormat.UIntRGBA, VkFormat.R32g32b32a32Uint
            PixFormat.UIntRGBP, VkFormat.R32g32b32a32Uint

            
            PixFormat.UShortBGR, VkFormat.Undefined
            PixFormat.UShortBGRA, VkFormat.Undefined
            PixFormat.UShortBGRP, VkFormat.Undefined
            PixFormat.UShortGray, VkFormat.R16Uint
            PixFormat.UShortRGB, VkFormat.R16g16b16Uint
            PixFormat.UShortRGBA, VkFormat.R16g16b16a16Uint
            PixFormat.UShortRGBP, VkFormat.R16g16b16a16Uint
        ]

    let toDownloadFormat (fmt : VkFormat) =
        fmt |> toTextureFormat |> TextureFormat.toDownloadFormat


    // TODO: maybe remove??
    type Signedness =
        | Signed 
        | Unsigned

    type ChannelType =
        | Norm of signed : Signedness * bits : int
        | Scaled of signed : Signedness * bits : int
        | Int of signed : Signedness * bits : int
        | Srgb of bits : int
        | Float of bits : int
        | Undefined

    let channelType =
        let s = Signed
        let u = Unsigned
        lookupTable [
            VkFormat.Undefined, Undefined
            VkFormat.R4g4UnormPack8, Undefined
            VkFormat.R4g4b4a4UnormPack16, Undefined
            VkFormat.B4g4r4a4UnormPack16, Undefined
            VkFormat.R5g6b5UnormPack16, Undefined
            VkFormat.B5g6r5UnormPack16, Undefined
            VkFormat.R5g5b5a1UnormPack16, Undefined
            VkFormat.B5g5r5a1UnormPack16, Undefined
            VkFormat.A1r5g5b5UnormPack16, Undefined
            VkFormat.R8Unorm, Norm(u, 8)
            VkFormat.R8Snorm, Norm(s, 8)
            VkFormat.R8Uscaled, Scaled(u, 8)
            VkFormat.R8Sscaled, Scaled(s, 8)
            VkFormat.R8Uint, Int(u, 8)
            VkFormat.R8Sint, Int(s, 8)
            VkFormat.R8Srgb, Srgb(8)
            VkFormat.R8g8Unorm, Norm(u, 8)
            VkFormat.R8g8Snorm, Norm(s, 8)
            VkFormat.R8g8Uscaled, Scaled(u, 8)
            VkFormat.R8g8Sscaled, Scaled(s, 8)
            VkFormat.R8g8Uint, Int(u, 8)
            VkFormat.R8g8Sint, Int(s, 8)
            VkFormat.R8g8Srgb, Srgb(8)
            VkFormat.R8g8b8Unorm, Norm(u, 8)
            VkFormat.R8g8b8Snorm, Norm(s, 8)
            VkFormat.R8g8b8Uscaled, Scaled(u, 8)
            VkFormat.R8g8b8Sscaled, Scaled(s, 8)
            VkFormat.R8g8b8Uint, Int(u, 8)
            VkFormat.R8g8b8Sint, Int(s, 8)
            VkFormat.R8g8b8Srgb, Srgb(8)
            VkFormat.B8g8r8Unorm, Norm(u, 8)
            VkFormat.B8g8r8Snorm, Norm(s, 8)
            VkFormat.B8g8r8Uscaled, Scaled(u, 8)
            VkFormat.B8g8r8Sscaled, Scaled(s, 8)
            VkFormat.B8g8r8Uint, Int(u, 8)
            VkFormat.B8g8r8Sint, Int(s, 8)
            VkFormat.B8g8r8Srgb, Srgb(8)
            VkFormat.R8g8b8a8Unorm, Norm(u, 8)
            VkFormat.R8g8b8a8Snorm, Norm(s, 8)
            VkFormat.R8g8b8a8Uscaled, Scaled(u, 8)
            VkFormat.R8g8b8a8Sscaled, Scaled(s, 8)
            VkFormat.R8g8b8a8Uint, Int(u, 8)
            VkFormat.R8g8b8a8Sint, Int(s, 8)
            VkFormat.R8g8b8a8Srgb, Srgb(8)
            VkFormat.B8g8r8a8Unorm, Norm(u, 8)
            VkFormat.B8g8r8a8Snorm, Norm(s, 8)
            VkFormat.B8g8r8a8Uscaled, Scaled(u, 8)
            VkFormat.B8g8r8a8Sscaled, Scaled(s, 8)
            VkFormat.B8g8r8a8Uint, Int(u, 8)
            VkFormat.B8g8r8a8Sint, Int(s, 8)
            VkFormat.B8g8r8a8Srgb, Srgb(8)
            VkFormat.A8b8g8r8UnormPack32, Norm(u, 8)
            VkFormat.A8b8g8r8SnormPack32, Norm(s, 8)
            VkFormat.A8b8g8r8UscaledPack32, Scaled(u, 8)
            VkFormat.A8b8g8r8SscaledPack32, Scaled(s, 8)
            VkFormat.A8b8g8r8UintPack32, Int(u, 8)
            VkFormat.A8b8g8r8SintPack32, Int(s, 8)
            VkFormat.A8b8g8r8SrgbPack32, Srgb(8)
            VkFormat.A2r10g10b10UnormPack32, Undefined
            VkFormat.A2r10g10b10SnormPack32, Undefined
            VkFormat.A2r10g10b10UscaledPack32, Undefined
            VkFormat.A2r10g10b10SscaledPack32, Undefined
            VkFormat.A2r10g10b10UintPack32, Undefined
            VkFormat.A2r10g10b10SintPack32, Undefined
            VkFormat.A2b10g10r10UnormPack32, Undefined
            VkFormat.A2b10g10r10SnormPack32, Undefined
            VkFormat.A2b10g10r10UscaledPack32, Undefined
            VkFormat.A2b10g10r10SscaledPack32, Undefined
            VkFormat.A2b10g10r10UintPack32, Undefined
            VkFormat.A2b10g10r10SintPack32, Undefined
            VkFormat.R16Unorm, Norm(u, 16)
            VkFormat.R16Snorm, Norm(s, 16)
            VkFormat.R16Uscaled, Scaled(u, 16)
            VkFormat.R16Sscaled, Scaled(s, 16)
            VkFormat.R16Uint, Int(u, 16)
            VkFormat.R16Sint, Int(s, 16)
            VkFormat.R16Sfloat, Float(16)
            VkFormat.R16g16Unorm, Norm(u, 16)
            VkFormat.R16g16Snorm, Norm(s, 16)
            VkFormat.R16g16Uscaled, Scaled(u, 16)
            VkFormat.R16g16Sscaled, Scaled(s, 16)
            VkFormat.R16g16Uint, Int(u, 16)
            VkFormat.R16g16Sint, Int(s, 16)
            VkFormat.R16g16Sfloat, Float(16)
            VkFormat.R16g16b16Unorm, Norm(u, 16)
            VkFormat.R16g16b16Snorm, Norm(s, 16)
            VkFormat.R16g16b16Uscaled, Scaled(u, 16)
            VkFormat.R16g16b16Sscaled, Scaled(s, 16)
            VkFormat.R16g16b16Uint, Int(u, 16)
            VkFormat.R16g16b16Sint, Int(s, 16)
            VkFormat.R16g16b16Sfloat, Float(16)
            VkFormat.R16g16b16a16Unorm, Norm(u, 16)
            VkFormat.R16g16b16a16Snorm, Norm(s, 16)
            VkFormat.R16g16b16a16Uscaled, Scaled(u, 16)
            VkFormat.R16g16b16a16Sscaled, Scaled(s, 16)
            VkFormat.R16g16b16a16Uint, Int(u, 16)
            VkFormat.R16g16b16a16Sint, Int(s, 16)
            VkFormat.R16g16b16a16Sfloat, Float(16)
            VkFormat.R32Uint, Int(u, 32)
            VkFormat.R32Sint, Int(s, 32)
            VkFormat.R32Sfloat, Float(32)
            VkFormat.R32g32Uint, Int(u, 32)
            VkFormat.R32g32Sint, Int(s, 32)
            VkFormat.R32g32Sfloat, Float(32)
            VkFormat.R32g32b32Uint, Int(u, 32)
            VkFormat.R32g32b32Sint, Int(s, 32)
            VkFormat.R32g32b32Sfloat, Float(32)
            VkFormat.R32g32b32a32Uint, Int(u, 32)
            VkFormat.R32g32b32a32Sint, Int(s, 32)
            VkFormat.R32g32b32a32Sfloat, Float(32)
            VkFormat.R64Uint, Int(u, 64)
            VkFormat.R64Sint, Int(s, 64)
            VkFormat.R64Sfloat, Float(64)
            VkFormat.R64g64Uint, Int(u, 64)
            VkFormat.R64g64Sint, Int(s, 64)
            VkFormat.R64g64Sfloat, Float(64)
            VkFormat.R64g64b64Uint, Int(u, 64)
            VkFormat.R64g64b64Sint, Int(s, 64)
            VkFormat.R64g64b64Sfloat, Float(64)
            VkFormat.R64g64b64a64Uint, Int(u, 64)
            VkFormat.R64g64b64a64Sint, Int(s, 64)
            VkFormat.R64g64b64a64Sfloat, Float(64)
            VkFormat.B10g11r11UfloatPack32, Undefined
            VkFormat.E5b9g9r9UfloatPack32, Undefined
            VkFormat.D16Unorm, Norm(u, 16)
            VkFormat.X8D24UnormPack32, Undefined
            VkFormat.D32Sfloat, Undefined
            VkFormat.S8Uint, Int(u,  8)
            VkFormat.D16UnormS8Uint, Undefined
            VkFormat.D24UnormS8Uint, Undefined
            VkFormat.D32SfloatS8Uint, Undefined

            VkFormat.Bc1RgbUnormBlock, Norm(u, 8)
            VkFormat.Bc1RgbSrgbBlock, Srgb(8)
            VkFormat.Bc1RgbaUnormBlock, Norm(u, 8)
            VkFormat.Bc1RgbaSrgbBlock, Srgb(8)
            VkFormat.Bc2UnormBlock, Norm(u, 8)
            VkFormat.Bc2SrgbBlock, Srgb(8)
            VkFormat.Bc3UnormBlock, Norm(u, 8)
            VkFormat.Bc3SrgbBlock, Srgb(8)
            VkFormat.Bc4UnormBlock, Norm(u, 8)
            VkFormat.Bc4SnormBlock, Norm(s, 8)
            VkFormat.Bc5UnormBlock, Norm(u, 8)
            VkFormat.Bc5SnormBlock, Norm(s, 8)
            VkFormat.Bc6hUfloatBlock, Float(32)
            VkFormat.Bc6hSfloatBlock, Float(32)
            VkFormat.Bc7UnormBlock, Norm(u, 8)
            VkFormat.Bc7SrgbBlock, Srgb(8)

            VkFormat.Etc2R8g8b8UnormBlock, Norm(u, 8)
            VkFormat.Etc2R8g8b8SrgbBlock, Srgb(8)
            VkFormat.Etc2R8g8b8a1UnormBlock, Norm(u, 8)
            VkFormat.Etc2R8g8b8a1SrgbBlock, Norm(s, 8)
            VkFormat.Etc2R8g8b8a8UnormBlock, Norm(u, 8)
            VkFormat.Etc2R8g8b8a8SrgbBlock, Srgb(8)

            VkFormat.EacR11UnormBlock, Norm(u, 11)
            VkFormat.EacR11SnormBlock, Norm(s, 11)
            VkFormat.EacR11g11UnormBlock, Norm(u, 11)
            VkFormat.EacR11g11SnormBlock, Norm(s, 11)

            VkFormat.Astc44UnormBlock, Norm(u, 8)
            VkFormat.Astc44SrgbBlock, Srgb(8)
            VkFormat.Astc54UnormBlock, Norm(u, 8)
            VkFormat.Astc54SrgbBlock, Norm(s, 8)
            VkFormat.Astc55UnormBlock, Norm(u, 8)
            VkFormat.Astc55SrgbBlock, Srgb(8)
            VkFormat.Astc65UnormBlock, Norm(u, 8)
            VkFormat.Astc65SrgbBlock, Srgb(8)
            VkFormat.Astc66UnormBlock, Norm(u, 8)
            VkFormat.Astc66SrgbBlock, Srgb(8)
            VkFormat.Astc85UnormBlock, Norm(u, 8)
            VkFormat.Astc85SrgbBlock, Srgb(8)
            VkFormat.Astc86UnormBlock, Norm(u, 8)
            VkFormat.Astc86SrgbBlock, Srgb(8)
            VkFormat.Astc88UnormBlock, Norm(u, 8)
            VkFormat.Astc88SrgbBlock, Srgb(8)
            VkFormat.Astc105UnormBlock, Norm(u, 8)
            VkFormat.Astc105SrgbBlock, Srgb(8)
            VkFormat.Astc106UnormBlock, Norm(u, 8)
            VkFormat.Astc106SrgbBlock, Srgb(8)
            VkFormat.Astc108UnormBlock, Norm(u, 8)
            VkFormat.Astc108SrgbBlock, Srgb(8)
            VkFormat.Astc1010UnormBlock, Norm(u, 10)
            VkFormat.Astc1010SrgbBlock, Srgb(8)
            VkFormat.Astc1210UnormBlock, Norm(u, 8)
            VkFormat.Astc1210SrgbBlock, Srgb(8)
            VkFormat.Astc1212UnormBlock, Norm(u, 8)
            VkFormat.Astc1212SrgbBlock, Srgb(8)
        ]

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ImageSubResource =

    let ofImage (img : Image) = ImageSubResource(img)
     
    let ofImageLevel (level : int) (img : Image) = ImageSubResource(img, level)

    let copy (src : ImageSubResource) (dst : ImageSubResource) =
        let srcSize = src.Size
        let dstSize = dst.Size
        if srcSize <> dstSize then
            failf "cannot copy image of size %A to one of size %A" srcSize dstSize

        let size = srcSize

        Command.custom (fun s ->
            let mutable s = s
            let cmd = s.buffer

            let mutable copy =
                VkImageCopy(
                    VkImageSubresourceLayers(VkImageAspectFlags.ColorBit, uint32 src.MipMapLevel, uint32 src.ArrayRange.Min, 1u + uint32 src.ArrayRange.Size),
                    VkOffset3D(src.Offset.X, src.Offset.Y, src.Offset.Z),
                    VkImageSubresourceLayers(VkImageAspectFlags.ColorBit, uint32 dst.MipMapLevel, uint32 dst.ArrayRange.Min, 1u + uint32 src.ArrayRange.Size),
                    VkOffset3D(dst.Offset.X, dst.Offset.Y, dst.Offset.Z),
                    VkExtent3D(size.X, size.Y, size.Z)
                )

            VkRaw.vkCmdCopyImage(cmd.Handle, src.Image.Handle, src.Image.Layout, dst.Image.Handle, dst.Image.Layout, 1u, &&copy)

            { s with isEmpty = false }
        )

    let blit (filter : VkFilter) (src : ImageSubResource) (dst : ImageSubResource) =
        Command.custom (fun s ->
            let mutable s = s
            let cmd = s.buffer

            let srcSize = src.Size
            let mutable srcRange = VkOffset3D_2()
            srcRange.[0] <- VkOffset3D(src.Offset.X, src.Offset.Y, src.Offset.Z)
            srcRange.[1] <- VkOffset3D(srcSize.X, srcSize.Y, srcSize.Z)

            let dstSize = dst.Size
            let mutable dstRange = VkOffset3D_2()
            dstRange.[0] <- VkOffset3D(dst.Offset.X, dst.Offset.Y, dst.Offset.Z)
            dstRange.[1] <- VkOffset3D(dstSize.X, dstSize.Y, dstSize.Z)


            let mutable blit =
                VkImageBlit(
                    VkImageSubresourceLayers(VkImageAspectFlags.ColorBit, uint32 src.MipMapLevel, uint32 src.ArrayRange.Min, 1u + uint32 src.ArrayRange.Size),
                    srcRange,
                    VkImageSubresourceLayers(VkImageAspectFlags.ColorBit, uint32 dst.MipMapLevel, uint32 dst.ArrayRange.Min, 1u + uint32 src.ArrayRange.Size),
                    dstRange
                )


            VkRaw.vkCmdBlitImage(
                cmd.Handle, 
                src.Image.Handle, src.Image.Layout, 
                dst.Image.Handle, dst.Image.Layout, 
                1u, &&blit, 
                filter
            )

            { s with isEmpty = false }
        )

    let uploadRaw (srcFormat : VkFormat) (src : NativeVolumeRaw) (dst : ImageSubResource) =
        Command.custom (fun s ->
            let mutable s = s

            let ctx = dst.Image.Context
            let size = dst.Size

            let mutable targetLayout = VkSubresourceLayout()
            let mutable targetSub = VkImageSubresource(VkImageAspectFlags.ColorBit, uint32 dst.MipMapLevel, 0u)
            VkRaw.vkGetImageSubresourceLayout(ctx.Device.Handle, dst.Image.Handle, &&targetSub, &&targetLayout)

            let pixelSize = targetLayout.size / uint64 (size.X * size.Y) |> nativeint
            let channelSize = src.Info.ElementSize
            let channels = pixelSize / channelSize
            let dy = nativeint targetLayout.rowPitch / channelSize
            let mem = dst.Image.Memory

            if srcFormat = dst.Image.Format && mem.IsHostVisible then
                DevicePtr.map mem (fun ptr ->
                    let info =
                        NativeVolumeInfo(
                            V3n(channels, -dy, 1n),
                            V3n(nativeint size.X, nativeint size.Y, nativeint size.Z),
                            channelSize
                        )

                    let ptr = ptr + dy * nativeint size.Y
                    let volume = NativeVolumeRaw.ofNativeInt info ptr
                    NativeVolumeRaw.copy src volume
                )
            else
                let mutable info = 
                    VkImageCreateInfo(
                        VkStructureType.ImageCreateInfo,
                        0n,
                        VkImageCreateFlags.None,
                        VkImageType.D2d,
                        srcFormat,
                        VkExtent3D(size.X, size.Y, size.Z),
                        1u, 1u, VkSampleCountFlags.D1Bit,
                        VkImageTiling.Linear, VkImageUsageFlags.TransferSrcBit,
                        VkSharingMode.Exclusive,
                        0u,
                        NativePtr.zero,
                        VkImageLayout.General
                    )
                let mutable tmp = VkImage.Null

                VkRaw.vkCreateImage(ctx.Device.Handle, &&info, NativePtr.zero, &&tmp)
                    |> check "vkCreateImage"

                let mutable reqs = VkMemoryRequirements()
                VkRaw.vkGetImageMemoryRequirements(ctx.Device.Handle, tmp, &&reqs)

                let mem = ctx.HostVisibleMemory.Alloc(int64 reqs.size)
                VkRaw.vkBindImageMemoryPtr(ctx.Device.Handle, tmp, mem)
                    |> check "vkBindImageMemory"

                DevicePtr.map mem (fun ptr ->
                    let info =
                        NativeVolumeInfo(
                            V3n(channels, -dy, 1n),
                            V3n(nativeint size.X, nativeint size.Y, nativeint size.Z),
                            channelSize
                        )

                    let ptr = ptr + dy * nativeint size.Y
                    let volume = NativeVolumeRaw.ofNativeInt info ptr
                    NativeVolumeRaw.copy src volume
                )

                let tmpImg =
                    Image(
                        ctx, mem, tmp, 
                        VkImageType.D2d, 
                        srcFormat,
                        TextureFormat.One,
                        size, 1, 1, 1, 
                        VkImageUsageFlags.TransferSrcBit,
                        VkImageLayout.General
                    )

                (copy (ImageSubResource(tmpImg)) dst).Run &s

                let clean() =
                    VkRaw.vkDestroyImage(ctx.Device.Handle, tmp, NativePtr.zero)
                    mem.Dispose()

                s <- { s with cleanupActions = clean :: s.cleanupActions }



            s
        )

    let downloadRaw (dstFormat : VkFormat) (src : ImageSubResource) (dst : NativeVolumeRaw) =
        Command.custom (fun s ->
            let mutable s = s

            let ctx = src.Image.Context
            let size = src.Size

            let mutable targetLayout = VkSubresourceLayout()
            let mutable targetSub = VkImageSubresource(VkImageAspectFlags.ColorBit, uint32 src.MipMapLevel, 0u)
            VkRaw.vkGetImageSubresourceLayout(ctx.Device.Handle, src.Image.Handle, &&targetSub, &&targetLayout)

            let pixelSize = targetLayout.size / uint64 (size.X * size.Y) |> nativeint
            let channelSize = dst.Info.ElementSize
            let channels = pixelSize / channelSize
            let dy = nativeint targetLayout.rowPitch / channelSize
            let mem = src.Image.Memory

            if dstFormat = src.Image.Format && mem.IsHostVisible then
                DevicePtr.map mem (fun ptr ->
                    let info =
                        NativeVolumeInfo(
                            V3n(channels, -dy, 1n),
                            V3n(nativeint size.X, nativeint size.Y, nativeint size.Z),
                            channelSize
                        )

                    let ptr = ptr + dy * nativeint size.Y
                    let volume = NativeVolumeRaw.ofNativeInt info ptr
                    NativeVolumeRaw.copy volume dst
                )
            else
                let mutable info = 
                    VkImageCreateInfo(
                        VkStructureType.ImageCreateInfo,
                        0n,
                        VkImageCreateFlags.None,
                        VkImageType.D2d,
                        dstFormat,
                        VkExtent3D(size.X, size.Y, size.Z),
                        1u, 1u, VkSampleCountFlags.D1Bit,
                        VkImageTiling.Linear, VkImageUsageFlags.TransferDstBit,
                        VkSharingMode.Exclusive,
                        0u,
                        NativePtr.zero,
                        VkImageLayout.General
                    )
                let mutable tmp = VkImage.Null

                VkRaw.vkCreateImage(ctx.Device.Handle, &&info, NativePtr.zero, &&tmp)
                    |> check "vkCreateImage"

                let mutable reqs = VkMemoryRequirements()
                VkRaw.vkGetImageMemoryRequirements(ctx.Device.Handle, tmp, &&reqs)

                let mem = ctx.HostVisibleMemory.Alloc(int64 reqs.size)
                VkRaw.vkBindImageMemoryPtr(ctx.Device.Handle, tmp, mem)
                    |> check "vkBindImageMemory"


                let tmpImg =
                    Image(
                        ctx, mem, tmp, 
                        VkImageType.D2d, 
                        dstFormat,
                        TextureFormat.One,
                        size, 1, 1, 1, 
                        VkImageUsageFlags.TransferDstBit,
                        VkImageLayout.General
                    )

                (copy src (ImageSubResource(tmpImg))).Run &s

                let clean() =
                    DevicePtr.map mem (fun ptr ->
                        let info =
                            NativeVolumeInfo(
                                V3n(channels, -dy, 1n),
                                V3n(nativeint size.X, nativeint size.Y, nativeint size.Z),
                                channelSize
                            )

                        let ptr = ptr + dy * nativeint size.Y
                        let volume = NativeVolumeRaw.ofNativeInt info ptr
                        NativeVolumeRaw.copy volume dst
                    )

                    VkRaw.vkDestroyImage(ctx.Device.Handle, tmp, NativePtr.zero)
                    mem.Dispose()

                s <- { s with cleanupActions = clean :: s.cleanupActions }



            s
        )

    let upload (srcFormat : VkFormat) (src : NativeVolume<'a>) (dst : ImageSubResource) =
        uploadRaw srcFormat (NativeVolumeRaw.ofNativeVolume src) dst

    let download (srcFormat : VkFormat) (src : ImageSubResource) (dst : NativeVolume<'a>) =
        downloadRaw srcFormat src (NativeVolumeRaw.ofNativeVolume dst)

    let uploadImage (src : PixImage) (dst : ImageSubResource) =
        Command.custom (fun s ->
            let mutable s = s
            let gc = GCHandle.Alloc(src.Array, GCHandleType.Pinned)
            let info =
                src.VolumeInfo 
                    |> NativeVolumeInfo.ofVolumeInfo
                    |> NativeVolumeInfo.scaled (Marshal.SizeOf src.PixFormat)
            let volume = gc.AddrOfPinnedObject() |> NativeVolumeRaw.ofNativeInt info


            let dataFormat = VkFormat.toUploadFormat src.PixFormat
            (uploadRaw dataFormat volume dst).Run(&s)

            { s with cleanupActions = gc.Free :: s.cleanupActions}

        )

    let downloadImage (src : ImageSubResource) (dst : PixImage) =
        Command.custom (fun s ->
            let mutable s = s
            let gc = GCHandle.Alloc(dst.Array, GCHandleType.Pinned)
            let info =
                dst.VolumeInfo 
                    |> NativeVolumeInfo.ofVolumeInfo
                    |> NativeVolumeInfo.scaled (Marshal.SizeOf dst.PixFormat)
            let volume = gc.AddrOfPinnedObject() |> NativeVolumeRaw.ofNativeInt info

            
            let dataFormat = VkFormat.toUploadFormat dst.PixFormat
            (downloadRaw dataFormat src volume).Run(&s)

            { s with cleanupActions = gc.Free :: s.cleanupActions}

        )

    module private DevIL = 
        open DevILSharp
        do PixImage.InitDevil()

        let typeSize =
            lookupTable [
                ChannelType.Byte, 1n
                ChannelType.Double, 8n
                ChannelType.Float, 4n
                ChannelType.Half, 2n
                ChannelType.Int, 4n
                ChannelType.Short, 2n
                ChannelType.UnsignedByte, 1n
                ChannelType.UnsignedInt, 4n
                ChannelType.UnsignedShort, 2n
            ]

        let dataFormat =
            lookupTable [
                (ChannelFormat.Alpha, ChannelType.Byte),                    VkFormat.R8Sint
                (ChannelFormat.BGR, ChannelType.Byte),                      VkFormat.B8g8r8Sint
                (ChannelFormat.BGRA, ChannelType.Byte),                     VkFormat.B8g8r8a8Sint
                (ChannelFormat.Luminance, ChannelType.Byte),                VkFormat.R8Sint
                (ChannelFormat.LuminanceAlpha, ChannelType.Byte),           VkFormat.R8g8Sint
                (ChannelFormat.RGB, ChannelType.Byte),                      VkFormat.R8g8b8Sint
                (ChannelFormat.RGBA, ChannelType.Byte),                     VkFormat.R8g8b8a8Sint

                (ChannelFormat.Alpha, ChannelType.UnsignedByte),            VkFormat.R8Uint
                (ChannelFormat.BGR, ChannelType.UnsignedByte),              VkFormat.B8g8r8Uint
                (ChannelFormat.BGRA, ChannelType.UnsignedByte),             VkFormat.B8g8r8a8Uint
                (ChannelFormat.Luminance, ChannelType.UnsignedByte),        VkFormat.R8Uint
                (ChannelFormat.LuminanceAlpha, ChannelType.UnsignedByte),   VkFormat.R8g8Uint
                (ChannelFormat.RGB, ChannelType.UnsignedByte),              VkFormat.R8g8b8Uint
                (ChannelFormat.RGBA, ChannelType.UnsignedByte),             VkFormat.R8g8b8a8Uint

                (ChannelFormat.Alpha, ChannelType.Short),                   VkFormat.R16Sint
                (ChannelFormat.Luminance, ChannelType.Short),               VkFormat.R16Sint
                (ChannelFormat.LuminanceAlpha, ChannelType.Short),          VkFormat.R16g16Sint
                (ChannelFormat.RGB, ChannelType.Short),                     VkFormat.R16g16b16Sint
                (ChannelFormat.RGBA, ChannelType.Short),                    VkFormat.R16g16b16a16Sint

                (ChannelFormat.Alpha, ChannelType.UnsignedShort),           VkFormat.R16Uint
                (ChannelFormat.Luminance, ChannelType.UnsignedShort),       VkFormat.R16Uint
                (ChannelFormat.LuminanceAlpha, ChannelType.UnsignedShort),  VkFormat.R16g16Uint
                (ChannelFormat.RGB, ChannelType.UnsignedShort),             VkFormat.R16g16b16Uint
                (ChannelFormat.RGBA, ChannelType.UnsignedShort),            VkFormat.R16g16b16a16Uint

                (ChannelFormat.Alpha, ChannelType.Int),                     VkFormat.R32Sint
                (ChannelFormat.Luminance, ChannelType.Int),                 VkFormat.R32Sint
                (ChannelFormat.LuminanceAlpha, ChannelType.Int),            VkFormat.R32g32Sint
                (ChannelFormat.RGB, ChannelType.Int),                       VkFormat.R32g32b32Sint
                (ChannelFormat.RGBA, ChannelType.Int),                      VkFormat.R32g32b32a32Sint

                (ChannelFormat.Alpha, ChannelType.UnsignedInt),             VkFormat.R32Uint
                (ChannelFormat.Luminance, ChannelType.UnsignedInt),         VkFormat.R32Uint
                (ChannelFormat.LuminanceAlpha, ChannelType.UnsignedInt),    VkFormat.R32g32Uint
                (ChannelFormat.RGB, ChannelType.UnsignedInt),               VkFormat.R32g32b32Uint
                (ChannelFormat.RGBA, ChannelType.UnsignedInt),              VkFormat.R32g32b32a32Uint

            ]

        let checkDevil (msg : string) (b : bool) =
            if not b then failf "%s" msg

        let load (src : string) : NativeVolumeRaw * VkFormat * IDisposable =
            let handle = IL.GenImage()
            try
                IL.BindImage(handle)
                IL.LoadImage(src) |> checkDevil "ilLoadImage"

                let fmt = IL.GetFormat()
                let dataType = IL.GetDataType()
                let width = IL.GetInteger(IntName.ImageWidth) |> nativeint
                let height = IL.GetInteger(IntName.ImageHeight) |> nativeint
                let channels = IL.GetInteger(IntName.ImageChannels) |> nativeint

                // TODO: Vulkan does not seem to support three-channel images properly atm.
                //       so we're converting them to 4 channels currently.
                let channels =
                    if channels = 3n then 
                        IL.ConvertImage(ChannelFormat.BGRA, dataType) |> checkDevil "ilConvertImage"
                        4n
                    else
                        channels

                let ptr = IL.GetData()

                let info =
                    NativeVolumeInfo(
                        V3n(channels, width * channels, 1n),
                        V3n(width, height, 1n),
                        typeSize dataType
                    )

                let v = NativeVolumeRaw.ofNativeInt info ptr

                let dataFormat = dataFormat (fmt, dataType)

                let release = 
                    { new IDisposable with
                        member x.Dispose() =
                            IL.BindImage(0)
                            IL.DeleteImage(handle)
                    }

                v, dataFormat, release

            finally
                IL.BindImage(0)
                IL.DeleteImage(handle)

        let upload (src : string) (dst : ImageSubResource) =
            command {
                let volume, dataFormat, release = load src
                try do! uploadRaw dataFormat volume dst
                finally release.Dispose()
            }

    let internal loadFile (file : string) =
        try DevIL.load file
        with _ ->
            warnf "DevIL failed to load file %A (using PixImage)" file
            let image = 
                PixImage.Create(
                    file, 
                    PixLoadOptions.Default
                )

            let gc = GCHandle.Alloc(image.Array, GCHandleType.Pinned)
            let volume =
                gc.AddrOfPinnedObject()
                    |> NativeVolumeRaw.ofNativeInt (NativeVolumeInfo.ofVolumeInfo image.VolumeInfo) 

            let dataFormat = VkFormat.toUploadFormat image.PixFormat

            let release =
                { new IDisposable with
                    member x.Dispose() =
                        gc.Free()
                }

            volume, dataFormat, release

    let uploadFile (file : string) (dst : ImageSubResource) =
        if System.IO.File.Exists file then
            command {
                try do! DevIL.upload file dst
                with _ ->
                    warnf "DevIL failed to load file %A (using PixImage)" file
                    let image = 
                        PixImage.Create(
                            file, 
                            PixLoadOptions.Default
                        )

                    if not (isNull image) then
                        do! uploadImage image dst
                    else
                        failf "failed to load image: %A" file
            }
        else
            failf "cannot load image from %A" file


[<AutoOpen>]
module ``VkAccessFlags Extensions`` =
    let private anyRead =
        VkAccessFlags.ColorAttachmentReadBit |||
        VkAccessFlags.DepthStencilAttachmentReadBit ||| 
        VkAccessFlags.HostReadBit |||
        VkAccessFlags.IndexReadBit ||| 
        VkAccessFlags.IndirectCommandReadBit ||| 
        VkAccessFlags.MemoryReadBit ||| 
        VkAccessFlags.ShaderReadBit ||| 
        VkAccessFlags.TransferReadBit |||
        VkAccessFlags.UniformReadBit |||
        VkAccessFlags.VertexAttributeReadBit

    let private anyWrite =
        VkAccessFlags.ColorAttachmentWriteBit |||
        VkAccessFlags.DepthStencilAttachmentWriteBit ||| 
        VkAccessFlags.HostWriteBit |||
        VkAccessFlags.MemoryWriteBit |||
        VkAccessFlags.ShaderWriteBit  |||
        VkAccessFlags.TransferWriteBit

    let any = anyRead ||| anyWrite

    type VkAccessFlags with
        static member AnyWrite = anyWrite
        static member AnyRead = anyRead
        static member Any = any

[<AbstractClass; Sealed; Extension>]
type ImageSubResourceExtensions private() =
    
    [<Extension>]
    static member CopyTo(src : ImageSubResource, dst : ImageSubResource) =
        ImageSubResource.copy src dst

    [<Extension>]
    static member BlitTo(src : ImageSubResource, dst : ImageSubResource, filter : VkFilter) =
        ImageSubResource.blit filter src dst

    [<Extension>]
    static member Upload(this : ImageSubResource, src : NativeVolumeRaw, srcFormat : VkFormat) =
        ImageSubResource.uploadRaw srcFormat src this

    [<Extension>]
    static member Upload(this : ImageSubResource, src : NativeVolume<'a>, srcFormat : VkFormat) =
        ImageSubResource.upload srcFormat src this

    [<Extension>]
    static member Upload(this : ImageSubResource, src : PixImage) =
        ImageSubResource.uploadImage src this

    [<Extension>]
    static member Upload(this : ImageSubResource, srcFile : string) =
        ImageSubResource.uploadFile srcFile this


    [<Extension>]
    static member Download(this : ImageSubResource, dst : NativeVolumeRaw, dstFormat : VkFormat) =
        ImageSubResource.downloadRaw dstFormat this dst

    [<Extension>]
    static member Download(this : ImageSubResource, dst : NativeVolume<'a>, dstFormat : VkFormat) =
        ImageSubResource.download dstFormat this dst

    [<Extension>]
    static member Download(this : ImageSubResource, dst : PixImage) =
        ImageSubResource.downloadImage this dst

    [<Extension>]
    static member Download(this : ImageSubResource) =
        command {
            let format = 
                this.Image.Format 
                    |> VkFormat.toTextureFormat
                    |> TextureFormat.toDownloadFormat

            let size = this.Size
            let dst = PixImage.Create(format, int64 size.X, int64 size.Y)
            do! ImageSubResource.downloadImage this dst

            return! fun () -> dst
        }

[<AbstractClass; Sealed; Extension>]
type ImageExtensions private() =
    
    [<Extension>]
    static member CreateImage(this : Context, imageType : VkImageType, format : TextureFormat, 
                              size : V3i, mipLevels : int, arraySize : int, samples : int, 
                              usage : VkImageUsageFlags, layout : VkImageLayout, tiling : VkImageTiling) =
        
        let vkfmt = VkFormat.ofTextureFormat format
        let mutable info =
            VkImageCreateInfo(
                VkStructureType.ImageCreateInfo,
                0n, VkImageCreateFlags.None,
                imageType, 
                vkfmt,
                VkExtent3D(size.X, size.Y, size.Z),
                uint32 mipLevels,
                uint32 arraySize,
                unbox<VkSampleCountFlags> samples,
                tiling,
                usage,
                VkSharingMode.Exclusive,
                0u,
                NativePtr.zero,
                layout
            )


        let mutable img = VkImage.Null
        VkRaw.vkCreateImage(this.Device.Handle, &&info, NativePtr.zero, &&img) 
            |> check "vkCreateImage"

        let mutable reqs = VkMemoryRequirements()
        VkRaw.vkGetImageMemoryRequirements(this.Device.Handle, img, &&reqs)

        let mem = this.Alloc(reqs)
        VkRaw.vkBindImageMemoryPtr(this.Device.Handle, img, mem)
            |> check "vkBindImageMemory"

        Image(this, mem, img, imageType, vkfmt, format, size, mipLevels, arraySize, samples, usage, layout)

    [<Extension>]
    static member Delete(this : Context, img : Image) =
        if img.Handle.IsValid then
            VkRaw.vkDestroyImage(this.Device.Handle, img.Handle, NativePtr.zero)
            img.Memory.Dispose()
            img.Handle <- VkImage.Null

    [<Extension>]
    static member CreateImage2D(this : Context, format : TextureFormat, size : V2i, mipLevels : int, usage : VkImageUsageFlags) =
        ImageExtensions.CreateImage(
            this,
            VkImageType.D2d,
            format,
            V3i(size.X, size.Y, 1),
            mipLevels,
            1, 1, 
            usage,
            VkImageLayout.ShaderReadOnlyOptimal,
            VkImageTiling.Optimal
        )
    
    [<Extension>]
    static member CreateImage3D(this : Context, format : TextureFormat, size : V3i, mipLevels : int, usage : VkImageUsageFlags) =
        ImageExtensions.CreateImage(
            this,
            VkImageType.D3d,
            format,
            size,
            mipLevels,
            1, 1, 
            usage,
            VkImageLayout.ShaderReadOnlyOptimal,
            VkImageTiling.Optimal
        )
    
    [<Extension>]
    static member GenerateMipMaps(this : Image) =
        command {
            for target in 1..this.MipMapLevels-1 do
                let source = target - 1

                do! ImageSubResource.blit 
                        VkFilter.Linear 
                        (ImageSubResource.ofImageLevel source this) 
                        (ImageSubResource.ofImageLevel target this)

                do! Command.custom (fun s ->
                        let mutable barrier =
                            VkImageMemoryBarrier(
                                VkStructureType.ImageMemoryBarrier, 0n,
                                VkAccessFlags.TransferWriteBit,
                                VkAccessFlags.AnyRead,
                                this.Layout,
                                this.Layout,
                                0u, 0u,
                                this.Handle,
                                VkImageSubresourceRange(VkImageAspectFlags.ColorBit, uint32 target, 1u, 0u, 1u)
                            )

                        VkRaw.vkCmdPipelineBarrier(
                            s.buffer.Handle,
                            VkPipelineStageFlags.TopOfPipeBit,
                            VkPipelineStageFlags.TopOfPipeBit,
                            VkDependencyFlags.None,
                            0u, NativePtr.zero,
                            0u, NativePtr.zero,
                            1u, &&barrier
                        )

                        { s with isEmpty = false }
                    )
        }

    [<Extension>]
    static member ToLayout(this : Image, layout : VkImageLayout) =
        Command.custom (fun s ->
            let mutable s = s
            if this.Layout <> layout then 
                let mutable barrier =
                    VkImageMemoryBarrier(
                        VkStructureType.ImageMemoryBarrier,
                        0n,
                        VkAccessFlags.None,
                        VkAccessFlags.None,
                        this.Layout,
                        layout,
                        0u,
                        0u,
                        this.Handle,
                        VkImageSubresourceRange(VkImageAspectFlags.ColorBit, 0u, uint32 this.MipMapLevels, 0u, uint32 this.ArraySize)
                    )


                VkRaw.vkCmdPipelineBarrier(
                    s.buffer.Handle,
                    VkPipelineStageFlags.TopOfPipeBit,
                    VkPipelineStageFlags.TopOfPipeBit,
                    VkDependencyFlags.None,
                    0u, NativePtr.zero,
                    0u, NativePtr.zero,
                    1u, &&barrier
                )

                let clean() = this.Layout <- layout

                { s with cleanupActions = clean :: s.cleanupActions; isEmpty = false }
            else
                s
        )



    [<Extension>]
    static member UploadLevel(this : Image, level : int, src : NativeVolumeRaw, srcFormat : VkFormat) =
        ImageSubResource.uploadRaw srcFormat src (ImageSubResource(this, level))

    [<Extension>]
    static member UploadLevel(this : Image, level : int, src : NativeVolume<'a>, srcFormat : VkFormat) =
        ImageSubResource.upload srcFormat src (ImageSubResource(this, level))

    [<Extension>]
    static member UploadLevel(this : Image, level : int, src : PixImage) =
        ImageSubResource.uploadImage src (ImageSubResource(this, level))

    [<Extension>]
    static member UploadLevel(this : Image, level : int, srcFile : string) =
        ImageSubResource.uploadFile srcFile (ImageSubResource(this, level))

    [<Extension>]
    static member Upload(this : Image, src : NativeVolumeRaw, srcFormat : VkFormat) =
        ImageSubResource.uploadRaw srcFormat src (ImageSubResource(this))

    [<Extension>]
    static member Upload(this : Image, src : NativeVolume<'a>, srcFormat : VkFormat) =
        ImageSubResource.upload srcFormat src (ImageSubResource(this))

    [<Extension>]
    static member Upload(this : Image, src : PixImage) =
        ImageSubResource.uploadImage src (ImageSubResource(this))

    [<Extension>]
    static member Upload(this : Image, srcFile : string) =
        ImageSubResource.uploadFile srcFile (ImageSubResource(this))



    [<Extension>]
    static member DownloadLevel(this : Image, level : int, dst : NativeVolumeRaw, dstFormat : VkFormat) =
        ImageSubResource.downloadRaw dstFormat (ImageSubResource(this, level)) dst

    [<Extension>]
    static member DownloadLevel(this : Image, level : int, dst : NativeVolume<'a>, dstFormat : VkFormat) =
        ImageSubResource.download dstFormat (ImageSubResource(this, level)) dst

    [<Extension>]
    static member DownloadLevel(this : Image, level : int, dst : PixImage) =
        ImageSubResource.downloadImage (ImageSubResource(this, level)) dst

    [<Extension>]
    static member DownloadLevel(this : Image, level : int) =
        ImageSubResource(this, level).Download()

    [<Extension>]
    static member Download(this : Image, dst : NativeVolumeRaw, dstFormat : VkFormat) =
        ImageSubResource.downloadRaw dstFormat (ImageSubResource(this)) dst

    [<Extension>]
    static member Download(this : Image, dst : NativeVolume<'a>, dstFormat : VkFormat) =
        ImageSubResource.download dstFormat (ImageSubResource(this)) dst

    [<Extension>]
    static member Download(this : Image, dst : PixImage) =
        ImageSubResource.downloadImage (ImageSubResource(this)) dst

    [<Extension>]
    static member Download(this : Image) =
        ImageSubResource(this).Download()

