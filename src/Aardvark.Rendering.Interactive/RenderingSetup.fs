namespace Aardvark.Rendering.Interactive

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg


module RenderingSetup =



    let setSg, win, mainTask = runInteractive ()

    module Default =
        let quadSg =
            let quad =
                let index = [|0;1;2; 0;2;3|]
                let positions = [|V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |]
                let coords = [|V2f(0.0,0.0); V2f(1.0,0.0); V2f(1.0,1.0); V2f(0.0,1.0) |]

                IndexedGeometry(IndexedGeometryMode.TriangleList, index, SymDict.ofList [DefaultSemantic.Positions, positions :> Array; DefaultSemantic.DiffuseColorCoordinates, coords :> Array], SymDict.empty)

            quad |> Sg.ofIndexedGeometry

        let viewTrafo' center lookAt =
            let view =  CameraView.LookAt(center, lookAt, V3d.OOI)
            DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        let viewTrafo () =
            viewTrafo' ( V3d(3.0, 3.0, 3.0) ) V3d.Zero

        let perspective () = 
            win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.01 1000.0 (float s.X / float s.Y))

        let defaultCamera () = 
            let vt = viewTrafo ()
            let p = perspective ()
            let cam = Mod.map2 Camera.create vt p
            cam


                


