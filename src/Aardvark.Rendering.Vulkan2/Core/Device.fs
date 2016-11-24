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


type Device internal(physical : PhysicalDevice, wantedLayers : Set<string>, wantedExtensions : Set<string>, queues : list<QueueFamilyInfo * int>) as this =

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

    let maxQueueCount = queues |> Seq.map snd |> Seq.max
    let queuePriorities =
        let ptr = NativePtr.alloc maxQueueCount
        for i in 0 .. maxQueueCount - 1 do NativePtr.set ptr i 1.0f
        ptr

    let queueInfos = 
        queues |> List.toArray |> Array.choose (fun (q,c) ->
            if c < 0 then 
                None
            else
                let count = 
                    if c > q.count then
                        VkRaw.warn "could not create %d queues for family %A (only %d available)" c q.index q.count
                        q.count
                    else
                        c 

                let result =
                    VkDeviceQueueCreateInfo(
                        VkStructureType.DeviceQueueCreateInfo, 0n,
                        0u,
                        uint32 q.index,
                        uint32 count,
                        queuePriorities
                    )

                Some result
        )


    let instance = physical.Instance
    let mutable device =
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


    let queueFamilies = 
        queueInfos |> Array.map (fun info ->
            let family = physical.QueueFamilies.[int info.queueFamilyIndex]
            let queues = List.init (int info.queueCount) (fun i -> DeviceQueue(this, device, family, i))
            new DeviceQueueFamily(this, family, queues)
        )

    let pAllFamilies =
        let ptr = NativePtr.alloc queueFamilies.Length
        for i in 0 .. queueFamilies.Length-1 do
            NativePtr.set ptr i (uint32 queueFamilies.[i].Index)
        ptr

    let memories = 
        physical.MemoryTypes |> Array.map (fun t ->
            DeviceHeap(this, t, t.heap)
        )

    let deviceMemory = memories.[physical.DeviceMemory.index]
    let hostMemory = memories.[physical.HostMemory.index]

