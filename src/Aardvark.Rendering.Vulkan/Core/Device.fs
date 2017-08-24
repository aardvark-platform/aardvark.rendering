namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

#nowarn "9"
#nowarn "51"

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

type Device internal(physical : PhysicalDevice, wantedLayers : Set<string>, wantedExtensions : Set<string>) as this =
    let pool = QueueFamilyPool(physical.QueueFamilies)
    let graphicsQueues  = pool.TryTakeSingleFamily(QueueFlags.Graphics, 4)
    let computeQueues   = pool.TryTakeSingleFamily(QueueFlags.Compute, 2)
    let transferQueues  = pool.TryTakeSingleFamily(QueueFlags.Transfer ||| QueueFlags.SparseBinding, 2)
    let onDispose = Event<unit>()


    let layers, extensions =
        let availableExtensions = physical.GlobalExtensions |> Seq.map (fun e -> e.name.ToLower(), e.name) |> Dictionary.ofSeq
        let availableLayerNames = physical.AvailableLayers |> Seq.map (fun l -> l.name.ToLower(), l) |> Map.ofSeq
        let enabledLayers = 
            wantedLayers |> Set.filter (fun name ->
                let name = name.ToLower()
                match Map.tryFind name availableLayerNames with
                    | Some layer -> 
                        VkRaw.debug "enabled layer %A" name
                        for e in layer.extensions do
                            availableExtensions.[e.name.ToLower()] <- e.name
                        true
                    | _ ->
                        VkRaw.warn "could not enable instance-layer '%s' since it is not available" name
                        false
            )

        let enabledExtensions =
            wantedExtensions |> Seq.choose (fun name ->
                let name = name.ToLower()
                match availableExtensions.TryGetValue name with
                    | (true, realName) -> 
                        VkRaw.debug "enabled extension %A" name
                        Some realName
                    | _ -> 
                        VkRaw.warn "could not enable instance-extension '%s' since it is not available" name
                        None
            ) |> Set.ofSeq

        enabledLayers, enabledExtensions
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
                        VkStructureType.DeviceQueueCreateInfo, 0n,
                        0u,
                        uint32 familyIndex,
                        uint32 count,
                        queuePriorities
                    )
                )

        queueInfos |> NativePtr.withA (fun ptr ->
            let layers = Set.toArray layers
            let extensions = Set.toArray extensions
            let pLayers = CStr.sallocMany layers
            let pExtensions = CStr.sallocMany extensions

            
            let mutable features = VkPhysicalDeviceFeatures()
            VkRaw.vkGetPhysicalDeviceFeatures(physical.Handle, &&features)

            let mutable info =
                VkDeviceCreateInfo(
                    VkStructureType.DeviceCreateInfo, 0n,
                    0u,
                    uint32 queueInfos.Length, ptr,
                    uint32 layers.Length, pLayers,
                    uint32 extensions.Length, pExtensions,
                    &&features
                )

            let mutable device = VkDevice.Zero
            VkRaw.vkCreateDevice(physical.Handle, &&info, NativePtr.zero, &&device)
                |> check "could not create device"

            device
        )

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
            new DeviceHeap(this, t, t.heap)
        )

    let hostMemory = memories.[physical.HostMemory.index]
    let deviceMemory = memories.[physical.DeviceMemory.index]

    let currentResourceToken = new ThreadLocal<ref<Option<DeviceToken>>>(fun _ -> ref None)
    let mutable runtime = Unchecked.defaultof<IRuntime>
    let memoryLimits = physical.Limits.Memory


    member x.Token =
        let ref = currentResourceToken.Value
        match !ref with
            | Some t ->
                t.AddRef()
                t
            | None ->
                let queue = graphicsFamily.Value.GetQueue()
                let t = new DeviceToken(queue, ref)
                ref := Some t
                t 

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

    member x.EnabledLayers = layers
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

    member x.GraphicsFamily = 
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
                onDispose.Trigger()
                for h in memories do h.Dispose()
                for f in queueFamilies do f.Dispose()
                VkRaw.vkDestroyDevice(device, NativePtr.zero)
                device <- VkDevice.Zero

    member x.Handle = device

    member x.PhysicalDevice = physical

    member x.CreateFence(signaled : bool) = new Fence(x, signaled)
    member x.CreateFence() = new Fence(x)
    member x.CreateSemaphore() = new Semaphore(x)
    member x.CreateEvent() = new Event(x)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

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
    let mutable handle = VkQueue.Zero
    do VkRaw.vkGetDeviceQueue(deviceHandle, uint32 familyInfo.index, uint32 index, &&handle)


    let transfer = QueueFlags.transfer familyInfo.flags
    let compute = QueueFlags.compute familyInfo.flags
    let graphics = QueueFlags.graphics familyInfo.flags
    let mutable family : DeviceQueueFamily = Unchecked.defaultof<_>


    member x.HasTransfer = transfer
    member x.HasCompute = compute
    member x.HasGraphics= graphics

    member x.Device = device
    member x.Family
        with get() : DeviceQueueFamily = family
        and internal set (f : DeviceQueueFamily) = family <- f

    member x.Flags = familyInfo.flags
    member x.FamilyIndex = familyInfo.index
    member x.Index = index
    member x.Handle = handle

    member x.RunSynchronously(cmd : CommandBuffer) =
        if cmd.IsRecording then
            failf "cannot submit recording CommandBuffer"

        if not cmd.IsEmpty then
            let fence = device.CreateFence()
            lock x (fun () ->
                let mutable handle = cmd.Handle
                let mutable submitInfo =
                    VkSubmitInfo(
                        VkStructureType.SubmitInfo, 0n,
                        0u, NativePtr.zero, NativePtr.zero,
                        1u, &&handle,
                        0u, NativePtr.zero
                    )

                VkRaw.vkQueueSubmit(x.Handle, 1u, &&submitInfo, fence.Handle)
                    |> check "could not submit command buffer"
            )
            fence.Wait()
            fence.Dispose()

    member x.StartAsync(cmd : CommandBuffer, waitFor : list<Semaphore>) =
        if cmd.IsRecording then
            failf "cannot submit recording CommandBuffer"

        let sem = device.CreateSemaphore()
        lock x (fun () ->
            let cnt = if cmd.IsEmpty then 0u else 1u
            let mutable handle = cmd.Handle
            let mutable semHandle = sem.Handle

            match waitFor with
                | [] ->
                    let mutable submitInfo =
                        VkSubmitInfo(
                            VkStructureType.SubmitInfo, 0n,
                            0u, NativePtr.zero, NativePtr.zero,
                            cnt, &&handle,
                            1u, &&semHandle
                        )

                    VkRaw.vkQueueSubmit(x.Handle, 1u, &&submitInfo, VkFence.Null)
                        |> check "could not submit command buffer"
                    sem

                | _ ->
                    let handles = waitFor |> List.map (fun s -> s.Handle) |> List.toArray
                    let mask = Array.create handles.Length (int VkPipelineStageFlags.TopOfPipeBit)
            
                    mask |> NativePtr.withA (fun pMask ->
                        handles |> NativePtr.withA (fun pWaitFor ->
                            let mutable submitInfo =
                                VkSubmitInfo(
                                    VkStructureType.SubmitInfo, 0n,
                                    uint32 handles.Length, pWaitFor, NativePtr.cast pMask,
                                    cnt, &&handle,
                                    1u, &&semHandle
                                )

                            VkRaw.vkQueueSubmit(x.Handle, 1u, &&submitInfo, VkFence.Null)
                                |> check "could not submit command buffer"
                            sem
                        )
                    )
        )

    member x.StartAsync(cmd : CommandBuffer) =
        x.StartAsync(cmd, [])
