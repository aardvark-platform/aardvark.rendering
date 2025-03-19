namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering
open FSharp.NativeInterop
open System
open System.Threading
open System.Runtime.InteropServices
open Vulkan11
open KHRBufferDeviceAddress

#nowarn "9"

type DeviceHeap internal(device : IDevice, memory : MemoryInfo, isHostMemory : bool, printUsage : ILogger -> unit) as this =
    let heap = memory.heap
    let hostVisible = memory.flags |> MemoryFlags.hostVisible
    let manager = DeviceMemoryManager(this, 128L <<< 20, isHostMemory)
    let mask = 1u <<< memory.index

    let physical = device.PhysicalDevice
    let maxAllocationSize = physical.MaxAllocationSize

    let createNullPtr() =

        let info =
            VkMemoryAllocateInfo(
                16UL,
                uint32 memory.index
            )

        let mem =
            info |> NativePtr.pin (fun pInfo ->
                temporary<VkDeviceMemory, VkDeviceMemory> (fun pHandle ->
                    VkRaw.vkAllocateMemory(device.Handle, pInfo, NativePtr.zero, pHandle)
                        |> check "could not 'allocate' null pointer for device heap"
                    NativePtr.read pHandle
                )
            )

        let hostPtr =
            if hostVisible then
                temporary<nativeint, nativeint> (fun pPtr ->
                    VkRaw.vkMapMemory(device.Handle, mem, 0UL, 16UL, VkMemoryMapFlags.None, pPtr)
                        |> check "could not map memory"
                    NativePtr.read pPtr
                )
            else
                0n

        new DeviceMemory(this, mem, 0L, hostPtr)

    let mutable nullptr = None

    member x.AllocatedMemory = manager.AllocatedMemory
    member x.UsedMemory = manager.UsedMemory
    member internal x.DeviceInterface = device
    member x.Info = memory
    member x.Index = memory.index
    member internal x.Mask = mask
    member x.HeapFlags = heap.Flags
    member x.Flags = memory.flags
    member x.Available = heap.Available
    member x.Allocated = heap.Allocated
    member x.Capacity = heap.Capacity

    member x.IsHostVisible = hostVisible
    member x.IsHostCoherent = memory.flags.HasFlag MemoryFlags.HostCoherent

    member x.Null =
        lock x (fun () ->
            match nullptr with
                | Some ptr -> ptr
                | None ->
                    let ptr = createNullPtr()
                    nullptr <- Some ptr
                    ptr
        )

    member x.Alloc(align : int64, size : int64, [<Optional; DefaultParameterValue(false)>] export : bool) = manager.Alloc(align, size, export)
    member x.Free(ptr : DevicePtr) = ptr.Dispose()

    member x.TryAllocRaw(size : int64, [<Optional; DefaultParameterValue(false)>] export : bool, [<Out>] ptr : byref<DeviceMemory>) =
        if size > maxAllocationSize then
            false
        else
            if heap.TryAdd size then
                if export && not <| device.IsExtensionEnabled ExternalMemory.Extension then
                    failf "Cannot export memory when %s extension is disabled" ExternalMemory.Extension

                let mem =
                    native {
                        let allocFlags =
                            if device.PhysicalDevice.Features.Memory.BufferDeviceAddress then
                                VkMemoryAllocateFlags.DeviceAddressBitKhr
                            else
                                VkMemoryAllocateFlags.None

                        let! pExportInfo =
                            VkExportMemoryAllocateInfo(VkExternalMemoryHandleTypeFlags.OpaqueBit)

                        let pNext =
                            if export then pExportInfo.Address
                            else 0n

                        let! pFlagsInfo = VkMemoryAllocateFlagsInfo(pNext, allocFlags, 0u)

                        let! pInfo =
                            VkMemoryAllocateInfo(
                                NativePtr.toNativeInt pFlagsInfo,
                                uint64 size,
                                uint32 memory.index
                            )

                        let! pHandle = VkDeviceMemory.Null
                        let result = VkRaw.vkAllocateMemory(device.Handle, pInfo, NativePtr.zero, pHandle)
                        if result <> VkResult.Success then
                            printUsage Logger.Default
                            result |> check $"could not allocate {Mem size} of memory type {memory.index} in heap {heap.Index}"

                        return !!pHandle
                    }

                let externalHandle : IExternalMemoryHandle =
                    if export then
                        mem |> ExternalMemory.ofDeviceMemory device.Handle
                    else
                        null

                let hostPtr =
                    if hostVisible then
                        temporary<nativeint, nativeint> (fun pPtr ->
                            VkRaw.vkMapMemory(device.Handle, mem, 0UL, uint64 size, VkMemoryMapFlags.None, pPtr)
                                |> check "could not map memory"
                            NativePtr.read pPtr
                        )
                    else
                        0n

                ptr <- new DeviceMemory(x, mem, size, hostPtr, externalHandle)
                true
            else
                false

    member x.AllocRaw(size : int64, [<Optional; DefaultParameterValue(false)>] export : bool) =
        if size > maxAllocationSize then
            failf "could not allocate %A (exceeds MaxAllocationSize: %A)" (Mem size) (Mem maxAllocationSize)
        else
            match x.TryAllocRaw(size, export) with
            | (true, ptr) -> ptr
            | _ ->
                printUsage Logger.Default
                failf $"could not allocate {Mem size} of memory type {memory.index} in heap {heap.Index} (only {heap.Available} available)"

    member x.TryAllocRaw(mem : Mem, [<Out>] ptr : byref<DeviceMemory>) = x.TryAllocRaw(mem.Bytes, false, &ptr)
    member x.TryAllocRaw(mem : VkDeviceSize, [<Out>] ptr : byref<DeviceMemory>) = x.TryAllocRaw(int64 mem, false, &ptr)
    member x.AllocRaw(mem : Mem) = x.AllocRaw(mem.Bytes)
    member x.AllocRaw(mem : VkDeviceSize) = x.AllocRaw(int64 mem)



    member x.Free(ptr : DeviceMemory) =
        if ptr.Size <> 0L then
            lock ptr (fun () ->
                if ptr.Handle.IsValid then
                    heap.Remove ptr.Size
                    if hostVisible then VkRaw.vkUnmapMemory(device.Handle, ptr.Handle)
                    VkRaw.vkFreeMemory(device.Handle, ptr.Handle, NativePtr.zero)
                    ptr.Handle <- VkDeviceMemory.Null
                    ptr.Size <- 0L

                    if ptr.IsExported then
                        ptr.ExternalHandle.Dispose()
                        ptr.ExternalHandle <- null
            )

    member x.Dispose() =
        match nullptr with
            | Some ptr ->
                VkRaw.vkFreeMemory(device.Handle, ptr.Handle, NativePtr.zero)
                nullptr <- None
            | None -> ()

        manager.Clear()

    member x.Copy() = new DeviceHeap(device, memory, isHostMemory, printUsage)

    interface IDeviceObject with
        member x.DeviceInterface = x.DeviceInterface

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and [<AllowNullLiteral>] DeviceBlock(manager : DeviceMemoryManager, mem : DeviceMemory, offset : int64, size : int64, isFree : bool, prev : DeviceBlock, next : DeviceBlock) =
    inherit DevicePtr(mem, offset, size)

    let mutable prev = prev
    let mutable next = next
    let mutable isFree = isFree


    member x.IsFree
        with get() = isFree
        and set f = isFree <- f

    member x.Prev
        with get() = prev
        and set p = prev <- p

    member x.Next
        with get() = next
        and set p = next <- p

    override x.Dispose() =
        manager.Free x

