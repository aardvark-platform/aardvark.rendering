namespace Aardvark.Base

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open System.Runtime.CompilerServices

type private AdaptiveTexture(runtime : ITextureRuntime, format : TextureFormat, samples : int, size : aval<V2i>) =
    inherit AbstractOutputMod<ITexture>()

    let mutable handle : Option<IBackendTexture> = None

    override x.Create() = ()
    override x.Destroy() =
        match handle with
        | Some h ->
            runtime.DeleteTexture(h)
            handle <- None
        | None ->
            ()

    override x.Compute(token : AdaptiveToken, t : RenderToken) =
        let size = size.GetValue(token)

        match handle with
        | Some h when h.Size.XY = size ->
            h :> ITexture

        | Some h ->
            t.ReplacedResource(ResourceKind.Texture)
            runtime.DeleteTexture(h)
            let tex = runtime.CreateTexture(size, format, 1, samples)
            handle <- Some tex
            tex :> ITexture

        | None ->
            t.CreatedResource(ResourceKind.Texture)
            let tex = runtime.CreateTexture(size, format, 1, samples)
            handle <- Some tex
            tex :> ITexture

type private AdaptiveCubeTexture(runtime : ITextureRuntime, format : TextureFormat, samples : int, size : aval<int>) =
    inherit AbstractOutputMod<ITexture>()

    let mutable handle : Option<IBackendTexture> = None

    override x.Create() = ()
    override x.Destroy() =
        match handle with
        | Some h ->
            runtime.DeleteTexture(h)
            handle <- None
        | None ->
            ()

    override x.Compute(token : AdaptiveToken, t : RenderToken) =
        let size = size.GetValue(token)

        match handle with
        | Some h when h.Size.X = size ->
            h :> ITexture

        | Some h ->
            t.ReplacedResource(ResourceKind.Texture)
            runtime.DeleteTexture(h)
            let tex = runtime.CreateTextureCube(size, format, 1, samples)
            handle <- Some tex
            tex :> ITexture

        | None ->
            t.CreatedResource(ResourceKind.Texture)
            let tex = runtime.CreateTextureCube(size, format, 1, samples)
            handle <- Some tex
            tex :> ITexture

type private AdaptiveRenderbuffer(runtime : ITextureRuntime, format : RenderbufferFormat, samples : int, size : aval<V2i>) =
    inherit AbstractOutputMod<IRenderbuffer>()

    let mutable handle : Option<IRenderbuffer> = None

    override x.Create() = ()
    override x.Destroy() =
        match handle with
        | Some h ->
            runtime.DeleteRenderbuffer(h)
            handle <- None
        | None ->
            ()

    override x.Compute(token : AdaptiveToken, t : RenderToken) =
        let size = size.GetValue(token)

        match handle with
        | Some h when h.Size = size ->
            h

        | Some h ->
            t.ReplacedResource(ResourceKind.Renderbuffer)
            runtime.DeleteRenderbuffer(h)
            let tex = runtime.CreateRenderbuffer(size, format, samples)
            handle <- Some tex
            tex

        | None ->
            t.CreatedResource(ResourceKind.Renderbuffer)
            let tex = runtime.CreateRenderbuffer(size, format, samples)
            handle <- Some tex
            tex

[<AbstractClass>]
type private AbstractAdaptiveFramebufferOutput(resource : IOutputMod) =
    inherit AbstractOutputMod<IFramebufferOutput>()

    override x.Create() = resource.Acquire()
    override x.Destroy() = resource.Release()

type private AdaptiveTextureAttachment(texture : IOutputMod<ITexture>, slice : int) =
    inherit AbstractAdaptiveFramebufferOutput(texture)
    override x.Compute(token : AdaptiveToken, t : RenderToken) =
        let tex = texture.GetValue(token, t)
        { texture = unbox tex; slice = slice; level = 0 } :> IFramebufferOutput

type private AdaptiveRenderbufferAttachment(renderbuffer : IOutputMod<IRenderbuffer>) =
    inherit AbstractAdaptiveFramebufferOutput(renderbuffer)
    override x.Compute(token : AdaptiveToken, t : RenderToken) =
        let rb = renderbuffer.GetValue(token, t)
        rb :> IFramebufferOutput


[<AbstractClass; Sealed; Extension>]
type ITextureRuntimeAdaptiveExtensions private() =

    [<Extension>]
    static member CreateTexture(this : ITextureRuntime, format : TextureFormat, samples : int, size : aval<V2i>) =
        AdaptiveTexture(this, format, samples, size) :> IOutputMod<_>

    [<Extension>]
    static member CreateTextureCube(this : ITextureRuntime, format : TextureFormat, samples : int, size : aval<int>) =
        AdaptiveCubeTexture(this, format, samples, size) :> IOutputMod<_>

    [<Extension>]
    static member CreateRenderbuffer(this : ITextureRuntime, format : RenderbufferFormat, samples : int, size : aval<V2i>) =
        AdaptiveRenderbuffer(this, format, samples, size) :> IOutputMod<_>

    [<Extension>]
    static member CreateTextureAttachment(_ : ITextureRuntime, texture : IOutputMod<ITexture>, slice : int) =
        AdaptiveTextureAttachment(texture, slice) :> IOutputMod<_>

    [<Extension>]
    static member CreateRenderbufferAttachment(_ : ITextureRuntime, renderbuffer : IOutputMod<IRenderbuffer>) =
        AdaptiveRenderbufferAttachment(renderbuffer) :> IOutputMod<_>