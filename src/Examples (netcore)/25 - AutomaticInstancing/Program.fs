open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO
open Aardvark.Rendering.Text
open Aardvark.Application

module Shader =
    open FShade

    type UniformScope with
        member x.Color : V4d = uniform?Color

    let uniformColor (v : Effects.Vertex) =
        fragment {
            return uniform.Color
        }



[<EntryPoint>]
let main argv = 
    Ag.initialize()
    Aardvark.Init()
    
    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug false
            samples 8
        }
        
    // let's compose a coordinate cross from existing primitives
    let coordinateCross =
        Sg.ofList [
            Sg.ofList [
                Sg.cylinder' 32 C4b.Red 0.05 1.0
                Sg.cone' 32 C4b.Red 0.1 0.2 |> Sg.translate 0.0 0.0 1.0
            ]
            |> Sg.transform (Trafo3d.RotateInto(V3d.OOI, V3d.IOO))
            
            Sg.ofList [
                Sg.cylinder' 32 C4b.Green 0.05 1.0
                Sg.cone' 32 C4b.Green 0.1 0.2 |> Sg.translate 0.0 0.0 1.0
            ]
            |> Sg.transform (Trafo3d.RotateInto(V3d.OOI, V3d.OIO))
            
            
            Sg.ofList [
                Sg.cylinder' 32 C4b.Blue 0.05 1.0
                Sg.cone' 32 C4b.Blue 0.1 0.2 |> Sg.translate 0.0 0.0 1.0
            ]
            |> Sg.transform (Trafo3d.RotateInto(V3d.OOI, V3d.OOI))

            Sg.sphere' 5 C4b.White 0.1
        ]
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }
        
    // and a cylinder with uniform-color
    let cylinder =
        Sg.cylinder' 32 C4b.Black 0.3 1.0
        |> Sg.translate 0.4 0.4 0.1
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! Shader.uniformColor
            do! DefaultSurfaces.simpleLighting
        }

    // compose the coordinate cross and the cylinder to a scene
    let scene =
        Sg.ofList [ cylinder; coordinateCross]


    // if we want to replicate the scene with random positions/rotations/colors we
    // need to create arrays holding these values
    let count = 1024
    let bounds = Box3d(-10.0 * V3d.III, 10.0 * V3d.III)

    let rand = RandomSystem()
    let trafos = Array.init count (fun _ -> Trafo3d.Rotation(rand.UniformV3dDirection(), rand.UniformDouble() * Constant.Pi) * Trafo3d.Translation(rand.UniformV3d bounds))
    let colors = Array.init count (fun _ -> rand.UniformC3f().ToC4b())


    // the backend needs to know which types are used for instanced uniforms
    // NOTE that the name "ModelTrafo" is treated specially by the system (since trafos are implicitly stacked)
    let instancedUniforms =
        Map.ofList [
            "ModelTrafo",   (typeof<Trafo3d>,   AVal.constant (trafos :> System.Array))
            "Color",        (typeof<C4b>,       AVal.constant (colors :> System.Array))
        ]

    // the magical combinator instanced adjusts all used shaders in order to use
    // the applied instance attributes instead of uniforms.
    // NOTE that the shaders do not need to know about this transformation in advance and the
    //      system automatically changes the resulting GLSL code.
    // LIMITATIONS
    //  Automatic instancing does not work on scenes
    //      * which already use instancing
    //      * using IndirectDraw (text, etc.)
    //      * having precompiled/changeable shaders
    //      * requesting per-instance uniforms having a non-trivial type (structs, etc.)
    win.Scene <- Sg.instanced' instancedUniforms scene
    win.Run()
    0
