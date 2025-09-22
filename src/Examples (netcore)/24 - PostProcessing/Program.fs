open System

open Aardvark.Base

open Aardvark.Rendering
open Aardvark.Rendering.ImGui
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

// we now need to define some shaders performing the per-pixel blur on a given input texture.
// since the gaussian filter is separable we create two shaders performing the vertical and horizontal blur.
module Shaders =
    open FShade

    type Vertex = { [<TexCoord>] tc : V2f; [<Position>] p : V4f }

    let inputTex =
        sampler2d {
            texture uniform?DiffuseColorTexture
            filter Filter.MinMagMipPoint
        }

    // for a given filterSize and sigma calculate the weights CPU-side
    let filterSize = 15
    let sigma = 6.0f

    let halfFilterSize = filterSize / 2
    let weights =
        let res = 
            Array.init filterSize (fun i ->
                let x = abs (i - halfFilterSize)
                exp (-float32 (x*x) / (2.0f * sigma * sigma))
            )

        // normalize the weights
        let sum = Array.sum res
        res |> Array.map (fun v -> v / sum)


    let gaussX (v : Vertex) =
        fragment {
            let mutable color = V4f.Zero

            let off = V2f(1.0f / float32 uniform.ViewportSize.X, 0.0f)
            for x in -halfFilterSize..halfFilterSize do
                let w = weights.[x+halfFilterSize]
                color <- color + w * inputTex.Sample(v.tc + (float32 x) * off)
             

            return V4f(color.XYZ, 1.0f)
        }

    let gaussY (v : Vertex) =
        fragment {
            let mutable color = V4f.Zero
            let off = V2f(0.0f, 1.0f / float32 uniform.ViewportSize.Y)
            for y in -halfFilterSize..halfFilterSize do
                let w = weights.[y+halfFilterSize]
                color <- color + w * inputTex.Sample(v.tc + (float32 y) * off)

            return V4f(color.XYZ, 1.0f)
        }
            
    type PSVertex = { [<TexCoord; Interpolation(InterpolationMode.Sample)>] tc : V2f }

    let pointSpriteFragment (v : PSVertex) =
        fragment {
            let tc = v.tc // + 0.00000001 * v.sp

            let c = 2.0f * tc - V2f.II
            if c.Length > 1.0f then
                discard()

            return v
        }

type Variant =
    | Final = 0
    | BlurX = 1
    | BlurY = 2
    | Input = 3

