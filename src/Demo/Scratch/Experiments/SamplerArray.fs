namespace Scratch

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
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
                return samplers.[index].Sample(v.tc)
            }

    [<Demo("Sampler Array")>]
    let run() =
        Sg.fullScreenQuad
            |> Sg.fileTexture (Symbol.Create "TexA") @"C:\Users\Schorsch\Development\WorkDirectory\grass_color.jpg" true
            |> Sg.fileTexture (Symbol.Create "TexB") @"C:\Users\Schorsch\Development\WorkDirectory\cliffs_color.jpg" true
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! Shader.anyTex
               }

