module Program

open System
open Rendering.Examples

[<EntryPoint>]
[<STAThread>]
let main args =  
    //HelloWorld.run() |> ignore
    Examples.TicTacToe.run()
    //HelloWorld.testLoD()
    //HelloWorld.testGeometrySet ()
    //Examples.LoD.run()
    0
