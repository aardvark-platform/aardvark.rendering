namespace Aardvark.Rendering

open System
open Aardvark.Base
open FSharp.Data.Adaptive

type TextureAspect =
    | Color
    | Depth
    | Stencil

[<AllowNullLiteral>]
type ITexture =
    abstract member WantMipMaps : bool

type ISparseTexture<'a when 'a : unmanaged> =
    inherit IDisposable

    [<CLIEvent>]
    abstract member OnSwap : IEvent<EventHandler, EventArgs>

    abstract member UsedMemory : Mem
    abstract member AllocatedMemory : Mem

    abstract member Size : V3i
    abstract member MipMapLevels : int
    abstract member Count : int
    abstract member Format : Col.Format
    abstract member Texture : aval<ITexture>
    abstract member GetBrickCount : level : int -> V3i

    abstract member SparseLevels : int
    abstract member BrickSize : V3i
    abstract member UploadBrick : level : int * slice : int * index : V3i * data : NativeTensor4<'a> -> IDisposable

type INativeTextureData =
    abstract member Size : V3i
    abstract member SizeInBytes : int64
    abstract member Use : (nativeint -> 'a) -> 'a

[<AllowNullLiteral>]
type INativeTexture =
    inherit ITexture
    abstract member Dimension : TextureDimension
    abstract member Format : TextureFormat
    abstract member MipMapLevels : int
    abstract member Count : int
    abstract member Item : slice : int * level : int -> INativeTextureData with get

type IStreamingTexture =
    inherit aval<ITexture>
    abstract member Update : format : PixFormat * size : V2i * data : nativeint -> unit
    abstract member UpdateAsync : format : PixFormat * size : V2i * data : nativeint -> Transaction
    abstract member ReadPixel : pos : V2i -> C4f

type IBackendTexture =
    inherit ITexture
    abstract member Runtime : ITextureRuntime
    abstract member Dimension : TextureDimension
    abstract member Format : TextureFormat
    abstract member Samples : int
    abstract member Count : int
    abstract member MipMapLevels : int
    abstract member Size : V3i
    abstract member Handle : obj

and IFramebufferOutput =
    abstract member Runtime : ITextureRuntime
    abstract member Format : RenderbufferFormat
    abstract member Samples : int
    abstract member Size : V2i

and IBackendTextureOutputView =
    inherit IFramebufferOutput
    abstract member texture : IBackendTexture
    abstract member level : int
    abstract member slices : Range1i

and ITextureRange =
    abstract member Texture : IBackendTexture
    abstract member Aspect : TextureAspect
    abstract member Levels : Range1i
    abstract member Slices : Range1i

and ITextureSlice =
    inherit ITextureRange
    abstract member Slice : int

and ITextureLevel =
    inherit ITextureRange
    inherit IFramebufferOutput
    abstract member Level : int
    abstract member Size : V3i

and ITextureSubResource =
    inherit ITextureSlice
    inherit ITextureLevel

and IRenderbuffer =
    inherit IFramebufferOutput
    abstract member Handle : obj

and ITextureRuntime =
    inherit IBufferRuntime

    abstract member CreateTexture : size : V3i * dim : TextureDimension * format : TextureFormat * slices : int * levels : int * samples : int -> IBackendTexture
    abstract member PrepareTexture : ITexture -> IBackendTexture

    abstract member Copy : src : NativeTensor4<'a> * srcFormat : Col.Format * dst : ITextureSubResource * dstOffset : V3i * size : V3i -> unit
    abstract member Copy : src : ITextureSubResource * srcOffset : V3i * dst : NativeTensor4<'a> * dstFormat : Col.Format * size : V3i -> unit
    abstract member Copy : src : IFramebufferOutput * srcOffset : V3i * dst : IFramebufferOutput * dstOffset : V3i * size : V3i -> unit

    abstract member DeleteTexture : IBackendTexture -> unit

    abstract member CreateRenderbuffer : size : V2i * format : RenderbufferFormat * samples : int -> IRenderbuffer
    abstract member DeleteRenderbuffer : IRenderbuffer -> unit

    abstract member CreateStreamingTexture : mipMaps : bool -> IStreamingTexture
    abstract member DeleteStreamingTexture : IStreamingTexture -> unit

    abstract member CreateSparseTexture<'a when 'a : unmanaged> : size : V3i * levels : int * slices : int * dim : TextureDimension * format : Col.Format * brickSize : V3i * maxMemory : int64 -> ISparseTexture<'a>

    abstract member GenerateMipMaps : IBackendTexture -> unit
    abstract member ResolveMultisamples : src : IFramebufferOutput * target : IBackendTexture * imgTrafo : ImageTrafo -> unit
    abstract member Upload : texture : IBackendTexture * level : int * slice : int * source : PixImage -> unit
    abstract member Download : texture : IBackendTexture * level : int * slice : int * target : PixImage -> unit
    abstract member Download : texture : IBackendTexture * level : int * slice : int * target : PixVolume -> unit
    abstract member DownloadStencil : texture : IBackendTexture * level : int * slice : int * target : Matrix<int> -> unit
    abstract member DownloadDepth : texture : IBackendTexture * level : int * slice : int * target : Matrix<float32> -> unit
    abstract member Copy : src : IBackendTexture * srcBaseSlice : int * srcBaseLevel : int * dst : IBackendTexture * dstBaseSlice : int * dstBaseLevel : int * slices : int * levels : int -> unit

    abstract member CreateTexture : size : V2i * format : TextureFormat * levels : int * samples : int -> IBackendTexture
    abstract member CreateTextureArray : size : V2i * format : TextureFormat * levels : int * samples : int * count : int -> IBackendTexture
    abstract member CreateTextureCube : size : int * format : TextureFormat * levels : int * samples : int -> IBackendTexture
    abstract member CreateTextureCubeArray : size : int * format : TextureFormat * levels : int * samples : int * count : int -> IBackendTexture

    abstract member ClearColor : texture : IBackendTexture * color : C4f -> unit
    abstract member ClearDepthStencil : texture : IBackendTexture * depth : Option<float> * stencil : Option<int> -> unit

    abstract member CreateTextureView : texture : IBackendTexture * levels : Range1i * slices : Range1i * isArray : bool -> IBackendTexture