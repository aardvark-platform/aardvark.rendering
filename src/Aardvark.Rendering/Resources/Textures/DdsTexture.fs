namespace Aardvark.Rendering

open System
open System.IO
open System.Runtime.InteropServices
open Aardvark.Base

#nowarn "9"

module DdsTexture =

    [<AutoOpen>]
    module private Internals =

        [<Flags>]
        type PixelFormatFlags =
            /// Texture contains alpha data
            | AlphaPixels = 0x1u

            /// Used in some older DDS files for alpha channel only uncompressed data
            | Alpha       = 0x2u

            /// Texture contains compressed RGB data
            | FourCC      = 0x4u

            /// Texture contains uncompressed RGB data
            | RGB         = 0x40u

            /// Used in some older DDS files for YUV uncompressed data
            | YUV         = 0x200u

            /// Used in some older DDS files for single channel color uncompressed data
            | Luminance   = 0x20000u

        type FourCC =
                | DXT1 = 827611204u
                | DXT3 = 861165636u
                | DXT5 = 894720068u
                | DX10 = 808540228u

        [<Struct; StructLayout(LayoutKind.Sequential, Size = 32)>]
        type PixelFormat =
            {
                Size        : uint32
                Flags       : PixelFormatFlags
                FourCC      : FourCC
                RGBBitCount : uint32
                RBitMask    : uint32
                GBitMask    : uint32
                BBitMask    : uint32
                ABitMask    : uint32
            }

            member x.HasAlpha =
                x.Flags.HasFlag(PixelFormatFlags.Alpha) ||
                x.Flags.HasFlag(PixelFormatFlags.AlphaPixels)

            member x.IsCompressed =
                x.Flags.HasFlag(PixelFormatFlags.FourCC)

            member x.HasExtendedHeader =
                x.FourCC.HasFlag(FourCC.DX10)

        /// Flags to indicate which members contain valid data
        [<Flags>]
        type Flags =
            /// Required in every .dds file.
            | Caps        = 0x1u

            /// Required in every .dds file.
            | Height      = 0x2u

            /// Required in every .dds file.
            | Width       = 0x4u

            /// Required when pitch is provided for an uncompressed texture.
            | Pitch       = 0x8u

            /// Required in every .dds file.
            | PixelFormat = 0x1000u

            /// Required in a mipmapped texture.
            | MipMapCount = 0x20000u

            /// Required when pitch is provided for a compressed texture.
            | LinearSize  = 0x80000u

            /// Required in a depth texture.
            | Depth       = 0x800000u

        [<Flags>]
        type Caps =
            /// Optional; must be used on any file that contains more than one surface (a mipmap, a cubic environment map, or mipmapped volume texture).
            | Complex = 0x8u

            /// Optional; should be used for a mipmap.
            | MipMap  = 0x400000u

            /// Required
            | Texture = 0x1000u

        [<Flags>]
        type Caps2 =
            /// Required for a cube map.
            | CubeMap           = 0x200u

            /// Required when these surfaces are stored in a cube map.
            | CubeMapPositiveX  = 0x400u

            /// Required when these surfaces are stored in a cube map.
            | CubeMapNegativeX  = 0x800u

            /// Required when these surfaces are stored in a cube map.
            | CubeMapPositiveY  = 0x1000u

            /// Required when these surfaces are stored in a cube map.
            | CubeMapNegativeY  = 0x2000u

            /// Required when these surfaces are stored in a cube map.
            | CubeMapPositiveZ  = 0x4000u

            /// Required when these surfaces are stored in a cube map.
            | CubeMapNegativeZ  = 0x8000u

            /// Required for a volume texture.
            | Volume            = 0x200000u

        type ResourceDimension =
            | Texture1D = 2u
            | Texture2D = 3u
            | Texture3D = 4u

        [<Flags>]
        type MiscFlags =
            /// Indicates a 2D texture is a cube-map texture.
            | TextureCube   = 0x4u

        type AlphaMode =
            /// Alpha channel content is unknown. This is the value for legacy files, which typically is assumed to be 'straight' alpha.
            | Unknown       = 0u

            /// Any alpha channel content is presumed to use straight alpha.
            | Straight      = 1u

            /// Any alpha channel content is using premultiplied alpha. The only legacy file formats that indicate this information are 'DX2' and 'DX4'.
            | Premultiplied = 2u

            /// Any alpha channel content is all set to fully opaque.
            | Opaque        = 3u

            /// Any alpha channel content is being used as a 4th channel and is not intended to represent transparency (straight or premultiplied).
            | Custom        = 4u

        type DxgiFormat =
            | BC1          = 70u
            | BC1Unorm     = 71u
            | BC1UnormSrgb = 72u
            | BC2          = 73u
            | BC2Unorm     = 74u
            | BC2UnormSrgb = 75u
            | BC3          = 76u
            | BC3Unorm     = 77u
            | BC3UnormSrgb = 78u
            | BC4          = 79u
            | BC4Unorm     = 80u
            | BC4Snorm     = 81u
            | BC5          = 82u
            | BC5Unorm     = 83u
            | BC5Snorm     = 84u
            | BC6H         = 94u
            | BC6HUf16     = 95u
            | BC6HSf16     = 96u
            | BC7          = 97u
            | BC7Unorm     = 98u
            | BC7UnormSrgb = 99u

        [<Struct; StructLayout(LayoutKind.Explicit, Size = 124)>]
        type Header =
            {
                [<FieldOffset(0)>] Size         : uint32
                [<FieldOffset(4)>] Flags        : Flags
                [<FieldOffset(8)>] Height       : uint32
                [<FieldOffset(12)>] Width       : uint32
                [<FieldOffset(20)>] Depth       : uint32
                [<FieldOffset(24)>] MipMapCount : uint32
                [<FieldOffset(72)>] PixelFormat : PixelFormat
                [<FieldOffset(104)>] Caps       : Caps
                [<FieldOffset(108)>] Caps2      : Caps2
            }

            member x.HasExtendedHeader =
                x.PixelFormat.HasExtendedHeader

            member x.Dimension =
                if x.Caps2.HasFlag(Caps2.CubeMap) then TextureDimension.TextureCube
                elif x.Caps2.HasFlag(Caps2.Volume) then TextureDimension.Texture3D
                else TextureDimension.Texture2D

        [<Struct; StructLayout(LayoutKind.Sequential)>]
        type HeaderDxt10 =
            {
                Format            : DxgiFormat
                ResourceDimension : ResourceDimension
                MiscFlag          : MiscFlags
                ArraySize         : uint32
                AlphaMode         : AlphaMode
            }

            member x.Dimension =
                match x.ResourceDimension with
                | ResourceDimension.Texture1D -> TextureDimension.Texture1D
                | ResourceDimension.Texture3D -> TextureDimension.Texture3D
                | _ ->
                    if x.MiscFlag.HasFlag(MiscFlags.TextureCube) then
                        TextureDimension.TextureCube
                    else
                        TextureDimension.Texture2D

        module TextureFormat =

            let bytesPerBlock = function
                | TextureFormat.CompressedRgbS3tcDxt1
                | TextureFormat.CompressedRgbaS3tcDxt1
                | TextureFormat.CompressedSrgbS3tcDxt1
                | TextureFormat.CompressedSrgbAlphaS3tcDxt1
                | TextureFormat.CompressedRedRgtc1
                | TextureFormat.CompressedSignedRedRgtc1 -> 8
                | _ -> 16

            let ofDxgiFormat (withAlpha : bool) = function
                | DxgiFormat.BC1 | DxgiFormat.BC1Unorm ->
                    if withAlpha then TextureFormat.CompressedRgbaS3tcDxt1
                    else TextureFormat.CompressedRgbS3tcDxt1

                | DxgiFormat.BC1UnormSrgb ->
                    if withAlpha then TextureFormat.CompressedSrgbAlphaS3tcDxt1
                    else TextureFormat.CompressedSrgbS3tcDxt1

                | DxgiFormat.BC2 | DxgiFormat.BC2Unorm  -> TextureFormat.CompressedRgbaS3tcDxt3
                | DxgiFormat.BC2UnormSrgb               -> TextureFormat.CompressedSrgbAlphaS3tcDxt3
                | DxgiFormat.BC3 | DxgiFormat.BC3Unorm  -> TextureFormat.CompressedRgbaS3tcDxt5
                | DxgiFormat.BC3UnormSrgb               -> TextureFormat.CompressedSrgbAlphaS3tcDxt5
                | DxgiFormat.BC4 | DxgiFormat.BC4Unorm  -> TextureFormat.CompressedRedRgtc1
                | DxgiFormat.BC4Snorm                   -> TextureFormat.CompressedSignedRedRgtc1
                | DxgiFormat.BC5 | DxgiFormat.BC5Unorm  -> TextureFormat.CompressedRgRgtc2
                | DxgiFormat.BC5Snorm                   -> TextureFormat.CompressedSignedRgRgtc2
                | DxgiFormat.BC6H | DxgiFormat.BC6HUf16 -> TextureFormat.CompressedRgbBptcUnsignedFloat
                | DxgiFormat.BC6HSf16                   -> TextureFormat.CompressedRgbBptcSignedFloat
                | DxgiFormat.BC7 | DxgiFormat.BC7Unorm  -> TextureFormat.CompressedRgbaBptcUnorm
                | DxgiFormat.BC7UnormSrgb               -> TextureFormat.CompressedSrgbAlphaBptcUnorm
                | fmt ->
                    failwithf "Unknown compressed DXGI format: %A" fmt

            let ofFourCC (withAlpha : bool) = function
                | FourCC.DXT1 -> if withAlpha then TextureFormat.CompressedRgbaS3tcDxt1 else TextureFormat.CompressedRgbS3tcDxt1
                | FourCC.DXT3 -> TextureFormat.CompressedRgbaS3tcDxt3
                | FourCC.DXT5 -> TextureFormat.CompressedRgbaS3tcDxt5
                | fcc ->
                    if Enum.IsDefined(typeof<DxgiFormat>, uint32 fcc) then
                        fcc |> unbox<DxgiFormat> |> ofDxgiFormat withAlpha
                    else
                        failwithf "Unexpected four character code %A" fcc

    [<AutoOpen>]
    module BinaryStreamReading =

        let readRaw (sizeInBytes : int) (message : string) (stream : Stream) =
            let buffer = Array.zeroCreate<uint8> sizeInBytes

            let rec copy (offset : int) (sizeInBytes : int) : unit =
                let n = stream.Read(buffer, offset, sizeInBytes)

                if n < sizeInBytes then
                    if n = 0 then
                        failwithf "unexpected DDS stream end when reading %s" message

                    copy n (sizeInBytes - n)

            copy 0 sizeInBytes
            buffer

        let read<'T> (message : string) (stream : Stream) =
            let buffer = stream |> readRaw sizeof<'T> message

            let handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
                unbox<'T> <| Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof<'T>);
            finally
                handle.Free();

    let loadCompressedFromStream (stream : Stream) =

        let magic = stream |> read<uint32> "magic number"
        if magic <> 0x20534444u then
            failwithf "Unexpected magic number %A" magic

        let header = stream |> read<Header> "header"
        if int header.Size <> sizeof<Header> then
            failwithf "Unexpected header size %d" header.Size

        if not header.PixelFormat.IsCompressed then
            failwithf "Not compressed"

        let header10 =
            if header.HasExtendedHeader then
                Some (stream |> read<HeaderDxt10> "extended header")
            else
                None

        let dimension =
            match header10 with
            | Some h -> h.Dimension
            | _ -> header.Dimension

        let size =
            match dimension with
            | TextureDimension.Texture1D -> V3i(int header.Width, 1, 1)
            | TextureDimension.Texture3D -> V3i(int header.Width, int header.Height, int header.Depth)
            | _ -> V3i(int header.Width, int header.Height, 1)

        let format =
            match header10 with
            | Some h -> h.Format |> TextureFormat.ofDxgiFormat header.PixelFormat.HasAlpha
            | _ -> header.PixelFormat.FourCC |> TextureFormat.ofFourCC header.PixelFormat.HasAlpha

        let levels =
            if header.Flags.HasFlag(Flags.MipMapCount) || header.Caps.HasFlag(Caps.MipMap) then int header.MipMapCount
            else 1

        let count =
            header10 |> Option.map (fun h -> int h.ArraySize) |> Option.defaultValue 1

        let slices =
            match dimension with
            | TextureDimension.TextureCube -> count * 6
            | _ -> count

        let mutable totalSize = 0

        let metaData =
            let bytesPerBlock =
                format |> TextureFormat.bytesPerBlock

            Array.init slices (fun layer ->
                Array.init levels (fun level ->
                    let size = Fun.MipmapLevelSize(size, level)
                    let blocks = max 1 ((size + 3) / 4)
                    let sizeInBytes = blocks.X * blocks.Y * blocks.Z * bytesPerBlock
                    let offset = totalSize
                    totalSize <- totalSize + sizeInBytes

                    (size, blocks, nativeint offset, sizeInBytes)
                )
            )

        let data = stream |> readRaw totalSize "texture data"

        let layers =
            Array.init slices (fun layer ->
                Array.init levels (fun level ->
                    let size, _, offset, sizeInBytes = metaData.[layer].[level]

                    { new INativeTextureData with
                        member x.Size = size
                        member x.SizeInBytes = int64 sizeInBytes
                        member x.Use(f) = pinned data (fun ptr -> f (ptr + offset)) }
                )
            )

        { new INativeTexture with
            member x.Dimension    = dimension
            member x.Format       = format
            member x.MipMapLevels = levels
            member x.Count        = count
            member x.WantMipMaps  = levels > 1
            member x.Item
                with get(slice, level) = layers.[slice].[level] }

    let tryLoadCompressedFromStream (stream : Stream) =
        try
            Some <| loadCompressedFromStream stream
        with
        | _ ->
            None