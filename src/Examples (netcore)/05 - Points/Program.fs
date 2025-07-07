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
            pos : V4f
            [<PointCoord>]
            tc : V2f
            [<Semantic("DepthRange"); Interpolation(InterpolationMode.Flat)>] 
            depthRange : float32
            [<FragCoord>] 
            fc : V4f
            [<Semantic("PointPixelSize")>] ps : float32
            [<PointSize>] s: float32
        }

    type Fragment = 
        {   
            [<Color>]
            color : V4f

            [<Depth(DepthWriteMode.OnlyGreater)>] 
            d : float32
        }

    let pointSprite (v : Vertex) = 
        vertex {
            let mv = uniform.ModelViewTrafo
            let vp = mv * v.pos

            let s = uniform.PointSize / V2f uniform.ViewportSize
            let opp = uniform.ProjTrafo * vp
            let pp0 = opp.XYZ / opp.W


            let vpx = 
                let temp = uniform.ProjTrafoInv * V4f(pp0 + V3f(s.X, 0.0f, 0.0f), 1.0f)
                temp.XYZ / temp.W
     
            let vpy = 
                let temp = uniform.ProjTrafoInv * V4f(pp0 + V3f(0.0f, s.Y, 0.0f), 1.0f)
                temp.XYZ / temp.W

            let worldRadius =
                0.5f * (Vec.length (vp.XYZ - vpx) + Vec.length (vp.XYZ - vpy))

            let vpz = vp + V4f(0.0f, 0.0f, worldRadius, 0.0f)
            let ppz = uniform.ProjTrafo * vpz
            let ppz = ppz.XYZ / ppz.W

            let depthRange = abs (ppz.Z - pp0.Z)

            let pixelDist = 
                uniform.PointSize
 
            let pixelDist = 
                if ppz.Z < -1.0f then -1.0f
                else pixelDist

            return { v with ps = floor pixelDist; s = pixelDist; pos = V4f(ppz, 1.0f); depthRange = depthRange; }
        }


    let pointSpriteFragment (v : Vertex) = 
        fragment {
           let mutable cc = v.tc
           let c = v.tc * 2.0f - V2f.II
           let f = Vec.dot c c - 1.0f
           if f > 0.0f then discard()
   
           let t = 1.0f - sqrt (-f)
           let depth = v.fc.Z
           let outDepth = depth + v.depthRange * t

           let c = c * (1.0f + 2.0f / v.ps)
           let f = Vec.dot c c 
           let diffuse = sqrt (max 0.0f (1.0f - f))
           let color = V3f.III * diffuse

           return { color = V4f(color, 1.0f); d = outDepth }
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
            do! Shader.pointSprite
            do! Shader.pointSpriteFragment
        }
        |> Sg.uniform "PointSize" (AVal.constant 50.0)
    

    // show the scene in a simple window
    show {
        backend Backend.GL
        display Display.Mono
        debug false
        samples 8
        scene sg
    }

    0
