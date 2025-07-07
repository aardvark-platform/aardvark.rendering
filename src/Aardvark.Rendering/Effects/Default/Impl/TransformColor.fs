namespace Aardvark.Rendering.Effects

open Aardvark.Base
open Aardvark.Rendering
open FShade
open Microsoft.FSharp.Quotations

module TransformColor = 

    let internal transformColor (f : Expr<V4f -> V4f>) (v : Vertex) =
        fragment {
            return (%f) v.c
        }

    let Effect (f : Expr<V4f -> V4f>) =
        toEffect (transformColor f)
