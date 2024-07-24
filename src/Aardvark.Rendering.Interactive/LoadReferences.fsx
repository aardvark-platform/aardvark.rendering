#r "Aardvark.Base.dll"
#r "Aardvark.Base.TypeProviders.dll"
#r "Aardvark.Base.Essentials.dll"
#r "Aardvark.Base.FSharp.dll"
#r "Aardvark.Base.Incremental.dll"
#r "Aardvark.Rendering.dll"
#r "FShade.dll"
#r "FShade.Compiler.dll"
#r "Aardvark.SceneGraph.dll"
#r "Aardvark.Rendering.NanoVg.dll"
#r "Aardvark.Rendering.GL.dll"
#r "Aardvark.Application.dll"
#r "Aardvark.Application.WinForms.dll"
#r "Aardvark.Application.WinForms.GL.dll"
#r "System.Reactive.Core.dll"
#r "Aardvark.Rendering.Interactive.dll"
#r "Aardvark.SceneGraph.Assimp.dll"

namespace Aardvark.Rendering.Interactive

[<AutoOpen>]
module Dirs = 
    let BinDirectory =  __SOURCE_DIRECTORY__
