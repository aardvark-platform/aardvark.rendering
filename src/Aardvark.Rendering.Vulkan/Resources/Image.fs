namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.ReflectionHelpers



#nowarn "9"
// #nowarn "51"

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
        
    let private allAccess = 
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
        VkAccessFlags.VertexAttributeReadBit ||| 
        VkAccessFlags.ColorAttachmentWriteBit |||
        VkAccessFlags.DepthStencilAttachmentWriteBit |||
        VkAccessFlags.HostWriteBit |||
        VkAccessFlags.MemoryWriteBit |||
        VkAccessFlags.ShaderWriteBit |||
        VkAccessFlags.TransferWriteBit



    let private allAspects =
        VkImageAspectFlags.ColorBit |||
        VkImageAspectFlags.DepthBit ||| 
        VkImageAspectFlags.StencilBit

    type VkAccessFlags with
        static member Write = writeAccess
        static member Read = readAccess
        static member All = allAccess


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
        let private depthFormats = HashSet.ofList [ VkFormat.D16Unorm; VkFormat.D32Sfloat; VkFormat.X8D24UnormPack32 ]
        let private depthStencilFormats = HashSet.ofList [VkFormat.D16UnormS8Uint; VkFormat.D24UnormS8Uint; VkFormat.D32SfloatS8Uint ]

        let toImageKind (fmt : VkFormat) =
            if depthStencilFormats.Contains fmt then ImageKind.DepthStencil
            elif depthFormats.Contains fmt then ImageKind.Depth
            else ImageKind.Color


    type VolumeInfo with
        member x.Transformed(t : ImageTrafo) =
            let sx = x.SX
            let sy = x.SY
            let sz = x.SZ
            let dx = x.DX
            let dy = x.DY
            let dz = x.DZ
            match t with
                | ImageTrafo.Identity -> x
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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkImageAspectFlags =
    let ofImageAspect (a : ImageAspect) =
        a |> int |> unbox<VkImageAspectFlags>


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
        val mutable public PeerHandles : VkImage[]
        val mutable public Version : cval<int>

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
                PeerHandles = [||]
                Version = AVal.init 0
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
    
    let private devilLock = typeof<PixImageDevil>.GetField("s_devilLock", System.Reflection.BindingFlags.NonPublic ||| System.Reflection.BindingFlags.Static).GetValue(null)
    
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
                PixImageDevil.InitDevil()

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
                    let copy =
                        VkBufferImageCopy(
                            0UL,
                            0u,
                            0u,
                            dst.VkImageSubresourceLayers,
                            VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )
                        
                    cmd.AppendCommand()
                    copy |> pin (fun pCopy ->
                        VkRaw.vkCmdCopyBufferToImage(cmd.Handle, src.Buffer.Handle, dst.Image.Handle, dst.Image.Layout, 1u, pCopy)
                    )
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
                    let copy =
                        VkBufferImageCopy(
                            0UL,
                            0u,
                            0u,
                            src.VkImageSubresourceLayers,
                            VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )
                        
                    cmd.AppendCommand()
                    copy |> pin (fun pCopy ->
                        VkRaw.vkCmdCopyImageToBuffer(cmd.Handle, src.Image.Handle, src.Image.Layout, dst.Buffer.Handle, 1u, pCopy)
                    )
                    Disposable.Empty
            }

        static member Copy(src : ImageSubresource, dst : TensorImage) =
            if src.Size <> dst.Size then failf "[TensorImage] mismatching sizes in copy %A vs %A" src.Size dst.Size
            Command.Copy(src, V3i.Zero, dst, src.Size)

        static member Acquire(src : ImageSubresourceRange, srcLayout : VkImageLayout, dstLayout : VkImageLayout, srcQueue : DeviceQueueFamily) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd) =
                    cmd.AppendCommand()

                    let imageMemoryBarrier =
                        VkImageMemoryBarrier(
                            VkAccessFlags.None,
                            VkImageLayout.toAccessFlags dstLayout ,
                            srcLayout,
                            dstLayout,
                            uint32 srcQueue.Index,
                            uint32 cmd.QueueFamily.Index,
                            src.Image.Handle,
                            src.VkImageSubresourceRange
                        )

                    imageMemoryBarrier |> pin (fun pBarrier ->
                        VkRaw.vkCmdPipelineBarrier(
                            cmd.Handle,
                            VkPipelineStageFlags.TopOfPipeBit,
                            VkImageLayout.toDstStageFlags dstLayout,
                            VkDependencyFlags.None,
                            0u, NativePtr.zero,
                            0u, NativePtr.zero,
                            1u, pBarrier
                        )
                    )

                    Disposable.Empty
            }
            
        static member Acquire(src : ImageSubresourceRange, layout : VkImageLayout, srcQueue : DeviceQueueFamily) =
            Command.Acquire(src, layout, layout, srcQueue)

    type CopyCommand with
        

        static member Copy(src : ImageSubresourceLayers, srcOffset : V3i, dst : ImageSubresourceLayers, dstOffset : V3i, size : V3i) =
            CopyCommand.ImageToImageCmd(
                src.Image.Handle,
                VkImageLayout.TransferSrcOptimal,
                dst.Image.Handle,
                VkImageLayout.TransferDstOptimal,
                VkImageCopy(
                    src.VkImageSubresourceLayers,
                    VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                    dst.VkImageSubresourceLayers,
                    VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                    VkExtent3D(size.X, size.Y, size.Z)
                ), 
                0L
            )

        static member Copy(src : ImageSubresourceLayers, dst : ImageSubresourceLayers) =
            CopyCommand.Copy(src, V3i.Zero, dst, V3i.Zero, src.Size)

        // upload
        static member Copy(src : TensorImage, dst : ImageSubresource, dstOffset : V3i, size : V3i) =
            CopyCommand.Copy(
                src.Buffer.Handle, 0L,
                dst.Image.Handle,
                dst.Image.Layout,
                dst.Image.Format,
                VkBufferImageCopy(
                    0UL, 0u, 0u,
                    dst.VkImageSubresourceLayers,
                    VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                    VkExtent3D(size.X, size.Y, size.Z)
                )
            )
            
        static member Copy(src : TensorImage, dst : ImageSubresource) =
            CopyCommand.Copy(src, dst, V3i.Zero, src.Size)

        // download
        static member Copy(src : ImageSubresource, srcOffset : V3i, dst : TensorImage, size : V3i) =
            CopyCommand.Copy(
                src.Image.Handle,
                src.Image.Layout,
                dst.Buffer.Handle,
                src.Image.Format,
                VkBufferImageCopy(
                    0UL, 0u, 0u,
                    src.VkImageSubresourceLayers,
                    VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                    VkExtent3D(size.X, size.Y, size.Z)
                )
            )
            
        static member Copy(src : ImageSubresource, dst : TensorImage) =
            CopyCommand.Copy(src, V3i.Zero, dst, src.Size)


        static member TransformLayout(img : ImageSubresourceRange, srcLayout : VkImageLayout, dstLayout : VkImageLayout) =
            CopyCommand.TransformLayout(img.Image.Handle, img.VkImageSubresourceRange, srcLayout, dstLayout)

        static member Release(img : ImageSubresourceRange, srcLayout : VkImageLayout, dstLayout : VkImageLayout, dstQueueFamily : DeviceQueueFamily) =
            CopyCommand.Release(img.Image.Handle, img.VkImageSubresourceRange, srcLayout, dstLayout, dstQueueFamily.Index)

        static member Release(img : ImageSubresourceRange, layout : VkImageLayout, dstQueueFamily : DeviceQueueFamily) =
            CopyCommand.Release(img.Image.Handle, img.VkImageSubresourceRange, layout, layout, dstQueueFamily.Index)
            
        static member SyncImage(img : ImageSubresourceRange, layout : VkImageLayout, srcAccess : VkAccessFlags) =
            CopyCommand.SyncImage(img.Image.Handle, img.VkImageSubresourceRange, layout, srcAccess)

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

    open KHRSwapchain

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
                    let dstLayout = dst.Image.Layout // bring this one to dstoptimal if undefined.
                    let copy =
                        VkImageCopy(
                            src.VkImageSubresourceLayers,
                            VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                            dst.VkImageSubresourceLayers,
                            VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )

                    cmd.AppendCommand()
                    copy |> pin (fun pCopy ->
                        VkRaw.vkCmdCopyImage(cmd.Handle, src.Image.Handle, src.Image.Layout, dst.Image.Handle, dst.Image.Layout, 1u, pCopy)
                    )
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
                    let copy =
                        VkBufferImageCopy(
                            uint64 srcOffset,
                            uint32 srcStride.X,
                            uint32 srcStride.Y,
                            dst.VkImageSubresourceLayers,
                            VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )
                        
                    cmd.AppendCommand()
                    copy |> pin (fun pCopy ->
                        VkRaw.vkCmdCopyBufferToImage(cmd.Handle, src.Handle, dst.Image.Handle, dst.Image.Layout, 1u, pCopy)
                    )
                    Disposable.Empty
            }

        static member Copy(src : ImageSubresourceLayers, srcOffset : V3i, dst : Buffer, dstOffset : int64, dstStride : V2i, size : V3i) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let copy =
                        VkBufferImageCopy(
                            uint64 dstOffset,
                            uint32 dstStride.X,
                            uint32 dstStride.Y,
                            src.VkImageSubresourceLayers,
                            VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )
                        
                    cmd.AppendCommand()
                    copy |> pin (fun pCopy ->
                        VkRaw.vkCmdCopyImageToBuffer(cmd.Handle, src.Image.Handle, src.Image.Layout, dst.Handle, 1u, pCopy)
                    )
                    Disposable.Empty
            }

        static member ResolveMultisamples(src : ImageSubresourceLayers, srcOffset : V3i, dst : ImageSubresourceLayers, dstOffset : V3i, size : V3i) =
            if src.SliceCount <> dst.SliceCount then
                failf "cannot resolve image: { srcSlices = %A; dstSlices = %A }" src.SliceCount dst.SliceCount
                
            { new Command() with
                member x.Compatible = QueueFlags.Graphics
                member x.Enqueue (cmd : CommandBuffer) =
                    let resolve =
                        VkImageResolve(
                            src.VkImageSubresourceLayers,
                            VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                            dst.VkImageSubresourceLayers,
                            VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )

                    cmd.AppendCommand()
                    resolve |> pin (fun pResolve ->
                        VkRaw.vkCmdResolveImage(cmd.Handle, src.Image.Handle, src.Image.Layout, dst.Image.Handle, dst.Image.Layout, 1u, pResolve)
                    )
                    Disposable.Empty
            }

        static member ResolveMultisamples(src : ImageSubresourceLayers, dst : ImageSubresourceLayers) =
            if src.Size <> dst.Size then
                failf "cannot copy image: { srcSize = %A; dstSize = %A }" src.LevelCount dst.LevelCount

            Command.ResolveMultisamples(src, V3i.Zero, dst, V3i.Zero, src.Size)


        static member Blit(src : ImageSubresourceLayers, srcLayout : VkImageLayout, srcRange : Box3i, dst : ImageSubresourceLayers, dstLayout : VkImageLayout, dstRange : Box3i, filter : VkFilter) =
            { new Command() with
                member x.Compatible = QueueFlags.Graphics
                member x.Enqueue cmd =

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


                    let blit =
                        VkImageBlit(
                            src.VkImageSubresourceLayers,
                            srcOffsets,
                            dst.VkImageSubresourceLayers,
                            dstOffsets
                        )
                    
                    cmd.AppendCommand()
                    blit |> pin (fun pBlit ->
                        VkRaw.vkCmdBlitImage(cmd.Handle, src.Image.Handle, srcLayout, dst.Image.Handle, dstLayout, 1u, pBlit, filter)
                    )
                    Disposable.Empty
            }

        static member Blit(src : ImageSubresourceLayers, srcLayout : VkImageLayout, dst : ImageSubresourceLayers, dstLayout : VkImageLayout, dstRange : Box3i, filter : VkFilter) =
            Command.Blit(src, srcLayout, Box3i(V3i.Zero, src.Size - V3i.III), dst, dstLayout, dstRange, filter)

        static member Blit(src : ImageSubresourceLayers, srcLayout : VkImageLayout, srcRange : Box3i, dst : ImageSubresourceLayers, dstLayout : VkImageLayout, filter : VkFilter) =
            Command.Blit(src, srcLayout, srcRange, dst, dstLayout, Box3i(V3i.Zero, dst.Size - V3i.III), filter)

        static member Blit(src : ImageSubresourceLayers, srcLayout : VkImageLayout, dst : ImageSubresourceLayers, dstLayout : VkImageLayout, filter : VkFilter) =
            Command.Blit(src, srcLayout, Box3i(V3i.Zero, src.Size - V3i.III), dst, dstLayout, Box3i(V3i.Zero, dst.Size - V3i.III), filter)


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
                    
                        let clearValue = VkClearColorValue(float32 = color.ToV4f())
                        let range = img.VkImageSubresourceRange
                        clearValue |> pin (fun pClear ->
                            range |> pin (fun pRange ->
                                cmd.AppendCommand()
                                VkRaw.vkCmdClearColorImage(cmd.Handle, img.Image.Handle, VkImageLayout.TransferDstOptimal, pClear, 1u, pRange)
                            )
                        )
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
                        clearValue |> pin (fun pClear ->
                            range |> pin (fun pRange ->
                                cmd.AppendCommand()
                                VkRaw.vkCmdClearDepthStencilImage(cmd.Handle, img.Image.Handle, VkImageLayout.TransferDstOptimal, pClear, 1u, pRange)
                            )
                        )
                        cmd.Enqueue (Command.TransformLayout(img.Image, originalLayout))
                        Disposable.Empty
                }


        static member Sync(img : ImageSubresourceRange, layout : VkImageLayout, src : VkAccessFlags, dst : VkAccessFlags) =
            { new Command() with
                member x.Compatible = QueueFlags.Graphics ||| QueueFlags.Compute
                member x.Enqueue cmd =
                    cmd.AppendCommand()
                    let srcStage = VkAccessFlags.toVkPipelineStageFlags src
                    let dstStage = VkAccessFlags.toVkPipelineStageFlags dst

                    let image =
                        VkImageMemoryBarrier(
                            src, dst, 
                            layout, layout,
                            VK_QUEUE_FAMILY_IGNORED, VK_QUEUE_FAMILY_IGNORED,
                            img.Image.Handle,
                            img.VkImageSubresourceRange
                        )

                    image |> pin (fun pImage ->
                        VkRaw.vkCmdPipelineBarrier(
                            cmd.Handle,
                            srcStage, dstStage,
                            VkDependencyFlags.None,
                            0u, NativePtr.zero,
                            0u, NativePtr.zero,
                            1u, pImage
                        )
                    )

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
                        do! Command.Blit(img.[l - 1, *], VkImageLayout.TransferSrcOptimal, img.[l, *], VkImageLayout.TransferDstOptimal, VkFilter.Linear)
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

                        let barrier =
                            VkImageMemoryBarrier(
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
                        barrier |> pin (fun pBarrier ->
                            VkRaw.vkCmdPipelineBarrier(
                                cmd.Handle,
                                srcMask,
                                dstMask,
                                VkDependencyFlags.None,
                                0u, NativePtr.zero,
                                0u, NativePtr.zero,
                                1u, pBarrier
                            )
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


        static member SyncPeers (img : ImageSubresourceLayers, ranges : array<Range1i * Box3i>) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd) =
                
                    let device = img.Image.Device

                    let mutable totalSize = 0L

                    if img.Image.PeerHandles.Length > 0 then
                        cmd.AppendCommand()


                        let device = img.Image.Device

                        let baseImage = img.Image
                        let deviceIndices = baseImage.Device.AllIndicesArr
                        

                        for di in deviceIndices do
                            VkRaw.vkCmdSetDeviceMask(cmd.Handle, 1u <<< int di)

                            let srcSlices, srcRange = ranges.[int di]

                            for ci in 0 .. baseImage.PeerHandles.Length - 1 do
                                let srcSub = img.[srcSlices.Min .. srcSlices.Max].VkImageSubresourceLayers
                                let copy =
                                    VkImageCopy(
                                        srcSub,
                                        VkOffset3D(srcRange.Min.X, srcRange.Min.Y, srcRange.Min.Z),
                                        srcSub,
                                        VkOffset3D(srcRange.Min.X, srcRange.Min.Y, srcRange.Min.Z),
                                        VkExtent3D(1+srcRange.SizeX, 1+srcRange.SizeY, 1+srcRange.SizeZ)
                                    )

                                if di = 0u then
                                    let imgSize = int64 copy.extent.width * int64 copy.extent.height * int64 copy.extent.depth * 4L
                                    totalSize <- totalSize + imgSize * int64 copy.srcSubresource.layerCount

                                copy |> pin (fun pCopy ->
                                    VkRaw.vkCmdCopyImage(
                                        cmd.Handle, 
                                        baseImage.Handle, VkImageLayout.TransferSrcOptimal, 
                                        baseImage.PeerHandles.[ci], VkImageLayout.TransferDstOptimal,
                                        1u, pCopy
                                    )
                                )

                        VkRaw.vkCmdSetDeviceMask(cmd.Handle, baseImage.Device.AllMask)

                        let mem =
                            VkMemoryBarrier(
                                VkAccessFlags.TransferWriteBit,
                                VkAccessFlags.TransferReadBit ||| VkAccessFlags.ShaderReadBit
                            )
                        mem |> pin (fun pMem ->
                            VkRaw.vkCmdPipelineBarrier(
                                cmd.Handle,
                                VkPipelineStageFlags.TransferBit,
                                VkPipelineStageFlags.TransferBit ||| VkPipelineStageFlags.VertexShaderBit,
                                VkDependencyFlags.DeviceGroupBit,
                                1u, pMem, 
                                0u, NativePtr.zero, 
                                0u, NativePtr.zero
                            )
                        )

                    Disposable.Empty
            }




// ===========================================================================================
// Image functions
// ===========================================================================================
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Image =
    open KHRBindMemory2

    let allocLinear (size : V2i) (fmt : VkFormat) (usage : VkImageUsageFlags) (device : Device) =
        let info =
            VkImageCreateInfo(
                VkImageCreateFlags.None,
                VkImageType.D2d,
                fmt,
                VkExtent3D(uint32 size.X, uint32 size.Y, 1u),
                1u,
                1u,
                VkSampleCountFlags.D1Bit,
                VkImageTiling.Linear,
                usage,
                VkSharingMode.Exclusive,
                0u, NativePtr.zero,
                VkImageLayout.Preinitialized
            ) 

        let mutable handle = 
            temporary (fun pHandle ->
                info |> pin (fun pInfo ->
                    VkRaw.vkCreateImage(device.Handle, pInfo, NativePtr.zero, pHandle)
                        |> check "could not create image"
                    NativePtr.read pHandle
                )
            )

        let reqs =
            temporary (fun ptr ->
                VkRaw.vkGetImageMemoryRequirements(device.Handle, handle, ptr)
                NativePtr.read ptr
            )
        let memalign = int64 reqs.alignment |> Alignment.next device.BufferImageGranularity
        let memsize = int64 reqs.size |> Alignment.next device.BufferImageGranularity


//        let mutable resource = VkImageSubresource(VkImageAspectFlags.ColorBit, 0u, 0u)
//        let mutable layout = VkSubresourceLayout()
//
//        VkRaw.vkGetImageSubresourceLayout(device.Handle, handle, &&resource, &&layout)
//
//
//
//        let blocks = int64 reqs.size / 16L
//        let blocksX = int64 (Fun.Ceiling(float size.X / 4.0)) //(Alignment.next (int64 size.X) 4L / 4L)
//        let blocksY = int64 (Fun.Ceiling(float size.Y / 4.0)) //(Alignment.next (int64 size.Y) 4L / 4L)
//        let expectedBlocks = blocksX * blocksY
//
//        let vkX = blocks / blocksY
//
//        if blocksX % 2L <> 0L then
//            Log.warn "is bad %A????" size
//            Log.warn "%A" layout
//
//        if vkX <> blocksX then
//            Log.warn "bad: %A %A %A" size blocksX vkX
//




        if device.HostMemory.Mask &&& reqs.memoryTypeBits = 0u then
            VkRaw.vkDestroyImage(device.Handle, handle, NativePtr.zero)
            failf "cannot create linear image in host-memory"

        let ptr = device.HostMemory.Alloc(memalign, memsize)

        VkRaw.vkBindImageMemory(device.Handle, handle, ptr.Memory.Handle, uint64 ptr.Offset)
            |> check "could not bind image memory"

        let result = Image(device, handle, V3i(size, 1), 1, 1, 1, TextureDimension.Texture2D, fmt, ptr, VkImageLayout.Preinitialized)
        
        result

    let rec alloc (size : V3i) (mipMapLevels : int) (count : int) (samples : int) (dim : TextureDimension) (fmt : VkFormat) (usage : VkImageUsageFlags) (device : Device) =
        if device.PhysicalDevice.GetFormatFeatures(VkImageTiling.Optimal, fmt) = VkFormatFeatureFlags.None then
            match fmt.NextBetter with
                | Some fmt -> alloc size mipMapLevels count samples dim fmt usage device
                | None -> failf "bad image format %A" fmt
        else
            let mayHavePeers =
                device.IsDeviceGroup &&
                (
                    (usage &&& VkImageUsageFlags.ColorAttachmentBit <> VkImageUsageFlags.None) ||
                    (usage &&& VkImageUsageFlags.DepthStencilAttachmentBit <> VkImageUsageFlags.None) ||
                    (usage &&& VkImageUsageFlags.StorageBit <> VkImageUsageFlags.None)
                )

            let flags =
                if dim = TextureDimension.TextureCube then VkImageCreateFlags.CubeCompatibleBit
                else VkImageCreateFlags.None

            let flags =
                if mayHavePeers then VkImageCreateFlags.AliasBit ||| flags
                else flags
                
            let info =
                VkImageCreateInfo(
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

            let handle = 
                temporary (fun pHandle ->
                    info |> pin (fun pInfo ->
                        VkRaw.vkCreateImage(device.Handle, pInfo, NativePtr.zero, pHandle)
                            |> check "could not create image"
                        NativePtr.read pHandle
                    )
                )
            let reqs =
                temporary (fun ptr ->
                    VkRaw.vkGetImageMemoryRequirements(device.Handle, handle,ptr)
                    NativePtr.read ptr
                )
            let memalign = int64 reqs.alignment |> Alignment.next device.BufferImageGranularity
            let memsize = int64 reqs.size |> Alignment.next device.BufferImageGranularity
            let ptr = device.Alloc(VkMemoryRequirements(uint64 memsize, uint64 memalign, reqs.memoryTypeBits), true)



            if mayHavePeers then
                let indices = device.AllIndicesArr
                let handles = Array.zeroCreate indices.Length
                handles.[0] <- handle
                for i in 1 .. indices.Length - 1 do
                    let handle = 
                        temporary (fun pHandle ->
                            info |> pin (fun pInfo ->
                                VkRaw.vkCreateImage(device.Handle, pInfo, NativePtr.zero, pHandle)
                                    |> check "could not create image"
                                NativePtr.read pHandle
                            )
                        )
                    handles.[1] <- handle

                for off in 0 .. indices.Length - 1 do 
                    let deviceIndices =
                        Array.init indices.Length (fun i ->
                            indices.[(i+off) % indices.Length] |> uint32
                        )

                    native {
                        let! pDeviceIndices = deviceIndices
                        let groupInfo =
                            VkBindImageMemoryDeviceGroupInfo(
                                uint32 deviceIndices.Length, pDeviceIndices,
                                0u, NativePtr.zero
                            )
                        let! pGroup = groupInfo
                        let! pInfo =  
                            VkBindImageMemoryInfo(
                                NativePtr.toNativeInt pGroup,
                                handles.[off],
                                ptr.Memory.Handle,
                                uint64 ptr.Offset
                            )
                        VkRaw.vkBindImageMemory2(device.Handle, 1u, pInfo)
                            |> check "could not bind image memory"
                    }
                    

                let result = Image(device, handles.[0], size, mipMapLevels, count, samples, dim, fmt, ptr, VkImageLayout.Undefined)
                result.PeerHandles <- Array.skip 1 handles

                device.perform {
                    for i in 1 .. handles.Length - 1 do
                        let img = Image(device, handles.[i], size, mipMapLevels, count, samples, dim, fmt, ptr, VkImageLayout.Undefined)
                        do! Command.TransformLayout(img, VkImageLayout.TransferDstOptimal)
                }

                result
            else
                VkRaw.vkBindImageMemory(device.Handle, handle, ptr.Memory.Handle, uint64 ptr.Offset)
                    |> check "could not bind image memory"

                let result = Image(device, handle, size, mipMapLevels, count, samples, dim, fmt, ptr, VkImageLayout.Undefined)
        
                result

    let delete (img : Image) (device : Device) =
        if Interlocked.Decrement(&img.RefCount) = 0 then
            if device.Handle <> 0n && img.Handle.IsValid then
                VkRaw.vkDestroyImage(img.Device.Handle, img.Handle, NativePtr.zero)
                for p in img.PeerHandles do VkRaw.vkDestroyImage(img.Device.Handle, p, NativePtr.zero)
                img.PeerHandles <- null
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
                (VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.StorageBit)
                device

        let tempImages = 
            List.init uploadLevels (fun level ->
                let data = pi.ImageArray.[level]
                let temp = device.CreateTensorImage(V3i(data.Size.X, data.Size.Y, 1), expectedFormat, info.wantSrgb)
                temp.Write(data, ImageTrafo.MirrorY)
                temp
            )

        match device.UploadMode with
            | UploadMode.Async ->
                let imageRange = image.[ImageAspect.Color]
                image.Layout <- VkImageLayout.TransferDstOptimal

                device.CopyEngine.EnqueueSafe [
                    yield CopyCommand.TransformLayout(imageRange, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal)
                    yield! tempImages |> List.mapi (fun level src -> CopyCommand.Copy(src, imageRange.[level, 0]))
                    
                    yield CopyCommand.SyncImage(imageRange, VkImageLayout.TransferDstOptimal, VkAccessFlags.TransferWriteBit)

                    yield CopyCommand.Release(imageRange, VkImageLayout.TransferDstOptimal, device.GraphicsFamily)
                    yield CopyCommand.Callback (fun () -> tempImages |> List.iter device.Delete)
                ]

                device.eventually {
                    do! Command.Acquire(imageRange, VkImageLayout.TransferDstOptimal, device.TransferFamily)
                    if generateMipMaps then
                        do! Command.GenerateMipMaps image.[ImageAspect.Color]
                    do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)
                }

            | _ ->
                device.eventually {
                    try
                        do! Command.TransformLayout(image, VkImageLayout.TransferDstOptimal)

                        // upload the levels
                        let mutable level = 0
                        for temp in tempImages do
                            do! Command.Copy(temp, image.[ImageAspect.Color, level, 0])
                            level <- level + 1

                        // generate the mipMaps
                        if generateMipMaps then
                            do! Command.GenerateMipMaps image.[ImageAspect.Color]

                        do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)

                    finally
                        for t in tempImages do device.Delete t
                }

        image

    // TODO: check CopyEngine
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
                (VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.StorageBit)
                device

        let temp = device.CreateTensorImage(pi.Size, expectedFormat, info.wantSrgb)
        temp.Write(pi)

        
        match device.UploadMode with
            | UploadMode.Async ->
                let imageRange = image.[ImageAspect.Color]
                image.Layout <- VkImageLayout.TransferDstOptimal

                device.CopyEngine.EnqueueSafe [
                    CopyCommand.TransformLayout(imageRange, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal)
                    CopyCommand.Copy(temp, imageRange.[0,0])

                    CopyCommand.SyncImage(imageRange, VkImageLayout.TransferDstOptimal, VkAccessFlags.TransferWriteBit)

                    CopyCommand.Release(imageRange, VkImageLayout.TransferDstOptimal, device.GraphicsFamily)   
                    CopyCommand.Callback(fun () -> device.Delete temp)
                ]

                device.eventually {
                    do! Command.Acquire(imageRange, VkImageLayout.TransferDstOptimal, device.TransferFamily)
                    
                    // generate the mipMaps
                    if info.wantMipMaps then
                        do! Command.GenerateMipMaps image.[ImageAspect.Color]

                    do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)
                }

            | _ -> 
                device.eventually {
                    try
                        do! Command.TransformLayout(image, VkImageLayout.TransferDstOptimal)

                        // upload the level 0
                        do! Command.Copy(temp, image.[ImageAspect.Color, 0, 0])

                        // generate the mipMaps
                        if info.wantMipMaps then
                            do! Command.GenerateMipMaps image.[ImageAspect.Color]

                        do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)

                    finally
                        device.Delete temp
                }

        image
        
    // TODO: check CopyEngine
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
                (VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.StorageBit)
                device

        let tempImages =
            List.init uploadLevels (fun level ->
                List.init 6 (fun face ->
                    let data = pi.MipMapArray.[face].ImageArray.[level]
                    let temp = device.CreateTensorImage(V3i(data.Size.X, data.Size.Y, 1), expectedFormat, info.wantSrgb)
                    temp.Write(data, ImageTrafo.MirrorY)
                    temp
                )
            )

        match device.UploadMode with
            | UploadMode.Async ->
                let imageRange = image.[ImageAspect.Color]
                image.Layout <- VkImageLayout.TransferDstOptimal
                device.CopyEngine.EnqueueSafe [
                    yield CopyCommand.TransformLayout(imageRange, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal)
                    
                    for (level, faces) in Seq.indexed tempImages do
                        for (face, temp) in Seq.indexed faces do
                            yield CopyCommand.Copy(temp, image.[ImageAspect.Color, level, face])

                    yield CopyCommand.SyncImage(imageRange, VkImageLayout.TransferDstOptimal, VkAccessFlags.TransferWriteBit)

                    yield CopyCommand.Release(imageRange, VkImageLayout.TransferDstOptimal, device.GraphicsFamily)
                    yield CopyCommand.Callback (fun () -> tempImages |> List.iter (List.iter device.Delete))
                ]

                device.eventually {
                    do! Command.Acquire(imageRange, VkImageLayout.TransferDstOptimal, device.TransferFamily)
                    // generate the mipMaps
                    if generateMipMaps then
                        do! Command.GenerateMipMaps image.[ImageAspect.Color]

                    do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)
                }

            | _ -> 
                device.eventually {
                    try
                        do! Command.TransformLayout(image, VkImageLayout.TransferDstOptimal)

                        // upload the levels
                        for (level, faces) in Seq.indexed tempImages do
                            for (face, temp) in Seq.indexed faces do
                                do! Command.Copy(temp, image.[ImageAspect.Color, level, face])
                       
                        // generate the mipMaps
                        if generateMipMaps then
                            do! Command.GenerateMipMaps image.[ImageAspect.Color]

                        do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)

                    finally
                        tempImages |> List.iter (List.iter device.Delete)
                }

        image
  
  
    [<AutoOpen>]
    module private Helpers = 

        type nativeptr<'a when 'a : unmanaged> with
            member x.Item
                with inline get(i : int) = NativePtr.get x i
                and inline set(i : int) (v : 'a) = NativePtr.set x i v

        let inline (++) (ptr : nativeptr<'a>) (i : int) =
            NativePtr.add ptr i

        module NativePtr =
            let inline swap (l : nativeptr<'a>) (r : nativeptr<'a>) =
                let t = NativePtr.read l
                NativePtr.write l (NativePtr.read r)
                NativePtr.write r t

        let mirrorCopyDXT1 (src : nativeptr<byte>) (dst : nativeptr<byte>) =
            dst.[0] <- src.[0]
            dst.[1] <- src.[1]
            dst.[2] <- src.[2]
            dst.[3] <- src.[3]
            dst.[7] <- src.[4]
            dst.[6] <- src.[5]
            dst.[5] <- src.[6]
            dst.[4] <- src.[7]

        let mirrorCopyDXT3 (src : nativeptr<byte>) (dst : nativeptr<byte>) =
            dst.[6] <- src.[0]
            dst.[7] <- src.[1]
            dst.[4] <- src.[2]
            dst.[5] <- src.[3]
            dst.[2] <- src.[4]
            dst.[3] <- src.[5]
            dst.[0] <- src.[6]
            dst.[1] <- src.[7]
            mirrorCopyDXT1 (src ++ 8) (dst ++ 8)

        let mirrorCopyDXT5 (src : nativeptr<byte>) (dst : nativeptr<byte>) =
            let line_0_1 = uint32 src.[2] + 256u * (uint32 src.[3] + 256u * uint32 src.[4]);
            let line_2_3 = uint32 src.[5] + 256u * (uint32 src.[6] + 256u * uint32 src.[7]);
            let line_1_0 = ((line_0_1 &&& 0x000fffu) <<< 12) ||| ((line_0_1 &&& 0xfff000u) >>> 12);
            let line_3_2 = ((line_2_3 &&& 0x000fffu) <<< 12) |||  ((line_2_3 &&& 0xfff000u) >>> 12);
            dst.[0] <- src.[0]
            dst.[1] <- src.[1]
            dst.[2] <- byte (line_3_2 &&& 0xffu)
            dst.[3] <- byte ((line_3_2 &&& 0xff00u) >>> 8)
            dst.[4] <- byte ((line_3_2 &&& 0xff0000u) >>> 16)
            dst.[5] <- byte (line_1_0 &&& 0xffu)
            dst.[6] <- byte ((line_1_0 &&& 0xff00u) >>> 8)
            dst.[7] <- byte ((line_1_0 &&& 0xff0000u) >>> 16)

            mirrorCopyDXT1 (src ++ 8) (dst ++ 8)

        let inline ceilDiv (v : ^a) (d : ^a) : ^a =
            if v % d = LanguagePrimitives.GenericZero then v / d
            else LanguagePrimitives.GenericOne + v / d
    
    let ofNativeImage (image : INativeTexture) (device : Device) =
        if image.Count <> 1 then failf "NativeTexture layering not implemented"
        

        let levels = image.MipMapLevels
        let size = image.[0,0].Size

        let format = VkFormat.ofTextureFormat image.Format
        let compressionMode = TextureFormat.compressionMode image.Format


        let result =
            alloc size levels image.Count 1 image.Dimension format (VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.StorageBit) device


        if compressionMode <> CompressionMode.None then
            if image.Dimension <> TextureDimension.Texture2D then failf "NativeTexture: only 2D textures implemented atm."

            let levels =
                Array.init levels (fun level ->
                    let srcLevel = image.[0, level]
                    let temp = allocLinear srcLevel.Size.XY format VkImageUsageFlags.TransferSrcBit device

                    let resource = VkImageSubresource(VkImageAspectFlags.ColorBit, 0u, 0u)
                    let layout = 
                        resource |> pin (fun pResource ->
                            temporary (fun pLayout ->
                                VkRaw.vkGetImageSubresourceLayout(device.Handle, temp.Handle, pResource, pLayout)
                                NativePtr.read pLayout
                            )
                        )
                    let vkRowPitch = int64 layout.rowPitch

                    let blockSize = CompressionMode.blockSize compressionMode
                    let blockBytes = CompressionMode.blockSizeInBytes compressionMode |> int64

                    let blocksX = ceilDiv srcLevel.Size.X blockSize.X
                    let blocksY = ceilDiv srcLevel.Size.Y blockSize.Y
                    let rowPitch = blockBytes * int64 blocksX

                    if temp.Memory.Size < srcLevel.SizeInBytes then
                        failf "NativeTexture invalid memory-size"

                    temp.Memory.Mapped(fun dst ->
                        srcLevel.Use(fun src ->
//                            
//                            if rowPitch = vkRowPitch then
//                                Marshal.Copy(src, dst, srcLevel.SizeInBytes)
//                            else
                            let mutable src = src
                            let mutable dst = dst + nativeint vkRowPitch * nativeint (blocksY - 1)
                            let blockJmp = nativeint blockBytes
                            let dstJmp = nativeint -vkRowPitch - nativeint rowPitch

                            match compressionMode with
                                | CompressionMode.BC1 ->
                                    for r in 0 .. blocksY - 1 do
                                        for x in 0 .. blocksX - 1 do
                                            Helpers.mirrorCopyDXT1 (NativePtr.ofNativeInt src) (NativePtr.ofNativeInt dst)
                                            src <- src + blockJmp
                                            dst <- dst + blockJmp
                                        dst <- dst + dstJmp

                                | CompressionMode.BC2 ->
                                    for r in 0 .. blocksY - 1 do
                                        for x in 0 .. blocksX - 1 do
                                            Helpers.mirrorCopyDXT3 (NativePtr.ofNativeInt src) (NativePtr.ofNativeInt dst)
                                            src <- src + blockJmp
                                            dst <- dst + blockJmp
                                        dst <- dst + dstJmp

                                | CompressionMode.BC3 ->
                                    for r in 0 .. blocksY - 1 do
                                        for x in 0 .. blocksX - 1 do
                                            Helpers.mirrorCopyDXT5 (NativePtr.ofNativeInt src) (NativePtr.ofNativeInt dst)
                                            src <- src + blockJmp
                                            dst <- dst + blockJmp
                                        dst <- dst + dstJmp

                                | _ ->
                                    failf "compression %A not implemented" compressionMode

                        )
                    )

                    temp
                )

            match device.UploadMode with
                | UploadMode.Async ->
                    device.CopyEngine.EnqueueSafe [
                        yield CopyCommand.TransformLayout(result.[ImageAspect.Color], VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal)

                        for level in 0 .. levels.Length - 1 do
                            let src = levels.[level]

                            yield CopyCommand.TransformLayout(src.[ImageAspect.Color], VkImageLayout.Preinitialized, VkImageLayout.TransferSrcOptimal)
                            yield CopyCommand.Copy(src.[ImageAspect.Color, 0, 0], result.[ImageAspect.Color, level, 0])
                               
                        yield CopyCommand.SyncImage(result.[ImageAspect.Color], VkImageLayout.TransferDstOptimal, VkAccessFlags.TransferWriteBit)

                        yield CopyCommand.Release(result.[ImageAspect.Color], VkImageLayout.TransferDstOptimal, device.GraphicsFamily)
                        yield CopyCommand.Callback (fun () -> levels |> Array.iter (fun i -> delete i device))
                    ]

                    result.Layout <- VkImageLayout.TransferDstOptimal
                    device.eventually {
                        do! Command.Acquire(result.[ImageAspect.Color], VkImageLayout.TransferDstOptimal, device.TransferFamily)
                        do! Command.TransformLayout(result, VkImageLayout.ShaderReadOnlyOptimal)
                    }

                    result

                | _ ->
                    failf "NativeTexture sync upload not implemented"


        else
            match device.UploadMode with
                | UploadMode.Async ->
                    let tempImages =
                        Array.init image.MipMapLevels (fun level ->
                            let data = image.[0, level]
                            let buffer = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit data.SizeInBytes

                            buffer.Memory.Mapped (fun dst ->
                                data.Use (fun src ->
                                    Marshal.Copy(src, dst, data.SizeInBytes)
                                )  
                            )
                            buffer, data.Size
                        ) 

                    device.CopyEngine.EnqueueSafe [

                        yield CopyCommand.TransformLayout(result.[ImageAspect.Color], VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal)

                        for level in 0 .. tempImages.Length - 1 do
                            let src, size = tempImages.[level]
                            let dst = result.[ImageAspect.Color, level, 0]
                            yield CopyCommand.Copy(
                                src.Handle, 0L, 
                                result.Handle, VkImageLayout.TransferDstOptimal, result.Format,
                                VkBufferImageCopy(
                                    0UL,
                                    0u, 0u, 
                                    dst.VkImageSubresourceLayers,
                                    VkOffset3D(0,0,0),
                                    VkExtent3D(size.X, size.Y, size.Z)
                                )
                            )

                        yield CopyCommand.SyncImage(result.[ImageAspect.Color], VkImageLayout.TransferDstOptimal, VkAccessFlags.TransferWriteBit)

                        yield CopyCommand.Release(result.[ImageAspect.Color], VkImageLayout.TransferDstOptimal, device.GraphicsFamily)
                        yield CopyCommand.Callback(fun () -> tempImages |> Array.iter (fst >> device.Delete))
                    ]

                    result.Layout <- VkImageLayout.TransferDstOptimal

                    device.eventually {
                        do! Command.Acquire(result.[ImageAspect.Color], VkImageLayout.TransferDstOptimal, device.TransferFamily)
                        do! Command.TransformLayout(result, VkImageLayout.ShaderReadOnlyOptimal)
                    }

                    result

                | _ ->
                    
                    failf "synchronous upload of NativeTexture not implemented"

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
                (VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.StorageBit)
                device
        
        match device.UploadMode with
            | UploadMode.Async ->
                let imageRange = image.[ImageAspect.Color]
                
                image.Layout <- VkImageLayout.TransferDstOptimal
                device.CopyEngine.EnqueueSafe [
                    CopyCommand.TransformLayout(imageRange, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal)
                    CopyCommand.Copy(temp, imageRange.[0,0])
                    CopyCommand.SyncImage(imageRange, VkImageLayout.TransferDstOptimal, VkAccessFlags.TransferWriteBit)

                    CopyCommand.Release(imageRange, VkImageLayout.TransferDstOptimal, device.GraphicsFamily)
                    CopyCommand.Callback(fun () -> device.Delete temp)
                ]
                
                device.eventually {
                    do! Command.Acquire(imageRange, VkImageLayout.TransferDstOptimal, device.TransferFamily)

                    // generate the mipMaps
                    if info.wantMipMaps then
                        do! Command.GenerateMipMaps image.[ImageAspect.Color]

                    do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)

                }


            | _ -> 
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

            | :? PixTexture3d as t ->
                device |> ofPixVolume t.PixVolume t.TextureParams

            | :? FileTexture as t ->
                device |> ofFile t.FileName t.TextureParams

            | :? INativeTexture as nt ->
                device |> ofNativeImage nt

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

    let downloadLevel3d (src : ImageSubresource) (dst : PixVolume) (device : Device) =
        let format = src.Image.Format
        let sourcePixFormat = PixFormat(VkFormat.expectedType format, VkFormat.toColFormat format)

        let temp = device.CreateTensorImage(V3i(dst.Size.X, dst.Size.Y, dst.Size.Z), sourcePixFormat, false)
        try
            device.GraphicsFamily.run {
                let layout = src.Image.Layout
                do! Command.TransformLayout(src.Image, VkImageLayout.TransferSrcOptimal)
                do! Command.Copy(src, temp)
                do! Command.TransformLayout(src.Image, layout)
            }
            temp.Read(dst)
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
        
    [<Extension>]
    static member inline DownloadLevel(this : Device, src : ImageSubresource, dst : PixVolume) =
        this |> Image.downloadLevel3d src dst

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
        
