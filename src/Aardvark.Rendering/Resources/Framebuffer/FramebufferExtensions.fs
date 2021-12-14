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
    static member CreateFramebufferSignature(this : IFramebufferRuntime, samples : int, attachments : Map<Symbol, TextureFormat>, layers : int, perLayerUniforms : Set<string>) =
        this.CreateFramebufferSignature(
            attachments |> Map.map (fun _ f -> { format = f; samples = samples }),
            layers, perLayerUniforms
        )

    /// Creates a framebuffer signature with the given attachment formats and number of samples.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, samples : int, attachments : seq<Symbol * TextureFormat>, layers : int, perLayerUniforms : Set<string>) =
        this.CreateFramebufferSignature(samples, Map.ofSeq attachments, layers, perLayerUniforms)

    /// Creates a framebuffer signature with the given attachment formats and number of samples.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, attachments : Map<Symbol, TextureFormat>, layers : int, perLayerUniforms : Set<string>) =
        this.CreateFramebufferSignature(
            attachments |> Map.map (fun _ f -> { format = f; samples = 1 }),
            layers, perLayerUniforms
        )

    /// Creates a framebuffer signature with the given attachment formats and number of samples.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, attachments : seq<Symbol * TextureFormat>, layers : int, perLayerUniforms : Set<string>) =
        this.CreateFramebufferSignature(Map.ofSeq attachments, layers, perLayerUniforms)

    /// Creates a framebuffer signature with the given attachment formats and number of samples.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, samples : int, attachments : Map<Symbol, TextureFormat>) =
        this.CreateFramebufferSignature(samples, attachments, 1, Set.empty)

    /// Creates a framebuffer signature with the given attachment formats and number of samples.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, samples : int, attachments : seq<Symbol * TextureFormat>) =
        this.CreateFramebufferSignature(samples, attachments, 1, Set.empty)

    /// Creates a framebuffer signature with the given attachment formats and number of samples.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, attachments : Map<Symbol, TextureFormat>) =
        this.CreateFramebufferSignature(attachments, 1, Set.empty)

    /// Creates a framebuffer signature with the given attachment formats and number of samples.
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, attachments : seq<Symbol * TextureFormat>) =
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

    /// Clears the framebuffer with the given color.
    [<Extension>]
    static member inline Clear(this : IFramebufferRuntime, fbo : IFramebuffer, color : ^Color) =
        let values = color |> (fun c -> clear { color c })
        this.Clear(fbo, values)

    /// Clears the framebuffer with the given color and depth.
    [<Extension>]
    static member inline Clear(this : IFramebufferRuntime, fbo : IFramebuffer, color : ^Color, depth : ^Depth) =
        let values = (color, depth) ||> (fun c d -> clear { color c; depth d })
        this.Clear(fbo, values)

    /// Clears the framebuffer with the given color, depth and stencil values.
    [<Extension>]
    static member inline Clear(this : IFramebufferRuntime, fbo : IFramebuffer, color : ^Color, depth : ^Depth, stencil : ^Stencil) =
        let values = (color, depth, stencil) |||> (fun c d s -> clear { color c; depth d; stencil s })
        this.Clear(fbo, values)

    /// Clears the framebuffer with the given depth value.
    [<Extension>]
    static member inline ClearDepth(this : IFramebufferRuntime, fbo : IFramebuffer, depth : ^Depth) =
        let values = depth |> (fun d -> clear { depth d })
        this.Clear(fbo, values)

    /// Clears the framebuffer with the given stencil value.
    [<Extension>]
    static member inline ClearStencil(this : IFramebufferRuntime, fbo : IFramebuffer, stencil : ^Stencil) =
        let values = stencil |> (fun s -> clear { stencil s })
        this.Clear(fbo, values)

    /// Clears the framebuffer with the given depth and stencil values.
    [<Extension>]
    static member inline ClearDepthStencil(this : IFramebufferRuntime, fbo : IFramebuffer, depth : ^Depth, stencil : ^Stencil) =
        let values = (depth, stencil) ||> (fun d s -> clear { depth d; stencil s })
        this.Clear(fbo, values)


[<AutoOpen>]
module IFramebufferSignatureExtensions =

    let private signatureAssignableFrom (mine : AttachmentSignature) (other : AttachmentSignature) =
        TextureFormat.toColFormat mine.format = TextureFormat.toColFormat other.format

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