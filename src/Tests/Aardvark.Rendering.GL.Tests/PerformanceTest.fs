namespace PerformanceTests

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Interactive
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.SceneGraph.Semantics
open System.Diagnostics
open Aardvark.Rendering.GL
open OpenTK.Graphics.OpenGL4


module RandomCubesPerformanceTest =

    let runConsole () =
        let out = Console.Out
        Console.SetOut(System.IO.TextWriter.Null)


        Aardvark.Init()

        use app = new OpenGlApplication()

        let initialView = CameraView.LookAt(180.0 * V3d.III, V3d.OOO, V3d.OOI)
        let perspective = AVal.constant (Frustum.perspective 60.0 0.1 1000.0 1.0)
        let cameraView  = AVal.constant initialView

        let candidates = 
            [| for _ in 1 .. 9 do yield Sg.box' C4b.Red Box3d.Unit |]

        let scale = 100.0
        let rnd = Random()
        let nextTrafo () =
            let x,y,z = rnd.NextDouble(), rnd.NextDouble(), rnd.NextDouble()
            Trafo3d.Translation(x*scale,y*scale,z*scale) 

        let objects = 
            [ for x in 1 .. 25000 do
                let r = rnd.Next(candidates.Length)
                yield Sg.trafo (nextTrafo () |> AVal.constant) candidates.[r]
            ] |> Sg.ofList

        let sg =
            objects
                |> Sg.viewTrafo (cameraView  |> AVal.map CameraView.viewTrafo )
                |> Sg.projTrafo (perspective |> AVal.map Frustum.projTrafo    )
                |> Sg.effect [ 
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.constantColor (C4f(1.0,1.0,1.0,0.2)) |> toEffect 
                ]

        let config = BackendConfiguration.Native

        let signature =
            app.Runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, RenderbufferFormat.Rgba8
                DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
            ]

        let fbo = app.Runtime.CreateFramebuffer(signature, AVal.constant (V2i(1024, 1024)))
        fbo.Acquire()

        let fbo = fbo.GetValue()

        let task = 
            RenderTask.ofList [
                app.Runtime.CompileClear(signature, AVal.constant C4f.Black, AVal.constant 1.0)
                app.Runtime.CompileRender(signature, config, sg.RenderObjects())
            ]

        for i in 1..10 do
            task.Run(RenderToken.Empty, fbo)


        let sw = System.Diagnostics.Stopwatch()


        let iter = 500
        sw.Start()
        for i in 1 .. iter do
            task.Run(RenderToken.Empty, fbo)
        sw.Stop()

        // let tex = fbo.Attachments.[DefaultSemantic.Colors] |> unbox<BackendTextureOutputView>
        // let img = app.Runtime.Download(tex.texture)
        // img.SaveAsImage @"C:\Users\Schorsch\Desktop\cubes.jpg"


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
            win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))
        let cameraView  = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

        let candidates = 
            [| for _ in 1 .. 9 do yield Sg.box' C4b.Red Box3d.Unit  |]

        let scale = 100.0
        let rnd = Random()
        let nextTrafo () =
            let x,y,z = rnd.NextDouble(), rnd.NextDouble(), rnd.NextDouble()
            Trafo3d.Translation(x*scale,y*scale,z*scale) 

        let objects = 
            [ for x in 1 .. 25000 do
                let r = rnd.Next(candidates.Length)
                yield Sg.trafo (nextTrafo () |> AVal.constant) candidates.[r]
            ] |> Sg.ofList

        let sg =
            objects
                |> Sg.viewTrafo (cameraView  |> AVal.map CameraView.viewTrafo )
                |> Sg.projTrafo (perspective |> AVal.map Frustum.projTrafo    )
                |> Sg.effect [ 
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.constantColor (C4f(1.0,1.0,1.0,0.2)) |> toEffect 
                ]

        let config = BackendConfiguration.Native
        win.RenderTask <- app.Runtime.CompileRender(win.FramebufferSignature, config, sg.RenderObjects())

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

module RenderTaskPerformance =


    let run () =
        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateGameWindow(1)

        let initialView = CameraView.LookAt(V3d.III, V3d.OOO, V3d.OOI)
        let perspective = 
            win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
        let cameraView  = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

        let candidates = 
            [| for _ in 0 .. 8 do yield Sg.box' C4b.Red Box3d.Unit  |]

        let scale = 100.0
        let rnd = Random()
        let nextTrafo () =
            let x,y,z = rnd.NextDouble(), rnd.NextDouble(), rnd.NextDouble()
            Trafo3d.Translation(x*scale,y*scale,z*scale) 

        let objects = 
            [ for x in 0 .. 20 do
                let r = rnd.Next(candidates.Length)
                yield Sg.trafo (nextTrafo () |> AVal.constant) candidates.[r]
            ] |> Sg.ofList

        let renderObjects = Semantics.RenderObjectSemantics.Semantic.renderObjects objects

        let transparency = RenderPass.after "nam" RenderPassOrder.Arbitrary RenderPass.main
       
        let s = app.Runtime.PrepareEffect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor (C4f(1.0,1.0,1.0,0.2)) |> toEffect ] :> ISurface

        let renderObjects =
            objects
                |> Sg.viewTrafo (cameraView  |> AVal.map CameraView.viewTrafo )
                |> Sg.projTrafo (perspective |> AVal.map Frustum.projTrafo    )
                |> Sg.surface s
                |> Sg.pass transparency
                |> Sg.blendMode (AVal.constant BlendMode.Blend)
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

        let config = BackendConfiguration.Native
        let r = System.Random()
        let renderTasks = 
            [ for i in 0 .. 10 do
                let task = app.Runtime.CompileRender(win.FramebufferSignature, config, renderObjects)
                yield task, framebuffers.[r.Next(framebuffers.Length-1)]
            ]

        let customTask = 
            RenderTask.custom (fun (s,t,output) ->
                for i in 0 .. 100 do
                    for (r,fbo) in renderTasks do
                        r.Run(t, output.framebuffer)
            )

        win.RenderTask <- customTask

        win.Run()
    
module StartupPerformance =

    let runConsole args =

        let out = Console.Out
        //Console.SetOut(IO.TextWriter.Null)
        
        let n,wantedConfig = 
            match args with
                |  [| n; config; |] -> Int32.Parse n, Int32.Parse config
                | _ -> failwith "wrong args"

        Aardvark.Init()

        use app = new OpenGlApplication()
        
        let cameraView = CameraView.LookAt(180.0 * V3d.III, V3d.OOO, V3d.OOI)
        let cameraProj = Frustum.perspective 60.0 0.1 1000.0 1.0
                
        let colorBuffer = app.Runtime.CreateTexture(V2i(1024, 1024), TextureFormat.Rgba8, 1, 1, 1)
        let depthBuffer = app.Runtime.CreateRenderbuffer(V2i(1024, 1024), RenderbufferFormat.Depth24Stencil8, 1)

        let fboSig = 
            app.Runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
                DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
            ]


        let fbo = 
            app.Runtime.CreateFramebuffer(fboSig, [
                                            DefaultSemantic.Colors, { texture = colorBuffer; level = 0; slice = 0 } :> IFramebufferOutput
                                            DefaultSemantic.Depth, depthBuffer :> IFramebufferOutput
                                        ])

        let effect = 
            using app.Context.ResourceLock (fun _ -> 
                app.Runtime.PrepareEffect(fboSig, [ DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor (C4f(1.0,1.0,1.0,0.2)) |> toEffect ]) :> ISurface
            )

        let test (n : int, cfg : BackendConfiguration) =

            Report.BeginTimed("{0} objects one-shot rendering", n)

            Report.BeginTimed("Generating Geometries")
            let geo = Sg.sphere' 2 C4b.Red 1.0
            let objects = 
                [| for _ in 1 .. n do yield geo |]
            Report.End() |> ignore

            let scale = 100.0
            let rnd = Random()
            let nextTrafo () =
                let x,y,z = rnd.NextDouble(), rnd.NextDouble(), rnd.NextDouble()
                Trafo3d.Translation(x*scale,y*scale,z*scale) 

            let objectsSg = 
                [ for x in objects do
                    yield Sg.trafo (nextTrafo () |> AVal.constant) x
                ] |> Sg.ofList

            let sg =
                objectsSg
                    |> Sg.viewTrafo (AVal.constant cameraView.ViewTrafo)
                    |> Sg.projTrafo (AVal.constant (Frustum.projTrafo cameraProj))
                    |> Sg.surface effect

            Report.BeginTimed("Gathering render objects")
            let renderObjects = ASet.force (sg.RenderObjects())
            Report.End() |> ignore

            Report.BeginTimed("Preparing render objects")
            let preparedRenderObjects = renderObjects.Map (fun x -> app.Runtime.PrepareRenderObject(fboSig, x) :> IRenderObject)
            Report.End() |> ignore

            printfn "start your engine!!!"
            Console.ReadLine() |> ignore
            let renderTask = app.Runtime.CompileRender(fboSig, cfg, ASet.ofHashSet preparedRenderObjects)
            let sw = System.Diagnostics.Stopwatch()
            Report.BeginTimed("Preparing Task")
            sw.Start()
            renderTask.Run(RenderToken.Empty, fbo)
            Report.End() |> ignore

            Report.BeginTimed("RenderTask Execution 1")
            renderTask.Run(RenderToken.Empty, fbo)
            sw.Stop()
            Report.End() |> ignore

            Report.BeginTimed("RenderTask Execution 2")
            let rs = RenderToken()
            renderTask.Run(rs, fbo)
            Report.End() |> ignore

            Report.Line("Stats: DC={0} Instr={1} Prim={2}", rs.DrawCallCount, rs.TotalInstructions, rs.PrimitiveCount)

            Report.End() |> ignore
            Report.Line()

            sw.Elapsed.TotalSeconds

        let config = BackendConfiguration.Native
        
        let config = 
            match wantedConfig with
                | 0 -> BackendConfiguration.Native
                //| 1 -> BackendConfiguration.NativeUnoptimized
                //| 2 -> BackendConfiguration.UnmanagedOptimized
                //| 3 -> BackendConfiguration.UnmanagedUnoptimized
                //| 4 -> BackendConfiguration.ManagedOptimized
                //| 5 -> BackendConfiguration.ManagedUnoptimized
                | _ -> failwith "unknown config"
    

        if true then
            let initialRun = test(n, config)

            Console.SetOut(out)
            printfn "%d;%d;%f" wantedConfig n initialRun

        else 
            test(50000, config) |> ignore

