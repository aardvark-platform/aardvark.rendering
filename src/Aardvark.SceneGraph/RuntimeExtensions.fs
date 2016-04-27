namespace Aardvark.SceneGraph

open Aardvark.SceneGraph.Semantics

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Ag
open Aardvark.Base.Incremental

[<AbstractClass; Sealed; Extension>]
type SceneGraphRuntimeExtensions private() =




    [<Extension>]
    static member CompileRender(x : IRuntime, signature : IFramebufferSignature, rjs : aset<IRenderObject>) =
        x.CompileRender(signature, BackendConfiguration.Default, rjs)

    [<Extension>]
    static member CompileRender (x : IRuntime, signature : IFramebufferSignature, engine : BackendConfiguration, s : ISg) =
        let app = Sg.DynamicNode(Mod.constant s)
        app?Runtime <- x
        let jobs : aset<IRenderObject> = app.RenderObjects()
        let overlays = app.OverlayTasks() |> ASet.sortBy fst |> AList.map snd |> RenderTask.ofAList
        RenderTask.ofList [x.CompileRender(signature, engine, jobs); overlays]

    [<Extension>]
    static member CompileRender (x : IRuntime, signature : IFramebufferSignature, s : ISg) =
        SceneGraphRuntimeExtensions.CompileRender(x, signature, BackendConfiguration.Default, s)

[<AutoOpen>]
module RuntimeSgExtensions =
    module Sg =
    
        let compile (runtime : IRuntime) (signature : IFramebufferSignature) (sg : ISg) =
            runtime.CompileRender(signature, sg)
