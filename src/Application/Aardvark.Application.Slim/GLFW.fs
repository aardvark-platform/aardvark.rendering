namespace Aardvark.Glfw

open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Rendering
open System.Threading
open System.Threading.Tasks
open Silk.NET.GLFW
open System.Runtime.InteropServices
open FSharp.Control
open FSharp.Data.Adaptive
open System.Runtime.InteropServices

type private IGamepad = Aardvark.Application.IGamepad
type private GamepadButton = Aardvark.Application.GamepadButton
type private Cursor = Aardvark.Application.Cursor

#nowarn "9"
#nowarn "51"

module Translations =

    type K = Silk.NET.GLFW.Keys
    type A = Aardvark.Application.Keys

    let table (elements : array<'a * 'b>) =
        let dict = System.Collections.Generic.Dictionary<'a, 'b>()
        for (k,v) in elements do
            dict.[k] <- v
        fun k ->
            let mutable v = Unchecked.defaultof<'b>
            if dict.TryGetValue(k, &v) then
                ValueSome v
            else
                ValueNone           

    let getKeyByName =
        table [|
            "A", A.A
            "B", A.B
            "C", A.C
            "D", A.D
            "E", A.E
            "F", A.F
            "G", A.G
            "H", A.H
            "I", A.I
            "J", A.J
            "K", A.K
            "L", A.L
            "M", A.M
            "N", A.N
            "O", A.O
            "P", A.P
            "Q", A.Q
            "R", A.R
            "S", A.S
            "T", A.T
            "U", A.U
            "V", A.V
            "W", A.W
            "X", A.X
            "Y", A.Y
            "Z", A.Z

            "-", A.OemMinus
            ".", A.OemPeriod
            "+", A.OemPlus
            ",", A.OemComma
            ";", A.OemSemicolon

            "ß", A.OemOpenBrackets
            "´", A.Oem6
            "Ü", A.Oem1
            "Ö", A.Oem3
            "Ä", A.OemQuotes
            "#", A.OemQuestion
            "^", A.Oem5
            "<", A.OemBackslash
        |]

    let getKeyByKey = 
        table [|
            K.A, A.A
            K.AltLeft, A.LeftAlt
            K.AltRight, A.RightAlt
            K.Apostrophe, A.OemQuotes
            K.B, A.B
            K.BackSlash, A.OemBackslash
            K.Backspace, A.Back
            K.C, A.C
            K.CapsLock, A.CapsLock
            K.Comma, A.OemComma
            K.ControlLeft, A.LeftCtrl
            K.ControlRight, A.RightCtrl
            K.D, A.D
            K.D0, A.D0
            K.Delete, A.Delete
            K.Down, A.Down
            K.E, A.E
            K.End, A.End
            K.Enter, A.Enter
            K.Equal, A.None // TODO
            K.Escape, A.Escape
            K.F, A.F
            K.F1, A.F1
            K.F2, A.F2
            K.F3, A.F3
            K.F4, A.F4
            K.F5, A.F5
            K.F6, A.F6
            K.F7, A.F7
            K.F8, A.F8
            K.F9, A.F9
            K.F10, A.F10
            K.F11, A.F11
            K.F12, A.F12
            K.F13, A.F13
            K.F14, A.F14
            K.F15, A.F15
            K.F16, A.F16
            K.F17, A.F17
            K.F18, A.F18
            K.F19, A.F19
            K.F20, A.F20
            K.F21, A.F21
            K.F22, A.F22
            K.F23, A.F23
            K.F24, A.F24
            K.F25, A.None
            K.G, A.G
            K.GraveAccent, A.Oem5
            K.H, A.H
            K.Home, A.Home
            K.I, A.I
            K.Insert, A.Insert
            K.J, A.J
            K.K, A.K
            K.Keypad0, A.NumPad0
            K.Keypad1, A.NumPad1
            K.Keypad2, A.NumPad2
            K.Keypad3, A.NumPad3
            K.Keypad4, A.NumPad4
            K.Keypad5, A.NumPad5
            K.Keypad6, A.NumPad6
            K.Keypad7, A.NumPad7
            K.Keypad8, A.NumPad8
            K.Keypad9, A.NumPad9
            K.KeypadAdd, A.Add
            K.KeypadDecimal, A.Decimal
            K.KeypadDivide, A.Divide
            K.KeypadEnter, A.Return
            K.KeypadEqual, A.None // TODO
            K.KeypadMultiply, A.Multiply
            K.KeypadSubtract, A.Subtract
            K.L, A.L
            K.LastKey, A.None // TODO
            K.Left, A.Left
            K.LeftBracket, A.None // TODO
            K.M, A.M
            K.Menu, A.None // TODO
            K.Minus, A.OemMinus
            K.N, A.N
            K.Number1, A.D1
            K.Number2, A.D2
            K.Number3, A.D3
            K.Number4, A.D4
            K.Number5, A.D5
            K.Number6, A.D6
            K.Number7, A.D7
            K.Number8, A.D8
            K.Number9, A.D9
            K.NumLock, A.NumLock
            K.O, A.O
            K.P, A.P
            K.PageDown, A.PageDown
            K.PageUp, A.PageUp
            K.Pause, A.Pause
            K.Period, A.OemPeriod
            K.PrintScreen, A.PrintScreen
            K.Q, A.Q
            K.R, A.R
            K.Right, A.Right
            K.RightBracket, A.None // TODO
            K.S, A.S
            K.ScrollLock, A.Scroll
            K.Semicolon, A.OemSemicolon
            K.ShiftLeft, A.LeftShift
            K.ShiftRight, A.RightShift
            K.Slash, A.None // TODO
            K.Space, A.Space
            K.SuperLeft, A.LWin
            K.SuperRight, A.RWin
            K.T, A.T
            K.Tab, A.Tab
            K.U, A.U
            K.Unknown, A.None
            K.Up, A.Up
            K.V, A.V
            K.W, A.W
            K.World1, A.None // TODO
            K.World2, A.None // TODO
            K.X, A.X
            K.Y, A.Y
            K.Z, A.Z

        |]

    let tryGetKey (k : Keys) (scan : int) (name : string) =     
        match getKeyByName name with
        | ValueSome k when k <> A.None -> ValueSome k
        | _ ->
            match getKeyByKey k with
            | ValueSome k  when k <> A.None -> ValueSome k
            | _ ->  
                if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                    let k = Aardvark.Application.KeyConverter.keyFromVirtualKey scan
                    if k <> A.None then ValueSome k
                    else ValueNone
                else
                    ValueNone               


    let toMouseButton (b : MouseButton) =
        match b with
        | MouseButton.Left -> Aardvark.Application.MouseButtons.Left
        | MouseButton.Right -> Aardvark.Application.MouseButtons.Right
        | MouseButton.Middle -> Aardvark.Application.MouseButtons.Middle
        | _ -> Aardvark.Application.MouseButtons.None

