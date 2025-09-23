open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.ImGui
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open FShade

// This example illustrates how to use dynamic shaders.

[<EntryPoint>]
let main _argv =

    // first we need to initialize Aardvark's core components
    Aardvark.Init()

    use win =
        window {
            backend Backend.GL
            display Display.Mono
            debug true
            samples 8
            showHelp false
            initialCamera (CameraView.lookAt (V3d(2.0, 2.0, 1.5)) V3d.Zero V3d.OOI)
        }

    use gui = win.Control.InitializeImGui()

    let activeShader = AVal.init 0

    let effects =
        [|
            // red
            "Constant color (flat)", Effect.compose [
                toEffect DefaultSurfaces.trafo
                toEffect (DefaultSurfaces.constantColor C4f.Red)
            ]

            // red with lighting
            "Constant color", Effect.compose [
                toEffect DefaultSurfaces.trafo
                toEffect (DefaultSurfaces.constantColor C4f.Red)
                toEffect DefaultSurfaces.simpleLighting
            ]

            // vertex colors with lighting
            "Vertex color", Effect.compose [
                toEffect DefaultSurfaces.trafo
                toEffect DefaultSurfaces.simpleLighting
            ]

            // texture with lighting
            "Textured", Effect.compose [
                toEffect DefaultSurfaces.trafo
                toEffect DefaultSurfaces.diffuseTexture
                toEffect DefaultSurfaces.simpleLighting
            ]
        |]

    gui.Render <- fun () ->
        if ImGui.Begin("Settings", ImGuiWindowFlags.AlwaysAutoResize) then
            let active, _ = effects.[activeShader.Value]
            if ImGui.BeginCombo("##shader", active) then
                for i = 0 to effects.Length - 1 do
                    if ImGui.Selectable(fst effects.[i], (activeShader.Value = i)) then
                        activeShader.Value <- i
                ImGui.EndCombo()
        ImGui.End()

    let sg =
        let effects = effects |> Array.map snd

        Sg.box' C4b.Green Box3d.Unit
            |> Sg.diffuseTexture DefaultTextures.checkerboard
            |> Sg.effectPool effects activeShader

    // show the scene in a simple window
    win.Scene <- RenderCommand.Ordered [sg; gui] |> Sg.execute
    win.Run()

    0
