namespace Aardvark.SceneGraph.Simple

// Bridges — both directions between the Simple world (ISimpleSg + TraversalState) and the
// legacy Ag world (ISg + Ag.Scope). Field-by-field, no caching — ASet does the delta work.
//
// ── Simple inside Legacy ──
// The Ag's [<Rule>] SimpleSgSemantics handles `RenderObjects` for any ISimpleSg by reading
// the Ag.Scope into a TraversalState and dispatching the node's GetRenderObjects. So an
// ISimpleSg can sit anywhere inside a legacy Sg tree.
//
// ── Legacy inside Simple ──
// `LegacyAdapter(child : ISg)` is an ISimpleSg whose GetRenderObjects builds a small Ag
// helper node (`LegacyBridge`) carrying the current TraversalState, then asks Ag for its
// RenderObjects. The bridge's `[<Rule>]` handlers seed every Ag attribute on the legacy
// child from the TraversalState (same shape as `TrafoApplicator`'s
// `t.Child?ModelTrafoStack <- ...` handlers in Semantics/Trafo.fs), and `RenderObjects`
// delegates to the child via `a.Child?RenderObjects(scope)` (same as Semantics/Adapter.fs).

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open FSharp.Data.Adaptive
open System


/// Bridge helpers between TraversalState and Ag.Scope. (The TraversalState module
/// proper lives next to the type in Simple/TraversalState.fs; this companion adds the
/// Ag-dependent helpers here to keep TraversalState.fs free of Ag references.)
module Bridge =

    /// Read an Ag.Scope into a TraversalState — each field comes from the corresponding
    /// scope attribute extension in Semantics/* (Trafo / Uniforms / Attributes /
    /// Surface / Flags / Modes / Activate).
    let ofScope (scope : Ag.Scope) : TraversalState =
        {
            ModelTrafoStack          = scope.ModelTrafoStack
            ViewTrafo                = scope.ViewTrafo
            ProjTrafo                = scope.ProjTrafo

            Uniforms                 = scope.Uniforms
            VertexAttributes         = scope.VertexAttributes
            InstanceAttributes       = scope.InstanceAttributes
            VertexIndexBuffer        = scope.VertexIndexBuffer
            FaceVertexCount          = scope.FaceVertexCount

            Surface                  = scope.Surface
            IsActive                 = scope.IsActive
            IsTransparent            = scope.IsTransparent
            RenderPass               = scope.RenderPass

            BlendMode                = scope.BlendMode
            BlendConstant            = scope.BlendConstant
            ColorWriteMask           = scope.ColorWriteMask
            AttachmentBlendMode      = scope.AttachmentBlendMode
            AttachmentColorWriteMask = scope.AttachmentColorWriteMask

            DepthTest                = scope.DepthTest
            DepthBias                = scope.DepthBias
            DepthWriteMask           = scope.DepthWriteMask
            DepthClamp               = scope.DepthClamp

            StencilModeFront         = scope.StencilModeFront
            StencilWriteMaskFront    = scope.StencilWriteMaskFront
            StencilModeBack          = scope.StencilModeBack
            StencilWriteMaskBack     = scope.StencilWriteMaskBack

            CullMode                 = scope.CullMode
            FrontFacing              = scope.FrontFacing
            FillMode                 = scope.FillMode
            Multisample              = scope.Multisample
            ConservativeRaster       = scope.ConservativeRaster
            Viewport                 = scope.Viewport
            Scissor                  = scope.Scissor

            CameraLocation           = scope.CameraLocation
            Activate                 = scope.Activate
        }


/// Embeds a legacy ISg inside a Simple tree. Its GetRenderObjects constructs a
/// LegacyBridge carrying the current TraversalState and asks Ag for its RenderObjects;
/// the bridge's [<Rule>] handlers seed every attribute on the child from the TS.
[<Sealed>]
type LegacyAdapter(child : ISg) =
    member _.Child = child
    interface ISg
    interface ISimpleSg with
        member _.GetRenderObjects (ts : TraversalState) =
            let bridge = LegacyBridge(child, ts) :> ISg
            bridge.RenderObjects(Ag.Scope.Root)


/// Helper Ag node: carries (child, ts) so its [<Rule>] handlers can inject the TS values
/// onto the child's scope. Not public — instantiated only by LegacyAdapter.
and [<Sealed>] LegacyBridge(child : ISg, ts : TraversalState) =
    member _.Child = child
    member _.TS    = ts
    interface ISg


