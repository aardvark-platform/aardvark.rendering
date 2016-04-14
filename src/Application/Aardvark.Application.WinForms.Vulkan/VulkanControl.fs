namespace Aardvark.Application.WinForms

open System
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Windows.Forms
open Aardvark.Rendering.Vulkan
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering

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

type VulkanRenderControl(runtime : Runtime, samples : int) as this =
    inherit VulkanControl(runtime.Context, VkFormat.D24UnormS8Uint, samples)
    
    static let messageLoop = MessageLoop()
    static do messageLoop.Start()

    let context = runtime.Context
    let mutable renderTask : IRenderTask = Unchecked.defaultof<_>
    let mutable taskSubscription : IDisposable = null
    let mutable sizes = Mod.init (V2i(this.ClientSize.Width, this.ClientSize.Height))
    let mutable needsRedraw = false

    let time = Mod.custom(fun _ -> DateTime.Now)

    override x.OnRenderFrame(pass, fbo) =
        needsRedraw <- false
        let s = V2i(x.ClientSize.Width, x.ClientSize.Height)
        if s <> sizes.Value then
            transact (fun () -> Mod.change sizes s)

        renderTask.Run(fbo) |> ignore

        //x.Invalidate()
        transact (fun () -> time.MarkOutdated())


    member x.Time = time
    member x.Sizes = sizes :> IMod<_>

    member x.Screenshot(size : V2i) =
        let desc = x.SwapChainDescription

        let color =
            context.CreateImage(
                VkImageType.D2d, desc.ColorFormat, TextureDimension.Texture2D, V3i(size.X, size.Y, 1), 1, 1, 1, 
                VkImageUsageFlags.ColorAttachmentBit, VkImageLayout.TransferSrcOptimal, VkImageTiling.Optimal
            )

        let depth =
            context.CreateImage(
                VkImageType.D2d, desc.DepthFormat, TextureDimension.Texture2D, V3i(size.X, size.Y, 1), 1, 1, 1, 
                VkImageUsageFlags.DepthStencilAttachmentBit, VkImageLayout.DepthStencilAttachmentOptimal, VkImageTiling.Optimal
            )

        let colorView = context.CreateImageOutputView(color, 0, 0)
        let depthView = context.CreateImageOutputView(depth, 0, 0)

        let fbo = 
            context.CreateFramebuffer(
                x.RenderPass, 
                [ colorView; depthView ]
            )

        renderTask.Run(fbo) |> ignore

        let image = PixImage<byte>(Col.Format.BGRA, size)
        color.Download(image) |> context.DefaultQueue.RunSynchronously
        
        context.Delete fbo
        context.Delete depthView
        context.Delete colorView
        context.Delete color
        context.Delete depth

        image

    member x.FramebufferSignature = x.RenderPass :> IFramebufferSignature

    member private x.ForceRedraw() =
        messageLoop.Draw(x)

    member x.RenderTask
        with get() = renderTask
        and set t = 
            if not (isNull taskSubscription) then 
                taskSubscription.Dispose()
                renderTask.Dispose()

            renderTask <- t
            taskSubscription <- t.AddMarkingCallback x.ForceRedraw

    interface IControl with
        member x.IsInvalid = needsRedraw
        member x.Invalidate() =
            if not needsRedraw then
                needsRedraw <- true
                x.Invalidate()

        member x.Paint() =
            use g = x.CreateGraphics()
            use e = new System.Windows.Forms.PaintEventArgs(g, x.ClientRectangle)
            x.InvokePaint(x, e)

        member x.Invoke f =
            base.Invoke (new System.Action(f)) |> ignore

    interface IRenderTarget with
        member x.FramebufferSignature = x.RenderPass :> IFramebufferSignature
        member x.Runtime = runtime :> IRuntime
        member x.RenderTask
            with get() = x.RenderTask
            and set t = x.RenderTask <- unbox t

        member x.Samples = 1
        member x.Sizes = sizes :> IMod<_>
        member x.Time = time


