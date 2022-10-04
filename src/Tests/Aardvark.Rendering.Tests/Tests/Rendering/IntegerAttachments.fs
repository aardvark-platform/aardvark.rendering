namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open Expecto

module IntegerAttachments =

    module private Shader =
        open FShade

        type Vertex = {
            [<PrimitiveId>] primId : int
        }

        let primitiveIdSigned (v : Vertex) =
            fragment {
                return V4i(v.primId)
            }

        let primitiveIdUnsigned (v : Vertex) =
            fragment {
                return V4ui(v.primId)
            }

    module Cases =

        let inline private renderPrimitiveId (_ : ^T) (format : TextureFormat) (samples : int) (runtime : IRuntime) =
            let size = V2i(256)

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, format
                ], samples = samples)

            use task =
                Sg.fullScreenQuad
                |> Sg.shader {
                    do! DefaultSurfaces.diffuseTexture
                    if format.IsSigned then
                        do! Shader.primitiveIdSigned
                    else
                        do! Shader.primitiveIdUnsigned
                }
                |> Sg.compile runtime signature

            let buffer = task |> RenderTask.renderToColor (AVal.constant size)
            buffer.Acquire()

            try
                let output = buffer.GetValue().Download().AsPixImage< ^T>()
                let values = output.Data |> unbox< ^T[]> |> Array.distinct |> Array.sort
                Expect.equal values.Length 2 "Unexpected number of distinct values"
                Expect.equal values.[0] LanguagePrimitives.GenericZero< ^T> "Unexpected value"
                Expect.equal values.[1] LanguagePrimitives.GenericOne< ^T> "Unexpected value"

            finally
                buffer.Release()

        let renderToR8i (samples : int) (runtime : IRuntime) =
            runtime |> renderPrimitiveId Unchecked.defaultof<int8> TextureFormat.R8i samples

        let renderToR16i (samples : int) (runtime : IRuntime) =
            runtime |> renderPrimitiveId Unchecked.defaultof<int16> TextureFormat.R16i samples

        let renderToR32i (samples : int) (runtime : IRuntime) =
            runtime |> renderPrimitiveId Unchecked.defaultof<int32> TextureFormat.R32i samples

        let renderToR8ui (samples : int) (runtime : IRuntime) =
            runtime |> renderPrimitiveId Unchecked.defaultof<uint8> TextureFormat.R8ui samples

        let renderToR16ui (samples : int) (runtime : IRuntime) =
            runtime |> renderPrimitiveId Unchecked.defaultof<uint16> TextureFormat.R16ui samples

        let renderToR32ui (samples : int) (runtime : IRuntime) =
            runtime |> renderPrimitiveId Unchecked.defaultof<uint32> TextureFormat.R32ui samples

    let tests (backend : Backend) =
        [
            "2D r8i", Cases.renderToR8i 1
            "2D r16i", Cases.renderToR16i 1
            "2D r32i", Cases.renderToR32i 1
            "2D r8ui", Cases.renderToR8ui 1
            "2D r16ui", Cases.renderToR16ui 1
            "2D r32ui", Cases.renderToR32ui 1
            "2D r32ui multisampled", Cases.renderToR32ui 4
        ]
        |> prepareCases backend "Integer attachments"
