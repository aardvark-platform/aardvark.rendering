namespace Aardvark.Application.Slim

open OpenTK
open OpenTK.Graphics.OpenGL4

open System

open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.Rendering.GL
open Aardvark.Application

module GameWindowIO =
    open OpenTK.Input
    type GameWindowKeys = OpenTK.Input.Key
    type GameWindowMouseButtons = OpenTK.Input.MouseButton
    type AardKeys = Aardvark.Application.Keys

    let translateKey (k : GameWindowKeys) : Aardvark.Application.Keys =
        match k with
            | GameWindowKeys.Unknown -> AardKeys.None
            | GameWindowKeys.ShiftLeft -> AardKeys.LeftShift
            | GameWindowKeys.ShiftRight -> AardKeys.RightShift
            | GameWindowKeys.ControlLeft -> AardKeys.LeftCtrl
            | GameWindowKeys.ControlRight -> AardKeys.RightCtrl
            | GameWindowKeys.AltLeft -> AardKeys.LeftAlt
            | GameWindowKeys.AltRight -> AardKeys.RightAlt
            | GameWindowKeys.WinLeft -> AardKeys.LWin
            | GameWindowKeys.WinRight -> AardKeys.RWin
            | GameWindowKeys.Menu -> failwith "you discovered the menu key (i didn't)"
            | GameWindowKeys.F1 -> AardKeys.F1
            | GameWindowKeys.F2 -> AardKeys.F2
            | GameWindowKeys.F3 -> AardKeys.F3
            | GameWindowKeys.F4 -> AardKeys.F4
            | GameWindowKeys.F5 -> AardKeys.F5
            | GameWindowKeys.F6 -> AardKeys.F6
            | GameWindowKeys.F7 -> AardKeys.F7
            | GameWindowKeys.F8 -> AardKeys.F8
            | GameWindowKeys.F9 -> AardKeys.F9
            | GameWindowKeys.F10 -> AardKeys.F10
            | GameWindowKeys.F11 -> AardKeys.F11
            | GameWindowKeys.F12 -> AardKeys.F12
            | GameWindowKeys.F13 -> AardKeys.F13
            | GameWindowKeys.F14 -> AardKeys.F14
            | GameWindowKeys.F15 -> AardKeys.F15
            | GameWindowKeys.F16 -> AardKeys.F16
            | GameWindowKeys.F17 -> AardKeys.F17
            | GameWindowKeys.F18 -> AardKeys.F18
            | GameWindowKeys.F19 -> AardKeys.F19
            | GameWindowKeys.F20 -> AardKeys.F20
            | GameWindowKeys.F21 -> AardKeys.F21
            | GameWindowKeys.F22 -> AardKeys.F22
            | GameWindowKeys.F23 -> AardKeys.F23
            | GameWindowKeys.F24 -> AardKeys.F24
            | GameWindowKeys.F25 -> AardKeys.None
            | GameWindowKeys.F26 -> AardKeys.None
            | GameWindowKeys.F27 -> AardKeys.None
            | GameWindowKeys.F28 -> AardKeys.None
            | GameWindowKeys.F29 -> AardKeys.None
            | GameWindowKeys.F30 -> AardKeys.None
            | GameWindowKeys.F31 -> AardKeys.None
            | GameWindowKeys.F32 -> AardKeys.None
            | GameWindowKeys.F33 -> AardKeys.None
            | GameWindowKeys.F34 -> AardKeys.None
            | GameWindowKeys.F35 -> AardKeys.None
            | GameWindowKeys.Up -> AardKeys.Up
            | GameWindowKeys.Down -> AardKeys.Down
            | GameWindowKeys.Left -> AardKeys.Left
            | GameWindowKeys.Right -> AardKeys.Right
            | GameWindowKeys.Enter -> AardKeys.Enter
            | GameWindowKeys.Escape -> AardKeys.Escape
            | GameWindowKeys.Space -> AardKeys.Space
            | GameWindowKeys.Tab -> AardKeys.Tab
            | GameWindowKeys.BackSpace -> AardKeys.Back
            | GameWindowKeys.Insert -> AardKeys.Insert
            | GameWindowKeys.Delete -> AardKeys.Delete
            | GameWindowKeys.PageUp -> AardKeys.PageUp
            | GameWindowKeys.PageDown -> AardKeys.PageDown
            | GameWindowKeys.Home -> AardKeys.Home
            | GameWindowKeys.End -> AardKeys.End
            | GameWindowKeys.CapsLock -> AardKeys.CapsLock
            | GameWindowKeys.ScrollLock -> AardKeys.Scroll
            | GameWindowKeys.PrintScreen -> AardKeys.PrintScreen
            | GameWindowKeys.Pause -> AardKeys.Pause
            | GameWindowKeys.NumLock -> AardKeys.NumLock
            | GameWindowKeys.Clear -> AardKeys.Clear
            | GameWindowKeys.Sleep -> AardKeys.Sleep
