#load @"paket-files/build/vrvis/Aardvark.Fake/DefaultSetup.fsx"

open Fake
open System
open System.IO
open System.Diagnostics
open Aardvark.Fake


do Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

DefaultSetup.install ["src/Aardvark.Rendering.sln"]

Target "Aardvark.Interactive.GL" (fun () ->
    let (!!) str = Path.Combine("bin", "Release", str)
    ILMerge (fun p ->
        { p with
            DebugInfo = false
            XmlDocs = false
            ToolPath = Path.Combine("Packages", "build", "ilmerge", "tools", "ILMerge.exe")
            Libraries = 
                [
                    !!"Aardvark.Application.dll"
                    !!"Aardvark.Application.WinForms.dll"
                    !!"Aardvark.Base.dll"
                    !!"Aardvark.Base.Essentials.dll"
                    !!"Aardvark.Base.FSharp.dll"
                    !!"Aardvark.Base.Incremental.dll"
                    !!"Aardvark.Base.Rendering.dll"
                    !!"Aardvark.Base.TypeProviders.dll"
                    !!"Aardvark.Rendering.GL.dll"
                    !!"DevILSharp.dll"
                    !!"OpenTK.dll"
                    !!"OpenTK.Compatibility.dll"
                    !!"OpenTK.GLControl.dll"
                ]
        }
    ) !!"Aardvark.Interactive.GL.dll" !!"Aardvark.Application.WinForms.GL.dll"
)

#if DEBUG
do System.Diagnostics.Debugger.Launch() |> ignore
#endif


entry()
