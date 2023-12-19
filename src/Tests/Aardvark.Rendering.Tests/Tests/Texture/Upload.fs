namespace Aardvark.Rendering.Tests.Texture

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open FShade
open Expecto

module TextureUpload =

    [<RequireQualifiedAccess>]
    type MipmapInput =
        | None
        | Partial
        | Full

        member x.GetLevels(size : V3i) =
            match x with
            | None -> 1
            | Partial -> Fun.MipmapLevels(size) / 2
            | Full -> Fun.MipmapLevels(size)

        member x.GetLevels(size : V2i) =
            x.GetLevels(size.XYI)

        member x.GetLevels(size : int) =
            x.GetLevels(V2i size)

    module private Shader =
        let diffuseSampler =
            sampler2d {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagLinear
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }

        let diffuseTextureLod (level : float) (v : Effects.Vertex) =
            fragment {
                return diffuseSampler.SampleLevel(v.tc, level)
            }

    module Cases =

        module private NativeTexture =

            let ofPixImages (format : TextureFormat) (wantMipmap : bool) (data : PixImage<'T>[][]) =
                let nativeData =
                    data |> Array.map (Array.map (fun pi ->
                        { new INativeTextureData with
                            member x.Size = V3i(pi.Size, 1)
                            member x.SizeInBytes = int64 <| pi.Array.Length * sizeof<'T>
                            member x.Use (f : nativeint -> 'U) = pinned pi.Array f }
                    ))

                { new INativeTexture with
                    member x.Format = format
                    member x.Dimension = TextureDimension.Texture2D
                    member x.MipMapLevels = data |> Array.map Array.length |> Array.min
                    member x.Count = data.Length
                    member x.Item with get(slice, level) = nativeData.[slice].[level]
                    member x.WantMipMaps = wantMipmap }

        let private uploadAndDownloadTexture1D (runtime : IRuntime)
                                               (size : int) (levels : int) (count : int)
                                               (level : int) (slice : int)
                                               (window : Range1i option) =
            let region =
                match window with
                | Some r -> r
                | None ->
                    let s = max 1 (size >>> level)
                    Range1i(0, s)

            let regionOffset = V3i(region.Min, 0, 0)
            let regionSize = V3i(region.Size, 1, 1)
            let data = PixVolume.random32ui regionSize
            let fmt = TextureFormat.ofPixFormat data.PixFormat TextureParams.empty

            let texture =
                if count > 1 then
                    runtime.CreateTexture1DArray(size, fmt, levels, count)
                else
                    runtime.CreateTexture1D(size, fmt, levels)

            try
                let target = texture.[TextureAspect.Color, level, slice]
                target.Upload(data, regionOffset, regionSize)

                let result = PixVolume<uint32>(Col.Format.RGBA, regionSize)
                target.Download(result, regionOffset, regionSize)

                PixVolume.compare V3i.Zero data result

            finally
                runtime.DeleteTexture(texture)

        let texture1D (runtime : IRuntime) =
            uploadAndDownloadTexture1D runtime 100 1 1 0 0 None

        let texture1DSubwindow (runtime : IRuntime) =
            let region = Some <| Range1i(11, 87)
            uploadAndDownloadTexture1D runtime 100 1 1 0 0 region

        let texture1DLevel (runtime : IRuntime) =
            uploadAndDownloadTexture1D runtime 100 4 1 2 0 None

        let texture1DLevelSubwindow (runtime : IRuntime) =
            let region = Some <| Range1i(13, 47)
            uploadAndDownloadTexture1D runtime 100 4 1 1 0 region

        let texture1DArray (runtime : IRuntime) =
            uploadAndDownloadTexture1D runtime 100 1 5 0 2 None

        let texture1DArraySubwindow (runtime : IRuntime) =
            let region = Some <| Range1i(11, 87)
            uploadAndDownloadTexture1D runtime 100 1 5 0 2 region

        let texture1DArrayLevel (runtime : IRuntime) =
            uploadAndDownloadTexture1D runtime 100 4 5 2 1 None

        let texture1DArrayLevelSubwindow (runtime : IRuntime) =
            let region = Some <| Range1i(13, 47)
            uploadAndDownloadTexture1D runtime 100 4 5 1 3 region

        let private renderQuadWithNullTexture (shader : ISg -> ISg) (randomTexture : ITexture) (runtime : IRuntime) =
            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ])

            // First render with a random texture to ensure OpenGL actually binds a null texture in the appropriate slot
            // If it doesn't the random texture will still be bound
            use randomTask =
                Sg.fullScreenQuad
                |> Sg.diffuseTexture' randomTexture
                |> shader
                |> Sg.compile runtime signature

            let randomBuffer = randomTask |> RenderTask.renderToColor (AVal.init <| V2i(256))
            randomBuffer.Acquire()

            try randomBuffer.GetValue() |> ignore
            finally randomBuffer.Release()

            use task =
                Sg.fullScreenQuad
                |> Sg.diffuseTexture' nullTexture
                |> shader
                |> Sg.compile runtime signature

            // Render sampling the null texture.
            // We can't check the result since null textures are uninitialized, just make sure there are no errors.
            let buffer = task |> RenderTask.renderToColor (AVal.init <| V2i(256))
            buffer.Acquire()
            try buffer.GetValue() |> ignore
            finally buffer.Release()

        let texture1DNull (runtime : IRuntime) =
            let diffuseSampler =
                sampler1d {
                    texture uniform?DiffuseColorTexture
                    filter Filter.MinMagLinear
                    addressU WrapMode.Clamp
                    addressV WrapMode.Clamp
                }

            let diffuseTexture (v : Effects.Vertex) =
                fragment {
                    return diffuseSampler.Sample(v.tc.X)
                }

            let shader =
                Sg.shader {
                    do! diffuseTexture
                }

            use randomTexture = runtime.CreateTexture1D(8, TextureFormat.Rgba8)
            randomTexture.Upload(PixVolume.random8ui <| V3i(8, 1, 1))

            runtime |> renderQuadWithNullTexture shader randomTexture

        let texture1DNullArray (runtime : IRuntime) =
            let diffuseSampler =
                sampler1dArray {
                    texture uniform?DiffuseColorTexture
                    filter Filter.MinMagLinear
                    addressU WrapMode.Clamp
                    addressV WrapMode.Clamp
                }

            let diffuseTexture (v : Effects.Vertex) =
                fragment {
                    return diffuseSampler.Sample(v.tc.X, 32)
                }

            let shader =
                Sg.shader {
                    do! diffuseTexture
                }

            use randomTexture = runtime.CreateTexture1DArray(8, TextureFormat.Rgba8, count = 1)
            randomTexture.Upload(PixVolume.random8ui <| V3i(8, 1, 1))

            runtime |> renderQuadWithNullTexture shader randomTexture


        let private uploadAndDownloadTexture2D' (randomPix : V2i -> PixImage<'T>)
                                                (runtime : IRuntime)
                                                (size : V2i) (levels : int) (count : int) (samples : int)
                                                (level : int) (slice : int)
                                                (window : Box2i option) =
            let region =
                match window with
                | Some r -> r
                | None ->
                    let s = max 1 (size >>> level)
                    Box2i(V2i.Zero, s)

            let data = randomPix region.Size
            let fmt = TextureFormat.ofPixFormat data.PixFormat TextureParams.empty

            let texture =
                if count > 1 then
                    runtime.CreateTexture2DArray(size, fmt, levels = levels, samples = samples, count = count)
                else
                    runtime.CreateTexture2D(size, fmt, levels = levels, samples = samples)

            try
                texture.Upload(data, level, slice, region.Min)
                let result = texture.Download(level, slice, region).AsPixImage<'T>()

                PixImage.compare V2i.Zero data result

            finally
                runtime.DeleteTexture(texture)

        let private uploadAndDownloadTexture2D =
            uploadAndDownloadTexture2D' PixImage.random32ui

        let texture2D (runtime : IRuntime) =
            let size = V2i(100, 75)
            uploadAndDownloadTexture2D runtime size 1 1 1 0 0 None

        let texture2DBgra (runtime : IRuntime) =
            let size = V2i(100, 75)
            uploadAndDownloadTexture2D' (PixImage.random8ui' Col.Format.BGRA) runtime size 1 1 1 0 0 None

        let texture2DSubwindow (runtime : IRuntime) =
            let size = V2i(100, 75)
            let region = Some <| Box2i(11, 34, 87, 66)
            uploadAndDownloadTexture2D runtime size 1 1 1 0 0 region

        let texture2DLevel (runtime : IRuntime) =
            let size = V2i(100, 75)
            uploadAndDownloadTexture2D runtime size 4 1 1 2 0 None

        let texture2DLevelSubwindow (runtime : IRuntime) =
            let size = V2i(100, 75)
            let region = Some <| Box2i(11, 25, 47, 29)
            uploadAndDownloadTexture2D runtime size 4 1 1 1 0 region

        let texture2DArray (runtime : IRuntime) =
            let size = V2i(100, 75)
            uploadAndDownloadTexture2D runtime size 1 5 1 0 1 None

        let texture2DArraySubwindow (runtime : IRuntime) =
            let size = V2i(100, 75)
            let region = Some <| Box2i(11, 34, 87, 66)
            uploadAndDownloadTexture2D runtime size 1 5 1 0 2 region

        let texture2DArrayLevel (runtime : IRuntime) =
            let size = V2i(100, 75)
            uploadAndDownloadTexture2D runtime size 4 5 1 2 3 None

        let texture2DArrayLevelSubwindow (runtime : IRuntime) =
            let size = V2i(100, 75)
            let region = Some <| Box2i(11, 25, 47, 29)
            uploadAndDownloadTexture2D runtime size 4 5 1 1 4 region

        let texture2DMultisampled (runtime : IRuntime) =
            let size = V2i(100, 75)
            uploadAndDownloadTexture2D runtime size 1 1 4 0 0 None

        let texture2DMultisampledSubwindow (runtime : IRuntime) =
            let size = V2i(100, 75)
            let region = Some <| Box2i(11, 34, 87, 66)
            uploadAndDownloadTexture2D runtime size 1 1 4 0 0 region

        let texture2DMultisampledArray (runtime : IRuntime) =
            let size = V2i(100, 75)
            uploadAndDownloadTexture2D runtime size 1 5 4 0 1 None

        let texture2DMultisampledArraySubwindow (runtime : IRuntime) =
            let size = V2i(100, 75)
            let region = Some <| Box2i(11, 34, 87, 66)
            uploadAndDownloadTexture2D runtime size 1 5 4 0 2 region

        let texture2DRgbVsBgrFormat (runtime : IRuntime) =
            let size = V2i(256)
            let piRgb = PixImage<byte>(Col.Format.RGB, size)
            let piBgr = PixImage<byte>(Col.Format.BGR, size)

            let fill (pi : PixImage<byte>) =
                pi.GetChannel(Col.Channel.Red).SetByCoord(fun _ -> 255uy) |> ignore
                pi.GetChannel(Col.Channel.Green).SetByCoord(fun _ -> 0uy) |> ignore
                pi.GetChannel(Col.Channel.Blue).SetByCoord(fun _ -> 100uy) |> ignore

            fill piRgb
            fill piBgr

            // Direct upload
            let texture = runtime.CreateTexture2D(size, TextureFormat.Rgba8)

            texture.Upload(piRgb)
            let piRgbDirectlyUploaded = texture.Download().AsPixImage<uint8>().ToFormat(Col.Format.RGB)

            texture.Upload(piBgr)
            let piBgrDirectlyUploaded = texture.Download().AsPixImage<uint8>().ToFormat(Col.Format.BGR)

            // Prepared
            let texturePreparedRgb = runtime.PrepareTexture(PixTexture2d(piRgb, false))
            let piRgbPrepared = texturePreparedRgb.Download().AsPixImage<uint8>().ToFormat(Col.Format.RGB)

            let texturePreparedBgr = runtime.PrepareTexture(PixTexture2d(piBgr, false))
            let piBgrPrepared = texturePreparedBgr.Download().AsPixImage<uint8>().ToFormat(Col.Format.BGR)

            // Rendered
            use signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ]

            let compileTask (pi : PixImage) =
                Sg.fullScreenQuad
                |> Sg.diffuseTexture' (PixTexture2d(pi, false))
                |> Sg.shader {
                    do! DefaultSurfaces.diffuseTexture
                }
                |> Sg.compile runtime signature

            let renderAndDownload (pi : PixImage<'T>) =
                use task = compileTask pi
                let buffer = task |> RenderTask.renderToColor ~~size
                buffer.Acquire()

                try
                    buffer.GetValue().Download().AsPixImage<'T>().ToFormat(pi.Format)
                finally
                    buffer.Release()

            let piRgbRendered = renderAndDownload piRgb
            let piBgrRendered = renderAndDownload piBgr

            try
                PixImage.compare V2i.Zero piRgb piBgr

                PixImage.compare V2i.Zero piRgb piRgbDirectlyUploaded
                PixImage.compare V2i.Zero piBgr piBgrDirectlyUploaded

                PixImage.compare V2i.Zero piRgb piRgbPrepared
                PixImage.compare V2i.Zero piBgr piBgrPrepared

                PixImage.compare V2i.Zero piRgb piRgbRendered
                PixImage.compare V2i.Zero piBgr piBgrRendered

            finally
                runtime.DeleteTexture texture
                runtime.DeleteTexture texturePreparedRgb
                runtime.DeleteTexture texturePreparedBgr

        let private uploadAndDownloadPixTexture2D
            (randomPix : V2i -> PixImage<'T>) (zero : 'T) (expectedFormat : TextureFormat)
            (runtime : IRuntime) (bgra : bool) (size : V2i) (levels : int) (textureParams : TextureParams) =

            let expectedLevels = if textureParams.wantMipMaps then Fun.MipmapLevels(size) else 1

            let data =
                Array.init levels (fun level ->
                    let size = Fun.MipmapLevelSize(size, level)
                    let format = if bgra then Col.Format.BGRA else Col.Format.RGBA
                    PixImage<'T>(format, randomPix <| V2i size)
                )

            use texture =
                let data = data |> Array.map (fun pi -> pi :> PixImage)
                let pix = PixTexture2d(PixImageMipMap data, textureParams)
                runtime.PrepareTexture pix

            Expect.equal texture.Format expectedFormat "unexpected format"
            Expect.equal texture.MipMapLevels expectedLevels "unexpected number of mipmap levels"

            for level = 0 to expectedLevels - 1 do
                let result = runtime.Download(texture, level = level).AsPixImage<'T>()
                Expect.equal result.Size (Fun.MipmapLevelSize(size, level)) "image size mismatch"
                if level < levels then
                    PixImage.compare V2i.Zero data.[level] result
                else
                    let maxValue = result.Array |> unbox<'T[]> |> Array.max
                    Expect.isGreaterThan maxValue zero "image all black"

        let pixTexture2D (runtime : IRuntime) =
            let size = V2i(256)
            uploadAndDownloadPixTexture2D
                PixImage.random16ui 0us TextureFormat.Rgba16
                runtime false size 1 TextureParams.empty

        let pixTexture2DBgra (runtime : IRuntime) =
            let size = V2i(256)
            uploadAndDownloadPixTexture2D
                PixImage.random16ui 0us TextureFormat.Rgba16
                runtime true size 1 TextureParams.empty

        let pixTexture2DSrgb (runtime : IRuntime) =
            let size = V2i(256)
            uploadAndDownloadPixTexture2D
                PixImage.random8ui 0uy TextureFormat.Srgb8Alpha8
                runtime false size 1 TextureParams.srgb

        let pixTexture2DMipmapped (mipmapInput : MipmapInput) (runtime : IRuntime) =
            let size = V2i(435, 231)
            let levels = mipmapInput.GetLevels(size)
            uploadAndDownloadPixTexture2D
                PixImage.random16ui 0us TextureFormat.Rgba16
                runtime false size levels TextureParams.mipmapped

        let pixTexture2DMipmappedInteger (mipmapInput : MipmapInput) (runtime : IRuntime) =
            let size = V2i(435, 231)
            let levels = mipmapInput.GetLevels(size)
            uploadAndDownloadPixTexture2D
                PixImage.random32ui 0u TextureFormat.Rgba32ui
                runtime false size levels TextureParams.mipmapped

        let pixTexture2DPixVolume (runtime : IRuntime) =
            let size = V2i(256)
            let data = PixImage.random16ui <| V2i size

            let texture =
                let data = data :> PixImage
                let pix = PixTexture2d(PixImageMipMap data, TextureParams.empty)
                runtime.PrepareTexture pix

            try
                let result =
                    let pv = PixVolume<uint16>(Col.Format.RGBA, V3i(size, 1))
                    texture.Download(pv)
                    let volume = new Volume<uint16>(pv.Tensor4.Data, pv.Tensor4.Info.SubXYZVolume(0L));
                    PixImage<uint16>(Col.Format.RGBA, volume)

                Expect.equal result.Size size "image size mismatch"
                PixImage.compare V2i.Zero data result
            finally
                runtime.DeleteTexture(texture)

        let inline private texture2DOnTheFlyCompressed (format : TextureFormat) (data : PixImage<'T>) (texture : ITexture) (generateMipmap : bool) (runtime : IRuntime) =
            let expectedLevels = if generateMipmap then Fun.MipmapLevels(data.Size) else 1

            let levelData = Array.zeroCreate expectedLevels
            levelData.[0] <- data

            for level = 1 to expectedLevels - 1 do
                let size = Fun.MipmapLevelSize(data.Size, level)
                levelData[level] <- levelData[level - 1] |> PixImage.resized size

            let texture =
                runtime.PrepareTexture texture

            try
                Expect.equal texture.MipMapLevels expectedLevels "unexpected mipmap count"
                Expect.equal texture.Format format "unexpected texture format"

                for level = 0 to expectedLevels - 1 do
                    let output = runtime.Download(texture, level = level).AsPixImage<'T>()
                    Expect.equal output.Size levelData.[level].Size "image size mismatch"

                    let psnr = PixImage.peakSignalToNoiseRatio levelData.[level] output
                    Expect.isGreaterThan psnr 10.0 "Bad peak-signal-to-noise ratio"

            finally
                runtime.DeleteTexture(texture)

        let pixTexture2DCompressed (generateMipmap : bool) (runtime : IRuntime) =
            let data = EmbeddedResource.loadPixImage<uint8> "data/spiral.png"
            let texture = PixTexture2d(data, { TextureParams.compressed with wantMipMaps = generateMipmap })
            runtime |> texture2DOnTheFlyCompressed TextureFormat.CompressedRgbaS3tcDxt5 data texture generateMipmap

        let streamTextureCompressed (generateMipmap : bool) (runtime : IRuntime) =
            let getStream() =
                EmbeddedResource.get "data/spiral.png"

            let data = PixImage.Load(getStream()).AsPixImage<uint8>()
            let texture = StreamTexture(getStream, { TextureParams.compressed with wantMipMaps = generateMipmap })
            runtime |> texture2DOnTheFlyCompressed TextureFormat.CompressedRgbaS3tcDxt5 data texture generateMipmap

        let private texture2DCompressed (expectedFormat : TextureFormat) (pathReference : string) (path : string) (level : int) (runtime : IRuntime) =
            let reference = EmbeddedResource.loadPixImage<uint8> pathReference
            let compressed = EmbeddedResource.getTexture TextureParams.mipmappedCompressed path

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ])

            let texture =
                runtime.PrepareTexture(compressed)

            try
                Expect.equal texture.Dimension TextureDimension.Texture2D "unexpected dimension"
                Expect.equal texture.Count 1 "unexpected count"
                Expect.equal texture.Format expectedFormat "unexpected format"
                Expect.equal texture.MipMapLevels (Fun.MipmapLevels(reference.Size)) "unexpected mipmap count"

                let levelSize = Fun.MipmapLevelSize(reference.Size, level)
                let reference = reference |> PixImage.resized levelSize

                match expectedFormat with
                | TextureFormat.CompressedRgbaS3tcDxt1
                | TextureFormat.CompressedSrgbAlphaS3tcDxt1 ->
                    reference.GetMatrix<C4b>().Apply(fun color ->
                        if color.A < 127uy then
                            C4b.Zero
                        else
                            C4b(color.RGB, 255uy)
                    ) |> ignore

                | TextureFormat.CompressedRedRgtc1
                | TextureFormat.CompressedSignedRedRgtc1 ->
                    let red = reference.GetChannel(0L)
                    reference.GetChannel(Col.Channel.Green).Set(red) |> ignore
                    reference.GetChannel(Col.Channel.Blue).Set(red) |> ignore

                | TextureFormat.CompressedRgRgtc2
                | TextureFormat.CompressedSignedRgRgtc2 ->
                    reference.GetChannel(Col.Channel.Blue).Set(0uy) |> ignore

                | _ ->
                    ()

                use task =
                    Sg.fullScreenQuad
                    |> Sg.diffuseTexture' texture
                    |> Sg.shader {
                        do! Shader.diffuseTextureLod (float level)
                    }
                    |> Sg.compile runtime signature

                let buffer = task |> RenderTask.renderToColor (~~reference.Size)
                buffer.Acquire()

                try
                    let result = buffer.GetValue().Download().AsPixImage<byte>()
                    Expect.equal result.Size reference.Size "image size mismatch"

                    let psnr = PixImage.peakSignalToNoiseRatio reference result
                    Expect.isGreaterThan psnr 30.0 "bad peak signal-to-noise ratio"

                finally
                    buffer.Release()

            finally
                runtime.DeleteTexture(texture)

        let texture2DCompressedDDSBC1 (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRgbaS3tcDxt1 "data/rgba.png" "data/bc1.dds" 2

        let texture2DCompressedDDSBC1MipmapGeneration (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRgbaS3tcDxt1 "data/rgba.png" "data/bc1_no_mip.dds" 2

        let texture2DCompressedDDSBC2 (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRgbaS3tcDxt3 "data/rgba.png" "data/bc2.dds" 1

        let texture2DCompressedDDSBC2MipmapGeneration (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRgbaS3tcDxt3 "data/rgba.png" "data/bc2_no_mip.dds" 1

        let texture2DCompressedDDSBC3 (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRgbaS3tcDxt5 "data/rgba.png" "data/bc3.dds" 1

        let texture2DCompressedDDSBC3MipmapGeneration (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRgbaS3tcDxt5 "data/rgba.png" "data/bc3_no_mip.dds" 1

        let texture2DCompressedDDSBC4u (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRedRgtc1 "data/rgb.png" "data/bc4.dds" 1

        let texture2DCompressedDDSBC4uMipmapGeneration (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRedRgtc1 "data/rgb.png" "data/bc4_no_mip.dds" 1

        let texture2DCompressedDDSBC5u (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRgRgtc2 "data/rgb.png" "data/bc5.dds" 1

        let texture2DCompressedDDSBC5uMipmapGeneration (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRgRgtc2 "data/rgb.png" "data/bc5_no_mip.dds" 1

        let texture2DCompressedDDSBC6h (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRgbBptcUnsignedFloat "data/rgb.png" "data/bc6h.dds" 1

        let texture2DCompressedDDSBC7 (runtime : IRuntime) =
            runtime |> texture2DCompressed TextureFormat.CompressedRgbaBptcUnorm "data/rgba.png" "data/bc7.dds" 1

        let texture2DCompressedSubwindow (runtime : IRuntime) =
            let data = EmbeddedResource.loadPixImage<uint8> "data/spiral.png"

            let texture =
                runtime.CreateTexture2D(V2i(100, 80), TextureFormat.CompressedRgbaS3tcDxt5)

            let region = Box2i(12, 24, 80, 76)

            let reference =
                PixImage.cropped (Box2i.FromMinAndSize(V2i.Zero, region.Size)) data

            try
                texture.Upload(data, offset = region.Min, size = region.Size)

                let output = texture.Download(region = region).AsPixImage<uint8>()
                Expect.equal output.Size region.Size "image size mismatch"

                let psnr = PixImage.peakSignalToNoiseRatio reference output
                let rmse = PixImage.rootMeanSquaredError reference output
                Expect.isGreaterThan psnr 6.8 "Bad peak-signal-to-noise ratio"
                Expect.isLessThan rmse 4.2 "Bad root-mean-square error"

            finally
                runtime.DeleteTexture(texture)

        let private uploadAndDownloadTextureNative (runtime : IRuntime) (size : V2i)
                                                   (levels : int) (count : int) (wantMipmap : bool) =
            let expectedLevels = if wantMipmap then Fun.MipmapLevels(size) else 1

            let data =
                 Array.init count (fun _ ->
                     Array.init levels (fun i ->
                         let size = max 1 (size >>> i)
                         PixImage.random16ui size
                     )
                 )

            let texture =
                NativeTexture.ofPixImages TextureFormat.Rgba16 wantMipmap data
                |> runtime.PrepareTexture

            try
                Expect.equal texture.MipMapLevels expectedLevels "unexpected number of mipmap levels"

                for slice = 0 to count - 1 do
                    for level = 0 to expectedLevels - 1 do
                        let result = runtime.Download(texture, level = level, slice = slice)
                        let result = result.Transformed(ImageTrafo.MirrorY).AsPixImage<uint16>()

                        Expect.equal result.Size (Fun.MipmapLevelSize(size, level)) "image size mismatch"
                        if level < levels then
                            PixImage.compare V2i.Zero data.[slice].[level] result
                        else
                            let maxValue = result.Array |> unbox<uint16[]> |> Array.max
                            Expect.isGreaterThan maxValue 0us "image all black"

            finally
                runtime.DeleteTexture(texture)

        let texture2DNative (runtime : IRuntime) =
            let size = V2i(258, 125)
            let levels = Fun.MipmapLevels(size)
            let count = 5
            uploadAndDownloadTextureNative runtime size levels count true

        let texture2DNativeMipmapGeneration (runtime : IRuntime) =
            let size = V2i(258, 125)
            let levels = Fun.MipmapLevels(size) / 2
            let count = 3
            uploadAndDownloadTextureNative runtime size levels count true

        let texture2DNull (runtime : IRuntime) =
            let shader =
                Sg.shader {
                    do! DefaultSurfaces.diffuseTexture
                }

            use randomTexture = runtime.CreateTexture2D(V2i(8, 8), TextureFormat.Rgba8)
            randomTexture.Upload(PixImage.random8ui <| V2i(8, 8))

            runtime |> renderQuadWithNullTexture shader randomTexture

        let texture2DNullArray (runtime : IRuntime) =
            let diffuseSampler =
                sampler2dArray {
                    texture uniform?DiffuseColorTexture
                    filter Filter.MinMagLinear
                    addressU WrapMode.Clamp
                    addressV WrapMode.Clamp
                }

            let diffuseTexture (v : Effects.Vertex) =
                fragment {
                    return diffuseSampler.Sample(v.tc, 16)
                }

            let shader =
                Sg.shader {
                    do! diffuseTexture
                }

            use randomTexture = runtime.CreateTexture2DArray(V2i(8, 8), TextureFormat.Rgba8, count = 1)
            randomTexture.Upload(PixImage.random8ui <| V2i(8, 8))

            runtime |> renderQuadWithNullTexture shader randomTexture

        let texture2DNullMultisampled (runtime : IRuntime) =
            let diffuseSampler =
                sampler2dMS {
                    texture uniform?DiffuseColorTexture
                    filter Filter.MinMagLinear
                    addressU WrapMode.Clamp
                    addressV WrapMode.Clamp
                }

            let diffuseTexture (v : Effects.Vertex) =
                fragment {
                    return diffuseSampler.Read(V2i.Zero, 0)
                }

            let shader =
                Sg.shader {
                    do! diffuseTexture
                }

            runtime |> renderQuadWithNullTexture shader nullTexture

        let texture2DNullMultisampledArray (runtime : IRuntime) =
            let diffuseSampler =
                sampler2dArrayMS {
                    texture uniform?DiffuseColorTexture
                    filter Filter.MinMagLinear
                    addressU WrapMode.Clamp
                    addressV WrapMode.Clamp
                }

            let diffuseTexture (v : Effects.Vertex) =
                fragment {
                    return diffuseSampler.Read(V2i.Zero, 0, 0)
                }

            let shader =
                Sg.shader {
                    do! diffuseTexture
                }

            runtime |> renderQuadWithNullTexture shader nullTexture

        let texture2DNullInt(runtime : IRuntime) =
            let diffuseSampler =
                intSampler2d {
                    texture uniform?DiffuseColorTexture
                    filter Filter.MinMagPoint
                    addressU WrapMode.Clamp
                    addressV WrapMode.Clamp
                }

            let diffuseTexture (v : Effects.Vertex) =
                fragment {
                    return V4d(diffuseSampler.Read(V2i.Zero, 0))
                }

            let shader =
                Sg.shader {
                    do! diffuseTexture
                }

            use randomTexture = runtime.CreateTexture2D(V2i(8, 8), TextureFormat.Rgba8ui)
            randomTexture.Upload(PixImage.random8ui <| V2i(8, 8))

            runtime |> renderQuadWithNullTexture shader randomTexture

        let texture2DNullShadow(runtime : IRuntime) =
            let diffuseSampler =
                sampler2dShadow {
                    texture uniform?DiffuseColorTexture
                    filter Filter.MinMagLinear
                    addressU WrapMode.Clamp
                    addressV WrapMode.Clamp
                }

            let diffuseTexture (v : Effects.Vertex) =
                fragment {
                    return V4d(diffuseSampler.Sample(v.tc, 0.0))
                }

            let shader =
                Sg.shader {
                    do! diffuseTexture
                }

            runtime |> renderQuadWithNullTexture shader nullTexture

        let texture2DStreaming (runtime : IRuntime) =
            let texture = runtime.CreateStreamingTexture(false)

            let size = V2i(256)
            let input = PixImage<uint8>(Col.Format.BGRA, PixImage.random8ui size)

            try
                PixImage.pin input (fun src ->
                    texture.Update(input.PixFormat, size, src.Address)
                )

                let output = (texture.GetValue() :?> IBackendTexture).Download().Transformed(ImageTrafo.MirrorY).AsPixImage<uint8>()
                PixImage.compare V2i.Zero input output

                let pos = V2i(123, 231)
                let pixel = texture.ReadPixel(pos).ToC4b()
                Expect.equal pixel (output.GetMatrix<C4b>().[pos]) "Pixel read failed"

            finally
                runtime.DeleteStreamingTexture(texture)

        let private uploadAndDownloadTexture3D (runtime : IRuntime)
                                               (size : V3i) (levels : int) (level : int)
                                               (window : Box3i option) =
            let region =
                match window with
                | Some r -> r
                | None ->
                    let s = max 1 (size >>> level)
                    Box3i(V3i.Zero, s)

            let data = PixVolume.random32ui region.Size
            let fmt = TextureFormat.ofPixFormat data.PixFormat TextureParams.empty

            let texture =
                runtime.CreateTexture3D(size, fmt, levels)

            try
                let target = texture.[TextureAspect.Color, level, 0]
                target.Upload(data, region.Min, region.Size)

                let result = PixVolume<uint32>(Col.Format.RGBA, region.Size)
                texture.Download(result, level = level, offset = region.Min)

                PixVolume.compare V3i.Zero data result

            finally
                runtime.DeleteTexture(texture)

        let texture3D (runtime : IRuntime) =
            let size = V3i(100, 75, 66)
            uploadAndDownloadTexture3D runtime size 1 0 None

        let texture3DSubwindow (runtime : IRuntime) =
            let size = V3i(100, 75, 66)
            let region = Some <| Box3i(11, 23, 17, 88, 67, 32)
            uploadAndDownloadTexture3D runtime size 1 0 region

        let texture3DLevel (runtime : IRuntime) =
            let size = V3i(100, 75, 66)
            uploadAndDownloadTexture3D runtime size 4 2 None

        let texture3DLevelSubwindow (runtime : IRuntime) =
            let size = V3i(100, 75, 66)
            let region = Some <| Box3i(13, 5, 7, 47, 30, 31)
            uploadAndDownloadTexture3D runtime size 4 1 region

        let texture3DNull (runtime : IRuntime) =
            let diffuseSampler =
                sampler3d {
                    texture uniform?DiffuseColorTexture
                    filter Filter.MinMagLinear
                    addressU WrapMode.Clamp
                    addressV WrapMode.Clamp
                }

            let diffuseTexture (v : Effects.Vertex) =
                fragment {
                    return diffuseSampler.Sample(V3d(v.tc.XY))
                }

            let shader =
                Sg.shader {
                    do! diffuseTexture
                }

            use randomTexture = runtime.CreateTexture3D(V3i(8, 8, 8), TextureFormat.Rgba8)
            randomTexture.Upload(PixVolume.random8ui <| V3i(8, 8, 8))

            runtime |> renderQuadWithNullTexture shader randomTexture

        let private uploadAndDownloadPixTexture3D (runtime : IRuntime) (size : V3i) (levels : int) (textureParams : TextureParams) =
            let expectedLevels =
                if textureParams.wantMipMaps then Fun.MipmapLevels(size) else 1

            let data = PixVolume.random16ui size

            let pixTexture = PixTexture3d(data, textureParams)
            let texture = runtime.PrepareTexture pixTexture

            try
                Expect.equal texture.MipMapLevels expectedLevels "unexpected level count"

                let target = texture.[TextureAspect.Color, 0, 0]
                let result = PixVolume<uint16>(Col.Format.RGBA, size)
                runtime.Download(target, result)

                PixVolume.compare V3i.Zero data result

            finally
                runtime.DeleteTexture(texture)

        let pixTexture3D (runtime : IRuntime) =
            let size = V3i(64)
            uploadAndDownloadPixTexture3D runtime size 1 TextureParams.empty

        let pixTexture3DSrgb (runtime : IRuntime) =
            let size = V3i(64)
            uploadAndDownloadPixTexture3D runtime size 1 TextureParams.srgb

        let pixTexture3DMipmapGeneration (runtime : IRuntime) =
            let size = V3i(67)
            let levels = Fun.MipmapLevels(size)
            uploadAndDownloadPixTexture3D runtime size levels TextureParams.srgb


        let private uploadAndDownloadTextureCube (runtime : IRuntime)
                                                 (size : int) (levels : int) (count : int)
                                                 (level : int) (slice : int)
                                                 (window : Box2i option) =
            let region =
                match window with
                | Some r -> r
                | None ->
                    let s = max 1 (size >>> level)
                    Box2i(0, 0, s, s)

            let regionOffset = V3i(region.Min, 0)
            let regionSize = V3i(region.Size, 1)
            let data = PixVolume.random32ui regionSize
            let fmt = TextureFormat.ofPixFormat data.PixFormat TextureParams.empty

            let texture =
                if count > 1 then
                    runtime.CreateTextureCubeArray(size, fmt, levels, count)
                else
                    runtime.CreateTextureCube(size, fmt, levels)


            try
                let target = texture.[TextureAspect.Color, level, slice]
                target.Upload(data, regionOffset, regionSize)

                let result = PixVolume<uint32>(Col.Format.RGBA, regionSize)
                target.Download(result, regionOffset, regionSize)

                PixVolume.compare V3i.Zero data result

            finally
                runtime.DeleteTexture(texture)

        let textureCube (runtime : IRuntime) =
            uploadAndDownloadTextureCube runtime 100 1 1 0 0 None

        let textureCubeSubwindow (runtime : IRuntime) =
            let region = Some <| Box2i(11, 45, 87, 58)
            uploadAndDownloadTextureCube runtime 100 1 1 0 0 region

        let textureCubeLevel (runtime : IRuntime) =
            uploadAndDownloadTextureCube runtime 100 4 1 2 0 None

        let textureCubeLevelSubwindow (runtime : IRuntime) =
            let region = Some <| Box2i(11, 45, 37, 47)
            uploadAndDownloadTextureCube runtime 100 4 1 1 0 region

        let textureCubeArray (runtime : IRuntime) =
            uploadAndDownloadTextureCube runtime 100 1 5 0 2 None

        let textureCubeArraySubwindow (runtime : IRuntime) =
            let region = Some <| Box2i(11, 45, 87, 58)
            uploadAndDownloadTextureCube runtime 100 1 5 0 2 region

        let textureCubeArrayLevel (runtime : IRuntime) =
            uploadAndDownloadTextureCube runtime 100 4 5 2 1 None

        let textureCubeArrayLevelSubwindow (runtime : IRuntime) =
            let region = Some <| Box2i(11, 45, 37, 47)
            uploadAndDownloadTextureCube runtime 100 4 5 1 3 region

        let private uploadAndDownloadPixTextureCube (runtime : IRuntime) (size : int) (levels : int) (textureParams : TextureParams) =
            let expectedLevels =
                if textureParams.wantMipMaps then Fun.MipmapLevels(size) else 1

            let data =
                Array.init 6 (fun _ ->
                    Array.init levels (fun level ->
                        let size = Fun.MipmapLevelSize(size, level)
                        PixImage.random16ui <| V2i size
                    )
                )

            let texture =
                let toMipMap arr =
                    PixImageMipMap(arr |> Array.map (fun pi -> pi :> PixImage))

                let pc = PixImageCube(data |> Array.map toMipMap)
                let pt = PixTextureCube(pc, textureParams)
                runtime.PrepareTexture(pt)

            try
                Expect.equal texture.MipMapLevels expectedLevels "unexpected level count"

                for slice = 0 to 5 do
                    for level = 0 to expectedLevels - 1 do
                        let result = runtime.Download(texture, level = level, slice = slice).AsPixImage<uint16>()

                        Expect.equal result.Size (Fun.MipmapLevelSize(V2i(size), level)) "image size mismatch"
                        if level < levels then
                            PixImage.compare V2i.Zero data.[slice].[level] result
                        else
                            let maxValue = result.Array |> unbox<uint16[]> |> Array.max
                            Expect.isGreaterThan maxValue 0us "image all black"

            finally
                runtime.DeleteTexture(texture)

        let pixTextureCube (runtime : IRuntime) =
            let size = 128
            uploadAndDownloadPixTextureCube runtime size 1 TextureParams.empty

        let pixTextureCubeSrgb (runtime : IRuntime) =
            let size = 128
            uploadAndDownloadPixTextureCube runtime size 1 TextureParams.srgb

        let pixTextureCubeMipmapped (mipmapInput : MipmapInput) (runtime : IRuntime) =
            let size = 128
            let levels = mipmapInput.GetLevels(size)
            uploadAndDownloadPixTextureCube runtime size levels TextureParams.mipmapped

        let textureCubeNull (runtime : IRuntime) =
            let diffuseSampler =
                samplerCube {
                    texture uniform?DiffuseColorTexture
                    filter Filter.MinMagLinear
                    addressU WrapMode.Clamp
                    addressV WrapMode.Clamp
                }

            let diffuseTexture (v : Effects.Vertex) =
                fragment {
                    return diffuseSampler.Sample(V3d.Zero)
                }

            let shader =
                Sg.shader {
                    do! diffuseTexture
                }

            runtime |> renderQuadWithNullTexture shader nullTexture

        let textureCubeNullArray (runtime : IRuntime) =
            let diffuseSampler =
                samplerCubeArray {
                    texture uniform?DiffuseColorTexture
                    filter Filter.MinMagLinear
                    addressU WrapMode.Clamp
                    addressV WrapMode.Clamp
                }

            let diffuseTexture (v : Effects.Vertex) =
                fragment {
                    return diffuseSampler.Sample(V3d.Zero, 32)
                }

            let shader =
                Sg.shader {
                    do! diffuseTexture
                }

            runtime |> renderQuadWithNullTexture shader nullTexture

    let tests (backend : Backend) =
        [
            "1D",                        Cases.texture1D
            "1D subwindow",              Cases.texture1DSubwindow
            "1D level",                  Cases.texture1DLevel
            "1D level subwindow",        Cases.texture1DLevelSubwindow

            "1D array",                  Cases.texture1DArray
            "1D array subwindow",        Cases.texture1DArraySubwindow
            "1D array level",            Cases.texture1DArrayLevel
            "1D array level subwindow",  Cases.texture1DArrayLevelSubwindow

            "1D NullTexture",            Cases.texture1DNull
            "1D NullTexture array",      Cases.texture1DNullArray

            "2D",                           Cases.texture2D
            "2D BGRA",                      Cases.texture2DBgra
            "2D subwindow",                 Cases.texture2DSubwindow
            "2D level",                     Cases.texture2DLevel
            "2D level subwindow",           Cases.texture2DLevelSubwindow
            "2D native",                    Cases.texture2DNative
            "2D native mipmap generation",  Cases.texture2DNativeMipmapGeneration
            "2D RGB vs BGR format",         Cases.texture2DRgbVsBgrFormat

            "2D array",                  Cases.texture2DArray
            "2D array subwindow",        Cases.texture2DArraySubwindow
            "2D array level",            Cases.texture2DArrayLevel
            "2D array level subwindow",  Cases.texture2DArrayLevelSubwindow

            "2D NullTexture",                       Cases.texture2DNull
            "2D NullTexture array",                 Cases.texture2DNullArray
            "2D NullTexture multisampled",          Cases.texture2DNullMultisampled
            "2D NullTexture multisampled array",    Cases.texture2DNullMultisampledArray
            "2D NullTexture int",                   Cases.texture2DNullInt
            "2D NullTexture shadow",                Cases.texture2DNullShadow

            "2D PixTexture",                                    Cases.pixTexture2D
            "2D PixTexture BGRA",                               Cases.pixTexture2DBgra
            "2D PixTexture sRGB",                               Cases.pixTexture2DSrgb
            "2D PixTexture mipmapped",                          Cases.pixTexture2DMipmapped MipmapInput.Full
            "2D PixTexture mipmap generation",                  Cases.pixTexture2DMipmapped MipmapInput.None
            "2D PixTexture mipmap partial generation",          Cases.pixTexture2DMipmapped MipmapInput.Partial

            if backend <> Backend.GL then   // not supported
                "2D PixTexture mipmap integer generation",          Cases.pixTexture2DMipmappedInteger MipmapInput.None
                "2D PixTexture mipmap integer partial generation",  Cases.pixTexture2DMipmappedInteger MipmapInput.Partial

            "2D PixTexture as PixVolume",                       Cases.pixTexture2DPixVolume
            "2D PixTexture compressed",                         Cases.pixTexture2DCompressed false
            "2D PixTexture compressed mipmap generation",       Cases.pixTexture2DCompressed true

            "2D StreamTexture compressed",                   Cases.streamTextureCompressed false
            "2D StreamTexture compressed mipmap generation", Cases.streamTextureCompressed true

            if backend <> Backend.Vulkan then // not supported
                "2D multisampled",                        Cases.texture2DMultisampled
                "2D multisampled subwindow",              Cases.texture2DMultisampledSubwindow
                "2D multisampled array",                  Cases.texture2DMultisampledArray
                "2D multisampled array subwindow",        Cases.texture2DMultisampledArraySubwindow

            "2D compressed DDS BC1",                    Cases.texture2DCompressedDDSBC1
            "2D compressed DDS BC2",                    Cases.texture2DCompressedDDSBC2
            "2D compressed DDS BC3",                    Cases.texture2DCompressedDDSBC3
            "2D compressed DDS BC4u",                   Cases.texture2DCompressedDDSBC4u
            "2D compressed DDS BC5u",                   Cases.texture2DCompressedDDSBC5u
            "2D compressed subwindow",                    Cases.texture2DCompressedSubwindow

            // Uploading BC6/7 is possible on both backends, but there is no
            // easy way to flip these, and unfortunately we want to flip all our textures on upload -_-
                //"2D compressed DDS BC6h",     Cases.texture2DCompressedDDSBC6h
                //"2D compressed DDS BC7",      Cases.texture2DCompressedDDSBC7

            // Vulkan does not support generation of mipmaps for already compressed textures
            if backend <> Backend.Vulkan then
                "2D compressed DDS BC1 mipmap generation",  Cases.texture2DCompressedDDSBC1MipmapGeneration
                "2D compressed DDS BC2 mipmap generation",  Cases.texture2DCompressedDDSBC2MipmapGeneration
                "2D compressed DDS BC3 mipmap generation",  Cases.texture2DCompressedDDSBC3MipmapGeneration
                "2D compressed DDS BC4u mipmap generation", Cases.texture2DCompressedDDSBC4uMipmapGeneration
                "2D compressed DDS BC5u mipmap generation", Cases.texture2DCompressedDDSBC5uMipmapGeneration

            if backend <> Backend.Vulkan then // not supported (only really used for CEF?)
                "2D StreamingTexture",      Cases.texture2DStreaming

            "3D",                           Cases.texture3D
            "3D subwindow",                 Cases.texture3DSubwindow
            "3D level",                     Cases.texture3DLevel
            "3D level subwindow",           Cases.texture3DLevelSubwindow

            "3D NullTexture",               Cases.texture3DNull

            "3D PixTexture",                    Cases.pixTexture3D
            "3D PixTexture sRGB",               Cases.pixTexture3DSrgb
            "3D PixTexture mipmap generation",  Cases.pixTexture3DMipmapGeneration

            "Cube",                         Cases.textureCube
            "Cube subwindow",               Cases.textureCubeSubwindow
            "Cube level",                   Cases.textureCubeLevel
            "Cube level subwindow",         Cases.textureCubeLevelSubwindow

            "Cube array",                   Cases.textureCubeArray
            "Cube array subwindow",         Cases.textureCubeArraySubwindow
            "Cube array level",             Cases.textureCubeArrayLevel
            "Cube array level subwindow",   Cases.textureCubeArrayLevelSubwindow

            "Cube NullTexture",             Cases.textureCubeNull
            "Cube NullTexture array",       Cases.textureCubeNullArray

            "Cube PixTexture",                           Cases.pixTextureCube
            "Cube PixTexture sRGB",                      Cases.pixTextureCubeSrgb
            "Cube PixTexture mipmapped",                 Cases.pixTextureCubeMipmapped MipmapInput.Full
            "Cube PixTexture mipmap generation",         Cases.pixTextureCubeMipmapped MipmapInput.None
            "Cube PixTexture mipmap partial generation", Cases.pixTextureCubeMipmapped MipmapInput.Partial
        ]
        |> prepareCases backend "Upload"
