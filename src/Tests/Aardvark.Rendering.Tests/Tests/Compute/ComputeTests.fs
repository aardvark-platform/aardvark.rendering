namespace Aardvark.Rendering.Tests

open Aardvark.Application
open Aardvark.Rendering.Tests.Rendering
open Expecto

module ``Compute Tests`` =

    let private tests = [
        ComputeImages.tests
        MutableInputBinding.tests
    ]

    [<Tests>]
    let textureTestsGL =
        tests |> testBackend Backend.GL "Compute"

    [<Tests>]
    let textureTestsVulkan =
        tests |> testBackend Backend.Vulkan "Compute"