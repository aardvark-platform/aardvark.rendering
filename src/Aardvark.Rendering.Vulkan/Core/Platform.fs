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

#nowarn "9"
#nowarn "51"

type Instance(apiVersion : Version, layers : Set<string>, extensions : Set<string>) as this =   
    inherit VulkanObject()

    let extensions = 
        extensions 
            |> Set.add KHRGetPhysicalDeviceProperties2.Name

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
                        //VkRaw.warn "could not enable instance-layer '%s' since it is not available" name
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
                        //VkRaw.warn "could not enable instance-extension '%s' since it is not available" name
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
                    VkInstanceCreateFlags.MinValue,
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

        devices |> Array.map (fun d -> PhysicalDevice(this, d, extensions))

    let groups =    
        let mutable groupCount = 0u

        VkRaw.vkEnumeratePhysicalDeviceGroups(instance, &&groupCount, NativePtr.zero)
            |> check "could not get physical device groups"


        let groups = Array.zeroCreate (int groupCount)
        groups |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumeratePhysicalDeviceGroups(instance, &&groupCount, ptr)
                |> check "could not get physical device groups"
        )

        groups |> Array.mapi (fun i d -> 
            let devices = 
                Array.init (int d.physicalDeviceCount) (fun ii ->
                    let handle = d.physicalDevices.[ii]
                    devices |> Array.find (fun dd -> dd.Handle = handle)
                )
            PhysicalDeviceGroup(this, devices, extensions)
        )
        |> Array.filter (fun g -> g.Devices.Length > 1)
      

    let devicesAndGroups =
        Array.append devices (groups |> Array.map (fun a -> a :> _))
    
    static member AvailableLayers = availableLayers
    static member GlobalExtensions = globalExtensions

    override x.Release() =
        VkRaw.vkDestroyInstance(instance, NativePtr.zero)
        instance <- VkInstance.Zero

    member x.EnabledLayers = layers
    member x.EnabledExtensions = extensions

    member x.Handle = instance

    member x.Devices = devicesAndGroups

    member x.PrintInfo(l : ILogger, chosenDevice : PhysicalDevice) =
        let devices =
            match chosenDevice with
                | :? PhysicalDeviceGroup as g -> g.Devices
                | _ -> [| chosenDevice |]

        let chosenDevices = HSet.ofArray devices

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
                    let isEnabled = Set.contains layer.name layers
                    let suffix = if isEnabled then "(X)" else "( )"
                    l.line "%s (v%A) %s" layer.name layer.specification suffix
            )

            l.section "extensions:" (fun () ->
                for ext in globalExtensions do
                    let isEnabled = Set.contains ext.name extensions
                    let suffix = if isEnabled then "(X)" else "( )"
                    l.line "%s (v%A) %s" ext.name ext.specification suffix
            )

            l.section "devices:" (fun () ->
                let mutable index = 0
                for d in devices do
                    let l =
                        if HSet.contains d chosenDevices then l
                        else l.WithVerbosity(l.Verbosity + 1)


                    l.section "%d:" index (fun () ->
                        if HSet.contains d chosenDevices then 
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

and PhysicalDevice internal(instance : Instance, handle : VkPhysicalDevice, enabledInstanceExtensions : Set<string>) =
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


    let maxAllocationSize, maxPerSetDescriptors =
        let mutable main3 = 
            VkPhysicalDeviceMaintenance3Properties(
                VkStructureType.PhysicalDeviceMaintenance3Properties, 0n, 10u, 10UL
            )

        let mutable props = 
            VkPhysicalDeviceProperties2KHR(
                VkStructureType.PhysicalDeviceProperties2,
                NativePtr.toNativeInt &&main3,
                VkPhysicalDeviceProperties()
            )

        VkRaw.vkGetPhysicalDeviceProperties2(handle, &&props)


        let maxMemoryAllocationSize = min (uint64 Int64.MaxValue) main3.maxMemoryAllocationSize |> int64
        let maxPerSetDescriptors = min (uint32 Int32.MaxValue) main3.maxPerSetDescriptors |> int

        maxMemoryAllocationSize, maxPerSetDescriptors
  
    let uniqueId, deviceMask =
        let mutable id =
            KHRExternalMemoryCapabilities.VkPhysicalDeviceIDPropertiesKHR(
                VkStructureType.PhysicalDeviceIdProperties,
                0n,
                Guid.Empty,
                Guid.Empty,
                byte_8 (),
                0u,
                0u
            )
            
        let mutable khrProps = 
            VkPhysicalDeviceProperties2KHR(
                VkStructureType.PhysicalDeviceProperties2,
                NativePtr.toNativeInt &&id,
                VkPhysicalDeviceProperties()
            )
        VkRaw.vkGetPhysicalDeviceProperties2(handle, &&khrProps)
        let uid = sprintf "{ GUID = %A; Mask = %d }" id.deviceUUID id.deviceNodeMask
        uid, id.deviceNodeMask
     

    let limits = DeviceLimits.ofVkDeviceLimits (Mem maxAllocationSize) properties.limits
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

    let hostMemory = memoryTypes |> Array.maxBy MemoryInfo.hostScore
    let deviceMemory = memoryTypes |> Array.maxBy MemoryInfo.deviceScore
    
    member x.MaxAllocationSize = maxAllocationSize
    member x.MaxPerSetDescriptors = maxPerSetDescriptors

    member x.GetFormatFeatures(tiling : VkImageTiling, fmt : VkFormat) =
        match tiling with
            | VkImageTiling.Linear -> formatProperties.[fmt].linearTilingFeatures
            | _ -> formatProperties.[fmt].optimalTilingFeatures

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
    member x.Limits : DeviceLimits = limits

    abstract member DeviceMask : uint32
    default x.DeviceMask = deviceMask

    abstract member Id : string
    default x.Id = uniqueId

    override x.ToString() =
        sprintf "{ name = %s; type = %A; api = %A }" name x.Type x.APIVersion
    

and PhysicalDeviceGroup internal(instance : Instance, devices : PhysicalDevice[], enabledInstanceExtensions : Set<string>) =
    inherit PhysicalDevice(instance, devices.[0].Handle, enabledInstanceExtensions)
   
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


        let Nsight              = "VK_LAYER_NV_nsight"
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PhysicalDevice =
    module Extensions =
        let SwapChain = "VK_KHR_swapchain"



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

    let run(devices : seq<PhysicalDevice>) =
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
                        | _ -> true

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

