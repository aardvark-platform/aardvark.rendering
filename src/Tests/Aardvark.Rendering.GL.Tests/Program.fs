open Aardvark.Rendering.GL.Tests

[<EntryPoint>]
let main args =
    Aardvark.Base.Ag.initialize()
    RenderingTests.``[GL] nested trafos``()
    0
