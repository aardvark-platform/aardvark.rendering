namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System
open System.Reflection
open System.Runtime.InteropServices

type CustomDeviceChooser private() =
    static let mutable choose : Option<seq<PhysicalDevice> -> PhysicalDevice> = None

    static member Register(c : seq<PhysicalDevice> -> PhysicalDevice) =
        choose <- Some c

    static member Filter(devices : seq<PhysicalDevice>) =
        match choose with
        | Some c -> Seq.singleton(c devices)
        | None -> devices

module ConsoleDeviceChooser =
    open System.IO

    module private Keyboard =

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

    module Config =
        open System.Security.Cryptography
        open System.Text

        let private filePath =
            let newHash() =
                Guid.NewGuid().ToByteArray() |> Convert.ToBase64String

            let appHash =
                try
                    let asm = Assembly.GetEntryAssembly()
                    let location = if isNull asm then null else asm.Location

                    if String.IsNullOrWhiteSpace location then
                        newHash()
                    else
                        let md5 = MD5.Create()
                        location
                        |> Encoding.Unicode.GetBytes
                        |> md5.ComputeHash
                        |> Convert.ToBase64String
                with _ ->
                    newHash()

            Path.combine [
                CachingProperties.CacheDirectory
                "Config"
                $"{appHash.Replace('/', '_')}.vkconfig"
            ]

        /// Reads the config file to determine a device to use.
        let tryRead (devices: PhysicalDevice seq) =
            if File.Exists filePath then
                try
                    let currentIds = devices |> Seq.map _.Id |> Set.ofSeq
                    let cachedIds = File.readAllLines filePath

                    // If there is a new device do not use the cached setting
                    if Set.isSuperset (Set.ofSeq cachedIds) currentIds then
                        devices |> Seq.tryFind (fun d -> d.Id = cachedIds.[0])
                    else
                        None

                with e ->
                    Log.warn $"[Vulkan] Failed to read device config file '{filePath}': {e.Message}"
                    None
            else
                None

        /// Writes the chosen device to the config file.
        let write (chosen: PhysicalDevice) (devices: PhysicalDevice seq) =
            try
                let otherDeviceIds =
                    devices
                    |> Seq.map _.Id
                    |> Seq.distinct
                    |> Seq.filter ((<>) chosen.Id)
                    |> Seq.toArray

                Array.append [| chosen.Id |] otherDeviceIds
                |> File.writeAllLinesSafe filePath
            with e ->
                Log.warn $"[Vulkan] Failed to write device config file '{filePath}': {e.Message}"

    let run' (preferred : Option<PhysicalDevice>) (devices : seq<PhysicalDevice>) =
        let devices = Seq.toList devices
        match devices with
        | [single] -> single
        | _ ->
            let choose() =
                let devices = List.toArray devices
                Log.line "Multiple GPUs detected (please select one)"
                for i in 0 .. devices.Length - 1 do
                    let d = devices.[i]

                    let prefix =
                        match d with
                            | :? PhysicalDeviceGroup as g -> sprintf "%d x "g.Devices.Length
                            | _ -> ""

                    Log.line "   %d: %s%s" i prefix d.FullName

                let mutable chosenId = -1
                while chosenId < 0 do
                    printf " > "
                    let entry = Console.ReadLine()
                    match Int32.TryParse(entry) with
                    | true, v when v >= 0 && v < devices.Length -> chosenId <- v
                    | _ -> ()

                let chosen = devices.[chosenId]
                Config.write chosen devices
                chosen

            if Keyboard.altDown() then
                choose()
            else
                match preferred with
                | Some pref -> pref
                | _ ->
                    match Config.tryRead devices with
                    | Some chosen -> chosen
                    | _ -> choose()

    let run (devices : seq<PhysicalDevice>) : PhysicalDevice =
        run' None devices