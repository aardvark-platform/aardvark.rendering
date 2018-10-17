open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.SceneGraph
open Aardvark.Rendering.Text
open Aardvark.Application

// This example illustrates how to render a simple triangle using aardvark.

[<EntryPoint>]
let main argv = 
    
    Ag.initialize()
    Aardvark.Init()

    // window { ... } is similar to show { ... } but instead
    // of directly showing the window we get the window-instance
    // and may show it later.
    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug false
            samples 8
        }


    let baseTrafo =
        Mod.init (Trafo3d.FromOrthoNormalBasis(V3d.IOO, V3d.OOI, -V3d.OIO) * Trafo3d.Scale(0.6))

    let suffix =
        Mod.init ""

    let withSuffix str =
        suffix |> Mod.map (fun s -> str + s)

    let withTrafo t =  Mod.map (fun b -> b * t) baseTrafo

    let rand = RandomSystem()

    let texts =
        CSet.ofList [
            baseTrafo :> IMod<_>, withSuffix "Identity"
            withTrafo (Trafo3d.Translation(0.0, 0.0, 3.0)), withSuffix "Up"
            withTrafo (Trafo3d.Translation(3.0, 0.0, 0.0)), withSuffix "Right"
            withTrafo (Trafo3d.Translation(0.0, 3.0, 0.0)), withSuffix "Forward"
        ]


    win.Keyboard.KeyDown(Keys.Space).Values.Add (fun _ ->
        transact (fun () ->
            baseTrafo.Value <- baseTrafo.Value * Trafo3d.Translation(0.5, 0.0, 0.0)
        )
    )

    win.Keyboard.KeyDown(Keys.Enter).Values.Add (fun _ ->
        transact (fun () ->
            suffix.Value <- suffix.Value + "!"
        )
    )

    
    let box = Box3d(-5.0 * V3d.III, 5.0 * V3d.III)
    win.Keyboard.KeyDown(Keys.X).Values.Add (fun _ ->
        transact (fun () ->
            let pos = rand.UniformV3d(box)
            let text = sprintf "%s" (pos.ToString("0.000"))
            texts.Add(withTrafo (Trafo3d.Translation(pos)), withSuffix text) |> ignore
            
        )
    )

    let font = Font "Consolas"

    let shapes =
        Text.Layout(font, C4b.White, TextAlignment.Center, Box2d(V2d(0.0, -0.25), V2d(1.0, 0.5)), "HUGO g | µ\nWAWAWA ||| )(){}\nWAWAW")
           
    let shapes = { shapes with flipViewDependent = false }

    let cfg = 
        {
            font = Font "Times New Roman"
            color = C4b.DarkMagenta
            align = TextAlignment.Right
            flipViewDependent = false
        }
    let sg = Sg.textsWithConfig cfg texts
        //Sg.shapeWithBackground C4b.Red Border2d.None ~~shapes
        //    |> Sg.transform (Trafo3d.FromOrthoNormalBasis(V3d.IOO, V3d.OOI, -V3d.OIO))//Sg.texts font C4b.White texts
    
    //let sg = Sg.markdown MarkdownConfig.light ~~"# bla\n## bla2\n* asd\n* agdgsdfh\n* dfhsdf\n\n\n-------\nai oaisnfasof asf ijasf ijasofi jasofi jasöoijf aosjf aosif aosifj aosuf "


    // show the window
    win.Scene <- sg
    win.Run()

    0
