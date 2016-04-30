namespace Aardvark.Base.Rendering

open Aardvark.Base
open Aardvark.Base.Incremental
open FShade

module DefaultSurfaces =
    
    type Vertex = {
        [<Position>]        pos     : V4d
        [<WorldPosition>]   wp      : V4d
        [<Normal>]          n       : V3d
        [<BiNormal>]        b       : V3d
        [<Tangent>]         t       : V3d
        [<Color>]           c       : V4d
        [<TexCoord>]        tc      : V2d
    }

    let trafo (v : Vertex) =
        vertex {
            let wp = uniform.ModelTrafo * v.pos
            return {
                pos = uniform.ViewProjTrafo * wp
                wp = wp
                n = uniform.NormalMatrix * v.n
                b = uniform.NormalMatrix * v.b
                t = uniform.NormalMatrix * v.t
                c = v.c
                tc = v.tc
            }
        }

    let pointSurface (size : IMod<V2d>) (p : Point<Vertex>) =
        triangle {
            let pos = p.Value.pos
            let pxyz = pos.XYZ / pos.W
            let s = !!size
            //let v = p.Value

            let p00 = V3d(pxyz + V3d( -s.X, -s.Y, 0.0 ))
            let p01 = V3d(pxyz + V3d( -s.X,  s.Y, 0.0 ))
            let p10 = V3d(pxyz + V3d(  s.X, -s.Y, 0.0 ))
            let p11 = V3d(pxyz + V3d(  s.X,  s.Y, 0.0 ))

            yield { p.Value with pos = V4d(p00 * pos.W, pos.W); tc = V2d.OO }
            yield { p.Value with pos = V4d(p10 * pos.W, pos.W); tc = V2d.IO }
            yield { p.Value with pos = V4d(p01 * pos.W, pos.W); tc = V2d.OI }
            yield { p.Value with pos = V4d(p11 * pos.W, pos.W); tc = V2d.II }

        }


    type ThickLineVertex = {
        [<Position>]                pos     : V4d
        [<Color>]                   c       : V4d
        [<Semantic("LineCoord")>]   lc      : V2d
        [<Semantic("Width")>]       w       : float
    }

    let thickLine (line : Line<ThickLineVertex>) =
        triangle {
            let t = uniform.LineWidth
            let sizeF = V3d(float uniform.ViewportSize.X, float uniform.ViewportSize.Y, 1.0)

            let w0 = max line.P0.pos.W 0.001
            let w1 = max line.P1.pos.W 0.001
            let p0 = line.P0.pos.XYZ / w0
            let p1 = line.P1.pos.XYZ / w1

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
            yield { line.P0 with pos = V4d(p10, 1.0); lc = V2d(1.0, -rel); w = rel }
            yield { line.P1 with pos = V4d(p01, 1.0); lc = V2d(-1.0, 1.0 + rel); w = rel }
            yield { line.P1 with pos = V4d(p11, 1.0); lc = V2d(1.0, 1.0 + rel); w = rel }

        }
        
    let thickLineRoundCaps (v : ThickLineVertex) =
        fragment {
            if v.lc.Y < 0.0 then
                let tc = v.lc / V2d(1.0, v.w)
                if tc.Length > 1.0 then discard()

            elif v.lc.Y >= 1.0 then
                let tc = (v.lc - V2d.OI) / V2d(1.0, v.w)
                if tc.Length > 1.0 then discard()


            return v.c
        }
    
    let thickLineSparePointSizeCaps (v : ThickLineVertex) =
        fragment {
            let r = uniform.PointSize / uniform.LineWidth
            if v.lc.Y < 0.5 then
                let tc = v.lc / V2d(1.0, v.w)
                if v.lc.Y < 0.0 || tc.Length < r then discard()

            else
                let tc = (v.lc - V2d.OI) / V2d(1.0, v.w)
                if v.lc.Y > 1.0 || tc.Length < r then discard()


            return v.c
        }


    let pointSprite (p : Point<Vertex>) =
        triangle {
            let s = uniform.PointSize / V2d uniform.ViewportSize
            let pos = p.Value.pos
            let pxyz = pos.XYZ / pos.W

            let p00 = V3d(pxyz + V3d( -s.X, -s.Y, 0.0 ))
            let p01 = V3d(pxyz + V3d( -s.X,  s.Y, 0.0 ))
            let p10 = V3d(pxyz + V3d(  s.X, -s.Y, 0.0 ))
            let p11 = V3d(pxyz + V3d(  s.X,  s.Y, 0.0 ))

            yield { p.Value with pos = V4d(p00 * pos.W, pos.W); tc = V2d.OO }
            yield { p.Value with pos = V4d(p10 * pos.W, pos.W); tc = V2d.IO }
            yield { p.Value with pos = V4d(p01 * pos.W, pos.W); tc = V2d.OI }
            yield { p.Value with pos = V4d(p11 * pos.W, pos.W); tc = V2d.II }

        }

    let viewSizedPointSprites (p : Point<Vertex>) =
        triangle {
            let ratio = V2d uniform.ViewportSize
            let s = uniform.PointSize * V2d(ratio.Y / ratio.X, 1.0) * 0.5
            let pos = p.Value.pos
            let pxyz = pos.XYZ / pos.W

            let p00 = V3d(pxyz + V3d( -s.X, -s.Y, 0.0 ))
            let p01 = V3d(pxyz + V3d( -s.X,  s.Y, 0.0 ))
            let p10 = V3d(pxyz + V3d(  s.X, -s.Y, 0.0 ))
            let p11 = V3d(pxyz + V3d(  s.X,  s.Y, 0.0 ))

            yield { p.Value with pos = V4d(p00 * pos.W, pos.W); tc = V2d.OO }
            yield { p.Value with pos = V4d(p10 * pos.W, pos.W); tc = V2d.IO }
            yield { p.Value with pos = V4d(p01 * pos.W, pos.W); tc = V2d.OI }
            yield { p.Value with pos = V4d(p11 * pos.W, pos.W); tc = V2d.II }

        }



    let pointSpriteFragment (v : Vertex) =
        fragment {
            let c = 2.0 * v.tc - V2d.II
            if c.Length > 1.0 then
                discard()

            return v.c
        }

    let uniformColor (c : IMod<C4f>) (v : Vertex) =
        let c = c |> Mod.map (fun col -> col.ToV4d())
        fragment {
            return !!c
        }

    let constantColor (c : C4f) (v : Vertex) =
        let c = c.ToV4d()
        fragment {
            return c
        }

    let sgColor (v : Vertex) =
        fragment {
            let c : V4d = uniform?Color
            return c
        }

    let vertexColor (v : Vertex) =
        fragment {
            return v.c
        }

    let simpleLighting (v : Vertex) =
        fragment {
            let n = v.n |> Vec.normalize
            let c = uniform.CameraLocation - v.wp.XYZ |> Vec.normalize

            let ambient = 0.2
            let diffuse = Vec.dot c n |> abs

            let l = ambient + (1.0 - ambient) * diffuse

            return V4d(v.c.XYZ * diffuse, v.c.W)
        }

    let private diffuseSampler =
        sampler2d {
            texture uniform?DiffuseColorTexture
            filter Filter.MinMagMipLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let diffuseTexture (v : Vertex) =
        fragment {
            let texColor = diffuseSampler.Sample(v.tc)
            return texColor
        }

    let private normalSampler =
        sampler2d {
            texture uniform?NormalMapTexture
            filter Filter.MinMagMipLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let normalMap (v : Vertex) =
        fragment {
            let texColor = normalSampler.Sample(v.tc).XYZ
            let texNormal = 2.0 * texColor - V3d.III |> Vec.normalize

            let n = v.n * texNormal.Z + v.b * texNormal.X + v.t * texNormal.Y |> Vec.normalize

            return { v with n = n }
        }
