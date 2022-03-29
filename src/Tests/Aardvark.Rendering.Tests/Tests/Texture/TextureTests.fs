namespace Aardvark.Rendering.Tests

open Aardvark.Application
open Aardvark.Rendering.Tests.Texture
open Expecto

module ``Texture Tests`` =

    let private tests = [
        TextureUpload.tests
        TextureDownload.tests
        TextureCreate.tests
        TextureCopy.tests
        TextureClear.tests
    ]

    [<Tests>]
    let textureCompressionTests =
        [ TextureCompression.tests ] |> testList "PixImage"

    [<Tests>]
    let textureTestsGL =
        tests |> testBackend Backend.GL "Textures"

    [<Tests>]
    let textureTestsVulkan =
        tests |> testBackend Backend.Vulkan "Textures"