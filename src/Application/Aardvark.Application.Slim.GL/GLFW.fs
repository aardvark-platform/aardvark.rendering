namespace Glfw

open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open System.Threading
open Silk.NET.GLFW
open OpenTK.Graphics.OpenGL4
open System.Runtime.InteropServices
open FSharp.Control
open FSharp.Data.Adaptive

#nowarn "9"

module private Translations =

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
            let m = glfw.Library.LoadFunction "glfwGetWindowContentScale"
            Marshal.GetDelegateForFunctionPointer(m, typeof<GetWindowContentScaleDel>) |> unbox<GetWindowContentScaleDel>
        )
    let private getKeyName (glfw : Glfw) =
        keyNameDict.GetOrAdd(glfw, fun glfw ->
            let m = glfw.Library.LoadFunction "glfwGetKeyName"
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

module internal OpenTKContext = 
    open System.Runtime.InteropServices
    open System.Reflection
    open OpenTK
    open OpenTK.Graphics
    open OpenTK.Platform
    open OpenTK.Graphics.OpenGL4

    type MyWindowInfo(win : nativeptr<WindowHandle>) =
        let mutable win = win
        interface IWindowInfo with
            member x.Dispose(): unit = 
                win <- NativePtr.zero

            member x.Handle: nativeint = 
                NativePtr.toNativeInt win
    
    [<AllowNullLiteral>]
    type MyGraphicsContext(glfw : Glfw, win : nativeptr<WindowHandle>) as this =
        [<System.ThreadStaticAttribute; DefaultValue>]
        static val mutable private CurrentContext : OpenTK.ContextHandle

        let mutable win = win

        static let addContext = typeof<GraphicsContext>.GetMethod("AddContext", BindingFlags.NonPublic ||| BindingFlags.Static)
        static let remContext = typeof<GraphicsContext>.GetMethod("RemoveContext", BindingFlags.NonPublic ||| BindingFlags.Static)


        static do 
            let get = GraphicsContext.GetCurrentContextDelegate(fun () -> MyGraphicsContext.CurrentContext)
            let t = typeof<GraphicsContext>
            let f = t.GetField("GetCurrentContext", BindingFlags.NonPublic ||| BindingFlags.Static)
            f.SetValue(null, get)

        do addContext.Invoke(null, [| this :> obj |]) |> ignore

        member x.LoadAll(): unit = 
            let t = typeof<GL>
            let m = t.GetMethod("LoadEntryPoints", BindingFlags.NonPublic ||| BindingFlags.Instance)
            let gl = GL()
            m.Invoke(gl, null) |> ignore
        
        interface IGraphicsContext with
            member x.Dispose(): unit = 
                remContext.Invoke(null, [| x :> obj |]) |> ignore
                win <- NativePtr.zero
                ()

            member x.ErrorChecking
                with get () = false
                and set _ = ()

            member x.GraphicsMode = 
                GraphicsMode.Default

            member x.IsCurrent =
                glfw.GetCurrentContext() = win
            member x.IsDisposed: bool = 
                win = NativePtr.zero
            member x.LoadAll() = x.LoadAll()
            member x.MakeCurrent(window: IWindowInfo): unit = 
                if isNull window then 
                    glfw.MakeContextCurrent(NativePtr.zero)
                    MyGraphicsContext.CurrentContext <- ContextHandle.Zero
                else 
                    MyGraphicsContext.CurrentContext <- ContextHandle(NativePtr.toNativeInt win)
                    glfw.MakeContextCurrent(win)

            member x.SwapBuffers(): unit = 
                glfw.SwapBuffers(win)
            member x.SwapInterval
                with get() = 0
                and set v = ()
            member x.Update(window: IWindowInfo): unit = 
                ()

        interface IGraphicsContextInternal with
            member x.Context: ContextHandle = 
                ContextHandle(NativePtr.toNativeInt win)
            member x.GetAddress(name : string): nativeint = 
                glfw.GetProcAddress name
            member x.GetAddress(name: nativeint): nativeint = 
                let str = Marshal.PtrToStringAnsi name
                glfw.GetProcAddress str
            member x.Implementation: IGraphicsContext = 
                x :> _
            member x.LoadAll() = x.LoadAll()
        

type internal WindowEvent =
    | Resize
    | Run of action : (unit -> unit)


type KeyEvent internal(key : Aardvark.Application.Keys, scanCode : int, action : InputAction, modifiers : KeyModifiers, keyName : string) =
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
                if x.Alt then "alt"
                if x.Shift then "shift"
                if x.Ctrl then "ctrl"
                if x.Super then "super"          
            ]

        sprintf "%s { key: %A; scan: %A; mod: [%s]; name: %s }" kind key scanCode (String.concat "; " modifiers) keyName

