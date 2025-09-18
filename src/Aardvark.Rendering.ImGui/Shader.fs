namespace Aardvark.Rendering.ImGui

open Aardvark.Base
open Aardvark.Rendering
open FShade

module internal Shader =

    let private diffuseSampler =
        sampler2d {
            texture uniform?DiffuseColorTexture
            filter Filter.MinMagLinear
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
        }

    type private Vertex =
        {
            [<Position>] p  : V4f
            [<TexCoord>] tc : V2f
            [<Color>]    c  : V4f
        }

    let private vs (v : Vertex) =
        vertex {
            return { v with p = uniform.ProjTrafo * v.p.XYOI }
        }

    let private fs (v: Vertex) =
        fragment {
            let diffuse = diffuseSampler.Sample(V2f(v.tc.X, 1.0f - v.tc.Y))
            return v.c.ZYXW * diffuse
        }

    let Effect =
        Effect.compose [
            toEffect vs
            toEffect fs
        ]