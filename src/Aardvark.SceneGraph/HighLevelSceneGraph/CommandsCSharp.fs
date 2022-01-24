namespace Aardvark.SceneGraph.CSharp

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices

[<AbstractClass; Sealed; Extension>]
type RuntimeCommandExtensions private() =

    // Single color
    static member Clear(color : aval<ClearColor>) = RenderCommand.Clear<_>(color)
    static member Clear(color : aval<C4f>)        = RenderCommand.Clear<_>(color)
    static member Clear(color : aval<V4i>)        = RenderCommand.Clear<_>(color)

    // Single color and depth
    static member Clear(color : aval<ClearColor>, depth : aval<ClearDepth>)  = RenderCommand.Clear<_,_>(color, depth)
    static member Clear(color : aval<C4f>,  depth : aval<float>)             = RenderCommand.Clear<_,_>(color, depth)
    static member Clear(color : aval<V4i>,  depth : aval<float>)             = RenderCommand.Clear<_,_>(color, depth)

    // Single color, depth, and stencil
    static member Clear(color : aval<ClearColor>, depth : aval<ClearDepth>, stencil : aval<ClearStencil>) = RenderCommand.Clear<_,_,_>(color, depth, stencil)
    static member Clear(color : aval<C4f>,  depth : aval<float>, stencil : aval<int>)                     = RenderCommand.Clear<_,_,_>(color, depth, stencil)
    static member Clear(color : aval<V4i>,  depth : aval<float>, stencil : aval<int>)                     = RenderCommand.Clear<_,_,_>(color, depth, stencil)

    // Depth
    static member ClearDepth(depth : aval<ClearDepth>) = RenderCommand.ClearDepth<_>(depth)
    static member ClearDepth(depth : aval<float>)      = RenderCommand.ClearDepth<_>(depth)

    // Stencil
    static member ClearStencil(stencil : aval<ClearStencil>) = RenderCommand.ClearStencil<_>(stencil)
    static member ClearStencil(stencil : aval<int>)          = RenderCommand.ClearStencil<_>(stencil)

    // Depth, stencil
    static member ClearDepthStencil(depth : aval<ClearDepth>, stencil : aval<ClearStencil>) = RenderCommand.ClearDepthStencil<_,_>(depth, stencil)
    static member ClearDepthStencil(depth : aval<float>, stencil : aval<int>)               = RenderCommand.ClearDepthStencil<_,_>(depth, stencil)

    // Non adaptive
    // Don't need overloads here due to implicit conversions
    static member Clear(color : ClearColor)                                             = RenderCommand.Clear<_>(color)
    static member Clear(color : ClearColor, depth : ClearDepth)                         = RenderCommand.Clear<_,_>(color, depth)
    static member Clear(color : ClearColor, depth : ClearDepth, stencil : ClearStencil) = RenderCommand.Clear<_,_,_>(color, depth, stencil)
    static member ClearDepth(depth : ClearDepth)                                        = RenderCommand.ClearDepth<_>(depth)
    static member ClearStencil(stencil : ClearStencil)                                  = RenderCommand.ClearStencil<_>(stencil)
    static member ClearDepthStencil(depth : ClearDepth, stencil : ClearStencil)         = RenderCommand.ClearDepthStencil<_,_>(depth, stencil)