//
//    member x.Wait(sem : Semaphore) =
//        lock x (fun () ->
//            let mutable semHandle = sem.Handle
//            let mutable submitInfo =
//                let mutable dstStage = VkPipelineStageFlags.BottomOfPipeBit
//                VkSubmitInfo(
//                    VkStructureType.SubmitInfo, 0n, 
//                    1u, &&semHandle, &&dstStage,
//                    0u, NativePtr.zero,
//                    0u, NativePtr.zero
//                )
//
//            VkRaw.vkQueueSubmit(x.Handle, 1u, &&submitInfo, VkFence.Null) 
//                |> check "vkQueueWaitSemaphore"
//        )

    member x.Wait(sem : Semaphore) =
        let f = device.CreateFence()
        lock x (fun () ->
            let mutable semHandle = sem.Handle
            let mutable submitInfo =
                let mutable dstStage = VkPipelineStageFlags.BottomOfPipeBit
                VkSubmitInfo(
                    VkStructureType.SubmitInfo, 0n, 
                    1u, &&semHandle, &&dstStage,
                    0u, NativePtr.zero,
                    0u, NativePtr.zero
                )

            VkRaw.vkQueueSubmit(x.Handle, 1u, &&submitInfo, f.Handle) 
                |> check "vkQueueWaitSemaphore"
        )
        f.Wait()
        f.Dispose()

    member x.Wait(sems : seq<Semaphore>) =
        let f = device.CreateFence()
        lock x (fun () ->
            let sems = sems |> Seq.map (fun s -> s.Handle) |> Seq.toArray
            let masks = Array.create sems.Length (int VkPipelineStageFlags.TopOfPipeBit)

            sems |> NativePtr.withA (fun pSems ->
                masks |> NativePtr.withA (fun pMask ->
                    let mutable submitInfo =
                        VkSubmitInfo(
                            VkStructureType.SubmitInfo, 0n, 
                            uint32 sems.Length, pSems, NativePtr.cast pMask,
                            0u, NativePtr.zero,
                            0u, NativePtr.zero
                        )

                    VkRaw.vkQueueSubmit(x.Handle, 1u, &&submitInfo, f.Handle) 
                        |> check "vkQueueWaitSemaphore"
                )
            )
        )
        f.Wait()
        f.Dispose()

