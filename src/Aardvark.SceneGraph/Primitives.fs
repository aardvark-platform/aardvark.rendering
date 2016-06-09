namespace Aardvark.SceneGraph


open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Ag
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

open Aardvark.Base.Incremental

#nowarn "9"
#nowarn "51"

[<AutoOpen>]
module SgPrimitives =

   

    module Sg =
        open Aardvark.Base.Incremental.Operators

        let private unitBoxGeometry =
            let box = Box3d.Unit
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

            let texcoords =
                [|
                    V2f.OO; V2f.IO; V2f.II;  V2f.OO; V2f.II; V2f.OI
                |]

            IndexedGeometry(
                Mode = IndexedGeometryMode.TriangleList,

                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                        DefaultSemantic.Normals, indices |> Array.mapi (fun ti _ -> normals.[ti / 6]) :> Array
                        DefaultSemantic.DiffuseColorCoordinates, indices |> Array.mapi (fun ti _ -> texcoords.[ti % 6]) :> Array
                    ]

            ) |> Sg.ofIndexedGeometry

        let private unitWireBoxGeometry =
            let box = Box3d.Unit
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
                        DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                        DefaultSemantic.Normals, indices |> Array.mapi (fun ti _ -> normals.[ti / 6]) :> Array
                    ]

            ) |> Sg.ofIndexedGeometry


        
        let fullScreenQuad =
            let drawCall = 
                DrawCallInfo(
                    FaceVertexCount = 4,
                    InstanceCount = 1
                )

            let positions =     [| V3f(-1,-1,0); V3f(1,-1,0); V3f(-1,1,0); V3f(1,1,0) |]
            let texcoords =     [| V2f(0,0); V2f(1,0); V2f(0,1); V2f(1,1) |]
            let normals =       [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |]

            drawCall
                |> Sg.render IndexedGeometryMode.TriangleStrip 
                |> Sg.vertexAttribute DefaultSemantic.Positions (Mod.constant positions)
                |> Sg.vertexAttribute DefaultSemantic.Normals (Mod.constant normals)
                |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (Mod.constant texcoords)


        let box (color : IMod<C4b>) (bounds : IMod<Box3d>) =
            let trafo = bounds |> Mod.map (fun box -> Trafo3d.Scale(box.Size) * Trafo3d.Translation(box.Min))
            let color = color |> Mod.map (fun c -> c.ToC4f().ToV4f())
            unitBoxGeometry
                |> Sg.vertexBufferValue DefaultSemantic.Colors color
                |> Sg.trafo trafo

        let box' (color : C4b) (bounds : Box3d) =
            box ~~color ~~bounds


        let wireBox (color : IMod<C4b>) (bounds : IMod<Box3d>) =
            let trafo = bounds |> Mod.map (fun box -> Trafo3d.Scale(box.Size) * Trafo3d.Translation(box.Min))
            let color = color |> Mod.map (fun c -> c.ToC4f().ToV4f())
            unitWireBoxGeometry
                |> Sg.vertexBufferValue DefaultSemantic.Colors color
                |> Sg.trafo trafo

        let wireBox' (color : C4b) (bounds : Box3d) =
            wireBox ~~color ~~bounds


        let frustum (color : IMod<C4b>) (view : IMod<CameraView>) (proj : IMod<Frustum>) =
            let invViewProj = Mod.map2 (fun v p -> (CameraView.viewTrafo v * Frustum.projTrafo p).Inverse) view proj

            Box3d(-V3d.III, V3d.III)
                |> Mod.constant
                |> wireBox color
                |> Sg.trafo invViewProj