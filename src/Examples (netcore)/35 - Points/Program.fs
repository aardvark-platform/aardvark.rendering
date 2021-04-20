open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open System.Runtime.InteropServices

#nowarn "9"


module Shader = 
    

    open FShade
    
    type Vertex = 
        {
            [<Position>]
            pos : V4d
            [<PointCoord>]
            tc : V2d
            [<PointSize>]
            ps : float
        }

    let pointSprite (v : Vertex) = 
        vertex {
            return { v with ps = 30.0 }
        }


    let pointSpriteFragment (v : Vertex) = 
        fragment {
            let c = v.tc * 2.0 - V2d.II
            let f = Vec.dot c c
            let diffuse = sqrt (max 0.0 (1.0 - f))
            return V4d(V3d.III * diffuse, 1.0)
        }




[<EntryPoint>]
let main argv = 
    Aardvark.Init()

    let pts = 
        let rnd = new System.Random()
        Array.init 100 (fun _ -> V3f(rnd.NextDouble() * 5.0,rnd.NextDouble() * 5.0,rnd.NextDouble() * 5.0))

    let sg = 
        Sg.draw IndexedGeometryMode.PointList
        |> Sg.vertexArray DefaultSemantic.Positions pts
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! Shader.pointSprite
            do! Shader.pointSpriteFragment
        }
    

    // show the scene in a simple window
    show {
        backend Backend.GL
        display Display.Mono
        debug false
        samples 8
        scene sg
    }

    0
