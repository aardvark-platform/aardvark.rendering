namespace Scratch

open System
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms

#nowarn "9"

module TPL =
    


    type WorkItem = DeviceQueue -> Fence -> bool

    [<Struct>]
    type TaskInfo(submitTime : MicroTime, startTime : MicroTime, finishTime : MicroTime) =
        member x.WaitTime = startTime - submitTime
        member x.ExecutionTime = finishTime - startTime
        member x.TotalTime = finishTime - submitTime

        override x.ToString() =
            sprintf "TaskInfo { WaitTime = %A; ExecutionTime = %A }" x.WaitTime x.ExecutionTime

    type GPUTask(pool : ThreadPool) =

        static let emptyItem : WorkItem = fun _ _ -> false

        let lockObj = obj()
        let mutable state = 0
        let mutable info = Unchecked.defaultof<TaskInfo>
        let mutable exn = Unchecked.defaultof<Exception>

        let conts = List<GPUTask * (GPUTask -> WorkItem)>()

        member x.Pool = pool

        member x.Wait() =
            lock lockObj (fun () ->
                while state < 2 do 
                    Monitor.Wait lockObj |> ignore

                if state = 3 then raise exn
            )

        member x.Result =
            x.Wait()
            info

        member x.ContinueWith(item : GPUTask -> WorkItem) =
            lock lockObj (fun () ->
                if state >= 2 then 
                    pool.StartAsTask (item x)
                else
                    let task = new GPUTask(pool)
                    conts.Add(task, item)
                    task
            )

        member x.ContinueWith(item : GPUTask -> unit) =
            x.ContinueWith (fun task ->
                item task
                emptyItem
            )

        member internal x.Started() =
            lock lockObj (fun () ->
                if state <> 0 then failwithf "[GPUTask] cannot start"
                state <- 1
                Monitor.PulseAll lockObj
            )

        member internal x.Trigger(i : TaskInfo) =
            lock lockObj (fun () ->
                if state <> 1 then failwithf "[GPUTask] unstarted task cannot finish"
                info <- i
                state <- 2

                if conts.Count > 0 then
                    for (t,w) in conts do 
                        try pool.StartTask(t, w x)
                        with _ -> ()
                    conts.Clear()
                Monitor.PulseAll lockObj
            )

        member internal x.SetException(e : Exception) =
            lock lockObj (fun () ->
                if state <> 1 then failwithf "[GPUTask] unstarted task cannot finish"
                exn <- e
                state <- 3

                if conts.Count > 0 then
                    for (t,w) in conts do 
                        try pool.StartTask(t, w x)
                        with _ -> ()
                    conts.Clear()

                Monitor.PulseAll lockObj
            )
            

    and ThreadPool(queues : DeviceQueue[]) =
        
        let time = System.Diagnostics.Stopwatch.StartNew()

        let mutable running = false
        let cancel = new CancellationTokenSource()
        let items = new BlockingCollection<WorkItem * GPUTask * MicroTime>()

        let totalThreadCount = queues.Length
        let mutable activeThreadCount = 0

        let allRunning = new ManualResetEventSlim(false)

        let run (queue : DeviceQueue) (workerId : int) () =

            let device = queue.Device
            let fence = device.CreateFence()

            if Interlocked.Increment(&activeThreadCount) = totalThreadCount then
                allRunning.Set()

            allRunning.Wait()
            if workerId = 0 then
                Log.line "[VTPL] %d workers running" totalThreadCount

            try
                while true do
                    let item, signal, submitTime = items.Take(cancel.Token)
                    fence.Reset()

                    try
                        let startTime = time.MicroTime
                        let needWait = item queue fence
                        signal.Started()

                        if needWait then fence.Wait()
                        let finishTime = time.MicroTime
                        signal.Trigger(TaskInfo(submitTime, startTime, finishTime))

                    with 
                        | :? OperationCanceledException as e -> 
                            signal.SetException(e)
                        | e ->
                            Log.warn "[VTPL] worker%d: item faulted with %A" workerId e
                            signal.SetException(e)


            with 
                | :? OperationCanceledException ->
                    ()
                | e -> 
                    Log.line "[VTPL] worker%d faulted: %A" workerId e

            fence.Dispose()

            if Interlocked.Decrement(&activeThreadCount) = 0 then
                allRunning.Reset()
                Log.line "[VTPL] shutdown"

        let threads = 
            queues |> Array.mapi (fun i q -> 
                let id = i
                new Thread(
                    ThreadStart(run q id), 
                    IsBackground = true,
                    Name = sprintf "VTPL %d" i,
                    Priority = ThreadPriority.Highest
                )
            )

        member x.IsRunning = running

        member x.Start() =
            if not running then
                running <- true
                threads |> Array.iter (fun t -> t.Start())
                allRunning.Wait()

        member x.Stop() =
            if running then
                running <- false
                cancel.Cancel()
                for t in threads do t.Join()

        member x.Dispose() =
            x.Stop()
            let exn = new OperationCanceledException()
            for (_,i,_) in items do i.SetException(exn) |> ignore
            items.Dispose()
            allRunning.Dispose()



        member internal x.StartTask(task : GPUTask, item : WorkItem) =
            let submitTime = time.MicroTime
            items.Add((item, task, submitTime))
            

        member x.StartAsTask(item : WorkItem) =
            let task = new GPUTask(x)
            let submitTime = time.MicroTime
            items.Add((item, task, submitTime))
            task

        interface IDisposable with
            member x.Dispose() = x.Dispose()



    let blocking (e : Event) (src : Buffer) (dst : Buffer) (queue : DeviceQueue) (fence : Fence) =
        let cmd = queue.Family.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        cmd.Begin(CommandBufferUsage.OneTimeSubmit)


        cmd.enqueue {
            do! Command.Wait(e, VkPipelineStageFlags.TransferBit)
            do! Command.Copy(src, dst)
        }


        cmd.End()
        queue.Submit(cmd, fence)

    let other (src : Buffer) (dst : Buffer) (queue : DeviceQueue) (fence : Fence) =
        let cmd = queue.Family.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        cmd.Begin(CommandBufferUsage.OneTimeSubmit)


        cmd.enqueue {
            do! Command.Copy(src, dst)
        }

        cmd.End()
        queue.Submit(cmd, fence)
   
    let copy (src : Buffer) (dst : Buffer) = 
        let cmd = src.Device.GraphicsFamily.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        cmd.Begin(CommandBufferUsage.SimultaneousUse)
        cmd.enqueue {
            do! Command.Copy(src, dst)
        }
        cmd.End()

        fun (queue : DeviceQueue) (fence : Fence) ->
            queue.Submit(cmd, fence)
             
    module Shader =
        open FShade

        [<LocalSize(X = 64)>]
        let copyShader (src : int[]) (dst : int[]) =
            compute {
                let id = getGlobalId().X
                dst.[id] <- src.[id]
            }
    open System.Reflection
 
    let runRace() =
        use app = new HeadlessVulkanApplication(false)
        let device = app.Runtime.Device

        let copyEngine = CopyEngine(device.TransferFamily)

        let mutable pending = 0L


        let evt = new SemaphoreSlim(0)
        let size = 16L <<< 20
        let enqueueCopy(size : int64) =
            let hostBuffer = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit size
            hostBuffer.Memory.Mapped(fun ptr -> Marshal.Set(ptr, 1, size))
            let deviceBuffer = device.DeviceMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit size


            Interlocked.Add(&pending, size) |> ignore
            let destroy() =
                device.Delete hostBuffer
                device.Delete deviceBuffer
                evt.Release() |> ignore
            copyEngine.Enqueue(CopyCommand.BufferCopy(hostBuffer.Handle, deviceBuffer.Handle, VkBufferCopy(0UL, 0UL, uint64 size), destroy))
            evt.Wait()

        let rand = RandomSystem()
        let mutable running = false
        let copyRunning = new ManualResetEventSlim(false)
        let copyThreadHate () =
            while true do
                copyRunning.Wait()
                let size = 16L <<< 20 //rand.UniformLong(1L <<< 16) + 1024L
                enqueueCopy size

        let thread = Thread(ThreadStart(copyThreadHate), IsBackground = true)
        thread.Start()

        let computeThread () =
            let data = Array.init (1 <<< 23) (fun i -> i + 1)
            let a = app.Runtime.CreateBuffer(data)
            let b = app.Runtime.CreateBuffer<int>(a.Count)


            let copyCompute =
                let shader = app.Runtime.CreateComputeShader Shader.copyShader
                let inputs = shader.Runtime.NewInputBinding shader
            
                inputs.["src"] <- a
                inputs.["dst"] <- b
                inputs.Flush()


                let prog = 
                    shader.Runtime.Compile [ 
                        ComputeCommand.SetInput inputs
                        ComputeCommand.Bind shader
                        ComputeCommand.Dispatch(data.Length / 64)
                    ]

                let stream = prog.GetType().GetProperty("Stream", BindingFlags.Instance ||| BindingFlags.NonPublic).GetValue(prog) |> unbox<VKVM.CommandStream>

                let cmd = device.GraphicsFamily.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
                cmd.Begin(CommandBufferUsage.SimultaneousUse)
                cmd.AppendCommand()
                stream.Run(cmd.Handle)
                cmd.End()

            
                fun (queue : DeviceQueue) (fence : Fence) ->
                    queue.Submit(cmd, fence) |> ignore

            let fence = device.CreateFence()
            let q = device.GraphicsFamily.GetQueue()

            let sw = System.Diagnostics.Stopwatch()
            let mutable iter = 0
            while true do
                fence.Reset()
                sw.Start()
                a.Upload data
                copyCompute q fence
                fence.Wait()
                sw.Stop()
                iter <- iter + 1

                if iter >= 100 then
                    let t = sw.MicroTime / iter
                    Log.line "took: %A" t
                    iter <- 0
                    sw.Reset()




        let computeThread = Thread(ThreadStart(computeThread), IsBackground = true)
        computeThread.Start()


        while true do
            printfn "press enter to toggle upload"
            Console.ReadLine() |> ignore
            running <- not running
            if running then copyRunning.Set()
            else copyRunning.Reset()


        ()   
        
    let runTest() =
        use app = new HeadlessVulkanApplication(false)
        let device = app.Runtime.Device


        let data = Array.init (1 <<< 25) (fun i -> i + 1)
        let a = app.Runtime.CreateBuffer(data)
        let b = app.Runtime.CreateBuffer<int>(a.Count)
        let c = app.Runtime.CreateBuffer<int>(a.Count)
        let d = app.Runtime.CreateBuffer<int>(a.Count)
        
        let queue0 = device.GraphicsFamily.GetQueue()
        let queue1 = device.GraphicsFamily.GetQueue()
        use pool = new ThreadPool([| queue0; queue1 |])
        pool.Start()


        

        let a2b = pool.StartAsTask (copy (unbox a.Buffer) (unbox b.Buffer))
        let b2c = a2b.ContinueWith (fun _ -> copy (unbox b.Buffer) (unbox c.Buffer))
        let b2d = a2b.ContinueWith (fun _ -> copy (unbox b.Buffer) (unbox d.Buffer))
        b2c.Wait()
        b2d.Wait()


        let dAB = Array.fold2 (fun c a b -> if a <> b then c + 1 else c) 0 data (b.Download())
        if dAB <> 0 then Log.warn "b invalid (%A)" dAB
        
        let dAC = Array.fold2 (fun c a b -> if a <> b then c + 1 else c) 0 data (c.Download())
        if dAC <> 0 then Log.warn "c invalid (%A)" dAB

        let dAD = Array.fold2 (fun c a b -> if a <> b then c + 1 else c) 0 data (d.Download())
        if dAD <> 0 then Log.warn "d invalid (%A)" dAB


    
    type IVolumeDataProvider<'a when 'a : unmanaged> =
        abstract member Size : V3i
        abstract member BrickSize : V3i
        abstract member Levels : int
        abstract member Format : TextureFormat
        abstract member Read : level : int * brick : V3i * (NativeTensor4<'a> -> 'r) -> 'r

    type NativeDict<'k, 'v when 'k : unmanaged and 'v : unmanaged>(capacity : int, hash : 'k -> int) =
        
        let pKeys : nativeptr<'k> = NativePtr.alloc capacity
        let pValues : nativeptr<'v> = NativePtr.alloc capacity
        let pNext : nativeptr<int> = 
            let ptr = NativePtr.alloc capacity
            Marshal.Set(NativePtr.toNativeInt ptr, -1, int64 capacity * 4L)
            ptr

        let pIndices : nativeptr<int> = 
            let ptr = NativePtr.alloc capacity
            Marshal.Set(NativePtr.toNativeInt ptr, -1, int64 capacity * 4L)
            ptr

        let pLast =
            let ptr = NativePtr.alloc 1
            NativePtr.write ptr 0
            ptr

        let pCount =
            let ptr = NativePtr.alloc 1
            NativePtr.write ptr 0
            ptr

        let pFree = 
            let ptr = NativePtr.alloc 1
            NativePtr.write ptr -1
            ptr

        let incrementCount() =
            let c = NativePtr.read pCount
            if c >= capacity then failwith "[NativeDict] out of memory"
            NativePtr.write pCount (c + 1)

        let decrementCount() =
            let c = NativePtr.read pCount
            NativePtr.write pCount (c - 1)

        let incrementLast() =
            let c = NativePtr.read pLast
            if c >= capacity then failwith "[NativeDict] out of memory"
            NativePtr.write pLast (c + 1)
            c

        let newDataSlot () =
            incrementCount()

            let fi = NativePtr.read pFree
            if fi < 0 then
                let slot = incrementLast()
                slot
            else
                let next = NativePtr.get pNext fi
                NativePtr.set pNext fi -1
                NativePtr.write pFree next
                fi

        member x.Capacity = capacity
        member x.PIndices = pIndices
        member x.PKeys = pKeys
        member x.PValues = pValues
        member x.PNext = pNext
        member x.PLast = pLast
        member x.PCount = pCount


        member x.Set(key : 'k, value : 'v) =
            let hash = hash key
            let id = abs hash % capacity

            let eid = NativePtr.get pIndices id
            if eid < 0 then
                // free slot
                let slot = newDataSlot()
                NativePtr.set pIndices id slot
                NativePtr.set pKeys slot key
                NativePtr.set pValues slot value

            else
                let ck = NativePtr.get pKeys eid
                if Unchecked.equals key ck then
                    // override
                    NativePtr.set pValues eid value
                else
                    let mutable found = false
                    let mutable last = eid
                    let mutable eid = NativePtr.get pNext last
                    while not found && eid >= 0 do
                        let ck = NativePtr.get pKeys eid
                        if Unchecked.equals key ck then
                            // override
                            NativePtr.set pValues eid value
                            found <- true
                        else
                            last <- eid
                            eid <- NativePtr.get pNext last

                    if not found then
                        let slot = newDataSlot()
                        NativePtr.set pNext last slot
                        NativePtr.set pKeys slot key
                        NativePtr.set pValues slot value
        
        member x.TryGet(key : 'k) =
            let hash = hash key
            let id = abs hash % capacity

            let mutable eid = NativePtr.get pIndices id
            if eid >= 0 then
                
                let mutable resultId = -1

                while resultId < 0 && eid >= 0 do
                    let ck = NativePtr.get pKeys eid
                    if Unchecked.equals key ck then
                        resultId <- eid
                    else
                        eid <- NativePtr.get pNext eid
                
                if resultId >= 0 then
                    Some (NativePtr.get pValues resultId)
                else
                    None
            else
                None

        member x.Count = NativePtr.read pCount

        member x.Remove(key : 'k) =
            let hash = hash key
            let id = abs hash % capacity

            let mutable last = -1
            let mutable eid = NativePtr.get pIndices id

            if eid >= 0 then
                let mutable found = false
                while not found && eid >= 0 do
                    let ck = NativePtr.get pKeys eid
                    if Unchecked.equals key ck then
                        if last < 0 then
                            let next = NativePtr.get pNext eid
                            NativePtr.set pIndices id next
                        else
                            let next = NativePtr.get pNext eid
                            NativePtr.set pNext last next

                        let cnt = NativePtr.read pLast
                        if eid = cnt - 1 then
                            NativePtr.write pLast (cnt - 1)
                        else
                            let f = NativePtr.read pFree
                            NativePtr.set pNext eid f
                            NativePtr.write pFree eid

                        decrementCount()
                        found <- true
                    else
                        last <- eid
                        eid <- NativePtr.get pNext eid
                
                found
            else
                false

    type SparseVolume<'a when 'a : unmanaged>(device : Device, data : IVolumeDataProvider<'a>) =
        static let ceilDiv (a : V3i) (b : V3i) =
            V3i(
                (if a.X % b.X = 0 then a.X / b.X else 1 + a.X / b.X),
                (if a.Y % b.Y = 0 then a.Y / b.Y else 1 + a.Y / b.Y),
                (if a.Z % b.Z = 0 then a.Z / b.Z else 1 + a.Z / b.Z)
            )

        static let brickHash (level : int) (v : V3i) =
            (v.Z <<< 24) ||| (v.Y <<< 16) ||| (v.X <<< 8) ||| level

        let channels, typ = 
            let pix = TextureFormat.toDownloadFormat data.Format
            pix.ChannelCount, pix.Type

        let bSize = data.BrickSize

        let bSizeInBytes = int64 bSize.X * int64 bSize.Y * int64 bSize.Z * int64 channels * int64 sizeof<'a>
        let sbCount = V3i(10,10,10)
        let stCount = sbCount.X * sbCount.Y * sbCount.Z

        let image = device.CreateImage(sbCount * bSize, 1, 1, 1, TextureDimension.Texture3D, TextureFormat.R16, VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.TransferDstBit)

        

        let dbCount = ceilDiv data.Size bSize
        let dtCount = dbCount.X * dbCount.Y * dbCount.Z

        let brickLock = obj()

        let mutable freeBricks =
            [ 
                for z in 0 .. dbCount.Z - 1 do
                    for y in 0 .. dbCount.Y - 1 do
                        for x in 0 .. dbCount.X - 1 do
                            yield V4i(x,y,z,0)  
            ]

        let alloc () =
            lock brickLock (fun () ->
                match freeBricks with
                    | f :: r ->
                        freeBricks <- r
                        f
                    | [] ->
                        failwith "out of memory"
            )
        
        let free (b : V4i) =
            lock brickLock (fun () ->
                freeBricks <- b :: freeBricks
            )

        let dict = NativeDict<V4i, V4i>(dtCount, fun v -> brickHash v.W v.XYZ)

        let mutable allBuffers = []

        let tempBuffer =
            new ThreadLocal<Buffer>(fun () ->
                let tempBuffer = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit bSizeInBytes
                Interlocked.Change(&allBuffers, fun bs -> tempBuffer :: bs) |> ignore
                tempBuffer
            )

        let bIndices = device.Runtime.CreateBuffer<int>(dict.Capacity)
        let bNext = device.Runtime.CreateBuffer<int>(dict.Capacity)
        let bKeys = device.Runtime.CreateBuffer<V4i>(dict.Capacity)
        let bValues = device.Runtime.CreateBuffer<V4i>(dict.Capacity)

        member x.Commit(brick : V4i) =
            match dict.TryGet brick with
                | Some b ->
                    b
                | None ->
                    let tempBuffer = tempBuffer.Value
                    data.Read(brick.W, brick.XYZ, fun src ->
                        tempBuffer.Memory.MappedTensor4(V4i(bSize, channels), fun dst ->
                            NativeTensor4.copy src dst
                        )
                    )

                    let b = alloc()
                    dict.Set(brick, b)
                    let offset = b.XYZ * bSize
                    device.perform {
                        do! Command.TransformLayout(image, VkImageLayout.TransferDstOptimal)
                        do! Command.Copy(tempBuffer, 0L, V2i.Zero, image.[ImageAspect.Color, 0, 0], offset, bSize)
                        do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)
                    }
                    b

        member x.Decommit(brick : V4i) =
            match dict.TryGet brick with
                | Some b ->
                    dict.Remove(brick) |> ignore
                    free b
                    true
                | None ->
                    false

        member x.Flush() =
            let cap = nativeint dict.Capacity
            
            use token = device.Token
            bIndices.Upload(NativePtr.toNativeInt dict.PIndices, cap * nativeint sizeof<int>)
            bNext.Upload(NativePtr.toNativeInt dict.PNext, cap * nativeint sizeof<int>)
            bKeys.Upload(NativePtr.toNativeInt dict.PKeys, cap * nativeint sizeof<V4i>)
            bValues.Upload(NativePtr.toNativeInt dict.PValues, cap * nativeint sizeof<V4i>)
            token.Sync()

        member x.BIndices = Mod.constant (bIndices.Buffer :> IBuffer)
        member x.BNext = Mod.constant (bNext.Buffer :> IBuffer)
        member x.BKeys = Mod.constant (bKeys.Buffer :> IBuffer)
        member x.BValues = Mod.constant (bValues.Buffer :> IBuffer)
        member x.Capacity = Mod.constant dict.Capacity

    type DummyData(size : V3i) =
        let brickSize = V3i(128, 128, 128)
        let brickPixels = brickSize.X * brickSize.Y * brickSize.Z

        member x.Read(level : int, brick : V3i, f : NativeTensor4<uint16> -> 'r) =
            let ptr = NativePtr.alloc brickPixels
            let rand = RandomSystem()
            for i in 0 .. brickPixels - 1 do
                NativePtr.set ptr i (rand.UniformInt(65536) |> uint16)

            
            let tensor = NativeTensor4<uint16>(ptr, Tensor4Info(V4i(brickSize, 1)))
            let res = f tensor
            NativePtr.free ptr

            res

        member x.BrickTrafo(brick : V4i) =
            let s = V3d.III / float (1 <<< brick.W)
            M44f.Translation(V3f (s * V3d brick.XYZ)) * M44f.Scale(V3f s)
            //Trafo3d.Scale(s) * Trafo3d.Translation(s * V3d brick.XYZ)



        interface IVolumeDataProvider<uint16> with
            member x.Size = size
            member x.BrickSize = brickSize
            member x.Levels = 1
            member x.Format = TextureFormat.R16
            member x.Read(level, brick, f) = x.Read(level, brick, f)

    module Shader2 =
        open FShade 

        type Vertex =
            {
                [<Position>] pos : V4d
                [<Color>] c : V4d
                [<Semantic("BrickId")>] id : V4i
            }

        type UniformScope with
            member x.Capacity : int = uniform?Capacity
            member x.Indices : int[] = uniform?StorageBuffer?Indices
            member x.Next : int[] = uniform?StorageBuffer?Next
            member x.Keys : V4i[] = uniform?StorageBuffer?Keys
            member x.Values : V4i[] = uniform?StorageBuffer?Values

        [<ReflectedDefinition>]
        let hash (b : V4i) =
            (b.Z <<< 24) ||| (b.Y <<< 16) ||| (b.X <<< 8) ||| b.W


        let sample (v : Vertex) =
            vertex {
                let id = v.id
                let pos = abs (hash id) % uniform.Capacity


                let mutable resId = -1
                let mutable eid = uniform.Indices.[pos]
                while resId < 0 && eid >= 0 do
                    let k = uniform.Keys.[eid]
                    if k = id then
                        resId <- eid
                    else
                        eid <- uniform.Next.[eid]


                if resId < 0 then
                    return { v with c = V4d.IOOI }
                else
                    let offset = uniform.Values.[resId].XYZ + V3i(1,1,1) |> V3d
                    let rOffset = offset / V3d(12.0, 12.0, 12.0)

                    return { v with c = V4d(rOffset, 1.0) }
                    
            }

    let run() =
        runRace()
        Environment.Exit 0

        use app = new VulkanApplication(true)
        let device = app.Runtime.Device

        let data = DummyData(V3i(1024, 1024, 1024))
        let v = SparseVolume(device, data)

        let b0 = V4i(0,0,0,0)
        let b1 = V4i(0,1,0,0)
        let b2 = V4i(2,0,0,0)

        v.Commit(b0) |> ignore
        v.Commit(b1) |> ignore
        v.Commit(b2) |> ignore

//        v.Decommit(b0) |> printfn "decommit(%A): %A" b0
//        v.Decommit(b1) |> printfn "decommit(%A): %A" b1
//        v.Decommit(b2) |> printfn "decommit(%A): %A" b2

        v.Flush()


        let ids = [| b0; b1; b2 |]
        let trafos = ids |> Array.map data.BrickTrafo //[| data.BrickTrafo b0; data.BrickTrafo b1; data.BrickTrafo b2  |]


            
        let win = app.CreateSimpleRenderWindow()
        let view = 
            CameraView.lookAt (V3d(4,4,4)) V3d.Zero V3d.OOI
                |> DefaultCameraController.control win.Mouse win.Keyboard win.Time

        let proj =
            win.Sizes |> Mod.map (fun s -> 
                Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y)
            )


        let instanced (trafos : IMod<M44f[]>) (ids : IMod<V4i[]>) (sg : ISg) : ISg =
            Sg.InstancingNode(trafos, Map.ofList ["BrickId", Aardvark.Base.BufferView(ids |> Mod.map (fun a -> ArrayBuffer a :> IBuffer), typeof<V4i>)], Mod.constant sg) :> ISg

        let task =
            Primitives.unitBox
                |> Sg.ofIndexedGeometry
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! Shader2.sample
                    do! DefaultSurfaces.simpleLighting
                }
                |> instanced (Mod.constant trafos) (Mod.constant ids)
                |> Sg.cullMode (Mod.constant CullMode.Clockwise)

                |> Sg.uniform "Indices" v.BIndices
                |> Sg.uniform "Next" v.BNext
                |> Sg.uniform "Keys" v.BKeys
                |> Sg.uniform "Values" v.BValues
                |> Sg.uniform "Capacity" v.Capacity

                |> Sg.viewTrafo (Mod.map CameraView.viewTrafo view)
                |> Sg.projTrafo (Mod.map Frustum.projTrafo proj)
                |> Sg.compile win.Runtime win.FramebufferSignature

        win.RenderTask <- task
        win.Run()

        Environment.Exit 0



        let d = new NativeDict<int, int>(1024, fun i -> 0)

        d.Set(1, 1)
        d.Set(2, 2)
        d.Set(5, 5)

        printfn "1: %A" (d.TryGet(1))
        printfn "2: %A" (d.TryGet(2))
        printfn "5: %A" (d.TryGet(5))
        printfn "9: %A" (d.TryGet(9))
        printfn "count: %A" d.Count

        printfn "rem(1): %A" (d.Remove 1)
        
        printfn "count: %A" d.Count
        printfn "1: %A" (d.TryGet(1))
        printfn "2: %A" (d.TryGet(2))
        printfn "5: %A" (d.TryGet(5))
        printfn "9: %A" (d.TryGet(9))
        
        d.Set(9, 9)
        printfn "count: %A" d.Count
        printfn "1: %A" (d.TryGet(1))
        printfn "2: %A" (d.TryGet(2))
        printfn "5: %A" (d.TryGet(5))
        printfn "9: %A" (d.TryGet(9))

        
        d.Set(9, 99)
        printfn "count: %A" d.Count
        printfn "1: %A" (d.TryGet(1))
        printfn "2: %A" (d.TryGet(2))
        printfn "5: %A" (d.TryGet(5))
        printfn "9: %A" (d.TryGet(9))




//        runTest()
//        Environment.Exit 0

        let memorySize (s : V3i) =
            Mem (int64 s.X * int64 s.Y * int64 s.Z * 2L)

        let brickSize = V3i(128,128,128)
        let mutable bricks = 10

        Log.line "memory: %A" device.DeviceMemory.Capacity
        Log.line "used:   %A" (memorySize (bricks * brickSize))
        Log.line "bricks: %A" bricks

        let tex = device.CreateImage(bricks * brickSize, 1, 1, 1, TextureDimension.Texture3D, TextureFormat.R16, VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.TransferDstBit)

        let brickHash (level : int) (v : V3i) =
            (v.Z <<< 24) ||| (v.Y <<< 16) ||| (v.X <<< 8) ||| level



        device.Delete tex
        Environment.Exit 0


        let data = Array.init (1 <<< 20) id
        let a = app.Runtime.CreateBuffer(data)
        let b = app.Runtime.CreateBuffer<int>(a.Count)
        let c = app.Runtime.CreateBuffer<int>(a.Count)

        let e = device.CreateEvent()


        let queue0 = device.GraphicsFamily.GetQueue()
        let queue1 = device.GraphicsFamily.GetQueue()

        use pool = new ThreadPool([| queue0; queue1 |])
        pool.Start()

        let b2c = pool.StartAsTask(fun q -> Log.line "b2c start"; blocking e (unbox b.Buffer) (unbox c.Buffer) q)
        Thread.Sleep 1000
        let a2b = pool.StartAsTask(fun q -> Log.line "a2b start"; other (unbox a.Buffer) (unbox b.Buffer) q)

        use sem = new SemaphoreSlim(0)

        b2c.ContinueWith (fun (t : GPUTask) -> Log.line "b2c done (%A)" t.Result; sem.Release() |> ignore) |> ignore
        a2b.ContinueWith (fun (t : GPUTask) -> Log.line "a2b done (%A)" t.Result; Log.line "set event"; e.Set()) |> ignore

        sem.Wait()

        let bValid = b.Download() = data
        let cValid = c.Download() = data

        Log.line "b: %A" bValid
        Log.line "c: %A" cValid

        pool.Stop()

        ()