//    let minMemoryMapAlignment = int64 physical.Limits.minMemoryMapAlignment
//    let minTexelBufferOffsetAlignment = int64 physical.Limits.minTexelBufferOffsetAlignment
//    let minUniformBufferOffsetAlignment = int64 physical.Limits.minUniformBufferOffsetAlignment
//    let minStorageBufferOffsetAlignment = int64 physical.Limits.minStorageBufferOffsetAlignment
//    let bufferImageGranularity = int64 physical.Limits.bufferImageGranularity

    let computeFamily = 
        queueFamilies 
            |> Seq.tryFind (fun f -> QueueFlags.compute f.Flags)


    let graphicsFamily = 
        queueFamilies 
            |> Seq.tryFind (fun f -> QueueFlags.graphics f.Flags)

    let transferFamily = 
        queueFamilies 
            |> Seq.tryFind (fun f -> QueueFlags.transfer f.Flags)

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
                let t = new DeviceToken(x, ref)
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

    member x.EnabledLayers = layers
    member x.EnabledExtensions = extensions

    member x.MinMemoryMapAlignment = memoryLimits.MinMemoryMapAlignment
    member x.MinTexelBufferOffsetAlignment = memoryLimits.MinTexelBufferOffsetAlignment
    member x.MinUniformBufferOffsetAlignment = memoryLimits.MinUniformBufferOffsetAlignment
    member x.MinStorageBufferOffsetAlignment = memoryLimits.MinStorageBufferOffsetAlignment
    member x.BufferImageGranularity = memoryLimits.BufferImageGranularity

    member x.Instance = instance
    member x.QueueFamilies = queueFamilies

    member internal x.AllQueueFamiliesPtr = pAllFamilies
    member internal y.AllQueueFamiliesCnt = uint32 queueFamilies.Length

    member x.ComputeFamily = 
        match computeFamily with
            | Some pool -> pool
            | None -> failf "the device does not support compute-queues"

    member x.GraphicsFamily = 
        match graphicsFamily with
            | Some pool -> pool
            | None -> failf "the device does not support graphics-queues"

    member x.TransferFamily = 
        match transferFamily with
            | Some pool -> pool
            | None -> failf "the device does not support transfer-queues"


    member x.IsDisposed = instance.IsDisposed || isDisposed <> 0

    member x.Memories = memories
    member x.DeviceMemory = deviceMemory
    member x.HostMemory = hostMemory

    member x.Dispose() =
        if not instance.IsDisposed then
            let o = Interlocked.Exchange(&isDisposed, 1)
            if o = 0 then 
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
            let mutable handle = cmd.Handle
            let mutable submitInfo =
                VkSubmitInfo(
                    VkStructureType.SubmitInfo, 0n,
                    0u, NativePtr.zero, NativePtr.zero,
                    1u, &&handle,
                    0u, NativePtr.zero
                )

            let fence = device.CreateFence()
            VkRaw.vkQueueSubmit(x.Handle, 1u, &&submitInfo, fence.Handle)
                |> check "could not submit command buffer"
            fence.Wait()

    member x.Start(cmd : CommandBuffer) =
        if cmd.IsRecording then
            failf "cannot submit recording CommandBuffer"

        if not cmd.IsEmpty then
            let mutable handle = cmd.Handle
            let mutable submitInfo =
                VkSubmitInfo(
                    VkStructureType.SubmitInfo, 0n,
                    0u, NativePtr.zero, NativePtr.zero,
                    1u, &&handle,
                    0u, NativePtr.zero
                )


            VkRaw.vkQueueSubmit(x.Handle, 1u, &&submitInfo, VkFence.Null)
                |> check "could not submit command buffer"

    member x.Wait(sem : Semaphore) =
        let mutable semHandle = sem.Handle
        let mutable submitInfo =
            let mutable dstStage = VkPipelineStageFlags.BottomOfPipeBit
            VkSubmitInfo(
                VkStructureType.SubmitInfo, 0n, 
                1u, &&semHandle, &&dstStage,
                0u, NativePtr.zero,
                0u, NativePtr.zero
            )

        VkRaw.vkQueueSubmit(x.Handle, 1u, &&submitInfo, VkFence.Null) 
            |> check "vkQueueWaitSemaphore"

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

        handle, ConcurrentBag<CommandBuffer>(), ConcurrentBag<CommandBuffer>()

    let handles = new ConcurrentDictionary<int, VkCommandPool * ConcurrentBag<CommandBuffer> * ConcurrentBag<CommandBuffer>>()
    let get (key : int) =
        handles.GetOrAdd(key, System.Func<_,_>(createCommandPoolHandle))

    member x.Device = device
    member x.QueueFamily = queueFamily

    member x.CreateCommandBuffer(level : CommandBufferLevel) =
        let id = Thread.CurrentThread.ManagedThreadId
        let pool, primaryBag, secondaryBag = get id

        let bag =
            match level with
                | CommandBufferLevel.Primary -> primaryBag
                | CommandBufferLevel.Secondary -> secondaryBag
                | _ -> failf "invalid command-buffer level"

        match bag.TryTake() with
            | (true, cmd) -> cmd
            | _ ->
                { new CommandBuffer(device, pool, queueFamily, level) with
                    override x.Dispose() =
                        x.Reset()
                        bag.Add x
                }


    member x.Dispose() =
        if device.Handle <> 0n then
            let all = handles |> Seq.map (fun (KeyValue(_,(p,_,_))) -> p) |> Seq.toArray
            handles.Clear()
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
    let mutable info =
        VkCommandBufferAllocateInfo(
            VkStructureType.CommandBufferAllocateInfo, 0n,
            pool,
            unbox (int level),
            1u
        )

    let mutable handle = VkCommandBuffer.Zero
    do VkRaw.vkAllocateCommandBuffers(device.Handle, &&info, &&handle)
        |> check "could not allocated command buffer"
    
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

