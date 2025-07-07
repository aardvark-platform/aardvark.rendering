namespace Examples


open System
open System.IO
open Aardvark.Base
open FSharp.Data.Adaptive

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open FSharp.Data.Adaptive.Operators
open Aardvark.Rendering
open Aardvark.Rendering.ShaderReflection
open Aardvark.Rendering.Text
open System.Runtime.InteropServices
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Assimp
open FShade
open FShade.Imperative
open System.Reflection

module MyShader =

    type Vertex = {
        [<Position>]        pos     : V4f
        [<WorldPosition>]   wp      : V4f
        [<Normal>]          n       : V3f
        [<BiNormal>]        b       : V3f
        [<Tangent>]         t       : V3f
        [<Color>]           c       : V4f
        [<TexCoord>]        tc      : V2f
    }

    let light (v : Vertex) = 
        fragment {
            let lightpos = V3f(5.0f,6.0f,7.0f)

            let n = v.n |> Vec.normalize
            let c = lightpos - v.wp.XYZ |> Vec.normalize
            let r = 2.0f * (Vec.dot c n |> clamp 0.0f 1.0f) * n - c |> Vec.normalize
            let cam = uniform?PerView?CameraLocation
            let vd = cam - v.wp.XYZ |> Vec.normalize
    
            let ambient = 0.1f
            let diffuse = Vec.dot c n |> clamp 0.0f 1.0f
            let specular = V3f.III * (max (pow (Vec.dot r vd) 50.0f) 0.0f)
        
            let l = v.c.XYZ * (ambient + (1.0f - ambient) * diffuse)
            let col = l + l.Length * specular
        
            return V4f(col, v.c.W)
        }

module IGPrimitives =
    let run() =
        
        let win = 
            window {
                display Display.Mono
                samples 1
                backend Backend.Vulkan

                debug false
            }
            
        let igs =
            [|
                IndexedGeometryPrimitives.Sphere.solidPhiThetaSphere (Sphere3d(V3d.OOO,0.5)) 12 (C4b(160,120,190))
                IndexedGeometryPrimitives.Sphere.wireframePhiThetaSphere (Sphere3d(V3d.OOO,0.5)) 12 (C4b(160,120,190))
                IndexedGeometryPrimitives.Sphere.solidSubdivisionSphere (Sphere3d(V3d.OOO,0.5)) (2) ((C4b(120,0,240)))
                IndexedGeometryPrimitives.Sphere.wireframeSubdivisionSphere (Sphere3d(V3d.OOO,0.5)) (2) ((C4b(120,0,240)))
                IndexedGeometryPrimitives.Quad.solidQuadrangle     V3d.OOO V3d.IOO V3d.IOI V3d.OOI (C4b(255,255,255)) V3d.OIO
                IndexedGeometryPrimitives.Quad.wireframeQuadrangle V3d.OOO V3d.IOO V3d.IOI V3d.OOI (C4b(255,255,255)) V3d.OIO
                IndexedGeometryPrimitives.Triangle.solidTrianglesWithColor [ Triangle3d(V3d(0.2,0.2,0.2),V3d(0.4,0.0,0.4),V3d(0.3,0.3,0.0))     ; Triangle3d(V3d(0.6,0.7,0.7),V3d(0.8,0.9,0.9),V3d(0.8,0.7,0.7)) ] (C4b(220,210,60))
                IndexedGeometryPrimitives.Triangle.wireframeTrianglesWithColor [ Triangle3d(V3d(0.2,0.2,0.2),V3d(0.4,0.0,0.4),V3d(0.3,0.3,0.0)) ; Triangle3d(V3d(0.6,0.7,0.7),V3d(0.8,0.9,0.9),V3d(0.8,0.7,0.7)) ] (C4b(220,210,60))
                IndexedGeometryPrimitives.Stuff.coordinateCross V3d.III
                IndexedGeometryPrimitives.Box.solidBox (Box3d(V3d.OOO, V3d.III)) (C4b(240,150,220))
                IndexedGeometryPrimitives.Box.wireBox (Box3d(V3d.OOO, V3d.III)) (C4b(240,150,220))
                IndexedGeometryPrimitives.Torus.solidTorus (Torus3d(V3d.OOO, V3d.OOI, 0.8, 0.2)) (C4b(200,180,255)) 12 8
                IndexedGeometryPrimitives.Torus.wireframeTorus (Torus3d(V3d.OOO, V3d.OOI, 0.8, 0.2)) (C4b(200,180,255)) 12 8
                IndexedGeometryPrimitives.Cone.solidCone V3d.OOO V3d.OOI 1.0 0.25 12 (C4b(120, 130, 170))
                IndexedGeometryPrimitives.Cone.wireframeCone V3d.OOO V3d.OOI 1.0 0.25 12 (C4b(120, 130, 170))
                IndexedGeometryPrimitives.Sphere.wireframePhiThetaSphere (Sphere3d(V3d.OOO,0.5)) 1 (C4b(200,200,200))
                IndexedGeometryPrimitives.Sphere.wireframePhiThetaSphere (Sphere3d(V3d.OOO,0.5)) 2 (C4b(200,200,200))
                IndexedGeometryPrimitives.Sphere.wireframePhiThetaSphere (Sphere3d(V3d.OOO,0.5)) 3 (C4b(200,200,200))
                IndexedGeometryPrimitives.Sphere.wireframePhiThetaSphere (Sphere3d(V3d.OOO,0.5)) 4 (C4b(200,200,200))
                IndexedGeometryPrimitives.Sphere.wireframePhiThetaSphere (Sphere3d(V3d.OOO,0.5)) 5 (C4b(200,200,200))
                IndexedGeometryPrimitives.Sphere.wireframePhiThetaSphere (Sphere3d(V3d.OOO,0.5)) 6 (C4b(200,200,200))
                IndexedGeometryPrimitives.Tetrahedron.solidTetrahedron V3d.OOO 1.0 (C4b(26,100,240))
                IndexedGeometryPrimitives.Tetrahedron.wireframeTetrahedron V3d.OOO 1.0 (C4b(26,100,240))
                IndexedGeometryPrimitives.Cylinder.solidCylinder V3d.OOO V3d.OOI 1.0 0.25 0.15 16 (C4b(250, 100, 140))
                IndexedGeometryPrimitives.Cylinder.wireframeCylinder V3d.OOO V3d.OOI 1.0 0.25 0.15 16 (C4b(250, 100, 140))
            |]

        let sg = 
            [ 
                let perLine = 6
                for i in 0..(igs.Length-1) do
                    yield igs.[i]
                           |> Sg.ofIndexedGeometry
                           |> Sg.translate (float (i%perLine) * 1.5) 0.0 (float (i/perLine) * 1.5)
            ]
                |> Sg.ofList
                |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero))
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! MyShader.light

                }
        
        //FShade.EffectDebugger.attach()

        win.Scene <- sg

        win.Run()
