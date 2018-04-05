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
    open FShade

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

        let effectRed = 
            Effect.compose [
                DefaultSurfaces.trafo |> toEffect
                DefaultSurfaces.constantColor C4f.Red |> toEffect
            ]

        let effectGreen = 
            Effect.compose [
                DefaultSurfaces.trafo |> toEffect
                DefaultSurfaces.constantColor C4f.Green |> toEffect
                DefaultSurfaces.simpleLighting |> toEffect
            ]

        let effectBlue = 
            Effect.compose [
                DefaultSurfaces.trafo |> toEffect
                DefaultSurfaces.constantColor C4f.Blue |> toEffect
                DefaultSurfaces.diffuseTexture |> toEffect
                DefaultSurfaces.simpleLighting |> toEffect
                
            ]

        let effects = [| effectRed; effectGreen; effectBlue |]
        let effectId = Mod.init 0

        let prepare = r.PrepareEffect(fboSig, effectBlue)

        let compile (config : EffectConfig) =
            let modules = effects |> Array.map (Effect.toModule config)
            let layout = EffectInputLayout.ofModules modules


            let modules = modules |> Array.map (EffectInputLayout.apply layout)

            let currentModule = effectId |> Mod.map (fun i -> modules.[i % modules.Length])

            layout, currentModule

  
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
                let sr = effectId.Value + 1
                Report.Line("switching shader: {0}", sr)

                transact (fun () ->
                    effectId.Value <- sr
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

        let surface (sg : ISg) =
            Sg.SurfaceApplicator(Surface.FShade compile, sg) :> ISg

        let randomColors =
            let rand = RandomSystem()
            let img = PixImage<byte>(Col.Format.RGBA, 32L, 32L)
            img.GetMatrix<C4b>().SetByIndex (fun (i : int64) ->
                rand.UniformC3f().ToC4b()
            ) |> ignore

            PixTexture2d(PixImageMipMap(img), TextureParams.empty) :> ITexture

        let sg = 
            Sg.set(geometries |> ASet.map (fun (vg,t) -> Sg.ofIndexedGeometry vg |> Sg.trafo (Mod.constant t)))
                |> Sg.fillMode mode
                |> Sg.uniform "LightLocation" (Mod.constant (10.0 * V3d.III))
                //|> Sg.surface (prepare :> ISurface)
                |> Sg.texture DefaultSemantic.DiffuseColorTexture (Mod.constant randomColors)
                |> surface
        sg
