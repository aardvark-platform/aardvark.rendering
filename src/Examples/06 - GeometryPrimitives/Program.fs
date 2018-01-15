open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

(* This example shows how to 
   - render some built-in geometric primities (sphere, box etc)
   - how create a window using the convenient window computation in order to
     create a window which we can use to subscribe to mouse and keyboard
*)

[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    Ag.initialize()
    Aardvark.Init()

    // Sg.box provides a scenegraph containing a box (which can be changed, as
    // indicated by the modifiable arguments).
    // Sg.box' is a static variant thereof
    let staticBox = Sg.box' C4b.Green (Box3d.FromMinAndSize(new V3d(-1, -2, 0), new V3d(15, 10, 1)))

    // now let us use the dynamic box (in order to change vertex attributes)
    let colors = [ C4b.Yellow; C4b.Green; C4b.Blue ]
    let mutable currentIndex = 0
    let boxColor = Mod.init colors.[currentIndex]
    
    let dynamicBox = Sg.box boxColor (Box3d.FromMinAndSize(new V3d(2, 1, 1), new V3d(3, 4, 2)) |> Mod.constant)

    // create a simple subdivision sphere (as with box the tick' version of the function 
    // can be used to generate a static sphere
    let sphere = Sg.sphere' 5 C4b.Red 2.0

    // quad can be used to create a simple quad, let us scale it using Sg.scale
    let groundPlane = 
        Sg.quad 
        |> Sg.vertexArray DefaultSemantic.Colors (Array.create 4 C4b.White)
        |> Sg.translate 0.5 0.5 0.0
        |> Sg.scale 10.0

    let boxes = 
        Sg.ofSeq [
            staticBox   
            dynamicBox  
            Sg.box' C4b.White (Box3d.FromMinAndSize(new V3d(2, 1, 3), new V3d(1, 1, 3)))
        ]

    let cylinder = 
        IndexedGeometryPrimitives.solidCylinder (V3d(25,5,0)) V3d.ZAxis 6.0 1.5 1.5 12 C4b.Blue

    let cone =
        IndexedGeometryPrimitives.solidCone (V3d(30,0,0)) V3d.ZAxis 5.0 3.0 128 C4b.White

    let scene =
        Sg.ofSeq [
            boxes
            sphere   |> Sg.translate 20.0 0.0 0.0
            cylinder |> Sg.ofIndexedGeometry 
            cone     |> Sg.ofIndexedGeometry
        ]
    
    let sg = 
        scene
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }
    

    // similarly to show, we can use the window computation expression in order create a window conveniently.
    use win = 
        window {
            display Display.Mono
            samples 8
            backend Backend.GL
            initialCamera (CameraView.lookAt (V3d.III * 20.0) V3d.OOO V3d.OOI)
            debug true
        }

    // quick and dirty keyboard bindings (Values is an Event, which is similar to observables)
    win.Keyboard.KeyDown(Keys.C).Values.Subscribe(fun _ -> 
        // cycle through the available colors
        currentIndex <- (currentIndex + 1) % colors.Length
        transact (fun _ -> 
            boxColor.Value <- colors.[currentIndex]
        )
    ) |> ignore // subscribe returns a Disposable in order to unregister, we ignore this one here.

    win.Scene <- sg
    win.Run()

    0
