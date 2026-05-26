namespace Aardvark.SceneGraph.Simple

// ISimpleSg — explicit, Ag-free scene-graph interface for render-object construction.
// A node yields its `aset<IRenderObject>` given an explicit `TraversalState` (the
// analogue of an Ag.Scope). Composition is natural ASet.collect; there's no caching
// because the adaptive system already does the delta plumbing.
//
// This file ships THREE starter nodes (group / trafo applicator / render-object leaf).
// More node kinds (uniform applicator, surface applicator, state setters …) follow as
// the design proves out. The two bridges to/from the legacy Ag world live in Bridges.fs.

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


// ── starter nodes ─────────────────────────────────────────────────────────

/// Reactive group: union of children's render objects under the inherited TraversalState.
/// Adds/removes in `children` and in any child's emitted aset propagate naturally through
/// `ASet.collect`.
type SimpleGroup(children : aset<ISimpleSg>) =
    member _.Children = children
    interface ISimpleSg with
        member _.GetRenderObjects ts =
            children |> ASet.collect (fun c -> c.GetRenderObjects ts)


/// Pushes a model-trafo onto the TraversalState stack before delegating to a single child.
/// Matches Ag's TrafoApplicator semantics — the trafo is prepended (child's own trafo first
/// in the stack) so flattenStack reproduces the existing compose order.
type SimpleTrafoApplicator(trafo : aval<Trafo3d>, child : ISimpleSg) =
    member _.Trafo = trafo
    member _.Child = child
    interface ISimpleSg with
        member _.GetRenderObjects ts =
            child.GetRenderObjects (TraversalState.pushModelTrafo trafo ts)


/// Leaf node holding a pre-computed `aset<IRenderObject>`. The TraversalState is ignored
/// — the caller is assumed to have already baked any inherited state into the ROs (or
/// this leaf wraps ROs that don't need any inherited state, e.g. fully-self-contained
/// indirect-multidraw buckets produced by Heap.ofRenderObjects).
type SimpleRenderObjects(ros : aset<IRenderObject>) =
    member _.RenderObjects = ros
    interface ISimpleSg with
        member _.GetRenderObjects _ = ros
