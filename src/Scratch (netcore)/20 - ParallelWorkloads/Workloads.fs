namespace Aardvark.Base

open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Concurrent
open System.Runtime.InteropServices

open Aardvark.Rendering
open System.IO

type Worker(name: string,
            degreeOfParallelism : int, 
            globalExecutionContext     : unit -> IDisposable, 
            perElementExecutionContext : unit -> IDisposable,
            useThread : bool) = 

    let bc = new BlockingCollection<unit -> unit>()

    member x.StartAsTasks(ct : CancellationToken) : Task =
        let runningTasks = ref degreeOfParallelism
        let allWorkersGone = new TaskCompletionSource<_>()
        let run () = 
            try
                try
                    use __ = globalExecutionContext()
                    for f in bc.GetConsumingEnumerable(ct) do
                        try
                            use __ = perElementExecutionContext()
                            f()
                        with e -> 
                            Log.warn "[Worker/%s] item function crashed: %A" name e
                with e -> 
                    match e with
                    | :? OperationCanceledException as o -> 
                        Log.line "[Worker/%s] cancelled" name
                    | _ -> 
                        Log.warn "[Worker/%s] item function crashed: %A" name e
            finally
                if Interlocked.Decrement(runningTasks) <= 0 then
                    allWorkersGone.SetResult(obj())
                
        if useThread then
            [0 .. degreeOfParallelism - 1] |> List.iter (fun i -> 
                let thread = Thread(ThreadStart run)
                thread.Priority <- ThreadPriority.Lowest
                thread.Start()
            )
        else
            [0 .. degreeOfParallelism - 1] |> List.iter (fun i -> 
                Task.Factory.StartNew(run, ct, TaskCreationOptions.LongRunning, TaskScheduler.Current) |> ignore
            )

        allWorkersGone.Task

    member x.Enqueue(f : unit -> unit) =
        bc.Add f

    member x.Status() = 
        String.Format($"{bc.Count}/{bc.BoundedCapacity}")

    member x.Load() = bc.Count

    member x.Name = name


