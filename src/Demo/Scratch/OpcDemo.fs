namespace OpcDemo

open Aardvark.Base
open Aardvark.SceneGraph.Opc

module OpcDemo =

  let input = 
    [
       (OpcPaths @"E:\Aardwork\Exomars\_SCENES\cd - Copy\Surface\Cape_Desire_RGB\OPC_000_000","07-Patch-00001~0016") 
       (OpcPaths @"G:\1285_TSC_Selzthaltunnel_2017\Selzthaltunnel_RFB-Graz\2017_Bestandsaufnahme\OPC\OPC\cQXhT4u3RFSYQtH4asVbFQ","00-Patch-00024~0019") 
    ]

  let loadPatchFileInfos paths =          
     paths |> List.map(fun (a,b) -> PatchFileInfo.load a b)               
    

  let loadPatches (paths:list<OpcPaths*string>) (infos : list<PatchFileInfo>) = 
    infos 
      |> List.zip paths
      |> List.map(fun ((opc,_),info) -> Patch.load opc ViewerModality.XYZ info)

  let start() =
    let infos = input |> loadPatchFileInfos
    let patches = loadPatches input infos
       
    Log.line "[OpcDemo:] %A" patches

