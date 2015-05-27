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
        member x.CompileRender (e : Sg.Environment) =
            let jobs : aset<RenderJob> = e?RenderJobs()
            x.CompileRender(jobs)

    [<Semantic>]
    type RuntimeSem() =
            member x.Runtime(e : Sg.Environment) =
                e.Child?Runtime <- e.Runtime