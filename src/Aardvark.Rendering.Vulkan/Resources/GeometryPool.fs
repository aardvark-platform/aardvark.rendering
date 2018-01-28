namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open Management

#nowarn "9"
#nowarn "51"

module GeometryPoolUtilities =

    type HalfRangeKind =
        | Left = 0
        | Right = 1

    [<StructuredFormatDisplay("{AsString}")>]
    type RangeSet(store : MapExt<int64, HalfRangeKind>) =
        static let empty = RangeSet(MapExt.empty)

        static member Empty = empty

        member private x.store = store

        static member OfSeq(s : seq<Range1l>) =
            let arr = s |> Seq.toArray
            if arr.Length = 0 then
                empty
            elif arr.Length = 1 then
                let r = arr.[0]
                RangeSet(MapExt.ofList [r.Min, HalfRangeKind.Left; r.Max + 1L, HalfRangeKind.Right ])
            else
                // TODO: better impl possible (sort array and traverse)
                arr |> Array.fold (fun s r -> s.Add r) empty

        member x.Add(r : Range1l) =

            if r.Max < r.Min then 
                x 
            else
                let min = r.Min
                let max = r.Max + 1L

                let lm, _, inner = MapExt.split min store
                let inner, _, rm = MapExt.split max inner

                let before = MapExt.tryMax lm |> Option.map (fun mk -> mk, lm.[mk])
                let after = MapExt.tryMin rm |> Option.map (fun mk -> mk, rm.[mk])

                let newStore = 
                    match before, after with
                        | None, None ->
                            MapExt.ofList [ min, HalfRangeKind.Left; max, HalfRangeKind.Right]

                        | Some(bk, HalfRangeKind.Right), None ->
                            lm 
                            |> MapExt.add min HalfRangeKind.Left
                            |> MapExt.add max HalfRangeKind.Right

                        | Some(bk, HalfRangeKind.Left), None ->
                            lm 
                            |> MapExt.add max HalfRangeKind.Right

                        | None, Some(ak, HalfRangeKind.Left) ->
                            rm
                            |> MapExt.add min HalfRangeKind.Left
                            |> MapExt.add max HalfRangeKind.Right

                        | None, Some(ak, HalfRangeKind.Right) ->
                            rm
                            |> MapExt.add min HalfRangeKind.Left

                        | Some(bk, HalfRangeKind.Right), Some(ak, HalfRangeKind.Left) ->
                            let self = MapExt.ofList [ min, HalfRangeKind.Left; max, HalfRangeKind.Right]
                            MapExt.union (MapExt.union lm self) rm
                        
                        | Some(bk, HalfRangeKind.Left), Some(ak, HalfRangeKind.Left) ->
                            let self = MapExt.ofList [ max, HalfRangeKind.Right]
                            MapExt.union (MapExt.union lm self) rm

                        | Some(bk, HalfRangeKind.Right), Some(ak, HalfRangeKind.Right) ->
                            let self = MapExt.ofList [ min, HalfRangeKind.Left ]
                            MapExt.union (MapExt.union lm self) rm

                        | Some(bk, HalfRangeKind.Left), Some(ak, HalfRangeKind.Right) ->
                            MapExt.union lm rm

                        | _ ->
                            failwithf "impossible"
                assert (newStore.Count % 2 = 0)
                RangeSet(newStore)

        member x.Remove(r : Range1l) =
            if r.Max < r.Min then
                x
            else
                let min = r.Min
                let max = r.Max + 1L

                let lm, _, inner = MapExt.split min store
                let inner, _, rm = MapExt.split max inner

                let before = MapExt.tryMax lm |> Option.map (fun mk -> mk, lm.[mk])
                let after = MapExt.tryMin rm |> Option.map (fun mk -> mk, rm.[mk])

                let newStore = 
                    match before, after with
                        | None, None ->
                            MapExt.empty

                        | Some(bk, HalfRangeKind.Right), None ->
                            lm

                        | Some(bk, HalfRangeKind.Left), None ->
                            lm 
                            |> MapExt.add min HalfRangeKind.Right

                        | None, Some(ak, HalfRangeKind.Left) ->
                            rm

                        | None, Some(ak, HalfRangeKind.Right) ->
                            rm
                            |> MapExt.add max HalfRangeKind.Left

                        | Some(bk, HalfRangeKind.Right), Some(ak, HalfRangeKind.Left) ->
                            MapExt.union lm rm
                        
                        | Some(bk, HalfRangeKind.Left), Some(ak, HalfRangeKind.Left) ->
                            let self = MapExt.ofList [ min, HalfRangeKind.Right]
                            MapExt.union (MapExt.union lm self) rm

                        | Some(bk, HalfRangeKind.Right), Some(ak, HalfRangeKind.Right) ->
                            let self = MapExt.ofList [ max, HalfRangeKind.Left ]
                            MapExt.union (MapExt.union lm self) rm

                        | Some(bk, HalfRangeKind.Left), Some(ak, HalfRangeKind.Right) ->
                            let self = MapExt.ofList [ min, HalfRangeKind.Right; max, HalfRangeKind.Left]
                            MapExt.union (MapExt.union lm self) rm

                        | _ ->
                            failwithf "impossible"

                RangeSet(newStore)

        member x.Contains(v : int64) =
            let l, s, _ = MapExt.neighbours v store
            match s with
                | Some(_,k) -> 
                    k = HalfRangeKind.Left
                | _ ->
                    match l with
                        | Some(_,HalfRangeKind.Left) -> true
                        | _ -> false

        member x.Count = 
            assert (store.Count &&& 1 = 0)
            store.Count / 2

        member private x.AsString = x.ToString()

        member x.ToArray() =
            let arr = Array.zeroCreate (store.Count / 2)
            let rec write (i : int) (l : list<int64 * HalfRangeKind>) =
                match l with
                    | (lKey,lValue) :: (rKey, rValue) :: rest ->
                        arr.[i] <- Range1l(lKey, rKey - 1L)
                        write (i + 1) rest

                    | [_] -> failwith "bad RangeSet"

                    | [] -> ()
                    
            store |> MapExt.toList |> write 0
            arr

        member x.ToList() =
            let rec build (l : list<int64 * HalfRangeKind>) =
                match l with
                    | (lKey,lValue) :: (rKey, rValue) :: rest ->
                        Range1l(lKey, rKey - 1L) :: 
                        build rest

                    | [_] -> failwith "bad RangeSet"

                    | [] -> []

            store |> MapExt.toList |> build
             
        member x.ToSeq() =
            x :> seq<_>       

        override x.ToString() =
            let rec ranges (l : list<int64 * HalfRangeKind>) =
                match l with
                    | (kMin, vMin) :: (kMax, vMax) :: rest ->
                        sprintf "[%d,%d)" kMin kMax ::
                        ranges rest

                    | [(k,v)] ->
                        [ sprintf "ERROR: %d %A" k v ]

                    | [] ->
                        []
                
            store |> MapExt.toList |> ranges |> String.concat ", " |> sprintf "ranges [ %s ]"

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = new RangeSetEnumerator((store :> seq<_>).GetEnumerator()) :> _
            
        interface System.Collections.Generic.IEnumerable<Range1l> with
            member x.GetEnumerator() = new RangeSetEnumerator((store :> seq<_>).GetEnumerator()) :> _

    and private RangeSetEnumerator(e : IEnumerator<KeyValuePair<int64, HalfRangeKind>>) =
        
        let mutable a = Unchecked.defaultof<_>
        let mutable b = Unchecked.defaultof<_>

        member x.MoveNext() =
            if e.MoveNext() then
                a <- e.Current
                if e.MoveNext() then
                    b <- e.Current
                    true
                else
                    failwithf "impossible"
            else
                false
            
        member x.Reset() =
            e.Reset()
            a <- Unchecked.defaultof<_>
            b <- Unchecked.defaultof<_>

        member x.Current =
            assert (a.Value = HalfRangeKind.Left && b.Value = HalfRangeKind.Right)
            Range1l(a.Key, b.Key - 1L)

        member x.Dispose() =
            e.Dispose()
            a <- Unchecked.defaultof<_>
            b <- Unchecked.defaultof<_>

        interface System.Collections.IEnumerator with
            member x.MoveNext() = x.MoveNext()
            member x.Current = x.Current :> obj
            member x.Reset() = x.Reset()

        interface System.Collections.Generic.IEnumerator<Range1l> with
            member x.Dispose() = x.Dispose()
            member x.Current = x.Current

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module RangeSet =
        let empty = RangeSet.Empty

        let inline ofSeq (s : seq<Range1l>) = RangeSet.OfSeq s
        let inline ofList (s : list<Range1l>) = RangeSet.OfSeq s
        let inline ofArray (s : Range1l[]) = RangeSet.OfSeq s

        let inline add (r : Range1l) (s : RangeSet) = s.Add r
        let inline remove (r : Range1l) (s : RangeSet) = s.Remove r
        let inline contains (v : int64) (s : RangeSet) = s.Contains v
        let inline count (s : RangeSet) = s.Count

        let inline toSeq (s : RangeSet) = s :> seq<_>
        let inline toList (s : RangeSet) = s.ToList()
        let inline toArray (s : RangeSet) = s.ToArray()

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

        let align = int64 device.MinUniformBufferOffsetAlignment
        //let hm = device.HostMemory.AllocRaw(ptr.Size)
        
        let mutable ptr = 0n
        let hm, hostBuffer =
            let mutable handle = VkBuffer.Null
            let mutable info =
                VkBufferCreateInfo(
                    VkStructureType.BufferCreateInfo, 0n,
                    VkBufferCreateFlags.None,
                    uint64 size, 
                    VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit,
                    device.AllSharingMode,
                    device.AllQueueFamiliesCnt, device.AllQueueFamiliesPtr
                )
            VkRaw.vkCreateBuffer(device.Handle, &&info, NativePtr.zero, &&handle)
                |> check "could not create buffer"

            let mutable reqs = VkMemoryRequirements()
            VkRaw.vkGetBufferMemoryRequirements(device.Handle, handle, &&reqs)
            let hm = device.HostMemory.AllocRaw(int64 reqs.size)

            VkRaw.vkBindBufferMemory(device.Handle, handle, hm.Handle, 0UL)
                |> check "could not bind host memory"

            VkRaw.vkMapMemory(device.Handle, hm.Handle, 0UL, uint64 hm.Size, VkMemoryMapFlags.MinValue, &&ptr)
                |> check "could not map memory"
            
            hm, new Buffer(device, handle, hm, size, VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit)

        let mutable isEmpty = true

        let mutable dirty = RangeSet.empty

        member private x.HostBuffer = hostBuffer

        member x.Write(offset : int64, size : int64, data : nativeint) =
            LockedResource.access x (fun () ->
                isEmpty <- false
                assert (offset >= 0L && size >= 0L && offset + size <= hostBuffer.Size)
                Marshal.Copy(data, ptr + nativeint offset, size)

                let range = Range1l(offset, offset + size - 1L)
                Interlocked.Change(&dirty, RangeSet.add range) |> ignore
            )

        member x.Flush() =
            LockedResource.update x (fun () ->
                let dirty = Interlocked.Exchange(&dirty, RangeSet.empty)
                
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
                                    VkStructureType.MappedMemoryRange, 0n,
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
                                    do! Command.SyncWrite x
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
                                    //do! Command.Copy(hostBuffer, newBuffer.HostBuffer, copySize)
                                    do! Command.Copy(x, newBuffer, copySize)
                                    //do! Command.SyncWrite(newBuffer.HostBuffer)
                                    do! Command.SyncWrite(newBuffer)
                            }

                        run update

                        newBuffer
                    )
                else
                    x
            )
  
        member x.Realloc(newCapacity : int64) =
            x.Realloc(newCapacity, transfer.RunSynchronously)

        member x.Dispose() =
            if hostBuffer.Handle.IsValid then
                //VkRaw.vkUnmapMemory(device.Handle, hm.Handle)
                device.Delete hostBuffer
                ptr <- 0n
                device.Delete(x)

        interface ILockedResource with
            member x.Lock = lock.Lock
            member x.OnLock c = lock.OnLock c
            member x.OnUnlock c = lock.OnUnlock c

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type StreamingBufferOld(device : Device, rlock : ResourceLock2, usage : VkBufferUsageFlags, handle : VkBuffer, devPtr : DevicePtr, size : int64) =
        inherit Buffer(device, handle, devPtr, size, usage)

        let streamSize = size

        let mutable scratchBuffer, scratchMem, scratchPtr =
            if size > 0L then
                let mutable buffer = VkBuffer.Null
                let mutable info =
                    VkBufferCreateInfo(
                        VkStructureType.BufferCreateInfo, 0n,
                        VkBufferCreateFlags.None,
                        uint64 streamSize,
                        VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit,
                        device.AllSharingMode,
                        device.AllQueueFamiliesCnt,
                        device.AllQueueFamiliesPtr
                    )

                VkRaw.vkCreateBuffer(device.Handle, &&info, NativePtr.zero, &&buffer)
                    |> check "could not create buffer"

                let mutable reqs = VkMemoryRequirements()
                VkRaw.vkGetBufferMemoryRequirements(device.Handle, buffer, &&reqs)

                let compatible = reqs.memoryTypeBits &&& (1u <<< device.HostMemory.Index) <> 0u
                if not compatible then
                    failf "cannot create buffer with host visible memory"

                let bufferMem = device.HostMemory.AllocRaw(int64 reqs.size)

                VkRaw.vkBindBufferMemory(device.Handle, buffer, bufferMem.Handle, 0UL)
                    |> check "could not bind buffer memory"

                let mutable memPtr = 0n
                VkRaw.vkMapMemory(device.Handle, bufferMem.Handle, 0UL, uint64 bufferMem.Size, VkMemoryMapFlags.MinValue, &&memPtr)
                    |> check "could not map memory"

                buffer, bufferMem, memPtr
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
                        let gc = GCHandle.Alloc(todo, GCHandleType.Pinned)
                        cmd.AppendCommand()
                        VkRaw.vkCmdCopyBuffer(cmd.Handle, scratchBuffer, handle, uint32 todoCount, NativePtr.ofNativeInt (gc.AddrOfPinnedObject()))
                        gc.Free()

                    Disposable.Empty
            }

        member x.Dispose() =
            if scratchBuffer.IsValid then
                device.Delete x
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
                        let mutable region =
                            VkMappedMemoryRange(
                                VkStructureType.MappedMemoryRange, 0n,
                                scratchMem.Handle, 
                                uint64 scratchOffset, uint64 size
                            )

                        VkRaw.vkFlushMappedMemoryRanges(device.Handle, 1u, &&region)
                            |> check "could not flush range"

                        use t = device.Token

                        t.Enqueue 
                            { new Command() with
                                member x.Compatible = QueueFlags.All
                                member x.Enqueue cmd =
                                    let mutable copyInfo =
                                        VkBufferCopy(uint64 scratchOffset, uint64 offset, uint64 size)
                                    cmd.AppendCommand()
                                    VkRaw.vkCmdCopyBuffer(cmd.Handle, scratchBuffer, handle, 1u, &&copyInfo)
                                    Disposable.Empty
                            }

                        t.Sync()
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
                let mutable buffer = VkBuffer.Null
                let mutable info =
                    VkBufferCreateInfo(
                        VkStructureType.BufferCreateInfo, 0n,
                        VkBufferCreateFlags.None,
                        uint64 streamSize,
                        VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit,
                        device.AllSharingMode,
                        device.AllQueueFamiliesCnt,
                        device.AllQueueFamiliesPtr
                    )

                VkRaw.vkCreateBuffer(device.Handle, &&info, NativePtr.zero, &&buffer)
                    |> check "could not create buffer"

                let mutable reqs = VkMemoryRequirements()
                VkRaw.vkGetBufferMemoryRequirements(device.Handle, buffer, &&reqs)

                let compatible = reqs.memoryTypeBits &&& (1u <<< device.HostMemory.Index) <> 0u
                if not compatible then
                    failf "cannot create buffer with host visible memory"

                let bufferMem = device.HostMemory.Alloc(int64 reqs.alignment, int64 reqs.size)

                VkRaw.vkBindBufferMemory(device.Handle, buffer, bufferMem.Memory.Handle, uint64 bufferMem.Offset)
                    |> check "could not bind buffer memory"

