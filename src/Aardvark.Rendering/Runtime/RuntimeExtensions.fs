namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open System.Runtime.CompilerServices

[<AbstractClass; Sealed; Extension>]
type IRuntimeExtensions private() =

    // ================================================================================================================
    // CompileRender
    // ================================================================================================================

    /// Compiles a render task for the given render objects.
    [<Extension>]
    static member CompileRender(this : IRuntime, signature : IFramebufferSignature, rjs : aset<IRenderObject>) =
        this.CompileRender(signature, BackendConfiguration.Default, rjs)


    // ================================================================================================================
    // CompileClear (only color)
    // ================================================================================================================

    /// Clears the given color attachments to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : aval<Map<Symbol, C4f>>) =
        this.CompileClear(
            signature,
            colors,
            ~~None,
            ~~None
        )

    /// Clears the given color attachments to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, colors : Map<Symbol, C4f>) =
        this.CompileClear(signature, ~~colors)


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
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, colors : seq<Symbol * C4f>) =
        this.CompileClear(signature, ~~colors)


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

    /// Clears all color attachments to the specified value.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : C4f) =
        this.CompileClear(signature, ~~color)


    // ================================================================================================================
    // CompileClear (color, depth, stencil)
    // ================================================================================================================

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
                               colors : seq<Symbol * C4f>,
                               depth : float,
                               stencil : int) =
        this.CompileClear(signature, ~~colors, ~~depth, ~~stencil)


    /// Clears the given color, and (optionally) the depth and stencil attachments to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : aval<list<Symbol * C4f>>,
                               depth : aval<float option>,
                               stencil : aval<int option>) =
        this.CompileClear(
            signature,
            colors |> AVal.map Map.ofList,
            depth,
            stencil
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


    /// Clears all color, and (optionally) the depth and stencil attachments to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime,
                               signature : IFramebufferSignature,
                               color : aval<C4f>,
                               depth : aval<float option>,
                               stencil : aval<int option>) =

        let attachments =
            signature.ColorAttachments |> Map.toList |> List.map (snd >> fst)

        this.CompileClear(
            signature,
            color |> AVal.map (fun c -> attachments |> List.map (fun sem -> sem, c) |> Map.ofList),
            depth,
            stencil
        )


    /// Clears all color, depth and stencil attachments to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime,
                               signature : IFramebufferSignature,
                               color : aval<C4f>,
                               depth : aval<float>,
                               stencil : aval<int>) =
        this.CompileClear(
            signature,
            color,
            depth |> AVal.map Some,
            stencil |> AVal.map Some
        )

    /// Clears all color, depth and stencil attachments to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime,
                               signature : IFramebufferSignature,
                               color : C4f,
                               depth : float,
                               stencil : int) =
        this.CompileClear(signature, ~~color, ~~depth, ~~stencil)


    /// Clears the depth and stencil attachments to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, depth : aval<float>, stencil : aval<int>) =
        this.CompileClear(signature, ~~Map.empty, depth |> AVal.map Some, stencil |> AVal.map Some)

    /// Clears the depth attachment to the specified value.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, depth : float, stencil : int) =
        this.CompileClear(signature, ~~depth, ~~stencil)


    /// Clears the depth attachment to the specified value.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, depth : aval<float>) =
        this.CompileClear(signature, ~~Map.empty, depth |> AVal.map Some, ~~None)

    /// Clears the depth attachment to the specified value.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, depth : float) =
        this.CompileClear(signature, ~~depth)


    /// Clear the stencil attachment to the specified value.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, stencil : aval<int>) =
        this.CompileClear(signature, ~~Map.empty, ~~None, stencil |> AVal.map Some)

    /// Clear the stencil attachment to the specified value.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, stencil : int) =
        this.CompileClear(signature, ~~stencil)


    // ================================================================================================================
    // CompileClear (color, depth)
    // ================================================================================================================

    /// Clears the given color attachments, and (optionally) the depth attachment to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : aval<Map<Symbol, C4f>>, depth : aval<float option>) =
        this.CompileClear(signature, colors, depth, ~~None)


    /// Clears the given color attachments, and the depth attachment to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : aval<seq<Symbol * C4f>>,
                               depth : aval<float>) =
        this.CompileClear(
            signature,
            colors |> AVal.map Map.ofSeq,
            depth |> AVal.map Some,
            ~~None
        )

    /// Clears the given color attachments, and the depth attachment to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : seq<Symbol * C4f>, depth : float) =
        this.CompileClear(signature, ~~colors, ~~depth)


    /// Clears the given color attachments, and the depth attachment to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : aval<list<Symbol * C4f>>,
                               depth : aval<float>) =
        this.CompileClear(
            signature,
            colors |> AVal.map Map.ofList,
            depth |> AVal.map Some,
            ~~None
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
            ~~None
        )

    /// Clears all color attachments and the depth attachment to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime,
                               signature : IFramebufferSignature,
                               color : C4f, depth : float) =
        this.CompileClear(signature, ~~color, ~~depth)


    // ================================================================================================================
    // CompileClear (color, stencil)
    // ================================================================================================================

    /// Clears the given color attachments, and (optionally) the stencil attachment to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : aval<Map<Symbol, C4f>>, stencil : aval<int option>) =
        this.CompileClear(signature, colors, ~~None, stencil)


    /// Clears the given color attachments, and the stencil attachment to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : aval<seq<Symbol * C4f>>,
                               stencil : aval<int>) =
        this.CompileClear(
            signature,
            colors |> AVal.map Map.ofSeq,
            ~~None,
            stencil |> AVal.map Some
        )

    /// Clears the given color attachments, and the stencil attachment to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : seq<Symbol * C4f>, stencil : int) =
        this.CompileClear(signature, ~~colors, ~~stencil)


    /// Clears the given color attachments, and the stencil attachment to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature,
                               colors : aval<list<Symbol * C4f>>,
                               stencil : aval<int>) =
        this.CompileClear(
            signature,
            colors |> AVal.map Map.ofList,
            ~~None,
            stencil |> AVal.map Some
        )


    /// Clears all color attachments and the stencil attachment to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime,
                               signature : IFramebufferSignature,
                               color : aval<C4f>,
                               stencil : aval<int>) =

        let attachments =
            signature.ColorAttachments |> Map.toList |> List.map (snd >> fst)

        this.CompileClear(
            signature,
            color |> AVal.map (fun c -> attachments |> List.map (fun sem -> sem, c) |> Map.ofList),
            ~~None,
            stencil |> AVal.map Some
        )

    /// Clears all color attachments and the stencil attachment to the specified values.
    [<Extension>]
    static member CompileClear(this : IRuntime,
                               signature : IFramebufferSignature,
                               color : C4f, stencil : int) =
        this.CompileClear(signature, ~~color, ~~stencil)