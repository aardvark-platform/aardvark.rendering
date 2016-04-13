// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.
open Aardvark.Rendering.Vulkan.Tests

[<EntryPoint>]
let main argv = 
    Aardvark.Base.Ag.initialize()
    ``Rendering Tests``.``[Vulkan] textures working``()
    0 // return an integer exit code
