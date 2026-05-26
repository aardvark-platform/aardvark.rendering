namespace Aardvark.SceneGraph

open Aardvark.SceneGraph.Semantics
open Aardvark.SceneGraph.Simple

open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Base.Ag
open FSharp.Data.Adaptive

[<AbstractClass; Sealed; Extension>]
type SceneGraphRuntimeExtensions private() =

    static let toRenderObjects (runtime : IRuntime) (sg : ISg) =
        // ISimpleSg-direct entry when `SimpleConfig.Enabled` is true: seed Runtime
        // on the TraversalState (the one ambient Ag attribute used in production —
        // Text.ShapeSem / PointCloud / RuntimeDependentUniformHolder) and dispatch
        // GetRenderObjects without touching Ag. Off by default — the legacy
        // `app?Runtime <- runtime` path is still the production route while leaves
        // get ported to TS-direct.
        match sg with
        | :? ISimpleSg as s when SimpleConfig.Enabled ->
            s.GetRenderObjects (TraversalState.withRuntime runtime TraversalState.empty)
        | _ ->
            let app = Sg.DynamicNode(AVal.constant sg)
            app?Runtime <- runtime
            app.RenderObjects(Ag.Scope.Root)

    [<Extension>]
    static member CompileRender(this : IRuntime, signature : IFramebufferSignature, sg : ISg) =
        let ro = sg |> toRenderObjects this
        this.CompileRender(signature, ro)

[<AutoOpen>]
module RuntimeSgExtensions =
    module Sg =

        let compile (runtime : IRuntime) (signature : IFramebufferSignature) (sg : ISg) =
            runtime.CompileRender(signature, sg)
