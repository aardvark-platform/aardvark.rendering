namespace Aardvark.Rendering.Vulkan

open System

type HeadlessVulkanApplication(debug : bool, instanceExtensions : list<string>, deviceExtensions : PhysicalDevice -> list<string>) =
    let requestedExtensions =
        [
            yield Instance.Extensions.Surface
            yield Instance.Extensions.SwapChain
            yield Instance.Extensions.Win32Surface
            yield Instance.Extensions.XcbSurface
            yield Instance.Extensions.XlibSurface

            yield! instanceExtensions

            yield "VK_EXT_shader_subgroup_ballot"
            yield "VK_EXT_shader_subgroup_vote"
            if debug then
                yield Instance.Extensions.DebugReport
        ]

    let requestedLayers =
        [
            if debug then
                yield Instance.Layers.Nsight
                yield Instance.Layers.SwapChain
                yield Instance.Layers.DrawState
                yield Instance.Layers.ParamChecker
                yield Instance.Layers.StandardValidation
                yield Instance.Layers.DeviceLimits
                yield Instance.Layers.CoreValidation
                yield Instance.Layers.ParameterValidation
                yield Instance.Layers.ObjectTracker
                // complaining about concurrent usage of a concurrent image
                yield Instance.Layers.Threading
                yield Instance.Layers.UniqueObjects
                yield Instance.Layers.Image
        ]

    let instance = 
        let availableExtensions =
            Instance.GlobalExtensions |> Seq.map (fun e -> e.name) |> Set.ofSeq

        let availableLayers =
            Instance.AvailableLayers |> Seq.map (fun l -> l.name) |> Set.ofSeq

        // create an instance
        let enabledExtensions = requestedExtensions |> List.filter (fun r -> Set.contains r availableExtensions) |> Set.ofList
        let enabledLayers = requestedLayers |> List.filter (fun r -> Set.contains r availableLayers) |> Set.ofList
    
        new Instance(Version(1,0,0), enabledLayers, enabledExtensions)


    // choose a physical device
    let physicalDevice = 
        if instance.Devices.Length = 0 then
            failwithf "[Vulkan] could not get vulkan devices"
        else
            ConsoleDeviceChooser.run instance.Devices

    do instance.PrintInfo(Logger.Default, physicalDevice.Index)

    // create a device
    let device = 
        let availableExtensions =
            physicalDevice.GlobalExtensions |> Seq.map (fun e -> e.name) |> Set.ofSeq

        let availableLayers =
            physicalDevice.AvailableLayers |> Seq.map (fun l -> l.name) |> Set.ofSeq

        let devExt = deviceExtensions physicalDevice

        let enabledExtensions = Set.union (Set.ofList requestedExtensions) (Set.ofList devExt) |> Set.toList |> List.filter (fun r -> Set.contains r availableExtensions) |> Set.ofList
        let enabledLayers = requestedLayers |> List.filter (fun r -> Set.contains r availableLayers) |> Set.ofList
        
        physicalDevice.CreateDevice(enabledLayers, enabledExtensions)

    // create a runtime
    let runtime = new Runtime(device, false, false, debug)

    member x.Dispose() =
        runtime.Dispose()
        device.Dispose()
        instance.Dispose()


    member x.Instance = instance
    member x.Device = device
    member x.Runtime = runtime

    new() = new HeadlessVulkanApplication(false, [], fun _ -> [])
    new(debug) = new HeadlessVulkanApplication(debug, [], fun _ -> [])

    interface IDisposable with
        member x.Dispose() = x.Dispose()