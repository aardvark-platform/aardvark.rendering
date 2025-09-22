(*

This example demonstrates various techniques for order-independent transparency (OIT).

Currently the following techniques are implemented:
 * Weighted Blended Order-Independent Transparency [McGuire2013]

 The project is organized in a way that makes it possible to easily add more techniques in the future.

*)

namespace Transparency

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.ImGui
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim

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
                C4f(C3f.DodgerBlue, 0.25f)
                C4f(C3f.Salmon, 0.25f)
                C4f(C3f.SpringGreen, 0.25f)
                C4f(C3f.Plum, 0.25f)
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
    let main _argv =

        Aardvark.Init()

        // uncomment/comment to switch between the backends
        use app = new VulkanApplication(debug = true)
        //use app = new OpenGlApplication(debug = true)
        let runtime = app.Runtime :> IRuntime

        // create a game window (better for measuring fps)
        use win = app.CreateGameWindow(samples = 8)
        win.RenderAsFastAsPossible <- true

        use gui = win.InitializeImGui()

        let initialView = CameraView.LookAt(V3d(10.0,10.0,10.0), V3d.Zero, V3d.OOI)
        let frustum =
            win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 150.0 (float s.X / float s.Y))

        let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

        let currentTechnique = AVal.init 0
        let backgroundColor = AVal.init C3b.PaleTurquoise

        let framebuffer =
            {
                size = win.Sizes
                signature = win.FramebufferSignature
                clearColor = backgroundColor |> AVal.map c4b
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

        gui.Render <- fun () ->
            if ImGui.Begin("Settings", ImGuiWindowFlags.AlwaysAutoResize) then
                ImGui.ColorEdit3("Background color", backgroundColor, ImGuiColorEditFlags.NoInputs)

                let technique = techniques.[currentTechnique.Value]
                if ImGui.BeginCombo("##technique", technique.Name) then
                    for i = 0 to techniques.Length - 1 do
                        if ImGui.Selectable(techniques.[i].Name, (currentTechnique.Value = i)) then
                            currentTechnique.Value <- i
                    ImGui.EndCombo()
            ImGui.End()

        // precompile
        for t in techniques do
            Log.startTimed "[%s] compile scene" t.Name
            t.Task.PrepareForRender()
            Log.stop()

        use task =
            RenderTask.custom (fun (t, rt, desc) ->
                let current = currentTechnique.GetValue(t)
                techniques.[current].Task.Run(t, rt, desc)
            )

        use guiTask =
            gui
            |> Sg.compile win.Runtime win.FramebufferSignature

        win.RenderTask <- RenderTask.ofList [ task; guiTask ]
        win.Run()

        for t in techniques do
            t.Dispose()

        0