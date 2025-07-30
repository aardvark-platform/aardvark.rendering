namespace Aardvark.Application.WinForms

open System
open Aardvark.Application
open Aardvark.Rendering.Vulkan
open Aardvark.Base
open Aardvark.Rendering
open System.Runtime.InteropServices
open System.Windows.Forms

/// Device chooser with a GUI.
type DeviceChooserVisual() =
    inherit DeviceChooser()

    override this.IgnoreCache = Control.ModifierKeys = Keys.Alt

    override this.Choose(devices) =
        devices
        |> Array.map (fun d ->
            let prefix =
                match d with
                | :? PhysicalDeviceGroup as g -> $"{g.Devices.Length} x "
                | _ -> ""

            $"{prefix}{d.FullName}", d
        )
        |> ChooseForm.run
        |> Option.defaultWith (fun _ ->
            Log.warn "no vulkan device chosen => stopping Application"
            Environment.Exit 0
            Unchecked.defaultof<_>
        )

type VulkanApplication(debug: IDebugConfig,
                       [<Optional; DefaultParameterValue(null : string seq)>] extensions: string seq,
                       [<Optional; DefaultParameterValue(null : Func<DeviceFeatures, DeviceFeatures>)>] deviceFeatures: Func<DeviceFeatures, DeviceFeatures>,
                       [<Optional; DefaultParameterValue(null : IDeviceChooser)>] deviceChooser: IDeviceChooser) =
    let debug = DebugConfig.unbox debug

    let requestedExtensions =
        [
            if extensions <> null then
                yield! extensions

            yield Instance.Extensions.Surface
            yield Instance.Extensions.SwapChain
            yield Instance.Extensions.Win32Surface
            yield Instance.Extensions.XcbSurface
            yield Instance.Extensions.XlibSurface

            yield Instance.Extensions.ShaderSubgroupVote
            yield Instance.Extensions.ShaderSubgroupBallot
            yield! Instance.Extensions.Shader8Bit16Bit
            yield Instance.Extensions.GetPhysicalDeviceProperties2
            yield Instance.Extensions.ConservativeRasterization
            yield Instance.Extensions.MemoryBudget
            yield Instance.Extensions.MemoryPriority
            yield Instance.Extensions.DeviceFault

            yield! Instance.Extensions.Maintenance
            yield! Instance.Extensions.Raytracing debug.RaytracingValidationEnabled
            yield! Instance.Extensions.Sharing
        ]

    let requestedLayers =
        [
        ]

    let instance = 
        let availableExtensions =
            Instance.GlobalExtensions |> Seq.map _.name |> Set.ofSeq

        let availableLayers =
            Instance.AvailableLayers |> Seq.map _.name |> Set.ofSeq

        // create an instance
        let enabledExtensions = requestedExtensions |> List.filter (fun r -> Set.contains r availableExtensions)
        let enabledLayers = requestedLayers |> List.filter (fun r -> Set.contains r availableLayers)
    
        new Instance(enabledLayers, enabledExtensions, debug)

    // choose a physical device
    let physicalDevice = 
        if instance.Devices.Length = 0 then
            failwithf "[Vulkan] could not get vulkan devices"
        else
            let chooser = if deviceChooser <> null then deviceChooser else DeviceChooserVisual()
            chooser.Run instance.Devices

    do instance.PrintInfo(physicalDevice, debug.PlatformInformationVerbosity)

    // create a device
    let device = 
        let availableExtensions =
            physicalDevice.GlobalExtensions |> Seq.map _.name |> Set.ofSeq
  
        let enabledExtensions = requestedExtensions |> List.filter (fun r -> Set.contains r availableExtensions)
        let selectFeatures = if isNull deviceFeatures then DeviceFeatures.getDefault else deviceFeatures.Invoke

        physicalDevice.CreateDevice(enabledExtensions, selectFeatures)

    // create a runtime
    let runtime = new Runtime(device)

    let canCreateRenderControl =
        List.contains Instance.Extensions.SwapChain device.EnabledExtensions

    member x.Runtime = runtime

    member x.Initialize(ctrl : IRenderControl, samples : int) =
        match ctrl with
            | :? Aardvark.Application.WinForms.RenderControl as ctrl ->
                if canCreateRenderControl then
                    let mode = GraphicsMode(Col.Format.RGBA, 8, 24, 8, 2, samples, ImageTrafo.MirrorY, true)
                    let impl = new VulkanRenderControl(runtime, mode)
                    ctrl.Implementation <- impl
                else
                    failwithf "[Vulkan] cannot initialize RenderControl since device-extension (%s) is missing" Instance.Extensions.SwapChain

            | _ ->
                failwithf "[Vulkan] unsupported RenderControl-Type %A" ctrl

    member x.GetRenderPass(ctrl : IRenderControl) =
        match ctrl with
            | :? Aardvark.Application.WinForms.RenderControl as ctrl ->
                match ctrl.Implementation with
                    | :? VulkanRenderControl as ctrl -> ctrl.RenderPass
                    | _ -> failwith "unsupported RenderControl"

            | _ ->
                failwith "unsupported RenderControl"

    member x.Dispose() =
        runtime.Dispose()
        device.Dispose()
        instance.Dispose()

    interface IApplication with
        member x.Runtime = runtime :> _ 
        member x.Initialize(ctrl : IRenderControl, samples : int) =
            x.Initialize(ctrl, samples)

        member x.Dispose() = x.Dispose()

    new([<Optional; DefaultParameterValue(false)>] debug: bool,
        [<Optional; DefaultParameterValue(null : string seq)>] extensions: string seq,
        [<Optional; DefaultParameterValue(null : Func<DeviceFeatures, DeviceFeatures>)>] deviceFeatures: Func<DeviceFeatures, DeviceFeatures>,
        [<Optional; DefaultParameterValue(null : IDeviceChooser)>] deviceChooser: IDeviceChooser) =
        new VulkanApplication(DebugLevel.ofBool debug, extensions, deviceFeatures, deviceChooser)