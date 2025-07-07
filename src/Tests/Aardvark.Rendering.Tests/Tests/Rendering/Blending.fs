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
        let Output2 = Sym.ofString "Output2"

    module private BlendMode =
        let ColorMulAlphaAdd =
            { BlendMode.Blend with
                SourceColorFactor = BlendFactor.Zero
                SourceAlphaFactor = BlendFactor.One
                DestinationColorFactor = BlendFactor.SourceColor
                DestinationAlphaFactor = BlendFactor.One }

        let DisabledWithConstant =
            { Enabled                = false
              SourceColorFactor      = BlendFactor.ConstantColor
              SourceAlphaFactor      = BlendFactor.ConstantAlpha
              DestinationColorFactor = BlendFactor.Zero
              DestinationAlphaFactor = BlendFactor.Zero
              ColorOperation         = BlendOperation.Add
              AlphaOperation         = BlendOperation.Add}

    module private Shader =
        open FShade

        let inline output0 (c : ^Color) (v : Effects.Vertex) =
            let c = v4f c
            fragment {
                return {| Output0 = c |}
            }

        let inline output1 (c : ^Color) (v : Effects.Vertex) =
            let c = v4f c
            fragment {
                return {| Output1 = c |}
            }

        let inline output2 (c : ^Color) (v : Effects.Vertex) =
            let c = v4f c
            fragment {
                return {| Output2 = c |}
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
            let blendedColor0 = C4f(0.1, 0.1, 0.1, 0.1)
            let blendedColor1 = C4f.White
            let blendedColor2 = C4f(0.5, 0.5, 0.5, 0.5)
            let blendConstant = C4f(0.3, 0.2, 0.4, 0.5)
            let expectedColor0 = clearColor + blendedColor0
            let expectedColor1 = C4f.White
            let expectedColor2 = clearColor * blendedColor2

            use signature =
                runtime.CreateFramebufferSignature([
                    Semantic.Output0, TextureFormat.Rgba32f
                    Semantic.Output1, TextureFormat.Rgba32f
                    Semantic.Output2, TextureFormat.Rgba32f
                ])

            let modes =
                Map.ofList [
                    Semantic.Output0, BlendMode.Add
                    Semantic.Output1, BlendMode.DisabledWithConstant
                    Semantic.Output2, BlendMode.Multiply
                ]

            use task =
                Sg.fullScreenQuad
                |> Sg.shader {
                    do! Shader.output0 blendedColor0
                    do! Shader.output1 blendedColor1
                    do! Shader.output2 blendedColor2
                }
                |> Sg.blendModes' modes
                |> Sg.blendConstant' blendConstant
                |> Sg.compile runtime signature

            let r0, r1, r2 =
                let fbo = runtime.CreateFramebuffer(signature, ~~V2i(256))
                let clear = clear { color clearColor }
                let output = task |> RenderTask.renderToWithClear fbo clear
                let o0 = output.GetOutputTexture(Semantic.Output0)
                let o1 = output.GetOutputTexture(Semantic.Output1)
                let o2 = output.GetOutputTexture(Semantic.Output2)
                o0, o1, o2

            r0.Acquire(); r1.Acquire(); r2.Acquire()

            try
                let p0 = r0.GetValue().Download().AsPixImage<float32>()
                p0 |> PixImage.isColor (expectedColor0.ToArray())

                let p1 = r1.GetValue().Download().AsPixImage<float32>()
                p1 |> PixImage.isColor (expectedColor1.ToArray())

                let p2 = r2.GetValue().Download().AsPixImage<float32>()
                p2 |> PixImage.isColor (expectedColor2.ToArray())

            finally
                r0.Release(); r1.Release(); r2.Release()

    let tests (backend : Backend) =
        [
            "Global (add)",                       Cases.globalBlend BlendMode.Add (+) (+)
            "Global (color multiply, alpha add)", Cases.globalBlend BlendMode.ColorMulAlphaAdd (*) (+)
            "Per attachment",                     Cases.perAttachmentBlend
        ]
        |> prepareCases backend "Blending"