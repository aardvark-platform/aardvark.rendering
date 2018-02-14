namespace Aardvark.Application.WinForms

open System
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Windows.Forms
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Aardvark.Application.WinForms.Vulkan


[<AbstractClass>]
type VulkanControl(device : Device, graphicsMode : AbstractGraphicsMode) =
    inherit UserControl()

    let mutable surface : Surface = Unchecked.defaultof<_>
    let mutable swapchainDescription : SwapchainDescription = Unchecked.defaultof<_>
    let mutable swapchain : Swapchain = Unchecked.defaultof<_>
    let mutable loaded = false
    let mutable isInvalid = true

    member x.SwapChainDescription = 
        if not x.IsHandleCreated then x.CreateHandle()
        swapchainDescription

    member x.RenderPass = 
        if not x.IsHandleCreated then x.CreateHandle()
        swapchainDescription.renderPass

    abstract member OnLoad : SwapchainDescription -> unit
    abstract member OnUnload : unit -> unit
    abstract member OnRenderFrame : RenderPass * Framebuffer -> bool
    
    member x.IsInvalid = isInvalid

    interface IInvalidateControl with
        member x.IsInvalid = x.IsInvalid

    override x.OnHandleCreated(e) =
        base.OnHandleCreated e

        x.SetStyle(ControlStyles.UserPaint, true)
        x.SetStyle(ControlStyles.DoubleBuffer, false)
        x.SetStyle(ControlStyles.AllPaintingInWmPaint, true)
        x.SetStyle(ControlStyles.Opaque, true)
        x.SetStyle(ControlStyles.ResizeRedraw, true)
        x.Padding <- Padding(0,0,0,0)
        x.Margin <- Padding(0,0,0,0)
        x.BorderStyle <- BorderStyle.None
        


        surface <- device.CreateSurface(x)
        swapchainDescription <- device.CreateSwapchainDescription(surface, graphicsMode)
        swapchain <- device.CreateSwapchain(swapchainDescription)
        x.OnLoad swapchainDescription
        loaded <- true

    override x.OnPaint(e) =
        isInvalid <- true
        base.OnPaint(e)

        if loaded then
            let invalidate = 
                swapchain.RenderFrame(fun framebuffer ->
                    Aardvark.Base.Incremental.EvaluationUtilities.evaluateTopLevel (fun () ->
                        x.OnRenderFrame(swapchainDescription.renderPass, framebuffer)
                    )
                )
            isInvalid <- invalidate
            if invalidate then 
                x.Invalidate()

    override x.Dispose(d) =
        if loaded then
            loaded <- false
            x.OnUnload()
            device.Delete swapchain
            device.Delete swapchainDescription
            device.Delete surface

        base.Dispose(d)

type VulkanRenderControl(runtime : Runtime, graphicsMode : AbstractGraphicsMode) as this =
    inherit VulkanControl(runtime.Device, graphicsMode)
    
//    static let messageLoop = MessageLoop()
//    static do messageLoop.Start()

    let mutable renderTask : IRenderTask = RenderTask.empty
    let mutable taskSubscription : IDisposable = null
    let mutable sizes = Mod.init (V2i.II)
    let mutable needsRedraw = false
    let mutable renderContiuously = false

    let frameTime = RunningMean(10)
    let frameWatch = System.Diagnostics.Stopwatch()

    let timeWatch = System.Diagnostics.Stopwatch()
    let baseTime = DateTime.Now.Ticks
    do timeWatch.Start()



    let now() = DateTime(timeWatch.Elapsed.Ticks + baseTime)
    let nextFrameTime() = 
        if frameTime.Count >= 10 then
            now() + TimeSpan.FromSeconds frameTime.Average
        else
            now()

//    do Async.Start <| 
//        async {
//            while true do
//                do! Async.Sleep 500
//                Log.line "frame-time: %.2fms" (1000.0 * frameTime.Average)
//        }

    let time = Mod.init (now()) //Mod.custom(fun _ -> now())

    let beforeRender = Event<unit>()
    let afterRender = Event<unit>()
    let mutable first = true

    override x.OnLoad(desc : SwapchainDescription) =
        transact (fun () -> sizes.Value <- V2i(this.ClientSize.Width, this.ClientSize.Height))


        x.KeyDown.Add(fun e ->
            if e.KeyCode = System.Windows.Forms.Keys.End && e.Control then
                renderContiuously <- not renderContiuously
                if renderContiuously then
                    x.Invalidate()
                else
                    MessageLoop.Invalidate x |> ignore
                e.Handled <- true
        )

    override x.OnRenderFrame(pass, fbo) =
        needsRedraw <- false
        let s = V2i(x.ClientSize.Width, x.ClientSize.Height)
        if s <> sizes.Value then
            transact (fun () -> Mod.change sizes s)


        frameWatch.Restart()
        transact (fun () -> time.Value <- nextFrameTime())
        beforeRender.Trigger()
        renderTask.Run(RenderToken.Empty, fbo)
        afterRender.Trigger()
        frameWatch.Stop()

        if not first then
            frameTime.Add frameWatch.Elapsed.TotalSeconds

        //x.Invalidate()
        transact (fun () -> time.MarkOutdated())

        first <- false
        renderContiuously

    override x.OnUnload() =
        if not (isNull taskSubscription) then
            taskSubscription.Dispose()
            taskSubscription <- null

        renderTask.Dispose()
        renderTask <- RenderTask.empty
        ()

    member x.Time = time
    member x.Sizes = sizes :> IMod<_>

    member x.FramebufferSignature = x.RenderPass :> IFramebufferSignature


    member private x.ForceRedraw() =
        if not renderContiuously then
            MessageLoop.Invalidate x |> ignore
        //messageLoop.Draw(x)

    member x.RenderTask
        with get() = renderTask
        and set t = 
            if not (isNull taskSubscription) then 
                taskSubscription.Dispose()
                renderTask.Dispose()

            renderTask <- t
            taskSubscription <- t.AddMarkingCallback x.ForceRedraw

    member x.BeforeRender = beforeRender.Publish
    member x.AfterRender = afterRender.Publish
    interface IRenderTarget with
        member x.FramebufferSignature = x.RenderPass :> IFramebufferSignature
        member x.Runtime = runtime :> IRuntime
        member x.RenderTask
            with get() = x.RenderTask
            and set t = x.RenderTask <- unbox t

        member x.Samples = 1
        member x.Sizes = sizes :> IMod<_>
        member x.Time = time :> IMod<_>
        member x.BeforeRender = beforeRender.Publish
        member x.AfterRender = afterRender.Publish


