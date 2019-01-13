namespace Aardvark.Application.WinForms


open OpenTK
open OpenTK.Graphics.OpenGL4

open System
open System.Windows.Forms

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Rendering.GL
open Aardvark.Application

type OpenGlRenderControl(runtime : Runtime, enableDebug : bool, samples : int) =
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
//    static let messageLoop = MessageLoop()
//    static do messageLoop.Start()

    let ctx = runtime.Context
    let mutable loaded = false

    let mutable task : Option<IRenderTask> = None
    let mutable taskSubscription : IDisposable = null

    let depthStencilSignature =
        match Config.DepthBits, Config.StencilBits with
            | 0, 0 -> None
            | 16, 0 -> Some { format = RenderbufferFormat.DepthComponent16; samples = samples }
            | 24, 0 -> Some { format = RenderbufferFormat.DepthComponent24; samples = samples }
            | 32, 0 -> Some { format = RenderbufferFormat.DepthComponent32; samples = samples }
            | 24, 8 -> Some { format = RenderbufferFormat.Depth24Stencil8; samples = samples }
            | 32, 8 -> Some { format = RenderbufferFormat.Depth32fStencil8; samples = samples }
            | _ -> failwith "invalid depth-stencil mode"

    let fboSignature =
        FramebufferSignature(
            runtime,
            Map.ofList [0, (DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = samples })],
            Map.empty,
            depthStencilSignature,
            None,
            1,
            Set.empty
        )

    let mutable contextHandle : ContextHandle = null 
    let defaultFramebuffer = 
        new Framebuffer(
            ctx, fboSignature, 
            (fun _ -> 0), 
            ignore, 
            [0, DefaultSemantic.Colors, Renderbuffer(ctx, 0, V2i.Zero, RenderbufferFormat.Rgba8, samples, 0L) :> IFramebufferOutput], None
        )
    let mutable defaultOutput = OutputDescription.ofFramebuffer defaultFramebuffer

    let sizes = Mod.init (V2i(base.ClientSize.Width, base.ClientSize.Height))

    let frameTime = RunningMean(10)
    let frameWatch = System.Diagnostics.Stopwatch()

    let timeWatch = System.Diagnostics.Stopwatch()
    let baseTime = DateTime.Now.Ticks
    do timeWatch.Start()

    let now() = DateTime(timeWatch.Elapsed.Ticks + baseTime)
    let nextFrameTime() = 
        if frameTime.Count >= 10 then
            now() + TimeSpan.FromSeconds frameTime.Average
        else
            now()
    let time = Mod.init (now())


    let mutable needsRedraw = false
    let mutable first = true
    
    let mutable renderContinuously = false
    let mutable autoInvalidate = true
    let mutable threadStealing : StopStealing = 
        { new StopStealing with member x.StopStealing () = { new IDisposable with member x.Dispose() = () } }

    let beforeRender = Event<unit>()
    let afterRender = Event<unit>()

    member x.ContextHandle = contextHandle

    member val OnPaintRender = true with get, set

    member x.DisableThreadStealing  
        with get () = threadStealing 
        and set v = threadStealing <- v

    // automatically invalidates after OnPaint if renderTask is out of date again
    member x.AutoInvalidate 
        with get () = autoInvalidate
        and set v = autoInvalidate <- v



    interface IInvalidateControl with
        member x.IsInvalid = needsRedraw

    member private x.ForceRedraw() =
        if not renderContinuously then 
            MessageLoop.Invalidate x |> ignore

    member x.RenderContinuously
        with get() = renderContinuously
        and set v = 
            renderContinuously <- v
            // if continuous rendering is enabled make sure rendering is initiated
            if v then 
                x.Invalidate()


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

    member x.Sizes = sizes :> IMod<_>
    member x.Samples = samples



    override x.OnHandleCreated(e) =
        let c = OpenTK.Graphics.GraphicsContext.CurrentContext
        GL.Hint(HintTarget.PointSmoothHint, HintMode.Fastest)
        GL.Enable(EnableCap.TextureCubeMapSeamless)
        GL.Disable(EnableCap.PolygonSmooth)

        if c <> null then
            c.MakeCurrent(null)

        //if ContextHandle.primaryContext <> null then
        //    ContextHandle.primaryContext.MakeCurrent()

        base.OnHandleCreated(e)
        loaded <- true

        x.KeyDown.Add(fun e ->
            if e.KeyCode = System.Windows.Forms.Keys.End && e.Control then
                renderContinuously <- not renderContinuously
                x.Invalidate()
                e.Handled <- true
        )

        base.MakeCurrent()
         
    member x.Render() = 
        let mutable initial = false
        if loaded then
            if isNull contextHandle || contextHandle.Handle.IsDisposed then
                contextHandle <- ContextHandle(base.Context, base.WindowInfo) 
                contextHandle.AttachDebugOutputIfNeeded(enableDebug)
                initial <- true

            let size = V2i(base.ClientSize.Width, base.ClientSize.Height)
          
            
            beforeRender.Trigger()

            match task with
                | Some t ->
                    using (ctx.RenderingLock contextHandle) (fun _ ->
                        if initial then
                            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
                            let ms = GL.IsEnabled(EnableCap.Multisample)
                            if ms then Log.warn "multisample enabled"
                            else Log.warn "multisample disabled"
                            let samples = Array.zeroCreate 1
                            GL.GetFramebufferParameter(FramebufferTarget.Framebuffer, unbox (int All.Samples), samples)
                            Log.warn "effective samples: %A" samples.[0]

                        let stopDispatcherProcessing = threadStealing.StopStealing()

                        frameWatch.Restart()
                        transact (fun () -> time.Value <- nextFrameTime())

                        if size <> sizes.Value then
                            transact (fun () -> Mod.change sizes size)

                        defaultFramebuffer.Size <- V2i(x.ClientSize.Width, x.ClientSize.Height)
                        defaultOutput <- { defaultOutput with viewport = Box2i(V2i.OO, defaultFramebuffer.Size - V2i.II) }

                        GL.ColorMask(true, true, true, true)
                        GL.DepthMask(true)
                        GL.StencilMask(0xFFFFFFFFu)
                        GL.Viewport(0,0,x.ClientSize.Width, x.ClientSize.Height)
                        GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f)
                        GL.ClearDepth(1.0)
                        GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)

                        EvaluationUtilities.evaluateTopLevel(fun () ->
                            t.Run(AdaptiveToken.Top, RenderToken.Empty, defaultOutput)
                        )
                        
