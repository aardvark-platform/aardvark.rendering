namespace Aardvark.SceneGraph.Simple

// Bridges — *late half*. Holds the bits that depend on the scope member-extensions
// defined in `Semantics/*.fs` (so this file must compile AFTER them). The early half
// (LegacyAdapter / LegacyBridge / LegacyBridgeSem / SimpleDispatch) lives in
// Simple/Legacy.fs and is what the legacy `Sg.*` ISimpleSg implementations call into.
//
// What this file adds:
//   • `Bridge.ofScope` — read an Ag.Scope into a TraversalState.
//   • `[<Rule>] SimpleSgSemantics` — Ag rule so an ISimpleSg embedded in a legacy tree
//     yields its RenderObjects via GetRenderObjects(ofScope scope).
//   • `composedModelTrafo` — the Ag-equivalent flattenStack helper for the model-trafo
//     stack (uses TrafoSemantics.flattenStack).

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open FSharp.Data.Adaptive


module Bridge =

    /// Read an Ag.Scope into a TraversalState — each field comes from the corresponding
    /// scope member-extension in Semantics/* (Trafo / Uniforms / Attributes / Surface /
    /// Flags / Modes / Activate).
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

    /// The composed model trafo — Ag-equivalent of `ModelTrafo`: flattens the stack via
    /// `TrafoSemantics.flattenStack`. Constant chains fold to one `AVal.constant`;
    /// otherwise the chain composes via the `<*>` operator the Ag uses.
    let inline composedModelTrafo (ts : TraversalState) : aval<Trafo3d> =
        TrafoSemantics.flattenStack ts.ModelTrafoStack


[<AutoOpen>]
module BridgeSemantics =

    /// Ag rule for `ISimpleSg` in a legacy tree: read the scope into a TraversalState and
    /// dispatch the node's GetRenderObjects. The *Simple-inside-legacy* bridge.
    [<Rule>]
    type SimpleSgSemantics() =
        member x.RenderObjects(s : ISimpleSg, scope : Ag.Scope) : aset<IRenderObject> =
            s.GetRenderObjects (Bridge.ofScope scope)
