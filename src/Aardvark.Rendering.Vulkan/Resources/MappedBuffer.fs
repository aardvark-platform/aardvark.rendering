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


type MappedBuffer(ctx : Context) =
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

    let create () =
        if capacity = 0 then
            match handle with
                | Some h -> 
                    unmap h
                    pointer <- 0n
                    releaseBuffer h
                    handle <- None
                | _ -> 
                    ()
        else
            match handle with
                | Some h when h.Size = int64 capacity ->
                    ()

                | Some h -> 
                    unmap h
                    let mem, newBuffer = createBuffer capacity
                    h.CopyTo(newBuffer, 0L, min h.Size newBuffer.Size) |> ctx.DefaultQueue.RunSynchronously
                    memory <- mem
                    handle <- Some newBuffer
                    pointer <- map newBuffer
                    releaseBuffer h

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
            transact (fun () -> x.MarkOutdated())
             
    member x.Write(ptr : nativeint, offset : int, sizeInBytes : int) =
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

    interface IMappedBuffer with
        member x.Dispose() = x.Dispose()
        member x.Write(ptr, off, size) = x.Write(ptr, off, size)
        member x.Read(ptr, off, size) = x.Read(ptr, off, size)
        member x.Resize(size) = x.Resize(size)
        member x.Capacity = x.Capacity
        member x.OnDispose = x.OnDispose

    override x.Compute() =
        create()
        handle.Value :> IBuffer

