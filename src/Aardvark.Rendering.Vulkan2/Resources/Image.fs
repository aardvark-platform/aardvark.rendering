namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"


// ===========================================================================================
// Format Conversions
// ===========================================================================================

[<AutoOpen>]
module ``Image Format Extensions`` =
    
    let private writeAccess = 
        VkAccessFlags.ColorAttachmentWriteBit |||
        VkAccessFlags.DepthStencilAttachmentWriteBit |||
        VkAccessFlags.HostWriteBit |||
        VkAccessFlags.MemoryWriteBit |||
        VkAccessFlags.ShaderWriteBit |||
        VkAccessFlags.TransferWriteBit
  
    let private readAccess = 
        VkAccessFlags.ColorAttachmentReadBit ||| 
        VkAccessFlags.DepthStencilAttachmentReadBit ||| 
        VkAccessFlags.HostReadBit |||
        VkAccessFlags.IndexReadBit |||
        VkAccessFlags.IndirectCommandReadBit ||| 
        VkAccessFlags.InputAttachmentReadBit |||
        VkAccessFlags.MemoryReadBit |||
        VkAccessFlags.ShaderReadBit |||
        VkAccessFlags.TransferReadBit |||
        VkAccessFlags.UniformReadBit |||
        VkAccessFlags.VertexAttributeReadBit

    let private allAspects =
        VkImageAspectFlags.ColorBit |||
        VkImageAspectFlags.DepthBit ||| 
        VkImageAspectFlags.StencilBit

    type VkAccessFlags with
        static member Write = writeAccess
        static member Read = readAccess

    type VkImageAspectFlags with
        static member All = allAspects

    module VkImageType =
        let ofTextureDimension =
            LookupTable.lookupTable [
                TextureDimension.Texture1D, VkImageType.D1d
                TextureDimension.Texture2D, VkImageType.D2d
                TextureDimension.Texture3D, VkImageType.D3d
                TextureDimension.TextureCube, VkImageType.D2d
            ]

    type ImageKind =
        | Color = 1
        | Depth = 2
        | DepthStencil = 3

    module VkIndexType =
        let ofType =
            LookupTable.lookupTable [
                typeof<int16>, VkIndexType.Uint16
                typeof<uint16>, VkIndexType.Uint16
                typeof<int32>, VkIndexType.Uint32
                typeof<uint32>, VkIndexType.Uint32
            ]

    module VkFormat =
        let ofTextureFormat =
            LookupTable.lookupTable [
                TextureFormat.Bgr8, VkFormat.B8g8r8Unorm
                TextureFormat.Bgra8, VkFormat.B8g8r8a8Unorm


                TextureFormat.DepthComponent, VkFormat.D24UnormS8Uint
                TextureFormat.Alpha, VkFormat.R8Unorm
                TextureFormat.Rgb, VkFormat.R8g8b8Unorm
                TextureFormat.Rgba, VkFormat.R8g8b8a8Unorm
                TextureFormat.Luminance, VkFormat.R8Unorm
                TextureFormat.LuminanceAlpha, VkFormat.R8g8Unorm
                TextureFormat.Rgb4, VkFormat.R4g4b4a4UnormPack16
                TextureFormat.Rgb5, VkFormat.R5g5b5a1UnormPack16
                TextureFormat.Rgb8, VkFormat.R8g8b8Unorm
                TextureFormat.Rgb10, VkFormat.A2b10g10r10UnormPack32
                TextureFormat.Rgb16, VkFormat.R16g16b16Uint
                TextureFormat.Rgba4, VkFormat.R4g4b4a4UnormPack16
                TextureFormat.Rgb5A1, VkFormat.R5g5b5a1UnormPack16
                TextureFormat.Rgba8, VkFormat.R8g8b8a8Unorm
                TextureFormat.Rgb10A2, VkFormat.A2r10g10b10UnormPack32
                TextureFormat.Rgba16, VkFormat.R16g16b16a16Unorm
                TextureFormat.DualAlpha4Sgis, VkFormat.R4g4UnormPack8
                TextureFormat.DualAlpha8Sgis, VkFormat.R8g8Unorm
                TextureFormat.DualAlpha16Sgis, VkFormat.R16g16Unorm
                TextureFormat.DualLuminance4Sgis, VkFormat.R4g4UnormPack8
                TextureFormat.DualLuminance8Sgis, VkFormat.R8g8Unorm
                TextureFormat.DualLuminance16Sgis, VkFormat.R16g16Unorm
                TextureFormat.DualIntensity4Sgis, VkFormat.R4g4UnormPack8
                TextureFormat.DualIntensity8Sgis, VkFormat.R8g8Unorm
                TextureFormat.DualIntensity16Sgis, VkFormat.R16g16Unorm
                TextureFormat.DualLuminanceAlpha4Sgis, VkFormat.R4g4UnormPack8
                TextureFormat.DualLuminanceAlpha8Sgis, VkFormat.R8g8Unorm
                TextureFormat.QuadAlpha4Sgis, VkFormat.R4g4b4a4UnormPack16
                TextureFormat.QuadAlpha8Sgis, VkFormat.R8g8b8a8Unorm
                TextureFormat.QuadLuminance4Sgis, VkFormat.R4g4b4a4UnormPack16
                TextureFormat.QuadLuminance8Sgis, VkFormat.R8g8b8a8Unorm
                TextureFormat.QuadIntensity4Sgis, VkFormat.R4g4b4a4UnormPack16
                TextureFormat.QuadIntensity8Sgis, VkFormat.R8g8b8a8Unorm
                TextureFormat.DepthComponent16, VkFormat.D16Unorm
                TextureFormat.DepthComponent24, VkFormat.D24UnormS8Uint
                TextureFormat.DepthComponent32, VkFormat.D32SfloatS8Uint
//                TextureFormat.CompressedRed, VkFormat.
//                TextureFormat.CompressedRg, VkFormat.
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
//                TextureFormat.CompressedRgbS3tcDxt1Ext, VkFormat.
//                TextureFormat.CompressedRgbaS3tcDxt1Ext, VkFormat.
//                TextureFormat.CompressedRgbaS3tcDxt3Ext, VkFormat.
//                TextureFormat.CompressedRgbaS3tcDxt5Ext, VkFormat.
//                TextureFormat.RgbIccSgix, VkFormat.
//                TextureFormat.RgbaIccSgix, VkFormat.
//                TextureFormat.AlphaIccSgix, VkFormat.
//                TextureFormat.LuminanceIccSgix, VkFormat.
//                TextureFormat.IntensityIccSgix, VkFormat.
//                TextureFormat.LuminanceAlphaIccSgix, VkFormat.
//                TextureFormat.R5G6B5IccSgix, VkFormat.
//                TextureFormat.R5G6B5A8IccSgix, VkFormat.
//                TextureFormat.Alpha16IccSgix, VkFormat.
//                TextureFormat.Luminance16IccSgix, VkFormat.
//                TextureFormat.Intensity16IccSgix, VkFormat.
//                TextureFormat.Luminance16Alpha8IccSgix, VkFormat.
//                TextureFormat.CompressedAlpha, VkFormat.
//                TextureFormat.CompressedLuminance, VkFormat.
//                TextureFormat.CompressedLuminanceAlpha, VkFormat.
//                TextureFormat.CompressedIntensity, VkFormat.
//                TextureFormat.CompressedRgb, VkFormat.
//                TextureFormat.CompressedRgba, VkFormat.
                TextureFormat.DepthStencil, VkFormat.D24UnormS8Uint
                TextureFormat.Rgba32f, VkFormat.R32g32b32a32Sfloat
                TextureFormat.Rgb32f, VkFormat.R32g32b32Sfloat
                TextureFormat.Rgba16f, VkFormat.R16g16b16a16Sfloat
                TextureFormat.Rgb16f, VkFormat.R16g16b16Sfloat
                TextureFormat.Depth24Stencil8, VkFormat.D24UnormS8Uint
//                TextureFormat.R11fG11fB10f, VkFormat.R11
//                TextureFormat.Rgb9E5, VkFormat.
                TextureFormat.Srgb, VkFormat.R8g8b8Srgb
                TextureFormat.Srgb8, VkFormat.R8g8b8Srgb
                TextureFormat.SrgbAlpha, VkFormat.R8g8b8a8Srgb
                TextureFormat.Srgb8Alpha8, VkFormat.R8g8b8a8Srgb
//                TextureFormat.SluminanceAlpha, VkFormat.
//                TextureFormat.Sluminance8Alpha8, VkFormat.
//                TextureFormat.Sluminance, VkFormat.
//                TextureFormat.Sluminance8, VkFormat.
//                TextureFormat.CompressedSrgb, VkFormat.
//                TextureFormat.CompressedSrgbAlpha, VkFormat.
//                TextureFormat.CompressedSluminance, VkFormat.
//                TextureFormat.CompressedSluminanceAlpha, VkFormat.
//                TextureFormat.CompressedSrgbS3tcDxt1Ext, VkFormat.
//                TextureFormat.CompressedSrgbAlphaS3tcDxt1Ext, VkFormat.
//                TextureFormat.CompressedSrgbAlphaS3tcDxt3Ext, VkFormat.
//                TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext, VkFormat.
                TextureFormat.DepthComponent32f, VkFormat.D32Sfloat
                TextureFormat.Depth32fStencil8, VkFormat.D32SfloatS8Uint
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
//                TextureFormat.CompressedRedRgtc1, VkFormat.
//                TextureFormat.CompressedSignedRedRgtc1, VkFormat.
//                TextureFormat.CompressedRgRgtc2, VkFormat.
//                TextureFormat.CompressedSignedRgRgtc2, VkFormat.
//                TextureFormat.CompressedRgbaBptcUnorm, VkFormat.
//                TextureFormat.CompressedRgbBptcSignedFloat, VkFormat.
//                TextureFormat.CompressedRgbBptcUnsignedFloat, VkFormat.
                TextureFormat.R8Snorm, VkFormat.R8Snorm
                TextureFormat.Rg8Snorm, VkFormat.R8g8Snorm
                TextureFormat.Rgb8Snorm, VkFormat.R8g8b8Snorm
                TextureFormat.Rgba8Snorm, VkFormat.R8g8b8a8Snorm
                TextureFormat.R16Snorm, VkFormat.R16Snorm
                TextureFormat.Rg16Snorm, VkFormat.R16g16Snorm
                TextureFormat.Rgb16Snorm, VkFormat.R16g16b16Snorm
                TextureFormat.Rgba16Snorm, VkFormat.R16g16b16a16Snorm
                TextureFormat.Rgb10A2ui, VkFormat.A2b10g10r10UintPack32
//                TextureFormat.One, VkFormat.
//                TextureFormat.Two, VkFormat.
//                TextureFormat.Three, VkFormat.
//                TextureFormat.Four, VkFormat.

            ]

        let ofRenderbufferFormat (fmt : RenderbufferFormat) =
            fmt |> int |> unbox<TextureFormat> |> ofTextureFormat

        let toTextureFormat =
            let unknown = unbox<TextureFormat> 0
            LookupTable.lookupTable [
                VkFormat.Undefined, unknown
                VkFormat.R4g4UnormPack8, unknown
                VkFormat.R4g4b4a4UnormPack16, TextureFormat.Rgba4
                VkFormat.B4g4r4a4UnormPack16, unknown
                VkFormat.R5g6b5UnormPack16, TextureFormat.R5G6B5IccSgix
                VkFormat.B5g6r5UnormPack16, unknown
                VkFormat.R5g5b5a1UnormPack16, TextureFormat.R5G6B5A8IccSgix
                VkFormat.B5g5r5a1UnormPack16, unknown
                VkFormat.A1r5g5b5UnormPack16, unknown
                VkFormat.R8Unorm, TextureFormat.R8
                VkFormat.R8Snorm, TextureFormat.R8Snorm
                VkFormat.R8Uscaled, TextureFormat.R8
                VkFormat.R8Sscaled, TextureFormat.R8
                VkFormat.R8Uint, TextureFormat.R8ui
                VkFormat.R8Sint, TextureFormat.R8i
                VkFormat.R8Srgb, TextureFormat.R8
                VkFormat.R8g8Unorm, TextureFormat.Rg8
                VkFormat.R8g8Snorm, TextureFormat.Rg8Snorm
                VkFormat.R8g8Uscaled, TextureFormat.Rg8
                VkFormat.R8g8Sscaled, TextureFormat.Rg8
                VkFormat.R8g8Uint, TextureFormat.Rg8ui
                VkFormat.R8g8Sint, TextureFormat.Rg8i
                VkFormat.R8g8Srgb, TextureFormat.Rg8
                VkFormat.R8g8b8Unorm, TextureFormat.Rgb8
                VkFormat.R8g8b8Snorm, TextureFormat.Rgb8Snorm
                VkFormat.R8g8b8Uscaled, TextureFormat.Rgb8
                VkFormat.R8g8b8Sscaled, TextureFormat.Rgb8
                VkFormat.R8g8b8Uint, TextureFormat.Rgb8ui
                VkFormat.R8g8b8Sint, TextureFormat.Rgb8i
                VkFormat.R8g8b8Srgb, TextureFormat.Srgb8
                VkFormat.B8g8r8Unorm, TextureFormat.Bgr8
                VkFormat.B8g8r8Snorm, TextureFormat.Bgr8
                VkFormat.B8g8r8Uscaled, TextureFormat.Bgr8
                VkFormat.B8g8r8Sscaled, TextureFormat.Bgr8
                VkFormat.B8g8r8Uint, TextureFormat.Bgr8
                VkFormat.B8g8r8Sint, TextureFormat.Bgr8
                VkFormat.B8g8r8Srgb, TextureFormat.Bgr8
                VkFormat.R8g8b8a8Unorm, TextureFormat.Rgba8
                VkFormat.R8g8b8a8Snorm, TextureFormat.Rgba8Snorm
                VkFormat.R8g8b8a8Uscaled, TextureFormat.Rgba8
                VkFormat.R8g8b8a8Sscaled, TextureFormat.Rgba8
                VkFormat.R8g8b8a8Uint, TextureFormat.Rgba8ui
                VkFormat.R8g8b8a8Sint, TextureFormat.Rgba8i
                VkFormat.R8g8b8a8Srgb, TextureFormat.Srgb8Alpha8
                VkFormat.B8g8r8a8Unorm, TextureFormat.Bgra8
                VkFormat.B8g8r8a8Snorm, TextureFormat.Bgra8
                VkFormat.B8g8r8a8Uscaled, TextureFormat.Bgra8
                VkFormat.B8g8r8a8Sscaled, TextureFormat.Bgra8
                VkFormat.B8g8r8a8Uint, TextureFormat.Bgra8
                VkFormat.B8g8r8a8Sint, TextureFormat.Bgra8
                VkFormat.B8g8r8a8Srgb, TextureFormat.Bgra8
                VkFormat.A8b8g8r8UnormPack32, unknown
                VkFormat.A8b8g8r8SnormPack32, unknown
                VkFormat.A8b8g8r8UscaledPack32, unknown
                VkFormat.A8b8g8r8SscaledPack32, unknown
                VkFormat.A8b8g8r8UintPack32, unknown
                VkFormat.A8b8g8r8SintPack32, unknown
                VkFormat.A8b8g8r8SrgbPack32, unknown
                VkFormat.A2r10g10b10UnormPack32, unknown
                VkFormat.A2r10g10b10SnormPack32, unknown
                VkFormat.A2r10g10b10UscaledPack32, unknown
                VkFormat.A2r10g10b10SscaledPack32, unknown
                VkFormat.A2r10g10b10UintPack32, unknown
                VkFormat.A2r10g10b10SintPack32, unknown
                VkFormat.A2b10g10r10UnormPack32, unknown
                VkFormat.A2b10g10r10SnormPack32, unknown
                VkFormat.A2b10g10r10UscaledPack32, unknown
                VkFormat.A2b10g10r10SscaledPack32, unknown
                VkFormat.A2b10g10r10UintPack32, unknown
                VkFormat.A2b10g10r10SintPack32, unknown
                VkFormat.R16Unorm, TextureFormat.R16
                VkFormat.R16Snorm, TextureFormat.R16Snorm
                VkFormat.R16Uscaled, TextureFormat.R16
                VkFormat.R16Sscaled, TextureFormat.R16
                VkFormat.R16Uint, TextureFormat.R16ui
                VkFormat.R16Sint, TextureFormat.R16i
                VkFormat.R16Sfloat, TextureFormat.R16f
                VkFormat.R16g16Unorm, TextureFormat.Rg16
                VkFormat.R16g16Snorm, TextureFormat.Rg16Snorm
                VkFormat.R16g16Uscaled, TextureFormat.Rg16
                VkFormat.R16g16Sscaled, TextureFormat.Rg16
                VkFormat.R16g16Uint, TextureFormat.Rg16ui
                VkFormat.R16g16Sint, TextureFormat.Rg16i
                VkFormat.R16g16Sfloat, TextureFormat.Rg16f
                VkFormat.R16g16b16Unorm, TextureFormat.Rgb16
                VkFormat.R16g16b16Snorm, TextureFormat.Rgb16Snorm
                VkFormat.R16g16b16Uscaled, TextureFormat.Rgb16
                VkFormat.R16g16b16Sscaled, TextureFormat.Rgb16
                VkFormat.R16g16b16Uint, TextureFormat.Rgb16ui
                VkFormat.R16g16b16Sint, TextureFormat.Rgb16i
                VkFormat.R16g16b16Sfloat, TextureFormat.Rgb16f
                VkFormat.R16g16b16a16Unorm, TextureFormat.Rgba16
                VkFormat.R16g16b16a16Snorm, TextureFormat.Rgba16Snorm
                VkFormat.R16g16b16a16Uscaled, TextureFormat.Rgba16
                VkFormat.R16g16b16a16Sscaled, TextureFormat.Rgba16
                VkFormat.R16g16b16a16Uint, TextureFormat.Rgba16ui
                VkFormat.R16g16b16a16Sint, TextureFormat.Rgba16i
                VkFormat.R16g16b16a16Sfloat, TextureFormat.Rgba16f
                VkFormat.R32Uint, TextureFormat.R32ui
                VkFormat.R32Sint, TextureFormat.R32i
                VkFormat.R32Sfloat, TextureFormat.R32f
                VkFormat.R32g32Uint, TextureFormat.Rg32ui
                VkFormat.R32g32Sint, TextureFormat.Rg32i
                VkFormat.R32g32Sfloat, TextureFormat.Rg32f
                VkFormat.R32g32b32Uint, TextureFormat.Rgb32ui
                VkFormat.R32g32b32Sint, TextureFormat.Rgb32i
                VkFormat.R32g32b32Sfloat, TextureFormat.Rgb32f
                VkFormat.R32g32b32a32Uint, TextureFormat.Rgba32ui
                VkFormat.R32g32b32a32Sint, TextureFormat.Rgba32i
                VkFormat.R32g32b32a32Sfloat, TextureFormat.Rgba32f
                VkFormat.R64Uint, unknown
                VkFormat.R64Sint, unknown
                VkFormat.R64Sfloat, unknown
                VkFormat.R64g64Uint, unknown
                VkFormat.R64g64Sint, unknown
                VkFormat.R64g64Sfloat, unknown
                VkFormat.R64g64b64Uint, unknown
                VkFormat.R64g64b64Sint, unknown
                VkFormat.R64g64b64Sfloat, unknown
                VkFormat.R64g64b64a64Uint, unknown
                VkFormat.R64g64b64a64Sint, unknown
                VkFormat.R64g64b64a64Sfloat, unknown
                VkFormat.B10g11r11UfloatPack32, TextureFormat.R11fG11fB10f
                VkFormat.E5b9g9r9UfloatPack32, unknown
                VkFormat.D16Unorm, TextureFormat.DepthComponent16
                VkFormat.X8D24UnormPack32, TextureFormat.DepthComponent24
                VkFormat.D32Sfloat, TextureFormat.DepthComponent32f
                VkFormat.S8Uint, unknown
                VkFormat.D16UnormS8Uint, unknown
                VkFormat.D24UnormS8Uint, TextureFormat.Depth24Stencil8
                VkFormat.D32SfloatS8Uint, TextureFormat.Depth32fStencil8
                VkFormat.Bc1RgbUnormBlock, unknown
                VkFormat.Bc1RgbSrgbBlock, unknown
                VkFormat.Bc1RgbaUnormBlock, unknown
                VkFormat.Bc1RgbaSrgbBlock, unknown
                VkFormat.Bc2UnormBlock, unknown
                VkFormat.Bc2SrgbBlock, unknown
                VkFormat.Bc3UnormBlock, unknown
                VkFormat.Bc3SrgbBlock, unknown
                VkFormat.Bc4UnormBlock, unknown
                VkFormat.Bc4SnormBlock, unknown
                VkFormat.Bc5UnormBlock, unknown
                VkFormat.Bc5SnormBlock, unknown
                VkFormat.Bc6hUfloatBlock, unknown
                VkFormat.Bc6hSfloatBlock, unknown
                VkFormat.Bc7UnormBlock, unknown
                VkFormat.Bc7SrgbBlock, unknown
                VkFormat.Etc2R8g8b8UnormBlock, unknown
                VkFormat.Etc2R8g8b8SrgbBlock, unknown
                VkFormat.Etc2R8g8b8a1UnormBlock, unknown
                VkFormat.Etc2R8g8b8a1SrgbBlock, unknown
                VkFormat.Etc2R8g8b8a8UnormBlock, unknown
                VkFormat.Etc2R8g8b8a8SrgbBlock, unknown
                VkFormat.EacR11UnormBlock, unknown
                VkFormat.EacR11SnormBlock, unknown
                VkFormat.EacR11g11UnormBlock, unknown
                VkFormat.EacR11g11SnormBlock, unknown
                VkFormat.Astc44UnormBlock, unknown
                VkFormat.Astc44SrgbBlock, unknown
                VkFormat.Astc54UnormBlock, unknown
                VkFormat.Astc54SrgbBlock, unknown
                VkFormat.Astc55UnormBlock, unknown
                VkFormat.Astc55SrgbBlock, unknown
                VkFormat.Astc65UnormBlock, unknown
                VkFormat.Astc65SrgbBlock, unknown
                VkFormat.Astc66UnormBlock, unknown
                VkFormat.Astc66SrgbBlock, unknown
                VkFormat.Astc85UnormBlock, unknown
                VkFormat.Astc85SrgbBlock, unknown
                VkFormat.Astc86UnormBlock, unknown
                VkFormat.Astc86SrgbBlock, unknown
                VkFormat.Astc88UnormBlock, unknown
                VkFormat.Astc88SrgbBlock, unknown
                VkFormat.Astc105UnormBlock, unknown
                VkFormat.Astc105SrgbBlock, unknown
                VkFormat.Astc106UnormBlock, unknown
                VkFormat.Astc106SrgbBlock, unknown
                VkFormat.Astc108UnormBlock, unknown
                VkFormat.Astc108SrgbBlock, unknown
                VkFormat.Astc1010UnormBlock, unknown
                VkFormat.Astc1010SrgbBlock, unknown
                VkFormat.Astc1210UnormBlock, unknown
                VkFormat.Astc1210SrgbBlock, unknown
                VkFormat.Astc1212UnormBlock, unknown
                VkFormat.Astc1212SrgbBlock, unknown       
            ]

        let toRenderbufferFormat (fmt : VkFormat) =
            fmt |> toTextureFormat |> int |> unbox<RenderbufferFormat>


        let private depthFormats = HashSet.ofList [ VkFormat.D16Unorm; VkFormat.D32Sfloat; VkFormat.X8D24UnormPack32 ]
        let private depthStencilFormats = HashSet.ofList [VkFormat.D16UnormS8Uint; VkFormat.D24UnormS8Uint; VkFormat.D32SfloatS8Uint ]

        let toAspect (fmt : VkFormat) =
            if depthStencilFormats.Contains fmt then VkImageAspectFlags.DepthBit ||| VkImageAspectFlags.StencilBit
            elif depthFormats.Contains fmt then VkImageAspectFlags.DepthBit
            else VkImageAspectFlags.ColorBit

        let toImageKind (fmt : VkFormat) =
            if depthStencilFormats.Contains fmt then ImageKind.DepthStencil
            elif depthFormats.Contains fmt then ImageKind.Depth
            else ImageKind.Color


        let toColFormat =
            let r = Col.Format.Gray
            let rg = Col.Format.NormalUV
            let rgb = Col.Format.RGB
            let rgba = Col.Format.RGBA
            let bgr = Col.Format.BGR
            let bgra = Col.Format.BGRA
            let argb = Col.Format.None
            let abgr = Col.Format.None
            let none = Col.Format.None
            let d = Col.Format.Gray
            let ds = Col.Format.GrayAlpha
            let s = Col.Format.Alpha
            let unknown = Col.Format.None
            LookupTable.lookupTable [
                VkFormat.Undefined, none
                VkFormat.R4g4UnormPack8, rg
                VkFormat.R4g4b4a4UnormPack16, rgba
                VkFormat.B4g4r4a4UnormPack16, bgra
                VkFormat.R5g6b5UnormPack16, rgb
                VkFormat.B5g6r5UnormPack16, bgr
                VkFormat.R5g5b5a1UnormPack16, rgba
                VkFormat.B5g5r5a1UnormPack16, bgra
                VkFormat.A1r5g5b5UnormPack16, argb
                VkFormat.R8Unorm, r
                VkFormat.R8Snorm, r
                VkFormat.R8Uscaled, r
                VkFormat.R8Sscaled, r
                VkFormat.R8Uint, r
                VkFormat.R8Sint, r
                VkFormat.R8Srgb, r
                VkFormat.R8g8Unorm, rg
                VkFormat.R8g8Snorm, rg
                VkFormat.R8g8Uscaled, rg
                VkFormat.R8g8Sscaled, rg
                VkFormat.R8g8Uint, rg
                VkFormat.R8g8Sint, rg
                VkFormat.R8g8Srgb, rg
                VkFormat.R8g8b8Unorm, rgb
                VkFormat.R8g8b8Snorm, rgb
                VkFormat.R8g8b8Uscaled, rgb
                VkFormat.R8g8b8Sscaled, rgb
                VkFormat.R8g8b8Uint, rgb
                VkFormat.R8g8b8Sint, rgb
                VkFormat.R8g8b8Srgb, rgb
                VkFormat.B8g8r8Unorm, bgr
                VkFormat.B8g8r8Snorm, bgr
                VkFormat.B8g8r8Uscaled, bgr
                VkFormat.B8g8r8Sscaled, bgr
                VkFormat.B8g8r8Uint, bgr
                VkFormat.B8g8r8Sint, bgr
                VkFormat.B8g8r8Srgb, bgr
                VkFormat.R8g8b8a8Unorm, rgba
                VkFormat.R8g8b8a8Snorm, rgba
                VkFormat.R8g8b8a8Uscaled, rgba
                VkFormat.R8g8b8a8Sscaled, rgba
                VkFormat.R8g8b8a8Uint, rgba
                VkFormat.R8g8b8a8Sint, rgba
                VkFormat.R8g8b8a8Srgb, rgba
                VkFormat.B8g8r8a8Unorm, bgra
                VkFormat.B8g8r8a8Snorm, bgra
                VkFormat.B8g8r8a8Uscaled, bgra
                VkFormat.B8g8r8a8Sscaled, bgra
                VkFormat.B8g8r8a8Uint, bgra
                VkFormat.B8g8r8a8Sint, bgra
                VkFormat.B8g8r8a8Srgb, bgra
                VkFormat.A8b8g8r8UnormPack32, abgr
                VkFormat.A8b8g8r8SnormPack32, abgr
                VkFormat.A8b8g8r8UscaledPack32, abgr
                VkFormat.A8b8g8r8SscaledPack32, abgr
                VkFormat.A8b8g8r8UintPack32, abgr
                VkFormat.A8b8g8r8SintPack32, abgr
                VkFormat.A8b8g8r8SrgbPack32, abgr
                VkFormat.A2r10g10b10UnormPack32, argb
                VkFormat.A2r10g10b10SnormPack32, argb
                VkFormat.A2r10g10b10UscaledPack32, argb
                VkFormat.A2r10g10b10SscaledPack32, argb
                VkFormat.A2r10g10b10UintPack32, argb
                VkFormat.A2r10g10b10SintPack32, argb
                VkFormat.A2b10g10r10UnormPack32, abgr
                VkFormat.A2b10g10r10SnormPack32, abgr
                VkFormat.A2b10g10r10UscaledPack32, abgr
                VkFormat.A2b10g10r10SscaledPack32, abgr
                VkFormat.A2b10g10r10UintPack32, abgr
                VkFormat.A2b10g10r10SintPack32, abgr
                VkFormat.R16Unorm, r
                VkFormat.R16Snorm, r
                VkFormat.R16Uscaled, r
                VkFormat.R16Sscaled, r
                VkFormat.R16Uint, r
                VkFormat.R16Sint, r
                VkFormat.R16Sfloat, r
                VkFormat.R16g16Unorm, rg
                VkFormat.R16g16Snorm, rg
                VkFormat.R16g16Uscaled, rg
                VkFormat.R16g16Sscaled, rg
                VkFormat.R16g16Uint, rg
                VkFormat.R16g16Sint, rg
                VkFormat.R16g16Sfloat, rg
                VkFormat.R16g16b16Unorm, rgb
                VkFormat.R16g16b16Snorm, rgb
                VkFormat.R16g16b16Uscaled, rgb
                VkFormat.R16g16b16Sscaled, rgb
                VkFormat.R16g16b16Uint, rgb
                VkFormat.R16g16b16Sint, rgb
                VkFormat.R16g16b16Sfloat, rgb
                VkFormat.R16g16b16a16Unorm, rgba
                VkFormat.R16g16b16a16Snorm, rgba
                VkFormat.R16g16b16a16Uscaled, rgba
                VkFormat.R16g16b16a16Sscaled, rgba
                VkFormat.R16g16b16a16Uint, rgba
                VkFormat.R16g16b16a16Sint, rgba
                VkFormat.R16g16b16a16Sfloat, rgba
                VkFormat.R32Uint, r
                VkFormat.R32Sint, r
                VkFormat.R32Sfloat, r
                VkFormat.R32g32Uint, rg
                VkFormat.R32g32Sint, rg
                VkFormat.R32g32Sfloat, rg
                VkFormat.R32g32b32Uint, rgb
                VkFormat.R32g32b32Sint, rgb
                VkFormat.R32g32b32Sfloat, rgb
                VkFormat.R32g32b32a32Uint, rgba
                VkFormat.R32g32b32a32Sint, rgba
                VkFormat.R32g32b32a32Sfloat, rgba
                VkFormat.R64Uint, r
                VkFormat.R64Sint, r
                VkFormat.R64Sfloat, r
                VkFormat.R64g64Uint, rg
                VkFormat.R64g64Sint, rg
                VkFormat.R64g64Sfloat, rg
                VkFormat.R64g64b64Uint, rgb
                VkFormat.R64g64b64Sint, rgb
                VkFormat.R64g64b64Sfloat, rgb
                VkFormat.R64g64b64a64Uint, rgba
                VkFormat.R64g64b64a64Sint, rgba
                VkFormat.R64g64b64a64Sfloat, rgba
                VkFormat.B10g11r11UfloatPack32, bgr
                VkFormat.E5b9g9r9UfloatPack32, bgr
                VkFormat.D16Unorm, d
                VkFormat.X8D24UnormPack32, d
                VkFormat.D32Sfloat, ds
                VkFormat.S8Uint, s
                VkFormat.D16UnormS8Uint, ds
                VkFormat.D24UnormS8Uint, ds
                VkFormat.D32SfloatS8Uint, ds
                VkFormat.Bc1RgbUnormBlock, rgb
                VkFormat.Bc1RgbSrgbBlock, rgb
                VkFormat.Bc1RgbaUnormBlock, rgba
                VkFormat.Bc1RgbaSrgbBlock, rgba
                VkFormat.Bc2UnormBlock, unknown
                VkFormat.Bc2SrgbBlock, rgb
                VkFormat.Bc3UnormBlock, unknown
                VkFormat.Bc3SrgbBlock, rgb
                VkFormat.Bc4UnormBlock, unknown
                VkFormat.Bc4SnormBlock, unknown
                VkFormat.Bc5UnormBlock, unknown
                VkFormat.Bc5SnormBlock, unknown
                VkFormat.Bc6hUfloatBlock, unknown
                VkFormat.Bc6hSfloatBlock, unknown
                VkFormat.Bc7UnormBlock, unknown
                VkFormat.Bc7SrgbBlock, rgb
                VkFormat.Etc2R8g8b8UnormBlock, rgb
                VkFormat.Etc2R8g8b8SrgbBlock, rgb
                VkFormat.Etc2R8g8b8a1UnormBlock, rgba
                VkFormat.Etc2R8g8b8a1SrgbBlock, rgba
                VkFormat.Etc2R8g8b8a8UnormBlock, rgba
                VkFormat.Etc2R8g8b8a8SrgbBlock, rgba
                VkFormat.EacR11UnormBlock, r
                VkFormat.EacR11SnormBlock, r
                VkFormat.EacR11g11UnormBlock, rg
                VkFormat.EacR11g11SnormBlock, rg
                VkFormat.Astc44UnormBlock, unknown
                VkFormat.Astc44SrgbBlock, rgb
                VkFormat.Astc54UnormBlock, unknown
                VkFormat.Astc54SrgbBlock, rgb
                VkFormat.Astc55UnormBlock, unknown
                VkFormat.Astc55SrgbBlock, rgb
                VkFormat.Astc65UnormBlock, unknown
                VkFormat.Astc65SrgbBlock, rgb
                VkFormat.Astc66UnormBlock, unknown
                VkFormat.Astc66SrgbBlock, rgb
                VkFormat.Astc85UnormBlock, unknown
                VkFormat.Astc85SrgbBlock, rgb
                VkFormat.Astc86UnormBlock, unknown
                VkFormat.Astc86SrgbBlock, rgb
                VkFormat.Astc88UnormBlock, unknown
                VkFormat.Astc88SrgbBlock, rgb
                VkFormat.Astc105UnormBlock, unknown
                VkFormat.Astc105SrgbBlock, rgb
                VkFormat.Astc106UnormBlock, unknown
                VkFormat.Astc106SrgbBlock, rgb
                VkFormat.Astc108UnormBlock, unknown
                VkFormat.Astc108SrgbBlock, rgb
                VkFormat.Astc1010UnormBlock, unknown
                VkFormat.Astc1010SrgbBlock, rgb
                VkFormat.Astc1210UnormBlock, unknown
                VkFormat.Astc1210SrgbBlock, rgb
                VkFormat.Astc1212UnormBlock, unknown
                VkFormat.Astc1212SrgbBlock, rgb   
            ]

        let channels =
            LookupTable.lookupTable [
                VkFormat.Undefined, -1
                VkFormat.R4g4UnormPack8, 2
                VkFormat.R4g4b4a4UnormPack16, 4
                VkFormat.B4g4r4a4UnormPack16, 4
                VkFormat.R5g6b5UnormPack16, 3
                VkFormat.B5g6r5UnormPack16, 3
                VkFormat.R5g5b5a1UnormPack16, 4
                VkFormat.B5g5r5a1UnormPack16, 4
                VkFormat.A1r5g5b5UnormPack16, 4
                VkFormat.R8Unorm, 1
                VkFormat.R8Snorm, 1
                VkFormat.R8Uscaled, 1
                VkFormat.R8Sscaled, 1
                VkFormat.R8Uint, 1
                VkFormat.R8Sint, 1
                VkFormat.R8Srgb, 1
                VkFormat.R8g8Unorm, 2
                VkFormat.R8g8Snorm, 2
                VkFormat.R8g8Uscaled, 2
                VkFormat.R8g8Sscaled, 2
                VkFormat.R8g8Uint, 2
                VkFormat.R8g8Sint, 2
                VkFormat.R8g8Srgb, 2
                VkFormat.R8g8b8Unorm, 3
                VkFormat.R8g8b8Snorm, 3
                VkFormat.R8g8b8Uscaled, 3
                VkFormat.R8g8b8Sscaled, 3
                VkFormat.R8g8b8Uint, 3
                VkFormat.R8g8b8Sint, 3
                VkFormat.R8g8b8Srgb, 3
                VkFormat.B8g8r8Unorm, 3
                VkFormat.B8g8r8Snorm, 3
                VkFormat.B8g8r8Uscaled, 3
                VkFormat.B8g8r8Sscaled, 3
                VkFormat.B8g8r8Uint, 3
                VkFormat.B8g8r8Sint, 3
                VkFormat.B8g8r8Srgb, 3
                VkFormat.R8g8b8a8Unorm, 4
                VkFormat.R8g8b8a8Snorm, 4
                VkFormat.R8g8b8a8Uscaled, 4
                VkFormat.R8g8b8a8Sscaled, 4
                VkFormat.R8g8b8a8Uint, 4
                VkFormat.R8g8b8a8Sint, 4
                VkFormat.R8g8b8a8Srgb, 4
                VkFormat.B8g8r8a8Unorm, 4
                VkFormat.B8g8r8a8Snorm, 4
                VkFormat.B8g8r8a8Uscaled, 4
                VkFormat.B8g8r8a8Sscaled, 4
                VkFormat.B8g8r8a8Uint, 4
                VkFormat.B8g8r8a8Sint, 4
                VkFormat.B8g8r8a8Srgb, 4
                VkFormat.A8b8g8r8UnormPack32, 4
                VkFormat.A8b8g8r8SnormPack32, 4
                VkFormat.A8b8g8r8UscaledPack32, 4
                VkFormat.A8b8g8r8SscaledPack32, 4
                VkFormat.A8b8g8r8UintPack32, 4
                VkFormat.A8b8g8r8SintPack32, 4
                VkFormat.A8b8g8r8SrgbPack32, 4
                VkFormat.A2r10g10b10UnormPack32, 4
                VkFormat.A2r10g10b10SnormPack32, 4
                VkFormat.A2r10g10b10UscaledPack32, 4
                VkFormat.A2r10g10b10SscaledPack32, 4
                VkFormat.A2r10g10b10UintPack32, 4
                VkFormat.A2r10g10b10SintPack32, 4
                VkFormat.A2b10g10r10UnormPack32, 4
                VkFormat.A2b10g10r10SnormPack32, 4
                VkFormat.A2b10g10r10UscaledPack32, 4
                VkFormat.A2b10g10r10SscaledPack32, 4
                VkFormat.A2b10g10r10UintPack32, 4
                VkFormat.A2b10g10r10SintPack32, 4
                VkFormat.R16Unorm, 1
                VkFormat.R16Snorm, 1
                VkFormat.R16Uscaled, 1
                VkFormat.R16Sscaled, 1
                VkFormat.R16Uint, 1
                VkFormat.R16Sint, 1
                VkFormat.R16Sfloat, 1
                VkFormat.R16g16Unorm, 2
                VkFormat.R16g16Snorm, 2
                VkFormat.R16g16Uscaled, 2
                VkFormat.R16g16Sscaled, 2
                VkFormat.R16g16Uint, 2
                VkFormat.R16g16Sint, 2
                VkFormat.R16g16Sfloat, 2
                VkFormat.R16g16b16Unorm, 3
                VkFormat.R16g16b16Snorm, 3
                VkFormat.R16g16b16Uscaled, 3
                VkFormat.R16g16b16Sscaled, 3
                VkFormat.R16g16b16Uint, 3
                VkFormat.R16g16b16Sint, 3
                VkFormat.R16g16b16Sfloat, 3
                VkFormat.R16g16b16a16Unorm, 4
                VkFormat.R16g16b16a16Snorm, 4
                VkFormat.R16g16b16a16Uscaled, 4
                VkFormat.R16g16b16a16Sscaled, 4
                VkFormat.R16g16b16a16Uint, 4
                VkFormat.R16g16b16a16Sint, 4
                VkFormat.R16g16b16a16Sfloat, 4
                VkFormat.R32Uint, 1
                VkFormat.R32Sint, 1
                VkFormat.R32Sfloat, 1
                VkFormat.R32g32Uint, 2
                VkFormat.R32g32Sint, 2
                VkFormat.R32g32Sfloat, 2
                VkFormat.R32g32b32Uint, 3
                VkFormat.R32g32b32Sint, 3
                VkFormat.R32g32b32Sfloat, 3
                VkFormat.R32g32b32a32Uint, 4
                VkFormat.R32g32b32a32Sint, 4
                VkFormat.R32g32b32a32Sfloat, 4
                VkFormat.R64Uint, 1
                VkFormat.R64Sint, 1
                VkFormat.R64Sfloat, 1
                VkFormat.R64g64Uint, 2
                VkFormat.R64g64Sint, 2
                VkFormat.R64g64Sfloat, 2
                VkFormat.R64g64b64Uint, 3
                VkFormat.R64g64b64Sint, 3
                VkFormat.R64g64b64Sfloat, 3
                VkFormat.R64g64b64a64Uint, 4
                VkFormat.R64g64b64a64Sint, 4
                VkFormat.R64g64b64a64Sfloat, 4
                VkFormat.B10g11r11UfloatPack32, 3
                VkFormat.E5b9g9r9UfloatPack32, 3
                VkFormat.D16Unorm, 1
                VkFormat.X8D24UnormPack32, 1
                VkFormat.D32Sfloat, 1
                VkFormat.S8Uint, 1
                VkFormat.D16UnormS8Uint, 2
                VkFormat.D24UnormS8Uint, 2
                VkFormat.D32SfloatS8Uint, 2
                VkFormat.Bc1RgbUnormBlock, -1
                VkFormat.Bc1RgbSrgbBlock, -1
                VkFormat.Bc1RgbaUnormBlock, -1
                VkFormat.Bc1RgbaSrgbBlock, -1
                VkFormat.Bc2UnormBlock, -1
                VkFormat.Bc2SrgbBlock, -1
                VkFormat.Bc3UnormBlock, -1
                VkFormat.Bc3SrgbBlock, -1
                VkFormat.Bc4UnormBlock, -1
                VkFormat.Bc4SnormBlock, -1
                VkFormat.Bc5UnormBlock, -1
                VkFormat.Bc5SnormBlock, -1
                VkFormat.Bc6hUfloatBlock, -1
                VkFormat.Bc6hSfloatBlock, -1
                VkFormat.Bc7UnormBlock, -1
                VkFormat.Bc7SrgbBlock, -1
                VkFormat.Etc2R8g8b8UnormBlock, -1
                VkFormat.Etc2R8g8b8SrgbBlock, -1
                VkFormat.Etc2R8g8b8a1UnormBlock, -1
                VkFormat.Etc2R8g8b8a1SrgbBlock, -1
                VkFormat.Etc2R8g8b8a8UnormBlock, -1
                VkFormat.Etc2R8g8b8a8SrgbBlock, -1
                VkFormat.EacR11UnormBlock, -1
                VkFormat.EacR11SnormBlock, -1
                VkFormat.EacR11g11UnormBlock, -1
                VkFormat.EacR11g11SnormBlock, -1
                VkFormat.Astc44UnormBlock, -1
                VkFormat.Astc44SrgbBlock, -1
                VkFormat.Astc54UnormBlock, -1
                VkFormat.Astc54SrgbBlock, -1
                VkFormat.Astc55UnormBlock, -1
                VkFormat.Astc55SrgbBlock, -1
                VkFormat.Astc65UnormBlock, -1
                VkFormat.Astc65SrgbBlock, -1
                VkFormat.Astc66UnormBlock, -1
                VkFormat.Astc66SrgbBlock, -1
                VkFormat.Astc85UnormBlock, -1
                VkFormat.Astc85SrgbBlock, -1
                VkFormat.Astc86UnormBlock, -1
                VkFormat.Astc86SrgbBlock, -1
                VkFormat.Astc88UnormBlock, -1
                VkFormat.Astc88SrgbBlock, -1
                VkFormat.Astc105UnormBlock, -1
                VkFormat.Astc105SrgbBlock, -1
                VkFormat.Astc106UnormBlock, -1
                VkFormat.Astc106SrgbBlock, -1
                VkFormat.Astc108UnormBlock, -1
                VkFormat.Astc108SrgbBlock, -1
                VkFormat.Astc1010UnormBlock, -1
                VkFormat.Astc1010SrgbBlock, -1
                VkFormat.Astc1210UnormBlock, -1
                VkFormat.Astc1210SrgbBlock, -1
                VkFormat.Astc1212UnormBlock, -1
                VkFormat.Astc1212SrgbBlock, -1      
            ]

        let sizeInBytes =

            LookupTable.lookupTable [
                VkFormat.Undefined, -1
                VkFormat.R4g4UnormPack8, 1
                VkFormat.R4g4b4a4UnormPack16, 2
                VkFormat.B4g4r4a4UnormPack16, 2
                VkFormat.R5g6b5UnormPack16, 2
                VkFormat.B5g6r5UnormPack16, 2
                VkFormat.R5g5b5a1UnormPack16, 2
                VkFormat.B5g5r5a1UnormPack16, 2
                VkFormat.A1r5g5b5UnormPack16, 2
                VkFormat.R8Unorm, 1
                VkFormat.R8Snorm, 1
                VkFormat.R8Uscaled, 1
                VkFormat.R8Sscaled, 1
                VkFormat.R8Uint, 1
                VkFormat.R8Sint, 1
                VkFormat.R8Srgb, 1
                VkFormat.R8g8Unorm, 2
                VkFormat.R8g8Snorm, 2
                VkFormat.R8g8Uscaled, 2
                VkFormat.R8g8Sscaled, 2
                VkFormat.R8g8Uint, 2
                VkFormat.R8g8Sint, 2
                VkFormat.R8g8Srgb, 2
                VkFormat.R8g8b8Unorm, 3
                VkFormat.R8g8b8Snorm, 3
                VkFormat.R8g8b8Uscaled, 3
                VkFormat.R8g8b8Sscaled, 3
                VkFormat.R8g8b8Uint, 3
                VkFormat.R8g8b8Sint, 3
                VkFormat.R8g8b8Srgb, 3
                VkFormat.B8g8r8Unorm, 3
                VkFormat.B8g8r8Snorm, 3
                VkFormat.B8g8r8Uscaled, 3
                VkFormat.B8g8r8Sscaled, 3
                VkFormat.B8g8r8Uint, 3
                VkFormat.B8g8r8Sint, 3
                VkFormat.B8g8r8Srgb, 3
                VkFormat.R8g8b8a8Unorm, 4
                VkFormat.R8g8b8a8Snorm, 4
                VkFormat.R8g8b8a8Uscaled, 4
                VkFormat.R8g8b8a8Sscaled, 4
                VkFormat.R8g8b8a8Uint, 4
                VkFormat.R8g8b8a8Sint, 4
                VkFormat.R8g8b8a8Srgb, 4
                VkFormat.B8g8r8a8Unorm, 4
                VkFormat.B8g8r8a8Snorm, 4
                VkFormat.B8g8r8a8Uscaled, 4
                VkFormat.B8g8r8a8Sscaled, 4
                VkFormat.B8g8r8a8Uint, 4
                VkFormat.B8g8r8a8Sint, 4
                VkFormat.B8g8r8a8Srgb, 4
                VkFormat.A8b8g8r8UnormPack32, 4
                VkFormat.A8b8g8r8SnormPack32, 4
                VkFormat.A8b8g8r8UscaledPack32, 4
                VkFormat.A8b8g8r8SscaledPack32, 4
                VkFormat.A8b8g8r8UintPack32, 4
                VkFormat.A8b8g8r8SintPack32, 4
                VkFormat.A8b8g8r8SrgbPack32, 4
                VkFormat.A2r10g10b10UnormPack32, 4
                VkFormat.A2r10g10b10SnormPack32, 4
                VkFormat.A2r10g10b10UscaledPack32, 4
                VkFormat.A2r10g10b10SscaledPack32, 4
                VkFormat.A2r10g10b10UintPack32, 4
                VkFormat.A2r10g10b10SintPack32, 4
                VkFormat.A2b10g10r10UnormPack32, 4
                VkFormat.A2b10g10r10SnormPack32, 4
                VkFormat.A2b10g10r10UscaledPack32, 4
                VkFormat.A2b10g10r10SscaledPack32, 4
                VkFormat.A2b10g10r10UintPack32, 4
                VkFormat.A2b10g10r10SintPack32, 4
                VkFormat.R16Unorm, 2
                VkFormat.R16Snorm, 2
                VkFormat.R16Uscaled, 2
                VkFormat.R16Sscaled, 2
                VkFormat.R16Uint, 2
                VkFormat.R16Sint, 2
                VkFormat.R16Sfloat, 2
                VkFormat.R16g16Unorm, 4
                VkFormat.R16g16Snorm, 4
                VkFormat.R16g16Uscaled, 4
                VkFormat.R16g16Sscaled, 4
                VkFormat.R16g16Uint, 4
                VkFormat.R16g16Sint, 4
                VkFormat.R16g16Sfloat, 4
                VkFormat.R16g16b16Unorm, 6
                VkFormat.R16g16b16Snorm, 6
                VkFormat.R16g16b16Uscaled, 6
                VkFormat.R16g16b16Sscaled, 6
                VkFormat.R16g16b16Uint, 6
                VkFormat.R16g16b16Sint, 6
                VkFormat.R16g16b16Sfloat, 6
                VkFormat.R16g16b16a16Unorm, 8
                VkFormat.R16g16b16a16Snorm, 8
                VkFormat.R16g16b16a16Uscaled, 8
                VkFormat.R16g16b16a16Sscaled, 8
                VkFormat.R16g16b16a16Uint, 8
                VkFormat.R16g16b16a16Sint, 8
                VkFormat.R16g16b16a16Sfloat, 8
                VkFormat.R32Uint, 4
                VkFormat.R32Sint, 4
                VkFormat.R32Sfloat, 4
                VkFormat.R32g32Uint, 8
                VkFormat.R32g32Sint, 8
                VkFormat.R32g32Sfloat, 8
                VkFormat.R32g32b32Uint, 12
                VkFormat.R32g32b32Sint, 12
                VkFormat.R32g32b32Sfloat, 12
                VkFormat.R32g32b32a32Uint, 16
                VkFormat.R32g32b32a32Sint, 16
                VkFormat.R32g32b32a32Sfloat, 16
                VkFormat.R64Uint, 8
                VkFormat.R64Sint, 8
                VkFormat.R64Sfloat, 8
                VkFormat.R64g64Uint, 16
                VkFormat.R64g64Sint, 16
                VkFormat.R64g64Sfloat, 16
                VkFormat.R64g64b64Uint, 24
                VkFormat.R64g64b64Sint, 24
                VkFormat.R64g64b64Sfloat, 24
                VkFormat.R64g64b64a64Uint, 32
                VkFormat.R64g64b64a64Sint, 32
                VkFormat.R64g64b64a64Sfloat, 32
                VkFormat.B10g11r11UfloatPack32, 4
                VkFormat.E5b9g9r9UfloatPack32, 4
                VkFormat.D16Unorm, 2
                VkFormat.X8D24UnormPack32, 4
                VkFormat.D32Sfloat, 4
                VkFormat.S8Uint, 1
                VkFormat.D16UnormS8Uint, 3
                VkFormat.D24UnormS8Uint, 4
                VkFormat.D32SfloatS8Uint, 5
                VkFormat.Bc1RgbUnormBlock, -1
                VkFormat.Bc1RgbSrgbBlock, -1
                VkFormat.Bc1RgbaUnormBlock, -1
                VkFormat.Bc1RgbaSrgbBlock, -1
                VkFormat.Bc2UnormBlock, -1
                VkFormat.Bc2SrgbBlock, -1
                VkFormat.Bc3UnormBlock, -1
                VkFormat.Bc3SrgbBlock, -1
                VkFormat.Bc4UnormBlock, -1
                VkFormat.Bc4SnormBlock, -1
                VkFormat.Bc5UnormBlock, -1
                VkFormat.Bc5SnormBlock, -1
                VkFormat.Bc6hUfloatBlock, -1
                VkFormat.Bc6hSfloatBlock, -1
                VkFormat.Bc7UnormBlock, -1
                VkFormat.Bc7SrgbBlock, -1
                VkFormat.Etc2R8g8b8UnormBlock, -1
                VkFormat.Etc2R8g8b8SrgbBlock, -1
                VkFormat.Etc2R8g8b8a1UnormBlock, -1
                VkFormat.Etc2R8g8b8a1SrgbBlock, -1
                VkFormat.Etc2R8g8b8a8UnormBlock, -1
                VkFormat.Etc2R8g8b8a8SrgbBlock, -1
                VkFormat.EacR11UnormBlock, -1
                VkFormat.EacR11SnormBlock, -1
                VkFormat.EacR11g11UnormBlock, -1
                VkFormat.EacR11g11SnormBlock, -1
                VkFormat.Astc44UnormBlock, -1
                VkFormat.Astc44SrgbBlock, -1
                VkFormat.Astc54UnormBlock, -1
                VkFormat.Astc54SrgbBlock, -1
                VkFormat.Astc55UnormBlock, -1
                VkFormat.Astc55SrgbBlock, -1
                VkFormat.Astc65UnormBlock, -1
                VkFormat.Astc65SrgbBlock, -1
                VkFormat.Astc66UnormBlock, -1
                VkFormat.Astc66SrgbBlock, -1
                VkFormat.Astc85UnormBlock, -1
                VkFormat.Astc85SrgbBlock, -1
                VkFormat.Astc86UnormBlock, -1
                VkFormat.Astc86SrgbBlock, -1
                VkFormat.Astc88UnormBlock, -1
                VkFormat.Astc88SrgbBlock, -1
                VkFormat.Astc105UnormBlock, -1
                VkFormat.Astc105SrgbBlock, -1
                VkFormat.Astc106UnormBlock, -1
                VkFormat.Astc106SrgbBlock, -1
                VkFormat.Astc108UnormBlock, -1
                VkFormat.Astc108SrgbBlock, -1
                VkFormat.Astc1010UnormBlock, -1
                VkFormat.Astc1010SrgbBlock, -1
                VkFormat.Astc1210UnormBlock, -1
                VkFormat.Astc1210SrgbBlock, -1
                VkFormat.Astc1212UnormBlock, -1
                VkFormat.Astc1212SrgbBlock, -1               
            ]

        let ofPixFormat (fmt : PixFormat) =
            TextureFormat.ofPixFormat fmt TextureParams.empty |> ofTextureFormat

    type VolumeInfo with
        member x.Transformed(t : ImageTrafo) =
            let sx = x.SX
            let sy = x.SY
            let sz = x.SZ
            let dx = x.DX
            let dy = x.DY
            let dz = x.DZ
            match t with
                | ImageTrafo.Rot0 -> x
                | ImageTrafo.Rot90 -> x.SubVolume(sx - 1L, 0L, 0L, sy, sx, sz, dy, -dx, dz)
                | ImageTrafo.Rot180 -> x.SubVolume(sx - 1L, sy - 1L, 0L, sx, sy, sz, -dx, -dy, dz)
                | ImageTrafo.Rot270 -> x.SubVolume(0L, sy - 1L, 0L, sy, sx, sz, -dy, dx, dz)
                | ImageTrafo.MirrorX -> x.SubVolume(sx - 1L, 0L, 0L, sx, sy, sz, -dx, dy, dz)
                | ImageTrafo.Transpose -> x.SubVolume(0L, 0L, 0L, sy, sx, sz, dy, dx, dz)
                | ImageTrafo.MirrorY -> x.SubVolume(0L, sy - 1L, 0L, sx, sy, sz, dx, -dy, dz)
                | ImageTrafo.Transverse -> x.SubVolume(sx - 1L, sy - 1L, 0L, sy, sx, sz, -dy, -dx, dz)
                | _ -> failf "invalid ImageTrafo"

    type Device with
        
        member x.GetSupportedFormat(tiling : VkImageTiling, fmt : PixFormat) =
            let retry f = x.GetSupportedFormat(tiling, PixFormat(fmt.Type, f))

            match fmt.Format with
                | Col.Format.BGR    -> retry Col.Format.RGB
                | Col.Format.BGRA   -> retry Col.Format.RGBA
                | Col.Format.BGRP   -> retry Col.Format.RGBP
                | _ -> 
                    let test = VkFormat.ofPixFormat fmt
                    let features = x.PhysicalDevice.GetFormatFeatures(tiling, test)

                    if features <> VkFormatFeatureFlags.None then
                        test
                    else
                        match fmt.Format with
                            | Col.Format.BW         -> retry Col.Format.Gray
                            | Col.Format.Alpha      -> retry Col.Format.Gray
                            | Col.Format.Gray       -> retry Col.Format.NormalUV
                            | Col.Format.NormalUV   -> retry Col.Format.RGB
                            | Col.Format.GrayAlpha  -> retry Col.Format.RGB
                            | Col.Format.RGB        -> retry Col.Format.RGBA
                            | _ ->
                                failf "bad format"


    type VkStructureType with
        static member SwapChainCreateInfoKHR = 1000001000 |> unbox<VkStructureType>
        static member PresentInfoKHR = 1000001001 |> unbox<VkStructureType>

    type VkImageLayout with
        static member PresentSrcKhr = unbox<VkImageLayout> 1000001002


    module VkComponentMapping =
        let Identity = VkComponentMapping(VkComponentSwizzle.R, VkComponentSwizzle.G, VkComponentSwizzle.B, VkComponentSwizzle.A)

        let ofColFormat =
            let c0 = VkComponentSwizzle.R
            let c1 = VkComponentSwizzle.G
            let c2 = VkComponentSwizzle.B
            let c3 = VkComponentSwizzle.A
            let zero = VkComponentSwizzle.Zero
            let one = VkComponentSwizzle.One
            LookupTable.lookupTable [
                Col.Format.Alpha, VkComponentMapping(zero, zero, zero, c0)
                Col.Format.BGR, VkComponentMapping(c2, c1, c0, one)
                Col.Format.BGRA, VkComponentMapping(c2, c1, c0, c3)
                Col.Format.BGRP, VkComponentMapping(c2, c1, c0, c3)
                Col.Format.BW, VkComponentMapping(c0, c0, c0, one)
                Col.Format.Gray, VkComponentMapping(c0, c0, c0, one)
                Col.Format.GrayAlpha, VkComponentMapping(c0, c0, c0, c1) 
                Col.Format.NormalUV, VkComponentMapping(c0, c1, zero, one) 
                Col.Format.RGB, VkComponentMapping(c0, c1, c2, one)
                Col.Format.RGBA, VkComponentMapping(c0, c1, c2, c3)
                Col.Format.RGBP, VkComponentMapping(c0, c1, c2, c3)
            ]

        let ofTextureFormat =
       
            let r = VkComponentSwizzle.R
            let g = VkComponentSwizzle.G
            let b = VkComponentSwizzle.B
            let a = VkComponentSwizzle.A
            let z = VkComponentSwizzle.Zero
            let i = VkComponentSwizzle.One

            let create a b c d = VkComponentMapping(a,b,c,d)

            LookupTable.lookupTable [
                TextureFormat.Bgr8, create r g b i
                TextureFormat.Bgra8, create r g b a
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


// ===========================================================================================
// Image Resource Type
// ===========================================================================================

[<Flags>]
type ImageAspect =
    | None = 0x00000000
    | Color = 0x00000001
    | Depth = 0x00000002
    | Stencil = 0x00000004
    | Metadata = 0x00000008
    | DepthStencil = 0x00000006

type Image =
    class 
        inherit Resource<VkImage>

        val mutable public Size : V3i
        val mutable public MipMapLevels : int
        val mutable public Count : int
        val mutable public Samples : int
        val mutable public Dimension : TextureDimension
        val mutable public Format : VkFormat
        val mutable public ComponentMapping : VkComponentMapping
        val mutable public Memory : DevicePtr
        val mutable public Layout : VkImageLayout

        interface ITexture with 
            member x.WantMipMaps = x.MipMapLevels > 1

        interface IBackendTexture with
            member x.Handle = x.Handle :> obj
            member x.Count = x.Count
            member x.Dimension = x.Dimension
            member x.Format = VkFormat.toTextureFormat x.Format
            member x.MipMapLevels = x.MipMapLevels
            member x.Samples = x.Samples
            member x.Size = x.Size

        interface IRenderbuffer with
            member x.Size = x.Size.XY
            member x.Samples = x.Samples
            member x.Format = VkFormat.toTextureFormat x.Format |> TextureFormat.toRenderbufferFormat
            member x.Handle = x.Handle :> obj

        member x.IsNull = x.Handle.IsNull

        member x.Item with get(aspect : ImageAspect) = ImageSubresourceRange(x, aspect, 0, x.MipMapLevels, 0, x.Count)
        member x.Item with get(aspect : ImageAspect, level : int) = ImageSubresourceLevels(x, aspect, level, 0, x.Count)
        member x.Item with get(aspect : ImageAspect, level : int, slice : int) = ImageSubresource(x, aspect, level, slice)
              
                
        member x.GetSlice(aspect : ImageAspect, minLevel : Option<int>, maxLevel : Option<int>, minSlice : Option<int>, maxSlice : Option<int>) =
            x.[aspect].GetSlice(minLevel, maxLevel, minSlice, maxSlice)

        member x.GetSlice(aspect : ImageAspect, minLevel : Option<int>, maxLevel : Option<int>, slice : int) =
            x.[aspect].GetSlice(minLevel, maxLevel, slice)
      
        member x.GetSlice(aspect : ImageAspect, level : int, minSlice : Option<int>, maxSlice : Option<int>) =
            x.[aspect].GetSlice(level, minSlice, maxSlice)

            
        override x.ToString() =
            sprintf "0x%08X" x.Handle.Handle

        new(dev, handle, s, levels, count, samples, dim, fmt, mapping, mem, layout) = 
            {
                inherit Resource<_>(dev, handle);
                Size = s
                MipMapLevels = levels
                Count = count
                Samples = samples
                Dimension = dim
                ComponentMapping = mapping
                Format = fmt
                Memory = mem
                Layout = layout
            }
    end

and ImageSubresourceRange(image : Image, aspect : ImageAspect, baseLevel : int, levelCount : int, baseSlice : int, sliceCount : int) =
    let maxSize = 
        if baseLevel = 0 then
            image.Size
        else
            let imageSize = image.Size
            let divisor = 1 <<< baseLevel
            V3i(
                max 1 (imageSize.X / divisor),
                max 1 (imageSize.Y / divisor),
                max 1 (imageSize.Z / divisor)
            )
  
    member internal x.Flags = unbox<VkImageAspectFlags> aspect
    member x.Aspect = aspect
    member x.Image = image
    member x.VkImageSubresourceRange = VkImageSubresourceRange(x.Flags, uint32 baseLevel, uint32 levelCount, uint32 baseSlice, uint32 sliceCount)

    member x.BaseLevel = baseLevel
    member x.LevelCount = levelCount
    member x.BaseSlice = baseSlice
    member x.SliceCount = sliceCount

    member x.MaxSize = maxSize

    member x.GetSlice(minLevel : Option<int>, maxLevel : Option<int>, minSlice : Option<int>, maxSlice : Option<int>) =
        let levelTop = levelCount - 1
        let sliceTop = sliceCount - 1
        let minLevel = defaultArg minLevel 0            |> clamp 0 levelTop
        let maxLevel = defaultArg maxLevel levelTop     |> clamp minLevel levelTop
        let minSlice = defaultArg minSlice 0            |> clamp 0 sliceTop
        let maxSlice = defaultArg maxSlice sliceTop     |> clamp minSlice sliceTop
        ImageSubresourceRange(image, aspect, baseLevel + minLevel, 1 + maxLevel - minLevel, baseSlice + minSlice, 1 + maxSlice - minSlice)

    member x.GetSlice(level : int, minSlice : Option<int>, maxSlice : Option<int>) =
        let sliceTop = sliceCount - 1
        let minSlice = defaultArg minSlice 0            |> clamp 0 sliceTop
        let maxSlice = defaultArg maxSlice sliceTop     |> clamp minSlice sliceTop
        ImageSubresourceLayers(image, aspect, baseLevel + level, baseSlice + minSlice, 1 + maxSlice - minSlice)

    member x.GetSlice(minLevel : Option<int>, maxLevel : Option<int>, slice : int) =
        let levelTop = levelCount - 1
        let minLevel = defaultArg minLevel 0            |> clamp 0 levelTop
        let maxLevel = defaultArg maxLevel levelTop     |> clamp minLevel levelTop
        ImageSubresourceLevels(image, aspect, baseLevel + minLevel, 1 + maxLevel - minLevel, baseSlice + slice)

    member x.Item with get(level : int, slice : int) = ImageSubresource(image, aspect, baseLevel + level, baseSlice + slice)

    override x.ToString() =
        let minLevel = baseLevel
        let maxLevel = minLevel + levelCount - 1
        let minSlice = baseSlice
        let maxSlice = minSlice + sliceCount - 1
        match (minLevel = maxLevel), (minSlice = maxSlice) with
            | true,     true    -> sprintf "%A[%A, %d, %d]" image aspect minLevel minSlice
            | true,     false   -> sprintf "%A[%A, %d, %d..%d]" image aspect minLevel minSlice maxSlice
            | false,    true    -> sprintf "%A[%A, %d..%d, %d]" image aspect minLevel maxLevel minSlice
            | false,    false   -> sprintf "%A[%A, %d..%d, %d..%d]" image aspect minLevel maxLevel minSlice maxSlice


and ImageSubresourceLayers(image : Image, aspect : ImageAspect, level : int, baseSlice : int, sliceCount : int) =
    inherit ImageSubresourceRange(image, aspect, level, 1, baseSlice, sliceCount)
    member x.Level = level
    member x.Size = x.MaxSize
    member x.Item with get (slice : int) = x.[0, slice]
    member x.GetSlice (minSlice : Option<int>, maxSlice : Option<int>) = x.GetSlice(0, minSlice, maxSlice)
    member x.VkImageSubresourceLayers = VkImageSubresourceLayers(x.Flags, uint32 level, uint32 baseSlice, uint32 sliceCount)

and ImageSubresourceLevels(image : Image, aspect : ImageAspect, baseLevel : int, levelCount : int, slice : int) =
    inherit ImageSubresourceRange(image, aspect, baseLevel, levelCount, slice, 1)
    member x.Slice = slice
    member x.Item with get (i : int) = x.[i, 0]
    member x.GetSlice (minLevel : Option<int>, maxLevel : Option<int>) = x.GetSlice(minLevel, maxLevel, 0)

and ImageSubresource(image : Image, aspect : ImageAspect, level : int, slice : int) =
    inherit ImageSubresourceLayers(image, aspect, level, slice, 1)
    member x.Slice = slice
    member x.VkImageSubresource = VkImageSubresource(x.Flags, uint32 level, uint32 slice)

// ===========================================================================================
// DevicePixImageMipMap
// ===========================================================================================

[<AbstractClass>]
type DeviceMemoryImage(device : Device, image : Image, aspect : ImageAspect) =
    inherit ImageSubresourceLevels(image, aspect, 0, image.MipMapLevels, 0)
    member x.Device = device
    member x.Image = image

    member x.LevelCount = image.MipMapLevels
    member x.Size = image.Size.XY

    abstract member GetLevelSize : int -> V3i
    abstract member PixFormat : PixFormat

    abstract member Upload : level : int * src : PixImage -> unit
    abstract member Download : level : int * dst : PixImage -> unit

    abstract member Upload : level : int * format : PixFormat * srcTrafo : ImageTrafo * src : nativeint * srcRowSize : int64 -> unit
    abstract member Download : level : int * format : PixFormat * dstTrafo : ImageTrafo * dst : nativeint * dstRowSize : int64 -> unit

type DeviceMemoryImage<'a when 'a : unmanaged> internal(device : Device, image : Image, aspect : ImageAspect) =
    inherit DeviceMemoryImage(device, image, aspect)

    static let defaultValue =
        match typeof<'a> with
            | TypeInfo.Patterns.Byte    -> 255uy |> unbox<'a>
            | TypeInfo.Patterns.SByte   -> 127y |> unbox<'a>
            | TypeInfo.Patterns.UInt16  -> UInt16.MaxValue |> unbox<'a>
            | TypeInfo.Patterns.Int16   -> Int16.MaxValue |> unbox<'a>
            | TypeInfo.Patterns.UInt32  -> UInt32.MaxValue |> unbox<'a>
            | TypeInfo.Patterns.Int32   -> Int32.MaxValue |> unbox<'a>
            | TypeInfo.Patterns.UInt64  -> UInt64.MaxValue |> unbox<'a>
            | TypeInfo.Patterns.Int64   -> Int64.MaxValue |> unbox<'a>
            | TypeInfo.Patterns.Float32 -> 1.0f |> unbox<'a>
            | TypeInfo.Patterns.Float64 -> 1.0 |> unbox<'a>
            | _ -> failf "unsupported channel-type: %A" typeof<'a>

    let aspect = VkFormat.toAspect image.Format
    let channels = VkFormat.channels image.Format
    let channelSize = int64 sizeof<'a>

    let tensor4Infos = 
        Array.init image.MipMapLevels (fun level ->
            let mutable subresource = VkImageSubresource(aspect, uint32 level, 0u)
            let mutable layout = VkSubresourceLayout()
            VkRaw.vkGetImageSubresourceLayout(device.Handle, image.Handle, &&subresource, &&layout)

            let divisor = 1 <<< level
            let size = V3i(image.Size.X / divisor |> max 1, image.Size.Y / divisor |> max 1, image.Size.Z / divisor |> max 1)

            Tensor4Info(
                int64 layout.offset / channelSize,
                V4l(int64 size.X, int64 size.Y, int64 channels, int64 size.Z),
                V4l(
                    int64 channels,
                    int64 layout.rowPitch / channelSize,
                    1L,
                    int64 layout.depthPitch / channelSize
                )
            )
        )

    let pixFormat = PixFormat(typeof<'a>, VkFormat.toColFormat image.Format)

    let copyVolume (fill : bool) (src : NativeVolume<'a>) (dst : NativeVolume<'a>) =
        if src.SZ = dst.SZ then
            NativeVolume.copy src dst
        elif src.SZ < dst.SZ then
            NativeVolume.copy src dst.[*, *, 0L .. src.SZ - 1L]
            if fill then NativeVolume.set defaultValue dst.[*, *, src.SZ .. dst.SZ - 1L]
        else
            NativeVolume.copy src.[*, *, 0L .. dst.SZ - 1L] dst

    let copyTensor (fill : bool) (src : NativeTensor4<'a>) (dst : NativeTensor4<'a>) =
        if src.SZ = dst.SZ then
            NativeTensor4.copy src dst
        elif src.SZ < dst.SZ then
            NativeTensor4.copy src dst.[*, *, 0L .. src.SZ - 1L, *]
            if fill then NativeTensor4.set defaultValue dst.[*, *, src.SZ .. dst.SZ - 1L, *]
        else
            NativeTensor4.copy src.[*, *, 0L .. dst.SZ - 1L, *] dst

    override x.PixFormat = pixFormat

    override x.Upload(level : int, src : PixImage) =
        let src = unbox<PixImage<'a>>(src).Volume.Transformed(ImageTrafo.MirrorY)
        NativeVolume.using src (fun src ->
            x.MapVolume(level, fun dst -> 
                copyVolume true src dst
            )
        )

    override x.Download(level : int, dst : PixImage) =
        let dst = unbox<PixImage<'a>>(dst).Volume.Transformed(ImageTrafo.MirrorY)
        NativeVolume.using dst (fun dst ->
            x.MapVolume(level, fun src ->
                copyVolume false src dst
            )
        )

    override x.Upload(level : int, format : PixFormat, srcTrafo : ImageTrafo, src : nativeint, srcRowSize : int64) =
        if format.Type <> typeof<'a> then
            failf "cannot upload input-data of type: %A" format.Type

        let dstInfo = tensor4Infos.[level].SubXYZVolume(0L)
        let srcSize = srcTrafo |> ImageTrafo.inverseTransformSize image.Size.XY
        let srcChannels = format.ChannelCount
        
        let untransformedSrcInfo =
            VolumeInfo(
                0L,
                V3l(int64 srcSize.X, int64 srcSize.Y, int64 srcChannels),
                V3l(int64 srcChannels, srcRowSize / channelSize, 1L)
            )

        let srcInfo = untransformedSrcInfo.Transformed(srcTrafo)
        let src = NativeVolume<'a>(NativePtr.ofNativeInt src, srcInfo)

        x.MapVolume(level, fun dst -> copyVolume true src dst)

    override x.Download(level : int, format : PixFormat, dstTrafo : ImageTrafo, dst : nativeint, dstRowSize : int64) =
        if format.Type <> typeof<'a> then
            failf "cannot upload input-data of type: %A" format.Type
            
        let dstInfo = tensor4Infos.[level].SubXYZVolume(0L)
        let dstSize = dstTrafo |> ImageTrafo.transformSize image.Size.XY
        let dstChannels = format.ChannelCount

        let untransformedDstInfo =
            VolumeInfo(
                0L,
                V3l(int64 dstSize.X, int64 dstSize.Y, int64 dstChannels),
                V3l(int64 dstChannels, dstRowSize / channelSize, 1L)
            )

        let dstInfo = untransformedDstInfo.Transformed(ImageTrafo.inverse dstTrafo)
        let dst = NativeVolume<'a>(NativePtr.ofNativeInt dst, dstInfo)
        x.MapVolume(level, fun src -> copyVolume true src dst)

    override x.GetLevelSize (level : int) =
        tensor4Infos.[level].Size.XYW |> V3i

    member x.MapVolume<'x>(level : int, f : NativeVolume<'a> -> 'x) : 'x =
        let info = tensor4Infos.[level].SubXYZVolume(0L)
        image.Memory.Mapped(fun ptr ->
            let volume = NativeVolume<'a>(NativePtr.ofNativeInt ptr, info)
            f volume
        )
    member x.MapTensor<'x>(level : int, f : NativeTensor4<'a> -> 'x) : 'x =
        let info = tensor4Infos.[level]
        image.Memory.Mapped(fun ptr ->
            let volume = NativeTensor4<'a>(NativePtr.ofNativeInt ptr, info)
            f volume
        )

    member x.Image = image
    member x.Channels = channels
    member x.Format = image.Format
    member x.Aspect = aspect

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DeviceMemoryImage =

    let create<'a when 'a : unmanaged> (size : V2i) (levels : int) (fmt : Col.Format) (device : Device) =
        let pixFormat = PixFormat(typeof<'a>, fmt)
        let format = device.GetSupportedFormat(VkImageTiling.Optimal, pixFormat)
        let extent = VkExtent3D(size.X, size.Y, 1)
        let mutable info =
            VkImageCreateInfo(
                VkStructureType.ImageCreateInfo, 0n,
                VkImageCreateFlags.None,
                VkImageType.D2d,
                format,
                extent, 
                uint32 levels, 1u, VkSampleCountFlags.D1Bit,
                VkImageTiling.Linear,
                VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit,
                VkSharingMode.Exclusive,
                0u, NativePtr.zero,
                VkImageLayout.Preinitialized
            )

        let mutable handle = VkImage.Null
        VkRaw.vkCreateImage(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "could not create image for DeviceVolume"

        let mutable requirements = VkMemoryRequirements()
        VkRaw.vkGetImageMemoryRequirements(device.Handle, handle, &&requirements)
        let hostComp = device.HostMemory.Mask &&& requirements.memoryTypeBits <> 0u
        if not hostComp then 
            VkRaw.vkDestroyImage(device.Handle, handle, NativePtr.zero)
            failf "could not allocate DeviceVolume since HostMemory is incompatible"

        let memalign = int64 requirements.alignment |> Alignment.next device.BufferImageGranularity
        let memsize = int64 requirements.size |> Alignment.next device.BufferImageGranularity

        let memory = device.HostMemory.Alloc(memalign, memsize)
        VkRaw.vkBindImageMemory(device.Handle, handle, memory.Memory.Handle, uint64 memory.Offset)
            |> check "could not bind image memory for DeviceVolume"

        let image = Image(device, handle, V3i(size.X, size.Y, 1), levels, 1, 1, TextureDimension.Texture2D, format, VkComponentMapping.Identity, memory, VkImageLayout.Preinitialized)
        DeviceMemoryImage<'a>(device, image, ImageAspect.Color)

    let delete (mipMap : DeviceMemoryImage) (device : Device) =
        let image = mipMap.Image
        if image.Handle.IsValid then
            image.Memory.Dispose()
            VkRaw.vkDestroyImage(device.Handle, image.Handle, NativePtr.zero)
            image.Handle <- VkImage.Null

    let inline private createUpcast<'a when 'a : unmanaged> (size : V2i) (levels : int) (fmt : Col.Format) (device : Device) =
        create<'a> size levels fmt device :> DeviceMemoryImage

    let private ctors =
        LookupTable.lookupTable [
            typeof<uint8>, createUpcast<uint8>
            typeof<int8>, createUpcast<int8>
            typeof<uint16>, createUpcast<uint16>
            typeof<int16>, createUpcast<int16>
            typeof<uint32>, createUpcast<uint32>
            typeof<int32>, createUpcast<int32>
            typeof<uint64>, createUpcast<uint64>
            typeof<int64>, createUpcast<int64>
            typeof<float16>, createUpcast<float16>
            typeof<float32>, createUpcast<float32>
            typeof<float>, createUpcast<float>
        ]

    let createUntyped (size : V2i) (levels : int) (pixFormat : PixFormat) (device : Device) =
        ctors pixFormat.Type size levels pixFormat.Format device

    let ofPixImageMipMap (data : PixImageMipMap) (levels : int) (device : Device) =
        let size = data.[0].Size
        let pixFormat = data.PixFormat
        let res = device |> createUntyped size levels pixFormat

        for l in 0 .. levels - 1 do
            res.Upload(l, data.[l].Transformed(ImageTrafo.MirrorY))

        res


[<AutoOpen>]
module ``Devil Loader`` =
    open DevILSharp
    
    let private devilLock = typeof<PixImage>.GetField("s_devilLock", System.Reflection.BindingFlags.NonPublic ||| System.Reflection.BindingFlags.Static).GetValue(null)
    
    let private checkf (fmt : Printf.StringFormat<'a, bool -> unit>) =
        Printf.kprintf (fun str ->
            fun (success : bool) ->
                if not success then failwith ("[Devil] " + str)
        ) fmt
 
    module private PixFormat =
        let private types =
            LookupTable.lookupTable [
                ChannelType.Byte, typeof<int8>
                //ChannelType.Double, PixelType.Double
                ChannelType.Float, typeof<float32>
                ChannelType.Half, typeof<float16>
                ChannelType.Int, typeof<int>
                ChannelType.Short, typeof<int16>
                ChannelType.UnsignedByte, typeof<uint8>
                ChannelType.UnsignedInt, typeof<uint32>
                ChannelType.UnsignedShort, typeof<uint16>
            ]

        let private colFormat =
            LookupTable.lookupTable [
                ChannelFormat.RGB, Col.Format.RGB
                ChannelFormat.BGR, Col.Format.BGR
                ChannelFormat.RGBA, Col.Format.RGBA
                ChannelFormat.BGRA, Col.Format.BGRA
                ChannelFormat.Luminance, Col.Format.Gray
                ChannelFormat.Alpha, Col.Format.Alpha
                ChannelFormat.LuminanceAlpha, Col.Format.GrayAlpha
            ]

        let ofDevil (fmt : ChannelFormat) (t : ChannelType) =
            PixFormat(types t, colFormat fmt)

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module DeviceMemoryImage =   
        let ofFile (file : string) (device : Device) =
            lock devilLock (fun () ->
                PixImage.InitDevil()

                let img = IL.GenImage()
                try
                    IL.BindImage(img)
                    IL.LoadImage file
                        |> checkf "could not load image %A" file

                    let width       = IL.GetInteger(IntName.ImageWidth)
                    let height      = IL.GetInteger(IntName.ImageHeight)
                    let channelType = IL.GetDataType()
                    let format      = IL.GetFormat()
                    let data        = IL.GetData()
                    let pixFormat   = PixFormat.ofDevil format channelType

                    let bytesPerPixel = IL.GetInteger(IntName.ImageBytesPerPixel)
                    let rowSize = int64 bytesPerPixel * int64 width

                    let target = device |> DeviceMemoryImage.createUntyped (V2i(width, height)) 1 pixFormat
                    target.Upload(0, pixFormat, ImageTrafo.Rot0, data, rowSize)
                    
                    target
                finally
                    IL.BindImage(0)
                    IL.DeleteImage(img)

            )

[<AbstractClass; Sealed; Extension>]
type DeviceMemoryImageExtensions private() =

    [<Extension>]
    static member inline CreateDeviceMemoryImage(this : Device, data : PixImageMipMap, levels : int) =
        this |> DeviceMemoryImage.ofPixImageMipMap data levels

    [<Extension>]
    static member inline CreateDeviceMemoryImage(this : Device, data : PixImageMipMap) =
        this |> DeviceMemoryImage.ofPixImageMipMap data data.LevelCount

    [<Extension>]
    static member inline CreateDeviceMemoryImage(this : Device, file : string) =
        this |> DeviceMemoryImage.ofFile file

    [<Extension>]
    static member inline CreateDeviceMemoryImage(this : Device, size : V2i, levels : int, fmt : PixFormat) =
        this |> DeviceMemoryImage.createUntyped size levels fmt

    [<Extension>]
    static member inline CreateDeviceMemoryImage<'a when 'a : unmanaged>(this : Device, size : V2i, levels : int, fmt : Col.Format) =
        this |> DeviceMemoryImage.create<'a> size levels fmt

    [<Extension>]
    static member inline Delete(this : Device, mipMap : DeviceMemoryImage) =
        this |> DeviceMemoryImage.delete mipMap



// ===========================================================================================
// Image Command Extensions
// ===========================================================================================
[<AutoOpen>]
module ``Image Command Extensions`` =


    let private srcMasks =
        LookupTable.lookupTable [
            VkImageLayout.Undefined,                        VkAccessFlags.None
            VkImageLayout.General,                          VkAccessFlags.Write ||| VkAccessFlags.Read
            VkImageLayout.ColorAttachmentOptimal,           VkAccessFlags.ColorAttachmentWriteBit
            VkImageLayout.DepthStencilAttachmentOptimal,    VkAccessFlags.DepthStencilAttachmentWriteBit
            VkImageLayout.DepthStencilReadOnlyOptimal,      VkAccessFlags.DepthStencilAttachmentReadBit
            VkImageLayout.ShaderReadOnlyOptimal,            VkAccessFlags.ShaderReadBit
            VkImageLayout.TransferSrcOptimal,               VkAccessFlags.TransferReadBit
            VkImageLayout.TransferDstOptimal,               VkAccessFlags.TransferWriteBit
            VkImageLayout.Preinitialized,                   VkAccessFlags.HostWriteBit ||| VkAccessFlags.TransferWriteBit
        ]


    let private dstMasks =
        LookupTable.lookupTable [
            VkImageLayout.Undefined,                        VkAccessFlags.None
            VkImageLayout.General,                          VkAccessFlags.Write ||| VkAccessFlags.Read
            VkImageLayout.ColorAttachmentOptimal,           VkAccessFlags.ColorAttachmentWriteBit
            VkImageLayout.DepthStencilAttachmentOptimal,    VkAccessFlags.DepthStencilAttachmentWriteBit
            VkImageLayout.DepthStencilReadOnlyOptimal,      VkAccessFlags.DepthStencilAttachmentReadBit
            VkImageLayout.ShaderReadOnlyOptimal,            VkAccessFlags.ShaderReadBit
            VkImageLayout.TransferSrcOptimal,               VkAccessFlags.TransferReadBit
            VkImageLayout.TransferDstOptimal,               VkAccessFlags.TransferWriteBit
            VkImageLayout.Preinitialized,                   VkAccessFlags.HostWriteBit ||| VkAccessFlags.TransferWriteBit
        ]


    type Command with

        static member Copy(src : ImageSubresourceLayers, srcOffset : V3i, dst : ImageSubresourceLayers, dstOffset : V3i, size : V3i) =
            if src.SliceCount <> dst.SliceCount then
                failf "cannot copy image: { srcSlices = %A, dstSlices = %A }" src.SliceCount dst.SliceCount

            command {
                let srcLayout = src.Image.Layout
                let dstLayout = dst.Image.Layout
                do! Command.Custom (fun cmd ->
                    let mutable copy =
                        VkImageCopy(
                            src.VkImageSubresourceLayers,
                            VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                            dst.VkImageSubresourceLayers,
                            VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )

                    cmd.AppendCommand()
                    VkRaw.vkCmdCopyImage(cmd.Handle, src.Image.Handle, src.Image.Layout, dst.Image.Handle, dst.Image.Layout, 1u, &&copy)
                )

            }

        static member Copy(src : ImageSubresourceLayers, dst : ImageSubresourceLayers) =
            let srcSize = src.Size
            let dstSize = dst.Size

            if srcSize <> dstSize then
                failf "cannot copy image: { srcSize = %A; dstSize = %A }" srcSize dstSize

            Command.Copy(src, V3i.Zero, dst, V3i.Zero, srcSize)

        static member Copy(src : ImageSubresourceRange, dst : ImageSubresourceRange) =
            if src.LevelCount <> dst.LevelCount then
                failf "cannot copy image: { srcLevels = %A; dstLevels = %A }" src.LevelCount dst.LevelCount

            if src.SliceCount <> dst.SliceCount then
                failf "cannot copy image: { srcSlices = %A; dstSlices = %A }" src.SliceCount dst.SliceCount

            if src.MaxSize <> dst.MaxSize then
                failf "cannot copy image: { srcSize = %A; dstSize = %A }" src.LevelCount dst.LevelCount

            command {
                for i in 0 .. src.LevelCount-1 do
                    do! Command.Copy(src.[i, *], dst.[i, *])
            }


        static member ResolveMultisamples(src : ImageSubresourceLayers, srcOffset : V3i, dst : ImageSubresourceLayers, dstOffset : V3i, size : V3i) =
            if src.SliceCount <> dst.SliceCount then
                failf "cannot resolve image: { srcSlices = %A; dstSlices = %A }" src.SliceCount dst.SliceCount
                
            { new Command() with
                member x.Enqueue (cmd : CommandBuffer) =
                    let mutable resolve =
                        VkImageResolve(
                            src.VkImageSubresourceLayers,
                            VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                            dst.VkImageSubresourceLayers,
                            VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )

                    cmd.AppendCommand()
                    VkRaw.vkCmdResolveImage(cmd.Handle, src.Image.Handle, src.Image.Layout, dst.Image.Handle, dst.Image.Layout, 1u, &&resolve)
                    Disposable.Empty
            }

        static member ResolveMultisamples(src : ImageSubresourceLayers, dst : ImageSubresourceLayers) =
            if src.Size <> dst.Size then
                failf "cannot copy image: { srcSize = %A; dstSize = %A }" src.LevelCount dst.LevelCount

            Command.ResolveMultisamples(src, V3i.Zero, dst, V3i.Zero, src.Size)


        static member Blit(src : ImageSubresourceLayers, srcRange : Box3i, dst : ImageSubresourceLayers, dstRange : Box3i, filter : VkFilter) =
            command {
                let srcLayout = src.Image.Layout
                let dstLayout = dst.Image.Layout

                do! Command.Custom (fun cmd ->
                    let mutable srcOffsets = VkOffset3D_2()
                    srcOffsets.[0] <- VkOffset3D(srcRange.Min.X, srcRange.Min.Y, srcRange.Min.Z)
                    srcOffsets.[1] <- VkOffset3D(1 + srcRange.Max.X, 1 + srcRange.Max.Y, 1 + srcRange.Max.Z)

                    let mutable dstOffsets = VkOffset3D_2()
                    dstOffsets.[0] <- VkOffset3D(dstRange.Min.X, dstRange.Min.Y, dstRange.Min.Z)
                    dstOffsets.[1] <- VkOffset3D(1 + dstRange.Max.X, 1 + dstRange.Max.Y, 1 + dstRange.Max.Z)

                    let mutable blit =
                        VkImageBlit(
                            src.VkImageSubresourceLayers,
                            srcOffsets,
                            dst.VkImageSubresourceLayers,
                            dstOffsets
                        )
                    
                    cmd.AppendCommand()
                    VkRaw.vkCmdBlitImage(cmd.Handle, src.Image.Handle, src.Image.Layout, dst.Image.Handle, dst.Image.Layout, 1u, &&blit, filter)
                )

            }


        static member Blit(src : ImageSubresourceLayers, dst : ImageSubresourceLayers, dstRange : Box3i, filter : VkFilter) =
            Command.Blit(src, Box3i(V3i.Zero, src.Size - V3i.III), dst, dstRange, filter)

        static member Blit(src : ImageSubresourceLayers, srcRange : Box3i, dst : ImageSubresourceLayers, filter : VkFilter) =
            Command.Blit(src, srcRange, dst, Box3i(V3i.Zero, dst.Size - V3i.III), filter)

        static member Blit(src : ImageSubresourceLayers, dst : ImageSubresourceLayers, filter : VkFilter) =
            Command.Blit(src, Box3i(V3i.Zero, src.Size - V3i.III), dst, Box3i(V3i.Zero, dst.Size - V3i.III), filter)


        static member ClearColor(img : ImageSubresourceRange, color : C4f) =
            if img.Image.IsNull then
                Command.Nop
            else
                if img.Aspect <> ImageAspect.Color then
                    failf "cannot clear image with aspect %A using color" img.Aspect

                { new Command() with
                    member x.Enqueue cmd =
                        let originalLayout = img.Image.Layout
            
                        cmd.Enqueue (Command.TransformLayout(img.Image, VkImageLayout.TransferDstOptimal))
                    
                        let mutable clearValue = VkClearColorValue(float32 = color.ToV4f())
                        let mutable range = img.VkImageSubresourceRange
                        cmd.AppendCommand()
                        VkRaw.vkCmdClearColorImage(cmd.Handle, img.Image.Handle, VkImageLayout.TransferDstOptimal, &&clearValue, 1u, &&range)

                        cmd.Enqueue (Command.TransformLayout(img.Image, originalLayout))
                        Disposable.Empty
                }

        static member ClearDepthStencil(img : ImageSubresourceRange, depth : float, stencil : uint32) =
            if img.Image.IsNull then
                Command.Nop
            else
                if img.Aspect = ImageAspect.Color || img.Aspect = ImageAspect.Metadata then
                    failf "cannot clear image with aspect %A using depth/stencil" img.Aspect

                { new Command() with
                    member x.Enqueue cmd =
                        let originalLayout = img.Image.Layout
                        cmd.Enqueue (Command.TransformLayout(img.Image, VkImageLayout.TransferDstOptimal))
                  
                        let mutable clearValue = VkClearDepthStencilValue(float32 depth, stencil)
                        let mutable range = img.VkImageSubresourceRange
                        cmd.AppendCommand()
                        VkRaw.vkCmdClearDepthStencilImage(cmd.Handle, img.Image.Handle, VkImageLayout.TransferDstOptimal, &&clearValue, 1u, &&range)

                        cmd.Enqueue (Command.TransformLayout(img.Image, originalLayout))
                        Disposable.Empty
                }


        static member GenerateMipMaps (img : ImageSubresourceRange) =
            if img.Image.IsNull then
                Command.Nop
            else
                command {
                    let oldLayout = img.Image.Layout

                    for l in 1 .. img.LevelCount - 1 do
                        do! Command.Sync(img.[l - 1, *])
                        do! Command.Blit(img.[l - 1, *], img.[l, *], VkFilter.Linear)

                    do! Command.TransformLayout(img.Image, oldLayout)
                }

        static member TransformLayout(img : Image, target : VkImageLayout) =
            if img.IsNull || target = VkImageLayout.Undefined || target = VkImageLayout.Preinitialized then
                Command.Nop
            else
                { new Command() with
                    member x.Enqueue (cmd : CommandBuffer) =
                        if img.Layout = target then
                            Disposable.Empty
                        else
                            let source = img.Layout
                            img.Layout <- target

                            let src =
                                if source = VkImageLayout.ColorAttachmentOptimal then VkAccessFlags.ColorAttachmentWriteBit
                                elif source = VkImageLayout.DepthStencilAttachmentOptimal then VkAccessFlags.DepthStencilAttachmentWriteBit
                                elif source = VkImageLayout.TransferDstOptimal then VkAccessFlags.TransferWriteBit
                                elif source = VkImageLayout.PresentSrcKhr then VkAccessFlags.MemoryReadBit
                                elif source = VkImageLayout.Preinitialized then VkAccessFlags.HostWriteBit
                                elif source = VkImageLayout.TransferSrcOptimal then VkAccessFlags.TransferReadBit
                                else VkAccessFlags.None

                            let dst =
                                if target = VkImageLayout.TransferSrcOptimal then VkAccessFlags.TransferReadBit
                                elif target = VkImageLayout.TransferDstOptimal then VkAccessFlags.TransferWriteBit
                                elif target = VkImageLayout.ColorAttachmentOptimal then VkAccessFlags.ColorAttachmentWriteBit
                                elif target = VkImageLayout.DepthStencilAttachmentOptimal then VkAccessFlags.DepthStencilAttachmentWriteBit
                                elif target = VkImageLayout.ShaderReadOnlyOptimal then VkAccessFlags.ShaderReadBit ||| VkAccessFlags.InputAttachmentReadBit
                                elif target = VkImageLayout.PresentSrcKhr then VkAccessFlags.MemoryReadBit
                                else VkAccessFlags.None

                            let mutable barrier =
                                VkImageMemoryBarrier(
                                    VkStructureType.ImageMemoryBarrier, 0n, 
                                    src,
                                    dst,
                                    source,
                                    target,
                                    VK_QUEUE_FAMILY_IGNORED,
                                    VK_QUEUE_FAMILY_IGNORED,
                                    img.Handle,
                                    VkImageSubresourceRange(
                                        VkImageAspectFlags.ColorBit ||| VkImageAspectFlags.DepthBit ||| VkImageAspectFlags.StencilBit, 
                                        0u, uint32 img.MipMapLevels, 
                                        0u, uint32 img.Count
                                    )
                                )

                            cmd.AppendCommand()
                            VkRaw.vkCmdPipelineBarrier(
                                cmd.Handle,
                                VkPipelineStageFlags.TopOfPipeBit,
                                VkPipelineStageFlags.TopOfPipeBit,
                                VkDependencyFlags.None,
                                0u, NativePtr.zero,
                                0u, NativePtr.zero,
                                1u, &&barrier
                            )
                            Disposable.Empty
                }

        static member Sync(img : ImageSubresourceRange) =
            if img.Image.IsNull then
                Command.Nop
            else
                { new Command() with
                    member x.Enqueue (cmd : CommandBuffer) =
                        let layout = img.Image.Layout
                        let mutable barrier =
                            VkImageMemoryBarrier(
                                VkStructureType.ImageMemoryBarrier, 0n, 
                                VkAccessFlags.Write,
                                VkAccessFlags.Read,
                                layout,
                                layout,
                                VK_QUEUE_FAMILY_IGNORED,
                                VK_QUEUE_FAMILY_IGNORED,
                                img.Image.Handle,
                                img.VkImageSubresourceRange
                            )
                            
                        cmd.AppendCommand()
                        VkRaw.vkCmdPipelineBarrier(
                            cmd.Handle,
                            VkPipelineStageFlags.TopOfPipeBit,
                            VkPipelineStageFlags.TopOfPipeBit,
                            VkDependencyFlags.None,
                            0u, NativePtr.zero,
                            0u, NativePtr.zero,
                            1u, &&barrier
                        )
                        Disposable.Empty
                }




// ===========================================================================================
// Image functions
// ===========================================================================================
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Image =
    [<AbstractClass>]
    type private PixImageVisitor<'r>() =
        static let table =
            LookupTable.lookupTable [
                typeof<int8>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<int8>(unbox img, 127y))
                typeof<uint8>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<uint8>(unbox img, 255uy))
                typeof<int16>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<int16>(unbox img, Int16.MaxValue))
                typeof<uint16>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<uint16>(unbox img, UInt16.MaxValue))
                typeof<int32>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<int32>(unbox img, Int32.MaxValue))
                typeof<uint32>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<uint32>(unbox img, UInt32.MaxValue))
                typeof<int64>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<int64>(unbox img, Int64.MaxValue))
                typeof<uint64>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<uint64>(unbox img, UInt64.MaxValue))
                typeof<float16>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<float16>(unbox img, float16(Float32 = 1.0f)))
                typeof<float32>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<float32>(unbox img, 1.0f))
                typeof<float>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<float>(unbox img, 1.0))
            ]
        abstract member Visit<'a when 'a : unmanaged> : PixImage<'a> * 'a -> 'r

        


        interface IPixImageVisitor<'r> with
            member x.Visit<'a>(img : PixImage<'a>) =
                table (typeof<'a>) (x, img)


    let alloc (size : V3i) (mipMapLevels : int) (count : int) (samples : int) (dim : TextureDimension) (fmt : VkFormat) (compMapping : VkComponentMapping) (usage : VkImageUsageFlags) (device : Device) =
        if device.PhysicalDevice.GetFormatFeatures(VkImageTiling.Optimal, fmt) = VkFormatFeatureFlags.None then
            failf "bad image format %A" fmt

        let flags =
            if dim = TextureDimension.TextureCube then VkImageCreateFlags.CubeCompatibleBit
            else VkImageCreateFlags.None

        let mutable info =
            VkImageCreateInfo(
                VkStructureType.ImageCreateInfo, 0n,
                flags,
                VkImageType.ofTextureDimension dim,
                fmt,
                VkExtent3D(uint32 size.X, uint32 size.Y, uint32 size.Z),
                uint32 mipMapLevels,
                uint32 count,
                unbox<VkSampleCountFlags> samples,
                VkImageTiling.Optimal,
                usage,
                VkSharingMode.Exclusive,
                0u, NativePtr.zero,
                VkImageLayout.Undefined
            ) 

        let mutable handle = VkImage.Null
        VkRaw.vkCreateImage(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "could not create image"

        let mutable reqs = VkMemoryRequirements()
        VkRaw.vkGetImageMemoryRequirements(device.Handle, handle, &&reqs)
        let memalign = int64 reqs.alignment |> Alignment.next device.BufferImageGranularity
        let memsize = int64 reqs.size |> Alignment.next device.BufferImageGranularity
        let ptr = device.DeviceMemory.Alloc(memalign, memsize)

        VkRaw.vkBindImageMemory(device.Handle, handle, ptr.Memory.Handle, uint64 ptr.Offset)
            |> check "could not bind image memory"

        let result = Image(device, handle, size, mipMapLevels, count, samples, dim, fmt, compMapping, ptr, VkImageLayout.Undefined)
        result

    let delete (img : Image) (device : Device) =
        if device.Handle <> 0n && img.Handle.IsValid then
            VkRaw.vkDestroyImage(img.Device.Handle, img.Handle, NativePtr.zero)
            img.Memory.Dispose()
            img.Handle <- VkImage.Null

    let create (size : V3i) (mipMapLevels : int) (count : int) (samples : int) (dim : TextureDimension) (fmt : TextureFormat) (usage : VkImageUsageFlags) (device : Device) =
        let vkfmt = VkFormat.ofTextureFormat fmt
        let swizzle = VkComponentMapping.ofTextureFormat fmt
        alloc size mipMapLevels count samples dim vkfmt swizzle usage device

    let private ofDeviceMemoryImage (dispose : bool) (tempImage : DeviceMemoryImage) (info : TextureParams) (device : Device) =
        if tempImage.LevelCount <= 0 then failf "empty PixImageMipMap"

        let size = tempImage.Size

        // figure out whether to create/upload mipmaps or not
        let generateMipMaps = 
            info.wantMipMaps && tempImage.LevelCount = 1

        let mipMapLevels =
            if info.wantMipMaps then
                if tempImage.LevelCount = 1 then
                    1 + max size.X size.Y |> Fun.Log2 |> floor |> int 
                else 
                    tempImage.LevelCount
            else
                1

        // allocate the final image
        let format          = tempImage.Image.Format
        let size            = V3i(size, 1)
        let usage           = VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.TransferSrcBit
        let compMapping     = VkComponentMapping.Identity
        let img             = device |> alloc size mipMapLevels 1 1 TextureDimension.Texture2D format compMapping usage
        let copyLevels      = min tempImage.LevelCount mipMapLevels

        // enqueue the copy command and finally delete the temporary image
        // if mipmaps need to be generated do that on the GPU-Memory-Image
        device.eventually {
            try 
                do! Command.TransformLayout(tempImage.Image, VkImageLayout.TransferSrcOptimal)
                do! Command.TransformLayout(img, VkImageLayout.TransferDstOptimal)

                let color = img.[tempImage.Aspect, *, 0]
                do! Command.Copy(tempImage.[0 .. copyLevels - 1], color.[0 .. copyLevels - 1])

                // generate mipmaps if needed
                if generateMipMaps then
                    do! Command.TransformLayout(img, VkImageLayout.General)
                    do! Command.GenerateMipMaps color
                    

                // convert to shader read layout
                do! Command.TransformLayout(img, VkImageLayout.ShaderReadOnlyOptimal)

            finally 
                if dispose then device.Delete tempImage
        }

        // finally return the created image
        img

    let ofPixImageMipMap (pi : PixImageMipMap) (info : TextureParams) (device : Device) =
        if pi.LevelCount <= 0 then failf "empty PixImageMipMap"

        let temp = device.CreateDeviceMemoryImage(pi)
        device |> ofDeviceMemoryImage true temp info

    let ofPixImageCube (pi : PixImageCube) (info : TextureParams) (device : Device) =

        let levels = pi.MipMapArray |> Seq.map (fun pi -> pi.LevelCount) |> Seq.min
        if levels < 1 then failf "empty PixImageCube"

        let sizes = pi.MipMapArray |> Seq.map (fun pi -> pi.[0].Size) |> HashSet.ofSeq
        if sizes.Count <> 1 then failf "PixImageCube faces have differing sizes: %A" (Seq.toList sizes)

        let faceSize = sizes |> Seq.head

        let generateMipMaps = info.wantMipMaps && levels = 1

        let mipMapLevels =
            if info.wantMipMaps then
                if levels = 1 then
                    1 + max faceSize.X faceSize.Y |> Fun.Log2 |> floor |> int 
                else 
                    levels
            else
                1

        let copyLevels = min mipMapLevels levels

        let faces = pi.MipMapArray |> Array.map (fun img -> device.CreateDeviceMemoryImage(img, copyLevels))

        // allocate the final image
        let format          = faces.[0].Image.Format
        let aspect          = faces.[0].Aspect
        let size            = V3i(faceSize, 1)
        let usage           = VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.TransferSrcBit
        let compMapping     = VkComponentMapping.Identity
        let img             = device |> alloc size mipMapLevels 6 1 TextureDimension.TextureCube format compMapping usage


        // enqueue the copy command and finally delete the temporary image
        // if mipmaps need to be generated do that on the GPU-Memory-Image
        device.eventually {
            try 
                do! Command.TransformLayout(img, VkImageLayout.TransferDstOptimal)
                for fi in 0 .. 5 do
                    let src = faces.[fi]
                    do! Command.TransformLayout(src.Image, VkImageLayout.TransferSrcOptimal)

                    let dst = img.[aspect, *, fi]
                    do! Command.Copy(src.[0 .. copyLevels-1], dst.[0 .. copyLevels - 1])

                 
                // generate mipmaps if needed
                if generateMipMaps then
                    do! Command.TransformLayout(img, VkImageLayout.General)
                    do! Command.GenerateMipMaps img.[aspect, *, *]
                    
                // convert to shader read layout
                do! Command.TransformLayout(img, VkImageLayout.ShaderReadOnlyOptimal)

            finally 
                faces |> Array.iter device.Delete
        }

        // finally return the created image
        img

    let ofFile (file : string) (info : TextureParams) (device : Device) =
        if not (System.IO.File.Exists file) then failf "file does not exists: %A" file

        let temp = device.CreateDeviceMemoryImage(file)
        device |> ofDeviceMemoryImage true temp info

    let ofTexture (t : ITexture) (device : Device) =
        match t with
            | :? PixTexture2d as t ->
                device |> ofPixImageMipMap t.PixImageMipMap t.TextureParams

            | :? PixTextureCube as c ->
                device |> ofPixImageCube c.PixImageCube c.TextureParams

            | :? NullTexture as t ->
                Image(device, VkImage.Null, V3i.Zero, 0, 0, 1, TextureDimension.Texture2D, VkFormat.Undefined, VkComponentMapping.Identity, DevicePtr.Null, VkImageLayout.ShaderReadOnlyOptimal)

            | :? PixTexture3d as t ->
                failf "please implement volume textures"

            | :? FileTexture as t ->
                device |> ofFile t.FileName t.TextureParams

            | :? INativeTexture as nt ->
                failf "please implement INativeTexture upload"

            | :? BitmapTexture as bt ->
                failf "BitmapTexture considered obsolete"

            | :? Image as t ->
                t

            | _ ->
                failf "unsupported texture-type: %A" t

    let downloadLevel (src : ImageSubresource) (dst : PixImage) (device : Device) =
        let temp = device.CreateDeviceMemoryImage(dst.Size, 1, dst.PixFormat)
        try
            device.TransferFamily.run {
                let layout = src.Image.Layout
                do! Command.TransformLayout(src.Image, VkImageLayout.TransferSrcOptimal)
                do! Command.Copy(src, temp.[0])
                do! Command.TransformLayout(src.Image, layout)
            }
            temp.Download(0, dst)
        finally 
            device.Delete temp

    let uploadLevel (src : PixImage) (dst : ImageSubresource) (device : Device) =
        let temp = device.CreateDeviceMemoryImage(PixImageMipMap [|src|])
        device.eventually {
            try 
                let layout = dst.Image.Layout
                do! Command.TransformLayout(temp.Image, VkImageLayout.TransferSrcOptimal)
                do! Command.TransformLayout(dst.Image, VkImageLayout.TransferDstOptimal)

                do! Command.Copy(temp.[0], dst)
                
                do! Command.TransformLayout(dst.Image, layout)

            finally 
                device.Delete temp
        }

[<AbstractClass; Sealed; Extension>]
type ContextImageExtensions private() =

    [<Extension>]
    static member inline CreateImage(this : Device, pi : PixImageMipMap, info : TextureParams) =
        this |> Image.ofPixImageMipMap pi info

    [<Extension>]
    static member inline CreateImage(this : Device, file : string, info : TextureParams) =
        this |> Image.ofFile file info

    [<Extension>]
    static member inline CreateImage(this : Device, t : ITexture) =
        this |> Image.ofTexture t

    [<Extension>]
    static member inline Delete(this : Device, img : Image) =
        this |> Image.delete img

    [<Extension>]
    static member inline CreateImage(this : Device, size : V3i, mipMapLevels : int, count : int, samples : int, dim : TextureDimension, fmt : TextureFormat, usage : VkImageUsageFlags) =
        this |> Image.create size mipMapLevels count samples dim fmt usage

    [<Extension>]
    static member inline UploadLevel(this : Device, dst : ImageSubresource, src : PixImage) =
        this |> Image.uploadLevel src dst

    [<Extension>]
    static member inline DownloadLevel(this : Device, src : ImageSubresource, dst : PixImage) =
        this |> Image.downloadLevel src dst