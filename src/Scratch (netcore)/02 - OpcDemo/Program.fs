open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Data.Opc

open MBrace.FsPickler
//open MBrace.FsPickler.Json

#nowarn "8989"

module Serialization =

  let registry = new CustomPicklerRegistry()    
  let cache = PicklerCache.FromCustomPicklerRegistry registry    

  let binarySerializer = FsPickler.CreateBinarySerializer(picklerResolver = cache)
  //let jsonSerializer = FsPickler.CreateJsonSerializer(indent=true)

module Discover = 
  open System.IO
  
  /// <summary>
  /// checks if "path" is a valid opc folder containing "images", "patches", and patchhierarchy.xml
  /// </summary>
  let isOpcFolder (path : string) = 
      let imagePath = Path.combine [path; "images"]
      let patchPath = Path.combine [path; "patches"]
      (Directory.Exists imagePath) &&
      (Directory.Exists patchPath) && 
       File.Exists(patchPath + "\\patchhierarchy.xml")
  
  /// <summary>
  /// checks if "path" is a valid surface folder
  /// </summary>        
  let isSurfaceFolder (path : string) =
      Directory.GetDirectories(path) |> Seq.forall isOpcFolder
  
  let discover (p : string -> bool) path : list<string> =
    if Directory.Exists path then
      Directory.EnumerateDirectories path                     
        |> Seq.filter p            
        |> Seq.toList
    else List.empty
  
  /// returns all valid surface folders in "path"   
  let discoverSurfaces path = 
    discover isSurfaceFolder path          
  
  let discoverOpcs path = 
    discover isOpcFolder path


[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    
    Aardvark.Init()


    // lets define the bounds/color for our box
    // NOTE that the color is going to be ignored since we're using a texture
    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

    let sg = 
        // thankfully aardvark defines a primitive box
        Sg.box (AVal.constant color) (AVal.constant box)

            // apply the texture as "DiffuseTexture"
            |> Sg.diffuseTexture DefaultTextures.checkerboard

            // apply a shader ...
            // * transforming all vertices
            // * looking up the DiffuseTexture 
            // * applying a simple lighting to the geometry (headlight)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.diffuseTexture
                do! DefaultSurfaces.simpleLighting
            }
    let trafos = 
      [
        @"[[[0.946904568258789, -0.139837982053283, -0.289511791445387, -1775711.24574897], [-0.139837982053282, 0.63170727530683, -0.762490168526582, -4676628.43142898], [0.289511791445387, 0.762490168526581, 0.578611843565602, 3945756.08543205], [0, 0, 0, 1]], [[0.946904568258789, -0.139837982053282, 0.289511791445387, -114884.105055822], [-0.139837982053283, 0.63170727530683, 0.762490168526581, -302651.895820294], [-0.289511791445387, -0.762490168526582, 0.578611843565602, -6363033.74751501], [0, 0, 0, 1]]]" |> Trafo3d.Parse
        @"[[[-0.583719594844827, -0.74285860550925, -0.327769014421824, -1775697.97909903], [0.660570939555018, -0.199742013243182, -0.723705162314712, -4676580.77770396], [0.472141364857597, -0.638955569947094, 0.607304134041576, 3945809.08376876], [0, 0, 0, 1]], [[-0.583719594844827, 0.660570939555018, 0.472141364857597, 189723.967028285], [-0.74285860550925, -0.199742013243182, -0.638955569947094, 267994.507829842], [-0.327769014421824, -0.723705162314712, 0.607304134041576, -6362790.59603779], [0, 0, 0, 1]]]" |> Trafo3d.Parse
        @"[[[-0.442985368131951, -0.858934018017014, -0.256897482109369, -1775690.45944296], [0.559434158595298, -0.0409168834430755, -0.827864258707974, -4676578.492377], [0.700569329772434, -0.510448980156151, 0.498642610333074, 3945817.26073413], [0, 0, 0, 1]], [[-0.442985368131951, 0.559434158595298, 0.700569329772434, -934685.691633964], [-0.858934018017014, -0.0409168834430755, -0.510448980156151, 297586.438455486], [-0.256897482109369, -0.827864258707974, 0.498642610333074, -6295295.21370768], [0, 0, 0, 1]]]" |> Trafo3d.Parse
        @"[[[-0.446828407749373, 0.76557851551596, -0.462854092137632, -1775635.89447231], [-0.489216491874536, -0.64226509780579, -0.590053191007806, -4676551.11197109], [-0.749007074803784, -0.0372166726201055, 0.66151592662079, 3945881.25880692], [0, 0, 0, 1]], [[-0.446828407749373, -0.489216491874536, -0.749007074803784, -125757.509358091], [0.76557851551596, -0.64226509780579, -0.0372166726201055, -1497344.2941301], [-0.462854092137632, -0.590053191007806, 0.66151592662079, -6191537.54368789], [0, 0, 0, 1]]]" |> Trafo3d.Parse
      ]

    let patchHierarchies = 
      [
        Discover.discoverOpcs @"I:\20170330_ICL_Demos\MURFI NonMission\Surfaces\Dinosaur_Quarry_2"
        Discover.discoverOpcs @"I:\20170330_ICL_Demos\MURFI NonMission\Surfaces\Dinosaur_Quarry_3"
        Discover.discoverOpcs @"I:\20170330_ICL_Demos\MURFI NonMission\Surfaces\Dinosaur_Quarry_4"
        Discover.discoverOpcs @"I:\20170330_ICL_Demos\MURFI NonMission\Surfaces\Dinosaur_Quarry_5b"
      ] |> List.concat |> List.map OpcPaths
        

    let opcSg = 
      Sg2.createFlatISg' Serialization.binarySerializer.Pickle Serialization.binarySerializer.UnPickle patchHierarchies trafos
         |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.diffuseTexture
               // do! DefaultSurfaces.simpleLighting
            }
         |> Sg.trafo (AVal.constant trafos.[0].Inverse)

    // show the scene in a simple window
    show {
        backend Backend.Vulkan
        display Display.Mono
        debug true
        samples 8
        scene opcSg
    }

    0
