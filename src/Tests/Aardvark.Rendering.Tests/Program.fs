open Aardvark.Rendering.Tests
open Expecto

[<EntryPoint>]
let main argv =

    let allTests =
        testList "Tests" [
            ``Buffer Tests``.testsGL
            ``Buffer Tests``.testsVulkan

            ``Texture Tests``.compressionTests
            ``Texture Tests``.testsGL
            ``Texture Tests``.testsVulkan

            ``Rendering Tests``.testsGL
            ``Rendering Tests``.testsVulkan

            ``Compute Tests``.testsGL
            ``Compute Tests``.testsVulkan

            ``Camera Tests``.tests
            ``IndexedGeometry Tests``.tests
            ``SceneGraph Tests``.tests
            ``CompactSet Tests``.tests
            ``AdaptiveResource Tests``.tests
            ``ContextCreation Tests``.tests
            ``Vulkan Wrapper Tests``.tests
            ``IDictionary StructuralComparer Tests``.tests
        ]

    let runManuallyInMain = true

    if runManuallyInMain then
        runTestsSynchronously true allTests
    else
        runTestsWithCLIArgs [ CLIArguments.No_Spinner ] argv allTests
