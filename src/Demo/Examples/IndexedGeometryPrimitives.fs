
#if INTERACTIVE
#I @"../../../bin/Debug"
#I @"../../../bin/Release"
#load "LoadReferences.fsx"
#else
namespace Examples
#endif

open System
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.Rendering.Interactive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Rendering.NanoVg

module IndexedGeometry = 

    FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])

    module lol =
        
        let getCirclePos (i : int) (tessellation : int) (trafo : Trafo3d) =
            let angle = float i * Constant.PiTimesTwo / float tessellation
            trafo.Forward.TransformPos(V3d(Fun.Cos(angle), Fun.Sin(angle), 0.0))

        let private cylinderTriangleIndices1 (indices : List<int>) (i : int) (tessellation : int) =
                indices.Add(i * 2)
                indices.Add(i * 2 + 1)
                indices.Add((i * 2 + 2) % (tessellation * 2))

                indices.Add(i * 2 + 1)
                indices.Add((i * 2 + 3) % (tessellation * 2))
                indices.Add((i * 2 + 2) % (tessellation * 2))

        let private cylinderTriangleIndices2 (indices : List<int>) (i : int) (tessellation : int) =
                // top
                indices.Add(i * 2 + tessellation * 2)
                indices.Add(((i * 2 + 2) % (tessellation * 2)) + tessellation * 2)
                indices.Add(tessellation * 4)

                // bottom
                indices.Add(((i * 2 + 3) % (tessellation * 2)) + tessellation * 2)
                indices.Add(i * 2 + 1 + tessellation * 2)
                indices.Add(tessellation * 4 + 1)
           
        let private cylinderLineIndices1 (indices : List<int>) (i : int) (tessellation : int) =
                indices.Add(i * 2)
                indices.Add(i * 2 + 1)
//                indices.Add(i * 2 + 1)
//                indices.Add((i * 2 + 2) % (tessellation * 2))

