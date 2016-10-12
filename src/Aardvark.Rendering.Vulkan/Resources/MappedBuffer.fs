namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Threading
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering.Vulkan


type MappedBufferOld(ctx : Context) =
    inherit Mod.AbstractMod<IBuffer>()
    static let flags = VkBufferUsageFlags.VertexBufferBit ||| VkBufferUsageFlags.TransferDstBit
    let device = ctx.Device


    let createBuffer(size : int) =
        let mutable info =
            VkBufferCreateInfo(
                VkStructureType.BufferCreateInfo, 0n,
                VkBufferCreateFlags.None, 
                uint64 size,
                flags,
                VkSharingMode.Exclusive,
                0u, NativePtr.zero
            )

        let mutable buffer = VkBuffer.Null
        VkRaw.vkCreateBuffer(device.Handle, &&info, NativePtr.zero, &&buffer)
            |> check "vkCreateBuffer"

        let mutable reqs = VkMemoryRequirements()
        VkRaw.vkGetBufferMemoryRequirements(device.Handle, buffer, &&reqs)

        let mem = device.HostVisibleMemory.Alloc(int64 reqs.size)
        VkRaw.vkBindBufferMemoryPtr(device.Handle, buffer, mem)
            |> check "vkBindBufferMemory"

        match mem.Pointer with
            | Real(m, _) -> m.Handle, Buffer(ctx, buffer, VkFormat.Undefined, mem, flags)
            | _ -> failf "asdasdsadasd"

    let releaseBuffer(b : Buffer) =
        if b.Handle.IsValid then
            b.Memory.Dispose()
            VkRaw.vkDestroyBuffer(device.Handle, b.Handle, NativePtr.zero)

    let map(b : Buffer) =
        let mutable ptr = 0n
        match b.Memory.Pointer with
            | Null -> ()

            | Real(m,s) -> 
                VkRaw.vkMapMemory(device.Handle, m.Handle, 0UL, uint64 s, VkMemoryMapFlags.MinValue, &&ptr)
                    |> check "vkMapMemory"

            | View(m,o,s) ->
                VkRaw.vkMapMemory(device.Handle, m.Handle, uint64 o, uint64 s, VkMemoryMapFlags.MinValue, &&ptr)
                    |> check "vkMapMemory"

            | Managed(_,b,s) ->
                VkRaw.vkMapMemory(device.Handle, b.Memory.Handle, uint64 b.Offset, uint64 s, VkMemoryMapFlags.MinValue, &&ptr)
                    |> check "vkMapMemory"

        ptr

    let unmap(b : Buffer) =
        if b.Handle.IsValid then
            match b.Memory.Pointer with
                | Null -> ()
                | Real(m,_) -> VkRaw.vkUnmapMemory(device.Handle, m.Handle)
                | View(m,_,_) -> VkRaw.vkUnmapMemory(device.Handle, m.Handle)
                | Managed(_,b,_) -> VkRaw.vkUnmapMemory(device.Handle, b.Memory.Handle)


    let onDispose = new System.Reactive.Subjects.Subject<unit>()
    let mutable pointer = 0n
    let mutable memory = VkDeviceMemory.Null
    let mutable handle : Option<Buffer> = None
    let mutable capacity = 0

    let deleteBuffers = System.Collections.Generic.List<Buffer>()

    let create () =
        match handle with
            | Some h when h.Size = int64 capacity ->
                ()

            | Some h -> 
                let copySize = min (int h.Size) capacity
                let mem, newBuffer = createBuffer capacity
                let newPointer = map newBuffer

                unmap h
                if copySize > 0 then
                    h.CopyTo(newBuffer, 0L, int64 copySize) |> ctx.DefaultQueue.RunSynchronously

                memory <- mem
                handle <- Some newBuffer
                pointer <- newPointer
                deleteBuffers.Add h

            | None ->
                let mem, newBuffer = createBuffer capacity 
                memory <- mem
                handle <- Some newBuffer
                pointer <- map newBuffer

    member x.Dispose() =
        match handle with
            | Some h ->
                onDispose.OnNext(())
                unmap h
                releaseBuffer h
                handle <- None
                memory <- VkDeviceMemory.Null
                pointer <- 0n
                capacity <- 0
            | _ ->
                ()

    member x.Capacity = capacity         
    
    member x.Resize(size : int) =
        let o = Interlocked.Exchange(&capacity, size)
        if o <> size then
            create()
            transact (fun () -> x.MarkOutdated())
        
    member x.Write(ptr : nativeint, offset : int, sizeInBytes : int) =
        if sizeInBytes > 0 then 
            create()
            Marshal.Copy(ptr, pointer + nativeint offset, sizeInBytes)
            let mutable range = 
                VkMappedMemoryRange(
                    VkStructureType.MappedMemoryRange, 0n,
                    memory,
                    uint64 offset,
                    uint64 sizeInBytes
                )

            VkRaw.vkFlushMappedMemoryRanges(device.Handle, 1u, &&range)
                |> check "vkFlushMappedMemoryRanges"

    member x.Read(ptr : nativeint, offset : int, sizeInBytes : int) =
        create()
        Marshal.Copy(pointer + nativeint offset, ptr, sizeInBytes)


    member x.OnDispose = onDispose :> IObservable<_>

    interface ILockedResource with
        member x.Use _ = failwith "not implemented"
        member x.AddLock _ = failwith "not implemented"
        member x.RemoveLock _ = failwith "not implemented"

    interface IMappedBuffer with
        member x.Dispose() = x.Dispose()
        member x.Write(ptr, off, size) = x.Write(ptr, int off, int size)
        member x.Read(ptr, off, size) = x.Read(ptr, int off, int size)
        member x.Resize(size) = x.Resize(int size)
        member x.Capacity = nativeint x.Capacity
        member x.OnDispose = x.OnDispose
        member x.UseRead(_,_,_) = failwith "not implemented"
        member x.UseWrite(_,_,_) = failwith "not implemented"

    override x.Compute() =
        create()

        for r in deleteBuffers do
            releaseBuffer r

        deleteBuffers.Clear()

        handle.Value :> IBuffer


