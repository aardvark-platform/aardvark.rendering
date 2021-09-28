namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4

module internal PixelType =

    let size =
        LookupTable.lookupTable [
            PixelType.UnsignedByte,  1
            PixelType.Byte,          1
            PixelType.UnsignedShort, 2
            PixelType.Short,         2
            PixelType.UnsignedInt,   4
            PixelType.Int,           4
            PixelType.HalfFloat,     2
            PixelType.Float,         4
        ]

    let ofType =
        LookupTable.lookupTable' [
            typeof<uint8>,   PixelType.UnsignedByte
            typeof<int8>,    PixelType.Byte
            typeof<uint16>,  PixelType.UnsignedShort
            typeof<int16>,   PixelType.Short
            typeof<uint32>,  PixelType.UnsignedInt
            typeof<int32>,   PixelType.Int
            typeof<float16>, PixelType.HalfFloat
            typeof<float32>, PixelType.Float
        ]

    let compressedFormat =
        LookupTable.lookupTable' [
            (PixelFormat.Rgb, PixelType.UnsignedByte, false), (TextureFormat.CompressedRgbS3tcDxt1Ext, PixelInternalFormat.CompressedRgbS3tcDxt1Ext)
            (PixelFormat.Rgba, PixelType.UnsignedByte, false), (TextureFormat.CompressedRgbaS3tcDxt5Ext, PixelInternalFormat.CompressedRgbaS3tcDxt5Ext)
            (PixelFormat.Rgb, PixelType.UnsignedByte, true), (TextureFormat.CompressedSrgbS3tcDxt1Ext, PixelInternalFormat.CompressedSrgbS3tcDxt1Ext)
            (PixelFormat.Rgba, PixelType.UnsignedByte, true), (TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext, PixelInternalFormat.CompressedSrgbAlphaS3tcDxt5Ext)
            (PixelFormat.Bgr, PixelType.UnsignedByte, false), (TextureFormat.CompressedRgbS3tcDxt1Ext, PixelInternalFormat.CompressedRgbS3tcDxt1Ext)
            (PixelFormat.Bgra, PixelType.UnsignedByte, false), (TextureFormat.CompressedRgbaS3tcDxt5Ext, PixelInternalFormat.CompressedRgbaS3tcDxt5Ext)
            (PixelFormat.Bgr, PixelType.UnsignedByte, true), (TextureFormat.CompressedSrgbS3tcDxt1Ext, PixelInternalFormat.CompressedSrgbS3tcDxt1Ext)
            (PixelFormat.Bgra, PixelType.UnsignedByte, true), (TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext, PixelInternalFormat.CompressedSrgbAlphaS3tcDxt5Ext)
            (PixelFormat.Luminance, PixelType.UnsignedByte, false), (TextureFormat.CompressedRedRgtc1, PixelInternalFormat.CompressedRedRgtc1)
        ]

[<AutoOpen>]
module internal PixelTypeExtensions =

    type PixelType with
        member x.Size = PixelType.size x