and Fence internal(device : Device, signaled : bool) =
    static let infinite = -1L

    let mutable handle : VkFence = VkFence.Null
    do 
        let mutable info =
            VkFenceCreateInfo(
                VkStructureType.FenceCreateInfo, 0n,
                (if signaled then VkFenceCreateFlags.SignaledBit else VkFenceCreateFlags.None)
            )
        VkRaw.vkCreateFence(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "could not create fence"


    member x.Device = device
    member x.Handle = handle

    member x.Signaled =
        if handle.IsValid then
            VkRaw.vkGetFenceStatus(device.Handle, handle) = VkResult.VkSuccess
        else
            true

    member x.Completed =
        if handle.IsValid then
            VkRaw.vkGetFenceStatus(device.Handle, handle) <> VkResult.VkNotReady
        else
            true
    member x.TryWait(timeoutInNanoseconds : int64) =
        let waitResult = VkRaw.vkWaitForFences(device.Handle, 1u, &&handle, 1u, uint64 timeoutInNanoseconds)
        match waitResult with
            | VkResult.VkTimeout -> false
            | VkResult.VkSuccess -> 
                VkRaw.vkDestroyFence(device.Handle, handle, NativePtr.zero)
                handle <- VkFence.Null
                true
            | err -> 
                VkRaw.vkDestroyFence(device.Handle, handle, NativePtr.zero)
                handle <- VkFence.Null
                failf "could not wait for fences: %A" err
    
    member x.TryWait() = x.TryWait(infinite)

    member x.Wait(timeoutInNanoseconds : int64) = 
        if not (x.TryWait(timeoutInNanoseconds)) then
            raise <| TimeoutException("Fence")

    member x.Wait() = 
        if not (x.TryWait()) then
            raise <| TimeoutException("Fence")


    new(device : Device) = new Fence(device, false)

and Semaphore internal(device : Device) =
    let mutable info =
        VkSemaphoreCreateInfo(
            VkStructureType.SemaphoreCreateInfo, 0n,
            0u
        )

    let mutable handle = VkSemaphore.Null
    do VkRaw.vkCreateSemaphore(device.Handle, &&info, NativePtr.zero, &&handle)
        |> check "could not create semaphore"

    member x.Device = device
    member x.Handle = handle


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


    member x.Alloc(align : int64, size : int64) = manager.Alloc(align, size)
    member x.Free(ptr : DevicePtr) = ptr.Dispose()

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
        lock ptr (fun () ->
            if ptr.Handle.IsValid then
                heap.Remove ptr.Size
                VkRaw.vkFreeMemory(device.Handle, ptr.Handle, NativePtr.zero)
                ptr.Handle <- VkDeviceMemory.Null
                ptr.Size <- 0L
        )

and DeviceMemoryManager internal(heap : DeviceHeap, virtualSize : int64, blockSize : int64) =
    let manager = MemoryManager.createNop()
    do manager.Free(manager.Alloc(nativeint virtualSize))

    let memories = List<DeviceMemory>()

    let tryCollapse() =
        if manager.AllocatedBytes = 0n then
            for mem in memories do
                mem.Dispose()
            memories.Clear()

        else
            let e = manager.LastUsedByte |> int64
            let memid = e / blockSize |> int
            let mutable last = memories.Count - 1
            while memid < last do
                memories.[last].Dispose()
                memories.RemoveAt last
                last <- last - 1

    member internal x.Release (ptr : managedptr) =
        if not ptr.Free then
            lock manager (fun () ->
                manager.Free ptr
                tryCollapse()
            )

    member internal x.TryResize (ptr : managedptr, newSize : int64) =
        let newSize = nativeint newSize
        if newSize = ptr.Size then 
            true
        elif newSize < ptr.Size then
            manager.Realloc(ptr, nativeint newSize) |> ignore
            tryCollapse()
            true
        else
            false

    member x.Alloc(align : int64, size : int64) =
        if size > blockSize then
            heap.AllocRaw(size) :> DevicePtr
        else
            lock manager (fun () ->
                let ptr = manager.AllocAligned(nativeint align, nativeint size)

                let offset = int64 ptr.Offset
                let memid = offset / blockSize |> int
                let offset = offset % blockSize

                if memid >= memories.Count then
                    // the pointer is in an entirely new block
                    let mem = heap.AllocRaw blockSize
                    memories.Add mem
                    new ManagedDevicePtr(mem, offset, size, x, ptr) :> DevicePtr

                elif offset + size > blockSize then
                    // the allocated pointer crosses a block boundary

                    // shrink the allocated pointer to s.t. it exactly fits into the block
                    manager.Realloc(ptr, nativeint (blockSize - offset)) |> ignore

                    // try to allocate again
                    let realPtr = x.Alloc(align, size)

                    // free the temporary pointer
                    manager.Free(ptr)

                    // return the real one
                    realPtr
                else
                    // the allocated pointer lies in an existing block
                    let mem = memories.[memid]
                    new ManagedDevicePtr(mem, offset, size, x, ptr) :> DevicePtr
            )

and ManagedDevicePtr internal(memory : DeviceMemory, offset : int64, size : int64, parent : DeviceMemoryManager, ptr : managedptr) =
    inherit DevicePtr(memory, offset, size)
    override x.Dispose() = parent.Release ptr

    override x.TryResize(s : int64) =
        if parent.TryResize(ptr, s) then
            x.Size <- s
            true
        else
            false

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

and DevicePtr internal(memory : DeviceMemory, offset : int64, size : int64) =
    let mutable size = size
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

    member x.Offset = offset
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
        if memory.Heap.IsHostVisible then
            let device = memory.Heap.Device
            let mutable ptr = 0n

            VkRaw.vkMapMemory(device.Handle, memory.Handle, uint64 x.Offset, uint64 x.Size, 0u, &&ptr)
                |> check "could not map memory"

            try f ptr
            finally VkRaw.vkUnmapMemory(device.Handle, memory.Handle)
        else
            failf "cannot map host-invisible memory"


and ICommand =
    abstract member Compatible : QueueFlags
    abstract member TryEnqueue : CommandBuffer * byref<Disposable> -> bool

and IQueueCommand =
    abstract member Compatible : QueueFlags
    abstract member TryEnqueue : DeviceQueue * byref<Disposable> -> bool

and DeviceToken(device : Device, ref : ref<Option<DeviceToken>>) =
    let cleanup = List<unit -> unit>()
    let commands = List<Choice<List<ICommand>, List<IQueueCommand>>>()
    let mutable last = None
    let mutable cnt = 1
    let mutable compatible = QueueFlags.All

    let enqueue (disps : List<Disposable>) (q : DeviceQueue) (l : Choice<List<ICommand>, List<IQueueCommand>>) =
        match l with
            | Choice1Of2 cmds ->
                if cmds.Count > 0 then
                    let buffer = q.Family.DefaultCommandPool.CreateCommandBuffer CommandBufferLevel.Primary
                    buffer.Begin(CommandBufferUsage.OneTimeSubmit)
                    for cmd in cmds do 
                        let mutable disp = null
                        if cmd.TryEnqueue(buffer, &disp) then
                            if not (isNull disp) then
                                disps.Add disp
                        else
                            for d in disps do d.Dispose()
                            failf "could not enqueue commands"
                    buffer.End()
                    disps.Add { new Disposable() with member x.Dispose() = buffer.Dispose() }
                    q.Start buffer

            | Choice2Of2 cmds ->
                for cmd in cmds do
                    let mutable disp = null
                    if cmd.TryEnqueue(q, &disp) then
                        if not (isNull disp) then
                            disps.Add disp
                    else
                        for d in disps do d.Dispose()
                        failf "could not enqueue commands"
                        
    let run(clean : bool) =
        match last with
            | Some l ->
                commands.Add l
                let family = device.QueueFamilies |> Array.tryFind (fun q -> q.Flags &&& compatible <> QueueFlags.None)
                match family with
                    | Some f ->
                        let disp = new List<Disposable>()
                        let queue = f.GetQueue()
                        commands |> CSharpList.iter (enqueue disp queue)
                        queue.WaitIdle()
                        for d in disp do d.Dispose()

                    | None ->
                        failf "could not find family with compatible flags %A" compatible

                commands.Clear()
                last <- None
                compatible <- QueueFlags.All

            | None ->
                ()

        if clean then
            for c in cleanup do c()
            cleanup.Clear()

    member x.Enqueue (cmd : ICommand) =
        compatible <- compatible &&& cmd.Compatible
        let commandList = 
            match last with
                | Some (Choice1Of2 cmds) -> cmds
                | Some queueCommands ->
                    commands.Add(queueCommands)
                    let res = List()
                    last <- Some (Choice1Of2 res)
                    res
                | None -> 
                    let res = List()
                    last <- Some (Choice1Of2 res)
                    res
                        
        commandList.Add cmd

    member x.Enqueue (cmd : IQueueCommand) =
        compatible <- compatible &&& cmd.Compatible
        let queueList = 
            match last with
                | Some (Choice2Of2 queueCommands) -> queueCommands
                | Some cmd ->
                    commands.Add(cmd)
                    let res = List()
                    last <- Some (Choice2Of2 res)
                    res
                | None -> 
                    let res = List()
                    last <- Some (Choice2Of2 res)
                    res
                        
        queueList.Add cmd

    member x.AddCleanup (d : unit -> unit) =
        cleanup.Add d

    member x.Sync() = run(false)

    member internal x.AddRef() = 
        cnt <- cnt + 1

    member internal x.RemoveRef() = 
        cnt <- cnt - 1
        if cnt = 0 then 
            ref := None
            run(true)

    member x.Dispose() =
        x.RemoveRef()

    interface IDisposable with
        member x.Dispose() = x.Dispose()






[<AbstractClass; Sealed; Extension>]
type DeviceExtensions private() =

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

    [<Extension>]
    static member CreateDevice(this : PhysicalDevice, wantedLayers : Set<string>, wantedExtensions : Set<string>, queues : list<QueueFamilyInfo * int>) =
        new Device(this, wantedLayers, wantedExtensions, queues)

    [<Extension>]
    static member Alloc(this : Device, reqs : VkMemoryRequirements, preferDevice : bool) =
        if preferDevice then
            let mem = this.DeviceMemory
            if reqs.memoryTypeBits &&& mem.Mask <> 0u then
                mem.Alloc(int64 reqs.alignment, int64 reqs.size)
            else
                match tryAlloc reqs 0 this.Memories with
                    | Some mem -> mem
                    | None -> failf "could not find compatible memory for %A" reqs
        else
            match tryAlloc reqs 0 this.Memories with
                | Some mem -> mem
                | None -> failf "could not find compatible memory for %A" reqs

    [<Extension>]
    static member Alloc(this : Device, reqs : VkMemoryRequirements) =
        DeviceExtensions.Alloc(this, reqs, false)