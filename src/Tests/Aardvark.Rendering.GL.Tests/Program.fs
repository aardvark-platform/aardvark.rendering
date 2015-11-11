open Aardvark.Rendering.GL.Tests

[<EntryPoint>]
let main args =
    Aardvark.Base.Ag.initialize()
    RenderingTests.``[GL] simple render to multiple texture``()
    0
