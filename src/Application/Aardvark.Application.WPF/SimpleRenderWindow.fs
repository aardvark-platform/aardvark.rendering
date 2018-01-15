﻿namespace Aardvark.Application.WPF

#if WINDOWS

open System.Runtime.CompilerServices
open System.Windows
open System.Windows.Controls
open System.Windows.Media
open Aardvark.Base
open Aardvark.Application



type SimpleRenderWindow() =
    inherit Window()
    let ctrl = new RenderControl()

    do base.Width <- 1024.0
       base.Height <- 768.0
       base.Content <- ctrl

    member x.Control = ctrl

    member x.RenderTask
        with get() = ctrl.RenderTask
        and set t = ctrl.RenderTask <- t
        
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
        
    member x.BeforeRender = ctrl.BeforeRender
    member x.AfterRender = ctrl.AfterRender
    interface IRenderWindow with
        member x.FramebufferSignature = ctrl.FramebufferSignature
        member x.Runtime = ctrl.Runtime
        member x.Time = ctrl.Time
        member x.RenderTask
            with get() = ctrl.RenderTask
            and set t = ctrl.RenderTask <- t

        member x.Sizes = ctrl.Sizes
        member x.Samples = ctrl.Samples
        member x.Keyboard = ctrl.Keyboard
        member x.Mouse = ctrl.Mouse
        member x.Run() = x.Run()
        member x.BeforeRender = ctrl.BeforeRender
        member x.AfterRender = ctrl.AfterRender


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

#endif