//
//    member x.Signal() =
//        let sem = device.CreateSemaphore()
//        lock x (fun () ->
//            let mutable semHandle = sem.Handle
//            let mutable submitInfo =
//                VkSubmitInfo(
//                    VkStructureType.SubmitInfo, 0n, 
//                    0u, NativePtr.zero, NativePtr.zero,
//                    0u, NativePtr.zero,
//                    1u, &&semHandle
//                )
//            VkRaw.vkQueueSubmit(x.Handle, 1u, &&submitInfo, VkFence.Null) 
//                |> check "vkQueueWaitSemaphore"
//        )
//        sem

//    member x.Fence() =
//        let fence = device.CreateFence()
//        lock x (fun () ->
//            VkRaw.vkQueueSubmit(x.Handle, 0u, NativePtr.zero, fence.Handle)
//                |> check "could not submit command buffer"
//        )
//        fence

    member x.WaitIdle() =
        VkRaw.vkQueueWaitIdle(x.Handle)
            |> check "could not wait for queue"

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


and DeviceQueueFamily internal(device : Device, info : QueueFamilyInfo, queues : list<DeviceQueue>) as this =
    let store = queues |> List.toArray
    do for q in store do q.Family <- this
    let mutable current = 0

    let defaultPool = new DeviceCommandPool(device, info.index, this)

    member x.Device = device
    member x.Info = info
    member x.Index = info.index
    member x.Flags = info.flags
    member x.Queues = queues
    member x.DefaultCommandPool = defaultPool
    member x.CreateCommandPool() = new CommandPool(device, info.index, x)

    member x.RunSynchronously(cmd : CommandBuffer) =
        let q = x.GetQueue()
        q.RunSynchronously(cmd)


    member x.GetQueue () : DeviceQueue =
        let next = Interlocked.Change(&current, fun c -> (c + 1) % store.Length)
        store.[next]

    member x.Dispose() =
        defaultPool.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and DeviceCommandPool internal(device : Device, index : int, queueFamily : DeviceQueueFamily) =

    let allPools = ConcurrentHashSet<VkCommandPool>()

    let createCommandPoolHandle _ =
        let mutable createInfo =
            VkCommandPoolCreateInfo(
                VkStructureType.CommandPoolCreateInfo, 0n,
                VkCommandPoolCreateFlags.ResetCommandBufferBit,
                uint32 index
            )
        let mutable handle = VkCommandPool.Null
        VkRaw.vkCreateCommandPool(device.Handle, &&createInfo, NativePtr.zero, &&handle)
            |> check "could not create command pool"

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
//
//        let bag =
//            match level with
//                | CommandBufferLevel.Primary -> primaryBag
//                | CommandBufferLevel.Secondary -> secondaryBag
//                | _ -> failf "invalid command-buffer level"
//
//        match bag.TryTake() with
//            | (true, cmd) -> cmd
//            | _ -> 
        { new CommandBuffer(device, pool, queueFamily, level) with
            override x.Dispose() =
                let mutable handle = x.Handle
                VkRaw.vkFreeCommandBuffers(device.Handle, pool, 1u, &&handle)

        }


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

