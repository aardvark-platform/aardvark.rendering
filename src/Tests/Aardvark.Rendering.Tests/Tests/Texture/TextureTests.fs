namespace Aardvark.Rendering.Tests

open Aardvark.Application
open Aardvark.Rendering.Tests.Texture
open Expecto

module ``Texture Tests`` =

    let private tests = [
        TextureDownload.tests
        TextureCreate.tests
        TextureCopy.tests
    ]

    [<Tests>]
    let textureTestsGL =
        tests |> testBackend Backend.GL "Textures"

    [<Tests>]
    let textureTestsVulkan =
        tests |> testBackend Backend.Vulkan "Textures"