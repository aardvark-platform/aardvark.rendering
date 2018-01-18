namespace Scratch

open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan

module TPL =
    


    type WorkItem = DeviceQueue -> Fence

    [<Struct>]
    type TaskInfo(submitTime : MicroTime, startTime : MicroTime, finishTime : MicroTime) =
        member x.WaitTime = startTime - submitTime
        member x.ExecutionTime = finishTime - startTime
        member x.TotalTime = finishTime - submitTime

        override x.ToString() =
            sprintf "TaskInfo { WaitTime = %A; ExecutionTime = %A }" x.WaitTime x.ExecutionTime

    type ThreadPool(queues : DeviceQueue[], threadsPerQueue : int) =
        
        let time = System.Diagnostics.Stopwatch.StartNew()

        let mutable running = false
        let cancel = new CancellationTokenSource()
        let items = new BlockingCollection<WorkItem * TaskCompletionSource<TaskInfo> * MicroTime>()

        let totalThreadCount = queues.Length * threadsPerQueue
        let mutable activeThreadCount = 0

        let allRunning = new ManualResetEventSlim(false)

        let run (queue : DeviceQueue) (workerId : int) () =

            if Interlocked.Increment(&activeThreadCount) = totalThreadCount then
                allRunning.Set()

            allRunning.Wait()
            if workerId = 0 then
                Log.line "[VTPL] %d workers running" totalThreadCount

            try
                while true do
                    let item, signal, submitTime = items.Take(cancel.Token)

                    try
                        let startTime = time.MicroTime
                        let fence = item queue
                        fence.Wait()
                        let finishTime = time.MicroTime

                        signal.SetResult(TaskInfo(submitTime, startTime, finishTime))
                    with 
                        | :? OperationCanceledException -> 
                            signal.SetCanceled()
                        | e ->
                            Log.warn "[VTPL] worker%d: item faulted with %A" workerId e
                            signal.SetException(e)


            with 
                | :? OperationCanceledException ->
                    ()
                | e -> 
                    Log.line "[VTPL] worker%d faulted: %A" workerId e

            if Interlocked.Decrement(&activeThreadCount) = 0 then
                allRunning.Reset()
                Log.line "[VTPL] shutdown"

        let threads = 
            let mutable i = 0
            queues |> Array.collect (fun q ->
                Array.init threadsPerQueue (fun _ -> 
                    let id = i
                    i <- i + 1
                    new Thread(
                        ThreadStart(run q id), 
                        IsBackground = true,
                        Name = sprintf "VTPL %d" i,
                        Priority = ThreadPriority.Highest
                    )
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
            for (_,i,_) in items do i.TrySetCanceled() |> ignore
            items.Dispose()
            allRunning.Dispose()

        member x.StartAsTask(item : WorkItem) =
            let submitTime = time.MicroTime
            let res = TaskCompletionSource<TaskInfo>()
            items.Add((item, res, submitTime))
            res.Task

        interface IDisposable with
            member x.Dispose() = x.Dispose()



    let blocking (e : Event) (src : Buffer) (dst : Buffer) (queue : DeviceQueue) =
        let cmd = queue.Family.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        cmd.Begin(CommandBufferUsage.OneTimeSubmit)


        cmd.enqueue {
            do! Command.Wait(e, VkPipelineStageFlags.TransferBit)
            do! Command.Copy(src, dst)
        }


        cmd.End()
        queue.StartFence cmd |> Option.get

    let other (src : Buffer) (dst : Buffer) (queue : DeviceQueue) = 
        let cmd = queue.Family.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        cmd.Begin(CommandBufferUsage.OneTimeSubmit)


        cmd.enqueue {
            do! Command.Copy(src, dst)
        }


        cmd.End()
        queue.StartFence cmd |> Option.get
   
    let copy (src : Buffer) (dst : Buffer) = 
        let cmd = src.Device.GraphicsFamily.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        cmd.Begin(CommandBufferUsage.SimultaneousUse)
        cmd.enqueue {
            do! Command.Copy(src, dst)
        }
        cmd.End()

        fun (queue : DeviceQueue) ->
            queue.StartFence cmd |> Option.get
             
 
    let runRace() =
        use app = new HeadlessVulkanApplication(false)
        let device = app.Runtime.Device


        let data = Array.init (1 <<< 25) id
        let a = app.Runtime.CreateBuffer(data)
        let b = app.Runtime.CreateBuffer<int>(a.Count)
        let c = app.Runtime.CreateBuffer<int>(a.Count)

        let e = device.CreateEvent()


        let queue0 = device.GraphicsFamily.GetQueue()
        let queue1 = device.GraphicsFamily.GetQueue()

        use pool = new ThreadPool([| queue0; queue1 |], 1)
        pool.Start()


        let b2c =  copy (unbox b.Buffer) (unbox c.Buffer)
        let a2b =  copy (unbox a.Buffer) (unbox b.Buffer)

        Report.Begin("testing for race")
        for i in 1 .. 1000 do
            device.perform {
                do! Command.ZeroBuffer(unbox b.Buffer)
                do! Command.ZeroBuffer(unbox c.Buffer)
            }
            let a2b = pool.StartAsTask(a2b)
            let b2c = pool.StartAsTask(b2c)

            let info1 = b2c.Result
            let info0 = a2b.Result


            let b = b.Download()
            let c = c.Download()

            let dAB = Array.fold2 (fun c a b -> if a <> b then c + 1 else c) 0 data b
            let dAC = Array.fold2 (fun c a b -> if a <> b then c + 1 else c) 0 data c

            if dAB <> 0 then
                Log.warn "b invalid (%A)" dAB

            if dAC <> 0 then
                Log.line "c invalid (%d)" dAC

            Report.Progress(float i / 1000.0)

        pool.Stop()

        Log.stop()

        ()   

    let run() =
        runRace()
        Environment.Exit 0

        use app = new HeadlessVulkanApplication(false)
        let device = app.Runtime.Device


        let data = Array.init (1 <<< 20) id
        let a = app.Runtime.CreateBuffer(data)
        let b = app.Runtime.CreateBuffer<int>(a.Count)
        let c = app.Runtime.CreateBuffer<int>(a.Count)

        let e = device.CreateEvent()


        let queue0 = device.GraphicsFamily.GetQueue()
        let queue1 = device.GraphicsFamily.GetQueue()

        use pool = new ThreadPool([| queue0; queue1 |], 1)
        pool.Start()

        let b2c = pool.StartAsTask(fun q -> Log.line "b2c start"; blocking e (unbox b.Buffer) (unbox c.Buffer) q)
        Thread.Sleep 1000
        let a2b = pool.StartAsTask(fun q -> Log.line "a2b start"; other (unbox a.Buffer) (unbox b.Buffer) q)

        use sem = new SemaphoreSlim(0)

        b2c.ContinueWith (fun (t : Task<TaskInfo>) -> Log.line "b2c done (%A)" t.Result; sem.Release() |> ignore) |> ignore
        a2b.ContinueWith (fun (t : Task<TaskInfo>) -> Log.line "a2b done (%A)" t.Result; Log.line "set event"; e.Set()) |> ignore

        sem.Wait()

        let bValid = b.Download() = data
        let cValid = c.Download() = data

        Log.line "b: %A" bValid
        Log.line "c: %A" cValid

        pool.Stop()

        ()


