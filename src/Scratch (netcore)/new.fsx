#r "System.Xml.dll"
#r "System.Xml.Linq.dll"

open System
open System.IO
open System.Text.RegularExpressions

type Sdk = Core | Framework

type ProjectKind =
    | Folder
    | FSharp of Sdk
    | CSharp of Sdk
    | Unknown of Guid

type ProjectSection =
    {
        name : string
        kind : string
        associations : list<string * string>
    }

type Project =
    {
        kind : ProjectKind
        guid : Guid
        name : string
        path : string
        sections : list<ProjectSection>
    }

type Configuration =
    {
        name : string
        platform : string
    }


type Value =
    | Unknown of string
    | Config of config : string * platform : string
    | Project of Guid
    | Nested of Value * Value

type Solution =
    {
        directory : string
        version : Version
        minVerson : Version
        projects : list<Project>
        globalSections : Map<string, string * list<Value * Value>>
    }
    
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Solution =
    [<AutoOpen>]
    module private Parser = 
        type Parser<'a> = { parse : string -> Option<'a * string> }

        let pRegex (pattern : string) =
            let r = Regex pattern
            { parse = fun (c : string) ->
                let m = r.Match c 
                if m.Success then
                    Some (m, c.Substring(m.Index + m.Length))
                else
                    None
            }
          
        let pString (p : string) =
            { parse = fun str ->
                if str.StartsWith p then Some(p, str.Substring(p.Length))
                else None
            }

        let pMap (f : 'a -> 'b) (p : Parser<'a>) =
            { parse = fun str ->
                match p.parse str with
                    | Some (res, str) -> Some(f res, str)
                    | None -> None   
            }  

        let pIgnore p = pMap ignore p

        let (||>) (p : Parser<'a>) (f : 'a -> 'b) =
            pMap f p
        
        let (<||) (f : 'a -> 'b) (p : Parser<'a>) =
            pMap f p

        let pConcat (l : Parser<'a>) (r : Parser<'b>) =
            { parse = fun str ->
                match l.parse str with
                    | Some (lv, str) ->
                        match r.parse str with
                            | Some (rv, str) ->
                                Some ((lv,rv), str)
                            | None ->
                                None
                    | None ->
                        None
            }

        let pOr (l : Parser<'a>) (r : Parser<'a>) =
            { parse = fun str ->
                match l.parse str with
                    | Some (v, str) -> Some(v, str)
                    | None ->
                        match r.parse str with
                            | Some(v, str) -> Some (v, str)
                            | None -> None
            }
        
        let (<|>) (l : Parser<'a>) (r : Parser<'a>) = pOr l r
        let (.*.) (l : Parser<'a>) (r : Parser<'b>) = pConcat l r
        let ( *.) (l : Parser<'a>) (r : Parser<'b>) = pConcat l r |> pMap snd
        let ( .*) (l : Parser<'a>) (r : Parser<'b>) = pConcat l r |> pMap fst

        let pChoice (parsers : list<Parser<'a>>) =
            { parse = fun str ->
                parsers |> List.tryPick (fun p -> p.parse str)
            }

        let pEmpty v = { parse = fun str -> Some(v, str) }

        let rec pMany (p : Parser<'a>) =
            { parse = fun str ->
                match p.parse str with
                    | Some (v, str) ->
                        match pMany(p).parse str with
                            | Some (r, str) -> Some (v::r, str)
                            | None -> Some([v], str)
                    | None ->
                        Some([], str)
            }

        let pMany1 (p : Parser<'a>) =
            p .*. pMany p |> pMap (fun (h,t) -> h::t)

    [<AutoOpen>]
    module private Helpers = 

        let folderKind = Guid.Parse "2150E333-8FDC-42A3-9474-1A3956D46DE8"
        let fsharpKind = Guid.Parse "F2A71F9B-5D33-465A-A702-920D77279786"
        let csharpKind =   Guid.Parse "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC"
        let csharpDotNet = Guid.Parse "9A19103F-16F7-4668-BE54-9A1E7A4F7556"
        let fsharpDotNet = Guid.Parse "6EC3EE1D-3C4E-46DD-8F32-0CC8E7565705"
        let guidPattern = @"[a-fA-F0-9\-]+"

        let toKind (g : Guid) =
            if g = folderKind then Folder
            elif g = fsharpKind then FSharp Sdk.Framework
            elif g = csharpKind then CSharp Sdk.Framework
            elif g = fsharpDotNet then FSharp Sdk.Core
            else ProjectKind.Unknown g

        let pVersion = 
            pRegex @"^[\r\n \t]*Microsoft[ \t]*Visual[ \t]*Studio[ \t]*Solution[ \t]*File[ \t]*,[ \t]*Format[ \t]*Version[ \t]*(?<version>[0-9\.]+)[ \t]*\r\n"
                |> pMap (fun m -> m.Groups.["version"].Value)

        let pVisualStudioVersion =
            pRegex "^[\r\n]*VisualStudioVersion[ \t]*\=[ \t]*(?<version>.*)"
                |> pMap (fun m -> m.Groups.["version"].Value)
                
        let pMinimumVisualStudioVersion =
            pRegex "^[\r\n]*MinimumVisualStudioVersion[ \t]*\=[ \t]*(?<version>.*)"
                |> pMap (fun m -> m.Groups.["version"].Value)

        let pStartProject = 
            pRegex ("^[\r\n]*Project\(\"\{(?<kind>" + guidPattern + ")\}\"\)[ \t]*\=[ \t]*\"(?<name>[^\"]+)\"[ \t]*,[ \t]*\"(?<path>[^\"]+)\"[ \t]*,[ \t]*\"\{(?<guid>" + guidPattern + ")\}\"[ \t]*")
                |> pMap (fun m ->
                    match Guid.TryParse m.Groups.["kind"].Value, Guid.TryParse m.Groups.["guid"].Value with
                        | (true, kind), (true, guid) ->
                            let p = { name = m.Groups.["name"].Value; kind = toKind kind; path = m.Groups.["path"].Value; guid = guid; sections = [] }
                            p
                        | _ ->
                            failwith "invalid GUID"
                )   

        let pEndProject =
            pRegex "^[\r\n]*EndProject"
                |> pMap ignore

        let pComment =
            pRegex @"^[\r\n]*#(?<comment>.*)"
                |> pMap (fun m -> None)

        let pStartSection = 
            pRegex @"^[\r\n]*[ \t]*ProjectSection\((?<name>.*?)\)[ \t]*\=[ \t]*(?<kind>.*)"
                |> pMap (fun m ->
                    let name = m.Groups.["name"].Value
                    let kind = m.Groups.["kind"].Value
                    name, kind
                )

        let pFileAssoc =
            pRegex @"^[\r\n]*[ \t]*(?<name>.*)[ \t]*\=[ \t]*(?<path>[^\r\n]*)"
                |> pMap (fun m ->
                    m.Groups.["name"].Value, m.Groups.["path"].Value
                )

        let pEndSection =
            pRegex @"^[\r\n]*[ \t]*EndProjectSection"
                |> pMap ignore


        let pSection =
            pStartSection .*.
            pMany pFileAssoc .*
            pEndSection
            |> pMap (fun ((n,k), assoc) -> { name = n; kind = k; associations = assoc })

        let pProject =
            pStartProject .*.
            pMany (pChoice [ pComment; pSection ||> Some ]) .*
            pEndProject
            |> pMap (fun (p, sections) -> { p with sections = List.choose id sections })

        let pStartGlobal =
            pRegex "^[\r\n]*Global" |> pIgnore

        let pEndGlobal =
            pRegex "^[\r\n]*EndGlobal" |> pIgnore

        let pStartGlobalSection =
            pRegex "^[\r\n]*[ \t]*GlobalSection\((?<name>.*)\)[ \t]*\=[ \t]*(?<kind>(preSolution|postSolution))"
                |> pMap (fun m ->
                    m.Groups.["name"].Value, m.Groups.["kind"].Value
                )

        let pEndGlobalSection =
            pRegex "^[\r\n]*[ \t]*EndGlobalSection" |> pIgnore


        let pConfig =
            pRegex @"^(?<config>[^\|\r\n]*)[ \t]*\|[ \t]*(?<platform>[^\=\r\n]*)"
                |> pMap (fun m ->
                    Value.Config(m.Groups.["config"].Value.Trim(), m.Groups.["platform"].Value.Trim())
                )
        
        let pGuid = 
            pRegex (@"^\{(?<guid>" + guidPattern + @")\}")
                |> pMap (fun m -> Guid.Parse m.Groups.["guid"].Value)

        let pProjectConfig =
            pGuid .* pString "." .*. pConfig |> pMap (fun (g,c) -> Value.Nested(Value.Project g, c))

        let pGuidValue =
            pGuid |> pMap (fun g -> Value.Project g)

        let pAnyValue =
            pRegex "^(?<value>[^\=\r\n]*)" |> pMap (fun m -> Value.Unknown m.Groups.["value"].Value)

        let pAssoc =
            pRegex @"^[\r\n]*[ \t]*" *.
            pChoice [ pProjectConfig; pGuidValue; pConfig; pAnyValue ] .*
            pRegex @"^[ \t]*\=[ \t]*" .*.
            pChoice [ pConfig; pGuidValue; pAnyValue ]

        let pGlobalSection =
            pStartGlobalSection .*.
            pMany pAssoc .*
            pEndGlobalSection
            |> pMap (fun ((name,kind),a) -> (name, kind, a))

        let pGlobal =
            pStartGlobal *.
            pMany pGlobalSection .*
            pEndGlobal

        let pSln =
            pVersion *.
            pComment *.
            pVisualStudioVersion .*.
            pMinimumVisualStudioVersion .*.
            pMany pProject .*.
            pGlobal .*
            pRegex "[ \t\r\n]*"

            |> pMap (fun (((v, mv), ps), g) -> 
                {
                    directory = ""
                    version = Version.Parse v
                    minVerson = Version.Parse mv
                    projects = ps
                    globalSections = g |> List.map (fun (k,n,a) -> k, (n, a)) |> Map.ofList
                }
            )

        open System
        open System.IO
        let relativePath (o : string) (n : string) =
            let o = if o.Length > 0 && o.[o.Length - 1] = Path.DirectorySeparatorChar then o else o + String [| Path.DirectorySeparatorChar |]
            let n = if n.Length > 0 && n.[n.Length - 1] = Path.DirectorySeparatorChar then n else n + String [| Path.DirectorySeparatorChar |]

            let ou = Uri o
            let nu = Uri n
            let res = nu.MakeRelativeUri(ou)
            res.ToString().Replace('/', Path.DirectorySeparatorChar).Replace("%20", " ")

        type Path with
            static member Components(path : string) =
                if Path.IsPathRooted path then
                    let root = Path.GetPathRoot(path)
                    Array.append [|root|] (Path.Components(path.Substring(root.Length)))
                else
                    path.Split([| Path.DirectorySeparatorChar; Path.AltDirectorySeparatorChar |], StringSplitOptions.None)

            static member Simplify (path : string) =
                let components = Path.Components path
                let l = System.Collections.Generic.List<string>()

                for c in components do
                    match c with
                        | "." -> ()
                        | ".." ->
                            if l.Count > 0 && l.[l.Count - 1] <> ".." && not (l.[l.Count-1].EndsWith(":")) then l.RemoveAt (l.Count - 1)
                            else l.Add ".."
                        | c ->
                            l.Add c

                Path.Combine(Seq.toArray l)


    let tryParse (directory : string) (content : string) =
        match pSln.parse content with
            | Some(p,"") -> Some { p with directory = directory }
            | _ -> None

    let tryLoad (file : string) =
        let file = Path.GetFullPath file
        if File.Exists file then
            let dir = Path.GetDirectoryName file
            let content = File.ReadAllText file
            tryParse dir content
        else
            None

    let load (file : string) =
        match tryLoad file with
            | Some sln -> sln
            | None -> failwith "could not load solution"

    let toString (sln : Solution) =
        let guidstr (g : Guid) = g.ToString("B").ToUpper()
        String.concat "\r\n" [
            yield ""
            yield "Microsoft Visual Studio Solution File, Format Version 12.00"
            yield "# Visual Studio 15"
            yield sprintf "VisualStudioVersion = %s" (string sln.version)
            yield sprintf "MinimumVisualStudioVersion = %s" (string sln.minVerson)

            for p in sln.projects do
                let kind =
                    match p.kind with
                        | FSharp Framework -> fsharpKind
                        | CSharp Framework -> csharpKind
                        | FSharp Core -> fsharpDotNet
                        | CSharp Core -> csharpDotNet
                        | Folder -> folderKind
                        | ProjectKind.Unknown g -> g

                yield sprintf "Project(\"%s\") = \"%s\", \"%s\", \"%s\"" (guidstr kind) p.name p.path (guidstr p.guid)
                for a in p.sections do
                    yield sprintf "\tProjectSection(%s) = %s" a.name a.kind
                    for (l,r) in a.associations do
                        yield sprintf "\t\t%s = %s" l r
                    yield "EndProjectSection"

                yield "EndProject"


            yield "Global"
            for (name, (kind, values)) in Map.toSeq sln.globalSections do
                yield sprintf "\tGlobalSection(%s) = %s" name kind

                let rec valuestr (v : Value) =
                    match v with    
                        | Value.Config(c,p) -> sprintf "%s|%s" c p
                        | Value.Project p -> guidstr p
                        | Value.Nested(p, c) -> sprintf "%s.%s" (valuestr p) (valuestr c)
                        | Value.Unknown s -> s

                for (l,r) in values do
                    yield sprintf "\t\t%s = %s" (valuestr l) (valuestr r)
                    

                yield "\tEndGlobalSection"

            yield "EndGlobal"

        ]


    let internal changeDirectory (path : string) (sln : Solution) =
        let path = Path.GetFullPath path
        if path = sln.directory then 
            sln
        else
            { sln with
                directory = path
                projects = 
                    sln.projects |> List.map (fun p ->
                        let full = Path.GetFullPath (Path.Combine(sln.directory, p.path))
                        let rel = relativePath (Path.GetDirectoryName full) path
                        let name = Path.GetFileName p.path
                        { p with path = Path.Simplify(Path.Combine(rel, name)) }
                    )
            }

    let save (file : string) (sln : Solution) =
        let str = toString (changeDirectory (Path.GetDirectoryName file) sln)
        File.WriteAllText(file, str)

    let tryFind (name : string) (sln : Solution) =
        sln.projects |> List.tryFind (fun p -> p.name = name)

    let tryFindFolder (name : string) (sln : Solution) =
        sln.projects |> List.tryFind (fun p -> p.name = name && p.kind = ProjectKind.Folder)

    let remove (p : Project) (sln : Solution) =
        { sln with
            projects = sln.projects |> List.filter (fun pp -> pp.guid <> p.guid)
            globalSections = 
                sln.globalSections |> Map.map (fun name (kind, assoc) ->
                    let assoc = 
                        assoc |> List.filter (fun (l,r) ->
                            match l with
                                | Project pp when pp = p.guid -> false
                                | Nested(Project pp,_) when pp = p.guid -> false
                                | _ -> true
                        )
                    (kind, assoc)
                )
        }

    let add (parent : Option<Project>) (p : Project) (sln : Solution) =
        let dir = Path.GetDirectoryName p.path
        let p = { p with path = Path.Combine(relativePath dir sln.directory, Path.GetFileName p.path) }

        let sln = 
            match parent with
                | Some parent -> 
                    if not (Map.containsKey "NestedProjects" sln.globalSections) then
                        { sln with globalSections = Map.add "NestedProjects" ("preSolution", []) sln.globalSections }
                    else
                        sln
                | None ->
                    sln

        { sln with
            projects = p :: sln.projects
            globalSections =
                sln.globalSections |> Map.map (fun name (kind, assoc) ->
                    
                    let assoc = 
                        match name with
                            | "ProjectConfigurationPlatforms" ->
                                let additional = 
                                    [
                                        Value.Nested(Value.Project(p.guid), Value.Config("Debug", "Any CPU.ActiveCfg")), Value.Config("Debug", "Any CPU")
                                        Value.Nested(Value.Project(p.guid), Value.Config("Debug", "Any CPU.Build.0")), Value.Config("Debug", "Any CPU")
                                        Value.Nested(Value.Project(p.guid), Value.Config("Release", "Any CPU.ActiveCfg")), Value.Config("Release", "Any CPU")
                                        Value.Nested(Value.Project(p.guid), Value.Config("Release", "Any CPU.Build.0")), Value.Config("Release", "Any CPU")
                                    ]
                                additional @ assoc

                            | "NestedProjects" ->
                                match parent with
                                    | Some parent ->
                                        [Value.Project p.guid, Value.Project parent.guid] @ assoc
                                    | None ->
                                        assoc

                            | _ -> 
                                assoc

                    (kind, assoc)

                )
        
        }

module ProjectDirectory =
    
    open System
    open System.IO
    open System.Xml
    open System.Xml.Linq

    let private xname (name : string) =
        XName.Get(name, "http://schemas.microsoft.com/developer/msbuild/2003")

    let private (?<-) (d : XElement) (name : string) (value : string) =
        let dd = d.Descendants(xname name)
        for e in dd do
            e.Value <- value

    let copy (source : string) (kind : ProjectKind) (id : int) (target : string) =
        if Directory.Exists target then Directory.Delete(target, true)
        let target = Directory.CreateDirectory target
        let source = DirectoryInfo source

        let rec copy (src : DirectoryInfo) (dst : DirectoryInfo) =
            let files = src.GetFiles("*", SearchOption.TopDirectoryOnly)
            for f in files do 
                if f.Extension <> ".fsproj" && f.Extension <> ".csproj" then
                    f.CopyTo(Path.Combine(dst.FullName, f.Name), true) |> ignore //f.FullName.Replace(src.FullName, dst.FullName), true) |> ignore

            let dirs = src.GetDirectories("*", SearchOption.TopDirectoryOnly)
            for d in dirs do
                if d.Name <> "obj" && d.Name <> "bin" then
                    printfn "%A" d
                    let dPath = Path.Combine(dst.FullName, d.Name) //d.FullName.Replace(src.FullName, dst.FullName)
                    if Directory.Exists dPath then Directory.Delete(dPath, true)
                    let dDir = Directory.CreateDirectory dPath
                    copy d dDir

        copy source target
        
        let projectName = target.Name.Substring(5)

        let projectFiles = 
            Array.append
                (source.GetFiles("*.fsproj", SearchOption.TopDirectoryOnly))
                (source.GetFiles("*.csproj", SearchOption.TopDirectoryOnly))

        let mutable newProjects = Set.empty

        for p in projectFiles do
            let d = XDocument.Load p.FullName
            let guid = Guid.NewGuid()
            let fileName = sprintf "%02d - %s" id projectName
            printfn "%A" fileName
            //let prop = d.Descendants(xname "PropertyGroup") |> Seq.head
            //prop?ProjectGuid <- guid.ToString("B").ToUpper()
            //prop?Name <- sprintf "%02d - %s" id projectName
            //prop?RootNamespace <- projectName
            //prop?AssemblyName <- projectName

            let outFile = Path.Combine(target.FullName, fileName + p.Extension)
            d.Save(outFile)

            let project = 
                {
                    kind = kind
                    guid = guid
                    name = target.Name
                    path = outFile
                    sections = []
                }

            newProjects <- Set.add project newProjects

        newProjects



let private numberedRx = System.Text.RegularExpressions.Regex @"(?<number>[0-9]+) \- .*"
let newExample (name : string) (dir : string) =
    let dirs = Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly)
    let sourceProjectName = "01 - Triangle"
    let template = Path.Combine(dir, sourceProjectName)

    let maxIndex =
        dirs |> Seq.map (fun d ->
            let m = numberedRx.Match d
            if m.Success then m.Groups.["number"].Value |> int
            else 0
        ) |> Seq.max

    let id = maxIndex + 1

    let addToSolution (solutionName : string) =
        let slnPath = Path.Combine(dir, "..", solutionName)
        let sln = slnPath |> Solution.load
        let examples = Solution.tryFindFolder "Scratch (netcore)" sln

        match Solution.tryFind sourceProjectName sln with
            | Some p -> 
                let dst = Path.Combine(dir, sprintf "%02d - %s" id name)
                let newProjs = ProjectDirectory.copy template  p.kind id dst 

                let mutable sln = sln
                for newProj in newProjs do
                    sln <- Solution.add examples newProj sln

                Solution.save slnPath sln
            | None -> failwithf "could not find source project in solution: %A" sourceProjectName

    addToSolution "Aardvark.Rendering.sln"

printfn "enter a name:"
let name = Console.ReadLine()

newExample name __SOURCE_DIRECTORY__

