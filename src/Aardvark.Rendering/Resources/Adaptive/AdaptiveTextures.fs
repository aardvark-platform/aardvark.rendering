namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

[<AutoOpen>]
module private AdaptiveTextureTypes =

    type TextureParams = {
            Size : V3i
            Format : TextureFormat
            Levels : int
            Samples : int
        }

    type TextureArrayParams = {
            Texture : TextureParams
            Count : int
        }

    [<AbstractClass>]
    type AbstractAdaptiveTexture<'Params when 'Params : equality>(runtime : ITextureRuntime) =
        inherit AdaptiveResource<IBackendTexture>()

        let mutable handle : Option<IBackendTexture * 'Params> = None

        abstract member GetParams : AdaptiveToken -> 'Params
        abstract member CreateTexture : ITextureRuntime * 'Params -> IBackendTexture

        member private x.CreateHandle(runtime : ITextureRuntime, textureParams : 'Params) =
            let tex = x.CreateTexture(runtime, textureParams)
            handle <- Some (tex, textureParams)
            tex

        override x.Create() = ()
        override x.Destroy() =
            match handle with
            | Some (h, _) ->
                runtime.DeleteTexture h
                handle <- None
            | None ->
                ()

        override x.Compute(token : AdaptiveToken, t : RenderToken) =

            let textureParams = x.GetParams token

            match handle with
            | Some (h, p) when textureParams = p ->
                h

            | Some (h, _) ->
                t.ReplacedResource(ResourceKind.Texture)
                runtime.DeleteTexture h
                x.CreateHandle(runtime, textureParams)

            | None ->
                t.CreatedResource(ResourceKind.Texture)
                x.CreateHandle(runtime, textureParams)


    type AdaptiveTexture(runtime : ITextureRuntime, dimension : TextureDimension,
                         size : aval<V3i>, format : aval<TextureFormat>, levels : aval<int>, samples : aval<int>) =
        inherit AbstractAdaptiveTexture<TextureParams>(runtime)

        override x.GetParams(token : AdaptiveToken) =
            {
                Size = size.GetValue token
                Format = format.GetValue token
                Levels = levels.GetValue token
                Samples = samples.GetValue token
            }

        override x.CreateTexture(runtime : ITextureRuntime, p : TextureParams) =
            runtime.CreateTexture(p.Size, dimension, p.Format, levels = p.Levels, samples = p.Samples)

    type AdaptiveTextureArray(runtime : ITextureRuntime, dimension : TextureDimension,
                              size : aval<V3i>, format : aval<TextureFormat>, levels : aval<int>, samples : aval<int>, count : aval<int>) =
        inherit AbstractAdaptiveTexture<TextureArrayParams>(runtime)

        override x.GetParams(token : AdaptiveToken) =
            let texture : TextureParams =
                {
                    Size = size.GetValue token
                    Format = format.GetValue token
                    Levels = levels.GetValue token
                    Samples = samples.GetValue token
                }

            { Texture = texture; Count = count.GetValue token }

        override x.CreateTexture(runtime : ITextureRuntime, p : TextureArrayParams) =
            runtime.CreateTextureArray(p.Texture.Size, dimension, p.Texture.Format, levels = p.Texture.Levels, samples = p.Texture.Samples, count = p.Count)


[<AbstractClass; Sealed; Extension>]
type ITextureRuntimeAdaptiveTextureExtensions private() =

    // ================================================================================================================
    // All dimensions
    // ================================================================================================================

    ///<summary>Creates an adaptive texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    [<Extension>]
    static member CreateTexture(this : ITextureRuntime, size : aval<V3i>, dimension : TextureDimension,
                                format : TextureFormat, levels : aval<int>, samples : aval<int>) =
        AdaptiveTexture(this, dimension, size, ~~format, levels = levels, samples = samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    ///<param name="samples">The number of samples. Default is 1.</param>
    [<Extension>]
    static member CreateTexture(this : ITextureRuntime, size : aval<V3i>, dimension : TextureDimension, format : TextureFormat,
                                [<Optional; DefaultParameterValue(1)>] levels : int,
                                [<Optional; DefaultParameterValue(1)>] samples : int) =
        AdaptiveTexture(this, dimension, size, ~~format, levels = ~~levels, samples = ~~samples) :> IAdaptiveResource<_>

    // ================================================================================================================
    // All dimensions (array)
    // ================================================================================================================

    ///<summary>Creates an adaptive texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, size : aval<V3i>, dimension : TextureDimension,
                                     format : TextureFormat, levels : aval<int>, samples : aval<int>, count : aval<int>) =
        AdaptiveTextureArray(this, dimension, size, ~~format, levels = levels, samples = samples, count = count) :> IAdaptiveResource<_>


    ///<summary>Creates an adaptive texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    ///<param name="samples">The number of samples. Default is 1.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, size : aval<V3i>, dimension : TextureDimension, format : TextureFormat,
                                     [<Optional; DefaultParameterValue(1)>] levels : int,
                                     [<Optional; DefaultParameterValue(1)>] samples : int,
                                     count : aval<int>) =
        AdaptiveTextureArray(this, dimension, size, ~~format, levels = ~~levels, samples = ~~samples, count = count) :> IAdaptiveResource<_>


    ///<summary>Creates an adaptive texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    ///<param name="samples">The number of samples. Default is 1.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, size : aval<V3i>, dimension : TextureDimension, format : TextureFormat,
                                     [<Optional; DefaultParameterValue(1)>] levels : int,
                                     [<Optional; DefaultParameterValue(1)>] samples : int,
                                     count : int) =
        AdaptiveTextureArray(this, dimension, size, ~~format, levels = ~~levels, samples = ~~samples, count = ~~count) :> IAdaptiveResource<_>

    // ================================================================================================================
    // 1D Textures
    // ================================================================================================================

    ///<summary>Creates an adaptive 1D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    [<Extension>]
    static member CreateTexture1D(this : ITextureRuntime, size : aval<int>, format : TextureFormat, levels : aval<int>) =
        let size = size |> AVal.mapNonAdaptive (fun s -> V3i(s, 1, 1))
        AdaptiveTexture(this, TextureDimension.Texture1D, size, ~~format, levels = levels, samples = ~~1) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 1D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    [<Extension>]
    static member CreateTexture1D(this : ITextureRuntime, size : aval<int>, format : TextureFormat,
                                  [<Optional; DefaultParameterValue(1)>] levels : int) =
        this.CreateTexture1D(size, format, levels = ~~levels)

    // ================================================================================================================
    // 1D Textures (array)
    // ================================================================================================================

    ///<summary>Creates an adaptive 1D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture1DArray(this : ITextureRuntime,
                                       size : aval<int>, format : TextureFormat, levels : aval<int>, count : aval<int>) =
        let size = size |> AVal.mapNonAdaptive (fun s -> V3i(s, 1, 1))
        AdaptiveTextureArray(this, TextureDimension.Texture1D, size, ~~format, levels = levels, samples = ~~1, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 1D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture1DArray(this : ITextureRuntime,
                                       size : aval<int>, format : TextureFormat,
                                       [<Optional; DefaultParameterValue(1)>] levels : int,
                                       count : aval<int>) =
        this.CreateTexture1DArray(size, format, levels = ~~levels, count = count)

    ///<summary>Creates an adaptive 1D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture1DArray(this : ITextureRuntime,
                                       size : aval<int>, format : TextureFormat,
                                       [<Optional; DefaultParameterValue(1)>] levels : int,
                                       count : int) =
        this.CreateTexture1DArray(size, format, levels = ~~levels, count = ~~count)

    // ================================================================================================================
    // 2D Textures
    // ================================================================================================================

    ///<summary>Creates an adaptive 2D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    [<Extension>]
    static member CreateTexture2D(this : ITextureRuntime,
                                  size : aval<V2i>, format : TextureFormat, levels : aval<int>, samples : aval<int>) =
        let size = size |> AVal.mapNonAdaptive (fun s -> V3i(s, 1))
        AdaptiveTexture(this, TextureDimension.Texture2D, size, ~~format, levels = levels, samples = samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 2D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    ///<param name="samples">The number of samples. Default is 1.</param>
    [<Extension>]
    static member CreateTexture2D(this : ITextureRuntime,
                                  size : aval<V2i>, format : TextureFormat,
                                  [<Optional; DefaultParameterValue(1)>] levels : int,
                                  [<Optional; DefaultParameterValue(1)>] samples : int) =
        this.CreateTexture2D(size, format, levels = ~~levels, samples = ~~samples)

    // ================================================================================================================
    // 2D Textures (array)
    // ================================================================================================================

    ///<summary>Creates an adaptive 2D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture2DArray(this : ITextureRuntime,
                                       size : aval<V2i>, format : TextureFormat, levels : aval<int>, samples : aval<int>, count : aval<int>) =
        let size = size |> AVal.mapNonAdaptive (fun s -> V3i(s, 1))
        AdaptiveTextureArray(this, TextureDimension.Texture2D, size, ~~format, levels = levels, samples = samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 2D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    ///<param name="samples">The number of samples. Default is 1.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture2DArray(this : ITextureRuntime,
                                       size : aval<V2i>, format : TextureFormat,
                                       [<Optional; DefaultParameterValue(1)>] levels : int,
                                       [<Optional; DefaultParameterValue(1)>] samples : int,
                                       count : aval<int>) =
        this.CreateTexture2DArray(size, format, levels = ~~levels, samples = ~~samples, count = count)

    ///<summary>Creates an adaptive 2D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    ///<param name="samples">The number of samples. Default is 1.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture2DArray(this : ITextureRuntime,
                                       size : aval<V2i>, format : TextureFormat,
                                       [<Optional; DefaultParameterValue(1)>] levels : int,
                                       [<Optional; DefaultParameterValue(1)>] samples : int,
                                       count : int) =
        this.CreateTexture2DArray(size, format, levels = ~~levels, samples = ~~samples, count = ~~count)

    // ================================================================================================================
    // 3D Textures
    // ================================================================================================================

    ///<summary>Creates an adaptive 3D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    [<Extension>]
    static member CreateTexture3D(this : ITextureRuntime,
                                  size : aval<V3i>, format : TextureFormat, levels : aval<int>) =
        AdaptiveTexture(this, TextureDimension.Texture3D, size, ~~format, levels = levels, samples = ~~1) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 3D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    [<Extension>]
    static member CreateTexture3D(this : ITextureRuntime,
                                  size : aval<V3i>, format : TextureFormat,
                                  [<Optional; DefaultParameterValue(1)>] levels : int) =
        this.CreateTexture3D(size, format, levels = ~~levels)

    // ================================================================================================================
    // Cube Textures
    // ================================================================================================================

    ///<summary>Creates an adaptive cube texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    [<Extension>]
    static member CreateTextureCube(this : ITextureRuntime,
                                    size : aval<int>, format : TextureFormat, levels : aval<int>) =
        let size = size |> AVal.mapNonAdaptive (fun s -> V3i(s, s, 1))
        AdaptiveTexture(this, TextureDimension.TextureCube, size, ~~format, levels = levels, samples = ~~1) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive cube texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    [<Extension>]
    static member CreateTextureCube(this : ITextureRuntime,
                                    size : aval<int>, format : TextureFormat,
                                    [<Optional; DefaultParameterValue(1)>] levels : int) =
        this.CreateTextureCube(size, format, levels = ~~levels)

    // ================================================================================================================
    // Cube Textures (array)
    // ================================================================================================================

    ///<summary>Creates an adaptive cube texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime,
                                         size : aval<int>, format : TextureFormat, levels : aval<int>, count : aval<int>) =
        let size = size |> AVal.mapNonAdaptive (fun s -> V3i(s, s, 1))
        AdaptiveTextureArray(this, TextureDimension.TextureCube, size, ~~format, levels = levels, samples = ~~1, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive cube texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime,
                                         size : aval<int>, format : TextureFormat,
                                         [<Optional; DefaultParameterValue(1)>] levels : int,
                                         count : aval<int>) =
        this.CreateTextureCubeArray(size, format, levels = ~~levels, count = count)

    ///<summary>Creates an adaptive cube texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime,
                                         size : aval<int>, format : TextureFormat,
                                         [<Optional; DefaultParameterValue(1)>] levels : int,
                                         count : int) =
        this.CreateTextureCubeArray(size, format, levels = ~~levels, count = ~~count)