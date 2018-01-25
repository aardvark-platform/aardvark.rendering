﻿namespace Aardvark.Rendering.Vulkan

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

[<AllowNullLiteral>]
type private SparseBlock =
    class
        val mutable public Min      : int64
        val mutable public Max      : int64
        val mutable public Pointer  : Option<DevicePtr>
        val mutable public Next     : SparseBlock
        val mutable public Prev     : SparseBlock

        member inline x.Overlaps(other : SparseBlock) =
            //not (x.Min > other.Max || x.Min < other.Min)

            x.Min <= other.Max && x.Max >= other.Min

        interface IComparable with
            member x.CompareTo o =
                match o with
                    | :? SparseBlock as o -> compare x.Min o.Min
                    | _ -> failf "cannot compare SparseBlock to %A" o

        interface IComparable<SparseBlock> with
            member x.CompareTo o = compare x.Min o.Min

        override x.GetHashCode() =
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode x

        override x.Equals o =
            System.Object.ReferenceEquals(x,o)

        new(min : int64, max : int64, ptr, prev, next) = { Min = min; Max = max; Pointer = ptr; Prev = prev; Next = next }
    end

[<Obsolete("not finished yet")>]
type SparseBuffer(device : Device, usage : VkBufferUsageFlags, handle : VkBuffer, virtualSize : int64) =
    inherit Buffer(device, handle, new DevicePtr(DeviceMemory.Null, 0L, virtualSize), virtualSize, usage)
    
    let mutable reqs = VkMemoryRequirements()
    do VkRaw.vkGetBufferMemoryRequirements(device.Handle, handle, &&reqs)
    let align = int64 reqs.alignment
    let memoryTypeBits = reqs.memoryTypeBits

    let malloc(size : int64) =
        let reqs = VkMemoryRequirements(uint64 size, uint64 align, memoryTypeBits)
        device.Alloc(reqs, true)



    let mutable first = SparseBlock(0L, virtualSize-1L, None, null, null)
    let mutable last = first
    let blocks = SortedSetExt<SparseBlock>([first])

    let free (block : SparseBlock) =
        block.Pointer <- None

        let l = block.Prev
        let r = block.Next

        if not (isNull l) && Option.isNone l.Pointer then
            blocks.Remove l |> ignore
            blocks.Remove block |> ignore

            block.Min <- l.Min
            if isNull l.Prev then first <- block
            else l.Prev.Next <- block
            block.Prev <- l.Prev

            blocks.Add block |> ignore

        if not (isNull r) && Option.isNone r.Pointer then
            blocks.Remove r |> ignore

            block.Max <- r.Max
            if isNull r.Next then last <- block
            else r.Next.Prev <- block
            block.Next <- r.Next

    let setMax (current : SparseBlock) (max : int64) =
        let rest = SparseBlock(max + 1L, current.Max, None, current, current.Next)
        if isNull current.Next then last <- rest
        else current.Next.Prev <- rest
        current.Next <- rest
        current.Max <- max
        blocks.Add rest |> ignore
        let o = current.Pointer
        current.Pointer <- Some Unchecked.defaultof<_>
        free rest
        current.Pointer <- o

    let getFirstOverlapping (min : int64) =
        let self = SparseBlock(min, min, None, null, null)
        let mutable current =
            let found, smaller = blocks.TryFindSmaller(self)
            if found then smaller
            else first

        if not (current.Overlaps self) then
            current <- current.Next

        assert (current.Overlaps self)
        current

    let rec commitEverythingTo commits (current : SparseBlock) (max : int64) =
        if current.Min > max then
            commits
        else
            match current.Pointer with
                | Some ptr ->
                    commitEverythingTo commits current.Next max
                | None ->
                    if current.Max <= max then
                        let mem = malloc (1L + current.Max - current.Min)
                        current.Pointer <- Some mem
                        commitEverythingTo ((current.Min, current.Max, mem) :: commits) current.Next max
                    else
                        setMax current max
                        commitEverythingTo commits current max
                        
    let rec decommitEverythingTo decommits (current : SparseBlock) (max : int64) =
        if current.Min > max then
            decommits
        else
            match current.Pointer with
                | None -> 
                    decommitEverythingTo decommits current.Next max

                | Some ptr ->
                    if current.Max <= max then
                        current.Pointer <- None
                        let cmin = current.Min
                        let cmax = current.Max
                        ptr.Dispose()
                        free current
                        decommitEverythingTo ((cmin, cmax) :: decommits) current.Next max
                    else
                        failf "cannot decommit the start of a committed region"
