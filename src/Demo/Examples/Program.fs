module Program

open System
open Rendering.Examples

[<EntryPoint>]
[<STAThread>]
let main args =  
    //HelloWorld.testLoD()
    //HelloWorld.run()
    //HelloWorld.testGeometrySet ()
    Examples.LoD.run()
    0
