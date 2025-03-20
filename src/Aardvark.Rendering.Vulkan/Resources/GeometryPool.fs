namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Management
open FSharp.Data.Adaptive
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

module GeometryPoolUtilities =

    [<AbstractClass>]
    type ResourceLock2() =
        let lock = ResourceLock()

        member x.Lock = lock
        abstract member OnLock : Option<ResourceUsage> -> unit
        abstract member OnUnlock : Option<ResourceUsage> -> unit


    type MappedBufferOld(device : Device, lock : ResourceLock2, usage : VkBufferUsageFlags, handle : VkBuffer, devPtr : DevicePtr, size : int64) =
        inherit Buffer(device, handle, devPtr, size, usage)
        static let sRange = sizeof<VkMappedMemoryRange> |> nativeint

        let transfer = device.TransferFamily
       
        
        let mutable ptr, hm, hostBuffer =
            native {
                let! pHandle = VkBuffer.Null
                let! pInfo =
                    VkBufferCreateInfo(
                        VkBufferCreateFlags.None,
                        uint64 size, 
                        VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit,
                        device.SharingMode,
                        device.QueueFamilyCount, device.QueueFamilyIndices
                    )
                VkRaw.vkCreateBuffer(device.Handle, pInfo, NativePtr.zero, pHandle)
                    |> check "could not create buffer"

                let! pReqs = VkMemoryRequirements()
                VkRaw.vkGetBufferMemoryRequirements(device.Handle, handle, pReqs)
                let reqs = pReqs.Value
                let hm = device.HostMemory.AllocRaw(int64 reqs.size)

                VkRaw.vkBindBufferMemory(device.Handle, handle, hm.Handle, 0UL)
                    |> check "could not bind host memory"

                let! pPtr = 0n
                VkRaw.vkMapMemory(device.Handle, hm.Handle, 0UL, uint64 hm.Size, VkMemoryMapFlags.None, pPtr)
                    |> check "could not map memory"
                    
                return pPtr.Value, hm, new Buffer(device, handle, hm, size, VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit)
            }
        let mutable isEmpty = true

        let mutable dirty = RangeSet1l.empty

        member private x.HostBuffer = hostBuffer

        member x.Write(offset : int64, size : int64, data : nativeint) =
            LockedResource.access x (fun () ->
                isEmpty <- false
                assert (offset >= 0L && size >= 0L && offset + size <= hostBuffer.Size)
                Marshal.Copy(data, ptr + nativeint offset, size)

                let range = Range1l(offset, offset + size - 1L)
                Interlocked.Change(&dirty, RangeSet1l.add range) |> ignore
            )

        member x.Flush() =
            LockedResource.update x (fun () ->
                let dirty = Interlocked.Exchange(&dirty, RangeSet1l.empty)
                
                let cnt = dirty.Count
                if cnt <> 0 then
                    Log.warn "flush %d" cnt
                    let pRanges = NativePtr.alloc cnt
                    let ranges = Array.zeroCreate cnt
                    try
                        let mutable current = NativePtr.toNativeInt pRanges
                        let mutable i = 0
                        for r in dirty do
                            ranges.[i] <- r
                            let range =
                                VkMappedMemoryRange(
                                    hm.Handle,
                                    uint64 r.Min,
                                    uint64 (1L + r.Max - r.Min)
                                )

                            NativeInt.write current range
                            current <- current + sRange

                        VkRaw.vkFlushMappedMemoryRanges(device.Handle, uint32 cnt, pRanges)
                            |> check "could not flush mapped memory"

                        let copy =
                            command {
                                if not isEmpty then
                                    do! Command.Copy(hostBuffer, x, ranges)
                                    do! Command.Sync(x, VkPipelineStageFlags.TransferBit, VkAccessFlags.TransferWriteBit)
                            }

                        Some copy

                    finally
                        NativePtr.free pRanges
                else
                    None
            )

        member x.Realloc(newCapacity : int64, run : Command -> unit) =
            LockedResource.access x (fun () ->
                if x.Size <> newCapacity then
                    let copySize = min newCapacity x.Size
                    LockedResource.update x (fun () ->
                        let flush = x.Flush()

                        let newBuffer = 
                            let b = device.CreateBuffer(usage, newCapacity)
                            new MappedBufferOld(device, lock, usage, b.Handle, b.Memory, b.Size)

                        let update =
                            command {
                                match flush with
                                    | Some cmd -> do! cmd
                                    | None -> ()
                                    
                                if not isEmpty then
                                    do! Command.Copy(x, newBuffer, copySize)
                                    do! Command.Sync(newBuffer, VkPipelineStageFlags.TransferBit, VkAccessFlags.TransferWriteBit)
                            }

                        run update

                        newBuffer
                    )
                else
                    x
            )
  
        member x.Realloc(newCapacity : int64) =
            x.Realloc(newCapacity, transfer.RunSynchronously)

        override x.Destroy() =
            if hostBuffer.Handle.IsValid then
                //VkRaw.vkUnmapMemory(device.Handle, hm.Handle)
                hostBuffer.Dispose()
                ptr <- 0n
                base.Destroy()

        interface ILockedResource with
            member x.Lock = lock.Lock
            member x.OnLock c = lock.OnLock c
            member x.OnUnlock c = lock.OnUnlock c

    type StreamingBufferOld(device : Device, rlock : ResourceLock2, usage : VkBufferUsageFlags, handle : VkBuffer, devPtr : DevicePtr, size : int64) =
        inherit Buffer(device, handle, devPtr, size, usage)

        let streamSize = size

        let mutable scratchBuffer, scratchMem, scratchPtr =
            if size > 0L then
                native {
                    let! pBuffer = VkBuffer.Null
                    let! pInfo =
                        VkBufferCreateInfo(
                            VkBufferCreateFlags.None,
                            uint64 streamSize,
                            VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit,
                            device.SharingMode,
                            device.QueueFamilyCount,
                            device.QueueFamilyIndices
                        )

                    VkRaw.vkCreateBuffer(device.Handle, pInfo, NativePtr.zero, pBuffer)
                        |> check "could not create buffer"

                    let buffer = !!pBuffer
                    let! pReqs = VkMemoryRequirements()
                    VkRaw.vkGetBufferMemoryRequirements(device.Handle, buffer, pReqs)
                    let reqs = !!pReqs

                    let compatible = reqs.memoryTypeBits &&& (1u <<< device.HostMemory.Index) <> 0u
                    if not compatible then
                        failf "cannot create buffer with host visible memory"

                    let bufferMem = device.HostMemory.AllocRaw(int64 reqs.size)

                    VkRaw.vkBindBufferMemory(device.Handle, buffer, bufferMem.Handle, 0UL)
                        |> check "could not bind buffer memory"

                    let! pMemPtr = 0n
                    VkRaw.vkMapMemory(device.Handle, bufferMem.Handle, 0UL, uint64 bufferMem.Size, VkMemoryMapFlags.None, pMemPtr)
                        |> check "could not map memory"

                    return buffer, bufferMem, !!pMemPtr
                }
            else
                VkBuffer.Null, DeviceMemory.Null, 0n

        let todoLock = obj()

        let scratchManager = MemoryManager.createNop()


        let mutable todo : VkBufferCopy[] = Array.zeroCreate 16
        let mutable todoCount = 0
        let mutable scratchOffset = 0L

        let flush =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd : CommandBuffer) =
                    let todo, todoCount = 
                        lock todoLock (fun () ->
                            let mine = todo
                            let myCount = todoCount
                            todo <- Array.zeroCreate 16
                            todoCount <- 0
                            scratchOffset <- 0L
                            mine, myCount
                        )

                    if todoCount > 0 then
                        todo |> NativePtr.pinArr (fun ptr ->
                            cmd.AppendCommand()
                            VkRaw.vkCmdCopyBuffer(cmd.Handle, scratchBuffer, handle, uint32 todoCount, ptr)
                        )
            }

        override x.Destroy() =
            if scratchBuffer.IsValid then
                base.Destroy()
                VkRaw.vkDestroyBuffer(device.Handle, scratchBuffer, NativePtr.zero)
                scratchMem.Dispose()
                scratchBuffer <- VkBuffer.Null
                scratchPtr <- 0n
                todo <- null
                todoCount <- 0
                scratchOffset <- 0L

        member x.Flush = flush

        member x.Write(offset : int64, size : int64, data : nativeint) =
            rlock.Lock.Use(ResourceUsage.Access, fun () ->
                let scratchOffset = 
                    lock todoLock (fun () ->
                        let localOffset = scratchOffset
                        if localOffset + size > streamSize then
                            None
                        elif not (isNull todo) then
                            if todoCount >= todo.Length then
                                Array.Resize(&todo, todo.Length * 2)

                            todo.[todoCount] <- VkBufferCopy(uint64 localOffset, uint64 offset, uint64 size)
                            todoCount <- todoCount + 1
                            scratchOffset <- scratchOffset + size
                            Some localOffset
                        else
                            None
                    )


                match scratchOffset with
                    | None -> 
                        rlock.Lock.Use(fun () ->
                            device.perform { do! x.Flush }
                        )
                        x.Write(offset, size, data)

                    | Some scratchOffset ->
                        Marshal.Copy(data, scratchPtr + nativeint scratchOffset, size)
                        native {
                            let! pRegion =
                                VkMappedMemoryRange(
                                    scratchMem.Handle, 
                                    uint64 scratchOffset, uint64 size
                                )

                            VkRaw.vkFlushMappedMemoryRanges(device.Handle, 1u, pRegion)
                                |> check "could not flush range"
                        }

                        device.GraphicsFamily.RunSynchronously(
                            { new Command() with
                                member x.Compatible = QueueFlags.All
                                member x.Enqueue cmd =
                                    cmd.AppendCommand()
                                    let mutable copyInfo = VkBufferCopy(uint64 scratchOffset, uint64 offset, uint64 size)
                                    VkRaw.vkCmdCopyBuffer(cmd.Handle, scratchBuffer, handle, 1u, &&copyInfo)
                            }
                        )
            )

        interface ILockedResource with
            member x.Lock = rlock.Lock
            member x.OnLock c = rlock.OnLock c
            member x.OnUnlock c = rlock.OnUnlock c

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type StreamingBuffer(device : Device, rlock : ResourceLock2, usage : VkBufferUsageFlags, handle : VkBuffer, devPtr : DevicePtr, size : int64) =
        inherit Buffer(device, handle, devPtr, size, usage)
        let streamSize = size

        let mutable scratchBuffer, scratchMem =
            if size > 0L then
                native {
                    let! pBuffer = VkBuffer.Null
                    let! pInfo =
                        VkBufferCreateInfo(
                            VkBufferCreateFlags.None,
                            uint64 streamSize,
                            VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit,
                            device.SharingMode,
                            device.QueueFamilyCount,
                            device.QueueFamilyIndices
                        )

                    VkRaw.vkCreateBuffer(device.Handle, pInfo, NativePtr.zero, pBuffer)
                        |> check "could not create buffer"

                    let buffer = !!pBuffer
                    let! pReqs = VkMemoryRequirements()
                    VkRaw.vkGetBufferMemoryRequirements(device.Handle, buffer, pReqs)
                    let reqs = !!pReqs

                    let compatible = reqs.memoryTypeBits &&& (1u <<< device.HostMemory.Index) <> 0u
                    if not compatible then
                        failf "cannot create buffer with host visible memory"

                    let bufferMem = device.HostMemory.Alloc(int64 reqs.alignment, int64 reqs.size)

                    VkRaw.vkBindBufferMemory(device.Handle, buffer, bufferMem.Memory.Handle, uint64 bufferMem.Offset)
                        |> check "could not bind buffer memory"

                    return buffer, bufferMem
                }
            else
                VkBuffer.Null, DevicePtr.Null

        let mutable isEmpty = true

        override x.Destroy() =
            if scratchBuffer.IsValid then
                base.Destroy()
                VkRaw.vkDestroyBuffer(device.Handle, scratchBuffer, NativePtr.zero)
                scratchMem.Dispose()
                scratchBuffer <- VkBuffer.Null

        member x.Write(offset : int64, size : int64, data : nativeint) =
            rlock.Lock.Use(ResourceUsage.Access, fun () ->
                isEmpty <- false
                scratchMem.Mapped (fun scratchPtr ->
                    Marshal.Copy(data, scratchPtr + nativeint offset, size)
                )

                match device.UploadMode with
                    | UploadMode.Async ->
                        let tcs = new System.Threading.Tasks.TaskCompletionSource<unit>()
                        device.CopyEngine.Enqueue [
                            CopyCommand.Copy(scratchBuffer, offset, handle, offset, size)
                            CopyCommand.Callback(fun () -> tcs.SetResult())
                        ]
                        tcs.Task.Wait()

                    | _ ->
                        device.GraphicsFamily.RunSynchronously(
                            { new Command() with
                                member x.Compatible = QueueFlags.All
                                member x.Enqueue(cmd) =
                                    cmd.AppendCommand()
                                    let mutable copyInfo = VkBufferCopy(uint64 offset, uint64 offset, uint64 size)
                                    VkRaw.vkCmdCopyBuffer(cmd.Handle, scratchBuffer, handle, 1u, &&copyInfo)
                            }
                        )
            )

        member x.IsEmpty
            with get() = isEmpty
            and set e = isEmpty <- e

        interface ILockedResource with
            member x.Lock = rlock.Lock
            member x.OnLock c = rlock.OnLock c
            member x.OnUnlock c = rlock.OnUnlock c

    [<AbstractClass; Sealed; Extension>]
    type DeviceMappedBufferExts private() =

        [<Extension>]
        static member CreateMappedBuffer(device : Device, lock : ResourceLock2, usage : VkBufferUsageFlags, size : int64) =
            let usage = VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.TransferSrcBit ||| usage
            let b = device |> Buffer.alloc' true false 1UL usage size
            new MappedBufferOld(device, lock, usage, b.Handle, b.Memory, size)


        [<Extension>]
        static member CreateStreamingBuffer(device : Device, lock : ResourceLock2, usage : VkBufferUsageFlags, size : int64) =
            if size = 0L then
                new StreamingBuffer(device, lock, usage, VkBuffer.Null, DevicePtr.Null, size)
            else
                let usage = VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.TransferSrcBit ||| usage
                let b = device |> Buffer.alloc' true false 1UL usage size
                new StreamingBuffer(device, lock, usage, b.Handle, b.Memory, size)


    type GeometryPool(device : Device, types : Map<Symbol, Type>) as this =
        let manager = MemoryManager.createNop()
        let minCapacity = 1L <<< 10
        let mutable capacity = minCapacity
        let mutable count = 0


        static let usage = VkBufferUsageFlags.VertexBufferBit ||| VkBufferUsageFlags.TransferDstBit
        let lock = 
            { new ResourceLock2() with
                member x.OnLock c = this.onLock c
                member x.OnUnlock c = this.onUnlock c
            }

        let buffers =
            types |> Map.map (fun sem t ->
                let elemSize = Marshal.SizeOf t |> int64

                let s = capacity * elemSize
                let handle = device.CreateStreamingBuffer(lock, usage, s)
            
                elemSize, t, AVal.init (handle :> IBuffer)
            )

        let views =
            buffers |> Map.map (fun _ (_,t,b) ->
                Aardvark.Rendering.BufferView(b, t)
            )

        let vertexSize = types |> Map.toSeq |> Seq.sumBy (fun (_,t) -> Marshal.SizeOf t |> int64)

        let reallocIfNeeded () =
            let newCapacity = manager.Capactiy + 1n |> int64 |> Fun.NextPowerOfTwo |> max minCapacity
            if newCapacity <> capacity then
                let result = 
                    lock.Lock.Use(fun () ->
                        let newCapacity = manager.Capactiy + 1n |> int64 |> Fun.NextPowerOfTwo |> max minCapacity
                        if capacity <> newCapacity then
                            Log.line "realloc %A -> %A" (Mem (vertexSize * capacity)) (Mem (vertexSize * newCapacity))
                            let copySize = min capacity newCapacity
                            let t = new Transaction()
                            let commands = List<Command>()
                            let deleteBuffers = List<StreamingBuffer>()
                            
                            use token = device.Token
                            for (_, (elemSize,_,b)) in Map.toSeq buffers do
                                let old = b.Value |> unbox<StreamingBuffer>
                                let n = device.CreateStreamingBuffer(lock, usage, elemSize * newCapacity)

                                if copySize > 0L && not old.IsEmpty then
                                    let copy = Command.Copy(old, n, elemSize * copySize)
                                    token.Enqueue(copy)
                                    n.IsEmpty <- false

                                Operators.lock b (fun () ->
                                    let wasOutdated = b.OutOfDate
                                    b.OutOfDate <- true
                                    b.Value <- n
                                    b.OutOfDate <- wasOutdated
                                )
                                t.Enqueue(b)
                                deleteBuffers.Add old

                            token.Flush()
                            capacity <- newCapacity

                            fun () ->
                                t.Commit()
                                for d in deleteBuffers do d.Dispose()
                                t.Dispose()
                        else
                            id
                    )

                result()

        member private x.onLock (c : Option<ResourceUsage>) =
            match c with
                | Some ResourceUsage.Render ->
                    ()
