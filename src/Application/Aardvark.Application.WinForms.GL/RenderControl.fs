namespace Aardvark.Application.WinForms


open OpenTK
open OpenTK.Graphics.OpenGL4

open System
open System.Windows.Forms

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering.GL
open Aardvark.Application

type OpenGlRenderControl(ctx : Context, samples : int) =
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
    let statistics = EventSource<FrameStatistics>(FrameStatistics.Zero)

    let mutable task : Option<IRenderTask> = None
    let mutable taskSubscription : IDisposable = null

    let mutable contextHandle : ContextHandle = null 
    let defaultFramebuffer = new Framebuffer(ctx, (fun _ -> 0), ignore, [], None)

    let avgFrameTime = RunningMean(10)
    let sizes = EventSource<V2i>(V2i(base.ClientSize.Width, base.ClientSize.Height))
    let time = Mod.custom (fun () -> DateTime.Now + TimeSpan.FromSeconds(avgFrameTime.Average))
    let mutable needsRedraw = false
    let mutable first = true
    

    interface IControl with
        member x.IsInvalid = needsRedraw
        member x.Invalidate() =
            if not needsRedraw then
                needsRedraw <- true
                x.Invalidate()

        member x.Paint() =
            use g = x.CreateGraphics()
            use e = new PaintEventArgs(g, x.ClientRectangle)
            x.InvokePaint(x, e)

        member x.Invoke f =
            base.Invoke (new System.Action(f)) |> ignore

    member private x.ForceRedraw() =
        messageLoop.Draw x


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

    member x.Sizes = sizes :> IEvent<_>

    override x.OnHandleCreated(e) =
        let c = OpenTK.Graphics.GraphicsContext.CurrentContext
        if c <> null then
            c.MakeCurrent(null)

        ContextHandle.primaryContext.MakeCurrent()
        base.OnHandleCreated(e)
        loaded <- true
        base.MakeCurrent()
            
    override x.OnPaint(e) =
        if loaded then
            if contextHandle = null then
                contextHandle <- ContextHandle(base.Context, base.WindowInfo) 

            let size = V2i(base.ClientSize.Width, base.ClientSize.Height)
            

            match task with
                | Some t ->
                    using (ctx.RenderingLock contextHandle) (fun _ ->
                        needsRedraw <- false
                        let sw = System.Diagnostics.Stopwatch()
                        sw.Start()
                        if size <> sizes.Latest then
                            transact (fun () -> sizes.Emit size)
                        defaultFramebuffer.Size <- V2i(x.ClientSize.Width, x.ClientSize.Height)
                        GL.Viewport(0,0,x.ClientSize.Width, x.ClientSize.Height)
                        GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f)
                        GL.ClearDepth(1.0)
                        GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)

                        
                        let res = t.Run(defaultFramebuffer)
                        
                        statistics.Emit res.Statistics
                        
//                        let sw = System.Diagnostics.Stopwatch()
//                        sw.Start()
//                        while sw.Elapsed.TotalMilliseconds < 10.0 do 1;



                        x.SwapBuffers()
                        //System.Threading.Thread.Sleep(200)
                        sw.Stop()
                        if not first then
                            avgFrameTime.Add(sw.Elapsed.TotalSeconds)

                        transact (fun () -> time.MarkOutdated())

                        if t.OutOfDate then
                            needsRedraw <- true
                            x.Invalidate()
                        else
                            needsRedraw <- false

                        first <- false
                    )

                | None ->
                    if size <> sizes.Latest then
                        transact (fun () -> sizes.Emit size)

                    needsRedraw <- false
                    
//    override x.OnResize(e) =
//        base.OnResize(e)
//        sizes.Emit <| V2i(base.ClientSize.Width, base.ClientSize.Height)

    member x.Time = time

    interface IRenderTarget with
        member x.Time = time
        member x.RenderTask
            with get() = x.RenderTask
            and set t = x.RenderTask <- t
        member x.Sizes = sizes :> IEvent<_>

    new(ctx : Context) = new OpenGlRenderControl(ctx, 1)

