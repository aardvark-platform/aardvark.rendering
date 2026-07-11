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

            ``Heap Gauntlet``.tests

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

    // run everything synchronously when invoked with no args (the default / CI path);
    // when args ARE given, honour Expecto's CLI so e.g. `--filter-test-list "Heap uniforms"`
    // runs just that subtree (otherwise argv was silently ignored).
    if Array.isEmpty argv then
        runTestsSynchronously true allTests
    else
        runTestsWithCLIArgs [ CLIArguments.No_Spinner ] argv allTests
