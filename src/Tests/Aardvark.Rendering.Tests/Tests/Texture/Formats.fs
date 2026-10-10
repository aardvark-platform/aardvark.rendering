namespace Aardvark.Rendering.Tests.Texture

open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto

module TextureFormats =

    module private Cases =

        let integerClassification() =
            let integerFormats = [
                TextureFormat.R8i; TextureFormat.R8ui
                TextureFormat.R16i; TextureFormat.R16ui
                TextureFormat.R32i; TextureFormat.R32ui
                TextureFormat.Rg8i; TextureFormat.Rg8ui
                TextureFormat.Rg16i; TextureFormat.Rg16ui
                TextureFormat.Rg32i; TextureFormat.Rg32ui
                TextureFormat.Rgb8i; TextureFormat.Rgb8ui
                TextureFormat.Rgb16i; TextureFormat.Rgb16ui
                TextureFormat.Rgb32i; TextureFormat.Rgb32ui
                TextureFormat.Rgba8i; TextureFormat.Rgba8ui
                TextureFormat.Rgba16i; TextureFormat.Rgba16ui
                TextureFormat.Rgba32i; TextureFormat.Rgba32ui
                TextureFormat.Rgb10A2ui
            ]
            let nonIntegerFormats = [
                TextureFormat.Rgba8; TextureFormat.Rgb10A2; TextureFormat.Rgb10
                TextureFormat.Rgba16; TextureFormat.Rgba8Snorm; TextureFormat.Rgba16Snorm
                TextureFormat.Srgb8; TextureFormat.Srgb8Alpha8
                TextureFormat.R16f; TextureFormat.Rg32f; TextureFormat.Rgba32f
                TextureFormat.R11fG11fB10f; TextureFormat.Rgb9E5
                TextureFormat.CompressedRgbaS3tcDxt5; TextureFormat.CompressedSignedRedRgtc1
                TextureFormat.CompressedRgRgtc2; TextureFormat.CompressedRgbBptcUnsignedFloat
                TextureFormat.CompressedRgbaBptcUnorm
                TextureFormat.DepthComponent16; TextureFormat.DepthComponent24
                TextureFormat.DepthComponent32; TextureFormat.DepthComponent32f
                TextureFormat.Depth24Stencil8; TextureFormat.Depth32fStencil8
                TextureFormat.StencilIndex8
            ]
            for expected, formats in [true, integerFormats; false, nonIntegerFormats] do
                for format in formats do
                    Expect.equal (TextureFormat.isIntegerFormat format) expected $"{format}: module integer classification"
                    Expect.equal format.IsIntegerFormat expected $"{format}: extension integer classification"
            Expect.isFalse TextureFormat.Rgb10A2ui.IsSigned "Packed unsigned integer format changed signedness"

    let tests (target : TestTarget) =
        [
            "Integer classification", Cases.integerClassification
        ]
        |> prepareCasesCpu "Formats" target
