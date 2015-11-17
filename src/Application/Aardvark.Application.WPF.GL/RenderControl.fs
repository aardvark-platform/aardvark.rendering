namespace Aardvark.Application.WPF

#if WINDOWS

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering.GL
open OpenTK.Graphics.OpenGL4
open System.Windows
open System.Windows.Controls
open System.Windows.Forms.Integration
open Aardvark.Application

type private WinFormsControl = Aardvark.Application.WinForms.OpenGlRenderControl

type OpenGlRenderControl(runtime : Runtime, samples : int) as this =
    inherit WindowsFormsHost()
    let ctrl = new WinFormsControl(runtime, samples)

    do this.Child <- ctrl
       this.Loaded.Add(fun e -> this.Focusable <- false)

    member x.Inner = ctrl

    member x.RenderTask
        with get() = ctrl.RenderTask
        and set t = ctrl.RenderTask <- t

    member x.Sizes = ctrl.Sizes
    member x.Samples = ctrl.Samples
    member x.Context = ctrl.Context
    member x.WindowInfo = ctrl.WindowInfo
    member x.FramebufferSignature = ctrl.FramebufferSignature

    member x.Time = ctrl.Time
    interface IRenderTarget with
        member x.FramebufferSignature = ctrl.FramebufferSignature
        member x.Samples = ctrl.Samples
        member x.Runtime = runtime :> IRuntime
        member x.Time = ctrl.Time
        member x.RenderTask
            with get() = x.RenderTask
            and set t = x.RenderTask <- t

        member x.Sizes = x.Sizes

    new(context) = new OpenGlRenderControl(context, 1)

#endif
