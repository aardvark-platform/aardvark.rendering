namespace Aardvark.Base

open Aardvark.Base
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.Base.Rendering
open System.Runtime.CompilerServices

[<AutoOpen>]
module private AdaptiveTextureTypes =

    type TextureParams2D = {
            size : V2i
            format : TextureFormat
            levels : int
            samples : int
        }

    type TextureArrayParams2D = {
            texture : TextureParams2D
            count : int
        }

    type TextureParamsCube = {
            size : int
            format : TextureFormat
            levels : int
            samples : int
        }

    type TextureArrayParamsCube = {
            texture : TextureParamsCube
            count : int
        }

    [<AbstractClass>]
    type AbstractAdaptiveTexture<'a when 'a : equality>(runtime : ITextureRuntime) =
        inherit AdaptiveResource<ITexture>()

        let mutable handle : Option<IBackendTexture * 'a> = None

        abstract member GetParams : AdaptiveToken -> 'a
        abstract member CreateTexture : ITextureRuntime * 'a -> IBackendTexture

        member private x.CreateHandle(runtime : ITextureRuntime, textureParams : 'a) =
            let tex = x.CreateTexture(runtime, textureParams)
            handle <- Some (tex, textureParams)
            tex :> ITexture

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
                h :> ITexture

            | Some (h, _) ->
                t.ReplacedResource(ResourceKind.Texture)
                runtime.DeleteTexture h
                x.CreateHandle(runtime, textureParams)

            | None ->
                t.CreatedResource(ResourceKind.Texture)
                x.CreateHandle(runtime, textureParams)


    type AdaptiveTexture(runtime : ITextureRuntime, size : aval<V2i>, format : aval<TextureFormat>, levels : aval<int>, samples : aval<int>) =
        inherit AbstractAdaptiveTexture<TextureParams2D>(runtime)

        override x.GetParams(token : AdaptiveToken) =
            {
                size = size.GetValue token
                format = format.GetValue token
                levels = levels.GetValue token
                samples = samples.GetValue token
            }

        override x.CreateTexture(runtime : ITextureRuntime, p : TextureParams2D) =
            runtime.CreateTexture(p.size, p.format, p.levels, p.samples)


    type AdaptiveTextureArray(runtime : ITextureRuntime, size : aval<V2i>, format : aval<TextureFormat>, levels : aval<int>, samples : aval<int>, count : aval<int>) =
        inherit AbstractAdaptiveTexture<TextureArrayParams2D>(runtime)

        override x.GetParams(token : AdaptiveToken) =
            let texture : TextureParams2D =
                {
                    size = size.GetValue token
                    format = format.GetValue token
                    levels = levels.GetValue token
                    samples = samples.GetValue token
                }

            { texture = texture; count = count.GetValue token }

        override x.CreateTexture(runtime : ITextureRuntime, p : TextureArrayParams2D) =
            runtime.CreateTextureArray(p.texture.size, p.texture.format, p.texture.levels, p.texture.samples, p.count)


    type AdaptiveCubeTexture(runtime : ITextureRuntime, size : aval<int>, format : aval<TextureFormat>, levels : aval<int>, samples : aval<int>) =
        inherit AbstractAdaptiveTexture<TextureParamsCube>(runtime)

        override x.GetParams(token : AdaptiveToken) =
            {
                size = size.GetValue token
                format = format.GetValue token
                levels = levels.GetValue token
                samples = samples.GetValue token
            }

        override x.CreateTexture(runtime : ITextureRuntime, p : TextureParamsCube) =
            runtime.CreateTextureCube(p.size, p.format, p.levels, p.samples)


    type AdaptiveCubeTextureArray(runtime : ITextureRuntime, size : aval<int>, format : aval<TextureFormat>, levels : aval<int>, samples : aval<int>, count : aval<int>) =
        inherit AbstractAdaptiveTexture<TextureArrayParamsCube>(runtime)

        override x.GetParams(token : AdaptiveToken) =
            let texture : TextureParamsCube =
                {
                    size = size.GetValue token
                    format = format.GetValue token
                    levels = levels.GetValue token
                    samples = samples.GetValue token
                }

            { texture = texture; count = count.GetValue token }

        override x.CreateTexture(runtime : ITextureRuntime, p : TextureArrayParamsCube) =
            runtime.CreateTextureCubeArray(p.texture.size, p.texture.format, p.texture.levels, p.texture.samples, p.count)


[<AbstractClass; Sealed; Extension>]
type ITextureRuntimeAdaptiveTextureExtensions private() =

    // CreateTexture
    [<Extension>]
    static member CreateTexture(this : ITextureRuntime, format : aval<TextureFormat>, levels : aval<int>, samples : aval<int>, size : aval<V2i>) =
        AdaptiveTexture(this, size, format, levels, samples) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTexture(this : ITextureRuntime, format : TextureFormat, levels : int, samples : int, size : aval<V2i>) =
        AdaptiveTexture(this, size, ~~format, ~~levels, ~~samples) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTexture(this : ITextureRuntime, format : aval<TextureFormat>, samples : aval<int>, size : aval<V2i>) =
        AdaptiveTexture(this, size, format, ~~1, samples) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTexture(this : ITextureRuntime, format : TextureFormat, samples : aval<int>, size : aval<V2i>) =
        AdaptiveTexture(this, size, ~~format, ~~1, samples) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTexture(this : ITextureRuntime, format : TextureFormat, samples : int, size : aval<V2i>) =
        AdaptiveTexture(this, size, ~~format, ~~1, ~~samples) :> IAdaptiveResource<_>

    // CreateTextureArray
    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, format : aval<TextureFormat>, levels : aval<int>, samples : aval<int>, size : aval<V2i>, count : aval<int>) =
        AdaptiveTextureArray(this, size, format, levels, samples, count) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, format : TextureFormat, levels : int, samples : int, size : aval<V2i>, count : aval<int>) =
        AdaptiveTextureArray(this, size, ~~format, ~~levels, ~~samples, count) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, format : aval<TextureFormat>, samples : aval<int>, size : aval<V2i>, count : aval<int>) =
        AdaptiveTextureArray(this, size, format, ~~1, samples, count) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, format : TextureFormat, samples : aval<int>, size : aval<V2i>, count : aval<int>) =
        AdaptiveTextureArray(this, size, ~~format, ~~1, samples, count) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTextureArray(this : ITextureRuntime, format : TextureFormat, samples : int, size : aval<V2i>, count : aval<int>) =
        AdaptiveTextureArray(this, size, ~~format, ~~1, ~~samples, count) :> IAdaptiveResource<_>

    // CreateTextureCube
    [<Extension>]
    static member CreateTextureCube(this : ITextureRuntime, format : aval<TextureFormat>, levels : aval<int>, samples : aval<int>, size : aval<int>) =
        AdaptiveCubeTexture(this, size, format, levels, samples) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTextureCube(this : ITextureRuntime, format : TextureFormat, levels : int, samples : int, size : aval<int>) =
        AdaptiveCubeTexture(this, size, ~~format, ~~levels, ~~samples) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTextureCube(this : ITextureRuntime, format : aval<TextureFormat>, samples : aval<int>, size : aval<int>) =
        AdaptiveCubeTexture(this, size, format, ~~1, samples) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTextureCube(this : ITextureRuntime, format : aval<TextureFormat>, samples : int, size : aval<int>) =
        AdaptiveCubeTexture(this, size, format, ~~1, ~~samples) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTextureCube(this : ITextureRuntime, format : TextureFormat, samples : int, size : aval<int>) =
        AdaptiveCubeTexture(this, size, ~~format, ~~1, ~~samples) :> IAdaptiveResource<_>

    // CreateTextureCubeArray
    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime, format : aval<TextureFormat>, levels : aval<int>, samples : aval<int>, size : aval<int>, count : aval<int>) =
        AdaptiveCubeTextureArray(this, size, format, levels, samples, count) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime, format : TextureFormat, levels : int, samples : int, size : aval<int>, count : aval<int>) =
        AdaptiveCubeTextureArray(this, size, ~~format, ~~levels, ~~samples, count) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime, format : aval<TextureFormat>, samples : aval<int>, size : aval<int>, count : aval<int>) =
        AdaptiveCubeTextureArray(this, size, format, ~~1, samples, count) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime, format : aval<TextureFormat>, samples : int, size : aval<int>, count : aval<int>) =
        AdaptiveCubeTextureArray(this, size, format, ~~1, ~~samples, count) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime, format : TextureFormat, samples : int, size : aval<int>, count : aval<int>) =
        AdaptiveCubeTextureArray(this, size, ~~format, ~~1, ~~samples, count) :> IAdaptiveResource<_>