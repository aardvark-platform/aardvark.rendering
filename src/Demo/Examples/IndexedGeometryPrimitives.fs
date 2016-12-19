
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

