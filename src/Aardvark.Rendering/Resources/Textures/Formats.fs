namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open System

type TextureDimension =
    | Texture1D = 1
    | Texture2D = 2
    | TextureCube = 3
    | Texture3D = 4

type TextureFormat =
    | Bgr8 = 1234
    | Bgra8 = 1235
    | R3G3B2 = 10768
    | Rgb4 = 32847
    | Rgb5 = 32848
    | Rgb8 = 32849
    | Rgb10 = 32850
    | Rgb12 = 32851
    | Rgb16 = 32852
    | Rgba2 = 32853
    | Rgba4 = 32854
    | Rgb5A1 = 32855
    | Rgba8 = 32856
    | Rgb10A2 = 32857
    | Rgba12 = 32858
    | Rgba16 = 32859
    | R8 = 33321
    | R16 = 33322
    | Rg8 = 33323
    | Rg16 = 33324
    | R16f = 33325
    | R32f = 33326
    | Rg16f = 33327
    | Rg32f = 33328
    | R8i = 33329
    | R8ui = 33330
    | R16i = 33331
    | R16ui = 33332
    | R32i = 33333
    | R32ui = 33334
    | Rg8i = 33335
    | Rg8ui = 33336
    | Rg16i = 33337
    | Rg16ui = 33338
    | Rg32i = 33339
    | Rg32ui = 33340
    | Rgba32f = 34836
    | Rgb32f = 34837
    | Rgba16f = 34842
    | Rgb16f = 34843
    | R11fG11fB10f = 35898
    | Rgb9E5 = 35901
    | Srgb8 = 35905
    | Srgb8Alpha8 = 35907
    | Rgba32ui = 36208
    | Rgb32ui = 36209
    | Rgba16ui = 36214
    | Rgb16ui = 36215
    | Rgba8ui = 36220
    | Rgb8ui = 36221
    | Rgba32i = 36226
    | Rgb32i = 36227
    | Rgba16i = 36232
    | Rgb16i = 36233
    | Rgba8i = 36238
    | Rgb8i = 36239
    | R8Snorm = 36756
    | Rg8Snorm = 36757
    | Rgb8Snorm = 36758
    | Rgba8Snorm = 36759
    | R16Snorm = 36760
    | Rg16Snorm = 36761
    | Rgb16Snorm = 36762
    | Rgba16Snorm = 36763
    | Rgb10A2ui = 36975
    | DepthComponent16 = 33189
    | DepthComponent24 = 33190
    | DepthComponent32 = 33191
    | DepthComponent32f = 36012
    | Depth24Stencil8 = 35056
    | Depth32fStencil8 = 36013
    | StencilIndex8 = 36168
    | CompressedRgbS3tcDxt1 = 33776             // BC1
    | CompressedSrgbS3tcDxt1 = 35916
    | CompressedRgbaS3tcDxt1 = 33777
    | CompressedSrgbAlphaS3tcDxt1 = 35917
    | CompressedRgbaS3tcDxt3 = 33778            // BC2
    | CompressedSrgbAlphaS3tcDxt3 = 35918
    | CompressedRgbaS3tcDxt5 = 33779            // BC3
    | CompressedSrgbAlphaS3tcDxt5 = 35919
    | CompressedRedRgtc1 = 36283                // BC4
    | CompressedSignedRedRgtc1 = 36284
    | CompressedRgRgtc2 = 36285                 // BC5
    | CompressedSignedRgRgtc2 = 36286
    | CompressedRgbBptcSignedFloat = 36494      // BC6h
    | CompressedRgbBptcUnsignedFloat = 36495
    | CompressedRgbaBptcUnorm = 36492           // BC7
    | CompressedSrgbAlphaBptcUnorm = 36493

[<Flags>]
type TextureAspect =
    | None         = 0x00000000
    | Color        = 0x00000001
    | Depth        = 0x00000002
    | Stencil      = 0x00000004
    | DepthStencil = 0x00000006

