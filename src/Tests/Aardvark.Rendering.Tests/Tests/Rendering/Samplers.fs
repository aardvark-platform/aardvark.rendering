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

        let private diffuseIntSampler =
            intSampler2d {
                texture uniform?DiffuseColorTexture
            }

        let private diffuseUIntSampler =
            uintSampler2d {
                texture uniform?DiffuseColorTexture
            }

        let diffuseIntTexture (v : Vertex) =
            fragment {
                return diffuseIntSampler.Sample v.tc
            }

        let diffuseUIntTexture (v : Vertex) =
            fragment {
                return diffuseUIntSampler.Sample v.tc
            }

    module Cases =

        let private sample2DInteger (getPixImage : V2i -> PixImage<'T>) (format : TextureFormat) (samples : int) (runtime : IRuntime) =
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
                    if format.IsSigned then
                        do! Shader.diffuseIntTexture
                    else
                        do! Shader.diffuseUIntTexture
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
            runtime |> sample2DInteger PixImage.random8i TextureFormat.Rgba8i samples

        let sample2Drgba16i (samples : int) (runtime : IRuntime) =
            runtime |> sample2DInteger PixImage.random16i TextureFormat.Rgba16i samples

        let sample2Drgba32i (samples : int) (runtime : IRuntime) =
            runtime |> sample2DInteger PixImage.random32i TextureFormat.Rgba32i samples

        let sample2Drgba8ui (samples : int) (runtime : IRuntime) =
            runtime |> sample2DInteger PixImage.random8ui TextureFormat.Rgba8ui samples

        let sample2Drgba16ui (samples : int) (runtime : IRuntime) =
            runtime |> sample2DInteger PixImage.random16ui TextureFormat.Rgba16ui samples

        let sample2Drgba32ui (samples : int) (runtime : IRuntime) =
            runtime |> sample2DInteger PixImage.random32ui TextureFormat.Rgba32ui samples

        let sample2DGrayscale (runtime : IRuntime) =
            let size = V2i(256)

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ])

            let input = PixImage.random8ui' Col.Format.Gray size

            let reference =
                let pi = PixImage<uint8>(Col.Format.RGBA, size)
                let red = input.GetChannel(0L)

                for i = 0 to 2 do
                    pi.GetChannel(int64 i).Set(red) |> ignore
                pi.GetChannel(3L).Set(255uy) |> ignore

                pi

            use inputTexture = runtime.CreateTexture2D(size, TextureFormat.R8)
            inputTexture.Upload input

            use task =
                Sg.fullScreenQuad
                |> Sg.diffuseTexture' inputTexture
                |> Sg.shader {
                    do! DefaultSurfaces.diffuseTexture
                }
                |> Sg.compile runtime signature

            let buffer = task |> RenderTask.renderToColor (AVal.constant size)
            buffer.Acquire()

            try
                let output = buffer.GetValue().Download().AsPixImage<uint8>()

                Expect.equal output.Size size "Unexpected texture size"
                Expect.equal output.ChannelCount 4 "Unexpected channel count"
                PixImage.compare V2i.Zero reference output

            finally
                buffer.Release()

    let tests (backend : Backend) =
        [
            "2D rgba8i", Cases.sample2Drgba8i 1
            "2D rgba16i", Cases.sample2Drgba16i 1
            "2D rgba32i", Cases.sample2Drgba32i 1
            "2D rgba32i multisampled", Cases.sample2Drgba32i 4

            "2D rgba8ui", Cases.sample2Drgba8ui 1
            "2D rgba16ui", Cases.sample2Drgba16ui 1
            "2D rgba32ui", Cases.sample2Drgba32ui 1
            "2D rgba32ui multisampled", Cases.sample2Drgba32ui 2

            "2D grayscale", Cases.sample2DGrayscale
        ]
        |> prepareCases backend "Samplers"