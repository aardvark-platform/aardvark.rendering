namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

#nowarn "9"
#nowarn "51"


type Instance(apiVersion : Version, layers : Set<string>, extensions : Set<string>) as this =   
    inherit VulkanObject()

    static let availableLayers =
        let mutable count = 0u
        VkRaw.vkEnumerateInstanceLayerProperties(&&count, NativePtr.zero)
            |> check "could not get available instance layers"

        let properties = Array.zeroCreate (int count)
        properties |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumerateInstanceLayerProperties(&&count, ptr)
                |> check "could not get available instance layers"
        )
        properties |> Array.map LayerInfo.ofVulkan

    static let globalExtensions =
        let mutable count = 0u
        VkRaw.vkEnumerateInstanceExtensionProperties(null, &&count, NativePtr.zero)
            |> check "could not get available instance layers"

        let properties = Array.zeroCreate (int count)
        properties |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumerateInstanceExtensionProperties(null, &&count, ptr)
                |> check "could not get available instance layers"
        )
        properties |> Array.map ExtensionInfo.ofVulkan


    static let availableLayerNames = availableLayers |> Seq.map (fun l -> l.name.ToLower(), l) |> Map.ofSeq
    static let globalExtensionNames = globalExtensions |> Seq.map (fun p -> p.name.ToLower(), p.name) |> Map.ofSeq


    static let filterLayersAndExtensions (wantedLayers : Set<string>, wantedExtensions : Set<string>) =
        let availableExtensions = Dictionary globalExtensionNames

        let enabledLayers = 
            wantedLayers |> Set.filter (fun name ->
                let name = name.ToLower()
                match Map.tryFind name availableLayerNames with
                    | Some layer -> 
                        VkRaw.debug "enabled layer %A" name
                        for e in layer.extensions do
                            availableExtensions.[e.name.ToLower()] <- e.name
                        true
                    | _ ->
                        VkRaw.warn "could not enable instance-layer '%s' since it is not available" name
                        false
            )

        let enabledExtensions =
            wantedExtensions |> Seq.choose (fun name ->
                let name = name.ToLower()
                match availableExtensions.TryGetValue name with
                    | (true, realName) -> 
                        VkRaw.debug "enabled extension %A" name
                        Some realName
                    | _ -> 
                        VkRaw.warn "could not enable instance-extension '%s' since it is not available" name
                        None
            ) |> Set.ofSeq

        enabledLayers, enabledExtensions

    let layers, extensions = filterLayersAndExtensions (layers, extensions)

    let mutable instance =
        try
            let layers = Set.toArray layers
            let extensions = Set.toArray extensions

            let pLayers = CStr.sallocMany layers
            let pExtensions = CStr.sallocMany extensions
            let appName = CStr.salloc "Aardvark"

            let apiVersion = apiVersion.ToVulkan()

            let mutable applicationInfo =
                VkApplicationInfo(
                    VkStructureType.ApplicationInfo, 0n,
                    appName,
                    0u,
                    appName,
                    0u,
                    apiVersion
                )

            let mutable info =
                VkInstanceCreateInfo(
                    VkStructureType.InstanceCreateInfo, 0n,
                    0u,
                    &&applicationInfo,
                    uint32 layers.Length, pLayers,
                    uint32 extensions.Length, pExtensions
                )

            let mutable instance = VkInstance.Zero

            VkRaw.vkCreateInstance(&&info, NativePtr.zero, &&instance)
                |> check "could not create instance"

            instance
        finally
            ()

    let devices =
        let mutable deviceCount = 0u
        VkRaw.vkEnumeratePhysicalDevices(instance, &&deviceCount, NativePtr.zero)
            |> check "could not get physical devices"

        let devices = Array.zeroCreate (int deviceCount)
        devices |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumeratePhysicalDevices(instance, &&deviceCount, ptr)
                |> check "could not get physical devices"
        )

        devices |> Array.mapi (fun i d -> PhysicalDevice(this, d, i))

    
    static member AvailableLayers = availableLayers
    static member GlobalExtensions = globalExtensions

    override x.Release() =
        VkRaw.vkDestroyInstance(instance, NativePtr.zero)
        instance <- VkInstance.Zero

    member x.EnabledLayers = layers
    member x.EnabledExtensions = extensions

    member x.Handle = instance

    member x.Devices = devices

    member x.PrintInfo(verbosity : int) =
        let section (fmt : Printf.StringFormat<'a, (unit -> 'x) -> 'x>) =
            fmt |> Printf.kprintf (fun str ->
                fun cont -> 
                    try
                        Report.Begin(verbosity, "{0}", str)
                        cont()
                    finally 
                        Report.End(verbosity) |> ignore
            )

        let line fmt = Printf.kprintf (fun str -> Report.Line(verbosity, str)) fmt

        section "instance" (fun () ->
            section "layers:" (fun () ->
                for l in availableLayers do
                    let isEnabled = Set.contains l.name layers
                    let suffix = if isEnabled then "(X)" else "( )"
                    line "%s (v%A) %s" l.name l.specification suffix
            )

            section "extensions:" (fun () ->
                for l in globalExtensions do
                    let isEnabled = Set.contains l.name extensions
                    let suffix = if isEnabled then "(X)" else "( )"
                    line "%s (v%A) %s" l.name l.specification suffix
            )

            section "devices:" (fun () ->
                for d in devices do
                    section "%d:" d.Index (fun () ->
                        line "type:     %A" d.Type
                        line "vendor:   %s" d.Vendor
                        line "name:     %s" d.Name
                        line "version:  %A" d.APIVersion
                        line "driver:   %A" d.DriverVersion

                        section "layers:" (fun () ->
                            for l in d.AvailableLayers do
                                line "%s (v%A)" l.name l.specification
                        )

                        section "extensions:" (fun () ->
                            for l in d.GlobalExtensions do
                                line "%s (v%A)" l.name l.specification
                        )

                        section "heaps:" (fun () ->
                            for (h : MemoryHeapInfo) in d.Heaps do
                                section "%d:" h.Index (fun () ->
                                    line "capacity: %A" h.Capacity
                                    line "flags:    %A" h.Flags
                                )
                        )

                        section "memories:" (fun () ->
                            for (h : MemoryInfo) in d.MemoryTypes do
                                if h.flags <> MemoryFlags.None then
                                    section "%d:" h.index (fun () ->
                                        line "heap:     %A" h.heap.Index
                                        line "flags:    %A" h.flags
                                    )
                        )

                        section "queues:" (fun () ->
                            for (q : QueueFamilyInfo) in d.QueueFamilies do
                                section "%d:" q.index (fun () ->
                                    line "capabilities:   %A" q.flags
                                    line "count:          %d" q.count
                                    line "timestamp bits: %d" q.timestampBits
                                    line "img transfer:   %A" q.minImgTransferGranularity
                                )

                            line "main-queue: %d" d.MainQueue.index

                        )


                    )
            )

        )


