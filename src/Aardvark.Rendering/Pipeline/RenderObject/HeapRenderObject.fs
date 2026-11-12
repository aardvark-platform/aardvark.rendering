namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive

/// A render unit of the GPU heap that the backend records as one piece into one
/// command buffer: every derive dispatch (compute, placed before BeginRenderPass),
/// then one compute-to-vertex memory barrier, then every draw (inside the render
/// pass). Each derive writes the data of its page and the barrier covers all the
/// draws, so a page is always drawn against its own fresh derive and no split into
/// several render tasks (for example pickable and non-pickable tasks) can separate
/// the derive from the draws it feeds. The whole unit is one submission; compute and
/// graphics share a command buffer instead of a synchronous compute submission.
/// Vulkan only; other backends fall back to rendering <c>Draws</c>, and the derive
/// must then be produced some other way.
type HeapRenderObject(pass : RenderPass, scope : Ag.Scope,
                      derives : list<IComputeInputBinding * aval<V3i>>,
                      draws : list<IRenderObject>, isPickable : bool) =
    let id = RenderObjectId.New()

    member x.Id = id
    member x.RenderPass = pass
    member x.AttributeScope = scope
    /// per-page derive dispatches (input binding, group count)
    member x.Derives = derives
    /// the page draws that read the derive outputs (recorded after the barrier)
    member x.Draws = draws

    /// True when this bucket's geometry is transparent. The page draws are clones
    /// of the bucket's representative RenderObject (RenderObject.Clone copies
    /// IsTransparent), so the flag is present on every draw; it is surfaced on the
    /// unit so that the TransparencyRenderTask can route the whole unit through the
    /// transparency pass without looking inside.
    member x.IsTransparent =
        match draws with
        | :? RenderObject as r :: _ -> r.IsTransparent
        | _ -> false

    /// True when this bucket's draws write pick ids, so that a pickable bundle can be
    /// routed into a pass with a pick-id attachment.
    member x.IsPickable = isPickable

    interface IRenderObject with
        member x.Id = id
        member x.RenderPass = pass
        member x.AttributeScope = scope

    override x.GetHashCode() = id.GetHashCode()
    override x.Equals o =
        match o with
        | :? HeapRenderObject as o -> id = o.Id
        | _ -> false
