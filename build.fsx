#I @"packages/FAKE/tools/"
#I @"packages/Aardvark.Build/lib/net45"
#I @"packages/Mono.Cecil/lib/net45"
#I @"packages/Paket.Core/lib/net45"
#r @"System.Xml.Linq"
#r @"FakeLib.dll"
#r @"Aardvark.Build.dll"
#r @"Mono.Cecil.dll"
#r @"Paket.Core.dll"


open Fake
open System
open System.IO
open Aardvark.Build
open System.Diagnostics
open System.Text.RegularExpressions

do Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let packageNameRx = Regex @"(?<name>[a-zA-Z_0-9\.]+?)\.(?<version>([0-9]+\.)*[0-9]+)\.nupkg"
let core = ["src/Aardvark.Rendering.sln"];
let paketDependencies = Paket.Dependencies.Locate(__SOURCE_DIRECTORY__)


Paket.Logging.event.Publish.Subscribe (fun a -> 
    match a.Level with
        | TraceLevel.Error -> traceError a.Text
        | TraceLevel.Info -> trace a.Text
        | TraceLevel.Warning -> traceImportant a.Text
        | TraceLevel.Verbose -> trace a.Text
        | _ -> ()
) |> ignore

Target "Restore" (fun () ->
    paketDependencies.Restore()
)


Target "AddSource" (fun () ->
    let args = Environment.GetCommandLineArgs()
    let folder =
        match args with
            | [|_;_;_;a|] -> a
            | _ -> failwith "no source folder given"

    tracefn "%A" folder

    let sourceFolders =
        if File.Exists "sources.references" then 
            File.ReadAllLines "sources.references" |> Set.ofArray
        else 
            Set.empty

    let newSourceFolders = Set.add folder sourceFolders

    File.WriteAllLines("sources.references", newSourceFolders)
)

Target "RemoveSource" (fun () ->
    let args = Environment.GetCommandLineArgs()
    let folder =
        match args with
            | [|_;_;_;a|] -> a
            | _ -> failwith "no source folder given"

    tracefn "%A" folder

    let sourceFolders =
        if File.Exists "sources.references" then 
            File.ReadAllLines "sources.references" |> Set.ofArray
        else 
            Set.empty

    let newSourceFolders = Set.remove folder sourceFolders

    if Set.isEmpty newSourceFolders then
        if File.Exists "sources.references" then
            File.Delete "sources.references"
    else
        File.WriteAllLines("sources.references", newSourceFolders)
)


Target "InstallSources" (fun () ->
    let sourceLines =
        if File.Exists "sources.references" then 
            File.ReadAllLines "sources.references" |> Array.toList
        else 
            []

    let buildSourceFolder (folder : string) : Set<string> =
        let code = shellExec { CommandLine = "/C build.cmd CreatePackage"; Program = "cmd.exe"; WorkingDirectory = folder; Args = [] }
        if code <> 0 then
            failwith "failed to build: %A" folder
        else
            let binPath = Path.Combine(folder, "bin", "*.nupkg")
            !!binPath 
                |> Seq.choose (fun str ->
                    let m = packageNameRx.Match str
                    if m.Success then
                        Some m.Groups.["name"].Value
                    else
                        None
                    )
                |> Set.ofSeq


    let sourcePackages = sourceLines |> List.map (fun f -> f, buildSourceFolder f) |> Map.ofList
    let installedPackages = paketDependencies.GetInstalledPackages() |> List.map fst |> Set.ofList

    let reinstallPackages = Set.intersect installedPackages (sourcePackages |> Map.toSeq |> Seq.map snd |> Set.unionMany)
    let tempFile = Path.GetTempFileName() + ".references"


    let packageSources = sourcePackages |> Map.toSeq |> Seq.collect (fun (a,b) -> b |> Seq.map (fun b -> (Paket.Domain.NormalizedPackageName (Paket.Domain.PackageName b),a))) |> Map.ofSeq

    for p in reinstallPackages do
        Directory.Delete(Path.Combine("packages", p), true)

    let fixLockFile (folders : list<string>) =
        let lockFile = Paket.LockFile.LoadFrom("paket.lock")

        let newPackages =
            lockFile.ResolvedPackages
                |> Map.map (fun k v ->
                    match Map.tryFind k packageSources with
                        | Some folder ->
                            tracefn "patching package: %A" k
                            { v with Source = Paket.PackageSources.PackageSource.LocalNuget folder }
                        | None ->
                            v
                )


        let test = Paket.LockFile.Create("paket.lock", lockFile.Options, Paket.PackageResolver.Resolution.Ok newPackages, lockFile.SourceFiles)
        test.Save()
        ()

    fixLockFile sourceLines

    paketDependencies.Restore()
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
    paketDependencies.Pack("bin", version = tag, releaseNotes = releaseNotes)
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
RunTargetOrDefault "Default"

