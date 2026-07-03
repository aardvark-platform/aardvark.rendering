namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive

[<RequireQualifiedAccess>]
type RuntimeCommand =
    | EmptyCmd
    | RenderCmd of objects : aset<IRenderObject>
    | OrderedCmd of commands : alist<RuntimeCommand>
    | IfThenElseCmd of condition : aval<bool> * ifTrue : RuntimeCommand * ifFalse : RuntimeCommand
    | ClearCmd of values : aval<ClearValues>
    | DispatchCmd of shader : IComputeShader * groups : aval<V3i> * arguments : Map<string, obj>
    | GeometriesCmd of surface : Surface * pipeline : PipelineState * geometries : aset<Geometry>
    | LodTreeCmd of surface : Surface * pipeline : PipelineState * geometries : LodTreeLoader<Geometry>
    | GeometriesSimpleCmd of effect : FShade.Effect * pipeline : PipelineState * geometries : aset<IndexedGeometry>

    static member Empty = RuntimeCommand.EmptyCmd

    static member Render(objects : aset<IRenderObject>) =
        RuntimeCommand.RenderCmd(objects)

    static member Dispatch(shader : IComputeShader, groups : aval<V3i>, arguments : Map<string, obj>) =
        RuntimeCommand.DispatchCmd(shader, groups, arguments)

    static member Clear(values : aval<ClearValues>) =
        RuntimeCommand.ClearCmd(values)

    static member Ordered(commands : alist<RuntimeCommand>) =
        RuntimeCommand.OrderedCmd(commands)

    static member IfThenElse(condition : aval<bool>, ifTrue : RuntimeCommand, ifFalse : RuntimeCommand) =
        RuntimeCommand.IfThenElseCmd(condition, ifTrue, ifFalse)

    static member Geometries(surface : Surface, pipeline : PipelineState, geometries : aset<Geometry>) =
        RuntimeCommand.GeometriesCmd(surface, pipeline, geometries)

    static member Geometries(surface : FShade.Effect, pipeline : PipelineState, geometries : aset<IndexedGeometry>) =
        RuntimeCommand.GeometriesSimpleCmd(surface, pipeline, geometries)

    static member LodTree(surface : Surface, pipeline : PipelineState, geometries : LodTreeLoader<Geometry>) =
        RuntimeCommand.LodTreeCmd(surface, pipeline, geometries)

type CommandRenderObject(pass : RenderPass, scope : Ag.Scope, command : RuntimeCommand) =
    let id = RenderObjectId.New()

    member x.Id = id
    member x.RenderPass = pass
    member x.AttributeScope = scope
    member x.Command = command

    interface IRenderObject with
        member x.Id = id
        member x.RenderPass = pass
        member x.AttributeScope = scope

    override x.GetHashCode() = id.GetHashCode()
    override x.Equals o =
        match o with
            | :? CommandRenderObject as o -> id = o.Id
            | _ -> false

/// A GPU-heap render unit the backend records ATOMICALLY into one command buffer:
/// every DERIVE dispatch (a compute pre-pass, lifted before BeginRenderPass) → one
/// compute→vertex memory barrier → every DRAW (inside the render pass). Each derive
/// writes its page's arena and the single barrier covers all the draws, so a page is
/// always drawn against its OWN fresh derive (no page>0 staleness) and no render-task
/// split (e.g. Aardvark.Dom's pickable/non-pickable tasks) can separate the derive from
/// the draws it feeds. The whole unit is one submission — the compute and the graphics
/// share a command buffer instead of a synchronous compute submit.
/// Vulkan-only; other backends fall back to rendering `Draws` (the derive must then be
/// produced some other way).
type HeapRenderObject(pass : RenderPass, scope : Ag.Scope,
                      derives : list<IComputeShader * aval<V3i> * Map<string, obj>>,
                      draws : list<IRenderObject>) =
    let id = RenderObjectId.New()

    member x.Id = id
    member x.RenderPass = pass
    member x.AttributeScope = scope
    /// per-page derive dispatches (shader, group count, prepared input binding via "__input")
    member x.Derives = derives
    /// the page draws that read the derive outputs (recorded after the barrier)
    member x.Draws = draws

    /// True when this bucket's geometry is transparent. The page draws are clones
    /// of the bucket's representative RenderObject (RenderObject.Clone copies
    /// IsTransparent), so the flag survives on every draw — this surfaces it on the
    /// bundle so the OIT router (TransparencyRenderTask) can see through the wrapper
    /// and route the whole heap unit through the transparency pipeline.
    member x.IsTransparent =
        match draws with
        | (:? RenderObject as r) :: _ -> r.IsTransparent
        | _ -> false

    /// True when this bucket carries the per-slot pick write (set by the heap's picking
    /// path). dom's SceneHandler routes a pickable bundle into the PickId-attachment pass
    /// so the composed per-slot pick write actually reaches the pick buffer.
    member val IsPickable = false with get, set

    interface IRenderObject with
        member x.Id = id
        member x.RenderPass = pass
        member x.AttributeScope = scope

    override x.GetHashCode() = id.GetHashCode()
    override x.Equals o =
        match o with
            | :? HeapRenderObject as o -> id = o.Id
            | _ -> false

/// A render object that carries NOTHING to render — backends ignore it for
/// drawing and only invoke its `Activate`, disposing the returned handle when
/// the object leaves the render task. Lets a producer (e.g. the GPU heap) scope
/// resource lifetime to the union of tasks rendering it: build on first activate,
/// tear down when the last task drops it.
type ActivationRenderObject(pass : RenderPass, scope : Ag.Scope, activate : unit -> System.IDisposable) =
    let id = RenderObjectId.New()

    member x.Id = id
    member x.RenderPass = pass
    member x.AttributeScope = scope
    member x.Activate() = activate()

    interface IRenderObject with
        member x.Id = id
        member x.RenderPass = pass
        member x.AttributeScope = scope

    override x.GetHashCode() = id.GetHashCode()
    override x.Equals o =
        match o with
            | :? ActivationRenderObject as o -> id = o.Id
            | _ -> false

/// A render object whose REAL render objects depend on the framebuffer signature it is compiled
/// into. `CompileRender` expands it — `ASet.collect Expand renderPass` — BEFORE the normal per-RO
/// prepare, so the command task never sees it (unlike ActivationRenderObject, no backend command
/// handling is needed). Lets signature-dependent nodes (e.g. the GPU heap) defer their
/// signature-bound construction (attribute-DCE, arena layout, gather) to compile time instead of
/// guessing a signature up front. `IsTransparent` is known eagerly so the OIT split (which runs on
/// the UN-expanded set, `TransparencyRenderTask.isTransparent`) routes each variant correctly.
type SignatureDependentRenderObject(pass : RenderPass, scope : Ag.Scope, isTransparent : bool, expand : IFramebufferSignature -> aset<IRenderObject>) =
    let id = RenderObjectId.New()

    member x.Id = id
    member x.RenderPass = pass
    member x.AttributeScope = scope
    member x.IsTransparent = isTransparent
    member x.Expand (signature : IFramebufferSignature) : aset<IRenderObject> = expand signature

    interface IRenderObject with
        member x.Id = id
        member x.RenderPass = pass
        member x.AttributeScope = scope

    override x.GetHashCode() = id.GetHashCode()
    override x.Equals o =
        match o with
            | :? SignatureDependentRenderObject as o -> id = o.Id
            | _ -> false
