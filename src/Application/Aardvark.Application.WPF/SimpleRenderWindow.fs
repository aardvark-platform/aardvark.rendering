namespace Aardvark.Application.WPF

open System.Runtime.CompilerServices
open System.Windows
open Aardvark.Base
open Aardvark.Application
open System.Windows.Media.Imaging

type SimpleRenderWindow() as this =
    inherit Window()
    let ctrl = RenderControl()

    do
        base.Width <- 1024.0
        base.Height <- 768.0
        base.Title <- "Aardvark rocks WPF \\o/"
        ctrl.BorderThickness <- Thickness(0.0)
        base.BorderThickness <- Thickness(0.0)
        base.Content <- ctrl

        try
            let asm = typeof<SimpleRenderWindow>.Assembly
            let resourceName = asm.GetManifestResourceNames() |> Array.tryFind (String.endsWith "aardvark-icon.bin")

            match resourceName with
            | Some name ->
                use s = asm.GetManifestResourceStream(name)
                let png = IconBitmapDecoder(s, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None)
                this.Icon <- png.Frames.[0]
            | _ ->
                Log.error "[WPF] Could not find icon."
        with exn ->
            Log.error "[WPF] Failed to load icon: %s" exn.Message

        let mutable d = { new System.IDisposable with member x.Dispose() = () }
        d <- ctrl.AfterRender.Subscribe (fun _ -> d.Dispose(); this.Dispatcher.BeginInvoke(new System.Action(fun () -> ctrl.FocusReal() |> ignore)) |> ignore)

    member x.Control = ctrl

    member x.RenderTask
        with get() = ctrl.RenderTask
        and set t = ctrl.RenderTask <- t
    member x.SubSampling
        with get() = ctrl.SubSampling
        and set t = ctrl.SubSampling <- t
        
    member x.FramebufferSignature = ctrl.FramebufferSignature
    member x.Runtime = ctrl.Runtime
    member x.Sizes = ctrl.Sizes
    member x.Samples = ctrl.Samples
    member x.Keyboard = ctrl.Keyboard
    member x.Mouse = ctrl.Mouse
    member x.Time = ctrl.Time
    member x.Run() = 
        let app = Application()
        app.Run(x) |> ignore
        
    member x.Cursor
        with get() = ctrl.Cursor
        and set c = ctrl.Cursor <- c
    member x.BeforeRender = ctrl.BeforeRender
    member x.AfterRender = ctrl.AfterRender
    interface IRenderWindow with
        member x.Cursor
            with get() = ctrl.Cursor
            and set c = ctrl.Cursor <- c
        member x.FramebufferSignature = ctrl.FramebufferSignature
        member x.Runtime = ctrl.Runtime
        member x.Time = ctrl.Time
        member x.RenderTask
            with get() = ctrl.RenderTask
            and set t = ctrl.RenderTask <- t
        member x.SubSampling
            with get() = ctrl.SubSampling
            and set t = ctrl.SubSampling <- t

        member x.Sizes = ctrl.Sizes
        member x.Samples = ctrl.Samples
        member x.Keyboard = ctrl.Keyboard
        member x.Mouse = ctrl.Mouse
        member x.Run() = x.Run()
        member x.BeforeRender = ctrl.BeforeRender
        member x.AfterRender = ctrl.AfterRender
        member x.Dispose() = ()


[<AbstractClass; Sealed; Extension>]
type WPFApplicationExtensions private() =
    
    [<Extension>]
    static member CreateSimpleRenderWindow(this : IApplication, samples : int) =
        let w = new SimpleRenderWindow()
        this.Initialize(w.Control, samples)
        w

    [<Extension>]
    static member CreateSimpleRenderWindow(this : IApplication) =
        WPFApplicationExtensions.CreateSimpleRenderWindow(this, 1)

