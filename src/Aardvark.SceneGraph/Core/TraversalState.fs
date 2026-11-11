namespace Aardvark.SceneGraph

// TraversalState is the explicit state that IRenderSg.GetRenderObjects threads through
// the children, the analogue of an Ag.Scope in the attribute grammar (Ag). It carries
// exactly the attributes the Ag rules in Semantics/* carry, with the same field shapes
// (list<IUniformProvider>, Map<Symbol, BufferView>, list<aval<Trafo3d>> for the model
// trafo stack), so RenderSg.bridge can copy the fields onto an Ag scope one by one
// without converting representations.
//
// The defaults match the Root<ISg> seeders of the attribute grammar one-to-one:
//   Trafo.fs        ModelTrafoStack / ViewTrafo / ProjTrafo
//   Uniforms.fs     Uniforms
//   Attributes.fs   VertexAttributes / InstanceAttributes / VertexIndexBuffer / FaceVertexCount
//   Surface.fs      Surface
//   Flags.fs        IsActive / RenderPass / IsTransparent
//   Modes.fs        Blend* / Depth* / Stencil* / Cull/FillMode / FrontFacing /
//                   Multisample / ConservativeRaster / Viewport / Scissor
//   Environment.fs  CameraLocation (no root seeder: None until a view trafo is set)
//   Activate.fs     Activate
//
// `composedModelTrafo` is the equivalent of the Ag's `ModelTrafo` attribute: it collapses
// the stack with the same fold as TrafoSemantics.flattenStack, so the composed result is
// identical to the Ag-derived ModelTrafo.

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

        CameraLocation           : option<aval<V3d>>
        Activate                 : list<unit -> IDisposable>

        /// The ambient `Runtime` attribute: seeded by the render-task entry point
        /// (`app?Runtime &lt;- runtime` in the Ag path, `withRuntime` here) and read by
        /// leaves that need the runtime (for example Text.Sg.Shape, GeometrySetNode,
        /// PointCloud and Providers.RuntimeDependentUniformHolder). `null` until seeded.
        Runtime                  : IRuntime
    }

