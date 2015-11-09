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
            for x in -4..4 do
                for y in -4..4 do
                    yield Trafo3d.Translation(2.0 * float x - 0.5, 2.0 * float y - 0.5, 0.0)
        |]

    let trafos = trafos |> Mod.constant

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

    let cam = DefaultCameraController.control win.Mouse win.Keyboard win.Time cam // |> AFun.integrate controller

    win.Mouse.Click.Values.Subscribe(printfn "click %A") |> ignore
    win.Mouse.DoubleClick.Values.Subscribe(printfn "double click %A") |> ignore

    let e = FShade.SequentialComposition.compose [toEffect Shader.trafo; toEffect Shader.white]
    let s = FShadeSurface(e) :> ISurface
    let compiled = app.Runtime.PrepareSurface s :> ISurface

    let sg =
        geometry 
            |> Sg.instancedGeometry trafos
            |> Sg.viewTrafo (cam |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo proj.ProjectionTrafos.Mod
            |> Sg.surface (Mod.constant compiled)

    let g = Sg.ofIndexedGeometry geometry
    let tex = FileTexture(@"E:\Development\WorkDirectory\DataSVN\pattern.jpg", true) :> ITexture

    let textures = System.Collections.Generic.List<ModRef<ITexture>>()

    let sgs = 
        Sg.group' [
            for x in -4..4 do
                for y in -4..4 do
                    let trafo = Trafo3d.Translation(2.0 * float x - 0.5, 2.0 * float y - 0.5, 0.0)

                    let tex = Mod.init tex
                    textures.Add tex
                    yield g |> Sg.trafo (Mod.constant trafo)
                            |> Sg.texture DefaultSemantic.DiffuseColorTexture tex
        ]
        
//    let test = sgs |> ASet.map id
//    let r = test.GetReader()
//    r.GetDelta() |> List.length |> printfn "got %d deltas"


    let sg = 
        sgs
            |> Sg.viewTrafo (cam |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo proj.ProjectionTrafos.Mod
            |> Sg.effect [toEffect DefaultSurfaces.trafo; toEffect DefaultSurfaces.diffuseTexture]

    let main = app.Runtime.CompileRender(BackendConfiguration.NativeOptimized, sg) // |> DefaultOverlays.withStatistics

    let r = Random()
    win.Keyboard.KeyDown(Keys.Z).Values.Subscribe(fun () ->
        let index = r.Next(textures.Count)
        let t = textures.[index]
        textures.RemoveAt index

        transact (fun () ->
            Mod.change t (FileTexture(@"C:\Users\Schorsch\Development\WorkDirectory\Server\sand_color.jpg", true) :> ITexture)
        )

    ) |> ignore


    win.Keyboard.KeyDown(Keys.G).Values.Subscribe(fun () ->
        System.GC.AddMemoryPressure(100000000L)
        Log.startTimed "GC"
        System.GC.Collect()
        System.GC.WaitForFullGCComplete() |> ignore
        Log.stop()
        System.GC.RemoveMemoryPressure(100000000L)

    ) |> ignore

    win.RenderTask <- RenderTask.ofList [main]
    win.Run()
    0 
