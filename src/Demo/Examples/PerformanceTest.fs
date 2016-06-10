namespace Examples

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Interactive
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg
open Aardvark.SceneGraph.Semantics

module PerformanceTest =


    let run () =

        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateGameWindow(1)

        let initialView = CameraView.LookAt(V3d.III, V3d.OOO, V3d.OOI)
        let perspective = 
            win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
        let cameraView  = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

        let candidates = 
            [| for _ in 0 .. 8 do yield Helpers.box C4b.Red Box3d.Unit |> Sg.ofIndexedGeometry |]

        let scale = 100.0
        let rnd = Random()
        let nextTrafo () =
            let x,y,z = rnd.NextDouble(), rnd.NextDouble(), rnd.NextDouble()
            Trafo3d.Translation(x*scale,y*scale,z*scale) 

        let objects = 
            [ for x in 0 .. 25000 do
                let r = rnd.Next(candidates.Length)
                yield Sg.trafo (nextTrafo () |> Mod.constant) candidates.[r]
            ] |> Sg.group

        let sg =
            objects
                |> Sg.viewTrafo (cameraView  |> Mod.map CameraView.viewTrafo )
                |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo    )
                |> Sg.effect [ DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.simpleLighting |> toEffect ]

        let config = BackendConfiguration.Interpreted
        win.RenderTask <- app.Runtime.CompileRender(win.FramebufferSignature, config, sg.RenderObjects()) |> DefaultOverlays.withStatistics

        win.Run()


(*
unscientific approximate numbers on GTX980
25k objects:
managedOptimized 12  fps => 300k draw calls
nativeoptimized  30  fps => 750k draw calls
glvm/opt         30  fps => 750k draw calls
glvm/nopt        15  fps => 375k draw calls
interpreter      0.22fps => 5.5k draw calls
*)