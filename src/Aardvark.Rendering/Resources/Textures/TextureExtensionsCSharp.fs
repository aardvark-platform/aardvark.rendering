namespace Aardvark.Rendering.CSharp

open Aardvark.Rendering
open System.Runtime.CompilerServices

[<AbstractClass; Sealed; Extension>]
type ITextureRuntimeExtensions private() =

    // ================================================================================================================
    // Clear
    // ================================================================================================================

    /// Clears the texture with the given color.
    [<Extension>]
    static member Clear(this : ITextureRuntime, texture : IBackendTexture, color : ClearColor) =
        this.Clear<_>(texture, color)

    /// Clears the texture with the given depth value.
    [<Extension>]
    static member ClearDepth(this : ITextureRuntime, texture : IBackendTexture, depth : ClearDepth) =
         this.ClearDepth<_>(texture, depth)

    /// Clears the texture with the given stencil value.
    [<Extension>]
    static member ClearStencil(this : ITextureRuntime, texture : IBackendTexture, stencil : ClearStencil) =
         this.ClearStencil<_>(texture, stencil)

    /// Clears the texture with the given depth and stencil values.
    [<Extension>]
    static member ClearDepthStencil(this : ITextureRuntime, texture : IBackendTexture, depth : ClearDepth, stencil : ClearStencil) =
         this.ClearDepthStencil<_, _>(texture, depth, stencil)