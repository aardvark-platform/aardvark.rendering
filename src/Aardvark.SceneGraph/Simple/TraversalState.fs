namespace Aardvark.SceneGraph.Simple

// TraversalState — the explicit, threaded-through-children analogue of an Ag.Scope for
// ISimpleSg's GetRenderObjects. Carries EXACTLY the attributes the Ag carries in
// src/Aardvark.SceneGraph/Semantics/* (same field shapes — list<IUniformProvider>,
// Map<Symbol, BufferView>, list<aval<Trafo3d>> for the model-trafo stack — so bridges
// to/from Ag.Scope are field-by-field copies, no representation conversion).
//
// Defaults match every Root<ISg> seeder one-to-one:
//   Trafo.fs:55-75       ModelTrafoStack / ViewTrafo / ProjTrafo
//   Uniforms.fs:19-20    Uniforms
//   Attributes.fs:29-53  VertexAttributes / InstanceAttributes / VertexIndexBuffer / FaceVertexCount
//   Surface.fs:23-24     Surface
//   Flags.fs:48-71       IsActive / RenderPass / IsTransparent
//   Modes.fs:72-154      Blend* / Depth* / Stencil* / Cull/FillMode / FrontFacing /
//                        Multisample / ConservativeRaster / Viewport / Scissor
//   Activate.fs:21-22    Activate
//
// `composedModelTrafo` is the equivalent of Ag's `ModelTrafo` attribute — collapses the
// stack via TrafoSemantics.flattenStack (the same fold the Ag uses, so the composed
// result is bit-identical to today's Ag-derived ModelTrafo).

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.TrafoOperators
open FSharp.Data.Adaptive
open System

type TraversalState =
    {
        ModelTrafoStack          : list<aval<Trafo3d>>
        ViewTrafo                : aval<Trafo3d>
        ProjTrafo                : aval<Trafo3d>

        Uniforms                 : list<IUniformProvider>
        VertexAttributes         : Map<Symbol, BufferView>
        InstanceAttributes       : Map<Symbol, BufferView>
        VertexIndexBuffer        : option<BufferView>
        FaceVertexCount          : aval<int>

        Surface                  : Surface
        IsActive                 : aval<bool>
        IsTransparent            : bool
        RenderPass               : RenderPass

        BlendMode                : aval<BlendMode>
        BlendConstant            : aval<C4f>
        ColorWriteMask           : aval<ColorMask>
        AttachmentBlendMode      : aval<Map<Symbol, BlendMode>>
        AttachmentColorWriteMask : aval<Map<Symbol, ColorMask>>

        DepthTest                : aval<DepthTest>
        DepthBias                : aval<DepthBias>
        DepthWriteMask           : aval<bool>
        DepthClamp               : aval<bool>

        StencilModeFront         : aval<StencilMode>
        StencilWriteMaskFront    : aval<StencilMask>
        StencilModeBack          : aval<StencilMode>
        StencilWriteMaskBack     : aval<StencilMask>

        CullMode                 : aval<CullMode>
        FrontFacing              : aval<WindingOrder>
        FillMode                 : aval<FillMode>
        Multisample              : aval<bool>
        ConservativeRaster       : aval<bool>
        Viewport                 : option<aval<Box2i>>
        Scissor                  : option<aval<Box2i>>

        CameraLocation           : aval<V3d>
        Activate                 : list<unit -> IDisposable>
    }

