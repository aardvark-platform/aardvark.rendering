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


    type AdaptiveFramebufferCube(runtime : IFramebufferRuntime, signature : IFramebufferSignature, attachments : CubeMap<Map<Symbol, aval<IFramebufferOutput>>>) =
        inherit AdaptiveResource<CubeMap<IFramebuffer>>()

        let mutable handles : CubeMap<IFramebuffer * Map<Symbol, IFramebufferOutput>> = CubeMap.empty

        let inputs =
            attachments
            |> CubeMap.data
            |> Array.collect (Map.toArray >> Array.map snd)

        let compare x y =
            let x = x |> Map.toList |> List.map snd
            let y = y |> Map.toList |> List.map snd
            List.forall2 (=) x y

        let create face level signature att =
            let fbo = runtime.CreateFramebuffer(signature, att)
            handles.[face, level] <- (fbo, att)
            fbo

        override x.Create() =
            for att in inputs do att.Acquire()

        override x.Destroy() =
            for att in inputs do att.Release()

            for (fbo, _) in handles do
                runtime.DeleteFramebuffer(fbo)

            handles <- CubeMap.empty

        override x.Compute(t : AdaptiveToken, rt : RenderToken) =

            let empty =
                if handles.IsEmpty then
                    handles <- CubeMap(attachments.Levels)
                    true
                else
                    false

            attachments |> CubeMap.mapi (fun face level attachments ->
                let att =
                    attachments |> Map.map (fun _ att -> att.GetValue(t, rt))

                if empty then
                    rt.CreatedResource(ResourceKind.Framebuffer)
                    create face level signature att
                else
                    let (h, att') = handles.[face, level]
                    if compare att att' then
                        h
                    else
                        rt.ReplacedResource(ResourceKind.Framebuffer)
                        runtime.DeleteFramebuffer(h)
                        create face level signature att
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

    /// Creates a framebuffer with the given adaptive attachments.
    [<Extension>]
    static member CreateFramebufferCube(this : IFramebufferRuntime, signature : IFramebufferSignature, attachments : CubeMap<Map<Symbol, #aval<IFramebufferOutput>>>) =
        let atts = attachments |> CubeMap.map (Map.map (fun _ x -> x :> aval<_>))
        AdaptiveFramebufferCube(this, signature, atts) :> IAdaptiveResource<_>

    /// Creates a framebuffer with the given sequence of adaptive attachments.
    /// Each consecutive six elements represent a mip level, face indices within a level are determined by
    /// the CubeSide enumeration.
    [<Extension>]
    static member CreateFramebufferCube(this : IFramebufferRuntime, signature : IFramebufferSignature, attachments : Map<Symbol, #aval<IFramebufferOutput>> seq) =
        let atts = CubeMap(attachments)
        this.CreateFramebufferCube(signature, atts)

    /// Creates a cube framebuffer of the given signature for the given adaptive size and number of levels.
    /// writeOnly indicates which attachments can be represented as render buffers instead of textures.
    [<Extension>]
    static member CreateFramebufferCube(this : IFramebufferRuntime, signature : IFramebufferSignature, size : aval<int>, levels : int, writeOnly : Set<Symbol>) =
        let store = SymDict.empty

        let createAttachment (sem : Symbol) (face : CubeSide) (level : int) (att : AttachmentSignature) =
            if writeOnly |> Set.contains sem then
                let rb =
                    store.GetOrCreate(sem, fun _ ->
                        this.CreateRenderbuffer(att.format, att.samples, size |> AVal.map V2i) :> IAdaptiveResource
                    ) |> unbox<IAdaptiveResource<IRenderbuffer>>

                this.CreateRenderbufferAttachment(rb) :> aval<_>
            else
                let tex =
                    store.GetOrCreate(sem, fun _ ->
                        this.CreateTextureCube(TextureFormat.ofRenderbufferFormat att.format, levels, att.samples, size) :> IAdaptiveResource
                    ) |> unbox<IAdaptiveResource<ITexture>>

                this.CreateTextureAttachment(tex, int face, level) :> aval<_>

        let attachments =
            CubeMap.init levels (fun face level ->
                let atts = SymDict.empty

                signature.DepthAttachment |> Option.iter (fun d ->
                    atts.[DefaultSemantic.Depth] <- createAttachment DefaultSemantic.Depth face level d
                )

                signature.StencilAttachment |> Option.iter (fun s ->
                    atts.[DefaultSemantic.Stencil] <- createAttachment DefaultSemantic.Stencil face level s
                )

                for (_, (sem, att)) in Map.toSeq signature.ColorAttachments do
                    atts.[sem] <- createAttachment sem face level att

                atts |> SymDict.toMap
            )

        this.CreateFramebufferCube(signature, attachments)

    /// Creates a cube framebuffer of the given signature for the given adaptive size and number of levels.
    /// Textures are used for all attachments except depth and stencil.
    [<Extension>]
    static member CreateFramebufferCube(this : IFramebufferRuntime, signature : IFramebufferSignature, size : aval<int>, levels : int) =
        this.CreateFramebufferCube(signature, size, levels, Set.ofList [DefaultSemantic.Depth; DefaultSemantic.Stencil])

    /// Creates a cube framebuffer of the given signature for the given adaptive size.
    /// writeOnly indicates which attachments can be represented as render buffers instead of textures.
    [<Extension>]
    static member CreateFramebufferCube(this : IFramebufferRuntime, signature : IFramebufferSignature, size : aval<int>, writeOnly : Set<Symbol>) =
        this.CreateFramebufferCube(signature, size, 1, writeOnly)

    /// Creates a cube framebuffer of the given signature for the given adaptive size.
    /// Textures are used for all attachments except depth and stencil.
    [<Extension>]
    static member CreateFramebufferCube(this : IFramebufferRuntime, signature : IFramebufferSignature, size : aval<int>) =
        this.CreateFramebufferCube(signature, size, 1)