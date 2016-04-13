namespace Aardvark.Application.WinForms

open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Windows.Forms
open Aardvark.Rendering.Vulkan

type VulkanGameWindowWin32(r : Runtime) =
    inherit Form()

    let ctrl = new VulkanRenderControl(r, 1, Dock = DockStyle.Fill)
    let keyboard = new Keyboard()
    let mouse = new Mouse()

    do base.Controls.Add ctrl
       base.SetStyle(ControlStyles.AllPaintingInWmPaint, true)
       keyboard.SetControl(ctrl)
       mouse.SetControl(ctrl)

    member x.Keyboard = keyboard :> IKeyboard
    member x.Mouse = mouse :> IMouse
    member x.FramebufferSignature = ctrl.RenderPass :> IFramebufferSignature
    member x.Runtime = r :> IRuntime
    member x.RenderTask
        with get() = ctrl.RenderTask
        and set t = ctrl.RenderTask <- t

    member x.Samples = 1
    member x.Sizes = ctrl.Sizes
    member x.Time = ctrl.Time

    override x.OnPaint(e) =
        base.OnPaint(e)
        ctrl.Invalidate()

    interface IRenderTarget with
        member x.FramebufferSignature = ctrl.RenderPass :> IFramebufferSignature
        member x.Runtime = r :> IRuntime
        member x.RenderTask
            with get() = ctrl.RenderTask
            and set t = ctrl.RenderTask <- t

        member x.Samples = 1
        member x.Sizes = ctrl.Sizes
        member x.Time = ctrl.Time

    interface IRenderControl with
        member x.Keyboard = keyboard :> IKeyboard
        member x.Mouse = mouse :> IMouse

    interface IVulkanGameWindow with
        member x.Run() = x.Run()

    member x.Run() =
        Application.Run(x)












