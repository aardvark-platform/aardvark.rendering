namespace Aardvark.Base.Rendering.Effects

open Aardvark.Base
open Aardvark.Base.Rendering
open FShade

module SimpleLighting = 

    let internal simpleLighting (v : Vertex) =
        fragment {
            let n = v.n |> Vec.normalize
            let c = uniform.LightLocation - v.wp.XYZ |> Vec.normalize

            let ambient = 0.2
            let diffuse = Vec.dot c n |> abs

            let l = ambient + (1.0 - ambient) * diffuse

            return V4d(v.c.XYZ * diffuse, v.c.W)
        }

    type Vertex = {
        [<Position>]                pos     : V4d
        [<Normal>]                  n       : V3d
        [<BiNormal>]                b       : V3d
        [<Tangent>]                 t       : V3d
        [<Color>]                   c       : V4d
        [<Semantic("LightDir")>]    ldir    : V3d
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

            let ambient = 0.2
            let diffuse = Vec.dot c n |> abs

            let l = ambient + (1.0 - ambient) * diffuse

            return V4d(v.c.XYZ * diffuse, v.c.W)
        }

    let Effect = 
        toEffect simpleLighting

