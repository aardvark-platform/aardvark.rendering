namespace Aardvark.Application.Slim

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Application
open OpenTK
open OpenTK.Graphics
open OpenTK.Platform
open System.Threading
open System.Reactive.Linq

[<AutoOpen>]
module private GameWindowIO =
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
        let mutable ctrl : Option<INativeWindow> = None
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

        member x.SetControl(c : INativeWindow) =
            if isNull c then
                removeHandlers()
                ctrl <- None
            else
                removeHandlers()
                ctrl <- Some c
                addHandlers()

        member x.Dispose() = removeHandlers()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type Keyboard() as this =
        inherit EventKeyboard()

        let mutable ctrl : Option<INativeWindow> = None

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
            if isNull c then
                removeHandlers()
                ctrl <- None
            else
                removeHandlers()
                ctrl <- Some c
                addHandlers()

        member x.Dispose() = 
            removeHandlers()
            ctrl <- None

        interface IDisposable with
            member x.Dispose() = x.Dispose()



type ResizeEventArgs(size : V2i) =
    inherit EventArgs()
    member x.Size = size

type ResizeEventHandler = delegate of obj * ResizeEventArgs -> unit


type SimpleWindow(position : V2i, initialSize : V2i)  =
    
    let handle = Factory.Default.CreateNativeWindow(position.X, position.Y, initialSize.X, initialSize.Y, "Aardvark rocks \\o/", GraphicsMode.Default, GameWindowFlags.Default, DisplayDevice.Default)
    
    let mSize = Mod.init initialSize
    
    let eResize = Event<ResizeEventHandler, ResizeEventArgs>()
    let keyboard = new GameWindowIO.Keyboard()
    let mouse = new GameWindowIO.Mouse()


    let mutable isInvalid = 1
    let mutable isRunning = 0

    let mutable isClosed = new ManualResetEventSlim(true)

    let mutable loaded = false
    let mutable resizeSub : IDisposable = null 

    let mutable visible = false
    let mutable visibleSub : IDisposable = null

    let mutable fullscreen = false
    let mutable oldBorder = WindowBorder.Resizable
    let mutable oldState = WindowState.Normal

    member x.Load() =
        lock handle (fun () ->
            if not loaded then
            
                resizeSub <- 
                    handle.Resize.Subscribe(fun e -> 
                        let newSize = x.ClientSize
                        eResize.Trigger(x, ResizeEventArgs newSize)
                        if mSize.Value <> newSize then
                            transact (fun () -> mSize.Value <- newSize)
                    )
                visibleSub <- 
                    handle.Closing.Subscribe(fun _ ->
                        visible <- false
                    )

                keyboard.SetControl handle
                mouse.SetControl handle
                x.OnLoad()
                loaded <- true
                visible <- false
                

        )

    member private x.Unload() =
        if loaded then
            x.OnUnload()

            if not (isNull resizeSub) then
                resizeSub.Dispose()
                resizeSub <- null

            if not (isNull visibleSub) then
                visibleSub.Dispose()
                visibleSub <- null

            keyboard.SetControl null
            mouse.SetControl null


            loaded <- false

    abstract member OnLoad : unit -> unit
    default x.OnLoad() = ()
    
    abstract member OnUnload : unit -> unit
    default x.OnUnload() = ()

    abstract member OnRender : unit -> unit
    default x.OnRender() = ()

    member x.Invalidate() =
        Interlocked.CompareExchange(&isInvalid, 1, 0) = 0 |> ignore
        handle.Invalidate()

    member x.Title 
        with get() = handle.Title
        and set t = handle.Title <- t

    member x.Position
        with get() = V2i(handle.Location.X, handle.Location.Y)
        and set (p : V2i) = handle.Location <- System.Drawing.Point(p.X, p.Y)
        
    member x.ClientSize
        with get() = V2i(handle.ClientSize.Width, handle.ClientSize.Height)
        and set (s : V2i) = handle.ClientSize <- System.Drawing.Size(s.X, s.Y)

    member x.Size
        with get() = V2i(handle.Size.Width, handle.Size.Height)
        and set (s : V2i) = handle.Size <- System.Drawing.Size(s.X, s.Y)

    member x.Sizes = mSize :> IMod<_>
    member x.Keyboard = keyboard :> IKeyboard
    member x.Mouse = mouse :> IMouse

    member x.WindowInfo = handle.WindowInfo :> obj

    member x.Fullscreen
        with get() = fullscreen
        and set v =
            if v then
                if not fullscreen then
                    oldBorder <- handle.WindowBorder
                    oldState <- handle.WindowState
                    handle.WindowBorder <- WindowBorder.Hidden
                    handle.WindowState <- WindowState.Fullscreen
                fullscreen <- true
            else
                if fullscreen then
                    handle.WindowBorder <- oldBorder
                    handle.WindowState <- oldState
                fullscreen <- false
                    



    [<CLIEvent>]
    member x.Resize = eResize.Publish
    
    member x.Close() =
        handle.Close()

    member x.Run() =
        if Interlocked.Increment(&isRunning) = 1 then
            isClosed.Reset()

            x.Load()
            handle.Visible <- true
            visible <- true
            while visible do
                handle.ProcessEvents()
                let o = Interlocked.Exchange(&isInvalid, 0)
                if o = 1 then x.OnRender()

            x.Unload()

            isClosed.Set()
        else
            isClosed.Wait()
        
    member x.Dispose() =
        if handle.Exists && handle.Visible then handle.Close()
        x.Unload()
        keyboard.Dispose()
        mouse.Dispose()
        handle.Dispose()
        transact (fun () -> mSize.Value <- initialSize)

    interface IDisposable with
        member x.Dispose() = x.Dispose()
