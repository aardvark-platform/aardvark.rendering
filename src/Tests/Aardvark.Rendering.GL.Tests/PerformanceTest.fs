namespace PerformanceTests

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
            [ for x in 1 .. 25000 do
                let r = rnd.Next(candidates.Length)
                yield Sg.trafo (nextTrafo () |> Mod.constant) candidates.[r]
            ] |> Sg.group

        let transparency = RenderPass.after "nam" RenderPassOrder.BackToFront RenderPass.main
       
        let sg =
            objects
                |> Sg.viewTrafo (cameraView  |> Mod.map CameraView.viewTrafo )
                |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo    )
                |> Sg.effect [ DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor (C4f(1.0,1.0,1.0,0.2)) |> toEffect ]
                //|> Sg.pass transparency
                //|> Sg.blendMode (Mod.constant BlendMode.Blend)

        let config = BackendConfiguration.NativeOptimized
        win.RenderTask <- app.Runtime.CompileRender(win.FramebufferSignature, config, sg.RenderObjects()) //|> DefaultOverlays.withStatistics

        win.Run()

module RenderTaskPerformance =


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
            [ for x in 0 .. 20 do
                let r = rnd.Next(candidates.Length)
                yield Sg.trafo (nextTrafo () |> Mod.constant) candidates.[r]
            ] |> Sg.group

        let renderObjects = Semantics.RenderObjectSemantics.Semantic.renderObjects objects

        let transparency = RenderPass.after "nam" RenderPassOrder.Arbitrary RenderPass.main
       
        let s = app.Runtime.PrepareEffect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor (C4f(1.0,1.0,1.0,0.2)) |> toEffect ] :> ISurface

        let renderObjects =
            objects
                |> Sg.viewTrafo (cameraView  |> Mod.map CameraView.viewTrafo )
                |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo    )
                |> Sg.surface (Mod.constant s)
                |> Sg.pass transparency
                |> Sg.blendMode (Mod.constant BlendMode.Blend)
                |> Semantics.RenderObjectSemantics.Semantic.renderObjects


        let framebuffers =
            [| for i in 0 .. 6 do 
                let color = app.Runtime.CreateRenderbuffer(win.Sizes.GetValue(),RenderbufferFormat.Rgba8,1) :> IFramebufferOutput
                let depth = app.Runtime.CreateRenderbuffer(win.Sizes.GetValue(),RenderbufferFormat.Depth24Stencil8, 1) :> IFramebufferOutput
                yield
                    app.Runtime.CreateFramebuffer(
                        win.FramebufferSignature, [
                                DefaultSemantic.Colors, color
                                DefaultSemantic.Depth, depth
                            ]
                        )
             |]

        let config = BackendConfiguration.NativeOptimized
        let r = System.Random()
        let renderTasks = 
            [ for i in 0 .. 10 do
                let task = app.Runtime.CompileRender(win.FramebufferSignature, config, renderObjects)
                yield task, framebuffers.[r.Next(framebuffers.Length-1)]
            ]

        let customTask = 
            RenderTask.custom (fun (s,output) ->
                for i in 0 .. 100 do
                    for (r,fbo) in renderTasks do
                        r.Run output.framebuffer |> ignore
                RenderingResult(output.framebuffer, FrameStatistics.Zero)
            )

        win.RenderTask <- DefaultOverlays.withStatistics customTask

        win.Run()
    
    
(*
unscientific approximate numbers on GTX980
25k objects:
managedOptimized 12  fps => 300k draw calls
nativeoptimized  30  fps => 750k draw calls
glvm/opt         30  fps => 750k draw calls
glvm/nopt        15  fps => 375k draw calls
interpreter      0.22fps => 5.5k draw calls


5k sorted
30 => 150k

renderTasks:
6 fps, 100 * 10 per frame * 20 objects => 120k draw calls
*)