//                    use token = device.Token
//                    let update = 
//                        command {
//                            for (_, (_,_,b)) in Map.toSeq buffers do
//                                let b = b.Value |> unbox<StreamingBuffer>
//                                //do! b.Flush
//                                //do! Command.SyncWrite b
//                        }
//
//                    token.Enqueue update
                | _ -> 

                    ()

        member private x.onUnlock (c : Option<ResourceUsage>) =
            ()

        member x.Alloc(fvc : int, geometry : IndexedGeometry) =
            let ptr = manager.Alloc(nativeint fvc)
            reallocIfNeeded()

//            use t = device.Token
//            try
            lock.Lock.Use(ResourceUsage.Access, fun () -> 

                for (sem, (elemSize, elemType, buffer)) in Map.toSeq buffers do
                    let buffer = buffer.Value |> unbox<StreamingBuffer>
                    let offset = elemSize * int64 ptr.Offset
                    let size = elemSize * int64 fvc

                    if size > 0L then
                        match geometry.IndexedAttributes.TryGetValue sem with
                            | (true, arr) ->
                                assert(arr.GetType().GetElementType() = elemType)
                                let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
                                try buffer.Write(offset, size, gc.AddrOfPinnedObject())
                                finally gc.Free()

                                //t.Enqueue buffer.Flush

                            | _ ->
                                ()

                Interlocked.Increment(&count) |> ignore
                ptr
            )
//            finally 
//                t.Sync()

        member x.Free(ptr : Block<unit>) =
            manager.Free ptr
            reallocIfNeeded()
            Interlocked.Decrement(&count) |> ignore

        member x.UsedMemory =
            Mem (vertexSize * capacity)

        member x.Count = count

        member x.TryGetBufferView(sem : Symbol) =
            Map.tryFind sem views

        member x.Dispose() =
            buffers |> Map.iter (fun _ (_,_,b) ->
                let b = unbox<StreamingBuffer> b.Value
                b.Dispose()
                manager.Dispose()
                capacity <- 0L
                count <- 0
            )

        interface IGeometryPool with
            member x.Dispose() = x.Dispose()
            member x.Alloc(fvc, g) = x.Alloc(fvc, g)
            member x.Free(a) = x.Free(a)
            member x.TryGetBufferView(sem) = x.TryGetBufferView(sem)
            member x.UsedMemory = x.UsedMemory
            member x.Count = x.Count
        




