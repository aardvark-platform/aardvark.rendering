namespace Aardvark.Application.WinForms

open System
open System.Windows.Forms
open System.Drawing
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Application


type RenderControl() as this =
    inherit Control()

    let mutable subsampling = 1.0
    let mutable renderTask : Option<IRenderTask> = None
    let mutable impl : Option<IRenderTarget> = None
    let mutable ctrl : Option<Control> = None

    let keyboard = new Keyboard()
    let mouse = new Mouse()
    let sizes = AVal.init (V2i.II)
    let focus = AVal.init false
    let mutable inner : Option<aval<DateTime>> = None

    let beforeRender = Event<unit>()
    let afterRender = Event<unit>()

    let onGotFocus (sender : obj) (e : EventArgs) =
        transact(fun () -> focus.Value <- true)

    let onLostFocus (sender : obj) (e : EventArgs) =
        transact(fun () -> focus.Value <- false)

    let time = 
        AVal.custom (fun s -> 
            match inner with
                | Some m -> m.GetValue s
                | None -> DateTime.Now
           )

    let gotFocusHandler = EventHandler onGotFocus
    let lostFocusHandler = EventHandler onLostFocus

    let setControl (self : RenderControl) (c : Control) (cr : IRenderTarget) =
        self.SuspendLayout()
        match impl with
            | Some i -> 
                c.GotFocus.RemoveHandler gotFocusHandler
                c.LostFocus.RemoveHandler lostFocusHandler
                i.Time.Outputs.Remove time |> ignore
            | None -> 
                ()

        self.Controls.Clear()
        c.Dock <- DockStyle.Fill
        self.Controls.Add c

        match cr with
            | :? IRenderControl as cr -> 
                keyboard.Use cr.Keyboard |> ignore
                mouse.Use cr.Mouse |> ignore

            | _ ->
                keyboard.SetControl c
                mouse.SetControl c


        match renderTask with
            | Some task -> cr.RenderTask <- task
            | None -> ()

        cr.SubSampling <- subsampling

        transact(fun () ->
            inner <- Some cr.Time
            focus.Value <- c.Focused
            cr.Time.Outputs.Add time |> ignore
        )
        ctrl <- Some c
        impl <- Some cr
        
        cr.BeforeRender.Add beforeRender.Trigger
        cr.AfterRender.Add afterRender.Trigger

        c.GotFocus.AddHandler gotFocusHandler
        c.LostFocus.AddHandler lostFocusHandler
        self.ResumeLayout()

    static let rec subscribeToLocationChange (ctrl : Control) (action : EventHandler) : IDisposable =
        if ctrl <> null then
            let inner = ref <| subscribeToLocationChange ctrl.Parent action
            
            let parentChange = EventHandler (fun s e ->
                inner.Value.Dispose()
                inner := subscribeToLocationChange ctrl.Parent action
                action.Invoke(s, e)
            )
            ctrl.LocationChanged.AddHandler parentChange
            ctrl.ParentChanged.AddHandler action
            
            { new IDisposable with 
                member x.Dispose() =
                    inner.Value.Dispose()
                    ctrl.LocationChanged.RemoveHandler action
                    ctrl.ParentChanged.RemoveHandler parentChange
            }
        else
            { new IDisposable with member x.Dispose() = () }

    static let getScreenLocation (ctrl : Control) =
        let currentPos() =
            let point = ctrl.PointToScreen(Point(0,0))
            V2i(point.X, point.Y)

        let res = AVal.init (currentPos())

        let update (s : obj) (e : EventArgs) =
            transact (fun () ->
                res.Value <- currentPos()
            )

        let d = subscribeToLocationChange ctrl (EventHandler update)
        d, res :> aval<_>

    let locationAndSub = lazy ( getScreenLocation this )

    member x.Location =
        locationAndSub.Value |> snd


    member x.Implementation
        with get() = match ctrl with | Some c -> c | _ -> null
        and set v = setControl x v (v |> unbox<IRenderTarget>)

    override x.OnHandleCreated(e) =
        base.OnHandleCreated(e)
        transact (fun () -> sizes.Value <- V2i(this.ClientSize.Width, this.ClientSize.Height))

    override x.OnResize(e) =
        base.OnResize(e)
        transact (fun () -> sizes.Value <- (V2i(x.ClientSize.Width, x.ClientSize.Height)))
    
    //override x.OnDpiChangedBeforeParent(e) =
    //    Log.warn "asdasdasdasd"
    //    base.OnDpiChangedBeforeParent(e)

    member x.Sizes = sizes :> aval<V2i>
    member x.Samples = impl.Value.Samples
    member x.Keyboard = keyboard :> IKeyboard
    member x.Mouse = mouse :> IMouse

    member x.RenderTask 
        with get() = match renderTask with | Some t -> t | _ -> Unchecked.defaultof<_>
        and set t = 
            renderTask <- Some t
            match impl with
                | Some i -> i.RenderTask <- t
                | None -> ()

    member x.FramebufferSignature = impl.Value.FramebufferSignature
    member x.Runtime = impl.Value.Runtime
    member x.Time = time
    member x.Focus = focus :> aval<_>
    
    member x.SubSampling 
        with get() = subsampling
        and set v =
            subsampling <- v
            match impl with
            | Some i -> i.SubSampling <- v
            | None -> ()
            

    [<CLIEvent>]
    member x.BeforeRender = beforeRender.Publish
    [<CLIEvent>]
    member x.AfterRender = afterRender.Publish

    interface IRenderControl with
        
        member x.SubSampling
            with get() = x.SubSampling
            and set v = x.SubSampling <- v

        member x.FramebufferSignature = impl.Value.FramebufferSignature
        member x.Runtime = impl.Value.Runtime
        member x.Time = time
        member x.Sizes = x.Sizes
        member x.Samples = impl.Value.Samples

        member x.Keyboard = x.Keyboard
        member x.Mouse = x.Mouse

        member x.BeforeRender = beforeRender.Publish
        member x.AfterRender = afterRender.Publish

        member x.RenderTask 
            with get() = x.RenderTask
            and set t = x.RenderTask <- t
    