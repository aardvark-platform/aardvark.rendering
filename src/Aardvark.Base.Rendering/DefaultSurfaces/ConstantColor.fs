namespace Aardvark.Base.Rendering.Effects

open Aardvark.Base
open Aardvark.Base.Rendering
open FShade

module ConstantColor = 

    let internal constantColor (c : C4f) (v : Vertex) =
        let c = c.ToV4d()
        fragment {
            return c
        }

    let Effect (c : C4f) = 
        toEffect (constantColor c)