namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators

module ColorMasks =

    module private Semantic =
        let Output0 = Sym.ofString "Output0"
        let Output1 = Sym.ofString "Output1"

    module private Shader =
        open FShade

        let output01White (v : Effects.Vertex) =
            fragment {
                return {| Output0 = V4d.One; Output1 = V4d.One |}
            }

    module Cases =

        let globalMask (runtime : IRuntime) =
            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ])

            use task =
                Sg.fullScreenQuad
                |> Sg.shader {
                    do! DefaultSurfaces.constantColor C4f.White
                }
                |> Sg.colorMask' (ColorMask.Green ||| ColorMask.Alpha)
                |> Sg.compile runtime signature

            let size = ~~V2i(256)
            let output = task |> RenderTask.renderToColor size
            output.Acquire()

            try
                let result = output.GetValue().Download().AsPixImage<uint8>()
                result |> PixImage.isColor [| 0uy; 255uy; 0uy; 255uy |]
            finally
                output.Release()

        let perAttachmentMask (runtime : IRuntime) =
            use signature =
                runtime.CreateFramebufferSignature([
                    Semantic.Output0, TextureFormat.Rgba8
                    Semantic.Output1, TextureFormat.Rgba8
                ])

            let masks =
                Map.ofList [
                    Semantic.Output0, ColorMask.Green ||| ColorMask.Alpha
                    Semantic.Output1, ColorMask.Red ||| ColorMask.Blue ||| ColorMask.Alpha
                ]

            use task =
                Sg.fullScreenQuad
                |> Sg.shader {
                    do! Shader.output01White
                }
                |> Sg.colorMasks' masks
                |> Sg.compile runtime signature

            let r1, r2 =
                let fbo = runtime.CreateFramebuffer(signature, ~~V2i(256))
                let clear = ClearValues.ofColor V4f.Zero
                let output = task |> RenderTask.renderToWithClear fbo clear
                output.GetOutputTexture(Semantic.Output0), output.GetOutputTexture(Semantic.Output1)

            r1.Acquire(); r2.Acquire()

            try
                let p1 = r1.GetValue().Download().AsPixImage<byte>()
                p1 |> PixImage.isColor [| 0uy; 255uy; 0uy; 255uy |]

                let p2 = r2.GetValue().Download().AsPixImage<byte>()
                p2 |> PixImage.isColor [| 255uy; 0uy; 255uy; 255uy |]

            finally
                r1.Release(); r2.Release()

    let tests (backend : Backend) =
        [
            "Global",         Cases.globalMask
            "Per attachment", Cases.perAttachmentMask
        ]
        |> prepareCases backend "Color masks"