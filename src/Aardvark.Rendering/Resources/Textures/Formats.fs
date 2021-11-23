namespace Aardvark.Rendering

open System
open Aardvark.Base
open FSharp.Data.Adaptive

type TextureDimension =
    | Texture1D = 1
    | Texture2D = 2
    | TextureCube = 3
    | Texture3D = 4

type RenderbufferFormat =
    | DepthComponent = 6402
    | R3G3B2 = 10768
    | Rgb4 = 32847
    | Rgb5 = 32848
    | Rgb8 = 32849
    | Rgb10 = 32850
    | Rgb12 = 32851
    | Rgb16 = 32852
    | Rgba2 = 32853
    | Rgba4 = 32854
    | Rgba8 = 32856
    | Rgb10A2 = 32857
    | Rgba12 = 32858
    | Rgba16 = 32859
    | DepthComponent16 = 33189
    | DepthComponent24 = 33190
    | DepthComponent32 = 33191
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
    | DepthStencil = 34041
    | Rgba32f = 34836
    | Rgb32f = 34837
    | Rgba16f = 34842
    | Rgb16f = 34843
    | Depth24Stencil8 = 35056
    | R11fG11fB10f = 35898
    | Rgb9E5 = 35901
    | Srgb8 = 35905
    | Srgb8Alpha8 = 35907
    | DepthComponent32f = 36012
    | Depth32fStencil8 = 36013
    | StencilIndex1Ext = 36166
    | StencilIndex1 = 36166
    | StencilIndex4Ext = 36167
    | StencilIndex4 = 36167
    | StencilIndex8 = 36168
    | StencilIndex8Ext = 36168
    | StencilIndex16Ext = 36169
    | StencilIndex16 = 36169
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
    | Rgb10A2ui = 36975

type CompressionMode =
    | None = 0
    | BC1 = 1
    | BC2 = 2
    | BC3 = 3
    | BC4 = 4
    | BC5 = 5

