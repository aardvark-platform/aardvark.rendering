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
open Aardvark.Rendering.Vulkan.NativeUtilities

#nowarn "9"
#nowarn "51"

[<AutoOpen>]
module ``Image Format Extensions`` =
    
    type DeviceVolume =
        {
            ptr     : DevicePtr
            size    : V2l
            rowSize : int64
        }
    
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

        member x.CreateVolume(def : 'a, src : NativeVolume<'a>, srcFormat : VkFormat, trafo : ImageTrafo, dstFormat : VkFormat) =
            let rowAlign = 4L
            let memAlign = x.MinUniformBufferOffsetAlignment

            let channelSize = int64 sizeof<'a>
            let channels = VkFormat.channels dstFormat |> int64
            let pixelSize = VkFormat.sizeInBytes dstFormat |> int64
            if pixelSize % channels <> 0L || pixelSize / channels <> channelSize then
                failf "cannot copy ill-aligned image"

            let rowSize = src.SX * pixelSize
            let alignedRowSize = Alignment.next rowAlign rowSize

            let totalSize = alignedRowSize * src.SY

            let result = x.HostMemory.Alloc(memAlign, int64 totalSize)
            
            if alignedRowSize % channelSize <> 0L then
                failf "cannot copy ill-aligned image"

            let dy = alignedRowSize / channelSize
            
            let straigtInfo =
                VolumeInfo(
                    0L,
                    V3l(src.SX, src.SY, channels),
                    V3l(channels, dy, 1L)
                )

            result.Mapped(fun pDst ->
                let dst = NativeVolume<'a>(NativePtr.ofNativeInt pDst, straigtInfo.Transformed trafo)

                if dst.SZ = src.SZ then 
                    NativeVolume.copy src dst
                elif dst.SZ > src.SZ then
                    let dst' = dst.SubVolume(0L, 0L, 0L, src.SX, src.SY, src.SZ)
                    NativeVolume.copy src dst'

                    let dst' = dst.SubVolume(0L, 0L, src.SZ, src.SX, src.SY, dst.SZ - src.SZ)
                    NativeVolume.set def dst' 
                else 
                    let src' = src.SubVolume(0L, 0L, 0L, src.SX, src.SY, dst.SZ)
                    NativeVolume.copy src' dst
                NativeVolume.copy src dst
            )

            {
                ptr     = result
                size    = V2l(int64 src.SX, int64 src.SY)
                rowSize = int64 alignedRowSize
            }

    module VkComponentMapping =
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

