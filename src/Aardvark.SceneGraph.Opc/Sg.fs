namespace Aardvark.SceneGraph.Opc

open System.IO
open Aardvark.Base
open Aardvark.Base.Incremental  
open Aardvark.SceneGraph

module Sg2 = 
  
  
  let createFlatISg pickle unpickle (patchHierarchies) : ISg =
        
    let mode = ViewerModality.XYZ

    let patchHierarchies =       
      patchHierarchies |> Seq.map(fun x -> PatchHierarchy.load pickle unpickle x) |> Seq.toList
    
    let leaves = 
      patchHierarchies 
        |> List.collect(fun x ->  
           x.tree |> QTree.getLeaves |> Seq.toList |> List.map(fun y -> (x.opcPaths, y)))

    let sg = 
      let config = { wantMipMaps = true; wantSrgb = false; wantCompressed = false }

      leaves 
        |> List.map(fun (opcPaths,patch) -> (Patch.load opcPaths mode patch.info, opcPaths, patch.info)) 
        |> List.map(fun ((a,_),c,d) -> (a,c,d))
        |> List.map (fun (g,opcPaths,info) -> 

           let texPath = Patch.extractTexturePath opcPaths info 0
           let tex = FileTexture(texPath,config) :> ITexture

           Sg.ofIndexedGeometry g
             |> Sg.trafo (Mod.constant info.Local2Global)             
             |> Sg.diffuseTexture (Mod.constant tex)             
           )
        |> Sg.ofList
        
    sg
