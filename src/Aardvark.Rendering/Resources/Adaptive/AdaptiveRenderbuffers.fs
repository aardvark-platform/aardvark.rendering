namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators

open System.Runtime.CompilerServices
open System.Runtime.InteropServices

type IAdaptiveFramebufferOutput =
    inherit IAdaptiveResource<IFramebufferOutput>
    abstract member Format : aval<TextureFormat>
    abstract member Samples : aval<int>
    abstract member Size : aval<V2i>

type IAdaptiveRenderbuffer =
    inherit IAdaptiveFramebufferOutput
    inherit IAdaptiveResource<IRenderbuffer>
    abstract member Runtime : ITextureRuntime
    abstract member Name : string with get, set

[<AutoOpen>]
module private AdaptiveRenderbufferTypes =

    type AdaptiveRenderbuffer(runtime : ITextureRuntime, format : aval<TextureFormat>, samples : aval<int>, size : aval<V2i>) =
        inherit AdaptiveResource<IRenderbuffer>()

        let mutable handle : Option<IRenderbuffer> = None
        let mutable name = null

        let create size format samples =
            let rb = runtime.CreateRenderbuffer(size, format, samples)
            rb.Name <- name
            handle <- Some rb
            rb

        member x.Name
            with get() = name
            and set value =
                name <- value
                handle |> Option.iter _.set_Name(name)

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
                create size format samples

            | None ->
                t.CreatedResource(ResourceKind.Renderbuffer)
                create size format samples

        interface IAdaptiveRenderbuffer with
            member x.Runtime = runtime
            member x.Format = format
            member x.Samples = samples
            member x.Size = size
            member x.GetValue(t) = x.GetValue(t) :> IFramebufferOutput
            member x.GetValue(t, rt) = x.GetValue(t, rt) :> IFramebufferOutput
            member x.Name with get() = x.Name and set name = x.Name <- name

    type AdaptiveTextureAttachment<'T when 'T :> IBackendTexture>(texture : IAdaptiveResource<'T>, slice : aval<int>, level : aval<int>) =
        inherit AdaptiveResource<IFramebufferOutput>()

        let format = texture |> AVal.mapNonAdaptive _.Format
        let samples = texture |> AVal.mapNonAdaptive _.Samples
        let size = texture |> AVal.mapNonAdaptive _.Size.XY

        override x.Create() = texture.Acquire()
        override x.Destroy() = texture.Release()

        override x.Compute(token : AdaptiveToken, t : RenderToken) =
            let tex = texture.GetValue(token, t)
            let slice = slice.GetValue token
            let level = level.GetValue token
            tex.GetOutputView(level, slice)

        interface IAdaptiveFramebufferOutput with
            member x.Format = format
            member x.Samples = samples
            member x.Size = size


[<AbstractClass; Sealed; Extension>]
type ITextureRuntimeAdaptiveRenderbufferExtensions private() =

    ///<summary>Creates an adaptive renderbuffer.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the renderbuffer.</param>
    ///<param name="format">The desired renderbuffer format.</param>
    ///<param name="samples">The number of samples.</param>
    [<Extension>]
    static member CreateRenderbuffer(this : ITextureRuntime, size : aval<V2i>, format : TextureFormat, samples : aval<int>) =
        AdaptiveRenderbuffer(this, ~~format, samples, size) :> IAdaptiveRenderbuffer

    ///<summary>Creates an adaptive renderbuffer.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the renderbuffer.</param>
    ///<param name="format">The desired renderbuffer format.</param>
    ///<param name="samples">The number of samples. Default is 1.</param>
    [<Extension>]
    static member CreateRenderbuffer(this : ITextureRuntime, size : aval<V2i>, format : TextureFormat,
                                     [<Optional; DefaultParameterValue(1)>] samples : int) =
        AdaptiveRenderbuffer(this, ~~format, ~~samples, size) :> IAdaptiveRenderbuffer


    ///<summary>Creates a framebuffer attachment from the given adaptive texture.</summary>
    ///<param name="texture">The input texture.</param>
    ///<param name="slice">The slice of the texture to use as output. If negative, all slices are used.</param>
    ///<param name="level">The mip level of the texture to use as output.</param>
    [<Extension>]
    static member CreateTextureAttachment(_ : ITextureRuntime, texture : IAdaptiveResource<#IBackendTexture>, slice : aval<int>, level : aval<int>) =
        AdaptiveTextureAttachment(texture, slice, level) :> IAdaptiveFramebufferOutput

    ///<summary>Creates a framebuffer attachment from the given adaptive texture.
    /// If the input texture is mipmapped, the first level is used.</summary>
    ///<param name="texture">The input texture.</param>
    ///<param name="slice">The slice of the texture to use as output. If negative, all slices are used.</param>
    [<Extension>]
    static member CreateTextureAttachment(_ : ITextureRuntime, texture : IAdaptiveResource<#IBackendTexture>, slice : aval<int>) =
        AdaptiveTextureAttachment(texture, slice, ~~0) :> IAdaptiveFramebufferOutput

    ///<summary>Creates a framebuffer attachment from the given adaptive texture.</summary>
    ///<param name="texture">The input texture.</param>
    ///<param name="slice">The slice of the texture to use as output. If negative, all slices are used. Default is -1.</param>
    ///<param name="level">The mip level of the texture to use as output. Default is 0.</param>
    [<Extension>]
    static member CreateTextureAttachment(_ : ITextureRuntime, texture : IAdaptiveResource<#IBackendTexture>,
                                          [<Optional; DefaultParameterValue(-1)>] slice : int,
                                          [<Optional; DefaultParameterValue(0)>] level : int) =
        AdaptiveTextureAttachment(texture, ~~slice, ~~level) :> IAdaptiveFramebufferOutput