module IsActiveFlagPerformance = 
    
    open FShade

    let run args =

        let out = Console.Out
        //Console.SetOut(IO.TextWriter.Null)
        
        let n,wantedConfig = 
            match args with
                |  [| n; config; |] -> Int32.Parse n, Int32.Parse config
                | _ -> failwith "wrong args"

        Aardvark.Init()

        use app = new OpenGlApplication()
        
        let cameraView = CameraView.LookAt(180.0 * V3d.III, V3d.OOO, V3d.OOI)
        let cameraProj = Frustum.perspective 60.0 0.1 1000.0 1.0
                
        let colorBuffer = app.Runtime.CreateTexture(V2i(1024, 1024), TextureFormat.Rgba8, 1, 1, 1)
        let depthBuffer = app.Runtime.CreateRenderbuffer(V2i(1024, 1024), RenderbufferFormat.Depth24Stencil8, 1)

        let fboSig = 
            app.Runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
                DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
            ]


        let fbo = 
            app.Runtime.CreateFramebuffer(fboSig, [
                                            DefaultSemantic.Colors, { texture = colorBuffer; level = 0; slice = 0 } :> IFramebufferOutput
                                            DefaultSemantic.Depth, depthBuffer :> IFramebufferOutput
                                        ])

        let bla (v : Effects.Vertex)= 
            fragment {
                let a : V4d = uniform?Blah
                return a
            }

        let effect = app.Runtime.PrepareEffect(fboSig, [ DefaultSurfaces.trafo |> toEffect; bla |> toEffect ]) :> ISurface

        let test (n : int, cfg : BackendConfiguration) =

            Report.BeginTimed("{0} objects one-shot rendering", n)

            Report.BeginTimed("Generating Geometries")

            let r = System.Random()
            let isActives = Array.init n (fun _ -> AVal.init (r.NextDouble() > 0.5))
            let sphere = Sg.sphere' 2 C4b.Red 1.0
            let objects = 
                [| for i in 1 .. n - 1 do 
                    let sg = Sg.onOff isActives.[i] sphere
                    yield Sg.uniform "Blah" (AVal.init C4b.Red) sg 
                |]
            Report.End() |> ignore

            let scale = 100.0
            let rnd = Random()
            let nextTrafo () =
                let x,y,z = rnd.NextDouble(), rnd.NextDouble(), rnd.NextDouble()
                Trafo3d.Translation(x*scale,y*scale,z*scale) 

            let objectsSg = 
                [ for x in objects do
                    yield Sg.trafo (nextTrafo () |> AVal.constant) x
                ] |> Sg.ofList

            let sg =
                objectsSg
                    |> Sg.viewTrafo (AVal.constant cameraView.ViewTrafo)
                    |> Sg.projTrafo (AVal.constant (Frustum.projTrafo cameraProj))
                    |> Sg.surface effect

            Report.BeginTimed("Gathering render objects")
            let renderObjects = ASet.force (sg.RenderObjects())
            Report.End() |> ignore

            Report.BeginTimed("Preparing render objects")
            let preparedRenderObjects = renderObjects.Map (fun x -> app.Runtime.PrepareRenderObject(fboSig, x) :> IRenderObject)
            Report.End() |> ignore

            printfn "start your engine!!!"
            //Console.ReadLine() |> ignore

            let secondRenderTask = app.Runtime.CompileRender(fboSig, cfg, ASet.ofHashSet preparedRenderObjects)
            secondRenderTask.Run(RenderToken.Empty, fbo)
            secondRenderTask.Dispose()

            let renderTask = app.Runtime.CompileRender(fboSig, cfg, ASet.ofHashSet preparedRenderObjects)
            

            let sw = System.Diagnostics.Stopwatch()
            Report.BeginTimed("RenderTask Execution 1")
            sw.Start()
            renderTask.Run(RenderToken.Empty, fbo)
            sw.Stop()
            Report.End() |> ignore

            let renderOnce () =
                sw.Restart()
                renderTask.Run(RenderToken.Empty, fbo)
                sw.Stop()
                sw.Elapsed.TotalSeconds


            let secondFrame = renderOnce() 
            printfn "Second frame took: %f seconds" secondFrame 

            renderOnce, isActives

        
        let n = 50000
        let renderFrame,isActiveFlags = test (n, BackendConfiguration.Native)

        let path = "isActivePerformance4.Managed.csv"
        if System.IO.File.Exists(path) |> not then
            System.IO.File.WriteAllLines(path, [| "n;time(s);time transact (s);" |])


        let sw = System.Diagnostics.Stopwatch()
        let r = System.Random()
        for i in 0 .. 1000 .. n - 1 do
            sw.Restart()
            transact (fun () -> 
            for o in 0 .. i - 1 do
                isActiveFlags.[o].Value <- (not isActiveFlags.[o].Value)
            )
            sw.Stop()
            let elapsed = renderFrame()
            printfn "changing %d isActiveFlags took: %f, transact: %f s" i elapsed sw.Elapsed.TotalSeconds
            System.IO.File.AppendAllLines(path,[| sprintf "%d;%f;%f" i elapsed sw.Elapsed.TotalSeconds |])

    