open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application


module Shader =

    open FShade
    

    type Vertex =
        {
            [<Position>]
            position : V4d
            [<Semantic("Offset")>]
            offset : V4d
        }

    let instanceTrafo (v : Vertex) =
        vertex {
            return 
                { v with 
                    position = V4d(v.position.XYZ + v.offset.XYZ,1.0)
                }
        }

[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    
    Aardvark.Init()

    Aardvark.Rendering.GL.RuntimeConfig.SupressSparseBuffers <- true
    
    let geometries = 
        [
            IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.White 
            IndexedGeometryPrimitives.Sphere.solidPhiThetaSphere Sphere3d.Unit 10 C4b.Gray
            IndexedGeometryPrimitives.Sphere.solidPhiThetaSphere Sphere3d.Unit 10 C4b.Red
            IndexedGeometryPrimitives.Sphere.solidPhiThetaSphere Sphere3d.Unit 10 C4b.Green
            IndexedGeometryPrimitives.Sphere.solidPhiThetaSphere Sphere3d.Unit 10 C4b.Blue
        ] |> List.map (fun ig -> ig.Flat) |> List.toArray

    
    let signature =
        {
            mode = IndexedGeometryMode.TriangleList
            vertexTypes = Map.ofList [DefaultSemantic.Positions, typeof<V3f>; DefaultSemantic.Colors, typeof<C4b>; DefaultSemantic.Normals, typeof<V3f>; ]
            uniformTypes = Map.ofList ["Offset", typeof<V4f>]
        }

    let showScene = AVal.init true
    let scale = AVal.init 1.0f

    let rnd = RandomSystem()
    let set : cset<IndexedGeometry * V4f> = 
        cset(
            List.init 10 (fun _ ->    
                let pos = V4d(rnd.UniformV3d(),1.0).ToV4f() * 5.0f
                let geo = rnd.UniformInt(geometries.Length)
                (geometries.[geo], pos)
            )            
        )

    let node = Sg.indirect signature (set |> ASet.map (fun (ig,pos) -> ig,Map.ofList ["Offset", scale |> AVal.map (fun s -> (pos * s * V4f.One)) :> IAdaptiveValue]))

    let sg =
        node 
        |> Sg.shader {
             do! Shader.instanceTrafo
             do! DefaultSurfaces.trafo
             do! DefaultSurfaces.vertexColor
             do! DefaultSurfaces.simpleLighting
           }
    
    let sg =
        Sg.dynamic (showScene |> AVal.map (function true -> sg | _ -> Sg.empty))


    let win = window {
        backend Backend.GL
        display Display.Mono
        debug false
        samples 8
    }


    win.Keyboard.DownWithRepeats.Values.Add(fun k -> 
        match k with
            | Keys.Subtract | Keys.OemMinus -> 
                if set.Count > 0 then
                    transact (fun _ -> 
                        let geo = rnd.UniformInt(set.Count)
                        let el = Seq.item geo (AVal.force (set :> aset<_>).Content)
                        set.Remove(el) |> ignore
                    )
                printfn "count: %A" set.Count
            | Keys.Add | Keys.OemPlus -> 
                transact (fun _ -> 
                    let pos = V4d(rnd.UniformV3d(),1.0).ToV4f() * 5.0f
                    let geo = rnd.UniformInt(geometries.Length)
                    set.Add((geometries.[geo], pos)) |> ignore
                )
                printfn "count: %A" set.Count
            | Keys.Space -> 
                transact (fun _ -> 
                    scale.Value <- scale.Value + 0.2f
                )
            | Keys.Multiply | Keys.P -> 
                transact (fun _ -> 
                    for i in 1 .. 100 do
                        let pos = V4d(rnd.UniformV3d(),1.0).ToV4f() * 5.0f
                        let geo = rnd.UniformInt(geometries.Length)
                        set.Add((geometries.[geo], pos)) |> ignore
                )
                printfn "count: %A" set.Count
            | Keys.Delete -> 
                transact (fun _ -> 
                    set.Clear()
                )
            | Keys.PageUp | Keys.Space ->
                transact (fun () -> showScene.Value <- not showScene.Value)

            | _ -> ()
    ) 


    win.Scene <- sg
    win.Run()
    0