module TraversalState =

    /// The empty state, matching the Ag's Root&lt;ISg&gt; seeders one-to-one.
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

            CameraLocation           = None
            Activate                 = []

            // IRuntime does not have [<AllowNullLiteral>], so use Unchecked.defaultof:
            // null at runtime, but it bypasses the F# null-literal check.
            Runtime                  = Unchecked.defaultof<IRuntime>
        }

    let inline withRuntime (r : IRuntime) (ts : TraversalState) =
        { ts with Runtime = r }

    // Trafos

    /// Prepends a trafo to the stack, in the same direction as the Ag's
    /// `t.Trafo :: scope.ModelTrafoStack` in Semantics/Trafo.fs.
    let inline pushModelTrafo (t : aval<Trafo3d>) (ts : TraversalState) =
        { ts with ModelTrafoStack = t :: ts.ModelTrafoStack }

    /// Sets the view trafo and derives CameraLocation from it, like the Ag's
    /// CameraLocation rule for Sg.ViewTrafoApplicator in Semantics/Environment.fs.
    let withViewTrafo (v : aval<Trafo3d>) (ts : TraversalState) =
        { ts with
            ViewTrafo      = v
            CameraLocation = Some (v |> AVal.map _.GetViewPosition()) }

    let inline withProjTrafo (p : aval<Trafo3d>) (ts : TraversalState) = { ts with ProjTrafo = p }

    /// Flattens a `list&lt;aval&lt;Trafo3d&gt;&gt;` to one composed aval. A copy of
    /// `TrafoSemantics.flattenStack` (Semantics/Trafo.fs), repeated here because
    /// TraversalState.fs is compiled before Semantics/Trafo.fs. It folds constants the
    /// same way (runs of constants collapse to one AVal.constant), so the result is
    /// identical to the Ag-derived `ModelTrafo`.
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

    /// The composed model trafo, the equivalent of the Ag's `ModelTrafo`: flattens the
    /// `ModelTrafoStack` with `flattenStack`. Constant chains fold to one
    /// `AVal.constant`; otherwise the chain is composed with the `&lt;*&gt;` operator.
    let inline composedModelTrafo (ts : TraversalState) : aval<Trafo3d> =
        flattenStack ts.ModelTrafoStack

    // Providers and attributes

    /// Prepends a uniform provider, matching the Ag's innermost-first ordering of
    /// `Uniforms : list&lt;IUniformProvider&gt;` (Semantics/Uniforms.fs): the provider
    /// closest to the leaf comes first and wins.
    let inline pushUniforms (p : IUniformProvider) (ts : TraversalState) =
        { ts with Uniforms = p :: ts.Uniforms }

    /// Merges vertex attributes into the existing map, matching
    /// `Map.union scope.VertexAttributes v.Values` in Semantics/Attributes.fs.
    let inline mergeVertexAttributes (m : Map<Symbol, BufferView>) (ts : TraversalState) =
        { ts with VertexAttributes = Map.union ts.VertexAttributes m }

    let inline mergeInstanceAttributes (m : Map<Symbol, BufferView>) (ts : TraversalState) =
        { ts with InstanceAttributes = Map.union ts.InstanceAttributes m }

    let inline withVertexIndexBuffer (b : option<BufferView>) (ts : TraversalState) =
        { ts with VertexIndexBuffer = b }

    let inline withFaceVertexCount (c : aval<int>) (ts : TraversalState) =
        { ts with FaceVertexCount = c }

    /// What the Ag rules do for Sg.VertexIndexApplicator: sets the index buffer and
    /// also sets FaceVertexCount to the buffer's count. Mirrors AttributeSem in
    /// Semantics/Attributes.fs.
    let applyVertexIndexBuffer (view : BufferView) (ts : TraversalState) =
        { ts with
            VertexIndexBuffer = Some view
            FaceVertexCount   = BufferView.getCount view }

    /// What the Ag rules do for Sg.VertexAttributeApplicator: merges into the existing
    /// map and, only when no index buffer is set, derives FaceVertexCount from the
    /// Positions buffer if there is one. Mirrors AttributeSem in Semantics/Attributes.fs.
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

    // Flags and surface

    let inline withSurface       s   ts = { ts with Surface = s }
    let inline withIsActive      a   ts = { ts with IsActive = a }
    let inline withTransparent   t   ts = { ts with IsTransparent = t }
    let inline withRenderPass    p   ts = { ts with RenderPass = p }

    /// What the Ag rules do for Sg.OnOffNode: combines `IsActive` with the current
    /// value by logical and, with the same constant folding as the Ag's `&lt;&amp;&gt;`
    /// operator (Semantics/Flags.fs).
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

    // Blend, depth, stencil, rasterizer and viewport: plain value replacements

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

    let inline withCameraLocation (v : aval<V3d>) (ts : TraversalState) = { ts with CameraLocation = Some v }

    /// Prepend an activation callback (Semantics/Activate.fs seeds `[]`).
    let inline pushActivate (a : unit -> IDisposable) (ts : TraversalState) =
        { ts with Activate = a :: ts.Activate }

    /// Uniform provider over a TraversalState. Same lookup order as the scope-based
    /// `Providers.UniformProvider` (Core/Core.fs), but the fallback for attribute-derived
    /// uniforms (ModelTrafo, ModelTrafoStack, ViewTrafo, ProjTrafo, CameraLocation,
    /// LightLocation, ViewportSize, RcpViewportSize) reads from the state instead of
    /// `scope.TryGetAttributeValueV`. Derived combinations (ModelViewTrafo, NormalMatrix,
    /// ...) are computed by the rendering backends through
    /// `Aardvark.Rendering.Uniforms.tryGetDerivedUniform` over this provider, so it only
    /// exposes the inputs that table multiplies. Cached per Symbol like the original.
    type internal UniformProvider(ts : TraversalState, attributeProviders : list<IAttributeProvider>) =
        let cache = SymbolDict<IAdaptiveValue>()
        let uniforms = ts.Uniforms

        // Lazy because composedModelTrafo allocates an aval chain.
        let modelTrafo = lazy (composedModelTrafo ts)

        // The unfolded model trafo stack as a constant array, for consumers that compose
        // the chain themselves (e.g. on the GPU) instead of reading the folded ModelTrafo.
        let modelTrafoStack = lazy (AVal.constant (List.toArray ts.ModelTrafoStack))

        // ViewportSize / RcpViewportSize derived from the optional Viewport box.
        let viewportSize : Lazy<aval<V2i> voption> =
            lazy (
                match ts.Viewport with
                | Some vp -> vp |> AVal.map (fun (b : Box2i) -> b.Size) |> ValueSome
                | None    -> ValueNone
            )

        let rcpViewportSize : Lazy<aval<V2d> voption> =
            lazy (
                match ts.Viewport with
                | Some vp -> vp |> AVal.map (fun (b : Box2i) -> 1.0 / V2d b.Size) |> ValueSome
                | None    -> ValueNone
            )

        let tryDerive (name : string) : IAdaptiveValue voption =
            match name with
            | "ModelTrafo"      -> modelTrafo.Value :> IAdaptiveValue |> ValueSome
            | "ModelTrafoStack" -> modelTrafoStack.Value :> IAdaptiveValue |> ValueSome
            | "ViewTrafo"       -> ts.ViewTrafo :> IAdaptiveValue |> ValueSome
            | "ProjTrafo"       -> ts.ProjTrafo :> IAdaptiveValue |> ValueSome
            | "CameraLocation" | "LightLocation" ->
                // LightLocation is the camera location (Semantics/Environment.fs); both are
                // absent until a view trafo is set, like in the attribute grammar.
                match ts.CameraLocation with
                | Some v -> v :> IAdaptiveValue |> ValueSome
                | None   -> ValueNone
            | "ViewportSize" ->
                match viewportSize.Value with
                | ValueSome v -> v :> IAdaptiveValue |> ValueSome
                | ValueNone   -> ValueNone
            | "RcpViewportSize" ->
                match rcpViewportSize.Value with
                | ValueSome v -> v :> IAdaptiveValue |> ValueSome
                | ValueNone   -> ValueNone
            | _ -> ValueNone

        /// Used for `HasFoo` queries when `Foo` is a vertex or instance attribute but
        /// not a uniform; mirrors `contains` in Core/Core.fs.
        let attributeContains (s : Symbol) : bool =
            let picked =
                attributeProviders |> List.tryPickV (fun p ->
                    match p.TryGetAttribute s with
                    | ValueSome v -> ValueSome (not v.IsSingleValue)
                    | ValueNone   -> ValueNone)
            match picked with
            | ValueSome v -> v
            | ValueNone   -> false

        interface IUniformProvider with
            member x.Dispose() = cache.Clear()

            member x.TryGetUniform(scope, s : Symbol) =
                let str = s.ToString()
                match cache.TryGetValue s with
                | (true, m) -> ValueSome m
                | _ ->
                    match uniforms |> List.tryPickV (fun u -> u.TryGetUniform(scope, s)) with
                    | ValueSome v ->
                        cache.Add(s, v)
                        ValueSome v
                    | ValueNone ->
                        match tryDerive str with
                        | ValueSome v ->
                            cache.Add(s, v)
                            ValueSome v
                        | ValueNone ->
                            // "Has" prefix, same as Providers.UniformProvider in Core/Core.fs.
                            if str.StartsWith("Has") then
                                let baseName = str.Substring(3)
                                let baseSym  = Symbol.Create baseName
                                let baseUniform = (x :> IUniformProvider).TryGetUniform(scope, baseSym)
                                let result =
                                    match baseUniform with
                                    | ValueSome v -> NullResources.isValidResourceAdaptive v :> IAdaptiveValue
                                    | ValueNone   -> attributeContains baseSym |> AVal.constant :> IAdaptiveValue
                                cache.Add(s, result)
                                ValueSome result
                            else
                                ValueNone


