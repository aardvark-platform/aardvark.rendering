// This example illustrates how to use quadbuffered stereo with aardvark.

open System
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.SceneGraph.Assimp
open Aardvark.Rendering.Text
open Aardvark.Glfw

module Shader =

    open FShade
    open Aardvark.Rendering.Effects

    [<GLSLIntrinsic("gl_Layer")>]
    let layer() : int = onlyInShaderCode "gl_Layer"

    let leftRightTest (v : Vertex) =
        fragment {
            return { v with c = if layer() = 0 then V4d.IOOI elif layer() = 1 then V4d.OIOI else V4d.OOII }
        }


[<EntryPoint>]
let main argv =

    Aardvark.Init()

    use app = new OpenGlApplication(true)
    let win = app.CreateGameWindow({ WindowConfig.Default with samples = 8; width = 1760; height = 1080; stereo = true })
    win.Title <- "Quadbuffer Stereo. Use mouse to Rotate."
    win.Cursor <- Cursor.None
    win.RenderAsFastAsPossible <- true

    let eyeDistance = cval 0.05
    let convergence = cval 1.00

    win.Keyboard.DownWithRepeats.Values.Add(fun k -> 
        transact (fun _ -> 
            match k, win.Keyboard.IsDown(Keys.LeftAlt).GetValue() with
            | Keys.OemPlus, false -> 
                eyeDistance.Value <- eyeDistance.Value + 0.001
            | Keys.OemMinus, false ->
                eyeDistance.Value <- eyeDistance.Value - 0.001
            | Keys.OemPlus, true -> 
                convergence.Value <- convergence.Value + 0.001
            | Keys.OemMinus, true ->
                convergence.Value <- convergence.Value - 0.001
            | _ -> ()

            Log.line $"eye distance: {eyeDistance.Value}m, convergence: {convergence.Value}" 
        )
    )

    // Given eye, target and sky vector we compute our initial camera pose
    let initialView = CameraView.LookAt(V3d(1.5,1.5,1.5), V3d.Zero, V3d.OOI)
    let cameraView = 
        AVal.integrate initialView win.Time [
            DefaultCameraController.controlOrbitAround win.Mouse (AVal.constant V3d.Zero)
            DefaultCameraController.controlZoom win.Mouse
            DefaultCameraController.controllScroll win.Mouse win.Time
        ]

    let projTrafos = 
        let stereoSettings = AVal.map2 (fun e c -> (e,c)) eyeDistance convergence
        // the frustum needs to depend on the window size (in oder to get proper aspect ratio)
        (win.Sizes, stereoSettings, cameraView) 
            // construct a standard perspective frustum (60 degrees horizontal field of view,
            // near plane 0.1, far plane 50.0 and aspect ratio x/y.
            |||> AVal.map3 (fun windowSize (eyeDistance, convergence) cameraView -> 

                // this demo shows the basic working.
                // for further tuning see 
                // - Schneider Digital has great material online: https://www.3d-pluraview.com/de/technische-daten/support-bereich
                // also including UI/Cursor and camera considerations.
                // - https://developer.download.nvidia.com/presentations/2009/SIGGRAPH/3DVision_Develop_Design_Play_in_3D_Stereo.pdf 
                let separation = eyeDistance * 0.5
                let camFov = 70.0
                
                // use this to focus always on orbit (screen = orbit center)
                let convergence = cameraView.Location.Length

                let center = Frustum.perspective camFov 0.001 100.0 (float windowSize.X / float windowSize.Y) |> Frustum.projTrafo
                let p = center.Forward
                let pl = 
                    M44d(
                        p.M00, p.M01, p.M02 - separation, p.M03-separation*convergence,
                        p.M10, p.M11, p.M12, p.M13,
                        p.M20, p.M21, p.M22, p.M23,
                        p.M30, p.M31, p.M32, p.M33
                    )
                let pr = 
                    M44d(
                        p.M00, p.M01, p.M02 + separation, p.M03+separation*convergence,
                        p.M10, p.M11, p.M12, p.M13,
                        p.M20, p.M21, p.M22, p.M23,
                        p.M30, p.M31, p.M32, p.M33
                    )

                [|
                    Trafo3d(pr, pr.Inverse)
                    Trafo3d(pl, pl.Inverse)
                |]
            )



    let model = 
        // load the scene and wrap it in an adapter
        Loader.Assimp.load (Path.combine [__SOURCE_DIRECTORY__; "..";"..";"..";"data";"aardvark";"aardvark.obj"])
            |> Sg.adapter

            // flip the z coordinates (since the model is upside down)
            |> Sg.transform (Trafo3d.Scale(1.0, 1.0, -1.0)) 
            |> Sg.scale 1.0

            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.White
                do! DefaultSurfaces.diffuseTexture
                do! DefaultSurfaces.normalMap
                do! DefaultSurfaces.simpleLighting
                //do! Shader.leftRightTest
            }

    let coordinateCross = 
        Sg.coordinateCross' 1.0 
        |> Sg.shader {
            do! DefaultSurfaces.trafo
        }

    let helpTextSg =

        let helpText = 
            (eyeDistance, convergence) ||> AVal.map2 (fun eyeDistance convergence -> 
                String.concat "\r\n" [
                    $"Eye Distance: {eyeDistance}, Screen distance: {convergence}"
                    $""
                    $"Key Bindings:"
                    $"  Plus or Minus        increase/decrease eye separation"
                    //$"  Left Alt+Plus/Minus  increase/decrease screen distance"
                    $"  Mouse Wheel          zoom"
                    $"  Drag Mouse           rotate around object"
                    $""
                ]
            )

        let trafo = 
            win.Sizes |> AVal.map (fun s -> 
                let border = V2d(20.0, 10.0) / V2d s
                let pixels = 30.0 / float s.Y
                Trafo3d.Scale(pixels) *
                Trafo3d.Scale(float s.Y / float s.X, 1.0, 1.0) *
                Trafo3d.Translation(-1.0 + border.X, 1.0 - border.Y - pixels, -1.0)
            )

        // Use NoBoundary to resolve issue with render passes, such the Cube not being visible when behind the text in the WriteBuffers example
        let textCfg = TextConfig.create DefaultFonts.Hack.Regular C4b.White TextAlignment.Left false RenderStyle.NoBoundary
        Sg.textWithConfig textCfg helpText
            |> Sg.trafo trafo
            |> Sg.uniform "ViewTrafo" (Trafo3d.Identity |> Array.create 2 |> AVal.constant)
            |> Sg.uniform "ProjTrafo" (Trafo3d.Identity |> Array.create 2 |> AVal.constant)
            |> Sg.viewTrafo (AVal.constant Trafo3d.Identity)
            |> Sg.projTrafo (AVal.constant Trafo3d.Identity)
   

    let sg =
        Sg.ofList [model; coordinateCross; helpTextSg]
        // extract our viewTrafo from the dynamic cameraView and attach it to the scene graphs viewTrafo 
        |> Sg.uniform "ViewTrafo" (cameraView|> AVal.map (CameraView.viewTrafo >> Array.create 2))
        // compute a projection trafo, given the frustum contained in frustum
        |> Sg.uniform "ProjTrafo" projTrafos
        |> Sg.viewTrafo (cameraView |> AVal.map CameraView.viewTrafo)
        |> Sg.projTrafo (projTrafos |> AVal.map (fun a -> a.[0]))


    let renderTask = 
        // compile the scene graph into a render task
        app.Runtime.CompileRender(win.FramebufferSignature, sg)

    // assign the render task to our window...
    win.RenderTask <- renderTask
    win.Run()
    0
