open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Rendering.Text

// This example illustrates and tests new pipeline states.

module Semantic =
    let Color0 = Sym.ofString "Color0"
    let Color1 = Sym.ofString "Color1"
    let Color2 = Sym.ofString "Color2"
    let Color3 = Sym.ofString "Color3"
    let All = Set.ofList [Color0; Color1; Color2; Color3; DefaultSemantic.Depth]

    let Color0Texture = Sym.ofString "Color0Texture"
    let Color1Texture = Sym.ofString "Color1Texture"
    let Color2Texture = Sym.ofString "Color2Texture"
    let Color3Texture = Sym.ofString "Color3Texture"

module Shaders =
    open FShade

    type Fragment = {
        [<Color>] color : V4d
        [<FragCoord>] coord : V4d
        [<SampleId>] sample : int
    }

    let quadrupleOutput (f : Fragment) =
        fragment {
            return {| Color0 = f.color
                      Color1 = f.color
                      Color2 = f.color
                      Color3 = f.color |}
        }

    let private color0Sampler =
        sampler2d {
            texture uniform?Color0Texture
            filter Filter.MinMagPoint
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let private color0SamplerMS =
        sampler2dMS {
            texture uniform?Color0Texture
            filter Filter.MinMagPoint
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let blit (samples : int) (f : Fragment) =
        fragment {
            if samples > 1 then
                return color0SamplerMS.Read(V2i f.coord.XY / 2, f.sample)
            else
                return color0Sampler.Read(V2i f.coord.XY / 2, 0)
        }

[<EntryPoint>]
let main argv =

    Aardvark.Init()

    // uncomment/comment to switch between the backends
    //use app = new VulkanApplication(debug = true)
    use app = new OpenGlApplication()
    let runtime = app.Runtime :> IRuntime
    let samples = 8

    // create a game window (better for measuring fps)
    use win = app.CreateGameWindow(samples = samples)

    let triangleSg =
        [| Triangle3d(V3d(-0.75, -0.75, 0.0), V3d(0.0, 0.75, 0.0), V3d(0.75, -0.75, 0.0)) |]
        |> Sg.triangles' C4b.White
        |> Sg.depthTest' DepthTest.None
        |> Sg.shader {
            do! DefaultSurfaces.vertexColor
            do! Shaders.quadrupleOutput
        }

    let signature =
        runtime.CreateFramebufferSignature(samples, [
            Semantic.Color0, RenderbufferFormat.Rgba8
            Semantic.Color1, RenderbufferFormat.Rgba8
            Semantic.Color2, RenderbufferFormat.Rgba8
            Semantic.Color3, RenderbufferFormat.Rgba8
            DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
        ])

    let output =
        triangleSg
        |> Sg.compile runtime signature
        |> RenderTask.renderSemantics Semantic.All (win.Sizes |> AVal.map (fun s -> s / 2))

    let finalTask =
        let sg =
            Sg.fullScreenQuad
            |> Sg.texture Semantic.Color0Texture output.[Semantic.Color0]
            |> Sg.texture Semantic.Color1Texture output.[Semantic.Color1]
            |> Sg.texture Semantic.Color2Texture output.[Semantic.Color2]
            |> Sg.texture Semantic.Color3Texture output.[Semantic.Color3]
            |> Sg.depthTest' DepthTest.None
            |> Sg.shader {
                do! Shaders.blit samples
            }

        RenderTask.ofList [
            runtime.CompileClear(win.FramebufferSignature, C4f.DarkSlateGray)
            runtime.CompileRender(win.FramebufferSignature, sg)
        ]

    // show the window
    win.RenderTask <- finalTask
    win.Run()

    runtime.DeleteFramebufferSignature(signature)

    0
