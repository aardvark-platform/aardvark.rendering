namespace Aardvark.Rendering.Tests

open Aardvark.Application
open Aardvark.Rendering.Tests.Rendering
open Expecto

module ``Rendering Tests`` =

    let private tests = [
        Blending.tests
        ColorMasks.tests
        Culling.tests
        RenderTasks.tests
        FramebufferSignature.tests
        IntegerAttachments.tests
        Samplers.tests
        Uniforms.tests
        Surfaces.tests
        DrawCalls.tests
        Viewport.tests
        ResourceManagement.tests
        Commands.tests
    ]

    [<Tests>]
    let testsGL =
        tests |> testBackend Backend.GL "Rendering"

    [<Tests>]
    let testsVulkan =
        tests |> testBackend Backend.Vulkan "Rendering"