namespace Aardvark.Rendering.Tests.Compute

open Aardvark.Base
open Aardvark.GPGPU
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open System.IO
open Expecto

module ComputeJpeg =

    module Cases =

        let compress (runtime : IRuntime) =
            let input = EmbeddedResource.loadPixImage<uint8> "data/rgb.png"
            use inputTexture = runtime.PrepareTexture(PixTexture2d input)

            use jpeg = new JpegCompressor(runtime)
            use jpegInst = jpeg.NewInstance(input.Size, Quantization.photoshop90)

            let compressed = jpegInst.Compress(inputTexture.[TextureAspect.Color, 0, 0])
            use compressedStream = new MemoryStream(compressed)

            let output = (PixImage.Load compressedStream).AsPixImage<uint8>()

            let psnr = PixImage.peakSignalToNoiseRatio input output
            let rmse = PixImage.rootMeanSquaredError input output

            Expect.isGreaterThan psnr 50.0 "Bad peak-signal-to-noise ratio"
            Expect.isLessThan rmse 1.0 "Bad root-mean-square error"

    let tests (backend : Backend) =
        [
            "Compress", Cases.compress
        ]
        |> prepareCases backend "Jpeg"