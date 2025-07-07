namespace Aardvark.Rendering.Effects

open Aardvark.Base
open Aardvark.Rendering
open FShade

module ViewSizedPointSprites =
    
    let internal viewSizedPointSprites (p : Point<Vertex>) =
        triangle {
            let ratio = V2f uniform.ViewportSize
            let s = uniform.PointSize * V2f(ratio.Y / ratio.X, 1.0f) * 0.5f
            let pos = p.Value.pos
            let pxyz = pos.XYZ / pos.W

            let p00 = V3f(pxyz + V3f( -s.X, -s.Y, 0.0f ))
            let p01 = V3f(pxyz + V3f( -s.X,  s.Y, 0.0f ))
            let p10 = V3f(pxyz + V3f(  s.X, -s.Y, 0.0f ))
            let p11 = V3f(pxyz + V3f(  s.X,  s.Y, 0.0f ))

            yield { p.Value with pos = V4f(p00 * pos.W, pos.W); tc = V2f.OO }
            yield { p.Value with pos = V4f(p10 * pos.W, pos.W); tc = V2f.IO }
            yield { p.Value with pos = V4f(p01 * pos.W, pos.W); tc = V2f.OI }
            yield { p.Value with pos = V4f(p11 * pos.W, pos.W); tc = V2f.II }

        }

    let Effect =
        toEffect viewSizedPointSprites

