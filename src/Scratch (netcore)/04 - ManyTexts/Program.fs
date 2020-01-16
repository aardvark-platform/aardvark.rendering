open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
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
            device DeviceKind.Dedicated
            debug false
            samples 8
        }


    let baseTrafo =
        AVal.init (Trafo3d.FromOrthoNormalBasis(V3d.IOO, V3d.OOI, -V3d.OIO) * Trafo3d.Scale(0.6))

    let suffix =
        AVal.init ""

    let withSuffix str =
        suffix |> AVal.map (fun s -> str + s)

    let withTrafo t =  AVal.map (fun b -> b * t) baseTrafo

    let rand = RandomSystem()

    let texts =
        CSet.ofList [
            baseTrafo :> aval<_>, withSuffix "Identity"
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
    win.Keyboard.DownWithRepeats.Values.Add (fun k ->
        match k with
            | Keys.X -> 
                transact (fun () ->
                    let pos = rand.UniformV3d(box)
                    let text = sprintf "%s" (pos.ToString("0.000"))
                    texts.Add(withTrafo (Trafo3d.Translation(pos)), withSuffix text) |> ignore
            
                )
            | _ ->
                ()
    )

    let font = Font "Consolas"

    //let shapes =
    //    Text.Layout(font, TextAlignment.Center, Box2d(V2d(0.0, -0.25), V2d(1.0, 0.5)), "HUGO g | µ\nWAWAWA ||| )(){}\nWAWAW")
           
    //let shapes = { shapes with flipViewDependent = false }

    let cfg : TextConfig = 
        {
            font = Font "Consolas"
            color = C4b.White
            align = TextAlignment.Center
            flipViewDependent = true
            renderStyle       = RenderStyle.NoBoundary
        }
        

    let path = 
        let off = V2d(2,0)
        Path.ofList [
            PathSegment.arcSegment V2d.OI V2d.II V2d.IO
            PathSegment.line V2d.IO V2d.OO 
            PathSegment.line V2d.OO V2d.OI 


            
            PathSegment.line (off + V2d.OI) (off + V2d.IO)
            PathSegment.line (off + V2d.IO) (off + V2d.OO)
            PathSegment.line (off + V2d.OO) (off + V2d.OI)

        ]
    //let shapes =
    //    ShapeList.ofList [
    //        //ConcreteShape.ofPath V2d.Zero V2d.II C4b.Red path
    //        ConcreteShape.fillArcPath C4b.White 0.05 [
    //            V2d( Constant.Sqrt2Half, Constant.Sqrt2Half)
    //            V2d(-Constant.Sqrt2Half, Constant.Sqrt2Half)
    //            V2d( 0.0, 2.0 + Constant.Sqrt2Half)
    //        ]
    //    ]

    let coord =
        Sg.coordinateCross' 4.0
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
            }

    let bias = AVal.init (1.0 / float (1 <<< 22))

    let shapes =
        let dark = C4b(30uy, 30uy, 30uy, 255uy)
        let blue = C4b(0uy, 122uy, 204uy, 255uy)

        texts |> ASet.map (fun (trafo, text) ->
            let shape = 
                text |> AVal.map cfg.Layout |> AVal.map (fun shapes ->
                    let bounds = shapes.bounds.EnlargedBy(V2d(0.05, 0.0))
                    let rect = ConcreteShape.fillRoundedRectangle dark 0.05 bounds
                    let rectb = ConcreteShape.roundedRectangle blue 0.05 0.075 (bounds.EnlargedBy 0.025)
                    let shapes = ShapeList.prepend rect shapes
                    ShapeList.prepend rectb shapes
                )
            trafo, shape
        )

    win.Keyboard.KeyDown(Keys.Add).Values.Add(fun () ->
        transact (fun () -> bias.Value <- bias.Value * 2.0)
        printfn "bias: %.10f" bias.Value
    )
    win.Keyboard.KeyDown(Keys.Subtract).Values.Add(fun () ->
        transact (fun () -> bias.Value <- bias.Value / 2.0)
        printfn "bias: %.10f" bias.Value
    )


    let sg =
        Sg.shapes shapes
            //|> Sg.transform (Trafo3d.FromOrthoNormalBasis(V3d.IOO, V3d.OOI, -V3d.OIO))
            |> Sg.andAlso coord
            |> Sg.uniform "DepthBias" bias
        //let bounds = shapes.bounds.EnlargedBy(V2d(0.05, 0.0))
        //let rect = ConcreteShape.fillRoundedRectangle C4b.White 0.1 bounds
        ////let rectb = ConcreteShape.roundedRectangle C4b.Gray 0.05 0.125 (bounds.EnlargedBy 0.025)
        ////let shapes = ShapeList.prepend rect shapes
        //ShapeList.prepend rect shapes
        //|> AVal.constant
        //|> Sg.shape
        //|> Sg.transform (Trafo3d.FromOrthoNormalBasis(V3d.IOO, V3d.OOI, -V3d.OIO))

        //Sg.shapes shapes
        //Sg.shapes (ASet.ofList [~~Trafo3d.Identity, ~~shapes])
        ////Sg.shape ~~shapes //Sg.textsWithConfig cfg texts
        //    |> Sg.transform (Trafo3d.FromOrthoNormalBasis(V3d.IOO, V3d.OOI, -V3d.OIO))//Sg.texts font C4b.White texts
        //    |> Sg.andAlso coord
            

    //let sg = Sg.markdown MarkdownConfig.light ~~"# bla\n## bla2\n* asd\n* agdgsdfh\n* dfhsdf\n\n\n-------\nai oaisnfasof asf ijasf ijasofi jasofi jasöoijf aosjf aosif aosifj aosuf "


    // show the window
    win.Scene <- sg
    win.Run()

    0