//            | GameWindowKeys.Close -> 
//            | GameWindowKeys.Reply -> 
//            | GameWindowKeys.Forward -> 
//            | GameWindowKeys.Send -> 
//            | GameWindowKeys.Spell -> 
//            | GameWindowKeys.Save -> 
//            | GameWindowKeys.Calculator -> 
//            | GameWindowKeys.Documents -> 
//            | GameWindowKeys.Pictures -> 
//            | GameWindowKeys.Music -> 
//            | GameWindowKeys.MediaPlayer -> 
//            | GameWindowKeys.Mail -> 
//            | GameWindowKeys.Browser -> 
//            | GameWindowKeys.Messenger -> 
//            | GameWindowKeys.Mute -> 
//            | GameWindowKeys.PlayPause -> 
//            | GameWindowKeys.Stop -> 
//            | GameWindowKeys.VolumeUp -> 
//            | GameWindowKeys.VolumeDown -> 
//            | GameWindowKeys.TrackPrevious -> 
//            | GameWindowKeys.TrackNext -> */
            | GameWindowKeys.Keypad0 -> AardKeys.NumPad0
            | GameWindowKeys.Keypad1 -> AardKeys.NumPad1
            | GameWindowKeys.Keypad2 -> AardKeys.NumPad2
            | GameWindowKeys.Keypad3 -> AardKeys.NumPad3
            | GameWindowKeys.Keypad4 -> AardKeys.NumPad4
            | GameWindowKeys.Keypad5 -> AardKeys.NumPad5
            | GameWindowKeys.Keypad6 -> AardKeys.NumPad6
            | GameWindowKeys.Keypad7 -> AardKeys.NumPad7
            | GameWindowKeys.Keypad8 -> AardKeys.NumPad8
            | GameWindowKeys.Keypad9 -> AardKeys.NumPad9
            | GameWindowKeys.KeypadDivide -> AardKeys.Divide
            | GameWindowKeys.KeypadMultiply -> AardKeys.Multiply
            | GameWindowKeys.KeypadSubtract -> AardKeys.Subtract
            | GameWindowKeys.KeypadAdd -> AardKeys.Add
            | GameWindowKeys.KeypadDecimal -> AardKeys.Decimal
            | GameWindowKeys.KeypadEnter -> AardKeys.Enter
            | GameWindowKeys.A -> AardKeys.A
            | GameWindowKeys.B -> AardKeys.B
            | GameWindowKeys.C -> AardKeys.C
            | GameWindowKeys.D -> AardKeys.D
            | GameWindowKeys.E -> AardKeys.E
            | GameWindowKeys.F -> AardKeys.F
            | GameWindowKeys.G -> AardKeys.G
            | GameWindowKeys.H -> AardKeys.H
            | GameWindowKeys.I -> AardKeys.I
            | GameWindowKeys.J -> AardKeys.J
            | GameWindowKeys.K -> AardKeys.K
            | GameWindowKeys.L -> AardKeys.L
            | GameWindowKeys.M -> AardKeys.M
            | GameWindowKeys.N -> AardKeys.N
            | GameWindowKeys.O -> AardKeys.O
            | GameWindowKeys.P -> AardKeys.P
            | GameWindowKeys.Q -> AardKeys.Q
            | GameWindowKeys.R -> AardKeys.R
            | GameWindowKeys.S -> AardKeys.S
            | GameWindowKeys.T -> AardKeys.T
            | GameWindowKeys.U -> AardKeys.U
            | GameWindowKeys.V -> AardKeys.V
            | GameWindowKeys.W -> AardKeys.W
            | GameWindowKeys.X -> AardKeys.X
            | GameWindowKeys.Y -> AardKeys.Y
            | GameWindowKeys.Z -> AardKeys.Z
            | GameWindowKeys.Number0 -> AardKeys.D0
            | GameWindowKeys.Number1 -> AardKeys.D1
            | GameWindowKeys.Number2 -> AardKeys.D2
            | GameWindowKeys.Number3 -> AardKeys.D3
            | GameWindowKeys.Number4 -> AardKeys.D4
            | GameWindowKeys.Number5 -> AardKeys.D5
            | GameWindowKeys.Number6 -> AardKeys.D6
            | GameWindowKeys.Number7 -> AardKeys.D7
            | GameWindowKeys.Number8 -> AardKeys.D8
            | GameWindowKeys.Number9 -> AardKeys.D9
            | GameWindowKeys.Tilde -> AardKeys.OemTilde
            | GameWindowKeys.Minus -> AardKeys.OemMinus
            | GameWindowKeys.Plus -> AardKeys.OemPlus
            | GameWindowKeys.BracketLeft -> AardKeys.OemOpenBrackets
            | GameWindowKeys.BracketRight -> AardKeys.OemCloseBrackets
            | GameWindowKeys.Semicolon -> AardKeys.OemSemicolon
            | GameWindowKeys.Quote -> AardKeys.OemQuotes
            | GameWindowKeys.Comma -> AardKeys.OemComma
            | GameWindowKeys.Period -> AardKeys.OemPeriod
            | GameWindowKeys.Slash -> AardKeys.None
            | GameWindowKeys.BackSlash ->AardKeys.OemBackslash 
            | GameWindowKeys.NonUSBackSlash -> AardKeys.OemBackslash
            | _ -> AardKeys.None


    type Mouse() as this =
        inherit EventMouse(true)
        let mutable ctrl : Option<GameWindow> = None
        let mutable lastPos = PixelPosition(0,0,0,0)

        let size() =
            match ctrl with
                | Some ctrl -> V2i(ctrl.ClientSize.Width, ctrl.ClientSize.Height)
                | _ -> V2i.Zero

        let (~%) (m : GameWindowMouseButtons) =
            let mutable buttons = MouseButtons.None

            match m with
                | GameWindowMouseButtons.Left -> MouseButtons.Left
                | GameWindowMouseButtons.Right -> MouseButtons.Right
                | GameWindowMouseButtons.Middle -> MouseButtons.Middle
                | _ -> MouseButtons.None

        let (~%%) (e : MouseEventArgs) =
            let s = size()
            let pp = PixelPosition(e.X, e.Y, s.X, s.Y)
            pp

        let mousePos() =
            try
             match ctrl with
                | Some ctrl -> 
                    let state = OpenTK.Input.Mouse.GetCursorState()
                    let p = ctrl.PointToClient(Drawing.Point(state.X, state.Y))
                    let s = ctrl.ClientSize
        
                    let x = clamp 0 (s.Width-1) p.X
                    let y = clamp 0 (s.Height-1) p.Y

                    PixelPosition(x, y, ctrl.ClientSize.Width, ctrl.ClientSize.Height)
                | _ ->
                    PixelPosition(0,0,0,0)
             with e -> 
                Log.warn "could not grab mouse position."
                lastPos


        let onMouseDownHandler = EventHandler<MouseButtonEventArgs>(fun s e -> this.Down(%%e, %e.Button))
        let onMouseUpHandler = EventHandler<MouseButtonEventArgs>(fun s e -> this.Up(%%e, %e.Button))
        let onMouseMoveHandler = EventHandler<MouseMoveEventArgs>(fun s e -> this.Move %%e)
        let onMouseWheelHandler = EventHandler<MouseWheelEventArgs>(fun s e -> this.Scroll (%%e, (float e.Delta * 120.0)))
        let onMouseEnter = EventHandler<EventArgs>(fun s e -> this.Enter (mousePos()))
        let onMouseLeave = EventHandler<EventArgs>(fun s e -> this.Leave (mousePos()))

        


        let addHandlers() =
            match ctrl with
                | Some ctrl ->
                    ctrl.MouseDown.AddHandler onMouseDownHandler
                    ctrl.MouseUp.AddHandler onMouseUpHandler
                    ctrl.MouseMove.AddHandler onMouseMoveHandler
                    ctrl.MouseWheel.AddHandler onMouseWheelHandler
                    ctrl.MouseEnter.AddHandler onMouseEnter
                    ctrl.MouseLeave.AddHandler onMouseLeave
                | _ ->()

        let removeHandlers() =
            match ctrl with
                | Some ctrl ->
                    ctrl.MouseDown.RemoveHandler onMouseDownHandler
                    ctrl.MouseUp.RemoveHandler onMouseUpHandler
                    ctrl.MouseMove.RemoveHandler onMouseMoveHandler
                    ctrl.MouseWheel.RemoveHandler onMouseWheelHandler
                    ctrl.MouseEnter.RemoveHandler onMouseEnter
                    ctrl.MouseLeave.RemoveHandler onMouseLeave
                | None -> ()

        member x.SetControl(c : GameWindow) =
            removeHandlers()
            ctrl <- Some c
            addHandlers()

        member x.Dispose() = removeHandlers()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type Keyboard() as this =
        inherit EventKeyboard()

        let mutable ctrl : Option<GameWindow> = None

        let (~%) (k : GameWindowKeys) : Keys =
            translateKey k


        let onKeyDown (s : obj) (e : KeyboardKeyEventArgs) =
            this.KeyDown (%e.Key)

        let onKeyUp (s : obj) (e : KeyboardKeyEventArgs) =
            this.KeyUp (%e.Key)

        let onKeyPress (s : obj) (e : OpenTK.KeyPressEventArgs) =
            this.KeyPress e.KeyChar


        let onKeyDownHandler = EventHandler<KeyboardKeyEventArgs>(onKeyDown)
        let onKeyUpHandler = EventHandler<KeyboardKeyEventArgs>(onKeyUp)
        let onKeyPressHandler = EventHandler<OpenTK.KeyPressEventArgs>(onKeyPress)

        let addHandlers() =
            match ctrl with
                | Some ctrl ->
                   ctrl.KeyDown.AddHandler onKeyDownHandler
                   ctrl.KeyUp.AddHandler onKeyUpHandler
                   ctrl.KeyPress.AddHandler onKeyPressHandler
                | _ -> ()

        let removeHandlers() =
            match ctrl with
                | Some ctrl ->
                    ctrl.KeyDown.RemoveHandler onKeyDownHandler
                    ctrl.KeyUp.RemoveHandler onKeyUpHandler
                    ctrl.KeyPress.RemoveHandler onKeyPressHandler
                | _ -> ()

        member x.SetControl c =
            removeHandlers()
            ctrl <- Some c
            addHandlers()

        member x.Dispose() = 
            removeHandlers()
            ctrl <- None

        interface IDisposable with
            member x.Dispose() = x.Dispose()

