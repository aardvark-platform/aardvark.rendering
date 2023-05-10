open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim

open FSharp.Data.Adaptive
open System.Threading


[<EntryPoint>]
let main argv = 

    Aardvark.Init()

    use app = new OpenGlApplication()

    let win = app.CreateGameWindow(samples = 4)

    let initialView = CameraView.LookAt(V3d(3.0), V3d.Zero, V3d.OOI)

    let frustum = 
        win.Sizes 
            |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

    let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    let box = Primitives.unitBox
                    
    let instances = cset<ISg>()

    let sg = Sg.Set(instances :> aset<_>)
                |> Sg.effect [
                                DefaultSurfaces.trafo                 |> toEffect
                                DefaultSurfaces.constantColor C4f.Red |> toEffect
                             ]
                |> Sg.viewTrafo (cameraView |> AVal.map CameraView.viewTrafo )
                |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo )

    let renderTask = 
        app.Runtime.CompileRender(win.FramebufferSignature, sg)

    // trigger to pause updates:
    // allows to make more "stable" memory snapshots (not in between the add/remove)
    let mutable performUpdates = true
    win.Keyboard.KeyDown(Keys.Enter).Values.Add(fun _ ->
        performUpdates <- not performUpdates
    )

    let r = new Random()

    let renderTask = 
        RenderTask.custom (fun (self,token,outputDesc) -> 
            win.Time.GetValue self |> ignore

            if performUpdates then

                // NOTE: Leak fixed in Aardvark.Assembler 0.0.8 
                let cnt = r.Next(100) // fast leak ~5mb/s
                //let cnt = 100 // very slow leak

                transact(fun () -> 
                    instances.Clear()
                    instances.AddRange(Array.init cnt (fun _ -> box 
                                                                    |> Sg.ofIndexedGeometry 
                                                                    |> Sg.trafo (AVal.constant (Trafo3d.Translation(r.NextDouble() * 10.0 - 5.0, r.NextDouble() * 10.0 - 5.0, 0.0)))
                                                      ))
                    )
            else 
                if instances.Count > 0 then
                    transact(fun () -> 
                        instances.Clear())
            
            renderTask.Run(self, token, outputDesc)
        )

    win.RenderTask <- renderTask
    win.Run()
    0
