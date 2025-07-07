namespace Aardvark.Rendering.Effects

open Aardvark.Base
open Aardvark.Rendering
open FShade

module ThickLineWithCulling = 

    type ThickLineVertexWithClipDistance = {
        [<Position>]                pos     : V4f
        [<Color>]                   c       : V4f
        [<Normal>]                  n       : V3f
        [<ClipDistance>]            cd      : float32[]
        [<Semantic("LineCoord")>]   lc      : V2f
        [<Semantic("Width")>]       w       : float32
    }
    
    let internal thickLineWithCulling (line : Line<ThickLineVertexWithClipDistance>) =
        triangle {
            let t = uniform.LineWidth
            let sizeF = V3f(uniform.ViewportSize, 1)

            let pp0 = line.P0.pos
            let pp1 = line.P1.pos

            // NOTE: clips by z=0 assuming DX clip space [0, 1] -> clipping too early in GL with default depth range [-1, 1]
            let pp0 = if pp0.Z < 0.0f then (lerp pp1 pp0 (pp1.Z / (pp1.Z - pp0.Z))) else pp0
            let pp1 = if pp1.Z < 0.0f then (lerp pp0 pp1 (pp0.Z / (pp0.Z - pp1.Z))) else pp1

            let p0 = pp0.XYZ / pp0.W
            let p1 = pp1.XYZ / pp1.W

            let fwp = (p1.XYZ - p0.XYZ) * sizeF

            let fw = V3f(fwp.XY * 2.0f, 0.0f) |> Vec.normalize
            let r = V3f(-fw.Y, fw.X, 0.0f) / sizeF
            let d = fw / sizeF
            let p00 = p0 - r * t - d * t
            let p10 = p0 + r * t - d * t
            let p11 = p1 + r * t + d * t
            let p01 = p1 - r * t + d * t

            let rel = t / (Vec.length fwp)

            let vn0 = uniform.ViewTrafo * V4f(line.P0.n, 0.0f)
            let vn1 = uniform.ViewTrafo * V4f(line.P1.n, 0.0f)

            yield { line.P0 with pos = V4f(p00, 1.0f); lc = V2f(-1.0f, -rel); w = rel; cd = [| vn0.Z |] }
            yield { line.P0 with pos = V4f(p10, 1.0f); lc = V2f( 1.0f, -rel); w = rel; cd = [| vn0.Z |] }
            yield { line.P1 with pos = V4f(p01, 1.0f); lc = V2f(-1.0f, 1.0f + rel); w = rel; cd = [| vn1.Z |] }
            yield { line.P1 with pos = V4f(p11, 1.0f); lc = V2f( 1.0f, 1.0f + rel); w = rel; cd = [| vn1.Z |] }
        }

    let Effect = 
        toEffect thickLineWithCulling