and CommandPool internal(device : Device, familyIndex : int, queueFamily : DeviceQueueFamily) =
    let mutable handle = VkCommandPool.Null
    let mutable createInfo =
        VkCommandPoolCreateInfo(
            VkStructureType.CommandPoolCreateInfo, 0n,
            VkCommandPoolCreateFlags.ResetCommandBufferBit,
            uint32 familyIndex
        )

    do VkRaw.vkCreateCommandPool(device.Handle, &&createInfo, NativePtr.zero, &&handle)
        |> check "could not create command pool"

    member x.Device = device
    member x.QueueFamily = queueFamily
    member x.Handle = handle

    member x.Dispose() =
        if handle.IsValid && device.Handle <> 0n then
            VkRaw.vkDestroyCommandPool(device.Handle, handle, NativePtr.zero)

    member x.CreateCommandBuffer(level : CommandBufferLevel) =
        new CommandBuffer(device, handle, queueFamily, level)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and CommandBuffer internal(device : Device, pool : VkCommandPool, queueFamily : DeviceQueueFamily, level : CommandBufferLevel) =   

    let mutable handle = 
        let mutable info =
            VkCommandBufferAllocateInfo(
                VkStructureType.CommandBufferAllocateInfo, 0n,
                pool,
                unbox (int level),
                1u
            )
        let mutable handle = VkCommandBuffer.Zero
        VkRaw.vkAllocateCommandBuffers(device.Handle, &&info, &&handle)
            |> check "could not allocated command buffer"

        handle

    let mutable commands = 0
    let mutable recording = false
    let cleanupTasks = List<IDisposable>()

    let cleanup() =
        for c in cleanupTasks do c.Dispose()
        cleanupTasks.Clear()

    member x.Reset() =
        cleanup()

        VkRaw.vkResetCommandBuffer(handle, VkCommandBufferResetFlags.None)
            |> check "could not reset command buffer"
        commands <- 0
        recording <- false

    member x.Begin(usage : CommandBufferUsage) =
        cleanup()
        let mutable inh =
            VkCommandBufferInheritanceInfo(
                VkStructureType.CommandBufferInheritanceInfo, 0n,
                VkRenderPass.Null, 0u,
                VkFramebuffer.Null, 
                0u,
                VkQueryControlFlags.None,
                VkQueryPipelineStatisticFlags.None
            )

        let mutable info =
            VkCommandBufferBeginInfo(
                VkStructureType.CommandBufferBeginInfo, 0n,
                unbox (int usage),
                &&inh
            )

        VkRaw.vkBeginCommandBuffer(handle, &&info)
            |> check "could not begin command buffer"

        commands <- 0
        recording <- true

    member x.Begin(pass : Resource<VkRenderPass>, usage : CommandBufferUsage) =
        cleanup()
        let mutable inh =
            VkCommandBufferInheritanceInfo(
                VkStructureType.CommandBufferInheritanceInfo, 0n,
                pass.Handle, 0u,
                VkFramebuffer.Null, 
                0u,
                VkQueryControlFlags.None,
                VkQueryPipelineStatisticFlags.None
            )

        let mutable info =
            VkCommandBufferBeginInfo(
                VkStructureType.CommandBufferBeginInfo, 0n,
                unbox (int usage),
                &&inh
            )

        VkRaw.vkBeginCommandBuffer(handle, &&info)
            |> check "could not begin command buffer"


        commands <- 0
        recording <- true

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
        let handles = e |> Array.map (fun e -> e.Handle)
        handles |> NativePtr.withA (fun ptr ->
            VkRaw.vkCmdWaitEvents(
                handle, uint32 handles.Length, ptr,
                VkPipelineStageFlags.None, VkPipelineStageFlags.AllCommandsBit,
                0u, NativePtr.zero,
                0u, NativePtr.zero,
                0u, NativePtr.zero
            )
        )

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

    abstract member Dispose : unit -> unit
    default x.Dispose() =
        if handle <> 0n && device.Handle <> 0n then
            cleanup()
            VkRaw.vkFreeCommandBuffers(device.Handle, pool, 1u, &&handle)
            handle <- 0n

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and private FenceCallbacks(device : Device) =
    static let noDisposable = { new IDisposable with member x.Dispose() = () }

    let infinite = System.UInt64.MaxValue

    let cont = Dict<Fence, Dictionary<int, unit -> unit>>()
    
    let mutable version = 0

    let mutable currentId = 0
    let newId() = Interlocked.Increment(&currentId)

    let changedFence = device.CreateFence()
    let mutable changed = 0

    let timeout = 1000UL
    let run () =
        let mutable all = [||]
        let mutable ptr : nativeptr<VkFence> = NativePtr.alloc 1
        let mutable lastVersion = -1

        while true do
            let version = Volatile.Read(&version)

            if version <> lastVersion then
                lastVersion <- version

                all <-
                    lock cont (fun () ->
                        while cont.Count = 0 do
                            Monitor.Wait(cont) |> ignore

                        changedFence.Reset()
                        changed <- 0
                        cont.Keys |> Seq.toArray
                    )

                NativePtr.free ptr
                ptr <- NativePtr.alloc (1 + all.Length)
                NativePtr.write ptr changedFence.Handle
                for i in 0 .. all.Length - 1 do
                    NativePtr.set ptr (i + 1) all.[i].Handle

            let status = 
                VkRaw.vkWaitForFences(device.Handle, uint32 (1 + all.Length), ptr, 0u, infinite)
                    
            match status with
                | VkResult.VkSuccess ->
                    for f in all do
                        if f.Completed then
                            match lock cont (fun () -> cont.TryRemove f) with
                                | (true, cs) -> for (KeyValue(_,cb)) in cs do cb()
                                | _ -> ()
                    
                | _ ->
                    ()

    let thread = Thread(ThreadStart(run), IsBackground = true, Priority = ThreadPriority.Highest)
    do thread.Start()

    let remove (f : Fence) (id : int) =
        lock cont (fun () ->
            match cont.TryGetValue f with
                | (true, cbs) ->
                    if cbs.Remove id then
                        if cbs.Count = 0 then 
                            version <- version + 1
                            cont.Remove f |> ignore
                        true
                    else
                        false
                | _ ->
                    false
        )

    member x.ContinueWith(f : Fence, ct : CancellationToken, cb : unit -> unit) =
        if not ct.IsCancellationRequested then
            if f.Completed then 
                cb()
            else
                lock cont (fun () ->
                    let mutable isNew = false
                    let l = cont.GetOrCreate(f, fun _ -> isNew <- true; Dictionary())
                    let id = newId()
                    if ct.CanBeCanceled then
                        let mutable reg = noDisposable
                        l.[id] <- (fun () -> reg.Dispose(); cb())
                        reg <- ct.Register(fun () -> remove f id |> ignore)
                    else
                        l.[id] <- cb
                
                    if isNew then 
                        version <- version + 1
                        if changed = 0 then
                            changed <- 1
                            changedFence.Set()

                        if cont.Count = 1 then
                            Monitor.Pulse cont
                )

   
