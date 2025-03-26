namespace Aardvark.Rendering.Vulkan.Memory

#nowarn "51"

open Aardvark.Base
open Aardvark.Rendering.Vulkan
open System
open Vulkan11

type internal ExternalMemoryPool(allocator: VmaAllocator, memoryTypeIndex: uint32) =
    let mutable pExportInfo = NativePtr.alloc 1

    let mutable createInfo =
        { VmaPoolCreateInfo.Empty with
            memoryTypeIndex     = memoryTypeIndex
            pMemoryAllocateNext = pExportInfo.Address }

    let mutable handle = VmaPool.Zero
    do
        pExportInfo.[0] <- VkExportMemoryAllocateInfo(VkExternalMemoryHandleTypeFlags.OpaqueBit)
        Vma.createPool(allocator, &&createInfo, &&handle)
            |> check "failed to create pool"

    member _.Handle = handle

    member x.Dispose() =
        if handle <> VmaPool.Zero then
            NativePtr.free pExportInfo
            pExportInfo <- NativePtr.zero
            Vma.destroyPool(allocator, handle)
            handle <- VmaPool.Zero

    interface IDisposable with
        member x.Dispose() = x.Dispose()