namespace Aardvark.Base.Rendering.Effects

open Aardvark.Base
open Aardvark.Base.Rendering
open FShade
open DefaultSurfaceVertex

module InstanceTrafo = 
    
    type InstanceVertex = { 
        [<Position>]      pos   : V4d 
        [<InstanceTrafo>] trafo : M44d
    }

    let internal instanceTrafo (v : InstanceVertex) =
        vertex {
            return { v with pos = v.trafo * v.pos }
        }

    let Effect = 
        toEffect instanceTrafo