and DeviceFreeList() =

    static let comparer =
        { new System.Collections.Generic.IComparer<DeviceBlock> with
            member x.Compare(l : DeviceBlock, r : DeviceBlock) =
                if isNull l then
                    if isNull r then 0
                    else 1
                elif isNull r then
                    -1
                else
                    let c = compare l.Size r.Size
                    if c <> 0 then c
                    else
                        let c = compare l.Offset r.Offset
                        if c <> 0 then c
                        else
                            let c = compare l.Memory.Handle.Handle r.Memory.Handle.Handle
                            if c = 0 then 0
                            else c
        }

    static let next (align : int64) (v : int64) =
        if v % align = 0L then v
        else v + (align - v % align)


    let store = SortedSetExt<DeviceBlock>(Seq.empty, comparer)
    let storeExported = SortedSetExt<DeviceBlock>(Seq.empty, comparer)

    let getStore (export : bool) =
        if export then
            storeExported
        else
            store

    member x.TryGetAlignedV(align : int64, size : int64, [<Optional; DefaultParameterValue(false)>] export : bool) =
        let min = new DeviceBlock(Unchecked.defaultof<_>, Unchecked.defaultof<_>, -1L, size, false, null, null)
        let store = getStore export
        let view = store.GetViewBetween(min, null)

        let mutable foundSlot = false
        let mutable e = view.GetEnumerator()
        while not foundSlot && e.MoveNext() do
            let b = e.Current
            let o = next align b.Offset
            let s = b.Size - (o - b.Offset)
            foundSlot <- s >= size

        if foundSlot then
            store.Remove e.Current |> ignore
            ValueSome e.Current
        else
            ValueNone

    member x.Insert(b : DeviceBlock) =
        let store = getStore b.Memory.IsExported
        store.Add b |> ignore

    member x.Remove(b : DeviceBlock) =
        let store = getStore b.Memory.IsExported
        store.Remove b |> ignore

    member x.Clear() =
        storeExported.Clear()
        store.Clear()

