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

    let tests (target: TestTarget) =
        [
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