type ResizeEvent(framebufferSize : V2i, physicalSize : V2i, windowSize : V2i) =
    member x.FramebufferSize = framebufferSize
    member x.PhysicalSize = physicalSize
    member x.WindowSize = windowSize

    override x.ToString() = 
        sprintf "Resize { framebuffer: %A; physical: %A; window: %A }" framebufferSize physicalSize windowSize

type MouseEvent internal(button : Aardvark.Application.MouseButtons, position: V2d, action : InputAction, modifiers : KeyModifiers) =
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
                if x.Alt then "alt"
                if x.Shift then "shift"
                if x.Ctrl then "ctrl"
                if x.Super then "super"          
            ]

        sprintf "%s { button: %A; mod: [%s]; position: %A }" kind button (String.concat "; " modifiers) position

type WindowState =
    | Normal = 0
    | Minimized = 1
    | Maximized = 2
    | Invisible = 3


open System.Threading.Tasks


type WindowConfig =
    {
        title : string
        width : int
        height : int
        focus : bool
        resizable : bool
        refreshRate : int
        transparent : bool
        opengl : option<int*int>
        physicalSize : bool
        samples : int
    }    

[<Sealed>]
type Application(runtime : IRuntime) =
    [<System.ThreadStatic; DefaultValue>]
    static val mutable private IsMainThread_ : bool

    let mutable ctx = Unchecked.defaultof<Aardvark.Rendering.GL.Context>

    let glfw = Glfw.GetApi()
    do if not (glfw.Init()) then  failwith "GLFW init failed"

    let lockObj = obj()
    let mutable lastWindow = None
    let queue = System.Collections.Concurrent.ConcurrentQueue<unit -> unit>()

    let existingWindows = System.Collections.Concurrent.ConcurrentHashSet<Window>()
    let visibleWindows = System.Collections.Concurrent.ConcurrentHashSet<Window>()
    do Application.IsMainThread_ <- true

    //let thread =
    //    let mutable inited = None
    //    let thread = 
    //        startThread (fun () ->
    //            Application.IsMainThread_ <- true
    //            if not (glfw.Init()) then 
    //                lock lockObj (fun () ->
    //                    inited <- Some false
    //                    Monitor.PulseAll lockObj
    //                )
    //            else                
    //                lock lockObj (fun () -> 
    //                    inited <- Some true
    //                    Monitor.PulseAll lockObj
    //                )
    //                let mutable outDated = false
    //                while true do
    //                    glfw.WaitEvents()

    //                    let mutable action = Unchecked.defaultof<unit -> unit>
    //                    while queue.TryDequeue(&action) do
    //                        try action()
    //                        with _ -> ()

    //                    outDated <- false
    //                    for w in visibleWindows do
    //                        let v = w.Redraw()
    //                        outDated <- outDated || v
    //        )

        //lock lockObj (fun () -> 
        //    while Option.isNone inited do
        //        Monitor.Wait lockObj |> ignore
        //)

        //if not inited.Value then failwith "GLFW init failed"
        //thread


    member x.Runtime = runtime
    member x.Context 
        with get() = ctx
        and internal set c = ctx <- c

    member internal x.AddExistingWindow(w : Window) =
        existingWindows.Add w |> ignore
        glfw.PostEmptyEvent()

    member internal x.RemoveExistingWindow(w : Window) =
        existingWindows.Remove w |> ignore
        visibleWindows.Remove w |> ignore
        glfw.PostEmptyEvent()

    member internal x.AddVisibleWindow(w : Window) =
        visibleWindows.Add w |> ignore
        glfw.PostEmptyEvent()

    member internal x.RemoveVisibleWindow(w : Window) =
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
            let mutable glContext = false
            let mutable parent : nativeptr<WindowHandle> = NativePtr.zero
            glfw.DefaultWindowHints()

            match cfg.opengl with
            | Some (major, minor) ->
                glContext <- true
                glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.OpenGL)
                glfw.WindowHint(WindowHintInt.ContextVersionMajor, major)
                glfw.WindowHint(WindowHintInt.ContextVersionMinor, minor)
                glfw.WindowHint(WindowHintInt.DepthBits, 24)
                glfw.WindowHint(WindowHintInt.StencilBits, 8)


                let m = glfw.GetPrimaryMonitor()
                let mode = glfw.GetVideoMode(m) |> NativePtr.read
                glfw.WindowHint(WindowHintInt.RedBits, mode.RedBits)
                glfw.WindowHint(WindowHintInt.GreenBits, mode.GreenBits)
                glfw.WindowHint(WindowHintInt.BlueBits, mode.BlueBits)
                glfw.WindowHint(WindowHintInt.AlphaBits, if cfg.transparent then 8 else 0)
                glfw.WindowHint(WindowHintInt.RefreshRate, mode.RefreshRate)
                glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core)
                glfw.WindowHint(WindowHintRobustness.ContextRobustness, Robustness.LoseContextOnReset)
                glfw.WindowHint(WindowHintBool.OpenGLForwardCompat, true)
                glfw.WindowHint(WindowHintBool.DoubleBuffer, false)
                glfw.WindowHint(WindowHintBool.OpenGLDebugContext, false)
                glfw.WindowHint(WindowHintBool.ContextNoError, true)
                glfw.WindowHint(WindowHintBool.SrgbCapable, true)
                if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
                    glfw.WindowHint(unbox<WindowHintBool> 0x00023001, cfg.physicalSize)

                glfw.WindowHint(unbox<WindowHintBool> 0x0002200C, false)
                glfw.WindowHint(WindowHintInt.Samples, cfg.samples)
                match lastWindow with
                | Some l -> parent <- l
                | None -> ()
            | None ->
                glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi)

            glfw.WindowHint(WindowHintBool.TransparentFramebuffer, cfg.transparent)
            glfw.WindowHint(WindowHintBool.Visible, false)
            glfw.WindowHint(WindowHintBool.Resizable, cfg.resizable)
            glfw.WindowHint(WindowHintInt.RefreshRate, cfg.refreshRate)
            glfw.WindowHint(WindowHintBool.FocusOnShow, cfg.focus)


            let win = glfw.CreateWindow(cfg.width, cfg.height, cfg.title, NativePtr.zero, parent)
            if win = NativePtr.zero then failwith "GLFW could not create window"

            if glContext then lastWindow <- Some win

            let ctx =
                if glContext then new OpenTKContext.MyGraphicsContext(glfw, win) :> OpenTK.Graphics.IGraphicsContext        
                else null

            let info =
                new OpenTKContext.MyWindowInfo(win)

            if not (isNull ctx) then  
                ctx.MakeCurrent info
                ctx.LoadAll()          

            glfw.MakeContextCurrent(NativePtr.zero)
            new Window(x, win, cfg.title, ctx, info, cfg.samples)
        )        

    member x.Run([<System.ParamArray>] ws : Window[]) =    
        let mutable outDated = false

        for w in ws do w.IsVisible <- true

        while existingWindows.Count > 0 do
            glfw.WaitEvents()

            let mutable action = Unchecked.defaultof<unit -> unit>
            while queue.TryDequeue(&action) do
                try action()
                with _ -> ()

            outDated <- false
            for w in visibleWindows do
                let v = w.Redraw()
                outDated <- outDated || v


