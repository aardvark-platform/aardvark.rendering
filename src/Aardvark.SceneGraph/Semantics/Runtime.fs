namespace Aardvark.SceneGraph

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open Aardvark.SceneGraph
open Aardvark.Base.Rendering

open Aardvark.SceneGraph.Internal

[<AutoOpen>]
module RuntimeSemantics =

    type IRuntime with

        member x.CompileRender(rjs : aset<RenderJob>) =
            x.CompileRender(ExecutionEngine.Default, rjs)

        member x.CompileRender (engine : ExecutionEngine, e : Sg.Environment) =
            let jobs : aset<RenderJob> = e?RenderJobs()
            x.CompileRender(engine, jobs)

        member x.CompileRender (engine : ExecutionEngine, s : ISg) =
            let app = Sg.DynamicNode(Mod.constant s)
            app?Runtime <- x
            let jobs : aset<RenderJob> = app?RenderJobs()
            x.CompileRender(engine, jobs)

        member x.CompileRender (e : Sg.Environment) =
            x.CompileRender(ExecutionEngine.Default, e)

        member x.CompileRender (s : ISg) =
            x.CompileRender(ExecutionEngine.Default, s)


    [<Semantic>]
    type RuntimeSem() =
        member x.Runtime(e : Sg.Environment) =
            e.Child?Runtime <- e.Runtime