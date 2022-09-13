namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open Expecto

module Samplers =

    module private Literal =
        [<Literal>]
        let IntColor = "IntColor"

    module private Semantic =
        let IntColor = Sym.ofString Literal.IntColor

    module private Shader =
        open FShade

        type Vertex = {
            [<TexCoord>] tc : V2d
        }

        type Fragment = {
            //[<Color>] c : V4i
            [<Semantic(Literal.IntColor)>] c : V4i
        }

        let private diffuseSampler =
            intSampler2d {
                texture uniform?DiffuseColorTexture
            }

        let diffuseTexture (v : Vertex) =
            fragment {
                return { c = diffuseSampler.Sample(v.tc) }
            }

    module Cases =

        let sample2Drgba32ui (runtime : IRuntime) =
            let size = V2i(256)

            use signature =
                runtime.CreateFramebufferSignature([
                    Semantic.IntColor, TextureFormat.Rgba32ui
                ])

            let input = PixImage.random32ui size

            use task =
                Sg.fullScreenQuad
                |> Sg.diffuseTexture' (PixTexture2d(input, false))
                |> Sg.shader {
                    do! Shader.diffuseTexture
                }
                |> Sg.compile runtime signature

            let buffer = task |> RenderTask.renderSemantics (Set.ofList [Semantic.IntColor]) (AVal.constant size) |> Map.find Semantic.IntColor
            buffer.Acquire()

            try
                let output = buffer.GetValue().Download().AsPixImage<uint32>()

                Expect.equal output.Size size "Unexpected texture size"
                PixImage.compare V2i.Zero input output

            finally
                buffer.Release()

    let tests (backend : Backend) =
        [
            "2D rgba32ui", Cases.sample2Drgba32ui
        ]
        |> prepareCases backend "Samplers"