namespace Aardvark.Application.WinForms

open System
open System.Windows.Forms
open System.Drawing
open Aardvark.Base
open Aardvark.Application

type RenderControl() =
    inherit Control()

    let mutable renderTask : Option<IRenderTask> = None
    let mutable impl : Option<IRenderControlImplementation> = None
    let mutable ctrl : Option<Control> = None

    let keyboard = new ChangeableKeyboard()
    let mouse = new ChangeableMouse()
    let sizes = new EventSource<V2i>()
    let mutable cameraView = CameraViewWithSky(Location = V3d.III * 6.0, Forward = -V3d.III.Normalized) :> ICameraView
    let mutable cameraProjection = CameraProjectionPerspective(60.0, 0.1, 1000.0, 1.0) :> ICameraProjection

    let setControl (self : RenderControl) (c : Control) (cr : IRenderControlImplementation) =
        match impl with
            | Some i -> failwith "implementation can only be set once per control"
            | None -> ()

        c.Dock <- DockStyle.Fill
        self.Controls.Add c

        keyboard.Inner <- new Keyboard(c)
        mouse.Inner <- new Mouse(c)
        keyboard.Inner <- new Keyboard(c)
        match renderTask with
            | Some task -> cr.RenderTask <- task
            | None -> ()


        ctrl <- Some c
        impl <- Some cr

    member x.Implementation
        with get() = match ctrl with | Some c -> c | _ -> null
        and set v = setControl x v (v |> unbox<IRenderControlImplementation>)

    override x.OnResize(e) =
        base.OnResize(e)
        sizes.Emit (V2i(base.ClientSize.Width, base.ClientSize.Height))



    member x.Sizes = sizes :> IEvent<V2i>
    member x.CameraView
        with get() = cameraView
        and set v = cameraView <- v

    member x.CameraProjection
        with get() = cameraProjection
        and set p = cameraProjection <- p

    member x.Keyboard = keyboard :> IKeyboard
    member x.Mouse = mouse :> IMouse

    member x.RenderTask 
        with get() = match renderTask with | Some t -> t | _ -> Unchecked.defaultof<_>
        and set t = 
            renderTask <- Some t
            match impl with
                | Some i -> i.RenderTask <- t
                | None -> ()


    interface IRenderControl with
        member x.Sizes = x.Sizes
        member x.CameraView
            with get() = x.CameraView
            and set v = x.CameraView <- v

        member x.CameraProjection
            with get() = x.CameraProjection
            and set p = x.CameraProjection <- p

        member x.Keyboard = x.Keyboard
        member x.Mouse = x.Mouse

        member x.RenderTask 
            with get() = x.RenderTask
            and set t = x.RenderTask <- t
    