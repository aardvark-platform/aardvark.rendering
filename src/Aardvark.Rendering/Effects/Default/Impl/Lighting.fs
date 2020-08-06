namespace Aardvark.Base.Rendering.Effects

open Aardvark.Base
open Aardvark.Base.Rendering
open FShade

module Lighting = 

    let private specular =
        sampler2d {
            texture uniform?SpecularColorTexture
            filter Filter.MinMagMipLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    type UniformScope with
        member x.HasSpecularColorTexture : bool = x?HasSpecularColorTexture

    let internal lighting (twoSided : bool) (v : Vertex) =
        fragment {
            let n = v.n |> Vec.normalize
            let c = uniform.LightLocation - v.wp.XYZ |> Vec.normalize
            let l = c
            let h = c

            let ambient = 0.1
            let diffuse = 
                if twoSided then Vec.dot l n |> abs
                else Vec.dot l n |> max 0.0

            let s = Vec.dot h n 

            let l = ambient + (1.0 - ambient) * diffuse

            let spec =
                if uniform.HasSpecularColorTexture then 
                    let v = specular.Sample(v.tc).XYZ
                    v.X * V3d.III
                else V3d.III

            return V4d(v.c.XYZ * l + spec * pown s 32, v.c.W)
        }

    let Effect (twoSided : bool)= 
        toEffect (lighting twoSided)
