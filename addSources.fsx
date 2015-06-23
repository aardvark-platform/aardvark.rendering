#if INTERACTIVE
#I @"packages/FAKE/tools/"
#I @"packages/Paket.Core/lib/net45"
#r @"Paket.Core.dll"
#r @"FakeLib.dll"
namespace AdditionalSources
module AdditionalSources =
#else
#endif

    open System.IO
    open System
    open System.Diagnostics
    open Paket
    open Fake
    open System.Text.RegularExpressions

    Logging.event.Publish.Subscribe (fun a -> 
        match a.Level with
            | TraceLevel.Error -> traceError a.Text
            | TraceLevel.Info ->  trace a.Text
            | TraceLevel.Warning -> traceImportant a.Text
            | TraceLevel.Verbose -> trace a.Text
            | _ -> ()
    ) |> ignore

    let packageNameRx = Regex @"(?<name>[a-zA-Z_0-9\.]+?)\.(?<version>([0-9]+\.)*[0-9]+)\.nupkg"

    let paketDependencies = Paket.Dependencies.Locate(__SOURCE_DIRECTORY__)

    let addSource folder = 

        let sourceFolders =
            if File.Exists "sources.references" then 
                File.ReadAllLines "sources.references" |> Set.ofArray
            else 
                Set.empty

        let newSourceFolders = Set.add folder sourceFolders

        File.WriteAllLines("sources.references", newSourceFolders)

    let removeSource folder =
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

        printfn "removing paket.lock. InstallSources automatically creates a new one."
        File.Delete "paket.lock"

    let installSources () =
        if File.Exists "paket.lock" |> not 
        then
            printfn "paket.lock is missing. Reinstalling paket sources."
            Paket.Dependencies.Locate().Install(true, false, false, false)

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