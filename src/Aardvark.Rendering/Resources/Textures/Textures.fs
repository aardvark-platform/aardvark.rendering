﻿namespace Aardvark.Rendering

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open System.Runtime.InteropServices

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
    inherit IDisposable
    abstract member Update : format : PixFormat * size : V2i * data : nativeint -> unit
    abstract member UpdateAsync : format : PixFormat * size : V2i * data : nativeint -> Transaction
    abstract member ReadPixel : pos : V2i -> C4f

type IBackendTexture =
    inherit ITexture
    inherit IDisposable
    abstract member Runtime : ITextureRuntime
    abstract member Dimension : TextureDimension
    abstract member Format : TextureFormat
    abstract member Samples : int
    abstract member Count : int
    abstract member MipMapLevels : int
    abstract member Size : V3i
    abstract member Handle : obj

/// Interface for textures that are backed by exported memory.
/// Used for sharing textures between different backends.
and IExportedBackendTexture =
    inherit IBackendTexture
    inherit IExportedResource
    abstract member IsArray : bool

and IFramebufferOutput =
    abstract member Runtime : ITextureRuntime
    abstract member Format : TextureFormat
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
    inherit IDisposable
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

    ///<summary>Prepares a texture by uploading the data to the device.</summary>
    ///<param name="texture">The texture to prepare.</param>
    abstract member PrepareTexture : texture : ITexture -> IBackendTexture

    ///<summary>Copies multiple levels and slices from a texture to another.</summary>
    ///<param name="src">The texture to copy from.</param>
    ///<param name="srcBaseSlice">The first slice to copy from.</param>
    ///<param name="srcBaseLevel">The first level to copy from.</param>
    ///<param name="dst">The texture to copy to.</param>
    ///<param name="dstBaseSlice">The first slice to copy to.</param>
    ///<param name="dstBaseLevel">The first level to copy to.</param>
    ///<param name="slices">The number of slices to copy.</param>
    ///<param name="levels">The number of mip levels to copy.</param>
    abstract member Copy : src : IBackendTexture * srcBaseSlice : int * srcBaseLevel : int *
                           dst : IBackendTexture * dstBaseSlice : int * dstBaseLevel : int *
                           slices : int * levels : int -> unit

    ///<summary>
    /// Copies a region from a framebuffer output to a region of another, performing any scaling if required.
    /// If the extent of any dimension is positive in the source region and negative in the target region (or vice versa), the contents are mirrored in that dimension.
    ///</summary>
    ///<param name="src">The framebuffer output to copy from.</param>
    ///<param name="srcRegion">The region to copy from.</param>
    ///<param name="dst">The framebuffer output to copy to.</param>
    ///<param name="dstRegion">The region to copy to.</param>
    abstract member Blit : src : IFramebufferOutput * srcRegion : Box3i *
                           dst : IFramebufferOutput * dstRegion : Box3i -> unit

    ///<summary>Creates a renderbuffer.</summary>
    ///<param name="size">The size of the renderbuffer.</param>
    ///<param name="format">The desired renderbuffer format.</param>
    ///<param name="samples">The number of samples. Default is 1.</param>
    ///<exception cref="ArgumentException">if <paramref name="samples"/> is less than 1.</exception>
    abstract member CreateRenderbuffer : size : V2i * format : TextureFormat *
                                         [<Optional; DefaultParameterValue(1)>] samples : int -> IRenderbuffer

    abstract member CreateStreamingTexture : mipMaps : bool -> IStreamingTexture

    abstract member CreateSparseTexture<'T when 'T : unmanaged> : size : V3i * levels : int * slices : int *
                                                                  dimension : TextureDimension * format : Col.Format *
                                                                  brickSize : V3i * maxMemory : int64 -> ISparseTexture<'T>

    abstract member GenerateMipMaps : IBackendTexture -> unit

    ///<summary>Downloads data from the given texture sub resource to a NativeTensor4.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The NativeTensor4 to copy the data to.</param>
    ///<param name="format">The color format of the target tensor.</param>
    ///<param name="offset">The minimum coordinate to download. Default is V3i.Zero.</param>
    ///<param name="size">The size of the texture region to download or V3i.Zero for the target size. Default is V3i.Zero.</param>
    abstract member Download : texture : ITextureSubResource * target : NativeTensor4<'T> * format : Col.Format *
                               [<Optional; DefaultParameterValue(V3i())>] offset : V3i *
                               [<Optional; DefaultParameterValue(V3i())>] size : V3i -> unit

    ///<summary>Uploads data from a NativeTensor4 to the given texture sub resource.</summary>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The NativeTensor4 containing the data to upload.</param>
    ///<param name="format">The color format of the source tensor.</param>
    ///<param name="offset">The minimum coordinate to update. Default is V3i.Zero.</param>
    ///<param name="size">The size of the texture region to update or V3i.Zero for the source size. Default is V3i.Zero.</param>
    abstract member Upload : texture : ITextureSubResource * source : NativeTensor4<'T> * format : Col.Format *
                             [<Optional; DefaultParameterValue(V3i())>] offset : V3i *
                             [<Optional; DefaultParameterValue(V3i())>] size : V3i -> unit

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The matrix to copy the data to.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="offset">The minimum coordinate to download. Default is V2i.Zero.</param>
    abstract member DownloadStencil : texture : IBackendTexture * target : Matrix<int> *
                                      [<Optional; DefaultParameterValue(0)>] level : int *
                                      [<Optional; DefaultParameterValue(0)>] slice : int *
                                      [<Optional; DefaultParameterValue(V2i())>] offset : V2i -> unit

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The matrix to copy the data to.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="offset">The minimum coordinate to download. Default is V2i.Zero.</param>
    abstract member DownloadDepth : texture : IBackendTexture * target : Matrix<float32> *
                                    [<Optional; DefaultParameterValue(0)>] level : int *
                                    [<Optional; DefaultParameterValue(0)>] slice : int *
                                    [<Optional; DefaultParameterValue(V2i())>] offset : V2i -> unit

    /// Clears the texture with the given values.
    abstract member Clear : texture: IBackendTexture * values: ClearValues -> unit

    abstract member CreateTextureView : texture : IBackendTexture * levels : Range1i * slices : Range1i * isArray : bool -> IBackendTexture