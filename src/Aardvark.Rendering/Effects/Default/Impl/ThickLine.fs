namespace Aardvark.Rendering.Effects

open Aardvark.Base
open Aardvark.Rendering
open FShade

module ThickLine = 

    type ThickLineVertex = {
        [<Position>]                pos     : V4f
        [<Color>]                   c       : V4f
        [<Semantic("LineCoord")>]   lc      : V2f
        [<Semantic("Width")>]       w       : float32
    }

    [<ReflectedDefinition>]
    let clipLine (plane : V4f) (p0 : ref<V4f>) (p1 : ref<V4f>) =
        let h0 = Vec.dot plane !p0
        let h1 = Vec.dot plane !p1

        // h = h0 + (h1 - h0)*t
        // 0 = h0 + (h1 - h0)*t
        // (h0 - h1)*t = h0
        // t = h0 / (h0 - h1)
        if h0 > 0.0f && h1 > 0.0f then
            false
        elif h0 < 0.0f && h1 > 0.0f then
            let t = h0 / (h0 - h1)
            p1 := !p0 + t * (!p1 - !p0)
            true
        elif h1 < 0.0f && h0 > 0.0f then
            let t = h0 / (h0 - h1)
            p0 := !p0 + t * (!p1 - !p0)
            true
        else
            true

    [<ReflectedDefinition>]
    let clipLinePure (plane : V4f) (p0 : V4f) (p1 : V4f) =
        let h0 = Vec.dot plane p0
        let h1 = Vec.dot plane p1

        // h = h0 + (h1 - h0)*t
        // 0 = h0 + (h1 - h0)*t
        // (h0 - h1)*t = h0
        // t = h0 / (h0 - h1)
        if h0 > 0.0f && h1 > 0.0f then
            (false, p0, p1)
        elif h0 < 0.0f && h1 > 0.0f then
            let t = h0 / (h0 - h1)
            let p11 = p0 + t * (p1 - p0)
            (true, p0, p11)
        elif h1 < 0.0f && h0 > 0.0f then
            let t = h0 / (h0 - h1)
            let p01 = p0 + t * (p1 - p0)
            
            (true, p01, p1)
        else
            (true, p0, p1)

    let internal thickLine (line : Line<ThickLineVertex>) =
        triangle {
            let t = uniform.LineWidth
            let sizeF = V3f(uniform.ViewportSize, 1)

            let mutable pp0 = line.P0.pos
            let mutable pp1 = line.P1.pos

            let w = 1.0f
            
            //let (a0, pp0, pp1) = clipLinePure (V4d( 1.0,  0.0,  0.0, -w)) pp0 pp1
            //let (a1, pp0, pp1) = clipLinePure (V4d(-1.0,  0.0,  0.0, -w)) pp0 pp1
            //let (a2, pp0, pp1) = clipLinePure (V4d( 0.0,  1.0,  0.0, -w)) pp0 pp1
            //let (a3, pp0, pp1) = clipLinePure (V4d( 0.0, -1.0,  0.0, -w)) pp0 pp1
            //let (a4, pp0, pp1) = clipLinePure (V4d( 0.0,  0.0,  1.0, -1.0)) pp0 pp1
            //let (a5, pp0, pp1) = clipLinePure (V4d( 0.0,  0.0, -1.0, -1.0)) pp0 pp1
            
            let add = 2.0f * V2f(t,t) / sizeF.XY

            // x = w

            // p' = p / p.w
            // p' € [-1,1]
            // p' € [-1-add.X,1+add.X]


            // p.x - (1+add.X)*p.w = 0



            let a0 = clipLine (V4f( 1.0f,  0.0f,  0.0f, -(1.0f + add.X))) &&pp0 &&pp1
            let a1 = clipLine (V4f(-1.0f,  0.0f,  0.0f, -(1.0f + add.X))) &&pp0 &&pp1
            let a2 = clipLine (V4f( 0.0f,  1.0f,  0.0f, -(1.0f + add.Y))) &&pp0 &&pp1
            let a3 = clipLine (V4f( 0.0f, -1.0f,  0.0f, -(1.0f + add.Y))) &&pp0 &&pp1
            let a4 = clipLine (V4f( 0.0f,  0.0f,  1.0f, -1.0f)) &&pp0 &&pp1
            let a5 = clipLine (V4f( 0.0f,  0.0f, -1.0f, -1.0f)) &&pp0 &&pp1

            if a0 && a1 && a2 && a3 && a4 && a5 then
                let p0 = pp0.XYZ / pp0.W
                let p1 = pp1.XYZ / pp1.W

                let fwp = (p1.XYZ - p0.XYZ) * sizeF

                let fw = V3f(fwp.XY, 0.0f) |> Vec.normalize
                let r = V3f(-fw.Y, fw.X, 0.0f) / sizeF
                let d = fw / sizeF
                let p00 = p0 - r * t - d * t
                let p10 = p0 + r * t - d * t
                let p11 = p1 + r * t + d * t
                let p01 = p1 - r * t + d * t

                let rel = t / (Vec.length fwp)

                yield { line.P0 with pos = V4f(p00, 1.0f); lc = V2f(-1.0f, -rel); w = rel }
                yield { line.P0 with pos = V4f(p10, 1.0f); lc = V2f( 1.0f, -rel); w = rel }
                yield { line.P1 with pos = V4f(p01, 1.0f); lc = V2f(-1.0f, 1.0f + rel); w = rel }
                yield { line.P1 with pos = V4f(p11, 1.0f); lc = V2f( 1.0f, 1.0f + rel); w = rel }
        }

    let Effect = 
        toEffect thickLine