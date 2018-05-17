namespace Aardvark.SceneGraph.Opc

open System.IO
open Aardvark.Base

type OpcPaths = OpcPaths of string

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module OpcPaths =

    // patches/
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
    let Images_DirName = "images"
    let ImagePyramid_FileName = "imagepyramid.xml"

    let value (OpcPaths opc_DirAbsPath) = opc_DirAbsPath

[<AutoOpen>]
module OpcPathsExtensions =
    open OpcPaths

    let (+/) path1 path2 = Path.Combine(path1, path2)

    type OpcPaths with
        member this.Opc_DirAbsPath = value this

        // ?? for debug output
        member this.ShortName = Directory.GetParent(this.Opc_DirAbsPath).Name +/ Path.GetFileName(this.Opc_DirAbsPath)

        // Patches
        member this.Patches_DirAbsPath = this.Opc_DirAbsPath +/ Patches_DirName

        member this.PatchHierarchy_FileAbsPath = this.Patches_DirAbsPath +/ PatchHierarchy_FileName
        member this.PatchHierarchyCache_FileAbsPath = this.Patches_DirAbsPath +/ PatchHierarchyCache_FileName
        member this.profileLut_FileAbsPath = this.Patches_DirAbsPath +/ ProfilLut_FileName

        // Images
        member this.Images_DirAbsPath = this.Opc_DirAbsPath +/ Images_DirName
        member this.ImagePyramid_FileAbsPaths =
            Directory.GetDirectories(this.Images_DirAbsPath)
            |> Array.map (fun images_DirPath -> this.Images_DirAbsPath +/ images_DirPath +/ ImagePyramid_FileName)