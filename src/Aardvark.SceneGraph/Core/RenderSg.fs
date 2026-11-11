namespace Aardvark.SceneGraph

// The direct render path: IRenderSg, the RenderObject builder over a TraversalState,
// and the RenderSg dispatch helper with the bridge back into the attribute grammar (Ag).
//
// IRenderSg is the explicit, Ag-free scene-graph interface for render-object
// construction. A node yields its `aset<IRenderObject>` for an explicit `TraversalState`
// (the analogue of an Ag.Scope). Composition is plain ASet.collect; there is no caching
// because the adaptive system already tracks the deltas. All `Sg.*` node types implement
// it next to their existing `ISg` (same data, same constructor, both protocols), so trees
// built with `Sg.*` work on both paths unchanged.
//
// RenderObject.ofTraversalState is the analogue of
// `Aardvark.SceneGraph.Semantics.RenderObject.ofScope`. It builds a `RenderObject` from a
// `TraversalState` without touching an Ag.Scope. Each field copy mirrors the
// corresponding line of `RenderObject.ofScope`, so a render object built from a state is
// observationally identical to one built from a scope of the same node tree. Attributes
// come from `AttributeProvider.ofMap` over the state's maps, uniforms from
// `TraversalState.UniformProvider`.
//
// RenderSg is the dispatch helper used by the IRenderSg implementation of every Sg.X node
// in Core/Sg.fs, plus the bridge that lets an IRenderSg parent consume a legacy `ISg`
// (one that does not implement IRenderSg) or a leaf whose render objects are still built
// by its Ag rule. The bridge is an Ag node whose rules seed every TraversalState field
// onto the child's scope, so the child's own Ag rules see the same attributes they would
// in a plain Ag traversal. The reverse direction (an IRenderSg node inside a legacy Ag
// tree) needs no bridge: every IRenderSg node keeps its Ag rules.
//
// Compiled before Core/Sg.fs and Semantics/*. The bridge only writes attributes and
// never reads the scope, so nothing here depends on the scope extension members of
// Semantics.

open System
open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering
open FSharp.Data.Adaptive

/// A scene-graph node that emits its render objects directly, without going through
/// Ag attribute resolution. `GetRenderObjects ts` must be a pure function of `ts` and
/// the node's own data; composition and delta tracking are ASet's job, so
/// implementations do not memoize.
type IRenderSg =
    inherit ISg
    abstract member GetRenderObjects : TraversalState -> aset<IRenderObject>


[<RequireQualifiedAccess>]
module RenderSg =

    /// Ag node carrying the child and the state so that its rules can seed the state's
    /// values onto the child's scope. An IApplicator, so the generic RenderObjects rule
    /// for applicators (Semantics/RenderObject.fs) forwards to the child.
    type private LegacyBridge(child : ISg, ts : TraversalState) =
        let child = AVal.constant child
        member _.Child = child
        member _.TS    = ts
        interface IApplicator with
            member _.Child = child

    // CameraLocation and Runtime have no root seeder in the attribute grammar: the
    // former exists only below a view trafo applicator, the latter only when the entry
    // point seeded it. An inherit rule that does not assign its attribute makes Ag warn
    // on every lookup, so each of them is seeded by its own node that `bridge` inserts
    // only when the state has a value.

    /// Seeds the ambient Runtime onto its child's scope.
    type private RuntimeNode(child : ISg, runtime : IRuntime) =
        let child = AVal.constant child
        member _.Child   = child
        member _.Runtime = runtime
        interface IApplicator with
            member _.Child = child

    /// Seeds CameraLocation onto its child's scope.
    type private CameraLocationNode(child : ISg, cameraLocation : aval<V3d>) =
        let child = AVal.constant child
        member _.Child          = child
        member _.CameraLocation = cameraLocation
        interface IApplicator with
            member _.Child = child

    /// Ag rules seeding every TraversalState field onto the child of a LegacyBridge.
    /// Same shape as the `t.Child?ModelTrafoStack &lt;- ...` rule for `TrafoApplicator`
    /// in Semantics/Trafo.fs and its siblings: one method per field.
    [<Rule>]
    type private LegacyBridgeSem() =

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
        member x.Activate(a : LegacyBridge, _ : Scope) = a.Child?Activate <- a.TS.Activate

        // Optional attributes, seeded by their own nodes.
        member x.Runtime       (a : RuntimeNode, _ : Scope)        = a.Child?Runtime        <- a.Runtime
        member x.CameraLocation(a : CameraLocationNode, _ : Scope) = a.Child?CameraLocation <- a.CameraLocation

    /// Builds the render objects of `sg` through the attribute grammar. Used for a legacy
    /// `ISg` (one without IRenderSg) and by the leaf nodes whose render objects are still
    /// built by an Ag rule that reads the scope: DelayNode, an AdapterNode wrapping a
    /// plain object, LodNode, RuntimeCommandNode, AirNode, InstancingNode and PointCloud.
    /// The call goes
    ///     leaf.GetRenderObjects ts
    ///       -> LegacyBridge(leaf, ts) -> Ag dispatch
    ///       -> RenderObjectSem.RenderObjects(IApplicator) -> leaf?RenderObjects(scope)
    /// Ag picks the rule for the most specific concrete type, so `leaf?RenderObjects(scope)`
    /// reaches the leaf's own Ag rule (e.g. `DelaySem.RenderObjects(n : Sg.DelayNode, scope)`)
    /// and does not come back here.
    let bridge (sg : ISg) (ts : TraversalState) : aset<IRenderObject> =
        // A direct Ag query: the ISg.RenderObjects(scope) extension member in
        // Semantics/RenderObject.fs is compiled later, so it cannot be used here.
        let sg =
            match ts.CameraLocation with
            | Some location -> CameraLocationNode(sg, location) :> ISg
            | None          -> sg
        let sg =
            if isNull ts.Runtime then sg
            else RuntimeNode(sg, ts.Runtime) :> ISg
        let bridge = LegacyBridge(sg, ts) :> ISg
        bridge?RenderObjects(Ag.Scope.Root)

    /// Render objects of `sg` for the state `ts`: the direct path if `sg` is an
    /// IRenderSg, otherwise `bridge`. Used by every applicator-style Sg.* node.
    let get (sg : ISg) (ts : TraversalState) : aset<IRenderObject> =
        match sg with
        | :? IRenderSg as s -> s.GetRenderObjects ts
        | _                 -> bridge sg ts


