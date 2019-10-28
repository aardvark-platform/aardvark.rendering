namespace Aardvark.Application.Slim

#nowarn "9"

open System
open System.IO
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Application
open Aardvark.Rendering.Vulkan

[<AutoOpen>]
module private VulkanHandles =
    open OpenTK
    open OpenTK.Platform
    open OpenTK.Platform.Windows
    open OpenTK.Platform.X11
    open OpenTK.Platform.Linux
    
    module SurfaceInfo =
        open System.Runtime.InteropServices
        open Microsoft.FSharp.NativeInterop

        module Win32 =
            open System.Runtime.InteropServices

            [<DllImport("kernel32.dll")>]
            extern nativeint GetModuleHandle(string lpModuleName)
            
        module private Xcb = 
            open System.Runtime.InteropServices

            [<DllImport("X11-xcb")>]
            extern nativeint XGetXCBConnection(nativeint xdisplay)


        let tryOfWindowInfo (i : IWindowInfo) =

            match i with    
                | :? WinWindowInfo as i ->
                    let hinstance = Win32.GetModuleHandle(null)
                    let info = { hinstance = hinstance; hwnd = i.Handle }
                    Some (SurfaceInfo.Win32 info)

                | :? X11WindowInfo as i ->
                    let connection = Xcb.XGetXCBConnection(i.Display)
                    let info = { XcbSurfaceInfo.connection = NativePtr.ofNativeInt connection; XcbSurfaceInfo.window = i.WindowHandle }
                    Some (SurfaceInfo.Xcb info)

                | _ ->

                    None
            

module private DefaultText =
    [<Literal>]
    let baseText = "Aardvark rocks \\o/ - Vulkan GameWindow"



type VulkanRenderWindow(instance : Instance, runtime : Runtime, position : V2i, size : V2i, mode : AbstractGraphicsMode) =
    inherit SimpleWindow(position, size)

    static let noDispose = { new IDisposable with member x.Dispose() = () }

    let device = runtime.Device

    let mutable task = RenderTask.empty
    let mutable taskSub = noDispose

    let mutable invalidSize = false
    let mutable resizeSub = noDispose

    let mutable swapchainDesc : SwapchainDescription = Unchecked.defaultof<_>
    let mutable surface : Surface = Unchecked.defaultof<_>
    let mutable swapchain : Option<Swapchain> = None
    let mutable rafap = false

    let startTime = DateTime.Now
    let sw = System.Diagnostics.Stopwatch.StartNew()
    let time = Mod.custom (fun _ -> startTime + sw.Elapsed) 
    
    let frameWatch = System.Diagnostics.Stopwatch()
    let mutable frameCount = 0
    let mutable totalTime = MicroTime.Zero
    let mutable baseTitle = ""


    let eBeforeRender = FSharp.Control.Event<unit>()
    let eAfterRender =  FSharp.Control.Event<unit>()

    let createSurface (info : OpenTK.Platform.IWindowInfo) =
        match SurfaceInfo.tryOfWindowInfo info with
            | Some info ->
                device.CreateSurface(info)
            | None ->
                Log.error "[Vulkan] cannot create device for window: %A" info
                failwithf "[Vulkan] cannot create device for window: %A" info
    
    member x.NewFrame (t : MicroTime) = 
        frameCount <- frameCount + 1
        totalTime <- totalTime + t
        if frameCount > 50 then
            let fps = float frameCount / totalTime.TotalSeconds
            base.Title <- DefaultText.baseText + sprintf " (%.3f fps)" fps
            frameCount <- 0
            totalTime <- MicroTime.Zero
        ()
    member x.RenderTask
        with get() = 
            task

        and set (t : IRenderTask) =
            taskSub.Dispose()
            taskSub <- t.AddMarkingCallback(x.Invalidate)
            task <- t
            x.Invalidate()

    member x.RenderAsFastAsPossible
        with get() = rafap
        and set v =
            if v then
                if not rafap then
                    rafap <- true
                    x.Invalidate()
            else
                rafap <- false
                
    member x.BeforeRender : FSharp.Control.IEvent<unit> = eBeforeRender.Publish
    member x.AfterRender : FSharp.Control.IEvent<unit> = eAfterRender.Publish

    override x.OnLoad() =
        let info = unbox<OpenTK.Platform.IWindowInfo> x.WindowInfo
        surface <- createSurface info
        swapchainDesc <- device.CreateSwapchainDescription(surface, mode)

        let k = x.Keyboard
        k.KeyDown(Keys.End).Values.Add (fun () ->
            if Mod.force k.Control then
                x.RenderAsFastAsPossible <- not x.RenderAsFastAsPossible
        )
    
        k.KeyDown(Keys.Enter).Values.Add(fun () ->
            if Mod.force k.Alt && Mod.force k.Shift then
                x.Fullscreen <- not x.Fullscreen
        )

        // We can only render if we have a surface with a valid size > (0, 0)
        // Invalidate the window if it changes from an invalid to a valid
        // state, so we can continue rendering again
        resizeSub <- x.Resize.Subscribe(fun ev ->
            let newInvalidSize = ev.Size.AnySmallerOrEqual 0

            if invalidSize && not newInvalidSize then
                x.Invalidate()

            invalidSize <- newInvalidSize
        )
        
        let sw = System.Diagnostics.Stopwatch()
        eBeforeRender.Publish.Add sw.Restart
        eAfterRender.Publish.Add (fun () -> sw.Stop(); x.NewFrame sw.MicroTime)
        

    override x.OnUnload() =
        match swapchain with
            | Some c -> 
                device.Delete c
                swapchain <- None
            | None -> ()

        device.Delete swapchainDesc
        device.Delete surface
        surface <- Unchecked.defaultof<_>
        swapchainDesc <- Unchecked.defaultof<_>

        task.Dispose()
        task <- RenderTask.empty

        resizeSub.Dispose()
        resizeSub <- noDispose

        ()

    override x.OnRender() =
        transact time.MarkOutdated
        eBeforeRender.Trigger()
        let s = surface.Size

        if s.AllGreater 0 then
            let swapchain =
                match swapchain with
                    | Some c when c.Size = s ->
                        c
                    | None ->
                        let c = device.CreateSwapchain(swapchainDesc)
                        swapchain <- Some c
                        c
                    | Some o ->
                        o.Dispose()
                        let c = device.CreateSwapchain(swapchainDesc)
                        swapchain <- Some c
                        c

            swapchain.RenderFrame(fun framebuffer ->
                task.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer framebuffer)
            )

        eAfterRender.Trigger()
        transact time.MarkOutdated
        if rafap then x.Invalidate()

   
    member this.FramebufferSignature =
        this.Load()
        swapchainDesc.renderPass :> IFramebufferSignature
        
    member this.Runtime = runtime
    member this.Samples = 
        this.Load()
        swapchainDesc.samples
    member this.Time = time
    
    member x.AverageFrameTime = totalTime / float frameCount


    interface IRenderTarget with
        member this.AfterRender = this.AfterRender
        member this.BeforeRender = this.BeforeRender
        member this.FramebufferSignature = this.FramebufferSignature
        member this.RenderTask
            with get () = this.RenderTask
            and set v = this.RenderTask <- v
        member this.Runtime = this.Runtime :> IRuntime
        member this.Samples = this.Samples
        member this.Sizes = this.Sizes
        member this.Time = time

    interface IRenderControl with
        member this.Keyboard = this.Keyboard
        member this.Mouse = this.Mouse

    interface IRenderWindow with
        member x.Run() = x.Run()


