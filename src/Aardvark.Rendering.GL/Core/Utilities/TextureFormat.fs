namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module internal TextureFormatExtensions =

    module TextureFormat =

        let toFormatAndType =
            let table =
                LookupTable.lookupTable' [
                    TextureFormat.Bgr8,    (PixelFormat.Bgr, PixelType.UnsignedByte)
                    TextureFormat.Bgra8,   (PixelFormat.Bgra, PixelType.UnsignedByte)
                    TextureFormat.Rgb8,    (PixelFormat.Rgb, PixelType.UnsignedByte)
                    TextureFormat.Rgb16,   (PixelFormat.Rgb, PixelType.UnsignedShort)
                    TextureFormat.Rgba8,   (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.Rgb10A2, (PixelFormat.Rgba, PixelType.UnsignedInt1010102)
                    TextureFormat.Rgba16,  (PixelFormat.Rgba, PixelType.UnsignedShort)

                    TextureFormat.R8,     (PixelFormat.Red, PixelType.UnsignedByte)
                    TextureFormat.R16,    (PixelFormat.Red, PixelType.UnsignedShort)
                    TextureFormat.Rg8,    (PixelFormat.Rg, PixelType.UnsignedByte)
                    TextureFormat.Rg16,   (PixelFormat.Rg, PixelType.UnsignedShort)
                    TextureFormat.R16f,   (PixelFormat.Red, PixelType.HalfFloat)
                    TextureFormat.R32f,   (PixelFormat.Red, PixelType.Float)
                    TextureFormat.Rg16f,  (PixelFormat.Rg, PixelType.HalfFloat)
                    TextureFormat.Rg32f,  (PixelFormat.Rg, PixelType.Float)
                    TextureFormat.R8i,    (PixelFormat.RedInteger, PixelType.Byte)
                    TextureFormat.R8ui,   (PixelFormat.RedInteger, PixelType.UnsignedByte)
                    TextureFormat.R16i,   (PixelFormat.RedInteger, PixelType.Short)
                    TextureFormat.R16ui,  (PixelFormat.RedInteger, PixelType.UnsignedShort)
                    TextureFormat.R32i,   (PixelFormat.RedInteger, PixelType.Int)
                    TextureFormat.R32ui,  (PixelFormat.RedInteger, PixelType.UnsignedInt)
                    TextureFormat.Rg8i,   (PixelFormat.RgInteger, PixelType.Byte)
                    TextureFormat.Rg8ui,  (PixelFormat.RgInteger, PixelType.UnsignedByte)
                    TextureFormat.Rg16i,  (PixelFormat.RgInteger, PixelType.Short)
                    TextureFormat.Rg16ui, (PixelFormat.RgInteger, PixelType.UnsignedShort)
                    TextureFormat.Rg32i,  (PixelFormat.RgInteger, PixelType.Int)
                    TextureFormat.Rg32ui, (PixelFormat.RgInteger, PixelType.UnsignedInt)

                    TextureFormat.CompressedRgbS3tcDxt1,          (PixelFormat.Rgb, PixelType.UnsignedByte)
                    TextureFormat.CompressedRgbaS3tcDxt1,         (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.CompressedRgbaS3tcDxt3,         (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.CompressedRgbaS3tcDxt5,         (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.CompressedSrgbS3tcDxt1,         (PixelFormat.Rgb, PixelType.UnsignedByte)
                    TextureFormat.CompressedSrgbAlphaS3tcDxt1,    (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.CompressedSrgbAlphaS3tcDxt3,    (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.CompressedSrgbAlphaS3tcDxt5,    (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.CompressedRedRgtc1,             (PixelFormat.Red, PixelType.UnsignedByte)
                    TextureFormat.CompressedSignedRedRgtc1,       (PixelFormat.Red, PixelType.Byte)
                    TextureFormat.CompressedRgRgtc2,              (PixelFormat.Rg, PixelType.UnsignedByte)
                    TextureFormat.CompressedSignedRgRgtc2,        (PixelFormat.Rg, PixelType.Byte)
                    TextureFormat.CompressedRgbaBptcUnorm,        (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.CompressedRgbBptcSignedFloat,   (PixelFormat.Rgb, PixelType.Float)
                    TextureFormat.CompressedRgbBptcUnsignedFloat, (PixelFormat.Rgb, PixelType.Float)

                    TextureFormat.StencilIndex8,     (PixelFormat.StencilIndex, PixelType.UnsignedByte)

                    TextureFormat.DepthComponent16,  (PixelFormat.DepthComponent, PixelType.UnsignedShort)
                    TextureFormat.DepthComponent24,  (PixelFormat.DepthComponent, PixelType.UnsignedInt)
                    TextureFormat.DepthComponent32,  (PixelFormat.DepthComponent, PixelType.UnsignedInt)
                    TextureFormat.DepthComponent32f, (PixelFormat.DepthComponent, PixelType.Float)

                    TextureFormat.Depth24Stencil8,   (PixelFormat.DepthStencil, PixelType.UnsignedInt248)
                    TextureFormat.Depth32fStencil8,  (PixelFormat.DepthStencil, PixelType.Float32UnsignedInt248Rev)

                    TextureFormat.Rgba32f,     (PixelFormat.Rgba, PixelType.Float)
                    TextureFormat.Rgb32f,      (PixelFormat.Rgb, PixelType.Float)
                    TextureFormat.Rgba16f,     (PixelFormat.Rgba, PixelType.HalfFloat)
                    TextureFormat.Rgb16f,      (PixelFormat.Rgb, PixelType.HalfFloat)
                    TextureFormat.Srgb8,       (PixelFormat.Rgb, PixelType.UnsignedByte)
                    TextureFormat.Srgb8Alpha8, (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.Rgba32ui,    (PixelFormat.RgbaInteger, PixelType.UnsignedInt)
                    TextureFormat.Rgb32ui,     (PixelFormat.RgbInteger, PixelType.UnsignedInt)
                    TextureFormat.Rgba16ui,    (PixelFormat.RgbaInteger, PixelType.UnsignedShort)
                    TextureFormat.Rgb16ui,     (PixelFormat.RgbInteger, PixelType.UnsignedShort)
                    TextureFormat.Rgba8ui,     (PixelFormat.RgbaInteger, PixelType.UnsignedByte)
                    TextureFormat.Rgb8ui,      (PixelFormat.RgbInteger, PixelType.UnsignedByte)
                    TextureFormat.Rgba32i,     (PixelFormat.RgbaInteger, PixelType.Int)
                    TextureFormat.Rgb32i,      (PixelFormat.RgbInteger, PixelType.Int)
                    TextureFormat.Rgba16i,     (PixelFormat.RgbaInteger, PixelType.Short)
                    TextureFormat.Rgb16i,      (PixelFormat.RgbInteger, PixelType.Short)
                    TextureFormat.Rgba8i,      (PixelFormat.RgbaInteger, PixelType.Byte)
                    TextureFormat.Rgb8i,       (PixelFormat.RgbInteger, PixelType.Byte)
                    TextureFormat.R8Snorm,     (PixelFormat.Red, PixelType.Byte)
                    TextureFormat.Rg8Snorm,    (PixelFormat.Rg, PixelType.Byte)
                    TextureFormat.Rgb8Snorm,   (PixelFormat.Rgb, PixelType.Byte)
                    TextureFormat.Rgba8Snorm,  (PixelFormat.Rgba, PixelType.Byte)
                    TextureFormat.R16Snorm,    (PixelFormat.Red, PixelType.Short)
                    TextureFormat.Rg16Snorm,   (PixelFormat.Rg, PixelType.Short)
                    TextureFormat.Rgb16Snorm,  (PixelFormat.Rgb, PixelType.Short)
                    TextureFormat.Rgba16Snorm, (PixelFormat.Rgba, PixelType.Short)
                ]

            fun fmt ->
                match fmt |> table with
                | Some res -> res
                | _ -> failwithf "Conversion from format %A supported" fmt