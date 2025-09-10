namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan.Memory
open System
open System.Runtime.InteropServices

#nowarn "51"

/// Represents a memory allocation.
type DevicePtr =
    val private device : IDevice
    val private allocator : VmaAllocator
    val mutable private allocation : VmaAllocation
    val private allocationInfo : VmaAllocationInfo2
    val private hostVisible : bool
    val mutable private externalBlock : ExternalMemoryBlock

    internal new (device: IDevice, allocator: VmaAllocator, allocation: VmaAllocation, hostVisible: bool, export: bool) =
        let mutable info = VmaAllocationInfo2.Empty
        Vma.getAllocationInfo2(allocator, allocation, &&info)

        let externalBlock =
            if export then
                let handle =
                    if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                        ExternalMemory.Win32.getHandle allocator allocation
                    else
                        ExternalMemory.Posix.getHandle device.Handle info.allocationInfo.deviceMemory

                new ExternalMemoryBlock(handle, info.allocationInfo.deviceMemory, info.blockSize)
            else
                null

        { device         = device
          allocator      = allocator
          allocation     = allocation
          allocationInfo = info
          hostVisible    = hostVisible
          externalBlock  = externalBlock }

    internal new (device: IDevice) =
        { device         = device
          allocator      = VmaAllocator.Zero
          allocation     = VmaAllocation.Zero
          allocationInfo = VmaAllocationInfo2.Empty
          hostVisible    = false
          externalBlock  = null }

    member this.ExternalBlock : IExternalMemoryBlock =
        if isNull this.externalBlock then
            raise <| NotSupportedException("[Vulkan] Cannot access external handle of unexported memory allocation.")

        this.externalBlock

    member this.Block = this.allocationInfo.allocationInfo.deviceMemory
    member this.BlockSize = this.allocationInfo.blockSize
    member this.Offset = this.allocationInfo.allocationInfo.offset
    member this.Size = this.allocationInfo.allocationInfo.size
    member this.IsDedicated = this.allocationInfo.dedicatedMemory = VkTrue
    member this.IsHostVisible = this.hostVisible

    /// Copy from the given host memory pointer to the allocation.
    member this.CopyFrom(offset: uint64, size: uint64, src: nativeint) =
        if not this.hostVisible then
            raise <| NotSupportedException("[Vulkan] Cannot copy to memory allocation without host access.")

        Vma.copyMemoryToAllocation(this.allocator, src, this.allocation, offset, size)
            |> checkf "could not copy memory"

    /// Copy from the given host memory pointer to the allocation.
    member this.CopyFrom(size: uint64, src: nativeint) =
        this.CopyFrom(0UL, size, src)

    /// Copy from the allocation to the given host memory pointer.
    member this.CopyTo(offset: uint64, size: uint64, dst: nativeint) =
        if not this.hostVisible then
            raise <| NotSupportedException("[Vulkan] Cannot copy to memory allocation without host access.")

        Vma.copyAllocationToMemory(this.allocator, this.allocation, offset, dst, size)
            |> checkf "could not copy memory"

    /// Copy from the allocation to the given host memory pointer.
    member this.CopyTo(size: uint64, dst: nativeint) =
        this.CopyTo(0UL, size, dst)

    member this.Mapped (offset: uint64, size: uint64, action: nativeint -> 'T) =
        if not this.hostVisible then
            raise <| NotSupportedException("[Vulkan] Cannot map memory allocation without host access.")

        let result = action this.allocationInfo.allocationInfo.pMappedData

        Vma.flushAllocation(this.allocator, this.allocation, offset, size)
            |> checkf "could not flush memory allocation"

        result

    member this.Mapped (action: nativeint -> 'T) =
        this.Mapped(0UL, VkWholeSize, action)

    member this.BindBuffer(buffer: VkBuffer) =
        Vma.bindBufferMemory(this.allocator, this.allocation, buffer)
            |> checkf "failed to bind buffer memory"

    member this.BindBuffer(buffer: VkBuffer, offset: uint64, pNext: nativeptr<'T>) =
        Vma.bindBufferMemory2(this.allocator, this.allocation, offset, buffer, pNext.Address)
            |> checkf "failed to bind buffer memory"

    member this.BindImage(image: VkImage) =
        Vma.bindImageMemory(this.allocator, this.allocation, image)
            |> checkf "failed to bind image memory"

    member this.BindImage(image: VkImage, offset: uint64, pNext: nativeptr<'T>) =
        Vma.bindImageMemory2(this.allocator, this.allocation, offset, image, pNext.Address)
            |> checkf "failed to bind image memory"

    member this.Dispose() =
        if this.allocation <> 0n then
            if notNull this.externalBlock then
                this.externalBlock.Dispose()
                this.externalBlock <- null

            Vma.freeMemory(this.allocator, this.allocation)
            this.allocation <- 0n

    interface IDeviceObject with
        member x.DeviceInterface = x.device

    interface IDisposable with
        member x.Dispose() = x.Dispose()