module TraversalState =

    /// Empty state matching the Ag's Root<ISg> seeders one-to-one.
    let empty : TraversalState =
        let blend    = BlendState.Default
        let depth    = DepthState.Default
        let stencil  = StencilState.Default
        let raster   = RasterizerState.Default
        let viewport = ViewportState.Default
        {
            ModelTrafoStack          = []
            ViewTrafo                = AVal.constant Trafo3d.Identity
            ProjTrafo                = AVal.constant Trafo3d.Identity

            Uniforms                 = []
            VertexAttributes         = Map.empty
            InstanceAttributes       = Map.empty
            VertexIndexBuffer        = None
            FaceVertexCount          = AVal.constant 0

            Surface                  = Surface.None
            IsActive                 = AVal.constant true
            IsTransparent            = false
            RenderPass               = RenderPass.main

            BlendMode                = blend.Mode
            BlendConstant            = blend.ConstantColor
            ColorWriteMask           = blend.ColorWriteMask
            AttachmentBlendMode      = blend.AttachmentMode
            AttachmentColorWriteMask = blend.AttachmentWriteMask

            DepthTest                = depth.Test
            DepthBias                = depth.Bias
            DepthWriteMask           = depth.WriteMask
            DepthClamp               = depth.Clamp

            StencilModeFront         = stencil.ModeFront
            StencilWriteMaskFront    = stencil.WriteMaskFront
            StencilModeBack          = stencil.ModeBack
            StencilWriteMaskBack     = stencil.WriteMaskBack

            CullMode                 = raster.CullMode
            FrontFacing              = raster.FrontFacing
            FillMode                 = raster.FillMode
            Multisample              = raster.Multisample
            ConservativeRaster       = raster.ConservativeRaster
            Viewport                 = viewport.Viewport
            Scissor                  = viewport.Scissor

            CameraLocation           = AVal.constant V3d.Zero
            Activate                 = []
        }

    // ── trafos ─────────────────────────────────────────────────────────────
    /// Prepend a trafo to the stack — same direction as Ag's
    /// `t.Trafo :: scope.ModelTrafoStack` at Trafo.fs:59.
    let inline pushModelTrafo (t : aval<Trafo3d>) (ts : TraversalState) =
        { ts with ModelTrafoStack = t :: ts.ModelTrafoStack }

    let inline withViewTrafo (v : aval<Trafo3d>) (ts : TraversalState) = { ts with ViewTrafo = v }
    let inline withProjTrafo (p : aval<Trafo3d>) (ts : TraversalState) = { ts with ProjTrafo = p }

    /// Flatten a `list<aval<Trafo3d>>` to one composed aval. Copy of
    /// `TrafoSemantics.flattenStack` (Semantics/Trafo.fs) — we inline it here because
    /// TraversalState.fs is compiled BEFORE Semantics/Trafo.fs; same constant-fold
    /// behaviour (runs of constants collapse to one AVal.constant) so the result is
    /// bit-identical to the Ag-derived `ModelTrafo`.
    let flattenStack (stack : list<aval<Trafo3d>>) : aval<Trafo3d> =
        let rec foldConstants (l : list<aval<Trafo3d>>) =
            match l with
            | [] -> []
            | a :: b :: rest when a.IsConstant && b.IsConstant ->
                let n = (AVal.constant (a.GetValue() * b.GetValue())) :: rest
                foldConstants n
            | a :: rest ->
                a :: foldConstants rest

        match foldConstants stack with
        | [] -> AVal.constant Trafo3d.Identity
        | [a] -> a
        | [a; b] -> a <*> b
        | s -> s |> List.fold (<*>) (AVal.constant Trafo3d.Identity)

    /// The composed model trafo — Ag-equivalent of `ModelTrafo`: flatten the
    /// `ModelTrafoStack` via `flattenStack`. Constant chains fold to one
    /// `AVal.constant`; otherwise the chain composes via the `<*>` operator.
    let inline composedModelTrafo (ts : TraversalState) : aval<Trafo3d> =
        flattenStack ts.ModelTrafoStack

    // ── providers + attributes ─────────────────────────────────────────────
    /// Prepend a uniform provider — matches the Ag's child-first ordering for
    /// `Uniforms : list<IUniformProvider>` (Uniforms.fs).
    let inline pushUniforms (p : IUniformProvider) (ts : TraversalState) =
        { ts with Uniforms = p :: ts.Uniforms }

    /// Merge child VertexAttributes onto the existing map — matches
    /// `Map.union scope.VertexAttributes v.Values` at Attributes.fs:55.
    let inline mergeVertexAttributes (m : Map<Symbol, BufferView>) (ts : TraversalState) =
        { ts with VertexAttributes = Map.union ts.VertexAttributes m }

    let inline mergeInstanceAttributes (m : Map<Symbol, BufferView>) (ts : TraversalState) =
        { ts with InstanceAttributes = Map.union ts.InstanceAttributes m }

    let inline withVertexIndexBuffer (b : option<BufferView>) (ts : TraversalState) =
        { ts with VertexIndexBuffer = b }

    let inline withFaceVertexCount (c : aval<int>) (ts : TraversalState) =
        { ts with FaceVertexCount = c }

    /// Ag-faithful version of Sg.VertexIndexApplicator: sets the index buffer AND
    /// updates FaceVertexCount to the buffer's count. Mirrors AttributeSem in
    /// Semantics/Attributes.fs.
    let applyVertexIndexBuffer (view : BufferView) (ts : TraversalState) =
        { ts with
            VertexIndexBuffer = Some view
            FaceVertexCount   = BufferView.getCount view }

    /// Ag-faithful version of Sg.VertexAttributeApplicator: merges into the existing
    /// map AND — only when no index buffer is set — derives FaceVertexCount from the
    /// Positions buffer if present. Mirrors AttributeSem in Semantics/Attributes.fs.
    let applyVertexAttributes (values : Map<Symbol, BufferView>) (ts : TraversalState) =
        let merged = Map.union ts.VertexAttributes values
        let newCount =
            if ts.VertexIndexBuffer.IsNone then
                match Map.tryFind DefaultSemantic.Positions values with
                | Some positions -> BufferView.getCount positions
                | _ -> ts.FaceVertexCount
            else
                ts.FaceVertexCount
        { ts with VertexAttributes = merged; FaceVertexCount = newCount }

    // ── flags / surface ────────────────────────────────────────────────────
    let inline withSurface       s   ts = { ts with Surface = s }
    let inline withIsActive      a   ts = { ts with IsActive = a }
    let inline withTransparent   t   ts = { ts with IsTransparent = t }
    let inline withRenderPass    p   ts = { ts with RenderPass = p }

    /// Ag-faithful version of Sg.OnOffNode: `IsActive` AND-combines with the current
    /// state, with the same constant-folding the Ag's `<&>` operator does
    /// (Semantics/Flags.fs).
    let andIsActive (active : aval<bool>) (ts : TraversalState) =
        let current = ts.IsActive
        let combined =
            match current.IsConstant, active.IsConstant with
            | true, true ->
                if current.GetValue() && active.GetValue() then AVal.constant true
                else AVal.constant false
            | true, false ->
                if current.GetValue() then active else AVal.constant false
            | false, true ->
                if active.GetValue() then current else AVal.constant false
            | _ ->
                AVal.map2 (&&) current active
        { ts with IsActive = combined }

    // ── blend / depth / stencil / rasterizer / viewport — value replacements ─
    let inline withBlendMode                m (ts : TraversalState) = { ts with BlendMode = m }
    let inline withBlendConstant            c (ts : TraversalState) = { ts with BlendConstant = c }
    let inline withColorWriteMask           m (ts : TraversalState) = { ts with ColorWriteMask = m }
    let inline withAttachmentBlendMode      m (ts : TraversalState) = { ts with AttachmentBlendMode = m }
    let inline withAttachmentColorWriteMask m (ts : TraversalState) = { ts with AttachmentColorWriteMask = m }

    let inline withDepthTest       v (ts : TraversalState) = { ts with DepthTest = v }
    let inline withDepthBias       v (ts : TraversalState) = { ts with DepthBias = v }
    let inline withDepthWriteMask  v (ts : TraversalState) = { ts with DepthWriteMask = v }
    let inline withDepthClamp      v (ts : TraversalState) = { ts with DepthClamp = v }

    let inline withStencilModeFront      v (ts : TraversalState) = { ts with StencilModeFront = v }
    let inline withStencilWriteMaskFront v (ts : TraversalState) = { ts with StencilWriteMaskFront = v }
    let inline withStencilModeBack       v (ts : TraversalState) = { ts with StencilModeBack = v }
    let inline withStencilWriteMaskBack  v (ts : TraversalState) = { ts with StencilWriteMaskBack = v }

    let inline withCullMode            v (ts : TraversalState) = { ts with CullMode = v }
    let inline withFrontFacing         v (ts : TraversalState) = { ts with FrontFacing = v }
    let inline withFillMode            v (ts : TraversalState) = { ts with FillMode = v }
    let inline withMultisample         v (ts : TraversalState) = { ts with Multisample = v }
    let inline withConservativeRaster  v (ts : TraversalState) = { ts with ConservativeRaster = v }
    let inline withViewport            v (ts : TraversalState) = { ts with Viewport = v }
    let inline withScissor             v (ts : TraversalState) = { ts with Scissor = v }

    let inline withCameraLocation v (ts : TraversalState) = { ts with CameraLocation = v }

    /// Prepend an activation callback (Activate.fs:22 seeds `[]`).
    let inline pushActivate (a : unit -> IDisposable) (ts : TraversalState) =
        { ts with Activate = a :: ts.Activate }
