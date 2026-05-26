namespace Aardvark.SceneGraph.Simple

// ISimpleSg — explicit, Ag-free scene-graph interface for render-object construction.
// A node yields its `aset<IRenderObject>` given an explicit `TraversalState` (the
// analogue of an Ag.Scope). Composition is natural ASet.collect; there's no caching
// because the adaptive system already does the delta plumbing.
//
// All legacy `Sg.*` node types implement this directly (next to their existing `ISg`)
// — same data, same constructor, dual-protocol. Trees built with `Sg.*` work in both
// the Ag world and the explicit-traversal world unchanged.

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive

/// A scene-graph node that knows how to emit render objects directly, without going
/// through Ag attribute resolution. `GetRenderObjects ts` must be a pure function of
/// `ts` (and the node's own data) — composition + delta tracking is ASet's job, so no
/// memoization in implementations.
type ISimpleSg =
    inherit ISg
    abstract member GetRenderObjects : TraversalState -> aset<IRenderObject>
