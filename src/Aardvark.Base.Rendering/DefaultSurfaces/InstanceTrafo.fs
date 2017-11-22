namespace Aardvark.Base.Rendering.Effects

open Aardvark.Base
open Aardvark.Base.Rendering
open FShade

module InstanceTrafo = 
    
    type InstanceVertex = { 
        [<Position>]      pos   : V4d 
        [<Normal>]        n     : V3d 
        [<BiNormal>]      b     : V3d 
        [<Tangent>]       t     : V3d 
        [<InstanceTrafo>] trafo : M44d
    }

    let internal instanceTrafo (v : InstanceVertex) =
        vertex {
            return 
                { v with 
                    pos = v.trafo * v.pos 
                    n = v.trafo.TransformDir(v.n)
                    b = v.trafo.TransformDir(v.b)
                    t = v.trafo.TransformDir(v.t)
                }
        }

    let Effect = 
        toEffect instanceTrafo