[<AutoOpen>]
module MissingGlfwFunctions =
    open System.Runtime.InteropServices

    type private GetWindowContentScaleDel = delegate of nativeptr<WindowHandle> * byref<float32> * byref<float32> -> unit

    type private GetKeyNameDel = delegate of Keys * int -> nativeint

    let private scaleDict = System.Collections.Concurrent.ConcurrentDictionary<Glfw, GetWindowContentScaleDel>()
    let private keyNameDict = System.Collections.Concurrent.ConcurrentDictionary<Glfw, GetKeyNameDel>()

    let private getWindowScale (glfw : Glfw) =
        scaleDict.GetOrAdd(glfw, fun glfw ->
            let m = glfw.Context.GetProcAddress "glfwGetWindowContentScale"
            Marshal.GetDelegateForFunctionPointer(m, typeof<GetWindowContentScaleDel>) |> unbox<GetWindowContentScaleDel>
        )
    let private getKeyName (glfw : Glfw) =
        keyNameDict.GetOrAdd(glfw, fun glfw ->
            let m = glfw.Context.GetProcAddress "glfwGetKeyName"
            Marshal.GetDelegateForFunctionPointer(m, typeof<GetKeyNameDel>) |> unbox<GetKeyNameDel>
        )

    type Glfw with
        member x.GetWindowContentScale(win : nativeptr<WindowHandle>, [<Out>] sx : byref<float32>, [<Out>] sy : byref<float32>) =
            getWindowScale(x).Invoke(win, &sx, &sy)

        member x.GetKeyNamePtr(key : Keys, code : int) =
            let mutable ptr = getKeyName(x).Invoke(key, code) |> NativePtr.ofNativeInt<byte>

            if ptr = NativePtr.zero then 
                ""
            else
                let l = System.Collections.Generic.List<byte>()
                let mutable c = NativePtr.read ptr
                while c <> 0uy do
                    l.Add c
                    ptr <- NativePtr.add ptr 1
                    c <- NativePtr.read ptr

                System.Text.Encoding.UTF8.GetString(l.ToArray())

type WindowEvent =
    | Resize
    | Run of action : (unit -> unit)


type KeyEvent(key : Aardvark.Application.Keys, scanCode : int, action : InputAction, modifiers : KeyModifiers, keyName : string) =
    member x.Key = key
    member x.ScanCode = scanCode
    member x.Name = keyName  

    member x.IsRepeat = action = InputAction.Repeat
    member x.Alt = int (modifiers &&& KeyModifiers.Alt) <> 0
    member x.Shift = int (modifiers &&& KeyModifiers.Shift) <> 0
    member x.Ctrl = int (modifiers &&& KeyModifiers.Control) <> 0
    member x.Super = int (modifiers &&& KeyModifiers.Super) <> 0

    override x.ToString() =
        let kind =
            match action with
            | InputAction.Press -> "KeyDown"
            | InputAction.Repeat -> "KeyRepeat"
            | InputAction.Release -> "KeyUp"
            | _ -> "KeyUnknown"

        let modifiers = 
            [
                if x.Alt then yield "alt"
                if x.Shift then yield "shift"
                if x.Ctrl then yield "ctrl"
                if x.Super then yield "super"          
            ]

        sprintf "%s { key: %A; scan: %A; mod: [%s]; name: %s }" kind key scanCode (String.concat "; " modifiers) keyName

type ResizeEvent(framebufferSize : V2i, physicalSize : V2i, windowSize : V2i) =
    member x.FramebufferSize = framebufferSize
    member x.PhysicalSize = physicalSize
    member x.WindowSize = windowSize

    override x.ToString() = 
        sprintf "Resize { framebuffer: %A; physical: %A; window: %A }" framebufferSize physicalSize windowSize

type MouseEvent(button : Aardvark.Application.MouseButtons, position: V2d, action : InputAction, modifiers : KeyModifiers) =
    member x.Button = button
    member x.Alt = int (modifiers &&& KeyModifiers.Alt) <> 0
    member x.Shift = int (modifiers &&& KeyModifiers.Shift) <> 0
    member x.Ctrl = int (modifiers &&& KeyModifiers.Control) <> 0
    member x.Super = int (modifiers &&& KeyModifiers.Super) <> 0
    member x.Position = position

    override x.ToString() =
        let kind =
            match action with
            | InputAction.Press -> "MouseDown"
            | InputAction.Release -> "MouseUp"
            | _ -> "MouseUnknown"

        let modifiers = 
            [
                if x.Alt then yield "alt"
                if x.Shift then yield "shift"
                if x.Ctrl then yield "ctrl"
                if x.Super then yield "super"          
            ]

        sprintf "%s { button: %A; mod: [%s]; position: %A }" kind button (String.concat "; " modifiers) position

type WindowState =
    | Normal = 0
    | Minimized = 1
    | Maximized = 2
    | Invisible = 3



type WindowConfig =
    {
        title : string
        width : int
        height : int
        focus : bool
        resizable : bool
        vsync : bool
        transparent : bool
        opengl : bool
        physicalSize : bool
        samples : int
    }    


module IconLoader = 
    
    type private Self = Self
    let private getIcon'() = 
        let sizes = Array.sortDescending [| 16; 24; 32; 48; 64; 128; 256 |]
        let ass = typeof<Self>.Assembly
        let name = ass.GetManifestResourceNames() |> Array.find (fun n -> n.EndsWith "aardvark.png")

        let img = 
            use src = ass.GetManifestResourceStream name
            PixImageSharp.Create(src).ToPixImage<byte>(Col.Format.RGBA)
        
        let levels =
            let mutable last = img
            sizes |> Array.map (fun s ->
                if s = last.Size.X && s = last.Size.Y then
                    last :> PixImage
                else
                    let dst = PixImage<byte>(Col.Format.RGBA, V2i(s,s))
                    NativeVolume.using last.Volume (fun src ->
                        NativeVolume.using dst.Volume (fun dst ->
                            NativeVolume.blit src dst
                        )
                    )

                    last <- dst
                    dst :> PixImage
            )

        PixImageMipMap levels

    let getIcon() =
        try 
            getIcon'() |> Some
        with e -> 
            Log.warn "could not load icon. %A" e.Message
            None


