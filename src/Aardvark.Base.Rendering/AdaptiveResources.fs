namespace Aardvark.Base

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open System.Runtime.CompilerServices

[<AutoOpen>]
module private RefCountedResources =

    type IAdaptiveValue<'a> with
        member x.GetValue(c : AdaptiveToken, t : RenderToken) =
            match x with
                | :? IOutputMod<'a> as x -> x.GetValue(c, t)
                | _ -> x.GetValue(c)

    type AdaptiveTexture(runtime : ITextureRuntime, format : TextureFormat, samples : int, size : aval<V2i>) =
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

    type AdaptiveCubeTexture(runtime : ITextureRuntime, format : TextureFormat, samples : int, size : aval<int>) =
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

    type AdaptiveRenderbuffer(runtime : ITextureRuntime, format : RenderbufferFormat, samples : int, size : aval<V2i>) =
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
    type AbstractAdaptiveFramebufferOutput(resource : IOutputMod) =
        inherit AbstractOutputMod<IFramebufferOutput>()

        override x.Create() = resource.Acquire()
        override x.Destroy() = resource.Release()

    type AdaptiveTextureAttachment(texture : IOutputMod<ITexture>, slice : int) =
        inherit AbstractAdaptiveFramebufferOutput(texture)
        override x.Compute(token : AdaptiveToken, t : RenderToken) =
            let tex = texture.GetValue(token, t)
            { texture = unbox tex; slice = slice; level = 0 } :> IFramebufferOutput

    type AdaptiveRenderbufferAttachment(renderbuffer : IOutputMod<IRenderbuffer>) =
        inherit AbstractAdaptiveFramebufferOutput(renderbuffer)
        override x.Compute(token : AdaptiveToken, t : RenderToken) =
            let rb = renderbuffer.GetValue(token, t)
            rb :> IFramebufferOutput

    type ITextureRuntime with
        member x.CreateTexture(format : TextureFormat, samples : int, size : aval<V2i>) =
            AdaptiveTexture(x, format, samples, size) :> IOutputMod<ITexture>

        member x.CreateTextureCube(format : TextureFormat, samples : int, size : aval<int>) =
            AdaptiveCubeTexture(x, format, samples, size) :> IOutputMod<ITexture>

        member x.CreateRenderbuffer(format : RenderbufferFormat, samples : int, size : aval<V2i>) =
            AdaptiveRenderbuffer(x, format, samples, size) :> IOutputMod<IRenderbuffer>

        member x.CreateTextureAttachment(texture : IOutputMod<ITexture>, slice : int) =
            AdaptiveTextureAttachment(texture, slice) :> IOutputMod<_>

        member x.CreateRenderbufferAttachment(renderbuffer : IOutputMod<IRenderbuffer>) =
            AdaptiveRenderbufferAttachment(renderbuffer) :> IOutputMod<_>



    type AdaptiveFramebuffer(runtime : IFramebufferRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : aval<V2i>) =
        inherit AbstractOutputMod<IFramebuffer>()

        let createAttachment (sem : Symbol) (att : AttachmentSignature) =
            let isTexture = Set.contains sem textures
            if isTexture then
                let tex = runtime.CreateTexture(unbox (int att.format), att.samples, size)
                runtime.CreateTextureAttachment(tex, 0)
            else
                let rb = runtime.CreateRenderbuffer(att.format, att.samples, size)
                runtime.CreateRenderbufferAttachment(rb)

        let attachments = SymDict.empty
        let mutable handle : Option<IFramebuffer> = None

        do
            match signature.DepthAttachment with
                | Some d ->
                    attachments.[DefaultSemantic.Depth] <- createAttachment DefaultSemantic.Depth d
                | None ->
                    ()

            for (index, (sem, att)) in Map.toSeq signature.ColorAttachments do
                let a = createAttachment sem att
                attachments.[sem] <- a

        override x.Create() =
            for att in attachments.Values do att.Acquire()

        override x.Destroy() =
            for att in attachments.Values do att.Release()
            match handle with
                | Some h ->
                    runtime.DeleteFramebuffer(h)
                    handle <- None
                | None -> ()
        override x.Compute(token : AdaptiveToken, t : RenderToken) =
            let att =
                attachments
                    |> SymDict.toMap
                    |> Map.map (fun sem att -> att.GetValue(token, t))

            match handle with
                | Some h ->
                    runtime.DeleteFramebuffer(h)
                    t.ReplacedResource(ResourceKind.Framebuffer)
                | None ->
                    t.CreatedResource(ResourceKind.Framebuffer)

            let fbo = runtime.CreateFramebuffer(signature, att)
            handle <- Some fbo
            fbo

    type AdaptiveFramebufferCube(runtime : IFramebufferRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : aval<int>) =
        inherit AbstractOutputMod<IFramebuffer[]>()

        let store = SymDict.empty

        let createAttachment (sem : Symbol) (face : CubeSide) (att : AttachmentSignature) =
            let isTexture = Set.contains sem textures
            if isTexture then

                let tex =
                    store.GetOrCreate(sem, fun sem ->
                        runtime.CreateTextureCube(unbox (int att.format), att.samples, size) :> IOutputMod
                    ) |> unbox<IOutputMod<ITexture>>

                runtime.CreateTextureAttachment(tex, int face)
            else
                let rb =
                    store.GetOrCreate(sem, fun sem ->
                        runtime.CreateRenderbuffer(att.format, att.samples, size |> AVal.map(fun x -> V2i(x))) :> IOutputMod
                    ) |> unbox<IOutputMod<IRenderbuffer>>

                runtime.CreateRenderbufferAttachment(rb)

        let mutable handle : Option<IFramebuffer>[] = Array.zeroCreate 6

        let attachments =
            Array.init 6 (fun face ->
                let face = unbox<CubeSide> face
                let attachments = SymDict.empty
                match signature.DepthAttachment with
                    | Some d ->
                        attachments.[DefaultSemantic.Depth] <- createAttachment DefaultSemantic.Depth face d
                    | None ->
                        ()

                for (index, (sem, att)) in Map.toSeq signature.ColorAttachments do
                    let a = createAttachment sem face att
                    attachments.[sem] <- a

                attachments
            )

        override x.Create() =
            for face in 0 .. 5 do
                for att in attachments.[face].Values do att.Acquire()

        override x.Destroy() =
            for face in 0 .. 5 do
                for att in attachments.[face].Values do att.Release()
                match handle.[face] with
                    | Some h ->
                        runtime.DeleteFramebuffer(h)
                        handle.[face] <- None
                    | None -> ()

        override x.Compute(token : AdaptiveToken, t : RenderToken) =
            attachments |> Array.mapi (fun i attachments ->
                let att =
                    attachments
                        |> SymDict.toMap
                        |> Map.map (fun sem att -> att.GetValue(token, t))


                match handle.[i] with
                    | Some h ->
                        runtime.DeleteFramebuffer(h)
                        t.ReplacedResource(ResourceKind.Framebuffer)
                    | None ->
                        t.CreatedResource(ResourceKind.Framebuffer)

                let fbo = runtime.CreateFramebuffer(signature, att)
                handle.[i] <- Some fbo
                fbo
            )

    type AdaptiveRenderingResult(task : IRenderTask, target : IOutputMod<IFramebuffer>) =
        inherit AbstractOutputMod<IFramebuffer>()

        override x.Compute(token : AdaptiveToken, t : RenderToken) =
            let fbo = target.GetValue(token, t)
            task.Run(token, t, OutputDescription.ofFramebuffer fbo)
            fbo

        override x.Create() =
            Log.line "result created"
            target.Acquire()

        override x.Destroy() =
            Log.line "result deleted"
            target.Release()

    type AdaptiveOutputTexture(semantic : Symbol, res : IOutputMod<IFramebuffer>) =
        inherit AbstractOutputMod<ITexture>()

        override x.Compute(token : AdaptiveToken, t : RenderToken) =
            let res = res.GetValue(token, t)

            match Map.tryFind semantic res.Attachments with
                | Some (:? IBackendTextureOutputView as t) ->
                    t.texture :> ITexture
                | _ ->
                    failwithf "could not get result for semantic %A as texture" semantic

        override x.Create() =
            Log.line "texture created"
            res.Acquire()

        override x.Destroy() =
            Log.line "texture deleted"
            res.Release()

