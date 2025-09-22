open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.ImGui
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

module Shader = 
    open FShade
    open Aardvark.Rendering.Effects

    let private colorSampler =
        sampler2d {
            texture uniform?Colors
            filter Filter.MinMagLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let private posSampler =
        sampler2d {
            texture uniform?WPos
            filter Filter.MinMagPoint
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    type Fragment = { [<Color>] color: V4f;  [<Semantic("WPos")>] wpos : V4f }

    let pass0 (v : Vertex) =
        fragment {
            return { color = v.c; wpos = v.wp }
        }

    let composite (v : Vertex) =
        fragment {
            let mode : int = uniform?Mode
            if mode = 0 then
                return colorSampler.Sample(v.tc)
            elif mode = 1 then
                return posSampler.SampleLevel(v.tc,0.0f)
            else return V4f.IOOI
        }

[<EntryPoint>]
let main _argv =
    
    Aardvark.Init()
    
    // window { ... } is similar to show { ... } but instead
    // of directly showing the window we get the window-instance
    // and may show it later.
    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug true
            samples 1
            showHelp false
        }

    use gui = win.Control.InitializeImGui()

    // define a dynamic transformation depending on the window's time
    // This time is a special value that can be used for animations which
    // will be evaluated when rendering the scene
    let dynamicTrafo =
        let startTime = System.DateTime.Now
        win.Time |> AVal.map (fun t ->
            let t = (t - startTime).TotalSeconds
            Trafo3d.RotationZ (0.5 * t)
        )

    let box = Box3d(-V3d.III, V3d.III)
    let color = cval C3b.Red
    let size = cval 512
    let mode = AVal.init 0
    let getModeText = function 0 -> "Color" | _ -> "Positions"

    gui.Render <- fun () ->
        if ImGui.Begin("Settings", ImGuiWindowFlags.AlwaysAutoResize) then
            ImGui.SliderInt("Texture size", size, 64, 2048, $"{size.Value} x {size.Value}")

            if ImGui.BeginCombo("Mode", getModeText mode.Value) then
                for i = 0 to 1 do if ImGui.Selectable(getModeText i, (mode.Value = i)) then mode.Value <- i
                ImGui.EndCombo()

            if mode.Value = 0 then
                ImGui.ColorEdit3("Color", color)
        ImGui.End()

    use signature = 
        win.Runtime.CreateFramebufferSignature(
            [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                Sym.ofString "WPos"  , TextureFormat.Rgba32f
                DefaultSemantic.DepthStencil,  TextureFormat.Depth24Stencil8
            ])

    let pass0 =
        let size = size |> AVal.map V2i
        let color = color |> AVal.map C4b

        // create a red box with a simple shader
        Sg.box color (AVal.constant box)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! Shader.pass0
            }

            // apply the dynamic transformation to the box
            |> Sg.trafo dynamicTrafo
            |> Sg.viewTrafo (win.View |> AVal.map (Array.item 0))
            |> Sg.projTrafo (win.Proj |> AVal.map (Array.item 0))
            |> Sg.compile win.Runtime signature
            |> RenderTask.renderSemantics (
                    Set.ofList [
                        DefaultSemantic.DepthStencil
                        DefaultSemantic.Colors
                        Sym.ofString "WPos"]
               ) size

    let finalComposite = 
        Sg.fullScreenQuad
        |> Sg.shader {
             do! Shader.composite
          }
        |> Sg.uniform "Mode" mode
        |> Sg.texture (Sym.ofString "Colors") (Map.find DefaultSemantic.Colors pass0)
        |> Sg.texture (Sym.ofString "WPos") (Map.find (Sym.ofString "WPos") pass0)

    let scene =
        RenderCommand.Ordered [
            RenderCommand.Render finalComposite
            RenderCommand.Render gui
        ]
        |> Sg.execute

    win.Scene <- scene
    win.Run()

    0