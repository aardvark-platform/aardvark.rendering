module Program

open System
open Rendering.Examples

[<EntryPoint>]
[<STAThread>]
let main args =
    //Examples.Tutorial.run()
    //Examples.Instancing.run()
    //Examples.Render2TexturePrimitive.run()
    //Examples.Render2TextureComposable.run()
    //Examples.Render2TexturePrimiviteChangeableSize.run()
    //Examples.Render2TexturePrimitiveFloat.run()
    Examples.ComputeTest.run()
    //Examples.Shadows.run()
    //Examples.AssimpInterop.run() 
    //Examples.LoD.run()
    //Examples.Polygons.run()           attention: this one is currently broken due to package refactoring
    //Examples.TicTacToe.run()          attention: this one is currently broken due to package refactoring
    0
