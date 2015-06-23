#if INTERACTIVE
#I @"../packages/FAKE/tools/"
#I @"../packages/Paket.Core/lib/net45"
#r @"Paket.Core.dll"
#r @"FakeLib.dll"
//do System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
namespace AdditionalSources
#else
#endif

module AdditionalSources =

    open System.IO
    open System
    open System.Diagnostics
    open Paket
    open Fake
    open System.Text.RegularExpressions
    open System.IO.Compression
    open System.Security.Cryptography
    open System.Text

    Logging.event.Publish.Subscribe (fun a -> 
        match a.Level with
            | TraceLevel.Error -> traceError a.Text
            | TraceLevel.Info ->  trace a.Text
            | TraceLevel.Warning -> traceImportant a.Text
            | TraceLevel.Verbose -> trace a.Text
            | _ -> ()
    ) |> ignore

    let packageNameRx = Regex @"(?<name>[a-zA-Z_0-9\.]+?)\.(?<version>([0-9]+\.)*[0-9]+)\.nupkg"
    let idRegex = Regex @"^id[ \t]+(?<id>.*)$"
    let versionRx = Regex @"(?<version>([0-9]+\.)*[0-9]+).*"
    
    // a hash based on the current path
    let cacheFile = Path.Combine(Path.GetTempPath(), Convert.ToBase64String(MD5.Create().ComputeHash(UnicodeEncoding.Unicode.GetBytes(Environment.CurrentDirectory))))
    let paketDependencies = Paket.Dependencies.Locate()

    let tryReadPackageId (file : string) =
        let lines = file |> File.ReadAllLines
        
        lines |> Array.tryPick (fun l ->
            let m = idRegex.Match l
            if m.Success then Some m.Groups.["id"].Value
            else None
        )

    let findCreatedPackages (folder : string) =
        let files = !!Path.Combine(folder, "**", "paket.template") |> Seq.toList
        let ids = files |> List.choose tryReadPackageId
        let tag = 
            try Git.Information.describe folder
            with _ -> ""

        let m = versionRx.Match tag
        if m.Success then
            Some (m.Groups.["version"].Value, ids)
        else
            None

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

        tracefn "removing paket.lock. InstallSources automatically creates a new one."
        File.Delete "paket.lock"

    let installPackage (pkgFile : string) =
        let m = pkgFile |> Path.GetFileName |> packageNameRx.Match
        if m.Success then
            let id = m.Groups.["name"].Value
            let outputFolder = Path.Combine("packages", id) |> Path.GetFullPath
            
            if not <| Directory.Exists outputFolder then
                Directory.CreateDirectory outputFolder |> ignore

            Unzip outputFolder pkgFile
            File.Copy(pkgFile, Path.Combine(outputFolder, Path.GetFileName pkgFile), true)
            true
        else
            false

    let latestModificationDate (folder : string) =
        let file = DirectoryInfo(folder).GetFileSystemInfos("*", SearchOption.AllDirectories) |> Seq.maxBy (fun fi -> fi.LastWriteTime)
        file.LastWriteTime

    let installSources () =
        if not <| File.Exists "paket.lock" then
            tracefn "paket.lock is missing. Reinstalling paket sources."
            Paket.Dependencies.Locate().Install(true, false, false, false)

        let sourceLines =
            if File.Exists "sources.references" then 
                File.ReadAllLines "sources.references" |> Array.toList
            else 
                []

        let cacheTimes = 
            if File.Exists cacheFile then 
                cacheFile |> File.ReadAllLines |> Array.choose (fun str -> match str.Split [|';'|] with [|a;b|] -> Some (a,DateTime(b |> int64)) | _ -> None) |> Map.ofArray |> ref
            else
                Map.empty |> ref

        let buildSourceFolder (folder : string) : Map<string, Version> =
            let cacheTime =
                match Map.tryFind folder !cacheTimes with
                    | Some t -> t
                    | None -> DateTime.MinValue

            let modTime = latestModificationDate folder

            let code = 
                if modTime > cacheTime then
                    shellExec { CommandLine = "/C build.cmd CreatePackage"; Program = "cmd.exe"; WorkingDirectory = folder; Args = [] }
                else
                    0

            if code <> 0 then
                failwith "failed to build: %A" folder
            else
                cacheTimes := Map.add folder DateTime.Now !cacheTimes
                let binPath = Path.Combine(folder, "bin", "*.nupkg")
                !!binPath 
                    |> Seq.choose (fun str ->
                        let m = packageNameRx.Match str
                        if m.Success then
                            Some (m.Groups.["name"].Value, Version.Parse m.Groups.["version"].Value)
                        else
                            None
                        )
                    |> Seq.groupBy fst
                    |> Seq.map (fun (id,versions) -> (id, versions |> Seq.map snd |> Seq.max))
                    |> Map.ofSeq

        let sourcePackages = sourceLines |> List.map (fun f -> f, buildSourceFolder f) |> Map.ofList
        let installedPackages = paketDependencies.GetInstalledPackages() |> List.map fst |> Set.ofList


        for (source, packages) in Map.toSeq sourcePackages do
            for (id, version) in Map.toSeq packages do
                let fileName = sprintf "%s.%s.nupkg" id (string version)
                let path = Path.Combine(source, "bin", fileName)
                let installPath = Path.Combine("packages", id)

                if Directory.Exists installPath then
                    Directory.Delete(installPath, true)

                if installPackage path then
                    tracefn "reinstalled %A" id
                else
                    traceError <| sprintf "failed to reinstall: %A" id

        tracefn "%A" cacheFile
        File.WriteAllLines(cacheFile, !cacheTimes |> Map.toSeq |> Seq.map (fun (a, time) -> sprintf "%s;%d" a time.Ticks))
        