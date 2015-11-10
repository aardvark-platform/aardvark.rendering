namespace Aardvark.Base


open System
open Aardvark.Base.Incremental

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

type TextureFormat =
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
    open System.Collections.Generic

    let private lookupTable (l : list<'a * 'b>) =
        let d = Dictionary()
        for (k,v) in l do

            match d.TryGetValue k with
                | (true, vo) -> failwithf "duplicated lookup-entry: %A (%A vs %A)" k vo v
                | _ -> ()

            d.[k] <- v

        fun (key : 'a) ->
            match d.TryGetValue key with
                | (true, v) -> v
                | _ -> failwithf "unsupported %A: %A" typeof<'a> key

    let toRenderbufferFormat (fmt : TextureFormat) =
        convertEnum<_, RenderbufferFormat> fmt

    let ofRenderbufferFormat (fmt : RenderbufferFormat) =
        convertEnum<_, TextureFormat> fmt



    let ofPixFormat = 
        let rgb = 
            lookupTable [
                    TextureParams.empty, TextureFormat.Rgb8
                    { TextureParams.empty with wantSrgb = true}, TextureFormat.Srgb8
                    { TextureParams.empty with wantMipMaps = true }, TextureFormat.Rgb8
                    { TextureParams.empty with wantSrgb = true; wantMipMaps = true}, TextureFormat.Srgb8
                ]
        let rgba = 
            lookupTable [
                    TextureParams.empty, TextureFormat.Rgba8
                    { TextureParams.empty with wantSrgb = true}, TextureFormat.Srgb8Alpha8
                    { TextureParams.empty with wantMipMaps = true }, TextureFormat.Rgba8
                    { TextureParams.empty with wantSrgb = true; wantMipMaps = true }, TextureFormat.Srgb8Alpha8
                ]

        lookupTable [
            PixFormat.ByteBGR  ,  rgb
            PixFormat.ByteBGRA , rgba
            PixFormat.ByteBGRP , rgba
            PixFormat.ByteRGB  ,  rgb
            PixFormat.ByteRGBA , rgba
            PixFormat.ByteRGBP , rgba

            PixFormat(typeof<float32>, Col.Format.None), (fun _ -> TextureFormat.DepthComponent32)
                 

        ]
        
    let toDownloadFormat =
        lookupTable [
            TextureFormat.Rgba8, PixFormat.ByteRGBA
            TextureFormat.Rgb8, PixFormat.ByteRGB
            TextureFormat.CompressedRgb, PixFormat.ByteRGB
            TextureFormat.CompressedRgba, PixFormat.ByteRGBA

        ]

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RenderbufferFormat =
    let toTextureFormat (fmt : RenderbufferFormat) =
        convertEnum<_, TextureFormat> fmt

    let ofTextureFormat (fmt : TextureFormat) =
        convertEnum<_, RenderbufferFormat> fmt



        
 
type IFramebufferOutput =
    abstract member Format : RenderbufferFormat
    abstract member Samples : int
    abstract member Size : V2i

type IBackendTexture =
    inherit ITexture
    abstract member Dimension : TextureDimension
    abstract member Format : TextureFormat
    abstract member Samples : int
    abstract member Count : int
    abstract member MipMapLevels : int
    abstract member Size : V3i
    abstract member Handle : obj

type IRenderbuffer =
    inherit IFramebufferOutput
    abstract member Handle : obj

type BackendTextureOutputView = { texture : IBackendTexture; level : int; slice : int } with
    interface IFramebufferOutput with
        member x.Format = TextureFormat.toRenderbufferFormat x.texture.Format
        member x.Samples = x.texture.Samples
        member x.Size = x.texture.Size.XY


type AttachmentSignature = { format : RenderbufferFormat; samples : int }

[<AllowNullLiteral>]
type IFramebufferSignature =
    abstract member ColorAttachments : Map<int, Symbol * AttachmentSignature>
    abstract member DepthStencilAttachment : Option<AttachmentSignature>



type IFramebuffer =
    inherit IDisposable
    abstract member Signature : IFramebufferSignature
    abstract member Size : V2i
    abstract member GetHandle : IAdaptiveObject -> obj
    abstract member Attachments : Map<Symbol, IFramebufferOutput>

