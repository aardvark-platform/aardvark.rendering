
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
let mkViewTrafo location center = 
    let view =  CameraView.LookAt(location, center, V3d.OOI)
    DefaultCameraController.control win.Mouse win.Keyboard win.Time view

module Default =
    let quadSg =
        let quad =
            let index = [|0;1;2; 0;2;3|]
            let positions = [|V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |]
            let coords = [|V2f(0.0,0.0); V2f(1.0,0.0); V2f(1.0,1.0); V2f(0.0,1.0) |]

            IndexedGeometry(IndexedGeometryMode.TriangleList, index, SymDict.ofList [DefaultSemantic.Positions, positions :> Array; DefaultSemantic.DiffuseColorCoordinates, coords :> Array], SymDict.empty)

        quad |> Sg.ofIndexedGeometry

    let viewTrafo () = mkViewTrafo (V3d(3.0,3.0,3.0)) V3d.Zero
    let perspective () = 
        win.Sizes 
            |> Mod.map (fun s -> Frustum.perspective 60.0 0.01 10.0 (float s.X / float s.Y))