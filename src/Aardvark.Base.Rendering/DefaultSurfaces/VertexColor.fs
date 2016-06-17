namespace Aardvark.Base.Rendering.Effects

open Aardvark.Base
open Aardvark.Base.Rendering
open FShade
open DefaultSurfaceVertex

module VertexColor = 

    let internal vertexColor (v : Vertex) =
        fragment {
            return v.c
        }

    let Effect =
        toEffect vertexColor

