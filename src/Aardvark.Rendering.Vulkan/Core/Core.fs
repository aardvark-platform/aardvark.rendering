namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Threading
open Microsoft.FSharp.NativeInterop
open Aardvark.Base


/// Instance represents a Vulkan Instance which can be created using a set of extensions/layers.
/// It also holds a list of associated PhysicalDevices which can in turn be used to create logical
/// Devices.
type Instance(appName : string, appVersion : Version, layers : list<string>, extensions : list<string>) =
    inherit Resource()

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
            if extensions |> Array.exists (fun n -> n = "VK_EXT_debug_report") then
                let adapter = DebugReport.Adapter(handle.Value, VkDebugReportFlagBitsEXT.All)
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
        let evt = e.Publish

        { new FSharp.Control.IEvent<DebugReport.Message> with
            member x.Subscribe (obs : IObserver<_>) = 
                obs.OnNext(DebugReport.startMessage)
                let s = evt.Subscribe(obs)
                { new IDisposable with
                    member x.Dispose() = 
                        obs.OnNext(DebugReport.stopMessage)
                        s.Dispose()
                }

            member x.AddHandler(h) =
                h.Invoke(null, DebugReport.startMessage)
                evt.AddHandler h

            member x.RemoveHandler(h) =
                h.Invoke(null, DebugReport.stopMessage)
                evt.RemoveHandler h
        }


    member x.CreateDevice(physical : PhysicalDevice, requestedQueues : Map<PhysicalQueueFamily, int>, layers : list<string>, extensions : list<string>) =
        let mutable features = physical.Features

        let queues = requestedQueues |> Map.toArray
        let queueInfos = NativePtr.stackalloc queues.Length

        if queues.Length = 0 then
            failf "cannot create a device without any queues"

        for i in 0..queues.Length-1 do
            let (family, count) = queues.[i]

            if count > family.QueueCount then
                failf "cannot allocate %d queues in family: %A" count family

            let info = 
                VkDeviceQueueCreateInfo(
                    VkStructureType.DeviceQueueCreateInfo,
                    0n, VkDeviceQueueCreateFlags.MinValue,
                    uint32 family.Index,
                    uint32 count, 
                    NativePtr.zero
                )  
            NativePtr.set queueInfos i info



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

                uint32 queues.Length,
                queueInfos, 
        
                uint32 layers.Length, 
                pLayers,
        
                uint32 extensions.Length,
                pExts,
        
                NativePtr.zero
            )
        let mutable device = 0n

        
        VkRaw.vkCreateDevice(physical.Handle, &&createInfo, NativePtr.zero, &&device) |> check "vkCreateDevice"

        new Device(x, physical, device, requestedQueues)

    member x.CreateDevice(physical : PhysicalDevice, queues : Map<PhysicalQueueFamily, int>) =
        x.CreateDevice(physical, queues, [], [])

    member x.CreateDevice(physical : PhysicalDevice, layers : list<string>, extensions : list<string>) =
        let defaultQueue =
            let families = physical.QueueFamilies
            if families.Length = 1 then
                Map.ofList [families.[0], 1]
            else
                failf "could not determine a default queue for device: %A" physical

        x.CreateDevice(physical, defaultQueue, layers, extensions)

    member x.CreateDevice(physical : PhysicalDevice) =
        x.CreateDevice(physical, [], [])

    override x.Release() =
        if debugAdapterAndEvent.IsValueCreated then
            match debugAdapterAndEvent.Value with
                | (Some a, _) -> a.Stop()
                | _ -> ()

        if handle.IsValueCreated then
            VkRaw.vkDestroyInstance(handle.Value, NativePtr.zero)

    new(appName, layers, extensions) = new Instance(appName, Version(1,0,0), layers, extensions)
    new(layers, extensions) = new Instance("Aardvark", Version(1,0,0), layers, extensions)
    new() = new Instance("Aardvark", Version(1,0,0), [], [])

    override x.ToString() =
        sprintf "Instance { Name = %s; Version = %A }" appName appVersion

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
                | 0x10DEu -> 
                    DeviceVendor.Nvidia

                | 0x163Cu | 0x8086u | 0x8087u ->
                    DeviceVendor.Intel

                | 0x1002u | 0x1022u ->
                    DeviceVendor.AMD

                | 0x5143u ->
                    DeviceVendor.Qualcomm

                | 0x10C3u ->
                    DeviceVendor.Samsung

                | 0x121Au ->
                    DeviceVendor.ThreeDFX

                | 0x13B5u ->
                    DeviceVendor.ARM

                | 0x14E4u ->
                    DeviceVendor.Broadcom

                | 0x102Bu ->
                    DeviceVendor.Matrox

                | 0x1039u ->
                    DeviceVendor.SiS

                | 0x1106u ->
                    DeviceVendor.VIA

                | _ -> 
                    DeviceVendor.Unknown
        )

    let queueFamilies =
        lazy (
            let mutable count = 0u
            VkRaw.vkGetPhysicalDeviceQueueFamilyProperties(handle, &&count, NativePtr.zero)
            
            NativePtr.stackallocWith (int count) (fun ptr ->
                VkRaw.vkGetPhysicalDeviceQueueFamilyProperties(handle, &&count, ptr)
                ptr 
                |> NativePtr.toArray (int count)
                |> Array.mapi (fun i props -> PhysicalQueueFamily(this, i, props))

            )
        )

    override x.ToString() =
        sprintf "Device { Vendor = %A; Name = %s }" vendor.Value properties.Value.deviceName.Value

    override x.GetHashCode() =
        handle.GetHashCode()

    override x.Equals o =
        match o with
            | :? PhysicalDevice as o -> handle = o.Handle
            | _ -> false

    interface IComparable with
        member x.CompareTo o =
            match o with
                | :? PhysicalDevice as o -> compare x.Handle o.Handle
                | _ -> failf "cannot compare PhysicalDevice to %A" o

    member x.Handle = handle
    member x.Vendor = vendor.Value
    member x.Name = properties.Value.deviceName.Value
    member x.DeviceId = properties.Value.deviceID
    member x.DeviceType = properties.Value.deviceType
    member x.Features = features.Value
    member x.Extensions = extensions.Value
    member x.Layers = layers.Value
    member x.Properties = properties.Value
    member x.MemoryProperties = memoryProperties.Value
    member x.MemoryHeaps = memoryHeaps.Value
    member x.MemoryTypes = memoryTypes.Value
    member x.QueueFamilies : PhysicalQueueFamily[] = queueFamilies.Value
 
