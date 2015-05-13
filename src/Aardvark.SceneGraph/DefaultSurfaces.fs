namespace Aardvark.SceneGraph

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

    let thickLine (width : float) (line : Line<Vertex>) =
        triangle {
            let p0 = line.[0].pos.XYZ / line.[0].pos.W
            let p1 = line.[1].pos.XYZ / line.[1].pos.W

            let fw = p1 - p0
            let r = V3d(-fw.Y, fw.X, fw.Z)


            let p00 = width

            yield line.[0]
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
