namespace Aardvark.SceneGraph.Simple

// RenderObjectBuilder — explicit-traversal analogue of
// `Aardvark.SceneGraph.Semantics.RenderObject.ofScope`. Builds a `RenderObject`
// from a `TraversalState` without ever touching Ag.Scope. Each field copy mirrors
// the corresponding `RenderObject.ofScope` line so a TS-built RO is observationally
// identical to a scope-built one (assuming the TS came from the same node tree).
//
// The two providers it sets up:
//   • `AttributeProvider.ofMap` — wraps the TS's Map<Symbol, BufferView> directly
//     (no scope-coupled lookup).
//   • `TraversalStateUniformProvider` — same protocol as the legacy
//     `Providers.UniformProvider`, but the derived-uniform fallback reads from TS
//     fields (ModelTrafo / ViewTrafo / ProjTrafo / CameraLocation / LightLocation /
//     RcpViewportSize) instead of `scope.TryGetAttributeValueV`. Derived combos
//     (ModelViewTrafo, NormalMatrix, …) are computed by the rendering backends via
//     `Aardvark.Rendering.Uniforms.tryGetDerivedUniform` over THIS provider, so we
//     only need to expose the leaves the table multiplies through.

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System
open System.Collections.Generic


[<AutoOpen>]
module internal UniformProviderInternals =

    /// Same fall-through behaviour as the legacy `Providers.UniformProvider`
    /// (Aardvark.SceneGraph.Core.Core.fs), but the Ag-attribute escape is replaced
    /// by a TraversalState-driven `tryDerive`. Cached per Symbol like the original.
    type TraversalStateUniformProvider(ts : TraversalState,
                                       attributeProviders : list<IAttributeProvider>) =
        let cache = SymbolDict<IAdaptiveValue>()
        let uniforms = ts.Uniforms

        // Lazy because composedModelTrafo allocates an aval chain.
        let modelTrafo = lazy (TraversalState.composedModelTrafo ts)

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
            | "ViewTrafo"       -> ts.ViewTrafo :> IAdaptiveValue |> ValueSome
            | "ProjTrafo"       -> ts.ProjTrafo :> IAdaptiveValue |> ValueSome
            | "CameraLocation"  -> ts.CameraLocation :> IAdaptiveValue |> ValueSome
            | "LightLocation"   ->
                // Matches `EnvironmentSemantics.LightLocation` in Semantics/Environment.fs:23.
                ts.CameraLocation :> IAdaptiveValue |> ValueSome
            | "ViewportSize" ->
                match viewportSize.Value with
                | ValueSome v -> v :> IAdaptiveValue |> ValueSome
                | ValueNone   -> ValueNone
            | "RcpViewportSize" ->
                match rcpViewportSize.Value with
                | ValueSome v -> v :> IAdaptiveValue |> ValueSome
                | ValueNone   -> ValueNone
            | _ -> ValueNone

        /// Used for `HasFoo` queries when `Foo` is a vertex/instance attribute but
        /// not a uniform — mirrors `contains` in Core/Core.fs:108.
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
                            // HasX prefix — same shape as Core/Core.fs:160.
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


module PipelineState =

    /// TS-direct analogue of `Aardvark.SceneGraph.Semantics.PipelineState.ofScope`,
    /// used by leaves like `LodTreeNode` that need to bundle pipeline state alongside
    /// a custom RO type. Mirrors the scope-based builder field-by-field.
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

            GlobalUniforms      = new TraversalStateUniformProvider(ts, [attributes]) :> IUniformProvider
            PerGeometryUniforms = Map.empty
        }


module RenderObjectBuilder =

    /// Build a `RenderObject` from a `TraversalState`. Mirrors
    /// `Aardvark.SceneGraph.Semantics.RenderObject.ofScope` field-by-field, with
    /// scope-coupled providers swapped out for TS-coupled ones.
    let ofTraversalState (ts : TraversalState) : RenderObject =
        let rj = RenderObject()

        // No scope to attach — the rendering backends only need it for the legacy
        // UniformProvider's derived lookups, which the TS provider already covers.
        rj.AttributeScope <- Ag.Scope.Root

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
        rj.Uniforms <- new TraversalStateUniformProvider(ts, [attributes]) :> IUniformProvider

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
