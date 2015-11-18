
#load "FsiSetup.fsx"

open Examples
open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics

open Aardvark.Application
open Aardvark.Application.WinForms

let setSg, win = runInteractive ()

let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
let perspective = 
    win.Sizes 
        |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 10.0 (float s.X / float s.Y))


let viewTrafo = DefaultCameraController.control win.Mouse win.Keyboard win.Time view


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
        |> Sg.viewTrafo (viewTrafo   |> Mod.map CameraView.viewTrafo )
        |> Sg.projTrafo (perspective |> Mod.map Frustum.toTrafo      )

setSg sg

printfn "Done"