[<AutoOpen>]
module TraversalStateExtensions =

    module PipelineState =

        /// The direct render path's analogue of
        /// `Aardvark.SceneGraph.Semantics.PipelineState.ofScope`, used by leaves like
        /// `LodTreeNode` that bundle the pipeline state with a custom render object type.
        /// Mirrors the scope-based builder field by field.
        let ofTraversalState (ts : TraversalState) : PipelineState =
            let vertexAttributes   = AttributeProvider.ofMap ts.VertexAttributes
            let instanceAttributes = AttributeProvider.ofMap ts.InstanceAttributes
            let attributes = AttributeProvider.union vertexAttributes instanceAttributes

            {
                Mode                = IndexedGeometryMode.PointList
                VertexInputTypes    = Map.empty

                BlendState =
                    {
                        Mode                = ts.BlendMode
                        ColorWriteMask      = ts.ColorWriteMask
                        ConstantColor       = ts.BlendConstant
                        AttachmentMode      = ts.AttachmentBlendMode
                        AttachmentWriteMask = ts.AttachmentColorWriteMask
                    }
                DepthState =
                    {
                        Test      = ts.DepthTest
                        Bias      = ts.DepthBias
                        WriteMask = ts.DepthWriteMask
                        Clamp     = ts.DepthClamp
                    }
                StencilState =
                    {
                        ModeFront      = ts.StencilModeFront
                        WriteMaskFront = ts.StencilWriteMaskFront
                        ModeBack       = ts.StencilModeBack
                        WriteMaskBack  = ts.StencilWriteMaskBack
                    }
                RasterizerState =
                    {
                        CullMode           = ts.CullMode
                        FrontFacing        = ts.FrontFacing
                        FillMode           = ts.FillMode
                        Multisample        = ts.Multisample
                        ConservativeRaster = ts.ConservativeRaster
                    }
                ViewportState =
                    {
                        Viewport = ts.Viewport
                        Scissor  = ts.Scissor
                    } : ViewportState

                GlobalUniforms      = new TraversalState.UniformProvider(ts, [attributes]) :> IUniformProvider
                PerGeometryUniforms = Map.empty
            }