type MappedBuffer(ctx : Context) =
    inherit Mod.AbstractMod<IBuffer>()

    let mutable capacity = 0
    //let rw = new ReaderWriterLockSlim()

    let mutable disp = []
    let mutable buffer = ctx.CreateBuffer(0L, VkBufferUsageFlags.VertexBufferBit)
    

    let onDispose = new System.Reactive.Subjects.Subject<unit>()

    member private x.Realloc() = 
        let cap = int64 capacity
        if cap <> buffer.Size then
            let newBuffer = ctx.CreateBuffer(cap, VkBufferUsageFlags.VertexBufferBit)
            buffer.CopyTo(newBuffer, 0L, min buffer.Size newBuffer.Size) |> ctx.DefaultQueue.RunSynchronously
            transact (fun () -> x.MarkOutdated())
            disp <- buffer::disp
            buffer <- newBuffer


    member x.Capacity = capacity

    member x.Resize (size : int) =
        capacity <- size
        //ReaderWriterLock.write rw (fun () ->
        x.Realloc()
        //)
            
    member x.Write(data : nativeint, offset : int, size : int) =
        assert(size >= 0 && offset >= 0 && offset + size <= capacity)
        //ReaderWriterLock.read rw (fun () ->
        buffer.Upload(int64 offset, data, int64 size)
            |> ctx.DefaultQueue.RunSynchronously
        //)

    member x.Read(data : nativeint, offset : int, size : int) =
        assert(size >= 0 && offset >= 0 && offset + size <= capacity)
        //ReaderWriterLock.read rw (fun () ->
        buffer.Download(int64 offset, data, int64 size)
            |> ctx.DefaultQueue.RunSynchronously
        //)

    member x.OnDispose = onDispose :> IObservable<_>

    member x.Dispose() =
        let old = Interlocked.Exchange(&disp, [])
        for o in old do ctx.Delete o
        capacity <- 0
        ctx.Delete buffer
        buffer <- ctx.CreateBuffer(0L, VkBufferUsageFlags.VertexBufferBit)

    override x.Compute() =
        let old = Interlocked.Exchange(&disp, [])
        for o in old do ctx.Delete o

        buffer :> IBuffer

    interface ILockedResource with
        member x.Use _ = failwith "not implemented"
        member x.AddLock _ = failwith "not implemented"
        member x.RemoveLock _ = failwith "not implemented"

    interface IMappedBuffer with
        member x.Dispose() = x.Dispose()
        member x.Write(ptr, off, size) = x.Write(ptr, int off, int size)
        member x.Read(ptr, off, size) = x.Read(ptr, int off, int size)
        member x.Resize(size) = x.Resize(int size)
        member x.Capacity = nativeint x.Capacity
        member x.OnDispose = x.OnDispose
        member x.UseRead(_,_,_) = failwith "not implemented"
        member x.UseWrite(_,_,_) = failwith "not implemented"
