namespace Aardvark.Base

open Aardvark.Base
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices

[<AbstractClass; Sealed; Extension>]
type IRuntimeExtensions private() =

    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, colors : aval<seq<Symbol * C4f>>, depth : aval<float>) =
        this.CompileClear(signature, colors |> AVal.map Map.ofSeq, depth |> AVal.map Some)

    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, colors : aval<list<Symbol * C4f>>, depth : aval<float>) =
        this.CompileClear(signature, colors |> AVal.map Map.ofList, depth |> AVal.map Some)

    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : aval<C4f>, depth : aval<float>) =
        this.CompileClear(signature, color |> AVal.map (fun c -> Map.ofList [DefaultSemantic.Colors, c]), depth |> AVal.map Some)

    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : aval<C4f>) =
        this.CompileClear(signature, color |> AVal.map (fun c -> Map.ofList [DefaultSemantic.Colors, c]), AVal.constant None)

    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, depth : aval<float>) =
        this.CompileClear(signature, AVal.constant Map.empty, depth |> AVal.map Some)

    /// <summary>
    /// Clear all color attachments, depth and stencil of a framebuffer object (each optional).
    /// </summary>
    [<Extension>]
    static member Clear(this : IRuntime, fbo : IFramebuffer, color : Option<C4f>, depth : Option<float>, stencil : Option<int>) =
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
    static member Clear(this : IRuntime, fbo : IFramebuffer, name : Symbol, color : C4f) =
        let clearColors = Map.ofSeq [(name, color) ]
        this.Clear(fbo, clearColors, None, None)