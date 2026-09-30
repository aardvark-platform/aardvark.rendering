open Aardvark.Rendering.Tests
open Expecto

[<Tests>]
let tests =
    Test.ofList [
        testAllTargets "Buffers" [
            Buffer.BufferCopy.tests
            Buffer.BufferUpload.tests
            Buffer.BufferDownload.tests
            Buffer.BufferToArray.tests
            Buffer.AttributeBuffer.tests
            Buffer.ManagedBuffer.tests
            Buffer.IndirectBuffer.tests
            Buffer.BufferView.tests
        ]

        testAllTargets "Textures" [
            Texture.TextureUpload.tests
            Texture.TextureDownload.tests
            Texture.TextureCreate.tests
            Texture.TextureCopy.tests
            Texture.TextureClear.tests
            Texture.TextureCompression.tests
            Texture.TextureDds.tests
        ]

        testAllTargets "Rendering" [
            Rendering.Blending.tests
            Rendering.ColorMasks.tests
            Rendering.Culling.tests
            Rendering.RenderTasks.tests
            Rendering.FramebufferSignature.tests
            Rendering.IntegerAttachments.tests
            Rendering.Samplers.tests
            Rendering.Uniforms.tests
            Rendering.Surfaces.tests
            Rendering.DrawCalls.tests
            Rendering.Viewport.tests
            Rendering.ResourceManagement.tests
            Rendering.Commands.tests
            Rendering.LodRenderer.tests
            Rendering.SceneGraph.tests
        ]

        testAllTargets "Compute" [
            Compute.ComputeImages.tests
            Compute.ComputeBuffers.tests
            Compute.ComputePrimitives.tests
            Compute.ComputeSorting.tests
            Compute.ComputeJpeg.tests
            Compute.MutableInputBinding.tests
        ]

        testAllTargets "Raytracing" [
            Raytracing.TraceGeometry.tests
        ]

        testAllTargets "IndexedGeometry" [
            IndexedGeometry.Operations.tests
            IndexedGeometry.Sphere.tests
            IndexedGeometry.CylinderNormals.tests
            IndexedGeometry.PrimitiveSequences.tests
        ]

        testAllTargets "Utilities" [
            Utilities.CompactSet.tests
            Utilities.MemoryManager.tests
            Utilities.Trie.tests
        ]

        testAllTargets "Other" [
            Other.Camera.tests
            Other.ShapeListConcat.tests
            Other.UniformWriter.tests
            Other.AdaptiveResource.tests
            Other.ContextCreation.tests
            Other.VulkanWrapper.tests
            Other.IDictionaryStructuralComparer.tests
        ]
    ]

[<EntryPoint>]
let main argv =
    let runManuallyInMain = true

    if runManuallyInMain then
        runTestsSynchronously true tests
    else
        runTestsWithCLIArgs [ CLIArguments.No_Spinner ] argv tests