and DeviceMemoryManager internal(heap : DeviceHeap, blockSize : int64, keepReserveBlock : bool) =
    static let next (align : int64) (v : int64) =
        if v % align = 0L then v
        else v + (align - v % align)

    let free = DeviceFreeList()
    let blocks = System.Collections.Generic.HashSet<DeviceMemory>()
    let mutable allocatedMemory = 0L
    let mutable usedMemory = 0L

    let addBlock(this : DeviceMemoryManager) (export : bool) =
        let store = heap.AllocRaw(blockSize, export)

        Interlocked.Add(&allocatedMemory, blockSize) |> ignore
        blocks.Add store |> ignore

        let block = new DeviceBlock(this, store, 0L, blockSize, true, null, null)
        free.Insert(block)

    member x.AllocatedMemory = Mem allocatedMemory
    member x.UsedMemory = Mem usedMemory

    member x.Alloc(align : int64, size : int64, export : bool) =
        if size <= 0L then
            DevicePtr.Null
        elif size >= blockSize then
            let mem = heap.AllocRaw(size, export)
            Interlocked.Add(&usedMemory, size) |> ignore
            Interlocked.Add(&allocatedMemory, size) |> ignore
            { new DevicePtr(mem, 0L, size) with
                override x.Dispose() =
                    mem.Dispose()
                    Interlocked.Add(&usedMemory, -size) |> ignore
                    Interlocked.Add(&allocatedMemory, -size) |> ignore
            }

        else
            lock free (fun () ->
                match free.TryGetAlignedV(align, size, export) with
                | ValueSome b ->
                    let alignedOffset = next align b.Offset
                    let alignedSize = b.Size - (alignedOffset - b.Offset)
                    if alignedOffset > b.Offset then
                        let l = new DeviceBlock(x, b.Memory, b.Offset, alignedOffset - b.Offset, true, b.Prev, b)

                        if not (isNull l.Prev) then l.Prev.Next <- l
                        b.Prev <- l

                        free.Insert(l)
                        b.Offset <- alignedOffset
                        b.Size <- alignedSize


                    if alignedSize > size then
                        let r = new DeviceBlock(x, b.Memory, alignedOffset + size, alignedSize - size, true, b, b.Next)
                        if not (isNull r.Next) then r.Next.Prev <- r
                        b.Next <- r
                        free.Insert(r)
                        b.Size <- size

                    Interlocked.Add(&usedMemory, size) |> ignore
                    b.IsFree <- false
                    b :> DevicePtr

                | ValueNone ->
                    addBlock x export
                    x.Alloc(align, size, export)
            )

    member internal x.Free(b : DeviceBlock) =
        if not b.IsFree then
            lock free (fun () ->
                let old = b

                let b = new DeviceBlock(x, b.Memory, b.Offset, b.Size, b.IsFree, b.Prev, b.Next)
                if not (isNull b.Prev) then b.Prev.Next <- b
                if not (isNull b.Next) then b.Next.Prev <- b

                old.Next <- null
                old.Prev <- null
                old.Offset <- -1234L
                old.Size <- -2000L
                old.IsFree <- true
                Interlocked.Add(&usedMemory, -b.Size) |> ignore

                let prev = b.Prev
                let next = b.Next
                let mutable isFirst = isNull prev
                let mutable isLast = isNull next
                if not isFirst && prev.IsFree then
                    free.Remove(prev) |> ignore

                    b.Prev <- prev.Prev
                    if isNull b.Prev then isFirst <- true
                    else b.Prev.Next <- b

                    b.Offset <- prev.Offset
                    b.Size <- b.Size + prev.Size

                if not isLast && next.IsFree then
                    free.Remove(next) |> ignore
                    b.Next <- next.Next
                    if isNull b.Next then isLast <- true
                    else b.Next.Prev <- b

                    b.Size <- b.Size + next.Size

                b.IsFree <- true



                if isFirst && isLast && not keepReserveBlock then
                    assert (b.Offset = 0L && b.Size = b.Memory.Size)
                    blocks.Remove b.Memory |> ignore
                    Interlocked.Add(&allocatedMemory, -b.Memory.Size) |> ignore
                    b.Memory.Dispose()
                else
                    free.Insert(b)

            )

    member x.Clear() =
        lock free (fun () ->
            for f in blocks do f.Dispose()
            blocks.Clear()
            free.Clear()
            allocatedMemory <- 0L
            usedMemory <- 0L
        )


