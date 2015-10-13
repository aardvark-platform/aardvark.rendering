namespace Aardvark.SceneGraph

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.SceneGraph.Internal

open System.Runtime.CompilerServices

[<AutoOpen>]
module RuntimeSemantics =

    type ISg with
        member x.Runtime : IRuntime = x?Runtime

    [<Semantic>]
    type RuntimeSem() =
        member x.Runtime(e : Sg.Environment) =
            e.Child?Runtime <- e.Runtime

    type IRuntime with

        member x.CompileRender(rjs : aset<IRenderObject>) =
            x.CompileRender(BackendConfiguration.Default, rjs)

        member x.CompileRender (engine : BackendConfiguration, e : Sg.Environment) =
            let jobs : aset<IRenderObject> = e?RenderObjects()
            x.CompileRender(engine, jobs)

        member x.CompileRender (engine : BackendConfiguration, s : ISg) =
            let app = Sg.DynamicNode(Mod.constant s)
            app?Runtime <- x
            let jobs : aset<IRenderObject> = app?RenderObjects()
            x.CompileRender(engine, jobs)

        member x.CompileRender (s : ISg) =
            x.CompileRender(BackendConfiguration.Default, s)


[<Extension; AbstractClass; Sealed>]
type RuntimeExtensions private() =

    [<Extension>]
    static member CompileRender(x : IRuntime, rjs : aset<IRenderObject>) =
        x.CompileRender(rjs)

    [<Extension>]   
    static member CompileRender (x : IRuntime, engine : BackendConfiguration, e : Sg.Environment) =
        x.CompileRender(engine,e)

    [<Extension>]   
    static member  CompileRender (x : IRuntime, engine : BackendConfiguration, s : ISg) =
        x.CompileRender(engine,s)

    [<Extension>]   
    static member CompileRender (x : IRuntime, s : ISg) =
        x.CompileRender(s)
