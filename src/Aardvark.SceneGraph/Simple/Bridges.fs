namespace Aardvark.SceneGraph.Simple

// Bridges — late-half. Holds bits that depend on `Semantics/*`. After the global
// flip in `Semantics/RenderObject.fs`, this file is intentionally thin:
//
//   • `Bridge.ofScope` (used by the global flip and by `LegacyAdapter`) lives in
//     `Simple/Legacy.fs` (early) — uses the raw Ag `?` operator, no scope
//     member-extension dependency.
//
//   • `[<Rule>] SimpleSgSemantics` is no longer needed: every `RenderObjects`
//     query now goes through the ISg member-extension / `Semantic.renderObjects`
//     in `Semantics/RenderObject.fs`, which already dispatches to `ISimpleSg`. A
//     residual fallback `?RenderObjects(scope)` on an ISimpleSg would still find
//     its concrete-type rule (e.g. `Sg.RenderNode`), which we keep so the legacy
//     Ag traversal continues to work for any non-flipped consumer.
//
// Kept as a placeholder so future late-half helpers have a home.

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open FSharp.Data.Adaptive
