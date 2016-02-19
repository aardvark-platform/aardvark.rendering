module Program

open System
open Rendering.Examples

[<EntryPoint>]
[<STAThread>]
let main args =  
    //HelloWorld.run() |> ignore

    //Examples.Tutorial.run()
    //Examples.Render2TexturePrimitive.run()
    //Examples.Render2TextureComposable.run()
    //Examples.Render2TexturePrimiviteChangeableSize.run()
    Examples.PostProcessing.run()
    //Examples.Shadows.run()
    //Examples.GeometrySet.run()
    //Examples.LoD.run()
    //Examples.TicTacToe.run()
    0
