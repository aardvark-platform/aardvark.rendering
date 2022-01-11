namespace Aardvark.Application.Slim

open FSharp.Data.Adaptive
open System.Reflection
open Aardvark.Base
open Silk.NET.GLFW
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Aardvark.Base
open Microsoft.FSharp.NativeInterop
open Aardvark.Application
open Aardvark.Glfw

#nowarn "9"


module private Vulkan =
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

        let surface = Aardvark.Rendering.Vulkan.KHRSurface.VkSurfaceKHR(surf.Handle)
        let surf = new Aardvark.Rendering.Vulkan.Surface(runtime.Device, surface)

        let description =
            let graphicsMode = GraphicsMode(Col.Format.BGRA, 8, 24, 8, 2, cfg.samples, ImageTrafo.MirrorY, cfg.vsync)
            device.CreateSwapchainDescription(surf, graphicsMode)

        { new IWindowSurface with
            override __.Signature = 
                description.renderPass :> IFramebufferSignature
            override this.CreateSwapchain(size: V2i) = 
                let swap = device.CreateSwapchain(description)
                { new ISwapchain with
                    override this.Dispose() = 
                        swap.Dispose()
                    override this.Run(task : IRenderTask, query : IQuery)  = 
                        swap.RenderFrame (fun fbo ->
                            let output = OutputDescription.ofFramebuffer fbo
                            task.Run(AdaptiveToken.Top, RenderToken.Empty, output, query)
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
            override __.Boot(_) =
                ()

            override __.CreateSurface(runtime : IRuntime, cfg: WindowConfig, glfw: Glfw, win: nativeptr<WindowHandle>) = 
                createSurface (runtime :?> _) cfg glfw win

            override __.WindowHints(cfg: WindowConfig, glfw: Glfw) = 
                glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi)
        }

type VulkanApplication(userExt : list<string>, debug : option<DebugConfig>) =    
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

    static let getExtensions (user : list<string>) =
        let mutable r = surfaceExtensions
        for u in user do r <- Set.add u r
        r |> Set.toList


    let app = new HeadlessVulkanApplication(debug, getExtensions userExt, (fun _ -> []))
    let glfw = Application(app.Runtime, Vulkan.interop, false)

    let windowConfig =
        {
            WindowConfig.title = "Aardvark rocks \\o/"
            WindowConfig.width = 1024
            WindowConfig.height = 768
            WindowConfig.resizable = true
            WindowConfig.focus = true
            WindowConfig.vsync = true
            WindowConfig.opengl = true
            WindowConfig.physicalSize = false
            WindowConfig.transparent = false
            WindowConfig.samples = 1
        }
    
    new(userExt : list<string>, debug : bool) =
        new VulkanApplication(userExt, if debug then Some DebugConfig.Default else None)
    
    new(debug : bool) =
        new VulkanApplication([], if debug then Some DebugConfig.Default else None)
    
    new() =
        new VulkanApplication([], None)
    
    static member SetDeviceChooser(chooser : PhysicalDevice[] -> int) =
        Aardvark.Rendering.Vulkan.CustomDeviceChooser.Register(fun ds ->
            let ds = Seq.toArray ds
            ds.[chooser ds]
        )

    member x.Runtime = app.Runtime

    member x.Dispose() =
        // first dispose runtime in order to properly dispose resources..
        app.Dispose()

    member x.Initialize(ctrl : IRenderControl, samples : int) = 
        failwithf "unknown control type: %A" ctrl
        

    member x.CreateGameWindow(?samples : int) =
        let samples = defaultArg samples 1
        let w = glfw.CreateWindow { windowConfig with samples = samples }

        w.KeyDown.Add (fun e ->
            match e.Key with
            | Keys.R when e.Ctrl && e.Shift ->
                w.RenderAsFastAsPossible <- not w.RenderAsFastAsPossible
                Log.line "[Window] RenderAsFastAsPossible: %A" w.RenderAsFastAsPossible
            | Keys.V when e.Ctrl && e.Shift ->
                w.VSync <- not w.VSync
                Log.line "[Window] VSync: %A" w.VSync
            | Keys.G when e.Ctrl && e.Shift ->
                w.MeasureGpuTime <- not w.MeasureGpuTime
                Log.line "[Window] MeasureGpuTime: %A" w.MeasureGpuTime
            | Keys.Enter when e.Ctrl && e.Shift ->
                w.Fullcreen <- not w.Fullcreen
                Log.line "[Window] Fullcreen: %A" w.Fullcreen
            | _ ->
                ()
        )

        w

    interface IApplication with
        member x.Initialize(ctrl : IRenderControl, samples : int) = x.Initialize(ctrl, samples)
        member x.Runtime = x.Runtime :> IRuntime
        member x.Dispose() = x.Dispose()       