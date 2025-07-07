namespace Aardvark.Rendering.Effects

open Aardvark.Base
open Aardvark.Rendering
open FShade

module SgColor =

    let internal sgColor (v : Vertex) =
            fragment {
                let c : V4f = uniform?Color
                return c
            }

    let Effect = 
        toEffect sgColor