and Fence internal(device : Device, signaled : bool) =
    static let infinite = System.UInt64.MaxValue
    static let cbs = ConcurrentDictionary<Device, FenceCallbacks>()

    let pFence : nativeptr<VkFence> = NativePtr.alloc 1

    do 
        let info =
            VkFenceCreateInfo(
                VkStructureType.FenceCreateInfo, 0n,
                (if signaled then VkFenceCreateFlags.SignaledBit else VkFenceCreateFlags.None)
            )
        [|info|] |> NativePtr.withA (fun pInfo ->
            VkRaw.vkCreateFence(device.Handle, pInfo, NativePtr.zero, pFence)
                |> check "could not create fence"
        )

    member x.Device = device
    member x.Handle = NativePtr.read pFence

    member x.ContinueWith(f : unit -> unit, cancellationToken : CancellationToken) =
        let cb = cbs.GetOrAdd(device, fun d -> FenceCallbacks(d))
        cb.ContinueWith(x, cancellationToken, f)

    member x.ContinueWith(f : unit -> unit) =
        x.ContinueWith(f, CancellationToken.None)

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

    member x.Set() =
        let handle = NativePtr.read pFence
        if handle.IsValid then
            let queue = device.ComputeFamily.GetQueue()
            VkRaw.vkQueueSubmit(queue.Handle, 0u, NativePtr.zero, handle)
                |> check "cannot signal fence"
        else
            failf "cannot signal disposed fence" 

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
        let mutable handle = VkSemaphore.Null
        let mutable info =
            VkSemaphoreCreateInfo(
                VkStructureType.SemaphoreCreateInfo, 0n,
                0u
            )

        VkRaw.vkCreateSemaphore(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "could not create semaphore"

        handle

    member x.Device = device
    member x.Handle = handle

    member x.Set() =
        if handle.IsValid then
            let queue = device.ComputeFamily.GetQueue()

            let mutable submitInfo =
                VkSubmitInfo(
                    VkStructureType.SubmitInfo, 0n,
                    0u, NativePtr.zero, NativePtr.zero,
                    0u, NativePtr.zero,
                    1u, &&handle
                )

            VkRaw.vkQueueSubmit(queue.Handle, 1u, &&submitInfo, VkFence.Null)
                |> check "cannot signal fence"
        else
            failf "cannot signal disposed fence" 

    member x.Dispose() =
        if handle.IsValid && device.Handle <> 0n then
            VkRaw.vkDestroySemaphore(device.Handle, handle, NativePtr.zero)
            handle <- VkSemaphore.Null

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and Event internal(device : Device) =
    let mutable info =
        VkEventCreateInfo(
            VkStructureType.EventCreateInfo, 0n,
            VkEventCreateFlags.MinValue
        )

    let mutable handle = VkEvent.Null
    do VkRaw.vkCreateEvent(device.Handle, &&info, NativePtr.zero, &&handle)
        |> check "could not create event"

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


and DeviceHeap internal(device : Device, memory : MemoryInfo, heap : MemoryHeapInfo) as this =
    let hostVisible = memory.flags |> MemoryFlags.hostVisible
    let manager = DeviceMemoryManager(this, heap.Capacity.Bytes, 128L <<< 20)
    let mask = 1u <<< memory.index

    let nullptr = 
        lazy (
            let mutable mem = VkDeviceMemory.Null

            let mutable info =
                VkMemoryAllocateInfo(
                    VkStructureType.MemoryAllocateInfo, 0n, 
                    16UL,
                    uint32 memory.index
                )

            VkRaw.vkAllocateMemory(device.Handle, &&info, NativePtr.zero, &&mem)
                |> check "could not 'allocate' null pointer for device heap"

            new DeviceMemory(this, mem, 0L)
        )

    member x.Device = device
    member x.Info = memory
    member x.Index = memory.index
    member internal x.Mask = mask
    member x.IsHostVisible = hostVisible
    member x.HeapFlags = heap.Flags
    member x.Flags = memory.flags
    member x.Available = heap.Available
    member x.Allocated = heap.Allocated
    member x.Capacity = heap.Capacity


    member x.Null = nullptr.Value

    member x.Alloc(align : int64, size : int64) = manager.Alloc(align, size)
    member x.Free(ptr : DevicePtr) = ptr.Dispose()

    member x.AllocTemp(align : int64, size : int64) =
        x.AllocRaw(size) :> DevicePtr


    member x.TryAllocRaw(size : int64, [<Out>] ptr : byref<DeviceMemory>) =
        if heap.TryAdd size then
            let mutable info =
                VkMemoryAllocateInfo(
                    VkStructureType.MemoryAllocateInfo, 0n,
                    uint64 size,
                    uint32 memory.index
                )

            let mutable mem = VkDeviceMemory.Null

            VkRaw.vkAllocateMemory(device.Handle, &&info, NativePtr.zero, &&mem)
                |> check "could not allocate memory"

            ptr <- new DeviceMemory(x, mem, size)
            true
        else
            false

    member x.AllocRaw(size : int64) =
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
                    VkRaw.vkFreeMemory(device.Handle, ptr.Handle, NativePtr.zero)
                    ptr.Handle <- VkDeviceMemory.Null
                    ptr.Size <- 0L
            )

    member x.Dispose() =
        if nullptr.IsValueCreated then
            VkRaw.vkFreeMemory(device.Handle, nullptr.Value.Handle, NativePtr.zero)
        
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

    let addBlock(this : DeviceMemoryManager) =
        let store = heap.AllocRaw blockSize

        let block = new DeviceBlock(this, store, 0L, blockSize, true, null, null)
        free.Insert(block)


    member x.Alloc(align : int64, size : int64) =
        if size <= 0L then
            DevicePtr.Null
        elif size >= blockSize then
            heap.AllocRaw(size) :> DevicePtr
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
                    b.Memory.Dispose()
                else
                    free.Insert(b)

            )

