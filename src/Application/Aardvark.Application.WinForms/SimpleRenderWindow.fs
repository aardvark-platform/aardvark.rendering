namespace Aardvark.Application.WinForms

open System
open System.Runtime.CompilerServices
open Aardvark.Application
open System.Windows.Forms

type SimpleRenderWindow() =
    inherit Form()

    let ctrl = new RenderControl()

    do base.Width <- 1024
       base.Height <- 768
       ctrl.Dock <- DockStyle.Fill
       base.Controls.Add ctrl

    member x.Control = ctrl

    member x.RenderTask
        with get() = ctrl.RenderTask
        and set t = ctrl.RenderTask <- t

    member x.Sizes = ctrl.Sizes
    member x.Keyboard = ctrl.Keyboard
    member x.Mouse = ctrl.Mouse

    interface IRenderControl with
        member x.RenderTask
            with get() = ctrl.RenderTask
            and set t = ctrl.RenderTask <- t

        member x.Sizes = ctrl.Sizes
        member x.Keyboard = ctrl.Keyboard
        member x.Mouse = ctrl.Mouse


[<AbstractClass; Sealed; Extension>]
type WinFormsApplicationExtensions private() =
    
    [<Extension>]
    static member CreateSimpleRenderWindow(this : IApplication, samples : int) =
        let w = new SimpleRenderWindow()
        this.Initialize(w.Control)
        w

    [<Extension>]
    static member CreateSimpleRenderWindow(this : IApplication) =
        WinFormsApplicationExtensions.CreateSimpleRenderWindow(this, 1)