[<AutoOpen>]
module ``Image Command Extensions`` =
    type Command with
        static member Copy(src : Buffer, srcOffset : int64, srcRowLength : int64, srcSize : V2l, dst : Image, dstSlice : int, dstLevel : int, dstOffset : V3i, size : V3i) =
            if dst.Samples <> 1 then
                failf "cannot copy buffer to multisampled image"

            if dst.Layout <> VkImageLayout.General && dst.Layout <> VkImageLayout.TransferDstOptimal then
                failf "cannot copy buffer to image with layout: %A" dst.Layout

            { new Command<unit>() with
                member x.Enqueue (cmd : CommandBuffer) =
                    let mutable region =
                        VkBufferImageCopy(
                            uint64 srcOffset,
                            uint32 srcRowLength,
                            uint32 srcSize.Y,
                            VkImageSubresourceLayers(VkImageAspectFlags.ColorBit, uint32 dstLevel, uint32 dstSlice, 1u),
                            VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )
                    VkRaw.vkCmdCopyBufferToImage(cmd.Handle, src.Handle, dst.Handle, dst.Layout, 1u, &&region)
                member x.Dispose() =
                    ()
            }

        static member Copy(src : DeviceVolume, dst : Image, dstSlice : int, dstLevel : int, dstOffset : V3i, size : V3i) =
            let device = src.ptr.Device
            command {
                let mutable buffer = VkBuffer.Null
                try
                    let totalSize = src.rowSize * src.size.Y
                    let align = device.MinUniformBufferOffsetAlignment

                    let srcOffset = src.ptr.Offset
                    let srcBufferOffset = Alignment.prev align srcOffset
                    let srcCopyOffset = srcOffset - srcBufferOffset
                    let srcBufferSize = totalSize + srcCopyOffset

                    let mutable srcInfo =
                        VkBufferCreateInfo(
                            VkStructureType.BufferCreateInfo, 0n,
                            VkBufferCreateFlags.None,
                            uint64 srcBufferSize, VkBufferUsageFlags.TransferSrcBit, VkSharingMode.Exclusive, 
                            0u, NativePtr.zero
                    )

                    VkRaw.vkCreateBuffer(device.Handle, &&srcInfo, NativePtr.zero, &&buffer)
                        |> check "could not create temporary buffer"

                    VkRaw.vkBindBufferMemory(device.Handle, buffer, src.ptr.Memory.Handle, uint64 srcBufferOffset)
                        |> check "could not bind temporary buffer memory"


                    let pseudo = Buffer(device, buffer, Unchecked.defaultof<_>)

                    do! Command.Copy(pseudo, srcCopyOffset, src.rowSize, src.size, dst, dstSlice, dstLevel, dstOffset, size)
                finally
                    if buffer.IsValid then
                        VkRaw.vkDestroyBuffer(device.Handle, buffer, NativePtr.zero)
            }

        static member TransformLayout(img : Image, target : VkImageLayout) =
            if img.Layout = target then
                { new Command<unit>() with
                    member x.Enqueue _ = ()
                    member x.Dispose() = ()
                }
            else
                let source = img.Layout
                img.Layout <- target
                { new Command<unit>() with
                    member x.Enqueue (buffer : CommandBuffer) =
                        let queueIndex = uint32 buffer.QueueFamily.Info.index
                        let mutable barrier =
                            VkImageMemoryBarrier(
                                VkStructureType.ImageMemoryBarrier, 0n, 
                                VkAccessFlags.Write,
                                VkAccessFlags.Read,
                                source,
                                target,
                                queueIndex,
                                queueIndex,
                                img.Handle,
                                VkImageSubresourceRange(
                                    VkImageAspectFlags.ColorBit ||| VkImageAspectFlags.DepthBit ||| VkImageAspectFlags.StencilBit, 
                                    0u, uint32 img.MipMapLevels, 
                                    0u, uint32 img.Count
                                )
                            )

                        VkRaw.vkCmdPipelineBarrier(
                            buffer.Handle,
                            VkPipelineStageFlags.None,
                            VkPipelineStageFlags.None,
                            VkDependencyFlags.None,
                            0u, NativePtr.zero,
                            0u, NativePtr.zero,
                            1u, &&barrier
                        )
                    member x.Dispose() =
                        ()
                }

        static member Sync(img : Image) =
            { new Command<unit>() with
                member x.Enqueue (buffer : CommandBuffer) =
                    let queueIndex = uint32 buffer.QueueFamily.Info.index
                    let mutable barrier =
                        VkImageMemoryBarrier(
                            VkStructureType.ImageMemoryBarrier, 0n, 
                            VkAccessFlags.Write,
                            VkAccessFlags.Read,
                            img.Layout,
                            img.Layout,
                            queueIndex,
                            queueIndex,
                            img.Handle,
                            VkImageSubresourceRange(
                                VkImageAspectFlags.ColorBit ||| VkImageAspectFlags.DepthBit ||| VkImageAspectFlags.StencilBit, 
                                0u, uint32 img.MipMapLevels, 
                                0u, uint32 img.Count
                            )
                        )

                    VkRaw.vkCmdPipelineBarrier(
                        buffer.Handle,
                        VkPipelineStageFlags.None,
                        VkPipelineStageFlags.None,
                        VkDependencyFlags.None,
                        0u, NativePtr.zero,
                        0u, NativePtr.zero,
                        1u, &&barrier
                    )
                member x.Dispose() =
                    ()
            }

        static member Blit(src : Image, srcLevel : int, srcSlice : int, srcRange : Box3i, dst : Image, dstLevel : int, dstSlice : int, dstRange : Box3i, filter : VkFilter) =
            { new Command<unit>() with
                member x.Enqueue (buffer : CommandBuffer) =
                    
                    let mutable srcOffsets = VkOffset3D_2()
                    srcOffsets.[0] <- VkOffset3D(srcRange.Min.X, srcRange.Min.Y, srcRange.Min.Z)
                    srcOffsets.[1] <- VkOffset3D(srcRange.SizeX, srcRange.SizeY, srcRange.SizeZ)

                    let mutable dstOffsets = VkOffset3D_2()
                    dstOffsets.[0] <- VkOffset3D(dstRange.Min.X, dstRange.Min.Y, dstRange.Min.Z)
                    dstOffsets.[1] <- VkOffset3D(dstRange.SizeX, dstRange.SizeY, dstRange.SizeZ)

                    let mutable blit =
                        VkImageBlit(
                            VkImageSubresourceLayers(VkImageAspectFlags.All, uint32 srcLevel, uint32 srcSlice, 1u),
                            srcOffsets,
                            VkImageSubresourceLayers(VkImageAspectFlags.All, uint32 dstLevel, uint32 dstSlice, 1u),
                            dstOffsets
                        )
                    

                    VkRaw.vkCmdBlitImage(buffer.Handle, src.Handle, src.Layout, dst.Handle, dst.Layout, 1u, &&blit, filter)

                member x.Dispose() =
                    ()
            }

        static member GenerateMipMaps (img : Image) : Command<unit> =
            command {
                let mutable parentSize = img.Size
                for l in 1 .. img.MipMapLevels - 1 do
                    do! Command.Sync img
                    let size = parentSize / 2
                    do! Command.Blit(
                            img, l - 1, 0, Box3i(V3i.Zero, parentSize), 
                            img, l,     0, Box3i(V3i.Zero, size),
                            VkFilter.Linear
                        )
                    parentSize <- size

            }




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


    let alloc (size : V3i) (mipMapLevels : int) (count : int) (samples : int) (dim : TextureDimension) (fmt : VkFormat) (compMapping : VkComponentMapping) (usage : VkImageUsageFlags) (layout : VkImageLayout) (device : Device) =
        use token = device.ResourceToken

        if device.PhysicalDevice.GetFormatFeatures(VkImageTiling.Optimal, fmt) = VkFormatFeatureFlags.None then
            failf "bad image format %A" fmt

        let mutable info =
            VkImageCreateInfo(
                VkStructureType.ImageCreateInfo, 0n,
                VkImageCreateFlags.None,
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
        let ptr = device.Alloc(reqs, true)

        VkRaw.vkBindImageMemory(device.Handle, handle, ptr.Memory.Handle, uint64 ptr.Offset)
            |> check "could not bind image memory"

        let result = Image(device, handle, size, mipMapLevels, count, samples, dim, fmt, compMapping, ptr, VkImageLayout.Undefined)

        token.enqueue {
            do! Command.TransformLayout(result, layout)
        }

        result

    let delete (img : Image) (device : Device) =
        if device.Handle <> 0n && img.Handle.IsValid then
            VkRaw.vkDestroyImage(img.Device.Handle, img.Handle, NativePtr.zero)
            img.Memory.Dispose()
            img.Handle <- VkImage.Null

    let create (size : V3i) (mipMapLevels : int) (count : int) (samples : int) (dim : TextureDimension) (fmt : TextureFormat) (usage : VkImageUsageFlags) (layout : VkImageLayout) (device : Device) =
        let vkfmt = VkFormat.ofTextureFormat fmt
        let swizzle = VkComponentMapping.ofTextureFormat fmt
        alloc size mipMapLevels count samples dim vkfmt swizzle usage layout device



    let ofPixImageMipMap (pi : PixImageMipMap) (info : TextureParams) (device : Device) =
        use token = device.ResourceToken

        let inputFormat     = VkFormat.ofPixFormat pi.PixFormat
        let textureFormat   = device.GetSupportedFormat(VkImageTiling.Optimal, pi.PixFormat)

        if pi.LevelCount < 1 then
            failf "empty image"

        let level0 = pi.[0]
        let size = V3i(level0.Size.X, level0.Size.Y, 1)

        let mipMapLevels =
            if info.wantMipMaps then
                if pi.LevelCount = 1 then
                    1 + max size.X size.Y |> Fun.Log2 |> floor |> int 
                else pi.LevelCount
            else
                1

        let generateMipMaps =
            info.wantMipMaps && pi.LevelCount = 1

        let copyLevels =
            if info.wantMipMaps then
                if pi.LevelCount = 1 then 1
                else pi.LevelCount
            else
                1


        let usage = VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.TransferSrcBit

        let compMapping = VkComponentMapping.ofColFormat level0.Format
        let img = device |> alloc size mipMapLevels 1 1 TextureDimension.Texture2D textureFormat compMapping usage VkImageLayout.TransferDstOptimal

        let align = device.MinUniformBufferOffsetAlignment

        let levelPointers = 
            Array.init copyLevels (fun l ->
                let img = pi.[l]

                let pointer = 
                    img.Visit {
                        new PixImageVisitor<_>() with
                            member x.Visit (img : PixImage<'a>, def : 'a) =
                                let gc = GCHandle.Alloc(img.Volume.Data, GCHandleType.Pinned)
                                try
                                    let src =
                                        NativeVolume<'a>(
                                            NativePtr.ofNativeInt (gc.AddrOfPinnedObject()), 
                                            img.Volume.Info
                                        )
                                
                                    device.CreateVolume(def, src, inputFormat, ImageTrafo.MirrorY, textureFormat)
                                finally
                                    gc.Free()
                            
                    }

                pointer
            )

        token.enqueue {
            try 
                // copy the existing data
                let mutable level = 0
                for ptr in levelPointers do
                    let size = ptr.size
                    do! Command.Copy(ptr, img, 0, level, V3i.Zero, V3i(int size.X, int size.Y, 1))
                    level <- level + 1

                // generate mipmaps if needed
                if generateMipMaps then
                    do! Command.GenerateMipMaps img

                // convert to shader read layout
                do! Command.TransformLayout(img, VkImageLayout.ShaderReadOnlyOptimal)

            finally 
                // release all temporary buffers
                for p in levelPointers do p.ptr.Dispose()
        }


        img

    let ofFile (file : string) (info : TextureParams) (device : Device) =
        // TODO: directly use devil here
        let img = PixImage.Create(file).ToPixImage<uint16>(Col.Format.BGR) :> PixImage
        ofPixImageMipMap (PixImageMipMap [|img|]) info device

    let ofTexture (t : ITexture) (device : Device) =
        match t with
            | :? PixTexture2d as t ->
                device |> ofPixImageMipMap t.PixImageMipMap t.TextureParams

            | :? FileTexture as t ->
                device |> ofFile t.FileName t.TextureParams

            | :? Image as t ->
                t

            | _ ->
                failf "unsupported texture-type: %A" t



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
    static member inline CreateImage(this : Device, size : V3i, mipMapLevels : int, count : int, samples : int, dim : TextureDimension, fmt : TextureFormat, usage : VkImageUsageFlags, layout : VkImageLayout) =
        this |> Image.create size mipMapLevels count samples dim fmt usage layout