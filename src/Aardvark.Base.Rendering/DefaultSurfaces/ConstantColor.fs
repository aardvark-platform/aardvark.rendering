namespace Aardvark.Base.Rendering

open Aardvark.Base
open Aardvark.Base.Incremental
open FShade
open Microsoft.FSharp.Quotations
open DefaultSurfaceVertex

module ConstantColor = 

    let constantColor (c : C4f) (v : Vertex) =
        let c = c.ToV4d()
        fragment {
            return c
        }

    let Effect (c : C4f) = 
        toEffect (constantColor c)