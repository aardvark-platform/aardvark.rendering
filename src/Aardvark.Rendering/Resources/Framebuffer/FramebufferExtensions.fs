namespace Aardvark.Rendering

open Aardvark.Base
open System.Runtime.CompilerServices

[<AbstractClass; Sealed; Extension>]
type IFramebufferRuntimeExtensions private() =

    // ================================================================================================================
    // CreateFramebufferSignature
    // ================================================================================================================

    /// Creates a framebuffer signature with the given attachment signatures.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, attachments : SymbolDict<AttachmentSignature>, layers : int, perLayerUniforms : Set<string>) =
        this.CreateFramebufferSignature(SymDict.toMap attachments, layers, perLayerUniforms)

    /// Creates a framebuffer signature with the given attachment signatures.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, attachments : seq<Symbol * AttachmentSignature>, layers : int, perLayerUniforms : Set<string>) =
        this.CreateFramebufferSignature(Map.ofSeq attachments, layers, perLayerUniforms)

    /// Creates a framebuffer signature with the given attachment signatures.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, attachments : Map<Symbol, AttachmentSignature>) =
        this.CreateFramebufferSignature(attachments, 1, Set.empty)

    /// Creates a framebuffer signature with the given attachment signatures.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, attachments : SymbolDict<AttachmentSignature>) =
        this.CreateFramebufferSignature(SymDict.toMap attachments)

    /// Creates a framebuffer signature with the given attachment signatures.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, attachments : seq<Symbol * AttachmentSignature>) =
        this.CreateFramebufferSignature(Map.ofSeq attachments)

    /// Creates a framebuffer signature with the given attachment formats and number of samples.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, samples : int, attachments : Map<Symbol, RenderbufferFormat>, layers : int, perLayerUniforms : Set<string>) =
        this.CreateFramebufferSignature(
            attachments |> Map.map (fun _ f -> { format = f; samples = samples }),
            layers, perLayerUniforms
        )

    /// Creates a framebuffer signature with the given attachment formats and number of samples.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, samples : int, attachments : seq<Symbol * RenderbufferFormat>, layers : int, perLayerUniforms : Set<string>) =
        this.CreateFramebufferSignature(samples, Map.ofSeq attachments, layers, perLayerUniforms)

    /// Creates a framebuffer signature with the given attachment formats and number of samples.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, attachments : Map<Symbol, RenderbufferFormat>, layers : int, perLayerUniforms : Set<string>) =
        this.CreateFramebufferSignature(
            attachments |> Map.map (fun _ f -> { format = f; samples = 1 }),
            layers, perLayerUniforms
        )

    /// Creates a framebuffer signature with the given attachment formats and number of samples.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, attachments : seq<Symbol * RenderbufferFormat>, layers : int, perLayerUniforms : Set<string>) =
        this.CreateFramebufferSignature(Map.ofSeq attachments, layers, perLayerUniforms)

    /// Creates a framebuffer signature with the given attachment formats and number of samples.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, samples : int, attachments : Map<Symbol, RenderbufferFormat>) =
        this.CreateFramebufferSignature(samples, attachments, 1, Set.empty)

    /// Creates a framebuffer signature with the given attachment formats and number of samples.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, samples : int, attachments : seq<Symbol * RenderbufferFormat>) =
        this.CreateFramebufferSignature(samples, attachments, 1, Set.empty)

    /// Creates a framebuffer signature with the given attachment formats and number of samples.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, attachments : Map<Symbol, RenderbufferFormat>) =
        this.CreateFramebufferSignature(attachments, 1, Set.empty)

    /// Creates a framebuffer signature with the given attachment formats and number of samples.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, attachments : seq<Symbol * RenderbufferFormat>) =
        this.CreateFramebufferSignature(attachments, 1, Set.empty)


    // ================================================================================================================
    // CreateFramebuffer
    // ================================================================================================================

    /// Creates a framebuffer of the given signature and with the given attachments.
    [<Extension>]
    static member CreateFramebuffer(this : IFramebufferRuntime, signature : IFramebufferSignature, attachments : seq<Symbol * IFramebufferOutput>) =
        this.CreateFramebuffer(
            signature,
            Map.ofSeq attachments
        )


    // ================================================================================================================
    // Clear
    // ================================================================================================================

    /// Clear all color attachments, depth and stencil of a framebuffer object (each optional).
    [<Extension>]
    static member Clear(this : IFramebufferRuntime, fbo : IFramebuffer, color : Option<C4f>, depth : Option<float>, stencil : Option<int>) =
        let clearColors =
            match color with
            | Some c ->
                fbo.Signature.ColorAttachments |> Seq.map (fun x-> (fst x.Value, c)) |> Map.ofSeq
            | None -> Map.empty
        this.Clear(fbo, clearColors, depth, stencil)

    /// Clear a specific color attachment of a framebuffer object with the given color.
    [<Extension>]
    static member Clear(this : IFramebufferRuntime, fbo : IFramebuffer, name : Symbol, color : C4f) =
        let clearColors = Map.ofSeq [(name, color) ]
        this.Clear(fbo, clearColors, None, None)


[<AutoOpen>]
module IFramebufferSignatureExtensions =

    let private signatureAssignableFrom (mine : AttachmentSignature) (other : AttachmentSignature) =
        RenderbufferFormat.toColFormat mine.format = RenderbufferFormat.toColFormat other.format

    let private colorsAssignableFrom (mine : Map<int, Symbol * AttachmentSignature>) (other : Map<int, Symbol * AttachmentSignature>) =
        mine |> Map.forall (fun id (sem, signature) ->
            match Map.tryFind id other with
            | Some (otherSem, otherSig) when sem = otherSem ->
                signatureAssignableFrom signature otherSig
            | None -> true
            | _ -> false
        )

    let private depthAssignableFrom (mine : Option<AttachmentSignature>) (other : Option<AttachmentSignature>) =
        match mine, other with
        | Some mine, Some other -> signatureAssignableFrom mine other
        | _ -> true

    type IFramebufferSignature with

        /// Gets the framebuffer layout described by the signature.
        member x.Layout =
            {
                ColorAttachments = x.ColorAttachments
                DepthAttachment = x.DepthAttachment
                StencilAttachment = x.StencilAttachment
                LayerCount = x.LayerCount
                PerLayerUniforms = x.PerLayerUniforms
            }

    [<AbstractClass; Sealed; Extension>]
    type IFramebufferSignatureExtensions private() =

        /// Checks if the signature is assignable from the other signature (i.e. it is a subset of the other signature).
        [<Extension>]
        static member IsAssignableFrom (this : IFramebufferSignature, other : IFramebufferSignature) =
            if LanguagePrimitives.PhysicalEquality this other then
                true
            else
                this.LayerCount = other.LayerCount &&
                this.PerLayerUniforms = other.PerLayerUniforms &&
                colorsAssignableFrom this.ColorAttachments other.ColorAttachments
                // TODO: check depth and stencil (cumbersome for combined DepthStencil attachments)

        /// Creates a framebuffer of the given signature and with the given attachments.
        [<Extension>]
        static member CreateFramebuffer (this : IFramebufferSignature, attachments : Map<Symbol, IFramebufferOutput>) =
            this.Runtime.CreateFramebuffer(this, attachments)

        /// Creates a framebuffer of the given signature and with the given attachments.
        [<Extension>]
        static member CreateFramebuffer (this : IFramebufferSignature, attachments : seq<Symbol * IFramebufferOutput>) =
            this.Runtime.CreateFramebuffer(this, attachments)