type TextureParams =
    {
        wantMipMaps : bool
        wantSrgb : bool
        wantCompressed : bool
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TextureParams =

    let empty = { wantMipMaps = false; wantSrgb = false; wantCompressed = false}
    let srgb = { wantMipMaps = false; wantSrgb = true; wantCompressed = false}
    let compressed = { wantMipMaps = false; wantSrgb = false; wantCompressed = true }
    let mipmapped = { wantMipMaps = true; wantSrgb = false; wantCompressed = false }
    let mipmappedSrgb = { wantMipMaps = true; wantSrgb = true; wantCompressed = false }
    let mipmappedCompressed = { wantMipMaps = true; wantSrgb = false; wantCompressed = true }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TextureFormat =

    let private srgbFormats =
        HashSet.ofList [
            TextureFormat.Srgb8
            TextureFormat.Srgb8Alpha8

            TextureFormat.CompressedSrgbS3tcDxt1
            TextureFormat.CompressedSrgbAlphaS3tcDxt1
            TextureFormat.CompressedSrgbAlphaS3tcDxt3
            TextureFormat.CompressedSrgbAlphaS3tcDxt5
            TextureFormat.CompressedSrgbAlphaBptcUnorm
        ]

    let isSrgb (fmt : TextureFormat) =
        srgbFormats.Contains fmt

    let private depthFormats =
        HashSet.ofList [
            TextureFormat.DepthComponent16
            TextureFormat.DepthComponent24
            TextureFormat.DepthComponent32
            TextureFormat.DepthComponent32f
        ]

    let private stencilFormats =
        HashSet.ofList [
            TextureFormat.StencilIndex8
        ]

    let private depthStencilFormats =
        HashSet.ofList [
            TextureFormat.Depth24Stencil8
            TextureFormat.Depth32fStencil8
        ]

    /// Returns whether the given format is a depth format.
    let isDepth (fmt : TextureFormat) =
        depthFormats.Contains fmt

    /// Returns whether the given format is a stencil format.
    let isStencil (fmt : TextureFormat) =
        stencilFormats.Contains fmt

    /// Returns whether the given format is a combined depth-stencil format.
    let isDepthStencil (fmt : TextureFormat) =
        depthStencilFormats.Contains fmt

    /// Returns whether the given format has a depth component (i.e. it is
    /// either a depth for a combined depth-stencil format).
    let hasDepth (fmt : TextureFormat) =
        isDepth fmt || isDepthStencil fmt

    /// Returns whether the given format has a stencil component (i.e. it is
    /// either a stencil for a combined depth-stencil format).
    let hasStencil (fmt : TextureFormat) =
        isStencil fmt || isDepthStencil fmt

    /// Returns the aspect of the given texture format.
    let toAspect (fmt : TextureFormat) =
        if isDepthStencil fmt then TextureAspect.DepthStencil
        elif isDepth fmt then TextureAspect.Depth
        elif isStencil fmt then TextureAspect.Stencil
        else TextureAspect.Color

    let ofPixFormat =

        let buildLookup (rgb, srgb) = function
            | { wantSrgb = true } -> srgb
            | _ -> rgb

        let rgb8 = buildLookup(TextureFormat.Rgb8, TextureFormat.Srgb8)
        let rgba8 = buildLookup(TextureFormat.Rgba8, TextureFormat.Srgb8Alpha8)

        LookupTable.lookupTable [
            PixFormat.ByteBGR  , rgb8
            PixFormat.ByteBGRA , rgba8
            PixFormat.ByteBGRP , rgba8
            PixFormat.ByteRGB  , rgb8
            PixFormat.ByteRGBA , rgba8
            PixFormat.ByteRGBP , rgba8
            PixFormat.ByteGray , (fun _ -> TextureFormat.R8)

            PixFormat.UShortRGB  ,  (fun _ -> TextureFormat.Rgb16)
            PixFormat.UShortRGBA ,  (fun _ -> TextureFormat.Rgba16)
            PixFormat.UShortRGBP ,  (fun _ -> TextureFormat.Rgba16)
            PixFormat.UShortBGR  ,  (fun _ -> TextureFormat.Rgb16)
            PixFormat.UShortBGRA ,  (fun _ -> TextureFormat.Rgba16)
            PixFormat.UShortBGRP ,  (fun _ -> TextureFormat.Rgba16)
            PixFormat.UShortGray ,  (fun _ -> TextureFormat.R16)

            PixFormat(typeof<float16>, Col.Format.Gray)     , (fun _ -> TextureFormat.R16f)
            PixFormat(typeof<float16>, Col.Format.NormalUV) , (fun _ -> TextureFormat.Rg16f)
            PixFormat(typeof<float16>, Col.Format.RGB)      , (fun _ -> TextureFormat.Rgb16f)
            PixFormat(typeof<float16>, Col.Format.RGBA)     , (fun _ -> TextureFormat.Rgba16f)

            PixFormat(typeof<float32>, Col.Format.Gray)     , (fun _ -> TextureFormat.R32f)
            PixFormat(typeof<float32>, Col.Format.NormalUV) , (fun _ -> TextureFormat.Rg32f)
            PixFormat(typeof<float32>, Col.Format.RGB)      , (fun _ -> TextureFormat.Rgb32f)
            PixFormat(typeof<float32>, Col.Format.RGBA)     , (fun _ -> TextureFormat.Rgba32f)

            PixFormat(typeof<int8>, Col.Format.Gray)        , (fun _ -> TextureFormat.R8Snorm)
            PixFormat(typeof<int8>, Col.Format.NormalUV)    , (fun _ -> TextureFormat.Rg8Snorm)
            PixFormat(typeof<int8>, Col.Format.RGB)         , (fun _ -> TextureFormat.Rgb8Snorm)
            PixFormat(typeof<int8>, Col.Format.RGBA)        , (fun _ -> TextureFormat.Rgba8Snorm)

            PixFormat(typeof<int16>, Col.Format.Gray)       , (fun _ -> TextureFormat.R16Snorm)
            PixFormat(typeof<int16>, Col.Format.NormalUV)   , (fun _ -> TextureFormat.Rg16Snorm)
            PixFormat(typeof<int16>, Col.Format.RGB)        , (fun _ -> TextureFormat.Rgb16Snorm)
            PixFormat(typeof<int16>, Col.Format.RGBA)       , (fun _ -> TextureFormat.Rgba16Snorm)

            PixFormat(typeof<int32>, Col.Format.Gray)       , (fun _ -> TextureFormat.R32i)
            PixFormat(typeof<int32>, Col.Format.NormalUV)   , (fun _ -> TextureFormat.Rg32i)
            PixFormat(typeof<int32>, Col.Format.RGB)        , (fun _ -> TextureFormat.Rgb32i)
            PixFormat(typeof<int32>, Col.Format.RGBA)       , (fun _ -> TextureFormat.Rgba32i)

            PixFormat(typeof<uint32>, Col.Format.Gray)      , (fun _ -> TextureFormat.R32ui)
            PixFormat(typeof<uint32>, Col.Format.NormalUV)  , (fun _ -> TextureFormat.Rg32ui)
            PixFormat(typeof<uint32>, Col.Format.RGB)       , (fun _ -> TextureFormat.Rgb32ui)
            PixFormat(typeof<uint32>, Col.Format.RGBA)      , (fun _ -> TextureFormat.Rgba32ui)
        ]

    let private integerFormats =
        HashSet.ofList [
            TextureFormat.R8i
            TextureFormat.R8ui
            TextureFormat.R16i
            TextureFormat.R16ui
            TextureFormat.R32i
            TextureFormat.R32ui
            TextureFormat.Rg8i
            TextureFormat.Rg8ui
            TextureFormat.Rg16i
            TextureFormat.Rg16ui
            TextureFormat.Rg32i
            TextureFormat.Rg32ui
            TextureFormat.Rgba32ui
            TextureFormat.Rgb32ui
            TextureFormat.Rgba16ui
            TextureFormat.Rgb16ui
            TextureFormat.Rgba8ui
            TextureFormat.Rgb8ui
            TextureFormat.Rgba32i
            TextureFormat.Rgb32i
            TextureFormat.Rgba16i
            TextureFormat.Rgb16i
            TextureFormat.Rgba8i
            TextureFormat.Rgb8i
        ]

    let isIntegerFormat (format : TextureFormat) =
        integerFormats |> HashSet.contains format

    let private signedFormats =
        HashSet.ofList [
            TextureFormat.R16f
            TextureFormat.R32f
            TextureFormat.Rg16f
            TextureFormat.Rg32f
            TextureFormat.R8i
            TextureFormat.R16i
            TextureFormat.R32i
            TextureFormat.Rg8i
            TextureFormat.Rg16i
            TextureFormat.Rg32i
            TextureFormat.Rgba32f
            TextureFormat.Rgb32f
            TextureFormat.Rgba16f
            TextureFormat.Rgb16f
            TextureFormat.R11fG11fB10f
            TextureFormat.Rgba32i
            TextureFormat.Rgb32i
            TextureFormat.Rgba16i
            TextureFormat.Rgb16i
            TextureFormat.Rgba8i
            TextureFormat.Rgb8i
            TextureFormat.R8Snorm
            TextureFormat.Rg8Snorm
            TextureFormat.Rgb8Snorm
            TextureFormat.Rgba8Snorm
            TextureFormat.R16Snorm
            TextureFormat.Rg16Snorm
            TextureFormat.Rgb16Snorm
            TextureFormat.Rgba16Snorm
            TextureFormat.DepthComponent32f
            TextureFormat.CompressedSignedRedRgtc1          // BC4
            TextureFormat.CompressedSignedRgRgtc2           // BC5
            TextureFormat.CompressedRgbBptcSignedFloat      // BC6h
        ]

    /// Returns if the given format stores signed values (i.e. floating-point, signed integer, or signed normalized values)
    let isSigned (format : TextureFormat) =
        signedFormats |> HashSet.contains format

    let private compressedFormats =
        HashSet.ofList [
            TextureFormat.CompressedRgbS3tcDxt1
            TextureFormat.CompressedSrgbS3tcDxt1
            TextureFormat.CompressedRgbaS3tcDxt1
            TextureFormat.CompressedSrgbAlphaS3tcDxt1
            TextureFormat.CompressedRgbaS3tcDxt3
            TextureFormat.CompressedSrgbAlphaS3tcDxt3
            TextureFormat.CompressedRgbaS3tcDxt5
            TextureFormat.CompressedSrgbAlphaS3tcDxt5
            TextureFormat.CompressedRedRgtc1
            TextureFormat.CompressedSignedRedRgtc1
            TextureFormat.CompressedRgRgtc2
            TextureFormat.CompressedSignedRgRgtc2
            TextureFormat.CompressedRgbaBptcUnorm
            TextureFormat.CompressedSrgbAlphaBptcUnorm
            TextureFormat.CompressedRgbBptcSignedFloat
            TextureFormat.CompressedRgbBptcUnsignedFloat
        ]

    let isCompressed (fmt : TextureFormat) =
        compressedFormats |> HashSet.contains fmt

    let toCompressed =
        LookupTable.lookupTable' [
            TextureFormat.Rgb8, TextureFormat.CompressedRgbS3tcDxt1
            TextureFormat.Rgb8ui, TextureFormat.CompressedRgbS3tcDxt1
            TextureFormat.Srgb8, TextureFormat.CompressedSrgbS3tcDxt1

            TextureFormat.R8, TextureFormat.CompressedRedRgtc1
            TextureFormat.R8ui, TextureFormat.CompressedRedRgtc1
            TextureFormat.R8i, TextureFormat.CompressedSignedRedRgtc1
            TextureFormat.R8Snorm, TextureFormat.CompressedSignedRedRgtc1

            TextureFormat.Rg8, TextureFormat.CompressedRgRgtc2
            TextureFormat.Rg8ui, TextureFormat.CompressedRgRgtc2
            TextureFormat.Rg8i, TextureFormat.CompressedSignedRgRgtc2
            TextureFormat.Rg8Snorm, TextureFormat.CompressedSignedRgRgtc2

            TextureFormat.Rgba8, TextureFormat.CompressedRgbaS3tcDxt5
            TextureFormat.Rgba8ui, TextureFormat.CompressedRgbaS3tcDxt5
            TextureFormat.Srgb8Alpha8, TextureFormat.CompressedSrgbAlphaS3tcDxt5
        ]

    [<Obsolete>]
    let isFilterable (fmt : TextureFormat) =
        not (isIntegerFormat fmt || isCompressed fmt)

    let toDownloadFormat =
        LookupTable.lookupTable [
            TextureFormat.R8, PixFormat.ByteGray
            TextureFormat.Bgr8, PixFormat.ByteBGR
            TextureFormat.Bgra8, PixFormat.ByteBGRA
            TextureFormat.Rgba8, PixFormat.ByteRGBA
            TextureFormat.Rgb8, PixFormat.ByteRGB

            TextureFormat.Srgb8, PixFormat.ByteRGB
            TextureFormat.Srgb8Alpha8, PixFormat.ByteRGBA

            TextureFormat.R16, PixFormat.UShortGray
            TextureFormat.Rgba16, PixFormat.UShortRGBA
            TextureFormat.Rgb16, PixFormat.UShortRGB

            TextureFormat.R16f, PixFormat(typeof<float16>, Col.Format.Gray)
            TextureFormat.Rg16f, PixFormat(typeof<float16>, Col.Format.NormalUV)
            TextureFormat.Rgb16f, PixFormat(typeof<float16>, Col.Format.RGB)
            TextureFormat.Rgba16f, PixFormat(typeof<float16>, Col.Format.RGBA)

            TextureFormat.R32f, PixFormat(typeof<float32>, Col.Format.Gray)
            TextureFormat.Rg32f, PixFormat(typeof<float32>, Col.Format.NormalUV)
            TextureFormat.Rgb32f, PixFormat(typeof<float32>, Col.Format.RGB)
            TextureFormat.Rgba32f, PixFormat(typeof<float32>, Col.Format.RGBA)

            TextureFormat.R8Snorm, PixFormat(typeof<int8>, Col.Format.Gray)
            TextureFormat.Rg8Snorm, PixFormat(typeof<int8>, Col.Format.NormalUV)
            TextureFormat.Rgb8Snorm, PixFormat(typeof<int8>, Col.Format.RGB)
            TextureFormat.Rgba8Snorm, PixFormat(typeof<int8>, Col.Format.RGBA)

            TextureFormat.R16Snorm, PixFormat(typeof<int16>, Col.Format.Gray)
            TextureFormat.Rg16Snorm, PixFormat(typeof<int16>, Col.Format.NormalUV)
            TextureFormat.Rgb16Snorm, PixFormat(typeof<int16>, Col.Format.RGB)
            TextureFormat.Rgba16Snorm, PixFormat(typeof<int16>, Col.Format.RGBA)

            TextureFormat.R8i, PixFormat(typeof<int8>, Col.Format.Gray)
            TextureFormat.Rg8i, PixFormat(typeof<int8>, Col.Format.NormalUV)
            TextureFormat.Rgb8i, PixFormat(typeof<int8>, Col.Format.RGB)
            TextureFormat.Rgba8i, PixFormat(typeof<int8>, Col.Format.RGBA)

            TextureFormat.R8ui, PixFormat(typeof<uint8>, Col.Format.Gray)
            TextureFormat.Rg8ui, PixFormat(typeof<uint8>, Col.Format.NormalUV)
            TextureFormat.Rgb8ui, PixFormat(typeof<uint8>, Col.Format.RGB)
            TextureFormat.Rgba8ui, PixFormat(typeof<uint8>, Col.Format.RGBA)

            TextureFormat.R16i, PixFormat(typeof<int16>, Col.Format.Gray)
            TextureFormat.Rg16i, PixFormat(typeof<int16>, Col.Format.NormalUV)
            TextureFormat.Rgb16i, PixFormat(typeof<int16>, Col.Format.RGB)
            TextureFormat.Rgba16i, PixFormat(typeof<int16>, Col.Format.RGBA)

            TextureFormat.R16ui, PixFormat(typeof<uint16>, Col.Format.Gray)
            TextureFormat.Rg16ui, PixFormat(typeof<uint16>, Col.Format.NormalUV)
            TextureFormat.Rgb16ui, PixFormat(typeof<uint16>, Col.Format.RGB)
            TextureFormat.Rgba16ui, PixFormat(typeof<uint16>, Col.Format.RGBA)

            TextureFormat.R32i, PixFormat(typeof<int>, Col.Format.Gray)
            TextureFormat.Rg32i, PixFormat(typeof<int>, Col.Format.NormalUV)
            TextureFormat.Rgb32i, PixFormat(typeof<int>, Col.Format.RGB)
            TextureFormat.Rgba32i, PixFormat(typeof<int>, Col.Format.RGBA)

            TextureFormat.R32ui, PixFormat(typeof<uint32>, Col.Format.Gray)
            TextureFormat.Rg32ui, PixFormat(typeof<uint32>, Col.Format.NormalUV)
            TextureFormat.Rgb32ui, PixFormat(typeof<uint32>, Col.Format.RGB)
            TextureFormat.Rgba32ui, PixFormat(typeof<uint32>, Col.Format.RGBA)

            TextureFormat.CompressedRgbS3tcDxt1, PixFormat.ByteRGB
            TextureFormat.CompressedSrgbS3tcDxt1, PixFormat.ByteRGB
            TextureFormat.CompressedRgbaS3tcDxt1, PixFormat.ByteRGBA
            TextureFormat.CompressedSrgbAlphaS3tcDxt1, PixFormat.ByteRGBA

            TextureFormat.CompressedRgbaS3tcDxt3, PixFormat.ByteRGBA
            TextureFormat.CompressedSrgbAlphaS3tcDxt3, PixFormat.ByteRGBA

            TextureFormat.CompressedRgbaS3tcDxt5, PixFormat.ByteRGBA
            TextureFormat.CompressedSrgbAlphaS3tcDxt5, PixFormat.ByteRGBA

            TextureFormat.CompressedRedRgtc1, PixFormat.ByteGray
            TextureFormat.CompressedSignedRedRgtc1, PixFormat.SByteGray
            TextureFormat.CompressedRgRgtc2, PixFormat.ByteRGB
            TextureFormat.CompressedSignedRgRgtc2, PixFormat.SByteRGB
            TextureFormat.CompressedRgbaBptcUnorm, PixFormat.ByteRGBA
            TextureFormat.CompressedSrgbAlphaBptcUnorm, PixFormat.ByteRGBA
            TextureFormat.CompressedRgbBptcSignedFloat, PixFormat.FloatRGB
            TextureFormat.CompressedRgbBptcUnsignedFloat, PixFormat.FloatRGB
        ]

    let toColFormat =
        LookupTable.lookupTable [
            TextureFormat.Bgr8,                           Col.Format.BGR
            TextureFormat.Bgra8,                          Col.Format.BGRA
            TextureFormat.R3G3B2,                         Col.Format.RGB
            TextureFormat.Rgb4,                           Col.Format.RGB
            TextureFormat.Rgb5,                           Col.Format.RGB
            TextureFormat.Rgb8,                           Col.Format.RGB
            TextureFormat.Rgb10,                          Col.Format.RGB
            TextureFormat.Rgb12,                          Col.Format.RGB
            TextureFormat.Rgb16,                          Col.Format.RGB
            TextureFormat.Rgba2,                          Col.Format.RGBA
            TextureFormat.Rgba4,                          Col.Format.RGBA
            TextureFormat.Rgb5A1,                         Col.Format.RGBA
            TextureFormat.Rgba8,                          Col.Format.RGBA
            TextureFormat.Rgb10A2,                        Col.Format.RGBA
            TextureFormat.Rgba12,                         Col.Format.RGBA
            TextureFormat.Rgba16,                         Col.Format.RGBA
            TextureFormat.R8,                             Col.Format.Gray
            TextureFormat.R16,                            Col.Format.Gray
            TextureFormat.Rg8,                            Col.Format.NormalUV
            TextureFormat.Rg16,                           Col.Format.NormalUV
            TextureFormat.R16f,                           Col.Format.Gray
            TextureFormat.R32f,                           Col.Format.Gray
            TextureFormat.Rg16f,                          Col.Format.NormalUV
            TextureFormat.Rg32f,                          Col.Format.NormalUV
            TextureFormat.R8i,                            Col.Format.Gray
            TextureFormat.R8ui,                           Col.Format.Gray
            TextureFormat.R16i,                           Col.Format.Gray
            TextureFormat.R16ui,                          Col.Format.Gray
            TextureFormat.R32i,                           Col.Format.Gray
            TextureFormat.R32ui,                          Col.Format.Gray
            TextureFormat.Rg8i,                           Col.Format.NormalUV
            TextureFormat.Rg8ui,                          Col.Format.NormalUV
            TextureFormat.Rg16i,                          Col.Format.NormalUV
            TextureFormat.Rg16ui,                         Col.Format.NormalUV
            TextureFormat.Rg32i,                          Col.Format.NormalUV
            TextureFormat.Rg32ui,                         Col.Format.NormalUV
            TextureFormat.Rgba32f,                        Col.Format.RGBA
            TextureFormat.Rgb32f,                         Col.Format.RGB
            TextureFormat.Rgba16f,                        Col.Format.RGBA
            TextureFormat.Rgb16f,                         Col.Format.RGB
            TextureFormat.R11fG11fB10f,                   Col.Format.RGB
            TextureFormat.Rgb9E5,                         Col.Format.RGB
            TextureFormat.Srgb8,                          Col.Format.RGB
            TextureFormat.Srgb8Alpha8,                    Col.Format.RGBA
            TextureFormat.Rgba32ui,                       Col.Format.RGBA
            TextureFormat.Rgb32ui,                        Col.Format.RGB
            TextureFormat.Rgba16ui,                       Col.Format.RGBA
            TextureFormat.Rgb16ui,                        Col.Format.RGB
            TextureFormat.Rgba8ui,                        Col.Format.RGBA
            TextureFormat.Rgb8ui,                         Col.Format.RGB
            TextureFormat.Rgba32i,                        Col.Format.RGBA
            TextureFormat.Rgb32i,                         Col.Format.RGB
            TextureFormat.Rgba16i,                        Col.Format.RGBA
            TextureFormat.Rgb16i,                         Col.Format.RGB
            TextureFormat.Rgba8i,                         Col.Format.RGBA
            TextureFormat.Rgb8i,                          Col.Format.RGB
            TextureFormat.R8Snorm,                        Col.Format.Gray
            TextureFormat.Rg8Snorm,                       Col.Format.NormalUV
            TextureFormat.Rgb8Snorm,                      Col.Format.RGB
            TextureFormat.Rgba8Snorm,                     Col.Format.RGBA
            TextureFormat.R16Snorm,                       Col.Format.Gray
            TextureFormat.Rg16Snorm,                      Col.Format.NormalUV
            TextureFormat.Rgb16Snorm,                     Col.Format.RGB
            TextureFormat.Rgba16Snorm,                    Col.Format.RGBA
            TextureFormat.Rgb10A2ui,                      Col.Format.RGBA
            TextureFormat.DepthComponent16,               Col.Format.Gray
            TextureFormat.DepthComponent24,               Col.Format.Gray
            TextureFormat.DepthComponent32,               Col.Format.Gray
            TextureFormat.DepthComponent32f,              Col.Format.Gray
            TextureFormat.Depth24Stencil8,                Col.Format.Gray
            TextureFormat.Depth32fStencil8,               Col.Format.Gray
            TextureFormat.StencilIndex8,                  Col.Format.Gray
            TextureFormat.CompressedRgbS3tcDxt1,          Col.Format.RGB
            TextureFormat.CompressedSrgbS3tcDxt1,         Col.Format.RGB
            TextureFormat.CompressedRgbaS3tcDxt1,         Col.Format.RGBA
            TextureFormat.CompressedSrgbAlphaS3tcDxt1,    Col.Format.RGBA
            TextureFormat.CompressedRgbaS3tcDxt3,         Col.Format.RGBA
            TextureFormat.CompressedSrgbAlphaS3tcDxt3,    Col.Format.RGBA
            TextureFormat.CompressedRgbaS3tcDxt5,         Col.Format.RGBA
            TextureFormat.CompressedSrgbAlphaS3tcDxt5,    Col.Format.RGBA
            TextureFormat.CompressedRedRgtc1,             Col.Format.Gray
            TextureFormat.CompressedSignedRedRgtc1,       Col.Format.Gray
            TextureFormat.CompressedRgRgtc2,              Col.Format.NormalUV
            TextureFormat.CompressedSignedRgRgtc2,        Col.Format.NormalUV
            TextureFormat.CompressedRgbaBptcUnorm,        Col.Format.RGBA
            TextureFormat.CompressedSrgbAlphaBptcUnorm,   Col.Format.RGBA
            TextureFormat.CompressedRgbBptcSignedFloat,   Col.Format.RGB
            TextureFormat.CompressedRgbBptcUnsignedFloat, Col.Format.RGB
        ]

    let pixelSizeInBits =
        LookupTable.lookupTable [
            TextureFormat.Bgr8, 24
            TextureFormat.Bgra8, 32
            TextureFormat.R3G3B2, 6
            TextureFormat.Rgb4, 12
            TextureFormat.Rgb5, 15
            TextureFormat.Rgb8, 24
            TextureFormat.Rgb10, 30
            TextureFormat.Rgb12, 36
            TextureFormat.Rgb16, 48
            TextureFormat.Rgba2, 8
            TextureFormat.Rgba4, 16
            TextureFormat.Rgb5A1, 16
            TextureFormat.Rgba8, 32
            TextureFormat.Rgb10A2, 32
            TextureFormat.Rgba12, 48
            TextureFormat.Rgba16, 64
            TextureFormat.R8, 8
            TextureFormat.R16, 16
            TextureFormat.Rg8, 16
            TextureFormat.Rg16, 32
            TextureFormat.R16f, 32
            TextureFormat.R32f, 32
            TextureFormat.Rg16f, 32
            TextureFormat.Rg32f, 64
            TextureFormat.R8i, 8
            TextureFormat.R8ui, 8
            TextureFormat.R16i, 16
            TextureFormat.R16ui, 16
            TextureFormat.R32i, 32
            TextureFormat.R32ui, 32
            TextureFormat.Rg8i, 16
            TextureFormat.Rg8ui, 16
            TextureFormat.Rg16i, 32
            TextureFormat.Rg16ui, 32
            TextureFormat.Rg32i, 64
            TextureFormat.Rg32ui, 64
            TextureFormat.Rgba32f, 128
            TextureFormat.Rgb32f, 96
            TextureFormat.Rgba16f, 64
            TextureFormat.Rgb16f, 48
            TextureFormat.R11fG11fB10f, 32
            TextureFormat.Rgb9E5, 032
            TextureFormat.Srgb8, 24
            TextureFormat.Srgb8Alpha8, 32
            TextureFormat.Rgba32ui, 128
            TextureFormat.Rgb32ui, 96
            TextureFormat.Rgba16ui, 64
            TextureFormat.Rgb16ui, 48
            TextureFormat.Rgba8ui, 32
            TextureFormat.Rgb8ui, 24
            TextureFormat.Rgba32i, 128
            TextureFormat.Rgb32i, 96
            TextureFormat.Rgba16i, 64
            TextureFormat.Rgb16i, 48
            TextureFormat.Rgba8i, 32
            TextureFormat.Rgb8i, 24
            TextureFormat.R8Snorm, 8
            TextureFormat.Rg8Snorm, 16
            TextureFormat.Rgb8Snorm, 24
            TextureFormat.Rgba8Snorm, 32
            TextureFormat.R16Snorm, 16
            TextureFormat.Rg16Snorm, 32
            TextureFormat.Rgb16Snorm, 48
            TextureFormat.Rgba16Snorm, 64
            TextureFormat.Rgb10A2ui, 32
            TextureFormat.DepthComponent16, 16
            TextureFormat.DepthComponent24, 24
            TextureFormat.DepthComponent32, 32
            TextureFormat.Depth24Stencil8, 32
            TextureFormat.DepthComponent32f, 32
            TextureFormat.Depth32fStencil8, 40
            TextureFormat.StencilIndex8, 8
            TextureFormat.CompressedRgbS3tcDxt1, -1
            TextureFormat.CompressedSrgbS3tcDxt1, -1
            TextureFormat.CompressedRgbaS3tcDxt1, -1
            TextureFormat.CompressedSrgbAlphaS3tcDxt1, -1
            TextureFormat.CompressedRgbaS3tcDxt3, -1
            TextureFormat.CompressedSrgbAlphaS3tcDxt3, -1
            TextureFormat.CompressedRgbaS3tcDxt5, -1
            TextureFormat.CompressedSrgbAlphaS3tcDxt5, -1
            TextureFormat.CompressedRedRgtc1, -1
            TextureFormat.CompressedSignedRedRgtc1, -1
            TextureFormat.CompressedRgRgtc2, -1
            TextureFormat.CompressedSignedRgRgtc2, -1
            TextureFormat.CompressedRgbaBptcUnorm, -1
            TextureFormat.CompressedSrgbAlphaBptcUnorm, -1
            TextureFormat.CompressedRgbBptcSignedFloat, -1
            TextureFormat.CompressedRgbBptcUnsignedFloat, -1
        ]

    let pixelSizeInBytes (fmt : TextureFormat) =
        let s = pixelSizeInBits fmt
        if s < 0 then -1
        elif s % 8 = 0 then s / 8
        else failwithf "[TextureFormat] ill-aligned size %A" s

    /// Returns whether the given format is color renderable (i.e. an uncompressed color format)
    let isColorRenderable (fmt : TextureFormat) =
        not (hasDepth fmt || hasStencil fmt || isCompressed fmt)

