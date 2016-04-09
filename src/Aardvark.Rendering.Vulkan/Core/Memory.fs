namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Threading
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base


type devicemem internal(mem : DeviceMemory, handle : VkDeviceMemory, size : int64) =
    inherit Resource(if Unchecked.isNull mem then None else Some (mem.Device :> Resource))

    static let nullPtr = new devicemem(Unchecked.defaultof<_>, VkDeviceMemory.Null, -1L)
    let mutable mem = mem
    let mutable handle = handle



    static member Null = nullPtr

    member x.IsNull = handle.IsNull

    member x.Memory = mem
    member x.Handle = handle

    override x.Release() =
        let mem = Interlocked.Exchange(&mem, Unchecked.defaultof<_>)
        if Unchecked.notNull mem then
            mem.Heap.Remove size
            VkRaw.vkFreeMemory(mem.Device.Handle, handle, NativePtr.zero)
            handle <- VkDeviceMemory.Null
          
  
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DeviceMem =
    let alloc (size : int64) (this : DeviceMemory) =
        if this.Heap.TryAdd size then
            let mutable mem = VkDeviceMemory.Null
            let mutable info =
                VkMemoryAllocateInfo(
                    VkStructureType.MemoryAllocateInfo,
                    0n,
                    uint64 size,
                    uint32 this.TypeIndex
                )

            VkRaw.vkAllocateMemory(this.Device.Handle, &&info, NativePtr.zero, &&mem)
                |> check "vkAllocateMemory"

            new devicemem(this, mem, size)
        else
            failf "out of memory (tried to allocate %A)" (size_t size)
  
    let free (mem : devicemem) = mem.Dispose()
   
        
                
[<AllowNullLiteral>]
type ManagedBlock =
    class
        val mutable public Memory   : devicemem
        val mutable public Offset   : int64
        val mutable public Size     : int64
        val mutable public IsFree   : bool
        val mutable internal Next   : ManagedBlock
        val mutable internal Prev   : ManagedBlock
        val mutable internal Tag    : obj

        new(h, off, size, prev, next) = { Memory = h; Offset = off; Size = size; Prev = prev; Next = next; IsFree = true; Tag = null }

    end

type IMemoryManager =
    inherit IDisposable
    abstract member Memory : DeviceMemory
    abstract member FreeBlock : ManagedBlock -> unit
    abstract member TryAllocBlock : int64 -> Option<ManagedBlock>



type internal deviceptrimpl =
    private 
    | Null
    | Real of handle : devicemem * size : int64
    | View of handle : devicemem * offset : int64 * size : int64
    | Managed of manager : IMemoryManager * block : ManagedBlock * size : int64

[<CompiledName("DevicePtr")>]
type deviceptr internal(pointer : deviceptrimpl) =
    inherit Resource()

    let mutable pointer = pointer

    member x.IsHostVisible =
        match pointer with
            | Null -> true
            | Real(m,_) -> m.Memory.IsHostVisible
            | View(m,_,_) -> m.Memory.IsHostVisible
            | Managed(m,_,_) -> m.Memory.IsHostVisible

    member x.IsDeviceLocal =
        match pointer with
            | Null -> false
            | Real(m,_) -> m.Memory.IsDeviceLocal
            | View(m,_,_) -> m.Memory.IsDeviceLocal
            | Managed(m,_,_) -> m.Memory.IsDeviceLocal

    member internal x.Pointer
        with get() = pointer
        and set p = pointer <- p

    override x.Release() =
        let old = Interlocked.Exchange(&pointer, Unchecked.defaultof<_>)
        if Unchecked.notNull old then
            match old with
                | Real(memory, _) -> memory.Dispose()
                | Managed(manager, block, _) -> manager.FreeBlock block
                | Null -> ()
                | View _ -> ()

