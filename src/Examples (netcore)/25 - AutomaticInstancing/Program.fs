open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO
open Aardvark.Rendering.Text
open Aardvark.Application

[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    Ag.initialize()
    Aardvark.Init()
    
    let win =
        window {
            backend Backend.GL
            display Display.Stereo
            debug false
            samples 8
        }

    // lets define the bounds/color for our box
    // NOTE that the color is going to be ignored since we're using a texture
    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

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
        

    let eigi = 
        Loader.Assimp.load @"C:\Users\Schorsch\Desktop\raptor\raptor.dae"
            |> Sg.adapter
            |> Sg.transform (Trafo3d.RotateInto(V3d.OIO, V3d.OOI))
            |> Sg.scale 60.0
            |> Sg.translate 0.6 0.6 0.0
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.diffuseTexture
                do! DefaultSurfaces.simpleLighting
            }

    let size = Mod.init 1
    let showEigi = Mod.init false

    win.Keyboard.DownWithRepeats.Values.Add (fun k ->
        match k with
        | Keys.OemPlus -> transact (fun () -> size.Value <- size.Value + 5)
        | Keys.OemMinus -> transact (fun () -> size.Value <- max 1 (size.Value - 5))
        | Keys.Space -> transact (fun () -> showEigi.Value <- not showEigi.Value)
        | _ -> ()
    )

    let trafos =
        let step = 2.0
        size |> Mod.map (fun s ->
            let size = step * float s
            let off = -size / 2.0

            [|
                for x in 0 .. s - 1 do
                    for y in 0 .. s - 1 do
                        let pos = V2d(x,y) * step + V2d(off, off)
                        yield Trafo3d.Translation(V3d(pos.X, pos.Y,0.0))

            |]
        )

    let set =
        aset {
            yield coordinateCross
            yield eigi |> Sg.onOff showEigi
        }

    let count =
        let cfg =
            {
                align = TextAlignment.Center
                font = Font "Consolas"
                color = C4b.Black
                flipViewDependent = true
            }

        size 
        |> Mod.map (fun s -> s * s |> sprintf "count: %d") 
        |> Mod.map (fun s ->
            let shapes = Text.Layout(cfg, s)

            let border = ConcreteShape.fillRoundedRectangle C4b.White 0.1 (shapes.bounds.EnlargedBy 0.1)

            ShapeList.prepend border shapes
        )
        |> Sg.shape
        |> Sg.transform(Trafo3d.RotateInto(V3d.OIO, V3d.OOI))
        |> Sg.translate 0.0 0.0 4.0

    let sg =
        Sg.set set
        |> Sg.instanced trafos
        |> Sg.andAlso count



    win.Scene <- sg
    win.Run()
    
    

    0
