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
            color = C4b.White
            align = TextAlignment.Center
            flipViewDependent = true
        }

    let shapes = cfg.Layout "HUGO g | µ\nWAWAWA ||| )(){}\nWAWAW"
    let path = 
        let bounds = shapes.bounds.EnlargedBy(1.0, 3.0, 1.0, 1.0)

        let p00 = V2d(bounds.Min.X, bounds.Min.Y)
        let p01 = V2d(bounds.Min.X, bounds.Max.Y)
        let p10 = V2d(bounds.Max.X, bounds.Min.Y)
        let p11 = V2d(bounds.Max.X, bounds.Max.Y)
                                    
        let smaller = bounds.ShrunkBy(0.1)
        let q00 = V2d(smaller.Min.X, smaller.Min.Y)
        let q01 = V2d(smaller.Min.X, smaller.Max.Y)
        let q10 = V2d(smaller.Max.X, smaller.Min.Y)
        let q11 = V2d(smaller.Max.X, smaller.Max.Y)
        
        Path.ofList [
            PathSegment.line p00 p10
            PathSegment.line p10 p11
            PathSegment.line p11 p01
            PathSegment.line p01 p00
                                            
            PathSegment.line q00 q01
            PathSegment.line q01 q11
            PathSegment.line q11 q10
            PathSegment.line q10 q00

        ]

    let path = 
        Path.ofList [
            PathSegment.arc V2d.OI V2d.II V2d.IO
            PathSegment.line V2d.IO V2d.OO 
            PathSegment.line V2d.OO V2d.OI 
        ]
    let path2 = 
        Path.ofList [
            PathSegment.line V2d.OI V2d.II
            PathSegment.line V2d.II (V2d(1.0,0.5))
            PathSegment.arc (V2d(1.0,0.5)) (V2d(0.5, 0.5)) (V2d(0.5,0.0))
            PathSegment.line (V2d(0.5,0.0)) V2d.OO
            PathSegment.line V2d.OO V2d.OI
        ]
  
    let path = 
        Path.ofList [
            PathSegment.arc (V2d(0.0, 2.0)) (V2d(1.0, 6.0)) V2d.IO
            PathSegment.line V2d.IO V2d.OO 
            PathSegment.line V2d.OO V2d.OI 
        ]

    let shapes =
        ShapeList.ofList [
            ConcreteShape.fillEllipse C4b.Green (Ellipse2d(V2d(0.0, 0.0), V2d(1.1,0.0), V2d(0.0, 1.1)))
            ConcreteShape.ellipse C4b.Red 0.05 (Ellipse2d(V2d.Zero, V2d(1.0,0.0), V2d(0.0, 1.0)))

            


            //ConcreteShape.roundedRectangle C4b.Red 0.02 0.05 (Box2d(V2d.Zero, V2d(2.0, 1.0)))
            //ConcreteShape.ofPath V2d.Zero V2d.II C4b.Red path2
            //ConcreteShape.ofPath (V2d(2,0)) V2d.II C4b.Red path2
        ]
        //ShapeList.add shape shapes

    let coord =
        Sg.coordinateCross' 4.0
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
            }

    let sg = 
        Sg.shape ~~shapes //Sg.textsWithConfig cfg texts
            |> Sg.transform (Trafo3d.FromOrthoNormalBasis(V3d.IOO, V3d.OOI, -V3d.OIO))//Sg.texts font C4b.White texts
            |> Sg.andAlso coord
            

    //let sg = Sg.markdown MarkdownConfig.light ~~"# bla\n## bla2\n* asd\n* agdgsdfh\n* dfhsdf\n\n\n-------\nai oaisnfasof asf ijasf ijasofi jasofi jasöoijf aosjf aosif aosifj aosuf "


    // show the window
    win.Scene <- sg
    win.Run()

    0
