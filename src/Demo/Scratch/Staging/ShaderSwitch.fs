namespace Aardvark.SceneGraph.Pool

open System
open System.Threading
open System.Reflection
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg
open Aardvark.Base.Monads.State
open Microsoft.FSharp.NativeInterop

module ``Shader Switch`` =

    [<Demo("Shader Switch")>]
    let sg ()=
        
        let r = App.Runtime
        
        let rnd = Random()

        let geometry (pos : V3d) =  
            let trafo = Trafo3d.Scale 0.1 * Trafo3d.Translation pos

            let vg = Primitives.unitSphere 3

            (vg, trafo)


        let s = 5.0 

        let all =
            [    
                for x in -s / 2.0 .. s / 2.0 do
                    for y in -s / 2.0 .. s / 2.0 do
                        for z in -s / 2.0 .. s / 2.0 do
                            yield geometry(V3d(x,y,z))
            ]

        Log.line "count: %A" (List.length all)

        let geometries =
            CSet.ofList all

        let fboSig = App.FramebufferSignature

        let shaderRed = App.Runtime.PrepareEffect(fboSig, [ DefaultSurfaces.trafo |> toEffect
                                                            DefaultSurfaces.constantColor C4f.Red |> toEffect
                                                          ]) :> ISurface

        let shaderGreen = App.Runtime.PrepareEffect(fboSig, [ DefaultSurfaces.trafo |> toEffect
                                                              DefaultSurfaces.constantColor C4f.Green |> toEffect
                                                              DefaultSurfaces.simpleLighting |> toEffect ]) :> ISurface

        let shaderBlue  = App.Runtime.PrepareEffect(fboSig, [ DefaultSurfaces.trafo |> toEffect
                                                              DefaultSurfaces.constantColor C4f.Blue |> toEffect
                                                              DefaultSurfaces.simpleLighting |> toEffect ]) :> ISurface

        let shaderMod = Mod.init shaderRed

        let initial = geometries.Count
        let random = Random()
        App.Keyboard.DownWithRepeats.Values.Add(fun k ->
            if k = Keys.X then
                if geometries.Count > 0 then
                    let remove = geometries.RandomOrder() |> Seq.atMost 1024 |> Seq.toList
                    transact (fun () ->
                        geometries.ExceptWith remove
                    )

            if k = Keys.T then
                transact (fun () ->
                    geometries.Clear()
                )

            if k = Keys.R then
                transact (fun () ->
                    geometries.Clear()
                    geometries.UnionWith all
                )

            if k = Keys.Z then
                transact (fun () ->
                    
                    let rnd = Random()
                    Report.Line("adding new random stuff: Seed={0}", rnd.Next())
             
                    for i in 0 .. 100 do
                        let rx = rnd.NextDouble() * 10.0 - 5.0
                        let ry = rnd.NextDouble() * 10.0 - 5.0
                        let rz = rnd.NextDouble() * 10.0 - 5.0
                        let newStuff = geometry(V3d(rx, ry, rz))
                        geometries.Add newStuff |> ignore    
                )

                Report.Line("new geometry count: {0}", geometries.Count)

            if k = Keys.U then
                
                let sr = rnd.Next(3)
                Report.Line("switching shader: {0}", sr)

                transact (fun () ->
                    Mod.change shaderMod (match sr with
                                          | 1 -> shaderRed
                                          | 2 -> shaderGreen
                                          | _ -> shaderBlue)
                )
                
        )

        let mode = Mod.init FillMode.Fill
        App.Keyboard.KeyDown(Keys.K).Values.Add (fun () ->
            transact (fun () ->
                mode.Value <- 
                    match mode.Value with
                        | FillMode.Fill -> FillMode.Line
                        | _ -> FillMode.Fill
            )
        )

        let sg = Sg.set(geometries |> ASet.map (fun (vg,t) -> Sg.ofIndexedGeometry vg |> Sg.trafo (Mod.constant t)))
                |> Sg.fillMode mode
                |> Sg.uniform "LightLocation" (Mod.constant (10.0 * V3d.III))
                |> Sg.surface shaderMod
        sg
