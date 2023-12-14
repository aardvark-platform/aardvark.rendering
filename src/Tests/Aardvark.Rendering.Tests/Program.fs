open Aardvark.Rendering.Tests
open Aardvark.Application
open Expecto

[<EntryPoint>]
let main argv =

    let backendTests backend =
        let bufferTests =
            testBackend backend "Buffers" [
                Buffer.BufferCopy.tests
                Buffer.BufferUpload.tests
                Buffer.BufferDownload.tests
                Buffer.AttributeBuffer.tests
            ]

        let textureTests =
            testBackend backend "Textures" [
                Texture.TextureUpload.tests
                Texture.TextureDownload.tests
                Texture.TextureCreate.tests
                Texture.TextureCopy.tests
                Texture.TextureClear.tests
            ]

        let renderingTests =
            testBackend backend "Rendering" [
                Rendering.Culling.tests
                Rendering.RenderTasks.tests
                Rendering.FramebufferSignature.tests
                Rendering.IntegerAttachments.tests
                Rendering.Samplers.tests
                Rendering.Uniforms.tests
            ]

        let computeTests =
            testBackend backend "Compute" [
                Rendering.ComputeImages.tests
                Rendering.ComputeBuffers.tests
                Rendering.ComputePrimitives.tests
                Rendering.ComputeSorting.tests
                Rendering.ComputeJpeg.tests
                Rendering.MutableInputBinding.tests
            ]
        testList $"Tests ({backend})" [
            bufferTests
            textureTests
            renderingTests
            computeTests
        ]

    let otherTests =
        testList "Other tests" [
            ``SceneGraph Tests``.tests
            ``CompactSet Tests``.tests
            ``AdaptiveResource Tests``.tests
        ]

    let allTests =
        testList "Tests" [
            otherTests
            backendTests Backend.GL
            backendTests Backend.Vulkan
        ]

    let runManuallyInMain = true

    if runManuallyInMain then
        runTestsSynchronously false allTests
    else
        runTestsWithCLIArgs [ CLIArguments.No_Spinner ] argv allTests