/// PhysicalHeap represents a heap available on a PhysicalDevice which can be used to
/// allocate memory. It provides information about the memory's capabilities as well
/// as its size.
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

    override x.ToString() =
        sprintf "PhysicalHeap { Index = %A; Size = %A }" heapIndex size

    override x.GetHashCode() =
        HashCode.Combine(device.GetHashCode(), heapIndex.GetHashCode())

    override x.Equals o =
        match o with
            | :? PhysicalHeap as o -> heapIndex = o.HeapIndex && device = o.Device
            | _ -> false

    interface IComparable with
        member x.CompareTo o =
            match o with
                | :? PhysicalHeap as o -> 
                    let c = compare device o.Device
                    if c = 0 then
                        compare heapIndex o.HeapIndex
                    else
                        c
                | _ -> failf "cannot compare PhysicalHeap to %A" o

    member x.IsDeviceLocal = handle.flags.HasFlag(VkMemoryHeapFlags.DeviceLocalBit)

/// PhysicalMemory represents a "view" on a PhysicalHeap which extends the heap with
/// information about the memory's capabilities such as mappability, etc.
and PhysicalMemory(device : PhysicalDevice, typeIndex : int, memType : VkMemoryType, heap : PhysicalHeap) =
    member x.IsHostVisible = memType.propertyFlags.HasFlag(VkMemoryPropertyFlags.HostVisibleBit)
    member x.IsDeviceLocal = memType.propertyFlags.HasFlag(VkMemoryPropertyFlags.DeviceLocalBit)

    member x.Device = device
    member x.TypeIndex = typeIndex
    member x.Heap = heap
    member x.HeapIndex = heap.HeapIndex
    member x.HeapSize = heap.Size
    member x.Flags : VkMemoryPropertyFlags = memType.propertyFlags

    override x.ToString() =
        sprintf "PhysicalMemory { Index = %A; HeapIndex = %A; Flags = %A }" typeIndex heap.HeapIndex memType.propertyFlags

    override x.GetHashCode() =
        HashCode.Combine(device.GetHashCode(), typeIndex.GetHashCode())

    override x.Equals o =
        match o with
            | :? PhysicalMemory as o -> typeIndex = o.TypeIndex && device = o.Device
            | _ -> false

    interface IComparable with
        member x.CompareTo o =
            match o with
                | :? PhysicalMemory as o -> 
                    let c = compare device o.Device
                    if c = 0 then
                        compare typeIndex o.TypeIndex
                    else
                        c
                | _ -> failf "cannot compare PhysicalMemory to %A" o

