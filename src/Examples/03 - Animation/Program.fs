open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

[<EntryPoint>]
let main argv = 
    
    Ag.initialize()
    Aardvark.Init()

    // window { ... } is similar to show { ... } but instead
    // of directly showing the window we get the window-instance
    // and may show it later.
    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug true
            samples 8
        }

    // define a dynamic transformation depending on the window's time
    // This time is a special value that can be used for animations which
    // will be evaluated when rendering the scene

    let sg = 
        let rand = RandomSystem()
        Sg.ofList [
            for x in -10 .. 10 do
                for y in -10 .. 10 do
                    let rand = rand.UniformDouble()
                    let dynamicTrafo =
                        let startTime = System.DateTime.Now
                        win.Time |> Mod.map (fun t ->
                            let t = (t - startTime).TotalSeconds
                            Trafo3d.RotationZ (0.5 * t + rand) * Trafo3d.Translation(V3d(float x, float y, 0.0))
                        )

                    let box = Box3d(-V3d.III * 0.2, V3d.III * 0.2)
                    let color = C4b.Red

                    let sg = 
                        // create a red box with a simple shader
                        Sg.box (Mod.constant color) (Mod.constant box)
                        // apply the dynamic transformation to the box
                        |> Sg.trafo dynamicTrafo

                    yield sg
        ]


    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

    let sg = 
        sg
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.simpleLighting
            }
    
    // show the window
    win.Scene <- sg
    win.Run()

    0
