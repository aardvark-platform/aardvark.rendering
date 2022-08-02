namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open System.Reflection
open KHRGetPhysicalDeviceProperties2
open KHRExternalMemoryCapabilities
open KHRRayTracingPipeline
open KHRRayQuery
open KHRAccelerationStructure
open KHRBufferDeviceAddress
open EXTDescriptorIndexing
open Vulkan11

#nowarn "9"
// #nowarn "51"

type Instance(apiVersion : Version, layers : list<string>, extensions : list<string>) as this =   
    inherit VulkanObject()


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


    static let filterLayersAndExtensions (wantedLayers : list<string>, wantedExtensions : list<string>) =
        let availableExtensions = Dictionary globalExtensionNames

        let enabledLayers = 
            wantedLayers |> List.filter (fun name ->
                let name = name.ToLower()
                match Map.tryFind name availableLayerNames with
                    | Some layer -> 
                        VkRaw.debug "enabled layer %A" name
                        for e in layer.extensions do
                            availableExtensions.[e.name.ToLower()] <- e.name
                        true
                    | _ ->
                        //VkRaw.warn "could not enable instance-layer '%s' since it is not available" name
                        false
            )

        let enabledExtensions =
            wantedExtensions |> List.choose (fun name ->
                let name = name.ToLower()
                match availableExtensions.TryGetValue name with
                    | (true, realName) -> 
                        VkRaw.debug "enabled extension %A" name
                        Some realName
                    | _ -> 
                        //VkRaw.warn "could not enable instance-extension '%s' since it is not available" name
                        None
            )

        enabledLayers, enabledExtensions

    let layers, extensions = filterLayersAndExtensions (layers, extensions)

    let debugEnabled =
        extensions |> List.contains EXTDebugReport.Name &&
        extensions |> List.contains EXTDebugUtils.Name    
    
    let appName = CStr.malloc "Aardvark"

    let mutable instance, apiVersion =
        let layers = List.toArray layers
        let extensions = List.toArray extensions

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
                    
                let! pInfo =
                    VkInstanceCreateInfo(
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

            return devices |> Array.map (fun d -> PhysicalDevice(this, d, extensions, apiVersion))
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
                        PhysicalDeviceGroup(this, devices, extensions, apiVersion)
                    )
                    |> Array.filter (fun g -> g.Devices.Length > 1)
            }
        else
            [||]

    let devicesAndGroups =
        Array.append devices (groups |> Array.map (fun a -> a :> _))
    
    static member AvailableLayers = availableLayers
    static member GlobalExtensions = globalExtensions

    override x.Release() =
        VkRaw.vkDestroyInstance(instance, NativePtr.zero)
        instance <- VkInstance.Zero
        
    member x.EnabledLayers = layers
    member x.EnabledExtensions = extensions

    member x.DebugEnabled = debugEnabled

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
                    let isEnabled = List.contains ext.name extensions
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
                            for (h : MemoryHeapInfo) in d.Heaps do
                                match h.Flags with
                                    | MemoryHeapFlags.DeviceLocalBit -> l.line "%d: %A (device local)" h.Index h.Capacity
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

