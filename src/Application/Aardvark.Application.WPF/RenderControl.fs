namespace Aardvark.Application.WPF

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Media
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Application
open System.Windows.Forms.Integration

type RenderControl() as self =
    inherit Border()

    let mutable runtime : IRuntime = Unchecked.defaultof<IRuntime>
    let mutable renderTask : Option<IRenderTask> = None
    let mutable subsampling = 1.0
    let mutable impl : Option<IRenderTarget> = None
    let mutable ctrl : Option<FrameworkElement> = None

    let keyboard = new Aardvark.Application.WinForms.Keyboard()
    let mouse = new Aardvark.Application.WinForms.Mouse()
    let sizes = AVal.init (V2i(base.ActualWidth, base.ActualHeight))
    
    let beforeRender = Event<unit>()
    let afterRender = Event<unit>()


    let actualSize = 
        adaptive {
            let! s = sizes
            match impl with
             | Some v -> 
                return! v.Sizes
             | None -> return s
        }

    let mutable inner : Option<aval<DateTime>> = None
    let time = 
        AVal.custom (fun s -> 
            match inner with
                | Some m -> m.GetValue s
                | None -> DateTime.Now
           )

    let dragHandler = DragEventHandler(fun o a -> 
                            let pos = a.GetPosition(self)
                            mouse.DragMouse (int pos.X) (int pos.Y)
                        )
                                   
    let setupDragAndDrop() =
        self.DragOver.AddHandler dragHandler
        self.Drop.AddHandler dragHandler
                                   
    let disposeDragAndDrop() =
        self.DragOver.RemoveHandler dragHandler
        self.Drop.RemoveHandler dragHandler

    member x.SetControl (self : RenderControl) (c : FrameworkElement) (cr : IRenderTarget) =
        match impl with
            | Some i -> failwith "implementation can only be set once per control"
            | None -> ()

        self.Child <- c
        runtime <- cr.Runtime

        match c :> obj with
            | :? WindowsFormsHost as host ->
                keyboard.SetControl(host.Child)
                mouse.SetControl(host.Child)
                setupDragAndDrop()
            | :? IRenderControl as c ->
                keyboard.Use(c.Keyboard) |> ignore
                mouse.Use(c.Mouse) |> ignore
            | _ ->
                failwith "impossible to create WPF mouse"

        match renderTask with
            | Some task -> cr.RenderTask <- task
            | None -> ()
        cr.SubSampling <- subsampling
            
        cr.BeforeRender.Add beforeRender.Trigger
        cr.AfterRender.Add afterRender.Trigger

        ctrl <- Some c
        impl <- Some cr
        transact(fun () ->
            cr.Time.Outputs.Add time |> ignore
            inner <- Some cr.Time

            sizes.Value <- V2i.OO
        )

    member x.FocusReal() =
        match ctrl with
        | Some c -> c.Focus()
        | None -> x.Focus()

    override x.OnRenderSizeChanged(e) =
        base.OnRenderSizeChanged(e)
        transact (fun () -> sizes.Value <- (V2i(x.ActualWidth, x.ActualHeight)))

    override x.HitTestCore (hitTestParameters : PointHitTestParameters) =
        PointHitTestResult(x, hitTestParameters.HitPoint) :> HitTestResult

    member x.Implementation
        with get() = match ctrl with | Some c -> c | _ -> null
        and set v = x.SetControl x v (v |> unbox<IRenderTarget>)

    member x.Runtime = runtime
    member x.FramebufferSignature = impl.Value.FramebufferSignature
    member x.Sizes = actualSize 
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

    member x.SubSampling
        with get() = subsampling
        and set v =
            subsampling <- v
            match impl with
            | Some i -> i.SubSampling <- v
            | None -> ()

    member x.Time = time


    member x.BeforeRender = beforeRender.Publish
    member x.AfterRender = afterRender.Publish

    interface IRenderControl with
        member x.FramebufferSignature = x.FramebufferSignature
        member x.Runtime = x.Runtime
        member x.Sizes = x.Sizes
        member x.Samples = impl.Value.Samples
        member x.Time = time
        member x.Keyboard = x.Keyboard
        member x.Mouse = x.Mouse
        
        member x.BeforeRender = beforeRender.Publish
        member x.AfterRender = afterRender.Publish
        member x.RenderTask 
            with get() = x.RenderTask
            and set t = x.RenderTask <- t

        member x.SubSampling
            with get() = x.SubSampling
            and set v = x.SubSampling <- v