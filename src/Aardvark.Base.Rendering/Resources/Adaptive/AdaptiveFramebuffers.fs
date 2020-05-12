namespace Aardvark.Base

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open System.Runtime.CompilerServices

[<AutoOpen>]
module private AdaptiveFramebufferTypes =

    type AdaptiveFramebuffer(runtime : IFramebufferRuntime, signature : IFramebufferSignature, attachments : Map<Symbol, aval<IFramebufferOutput>>) =
        inherit AbstractOutputMod<IFramebuffer>()

        let mutable handle = None

        let compare x y =
            let x = x |> Map.toList |> List.map snd
            let y = y |> Map.toList |> List.map snd
            List.forall2 (=) x y

        let create signature att =
            let fbo = runtime.CreateFramebuffer(signature, att)
            handle <- Some (fbo, att)
            fbo

        override x.Create() =
            for (KeyValue (_, att)) in attachments do att.Acquire()

        override x.Destroy() =
            for (KeyValue (_, att)) in attachments do att.Release()
            handle |> Option.iter (fun (fbo, _) -> runtime.DeleteFramebuffer fbo)
            handle <- None

        override x.Compute(token : AdaptiveToken, t : RenderToken) =
            let att =
                attachments |> Map.map (fun _ att -> att.GetValue(token, t))

            match handle with
            | Some (h, att') ->
                if compare att att' then
                    h
                else
                    t.ReplacedResource(ResourceKind.Framebuffer)
                    runtime.DeleteFramebuffer(h)
                    create signature att

            | None ->
                t.CreatedResource(ResourceKind.Framebuffer)
                create signature att

    type AdaptiveFramebufferCube(runtime : IFramebufferRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : aval<int>) =
        inherit AbstractOutputMod<IFramebuffer[]>()

        let store = SymDict.empty

        let createAttachment (sem : Symbol) (face : CubeSide) (att : AttachmentSignature) =
            let isTexture = Set.contains sem textures
            if isTexture then

                let tex =
                    store.GetOrCreate(sem, fun sem ->
                        runtime.CreateTextureCube(TextureFormat.ofRenderbufferFormat att.format, att.samples, size) :> IOutputMod
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
    static member CreateFramebuffer(this : IFramebufferRuntime,
                                    signature : IFramebufferSignature,
                                    attachments : Map<Symbol, #aval<IFramebufferOutput>>) =

        let atts = attachments |> Map.map (fun _ x -> x :> aval<_>)
        AdaptiveFramebuffer(this, signature, atts) :> IOutputMod<_>

    [<Extension>]
    static member CreateFramebuffer(this : IFramebufferRuntime,
                                    signature : IFramebufferSignature,
                                    attachments : seq<Symbol * #aval<IFramebufferOutput>>) =

        this.CreateFramebuffer(signature, attachments |> Map.ofSeq)

    [<Extension>]
    static member CreateFramebuffer(this : IFramebufferRuntime,
                                    signature : IFramebufferSignature,
                                    color : Option<#aval<IFramebufferOutput>>,
                                    depth : Option<#aval<IFramebufferOutput>>,
                                    stencil : Option<#aval<IFramebufferOutput>>) =

        let inline add sem opt dict =
            opt |> Option.iter (fun x -> dict |> SymDict.add sem (x :> aval<_>))

        let atts = SymDict.empty
        atts |> add DefaultSemantic.Colors color
        atts |> add DefaultSemantic.Depth depth
        atts |> add DefaultSemantic.Stencil stencil

        this.CreateFramebuffer(signature, SymDict.toMap atts)

    [<Extension>]
    static member CreateFramebuffer (this : IFramebufferRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : aval<V2i>) =

        let inline createAttachment (sem : Symbol) (att : AttachmentSignature) =
            if textures |> Set.contains sem then
                let tex = this.CreateTexture(TextureFormat.ofRenderbufferFormat att.format, att.samples, size)
                this.CreateTextureAttachment(tex, 0) :> aval<_>
            else
                let rb = this.CreateRenderbuffer(att.format, att.samples, size)
                this.CreateRenderbufferAttachment(rb) :> aval<_>

        let atts = SymDict.empty

        signature.DepthAttachment
        |> Option.iter (fun d ->
            atts.[DefaultSemantic.Depth] <- createAttachment DefaultSemantic.Depth d
        )

        for (_, (sem, att)) in Map.toSeq signature.ColorAttachments do
            atts.[sem] <- createAttachment sem att

        this.CreateFramebuffer(signature, SymDict.toMap atts)

    [<Extension>]
    static member CreateFramebuffer (this : IFramebufferRuntime, signature : IFramebufferSignature, size : aval<V2i>) : IOutputMod<IFramebuffer> =
        let sems =
            Set.ofList [
                yield! signature.ColorAttachments |> Map.toSeq |> Seq.map snd |> Seq.map fst
                if Option.isSome signature.DepthAttachment then yield DefaultSemantic.Depth
                if Option.isSome signature.StencilAttachment then yield DefaultSemantic.Stencil
            ]

        this.CreateFramebuffer(signature, sems, size)

    [<Extension>]
    static member CreateFramebufferCube (this : IFramebufferRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : aval<int>) =
        AdaptiveFramebufferCube(this, signature, textures, size) :> IOutputMod<_>