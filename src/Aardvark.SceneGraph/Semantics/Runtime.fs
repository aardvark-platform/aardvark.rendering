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

[<Extension; AbstractClass; Sealed>]
type RuntimeExtensions private() =

    [<Extension>]
    static member CompileRender(x : IRuntime, signature : IFramebufferSignature, rjs : aset<IRenderObject>) =
        x.CompileRender(signature, BackendConfiguration.Default, rjs)

    [<Extension>]
    static member CompileRender (x : IRuntime, signature : IFramebufferSignature, engine : BackendConfiguration, e : Sg.Environment) =
        let jobs : aset<IRenderObject> = e?RenderObjects()
        x.CompileRender(signature, engine, jobs)

    [<Extension>]
    static member CompileRender (x : IRuntime, signature : IFramebufferSignature, engine : BackendConfiguration, s : ISg) =
        let app = Sg.DynamicNode(Mod.constant s)
        app?Runtime <- x
        let jobs : aset<IRenderObject> = app?RenderObjects()
        x.CompileRender(signature, engine, jobs)

    [<Extension>]
    static member CompileRender (x : IRuntime, signature : IFramebufferSignature, s : ISg) =
        RuntimeExtensions.CompileRender(x, signature, BackendConfiguration.Default, s)
