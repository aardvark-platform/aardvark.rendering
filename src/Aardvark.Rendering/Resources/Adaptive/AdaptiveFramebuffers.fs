namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices

[<AutoOpen>]
module private AdaptiveFramebufferTypes =

    type AdaptiveFramebuffer(runtime : IFramebufferRuntime, signature : IFramebufferSignature, attachments : Map<Symbol, aval<IFramebufferOutput>>) =
        inherit AdaptiveResource<IFramebuffer>()

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


    type AdaptiveFramebufferCube(runtime : IFramebufferRuntime, signature : IFramebufferSignature, size : aval<int>, writeOnly : Set<Symbol>) =
        inherit AdaptiveResource<IFramebuffer[]>()

        let store = SymDict.empty

        let createAttachment (sem : Symbol) (face : CubeSide) (att : AttachmentSignature) =
            if writeOnly |> Set.contains sem then
                let rb =
                    store.GetOrCreate(sem, fun _ ->
                        runtime.CreateRenderbuffer(att.format, att.samples, size |> AVal.map V2i) :> IAdaptiveResource
                    ) |> unbox<IAdaptiveResource<IRenderbuffer>>

                runtime.CreateRenderbufferAttachment(rb) :> aval<_>
            else
                let tex =
                    store.GetOrCreate(sem, fun _ ->
                        runtime.CreateTextureCube(TextureFormat.ofRenderbufferFormat att.format, att.samples, size) :> IAdaptiveResource
                    ) |> unbox<IAdaptiveResource<ITexture>>

                runtime.CreateTextureAttachment(tex, int face) :> aval<_>

        let mutable handle : Option<IFramebuffer>[] = Array.zeroCreate 6

        let attachments =
            Array.init 6 (fun face ->
                let face = unbox<CubeSide> face

                let atts = SymDict.empty

                signature.DepthAttachment |> Option.iter (fun d ->
                    atts.[DefaultSemantic.Depth] <- createAttachment DefaultSemantic.Depth face d
                )

                signature.StencilAttachment |> Option.iter (fun s ->
                    atts.[DefaultSemantic.Stencil] <- createAttachment DefaultSemantic.Stencil face s
                )

                for (_, (sem, att)) in Map.toSeq signature.ColorAttachments do
                    atts.[sem] <- createAttachment sem face att

                atts |> SymDict.toMap
            )

        override x.Create() =
            for face in 0 .. 5 do
                for (KeyValue(_, att)) in attachments.[face] do att.Acquire()

        override x.Destroy() =
            for face in 0 .. 5 do
                for (KeyValue(_, att)) in attachments.[face] do att.Release()
                match handle.[face] with
                | Some h ->
                    runtime.DeleteFramebuffer(h)
                    handle.[face] <- None
                | None -> ()

        override x.Compute(token : AdaptiveToken, t : RenderToken) =
            attachments |> Array.mapi (fun i attachments ->
                let att =
                    attachments |> Map.map (fun _ att -> att.GetValue(token, t))

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

    // ================================================================================================================
    // Framebuffer
    // ================================================================================================================

    /// Creates a framebuffer with the given adaptive attachments.
    [<Extension>]
    static member CreateFramebuffer(this : IFramebufferRuntime,
                                    signature : IFramebufferSignature,
                                    attachments : Map<Symbol, #aval<IFramebufferOutput>>) =

        let atts = attachments |> Map.map (fun _ x -> x :> aval<_>)
        AdaptiveFramebuffer(this, signature, atts) :> IAdaptiveResource<_>

    /// Creates a framebuffer with the given adaptive attachments.
    [<Extension>]
    static member CreateFramebuffer(this : IFramebufferRuntime,
                                    signature : IFramebufferSignature,
                                    attachments : seq<Symbol * #aval<IFramebufferOutput>>) =

        this.CreateFramebuffer(signature, attachments |> Map.ofSeq)

    /// Creates a framebuffer with the given adaptive attachments.
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

    /// Creates a framebuffer of the given signature for the given adaptive size.
    /// writeOnly indicates which attachments can be represented as render buffers instead of textures.
    [<Extension>]
    static member CreateFramebuffer (this : IFramebufferRuntime, signature : IFramebufferSignature, size : aval<V2i>, writeOnly : Set<Symbol>) =

        let inline createAttachment (sem : Symbol) (att : AttachmentSignature) =
            if writeOnly |> Set.contains sem then
                let rb = this.CreateRenderbuffer(att.format, att.samples, size)
                this.CreateRenderbufferAttachment(rb) :> aval<_>
            else
                let tex = this.CreateTexture(TextureFormat.ofRenderbufferFormat att.format, att.samples, size)
                this.CreateTextureAttachment(tex, 0) :> aval<_>

        let atts = SymDict.empty

        signature.DepthAttachment |> Option.iter (fun d ->
            atts.[DefaultSemantic.Depth] <- createAttachment DefaultSemantic.Depth d
        )

        signature.StencilAttachment |> Option.iter (fun s ->
            atts.[DefaultSemantic.Stencil] <- createAttachment DefaultSemantic.Stencil s
        )

        for (_, (sem, att)) in Map.toSeq signature.ColorAttachments do
            atts.[sem] <- createAttachment sem att

        this.CreateFramebuffer(signature, SymDict.toMap atts)

    /// Creates a framebuffer of the given signature for the given adaptive size.
    /// Textures are used for all attachments except depth and stencil.
    [<Extension>]
    static member CreateFramebuffer (this : IFramebufferRuntime, signature : IFramebufferSignature, size : aval<V2i>) : IAdaptiveResource<IFramebuffer> =
        this.CreateFramebuffer(signature, size, Set.ofList [DefaultSemantic.Depth; DefaultSemantic.Stencil])



    // ================================================================================================================
    // FramebufferCube
    // ================================================================================================================

    /// Creates a cube framebuffer of the given signature for the given adaptive size.
    /// writeOnly indicates which attachments can be represented as render buffers instead of textures.
    [<Extension>]
    static member CreateFramebufferCube(this : IFramebufferRuntime, signature : IFramebufferSignature, size : aval<int>, writeOnly : Set<Symbol>) =
        AdaptiveFramebufferCube(this, signature, size, writeOnly) :> IAdaptiveResource<_>

    /// Creates a cube framebuffer of the given signature for the given adaptive size.
    /// Textures are used for all attachments except depth and stencil.
    [<Extension>]
    static member CreateFramebufferCube(this : IFramebufferRuntime, signature : IFramebufferSignature, size : aval<int>) =
        this.CreateFramebufferCube(signature, size, Set.ofList [DefaultSemantic.Depth; DefaultSemantic.Stencil])