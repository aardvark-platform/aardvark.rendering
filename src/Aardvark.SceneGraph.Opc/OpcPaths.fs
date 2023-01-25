namespace Aardvark.SceneGraph.Opc

open System
open System.IO
open Aardvark.Base

type OpcPaths = OpcPaths of string

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module OpcPaths =

    // patches/
    let Patches_DirNames = ["patches"; "Patches"]

    // obsolete in favor of robust handling for: https://github.com/pro3d-space/PRo3D/issues/280
    [<Obsolete>]
    let Patches_DirName = "patches"

    let PatchHierarchy_FileName = "patchhierarchy.xml"
    let PatchHierarchyCache_FileName = "hierarchy.cache"
    let ProfilLut_FileName = "profilelut8.bin"

    // per patch
    // Patch.xml
    // .aara files: Normals, Offset, Positions, Positions2d.....
    let PatchFileInfo_FileName = "Patch.xml"

    // KdTrees in root-patch directory
    let KdTree_Ext = "aakd"
    let KdTreeMaster_Ext = "cache"
    
    // images/
    let Images_DirNames = ["images"; "Images"]
    // obsolete in favor of robust handling for: https://github.com/pro3d-space/PRo3D/issues/280
    [<Obsolete>]
    let Images_DirName = "images"

    let ImagePyramid_FileName = "imagepyramid.xml"

    let value (OpcPaths opc_DirAbsPath) = opc_DirAbsPath

[<AutoOpen>]
module PathCombineOperator =
    let (+/) path1 path2 = Path.Combine(path1, path2)

type OpcPaths with
    member this.Opc_DirAbsPath = OpcPaths.value this

    // ?? for debug output
    member this.ShortName = Directory.GetParent(this.Opc_DirAbsPath).Name +/ Path.GetFileName(this.Opc_DirAbsPath)

    // Patches. Note: this throws if no patches dir is found given current OpcPaths
    member this.Patches_DirAbsPath = 
        let probingDirs = 
            OpcPaths.Patches_DirNames |> List.map (fun patchSuffix -> this.Opc_DirAbsPath +/ patchSuffix)
        match probingDirs |> List.tryFind Directory.Exists with
        | None -> failwithf "patches dir not found. Probing directories: %A" probingDirs
        | Some d -> d

    member this.PatchHierarchy_FileAbsPath = this.Patches_DirAbsPath +/ OpcPaths.PatchHierarchy_FileName
    member this.PatchHierarchyCache_FileAbsPath = this.Patches_DirAbsPath +/ OpcPaths.PatchHierarchyCache_FileName
    member this.profileLut_FileAbsPath = this.Patches_DirAbsPath +/ OpcPaths.ProfilLut_FileName

    // Images. Note: this throws if no patches dir is found given current OpcPaths
    member this.Images_DirAbsPath = 
        let probingDirs = 
            OpcPaths.Images_DirNames |> List.map (fun imageSuffix -> this.Opc_DirAbsPath +/ imageSuffix)
        match probingDirs |> List.tryFind Directory.Exists with
        | None -> failwithf "images dir not found. Probing directories: %A" probingDirs
        | Some d -> d
        

    member this.ImagePyramid_FileAbsPaths =
        Directory.GetDirectories(this.Images_DirAbsPath)
        |> Array.map (fun images_DirPath -> this.Images_DirAbsPath +/ images_DirPath +/ OpcPaths.ImagePyramid_FileName)