#I @"packages/FAKE/tools/"
#r @"FakeLib.dll"

open Fake
open System
open System.IO

let core = ["src/Aardvark.Rendering.sln"];

module Std =
    open System.Runtime.InteropServices

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

let ownPackages = 
    Set.ofList [
        "Aardvark.Base.Rendering"
        "Aardvark.SceneGraph"
        "Aardvark.Rendering.GL"
        "Aardvark.Application"
        "Aardvark.Application.WinForms.GL"
        "Aardvark.Application.WPF.GL"
        
    ]

let subModulePackages =
    Map.ofList [
        "src/Aardvark.Base", [
            "Aardvark.Base"
            "Aardvark.Base.FSharp"
            "Aardvark.Base.Essentials"
            "Aardvark.Base.Incremental"
        ]
    ]


Target "CreatePackage" (fun () ->
    let branch = Fake.Git.Information.getBranchName "."
    let releaseNotes = Fake.Git.Information.getCurrentHash()

    if branch = "master" then
        let tag = Fake.Git.Information.getLastTag()

        for id in ownPackages do
            NuGetPack (fun p -> 
                { p with OutputPath = "bin"
                         Version = tag
                         ReleaseNotes = releaseNotes
                         WorkingDir = "bin"
                         Dependencies = p.Dependencies |> List.map (fun (id,version) -> if Set.contains id ownPackages then (id, tag) else (id,version)) 
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
                    for id in ownPackages do
                        NuGetPublish (fun p -> 
                            { p with 
                                Project = id
                                OutputPath = "bin"
                                Version = tag; 
                                ReleaseNotes = releaseNotes; 
                                WorkingDir = "bin"
                                Dependencies = p.Dependencies |> List.map (fun (id,version) -> if Set.contains id ownPackages then (id, tag) else (id,version)) 
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

// installs local packages specified by subModulePackages 
// NOTE: current packages will always be replaced
Target "InstallLocal" (fun () ->

    let buildCmdName =
        match Environment.OSVersion.Platform with
            | PlatformID.Unix 
            | PlatformID.MacOSX -> "build.sh"
            | _ -> "build.cmd"

    for (localModulePath, packages) in Map.toSeq subModulePackages do
        let modulePath = Path.GetFullPath localModulePath
        if Directory.Exists modulePath then

            let rec findPackagePath l =
                match l with
                    | [] -> None
                    | p::ps -> 
                        let p = Path.Combine(modulePath, p)
                        if Directory.Exists p then Some p
                        else findPackagePath ps

            let buildCmd = Path.Combine(modulePath, buildCmdName)

            let packageOutputPath = findPackagePath ["bin"; "build"]

            match packageOutputPath with
                | Some packageOutputPath ->
                    if File.Exists buildCmd then

                        //Git.Branches.checkout modulePath false "master"


                        let ret = 
                            ExecProcess (fun info -> 
                                info.UseShellExecute <- true
                                info.CreateNoWindow <- false
                                info.FileName <- buildCmd
                                info.Arguments <- "CreatePackage"
                                info.WorkingDirectory <- modulePath
                            ) TimeSpan.MaxValue


                        if ret = 0 then
                            let packagePath = Path.Combine(modulePath, "packages")

                            for p in packages do
                                try
                                    let outputFolders = Directory.GetDirectories("packages", p + ".*")
                                    for outputFolder in outputFolders do
                                        Directory.Delete(outputFolder, true)

                                    RestorePackageId (fun p -> { p with Sources = [packageOutputPath; packagePath]; OutputPath = "packages"; }) p
                                    trace (sprintf "successfully reinstalled %A" p)

                                with :? UnauthorizedAccessException as e ->
                                    traceImportant (sprintf "could not reinstall %A" p  )


                        else
                            traceError (sprintf "build failed for submodule: %A" localModulePath)
                    else
                        traceError (sprintf "could not locate build.cmd in submodule %A" localModulePath)
                | _ ->
                    traceError (sprintf "could not locate output folder in submodule %A" localModulePath)
        else
            trace (sprintf "could not locate submodule %A" localModulePath)

    ()
)


"Compile" ==> "CreatePackage"
"CreatePackage" ==> "Deploy"

// start build
RunTargetOrDefault "Default"

