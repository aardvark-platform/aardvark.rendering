namespace Aardvark.Rendering.Vulkan.Memory

open Aardvark.Base
open Aardvark.Rendering.Vulkan
open FSharp.NativeInterop
open System
open System.Diagnostics
open System.Security
open System.Runtime.InteropServices
open Vulkan11

#nowarn "9"
#nowarn "51"

[<Flags>]
type VmaAllocatorCreateFlags =
    | None = 0
    | ExternallySynchronizedBit = 0x00000001
    | KhrDedicatedAllocationBit = 0x00000002
    | KhrBindMemory2Bit = 0x00000004
    | ExtMemoryBudgetBit = 0x00000008
    | AmdDeviceCoherentMemoryBit = 0x00000010
    | BufferDeviceAddressBit = 0x00000020
    | ExtMemoryPriorityBit = 0x00000040
    | KhrMaintenance4Bit = 0x00000080
    | KhrMaintenance5Bit = 0x00000100
    | KhrExternalMemoryWin32Bit = 0x00000200
    | MaxEnum = 0x7FFFFFFF

type VmaMemoryUsage =
    | Unknown = 0
    | GpuOnly = 1
    | CpuOnly = 2
    | CpuToGpu = 3
    | GpuToCpu = 4
    | CpuCopy = 5
    | GpuLazilyAllocated = 6
    | Auto = 7
    | AutoPreferDevice = 8
    | AutoPreferHost = 9
    | MaxEnum = 0x7FFFFFFF

[<Flags>]
type VmaAllocationCreateFlags =
    | None = 0
    | DedicatedMemoryBit = 0x00000001
    | NeverAllocateBit = 0x00000002
    | MappedBit = 0x00000004
    | UserDataCopyStringBit = 0x00000020
    | UpperAddressBit = 0x00000040
    | DontBindBit = 0x00000080
    | WithinBudgetBit = 0x00000100
    | CanAliasBit = 0x00000200
    | HostAccessSequentialWriteBit = 0x00000400
    | HostAccessRandomBit = 0x00000800
    | HostAccessAllowTransferInsteadBit = 0x00001000
    | StrategyMinMemoryBit = 0x00010000
    | StrategyMinTimeBit = 0x00020000
    | StrategyMinOffsetBit = 0x00040000
    | StrategyBestFitBit = 0x00010000
    | StrategyFirstFitBit = 0x00020000
    | StrategyMask = (0x00010000 ||| 0x00020000 ||| 0x00040000)
    | MaxEnum = 0x7FFFFFFF

[<Flags>]
type VmaPoolCreateFlags =
    | None = 0
    | IgnoreBufferImageGranularityBit = 0x00000002
    | LinearAlgorithmBit = 0x00000004
    | AlgorithmMask = 0x00000004
    | MaxEnum = 0x7FFFFFFF

[<Flags>]
type VmaDefragmentationFlags =
    | None = 0
    | FlagAlgorithmFastBit = 0x1
    | FlagAlgorithmBalancedBit = 0x2
    | FlagAlgorithmFullBit = 0x4
    | FlagAlgorithmExtensiveBit = 0x8
    | FlagAlgorithmMask = (0x1 ||| 0x2 ||| 0x4 ||| 0x8)
    | MaxEnum = 0x7FFFFFFF

type VmaDefragmentationMoveOperation =
    | Copy = 0
    | Ignore = 1
    | Destroy = 2

[<Flags>]
type VmaVirtualBlockCreateFlags =
    | None = 0
    | LinearAlgorithmBit = 0x00000001
    | AlgorithmMask = 0x00000001
    | MaxEnum = 0x7FFFFFFF

[<Flags>]
type VmaVirtualAllocationCreateFlags =
    | None = 0
    | UpperAddressBit = 0x00000040
    | StrategyMinMemoryBit = 0x00010000
    | StrategyMinTimeBit = 0x00020000
    | StrategyMinOffsetBit = 0x00040000
    | StrategyMask = (0x00010000 ||| 0x00020000 ||| 0x00040000)
    | MaxEnum = 0x7FFFFFFF

type VmaAllocator = nativeint

type VmaPool = nativeint

type VmaAllocation = nativeint

type VmaDefragmentationContext = nativeint

