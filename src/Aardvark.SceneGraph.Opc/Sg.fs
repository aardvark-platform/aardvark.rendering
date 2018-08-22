namespace Aardvark.SceneGraph.Opc

open System.IO
open Aardvark.Base
open Aardvark.Base.Incremental  
open Aardvark.SceneGraph

module Sg2 = 
  
  
  let createFlatISg' pickle unpickle (patchHierarchies) (trafos : list<Trafo3d>) : ISg =
        
    let mode = ViewerModality.XYZ

    let patchHierarchies =       
      patchHierarchies |> List.map(fun x -> PatchHierarchy.load pickle unpickle x) |> List.zip trafos
    
    let leaves = 
      patchHierarchies 
        |> List.collect(fun (trafo,x) ->  
           x.tree 
             |> QTree.getLeaves 
             |> Seq.toList 
             |> List.map(fun y -> (x.opcPaths, y, trafo)))

    let sg = 
      let config = { wantMipMaps = true; wantSrgb = false; wantCompressed = false }

      leaves 
        |> List.map(fun (opcPaths,patch,trafo) -> (Patch.load opcPaths mode patch.info, opcPaths, patch.info, trafo)) 
        |> List.map(fun ((a,_),c,d,e) -> (a,c,d,e))
        |> List.map (fun (g,opcPaths,info,trafo) -> 

           let texPath = Patch.extractTexturePath opcPaths info 0
           let tex = FileTexture(texPath,config) :> ITexture

           Sg.ofIndexedGeometry g
             |> Sg.trafo (Mod.constant (info.Local2Global * trafo))             
             |> Sg.diffuseTexture (Mod.constant tex)             
           )
        |> Sg.ofList
        
    sg

  let createFlatISg pickle unpickle (patchHierarchies : list<OpcPaths>) =
      let trafos = List.init patchHierarchies.Length (fun _ -> Trafo3d.Identity)
      createFlatISg' pickle unpickle (patchHierarchies) trafos
