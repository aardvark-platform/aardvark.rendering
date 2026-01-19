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
        member x.Color: V4f = x?Color

    module private Semantic =
        let Output0 = Sym.ofString "Output0"
        let Output1 = Sym.ofString "Output1"
        let Output2 = Sym.ofString "Output2"
        let Output3 = Sym.ofString "Output3"
        let Output4 = Sym.ofString "Output4"

    module private Shader =

        let output0White (v : Effects.Vertex) =
            fragment {
                return {| Output0 = V4f.One |}
            }

        let output1White (v : Effects.Vertex) =
            fragment {
                return {| Output1 = V4f.One |}
            }

        let output4White (v : Effects.Vertex) =
            fragment {
                return {| Output4 = V4f.One |}
            }

        let output02White (v : Effects.Vertex) =
            fragment {
                return {| Output0 = V4f.One; Output2 = V4f.One |}
            }

        let output13White (v : Effects.Vertex) =
            fragment {
                return {| Output1 = V4f.One; Output3 = V4f.One |}
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

        let renderTo (dimension: TextureDimension) (dynamic: bool) (layered: bool) (count: int) (levels: int) (samples: int) (runtime: IRuntime) =
            let totalCount = if dimension = TextureDimension.TextureCube then count * 6 else count
            let layers = if layered then totalCount else 1
            let slice = if layered then -1 else totalCount - 1

            use signature =
                runtime.CreateFramebufferSignature (
                    [
                        DefaultSemantic.Colors,       TextureFormat.Rgba8
                        DefaultSemantic.DepthStencil, TextureFormat.DepthComponent32f
                    ],
                    samples = samples, layers = layers, perLayerUniforms = [ "Color" ]
                )

            let createBuffer (format: TextureFormat) =
                let size =
                    let size3D = V3i(123, 178, 77)

                    match dimension with
                    | TextureDimension.Texture1D -> size3D.XII
                    | TextureDimension.Texture2D -> size3D.XYI
                    | TextureDimension.Texture3D -> size3D
                    | TextureDimension.TextureCube -> size3D.XXI
                    | _ -> failwith ""

                if count > 1 then
                    runtime.CreateTextureArray(size, dimension, format, levels = levels, count = count, samples = samples)
                else
                    runtime.CreateTexture(size, dimension, format, levels = levels, samples = samples)

            use colorBuffer = createBuffer TextureFormat.Rgba8
            use depthBuffer = createBuffer TextureFormat.DepthComponent32f

            use framebuffer =
                runtime.CreateFramebuffer(signature, [
                    DefaultSemantic.Colors,       colorBuffer.GetOutputView(level = levels - 1, slice = slice)
                    DefaultSemantic.DepthStencil, depthBuffer.GetOutputView(level = levels - 1, slice = slice)
                ])

            let colors = Array.init totalCount (ignore >> Rnd.c4b)
            let activeShader = AVal.init 0

            let applyColor =
                if layered then
                    Sg.uniform' "Color" colors
                else
                    Sg.uniform' "Color" colors.[slice]

            let applySurface =
                let rgbaEffect = toEffect DefaultSurfaces.sgColor
                let bgraEffect = toEffect (fun (v: Effects.Vertex) -> fragment { return uniform.Color.ZYXW })

                if dynamic then
                    Sg.effectPool [| rgbaEffect; bgraEffect |] activeShader
                else
                    Sg.surface rgbaEffect

            let colorToArray (color: C4b) =
                if activeShader.Value = 0 then
                    [| color.R; color.G; color.B; color.A |]
                else
                    [| color.B; color.G; color.R; color.A |]

            use task =
                Sg.draw IndexedGeometryMode.TriangleStrip
                |> Sg.vertexAttribute' DefaultSemantic.Positions [| V3f(-1, -1, 0); V3f(1, -1, 0); V3f(-1, 1, 0); V3f(1, 1, 0) |]
                |> applyColor
                |> applySurface
                |> Sg.compile runtime signature

            let runAndCheck() =
                framebuffer.Clear(C4b.Black, 1.0f)
                task.Run framebuffer

                let s, e = if slice = -1 then 0, totalCount - 1 else slice, slice

                for i = s to e do
                    let pi = colorBuffer.Download(level = levels - 1, slice = i).AsPixImage<uint8>()
                    pi |> PixImage.isColor (colorToArray colors.[i])

                    if samples = 1 || not (runtime :? Vulkan.Runtime) then
                        let depth = depthBuffer.DownloadDepth(level = levels - 1, slice = i)
                        Expect.validDepthResult depth Accuracy.medium pi.Size 0.5 0.5

            runAndCheck()

            if dynamic then
                transact (fun _ -> activeShader.Value <- 1)
                runAndCheck()

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

            "Render to 1D",                                 Cases.renderTo TextureDimension.Texture1D false false 1 1 1
            "Render to 1D (dynamic)",                       Cases.renderTo TextureDimension.Texture1D true  false 1 1 1
            "Render to 1D (mipmap)",                        Cases.renderTo TextureDimension.Texture1D false false 1 3 1
            "Render to 1D (mipmap, dynamic)",               Cases.renderTo TextureDimension.Texture1D true  false 1 3 1
            "Render to 1D array slice",                     Cases.renderTo TextureDimension.Texture1D false false 6 1 1
            "Render to 1D array slice (dynamic)",           Cases.renderTo TextureDimension.Texture1D true  false 6 1 1
            "Render to 1D array slice (mipmap)",            Cases.renderTo TextureDimension.Texture1D false false 6 3 1
            "Render to 1D array slice (mipmap, dynamic)",   Cases.renderTo TextureDimension.Texture1D true  false 6 3 1
            "Render to 1D array layered",                   Cases.renderTo TextureDimension.Texture1D false true  6 1 1
            "Render to 1D array layered (dynamic)",         Cases.renderTo TextureDimension.Texture1D true  true  6 1 1
            "Render to 1D array layered (mipmap)",          Cases.renderTo TextureDimension.Texture1D false true  6 3 1
            "Render to 1D array layered (mipmap, dynamic)", Cases.renderTo TextureDimension.Texture1D true  true  6 3 1

            "Render to 2D",                                 Cases.renderTo TextureDimension.Texture2D false false 1 1 1
            "Render to 2D (dynamic)",                       Cases.renderTo TextureDimension.Texture2D true  false 1 1 1
            "Render to 2D (mipmap)",                        Cases.renderTo TextureDimension.Texture2D false false 1 3 1
            "Render to 2D (mipmap, dynamic)",               Cases.renderTo TextureDimension.Texture2D true  false 1 3 1
            "Render to 2D array slice",                     Cases.renderTo TextureDimension.Texture2D false false 6 1 1
            "Render to 2D array slice (dynamic)",           Cases.renderTo TextureDimension.Texture2D true  false 6 1 1
            "Render to 2D array slice (mipmap)",            Cases.renderTo TextureDimension.Texture2D false false 6 3 1
            "Render to 2D array slice (mipmap, dynamic)",   Cases.renderTo TextureDimension.Texture2D true  false 6 3 1
            "Render to 2D array layered",                   Cases.renderTo TextureDimension.Texture2D false true  6 1 1
            "Render to 2D array layered (dynamic)",         Cases.renderTo TextureDimension.Texture2D true  true  6 1 1
            "Render to 2D array layered (mipmap)",          Cases.renderTo TextureDimension.Texture2D false true  6 3 1
            "Render to 2D array layered (mipmap, dynamic)", Cases.renderTo TextureDimension.Texture2D true  true  6 3 1

            "Render to 2D multisampled",                         Cases.renderTo TextureDimension.Texture2D false false 1 1 2
            "Render to 2D multisampled (dynamic)",               Cases.renderTo TextureDimension.Texture2D true  false 1 1 2
            "Render to 2D multisampled array slice",             Cases.renderTo TextureDimension.Texture2D false false 6 1 2
            "Render to 2D multisampled array slice (dynamic)",   Cases.renderTo TextureDimension.Texture2D true  false 6 1 2
            "Render to 2D multisampled array layered",           Cases.renderTo TextureDimension.Texture2D false true  6 1 2
            "Render to 2D multisampled array layered (dynamic)", Cases.renderTo TextureDimension.Texture2D true  true  6 1 2

            "Render to cube slice",                           Cases.renderTo TextureDimension.TextureCube false false 1 1 1
            "Render to cube slice (dynamic)",                 Cases.renderTo TextureDimension.TextureCube true  false 1 1 1
            "Render to cube slice (mipmap)",                  Cases.renderTo TextureDimension.TextureCube false false 1 3 1
            "Render to cube slice (mipmap, dynamic)",         Cases.renderTo TextureDimension.TextureCube true  false 1 3 1
            "Render to cube layered",                         Cases.renderTo TextureDimension.TextureCube false true  1 1 1
            "Render to cube layered (dynamic)",               Cases.renderTo TextureDimension.TextureCube true  true  1 1 1
            "Render to cube layered (mipmap)",                Cases.renderTo TextureDimension.TextureCube false true  1 3 1
            "Render to cube layered (mipmap, dynamic)",       Cases.renderTo TextureDimension.TextureCube true  true  1 3 1
            "Render to cube array slice",                     Cases.renderTo TextureDimension.TextureCube false false 2 1 1
            "Render to cube array slice (dynamic)",           Cases.renderTo TextureDimension.TextureCube true  false 2 1 1
            "Render to cube array slice (mipmap)",            Cases.renderTo TextureDimension.TextureCube false false 2 3 1
            "Render to cube array slice (mipmap, dynamic)",   Cases.renderTo TextureDimension.TextureCube true  false 2 3 1
            "Render to cube array layered",                   Cases.renderTo TextureDimension.TextureCube false true  2 1 1
            "Render to cube array layered (dynamic)",         Cases.renderTo TextureDimension.TextureCube true  true  2 1 1
            "Render to cube array layered (mipmap)",          Cases.renderTo TextureDimension.TextureCube false true  2 3 1
            "Render to cube array layered (mipmap, dynamic)", Cases.renderTo TextureDimension.TextureCube true  true  2 3 1

            "Render prepared object with incompatible signature", Cases.renderPreparedWithIncompatibleSignature
        ]
        |> prepareCases backend "Framebuffer signatures"