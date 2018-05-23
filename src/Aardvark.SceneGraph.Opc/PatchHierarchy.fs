namespace Aardvark.SceneGraph.Opc

open System
open Aardvark.Base
open Aardvark.Prinziple

type PatchHierarchy =
    { 
        opcPaths : OpcPaths
        tree     : QTree<Patch>
    }

[<AutoOpen>]
module PatchHierarchyExtensions =
    module PatchHierarchy =
        let kdTree_FileName (patch_Name : string) (patch_Level : int) (posType : ViewerModality) =
              let lvl_Sub = 
                  match patch_Level > -1 with
                  | true  -> sprintf "-%i" patch_Level
                  | false -> ""
              let pos_Sub =
                  match posType with
                  | XYZ -> ""
                  | SvBR -> "-2d"
              sprintf "%s%s%s.%s" patch_Name lvl_Sub pos_Sub OpcPaths.KdTree_Ext

    type PatchHierarchy with

        [<Obsolete("baseDir is deprecated, please use opcPaths.Opc_DirAbsPath instead.")>]
        member this.baseDir = this.opcPaths.Opc_DirAbsPath

        // == Patch methods ==
        member this.rootPatch_DirName =
            let rootpatch = this.tree |> QTree.getRoot
            rootpatch.info.Name

        member this.rootPatch_DirAbsPath =
            this.opcPaths.Patches_DirAbsPath +/ this.rootPatch_DirName
    
        // == PatchTree methods ==
        member this.patchTree_DirAbsPaths =
            let patches_DirAbsPath = this.opcPaths.Patches_DirAbsPath
            this.tree |> QTree.map (fun patch -> patches_DirAbsPath +/ patch.info.Name)

        // == KdTree methods ==
        member this.kdTree_FileAbsPath patch_Name patch_Level posType =
            this.rootPatch_DirAbsPath +/ (PatchHierarchy.kdTree_FileName patch_Name patch_Level posType)

        member this.kdTreeAgg_FileAbsPath (lvl:int) (posType:ViewerModality) =
            this.kdTree_FileAbsPath this.rootPatch_DirName lvl posType

        member this.kdTreeN_FileAbsPath =
            this.kdTreeAgg_FileAbsPath -1 XYZ

        member this.kdTreeN2d_FileAbsPath =
            this.kdTreeAgg_FileAbsPath -1 SvBR

        member this.kdTreeAggZero_FileAbsPath =
            this.kdTreeAgg_FileAbsPath 0 XYZ

        member this.kdTreeAggZero2d_FileAbsPath =
            this.kdTreeAgg_FileAbsPath 0 SvBR

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PatchHierarchy =
    open XmlHelpers
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
    
    let loadAndCache (opcPaths : OpcPaths) (pickle : QTree<Patch> -> byte[]) =
       Log.startTimed "loading from hierarchy"
       let xml   = opcPaths.PatchHierarchy_FileAbsPath
       let cache = opcPaths.PatchHierarchyCache_FileAbsPath

       let tree, sizes =
           XDocument.Load(xml) |> ofDoc
       let hierarchy = 
           tree
               |> QTree.mapLevel (fun level p -> 
                       p |> PatchFileInfo.load opcPaths |> Patch.ofInfo level sizes.[level]
                   )
       hierarchy |> pickle |> File.writeAllBytes cache
       Log.stop()
       { opcPaths = opcPaths; tree = hierarchy }

    let load (pickle : QTree<Patch> -> byte[]) (unpickle :  byte[] -> QTree<Patch>) (opcPaths : OpcPaths) =
        let cachefile = opcPaths.PatchHierarchyCache_FileAbsPath
                
        if Prinziple.exists cachefile then
            try
                Log.startTimed "loading from cache file"

                let readFile = Prinziple.readAllBytes cachefile

                let r = { opcPaths = opcPaths; tree = readFile |> unpickle }
                Log.stop()
                r
            with e -> 
                Log.warn "could not parse cache file. recomputing."
                loadAndCache opcPaths pickle
        else
            loadAndCache opcPaths pickle

    let getLevelFromResolution (resolution : float) (ph : PatchHierarchy) =
            let (lvl, _) = 
              ph.tree
                |> QTree.flatten
                |> Array.map (fun patch -> (patch.level, patch.triangleSize))
                |> Array.distinct
                |> Array.sortByDescending (fun (lvl, _) -> lvl)
                |> Array.find (fun (_, triSize) -> resolution >= triSize)
            lvl

    [<Obsolete("getPatchKdTreePath is deprecated, please use h.kdTree_FileAbsPath instead.")>]
    let getPatchKdTreePath (h:PatchHierarchy) patchName = 
      h.kdTree_FileAbsPath patchName 0 XYZ
            
    [<Obsolete("getPatchKdTreePath2d is deprecated, please use h.kdTree_FileAbsPath instead.")>]
    let getPatchKdTreePath2d (h:PatchHierarchy) patchName = 
      h.kdTree_FileAbsPath patchName 0 SvBR
            
    [<Obsolete("getPatchPositionPath is deprecated, please use opcPaths.Patches_DirAbsPath and PatchFileInfo instead.")>]
    let getPatchPositionPath (h:PatchHierarchy) patchName =
      h.opcPaths.Patches_DirAbsPath +/ patchName +/ "positions.aara"
        
    [<Obsolete("getPatch2dPositionPath is deprecated, please use opcPaths.Patches_DirAbsPath and PatchFileInfo instead.")>]
    let getPatch2dPositionPath (h:PatchHierarchy) patchName = 
      h.opcPaths.Patches_DirAbsPath +/ patchName +/ "positions2d.aara"
        
    [<Obsolete("getProfileLutPath is deprecated, please use opcPaths.profileLut_FileAbsPath instead.")>]
    let getProfileLutPath (h:PatchHierarchy) = 
      h.opcPaths.profileLut_FileAbsPath
        
    [<Obsolete("getkdTreePath is deprecated, please use PatchHierarchy members instead.")>]
    let getkdTreePath (h:PatchHierarchy) (s) =
        let fileName = sprintf s h.rootPatch_DirName
        h.rootPatch_DirAbsPath +/ fileName

    [<Obsolete("getLevelNKdTreePath is deprecated, please use h.kdTreeN_FileAbsPath instead.")>]
    let getLevelNKdTreePath (h:PatchHierarchy) =
      h.kdTreeN_FileAbsPath
        
    [<Obsolete("getMasterKdTreePath is deprecated, please use h.kdTreeAggZero_FileAbsPath instead.")>]
    let getMasterKdTreePath (h:PatchHierarchy) =
      h.kdTreeAggZero_FileAbsPath

    [<Obsolete("getMasterKdTreePath2d is deprecated, please use h.kdTreeN2d_FileAbsPath instead.")>]
    let getMasterKdTreePath2d (h:PatchHierarchy) =
      h.kdTreeN2d_FileAbsPath