and DeviceMemory internal(heap : DeviceHeap, handle : VkDeviceMemory, size : int64, hostPtr : nativeint, externalHandle : IExternalMemoryHandle) =
    inherit DevicePtr(Unchecked.defaultof<_>, 0L, size)
    static let nullptr = new DeviceMemory(Unchecked.defaultof<_>, VkDeviceMemory.Null, 0L, 0n, null)

    let mutable handle = handle
    let mutable size = size
    let mutable externalHandle = externalHandle

    do if handle <> VkDeviceMemory.Null then heap.DeviceInterface.Instance.RegisterDebugTrace(handle.Handle)

    static member Null = nullptr

    new (heap : DeviceHeap, handle : VkDeviceMemory, size : int64, hostPtr : nativeint) =
        new DeviceMemory(heap, handle, size, hostPtr, null)

    member x.Heap = heap

    member x.Handle
        with get() : VkDeviceMemory = handle
        and internal set h = handle <- h

    member x.Size
        with get() : int64 = size
        and internal set s = size <- s

    member x.IsNull = handle.IsNull
    member x.IsValid = handle.IsValid
    member x.IsExported = externalHandle <> null

    member x.HostPointer = hostPtr

    member x.ExternalHandle
        with get() : IExternalMemoryHandle = externalHandle
        and internal set h = externalHandle <- h

    member x.ExternalBlock =
        { Handle = x.ExternalHandle
          SizeInBytes = x.Size }

    override x.Dispose() = heap.Free(x)
    override x.Memory = x

and [<AllowNullLiteral>] DevicePtr internal(memory : DeviceMemory, offset : int64, size : int64) =
    let mutable size = size
    let mutable offset = offset

    static let nullptr = lazy (new DevicePtr(DeviceMemory.Null, 0L, 0L))
    static member Null = nullptr.Value

    abstract member Memory : DeviceMemory
    default x.Memory = memory

    abstract member Dispose : unit -> unit
    default x.Dispose() = ()

    abstract member TryResize : int64 -> bool
    default x.TryResize (s : int64) = s = size

    member x.Offset
        with get() = offset
        and internal set o = offset <- o

    member x.Size
        with get() = size
        and internal set s = size <- s

    interface IDeviceObject with
        member x.DeviceInterface = memory.Heap.DeviceInterface

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    member x.Borrow = new DevicePtr(memory, offset, size)
    member x.View(off : int64, s : int64) = new DevicePtr(memory, offset + off, s)
    member x.Skip(off : int64) = new DevicePtr(memory, offset + off, size - off)
    member x.Take(s : int64) = new DevicePtr(memory, offset, s)

    member x.GetSlice(min : Option<int64>, max : Option<int64>) =
        let min = defaultArg min 0L
        let max = defaultArg max (size - 1L)
        new DevicePtr(memory, min, 1L + max - min)

    static member (+) (ptr : DevicePtr, off : int64) = new DevicePtr(ptr.Memory, ptr.Offset + off, ptr.Size - off)
    static member (+) (ptr : DevicePtr, off : int) = ptr + int64 off
    static member (+) (ptr : DevicePtr, off : nativeint) = ptr + int64 off
    static member (+) (off : int64, ptr : DevicePtr) = new DevicePtr(ptr.Memory, ptr.Offset + off, ptr.Size - off)
    static member (+) (off : int, ptr : DevicePtr) = ptr + int64 off
    static member (+) (off : nativeint, ptr : DevicePtr) = ptr + int64 off
    static member (-) (ptr : DevicePtr, off : int64) = new DevicePtr(ptr.Memory, ptr.Offset - off, ptr.Size + off)
    static member (-) (ptr : DevicePtr, off : int) = ptr - int64 off
    static member (-) (ptr : DevicePtr, off : nativeint) = ptr - int64 off

    member x.Mapped (f : nativeint -> 'a) =
        let memory = x.Memory
        if memory.Heap.IsHostVisible then
            let device = memory.Heap.DeviceInterface
            Monitor.Enter x
            try
                let ptr = memory.HostPointer + nativeint x.Offset
                f ptr
            finally
                if not memory.Heap.IsHostCoherent then
                    let range = VkMappedMemoryRange(memory.Handle, uint64 x.Offset, uint64 x.Size)
                    range |> NativePtr.pin (fun pRange ->
                        VkRaw.vkFlushMappedMemoryRanges(device.Handle, 1u, pRange)
                            |> check "could not flush memory range"
                    )

                Monitor.Exit x
        else
            failf "cannot map host-invisible memory"