//                let mutable memPtr = 0n
//                VkRaw.vkMapMemory(device.Handle, bufferMem.Handle, 0UL, uint64 bufferMem.Size, VkMemoryMapFlags.MinValue, &&memPtr)
//                    |> check "could not map memory"

//                let mutable all = VkMappedMemoryRange(VkStructureType.MappedMemoryRange, 0n, bufferMem.Handle, 0UL, uint64 bufferMem.Size)
//                VkRaw.vkFlushMappedMemoryRanges(device.Handle, 1u, &&all)
//                    |> check "could not flush mapped range"

                

                buffer, bufferMem
            else
                VkBuffer.Null, DevicePtr.Null

        let mutable isEmpty = true

        member x.Dispose() =
            if scratchBuffer.IsValid then
                device.Delete x
                //VkRaw.vkUnmapMemory(device.Handle, scratchMem.Handle)
                VkRaw.vkDestroyBuffer(device.Handle, scratchBuffer, NativePtr.zero)
                scratchMem.Dispose()
                scratchBuffer <- VkBuffer.Null
                //scratchPtr <- 0n

        member x.Write(offset : int64, size : int64, data : nativeint) =
            rlock.Lock.Use(ResourceUsage.Access, fun () ->
                isEmpty <- false
                scratchMem.Mapped (fun scratchPtr ->
                    Marshal.Copy(data, scratchPtr + nativeint offset, size)
                )

                let cmd = device.TransferFamily.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
                cmd.Begin(CommandBufferUsage.OneTimeSubmit)
                let mutable copyInfo = VkBufferCopy(uint64 offset, uint64 offset, uint64 size)
                cmd.AppendCommand()
                VkRaw.vkCmdCopyBuffer(cmd.Handle, scratchBuffer, handle, 1u, &&copyInfo)
                cmd.End()
                let queue = device.TransferFamily
                queue.RunSynchronously(cmd)
                cmd.Dispose()

            )

        member x.IsEmpty
            with get() = isEmpty
            and set e = isEmpty <- e

        interface ILockedResource with
            member x.Lock = rlock.Lock
            member x.OnLock c = rlock.OnLock c
            member x.OnUnlock c = rlock.OnUnlock c

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    [<AbstractClass; Sealed; Extension>]
    type DeviceMappedBufferExts private() =

        [<Extension>]
        static member CreateMappedBuffer(device : Device, lock : ResourceLock2, usage : VkBufferUsageFlags, size : int64) =
            let usage = VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.TransferSrcBit ||| usage
            let b = device |> Buffer.allocConcurrent true usage size
            new MappedBufferOld(device, lock, usage, b.Handle, b.Memory, size)


        [<Extension>]
        static member CreateStreamingBuffer(device : Device, lock : ResourceLock2, usage : VkBufferUsageFlags, size : int64) =
            if size = 0L then
                new StreamingBuffer(device, lock, usage, VkBuffer.Null, DevicePtr.Null, size)
            else
                let usage = VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.TransferSrcBit ||| usage
                let b = device |> Buffer.allocConcurrent true usage size
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
            
                elemSize, t, Mod.init (handle :> IBuffer)
            )

        let views =
            buffers |> Map.map (fun _ (_,t,b) ->
                Aardvark.Base.BufferView(b, t)
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

                                b.UnsafeCache <- n
                                t.Enqueue(b)
                                deleteBuffers.Add old

                            token.Sync()
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
        




