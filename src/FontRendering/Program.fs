// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

open System
open FShade
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open System.Windows.Media
open System.Windows
open System.Windows.Media.Imaging

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

[<EntryPoint>]
let main argv = 
    use app = new OpenGlApplication()
    use win = app.CreateSimpleRenderWindow()

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
            for x in 0..10 do
                for y in 0..10 do
                    yield Trafo3d.Translation(2.0 * float x, 2.0 * float y, 0.0)
        |]

    let trafos = trafos |> Mod.initConstant

    let time = (win :> IRenderTarget).Time
    let cam = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI
    let cam = 
        Mod.integrate cam time [ 
            DefaultCameraController.controlWSAD win.Keyboard time
            DefaultCameraController.controlLookAround win.Mouse
            DefaultCameraController.controlPan win.Mouse
            DefaultCameraController.controlZoom win.Mouse
            DefaultCameraController.controllScroll win.Mouse time
        ]


//    let cc = 
//        let impl = win.Control.Implementation
//        CSharpStuff.DefaultCameraControllers(
//            CSharpStuff.HciMouseWinFormsAsync(impl),
//            CSharpStuff.HciKeyboardWinFormsAsync(impl),
//            cam,
//            isEnabled = EventSource true
//        )

    let sg =
        geometry 
            |> Sg.instancedGeometry trafos
            |> Sg.viewTrafo (cam |> Mod.map CameraView.viewTrafo)
            //|> Sg.viewTrafo cam.ViewTrafos.Mod
            |> Sg.projTrafo proj.ProjectionTrafos.Mod
            |> Sg.effect [toEffect Shader.trafo; toEffect Shader.white]

    win.RenderTask <- app.Runtime.CompileRender(sg.RenderJobs())

    
    


//    let f = System.Windows.Media.GlyphTypeface(Uri(@"C:\Windows\Fonts\Arial.ttf"))
//   
//    let glyph = f.CharacterToGlyphMap.[int 'g']
//    let geom = f.GetGlyphOutline(glyph, 16.0, 16.0)
//
//    let outline = geom.GetOutlinedPathGeometry()
//
//    let b = outline.GetRenderBounds(Pen(Brushes.Black, 1.0))
//    let b = Box2i(int b.Left, int b.Bottom, int b.Right, int b.Top)

    System.Windows.Forms.Application.Run win
    0 // return an integer exit code
