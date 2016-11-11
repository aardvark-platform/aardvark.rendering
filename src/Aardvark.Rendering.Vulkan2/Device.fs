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
                        NativePtr.zero
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
        [
            for info in queueInfos do
                let family = physical.QueueFamilies.[int info.queueFamilyIndex]
                for i in 0 .. int info.queueCount - 1 do
                    yield family, DeviceQueue(this, device, family, i)
        ]
        |> Seq.groupBy fst 
        |> Seq.map (fun (k, vs) -> k, DeviceQueueFamily(this, k, Seq.toList (Seq.map snd vs)))
        |> HashMap.ofSeq

    let memories = 
        physical.MemoryTypes |> Array.map (fun t ->
            DeviceHeap(this, t, t.heap)
        )

    let deviceMemory = memories.[physical.DeviceMemory.index]
    let hostMemory = memories.[physical.HostMemory.index]

    let minMemoryMapAlignment = int64 physical.Limits.minMemoryMapAlignment
    let minTexelBufferOffsetAlignment = int64 physical.Limits.minTexelBufferOffsetAlignment
    let minUniformBufferOffsetAlignment = int64 physical.Limits.minUniformBufferOffsetAlignment
    let minStorageBufferOffsetAlignment = int64 physical.Limits.minStorageBufferOffsetAlignment

    let computeFamily = 
        queueFamilies 
            |> HashMap.toSeq 
            |> Seq.tryPick (fun (f,pool) -> 
                if QueueFlags.compute f.flags then
                    Some pool
                else
                    None
            )

    let graphicsFamily = 
        queueFamilies 
            |> HashMap.toSeq 
            |> Seq.tryPick (fun (f,pool) -> 
                if QueueFlags.graphics f.flags then
                    Some pool
                else
                    None
            )

    let transferFamily = 
        queueFamilies 
            |> HashMap.toSeq 
            |> Seq.tryPick (fun (f,pool) -> 
                if QueueFlags.transfer f.flags then
                    Some pool
                else
                    None
            )

    let currentResourceToken = new ThreadLocal<ref<Option<DeviceToken>>>(fun _ -> ref None)

    member x.ResourceToken =
        let ref = currentResourceToken.Value
        match !ref with
            | Some t ->
                t.AddRefCount()
                t
            | None ->
                let t = new DeviceToken(ref, transferFamily.Value.DefaultCommandPool)
                ref := Some t
                t

    member x.MinMemoryMapAlignment = minMemoryMapAlignment
    member x.MinTexelBufferOffsetAlignment = minTexelBufferOffsetAlignment
    member x.MinUniformBufferOffsetAlignment = minUniformBufferOffsetAlignment
    member x.MinStorageBufferOffsetAlignment = minStorageBufferOffsetAlignment


    member x.Instance = instance
    member x.QueueFamilies = queueFamilies

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

    abstract member Dispose : unit -> unit
    default x.Dispose() =
        if not instance.IsDisposed then
            let o = Interlocked.Exchange(&isDisposed, 1)
            if o = 0 then
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


and DeviceToken internal(cell : ref<Option<DeviceToken>>, commandPool : DeviceCommandPool) =
    let mutable cnt = 1

    let queueFamily : DeviceQueueFamily = commandPool.QueueFamily

    let cmd = 
        lazy (
            let res : CommandBuffer = commandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
            res.Begin(CommandBufferUsage.OneTimeSubmit)
            res
        )

    member x.Device = commandPool.Device
    member x.QueueFamily = commandPool.QueueFamily
    member x.CommandBuffer = cmd.Value

    member internal x.AddRefCount() =
        cnt <- cnt + 1

    interface IDisposable with
        member x.Dispose() =
            cnt <- cnt - 1
            if cnt = 0 then
                if cmd.IsValueCreated then
                    let cmd = cmd.Value
                    cmd.End()
                    queueFamily.RunSynchronously(cmd)
                    cmd.Dispose()

                cell := None

and DeviceQueue internal(device : Device, deviceHandle : VkDevice, family : QueueFamilyInfo, index : int) =
    let mutable handle = VkQueue.Zero
    do VkRaw.vkGetDeviceQueue(deviceHandle, uint32 family.index, uint32 index, &&handle)

    let transfer = QueueFlags.transfer family.flags
    let compute = QueueFlags.compute family.flags
    let graphics = QueueFlags.graphics family.flags

    member x.HasTransfer = transfer
    member x.HasCompute = compute
    member x.HasGraphics= graphics

    member x.Device = device
    member x.Family = family
    member x.Index = index
    member x.Handle = handle

