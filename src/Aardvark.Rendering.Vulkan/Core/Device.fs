namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic
open System.Collections.Concurrent
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Rendering
open KHRSwapchain
open KHRSurface
open KHRBufferDeviceAddress
open EXTMemoryBudget
open Vulkan11

#nowarn "9"
#nowarn "51"

[<AutoOpen>]
module internal NativeMemoryHandles =

    module private Win32Handle =
        open KHRExternalMemoryWin32

        let getMemoryHandle (device : VkDevice) (memory : VkDeviceMemory) =
            let handle =
                native {
                    let! pHandle = 0n
                    let! pInfo = VkMemoryGetWin32HandleInfoKHR(memory, VkExternalMemoryHandleTypeFlags.OpaqueWin32Bit)

                    VkRaw.vkGetMemoryWin32HandleKHR(device, pInfo, pHandle)
                        |> check "could not create shared handle"

                    return !!pHandle
                }

            new Win32Handle(handle) :> IExternalMemoryHandle

    module private PosixHandle =
        open KHRExternalMemoryFd

        let getMemoryHandle (device : VkDevice) (memory : VkDeviceMemory) =
            let handle =
                native {
                    let! pHandle = 0
                    let! pInfo = VkMemoryGetFdInfoKHR(memory, VkExternalMemoryHandleTypeFlags.OpaqueFdBit)

                    VkRaw.vkGetMemoryFdKHR(device, pInfo, pHandle)
                        |> check "could not create shared handle"

                    return !!pHandle
                }

            new PosixHandle(handle) :> IExternalMemoryHandle

    module ExternalMemory =

        let Extension =
            if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                KHRExternalMemoryWin32.Name
            else
                KHRExternalMemoryFd.Name

        let ofDeviceMemory (device : VkDevice) (memory : VkDeviceMemory) =
            if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                Win32Handle.getMemoryHandle device memory
            else
                PosixHandle.getMemoryHandle device memory

[<RequireQualifiedAccess>]
type UploadMode =
    | Sync
    | Async

