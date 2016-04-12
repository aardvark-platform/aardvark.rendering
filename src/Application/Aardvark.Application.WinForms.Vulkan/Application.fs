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


type VulkanApplication(appName : string, debug : bool, chooseDevice : PhysicalDevice -> bool) =
    static let instanceDebugLayers = [ "VK_LAYER_LUNARG_api_dump"; Instance.Layers.DrawState; Instance.Layers.ParamChecker; Instance.Layers.Threading; Instance.Layers.SwapChain; Instance.Layers.MemTracker ]
    static let deviceDebugLayers = instanceDebugLayers

    let requestedExtensions =
        [
            yield Instance.Extensions.Surface
            yield Instance.Extensions.SwapChain
            yield Instance.Extensions.Win32Surface
            yield Instance.Extensions.XcbSurface
            yield Instance.Extensions.XlibSurface

            if debug then
                yield Instance.Extensions.DebugReport
        ]

    let requestedLayers =
        [
            yield Instance.Layers.SwapChain
            if debug then
                yield Instance.Layers.DrawState
                yield Instance.Layers.ParamChecker
                yield Instance.Layers.StandardValidation
        ]

    let instance = 
        let availableExtensions =
            Instance.AvailableExtensions |> Seq.map (fun e -> e.extensionName.Value) |> Set.ofSeq

        let availableLayers =
            Instance.AvailableLayers |> Seq.map (fun l -> l.layerName.Value) |> Set.ofSeq

        // create an instance
        let enabledExtensions = requestedExtensions |> List.filter (fun r -> Set.contains r availableExtensions)
        let enabledLayers = requestedLayers |> List.filter (fun r -> Set.contains r availableExtensions)
    
        new Instance(appName, Version(1,0,0), enabledLayers, enabledExtensions)

    // install debug output to file (and errors/warnings to console)
    do if debug then
        instance.OnDebugMessage.Add (fun msg ->
            Log.warn "%s" msg.message
        )


    // choose a physical device
    let physicalDevice = 
        match instance.PhysicalDevices |> List.tryFind chooseDevice with
            | Some device -> device
            | _ -> failwithf "[Vulkan] could not choose device (see info for available ones)"

    // create a device
    let device = 
        let availableExtensions =
            physicalDevice.Extensions |> Seq.map (fun e -> e.extensionName.Value) |> Set.ofSeq

        let availableLayers =
            physicalDevice.Layers |> Seq.map (fun l -> l.layerName.Value) |> Set.ofSeq

        let enabledExtensions = requestedExtensions |> List.filter (fun r -> Set.contains r availableExtensions)
        let enabledLayers = requestedLayers |> List.filter (fun r -> Set.contains r availableExtensions)
        
        instance.CreateDevice(physicalDevice, enabledLayers, enabledExtensions)

    let printInfo() =
        
        Log.start "VulkanApplication"

        do  Log.start "instance"

            do  Log.start "layers"
                for l in Instance.AvailableLayers do
                    let layerName = l.layerName.Value
                    let version = l.implementationVersion |> Version.FromUInt32
                    if instance.Layers |> Array.exists (fun li -> li = layerName) then
                        Log.line "* %s (v%A)" layerName version
                    else
                        Log.line "  %s (v%A)" layerName version
                Log.stop()

            do  Log.start "extensions"
                for e in Instance.AvailableExtensions do
                    let extName = e.extensionName.Value
                    let version = e.specVersion |> Version.FromUInt32
                    if instance.Extensions |> Array.exists (fun ei -> ei = extName) then
                        Log.line "* %s (v%A)" extName version
                    else
                        Log.line "  %s (v%A)" extName version
                Log.stop()

            Log.stop()

        do  Log.start "%A %s" physicalDevice.Vendor physicalDevice.Name

            Log.line "kind:    %A" physicalDevice.DeviceType
            Log.line "API:     v%A" (Version.FromUInt32(physicalDevice.Properties.apiVersion))
            Log.line "driver:  v%A" (Version.FromUInt32(physicalDevice.Properties.driverVersion))

            do  Log.start "memory"
                for m in physicalDevice.MemoryHeaps do
                    let suffix =
                        if m.IsDeviceLocal then " (device)"
                        else ""

                    Log.line "memory %d: %A%s" m.HeapIndex m.Size suffix

                Log.stop()

            do  Log.start "layers"
                for l in physicalDevice.Layers do
                    let layerName = l.layerName.Value
                    let version = l.implementationVersion |> Version.FromUInt32
                    if device.Layers |> List.exists (fun li -> li = layerName) then
                        Log.line "* %s (v%A)" layerName version
                    else
                        Log.line "  %s (v%A)" layerName version
                Log.stop()

            do  Log.start "extensions"
                for e in physicalDevice.Extensions do
                    let extName = e.extensionName.Value
                    let version = e.specVersion |> Version.FromUInt32
                    if device.Extensions |> List.exists (fun ei -> ei = extName) then
                        Log.line "* %s (v%A)" extName version
                    else
                        Log.line "  %s (v%A)" extName version
                Log.stop()
            
            Log.stop()

        Log.stop()  


    do printInfo()


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


    new(appName, debug) = new VulkanApplication(appName, debug, fun _ -> true)
    new(appName) = new VulkanApplication(appName, false)
    new(debug) = new VulkanApplication("Aardvark", debug)
    new() = new VulkanApplication("Aardvark", false)