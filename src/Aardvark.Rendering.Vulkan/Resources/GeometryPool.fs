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

    type StreamingBuffer(device : Device, rlock : ResourceLock2, usage : VkBufferUsageFlags, handle : VkBuffer, devPtr : DevicePtr, size : uint64) =
        inherit Buffer(device, handle, devPtr, size, usage)

        let scratchBuffer =
            device.StagingMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit size

        let mutable isEmpty = true

        member _.ScratchBuffer = scratchBuffer

        override x.Destroy() =
            if scratchBuffer.IsValid then
                base.Destroy()
                scratchBuffer.Dispose()

        member x.Write(offset : uint64, size : uint64, data : nativeint) =
            rlock.Lock.Use(ResourceUsage.Access, fun () ->
                isEmpty <- false
                scratchBuffer.Memory.Mapped (fun scratchPtr ->
                    Marshal.Copy(data, scratchPtr + nativeint offset, size)
                )

                match device.UploadMode with
                    | UploadMode.Async ->
                        let tcs = new System.Threading.Tasks.TaskCompletionSource<unit>()
                        device.CopyEngine.Enqueue [
                            CopyCommand.Copy(scratchBuffer.Handle, offset, handle, offset, size)
                            CopyCommand.Callback(fun () -> tcs.SetResult())
                        ]
                        tcs.Task.Wait()

                    | _ ->
                        device.GraphicsFamily.RunSynchronously(
                            { new Command() with
                                member x.Compatible = QueueFlags.All
                                member x.Enqueue(cmd) =
                                    cmd.AppendCommand()
                                    let mutable copyInfo = VkBufferCopy(offset, offset, size)
                                    VkRaw.vkCmdCopyBuffer(cmd.Handle, scratchBuffer.Handle, handle, 1u, &&copyInfo)
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
        static member CreateStreamingBuffer(device : Device, lock : ResourceLock2, usage : VkBufferUsageFlags, size : uint64) =
            if size = 0UL then
                new StreamingBuffer(device, lock, usage, VkBuffer.Null, device.NullPtr, size)
            else
                let usage = VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.TransferSrcBit ||| usage
                let b = device.DeviceMemory |> Buffer.create' true false usage 0UL size
                new StreamingBuffer(device, lock, usage, b.Handle, b.Memory, size)


    type GeometryPool(device : Device, types : Map<Symbol, Type>) as this =
        let manager = MemoryManager.createNop()
        let minCapacity = 1UL <<< 10
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
                let elemSize = t.GetCLRSize() |> uint64

                let s = capacity * elemSize
                let handle = device.CreateStreamingBuffer(lock, usage, s)
                if device.DebugLabelsEnabled then
                    handle.Name <- $"{sem} (GeometryPool Buffer)"
                    handle.ScratchBuffer.Name <- $"{sem} (GeometryPool Scratch Buffer)"

                elemSize, t, AVal.init (handle :> IBuffer)
            )

        let views =
            buffers |> Map.map (fun _ (_,t,b) ->
                Aardvark.Rendering.BufferView(b, t)
            )

        let vertexSize = types |> Map.toSeq |> Seq.sumBy (fun (_,t) -> t.GetCLRSize() |> uint64)

        let reallocIfNeeded () =
            let newCapacity = manager.Capactiy + 1n |> int64 |> Fun.NextPowerOfTwo |> uint64 |> max minCapacity
            if newCapacity <> capacity then
                let result = 
                    lock.Lock.Use(fun () ->
                        let newCapacity = manager.Capactiy + 1n |> int64 |> Fun.NextPowerOfTwo |> uint64 |> max minCapacity
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
                                if notNull old.Name then
                                    n.Name <- old.Name
                                    n.ScratchBuffer.Name <- old.ScratchBuffer.Name

                                if copySize > 0UL && not old.IsEmpty then
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
                    let offset = elemSize * uint64 ptr.Offset
                    let size = elemSize * uint64 fvc

                    if size > 0UL then
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
            Map.tryFindV sem views

        member x.Dispose() =
            buffers |> Map.iter (fun _ (_,_,b) ->
                let b = unbox<StreamingBuffer> b.Value
                b.Dispose()
                manager.Dispose()
                capacity <- 0UL
                count <- 0
            )

        interface IGeometryPool with
            member x.Dispose() = x.Dispose()
            member x.Alloc(fvc, g) = x.Alloc(fvc, g)
            member x.Free(a) = x.Free(a)
            member x.TryGetBufferView(sem) = x.TryGetBufferView(sem)
            member x.UsedMemory = x.UsedMemory
            member x.Count = x.Count
        




