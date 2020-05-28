open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.Application
open Aardvark.SceneGraph
open System

[<EntryPoint>]
let main argv = 
    // first we need to initialize Aardvark's core components
    Aardvark.Rendering.GL.Config.UseNewRenderTask <- true

    Aardvark.Init()


    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug false
            samples 8
        }

    // lets define the bounds/color for our box
    // NOTE that the color is going to be ignored since we're using a texture
    let box = Box3d(-0.7 * V3d.III, 0.7 * V3d.III)
    let color = C4b.Red

    let clear = AVal.init false

    win.Keyboard.DownWithRepeats.Values.Add(fun k ->
        match k with
            | Keys.Space ->
                transact (fun () -> clear.Value <- not clear.Value)
            | _ ->
                ()
    )

    let box = 
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

    let sphere = 
        // thankfully aardvark defines a primitive sphere
        Sg.unitSphere' 5 C4b.Red
            // apply a shader ...
            // * transforming all vertices
            // * applying a simple lighting to the geometry (headlight)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.simpleLighting
            }

    let sg =
        Sg.execute (
            RenderCommand.Ordered [
                RenderCommand.Clear(C4f.White, 1.0, 0u)
                RenderCommand.Unordered [ box ]
                RenderCommand.When(
                    clear,
                    RenderCommand.Clear(1.0, 0u)
                )
                RenderCommand.Unordered [ sphere ]
            ]
        )
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.thickLine
        }  
        |> Sg.uniform "LineWidth" (AVal.constant 3.0)
        //|> Sg.uniform "ViewportSize" win.Sizes


    // show the scene in a simple window
    win.Scene <- sg
    win.Run()

    0
