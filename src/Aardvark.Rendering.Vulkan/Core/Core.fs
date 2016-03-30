namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open Microsoft.FSharp.NativeInterop
open Aardvark.Base


/// Instance represents a Vulkan Instance which can be created using a set of extensions/layers.
/// It also holds a list of associated PhysicalDevices which can in turn be used to create logical
/// Devices.
type Instance(appName : string, appVersion : Version, layers : list<string>, extensions : list<string>) =
    static let globalLayers : Lazy<list<VkLayerProperties>> =
        Lazy (fun () -> 
            let mutable layerCount = 0u
            VkRaw.vkEnumerateInstanceLayerProperties(&&layerCount, NativePtr.zero) |> check "vkGetGlobalLayerProperties"

            NativePtr.stackallocWith (int layerCount) (fun arr ->
                VkRaw.vkEnumerateInstanceLayerProperties(&&layerCount, arr) |> check "vkGetGlobalLayerProperties"
                List.init (int layerCount) (fun i -> NativePtr.get arr i) 
            )
        )

    static let globalExtensions =
        lazy (
            let mutable layerCount = 0u
            VkRaw.vkEnumerateInstanceExtensionProperties(null, &&layerCount, NativePtr.zero) |> check "vkGetGlobalLayerProperties"

            NativePtr.stackallocWith (int layerCount) (fun arr ->
                VkRaw.vkEnumerateInstanceExtensionProperties(null, &&layerCount, arr) |> check "vkGetGlobalLayerProperties"
                List.init (int layerCount) (fun i -> NativePtr.get arr i)
            )
        )

    let layers =
        let globalLayers = globalLayers.Value |> List.map (fun l -> l.layerName.Value) |> Set.ofList

        [|
            for requested in layers do
                if Set.contains requested globalLayers then
                    yield requested
                else
                    warnf "could not enable instance layer %A since it is not available" requested
        |]

    let extensions =
        let globalExtensions = globalExtensions.Value |> List.map (fun l -> l.extensionName.Value) |> Set.ofList

        [|
            for requested in extensions do
                if Set.contains requested globalExtensions then
                    yield requested
                else
                    warnf "could not enable instance extension %A since it is not available" requested
        |]

    let handle =
        lazy (
            let appNameC = CStr.salloc appName
            let engineNameC = CStr.salloc "Aardvark"

            let mutable appInfo =
                VkApplicationInfo(
                    VkStructureType.ApplicationInfo,
                    0n,
                    appNameC,
                    appVersion.UInt32,
                    engineNameC,
                    Version(1,0,0).UInt32,
                    Version(1, 0, 1).UInt32
                )

            let pLayers = CStr.sallocMany layers
            let pExts = CStr.sallocMany extensions

            let mutable instanceInfo =
                VkInstanceCreateInfo(
                    VkStructureType.InstanceCreateInfo,
                    0n, VkInstanceCreateFlags.MinValue,
                    &&appInfo,

                    uint32 layers.Length,
                    pLayers,
                    uint32 extensions.Length,
                    pExts
                )

            let mutable instance = 0n
            VkRaw.vkCreateInstance(&&instanceInfo, NativePtr.zero, &&instance) |> check "vkCreateInstance"

            instance  
        
        )

    let physicalDevices =
        lazy (
            let mutable count = 0u
            VkRaw.vkEnumeratePhysicalDevices(handle.Value, &&count, NativePtr.ofNativeInt 0n) |> check "vkEnumeratePhysicalDevices"

            NativePtr.stackallocWith (int count) (fun arr ->
                VkRaw.vkEnumeratePhysicalDevices(handle.Value, &&count, arr) |> check "vkEnumeratePhysicalDevices"

                List.init (int count) (fun i -> new PhysicalDevice(NativePtr.get arr i))
            )
        )

    let debugAdapterAndEvent =
        lazy (
            if extensions |> Array.exists (fun n -> n = "DEBUG_REPORT") then
                let adapter = DebugReport.Adapter(handle.Value, DebugReport.VkDbgReportFlags.All)
                adapter.Start()
                (Some adapter, adapter.OnMessageEvent)
            else
                (None, Event<_>())
        )


    static member AvailableExtensions = globalExtensions.Value
    static member AvailableLayers = globalLayers.Value
    member x.Extensions = extensions
    member x.Layers = layers
    member x.PhysicalDevices = physicalDevices.Value
    member x.Handle = handle.Value

    [<CLIEvent>]
    member x.OnDebugMessage = 
        let (_,e) = debugAdapterAndEvent.Value
        e.Publish


    member x.CreateDevice(physical : PhysicalDevice, features : VkPhysicalDeviceFeatures, layers : list<string>, extensions : list<string>) =
        let mutable features = features

        let mutable queueInfo =
            VkDeviceQueueCreateInfo(
                VkStructureType.DeviceQueueCreateInfo,
                0n, VkDeviceQueueCreateFlags.MinValue,
                0u,
                1u, NativePtr.zero
            )


        let layers =
            let available = physical.Layers |> List.map (fun (l : VkLayerProperties) -> l.layerName.Value) |> Set.ofList
            [|
                for requested in layers do
                    if Set.contains requested available then yield requested
                    else warnf "could not enable device layer %A since it is not available" requested
            |]

        let extensions =
            let available = physical.Extensions |> List.map (fun (l : VkExtensionProperties) -> l.extensionName.Value) |> Set.ofList
            [|
                for requested in extensions do
                    if Set.contains requested available then yield requested
                    else warnf "could not enable device extension %A since it is not available" requested
            |]
   
        let pExts = CStr.sallocMany extensions
        let pLayers = CStr.sallocMany layers

        let mutable createInfo = 
            VkDeviceCreateInfo(
                VkStructureType.DeviceCreateInfo,
                0n, VkDeviceCreateFlags.MinValue,

                1u,
                &&queueInfo, 
        
                uint32 layers.Length, 
                pLayers,
        
                uint32 extensions.Length,
                pExts,
        
                NativePtr.zero
            )
        let mutable device = 0n

        
        VkRaw.vkCreateDevice(physical.Handle, &&createInfo, NativePtr.zero, &&device) |> check "vkCreateDevice"

        new Device(x, physical, device)



    member x.Dispose() =
        if debugAdapterAndEvent.IsValueCreated then
            match debugAdapterAndEvent.Value with
                | (Some a, _) -> a.Stop()
                | _ -> ()

        if handle.IsValueCreated then
            VkRaw.vkDestroyInstance(handle.Value, NativePtr.zero)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    new(appName, layers, extensions) = new Instance(appName, Version(1,0,0), layers, extensions)
    new(layers, extensions) = new Instance("Aardvark", Version(1,0,0), layers, extensions)
    new() = new Instance("Aardvark", Version(1,0,0), [], [])


