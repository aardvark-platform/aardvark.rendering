namespace Aardvark.Application.WinForms

open System
open Aardvark.Application
open Aardvark.Rendering.Vulkan
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators

type VulkanRenderControl(runtime : Runtime, samples : int) as this =
    inherit Aardvark.Application.WinForms.VulkanControl(runtime.Context, VkFormat.D32Sfloat, samples)

    let context = runtime.Context
    let mutable renderTask : IRenderTask = Unchecked.defaultof<_>
    let mutable sizes = Mod.init (V2i(this.ClientSize.Width, this.ClientSize.Height))

    let time = Mod.custom(fun _ -> DateTime.Now)

    override x.OnRenderFrame(pass, fbo) =
        let s = V2i(x.ClientSize.Width, x.ClientSize.Height)
        if s <> sizes.Value then
            transact (fun () -> Mod.change sizes s)

        renderTask.Run(fbo) |> ignore

        x.Invalidate()
        transact (fun () -> time.MarkOutdated())


    member x.Time = time
    member x.Sizes = sizes :> IMod<_>

    member x.Screenshot(size : V2i) =
        let desc = x.SwapChainDescription

        let color =
            context.CreateImage(
                VkImageType.D2d, desc.ColorFormat, V3i(size.X, size.Y, 1), 1, 1, 1, 
                VkImageUsageFlags.ColorAttachmentBit, VkImageLayout.TransferSrcOptimal, VkImageTiling.Optimal
            )

        let depth =
            context.CreateImage(
                VkImageType.D2d, desc.DepthFormat, V3i(size.X, size.Y, 1), 1, 1, 1, 
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

    member x.RenderTask
        with get() = renderTask
        and set t = renderTask <- t

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
    static let instanceDebugLayers = [ Instance.Layers.DrawState; Instance.Layers.ParamChecker ]
    static let deviceDebugLayers = [ Instance.Layers.DrawState ]

    // create an instance
    let enabledLayers = if debug then instanceDebugLayers else []
    let enabledExtensions = if debug then [Instance.Extensions.DebugReport] else []
    
    let instance = new Instance(appName, Version(1,0,0), enabledLayers, enabledExtensions)

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