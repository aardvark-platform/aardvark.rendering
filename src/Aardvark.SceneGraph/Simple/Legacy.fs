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


module Bridge =

    /// Read an Ag.Scope into a TraversalState. Uses the raw `?` operator instead of
    /// the per-attribute `Scope.X` member-extensions because this file compiles before
    /// Semantics/*.fs where those extensions live — same backing Ag attribute lookup,
    /// just no syntactic sugar. Field-by-field copy.
    ///
    /// Special case: `Ag.Scope.Root` has no node, so the Root<ISg> seeder rules
    /// haven't populated any inherited attributes — eagerly reading them throws
    /// `ArgumentNullException` from the internal `ConditionalWeakTable`. We return
    /// `TraversalState.empty` instead, which mirrors every Root<ISg> seeder
    /// one-to-one by design (Semantics/Trafo.fs, Attributes.fs, Flags.fs, …).
    let ofScope (scope : Ag.Scope) : TraversalState =
        if System.Object.ReferenceEquals(scope, Ag.Scope.Root) then
            TraversalState.empty
        else
        {
            ModelTrafoStack          = scope?ModelTrafoStack
            ViewTrafo                = scope?ViewTrafo
            ProjTrafo                = scope?ProjTrafo

            Uniforms                 = scope?Uniforms
            VertexAttributes         = scope?VertexAttributes
            InstanceAttributes       = scope?InstanceAttributes
            VertexIndexBuffer        = scope?VertexIndexBuffer
            FaceVertexCount          = scope?FaceVertexCount

            Surface                  = scope?Surface
            IsActive                 = scope?IsActive
            IsTransparent            = scope?IsTransparent
            RenderPass               = scope?RenderPass

            BlendMode                = scope?BlendMode
            BlendConstant            = scope?BlendConstant
            ColorWriteMask           = scope?ColorWriteMask
            AttachmentBlendMode      = scope?AttachmentBlendMode
            AttachmentColorWriteMask = scope?AttachmentColorWriteMask

            DepthTest                = scope?DepthTest
            DepthBias                = scope?DepthBias
            DepthWriteMask           = scope?DepthWriteMask
            DepthClamp               = scope?DepthClamp

            StencilModeFront         = scope?StencilModeFront
            StencilWriteMaskFront    = scope?StencilWriteMaskFront
            StencilModeBack          = scope?StencilModeBack
            StencilWriteMaskBack     = scope?StencilWriteMaskBack

            CullMode                 = scope?CullMode
            FrontFacing              = scope?FrontFacing
            FillMode                 = scope?FillMode
            Multisample              = scope?Multisample
            ConservativeRaster       = scope?ConservativeRaster
            Viewport                 = scope?Viewport
            Scissor                  = scope?Scissor

            CameraLocation           = scope?CameraLocation
            Activate                 = scope?Activate

            // The one ambient (not-in-Root-seeders) attribute used in production:
            // tolerated as nullable since callers may not have set it on the scope.
            Runtime                  =
                try (scope?Runtime : IRuntime)
                with _ -> Unchecked.defaultof<IRuntime>
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

        // Ambient `Runtime` — only set when the TS carries one (entry-point seeded
        // it). Leaves like Text.Sg.ShapeSem read `scope.Runtime` and would throw
        // otherwise.
        member x.Runtime(a : LegacyBridge, _ : Scope) =
            if not (isNull a.TS.Runtime) then
                a.Child?Runtime <- a.TS.Runtime

        // RenderObjects: delegate to child — same as Semantics/Adapter.fs:15.
        member x.RenderObjects(a : LegacyBridge, scope : Ag.Scope) : aset<IRenderObject> =
            a.Child?RenderObjects(scope)


/// Dispatch helper for legacy `Sg.*` nodes' ISimpleSg implementations.
///
/// • `Get(child, ts)` — prefer the fast ISimpleSg path on the child if it has one,
///   otherwise bridge via LegacyAdapter. Used by every applicator-style Sg.* node.
///
/// • `Bridge(self, ts)` — the round-trip for *leaf* nodes (RenderNode, AdapterNode,
///   IndirectRenderNode, DelayNode, …) whose RenderObject construction depends on a
///   scope-coupled provider chain (`RenderObject.ofScope` builds AttributeProvider /
///   UniformProvider over `scope`). The round-trip goes:
///       leaf.GetRenderObjects ts
///         → LegacyBridge(leaf, ts) → Ag dispatch → LegacyBridgeSem.RenderObjects
///         → leaf?RenderObjects(scope)
///   Ag's `DefaultBinder.SelectMethod` picks the most-specific concrete-type rule, so
///   the existing `RenderObjectSem.RenderObjects(r : Sg.RenderNode, scope)` handler
///   wins over the generic `SimpleSgSemantics` rule — no infinite loop.
[<AbstractClass; Sealed>]
type SimpleDispatch private () =
    static member Get (sg : ISg, ts : TraversalState) : aset<IRenderObject> =
        match sg with
        | :? ISimpleSg as s -> s.GetRenderObjects ts
        | _                 -> (LegacyAdapter(sg) :> ISimpleSg).GetRenderObjects ts

    static member Bridge (sg : ISg, ts : TraversalState) : aset<IRenderObject> =
        (LegacyAdapter(sg) :> ISimpleSg).GetRenderObjects ts
