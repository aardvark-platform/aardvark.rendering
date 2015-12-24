namespace Aardvark.SceneGraph

open Aardvark.SceneGraph.Semantics

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Base.Incremental

[<AbstractClass; Sealed; Extension>]
type SceneGraphRuntimeExtensions private() =

    static let toSurface (l : list<FShadeEffect>) =
        match l with
            | [s] -> FShadeSurface s
            | l -> FShadeSurface (FShade.SequentialComposition.compose l)

    [<Extension>]
    static member PrepareEffect (this : IRuntime, signature : IFramebufferSignature, l : list<FShadeEffect>) =
        this.PrepareSurface(
            signature,
            toSurface l
        )

    [<Extension>]
    static member PrepareEffect (this : IRuntime, signature : IFramebufferSignature, l : IMod<list<FShadeEffect>>) =
        let mutable current = None
        l |> Mod.map (fun l ->
            let newPrep = 
                this.PrepareSurface(
                    signature,
                    toSurface l
                )
            match current with
                | Some c -> this.DeleteSurface c
                | None -> ()
            current <- Some newPrep
            newPrep :> ISurface
        )




    [<Extension>]
    static member CompileRender(x : IRuntime, signature : IFramebufferSignature, rjs : aset<IRenderObject>) =
        x.CompileRender(signature, BackendConfiguration.Default, rjs)

    [<Extension>]
    static member CompileRender (x : IRuntime, signature : IFramebufferSignature, engine : BackendConfiguration, s : ISg) =
        let app = Sg.DynamicNode(Mod.constant s)
        app?Runtime <- x
        let jobs : aset<IRenderObject> = app.RenderObjects()
        x.CompileRender(signature, engine, jobs)

    [<Extension>]
    static member CompileRender (x : IRuntime, signature : IFramebufferSignature, s : ISg) =
        SceneGraphRuntimeExtensions.CompileRender(x, signature, BackendConfiguration.Default, s)

[<AutoOpen>]
module RuntimeSgExtensions =
    module Sg =
    
        let compile (runtime : IRuntime) (signature : IFramebufferSignature) (sg : ISg) =
            runtime.CompileRender(signature, sg)
