namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Generic
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

#nowarn "9"
#nowarn "51"

[<AutoOpen>]
module private Utilities =
    let check (str : string) (err : VkResult) =
        if err <> VkResult.VkSuccess then failwithf "[Vulkan] %s" str

    let inline failf fmt = Printf.kprintf (fun str -> failwith ("[Vulkan] " + str)) fmt

    module NativePtr =
        let withA (f : nativeptr<'a> -> 'b) (a : 'a[]) =
            let gc = GCHandle.Alloc(a, GCHandleType.Pinned)
            try f (gc.AddrOfPinnedObject() |> NativePtr.ofNativeInt)
            finally gc.Free()

    type Version with
        member v.ToVulkan() =
            ((uint32 v.Major) <<< 22) ||| ((uint32 v.Minor) <<< 12) ||| (uint32 v.Build)

        static member FromVulkan (v : uint32) =
            Version(int (v >>> 22), int ((v >>> 12) &&& 0x3FFu), int (v &&& 0xFFFu))

    type V2i with
        static member OfExtent (e : VkExtent2D) =
            V2i(int e.width, int e.height)
        
        member x.ToExtent() =
            VkExtent2D(uint32 x.X, uint32 x.Y)

    type V3i with
        static member OfExtent (e : VkExtent3D) =
            V3i(int e.width, int e.height, int e.depth)
        
        member x.ToExtent() =
            VkExtent3D(uint32 x.X, uint32 x.Y, uint32 x.Z)

    module VkRaw =
        let warn fmt = Printf.kprintf (fun str -> Report.Warn("[Vulkan] {0}", str)) fmt

        let debug fmt = Printf.kprintf (fun str -> Report.Line(2, "[Vulkan] {0}", str)) fmt

type ExtensionInfo =
    {
        name            : string
        specification   : Version
    }

type LayerInfo =
    {
        name            : string
        description     : string
        implementation  : Version
        specification   : Version
        extensions      : list<ExtensionInfo>
    }

[<Flags>]
type QueueFlags = 
    | None              = 0x00000000
    | Graphics          = 0x00000001
    | Compute           = 0x00000002
    | Transfer          = 0x00000004
    | SparseBinding     = 0x00000008

type QueueFamilyInfo =
    {
        index                       : int
        count                       : int
        flags                       : QueueFlags
        minImgTransferGranularity   : V3i
        timestampBits               : int
    }

[<Flags>]
type MemoryHeapFlags =
    | None = 0
    | DeviceLocalBit = 0x00000001

type MemoryHeapInfo =
    {
        index           : int
        size            : Mem
        flags           : MemoryHeapFlags
    }

[<Flags>]
type MemoryFlags =
    | None              = 0x00000000
    | DeviceLocal       = 0x00000001
    | HostVisible       = 0x00000002
    | HostCoherent      = 0x00000004
    | HostCached        = 0x00000008
    | LazilyAllocated   = 0x00000010

type MemoryInfo =
    {
        index           : int
        heap            : MemoryHeapInfo
        flags           : MemoryFlags
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ExtensionInfo =
    let inline name (e : ExtensionInfo) = e.name
    let inline specification (e :ExtensionInfo) = e.specification

    let internal ofVkExtensionsProperties (e : VkExtensionProperties) =
        { 
            ExtensionInfo.name = e.extensionName.Value
            ExtensionInfo.specification = Version.FromVulkan e.specVersion 
        }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LayerInfo =
    let inline name (e : LayerInfo) = e.name
    let inline description (e : LayerInfo) = e.name
    let inline implementation (e : LayerInfo) = e.implementation
    let inline specification (e : LayerInfo) = e.specification
    let inline extensions (e : LayerInfo) = e.extensions

    let internal ofVkInstanceLayerProperties (prop : VkLayerProperties) =
        let name = prop.layerName.Value
        let mutable count = 0u
        VkRaw.vkEnumerateInstanceExtensionProperties(name, &&count, NativePtr.zero)
            |> check "could not get available instance layers"

        let properties = Array.zeroCreate (int count)
        properties |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumerateInstanceExtensionProperties(name, &&count, ptr)
                |> check "could not get available instance layers"
        )

        let layerExtensions = 
            properties 
                |> Array.toList 
                |> List.map ExtensionInfo.ofVkExtensionsProperties

        {
            LayerInfo.name = prop.layerName.Value
            LayerInfo.description = prop.description.Value
            LayerInfo.implementation = Version.FromVulkan prop.implementationVersion
            LayerInfo.specification = Version.FromVulkan prop.specVersion
            LayerInfo.extensions = layerExtensions
        }

    let internal ofVkDeviceLayerProperties (device : VkPhysicalDevice) (prop : VkLayerProperties) =
        let name = prop.layerName.Value
        let mutable count = 0u
        VkRaw.vkEnumerateDeviceExtensionProperties(device, name, &&count, NativePtr.zero)
            |> check "could not get available instance layers"

        let properties = Array.zeroCreate (int count)
        properties |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumerateDeviceExtensionProperties(device, name, &&count, ptr)
                |> check "could not get available instance layers"
        )

        let layerExtensions = 
            properties 
                |> Array.toList 
                |> List.map ExtensionInfo.ofVkExtensionsProperties

        {
            LayerInfo.name = prop.layerName.Value
            LayerInfo.description = prop.description.Value
            LayerInfo.implementation = Version.FromVulkan prop.implementationVersion
            LayerInfo.specification = Version.FromVulkan prop.specVersion
            LayerInfo.extensions = layerExtensions
        }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module QueueFlags =
    let inline graphics (f : QueueFlags) = (f &&& QueueFlags.Graphics) <> QueueFlags.None
    let inline compute (f : QueueFlags) = (f &&& QueueFlags.Compute) <> QueueFlags.None
    let inline transfer (f : QueueFlags) = (f &&& QueueFlags.Transfer) <> QueueFlags.None
    let inline sparseBinding (f : QueueFlags) = (f &&& QueueFlags.SparseBinding) <> QueueFlags.None


    let score (f : QueueFlags) =
        let mutable res = 0
        if graphics f then res <- res + 16
        if compute f then res <- res + 8
        if transfer f then res <- res + 4
        if sparseBinding f then res <- res + 2
        res

    let transferOnly (f : QueueFlags) =
        if graphics f || compute f then false
        else transfer f

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module QueueFamilyInfo =
    let inline index (qf : QueueFamilyInfo) = qf.index
    let inline count (qf : QueueFamilyInfo) = qf.count
    let inline flags (qf : QueueFamilyInfo) = qf.flags
    let inline minImgTransferGranularity (qf : QueueFamilyInfo) = qf.minImgTransferGranularity
    let inline timestampBits (qf : QueueFamilyInfo) = qf.timestampBits

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MemoryFlags =
    let inline deviceLocal (f : MemoryFlags) = (f &&& MemoryFlags.DeviceLocal) <> MemoryFlags.None
    let inline hostVisible (f : MemoryFlags) = (f &&& MemoryFlags.HostVisible) <> MemoryFlags.None
    let inline hostCoherent (f : MemoryFlags) = (f &&& MemoryFlags.HostCoherent) <> MemoryFlags.None
    let inline hostCached (f : MemoryFlags) = (f &&& MemoryFlags.HostCached) <> MemoryFlags.None
    let inline lazilyAllocated (f : MemoryFlags) = (f &&& MemoryFlags.LazilyAllocated) <> MemoryFlags.None


    let internal deviceScore (f : MemoryFlags) =
        if deviceLocal f then
            let mutable res = 16

            if hostVisible f then res <- res + 8
            if hostCoherent f then res <- res + 4
            if hostCached f then res <- res + 2
            if lazilyAllocated f then res <- res + 2
            res
        else
            0

    let internal hostScore (f : MemoryFlags) =
        if hostVisible f then
            let mutable res = 8
            if hostCoherent f then res <- res + 4
            if hostCached f then res <- res + 2
            if lazilyAllocated f then res <- res + 1
            res
        else
            0


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MemoryInfo =
    let inline index (info : MemoryInfo) = info.index
    let inline heap (info : MemoryInfo) = info.heap
    let inline flags (info : MemoryInfo) = info.flags

    let internal deviceScore (info : MemoryInfo) =
        (MemoryFlags.deviceScore info.flags, info.heap.size.Bytes)

    let internal hostScore (info : MemoryInfo) =
        (MemoryFlags.hostScore info.flags, info.heap.size.Bytes)


