namespace Aardvark.Base.Rendering.Effects

open Aardvark.Base
open Aardvark.Base.Rendering
open FShade

module ThickLine = 

    type ThickLineVertex = {
        [<Position>]                pos     : V4d
        [<Color>]                   c       : V4d
        [<Semantic("LineCoord")>]   lc      : V2d
        [<Semantic("Width")>]       w       : float
    }

    [<GLSLIntrinsic("mix({0}, {1}, {2})")>]
    let Lerp (a : V4d) (b : V4d) (s : float) : V4d = failwith ""

    let internal thickLine (line : Line<ThickLineVertex>) =
        triangle {
            let t = uniform.LineWidth
            let sizeF = V3d(float uniform.ViewportSize.X, float uniform.ViewportSize.Y, 1.0)

            let pp0 = line.P0.pos
            let pp1 = line.P1.pos

            let pp0 = if pp0.Z < 0.0 then (Lerp pp1 pp0 (pp1.Z / (pp1.Z - pp0.Z))) else pp0
            let pp1 = if pp1.Z < 0.0 then (Lerp pp0 pp1 (pp0.Z / (pp0.Z - pp1.Z))) else pp1

            let p0 = pp0.XYZ / pp0.W
            let p1 = pp1.XYZ / pp1.W

            let fwp = (p1.XYZ - p0.XYZ) * sizeF

            let fw = V3d(fwp.XY * 2.0, 0.0) |> Vec.normalize
            let r = V3d(-fw.Y, fw.X, 0.0) / sizeF
            let d = fw / sizeF
            let p00 = p0 - r * t - d * t
            let p10 = p0 + r * t - d * t
            let p11 = p1 + r * t + d * t
            let p01 = p1 - r * t + d * t

            let rel = t / (Vec.length fwp)

            yield { line.P0 with pos = V4d(p00, 1.0); lc = V2d(-1.0, -rel); w = rel }
            yield { line.P0 with pos = V4d(p10, 1.0); lc = V2d( 1.0, -rel); w = rel }
            yield { line.P1 with pos = V4d(p01, 1.0); lc = V2d(-1.0, 1.0 + rel); w = rel }
            yield { line.P1 with pos = V4d(p11, 1.0); lc = V2d( 1.0, 1.0 + rel); w = rel }
        }

    let Effect = 
        toEffect thickLine