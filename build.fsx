#I @"packages/FAKE/tools/"
#I @"packages/Aardvark.Build/lib/net45"
#I @"packages/Mono.Cecil/lib/net45"
#I @"packages/Paket.Core/lib/net45"
#r @"System.Xml.Linq"
#r @"FakeLib.dll"
#r @"Aardvark.Build.dll"
#r @"Mono.Cecil.dll"
#r @"Paket.Core.dll"

#load "addSources.fsx"


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

Target "Restore" (fun () ->
    AdditionalSources.paketDependencies.Restore()
)


Target "AddSource" (fun () ->
    let args = Environment.GetCommandLineArgs()
    let folder =
        match args with
            | [|_;_;_;a|] -> a
            | _ -> failwith "no source folder given"

    tracefn "%A" folder
    AdditionalSources.addSource folder
)

Target "RemoveSource" (fun () ->
    let args = Environment.GetCommandLineArgs()
    let folder =
        match args with
            | [|_;_;_;a|] -> a
            | _ -> failwith "no source folder given"

    tracefn "%A" folder

    AdditionalSources.removeSource folder
)


Target "InstallSources" (fun () ->
    AdditionalSources.installSources ()
)


Target "Clean" (fun () ->
    DeleteDir (Path.Combine("bin", "Release"))
    DeleteDir (Path.Combine("bin", "Debug"))
)

Target "Compile" (fun () ->
    MSBuildRelease "bin/Release" "Build" core |> ignore
)

Target "Inject" (fun () ->
    ()
)

Target "Default" (fun () -> ())

"Restore" ==> 
    "Compile" ==>
    "Default"


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


"Compile" ==> "CreatePackage"
"CreatePackage" ==> "Deploy"

// start build
//RunTargetOrDefault "Default"
RunTargetOrDefault "InstallSources"
