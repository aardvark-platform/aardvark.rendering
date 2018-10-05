open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

// This example illustrates how to render a simple triangle using aardvark.

[<EntryPoint>]
let main argv = 
    
    Ag.initialize()
    Aardvark.Init()

    // window { ... } is similar to show { ... } but instead
    // of directly showing the window we get the window-instance
    // and may show it later.
    let win =
        window {
            backend Backend.Vulkan
            display Display.Mono
            debug true
            samples 8
        }

    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

    let sg = 
        // create a red box with a simple shader
        Sg.box (Mod.constant color) (Mod.constant box)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.simpleLighting
            }
    
    // show the window
    win.Scene <- sg
    win.Run()

    0
