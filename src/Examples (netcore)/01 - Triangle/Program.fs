open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

// This example illustrates how to render a simple triangle using aardvark.

[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    
    Aardvark.Init()

    // then we define some vertex attributes for our triangle
    let positions = [| V3f(-0.5f, -0.5f, 0.0f); V3f(0.5f, -0.5f, 0.0f); V3f(0.0f, 0.5f, 0.0f) |]
    let colors    = [| C4b.Red; C4b.Green; C4b.Blue |]
    
    let sg = 
        // create a scenegraph rendering a triangle-list
        Sg.draw IndexedGeometryMode.TriangleList
            
            // apply the attributes we have (position, color) to the draw-call
            // NOTE that aardvark figures out how many triangles are rendered automatically
            //      in this simple case (Sg.render provides a more flexible API for creating draw-calls)
            |> Sg.vertexAttribute DefaultSemantic.Positions (AVal.constant positions)
            |> Sg.vertexAttribute DefaultSemantic.Colors (AVal.constant colors)

            // apply a simple shader defined in the core libraries
            // interpolating per-vertex colors for each fragment 
            |> Sg.shader {
                do! DefaultSurfaces.vertexColor
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
