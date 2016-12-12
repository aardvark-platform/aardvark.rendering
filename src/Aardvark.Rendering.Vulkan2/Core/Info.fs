namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open Aardvark.Base

#nowarn "51"

// =======================================================================
// Extension Info
// =======================================================================

type ExtensionInfo =
    {
        name            : string
        specification   : Version
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ExtensionInfo =
    let inline name (e : ExtensionInfo) = e.name
    let inline specification (e :ExtensionInfo) = e.specification

    let internal ofVulkan (e : VkExtensionProperties) =
        { 
            ExtensionInfo.name = e.extensionName.Value
            ExtensionInfo.specification = Version.FromVulkan e.specVersion 
        }


// =======================================================================
// Layer Info
// =======================================================================

type LayerInfo =
    {
        name            : string
        description     : string
        implementation  : Version
        specification   : Version
        extensions      : list<ExtensionInfo>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LayerInfo =
    let inline name (e : LayerInfo) = e.name
    let inline description (e : LayerInfo) = e.name
    let inline implementation (e : LayerInfo) = e.implementation
    let inline specification (e : LayerInfo) = e.specification
    let inline extensions (e : LayerInfo) = e.extensions

    let internal ofVulkan (prop : VkLayerProperties) =
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
                |> List.map ExtensionInfo.ofVulkan

        {
            LayerInfo.name = prop.layerName.Value
            LayerInfo.description = prop.description.Value
            LayerInfo.implementation = Version.FromVulkan prop.implementationVersion
            LayerInfo.specification = Version.FromVulkan prop.specVersion
            LayerInfo.extensions = layerExtensions
        }

    let internal ofVulkanDevice (device : VkPhysicalDevice) (prop : VkLayerProperties) =
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
                |> List.map ExtensionInfo.ofVulkan

        {
            LayerInfo.name = prop.layerName.Value
            LayerInfo.description = prop.description.Value
            LayerInfo.implementation = Version.FromVulkan prop.implementationVersion
            LayerInfo.specification = Version.FromVulkan prop.specVersion
            LayerInfo.extensions = layerExtensions
        }



// =======================================================================
// Queue Flags
// =======================================================================

[<Flags>]
type QueueFlags = 
    | None              = 0x00000000
    | Graphics          = 0x00000001
    | Compute           = 0x00000002
    | Transfer          = 0x00000004
    | SparseBinding     = 0x00000008
    | All               = 0x00000007


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module QueueFlags =
    let inline graphics (f : QueueFlags) = (f &&& QueueFlags.Graphics) <> QueueFlags.None
    let inline compute (f : QueueFlags) = (f &&& QueueFlags.Compute) <> QueueFlags.None
    let inline transfer (f : QueueFlags) = (f &&& QueueFlags.Transfer) <> QueueFlags.None
    let inline sparseBinding (f : QueueFlags) = (f &&& QueueFlags.SparseBinding) <> QueueFlags.None


    let internal score (f : QueueFlags) =
        let mutable res = 0
        if graphics f then res <- res + 16
        if compute f then res <- res + 8
        if transfer f then res <- res + 4
        if sparseBinding f then res <- res + 2
        res

    let internal transferOnly (f : QueueFlags) =
        if graphics f || compute f then false
        else transfer f

// =======================================================================
// QueueFamily Info
// =======================================================================

type QueueFamilyInfo =
    {
        index                       : int
        count                       : int
        flags                       : QueueFlags
        minImgTransferGranularity   : V3i
        timestampBits               : int
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module QueueFamilyInfo =
    let inline index (qf : QueueFamilyInfo) = qf.index
    let inline count (qf : QueueFamilyInfo) = qf.count
    let inline flags (qf : QueueFamilyInfo) = qf.flags
    let inline minImgTransferGranularity (qf : QueueFamilyInfo) = qf.minImgTransferGranularity
    let inline timestampBits (qf : QueueFamilyInfo) = qf.timestampBits


// =======================================================================
// Memory Heap Info
// =======================================================================

[<Flags>]
type MemoryHeapFlags =
    | None = 0
    | DeviceLocalBit = 0x00000001

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MemoryHeapFlags =
    let inline deviceLocal (f : MemoryHeapFlags) = f &&& MemoryHeapFlags.DeviceLocalBit <> MemoryHeapFlags.None


type MemoryHeapInfo(index : int, totalSize : int64, flags : MemoryHeapFlags) =
    let mutable allocated = 0L

    member x.TryAdd(size : int64) =
        Interlocked.Change(&allocated, fun v ->
            if v + size > totalSize then (v, false)
            else (v + size, true)
        )

    member inline x.TryAdd(size : VkDeviceSize) = x.TryAdd (int64 size)
    member inline x.TryAdd(size : Mem) = x.TryAdd size.Bytes

    member x.Remove(size : int64) = Interlocked.Add(&allocated, -size) |> ignore
    member inline x.Remove(size : VkDeviceSize) = x.Remove (int64 size)
    member inline x.Remove(size : Mem) = x.Remove size.Bytes

    member x.Index = index
    member x.Capacity = Mem totalSize
    member x.Allocated = Mem allocated
    member x.Available = Mem (totalSize - allocated)
    member x.Flags = flags

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MemoryHeapInfo =
    let inline index (info : MemoryHeapInfo) = info.Index 
    let inline capacity (info : MemoryHeapInfo) = info.Capacity 
    let inline available (info : MemoryHeapInfo) = info.Available 
    let inline allocated (info : MemoryHeapInfo) = info.Allocated 
    let inline flags (info : MemoryHeapInfo) = info.Flags 






// =======================================================================
// Memory Info
// =======================================================================

[<Flags>]
type MemoryFlags =
    | None              = 0x00000000
    | DeviceLocal       = 0x00000001
    | HostVisible       = 0x00000002
    | HostCoherent      = 0x00000004
    | HostCached        = 0x00000008
    | LazilyAllocated   = 0x00000010

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


type MemoryInfo =
    {
        index           : int
        heap            : MemoryHeapInfo
        flags           : MemoryFlags
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MemoryInfo =
    let inline index (info : MemoryInfo) = info.index
    let inline heap (info : MemoryInfo) = info.heap
    let inline flags (info : MemoryInfo) = info.flags

    let internal deviceScore (info : MemoryInfo) =
        (MemoryFlags.deviceScore info.flags, info.heap.Capacity.Bytes)

    let internal hostScore (info : MemoryInfo) =
        (MemoryFlags.hostScore info.flags, info.heap.Capacity.Bytes)


type CommandBufferLevel =
    | Primary = 0
    | Secondary = 1

[<Flags>]
type CommandBufferUsage = 
    | None = 0
    | OneTimeSubmit = 0x00000001
    | RenderPassContinue = 0x00000002
    | SimultaneousUse = 0x00000004
