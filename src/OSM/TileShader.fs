namespace OSM

open Aardvark.Base
open Aardvark.SceneGraph
open FShade


[<AutoOpen>]
module Shader = 
    type Vertex = { [<Position>] pos : V4d
                    [<TexCoord>] tc : V2d }

    let vertex (v : Vertex) =
        vertex {
            return { v with pos = uniform.ModelTrafo * v.pos }
        }

    let diffuseTex = 
        sampler2d {
            texture uniform?DiffuseColorTexture
            addressU WrapMode.Mirror
            addressV WrapMode.Mirror
            filter Filter.MinMagLinear
        }

    let fragment (v : Vertex) =
        fragment {
            let color = diffuseTex.Sample(v.tc)
            return color
        }