//                indices.Add(i * 2 + 1)
//                indices.Add((i * 2 + 3) % (tessellation * 2))
//                indices.Add((i * 2 + 3) % (tessellation * 2))
//                indices.Add((i * 2 + 2) % (tessellation * 2))

        let private cylinderLineIndices2 (indices : List<int>) (i : int) (tessellation : int) =
                // top
                indices.Add(i * 2 + tessellation * 2)
                indices.Add(((i * 2 + 2) % (tessellation * 2)) + tessellation * 2)
                indices.Add(((i * 2 + 2) % (tessellation * 2)) + tessellation * 2)
                indices.Add(tessellation * 4)

                // bottom
                indices.Add(((i * 2 + 3) % (tessellation * 2)) + tessellation * 2)
                indices.Add(i * 2 + 1 + tessellation * 2)
                indices.Add(i * 2 + 1 + tessellation * 2)
                indices.Add(tessellation * 4 + 1)
            

        let cylinder (center : V3d) (axis : V3d) (height : float) (radius : float) (radiusTop : float) (tessellation : int) (mode : IndexedGeometryMode) =
            
            let vertices = List<V3d>()
            let normals = List<V3d>()
            let indices = List<int>()

            let axisNormalized = axis.Normalized
            let trafo = Trafo3d.FromNormalFrame(V3d.OOO, axisNormalized)

            // create a ring of triangles around the outside of the cylinder
            for i in 0 .. tessellation - 1 do
                let normal = getCirclePos i tessellation trafo

                vertices.Add(normal * radiusTop + axis * height)
                normals.Add(normal)

                vertices.Add(normal * radius)
                normals.Add(normal)

                match mode with
                | IndexedGeometryMode.TriangleList ->
                    cylinderTriangleIndices1 indices i tessellation
                | IndexedGeometryMode.LineList ->
                    cylinderLineIndices1 indices i tessellation //todo
                | _ -> failwith "implement me"

            // top and bottom caps need replicated vertices because of
            // different normals
            for i in 0 .. tessellation - 1 do
                // top
                vertices.Add(vertices.[i * 2])
                normals.Add(axisNormalized)

                // bottom
                vertices.Add(vertices.[i * 2 + 1])
                normals.Add(-axisNormalized)
                
                match mode with
                | IndexedGeometryMode.TriangleList ->
                    cylinderTriangleIndices2 indices i tessellation
                | IndexedGeometryMode.LineList ->
                    cylinderLineIndices2 indices i tessellation //todo
                | _ -> failwith "implement me"

            // top cap center
            vertices.Add(axis * height)
            normals.Add(axisNormalized)

            // bottom cap center
            vertices.Add(V3d.OOO)
            normals.Add(-axisNormalized)

            let pos = vertices |> Seq.toArray |> Array.map (Trafo3d.Translation(center).Forward.TransformPos)
            let norm = normals |> Seq.toArray
            let idx = indices  |> Seq.toArray

            pos,norm,idx

        let cylinderWithCol (center : V3d) (axis : V3d) (height : float) (radius : float) (radiusTop : float) (tessellation : int) (mode : IndexedGeometryMode) (col : C4b) =
            let (pos,norm,idx) = cylinder center axis height radius radiusTop tessellation mode
            let col = Array.replicate (pos |> Array.length) col

            IndexedGeometryPrimitives.IndexedGeometry.fromPosColNorm pos col norm (Some idx) mode

        let solidCylinder (center : V3d) (axis : V3d) (height : float) (radiusBottom : float) (radiusTop : float) (tessellation : int) (color : C4b) =
            cylinderWithCol center axis height radiusBottom radiusTop tessellation IndexedGeometryMode.TriangleList color

        let wireframeCylinder (center : V3d) (axis : V3d) (height : float) (radiusBottom : float) (radiusTop : float) (tessellation : int) (color : C4b) =
            cylinderWithCol center axis height radiusBottom radiusTop tessellation IndexedGeometryMode.LineList color

        let solidCone (center : V3d) (axis : V3d) (height : float) (radius : float) (tessellation : int) (color : C4b) =
            cylinderWithCol center axis height radius 0.0 tessellation IndexedGeometryMode.TriangleList color
        
        let wireframeCone (center : V3d) (axis : V3d) (height : float) (radius : float) (tessellation : int) (color : C4b) =
            cylinderWithCol center axis height radius 0.0 tessellation IndexedGeometryMode.LineList color

    open lol

    Interactive.Samples <- 1
    let win = Interactive.Window

    let s = IndexedGeometryPrimitives.wireframeSubdivisionSphere
                    (Sphere3d((V3d.III),0.5)) 4  (C4b(240,150,200))

    let frustum = Mod.init 
                    (Frustum.perspective 60.0 0.1 100.0 0.75)

    let setfrustum f = transact ( fun _ -> frustum.Value <- f)
    let sglol = 
        let prims = 
            [
                IndexedGeometryPrimitives.coordinateCross (V3d(1.0,2.0,3.0))

                IndexedGeometryPrimitives.lines'
                    [
                        Line3d(V3d(1.0,2.0,5.0), V3d(4.0,-1.0,0.5))
                        Line3d(V3d(-1.0,-0.5,5.0), V3d(4.5,-1.5,0.5))
                    ] (C4b(255,180,220))

                IndexedGeometryPrimitives.points
                    [|
                        V3d(2.0,4.2,-6.3)
                        V3d(6.0,-4.2,-2.3)
                        V3d(3.0,0.2,3.3)
                        V3d(2.3,5.2,5.3)
                        V3d(2.4,1.2,-1.3)
                    |]
                    [|
                        C4b(235,235,215)
                        C4b(212,123,211)
                        C4b(112,101,025)
                        C4b(210,011,110)
                        C4b(004,210,150)
                    |]
                
                IndexedGeometryPrimitives.wireframeSubdivisionSphere
                    (Sphere3d((V3d.III),0.5)) 4  (C4b(240,150,200))

                IndexedGeometryPrimitives.solidSubdivisionSphere
                    (Sphere3d((V3d(-1.0,-2.0,2.5)),0.75)) 6  (C4b(140,250,230))

                IndexedGeometryPrimitives.wireframePhiThetaSphere
                    (Sphere3d((V3d(1.0,-2.0,-2.5)),0.65)) 6 (C4b(230, 120, 10))

                IndexedGeometryPrimitives.solidPhiThetaSphere
                    (Sphere3d((V3d(1.0,2.0,-2.5)),0.45)) 6 (C4b(60, 200, 220))

                wireframeCone (V3d(4.0,4.0,5.0)) (V3d.OOI) 3.0 4.0 16 (C4b(250,240,230))
            ]

        prims
            |> List.map Sg.ofIndexedGeometry
            |> Sg.group'
            |> Sg.andAlso (IndexedGeometryPrimitives.cameraFrustum (CameraView.lookAt V3d.III -V3d.III V3d.OOI |> Mod.constant) 
                                                        frustum
                                                        (C4b.Red |> Mod.constant)
                                                        |> Mod.map (Sg.ofIndexedGeometry)
                                                        |> Sg.dynamic
                                                        )
            |> Sg.viewTrafo Interactive.DefaultViewTrafo
            |> Sg.projTrafo Interactive.DefaultProjTrafo
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.vertexColor |> toEffect]
            |> Sg.fillMode (Mod.constant FillMode.Fill)
            |> Sg.cullMode (Mod.constant CullMode.CounterClockwise)

    let run () =
        Aardvark.Rendering.Interactive.FsiSetup.defaultCamera <- false
        Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])
        Interactive.SceneGraph <- sglol
        Interactive.RunMainLoop()


open IndexedGeometry

#if INTERACTIVE
Interactive.SceneGraph <- sglol
printfn "Done. Modify sg and call set the scene graph again in order to see the modified rendering result."
#endif

