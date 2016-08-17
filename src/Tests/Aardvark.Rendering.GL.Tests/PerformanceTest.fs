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
open System.Diagnostics
open Aardvark.Rendering.GL
open OpenTK.Graphics.OpenGL4
module PerformanceTest =

    let runConsole () =
        let out = Console.Out
        Console.SetOut(System.IO.TextWriter.Null)


        Aardvark.Init()

        use app = new OpenGlApplication()

        let initialView = CameraView.LookAt(180.0 * V3d.III, V3d.OOO, V3d.OOI)
        let perspective = Mod.constant (Frustum.perspective 60.0 0.1 1000.0 1.0)
        let cameraView  = Mod.constant initialView

        let candidates = 
            [| for _ in 1 .. 9 do yield Helpers.box C4b.Red Box3d.Unit |> Sg.ofIndexedGeometry |]

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

        let sg =
            objects
                |> Sg.viewTrafo (cameraView  |> Mod.map CameraView.viewTrafo )
                |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo    )
                |> Sg.effect [ 
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.constantColor (C4f(1.0,1.0,1.0,0.2)) |> toEffect 
                ]

        let config = BackendConfiguration.NativeOptimized

        let signature =
            app.Runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, RenderbufferFormat.Rgba8
                DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
            ]

        let fbo = app.Runtime.CreateFramebuffer(signature, Mod.constant (V2i(1024, 768)))
        match fbo with
            | :? IOutputMod<IFramebuffer> as m -> m.Acquire()
            | _ -> ()

        let fbo = fbo.GetValue()

        let task = 
            RenderTask.ofList [
                app.Runtime.CompileClear(signature, Mod.constant C4f.Black, Mod.constant 1.0)
                app.Runtime.CompileRender(signature, config, sg.RenderObjects())
            ]

        for i in 1..10 do
            task.Run(fbo) |> ignore


        let sw = System.Diagnostics.Stopwatch()


        let iter = 500
        sw.Start()
        for i in 1 .. iter do
            task.Run(fbo) |> ignore
        sw.Stop()

        let tex = fbo.Attachments.[DefaultSemantic.Colors] |> unbox<BackendTextureOutputView>
        let img = app.Runtime.Download(tex.texture)
        img.SaveAsImage @"C:\Users\Schorsch\Desktop\cubes.jpg"


        let str = sprintf "%.5f" (sw.Elapsed.TotalMilliseconds / float iter)
        out.WriteLine(str)
        //printfn "%.3ffps" (float iter / sw.Elapsed.TotalSeconds)




        ()



    let run () =

        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateGameWindow(1)

        let initialView = CameraView.LookAt(180.0 * V3d.III, V3d.OOO, V3d.OOI)
        let perspective = 
            win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))
        let cameraView  = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

        let candidates = 
            [| for _ in 1 .. 9 do yield Helpers.box C4b.Red Box3d.Unit |> Sg.ofIndexedGeometry |]

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

        let sg =
            objects
                |> Sg.viewTrafo (cameraView  |> Mod.map CameraView.viewTrafo )
                |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo    )
                |> Sg.effect [ 
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.constantColor (C4f(1.0,1.0,1.0,0.2)) |> toEffect 
                ]

        let config = BackendConfiguration.NativeOptimized
        win.RenderTask <- app.Runtime.CompileRender(win.FramebufferSignature, config, sg.RenderObjects()) |> DefaultOverlays.withStatistics

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
                FrameStatistics.Zero
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