[<AbstractClass; Sealed; Extension>]
type RuntimeFramebufferExtensions private() =

    [<Extension>]
    static member CreateFramebuffer (this : IFramebufferRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : aval<V2i>) : IOutputMod<IFramebuffer> =
        AdaptiveFramebuffer(this, signature, textures, size) :> IOutputMod<IFramebuffer>

    [<Extension>]
    static member CreateFramebufferCube (this : IFramebufferRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : aval<int>) : IOutputMod<IFramebuffer[]> =
        AdaptiveFramebufferCube(this, signature, textures, size) :> IOutputMod<IFramebuffer[]>

    [<Extension>]
    static member CreateFramebuffer (this : IFramebufferRuntime, signature : IFramebufferSignature, size : aval<V2i>) : IOutputMod<IFramebuffer> =
        let sems =
            Set.ofList [
                yield! signature.ColorAttachments |> Map.toSeq |> Seq.map snd |> Seq.map fst
                if Option.isSome signature.DepthAttachment then yield DefaultSemantic.Depth
                if Option.isSome signature.StencilAttachment then yield DefaultSemantic.Stencil
            ]

        AdaptiveFramebuffer(this, signature, sems, size) :> IOutputMod<IFramebuffer>

    [<Extension>]
    static member RenderTo(this : IRenderTask, output : IOutputMod<IFramebuffer>) =
        AdaptiveRenderingResult(this, output) :> IOutputMod<_>

    [<Extension>]
    static member GetOutputTexture (this : IOutputMod<IFramebuffer>, semantic : Symbol) =
        AdaptiveOutputTexture(semantic, this) :> IOutputMod<_>