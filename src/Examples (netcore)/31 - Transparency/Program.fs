(*

This example demonstrates various techniques for order-independent transparency (OIT).

Currently the following techniques are implemented:
 * Weighted Blended Order-Independent Transparency [McGuire2013]

 The project is organized in a way that makes it possible to easily add more techniques in the future.

*)

namespace Transparency

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Rendering.Text
open FShade

module Program =

    type IRenderTask with
        member x.PrepareForRender() =
            match x.FramebufferSignature with
                | Some signature ->
                    let runtime = signature.Runtime
                    let tempFbo = runtime.CreateFramebuffer(signature, AVal.constant(V2i(16,16)))
                    tempFbo.Acquire()
                    x.Run(RenderToken.Empty, tempFbo.GetValue())
                    tempFbo.Release()
                | None ->
                    ()

    let floorSg =
        Sg.quad
        |> Sg.diffuseTexture DefaultTextures.checkerboard
        |> Sg.scale 10.0

    let windowSg (color : C4f) =
        Sg.quad
        |> Sg.transform (Trafo3d.RotationXInDegrees 90.0)
        |> Sg.uniform "Color" ~~color

    let windowsSg =
        let colors = [
                C4f(C3f.DodgerBlue, 0.5f)
                C4f(C3f.Salmon, 0.5f)
                C4f(C3f.SpringGreen, 0.5f)
                C4f(C3f.Plum, 0.5f)
            ]

        colors
        |> List.mapi (fun i c ->
            let offset = V2d(0.25, 0.5) * float i
            let rotation = 20.0 * float i

            windowSg c
            |> Sg.transform (Trafo3d.RotationYInDegrees rotation)
            |> Sg.translate offset.X offset.Y 2.0
        )
        |> Sg.ofList

    [<EntryPoint>]
    let main argv =

        Aardvark.Init()

        // uncomment/comment to switch between the backends
        use app = new VulkanApplication(debug = true)
        //use app = new OpenGlApplication()
        let runtime = app.Runtime :> IRuntime
        let samples = 8

        // create a game window (better for measuring fps)
        use win = app.CreateGameWindow(samples = samples)

        // disable incremental rendering
        win.RenderAsFastAsPossible <- true

        let initialView = CameraView.LookAt(V3d(10.0,10.0,10.0), V3d.Zero, V3d.OOI)
        let frustum =
            win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 150.0 (float s.X / float s.Y))

        let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

        let framebuffer =
            {
                size = win.Sizes
                samples = samples
                signature = win.FramebufferSignature
            }

        let scene =
            {
                opaque = floorSg
                transparent = windowsSg
                viewTrafo = cameraView |> AVal.map CameraView.viewTrafo
                projTrafo = frustum |> AVal.map Frustum.projTrafo
            }

        let techniques =
            [|
                new Naive.Technique(runtime, framebuffer, scene) :> ITechnique
                new WeightedBlended.Technique(runtime, framebuffer, scene) :> ITechnique
            |]

        // precompile
        for t in techniques do
            Log.startTimed "[%s] compile scene" t.Name
            t.Task.PrepareForRender()
            Log.stop()

        // use this mutable to switch between techniques.
        let technique = AVal.init 0
        let fps = AVal.init 0.0

        win.Keyboard.KeyDown(Keys.V).Values.Add(fun _ ->
            transact (fun () ->
                technique.Value <- (technique.Value + 1) % techniques.Length
                fps.Value <- 0.0
            )
            Log.line "using: %s" techniques.[technique.Value].Name
        )

        win.Keyboard.KeyDown(Keys.OemPlus).Values.Add(fun _ ->
            transact (fun () ->
                technique.Value <- (technique.Value + 1) % techniques.Length
                fps.Value <- 0.0
            )
            Log.line "using: %s" techniques.[technique.Value].Name
        )

        win.Keyboard.KeyDown(Keys.OemMinus).Values.Add(fun _ ->
            transact (fun () ->
                technique.Value <- (technique.Value + techniques.Length - 1) % techniques.Length
                fps.Value <- 0.0
            )
            Log.line "using: %s" techniques.[technique.Value].Name
        )

        use task =
            RenderTask.custom (fun (t, rt, desc, q) ->
                techniques.[technique.Value].Task.Run(t, rt, desc, q)
            )

        let puller =
            async {
                while true do
                    if not (Fun.IsTiny win.AverageFrameTime.TotalSeconds) then
                        transact (fun () -> fps.Value <- 1.0 / win.AverageFrameTime.TotalSeconds)
                    do! Async.Sleep 200
            }

        Async.Start puller
        let overlayTask =
            let str =
                AVal.custom (fun t ->
                    let variant = technique.GetValue t
                    let fps = fps.GetValue t
                    let fps = if fps <= 0.0 then "" else sprintf "%.0ffps" fps
                    let variant = techniques.[variant].Name
                    String.concat " " [variant; fps]
                )

            let trafo =
                win.Sizes |> AVal.map (fun size ->
                    let px = 2.0 / V2d size
                    Trafo3d.Scale(0.05) *
                    Trafo3d.Scale(1.0, float size.X / float size.Y, 1.0) *
                    Trafo3d.Translation(-1.0 + 20.0 * px.X, -1.0 + 25.0 * px.Y, 0.0)
                )

            Sg.text (Font("Consolas")) C4b.White str
                |> Sg.trafo trafo
                |> Sg.compile runtime win.FramebufferSignature

        win.RenderTask <- RenderTask.ofList [ task; overlayTask ]
        win.Run()

        for t in techniques do
            t.Dispose()

        0