and DeviceQueueFamily internal(device : Device, info : QueueFamilyInfo, queues : list<DeviceQueue>) as this =
    let store = new BlockingCollection<DeviceQueue>()
    do for q in queues do store.Add q

    let defaultPool = new DeviceCommandPool(device, info.index, this)

    member x.Device = device
    member x.Info = info
    member x.Queues = queues
    member x.DefaultCommandPool = defaultPool
    member x.CreateCommandPool() = new CommandPool(device, info.index, x)

    member x.RunSynchronously(cmd : CommandBuffer) =
        x.UsingQueue(fun queue ->
            let mutable handle = cmd.Handle
            let mutable submitInfo =
                VkSubmitInfo(
                    VkStructureType.SubmitInfo, 0n,
                    0u, NativePtr.zero, NativePtr.zero,
                    1u, &&handle,
                    0u, NativePtr.zero
                )

            let fence = device.CreateFence()

            VkRaw.vkQueueSubmit(queue.Handle, 1u, &&submitInfo, fence.Handle)
                |> check "could not submit command buffer"

            fence.Wait()
        )

    member x.UsingQueue (f : DeviceQueue -> 'a) =
        let queue = store.Take()
        try f queue
        finally store.Add queue


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
    
    let mutable recording = false
    let cleanupTasks = List<IDisposable>()

    let cleanup() =
        for c in cleanupTasks do c.Dispose()
        cleanupTasks.Clear()

    member x.Reset() =
        cleanup()

        VkRaw.vkResetCommandBuffer(handle, VkCommandBufferResetFlags.None)
            |> check "could not reset command buffer"

        recording <- false

    member x.Begin(usage : CommandBufferUsage) =
        cleanup()
        let mutable info =
            VkCommandBufferBeginInfo(
                VkStructureType.CommandBufferBeginInfo, 0n,
                unbox (int usage),
                NativePtr.zero
            )

        VkRaw.vkBeginCommandBuffer(handle, &&info)
            |> check "could not begin command buffer"

        recording <- true

    member x.End() =
        VkRaw.vkEndCommandBuffer(handle)
            |> check "could not end command buffer"
        recording <- false

    member x.Set(e : Event, flags : VkPipelineStageFlags) =
        VkRaw.vkCmdSetEvent(handle, e.Handle, flags)

    member x.Reset(e : Event, flags : VkPipelineStageFlags) =
        VkRaw.vkCmdResetEvent(handle, e.Handle, flags)
 
    member x.WaitAll(e : Event[]) =
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

    member x.IsRecording = recording
    member x.Level = level
    member x.Handle = handle
    member x.Device = device
    member x.QueueFamily = queueFamily
    member x.Pool = pool

    member x.AddCompensation (d : IDisposable) =
        cleanupTasks.Add d

    abstract member Dispose : unit -> unit
    default x.Dispose() =
        if handle <> 0n && device.Handle <> 0n then
            cleanup()
            VkRaw.vkFreeCommandBuffers(device.Handle, pool, 1u, &&handle)
            handle <- 0n

    interface IDisposable with
        member x.Dispose() = x.Dispose()


and Fence internal(device : Device, signaled : bool) =
    let mutable info =
        VkFenceCreateInfo(
            VkStructureType.FenceCreateInfo, 0n,
            (if signaled then VkFenceCreateFlags.SignaledBit else VkFenceCreateFlags.None)
        )
    let mutable handle : VkFence = VkFence.Null
    do VkRaw.vkCreateFence(device.Handle, &&info, NativePtr.zero, &&handle)
        |> check "could not create fence"

    member x.Device = device
    member x.Handle = handle

    member x.Signaled =
        if handle.IsValid then
            VkRaw.vkGetFenceStatus(device.Handle, handle) = VkResult.VkSuccess
        else
            true

    member x.Wait(timeoutInNanoseconds : uint64) =
        if handle.IsValid then
            let result = 
                VkRaw.vkWaitForFences(device.Handle, 1u, &&handle, 1u, timeoutInNanoseconds)

            if result = VkResult.VkTimeout then raise <| TimeoutException("[Vulkan] fence timed out")
            else result |> check "could not wait for fence"
            
            VkRaw.vkDestroyFence(device.Handle, handle, NativePtr.zero)
            handle <- VkFence.Null
    
    member x.Wait() = x.Wait(~~~0UL)

    member x.Dispose() =
        if handle.IsValid then
            VkRaw.vkDestroyFence(device.Handle, handle, NativePtr.zero)
            handle <- VkFence.Null

    interface IDisposable with
        member x.Dispose() = x.Dispose()



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
    member x.Device = device
    member x.Info = memory

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

    member internal x.Release (ptr : managedptr) =
        if not ptr.Free then
            lock manager (fun () ->
                manager.Free ptr
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
            )

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

and DeviceMemory internal(heap : DeviceHeap, handle : VkDeviceMemory, size : int64) =
    inherit DevicePtr(Unchecked.defaultof<_>, 0L, size)
    let mutable handle = handle
    let mutable size = size

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

and DevicePtr internal(memory : DeviceMemory, offset : int64, size : int64) =
    
    abstract member Memory : DeviceMemory
    default x.Memory = memory

    abstract member Dispose : unit -> unit
    default x.Dispose() = ()

    member x.Offset = offset
    member x.Size = size

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    member x.Borrow = new DevicePtr(memory, offset, size)
    member x.View(off : int64, s : int64) = new DevicePtr(memory, offset + off, s)
    member x.Skip(off : int64) = new DevicePtr(memory, offset + off, size - off)
    member x.Take(s : int64) = new DevicePtr(memory, offset, s)

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




[<AbstractClass; Sealed; Extension>]
type DeviceExtensions private() =

    [<Extension>]
    static member CreateDevice(this : PhysicalDevice, wantedLayers : Set<string>, wantedExtensions : Set<string>, queues : list<QueueFamilyInfo * int>) =
        new Device(this, wantedLayers, wantedExtensions, queues)

