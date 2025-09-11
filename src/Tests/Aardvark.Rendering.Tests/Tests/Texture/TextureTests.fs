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
    let compressionTests =
        [ TextureCompression.tests ] |> testList "PixImage"

    [<Tests>]
    let testsGL =
        tests |> testBackend Backend.GL "Textures"

    [<Tests>]
    let testsVulkan =
        tests |> testBackend Backend.Vulkan "Textures"