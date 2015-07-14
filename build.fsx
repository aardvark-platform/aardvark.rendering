#I @"packages/FAKE/tools/"
#I @"packages/Aardvark.Build/lib/net45"
#I @"packages/Mono.Cecil/lib/net45"
#I @"packages/Paket.Core/lib/net45"
#r @"System.Xml.Linq"
#r @"FakeLib.dll"
#r @"Aardvark.Build.dll"
#r @"Mono.Cecil.dll"
#r @"Paket.Core.dll"

#load @"bin\addSources.fsx"


open Fake
open System
open System.IO
open Aardvark.Build
open System.Diagnostics
open System.Text.RegularExpressions

open AdditionalSources
 
do Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let packageNameRx = Regex @"(?<name>[a-zA-Z_0-9\.]+?)\.(?<version>([0-9]+\.)*[0-9]+)\.nupkg"
let core = ["src/Aardvark.Rendering.sln"];

Target "Install" (fun () ->
    AdditionalSources.paketDependencies.Install(false, false, false, true)
    AdditionalSources.installSources ()
)

Target "Restore" (fun () ->
    AdditionalSources.paketDependencies.Restore()
    AdditionalSources.installSources ()
)

Target "Update" (fun () ->
    AdditionalSources.paketDependencies.Update(false, false)
    AdditionalSources.installSources ()
)

Target "AddSource" (fun () ->
    let args = Environment.GetCommandLineArgs()
    let folders =
        if args.Length > 3 then
            Array.skip 3 args
        else
            failwith "no source folder given"

    AdditionalSources.addSources (Array.toList folders)
)

Target "RemoveSource" (fun () ->
    let args = Environment.GetCommandLineArgs()
    let folders =
        if args.Length > 3 then
            Array.skip 3 args
        else
            failwith "no source folder given"

    AdditionalSources.removeSources (Array.toList folders)
)


Target "Clean" (fun () ->
    DeleteDir (Path.Combine("bin", "Release"))
    DeleteDir (Path.Combine("bin", "Debug"))
)

Target "Compile" (fun () ->
    MSBuildRelease "bin/Release" "Build" core |> ignore
)

Target "Default" (fun () -> ())

"Restore" ==> 
    "Compile" ==>
    "Default"

Target "CopyGLVM" (fun () ->
    let arch =
        if Environment.Is64BitOperatingSystem then @"lib\Native\Aardvark.Rendering.GL\windows\AMD64"
        else @"lib\Native\Aardvark.Rendering.GL\windows\x86"

    let dir = DirectoryInfo(arch)
    let debug = DirectoryInfo(@"bin\Debug")
    let release = DirectoryInfo(@"bin\Release")
    
    debug.Create()
    release.Create()

    copyRecursive dir debug true |> ignore
    copyRecursive dir release true |> ignore
)

Target "InjectNativeDependencies" (fun () ->

    if Directory.Exists "bin/Debug" then
        File.Copy(@"packages/Aardvark.Build/lib/net45/Aardvark.Build.dll", @"bin/Debug/Aardvark.Build.dll", true)
    if Directory.Exists "bin/Release" then
        File.Copy(@"packages/Aardvark.Build/lib/net45/Aardvark.Build.dll", @"bin/Release/Aardvark.Build.dll", true)

    let dirs = Directory.GetDirectories "lib/Native"
    for d in dirs do
        let n = Path.GetFileName d
        let d = d |> Path.GetFullPath
        let paths = [
            Path.Combine("bin/Release", n + ".dll") |> Path.GetFullPath
            Path.Combine("bin/Release", n + ".exe") |> Path.GetFullPath
            Path.Combine("bin/Debug", n + ".dll") |> Path.GetFullPath
            Path.Combine("bin/Debug", n + ".exe") |> Path.GetFullPath
        ]

        let wd = Environment.CurrentDirectory
        try
        
            for p in paths do
                if File.Exists p then
                    Environment.CurrentDirectory <- Path.GetDirectoryName p
                    let ass = Mono.Cecil.AssemblyDefinition.ReadAssembly(p)
                    AssemblyInjector.addResources d ass
                    ass.Write p
                    tracefn "injected native stuff in %A" p
        finally
            Environment.CurrentDirectory <- wd

        ()

    ()
)

Target "CreatePackage" (fun () ->
    let releaseNotes = try Fake.Git.Information.getCurrentHash() |> Some with _ -> None
    if releaseNotes.IsNone then 
        //traceError "could not grab git status. Possible source: no git, not a git working copy"
        failwith "could not grab git status. Possible source: no git, not a git working copy"
    else 
        trace "git appears to work fine."
    
    let releaseNotes = releaseNotes.Value
    let branch = try Fake.Git.Information.getBranchName "." with e -> "master"

    let tag = Fake.Git.Information.getLastTag()
    AdditionalSources.paketDependencies.Pack("bin", version = tag, releaseNotes = releaseNotes)
)


Target "Push" (fun () ->
    let packages = !!"bin/*.nupkg"
    let packageNameRx = Regex @"(?<name>[a-zA-Z_0-9\.]+?)\.(?<version>([0-9]+\.)*[0-9]+)\.nupkg"
    let tag = Fake.Git.Information.getLastTag()

    let myPackages = 
        packages 
            |> Seq.choose (fun p ->
                let m = packageNameRx.Match (Path.GetFileName p)
                if m.Success then 
                    Some(m.Groups.["name"].Value)
                else
                    None
            )
            |> Set.ofSeq

    try
        for id in myPackages do
            let source = sprintf "bin/%s.%s.nupkg" id tag
            let target = sprintf @"\\hobel.ra1.vrvis.lan\NuGet\%s.%s.nupkg" id tag
            File.Copy(source, target, true)
    with e ->
        traceError (string e)
)


Target "Deploy" (fun () ->

    let packages = !!"bin/*.nupkg"
    

    let myPackages = 
        packages 
            |> Seq.choose (fun p ->
                let m = packageNameRx.Match (Path.GetFileName p)
                if m.Success then 
                    Some(m.Groups.["name"].Value)
                else
                    None
            )
            |> Set.ofSeq

    let accessKeyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "nuget.key")
    let accessKey =
        if File.Exists accessKeyPath then Some (File.ReadAllText accessKeyPath)
        else None

    let branch = Fake.Git.Information.getBranchName "."
    let releaseNotes = Fake.Git.Information.getCurrentHash()
    if branch = "master" then
        let tag = Fake.Git.Information.getLastTag()
        match accessKey with
            | Some accessKey ->
                try
                    for id in myPackages do
                        Paket.Dependencies.Push(sprintf "bin/%s.%s.nupkg" id tag, apiKey = accessKey)
                with e ->
                    traceError (string e)
            | None ->
                traceError (sprintf "Could not find nuget access key")
     else 
        traceError (sprintf "cannot deploy branch: %A" branch)
)

"CopyGLVM" ==> "Compile"
"Compile" ==> "InjectNativeDependencies" ==> "CreatePackage"
"CreatePackage" ==> "Deploy"
"CreatePackage" ==> "Push"

// start build
RunTargetOrDefault "Default"
