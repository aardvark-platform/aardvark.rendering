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

            yield Extensions.ShaderSubgroupVote
            yield Extensions.ShaderSubgroupBallot
            yield! Extensions.Shader8Bit16Bit
            yield Extensions.GetPhysicalDeviceProperties2
            yield Extensions.ConservativeRasterization
            yield Extensions.CustomBorderColor
            yield Extensions.MemoryBudget
            yield Extensions.MemoryPriority
            yield Extensions.DeviceFault

            yield! Extensions.Maintenance
            yield! Extensions.Raytracing debug.RaytracingValidationEnabled
            yield! Extensions.Sharing
        ]

    let requestedLayers =
        []

    let instance =
        new Instance(requestedLayers, requestedExtensions, debug)

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
        let deviceExtensions =
            if isNull deviceExtensions then Seq.empty
            else deviceExtensions.Invoke physicalDevice

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