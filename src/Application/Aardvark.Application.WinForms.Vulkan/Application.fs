namespace Aardvark.Application.WinForms

open System
open Aardvark.Application
open Aardvark.Rendering.Vulkan
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators

type VulkanRenderControl(runtime : Runtime, samples : int) as this =
    inherit Aardvark.Application.WinForms.VulkanControl(runtime.Context, VkFormat.D32Sfloat, samples)
    
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

type VulkanApplication(appName : string, debug : bool) =
    static let instanceDebugLayers = [ "VK_LAYER_LUNARG_api_dump"; Instance.Layers.DrawState; Instance.Layers.ParamChecker; Instance.Layers.Threading; Instance.Layers.SwapChain; Instance.Layers.MemTracker ]
    static let deviceDebugLayers = instanceDebugLayers




    // create an instance
    let enabledLayers = if debug then instanceDebugLayers else []
    let enabledExtensions = if debug then [Instance.Extensions.DebugReport] else []
    
    let instance = new Instance(appName, Version(1,0,0), enabledLayers, enabledExtensions)

    do
        Log.start "Vulkan info"
        let mutable index = 0
        for d in instance.PhysicalDevices do
            Log.line "device %d: %A %s" index d.Vendor d.Name
            index <- index + 1

        Log.start "layers"
        for l in Instance.AvailableLayers do
            Log.line "%s" l.layerName.Value
        Log.stop()

        Log.start "extensions"
        for l in Instance.AvailableExtensions do
            Log.line "%s" l.extensionName.Value
        Log.stop()

        Log.stop()


    // install debug output to file (and errors/warnings to console)
    do if debug then
        Log.warn "[Vulkan] debug support not implemented"
        instance.OnDebugMessage.Add (fun msg ->
            Log.warn "%s" msg.message
        )
        //instance.InstallDebugFileWriter "vk.log"

    // choose a physical device
    let physicalDevice = instance.PhysicalDevices |> List.head

    // create a device
    let enabledDeviceLayers = if debug then deviceDebugLayers else []
    let enabledDeviceExtensions = [ ]
    let device = instance.CreateDevice(physicalDevice, enabledDeviceLayers, enabledDeviceExtensions)

    // create a runtime
    let runtime = new Runtime(device)


    member x.Runtime = runtime

    member x.Initialize(ctrl : IRenderControl, samples : int) =
        match ctrl with
            | :? Aardvark.Application.WinForms.RenderControl as ctrl ->
                let impl = new VulkanRenderControl(runtime, samples)
                ctrl.Implementation <- impl

            | _ ->
                failwith "unsupported RenderControl"

    member x.GetRenderPass(ctrl : IRenderControl) =
        match ctrl with
            | :? Aardvark.Application.WinForms.RenderControl as ctrl ->
                match ctrl.Implementation with
                    | :? VulkanRenderControl as ctrl -> ctrl.RenderPass
                    | _ -> failwith "unsupported RenderControl"

            | _ ->
                failwith "unsupported RenderControl"


    interface IApplication with
        member x.Runtime = runtime :> _ 
        member x.Initialize(ctrl : IRenderControl, samples : int) =
            x.Initialize(ctrl, samples)

        member x.Dispose() =
            device.Dispose()
            instance.Dispose()


    new(appName) = new VulkanApplication(appName, false)
    new() = new VulkanApplication("Aardvark", false)