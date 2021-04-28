namespace Aardvark.Rendering.Effects

open Aardvark.Rendering
open FShade

module VertexColor = 

    let internal vertexColor (v : Vertex) =
        fragment {
            return v.c
        }

    let Effect =
        toEffect vertexColor

