namespace Aardvark.Rendering.CSharp

open Aardvark.Rendering
open System.Runtime.CompilerServices

[<AbstractClass; Sealed; Extension>]
type IFramebufferRuntimeExtensions private() =

    // ================================================================================================================
    // Clear
    // ================================================================================================================

    /// Clears the framebuffer with the given color.
    [<Extension>]
    static member Clear(this : IFramebufferRuntime, fbo : IFramebuffer, color : ClearColor) =
        this.Clear<_>(fbo, color)

    /// Clears the framebuffer with the given color and depth.
    [<Extension>]
    static member Clear(this : IFramebufferRuntime, fbo : IFramebuffer, color : ClearColor, depth : ClearDepth) =
        this.Clear<_, _>(fbo, color, depth)

    /// Clears the framebuffer with the given color, depth and stencil values.
    [<Extension>]
    static member Clear(this : IFramebufferRuntime, fbo : IFramebuffer, color : ClearColor, depth : ClearDepth, stencil : ClearStencil) =
        this.Clear<_, _, _>(fbo, color, depth, stencil)

    /// Clears the framebuffer with the given depth value.
    [<Extension>]
    static member ClearDepth(this : IFramebufferRuntime, fbo : IFramebuffer, depth : ClearDepth) =
         this.ClearDepth<_>(fbo, depth)

    /// Clears the framebuffer with the given stencil value.
    [<Extension>]
    static member ClearStencil(this : IFramebufferRuntime, fbo : IFramebuffer, stencil : ClearStencil) =
         this.ClearStencil<_>(fbo, stencil)

    /// Clears the framebuffer with the given depth and stencil values.
    [<Extension>]
    static member ClearDepthStencil(this : IFramebufferRuntime, fbo : IFramebuffer, depth : ClearDepth, stencil : ClearStencil) =
         this.ClearDepthStencil<_, _>(fbo, depth, stencil)