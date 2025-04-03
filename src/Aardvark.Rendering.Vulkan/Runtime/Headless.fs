namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open Aardvark.Rendering

type HeadlessVulkanApplication(debug: IDebugConfig, instanceExtensions: string seq, deviceExtensions: PhysicalDevice -> string seq,
                               [<Optional; DefaultParameterValue(null : IDeviceChooser)>] chooser: IDeviceChooser) =
    let debug = DebugConfig.unbox debug

    let requestedExtensions =
        [
            yield! instanceExtensions

            yield Instance.Extensions.ShaderSubgroupVote
            yield Instance.Extensions.ShaderSubgroupBallot
            yield Instance.Extensions.GetPhysicalDeviceProperties2
            yield Instance.Extensions.ConservativeRasterization
            yield Instance.Extensions.MemoryBudget
            yield Instance.Extensions.MemoryPriority

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
            let chooser = if chooser <> null then chooser else DeviceChooserAuto(preferDedicated = true)
            chooser.Run instance.Devices

    do instance.PrintInfo(physicalDevice, debug.PlatformInformationVerbosity)

    // create a device
    let device = 
        let availableExtensions =
            physicalDevice.GlobalExtensions |> Seq.map _.name |> Set.ofSeq

        let devExt = deviceExtensions physicalDevice
        let devExt = devExt |> Seq.filter (fun r -> Set.contains r availableExtensions)

        physicalDevice.CreateDevice(Seq.append requestedExtensions devExt)

    // create a runtime
    let runtime = new Runtime(device)

    member x.Dispose() =
        runtime.Dispose()
        device.Dispose()
        instance.Dispose()

    member x.Instance = instance
    member x.Device = device
    member x.Runtime = runtime

    new(debug: IDebugConfig,
        [<Optional; DefaultParameterValue(null : IDeviceChooser)>] chooser : IDeviceChooser) =
        new HeadlessVulkanApplication(debug, [], (fun _ -> Seq.empty), chooser)

    new(debug: bool, instanceExtensions: string seq, deviceExtensions : PhysicalDevice -> string seq,
        [<Optional; DefaultParameterValue(null : IDeviceChooser)>] chooser : IDeviceChooser) =
        new HeadlessVulkanApplication(DebugLevel.ofBool debug, instanceExtensions, deviceExtensions, chooser)

    new([<Optional; DefaultParameterValue(false)>] debug: bool,
        [<Optional; DefaultParameterValue(null : IDeviceChooser)>] chooser: IDeviceChooser) =
        new HeadlessVulkanApplication(debug, [], (fun _ -> Seq.empty), chooser)

    interface IDisposable with
        member x.Dispose() = x.Dispose()