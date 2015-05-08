#I @"packages/FAKE/tools/"
#r @"FakeLib.dll"

open Fake
open System
open System.IO

let core = !!"src/**/*.fsproj" ++ "src/**/*.csproj";

printfn "%A" (core |> Seq.toList)

let packageRx = System.Text.RegularExpressions.Regex @"(?<name>.*?)\.(?<version>([0-9]+\.)*[0-9]+)\.nupkg$"

let updatePackages (sources : list<string>) (projectFiles : #seq<string>) =
    let packages = 
        sources |> List.collect (fun source ->  
            Directory.GetFiles(source,"*.nupkg") 
                |> Array.choose (fun s -> 
                    let m = Path.GetFileName s |> packageRx.Match in if m.Success then m.Groups.["name"].Value |> Some else None
                ) 
                |> Array.toList
        ) |> Set.ofList

    if Set.count packages <> 0 then
        for project in projectFiles do
            project |> Fake.NuGet.Update.NugetUpdate (fun p ->  
                { p with 
                    Ids = packages |> Set.intersect (Set.ofList p.Ids) |> Set.toList
                    //ToolPath = @"E:\Development\aardvark-2015\tools\NuGet\nuget.exe"
                    RepositoryPath = "packages"
                    Sources = sources @ Fake.NuGet.Install.NugetInstallDefaults.Sources
                    //Prerelease = true
                } 
            )

Target "Restore" (fun () ->

    let packageConfigs = !!"src/**/packages.config" |> Seq.toList

    let addtionalSources = (environVarOrDefault "AdditionalNugetSources" "").Split([|";"|],StringSplitOptions.RemoveEmptyEntries) |> Array.toList
    let defaultNuGetSources = RestorePackageHelper.RestorePackageDefaults.Sources
    for pc in packageConfigs do
        RestorePackage (fun p -> { p with OutputPath = "packages"
                                          Sources = addtionalSources @ defaultNuGetSources  
                                 }) pc

    updatePackages addtionalSources  (!!"src/**/*.csproj" ++ "src/**/*.fsproj")
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

let knownPackages = 
    Set.ofList [
        "Aardvark.Base.Rendering"
    ]


Target "CreatePackage" (fun () ->
    let branch = Fake.Git.Information.getBranchName "."
    let releaseNotes = Fake.Git.Information.getCurrentHash()

    if branch = "master" then
        let tag = Fake.Git.Information.getLastTag()

        for id in knownPackages do
            NuGetPack (fun p -> 
                { p with OutputPath = "bin"; 
                         Version = tag; 
                         ReleaseNotes = releaseNotes; 
                         WorkingDir = "bin"
                         Dependencies = p.Dependencies |> List.map (fun (id,version) -> if Set.contains id knownPackages then (id, tag) else (id,version)) 
                }) (sprintf "bin/%s.nuspec" id)
    
    else 
        traceError (sprintf "cannot create package for branch: %A" branch)
)

Target "Deploy" (fun () ->

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
                    for id in knownPackages do
                        NuGetPublish (fun p -> 
                            { p with 
                                Project = id
                                OutputPath = "bin"
                                Version = tag; 
                                ReleaseNotes = releaseNotes; 
                                WorkingDir = "bin"
                                Dependencies = p.Dependencies |> List.map (fun (id,version) -> if Set.contains id knownPackages then (id, tag) else (id,version)) 
                                AccessKey = accessKey
                                Publish = true
                            })
                with e ->
                    ()
            | None ->
                ()
     else 
        traceError (sprintf "cannot deploy branch: %A" branch)
)

"Compile" ==> "CreatePackage"
"CreatePackage" ==> "Deploy"

// start build
RunTargetOrDefault "Default"

