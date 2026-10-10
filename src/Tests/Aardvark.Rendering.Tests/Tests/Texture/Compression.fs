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
            app <- TestApplication.create' DebugLevel.None (TestBackend.GL Framework.OpenTK)

            let size = V2i x.Size
            image <- PixImage<uint8>(Col.Format.RGBA, size)

            for c in image.ChannelArray do
                c.SetByIndex(ignore >> Rnd.uint8) |> ignore

        [<GlobalCleanup>]
        member x.Cleanup() =
            app.Complete()
            app.Dispose()

        [<Benchmark>]
        member x.Upload() =
            app.Runtime.PrepareTexture(PixTexture2d(image, TextureParams.None)) |> ignore
            finish()

        [<Benchmark>]
        member x.UploadCompressed() =
            GL.RuntimeConfig.PreferHostSideTextureCompression <- false
            app.Runtime.PrepareTexture(PixTexture2d(image, TextureParams.Compress)) |> ignore
            finish()

        [<Benchmark>]
        member x.UploadHostCompressed() =
            GL.RuntimeConfig.PreferHostSideTextureCompression <- true
            app.Runtime.PrepareTexture(PixTexture2d(image, TextureParams.Compress)) |> ignore
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

                let psnr = PixImage.peakSignalToNoiseRatio reference output
                let rmse = PixImage.rootMeanSquaredError reference output
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

                let psnr = PixImage.peakSignalToNoiseRatio reference output
                let rmse = PixImage.rootMeanSquaredError reference output
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

        let modes =
            [ CompressionMode.BC1, 8n
              CompressionMode.BC2, 16n
              CompressionMode.BC3, 16n
              CompressionMode.BC4 false, 8n
              CompressionMode.BC4 true, 8n
              CompressionMode.BC5 false, 16n
              CompressionMode.BC5 true, 16n
              CompressionMode.BC6h, 16n
              CompressionMode.BC7, 16n ]

        // Independent, widened reference calculation: BC blocks have no depth extent.
        let private blocksXY n = max 1L ((int64 n + 3L) / 4L) |> int
        let private expectedBlocks (size : V3i) = V3i(blocksXY size.X, blocksXY size.Y, max 1 size.Z)
        let private expectedBytes (blocks : V3i) bytesPerBlock =
            nativeint blocks.X * nativeint blocks.Y * nativeint blocks.Z * bytesPerBlock

        let blocks4x4x1 mode bytesPerBlock () =
            Expect.equal (CompressionMode.blockSize mode) 4 "Scalar XY block size"
            Expect.equal (CompressionMode.bytesPerBlock mode) bytesPerBlock "Bytes per block"
            for width in [1; 2; 3; 4; 5; 7; 8; 9] do
                for height in [1; 3; 4; 5; 8; 9] do
                    for depth in [1; 2; 3; 4; 5; 7; 8; 9] do
                        let size = V3i(width, height, depth)
                        let blocks = expectedBlocks size
                        Expect.equal (CompressionMode.numberOfBlocks size mode) blocks $"Block count for {size}"
                        Expect.equal (CompressionMode.sizeInBytes size mode) (expectedBytes blocks bytesPerBlock) $"Byte count for {size}"

        let compatibility mode bytesPerBlock () =
            for width in [1; 3; 4; 5; 16; 127; 256] do
                for height in [1; 3; 4; 5; 16; 127; 256] do
                    let size = V3i(width, height, 1)
                    let previous = max 1 ((size + 3) / 4)
                    Expect.equal (CompressionMode.numberOfBlocks size mode) previous $"Block count for {size}"
                    Expect.equal (CompressionMode.sizeInBytes size mode) (expectedBytes previous bytesPerBlock) $"Byte count for {size}"

        let representableLargeByteCounts mode bytesPerBlock () =
            if System.IntPtr.Size = 8 then
                // XY, XYZ, and byte products exceed Int32 without allocating payloads.
                for size in [V3i(262144, 262144, 1); V3i(65536, 65536, 16); V3i(65536, 65536, 1);
                             V3i(System.Int32.MaxValue, 5, 1); V3i(5, System.Int32.MaxValue, 1);
                             V3i(1, 1, System.Int32.MaxValue)] do
                    let blocks = expectedBlocks size
                    Expect.equal (CompressionMode.numberOfBlocks size mode) blocks $"Large block count for {size}"
                    Expect.equal (CompressionMode.sizeInBytes size mode) (expectedBytes blocks bytesPerBlock) $"Large byte count for {size}"

        let uncompressedLayoutUnchanged () =
            Expect.equal (CompressionMode.blockSize CompressionMode.None) 1 "Uncompressed block size"
            Expect.equal (CompressionMode.bytesPerBlock CompressionMode.None) 0n "Uncompressed bytes per block"
            for size in [V3i.Zero; V3i(-3, 0, -1); V3i.One; V3i(5, 7, 9); V3i(System.Int32.MaxValue)] do
                Expect.equal (CompressionMode.numberOfBlocks size CompressionMode.None) (max 1 size) "Uncompressed block count"
                Expect.equal (CompressionMode.sizeInBytes size CompressionMode.None) 0n "Uncompressed byte count"

        let minimumBlockCountUnchanged () =
            for mode, bytesPerBlock in modes do
                for size in [V3i.Zero; V3i(-3, 0, -1)] do
                    Expect.equal (CompressionMode.numberOfBlocks size mode) V3i.One "At least one block per axis"
                    Expect.equal (CompressionMode.sizeInBytes size mode) bytesPerBlock "At least one block"

    module private EndpointEncoding =

        let private modes = [CompressionMode.BC1; CompressionMode.BC2; CompressionMode.BC3]

        // Exactly representable RGB565 pairs with equal channel sums: the all-ones PCA seed is orthogonal to their variation.
        let private pairs = [|
            "red/green",    C3b(255uy, 0uy, 0uy),   C3b(0uy, 255uy, 0uy),   0xF800us, 0x07E0us
            "green/blue",   C3b(0uy, 255uy, 0uy),   C3b(0uy, 0uy, 255uy),   0x07E0us, 0x001Fus
            "blue/red",     C3b(0uy, 0uy, 255uy),   C3b(255uy, 0uy, 0uy),   0x001Fus, 0xF800us
            "cyan/magenta", C3b(0uy, 255uy, 255uy), C3b(255uy, 0uy, 255uy), 0x07FFus, 0xF81Fus
            // The largest covariance diagonal is Z, rather than the X/Y ties above.
            "blue/yellow-green", C3b(0uy, 0uy, 255uy), C3b(24uy, 231uy, 0uy), 0x001Fus, 0x1F20us
        |]

        let private check context mode (size : V2i) pixel word expected =
            let context = $"{context}, {mode}, size={size}"
            let input = PixImage<byte>(Col.Format.RGBA, size)
            let mutable inputPixels = input.GetMatrix<C4b>()
            for y in 0 .. size.Y - 1 do
                for x in 0 .. size.X - 1 do
                    inputPixels.[x, y] <- pixel x y
            let original = Array.copy input.Data
            let output = PixImage<byte>(Col.Format.RGBA, size)
            let blocksX, blocksY = (size.X + 3) / 4, (size.Y + 3) / 4
            let blockBytes, colorOffset = if mode = CompressionMode.BC1 then 8, 0 else 16, 8
            let payloadSize = blocksX * blocksY * blockBytes
            let compressed = Array.create (payloadSize + 32) 0xCDuy
            compressed |> NativePtr.pinArr (fun data ->
                try
                    PixImage.pin input (fun src -> BlockCompression.encode mode src.Address src.Info (data.Address + 16n))
                    PixImage.pin output (fun dst -> BlockCompression.decode mode V2i.Zero size (data.Address + 16n) dst.Address dst.Info)
                with error -> failtestf "%s: %O" context error
            )
            Expect.equal input.Data original $"{context}: encoding modified source pixels"
            for i in 0 .. 15 do
                Expect.equal compressed.[i] 0xCDuy $"{context}: prefix guard {i}"
                Expect.equal compressed.[16 + payloadSize + i] 0xCDuy $"{context}: suffix guard {i}"

            // Check endpoint words independently of decoding: two identical erroneous endpoints must not pass.
            for by in 0 .. blocksY - 1 do
                for bx in 0 .. blocksX - 1 do
                    let mutable lo, hi, transparent = System.UInt16.MaxValue, 0us, false
                    for y in by * 4 .. min (size.Y - 1) (by * 4 + 3) do
                        for x in bx * 4 .. min (size.X - 1) (bx * 4 + 3) do
                            lo <- min lo (word x y)
                            hi <- max hi (word x y)
                            transparent <- transparent || (pixel x y).A < 127uy
                    let expectedEndpoints = if mode = CompressionMode.BC1 && transparent then lo, hi else hi, lo
                    let offset = 16 + (by * blocksX + bx) * blockBytes + colorOffset
                    let readWord offset = uint16 compressed.[offset] ||| (uint16 compressed.[offset + 1] <<< 8)
                    Expect.equal (readWord offset, readWord (offset + 2)) expectedEndpoints
                        $"{context}, block=({bx},{by}): RGB565 endpoints"
            let decoded = output.GetMatrix<C4b>()
            for y in 0 .. size.Y - 1 do
                for x in 0 .. size.X - 1 do
                    Expect.equal decoded.[x, y] (expected x y) $"{context}, pixel=({x},{y})"

        let private checkPair context mode size pairAt second alpha =
            let pixel x y =
                let _, a, b, _, _ = pairAt x y
                let color : C3b = if second x y then b else a
                C4b(color, alpha x y)
            let word x y =
                let _, _, _, a, b = pairAt x y
                if second x y then b else a
            let expected x y =
                let color = pixel x y
                if mode <> CompressionMode.BC1 then color
                elif color.A < 127uy then C4b.Zero
                else C4b(color.RGB, 255uy)
            check context mode size pixel word expected

        let chromaticBlocks() =
            for mode in modes do
                for (name, _, _, _, _) as pair in pairs do
                    for pattern in ["checkerboard"; "columns"; "rows"] do
                        for reverse in [false; true] do
                            let second x y =
                                let value =
                                    match pattern with
                                    | "columns" -> x >= 2
                                    | "rows" -> y >= 2
                                    | _ -> (x + y) % 2 <> 0
                                value <> reverse
                            checkPair $"{name}, {pattern}, reverse={reverse}" mode (V2i(4))
                                (fun _ _ -> pair) second (fun _ _ -> 255uy)

        let partialAndMultipleBlocks() =
            for mode in modes do
                for size in [V2i(3, 2); V2i(2, 3); V2i(4, 1); V2i(1, 4); V2i(8, 8); V2i(7, 6); V2i(9, 5)] do
                    for firstPair in 0 .. pairs.Length - 1 do
                        let pairAt x y = pairs.[(firstPair + x / 4 + (y / 4) * ((size.X + 3) / 4)) % pairs.Length]
                        checkPair $"mixed blocks, first pair={firstPair}" mode size pairAt
                            (fun x y -> (x + y) % 2 <> 0) (fun _ _ -> 255uy)

        let solidAndGrayscale() =
            for mode in modes do
                for size in [V2i(4); V2i(3, 2); V2i(7, 5)] do
                    for color, word, reconstructed in [
                        C3b(0uy), 0x0000us, C3b(0uy)
                        C3b(255uy), 0xFFFFus, C3b(255uy)
                        C3b(255uy, 0uy, 0uy), 0xF800us, C3b(255uy, 0uy, 0uy)
                        C3b(0uy, 255uy, 0uy), 0x07E0us, C3b(0uy, 255uy, 0uy)
                        C3b(0uy, 0uy, 255uy), 0x001Fus, C3b(0uy, 0uy, 255uy)
                        C3b(128uy), 0x8410us, C3b(132uy, 130uy, 132uy)
                    ] do
                        check $"solid {color}" mode size (fun _ _ -> C4b color) (fun _ _ -> word) (fun _ _ -> C4b reconstructed)
                for size in [V2i(4); V2i(8, 4)] do
                    let pixel x _ = C4b(C3b(byte ((x % 4) * 85)))
                    let words = [| 0x0000us; 0x52AAus; 0xAD55us; 0xFFFFus |]
                    let word x _ = words.[x % 4]
                    check "four grayscale levels" mode size pixel word pixel

        let tinyBlocks() =
            for mode in modes do
                for (name, _, _, _, _) as pair in pairs do
                    for size in [V2i(1); V2i(1, 2); V2i(2, 1)] do
                        for reverse in [false; true] do
                            checkPair $"{name}, tiny block, reverse={reverse}" mode size (fun _ _ -> pair)
                                (fun x y -> ((x + y) % 2 <> 0) <> reverse) (fun _ _ -> 255uy)

        let alpha() =
            for mode in modes do
                let alphas =
                    match mode with
                    | CompressionMode.BC1 -> [| 0uy; 126uy; 127uy; 255uy |]
                    | CompressionMode.BC2 -> Array.init 16 (fun i -> byte (17 * i))
                    | _ -> [| 255uy; 0uy; 218uy; 182uy; 145uy; 109uy; 72uy; 36uy |]
                for (name, _, _, _, _) as pair in pairs do
                    for values in [alphas; [| 0uy |]; [| 255uy |]] do
                        checkPair (sprintf "%s, alpha=%A" name values) mode (V2i(4)) (fun _ _ -> pair)
                            (fun x y -> (x + y) % 2 <> 0) (fun x y -> values.[(y * 4 + x) % values.Length])

    module private Handcrafted =

        // Literal RGB565 endpoints, four-color palettes and BC1 midpoints; no encoder or production interpolation oracle.
        let private palettes = [|
            "ascending black/white", 0x0000us, 0xFFFFus,
                [| C3b(0uy); C3b(255uy); C3b(85uy); C3b(170uy) |], C3b(128uy)
            "equal red", 0xF800us, 0xF800us,
                Array.create 4 (C3b(255uy, 0uy, 0uy)), C3b(255uy, 0uy, 0uy)
            "descending white/black", 0xFFFFus, 0x0000us,
                [| C3b(255uy); C3b(0uy); C3b(170uy); C3b(85uy) |], C3b(128uy)
            "ascending rounded components", 0x0821us, 0x1062us,
                [| C3b(8uy, 4uy, 8uy); C3b(16uy, 12uy, 16uy); C3b(11uy, 7uy, 11uy); C3b(13uy, 9uy, 13uy) |], C3b(12uy, 8uy, 12uy)
            "descending rounded components", 0x1062us, 0x0821us,
                [| C3b(16uy, 12uy, 16uy); C3b(8uy, 4uy, 8uy); C3b(13uy, 9uy, 13uy); C3b(11uy, 7uy, 11uy) |], C3b(12uy, 8uy, 12uy)
            "ascending distinct RGB", 0x17E5us, 0xD1B7us,
                [| C3b(16uy, 255uy, 41uy); C3b(214uy, 52uy, 189uy); C3b(82uy, 187uy, 90uy); C3b(148uy, 120uy, 140uy) |], C3b(115uy, 154uy, 115uy)
        |]

        let private alphaCases = function
            | CompressionMode.BC1 -> [| Array.empty<byte>, Array.create 16 255uy |]
            | CompressionMode.BC2 -> [|
                Array.create 8 255uy, Array.create 16 255uy
                [| 0x10uy; 0x32uy; 0x54uy; 0x76uy; 0x98uy; 0xBAuy; 0xDCuy; 0xFEuy |], Array.init 16 (fun i -> byte (17 * i))
              |]
            | CompressionMode.BC3 -> [|
                [| 255uy; 255uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy |], Array.create 16 255uy
                // The six selector bytes encode indices 0..7 twice, including selectors crossing byte boundaries.
                [| 255uy; 0uy; 0x88uy; 0xC6uy; 0xFAuy; 0x88uy; 0xC6uy; 0xFAuy |],
                    Array.init 16 (fun i -> [| 255uy; 0uy; 218uy; 182uy; 145uy; 109uy; 72uy; 36uy |].[i % 8])
                [| 11uy; 240uy; 0x88uy; 0xC6uy; 0xFAuy; 0x88uy; 0xC6uy; 0xFAuy |],
                    Array.init 16 (fun i -> [| 11uy; 240uy; 56uy; 102uy; 148uy; 194uy; 0uy; 255uy |].[i % 8])
              |]
            | mode -> failwithf "Unexpected fixture format %A" mode

        let private block mode paletteIndex alphaIndex variedRows =
            let _, c0, c1, fourColors, midpoint = palettes.[paletteIndex]
            let alphaBytes, alpha = (alphaCases mode).[alphaIndex]
            let rows, selectors =
                if variedRows then
                    [| 0xE4uy; 0x1Buy; 0x4Euy; 0xB1uy |], [| 0; 1; 2; 3; 3; 2; 1; 0; 2; 3; 0; 1; 1; 0; 3; 2 |]
                else
                    Array.create 4 0xE4uy, Array.init 16 (fun i -> i % 4)
            let rgbBytes = Array.append [| byte c0; byte (c0 >>> 8); byte c1; byte (c1 >>> 8) |] rows
            let expected =
                selectors |> Array.mapi (fun i selector ->
                    if mode = CompressionMode.BC1 && c0 <= c1 then
                        match selector with
                        | 2 -> C4b(midpoint, 255uy)
                        | 3 -> C4b.Zero
                        | _ -> C4b(fourColors.[selector], 255uy)
                    else
                        C4b(fourColors.[selector], alpha.[i])
                )
            Array.append alphaBytes rgbBytes, expected

        let private layouts = ["packed"; "padded"; "planar"; "reversed rows"]

        let private check mode channels layout (offset : V2i) (size : V2i) (blocks : (byte[] * C4b[])[]) context =
            let blocksX = (offset.X + size.X + 3) / 4
            let blocksY = (offset.Y + size.Y + 3) / 4
            Expect.equal blocks.Length (blocksX * blocksY) "Handcrafted block count"
            let payload = blocks |> Array.collect fst
            let source = Array.create (payload.Length + 32) 0xD7uy
            System.Array.Copy(payload, 0, source, 16, payload.Length)
            let original = Array.copy source
            let dx, dy, dc =
                match layout with
                | "packed" -> channels, size.X * channels, 1
                | "padded" -> channels + 2, (size.X + 3) * (channels + 2) + 5, 1
                | "planar" -> 1, size.X + 3, (size.Y + 2) * (size.X + 3) + 7
                | "reversed rows" -> channels + 1, -((size.X + 3) * (channels + 1) + 5), 1
                | _ -> failwith "Unexpected fixture layout"
            let ex, ey, ec = (size.X - 1) * dx, (size.Y - 1) * dy, (channels - 1) * dc
            let origin = 32 - min 0 ex - min 0 ey - min 0 ec
            let length = origin + max 0 ex + max 0 ey + max 0 ec + 33
            let destination = Array.create length 0xCDuy
            let expected = Array.copy destination
            for y in 0 .. size.Y - 1 do
                for x in 0 .. size.X - 1 do
                    let sx, sy = x + offset.X, y + offset.Y
                    let _, colors = blocks.[(sy / 4) * blocksX + sx / 4]
                    let color = colors.[(sy % 4) * 4 + sx % 4]
                    for c in 0 .. channels - 1 do
                        expected.[origin + x * dx + y * dy + c * dc] <- color.[c]
            let info = VolumeInfo(int64 origin, V3l(size.X, size.Y, channels), V3l(dx, dy, dc))
            source |> NativePtr.pinArr (fun src ->
                destination |> NativePtr.pinArr (fun dst ->
                    BlockCompression.decode mode offset size (src.Address + 16n) dst.Address info
                )
            )
            Expect.equal source original $"{context}: source and its guards are unchanged"
            for i in 0 .. destination.Length - 1 do
                Expect.equal destination.[i] expected.[i]
                    $"{context}, {mode}, {layout}, channels={channels}, offset={offset}, size={size}, destination byte={i} (including guards)"

        let palette mode () =
            for paletteIndex in 0 .. palettes.Length - 1 do
                let name, c0, c1, _, _ = palettes.[paletteIndex]
                for alphaIndex in 0 .. (alphaCases mode).Length - 1 do
                    for channels in (if mode = CompressionMode.BC1 then [3; 4] else [4]) do
                        for layout in layouts do
                            let context = $"{name}, endpoints={c0}/{c1}, alpha case={alphaIndex}, color index bytes=0xE4"
                            check mode channels layout V2i.Zero (V2i(4, 4)) [| block mode paletteIndex alphaIndex false |] context

        let windows() =
            for mode in [CompressionMode.BC1; CompressionMode.BC2; CompressionMode.BC3] do
                for offset, size in [V2i.Zero, V2i(1, 1); V2i.Zero, V2i(3, 2); V2i(1, 1), V2i(2, 2);
                                     V2i(3, 3), V2i(1, 1); V2i.Zero, V2i(9, 7); V2i(1, 2), V2i(6, 5); V2i(3, 1), V2i(7, 7)] do
                    let count = ((offset.X + size.X + 3) / 4) * ((offset.Y + size.Y + 3) / 4)
                    let blocks = Array.init count (fun i -> block mode (i % palettes.Length) (i % (alphaCases mode).Length) true)
                    for channels in (if mode = CompressionMode.BC1 then [3; 4] else [4]) do
                        for layout in layouts do
                            check mode channels layout offset size blocks "Mixed handcrafted blocks with distinct selector rows"

    module private SignedDecoding =

        // Literal RGTC palettes, including truncation toward zero. The raw
        // (-127, -128) pair is implementation-dependent and is not an oracle.
        let private minimumPalettes = [|
            27, -128, [|27; -127; 5; -17; -39; -61; -83; -105|]
            -128, 27, [|-127; 27; -96; -65; -34; -3; -127; 127|]
            -128, -128, [|-127; -127; -127; -127; -127; -127; -127; 127|]
            -128, -127, [|-127; -127; -127; -127; -127; -127; -127; 127|]
            -1, -128, [|-1; -127; -19; -37; -55; -73; -91; -109|]
            -128, -1, [|-127; -1; -101; -76; -51; -26; -127; 127|]
        |]

        let private canonicalPalettes = [|
            27, -127, [|27; -127; 5; -17; -39; -61; -83; -105|]
            -127, 27, [|-127; 27; -96; -65; -34; -3; -127; 127|]
            127, -127, [|127; -127; 90; 54; 18; -18; -54; -90|]
            -127, 127, [|-127; 127; -76; -25; 25; 76; -127; 127|]
            -127, -127, [|-127; -127; -127; -127; -127; -127; -127; 127|]
        |]

        let private unsignedPalettes = [|
            27, 128, [|27; 128; 47; 67; 87; 107; 0; 255|]
            128, 27, [|128; 27; 113; 99; 84; 70; 55; 41|]
            255, 0, [|255; 0; 218; 182; 145; 109; 72; 36|]
            128, 128, [|128; 128; 128; 128; 128; 128; 0; 255|]
        |]

        let private block mode (palettes : (int * int * int[])[]) first variant =
            let count = match mode with CompressionMode.BC5 _ -> 2 | _ -> 1
            let blocks = Array.init count (fun channel ->
                let r0, r1, palette = palettes.[(first + channel * 3) % palettes.Length]
                let indices = Array.init 16 (fun i -> (i + channel * 3 + variant) % 8)
                let mutable bits = 0UL
                for i in 0 .. 15 do bits <- bits ||| (uint64 indices.[i] <<< (i * 3))
                let data = Array.init 8 (fun i -> if i = 0 then byte r0 elif i = 1 then byte r1 else byte (bits >>> ((i - 2) * 8)))
                data, Array.map (fun i -> byte palette.[i]) indices
            )
            let data = blocks |> Array.collect fst
            let expected = Array.init 16 (fun i -> blocks |> Array.map (fun (_, values) -> values.[i]))
            if mode = CompressionMode.BC3 then
                // Equal white RGB endpoints isolate the unchanged unsigned alpha path.
                Array.append data [|255uy; 255uy; 255uy; 255uy; 0uy; 0uy; 0uy; 0uy|],
                expected |> Array.map (fun alpha -> [|255uy; 255uy; 255uy; alpha.[0]|])
            else data, expected

        let private check mode channels layout (offset : V2i) (size : V2i) palettes first =
            let context = $"{mode}, channels={channels}, layout={layout}, offset={offset}, size={size}, firstPalette={first}"
            let blocksX, blocksY = (offset.X + size.X + 3) / 4, (offset.Y + size.Y + 3) / 4
            let blocks = Array.init (blocksX * blocksY) (fun i -> block mode palettes (first + i) i)
            let payload = blocks |> Array.collect fst
            let source = Array.create (payload.Length + 32) 0xD7uy
            System.Array.Copy(payload, 0, source, 16, payload.Length)
            let original = Array.copy source
            let dx, dy, dc =
                match layout with
                | "packed" -> channels, size.X * channels, 1
                | "padded" -> channels + 2, (size.X + 3) * (channels + 2) + 5, 1
                | "planar" -> 1, size.X + 3, (size.Y + 2) * (size.X + 3) + 7
                | "reversed rows" -> channels + 1, -((size.X + 3) * (channels + 1) + 5), 1
                | _ -> failwith "Unexpected signed decoding layout"
            let ey = (size.Y - 1) * dy
            let origin = 32 - min 0 ey
            let length = origin + (size.X - 1) * dx + max 0 ey + (channels - 1) * dc + 33
            let destination = Array.create length 0xCDuy
            let expected = Array.copy destination
            for y in 0 .. size.Y - 1 do
                for x in 0 .. size.X - 1 do
                    let sx, sy = x + offset.X, y + offset.Y
                    let _, values = blocks.[(sy / 4) * blocksX + sx / 4]
                    let values = values.[(sy % 4) * 4 + sx % 4]
                    for c in 0 .. values.Length - 1 do
                        expected.[origin + x * dx + y * dy + c * dc] <- values.[c]
            let info = VolumeInfo(int64 origin, V3l(size.X, size.Y, channels), V3l(dx, dy, dc))
            source |> NativePtr.pinArr (fun src ->
                destination |> NativePtr.pinArr (fun dst ->
                    BlockCompression.decode mode offset size (src.Address + 16n) dst.Address info
                )
            )
            Expect.equal source original $"{context}: source and guards changed"
            for i in 0 .. destination.Length - 1 do
                Expect.equal destination.[i] expected.[i] $"{context}: destination byte {i}, including guards and spare channels"

        let private layouts = ["packed"; "padded"; "planar"; "reversed rows"]

        let private checkPalettes modes palettes =
            for mode in modes do
                let channels = match mode with CompressionMode.BC4 _ -> [1; 3] | CompressionMode.BC5 _ -> [2; 4] | _ -> [4]
                for first in 0 .. Array.length palettes - 1 do
                    for channelCount in channels do
                        for layout in layouts do
                            check mode channelCount layout V2i.Zero (V2i(4)) palettes first

        let minimumEndpoints() =
            checkPalettes [CompressionMode.BC4 true; CompressionMode.BC5 true] minimumPalettes

        let guardedWindows() =
            for mode in [CompressionMode.BC4 true; CompressionMode.BC5 true] do
                let channels = if mode = CompressionMode.BC4 true then 3 else 4
                for offset, size in [V2i.Zero, V2i.One; V2i.Zero, V2i(3, 2); V2i(1, 1), V2i(2, 2);
                                     V2i(3, 3), V2i.One; V2i.Zero, V2i(9, 7); V2i(1, 2), V2i(6, 5); V2i(3, 1), V2i(7, 7)] do
                    for first in 0 .. minimumPalettes.Length - 1 do
                        for layout in layouts do
                            check mode channels layout offset size minimumPalettes first

        let compatibility() =
            checkPalettes [CompressionMode.BC4 true; CompressionMode.BC5 true] canonicalPalettes
            checkPalettes [CompressionMode.BC4 false; CompressionMode.BC5 false; CompressionMode.BC3] unsignedPalettes

    let tests (target: TestTarget) =
        [
            "Signed decoding.Minimum endpoint palettes",   SignedDecoding.minimumEndpoints
            "Signed decoding.Guarded windows and strides", SignedDecoding.guardedWindows
            "Signed decoding.Unchanged palette controls",  SignedDecoding.compatibility

            "Encoding.Chromatic endpoint variation",           EndpointEncoding.chromaticBlocks
            "Encoding.Partial and multiple chromatic blocks",  EndpointEncoding.partialAndMultipleBlocks
            "Encoding.Solid colors and grayscale",             EndpointEncoding.solidAndGrayscale
            "Encoding.Tiny blocks",                            EndpointEncoding.tinyBlocks
            "Encoding.Chromatic alpha controls",               EndpointEncoding.alpha

            "BC1 handcrafted palette and transparency",          Handcrafted.palette CompressionMode.BC1
            "BC2 handcrafted four-color and explicit alpha",     Handcrafted.palette CompressionMode.BC2
            "BC3 handcrafted four-color and interpolated alpha", Handcrafted.palette CompressionMode.BC3
            "Handcrafted partial blocks and guarded windows",    Handcrafted.windows

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

            for mode, bytesPerBlock in Cases.modes do
                $"Layout.{mode}.4x4x1 blocks",                    Cases.blocks4x4x1 mode bytesPerBlock
                $"Layout.{mode}.1D and 2D compatibility",         Cases.compatibility mode bytesPerBlock
                $"Layout.{mode}.Representable large byte counts", Cases.representableLargeByteCounts mode bytesPerBlock

            "Layout.Uncompressed layout is unchanged", Cases.uncompressedLayoutUnchanged
            "Layout.Minimum block count is unchanged", Cases.minimumBlockCountUnchanged
        ]
        |> prepareCasesCpu "Compression" target