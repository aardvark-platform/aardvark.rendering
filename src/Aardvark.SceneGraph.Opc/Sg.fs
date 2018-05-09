namespace Aardvark.SceneGraph.Opc

open System.IO
open Aardvark.Base
open Aardvark.Base.Incremental  
open Aardvark.SceneGraph

module OpcHelpers =

  let getPatchPositionPath (h:PatchHierarchy) patchName =
    let fileName = "positions.aara"
    Path.combine [h.baseDir; "patches"; patchName; fileName]

module Sg2 = 
  
  
  let createFlatISg pickle unpickle (patchHierarchies) : ISg =
        
    let patchHierarchies =       
      patchHierarchies |> Seq.map(fun x -> PatchHierarchy.load pickle unpickle x) |> Seq.toList
    
    let leaves = 
      patchHierarchies 
        |> List.collect(fun x ->  
           x.tree |> QTree.getLeaves |> Seq.toList |> List.map(fun y -> (x.baseDir, y)))

    let sg = 
      let config = { wantMipMaps = true; wantSrgb = false; wantCompressed = false }

      leaves 
        |> List.map(fun (dir,patch) -> (Patch.load dir patch.info,dir, patch.info)) 
        |> List.map(fun ((a,_),c,d) -> (a,c,d))
        |> List.map (fun (g,dir,info) -> 

           let texPath = Patch.extractTexturePath dir info 0
           let tex = FileTexture(texPath,config) :> ITexture

           Sg.ofIndexedGeometry g
             |> Sg.trafo (Mod.constant info.Local2Global)             
             |> Sg.diffuseTexture (Mod.constant tex)             
           )
        |> Sg.ofList
        
    sg
