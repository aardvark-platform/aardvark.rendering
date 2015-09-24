namespace Aardvark.Application.WPF

#if WINDOWS

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Media
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Application
open System.Windows.Forms.Integration

type RenderControl() =
    inherit ContentControl()

    let mutable runtime : IRuntime = Unchecked.defaultof<IRuntime>
    let mutable renderTask : Option<IRenderTask> = None
    let mutable impl : Option<IRenderTarget> = None
    let mutable ctrl : Option<FrameworkElement> = None

    let keyboard = new Aardvark.Application.WinForms.Keyboard()
    let mouse = new Aardvark.Application.WinForms.Mouse()
    let sizes = Mod.init (V2i(base.ActualWidth, base.ActualHeight))

    let actualSize = 
        adaptive {
            let! s = sizes
            match impl with
             | Some v -> 
                printfn "switched to impl"
                return! v.Sizes
             | None -> return s
        }

    let mutable inner : Option<IMod<DateTime>> = None
    let time = 
        Mod.custom (fun () -> 
            match inner with
                | Some m -> m.GetValue()
                | None -> DateTime.Now
           )

    member x.SetControl (self : RenderControl) (c : FrameworkElement) (cr : IRenderTarget) =
        match impl with
            | Some i -> failwith "implementation can only be set once per control"
            | None -> ()

        self.Content <- c
        runtime <- cr.Runtime

        match c with
            | :? WindowsFormsHost as host ->
                keyboard.SetControl(host.Child)
                mouse.SetControl(host.Child)
                
            | _ ->
                failwith "impossible to create WPF mouse"

        match renderTask with
            | Some task -> cr.RenderTask <- task
            | None -> ()


        ctrl <- Some c
        impl <- Some cr
        transact(fun () ->
            cr.Time.AddOutput(time)
            inner <- Some cr.Time

            Mod.change sizes V2i.OO
        )

    override x.OnRenderSizeChanged(e) =
        base.OnRenderSizeChanged(e)
        transact (fun () -> Mod.change sizes (V2i(x.ActualWidth, x.ActualHeight)))

    member x.Implementation
        with get() = match ctrl with | Some c -> c | _ -> null
        and set v = x.SetControl x v (v |> unbox<IRenderTarget>)

    member x.Runtime = runtime
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

    member x.Time = time
    interface IRenderControl with
        member x.Runtime = x.Runtime
        member x.Sizes = x.Sizes
        member x.Samples = impl.Value.Samples
        member x.Time = time
        member x.Keyboard = x.Keyboard
        member x.Mouse = x.Mouse

        member x.RenderTask 
            with get() = x.RenderTask
            and set t = x.RenderTask <- t

#endif
