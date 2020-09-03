namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices

[<AbstractClass; Sealed; Extension>]
type IRuntimeExtensions private() =

    // Overloads without stencil and depth

    /// Clears the given color attachments to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : aval<Map<Symbol, C4f>>) =
        this.CompileClear(
            signature,
            colors,
            AVal.constant None,
            AVal.constant None
        )

    /// Clears the given color attachments to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : aval<seq<Symbol * C4f>>) =
        this.CompileClear(
            signature,
            colors |> AVal.map Map.ofSeq
        )

    /// Clears the given color attachments to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : aval<list<Symbol * C4f>>) =
        this.CompileClear(
            signature,
            colors |> AVal.map Map.ofList
        )

    /// Clears all color attachments to the specified value.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : aval<C4f>) =
        let attachments =
            signature.ColorAttachments |> Map.toList |> List.map (snd >> fst)

        this.CompileClear(
            signature,
            color |> AVal.map (fun c -> attachments |> List.map (fun sem -> sem, c) |> Map.ofList)
        )


    // Overloads with stencil

    /// Clears the given color, depth and stencil attachments to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : aval<seq<Symbol * C4f>>,
                               depth : aval<float>,
                               stencil : aval<int>) =
        this.CompileClear(
            signature,
            colors |> AVal.map Map.ofSeq,
            depth |> AVal.map Some,
            stencil |> AVal.map Some
        )

    /// Clears the given color, depth and stencil attachments to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : aval<list<Symbol * C4f>>,
                               depth : aval<float>,
                               stencil : aval<int>) =
        this.CompileClear(
            signature,
            colors |> AVal.map Map.ofList,
            depth |> AVal.map Some,
            stencil |> AVal.map Some
        )

    /// Clears all color, depth and stencil attachments to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime,
                               signature : IFramebufferSignature,
                               color : aval<C4f>,
                               depth : aval<float>,
                               stencil : aval<int>) =

        let attachments =
            signature.ColorAttachments |> Map.toList |> List.map (snd >> fst)

        this.CompileClear(
            signature,
            color |> AVal.map (fun c -> attachments |> List.map (fun sem -> sem, c) |> Map.ofList),
            depth |> AVal.map Some,
            stencil |> AVal.map Some
        )

    /// Clears the depth attachment to the specified value.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, depth : aval<float>) =
        this.CompileClear(signature, AVal.constant Map.empty, depth |> AVal.map Some, AVal.constant None)

    /// Clear the stencil attachment to the specified value.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, stencil : aval<int>) =
        this.CompileClear(signature, AVal.constant Map.empty, AVal.constant None, stencil |> AVal.map Some)


    // Overloads without stencil

    /// Clears the given color attachments, and (optionally) the depth attachment to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : aval<Map<Symbol, C4f>>, depth : aval<float option>) =
        this.CompileClear(signature, colors, depth, AVal.constant None)

    /// Clears the given color attachments, and the depth attachment to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : aval<seq<Symbol * C4f>>,
                               depth : aval<float>) =
        this.CompileClear(
            signature,
            colors |> AVal.map Map.ofSeq,
            depth |> AVal.map Some,
            AVal.constant None
        )

    /// Clears the given color attachments, and the depth attachment to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : aval<list<Symbol * C4f>>,
                               depth : aval<float>) =
        this.CompileClear(
            signature,
            colors |> AVal.map Map.ofList,
            depth |> AVal.map Some,
            AVal.constant None
        )

    /// Clears all color attachments and the depth attachment to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime,
                               signature : IFramebufferSignature,
                               color : aval<C4f>,
                               depth : aval<float>) =

        let attachments =
            signature.ColorAttachments |> Map.toList |> List.map (snd >> fst)

        this.CompileClear(
            signature,
            color |> AVal.map (fun c -> attachments |> List.map (fun sem -> sem, c) |> Map.ofList),
            depth |> AVal.map Some,
            AVal.constant None
        )