[<AutoOpen>]
module BridgeSemantics =

    /// Ag rule injecting every TraversalState field onto the child of a LegacyBridge.
    /// Mirrors the shape of `TrafoApplicator`'s `t.Child?ModelTrafoStack <- ...` handlers
    /// in Semantics/Trafo.fs etc.; one method per field (33), all the same shape.
    [<Rule>]
    type LegacyBridgeSem() =

        // Trafos
        member x.ModelTrafoStack(a : LegacyBridge, _ : Scope) = a.Child?ModelTrafoStack <- a.TS.ModelTrafoStack
        member x.ViewTrafo      (a : LegacyBridge, _ : Scope) = a.Child?ViewTrafo       <- a.TS.ViewTrafo
        member x.ProjTrafo      (a : LegacyBridge, _ : Scope) = a.Child?ProjTrafo       <- a.TS.ProjTrafo

        // Providers + attributes
        member x.Uniforms          (a : LegacyBridge, _ : Scope) = a.Child?Uniforms          <- a.TS.Uniforms
        member x.VertexAttributes  (a : LegacyBridge, _ : Scope) = a.Child?VertexAttributes  <- a.TS.VertexAttributes
        member x.InstanceAttributes(a : LegacyBridge, _ : Scope) = a.Child?InstanceAttributes<- a.TS.InstanceAttributes
        member x.VertexIndexBuffer (a : LegacyBridge, _ : Scope) = a.Child?VertexIndexBuffer <- a.TS.VertexIndexBuffer
        member x.FaceVertexCount   (a : LegacyBridge, _ : Scope) = a.Child?FaceVertexCount   <- a.TS.FaceVertexCount

        // Surface + flags
        member x.Surface      (a : LegacyBridge, _ : Scope) = a.Child?Surface       <- a.TS.Surface
        member x.IsActive     (a : LegacyBridge, _ : Scope) = a.Child?IsActive      <- a.TS.IsActive
        member x.IsTransparent(a : LegacyBridge, _ : Scope) = a.Child?IsTransparent <- a.TS.IsTransparent
        member x.RenderPass   (a : LegacyBridge, _ : Scope) = a.Child?RenderPass    <- a.TS.RenderPass

        // Blend
        member x.BlendMode               (a : LegacyBridge, _ : Scope) = a.Child?BlendMode                <- a.TS.BlendMode
        member x.BlendConstant           (a : LegacyBridge, _ : Scope) = a.Child?BlendConstant            <- a.TS.BlendConstant
        member x.ColorWriteMask          (a : LegacyBridge, _ : Scope) = a.Child?ColorWriteMask           <- a.TS.ColorWriteMask
        member x.AttachmentBlendMode     (a : LegacyBridge, _ : Scope) = a.Child?AttachmentBlendMode      <- a.TS.AttachmentBlendMode
        member x.AttachmentColorWriteMask(a : LegacyBridge, _ : Scope) = a.Child?AttachmentColorWriteMask <- a.TS.AttachmentColorWriteMask

        // Depth
        member x.DepthTest     (a : LegacyBridge, _ : Scope) = a.Child?DepthTest      <- a.TS.DepthTest
        member x.DepthBias     (a : LegacyBridge, _ : Scope) = a.Child?DepthBias      <- a.TS.DepthBias
        member x.DepthWriteMask(a : LegacyBridge, _ : Scope) = a.Child?DepthWriteMask <- a.TS.DepthWriteMask
        member x.DepthClamp    (a : LegacyBridge, _ : Scope) = a.Child?DepthClamp     <- a.TS.DepthClamp

        // Stencil
        member x.StencilModeFront     (a : LegacyBridge, _ : Scope) = a.Child?StencilModeFront      <- a.TS.StencilModeFront
        member x.StencilWriteMaskFront(a : LegacyBridge, _ : Scope) = a.Child?StencilWriteMaskFront <- a.TS.StencilWriteMaskFront
        member x.StencilModeBack      (a : LegacyBridge, _ : Scope) = a.Child?StencilModeBack       <- a.TS.StencilModeBack
        member x.StencilWriteMaskBack (a : LegacyBridge, _ : Scope) = a.Child?StencilWriteMaskBack  <- a.TS.StencilWriteMaskBack

        // Rasterizer + viewport
        member x.CullMode          (a : LegacyBridge, _ : Scope) = a.Child?CullMode           <- a.TS.CullMode
        member x.FrontFacing       (a : LegacyBridge, _ : Scope) = a.Child?FrontFacing        <- a.TS.FrontFacing
        member x.FillMode          (a : LegacyBridge, _ : Scope) = a.Child?FillMode           <- a.TS.FillMode
        member x.Multisample       (a : LegacyBridge, _ : Scope) = a.Child?Multisample        <- a.TS.Multisample
        member x.ConservativeRaster(a : LegacyBridge, _ : Scope) = a.Child?ConservativeRaster <- a.TS.ConservativeRaster
        member x.Viewport          (a : LegacyBridge, _ : Scope) = a.Child?Viewport           <- a.TS.Viewport
        member x.Scissor           (a : LegacyBridge, _ : Scope) = a.Child?Scissor            <- a.TS.Scissor

        // Environment + lifecycle
        member x.CameraLocation(a : LegacyBridge, _ : Scope) = a.Child?CameraLocation <- a.TS.CameraLocation
        member x.Activate      (a : LegacyBridge, _ : Scope) = a.Child?Activate       <- a.TS.Activate

        // RenderObjects: delegate to child — same as Semantics/Adapter.fs:15.
        member x.RenderObjects(a : LegacyBridge, scope : Ag.Scope) : aset<IRenderObject> =
            a.Child?RenderObjects(scope)


    /// Ag rule for `ISimpleSg` in a legacy tree: read the scope into a TraversalState and
    /// dispatch the node's GetRenderObjects. This is the *Simple-inside-legacy* bridge.
    [<Rule>]
    type SimpleSgSemantics() =
        member x.RenderObjects(s : ISimpleSg, scope : Ag.Scope) : aset<IRenderObject> =
            s.GetRenderObjects (Bridge.ofScope scope)
