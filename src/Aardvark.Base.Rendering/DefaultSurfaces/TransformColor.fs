namespace Aardvark.Base.Rendering

open Aardvark.Base
open Aardvark.Base.Incremental
open FShade
open Microsoft.FSharp.Quotations
open DefaultSurfaceVertex

module TransformColor = 

    let transformColor (f : Expr<V4d -> V4d>) (v : Vertex) =
        fragment {
            return (%f) v.c
        }

    let Effect (f : Expr<V4d -> V4d>) =
        toEffect (transformColor f)
