namespace Aardvark.Base.Rendering

open Aardvark.Base
open Aardvark.Base.Incremental
open FShade
open Microsoft.FSharp.Quotations
open DefaultSurfaceVertex

module SgColor =

    let sgColor (v : Vertex) =
            fragment {
                let c : V4d = uniform?Color
                return c
            }

    let Effect = 
        toEffect sgColor