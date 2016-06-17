namespace Aardvark.Base.Rendering.Effects

open Aardvark.Base
open Aardvark.Base.Rendering
open FShade

module VertexColor = 

    let internal vertexColor (v : Vertex) =
        fragment {
            return v.c
        }

    let Effect =
        toEffect vertexColor