module DefaultText =
    [<Literal>]
    let baseText = "Aardvark rocks \\o/ - OpenGL GameWindow"

type GameWindow(runtime : Runtime, enableDebug : bool, samples : int, graphicsMode : Graphics.GraphicsMode) as this =
    inherit OpenTK.GameWindow(
        1024,
        768,
        graphicsMode,
        DefaultText.baseText,
        GameWindowFlags.Default,
        DisplayDevice.Default,
        Config.MajorVersion, 
        Config.MinorVersion, 
        Config.ContextFlags,
        VSync = VSyncMode.Off,
        RenderAsFastAsPossible = false
    )
    let ctx = runtime.Context


    let mutable loaded = false
    let mutable task = RenderTask.empty
    let mutable taskSub : IDisposable = { new IDisposable with member x.Dispose() = () }

    let depthSignature =
        match Config.DepthBits, Config.StencilBits with
            | 0, 0 -> None
            | 16, 0 -> Some { format = RenderbufferFormat.DepthComponent16; samples = samples }
            | 24, 0 -> Some { format = RenderbufferFormat.DepthComponent24; samples = samples }
            | 32, 0 -> Some { format = RenderbufferFormat.DepthComponent32; samples = samples }
            | 24, 8 -> Some { format = RenderbufferFormat.Depth24Stencil8; samples = samples }
            | 32, 8 -> Some { format = RenderbufferFormat.Depth32fStencil8; samples = samples }
            | _ -> failwith "invalid depth-stencil mode"

    let fboSignature =
        let set =
            if graphicsMode.Stereo then 
                Set.ofList [
                    "ProjTrafo"; 
                    "ViewTrafo"; 
                    "ModelViewTrafo"; 
                    "ViewProjTrafo"; 
                    "ModelViewProjTrafo"
                       
                    "ProjTrafoInv"; 
                    "ViewTrafoInv"; 
                    "ModelViewTrafoInv"; 
                    "ViewProjTrafoInv"; 
                    "ModelViewProjTrafoInv"
                ]
            else Set.empty
        FramebufferSignature(
            runtime,
            Map.ofList [0, (DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = samples })],
            Map.empty,
            depthSignature,
            None,
            (if graphicsMode.Stereo then 2 else 1),
            set
        )




    let mutable contextHandle : ContextHandle = null 
    let defaultFramebuffer = 
        new Framebuffer(
            ctx, fboSignature, 
            (fun _ -> 0), 
            ignore, 
            [0, DefaultSemantic.Colors, Renderbuffer(ctx, 0, V2i.Zero, RenderbufferFormat.Rgba8, samples, 0L) :> IFramebufferOutput], None
        ) 
    let mutable defaultOutput = defaultFramebuffer |> OutputDescription.ofFramebuffer

    let mutable stereoFramebuffer : Option<OutputDescription * Framebuffer * Texture * Texture> = None
    let clearTask = runtime.CompileClear(fboSignature, AVal.constant C4f.Black, AVal.constant 1.0)

    let avgFrameTime = RunningMean(3)
    let sizes = AVal.init (V2i(base.ClientSize.Width, base.ClientSize.Height))
    let time = AVal.custom (fun s -> DateTime.Now + TimeSpan.FromSeconds(avgFrameTime.Average))
    let mutable first = true

    let mouse = new GameWindowIO.Mouse()
    let keyboard = new GameWindowIO.Keyboard()

    let startupTime = System.Diagnostics.Stopwatch()

   
    let frameWatch = System.Diagnostics.Stopwatch()

    let beforeRender = Event<unit>()
    let afterRender = Event<unit>()


    do mouse.SetControl this
       keyboard.SetControl this

    let mutable frameCount = 0
    let mutable totalTime = MicroTime.Zero
    let mutable baseTitle = ""

    //member private x.Invalidate() =
    //    if not x.RenderAsFastAsPossible then
    //        base.Invalidate()

    member x.NewFrame (t : MicroTime) = 
        frameCount <- frameCount + 1
        totalTime <- totalTime + t
        if frameCount > 50 then
            let fps = float frameCount / totalTime.TotalSeconds
            base.Title <- DefaultText.baseText + sprintf " (%.3f fps)" fps
            frameCount <- 0
            totalTime <- MicroTime.Zero
        ()

    member x.RenderTask
        with get() = task
        and set t = 
            task.Dispose()
            taskSub.Dispose()
            task <- t
            taskSub <- t.AddMarkingCallback (fun () -> x.Invalidate())

    member x.Sizes = sizes :> aval<_>

    member x.Time = time :> aval<_>

    member x.AverageFrameTime = MicroTime(int64 (avgFrameTime.Average * 1E9))

    member x.BeforeRender = beforeRender.Publish
    member x.AfterRender = afterRender.Publish

    override x.OnLoad(e) =
        GL.Hint(HintTarget.PointSmoothHint, HintMode.Fastest)
        GL.Enable(EnableCap.TextureCubeMapSeamless)
        GL.Disable(EnableCap.PolygonSmooth)
        let c = OpenTK.Graphics.GraphicsContext.CurrentContext
        if c <> null then
            c.MakeCurrent(null)

        if ContextHandle.primaryContext <> null then
            ContextHandle.primaryContext.MakeCurrent()

        let sw = System.Diagnostics.Stopwatch()
        x.BeforeRender.Add sw.Restart
        x.AfterRender.Add (fun () -> sw.Stop(); x.NewFrame sw.MicroTime)

        let k = keyboard :> IKeyboard
        k.KeyDown(Keys.End).Values.Add (fun () ->
            if AVal.force k.Control then
                x.RenderAsFastAsPossible <- not x.RenderAsFastAsPossible
        )
    
        let mutable oldState = x.WindowState
        let mutable oldBorder = x.WindowBorder
        let mutable full = false
        k.KeyDown(Keys.Enter).Values.Add(fun () ->
            if AVal.force k.Alt && AVal.force k.Shift then
                if full then
                    full <- false
                    x.WindowBorder <- oldBorder
                    x.WindowState <- oldState
                else
                    full <- true
                    oldState <- x.WindowState
                    oldBorder <- x.WindowBorder
                    x.WindowBorder <- WindowBorder.Hidden
                    x.WindowState <- WindowState.Fullscreen
        )





        base.OnLoad(e)
        loaded <- true
        base.MakeCurrent()
     
    override x.OnRenderFrame(e) =
        if loaded then
            frameWatch.Restart()

            beforeRender.Trigger()

            if contextHandle = null then
                contextHandle <- ContextHandle(base.Context, base.WindowInfo) 
                contextHandle.AttachDebugOutputIfNeeded(enableDebug)

            let size = V2i(base.ClientSize.Width, base.ClientSize.Height)
            
            Operators.using (ctx.RenderingLock contextHandle) (fun _ ->
                        
                if size <> sizes.Value then
                    transact (fun () -> sizes.Value <- size)

                if graphicsMode.Stereo then
                    let outputDesc, colorTex = 
                        match stereoFramebuffer with    
                        | Some (outputDesc, _, c, _) when c.Size.XY = size -> 
                            outputDesc, c
                        | _ -> 
                            match stereoFramebuffer with
                            | Some (_, f, c, d) -> 
                                ctx.Delete f
                                ctx.Delete c
                                ctx.Delete d
                            | _ -> ()
                            let c = ctx.CreateTexture(V3i(size,1), TextureDimension.Texture2D, TextureFormat.Rgba8, 2, 1, samples)
                            let d = ctx.CreateTexture(V3i(size,1), TextureDimension.Texture2D, TextureFormat.Depth24Stencil8, 2, 1, samples)
                            let f = 
                                runtime.CreateFramebuffer(
                                    fboSignature,
                                    [   
                                        DefaultSemantic.Colors, c.[TextureAspect.Color,0,*] :> IFramebufferOutput
                                        DefaultSemantic.Depth,  d.[TextureAspect.Depth,0,*] :> IFramebufferOutput
                                    ]
                                ) |> unbox<Framebuffer>
                            let o = OutputDescription.ofFramebuffer f
                            stereoFramebuffer <- Some (o,f,c,d)
                            o, c
                    
                    clearTask.Run(AdaptiveToken.Top, RenderToken.Empty, outputDesc)
                    task.Run(AdaptiveToken.Top, RenderToken.Empty, outputDesc)

                    let fSrc = GL.GenFramebuffer()
                    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
                    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fSrc)
                    
                    GL.FramebufferTextureLayer(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, colorTex.Handle, 0, 0)
                    GL.DrawBuffer(DrawBufferMode.BackLeft)
                    GL.BlitFramebuffer(0, 0, colorTex.Size.X, colorTex.Size.Y, 0, 0, colorTex.Size.X, colorTex.Size.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest)

                    GL.FramebufferTextureLayer(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, colorTex.Handle, 0, 1)
                    GL.DrawBuffer(DrawBufferMode.BackRight)
                    GL.BlitFramebuffer(0, 0, colorTex.Size.X, colorTex.Size.Y, 0, 0, colorTex.Size.X, colorTex.Size.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest)

                    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
                    GL.DeleteFramebuffer(fSrc)

                else
                    defaultFramebuffer.Size <- V2i(x.ClientSize.Width, x.ClientSize.Height)
                    defaultOutput <- { defaultOutput with viewport = Box2i(V2i.OO, defaultFramebuffer.Size - V2i.II) }

                    GL.ColorMask(true, true, true, true)
                    GL.DepthMask(true)
                    GL.Viewport(0,0,x.ClientSize.Width, x.ClientSize.Height)
                    GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f)
                    GL.ClearDepth(1.0)
                    GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)

                    let desc = OutputDescription.ofFramebuffer defaultFramebuffer
                    task.Run(AdaptiveToken.Top, RenderToken.Empty, defaultOutput)
                        

                x.SwapBuffers()

                if not time.OutOfDate then
                    transact (fun () -> time.MarkOutdated())
                        
            )
                    
            if not first then
                avgFrameTime.Add(frameWatch.Elapsed.TotalSeconds)
            first <- false

            afterRender.Trigger()

   
    member x.Mouse = mouse :> IMouse
    member x.Keyboard = keyboard :> IKeyboard     
    member x.FramebufferSignature = fboSignature :> IFramebufferSignature

    member x.Run()  =
        startupTime.Start()
        x.Run()
       

    interface IRenderTarget with
        member x.FramebufferSignature = fboSignature :> IFramebufferSignature
        member x.Runtime = runtime :> IRuntime
        member x.Time = time
        member x.RenderTask
            with get() = x.RenderTask
            and set t = x.RenderTask <- t
        member x.Sizes = sizes :> aval<_>
        member x.Samples = samples
        member x.BeforeRender = beforeRender.Publish
        member x.AfterRender = afterRender.Publish

    interface IRenderControl with
        member x.Mouse = mouse :> IMouse
        member x.Keyboard = keyboard :> IKeyboard

    interface IRenderWindow with
        member x.Run() = x.Run()

    new(runtime : Runtime, enableDebug : bool, samples : int, stereo : bool) =
        let graphicsMode = 
            Graphics.GraphicsMode(
                OpenTK.Graphics.ColorFormat(Config.BitsPerPixel), 
                Config.DepthBits, 
                Config.StencilBits, 
                samples, 
                OpenTK.Graphics.ColorFormat.Empty,
                Config.Buffers, 
                stereo
            )
        new GameWindow(runtime,enableDebug,samples,graphicsMode)

    new(runtime : Runtime, enableDebug : bool, samples : int) =
        new GameWindow(runtime, enableDebug, samples, false)
