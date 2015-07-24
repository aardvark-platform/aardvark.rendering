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
            x.CompileRender(BackendConfiguration.native, rjs)

        member x.CompileRender (engine : BackendConfiguration, e : Sg.Environment) =
            let jobs : aset<RenderJob> = e?RenderJobs()
            x.CompileRender(engine, jobs)

        member x.CompileRender (engine : BackendConfiguration, s : ISg) =
            let app = Sg.DynamicNode(Mod.constant s)
            app?Runtime <- x
            let jobs : aset<RenderJob> = app?RenderJobs()
            x.CompileRender(engine, jobs)

        member x.CompileRender (s : ISg) =
            x.CompileRender(BackendConfiguration.native, s)


    [<Semantic>]
    type RuntimeSem() =
        member x.Runtime(e : Sg.Environment) =
            e.Child?Runtime <- e.Runtime