namespace Aardvark.Rendering.Effects

open Aardvark.Base
open Aardvark.Rendering
open FShade

module NormalMap =
 
    let private normalSampler =
        sampler2d {
            texture uniform?NormalMapTexture
            filter Filter.MinMagMipLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let internal normalMap (v : Vertex) =
        fragment {
            let texColor = normalSampler.Sample(v.tc).XYZ
            let texNormal = (2.0 * texColor - V3d.III) |> Vec.normalize

            // make sure tangent space basis is orthonormal -> perform gram-smith normalization
            let n = v.n.Normalized
            let t = v.t.Normalized
            let t = (t - n * (Vec.dot t n)) |> Vec.normalize
            let b = (Vec.cross n t) |> Vec.normalize // NOTE: v.b might be used here to maintain handedness
                        
            // texture normal from tangent to world space
            let n = 
                texNormal.X * t +
                texNormal.Y * b +
                texNormal.Z * n
            
            // NOTE: the tangent space basis with 't' and 'b' might rebuilt with the normal 'n' here and passed forward (e.g. for specular lobe sampling)

            return { v with n = n } 
        }

    let Effect = 
        toEffect normalMap

