open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Application.WinForms.Vulkan

open System.Threading

open Aardvark.Application.Slim

open Aardvark.Base.Ag
// This example illustrates how to render a simple triangle using aardvark.

open Aardvark.Rendering.Text

module IncrementalExtensions = 
    open System.Threading

    let throttled (name : string) (ms : int) (f : 'a -> unit) (m : IMod<'a>) : IMod<'a> = 
        let v = MVar.empty()
        let r = Mod.init (Mod.force m)
        let f () = 
            while true do
                let () = MVar.take v
                let v = Mod.force m
                f v 
                transact (fun _ -> r.Value <- v)
                Thread.Sleep ms

        let d = m.AddMarkingCallback(fun _ -> MVar.put v ())

        let t = Thread(ThreadStart f)
        t.IsBackground <- true
        t.Name <- name
        t.Start()
        r :> IMod<_>


type WtfRuntimeApplicator(r : IRuntime, child : ISg) =
    inherit Sg.AbstractApplicator(child)
    member x.Runtime = r

[<Semantic>]
type RuntimeSem() =
    member x.Runtime(w : WtfRuntimeApplicator) = w.Child?Runtime <- w.Runtime

module Sg = 
    let applyRuntime (r : IRuntime) (sg : ISg) = WtfRuntimeApplicator(r,sg) :> ISg

[<EntryPoint>]
let main argv = 
    
    Aardvark.Rendering.Vulkan.Config.showRecompile <- false

    // first we need to initialize Aardvark's core components
    Ag.initialize()
    Aardvark.Init()

    // create an OpenGL/Vulkan application. Use the use keyword (using in C#) in order to
    // properly dipose resources on shutdown...
    use app = new VulkanApplication()
    //use app = new OpenGlApplication()
    // SimpleRenderWindow is a System.Windows.Forms.Form which contains a render control
    // of course you can a custum form and add a control to it.
    // Note that there is also a WPF binding for OpenGL. For more complex GUIs however,
    // we recommend using aardvark-media anyways..
    //let win = app.CreateGameWindow(1)
    let signature =
        app.Runtime.CreateFramebufferSignature [
            DefaultSemantic.Colors, RenderbufferFormat.Rgba8
            DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
        ]
        
    let sw = System.Diagnostics.Stopwatch.StartNew()
    let trigger = Mod.custom (fun _ -> sw.Elapsed.TotalSeconds)
    

    let cfg = { Aardvark.Rendering.Text.TextConfig.Default with color = C4b.Red; align = TextAlignment.Center; renderStyle = RenderStyle.NoBoundary }

    //let inputText = Mod.init "aaaa"

    //let text = inputText |> Mod.map (fun t -> cfg.Layout t)


    //let size = V2i(512,512)
    //let color = runtime.CreateTexture(size, TextureFormat.Rgba8, 1, 1)
    //let prepare = 

    //    let depth = runtime.CreateRenderbuffer(size, RenderbufferFormat.Depth24Stencil8, 1)

    //    let signature =
    //        runtime.CreateFramebufferSignature [
    //            DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
    //            DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
    //        ]

    //    let fbo = 
    //        runtime.CreateFramebuffer(
    //            signature, 
    //            Map.ofList [
    //                DefaultSemantic.Colors, ({ texture = color; slice = 0; level = 0 } :> IFramebufferOutput)
    //                DefaultSemantic.Depth, (depth :> IFramebufferOutput)
    //            ]
    //        )
    //    let mutable old : Option<IRenderTask> = None
    //    let t = Mod.init (text |> Mod.force) |> Sg.shape |> Sg.applyRuntime runtime |> Sg.compile runtime signature
    //    fun (s : ShapeList) ->
    //        let o = old
    //        Log.startTimed "compile text"
    //        let s = Sg.shape (Mod.constant s) 
    //        s?Runtime <- runtime
    //        let task = 
    //            s 
    //            |> Sg.applyRuntime runtime 
    //            |> Sg.scale 0.1
    //            |> Sg.viewTrafo (Mod.constant Trafo3d.Identity)
    //            |> Sg.projTrafo (Frustum.ortho (Box3d.Unit) |> Frustum.projTrafo |> Mod.constant)
    //            |> Sg.compile runtime signature
    //        old <- Some task
    //        task.Run(RenderToken.Empty, fbo)
    //        match o with
    //            | None -> ()
    //            | Some o -> o.Dispose()
    //        Log.stop()

    let prepare () = ()


    let text = trigger  |> Mod.map (fun _ -> 
            //Log.startTimed "layout"
            let s = System.Guid.NewGuid() |> string 
            let r = cfg.Layout s
            //Log.stop()
           
            r
          )

    //let text = "jsdfjdsfj"
    //let c = cfg.Layout text
    //let text = Mod.constant c
    //let text = Mod.constant (cfg.Layout "")
    //let text = IncrementalExtensions.throttled "ttext" 400000 prepare text
    let overlay = text |> Sg.shape // |> Sg.scale 0.02

    //let changeThread =
    //    let f () =
    //        while true do
    //            Thread.Sleep 100
    //            transact (fun _ -> inputText.Value <- System.Guid.NewGuid() |> string)
    //    let t = Thread(ThreadStart f)
    //    t.IsBackground <- true
    //    t.Start()
    //    t
    let t = 
        trigger |> Mod.map (fun time -> 
            Trafo3d.RotationZ (time * 0.1)
        )
    let t = Mod.constant Trafo3d.Identity

 
    let rand = Random()



    let pointCount = 10000
    
    let positions = 
        let randomV3f() = V3d(rand.NextDouble(), rand.NextDouble(), rand.NextDouble()) |> V3f.op_Explicit
        let pts =  Array.init pointCount (fun _ -> randomV3f()) :> Array
        trigger |> Mod.map (fun time -> 
            ArrayBuffer(pts.Copy()) :> IBuffer
        )


        
    let vertices = BufferView(positions,typeof<V3f>)
    //let normals = BufferView(ArrayBuffer(packedGeometry.normals) :> IBuffer |> Mod.constant,typeof<V3f>)

    let dyncGen = 
        Sg.draw IndexedGeometryMode.PointList
        |> 
        Sg.vertexBuffer DefaultSemantic.Positions vertices
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.constantColor C4f.White
            }

    let sg = 
        let view = CameraView.lookAt V3d.III V3d.Zero V3d.OOI
        let frustum = Frustum.perspective 60.0 0.1 100.0 1.0
        //dyncGen
        overlay
        |> Sg.trafo t
        |> Sg.viewTrafo (Mod.constant (CameraView.viewTrafo view))
        |> Sg.projTrafo (Mod.constant (Frustum.projTrafo frustum))


    let res = 
        RenderTask.ofList [
            app.Runtime.CompileClear(signature,Mod.constant C4f.Black, Mod.constant 1.0)
            Sg.compile app.Runtime signature sg
        ]
    let fbo = app.Runtime.CreateFramebuffer(signature, Mod.constant (V2i(1024, 768)))
    fbo.Acquire()

    let f = Mod.force fbo

    let sw = System.Diagnostics.Stopwatch()
    while true do
        let took = sw.Elapsed.TotalMilliseconds
        if took > 0.0 then
            if took > 4.0 then
                Console.WriteLine("\rlast frame took: {0}              ", took)
            Console.Write("\rlast frame took: {0}              ", took)
        sw.Restart()
        res.Run(RenderToken.Empty, f)
        transact (fun () -> trigger.MarkOutdated())


    
    //// show the window
    //win.RenderTask <- sg |> Sg.compile runtime win.FramebufferSignature 
    //win.Run()

    0
