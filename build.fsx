#load @"paket-files/build/vrvis/Aardvark.Fake/DefaultSetup.fsx"

open Fake
open System
open System.IO
open System.Diagnostics
open Aardvark.Fake

//do MSBuildDefaults <- { MSBuildDefaults with Verbosity = Some Minimal }
do Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

DefaultSetup.install ["src/Aardvark.Rendering.sln"]

#if DEBUG
do System.Diagnostics.Debugger.Launch() |> ignore
#endif

Target "PerfTest" (fun () ->
    let exeFile = "bin/Release/perfTest.exe"
    let exeModified =
        if File.Exists exeFile then FileInfo(exeFile).LastWriteTime
        else DateTime.MinValue

    let sourceModified = FileInfo("perfTest.fsx").LastWriteTime

    if sourceModified > exeModified then
        
        let refs = 
            List.map (fun p -> Path.Combine(Environment.CurrentDirectory, p))
                [@"packages\FSharp.Charting\lib\net40\FSharp.Charting.dll"; @"packages\build\FAKE\tools\FakeLib.dll"]

        FscHelper.Compile 
            (
                (refs |> List.map FscHelper.FscParam.Reference) @
                [
                    FscHelper.FscParam.Out exeFile
                    FscHelper.FscParam.Target FscHelper.TargetType.Exe
                ]
            )
            ["perfTest.fsx"]

        for r in refs do
            let file = Path.GetFileName r
            File.Copy(r, Path.Combine("bin", "Release", file), true)

    else
        tracefn "executable up-to-date"
)



"Restore" ==> "PerfTest"


entry()
