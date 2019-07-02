namespace Aardvark.SceneGraph

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering


[<AutoOpen>]
module AfterShader = 

    open Aardvark.SceneGraph.Semantics
    open Aardvark.Base.Ag

    type AfterSg(e : FShade.Effect, sg : ISg) = 
        inherit Sg.AbstractApplicator(sg)
        member x.Sg = sg
        member x.Effect = e

    [<Semantic>]
    type AfterSgSem() =
        let rec adjust (e : FShade.Effect) (o : IRenderObject) =
                match o with
                | :? RenderObject as o ->
                    { o with
                        Id = newId()
                        Surface =
                            match o.Surface with
                            | Surface.FShadeSimple o -> Surface.FShadeSimple (FShade.Effect.compose [o; e])
                            | s -> s
                    } :> IRenderObject
                | :? MultiRenderObject as o ->
                    o.Children |> List.map (adjust e) |> MultiRenderObject :> IRenderObject
                | _ ->
                    o

        member x.RenderObjects(a : AfterSg) = 
            a.Child |> ASet.bind (fun c -> c.RenderObjects() |> ASet.map (adjust a.Effect))


    module Sg =
        
        let afterEffect (e : list<FShade.Effect>) (sg : ISg) = 
            AfterSg(FShade.Effect.compose e, sg) :> ISg