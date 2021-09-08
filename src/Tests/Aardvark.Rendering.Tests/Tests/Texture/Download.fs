namespace Aardvark.Rendering.Tests.Texture

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Aardvark.Application
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Expecto

module TextureDownload =

    module Cases =

        let simple (runtime : IRuntime) =
            let size = V2i(333, 666)
            let data = PixImage.checkerboard C4b.BurlyWood
            let fmt = TextureFormat.ofPixFormat data.PixFormat TextureParams.empty

            let t = runtime.CreateTexture2D(size, fmt, 1, 1)
            t.Clear(C4b.Zero)
            runtime.Upload(t, data)
            let result = runtime.Download(t).ToPixImage<byte>()
            runtime.DeleteTexture(t)

            Expect.equal result.Size size "Unexpected texture size"
            PixImage.compare V2i.Zero data result

        let multisampled (runtime : IRuntime) =
            let data = PixImage.checkerboard C4b.BurlyWood
            let size = data.Size
            let samples = 8

            let signature = runtime.CreateFramebufferSignature(samples, [DefaultSemantic.Colors, RenderbufferFormat.Rgba32f])
            let colorTexture = runtime.CreateTexture2D(size, TextureFormat.Rgba32f, 1, samples)
            let framebuffer = runtime.CreateFramebuffer(signature, [DefaultSemantic.Colors, colorTexture.GetOutputView()])

            let sampler =
                Some { SamplerState.Default with Filter = TextureFilter.MinMagPoint }

            use task =
                Sg.fullScreenQuad
                |> Sg.diffuseTexture' (data |> PixImage.toTexture false)
                |> Sg.samplerState' DefaultSemantic.DiffuseColorTexture sampler
                |> Sg.shader {
                    do! DefaultSurfaces.diffuseTexture
                }
                |> Sg.compile runtime signature

            task.Run(RenderToken.Empty, framebuffer)
            let result = runtime.Download(colorTexture).ToPixImage<byte>()

            runtime.DeleteFramebuffer(framebuffer)
            runtime.DeleteTexture(colorTexture)
            runtime.DeleteFramebufferSignature(signature)

            Expect.equal result.Size size "Unexpected texture size"
            PixImage.compare V2i.Zero data result

        let mipmappedArray (runtime : IRuntime) =
            let count = 4
            let levels = 3
            let size = V2i(128)

            let data =
                Array.init count (fun index ->
                    let data = PixImage.checkerboard testColors.[index]

                    Array.init levels (fun level ->
                        let size = size / (1 <<< level)
                        data |> PixImage.resized size
                    )
                )

            let format = TextureFormat.ofPixFormat data.[0].[0].PixFormat TextureParams.empty
            let t = runtime.CreateTexture2DArray(size, format, levels = levels, count = count)

            data |> Array.iteri (fun index mipmaps ->
                mipmaps |> Array.iteri (fun level img ->
                    runtime.Upload(t, level, index, img)
                )
            )

            let result =
                data |> Array.mapi (fun index mipmaps ->
                    mipmaps |> Array.mapi (fun level _ ->
                        runtime.Download(t, level, index).ToPixImage<byte>()
                    )
                )

            runtime.DeleteTexture(t)

            (data, result) ||> Array.iter2 (Array.iter2 (fun src dst ->
                    Expect.equal dst.Size src.Size "Unexpected texture size"
                    PixImage.compare V2i.Zero src dst
                )
            )

        let mipmappedCube (runtime : IRuntime) =
            let levels = 3
            let size = V2i(128)

            let data =
                CubeMap.init levels (fun side level ->
                    let data = PixImage.checkerboard testColors.[int side]
                    let size = size / (1 <<< level)
                    data |> PixImage.resized size
                )

            let format = TextureFormat.ofPixFormat data.[CubeSide.PositiveX].PixFormat TextureParams.empty
            let t = runtime.CreateTextureCube(size.X, format, levels = levels)

            data |> CubeMap.iteri (fun side level img ->
                runtime.Upload(t, level, int side, img)
            )

            let result =
                data |> CubeMap.mapi (fun side level _ ->
                    runtime.Download(t, level, int side).ToPixImage<byte>()
                )

            (data, result) ||> CubeMap.iteri2 (fun side level src dst ->
                Expect.equal dst.Size src.Size "Unexpected texture size"
                PixImage.compare V2i.Zero src dst
            )

            runtime.DeleteTexture(t)

        let mipmappedCubeArray (runtime : IRuntime) =
            let count = 2
            let levels = 3
            let size = V2i(128)

            let data =
                Array.init count (fun index ->
                    CubeMap.init levels (fun side level ->
                        let data = PixImage.checkerboard testColors.[index * 6 + int side]
                        let size = size / (1 <<< level)
                        data |> PixImage.resized size
                    )
                )

            let format = TextureFormat.ofPixFormat data.[0].[CubeSide.PositiveX].PixFormat TextureParams.empty
            let t = runtime.CreateTextureCubeArray(size.X, format, levels = levels, count = count)

            data |> Array.iteri (fun index mipmaps ->
                mipmaps |> CubeMap.iteri (fun side level img ->
                    runtime.Upload(t, level, index * 6 + int side, img)
                )
            )

            let result =
                data |> Array.mapi (fun index mipmaps ->
                    mipmaps |> CubeMap.mapi (fun side level _ ->
                        let slice = index * 6 + int side
                        runtime.Download(t, level, slice).ToPixImage<byte>()
                    )
                )

            (data, result) ||> Array.iter2 (CubeMap.iter2 (fun src dst ->
                    Expect.equal dst.Size src.Size "Unexpected texture size"
                    PixImage.compare V2i.Zero src dst
                )
            )

            runtime.DeleteTexture(t)

        let subwindow (runtime : IRuntime) =
            let count = 2
            let levels = 3
            let size = V2i(128)

            let data =
                Array.init count (fun index ->
                    CubeMap.init levels (fun side level ->
                        let data = PixImage.checkerboard testColors.[index * 6 + int side]
                        let size = size / (1 <<< level)
                        data |> PixImage.resized size
                    )
                )

            let format = TextureFormat.ofPixFormat data.[0].[CubeSide.PositiveX].PixFormat TextureParams.empty
            let t = runtime.CreateTextureCubeArray(size.X, format, levels = levels, count = count)

            data |> Array.iteri (fun index mipmaps ->
                mipmaps |> CubeMap.iteri (fun side level img ->
                    runtime.Upload(t, level, index * 6 + int side, img)
                )
            )

            let side = CubeSide.NegativeZ
            let index = 1
            let level = 2
            let region = Box2i.FromMinAndSize(V2i(14, 18), V2i(10, 3))
            let result = runtime.Download(t, level, index * 6 + int side, region).ToPixImage<byte>()

            let reference = data.[index].[side, level].SubImage(region)
            Expect.equal result.Size reference.Size "Unexpected texture size"
            PixImage.compare V2i.Zero reference result

            runtime.DeleteTexture(t)

        let depthAndStencil (runtime : IRuntime) =
            let signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
                ])

            use task =
                let drawCall =
                    DrawCallInfo(
                        FaceVertexCount = 4,
                        InstanceCount = 1
                    )

                let positions = [| V3f(-0.5,-0.5,0.0); V3f(0.5,-0.5,0.0); V3f(-0.5,0.5,0.0); V3f(0.5,0.5,0.0) |]

                let stencilMode =
                    { StencilMode.None with
                        Pass = StencilOperation.Replace;
                        Reference = 3 }

                drawCall
                |> Sg.render IndexedGeometryMode.TriangleStrip
                |> Sg.vertexAttribute DefaultSemantic.Positions (AVal.constant positions)
                |> Sg.stencilMode' stencilMode
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                }
                |> Sg.compile runtime signature


            let size = V2i(256)

            let depthStencilBuffer =
                let clear = clear { depth 1.0; stencil 0 }
                task |> RenderTask.renderToDepthWithClear (AVal.constant size) clear

            depthStencilBuffer.Acquire()

            let depthResult = runtime.DownloadDepth(depthStencilBuffer.GetValue())
            let stencilResult = runtime.DownloadStencil(depthStencilBuffer.GetValue())

            Expect.equal (V2i depthResult.Size) size "Unexpected depth texture size"
            Expect.isGreaterThan (Array.min depthResult.Data) 0.0f "Contains zero depth value"
            Expect.isLessThan (Array.min depthResult.Data) 1.0f "All depth one"

            Expect.equal (V2i stencilResult.Size) size "Unexpected stencil texture size"
            Expect.equal (Array.max stencilResult.Data) 3 "Unexpected stencil value"

            depthStencilBuffer.Release()

            runtime.DeleteFramebufferSignature(signature)

        let argumentsOutOfRange (runtime : IRuntime) =
            let createAndDownload (dimension : TextureDimension) (levels : int) (level : int) (slice : int) (region : Box2i) () =
                let t = runtime.CreateTexture(V3i(128), dimension, TextureFormat.Rgba32f, levels, 1)
                try
                    runtime.Download(t, level, slice, region) |> ignore
                finally
                    runtime.DeleteTexture(t)

            let full = Box2i.Infinite
            let window m s = Box2i.FromMinAndSize(m, s)
            let neg = window V2i.NN V2i.One

            createAndDownload TextureDimension.Texture2D  4 -1  0  full   |> shouldThrowArgExn "level cannot be negative"
            createAndDownload TextureDimension.Texture2D  4  4  0  full   |> shouldThrowArgExn "cannot access texture level"
            createAndDownload TextureDimension.Texture2D  4  2  0  neg    |> shouldThrowArgExn "offset cannot be negative"
            createAndDownload TextureDimension.Texture2D  4  2  0  (window (V2i(8)) (V2i(25, 1))) |> shouldThrowArgExn "exceeds size"

            createAndDownload TextureDimension.TextureCube  4 -1  2  full |> shouldThrowArgExn "level cannot be negative"
            createAndDownload TextureDimension.TextureCube  4  4  2  full |> shouldThrowArgExn "cannot access texture level"
            createAndDownload TextureDimension.TextureCube  4  2 -1  full |> shouldThrowArgExn "slice cannot be negative"
            createAndDownload TextureDimension.TextureCube  4  2  6  full |> shouldThrowArgExn "cannot access texture slice"
            createAndDownload TextureDimension.TextureCube  4  2  3  neg  |> shouldThrowArgExn "offset cannot be negative"
            createAndDownload TextureDimension.TextureCube  4  2  3  (window (V2i(8)) (V2i(25, 1))) |> shouldThrowArgExn "exceeds size"

    let tests (backend : Backend) =
        [
            "Simple",                 Cases.simple
            "Mulitsampled",           Cases.multisampled
            "Mipmapped array",        Cases.mipmappedArray
            "Mipmapped cube",         Cases.mipmappedCube
            "Mipmapped cube array",   Cases.mipmappedCubeArray
            "Subwindow",              Cases.subwindow

            if backend <> Backend.Vulkan then // not implemented
                "Depth and stencil",      Cases.depthAndStencil

            "Arguments out of range", Cases.argumentsOutOfRange
        ]
        |> prepareCases backend "Download"