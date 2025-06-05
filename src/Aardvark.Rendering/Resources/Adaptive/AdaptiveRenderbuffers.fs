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

    type AdaptiveTextureAttachment<'T when 'T :> IBackendTexture>(texture : aval<'T>, aspect : aval<TextureAspect>, level : aval<int>, slice : aval<int>) =
        inherit AdaptiveResource<IFramebufferOutput>()

        let format = texture |> AVal.mapNonAdaptive _.Format
        let samples = texture |> AVal.mapNonAdaptive _.Samples
        let size = texture |> AVal.mapNonAdaptive _.Size.XY

        override x.Create() = texture.Acquire()
        override x.Destroy() = texture.Release()

        override x.Compute(token : AdaptiveToken, t : RenderToken) =
            let tex = texture.GetValue(token, t)
            let aspect = aspect.GetValue token
            let level = level.GetValue token
            let slice = slice.GetValue token
            tex.GetOutputView(aspect, level, slice)

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

    ///<summary>
    /// Creates an output view of the texture with the given level and slice.
    /// In case the texture is an array or a cube and slice is negative, all items or faces are selected as texture layers.
    ///</summary>
    ///<param name="texture">The texture of the output view.</param>
    ///<param name="aspect">The aspect of the texture.</param>
    ///<param name="level">The level for the output view.</param>
    ///<param name="slice">The slice for the output view or -1 for all slices.</param>
    [<Extension>]
    static member GetOutputView(texture : aval<#IBackendTexture>, aspect : aval<TextureAspect>, level : aval<int>, slice : aval<int>) =
        AdaptiveTextureAttachment(texture, aspect, level, slice) :> IAdaptiveFramebufferOutput

    ///<summary>
    /// Creates an output view of the texture with the given level and slice.
    /// In case the texture is an array or a cube and slice is negative, all items or faces are selected as texture layers.
    ///</summary>
    ///<param name="texture">The texture of the output view.</param>
    ///<param name="aspect">The aspect of the texture.</param>
    ///<param name="level">The level for the output view. Default is 0.</param>
    ///<param name="slice">The slice for the output view or -1 for all slices. Default is -1.</param>
    [<Extension>]
    static member GetOutputView(texture : aval<#IBackendTexture>, aspect : TextureAspect,
                                [<Optional; DefaultParameterValue(0)>] level : int,
                                [<Optional; DefaultParameterValue(-1)>] slice : int) =
        AdaptiveTextureAttachment(texture, ~~aspect, ~~level, ~~slice) :> IAdaptiveFramebufferOutput

    ///<summary>
    /// Creates an output view of the texture with the given level and slice.
    /// In case the texture is an array or a cube and slice is negative, all items or faces are selected as texture layers.
    ///</summary>
    ///<param name="texture">The texture of the output view.</param>
    ///<param name="level">The level for the output view.</param>
    ///<param name="slice">The slice for the output view or -1 for all slices.</param>
    [<Extension>]
    static member GetOutputView(texture : aval<#IBackendTexture>, level : aval<int>, slice : aval<int>) =
        let aspect = texture |> AVal.mapNonAdaptive _.Format.Aspect
        AdaptiveTextureAttachment(texture, aspect, level, slice) :> IAdaptiveFramebufferOutput

    ///<summary>
    /// Creates an output view of the texture with the given level and slice.
    /// In case the texture is an array or a cube and slice is negative, all items or faces are selected as texture layers.
    ///</summary>
    ///<param name="texture">The texture of the output view.</param>
    ///<param name="level">The level for the output view. Default is 0.</param>
    ///<param name="slice">The slice for the output view or -1 for all slices. Default is -1.</param>
    [<Extension>]
    static member GetOutputView(texture : aval<#IBackendTexture>,
                                [<Optional; DefaultParameterValue(0)>] level : int,
                                [<Optional; DefaultParameterValue(-1)>] slice : int) =
        let aspect = texture |> AVal.mapNonAdaptive _.Format.Aspect
        AdaptiveTextureAttachment(texture, aspect, ~~level, ~~slice) :> IAdaptiveFramebufferOutput

    [<Extension; System.Obsolete("Use texture.GetOutputView() instead. Note that slice and level parameter positions are switched.")>]
    static member CreateTextureAttachment(_ : ITextureRuntime, texture : IAdaptiveResource<#IBackendTexture>, slice : aval<int>, level : aval<int>) =
        texture.GetOutputView(level, slice)

    [<Extension; System.Obsolete("Use texture.GetOutputView() instead. Note that slice and level parameter positions are switched.")>]
    static member CreateTextureAttachment(_ : ITextureRuntime, texture : IAdaptiveResource<#IBackendTexture>, slice : aval<int>) =
        texture.GetOutputView(~~0, slice)

    [<Extension; System.Obsolete("Use texture.GetOutputView() instead. Note that slice and level parameter positions are switched.")>]
    static member CreateTextureAttachment(_ : ITextureRuntime, texture : IAdaptiveResource<#IBackendTexture>,
                                          [<Optional; DefaultParameterValue(-1)>] slice : int,
                                          [<Optional; DefaultParameterValue(0)>] level : int) =
        texture.GetOutputView(level, slice)