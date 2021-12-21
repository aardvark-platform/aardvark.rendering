namespace Aardvark.Rendering.Tests.Rendering

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Expecto

module FramebufferSignature =

    module private Semantic =
        let Output0 = Sym.ofString "Output0"
        let Output1 = Sym.ofString "Output1"
        let Output2 = Sym.ofString "Output2"
        let Output3 = Sym.ofString "Output3"

    module private Shader =
        open FShade

        let output1White (v : Effects.Vertex) =
            fragment {
                return {| Output1 = V4d.One |}
            }

        let output13White (v : Effects.Vertex) =
            fragment {
                return {| Output1 = V4d.One; Output3 = V4d.One |}
            }
       
    module Cases =

        let framebufferWithHoles (runtime : IRuntime) =
            use signature =
                runtime.CreateFramebufferSignature(Map.ofList [
                    1, { Name = Semantic.Output1; Format = TextureFormat.Rgba8 }
                    3, { Name = Semantic.Output3; Format = TextureFormat.Rgba8 }
                ], None)

            use task =
                let masks =
                    Map.ofList [
                        Semantic.Output1, ColorMask.Red
                        Semantic.Output3, ColorMask.Green
                    ]

                Sg.fullScreenQuad
                |> Sg.shader {
                    do! Shader.output13White
                }
                |> Sg.colorMasks' masks
                |> Sg.compile runtime signature

            let r1, r3 =
                let fbo = runtime.CreateFramebuffer(signature, ~~V2i(256))
                let output = task |> RenderTask.renderTo fbo
                output.GetOutputTexture(Semantic.Output1), output.GetOutputTexture(Semantic.Output3)

            r1.Acquire(); r3.Acquire()

            try
                let p1 = r1.GetValue().Download().AsPixImage<byte>()
                p1 |> PixImage.isColor [| 255uy; 0uy; 0uy; 0uy |]

                let p3 = r3.GetValue().Download().AsPixImage<byte>()
                p3 |> PixImage.isColor [| 0uy; 255uy; 0uy; 0uy |]

            finally
                r1.Release(); r3.Release()

        // Note: Can't do that with vulkan, render pass compability is very strict.
        // https://www.khronos.org/registry/vulkan/specs/1.2-extensions/html/vkspec.html#renderpass-compatibility
        let renderToSubset (runtime : IRuntime) =
            use signature =
                runtime.CreateFramebufferSignature(Map.ofList [
                    0, { Name = Semantic.Output1; Format = TextureFormat.Rgba8 }
                    1, { Name = Semantic.Output2; Format = TextureFormat.Rgba8 }
                    2, { Name = Semantic.Output0; Format = TextureFormat.Rgba8 }
                ], None)

            use taskSignature =
                runtime.CreateFramebufferSignature(Map.ofList [
                    0, { Name = Semantic.Output1; Format = TextureFormat.Rgba8 }
                ], None)

            use task =
                Sg.fullScreenQuad
                |> Sg.shader {
                    do! Shader.output1White
                }
                |> Sg.colorMask' ColorMask.Red
                |> Sg.compile runtime taskSignature
       
            let result =
                let fbo = runtime.CreateFramebuffer(signature, ~~V2i(256))
                let output = task |> RenderTask.renderTo fbo
                output.GetOutputTexture(Semantic.Output1)

            result.Acquire()

            try
                let pi = result.GetValue().Download().AsPixImage<byte>()
                pi |> PixImage.isColor [| 255uy; 0uy; 0uy; 0uy |]

            finally
                result.Release()

    let tests (backend : Backend) =        
        [
            "Framebuffer with holes",  Cases.framebufferWithHoles

            if backend <> Backend.Vulkan then
                "Render subset",           Cases.renderToSubset
        ]
        |> prepareCases backend "Framebuffer signatures"