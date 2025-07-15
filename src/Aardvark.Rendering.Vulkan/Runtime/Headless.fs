namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering

type HeadlessVulkanApplication(debug: IDebugConfig,
                               [<Optional; DefaultParameterValue(null : string seq)>] instanceExtensions: string seq,
                               [<Optional; DefaultParameterValue(null : Func<PhysicalDevice, string seq>)>] deviceExtensions: Func<PhysicalDevice, string seq>,
                               [<Optional; DefaultParameterValue(null : Func<DeviceFeatures, DeviceFeatures>)>] deviceFeatures: Func<DeviceFeatures, DeviceFeatures>,
                               [<Optional; DefaultParameterValue(null : IDeviceChooser)>] deviceChooser: IDeviceChooser) =
    let debug = DebugConfig.unbox debug

    let requestedExtensions =
        [
            if instanceExtensions <> null then
                yield! instanceExtensions

            yield Instance.Extensions.ShaderSubgroupVote
            yield Instance.Extensions.ShaderSubgroupBallot
            yield! Instance.Extensions.Shader8Bit16Bit
            yield Instance.Extensions.GetPhysicalDeviceProperties2
            yield Instance.Extensions.ConservativeRasterization
            yield Instance.Extensions.MemoryBudget
            yield Instance.Extensions.MemoryPriority
            yield Instance.Extensions.DeviceFault

            yield! Instance.Extensions.Maintenance
            yield! Instance.Extensions.Raytracing
            yield! Instance.Extensions.Sharing
        ]

    let requestedLayers =
        []

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
            let chooser = if deviceChooser <> null then deviceChooser else DeviceChooserAuto(preferDedicated = true)
            chooser.Run instance.Devices

    do instance.PrintInfo(physicalDevice, debug.PlatformInformationVerbosity)

    // create a device
    let device = 
        let availableExtensions =
            physicalDevice.GlobalExtensions |> Seq.map _.name |> Set.ofSeq

        let deviceExtensions =
            if isNull deviceExtensions then Seq.empty
            else
                deviceExtensions.Invoke physicalDevice
                |> Seq.filter (flip Set.contains availableExtensions)

        let selectFeatures = if isNull deviceFeatures then DeviceFeatures.getDefault else deviceFeatures.Invoke
        physicalDevice.CreateDevice(Seq.append requestedExtensions deviceExtensions, selectFeatures)

    // create a runtime
    let runtime = new Runtime(device)

    member x.Dispose() =
        runtime.Dispose()
        device.Dispose()
        instance.Dispose()

    member x.Instance = instance
    member x.Device = device
    member x.Runtime = runtime

    new([<Optional; DefaultParameterValue(false)>] debug: bool,
        [<Optional; DefaultParameterValue(null : string seq)>] instanceExtensions: string seq,
        [<Optional; DefaultParameterValue(null : Func<PhysicalDevice, string seq>)>] deviceExtensions: Func<PhysicalDevice, string seq>,
        [<Optional; DefaultParameterValue(null : Func<DeviceFeatures, DeviceFeatures>)>] deviceFeatures: Func<DeviceFeatures, DeviceFeatures>,
        [<Optional; DefaultParameterValue(null : IDeviceChooser)>] deviceChooser: IDeviceChooser) =
        new HeadlessVulkanApplication(DebugLevel.ofBool debug, instanceExtensions, deviceExtensions, deviceFeatures, deviceChooser)

    interface IDisposable with
        member x.Dispose() = x.Dispose()