namespace Aardvark.Rendering.Tests.Texture

open System
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto

module PixelSize =

    module private Cases =

        let private checkBits format expected =
            let context = sprintf "format=%A" format
            Expect.equal (TextureFormat.pixelSizeInBits format) expected (context + ": module bits")
            Expect.equal format.PixelSizeInBits expected (context + ": extension bits")

        let private checkBytes format expected =
            let context = sprintf "format=%A" format
            Expect.equal (TextureFormat.pixelSizeInBytes format) expected (context + ": module bytes")
            Expect.equal format.PixelSizeInBytes expected (context + ": extension bytes")

        let uncompressedSizes () =
            for format, bits in [
                TextureFormat.R16f, 16
                TextureFormat.Rg16f, 32
                TextureFormat.Rgb16f, 48
                TextureFormat.Rgba16f, 64
                TextureFormat.R32f, 32
                TextureFormat.Rg32f, 64
                TextureFormat.Rgb32f, 96
                TextureFormat.Rgba32f, 128
                TextureFormat.R16i, 16
                TextureFormat.R16ui, 16
                TextureFormat.Rg16i, 32
                TextureFormat.Rg16ui, 32
                TextureFormat.Rgb16i, 48
                TextureFormat.Rgb16ui, 48
                TextureFormat.Rgba16i, 64
                TextureFormat.Rgba16ui, 64
                TextureFormat.R16, 16
                TextureFormat.Rg16, 32
                TextureFormat.Rgb16, 48
                TextureFormat.Rgba16, 64
                TextureFormat.R16Snorm, 16
                TextureFormat.Rg16Snorm, 32
                TextureFormat.Rgb16Snorm, 48
                TextureFormat.Rgba16Snorm, 64
            ] do
                checkBits format bits
                checkBytes format (bits / 8)

        let compressedSentinels () =
            for format in [
                TextureFormat.CompressedRgbS3tcDxt1
                TextureFormat.CompressedSrgbS3tcDxt1
                TextureFormat.CompressedRgbaS3tcDxt1
                TextureFormat.CompressedSrgbAlphaS3tcDxt1
                TextureFormat.CompressedRgbaS3tcDxt3
                TextureFormat.CompressedSrgbAlphaS3tcDxt3
                TextureFormat.CompressedRgbaS3tcDxt5
                TextureFormat.CompressedSrgbAlphaS3tcDxt5
                TextureFormat.CompressedRedRgtc1
                TextureFormat.CompressedSignedRedRgtc1
                TextureFormat.CompressedRgRgtc2
                TextureFormat.CompressedSignedRgRgtc2
                TextureFormat.CompressedRgbaBptcUnorm
                TextureFormat.CompressedSrgbAlphaBptcUnorm
                TextureFormat.CompressedRgbBptcSignedFloat
                TextureFormat.CompressedRgbBptcUnsignedFloat
            ] do
                checkBits format -1
                checkBytes format -1

        let illAlignedSizes () =
            for format, bits in [
                TextureFormat.Rgb4, 12
                TextureFormat.Rgb5, 15
                TextureFormat.Rgb10, 30
                TextureFormat.Rgb12, 36
            ] do
                checkBits format bits
                for label, getBytes in [
                    "module", TextureFormat.pixelSizeInBytes
                    "extension", fun format -> format.PixelSizeInBytes
                ] do
                    let context = sprintf "format=%A, bits=%d, path=%s" format bits label
                    let mutable failure = None
                    try getBytes format |> ignore
                    with error -> failure <- Some error
                    Expect.isSome failure (context + ": byte size is rejected")
                    let error = failure.Value
                    Expect.equal (error.GetType()) typeof<Exception> (context + ": exception type")
                    Expect.equal error.Message (sprintf "[TextureFormat] ill-aligned size %d" bits) (context + ": exception message")

    let tests (target : TestTarget) =
        [
            "Uncompressed module and extension sizes", Cases.uncompressedSizes
            "Compressed size sentinels",               Cases.compressedSentinels
            "Ill-aligned byte sizes remain rejected",  Cases.illAlignedSizes
        ]
        |> prepareCasesCpu "PixelSize" target