type TextureFormat =
    | Bgr8 = 1234
    | Bgra8 = 1235
    | DepthComponent = 6402
    | Alpha = 6406
    | Rgb = 6407
    | Rgba = 6408
    | Luminance = 6409
    | LuminanceAlpha = 6410
    | R3G3B2 = 10768
    | Rgb2Ext = 32846
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
    | DualAlpha4Sgis = 33040
    | DualAlpha8Sgis = 33041
    | DualAlpha12Sgis = 33042
    | DualAlpha16Sgis = 33043
    | DualLuminance4Sgis = 33044
    | DualLuminance8Sgis = 33045
    | DualLuminance12Sgis = 33046
    | DualLuminance16Sgis = 33047
    | DualIntensity4Sgis = 33048
    | DualIntensity8Sgis = 33049
    | DualIntensity12Sgis = 33050
    | DualIntensity16Sgis = 33051
    | DualLuminanceAlpha4Sgis = 33052
    | DualLuminanceAlpha8Sgis = 33053
    | QuadAlpha4Sgis = 33054
    | QuadAlpha8Sgis = 33055
    | QuadLuminance4Sgis = 33056
    | QuadLuminance8Sgis = 33057
    | QuadIntensity4Sgis = 33058
    | QuadIntensity8Sgis = 33059
    | DepthComponent16 = 33189
    | DepthComponent16Sgix = 33189
    | DepthComponent24 = 33190
    | DepthComponent24Sgix = 33190
    | DepthComponent32 = 33191
    | DepthComponent32Sgix = 33191
    | CompressedRed = 33317
    | CompressedRg = 33318
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
    | CompressedRgbS3tcDxt1Ext = 33776
    | CompressedRgbaS3tcDxt1Ext = 33777
    | CompressedRgbaS3tcDxt3Ext = 33778
    | CompressedRgbaS3tcDxt5Ext = 33779
    | RgbIccSgix = 33888
    | RgbaIccSgix = 33889
    | AlphaIccSgix = 33890
    | LuminanceIccSgix = 33891
    | IntensityIccSgix = 33892
    | LuminanceAlphaIccSgix = 33893
    | R5G6B5IccSgix = 33894
    | R5G6B5A8IccSgix = 33895
    | Alpha16IccSgix = 33896
    | Luminance16IccSgix = 33897
    | Intensity16IccSgix = 33898
    | Luminance16Alpha8IccSgix = 33899
    | CompressedAlpha = 34025
    | CompressedLuminance = 34026
    | CompressedLuminanceAlpha = 34027
    | CompressedIntensity = 34028
    | CompressedRgb = 34029
    | CompressedRgba = 34030
    | DepthStencil = 34041
    | Rgba32f = 34836
    | Rgb32f = 34837
    | Rgba16f = 34842
    | Rgb16f = 34843
    | Depth24Stencil8 = 35056
    | R11fG11fB10f = 35898
    | Rgb9E5 = 35901
    | Srgb = 35904
    | Srgb8 = 35905
    | SrgbAlpha = 35906
    | Srgb8Alpha8 = 35907
    | SluminanceAlpha = 35908
    | Sluminance8Alpha8 = 35909
    | Sluminance = 35910
    | Sluminance8 = 35911
    | CompressedSrgb = 35912
    | CompressedSrgbAlpha = 35913
    | CompressedSluminance = 35914
    | CompressedSluminanceAlpha = 35915
    | CompressedSrgbS3tcDxt1Ext = 35916
    | CompressedSrgbAlphaS3tcDxt1Ext = 35917
    | CompressedSrgbAlphaS3tcDxt3Ext = 35918
    | CompressedSrgbAlphaS3tcDxt5Ext = 35919
    | DepthComponent32f = 36012
    | Depth32fStencil8 = 36013
    | StencilIndex8 = 36168
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
    | Float32UnsignedInt248Rev = 36269
    | CompressedRedRgtc1 = 36283
    | CompressedSignedRedRgtc1 = 36284
    | CompressedRgRgtc2 = 36285
    | CompressedSignedRgRgtc2 = 36286
    | CompressedRgbaBptcUnorm = 36492
    | CompressedRgbBptcSignedFloat = 36494
    | CompressedRgbBptcUnsignedFloat = 36495
    | R8Snorm = 36756
    | Rg8Snorm = 36757
    | Rgb8Snorm = 36758
    | Rgba8Snorm = 36759
    | R16Snorm = 36760
    | Rg16Snorm = 36761
    | Rgb16Snorm = 36762
    | Rgba16Snorm = 36763
    | Rgb10A2ui = 36975
    | One = 1
    | Two = 2
    | Three = 3
    | Four = 4

type AttachmentSignature =
    {
        format : RenderbufferFormat
        samples : int
    }

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


