open Aardvark.Rendering.Tests
open Expecto

let tests (targets: TestTarget list) =
    let testAllTargets  =
        if targets.IsEmpty then
            testAllTargets
        else
            fun group tests ->
                targets
                |> List.map (fun target -> testTarget target tests)
                |> testList group

    Test.ofList [
        testAllTargets "Buffers" [
            Buffer.AdaptiveBufferTests.tests
            Buffer.BufferCopy.tests
            Buffer.BufferUpload.tests
            Buffer.BufferDownload.tests
            Buffer.BufferToArray.tests
            Buffer.AttributeBuffer.tests
            Buffer.ManagedBuffer.tests
            Buffer.IndirectBuffer.tests
            Buffer.BufferView.tests
            Buffer.BufferViewBoundaries.tests
        ]

        testAllTargets "Textures" [
            Texture.AdaptiveTexture.tests
            Texture.AdaptiveAttachment.tests
            Texture.TextureUpload.tests
            Texture.TextureDownload.tests
            Texture.TextureCreate.tests
            Texture.TextureCopy.tests
            Texture.TextureClear.tests
            Texture.TextureCompression.tests
            Texture.TextureDds.tests
        ]

        testAllTargets "Rendering" [
            Rendering.AdaptiveFramebufferCube.tests
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
            Utilities.ConcurrentDeltaPriorityQueue.tests
            Utilities.CompactSet.tests
            Utilities.ColoredLock.tests
            Utilities.MemoryManager.tests
            Utilities.Trie.tests
            Utilities.TaskUtilities.tests
            Utilities.LockedSet.tests
        ]

        testAllTargets "Other" [
            Other.Camera.tests
            Other.ShapeListConcat.tests
            Other.UniformWriter.tests
            Other.AdaptiveResource.tests
            Other.Input.tests
            Other.ContextCreation.tests
            Other.VulkanWrapper.tests
            Other.IDictionaryStructuralComparer.tests
        ]
    ]

[<Tests>]
let allTests = tests []

[<EntryPoint>]
let main argv =
    let switches =
        Map.ofList [
            "cpu-only",     "Only run CPU tests."
            "stop-on-fail", "Stop as soon as a test fails."
            "help",         "Show this help text."
        ]

    let switch name =
        switches.ContainsKey name && argv |> Array.contains $"--{name}"

    if switch "help" then
        printfn "Run tests"
        printfn ""
        printfn "OPTIONS"
        let n = switches.Keys |> Seq.map _.Length |> Seq.max
        for KeyValue(name, desc) in switches do
            printfn $"    --{name.PadRight n}    {desc}"
        0
    else
        let tests =
            if switch "cpu-only" then
                tests [TestTarget.Cpu]
            else
                allTests

        let stopOnFail = switch "stop-on-fail"
        runTestsSynchronously stopOnFail tests