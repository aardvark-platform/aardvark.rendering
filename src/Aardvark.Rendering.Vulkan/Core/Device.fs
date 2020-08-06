namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Rendering
open KHRSwapchain

#nowarn "9"
//// #nowarn "51"

type private QueueFamilyPool(allFamilies : array<QueueFamilyInfo>) =
    let available = Array.copy allFamilies

    let familyScore (f : QueueFamilyInfo) =
        let flagScore = QueueFlags.score f.flags
        10 + flagScore * f.count

    member x.Take(caps : QueueFlags, count : int) =
        let families = 
            available 
                |> Array.toList
                |> List.indexed
                |> List.filter (fun (i, f) -> f.count > 0 && (f.flags &&& caps) = caps)
                |> List.sortByDescending (snd >> familyScore)


        let usedQueues = Dictionary.empty
        let mutable missing = count
        for (i, f) in families  do
            if missing > 0 then
                if f.count <= missing then
                    available.[i] <- { f with count = 0 }
                    usedQueues.[f.index] <- f.count
                    missing <- missing - f.count
                else
                    available.[i] <- { f with count = f.count - missing }
                    usedQueues.[f.index] <- missing
                    missing <- 0
     
        usedQueues
            |> Dictionary.toList
            |> List.map (fun (i,cnt) -> allFamilies.[i], cnt)    

    member x.TryTakeSingleFamily(caps : QueueFlags, count : int) =
        let families = 
            available 
                |> Array.toList
                |> List.indexed
                |> List.filter (fun (i, f) -> f.count > 0 && (f.flags &&& caps) = caps)
                |> List.sortByDescending (snd >> familyScore)
        
        let mutable chosen = None

        for (i, f) in families  do
            if Option.isNone chosen then
                if f.count >= count then
                    available.[i] <- { f with count = f.count - count }
                    chosen <- Some (f.index, count)

        match chosen with
            | Some (familyIndex, count) -> 
                Some (allFamilies.[familyIndex], count)

            | None ->
                match families with
                    | [] -> None
                    | (_, fam) :: _ -> 
                        available.[fam.index] <- { fam with count = 0 }
                        Some (allFamilies.[fam.index], fam.count)

    member x.TryTakeExplicit(min : QueueFlags, notMax : QueueFlags, count : int) =
        let families = 
            available 
                |> Array.toList
                |> List.indexed
                |> List.filter (fun (i, f) -> f.count > 0 && (f.flags &&& notMax) = QueueFlags.None && (f.flags &&& min) = min)
                |> List.sortByDescending (snd >> familyScore)
        
        let mutable chosen = None

        for (i, f) in families  do
            if Option.isNone chosen then
                if f.count >= count then
                    available.[i] <- { f with count = f.count - count }
                    chosen <- Some (f.index, count)

        match chosen with
            | Some (familyIndex, count) -> 
                Some (allFamilies.[familyIndex], count)

            | None ->
                match families with
                    | [] -> None
                    | (_, fam) :: _ -> 
                        available.[fam.index] <- { fam with count = 0 }
                        Some (allFamilies.[fam.index], fam.count)

[<RequireQualifiedAccess>]
type UploadMode =
    | Direct
    | Sync
    | Async