[<AutoOpen>]
module private ConversionHelpers =
    let inline internal convertEnum< ^a, ^b when ^a : (static member op_Explicit : ^a -> int)> (fmt : ^a) : ^b =
        let v = int fmt
        if Enum.IsDefined(typeof< ^b >, v) then
            unbox< ^b > v
        else
            failwithf "cannot convert %s %A to %s" typeof< ^a >.Name fmt typeof< ^b >.Name

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TextureFormat =

    let toRenderbufferFormat (fmt : TextureFormat) =
        convertEnum<_, RenderbufferFormat> fmt

    let ofRenderbufferFormat (fmt : RenderbufferFormat) =
        convertEnum<_, TextureFormat> fmt

    let private srgbFormats =
        HashSet.ofList [
            TextureFormat.Srgb
            TextureFormat.SrgbAlpha
            TextureFormat.Srgb8
            TextureFormat.Srgb8Alpha8

            TextureFormat.CompressedSrgbS3tcDxt1Ext
            TextureFormat.CompressedSrgbAlphaS3tcDxt1Ext
            TextureFormat.CompressedSrgbAlphaS3tcDxt3Ext
            TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext
        ]

    let isSrgb (fmt : TextureFormat) =
        srgbFormats.Contains fmt

    let private depthFormats =
        HashSet.ofList [
            TextureFormat.DepthComponent
            TextureFormat.DepthComponent16
            TextureFormat.DepthComponent16Sgix
            TextureFormat.DepthComponent24
            TextureFormat.DepthComponent24Sgix
            TextureFormat.DepthComponent32
            TextureFormat.DepthComponent32Sgix
            TextureFormat.DepthComponent32f
        ]

    let private stencilFormats =
        HashSet.ofList [
            TextureFormat.StencilIndex8
        ]

    let private depthStencilFormats =
        HashSet.ofList [
            TextureFormat.DepthStencil
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

            PixFormat(typeof<float32>, Col.Format.None), (fun _ -> TextureFormat.DepthComponent32)
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

    let private compressedFormats =
        HashSet.ofList [
            TextureFormat.CompressedAlpha
            TextureFormat.CompressedLuminance
            TextureFormat.CompressedLuminanceAlpha
            TextureFormat.CompressedIntensity
            TextureFormat.CompressedRgb
            TextureFormat.CompressedRgba
            TextureFormat.CompressedRed
            TextureFormat.CompressedRg
            TextureFormat.CompressedSrgb
            TextureFormat.CompressedSrgbAlpha
            TextureFormat.CompressedSluminance
            TextureFormat.CompressedSluminanceAlpha
            TextureFormat.CompressedSrgbS3tcDxt1Ext
            TextureFormat.CompressedSrgbAlphaS3tcDxt1Ext
            TextureFormat.CompressedSrgbAlphaS3tcDxt3Ext
            TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext
            TextureFormat.CompressedRgbS3tcDxt1Ext
            TextureFormat.CompressedRgbaS3tcDxt1Ext
            TextureFormat.CompressedRgbaS3tcDxt3Ext
            TextureFormat.CompressedRgbaS3tcDxt5Ext
            TextureFormat.CompressedRedRgtc1
            TextureFormat.CompressedSignedRedRgtc1
            TextureFormat.CompressedRgRgtc2
            TextureFormat.CompressedSignedRgRgtc2
            TextureFormat.CompressedRgbaBptcUnorm
            TextureFormat.CompressedRgbBptcSignedFloat
            TextureFormat.CompressedRgbBptcUnsignedFloat
        ]

    let isCompressed (fmt : TextureFormat) =
        compressedFormats |> HashSet.contains fmt

    let toCompressed =
        LookupTable.lookupTable' [
            TextureFormat.Rgb8, TextureFormat.CompressedRgbS3tcDxt1Ext
            TextureFormat.Srgb8, TextureFormat.CompressedSrgbS3tcDxt1Ext
            TextureFormat.Rgba8, TextureFormat.CompressedRgbaS3tcDxt5Ext
            TextureFormat.Srgb8Alpha8, TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext
        ]

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

            TextureFormat.CompressedRgb, PixFormat.ByteRGB
            TextureFormat.CompressedRgba, PixFormat.ByteRGBA
            TextureFormat.CompressedRgbS3tcDxt1Ext, PixFormat.ByteRGB
            TextureFormat.CompressedRgbaS3tcDxt1Ext, PixFormat.ByteRGBA
            TextureFormat.CompressedRgbaS3tcDxt3Ext, PixFormat.ByteRGBA
            TextureFormat.CompressedRgbaS3tcDxt5Ext, PixFormat.ByteRGBA

            TextureFormat.CompressedSrgb, PixFormat.ByteRGB
            TextureFormat.CompressedSrgbAlpha, PixFormat.ByteRGBA
            TextureFormat.CompressedSrgbS3tcDxt1Ext, PixFormat.ByteRGB
            TextureFormat.CompressedSrgbAlphaS3tcDxt1Ext, PixFormat.ByteRGBA
            TextureFormat.CompressedSrgbAlphaS3tcDxt3Ext, PixFormat.ByteRGBA
            TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext, PixFormat.ByteRGBA

            TextureFormat.CompressedRedRgtc1, PixFormat.ByteGray
            TextureFormat.CompressedSignedRedRgtc1, PixFormat.ByteGray
            TextureFormat.CompressedRgRgtc2, PixFormat(typeof<uint8>, Col.Format.NormalUV)
            TextureFormat.CompressedSignedRgRgtc2, PixFormat(typeof<uint8>, Col.Format.NormalUV)
            TextureFormat.CompressedRgbaBptcUnorm, PixFormat.ByteRGBA
            TextureFormat.CompressedRgbBptcSignedFloat, PixFormat.FloatRGB
            TextureFormat.CompressedRgbBptcUnsignedFloat, PixFormat.FloatRGB
        ]

    let pixelSizeInBits =
        LookupTable.lookupTable [
            TextureFormat.Bgr8, 24
            TextureFormat.Bgra8, 32
            TextureFormat.DepthComponent, 24
            TextureFormat.Alpha, 8
            TextureFormat.Rgb, 24
            TextureFormat.Rgba,32
            TextureFormat.Luminance, 8
            TextureFormat.LuminanceAlpha, 16
            TextureFormat.R3G3B2, 6
            TextureFormat.Rgb2Ext, 6
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
            TextureFormat.DualAlpha4Sgis, 8
            TextureFormat.DualAlpha8Sgis, 16
            TextureFormat.DualAlpha12Sgis, 24
            TextureFormat.DualAlpha16Sgis,32
            TextureFormat.DualLuminance4Sgis, 8
            TextureFormat.DualLuminance8Sgis, 16
            TextureFormat.DualLuminance12Sgis, 24
            TextureFormat.DualLuminance16Sgis, 32
            TextureFormat.DualIntensity4Sgis, 8
            TextureFormat.DualIntensity8Sgis, 16
            TextureFormat.DualIntensity12Sgis, 24
            TextureFormat.DualIntensity16Sgis, 32
            TextureFormat.DualLuminanceAlpha4Sgis, -1
            TextureFormat.DualLuminanceAlpha8Sgis, -1
            TextureFormat.QuadAlpha4Sgis, 16
            TextureFormat.QuadAlpha8Sgis,32
            TextureFormat.QuadLuminance4Sgis, 16
            TextureFormat.QuadLuminance8Sgis, 32
            TextureFormat.QuadIntensity4Sgis, 16
            TextureFormat.QuadIntensity8Sgis, 32
            TextureFormat.DepthComponent16, 16
            TextureFormat.DepthComponent24, 24
            TextureFormat.DepthComponent32, 32
            TextureFormat.CompressedRed, -1
            TextureFormat.CompressedRg, -1
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
            TextureFormat.CompressedRgbS3tcDxt1Ext, -1
            TextureFormat.CompressedRgbaS3tcDxt1Ext, -1
            TextureFormat.CompressedRgbaS3tcDxt3Ext, -1
            TextureFormat.CompressedRgbaS3tcDxt5Ext, -1
            TextureFormat.RgbIccSgix, 24
            TextureFormat.RgbaIccSgix, 32
            TextureFormat.AlphaIccSgix, 8
            TextureFormat.LuminanceIccSgix, 8
            TextureFormat.IntensityIccSgix, 8
            TextureFormat.LuminanceAlphaIccSgix, 16
            TextureFormat.R5G6B5IccSgix, 16
            TextureFormat.R5G6B5A8IccSgix, 24
            TextureFormat.Alpha16IccSgix, 16
            TextureFormat.Luminance16IccSgix, 16
            TextureFormat.Intensity16IccSgix, 16
            TextureFormat.Luminance16Alpha8IccSgix, 24
            TextureFormat.CompressedAlpha, -1
            TextureFormat.CompressedLuminance, -1
            TextureFormat.CompressedLuminanceAlpha, -1
            TextureFormat.CompressedIntensity, -1
            TextureFormat.CompressedRgb, -1
            TextureFormat.CompressedRgba, -1
            TextureFormat.DepthStencil, 32
            TextureFormat.Rgba32f, 128
            TextureFormat.Rgb32f, 96
            TextureFormat.Rgba16f, 64
            TextureFormat.Rgb16f, 48
            TextureFormat.Depth24Stencil8, 32
            TextureFormat.R11fG11fB10f, 32
            TextureFormat.Rgb9E5, 032
            TextureFormat.Srgb, 24
            TextureFormat.Srgb8, 24
            TextureFormat.SrgbAlpha, 32
            TextureFormat.Srgb8Alpha8, 32
            TextureFormat.SluminanceAlpha, 16
            TextureFormat.Sluminance8Alpha8, 16
            TextureFormat.Sluminance, 8
            TextureFormat.Sluminance8, 8
            TextureFormat.CompressedSrgb, -1
            TextureFormat.CompressedSrgbAlpha, -1
            TextureFormat.CompressedSluminance, -1
            TextureFormat.CompressedSluminanceAlpha, -1
            TextureFormat.CompressedSrgbS3tcDxt1Ext, -1
            TextureFormat.CompressedSrgbAlphaS3tcDxt1Ext, -1
            TextureFormat.CompressedSrgbAlphaS3tcDxt3Ext, -1
            TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext, -1
            TextureFormat.DepthComponent32f, 32
            TextureFormat.Depth32fStencil8, 40
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
            TextureFormat.Float32UnsignedInt248Rev, 32
            TextureFormat.CompressedRedRgtc1, -1
            TextureFormat.CompressedSignedRedRgtc1, -1
            TextureFormat.CompressedRgRgtc2, -1
            TextureFormat.CompressedSignedRgRgtc2, -1
            TextureFormat.CompressedRgbaBptcUnorm, -1
            TextureFormat.CompressedRgbBptcSignedFloat, -1
            TextureFormat.CompressedRgbBptcUnsignedFloat, -1
            TextureFormat.R8Snorm, 8
            TextureFormat.Rg8Snorm, 16
            TextureFormat.Rgb8Snorm, 24
            TextureFormat.Rgba8Snorm, 32
            TextureFormat.R16Snorm, 16
            TextureFormat.Rg16Snorm, 32
            TextureFormat.Rgb16Snorm, 48
            TextureFormat.Rgba16Snorm, 64
            TextureFormat.Rgb10A2ui, 32
            TextureFormat.One, -1
            TextureFormat.Two, -1
            TextureFormat.Three, -1
            TextureFormat.Four, -1
        ]

    let pixelSizeInBytes (fmt : TextureFormat) =
        let s = pixelSizeInBits fmt
        if s < 0 then -1
        elif s % 8 = 0 then s / 8
        else failwithf "[TextureFormat] ill-aligned size %A" s

    let private compressionModes =
        Dictionary.ofList [
            TextureFormat.CompressedRgbS3tcDxt1Ext, CompressionMode.BC1
            TextureFormat.CompressedRgbaS3tcDxt1Ext, CompressionMode.BC1
            TextureFormat.CompressedSrgbS3tcDxt1Ext, CompressionMode.BC1
            TextureFormat.CompressedSrgbAlphaS3tcDxt1Ext, CompressionMode.BC1

            TextureFormat.CompressedRgbaS3tcDxt3Ext, CompressionMode.BC2
            TextureFormat.CompressedSrgbAlphaS3tcDxt3Ext, CompressionMode.BC2

            TextureFormat.CompressedRgbaS3tcDxt5Ext, CompressionMode.BC3
            TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext, CompressionMode.BC3

            TextureFormat.CompressedRedRgtc1, CompressionMode.BC4
            TextureFormat.CompressedSignedRedRgtc1, CompressionMode.BC4

            TextureFormat.CompressedRgRgtc2, CompressionMode.BC5
            TextureFormat.CompressedSignedRgRgtc2, CompressionMode.BC5
        ]

    let compressionMode (fmt : TextureFormat) =
        match compressionModes.TryGetValue fmt with
            | (true, mode) -> mode
            | _ -> CompressionMode.None

