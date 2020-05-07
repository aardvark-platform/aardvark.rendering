namespace Aardvark.Base

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open System.Runtime.CompilerServices

type private AdaptiveFramebuffer(runtime : IFramebufferRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : aval<V2i>) =
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

type private AdaptiveFramebufferCube(runtime : IFramebufferRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : aval<int>) =
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

[<AbstractClass; Sealed; Extension>]
type IFramebufferRuntimeAdaptiveExtensions private() =

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