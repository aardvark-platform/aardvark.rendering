namespace Aardvark.SceneGraph

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive

[<AutoOpen>]
module AfterShader = 

    open Aardvark.SceneGraph.Semantics
    open Aardvark.Base.Ag

    type AfterSg(e : FShade.Effect, sg : ISg) = 
        inherit Sg.AbstractApplicator(sg)
        member x.Sg = sg
        member x.Effect = e

    [<Rule>]
    type AfterSgSem() =
        let rec adjust (e : FShade.Effect) (o : IRenderObject) =
                match o with
                | :? RenderObject as o ->
                    let ro = RenderObject.Clone o
                    ro.Surface <-
                        match o.Surface with
                        | Surface.Effect o -> Surface.Effect (FShade.Effect.compose [o; e])
                        | s -> s
                    ro :> IRenderObject
                | :? MultiRenderObject as o ->
                    o.Children |> List.map (adjust e) |> MultiRenderObject :> IRenderObject
                | _ ->
                    o

        member x.RenderObjects(a : AfterSg, scope : Ag.Scope) = 
            a.Child |> ASet.bind (fun c -> c.RenderObjects(scope) |> ASet.map (adjust a.Effect))


    module Sg =
        
        let afterEffect (e : list<FShade.Effect>) (sg : ISg) = 
            AfterSg(FShade.Effect.compose e, sg) :> ISg