#load @"paket-files/build/vrvis/Aardvark.Fake/DefaultSetup.fsx"

open Fake
open System
open System.IO
open System.Diagnostics
open Aardvark.Fake



do Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

DefaultSetup.install ["src/Aardvark.Rendering.sln"]

#if DEBUG
do System.Diagnostics.Debugger.Launch() |> ignore
#endif

Target "PerfTest" (fun () ->
    
    let exeModified =
        if File.Exists "perfTest.exe" then FileInfo("perfTest.exe").LastWriteTime
        else DateTime.MinValue

    let sourceModified = FileInfo("perfTest.fsx").LastWriteTime

    if sourceModified > exeModified then
        
        FscHelper.Compile [FscHelper.FscParam.Standalone; FscHelper.FscParam.Reference "packages/FSharp.Charting/lib/net40/FSharp.Charting.dll"] ["perfTest.fsx"]
    else
        tracefn "executable up-to-date"
)



"Restore" ==> "PerfTest"


entry()