/// PhysicalDevice represents a "real" hardware device with Vulkan functionality.
/// It provides several properties for a device (e.g. Memory Kinds/Heaps, vendor information, etc.)
and PhysicalDevice(handle : VkPhysicalDevice) as this =
    let features = 
        lazy (
            let mutable features = VkPhysicalDeviceFeatures()
            VkRaw.vkGetPhysicalDeviceFeatures(handle, &&features)

            features
        )

    let layers = 
        lazy (
            let mutable count = 0u
            VkRaw.vkEnumerateDeviceLayerProperties(handle, &&count, NativePtr.ofNativeInt 0n) |> check "vkGetPhysicalDeviceLayerProperties"

            NativePtr.stackallocWith (int count) (fun layerProperties ->
                VkRaw.vkEnumerateDeviceLayerProperties(handle, &&count, layerProperties) |> check "vkGetPhysicalDeviceLayerProperties"

                List.init (int count) (fun i -> NativePtr.get layerProperties i) 
            )
        )


    let extensions = 
        lazy (
            let mutable count = 0u
            VkRaw.vkEnumerateDeviceExtensionProperties(handle, null, &&count, NativePtr.ofNativeInt 0n) |> check "vkGetPhysicalDeviceExtensionProperties"

            NativePtr.stackallocWith (int count) (fun extensionProperties ->
                VkRaw.vkEnumerateDeviceExtensionProperties(handle, null, &&count, extensionProperties) |> check "vkGetPhysicalDeviceExtensionProperties"

                List.init (int count) (fun i -> NativePtr.get extensionProperties i)
            )
        )

    let queueProperties =
        lazy (
            let mutable count = 0u
            VkRaw.vkGetPhysicalDeviceQueueFamilyProperties(handle, &&count, NativePtr.zero) 

            NativePtr.stackallocWith (int count) (fun props ->
                VkRaw.vkGetPhysicalDeviceQueueFamilyProperties(handle, &&count, props)

                List.init (int count) (fun i -> NativePtr.get props i)
            )

        )

    let properties =
        lazy (
            let mutable properties = VkPhysicalDeviceProperties()
            VkRaw.vkGetPhysicalDeviceProperties(handle, &&properties)

            properties
        )

    let memoryProperties =
        lazy (
            let mutable mem = VkPhysicalDeviceMemoryProperties()
            VkRaw.vkGetPhysicalDeviceMemoryProperties(handle, &&mem) 
            mem
        )

    let memoryHeaps =
        lazy (
            let props = memoryProperties.Value
            Array.init (int props.memoryHeapCount) (fun i -> PhysicalHeap(this, i, props.memoryHeaps.[i]))
        )

    let memoryTypes =
        lazy (
            let props = memoryProperties.Value
            Array.init (int props.memoryTypeCount) (fun i -> PhysicalMemory(this, i, props.memoryTypes.[i], memoryHeaps.Value.[int props.memoryTypes.[i].heapIndex]))
        )


    let vendor =
        lazy (
            let props = properties.Value
            match props.vendorID with
                | 0x000010DEu -> 
                    DeviceVendor.Nvidia

                | 0x0000163Cu | 0x00008086u | 0x00008087u ->
                    DeviceVendor.Intel

                | 0x00001002u | 0x00001022u ->
                    DeviceVendor.AMD

                | 0x00005143u ->
                    DeviceVendor.Qualcomm

                | 0x000010C3u ->
                    DeviceVendor.Samsung

                | 0x0000121Au ->
                    DeviceVendor.ThreeDFX

                | 0x000013B5u ->
                    DeviceVendor.ARM

                | 0x000014E4u ->
                    DeviceVendor.Broadcom

                | 0x0000102Bu ->
                    DeviceVendor.Matrox

                | 0x00001039u ->
                    DeviceVendor.SiS

                | 0x00001106u ->
                    DeviceVendor.VIA

                | _ -> 
                    DeviceVendor.Unknown
        )


    member x.Handle = handle
    member x.Vendor = vendor.Value
    member x.Name = properties.Value.deviceName.Value
    member x.DeviceId = properties.Value.deviceID
    member x.DeviceType = properties.Value.deviceType
    member x.Features = features.Value
    member x.Extensions = extensions.Value
    member x.Layers = layers.Value
    member x.QueueFamilyProperties = queueProperties.Value
    member x.Properties = properties.Value
    member x.MemoryProperties = memoryProperties.Value
    member x.MemoryHeaps = memoryHeaps.Value
    member x.MemoryTypes = memoryTypes.Value

