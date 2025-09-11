namespace Aardvark.Rendering.Tests

open Aardvark.Application
open Aardvark.Rendering.Tests.Compute
open Expecto

module ``Compute Tests`` =

    let private tests = [
        ComputeImages.tests
        ComputeBuffers.tests
        ComputePrimitives.tests
        ComputeSorting.tests
        ComputeJpeg.tests
        MutableInputBinding.tests
    ]

    [<Tests>]
    let testsGL =
        tests |> testBackend Backend.GL "Compute"

    [<Tests>]
    let testsVulkan =
        tests |> testBackend Backend.Vulkan "Compute"