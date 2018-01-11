namespace Examples


open System
open System.IO
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.Text
open System.Runtime.InteropServices
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO


module Sponza =
    let run() =

        let win = 
            window {
                display Display.Mono
                samples 8
                backend Backend.Vulkan
                debug true
            }

        let sg = 
            Aardvark.SceneGraph.IO.Loader.Assimp.loadFrom @"C:\Users\steinlechner\Desktop\Sponza bunt\sponza_cm.obj" Loader.Assimp.defaultFlags
                |> Sg.adapter
                |> Sg.scale (0.01)
                |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero))
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.diffuseTexture
                    do! DefaultSurfaces.simpleLighting
                }

        win.Scene <- sg

        win.Run()