and DeviceMemory internal(heap : DeviceHeap, handle : VkDeviceMemory, size : int64) =
    inherit DevicePtr(Unchecked.defaultof<_>, 0L, size)
    static let nullptr = new DeviceMemory(Unchecked.defaultof<_>, VkDeviceMemory.Null, 0L)

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
            let mutable mapped = false
            Monitor.Enter memory
            try
                let mutable ptr = 0n
                VkRaw.vkMapMemory(device.Handle, memory.Handle, uint64 x.Offset, uint64 x.Size, 0u, &&ptr)
                    |> check "could not map memory"
                mapped <- true
                f ptr
            finally 
                if mapped then VkRaw.vkUnmapMemory(device.Handle, memory.Handle)
                Monitor.Exit memory
        else
            failf "cannot map host-invisible memory"



and ICommand =
    abstract member Compatible : QueueFlags
    abstract member TryEnqueue : CommandBuffer * byref<Disposable> -> bool

and IQueueCommand =
    abstract member Compatible : QueueFlags
    abstract member TryEnqueue : queue : DeviceQueue * waitFor : list<Semaphore> * disp : byref<Disposable> -> Option<Semaphore>

and DeviceToken(queue : DeviceQueue, ref : ref<Option<DeviceToken>>) =
    let mutable current             : Option<CommandBuffer> = None
    let disposables                 : List<Disposable>      = List()
    let semaphores                  : List<Semaphore>       = List()

    let mutable isEmpty = true
    let mutable refCount = 1

    let mutable lastSems : list<Semaphore> = []

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

        refCount <- 1
        isEmpty <- true
        ref := None

    let enqueue (buffer : CommandBuffer) (cmd : ICommand) =
        let mutable disp = Disposable.Empty
        if cmd.TryEnqueue(buffer, &disp) then
            if not (isNull disp) then disposables.Add disp
        else
            cleanup()
            failf "could not enqueue command: %A" cmd

    let flush() =
        match current with
            | Some buffer ->
                buffer.End()
                if not buffer.IsEmpty then
                    isEmpty <- false
                    let sem = queue.StartAsync(buffer, lastSems)
                    semaphores.Add sem
                    disposables.Add { new Disposable() with member x.Dispose() = buffer.Dispose() }
                    lastSems <- [sem]

                current <- None

            | None ->
                ()


    member x.Flush() =
        check()
        flush()

    member x.Sync() =
        check()
        flush()

        if not isEmpty then
            match lastSems with
                | [] -> ()
                | sems -> queue.Wait(sems)

            for s in semaphores do s.Dispose()
            semaphores.Clear()
            lastSems <- []


    member x.AddCleanup(f : unit -> unit) =
        check()
        disposables.Add { new Disposable() with member x.Dispose() = f() }

    member x.Enqueue (cmd : ICommand) =
        check()
        match current with
            | Some buffer -> 
                enqueue buffer cmd
                
            | None ->
                let buffer = queue.Family.DefaultCommandPool.CreateCommandBuffer CommandBufferLevel.Primary
                buffer.Begin CommandBufferUsage.OneTimeSubmit
                current <- Some buffer
                enqueue buffer cmd

    member x.Enqueue (cmd : IQueueCommand) =
        check()
        flush ()
        let mutable disp = Disposable.Empty
        let sem = cmd.TryEnqueue(queue, lastSems, &disp) 
        if not (isNull disp) then disposables.Add disp
        sem |> Option.iter semaphores.Add
        isEmpty <- false
        lastSems <- Option.toList sem

    member internal x.AddRef() = 
        check()
        refCount <- refCount + 1

    member internal x.RemoveRef() = 
        check()
        refCount <- refCount - 1
        if refCount = 0 then 
            ref := None
            x.Sync()
            cleanup()

    member x.Dispose() =
        check()
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
    static member CreateDevice(this : PhysicalDevice, wantedLayers : Set<string>, wantedExtensions : Set<string>) =
        new Device(this, wantedLayers, wantedExtensions)

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