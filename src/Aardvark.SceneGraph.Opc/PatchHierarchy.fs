namespace Aardvark.SceneGraph.Opc

open System
open System.IO
open Aardvark.Base
open Aardvark.Base.IO
open Aardvark.Prinziple

type PatchHierarchy =
    { 
        baseDir : string
        tree    : QTree<Patch>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PatchHierarchy =
    open XmlHelpers
    open System.Xml
    open System.Xml.Linq
    
    let parseDouble d =
        let mutable r = 0.0
        if Double.TryParse(d,Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, &r) then r
        else failwithf "could not parse int double:"

    let private ofDoc (x : XDocument) =
        let hierarchy = x.Descendants(xname "PatchHierarchy") |> Seq.head
        let rootPatch = hierarchy |> elem "RootPatch" |> xvalue

        let geometrySizes = hierarchy |> elem "AvgGeometrySizes" |> xvalue 
        let avgSizes = ((geometrySizes.[1..(geometrySizes.Length-2)]).Replace(" ","").Split [|','|]) |> Array.map parseDouble
        Log.line "avgSizes %A" avgSizes

        let map = hierarchy |> elem' "SubPatchMap"
        let items = 
            match map with
                | Some subs -> subs.Descendants(xname "item") |> Seq.toList
                | None -> []

        let children = 
            items |> List.map (fun e -> 
                let key = elem "key" e |> xvalue
                let values = elem "val" e
                let nw = elem' "nw" values |> Option.map xvalue 
                let ne = elem' "ne" values |> Option.map xvalue 
                let sw = elem' "sw" values |> Option.map xvalue 
                let se = elem' "se" values |> Option.map xvalue 
                key, Array.choose id [|nw;ne;sw;se |]
            ) |> Dictionary.ofList
  
        let rec mkTree (name : string) =
            match children.TryGetValue name with
                | (true,v) -> 
                    QTree.Node(name, v |> Array.map mkTree)
                | _ -> Leaf name

        let tree = mkTree rootPatch
        tree, avgSizes |> Array.rev
    
    let loadAndCache dir (xml:string) (pickle : QTree<Patch> -> byte[]) cache =
       Log.startTimed "loading from hierarchy"
       let tree, sizes =
           XDocument.Load(xml) |> ofDoc
       let hierarchy = 
           tree
               |> QTree.mapLevel 0 (fun level p -> 
                       p |> PatchFileInfo.load dir |> Patch.ofInfo level sizes.[level]
                   )
       hierarchy |> pickle |> File.writeAllBytes cache
       Log.stop()
       { baseDir = dir; tree = hierarchy }

    let load (pickle : QTree<Patch> -> byte[]) (unpickle :  byte[] -> QTree<Patch>) (folder : string) =
        let xmlPath   = Path.combine [folder; @"Patches\patchhierarchy.xml"]
        let cachefile = Path.combine [folder; "hierarchy.cache"]
                
        if Prinziple.exists cachefile then
            try
                Log.startTimed "loading from cache file"

                let readFile = Prinziple.readAllBytes cachefile

                let r = { baseDir = folder; tree = readFile |> unpickle }
                Log.stop()
                r
            with e -> 
                Log.warn "could not parse cache file. recomputing."
                loadAndCache folder xmlPath pickle cachefile
        else
            loadAndCache folder xmlPath pickle cachefile

    let getPatchKdTreePath (h:PatchHierarchy) patchName = 
            let rootpatch = h.tree |> QTree.getRoot 

            let fileName = sprintf "%s-0.aakd" patchName            
            Path.combine [h.baseDir; "patches"; rootpatch.info.Name; fileName]

    let getPatchKdTreePath2d (h:PatchHierarchy) patchName = 
            let rootpatch = h.tree |> QTree.getRoot 

            let fileName = sprintf "%s-0-2d.aakd" patchName
            Path.combine [h.baseDir; "patches"; rootpatch.info.Name; fileName]

    let getPatchPositionPath (h:PatchHierarchy) patchName =
        let fileName = "positions.aara"
        Path.combine [h.baseDir; "patches"; patchName; fileName]

    let getPatch2dPositionPath (h:PatchHierarchy) patchName = 
        let fileName = "positions2d.aara"
        Path.combine [h.baseDir; "patches"; patchName; fileName]

    let getProfileLutPath (h:PatchHierarchy) = 
        let fileName = "profilelut8.bin"
        Path.combine [h.baseDir; "patches"; fileName]

    let getkdTreePath (h:PatchHierarchy) (s) =
        let rootpatch = h.tree |> QTree.getRoot 
                                
        let fileName = sprintf s rootpatch.info.Name
        Path.combine [h.baseDir; "patches"; rootpatch.info.Name; fileName]

    let getLevelNKdTreePath (h:PatchHierarchy) =
        getkdTreePath h "%s.aakd"
        
    let getMasterKdTreePath (h:PatchHierarchy) =
        getkdTreePath h "%s-0.aakd"

    let getMasterKdTreePath2d (h:PatchHierarchy) =
        getkdTreePath h "%s-0-2d.aakd"

