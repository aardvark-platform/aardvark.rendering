namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering.Vulkan.Memory
open System
open System.Runtime.InteropServices
open System.Collections.Concurrent

#nowarn "9"
#nowarn "51"

type internal HostAccess =
    | None = 0
    | ReadWrite = 1
    | WriteOnly = 2

/// Represents a memory type.
type IDeviceMemory =
    inherit IDeviceObject

    abstract member IsHostVisible : bool

    abstract member CreateBuffer : info: VkBufferCreateInfo byref *
                                   [<Optional; DefaultParameterValue(0UL)>] alignment: uint64 *
                                   [<Optional; DefaultParameterValue(0.5)>] priority: float *
                                   [<Optional; DefaultParameterValue(false)>] export: bool *
                                   [<Optional; DefaultParameterValue(true)>] bind: bool *
                                   [<Optional; DefaultParameterValue(false)>] mayAlias: bool -> struct (VkBuffer * DevicePtr)

    abstract member CreateImage : info: VkImageCreateInfo byref *
                                  [<Optional; DefaultParameterValue(0.5)>] priority: float *
                                  [<Optional; DefaultParameterValue(false)>] export: bool *
                                  [<Optional; DefaultParameterValue(true)>] bind: bool *
                                  [<Optional; DefaultParameterValue(false)>] mayAlias: bool -> struct (VkImage * DevicePtr)