//                        let sw = System.Diagnostics.Stopwatch()
//                        sw.Start()
//                        while sw.Elapsed.TotalMilliseconds < 10.0 do 1;


                        
                        x.SwapBuffers()
                        //System.Threading.Thread.Sleep(200)
                        frameWatch.Stop()
                        if not first then
                            frameTime.Add frameWatch.Elapsed.TotalSeconds

                        transact (fun () -> time.MarkOutdated())

                        stopDispatcherProcessing.Dispose()
                        if t.OutOfDate then
//                            let sleepTime = max 0.0 (10.0 - sw.Elapsed.TotalMilliseconds)
//                            let t = System.Threading.Tasks.Task.Delay (int sleepTime)
//                            t.Wait()
                            needsRedraw <- true                    
                            if autoInvalidate then x.Invalidate()
                            ()
                        else
                            needsRedraw <- false

                        first <- false
                    )

                | None ->
                    if size <> sizes.Value then
                        transact (fun () -> Mod.change sizes size)

                    needsRedraw <- false

            afterRender.Trigger()

            needsRedraw <- renderContinuously
            if renderContinuously then
                x.Invalidate()
        
    override x.OnPaint(e) =
        if x.OnPaintRender then
            x.Render()
                    
//    override x.OnResize(e) =
//        base.OnResize(e)
//        sizes.Emit <| V2i(base.ClientSize.Width, base.ClientSize.Height)

    member x.Time = time :> IMod<_>
    member x.FramebufferSignature = fboSignature :> IFramebufferSignature
    
    member x.BeforeRender = beforeRender.Publish
    member x.AfterRender = afterRender.Publish

    interface IRenderTarget with
        member x.FramebufferSignature = fboSignature :> IFramebufferSignature
        member x.Runtime = runtime :> IRuntime
        member x.Time = time :> IMod<_>
        member x.RenderTask
            with get() = x.RenderTask
            and set t = x.RenderTask <- t
        member x.Sizes = sizes :> IMod<_>
        member x.Samples = samples
        member x.BeforeRender = beforeRender.Publish
        member x.AfterRender = afterRender.Publish

    new(runtime : Runtime, enableDebug : bool) = new OpenGlRenderControl(runtime, enableDebug, 1)

