namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open Expecto

module Samplers =

    module private Shader =
        open FShade

        type Vertex = {
            [<TexCoord>] tc : V2d
        }

        type Fragment = {
            [<Color>] c : V4i
        }

        let private diffuseSampler =
            intSampler2d {
                texture uniform?DiffuseColorTexture
            }

        let diffuseTexture (v : Vertex) =
            fragment {
                return diffuseSampler.Sample(v.tc)
            }

    module Cases =

        let private sample2DSignedInteger (getPixImage : V2i -> PixImage<'T>) (format : TextureFormat) (samples : int) (runtime : IRuntime) =
            let size = V2i(256)

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, format
                ], samples = samples)

            let input = getPixImage size

            use inputTexture = runtime.CreateTexture2D(size, format)
            inputTexture.Upload input

            use task =
                Sg.fullScreenQuad
                |> Sg.diffuseTexture' inputTexture
                |> Sg.shader {
                    do! Shader.diffuseTexture
                }
                |> Sg.compile runtime signature

            let buffer = task |> RenderTask.renderToColor (AVal.constant size)
            buffer.Acquire()

            try
                let output = buffer.GetValue().Download().AsPixImage<'T>()

                Expect.equal output.Size size "Unexpected texture size"
                PixImage.compare V2i.Zero input output

            finally
                buffer.Release()

        let sample2Drgba8i (samples : int) (runtime : IRuntime) =
            runtime |> sample2DSignedInteger PixImage.random8i TextureFormat.Rgba8i 1

        let sample2Drgba16i (samples : int) (runtime : IRuntime) =
            runtime |> sample2DSignedInteger PixImage.random16i TextureFormat.Rgba16i 1

        let sample2Drgba32i (samples : int) (runtime : IRuntime) =
            runtime |> sample2DSignedInteger PixImage.random32i TextureFormat.Rgba32i 1

        // TODO: Use unsigned integer samplers once FShade supports them
        let sample2Drgba8ui (samples : int) (runtime : IRuntime) =
            runtime |> sample2DSignedInteger PixImage.random8ui TextureFormat.Rgba8ui 1

        let sample2Drgba16ui (samples : int) (runtime : IRuntime) =
            runtime |> sample2DSignedInteger PixImage.random16ui TextureFormat.Rgba16ui 1

        let sample2Drgba32ui (samples : int) (runtime : IRuntime) =
            runtime |> sample2DSignedInteger PixImage.random32ui TextureFormat.Rgba32ui 1

    let tests (backend : Backend) =
        [
            "2D rgba8i", Cases.sample2Drgba8i 1
            "2D rgba16i", Cases.sample2Drgba16i 1
            "2D rgba32i", Cases.sample2Drgba32i 1
            "2D rgba32i multisampled", Cases.sample2Drgba32i 4

            "2D rgba8ui", Cases.sample2Drgba8ui 1
            "2D rgba16ui", Cases.sample2Drgba16ui 1
            "2D rgba32ui", Cases.sample2Drgba32ui 1
        ]
        |> prepareCases backend "Samplers"