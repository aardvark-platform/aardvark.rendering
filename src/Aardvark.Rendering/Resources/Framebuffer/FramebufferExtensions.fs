namespace Aardvark.Rendering

open Aardvark.Base
open System.Runtime.CompilerServices

[<AbstractClass; Sealed; Extension>]
type IFramebufferRuntimeExtensions private() =

    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, l : SymbolDict<AttachmentSignature>) =
        this.CreateFramebufferSignature(l, Set.empty, 1, Set.empty)

    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, l : seq<Symbol * AttachmentSignature>) =
        this.CreateFramebufferSignature(SymDict.ofSeq l, Set.empty, 1, Set.empty)

    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, l : list<Symbol * AttachmentSignature>) =
        this.CreateFramebufferSignature(SymDict.ofList l, Set.empty, 1, Set.empty)

    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, l : Map<Symbol, AttachmentSignature>) =
        this.CreateFramebufferSignature(SymDict.ofMap l, Set.empty, 1, Set.empty)

    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, samples : int, l : seq<Symbol * RenderbufferFormat>) =
        this.CreateFramebufferSignature(
            l |> Seq.map (fun (s,f) -> s, { format = f; samples = samples }) |> SymDict.ofSeq,
            Set.empty,
            1, Set.empty
        )

    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime, l : seq<Symbol * RenderbufferFormat>) =
        this.CreateFramebufferSignature(
            l |> Seq.map (fun (s,f) -> s, { format = f; samples = 1 }) |> SymDict.ofSeq,
            Set.empty,
            1, Set.empty
        )

    [<Extension>]
    static member CreateFramebuffer(this : IFramebufferRuntime, signature : IFramebufferSignature, attachments : seq<Symbol * IFramebufferOutput>) =
        this.CreateFramebuffer(
            signature,
            Map.ofSeq attachments
        )

    /// <summary>
    /// Clear all color attachments, depth and stencil of a framebuffer object (each optional).
    /// </summary>
    [<Extension>]
    static member Clear(this : IFramebufferRuntime, fbo : IFramebuffer, color : Option<C4f>, depth : Option<float>, stencil : Option<int>) =
        let clearColors =
            match color with
            | Some c ->
                fbo.Signature.ColorAttachments |> Seq.map (fun x-> (fst x.Value, c)) |> Map.ofSeq
            | None -> Map.empty
        this.Clear(fbo, clearColors, depth, stencil)

    /// <summary>
    /// Clear a specific color attachment of a framebuffer object with the given color.
    /// </summary>
    [<Extension>]
    static member Clear(this : IFramebufferRuntime, fbo : IFramebuffer, name : Symbol, color : C4f) =
        let clearColors = Map.ofSeq [(name, color) ]
        this.Clear(fbo, clearColors, None, None)


[<AbstractClass; Sealed; Extension>]
type IFramebufferSignatureExtensions private() =
    [<Extension>]
    static member CreateFramebuffer (this : IFramebufferSignature, attachments : Map<Symbol, IFramebufferOutput>) =
        this.Runtime.CreateFramebuffer(this, attachments)

    [<Extension>]
    static member CreateFramebuffer (this : IFramebufferSignature, attachments : seq<Symbol * IFramebufferOutput>) =
        this.Runtime.CreateFramebuffer(this, attachments)