namespace Aardvark.Rendering.ImGui

open Aardvark.Application
open Aardvark.Base
open Silk.NET.GLFW
open Hexa.NET.ImGui
open System
open System.Diagnostics
open System.Numerics

// Based on the official ImGui GLFW backend
// Reimplemented here to avoid issues with native dependencies

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal Keys =

    let private ofKeyName =
        LookupTable.tryLookupV [
            '`',  Keys.GraveAccent
            '-',  Keys.Minus
            '=',  Keys.Equal
            '[',  Keys.LeftBracket
            ']',  Keys.RightBracket
            '\\', Keys.BackSlash
            ',',  Keys.Comma
            ';',  Keys.Semicolon
            '\'', Keys.Apostrophe
            '.',  Keys.Period
            '/',  Keys.Slash
            '0',  Keys.Number0
            '1',  Keys.Number1
            '2',  Keys.Number2
            '3',  Keys.Number3
            '4',  Keys.Number4
            '5',  Keys.Number5
            '6',  Keys.Number6
            '7',  Keys.Number7
            '8',  Keys.Number8
            '9',  Keys.Number9
            'a',  Keys.A
            'b',  Keys.B
            'c',  Keys.C
            'd',  Keys.D
            'e',  Keys.E
            'f',  Keys.F
            'g',  Keys.G
            'h',  Keys.H
            'i',  Keys.I
            'j',  Keys.J
            'k',  Keys.K
            'l',  Keys.L
            'm',  Keys.M
            'n',  Keys.N
            'o',  Keys.O
            'p',  Keys.P
            'q',  Keys.Q
            'r',  Keys.R
            's',  Keys.S
            't',  Keys.T
            'u',  Keys.U
            'v',  Keys.V
            'w',  Keys.W
            'x',  Keys.X
            'y',  Keys.Y
            'z',  Keys.Z
            'A',  Keys.A
            'B',  Keys.B
            'C',  Keys.C
            'D',  Keys.D
            'E',  Keys.E
            'F',  Keys.F
            'G',  Keys.G
            'H',  Keys.H
            'I',  Keys.I
            'J',  Keys.J
            'K',  Keys.K
            'L',  Keys.L
            'M',  Keys.M
            'N',  Keys.N
            'O',  Keys.O
            'P',  Keys.P
            'Q',  Keys.Q
            'R',  Keys.R
            'S',  Keys.S
            'T',  Keys.T
            'U',  Keys.U
            'V',  Keys.V
            'W',  Keys.W
            'X',  Keys.X
            'Y',  Keys.Y
            'Z',  Keys.Z
        ]

    let untranslate (glfw: Glfw) (scancode: int) (key: Keys) =
        if key >= Keys.Keypad0 && key <= Keys.KeypadEqual then
            key
        else
            let prevErrorCb = glfw.SetErrorCallback null
            let keyName = glfw.GetKeyName(int key, scancode)
            glfw.SetErrorCallback prevErrorCb |> ignore
            glfw.GetError() |> ignore

            if String.IsNullOrEmpty keyName || keyName.Length > 1 then
                key
            else
                ofKeyName keyName.[0] |> ValueOption.defaultValue key

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal ImGuiKey =

    let ofKeys =
        LookupTable.tryLookupV [
            Keys.Tab,            ImGuiKey.Tab;
            Keys.Left,           ImGuiKey.LeftArrow;
            Keys.Right,          ImGuiKey.RightArrow;
            Keys.Up,             ImGuiKey.UpArrow;
            Keys.Down,           ImGuiKey.DownArrow;
            Keys.PageUp,         ImGuiKey.PageUp;
            Keys.PageDown,       ImGuiKey.PageDown;
            Keys.Home,           ImGuiKey.Home;
            Keys.End,            ImGuiKey.End;
            Keys.Insert,         ImGuiKey.Insert;
            Keys.Delete,         ImGuiKey.Delete;
            Keys.Backspace,      ImGuiKey.Backspace;
            Keys.Space,          ImGuiKey.Space;
            Keys.Enter,          ImGuiKey.Enter;
            Keys.Escape,         ImGuiKey.Escape;
            Keys.Apostrophe,     ImGuiKey.Apostrophe;
            Keys.Comma,          ImGuiKey.Comma;
            Keys.Minus,          ImGuiKey.Minus;
            Keys.Period,         ImGuiKey.Period;
            Keys.Slash,          ImGuiKey.Slash;
            Keys.Semicolon,      ImGuiKey.Semicolon;
            Keys.Equal,          ImGuiKey.Equal;
            Keys.LeftBracket,    ImGuiKey.LeftBracket;
            Keys.BackSlash,      ImGuiKey.Backslash;
            Keys.World1,         ImGuiKey.Oem102;
            Keys.World2,         ImGuiKey.Oem102;
            Keys.RightBracket,   ImGuiKey.RightBracket;
            Keys.GraveAccent,    ImGuiKey.GraveAccent;
            Keys.CapsLock,       ImGuiKey.CapsLock;
            Keys.ScrollLock,     ImGuiKey.ScrollLock;
            Keys.NumLock,        ImGuiKey.NumLock;
            Keys.PrintScreen,    ImGuiKey.PrintScreen;
            Keys.Pause,          ImGuiKey.Pause;
            Keys.Keypad0,        ImGuiKey.Keypad0;
            Keys.Keypad1,        ImGuiKey.Keypad1;
            Keys.Keypad2,        ImGuiKey.Keypad2;
            Keys.Keypad3,        ImGuiKey.Keypad3;
            Keys.Keypad4,        ImGuiKey.Keypad4;
            Keys.Keypad5,        ImGuiKey.Keypad5;
            Keys.Keypad6,        ImGuiKey.Keypad6;
            Keys.Keypad7,        ImGuiKey.Keypad7;
            Keys.Keypad8,        ImGuiKey.Keypad8;
            Keys.Keypad9,        ImGuiKey.Keypad9;
            Keys.KeypadDecimal,  ImGuiKey.KeypadDecimal;
            Keys.KeypadDivide,   ImGuiKey.KeypadDivide;
            Keys.KeypadMultiply, ImGuiKey.KeypadMultiply;
            Keys.KeypadSubtract, ImGuiKey.KeypadSubtract;
            Keys.KeypadAdd,      ImGuiKey.KeypadAdd;
            Keys.KeypadEnter,    ImGuiKey.KeypadEnter;
            Keys.KeypadEqual,    ImGuiKey.KeypadEqual;
            Keys.ShiftLeft,      ImGuiKey.LeftShift;
            Keys.ControlLeft,    ImGuiKey.LeftCtrl;
            Keys.AltLeft,        ImGuiKey.LeftAlt;
            Keys.SuperLeft,      ImGuiKey.LeftSuper;
            Keys.ShiftRight,     ImGuiKey.RightShift;
            Keys.ControlRight,   ImGuiKey.RightCtrl;
            Keys.AltRight,       ImGuiKey.RightAlt;
            Keys.SuperRight,     ImGuiKey.RightSuper;
            Keys.Menu,           ImGuiKey.Menu;
            Keys.Number0,        ImGuiKey.Key0;
            Keys.Number1,        ImGuiKey.Key1;
            Keys.Number2,        ImGuiKey.Key2;
            Keys.Number3,        ImGuiKey.Key3;
            Keys.Number4,        ImGuiKey.Key4;
            Keys.Number5,        ImGuiKey.Key5;
            Keys.Number6,        ImGuiKey.Key6;
            Keys.Number7,        ImGuiKey.Key7;
            Keys.Number8,        ImGuiKey.Key8;
            Keys.Number9,        ImGuiKey.Key9;
            Keys.A,              ImGuiKey.A;
            Keys.B,              ImGuiKey.B;
            Keys.C,              ImGuiKey.C;
            Keys.D,              ImGuiKey.D;
            Keys.E,              ImGuiKey.E;
            Keys.F,              ImGuiKey.F;
            Keys.G,              ImGuiKey.G;
            Keys.H,              ImGuiKey.H;
            Keys.I,              ImGuiKey.I;
            Keys.J,              ImGuiKey.J;
            Keys.K,              ImGuiKey.K;
            Keys.L,              ImGuiKey.L;
            Keys.M,              ImGuiKey.M;
            Keys.N,              ImGuiKey.N;
            Keys.O,              ImGuiKey.O;
            Keys.P,              ImGuiKey.P;
            Keys.Q,              ImGuiKey.Q;
            Keys.R,              ImGuiKey.R;
            Keys.S,              ImGuiKey.S;
            Keys.T,              ImGuiKey.T;
            Keys.U,              ImGuiKey.U;
            Keys.V,              ImGuiKey.V;
            Keys.W,              ImGuiKey.W;
            Keys.X,              ImGuiKey.X;
            Keys.Y,              ImGuiKey.Y;
            Keys.Z,              ImGuiKey.Z;
            Keys.F1,             ImGuiKey.F1;
            Keys.F2,             ImGuiKey.F2;
            Keys.F3,             ImGuiKey.F3;
            Keys.F4,             ImGuiKey.F4;
            Keys.F5,             ImGuiKey.F5;
            Keys.F6,             ImGuiKey.F6;
            Keys.F7,             ImGuiKey.F7;
            Keys.F8,             ImGuiKey.F8;
            Keys.F9,             ImGuiKey.F9;
            Keys.F10,            ImGuiKey.F10;
            Keys.F11,            ImGuiKey.F11;
            Keys.F12,            ImGuiKey.F12;
            Keys.F13,            ImGuiKey.F13;
            Keys.F14,            ImGuiKey.F14;
            Keys.F15,            ImGuiKey.F15;
            Keys.F16,            ImGuiKey.F16;
            Keys.F17,            ImGuiKey.F17;
            Keys.F18,            ImGuiKey.F18;
            Keys.F19,            ImGuiKey.F19;
            Keys.F20,            ImGuiKey.F20;
            Keys.F21,            ImGuiKey.F21;
            Keys.F22,            ImGuiKey.F22;
            Keys.F23,            ImGuiKey.F23;
            Keys.F24,            ImGuiKey.F24;
        ]
        >> ValueOption.defaultValue ImGuiKey.None

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal ImGuiMouseCursor =

    let toCursor =
        LookupTable.tryLookupV [
              ImGuiMouseCursor.None,       Cursor.None
              ImGuiMouseCursor.Arrow,      Cursor.Arrow
              ImGuiMouseCursor.TextInput,  Cursor.Text
              ImGuiMouseCursor.ResizeAll,  Cursor.Arrow
              ImGuiMouseCursor.ResizeNs,   Cursor.VerticalResize
              ImGuiMouseCursor.ResizeEw,   Cursor.HorizontalResize
              ImGuiMouseCursor.ResizeNesw, Cursor.Arrow
              ImGuiMouseCursor.ResizeNwse, Cursor.Arrow
              ImGuiMouseCursor.Hand,       Cursor.Hand
              ImGuiMouseCursor.Wait,       Cursor.Arrow
              ImGuiMouseCursor.Progress,   Cursor.Arrow
              ImGuiMouseCursor.NotAllowed, Cursor.Arrow
        ]
        >> ValueOption.defaultValue Cursor.Arrow

