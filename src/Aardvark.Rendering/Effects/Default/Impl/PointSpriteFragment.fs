namespace Aardvark.Rendering.Effects

open Aardvark.Base
open Aardvark.Rendering
open FShade

module PointSpriteFragment = 

    let internal pointSpriteFragment (v : Vertex) =
        fragment {
            let c = 2.0f * v.tc - V2f.II
            if c.Length > 1.0f then
                discard()

            let z = sqrt (1.0f - c.LengthSquared)
            let n = V3f(c.XY,z)

            return { v with n = n } 
        }

    let Effect =
        toEffect pointSpriteFragment

