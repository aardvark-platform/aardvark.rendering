namespace Aardvark.SceneGraph

open Aardvark.SceneGraph.Semantics

open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Base.Ag
open FSharp.Data.Adaptive

[<AbstractClass; Sealed; Extension>]
type SceneGraphRuntimeExtensions private() =

    static let toRenderObjects (runtime : IRuntime) (sg : ISg) =
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
