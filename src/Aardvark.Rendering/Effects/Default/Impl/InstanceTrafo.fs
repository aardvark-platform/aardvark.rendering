namespace Aardvark.Rendering.Effects

open Aardvark.Base
open Aardvark.Rendering
open FShade

module InstanceTrafo = 
    
    type InstanceVertex = { 
        [<Position>]            pos   : V4f
        [<Normal>]              n     : V3f
        [<BiNormal>]            b     : V3f
        [<Tangent>]             t     : V3f
        [<InstanceTrafo>]       trafo : M44f
        [<InstanceTrafoInv>]    trafoInv : M44f
    }

    let internal instanceTrafo (v : InstanceVertex) =
        vertex {
            return 
                { v with 
                    pos = v.trafo * v.pos 
                    n = v.trafoInv.TransposedTransformDir(v.n)
                    b = v.trafo.TransformDir(v.b)
                    t = v.trafo.TransformDir(v.t)
                }
        }

    let Effect = 
        toEffect instanceTrafo

