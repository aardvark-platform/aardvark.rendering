namespace Aardvark.Rendering.Tests

open Aardvark.Application
open Aardvark.Rendering.Tests.Buffer
open Expecto

module ``Buffer Tests`` =

    let private tests =
        [
            BufferCopy.tests
            BufferUpload.tests
            BufferDownload.tests
            BufferToArray.tests
            AttributeBuffer.tests
        ]

    [<Tests>]
    let testsGL =
        tests |> testBackend Backend.GL "Buffers"

    [<Tests>]
    let testsVulkan =
        tests |> testBackend Backend.Vulkan "Buffers"