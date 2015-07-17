// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

open System
open FShade
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering
open Aardvark.Rendering.NanoVg
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open System.Windows.Media
open System.Windows
open System.Windows.Media.Imaging
open FontRendering


module Shader =
    type Vertex = { 
        [<Position>] pos : V4d 
        [<TexCoord>] tc : V2d
        [<Semantic("ZZZInstanceTrafo")>] trafo : M44d
    }

    let trafo (v : Vertex) =
        vertex {

            let wp = uniform.ModelTrafo * (v.trafo * v.pos)
            return { 
                pos = uniform.ViewProjTrafo * wp
                tc = v.tc
                trafo = v.trafo
            }
        }

    let white (v : Vertex) =
        fragment {
            return V4d.IIII
        }

module Overlays =   
    open Aardvark.Base.Incremental.Operators

    let simple =
        {
            transform = ~~(M33d.Translation(V2d(10.0, 10.0)))
            scissor = ~~Box2d.Infinite
            fillColor = ~~C4f.White
            command = 
                Right {
                    font = ~~(SystemFont("Arial", FontStyle.Regular))
                    size = ~~22.0
                    letterSpacing = ~~0.0
                    lineHeight = ~~1.0
                    blur = ~~0.0
                    align = ~~(TextAlign.Left ||| TextAlign.Top)
                    content = ~~"This is NanoVg working in Aardvark\r\nThis is pretty cool for rendering simple overlays and stuff..."
                }
        }
    


[<EntryPoint>]
let main argv = 
    use app = new OpenGlApplication()
    use win = app.CreateGameWindow()

    Aardvark.Init()

    let cam = CameraViewWithSky(Location = V3d.III * 2.0, Forward = -V3d.III.Normalized)
    let proj = CameraProjectionPerspective(60.0, 0.1, 1000.0, float win.Width / float win.Height)

    let geometry = 
        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexArray = [| 0; 1; 2; 0; 2; 3 |],
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions,                  [| V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO |] :> Array
                    DefaultSemantic.DiffuseColorCoordinates,    [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array
                    DefaultSemantic.Normals,                    [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |] :> Array
                ]
        )



    let trafos =
        [|
            for x in -4..4 do
                for y in -4..4 do
                    yield Trafo3d.Translation(2.0 * float x - 0.5, 2.0 * float y - 0.5, 0.0)
        |]

    let trafos = trafos |> Mod.constant

//    let time = (win :> IRenderTarget).Time
//    let cam = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI
//    let cam = 
//        Mod.integrate cam time [ 
//            DefaultCameraController.controlWSAD win.Keyboard time
//            DefaultCameraController.controlLookAround win.Mouse
//            DefaultCameraController.controlPan win.Mouse
//            DefaultCameraController.controlZoom win.Mouse
//            DefaultCameraController.controllScroll win.Mouse time
//        ]
//

    let cam = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI
    let controller = 
        AFun.chain [
            CameraController.controlLook win.Mouse
            CameraController.controlWSAD win.Keyboard 1.2
            CameraController.controlPan win.Mouse 0.05
            CameraController.controlZoom win.Mouse 0.05
            CameraController.controlScroll win.Mouse 0.1 0.004
//            CameraController.controlOrbit win.Mouse V3d.Zero
//            CameraController.controlOrbitScroll win.Mouse V3d.Zero 0.1 0.004
        ]

    let cam = cam |> AFun.integrate controller

//    let cc = 
//        let impl = win.Control.Implementation
//        CSharpStuff.DefaultCameraControllers(
//            CSharpStuff.HciMouseWinFormsAsync(impl),
//            CSharpStuff.HciKeyboardWinFormsAsync(impl),
//            cam,
//            isEnabled = EventSource true
//        )

    win.Mouse.Click.Values.Subscribe(printfn "click %A") |> ignore
    win.Mouse.DoubleClick.Values.Subscribe(printfn "double click %A") |> ignore

    let sg =
        geometry 
            |> Sg.instancedGeometry trafos
            |> Sg.viewTrafo (cam |> Mod.map CameraView.viewTrafo)
            //|> Sg.viewTrafo cam.ViewTrafos.Mod
            |> Sg.projTrafo proj.ProjectionTrafos.Mod
            |> Sg.effect [toEffect Shader.trafo; toEffect Shader.white]

    let engine = ExecutionEngine.Unmanaged ||| ExecutionEngine.RuntimeOptimized
    let main = app.Runtime.CompileRender(engine, sg) |> DefaultOverlays.withStatistics (Mod.constant C4f.Red)
    let overlay = [Overlays.simple] |> AList.ofList |> app.Runtime.CompileRender
    
    win.RenderTask <- RenderTask.ofList [main]
    
    


//    let f = System.Windows.Media.GlyphTypeface(Uri(@"C:\Windows\Fonts\Arial.ttf"))
//   
//    let glyph = f.CharacterToGlyphMap.[int 'g']
//    let geom = f.GetGlyphOutline(glyph, 16.0, 16.0)
//
//    let outline = geom.GetOutlinedPathGeometry()
//
//    let b = outline.GetRenderBounds(Pen(Brushes.Black, 1.0))
//    let b = Box2i(int b.Left, int b.Bottom, int b.Right, int b.Top)

    win.Run()
    0 // return an integer exit code
