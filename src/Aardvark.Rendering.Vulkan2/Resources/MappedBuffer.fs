namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Rendering.Vulkan


#nowarn "9"
#nowarn "51"


type MappedBuffer(device : Device, usage : VkBufferUsageFlags, handle : VkBuffer) =
    inherit Resource<VkBuffer>(device, handle)

    let mutable reqs = VkMemoryRequirements()
    do VkRaw.vkGetBufferMemoryRequirements(device.Handle, handle, &&reqs)
    let align = int64 reqs.alignment
    let memoryTypeBits = reqs.memoryTypeBits

    let memories = List<DevicePtr>()
    let mutable capacity = 0L

    let rw = new ReaderWriterLockSlim()

    member x.Capacity = capacity
    member x.Size = Mem capacity

    member x.Resize(newSize : int64) =
        let newSize = Fun.NextPowerOfTwo newSize |> Alignment.next align

        ReaderWriterLock.write rw (fun () ->
            if capacity <> 0L && newSize = 0L then
                let mutable unbind =
                    VkSparseMemoryBind(
                        0UL,
                        uint64 capacity,
                        VkDeviceMemory.Null,
                        0UL,
                        VkSparseMemoryBindFlags.None
                    ) 

                let mutable bufferInfo =
                    VkSparseBufferMemoryBindInfo(
                        handle, 
                        1u, &&unbind
                    )

                let mutable bindInfo =
                    VkBindSparseInfo(
                        VkStructureType.BindSparseInfo, 0n,
                        0u, NativePtr.zero,
                        1u, &&bufferInfo,
                        0u, NativePtr.zero,
                        0u, NativePtr.zero,
                        0u, NativePtr.zero
                    )

                let fence = device.CreateFence()
                device.TransferFamily.UsingQueue (fun queue ->
                    VkRaw.vkQueueBindSparse(queue.Handle, 1u, &&bindInfo, fence.Handle)
                        |> check "could not bind buffer memory"

                    fence.Wait()
                )

                for m in memories do m.Dispose()
                memories.Clear()

                capacity <- 0L


            elif capacity < newSize then
                let delta = newSize - capacity |> Alignment.next align
                let newSize = capacity + delta

                let part = VkMemoryRequirements(uint64 delta, uint64 align, memoryTypeBits)
                let ptr = device.Alloc(part, true)
                memories.Add ptr

                let mutable bind =
                    VkSparseMemoryBind(
                        uint64 capacity,
                        uint64 delta,
                        ptr.Memory.Handle,
                        uint64 ptr.Offset,
                        VkSparseMemoryBindFlags.None
                    )

                let mutable bufferInfo =
                    VkSparseBufferMemoryBindInfo(
                        handle, 
                        1u, &&bind
                    )

                let mutable bindInfo =
                    VkBindSparseInfo(
                        VkStructureType.BindSparseInfo, 0n,
                        0u, NativePtr.zero,
                        1u, &&bufferInfo,
                        0u, NativePtr.zero,
                        0u, NativePtr.zero,
                        0u, NativePtr.zero
                    )

                let fence = device.CreateFence()
                device.TransferFamily.UsingQueue (fun queue ->
                    VkRaw.vkQueueBindSparse(queue.Handle, 1u, &&bindInfo, fence.Handle)
                        |> check "could not bind buffer memory"

                    fence.Wait()
                )

                capacity <- newSize
        
            elif capacity > newSize then
                let delta = capacity - newSize |> Alignment.next align
                let newSize = capacity - delta

                let mutable unbind =
                    VkSparseMemoryBind(
                        uint64 newSize,
                        uint64 delta,
                        VkDeviceMemory.Null,
                        0UL,
                        VkSparseMemoryBindFlags.None
                    ) 

                let mutable bufferInfo =
                    VkSparseBufferMemoryBindInfo(
                        handle, 
                        1u, &&unbind
                    )

                let mutable bindInfo =
                    VkBindSparseInfo(
                        VkStructureType.BindSparseInfo, 0n,
                        0u, NativePtr.zero,
                        1u, &&bufferInfo,
                        0u, NativePtr.zero,
                        0u, NativePtr.zero,
                        0u, NativePtr.zero
                    )

                let fence = device.CreateFence()
                device.TransferFamily.UsingQueue (fun queue ->
                    VkRaw.vkQueueBindSparse(queue.Handle, 1u, &&bindInfo, fence.Handle)
                        |> check "could not bind buffer memory"

                    fence.Wait()
                )

                let mutable last = memories.[memories.Count - 1]
                let mutable total = capacity
                while total - last.Size >= newSize do
                    memories.RemoveAt(memories.Count-1)
                    total <- total - last.Size
                    last.Dispose()
                    last <- memories.[memories.Count - 1]

                if total > newSize then
                    let tooMuch = total - newSize
                    let worked = last.TryResize(last.Size - tooMuch)
                    if not worked then failf "cannot resize memory"

                capacity <- newSize

        )
    
    member x.UseWrite(offset : int64, size : int64, writer : nativeint -> 'a) =
        if offset < 0L || size < 0L || offset + size > capacity then
            failf "MappedBuffer range out of bounds { offset = %A; size = %A; capacity = %A" offset size capacity
        
        if usage &&& VkBufferUsageFlags.TransferDstBit = VkBufferUsageFlags.None then
            failf "MappedBuffer not writeable"

        ReaderWriterLock.read rw (fun () ->
            let align = device.MinUniformBufferOffsetAlignment
            let alignedSize = size |> Alignment.next align
            let temp = device.HostMemory.Alloc(align, alignedSize)

            let res = temp.Mapped writer

            let dst = Buffer(device, handle, new DevicePtr(Unchecked.defaultof<_>, 0L, capacity))
            use token = device.ResourceToken
            token.enqueue {
                try do! Command.Copy(temp, 0L, dst, offset, size)
                finally temp.Dispose()
            }

            res
        )

    member x.UseRead(offset : int64, size : int64, reader : nativeint -> 'a) =
        if offset < 0L || size < 0L || offset + size > capacity then
            failf "MappedBuffer range out of bounds { offset = %A; size = %A; capacity = %A" offset size capacity

        if usage &&& VkBufferUsageFlags.TransferSrcBit = VkBufferUsageFlags.None then
            failf "MappedBuffer not readable"
        
        ReaderWriterLock.read rw (fun () ->
            let align = device.MinUniformBufferOffsetAlignment
            let alignedSize = size |> Alignment.next align
            use temp = device.HostMemory.Alloc(align, alignedSize)
            let src = Buffer(device, handle, new DevicePtr(Unchecked.defaultof<_>, 0L, capacity))

            let family = device.TransferFamily
            use cmd = family.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
            cmd.Begin(CommandBufferUsage.OneTimeSubmit)
            cmd.enqueue {
                try do! Command.Copy(src, offset, temp, 0L, size)
                finally temp.Dispose()
            }
            cmd.End()
            family.RunSynchronously(cmd)


            let res = temp.Mapped reader
            res
        )

    member x.Write<'a when 'a : unmanaged> (offset : int64, data : 'a[], startIndex : int, count : int) =
        let sa = int64 sizeof<'a>
        let size = sa * int64 count
        let srcOffset = sa * int64 startIndex |> nativeint
        x.UseWrite(offset, size, fun ptr ->
            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
            try Marshal.Copy(gc.AddrOfPinnedObject() + srcOffset, ptr, size) 
            finally gc.Free()
        )

    member x.Read<'a when 'a : unmanaged> (offset : int64, data : 'a[], startIndex : int, count : int) =
        let sa = int64 sizeof<'a>
        let size = sa * int64 count
        let srcOffset = sa * int64 startIndex |> nativeint
        x.UseRead(offset, size, fun ptr ->
            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
            try Marshal.Copy(ptr, gc.AddrOfPinnedObject() + srcOffset, size) 
            finally gc.Free()
        )

    member x.Write<'a when 'a : unmanaged> (offset : int64, data : 'a[]) = x.Write(offset, data, 0, data.Length)
    member x.Read<'a when 'a : unmanaged> (offset : int64, data : 'a[]) = x.Read(offset, data, 0, data.Length)
    member x.Read<'a when 'a : unmanaged>(offset : int64, count : int) = 
        let arr : 'a[] = Array.zeroCreate count
        x.Read(offset, arr)
        arr

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MappedBuffer =
    let create (usage : VkBufferUsageFlags) (device : Device) =
        let virtualSize = 1UL <<< 30
        let mutable info =
            VkBufferCreateInfo(
                VkStructureType.BufferCreateInfo, 0n,
                VkBufferCreateFlags.SparseBindingBit ||| VkBufferCreateFlags.SparseResidencyBit,
                virtualSize,
                usage,
                VkSharingMode.Exclusive,
                0u, NativePtr.zero
            )

        let mutable handle = VkBuffer.Null

        VkRaw.vkCreateBuffer(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "could not create sparse buffer"

        MappedBuffer(device, usage, handle)