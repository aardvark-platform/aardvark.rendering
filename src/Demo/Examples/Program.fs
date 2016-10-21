module Program

open System
open Rendering.Examples

[<EntryPoint>]
[<STAThread>]
let main args =
    // Aardvark.Base.DynamicLinker.tryLoadLibrary "nvapi64.dll" |> ignore
    //Vulkan.Lod.run()  
    //Vulkan.Simple.run()  

    //Playground.run() |> ignore // former hacked helloWorld example
    //HelloWorld.run() |> ignore
    //NullBufferTest.run() |> ignore

    Examples.Tutorial.run()
    //Examples.Render2TexturePrimitive.run()
    //Examples.Render2TextureComposable.run()
    //Examples.Render2TexturePrimiviteChangeableSize.run()
    //Examples.Render2TexturePrimitiveFloat.run()
    //Examples.PostProcessing.run()
    //Examples.Shadows.run()
    //Examples.Maya.run()
    //Examples.GeometrySet.run()
    //Examples.LoD.run()
    //Examples.Polygons.run()           attention: this one is currently broken due to package refactoring
    //Examples.TicTacToe.run()          attention: this one is currently broken due to package refactoring
    //Examples.AssimpInterop.run()
    //Examples.Shadows.run()
    0
