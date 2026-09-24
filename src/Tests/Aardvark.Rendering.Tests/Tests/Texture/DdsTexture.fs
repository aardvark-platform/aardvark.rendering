namespace Aardvark.Rendering.Tests.Texture

open System.IO
open System.Runtime.InteropServices
open System.Text
open Aardvark.Base
open Aardvark.Rendering
open Expecto

module TextureDds =

    type private Header =
        | Legacy of fourCC : string
        | DX10 of dxgiFormat : uint32

    type private Fixture =
        { Bytes : byte[]
          HeaderSize : int
          Sizes : V3i[]
          Payloads : byte[][][]
          Dimension : TextureDimension
          Count : int
          Format : TextureFormat }

    // Write the wire format directly, independently of the parser's private header types.
    let private fixture header format dimension count (size : V3i) levels bytesPerBlock =
        let headerSize = match header with Legacy _ -> 128 | DX10 _ -> 148
        let bytes = Array.zeroCreate<byte> headerSize
        use stream = new MemoryStream(bytes, true)
        use writer = new BinaryWriter(stream, Encoding.ASCII, true)
        let put offset (value : uint32) =
            stream.Position <- int64 offset
            writer.Write value

        put 0 0x20534444u // DDS magic
        put 4 124u
        let isVolume = dimension = TextureDimension.Texture3D
        let isCube = dimension = TextureDimension.TextureCube
        put 8 (0x81007u ||| (if levels > 1 then 0x20000u else 0u) ||| (if isVolume then 0x800000u else 0u))
        put 12 (uint32 size.Y)
        put 16 (uint32 size.X)
        put 20 (uint32 (((size.X + 3) / 4) * ((size.Y + 3) / 4) * size.Z * bytesPerBlock))
        put 24 (if isVolume then uint32 size.Z else 0u)
        put 28 (uint32 levels)
        put 76 32u // DDS_PIXELFORMAT size
        put 80 4u // DDPF_FOURCC
        stream.Position <- 84L
        writer.Write(Encoding.ASCII.GetBytes(match header with Legacy cc -> cc | DX10 _ -> "DX10"))
        put 108 (0x1000u ||| (if levels > 1 then 0x400008u elif isCube || isVolume then 8u else 0u))
        put 112 (if isVolume then 0x200000u elif isCube then 0xFE00u else 0u)

        match header with
        | Legacy _ -> ()
        | DX10 dxgi ->
            put 128 dxgi
            put 132 (if dimension = TextureDimension.Texture1D then 2u elif isVolume then 4u else 3u)
            put 136 (if isCube then 4u else 0u)
            put 140 (uint32 count)
            put 144 0u

        let sizes = Array.init levels (fun level -> V3i(max 1 (size.X >>> level), max 1 (size.Y >>> level), max 1 (size.Z >>> level)))
        let slices = if isCube then count * 6 else count
        let mutable marker = 0
        let payloads =
            Array.init slices (fun _ ->
                sizes |> Array.map (fun size ->
                    let planeBytes = ((size.X + 3) / 4) * ((size.Y + 3) / 4) * bytesPerBlock
                    Array.init size.Z (fun _ ->
                        // Every mip/depth/array/cube plane has a distinct marker and byte pattern.
                        marker <- marker + 1
                        Array.init planeBytes (fun i -> byte ((marker + i) % 256))
                    ) |> Array.concat
                )
            )

        { Bytes = Array.concat [| bytes; payloads |> Array.collect Array.concat |]
          HeaderSize = headerSize
          Sizes = sizes
          Payloads = payloads
          Dimension = dimension
          Count = count
          Format = format }

    let private verify (fixture : Fixture) wantMipMaps =
        // Trailing data catches overreads; exact Position catches underreads.
        let suffix = [| 0xDEuy; 0xADuy; 0xBEuy; 0xEFuy |]
        for tryLoad in [false; true] do
            use stream = new MemoryStream(Array.append fixture.Bytes suffix, false)
            let texture =
                if tryLoad then
                    let result = DdsTexture.tryLoadCompressedFromStream wantMipMaps stream
                    Expect.isSome result "Valid DDS must load"
                    Option.get result
                else
                    DdsTexture.loadCompressedFromStream wantMipMaps stream

            Expect.equal texture.Dimension fixture.Dimension "Texture dimension"
            Expect.equal texture.Format fixture.Format "Texture format"
            Expect.equal texture.Count fixture.Count "Array/cube count"
            Expect.equal texture.MipMapLevels fixture.Sizes.Length "Stored mip count is independent of WantMipMaps"
            Expect.equal texture.WantMipMaps wantMipMaps "Requested mip policy"
            for slice = 0 to fixture.Payloads.Length - 1 do
                for level = 0 to fixture.Sizes.Length - 1 do
                    let data = texture.[slice, level]
                    let expected = fixture.Payloads.[slice].[level]
                    Expect.equal data.Size fixture.Sizes.[level] $"Size of slice {slice}, mip {level}"
                    Expect.equal data.SizeInBytes (uint64 expected.Length) $"Bytes in slice {slice}, mip {level}"
                    let actual = Array.zeroCreate<byte> expected.Length
                    data.Use(fun ptr -> Marshal.Copy(ptr, actual, 0, actual.Length))
                    Expect.sequenceEqual actual expected $"Payload of slice {slice}, mip {level}"
            Expect.equal stream.Position (int64 fixture.Bytes.Length) "Exactly the complete DDS payload is consumed"
            for value in suffix do
                Expect.equal (stream.ReadByte()) (int value) "Trailing bytes remain unread"

    let private verifyTruncated (fixture : Fixture) =
        let payloadSize = fixture.Bytes.Length - fixture.HeaderSize
        for wantMipMaps in [false; true] do
            // Every prefix includes the old, incorrectly accepted 24-byte BC1 / 48-byte BC3 payload.
            for length = 0 to payloadSize - 1 do
                use stream = new MemoryStream(fixture.Bytes, 0, fixture.HeaderSize + length, false)
                Expect.throwsT<DdsTexture.DdsParseException>
                    (fun () -> DdsTexture.loadCompressedFromStream wantMipMaps stream |> ignore)
                    $"Reject payload prefix of {length}/{payloadSize} bytes"
            use stream = new MemoryStream(fixture.Bytes, 0, fixture.Bytes.Length - 1, false)
            Expect.isNone (DdsTexture.tryLoadCompressedFromStream wantMipMaps stream) "Try-load rejects a missing final byte"

    let tests =
        let formats =
            [ "legacy BC1", Legacy "DXT1", TextureFormat.CompressedRgbaS3tcDxt1, 8
              "legacy BC3", Legacy "DXT5", TextureFormat.CompressedRgbaS3tcDxt5, 16
              "DX10 BC1", DX10 71u, TextureFormat.CompressedRgbaS3tcDxt1, 8
              "DX10 BC2", DX10 74u, TextureFormat.CompressedRgbaS3tcDxt3, 16
              "DX10 BC3", DX10 77u, TextureFormat.CompressedRgbaS3tcDxt5, 16
              "DX10 BC4 unsigned", DX10 80u, TextureFormat.CompressedRedRgtc1, 8
              "DX10 BC4 signed", DX10 81u, TextureFormat.CompressedSignedRedRgtc1, 8
              "DX10 BC5 unsigned", DX10 83u, TextureFormat.CompressedRgRgtc2, 16
              "DX10 BC5 signed", DX10 84u, TextureFormat.CompressedSignedRgRgtc2, 16
              "DX10 BC6h unsigned", DX10 95u, TextureFormat.CompressedRgbBptcUnsignedFloat, 16
              "DX10 BC6h signed", DX10 96u, TextureFormat.CompressedRgbBptcSignedFloat, 16
              "DX10 BC7", DX10 98u, TextureFormat.CompressedRgbaBptcUnorm, 16 ]

        testList "DDS" [
            for name, header, format, bytesPerBlock in formats do
                testList name [
                    testCase "4x4x4 volume mip payloads" <| fun _ ->
                        let fixture = fixture header format TextureDimension.Texture3D 1 (V3i 4) 3 bytesPerBlock
                        let expected = if bytesPerBlock = 8 then [|32; 16; 8|] else [|64; 32; 16|]
                        Expect.sequenceEqual (fixture.Payloads.[0] |> Array.map Array.length) expected "Reference mip byte sizes"
                        Expect.equal (fixture.Bytes.Length - fixture.HeaderSize) (if bytesPerBlock = 8 then 56 else 112) "Reference volume payload size"
                        for wantMipMaps in [false; true] do verify fixture wantMipMaps

                    testCase "Unaligned XY and depth boundaries" <| fun _ ->
                        for depth in [1; 2; 3; 4; 5; 7; 8; 9] do
                            let levels = if depth >= 8 then 4 else 3
                            let fixture = fixture header format TextureDimension.Texture3D 1 (V3i(5, 7, depth)) levels bytesPerBlock
                            for wantMipMaps in [false; true] do verify fixture wantMipMaps

                    testCase "Truncated volume payloads" <| fun _ ->
                        fixture header format TextureDimension.Texture3D 1 (V3i 4) 3 bytesPerBlock |> verifyTruncated
                ]

            for name, header, format, bytesPerBlock in formats do
                // Legacy and DX10 2D/cube controls cover both block-byte sizes and every BC mode.
                for dimension in [TextureDimension.Texture2D; TextureDimension.TextureCube] do
                    testCase $"{name} {dimension} ordering" <| fun _ ->
                        let size = if dimension = TextureDimension.TextureCube then V3i(8, 8, 1) else V3i(5, 7, 1)
                        for levels in [1; 3] do
                            let fixture = fixture header format dimension 1 size levels bytesPerBlock
                            for wantMipMaps in [false; true] do verify fixture wantMipMaps

                match header with
                | Legacy _ -> ()
                | DX10 _ ->
                    for dimension in [TextureDimension.Texture1D; TextureDimension.Texture2D; TextureDimension.TextureCube] do
                        testCase $"{name} {dimension} array ordering" <| fun _ ->
                            let size =
                                match dimension with
                                | TextureDimension.Texture1D -> V3i(9, 1, 1)
                                | TextureDimension.TextureCube -> V3i(8, 8, 1)
                                | _ -> V3i(5, 7, 1)
                            for count in [1; 2] do
                                for levels in [1; 3] do
                                    let fixture = fixture header format dimension count size levels bytesPerBlock
                                    for wantMipMaps in [false; true] do verify fixture wantMipMaps
        ]