[<StructLayout(LayoutKind.Sequential)>]
type VmaVirtualAllocation =
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VmaVirtualAllocation(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

type VmaVirtualBlock = nativeint

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaDeviceMemoryCallbacks =
    {
        mutable pfnAllocate : nativeint
        mutable pfnFree : nativeint
        mutable pUserData : nativeint
    }

    static member Empty : VmaDeviceMemoryCallbacks =
        {
            pfnAllocate = Unchecked.defaultof<nativeint>
            pfnFree = Unchecked.defaultof<nativeint>
            pUserData = Unchecked.defaultof<nativeint>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaVulkanFunctions =
    {
        mutable vkGetInstanceProcAddr : nativeint
        mutable vkGetDeviceProcAddr : nativeint
        mutable vkGetPhysicalDeviceProperties : nativeint
        mutable vkGetPhysicalDeviceMemoryProperties : nativeint
        mutable vkAllocateMemory : nativeint
        mutable vkFreeMemory : nativeint
        mutable vkMapMemory : nativeint
        mutable vkUnmapMemory : nativeint
        mutable vkFlushMappedMemoryRanges : nativeint
        mutable vkInvalidateMappedMemoryRanges : nativeint
        mutable vkBindBufferMemory : nativeint
        mutable vkBindImageMemory : nativeint
        mutable vkGetBufferMemoryRequirements : nativeint
        mutable vkGetImageMemoryRequirements : nativeint
        mutable vkCreateBuffer : nativeint
        mutable vkDestroyBuffer : nativeint
        mutable vkCreateImage : nativeint
        mutable vkDestroyImage : nativeint
        mutable vkCmdCopyBuffer : nativeint
        mutable vkGetBufferMemoryRequirements2KHR : nativeint
        mutable vkGetImageMemoryRequirements2KHR : nativeint
        mutable vkBindBufferMemory2KHR : nativeint
        mutable vkBindImageMemory2KHR : nativeint
        mutable vkGetPhysicalDeviceMemoryProperties2KHR : nativeint
        mutable vkGetDeviceBufferMemoryRequirements : nativeint
        mutable vkGetDeviceImageMemoryRequirements : nativeint
        mutable vkGetMemoryWin32HandleKHR : nativeint
    }

    static member Empty : VmaVulkanFunctions =
        {
            vkGetInstanceProcAddr = Unchecked.defaultof<nativeint>
            vkGetDeviceProcAddr = Unchecked.defaultof<nativeint>
            vkGetPhysicalDeviceProperties = Unchecked.defaultof<nativeint>
            vkGetPhysicalDeviceMemoryProperties = Unchecked.defaultof<nativeint>
            vkAllocateMemory = Unchecked.defaultof<nativeint>
            vkFreeMemory = Unchecked.defaultof<nativeint>
            vkMapMemory = Unchecked.defaultof<nativeint>
            vkUnmapMemory = Unchecked.defaultof<nativeint>
            vkFlushMappedMemoryRanges = Unchecked.defaultof<nativeint>
            vkInvalidateMappedMemoryRanges = Unchecked.defaultof<nativeint>
            vkBindBufferMemory = Unchecked.defaultof<nativeint>
            vkBindImageMemory = Unchecked.defaultof<nativeint>
            vkGetBufferMemoryRequirements = Unchecked.defaultof<nativeint>
            vkGetImageMemoryRequirements = Unchecked.defaultof<nativeint>
            vkCreateBuffer = Unchecked.defaultof<nativeint>
            vkDestroyBuffer = Unchecked.defaultof<nativeint>
            vkCreateImage = Unchecked.defaultof<nativeint>
            vkDestroyImage = Unchecked.defaultof<nativeint>
            vkCmdCopyBuffer = Unchecked.defaultof<nativeint>
            vkGetBufferMemoryRequirements2KHR = Unchecked.defaultof<nativeint>
            vkGetImageMemoryRequirements2KHR = Unchecked.defaultof<nativeint>
            vkBindBufferMemory2KHR = Unchecked.defaultof<nativeint>
            vkBindImageMemory2KHR = Unchecked.defaultof<nativeint>
            vkGetPhysicalDeviceMemoryProperties2KHR = Unchecked.defaultof<nativeint>
            vkGetDeviceBufferMemoryRequirements = Unchecked.defaultof<nativeint>
            vkGetDeviceImageMemoryRequirements = Unchecked.defaultof<nativeint>
            vkGetMemoryWin32HandleKHR = Unchecked.defaultof<nativeint>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaAllocatorCreateInfo =
    {
        mutable flags : VmaAllocatorCreateFlags
        mutable physicalDevice : VkPhysicalDevice
        mutable device : VkDevice
        mutable preferredLargeHeapBlockSize : VkDeviceSize
        mutable pAllocationCallbacks : nativeptr<VkAllocationCallbacks>
        mutable pDeviceMemoryCallbacks : nativeptr<VmaDeviceMemoryCallbacks>
        mutable pHeapSizeLimit : nativeptr<VkDeviceSize>
        mutable pVulkanFunctions : nativeptr<VmaVulkanFunctions>
        mutable instance : VkInstance
        mutable vulkanApiVersion : uint32
        mutable pTypeExternalMemoryHandleTypes : nativeptr<VkExternalMemoryHandleTypeFlags>
    }

    static member Empty : VmaAllocatorCreateInfo =
        {
            flags = Unchecked.defaultof<VmaAllocatorCreateFlags>
            physicalDevice = Unchecked.defaultof<VkPhysicalDevice>
            device = Unchecked.defaultof<VkDevice>
            preferredLargeHeapBlockSize = Unchecked.defaultof<VkDeviceSize>
            pAllocationCallbacks = Unchecked.defaultof<nativeptr<VkAllocationCallbacks>>
            pDeviceMemoryCallbacks = Unchecked.defaultof<nativeptr<VmaDeviceMemoryCallbacks>>
            pHeapSizeLimit = Unchecked.defaultof<nativeptr<VkDeviceSize>>
            pVulkanFunctions = Unchecked.defaultof<nativeptr<VmaVulkanFunctions>>
            instance = Unchecked.defaultof<VkInstance>
            vulkanApiVersion = Unchecked.defaultof<uint32>
            pTypeExternalMemoryHandleTypes = Unchecked.defaultof<nativeptr<VkExternalMemoryHandleTypeFlags>>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaAllocatorInfo =
    {
        mutable instance : VkInstance
        mutable physicalDevice : VkPhysicalDevice
        mutable device : VkDevice
    }

    static member Empty : VmaAllocatorInfo =
        {
            instance = Unchecked.defaultof<VkInstance>
            physicalDevice = Unchecked.defaultof<VkPhysicalDevice>
            device = Unchecked.defaultof<VkDevice>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaStatistics =
    {
        mutable blockCount : uint32
        mutable allocationCount : uint32
        mutable blockBytes : VkDeviceSize
        mutable allocationBytes : VkDeviceSize
    }

    static member Empty : VmaStatistics =
        {
            blockCount = Unchecked.defaultof<uint32>
            allocationCount = Unchecked.defaultof<uint32>
            blockBytes = Unchecked.defaultof<VkDeviceSize>
            allocationBytes = Unchecked.defaultof<VkDeviceSize>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaDetailedStatistics =
    {
        mutable statistics : VmaStatistics
        mutable unusedRangeCount : uint32
        mutable allocationSizeMin : VkDeviceSize
        mutable allocationSizeMax : VkDeviceSize
        mutable unusedRangeSizeMin : VkDeviceSize
        mutable unusedRangeSizeMax : VkDeviceSize
    }

    static member Empty : VmaDetailedStatistics =
        {
            statistics = Unchecked.defaultof<VmaStatistics>
            unusedRangeCount = Unchecked.defaultof<uint32>
            allocationSizeMin = Unchecked.defaultof<VkDeviceSize>
            allocationSizeMax = Unchecked.defaultof<VkDeviceSize>
            unusedRangeSizeMin = Unchecked.defaultof<VkDeviceSize>
            unusedRangeSizeMax = Unchecked.defaultof<VkDeviceSize>
        }

[<StructLayout(LayoutKind.Explicit, Size = 32 * 64)>]
type VmaDetailedStatistics_32 =
    struct
        member x.Item
            with get (index: int) : VmaDetailedStatistics =
                if index < 0 || index > 31 then raise <| IndexOutOfRangeException()
                let ptr = NativePtr.cast &&x
                ptr.[index]
            and set (index: int) (value: VmaDetailedStatistics) =
                if index < 0 || index > 31 then raise <| IndexOutOfRangeException()
                let ptr = NativePtr.cast &&x
                ptr.[index] <- value

        member x.Length = 32

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = let x = x in (Seq.init 32 (fun i -> x.[i])).GetEnumerator() :> System.Collections.IEnumerator

        interface System.Collections.Generic.IEnumerable<VmaDetailedStatistics> with
            member x.GetEnumerator() = let x = x in (Seq.init 32 (fun i -> x.[i])).GetEnumerator()
    end

[<StructLayout(LayoutKind.Explicit, Size = 16 * 64)>]
type VmaDetailedStatistics_16 =
    struct
        member x.Item
            with get (index: int) : VmaDetailedStatistics =
                if index < 0 || index > 15 then raise <| IndexOutOfRangeException()
                let ptr = NativePtr.cast &&x
                ptr.[index]
            and set (index: int) (value: VmaDetailedStatistics) =
                if index < 0 || index > 15 then raise <| IndexOutOfRangeException()
                let ptr = NativePtr.cast &&x
                ptr.[index] <- value

        member x.Length = 16

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = let x = x in (Seq.init 16 (fun i -> x.[i])).GetEnumerator() :> System.Collections.IEnumerator

        interface System.Collections.Generic.IEnumerable<VmaDetailedStatistics> with
            member x.GetEnumerator() = let x = x in (Seq.init 16 (fun i -> x.[i])).GetEnumerator()
    end

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaTotalStatistics =
    {
        mutable memoryType : VmaDetailedStatistics_32
        mutable memoryHeap : VmaDetailedStatistics_16
        mutable total : VmaDetailedStatistics
    }

    static member Empty : VmaTotalStatistics =
        {
            memoryType = Unchecked.defaultof<VmaDetailedStatistics_32>
            memoryHeap = Unchecked.defaultof<VmaDetailedStatistics_16>
            total = Unchecked.defaultof<VmaDetailedStatistics>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaBudget =
    {
        mutable statistics : VmaStatistics
        mutable usage : VkDeviceSize
        mutable budget : VkDeviceSize
    }

    static member Empty : VmaBudget =
        {
            statistics = Unchecked.defaultof<VmaStatistics>
            usage = Unchecked.defaultof<VkDeviceSize>
            budget = Unchecked.defaultof<VkDeviceSize>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaAllocationCreateInfo =
    {
        mutable flags : VmaAllocationCreateFlags
        mutable usage : VmaMemoryUsage
        mutable requiredFlags : VkMemoryPropertyFlags
        mutable preferredFlags : VkMemoryPropertyFlags
        mutable memoryTypeBits : uint32
        mutable pool : VmaPool
        mutable pUserData : nativeint
        mutable priority : float32
    }

    static member Empty : VmaAllocationCreateInfo =
        {
            flags = Unchecked.defaultof<VmaAllocationCreateFlags>
            usage = Unchecked.defaultof<VmaMemoryUsage>
            requiredFlags = Unchecked.defaultof<VkMemoryPropertyFlags>
            preferredFlags = Unchecked.defaultof<VkMemoryPropertyFlags>
            memoryTypeBits = Unchecked.defaultof<uint32>
            pool = Unchecked.defaultof<VmaPool>
            pUserData = Unchecked.defaultof<nativeint>
            priority = Unchecked.defaultof<float32>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaPoolCreateInfo =
    {
        mutable memoryTypeIndex : uint32
        mutable flags : VmaPoolCreateFlags
        mutable blockSize : VkDeviceSize
        mutable minBlockCount : uint64
        mutable maxBlockCount : uint64
        mutable priority : float32
        mutable minAllocationAlignment : VkDeviceSize
        mutable pMemoryAllocateNext : nativeint
    }

    static member Empty : VmaPoolCreateInfo =
        {
            memoryTypeIndex = Unchecked.defaultof<uint32>
            flags = Unchecked.defaultof<VmaPoolCreateFlags>
            blockSize = Unchecked.defaultof<VkDeviceSize>
            minBlockCount = Unchecked.defaultof<uint64>
            maxBlockCount = Unchecked.defaultof<uint64>
            priority = Unchecked.defaultof<float32>
            minAllocationAlignment = Unchecked.defaultof<VkDeviceSize>
            pMemoryAllocateNext = Unchecked.defaultof<nativeint>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaAllocationInfo =
    {
        mutable memoryType : uint32
        mutable deviceMemory : VkDeviceMemory
        mutable offset : VkDeviceSize
        mutable size : VkDeviceSize
        mutable pMappedData : nativeint
        mutable pUserData : nativeint
        mutable pName : cstr
    }

    static member Empty : VmaAllocationInfo =
        {
            memoryType = Unchecked.defaultof<uint32>
            deviceMemory = Unchecked.defaultof<VkDeviceMemory>
            offset = Unchecked.defaultof<VkDeviceSize>
            size = Unchecked.defaultof<VkDeviceSize>
            pMappedData = Unchecked.defaultof<nativeint>
            pUserData = Unchecked.defaultof<nativeint>
            pName = Unchecked.defaultof<cstr>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaAllocationInfo2 =
    {
        mutable allocationInfo : VmaAllocationInfo
        mutable blockSize : VkDeviceSize
        mutable dedicatedMemory : VkBool32
    }

    static member Empty : VmaAllocationInfo2 =
        {
            allocationInfo = Unchecked.defaultof<VmaAllocationInfo>
            blockSize = Unchecked.defaultof<VkDeviceSize>
            dedicatedMemory = Unchecked.defaultof<VkBool32>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaDefragmentationInfo =
    {
        mutable flags : VmaDefragmentationFlags
        mutable pool : VmaPool
        mutable maxBytesPerPass : VkDeviceSize
        mutable maxAllocationsPerPass : uint32
        mutable pfnBreakCallback : nativeint
        mutable pBreakCallbackUserData : nativeint
    }

    static member Empty : VmaDefragmentationInfo =
        {
            flags = Unchecked.defaultof<VmaDefragmentationFlags>
            pool = Unchecked.defaultof<VmaPool>
            maxBytesPerPass = Unchecked.defaultof<VkDeviceSize>
            maxAllocationsPerPass = Unchecked.defaultof<uint32>
            pfnBreakCallback = Unchecked.defaultof<nativeint>
            pBreakCallbackUserData = Unchecked.defaultof<nativeint>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaDefragmentationMove =
    {
        mutable operation : VmaDefragmentationMoveOperation
        mutable srcAllocation : VmaAllocation
        mutable dstTmpAllocation : VmaAllocation
    }

    static member Empty : VmaDefragmentationMove =
        {
            operation = Unchecked.defaultof<VmaDefragmentationMoveOperation>
            srcAllocation = Unchecked.defaultof<VmaAllocation>
            dstTmpAllocation = Unchecked.defaultof<VmaAllocation>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaDefragmentationPassMoveInfo =
    {
        mutable moveCount : uint32
        mutable pMoves : nativeptr<VmaDefragmentationMove>
    }

    static member Empty : VmaDefragmentationPassMoveInfo =
        {
            moveCount = Unchecked.defaultof<uint32>
            pMoves = Unchecked.defaultof<nativeptr<VmaDefragmentationMove>>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaDefragmentationStats =
    {
        mutable bytesMoved : VkDeviceSize
        mutable bytesFreed : VkDeviceSize
        mutable allocationsMoved : uint32
        mutable deviceMemoryBlocksFreed : uint32
    }

    static member Empty : VmaDefragmentationStats =
        {
            bytesMoved = Unchecked.defaultof<VkDeviceSize>
            bytesFreed = Unchecked.defaultof<VkDeviceSize>
            allocationsMoved = Unchecked.defaultof<uint32>
            deviceMemoryBlocksFreed = Unchecked.defaultof<uint32>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaVirtualBlockCreateInfo =
    {
        mutable size : VkDeviceSize
        mutable flags : VmaVirtualBlockCreateFlags
        mutable pAllocationCallbacks : nativeptr<VkAllocationCallbacks>
    }

    static member Empty : VmaVirtualBlockCreateInfo =
        {
            size = Unchecked.defaultof<VkDeviceSize>
            flags = Unchecked.defaultof<VmaVirtualBlockCreateFlags>
            pAllocationCallbacks = Unchecked.defaultof<nativeptr<VkAllocationCallbacks>>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaVirtualAllocationCreateInfo =
    {
        mutable size : VkDeviceSize
        mutable alignment : VkDeviceSize
        mutable flags : VmaVirtualAllocationCreateFlags
        mutable pUserData : nativeint
    }

    static member Empty : VmaVirtualAllocationCreateInfo =
        {
            size = Unchecked.defaultof<VkDeviceSize>
            alignment = Unchecked.defaultof<VkDeviceSize>
            flags = Unchecked.defaultof<VmaVirtualAllocationCreateFlags>
            pUserData = Unchecked.defaultof<nativeint>
        }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type VmaVirtualAllocationInfo =
    {
        mutable offset : VkDeviceSize
        mutable size : VkDeviceSize
        mutable pUserData : nativeint
    }

    static member Empty : VmaVirtualAllocationInfo =
        {
            offset = Unchecked.defaultof<VkDeviceSize>
            size = Unchecked.defaultof<VkDeviceSize>
            pUserData = Unchecked.defaultof<nativeint>
        }

[<SuppressUnmanagedCodeSecurity>]
module Vma =
    [<Literal>]
    let private lib = "vkvm"

    #if DEBUG
    do
        Debug.Assert(sizeof<VmaDetailedStatistics_32> = sizeof<VmaDetailedStatistics> * 32, $"Unexpected size for VmaDetailedStatistics_32, expected {sizeof<VmaDetailedStatistics> * 32} but got {sizeof<VmaDetailedStatistics_32>}.")
        Debug.Assert(sizeof<VmaDetailedStatistics_16> = sizeof<VmaDetailedStatistics> * 16, $"Unexpected size for VmaDetailedStatistics_16, expected {sizeof<VmaDetailedStatistics> * 16} but got {sizeof<VmaDetailedStatistics_16>}.")
    #endif

    [<DllImport(lib, EntryPoint = "vmaCreateAllocator")>]
    extern VkResult createAllocator(VmaAllocatorCreateInfo* pCreateInfo, VmaAllocator* pAllocator)

    [<DllImport(lib, EntryPoint = "vmaDestroyAllocator")>]
    extern void destroyAllocator(VmaAllocator allocator)

    [<DllImport(lib, EntryPoint = "vmaGetAllocatorInfo")>]
    extern void getAllocatorInfo(VmaAllocator allocator, VmaAllocatorInfo* pAllocatorInfo)

    [<DllImport(lib, EntryPoint = "vmaGetPhysicalDeviceProperties")>]
    extern void getPhysicalDeviceProperties(VmaAllocator allocator, VkPhysicalDeviceProperties* * ppPhysicalDeviceProperties)

    [<DllImport(lib, EntryPoint = "vmaGetMemoryProperties")>]
    extern void getMemoryProperties(VmaAllocator allocator, VkPhysicalDeviceMemoryProperties* * ppPhysicalDeviceMemoryProperties)

    [<DllImport(lib, EntryPoint = "vmaGetMemoryTypeProperties")>]
    extern void getMemoryTypeProperties(VmaAllocator allocator, uint32 memoryTypeIndex, VkMemoryPropertyFlags* pFlags)

    [<DllImport(lib, EntryPoint = "vmaSetCurrentFrameIndex")>]
    extern void setCurrentFrameIndex(VmaAllocator allocator, uint32 frameIndex)

    [<DllImport(lib, EntryPoint = "vmaCalculateStatistics")>]
    extern void calculateStatistics(VmaAllocator allocator, VmaTotalStatistics* pStats)

    [<DllImport(lib, EntryPoint = "vmaGetHeapBudgets")>]
    extern void getHeapBudgets(VmaAllocator allocator, VmaBudget* pBudgets)

    [<DllImport(lib, EntryPoint = "vmaFindMemoryTypeIndex")>]
    extern VkResult findMemoryTypeIndex(VmaAllocator allocator, uint32 memoryTypeBits, VmaAllocationCreateInfo* pAllocationCreateInfo, uint32* pMemoryTypeIndex)

    [<DllImport(lib, EntryPoint = "vmaFindMemoryTypeIndexForBufferInfo")>]
    extern VkResult findMemoryTypeIndexForBufferInfo(VmaAllocator allocator, VkBufferCreateInfo* pBufferCreateInfo, VmaAllocationCreateInfo* pAllocationCreateInfo, uint32* pMemoryTypeIndex)

    [<DllImport(lib, EntryPoint = "vmaFindMemoryTypeIndexForImageInfo")>]
    extern VkResult findMemoryTypeIndexForImageInfo(VmaAllocator allocator, VkImageCreateInfo* pImageCreateInfo, VmaAllocationCreateInfo* pAllocationCreateInfo, uint32* pMemoryTypeIndex)

    [<DllImport(lib, EntryPoint = "vmaCreatePool")>]
    extern VkResult createPool(VmaAllocator allocator, VmaPoolCreateInfo* pCreateInfo, VmaPool* pPool)

    [<DllImport(lib, EntryPoint = "vmaDestroyPool")>]
    extern void destroyPool(VmaAllocator allocator, VmaPool pool)

    [<DllImport(lib, EntryPoint = "vmaGetPoolStatistics")>]
    extern void getPoolStatistics(VmaAllocator allocator, VmaPool pool, VmaStatistics* pPoolStats)

    [<DllImport(lib, EntryPoint = "vmaCalculatePoolStatistics")>]
    extern void calculatePoolStatistics(VmaAllocator allocator, VmaPool pool, VmaDetailedStatistics* pPoolStats)

    [<DllImport(lib, EntryPoint = "vmaCheckPoolCorruption")>]
    extern VkResult checkPoolCorruption(VmaAllocator allocator, VmaPool pool)

    [<DllImport(lib, EntryPoint = "vmaGetPoolName")>]
    extern void getPoolName(VmaAllocator allocator, VmaPool pool, byte* * ppName)

    [<DllImport(lib, EntryPoint = "vmaSetPoolName")>]
    extern void setPoolName(VmaAllocator allocator, VmaPool pool, cstr pName)

    [<DllImport(lib, EntryPoint = "vmaAllocateMemory")>]
    extern VkResult allocateMemory(VmaAllocator allocator, VkMemoryRequirements* pVkMemoryRequirements, VmaAllocationCreateInfo* pCreateInfo, VmaAllocation* pAllocation, VmaAllocationInfo* pAllocationInfo)

    [<DllImport(lib, EntryPoint = "vmaAllocateMemoryPages")>]
    extern VkResult allocateMemoryPages(VmaAllocator allocator, VkMemoryRequirements* pVkMemoryRequirements, VmaAllocationCreateInfo* pCreateInfo, uint64 allocationCount, VmaAllocation* pAllocations, VmaAllocationInfo* pAllocationInfo)

    [<DllImport(lib, EntryPoint = "vmaAllocateMemoryForBuffer")>]
    extern VkResult allocateMemoryForBuffer(VmaAllocator allocator, VkBuffer buffer, VmaAllocationCreateInfo* pCreateInfo, VmaAllocation* pAllocation, VmaAllocationInfo* pAllocationInfo)

    [<DllImport(lib, EntryPoint = "vmaAllocateMemoryForImage")>]
    extern VkResult allocateMemoryForImage(VmaAllocator allocator, VkImage image, VmaAllocationCreateInfo* pCreateInfo, VmaAllocation* pAllocation, VmaAllocationInfo* pAllocationInfo)

    [<DllImport(lib, EntryPoint = "vmaFreeMemory")>]
    extern void freeMemory(VmaAllocator allocator, VmaAllocation allocation)

    [<DllImport(lib, EntryPoint = "vmaFreeMemoryPages")>]
    extern void freeMemoryPages(VmaAllocator allocator, uint64 allocationCount, VmaAllocation* pAllocations)

    [<DllImport(lib, EntryPoint = "vmaGetAllocationInfo")>]
    extern void getAllocationInfo(VmaAllocator allocator, VmaAllocation allocation, VmaAllocationInfo* pAllocationInfo)

    [<DllImport(lib, EntryPoint = "vmaGetAllocationInfo2")>]
    extern void getAllocationInfo2(VmaAllocator allocator, VmaAllocation allocation, VmaAllocationInfo2* pAllocationInfo)

    [<DllImport(lib, EntryPoint = "vmaSetAllocationUserData")>]
    extern void setAllocationUserData(VmaAllocator allocator, VmaAllocation allocation, nativeint pUserData)

    [<DllImport(lib, EntryPoint = "vmaSetAllocationName")>]
    extern void setAllocationName(VmaAllocator allocator, VmaAllocation allocation, cstr pName)

    [<DllImport(lib, EntryPoint = "vmaGetAllocationMemoryProperties")>]
    extern void getAllocationMemoryProperties(VmaAllocator allocator, VmaAllocation allocation, VkMemoryPropertyFlags* pFlags)

    [<DllImport(lib, EntryPoint = "vmaGetMemoryWin32Handle")>]
    extern VkResult getMemoryWin32Handle(VmaAllocator allocator, VmaAllocation allocation, nativeint hTargetProcess, nativeint* pHandle)

    [<DllImport(lib, EntryPoint = "vmaMapMemory")>]
    extern VkResult mapMemory(VmaAllocator allocator, VmaAllocation allocation, void* * ppData)

    [<DllImport(lib, EntryPoint = "vmaUnmapMemory")>]
    extern void unmapMemory(VmaAllocator allocator, VmaAllocation allocation)

    [<DllImport(lib, EntryPoint = "vmaFlushAllocation")>]
    extern VkResult flushAllocation(VmaAllocator allocator, VmaAllocation allocation, VkDeviceSize offset, VkDeviceSize size)

    [<DllImport(lib, EntryPoint = "vmaInvalidateAllocation")>]
    extern VkResult invalidateAllocation(VmaAllocator allocator, VmaAllocation allocation, VkDeviceSize offset, VkDeviceSize size)

    [<DllImport(lib, EntryPoint = "vmaFlushAllocations")>]
    extern VkResult flushAllocations(VmaAllocator allocator, uint32 allocationCount, VmaAllocation* allocations, VkDeviceSize* offsets, VkDeviceSize* sizes)

    [<DllImport(lib, EntryPoint = "vmaInvalidateAllocations")>]
    extern VkResult invalidateAllocations(VmaAllocator allocator, uint32 allocationCount, VmaAllocation* allocations, VkDeviceSize* offsets, VkDeviceSize* sizes)

    [<DllImport(lib, EntryPoint = "vmaCopyMemoryToAllocation")>]
    extern VkResult copyMemoryToAllocation(VmaAllocator allocator, nativeint pSrcHostPointer, VmaAllocation dstAllocation, VkDeviceSize dstAllocationLocalOffset, VkDeviceSize size)

    [<DllImport(lib, EntryPoint = "vmaCopyAllocationToMemory")>]
    extern VkResult copyAllocationToMemory(VmaAllocator allocator, VmaAllocation srcAllocation, VkDeviceSize srcAllocationLocalOffset, nativeint pDstHostPointer, VkDeviceSize size)

    [<DllImport(lib, EntryPoint = "vmaCheckCorruption")>]
    extern VkResult checkCorruption(VmaAllocator allocator, uint32 memoryTypeBits)

    [<DllImport(lib, EntryPoint = "vmaBeginDefragmentation")>]
    extern VkResult beginDefragmentation(VmaAllocator allocator, VmaDefragmentationInfo* pInfo, VmaDefragmentationContext* pContext)

    [<DllImport(lib, EntryPoint = "vmaEndDefragmentation")>]
    extern void endDefragmentation(VmaAllocator allocator, VmaDefragmentationContext context, VmaDefragmentationStats* pStats)

    [<DllImport(lib, EntryPoint = "vmaBeginDefragmentationPass")>]
    extern VkResult beginDefragmentationPass(VmaAllocator allocator, VmaDefragmentationContext context, VmaDefragmentationPassMoveInfo* pPassInfo)

    [<DllImport(lib, EntryPoint = "vmaEndDefragmentationPass")>]
    extern VkResult endDefragmentationPass(VmaAllocator allocator, VmaDefragmentationContext context, VmaDefragmentationPassMoveInfo* pPassInfo)

    [<DllImport(lib, EntryPoint = "vmaBindBufferMemory")>]
    extern VkResult bindBufferMemory(VmaAllocator allocator, VmaAllocation allocation, VkBuffer buffer)

    [<DllImport(lib, EntryPoint = "vmaBindBufferMemory2")>]
    extern VkResult bindBufferMemory2(VmaAllocator allocator, VmaAllocation allocation, VkDeviceSize allocationLocalOffset, VkBuffer buffer, nativeint pNext)

    [<DllImport(lib, EntryPoint = "vmaBindImageMemory")>]
    extern VkResult bindImageMemory(VmaAllocator allocator, VmaAllocation allocation, VkImage image)

    [<DllImport(lib, EntryPoint = "vmaBindImageMemory2")>]
    extern VkResult bindImageMemory2(VmaAllocator allocator, VmaAllocation allocation, VkDeviceSize allocationLocalOffset, VkImage image, nativeint pNext)

    [<DllImport(lib, EntryPoint = "vmaCreateBuffer")>]
    extern VkResult createBuffer(VmaAllocator allocator, VkBufferCreateInfo* pBufferCreateInfo, VmaAllocationCreateInfo* pAllocationCreateInfo, VkBuffer* pBuffer, VmaAllocation* pAllocation, VmaAllocationInfo* pAllocationInfo)

    [<DllImport(lib, EntryPoint = "vmaCreateBufferWithAlignment")>]
    extern VkResult createBufferWithAlignment(VmaAllocator allocator, VkBufferCreateInfo* pBufferCreateInfo, VmaAllocationCreateInfo* pAllocationCreateInfo, VkDeviceSize minAlignment, VkBuffer* pBuffer, VmaAllocation* pAllocation, VmaAllocationInfo* pAllocationInfo)

    [<DllImport(lib, EntryPoint = "vmaCreateAliasingBuffer")>]
    extern VkResult createAliasingBuffer(VmaAllocator allocator, VmaAllocation allocation, VkBufferCreateInfo* pBufferCreateInfo, VkBuffer* pBuffer)

    [<DllImport(lib, EntryPoint = "vmaCreateAliasingBuffer2")>]
    extern VkResult createAliasingBuffer2(VmaAllocator allocator, VmaAllocation allocation, VkDeviceSize allocationLocalOffset, VkBufferCreateInfo* pBufferCreateInfo, VkBuffer* pBuffer)

    [<DllImport(lib, EntryPoint = "vmaDestroyBuffer")>]
    extern void destroyBuffer(VmaAllocator allocator, VkBuffer buffer, VmaAllocation allocation)

    [<DllImport(lib, EntryPoint = "vmaCreateImage")>]
    extern VkResult createImage(VmaAllocator allocator, VkImageCreateInfo* pImageCreateInfo, VmaAllocationCreateInfo* pAllocationCreateInfo, VkImage* pImage, VmaAllocation* pAllocation, VmaAllocationInfo* pAllocationInfo)

    [<DllImport(lib, EntryPoint = "vmaCreateAliasingImage")>]
    extern VkResult createAliasingImage(VmaAllocator allocator, VmaAllocation allocation, VkImageCreateInfo* pImageCreateInfo, VkImage* pImage)

    [<DllImport(lib, EntryPoint = "vmaCreateAliasingImage2")>]
    extern VkResult createAliasingImage2(VmaAllocator allocator, VmaAllocation allocation, VkDeviceSize allocationLocalOffset, VkImageCreateInfo* pImageCreateInfo, VkImage* pImage)

    [<DllImport(lib, EntryPoint = "vmaDestroyImage")>]
    extern void destroyImage(VmaAllocator allocator, VkImage image, VmaAllocation allocation)

    [<DllImport(lib, EntryPoint = "vmaCreateVirtualBlock")>]
    extern VkResult createVirtualBlock(VmaVirtualBlockCreateInfo* pCreateInfo, VmaVirtualBlock* pVirtualBlock)

    [<DllImport(lib, EntryPoint = "vmaDestroyVirtualBlock")>]
    extern void destroyVirtualBlock(VmaVirtualBlock virtualBlock)

    [<DllImport(lib, EntryPoint = "vmaIsVirtualBlockEmpty")>]
    extern VkBool32 isVirtualBlockEmpty(VmaVirtualBlock virtualBlock)

    [<DllImport(lib, EntryPoint = "vmaGetVirtualAllocationInfo")>]
    extern void getVirtualAllocationInfo(VmaVirtualBlock virtualBlock, VmaVirtualAllocation allocation, VmaVirtualAllocationInfo* pVirtualAllocInfo)

    [<DllImport(lib, EntryPoint = "vmaVirtualAllocate")>]
    extern VkResult virtualAllocate(VmaVirtualBlock virtualBlock, VmaVirtualAllocationCreateInfo* pCreateInfo, VmaVirtualAllocation* pAllocation, VkDeviceSize* pOffset)

    [<DllImport(lib, EntryPoint = "vmaVirtualFree")>]
    extern void virtualFree(VmaVirtualBlock virtualBlock, VmaVirtualAllocation allocation)

    [<DllImport(lib, EntryPoint = "vmaClearVirtualBlock")>]
    extern void clearVirtualBlock(VmaVirtualBlock virtualBlock)

    [<DllImport(lib, EntryPoint = "vmaSetVirtualAllocationUserData")>]
    extern void setVirtualAllocationUserData(VmaVirtualBlock virtualBlock, VmaVirtualAllocation allocation, nativeint pUserData)

    [<DllImport(lib, EntryPoint = "vmaGetVirtualBlockStatistics")>]
    extern void getVirtualBlockStatistics(VmaVirtualBlock virtualBlock, VmaStatistics* pStats)

    [<DllImport(lib, EntryPoint = "vmaCalculateVirtualBlockStatistics")>]
    extern void calculateVirtualBlockStatistics(VmaVirtualBlock virtualBlock, VmaDetailedStatistics* pStats)

    [<DllImport(lib, EntryPoint = "vmaBuildVirtualBlockStatsString")>]
    extern void buildVirtualBlockStatsString(VmaVirtualBlock virtualBlock, byte* * ppStatsString, VkBool32 detailedMap)

    [<DllImport(lib, EntryPoint = "vmaFreeVirtualBlockStatsString")>]
    extern void freeVirtualBlockStatsString(VmaVirtualBlock virtualBlock, cstr pStatsString)

    [<DllImport(lib, EntryPoint = "vmaBuildStatsString")>]
    extern void buildStatsString(VmaAllocator allocator, byte* * ppStatsString, VkBool32 detailedMap)

    [<DllImport(lib, EntryPoint = "vmaFreeStatsString")>]
    extern void freeStatsString(VmaAllocator allocator, cstr pStatsString)
