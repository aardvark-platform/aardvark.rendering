namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open System.Runtime.CompilerServices

[<AutoOpen>]
module IRuntimeFSharpExtensions =

    // These extensions use SRTPs so MUST NOT be exposed to C#
    type IRuntime with

        // ================================================================================================================
        // CompileClear
        // ================================================================================================================

        /// Compiles a render task for clearing a framebuffer with the given color.
        member inline x.CompileClear(signature : IFramebufferSignature, color : aval< ^Color>) =
            let values = color |> AVal.map (fun c -> clear { color c })
            x.CompileClear(signature, values)

        /// Compiles a render task for clearing a framebuffer with the given color and depth.
        member inline x.CompileClear(signature : IFramebufferSignature, color : aval< ^Color>, depth : aval< ^Depth>) =
            let values = (color, depth) ||> AVal.map2 (fun c d -> clear { color c; depth d })
            x.CompileClear(signature, values)

        /// Compiles a render task for clearing a framebuffer with the given color, depth, and stencil.
        member inline x.CompileClear(signature : IFramebufferSignature,
                                          color : aval< ^Color>, depth : aval< ^Depth>, stencil : aval< ^Stencil>) =
            let values = (color, depth, stencil) |||> AVal.map3 (fun c d s -> clear { color c; depth d; stencil s })
            x.CompileClear(signature, values)

        /// Compiles a render task for clearing a framebuffer with the given depth value.
        member inline x.CompileClearDepth(signature : IFramebufferSignature, depth : aval< ^Depth>) =
            let values = depth |> AVal.map (fun d -> clear { depth d })
            x.CompileClear(signature, values)

        /// Compiles a render task for clearing a framebuffer with the given stencil value.
        member inline x.CompileClearStencil(signature : IFramebufferSignature, stencil : aval< ^Stencil>) =
            let values = stencil |> AVal.map (fun s -> clear { stencil s })
            x.CompileClear(signature, values)

        /// Compiles a render task for clearing a framebuffer with the given depth and stencil values.
        member inline x.CompileClearDepthStencil(signature : IFramebufferSignature, depth : aval< ^Depth>, stencil : aval< ^Stencil>) =
            let values = (depth, stencil) ||> AVal.map2 (fun d s -> clear { depth d; stencil s })
            x.CompileClear(signature, values)


        /// Compiles a render task for clearing a framebuffer with the given color.
        member inline x.CompileClear(signature : IFramebufferSignature, color : ^Color) =
            x.CompileClear(signature, ~~color)

        /// Compiles a render task for clearing a framebuffer with the given colors.
        member inline x.CompileClear(signature : IFramebufferSignature, colors : Map<Symbol, ^Color>) =
            x.CompileClear(signature, ClearValues.empty |> ClearValues.colors colors |> AVal.constant)

        ///// Compiles a render task for clearing a framebuffer with the given colors.
        member inline x.CompileClear(signature : IFramebufferSignature, colors : seq<Symbol * ^Color>) =
            x.CompileClear(signature, ClearValues.empty |> ClearValues.colors (Map.ofSeq colors) |> AVal.constant)

        /// Compiles a render task for clearing a framebuffer with the given color and depth.
        member inline x.CompileClear(signature : IFramebufferSignature, color : ^Color, depth : ^Depth) =
            x.CompileClear(signature, ~~color, ~~depth)

        /// Compiles a render task for clearing a framebuffer with the given colors and depth.
        member inline x.CompileClear(signature : IFramebufferSignature, colors : Map<Symbol, ^Color>, depth : ^Depth) =
            let values = ClearValues.empty |> ClearValues.colors colors |> ClearValues.depth depth
            x.CompileClear(signature, AVal.constant values)

        /// Compiles a render task for clearing a framebuffer with the given colors and depth.
        member inline x.CompileClear(signature : IFramebufferSignature, colors : seq<Symbol * ^Color>, depth : ^Depth) =
            let values = ClearValues.empty |> ClearValues.colors (Map.ofSeq colors) |> ClearValues.depth depth
            x.CompileClear(signature, AVal.constant values)

        /// Compiles a render task for clearing a framebuffer with the given color, depth and stencil.
        member inline x.CompileClear(signature : IFramebufferSignature, color : ^Color, depth : ^Depth, stencil : ^Stencil) =
            x.CompileClear(signature, ~~color, ~~depth, ~~stencil)

        /// Compiles a render task for clearing a framebuffer with the given colors, depth and stencil.
        member inline x.CompileClear(signature : IFramebufferSignature, colors : Map<Symbol, ^Color>, depth : ^Depth, stencil : ^Stencil) =
            let values = ClearValues.empty |> ClearValues.colors colors |> ClearValues.depth depth |> ClearValues.stencil stencil
            x.CompileClear(signature, AVal.constant values)

        /// Compiles a render task for clearing a framebuffer with the given colors, depth and stencil.
        member inline x.CompileClear(signature : IFramebufferSignature, colors : seq<Symbol * ^Color>, depth : ^Depth, stencil : ^Stencil) =
            let values = ClearValues.empty |> ClearValues.colors (Map.ofSeq colors) |> ClearValues.depth depth |> ClearValues.stencil stencil
            x.CompileClear(signature, AVal.constant values)

        /// Compiles a render task for clearing a framebuffer with the given depth value.
        member inline x.CompileClearDepth(signature : IFramebufferSignature, depth : ^Depth) =
            x.CompileClearDepth(signature, ~~depth)

        /// Compiles a render task for clearing a framebuffer with the given stencil value.
        member inline x.CompileClearStencil(signature : IFramebufferSignature, stencil : ^Stencil) =
            x.CompileClearStencil(signature, ~~stencil)

        /// Compiles a render task for clearing a framebuffer with the given depth and stencil values.
        member inline x.CompileClearDepthStencil(signature : IFramebufferSignature, depth : ^Depth, stencil : ^Stencil) =
            x.CompileClearDepthStencil(signature, ~~depth, ~~stencil)


[<AbstractClass; Sealed; Extension>]
type IRuntimeExtensions private() =

    // ================================================================================================================
    // CompileRender
    // ================================================================================================================

    /// Compiles a render task for the given render objects.
    [<Extension>]
    static member CompileRender(this : IRuntime, signature : IFramebufferSignature, rjs : aset<IRenderObject>) =
        this.CompileRender(signature, rjs, this.DebugLevel = DebugLevel.Full)


    // ================================================================================================================
    // CompileClear
    // ================================================================================================================

    /// Compiles a render task for clearing a framebuffer with the given values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, values : ClearValues) =
        this.CompileClear(signature, ~~values)