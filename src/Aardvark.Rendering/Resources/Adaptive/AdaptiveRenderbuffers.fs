namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.Base.Rendering
open System.Runtime.CompilerServices

[<AutoOpen>]
module private AdaptiveRenderbufferTypes =

    type AdaptiveRenderbuffer(runtime : ITextureRuntime, format : aval<RenderbufferFormat>, samples : aval<int>, size : aval<V2i>) =
        inherit AdaptiveResource<IRenderbuffer>()

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
            let format = format.GetValue(token)
            let samples = samples.GetValue(token)

            match handle with
            | Some h when h.Size = size && h.Format = format && h.Samples = samples ->
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
    type AbstractAdaptiveFramebufferOutput(resource : IAdaptiveResource) =
        inherit AdaptiveResource<IFramebufferOutput>()

        override x.Create() = resource.Acquire()
        override x.Destroy() = resource.Release()

    type AdaptiveTextureAttachment<'a when 'a :> ITexture>(texture : IAdaptiveResource<'a>, slice : aval<int>, level : aval<int>) =
        inherit AbstractAdaptiveFramebufferOutput(texture)
        override x.Compute(token : AdaptiveToken, t : RenderToken) =
            let tex = texture.GetValue(token, t)
            let slice = slice.GetValue token
            let level = level.GetValue token
            { texture = unbox tex; level = level; slice = slice } :> IFramebufferOutput

    type AdaptiveRenderbufferAttachment<'a when 'a :> IRenderbuffer>(renderbuffer : IAdaptiveResource<'a>) =
        inherit AbstractAdaptiveFramebufferOutput(renderbuffer)
        override x.Compute(token : AdaptiveToken, t : RenderToken) =
            let rb = renderbuffer.GetValue(token, t)
            rb :> IFramebufferOutput


[<AbstractClass; Sealed; Extension>]
type ITextureRuntimeAdaptiveRenderbufferExtensions private() =

    // CreateRenderbuffer
    [<Extension>]
    static member CreateRenderbuffer(this : ITextureRuntime, format : aval<RenderbufferFormat>, samples : aval<int>, size : aval<V2i>) =
        AdaptiveRenderbuffer(this, format, samples, size) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateRenderbuffer(this : ITextureRuntime, format : RenderbufferFormat, samples : aval<int>, size : aval<V2i>) =
        AdaptiveRenderbuffer(this, ~~format, samples, size) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateRenderbuffer(this : ITextureRuntime, format : RenderbufferFormat, samples : int, size : aval<V2i>) =
        AdaptiveRenderbuffer(this, ~~format, ~~samples, size) :> IAdaptiveResource<_>

    // Attachments
    [<Extension>]
    static member CreateTextureAttachment(_ : ITextureRuntime, texture : IAdaptiveResource<#ITexture>, slice : aval<int>, level : aval<int>) =
        AdaptiveTextureAttachment(texture, slice, level) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTextureAttachment(_ : ITextureRuntime, texture : IAdaptiveResource<#ITexture>, slice : aval<int>) =
        AdaptiveTextureAttachment(texture, slice, ~~0) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTextureAttachment(_ : ITextureRuntime, texture : IAdaptiveResource<#ITexture>, slice : int, level : int) =
        AdaptiveTextureAttachment(texture, ~~slice, ~~level) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTextureAttachment(_ : ITextureRuntime, texture : IAdaptiveResource<#ITexture>, slice : int) =
        AdaptiveTextureAttachment(texture, ~~slice, ~~0) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateTextureAttachment(_ : ITextureRuntime, texture : IAdaptiveResource<#ITexture>) =
        AdaptiveTextureAttachment(texture, ~~(-1), ~~0) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateRenderbufferAttachment(_ : ITextureRuntime, renderbuffer : IAdaptiveResource<#IRenderbuffer>) =
        AdaptiveRenderbufferAttachment(renderbuffer) :> IAdaptiveResource<_>