and PhysicalDevice internal(instance : Instance, handle : VkPhysicalDevice, enabledInstanceExtensions : list<string>, instanceVersion : Version) =
    static let allFormats = Enum.GetValues(typeof<VkFormat>) |> unbox<VkFormat[]>
    

    let availableLayers = 
        native {
            let! pCount = 0u
            VkRaw.vkEnumerateDeviceLayerProperties(handle, pCount, NativePtr.zero)
                |> check "could not get device-layers"
                
            let props = Array.zeroCreate (int !!pCount)
            let! ptr = props
            VkRaw.vkEnumerateDeviceLayerProperties(handle, pCount, ptr)
                |> check "could not get device-layers"
      
            return props |> Array.map (LayerInfo.ofVulkanDevice handle)
        }

    let globalExtensions =
        native {
            let! pCount = 0u
            VkRaw.vkEnumerateDeviceExtensionProperties(handle, null, pCount, NativePtr.zero)
                |> check "could not get device-extensions"
                
            let props = Array.zeroCreate (int !!pCount)
            let! ptr = props
            VkRaw.vkEnumerateDeviceExtensionProperties(handle, null, pCount, ptr)
                |> check "could not get device-layers"
            return props |> Array.map ExtensionInfo.ofVulkan
        }

    let hasInstanceExtension (name : string) =
        enabledInstanceExtensions |> List.exists (fun e -> e = name)

    let hasExtension (name : string) =
        globalExtensions |> Array.exists (fun e -> e.name = name)

    let features =
        let inline readOrEmpty (ptr : nativeptr< ^a>) =
            if NativePtr.isNull ptr then
                ((^a) : (static member Empty : ^a) ())
            else
                !!ptr

        let f, pm, ycbcr, s16, vp, sdp, idx, rtp, acc, rq, bda =
            use chain = new VkStructChain()
            let pMem        = chain.Add<VkPhysicalDeviceProtectedMemoryFeatures>()
            let pYcbcr      = chain.Add<VkPhysicalDeviceSamplerYcbcrConversionFeatures>()
            let p16bit      = chain.Add<VkPhysicalDevice16BitStorageFeatures>()
            let pVarPtrs    = chain.Add<VkPhysicalDeviceVariablePointersFeatures>()
            let pDrawParams = chain.Add<VkPhysicalDeviceShaderDrawParametersFeatures>()
            let pIdx        = if hasExtension EXTDescriptorIndexing.Name then chain.Add<VkPhysicalDeviceDescriptorIndexingFeaturesEXT>() else NativePtr.zero
            let pRTP        = if hasExtension KHRRayTracingPipeline.Name then chain.Add<VkPhysicalDeviceRayTracingPipelineFeaturesKHR>() else NativePtr.zero
            let pAcc        = if hasExtension KHRAccelerationStructure.Name then chain.Add<VkPhysicalDeviceAccelerationStructureFeaturesKHR>() else NativePtr.zero
            let pRQ         = if hasExtension KHRRayQuery.Name then chain.Add<VkPhysicalDeviceRayQueryFeaturesKHR>() else NativePtr.zero
            let pDevAddr    = if hasExtension KHRBufferDeviceAddress.Name then chain.Add<VkPhysicalDeviceBufferDeviceAddressFeaturesKHR>() else NativePtr.zero
            let pFeatures   = chain.Add<VkPhysicalDeviceFeatures2>()

            VkRaw.vkGetPhysicalDeviceFeatures2(handle, VkStructChain.toNativePtr chain)
            (!!pFeatures).features, !!pMem, !!pYcbcr, !!p16bit, !!pVarPtrs, !!pDrawParams, readOrEmpty pIdx,
            readOrEmpty pRTP, readOrEmpty pAcc, readOrEmpty pRQ, readOrEmpty pDevAddr

        DeviceFeatures.create pm ycbcr s16 vp sdp idx rtp acc rq bda f

    let properties, raytracingProperties =
        use chain = new VkStructChain()

        let pRTP, pAcc =
            if hasExtension KHRRayTracingPipeline.Name then
                chain.Add<VkPhysicalDeviceRayTracingPipelinePropertiesKHR>(),
                chain.Add<VkPhysicalDeviceAccelerationStructurePropertiesKHR>()
            else
                NativePtr.zero, NativePtr.zero

        let pProperties = chain.Add<VkPhysicalDeviceProperties2>()

        VkRaw.vkGetPhysicalDeviceProperties2(handle, VkStructChain.toNativePtr chain)
        let props = (!!pProperties).properties

        if hasExtension KHRRayTracingPipeline.Name then
            props, Some(!!pRTP, !!pAcc)
        else
            props, None

    let name = properties.deviceName.Value
    let driverVersion = Version.FromVulkan properties.driverVersion
    let apiVersion = Version.FromVulkan properties.apiVersion


    let maxAllocationSize, maxPerSetDescriptors =
        if apiVersion >= Version(1,1,0) || hasExtension KHRMaintenance3.Name then
            let main3 = 
                VkPhysicalDeviceMaintenance3Properties(10u, 10UL)
            main3 |> pin (fun pMain3 ->
                let props = 
                    VkPhysicalDeviceProperties2(
                        NativePtr.toNativeInt pMain3,
                        VkPhysicalDeviceProperties()
                    )
                props |> pin (fun pProps ->
                    VkRaw.vkGetPhysicalDeviceProperties2(handle, pProps)
                    let props = NativePtr.read pProps
                    let main3 = NativePtr.read pMain3

                    let maxMemoryAllocationSize = min (uint64 Int64.MaxValue) main3.maxMemoryAllocationSize |> int64
                    let maxPerSetDescriptors = min (uint32 Int32.MaxValue) main3.maxPerSetDescriptors |> int

                    maxMemoryAllocationSize, maxPerSetDescriptors
                )
            )
        else
            Int64.MaxValue, Int32.MaxValue

    let uniqueId, deviceMask =
        if apiVersion >= Version(1,1,0) || hasInstanceExtension "VK_KHR_get_physical_device_properties2" then
            let id =
                KHRExternalMemoryCapabilities.VkPhysicalDeviceIDPropertiesKHR(
                    Guid.Empty,
                    Guid.Empty,
                    byte_8 (),
                    0u,
                    0u
                )
            id |> pin (fun pId ->
                let khrProps = 
                    VkPhysicalDeviceProperties2KHR(
                        NativePtr.toNativeInt pId,
                        VkPhysicalDeviceProperties()
                    )
                khrProps |> pin (fun pProps ->
                    VkRaw.vkGetPhysicalDeviceProperties2(handle, pProps)
                    let id = NativePtr.read pId
                    let uid = sprintf "{ GUID = %A; Mask = %d }" id.deviceUUID id.deviceNodeMask
                    uid, id.deviceNodeMask
                )
            )
        else
            let uid = Guid.NewGuid() |> string
            let mask = 1u
            uid, mask
     

    let limits = DeviceLimits.create (Mem maxAllocationSize) raytracingProperties properties.limits
    let vendor = PCI.vendorName (int properties.vendorID)


    

    let queueFamilyInfos =
        native {
            let! pCount = 0u
            VkRaw.vkGetPhysicalDeviceQueueFamilyProperties(handle, pCount, NativePtr.zero)
            
            let props = Array.zeroCreate (int !!pCount)
            let! ptr = props
            VkRaw.vkGetPhysicalDeviceQueueFamilyProperties(handle, pCount, ptr)  

            return props |> Array.mapi (fun i p ->
                {
                    index                       = i
                    count                       = int p.queueCount
                    flags                       = unbox (int p.queueFlags)
                    minImgTransferGranularity   = V3i.OfExtent p.minImageTransferGranularity
                    timestampBits               = int p.timestampValidBits
                }
            )
        }

    let mutable memoryProperties =
        native {
            let! pProps = VkPhysicalDeviceMemoryProperties()
            VkRaw.vkGetPhysicalDeviceMemoryProperties(handle, pProps)
            return !!pProps
        }
        
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
                let props =
                    temporary<VkFormatProperties,_> (fun pProps ->
                        VkRaw.vkGetPhysicalDeviceFormatProperties(handle, fmt, pProps)
                        NativePtr.read pProps
                    )
                yield fmt, props
        ]

    let imageFormatProperties =
        FastConcurrentDict()

    let hostMemory = memoryTypes |> Array.maxBy MemoryInfo.hostScore
    let deviceMemory = memoryTypes |> Array.maxBy MemoryInfo.deviceScore
    
    member x.MaxAllocationSize = maxAllocationSize
    member x.MaxPerSetDescriptors = maxPerSetDescriptors

    member x.GetFormatFeatures(tiling : VkImageTiling, fmt : VkFormat) =
        match tiling with
        | VkImageTiling.Linear -> formatProperties.[fmt].linearTilingFeatures
        | _ -> formatProperties.[fmt].optimalTilingFeatures

    member x.GetImageFormatProperties(format : VkFormat, typ : VkImageType, tiling : VkImageTiling, usage : VkImageUsageFlags, flags : VkImageCreateFlags) =
        let key = (format, typ, tiling, usage, flags)
        imageFormatProperties.GetOrCreate(key, fun _ ->
            temporary<VkImageFormatProperties,_> (fun pProps ->
                VkRaw.vkGetPhysicalDeviceImageFormatProperties(x.Handle, format, typ, tiling, usage, flags, pProps)
                    |> check "could not query image format properties"

                NativePtr.read pProps
            )
        )

    member x.GetBufferFormatFeatures(fmt : VkFormat) =
        formatProperties.[fmt].bufferFeatures

    member x.AvailableLayers = availableLayers
    member x.GlobalExtensions : ExtensionInfo[] = globalExtensions
    member x.QueueFamilies = queueFamilyInfos
    member x.MemoryTypes = memoryTypes
    member x.Heaps = heaps

    member x.Handle = handle
    //member x.Index : int = index
    member x.Vendor = vendor
    member x.Name = name
    member x.Type = properties.deviceType
    member x.APIVersion = apiVersion
    member x.DriverVersion = driverVersion

    member x.HostMemory = hostMemory
    member x.DeviceMemory = deviceMemory

    member x.Instance = instance
    member x.Features : DeviceFeatures = features
    member x.Limits : DeviceLimits = limits

    abstract member DeviceMask : uint32
    default x.DeviceMask = deviceMask

    abstract member Id : string
    default x.Id = uniqueId

    override x.ToString() =
        sprintf "{ name = %s; type = %A; api = %A }" name x.Type x.APIVersion
    

