
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

    let rand = RandomSystem()
    let setfrustum f = transact ( fun _ -> frustum.Value <- f)


    let getSomeTris () =
        let t = V3d(4,-6,12)
        (Array.init 30 
                (fun _ -> Triangle3d(rand.UniformV3d(Box3d.Unit)+t
                                    ,rand.UniformV3d(Box3d.Unit)+t
                                    ,rand.UniformV3d(Box3d.Unit)+t)))
                |> Array.map ( fun tri -> tri, (rand.UniformC3f().ToC4b()))

    let tris = Mod.init (getSomeTris ())
    let resetTris ()= transact ( fun _ -> tris.Value <- (getSomeTris()) )

    let sglol = 
        let prims = 
            [
                yield IndexedGeometryPrimitives.coordinateCross (V3d(1.0,2.0,3.0))

                yield IndexedGeometryPrimitives.lines'
                    [
                        Line3d(V3d(1.0,2.0,5.0), V3d(4.0,-1.0,0.5))
                        Line3d(V3d(-1.0,-0.5,5.0), V3d(4.5,-1.5,0.5))
                    ] (C4b(255,180,220))

                yield IndexedGeometryPrimitives.points
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
                
                yield IndexedGeometryPrimitives.wireframeSubdivisionSphere
                    (Sphere3d((V3d.III),0.5)) 4  (C4b(240,150,200))

                yield IndexedGeometryPrimitives.solidSubdivisionSphere
                    (Sphere3d((V3d(-1.0,-2.0,2.5)),0.75)) 6  (C4b(140,250,230))

                yield IndexedGeometryPrimitives.wireframePhiThetaSphere
                    (Sphere3d((V3d(1.0,-2.0,-2.5)),0.65)) 6 (C4b(230, 120, 10))

                yield IndexedGeometryPrimitives.solidPhiThetaSphere
                    (Sphere3d((V3d(1.0,2.0,-2.5)),0.45)) 6 (C4b(60, 200, 220))

                yield IndexedGeometryPrimitives.wireframeCylinder (V3d(4.0,4.0,5.0)) (V3d.OOI) 3.0 4.0 5.0 16 (C4b(250,240,230))

                yield IndexedGeometryPrimitives.wireframeTorus (Torus3d(V3d(-6,-6,-5),V3d.OOI,2.0,1.0)) (C4b(160,240,220)) 16 8

                yield IndexedGeometryPrimitives.solidTorus (Torus3d(V3d(-6,8,-5),V3d.OIO,3.0,0.5)) (C4b(240,160,220)) 8 8
                
                        
            ]

        prims
            |> List.map Sg.ofIndexedGeometry
            |> Sg.group'
            |> Sg.andAlso (tris |> Mod.map (IndexedGeometryPrimitives.triangles)
                                |> Mod.map (Sg.ofIndexedGeometry)
                                |> Sg.dynamic)
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

