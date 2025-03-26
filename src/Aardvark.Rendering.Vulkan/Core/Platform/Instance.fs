namespace Aardvark.Rendering.Vulkan

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Rendering
open EXTValidationFeatures
open Vulkan11

#nowarn "9"

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Instance =
    module Extensions =
        let Surface                         = KHRSurface.Name
        let SwapChain                       = KHRSwapchain.Name
        let Display                         = KHRDisplay.Name
        let DisplaySwapChain                = KHRDisplaySwapchain.Name

        let AndroidSurface                  = KHRAndroidSurface.Name
        let WaylandSurface                  = KHRWaylandSurface.Name
        let Win32Surface                    = KHRWin32Surface.Name
        let XcbSurface                      = KHRXcbSurface.Name
        let XlibSurface                     = KHRXlibSurface.Name
        let GetPhysicalDeviceProperties2    = KHRGetPhysicalDeviceProperties2.Name

        let ShaderSubgroupVote              = EXTShaderSubgroupVote.Name
        let ShaderSubgroupBallot            = EXTShaderSubgroupBallot.Name

        let ConservativeRasterization       = EXTConservativeRasterization.Name

        let Debug                           = EXTDebugUtils.Name

        let MemoryBudget                    = EXTMemoryBudget.Name

        let MemoryPriority                  = EXTMemoryPriority.Name

        let Maintenance = [
            KHRMaintenance4.Name
            KHRMaintenance5.Name
            KHRDynamicRendering.Name
            KHRDepthStencilResolve.Name
            KHRCreateRenderpass2.Name
        ]

        let Raytracing = [
                KHRRayTracingPipeline.Name
                KHRAccelerationStructure.Name
                KHRBufferDeviceAddress.Name
                KHRDeferredHostOperations.Name
                EXTDescriptorIndexing.Name
                KHRSpirv14.Name
                KHRShaderFloatControls.Name
            ]

        let ExternalMemory =
            if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                KHRExternalMemoryWin32.Name
            else
                KHRExternalMemoryFd.Name

        let Sharing = [
                KHRGetPhysicalDeviceProperties2.Name
                KHRExternalMemoryCapabilities.Name
                KHRExternalMemory.Name
                KHRExternalFenceCapabilities.Name
                KHRExternalFence.Name
                KHRExternalSemaphoreCapabilities.Name
                KHRExternalSemaphore.Name
                EXTExternalMemoryHost.Name

                if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                    KHRExternalMemoryWin32.Name
                    KHRExternalFenceWin32.Name
                    KHRExternalSemaphoreWin32.Name
                else
                    KHRExternalMemoryFd.Name
                    KHRExternalFenceFd.Name
                    KHRExternalSemaphoreFd.Name

                    if RuntimeInformation.IsOSPlatform OSPlatform.Linux then
                        EXTExternalMemoryDmaBuf.Name
            ]

    module Layers =
        let ApiDump             = "VK_LAYER_LUNARG_api_dump"
        let DeviceLimits        = "VK_LAYER_LUNARG_device_limits"
        let DrawState           = "VK_LAYER_LUNARG_draw_state"
        let Image               = "VK_LAYER_LUNARG_image"
        let MemTracker          = "VK_LAYER_LUNARG_mem_tracker"
        let ParamChecker        = "VK_LAYER_LUNARG_param_checker"
        let Screenshot          = "VK_LAYER_LUNARG_screenshot"
        let SwapChain           = "VK_LAYER_LUNARG_swapchain"
        let Trace               = "VK_LAYER_LUNARG_vktrace"
        let Validation          = "VK_LAYER_KHRONOS_validation"
        let Nsight              = "VK_LAYER_NV_nsight"

