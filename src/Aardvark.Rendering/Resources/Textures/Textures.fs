namespace Aardvark.Rendering

open System
open Aardvark.Base
open FSharp.Data.Adaptive

type TextureAspect =
    | Color
    | Depth
    | Stencil

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TextureAspect =

    /// Returns the aspect corresponding to the given format.
    let ofTextureFormat (format : TextureFormat) =
        if TextureFormat.hasDepth format then TextureAspect.Depth
        else TextureAspect.Color


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

    ///<summary>Creates a texture.</summary>
    ///<param name="size">The size of the texture.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<exception cref="ArgumentException">if the combination of parameters is invalid.</exception>
    abstract member CreateTexture : size : V3i * dimension : TextureDimension * format : TextureFormat * levels : int * samples : int -> IBackendTexture

    ///<summary>Creates a texture array.</summary>
    ///<param name="size">The size of the texture.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="count">The number of texture slices.</param>
    ///<exception cref="ArgumentException">if the combination of parameters is invalid.</exception>
    abstract member CreateTextureArray : size : V3i * dimension : TextureDimension * format : TextureFormat * levels : int * samples : int * count : int -> IBackendTexture

    abstract member PrepareTexture : ITexture -> IBackendTexture

    abstract member Copy : src : NativeTensor4<'a> * srcFormat : Col.Format * dst : ITextureSubResource * dstOffset : V3i * size : V3i -> unit
    abstract member Copy : src : ITextureSubResource * srcOffset : V3i * dst : NativeTensor4<'a> * dstFormat : Col.Format * size : V3i -> unit
    abstract member Copy : src : IFramebufferOutput * srcOffset : V3i * dst : IFramebufferOutput * dstOffset : V3i * size : V3i -> unit

    abstract member DeleteTexture : IBackendTexture -> unit

    ///<summary>Creates a renderbuffer.</summary>
    ///<param name="size">The size of the renderbuffer.</param>
    ///<param name="format">The desired renderbuffer format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<exception cref="ArgumentException">if <paramref name="samples"/> is less than 1.</exception>
    abstract member CreateRenderbuffer : size : V2i * format : RenderbufferFormat * samples : int -> IRenderbuffer
    abstract member DeleteRenderbuffer : IRenderbuffer -> unit

    abstract member CreateStreamingTexture : mipMaps : bool -> IStreamingTexture
    abstract member DeleteStreamingTexture : IStreamingTexture -> unit

    abstract member CreateSparseTexture<'a when 'a : unmanaged> : size : V3i * levels : int * slices : int * dim : TextureDimension * format : Col.Format * brickSize : V3i * maxMemory : int64 -> ISparseTexture<'a>

    abstract member GenerateMipMaps : IBackendTexture -> unit
    abstract member ResolveMultisamples : src : IFramebufferOutput * target : IBackendTexture * imgTrafo : ImageTrafo -> unit

    ///<summary>Uploads data from a PixImage to the given texture.</summary>
    ///<param name="texture">The texture to update.</param>
    ///<param name="level">The texture level to update.</param>
    ///<param name="slice">The texture slice to update.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    abstract member Upload : texture : IBackendTexture * level : int * slice : int * offset : V2i * source : PixImage -> unit

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    ///<param name="target">The PixImage to copy the data to.</param>
    abstract member Download : texture : IBackendTexture * level : int * slice : int * offset : V2i * target : PixImage -> unit

    ///<summary>Downloads color data from the given texture to a PixVolume.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    ///<param name="target">The PixVolume to copy the data to.</param>
    abstract member Download : texture : IBackendTexture * level : int * slice : int * offset : V3i * target : PixVolume -> unit

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    ///<param name="target">The matrix to copy the data to.</param>
    abstract member DownloadStencil : texture : IBackendTexture * level : int * slice : int * offset : V2i * target : Matrix<int> -> unit

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    ///<param name="target">The matrix to copy the data to.</param>
    abstract member DownloadDepth : texture : IBackendTexture * level : int * slice : int * offset : V2i * target : Matrix<float32> -> unit

    abstract member Copy : src : IBackendTexture * srcBaseSlice : int * srcBaseLevel : int *
                           dst : IBackendTexture * dstBaseSlice : int * dstBaseLevel : int *
                           slices : int * levels : int -> unit

    abstract member ClearColor : texture : IBackendTexture * color : C4f -> unit
    abstract member ClearDepthStencil : texture : IBackendTexture * depth : Option<float> * stencil : Option<int> -> unit

    abstract member CreateTextureView : texture : IBackendTexture * levels : Range1i * slices : Range1i * isArray : bool -> IBackendTexture