﻿open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Rendering.Text
open Aardvark.Application
open System.Threading

open Aardvark.Base.Ag
// This example illustrates how to render a simple triangle using aardvark.

[<EntryPoint>]
let main argv = 
    let renderBackend = Backend.GL

    let debugConfig : IDebugConfig =
        if renderBackend = Backend.Vulkan then
            { Vulkan.DebugConfig.Normal with
                PrintRenderTaskRecompile = false }
        else
            DebugLevel.Normal

    Aardvark.Init()

    // window { ... } is similar to show { ... } but instead
    // of directly showing the window we get the window-instance
    // and may show it later.
    let win =
        window {
            backend renderBackend
            display Display.Mono
            debug debugConfig
            samples 1
        }

    let inputText = win.Time |> AVal.map (fun t -> 
                let str = t.ToString()
                let cnt = int ((t.TimeOfDay.TotalMilliseconds / 100.0) % 17.0)
                str.Substring(3, min cnt (str.Length - 3)))

    let textTrafo = win.Sizes |> AVal.map (fun size -> 
    
         let ar = float size.X / float size.Y
         let relHeight = 0.1
         let relPos = V2d(0.1, 0.1)
         let scale = Trafo3d.Scale(relHeight * 2.0 / ar, relHeight * 2.0, 1.0)
         let t = Trafo3d.Translation(-1.0 + relPos.X * 2.0 / ar,
                                      1.0 - relPos.Y * 2.0, 
                                      0.0)
         scale * t
    )

    let sg = Sg.text DefaultFonts.Hack.Regular C4b.White inputText
               |> Sg.viewTrafo (AVal.constant Trafo3d.Identity)
               |> Sg.projTrafo textTrafo
    
    // show the window
    win.Scene <- sg
    win.Run()

    0
