namespace Aardvark.Rendering.Tests.Rendering

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
        let Output4 = Sym.ofString "Output4"

    module private Shader =
        open FShade

        let output1White (v : Effects.Vertex) =
            fragment {
                return {| Output1 = V4d.One |}
            }

        let output4White (v : Effects.Vertex) =
            fragment {
                return {| Output4 = V4d.One |}
            }

        let output02White (v : Effects.Vertex) =
            fragment {
                return {| Output0 = V4d.One; Output2 = V4d.One |}
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

        // Note: Same restriction as above
        let renderCombined (runtime : IRuntime) =
            use signature =
                runtime.CreateFramebufferSignature(Map.ofList [
                    0, { Name = Semantic.Output0; Format = TextureFormat.Rgba8 }
                    1, { Name = Semantic.Output1; Format = TextureFormat.Rgba8 }
                    2, { Name = Semantic.Output2; Format = TextureFormat.Rgba8 }
                    3, { Name = Semantic.Output3; Format = TextureFormat.Rgba8 }
                    4, { Name = Semantic.Output4; Format = TextureFormat.Rgba8 }
                ], Some TextureFormat.Depth24Stencil8)

            use t0sig =
                runtime.CreateFramebufferSignature(Map.ofList [
                    1, { Name = Semantic.Output1; Format = TextureFormat.Rgba8 }
                    3, { Name = Semantic.Output3; Format = TextureFormat.Rgba8 }
                ], None)

            use t0 =
                let masks =
                    Map.ofList [
                        Semantic.Output1, ColorMask.Red
                        Semantic.Output3, ColorMask.Green
                    ]

                Sg.fullScreenQuad
                |> Sg.shader {
                    do! Shader.output13White
                }
                |> Sg.depthTest' DepthTest.None
                |> Sg.colorMasks' masks
                |> Sg.compile runtime t0sig

            use t1sig =
                runtime.CreateFramebufferSignature(Map.ofList [
                    4, { Name = Semantic.Output4; Format = TextureFormat.Rgba8 }
                ], Some TextureFormat.Depth24Stencil8)

            use t1 =
                Sg.fullScreenQuad
                |> Sg.shader {
                    do! Shader.output4White
                }
                |> Sg.depthTest' DepthTest.None
                |> Sg.colorMask' ColorMask.Blue
                |> Sg.compile runtime t1sig

            use t2sig =
                runtime.CreateFramebufferSignature(Map.ofList [
                    0, { Name = Semantic.Output0; Format = TextureFormat.Rgba8 }
                    2, { Name = Semantic.Output2; Format = TextureFormat.Rgba8 }
                ], Some TextureFormat.Depth24Stencil8)

            use t2 =
                let masks =
                    Map.ofList [
                        Semantic.Output0, ColorMask.Alpha
                        Semantic.Output2, ColorMask.rgb
                    ]

                Sg.fullScreenQuad
                |> Sg.shader {
                    do! Shader.output02White
                }
                |> Sg.depthTest' DepthTest.None
                |> Sg.colorMasks' masks
                |> Sg.compile runtime t2sig

            use task =
                RenderTask.ofList [t0; t1; t2]

            // Combining tasks with varying signatures leads to task with no signature
            Expect.equal task.FramebufferSignature None "Unexpected render task signature"

            let r0, r1, r2, r3, r4 =
                let fbo = runtime.CreateFramebuffer(signature, ~~V2i(256))
                let output = task |> RenderTask.renderTo fbo
                output.GetOutputTexture(Semantic.Output0),
                output.GetOutputTexture(Semantic.Output1),
                output.GetOutputTexture(Semantic.Output2),
                output.GetOutputTexture(Semantic.Output3),
                output.GetOutputTexture(Semantic.Output4)

            r0.Acquire(); r1.Acquire(); r2.Acquire(); r3.Acquire(); r4.Acquire();

            try
                let p0 = r0.GetValue().Download().AsPixImage<byte>()
                p0 |> PixImage.isColor [| 0uy; 0uy; 0uy; 255uy |]

                let p1 = r1.GetValue().Download().AsPixImage<byte>()
                p1 |> PixImage.isColor [| 255uy; 0uy; 0uy; 0uy |]

                let p2 = r2.GetValue().Download().AsPixImage<byte>()
                p2 |> PixImage.isColor [| 255uy; 255uy; 255uy; 0uy |]

                let p3 = r3.GetValue().Download().AsPixImage<byte>()
                p3 |> PixImage.isColor [| 0uy; 255uy; 0uy; 0uy |]

                let p4 = r4.GetValue().Download().AsPixImage<byte>()
                p4 |> PixImage.isColor [| 0uy; 0uy; 255uy; 0uy |]

            finally
                r0.Release(); r1.Release(); r2.Release(); r3.Release(); r4.Release();

        // Render to a multisampled framebuffer with a single sample task signature
        // Note: We want this to be possible with the GL backend
        let renderToMultisampled (runtime : IRuntime) =
            use signature =
                runtime.CreateFramebufferSignature(Map.ofList [
                    1, { Name = Semantic.Output1; Format = TextureFormat.Rgba8 }
                ], None, samples = 4)

            use taskSignature =
                runtime.CreateFramebufferSignature(Map.ofList [
                    1, { Name = Semantic.Output1; Format = TextureFormat.Rgba8 }
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
                "Render subset",       Cases.renderToSubset
                "Render combined",     Cases.renderCombined
                "Render multisampled", Cases.renderToMultisampled
        ]
        |> prepareCases backend "Framebuffer signatures"