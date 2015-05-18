namespace Aardvark.Application.WPF


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

    member x.Sizes = ctrl.Sizes
    member x.Keyboard = ctrl.Keyboard
    member x.Mouse = ctrl.Mouse
    member x.Time = ctrl.Time

    interface IRenderControl with
        member x.Time = ctrl.Time
        member x.RenderTask
            with get() = ctrl.RenderTask
            and set t = ctrl.RenderTask <- t

        member x.Sizes = ctrl.Sizes
        member x.Keyboard = ctrl.Keyboard
        member x.Mouse = ctrl.Mouse

[<AbstractClass; Sealed; Extension>]
type WPFApplicationExtensions private() =
    
    [<Extension>]
    static member CreateSimpleRenderWindow(this : IApplication, samples : int) =
        let w = new SimpleRenderWindow()
        this.Initialize(w.Control)
        w

    [<Extension>]
    static member CreateSimpleRenderWindow(this : IApplication) =
        WPFApplicationExtensions.CreateSimpleRenderWindow(this, 1)