[<AutoOpen>]
module TextureFormatExtensions =
    type TextureFormat with
        member x.IsIntegerFormat = TextureFormat.isIntegerFormat x
        member x.IsSigned = TextureFormat.isSigned x
        member x.IsCompressed = TextureFormat.isCompressed x
        member x.IsSrgb = TextureFormat.isSrgb x
        member x.IsDepth = TextureFormat.isDepth x
        member x.IsStencil = TextureFormat.isStencil x
        member x.IsDepthStencil = TextureFormat.isDepthStencil x
        member x.IsColorRenderable = TextureFormat.isColorRenderable x
        member x.HasDepth = TextureFormat.hasDepth x
        member x.HasStencil = TextureFormat.hasStencil x
        member x.Aspect = TextureFormat.toAspect x
        member x.PixelSizeInBits = TextureFormat.pixelSizeInBits x
        member x.PixelSizeInBytes = TextureFormat.pixelSizeInBytes x

        [<Obsolete>]
        member x.IsFilterable = not (x.IsIntegerFormat || x.IsCompressed)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PixFormat =
    let private typeSize =
        LookupTable.lookupTable [
            typeof<int8>, 1
            typeof<uint8>, 1
            typeof<int16>, 2
            typeof<uint16>, 2
            typeof<int32>, 4
            typeof<uint32>, 4
            typeof<int64>, 8
            typeof<uint64>, 8
            typeof<float16>, 2
            typeof<float32>, 4
            typeof<float>, 8

        ]

    /// Returns the size of each channel in bytes.
    let channelSize (fmt : PixFormat) =
        typeSize fmt.Type

    /// Returns the total pixel size in bytes (i.e. channel size x channel count)
    let pixelSizeInBytes (fmt : PixFormat) =
        typeSize fmt.Type * fmt.Format.ChannelCount()

[<AutoOpen>]
module PixFormatExtensions =

    type PixFormat with
        /// The size of each channel in bytes.
        member x.ChannelSize = PixFormat.channelSize x

        /// The total pixel size in bytes (i.e. channel size x channel count)
        member x.PixelSize = PixFormat.pixelSizeInBytes x

    type PixImage with
        /// The size of each channel in bytes.
        member x.ChannelSize = PixFormat.channelSize x.PixFormat

        /// The total pixel size in bytes (i.e. channel size x channel count)
        member x.PixelSize = PixFormat.pixelSizeInBytes x.PixFormat

    type PixVolume with
        /// The size of each channel in bytes.
        member x.ChannelSize = PixFormat.channelSize x.PixFormat

        /// The total pixel size in bytes (i.e. channel size x channel count)
        member x.PixelSize = PixFormat.pixelSizeInBytes x.PixFormat