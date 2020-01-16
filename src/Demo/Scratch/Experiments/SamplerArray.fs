namespace Scratch

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

module SamplerArray =
    
    module Shader =
        open FShade

        let samplers =
            [|
                sampler2d {
                    texture uniform?TexA
                    filter Filter.Anisotropic
                    addressU WrapMode.Wrap
                    addressV WrapMode.Wrap
                }

                sampler2d {
                    texture uniform?TexB
                    filter Filter.Anisotropic
                    addressU WrapMode.Wrap
                    addressV WrapMode.Wrap
                }
            |]

        let anyTex (v : Effects.Vertex) =
            fragment {
                let index = if v.tc.X > 0.5 then 1 else 0
                let mutable res = V4d.Zero
                for i in 0 .. 1 do
                    let factor = if index = i then 1.0 else 0.0
                    let v = samplers.[i].Sample(v.tc)
                    res <- res + factor * v

                return res
            }

    [<Demo("Sampler Array")>]
    let run() =
        Sg.fullScreenQuad
            |> Sg.fileTexture (Symbol.Create "TexA") @"E:\Development\WorkDirectory\DataSVN\grass_color.jpg" true
            |> Sg.fileTexture (Symbol.Create "TexB") @"E:\Development\WorkDirectory\DataSVN\cliffs_color.jpg" true
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! Shader.anyTex
               }

