namespace Aardvark.Rendering

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
        this.CompileRender(signature, rjs, this.DebugLevel = DebugLevel.Full)


    // ================================================================================================================
    // CompileClear
    // ================================================================================================================

    /// Compiles a render task for clearing a framebuffer with the given color.
    [<Extension>]
    static member inline CompileClear(this : IRuntime, signature : IFramebufferSignature, color : aval< ^Color>) =
        let values = color |> AVal.map (fun c -> clear { color c })
        this.CompileClear(signature, values)

    /// Compiles a render task for clearing a framebuffer with the given color and depth.
    [<Extension>]
    static member inline CompileClear(this : IRuntime, signature : IFramebufferSignature, color : aval< ^Color>, depth : aval< ^Depth>) =
        let values = (color, depth) ||> AVal.map2 (fun c d -> clear { color c; depth d })
        this.CompileClear(signature, values)

    /// Compiles a render task for clearing a framebuffer with the given color, depth, and stencil.
    [<Extension>]
    static member inline CompileClear(this : IRuntime, signature : IFramebufferSignature,
                                      color : aval< ^Color>, depth : aval< ^Depth>, stencil : aval< ^Stencil>) =
        let values = (color, depth, stencil) |||> AVal.map3 (fun c d s -> clear { color c; depth d; stencil s })
        this.CompileClear(signature, values)

    /// Compiles a render task for clearing a framebuffer with the given depth value.
    [<Extension>]
    static member inline CompileClearDepth(this : IRuntime, signature : IFramebufferSignature, depth : aval< ^Depth>) =
        let values = depth |> AVal.map (fun d -> clear { depth d })
        this.CompileClear(signature, values)

    /// Compiles a render task for clearing a framebuffer with the given stencil value.
    [<Extension>]
    static member inline CompileClearStencil(this : IRuntime, signature : IFramebufferSignature, stencil : aval< ^Stencil>) =
        let values = stencil |> AVal.map (fun s -> clear { stencil s })
        this.CompileClear(signature, values)

    /// Compiles a render task for clearing a framebuffer with the given depth and stencil values.
    [<Extension>]
    static member inline CompileClearDepthStencil(this : IRuntime, signature : IFramebufferSignature, depth : aval< ^Depth>, stencil : aval< ^Stencil>) =
        let values = (depth, stencil) ||> AVal.map2 (fun d s -> clear { depth d; stencil s })
        this.CompileClear(signature, values)


    /// Compiles a render task for clearing a framebuffer with the given values.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, values : ClearValues) =
        this.CompileClear(signature, ~~values)

    /// Compiles a render task for clearing a framebuffer with the given color.
    [<Extension>]
    static member inline CompileClear(this : IRuntime, signature : IFramebufferSignature, color : ^Color) =
        this.CompileClear(signature, ~~color)

    /// Compiles a render task for clearing a framebuffer with the given color and depth.
    [<Extension>]
    static member inline CompileClear(this : IRuntime, signature : IFramebufferSignature, color : ^Color, depth : ^Depth) =
        this.CompileClear(signature, ~~color, ~~depth)

    /// Compiles a render task for clearing a framebuffer with the given color, depth and stencil.
    [<Extension>]
    static member inline CompileClear(this : IRuntime, signature : IFramebufferSignature, color : ^Color, depth : ^Depth, stencil : ^Stencil) =
        this.CompileClear(signature, ~~color, ~~depth, ~~stencil)

    /// Compiles a render task for clearing a framebuffer with the given depth value.
    [<Extension>]
    static member inline CompileClearDepth(this : IRuntime, signature : IFramebufferSignature, depth : ^Depth) =
        this.CompileClearDepth(signature, ~~depth)

    /// Compiles a render task for clearing a framebuffer with the given stencil value.
    [<Extension>]
    static member inline CompileClearStencil(this : IRuntime, signature : IFramebufferSignature, stencil : ^Stencil) =
        this.CompileClearStencil(signature, ~~stencil)

    /// Compiles a render task for clearing a framebuffer with the given depth and stencil values.
    [<Extension>]
    static member inline CompileClearDepthStencil(this : IRuntime, signature : IFramebufferSignature, depth : ^Depth, stencil : ^Stencil) =
        this.CompileClearDepthStencil(signature, ~~depth, ~~stencil)