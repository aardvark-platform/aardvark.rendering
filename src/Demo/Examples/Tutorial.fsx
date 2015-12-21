
#load "RenderingSetup.fsx"
open RenderingSetup

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

open Default // makes viewTrafo and other tutorial specicific default creators visible

let quadSg =
    let quad =
        let index = [|0;1;2; 0;2;3|]
        let positions = [|V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |]

        IndexedGeometry(IndexedGeometryMode.TriangleList, index, SymDict.ofList [DefaultSemantic.Positions, positions :> Array], SymDict.empty)

    quad |> Sg.ofIndexedGeometry

let sg =
    quadSg |> Sg.effect [
            DefaultSurfaces.trafo |> toEffect
            DefaultSurfaces.constantColor C4f.Red |> toEffect
            ]
        |> Sg.viewTrafo (viewTrafo   () |> Mod.map CameraView.viewTrafo )
        |> Sg.projTrafo (perspective () |> Mod.map Frustum.projTrafo    )

setSg sg

printfn "Done"
