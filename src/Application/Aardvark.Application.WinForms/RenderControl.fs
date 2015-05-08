namespace Aardvark.Application.WinForms

open System
open System.Windows.Forms
open System.Drawing
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Application

type RenderControl() =
    inherit Control()

    let mutable renderTask : Option<IRenderTask> = None
    let mutable impl : Option<IRenderTarget> = None
    let mutable ctrl : Option<Control> = None

    let keyboard = new ChangeableKeyboard()
    let mouse = new Mouse()
    let sizes = new EventSource<V2i>()
    let mutable inner : Option<IMod<DateTime>> = None
    let time = 
        Mod.custom (fun () -> 
            match inner with
                | Some m -> m.GetValue()
                | None -> DateTime.Now
           )

    let setControl (self : RenderControl) (c : Control) (cr : IRenderTarget) =
        match impl with
            | Some i -> failwith "implementation can only be set once per control"
            | None -> ()

        c.Dock <- DockStyle.Fill
        self.Controls.Add c

        keyboard.Inner <- new Keyboard(c)
        mouse.SetControl c
        match renderTask with
            | Some task -> cr.RenderTask <- task
            | None -> ()

        transact(fun () ->
            cr.Time.AddOutput(time)
            inner <- Some cr.Time
        )
        ctrl <- Some c
        impl <- Some cr

    member x.Implementation
        with get() = match ctrl with | Some c -> c | _ -> null
        and set v = setControl x v (v |> unbox<IRenderTarget>)

    override x.OnResize(e) =
        base.OnResize(e)
        sizes.Emit (V2i(base.ClientSize.Width, base.ClientSize.Height))



    member x.Sizes = sizes :> IEvent<V2i>

    member x.Keyboard = keyboard :> IKeyboard
    member x.Mouse = mouse :> IMouse

    member x.RenderTask 
        with get() = match renderTask with | Some t -> t | _ -> Unchecked.defaultof<_>
        and set t = 
            renderTask <- Some t
            match impl with
                | Some i -> i.RenderTask <- t
                | None -> ()

    member x.Time = time

    interface IRenderControl with
        member x.Time = time
        member x.Sizes = x.Sizes

        member x.Keyboard = x.Keyboard
        member x.Mouse = x.Mouse

        member x.RenderTask 
            with get() = x.RenderTask
            and set t = x.RenderTask <- t
    