[<AutoOpen>]
module RenderObjectExtensions =

    // Render objects built from a TraversalState have no Ag scope of their own. The
    // one ambient attribute that uniform providers still read from the scope they are
    // queried with is `Runtime` (Providers.RuntimeDependentUniformHolder), so they get
    // a minimal scope carrying the runtime, one per runtime.
    let private runtimeScopes = System.Runtime.CompilerServices.ConditionalWeakTable<IRuntime, Ag.Scope>()

    let private scopeFor (runtime : IRuntime) =
        if isNull runtime then Ag.Scope.Root
        else
            runtimeScopes.GetValue(runtime, fun r ->
                let holder = obj()
                holder?Runtime <- r
                Ag.Scope.Root.GetChildScope holder
            )

    module RenderObject =

        /// Builds a `RenderObject` from a `TraversalState`. Mirrors
        /// `Aardvark.SceneGraph.Semantics.RenderObject.ofScope` field by field, with the
        /// scope-based attribute and uniform providers replaced by state-based ones.
        let ofTraversalState (ts : TraversalState) : RenderObject =
            let rj = RenderObject()

            rj.AttributeScope <- scopeFor ts.Runtime

            rj.Indices       <- ts.VertexIndexBuffer
            rj.IsActive      <- ts.IsActive
            rj.RenderPass    <- ts.RenderPass
            rj.IsTransparent <- ts.IsTransparent

            if not ts.Activate.IsEmpty then
                let activate () =
                    let disp = ts.Activate |> List.map (fun a -> a())
                    { new IDisposable with
                        member x.Dispose() = disp |> List.iter Disposable.dispose }
                rj.Activate <- activate

            let vertexAttributes   = AttributeProvider.ofMap ts.VertexAttributes
            let instanceAttributes = AttributeProvider.ofMap ts.InstanceAttributes
            rj.VertexAttributes   <- vertexAttributes
            rj.InstanceAttributes <- instanceAttributes

            let attributes = AttributeProvider.union vertexAttributes instanceAttributes
            rj.Uniforms <- new TraversalState.UniformProvider(ts, [attributes]) :> IUniformProvider

            rj.Surface <- ts.Surface

            rj.BlendState <-
                {
                    Mode                = ts.BlendMode
                    ColorWriteMask      = ts.ColorWriteMask
                    ConstantColor       = ts.BlendConstant
                    AttachmentMode      = ts.AttachmentBlendMode
                    AttachmentWriteMask = ts.AttachmentColorWriteMask
                }
            rj.DepthState <-
                {
                    Test      = ts.DepthTest
                    Bias      = ts.DepthBias
                    WriteMask = ts.DepthWriteMask
                    Clamp     = ts.DepthClamp
                }
            rj.StencilState <-
                {
                    ModeFront      = ts.StencilModeFront
                    WriteMaskFront = ts.StencilWriteMaskFront
                    ModeBack       = ts.StencilModeBack
                    WriteMaskBack  = ts.StencilWriteMaskBack
                }
            rj.RasterizerState <-
                {
                    CullMode           = ts.CullMode
                    FrontFacing        = ts.FrontFacing
                    FillMode           = ts.FillMode
                    Multisample        = ts.Multisample
                    ConservativeRaster = ts.ConservativeRaster
                }
            rj.ViewportState <-
                {
                    Viewport = ts.Viewport
                    Scissor  = ts.Scissor
                } : ViewportState

            rj
