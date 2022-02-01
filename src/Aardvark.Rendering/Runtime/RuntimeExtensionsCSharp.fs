namespace Aardvark.Rendering.CSharp

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices

[<AbstractClass; Sealed; Extension>]
type IRuntimeExtensions private() =

    // ================================================================================================================
    // CompileClear (single color)
    // ================================================================================================================

    /// Compiles a render task for clearing a framebuffer with the given color.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : aval<ClearColor>) = this.CompileClear<_>(signature, color)

    /// Compiles a render task for clearing a framebuffer with the given color.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : aval<C4f>) = this.CompileClear<_>(signature, color)

    /// Compiles a render task for clearing a framebuffer with the given color.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : aval<V4i>) = this.CompileClear<_>(signature, color)


    // ================================================================================================================
    // CompileClear (single color and depth)
    // ================================================================================================================

    /// Compiles a render task for clearing a framebuffer with the given color and depth.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : aval<ClearColor>, depth : aval<ClearDepth>) = this.CompileClear<_, _>(signature, color, depth)

    /// Compiles a render task for clearing a framebuffer with the given color and depth.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : aval<C4f>, depth : aval<float>) = this.CompileClear<_, _>(signature, color, depth)

    /// Compiles a render task for clearing a framebuffer with the given color and depth.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : aval<V4i>, depth : aval<float>) = this.CompileClear<_, _>(signature, color, depth)


    // ================================================================================================================
    // CompileClear (single color, depth, and stencil)
    // ================================================================================================================

    /// Compiles a render task for clearing a framebuffer with the given color, depth, and stencil.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : aval<ClearColor>, depth : aval<ClearDepth>, stencil : aval<ClearStencil>) = this.CompileClear<_, _, _>(signature, color, depth, stencil)

    /// Compiles a render task for clearing a framebuffer with the given color, depth, and stencil.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : aval<C4f>, depth : aval<float>, stencil : aval<int>) = this.CompileClear<_, _, _>(signature, color, depth, stencil)
    
    /// Compiles a render task for clearing a framebuffer with the given color, depth, and stencil.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : aval<V4i>, depth : aval<float>, stencil : aval<int>) = this.CompileClear<_, _, _>(signature, color, depth, stencil)


    // ================================================================================================================
    // CompileClear (depth)
    // ================================================================================================================

    /// Compiles a render task for clearing a framebuffer with the given depth value.
    [<Extension>]
    static member CompileClearDepth(this : IRuntime, signature : IFramebufferSignature, depth : aval<ClearDepth>) =
        this.CompileClearDepth<_>(signature, depth)

    /// Compiles a render task for clearing a framebuffer with the given depth value.
    [<Extension>]
    static member CompileClearDepth(this : IRuntime, signature : IFramebufferSignature, depth : aval<float>) =
        this.CompileClearDepth<_>(signature, depth)


    // ================================================================================================================
    // CompileClear (stencil)
    // ================================================================================================================

    /// Compiles a render task for clearing a framebuffer with the given stencil value.
    [<Extension>]
    static member CompileClearStencil(this : IRuntime, signature : IFramebufferSignature, stencil : aval<ClearStencil>) =
        this.CompileClearStencil<_>(signature, stencil)

    /// Compiles a render task for clearing a framebuffer with the given stencil value.
    [<Extension>]
    static member CompileClearStencil(this : IRuntime, signature : IFramebufferSignature, stencil : aval<int>) =
        this.CompileClearStencil<_>(signature, stencil)


    // ================================================================================================================
    // CompileClear (depth, stencil)
    // ================================================================================================================

    /// Compiles a render task for clearing a framebuffer with the given depth and stencil values.
    [<Extension>]
    static member CompileClearDepthStencil(this : IRuntime, signature : IFramebufferSignature, depth : aval<ClearDepth>, stencil : aval<ClearStencil>) =
        this.CompileClearDepthStencil<_, _>(signature, depth, stencil)

    /// Compiles a render task for clearing a framebuffer with the given depth and stencil values.
    [<Extension>]
    static member CompileClearDepthStencil(this : IRuntime, signature : IFramebufferSignature, depth : aval<float>, stencil : aval<int>) =
        this.CompileClearDepthStencil<_, _>(signature, depth, stencil)


    // ================================================================================================================
    // CompileClear (non-adaptive with implicit conversions)
    // ================================================================================================================

    /// Compiles a render task for clearing a framebuffer with the given color.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : ClearColor) =
        this.CompileClear<_>(signature, color)

    /// Compiles a render task for clearing a framebuffer with the given color and depth.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : ClearColor, depth : ClearDepth) =
        this.CompileClear<_, _>(signature, color, depth)

    /// Compiles a render task for clearing a framebuffer with the given color, depth and stencil.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : ClearColor, depth : ClearDepth, stencil : ClearStencil) =
        this.CompileClear<_, _, _>(signature, color, depth, stencil)

    /// Compiles a render task for clearing a framebuffer with the given depth value.
    [<Extension>]
    static member CompileClearDepth(this : IRuntime, signature : IFramebufferSignature, depth : ClearDepth) =
        this.CompileClearDepth<_>(signature, depth)

    /// Compiles a render task for clearing a framebuffer with the given stencil value.
    [<Extension>]
    static member CompileClearStencil(this : IRuntime, signature : IFramebufferSignature, stencil : ClearStencil) =
        this.CompileClearStencil<_>(signature, stencil)

    /// Compiles a render task for clearing a framebuffer with the given depth and stencil values.
    [<Extension>]
    static member CompileClearDepthStencil(this : IRuntime, signature : IFramebufferSignature, depth : ClearDepth, stencil : ClearStencil) =
        this.CompileClearDepthStencil<_, _>(signature, depth, stencil)


    // ================================================================================================================
    // CompileClear (multiple nonadaptive colors)
    // ================================================================================================================

    /// Compiles a render task for clearing a framebuffer with the given colors.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, colors : Map<Symbol, C4f>) =
        this.CompileClear<_>(signature, colors)

    /// Compiles a render task for clearing a framebuffer with the given colors.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, colors : seq<Symbol * C4f>) =
        this.CompileClear<_>(signature, colors)

    /// Compiles a render task for clearing a framebuffer with the given colors and depth.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, colors : Map<Symbol, C4f>, depth : ClearDepth) =
        this.CompileClear<_, _>(signature, colors, depth)

    /// Compiles a render task for clearing a framebuffer with the given colors and depth.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, colors : seq<Symbol * C4f>, depth : ClearDepth) =
        this.CompileClear<_, _>(signature, colors, depth)

    /// Compiles a render task for clearing a framebuffer with the given colors, depth, and stencil.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, colors : Map<Symbol, C4f>, depth : ClearDepth, stencil : ClearStencil) =
        this.CompileClear<_, _, _>(signature, colors, depth, stencil)

    /// Compiles a render task for clearing a framebuffer with the given colors, depth, and stencil.
    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, colors : seq<Symbol * C4f>, depth : ClearDepth, stencil : ClearStencil) =
        this.CompileClear<_, _, _>(signature, colors, depth, stencil)