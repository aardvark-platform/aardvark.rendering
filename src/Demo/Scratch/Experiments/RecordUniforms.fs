namespace Scrach

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg
open FShade

module RecordUniforms =

    type TestRecord = { r : float; g : float; b : float }

    type UniformScope with
        member x.Hugo : Arr<2 N, TestRecord> = x?Hugo

    module Shader =
        open FShade


        let shader (v : Effects.Vertex) =
            fragment {
                let hugo = uniform.Hugo.[1]
                return V4d(hugo.r, hugo.g,hugo.b, 1.0)
            }

    [<Demo("AAAUniformTest")>]
    let run () =
        
        let sg = 
            Sg.fullScreenQuad
            |> Sg.effect [ Shader.shader |> toEffect ]
            |> Sg.uniform "Hugo" (Mod.constant [| { r = 1.0; g = 0.0; b = 1.0; }; { r = 1.0; g = 1.0; b = 0.0; } |])

        sg