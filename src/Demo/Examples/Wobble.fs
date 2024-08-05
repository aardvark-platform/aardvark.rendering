namespace Examples


open System
open Aardvark.Base
open FSharp.Data.Adaptive

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open FSharp.Data.Adaptive.Operators
open Aardvark.Rendering
open Aardvark.Rendering.ShaderReflection
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Text


module Wobble =
    let run() =
        use app = new VulkanApplication(true)
        let win = app.CreateSimpleRenderWindow(8)

        //FShade.EffectDebugger.attach()

        let offset = AVal.init (V3d(1000000.0, 0.0, 0.0))
        
        let cameraView = 
            let mutable currentCam = CameraView.lookAt (V3d(6,-6,2)) V3d.Zero V3d.OOI
            let mutable oldoffset = V3d.Zero
            offset |> AVal.bind (fun o ->
                let init = currentCam.WithLocation(currentCam.Location + (o - oldoffset))
                oldoffset <- o
                init
                    |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
                    |> AVal.map (fun c -> currentCam <- c; c)
                    |> AVal.map CameraView.viewTrafo
            )

        let projection = 
            win.Sizes 
                |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
                |> AVal.map Frustum.projTrafo

        let font = Aardvark.Base.Fonts.Font("Consolas")
        let task =
            Sg.ofList [
                Sg.ofList [
                    Sg.wireBox' C4b.Red Box3d.Unit
                        |> Sg.shader {
                            do! DefaultSurfaces.stableTrafo
                        }
                    Sg.sphere' 4 C4b.Green 0.5
                        |> Sg.shader {
                            do! DefaultSurfaces.stableTrafo
                            do! DefaultSurfaces.stableHeadlight
                        }

                    Sg.text font C4b.White (AVal.constant "stable trafo")
                        |> Sg.scale 0.2
                        |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero) * Trafo3d.Translation(0.0, 0.0, 1.5))

                ]

                Sg.ofList [
                    Sg.wireBox' C4b.Red Box3d.Unit
                        |> Sg.shader {
                            do! DefaultSurfaces.trafo
                        }
                    Sg.sphere' 4 C4b.Green 0.5
                        |> Sg.shader {
                            do! DefaultSurfaces.trafo
                            do! DefaultSurfaces.simpleLighting
                        }
                    Sg.text font C4b.White (AVal.constant "non-stable trafo")
                        |> Sg.scale 0.2
                        |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero) * Trafo3d.Translation(0.0, 0.0, 1.5))
                ]
                |> Sg.translate 2.0 0.0 0.0
                
                Sg.text font C4b.Red (offset |> AVal.map (fun v -> sprintf "offset = %A" v))
                    |> Sg.scale 0.2
                    |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero) * Trafo3d.Translation(0.0, 0.0, 2.0))
                    |> Sg.translate 1.0 0.0 0.0


            ]
            |> Sg.trafo (offset |> AVal.map Trafo3d.Translation)
            |> Sg.viewTrafo cameraView
            |> Sg.projTrafo projection
            |> Sg.compile app.Runtime win.FramebufferSignature

        
        let changeoffset =
            async {
                do! Async.SwitchToThreadPool()
                while true do
                    printf "enter an offset: "
                    match Double.TryParse(Console.ReadLine()) with
                        | (true, v) -> transact (fun () -> offset.Value <- V3d(v, 0.0, 0.0))
                        | _ -> printfn "not a number you basterd!!!"
            }

        win.Keyboard.KeyDown(Keys.Add).Values.Add (fun _ ->
            transact (fun () -> offset.Value <- 2.0 * offset.Value)
        )   

        win.Keyboard.KeyDown(Keys.Subtract).Values.Add (fun _ ->
            transact (fun () -> offset.Value <- 0.5 * offset.Value)
        )   
        win.Keyboard.KeyDown(Keys.OemPlus).Values.Add (fun _ ->
            transact (fun () -> offset.Value <- 2.0 * offset.Value)
        )   

        win.Keyboard.KeyDown(Keys.OemMinus).Values.Add (fun _ ->
            transact (fun () -> offset.Value <- 0.5 * offset.Value)
        )   
        
        win.Keyboard.KeyDown(Keys.Escape).Values.Add (fun _ ->
            win.Close()
        )   

        Async.Start changeoffset
//        win.FormBorderStyle <- Windows.Forms.FormBorderStyle.None
//        win.WindowState <- Windows.Forms.FormWindowState.Maximized

        win.RenderTask <- task
        win.Run()