open Aardvark.Rendering.GL.Tests

[<EntryPoint>]
let main args =
    Aardvark.Base.Ag.initialize()
    //RenderingTests.``[GL] concurrent group change``()
    //RenderingTests.``[GL] memory leak test``()
    //MultipleStageAgMemoryLeakTest.run() |> ignore

    //PerformanceTests.PerformanceTest.runConsole()
    //Examples.PerformanceTest.run()
    //PerformanceTests.StartupPerformance.runConsole args
    //PerformanceTests.IsActiveFlagPerformance.run args
    UseTest.bla()
    0
