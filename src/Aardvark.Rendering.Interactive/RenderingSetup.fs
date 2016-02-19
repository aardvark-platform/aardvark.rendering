namespace Aardvark.Rendering.Interactive


[<AutoOpen>]
module RenderingSetup =

    open System
    open Aardvark.Base
    open Aardvark.Base.Incremental
    open Aardvark.Base.Rendering
    open Aardvark.SceneGraph
    open Aardvark.SceneGraph.Semantics

    open Aardvark.Application
    open Aardvark.Application.WinForms


    let setSg, win, mainTask = runInteractive ()

    module Default =
        let quadSg =
            let quad =
                let index = [|0;1;2; 0;2;3|]
                let positions = [|V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |]
                let coords = [|V2f(0.0,0.0); V2f(1.0,0.0); V2f(1.0,1.0); V2f(0.0,1.0) |]

                IndexedGeometry(IndexedGeometryMode.TriangleList, index, SymDict.ofList [DefaultSemantic.Positions, positions :> Array; DefaultSemantic.DiffuseColorCoordinates, coords :> Array], SymDict.empty)

            quad |> Sg.ofIndexedGeometry

        let viewTrafo () =
            let view =  CameraView.LookAt(V3d(3.0, 3.0, 3.0), V3d.Zero, V3d.OOI)
            DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        let perspective () = 
            win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))


    module Helpers =

        let box (color : C4b) (box : Box3d) = 

            let randomColor = color //C4b(rand.Next(255) |> byte, rand.Next(255) |> byte, rand.Next(255) |> byte, 255uy)

            let indices =
                [|
                    1;2;6; 1;6;5
                    2;3;7; 2;7;6
                    4;5;6; 4;6;7
                    3;0;4; 3;4;7
                    0;1;5; 0;5;4
                    0;3;2; 0;2;1
                |]

            let positions = 
                [|
                    V3f(box.Min.X, box.Min.Y, box.Min.Z)
                    V3f(box.Max.X, box.Min.Y, box.Min.Z)
                    V3f(box.Max.X, box.Max.Y, box.Min.Z)
                    V3f(box.Min.X, box.Max.Y, box.Min.Z)
                    V3f(box.Min.X, box.Min.Y, box.Max.Z)
                    V3f(box.Max.X, box.Min.Y, box.Max.Z)
                    V3f(box.Max.X, box.Max.Y, box.Max.Z)
                    V3f(box.Min.X, box.Max.Y, box.Max.Z)
                |]

            let normals = 
                [| 
                    V3f.IOO;
                    V3f.OIO;
                    V3f.OOI;

                    -V3f.IOO;
                    -V3f.OIO;
                    -V3f.OOI;
                |]

            IndexedGeometry(
                Mode = IndexedGeometryMode.TriangleList,

                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                        DefaultSemantic.Normals, indices |> Array.mapi (fun ti _ -> normals.[ti / 6]) :> Array
                        DefaultSemantic.Colors, indices |> Array.map (fun _ -> randomColor) :> Array
                    ]

            )

        let wireBox (color : C4b) (box : Box3d) = 
            let indices =
                [|
                    1;2; 2;6; 6;5; 5;1;
                    2;3; 3;7; 7;6; 4;5; 
                    7;4; 3;0; 0;4; 0;1;
                |]

            let positions = 
                [|
                    V3f(box.Min.X, box.Min.Y, box.Min.Z)
                    V3f(box.Max.X, box.Min.Y, box.Min.Z)
                    V3f(box.Max.X, box.Max.Y, box.Min.Z)
                    V3f(box.Min.X, box.Max.Y, box.Min.Z)
                    V3f(box.Min.X, box.Min.Y, box.Max.Z)
                    V3f(box.Max.X, box.Min.Y, box.Max.Z)
                    V3f(box.Max.X, box.Max.Y, box.Max.Z)
                    V3f(box.Min.X, box.Max.Y, box.Max.Z)
                |]

            let normals = 
                [| 
                    V3f.IOO;
                    V3f.OIO;
                    V3f.OOI;

                    -V3f.IOO;
                    -V3f.OIO;
                    -V3f.OOI;
                |]

            IndexedGeometry(
                Mode = IndexedGeometryMode.LineList,

                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions, indices |> Array.map  (fun i -> positions.[i]) :> Array
                        DefaultSemantic.Normals,   indices |> Array.mapi (fun ti _ -> normals.[ti / 6]) :> Array
                        DefaultSemantic.Colors,    indices |> Array.map  (fun _ -> color) :> Array
                    ]

            )