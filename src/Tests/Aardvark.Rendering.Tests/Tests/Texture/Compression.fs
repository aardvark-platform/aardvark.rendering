namespace Aardvark.Rendering.Tests.Texture

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.NativeInterop
open Expecto

#nowarn "9"

module TextureCompression =

    open BenchmarkDotNet.Attributes
    open OpenTK.Graphics.OpenGL4
    open Aardvark.Application

    type OnTheFlyCompression() =
        let mutable app = Unchecked.defaultof<TestApplication>
        let mutable image = Unchecked.defaultof<PixImage<uint8>>

        let finish() =
            let runtime = app.Runtime :?> GL.Runtime
            use __ = runtime.Context.ResourceLock
            GL.Finish()

        [<DefaultValue; Params(128, 512, 1024, 2048, 4096)>]
        val mutable Size : int

        [<GlobalSetup>]
        member x.Setup() =
            app <- TestApplication.create' DebugLevel.None Backend.GL

            let rng = RandomSystem(1)

            let size = V2i x.Size
            image <- PixImage<uint8>(Col.Format.RGBA, size)

            for c in image.ChannelArray do
                c.SetByIndex(ignore >> rng.UniformUInt >> uint8) |> ignore

        [<GlobalCleanup>]
        member x.Cleanup() =
            app.Dispose()

        [<Benchmark>]
        member x.Upload() =
            app.Runtime.PrepareTexture(PixTexture2d(image, TextureParams.empty)) |> ignore
            finish()

        [<Benchmark>]
        member x.UploadCompressed() =
            GL.RuntimeConfig.PreferHostSideTextureCompression <- false
            app.Runtime.PrepareTexture(PixTexture2d(image, TextureParams.compressed)) |> ignore
            finish()

        [<Benchmark>]
        member x.UploadHostCompressed() =
            GL.RuntimeConfig.PreferHostSideTextureCompression <- true
            app.Runtime.PrepareTexture(PixTexture2d(image, TextureParams.compressed)) |> ignore
            finish()


    module Cases =

        let private testCompressionUnsigned (mode : CompressionMode) (path : string) (targetPsnr : float) (targetRsme : float) =
            let input = EmbeddedResource.loadPixImage<uint8> path
            let reference = input.Copy()

            match mode with
            | CompressionMode.BC1 ->
                reference.GetMatrix<C4b>().Apply(fun color ->
                    if color.A < 127uy then
                        C4b.Zero
                    else
                        C4b(color.RGB, 255uy)
                ) |> ignore

            | CompressionMode.BC4 _ ->
                reference.GetChannel(Col.Channel.Green).Set(0uy) |> ignore
                reference.GetChannel(Col.Channel.Blue).Set(0uy) |> ignore

            | CompressionMode.BC5 _ ->
                reference.GetChannel(Col.Channel.Blue).Set(0uy) |> ignore

            | _ ->
                ()

            let format =
                match mode with
                | CompressionMode.BC4 _ | CompressionMode.BC5 _ -> PixFormat.ByteRGB
                | _ -> input.PixFormat

            let size = input.Size
            let sizeInBytes = mode |> CompressionMode.sizeInBytes size.XYI

            let compressed = NativePtr.alloc<uint8> (int sizeInBytes)
            let output = PixImage.Create(format, int64 size.X, int64 size.Y).AsPixImage<uint8>()

            try
                let pCompressed = NativePtr.toNativeInt compressed

                PixImage.pin input (fun input ->
                    BlockCompression.encode mode input.Address input.Info pCompressed
                )

                PixImage.pin output (fun output ->
                    BlockCompression.decode mode V2i.Zero size pCompressed output.Address output.Info
                )

                let psnr = PixImage.peakSignalToNoiseRatio V2i.Zero reference output
                let rmse = PixImage.rootMeanSquaredError V2i.Zero reference output
                Expect.isGreaterThan psnr targetPsnr "Bad peak-signal-to-noise ratio"
                Expect.isLessThan rmse targetRsme "Bad root-mean-square error"

            finally
                NativePtr.free compressed

        let private testCompressionSigned (mode : CompressionMode) (path : string) (targetPsnr : float) (targetRsme : float) =
            let input = EmbeddedResource.loadPixImage<uint8> path
            let reference = input.Copy()

            match mode with
            | CompressionMode.BC1 ->
                reference.GetMatrix<C4b>().Apply(fun color ->
                    if color.A < 127uy then
                        C4b.Zero
                    else
                        C4b(color.RGB, 255uy)
                ) |> ignore

            | CompressionMode.BC4 _ ->
                reference.GetChannel(Col.Channel.Green).Set(0uy) |> ignore
                reference.GetChannel(Col.Channel.Blue).Set(0uy) |> ignore

            | CompressionMode.BC5 _ ->
                reference.GetChannel(Col.Channel.Blue).Set(0uy) |> ignore

            | _ ->
                ()

            let sinput =
                PixImage<int8>(
                    Col.Format.RGB,
                    input.Volume.Map(fun x -> int8 (min ((int16 x) - 127s) 127s))
                )

            let size = input.Size
            let sizeInBytes = mode |> CompressionMode.sizeInBytes size.XYI

            let compressed = NativePtr.alloc<uint8> (int sizeInBytes)
            let soutput = PixImage.Create(PixFormat.SByteRGB, int64 size.X, int64 size.Y).AsPixImage<int8>()

            try
                let pCompressed = NativePtr.toNativeInt compressed

                PixImage.pin sinput (fun input ->
                    BlockCompression.encode mode input.Address input.Info pCompressed
                )

                PixImage.pin soutput (fun output ->
                    BlockCompression.decode mode V2i.Zero size pCompressed output.Address output.Info
                )

                let output =
                    PixImage<uint8>(
                        Col.Format.RGB,
                        soutput.Volume.Map(fun x -> uint8 ((int16 x) + 127s))
                    )

                match mode with
                | CompressionMode.BC4 _ ->
                    output.GetChannel(Col.Channel.Green).Set(0uy) |> ignore
                    output.GetChannel(Col.Channel.Blue).Set(0uy) |> ignore

                | CompressionMode.BC5 _ ->
                    output.GetChannel(Col.Channel.Blue).Set(0uy) |> ignore

                | _ ->
                    ()

                let psnr = PixImage.peakSignalToNoiseRatio V2i.Zero reference output
                let rmse = PixImage.rootMeanSquaredError V2i.Zero reference output
                Expect.isGreaterThan psnr targetPsnr "Bad peak-signal-to-noise ratio"
                Expect.isLessThan rmse targetRsme "Bad root-mean-square error"

            finally
                NativePtr.free compressed

        let private testMirrorCopy (mode : CompressionMode) (path : string) (region : Box2i) =
            let input = path |> EmbeddedResource.loadPixImage<uint8> |> PixImage.cropped region

            let format =
                match mode with
                | CompressionMode.BC4 _ | CompressionMode.BC5 _ -> Col.Format.RGB
                | _ -> input.Format

            let input = PixImage<uint8>(format, input.Volume)

            let size = input.Size
            let blockSize = mode |> CompressionMode.blockSize
            let sizeInBytes = mode |> CompressionMode.sizeInBytes size.XYI

            let pBuffer1 = NativePtr.alloc<uint8> (int sizeInBytes)
            let output1 = PixImage.Create(input.PixFormat, int64 size.X, int64 size.Y).AsPixImage<uint8>()

            let pBuffer2 = NativePtr.alloc<uint8> (int sizeInBytes)
            let output2 = PixImage.Create(input.PixFormat, int64 size.X, int64 size.Y).AsPixImage<uint8>()

            try
                let compressed = NativePtr.toNativeInt pBuffer1
                let mirrored = NativePtr.toNativeInt pBuffer2

                PixImage.pin input (fun input ->
                    BlockCompression.encode mode input.Address input.Info compressed
                )

                BlockCompression.mirrorCopy mode size compressed mirrored

                PixImage.pin output1 (fun output ->
                    BlockCompression.decode mode V2i.Zero size compressed output.Address output.Info
                )

                PixImage.pin output2 (fun output ->
                    BlockCompression.decode mode V2i.Zero size mirrored output.Address output.Info
                )

                let output2 = output2.Transformed(ImageTrafo.MirrorY).AsPixImage<uint8>()

                if size.Y < blockSize || size.Y % blockSize = 0 then
                    // aligned or single block row -> no artifacts
                    PixImage.compare V2i.Zero output1 output2

                else
                    // if unaligned we lose some (at most 3) pixel rows, rest is equal (shifted though)
                    let rem = blockSize - (size.Y % blockSize)

                    let o1 =
                        let region = Box2i.FromMinAndSize(0, rem, size.X, size.Y - rem)
                        output1 |> PixImage.cropped region

                    let o2 =
                        let region = Box2i.FromMinAndSize(0, 0, size.X, size.Y - rem)
                        output2 |> PixImage.cropped region

                    PixImage.compare V2i.Zero o1 o2

                    // the new pixel rows are copied from the last row (similar to texture clamp wrap mode)
                    let lastRow =
                        let region = Box2i.FromMinAndSize(0, size.Y - 1, size.X, 1)
                        output1 |> PixImage.cropped region

                    for i = 0 to rem - 1 do
                        let region = Box2i.FromMinAndSize(0, size.Y - 1 - i, size.X, 1)
                        let row = output2 |> PixImage.cropped region
                        PixImage.compare V2i.Zero lastRow row

            finally
                NativePtr.free pBuffer1
                NativePtr.free pBuffer2

        let encodeBC1() =
            testCompressionUnsigned CompressionMode.BC1 "data/spiral.png" 6.8 4.2

        let encodeBC1a() =
            testCompressionUnsigned CompressionMode.BC1 "data/spiral_alpha.png" 13.5 3.32

        let mirrorCopyBC1 (height : int) () =
            let region = Box2i.FromMinAndSize(0, 0, 134, height)
            testMirrorCopy CompressionMode.BC1 "data/spiral.png" region


        let encodeBC2() =
            testCompressionUnsigned CompressionMode.BC2 "data/spiral_alpha.png" 6.4 4.28

        let mirrorCopyBC2 (height : int) () =
            let region = Box2i.FromMinAndSize(75, 59, 192, height)
            testMirrorCopy CompressionMode.BC2 "data/spiral_alpha.png" region


        let encodeBC3() =
            testCompressionUnsigned CompressionMode.BC3 "data/spiral_alpha.png" 6.8 4.18

        let mirrorCopyBC3 (height : int) () =
            let region = Box2i.FromMinAndSize(75, 59, 192, height)
            testMirrorCopy CompressionMode.BC3 "data/spiral_alpha.png" region


        let encodeBC4u() =
            testCompressionUnsigned (CompressionMode.BC4 false) "data/spiral.png" 51.9 0.65

        let encodeBC4s() =
            testCompressionSigned (CompressionMode.BC4 true) "data/spiral.png" 51.0 0.72

        let mirrorCopyBC4 (height : int) () =
            let region = Box2i.FromMinAndSize(0, 0, 134, height)
            testMirrorCopy (CompressionMode.BC4 false) "data/spiral.png" region


        let encodeBC5u() =
            testCompressionUnsigned (CompressionMode.BC5 false) "data/spiral.png" 48.3 0.98

        let encodeBC5s() =
            testCompressionSigned (CompressionMode.BC5 true) "data/spiral.png" 46.5 1.06

        let mirrorCopyBC5 (height : int) () =
            let region = Box2i.FromMinAndSize(0, 0, 134, height)
            testMirrorCopy (CompressionMode.BC5 false) "data/spiral.png" region

    let tests =
        [
            "BC1 encode",           Cases.encodeBC1
            "BC1a encode",          Cases.encodeBC1a
            "BC1 mirror copy 1px",  Cases.mirrorCopyBC1 1
            "BC1 mirror copy 2px",  Cases.mirrorCopyBC1 2
            "BC1 mirror copy 3px",  Cases.mirrorCopyBC1 3
            "BC1 mirror copy 4px",  Cases.mirrorCopyBC1 4
            "BC1 mirror copy 20px", Cases.mirrorCopyBC1 20
            "BC1 mirror copy 21px", Cases.mirrorCopyBC1 21
            "BC1 mirror copy 22px", Cases.mirrorCopyBC1 22
            "BC1 mirror copy 23px", Cases.mirrorCopyBC1 23

            "BC2 encode",           Cases.encodeBC2
            "BC2 mirror copy 1px",  Cases.mirrorCopyBC2 1
            "BC2 mirror copy 2px",  Cases.mirrorCopyBC2 2
            "BC2 mirror copy 3px",  Cases.mirrorCopyBC2 3
            "BC2 mirror copy 4px",  Cases.mirrorCopyBC2 4
            "BC2 mirror copy 20px", Cases.mirrorCopyBC2 20
            "BC2 mirror copy 21px", Cases.mirrorCopyBC2 21
            "BC2 mirror copy 22px", Cases.mirrorCopyBC2 22
            "BC2 mirror copy 23px", Cases.mirrorCopyBC2 23

            "BC3 encode",           Cases.encodeBC3
            "BC3 mirror copy 1px",  Cases.mirrorCopyBC3 1
            "BC3 mirror copy 2px",  Cases.mirrorCopyBC3 2
            "BC3 mirror copy 3px",  Cases.mirrorCopyBC3 3
            "BC3 mirror copy 4px",  Cases.mirrorCopyBC3 4
            "BC3 mirror copy 20px", Cases.mirrorCopyBC3 20
            "BC3 mirror copy 21px", Cases.mirrorCopyBC3 21
            "BC3 mirror copy 22px", Cases.mirrorCopyBC3 22
            "BC3 mirror copy 23px", Cases.mirrorCopyBC3 23

            "BC4u encode",          Cases.encodeBC4u
            "BC4s encode",          Cases.encodeBC4s
            "BC4 mirror copy 1px",  Cases.mirrorCopyBC4 1
            "BC4 mirror copy 2px",  Cases.mirrorCopyBC4 2
            "BC4 mirror copy 3px",  Cases.mirrorCopyBC4 3
            "BC4 mirror copy 4px",  Cases.mirrorCopyBC4 4
            "BC4 mirror copy 20px", Cases.mirrorCopyBC4 20
            "BC4 mirror copy 21px", Cases.mirrorCopyBC4 21
            "BC4 mirror copy 22px", Cases.mirrorCopyBC4 22
            "BC4 mirror copy 23px", Cases.mirrorCopyBC4 23

            "BC5u encode",          Cases.encodeBC5u
            "BC5s encode",          Cases.encodeBC5s
            "BC5 mirror copy 1px",  Cases.mirrorCopyBC5 1
            "BC5 mirror copy 2px",  Cases.mirrorCopyBC5 2
            "BC5 mirror copy 3px",  Cases.mirrorCopyBC5 3
            "BC5 mirror copy 4px",  Cases.mirrorCopyBC5 4
            "BC5 mirror copy 20px", Cases.mirrorCopyBC5 20
            "BC5 mirror copy 21px", Cases.mirrorCopyBC5 21
            "BC5 mirror copy 22px", Cases.mirrorCopyBC5 22
            "BC5 mirror copy 23px", Cases.mirrorCopyBC5 23
        ]
        |> prepareCasesBackendAgnostic "Compression"