type Instance(apiVersion : Version, layers : Set<string>, extensions : Set<string>) as this =   
    static let availableLayers =
        let mutable count = 0u
        VkRaw.vkEnumerateInstanceLayerProperties(&&count, NativePtr.zero)
            |> check "could not get available instance layers"

        let properties = Array.zeroCreate (int count)
        properties |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumerateInstanceLayerProperties(&&count, ptr)
                |> check "could not get available instance layers"
        )
        properties |> Array.map LayerInfo.ofVkInstanceLayerProperties


    static let globalExtensions =
        let mutable count = 0u
        VkRaw.vkEnumerateInstanceExtensionProperties(null, &&count, NativePtr.zero)
            |> check "could not get available instance layers"

        let properties = Array.zeroCreate (int count)
        properties |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumerateInstanceExtensionProperties(null, &&count, ptr)
                |> check "could not get available instance layers"
        )
        properties |> Array.map ExtensionInfo.ofVkExtensionsProperties


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


    let mutable isDisposed = 0
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


    

    member x.Dispose() =
        let o = Interlocked.Exchange(&isDisposed, 1)
        if o = 0 then
            VkRaw.vkDestroyInstance(instance, NativePtr.zero)
            instance <- VkInstance.Zero

    member x.Handle = instance

    member x.Devices = devices

    member x.IsDisposed = isDisposed <> 0

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    static member AvailableLayers = availableLayers
    static member GlobalExtensions = globalExtensions

