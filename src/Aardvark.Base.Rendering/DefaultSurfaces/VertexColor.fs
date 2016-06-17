namespace Aardvark.Base.Rendering

open Aardvark.Base
open Aardvark.Base.Incremental
open FShade
open Microsoft.FSharp.Quotations
open DefaultSurfaceVertex

module VertexColor = 

    let vertexColor (v : Vertex) =
        fragment {
            return v.c
        }

    let Effect =
        toEffect vertexColor

