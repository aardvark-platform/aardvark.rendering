namespace Aardvark.Base.Rendering.Effects

open Aardvark.Base
open Aardvark.Base.Rendering
open FShade
open DefaultSurfaceVertex

module SimpleLighting = 

    let internal simpleLighting (v : Vertex) =
        fragment {
            let n = v.n |> Vec.normalize
            let c = uniform.CameraLocation - v.wp.XYZ |> Vec.normalize

            let ambient = 0.2
            let diffuse = Vec.dot c n |> abs

            let l = ambient + (1.0 - ambient) * diffuse

            return V4d(v.c.XYZ * diffuse, v.c.W)
        }

    let Effect = 
        toEffect simpleLighting