type internal Platform(window: Aardvark.Glfw.Window, io: ImGuiIOPtr) =
    let glfw = window.Instance.Glfw

    let mutable prevWindowFocusCb = Unchecked.defaultof<GlfwCallbacks.WindowFocusCallback>
    let mutable prevCursorEnterCb = Unchecked.defaultof<GlfwCallbacks.CursorEnterCallback>
    let mutable prevCursorPosCb   = Unchecked.defaultof<GlfwCallbacks.CursorPosCallback>
    let mutable prevMouseButtonCb = Unchecked.defaultof<GlfwCallbacks.MouseButtonCallback>
    let mutable prevScrollCb      = Unchecked.defaultof<GlfwCallbacks.ScrollCallback>
    let mutable prevKeyCb         = Unchecked.defaultof<GlfwCallbacks.KeyCallback>
    let mutable prevCharCb        = Unchecked.defaultof<GlfwCallbacks.CharCallback>

    let mutable lastValidMousePos = Vector2.Zero
    let mutable mouseWindow = NativePtr.zero
    let sw = Stopwatch()

    let isKeyPressed key =
        glfw.GetKey(window.Handle, key) = int InputAction.Press

    let updateKeyModifiers() =
        io.AddKeyEvent(ImGuiKey.ModCtrl, isKeyPressed Keys.ControlLeft || isKeyPressed Keys.ControlRight)
        io.AddKeyEvent(ImGuiKey.ModShift, isKeyPressed Keys.ShiftLeft || isKeyPressed Keys.ShiftRight)
        io.AddKeyEvent(ImGuiKey.ModAlt, isKeyPressed Keys.AltLeft || isKeyPressed Keys.AltRight)
        io.AddKeyEvent(ImGuiKey.ModSuper, isKeyPressed Keys.SuperLeft || isKeyPressed Keys.SuperRight)

    let onWindowFocus window focused =
        prevWindowFocusCb.Invoke(window, focused)
        io.AddFocusEvent focused

    let onCursorEnter window entered =
        prevCursorEnterCb.Invoke(window, entered)
        if entered then
            mouseWindow <- window
            io.AddMousePosEvent(lastValidMousePos.X, lastValidMousePos.Y)
        elif not entered && mouseWindow = window then
            lastValidMousePos <- io.MousePos
            mouseWindow <- NativePtr.zero
            io.AddMousePosEvent(Single.MinValue, Single.MinValue)

    let onCursorPos window x y =
        prevCursorPosCb.Invoke(window, x, y)
        lastValidMousePos <- Vector2(float32 x, float32 y)
        io.AddMousePosEvent(float32 x, float32 y)

    let onMouseButton window button action mods =
        prevMouseButtonCb.Invoke(window, button, action, mods)
        updateKeyModifiers()
        if int button >= 0 && int button < int ImGuiMouseButton.Count then
            io.AddMouseButtonEvent(int button, (action = InputAction.Press))

    let onScroll window offsetX offsetY =
        prevScrollCb.Invoke(window, offsetX, offsetY)
        io.AddMouseWheelEvent(float32 offsetX, float32 offsetY)

    let onKey window key scancode action mods =
        prevKeyCb.Invoke(window, key, scancode, action, mods)
        updateKeyModifiers()
        let key = Keys.untranslate glfw scancode key
        let imguiKey = ImGuiKey.ofKeys key
        io.AddKeyEvent(imguiKey, (action = InputAction.Press))
        io.SetKeyEventNativeData(imguiKey, int key, scancode)

    let onChar window codepoint =
        prevCharCb.Invoke(window, codepoint)
        io.AddInputCharacter codepoint

    let initialize() : unit =
        &io.BackendFlags |||= ImGuiBackendFlags.HasMouseCursors
        &io.BackendFlags |||= ImGuiBackendFlags.HasSetMousePos

        prevWindowFocusCb <- glfw.SetWindowFocusCallback(window.Handle, GlfwCallbacks.WindowFocusCallback onWindowFocus)
        prevCursorEnterCb <- glfw.SetCursorEnterCallback(window.Handle, GlfwCallbacks.CursorEnterCallback onCursorEnter)
        prevCursorPosCb   <- glfw.SetCursorPosCallback(window.Handle, GlfwCallbacks.CursorPosCallback onCursorPos)
        prevMouseButtonCb <- glfw.SetMouseButtonCallback(window.Handle, GlfwCallbacks.MouseButtonCallback onMouseButton)
        prevScrollCb      <- glfw.SetScrollCallback(window.Handle, GlfwCallbacks.ScrollCallback onScroll)
        prevKeyCb         <- glfw.SetKeyCallback(window.Handle, GlfwCallbacks.KeyCallback onKey)
        prevCharCb        <- glfw.SetCharCallback(window.Handle, GlfwCallbacks.CharCallback onChar)

    do initialize()

    member private _.SizeAndScale =
        let mutable windowSize = V2i.Zero
        let mutable displaySize = V2i.Zero
        glfw.GetWindowSize(window.Handle, &windowSize.X, &windowSize.Y)
        glfw.GetFramebufferSize(window.Handle, &displaySize.X, &displaySize.Y)

        let framebufferScale =
            if windowSize.X > 0 && windowSize.Y > 0 then
                V2f displaySize / V2f windowSize
            else
                V2f.One

        struct (windowSize, framebufferScale)

    member private _.UpdateMousePosition() =
        if glfw.GetWindowAttrib(window.Handle, WindowAttributeGetter.Focused) then
            if io.WantSetMousePos then
                glfw.SetCursorPos(window.Handle, float io.MousePos.X, float io.MousePos.Y)

            if NativePtr.isNull mouseWindow then
                let mutable pos = V2d.Zero
                glfw.GetCursorPos(window.Handle, &pos.X, &pos.Y)
                lastValidMousePos <- Vector2(float32 pos.X, float32 pos.Y)
                io.AddMousePosEvent(float32 pos.X, float32 pos.Y)

    member private _.UpdateMouseCursor() =
        let cursorChange = not <| io.ConfigFlags.HasFlag ImGuiConfigFlags.NoMouseCursorChange
        let cursorMode = enum<CursorModeValue> <| glfw.GetInputMode(window.Handle, CursorStateAttribute.Cursor)

        if cursorChange && cursorMode <> CursorModeValue.CursorDisabled then
            if io.MouseDrawCursor then
                window.Cursor <- Cursor.None
            else
                let cursor =  ImGui.GetMouseCursor() |> ImGuiMouseCursor.toCursor
                window.Cursor <- cursor

    member this.NewFrame() =
        let struct (size, scale) = this.SizeAndScale
        io.DisplaySize <- Vector2(float32 size.X, float32 size.Y)
        io.DisplayFramebufferScale <- Vector2(scale.X, scale.Y)
        io.DeltaTime <- float32 sw.Elapsed.TotalSeconds
        this.UpdateMousePosition()
        this.UpdateMouseCursor()
        sw.Restart()

    member _.Dispose() =
        &io.BackendFlags &&&= ~~~(ImGuiBackendFlags.HasMouseCursors ||| ImGuiBackendFlags.HasSetMousePos);
        glfw.SetWindowFocusCallback(window.Handle, prevWindowFocusCb) |> ignore
        glfw.SetCursorEnterCallback(window.Handle, prevCursorEnterCb) |> ignore
        glfw.SetCursorPosCallback(window.Handle, prevCursorPosCb) |> ignore
        glfw.SetMouseButtonCallback(window.Handle, prevMouseButtonCb) |> ignore
        glfw.SetScrollCallback(window.Handle, prevScrollCb) |> ignore
        glfw.SetKeyCallback(window.Handle, prevKeyCb) |> ignore
        glfw.SetCharCallback(window.Handle, prevCharCb) |> ignore

    interface IDisposable with
        member this.Dispose() = this.Dispose()