type VulkanApplication(debug : bool, chooseDevice : list<PhysicalDevice> -> PhysicalDevice) =
    let requestedExtensions =
        [
            yield Instance.Extensions.Surface
            yield Instance.Extensions.SwapChain
            yield Instance.Extensions.Win32Surface
            yield Instance.Extensions.XcbSurface
            yield Instance.Extensions.XlibSurface

            yield "VK_EXT_shader_subgroup_ballot"
            yield "VK_EXT_shader_subgroup_vote"
            yield Instance.Extensions.GetPhysicalDeviceProperties2

            if debug then
                yield Instance.Extensions.DebugReport
                yield Instance.Extensions.DebugUtils

        ]

    let requestedLayers =
        [
            if debug then
                yield Instance.Layers.StandardValidation
                yield "VK_LAYER_LUNARG_assistant_layer"
                //yield Instance.Layers.Nsight
                //yield Instance.Layers.SwapChain
                //yield Instance.Layers.DrawState
                //yield Instance.Layers.ParamChecker
                //yield Instance.Layers.DeviceLimits
                //yield Instance.Layers.CoreValidation
                //yield Instance.Layers.ParameterValidation
                //yield Instance.Layers.ObjectTracker
                //yield Instance.Layers.Threading
                //yield Instance.Layers.UniqueObjects
                //yield Instance.Layers.Image
        ]

    let instance = 
        let availableExtensions =
            Instance.GlobalExtensions |> Seq.map (fun e -> e.name) |> Set.ofSeq

        let availableLayers =
            Instance.AvailableLayers |> Seq.map (fun l -> l.name) |> Set.ofSeq

        // create an instance
        let enabledExtensions = requestedExtensions |> List.filter (fun r -> Set.contains r availableExtensions)
        let enabledLayers = requestedLayers |> List.filter (fun r -> Set.contains r availableLayers)
    
        new Instance(Version(1,1,0), enabledLayers, enabledExtensions)

    // choose a physical device
    let physicalDevice = 
        if instance.Devices.Length = 0 then
            failwithf "[Vulkan] could not get vulkan devices"
        else
            chooseDevice (Seq.toList (CustomDeviceChooser.Filter instance.Devices))

    do instance.PrintInfo(Logger.Get 2, physicalDevice)

    // create a device
    let device = 
        let availableExtensions =
            physicalDevice.GlobalExtensions |> Seq.map (fun e -> e.name) |> Set.ofSeq

        let enabledExtensions = requestedExtensions |> List.filter (fun r -> Set.contains r availableExtensions)

        physicalDevice.CreateDevice(enabledExtensions)

    

    // create a runtime
    let runtime = new Runtime(device, false, false, debug)

    let defaultCachePath =
        let dir =
            Path.combine [
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                "Aardvark"
                "VulkanShaderCache"
            ]
        runtime.ShaderCachePath <- Some dir
        dir

    let canCreateRenderControl =
        List.contains Instance.Extensions.SwapChain device.EnabledExtensions

    member x.Runtime = runtime

    member x.CreateGameWindow(samples : int) =
        new VulkanRenderWindow(instance, runtime, V2i(100,100), V2i(1024, 768), GraphicsMode(Col.Format.RGBA, 8, 24, 8, 2, samples, ImageTrafo.MirrorY))

    member x.Dispose() =
        runtime.Dispose()
        device.Dispose()
        instance.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()
        
    interface IApplication with
        member x.Runtime = runtime :> _ 
        member x.Initialize(ctrl : IRenderControl, samples : int) = failwithf "[Vulkan] unsupported RenderControl: %A" ctrl
           
           
    new(debug : bool) = new VulkanApplication(debug, ConsoleDeviceChooser.run)
    new(choose : list<PhysicalDevice> -> PhysicalDevice) = new VulkanApplication(false, choose)
    new() = new VulkanApplication(false, ConsoleDeviceChooser.run)