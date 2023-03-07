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
            SingleValueBuffer.tests
        ]

    [<Tests>]
    let bufferTestsGL =
        tests |> testBackend Backend.GL "Buffers"

    [<Tests>]
    let bufferTestsVulkan =
        tests |> testBackend Backend.Vulkan "Buffers"