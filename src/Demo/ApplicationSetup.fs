namespace Demo

open System
open OpenTK
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering.GL
open Aardvark.SceneGraph
open Aardvark.Base.Incremental
open System.Threading.Tasks
open System.Threading


type OpenGlApplication() =
    
    static let mutable appCreated = false

    do OpenTK.Toolkit.Init(new ToolkitOptions(Backend=OpenTK.PlatformBackend.PreferNative)) |> ignore
    let runtime = new Runtime()
    let ctx = new Context(runtime)
    do runtime.Context <- ctx

    member x.Context = ctx
    member x.Runtime = runtime

    member x.Dispose() =
        runtime.Dispose()
        ctx.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type GameWindow(a : OpenGlApplication) =
    inherit OpenTK.GameWindow()


    let mutable task : Option<IRenderTask> = None
    let fbo = new Framebuffer(a.Context, (fun _ -> 0), ignore, [], None)

    do base.VSync <- VSyncMode.Off
       base.Context.MakeCurrent(null)
    let ctx = ContextHandle(base.Context, base.WindowInfo)

    member x.RenderTask 
        with get() = task.Value
        and set v = task <- Some v

    override x.OnRenderFrame(e) =
        using (a.Context.RenderingLock ctx) (fun _ ->
            fbo.Size <- V2i(x.ClientSize.Width, x.ClientSize.Height)

            GL.Viewport(0,0,x.ClientSize.Width, x.ClientSize.Height)
            GL.ClearColor(0.0f,1.0f,0.0f,0.0f)
            GL.ClearDepth(1.0)
            GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)
        
            match task with
                | Some t -> t.Run(fbo) |> ignore
                | _ -> ()

            x.SwapBuffers()
        )

module WinForms = 
    open System.Windows.Forms
    open System.Collections.Concurrent
    open System.Threading
    open System.Diagnostics

    do Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException)

    type IControl =
        abstract member Paint : unit -> unit
        abstract member Invoke : (unit -> unit) -> unit

    type RunningMean(maxCount : int) =
        let values = Array.zeroCreate maxCount
        let mutable index = 0
        let mutable count = 0
        let mutable sum = 0.0

        member x.Add(v : float) =
            let newSum = 
                if count < maxCount then 
                    count <- count + 1
                    sum + v
                else 
                    sum + v - values.[index]

            sum <- newSum
            values.[index] <- v
            index <- (index + 1) % maxCount
              
        member x.Average =
            if count = 0 then 0.0
            else sum / float count  

    type Periodic(interval : int, f : float -> unit) =
        let times = RunningMean(100)
        let sw = Stopwatch()

        member x.RunIfNeeded() =
            if not sw.IsRunning then
                sw.Start()
            else
                let dt = sw.Elapsed.TotalMilliseconds
               
                if interval = 0 || dt >= float interval then
                    times.Add dt
                    sw.Restart()
                    f(times.Average / 1000.0)
                

    type MessageLoop() as this =

        let q = ConcurrentBag<IControl>()
        let mutable timer : Timer = null
        let periodic = ConcurrentHashSet<Periodic>()

        let rec processAll() =
            match q.TryTake() with
                | (true, ctrl) ->
                    ctrl.Invoke (fun () -> ctrl.Paint())
                    processAll()

                | _ -> ()

        member private x.Process() =
            Application.DoEvents()
            for p in periodic do p.RunIfNeeded()
            processAll()

        member x.Start() =
            if timer <> null then
                timer.Dispose()

            timer <- new Timer(TimerCallback(fun _ -> this.Process()), null, 0L, 2L)

        member x.Draw(c : IControl) =
            q.Add c 

        member x.EnqueuePeriodic (f : float -> unit, intervalInMilliseconds : int) =
            let p = Periodic(intervalInMilliseconds, f)
            periodic.Add p |> ignore

            { new IDisposable with
                member x.Dispose() =
                    periodic.Remove p |> ignore
            }
            
        member x.EnqueuePeriodic (f : float -> unit) =
            x.EnqueuePeriodic(f, 1)
                        
                

    type OpenGlControl(ctx : Context, samples : int) =

        inherit GLControl(
            Graphics.GraphicsMode(
                OpenTK.Graphics.ColorFormat(Config.BitsPerPixel), 
                Config.DepthBits, 
                Config.StencilBits, 
                samples, 
                OpenTK.Graphics.ColorFormat.Empty,
                Config.Buffers, 
                false
            ), 
            Config.MajorVersion, 
            Config.MinorVersion, 
            Config.ContextFlags, 
            VSync = false
        )

        static let messageLoop = MessageLoop()
        static do messageLoop.Start()


        let mutable loaded = false
        let sizes = EventSource<V2i>(V2i(base.ClientSize.Width, base.ClientSize.Height))
        let statistics = EventSource<FrameStatistics>(FrameStatistics.Zero)

        let mutable task : Option<IRenderTask> = None
        let mutable taskSubscription : IDisposable = null

        let mutable contextHandle : ContextHandle = null //ContextHandle(base.Context, base.WindowInfo)
        let defaultFramebuffer = new Framebuffer(ctx, (fun _ -> 0), ignore, [], None)

        let afterRender = Microsoft.FSharp.Control.Event<unit>()
        let keyDown = Microsoft.FSharp.Control.Event<KeyEventArgs>()

        

        static member MessageLoop = messageLoop

        [<CLIEvent>]
        member x.AfterRender = afterRender.Publish

        [<CLIEvent>]
        member x.KeyDown = keyDown.Publish


        override x.OnPreviewKeyDown(e) =
            if e.KeyData = (Keys.Alt ||| Keys.Menu) ||
               e.KeyData = (Keys.Alt ||| Keys.Menu ||| Keys.Control) ||
               e.KeyData = (Keys.ShiftKey ||| Keys.Shift) ||
               e.KeyData = Keys.LWin || e.KeyData = Keys.RWin
            then
                keyDown.Trigger(KeyEventArgs(e.KeyData))


        override x.OnKeyDown(e) =
            keyDown.Trigger(e)
            e.SuppressKeyPress <- true
            e.Handled <- true

        interface IControl with
            member x.Paint() =
                use g = x.CreateGraphics()
                use e = new PaintEventArgs(g, x.ClientRectangle)
                x.InvokePaint(x, e)

            member x.Invoke f =
                base.Invoke (new System.Action(f)) |> ignore

        member private x.ForceRedraw() =
            messageLoop.Draw x
        
