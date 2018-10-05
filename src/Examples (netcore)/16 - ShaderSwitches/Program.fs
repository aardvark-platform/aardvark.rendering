open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open FShade

// This example illustrates how to render a simple triangle using aardvark.

module Sg =

    let changeableSurface (compile : EffectConfig -> EffectInputLayout * IMod<FShade.Imperative.Module>) (sg : ISg) =
        Sg.SurfaceApplicator(Surface.FShade compile, sg) :> ISg

    let effectPool (active : IMod<int>) (effects : Effect[]) (sg : ISg) =
        let compile (cfg : EffectConfig) =
            let modules1 = effects |> Array.map (Effect.toModule cfg)
            let layout = EffectInputLayout.ofModules modules1
            let modules = modules1 |> Array.map (EffectInputLayout.apply layout)
            let current = active |> Mod.map (fun i -> modules.[i % modules.Length])
            layout, current

        changeableSurface compile sg


            





[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    Ag.initialize()
    Aardvark.Init()

    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug true
            samples 8
        }

    let activeShader = Mod.init 0

    let effects =
        [|
            // red
            Effect.compose [
                toEffect DefaultSurfaces.trafo
                toEffect (DefaultSurfaces.constantColor C4f.Red)
            ]
            
            // red with lighting
            Effect.compose [
                toEffect DefaultSurfaces.trafo
                toEffect (DefaultSurfaces.constantColor C4f.Red)
                toEffect DefaultSurfaces.simpleLighting
            ]
            
            // vertex colors with lighting
            Effect.compose [
                toEffect DefaultSurfaces.trafo
                toEffect DefaultSurfaces.simpleLighting
            ]
            
            // texture with lighting
            Effect.compose [
                toEffect DefaultSurfaces.trafo
                toEffect DefaultSurfaces.diffuseTexture
                toEffect DefaultSurfaces.simpleLighting
            ]
        |]


    win.Keyboard.DownWithRepeats.Values.Add (fun k ->
        match k with
            | Keys.Enter -> 
                transact (fun () -> activeShader.Value <- (activeShader.Value + 1) % effects.Length)
            | _ ->
                ()
    )

    let sg = 
        Sg.box' C4b.Green Box3d.Unit
            //|> Sg.ofIndexedGeometry
            |> Sg.diffuseTexture DefaultTextures.checkerboard
            |> Sg.effectPool activeShader effects

    // show the scene in a simple window
    win.Scene <- sg
    win.Run()

    0
