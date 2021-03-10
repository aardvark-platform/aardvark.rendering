namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open System.Runtime.CompilerServices

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
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture(this : ITextureRuntime, dimension : TextureDimension,
                                format : aval<TextureFormat>, levels : aval<int>, samples : aval<int>, size : aval<V3i>) =
        AdaptiveTexture(this, dimension, size, format, levels = levels, samples = samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture(this : ITextureRuntime, dimension : TextureDimension,
                                format : TextureFormat, levels : aval<int>, samples : aval<int>, size : aval<V3i>) =
        AdaptiveTexture(this, dimension, size, ~~format, levels = levels, samples = samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture(this : ITextureRuntime, dimension : TextureDimension,
                                format : TextureFormat, levels : aval<int>, samples : int, size : aval<V3i>) =
        AdaptiveTexture(this, dimension, size, ~~format, levels = levels, samples = ~~samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture(this : ITextureRuntime, dimension : TextureDimension,
                                format : TextureFormat, levels : int, samples : int, size : aval<V3i>) =
        AdaptiveTexture(this, dimension, size, ~~format, levels = ~~levels, samples = ~~samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture(this : ITextureRuntime, dimension : TextureDimension,
                                format : aval<TextureFormat>, samples : aval<int>, size : aval<V3i>) =
        AdaptiveTexture(this, dimension, size, format, levels = ~~1, samples = samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture(this : ITextureRuntime, dimension : TextureDimension,
                                format : TextureFormat, samples : aval<int>, size : aval<V3i>) =
        AdaptiveTexture(this, dimension, size, ~~format, levels = ~~1, samples = samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture(this : ITextureRuntime, dimension : TextureDimension,
                                format : TextureFormat, samples : int, size : aval<V3i>) =
        AdaptiveTexture(this, dimension, size, ~~format, levels = ~~1, samples = ~~samples) :> IAdaptiveResource<_>

    // ================================================================================================================
    // All dimensions (array)
    // ================================================================================================================

    ///<summary>Creates an adaptive texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, dimension : TextureDimension,
                                     format : aval<TextureFormat>, levels : aval<int>, samples : aval<int>, size : aval<V3i>, count : aval<int>) =
        AdaptiveTextureArray(this, dimension, size, format, levels = levels, samples = samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, dimension : TextureDimension,
                                     format : TextureFormat, levels : aval<int>, samples : aval<int>, size : aval<V3i>, count : aval<int>) =
        AdaptiveTextureArray(this, dimension, size, ~~format, levels = levels, samples = samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, dimension : TextureDimension,
                                     format : TextureFormat, levels : aval<int>, samples : int, size : aval<V3i>, count : aval<int>) =
        AdaptiveTextureArray(this, dimension, size, ~~format, levels = levels, samples = ~~samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, dimension : TextureDimension,
                                     format : TextureFormat, levels : int, samples : int, size : aval<V3i>, count : aval<int>) =
        AdaptiveTextureArray(this, dimension, size, ~~format, levels = ~~levels, samples = ~~samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, dimension : TextureDimension,
                                     format : TextureFormat, levels : int, samples : int, size : aval<V3i>, count : int) =
        AdaptiveTextureArray(this, dimension, size, ~~format, levels = ~~levels, samples = ~~samples, count = ~~count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, dimension : TextureDimension,
                                     format : aval<TextureFormat>, samples : aval<int>, size : aval<V3i>, count : aval<int>) =
        AdaptiveTextureArray(this, dimension, size, format, levels = ~~1, samples = samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, dimension : TextureDimension,
                                     format : TextureFormat, samples : aval<int>, size : aval<V3i>, count : aval<int>) =
        AdaptiveTextureArray(this, dimension, size, ~~format, levels = ~~1, samples = samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, dimension : TextureDimension,
                                     format : TextureFormat, samples : int, size : aval<V3i>, count : aval<int>) =
        AdaptiveTextureArray(this, dimension, size, ~~format, levels = ~~1, samples = ~~samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="dimension">The dimension of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, dimension : TextureDimension,
                                     format : TextureFormat, samples : int, size : aval<V3i>, count : int) =
        AdaptiveTextureArray(this, dimension, size, ~~format, levels = ~~1, samples = ~~samples, count = ~~count) :> IAdaptiveResource<_>


    // ================================================================================================================
    // 1D Textures
    // ================================================================================================================

    ///<summary>Creates an adaptive 1D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture1D(this : ITextureRuntime,
                                  format : aval<TextureFormat>, levels : aval<int>, size : aval<int>) =
        AdaptiveTexture(this, TextureDimension.Texture1D, size |> AVal.mapNonAdaptive V3i, format, levels = levels, samples = ~~1) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 1D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture1D(this : ITextureRuntime,
                                  format : TextureFormat, levels : aval<int>, size : aval<int>) =
        AdaptiveTexture(this, TextureDimension.Texture1D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = levels, samples = ~~1) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 1D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture1D(this : ITextureRuntime,
                                  format : TextureFormat, levels : int, size : aval<int>) =
        AdaptiveTexture(this, TextureDimension.Texture1D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~levels, samples = ~~1) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 1D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture1D(this : ITextureRuntime,
                                  format : aval<TextureFormat>, size : aval<int>) =
        AdaptiveTexture(this, TextureDimension.Texture1D, size |> AVal.mapNonAdaptive V3i, format, levels = ~~1, samples = ~~1) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 1D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture1D(this : ITextureRuntime,
                                  format : TextureFormat, size : aval<int>) =
        AdaptiveTexture(this, TextureDimension.Texture1D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~1, samples = ~~1) :> IAdaptiveResource<_>

    // ================================================================================================================
    // 1D Textures (array)
    // ================================================================================================================

    ///<summary>Creates an adaptive 1D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture1DArray(this : ITextureRuntime,
                                       format : aval<TextureFormat>, levels : aval<int>, size : aval<int>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.Texture1D, size |> AVal.mapNonAdaptive V3i, format, levels = levels, samples = ~~1, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 1D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture1DArray(this : ITextureRuntime,
                                       format : TextureFormat, levels : aval<int>, size : aval<int>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.Texture1D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = levels, samples = ~~1, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 1D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture1DArray(this : ITextureRuntime,
                                       format : TextureFormat, levels : int, size : aval<int>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.Texture1D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~levels, samples = ~~1, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 1D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture1DArray(this : ITextureRuntime,
                                       format : TextureFormat, levels : int, size : aval<int>, count : int) =
        AdaptiveTextureArray(this, TextureDimension.Texture1D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~levels, samples = ~~1, count = ~~count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 1D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture1DArray(this : ITextureRuntime,
                                       format : aval<TextureFormat>, size : aval<int>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.Texture1D, size |> AVal.mapNonAdaptive V3i, format, levels = ~~1, samples = ~~1, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 1D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture1DArray(this : ITextureRuntime,
                                       format : TextureFormat, size : aval<int>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.Texture1D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~1, samples = ~~1, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 1D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture1DArray(this : ITextureRuntime,
                                       format : TextureFormat, size : aval<int>, count : int) =
        AdaptiveTextureArray(this, TextureDimension.Texture1D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~1, samples = ~~1, count = ~~count) :> IAdaptiveResource<_>


    // ================================================================================================================
    // 2D Textures
    // ================================================================================================================

    ///<summary>Creates an adaptive 2D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture2D(this : ITextureRuntime,
                                  format : aval<TextureFormat>, levels : aval<int>, samples : aval<int>, size : aval<V2i>) =
        AdaptiveTexture(this, TextureDimension.Texture2D, size |> AVal.mapNonAdaptive V3i, format, levels = levels, samples = samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 2D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture2D(this : ITextureRuntime,
                                  format : TextureFormat, levels : aval<int>, samples : aval<int>, size : aval<V2i>) =
        AdaptiveTexture(this, TextureDimension.Texture2D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = levels, samples = samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 2D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture2D(this : ITextureRuntime,
                                  format : TextureFormat, levels : aval<int>, samples : int, size : aval<V2i>) =
        AdaptiveTexture(this, TextureDimension.Texture2D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = levels, samples = ~~samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 2D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture2D(this : ITextureRuntime,
                                  format : TextureFormat, levels : int, samples : int, size : aval<V2i>) =
        AdaptiveTexture(this, TextureDimension.Texture2D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~levels, samples = ~~samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 2D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture2D(this : ITextureRuntime,
                                  format : aval<TextureFormat>, samples : aval<int>, size : aval<V2i>) =
        AdaptiveTexture(this, TextureDimension.Texture2D, size |> AVal.mapNonAdaptive V3i, format, levels = ~~1, samples = samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 2D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture2D(this : ITextureRuntime,
                                  format : TextureFormat, samples : aval<int>, size : aval<V2i>) =
        AdaptiveTexture(this, TextureDimension.Texture2D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~1, samples = samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 2D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture2D(this : ITextureRuntime,
                                  format : TextureFormat, samples : int, size : aval<V2i>) =
        AdaptiveTexture(this, TextureDimension.Texture2D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~1, samples = ~~samples) :> IAdaptiveResource<_>

    // ================================================================================================================
    // 2D Textures (array)
    // ================================================================================================================

    ///<summary>Creates an adaptive 2D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture2DArray(this : ITextureRuntime,
                                       format : aval<TextureFormat>, levels : aval<int>, samples : aval<int>, size : aval<V2i>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.Texture2D, size |> AVal.mapNonAdaptive V3i, format, levels = levels, samples = samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 2D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture2DArray(this : ITextureRuntime,
                                       format : TextureFormat, levels : aval<int>, samples : aval<int>, size : aval<V2i>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.Texture2D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = levels, samples = samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 2D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture2DArray(this : ITextureRuntime,
                                       format : TextureFormat, levels : aval<int>, samples : int, size : aval<V2i>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.Texture2D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = levels, samples = ~~samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 2D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture2DArray(this : ITextureRuntime,
                                       format : TextureFormat, levels : int, samples : int, size : aval<V2i>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.Texture2D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~levels, samples = ~~samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 2D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture2DArray(this : ITextureRuntime,
                                       format : TextureFormat, levels : int, samples : int, size : aval<V2i>, count : int) =
        AdaptiveTextureArray(this, TextureDimension.Texture2D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~levels, samples = ~~samples, count = ~~count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 2D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture2DArray(this : ITextureRuntime,
                                       format : aval<TextureFormat>, samples : aval<int>, size : aval<V2i>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.Texture2D, size |> AVal.mapNonAdaptive V3i, format, levels = ~~1, samples = samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 2D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture2DArray(this : ITextureRuntime,
                                       format : TextureFormat, samples : aval<int>, size : aval<V2i>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.Texture2D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~1, samples = samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 2D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture2DArray(this : ITextureRuntime,
                                       format : TextureFormat, samples : int, size : aval<V2i>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.Texture2D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~1, samples = ~~samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 2D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture2DArray(this : ITextureRuntime,
                                       format : TextureFormat, samples : int, size : aval<V2i>, count : int) =
        AdaptiveTextureArray(this, TextureDimension.Texture2D, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~1, samples = ~~samples, count = ~~count) :> IAdaptiveResource<_>


    // ================================================================================================================
    // 3D Textures
    // ================================================================================================================

    ///<summary>Creates an adaptive 3D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture3D(this : ITextureRuntime,
                                  format : aval<TextureFormat>, levels : aval<int>, size : aval<V3i>) =
        AdaptiveTexture(this, TextureDimension.Texture3D, size, format, levels = levels, samples = ~~1) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 3D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture3D(this : ITextureRuntime,
                                  format : TextureFormat, levels : aval<int>, size : aval<V3i>) =
        AdaptiveTexture(this, TextureDimension.Texture3D, size, ~~format, levels = levels, samples = ~~1) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 3D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture3D(this : ITextureRuntime,
                                  format : TextureFormat, levels : int, size : aval<V3i>) =
        AdaptiveTexture(this, TextureDimension.Texture3D, size, ~~format, levels = ~~levels, samples = ~~1) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 3D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture3D(this : ITextureRuntime,
                                  format : aval<TextureFormat>, size : aval<V3i>) =
        AdaptiveTexture(this, TextureDimension.Texture3D, size, format, levels = ~~1, samples = ~~1) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive 3D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTexture3D(this : ITextureRuntime,
                                  format : TextureFormat, size : aval<V3i>) =
        AdaptiveTexture(this, TextureDimension.Texture3D, size, ~~format, levels = ~~1, samples = ~~1) :> IAdaptiveResource<_>


    // ================================================================================================================
    // Cube Textures
    // ================================================================================================================

    ///<summary>Creates an adaptive Cube texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTextureCube(this : ITextureRuntime,
                                    format : aval<TextureFormat>, levels : aval<int>, samples : aval<int>, size : aval<int>) =
        AdaptiveTexture(this, TextureDimension.TextureCube, size |> AVal.mapNonAdaptive V3i, format, levels = levels, samples = samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive Cube texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTextureCube(this : ITextureRuntime,
                                    format : TextureFormat, levels : aval<int>, samples : aval<int>, size : aval<int>) =
        AdaptiveTexture(this, TextureDimension.TextureCube, size |> AVal.mapNonAdaptive V3i, ~~format, levels = levels, samples = samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive Cube texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTextureCube(this : ITextureRuntime,
                                    format : TextureFormat, levels : aval<int>, samples : int, size : aval<int>) =
        AdaptiveTexture(this, TextureDimension.TextureCube, size |> AVal.mapNonAdaptive V3i, ~~format, levels = levels, samples = ~~samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive Cube texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTextureCube(this : ITextureRuntime,
                                    format : TextureFormat, levels : int, samples : int, size : aval<int>) =
        AdaptiveTexture(this, TextureDimension.TextureCube, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~levels, samples = ~~samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive Cube texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTextureCube(this : ITextureRuntime,
                                    format : aval<TextureFormat>, samples : aval<int>, size : aval<int>) =
        AdaptiveTexture(this, TextureDimension.TextureCube, size |> AVal.mapNonAdaptive V3i, format, levels = ~~1, samples = samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive Cube texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTextureCube(this : ITextureRuntime,
                                    format : TextureFormat, samples : aval<int>, size : aval<int>) =
        AdaptiveTexture(this, TextureDimension.TextureCube, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~1, samples = samples) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive Cube texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    [<Extension>]
    static member CreateTextureCube(this : ITextureRuntime,
                                    format : TextureFormat, samples : int, size : aval<int>) =
        AdaptiveTexture(this, TextureDimension.TextureCube, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~1, samples = ~~samples) :> IAdaptiveResource<_>

    // ================================================================================================================
    // Cube Textures (array)
    // ================================================================================================================

    ///<summary>Creates an adaptive Cube texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime,
                                         format : aval<TextureFormat>, levels : aval<int>, samples : aval<int>, size : aval<int>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.TextureCube, size |> AVal.mapNonAdaptive V3i, format, levels = levels, samples = samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive Cube texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime,
                                         format : TextureFormat, levels : aval<int>, samples : aval<int>, size : aval<int>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.TextureCube, size |> AVal.mapNonAdaptive V3i, ~~format, levels = levels, samples = samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive Cube texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime,
                                         format : TextureFormat, levels : aval<int>, samples : int, size : aval<int>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.TextureCube, size |> AVal.mapNonAdaptive V3i, ~~format, levels = levels, samples = ~~samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive Cube texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime,
                                         format : TextureFormat, levels : int, samples : int, size : aval<int>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.TextureCube, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~levels, samples = ~~samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive Cube texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime,
                                         format : TextureFormat, levels : int, samples : int, size : aval<int>, count : int) =
        AdaptiveTextureArray(this, TextureDimension.TextureCube, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~levels, samples = ~~samples, count = ~~count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive Cube texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime,
                                         format : aval<TextureFormat>, samples : aval<int>, size : aval<int>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.TextureCube, size |> AVal.mapNonAdaptive V3i, format, levels = ~~1, samples = samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive Cube texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime,
                                         format : TextureFormat, samples : aval<int>, size : aval<int>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.TextureCube, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~1, samples = samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive Cube texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime,
                                         format : TextureFormat, samples : int, size : aval<int>, count : aval<int>) =
        AdaptiveTextureArray(this, TextureDimension.TextureCube, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~1, samples = ~~samples, count = count) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive Cube texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="samples">The number of samples.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime,
                                         format : TextureFormat, samples : int, size : aval<int>, count : int) =
        AdaptiveTextureArray(this, TextureDimension.TextureCube, size |> AVal.mapNonAdaptive V3i, ~~format, levels = ~~1, samples = ~~samples, count = ~~count) :> IAdaptiveResource<_>