module Worker = 

    let execute (f : unit -> 'a) (worker : Worker) = 
        let tcs = new TaskCompletionSource<'a>()
        let enqueue () = 
            let r = f()
            tcs.SetResult(r)
        worker.Enqueue(enqueue)
        tcs.Task



type Gpu = class end
type Sync = class end
type CpuCompute = class end
type Transfer = class end
type IO = class end

type Kind = Gpu | Control | Compute | Transfer | IO | ThreadPool

type PipeState = 
    { finalizers : list<unit->unit>; 
      run : Kind -> (unit -> unit) -> unit
    }
type Pipe<'kind, 'a> = { run : PipeState -> PipeState * 'a }

type ItemResult<'a> =   
    | Succeeded of 'a
    | Cancelled of exn
    | Exception of exn

module Pipe =

    let enqueue (pipeState : PipeState) (kind : Kind) (f : unit -> 'a) : MVar<ItemResult<'a>> =
        let tcs = new MVar<ItemResult<'a>>()
        let run () =
            try
                let r = f()
                tcs.Put (Succeeded r)
            with e -> 
                match e with
                | :? OperationCanceledException as o -> 
                    tcs.Put(Cancelled o)
                | _ -> 
                    tcs.Put(Exception e)
        pipeState.run kind run |> ignore
        tcs


    let runTpl (pipeState : PipeState) (f : unit -> 'a) : Task<'a> =
        let tcs = new TaskCompletionSource<_>()
        let run () =
            try
                let r = f()
                tcs.SetResult r
            with e -> 
                match e with
                | :? OperationCanceledException as o -> 
                    tcs.SetException o
                | _ -> 
                    tcs.SetException e
        pipeState.run Kind.Control run |> ignore
        tcs.Task

    let private handleError (s : PipeState) (r : MVar<ItemResult<'a>>) = 
        match r.Take() with
        | Succeeded v -> s, v
        | Exception e -> raise e
        | Cancelled o -> raise o

    let uploadTexture (runtime : IRuntime) (t : INativeTexture) : Pipe<Transfer, IBackendTexture>  =
        { run = fun s -> 
            let f () =
                let r = runtime.PrepareTexture(t)
                r

            enqueue s Kind.Transfer f |> handleError s
        }

    let loadTexture (loader : IPixLoader) (fileName : string) : Pipe<IO, PixImage> =
        { run = fun s -> 
            let load () =
                PixImage.Load(fileName, loader) 

            enqueue s Kind.IO load |> handleError s
        }

    let readAllBytes (fileName : string) : Pipe<IO, byte[]> =
        { run = fun s -> 
            let load () =
                File.readAllBytes fileName
            enqueue s Kind.IO load |> handleError s
        }

    let imageFromBytes (loader : IPixLoader) (bytes : byte[]) : Pipe<CpuCompute, PixImage> = 
        {
            run = fun s -> 
                let run () =
                    let sw = System.Diagnostics.Stopwatch.StartNew()
                    use mem = new MemoryStream(bytes)
                    let r = PixImage.Load (mem, loader)
                    sw.Stop()
                    Console.WriteLine(sw.Elapsed.TotalMilliseconds)
                    r
                enqueue s Kind.Compute run  |> handleError s
        }

    let prepareForUpload (pi : PixImage) : Pipe<CpuCompute, INativeTexture> =
        let run () =
            let img = pi.ToPixImage<byte>(Col.Format.RGBA)

            let tex = 
                { new INativeTexture with   
                    member x.WantMipMaps = false
                    member x.Format = TextureFormat.Rgba8
                    member x.MipMapLevels = 1
                    member x.Count = 1
                    member x.Dimension = TextureDimension.Texture2D
                    member x.Item
                        with get(slice : int, level : int) = 
                            { new INativeTextureData with
                                member x.Size = V3i(img.Size, 1)
                                member x.SizeInBytes = img.Volume.Data.LongLength
                                member x.Use (action : nativeint -> 'a) =
                                    let gc = GCHandle.Alloc(img.Volume.Data, GCHandleType.Pinned)
                                    try action (gc.AddrOfPinnedObject())
                                    finally gc.Free()
                            }
                }

            tex
        { run = fun s -> 
            enqueue s Kind.Compute run  |> handleError s
        }


    let bind (f : 'a -> Pipe<_, 'b>) (pipe : Pipe<_, 'a>) : Pipe<_,'b> = 
        { run = fun s -> 
            let (s,r) = pipe.run s
            (f r).run s
        }



    type PipeBuilder() = 
        member x.Bind(a, f) = bind f a
        member x.Return v = 
            { run = 
                fun s -> 
                    s, v
            }
        

    let pipe = PipeBuilder()


    module Workloads =

        let cache = new ConcurrentDictionary<IRuntime, PipeState>()

        let throughput (runtime : IRuntime) (ct : CancellationToken) =  
            let empty () = { new IDisposable with member x.Dispose() = () }

            let glRuntime = runtime |> unbox<Aardvark.Rendering.GL.Runtime>
            let gpuContext = glRuntime.Context.CreateContext()
            let uploadContext = glRuntime.Context.CreateContext()
        
            let ioWorker = new Worker("io", 1, empty, empty, false)
            let cpuWorker = new Worker("cpu", 2, empty, empty, false)
            let control = new Worker("control", 2, empty, empty, false)
            let gpuTransfer = new Worker("transfer", 1, empty, (fun _ -> glRuntime.Context.RenderingLock(uploadContext)), false)
            let gpu = new Worker("gpu", 1, empty, (fun _ ->  glRuntime.Context.RenderingLock(gpuContext)), false)

            let workers = [ioWorker; cpuWorker; control; gpuTransfer; gpu]
            let report() = 
                let bottlneck = workers |> Seq.maxBy (fun w -> w.Load())
                Log.line "thread pool threads: %d" ThreadPool.ThreadCount
                Log.line "bottlneck: %s %s" bottlneck.Name (bottlneck.Status())
                Log.line "io: %s" (ioWorker.Status())
                Log.line "cpuWorker: %s" (cpuWorker.Status())
                Log.line "control: %s" (control.Status())
                Log.line "gpuTransfer: %s" (gpuTransfer.Status())
                Log.line "gpu: %s" (gpu.Status())

            let reporting = 
                let r =
                    async {
                        while not ct.IsCancellationRequested do
                            report ()
                            do! Async.Sleep 1000
                    } 
                Async.Start(r, ct)

            let s = { 
                    finalizers = []
                    run = fun kind f -> 
                        match kind with
                        | Kind.Gpu -> ioWorker.Enqueue f
                        | Kind.ThreadPool -> ThreadPool.QueueUserWorkItem(fun o -> f()) |> ignore
                        | Kind.Control -> control.Enqueue f
                        | Kind.Compute -> cpuWorker.Enqueue f
                        | Kind.Transfer -> gpuTransfer.Enqueue f
                        | Kind.IO -> ioWorker.Enqueue f
                }
            s, Task.WhenAll [| ioWorker.StartAsTasks(ct);  cpuWorker.StartAsTasks(ct); gpuTransfer.StartAsTasks(ct); gpu.StartAsTasks(ct); control.StartAsTasks(ct);  |]
            

    let run (env : PipeState) (p : Pipe<'s, 'a>) =
        let s, r = p.run env
        r

    let par (env : PipeState) (xs : seq<Pipe<'s,'a>>) =
        let elements = xs |> Seq.toArray
        let mutable result = []
        let l = obj()
        let tasks = 
            elements 
            |> Array.Parallel.map (fun e -> 
                runTpl env (fun () -> 
                    let s,r = e.run env
                    lock l (fun _ -> 
                        result <- r :: result
                    )
                ) :> Task
            )
        Task.WaitAll(tasks)
    
        result