[<AutoOpen>]
module TextureFormatExtensions =
    type TextureFormat with
        member x.IsIntegerFormat = TextureFormat.isIntegerFormat x
        member x.IsCompressed = TextureFormat.isCompressed x
        member x.IsSrgb = TextureFormat.isSrgb x
        member x.IsDepth = TextureFormat.isDepth x
        member x.IsStencil = TextureFormat.isStencil x
        member x.IsDepthStencil = TextureFormat.isDepthStencil x
        member x.HasDepth = TextureFormat.hasDepth x
        member x.HasStencil = TextureFormat.hasStencil x


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CompressionMode =
    let blockSize =
        LookupTable.lookupTable [
            CompressionMode.None, V2i.II
            CompressionMode.BC1, V2i(4,4)
            CompressionMode.BC2, V2i(4,4)
            CompressionMode.BC3, V2i(4,4)
            CompressionMode.BC4, V2i(4,4)
            CompressionMode.BC5, V2i(4,4)
        ]

    let blockSizeInBytes =
        LookupTable.lookupTable [
            CompressionMode.None, 0
            CompressionMode.BC1, 8
            CompressionMode.BC2, 16
            CompressionMode.BC3, 16
            CompressionMode.BC4, 8
            CompressionMode.BC5, 16

        ]


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PixFormat =
    let channels =
        LookupTable.lookupTable [
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

    let typeSize =
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

    let pixelSizeInBytes (fmt : PixFormat) =
        typeSize fmt.Type * channels fmt.Format

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RenderbufferFormat =

    let toColFormat =
        LookupTable.lookupTable [
            RenderbufferFormat.DepthComponent, Col.Format.Gray
            RenderbufferFormat.R3G3B2, Col.Format.RGB
            RenderbufferFormat.Rgb4, Col.Format.RGB
            RenderbufferFormat.Rgb5, Col.Format.RGB
            RenderbufferFormat.Rgb8, Col.Format.RGB
            RenderbufferFormat.Rgb10, Col.Format.RGB
            RenderbufferFormat.Rgb12, Col.Format.RGB
            RenderbufferFormat.Rgb16, Col.Format.RGB
            RenderbufferFormat.Rgba2, Col.Format.RGBA
            RenderbufferFormat.Rgba4, Col.Format.RGBA
            RenderbufferFormat.Rgba8, Col.Format.RGBA
            RenderbufferFormat.Rgb10A2, Col.Format.RGBA
            RenderbufferFormat.Rgba12, Col.Format.RGBA
            RenderbufferFormat.Rgba16, Col.Format.RGBA
            RenderbufferFormat.DepthComponent16, Col.Format.Gray
            RenderbufferFormat.DepthComponent24, Col.Format.Gray
            RenderbufferFormat.DepthComponent32, Col.Format.Gray
            RenderbufferFormat.R8, Col.Format.Gray
            RenderbufferFormat.R16, Col.Format.Gray
            RenderbufferFormat.Rg8, Col.Format.NormalUV
            RenderbufferFormat.Rg16, Col.Format.NormalUV
            RenderbufferFormat.R16f, Col.Format.Gray
            RenderbufferFormat.R32f, Col.Format.Gray
            RenderbufferFormat.Rg16f, Col.Format.NormalUV
            RenderbufferFormat.Rg32f, Col.Format.NormalUV
            RenderbufferFormat.R8i, Col.Format.Gray
            RenderbufferFormat.R8ui, Col.Format.Gray
            RenderbufferFormat.R16i, Col.Format.Gray
            RenderbufferFormat.R16ui, Col.Format.Gray
            RenderbufferFormat.R32i, Col.Format.Gray
            RenderbufferFormat.R32ui, Col.Format.Gray
            RenderbufferFormat.Rg8i, Col.Format.NormalUV
            RenderbufferFormat.Rg8ui, Col.Format.NormalUV
            RenderbufferFormat.Rg16i, Col.Format.NormalUV
            RenderbufferFormat.Rg16ui, Col.Format.NormalUV
            RenderbufferFormat.Rg32i, Col.Format.NormalUV
            RenderbufferFormat.Rg32ui, Col.Format.NormalUV
            RenderbufferFormat.DepthStencil, Col.Format.Gray
            RenderbufferFormat.Rgba32f, Col.Format.RGBA
            RenderbufferFormat.Rgb32f, Col.Format.RGB
            RenderbufferFormat.Rgba16f, Col.Format.RGBA
            RenderbufferFormat.Rgb16f, Col.Format.RGB
            RenderbufferFormat.Depth24Stencil8, Col.Format.Gray
            RenderbufferFormat.R11fG11fB10f, Col.Format.RGB
            RenderbufferFormat.Rgb9E5, Col.Format.RGB
            RenderbufferFormat.Srgb8, Col.Format.RGB
            RenderbufferFormat.Srgb8Alpha8, Col.Format.RGBA
            RenderbufferFormat.DepthComponent32f, Col.Format.Gray
            RenderbufferFormat.Depth32fStencil8, Col.Format.Gray
//            RenderbufferFormat.StencilIndex1Ext, 36166
//            RenderbufferFormat.StencilIndex1, 36166
//            RenderbufferFormat.StencilIndex4Ext, 36167
//            RenderbufferFormat.StencilIndex4, 36167
//            RenderbufferFormat.StencilIndex8, 36168
//            RenderbufferFormat.StencilIndex8Ext, 36168
//            RenderbufferFormat.StencilIndex16Ext, 36169
//            RenderbufferFormat.StencilIndex16, 36169
            RenderbufferFormat.Rgba32ui, Col.Format.RGBA
            RenderbufferFormat.Rgb32ui, Col.Format.RGB
            RenderbufferFormat.Rgba16ui, Col.Format.RGBA
            RenderbufferFormat.Rgb16ui, Col.Format.RGB
            RenderbufferFormat.Rgba8ui, Col.Format.RGBA
            RenderbufferFormat.Rgb8ui, Col.Format.RGB
            RenderbufferFormat.Rgba32i, Col.Format.RGBA
            RenderbufferFormat.Rgb32i, Col.Format.RGB
            RenderbufferFormat.Rgba16i, Col.Format.RGBA
            RenderbufferFormat.Rgb16i, Col.Format.RGB
            RenderbufferFormat.Rgba8i, Col.Format.RGBA
            RenderbufferFormat.Rgb8i, Col.Format.RGB
            RenderbufferFormat.Rgb10A2ui, Col.Format.RGBA

        ]

    let toTextureFormat (fmt : RenderbufferFormat) =
        convertEnum<_, TextureFormat> fmt

    let ofTextureFormat (fmt : TextureFormat) =
        convertEnum<_, RenderbufferFormat> fmt

    let private depthFormats =
        HashSet.ofList [
            RenderbufferFormat.DepthComponent
            RenderbufferFormat.DepthComponent16
            RenderbufferFormat.DepthComponent24
            RenderbufferFormat.DepthComponent32
            RenderbufferFormat.DepthComponent32f
        ]

    let private stencilFormats =
        HashSet.ofList [
            RenderbufferFormat.StencilIndex1Ext
            RenderbufferFormat.StencilIndex1
            RenderbufferFormat.StencilIndex4Ext
            RenderbufferFormat.StencilIndex4
            RenderbufferFormat.StencilIndex8
            RenderbufferFormat.StencilIndex8Ext
            RenderbufferFormat.StencilIndex16Ext
            RenderbufferFormat.StencilIndex16
        ]

    let private depthStencilFormats =
        HashSet.ofList [
            RenderbufferFormat.DepthStencil
            RenderbufferFormat.Depth24Stencil8
            RenderbufferFormat.Depth32fStencil8
        ]

    /// Returns whether the given format is a depth format.
    let isDepth (fmt : RenderbufferFormat) =
        depthFormats.Contains fmt

    /// Returns whether the given format is a stencil format.
    let isStencil (fmt : RenderbufferFormat) =
        stencilFormats.Contains fmt

    /// Returns whether the given format is a combined depth-stencil format.
    let isDepthStencil (fmt : RenderbufferFormat) =
        depthStencilFormats.Contains fmt

    /// Returns whether the given format has a depth component (i.e. it is
    /// either a depth for a combined depth-stencil format).
    let hasDepth (fmt : RenderbufferFormat) =
        isDepth fmt || isDepthStencil fmt

    /// Returns whether the given format has a stencil component (i.e. it is
    /// either a stencil for a combined depth-stencil format).
    let hasStencil (fmt : RenderbufferFormat) =
        isStencil fmt || isDepthStencil fmt

[<AutoOpen>]
module RenderbufferFormatExtensions =
    type RenderbufferFormat with
        member x.IsDepth = RenderbufferFormat.isDepth x
        member x.IsStencil = RenderbufferFormat.isStencil x
        member x.IsDepthStencil = RenderbufferFormat.isDepthStencil x
        member x.HasDepth = RenderbufferFormat.hasDepth x
        member x.HasStencil = RenderbufferFormat.hasStencil x


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AttachmentSignature =

    /// Returns the format of the given attachment signature.
    let format (signature : AttachmentSignature) =
        signature.format

    /// Returns the sample count of the given attachment signature.
    let samples (signature : AttachmentSignature) =
        signature.samples