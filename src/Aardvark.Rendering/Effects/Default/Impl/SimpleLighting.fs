namespace Aardvark.Rendering.Effects

open Aardvark.Base
open Aardvark.Rendering
open FShade

module SimpleLighting = 

    let internal simpleLighting (v : Vertex) =
        fragment {
            let n = v.n |> Vec.normalize
            let c = uniform.LightLocation - v.wp.XYZ |> Vec.normalize

            let ambient = 0.2f
            let diffuse = Vec.dot c n |> abs

            let l = ambient + (1.0f - ambient) * diffuse

            return V4f(v.c.XYZ * l, v.c.W)
        }

    type Vertex = {
        [<Position>]                pos     : V4f
        [<Normal>]                  n       : V3f
        [<BiNormal>]                b       : V3f
        [<Tangent>]                 t       : V3f
        [<Color>]                   c       : V4f
        [<Semantic("LightDir")>]    ldir    : V3f
    }

    let stableTrafo (v : Vertex) =
        vertex {
            let vp = uniform.ModelViewTrafo * v.pos

            return {
                pos = uniform.ProjTrafo * vp
                n = uniform.ModelViewTrafoInv.TransposedTransformDir v.n |> Vec.normalize
                b = uniform.ModelViewTrafo.TransformDir v.b |> Vec.normalize
                t = uniform.ModelViewTrafo.TransformDir v.t |> Vec.normalize
                c = v.c
                ldir = -vp.XYZ |> Vec.normalize
            }
        }

    let stableLight (v : Vertex) =
        fragment {
            let n = v.n |> Vec.normalize
            let c = v.ldir |> Vec.normalize

            let ambient = 0.2f
            let diffuse = Vec.dot c n |> abs

            let l = ambient + (1.0f - ambient) * diffuse

            return V4f(v.c.XYZ * l, v.c.W)
        }

    let Effect = 
        toEffect simpleLighting

