namespace Aardvark.Application.WPF

open Aardvark.Rendering
open Aardvark.Rendering.GL
open System.Windows.Forms.Integration
open Aardvark.Application
open System.Windows.Threading

type private WinFormsControl = Aardvark.Application.WinForms.ThreadedRenderControl

type OpenGlRenderControl(runtime : Runtime, debug : IDebugConfig, samples : int) as this =
    inherit WindowsFormsHost()
    let ctrl = new WinFormsControl(runtime, debug, samples)
    
    let yieldApp () = 
        let t = Dispatcher.Yield(DispatcherPriority.ApplicationIdle)
        let aw = t.GetAwaiter()
        aw.GetResult()

//    do ctrl.DisableThreadStealing <- 
//        { new StopStealing with
//            member x.StopStealing () = 
//                yieldApp ()
//                let stop = Dispatcher.CurrentDispatcher.DisableProcessing() 
//                { new IDisposable with member x.Dispose() = stop.Dispose(); yieldApp () } 
//        }
//    do ctrl.AutoInvalidate <- false

    do this.Child <- ctrl
       this.Loaded.Add(fun e -> this.Focusable <- false)

    //override x.OnDpiChanged(oldDpi : DpiScale, newDpi : DpiScale) =
    //    base.OnDpiChanged(oldDpi, newDpi)

    member x.Inner = ctrl

    member x.RenderTask
        with get() = ctrl.RenderTask
        and set t = ctrl.RenderTask <- t

    member x.Sizes = ctrl.Sizes
    member x.Samples = ctrl.Samples
    member x.Context = ctrl.Context
    member x.WindowInfo = ctrl.WindowInfo
    member x.FramebufferSignature = ctrl.FramebufferSignature
    
    member x.ContextHandle = ctrl.ContextHandle

    member x.Time = ctrl.Time

    member x.SubSampling
        with get() = ctrl.SubSampling
        and set v = ctrl.SubSampling <- v

    interface IRenderTarget with
        member x.FramebufferSignature = ctrl.FramebufferSignature
        member x.Samples = ctrl.Samples
        member x.Runtime = runtime :> IRuntime
        member x.Time = ctrl.Time
        member x.RenderTask
            with get() = x.RenderTask
            and set t = x.RenderTask <- t
        member x.SubSampling 
            with get() = x.SubSampling
            and set v = x.SubSampling <- v
        member x.Sizes = x.Sizes
        member x.BeforeRender = ctrl.BeforeRender
        member x.AfterRender = ctrl.AfterRender
    new(context, enableDebug) = new OpenGlRenderControl(context, enableDebug, 1)
