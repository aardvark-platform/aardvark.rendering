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
open Aardvark.Base.ReflectionHelpers

#nowarn "9"
#nowarn "51"

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

[<AbstractClass>]
type private PixVolumeVisitor<'r>() =
    static let table =
        LookupTable.lookupTable [
            typeof<int8>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<int8>(unbox img, 127y))
            typeof<uint8>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<uint8>(unbox img, 255uy))
            typeof<int16>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<int16>(unbox img, Int16.MaxValue))
            typeof<uint16>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<uint16>(unbox img, UInt16.MaxValue))
            typeof<int32>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<int32>(unbox img, Int32.MaxValue))
            typeof<uint32>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<uint32>(unbox img, UInt32.MaxValue))
            typeof<int64>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<int64>(unbox img, Int64.MaxValue))
            typeof<uint64>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<uint64>(unbox img, UInt64.MaxValue))
            typeof<float16>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<float16>(unbox img, float16(Float32 = 1.0f)))
            typeof<float32>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<float32>(unbox img, 1.0f))
            typeof<float>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<float>(unbox img, 1.0))
        ]
    abstract member Visit<'a when 'a : unmanaged> : PixVolume<'a> * 'a -> 'r

    interface IPixVolumeVisitor<'r> with
        member x.Visit<'a>(img : PixVolume<'a>) =
            table (typeof<'a>) (x, img)


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

        let hasDepth (fmt : VkFormat) =
            depthFormats.Contains fmt || depthStencilFormats.Contains fmt

        let toAspect (fmt : VkFormat) =
            if depthStencilFormats.Contains fmt then VkImageAspectFlags.DepthBit ||| VkImageAspectFlags.StencilBit
            elif depthFormats.Contains fmt then VkImageAspectFlags.DepthBit
            else VkImageAspectFlags.ColorBit

        let toShaderAspect (fmt : VkFormat) =
            if depthStencilFormats.Contains fmt then VkImageAspectFlags.DepthBit 
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


        let expectedType =
            LookupTable.lookupTable [
                VkFormat.Undefined, null
                VkFormat.R4g4UnormPack8, typeof<uint8>
                VkFormat.R4g4b4a4UnormPack16, typeof<uint16>
                VkFormat.B4g4r4a4UnormPack16, typeof<uint16>
                VkFormat.R5g6b5UnormPack16, typeof<uint16>
                VkFormat.B5g6r5UnormPack16, typeof<uint16>
                VkFormat.R5g5b5a1UnormPack16, typeof<uint16>
                VkFormat.B5g5r5a1UnormPack16, typeof<uint16>
                VkFormat.A1r5g5b5UnormPack16, typeof<uint16>
                VkFormat.R8Unorm, typeof<uint8>
                VkFormat.R8Snorm, typeof<int8>
                VkFormat.R8Uscaled, typeof<uint8>
                VkFormat.R8Sscaled, typeof<int8>
                VkFormat.R8Uint, typeof<uint8>
                VkFormat.R8Sint, typeof<int8>
                VkFormat.R8Srgb, typeof<uint8>
                VkFormat.R8g8Unorm, typeof<uint8>
                VkFormat.R8g8Snorm, typeof<int8>
                VkFormat.R8g8Uscaled, typeof<uint8>
                VkFormat.R8g8Sscaled, typeof<int8>
                VkFormat.R8g8Uint, typeof<uint8>
                VkFormat.R8g8Sint, typeof<int8>
                VkFormat.R8g8Srgb, typeof<uint8>
                VkFormat.R8g8b8Unorm, typeof<uint8>
                VkFormat.R8g8b8Snorm, typeof<int8>
                VkFormat.R8g8b8Uscaled, typeof<uint8>
                VkFormat.R8g8b8Sscaled, typeof<int8>
                VkFormat.R8g8b8Uint, typeof<uint8>
                VkFormat.R8g8b8Sint, typeof<int8>
                VkFormat.R8g8b8Srgb, typeof<uint8>
                VkFormat.B8g8r8Unorm, typeof<uint8>
                VkFormat.B8g8r8Snorm, typeof<int8>
                VkFormat.B8g8r8Uscaled, typeof<uint8>
                VkFormat.B8g8r8Sscaled, typeof<int8>
                VkFormat.B8g8r8Uint, typeof<uint8>
                VkFormat.B8g8r8Sint, typeof<int8>
                VkFormat.B8g8r8Srgb, typeof<uint8>
                VkFormat.R8g8b8a8Unorm, typeof<uint8>
                VkFormat.R8g8b8a8Snorm, typeof<int8>
                VkFormat.R8g8b8a8Uscaled, typeof<uint8>
                VkFormat.R8g8b8a8Sscaled, typeof<int8>
                VkFormat.R8g8b8a8Uint, typeof<uint8>
                VkFormat.R8g8b8a8Sint, typeof<int8>
                VkFormat.R8g8b8a8Srgb, typeof<uint8>
                VkFormat.B8g8r8a8Unorm, typeof<uint8>
                VkFormat.B8g8r8a8Snorm, typeof<int8>
                VkFormat.B8g8r8a8Uscaled, typeof<uint8>
                VkFormat.B8g8r8a8Sscaled, typeof<int8>
                VkFormat.B8g8r8a8Uint, typeof<uint8>
                VkFormat.B8g8r8a8Sint, typeof<int8>
                VkFormat.B8g8r8a8Srgb, typeof<uint8>
                VkFormat.A8b8g8r8UnormPack32, typeof<uint32>
                VkFormat.A8b8g8r8SnormPack32, typeof<uint32>
                VkFormat.A8b8g8r8UscaledPack32, typeof<uint32>
                VkFormat.A8b8g8r8SscaledPack32, typeof<uint32>
                VkFormat.A8b8g8r8UintPack32, typeof<uint32>
                VkFormat.A8b8g8r8SintPack32, typeof<uint32>
                VkFormat.A8b8g8r8SrgbPack32, typeof<uint32>
                VkFormat.A2r10g10b10UnormPack32, typeof<uint32>
                VkFormat.A2r10g10b10SnormPack32, typeof<uint32>
                VkFormat.A2r10g10b10UscaledPack32, typeof<uint32>
                VkFormat.A2r10g10b10SscaledPack32, typeof<uint32>
                VkFormat.A2r10g10b10UintPack32, typeof<uint32>
                VkFormat.A2r10g10b10SintPack32, typeof<uint32>
                VkFormat.A2b10g10r10UnormPack32, typeof<uint32>
                VkFormat.A2b10g10r10SnormPack32, typeof<uint32>
                VkFormat.A2b10g10r10UscaledPack32, typeof<uint32>
                VkFormat.A2b10g10r10SscaledPack32, typeof<uint32>
                VkFormat.A2b10g10r10UintPack32, typeof<uint32>
                VkFormat.A2b10g10r10SintPack32, typeof<uint32>
                VkFormat.R16Unorm, typeof<uint16>
                VkFormat.R16Snorm, typeof<int16>
                VkFormat.R16Uscaled, typeof<uint16>
                VkFormat.R16Sscaled, typeof<int16>
                VkFormat.R16Uint, typeof<uint16>
                VkFormat.R16Sint, typeof<int16>
                VkFormat.R16Sfloat, typeof<float16>
                VkFormat.R16g16Unorm, typeof<uint16>
                VkFormat.R16g16Snorm, typeof<int16>
                VkFormat.R16g16Uscaled, typeof<uint16>
                VkFormat.R16g16Sscaled, typeof<int16>
                VkFormat.R16g16Uint, typeof<uint16>
                VkFormat.R16g16Sint, typeof<int16>
                VkFormat.R16g16Sfloat, typeof<float16>
                VkFormat.R16g16b16Unorm, typeof<uint16>
                VkFormat.R16g16b16Snorm, typeof<int16>
                VkFormat.R16g16b16Uscaled, typeof<uint16>
                VkFormat.R16g16b16Sscaled, typeof<int16>
                VkFormat.R16g16b16Uint, typeof<uint16>
                VkFormat.R16g16b16Sint, typeof<int16>
                VkFormat.R16g16b16Sfloat, typeof<float16>
                VkFormat.R16g16b16a16Unorm, typeof<uint16>
                VkFormat.R16g16b16a16Snorm, typeof<int16>
                VkFormat.R16g16b16a16Uscaled, typeof<uint16>
                VkFormat.R16g16b16a16Sscaled, typeof<int16>
                VkFormat.R16g16b16a16Uint, typeof<uint16>
                VkFormat.R16g16b16a16Sint, typeof<int16>
                VkFormat.R16g16b16a16Sfloat, typeof<float16>
                VkFormat.R32Uint, typeof<uint32>
                VkFormat.R32Sint, typeof<int32>
                VkFormat.R32Sfloat, typeof<float32>
                VkFormat.R32g32Uint, typeof<uint32>
                VkFormat.R32g32Sint, typeof<int32>
                VkFormat.R32g32Sfloat, typeof<float32>
                VkFormat.R32g32b32Uint, typeof<uint32>
                VkFormat.R32g32b32Sint, typeof<int32>
                VkFormat.R32g32b32Sfloat, typeof<float32>
                VkFormat.R32g32b32a32Uint, typeof<uint32>
                VkFormat.R32g32b32a32Sint, typeof<int32>
                VkFormat.R32g32b32a32Sfloat, typeof<float32>
                VkFormat.R64Uint, typeof<uint64>
                VkFormat.R64Sint, typeof<int64>
                VkFormat.R64Sfloat, typeof<float>
                VkFormat.R64g64Uint, typeof<uint64>
                VkFormat.R64g64Sint, typeof<int64>
                VkFormat.R64g64Sfloat, typeof<float>
                VkFormat.R64g64b64Uint, typeof<uint64>
                VkFormat.R64g64b64Sint, typeof<int64>
                VkFormat.R64g64b64Sfloat, typeof<float>
                VkFormat.R64g64b64a64Uint, typeof<uint64>
                VkFormat.R64g64b64a64Sint, typeof<int64>
                VkFormat.R64g64b64a64Sfloat, typeof<float>
                VkFormat.B10g11r11UfloatPack32, typeof<uint32>
                VkFormat.E5b9g9r9UfloatPack32, typeof<uint32>
                VkFormat.D16Unorm, typeof<uint16>
                VkFormat.X8D24UnormPack32, typeof<uint32>
                VkFormat.D32Sfloat, null
                VkFormat.S8Uint, typeof<uint8>
                VkFormat.D16UnormS8Uint, null
                VkFormat.D24UnormS8Uint, typeof<uint32>
                VkFormat.D32SfloatS8Uint, null
                VkFormat.Bc1RgbUnormBlock, null
                VkFormat.Bc1RgbSrgbBlock, null
                VkFormat.Bc1RgbaUnormBlock, null
                VkFormat.Bc1RgbaSrgbBlock, null
                VkFormat.Bc2UnormBlock, null
                VkFormat.Bc2SrgbBlock, null
                VkFormat.Bc3UnormBlock, null
                VkFormat.Bc3SrgbBlock, null
                VkFormat.Bc4UnormBlock, null
                VkFormat.Bc4SnormBlock, null
                VkFormat.Bc5UnormBlock, null
                VkFormat.Bc5SnormBlock, null
                VkFormat.Bc6hUfloatBlock, null
                VkFormat.Bc6hSfloatBlock, null
                VkFormat.Bc7UnormBlock, null
                VkFormat.Bc7SrgbBlock, null
                VkFormat.Etc2R8g8b8UnormBlock, null
                VkFormat.Etc2R8g8b8SrgbBlock, null
                VkFormat.Etc2R8g8b8a1UnormBlock, null
                VkFormat.Etc2R8g8b8a1SrgbBlock, null
                VkFormat.Etc2R8g8b8a8UnormBlock, null
                VkFormat.Etc2R8g8b8a8SrgbBlock, null
                VkFormat.EacR11UnormBlock, null
                VkFormat.EacR11SnormBlock, null
                VkFormat.EacR11g11UnormBlock, null
                VkFormat.EacR11g11SnormBlock, null
                VkFormat.Astc44UnormBlock, null
                VkFormat.Astc44SrgbBlock, null
                VkFormat.Astc54UnormBlock, null
                VkFormat.Astc54SrgbBlock, null
                VkFormat.Astc55UnormBlock, null
                VkFormat.Astc55SrgbBlock, null
                VkFormat.Astc65UnormBlock, null
                VkFormat.Astc65SrgbBlock, null
                VkFormat.Astc66UnormBlock, null
                VkFormat.Astc66SrgbBlock, null
                VkFormat.Astc85UnormBlock, null
                VkFormat.Astc85SrgbBlock, null
                VkFormat.Astc86UnormBlock, null
                VkFormat.Astc86SrgbBlock, null
                VkFormat.Astc88UnormBlock, null
                VkFormat.Astc88SrgbBlock, null
                VkFormat.Astc105UnormBlock, null
                VkFormat.Astc105SrgbBlock, null
                VkFormat.Astc106UnormBlock, null
                VkFormat.Astc106SrgbBlock, null
                VkFormat.Astc108UnormBlock, null
                VkFormat.Astc108SrgbBlock, null
                VkFormat.Astc1010UnormBlock, null
                VkFormat.Astc1010SrgbBlock, null
                VkFormat.Astc1210UnormBlock, null
                VkFormat.Astc1210SrgbBlock, null
                VkFormat.Astc1212UnormBlock, null
                VkFormat.Astc1212SrgbBlock, null               
            ]

        let ofPixFormat (fmt : PixFormat) (t : TextureParams) =
            TextureFormat.ofPixFormat fmt t |> ofTextureFormat

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
        
        member x.GetSupportedFormat(tiling : VkImageTiling, fmt : PixFormat, t : TextureParams) =
            let retry f = x.GetSupportedFormat(tiling, PixFormat(fmt.Type, f), t)

            match fmt.Format with
                | Col.Format.BGR    -> retry Col.Format.RGB
                | Col.Format.BGRA   -> retry Col.Format.RGBA
                | Col.Format.BGRP   -> retry Col.Format.RGBP
                | _ -> 
                    let test = VkFormat.ofPixFormat fmt t
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

    let private colFormatChannels =
        Dictionary.ofList [
            Col.Format.Alpha, 1
            Col.Format.BGR, 3
            Col.Format.BGRA, 4
            Col.Format.BGRP, 4
            Col.Format.BW, 1
            Col.Format.Gray, 1
            Col.Format.GrayAlpha, 2
            Col.Format.NormalUV, 2
            Col.Format.RGB, 3
            Col.Format.RGBA, 4
            Col.Format.RGBP, 4
        ]

    type Col.Format with
        member x.Channels = 
            match colFormatChannels.TryGetValue x with
                | (true, c) -> c
                | _ -> failf "could not get channelcount for format %A" x

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
        val mutable public Memory : DevicePtr
        val mutable public Layout : VkImageLayout
        val mutable public RefCount : int

        member x.AddReference() = Interlocked.Increment(&x.RefCount) |> ignore

        interface ITexture with 
            member x.WantMipMaps = x.MipMapLevels > 1

        interface IBackendTexture with
            member x.Runtime = x.Device.Runtime :> ITextureRuntime
            member x.Handle = x.Handle :> obj
            member x.Count = x.Count
            member x.Dimension = x.Dimension
            member x.Format = VkFormat.toTextureFormat x.Format
            member x.MipMapLevels = x.MipMapLevels
            member x.Samples = x.Samples
            member x.Size = x.Size

        interface IRenderbuffer with
            member x.Runtime = x.Device.Runtime :> ITextureRuntime
            member x.Size = x.Size.XY
            member x.Samples = x.Samples
            member x.Format = VkFormat.toTextureFormat x.Format |> TextureFormat.toRenderbufferFormat
            member x.Handle = x.Handle :> obj

        member x.IsNull = x.Handle.IsNull

        member x.Item with get(aspect : ImageAspect) = ImageSubresourceRange(x, aspect, 0, x.MipMapLevels, 0, x.Count)
        member x.Item with get(aspect : ImageAspect, level : int) = ImageSubresourceLayers(x, aspect, level, 0, x.Count)
        member x.Item with get(aspect : ImageAspect, level : int, slice : int) = ImageSubresource(x, aspect, level, slice)
              
                
        member x.GetSlice(aspect : ImageAspect, minLevel : Option<int>, maxLevel : Option<int>, minSlice : Option<int>, maxSlice : Option<int>) =
            x.[aspect].GetSlice(minLevel, maxLevel, minSlice, maxSlice)

        member x.GetSlice(aspect : ImageAspect, minLevel : Option<int>, maxLevel : Option<int>, slice : int) =
            x.[aspect].GetSlice(minLevel, maxLevel, slice)
      
        member x.GetSlice(aspect : ImageAspect, level : int, minSlice : Option<int>, maxSlice : Option<int>) =
            x.[aspect].GetSlice(level, minSlice, maxSlice)

            
        override x.ToString() =
            sprintf "0x%08X" x.Handle.Handle

        new(dev, handle, s, levels, count, samples, dim, fmt, mem, layout) = 
            {
                inherit Resource<_>(dev, handle);
                Size = s
                MipMapLevels = levels
                Count = count
                Samples = samples
                Dimension = dim
                Format = fmt
                Memory = mem
                Layout = layout
                RefCount = 1
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

[<Struct>]
type DeviceVector<'a when 'a : unmanaged>(memory : DevicePtr, info : VectorInfo) =
    member x.Device = memory.Device
    member x.Info = info

    member x.Size = info.Size

    member x.Mapped (action : NativeVector<'a> -> 'r) =
        let info = info
        memory.Mapped(fun ptr ->
            action (NativeVector<'a>(NativePtr.ofNativeInt ptr, info))
        )

    member x.SubVector(origin : int64, size : int64, delta : int64) =
        DeviceVector<'a>(memory, info.SubVector(origin, size, delta))

    member x.SubVector(origin : int64, size : int64) = x.SubVector(origin, size, info.Delta)
    member x.SubVector(size : int64) = x.SubVector(info.Origin, size, info.Delta)

    // int64 slices
    member x.GetSlice(min : Option<int64>, max : Option<int64>) =
        let min = match min with | Some v -> v | None -> 0L
        let max = match max with | Some v -> v | None -> info.Size - 1L
        x.SubVector(min, 1L + max - min)

    // int slices
    member x.GetSlice(min : Option<int>, max : Option<int>) =
        let min = match min with | Some v -> int64 v | None -> 0L
        let max = match max with | Some v -> int64 v | None -> info.Size - 1L
        x.SubVector(min, 1L + max - min)

    member x.CopyTo(dst : NativeVector<'a>) =
        if info.Size <> dst.Size then failf "mismatching Vector size"
        x.Mapped (fun src ->
            NativeVector.copy src dst
        )

    member x.CopyTo(dst : Vector<'a>) =
        if info.Size <> dst.Size then failf "mismatching Vector size"
        x.Mapped (fun src ->
            NativeVector.using dst (fun dst ->
                NativeVector.copy src dst
            )
        )

    member x.CopyFrom(src : NativeVector<'a>) =
        if info.Size <> src.Size then failf "mismatching Vector size"
        x.Mapped (fun dst ->
            NativeVector.copy src dst
        )

    member x.CopyFrom(src : Vector<'a>) =
        if info.Size <> src.Size then failf "mismatching Vector size"
        x.Mapped (fun dst ->
            NativeVector.using src (fun src ->
                NativeVector.copy src dst
            )
        )

    member x.Set(v : 'a) =
        if info.Size > 0L then
            x.Mapped (fun dst ->
                NativeVector.set v dst
            )
        
[<Struct>]
type DeviceMatrix<'a when 'a : unmanaged>(memory : DevicePtr, info : MatrixInfo) =
    member x.Device = memory.Device
    member x.Info = info

    member x.Size = info.Size

    member x.Mapped (action : NativeMatrix<'a> -> 'r) =
        let info = info
        memory.Mapped(fun ptr ->
            action (NativeMatrix<'a>(NativePtr.ofNativeInt ptr, info))
        )

    member x.SubMatrix(origin : V2l, size : V2l, delta : V2l) =
        DeviceMatrix<'a>(memory, info.SubMatrix(origin, size, delta))
        
    member x.SubMatrix(origin : V2l, size : V2l) = x.SubMatrix(origin, size, info.Delta)
    member x.SubMatrix(size : V2l) = x.SubMatrix(V2l.Zero, size, info.Delta)

    member this.SubXVector(y : int64) = DeviceVector<'a>(memory, info.SubXVector(y))
    member this.SubYVector(x : int64) = DeviceVector<'a>(memory, info.SubYVector(x))
    
    // int64 slices
    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>) =
        let minX = match minX with | Some v -> v | None -> 0L
        let minY = match minY with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        this.SubMatrix(V2l(minX, minY), V2l(1L + maxX - minX, 1L + maxY - minY), info.Delta)

    member this.GetSlice(x : int64, minY : Option<int64>, maxY : Option<int64>) =
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        this.SubYVector(x).SubVector(minY, 1L + maxY - minY)

    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, y : int64) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        this.SubXVector(y).SubVector(minX, 1L + maxX - minX)
        
    // int slices
    member this.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        this.SubMatrix(V2l(minX, minY), V2l(1L + maxX - minX, 1L + maxY - minY), info.Delta)

    member this.GetSlice(x : int, minY : Option<int>, maxY : Option<int>) =
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        this.SubYVector(int64 x).SubVector(minY, 1L + maxY - minY)

    member this.GetSlice(minX : Option<int>, maxX : Option<int>, y : int) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        this.SubXVector(int64 y).SubVector(minX, 1L + maxX - minX)


    member x.CopyTo(dst : NativeMatrix<'a>) =
        if info.Size <> dst.Size then failf "mismatching Matrix size"
        x.Mapped (fun src ->
            NativeMatrix.copy src dst
        )

    member x.CopyTo(dst : Matrix<'a>) =
        if info.Size <> dst.Size then failf "mismatching Matrix size"
        x.Mapped (fun src ->
            NativeMatrix.using dst (fun dst ->
                NativeMatrix.copy src dst
            )
        )

    member x.CopyFrom(src : NativeMatrix<'a>) =
        if info.Size <> src.Size then failf "mismatching Matrix size"
        x.Mapped (fun dst ->
            NativeMatrix.copy src dst
        )

    member x.CopyFrom(src : Matrix<'a>) =
        if info.Size <> src.Size then failf "mismatching Matrix size"
        x.Mapped (fun dst ->
            NativeMatrix.using src (fun src ->
                NativeMatrix.copy src dst
            )
        )

    member x.Set(v : 'a) =
        if info.Size.AllGreater 0L then
            x.Mapped (fun dst ->
                NativeMatrix.set v dst
            )
       
[<Struct>]
type DeviceVolume<'a when 'a : unmanaged>(memory : DevicePtr, info : VolumeInfo) =
    member x.Device = memory.Device
    member x.Info = info

    member x.Size = info.Size

    member x.Mapped (action : NativeVolume<'a> -> 'r) =
        let info = info
        memory.Mapped(fun ptr ->
            action (NativeVolume<'a>(NativePtr.ofNativeInt ptr, info))
        )

    member x.SubVolume(origin : V3l, size : V3l, delta : V3l) =
        DeviceVolume<'a>(memory, info.SubVolume(origin, size, delta))
      
    member x.SubVolume(origin : V3l, size : V3l) = x.SubVolume(origin, size, info.Delta)
    member x.SubVolume(size : V3l) = x.SubVolume(V3l.Zero, size, info.Delta)
        
    member this.SubYZMatrix(x : int64) = DeviceMatrix<'a>(memory, info.SubYZMatrix(x))  
    member this.SubXZMatrix(y : int64) = DeviceMatrix<'a>(memory, info.SubXZMatrix(y))   
    member this.SubXYMatrix(z : int64) = DeviceMatrix<'a>(memory, info.SubXYMatrix(z))   
    
    member this.SubXVector(y : int64, z : int64) = DeviceVector<'a>(memory, VectorInfo(info.Index(0L, y, z), info.SX, info.DX))
    member this.SubYVector(x : int64, z : int64) = DeviceVector<'a>(memory, VectorInfo(info.Index(x, 0L, z), info.SY, info.DY))
    member this.SubZVector(x : int64, y : int64) = DeviceVector<'a>(memory, VectorInfo(info.Index(x, y, 0L), info.SZ, info.DZ))

    // int64 slices
    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> v | None -> 0L
        let maxZ = match maxZ with | Some v -> v | None -> (info.Size.Z - 1L)
        this.SubVolume(V3l(minX, minY, minZ), V3l(1L + maxX - minX, 1L + maxY - minY, 1L + maxZ - minZ), info.Delta)

    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, z : int64) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        this.SubXYMatrix(z).SubMatrix(V2l(minX, minY), V2l(1L + maxX - minX, 1L + maxY - minY))
        
    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, y : int64, minZ : Option<int64>, maxZ : Option<int64>) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        let minZ = match minZ with | Some v -> v | None -> 0L
        let maxZ = match maxZ with | Some v -> v | None -> (info.Size.Z - 1L)
        this.SubXZMatrix(y).SubMatrix(V2l(minX, maxZ), V2l(1L + maxX - minX, 1L + maxZ - minZ))

    member this.GetSlice(x : int64, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>) =
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> v | None -> 0L
        let maxZ = match maxZ with | Some v -> v | None -> (info.Size.Z - 1L)
        this.SubYZMatrix(x).SubMatrix(V2l(minY, maxZ), V2l(1L + maxY - minY, 1L + maxZ - minZ))

    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, y : int64, z : int64) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        this.SubXVector(y,z).SubVector(minX, 1L + maxX - minX)
        
    member this.GetSlice(x : int64, minY : Option<int64>, maxY : Option<int64>, z : int64) =
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        this.SubYVector(x,z).SubVector(minY, 1L + maxY - minY)

    member this.GetSlice(x : int64, y : int64, minZ : Option<int64>, maxZ : Option<int64>) =
        let minZ = match minZ with | Some v -> v | None -> 0L
        let maxZ = match maxZ with | Some v -> v | None -> (info.Size.Z - 1L)
        this.SubXVector(x,y).SubVector(minZ, 1L + maxZ - minZ)

    // int slices
    member this.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> int64 v | None -> 0L
        let maxZ = match maxZ with | Some v -> int64 v | None -> (info.Size.Z - 1L)
        this.SubVolume(V3l(minX, minY, minZ), V3l(1L + maxX - minX, 1L + maxY - minY, 1L + maxZ - minZ), info.Delta)

    member this.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, z : int) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        this.SubXYMatrix(int64 z).SubMatrix(V2l(minX, minY), V2l(1L + maxX - minX, 1L + maxY - minY))
        
    member this.GetSlice(minX : Option<int>, maxX : Option<int>, y : int, minZ : Option<int>, maxZ : Option<int>) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        let minZ = match minZ with | Some v -> int64 v | None -> 0L
        let maxZ = match maxZ with | Some v -> int64 v | None -> (info.Size.Z - 1L)
        this.SubXZMatrix(int64 y).SubMatrix(V2l(minX, maxZ), V2l(1L + maxX - minX, 1L + maxZ - minZ))

    member this.GetSlice(x : int, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>) =
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> int64 v | None -> 0L
        let maxZ = match maxZ with | Some v -> int64 v | None -> (info.Size.Z - 1L)
        this.SubYZMatrix(int64 x).SubMatrix(V2l(minY, maxZ), V2l(1L + maxY - minY, 1L + maxZ - minZ))

    member this.GetSlice(minX : Option<int>, maxX : Option<int>, y : int, z : int) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        this.SubXVector(int64 y, int64 z).SubVector(minX, 1L + maxX - minX)
        
    member this.GetSlice(x : int, minY : Option<int>, maxY : Option<int>, z : int) =
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        this.SubYVector(int64 x, int64 z).SubVector(minY, 1L + maxY - minY)

    member this.GetSlice(x : int, y : int, minZ : Option<int>, maxZ : Option<int>) =
        let minZ = match minZ with | Some v -> int64 v | None -> 0L
        let maxZ = match maxZ with | Some v -> int64 v | None -> (info.Size.Z - 1L)
        this.SubXVector(int64 x, int64 y).SubVector(minZ, 1L + maxZ - minZ)


    member x.CopyTo(dst : NativeVolume<'a>) =
        if info.Size <> dst.Size then failf "mismatching Volume size"
        x.Mapped (fun src ->
            NativeVolume.copy src dst
        )

    member x.CopyTo(dst : Volume<'a>) =
        if info.Size <> dst.Size then failf "mismatching Volume size"
        x.Mapped (fun src ->
            NativeVolume.using dst (fun dst ->
                NativeVolume.copy src dst
            )
        )

    member x.CopyFrom(src : NativeVolume<'a>) =
        if info.Size <> src.Size then failf "mismatching Volume size"
        x.Mapped (fun dst ->
            NativeVolume.copy src dst
        )

    member x.CopyFrom(src : Volume<'a>) =
        if info.Size <> src.Size then failf "mismatching Volume size"
        x.Mapped (fun dst ->
            NativeVolume.using src (fun src ->
                NativeVolume.copy src dst
            )
        )

    member x.Set(v : 'a) =
        if info.Size.AllGreater 0L then
            x.Mapped (fun dst ->
                NativeVolume.set v dst
            )
       
[<Struct>]
type DeviceTensor4<'a when 'a : unmanaged>(memory : DevicePtr, info : Tensor4Info) =
    member x.Device = memory.Device
    member x.Info = info

    member x.Size = info.Size

    member x.Mapped (action : NativeTensor4<'a> -> 'r) =
        let info = info
        memory.Mapped(fun ptr ->
            action (NativeTensor4<'a>(NativePtr.ofNativeInt ptr, info))
        )

    member x.SubTensor4(origin : V4l, size : V4l, delta : V4l) =
        DeviceTensor4<'a>(memory, info.SubTensor4(origin, size, delta))
       
    member x.SubTensor4(origin : V4l, size : V4l) = x.SubTensor4(origin, size, info.Delta)
    member x.SubTensor4(size : V4l) = x.SubTensor4(V4l.Zero, size, info.Delta)
            
    member this.SubXYZVolume(w : int64) = DeviceVolume<'a>(memory, info.SubXYZVolume(w))
    member this.SubXYWVolume(z : int64) = DeviceVolume<'a>(memory, info.SubXYWVolume(z))
    member this.SubXZWVolume(y : int64) = DeviceVolume<'a>(memory, info.SubXZWVolume(y))
    member this.SubYZWVolume(x : int64) = DeviceVolume<'a>(memory, info.SubYZWVolume(x))

    member this.SubXYMatrix(z : int64, w : int64) = DeviceMatrix<'a>(memory, MatrixInfo(info.Index(0L, 0L, z, w), info.Size.XY, info.Delta.XY))
    member this.SubXZMatrix(y : int64, w : int64) = DeviceMatrix<'a>(memory, MatrixInfo(info.Index(0L, y, 0L, w), info.Size.XZ, info.Delta.XZ))
    member this.SubXWMatrix(y : int64, z : int64) = DeviceMatrix<'a>(memory, MatrixInfo(info.Index(0L, y, z, 0L), info.Size.XW, info.Delta.XW))
    member this.SubYZMatrix(x : int64, w : int64) = DeviceMatrix<'a>(memory, MatrixInfo(info.Index(x, 0L, 0L, w), info.Size.YZ, info.Delta.YZ))
    member this.SubYWMatrix(x : int64, z : int64) = DeviceMatrix<'a>(memory, MatrixInfo(info.Index(x, 0L, z, 0L), info.Size.YW, info.Delta.YW))
    member this.SubZWMatrix(x : int64, y : int64) = DeviceMatrix<'a>(memory, MatrixInfo(info.Index(x, y, 0L, 0L), info.Size.ZW, info.Delta.ZW))

    member this.SubXVector(y : int64, z : int64, w : int64) = DeviceVector<'a>(memory, VectorInfo(info.Index(0L, y, z, w), info.SX, info.DX))
    member this.SubYVector(x : int64, z : int64, w : int64) = DeviceVector<'a>(memory, VectorInfo(info.Index(x, 0L, z, w), info.SY, info.DY))
    member this.SubZVector(x : int64, y : int64, w : int64) = DeviceVector<'a>(memory, VectorInfo(info.Index(x, y, 0L, w), info.SZ, info.SZ))
    member this.SubWVector(x : int64, y : int64, z : int64) = DeviceVector<'a>(memory, VectorInfo(info.Index(x, y, z, 0L), info.SW, info.SW))

    // int64 slices
    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> v | None -> 0L
        let maxZ = match maxZ with | Some v -> v | None -> (info.Size.Z - 1L)
        let minW = match minW with | Some v -> v | None -> 0L
        let maxW = match maxW with | Some v -> v | None -> (info.Size.W - 1L)
        this.SubTensor4(
            V4l(minX, minY, minZ, minW), 
            V4l(1L + maxX - minX, 1L + maxY - minY, 1L + maxZ - minZ, 1L + maxW - minW), 
            info.Delta
        )

    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, w : int64) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> v | None -> 0L
        let maxZ = match maxZ with | Some v -> v | None -> (info.Size.Z - 1L)

        this.SubXYZVolume(w).SubVolume(V3l(minX, minY, minZ), V3l(1L + maxX - minX, 1L + maxY - minY, 1L + maxZ - minZ))

    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, z : int64, minW : Option<int64>, maxW : Option<int64>) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        let minW = match minW with | Some v -> v | None -> 0L
        let maxW = match maxW with | Some v -> v | None -> (info.Size.W - 1L)

        this.SubXYWVolume(z).SubVolume(V3l(minX, minY, minW), V3l(1L + maxX - minX, 1L + maxY - minY, 1L + maxW - minW))

    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, y : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        let minZ = match minZ with | Some v -> v | None -> 0L
        let maxZ = match maxZ with | Some v -> v | None -> (info.Size.Z - 1L)
        let minW = match minW with | Some v -> v | None -> 0L
        let maxW = match maxW with | Some v -> v | None -> (info.Size.W - 1L)

        this.SubXZWVolume(y).SubVolume(V3l(minX, minZ, minW), V3l(1L + maxX - minX, 1L + maxZ - minZ, 1L + maxW - minW))

    member this.GetSlice(x : int64, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>) =
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> v | None -> 0L
        let maxZ = match maxZ with | Some v -> v | None -> (info.Size.Z - 1L)
        let minW = match minW with | Some v -> v | None -> 0L
        let maxW = match maxW with | Some v -> v | None -> (info.Size.W - 1L)

        this.SubYZWVolume(x).SubVolume(V3l(minY, minZ, minW), V3l(1L + maxY - minY, 1L + maxZ - minZ, 1L + maxW - minW))

    // TODO: matrix/vector slices


    // int slices
    // int64 slices
    member this.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> int64 v | None -> 0L
        let maxZ = match maxZ with | Some v -> int64 v | None -> (info.Size.Z - 1L)
        let minW = match minW with | Some v -> int64 v | None -> 0L
        let maxW = match maxW with | Some v -> int64 v | None -> (info.Size.W - 1L)
        this.SubTensor4(
            V4l(minX, minY, minZ, minW), 
            V4l(1L + maxX - minX, 1L + maxY - minY, 1L + maxZ - minZ, 1L + maxW - minW), 
            info.Delta
        )

    member this.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, w : int) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> int64 v | None -> 0L
        let maxZ = match maxZ with | Some v -> int64 v | None -> (info.Size.Z - 1L)

        this.SubXYZVolume(int64 w).SubVolume(V3l(minX, minY, minZ), V3l(1L + maxX - minX, 1L + maxY - minY, 1L + maxZ - minZ))

    member this.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, z : int, minW : Option<int>, maxW : Option<int>) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        let minW = match minW with | Some v -> int64 v | None -> 0L
        let maxW = match maxW with | Some v -> int64 v | None -> (info.Size.W - 1L)

        this.SubXYWVolume(int64 z).SubVolume(V3l(minX, minY, minW), V3l(1L + maxX - minX, 1L + maxY - minY, 1L + maxW - minW))

    member this.GetSlice(minX : Option<int>, maxX : Option<int>, y : int, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        let minZ = match minZ with | Some v -> int64 v | None -> 0L
        let maxZ = match maxZ with | Some v -> int64 v | None -> (info.Size.Z - 1L)
        let minW = match minW with | Some v -> int64 v | None -> 0L
        let maxW = match maxW with | Some v -> int64 v | None -> (info.Size.W - 1L)

        this.SubXZWVolume(int64 y).SubVolume(V3l(minX, minZ, minW), V3l(1L + maxX - minX, 1L + maxZ - minZ, 1L + maxW - minW))

    member this.GetSlice(x : int, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>) =
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> int64 v | None -> 0L
        let maxZ = match maxZ with | Some v -> int64 v | None -> (info.Size.Z - 1L)
        let minW = match minW with | Some v -> int64 v | None -> 0L
        let maxW = match maxW with | Some v -> int64 v | None -> (info.Size.W - 1L)

        this.SubYZWVolume(int64 x).SubVolume(V3l(minY, minZ, minW), V3l(1L + maxY - minY, 1L + maxZ - minZ, 1L + maxW - minW))

    // TODO: matrix/vector slices


    member x.CopyTo(dst : NativeTensor4<'a>) =
        if info.Size <> dst.Size then failf "mismatching Tensor4 size"
        x.Mapped (fun src ->
            NativeTensor4.copy src dst
        )

    member x.CopyTo(dst : Tensor4<'a>) =
        if info.Size <> dst.Size then failf "mismatching Tensor4 size"
        x.Mapped (fun src ->
            NativeTensor4.using dst (fun dst ->
                NativeTensor4.copy src dst
            )
        )

    member x.CopyFrom(src : NativeTensor4<'a>) =
        if info.Size <> src.Size then failf "mismatching Tensor4 size"
        x.Mapped (fun dst ->
            NativeTensor4.copy src dst
        )

    member x.CopyFrom(src : Tensor4<'a>) =
        if info.Size <> src.Size then failf "mismatching Tensor4 size"
        x.Mapped (fun dst ->
            NativeTensor4.using src (fun src ->
                NativeTensor4.copy src dst
            )
        )

    member x.Set(v : 'a) =
        if info.Size.AllGreater 0L then
            x.Mapped (fun dst ->
                NativeTensor4.set v dst
            )

[<AbstractClass>]
type TensorImage(buffer : Buffer, info : Tensor4Info, format : PixFormat, imageFormat : VkFormat) =
    member x.Buffer = buffer
    member x.Channels = int info.Size.W
    member x.Size = V3i info.Size.XYZ
    member x.PixFormat = format
    member x.Format = format.Format
    member x.ImageFormat = imageFormat

    abstract member Write<'x when 'x : unmanaged> : NativeMatrix<'x> -> unit
    abstract member Write<'x when 'x : unmanaged> : Col.Format * NativeVolume<'x> -> unit
    abstract member Write<'x when 'x : unmanaged> : Col.Format * NativeTensor4<'x> -> unit
    
    abstract member Read<'x when 'x : unmanaged> : NativeMatrix<'x> -> unit
    abstract member Read<'x when 'x : unmanaged> : Col.Format * NativeVolume<'x> -> unit
    abstract member Read<'x when 'x : unmanaged> : Col.Format * NativeTensor4<'x> -> unit

    abstract member Write : data : nativeint * rowSize : nativeint * format : Col.Format * trafo : ImageTrafo -> unit
    abstract member Read : data : nativeint * rowSize : nativeint * format : Col.Format * trafo : ImageTrafo -> unit

    member x.Write(img : PixImage, beforeRead : ImageTrafo) =
        img.Visit { 
            new PixImageVisitor<int>() with 
                override __.Visit(img : PixImage<'a>, value : 'a) =
                    let img = img.Transformed beforeRead |> unbox<PixImage<'a>>
                    NativeVolume.using img.Volume (fun src ->
                        x.Write(img.Format, src)
                    )
                    1
        } |> ignore

    member x.Read(img : PixImage, beforeWrite : ImageTrafo) =
        img.Visit { 
            new PixImageVisitor<int>() with 
                override __.Visit(img : PixImage<'a>, value : 'a) =
                    let img = img.Transformed beforeWrite |> unbox<PixImage<'a>>
                    NativeVolume.using img.Volume (fun dst ->
                        x.Read(img.Format, dst)
                    )
                    1
        } |> ignore

    member x.Write(img : PixVolume) =
        img.Visit { 
            new PixVolumeVisitor<int>() with 
                override __.Visit(img : PixVolume<'a>, value : 'a) =
                    NativeTensor4.using img.Tensor4 (fun src ->
                        x.Write(img.Format, src)
                    )
                    1
        } |> ignore

    member x.Read(img : PixVolume) =
        img.Visit { 
            new PixVolumeVisitor<int>() with 
                override __.Visit(img : PixVolume<'a>, value : 'a) =
                    NativeTensor4.using img.Tensor4 (fun dst ->
                        x.Read(img.Format, dst)
                    )
                    1
        } |> ignore
        
type TensorImage<'a when 'a : unmanaged> private(buffer : Buffer, info : Tensor4Info, format : Col.Format, imageFormat : VkFormat) =
    inherit TensorImage(buffer, info, PixFormat(typeof<'a>, format), imageFormat)

    static let sa = sizeof<'a> |> int64

    static let rgbFormats =
        HashSet.ofList [
            Col.Format.RGB
            Col.Format.RGBA
            Col.Format.RGBP
        ]

    static let bgrFormats =
        HashSet.ofList [
            Col.Format.BGR
            Col.Format.BGRA
            Col.Format.BGRP
        ]

    static let reverseRGB (srcFormat : Col.Format) (dstFormat : Col.Format) =
        (rgbFormats.Contains srcFormat && bgrFormats.Contains dstFormat) ||
        (rgbFormats.Contains dstFormat && bgrFormats.Contains srcFormat)


    let tensor = DeviceTensor4<'a>(buffer.Memory, info)

    static let defaultValue =
        match typeof<'a> with
            | TypeInfo.Patterns.Byte    -> 255uy |> unbox<'a>
            | TypeInfo.Patterns.SByte   -> 0y |> unbox<'a>
            | TypeInfo.Patterns.UInt16  -> UInt16.MaxValue |> unbox<'a>
            | TypeInfo.Patterns.Int16   -> 0 |> unbox<'a>
            | TypeInfo.Patterns.UInt32  -> UInt32.MaxValue |> unbox<'a>
            | TypeInfo.Patterns.Int32   -> 0 |> unbox<'a>
            | TypeInfo.Patterns.UInt64  -> UInt64.MaxValue |> unbox<'a>
            | TypeInfo.Patterns.Int64   -> 0 |> unbox<'a>
            | TypeInfo.Patterns.Float32 -> 1.0f |> unbox<'a>
            | TypeInfo.Patterns.Float64 -> 1.0 |> unbox<'a>
            | _ -> failf "unsupported channel-type: %A" typeof<'a>

    static let copy (src : NativeTensor4<'a>) (srcFormat : Col.Format) (dst : NativeTensor4<'a>) (dstFormat : Col.Format) =
        let channels = min src.SW dst.SW

        let mutable src = src
        let mutable dst = dst

        if src.Size.XYZ <> dst.Size.XYZ then
            let s = V3l(min src.SX dst.SX, min src.SY dst.SY, min src.SZ dst.SZ)
            src <- src.SubTensor4(V4l.Zero, V4l(s, src.SW))
            dst <- dst.SubTensor4(V4l.Zero, V4l(s, dst.SW))

        if reverseRGB srcFormat dstFormat then
            let src3 = src.[*,*,*,0..2].MirrorW()
            let dst3 = dst.[*,*,*,0..2]
            NativeTensor4.copy src3 dst3

            if channels > 3L then
                 NativeTensor4.copy src.[*,*,*,3..] dst.[*,*,*,3..]

            if dst.SW > channels then
                NativeTensor4.set defaultValue dst.[*,*,*,channels..]
        else
            // copy all available channels
            NativeTensor4.copy src dst.[*,*,*,0L..channels-1L]
            
            // set the missing channels to default
            if dst.SW > channels then
                NativeTensor4.set defaultValue dst.[*,*,*,channels..]

    override x.Write(data : nativeint, rowSize : nativeint, format : Col.Format, trafo : ImageTrafo) =
        let rowSize = int64 rowSize
        let channels = format.Channels

        if rowSize % sa <> 0L then failf "non-aligned row-size"
        let dy = rowSize / sa

        let srcInfo =
            VolumeInfo(
                0L,
                V3l(info.SX, info.SY, int64 channels),
                V3l(int64 channels, dy, 1L)
            )

        let srcInfo =
            srcInfo.Transformed(trafo)

        let src = NativeVolume<'a>(NativePtr.ofNativeInt data, srcInfo)
        x.Write(format, src)

    override x.Read(data : nativeint, rowSize : nativeint, format : Col.Format, trafo : ImageTrafo) =
        let rowSize = int64 rowSize
        let channels = format.Channels

        if rowSize % sa <> 0L then failf "non-aligned row-size"
        let dy = rowSize / sa

        let dstInfo =
            VolumeInfo(
                0L,
                V3l(info.SY, info.SY, int64 channels),
                V3l(int64 channels, dy, 1L)
            )

        let dstInfo =
            dstInfo.Transformed(trafo)

        let dst = NativeVolume<'a>(NativePtr.ofNativeInt data, dstInfo)
        x.Read(format, dst)

    override x.Write(matrix : NativeMatrix<'x>) : unit =
        if typeof<'a> = typeof<'x> then
            let src = unbox<NativeMatrix<'a>> matrix
            let dst = tensor.[*,*,*,0].[*,*,0]
            dst.CopyFrom src
            
            let rest = tensor.[*,*,1..,*]
            rest.Set defaultValue

            let rest = tensor.[*,*,0,1..]
            rest.Set defaultValue
        else
            failf "mismatching types is upload"

    override x.Write(fmt : Col.Format, volume : NativeVolume<'x>) =
        if typeof<'a> = typeof<'x> then
            let src = unbox<NativeVolume<'a>> volume
            
            let srcTensor = src.ToXYWTensor4()
            tensor.Mapped (fun dst ->
                copy srcTensor fmt dst format
            )
        else
            failf "mismatching types is upload"

    override x.Write(fmt : Col.Format, t : NativeTensor4<'x>) =
        if typeof<'a> = typeof<'x> then
            let src = unbox<NativeTensor4<'a>> t
            tensor.Mapped (fun dst ->
                copy src fmt dst format
            )

        else
            failf "mismatching types is upload"

    override x.Read(dst : NativeMatrix<'x>) : unit =
        if typeof<'a> = typeof<'x> then
            let dst = unbox<NativeMatrix<'a>> dst
            let src = tensor.[*,*,*,0].[*,*,0]
            src.CopyTo dst
        else
            failf "mismatching types is download"

    override x.Read(fmt : Col.Format, dst : NativeVolume<'x>) : unit =
        if typeof<'a> = typeof<'x> then
            let dst = unbox<NativeVolume<'a>> dst
            let dstTensor = dst.ToXYWTensor4()
            tensor.Mapped (fun src ->
                copy src format dstTensor fmt
            )

        else
            failf "mismatching types is download"

    override x.Read(fmt : Col.Format, dst : NativeTensor4<'x>) : unit =
        if typeof<'a> = typeof<'x> then
            let dst = unbox<NativeTensor4<'a>> dst
            tensor.Mapped (fun src ->
                copy src format dst fmt
            )
        else
            failf "mismatching types is download"

    member x.Buffer = buffer
    member x.Size = V3i info.Size.XYZ
    member x.Format = format
    member x.Channels = int info.SW
        
    member x.Tensor4 =
        tensor

    member x.Volume =
        if info.SZ <> 1L then failf "3d image cannot be interpreted as 2d image"
        else tensor.[*,*,0,*]

    member x.Matrix =
        if info.SZ <> 1L then failf "3d image cannot be interpreted as 2d matrix"
        elif info.SW <> 1L then failf "2d image with more than one channel cannot be interpreted as 2d matrix"
        else tensor.[*,*,*,0].[*,*,0]
            
    member x.Vector =
        if info.SZ <> 1L then failf "3d image cannot be interpreted as vector"
        elif info.SY <> 1L then failf "2d image cannot be interpreted as vector"
        elif info.SW <> 1L then failf "1d image with more than one channel cannot be interpreted as vector"
        else tensor.[*,*,*,0].[*,0,0]

    internal new(buffer : Buffer, size : V3i, format : Col.Format, imageFormat : VkFormat) =
        let channels = format.Channels
        let s = V4l(size.X, size.Y, size.Z, channels)
        let info =
            Tensor4Info(
                0L,
                s,
                V4l(s.W, s.W * s.X, s.W * s.X * s.Y, 1L)
            )
        TensorImage<'a>(buffer, info, format, imageFormat)
 
type TensorImageMipMap(images : TensorImage[]) =
    member x.LevelCount = images.Length
    member x.ImageArray = images
    member x.Format = images.[0].PixFormat

type TensorImageCube(faces : TensorImageMipMap[]) =
    do assert(faces.Length = 6)
    member x.MipMapArray = faces

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TensorImage =
    let create<'a when 'a : unmanaged> (size : V3i) (format : Col.Format) (srgb : bool) (device : Device) : TensorImage<'a> =
        let imageFormat = device.GetSupportedFormat(VkImageTiling.Optimal, PixFormat(typeof<'a>, format), { TextureParams.empty with wantSrgb = srgb })
        let format = PixFormat(VkFormat.expectedType imageFormat, VkFormat.toColFormat imageFormat)

        if format.Type <> typeof<'a> then
            failf "device does not support images of type %s" typeof<'a>.PrettyName

        let channels = format.Format.Channels
        let sizeInBytes = int64 size.X * int64 size.Y * int64 size.Z * int64 channels * int64 sizeof<'a>
        let buffer = device.HostMemory |> Buffer.create (VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.TransferSrcBit) sizeInBytes
        TensorImage<'a>(buffer, size, format.Format, imageFormat)

    let inline private erase (creator : V3i -> Col.Format -> bool -> Device-> TensorImage<'a>) (size : V3i) (format : Col.Format) (tp : bool) (device : Device) = creator size format tp device :> TensorImage

    let private creators =
        Dictionary.ofList [
            typeof<uint8>, erase create<uint8>
            typeof<int8>, erase create<int8>
            typeof<uint16>, erase create<uint16>
            typeof<int16>, erase create<int16>
            typeof<uint32>, erase create<uint32>
            typeof<int32>, erase create<int32>
            typeof<uint64>, erase create<uint64>
            typeof<int64>, erase create<int64>
            typeof<float16>, erase create<float16>
            typeof<float32>, erase create<float32>
            typeof<float>, erase create<float>
            // TODO: any others?
        ]

    let createUntyped (size : V3i) (format : PixFormat) (srgb : bool) (device : Device) =
        creators.[format.Type] size format.Format srgb device

    let ofPixImage (img : PixImage) (srgb : bool) (device : Device) =
        let dst = createUntyped (V3i(img.Size.X, img.Size.Y, 1)) img.PixFormat srgb device
        dst.Write(img, ImageTrafo.MirrorY)
        dst

    let ofPixVolume (img : PixVolume) (srgb : bool) (device : Device) =
        let dst = createUntyped (V3i(img.Size.X, img.Size.Y, 1)) img.PixFormat srgb device
        dst.Write(img)
        dst

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

    module TensorImage =
        let ofFile (file : string) (srgb : bool) (device : Device) =
            // let img = PixImage.Create file
            // TensorImage.ofPixImage img srgb device
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
                    let rowSize = nativeint bytesPerPixel * nativeint width
                    
                    let target = device |> TensorImage.createUntyped (V3i(width, height, 1)) pixFormat srgb
                    target.Write(data, rowSize, pixFormat.Format, ImageTrafo.MirrorY)

                    target
                finally
                    IL.BindImage(0)
                    IL.DeleteImage(img)
            )


[<AbstractClass; Sealed; Extension>]
type DeviceTensorExtensions private() =

    [<Extension>]
    static member inline CreateTensorImage<'a when 'a : unmanaged>(device : Device, size : V3i, format : Col.Format, srgb : bool) : TensorImage<'a> =
        TensorImage.create size format srgb device
        
    [<Extension>]
    static member inline CreateTensorImage(device : Device, size : V3i, format : PixFormat, srgb : bool) : TensorImage =
        TensorImage.createUntyped size format srgb device
        
    [<Extension>]
    static member inline Create(device : Device, data : PixImage, srgb : bool) : TensorImage =
        device |> TensorImage.ofPixImage data srgb
        
    [<Extension>]
    static member inline Create(device : Device, data : PixImageMipMap, levels : int, srgb : bool) : TensorImageMipMap =
        TensorImageMipMap(
            data.ImageArray |> Array.take levels |> Array.map (fun l -> TensorImage.ofPixImage l srgb device)
        )

    [<Extension>]
    static member inline Create(device : Device, data : PixImageCube, levels : int, srgb : bool) : TensorImageCube =
        TensorImageCube(
            data.MipMapArray |> Array.map (fun face ->
                DeviceTensorExtensions.Create(device, face, levels, srgb)
            )
        )

    [<Extension>]
    static member inline Create(device : Device, data : PixImageMipMap, srgb : bool) =
        DeviceTensorExtensions.Create(device, data, data.LevelCount, srgb)

    [<Extension>]
    static member inline Create(device : Device, data : PixImageCube, srgb : bool) =
        DeviceTensorExtensions.Create(device, data, data.MipMapArray.[0].LevelCount, srgb)

    [<Extension>]
    static member inline Create(device : Device, data : PixVolume, srgb : bool) : TensorImage =
        device |> TensorImage.ofPixVolume data srgb

    [<Extension>]
    static member Delete(device : Device, m : TensorImage) =
        device.Delete m.Buffer

    [<Extension>]
    static member Delete(device : Device, m : TensorImageMipMap) =
        for i in m.ImageArray do DeviceTensorExtensions.Delete (device, i)
        
    [<Extension>]
    static member Delete(device : Device, m : TensorImageCube) =
        for i in m.MipMapArray do DeviceTensorExtensions.Delete (device, i)

[<AutoOpen>]
module DeviceTensorCommandExtensions =
    type Command with
        // upload
        static member Copy(src : TensorImage, dst : ImageSubresource, dstOffset : V3i, size : V3i) =
            if dst.Aspect <> ImageAspect.Color then
                failf "[TensorImage] cannot copy to aspect %A" dst.Aspect

            if dstOffset.AnySmaller 0 || dst.Size.AnySmaller(dstOffset + size) then
                failf "[TensorImage] target region out of bounds"

            if src.Size.AnySmaller size then 
                failf "[TensorImage] insufficient size %A" src.Size

            let dstElementType = VkFormat.expectedType dst.Image.Format 
            let dstSizeInBytes = VkFormat.sizeInBytes dst.Image.Format 

            if isNull dstElementType || dstSizeInBytes < 0 then 
                failf "[TensorImage] format %A has no CPU representation" dst.Image.Format

            let dstChannels = dstSizeInBytes / Marshal.SizeOf dstElementType
            if dstChannels <> src.Channels then
                failf "[TensorImage] got '%d * %s' but expected '%d * %s'" src.Channels src.PixFormat.Type.PrettyName dstChannels dstElementType.PrettyName

            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let mutable copy =
                        VkBufferImageCopy(
                            0UL,
                            0u,
                            0u,
                            dst.VkImageSubresourceLayers,
                            VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )
                        
                    cmd.AppendCommand()
                    VkRaw.vkCmdCopyBufferToImage(cmd.Handle, src.Buffer.Handle, dst.Image.Handle, dst.Image.Layout, 1u, &&copy)
                    Disposable.Empty
            }

        static member Copy(src : TensorImage, dst : ImageSubresource) =
            if src.Size <> dst.Size then failf "[TensorImage] mismatching sizes in copy %A vs %A" src.Size dst.Size
            Command.Copy(src, dst, V3i.Zero, src.Size)

        // download
        static member Copy(src : ImageSubresource, srcOffset : V3i, dst : TensorImage, size : V3i) =
            if src.Aspect <> ImageAspect.Color then
                failf "[TensorImage] cannot copy from aspect %A" src.Aspect

            if srcOffset.AnySmaller 0 || src.Size.AnySmaller(srcOffset + size) then
                failf "[TensorImage] source region out of bounds"

            if dst.Size.AnySmaller size then 
                failf "[TensorImage] insufficient size %A" src.Size

            let srcElementType = VkFormat.expectedType src.Image.Format 
            let srcSizeInBytes = VkFormat.sizeInBytes src.Image.Format 

            if isNull srcElementType || srcSizeInBytes < 0 then 
                failf "[TensorImage] format %A has no CPU representation" src.Image.Format

            let srcChannels = srcSizeInBytes / Marshal.SizeOf srcElementType
            if srcChannels <> dst.Channels then
                failf "[TensorImage] got '%d * %s' but expected '%d * %s'" srcChannels srcElementType.PrettyName dst.Channels dst.PixFormat.Type.PrettyName

            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let mutable copy =
                        VkBufferImageCopy(
                            0UL,
                            0u,
                            0u,
                            src.VkImageSubresourceLayers,
                            VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )
                        
                    cmd.AppendCommand()
                    VkRaw.vkCmdCopyImageToBuffer(cmd.Handle, src.Image.Handle, src.Image.Layout, dst.Buffer.Handle, 1u, &&copy)
                    Disposable.Empty
            }

        static member Copy(src : ImageSubresource, dst : TensorImage) =
            if src.Size <> dst.Size then failf "[TensorImage] mismatching sizes in copy %A vs %A" src.Size dst.Size
            Command.Copy(src, V3i.Zero, dst, src.Size)

    module private MustCompile =

        let createImage (device : Device) =
            let img = device.CreateTensorImage<byte>(V3i.III, Col.Format.RGBA, false)

            let v = img.Vector
            let m = img.Matrix
            let v = img.Volume
            let t = img.Tensor4
            
            device.Delete img
            ()

        let testVec (v : DeviceVector<int>) =
            let a = v.[1L..]
            let b = v.[..10L]

            let x = v.[1..]
            let y = v.[..10]
        
            ()

        let testMat (m : DeviceMatrix<int>) =
            let a = m.[1L,*]
            let b = m.[*,2L]
            let c = m.[1L.., 1L..]
        
            let x = m.[1,*]
            let y = m.[*,2]
            let z = m.[1.., 1..]
        
            ()

        let testVol (v : DeviceVolume<int>) =
            let a = v.[1L,*,*]
            let b = v.[*,2L,*]
            let c = v.[*,*,3L]

            let a = v.[1L,1L,*]
            let b = v.[1L,*,1L]
            let c = v.[*,1L,1L]

            let a = v.[1L..,1L..,1L..]


        
            let a = v.[1,*,*]
            let b = v.[*,2,*]
            let c = v.[*,*,3]

            let a = v.[1,1,*]
            let b = v.[1,*,1]
            let c = v.[*,1,1]

            let a = v.[1..,1..,1..]
        
            ()

        
        let testTensor4 (t : DeviceTensor4<int>) =
            let a = t.[1L,*,*,*]
            let b = t.[*,2L,*,*]
            let c = t.[*,*,3L,*]
            let d = t.[*,*,*,4L]

            
            // t.[1L,*,*,4L]
            // t.[1L,2L,*,4L]

            let a = t.[1L..,1L..,1L..,1L..]

            let a = t.[1,*,*,*]
            let b = t.[*,2,*,*]
            let c = t.[*,*,3,*]
            let d = t.[*,*,*,4]
        
            let a = t.[1..,1..,1..,1..]

        
            ()



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

            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let srcLayout = src.Image.Layout
                    let dstLayout = dst.Image.Layout
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
                    Disposable.Empty
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


        static member Copy(src : Buffer, srcOffset : int64, srcStride : V2i, dst : ImageSubresourceLayers, dstOffset : V3i, size : V3i) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let mutable copy =
                        VkBufferImageCopy(
                            uint64 srcOffset,
                            uint32 srcStride.X,
                            uint32 srcStride.Y,
                            dst.VkImageSubresourceLayers,
                            VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )
                        
                    cmd.AppendCommand()
                    VkRaw.vkCmdCopyBufferToImage(cmd.Handle, src.Handle, dst.Image.Handle, dst.Image.Layout, 1u, &&copy)
                    Disposable.Empty
            }

        static member Copy(src : ImageSubresourceLayers, srcOffset : V3i, dst : Buffer, dstOffset : int64, dstStride : V2i, size : V3i) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let mutable copy =
                        VkBufferImageCopy(
                            uint64 dstOffset,
                            uint32 dstStride.X,
                            uint32 dstStride.Y,
                            src.VkImageSubresourceLayers,
                            VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )
                        
                    cmd.AppendCommand()
                    VkRaw.vkCmdCopyImageToBuffer(cmd.Handle, src.Image.Handle, src.Image.Layout, dst.Handle, 1u, &&copy)
                    Disposable.Empty
            }

        static member ResolveMultisamples(src : ImageSubresourceLayers, srcOffset : V3i, dst : ImageSubresourceLayers, dstOffset : V3i, size : V3i) =
            if src.SliceCount <> dst.SliceCount then
                failf "cannot resolve image: { srcSlices = %A; dstSlices = %A }" src.SliceCount dst.SliceCount
                
            { new Command() with
                member x.Compatible = QueueFlags.Graphics
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
            { new Command() with
                member x.Compatible = QueueFlags.Graphics
                member x.Enqueue cmd =
                    let srcLayout = src.Image.Layout
                    let dstLayout = dst.Image.Layout

                    let mutable srcOffsets = VkOffset3D_2()

                    let inline extend (b : Box3i) =
                        let mutable min = b.Min
                        let mutable max = b.Max

                        if max.X >= min.X then max.X <- 1 + max.X
                        else min.X <- min.X + 1

                        if max.Y >= min.Y then max.Y <- 1 + max.Y
                        else min.Y <- min.Y + 1

                        if max.Z >= min.Z then max.Z <- 1 + max.Z
                        else min.Z <- min.Z + 1

                        Box3i(min, max)
                         
                    let srcRange = extend srcRange
                    let dstRange = extend dstRange

                    srcOffsets.[0] <- VkOffset3D(srcRange.Min.X, srcRange.Min.Y, srcRange.Min.Z)
                    srcOffsets.[1] <- VkOffset3D(srcRange.Max.X, srcRange.Max.Y, srcRange.Max.Z)

                    let mutable dstOffsets = VkOffset3D_2()
                    dstOffsets.[0] <- VkOffset3D(dstRange.Min.X, dstRange.Min.Y, dstRange.Min.Z)
                    dstOffsets.[1] <- VkOffset3D(dstRange.Max.X, dstRange.Max.Y, dstRange.Max.Z)


                    let mutable blit =
                        VkImageBlit(
                            src.VkImageSubresourceLayers,
                            srcOffsets,
                            dst.VkImageSubresourceLayers,
                            dstOffsets
                        )
                    
                    cmd.AppendCommand()
                    VkRaw.vkCmdBlitImage(cmd.Handle, src.Image.Handle, src.Image.Layout, dst.Image.Handle, dst.Image.Layout, 1u, &&blit, filter)
                    Disposable.Empty
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
                    member x.Compatible = QueueFlags.Graphics ||| QueueFlags.Compute
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
                    member x.Compatible = QueueFlags.Graphics
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
                    do! Command.TransformLayout(img.[0,*], oldLayout, VkImageLayout.TransferSrcOptimal)

                    for l in 1 .. img.LevelCount - 1 do
                        do! Command.TransformLayout(img.[l,*], oldLayout, VkImageLayout.TransferDstOptimal)
                        do! Command.Blit(img.[l - 1, *], img.[l, *], VkFilter.Linear)
                        do! Command.TransformLayout(img.[l,*], VkImageLayout.TransferDstOptimal, VkImageLayout.TransferSrcOptimal)

                    do! Command.TransformLayout(img, VkImageLayout.TransferSrcOptimal, oldLayout)
                }

        static member TransformLayout(img : ImageSubresourceRange, source : VkImageLayout, target : VkImageLayout) =
            if img.Image.IsNull || target = VkImageLayout.Undefined || target = VkImageLayout.Preinitialized then
                Command.Nop
            else
                { new Command() with
                    member x.Compatible = QueueFlags.All
                    member x.Enqueue (cmd : CommandBuffer) =
                        let src =
                            if source = VkImageLayout.ColorAttachmentOptimal then VkAccessFlags.ColorAttachmentWriteBit
                            elif source = VkImageLayout.DepthStencilAttachmentOptimal then VkAccessFlags.DepthStencilAttachmentWriteBit
                            elif source = VkImageLayout.TransferDstOptimal then VkAccessFlags.TransferWriteBit
                            elif source = VkImageLayout.PresentSrcKhr then VkAccessFlags.MemoryReadBit
                            elif source = VkImageLayout.Preinitialized then VkAccessFlags.HostWriteBit
                            elif source = VkImageLayout.TransferSrcOptimal then VkAccessFlags.TransferReadBit
                            elif source = VkImageLayout.ShaderReadOnlyOptimal then VkAccessFlags.ShaderReadBit // ||| VkAccessFlags.InputAttachmentReadBit
                            else VkAccessFlags.None

                        let dst =
                            if target = VkImageLayout.TransferSrcOptimal then VkAccessFlags.TransferReadBit
                            elif target = VkImageLayout.TransferDstOptimal then VkAccessFlags.TransferWriteBit
                            elif target = VkImageLayout.ColorAttachmentOptimal then VkAccessFlags.ColorAttachmentWriteBit
                            elif target = VkImageLayout.DepthStencilAttachmentOptimal then VkAccessFlags.DepthStencilAttachmentWriteBit
                            elif target = VkImageLayout.ShaderReadOnlyOptimal then VkAccessFlags.ShaderReadBit // ||| VkAccessFlags.InputAttachmentReadBit
                            elif target = VkImageLayout.PresentSrcKhr then VkAccessFlags.MemoryReadBit
                            elif target = VkImageLayout.General then VkAccessFlags.None
                            else VkAccessFlags.None

                        let srcMask =
                            if source = VkImageLayout.ColorAttachmentOptimal then VkPipelineStageFlags.ColorAttachmentOutputBit
                            elif source = VkImageLayout.DepthStencilAttachmentOptimal then VkPipelineStageFlags.LateFragmentTestsBit
                            elif source = VkImageLayout.TransferDstOptimal then VkPipelineStageFlags.TransferBit
                            elif source = VkImageLayout.PresentSrcKhr then VkPipelineStageFlags.TransferBit
                            elif source = VkImageLayout.Preinitialized then VkPipelineStageFlags.HostBit
                            elif source = VkImageLayout.TransferSrcOptimal then VkPipelineStageFlags.TransferBit
                            elif source = VkImageLayout.ShaderReadOnlyOptimal then 
                                if cmd.QueueFamily.Flags &&& QueueFlags.Graphics <> QueueFlags.None then VkPipelineStageFlags.FragmentShaderBit
                                else VkPipelineStageFlags.ComputeShaderBit
                            elif source = VkImageLayout.Undefined then VkPipelineStageFlags.HostBit // VK_PIPELINE_STAGE_FLAGS_HOST_BIT
                            elif source = VkImageLayout.General then VkPipelineStageFlags.HostBit
                            else VkPipelineStageFlags.None

                        let dstMask =
                            if target = VkImageLayout.TransferSrcOptimal then VkPipelineStageFlags.TransferBit
                            elif target = VkImageLayout.TransferDstOptimal then VkPipelineStageFlags.TransferBit
                            elif target = VkImageLayout.ColorAttachmentOptimal then VkPipelineStageFlags.ColorAttachmentOutputBit
                            elif target = VkImageLayout.DepthStencilAttachmentOptimal then VkPipelineStageFlags.EarlyFragmentTestsBit
                            elif target = VkImageLayout.ShaderReadOnlyOptimal then 
                                if cmd.QueueFamily.Flags &&& QueueFlags.Graphics <> QueueFlags.None then VkPipelineStageFlags.VertexShaderBit
                                else VkPipelineStageFlags.ComputeShaderBit

                            elif target = VkImageLayout.PresentSrcKhr then VkPipelineStageFlags.TransferBit
                            elif target = VkImageLayout.General then VkPipelineStageFlags.HostBit


                            else VkPipelineStageFlags.None

                        let mutable barrier =
                            VkImageMemoryBarrier(
                                VkStructureType.ImageMemoryBarrier, 0n, 
                                src,
                                dst,
                                source,
                                target,
                                VK_QUEUE_FAMILY_IGNORED,
                                VK_QUEUE_FAMILY_IGNORED,
                                img.Image.Handle,
                                img.VkImageSubresourceRange
                            )

                        cmd.AppendCommand()
                        VkRaw.vkCmdPipelineBarrier(
                            cmd.Handle,
                            srcMask,
                            dstMask,
                            VkDependencyFlags.None,
                            0u, NativePtr.zero,
                            0u, NativePtr.zero,
                            1u, &&barrier
                        )
                        Disposable.Empty
                }

        static member TransformLayout(img : Image, target : VkImageLayout) =
            if img.IsNull || target = VkImageLayout.Undefined || target = VkImageLayout.Preinitialized then
                Command.Nop
            else
                { new Command() with
                    member x.Compatible = QueueFlags.All
                    member x.Enqueue (cmd : CommandBuffer) =
                        if img.Layout = target then
                            Disposable.Empty
                        else
                            let source = img.Layout
                            img.Layout <- target
                            let aspect = VkFormat.toAspect img.Format
                            Command.TransformLayout(img.[unbox (int aspect)], source, target).Enqueue(cmd)
                }
//
//        static member Sync(img : ImageSubresourceRange) =
//            if img.Image.IsNull then
//                Command.Nop
//            else
//                { new Command() with
//                    member x.Compatible = QueueFlags.All
//                    member x.Enqueue (cmd : CommandBuffer) =
//                        let layout = img.Image.Layout
//                        let mutable barrier =
//                            VkImageMemoryBarrier(
//                                VkStructureType.ImageMemoryBarrier, 0n, 
//                                VkAccessFlags.Write,
//                                VkAccessFlags.Read,
//                                layout,
//                                layout,
//                                VK_QUEUE_FAMILY_IGNORED,
//                                VK_QUEUE_FAMILY_IGNORED,
//                                img.Image.Handle,
//                                img.VkImageSubresourceRange
//                            )
//                            
//                        cmd.AppendCommand()
//                        VkRaw.vkCmdPipelineBarrier(
//                            cmd.Handle,
//                            VkPipelineStageFlags.TopOfPipeBit,
//                            VkPipelineStageFlags.TopOfPipeBit,
//                            VkDependencyFlags.None,
//                            0u, NativePtr.zero,
//                            0u, NativePtr.zero,
//                            1u, &&barrier
//                        )
//                        Disposable.Empty
//                }
//
//


// ===========================================================================================
// Image functions
// ===========================================================================================
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Image =

    let alloc (size : V3i) (mipMapLevels : int) (count : int) (samples : int) (dim : TextureDimension) (fmt : VkFormat) (usage : VkImageUsageFlags) (device : Device) =
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
        let ptr = device.Alloc(VkMemoryRequirements(uint64 memsize, uint64 memalign, reqs.memoryTypeBits), true)

        VkRaw.vkBindImageMemory(device.Handle, handle, ptr.Memory.Handle, uint64 ptr.Offset)
            |> check "could not bind image memory"

        let result = Image(device, handle, size, mipMapLevels, count, samples, dim, fmt, ptr, VkImageLayout.Undefined)
        result

    let delete (img : Image) (device : Device) =
        if Interlocked.Decrement(&img.RefCount) = 0 then
            if device.Handle <> 0n && img.Handle.IsValid then
                VkRaw.vkDestroyImage(img.Device.Handle, img.Handle, NativePtr.zero)
                img.Memory.Dispose()
                img.Handle <- VkImage.Null

    let create (size : V3i) (mipMapLevels : int) (count : int) (samples : int) (dim : TextureDimension) (fmt : TextureFormat) (usage : VkImageUsageFlags) (device : Device) =
        let vkfmt = VkFormat.ofTextureFormat fmt
        alloc size mipMapLevels count samples dim vkfmt usage device

    let ofPixImageMipMap (pi : PixImageMipMap) (info : TextureParams) (device : Device) =
        if pi.LevelCount <= 0 then failf "empty PixImageMipMap"
        
        let format = pi.ImageArray.[0].PixFormat
        let size = pi.ImageArray.[0].Size

        let format = device.GetSupportedFormat(VkImageTiling.Optimal, format, info)
        let textureFormat = VkFormat.toTextureFormat format
        let expectedFormat = PixFormat(VkFormat.expectedType format, VkFormat.toColFormat format)

        let uploadLevels =
            if info.wantMipMaps then pi.LevelCount
            else 1

        let mipMapLevels =
            if info.wantMipMaps then
                if pi.LevelCount > 1 then pi.LevelCount
                else 1 + max size.X size.Y |> Fun.Log2 |> floor |> int 
            else
                1

        let generateMipMaps =
            uploadLevels < mipMapLevels

        //create (size : V3i) (mipMapLevels : int) (count : int) (samples : int) (dim : TextureDimension) (fmt : TextureFormat) (usage : VkImageUsageFlags) (device : Device) =
        let image = 
            create 
                (V3i(size.X, size.Y, 1)) 
                mipMapLevels 1 1 
                TextureDimension.Texture2D 
                textureFormat 
                (VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit)
                device

        
        device.eventually {
            let tempImages = List()
            try
                do! Command.TransformLayout(image, VkImageLayout.TransferDstOptimal)

                // upload the levels
                for level in 0 .. uploadLevels - 1 do
                    let data = pi.ImageArray.[level]
                    let temp = device.CreateTensorImage(V3i(data.Size.X, data.Size.Y, 1), expectedFormat, info.wantSrgb)
                    temp.Write(data, ImageTrafo.MirrorY)
                    tempImages.Add temp
                    do! Command.Copy(temp, image.[ImageAspect.Color, level, 0])

                // generate the mipMaps
                if generateMipMaps then
                    do! Command.GenerateMipMaps image.[ImageAspect.Color]

                do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)

            finally
                for t in tempImages do device.Delete t
        }

        image

    let ofPixVolume (pi : PixVolume) (info : TextureParams) (device : Device) =
        let format = pi.PixFormat
        let size = pi.Size

        let format = device.GetSupportedFormat(VkImageTiling.Optimal, format, info)
        let textureFormat = VkFormat.toTextureFormat format
        let expectedFormat = PixFormat(VkFormat.expectedType format, VkFormat.toColFormat format)

        
        let mipMapLevels =
            if info.wantMipMaps then
                1 + (max (max size.X size.Y) size.Z) |> Fun.Log2 |> floor |> int 
            else
                1


        let image = 
            create 
                size
                mipMapLevels 1 1 
                TextureDimension.Texture3D 
                textureFormat 
                (VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit)
                device

        
        device.eventually {
            let tempImages = List()
            try
                do! Command.TransformLayout(image, VkImageLayout.TransferDstOptimal)

                // upload the level 0
                let temp = device.CreateTensorImage(pi.Size, expectedFormat, info.wantSrgb)
                temp.Write(pi)
                tempImages.Add temp
                do! Command.Copy(temp, image.[ImageAspect.Color, 0, 0])

                // generate the mipMaps
                if info.wantMipMaps then
                    do! Command.GenerateMipMaps image.[ImageAspect.Color]

                do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)

            finally
                for t in tempImages do device.Delete t
        }

        image

    let ofPixImageCube (pi : PixImageCube) (info : TextureParams) (device : Device) =
        let face0 = pi.MipMapArray.[0]
        if face0.LevelCount <= 0 then failf "empty PixImageMipMap"
        
        let format = face0.ImageArray.[0].PixFormat
        let size = face0.ImageArray.[0].Size

        let format = device.GetSupportedFormat(VkImageTiling.Optimal, format, info)
        let textureFormat = VkFormat.toTextureFormat format

        let expectedFormat = PixFormat(VkFormat.expectedType format, VkFormat.toColFormat format)

        let uploadLevels =
            if info.wantMipMaps then face0.LevelCount
            else 1

        let mipMapLevels =
            if info.wantMipMaps then
                if face0.LevelCount > 1 then face0.LevelCount
                else 1 + max size.X size.Y |> Fun.Log2 |> floor |> int 
            else
                1

        let generateMipMaps =
            uploadLevels < mipMapLevels

        //create (size : V3i) (mipMapLevels : int) (count : int) (samples : int) (dim : TextureDimension) (fmt : TextureFormat) (usage : VkImageUsageFlags) (device : Device) =
        let image = 
            create 
                (V3i(size.X, size.Y, 1)) 
                mipMapLevels 6 1 
                TextureDimension.TextureCube 
                textureFormat 
                (VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit)
                device

        
        device.eventually {
            let tempImages = List()
            try
                do! Command.TransformLayout(image, VkImageLayout.TransferDstOptimal)

                // upload the levels
                for level in 0 .. uploadLevels - 1 do
                    for face in 0 .. 5 do
                        let data = pi.MipMapArray.[face].ImageArray.[level]
                        let temp = device.CreateTensorImage(V3i(data.Size.X, data.Size.Y, 1), expectedFormat, info.wantSrgb)
                        temp.Write(data, ImageTrafo.MirrorY)
                        tempImages.Add temp
                        do! Command.Copy(temp, image.[ImageAspect.Color, level, face])

                // generate the mipMaps
                if generateMipMaps then
                    do! Command.GenerateMipMaps image.[ImageAspect.Color]

                do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)

            finally
                for t in tempImages do device.Delete t
        }

        image

    let ofFile (file : string) (info : TextureParams) (device : Device) =
        if not (System.IO.File.Exists file) then failf "file does not exists: %A" file

        let temp = device |> TensorImage.ofFile file info.wantSrgb
        let size = temp.Size
        let textureFormat = VkFormat.toTextureFormat temp.ImageFormat


        let mipMapLevels =
            if info.wantMipMaps then
                1 + max size.X size.Y |> Fun.Log2 |> floor |> int 
            else
                1

        let image = 
            create 
                size
                mipMapLevels 1 1 
                TextureDimension.Texture2D 
                textureFormat 
                (VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit)
                device
        
        device.eventually {
            try
                do! Command.TransformLayout(image, VkImageLayout.TransferDstOptimal)

                // upload the levels
                do! Command.Copy(temp, image.[ImageAspect.Color, 0, 0])

                // generate the mipMaps
                if info.wantMipMaps then
                    do! Command.GenerateMipMaps image.[ImageAspect.Color]

                do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)

            finally
                device.Delete temp
        }
        image

    let ofTexture (t : ITexture) (device : Device) =
        match t with
            | :? PixTexture2d as t ->
                device |> ofPixImageMipMap t.PixImageMipMap t.TextureParams

            | :? PixTextureCube as c ->
                device |> ofPixImageCube c.PixImageCube c.TextureParams

            | :? NullTexture as t ->
                device |> ofPixImageMipMap (PixImageMipMap [| PixImage<byte>(Col.Format.RGBA, V2i.II) :> PixImage |]) TextureParams.empty
                //Image(device, VkImage.Null, V3i.Zero, 1, 1, 1, TextureDimension.Texture2D, VkFormat.Undefined, DevicePtr.Null, VkImageLayout.ShaderReadOnlyOptimal)

            | :? PixTexture3d as t ->
                device |> ofPixVolume t.PixVolume t.TextureParams

            | :? FileTexture as t ->
                device |> ofFile t.FileName t.TextureParams

            | :? INativeTexture as nt ->
                failf "please implement INativeTexture upload"

            | :? BitmapTexture as bt ->
                failf "BitmapTexture considered obsolete"

            | :? Image as t ->
                t.AddReference()
                t

            | _ ->
                failf "unsupported texture-type: %A" t

    let downloadLevel (src : ImageSubresource) (dst : PixImage) (device : Device) =
        let format = src.Image.Format
        let sourcePixFormat = PixFormat(VkFormat.expectedType format, VkFormat.toColFormat format)

        let temp = device.CreateTensorImage(V3i(dst.Size.X, dst.Size.Y, 1), sourcePixFormat, false)
        try
            device.GraphicsFamily.run {
                let layout = src.Image.Layout
                do! Command.TransformLayout(src.Image, VkImageLayout.TransferSrcOptimal)
                do! Command.Copy(src, temp)
                do! Command.TransformLayout(src.Image, layout)
            }
            temp.Read(dst, ImageTrafo.MirrorY)
        finally
            device.Delete temp

    let uploadLevel (src : PixImage) (dst : ImageSubresource) (device : Device) =
        let format = dst.Image.Format
        let dstPixFormat = PixFormat(VkFormat.expectedType format, VkFormat.toColFormat format)
        
        let temp = device.CreateTensorImage(V3i(dst.Size.X, dst.Size.Y, 1), dstPixFormat, false)
        temp.Write(src, ImageTrafo.MirrorY)
        let layout = dst.Image.Layout
        device.eventually {
            try
                do! Command.TransformLayout(dst.Image, VkImageLayout.TransferDstOptimal)
                do! Command.Copy(temp, dst)
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

[<AutoOpen>]
module private ImageRanges =
    
    module ImageAspect =
        let ofTextureAspect =
            LookupTable.lookupTable [
                TextureAspect.Color, ImageAspect.Color
                TextureAspect.Depth, ImageAspect.Depth
                TextureAspect.Stencil, ImageAspect.Stencil
            ]
            
    module ImageSubresource =
        let ofTextureSubResource (src : ITextureSubResource) =
            let srcAspect = ImageAspect.ofTextureAspect src.Aspect
            let srcImage = src.Texture |> unbox<Image>
            srcImage.[srcAspect, src.Level, src.Slice]
            
    module ImageSubresourceLayers =
        let ofFramebufferOutput (src : IFramebufferOutput) =
            match src with
                | :? Image as img ->
                    if VkFormat.hasDepth img.Format then
                        img.[ImageAspect.Depth, 0, *]
                    else
                        img.[ImageAspect.Color, 0, *]

                | :? ITextureLevel as src ->
                    let srcAspect = ImageAspect.ofTextureAspect src.Aspect
                    let srcImage = src.Texture |> unbox<Image>
                    srcImage.[srcAspect, src.Level, src.Slices.Min .. src.Slices.Max]

                | _ ->
                    failf "unexpected IFramebufferOutput: %A" src
        
