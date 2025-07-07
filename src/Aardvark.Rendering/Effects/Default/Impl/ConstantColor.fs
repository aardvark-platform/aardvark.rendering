namespace Aardvark.Rendering.Effects

open Aardvark.Base
open Aardvark.Rendering
open FShade

module ConstantColor = 

    let internal constantColor (c : C4f) (v : Vertex) =
        let c = c.ToV4f()
        fragment {
            return c
        }

    let Effect (c : C4f) = 
        toEffect (constantColor c)