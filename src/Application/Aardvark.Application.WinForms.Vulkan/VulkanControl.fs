namespace Aardvark.Application.WinForms

open Aardvark.Application
open Aardvark.Application.WinForms
open System.Windows.Forms
open Aardvark.Rendering.Vulkan

type VulkanControl(context : Context, depthFormat : VkFormat, samples : int) as this =
    inherit UserControl()
    let queuePool = context.DefaultQueue

    do base.SetStyle(ControlStyles.UserPaint, true)
       base.SetStyle(ControlStyles.DoubleBuffer, false)
       base.SetStyle(ControlStyles.AllPaintingInWmPaint, true)
       base.SetStyle(ControlStyles.Opaque, true)
       
    let mutable samples = samples
    let mutable swapChainDescription : VulkanSwapChainDescription = Unchecked.defaultof<_>
    let mutable swapChain : IVulkanSwapChain = Unchecked.defaultof<_>
    let mutable renderPass : RenderPass = Unchecked.defaultof<_>
    let mutable loaded = false
    let mutable recreateSwapChain = true

    member x.UpdateSamples(newSamples : int) =

        if loaded && samples <> newSamples then
            swapChainDescription.Dispose()
            swapChain.Dispose()
            swapChainDescription <- this.CreateVulkanSwapChainDescription(context, depthFormat, newSamples)
            renderPass <- swapChainDescription.RenderPass
            recreateSwapChain <- true

        samples <- newSamples


    member x.SwapChainDescription = 
        if not x.IsHandleCreated then x.CreateHandle()
        swapChainDescription

    member x.RenderPass = 
        if not x.IsHandleCreated then x.CreateHandle()
        swapChainDescription.RenderPass


    abstract member OnRenderFrame : RenderPass * Framebuffer -> unit
    default x.OnRenderFrame(_,_) = ()


    override x.OnResize(e) =
        base.OnResize e
        if loaded then
            if not recreateSwapChain then
                swapChain.Dispose()
            recreateSwapChain <- true
        
    override x.OnHandleCreated(e) =
        base.OnHandleCreated e
        swapChainDescription <- this.CreateVulkanSwapChainDescription(context, depthFormat, samples)
        renderPass <- swapChainDescription.RenderPass
        
        recreateSwapChain <- true
        loaded <- true

    override x.OnPaint(e) =
        base.OnPaint(e)

        if loaded then
            if recreateSwapChain then
                recreateSwapChain <- false
                swapChain <- x.CreateVulkanSwapChain(swapChainDescription)

            let queue = queuePool.Acquire()
            try
                swapChain.BeginFrame queue

                let fbo = swapChain.Framebuffer
                Aardvark.Base.Incremental.EvaluationUtilities.evaluateTopLevel (fun () ->
                    x.OnRenderFrame(renderPass, fbo)
                )

                swapChain.EndFrame queue
            finally
                queuePool.Release(queue)

    override x.Dispose(d) =
        base.Dispose(d)

        if loaded then
            loaded <- false
            swapChain.Dispose()
            swapChainDescription.Dispose()


