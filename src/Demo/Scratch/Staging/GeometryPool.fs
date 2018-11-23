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
open Aardvark.Base.Monads.State
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

module ``Pool Tests`` =
    open FShade
    module Sem =
        let Hugo = Symbol.Create "Hugo"
        let HugoN = Symbol.Create "HugoN"

    type HugoVertex = 
        {
            [<Semantic("Hugo")>] mt : M44d
            //[<Semantic("HugoN")>] nt : M33d
            [<Position>] p : V4d
            [<Normal>] n : V3d
        }

    let hugoShade (v : HugoVertex) =
        vertex {
            return { v 
                with 
                    p = v.mt * v.p 
                    //n = v.nt * v.n
                    n = v.n
            }
        }

    [<Demo("Pooling Test")>]
    let sg ()=
        let r = App.Runtime
        let pool =
            r.CreateManagedPool {
                indexType = typeof<int>
                vertexBufferTypes = 
                    Map.ofList [ 
                        DefaultSemantic.Positions, typeof<V3f>
                        DefaultSemantic.Normals, typeof<V3f> 
                    ]
                uniformTypes = 
                    Map.ofList [ 
                        Sem.Hugo, typeof<M44f> 
                    ]
            }

        let rnd = Random()

        let geometry (pos : V3d) =  
            let trafo = Trafo3d.Scale 0.1 * Trafo3d.Translation pos

            Primitives.unitSphere 3
            //Primitives.unitSphere (rnd.Next(1, 7))
            //Primitives.unitCone (rnd.Next(10, 2000))
                |> AdaptiveGeometry.ofIndexedGeometry [Sem.Hugo, Mod.constant trafo :> IMod] // Sem.HugoN, Mod.constant (M44d.op_Explicit<M33d>(trafo.Backward.Transposed)) :> IMod ]
                |> pool.Add


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

        let sg = 
            Sg.PoolNode(pool, geometries, IndexedGeometryMode.TriangleList)
                |> Sg.fillMode mode
                |> Sg.uniform "LightLocation" (Mod.constant (10.0 * V3d.III))
                |> Sg.effect [
                    hugoShade |> toEffect
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting |> toEffect
                ]
        sg

    [<Demo("Pooling Test (Lazy)")>]
    let sg2 ()=
        let r = App.Runtime
        let pool =
            r.CreateManagedPool {
                indexType = typeof<int>
                vertexBufferTypes = 
                    Map.ofList [ 
                        DefaultSemantic.Positions, typeof<V3f>
                        DefaultSemantic.Normals, typeof<V3f> 
                        DefaultSemantic.DiffuseColorCoordinates, typeof<V3f> 
                    ]
                uniformTypes = 
                    Map.ofList [ 
                        Sem.Hugo, typeof<M44f> 
                        //Sem.HugoN, typeof<M33f> 
                    ]
            }

        let rnd = Random()

        let geometries = CSet.empty
    
        let addRandom() = 
            let pos = V3d( 
                        rnd.NextDouble() * 10.0 - 5.0, 
                        rnd.NextDouble() * 10.0 - 5.0,
                        rnd.NextDouble() * 10.0 - 5.0 )
            let trafo = Trafo3d.Scale 0.1 * Trafo3d.Translation pos
            let ig = Primitives.unitCone (rnd.Next(1000, 50000))
            geometries.Add (ig, trafo) |> ignore

        for i in 0 .. 10 do
            addRandom()

        let initial = geometries.Count

        App.Keyboard.DownWithRepeats.Values.Add(fun k ->
                    
            if k = Keys.R then
                transact (fun () ->
                    geometries.Clear()
                )

            if k = Keys.Z then
                transact (fun () ->
                    
                    Report.Line("adding new random stuff: Seed={0}", rnd.Next())
             
                    for i in 0 .. 10 do
                        addRandom()
                )

                Report.Line("new geometry count: {0}", geometries.Count)
                
        )

        let addToPool(ag : AdaptiveGeometry) = 
            
            Report.BeginTimed("add to pool: vc={0}", ag.vertexCount)
            Report.Line("ManagedBuffer.Set takes super long to fill up missing DiffuseColorCoordinates with 0")
            let mdc = pool.Add ag
            Report.End() |> ignore
            mdc

        let geometriesLazy = 
            geometries |> ASet.map (fun (ig, trafo) -> ig
                                                        |> AdaptiveGeometry.ofIndexedGeometry [ (Sem.Hugo, (Mod.constant trafo :> IMod)); (Sem.HugoN, Mod.constant (trafo.Backward.Transposed) :> IMod) ]
                                                        |> addToPool)

        // initial evaluation
        ASet.toArray geometriesLazy |> ignore

        let sg = 
            Sg.PoolNode(pool, geometriesLazy, IndexedGeometryMode.TriangleList)
                |> Sg.uniform "LightLocation" (Mod.constant (10.0 * V3d.III))
                |> Sg.effect [
                    hugoShade |> toEffect
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting |> toEffect
                ]
        sg







   