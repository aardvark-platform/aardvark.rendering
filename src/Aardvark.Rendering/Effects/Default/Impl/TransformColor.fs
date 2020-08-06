namespace Aardvark.Base.Rendering.Effects

open Aardvark.Base
open Aardvark.Base.Rendering
open FShade
open Microsoft.FSharp.Quotations

module TransformColor = 

    let internal transformColor (f : Expr<V4d -> V4d>) (v : Vertex) =
        fragment {
            return (%f) v.c
        }

    let Effect (f : Expr<V4d -> V4d>) =
        toEffect (transformColor f)