module Variant =
    let toString = function
        | Variant.Final -> "Final"
        | Variant.BlurX -> "Blur X"
        | Variant.BlurY -> "Blur Y"
        | Variant.Input -> "Input"
        | _ -> ""

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
        }

    use gui = win.Control.InitializeImGui()

    let pointSize = AVal.init 50.0
    let variant = AVal.init Variant.Final
    let showInput = AVal.init true

    gui.Render <- fun () ->
        if ImGui.Begin("Settings", ImGuiWindowFlags.AlwaysAutoResize) then
            let mutable pointSizeValue = pointSize.Value
            if ImGui.InputDouble("Point size", &pointSizeValue, 5.0, "%.1f") then
                pointSize.Value <- max 0.0 pointSizeValue

            if ImGui.BeginCombo("##variant", Variant.toString variant.Value) then
                for v in Enum.GetValues<Variant>() do
                    if ImGui.Selectable(Variant.toString v, (variant.Value = v)) then
                        variant.Value <- v
                ImGui.EndCombo()

            if variant.Value <> Variant.Input then
                ImGui.Checkbox("Show input", showInput)
        ImGui.End()

    let pointSg = 
        let pointCount = 2048
        let rand = Random()
        let randomV3f() = V3f(rand.NextDouble(), rand.NextDouble(), rand.NextDouble())
        let randomColor() = C4b(rand.NextDouble(), rand.NextDouble(), rand.NextDouble(), 1.0)

        Sg.draw IndexedGeometryMode.PointList
            |> Sg.vertexAttribute DefaultSemantic.Positions (Array.init pointCount (fun _ -> randomV3f()) |> AVal.constant)
            |> Sg.vertexAttribute DefaultSemantic.Colors (Array.init pointCount (fun _ -> randomColor()) |> AVal.constant)
            |> Sg.viewTrafo (win.View |> AVal.map (Array.item 0))    // for stereo rendering we would get two views
            |> Sg.projTrafo (win.Proj |> AVal.map (Array.item 0))    // but we take the first one here.
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.pointSprite |> toEffect; Shaders.pointSpriteFragment |> toEffect; DefaultSurfaces.vertexColor |> toEffect]
            |> Sg.uniform "PointSize" pointSize
   
    // for rendering the filtered image we need a fullscreen quad
    let fullscreenQuad =
        Sg.draw IndexedGeometryMode.TriangleStrip
            |> Sg.vertexAttribute DefaultSemantic.Positions (AVal.constant [|V3f(-1.0,-1.0,0.0); V3f(1.0,-1.0,0.0); V3f(-1.0,1.0,0.0);V3f(1.0,1.0,0.0) |])
            |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (AVal.constant [|V2f.OO; V2f.IO; V2f.OI; V2f.II|])
            |> Sg.depthTest' DepthTest.None

    // so in a first pass we need to render our pointScene to a color texture which
    // is quite simple using the RenderTask utilities provided in Base.Rendering.
    // from the rendering we get an aval<ITexture> which will be outOfDate whenever
    // something changes in pointScene and updated whenever subsequent passes need it.
    use singleSampledSignature =
        win.Runtime.CreateFramebufferSignature [
            DefaultSemantic.Colors, TextureFormat.Rgba8; 
            DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
        ]

    use mainTask =
        pointSg |> Sg.compile win.Runtime singleSampledSignature

    let input =
        mainTask |> RenderTask.renderToColor win.Sizes

    // by taking the texture created above and the fullscreen quad we can now apply
    // the first gaussian filter to it and in turn get a new aval<ITexture>     
    use blurXTask =
        fullscreenQuad 
            |> Sg.texture DefaultSemantic.DiffuseColorTexture input
            |> Sg.effect [Shaders.gaussX |> toEffect]
            |> Sg.compile win.Runtime singleSampledSignature

    let blurredOnlyX = blurXTask |> RenderTask.renderToColor win.Sizes

    // by taking the texture created above and the fullscreen quad we can now apply
    // the first gaussian filter to it and in turn get a new aval<ITexture>     
    use blurYTask =
        fullscreenQuad 
            |> Sg.texture DefaultSemantic.DiffuseColorTexture input
            |> Sg.effect [Shaders.gaussY |> toEffect]
            |> Sg.compile win.Runtime singleSampledSignature

    let blurredOnlyY = blurYTask |> RenderTask.renderToColor win.Sizes

    // apply the vertical blur to the output of the horizontal blur task to get the final blurred result
    use blurTask =
        fullscreenQuad
            |> Sg.texture DefaultSemantic.DiffuseColorTexture blurredOnlyX
            |> Sg.effect [Shaders.gaussY |> toEffect]
            |> Sg.compile win.Runtime singleSampledSignature

    let blurred = blurTask |> RenderTask.renderToColor win.Sizes

    let final =
        let showOverlay =
            (showInput, variant) ||> AVal.map2 (fun s v -> s && v <> Variant.Input)

        let overlayRelativeSize = 0.3
        let overlayPass = RenderPass.main |> RenderPass.after "overlay" RenderPassOrder.Arbitrary
        let overlayOriginal =
            fullscreenQuad
                |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.diffuseTexture |> toEffect]
                |> Sg.texture DefaultSemantic.DiffuseColorTexture input
                |> Sg.modifySamplerState' DefaultSemantic.DiffuseColorTexture (SamplerState.withAdressMode WrapMode.Clamp)
                |> Sg.trafo' (Trafo3d.Scale(overlayRelativeSize) * Trafo3d.Translation(1.0 - overlayRelativeSize, -1.0 + overlayRelativeSize, 0.0))
                |> Sg.pass overlayPass
                |> Sg.blendMode' BlendMode.Blend
                |> Sg.onOff showOverlay

        let result =
            variant |> AdaptiveResource.bind (function
                | Variant.Final -> blurred
                | Variant.BlurX -> blurredOnlyX
                | Variant.BlurY -> blurredOnlyY
                | _ -> input
            )

        let mainResult =
            fullscreenQuad
                |> Sg.texture DefaultSemantic.DiffuseColorTexture result
                |> Sg.effect [DefaultSurfaces.diffuseTexture |> toEffect]

        Sg.ofList [mainResult; overlayOriginal] 
            |> Sg.viewTrafo' Trafo3d.Identity
            |> Sg.projTrafo' Trafo3d.Identity

    let sg =
        RenderCommand.Ordered [final; gui]
        |> Sg.execute

    win.Scene <- sg
    win.Run()
    0