open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO
open Aardvark.Application

[<EntryPoint>]
let main argv = 
    
    Ag.initialize()
    Aardvark.Init()

    let sg = 
        // load the scene and wrap it in an adapter
        Loader.Assimp.load (Path.combine ["..";"..";"data";"aardvark";"aardvark.obj"])
            |> Sg.adapter

            // flip the z coordinates (since the model is upside down)
            |> Sg.transform (Trafo3d.Scale(1.0, 1.0, -1.0))
            |> Sg.scale 3.0

            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.diffuseTexture
                do! DefaultSurfaces.normalMap
                do! DefaultSurfaces.simpleLighting
            }
    

    // show the scene in a simple window
    show {
        backend Backend.GL
        display Display.Mono
        debug false
        samples 8
        scene sg
    }

    0
