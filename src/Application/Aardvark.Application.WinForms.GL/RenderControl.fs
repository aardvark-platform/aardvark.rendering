namespace Aardvark.Application.WinForms


open OpenTK
open OpenTK.Graphics.OpenGL4

open System
open System.Windows.Forms

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Rendering.GL
open Aardvark.Application

type OpenGlRenderControl(runtime : Runtime, enableDebug : bool, samples : int) =
    inherit GLControl(
        Graphics.GraphicsMode(
            OpenTK.Graphics.ColorFormat(Config.BitsPerPixel),
            Config.DepthBits,
            Config.StencilBits,
            0,
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

    let mutable framebuffer : option<Texture * Texture * Framebuffer * option<Texture * Framebuffer>> = None

    let getFramebuffer (realSize : V2i) (size : V2i) (samples : int) =
        let subsampled, resolved =
            match framebuffer with
            | Some (c, _, f, f0) when f.Size = realSize && c.Multisamples = samples ->
                f, f0
            | _ ->
                match framebuffer with
                | Some (c, d, f, f0) ->
                    ctx.Delete f
                    ctx.Delete c
                    ctx.Delete d
                    match f0 with
                    | Some (c0, f0) ->
                        ctx.Delete f0
                        ctx.Delete c0
                    | None ->
                        ()
                | _ ->
                    ()

                let c = ctx.CreateTexture(V3i(realSize, 1), TextureDimension.Texture2D, TextureFormat.Rgba8, 1, 1, samples)
                let d = ctx.CreateTexture(V3i(realSize, 1), TextureDimension.Texture2D, TextureFormat.Depth24Stencil8, 1, 1, samples)
                let f =
                    ctx.CreateFramebuffer(
                        fboSignature,
                        [ 0, DefaultSemantic.Colors, { texture = c; slice = 0; level = 0 } :> IFramebufferOutput ],
                        Some ( { texture = d; slice = 0; level = 0 } :> IFramebufferOutput),
                        None
                    )

                let f0 =
                    if samples > 1 then
                        let c0 = ctx.CreateTexture(V3i(realSize, 1), TextureDimension.Texture2D, TextureFormat.Rgba8, 1, 1, 1)
                        let f0 =
                            ctx.CreateFramebuffer(
                                fboSignature,
                                [ 0, DefaultSemantic.Colors, { texture = c0; slice = 0; level = 0 } :> IFramebufferOutput ],
                                None,
                                None
                            )
                        Some (c0, f0)
                    else
                        None

                framebuffer <- Some (c, d, f, f0)
                f, f0

        let blit() =
            let s = realSize
            match resolved with
            | Some(_, resolved) ->
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, subsampled.Handle)
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, resolved.Handle)
                GL.DrawBuffer(DrawBufferMode.ColorAttachment0)
                GL.ReadBuffer(ReadBufferMode.ColorAttachment0)
                GL.BlitFramebuffer(0, 0, s.X, s.Y, 0, 0, s.X, s.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest)
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)


                GL.DrawBuffer(DrawBufferMode.Back)
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, resolved.Handle)
                GL.ReadBuffer(ReadBufferMode.ColorAttachment0)
                GL.BlitFramebuffer(0, 0, s.X, s.Y, 0, 0, size.X, size.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear)
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
            | None ->
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, subsampled.Handle)
                GL.ReadBuffer(ReadBufferMode.ColorAttachment0)
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
                GL.DrawBuffer(DrawBufferMode.Back)
                GL.BlitFramebuffer(0, 0, s.X, s.Y, 0, 0, size.X, size.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear)
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)

        subsampled, blit

    //let mutable defaultOutput = OutputDescription.ofFramebuffer defaultFramebuffer

    let sizes = AVal.init (V2i(base.ClientSize.Width, base.ClientSize.Height))

    let frameTime = AverageWindow(10)
    let frameWatch = System.Diagnostics.Stopwatch()

    let timeWatch = System.Diagnostics.Stopwatch()
    let baseTime = DateTime.Now.Ticks
    do timeWatch.Start()

    let now() = DateTime(timeWatch.Elapsed.Ticks + baseTime)
    let nextFrameTime() =
        if frameTime.Count >= 10 then
            now() + TimeSpan.FromSeconds frameTime.Value
        else
            now()
    let time = AVal.init (now())

    let mutable samples = samples
    let mutable subsampling = 1.0

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

    /// <summary> Returns true if the control has been fully initialized.</summary>
    member x.IsLoaded
        with get () = loaded

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

    member x.Sizes = sizes :> aval<_>
    member x.Samples
        with get() = samples
        and set s =
            samples <- s
            x.ForceRedraw()

    member x.SubSampling
        with get() = subsampling
        and set v =
            subsampling <- v
            x.ForceRedraw()


    override x.OnHandleCreated(e) =

        base.OnHandleCreated(e) // creates the graphics context of the control and performs MakeCurrent -> NOTE: during this call rendering in other threads can break resource sharing

        ContextHandle.initGlConfig()

        //x.KeyDown.Add(fun e ->
        //    if e.KeyCode = System.Windows.Forms.Keys.End && e.Control then
        //        renderContinuously <- not renderContinuously
        //        x.Invalidate()
        //        e.Handled <- true
        //)

    member x.Render() =

        let mutable initial = false
        if loaded then
            if isNull contextHandle || contextHandle.Handle.IsDisposed then
                contextHandle <- ContextHandle(base.Context, base.WindowInfo)
                contextHandle.AttachDebugOutputIfNeeded(enableDebug)
                initial <- true

            beforeRender.Trigger()

            let screenSize = V2i(base.ClientSize.Width, base.ClientSize.Height)
            let fboSize = V2i(max 1 (int (round (float screenSize.X * subsampling))), (int (round (float screenSize.Y * subsampling))))
            match task with
            | Some t ->
                use __ = ctx.RenderingLock contextHandle
                let fbo, blit = getFramebuffer fboSize screenSize samples

                if initial then
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
                    GL.Disable(EnableCap.Multisample)
                    //let ms = GL.IsEnabled(EnableCap.Multisample)
                    //if ms then Log.warn "multisample enabled"
                    //else Log.warn "multisample disabled"
                    //let samples = Array.zeroCreate 1
                    //GL.GetFramebufferParameter(FramebufferTarget.Framebuffer, unbox (int All.Samples), samples)
                    //Log.warn "effective samples: %A" samples.[0]

                let stopDispatcherProcessing = threadStealing.StopStealing()

                frameWatch.Restart()
                transact (fun () -> time.Value <- nextFrameTime())

                if fboSize <> sizes.Value then
                    transact (fun () -> sizes.Value <- fboSize)

                defaultFramebuffer.Size <- V2i(x.ClientSize.Width, x.ClientSize.Height)
                //defaultOutput <- { defaultOutput with viewport = Box2i(V2i.OO, defaultFramebuffer.Size - V2i.II) }

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo.Handle)
                GL.ColorMask(true, true, true, true)
                GL.DepthMask(true)
                GL.StencilMask(0xFFFFFFFFu)
                GL.Viewport(0,0,x.ClientSize.Width, x.ClientSize.Height)
                GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f)
                GL.ClearDepth(1.0)
                GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)


                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
                //EvaluationUtilities.evaluateTopLevel(fun () ->
                t.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer fbo)
                blit()

                x.SwapBuffers()
                //System.Threading.Thread.Sleep(200)
                frameWatch.Stop()
                if not first then
                    frameTime.Insert frameWatch.Elapsed.TotalSeconds |> ignore

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

            | None ->
                if fboSize <> sizes.Value then
                    transact (fun () -> sizes.Value <- fboSize)

                needsRedraw <- false

            afterRender.Trigger()

            needsRedraw <- renderContinuously
            if renderContinuously then
                x.Invalidate()

    override x.OnPaint(e) =
        base.OnPaint(e)
        loaded <- true
        if x.OnPaintRender then
            x.Render()

//    override x.OnResize(e) =
//        base.OnResize(e)
//        sizes.Emit <| V2i(base.ClientSize.Width, base.ClientSize.Height)

    member x.Time = time :> aval<_>
    member x.FramebufferSignature = fboSignature :> IFramebufferSignature

    member x.BeforeRender = beforeRender.Publish
    member x.AfterRender = afterRender.Publish

    interface IRenderTarget with
        member x.FramebufferSignature = fboSignature :> IFramebufferSignature
        member x.Runtime = runtime :> IRuntime
        member x.Time = time :> aval<_>
        member x.RenderTask
            with get() = x.RenderTask
            and set t = x.RenderTask <- t

        member x.SubSampling
            with get() = x.SubSampling
            and set v = x.SubSampling <- v

        member x.Sizes = sizes :> aval<_>
        member x.Samples = samples
        member x.BeforeRender = beforeRender.Publish
        member x.AfterRender = afterRender.Publish

    new(runtime : Runtime, enableDebug : bool) = new OpenGlRenderControl(runtime, enableDebug, 1)

