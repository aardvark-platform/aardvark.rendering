namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Expecto

module Blending =

    module private Semantic =
        let Output0 = Sym.ofString "Output0"
        let Output1 = Sym.ofString "Output1"

    module private BlendMode =
        let ColorMulAlphaAdd =
            { BlendMode.Blend with
                SourceColorFactor = BlendFactor.Zero
                SourceAlphaFactor = BlendFactor.One
                DestinationColorFactor = BlendFactor.SourceColor
                DestinationAlphaFactor = BlendFactor.One }

    module private Shader =
        open FShade

        let inline output01 (c1 : ^Color) (c2 : ^Color) (v : Effects.Vertex) =
            let c1 = v4d c1
            let c2 = v4d c2
            fragment {
                return {| Output0 = c1; Output1 = c2 |}
            }

    module Cases =

        let globalBlend (mode : BlendMode) (colorOp : float32 -> float32 -> float32) (alphaOp : float32 -> float32 -> float32) (runtime : IRuntime) =
            let clearColor = C4f(0.1, 0.2, 0.3, 0.4)
            let blendedColor = C4f(0.1, 0.1, 0.1, 0.1)
            let expectedColor =
                C4f(
                    colorOp clearColor.R blendedColor.R,
                    colorOp clearColor.G blendedColor.G,
                    colorOp clearColor.B blendedColor.B,
                    alphaOp clearColor.A blendedColor.A
                )

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba32f
                ])

            use task =
                Sg.fullScreenQuad
                |> Sg.shader {
                    do! DefaultSurfaces.constantColor blendedColor
                }
                |> Sg.blendMode' mode
                |> Sg.compile runtime signature

            let output =
                let clear = clear { color clearColor }
                task |> RenderTask.renderToColorWithClear (~~V2i(256)) clear

            output.Acquire()

            try
                let result = output.GetValue().Download().AsPixImage<float32>()
                result |> PixImage.isColor32f Accuracy.medium (expectedColor.ToArray())
            finally
                output.Release()

        let perAttachmentBlend (runtime : IRuntime) =
            let clearColor = C4f(0.1, 0.2, 0.3, 0.4)
            let blendedColor1 = C4f(0.1, 0.1, 0.1, 0.1)
            let blendedColor2 = C4f(0.5, 0.5, 0.5, 0.5)
            let expectedColor1 = clearColor + blendedColor1
            let expectedColor2 = clearColor * blendedColor2

            use signature =
                runtime.CreateFramebufferSignature([
                    Semantic.Output0, TextureFormat.Rgba32f
                    Semantic.Output1, TextureFormat.Rgba32f
                ])

            let modes =
                Map.ofList [
                    Semantic.Output0, BlendMode.Add
                    Semantic.Output1, BlendMode.Multiply
                ]

            use task =
                Sg.fullScreenQuad
                |> Sg.shader {
                    do! Shader.output01 blendedColor1 blendedColor2
                }
                |> Sg.blendModes' modes
                |> Sg.compile runtime signature

            let r1, r2 =
                let fbo = runtime.CreateFramebuffer(signature, ~~V2i(256))
                let clear = clear { color clearColor }
                let output = task |> RenderTask.renderToWithClear fbo clear
                output.GetOutputTexture(Semantic.Output0), output.GetOutputTexture(Semantic.Output1)

            r1.Acquire(); r2.Acquire()

            try
                let p1 = r1.GetValue().Download().AsPixImage<float32>()
                p1 |> PixImage.isColor (expectedColor1.ToArray())

                let p2 = r2.GetValue().Download().AsPixImage<float32>()
                p2 |> PixImage.isColor (expectedColor2.ToArray())

            finally
                r1.Release(); r2.Release()

    let tests (backend : Backend) =
        [
            "Global (add)",                       Cases.globalBlend BlendMode.Add (+) (+)
            "Global (color multiply, alpha add)", Cases.globalBlend BlendMode.ColorMulAlphaAdd (*) (+)
            "Per attachment",                     Cases.perAttachmentBlend
        ]
        |> prepareCases backend "Blending"