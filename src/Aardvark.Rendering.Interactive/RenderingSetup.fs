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

        let viewTrafo' center lookAt =
            let view =  CameraView.LookAt(center, lookAt, V3d.OOI)
            DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        let viewTrafo () =
            viewTrafo' ( V3d(3.0, 3.0, 3.0) ) V3d.Zero


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

    module Sphere =
        open System.Collections.Generic

        let generate level =
            let vertices = List<_>()
        
            let addVertex =
                let mutable index = 0 
                fun (p:V3f) ->
                    vertices.Add <| Vec.normalize p
                    index <- index + 1
                    index - 1

            let emitTriangle (indices:List<_>) tri =
                indices.Add tri

            let getMiddlePoint =
                let cache = Dictionary()
                fun p1 p2 -> 
                    let small,great = if p1 < p2 then int64 p1,int64 p2 else int64 p2,int64 p1
                    let key = (small <<< 32) + great
                    match cache.TryGetValue key with
                        | (false,_) -> 
                            let p1 = vertices.[p1]
                            let p2 = vertices.[p2]
                            let m = V3f.op_Multiply(0.5f,p1+p2)
                            let i = addVertex m
                            //cache.[key] <- i
                            i
                        | (true,v) -> v

            let t = (1.0 + Fun.Sqrt 5.0) / 2.0

            let v = 
                [
                    V3f(-1.0,  t, 0.0);  V3f( 1.0,  t, 0.0);  V3f(-1.0, -t, 0.0); V3f( 1.0, -t, 0.0)
                    V3f( 0.0, -1.0,  t); V3f( 0.0,  1.0,  t); V3f( 0.0, -1.0, -t); V3f( 0.0,  1.0, -t)
                    V3f(  t, 0.0, -1.0); V3f(  t, 0.0,  1.0); V3f( -t, 0.0, -1.0); V3f( -t, 0.0,  1.0)
                ] |> List.iter (ignore << addVertex)

            let indices = List<_>()

            let i = 
                [ 
                  (0, 11, 5); (0,  5,  1); (0 ,  1, 7 ); (0 , 7, 10); (0, 10, 11)
                  (1, 5 , 9); (5, 11,  4); (11, 10, 2 ); (10, 7, 6 ); (7, 1 , 8 )
                  (3,  9, 4); (3,  4,  2); (3 ,  2, 6 ); (3 , 6, 8 ); (3, 8 , 9 )
                  (4,  9, 5); (2,  4, 11); (6 ,  2, 10); (8 , 6, 7 ); (9, 8 , 1 ) 
                ] |> List.iter (emitTriangle indices)
        
            let rec run faces toGo = 
                if toGo = 0 then faces
                else
                    let newFaces = List()
                    for (v1,v2,v3) in faces do
                      let a = getMiddlePoint v1 v2
                      let b = getMiddlePoint v2 v3
                      let c = getMiddlePoint v3 v1
                  
                      emitTriangle newFaces (v1, a, c)
                      emitTriangle newFaces (v2, b, a)
                      emitTriangle newFaces (v3, c, b)
                      emitTriangle newFaces (a, b, c)
                    run newFaces (toGo - 1)

            let indices = run indices level

            let normals =
                let center = V3f.OOO
                let normals = List()
                for v in vertices do
                    normals.Add ( (v - center).Normalized )
                normals

            indices.ToArray() |> Array.collect (fun (a,b,c) -> [|a;b;c|]), vertices.ToArray(), normals.ToArray()


        let solidSphere color n =
            let (indices,positions,normals) = generate n
            IndexedGeometry(
                Mode = IndexedGeometryMode.TriangleList,
                IndexArray = indices,

                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions, positions :> Array
                        DefaultSemantic.Normals, normals :> Array
                        DefaultSemantic.Colors, indices |> Array.map (fun _ -> color) :> Array
                    ]
            ) |> Sg.ofIndexedGeometry