and PhysicalHeap(device : PhysicalDevice, heapIndex : int, handle : VkMemoryHeap) =
    let size = handle.size |> size_t
    let mutable allocatedBytes = 0L

    member x.Device = device
    member x.HeapIndex = heapIndex
    member x.Size = size
    member x.Used = size_t allocatedBytes
    member x.Free = size - size_t allocatedBytes

    member internal x.TryAdd(size : int64) =
        let newSize = System.Threading.Interlocked.Add(&allocatedBytes, size)
        if uint64 newSize > handle.size then
            System.Threading.Interlocked.Add(&allocatedBytes, -size) |> ignore
            false
        else
            true

    member internal x.Remove(size : int64) =
        System.Threading.Interlocked.Add(&allocatedBytes, -size) |> ignore


    member x.IsDeviceLocal = handle.flags.HasFlag(VkMemoryHeapFlags.DeviceLocalBit)

and PhysicalMemory(device : PhysicalDevice, typeIndex : int, memType : VkMemoryType, heap : PhysicalHeap) =
    member x.IsHostVisible = memType.propertyFlags.HasFlag(VkMemoryPropertyFlags.HostVisibleBit)
    member x.IsDeviceLocal = memType.propertyFlags.HasFlag(VkMemoryPropertyFlags.DeviceLocalBit)

    member x.Device = device
    member x.TypeIndex = typeIndex
    member x.Heap = heap
    member x.HeapIndex = heap.HeapIndex
    member x.HeapSize = heap.Size
    member x.Flags = memType.propertyFlags



and Device(instance : Instance, physical : PhysicalDevice, handle : VkDevice) as this =

    let memories : Lazy<DeviceMemory[]> =
        lazy (
            let heaps = physical.MemoryHeaps
            physical.MemoryTypes
                |> Array.map (fun t ->
                    let heap = heaps.[t.HeapIndex]
                    DeviceMemory(this, t, heap)
                )
        )

    let hostVisibleMemory =
        lazy (
            memories.Value |> Array.find (fun m -> m.IsHostVisible)
        )

    let deviceLocalMemory =
        lazy (
            memories.Value |> Array.find (fun m -> m.IsDeviceLocal)
        )

    member x.Handle = handle
    member x.MemoryHeaps = physical.MemoryHeaps
    member x.Memories = memories.Value

    member x.HostVisibleMemory = hostVisibleMemory.Value
    member x.DeviceLocalMemory = deviceLocalMemory.Value

and DeviceMemory(device : Device, physical : PhysicalMemory, heap : PhysicalHeap) =
    member x.Device : Device = device
    member x.PhysicalMemory = physical
    member x.Heap = heap
    member x.IsHostVisible = physical.IsHostVisible
    member x.IsDeviceLocal = physical.IsDeviceLocal
    member x.TypeIndex = physical.TypeIndex