type Instance(apiVersion : Version, layers : list<string>, extensions : list<string>, debug : IDebugConfig) as this =

    static let defaultVersion = Version(1, 1, 0)

    static let availableLayers =
        native {
            let! pCount = 0u
            VkRaw.vkEnumerateInstanceLayerProperties(pCount, NativePtr.zero)
                |> check "could not get available instance layers"

            let properties = Array.zeroCreate (int !!pCount)
            let! ptr = properties
            VkRaw.vkEnumerateInstanceLayerProperties(pCount, ptr)
                |> check "could not get available instance layers"
            return properties |> Array.map LayerInfo.ofVulkan
        }

    static let globalExtensions =
        native {
            let! pCount = 0u
            VkRaw.vkEnumerateInstanceExtensionProperties(null, pCount, NativePtr.zero)
                |> check "could not get available instance layers"

            let properties = Array.zeroCreate (int !!pCount)
            let! ptr = properties
            VkRaw.vkEnumerateInstanceExtensionProperties(null, pCount, ptr)
                |> check "could not get available instance layers"
            return properties |> Array.map ExtensionInfo.ofVulkan
        }

    static let availableLayerNames = availableLayers |> Seq.map (fun l -> l.name.ToLower(), l) |> Map.ofSeq
    static let globalExtensionNames = globalExtensions |> Seq.map (fun p -> p.name.ToLower(), p.name) |> Map.ofSeq

    static let filterLayersAndExtensions (wantedLayers : list<string>) (wantedExtensions : list<string>) =
        let availableExtensions = Dictionary globalExtensionNames

        let enabledLayers =
            wantedLayers |> List.distinct |> List.filter (fun name ->
                let name = name.ToLower()
                match Map.tryFind name availableLayerNames with
                | Some layer ->
                    Log.Vulkan.debug "enabled layer %A" name
                    for e in layer.extensions do
                        availableExtensions.[e.name.ToLower()] <- e.name
                    true
                | _ ->
                    false
            )

        let enabledExtensions =
            wantedExtensions |> List.choose (fun name ->
                let name = name.ToLower()
                match availableExtensions.TryGetValue name with
                | (true, realName) ->
                    Log.Vulkan.debug "enabled instance extension %A" name
                    Some realName
                | _ ->
                    None
            )

        enabledLayers, enabledExtensions

    let mutable isDisposed = 0
    let beforeDispose = Event<unit>()

    let debug = DebugConfig.unbox debug

    let extensions =
        if debug.DebugReportEnabled || debug.ValidationLayerEnabled then
            extensions @ [Instance.Extensions.Debug]
        else
            extensions |> List.filter ((<>) Instance.Extensions.Debug)
        |> List.distinct

    let layers =
        if debug.ValidationLayerEnabled then
            layers @ [Instance.Layers.Validation]
        else
            layers |> List.filter ((<>) Instance.Layers.Validation)

    let layers, instanceExtensions = filterLayersAndExtensions layers extensions

    let hasInstanceExtension name =
        instanceExtensions |> List.contains name

    let debugReportEnabled =
        instanceExtensions |> List.contains Instance.Extensions.Debug

    let validationEnabled =
        layers |> List.contains Instance.Layers.Validation

    let appName = CStr.malloc "Aardvark"

    let mutable instance, apiVersion =
        let layers = List.toArray layers
        let extensions = List.toArray instanceExtensions

        let rec tryCreate (apiVersion : Version) =
            native {
                let! pLayers = layers
                let! pExtensions = extensions
                let version = apiVersion.ToVulkan()
                
                let! pApplicationInfo =
                    VkApplicationInfo(
                        appName,
                        0u,
                        appName,
                        0u,
                        version
                    )

                let enabledValidationFeatures, disabledValidationFeatures =
                    match debug.ValidationLayer with
                    | Some cfg when validationEnabled ->
                        [|
                            if cfg.BestPracticesValidation then
                                 yield VkValidationFeatureEnableEXT.BestPractices

                            if cfg.SynchronizationValidation then
                                yield VkValidationFeatureEnableEXT.SynchronizationValidation

                            match cfg.ShaderBasedValidation with
                            | ShaderValidation.GpuAssisted ->
                                yield VkValidationFeatureEnableEXT.GpuAssisted

                            | ShaderValidation.DebugPrint ->
                                yield VkValidationFeatureEnableEXT.DebugPrintf

                            | _ ->
                                ()
                        |],

                        [|
                            if not cfg.ThreadSafetyValidation then
                                VkValidationFeatureDisableEXT.ThreadSafety

                            if not cfg.ObjectLifetimesValidation then
                                VkValidationFeatureDisableEXT.ObjectLifetimes
                        |]

                    | _ ->
                        [||], [||]

                let! pEnabledValidationFeatures = enabledValidationFeatures
                let! pDisabledValidationFeatures = disabledValidationFeatures

                let! pValidationFeatures =
                    VkValidationFeaturesEXT(
                        uint32 enabledValidationFeatures.Length, pEnabledValidationFeatures,
                        uint32 disabledValidationFeatures.Length, pDisabledValidationFeatures
                    )

                let pNext =
                    if enabledValidationFeatures.Length > 0 || disabledValidationFeatures.Length > 0 then
                        NativePtr.toNativeInt pValidationFeatures
                    else
                        0n

                let! pInfo =
                    VkInstanceCreateInfo(
                        pNext,
                        VkInstanceCreateFlags.None,
                        pApplicationInfo,
                        uint32 layers.Length, pLayers,
                        uint32 extensions.Length, pExtensions
                    )
                let! pInstance = VkInstance.Zero
                
                let res = VkRaw.vkCreateInstance(pInfo, NativePtr.zero, pInstance)
                let instance = NativePtr.read pInstance
                if res = VkResult.Success then 
                    return Some (instance, apiVersion)
                elif apiVersion.Minor > 0 then
                    return tryCreate (Version(apiVersion.Major, apiVersion.Minor - 1, apiVersion.Build))
                else
                    return None
            }

        match tryCreate apiVersion with
            | Some instance -> instance
            | None -> failf "could not create instance"

    let devices =
        native {
            let! pDeviceCount = 0u
            VkRaw.vkEnumeratePhysicalDevices(instance, pDeviceCount, NativePtr.zero)
                |> check "could not get physical devices"
                
            let devices = Array.zeroCreate (int !!pDeviceCount)
            let! ptr = devices
            VkRaw.vkEnumeratePhysicalDevices(instance, pDeviceCount, ptr)
                |> check "could not get physical devices"

            return devices |> Array.map (fun d -> PhysicalDevice(this, d, hasInstanceExtension))
        }

    let groups =    
        if apiVersion >= Version(1,1) then
            native {
                let! pGroupCount = 0u

                VkRaw.vkEnumeratePhysicalDeviceGroups(instance, pGroupCount, NativePtr.zero)
                    |> check "could not get physical device groups"

                let groupCount = NativePtr.read pGroupCount
                let groups = Array.replicate (int groupCount) VkPhysicalDeviceGroupProperties.Empty
                let! ptr = groups
                VkRaw.vkEnumeratePhysicalDeviceGroups(instance, pGroupCount, ptr)
                    |> check "could not get physical device groups"

                return
                    groups |> Array.mapi (fun i d -> 
                        let devices = 
                            Array.init (int d.physicalDeviceCount) (fun ii ->
                                let handle = d.physicalDevices.[ii]
                                devices |> Array.find (fun dd -> dd.Handle = handle)
                            )
                        new PhysicalDeviceGroup(this, devices, hasInstanceExtension)
                    )
                    |> Array.filter (fun g -> g.Devices.Length > 1)
            }
        else
            [||]

    let devicesAndGroups =
        Array.append devices (groups |> Array.map (fun a -> a :> _))


    new (layers: list<string>, extensions: list<string>, debug: IDebugConfig) =
        new Instance(defaultVersion, layers, extensions, debug)

    new (apiVersion: Version, layers: list<string>, extensions: list<string>) =
        new Instance(apiVersion, layers, extensions, DebugConfig.None)

    new (layers: list<string>, extensions: list<string>) =
        new Instance(defaultVersion, layers, extensions)

    static member DefaultVersion = defaultVersion
    static member AvailableLayers = availableLayers
    static member GlobalExtensions = globalExtensions

    [<CLIEvent>]
    member x.BeforeDispose = beforeDispose.Publish

    member x.IsDisposed = isDisposed <> 0

    member private x.Dispose(disposing : bool) =
        let o = Interlocked.Exchange(&isDisposed, 1)
        if o = 0 then
            beforeDispose.Trigger()

            for g in groups do g.Dispose()
            VkRaw.vkDestroyInstance(instance, NativePtr.zero)
            instance <- VkInstance.Zero

            if disposing then GC.SuppressFinalize x

    member x.Dispose() = x.Dispose true

    override x.Finalize() = x.Dispose false

    member x.APIVersion = apiVersion
    member x.EnabledLayers = layers
    member x.EnabledExtensions = instanceExtensions

    member x.DebugReportEnabled = debugReportEnabled
    member x.DebugConfig = debug

    member x.Handle = instance

    member x.Devices = devicesAndGroups

    member x.PrintInfo(l : ILogger, chosenDevice : PhysicalDevice) =
        let devices =
            match chosenDevice with
                | :? PhysicalDeviceGroup as g -> g.Devices
                | _ -> [| chosenDevice |]

        let chosenDevices = HashSet.ofArray devices

        let caps = 
            [
                QueueFlags.Compute, "compute"
                QueueFlags.Graphics, "graphics"
                QueueFlags.Transfer, "transfer"
                QueueFlags.SparseBinding, "sparsebinding"
            ]

        let capString (c : QueueFlags) =
            caps |> List.choose (fun (f,n) ->
                if c.HasFlag(f) then Some n
                else None
            ) |> String.concat ", "


        l.section "instance:" (fun () ->
            l.section "layers:" (fun () ->
                for layer in availableLayers do
                    let isEnabled = List.contains layer.name layers
                    let suffix = if isEnabled then "(X)" else "( )"
                    l.line "%s (v%A) %s" layer.name layer.specification suffix
            )

            l.section "extensions:" (fun () ->
                for ext in globalExtensions do
                    let isEnabled = List.contains ext.name instanceExtensions
                    let suffix = if isEnabled then "(X)" else "( )"
                    l.line "%s (v%A) %s" ext.name ext.specification suffix
            )

            l.section "devices:" (fun () ->
                let mutable index = 0
                for d in devices do
                    let l =
                        if HashSet.contains d chosenDevices then l
                        else l.WithVerbosity(l.Verbosity + 1)


                    l.section "%d:" index (fun () ->
                        if HashSet.contains d chosenDevices then 
                            l.line "CHOSEN DEVICE"
                        l.line "type:     %A" d.Type
                        l.line "vendor:   %s" d.Vendor
                        l.line "name:     %s" d.Name
                        l.line "version:  %A" d.APIVersion
                        l.line "driver:   %A" d.DriverVersion

                        l.section "layers:" (fun () ->
                            for layer in d.AvailableLayers do
                                l.line "%s (v%A)" layer.name layer.specification
                        )

                        l.section "extensions:" (fun () ->
                            for ext in d.GlobalExtensions do
                                l.line "%s (v%A)" ext.name ext.specification
                        )

                        l.section "features: " (fun () ->
                            d.Features.Print(l)
                        )

                        l.section "limits:" (fun () ->
                            d.Limits.Print(l)
                        )

                        l.section "heaps:" (fun () ->
                            for (h : MemoryHeapInfo) in d.MemoryHeaps do
                                match h.Flags with
                                    | MemoryHeapFlags.DeviceLocal -> l.line "%d: %A (device local)" h.Index h.Capacity
                                    | _  -> l.line "%d: %A" h.Index h.Capacity
                        )

                        l.section "memories:" (fun () ->
                            for (h : MemoryInfo) in d.MemoryTypes do
                                if h.flags <> MemoryFlags.None then
                                    l.line "%d: %A (heap: %d)" h.index h.flags h.heap.Index
                        )

                        l.section "queues:" (fun () ->
                            for (q : QueueFamilyInfo) in d.QueueFamilies do
                                l.section "%d:" q.index (fun () ->
                                    l.line "capabilities:   %s" (capString q.flags)
                                    l.line "count:          %d" q.count
                                    l.line "timestamp bits: %d" q.timestampBits
                                    l.line "img transfer:   %A" q.minImgTransferGranularity
                                )

                        )


                    )

                    index <- index + 1
            )

            if devices.Length > 1 then
                l.section "group:" (fun () ->
                    for i in 0 .. devices.Length - 1 do
                        let d = devices.[i]
                        l.line "%d: %s %s" i d.Vendor d.Name
                )
            else
                let d = devices.[0]
                l.line "device: %s %s" d.Vendor d.Name
        )

    interface IVulkanInstance

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AutoOpen>]
module InstanceExtensions =

    type PhysicalDevice with
        member x.Instance = x.InstanceInterface :?> Instance