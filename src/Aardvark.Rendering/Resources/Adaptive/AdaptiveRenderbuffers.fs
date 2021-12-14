namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators

open System.Runtime.CompilerServices
open System.Runtime.InteropServices

[<AutoOpen>]
module private AdaptiveRenderbufferTypes =

    type AdaptiveRenderbuffer(runtime : ITextureRuntime, format : aval<TextureFormat>, samples : aval<int>, size : aval<V2i>) =
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
            let tex = unbox<IBackendTexture> <| texture.GetValue(token, t)
            let slice = slice.GetValue token
            let level = level.GetValue token
            tex.GetOutputView(level, slice)

    type AdaptiveRenderbufferAttachment<'a when 'a :> IRenderbuffer>(renderbuffer : IAdaptiveResource<'a>) =
        inherit AbstractAdaptiveFramebufferOutput(renderbuffer)
        override x.Compute(token : AdaptiveToken, t : RenderToken) =
            let rb = renderbuffer.GetValue(token, t)
            rb :> IFramebufferOutput


[<AbstractClass; Sealed; Extension>]
type ITextureRuntimeAdaptiveRenderbufferExtensions private() =

    ///<summary>Creates an adaptive renderbuffer.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the renderbuffer.</param>
    ///<param name="format">The desired renderbuffer format.</param>
    ///<param name="samples">The number of samples.</param>
    [<Extension>]
    static member CreateRenderbuffer(this : ITextureRuntime, size : aval<V2i>, format : TextureFormat, samples : aval<int>) =
        AdaptiveRenderbuffer(this, ~~format, samples, size) :> IAdaptiveResource<_>

    ///<summary>Creates an adaptive renderbuffer.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the renderbuffer.</param>
    ///<param name="format">The desired renderbuffer format.</param>
    ///<param name="samples">The number of samples. Default is 1.</param>
    [<Extension>]
    static member CreateRenderbuffer(this : ITextureRuntime, size : aval<V2i>, format : TextureFormat,
                                     [<Optional; DefaultParameterValue(1)>] samples : int) =
        AdaptiveRenderbuffer(this, ~~format, ~~samples, size) :> IAdaptiveResource<_>


    ///<summary>Creates a framebuffer attachment from the given adaptive texture.</summary>
    ///<param name="texture">The input texture.</param>
    ///<param name="slice">The slice of the texture to use as output. If negative, all slices are used.</param>
    ///<param name="level">The mip level of the texture to use as output.</param>
    [<Extension>]
    static member CreateTextureAttachment(_ : ITextureRuntime, texture : IAdaptiveResource<#ITexture>, slice : aval<int>, level : aval<int>) =
        AdaptiveTextureAttachment(texture, slice, level) :> IAdaptiveResource<_>

    ///<summary>Creates a framebuffer attachment from the given adaptive texture.
    /// If the input texture is mipmapped, the first level is used.</summary>
    ///<param name="texture">The input texture.</param>
    ///<param name="slice">The slice of the texture to use as output. If negative, all slices are used.</param>
    [<Extension>]
    static member CreateTextureAttachment(_ : ITextureRuntime, texture : IAdaptiveResource<#ITexture>, slice : aval<int>) =
        AdaptiveTextureAttachment(texture, slice, ~~0) :> IAdaptiveResource<_>

    ///<summary>Creates a framebuffer attachment from the given adaptive texture.</summary>
    ///<param name="texture">The input texture.</param>
    ///<param name="slice">The slice of the texture to use as output. If negative, all slices are used. Default is -1.</param>
    ///<param name="level">The mip level of the texture to use as output. Default is 0.</param>
    [<Extension>]
    static member CreateTextureAttachment(_ : ITextureRuntime, texture : IAdaptiveResource<#ITexture>,
                                          [<Optional; DefaultParameterValue(-1)>] slice : int,
                                          [<Optional; DefaultParameterValue(0)>] level : int) =
        AdaptiveTextureAttachment(texture, ~~slice, ~~level) :> IAdaptiveResource<_>

    ///<summary>Creates a framebuffer attachment from the given adaptive renderbuffer.</summary>
    ///<param name="renderbuffer">The input renderbuffer.</param>
    [<Extension>]
    static member CreateRenderbufferAttachment(_ : ITextureRuntime, renderbuffer : IAdaptiveResource<#IRenderbuffer>) =
        AdaptiveRenderbufferAttachment(renderbuffer) :> IAdaptiveResource<_>