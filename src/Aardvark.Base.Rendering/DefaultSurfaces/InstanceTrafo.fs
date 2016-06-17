namespace Aardvark.Base.Rendering

open Aardvark.Base
open Aardvark.Base.Incremental
open FShade
open Microsoft.FSharp.Quotations

module InstanceTrafo = 
    
    type InstanceVertex = { 
        [<Position>]      pos   : V4d 
        [<InstanceTrafo>] trafo : M44d
    }

    let instanceTrafo (v : InstanceVertex) =
        vertex {
            return { v with pos = v.trafo * v.pos }
        }

    let Effect = 
        toEffect instanceTrafo

