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
            [<Semantic("DepthRange"); Interpolation(InterpolationMode.Flat)>] 
            depthRange : float
            [<FragCoord>] 
            fc : V4d
            [<Semantic("PointPixelSize")>] ps : float
            [<PointSize>] s: float
        }

    type Fragment = 
        {   
            [<Color>]
            color : V4d

            [<Depth(DepthWriteMode.OnlyGreater)>] 
            d : float
        }

    let pointSprite (v : Vertex) = 
        vertex {
            let mv = uniform.ModelViewTrafo
            let vp = mv * v.pos

            let s = uniform.PointSize / V2d uniform.ViewportSize
            let opp = uniform.ProjTrafo * vp
            let pp0 = opp.XYZ / opp.W


            let vpx = 
                let temp = uniform.ProjTrafoInv * V4d(pp0 + V3d(s.X, 0.0, 0.0), 1.0)
                temp.XYZ / temp.W
     
            let vpy = 
                let temp = uniform.ProjTrafoInv * V4d(pp0 + V3d(0.0, s.Y, 0.0), 1.0)
                temp.XYZ / temp.W

            let worldRadius =
                0.5 * (Vec.length (vp.XYZ - vpx) + Vec.length (vp.XYZ - vpy))

            let vpz = vp + V4d(0.0, 0.0, worldRadius, 0.0)
            let ppz = uniform.ProjTrafo * vpz
            let ppz = ppz.XYZ / ppz.W

            let depthRange = abs (ppz.Z - pp0.Z)

            let pixelDist = 
                uniform.PointSize
 
            let pixelDist = 
                if ppz.Z < -1.0 then -1.0
                else pixelDist

            return { v with ps = floor pixelDist; s = pixelDist; pos = V4d(ppz, 1.0); depthRange = depthRange; }
        }


    let pointSpriteFragment (v : Vertex) = 
        fragment {
           let mutable cc = v.tc
           let c = v.tc * 2.0 - V2d.II
           let f = Vec.dot c c - 1.0
           if f > 0.0 then discard()
   
           let t = 1.0 - sqrt (-f)
           let depth = v.fc.Z
           let outDepth = depth + v.depthRange * t

           let c = c * (1.0 + 2.0 / v.ps)
           let f = Vec.dot c c 
           let diffuse = sqrt (max 0.0 (1.0 - f))
           let color = V3d.III * diffuse

           return { color = V4d(color, 1.0); d = outDepth }
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
