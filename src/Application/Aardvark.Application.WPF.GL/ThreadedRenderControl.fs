namespace Aardvark.Application.WinForms


open OpenTK
open OpenTK.Graphics.ES20
open OpenTK.Graphics.OpenGL
open OpenTK.Graphics.OpenGL4

open System
open System.Threading.Tasks
open System.Windows.Forms
open System.Threading
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Rendering.GL
open Aardvark.Application

type ThreadedRenderControl(runtime : Runtime, debug : IDebugConfig, samples : int) as this =
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
    let transaction = new Transaction()

    let mutable task : Option<IRenderTask> = None
    let mutable taskSubscription : IDisposable = null

    let depthStencilFormat =
        match Config.DepthBits, Config.StencilBits with
        | 0, 0 -> None
        | 16, 0 -> Some TextureFormat.DepthComponent16
        | 24, 0 -> Some TextureFormat.DepthComponent24
        | 32, 0 -> Some TextureFormat.DepthComponent32
        | 24, 8 -> Some TextureFormat.Depth24Stencil8
        | 32, 8 -> Some TextureFormat.Depth32fStencil8
        | 0, 8 -> Some TextureFormat.StencilIndex8
        | _ -> failwith "invalid depth-stencil mode"

    let fboSignature =
        let depthStencilAtt =
            depthStencilFormat |> Option.map (fun f -> DefaultSemantic.DepthStencil, f) |> Option.toList

        runtime.CreateFramebufferSignature([DefaultSemantic.Colors, TextureFormat.Rgba8] @ depthStencilAtt, samples)

    let mutable contextHandle : ContextHandle = null
    let defaultFramebuffer =
        new Framebuffer(
            ctx, fboSignature,
            (fun _ -> 0),
            ignore,
            [0, DefaultSemantic.Colors, new Renderbuffer(ctx, 0, V2i.Zero, TextureFormat.Rgba8, samples, 0L) :> IFramebufferOutput], None
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

                let c = ctx.CreateTexture2D(realSize, 1, TextureFormat.Rgba8, samples)
                let d = ctx.CreateTexture2D(realSize, 1, TextureFormat.Depth24Stencil8, samples)
                let f = 
                    ctx.CreateFramebuffer(
                        fboSignature,
                        [ 0, DefaultSemantic.Colors, c.GetOutputView() ],
                        Some ( d.GetOutputView())
                    )

                let f0 =
                    if samples > 1 then
                        let c0 = ctx.CreateTexture2D(realSize, 1, TextureFormat.Rgba8, 1)
                        let f0 =
                            ctx.CreateFramebuffer(
                                fboSignature,
                                [ 0, DefaultSemantic.Colors, c0.GetOutputView() ],
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

    // NOTE: previously the size was forced to > 0 in Render, but now is only updated if the ClientSize is > 0
    //       not sure if lying about the size is ideal, but a size of 0 area might crash client the application on the client size if not handled there
    //       earlier we seem to have followed the OnResize events and the "size" represented the true RenderControl size
    //       as we seem to have decided at some point to implement this behavior the initial size also needs to be > 0 here:
    let sizes = AVal.init (V2i(max 1 base.ClientSize.Width, max 1 base.ClientSize.Height)) 

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
        { new StopStealing with member x.StopStealing () = Disposable.empty }

    let beforeRender = Event<unit>()
    let afterRender = Event<unit>()

    let textures = new System.Collections.Concurrent.BlockingCollection<Texture>()
    let presentTextures = new System.Collections.Generic.Queue<Texture>()
    
    let pushNewTexture(size : V2i) =
        let tex = ctx.CreateTexture2D(size, 1, TextureFormat.Rgba8, 1)
        textures.Add tex
    
    let renderPendingLock = obj()
    let mutable renderPending = false
    
    let mutable disposed = false
    
    
    let renderThread =
        
        startThread <| fun () ->
            try
                let ctx = runtime.Context
                
                
                let mutable depth : option<Texture> = None
                let mutable realColor : option<Texture> = None
                
                
                let mutable ctxLock = None
                let t = new MultimediaTimer.Trigger(16)
                while not disposed do
                    t.Wait()
                    // lock renderPendingLock (fun () ->
                    //     while not renderPending do
                    //         Monitor.Wait renderPendingLock |> ignore
                    //     renderPending <- false 
                    // )
                    let shouldRender =
                        lock renderPendingLock (fun () ->
                            let o = renderPending
                            renderPending <- false
                            o
                        )
                    if shouldRender then 
                        match ctxLock with
                        | None -> 
                            let handle = runtime.Context.CreateContext()
                            let h = ctx.RenderingLock handle
                            ctxLock <- Some h
                            GL.SetDefaultStates()
                            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
                            GL.Disable(EnableCap.Multisample)
                        | Some _ -> ()
                        
                        match task with
                        | Some task ->
                            
                            let color = textures.Take()
                            
                            let depth =
                                match depth with
                                | Some depth when depth.Size.XY = color.Size.XY && depth.Multisamples = samples ->
                                    depth
                                | _ ->
                                    match depth with
                                    | Some t -> ctx.Delete t
                                    | None -> ()
                                    
                                    let d = ctx.CreateTexture2D(color.Size.XY, 1, TextureFormat.Depth24Stencil8, samples)
                                    depth <- Some d
                                    d
                            
                            let realColor =
                                if samples <= 1 then
                                    color
                                else 
                                    match realColor with
                                    | Some realColor when realColor.Size.XY = color.Size.XY && realColor.Multisamples = samples ->
                                        realColor
                                    | _ ->
                                        match realColor with
                                        | Some t -> ctx.Delete t
                                        | None -> ()
                                        
                                        let d = ctx.CreateTexture2D(color.Size.XY, 1, TextureFormat.Rgba8, samples)
                                        realColor <- Some d
                                        d 
                            let size = realColor.Size.XY
                            let fbo =
                                ctx.CreateFramebuffer(
                                    fboSignature,
                                    [0, DefaultSemantic.Colors, realColor.[TextureAspect.Color, 0, 0]],
                                    Some (depth.[TextureAspect.DepthStencil, 0, 0] :> IFramebufferOutput)
                                )
                                
                                
                            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo.Handle)
                            GL.ColorMask(true, true, true, true)
                            GL.DepthMask(true)
                            GL.StencilMask(0xFFFFFFFFu)
                            GL.Viewport(0,0,size.X, size.Y)
                            GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f)
                            GL.ClearDepth(1.0)
                            GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)
                            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
                                
                            task.Run(AdaptiveToken.Top, RenderToken.Zero, fbo)
                          
                            if samples > 1 then
                                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer,fbo.Handle)
                                let dst = GL.GenFramebuffer()
                                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer,dst)
                                GL.FramebufferTexture(FramebufferTarget.DrawFramebuffer,FramebufferAttachment.ColorAttachment0,color.Handle,0)
                                GL.BlitFramebuffer(0,0,size.X,size.Y,0,0,size.X,size.Y,ClearBufferMask.ColorBufferBit,BlitFramebufferFilter.Linear)
                                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer,0)
                                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer,0)
                                GL.DeleteFramebuffer(dst)
                            ctx.Delete fbo
                            
                            GL.Flush()
                            GL.Finish()
                            
                            if not first then
                                frameTime.Insert frameWatch.Elapsed.TotalSeconds |> ignore
                            frameWatch.Restart()
                            transact (fun () -> time.Value <- nextFrameTime())
                            first <- false
                            lock presentTextures (fun () ->
                                presentTextures.Enqueue color
                                Monitor.PulseAll presentTextures
                            )
                            MessageLoop.Invalidate this |> ignore
                            
                        | None ->
                            ()
            with e ->
                Log.error "%A" e
        
    member x.ContextHandle = contextHandle

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
        lock renderPendingLock (fun () ->
            renderPending <- true
            Monitor.PulseAll renderPendingLock
        )
      

    member x.RenderContinuously
        with get() = renderContinuously
        and set v =
            renderContinuously <- v
            // if continuous rendering is enabled make sure rendering is initiated
            if v then // -> only makes sense with onPaintRender
                x.ForceRedraw()


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
            x.ForceRedraw()

    member x.Sizes = sizes :> aval<_>
    member x.Samples
        with get() = samples
        and set s =
            if samples <> s then
                samples <- s
                x.ForceRedraw()

    member x.SubSampling
        with get() = subsampling
        and set v =
            if subsampling <> v then
                subsampling <- v
                x.ForceRedraw()


    override x.OnHandleCreated(e) =

        base.OnHandleCreated(e) // creates the graphics context of the control and performs MakeCurrent -> NOTE: during this call rendering in other threads can break resource sharing

        GL.SetDefaultStates()
        
        


    member x.Render() =
        let mutable initial = false
        if isNull contextHandle || contextHandle.Handle.IsDisposed then
            contextHandle <- new ContextHandle(base.Context, base.WindowInfo)
            contextHandle.Initialize(debug, setDefaultStates = false)
            initial <- true

        use __ = ctx.RenderingLock contextHandle
        
        let screenSize = V2i(x.ClientSize.Width, x.ClientSize.Height)
        let fboSize = V2i(max 1 (int (round (float screenSize.X * subsampling))), (int (round (float screenSize.Y * subsampling))))
        if fboSize <> sizes.Value then
            useTransaction transaction (fun () -> sizes.Value <- fboSize)
        
        
        let colorTex = 
            lock presentTextures (fun () ->
                if presentTextures.Count > 0 then Some (presentTextures.Dequeue())
                else None
            )
        
        match colorTex with
        | Some colorTex ->
            
            let src = GL.GenFramebuffer()
            GL.Check()
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, src)
            GL.Check()
            GL.FramebufferTexture(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, colorTex.Handle, 0)
            GL.Check()
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
            GL.Check()
            GL.BlitFramebuffer(0, 0, colorTex.Size.X, colorTex.Size.Y, 0, 0, screenSize.X, screenSize.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear)
            GL.Check()
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
            GL.Check()
            GL.DeleteFramebuffer(src)
            GL.Check()
            
            if colorTex.Size.XY = fboSize then
                textures.Add colorTex
            else
                ctx.Delete colorTex
                GL.Check()
                pushNewTexture fboSize
                x.ForceRedraw()
                
            x.SwapBuffers()
            
        | None ->
            x.ForceRedraw()
            

    override x.OnPaint(e) =
        //Log.line "PAINT VER 3"
        base.OnPaint(e)
        let screenSize = V2i(x.ClientSize.Width, x.ClientSize.Height)
        let fboSize = V2i(max 1 (int (round (float screenSize.X * subsampling))), (int (round (float screenSize.Y * subsampling))))
        if fboSize <> sizes.Value then
            transact (fun () -> sizes.Value <- fboSize)
            
        if not loaded then
            pushNewTexture fboSize
            x.ForceRedraw()
            // lock presentTextures (fun () ->
            //     while presentTextures.Count = 0 do
            //         Monitor.Wait presentTextures |> ignore
            // )
            
            
        loaded <- true
        x.Render()

//    override x.OnResize(e) =
//        base.OnResize(e)
//        sizes.Emit <| V2i(base.ClientSize.Width, base.ClientSize.Height)

    member x.Time = time :> aval<_>
    member x.FramebufferSignature = fboSignature

    member x.BeforeRender = beforeRender.Publish
    member x.AfterRender = afterRender.Publish

    interface IRenderTarget with
        member x.FramebufferSignature = fboSignature
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

    new(runtime : Runtime, debug : bool, samples : int) = new ThreadedRenderControl(runtime, DebugLevel.ofBool debug, samples)
    new(runtime : Runtime, debug : IDebugConfig) = new ThreadedRenderControl(runtime, debug, 1)
    new(runtime : Runtime, debug : bool) = new ThreadedRenderControl(runtime, debug, 1)