//                        let cmax = current.Max
//                        ptr.TryResize(1L + max - current.Min) |> ignore
//                        setMax current max
//                        (cmax, max) :: decommits

    let commit (min : int64) (max : int64) =
        let current = getFirstOverlapping min

        match current.Pointer with
            | Some ptr -> 
                commitEverythingTo [] current max

            | None ->
                if current.Min = min then
                    commitEverythingTo [] current max
                else
                    setMax current (min - 1L)
                    commitEverythingTo [] current.Next max
     
    let decommit (min : int64) (max : int64) =
        let current = getFirstOverlapping min
        
        match current.Pointer with
            | Some ptr ->
                ptr.TryResize(1L + min - current.Min) |> ignore
                setMax current (min - 1L)
                decommitEverythingTo [] current.Next max

            | None ->
                decommitEverythingTo [] current max
          
          
    let resourceLock = ResourceLock()
    
    member x.Lock = resourceLock
    interface ILockedResource with
        member x.Lock = resourceLock
        member x.OnLock u = ()
        member x.OnUnlock u = ()
         
    interface IDisposable with
        member x.Dispose() = x.Dispose()
         
    member x.Clear() =
        x.Decommit(0L, virtualSize)
        
    member x.Dispose() =
        x.Clear()
        device.Delete x

    member x.Commit(offset : int64, size : int64) =
        LockedResource.update x (fun () ->
            let commits = commit offset (offset + size - 1L)

            let binds =
                commits
                    |> List.toArray
                    |> Array.map (fun (min, max, ptr) ->
                        VkSparseMemoryBind(
                            uint64 min, 
                            uint64 (max - min + 1L),
                            ptr.Memory.Handle,
                            uint64 ptr.Offset,
                            VkSparseMemoryBindFlags.None
                        )
                    )

            if binds.Length > 0 then
                let queue = device.TransferFamily.GetQueue()
                let fence = device.CreateFence()

                lock queue (fun () ->
                    binds |> NativePtr.withA (fun pBinds ->

                        let mutable bufferInfo =
                            VkSparseBufferMemoryBindInfo(
                                handle, 
                                uint32 binds.Length, pBinds
                            )
                        let bindInfo =
                            VkBindSparseInfo(
                                VkStructureType.BindSparseInfo, 0n,
                                0u, NativePtr.zero,
                                1u, &&bufferInfo,
                                0u, NativePtr.zero,
                                0u, NativePtr.zero,
                                0u, NativePtr.zero
                            )

                        queue.BindSparse([| bindInfo |], fence.Handle)
                            |> check "could not bind buffer memory"
                    )
                )
                fence.Wait()
        )

    member x.Decommit(offset : int64, size : int64) =
        LockedResource.update x (fun () ->
            let decommits = decommit offset (offset + size - 1L)

            let unbinds =
                decommits
                    |> List.toArray
                    |> Array.map (fun (min, max) ->
                        VkSparseMemoryBind(
                            uint64 min, 
                            uint64 (max - min + 1L),
                            VkDeviceMemory.Null,
                            0UL,
                            VkSparseMemoryBindFlags.None
                        )
                    )

            if unbinds.Length > 0 then
                let queue = device.TransferFamily.GetQueue()
                let fence = device.CreateFence()

                lock queue (fun () ->
                    unbinds |> NativePtr.withA (fun pUnbinds ->

                        let mutable bufferInfo =
                            VkSparseBufferMemoryBindInfo(
                                handle, 
                                uint32 unbinds.Length, pUnbinds
                            )

                        let bindInfo =
                            VkBindSparseInfo(
                                VkStructureType.BindSparseInfo, 0n,
                                0u, NativePtr.zero,
                                1u, &&bufferInfo,
                                0u, NativePtr.zero,
                                0u, NativePtr.zero,
                                0u, NativePtr.zero
                            )

                        queue.BindSparse([| bindInfo |], fence.Handle)
                            |> check "could not bind buffer memory"
                    )
                )
                fence.Wait()

        )