and PhysicalDevice internal(instance : Instance, handle : VkPhysicalDevice, index : int) =
    static let allFormats = Enum.GetValues(typeof<VkFormat>) |> unbox<VkFormat[]>

    let availableLayers = 
        let mutable count = 0u
        VkRaw.vkEnumerateDeviceLayerProperties(handle, &&count, NativePtr.zero)
            |> check "could not get device-layers"

        let props = Array.zeroCreate (int count)
        props |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumerateDeviceLayerProperties(handle, &&count, ptr)
                |> check "could not get device-layers"
        )

        props |> Array.map (LayerInfo.ofVulkanDevice handle)

    let globalExtensions =
        let mutable count = 0u
        VkRaw.vkEnumerateDeviceExtensionProperties(handle, null, &&count, NativePtr.zero)
            |> check "could not get device-extensions"

        let props = Array.zeroCreate (int count)
        props |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumerateDeviceExtensionProperties(handle, null, &&count, ptr)
                |> check "could not get device-layers"
        )
        props |> Array.map ExtensionInfo.ofVulkan

    let mutable properties = VkPhysicalDeviceProperties()
    do VkRaw.vkGetPhysicalDeviceProperties(handle, &&properties)

    let limits = properties.limits
    let vendor = PCI.vendorName (int properties.vendorID)

    let name = properties.deviceName.Value
    let driverVersion = Version.FromVulkan properties.driverVersion
    let apiVersion = Version.FromVulkan properties.apiVersion
    
    let queueFamilyInfos =
        let mutable count = 0u
        VkRaw.vkGetPhysicalDeviceQueueFamilyProperties(handle, &&count, NativePtr.zero)

        let props = Array.zeroCreate (int count)
        props |> NativePtr.withA (fun ptr ->
            VkRaw.vkGetPhysicalDeviceQueueFamilyProperties(handle, &&count, ptr)  
        )

        props |> Array.mapi (fun i p ->
            {
                index                       = i
                count                       = int p.queueCount
                flags                       = unbox (int p.queueFlags)
                minImgTransferGranularity   = V3i.OfExtent p.minImageTransferGranularity
                timestampBits               = int p.timestampValidBits
            }
        )

    let mutable memoryProperties = VkPhysicalDeviceMemoryProperties()
    do VkRaw.vkGetPhysicalDeviceMemoryProperties(handle, &&memoryProperties)
        
    let heaps =
        Array.init (int memoryProperties.memoryHeapCount) (fun i ->
            let info = memoryProperties.memoryHeaps.[i]
            MemoryHeapInfo(i, int64 info.size, unbox (int info.flags))
        )

    let memoryTypes =
        Array.init (int memoryProperties.memoryTypeCount) (fun i ->
            let info = memoryProperties.memoryTypes.[i]
            { MemoryInfo.index = i; MemoryInfo.heap = heaps.[int info.heapIndex]; MemoryInfo.flags = unbox (int info.propertyFlags) }
        )
        
    let formatProperties =
        Dictionary.ofList [
            for fmt in allFormats do
                let mutable props = VkFormatProperties()
                VkRaw.vkGetPhysicalDeviceFormatProperties(handle, fmt, &&props)
                yield fmt, props
        ]

    let deviceMemory = memoryTypes |> Array.maxBy MemoryInfo.deviceScore
    let hostMemory = memoryTypes |> Array.maxBy MemoryInfo.hostScore

    let mainQueue = queueFamilyInfos |> Array.maxBy (QueueFamilyInfo.flags >> QueueFlags.score)
    let pureTransferQueue = queueFamilyInfos |> Array.tryFind (QueueFamilyInfo.flags >> QueueFlags.transferOnly)

    member x.GetFormatFeatures(tiling : VkImageTiling, fmt : VkFormat) =
        match tiling with
            | VkImageTiling.Linear -> formatProperties.[fmt].linearTilingFeatures
            | _ -> formatProperties.[fmt].optimalTilingFeatures

    member x.GetBufferFormatFeatures(fmt : VkFormat) =
        formatProperties.[fmt].bufferFeatures

    member x.AvailableLayers = availableLayers
    member x.GlobalExtensions : ExtensionInfo[] = globalExtensions
    member x.QueueFamilies = queueFamilyInfos
    member x.MainQueue : QueueFamilyInfo = mainQueue
    member x.TransferQueue = pureTransferQueue
    member x.MemoryTypes = memoryTypes
    member x.Heaps = heaps

    member x.Handle = handle
    member x.Index = index
    member x.Vendor = vendor
    member x.Name = name
    member x.Type = properties.deviceType
    member x.APIVersion = apiVersion
    member x.DriverVersion = driverVersion

    member x.DeviceMemory = deviceMemory
    member x.HostMemory = hostMemory

    member x.Instance = instance
    member x.Limits = limits

    override x.ToString() =
        sprintf "{ name = %s; type = %A; api = %A }" name x.Type x.APIVersion
        
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Instance =
    module Extensions =
        let DebugReport         = "VK_EXT_debug_report"
        let Surface             = "VK_KHR_surface"
        let SwapChain           = "VK_KHR_swapchain"
        let Display             = "VK_KHR_display"
        let DisplaySwapChain    = "VK_KHR_display_swapchain"

        let AndroidSurface      = "VK_KHR_android_surface"
        let MirSurface          = "VK_KHR_mir_surface"
        let WaylandSurface      = "VK_KHR_wayland_surface"
        let Win32Surface        = "VK_KHR_win32_surface"
        let XcbSurface          = "VK_KHR_xcb_surface"
        let XlibSurface         = "VK_KHR_xlib_surface"

    module Layers =
        let ApiDump             = "VK_LAYER_LUNARG_api_dump"
        let DeviceLimits        = "VK_LAYER_LUNARG_device_limits"
        let DrawState           = "VK_LAYER_LUNARG_draw_state"
        let Image               = "VK_LAYER_LUNARG_image"
        let MemTracker          = "VK_LAYER_LUNARG_mem_tracker"
        let ObjectTracker       = "VK_LAYER_LUNARG_object_tracker"
        let ParamChecker        = "VK_LAYER_LUNARG_param_checker"
        let Screenshot          = "VK_LAYER_LUNARG_screenshot"
        let SwapChain           = "VK_LAYER_LUNARG_swapchain"
        let StandardValidation  = "VK_LAYER_LUNARG_standard_validation"
        let Threading           = "VK_LAYER_GOOGLE_threading"
        let UniqueObjects       = "VK_LAYER_GOOGLE_unique_objects"
        let Trace               = "VK_LAYER_LUNARG_vktrace"
        let ParameterValidation = "VK_LAYER_LUNARG_parameter_validation"
        let CoreValidation      = "VK_LAYER_LUNARG_core_validation"

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PhysicalDevice =
    module Extensions =
        let SwapChain = "VK_KHR_swapchain"