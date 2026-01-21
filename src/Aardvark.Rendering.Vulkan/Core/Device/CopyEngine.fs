namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System
open System.Collections.Generic
open System.Threading

// TODO: The copy engine currently does not acquire references to resource handles,
// risking that resources are freed while still in use. This may lead to problems
// in some scenarios.
[<RequireQualifiedAccess>]
type CopyCommand =
    internal
        | BufferToBufferCmd  of src : VkBuffer * dst : VkBuffer * info : VkBufferCopy
        | BufferToImageCmd   of src : VkBuffer * dst : VkImage * dstLayout : VkImageLayout * info : VkBufferImageCopy * size : uint64
        | ImageToBufferCmd   of src : VkImage * srcLayout : VkImageLayout * dst : VkBuffer * info : VkBufferImageCopy * size : uint64
        | ImageToImageCmd    of src : VkImage * srcLayout : VkImageLayout * dst : VkImage * dstLayout : VkImageLayout * info : VkImageCopy * size : uint64
        | CallbackCmd        of (unit -> unit)
        | ReleaseBufferCmd   of buffer : VkBuffer * offset : uint64 * size : uint64 * dstQueueFamily : uint32
        | ReleaseImageCmd    of image : VkImage * range : VkImageSubresourceRange * srcLayout : VkImageLayout * dstLayout : VkImageLayout * dstQueueFamily : uint32
        | TransformLayoutCmd of image : VkImage * range : VkImageSubresourceRange * srcLayout : VkImageLayout * dstLayout : VkImageLayout

    static member TransformLayout(image : VkImage, range : VkImageSubresourceRange, srcLayout : VkImageLayout, dstLayout : VkImageLayout) =
        CopyCommand.TransformLayoutCmd(image, range, srcLayout, dstLayout)

    static member SyncImage(image : VkImage, range : VkImageSubresourceRange, layout : VkImageLayout) =
        CopyCommand.TransformLayoutCmd(image, range, layout, layout)

    static member Copy(src : VkBuffer, srcOffset : uint64, dst : VkBuffer, dstOffset : uint64, size : uint64) =
        CopyCommand.BufferToBufferCmd(
            src,
            dst,
            VkBufferCopy(srcOffset, dstOffset, size)
        )

    static member Copy(src : VkBuffer, dst : VkImage, dstLayout : VkImageLayout, format : VkFormat, info : VkBufferImageCopy) =
        let sizeInBytes =
            uint64 info.imageExtent.width *
            uint64 info.imageExtent.height *
            uint64 info.imageExtent.depth *
            uint64 (VkFormat.sizeInBytes format)

        CopyCommand.BufferToImageCmd(
            src,
            dst, dstLayout,
            info, sizeInBytes
        )

    static member Copy(src : VkImage, srcLayout : VkImageLayout, dst : VkBuffer, format : VkFormat, info : VkBufferImageCopy) =
        let sizeInBytes =
            uint64 info.imageExtent.width *
            uint64 info.imageExtent.height *
            uint64 info.imageExtent.depth *
            uint64 (VkFormat.sizeInBytes format)

        CopyCommand.ImageToBufferCmd(
            src, srcLayout,
            dst,
            info, sizeInBytes
        )

    static member Callback(cb : unit -> unit) =
        CopyCommand.CallbackCmd cb

    static member Release(buffer : VkBuffer, offset : uint64, size : uint64, dstQueueFamily : int) =
        CopyCommand.ReleaseBufferCmd(buffer, offset, size, uint32 dstQueueFamily)

    static member Release(image : VkImage, range : VkImageSubresourceRange, srcLayout : VkImageLayout, dstLayout : VkImageLayout, dstQueueFamily : int) =
        CopyCommand.ReleaseImageCmd(image, range, srcLayout, dstLayout, uint32 dstQueueFamily)

    member x.SizeInBytes =
        match x with
            | BufferToBufferCmd(_,_,i) -> i.size
            | BufferToImageCmd(_,_,_,_,s) -> s
            | ImageToBufferCmd(_,_,_,_,s) -> s
            | _ -> 0UL