and Window internal(app : Application, win : nativeptr<WindowHandle>, title : string, ctx : OpenTK.Graphics.IGraphicsContext, info : OpenTK.Platform.IWindowInfo, samples : int) as this =
    static let keyNameCache = System.Collections.Concurrent.ConcurrentDictionary<Keys * int, string>()

    let glfw = app.Glfw

    let mutable windowScale = V2d.II
    let mutable damaged = true
    let mutable title = title
    let mutable icon : option<PixImageMipMap> = None
    let mutable lastMousePosition = V2d.Zero

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

    let contentSize() =
        let mutable ps = V2i.Zero
        glfw.GetWindowSize(win, &ps.X, &ps.Y)
        V2i(round (float windowScale.X * float ps.X), round(float windowScale.Y * float ps.Y))

    let getResizeEvent() =
        let mutable fbo = V2i.Zero
        let mutable ps = V2i.Zero
        let mutable scale = V2f.Zero
        let border = getFrameBorder()

        glfw.GetFramebufferSize(win, &fbo.X, &fbo.Y)
        glfw.GetWindowSize(win, &ps.X, &ps.Y)
        glfw.GetWindowContentScale(win, &scale.X, &scale.Y)   
        windowScale <- V2d scale
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
            lastMousePosition <- pos * windowScale

        lastMousePosition

    let getPixelPostion() =
        let pos = getMousePosition()
        PixelPosition(V2i(pos.Round()), Box2i(V2i.Zero, contentSize()))

    let keyboard = Aardvark.Application.EventKeyboard()
    let mouse = Aardvark.Application.EventMouse(true)

    let signature =
        Aardvark.Rendering.GL.FramebufferSignature(
            app.Runtime,
            Map.ofList [0, (DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = samples })],
            Map.empty,
            Some { format = RenderbufferFormat.Depth24Stencil8; samples = samples },
            Some { format = RenderbufferFormat.Depth24Stencil8; samples = samples },
            1,
            Set.empty
        )
        
    let currentSize =
        let mutable s = V2i.Zero
        glfw.GetFramebufferSize(win, &s.X, &s.Y)
        cval s 

    let resizeCb =
        glfw.SetWindowSizeCallback(win, GlfwCallbacks.WindowSizeCallback(fun _ w h ->
            let evt = getResizeEvent()
            transact (fun () -> currentSize.Value <- evt.FramebufferSize)
            resize.Trigger evt     
            damaged <- true
            this.Redraw() |> ignore
        ))

    let maxCb =
        glfw.SetWindowMaximizeCallback(win, GlfwCallbacks.WindowMaximizeCallback(fun w b ->
            let windowState = getWindowState()
            stateChanged.Trigger windowState
        ))

    let minCb =
        glfw.SetWindowIconifyCallback(win, GlfwCallbacks.WindowIconifyCallback(fun w b ->
            let windowState = getWindowState()
            stateChanged.Trigger windowState
        ))

    let closingCallback = 
        glfw.SetWindowCloseCallback(win, GlfwCallbacks.WindowCloseCallback(fun w ->
            closeEvt.Trigger()
            glfw.HideWindow(win)
            app.RemoveExistingWindow this
        ))

    let focusCallback =
        glfw.SetWindowFocusCallback(win, GlfwCallbacks.WindowFocusCallback(fun w f ->
            focus.Trigger(f)
        ))

    let posCb =
        glfw.SetWindowPosCallback(win, GlfwCallbacks.WindowPosCallback(fun w x y ->
            let border = getFrameBorder()
            wpChanged.Trigger(V2i(x,y) - border.Min)
            cpChanged.Trigger(V2i(x,y))
        ))

    let keyCallback =
        glfw.SetKeyCallback(win, GlfwCallbacks.KeyCallback(fun w k c a m ->
            let name = getKeyName k c
            match Translations.tryGetKey k c name with
            | ValueSome k -> 
                match a with
                | InputAction.Press -> 
                    Log.warn "%A" k
                    keyboard.KeyDown(k)
                    keyDown.Trigger(KeyEvent(k, c, a, m, name))
                | InputAction.Repeat -> 
                    //keyboard.KeyDown(k)
                    //keyDown.Trigger(KeyEvent(k,c, a, m, name))
                    ()
                | InputAction.Release -> 
                    keyboard.KeyUp(k)
                    keyUp.Trigger(KeyEvent(k, c, a, m, name))
                | _ -> ()
            | ValueNone ->
                ()       
        ))

    let inputCallback =
        glfw.SetCharCallback(win, GlfwCallbacks.CharCallback (fun w c ->
            let str = System.Text.Encoding.UTF32.GetString(System.BitConverter.GetBytes(c))
            keyInput.Trigger(str)
        ))


    let moveCallback =
        glfw.SetCursorPosCallback(win, GlfwCallbacks.CursorPosCallback(fun w a b ->
            let v = 
                glfw.GetWindowAttrib(w, WindowAttributeGetter.Visible) &&
                not (glfw.GetWindowAttrib(w, WindowAttributeGetter.Iconified))
            if v then
                let p = windowScale * V2d(a,b)
                
                let pp = PixelPosition(V2i(p.Round()), Box2i(V2i.Zero, contentSize()))
                mouse.Move(pp)
                lastMousePosition <- p
                mouseMove.Trigger(p)
        ))

    let mouseCallback =
        glfw.SetMouseButtonCallback(win, GlfwCallbacks.MouseButtonCallback(fun w button action modifiers ->
            let mutable pos = V2d.Zero
            glfw.GetCursorPos(win, &pos.X, &pos.Y)
            let pos = windowScale * pos
            let evt = MouseEvent(Translations.toMouseButton button, pos, action, modifiers)
            match action with
            | InputAction.Press -> 
                let pp = PixelPosition(V2i(pos.Round()), Box2i(V2i.Zero, contentSize()))
                mouse.Down(pp, evt.Button)
                mouseDown.Trigger evt    
            | InputAction.Release -> 
                let pp = PixelPosition(V2i(pos.Round()), Box2i(V2i.Zero, contentSize()))
                mouse.Up(pp, evt.Button)
                mouseUp.Trigger evt
            | _ -> ()       
        ))    

    let wheelCallback =
        glfw.SetScrollCallback(win, GlfwCallbacks.ScrollCallback(fun w dx dy ->
            let mutable pos = V2d.Zero
            glfw.GetCursorPos(win, &pos.X, &pos.Y)
            let pp = getPixelPostion()
            mouse.Scroll(pp, dy * 120.0)
            mouseWheel.Trigger(V2d(dx, dy) * 120.0)
        ))    

    let damagedCallback =
        glfw.SetWindowRefreshCallback(win, GlfwCallbacks.WindowRefreshCallback(fun w ->
            damaged <- true
        ))

    let dropCallback =
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

    let enterLeave =
        glfw.SetCursorEnterCallback(win, GlfwCallbacks.CursorEnterCallback(fun _ entered ->
            if entered then
                mouse.Enter(getPixelPostion())
                mouseEnter.Trigger(getMousePosition())
            else 
                mouse.Leave(getPixelPostion())
                mouseLeave.Trigger(getMousePosition())
        ))    

    let time =
        let start = System.DateTime.Now
        let sw = System.Diagnostics.Stopwatch.StartNew()
        AVal.custom (fun _ ->
            start + sw.Elapsed
        )

    let mutable beforeFullScreen = Box2i.FromMinAndSize(V2i.Zero, V2i(1024, 768))

    let mutable renderTask : IRenderTask = RenderTask.empty
    let mutable renderTaskSub = 
        renderTask.AddMarkingCallback (fun () ->
            damaged <- true
            glfw.PostEmptyEvent()
        )

    let ctxHandle = Aardvark.Rendering.GL.ContextHandle(ctx, info)

    let defaultFramebuffer = 
        new Aardvark.Rendering.GL.Framebuffer(
            app.Context, signature, 
            (fun _ -> 0), 
            ignore, 
            [0, DefaultSemantic.Colors, Aardvark.Rendering.GL.Renderbuffer(app.Context, 0, V2i.Zero, RenderbufferFormat.Rgba8, samples, 0L) :> IFramebufferOutput], None
        ) 

    let mutable frameCount = 0
    let mutable totalTime = MicroTime.Zero
    let sw = System.Diagnostics.Stopwatch()


    member x.AfterRender = afterRender.Publish
    member x.BeforeRender = beforeRender.Publish
    member x.FramebufferSignature  = signature :> IFramebufferSignature
    member x.RenderTask
        with get () = renderTask
        and set (v: IRenderTask) = 
            x.Invoke(fun () ->
                renderTaskSub.Dispose() 
                renderTask.Dispose()
                renderTask <- v
                renderTaskSub <- 
                    v.AddMarkingCallback (fun () ->
                        damaged <- true
                        glfw.PostEmptyEvent()
                    )
            )
    member x.Runtime = app.Runtime
    member x.Samples = samples
    member x.Sizes = currentSize :> aval<_>
    member x.Time = time
    member x.Keyboard = keyboard :> Aardvark.Application.IKeyboard
    member x.Mouse = mouse :> Aardvark.Application.IMouse

    interface Aardvark.Application.IRenderTarget with
        member x.AfterRender = afterRender.Publish
        member x.BeforeRender = beforeRender.Publish
        member x.FramebufferSignature  = signature :> _
        member x.RenderTask
            with get () = x.RenderTask
            and set (v: IRenderTask) = x.RenderTask <- v
        member x.Runtime = app.Runtime
        member x.Samples = samples
        member x.Sizes = currentSize :> aval<_>
        member x.Time = time

    interface Aardvark.Application.IRenderControl with
        member x.Keyboard = keyboard :> _
        member x.Mouse = mouse :> _
       
    interface Aardvark.Application.IRenderWindow with
        member x.Run() = x.Run()
       
    member x.Dispose() =
        app.RemoveExistingWindow x
        app.Post (fun () -> 
            glfw.HideWindow(win)
            glfw.DestroyWindow(win)
            ctx.Dispose()
            info.Dispose()
        )

    interface System.IDisposable with
        member x.Dispose() = x.Dispose()


    member x.Context = ctx
    member x.WindowInfo = info

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
                    let o = x.ContentPosition
                    let s = x.FramebufferSize
                    beforeFullScreen <- Box2i.FromMinAndSize(o, s)
                    let m = glfw.GetPrimaryMonitor()
                    let (x,y,w,h) = glfw.GetMonitorWorkarea(m)
                    let mode = glfw.GetVideoMode(m) |> NativePtr.read
                    glfw.SetWindowMonitor(win, m, x, y, w, h, mode.RefreshRate)
                else
                    let o = beforeFullScreen.Min
                    let s = beforeFullScreen.Size
                    glfw.SetWindowMonitor(win, NativePtr.zero, o.X, o.Y, s.X, s.Y, 0)
                damaged <- true                    
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
                lastMousePosition <- pos * windowScale
            
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
                    glfw.SetWindowTitle(win, t)
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
                let mutable scale = V2f.Zero
                let border = getFrameBorder()

                glfw.GetWindowSize(win, &ps.X, &ps.Y)
                glfw.GetWindowContentScale(win, &scale.X, &scale.Y)   
                let ws = ps + border.Min + border.Max
                V2i(round (float scale.X * float ws.X), round(float scale.Y * float ws.Y))  
            )
        and set (v : V2i) = 
            x.Invoke(fun () ->
                let mutable scale = V2f.Zero
                let border = getFrameBorder()

                glfw.GetWindowContentScale(win, &scale.X, &scale.Y)   

                let ws = V2i(round (float v.X / float scale.X), round(float v.Y / float scale.Y))  
                let ps = V2i.Max(V2i.II, ws - (border.Min + border.Max))

                glfw.SetWindowSize(win, ps.X, ps.Y)
            )

    member x.FramebufferSize
        with get() = 
            x.Invoke(fun () ->
                let (w,h) = glfw.GetFramebufferSize(win)
                V2i(w,h)
            )
        and set (v : V2i) =
            x.Invoke(fun () ->
                let mutable scale = V2f.Zero
                glfw.GetWindowContentScale(win, &scale.X, &scale.Y)   
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

                let rec mipMaps (acc : System.Collections.Generic.List<Image>) (i : int) (img : PixImage[]) =
                    if i >= img.Length then
                        let arr = acc.ToArray()
                        use img = fixed arr
                        glfw.SetWindowIcon(win, acc.Count, img)
                    else
                        let mat = toMatrix (img.[i].ToPixImage<byte>())
                        use pdata = fixed mat.Data
                        acc.Add(Image(Width = int mat.Size.X, Height = int mat.Size.Y, Pixels = NativePtr.cast pdata))

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

    member x.NewFrame (t : MicroTime) = 
        frameCount <- frameCount + 1
        totalTime <- totalTime + t
        if frameCount > 50 then
            let fps = float frameCount / totalTime.TotalSeconds
            x.Title <- sprintf "Aardvark rocks \\o/ (%.3f fps)" fps
            frameCount <- 0
            totalTime <- MicroTime.Zero
        ()

    member internal x.Redraw() : bool =
        if damaged then
            damaged <- false
            if not (isNull ctx) then
                use __ = app.Context.RenderingLock ctxHandle
                
                glfw.SwapInterval(0)
                beforeRender.Trigger()
                let s = x.FramebufferSize
                defaultFramebuffer.Size <- s
                let output = OutputDescription.ofFramebuffer defaultFramebuffer

                GL.ColorMask(true, true, true, true)
                GL.DepthMask(true)
                GL.Viewport(0, 0, s.X, s.Y)
                GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f)
                GL.ClearDepth(1.0)
                GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)

                renderTask.Run(AdaptiveToken.Top, RenderToken.Empty, output)

                glfw.SwapBuffers(win)                 

                afterRender.Trigger()  
                transact time.MarkOutdated  

                let t = sw.MicroTime
                sw.Restart()
                x.NewFrame t

                renderTask.OutOfDate
            else
                renderTask.OutOfDate
        else
            renderTask.OutOfDate

    member x.Run() =
        app.Run x          