type ResizeBuffer(device : Device, usage : VkBufferUsageFlags, handle : VkBuffer, virtualSize : int64) =
    inherit Buffer(device, handle, new DevicePtr(DeviceMemory.Null, 0L, 2L <<< 30), virtualSize, usage)

    let align, memoryTypeBits = 
        let mutable reqs = VkMemoryRequirements()
        VkRaw.vkGetBufferMemoryRequirements(device.Handle, handle, &&reqs)
        int64 reqs.alignment, reqs.memoryTypeBits

    let resourceLock = new ResourceLock()

    let malloc (size : int64) =
        assert (size % align = 0L)
        device.Alloc(VkMemoryRequirements(uint64 size, uint64 align, memoryTypeBits), true)

    let memories = List<int64 * DevicePtr>()
    let mutable capacity = 0L

    let grow (additionalBytes : int64) =
        let offset = capacity
        let ptr = malloc additionalBytes
        memories.Add(offset, ptr)

        VkSparseMemoryBind(
            uint64 offset,
            uint64 additionalBytes,
            ptr.Memory.Handle,
            uint64 ptr.Offset,
            VkSparseMemoryBindFlags.None
        )

    let rec shrink (i : int) (freeBytes : int64) =
        if i < 0 || freeBytes <= 0L then 
            []
        else
            let offset, mem = memories.[i]
            let size = mem.Size
            if size <= freeBytes then
                mem.Dispose()
                memories.RemoveAt i

                let unbind =
                    VkSparseMemoryBind(
                        uint64 offset,
                        uint64 size,
                        VkDeviceMemory.Null,
                        0UL,
                        VkSparseMemoryBindFlags.None
                    )

                unbind :: shrink (i-1) (freeBytes - size)

            else
                let newSize = size - freeBytes
                let worked = mem.TryResize newSize
                if not worked then failf "could not resize memory"

                let unbind =
                    VkSparseMemoryBind(
                        uint64 (offset + newSize),
                        uint64 freeBytes,
                        VkDeviceMemory.Null,
                        0UL,
                        VkSparseMemoryBindFlags.None
                    )

                [unbind]

    member x.Lock = resourceLock

    member x.Capacity = capacity

    member x.Resize(newCapacity : int64) =
        let newCapacity = Fun.NextPowerOfTwo newCapacity |> Alignment.next align
        if capacity <> newCapacity then
            LockedResource.update x (fun () ->
                let binds = 
                    if capacity < newCapacity then
                        [ grow (newCapacity - capacity) ]

                    elif capacity > newCapacity then
                        if memories.Count > 0 then
                            shrink (memories.Count - 1) (capacity - newCapacity)
                        else
                            []
                    else
                        []

                match binds with
                    | [] -> ()
                    | binds ->
                        let binds = List.toArray binds
                        let queue = device.TransferFamily.GetQueue()
                        let fence = device.CreateFence()

                        lock queue (fun () ->
                            binds |> NativePtr.withA (fun pBinds ->

                                let mutable bufferInfo =
                                    VkSparseBufferMemoryBindInfo(
                                        handle, 
                                        uint32 binds.Length, pBinds
                                    )

                                let bindInfo =
                                    VkBindSparseInfo(
                                        VkStructureType.BindSparseInfo, 0n,
                                        0u, NativePtr.zero,
                                        1u, &&bufferInfo,
                                        0u, NativePtr.zero,
                                        0u, NativePtr.zero,
                                        0u, NativePtr.zero
                                    )

                                queue.BindSparse([| bindInfo |], fence.Handle)
                                    |> check "could not bind buffer memory"
                            )
                        )
                        fence.Wait()


                capacity <- newCapacity
            )

    member x.UseWrite(offset : int64, size : int64, writer : nativeint -> 'a) =
        if offset < 0L || size < 0L || offset + size > capacity then
            failf "MappedBuffer range out of bounds { offset = %A; size = %A; capacity = %A" offset size capacity
        
        if usage &&& VkBufferUsageFlags.TransferDstBit = VkBufferUsageFlags.None then
            failf "MappedBuffer not writeable"

        LockedResource.access x (fun () ->
            let align = device.MinUniformBufferOffsetAlignment
            let alignedSize = size |> Alignment.next align
            let temp = device.HostMemory.Alloc(align, alignedSize)
            let res = temp.Mapped writer
            device.TransferFamily.run {
                try do! Command.Copy(temp, 0L, x, offset, size)
                finally temp.Dispose()
            }
            res
        )

    member x.UseRead(offset : int64, size : int64, reader : nativeint -> 'a) =
        if offset < 0L || size < 0L || offset + size > capacity then
            failf "MappedBuffer range out of bounds { offset = %A; size = %A; capacity = %A" offset size capacity

        if usage &&& VkBufferUsageFlags.TransferSrcBit = VkBufferUsageFlags.None then
            failf "MappedBuffer not readable"
        
        LockedResource.access x (fun () ->
            let align = device.MinUniformBufferOffsetAlignment
            let alignedSize = size |> Alignment.next align
            let temp = device.HostMemory.Alloc(align, alignedSize)

            device.TransferFamily.run {
                try do! Command.Copy(x, offset, temp, 0L, size)
                finally temp.Dispose()
            }


            let res = temp.Mapped reader
            res
        )

    member x.Dispose() =
        if x.Handle.IsValid then
            x.Resize 0L
            device.Delete x

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface ILockedResource with
        member x.Lock = resourceLock
        member x.OnLock u = ()
        member x.OnUnlock u = ()

    interface IResizeBuffer with
        member x.Resize c = x.Resize (int64 c)
        member x.UseRead(offset, size, reader) = x.UseRead(int64 offset, int64 size, reader)
        member x.UseWrite(offset, size, writer) = x.UseWrite(int64 offset, int64 size, writer)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ResizeBuffer =
    let create (usage : VkBufferUsageFlags) (device : Device) =
        let usage = usage ||| VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.TransferSrcBit

        let virtualSize = 2L <<< 30
        let mutable info =
            VkBufferCreateInfo(
                VkStructureType.BufferCreateInfo, 0n,
                VkBufferCreateFlags.SparseBindingBit ||| VkBufferCreateFlags.SparseResidencyBit,
                uint64 virtualSize,
                usage,
                device.AllSharingMode,
                device.AllQueueFamiliesCnt,
                device.AllQueueFamiliesPtr
            )

        let mutable handle = VkBuffer.Null

        VkRaw.vkCreateBuffer(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "could not create sparse buffer"

        new ResizeBuffer(device, usage, handle, virtualSize)

    let delete (b : ResizeBuffer) (d : Device) =
        b.Dispose()