type Device internal(dev : PhysicalDevice, wantedExtensions : list<string>) as this =
    let isGroup, deviceGroup =
        match dev with
            | :? PhysicalDeviceGroup as g -> true, g.Devices
            | _ -> false, [| dev |]


    let physical = deviceGroup.[0]
    let instance = physical.Instance

    // Find a graphics, compute and transfer family
    let graphicsFamilyInfo, computeFamilyInfo, transferFamilyInfo =
        let sortedFamilies =
            physical.QueueFamilies
            |> Array.sortByDescending (_.flags >> QueueFlags.score)

        let findFamily excludedFlags requiredFlags =
            sortedFamilies
            |> Array.tryFind (fun qf ->
                qf.flags.HasFlag requiredFlags &&
                qf.flags &&& excludedFlags = QueueFlags.None
            )

        QueueFlags.Graphics |> findFamily QueueFlags.None,
        QueueFlags.Compute  |> findFamily QueueFlags.Graphics,
        QueueFlags.Transfer |> findFamily (QueueFlags.Graphics ||| QueueFlags.Compute)

    let queueFamilyInfos =
        [| graphicsFamilyInfo; computeFamilyInfo; transferFamilyInfo |]
        |> Array.collect Option.toArray
        |> Array.distinct

    let onDispose = Event<unit>()
    
    let mutable shaderCachePath : Option<string> =
        Some <| Path.combine [
            CachingProperties.CacheDirectory
            "Shaders"
            "Vulkan"
        ]

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

    let wantedExtensions =
        if instance.DebugConfig.DebugPrintEnabled then
            wantedExtensions @ [KHRShaderNonSemanticInfo.Name]
        else
            wantedExtensions

    let extensions =
        let availableExtensions = physical.GlobalExtensions |> Seq.map (fun e -> e.name.ToLower(), e.name) |> Dictionary.ofSeq

        let enabledExtensions =
            wantedExtensions
            |> List.filter (fun e -> dev.Instance.EnabledExtensions |> List.contains e |> not)
            |> List.choose (fun name ->
                let name = name.ToLower()
                match availableExtensions.TryGetValue name with
                | (true, realName) ->
                    VkRaw.debug "enabled device extension %A" name
                    Some realName
                | _ ->
                    VkRaw.warn "could not enable extension '%s' since it is not available" name
                    None
            )

        enabledExtensions

    let mutable isDisposed = 0

    let mutable device =
        let queuePriorities = Array.replicate 32 1.0f
        use pQueuePriorities = fixed queuePriorities

        let queueCreateInfos =
            queueFamilyInfos
            |> Array.map (fun familyInfo ->
                VkDeviceQueueCreateInfo(
                    VkDeviceQueueCreateFlags.None,
                    uint32 familyInfo.index,
                    uint32 familyInfo.count,
                    pQueuePriorities
                )
            )

        native {
            let! pQueueCreateInfos = queueCreateInfos
            let extensions = List.toArray extensions
            let! pExtensions = extensions

            let deviceHandles = deviceGroup |> Array.map (fun d -> d.Handle)
            let! pDevices = deviceHandles
            let groupInfo =
                VkDeviceGroupDeviceCreateInfo(
                    uint32 deviceGroup.Length,
                    pDevices
                )

            // TODO: Do we really want to enable all available features?
            // Does this have real performance implications?
            use pNext =
                dev.GetFeatures (fun e -> extensions |> Array.contains e)
                |> DeviceFeatures.toNativeChain
                |> if isGroup then VkStructChain.add groupInfo else id

            let! pInfo =
                VkDeviceCreateInfo(
                    pNext.Handle,
                    VkDeviceCreateFlags.None,
                    uint32 queueCreateInfos.Length, pQueueCreateInfos,
                    0u, NativePtr.zero,
                    uint32 extensions.Length, pExtensions,
                    NativePtr.zero
                )
            let! pDevice = VkDevice.Zero

            VkRaw.vkCreateDevice(physical.Handle,pInfo, NativePtr.zero, pDevice)
                |> check "could not create device"

            return !!pDevice
        }

    let queueFamilies =
        queueFamilyInfos |> Array.map (fun info ->
            new DeviceQueueFamily(physical, this, device, info)
        )

    let graphicsFamily, computeFamily, transferFamily =
        let getFamily (info: QueueFamilyInfo) =
            queueFamilies |> Array.find (fun qf -> qf.Info = info)

        graphicsFamilyInfo |> Option.map getFamily,
        computeFamilyInfo |> Option.map getFamily,
        transferFamilyInfo |> Option.map getFamily

    let pAllQueueFamilyIndices =
        if queueFamilies.Length <= 1 then
            NativePtr.zero
        else
            let ptr = NativePtr.alloc queueFamilies.Length
            for i = 0 to queueFamilies.Length - 1 do
                ptr.[i] <- uint32 queueFamilies.[i].Index
            ptr

    let sharingMode =
        if queueFamilies.Length = 1 then VkSharingMode.Exclusive
        else VkSharingMode.Concurrent

    let memories = 
        physical.MemoryTypes |> Array.mapi (fun i t ->
            let isHostMemory = (i = physical.HostMemory.index)
            new DeviceHeap(this, physical, t, t.heap, isHostMemory)
        )

    let hostMemory = memories.[physical.HostMemory.index]
    let deviceMemory = memories.[physical.DeviceMemory.index]

    let mutable runtime = Unchecked.defaultof<IRuntime>
    let memoryLimits = physical.Limits.Memory

    let caches = System.Collections.Concurrent.ConcurrentDictionary<Symbol, obj>()

    let uploadMode =
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

    member x.ShaderCachePath
        with get() = shaderCachePath
        and set p = shaderCachePath <- p

    member x.UploadMode = uploadMode

    member x.GetCache(name : Symbol) =
        let res =
            caches.GetOrAdd(name, fun name ->
                DeviceCache<'a, 'b>(x, name) :> obj
            )

        res |> unbox<DeviceCache<'a, 'b>>

    /// Gets or creates a cached resource for the cache with the given name.
    /// Cached resources are kept alive until they are removed from the cache (see Device.RemoveCached()) and
    /// all references to it have been disposed.
    member x.GetCached(cacheName : Symbol, value : 'a, create : 'a -> 'b) : 'b =
        let cache : DeviceCache<'a, 'b> = x.GetCache(cacheName)
        cache.Invoke(value, create)

    /// Removes the given resource from its cache (if it was cached).
    /// The resource is destroyed once all references have been disposed.
    member x.RemoveCached(value : #CachedResource) : unit =
        match value.Cache with
        | Some name ->
            match caches.TryGetValue(name) with
            | (true, (:? IDeviceCache<'b> as c)) -> c.Revoke value
            | (true, _) -> Log.warn "[Vulkan] Cannot remove from device cache '%A' since it is not compatible" name
            | _ -> Log.warn "[Vulkan] Cannot remove from device cache '%A' since it does not exist" name
        | _ -> ()

    member x.ComputeToken =
        x.ComputeFamily.CurrentToken

    member x.Token =
        x.GraphicsFamily.CurrentToken

    member x.Runtime
        with get() = runtime
        and internal set r = runtime <- r

    member x.QueueFamilies = queueFamilies
    
    member x.EnabledExtensions = extensions

    member x.IsExtensionEnabled(extension) =
        extensions |> List.contains extension

    /// Returns whether descriptors may be updated after being bound.
    member x.UpdateDescriptorsAfterBind =
        not <| RuntimeConfig.SuppressUpdateAfterBind &&
        x.IsExtensionEnabled EXTDescriptorIndexing.Name

    member x.MinMemoryMapAlignment = memoryLimits.MinMemoryMapAlignment
    member x.MinTexelBufferOffsetAlignment = memoryLimits.MinTexelBufferOffsetAlignment
    member x.MinUniformBufferOffsetAlignment = memoryLimits.MinUniformBufferOffsetAlignment
    member x.MinStorageBufferOffsetAlignment = memoryLimits.MinStorageBufferOffsetAlignment
    member x.BufferImageGranularity = memoryLimits.BufferImageGranularity

    member x.Instance = instance
    member x.DebugConfig = instance.DebugConfig

    member internal x.AllQueueFamiliesCnt = uint32 queueFamilies.Length
    member internal x.AllQueueFamiliesPtr = pAllQueueFamilyIndices
    member internal x.AllSharingMode = sharingMode

    member internal x.AllMask = allMask
    member x.AllCount = uint32 deviceGroup.Length
    member internal x.AllIndices = allIndices
    member x.AllIndicesArr = allIndicesArr

    member x.GraphicsFamily : DeviceQueueFamily  = 
        match graphicsFamily with
        | Some pool -> pool
        | None -> failf "the device does not support graphics-queues"

    member x.ComputeFamily : DeviceQueueFamily =
        match computeFamily with
        | Some pool -> pool
        | None -> failf "the device does not support compute-queues"

    member x.TransferFamily : DeviceQueueFamily =
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

                if queueFamilies.Length > 0 then
                    NativePtr.free pAllQueueFamilyIndices

    member x.Handle = device

    member x.PhysicalDevice = physical
    member x.PhysicalDevices = deviceGroup
    member x.IsDeviceGroup = deviceGroup.Length > 1

    member x.CreateFence(signaled : bool) = new Fence(x, signaled)
    member x.CreateFence() = new Fence(x)
    member x.CreateSemaphore() = new Semaphore(x)
    member x.CreateEvent() = new Event(x)

    member x.PrintMemoryUsage(l: ILogger) =
        let budget =
            if x.IsExtensionEnabled EXTMemoryBudget.Name then
                Some <| native {
                    let! pMemoryBudgetProps = VkPhysicalDeviceMemoryBudgetPropertiesEXT.Empty
                    let! pPhysicalDeviceMemoryProps2 = VkPhysicalDeviceMemoryProperties2(pMemoryBudgetProps.Address, VkPhysicalDeviceMemoryProperties.Empty)

                    VkRaw.vkGetPhysicalDeviceMemoryProperties2(physical.Handle, pPhysicalDeviceMemoryProps2)

                    return pMemoryBudgetProps.Value
                }
            else
                None

        for i = 0 to physical.Heaps.Length - 1 do
            let heap = physical.Heaps.[i]

            let heapFlags =
                if heap.Flags.HasFlag MemoryHeapFlags.DeviceLocalBit then $" (device local)"
                else ""

            l.section $"Heap {i}{heapFlags}" (fun _ ->
                l.line $"Capacity: {heap.Capacity}"
                l.line $"Allocated: {heap.Allocated}"
                l.line $"Available: {heap.Available}"

                budget |> Option.iter (fun b ->
                    let warning =
                        if b.heapUsage.[i] > b.heapBudget.[i] then " (!!!)"
                        else ""

                    l.line $"Budget: {Mem b.heapUsage.[i]} / {Mem b.heapBudget.[i]}{warning}"
                )
            )

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and IDeviceCache<'b> =
    abstract member Revoke : 'b -> unit

and DeviceCache<'a, 'b when 'b :> CachedResource>(device : Device, name : Symbol) =
    let store = Dict<'a, 'b>()
    let back = Dict<'b, 'a>()

    do device.OnDispose.Add(fun _ ->
            for k in back.Keys do
                if k.ReferenceCount > 1 then
                    Log.warn "[Vulkan] Cached resource %A still has %d references" k (k.ReferenceCount - 1)
                k.Dispose()
            store.Clear()
            back.Clear()
        )

    member x.Invoke(value : 'a, create : 'a -> 'b) : 'b =
        lock store (fun () ->
            let res =
                match store.TryGetValue value with
                | (true, r) -> r
                | _ ->
                    let r = create value
                    r.Cache <- Some name
                    back.[r] <- value
                    store.[value] <- r
                    r

            res.AddReference()
            res
        )

    member x.Revoke(res : 'b) : unit =
        lock store (fun () ->
            match back.TryRemove res with
            | (true, key) ->
                store.Remove key |> ignore
                res.Dispose()
            | _ ->
                Log.warn "[Vulkan] Cached resource to be removed not found"
        )

    interface IDeviceCache<'b> with
        member x.Revoke b = x.Revoke b

// TODO: The copy engine currently does not acquire references to resource handles,
// risking that resources are freed while still in use. This may lead to problems
// in some scenarios.
and [<RequireQualifiedAccess>] CopyCommand =
    internal
        | BufferToBufferCmd  of src : VkBuffer * dst : VkBuffer * info : VkBufferCopy
        | BufferToImageCmd   of src : VkBuffer * dst : VkImage * dstLayout : VkImageLayout * info : VkBufferImageCopy * size : int64
        | ImageToBufferCmd   of src : VkImage * srcLayout : VkImageLayout * dst : VkBuffer * info : VkBufferImageCopy * size : int64
        | ImageToImageCmd    of src : VkImage * srcLayout : VkImageLayout * dst : VkImage * dstLayout : VkImageLayout * info : VkImageCopy * size : int64
        | CallbackCmd        of (unit -> unit)
        | ReleaseBufferCmd   of buffer : VkBuffer * offset : int64 * size : int64 * dstQueueFamily : uint32
        | ReleaseImageCmd    of image : VkImage * range : VkImageSubresourceRange * srcLayout : VkImageLayout * dstLayout : VkImageLayout * dstQueueFamily : uint32
        | TransformLayoutCmd of image : VkImage * range : VkImageSubresourceRange * srcLayout : VkImageLayout * dstLayout : VkImageLayout

    static member TransformLayout(image : VkImage, range : VkImageSubresourceRange, srcLayout : VkImageLayout, dstLayout : VkImageLayout) =
        CopyCommand.TransformLayoutCmd(image, range, srcLayout, dstLayout)

    static member SyncImage(image : VkImage, range : VkImageSubresourceRange, layout : VkImageLayout) =
        CopyCommand.TransformLayoutCmd(image, range, layout, layout)

    static member Copy(src : VkBuffer, srcOffset : int64, dst : VkBuffer, dstOffset : int64, size : int64) =
        CopyCommand.BufferToBufferCmd(
            src, 
            dst, 
            VkBufferCopy(uint64 srcOffset, uint64 dstOffset, uint64 size)
        )

    static member Copy(src : VkBuffer, dst : VkImage, dstLayout : VkImageLayout, format : VkFormat, info : VkBufferImageCopy) =
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

            let copies, enq, totalSize =
                lock lockObj (fun () ->

                    if not running then
                        empty, 0L, 0L
                    else
                        while pending.Count = 0 && running do Monitor.Wait lockObj |> ignore
                        if not running then
                            empty, 0L, 0L
                        elif totalSize >= 0L then
                            let mine = pending
                            let s = totalSize
                            pending <- List()
                            totalSize <- 0L
                            mine, vEnqueue, s
                        else
                            empty, 0L, 0L
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

                stream.Run cmd.Handle
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
        let count = 1

        Array.init count (fun i ->
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

                let s = commands |> Seq.fold (fun s c -> s + c.SizeInBytes) 0L
                totalSize <- totalSize + s

                Monitor.PulseAll lockObj
                s
            )

        if size > 0L then () // trigger.Signal()

    /// Enqueues the commands and waits for them to be submitted.
    member x.EnqueueSafe(commands : seq<CopyCommand>) =
        let enq, size =
            lock lockObj (fun () ->
                vEnqueue <- vEnqueue + 1L
                pending.AddRange commands

                let s = commands |> Seq.fold (fun s c -> s + c.SizeInBytes) 0L
                totalSize <- totalSize + s

                Monitor.PulseAll lockObj

                vEnqueue, s
            )

        lock enqueueMon (fun () -> 
            while vDone < enq do
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

and [<AbstractClass>] CachedResource =
    class
        inherit Resource
        val mutable private cache : Symbol option

        member x.Cache
            with get () = x.cache
            and set (value) = x.cache <- value

        new(device : Device) = { inherit Resource(device); cache = None }
        new(device : Device, cache : Symbol) = { inherit Resource(device); cache = Some cache }
    end

and [<AbstractClass>] Resource =
    class
        val public Device : Device
        val mutable private refCount : int

        member x.ReferenceCount =
            x.refCount

        /// Increments the reference count only if it greater than zero.
        /// Returns if the reference count was incremented.
        member x.TryAddReference() =
            let mutable current = x.refCount

            while current > 0 && Interlocked.CompareExchange(&x.refCount, current + 1, current) <> current do
                current <- x.refCount

            current > 0

        member x.AddReference() =
            Interlocked.Increment(&x.refCount) |> ignore

        member x.Dispose() =
            let refs = Interlocked.Decrement(&x.refCount)
            if refs < 0 then
                Log.warn $"[Vulkan] Resource {x} has negative reference count ({refs})"
            elif refs = 0 then
                x.Destroy()

        abstract member IsValid : bool
        default x.IsValid =
            not x.Device.IsDisposed && x.refCount > 0

        abstract member Destroy : unit -> unit

        new(device : Device) = { Device = device; refCount = 1 }
        new(device : Device, refCount : int) = { Device = device; refCount = refCount }

        interface ICommandResource with
            member x.AddReference() = x.AddReference()
            member x.Dispose() = x.Dispose()
    end

and [<AbstractClass>] Resource<'T when 'T : unmanaged and 'T : equality> =
    class
        inherit Resource
        val mutable private handle : 'T

        member x.Handle
            with get () = x.handle
            and set (value) = x.handle <- value

        override x.IsValid =
            base.IsValid && x.handle <> Unchecked.defaultof<_>

        new(device : Device, handle : 'T, refCount : int) =
            handle |> NativePtr.pin device.Instance.RegisterDebugTrace
            { inherit Resource(device, refCount); handle = handle }

        new(device : Device, handle : 'T) =
            new Resource<_>(device, handle, 1)
    end

/// Represents a running device operation that can be waited on.
and DeviceTask internal (fence: Fence) =
    let lockObj = obj()
    let mutable fence = fence
    let mutable onCompleted = if isNull fence then null else ResizeArray<unit -> unit>()

    let finalize() =
        for a in onCompleted do a()
        onCompleted <- null
        fence.Dispose()
        fence <- null

    static let completed = new DeviceTask(null)
    static member Completed = completed

    member x.IsCompleted =
        if Monitor.TryEnter lockObj then
            try
                if fence <> null && fence.Completed then
                    finalize()
                    true
                else
                    false
            finally
                Monitor.Exit lockObj
        else
            false

    member x.Wait() =
        lock lockObj (fun _ ->
            if fence <> null then
                fence.Wait()
                finalize()
        )

    member x.OnCompletion(action: unit -> unit) =
        let completed =
            lock lockObj (fun _ ->
                if isNull fence then true
                else onCompleted.Add action; false
            )

        if completed then action()

    member x.Dispose() =
        x.Wait()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and DeviceQueue internal (instance: Instance, device: Device, deviceHandle: VkDevice, family: DeviceQueueFamily, familyIndex: int, index: int) =
    let handle =
        NativePtr.temp (fun pQueue ->
            VkRaw.vkGetDeviceQueue(deviceHandle, uint32 familyIndex, uint32 index, pQueue)
            pQueue.[0]
        )

    let fence = new Fence(instance, device, deviceHandle, false)

    member x.HasTransfer = family.Flags.HasFlag QueueFlags.Transfer
    member x.HasCompute = family.Flags.HasFlag QueueFlags.Compute
    member x.HasGraphics = family.Flags.HasFlag QueueFlags.Graphics

    member x.Device = family.Device
    member x.Family : DeviceQueueFamily = family
    member x.Flags = family.Info.flags
    member x.FamilyIndex = family.Info.index
    member x.Index = index
    member x.Handle = handle

    member x.BindSparse(binds: VkBindSparseInfo[], fence: Fence) =
        let fence =
            if isNull fence then VkFence.Null
            else fence.Handle

        if x.Device.IsDeviceGroup then
            let groupInfos =
                binds |> Array.collect (fun b ->
                    x.Device.AllIndicesArr |> Array.map (fun i ->
                        VkDeviceGroupBindSparseInfo(
                            uint32 i, uint32 i
                        )
                    )
                )

            use pGroupInfos = fixed groupInfos

            let binds =
                let mutable gi = 0
                binds |> Array.collect (fun b ->
                    x.Device.AllIndicesArr |> Array.map (fun i ->
                        let mutable res = b
                        res.pNext <- NativePtr.toNativeInt (NativePtr.add pGroupInfos gi)
                        gi <- gi + 1
                        res
                    )
                )

            use pBinds = fixed binds
            VkRaw.vkQueueBindSparse(handle, uint32 binds.Length, pBinds, fence)
                |> check "could not bind sparse memory"

        else
            use pBinds = fixed binds
            VkRaw.vkQueueBindSparse(handle, uint32 binds.Length, pBinds, fence)
                |> check "could not bind sparse memory"

    member x.BindSparseSynchronously(binds: VkBindSparseInfo[]) =
        fence.Reset()
        x.BindSparse(binds, fence)
        fence.Wait()

    member x.Submit(cmds: CommandBuffer[], waitFor: Semaphore[], signal: Semaphore[], fence: Fence) =
        let pWaitFor = waitFor |> NativePtr.stackUseArr _.Handle
        let pWaitDstFlags = waitFor |> NativePtr.stackUseArr (fun _ -> VkPipelineStageFlags.TopOfPipeBit)
        let pSignal = signal |> NativePtr.stackUseArr _.Handle
        let pCommandBuffers = cmds |> NativePtr.stackUseArr _.Handle

        let fence =
            if isNull fence then VkFence.Null
            else fence.Handle

        if x.Device.IsDeviceGroup then
            let pCommandBufferDeviceMasks = cmds |> NativePtr.stackUseArr (fun _ -> x.Device.AllMask)

            let waitCount, pWaitIndices =
                if waitFor.Length > 0 then x.Device.AllCount, x.Device.AllIndices
                else 0u, NativePtr.zero

            let signalCount, pSignalIndices =
                if waitFor.Length > 0 then x.Device.AllCount, x.Device.AllIndices
                else 0u, NativePtr.zero

            let mutable groupSubmitInfo =
                VkDeviceGroupSubmitInfo(
                    waitCount, pWaitIndices,
                    uint32 cmds.Length, pCommandBufferDeviceMasks,
                    signalCount, pSignalIndices
                )

            let mutable submitInfo =
                VkSubmitInfo(
                    NativePtr.toNativeInt &&groupSubmitInfo,
                    uint32 waitFor.Length, pWaitFor, pWaitDstFlags,
                    uint32 cmds.Length, pCommandBuffers,
                    uint32 signal.Length, pSignal
                )

            VkRaw.vkQueueSubmit(handle, 1u, &&submitInfo, fence)
                |> check "could not submit command buffer"

        else
            let mutable submitInfo =
                VkSubmitInfo(
                    uint32 waitFor.Length, pWaitFor, pWaitDstFlags,
                    uint32 cmds.Length, pCommandBuffers,
                    uint32 signal.Length, pSignal
                )

            VkRaw.vkQueueSubmit(handle, 1u, &&submitInfo, fence)
                |> check "could not submit command buffer"

    member x.RunSynchronously(cmds: CommandBuffer[], waitFor: Semaphore[], signal: Semaphore[]) =
        fence.Reset()
        x.Submit(cmds, waitFor, signal, fence)
        fence.Wait()

    member x.RunSynchronously(cmd : CommandBuffer) =
        if not cmd.IsEmpty then
            x.RunSynchronously([|cmd|], Array.empty, Array.empty)

    member x.StartTask(cmds: CommandBuffer[], waitFor: Semaphore[], signal: Semaphore[]) =
        let f = x.Device.CreateFence()
        x.Submit(cmds, waitFor, signal, f)
        new DeviceTask(f)

    member x.StartTask(cmd : CommandBuffer) =
        if cmd.IsEmpty then
            DeviceTask.Completed
        else
            x.StartTask([|cmd|], Array.empty, Array.empty)

    member x.Dispose() =
        fence.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and [<Struct>] DeviceQueueHandle =
    val mutable private queue : DeviceQueue
    val mutable private active : bool

    internal new (queue: DeviceQueue, returnOnDispose: bool) =
        { queue = queue; active = returnOnDispose }

    member x.Queue : DeviceQueue = x.queue

    member x.Dispose() =
        if x.active then
            x.queue.Family.Release x
            x.active <- false

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and DeviceQueueFamily internal(physicalDevice: PhysicalDevice, device: Device, deviceHandle: VkDevice, info: QueueFamilyInfo) as this =
    let currentQueue = new ThreadLocal<DeviceQueue voption>(fun () -> ValueNone)

    let availableQueues =
        let queues = Array.init info.count (fun index -> new DeviceQueue(physicalDevice.Instance, device, deviceHandle, this, info.index, index))
        ConcurrentBag<DeviceQueue>(queues)

    let availableQueueCount = new SemaphoreSlim(availableQueues.Count)

    let currentToken = new ThreadLocal<DeviceToken>(fun () -> new DeviceToken(this))

    let supportedStages =
        let features = physicalDevice.Features.Shaders
        let mutable stages = info.flags |> VkPipelineStageFlags.ofQueueFlags

        if not features.GeometryShader then
            stages <- stages &&& (~~~VkPipelineStageFlags.GeometryShaderBit)

        if not features.TessellationShader then
            stages <- stages &&& (~~~VkPipelineStageFlags.TessellationControlShaderBit)
            stages <- stages &&& (~~~VkPipelineStageFlags.TessellationEvaluationShaderBit)

        stages

    member x.Device = device
    member x.Info: QueueFamilyInfo = info
    member x.Index : int = info.index
    member x.Flags : QueueFlags = info.flags
    member x.Stages = supportedStages

    member x.CreateCommandPool() =
        new CommandPool(device, info.index, x)

    member x.CreateCommandPool(flags : CommandPoolFlags) =
        new CommandPool(device, info.index, x, flags)

    member x.RunSynchronously(cmd: CommandBuffer) =
        use h = x.CurrentQueue
        h.Queue.RunSynchronously(cmd)

    member x.RunSynchronously(cmd: ICommand) =
        use token = x.CurrentToken
        token.Enqueue cmd
        token.Flush()

    member x.StartTask(cmd: CommandBuffer) =
        use h = x.CurrentQueue
        h.Queue.StartTask(cmd)

    member x.StartTask(cmd: ICommand) =
        use token = x.CurrentToken
        token.Enqueue cmd
        token.FlushAsync()

    member internal x.Release(handle: DeviceQueueHandle) =
        currentQueue.Value <- ValueNone
        availableQueues.Add handle.Queue
        availableQueueCount.Release() |> ignore

    member x.CurrentQueue : DeviceQueueHandle =
        match currentQueue.Value with
        | ValueSome q -> new DeviceQueueHandle(q, false)
        | _ ->
            availableQueueCount.Wait()

            let queue =
                match availableQueues.TryTake() with
                | (true, q) -> q
                | _ -> failf "failed to get queue"

            currentQueue.Value <- ValueSome queue
            new DeviceQueueHandle(queue, true)

    member x.CurrentToken : DeviceToken =
        let token = currentToken.Value
        token.AddRef()
        token

    member x.Dispose() =
        currentToken.Dispose()
        currentQueue.Dispose()
        availableQueueCount.Dispose()
        for q in availableQueues do q.Dispose()

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
        createInfo |> NativePtr.pin (fun pCreate ->
            temporary<VkCommandPool, VkCommandPool> (fun pHandle ->
                VkRaw.vkCreateCommandPool(device.Handle, pCreate, NativePtr.zero, pHandle)
                    |> check "could not create command pool"
                NativePtr.read pHandle
            )
        )

    let buffers = HashSet<CommandBuffer>()

    do device.Instance.RegisterDebugTrace(handle.Handle)

    internal new(device : Device, familyIndex : int, queueFamily : DeviceQueueFamily) =
        new CommandPool(device, familyIndex, queueFamily, CommandPoolFlags.None)

    member x.Device = device
    member x.QueueFamily = queueFamily
    member x.Handle = handle

    member x.Reset() =
        VkRaw.vkResetCommandPool(device.Handle, handle, VkCommandPoolResetFlags.None)
            |> check "failed to reset command pool"

        for cmd in buffers do
            cmd.Reset(resetByPool = true)

    member x.Destroy() =
        if handle.IsValid && device.Handle <> 0n then
            for cmd in buffers do
                cmd.Dispose()
            buffers.Clear()

            VkRaw.vkDestroyCommandPool(device.Handle, handle, NativePtr.zero)
            handle <- VkCommandPool.Null

    abstract member Dispose : unit -> unit
    default x.Dispose() = x.Destroy()

    member internal x.RemoveCommandBuffer(buffer: CommandBuffer) =
        buffers.Remove buffer |> ignore

    member x.CreateCommandBuffer(level : CommandBufferLevel) =
        let buffer = new CommandBuffer(x, level)
        buffers.Add buffer |> ignore
        buffer

    interface IDisposable with
        member x.Dispose() = x.Dispose()

/// Interface for resources that are used by commands recorded in
/// command buffers. As long as a command buffer has commands recorded, it keeps references
/// to their resources to prevent their premature disposal.
and ICommandResource =
    inherit IDisposable
    abstract member AddReference : unit -> unit

and CommandBuffer internal(pool : CommandPool, level : CommandBufferLevel) =

    let mutable handle =
        native {
            let! pInfo =
                VkCommandBufferAllocateInfo(
                    pool.Handle,
                    unbox (int level),
                    1u
                )
            let! pHandle = VkCommandBuffer.Zero
            VkRaw.vkAllocateCommandBuffers(pool.Device.Handle, pInfo, pHandle)
                |> check "could not allocated command buffer"

            return !!pHandle
        }

    do pool.Device.Instance.RegisterDebugTrace(handle)

    let mutable commands = 0
    let mutable recording = false

    // Set of resources used by recorded commands. Need to be disposed whenever
    // the command buffer is reset to allow them to be freed.
    let resources = HashSet<ICommandResource>()

    let releaseResources() =
        for r in resources do r.Dispose()
        resources.Clear()

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
                let features = pool.Device.PhysicalDevice.Features.Queries

                if inheritQueries && features.InheritedQueries then
                    let control =
                        if features.OcclusionQueryPrecise then
                            VkQueryControlFlags.All
                        else
                            VkQueryControlFlags.All ^^^ VkQueryControlFlags.PreciseBit

                    let statistics =
                        if features.PipelineStatistics then
                            VkQueryPipelineStatisticFlags.All
                        else
                            VkQueryPipelineStatisticFlags.None

                    1u, control, statistics
                else
                    0u, VkQueryControlFlags.None, VkQueryPipelineStatisticFlags.None

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
        releaseResources()

        match level with
        | CommandBufferLevel.Primary -> beginPrimary usage
        | CommandBufferLevel.Secondary -> beginSecondary pass framebuffer inheritQueries usage
        | _ -> failwith "unknown command buffer level"

        commands <- 0
        recording <- true

    member internal x.Reset(resetByPool: bool) =
        releaseResources()

        if not resetByPool then
            VkRaw.vkResetCommandBuffer(handle, VkCommandBufferResetFlags.ReleaseResourcesBit)
                |> check "could not reset command buffer"

        commands <- 0
        recording <- false

    member x.Reset() =
        x.Reset false

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
    member x.Device = pool.Device
    member x.QueueFamily = pool.QueueFamily
    member x.Pool = pool

    member x.AddResource(r : ICommandResource) =
        if resources.Add(r) then r.AddReference()

    member x.AddResources(r : seq<ICommandResource>) =
        r |> Seq.iter x.AddResource

    member x.AddCompensation(f : unit -> unit) =
        x.AddResource(
            { new ICommandResource with
                member x.AddReference() = ()
                member x.Dispose() = f() }
        )

    member x.AddCompensation(d : IDisposable) =
        x.AddResource(
            { new ICommandResource with
                member x.AddReference() = ()
                member x.Dispose() = d.Dispose() }
        )

    member x.Dispose() =
        if handle <> 0n && pool.Device.Handle <> 0n then
            releaseResources()

            pool.RemoveCommandBuffer x
            handle |> NativePtr.pin (fun pHandle -> VkRaw.vkFreeCommandBuffers(pool.Device.Handle, pool.Handle, 1u, pHandle))
            handle <- 0n

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and [<AllowNullLiteral>] Fence internal (instance: Instance, device: Device, deviceHandle: VkDevice, signaled: bool) =
    static let infinite = System.UInt64.MaxValue

    let pFence : nativeptr<VkFence> = NativePtr.alloc 1

    do
        let mutable createInfo =
            VkFenceCreateInfo(
                if signaled then VkFenceCreateFlags.SignaledBit
                else VkFenceCreateFlags.None
            )
        VkRaw.vkCreateFence(deviceHandle, &&createInfo, NativePtr.zero, pFence)
            |> check "could not create fence"

    do instance.RegisterDebugTrace(pFence)

    member x.Device = device
    member x.Handle = NativePtr.read pFence

    static member WaitAll(fences : Fence[]) =
        if fences.Length > 0 then
            let device = fences.[0].Device
            let pFences = NativePtr.stackalloc fences.Length
            for i in 0 .. fences.Length - 1 do
                NativePtr.set pFences i fences.[i].Handle

            VkRaw.vkWaitForFences(device.Handle, uint32 fences.Length, pFences, 1u, infinite)
                |> check "failed to wait for fences"

    static member WaitAny(fences : Fence[]) =
        if fences.Length > 0 then
            let device = fences.[0].Device
            let pFences = NativePtr.stackalloc fences.Length
            for i in 0 .. fences.Length - 1 do
                NativePtr.set pFences i fences.[i].Handle

            VkRaw.vkWaitForFences(device.Handle, uint32 fences.Length, pFences, 0u, infinite)
                |> check "failed to wait for fences"

    member x.Signaled =
        let handle = NativePtr.read pFence
        if handle.IsValid then
            VkRaw.vkGetFenceStatus(device.Handle, handle) = VkResult.Success
        else
            true

    member x.Completed =
        let handle = NativePtr.read pFence
        if handle.IsValid then
            VkRaw.vkGetFenceStatus(device.Handle, handle) <> VkResult.NotReady
        else
            true

    member x.Reset() =
        let handle = NativePtr.read pFence
        if handle.IsValid then
            VkRaw.vkResetFences(device.Handle, 1u, pFence)
                |> check "failed to reset fence"
        else
            failf "cannot reset disposed fence"


    member x.TryWait([<Optional; DefaultParameterValue(~~~0UL)>] timeoutInNanoseconds : uint64) =
        match VkRaw.vkWaitForFences(device.Handle, 1u, pFence, 1u, timeoutInNanoseconds) with
        | VkResult.Success -> true
        | VkResult.Timeout -> false
        | err -> failf "could not wait for fences: %A" err

    member x.Dispose() =
        if not (NativePtr.isNull pFence) then
            let handle = NativePtr.read pFence
            if handle.IsValid then
                VkRaw.vkDestroyFence(device.Handle, handle, NativePtr.zero)
                NativePtr.write pFence VkFence.Null
            NativePtr.free pFence

    member x.Wait([<Optional; DefaultParameterValue(~~~0UL)>] timeoutInNanoseconds : uint64) =
        if not <| x.TryWait(timeoutInNanoseconds) then
            raise <| TimeoutException()

    new(device, signaled) = new Fence(device.Instance, device, device.Handle, signaled)
    new(device) = new Fence(device, false)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and Semaphore internal(device : Device) =


    let mutable handle = 
        let info = VkSemaphoreCreateInfo.Empty

        info |> NativePtr.pin (fun pInfo ->
            temporary<VkSemaphore, VkSemaphore> (fun pHandle ->
                VkRaw.vkCreateSemaphore(device.Handle, pInfo, NativePtr.zero, pHandle)
                    |> check "could not create semaphore"
                NativePtr.read pHandle
            )
        )

    do device.Instance.RegisterDebugTrace(handle.Handle)

    member x.Device = device
    member x.Handle = handle

    member x.Set() =
        if handle.IsValid then
            use h = device.GraphicsFamily.CurrentQueue
            h.Queue.RunSynchronously([||], [|x|], [||])
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

        info |> NativePtr.pin (fun pInfo ->
            temporary<VkEvent, VkEvent> (fun pHandle ->
                VkRaw.vkCreateEvent(device.Handle, pInfo, NativePtr.zero, pHandle)
                    |> check "could not create event"
                NativePtr.read pHandle
            )
        )

    do device.Instance.RegisterDebugTrace(handle.Handle)

    member x.Device = device
    member x.Handle = handle

    member x.IsSet =
        if handle.IsValid then
            let res = VkRaw.vkGetEventStatus(device.Handle, handle)
            if res = VkResult.EventSet then true
            elif res = VkResult.EventReset then false
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

and DeviceHeap internal(device : Device, physical : PhysicalDevice, memory : MemoryInfo, heap : MemoryHeapInfo, isHostMemory : bool) as this =
    let hostVisible = memory.flags |> MemoryFlags.hostVisible
    let manager = DeviceMemoryManager(this, 128L <<< 20, isHostMemory)
    let mask = 1u <<< memory.index

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
                            device.PrintMemoryUsage Logger.Default
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
                device.PrintMemoryUsage Logger.Default
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

    member x.Copy() = new DeviceHeap(device, physical, memory, heap, isHostMemory)

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

    [<Obsolete("use TryGetAlignedV")>]
    member x.TryGetAligned(align : int64, size : int64, [<Optional; DefaultParameterValue(false)>] export : bool) =
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
            Some e.Current
        else
            None

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

    do if handle <> VkDeviceMemory.Null then heap.Device.Instance.RegisterDebugTrace(handle.Handle)

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
                    range |> NativePtr.pin (fun pRange ->
                        VkRaw.vkFlushMappedMemoryRanges(device.Handle, 1u, pRange)
                            |> check "could not flush memory range"
                    )

                Monitor.Exit x
        else
            failf "cannot map host-invisible memory"

and ICommand =
    abstract member Compatible : QueueFlags
    abstract member Enqueue : CommandBuffer -> unit

/// Records commands submits them to a device queue when disposed.
and DeviceToken internal (family: DeviceQueueFamily) =
    let mutable currentBuffer : CommandBuffer voption = ValueNone

    let mutable refCount = 0

    do family.Device.OnDispose.Add (fun () ->
        match currentBuffer with
        | ValueSome b ->
            b.Pool.Dispose()
            currentBuffer <- ValueNone

        | _ -> ()
    )

    /// Gets the current command buffer or prepares one for recording.
    member private x.CurrentBuffer =
        match currentBuffer with
        | ValueSome buffer ->
            if not buffer.IsRecording then
                buffer.Begin CommandBufferUsage.OneTimeSubmit

            buffer

        | _ ->
            let pool = family.CreateCommandPool CommandPoolFlags.Transient
            let buffer = pool.CreateCommandBuffer CommandBufferLevel.Primary

            currentBuffer <- ValueSome buffer
            buffer.Begin CommandBufferUsage.OneTimeSubmit
            buffer

    member x.Device = family.Device
    member x.Family = family

    member inline private x.Flush(queue: DeviceQueue) =
        match currentBuffer with
        | ValueSome buffer when buffer.IsRecording ->
            buffer.End()
            queue.RunSynchronously buffer
            buffer.Pool.Reset()

        | _ -> ()

    /// Flushes any enqueued commands and waits for their completion.
    member x.Flush() =
        use h = family.CurrentQueue
        x.Flush h.Queue

    /// Flushes any enqueued commands and performs the given action on the current queue.
    member x.FlushAndPerform (action: DeviceQueue -> 'T) =
        use h = family.CurrentQueue
        x.Flush h.Queue
        action h.Queue

    /// Flushes any enqueued commands.
    member x.FlushAsync() =
        match currentBuffer with
        | ValueSome buffer when buffer.IsRecording ->
            currentBuffer <- ValueNone
            buffer.End()
            let task = family.StartTask buffer
            task.OnCompletion buffer.Pool.Dispose
            task

        | _ ->
            DeviceTask.Completed

    member x.AddCompensation(action: unit -> unit) =
        x.CurrentBuffer.AddCompensation(action)

    member x.Enqueue(cmd: ICommand) =
        cmd.Enqueue(x.CurrentBuffer)

    member internal x.AddRef() =
        refCount <- refCount + 1

    member internal x.RemoveRef() =
        if refCount = 1 then x.Flush()
        else refCount <- refCount - 1

    member x.Dispose() =
        x.RemoveRef()

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

    static let rec tryAlloc (reqs : VkMemoryRequirements) (export : bool) (i : int) (memories : DeviceHeap[]) =
        if i >= memories.Length then
            None
        else
            let mem = memories.[i]
            if mem.Mask &&& reqs.memoryTypeBits <> 0u then
                let ptr = mem.Alloc(int64 reqs.alignment, int64 reqs.size, export)
                Some ptr
            else
                tryAlloc reqs export (i + 1) memories

    static let rec tryAllocDevice (reqs : VkMemoryRequirements) (export : bool) (i : int) (memories : DeviceHeap[]) =
        if i >= memories.Length then
            None
        else
            let mem = memories.[i]
            if mem.Mask &&& reqs.memoryTypeBits <> 0u && mem.Info.flags &&& MemoryFlags.DeviceLocal <> MemoryFlags.None then
                let ptr = mem.Alloc(int64 reqs.alignment, int64 reqs.size, export)
                Some ptr
            else
                tryAllocDevice reqs export (i + 1) memories


    [<Extension>]
    static member CreateDevice(this : PhysicalDevice, wantedExtensions : list<string>) =
        new Device(this, wantedExtensions)

    [<Extension>]
    static member GetMemory(this : Device, bits : uint32,
                            [<Optional; DefaultParameterValue(false)>] preferDevice : bool) =
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
    static member Alloc(this : Device, reqs : VkMemoryRequirements,
                        [<Optional; DefaultParameterValue(false)>] preferDevice : bool,
                        [<Optional; DefaultParameterValue(false)>] export : bool) =
        if preferDevice then
            match tryAllocDevice reqs export 0 this.Memories with
            | Some mem -> mem
            | None -> 
                match tryAlloc reqs export 0 this.Memories with
                | Some mem -> mem
                | None -> failf "could not find compatible memory for %A" reqs
        else
            match tryAlloc reqs export 0 this.Memories with
            | Some mem -> mem
            | None -> failf "could not find compatible memory for %A" reqs