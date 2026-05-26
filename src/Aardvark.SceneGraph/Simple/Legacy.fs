namespace Aardvark.SceneGraph.Simple

// Legacy bridge — *Simple-side* half. Lets a legacy `ISg` (one that does NOT implement
// ISimpleSg) be consumed by an ISimpleSg parent. The reverse direction (an ISimpleSg
// inside a legacy Ag tree) lives in Simple/Bridges.fs because it needs the scope-reading
// member-extensions from Semantics/*, which compile later.
//
// This file ALSO hosts the dispatch helper used by every legacy Sg.X node's ISimpleSg
// implementation in Core/Sg.fs:
//
//     match child with
//     | :? ISimpleSg as s -> s.GetRenderObjects ts          // fast path
//     | _                 -> LegacyAdapter(child).GetRenderObjects ts
//
// Compiled BEFORE Core/Sg.fs so the legacy nodes can reference both LegacyAdapter and
// the dispatch helper. Writes only — no scope reads — so it doesn't depend on the
// Semantics scope-extension members.

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive


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
            // direct Ag query — the ISg.RenderObjects(scope) member-extension in
            // Semantics/RenderObject.fs:122 compiles later, so we can't use it here.
            bridge?RenderObjects(Ag.Scope.Root) : aset<IRenderObject>


/// Helper Ag node: carries (child, ts) so its [<Rule>] handlers can inject the TS values
/// onto the child's scope.
and [<Sealed>] LegacyBridge(child : ISg, ts : TraversalState) =
    member _.Child = child
    member _.TS    = ts
    interface ISg


[<AutoOpen>]
module LegacyBridgeSemantics =

    /// Ag rule injecting every TraversalState field onto the child of a LegacyBridge.
    /// Mirrors the shape of `TrafoApplicator`'s `t.Child?ModelTrafoStack <- ...` handlers
    /// in Semantics/Trafo.fs etc.; one method per field, all the same shape.
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


/// Dispatch helper for legacy `Sg.*` nodes' ISimpleSg implementations: prefer the fast
/// ISimpleSg path on the child if it has one, otherwise bridge via LegacyAdapter.
[<AbstractClass; Sealed>]
type SimpleDispatch private () =
    static member Get (sg : ISg, ts : TraversalState) : aset<IRenderObject> =
        match sg with
        | :? ISimpleSg as s -> s.GetRenderObjects ts
        | _                 -> (LegacyAdapter(sg) :> ISimpleSg).GetRenderObjects ts
