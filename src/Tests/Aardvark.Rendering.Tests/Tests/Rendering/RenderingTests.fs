namespace Aardvark.Rendering.Tests

open Aardvark.Application
open Aardvark.Rendering.Tests.Rendering
open Expecto

module ``Rendering Tests`` =

    let private tests = [
        Culling.tests
        RenderTasks.tests
        FramebufferSignature.tests
        IntegerAttachments.tests
        Samplers.tests
        Uniforms.tests
    ]

    [<Tests>]
    let textureTestsGL =
        tests |> testBackend Backend.GL "Rendering"

    [<Tests>]
    let textureTestsVulkan =
        tests |> testBackend Backend.Vulkan "Rendering"