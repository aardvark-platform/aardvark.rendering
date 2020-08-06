open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open System.Threading
[<EntryPoint>]
let main argv = 
    
    
    Aardvark.Init()

    // window { ... } is similar to show { ... } but instead
    // of directly showing the window we get the window-instance
    // and may show it later.
    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug false
            samples 8
        }


    // define a dynamic transformation depending on the window's time
    // This time is a special value that can be used for animations which
    // will be evaluated when rendering the scene
    let dynamicTrafo = //Trafo3d.Identity |> AVal.constant
        let startTime = System.DateTime.Now
        win.Time |> AVal.map (fun t ->
            let t = (t - startTime).TotalSeconds
            Trafo3d.RotationZ (0.5 * t)
        )

    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

    let sg = 
        // create a red box with a simple shader
        Sg.box (AVal.constant color) (AVal.constant box)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.simpleLighting
            }

            // apply the dynamic transformation to the box
            |> Sg.trafo dynamicTrafo
    
    let run () = 
        // show the window
        win.Scene <- sg
        win.Run()

    //let t = Thread(ThreadStart run)
    //t.Start()
    //
    //win2.Scene <- sg
    //win2.Run()

    run()
    0
