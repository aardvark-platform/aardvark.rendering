
#r "paket: groupref Build //"
#load ".fake/build.fsx/intellisense.fsx"
#load @"paket-files/build/aardvark-platform/aardvark.fake/DefaultSetup.fsx"

open System
open System.IO
open System.Diagnostics
open Aardvark.Fake
open Fake.Core
open Fake.Tools
open Fake.IO.Globbing.Operators
open System.Runtime.InteropServices

//do MSBuildDefaults <- { MSBuildDefaults with Verbosity = Some Minimal }
do Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
    DefaultSetup.install ["src/Aardvark.Rendering.sln"]
else
    DefaultSetup.install ["src/Aardvark.Rendering.NonWindows.sln"]

#if DEBUG
do System.Diagnostics.Debugger.Launch() |> ignore
#endif



//Target.create "CreateAssemblyInfos" (fun _ ->
//    let projects = !!"src/**/*.fsproj" ++ "src/**/*.csproj"

//    let version = getGitTag()
//    let currentHash = Git.Information.getCurrentHash()

//    for p in projects do
//        let dir = Path.GetDirectoryName(p)
//        let template = Path.Combine(dir, "paket.template")
//        if File.Exists template then
//            let create, infoFile =
//                match Path.GetExtension(p) with 
//                    | ".csproj" -> CreateCSharpAssemblyInfo, "AssemblyInfo.cs"
//                    | _ -> CreateFSharpAssemblyInfo, "AssemblyInfo.fs"

//            let assDir = Path.Combine(dir, "Properties")

//            if not (Directory.Exists assDir) then
//                Directory.CreateDirectory assDir |> ignore

//            let ass = Path.Combine(assDir, infoFile)
            

//            let name = Path.GetFileNameWithoutExtension(p)


//            create ass [
//                Attribute.Title name
//                Attribute.Description "Aardvark Rendering"
//                Attribute.Version version
//                Attribute.Product "Aardvark.Rendering"
//                Attribute.FileVersion version
//                Attribute.Configuration (if Aardvark.Fake.Startup.config.debug then "Debug" else "Release")
//                Attribute.Metadata("githash", currentHash)
//                Attribute.Copyright "Aardvark Platform Team"
//            ]
//    ()
//)

//Target.create "SourceLink.Test" (fun _ ->
//    !! "bin/*.nupkg" 
//    |> Seq.iter (fun nupkg ->
//        Fake.Core.
//            (fun p -> { p with WorkingDir = __SOURCE_DIRECTORY__ @@ "src" @@ "Demo" @@ "SlimJim" } )
//            (sprintf "sourcelink test %s" nupkg)
//    )
//)


//Target.create "PerfTest" (fun _ ->
//    let exeFile = "bin/Release/perfTest.exe"
//    let exeModified =
//        if File.Exists exeFile then FileInfo(exeFile).LastWriteTime
//        else DateTime.MinValue

//    let sourceModified = FileInfo("perfTest.fsx").LastWriteTime

//    if sourceModified > exeModified then
        
//        let refs = 
//            List.map (fun p -> Path.Combine(Environment.CurrentDirectory, p))
//                [@"packages\FSharp.Charting\lib\net40\FSharp.Charting.dll"; @"packages\build\FAKE\tools\FakeLib.dll"]

//        Fsc.Compile 
//            (
//                (refs |> List.map FscHelper.FscParam.Reference) @
//                [
//                    FscHelper.FscParam.Out exeFile
//                    FscHelper.FscParam.Target FscHelper.TargetType.Exe
//                ]
//            )
//            ["perfTest.fsx"]

//        for r in refs do
//            let file = Path.GetFileName r
//            File.Copy(r, Path.Combine("bin", "Release", file), true)

//    else
//        tracefn "executable up-to-date"
//)

//"CreatePackage" ==> "SourceLink.Test"


//"Restore" ==> "PerfTest"


entry()
