namespace Examples


open System
open System.IO
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.Text
open System.Runtime.InteropServices
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO
open FShade
open FShade.Imperative
open System.Reflection

module MyShader =

    type Vertex = {
        [<Position>]        pos     : V4d
        [<WorldPosition>]   wp      : V4d
        [<Normal>]          n       : V3d
        [<BiNormal>]        b       : V3d
        [<Tangent>]         t       : V3d
        [<Color>]           c       : V4d
        [<TexCoord>]        tc      : V2d
    }

    let light (v : Vertex) = 
        fragment {
            let lightpos = V3d(5.0,6.0,7.0)                

            let n = v.n |> Vec.normalize
            let c = lightpos - v.wp.XYZ |> Vec.normalize
            let r = 2.0 * (Vec.dot c n |> clamp 0.0 1.0) * n - c |> Vec.normalize
            let cam = uniform?PerView?CameraLocation
            let vd = cam - v.wp.XYZ |> Vec.normalize
    
            let ambient = 0.1
            let diffuse = Vec.dot c n |> clamp 0.0 1.0
            let specular = V3d.III * (max (pow (Vec.dot r vd) 50.0) 0.0)
        
            let l = v.c.XYZ * (ambient + (1.0 - ambient) * diffuse)
            let col = l + l.Length * specular
        
            return V4d(col, v.c.W)
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
                IndexedGeometryPrimitives.solidPhiThetaSphere (Sphere3d(V3d.OOO,0.5)) 12 (C4b(160,120,190))
                IndexedGeometryPrimitives.solidPhiThetaSphere (Sphere3d(V3d.OOO,0.5)) 12 (C4b(160,120,190))
                IndexedGeometryPrimitives.Sphere.solidSubdivisionSphere (Sphere3d(V3d.OOO,0.5)) (2) ((C4b(120,0,240)))
                IndexedGeometryPrimitives.solidSubdivisionSphere (Sphere3d(V3d.OOO,0.5)) (2) ((C4b(120,0,240)))
            |]

        let sg = 
            [ 
                for i in 0..(igs.Length-1) do
                    yield igs.[i]
                           |> Sg.ofIndexedGeometry
                           |> Sg.translate (float i * 1.5) 0.0 0.0
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