and PhysicalDevice internal(instance : Instance, handle : VkPhysicalDevice, index : int) =

    let availableLayers = 
        let mutable count = 0u
        VkRaw.vkEnumerateDeviceLayerProperties(handle, &&count, NativePtr.zero)
            |> check "could not get device-layers"

        let props = Array.zeroCreate (int count)
        props |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumerateDeviceLayerProperties(handle, &&count, ptr)
                |> check "could not get device-layers"
        )

        props |> Array.map (LayerInfo.ofVkDeviceLayerProperties handle)

    let globalExtensions =
        let mutable count = 0u
        VkRaw.vkEnumerateDeviceExtensionProperties(handle, null, &&count, NativePtr.zero)
            |> check "could not get device-extensions"

        let props = Array.zeroCreate (int count)
        props |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumerateDeviceExtensionProperties(handle, null, &&count, ptr)
                |> check "could not get device-layers"
        )
        props |> Array.map ExtensionInfo.ofVkExtensionsProperties

    let mutable properties = VkPhysicalDeviceProperties()
    do VkRaw.vkGetPhysicalDeviceProperties(handle, &&properties)

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
            { MemoryHeapInfo.index = i; MemoryHeapInfo.size = Mem info.size; MemoryHeapInfo.flags = unbox (int info.flags) }
        )

    let memoryTypes =
        Array.init (int memoryProperties.memoryTypeCount) (fun i ->
            let info = memoryProperties.memoryTypes.[i]
            { MemoryInfo.index = i; MemoryInfo.heap = heaps.[int info.heapIndex]; MemoryInfo.flags = unbox (int info.propertyFlags) }
        )
        

    let deviceMemory = memoryTypes |> Array.maxBy MemoryInfo.deviceScore
    let hostMemory = memoryTypes |> Array.maxBy MemoryInfo.hostScore

    let mainQueue = queueFamilyInfos |> Array.maxBy (QueueFamilyInfo.flags >> QueueFlags.score)
    let pureTransferQueue = queueFamilyInfos |> Array.tryFind (QueueFamilyInfo.flags >> QueueFlags.transferOnly)

    member x.AvailableLayers = availableLayers
    member x.GlobalExtensions = globalExtensions
    member x.QueueFamilies = queueFamilyInfos
    member x.MainQueue = mainQueue
    member x.TransferQueue = pureTransferQueue
    member x.MemoryTypes = memoryTypes
    member x.Heaps = heaps

    member x.Handle = handle
    member x.Index = index
    member x.Name = name
    member x.Type = properties.deviceType
    member x.APIVersion = apiVersion
    member x.DriverVersion = driverVersion

    member x.DeviceMemory = deviceMemory
    member x.HostMemory = hostMemory

    member x.Instance = instance


    member x.CreateDevice(layers : Set<string>, extensions : Set<string>, queues : list<QueueFamilyInfo * int>) =
        new Device(x, layers, extensions, queues)


    override x.ToString() =
        sprintf "{ name = %s; type = %A; api = %A }" name x.Type x.APIVersion