[<AutoOpen>]
module VkRawExt =
    module VkRaw =
        let vkBindImageMemoryPtr(device : VkDevice, img : VkImage, ptr : deviceptr) =
            let mem, off =
                match ptr.Pointer with
                    | Null -> VkDeviceMemory.Null, 0L
                    | Real(h, _) -> h.Handle, 0L
                    | View(h, o, _) -> h.Handle, o
                    | Managed(_,b,_) -> b.Memory.Handle, b.Offset

            VkRaw.vkBindImageMemory(device, img, mem, uint64 off)

        let vkBindBufferMemoryPtr(device : VkDevice, buffer : VkBuffer, ptr : deviceptr) =
            let mem, off =
                match ptr.Pointer with
                    | Null -> VkDeviceMemory.Null, 0L
                    | Real(h, _) -> h.Handle, 0L
                    | View(h, o, _) -> h.Handle, o
                    | Managed(_,b,_) -> b.Memory.Handle, b.Offset

            VkRaw.vkBindBufferMemory(device, buffer, mem, uint64 off)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DevicePtr =
        
    let memory (ptr : deviceptr) =
        match ptr.Pointer with
            | Null -> failf "cannot get device for null pointer"
            | Real(m,_) -> m.Memory
            | View(m,_,_) -> m.Memory
            | Managed(_,b,_) -> b.Memory.Memory

    let device (ptr : deviceptr) =
        match ptr.Pointer with
            | Null -> failf "cannot get device for null pointer"
            | Real(m,_) -> m.Memory.Device
            | View(m,_,_) -> m.Memory.Device
            | Managed(_,b,_) -> b.Memory.Memory.Device

    let offset (ptr : deviceptr) =
        match ptr.Pointer with
            | Null -> 0L
            | Real(_,_) -> 0L
            | View(_,o,_) -> o
            | Managed(_,b,_) -> b.Offset

    let size (ptr : deviceptr) =
        match ptr.Pointer with
            | Null -> 0L
            | Real(_,s) -> s
            | View(_,_,s) -> s
            | Managed(_,_,s) -> s


    let alloc (size : int64) (this : DeviceMemory)  =
        let mem = DeviceMem.alloc size this
        new deviceptr(Real(mem, size))

    let free (ptr : deviceptr) =
        ptr.Dispose()


    let sub (offset : int64) (size : int64) (ptr : deviceptr) =
        assert (size >= 0L)
        assert (offset >= 0L)

        match ptr.Pointer with
            | Null -> 
                failf "cannot create view for null deviceptr"

            | Real(h,realSize) -> 
                assert (offset + size <= realSize)
                new deviceptr(View(h, offset, size))

            | View(h,parentOff, parentSize) ->
                assert (offset + size <= parentSize)
                new deviceptr(View(h, parentOff + offset, size))

            | Managed(manager, block, parentSize) ->
                assert (offset + size <= parentSize)
                new deviceptr(View(block.Memory, block.Offset + offset, size))

    let skip (offset : int64) (ptr : deviceptr) =
        assert (offset >= 0L)

        match ptr.Pointer with
            | Null -> 
                failf "cannot create view for null deviceptr"

            | Real(h,realSize) -> 
                assert (offset <= realSize)
                new deviceptr(View(h, offset, realSize - offset))

            | View(h,parentOff, parentSize) ->
                assert (offset <= parentSize)
                new deviceptr(View(h, parentOff + offset, parentSize - offset))

            | Managed(manager, block, parentSize) ->
                assert (offset <= parentSize)
                new deviceptr(View(block.Memory, block.Offset + offset, parentSize - offset))

    let take (size : int64) (ptr : deviceptr) =
        assert (size >= 0L)

        match ptr.Pointer with
            | Null -> 
                failf "cannot create view for null deviceptr"

            | Real(h,realSize) -> 
                assert (size <= realSize)
                new deviceptr(View(h, 0L, size))

            | View(h,parentOff, parentSize) ->
                assert (size <= parentSize)
                new deviceptr(View(h, parentOff, size))

            | Managed(manager, block, parentSize) ->
                assert (size <= parentSize)
                new deviceptr(View(block.Memory, block.Offset, size))


    let map (ptr : deviceptr) (f : nativeint -> 'a) =
        let mem, off, size = 
            match ptr.Pointer with
                | Null -> failf "cannot map null pointer"
                | Real(mem, size) -> mem, 0L, size
                | View(mem, off, size) -> mem, off, size
                | Managed(man, block, size) -> block.Memory, block.Offset, size

        if not mem.Memory.IsHostVisible then
            failf "cannot map device-local memory: %A" ptr

        let mutable res = 0n
        VkRaw.vkMapMemory(mem.Memory.Device.Handle, mem.Handle, uint64 off, uint64 size, VkMemoryMapFlags.MinValue, &&res)
            |> check "vkMapMemory"

        try f res
        finally VkRaw.vkUnmapMemory(mem.Memory.Device.Handle, mem.Handle)

    let copy (source : deviceptr) (target : deviceptr) (size : int64) =
        Command.custom (fun s ->
            let device = device source

            let createBuffer (ptr : deviceptr) (size : int64) (source : bool) =
                let mem, off, size =
                    match ptr.Pointer with
                        | Null -> failf "cannot get memory for null deviceptr"
                        | Real(m,s) -> m, 0L, s
                        | View(m,o,s) -> m, o, s
                        | Managed(_,b,s) -> b.Memory, b.Offset, s

                let align = device.Physical.Properties.limits.minUniformBufferOffsetAlignment
                
                let m = align - 1UL |> int64
                let offset = off &&& ~~~m
                let add = off - offset

                let mutable info =
                    VkBufferCreateInfo(
                        VkStructureType.BufferCreateInfo,
                        0n,
                        VkBufferCreateFlags.None,
                        uint64 (size + add),
                        (if source then VkBufferUsageFlags.TransferSrcBit else VkBufferUsageFlags.TransferDstBit),
                        VkSharingMode.Exclusive,
                        0u, NativePtr.zero
                    )
                let mutable buffer = VkBuffer.Null
                VkRaw.vkCreateBuffer(device.Handle, &&info, NativePtr.zero, &&buffer)
                    |> check "vkCreateBuffer"

                VkRaw.vkBindBufferMemory(device.Handle, buffer, mem.Handle, uint64 offset)
                    |> check "vkBindBufferMemory"

                buffer, add

            let cmd = s.buffer

            let src, srcOff = createBuffer source size true
            let dst, dstOff = createBuffer target size false

            let mutable copy =
                VkBufferCopy(uint64 srcOff, uint64 dstOff, uint64 size)

            VkRaw.vkCmdCopyBuffer(
                cmd.Handle,
                src, dst, 1u, &&copy
            )



            let cleanup() = 
                VkRaw.vkDestroyBuffer(device.Handle, src, NativePtr.zero)
                VkRaw.vkDestroyBuffer(device.Handle, dst, NativePtr.zero)

            { s with cleanupActions = cleanup::s.cleanupActions; isEmpty = false }
        )


    let uploadPtr (source : nativeint) (target : deviceptr) (size : int64)  =
        Command.custom (fun s ->
            let mutable s = s

            let mem = memory target
            if mem.IsHostVisible then
                map target (fun t -> Marshal.Copy(source, t, int size))
                s
            else
                let device = mem.Device
                let temp = alloc size device.HostVisibleMemory
                map temp (fun t -> Marshal.Copy(source, t, int size))
                (copy temp target size).Run(&s)

                { s with cleanupActions = (fun () -> temp.Dispose())::s.cleanupActions }

        )

    let uploadPinned (source : obj) (offset : int64) (target : deviceptr) (size : int64)  =
        Command.custom (fun s ->
            let mutable s = s
            let gc = GCHandle.Alloc(source, GCHandleType.Pinned)
            (uploadPtr (nativeint offset + gc.AddrOfPinnedObject()) target size).Run(&s)
            gc.Free()
            s
        )

    let uploadRange (data : 'a[]) (start : int) (count : int) (target : deviceptr) =
        let off = sizeof<'a> * start |> int64
        let size = sizeof<'a> * count |> int64
        uploadPinned data off target size
     
    let upload (data : 'a[]) (target : deviceptr) =
        uploadRange data 0 data.Length target
 
    let write (value : 'a) (target : deviceptr) =
        uploadPinned value 0L target (int64 sizeof<'a>)


    let downloadPtr (source : deviceptr) (target : nativeint) (size : int64) =
        Command.custom (fun s ->
            let mutable s = s

            let mem = memory source
            if mem.IsHostVisible then
                let cleanup() =
                    map source (fun s -> Marshal.Copy(s, target, int size))
                { s with cleanupActions = cleanup::s.cleanupActions }
            else
                let device = mem.Device
                let temp = alloc size device.HostVisibleMemory
                (copy source temp size).Run(&s)
                
                let cleanup() =
                    map temp (fun s -> Marshal.Copy(s, target, int size))
                    temp.Dispose()

                { s with cleanupActions = cleanup::s.cleanupActions }

        )

    let downloadPinned (source : deviceptr) (target : obj) (offset : int64) (size : int64) =
        Command.custom (fun s ->
            let mutable s = s
            let gc = GCHandle.Alloc(target, GCHandleType.Pinned)
            (downloadPtr source (nativeint offset + gc.AddrOfPinnedObject()) size).Run(&s)
            let cleanup() = gc.Free()
            { s with cleanupActions = cleanup::s.cleanupActions }
        ) 

    let downloadRange (source : deviceptr) (target : 'a[]) (start : int) (count : int) =
        let off = sizeof<'a> * start |> int64
        let size = sizeof<'a> * count |> int64
        downloadPinned source target off size

    let download (source : deviceptr) (target : 'a[]) =
        downloadRange source target 0 target.Length

    let read<'a> (source : deviceptr) : Command<'a> =
        command {
            let arr = Array.zeroCreate 1
            do! download source arr

            return! fun () -> 
                arr.[0]
        }


[<AbstractClass; Sealed; Extension>]
type DevicePtrExtensions private() =
    
    [<Extension>]
    static member CopyTo(source : deviceptr, target : deviceptr, size : int64) =
        DevicePtr.copy source target size

    [<Extension>]
    static member CopyTo(source : deviceptr, target : deviceptr) =
        let size = min (DevicePtr.size source) (DevicePtr.size target)
        DevicePtr.copy source target size


    [<Extension>]
    static member Upload(this : deviceptr, data : nativeint, size : int64) =
        DevicePtr.uploadPtr data this size
   
    [<Extension>]
    static member Upload(this : deviceptr, data : 'a[]) =
        DevicePtr.upload data this

    [<Extension>]
    static member Upload(this : deviceptr, arr : Array, start : int, count : int) =
        let et = arr.GetType().GetElementType()
        let es = Marshal.SizeOf et 
        let off = es * start |> int64
        let size = es * count |> int64
        DevicePtr.uploadPinned arr off this size


    [<Extension>]
    static member Upload(this : deviceptr, data : 'a[], start : int, count : int) =
        DevicePtr.uploadRange data start count this

    [<Extension>]
    static member Write(this : deviceptr, value : 'a) =
        DevicePtr.write value this

    [<Extension>]
    static member Write(this : deviceptr, value : 'a, offset : int64, size : int64) =
        DevicePtr.uploadPinned value offset this size


    [<Extension>]
    static member Download(this : deviceptr, target : nativeint, size : int64) =
        DevicePtr.downloadPtr this target size

    [<Extension>]
    static member Download(this : deviceptr, target : 'a[]) =
        DevicePtr.download this target

    [<Extension>]
    static member Download(this : deviceptr, target : 'a[], start : int, count : int) =
        DevicePtr.downloadRange this target start count

    [<Extension>]
    static member Download(this : deviceptr, arr : Array, start : int, count : int) =
        let et = arr.GetType().GetElementType()
        let es = Marshal.SizeOf et 
        let off = es * start |> int64
        let size = es * count |> int64
        DevicePtr.downloadPinned this arr off size

    [<Extension>]
    static member Read<'a>(this : deviceptr) : Command<'a> =
        DevicePtr.read<'a>(this)

    [<Extension>]
    static member Download(this : deviceptr, count : int) : Command<'a[]> =
        command {
            //let count = (DevicePtr.size this) / int64 sizeof<'a> |> int
            let arr = Array.zeroCreate count
            do! DevicePtr.download this arr
            return! fun () -> arr
        }

    [<Extension>]
    static member Download(this : deviceptr) : Command<'a[]> =
        command {
            let count = (DevicePtr.size this) / int64 sizeof<'a> |> int
            let arr = Array.zeroCreate count
            do! DevicePtr.download this arr
            return! fun () -> arr
        }

[<AbstractClass; Sealed; Extension>]
type MemoryExtensions private() =

    [<Extension>]
    static member Alloc(deviceMem : DeviceMemory, size : int64) =
        DevicePtr.alloc size deviceMem

    [<Extension>]
    static member Sub(ptr : deviceptr, offset : int64, size : int64) =
        DevicePtr.sub offset size ptr

    [<Extension>]
    static member Skip(ptr : deviceptr, offset : int64) =
        DevicePtr.skip offset ptr

    [<Extension>]
    static member Take(ptr : deviceptr, size : int64) =
        DevicePtr.take size ptr

    [<Extension>]
    static member Map(ptr : deviceptr, action : Action<nativeint>) =
        DevicePtr.map ptr action.Invoke

    [<Extension>]
    static member CopyTo(source : deviceptr, target : deviceptr, size : int64) =
        DevicePtr.copy source target size

        
    [<Extension>]
    static member Alloc(manager : IMemoryManager, size : int64) =
        match manager.TryAllocBlock(size) with
            | Some b -> 
                new deviceptr(Managed(manager, b, size))

            | _ ->
                let mem = DeviceMem.alloc size manager.Memory
                new deviceptr(Real(mem, size))


module MemoryManager =
    [<AutoOpen>]
    module private Implementation =
        type FixedCapacityManager(mem : DeviceMemory, capacity : int64) =
            let mutable storage = devicemem.Null //mem.Alloc(capacity)

            let free = FreeList<int64, ManagedBlock>()
            let mutable first = null
            let mutable last = null

            let createStorage() =
                if storage.IsNull then 
                    storage <- mem |> DeviceMem.alloc capacity
                    first <- ManagedBlock(storage, 0L, capacity, null, null)
                    last <- first
                    free.Insert(capacity, first)

            let destroyStorage() =
                if not storage.IsNull then 
                    storage.Dispose()
                    storage <- devicemem.Null
                    first <- null
                    last <- null
                    free.Clear()
            
        
            member x.Storage = storage
            member x.Memory = mem

            member x.TryAlloc(size : int64) =
                createStorage()
                match free.TryGetGreaterOrEqual size with
                    | Some block ->

                        if block.Size > size then
                            let rest = ManagedBlock(block.Memory, block.Offset + size, block.Size - size, block, block.Next)
                            free.Insert(rest.Size, rest)

                            if isNull block.Next then last <- rest
                            else block.Next.Prev <- rest
                            block.Next <- rest

                            block.Size <- size

                        block.Tag <- x
                        block.IsFree <- false
                        Some block

                    | None -> 
                        None

            member x.Free(b : ManagedBlock) =
        
                let final = ManagedBlock(b.Memory, b.Offset, b.Size, b.Prev, b.Next)

                if not (isNull b.Prev) && b.Prev.IsFree then
                    let prev = b.Prev
                    final.Offset <-prev.Offset
                    final.Size <- final.Size + prev.Size
                    free.Remove(prev.Size, prev) |> ignore
                    final.Prev <- prev.Prev

                if not (isNull b.Next) && b.Next.IsFree then
                    let next = b.Next
                    final.Size <- final.Size + next.Size
                    free.Remove(next.Size, next) |> ignore
                    final.Next <- next.Next

                if isNull final.Next then last <- final
                else final.Next.Prev <- final

                if isNull final.Prev then first <- final
                else final.Prev.Next <- final

                b.IsFree <- true
                free.Insert(final.Size, final)

                if first = last && last.IsFree then 
                    destroyStorage()


            member x.Dispose() = storage.Dispose()

            interface IDisposable with
                member x.Dispose() = x.Dispose()

        type CategoryMemoryManager(mem : DeviceMemory, blockCapacity : int64) =
            let l = obj()

            let mutable managers : list<FixedCapacityManager> = []

            let rec tryAlloc (size : int64) (m : list<FixedCapacityManager>) =
                match m with
                    | [] -> None
                    | h::rest ->
                        match h.TryAlloc size with
                            | Some b -> Some (h,b)
                            | _ -> tryAlloc size rest

            member x.Alloc(size : int64) =
                lock l (fun () ->
                    match tryAlloc size managers with
                        | Some (man, block) -> block
                        | None ->
                            let man = new FixedCapacityManager(mem, blockCapacity)
                            managers <- man::managers
                            let block = man.TryAlloc size |> Option.get
                            block
                )

            member x.Free(ptr : ManagedBlock) =
                let man = ptr.Tag |> unbox<FixedCapacityManager>
                lock l (fun () ->
                    ptr.Tag <- null
                    man.Free(ptr)
                )

            member x.Dispose() =
                let old = Interlocked.Exchange(&managers, [])
                old |> List.iter (fun m -> m.Dispose())

            interface IDisposable with
                member x.Dispose() = x.Dispose()

        type MemoryManager(mem : DeviceMemory, align : int64) =
            inherit Resource(mem.Device)

            let alignMask = align - 1L
            let categories = 
                Map.ofList [ 
                    1024L,      1048576L
                    1048576L,   67108864L
                ]

            let managers =
                categories 
                    |> Map.toSeq 
                    |> Seq.map (fun (thres, cap) -> thres, new CategoryMemoryManager(mem, cap))
                    |> Seq.toArray

            let find (size : int64) =
                let rec find (size : int64) (l : int) (r : int) =
                    if l > r then 
                        l
                    else
                        let m = (l + r) / 2
                        let (ms, mm) = managers.[m]

                        if ms > size then
                            find size l (r-1)
                        elif ms < size then
                            find size (l + 1) r
                        else
                            m
            
                let index = find size 0 (managers.Length-1)
                if index >= 0 && index < managers.Length then
                    let (cap, manager) = managers.[index]
                    if size > cap then None
                    else Some manager
                else
                    None

            member x.TryAllocBlock(size : int64) =
                let size = (size + alignMask) &&& ~~~alignMask
                match find size with
                    | Some man -> man.Alloc(size) |> Some
                    | _ -> None

            member x.FreeBlock(block : ManagedBlock) =
                if x.IsLive then
                    // actually free does not need the correct manager
                    let _, man = managers.[0]
                    man.Free(block)

            override x.Release() =
                managers |> Array.iter (fun (_,m) ->
                    m.Dispose()
                )

            interface IMemoryManager with
                member x.Memory = mem
                member x.TryAllocBlock s = x.TryAllocBlock s
                member x.FreeBlock p = x.FreeBlock p
   
    let aligned (align : int64) (mem : DeviceMemory) =
        new MemoryManager(mem, align) :> IMemoryManager

    let create (mem : DeviceMemory) = aligned 1L mem