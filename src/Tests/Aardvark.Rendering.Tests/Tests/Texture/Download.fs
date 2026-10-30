namespace Aardvark.Rendering.Tests.Texture

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Expecto

module TextureDownload =

    module private DefaultDownload =
        let private formats =
            [| TextureFormat.R8, PixFormat.ByteGray
               TextureFormat.R16, PixFormat.UShortGray
               TextureFormat.Rgb8, PixFormat.ByteRGB
               TextureFormat.Rgb16, PixFormat.UShortRGB
               TextureFormat.Rgba8, PixFormat.ByteRGBA
               TextureFormat.Rgba16, PixFormat.UShortRGBA
               TextureFormat.Rg8Snorm, PixFormat(typeof<int8>, Col.Format.RG)
               TextureFormat.Rg16Snorm, PixFormat(typeof<int16>, Col.Format.RG)
               TextureFormat.Rg8ui, PixFormat(typeof<byte>, Col.Format.RG)
               TextureFormat.Rg16ui, PixFormat(typeof<uint16>, Col.Format.RG)
               TextureFormat.Rg8i, PixFormat(typeof<int8>, Col.Format.RG)
               TextureFormat.Rg16i, PixFormat(typeof<int16>, Col.Format.RG)
               TextureFormat.Rg16f, PixFormat(typeof<float16>, Col.Format.RG)
               TextureFormat.Rg32f, PixFormat(typeof<float32>, Col.Format.RG)
               TextureFormat.CompressedRgRgtc2, PixFormat.ByteRGB
               TextureFormat.CompressedSignedRgRgtc2, PixFormat.SByteRGB
               TextureFormat.Rg8, PixFormat(typeof<byte>, Col.Format.RG)
               TextureFormat.Rg16, PixFormat(typeof<uint16>, Col.Format.RG) |]

        type private RecordingRuntime() =
            inherit System.Reflection.DispatchProxy()
            let requests = ResizeArray<Type * ITextureSubResource * V4l * Col.Format * V3i * V3i>()
            member _.Requests = requests
            override _.Invoke(method, args) =
                if method.Name <> "Download" || not method.IsGenericMethod || args.Length <> 5 then
                    failwithf "Unexpected runtime operation: %s" method.Name
                let tensorSize = args.[1].GetType().GetProperty("Size").GetValue(args.[1]) :?> V4l
                requests.Add(method.GetGenericArguments().[0], args.[0] :?> ITextureSubResource,
                             tensorSize, unbox args.[2], unbox args.[3], unbox args.[4])
                null

        type private Texture(runtime : ITextureRuntime, format : TextureFormat) =
            let mutable disposed = 0
            let mutable name = null
            member _.Disposals = disposed
            interface IBackendTexture with
                member _.Runtime = runtime
                member _.Dimension = TextureDimension.Texture2D
                member _.Format = format
                member _.Size = V3i(32, 24, 1)
                member _.MipMapLevels = 3
                member _.Count = 4
                member _.Samples = 1
                member _.Handle = 0UL
                member _.WantMipMaps = true
                member _.Name with get() = name and set value = name <- value
                member _.Dispose() = disposed <- disposed + 1

        let mappings _ =
            for format, expected in formats do
                let actual = TextureFormat.toDownloadFormat format
                Expect.equal actual.Type expected.Type $"{format}: element type"
                Expect.equal actual.Format expected.Format $"{format}: channel semantics"
                Expect.equal actual.ChannelCount expected.ChannelCount $"{format}: channel count"

        let delegation _ =
            let runtime = System.Reflection.DispatchProxy.Create<ITextureRuntime, RecordingRuntime>()
            let recorder = box runtime :?> RecordingRuntime
            for format, expected in formats do
                let owner = new Texture(runtime, format)
                use texture = owner :> IBackendTexture
                for level in [0; 1; 2] do
                    for slice in [0; 1; 3] do
                        for region in [Box2i(); Box2i.Infinite; Box2i.FromMinAndSize(V2i(1, 1), V2i(3, 2))] do
                            let window =
                                if region.IsEmpty || region.IsInfinite then
                                    Box2i.FromMinAndSize(V2i.Zero, V2i(32, 24) >>> level)
                                else region
                            for convenience in [false; true] do
                                recorder.Requests.Clear()
                                let result =
                                    if level = 0 && slice = 0 && region = Box2i() then
                                        if convenience then texture.Download() else runtime.Download(texture)
                                    elif convenience then texture.Download(level, slice, region)
                                    else runtime.Download(texture, level, slice, region)
                                let context = $"{format}, level={level}, slice={slice}, region={region}, convenience={convenience}"
                                Expect.equal result.PixFormat expected (context + ": image format")
                                Expect.equal result.ChannelCount expected.ChannelCount (context + ": image channels")
                                Expect.equal result.Size window.Size (context + ": image size")
                                Expect.equal recorder.Requests.Count 1 (context + ": one backend download")
                                let typ, subresource, tensorSize, col, offset, size = recorder.Requests.[0]
                                Expect.equal typ expected.Type (context + ": backend element type")
                                Expect.equal col expected.Format (context + ": backend color format")
                                Expect.equal tensorSize (V4l(int64 window.SizeX, int64 window.SizeY, 1L, int64 expected.ChannelCount)) (context + ": XY and channel layout")
                                Expect.isTrue (obj.ReferenceEquals(subresource.Texture, texture)) (context + ": texture identity")
                                Expect.equal (subresource.Aspect, subresource.Level, subresource.Slice) (TextureAspect.Color, level, slice) (context + ": subresource")
                                Expect.equal offset window.Min.XYO (context + ": offset")
                                Expect.equal size window.Size.XYI (context + ": extent")
                                Expect.equal owner.Disposals 0 (context + ": caller retains ownership")

    module Cases =

        let private normalizedImage<'T> (convert : int -> 'T) modulus (size : V2i) level slice =
            let image = PixImage<'T>(Col.Format.RG, size)
            for y in 0 .. size.Y - 1 do
                for x in 0 .. size.X - 1 do
                    let red = (17 * x + 31 * y + 43 * level + 61 * slice) % modulus
                    let green = (red + modulus / 2 + 13) % modulus
                    image.Volume.[int64 x, int64 y, 0L] <- convert red
                    image.Volume.[int64 x, int64 y, 1L] <- convert green
            image

        let private compareNormalized<'T when 'T : equality> context (expected : PixImage<'T>) (actual : PixImage) =
            Expect.equal actual.PixFormat (PixFormat(typeof<'T>, Col.Format.RG)) (context + ": image format")
            Expect.equal actual.ChannelCount 2 (context + ": channel count")
            Expect.equal actual.Size expected.Size (context + ": image size")
            try PixImage.compare V2i.Zero expected (actual.AsPixImage<'T>())
            with error -> failtestf "%s: %O" context error

        let private normalizedWhole<'T when 'T : equality> (runtime : IRuntime) format convert modulus =
            let image = normalizedImage<'T> convert modulus (V2i(17, 11)) 0 0
            use texture = runtime.CreateTexture2D(image.Size, format, 1, 1)
            runtime.Upload(texture, image)
            compareNormalized $"{format}, runtime whole image" image (runtime.Download(texture))
            compareNormalized $"{format}, texture whole image" image (texture.Download())

        let normalizedRGWhole (runtime : IRuntime) =
            normalizedWhole<byte> runtime TextureFormat.Rg8 byte 256
            normalizedWhole<uint16> runtime TextureFormat.Rg16 uint16 65536

        let private normalizedSubresources<'T when 'T : equality> (runtime : IRuntime) format convert modulus =
            let size = V2i(32, 24)
            let levels, slices = 3, 3
            let images = Array.init slices (fun slice ->
                Array.init levels (fun level -> normalizedImage<'T> convert modulus (size >>> level) level slice))
            use texture = runtime.CreateTexture2DArray(size, format, levels = levels, count = slices)
            for slice in 0 .. slices - 1 do
                for level in 0 .. levels - 1 do
                    texture.Upload(images.[slice].[level], level, slice)
            for slice in 0 .. slices - 1 do
                for level in 0 .. levels - 1 do
                    let image = images.[slice].[level]
                    let crop = Box2i.FromMinAndSize(V2i(1, 2), V2i(3, 2))
                    for region in [Box2i.FromMinAndSize(V2i.Zero, image.Size); crop] do
                        let expected = image.SubImage(region)
                        let context = $"{format}, level={level}, slice={slice}, region={region}"
                        compareNormalized (context + ", runtime") expected (runtime.Download(texture, level, slice, region))
                        compareNormalized (context + ", texture") expected (texture.Download(level, slice, region))

        let normalizedRGSubresources (runtime : IRuntime) =
            normalizedSubresources<byte> runtime TextureFormat.Rg8 byte 256
            normalizedSubresources<uint16> runtime TextureFormat.Rg16 uint16 65536

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

        let inline private texture2DWithFormatNorm (runtime : IRuntime) (format : TextureFormat) (data : PixImage<'T>) =
            let comp a b = Expect.isLessThanOrEqual (abs (int64 a - int64 b)) 1L
            texture2DWithFormatWithComparer runtime comp format data

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
            data |> texture2DWithFormatNorm runtime TextureFormat.Rgba16Snorm

        let texture2Drgba32ui (runtime : IRuntime) =
            let data = PixImage.random32ui <| V2i(256)
            data |> texture2DWithFormat runtime TextureFormat.Rgba32ui

        let texture2Drgba32f (runtime : IRuntime) =
            let data = PixImage.random32f <| V2i(256)
            data |> texture2DWithFormat32f runtime Accuracy.veryHigh TextureFormat.Rgba32f

        let texture2DMultisampled (runtime : IRuntime) =
            let data = PixImage.random32f <| V2i(256)
            let size = data.Size
            let samples = 8

            let signature = runtime.CreateFramebufferSignature([DefaultSemantic.Colors, TextureFormat.Rgba32f], samples)
            let colorTexture = runtime.CreateTexture2D(size, TextureFormat.Rgba32f, 1, samples)
            let framebuffer = runtime.CreateFramebuffer(signature, [DefaultSemantic.Colors, colorTexture.GetOutputView()])

            let sampler =
                { SamplerState.Default with Filter = TextureFilter.MinMagPoint }

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
                let result = runtime.Download(colorTexture).AsPixImage<float32>()

                Expect.equal result.Size size "Unexpected texture size"
                PixImage.compare V2i.Zero data result

            finally
                framebuffer.Dispose()
                runtime.DeleteTexture(colorTexture)
                signature.Dispose()

        let texture2DArrayMipmapped (runtime : IRuntime) =
            let count = 4
            let levels = 3
            let size = V2i(128)

            let data =
                Array.init count (fun index ->
                    let data = PixImage.random16ui <| V2i(256)

                    Array.init levels (fun level ->
                        let size = size / (1 <<< level)
                        data |> PixImage.resized size
                    )
                )

            let format = TextureFormat.ofPixFormat data.[0].[0].PixFormat TextureParams.None
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
                            runtime.Download(t, level, index).AsPixImage<uint16>()
                        )
                    )

                (data, result) ||> Array.iter2 (Array.iter2 (fun src dst ->
                        Expect.equal dst.Size src.Size "Unexpected texture size"
                        PixImage.compare V2i.Zero src dst
                    )
                )

            finally
                runtime.DeleteTexture(t)


        let private texture2DCompressed (expectedFormat : TextureFormat) (path : string) (window : Box2i option) (runtime : IRuntime) =
            let compressed = EmbeddedResource.getTexture TextureParams.WantMipMaps path

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ])

            let texture =
                runtime.PrepareTexture(compressed)

            let region =
                match window with
                | Some r -> r
                | None -> Box2i(V2i.Zero, texture.Size.XY)

            try
                Expect.equal texture.Dimension TextureDimension.Texture2D "unexpected dimension"
                Expect.equal texture.Count 1 "unexpected count"
                Expect.equal texture.Format expectedFormat "unexpected format"

                use task =
                    Sg.fullScreenQuad
                    |> Sg.diffuseTexture' texture
                    |> Sg.shader {
                        do! DefaultSurfaces.diffuseTexture
                    }
                    |> Sg.compile runtime signature

                let buffer = task |> RenderTask.renderToColor (~~texture.Size.XY)
                buffer.Acquire()

                try
                    let reference = buffer.GetValue().Download(region = region).AsPixImage<byte>()
                    let result = texture.Download(region = region).ToPixImage<byte>()

                    Expect.equal result.Size result.Size "image size mismatch"
                    PixImage.compareWithEpsilon 5uy V2i.Zero reference result

                finally
                    buffer.Release()

            finally
                runtime.DeleteTexture(texture)

        let texture2DCompressedBC1 (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRgbaS3tcDxt1 "data/bc1.dds" None

        let texture2DCompressedBC1Subwindow (runtime : IRuntime) =
            runtime |> requireExtensionGL ["GL_ARB_get_texture_sub_image"]
            let window = Box2i.FromMinAndSize(V2i(3, 7), V2i(67, 31))
            runtime |> texture2DCompressed TextureFormat.CompressedRgbaS3tcDxt1 "data/bc1.dds" (Some window)


        let texture2DCompressedBC2 (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRgbaS3tcDxt3 "data/bc2.dds" None

        let texture2DCompressedBC2Subwindow (runtime : IRuntime) =
            runtime |> requireExtensionGL ["GL_ARB_get_texture_sub_image"]
            let window = Box2i.FromMinAndSize(V2i(4, 16), V2i(64, 32))
            runtime |> texture2DCompressed TextureFormat.CompressedRgbaS3tcDxt3 "data/bc2.dds" (Some window)


        let texture2DCompressedBC3 (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRgbaS3tcDxt5 "data/bc3.dds" None

        let texture2DCompressedBC3Subwindow (runtime : IRuntime) =
            runtime |> requireExtensionGL ["GL_ARB_get_texture_sub_image"]
            let window = Box2i.FromMinAndSize(V2i(0, 0), V2i(3, 2))
            runtime |> texture2DCompressed TextureFormat.CompressedRgbaS3tcDxt5 "data/bc3.dds" (Some window)


        let texture2DCompressedBC4u (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRedRgtc1 "data/bc4.dds" None

        let texture2DCompressedBC4uSubwindow (runtime : IRuntime) =
            runtime |> requireExtensionGL ["GL_ARB_get_texture_sub_image"]
            let window = Box2i(V2i(40, 87), V2i(253, 128))
            runtime |> texture2DCompressed TextureFormat.CompressedRedRgtc1 "data/bc4.dds" (Some window)


        let texture2DCompressedBC5u (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRgRgtc2 "data/bc5.dds" None

        let texture2DCompressedBC5uSubwindow (runtime : IRuntime) =
            runtime |> requireExtensionGL ["GL_ARB_get_texture_sub_image"]
            let window = Box2i(V2i(40, 87), V2i(251, 126))
            runtime |> texture2DCompressed TextureFormat.CompressedRgRgtc2 "data/bc5.dds" (Some window)


        let textureCubeMipmapped (runtime : IRuntime) =
            let levels = 3
            let size = V2i(128)

            let data =
                CubeMap.init levels (fun side level ->
                    let data = PixImage.random16ui <| V2i(256)
                    let size = size / (1 <<< level)
                    data |> PixImage.resized size
                )

            let format = TextureFormat.ofPixFormat data.[CubeSide.PositiveX].PixFormat TextureParams.None
            let t = runtime.CreateTextureCube(size.X, format, levels = levels)

            try
                data |> CubeMap.iteri (fun side level img ->
                    t.Upload(img, level, int side)
                )

                let result =
                    data |> CubeMap.mapi (fun side level _ ->
                        runtime.Download(t, level, int side).AsPixImage<uint16>()
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
                        let data = PixImage.random16ui <| V2i(256)
                        let size = size / (1 <<< level)
                        data |> PixImage.resized size
                    )
                )

            let format = TextureFormat.ofPixFormat data.[0].[CubeSide.PositiveX].PixFormat TextureParams.None
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
                            runtime.Download(t, level, slice).AsPixImage<uint16>()
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
                    let data = PixImage.random16ui <| V2i(256)
                    let size = size >>> level
                    data |> PixImage.resized size
                )

            let format = TextureFormat.ofPixFormat data.[0].PixFormat TextureParams.None
            let t = runtime.CreateTexture2D(size, format, levels = levels)

            try
                data |> Array.iteri (fun level img ->
                    t.Upload(img, level)
                )

                let level = 2
                let region = Box2i.FromMinAndSize(V2i(14, 18), V2i(10, 3))
                let result = runtime.Download(t, level = level, slice = 0, region = region).AsPixImage<uint16>()

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
                    let data = PixImage.random16ui <| V2i(256)
                    let size = size / (1 <<< level)
                    data |> PixImage.resized size
                )

            let format = TextureFormat.ofPixFormat data.[CubeSide.PositiveX].PixFormat TextureParams.None
            let t = runtime.CreateTextureCube(size.X, format, levels = levels)

            try
                data |> CubeMap.iteri (fun side level img ->
                    t.Upload(img, level, int side)
                )

                let side = CubeSide.PositiveY
                let level = 1
                let region = Box2i.FromMinAndSize(V2i(14, 18), V2i(10, 3))
                let result = t.Download(level, int side, region).AsPixImage<uint16>()

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
                        let data = PixImage.random16ui <| V2i(256)
                        let size = size / (1 <<< level)
                        data |> PixImage.resized size
                    )
                )

            let format = TextureFormat.ofPixFormat data.[0].[CubeSide.PositiveX].PixFormat TextureParams.None
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
                let result = t.Download(level, index * 6 + int side, region).AsPixImage<uint16>()

                let reference = data.[index].[side, level].SubImage(region)
                Expect.equal result.Size reference.Size "Unexpected texture size"
                PixImage.compare V2i.Zero reference result

            finally
                runtime.DeleteTexture(t)

        let private renderToDepthStencil runtime format size f =
            let clear =
                clear {
                    depth 0.95
                    stencil 1
                }

            renderQuadToDepthStencil runtime format 1 clear size f

        let textureDepthComponent (format : TextureFormat) (runtime : IRuntime) =
            let size = V2i(256)

            let accuracy =
                match format with
                | TextureFormat.DepthComponent16 -> Accuracy.low
                | TextureFormat.DepthComponent24 -> Accuracy.medium
                | _ -> Accuracy.high

            renderToDepthStencil runtime format size (fun buffer ->
                let depthResult = runtime.DownloadDepth(buffer)
                Expect.validDepthResult depthResult accuracy size 0.5 0.95
            )

        let textureDepthComponent16 = textureDepthComponent TextureFormat.DepthComponent16
        let textureDepthComponent24 = textureDepthComponent TextureFormat.DepthComponent24
        let textureDepthComponent32 = textureDepthComponent TextureFormat.DepthComponent32
        let textureDepthComponent32f = textureDepthComponent TextureFormat.DepthComponent32f

        let textureDepthStencil (format : TextureFormat) (runtime : IRuntime) =
            let size = V2i(256)

            let accuracy =
                match format with
                | TextureFormat.Depth24Stencil8 -> Accuracy.medium
                | _ -> Accuracy.high

            renderToDepthStencil runtime format size (fun buffer ->
                let depthResult = runtime.DownloadDepth(buffer)
                Expect.validDepthResult depthResult accuracy size 0.5 0.95

                let stencilResult = runtime.DownloadStencil(buffer)
                Expect.validStencilResult stencilResult size 1 3
            )

        let textureDepth24Stencil8 = textureDepthStencil TextureFormat.Depth24Stencil8
        let textureDepth32fStencil8 = textureDepthStencil TextureFormat.Depth32fStencil8

        let textureStencilIndex8 (runtime : IRuntime) =
            runtime |> requireExtensionGL ["GL_ARB_texture_stencil8"]
            let size = V2i(256)

            renderToDepthStencil runtime TextureFormat.StencilIndex8 size (fun buffer ->
                let stencilResult = runtime.DownloadStencil(buffer)
                Expect.validStencilResult stencilResult size 1 3
            )

        let windowValidation (runtime : IRuntime) =
            use texture = runtime.CreateTexture3D(V3i(8, 9, 10), TextureFormat.Rgba8, levels = 2)
            // Use a fixed target buffer: an allocating region overload could allocate before validation.
            let result = PixVolume<byte>(Col.Format.RGBA, V3i(2))

            for level in [0; 1] do
                let limit = texture.GetSize(level)
                for axis in [V3i.IOO; V3i.OIO; V3i.OOI] do
                    let windows =
                        [ -axis, V3i.III, "offset cannot be negative"
                          V3i.Zero, V3i.III - axis, "window size must be greater than 0"
                          V3i.Zero, V3i.III - 2 * axis, "window size must be greater than 0"
                          limit * axis, V3i.III, "exceeds size of texture level"
                          V3i.Zero, V3i.III + limit * axis, "exceeds size of texture level"
                          Int32.MaxValue * axis, V3i.III + axis, "exceeds size of texture level"
                          2 * axis, V3i.III + (Int32.MaxValue - 1) * axis, "exceeds size of texture level" ]

                    for offset, size, message in windows do
                        let download() = runtime.Download(texture, result, level = level, offset = offset, size = size)
                        try download |> shouldThrowArgExn message
                        with error -> failtestf "level = %d, offset = %A, size = %A: %O" level offset size error

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

    let tests (target: TestTarget) =
        [
            if target = TestTarget.Cpu then
                "Default format mappings",     DefaultDownload.mappings
                "Default download delegation", DefaultDownload.delegation

            if target.IsGpu then
                "Normalized RG whole images",                  Cases.normalizedRGWhole
                "Normalized RG selected mip slice and region", Cases.normalizedRGSubresources

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

                "2D compressed BC1",            Cases.texture2DCompressedBC1
                "2D compressed BC1 subwindow",  Cases.texture2DCompressedBC1Subwindow
                "2D compressed BC2",            Cases.texture2DCompressedBC2
                "2D compressed BC2 subwindow",  Cases.texture2DCompressedBC2Subwindow
                "2D compressed BC3",            Cases.texture2DCompressedBC3
                "2D compressed BC3 subwindow",  Cases.texture2DCompressedBC3Subwindow
                "2D compressed BC4u",           Cases.texture2DCompressedBC4u
                "2D compressed BC4u subwindow", Cases.texture2DCompressedBC4uSubwindow
                "2D compressed BC5u",           Cases.texture2DCompressedBC5u
                "2D compressed BC5u subwindow", Cases.texture2DCompressedBC5uSubwindow

                "Cube mipmapped",         Cases.textureCubeMipmapped
                "Cube array mipmapped",   Cases.textureCubeArrayMipmapped
                "Cube subwindow",         Cases.textureCubeSubwindow
                "Cube array subwindow",   Cases.textureCubeArraySubwindow

                "DepthComponent16",         Cases.textureDepthComponent16
                "DepthComponent24",         Cases.textureDepthComponent24
                "DepthComponent32",         Cases.textureDepthComponent32
                "DepthComponent32f",        Cases.textureDepthComponent32f
                "Depth24Stencil8",          Cases.textureDepth24Stencil8
                "Depth32fStencil8",         Cases.textureDepth32fStencil8
                "StencilIndex8",            Cases.textureStencilIndex8

                "Arguments out of range",   Cases.argumentsOutOfRange
                "Volume window validation", Cases.windowValidation
        ]
        |> prepareCases "Download" target