open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

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


    // show the scene in a simple window
    show {
        backend Backend.Vulkan
        display Display.Mono
        debug true
        samples 8
        scene sg
    }

    0
