open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

module Shaders =
    open FShade

    type UniformScope with
        member x.Colors : V4f[] = uniform?StorageBuffer?Colors
        member x.ColorCount : int = uniform?ColorCount

    type ColorIndexAttribute() =
        inherit SemanticAttribute("ColorIndex")

    type InstanceVertex = {
        [<Color>]        Color : V4f
        [<ColorIndex>]   ColorIndex : int
    }

    let bufferColor (v : InstanceVertex) =
        vertex {
            return { v with Color = uniform.Colors.[min v.ColorIndex (uniform.ColorCount - 1)] }
        }


module Scene =
    type ColoredObject =
        {
            Position : V3d
            Color : int
        }

    let data = clist [|
        { Position = V3d.Zero; Color = 0 }
        { Position = V3d(3, 4, 1); Color = 0 }
        { Position = V3d(-4, 2, 2); Color = 0 }
        { Position = V3d(1, -1, -2); Color = 0 }
    |]

    let randomize (count : int) (rnd : RandomSystem) =
        data.Value <-
            data.Value |> IndexList.map (fun o ->
                { o with Color = rnd.UniformInt(count) }
            )

    let drawInstanced (sg : ISg) =
        let getArray (mapping : ColoredObject -> 'a) =
            data
            |> AList.map mapping |> AList.toAVal
            |> AVal.map (IndexList.toArray >> unbox<System.Array>)

        let trafos =
            getArray (fun o -> Trafo3d.Translation o.Position)

        let colors =
            getArray (fun o -> o.Color)

        sg |> Sg.instanced' (Map.ofList [
            "ModelTrafo", (typeof<Trafo3d>, trafos)
            "ColorIndex", (typeof<int>,     colors)
        ])


[<EntryPoint>]
let main argv =
    Aardvark.Init()

    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug false
            samples 8
        }

    let rnd = RandomSystem()
    let colors = AVal.init [| C4f.Azure.ToV4f() |]

    win.Keyboard.DownWithRepeats.Values.Add (fun k ->
        match k with
        | Keys.Enter ->
            transact (fun _ ->
                Scene.randomize colors.Value.Length rnd
                Log.warn "randomized color indices"
            )

        | Keys.Space ->
            let c = V4f(rnd.UniformV3f(), 1.0f)
            transact (fun _ ->
                colors.Value <- Array.append colors.Value[|c|]
                Log.warn "using %d colors" colors.Value.Length
            )

        | Keys.Delete ->
            transact (fun _ ->
                let arr = colors.Value

                if arr.Length > 1 then
                    colors.Value <- arr |> Array.take (arr.Length - 1)
                    Log.warn "using %d colors" colors.Value.Length
            )
        | _ -> ()
    )

    let box = Box3d(-V3d.III, V3d.III)

    let sg =
        Sg.box (AVal.constant C4b.White) (AVal.constant box)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! Shaders.bufferColor
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
            }
            |> Scene.drawInstanced
            |> Sg.uniform "Colors" colors
            |> Sg.uniform "ColorCount" (colors |> AVal.map Array.length)

    win.Scene <- sg
    win.Run()

    0
