open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Application
open Aardvark.SceneGraph

[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    Ag.initialize()
    Aardvark.Init()


    // lets define the bounds/color for our box
    // NOTE that the color is going to be ignored since we're using a texture
    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

    let box = 
        // thankfully aardvark defines a primitive box
        Sg.box (Mod.constant color) (Mod.constant box)

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

    let sphere = 
        // thankfully aardvark defines a primitive box
        Sg.unitSphere' 5 C4b.Red
            // apply a shader ...
            // * transforming all vertices
            // * looking up the DiffuseTexture 
            // * applying a simple lighting to the geometry (headlight)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.simpleLighting
            }

    let sg =
        Sg.execute (
            RenderCommand.Ordered [
                RenderCommand.Unordered [ box ]
                RenderCommand.Clear(C4f.White)
                RenderCommand.Unordered [ sphere ]
            ]
        )

    // show the scene in a simple window
    show {
        backend Backend.Vulkan
        display Display.Mono
        verbosity DebugVerbosity.Warning
        samples 1
        scene sg
    }

    0