type GlfwGamepad() =
    let a = cval false
    let b = cval false
    let x = cval false
    let y = cval false
    let ls = cval false
    let rs = cval false
    let lsh = cval false
    let rsh = cval false

    let pl = cval false
    let pr = cval false
    let pu = cval false
    let pd = cval false
    let select = cval false
    let start = cval false

    let down = EventSource<GamepadButton>()
    let up = EventSource<GamepadButton>()

    let left = cval V2d.Zero
    let right = cval V2d.Zero
    let leftTrigger = cval 0.0
    let rightTrigger = cval 0.0

    let buttons =
        [|
            GamepadButton.A
            GamepadButton.B
            GamepadButton.Unknown
            GamepadButton.X
            GamepadButton.Y
            GamepadButton.Unknown
            GamepadButton.LeftShoulder; GamepadButton.RightShoulder
            GamepadButton.Unknown; GamepadButton.Unknown; GamepadButton.Unknown
            GamepadButton.Start
            GamepadButton.Unknown
            GamepadButton.LeftStick; GamepadButton.RightStick
            GamepadButton.Unknown
            GamepadButton.Select
            GamepadButton.CrossUp; GamepadButton.CrossRight; GamepadButton.CrossDown; GamepadButton.CrossLeft

        |]

    let changeables =
        [|
            a; b
            cval false
            x; y
            cval false
            lsh; rsh
            cval false; cval false; cval false
            start; 
            cval false
            ls; rs
            cval false
            select
            pu; pr; pd; pl
        |]


    member _.Update(glfw : Glfw, id : int) =
        let mutable cnt = 0
        let ptr = glfw.GetJoystickButtons(id, &cnt)

        let inline trigger (index : int) (state : bool) =
            if index < buttons.Length then
                let b = buttons.[index]
                if b <> GamepadButton.Unknown then
                    let o = changeables.[index]
                    if o.Value <> state then
                        o.Value <- state
                        if state then down.Emit(b) else up.Emit(b)

        for i in 0 .. cnt - 1 do
            trigger i (NativePtr.get ptr i <> 0uy)

        let axes = glfw.GetJoystickAxes(id, &cnt)
        let mutable vl = V2d.Zero
        let mutable vr = V2d.Zero
        let mutable vlt = 0.0
        let mutable vrt = 0.0

        for i in 0 .. cnt - 1 do
            let v = NativePtr.get axes i
            match i with
            | 0 -> vl.X <- float v
            | 1 -> vl.Y <- float -v
            | 2 -> vr.X <- float v
            | 3 -> vr.Y <- float -v
            | 4 -> vrt <- float v * 0.5 + 0.5
            | 5 -> vlt <- float v * 0.5 + 0.5
            | _ -> Log.warn "bad axis %d: %.3f" i v

        let vl = 
            if Vec.length vl > 0.1 then vl
            else V2d.Zero

        let vr = 
            if Vec.length vr > 0.1 then vr
            else V2d.Zero

        left.Value <- vl
        right.Value <- vr
        leftTrigger.Value <- vlt
        rightTrigger.Value <- vrt

    interface IGamepad with
        member __.Down = down :> Aardvark.Base.IEvent<_>
        member __.Up = up :> Aardvark.Base.IEvent<_>
        member __.A = a :> aval<_>
        member __.B = b :> aval<_>
        member __.X = x :> aval<_>
        member __.Y = y :> aval<_>
        member __.LeftStick = left :> aval<_>
        member __.RightStick = right :> aval<_>
        member __.LeftTrigger = leftTrigger :> aval<_>
        member __.RightTrigger = rightTrigger :> aval<_>
        member __.LeftShoulder = lsh :> aval<_>
        member __.RightShoulder = rsh :> aval<_>
        member __.Select = select :> aval<_>
        member __.Start = start :> aval<_>
        member __.CrossUp = pu :> aval<_>
        member __.CrossDown = pd :> aval<_>
        member __.CrossLeft = pl :> aval<_>
        member __.CrossRight = pr :> aval<_>
        member __.LeftStickDown = ls :> aval<_>
        member __.RightStickDown = rs :> aval<_>

type ISwapchain =
    inherit System.IDisposable
    abstract Size : V2i
    abstract Run : IRenderTask * IQuery -> bool

type IWindowSurface =
    inherit System.IDisposable
    abstract Signature : IFramebufferSignature
    abstract CreateSwapchain : V2i -> ISwapchain
    abstract Handle : obj

type IWindowInterop =
    abstract Boot : Glfw -> unit
    abstract CreateSurface : IRuntime * WindowConfig * Glfw * nativeptr<WindowHandle> -> IWindowSurface
    abstract WindowHints : WindowConfig * Glfw -> unit


type Application(runtime : Aardvark.Rendering.IRuntime, interop : IWindowInterop, hideCocoaMenuBar : bool) =
    [<System.ThreadStatic; DefaultValue>]
    static val mutable private IsMainThread_ : bool

    let glfw = Glfw.GetApi()
    do 
        if hideCocoaMenuBar then
            Log.line "hiding cocoa menubar"
            glfw.InitHint(Silk.NET.GLFW.InitHint.CocoaMenubar, false) // glfwInitHint(GLFW_COCOA_MENUBAR, GLFW_FALSE);
        
        if not (glfw.Init()) then  
            failwith "GLFW init failed"

        interop.Boot glfw

    let mutable lastWindow : nativeptr<WindowHandle> option = None
    let queue = System.Collections.Concurrent.ConcurrentQueue<unit -> unit>()

    let existingWindows = System.Collections.Concurrent.ConcurrentHashSet<Window>()
    let visibleWindows = System.Collections.Concurrent.ConcurrentHashSet<Window>()
    do Application.IsMainThread_ <- true

    let aardvarkIcon = IconLoader.getIcon()
                

    member x.Runtime = runtime

    member x.AddExistingWindow(w : Window) =
        existingWindows.Add w |> ignore
        glfw.PostEmptyEvent()

    member x.RemoveExistingWindow(w : Window) =
        existingWindows.Remove w |> ignore
        visibleWindows.Remove w |> ignore
        glfw.PostEmptyEvent()

    member x.AddVisibleWindow(w : Window) =
        visibleWindows.Add w |> ignore
        glfw.PostEmptyEvent()

    member x.RemoveVisibleWindow(w : Window) =
        visibleWindows.Remove w |> ignore
        glfw.PostEmptyEvent()

    member x.Glfw = glfw

    member x.IsMainThread = Application.IsMainThread_

    member x.Post(action : unit -> unit) =
        if Application.IsMainThread_ then
            try action()
            with _ -> ()            
        else        
            queue.Enqueue(action)
            glfw.PostEmptyEvent()

    member x.StartTask<'r>(action : unit -> 'r) : Task<'r> =
        if Application.IsMainThread_ then
            try
                let v = action()
                Task.FromResult v
            with e ->
                Task.FromException<'r> e          
        else        
            let tcs = TaskCompletionSource<'r>()
            x.Post(fun () ->
                try 
                    let v = action()
                    tcs.SetResult v
                with
                | :? System.OperationCanceledException -> tcs.SetCanceled()       
                | e -> tcs.SetException e            
            )     
            tcs.Task           

    member x.Invoke<'r>(action : unit -> 'r) : 'r =
        if Application.IsMainThread_ then
            action()
        else                
            let l = obj()
            let mutable res = None
            x.Post(fun () ->
                let value = 
                    try Result.Ok (action())
                    with e -> Result.Error e    
                lock l (fun () ->
                    res <- Some value
                    Monitor.PulseAll l
                )            
            )     
            lock l (fun () ->
                while Option.isNone res do
                    Monitor.Wait l |> ignore
                match res.Value with
                | Result.Ok v -> v
                | Result.Error e -> raise e         
            )      

    member x.CreateWindow(cfg : WindowConfig) =
        x.Invoke(fun () ->
            let old = glfw.GetCurrentContext()
            if old <> NativePtr.zero then
                glfw.MakeContextCurrent(NativePtr.zero)

            let parent =
                lastWindow |> Option.defaultValue NativePtr.zero

            glfw.DefaultWindowHints()

            interop.WindowHints(cfg, glfw)
            
            let retina =
                if RuntimeInformation.IsOSPlatform OSPlatform.OSX then cfg.physicalSize
                else false
            glfw.WindowHint(unbox<WindowHintBool> 0x00023001, retina)
            glfw.WindowHint(WindowHintBool.TransparentFramebuffer, cfg.transparent)
            glfw.WindowHint(WindowHintBool.Visible, false)
            glfw.WindowHint(WindowHintBool.Resizable, cfg.resizable)
            glfw.WindowHint(WindowHintInt.RefreshRate, 0)
            glfw.WindowHint(WindowHintBool.FocusOnShow, cfg.focus)

            let win = glfw.CreateWindow(cfg.width, cfg.height, cfg.title, NativePtr.zero, parent)
            if win = NativePtr.zero then
                failwithf "GLFW could not create window: %A" (glfw.GetError())

            lastWindow <- Some win
            
            let surface = interop.CreateSurface(runtime, cfg, glfw, win)
        
            let w = new Window(x, win, cfg.title, cfg.vsync, surface, surface.Signature.Samples, retina)

            if cfg.samples <> w.Samples then
                Report.Line(3, $"using {w.Samples} samples for window surface (requested {cfg.samples})")

            match aardvarkIcon with
            | Some icon -> w.Icon <- Some icon
            | _ -> ()


            w
        )        

    member x.Run([<System.ParamArray>] ws : Window[]) =    
        let mutable wait = false

        for w in ws do w.IsVisible <- true

        while existingWindows.Count > 0 do
            if wait then glfw.WaitEventsTimeout(0.01)
            else glfw.PollEvents()

            let mutable action = Unchecked.defaultof<unit -> unit>
            while queue.TryDequeue(&action) do
                try action()
                with _ -> ()

            for e in existingWindows do
                e.Update()

            wait <- true
            for w in visibleWindows do
                let v = w.Redraw()
                if v then wait <- false

    new(runtime, swap) = Application(runtime, swap, false)

