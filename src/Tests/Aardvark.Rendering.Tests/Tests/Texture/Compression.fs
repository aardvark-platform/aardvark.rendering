namespace Aardvark.Rendering.Tests.Texture

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.NativeInterop
open Expecto

#nowarn "9"

module TextureCompression =

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

        let BC1() =
            testCompressionUnsigned CompressionMode.BC1 "data/spiral.png" 6.0 5.0

        let BC1a() =
            testCompressionUnsigned CompressionMode.BC1 "data/spiral_alpha.png" 6.0 5.0

        let BC2() =
            testCompressionUnsigned CompressionMode.BC2 "data/spiral_alpha.png" 6.0 5.0

        let BC3() =
            testCompressionUnsigned CompressionMode.BC3 "data/spiral_alpha.png" 6.0 5.0

        let BC4u() =
            testCompressionUnsigned (CompressionMode.BC4 false) "data/spiral.png" 60.0 1.0

        let BC4s() =
            testCompressionSigned (CompressionMode.BC4 true) "data/spiral.png" 57.0 1.0

        let BC5u() =
            testCompressionUnsigned (CompressionMode.BC5 false) "data/spiral.png" 48.0 1.0

        let BC5s() =
            testCompressionSigned (CompressionMode.BC5 true) "data/spiral.png" 46.0 1.1

    let tests =
        [
            "BC1 encode",   Cases.BC1
            "BC1a encode",  Cases.BC1a

            "BC2 encode",   Cases.BC2

            "BC3 encode",   Cases.BC3

            "BC4u encode",  Cases.BC4u
            "BC4s encode",  Cases.BC4s

            "BC5u encode",  Cases.BC5u
            "BC5s encode",  Cases.BC5s
        ]
        |> prepareCasesBackendAgnostic "Compression"