namespace Aardvark.Rendering.Effects

open Aardvark.Base
open Aardvark.Rendering
open FShade

module ThickLine = 

    type ThickLineVertex = {
        [<Position>]                pos     : V4d
        [<Color>]                   c       : V4d
        [<Semantic("LineCoord")>]   lc      : V2d
        [<Semantic("Width")>]       w       : float
    }

    [<ReflectedDefinition>]
    let clipLine (plane : V4d) (p0 : ref<V4d>) (p1 : ref<V4d>) =
        let h0 = Vec.dot plane !p0
        let h1 = Vec.dot plane !p1

        // h = h0 + (h1 - h0)*t
        // 0 = h0 + (h1 - h0)*t
        // (h0 - h1)*t = h0
        // t = h0 / (h0 - h1)
        if h0 > 0.0 && h1 > 0.0 then
            false
        elif h0 < 0.0 && h1 > 0.0 then
            let t = h0 / (h0 - h1)
            p1 := !p0 + t * (!p1 - !p0)
            true
        elif h1 < 0.0 && h0 > 0.0 then
            let t = h0 / (h0 - h1)
            p0 := !p0 + t * (!p1 - !p0)
            true
        else
            true

    [<ReflectedDefinition>]
    let clipLinePure (plane : V4d) (p0 : V4d) (p1 : V4d) =
        let h0 = Vec.dot plane p0
        let h1 = Vec.dot plane p1

        // h = h0 + (h1 - h0)*t
        // 0 = h0 + (h1 - h0)*t
        // (h0 - h1)*t = h0
        // t = h0 / (h0 - h1)
        if h0 > 0.0 && h1 > 0.0 then
            (false, p0, p1)
        elif h0 < 0.0 && h1 > 0.0 then
            let t = h0 / (h0 - h1)
            let p11 = p0 + t * (p1 - p0)
            (true, p0, p11)
        elif h1 < 0.0 && h0 > 0.0 then
            let t = h0 / (h0 - h1)
            let p01 = p0 + t * (p1 - p0)
            
            (true, p01, p1)
        else
            (true, p0, p1)

    let internal thickLine (line : Line<ThickLineVertex>) =
        triangle {
            let t = uniform.LineWidth
            let sizeF = V3d(float uniform.ViewportSize.X, float uniform.ViewportSize.Y, 1.0)

            let mutable pp0 = line.P0.pos
            let mutable pp1 = line.P1.pos

            let w = 1.0
            
            //let (a0, pp0, pp1) = clipLinePure (V4d( 1.0,  0.0,  0.0, -w)) pp0 pp1
            //let (a1, pp0, pp1) = clipLinePure (V4d(-1.0,  0.0,  0.0, -w)) pp0 pp1
            //let (a2, pp0, pp1) = clipLinePure (V4d( 0.0,  1.0,  0.0, -w)) pp0 pp1
            //let (a3, pp0, pp1) = clipLinePure (V4d( 0.0, -1.0,  0.0, -w)) pp0 pp1
            //let (a4, pp0, pp1) = clipLinePure (V4d( 0.0,  0.0,  1.0, -1.0)) pp0 pp1
            //let (a5, pp0, pp1) = clipLinePure (V4d( 0.0,  0.0, -1.0, -1.0)) pp0 pp1
            
            let add = 2.0 * V2d(t,t) / sizeF.XY

            // x = w

            // p' = p / p.w
            // p' € [-1,1]
            // p' € [-1-add.X,1+add.X]


            // p.x - (1+add.X)*p.w = 0



            let a0 = clipLine (V4d( 1.0,  0.0,  0.0, -(1.0 + add.X))) &&pp0 &&pp1
            let a1 = clipLine (V4d(-1.0,  0.0,  0.0, -(1.0 + add.X))) &&pp0 &&pp1
            let a2 = clipLine (V4d( 0.0,  1.0,  0.0, -(1.0 + add.Y))) &&pp0 &&pp1
            let a3 = clipLine (V4d( 0.0, -1.0,  0.0, -(1.0 + add.Y))) &&pp0 &&pp1
            let a4 = clipLine (V4d( 0.0,  0.0,  1.0, -1.0)) &&pp0 &&pp1
            let a5 = clipLine (V4d( 0.0,  0.0, -1.0, -1.0)) &&pp0 &&pp1

            if a0 && a1 && a2 && a3 && a4 && a5 then
                let p0 = pp0.XYZ / pp0.W
                let p1 = pp1.XYZ / pp1.W

                let fwp = (p1.XYZ - p0.XYZ) * sizeF

                let fw = V3d(fwp.XY, 0.0) |> Vec.normalize
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