/// PhysicalQueueFamily provides information about queue families available on
/// a specific PhysicalDevice. This includes a maximal number of queues available for the
/// family as well as capabilities provided by the queue.
and PhysicalQueueFamily(device : PhysicalDevice, index : int, properties : VkQueueFamilyProperties) =
    member x.Device = device
    member x.Index = index
    member x.Compute = properties.queueFlags.HasFlag(VkQueueFlags.ComputeBit)
    member x.Graphics = properties.queueFlags.HasFlag(VkQueueFlags.GraphicsBit)
    member x.QueueCount = int properties.queueCount

    override x.ToString() =
        sprintf "PhysicalQueueFamily { Index = %A; Flags = %A; Count = %A }" index properties.queueFlags properties.queueCount
    
    override x.GetHashCode() =
        HashCode.Combine(device.GetHashCode(), index.GetHashCode())

    override x.Equals o =
        match o with
            | :? PhysicalQueueFamily as o -> index = o.Index && device = o.Device
            | _ -> false

    interface IComparable with
        member x.CompareTo o =
            match o with
                | :? PhysicalQueueFamily as o -> 
                    let c = compare device o.Device
                    if c = 0 then
                        compare index o.Index
                    else
                        c
                | _ -> failf "cannot compare PhysicalQueueFamily to %A" o


/// Device represents a logical vulkan device which can be created using the instance.
/// It's the central thing in vulkan which is needed for all resource-creations and commands.
and Device(instance : Instance, physical : PhysicalDevice, handle : VkDevice, queueFamilies : Map<PhysicalQueueFamily, int>) as this =
    inherit Resource(instance)

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
            memories.Value 
                |> Array.filter (fun m -> m.IsHostVisible)
                |> Array.maxBy (fun (m : DeviceMemory) ->
                    let cached = m.PhysicalMemory.Flags.HasFlag(VkMemoryPropertyFlags.HostCachedBit)
                    let coeherent = m.PhysicalMemory.Flags.HasFlag VkMemoryPropertyFlags.HostCoherentBit
                    let lazyily = m.PhysicalMemory.Flags.HasFlag VkMemoryPropertyFlags.LazilyAllocatedBit
                    
                    (if cached then 1 else 0) +
                    (if coeherent then 1 else 0) +
                    (if lazyily then 1 else 0)
                   
                   )
        )

    let deviceLocalMemory =
        lazy (
            memories.Value |> Array.find (fun m -> m.IsDeviceLocal)
        )

    override x.Release() =
        if handle <> 0n then
            VkRaw.vkDestroyDevice(handle, NativePtr.zero)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    member x.QueueFamilies = queueFamilies
    member x.Handle = handle
    member x.Physical = physical
    member x.Instance = instance
    member x.MemoryHeaps = physical.MemoryHeaps
    member x.Memories = memories.Value

    member x.HostVisibleMemory = hostVisibleMemory.Value
    member x.DeviceLocalMemory = deviceLocalMemory.Value

/// DeviceMemory is the logical equivalent of PhysicalMemory but is associated with
/// a logical device and can therefore provide alloc/free functionality.
and DeviceMemory(device : Device, physical : PhysicalMemory, heap : PhysicalHeap) =
    member x.Device : Device = device
    member x.PhysicalMemory : PhysicalMemory = physical
    member x.Heap = heap
    member x.IsHostVisible = physical.IsHostVisible
    member x.IsDeviceLocal = physical.IsDeviceLocal
    member x.TypeIndex = physical.TypeIndex


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Instance =
    module Extensions =
        let DebugReport = "VK_EXT_debug_report"
    
    module Layers =
        let DrawState = "VK_LAYER_LUNARG_draw_state"