//        override x.OnPreviewKeyDown(e) =
//            base.OnPreviewKeyDown(e)
//
//            
//            if e.KeyCode = Keys.R then
//                x.ForceRedraw()


        member x.Size =
            V2i(base.ClientSize.Width, base.ClientSize.Height)


        member x.Sizes =
            sizes :> IEvent<_>

        member x.Statistics =
            statistics :> IEvent<_>

        member x.RenderTask
            with get() = task.Value
            and set t =
                match task with
                    | Some old -> 
                        if taskSubscription <> null then taskSubscription.Dispose()
                        old.Dispose()
                    | None -> ()

                task <- Some t
                taskSubscription <- t.AddMarkingCallback x.ForceRedraw

        

        override x.OnHandleCreated(e) =
            let c = OpenTK.Graphics.GraphicsContext.CurrentContext
            if c <> null then
                c.MakeCurrent(null)

            ContextHandle.primaryContext.MakeCurrent()
            base.OnHandleCreated(e)
            loaded <- true
            base.MakeCurrent()
            
        override x.OnResize(e) =
            if loaded then
                base.OnResize(e)
                sizes.Emit(V2i(base.ClientSize.Width, base.ClientSize.Height))

        override x.OnPaint(e) =
            if loaded then
                if contextHandle = null then
                    contextHandle <- ContextHandle(base.Context, base.WindowInfo) 

                match task with
                    | Some t ->
                        using (ctx.RenderingLock contextHandle) (fun _ ->
                            //lock changePropagationLock (fun () ->
                                defaultFramebuffer.Size <- x.Size
                                GL.Viewport(0,0,x.ClientSize.Width, x.ClientSize.Height)
                                GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f)
                                GL.ClearDepth(1.0)
                                GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)

                                let res = t.Run(defaultFramebuffer)

                                statistics.Emit res.Statistics

                                x.SwapBuffers()
                                afterRender.Trigger ()
                            //)
                        )
                    | None ->
                        ()

    type Window(app : OpenGlApplication, samples : int) as this =
        inherit Form()

        let title = "WinForms Window"

        let ctrl = new OpenGlControl(app.Context, samples, Dock = DockStyle.Fill)
        do base.Controls.Add(ctrl)
           base.Width <- 1024
           base.Height <- 768
           base.Text <- title + " (0 frames rendered)"

           let frames = ref 0
           ctrl.AfterRender.Add(fun () ->
            frames := !frames + 1
            this.Text <- title + sprintf " (%d frames rendered)" !frames
            
           )


        member x.Run() =
            Application.Run x

        member x.Control = ctrl

        member x.Sizes = ctrl.Sizes
        member x.Statistics = ctrl.Statistics
        member x.RenderTask 
            with get() = ctrl.RenderTask
            and set t = ctrl.RenderTask <- t

        new(app) = new Window(app, 1)

    type CameraController(ctrl : OpenGlControl, view : ICameraView) as this =
        
        let speed = 1.5

        let mutable s : IDisposable = null //OpenGlControl.MessageLoop.EnqueuePeriodic(this.Update, 5)

        do ctrl.KeyDown.Add(fun e -> this.KeyDown e.KeyCode)
           ctrl.KeyUp.Add(fun e -> this.KeyUp e.KeyCode)
           ctrl.MouseDown.Add(fun e -> this.MouseDown(e.Button, V2i(e.X, e.Y)))
           ctrl.MouseUp.Add(fun e -> this.MouseUp(e.Button, V2i(e.X, e.Y)))
           ctrl.MouseMove.Add(fun e -> this.MouseMove(V2i(e.X, e.Y)))
           ctrl.MouseWheel.Add(fun e -> this.MouseWheel(e.Delta))

        let mutable left = false
        let mutable right = false
        let mutable forward = false
        let mutable backward = false

        let mutable leftDown = false
        let mutable rightDown = false
        let mutable middleDown = false

        let mutable lastMousePosition = V2i.Zero

        let mutable targetZoom = 0.0
        let mutable currentZoom = 0.0

        let (<+>) (a : Option<V3d>) (b : Option<V3d>) =
            match a,b with
                | Some a, Some b -> Some (a + b)
                | Some a, _ -> Some a
                | _, Some b -> Some b
                | _ -> None

        member x.MouseDown(m : MouseButtons, pos : V2i) =
            lastMousePosition <- pos
            match m with
                | MouseButtons.Left -> leftDown <- true
                | MouseButtons.Right -> rightDown <- true
                | MouseButtons.Middle -> middleDown <- true
                | _ -> ()

        member x.MouseUp(m : MouseButtons, pos : V2i) =
            lastMousePosition <- pos
            match m with
                | MouseButtons.Left -> leftDown <- false
                | MouseButtons.Right -> rightDown <- false
                | MouseButtons.Middle -> middleDown <- false
                | _ -> ()

        member x.MouseMove(pos : V2i) =
            let delta = pos - lastMousePosition 

            transact (fun () ->
                if leftDown then
                    let r = view.Right
                    let u = view.Up


                    let t = 
                        M44d.Rotation(u, -0.008 * float delta.X) *
                        M44d.Rotation(r, -0.008 * float delta.Y)

                    view.Forward <- t.TransformDir view.Forward
  
                elif middleDown then
                    let u = view.Up
                    let r = view.Right

                    view.Location <- view.Location + 
                        u * -(0.02 * speed) * float delta.Y +
                        r * 0.02 * speed * float delta.X
                elif rightDown then
                    let fw = view.Forward


                    view.Location <- view.Location + 
                        fw * -0.02 * speed * float delta.Y
            )


            lastMousePosition <- pos

        member x.MouseWheel(delta : int) =
            targetZoom <- targetZoom + float delta / 120.0

        member x.KeyUp(k : Keys) =
            match k with
                | Keys.W -> forward <- false
                | Keys.S -> backward <- false
                | Keys.A -> left <- false
                | Keys.D -> right <- false
                | _ -> ()

        member x.KeyDown(k : Keys) =
            match k with
                | Keys.W -> forward <- true
                | Keys.S -> backward <- true
                | Keys.A -> left <- true
                | Keys.D -> right <- true
                | _ -> ()

        member x.Update(elapsedSeconds : float) =
            let mutable delta = V3d.Zero

            let deltaRight =
                match left, right with
                    | (true, false) -> Some (-V3d.IOO * speed * elapsedSeconds)
                    | (false, true) -> Some (V3d.IOO * speed * elapsedSeconds)
                    | _ -> None

            let deltaForward =
                match forward, backward with
                    | (true, false) -> Some (-V3d.OOI * speed * elapsedSeconds)
                    | (false, true) -> Some (V3d.OOI * speed * elapsedSeconds)
                    | _ -> None

            let deltaZoom =
                let diff = targetZoom - currentZoom
                if abs diff > 0.05 then
                    let step = 4.0 * diff * elapsedSeconds

                    currentZoom <- currentZoom + step
                    
                    if sign (targetZoom - currentZoom) <> sign diff then
                        currentZoom <- targetZoom
                        Some (-V3d.OOI * (targetZoom - currentZoom))
                    else
                        Some (-V3d.OOI * step)

                else
                    None

            let delta = deltaRight <+> deltaForward <+> deltaZoom

            match delta with
                | Some delta ->
                    transact (fun () ->
                        let delta =  view.ViewTrafo.Backward.TransformDir delta
                        view.Location <- view.Location + delta
                    )
                | None ->
                    ()

        member x.Start() =
            if s <> null then
                s.Dispose()

            s <- OpenGlControl.MessageLoop.EnqueuePeriodic(this.Update, 1000 / 120)

        member x.Dispose() =
            if s <> null then
                s.Dispose()
                s <- null

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    let addCameraController (w : Window) (view : ICameraView) =
        let cc = new CameraController(w.Control, view)
        cc.Start()
        cc :> IDisposable

    let addFillModeController (w : Window) (fillMode : ModRef<FillMode>) =
        w.Control.KeyDown.Add (fun e ->
            if e.KeyCode = Keys.F && (e.Modifiers &&& Keys.Alt) <> Keys.None then
                
                let newMode = 
                    match fillMode.Value with
                        | FillMode.Fill -> FillMode.Line
                        | FillMode.Line -> FillMode.Point
                        | _ -> FillMode.Fill

                transact (fun () -> fillMode.Value <- newMode)
        )
