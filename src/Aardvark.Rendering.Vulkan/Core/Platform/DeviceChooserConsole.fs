namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System
open System.Runtime.InteropServices

module internal ``DeviceChooserConsole Utilities`` =

    module Keyboard =

        type private KeyCode =
            | LeftAlt = 0xA4
            | RightAlt = 0xA5
            | LeftShift = 0xA0
            | RightShift = 0xA1

        module private Win32 =
            [<DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)>]
            extern uint16 private GetKeyState(KeyCode keyCode)

            let isDown (key : KeyCode) =
                let state = GetKeyState(key)
                (state &&& 0x8000us) = 0x8000us

        module private X11 =
            let XK_Alt_L = 0xffe9

            [<DllImport("X11")>]
            extern nativeint XOpenDisplay(nativeint ptr)

            [<DllImport("X11")>]
            extern int XCloseDisplay(nativeint ptr)

            [<DllImport("X11")>]
            extern int XQueryKeymap(nativeint dpy, byte[] keys)

            [<DllImport("X11")>]
            extern byte XKeysymToKeycode(nativeint dpy, int thing)

            let altDown() =
                let dpy = XOpenDisplay(0n)
                if dpy = 0n then false
                else
                    try
                        let keys = Array.zeroCreate<byte> 256
                        XQueryKeymap(dpy, keys) |> ignore
                        let kc2 = XKeysymToKeycode(dpy, XK_Alt_L) |> int
                        let pressed = keys.[ kc2>>>3 ] &&& ( 1uy<<<(kc2&&&7) )
                        pressed <> 0uy
                    finally
                        XCloseDisplay(dpy) |> ignore

        /// Returns whether the ALT key is pressed.
        /// Only works on Windows and Linux with X11 currently.
        let altDown() =
            match Environment.OSVersion with
            | Windows -> Win32.isDown KeyCode.LeftAlt || Win32.isDown KeyCode.RightAlt
            | Linux -> X11.altDown()
            | _ -> false

type DeviceChooserConsole() =
    inherit DeviceChooser()

    override _.IgnoreCache = ``DeviceChooserConsole Utilities``.Keyboard.altDown()

    override _.Choose(devices) =
        Log.line "Multiple GPUs detected (please select one)"
        for i in 0 .. devices.Length - 1 do
            let d = devices.[i]

            let prefix =
                match d with
                | :? PhysicalDeviceGroup as g -> $"{g.Devices.Length} x "
                | _ -> ""

            Log.line $"   {i}: {prefix}{d.FullName}"

        let mutable chosenId = -1
        while chosenId < 0 do
            printf " > "
            let entry = Console.ReadLine()
            match Int32.TryParse(entry) with
            | true, v when v >= 0 && v < devices.Length -> chosenId <- v
            | _ -> ()

        devices.[chosenId]
