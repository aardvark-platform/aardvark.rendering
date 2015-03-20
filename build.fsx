#I @"packages/FAKE/tools/"
#r @"FakeLib.dll"

open Fake
open System
open System.IO

let core = ["src/Aardvark.Rendering.sln"];

module Std =
    open System.Runtime.InteropServices


    module private Kernel32 =
        let StdOutputHandle = 0xFFFFFFF5u

        [<DllImport("kernel32.dll")>]
        extern IntPtr GetStdHandle(UInt32 nStdHandle)

        [<DllImport("kernel32.dll")>]
        extern void SetStdHandle(UInt32 nStdHandle, IntPtr handle)

        let getstdout() = GetStdHandle(StdOutputHandle)
        let setstdout(h) = SetStdHandle(StdOutputHandle, h)

    module private Libc =
//        let STDOUT_FILENO = 1
//
//        [<DllImport("libc")>]
//        extern int dup(int oldfd)
//
//        [<DllImport("libc")>]
//        extern int dup2(int oldfd, int newfd)

        let getstdout() = 0n; //dup(STDOUT_FILENO) |> nativeint
        let setstdout(h : nativeint) = () //dup2(int h, STDOUT_FILENO) |> ignore

    let getstdout =
        match Environment.OSVersion.Platform with
            | PlatformID.Unix -> Libc.getstdout
            | _ -> Kernel32.getstdout

    let setstdout =
        match Environment.OSVersion.Platform with
            | PlatformID.Unix -> Libc.setstdout
            | _ -> Kernel32.setstdout

    let noOut (f : unit -> 'a) =
        let outHandle = getstdout()
        let out = Console.Out

        let success = 
            try
                setstdout(0n)
                Console.SetOut TextWriter.Null
                true
            with e ->
                false

        let res = f()

        if success then
            Console.SetOut out
            setstdout(outHandle)

        res

Target "Restore" (fun () ->

    let packageConfigs = !!"src/**/packages.config" |> Seq.toList

    let defaultNuGetSources = RestorePackageHelper.RestorePackageDefaults.Sources
    for pc in packageConfigs do
        RestorePackage (fun p -> { p with OutputPath = "packages" }) pc


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
        "Aardvark.Rendering"
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
                { p with OutputPath = "bin"; 
                         Version = tag; 
                         ReleaseNotes = releaseNotes; 
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

                                    Std.noOut (fun () ->
                                        RestorePackageId (fun p -> { p with Sources = [packageOutputPath; packagePath]; OutputPath = "packages"; }) p
                                    )
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

