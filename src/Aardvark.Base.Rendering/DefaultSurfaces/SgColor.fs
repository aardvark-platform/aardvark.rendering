namespace Aardvark.Base.Rendering.Effects

open Aardvark.Base
open Aardvark.Base.Rendering
open FShade

module SgColor =

    let internal sgColor (v : Vertex) =
            fragment {
                let c : V4d = uniform?Color
                return c
            }

    let Effect = 
        toEffect sgColor