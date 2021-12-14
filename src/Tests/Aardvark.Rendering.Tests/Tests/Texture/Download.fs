namespace Aardvark.Rendering.Tests.Texture

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Expecto

module TextureDownload =

    module Cases =

        let private texture2DWithFormatWithComparer<'T> (runtime : IRuntime)
                                                        (comparer : 'T -> 'T -> string-> unit)
                                                        (format : TextureFormat) (data : PixImage<'T>) =
            let texture = runtime.CreateTexture2D(data.Size, format, 1, 1)
            try
                runtime.Upload(texture, data)

                let result = runtime.Download(texture).ToPixImage<'T>()

                Expect.equal result.Size data.Size "Unexpected texture size"
                PixImage.compareWithComparer comparer V2i.Zero data result

            finally
                runtime.DeleteTexture(texture)

        let private texture2DWithFormat<'T when 'T : equality> (runtime : IRuntime) (format : TextureFormat) (data : PixImage<'T>) =
            texture2DWithFormatWithComparer runtime Expect.equal format data

        let private texture2DWithFormat32f (runtime : IRuntime) (accuracy : Accuracy) (format : TextureFormat) (data : PixImage<float32>) =
            let comp a b = Expect.floatClose accuracy (float a) (float b)
            texture2DWithFormatWithComparer runtime comp format data

        let texture2Dr8 (runtime : IRuntime) =
            let data = PixImage.random8ui' Col.Format.Gray <| V2i(123, 321)
            data |> texture2DWithFormat runtime TextureFormat.R8

        let texture2Drgba8 (runtime : IRuntime) =
            let data = PixImage.random8ui <| V2i(256)
            data |> texture2DWithFormat runtime TextureFormat.Rgba8

        let texture2DSrgba8 (runtime : IRuntime) =
            let data = PixImage.random8ui <| V2i(256)
            data |> texture2DWithFormat runtime TextureFormat.Srgb8Alpha8

        let texture2Drgba16 (runtime : IRuntime) =
            let data = PixImage.random16ui <| V2i(256)
            data |> texture2DWithFormat runtime TextureFormat.Rgba16

        let texture2Drgba16snorm (runtime : IRuntime) =
            let data = PixImage.random16i <| V2i(256)
            data |> texture2DWithFormat runtime TextureFormat.Rgba16Snorm

        let texture2Drgba32ui (runtime : IRuntime) =
            let data = PixImage.random32ui <| V2i(256)
            data |> texture2DWithFormat runtime TextureFormat.Rgba32ui

        let texture2Drgba32f (runtime : IRuntime) =
            let data = PixImage.random32f <| V2i(256)
            data |> texture2DWithFormat32f runtime Accuracy.veryHigh TextureFormat.Rgba32f

        let texture2DMultisampled (runtime : IRuntime) =
            let data = PixImage.random <| V2i(256)
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

            try
                task.Run(RenderToken.Empty, framebuffer)
                let result = runtime.Download(colorTexture).ToPixImage<byte>()

                Expect.equal result.Size size "Unexpected texture size"
                PixImage.compare V2i.Zero data result

            finally
                runtime.DeleteFramebuffer(framebuffer)
                runtime.DeleteTexture(colorTexture)
                runtime.DeleteFramebufferSignature(signature)

        let texture2DArrayMipmapped (runtime : IRuntime) =
            let count = 4
            let levels = 3
            let size = V2i(128)

            let data =
                Array.init count (fun index ->
                    let data = PixImage.random <| V2i(256)

                    Array.init levels (fun level ->
                        let size = size / (1 <<< level)
                        data |> PixImage.resized size
                    )
                )

            let format = TextureFormat.ofPixFormat data.[0].[0].PixFormat TextureParams.empty
            let t = runtime.CreateTexture2DArray(size, format, levels = levels, count = count)

            try
                data |> Array.iteri (fun index mipmaps ->
                    mipmaps |> Array.iteri (fun level img ->
                        t.Upload(img, level, index)
                    )
                )

                let result =
                    data |> Array.mapi (fun index mipmaps ->
                        mipmaps |> Array.mapi (fun level _ ->
                            runtime.Download(t, level, index).ToPixImage<byte>()
                        )
                    )

                (data, result) ||> Array.iter2 (Array.iter2 (fun src dst ->
                        Expect.equal dst.Size src.Size "Unexpected texture size"
                        PixImage.compare V2i.Zero src dst
                    )
                )

            finally
                runtime.DeleteTexture(t)


        let textureCubeMipmapped (runtime : IRuntime) =
            let levels = 3
            let size = V2i(128)

            let data =
                CubeMap.init levels (fun side level ->
                    let data = PixImage.random <| V2i(256)
                    let size = size / (1 <<< level)
                    data |> PixImage.resized size
                )

            let format = TextureFormat.ofPixFormat data.[CubeSide.PositiveX].PixFormat TextureParams.empty
            let t = runtime.CreateTextureCube(size.X, format, levels = levels)

            try
                data |> CubeMap.iteri (fun side level img ->
                    t.Upload(img, level, int side)
                )

                let result =
                    data |> CubeMap.mapi (fun side level _ ->
                        runtime.Download(t, level, int side).ToPixImage<byte>()
                    )

                (data, result) ||> CubeMap.iteri2 (fun side level src dst ->
                    Expect.equal dst.Size src.Size "Unexpected texture size"
                    PixImage.compare V2i.Zero src dst
                )

            finally
                runtime.DeleteTexture(t)

        let textureCubeArrayMipmapped (runtime : IRuntime) =
            let count = 2
            let levels = 3
            let size = V2i(128)

            let data =
                Array.init count (fun index ->
                    CubeMap.init levels (fun side level ->
                        let data = PixImage.random <| V2i(256)
                        let size = size / (1 <<< level)
                        data |> PixImage.resized size
                    )
                )

            let format = TextureFormat.ofPixFormat data.[0].[CubeSide.PositiveX].PixFormat TextureParams.empty
            let t = runtime.CreateTextureCubeArray(size.X, format, levels = levels, count = count)

            try
                data |> Array.iteri (fun index mipmaps ->
                    mipmaps |> CubeMap.iteri (fun side level img ->
                        t.Upload(img, level, index * 6 + int side)
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

            finally
                runtime.DeleteTexture(t)

        let texture2DSubwindow (runtime : IRuntime) =
            let size = V2i(128)
            let levels = 3

            let data =
                Array.init levels (fun level ->
                    let data = PixImage.random <| V2i(256)
                    let size = size >>> level
                    data |> PixImage.resized size
                )

            let format = TextureFormat.ofPixFormat data.[0].PixFormat TextureParams.empty
            let t = runtime.CreateTexture2D(size, format, levels = levels)

            try
                data |> Array.iteri (fun level img ->
                    t.Upload(img, level)
                )

                let level = 2
                let region = Box2i.FromMinAndSize(V2i(14, 18), V2i(10, 3))
                let result = runtime.Download(t, level = level, slice = 0, region = region).ToPixImage<byte>()

                let reference = data.[level].SubImage(region)
                Expect.equal result.Size reference.Size "Unexpected texture size"
                PixImage.compare V2i.Zero reference result

            finally
                runtime.DeleteTexture(t)

        let textureCubeSubwindow (runtime : IRuntime) =
            let levels = 3
            let size = V2i(128)

            let data =
                CubeMap.init levels (fun side level ->
                    let data = PixImage.random <| V2i(256)
                    let size = size / (1 <<< level)
                    data |> PixImage.resized size
                )

            let format = TextureFormat.ofPixFormat data.[CubeSide.PositiveX].PixFormat TextureParams.empty
            let t = runtime.CreateTextureCube(size.X, format, levels = levels)

            try
                data |> CubeMap.iteri (fun side level img ->
                    t.Upload(img, level, int side)
                )

                let side = CubeSide.PositiveY
                let level = 1
                let region = Box2i.FromMinAndSize(V2i(14, 18), V2i(10, 3))
                let result = t.Download(level, int side, region).ToPixImage<byte>()

                let reference = data.[side, level].SubImage(region)
                Expect.equal result.Size reference.Size "Unexpected texture size"
                PixImage.compare V2i.Zero reference result

            finally
                runtime.DeleteTexture(t)

        let textureCubeArraySubwindow (runtime : IRuntime) =
            let count = 2
            let levels = 3
            let size = V2i(128)

            let data =
                Array.init count (fun index ->
                    CubeMap.init levels (fun side level ->
                        let data = PixImage.random <| V2i(256)
                        let size = size / (1 <<< level)
                        data |> PixImage.resized size
                    )
                )

            let format = TextureFormat.ofPixFormat data.[0].[CubeSide.PositiveX].PixFormat TextureParams.empty
            let t = runtime.CreateTextureCubeArray(size.X, format, levels = levels, count = count)

            try
                data |> Array.iteri (fun index mipmaps ->
                    mipmaps |> CubeMap.iteri (fun side level img ->
                        t.Upload(img, level, index * 6 + int side)
                    )
                )

                let side = CubeSide.NegativeZ
                let index = 1
                let level = 2
                let region = Box2i.FromMinAndSize(V2i(14, 18), V2i(10, 3))
                let result = t.Download(level, index * 6 + int side, region).ToPixImage<byte>()

                let reference = data.[index].[side, level].SubImage(region)
                Expect.equal result.Size reference.Size "Unexpected texture size"
                PixImage.compare V2i.Zero reference result

            finally
                runtime.DeleteTexture(t)

        type private RenderTo =
            {
                Task : IRenderTask
                Disposable : IDisposable
            }

            member x.Dispose() =
                x.Task.Dispose()
                x.Disposable.Dispose()

            interface IDisposable with
                member x.Dispose() = x.Dispose()

        let private renderTo (runtime : IRuntime) (attachments : seq<Symbol * RenderbufferFormat>) =
            let signature =
                 runtime.CreateFramebufferSignature(attachments)

            let task =
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

            { Task = task
              Disposable =
                { new IDisposable with
                    member x.Dispose() = runtime.DeleteFramebufferSignature(signature)
                }
            }

        let private renderToStencil (runtime : IRuntime) (format : RenderbufferFormat) (size : V2i) (f : IBackendTexture -> 'T) =
            use task =
                let atts = [ DefaultSemantic.Stencil, format ]
                renderTo runtime atts

            let buffer =
                task.Task |> RenderTask.renderToStencil (AVal.constant size)

            buffer.Acquire()

            try
                f <| buffer.GetValue()
            finally
                buffer.Release()

        let private renderToDepth (runtime : IRuntime) (format : RenderbufferFormat) (size : V2i)
                                  (f : IBackendTexture -> 'T) =
            use task =
                let atts = [ DefaultSemantic.Depth, format ]
                renderTo runtime atts

            let buffer =
                task.Task |> RenderTask.renderToDepth (AVal.constant size)

            buffer.Acquire()

            try
                f <| buffer.GetValue()
            finally
                buffer.Release()

        let textureDepthComponent (format : RenderbufferFormat) (runtime : IRuntime) =
            let size = V2i(256)

            renderToDepth runtime format size (fun buffer ->
                let depthResult = runtime.DownloadDepth(buffer)

                Expect.equal (V2i depthResult.Size) size "Unexpected depth texture size"
                Expect.isGreaterThan (Array.min depthResult.Data) 0.0f "Contains zero depth value"
                Expect.isLessThan (Array.min depthResult.Data) 1.0f "All depth one"
            )

        let textureDepthComponent16 = textureDepthComponent RenderbufferFormat.DepthComponent16
        let textureDepthComponent24 = textureDepthComponent RenderbufferFormat.DepthComponent24
        let textureDepthComponent32 = textureDepthComponent RenderbufferFormat.DepthComponent32
        let textureDepthComponent32f = textureDepthComponent RenderbufferFormat.DepthComponent32f

        let textureDepth24Stencil8 (runtime : IRuntime) =
            let size = V2i(256)

            renderToDepth runtime RenderbufferFormat.Depth24Stencil8 size (fun buffer ->
                let depthResult = runtime.DownloadDepth(buffer)

                Expect.equal (V2i depthResult.Size) size "Unexpected depth texture size"
                Expect.isGreaterThan (Array.min depthResult.Data) 0.0f "Contains zero depth value"
                Expect.isLessThan (Array.min depthResult.Data) 1.0f "All depth one"

                let stencilResult = runtime.DownloadStencil(buffer)

                Expect.equal (V2i stencilResult.Size) size "Unexpected stencil texture size"
                Expect.equal (Array.max stencilResult.Data) 3 "Unexpected stencil value"
            )

        let textureDepth32fStencil8 (runtime : IRuntime) =
            let size = V2i(256)

            renderToDepth runtime RenderbufferFormat.Depth32fStencil8 size (fun buffer ->
                let depthResult = runtime.DownloadDepth(buffer)

                Expect.equal (V2i depthResult.Size) size "Unexpected depth texture size"
                Expect.isGreaterThan (Array.min depthResult.Data) 0.0f "Contains zero depth value"
                Expect.isLessThan (Array.min depthResult.Data) 1.0f "All depth one"

                let stencilResult = runtime.DownloadStencil(buffer)

                Expect.equal (V2i stencilResult.Size) size "Unexpected stencil texture size"
                Expect.equal (Array.max stencilResult.Data) 3 "Unexpected stencil value"
            )

        let textureStencilIndex8 (runtime : IRuntime) =
            let size = V2i(256)

            renderToStencil runtime RenderbufferFormat.StencilIndex8 size (fun buffer ->
                let stencilResult = runtime.DownloadStencil(buffer)

                Expect.equal (V2i stencilResult.Size) size "Unexpected stencil texture size"
                Expect.equal (Array.max stencilResult.Data) 3 "Unexpected stencil value"
            )

        let argumentsOutOfRange (runtime : IRuntime) =
            let createAndDownload (dimension : TextureDimension) (levels : int) (level : int) (slice : int) (region : Box2i) () =
                let size =
                    match dimension with
                    | TextureDimension.Texture1D -> V3i(128, 1, 1)
                    | TextureDimension.Texture2D -> V3i(128, 128, 1)
                    | TextureDimension.Texture3D -> V3i(128)
                    | _                          -> V3i(128, 128, 1)

                let t = runtime.CreateTexture(size, dimension, TextureFormat.Rgba32f, levels, 1)
                try
                    t.Download(level, slice, region) |> ignore
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
            "2D r8",                  Cases.texture2Dr8
            "2D rgba8",               Cases.texture2Drgba8
            "2D Srgba8",              Cases.texture2DSrgba8
            "2D rgba16",              Cases.texture2Drgba16
            "2D rgba16snorm",         Cases.texture2Drgba16snorm
            "2D rgba32ui",            Cases.texture2Drgba32ui
            "2D rgba32f",             Cases.texture2Drgba32f
            "2D mulitsampled",        Cases.texture2DMultisampled
            "2D level subwindow",     Cases.texture2DSubwindow
            "2D array mipmapped",     Cases.texture2DArrayMipmapped
            "Cube mipmapped",         Cases.textureCubeMipmapped
            "Cube array mipmapped",   Cases.textureCubeArrayMipmapped
            "Cube subwindow",         Cases.textureCubeSubwindow
            "Cube array subwindow",   Cases.textureCubeArraySubwindow

            if backend <> Backend.Vulkan then // not implemented
                "DepthComponent16",         Cases.textureDepthComponent16
                "DepthComponent24",         Cases.textureDepthComponent24
                "DepthComponent32",         Cases.textureDepthComponent32
                "DepthComponent32f",        Cases.textureDepthComponent32f
                "Depth24Stencil8",          Cases.textureDepth24Stencil8
                "Depth32fStencil8",         Cases.textureDepth32fStencil8
                "StencilIndex8",            Cases.textureStencilIndex8

            "Arguments out of range", Cases.argumentsOutOfRange
        ]
        |> prepareCases backend "Download"