and CopyEngine(family: DeviceQueueFamily) =
    let familyIndex = family.Index

    //let trigger = new MultimediaTimer.Trigger(1)  // was for batching, introduces latency

    // queue
    let lockObj = obj()
    let mutable pending = List<CopyCommand>()
    let mutable totalSize = 0UL
    let mutable running = true


    // stats
    let statLock = obj()
    let mutable batchCount = 0
    let mutable copiedSize = Mem.Zero
    let mutable copyTime = MicroTime.Zero
    let mutable minBatchSize = Mem(1UL <<< 60)
    let mutable maxBatchSize = Mem(0UL)
    let enqueueMon = obj()
    let mutable vEnqueue = 0UL
    let mutable vDone = 0UL
    let run (_threadName : string) (queue : DeviceQueue) () =
        let device = queue.DeviceInterface
        let vkvm = device.VKVM
        let fence = new Fence(device)
        use pool : CommandPool = queue.Family.CreateCommandPool()
        use cmd : CommandBuffer = pool.CreateCommandBuffer(CommandBufferLevel.Primary)
        use stream = new VKVM.CommandStream()
        let sw = System.Diagnostics.Stopwatch()
        let empty = List<CopyCommand>()

        while running do
            // now: latency or batch updates. how to allow both
            //trigger.Wait()

            let copies, enq, totalSize =
                lock lockObj (fun () ->

                    if not running then
                        empty, 0UL, 0UL
                    else
                        while pending.Count = 0 && running do Monitor.Wait lockObj |> ignore
                        if not running then
                            empty, 0UL, 0UL
                        elif totalSize >= 0UL then
                            let mine = pending
                            let s = totalSize
                            pending <- List()
                            totalSize <- 0UL
                            mine, vEnqueue, s
                        else
                            empty, 0UL, 0UL
                )

            if copies.Count > 0 then
                sw.Restart()
                fence.Reset()
                stream.Clear()

                let conts = List<unit -> unit>()

                pool.Reset()
                cmd.Begin(CommandBufferUsage.OneTimeSubmit)
                cmd.AppendCommand()

                for copy in copies do
                    match copy with
                    | CopyCommand.CallbackCmd cont ->
                        conts.Add cont

                    | CopyCommand.BufferToBufferCmd(src, dst, info) ->
                        stream.CopyBuffer(src, dst, [| info |]) |> ignore

                    | CopyCommand.BufferToImageCmd(src, dst, dstLayout, info, _size) ->
                        stream.CopyBufferToImage(src, dst, dstLayout, [| info |]) |> ignore

                    | CopyCommand.ImageToBufferCmd(src, srcLayout, dst, info, _size) ->
                        stream.CopyImageToBuffer(src, srcLayout, dst, [| info |]) |> ignore

                    | CopyCommand.ImageToImageCmd(src, srcLayout, dst, dstLayout, info, _size) ->
                        stream.CopyImage(src, srcLayout, dst, dstLayout, [| info |]) |> ignore

                    | CopyCommand.ReleaseBufferCmd(buffer, offset, size, dstQueue) ->
                        stream.PipelineBarrier(
                            VkPipelineStageFlags.TransferBit,
                            VkPipelineStageFlags.BottomOfPipeBit,
                            [||],
                            [|
                                VkBufferMemoryBarrier(
                                    VkAccessFlags.TransferWriteBit,
                                    VkAccessFlags.None,
                                    uint32 familyIndex,
                                    dstQueue,
                                    buffer,
                                    offset, size
                                )
                            |],
                            [||]
                        ) |> ignore

                    | CopyCommand.ReleaseImageCmd(image, range, srcLayout, dstLayout, dstQueue) ->
                        stream.PipelineBarrier(
                            VkPipelineStageFlags.TransferBit,
                            VkPipelineStageFlags.BottomOfPipeBit,
                            [||],
                            [||],
                            [|
                                VkImageMemoryBarrier(
                                    VkAccessFlags.TransferWriteBit,
                                    VkAccessFlags.None,
                                    srcLayout, dstLayout,
                                    uint32 familyIndex,
                                    uint32 dstQueue,
                                    image,
                                    range
                                )
                            |]
                        ) |> ignore

                    | CopyCommand.TransformLayoutCmd(image, range, srcLayout, dstLayout) ->
                        stream.PipelineBarrier(
                            VkPipelineStageFlags.TransferBit, // copy engine only performs transfer operations
                            VkPipelineStageFlags.TransferBit,
                            [||],
                            [||],
                            [|
                                VkImageMemoryBarrier(
                                    VkAccessFlags.TransferWriteBit,                                   // make transfer writes available
                                    VkAccessFlags.TransferReadBit ||| VkAccessFlags.TransferWriteBit, // make them visible to subsequent reads and writes
                                    srcLayout, dstLayout,
                                    uint32 familyIndex,
                                    uint32 familyIndex,
                                    image,
                                    range
                                )
                            |]
                        ) |> ignore

                vkvm.Run(cmd.Handle, stream)
                cmd.End()

                cmd.Handle |> NativePtr.pin (fun pCmd ->
                    let submit =
                        VkSubmitInfo(
                            0u, NativePtr.zero, NativePtr.zero,
                            1u, pCmd,
                            0u, NativePtr.zero
                        )
                    submit |> NativePtr.pin (fun pSubmit ->
                        VkRaw.vkQueueSubmit(queue.Handle, 1u, pSubmit, fence.Handle) |> ignore
                    )
                )
                lock enqueueMon (fun () ->
                    vDone <- enq
                    Monitor.PulseAll enqueueMon
                )
                fence.Wait()
                sw.Stop()

                if totalSize > 0UL then
                    lock statLock (fun () ->
                        let totalSize = Mem totalSize
                        maxBatchSize <- max maxBatchSize totalSize
                        minBatchSize <- min minBatchSize totalSize
                        batchCount <- batchCount + 1
                        copiedSize <- copiedSize + totalSize
                        copyTime <- copyTime + sw.MicroTime
                    )

                for c in conts do c()

        fence.Dispose()

    let threads =
        let count = 1

        Array.init count (fun _ ->
            let h = family.CurrentQueue
            let name = sprintf "VulkanUploader%d" h.Queue.Index
            let thread =
                Thread(
                    ThreadStart(run name h.Queue),
                    IsBackground = true,
                    Name = name,
                    Priority = ThreadPriority.AboveNormal
                )
            thread.Start()
            thread, h
        )

    member x.ResetStats() =
        lock statLock (fun () ->
            batchCount <- 0
            copiedSize <- Mem.Zero
            copyTime <- MicroTime.Zero
            minBatchSize <- Mem(1UL <<< 60)
            maxBatchSize <- Mem(0UL)
        )

    member x.PrintStats(reset : bool) =
        lock statLock (fun () ->
            if batchCount > 0 then
                let averageSize = copiedSize / batchCount
                Log.line "batch: { min: %A; avg: %A; max: %A }" minBatchSize averageSize maxBatchSize
                Log.line "speed: %A/s" (copiedSize / copyTime.TotalSeconds)
                if reset then x.ResetStats()
        )


    member x.Cancel() =
        let wait =
            lock lockObj (fun () ->
                if running then
                    running <- false
                    Monitor.PulseAll lockObj
                    true
                else
                    false
            )

        if wait then
            //trigger.Signal()
            for t, _ in threads do t.Join()

    member x.Enqueue(commands : seq<CopyCommand>) =
        let size =
            lock lockObj (fun () ->
                pending.AddRange commands

                let s = commands |> Seq.fold (fun s c -> s + c.SizeInBytes) 0UL
                totalSize <- totalSize + s

                Monitor.PulseAll lockObj
                s
            )

        if size > 0UL then () // trigger.Signal()

    /// Enqueues the commands and waits for them to be submitted.
    member x.EnqueueSafe(commands : seq<CopyCommand>) =
        let enq, size =
            lock lockObj (fun () ->
                vEnqueue <- vEnqueue + 1UL
                pending.AddRange commands

                let s = commands |> Seq.fold (fun s c -> s + c.SizeInBytes) 0UL
                totalSize <- totalSize + s

                Monitor.PulseAll lockObj

                vEnqueue, s
            )

        lock enqueueMon (fun () ->
            while vDone < enq do
                Monitor.Wait enqueueMon |> ignore
        )
        if size > 0UL then () // trigger.Signal()

    member x.Wait() =
        let l = obj()
        let mutable finished = false

        let signal() =
            lock l (fun () ->
                finished <- true
                Monitor.Pulse l
            )

        let wait() =
            lock l (fun () ->
                while not finished do
                    Monitor.Wait l |> ignore
            )
        x.Enqueue [CopyCommand.Callback signal]
        wait()

    /// Runs the given commands and waits for them to finish.
    member x.RunSynchronously(commands : seq<CopyCommand>) =
        let l = obj()
        let mutable finished = false

        let signal() =
            lock l (fun () ->
                finished <- true
                Monitor.Pulse l
            )

        let wait() =
            lock l (fun () ->
                while not finished do
                    Monitor.Wait l |> ignore
            )

        x.Enqueue(
            Seq.append commands [CopyCommand.Callback signal]
        )
        wait()

    member x.WaitTask() =
        let tcs = System.Threading.Tasks.TaskCompletionSource()
        x.Enqueue(CopyCommand.Callback tcs.SetResult)
        tcs.Task

    member x.Enqueue(c : CopyCommand) =
        x.Enqueue (Seq.singleton c)

    member x.Dispose() =
        x.Cancel()
        for _, h in threads do h.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()