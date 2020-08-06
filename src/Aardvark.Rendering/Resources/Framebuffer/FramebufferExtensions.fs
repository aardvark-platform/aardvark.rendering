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


[<AbstractClass; Sealed; Extension>]
type IFramebufferSignatureExtensions private() =
    [<Extension>]
    static member CreateFramebuffer (this : IFramebufferSignature, attachments : Map<Symbol, IFramebufferOutput>) =
        this.Runtime.CreateFramebuffer(this, attachments)

    [<Extension>]
    static member CreateFramebuffer (this : IFramebufferSignature, attachments : seq<Symbol * IFramebufferOutput>) =
        this.Runtime.CreateFramebuffer(this, attachments)