and Device internal(physical : PhysicalDevice, wantedLayers : Set<string>, wantedExtensions : Set<string>, queues : list<QueueFamilyInfo * int>) as this =

    let layers, extensions =
        let availableExtensions = physical.GlobalExtensions |> Seq.map (fun e -> e.name.ToLower(), e.name) |> Dictionary.ofSeq
        let availableLayerNames = physical.AvailableLayers |> Seq.map (fun l -> l.name.ToLower(), l) |> Map.ofSeq
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
    let mutable isDisposed = 0

    let queueInfos = 
        queues |> List.toArray |> Array.choose (fun (q,c) ->
            if c < 0 then 
                None
            else
                let count = 
                    if c > q.count then
                        VkRaw.warn "could not create %d queues for family %A (only %d available)" c q.index q.count
                        q.count
                    else
                        c 

                let result =
                    VkDeviceQueueCreateInfo(
                        VkStructureType.DeviceQueueCreateInfo, 0n,
                        0u,
                        uint32 q.index,
                        uint32 count,
                        NativePtr.zero
                    )

                Some result
        )

    let instance = physical.Instance
    let mutable device =
        queueInfos |> NativePtr.withA (fun ptr ->
            let layers = Set.toArray layers
            let extensions = Set.toArray extensions
            let pLayers = CStr.sallocMany layers
            let pExtensions = CStr.sallocMany extensions

            let mutable features = VkPhysicalDeviceFeatures()
            VkRaw.vkGetPhysicalDeviceFeatures(physical.Handle, &&features)

            let mutable info =
                VkDeviceCreateInfo(
                    VkStructureType.DeviceCreateInfo, 0n,
                    0u,
                    uint32 queueInfos.Length, ptr,
                    uint32 layers.Length, pLayers,
                    uint32 extensions.Length, pExtensions,
                    &&features
                )

            let mutable device = VkDevice.Zero
            VkRaw.vkCreateDevice(physical.Handle, &&info, NativePtr.zero, &&device)
                |> check "could not create device"

            device
        )


    let queues = 
        [
            for info in queueInfos do
                let family = physical.QueueFamilies.[int info.queueFamilyIndex]
                for i in 0 .. int info.queueCount - 1 do
                    yield family, DeviceQueue(this, device, family, i)
        ]
        |> Seq.groupBy fst 
        |> Seq.map (fun (k, vs) -> k, Seq.toArray (Seq.map snd vs))
        |> HashMap.ofSeq

    let heaps = physical.Heaps |> Array.map DeviceHeap
    
    let memories = 
        physical.MemoryTypes |> Array.map (fun t ->
            DeviceMemory(this, t, heaps.[t.heap.index])
        )

    let deviceMemory = memories.[physical.DeviceMemory.index]
    let hostMemory = memories.[physical.HostMemory.index]

    member x.Instance = instance

    member x.Queues = queues

    member x.IsDisposed = instance.IsDisposed || isDisposed <> 0

    member x.Memories = memories
    member x.DeviceMemory = deviceMemory
    member x.HostMemory = hostMemory

    member x.Dispose() =
        if not instance.IsDisposed then
            let o = Interlocked.Exchange(&isDisposed, 1)
            if o = 0 then
                VkRaw.vkDestroyDevice(device, NativePtr.zero)
                device <- VkDevice.Zero

    member x.Handle = device

    member x.PhysicalDevice = physical

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and DeviceQueue internal(device : Device, deviceHandle : VkDevice, family : QueueFamilyInfo, index : int) =
    let mutable handle = VkQueue.Zero
    do VkRaw.vkGetDeviceQueue(deviceHandle, uint32 family.index, uint32 index, &&handle)

    member x.Device = device
    member x.Family = family
    member x.Index = index
    member x.Handle = handle

