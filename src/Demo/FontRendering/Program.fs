// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

open System
open FShade
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Rendering
open Aardvark.Rendering.NanoVg
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open System.Windows.Media
open System.Windows
open FontRendering
open Aardvark.Rendering.Text

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


type CameraMode =
    | Orbit
    | Fly
    | Rotate



[<EntryPoint>]
let main argv = 
    Aardvark.Init()


    use app = new OpenGlApplication()
    use win = app.CreateSimpleRenderWindow(16)
    

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

    let cam = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI

    let mode = Mod.init Fly
    let controllerActive = Mod.init true

    let flyTo = Mod.init Box3d.Invalid

    let chainM (l : IMod<list<afun<'a, 'a>>>) =
        l |> Mod.map AFun.chain |> AFun.bind id

    let controller (loc : IMod<V3d>) (target : IMod<DateTime * V3d>) = 
        adaptive {
            let! active = controllerActive


            // if the controller is active determine the implementation
            // based on mode
            if active then
                
                let! mode = mode



                return [
                    

                    yield CameraControllers.fly target
                    // scroll and zoom 
                    yield CameraControllers.controlScroll win.Mouse 0.1 0.004
                    yield CameraControllers.controlZoom win.Mouse 0.05

                    
                    match mode with
                        | Fly ->
                            // fly controller special handlers
                            yield CameraControllers.controlLook win.Mouse
                            yield CameraControllers.controlWSAD win.Keyboard 5.0
                            yield CameraControllers.controlPan win.Mouse 0.05

                        | Orbit ->
                            // special orbit controller
                            yield CameraControllers.controlOrbit win.Mouse V3d.Zero

                        | Rotate ->
                            
//                            // rotate is just a regular orbit-controller
//                            // with a simple animation rotating around the Z-Axis
                            yield CameraControllers.controlOrbit win.Mouse V3d.Zero
                            yield CameraControllers.controlAnimation V3d.Zero V3d.OOI

                ]
            else
                // if the controller is inactive simply return an empty-list
                // of controller functions
                return []

        } |> chainM

    let resetPos = Mod.init (6.0 * V3d.III)
    let resetDir = Mod.init (DateTime.MaxValue, V3d.Zero)

    //let cam = DefaultCameraController.control win.Mouse win.Keyboard win.Time cam // |> AFun.integrate controller
    let cam = cam |> AFun.integrate (controller resetPos resetDir)

        
//    let test = sgs |> ASet.map id
//    let r = test.GetReader()
//    r.GetDelta() |> List.length |> printfn "got %d deltas"


    let all = "abcdefghijklmnopqrstuvwxyz\r\nABCDEFGHIJKLMNOPQRSTUVWXYZ\r\n1234567890 ?ß\\\r\n^°!\"§$%&/()=?´`@+*~#'<>|,;.:-_µ"
       
    let md = 
        "# Heading1\r\n" +
        "## Heading2\r\n" + 
        "\r\n" +
        "This is ***markdown*** code being parsed by *CommonMark.Net*  \r\n" +
        "It seems to work quite **well**\r\n" +
        "*italic* **bold** ***bold/italic***\r\n" +
        "\r\n" +
        "    type A(a : int) = \r\n" + 
        "        member x.A = a\r\n" +
        "\r\n" + 
        "regular Text again\r\n"+
        "\r\n" +
        "-----------------------------\r\n" +
        "is there a ruler???\r\n" + 
        "1) First *item*\r\n" + 
        "2) second *item*\r\n" +
        "\r\n"+
        "* First *item*  \r\n" + 
        "with multiple lines\r\n" + 
        "* second *item*\r\n" 


    let quad =
        geometry
            |> Sg.ofIndexedGeometry
            |> Sg.effect [ DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.Red |> toEffect ]

    let mode = Mod.init FillMode.Fill
    let font = new Font("Comic Sans")
    let sg = 
        Sg.markdown MarkdownConfig.light (Mod.constant md)
            |> Sg.trafo (Trafo3d.Scale 0.1 |> Mod.constant)
            |> Sg.billboard
            |> Sg.andAlso quad
        //Sg.label font C4b.White (Mod.constant all)
            |> Sg.viewTrafo (cam |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo (win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y) |> Frustum.projTrafo))
            |> Sg.fillMode mode

    win.Keyboard.KeyDown(Keys.F8).Values.Add (fun _ ->
        transact (fun () ->
            match mode.Value with
                | FillMode.Fill -> mode.Value <- FillMode.Line
                | _ -> mode.Value <- FillMode.Fill
        )
    )

    let main = app.Runtime.CompileRender(win.FramebufferSignature, sg) // |> DefaultOverlays.withStatistics
    let clear = app.Runtime.CompileClear(win.FramebufferSignature, Mod.constant C4f.White)

    win.RenderTask <- RenderTask.ofList [clear; main]
    win.Run()
    0 