type Device internal(dev : PhysicalDevice, wantedExtensions : list<string>) as this =
    let isGroup, deviceGroup =
        match dev with
            | :? PhysicalDeviceGroup as g -> true, g.Devices
            | _ -> false, [| dev |]


    let physical = deviceGroup.[0]
    let pool = QueueFamilyPool(physical.QueueFamilies)
    let graphicsQueues  = pool.TryTakeSingleFamily(QueueFlags.Graphics, 1)
    let computeQueues   = pool.TryTakeExplicit(QueueFlags.Compute, QueueFlags.Graphics, 1)
    let transferQueues  = pool.TryTakeExplicit(QueueFlags.Transfer, QueueFlags.Compute ||| QueueFlags.Graphics, 1)
    let onDispose = Event<unit>()
    
    let mutable shaderCachePath : Option<string> = None
    let mutable validateShaderCaches = false
    let mutable debugReportActive = true

    let allIndicesArr = 
        [|
            let mutable mask = 1u
            for i in 0u .. 31u do
                if dev.DeviceMask &&& mask <> 0u then
                    yield i
                mask <- mask <<< 1
                
        |]

    let allIndices = 
        let ptr = NativePtr.alloc allIndicesArr.Length
        for i in 0 .. allIndicesArr.Length - 1 do
            NativePtr.set ptr i allIndicesArr.[i]
        ptr

    let allMask = dev.DeviceMask

    let extensions =
        let availableExtensions = physical.GlobalExtensions |> Seq.map (fun e -> e.name.ToLower(), e.name) |> Dictionary.ofSeq
        //let availableLayerNames = physical.AvailableLayers |> Seq.map (fun l -> l.name.ToLower(), l) |> Map.ofSeq
        //let enabledLayers = 
        //    wantedLayers |> Set.filter (fun name ->
        //        let name = name.ToLower()
        //        match Map.tryFind name availableLayerNames with
        //            | Some layer -> 
        //                VkRaw.debug "enabled layer %A" name
        //                for e in layer.extensions do
        //                    availableExtensions.[e.name.ToLower()] <- e.name
        //                true
        //            | _ ->
        //                VkRaw.warn "could not enable device-layer '%s' since it is not available" name
        //                false
        //    )

        let enabledExtensions =
            wantedExtensions |> List.choose (fun name ->
                let name = name.ToLower()
                match availableExtensions.TryGetValue name with
                    | (true, realName) -> 
                        VkRaw.debug "enabled extension %A" name
                        Some realName
                    | _ -> 
                        VkRaw.warn "could not enable device-extension '%s' since it is not available" name
                        None
            )

        enabledExtensions

    let mutable isDisposed = 0

    let instance = physical.Instance

    let mutable device =
        let queuePriorities =
            let ptr = NativePtr.alloc 32
            for i in 0 .. 31 do NativePtr.set ptr i 1.0f
            ptr

        let queueInfos =
            let counts = Dictionary.empty
            for (fam, cnt) in List.concat [Option.toList graphicsQueues; Option.toList computeQueues; Option.toList transferQueues] do
                match counts.TryGetValue fam.index with
                    | (true, o) -> counts.[fam.index] <- o + cnt
                    | _ -> counts.[fam.index] <- cnt

            counts 
                |> Dictionary.toArray 
                |> Array.map (fun (familyIndex, count) ->
                    VkDeviceQueueCreateInfo(
                        VkDeviceQueueCreateFlags.MinValue,
                        uint32 familyIndex,
                        uint32 count,
                        queuePriorities
                    )
                )

        native {
            let! ptr = queueInfos
            let extensions = List.toArray extensions
            let! pExtensions = extensions

            let features = 
                temporary<VkPhysicalDeviceFeatures, VkPhysicalDeviceFeatures> (fun pFeatures ->
                    VkRaw.vkGetPhysicalDeviceFeatures(physical.Handle, pFeatures)
                    NativePtr.read pFeatures
                )


            let deviceHandles = deviceGroup |> Array.map (fun d -> d.Handle)
            let! pDevices = deviceHandles
            let! pGroupInfo =
                VkDeviceGroupDeviceCreateInfo(
                    uint32 deviceGroup.Length,
                    pDevices
                )
                
            let next = if isGroup then NativePtr.toNativeInt pGroupInfo else 0n
            let! pFeatures = features
            let! pInfo =
                VkDeviceCreateInfo(
                    next,
                    VkDeviceCreateFlags.MinValue,
                    uint32 queueInfos.Length, ptr,
                    0u, NativePtr.zero,
                    uint32 extensions.Length, pExtensions,
                    pFeatures
                )
            let! pDevice = VkDevice.Zero
            
            VkRaw.vkCreateDevice(physical.Handle,pInfo, NativePtr.zero, pDevice)
                |> check "could not create device"

            return !!pDevice
        }

    let graphicsFamily, computeFamily, transferFamily =
        let offsets = Array.zeroCreate physical.QueueFamilies.Length

        let toFamily (fam : QueueFamilyInfo, count : int) =
            let offset = offsets.[fam.index]
            offsets.[fam.index] <- offset + count

            let queues =
                List.init count (fun i ->
                    DeviceQueue(this, device, fam, offset + i)
                )

            let family = new DeviceQueueFamily(this, fam, queues)
            family

        let graphicsFamily  = graphicsQueues |> Option.map toFamily
        let computeFamily   = computeQueues |> Option.map toFamily
        let transferFamily  = transferQueues |> Option.map toFamily

        let computeFamily =
            match computeFamily with
                | Some c -> Some c
                | None -> graphicsFamily

        graphicsFamily, computeFamily, transferFamily

    let queueFamilies =
        Array.concat [
            Option.toArray graphicsFamily
            Option.toArray computeFamily
            Option.toArray transferFamily
        ]

    let usedFamilies = 
        List.concat [ Option.toList graphicsQueues; Option.toList computeQueues; Option.toList transferQueues ]
            |> List.map (fun (f,_) -> f.index)
            |> Set.ofList
            |> Set.toArray

    let pAllFamilies =
        if usedFamilies.Length <= 1 then
            NativePtr.zero
        else
            let ptr = NativePtr.alloc usedFamilies.Length
            for i in 0 .. usedFamilies.Length-1 do
                NativePtr.set ptr i (uint32 usedFamilies.[i])
            ptr

    let pAllFamiliesCnt =
        if usedFamilies.Length <= 1 then 0u
        else uint32 usedFamilies.Length

    let concurrentSharingMode =
        if usedFamilies.Length = 1 then VkSharingMode.Exclusive
        else VkSharingMode.Concurrent


    let memories = 
        physical.MemoryTypes |> Array.map (fun t ->
            new DeviceHeap(this, physical, t, t.heap)
        )

    let hostMemory = memories.[physical.HostMemory.index]
    let deviceMemory = memories.[physical.DeviceMemory.index]

    let currentResourceToken = new ThreadLocal<ref<Option<DeviceToken>>>(fun _ -> ref None)
    let mutable runtime = Unchecked.defaultof<IRuntime>
    let memoryLimits = physical.Limits.Memory

    let caches = System.Collections.Concurrent.ConcurrentDictionary<Symbol, obj>()

    let uploadMode =
        if deviceMemory.IsHostVisible then
            UploadMode.Direct 
        else
            match transferFamily with
                | Some _ -> UploadMode.Async
                | None -> UploadMode.Sync

    let copyEngine = 
        lazy (
            match transferFamily with
                | Some pool -> new CopyEngine(pool)
                | None -> failf "the device does not support transfer-queues"
        )

    member x.CopyEngine = copyEngine.Value

    member x.DebugReportActive 
        with get() = debugReportActive
        and set v = debugReportActive <- v

    member x.ValidateShaderCaches
        with get() = validateShaderCaches
        and set v = validateShaderCaches <- v

    member x.ShaderCachePath
        with get() = shaderCachePath
        and set v = 
            match v with    
                | Some path ->
                    try
                        if not (System.IO.Directory.Exists path) then System.IO.Directory.CreateDirectory path |> ignore 
                        shaderCachePath <- Some path
                    with _ ->
                        shaderCachePath <- None
                | None -> 
                    shaderCachePath <- None

    member x.UploadMode = uploadMode

    member x.GetCache(name : Symbol) =
        let res =
            caches.GetOrAdd(name, fun name ->
                DeviceCache<'a, 'b>(x) :> obj
            )

        res |> unbox<DeviceCache<'a, 'b>>

    member x.GetCached(cacheName : Symbol, value : 'a, create : 'a -> 'b) : 'b =
        let cache : DeviceCache<'a, 'b> = x.GetCache(cacheName)
        cache.Invoke(value, create)

    member x.RemoveCached(cacheName : Symbol, value : 'b) : unit =
        match caches.TryGetValue cacheName with
            | (true, (:? IDeviceCache<'b> as c)) -> c.Revoke value
            | _ -> ()
                

    member x.ComputeToken =
        let ref = currentResourceToken.Value
        match !ref with
            | Some t ->
                t.AddRef()
                t
            | None ->
                let t = new DeviceToken(computeFamily.Value, ref)
                ref := Some t
                t 

    member x.Token =
        let ref = currentResourceToken.Value
        match !ref with
            | Some t ->
                t.AddRef()
                t
            | None ->
                let t = new DeviceToken(graphicsFamily.Value, ref)
                ref := Some t
                t 

    member x.UnsafeCurrentToken =
        !currentResourceToken.Value

    member x.UnsafeSetToken (t : Option<DeviceToken>) =
        let ref = currentResourceToken.Value
        ref := t

    member x.Sync() =
        let ref = currentResourceToken.Value
        match !ref with
            | Some t -> t.Sync()
            | _ -> ()

    member x.Runtime
        with get() = runtime
        and internal set r = runtime <- r

    [<Obsolete>]
    member x.QueueFamilies = queueFamilies
    
    member x.EnabledExtensions = extensions

    member x.MinMemoryMapAlignment = memoryLimits.MinMemoryMapAlignment
    member x.MinTexelBufferOffsetAlignment = memoryLimits.MinTexelBufferOffsetAlignment
    member x.MinUniformBufferOffsetAlignment = memoryLimits.MinUniformBufferOffsetAlignment
    member x.MinStorageBufferOffsetAlignment = memoryLimits.MinStorageBufferOffsetAlignment
    member x.BufferImageGranularity = memoryLimits.BufferImageGranularity

    member x.Instance = instance

    member internal x.AllQueueFamiliesPtr = pAllFamilies
    member internal x.AllQueueFamiliesCnt = pAllFamiliesCnt
    member internal x.AllSharingMode = concurrentSharingMode

    member internal x.AllMask = allMask
    member x.AllCount = uint32 deviceGroup.Length
    member internal x.AllIndices = allIndices
    member x.AllIndicesArr = allIndicesArr

    member x.GraphicsFamily : DeviceQueueFamily  = 
        match graphicsFamily with
            | Some pool -> pool
            | None -> failf "the device does not support graphics-queues"

    member x.ComputeFamily = 
        match computeFamily with
            | Some pool -> pool
            | None -> failf "the device does not support compute-queues"

    member x.TransferFamily = 
        match transferFamily with
            | Some pool -> pool
            | None -> failf "the device does not support transfer-queues"


    member x.IsDisposed = instance.IsDisposed || isDisposed <> 0

    member x.Memories = memories

    member x.HostMemory = hostMemory
    member x.DeviceMemory = deviceMemory

    member x.OnDispose = onDispose.Publish :> IObservable<_>

    member x.Dispose() =
        if not instance.IsDisposed then
            let o = Interlocked.Exchange(&isDisposed, 1)
            if o = 0 then 
                if copyEngine.IsValueCreated then
                    copyEngine.Value.Dispose()

                onDispose.Trigger()
                for h in memories do h.Dispose()
                for f in queueFamilies do f.Dispose()
                VkRaw.vkDestroyDevice(device, NativePtr.zero)
                device <- VkDevice.Zero

    member x.Handle = device

    member x.PhysicalDevice = physical
    member x.PhysicalDevices = deviceGroup
    member x.IsDeviceGroup = deviceGroup.Length > 1

    member x.CreateFence(signaled : bool) = new Fence(x, signaled)
    member x.CreateFence() = new Fence(x)
    member x.CreateSemaphore() = new Semaphore(x)
    member x.CreateEvent() = new Event(x)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and IDeviceCache<'b> =
    abstract member Revoke : 'b -> unit

and DeviceCache<'a, 'b when 'b :> RefCountedResource>(device : Device) =
    let store = Dict<'a, 'b>()
    let back = Dict<'b, 'a>()

    do  device.OnDispose.Add(fun _ ->
            for k in back.Keys do
                k.Destroy()
            store.Clear()
            back.Clear()
        )

    member x.Invoke(value : 'a, create : 'a -> 'b) : 'b =
        lock store (fun () -> 
            let create (value : 'a) =
                let res = create value
                back.[res] <- value
                res
            let res = store.GetOrCreate(value, Func<'a, 'b>(create))
            Interlocked.Increment(&res.RefCount) |> ignore
            res
        )

    member x.Revoke(thing : 'b) : unit =
        lock store (fun () ->
            if Interlocked.Decrement(&thing.RefCount) = 0 then
                match back.TryRemove thing with
                    | (true, key) -> 
                        store.Remove key |> ignore
                        thing.Destroy()
                    | _ ->
                        failf "asdasds"
        )

    interface IDeviceCache<'b> with
        member x.Revoke b = x.Revoke b

and [<RequireQualifiedAccess>] CopyCommand =
    internal
        | BufferToBufferCmd of src : VkBuffer * dst : VkBuffer * info : VkBufferCopy
        | BufferToImageCmd of src : VkBuffer * dst : VkImage * dstLayout : VkImageLayout * info : VkBufferImageCopy * size : int64
        | ImageToBufferCmd of src : VkImage * srcLayout : VkImageLayout * dst : VkBuffer * info : VkBufferImageCopy * size : int64
        | ImageToImageCmd of src : VkImage * srcLayout : VkImageLayout * dst : VkImage * dstLayout : VkImageLayout * info : VkImageCopy * size : int64
        | CallbackCmd of (unit -> unit)
        | ReleaseBufferCmd of buffer : VkBuffer * offset : int64 * size : int64 * dstQueueFamily : uint32
        | ReleaseImageCmd of image : VkImage * range : VkImageSubresourceRange * srcLayout : VkImageLayout * dstLayout : VkImageLayout * dstQueueFamily : uint32
        | TransformLayoutCmd of image : VkImage * range : VkImageSubresourceRange * srcLayout : VkImageLayout * dstLayout : VkImageLayout
        | SyncImageCmd of image : VkImage * range : VkImageSubresourceRange * layout : VkImageLayout * srcAccess : VkAccessFlags

    static member TransformLayout(image : VkImage, range : VkImageSubresourceRange, srcLayout : VkImageLayout, dstLayout : VkImageLayout) =
        CopyCommand.TransformLayoutCmd(image, range, srcLayout, dstLayout)

    static member SyncImage(image : VkImage, range : VkImageSubresourceRange, layout : VkImageLayout, srcAccess : VkAccessFlags) =
        CopyCommand.SyncImageCmd(image, range, layout, srcAccess)

    static member Copy(src : VkBuffer, srcOffset : int64, dst : VkBuffer, dstOffset : int64, size : int64) =
        CopyCommand.BufferToBufferCmd(
            src, 
            dst, 
            VkBufferCopy(uint64 srcOffset, uint64 dstOffset, uint64 size)
        )

    static member Copy(src : VkBuffer, srcOffset : int64, dst : VkImage, dstLayout : VkImageLayout, format : VkFormat, info : VkBufferImageCopy) =
        let sizeInBytes = 
            int64 info.imageExtent.width *
            int64 info.imageExtent.height * 
            int64 info.imageExtent.depth *
            int64 (VkFormat.sizeInBytes format)

        CopyCommand.BufferToImageCmd(
            src,
            dst, dstLayout,
            info, sizeInBytes
        )

    static member Copy(src : VkImage, srcLayout : VkImageLayout, dst : VkBuffer, format : VkFormat, info : VkBufferImageCopy) =
        let sizeInBytes = 
            int64 info.imageExtent.width *
            int64 info.imageExtent.height * 
            int64 info.imageExtent.depth *
            int64 (VkFormat.sizeInBytes format)

        CopyCommand.ImageToBufferCmd(
            src, srcLayout,
            dst,
            info, sizeInBytes
        )

    static member Callback(cb : unit -> unit) =
        CopyCommand.CallbackCmd cb

    static member Release(buffer : VkBuffer, offset : int64, size : int64, dstQueueFamily : int) =
        CopyCommand.ReleaseBufferCmd(buffer, offset, size, uint32 dstQueueFamily)

    static member Release(image : VkImage, range : VkImageSubresourceRange, srcLayout : VkImageLayout, dstLayout : VkImageLayout, dstQueueFamily : int) =
        CopyCommand.ReleaseImageCmd(image, range, srcLayout, dstLayout, uint32 dstQueueFamily)

    member x.SizeInBytes =
        match x with
            | BufferToBufferCmd(_,_,i) -> int64 i.size
            | BufferToImageCmd(_,_,_,_,s) -> s
            | ImageToBufferCmd(_,_,_,_,s) -> s
            | _ -> 0L

and CopyEngine(family : DeviceQueueFamily) =
    let device : Device = family.Device
    let graphicsFamily = device.GraphicsFamily.Index
    let familyIndex = family.Index

    //let trigger = new MultimediaTimer.Trigger(1)  // was for batching, introduces latency
   
    let maxCommandSize = 16L <<< 30

    // queue
    let lockObj = obj()
    let mutable pending = List<CopyCommand>()
    let mutable totalSize = 0L
    let mutable running = true


    // stats
    let statLock = obj()
    let mutable batchCount = 0
    let mutable copiedSize = Mem.Zero
    let mutable copyTime = MicroTime.Zero
    let mutable minBatchSize = Mem(1UL <<< 60)
    let mutable maxBatchSize = Mem(0UL)
    let enqueueMon = obj()
    let mutable vEnqueue = 0L
    let mutable vDone = -1L
    let run (threadName : string) (queue : DeviceQueue) () =
        let family = queue.FamilyIndex
        let device = queue.Device
        let fence = new Fence(device)
        use pool : CommandPool = queue.Family.CreateCommandPool()
        use cmd : CommandBuffer = pool.CreateCommandBuffer(CommandBufferLevel.Primary)
        use stream = new VKVM.CommandStream()
        let sw = System.Diagnostics.Stopwatch()
        let empty = List<CopyCommand>()

        while running do
            // now: latency or batch updates. how to allow both
            //trigger.Wait()

            

            let copies, totalSize = 
                lock lockObj (fun () ->

                    if not running then 
                        empty, 0L
                    else
                        while pending.Count = 0 && running do Monitor.Wait lockObj |> ignore
                        if not running then
                            empty, 0L
                        elif totalSize >= 0L then
                            let mine = pending
                            let s = totalSize
                            pending <- List()
                            totalSize <- 0L
                            mine, s
                        else
                            empty, 0L
                )

            if copies.Count > 0 then
                sw.Restart()
                fence.Reset()
                stream.Clear()

                let conts = System.Collections.Generic.List<unit -> unit>()

                pool.Reset()
                cmd.Begin(CommandBufferUsage.OneTimeSubmit)
                cmd.AppendCommand()

                for copy in copies do
                    match copy with
                        | CopyCommand.CallbackCmd cont ->
                            conts.Add cont

                        | CopyCommand.BufferToBufferCmd(src, dst, info) ->
                            stream.CopyBuffer(src, dst, [| info |]) |> ignore

                        | CopyCommand.BufferToImageCmd(src, dst, dstLayout, info, size) ->
                            stream.CopyBufferToImage(src, dst, dstLayout, [| info |]) |> ignore

                        | CopyCommand.ImageToBufferCmd(src, srcLayout, dst, info, size) ->
                            stream.CopyImageToBuffer(src, srcLayout, dst, [| info |]) |> ignore

                        | CopyCommand.ImageToImageCmd(src, srcLayout, dst, dstLayout, info, size) ->
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
                                        uint64 offset, uint64 size
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
                                VkImageLayout.toSrcStageFlags srcLayout,
                                VkImageLayout.toDstStageFlags dstLayout,
                                [||],
                                [||],
                                [|
                                    VkImageMemoryBarrier(
                                        VkImageLayout.toAccessFlags srcLayout,
                                        VkImageLayout.toAccessFlags dstLayout,
                                        srcLayout, dstLayout,
                                        uint32 familyIndex,
                                        uint32 familyIndex,
                                        image,
                                        range
                                    )
                                |]
                            ) |> ignore

                        | CopyCommand.SyncImageCmd(image, range, layout, srcAccess) ->
                            stream.PipelineBarrier(
                                VkAccessFlags.toVkPipelineStageFlags srcAccess,
                                VkPipelineStageFlags.TopOfPipeBit,
                                [||],
                                [||],
                                [|
                                    VkImageMemoryBarrier(
                                        srcAccess,
                                        VkAccessFlags.None,
                                        layout, layout,
                                        uint32 familyIndex,
                                        uint32 familyIndex,
                                        image,
                                        range
                                    )
                                |]
                            ) |> ignore
                stream.Run cmd.Handle
                cmd.End()

                cmd.Handle |> pin (fun pCmd ->
                    let submit =
                        VkSubmitInfo(
                            0u, NativePtr.zero, NativePtr.zero,
                            1u, pCmd,
                            0u, NativePtr.zero
                        )
                    submit |> pin (fun pSubmit ->
                        VkRaw.vkQueueSubmit(queue.Handle, 1u, pSubmit, fence.Handle) |> ignore
                    )
                )
                lock enqueueMon (fun () -> 
                    vDone <- vEnqueue
                    Monitor.PulseAll enqueueMon
                )
                fence.Wait()
                sw.Stop()

                if totalSize > 0L then
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
        family.Queues |> List.take 1 |> List.map (fun (q : DeviceQueue) -> 
            let name = sprintf "VulkanUploader%d" q.Index
            let thread = 
                Thread(
                    ThreadStart(run name q), 
                    IsBackground = true, 
                    Name = name, 
                    Priority = ThreadPriority.AboveNormal
                )
            thread.Start()
            thread
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
            threads |> List.iter (fun t -> t.Join())

    member x.Enqueue(commands : seq<CopyCommand>) =
        let size = 
            lock lockObj (fun () ->
                pending.AddRange commands
                        
                //pending.AddRange commands
                let s = commands |> Seq.fold (fun s c -> s + c.SizeInBytes) 0L 
                totalSize <- totalSize + s

                Monitor.PulseAll lockObj
                s
            )

        if size > 0L then () // trigger.Signal()

    member x.EnqueueSafe(commands : seq<CopyCommand>) =
        let v = Interlocked.Increment(&vEnqueue)
        let size = 
            lock lockObj (fun () ->
                pending.AddRange commands
                        
                //pending.AddRange commands
                let s = commands |> Seq.fold (fun s c -> s + c.SizeInBytes) 0L 
                totalSize <- totalSize + s

                Monitor.PulseAll lockObj

                s
            )

        lock enqueueMon (fun () -> 
            while vDone < v do
                Monitor.Wait enqueueMon |> ignore
        )
        if size > 0L then () // trigger.Signal()

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

    member x.WaitTask() =
        let tcs = System.Threading.Tasks.TaskCompletionSource()
        x.Enqueue(CopyCommand.Callback tcs.SetResult)
        tcs.Task

    member x.Enqueue(c : CopyCommand) =
        x.Enqueue (Seq.singleton c)
          
    member x.Dispose() = x.Cancel()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and RefCountedResource =
    class
        val mutable public RefCount : int

        abstract member Destroy : unit -> unit
        default x.Destroy() = ()

        new() = { RefCount = 0}
    end

and [<AbstractClass>] Resource =
    class
        val mutable public Device : Device

        abstract member IsValid : bool
        default x.IsValid = x.Device.Handle <> 0n

        new(device : Device) = { Device = device }
    end

and [<AbstractClass>] Resource<'a when 'a : unmanaged and 'a : equality> =
    class
        inherit Resource
        val mutable public Handle : 'a

        override x.IsValid =
            not x.Device.IsDisposed && x.Handle <> Unchecked.defaultof<_>

        new(device : Device, handle : 'a) = 
            { inherit Resource(device); Handle = handle }
    end


and DeviceQueue internal(device : Device, deviceHandle : VkDevice, familyInfo : QueueFamilyInfo, index : int) =
    let handle = 
        temporary<VkQueue, VkQueue> (fun pQueue ->
            VkRaw.vkGetDeviceQueue(deviceHandle, uint32 familyInfo.index, uint32 index, pQueue)
            NativePtr.read pQueue
        )


    let transfer = QueueFlags.transfer familyInfo.flags
    let compute = QueueFlags.compute familyInfo.flags
    let graphics = QueueFlags.graphics familyInfo.flags
    let mutable family : DeviceQueueFamily = Unchecked.defaultof<_>

    member x.HasTransfer = transfer
    member x.HasCompute = compute
    member x.HasGraphics = graphics

    member x.Device = device
    member x.Family
        with get() : DeviceQueueFamily = family
        and internal set (f : DeviceQueueFamily) = family <- f

    member x.Flags = familyInfo.flags
    member x.FamilyIndex = familyInfo.index
    member x.Index = index
    member x.Handle = handle

    [<Obsolete>]
    member x.BindSparse(binds : VkBindSparseInfo[], fence : VkFence) =
        lock x (fun () ->
            if device.IsDeviceGroup then
                let groupInfos =
                    binds |> Array.collect (fun b ->
                        device.AllIndicesArr |> Array.map (fun i ->
                            VkDeviceGroupBindSparseInfo(
                                uint32 i, uint32 i
                            )
                        )
                    )

                native {
                    let! pGroupInfos = groupInfos
                    let binds = 
                        let mutable gi = 0
                        binds |> Array.collect (fun b ->
                            device.AllIndicesArr |> Array.map (fun i ->
                                let mutable res = b
                                res.pNext <- NativePtr.toNativeInt (NativePtr.add pGroupInfos gi)
                                gi <- gi + 1
                                res
                            )
                        )
                    let! pBinds= binds
                    return VkRaw.vkQueueBindSparse(handle, uint32 binds.Length, pBinds, fence)
                }
            else
                native {
                    let! pBinds = binds
                    return VkRaw.vkQueueBindSparse(handle, uint32 binds.Length, pBinds, fence)
                }
        )
        
    member private x.SubmitPrivate(cmds : list<CommandBuffer>, waitFor : list<Semaphore>, signal : list<Semaphore>, fence : Option<Fence>) =
        lock x (fun () ->
            let waitFor = waitFor |> List.map (fun s -> s.Handle) |> List.toArray
            let masks = Array.init waitFor.Length (fun _ -> int VkPipelineStageFlags.TopOfPipeBit)
            let signal = signal |> List.map (fun s -> s.Handle) |> List.toArray
            let cmds = cmds |> List.map (fun cmd -> cmd.Handle) |> List.toArray

            let fence =
                match fence with
                    | Some f -> f.Handle
                    | None -> VkFence.Null

            native {
                let! pSignal = signal
                let! pMasks = masks
                let! pWaitFor = waitFor
                let! pCmds = cmds

                if device.IsDeviceGroup then
                    let! pCmdMasks = cmds |> Array.map (fun _ -> device.AllMask)
                    //let mask = device.AllMask

                    let waitCount, pWaitIndices =
                        if waitFor.Length > 0 then device.AllCount, device.AllIndices
                        else 0u, NativePtr.zero
                    
                    let signalCount, pSignalIndices =
                        if waitFor.Length > 0 then device.AllCount, device.AllIndices
                        else 0u, NativePtr.zero

                    let ext =
                        VkDeviceGroupSubmitInfo(
                            waitCount, pWaitIndices,
                            uint32 cmds.Length, pCmdMasks,
                            signalCount, pSignalIndices
                        )
                    ext |> pin (fun pExt ->

                        let submit =
                            VkSubmitInfo(
                                NativePtr.toNativeInt pExt,
                                uint32 waitFor.Length, pWaitFor, NativePtr.cast pMasks,
                                uint32 cmds.Length, pCmds,
                                uint32 signal.Length, pSignal
                            )
                        submit |> pin (fun pSubmit ->
                            VkRaw.vkQueueSubmit(handle, 1u, pSubmit, fence)
                                |> check "could not submit command buffer"
                        )   
                    )

                else
                    let submit =
                        VkSubmitInfo(
                            uint32 waitFor.Length, pWaitFor, NativePtr.cast pMasks,
                            uint32 cmds.Length, pCmds,
                            uint32 signal.Length, pSignal
                        )
                        
                    submit |> pin (fun pSubmit ->
                        VkRaw.vkQueueSubmit(handle, 1u, pSubmit, fence)
                            |> check "could not submit command buffer"
                    )
            }
        )

    [<Obsolete>]
    member x.Submit(cmds : list<CommandBuffer>, waitFor : list<Semaphore>, signal : list<Semaphore>, fence : Option<Fence>) =
        x.SubmitPrivate(cmds, waitFor, signal, fence)

   
    [<Obsolete>]
    member x.RunSynchronously(cmd : CommandBuffer) =
        if cmd.IsRecording then
            failf "cannot submit recording CommandBuffer"

        if not cmd.IsEmpty then
            let fence = device.CreateFence()
            x.SubmitPrivate([cmd], [], [], Some fence)
            fence.Wait()
            fence.Dispose()

    [<Obsolete>]
    member x.RunSynchronously(cmd : CommandBuffer, waitFor : list<Semaphore>) =
        if cmd.IsRecording then
            failf "cannot submit recording CommandBuffer"

        if not cmd.IsEmpty then
            let fence = device.CreateFence()
            x.SubmitPrivate([cmd], waitFor, [], Some fence)
            fence.Wait()
            fence.Dispose()

    [<Obsolete>]
    member x.StartFence(cmd : CommandBuffer) =
        if cmd.IsRecording then
            failf "cannot submit recording CommandBuffer"

        if not cmd.IsEmpty then
            let fence = device.CreateFence()
            x.SubmitPrivate([cmd], [], [], Some fence)
            Some fence
        else
            None

    [<Obsolete>]
    member x.Submit(cmd : CommandBuffer, fence : Fence) =
        if cmd.IsRecording then
            failf "cannot submit recording CommandBuffer"

        if not cmd.IsEmpty then
            x.SubmitPrivate([cmd], [], [], Some fence)
            true
        else
            false

    [<Obsolete>]
    member x.StartAsync(cmd : CommandBuffer, waitFor : list<Semaphore>) =
        if cmd.IsRecording then
            failf "cannot submit recording CommandBuffer"

        let sem = device.CreateSemaphore()
        x.SubmitPrivate([cmd], waitFor, [sem], None)
        sem
        
    [<Obsolete>]
    member x.StartAsync(cmd : CommandBuffer) =
        if cmd.IsRecording then
            failf "cannot submit recording CommandBuffer"

        let sem = device.CreateSemaphore()
        x.SubmitPrivate([cmd], [], [sem], None)
        sem
        
    [<Obsolete>]
    member x.Wait(sem : Semaphore) =
        let f = device.CreateFence()
        x.SubmitPrivate([], [sem], [], Some f)
        f.Wait()
        f.Dispose()
        
    [<Obsolete>]
    member x.Wait(sems : seq<Semaphore>) =
        let f = device.CreateFence()
        x.SubmitPrivate([], Seq.toList sems, [], Some f)
        f.Wait()
        f.Dispose()

    [<Obsolete>]
    member x.WaitIdle() =
        lock x (fun () ->
            VkRaw.vkQueueWaitIdle(x.Handle)
                |> check "could not wait for queue"
        )

and DeviceQueuePool internal(device : Device, queues : list<DeviceQueueFamily>) =
    let available : HashSet<QueueFlags> = queues |> List.collect (fun f -> Enum.allSubFlags f.Flags) |> HashSet.ofList
    let store : MultiTable<DeviceQueueFamily, DeviceQueue> = 
        queues 
            |> List.collect (fun f -> f.Queues |> List.map (fun q -> f, q)) 
            |> MultiTable

    
    let changed = new AutoResetEvent(false)

    let checkFlags (cap : QueueFlags) (f : DeviceQueueFamily) =
        f.Flags &&& cap = cap

    let tryAcquireFlags (flags : QueueFlags) (queue : byref<DeviceQueue>) =
        Monitor.Enter store
        try store.TryRemove(checkFlags flags, &queue)
        finally Monitor.Exit store

    member x.TryAcquire(flags : QueueFlags, [<Out>] queue : byref<DeviceQueue>) : bool =
        if available.Contains flags then
            tryAcquireFlags flags &queue
        else
            false

    member x.Acquire (flags : QueueFlags) : DeviceQueue =
        if available.Contains flags then
            let mutable res = Unchecked.defaultof<_>
            while not (tryAcquireFlags flags &res) do
                changed.WaitOne() |> ignore
            res
        else
            failf "no queue with flags %A exists" flags

    member x.TryPeek(flags : QueueFlags, [<Out>] queue : byref<DeviceQueue>) : bool =
        if available.Contains flags then
            Monitor.Enter store
            try store.TryPeek(checkFlags flags, &queue)
            finally Monitor.Exit store
        else
            false

    member x.TryAcquire(family : DeviceQueueFamily, [<Out>] queue : byref<DeviceQueue>) =
        Monitor.Enter store
        try store.TryRemove((fun f -> f = family), &queue)
        finally Monitor.Exit store

    member x.TryPeek(family : DeviceQueueFamily, [<Out>] queue : byref<DeviceQueue>) =
        Monitor.Enter store
        try store.TryPeek((fun f -> f = family), &queue)
        finally Monitor.Exit store

    member x.Acquire (family : DeviceQueueFamily) : DeviceQueue =
        let mutable res = Unchecked.defaultof<_>
        while not (x.TryAcquire(family, &res)) do
            changed.WaitOne() |> ignore
        res

    member x.Release (queue : DeviceQueue) : unit =
        lock store (fun () ->
            if store.Add(queue.Family, queue) then
                changed.Set() |> ignore
        )

and DeviceTemporaryCommandPool(family : DeviceQueueFamily) =
    let bag = ConcurrentBag<CommandPool>()

    let mutable disposeInstalled = 0

    let dispose() =
        bag |> Seq.iter (fun c -> c.Destroy())

    member x.Take() =
        match bag.TryTake() with
            | (true, pool) -> 
                pool
            | _ ->
                if Interlocked.Exchange(&disposeInstalled, 1) = 0 then
                    family.Device.OnDispose.Add dispose
                { new CommandPool(family.Device, family.Index, family, CommandPoolFlags.ResetBuffer) with
                    override x.Dispose() =
                        bag.Add x
                }



and DeviceQueueFamily internal(device : Device, info : QueueFamilyInfo, queues : list<DeviceQueue>) as this =
    let store = queues |> List.toArray
    do for q in store do q.Family <- this
    let mutable current = 0

    let defaultPool = new DeviceCommandPool(device, info.index, this)
    let tempPool = new DeviceTemporaryCommandPool(this)
    
    let thread = lazy (new DeviceQueueThread(this))


    member x.TakeCommandPool() = tempPool.Take()

    member x.Device = device
    member x.Info = info
    member x.Index : int = info.index
    member x.Flags = info.flags
    member x.Queues = queues

    [<Obsolete>]
    member x.DefaultCommandPool = defaultPool

    member x.CreateCommandPool () =
        new CommandPool(device, info.index, x)

    member x.CreateCommandPool (flags : CommandPoolFlags) =
        new CommandPool(device, info.index, x, flags)

    member x.Start (cmd : QueueCommand) : DeviceTask =
        thread.Value.Enqueue cmd
        
    member x.Start (priority : int, cmd : QueueCommand) : DeviceTask =
        thread.Value.Enqueue(priority, cmd)

    member x.RunSynchronously(cmd : QueueCommand) : unit =
        let t = thread.Value.Enqueue cmd
        t.Wait()
        if t.IsFaulted then raise t.Exception
        
    member x.RunSynchronously(priority : int, cmd : QueueCommand) : unit =
        let t = thread.Value.Enqueue(priority, cmd)
        t.Wait()
        if t.IsFaulted then raise t.Exception

    member x.Start (cmd : CommandBuffer) =
        x.Start(QueueCommand.Submit([], [], [cmd]))
        
    member x.Start (priority : int, cmd : CommandBuffer) =
        x.Start(priority, QueueCommand.Submit([], [], [cmd]))
        
    member x.RunSynchronously (cmd : CommandBuffer) =
        x.RunSynchronously (QueueCommand.Submit([], [], [cmd]))
        
    member x.RunSynchronously (priority : int, cmd : CommandBuffer) =
        x.RunSynchronously (priority, QueueCommand.Submit([], [], [cmd]))

    [<Obsolete>]
    member x.GetQueue () : DeviceQueue =
        let next = Interlocked.Change(&current, fun c -> (c + 1) % store.Length)
        store.[next]

    member x.Dispose() =
        if thread.IsValueCreated then thread.Value.Cancel()
        defaultPool.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and DeviceCommandPool internal(device : Device, index : int, queueFamily : DeviceQueueFamily) =

    let allPools = ConcurrentHashSet<VkCommandPool>()

    let createCommandPoolHandle _ =
        let createInfo =
            VkCommandPoolCreateInfo(
                VkCommandPoolCreateFlags.ResetCommandBufferBit,
                uint32 index
            )
        let handle = 
            createInfo |> pin (fun pCreate ->
                temporary<VkCommandPool,VkCommandPool> (fun pHandle ->
                    VkRaw.vkCreateCommandPool(device.Handle, pCreate, NativePtr.zero, pHandle)
                        |> check "could not create command pool"
                    NativePtr.read pHandle
                )

            )
        allPools.Add handle |> ignore

        handle

    let handles = new ThreadLocal<VkCommandPool>(createCommandPoolHandle)

    //let handles = new ConcurrentDictionary<int, VkCommandPool * ConcurrentBag<CommandBuffer> * ConcurrentBag<CommandBuffer>>()
    let get () =
        handles.Value
        //handles.GetOrAdd(key, System.Func<_,_>(createCommandPoolHandle))

    member x.Device = device
    member x.QueueFamily = queueFamily

    member x.CreateCommandBuffer(level : CommandBufferLevel) =
        let pool = get()
        new CommandBuffer(device, pool, queueFamily, level)


    member x.Dispose() =
        if device.Handle <> 0n then
            let all = allPools |> Seq.toArray
            allPools.Clear()
            handles.Dispose()
            all |> Seq.iter (fun h -> 
                if h.IsValid then
                    VkRaw.vkDestroyCommandPool(device.Handle, h, NativePtr.zero)
            )

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and [<Flags>] CommandPoolFlags =
    | None          = 0
    | Transient     = 1
    | ResetBuffer   = 2
    | Protected     = 4

and CommandPool internal(device : Device, familyIndex : int, queueFamily : DeviceQueueFamily, flags : CommandPoolFlags) =
    let mutable handle =
        let createInfo =
            VkCommandPoolCreateInfo(
                flags |> int |> unbox,
                uint32 familyIndex
            )
        createInfo |> pin (fun pCreate ->
            temporary<VkCommandPool, VkCommandPool> (fun pHandle ->
                VkRaw.vkCreateCommandPool(device.Handle, pCreate, NativePtr.zero, pHandle)
                    |> check "could not create command pool"
                NativePtr.read pHandle
            )
        )

    internal new(device : Device, familyIndex : int, queueFamily : DeviceQueueFamily) =
        new CommandPool(device, familyIndex, queueFamily, CommandPoolFlags.None)

    member x.Device = device
    member x.QueueFamily = queueFamily
    member x.Handle = handle

    member x.Reset() =
        VkRaw.vkResetCommandPool(device.Handle, handle, VkCommandPoolResetFlags.None)
            |> check "failed to reset command pool"

    member x.Destroy() =
        if handle.IsValid && device.Handle <> 0n then
            VkRaw.vkDestroyCommandPool(device.Handle, handle, NativePtr.zero)

    abstract member Dispose : unit -> unit
    default x.Dispose() = x.Destroy()
    member x.CreateCommandBuffer(level : CommandBufferLevel) =
        new CommandBuffer(device, handle, queueFamily, level)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and CommandBuffer internal(device : Device, pool : VkCommandPool, queueFamily : DeviceQueueFamily, level : CommandBufferLevel) =

    let mutable handle = 
        native {
            let! pInfo =
                VkCommandBufferAllocateInfo(
                    pool,
                    unbox (int level),
                    1u
                )
            let! pHandle = VkCommandBuffer.Zero
            VkRaw.vkAllocateCommandBuffers(device.Handle, pInfo, pHandle)
                |> check "could not allocated command buffer"

            return !!pHandle
        }

    let mutable commands = 0
    let mutable recording = false
    let cleanupTasks = List<IDisposable>()
    
    let cleanup() =
        for c in cleanupTasks do c.Dispose()
        cleanupTasks.Clear()

    let beginPrimary (usage : CommandBufferUsage) =
        native {
            let! pInfo =
                VkCommandBufferBeginInfo(
                    unbox (int usage),
                    NativePtr.zero
                )

            VkRaw.vkBeginCommandBuffer(handle, pInfo)
                |> check "could not begin command buffer"
        }

    let beginSecondary (pass : VkRenderPass) (framebuffer : VkFramebuffer) (inheritQueries : bool) (usage : CommandBufferUsage) =
        native {
            let occlusion, control, statistics =
                match inheritQueries with
                | true -> 1u, VkQueryControlFlags.All, VkQueryPipelineStatisticFlags.All
                | _ -> 0u, VkQueryControlFlags.None, VkQueryPipelineStatisticFlags.None

            let! pInheritanceInfo =
                VkCommandBufferInheritanceInfo(
                    pass, 0u, framebuffer,
                    occlusion, control, statistics
                )

            let! pInfo =
                VkCommandBufferBeginInfo(
                    unbox (int usage),
                    pInheritanceInfo
                )

            VkRaw.vkBeginCommandBuffer(handle, pInfo)
                |> check "could not begin command buffer"
        }

    let beginBuffer (pass : VkRenderPass) (framebuffer : VkFramebuffer) (inheritQueries : bool) (usage : CommandBufferUsage) =
        cleanup()

        match level with
        | CommandBufferLevel.Primary -> beginPrimary usage
        | CommandBufferLevel.Secondary -> beginSecondary pass framebuffer inheritQueries usage
        | _ -> failwith "unknown command buffer level"

        commands <- 0
        recording <- true

    member x.Reset() =
        cleanup()

        VkRaw.vkResetCommandBuffer(handle, VkCommandBufferResetFlags.ReleaseResourcesBit)
            |> check "could not reset command buffer"
        commands <- 0
        recording <- false

    member x.Begin(pass : Resource<VkRenderPass>, framebuffer : Resource<VkFramebuffer>, usage : CommandBufferUsage, inheritQueries : bool) =
        beginBuffer pass.Handle framebuffer.Handle inheritQueries usage

    member x.Begin(usage : CommandBufferUsage) =
        beginBuffer VkRenderPass.Null VkFramebuffer.Null false usage

    member x.Begin(usage : CommandBufferUsage, inheritQueries : bool) =
        beginBuffer VkRenderPass.Null VkFramebuffer.Null inheritQueries usage

    member x.Begin(pass : Resource<VkRenderPass>, usage : CommandBufferUsage) =
        beginBuffer pass.Handle VkFramebuffer.Null false usage

    member x.Begin(pass : Resource<VkRenderPass>, framebuffer : Resource<VkFramebuffer>, usage : CommandBufferUsage) =
        beginBuffer pass.Handle framebuffer.Handle false usage

    member x.End() =
        VkRaw.vkEndCommandBuffer(handle)
            |> check "could not end command buffer"
        recording <- false

    member x.AppendCommand() =
        if not recording then failf "cannot enqueue commands to non-recording CommandBuffer"
        commands <- commands + 1

    member x.Set(e : Event, flags : VkPipelineStageFlags) =
        x.AppendCommand()
        VkRaw.vkCmdSetEvent(handle, e.Handle, flags)

    member x.Reset(e : Event, flags : VkPipelineStageFlags) =
        x.AppendCommand()
        VkRaw.vkCmdResetEvent(handle, e.Handle, flags)
 
    member x.WaitAll(e : Event[]) =
        x.AppendCommand()
        native {
            let handles = e |> Array.map (fun e -> e.Handle)
            let! ptr = handles
            VkRaw.vkCmdWaitEvents(
                handle, uint32 handles.Length, ptr,
                VkPipelineStageFlags.None, VkPipelineStageFlags.AllCommandsBit,
                0u, NativePtr.zero,
                0u, NativePtr.zero,
                0u, NativePtr.zero
            )
        }
    member x.WaitAll(e : Event[], dstFlags : VkPipelineStageFlags) =
        x.AppendCommand()
        native {
            let handles = e |> Array.map (fun e -> e.Handle)
            let! ptr = handles
            VkRaw.vkCmdWaitEvents(
                handle, uint32 handles.Length, ptr,
                VkPipelineStageFlags.None, dstFlags,
                0u, NativePtr.zero,
                0u, NativePtr.zero,
                0u, NativePtr.zero
            )
        }

    member x.IsEmpty = commands = 0
    member x.CommandCount = commands
    member x.IsRecording = recording
    member x.Level = level
    member x.Handle = handle
    member x.Device = device
    member x.QueueFamily = queueFamily
    member x.Pool = pool

    member x.AddCompensation (d : IDisposable) =
        cleanupTasks.Add d

    member x.Cleanup() =
        cleanup()

    member x.ClearCompensation() =
        cleanupTasks.Clear()



    //abstract member Dispose : unit -> unit
    member private x.Dispose(disposing : bool) =
        if handle <> 0n && device.Handle <> 0n then
            cleanup()
            
            handle |> pin (fun pHandle -> VkRaw.vkFreeCommandBuffers(device.Handle, pool, 1u, pHandle))
            handle <- 0n

        if disposing then 
            GC.SuppressFinalize(x)
        else 
            Log.warn "GC found leaking commandbuffer"

    member x.Dispose() = x.Dispose(true)
    override x.Finalize() = x.Dispose(false)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and Fence internal(device : Device, signaled : bool) =
    static let infinite = System.UInt64.MaxValue

    let pFence : nativeptr<VkFence> = NativePtr.alloc 1

    do 
        native {
            let! pInfo =
                VkFenceCreateInfo(
                    (if signaled then VkFenceCreateFlags.SignaledBit else VkFenceCreateFlags.None)
                )
            VkRaw.vkCreateFence(device.Handle, pInfo, NativePtr.zero, pFence)
                |> check "could not create fence"
        }

    member x.Device = device
    member x.Handle = NativePtr.read pFence

    static member WaitAll(fences : Fence[]) =
        if fences.Length > 0 then
            let device = fences.[0].Device
            let pFences = NativePtr.stackalloc fences.Length
            for i in 0 .. fences.Length - 1 do
                NativePtr.set pFences i fences.[i].Handle

            VkRaw.vkWaitForFences(device.Handle, uint32 fences.Length, pFences, 1u,  infinite)
                |> check "failed to wait for fences"

    static member WaitAny(fences : Fence[]) =
        if fences.Length > 0 then
            let device = fences.[0].Device
            let pFences = NativePtr.stackalloc fences.Length
            for i in 0 .. fences.Length - 1 do
                NativePtr.set pFences i fences.[i].Handle

            VkRaw.vkWaitForFences(device.Handle, uint32 fences.Length, pFences, 0u,  infinite)
                |> check "failed to wait for fences"

    member x.Signaled =
        let handle = NativePtr.read pFence
        if handle.IsValid then
            VkRaw.vkGetFenceStatus(device.Handle, handle) = VkResult.VkSuccess
        else
            true

    member x.Completed =
        let handle = NativePtr.read pFence
        if handle.IsValid then
            VkRaw.vkGetFenceStatus(device.Handle, handle) <> VkResult.VkNotReady
        else
            true

    member x.Reset() =
        let handle = NativePtr.read pFence
        if handle.IsValid then
            VkRaw.vkResetFences(device.Handle, 1u, pFence)
                |> check "failed to reset fence"
        else
            failf "cannot reset disposed fence"


    member x.TryWait(timeoutInNanoseconds : int64) =
        let waitResult = VkRaw.vkWaitForFences(device.Handle, 1u, pFence, 1u, uint64 timeoutInNanoseconds)
        match waitResult with
            | VkResult.VkTimeout -> 
                false
            | VkResult.VkSuccess -> 
                true
            | err -> 
                failf "could not wait for fences: %A" err
    
    member x.TryWait() = x.TryWait(-1L)

    member x.Dispose() =
        if not (NativePtr.isNull pFence) then
            let handle = NativePtr.read pFence
            if handle.IsValid then
                VkRaw.vkDestroyFence(device.Handle, handle, NativePtr.zero)
                NativePtr.write pFence VkFence.Null
            NativePtr.free pFence

    member x.Wait(timeoutInNanoseconds : int64) = 
        if not (x.TryWait(timeoutInNanoseconds)) then
            raise <| TimeoutException("Fence")

    member x.Wait() = 
        if not (x.TryWait()) then
            raise <| TimeoutException("Fence")


    new(device : Device) = new Fence(device, false)

and Semaphore internal(device : Device) =


    let mutable handle = 
        let info = VkSemaphoreCreateInfo.Empty

        info |> pin (fun pInfo ->
            temporary<VkSemaphore, VkSemaphore> (fun pHandle ->
                VkRaw.vkCreateSemaphore(device.Handle, pInfo, NativePtr.zero, pHandle)
                    |> check "could not create semaphore"
                NativePtr.read pHandle
            )
        )

    member x.Device = device
    member x.Handle = handle

    member x.Set() =
        if handle.IsValid then
            device.GraphicsFamily.RunSynchronously(QueueCommand.Submit([], [x], []))
        else
            failf "cannot signal disposed fence" 

    member x.Dispose() =
        if handle.IsValid && device.Handle <> 0n then
            VkRaw.vkDestroySemaphore(device.Handle, handle, NativePtr.zero)
            handle <- VkSemaphore.Null

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and Event internal(device : Device) =

    let mutable handle =
        let info = VkEventCreateInfo.Empty

        info |> pin (fun pInfo ->
            temporary<VkEvent, VkEvent> (fun pHandle ->
                VkRaw.vkCreateEvent(device.Handle, pInfo, NativePtr.zero, pHandle)
                    |> check "could not create event"
                NativePtr.read pHandle
            )
        )

    member x.Device = device
    member x.Handle = handle

    member x.IsSet =
        if handle.IsValid then
            let res = VkRaw.vkGetEventStatus(device.Handle, handle)
            if res = VkResult.VkEventSet then true
            elif res = VkResult.VkEventReset then false
            else failf "could not get event status"
        else
            failf "could not get event status"
           
    member x.Set() =
        VkRaw.vkSetEvent(device.Handle, handle)
            |> check "could not set event"
           
    member x.Reset() =
        VkRaw.vkResetEvent(device.Handle, handle)
            |> check "could not set event"
            
    member x.Dispose() =
        if handle.IsValid && device.Handle <> 0n then
            VkRaw.vkDestroyEvent(device.Handle, handle, NativePtr.zero)
            handle <- VkEvent.Null

    interface IDisposable with
        member x.Dispose() = x.Dispose()


and DeviceHeap internal(device : Device, physical : PhysicalDevice, memory : MemoryInfo, heap : MemoryHeapInfo) as this =
    let hostVisible = memory.flags |> MemoryFlags.hostVisible
    let manager = DeviceMemoryManager(this, heap.Capacity.Bytes, 128L <<< 20)
    let mask = 1u <<< memory.index

    let maxAllocationSize = physical.MaxAllocationSize

    let createNullPtr() =

        let info =
            VkMemoryAllocateInfo(
                16UL,
                uint32 memory.index
            )

        let mem = 
            info |> pin (fun pInfo ->
                temporary<VkDeviceMemory, VkDeviceMemory> (fun pHandle ->
                    VkRaw.vkAllocateMemory(device.Handle, pInfo, NativePtr.zero, pHandle)
                        |> check "could not 'allocate' null pointer for device heap"
                    NativePtr.read pHandle
                )
            )

        let hostPtr = 
            if hostVisible then
                temporary<nativeint, nativeint> (fun pPtr ->
                    VkRaw.vkMapMemory(device.Handle, mem, 0UL, 16UL, VkMemoryMapFlags.MinValue, pPtr)
                        |> check "could not map memory"
                    NativePtr.read pPtr
                )
            else
                0n

        new DeviceMemory(this, mem, 0L, hostPtr)

    let mutable nullptr = None

    member x.AllocatedMemory = manager.AllocatedMemory
    member x.UsedMemory = manager.UsedMemory
    member x.Device = device
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

    member x.Alloc(align : int64, size : int64) = manager.Alloc(align, size)
    member x.Free(ptr : DevicePtr) = ptr.Dispose()


    member x.TryAllocRaw(size : int64, [<Out>] ptr : byref<DeviceMemory>) =
        if size > maxAllocationSize then
            false
        else
            if heap.TryAdd size then
                let info =
                    VkMemoryAllocateInfo(
                        uint64 size,
                        uint32 memory.index
                    )

                let mem =
                    info |> pin (fun pInfo ->
                        temporary<VkDeviceMemory, VkDeviceMemory> (fun pHandle ->
                            VkRaw.vkAllocateMemory(device.Handle, pInfo, NativePtr.zero, pHandle)
                                |> check "could not allocate memory"
                            NativePtr.read pHandle
                        )
                    )
            
                let hostPtr = 
                    if hostVisible then
                        temporary<nativeint, nativeint> (fun pPtr ->
                            VkRaw.vkMapMemory(device.Handle, mem, 0UL, uint64 size, VkMemoryMapFlags.MinValue, pPtr)
                                |> check "could not map memory"
                            NativePtr.read pPtr
                        )
                    else
                        0n


                ptr <- new DeviceMemory(x, mem, size, hostPtr)
                true
            else
                false

    member x.AllocRaw(size : int64) =
        if size > maxAllocationSize then
            failf "could not allocate %A (exceeds MaxAllocationSize: %A)" (Mem size) (Mem maxAllocationSize)
        else
            match x.TryAllocRaw size with
                | (true, ptr) -> ptr
                | _ -> failf "could not allocate %A (only %A available)" (Mem size) heap.Available
            
    member x.TryAllocRaw(mem : Mem, [<Out>] ptr : byref<DeviceMemory>) = x.TryAllocRaw(mem.Bytes, &ptr)
    member x.TryAllocRaw(mem : VkDeviceSize, [<Out>] ptr : byref<DeviceMemory>) = x.TryAllocRaw(int64 mem, &ptr)
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
            )

    member x.Dispose() =
        match nullptr with
            | Some ptr -> 
                VkRaw.vkFreeMemory(device.Handle, ptr.Handle, NativePtr.zero)
                nullptr <- None
            | None -> ()
        
    member x.Clear() =

        match nullptr with
            | Some ptr -> 
                VkRaw.vkFreeMemory(device.Handle, ptr.Handle, NativePtr.zero)
                nullptr <- None
            | None -> ()

        manager.Clear()

    member x.Copy() = new DeviceHeap(device, physical, memory, heap)

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

    member x.TryGetAligned(align : int64, size : int64) =
        let min = new DeviceBlock(Unchecked.defaultof<_>, Unchecked.defaultof<_>, -1L, size, false, null, null)
        let view = store.GetViewBetween(min, null)

        let res = 
            view |> Seq.tryFind (fun b ->
                let o = next align b.Offset
                let s = b.Size - (o - b.Offset)
                s >= size
            )

        match res with
            | Some res -> 
                store.Remove res |> ignore
                Some res
            | None ->
                None

    member x.Insert(b : DeviceBlock) =
        store.Add b |> ignore

    member x.Remove(b : DeviceBlock) =
        store.Remove b |> ignore

    member x.Clear() =
        store.Clear()

and DeviceMemoryManager internal(heap : DeviceHeap, virtualSize : int64, blockSize : int64) =
    static let next (align : int64) (v : int64) =
        if v % align = 0L then v
        else v + (align - v % align)    
  
    let free = DeviceFreeList()
    let blocks = System.Collections.Generic.HashSet<DeviceMemory>()
    let mutable allocatedMemory = 0L
    let mutable usedMemory = 0L

    let addBlock(this : DeviceMemoryManager) =
        let store = heap.AllocRaw blockSize


        Interlocked.Add(&allocatedMemory, blockSize) |> ignore
        blocks.Add store |> ignore

        let block = new DeviceBlock(this, store, 0L, blockSize, true, null, null)
        free.Insert(block)

    member x.AllocatedMemory = Mem allocatedMemory
    member x.UsedMemory = Mem usedMemory

    member x.Alloc(align : int64, size : int64) =
        if size <= 0L then
            DevicePtr.Null
        elif size >= blockSize then
            let mem = heap.AllocRaw(size)
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
                match free.TryGetAligned(align, size) with
                    | Some b ->
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

                    | None ->
                        addBlock x
                        x.Alloc(align, size)
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



                if isFirst && isLast then
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
            

and DeviceMemory internal(heap : DeviceHeap, handle : VkDeviceMemory, size : int64, hostPtr : nativeint) =
    inherit DevicePtr(Unchecked.defaultof<_>, 0L, size)
    static let nullptr = new DeviceMemory(Unchecked.defaultof<_>, VkDeviceMemory.Null, 0L, 0n)

    let mutable handle = handle
    let mutable size = size

    static member Null = nullptr

    member x.Heap = heap

    member x.Handle
        with get() : VkDeviceMemory = handle
        and internal set h = handle <- h

    member x.Size
        with get() : int64 = size
        and internal set s = size <- s

    member x.IsNull = handle.IsNull
    member x.IsValid = handle.IsValid

    member x.HostPointer = hostPtr

    override x.Dispose() = heap.Free(x)
    override x.Memory = x
    override x.Device = heap.Device

and [<AllowNullLiteral>] DevicePtr internal(memory : DeviceMemory, offset : int64, size : int64) =
    let mutable size = size
    let mutable offset = offset

    static let nullptr = lazy (new DevicePtr(DeviceMemory.Null, 0L, 0L))
    static member Null = nullptr.Value

    abstract member Memory : DeviceMemory
    default x.Memory = memory

    abstract member Dispose : unit -> unit
    default x.Dispose() = ()

    abstract member Device : Device
    default x.Device : Device = memory.Device

    abstract member TryResize : int64 -> bool
    default x.TryResize (s : int64) = s = size

    member x.Offset
        with get() = offset
        and internal set o = offset <- o

    member x.Size
        with get() = size
        and internal set s = size <- s

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
            let device = memory.Heap.Device
            Monitor.Enter x
            try
                let ptr = memory.HostPointer + nativeint x.Offset
                f ptr
            finally 
                if not memory.Heap.IsHostCoherent then
                    let range = VkMappedMemoryRange(memory.Handle, uint64 x.Offset, uint64 x.Size)
                    range |> pin (fun pRange ->
                        VkRaw.vkFlushMappedMemoryRanges(device.Handle, 1u, pRange)
                            |> check "could not flush memory range"
                    )

                Monitor.Exit x
        else
            failf "cannot map host-invisible memory"



and ICommand =
    abstract member Compatible : QueueFlags
    abstract member TryEnqueue : CommandBuffer * byref<Disposable> -> bool

and [<Obsolete>] IQueueCommand =
    abstract member Compatible : QueueFlags
    abstract member TryEnqueue : queue : DeviceQueue * waitFor : list<Semaphore> * disp : byref<Disposable> * Option<Semaphore> * Option<Fence> -> bool

and ImageBind =
    {
        image : VkImage
        level : int
        slice : int
        offset : V3i
        size : V3i
        mem : DevicePtr
    }

and BufferBind =
    {
        buffer : VkBuffer
        offset : int64
        size : int64
        mem : DevicePtr
    }

and QueueCommand =
    | Submit of waitFor : list<Semaphore> * signal : list<Semaphore> * cmds : list<CommandBuffer>
    | BindSparse of imageBinds : list<ImageBind> * bufferBinds : list<BufferBind>
    | AcquireNextImage of swapchain : VkSwapchainKHR * buffer : ref<uint32>
    | Present of swapchain : VkSwapchainKHR * buffer : ref<uint32>
        
    | ExecuteCommand of waitFor : list<Semaphore> * signal : list<Semaphore> * cmd : ICommand
    | Atomically of children : list<QueueCommand>
    | Custom of (DeviceQueue -> Fence -> unit)

and DeviceTask(parent : DeviceQueueThread, priority : int) =
    let lockObj = obj()
    [<VolatileField>]
    let mutable status = 0
    let mutable ex : exn = null
    let mutable conts = []
    let mutable kill = []
    let mutable tcs : System.Threading.Tasks.TaskCompletionSource<unit> = null

    static let finished : DeviceTask =
        let t = DeviceTask(Unchecked.defaultof<_>, 0)
        t.SetStarted()
        t.SetFinished()
        t

    static member Finished : DeviceTask = finished

    static member internal CreateFaulted (e : exn) =
        let t = DeviceTask(Unchecked.defaultof<_>)
        t.SetStarted()
        t.SetFaulted(e)
        t

    member internal x.SetStarted() =
        lock lockObj (fun () ->
            if status <> 0 then Log.error "started task that is not in initial state"
            status <- 1
            Monitor.PulseAll lockObj
        )
            
    member internal x.SetFinished() =
        let conts = 
            lock lockObj (fun () ->
                if status <> 1 then Log.error "finished task that is not in running state"
                status <- 2
                Monitor.PulseAll lockObj
                let r = conts
                conts <- []

                for k in kill do k()
                kill <- []

                if not (isNull tcs) then tcs.SetResult()

                r
            )
        for (p,c,t) in conts do parent.Enqueue(p, c, t)

    member internal x.SetFaulted(e : exn) =
        let conts = 
            lock lockObj (fun () ->
                if status <> 1 then Log.error "faulted task that is not in running state"
                status <- 3
                ex <- e
                Monitor.PulseAll lockObj
                let r = conts
                conts <- []

                for k in kill do k()
                kill <- []
                if not (isNull tcs) then tcs.SetException(e)
                r
            )
        for (p,c,t) in conts do t.SetFaulted e

    member x.Wait() =
        lock lockObj (fun () ->
            while status < 2 do
                Monitor.Wait lockObj |> ignore
        )
        
    member x.IsRunning =
        lock lockObj (fun () ->
            status = 1
        )

    member x.IsFaulted =
        lock lockObj (fun () ->
            status = 3
        )

    member x.IsCompleted =
        lock lockObj (fun () ->
            status = 2
        )

    member x.Exception = ex

    member x.AsTask =
        lock lockObj (fun () ->
            if isNull tcs then
                tcs <- System.Threading.Tasks.TaskCompletionSource()
                if status = 3 then tcs.SetException ex
                elif status = 2 then tcs.SetResult ()
                tcs.Task
            else
                tcs.Task
        )

    member x.AddCleanup(f : unit -> unit) =
        lock lockObj (fun () ->
            kill <- f :: kill
        )
        

    member x.ContinueWith (priority : int, cmd : QueueCommand) =
        lock lockObj (fun () ->
            if status < 2 then
                let task = DeviceTask(parent, priority)
                conts <- (priority, cmd, task) :: conts
                task

            elif status = 2 then
                parent.Enqueue(priority,cmd)

            else (* status = 3 *) 
                DeviceTask.CreateFaulted(ex)
        )

    member x.ContinueWith (cmd : QueueCommand) =
        x.ContinueWith(priority, cmd)

and DeviceQueueThread(family : DeviceQueueFamily) =
    
    let mutable running = true
    
    let pending = SortedDictionary<int, Queue<QueueCommand * DeviceTask>>()

    let enqueue (priority : int) (item : QueueCommand) (task : DeviceTask) =
        match pending.TryGetValue priority with
            | (true, queue) -> 
                queue.Enqueue (item, task)
            | _ -> 
                let q = Queue()
                pending.[priority] <- q
                q.Enqueue (item, task)


    let dequeue () =
        if pending.Count > 0 then
            let (KeyValue(p, q)) = pending |> Seq.head
            let (item, task) = q.Dequeue()
            if q.Count = 0 then
                pending.Remove p |> ignore
            p, item, task
        else
            failwith "empty queue"

    let submit (queue : DeviceQueue) (waitFor : list<Semaphore>) (signal : list<Semaphore>) (cmds : list<CommandBuffer>) (fence : Fence) =
        let device = queue.Device
        let waitFor = waitFor |> List.map (fun s -> s.Handle) |> List.toArray
        let masks = Array.init waitFor.Length (fun _ -> int VkPipelineStageFlags.TopOfPipeBit)
        let signal = signal |> List.map (fun s -> s.Handle) |> List.toArray
        let cmds = cmds |> List.map (fun cmd -> cmd.Handle) |> List.toArray
        let fence = fence.Handle

        native {
            let! pSignal = signal
            let! pMasks = masks
            let! pWaitFor = waitFor
            let! pCmds = cmds

            if device.IsDeviceGroup then
                let! pCmdMasks = cmds |> Array.map (fun _ -> device.AllMask)
                let mutable mask = device.AllMask

                let waitCount, pWaitIndices =
                    if waitFor.Length > 0 then device.AllCount, device.AllIndices
                    else 0u, NativePtr.zero
                    
                let signalCount, pSignalIndices =
                    if waitFor.Length > 0 then device.AllCount, device.AllIndices
                    else 0u, NativePtr.zero
                    
                let! pExt = 
                    [|
                        VkDeviceGroupSubmitInfo(
                            waitCount, pWaitIndices,
                            uint32 cmds.Length, pCmdMasks,
                            signalCount, pSignalIndices
                        )
                    |]
                    
                let! pSubmit = 
                    [|
                        VkSubmitInfo(
                            NativePtr.toNativeInt pExt,
                            uint32 waitFor.Length, pWaitFor, NativePtr.cast pMasks,
                            uint32 cmds.Length, pCmds,
                            uint32 signal.Length, pSignal
                        )
                    |]

                VkRaw.vkQueueSubmit(queue.Handle, 1u, pSubmit, fence)
                    |> check "could not submit command buffer"

            else
                let! pSubmit = 
                    [|
                        VkSubmitInfo(
                            uint32 waitFor.Length, pWaitFor, NativePtr.cast pMasks,
                            uint32 cmds.Length, pCmds,
                            uint32 signal.Length, pSignal
                        )
                    |]

                VkRaw.vkQueueSubmit(queue.Handle, 1u, pSubmit, fence)
                    |> check "could not submit command buffer"
        }

    let bindSparse (queue : DeviceQueue) (imageBinds : list<ImageBind>) (bufferBinds : list<BufferBind>) (fence : Fence) =
        let device = queue.Device

        let imageBinds = imageBinds |> List.toArray
        let imageBindsNative =
            imageBinds |> Array.map (fun b ->
                VkSparseImageMemoryBind(
                    VkImageSubresource(VkImageAspectFlags.ColorBit, uint32 b.level, uint32 b.slice),
                    VkOffset3D(b.offset.X, b.offset.Y, b.offset.Z),
                    VkExtent3D(b.size.X, b.size.Y, b.size.Z),
                    b.mem.Memory.Handle,
                    uint64 b.mem.Offset,
                    VkSparseMemoryBindFlags.None
                )
            )  
                
        let bufferBinds = bufferBinds |> List.toArray
        let bufferBindsNative =
            bufferBinds |> Array.map (fun b ->
                VkSparseMemoryBind(
                    uint64 b.offset,
                    uint64 b.size,
                    b.mem.Memory.Handle,
                    uint64 b.mem.Offset,
                    VkSparseMemoryBindFlags.None
                )
            )

        native { 
            let! pImageBinds = imageBindsNative
            let! pBufferBinds = bufferBindsNative

            let! pImageBindInfos =
                Array.init imageBinds.Length (fun bi ->
                    VkSparseImageMemoryBindInfo(
                        imageBinds.[bi].image,
                        1u, NativePtr.add pImageBinds bi
                    )
                )  

            let! pBufferBindInfos =
                Array.init bufferBinds.Length (fun bi ->
                    VkSparseBufferMemoryBindInfo(
                        bufferBinds.[bi].buffer,
                        1u, NativePtr.add pBufferBinds bi
                    )
                )

            if device.IsDeviceGroup then
                let deviceCount = int device.AllCount

                let! pGroupInfos = 
                    Array.init deviceCount (fun di ->
                        VkDeviceGroupBindSparseInfo(
                            uint32 di, uint32 di
                        )
                    )

                let! pBindInfos =
                    Array.init deviceCount (fun di ->
                        let next = NativePtr.add pGroupInfos di
                        VkBindSparseInfo(
                            NativePtr.toNativeInt next,
                            0u, NativePtr.zero,
                            uint32 bufferBinds.Length, pBufferBindInfos,
                            0u, NativePtr.zero,
                            uint32 imageBinds.Length, pImageBindInfos,
                            0u, NativePtr.zero
                        )
                    )

                VkRaw.vkQueueBindSparse(queue.Handle, uint32 deviceCount, pBindInfos, fence.Handle)
                    |> check "could not bind sparse memory"

            else
                let bindInfo =
                    VkBindSparseInfo(
                        0u, NativePtr.zero,
                        uint32 bufferBinds.Length, pBufferBindInfos,
                        0u, NativePtr.zero,
                        uint32 imageBinds.Length, pImageBindInfos,
                        0u, NativePtr.zero
                    )
                bindInfo |> pin (fun pInfo ->
                    VkRaw.vkQueueBindSparse(queue.Handle, 1u, pInfo, fence.Handle)
                        |> check "could not bind sparse memory"
                )
        }

    let rec acquireNextImage (queue : DeviceQueue) (swapchain : VkSwapchainKHR) (buffer : ref<uint32>) (fence : Fence) =
        let res =
            native {
                let arr = [| !buffer |]
                let! pArr = arr
                let res = VkRaw.vkAcquireNextImageKHR(queue.Device.Handle, swapchain, ~~~0UL, VkSemaphore.Null, fence.Handle, pArr)
                buffer := arr.[0]
                return res
            }
        if res <> VkResult.VkSuccess then
            System.Diagnostics.Debugger.Launch() |> ignore
            acquireNextImage queue swapchain buffer fence
            //|> check "could not acquire Swapchain Image"

    let present (queue : DeviceQueue) (swapchain : VkSwapchainKHR) (buffer : ref<uint32>) =
        native {
            let arr = [| !buffer |]
            let! pHandle = [| swapchain |]
            let! pArr = arr
            let! pResult = [| VkResult.VkSuccess |]

            let! pInfo =
                [|
                    VkPresentInfoKHR(
                        0u, NativePtr.zero,
                        1u, pHandle,
                        pArr,
                        pResult
                    )
                |]
                
            VkRaw.vkQueuePresentKHR(queue.Handle, pInfo) 
                |> check "could not acquire image"

            VkRaw.vkQueueWaitIdle(queue.Handle)
                |> check "could not wait for queue"
            buffer := arr.[0]
        }



    let rec perform (queue : DeviceQueue) (pool : CommandPool) (cmd : QueueCommand) (fence : Fence) =
        match cmd with
            | QueueCommand.Custom action ->
                fence.Reset()
                action queue fence

            | QueueCommand.Submit(waitFor, signal, cmds) ->
                fence.Reset()
                submit queue waitFor signal cmds fence
                fence.Wait()

            | QueueCommand.BindSparse (imageBinds, bufferBinds) ->
                fence.Reset()
                bindSparse queue imageBinds bufferBinds fence
                fence.Wait()

            | QueueCommand.AcquireNextImage(chain, buffer) ->
                fence.Reset()
                acquireNextImage queue chain buffer fence
                fence.Wait()

            | QueueCommand.Present(chain, buffer) ->
                present queue chain buffer

            | QueueCommand.ExecuteCommand(waitFor, signal, cmd) ->
                let buffer = pool.CreateCommandBuffer(CommandBufferLevel.Primary)
                buffer.Begin(CommandBufferUsage.OneTimeSubmit)

                let mutable disp = Disposable.Empty
                if cmd.TryEnqueue(buffer, &disp) then
                    buffer.End()
                    fence.Reset()
                    submit queue waitFor signal [buffer] fence
                    fence.Wait()

                if not (isNull disp) then disp.Dispose()
                buffer.Dispose()

            | QueueCommand.Atomically many ->
                Log.warn "atomic"
                for m in many do perform queue pool m fence
                    

        ()

    let rec toString (cmd : QueueCommand) =
        match cmd with
            | QueueCommand.AcquireNextImage _ -> "AcquireNextImage"
            | QueueCommand.Atomically l -> l |> List.map toString |> String.concat "; " |> sprintf "[%s]"
            | QueueCommand.BindSparse _ -> "BindSparse"
            | QueueCommand.Present _ -> "Present"
            | QueueCommand.Submit(_,_,cmd) -> sprintf "Submit%d" (List.length cmd)
            | QueueCommand.Custom _ -> "Custom"
            | ExecuteCommand _ -> "Execute"

    let run (queue : DeviceQueue) () =
        let device = queue.Device
        let pool = queue.Family.CreateCommandPool(CommandPoolFlags.Transient)
        let fence = device.CreateFence()
        try
            while running do
                let priority, item, tcs =
                    lock pending (fun () ->
                        while pending.Count <= 0 && running do
                            Monitor.Wait pending |> ignore
                        if not running then raise <| OperationCanceledException()
                        dequeue()
                    )


                tcs.SetStarted()
                try
                    perform queue pool item fence
                    tcs.SetFinished ()
                with
                    e -> tcs.SetFaulted e
                    
        with 
            | :? OperationCanceledException -> ()
            | e -> Log.error "[Vulkan] DeviceQueueThread faulted: %A" e

        fence.Dispose()
        pool.Dispose()

    let threads = 
        family.Queues |> List.map (fun q -> 
            let thread = Thread(ThreadStart(run q), IsBackground = true)
            thread.Start()
            thread
        )

    member x.Cancel() =
        if running then
            running <- false
            lock pending (fun () ->
                Monitor.PulseAll pending
            )
            for t in threads do t.Join()

    member internal x.Enqueue(priority : int, item : QueueCommand, task : DeviceTask) =
        lock pending (fun () ->
            if task.IsCompleted || task.IsFaulted || task.IsRunning then Log.error "bad task"
            enqueue priority item task
            Monitor.PulseAll pending
        )

    member x.Enqueue(priority : int, item : QueueCommand) : DeviceTask =
        let task = DeviceTask(x, priority)
        x.Enqueue(priority, item, task)
        task

    member x.Enqueue(item : QueueCommand) : DeviceTask =
        x.Enqueue(3, item)



and DeviceToken(family : DeviceQueueFamily, ref : ref<Option<DeviceToken>>) =
    let mutable pool                : Option<CommandPool>   = None
    let mutable current             : Option<CommandBuffer> = None
    let disposables                 : List<Disposable>      = List()


    //let mutable isEmpty = true
    let mutable refCount = 1

    //let mutable lastTask : Option<DeviceTask> = None

//    #if DEBUG
//    let owner = Thread.CurrentThread.ManagedThreadId
//    let check() =
//        if Thread.CurrentThread.ManagedThreadId <> owner then
//            Log.warn "token accessed by different thread"
//
//    #else
//    let check () = ()
//    #endif

    let check () = ()

    let cleanup() =
        for d in disposables do d.Dispose()
        disposables.Clear()
        match current with
            | Some b -> 
                b.Dispose()
                current <- None
            | _ -> ()

        match pool with
            | Some p ->
                p.Dispose()
                pool <- None
            | _ -> ()

        refCount <- 1
        //isEmpty <- true
        ref := None

    let enqueue (buffer : CommandBuffer) (cmd : ICommand) =
        let mutable disp = Disposable.Empty
        if cmd.TryEnqueue(buffer, &disp) then
            if not (isNull disp) then disposables.Add disp
        else
            cleanup()
            failf "could not enqueue command: %A" cmd

    let flush(priority : int) =
        match current with
            | Some buffer ->
                buffer.End()
                if not buffer.IsEmpty then
                    family.RunSynchronously(priority, QueueCommand.Submit([], [], [buffer]))

                buffer.Dispose()
                current <- None

            | None ->
                ()

    let syncTask (priority : int) : DeviceTask =
        match current with
            | Some buffer ->
                current <- None
                buffer.End()
                if not buffer.IsEmpty then
                    let task = family.Start(priority, buffer)
                    task.AddCleanup buffer.Dispose
                    task
                else 
                    DeviceTask.Finished
            | None ->
                DeviceTask.Finished
        

    member x.Flush() =
        check()
        flush(3)

    member x.Sync(priority : int) =
        check()
        flush(priority)

    member x.Sync() = x.Sync(3)

    member x.SyncTask(priority : int) =
        check()
        syncTask priority


    member x.AddCleanup(f : unit -> unit) =
        check()
        disposables.Add { new Disposable() with member x.Dispose() = f() }

    member x.Enqueue (cmd : ICommand) =
        check()
        match current with
            | Some buffer -> 
                enqueue buffer cmd
                
            | None ->
                
                let pool =
                    match pool with
                        | Some p -> p
                        | None ->
                            let p = family.TakeCommandPool()
                            pool <- Some p
                            p

                let buffer = pool.CreateCommandBuffer CommandBufferLevel.Primary
                buffer.Begin CommandBufferUsage.OneTimeSubmit
                current <- Some buffer
                enqueue buffer cmd

    member x.Enqueue (cmd : QueueCommand) =
        check()
        flush(3)

        family.RunSynchronously cmd
//        let task =
//            match lastTask with
//                | Some t -> t.ContinueWith cmd
//                | None -> family.Start(cmd)
//
//        lastTask <- Some task


    member internal x.AddRef() = 
        check()
        refCount <- refCount + 1

    member internal x.RemoveRef(priority : int) = 
        check()
        refCount <- refCount - 1
        if refCount = 0 then 
            ref := None
            x.Sync(priority)
            cleanup()
            
    member x.Dispose(priority : int) =
        check()
        x.RemoveRef(priority)

    member x.Dispose() =
        check()
        x.RemoveRef(3)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AbstractClass; Sealed; Extension>]
type DeviceExtensions private() =

    static let rec tryFindMemory (bits : uint32) (i : int)  (memories : DeviceHeap[]) =
        if i >= memories.Length then
            None
        else
            let mem = memories.[i]
            if mem.Mask &&& bits <> 0u then
                Some mem
            else
                tryFindMemory bits (i + 1) memories

    static let rec tryFindDeviceMemory (bits : uint32) (i : int)  (memories : DeviceHeap[]) =
        if i >= memories.Length then
            None
        else
            let mem = memories.[i]
            if mem.Mask &&& bits <> 0u && mem.Info.flags &&& MemoryFlags.DeviceLocal <> MemoryFlags.None then
                Some mem
            else
                tryFindDeviceMemory bits (i + 1) memories

    static let rec tryAlloc (reqs : VkMemoryRequirements) (i : int) (memories : DeviceHeap[]) =
        if i >= memories.Length then
            None
        else
            let mem = memories.[i]
            if mem.Mask &&& reqs.memoryTypeBits <> 0u then
                let ptr = mem.Alloc(int64 reqs.alignment, int64 reqs.size)
                Some ptr
            else
                tryAlloc reqs (i + 1) memories

    static let rec tryAllocDevice (reqs : VkMemoryRequirements) (i : int) (memories : DeviceHeap[]) =
        if i >= memories.Length then
            None
        else
            let mem = memories.[i]
            if mem.Mask &&& reqs.memoryTypeBits <> 0u && mem.Info.flags &&& MemoryFlags.DeviceLocal <> MemoryFlags.None then
                let ptr = mem.Alloc(int64 reqs.alignment, int64 reqs.size)
                Some ptr
            else
                tryAllocDevice reqs (i + 1) memories


    [<Extension>]
    static member CreateDevice(this : PhysicalDevice, wantedExtensions : list<string>) =
        new Device(this, wantedExtensions)

    [<Extension>]
    static member GetMemory(this : Device, bits : uint32, preferDevice : bool) =
        if preferDevice then
            match tryFindDeviceMemory bits 0 this.Memories with
                 | Some mem -> mem
                 | None -> 
                    match tryFindMemory bits 0 this.Memories with
                        | Some mem -> mem
                        | None -> failf "could not find compatible memory for types: %A" bits
        else
            match tryFindMemory bits 0 this.Memories with
                | Some mem -> mem
                | None -> failf "could not find compatible memory for types: %A" bits

    [<Extension>]
    static member Alloc(this : Device, reqs : VkMemoryRequirements, preferDevice : bool) =
        if preferDevice then
            match tryAllocDevice reqs 0 this.Memories with
                 | Some mem -> mem
                 | None -> 
                    match tryAlloc reqs 0 this.Memories with
                        | Some mem -> mem
                        | None -> failf "could not find compatible memory for %A" reqs
        else
            match tryAlloc reqs 0 this.Memories with
                | Some mem -> mem
                | None -> failf "could not find compatible memory for %A" reqs

    [<Extension>]
    static member GetMemory(this : Device, bits : uint32) =
        DeviceExtensions.GetMemory(this, bits, false)

    [<Extension>]
    static member Alloc(this : Device, reqs : VkMemoryRequirements) =
        DeviceExtensions.Alloc(this, reqs, false)

