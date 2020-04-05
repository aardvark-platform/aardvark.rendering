namespace Examples


open System
open System.IO
open Aardvark.Base
open FSharp.Data.Adaptive

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open FSharp.Data.Adaptive.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.Text
open System.Runtime.InteropServices
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO
open FShade

module Sponza =
    let run() =
        
        let win = 
            window {
                display Display.Mono
                samples 8
                backend Backend.Vulkan

                debug false
            }

        let sg = 
            Aardvark.SceneGraph.IO.Loader.Assimp.loadFrom @"E:\Development\WorkDirectory\Sponza bunt\sponza_cm.obj" Loader.Assimp.defaultFlags
                |> Sg.adapter
                |> Sg.scale (0.01)
                |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero))
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.diffuseTexture
                    do! DefaultSurfaces.simpleLighting

//                    do! fun (v : Effects.Vertex) ->
//                        fragment {
//                            let mutable a = cos(v.tc.X)
//                            for i in 1 .. 2000 do
//                                a <- a * sin(float i) * cos(float i)
//                            return v.c * (1.0 + a)
//                        }

                }

        win.Scene <- sg

        win.Run()