type internal MemoryAllocator (device: IDevice) =
    let mutable allocator = VmaAllocator.Zero
    do
        let flags =
            [
                if device.IsExtensionEnabled EXTMemoryBudget.Name then
                    VmaAllocatorCreateFlags.ExtMemoryBudgetBit

                if device.EnabledFeatures.Memory.MemoryPriority then
                    VmaAllocatorCreateFlags.ExtMemoryPriorityBit

                if device.EnabledFeatures.Memory.BufferDeviceAddress then
                    VmaAllocatorCreateFlags.BufferDeviceAddressBit

                if device.IsExtensionEnabled KHRExternalMemoryWin32.Name then
                    VmaAllocatorCreateFlags.KhrExternalMemoryWin32Bit

                if device.IsExtensionEnabled KHRMaintenance4.Name then
                    VmaAllocatorCreateFlags.KhrMaintenance4Bit

                if device.IsExtensionEnabled KHRMaintenance5.Name then
                    VmaAllocatorCreateFlags.KhrMaintenance5Bit
            ]
            |> List.fold (|||) VmaAllocatorCreateFlags.None

        let mutable createInfo =
            { VmaAllocatorCreateInfo.Empty with
                instance         = device.Instance.Handle
                physicalDevice   = device.PhysicalDevice.Handle
                device           = device.Handle
                vulkanApiVersion = device.Instance.APIVersion.ToVulkan()
                flags            = flags }

        Vma.createAllocator(&&createInfo, &&allocator)
            |> check "could not create allocator"

    let nullPtr = new DevicePtr(device)

    let externalMemoryPools = ConcurrentDictionary<uint32, Lazy<ExternalMemoryPool>>()

    let getExternalMemoryPool memoryTypeIndex =
        externalMemoryPools.GetOrAdd(memoryTypeIndex, fun index ->
            lazy (new ExternalMemoryPool(allocator, index))
        ).Value

    let tryCreateBuffer pBufferCreateInfo pAllocationCreateInfo hostVisible alignment export =
        let mutable buffer = VkBuffer.Null
        let mutable allocation = VmaAllocation.Zero

        let result =
            Vma.createBufferWithAlignment(
                allocator, pBufferCreateInfo, pAllocationCreateInfo, alignment,
                &&buffer, &&allocation, NativePtr.zero
            )

        if result = VkResult.Success then
            let ptr = new DevicePtr(device, allocator, allocation, hostVisible, export)
            Result.Ok struct (buffer, ptr)
        else
            Result.Error result

    let tryCreateImage pImageCreateInfo pAllocationCreateInfo hostVisible export =
        let mutable image = VkImage.Null
        let mutable allocation = VmaAllocation.Zero

        let result =
            Vma.createImage(
                allocator, pImageCreateInfo, pAllocationCreateInfo,
                &&image, &&allocation, NativePtr.zero
            )

        if result = VkResult.Success then
            let ptr = new DevicePtr(device, allocator, allocation, hostVisible, export)
            Result.Ok struct (image, ptr)
        else
            Result.Error result

    let rec createExternalBuffer pBufferCreateInfo (allocationCreateInfo: _ byref) hostVisible alignment =
        let mutable memoryTypeIndex = 0u
        Vma.findMemoryTypeIndexForBufferInfo(allocator, pBufferCreateInfo, &&allocationCreateInfo, &&memoryTypeIndex)
            |> checkf "could not find memory type for buffer"

        let pool = getExternalMemoryPool memoryTypeIndex
        allocationCreateInfo.pool <- pool.Handle

        match tryCreateBuffer pBufferCreateInfo &&allocationCreateInfo hostVisible alignment true with
        | Result.Ok result -> result
        | Result.Error _ ->
            &allocationCreateInfo.memoryTypeBits &&&= ~~~(1u <<< int32 memoryTypeIndex)
            createExternalBuffer pBufferCreateInfo &allocationCreateInfo hostVisible alignment

    let rec createExternalImage pImageCreateInfo (allocationCreateInfo: _ byref) hostVisible =
        let mutable memoryTypeIndex = 0u
        Vma.findMemoryTypeIndexForImageInfo(allocator, pImageCreateInfo, &&allocationCreateInfo, &&memoryTypeIndex)
            |> checkf "could not find memory type for image"

        let pool = getExternalMemoryPool memoryTypeIndex
        allocationCreateInfo.pool <- pool.Handle

        match tryCreateImage pImageCreateInfo &&allocationCreateInfo hostVisible true with
        | Result.Ok result -> result
        | Result.Error _ ->
            &allocationCreateInfo.memoryTypeBits &&&= ~~~(1u <<< int32 memoryTypeIndex)
            createExternalImage pImageCreateInfo &allocationCreateInfo hostVisible

    let getAllocationInfo (preferDevice: bool) (hostAccess: HostAccess) (priority: float) (bind: bool) (mayAlias: bool) =
        let usage =
            if preferDevice then VmaMemoryUsage.AutoPreferDevice
            else VmaMemoryUsage.AutoPreferHost

        let mutable flags =
            match hostAccess with
            | HostAccess.ReadWrite -> VmaAllocationCreateFlags.MappedBit ||| VmaAllocationCreateFlags.HostAccessRandomBit
            | HostAccess.WriteOnly -> VmaAllocationCreateFlags.MappedBit ||| VmaAllocationCreateFlags.HostAccessSequentialWriteBit
            | _ -> VmaAllocationCreateFlags.None

        if not bind then
            &flags |||= VmaAllocationCreateFlags.DontBindBit

        if mayAlias then
            &flags |||= VmaAllocationCreateFlags.CanAliasBit

        { VmaAllocationCreateInfo.Empty with
            flags          = flags
            usage          = usage
            priority       = float32 priority
            memoryTypeBits = ~~~0u }

    member _.NullPtr = nullPtr

    member private this.CreateBuffer(bufferCreateInfo: VkBufferCreateInfo byref, preferDevice: bool, hostAccess: HostAccess,
                                     alignment: uint64, priority: float, export: bool, bind: bool, mayAlias: bool) =
        try
            let hostVisible =
                hostAccess <> HostAccess.None

            let mutable allocationCreateInfo = getAllocationInfo preferDevice hostAccess priority bind mayAlias

            if export then
                createExternalBuffer &&bufferCreateInfo &allocationCreateInfo hostVisible alignment
            else
                match tryCreateBuffer &&bufferCreateInfo &&allocationCreateInfo hostVisible alignment false with
                | Result.Ok result -> result
                | Result.Error error ->
                    error |> checkf "could not allocate memory for buffer"
                    Unchecked.defaultof<_>

        with :? VulkanException ->
            this.PrintUsage()
            reraise()

    member private this.CreateImage(imageCreateInfo: VkImageCreateInfo byref, preferDevice: bool, hostAccess: HostAccess,
                                    priority: float, export: bool, bind: bool, mayAlias: bool) =
        try
            let hostVisible =
                hostAccess <> HostAccess.None

            let mutable allocationCreateInfo = getAllocationInfo preferDevice hostAccess priority bind mayAlias

            if export then
                createExternalImage &&imageCreateInfo &allocationCreateInfo hostVisible
            else
                match tryCreateImage &&imageCreateInfo &&allocationCreateInfo hostVisible false with
                | Result.Ok result -> result
                | Result.Error error ->
                    error |> checkf "could not allocate memory for image"
                    Unchecked.defaultof<_>

        with :? VulkanException ->
            this.PrintUsage()
            reraise()

    member this.GetMemory(preferDevice: bool, hostAccess: HostAccess) =
        { new IDeviceMemory with
            member _.DeviceInterface = device

            member _.IsHostVisible = hostAccess <> HostAccess.None

            member _.CreateBuffer(info, alignment, priority, export, bind, mayAlias) =
                this.CreateBuffer(&info, preferDevice, hostAccess, alignment, priority, export, bind, mayAlias)

            member _.CreateImage(info, priority, export, bind, mayAlias) =
                this.CreateImage(&info, preferDevice, hostAccess, priority, export, bind, mayAlias)
        }

    member _.PrintUsage([<Optional; DefaultParameterValue(2)>] verbosity: int) =
        let l = Logger.Get verbosity
        let heaps = device.PhysicalDevice.MemoryHeaps

        let heapBudgets = Array.zeroCreate<VmaBudget> heaps.Length
        use pHeapBudgets = fixed heapBudgets
        Vma.getHeapBudgets(allocator, pHeapBudgets)

        for i = 0 to heaps.Length - 1 do
            let h = heaps.[i]
            let b = heapBudgets.[i]

            let heapFlags =
                if h.Flags.HasFlag MemoryHeapFlags.DeviceLocal then $" (device local)"
                else ""

            l.section $"Heap {i}{heapFlags}" (fun _ ->
                l.line $"Capacity: {h.Capacity}"
                l.line $"Allocated: {Mem b.statistics.allocationBytes}"
                l.line $"Available: {h.Capacity - Mem b.statistics.allocationBytes}"

                let warning =
                    if b.usage > b.budget then " (!!!)"
                    else ""

                l.line $"Budget: {Mem b.usage} / {Mem b.budget}{warning}"
            )

    member _.Dispose() =
        if allocator <> VmaAllocator.Zero then
            for KeyValue(_, pool) in externalMemoryPools do
                pool.Value.Dispose()

            Vma.destroyAllocator allocator
            allocator <- VmaAllocator.Zero

    interface IDeviceObject with
        member _.DeviceInterface = device

    interface IDisposable with
        member this.Dispose() = this.Dispose()