namespace Aardvark.Application.WinForms.Vulkan

open System
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Windows.Forms
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Reflection
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Surface =
    module private Xcb = 
        [<DllImport("X11-xcb")>]
        extern nativeint private XGetXCBConnection(nativeint xdisplay)

    let private staticFlags = BindingFlags.Static ||| BindingFlags.NonPublic ||| BindingFlags.Public
    let private (?) (t : Type) (name : string) =
        let prop = t.GetProperty(name, staticFlags)
        if isNull prop then 
            let field = t.GetField(name, staticFlags)
            if isNull field then failwithf "cannot get %A.%s" t name
            else field.GetValue(null) |> unbox<'a>
        else
            prop.GetValue(null) |> unbox<'a>

    let ofControl (ctrl : Control) (device : Device) =
        let instance = device.Instance

        match Environment.OSVersion with
            | Windows ->
                let entryModule     = Assembly.GetEntryAssembly().GetModules().[0]
                let hwnd            = ctrl.Handle
                let hinstance       = Marshal.GetHINSTANCE(entryModule)

                let info = Win32 { hinstance = hinstance; hwnd = hwnd }
                device.CreateSurface info

            | Linux ->
                let xp = Type.GetType("System.Windows.Forms.XplatUIX11, System.Windows.Forms")
                if isNull xp then failwithf "could not get System.Windows.Forms.XplatUIX11"
                let display = xp?DisplayHandle
                let window = ctrl.Handle
                

//                if Set.contains Instance.Extensions.XlibSurface instance.EnabledExtensions then
//                    let dpy = NativePtr.alloc 1
//                    NativePtr.write dpy display
//                    
//                    Log.warn "display: %A" display
//                    Log.warn "window:  %A" window
//
//                    let info = XLib { dpy = NativePtr.ofNativeInt display; window = window }
//                    let res = device.CreateSurface info
//                    res.OnDispose.Add (fun () -> NativePtr.free dpy)
//                    res
//
//                else

                let connection = Xcb.XGetXCBConnection(display)

                let dpy = NativePtr.alloc 1
                NativePtr.write dpy connection

                let info = Xcb { connection = dpy; window = window }
                let res = device.CreateSurface info
                res.OnDispose.Add (fun () -> NativePtr.free dpy)
                res

            | Mac ->
                failf "Apple sadly decided not to support Vulkan"

[<AbstractClass; Sealed; Extension>]
type DeviceSurfaceExtensions private() =
    [<Extension>]
    static member inline CreateSurface(this : Device, ctrl : Control) =
        this |> Surface.ofControl ctrl