and PhysicalDeviceGroup internal(instance : Instance, devices : PhysicalDevice[], enabledInstanceExtensions : list<string>, apiVersion : Version) =
    inherit PhysicalDevice(instance, devices.[0].Handle, enabledInstanceExtensions, apiVersion)
   
    let mask = devices |> Seq.map (fun d -> d.DeviceMask) |> Seq.fold (|||) 0u

    member x.Devices : PhysicalDevice[] = devices
    override x.Id = devices |> Seq.map (fun d -> d.Id) |> String.concat "_"

    override x.DeviceMask = mask

    override x.ToString() =
        let cnt = devices.Length
        sprintf "%d x { name = %s; type = %A; api = %A }" cnt x.Name x.Type x.APIVersion
    


            
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Instance =
    module Extensions =
        let DebugReport                     = EXTDebugReport.Name
        let DebugUtils                      = EXTDebugUtils.Name
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

        let Raytracing = [
                KHRRayTracingPipeline.Name
                KHRAccelerationStructure.Name
                KHRBufferDeviceAddress.Name
                KHRDeferredHostOperations.Name
                EXTDescriptorIndexing.Name
                KHRSpirv14.Name
                KHRShaderFloatControls.Name
            ]

        let Sharing = [
                // instance
                KHRExternalMemoryCapabilities.Name
                KHRGetPhysicalDeviceProperties2.Name
                KHRExternalSemaphoreCapabilities.Name

                // device
                KHRExternalSemaphore.Name
                KHRExternalMemory.Name
                // device win32
                KHRExternalSemaphoreWin32.Name
                KHRExternalMemoryWin32.Name
                // device fd
                KHRExternalSemaphoreFd.Name
                KHRExternalMemoryFd.Name
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
        let AssistantLayer      = "VK_LAYER_LUNARG_assistant_layer"
        let Validation          = "VK_LAYER_KHRONOS_validation"
        let Nsight              = "VK_LAYER_NV_nsight"



type CustomDeviceChooser private() =
    static let mutable choose : Option<seq<PhysicalDevice> -> PhysicalDevice> = None

    static member Register(c : seq<PhysicalDevice> -> PhysicalDevice) =
        choose <- Some c

    static member Filter(devices : seq<PhysicalDevice>) =
        match choose with
        | Some c -> Seq.singleton(c devices)
        | None -> devices


    


module ConsoleDeviceChooser =
    open System.IO
    open System.Reflection
    open System.Diagnostics
    open System.Runtime.InteropServices

    type private KeyCode =
        | LeftAlt = 0xA4
        | RightAlt = 0xA5
        | LeftShift = 0xA0
        | RightShift = 0xA1

    module private Win32 =
        [<DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)>]
        extern uint16 private GetKeyState(KeyCode keyCode)

        let isDown (key : KeyCode) =
            let state = GetKeyState(key) 
            (state &&& 0x8000us) = 0x8000us

    let private md5 = System.Security.Cryptography.MD5.Create()

    let private newHash() =
        Guid.NewGuid().ToByteArray() |> Convert.ToBase64String

    let private appHash =
        try
            let ass = Assembly.GetEntryAssembly()
            if isNull ass || String.IsNullOrWhiteSpace ass.Location then 
                newHash()
            else
                ass.Location 
                    |> System.Text.Encoding.Unicode.GetBytes
                    |> md5.ComputeHash
                    |> Convert.ToBase64String
                   
        with _ ->
            newHash()
               
    let private configFile =
        let configDir = Path.Combine(Path.GetTempPath(), "vulkan")

        if not (Directory.Exists configDir) then
            Directory.CreateDirectory configDir |> ignore


        let fileName = appHash.Replace('/', '_')
        Path.Combine(configDir, sprintf "%s.vkconfig" fileName)

    let run' (preferred : Option<PhysicalDevice>) (devices : seq<PhysicalDevice>) =
        let devices = Seq.toList devices
        match devices with
            | [single] -> single
            | _ -> 
                let allIds = devices |> List.map (fun d -> d.Id) |> String.concat ";"

                let choose() =
                    let devices = List.toArray devices
                    Log.line "Multiple GPUs detected (please select one)"
                    for i in 0 .. devices.Length - 1 do
                        let d = devices.[i]
                        
                        let prefix =
                            match d with
                                | :? PhysicalDeviceGroup as g -> sprintf "%d x "g.Devices.Length
                                | _ -> ""

                        Log.line "   %d: %s%s %s" i prefix d.Vendor d.Name
                    
                    let mutable chosenId = -1
                    while chosenId < 0 do
                        printf " 0: "
                        let entry = Console.ReadLine()
                        match Int32.TryParse(entry) with
                            | (true, v) when v >= 0 && v < devices.Length ->
                                let d = devices.[v]
                                File.WriteAllLines(configFile, [ allIds; d.Id ])
                                chosenId <- v
                            | _ ->
                                ()
                            
                    File.WriteAllLines(configFile, [ allIds; devices.[chosenId].Id ])
                    devices.[chosenId]

                let altDown = 
                    match System.Environment.OSVersion with
                        | Windows -> Win32.isDown KeyCode.LeftAlt || Win32.isDown KeyCode.RightAlt
                        | _ -> false

                if altDown then
                    choose()
                else

                    match preferred with
                        | Some pref -> 
                            pref
                        | _ ->
                            if File.Exists configFile && not altDown then
                                let cache = File.ReadAllLines configFile
                                match cache with
                                    | [| fAll; fcache |] when fAll = allIds ->

                                        match devices |> List.tryFind (fun d -> d.Id = fcache) with
                                            | Some d -> d
                                            | _ -> choose()

                                    | _ ->
                                        choose()
                            else
                                choose()
             
    
    let run (devices : seq<PhysicalDevice>) : PhysicalDevice =
        run' None devices
        