and internal DeviceHeap(heap : MemoryHeapInfo) =
    let size = heap.size.Bytes
    let mutable allocated = 0L

    member x.TryAdd(size : int64) =
        Interlocked.Change(&allocated, fun v ->
            if v + size > size then (v, false)
            else (v + size, true)
        )

    member inline x.TryAdd(size : VkDeviceSize) = x.TryAdd (int64 size)
    member inline x.TryAdd(size : Mem) = x.TryAdd size.Bytes

    member x.Remove(size : int64) = Interlocked.Add(&allocated, -size) |> ignore
    member inline x.Remove(size : VkDeviceSize) = x.Remove (int64 size)
    member inline x.Remove(size : Mem) = x.Remove size.Bytes

    member x.Info = heap
    member x.Index = heap.index
    member x.Allocated = Mem allocated
    member x.Available = Mem (size - allocated)
    member x.Size = size

and DeviceMemory internal(device : Device, memory : MemoryInfo, heap : DeviceHeap) =
    member x.Device = device
    member x.Info = memory

    member x.HeapFlags = heap.Info.flags
    member x.Flags = memory.flags
    member x.Available = heap.Available
    member x.Allocated = heap.Allocated
    member x.Size = heap.Size

    member x.TryAlloc(size : int64, [<Out>] ptr : byref<DevicePtr>) =
        if heap.TryAdd size then
            let mutable info =
                VkMemoryAllocateInfo(
                    VkStructureType.MemoryAllocateInfo, 0n,
                    uint64 size,
                    uint32 memory.index
                )

            let mutable mem = VkDeviceMemory.Null

            VkRaw.vkAllocateMemory(device.Handle, &&info, NativePtr.zero, &&mem)
                |> check "could not allocate memory"

            ptr <- DevicePtr(x, heap, mem, size)
            true
        else
            false

    member x.Alloc(size : int64) =
        match x.TryAlloc size with
            | (true, ptr) -> ptr
            | _ -> failf "could not allocate %A (only %A available)" (Mem size) heap.Available

    member x.Free(ptr : DevicePtr) =
        lock ptr (fun () ->
            if ptr.Handle.IsValid then
                heap.Remove ptr.Size
                VkRaw.vkFreeMemory(device.Handle, ptr.Handle, NativePtr.zero)
                ptr.Handle <- VkDeviceMemory.Null
                ptr.Size <- 0L
        )

and DevicePtr internal(memory : DeviceMemory, heap : DeviceHeap, handle : VkDeviceMemory, size : int64) =
    let mutable handle = handle
    let mutable size = size

    member x.Memory = memory

    member x.Handle
        with get() : VkDeviceMemory = handle
        and internal set h = handle <- h

    member x.Size
        with get() : int64 = size
        and internal set s = size <- s

    member x.IsNull = handle.IsNull
    member x.IsValid = handle.IsValid

    member x.Dispose() = memory.Free(x)
