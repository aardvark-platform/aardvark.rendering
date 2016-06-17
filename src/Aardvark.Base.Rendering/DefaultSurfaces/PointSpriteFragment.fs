namespace Aardvark.Base.Rendering

open Aardvark.Base
open Aardvark.Base.Incremental
open FShade
open Microsoft.FSharp.Quotations
open DefaultSurfaceVertex

module PointSpriteFragment = 

    let pointSpriteFragment (v : Vertex) =
        fragment {
            let c = 2.0 * v.tc - V2d.II
            if c.Length > 1.0 then
                discard()

            let z = sqrt (1.0 - c.LengthSquared)
            let n = V3d(c.XY,z)

            return { v with n = n } 
        }

    let Effect =
        toEffect pointSpriteFragment