and Window(app : Application, win : nativeptr<WindowHandle>, title : string, enableVSync : bool, surface : IWindowSurface, samples : int, retina : bool) as this =
    static let keyNameCache = System.Collections.Concurrent.ConcurrentDictionary<Keys * int, string>()

    let glfw = app.Glfw

    let contentScale() =
        if retina then
            let mutable scale = V2f.II
            glfw.GetWindowContentScale(win, &scale.X, &scale.Y)
            V2d scale
        else
            V2d.II
        
    let mutable requiresRedraw = true
    let mutable title = title
    let mutable icon : option<PixImageMipMap> = None
    let mutable lastMousePosition = V2d.Zero
    let mutable enableVSync = enableVSync
    let mutable vsync = enableVSync
    let mutable showFrameTime = true

    do app.AddExistingWindow this

    let getWindowState() =
        let vis = glfw.GetWindowAttrib(win, WindowAttributeGetter.Visible)
        if vis then 
            let min = glfw.GetWindowAttrib(win, WindowAttributeGetter.Iconified)
            let max = glfw.GetWindowAttrib(win, WindowAttributeGetter.Maximized)
            if min then WindowState.Minimized
            elif max then WindowState.Maximized
            else WindowState.Normal
        else
            WindowState.Invisible        


    let getKeyName(key : Keys) (code : int) =
        keyNameCache.GetOrAdd((key, code), fun (key, code) ->
            let c = if code >= 0 then code else glfw.GetKeyScancode(int key)
            let str = glfw.GetKeyNamePtr(key, c)
            if System.String.IsNullOrWhiteSpace str then
                string key
            else
                let a = str.Substring(0, 1)
                let b = str.Substring(1)   
                a.ToUpper() + b
        )

    let closeEvt = Event<unit>()    
    let resize = Event<ResizeEvent>()
    let wpChanged = Event<V2i>()
    let cpChanged = Event<V2i>()
    let focus = Event<bool>()
    let stateChanged = Event<WindowState>()
    let dropFiles = Event<string[]>()

    let keyDown = Event<KeyEvent>()
    let keyUp = Event<KeyEvent>()
    let keyInput = Event<string>()

    let mouseMove = Event<V2d>()
    let mouseDown = Event<MouseEvent>()
    let mouseUp = Event<MouseEvent>()
    let mouseWheel = Event<V2d>()
    let mouseEnter = Event<V2d>()
    let mouseLeave = Event<V2d>()

    let beforeRender = Event<unit>()
    let afterRender = Event<unit>()

    let getFrameBorder() =
        if glfw.GetWindowAttrib(win, WindowAttributeGetter.Decorated) && glfw.GetWindowMonitor(win) = NativePtr.zero then
            let mutable border = Border2i()
            glfw.GetWindowFrameSize(win, &border.Min.X, &border.Min.Y, &border.Max.X, &border.Max.Y)
            if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && System.Environment.OSVersion.Version.Major = 10 then
                if glfw.GetWindowAttrib(win, WindowAttributeGetter.Decorated) then
                    // see https://github.com/glfw/glfw/issues/539
                    border.Min.X <- 1
                    border.Max.X <- 1
                    border.Max.Y <- 1
            border  
        else
            Border2i()              

    let framebufferSize() =
        let mutable ps = V2i.Zero
        if retina then
            glfw.GetFramebufferSize(win, &ps.X, &ps.Y)
        else
            glfw.GetWindowSize(win, &ps.X, &ps.Y)
        ps 

    let getResizeEvent() =
        let mutable fbo = V2i.Zero
        let mutable ps = V2i.Zero
        
        let border = getFrameBorder()
        let scale = contentScale()
        
        glfw.GetWindowSize(win, &ps.X, &ps.Y)
        let fbo = framebufferSize()
        let ws = ps + border.Min + border.Max
        let ps = V2i(round (float scale.X * float ps.X), round(float scale.Y * float ps.Y))
        let ws = V2i(round (float scale.X * float ws.X), round(float scale.Y * float ws.Y))        

        ResizeEvent(
            fbo,
            ps, 
            ws
        )

    let getMousePosition() =
        let v = 
            glfw.GetWindowAttrib(win, WindowAttributeGetter.Visible) &&
            not (glfw.GetWindowAttrib(win, WindowAttributeGetter.Iconified))
        if v then
            let mutable pos = V2d.Zero
            glfw.GetCursorPos(win, &pos.X, &pos.Y)
            lastMousePosition <- pos * contentScale()

        lastMousePosition

    let getPixelPostion() =
        let pos = getMousePosition()
        let sfbo = framebufferSize()
        PixelPosition(V2i(pos.Round()), Box2i(V2i.Zero, sfbo))

    let keyboard = Aardvark.Application.EventKeyboard()
    let mouse = Aardvark.Application.EventMouse(true)

    // let signature =
    //     app.Runtime.CreateFramebufferSignature(samples, [
    //         DefaultSemantic.Colors, RenderbufferFormat.Rgba8
    //         DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
    //     ])

    let currentSize =
        let s = getResizeEvent()
        cval s.FramebufferSize

    let setCurrentSize s =
        if Vec.allGreater s 0 && currentSize.Value <> s then
            transact (fun () -> currentSize.Value <- s)

    let _resizeCb =
        glfw.SetFramebufferSizeCallback(win, GlfwCallbacks.FramebufferSizeCallback(fun _ w h ->
            let evt = getResizeEvent()
            setCurrentSize evt.FramebufferSize
            resize.Trigger evt     
            requiresRedraw <- true
            this.Redraw() |> ignore
        ))

    let _maxCb =
        glfw.SetWindowMaximizeCallback(win, GlfwCallbacks.WindowMaximizeCallback(fun w b ->
            let windowState = getWindowState()
            stateChanged.Trigger windowState
        ))

    let _minCb =
        glfw.SetWindowIconifyCallback(win, GlfwCallbacks.WindowIconifyCallback(fun w b ->
            let windowState = getWindowState()
            stateChanged.Trigger windowState
        ))

    let _closingCallback = 
        glfw.SetWindowCloseCallback(win, GlfwCallbacks.WindowCloseCallback(fun w ->
            closeEvt.Trigger()
            glfw.HideWindow(win)
            app.RemoveExistingWindow this
        ))

    let _focusCallback =
        glfw.SetWindowFocusCallback(win, GlfwCallbacks.WindowFocusCallback(fun w f ->
            focus.Trigger(f)
        ))

    let _posCb =
        glfw.SetWindowPosCallback(win, GlfwCallbacks.WindowPosCallback(fun w x y ->
            let border = getFrameBorder()
            wpChanged.Trigger(V2i(x,y) - border.Min)
            cpChanged.Trigger(V2i(x,y))
        ))

    let _keyCallback =
        glfw.SetKeyCallback(win, GlfwCallbacks.KeyCallback(fun w k c a m ->
            let name = getKeyName k c
            match Translations.tryGetKey k c name with
            | ValueSome k -> 
                match a with
                | InputAction.Press -> 
                    keyboard.KeyDown(k)
                    keyDown.Trigger(KeyEvent(k, c, a, m, name))
                | InputAction.Repeat -> 
                    keyboard.KeyDown(k)
                    keyDown.Trigger(KeyEvent(k,c, a, m, name))
                | InputAction.Release -> 
                    keyboard.KeyUp(k)
                    keyUp.Trigger(KeyEvent(k, c, a, m, name))
                | _ -> ()
            | ValueNone ->
                ()       
        ))

    let _inputCallback =
        glfw.SetCharCallback(win, GlfwCallbacks.CharCallback (fun w c ->
            let str = System.Text.Encoding.UTF32.GetString(System.BitConverter.GetBytes(c))
            for c in str do keyboard.KeyPress c
            keyInput.Trigger(str)
        ))


    let _moveCallback =
        glfw.SetCursorPosCallback(win, GlfwCallbacks.CursorPosCallback(fun w a b ->
            let v = 
                glfw.GetWindowAttrib(w, WindowAttributeGetter.Visible) &&
                not (glfw.GetWindowAttrib(w, WindowAttributeGetter.Iconified))
            if v then
                let sfbo = framebufferSize()
                let p = V2d(a,b) * contentScale()
                
                let pp = PixelPosition(V2i(p.Round()), Box2i(V2i.Zero, sfbo))
                mouse.Move(pp)
                lastMousePosition <- p
                mouseMove.Trigger(p)
        ))

    let _mouseCallback =
        glfw.SetMouseButtonCallback(win, GlfwCallbacks.MouseButtonCallback(fun w button action modifiers ->
            let mutable pos = V2d.Zero
            glfw.GetCursorPos(win, &pos.X, &pos.Y)
            let sfbo = framebufferSize()
            let pos = pos * contentScale()
            let evt = MouseEvent(Translations.toMouseButton button, pos, action, modifiers)
            match action with
            | InputAction.Press -> 
                let pp = PixelPosition(V2i(pos.Round()), Box2i(V2i.Zero, sfbo))
                mouse.Down(pp, evt.Button)
                mouseDown.Trigger evt    
            | InputAction.Release -> 
                let pp = PixelPosition(V2i(pos.Round()), Box2i(V2i.Zero, sfbo))
                mouse.Up(pp, evt.Button)
                mouseUp.Trigger evt
            | _ -> ()       
        ))    

    let _wheelCallback =
        glfw.SetScrollCallback(win, GlfwCallbacks.ScrollCallback(fun w dx dy ->
            let mutable pos = V2d.Zero
            glfw.GetCursorPos(win, &pos.X, &pos.Y)
            let pp = getPixelPostion()
            mouse.Scroll(pp, dy * 120.0)
            mouseWheel.Trigger(V2d(dx, dy) * 120.0)
        ))    

    let _damagedCallback =
        glfw.SetWindowRefreshCallback(win, GlfwCallbacks.WindowRefreshCallback(fun w ->
            requiresRedraw <- true
        ))

    let _dropCallback =
        glfw.SetDropCallback(win, GlfwCallbacks.DropCallback(fun _ cnt paths ->
            let ptr = NativePtr.ofNativeInt paths



            let files = 
                Array.init cnt (fun i ->
                    let mutable ptr = NativePtr.get ptr i
                    if ptr = NativePtr.zero then 
                        ""
                    else
                        let l = System.Collections.Generic.List<byte>()
                        let mutable c = NativePtr.read ptr
                        while c <> 0uy do
                            l.Add c
                            ptr <- NativePtr.add ptr 1
                            c <- NativePtr.read ptr

                        System.Text.Encoding.UTF8.GetString(l.ToArray())
                )
            dropFiles.Trigger files                 
        ))

    let _enterLeave =
        glfw.SetCursorEnterCallback(win, GlfwCallbacks.CursorEnterCallback(fun _ entered ->
            if entered then
                mouse.Enter(getPixelPostion())
                mouseEnter.Trigger(getMousePosition())
            else 
                mouse.Leave(getPixelPostion())
                mouseLeave.Trigger(getMousePosition())
        ))    


    let gamepads, updateGamepads =
        let mutable state : GamepadState = GamepadState()
        let connected = 
            let init (id : int, _) =
                let g = GlfwGamepad()
                g.Update(glfw, id)
                g

            let update (g : GlfwGamepad) (id : int, _) =
                g.Update(glfw, id)
                g

            FSharp.Data.Traceable.ChangeableModelMap<string, int * _, GlfwGamepad, IGamepad>(
                HashMap.empty,
                init, update,
                (fun g -> g :> IGamepad)
            )
        
        let mutable state = HashMap.empty
        let mutable guids = HashMap.empty
        
        let mutable unique = 0
        let update() =
            transact (fun () ->
                let uid = Interlocked.Increment &unique
                state |> HashMap.map (fun _ v -> v, uid) |> connected.Update
            )

        let callback =
            glfw.SetJoystickCallback(GlfwCallbacks.JoystickCallback(fun id c ->
                match c with
                | ConnectedState.Connected ->
                    let guid = glfw.GetJoystickGUID(id)
                    state <- HashMap.add guid id state
                    guids <- HashMap.add id guid guids
                    update()
                | _ ->
                    match HashMap.tryRemove id guids with
                    | Some (guid, rest) ->
                        guids <- rest
                        state <- HashMap.remove guid state
                        update()
                    | None ->
                        ()
            ))


        for i in 0 .. 15 do
            if glfw.JoystickPresent(i) then
                let guid = glfw.GetJoystickGUID i
                state <- HashMap.add guid i state
                guids <- HashMap.add i guid guids

        update()


        connected :> amap<_,_>, update


    let time =
        let start = System.DateTime.Now
        let sw = System.Diagnostics.Stopwatch.StartNew()
        AVal.custom (fun _ ->
            start + sw.Elapsed
        )

    let mutable isDisposed = false
    let mutable beforeFullScreen = Box2i.FromMinAndSize(V2i.Zero, V2i(1024, 768))
    let mutable renderContinuous = false

    let mutable renderTask : IRenderTask = RenderTask.empty
    let mutable renderTaskSub = 
        renderTask.AddMarkingCallback (fun () ->
            requiresRedraw <- true
            glfw.PostEmptyEvent()
        )

    let mutable frameCount = 0
    let mutable totalTime = MicroTime.Zero
    let mutable totalGpuTime = MicroTime.Zero
    let mutable averageFrameTime = MicroTime.Zero
    let mutable averageGpuTime = MicroTime.Zero
    let mutable measureGpuTime = true
    let mutable formattedTime = None

    let sw = System.Diagnostics.Stopwatch()

    let mutable gpuQuery : Option<ITimeQuery> = None

    let mutable glfwCursor = NativePtr.zero<Silk.NET.GLFW.Cursor>
    let mutable cursor = Cursor.Default

    // let device = app.Runtime.Device
    // let graphicsMode = GraphicsMode(Col.Format.BGRA, 8, 24, 8, 2, 1, ImageTrafo.MirrorY, vsync)
    // let mutable description = device.CreateSwapchainDescription(surface, graphicsMode)

    let mutable swapchain : option<ISwapchain> = None

    let refreshTitle() =
        let title =
            match formattedTime with
            | Some t when showFrameTime -> sprintf "%s %s" title t
            | _ -> title

        glfw.SetWindowTitle(win, title)

    let updateTime (total : MicroTime) (gpu : MicroTime) =
        frameCount <- frameCount + 1
        totalTime <- totalTime + total
        totalGpuTime <- totalGpuTime + gpu
        if frameCount > 50 then
            averageFrameTime <- totalTime / frameCount
            averageGpuTime <- totalGpuTime / frameCount

            if measureGpuTime then
                let r = 100.0 * (averageGpuTime / averageFrameTime)
                formattedTime <- Some <| sprintf "(%A/%.1f%%)" averageFrameTime r
            else
                formattedTime <- Some <| sprintf "(%A)" averageFrameTime

            frameCount <- 0
            totalTime <- MicroTime.Zero
            totalGpuTime <- MicroTime.Zero

            refreshTitle()

    member x.Surface = surface

    member x.Cursor
        with get() = cursor
        and set c =
            x.Invoke (fun () ->
                if c = Cursor.None then
                    if cursor <> Cursor.None then
                        glfw.SetInputMode(win, CursorStateAttribute.Cursor, CursorModeValue.CursorHidden)
                        if glfwCursor <> NativePtr.zero then glfw.DestroyCursor glfwCursor
                        glfwCursor <- NativePtr.zero
                        cursor <- c
                else
                    if cursor = Cursor.None then glfw.SetInputMode(win, CursorStateAttribute.Cursor, CursorModeValue.CursorNormal)
                    let handle = 
                        match c with
                        | Cursor.None -> NativePtr.zero // unreachable
                        | Cursor.Default -> NativePtr.zero
                        | Cursor.Arrow -> glfw.CreateStandardCursor(CursorShape.Arrow)
                        | Cursor.Hand -> glfw.CreateStandardCursor(CursorShape.Hand)
                        | Cursor.HorizontalResize -> glfw.CreateStandardCursor(CursorShape.HResize)
                        | Cursor.VerticalResize -> glfw.CreateStandardCursor(CursorShape.VResize)
                        | Cursor.Text -> glfw.CreateStandardCursor(CursorShape.IBeam)
                        | Cursor.Crosshair -> glfw.CreateStandardCursor(CursorShape.Crosshair)
                        | Cursor.Custom(img, hot) ->
                            let img = img.ToPixImage<byte>(Col.Format.RGBA)
                            NativeVolume.using img.Volume (fun pSrc ->
                                use dst = fixed (Array.zeroCreate<byte> (img.Size.X * img.Size.Y * 4))
                                let pDst = NativeVolume<byte>(dst, VolumeInfo(0L, V3l(img.Size, 4), V3l(4, img.Size.X * 4, 1)))
                                NativeVolume.copy pSrc pDst

                                use pImg = 
                                    fixed [| 
                                        Silk.NET.GLFW.Image(
                                            Width = img.Size.X,
                                            Height = img.Size.Y,
                                            Pixels = dst
                                        )
                                    |]

                                glfw.CreateCursor(pImg, hot.X, hot.Y)
                            )

                    glfw.SetCursor(win, handle)
                    if glfwCursor <> NativePtr.zero then glfw.DestroyCursor glfwCursor
                    glfwCursor <- handle
                    cursor <- c
            )


    member x.AfterRender = afterRender.Publish
    member x.BeforeRender = beforeRender.Publish
    member x.FramebufferSignature  = surface.Signature
    member x.RenderTask
        with get () = renderTask
        and set (v: IRenderTask) = 
            x.Invoke(fun () ->
                renderTaskSub.Dispose() 
                renderTask.Dispose()
                renderTask <- v
                renderTaskSub <- 
                    v.AddMarkingCallback (fun () ->
                        requiresRedraw <- true
                        glfw.PostEmptyEvent()
                    )
                requiresRedraw <- true
            )
            glfw.PostEmptyEvent()

    member x.Runtime = app.Runtime
    member x.Samples = samples
    member x.Sizes = currentSize :> aval<_>
    member x.Time = time
    member x.Keyboard = keyboard :> Aardvark.Application.IKeyboard
    member x.Mouse = mouse :> Aardvark.Application.IMouse
    member x.Gamepads = gamepads :> amap<_,_>

    member x.SubSampling
        with get() = 1.0
        and set v = if v <> 1.0 then failwithf "[GLFW] SubSampling not implemented"

    interface Aardvark.Application.IRenderTarget with
        member x.AfterRender = x.AfterRender
        member x.BeforeRender = x.BeforeRender
        member x.FramebufferSignature = x.FramebufferSignature
        member x.RenderTask
            with get () = x.RenderTask
            and set (v: IRenderTask) = x.RenderTask <- v
        member x.Runtime = x.Runtime
        member x.Samples = x.Samples
        member x.Sizes = x.Sizes
        member x.Time = x.Time

        member x.SubSampling
            with get() = x.SubSampling
            and set v = x.SubSampling <- v

    interface Aardvark.Application.IRenderControl with
        member x.Cursor
            with get() = x.Cursor
            and set c = x.Cursor <- c
        member x.Keyboard = x.Keyboard
        member x.Mouse = x.Mouse
       
    interface Aardvark.Application.IRenderWindow with
        member x.Run() = x.Run()
       
    member x.Dispose() =
        if not isDisposed then
            isDisposed <- true
            app.RemoveExistingWindow x
            app.Post (fun () ->
                swapchain |> Option.iter Disposable.dispose
                swapchain <- None

                surface.Dispose()

                renderTask.Dispose()
                renderTask <- RenderTask.empty

                gpuQuery |> Option.iter Disposable.dispose
                gpuQuery <- None

                glfw.HideWindow(win)
                glfw.DestroyWindow(win)
            )

    interface System.IDisposable with
        member x.Dispose() = x.Dispose()


    [<CLIEvent>]
    member x.WindowStateChanged = stateChanged.Publish
    [<CLIEvent>]
    member x.Closing = closeEvt.Publish
    [<CLIEvent>]
    member x.Resize = resize.Publish
    [<CLIEvent>]
    member x.FocusChanged = focus.Publish
    [<CLIEvent>]
    member x.WindowPositionChanged = wpChanged.Publish
    [<CLIEvent>]
    member x.ContentPositionChanged = cpChanged.Publish
    [<CLIEvent>]
    member x.DropFiles = dropFiles.Publish
    [<CLIEvent>]
    member x.KeyDown = keyDown.Publish
    [<CLIEvent>]
    member x.KeyUp = keyUp.Publish
    [<CLIEvent>]
    member x.KeyInput = keyInput.Publish

    [<CLIEvent>]
    member x.MouseDown = mouseDown.Publish
    [<CLIEvent>]
    member x.MouseUp = mouseUp.Publish
    [<CLIEvent>]
    member x.MouseMove = mouseMove.Publish
    [<CLIEvent>]
    member x.MouseWheel = mouseWheel.Publish
    [<CLIEvent>]
    member x.MouseEnter = mouseEnter.Publish
    [<CLIEvent>]
    member x.MouseLeave = mouseLeave.Publish


    member x.WindowState
        with get() = 
            x.Invoke(fun () -> getWindowState())
        and set (s : WindowState) =
            x.Invoke(fun () ->
                match s with
                | WindowState.Maximized -> glfw.MaximizeWindow(win)
                | WindowState.Minimized -> glfw.IconifyWindow(win)
                | WindowState.Normal -> glfw.RestoreWindow(win)
                | _ -> ()
            )   

    member x.Decorated
        with get() = x.Invoke(fun () -> glfw.GetWindowAttrib(win, WindowAttributeGetter.Decorated))
        and set b = x.Invoke(fun () -> glfw.SetWindowAttrib(win, WindowAttributeSetter.Decorated, b))         

    member x.Fullcreen
        with get() = 
            x.Invoke(fun () -> 
                glfw.GetWindowMonitor(win) <> NativePtr.zero
            )
        and set f =
            x.Invoke(fun () ->
                if f then 
                    let mutable os = V2i.Zero
                    let mutable oo = V2i.Zero
                    glfw.GetWindowSize(win, &os.X, &os.Y)
                    glfw.GetWindowPos(win, &oo.X, &oo.Y)
                    beforeFullScreen <- Box2i.FromMinAndSize(oo, os)

                    let m = glfw.GetPrimaryMonitor()
                    let mode = glfw.GetVideoMode(m) |> NativePtr.read

                    //let ws = V2i(round (float mode.Width / float scale.X), round(float mode.Height / float scale.Y))  

                    glfw.SetWindowMonitor(win, m, 0, 0, mode.Width, mode.Height, mode.RefreshRate)
                else
                    let o = beforeFullScreen.Min
                    let s = beforeFullScreen.Size
                    glfw.SetWindowMonitor(win, NativePtr.zero, o.X, o.Y, s.X, s.Y, 0)
                requiresRedraw <- true
                glfw.PostEmptyEvent()                            
            )        

    member x.Floating
        with get() = x.Invoke(fun () -> glfw.GetWindowAttrib(win, WindowAttributeGetter.Floating))
        and set b = x.Invoke(fun () -> glfw.SetWindowAttrib(win, WindowAttributeSetter.Floating, b))         

    member x.Resizable
        with get() = x.Invoke(fun () -> glfw.GetWindowAttrib(win, WindowAttributeGetter.Resizable))
        and set b = x.Invoke(fun () -> glfw.SetWindowAttrib(win, WindowAttributeSetter.Resizable, b))         

    member x.Transparent
        with get() = x.Invoke(fun () -> glfw.GetWindowAttrib(win, WindowAttributeGetter.TransparentFramebuffer))

    member x.MousePosition =
        x.Invoke(fun () ->
            let v = 
                glfw.GetWindowAttrib(win, WindowAttributeGetter.Visible) &&
                not (glfw.GetWindowAttrib(win, WindowAttributeGetter.Iconified))
            if v then
                let mutable pos = V2d.Zero
                glfw.GetCursorPos(win, &pos.X, &pos.Y)
                lastMousePosition <- pos * contentScale()
            
            lastMousePosition         
        )

    member x.GetKeyName(key : Keys, code : int) =
        match keyNameCache.TryGetValue((key, code)) with
        | (true, name) -> name
        | _ -> x.Invoke(fun () -> getKeyName key code)

    member x.GetKeyName(key : Keys) = x.GetKeyName(key, -1)
        

    member private x.RunEvents(evts : seq<WindowEvent>) =
        for e in evts do
            match e with
            | Run action -> action()
            | Resize -> resize.Trigger(getResizeEvent())

    member x.Invoke<'r>(action : unit -> 'r) : 'r =
        app.Invoke(action)


    member x.IsVisible 
        with get() =
            x.Invoke(fun () -> glfw.GetWindowAttrib(win, WindowAttributeGetter.Visible))         
        and set v =
            x.Invoke (fun () ->
                if v then 
                    app.AddVisibleWindow x
                    glfw.ShowWindow(win)
                else
                    app.RemoveVisibleWindow x
                    glfw.HideWindow(win)
            )

    member x.Title 
        with get() = title
        and set t =
            x.Invoke(fun () ->
                if title <> t then
                    title <- t
                    refreshTitle()
            ) 

    member x.Focus() = 
        x.Invoke(fun () ->
            let c = glfw.GetWindowAttrib(win, WindowAttributeGetter.Focused)
            if not c then glfw.FocusWindow(win)
        )       

    member x.Focused = x.Invoke(fun () -> glfw.GetWindowAttrib(win, WindowAttributeGetter.Focused))             

    member x.WindowSize
        with get() =
            x.Invoke(fun () ->
                let mutable ps = V2i.Zero
                let border = getFrameBorder()
                let scale = contentScale()

                glfw.GetWindowSize(win, &ps.X, &ps.Y) 
                let ws = ps + border.Min + border.Max
                V2i(round (scale.X * float ws.X), round(scale.Y * float ws.Y))  
            )
        and set (v : V2i) = 
            x.Invoke(fun () ->
                let scale = contentScale()
                let border = getFrameBorder()

                let ws = V2i(round (float v.X / float scale.X), round(float v.Y / float scale.Y))  
                let ps = V2i.Max(V2i.II, ws - (border.Min + border.Max))

                glfw.SetWindowSize(win, ps.X, ps.Y)
            )

    member x.FramebufferSize
        with get() = 
            x.Invoke(fun () ->
                framebufferSize()
            )
        and set (v : V2i) =
            x.Invoke(fun () ->
                let scale = contentScale()
                let ws = V2i(round (float v.X / float scale.X), round(float v.Y / float scale.Y)) 
                glfw.SetWindowSize(win, ws.X, ws.Y)
            )        

    member x.Icon 
        with get() = icon
        and set (v : option<PixImageMipMap>) =
            x.Invoke(fun () ->
                let inline toMatrix(img : PixImage<byte>) =
                    let dst = Matrix<uint32>(img.Size)
                    dst.SetMap(img.GetMatrix<C4b>(), fun c ->
                        (uint32 c.A <<< 24) ||| (uint32 c.B <<< 16) ||| (uint32 c.G <<< 8) ||| (uint32 c.R)
                    ) |> ignore
                    dst

                let rec mipMaps (acc : System.Collections.Generic.List<Silk.NET.GLFW.Image>) (i : int) (img : PixImage[]) =
                    if i >= img.Length then
                        let arr = acc.ToArray()
                        use img = fixed arr
                        glfw.SetWindowIcon(win, acc.Count, img)
                    else
                        let mat = toMatrix (img.[i].ToPixImage<byte>())
                        use pdata = fixed mat.Data
                        acc.Add(Silk.NET.GLFW.Image(Width = int mat.Size.X, Height = int mat.Size.Y, Pixels = NativePtr.cast pdata))

                        mipMaps acc (i+1) img                       

                match v with
                | Some img ->
                    let l = System.Collections.Generic.List()
                    mipMaps l 0 img.ImageArray
                | None ->
                    glfw.SetWindowIcon(win, 0, NativePtr.zero)  

                                  
                icon <- v                            
            )

    member x.WindowPosition
        with get() = 
            x.Invoke(fun () ->
                let mutable pos = V2i.Zero
                glfw.GetWindowPos(win, &pos.X, &pos.Y)
                let b = getFrameBorder()
                pos - b.Min
            )
        and set (pos : V2i) =   
            x.Invoke(fun () ->
                let b = getFrameBorder()
                let pp = b.Min + pos
                glfw.SetWindowPos(win, pp.X, pp.Y)
            ) 

    member x.ContentPosition
        with get() = 
            x.Invoke(fun () ->
                let mutable pos = V2i.Zero
                glfw.GetWindowPos(win, &pos.X, &pos.Y)
                pos
            )
        and set (pos : V2i) =   
            x.Invoke(fun () ->
                glfw.SetWindowPos(win, pos.X, pos.Y)
            )         

    member x.Close() =
        x.Invoke(fun () ->
            glfw.HideWindow(win)
            app.RemoveExistingWindow x
        )

    member x.VSync
        with get() = enableVSync
        and set v = enableVSync <- v
       
    member x.ShowFrameTime 
        with get() = showFrameTime
        and set v =
            if v <> showFrameTime then
                showFrameTime <- v
                x.Invoke refreshTitle

    member x.AverageFrameTime = averageFrameTime
    member x.AverageGPUFrameTime = averageGpuTime

    member x.MeasureGpuTime
        with get() = measureGpuTime
        and set v = measureGpuTime <- v

    member x.RenderAsFastAsPossible
        with get() = renderContinuous
        and set v =
            if renderContinuous <> v then
                renderContinuous <- v
                if v then glfw.PostEmptyEvent()
                else ()

    member x.Redraw() : bool =
        let sfbo = framebufferSize()
        setCurrentSize sfbo

        if (renderContinuous || requiresRedraw) && Vec.allGreater sfbo 0 then
            try
                sw.Restart()

                requiresRedraw <- false
                if vsync <> enableVSync then
                    vsync <- enableVSync
                    if enableVSync then glfw.SwapInterval(1)
                    else glfw.SwapInterval(0)

                let queries =
                    if measureGpuTime then
                        let query =
                            gpuQuery |> Option.defaultWith app.Runtime.CreateTimeQuery

                        gpuQuery <- Some query
                        query :> IQuery
                    else
                        Queries.none

                beforeRender.Trigger()

                let success =
                    match swapchain with
                    | Some ch when ch.Size = sfbo ->
                        ch.Run(renderTask, queries)

                    | _ ->
                        match swapchain with
                        | Some o -> o.Dispose()
                        | None -> ()

                        let swap = surface.CreateSwapchain(sfbo)
                        swapchain <- Some swap
                        swap.Run(renderTask, queries)

                if not success then
                    requiresRedraw <- true

                renderContinuous || renderTask.OutOfDate || requiresRedraw
            finally 
                afterRender.Trigger()  
                transact time.MarkOutdated  

                let gpuTime =
                    if measureGpuTime then
                        gpuQuery
                        |> Option.bind (fun q -> q.TryGetResult true)
                        |> Option.defaultValue MicroTime.Zero
                    else
                        MicroTime.Zero

                sw.Stop()
                updateTime sw.MicroTime gpuTime
        else
            renderContinuous || renderTask.OutOfDate

    member x.Update() =
        updateGamepads()

    member x.Run() =
        app.Run x          

