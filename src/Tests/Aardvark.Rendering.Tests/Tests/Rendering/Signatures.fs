namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open Aardvark.Application
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open FShade
open Expecto

module FramebufferSignature =

    type UniformScope with
        member x.Color: V4d = x?Color

    module private Semantic =
        let Output0 = Sym.ofString "Output0"
        let Output1 = Sym.ofString "Output1"
        let Output2 = Sym.ofString "Output2"
        let Output3 = Sym.ofString "Output3"
        let Output4 = Sym.ofString "Output4"

    module private Shader =

        let output0White (v : Effects.Vertex) =
            fragment {
                return {| Output0 = V4d.One |}
            }

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

        let framebufferWithUnsupportedSampleCounts (runtime : IRuntime) =
            let size = V2i(256)
            let input = PixImage.random8ui size

            use signature =
                runtime.CreateFramebufferSignature([
                        DefaultSemantic.Colors, TextureFormat.Rgba8
                        DefaultSemantic.DepthStencil, TextureFormat.DepthComponent32
                    ], samples = 64)

            Expect.isGreaterThan signature.Samples 1 "not multisampled"
            Expect.isLessThan signature.Samples 64 "weird sample count"

            let samplerState =
                SamplerState.Default |> SamplerState.withFilter TextureFilter.MinMagPoint

            use task =
                Sg.fullScreenQuad
                |> Sg.diffuseTexture' (PixTexture2d(input, false))
                |> Sg.samplerState' DefaultSemantic.DiffuseColorTexture samplerState
                |> Sg.shader {
                    do! DefaultSurfaces.diffuseTexture
                }
                |> Sg.compile runtime signature

            let buffer = task |> RenderTask.renderToColor (AVal.constant size)
            buffer.Acquire()

            try
                let output = buffer.GetValue().Download().AsPixImage<byte>()
                PixImage.compare V2i.Zero input output

            finally
                buffer.Release()

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
                let clear = ClearValues.ofColor V4f.Zero
                let output = task |> RenderTask.renderToWithClear fbo clear
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
                let clear = ClearValues.ofColor V4f.Zero
                let output = task |> RenderTask.renderToWithClear fbo clear
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

            use tclear =
                runtime.CompileClear(signature, ClearValues.ofColor V4f.Zero)

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
                RenderTask.ofList [tclear; t0; t1; t2]

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
                let clear = ClearValues.ofColor V4f.Zero
                let output = task |> RenderTask.renderToWithClear fbo clear
                output.GetOutputTexture(Semantic.Output1)

            result.Acquire()

            try
                let pi = result.GetValue().Download().AsPixImage<byte>()
                pi |> PixImage.isColor [| 255uy; 0uy; 0uy; 0uy |]

            finally
                result.Release()

        let renderToCubeArrayLayered (dynamic: bool) (runtime : IRuntime) =
            use signature =
                runtime.CreateFramebufferSignature(
                    [ DefaultSemantic.Colors, TextureFormat.Rgba8],
                    layers = 12, perLayerUniforms = [ "Color" ]
                )

            let colorBuffer =
                runtime.CreateTextureCubeArray(512, TextureFormat.Rgba8, count = 2)

            use framebuffer =
                runtime.CreateFramebuffer(signature, [
                    DefaultSemantic.Colors, colorBuffer.GetOutputView()
                ])

            let colors =
                [|
                    C4f.Aqua
                    C4f.Azure
                    C4f.Beige
                    C4f.BurlyWood
                    C4f.CadetBlue
                    C4f.Crimson
                    C4f.DeepPink
                    C4f.Gold
                    C4f.Indigo
                    C4f.Linen
                    C4f.Moccasin
                    C4f.Tomato
                |]

            let activeShader = AVal.init 0

            let applySurface =
                let rgbaEffect = toEffect DefaultSurfaces.sgColor
                let bgraEffect = toEffect (fun (v: Effects.Vertex) -> fragment { return uniform.Color.ZYXW })

                if dynamic then
                    Sg.effectPool [| rgbaEffect; bgraEffect |] activeShader
                else
                    Sg.surface rgbaEffect

            use task =
                Sg.fullScreenQuad
                |> Sg.uniform' "Color" colors
                |> applySurface
                |> Sg.compile runtime signature

            try
                let check (colorArr: C4b -> byte[]) =
                    for i = 0 to colors.Length - 1 do
                        let pi = colorBuffer.Download(slice = i).AsPixImage<uint8>()
                        let c = C4b colors.[i]
                        pi |> PixImage.isColor (colorArr c)

                task.Run framebuffer
                check (fun c -> [| c.R; c.G; c.B; c.A |])

                if dynamic then
                    transact (fun _ -> activeShader.Value <- 1)
                    task.Run framebuffer
                    check (fun c -> [| c.B; c.G; c.R; c.A |])

            finally
                runtime.DeleteTexture colorBuffer

        let renderPreparedWithIncompatibleSignature (runtime: IRuntime) =
            use signaturePrepared =
                runtime.CreateFramebufferSignature [
                    Semantic.Output0, TextureFormat.Rgba8
                ]

            use preparedObject =
                let sg =
                    Sg.fullScreenQuad
                    |> Sg.shader {
                        do! Shader.output0White
                    }

                let ro = Seq.head <| sg.RenderObjects(Ag.Scope.Root).Content.GetValue()
                runtime.PrepareRenderObject(signaturePrepared, ro)

            use signature =
                runtime.CreateFramebufferSignature [
                    Semantic.Output0, TextureFormat.Rgba8
                    Semantic.Output1, TextureFormat.Rgba8
                ]

            use output0 = runtime.CreateTexture2D(V2i(256), TextureFormat.Rgba8)
            use output1 = runtime.CreateTexture2D(V2i(256), TextureFormat.Rgba8)

            use framebuffer =
                runtime.CreateFramebuffer(signature, [
                    Semantic.Output0, output0.GetOutputView()
                    Semantic.Output1, output1.GetOutputView()
                ])

            use task =
                Sg.renderObjectSet (ASet.single preparedObject)
                |> Sg.compile runtime signature

            try
                task.Run(framebuffer)
            with e ->
                Expect.stringContains e.Message "framebuffer signature" "Unexpected exception"

    let tests (backend : Backend) =
        [
            "Framebuffer with unsupported sample count", Cases.framebufferWithUnsupportedSampleCounts
            "Framebuffer with holes",                    Cases.framebufferWithHoles

            if backend <> Backend.Vulkan then
                "Render subset",       Cases.renderToSubset
                "Render combined",     Cases.renderCombined
                "Render multisampled", Cases.renderToMultisampled

            "Render to cube array layered",              Cases.renderToCubeArrayLayered false
            "Render to cube array layered (dynamic)",    Cases.renderToCubeArrayLayered true

            "Render prepared object with incompatible signature", Cases.renderPreparedWithIncompatibleSignature
        ]
        |> prepareCases backend "Framebuffer signatures"