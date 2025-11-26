namespace Aardvark.Application.Slim

open FSharp.Data.Adaptive
open Aardvark.Base
open Silk.NET.GLFW
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open KHRSurface
open Microsoft.FSharp.NativeInterop
open Aardvark.Application
open Aardvark.Glfw
open System
open System.Runtime.InteropServices

#nowarn "9"

module private Vulkan =
    let getSupportedSamples (runtime : Runtime) =
        let limits = runtime.Device.PhysicalDevice.Limits.Framebuffer

        Set.intersectMany [
            limits.ColorSampleCounts
            limits.DepthSampleCounts
            limits.StencilSampleCounts
        ]

    let createSurface (runtime : Runtime) (cfg: WindowConfig) (glfw : Glfw) (win : nativeptr<WindowHandle>) =
        let device = runtime.Device
        use pSurf = fixed [| Unchecked.defaultof<_> |]
        let instanceHandle = Silk.NET.Core.Native.VkHandle(runtime.Device.Instance.Handle)
        let ret = glfw.CreateWindowSurface(instanceHandle, win, NativePtr.toVoidPtr NativePtr.zero<byte>, pSurf)
        let mutable desc = NativePtr.zero
        let code = glfw.GetError(&desc)
        if code <> ErrorCode.NoError then
            let bytes =
                let res = System.Collections.Generic.List()
                let mutable p = desc
                let mutable c = NativePtr.read p
                while c <> 0uy do
                    res.Add (char c)
                    p <- NativePtr.add p 1
                    c <- NativePtr.read p
                res.ToArray()

            Log.warn "%A: %s" code (System.String(bytes))

        let surf = NativePtr.read pSurf

        let surface = VkSurfaceKHR(surf.Handle)
        let surf = new Aardvark.Rendering.Vulkan.Surface(runtime.Device, surface)

        let samples =
            let supported = getSupportedSamples runtime

            if supported |> Set.contains cfg.samples then
                cfg.samples
            else
                Set.maxElement supported

        let description =
            let graphicsMode = GraphicsMode(Col.Format.BGRA, 8, 24, 8, 2, samples, ImageTrafo.MirrorY, cfg.vsync)
            device.CreateSwapchainDescription(surf, graphicsMode)

        { new IWindowSurface with
            override _.Signature =
                description.renderPass :> IFramebufferSignature
            override this.CreateSwapchain(size: V2i) = 
                let swap = device.CreateSwapchain(size, description)
                { new ISwapchain with
                    override this.Dispose() = 
                        swap.Dispose()
                    override this.Run(task : IRenderTask, queries : IQuery list)  = 
                        swap.RenderFrame (fun fbo ->
                            let output = OutputDescription.ofFramebuffer fbo
                            let rt = { RenderToken.Empty with Queries = queries }
                            task.Run(AdaptiveToken.Top, rt, output)
                        )
                    override x.Size = size
                }
            override this.Dispose() = 
                description.Dispose()
                surf.Dispose()

            override this.Handle = 
                surf :> obj
        }

    let interop =
        { new IWindowInterop with
            override _.Boot(_) =
                ()

            override _.CreateSurface(runtime : IRuntime, cfg: WindowConfig, glfw: Glfw, win: nativeptr<WindowHandle>) =
                createSurface (runtime :?> _) cfg glfw win

            override _.WindowHints(cfg: WindowConfig, glfw: Glfw) =
                glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi)

            override _.Dispose() =
                ()
        }

type VulkanApplication private (app : HeadlessVulkanApplication, hideCocoaMenuBar : bool) =
    inherit Application(app.Runtime, Vulkan.interop, hideCocoaMenuBar)

    static let surfaceExtensions =
        let all = Instance.GlobalExtensions
        all
        |> Array.choose (fun e ->
            match e.name with
            | "VK_KHR_surface"
            | "VK_KHR_swapchain"
            | "VK_MVK_moltenvk"
            | "VK_EXT_swapchain_colorspace" ->
                Some e.name
            | _ ->
                if e.name.EndsWith "_surface" then Some e.name
                else None
        )
        |> Set.ofArray
        |> Set.add "VK_KHR_swapchain"

    static let getExtensions (user : string seq) =
        let mutable r = surfaceExtensions
        if user <> null then
            for u in user do r <- Set.add u r
        r |> Set.toList

    new(debug : IDebugConfig,
        [<Optional; DefaultParameterValue(null : string seq)>] extensions : string seq,
        [<Optional; DefaultParameterValue(null : Func<DeviceFeatures, DeviceFeatures>)>] deviceFeatures: Func<DeviceFeatures, DeviceFeatures>,
        [<Optional; DefaultParameterValue(null : IDeviceChooser)>] deviceChooser: IDeviceChooser,
        [<Optional; DefaultParameterValue(false)>] hideCocoaMenuBar : bool) =
        let app = new HeadlessVulkanApplication(debug, getExtensions extensions, (fun _ -> Seq.empty), deviceFeatures, deviceChooser)
        new VulkanApplication(app, hideCocoaMenuBar)

    new([<Optional; DefaultParameterValue(false)>] debug : bool,
        [<Optional; DefaultParameterValue(null : string seq)>] extensions : string seq,
        [<Optional; DefaultParameterValue(null : Func<DeviceFeatures, DeviceFeatures>)>] deviceFeatures: Func<DeviceFeatures, DeviceFeatures>,
        [<Optional; DefaultParameterValue(null : IDeviceChooser)>] deviceChooser: IDeviceChooser,
        [<Optional; DefaultParameterValue(false)>] hideCocoaMenuBar : bool) =
        new VulkanApplication(DebugLevel.ofBool debug, extensions, deviceFeatures, deviceChooser, hideCocoaMenuBar)

    member x.Runtime = app.Runtime

    member x.Initialize(ctrl : IRenderControl, samples : int) =
        failwithf "unknown control type: %A" ctrl

    override x.Destroy() =
        app.Dispose()

    interface IApplication with
        member x.Initialize(ctrl : IRenderControl, samples : int) = x.Initialize(ctrl, samples)
        member x.Runtime = x.Runtime :> IRuntime