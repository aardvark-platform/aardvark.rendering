namespace Aardvark.Rendering

open System.Collections.Generic
open Aardvark.Base
open FSharp.Data.Traceable
open FSharp.Data.Adaptive

type ILodDataNode =
    abstract member Id : obj
    abstract member Level : int
    abstract member Bounds : Box3d
    abstract member LocalPointCount : int64
    abstract member Children : Option<array<ILodDataNode>>

[<CustomEquality; NoComparison>]
type LodDataNode =
    {
        id : obj
        level : int
        bounds : Box3d
        pointCountTree : int64
        pointCountNode : int64
        children : Option<array<LodDataNode>>
    }

    override x.GetHashCode() = x.id.GetHashCode()
    override x.Equals o =
        match o with
            | :? LodDataNode as o -> x.id.Equals(o.id)
            | _ -> false

    interface ILodDataNode with
        member x.Id = x.id
        member x.Bounds = x.bounds
        member x.Level = x.level
        member x.LocalPointCount = x.pointCountNode
        member x.Children = x.children |> (Option.map (Array.map (fun l -> l :> ILodDataNode)))

type ILodData =
    abstract member BoundingBox : Box3d
    abstract member RootNode : unit -> ILodDataNode
    abstract member Dependencies : list<IAdaptiveValue>
    abstract member GetData : node : ILodDataNode -> Async<Option<IndexedGeometry>>

module LodData =
    


    type Decider = Trafo3d -> Trafo3d -> V2i -> ILodDataNode -> bool
    
    let defaultLodDecider (targetPixelDistance : float) (viewTrafo : Trafo3d) (projTrafo : Trafo3d) (viewPortSize : V2i) (node : ILodDataNode )  =
        let bounds = node.Bounds

        let vp = viewTrafo * projTrafo

        let nearPlaneAreaInPixels =
            let npp= bounds.ComputeCorners()
                     |> Array.map (vp.Forward.TransformPosProj >> Vec.xy)
                     |> Polygon2d

            let npp = npp.ComputeConvexHullIndexPolygon().ToPolygon2d().Points
                      |> Seq.map ( fun p -> V2d(0.5 * p.X + 0.5, 0.5 - 0.5 * p.Y) * V2d viewPortSize )
                      |> Polygon2d
                      
            npp.ComputeArea()

        let averagePointDistanceInPixels = sqrt (nearPlaneAreaInPixels / float node.LocalPointCount)

        averagePointDistanceInPixels > targetPixelDistance

    type SetRasterizer = Trafo3d -> Trafo3d -> V2i -> FastHull3d -> ILodDataNode -> ISet<ILodDataNode>

    let defaultRasterizeSet (targetPixelDistance : float) (viewTrafo : Trafo3d) (projTrafo : Trafo3d) (viewPortSize : V2i) (cameraHull : FastHull3d) (root : ILodDataNode) =
        
        let result = System.Collections.Generic.HashSet<ILodDataNode>()

        let rec traverse (node : ILodDataNode) =
            if cameraHull.Intersects(node.Bounds) then
                if defaultLodDecider targetPixelDistance viewTrafo projTrafo viewPortSize node then
                    result.Add node |> ignore
                    match node.Children with
                    | None -> 
                        ()
                    | Some cs ->
                        cs |> Array.iter traverse
                else
                    ()
            else
                ()
        
        do traverse root

        result :> ISet<_>
        
    let rasterize viewTrafo projTrafo (viewPortSize : V2i) (x : ILodData) (rasterizeSet : SetRasterizer) =
        // create a FastHull3d for the (extended) camera
        let hull = viewTrafo * projTrafo |> ViewProjection.toFastHull3d

        let set = x.RootNode()

        let result = rasterizeSet viewTrafo projTrafo viewPortSize hull set

        // traverse the ILodData building a set of nodes in view respecting
        // the given nearPlaneDistance in [(-1,-1) x (1,1)] space
        //x.Traverse( traverser )

        // return the resulting node-set
        result

[<AutoOpen>]
module ``Lod Data Extensions`` =
    open System.Collections.Concurrent

    let inline private maxDir (dir : V3d) (b : Box3d) =
        V4d(
            (if dir.X > 0.0 then b.Max.X else b.Min.X), 
            (if dir.Y > 0.0 then b.Max.Y else b.Min.Y), 
            (if dir.Z > 0.0 then b.Max.Z else b.Min.Z), 
            1.0
        )

    let inline private height (plane : V4d) (b : Box3d) =
        plane.Dot(maxDir plane.XYZ b)

    let inline private extendView (view : CameraView) =
        // TODO: find some magic here (maybe needing movement info)
        view

    let inline private extendFrustum (frustum : Frustum) =
        // TODO: find some magic here
        frustum

    type ILodData with
        member x.Rasterize(viewTrafo : Trafo3d, projTrafo : Trafo3d, viewPortSize : V2i, rasterizeSet : LodData.SetRasterizer) =
            LodData.rasterize viewTrafo projTrafo viewPortSize x rasterizeSet
            
open System
open System.Threading

type private DeltaHeapEntry<'a, 'b> =
    class
        val mutable public Priority : 'b
        val mutable public Value : 'a
        val mutable public Index : int
        val mutable public RefCount : int

        new(v,p,i,r) = { Value = v; Priority = p; Index = i; RefCount = r }
    end

type ConcurrentDeltaPriorityQueue<'a, 'b when 'b : comparison>(getPriority : SetOperation<'a> -> 'b) =
    
    let heap = List<DeltaHeapEntry<'a, 'b>>()
    let entries = Dict<'a, DeltaHeapEntry<'a, 'b>>()
    let mutable adds = 0
    let mutable rems = 0

    let swap (l : DeltaHeapEntry<'a, 'b>) (r : DeltaHeapEntry<'a, 'b>) =
        let li = l.Index
        let ri = r.Index
        heap.[li] <- r
        heap.[ri] <- l
        l.Index <- ri
        r.Index <- li

    let rec pushDown (acc : int) (e : DeltaHeapEntry<'a, 'b>) =
        let l = 2 * e.Index + 1
        let r = 2 * e.Index + 2

        let cl = if l < heap.Count then compare e.Priority heap.[l].Priority <= 0 else true
        let cr = if r < heap.Count then compare e.Priority heap.[l].Priority <= 0 else true

        match cl, cr with
            | true, true -> 
                acc

            | false, true ->
                swap heap.[l] e
                pushDown (acc + 1) e

            | true, false ->
                swap heap.[r] e
                pushDown (acc + 1) e

            | false, false ->
                let c = compare heap.[l].Priority heap.[r].Priority
                if c < 0 then
                    swap heap.[l] e
                else
                    swap heap.[r] e
                        
                pushDown (acc + 1) e

    let rec bubbleUp (acc : int) (e : DeltaHeapEntry<'a, 'b>) =
        if e.Index > 0 then
            let pi = (e.Index - 1) / 2
            let pe = heap.[pi]

            if compare pe.Priority e.Priority > 0 then
                swap pe e
                bubbleUp (acc + 1) e
            else
                acc
        else
            acc

    let enqueue (e : DeltaHeapEntry<'a, 'b>) =
        e.Index <- heap.Count
        heap.Add(e)
        bubbleUp 0 e

    let changeKey (e : DeltaHeapEntry<'a, 'b>) (newKey : 'b) =
        if e.Index < 0 then
            e.Priority <- newKey
            enqueue e
        else
            let c = compare newKey e.Priority
            e.Priority <- newKey

            if c > 0 then pushDown 0 e
            elif c < 0 then bubbleUp 0 e
            else 0

    let dequeue() =
        if heap.Count <= 1 then
            let e = heap.[0]
            entries.Remove e.Value |> ignore
            heap.Clear()
            adds <- 0
            rems <- 0
            SetOperation(e.Value, e.RefCount)
        else
            let e = heap.[0]
            let l = heap.[heap.Count - 1]
            heap.RemoveAt (heap.Count - 1)
            heap.[0] <- l
            l.Index <- 0
            pushDown 0 l |> ignore
            e.Index <- -1
            entries.Remove e.Value |> ignore

            if e.RefCount < 0 then rems <- rems - 1
            elif e.RefCount > 0 then adds <- adds - 1

            SetOperation(e.Value, e.RefCount)

    let rec remove (e : DeltaHeapEntry<'a, 'b>) =
        if e.Index > 0 then
            let pi = (e.Index - 1) / 2
            let pe = heap.[pi]
            swap pe e
            remove e
        else
            dequeue() |> ignore

    member x.AddCount = adds
    member x.RemoveCount = rems

    member x.Count = heap.Count

    member x.Enqueue (a : SetOperation<'a>) : unit =
        if a.Count <> 0 then
            lock x (fun () ->
                let entry = entries.GetOrCreate(a.Value, fun v -> DeltaHeapEntry<'a, 'b>(a.Value, Unchecked.defaultof<'b>, -1, 0))
                let oldCount = entry.RefCount
                entry.RefCount <- entry.RefCount + a.Count

                if entry.RefCount = 0 then
                    if oldCount > 0 then adds <- adds - 1
                    elif oldCount < 0 then rems <- rems - 1

                    entries.Remove a.Value |> ignore
                    remove entry
                else
                    if oldCount <= 0 && entry.RefCount > 0 then adds <- adds + 1
                    elif oldCount >= 0 && entry.RefCount < 0 then rems <- rems + 1

                    changeKey entry (SetOperation(entry.Value, entry.RefCount) |> getPriority) |> ignore
                    Monitor.Pulse x

                assert(adds + rems = heap.Count)
            )

    member x.EnqueueMany (a : seq<SetOperation<'a>>) : unit =
        lock x (fun () ->
            for a in a do
                let entry = entries.GetOrCreate(a.Value, fun v -> DeltaHeapEntry<'a, 'b>(a.Value, Unchecked.defaultof<'b>, -1, 0))
                let oldCount = entry.RefCount
                entry.RefCount <- entry.RefCount + a.Count

                if entry.RefCount = 0 then
                    if oldCount > 0 then adds <- adds - 1
                    elif oldCount < 0 then rems <- rems - 1

                    entries.Remove a.Value |> ignore
                    remove entry
                else
                    if oldCount <= 0 && entry.RefCount > 0 then adds <- adds + 1
                    elif oldCount >= 0 && entry.RefCount < 0 then rems <- rems + 1

                    changeKey entry (SetOperation(entry.Value, entry.RefCount) |> getPriority) |> ignore

            Monitor.Pulse x
            assert(adds + rems = heap.Count)
        )

    member x.Pulse() =
        Monitor.Enter x
        Monitor.PulseAll x
        Monitor.Exit x


    member x.Dequeue (ct : CancellationToken) : SetOperation<'a> = 
        Monitor.Enter x
        while heap.Count = 0 do
            if ct.IsCancellationRequested then
                Monitor.Exit x
                raise <| OperationCanceledException()

            Monitor.Wait(x, 100) |> ignore

        let e = dequeue()
        assert(adds + rems = heap.Count)
        Monitor.Exit x
        e

    member x.Dequeue () : SetOperation<'a> = 
        Monitor.Enter x
        while heap.Count = 0 do
            Monitor.Wait(x) |> ignore
        let e = dequeue()
        assert(adds + rems = heap.Count)
        Monitor.Exit x
        e

type LoaderProgress =
    {
        targetCount     : int
        currentCount    : int
        queueAdds       : int
        queueRemoves    : int
        avgLoadTime     : MicroTime
        avgRemoveTime   : MicroTime
        avgEvaluateTime : MicroTime
    }

type LoadConfig<'a, 'b> =
    {
        continueLoader      : unit -> bool
        load                : CancellationToken -> 'a -> 'b
        unload              : 'b -> unit
        priority            : SetOperation<'a> -> int
        numThreads          : int
        submitDelay         : TimeSpan
        progressInterval    : TimeSpan
        progress            : LoaderProgress -> unit
        frozen              : aval<bool>
    }

module Loader =
    open System.Threading.Tasks
    open System.Collections.Concurrent

    [<AutoOpen>]
    module private BaseLibExtensions =
   
        let rec (|CancelExn|_|) (e : exn) =
            match e with
                | :? OperationCanceledException ->
                    Some()
                | :? AggregateException as e ->
                    if e.InnerExceptions.Count = 1 then
                        (|CancelExn|_|) e.InnerException
                    else
                        None
                | _ -> None

    
        type MVar<'a>() =
            let mutable content = Unchecked.defaultof<ref<'a>>
            // feel free to replace atomic + sem by appropriate .net synchronization data type
            let sem = new SemaphoreSlim(0)
            let mutable cnt = 0
            member x.Put v = 
                content <- ref v
                if Interlocked.Exchange(&cnt, 1) = 0 then
                    sem.Release() |> ignore

            member x.Take (ct : CancellationToken) =
                sem.Wait(ct)
                let res = !Interlocked.Exchange(&content,Unchecked.defaultof<_>)
                cnt <- 0
                res

            member x.Take () =
                x.Take (CancellationToken.None)

            member x.TakeAsync () =
                async {
                    let! ct = Async.CancellationToken
                    do! Async.AwaitTask(sem.WaitAsync(ct))
                    let res = !Interlocked.Exchange(&content,Unchecked.defaultof<_>)
                    cnt <- 0
                    return res
                }

        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module MVar =
            let empty () = MVar<'a>()
            let create a =
                let v = empty()
                v.Put a
                v
            let put (m : MVar<'a>) v = m.Put v
            let take (m : MVar<'a>) = m.Take()
            let takeAsync (m : MVar<'a>) = m.TakeAsync ()


        type AsyncSetReader<'a>(set : aset<'a>) =
            inherit AdaptiveObject()

            let evalTime = System.Diagnostics.Stopwatch()

            let mvar = MVar.create()
            let r = set.GetReader()

            member x.EvaluationTime = evalTime.MicroTime

            override x.MarkObject() =
                MVar.put mvar ()
                true

            member x.GetChanges(ct : CancellationToken) =
                try
                    mvar.Take(ct)
                    x.EvaluateAlways AdaptiveToken.Top (fun token ->
                        evalTime.Restart()
                        let ops = r.GetChanges(token)
                        evalTime.Stop()
                        Some ops
                    )
                with CancelExn -> 
                    None

        type IAdaptiveHashSet<'a> with
            member x.GetAsyncReader() =
                new AsyncSetReader<'a>(x)


    let private startThread (f : unit -> unit) fmt =
        Printf.kprintf (fun str -> 
            let t = new Thread(ThreadStart(f), IsBackground = true, Name = str)
            t.Start()
            t
        ) fmt

    type AsyncLoadASet<'a, 'x, 'b>(config : LoadConfig<'a, 'b>, input : aval<'x>, mapping : 'x -> ISet<'a>) as x =

        let resultDeltaLock = obj()
        let mutable dead = HashSet.empty

        let finalize (ops : HashSetDelta<'b>) =
            if ops.Count > 0 then
                lock resultDeltaLock (fun () ->
                    let old = dead.Count
                    for op in ops do
                        match op with
                            | Rem(_,v) -> dead <- HashSet.add v dead
                            | _ -> ()
                )

        let history = new History<CountingHashSet<'b>, HashSetDelta<'b>>(CountingHashSet.traceNoRefCount, finalize)

        let reset() =
            history.Perform(CountingHashSet.computeDelta history.State CountingHashSet.empty) |> ignore

        let emit (ops : HashSetDelta<'b>) =
            if ops.Count > 0 then
                history.Perform ops |> ignore



        let mutable targetCount = 0
        let mutable currentCount = 0
        let mutable queueAdds = 0
        let mutable queueRemoves = 0
        let mutable getDeltaTime = MicroTime.Zero
        let mutable getDeltaCount = 0
        let mutable loadCount = 0
        let mutable loadTicks = 0L
        let mutable remCount = 0
        let mutable remTicks = 0L

            

        let progressReport() =
            let loadTime = 
                if loadCount = 0 then MicroTime.Zero
                else MicroTime(TimeSpan.FromTicks loadTicks) / float loadCount

            let remTime = 
                if remCount = 0 then MicroTime.Zero
                else MicroTime(TimeSpan.FromTicks remTicks) / float remCount

            let pullTime =
                if getDeltaCount = 0 then MicroTime.Zero
                else getDeltaTime / float getDeltaCount

            loadCount <- 0
            loadTicks <- 0L
            remCount <- 0
            remTicks <- 0L
            getDeltaCount <- 0
            getDeltaTime <- MicroTime.Zero

            config.progress {
                targetCount = targetCount
                currentCount = currentCount
                queueAdds = queueAdds
                queueRemoves = queueRemoves
                avgLoadTime = loadTime
                avgRemoveTime = remTime
                avgEvaluateTime = pullTime
            }

        let mutable refCount = 0
        let mutable witness = Disposable.empty

        let removeRef () =
            lock x (fun () ->
                if Interlocked.Decrement(&refCount) = 0 then
                    Log.start "[Loader] stop loading"
                    witness.Dispose()
                    witness <- Disposable.empty
                    Log.stop()
            )

        let start() = 
            let queue = ConcurrentDeltaPriorityQueue<'a, int>(config.priority)
            let cancel = new CancellationTokenSource()
            let tasks = ConcurrentDictionary<'a, Task<'b> * CancellationTokenSource>()

            let reportTimer = 
                if config.progressInterval < TimeSpan.MaxValue then 
                    new Timer(TimerCallback(fun _ -> progressReport()), null, config.progressInterval, config.progressInterval)
                else
                    null

            let resultDeltaReady = MVar.empty()
            let mutable resultDeltas = HashSetDelta.empty

            let getDeltaThread () =
                let changed = MVar.create()
                let cb = input.AddMarkingCallback(fun () -> MVar.put changed ())
                try
                    try
                        let mutable last = System.Collections.Generic.HashSet() :> ISet<_>
                        let sw = System.Diagnostics.Stopwatch()
                        while true do
                            changed.Take(cancel.Token)
                            if config.frozen.GetValue() then
                                input.GetValue() |> ignore
                            else
                                sw.Restart()
                                let res = input.GetValue()
                                let set = mapping res
                                let add = set |> Seq.filter (last.Contains >> not) |> Seq.map Add
                                let rem = last |> Seq.filter (set.Contains >> not) |> Seq.map Rem
                                let ops = HashSetDelta.combine (HashSetDelta.ofSeq add) (HashSetDelta.ofSeq rem)
                                last <- set
                                sw.Stop()

                                getDeltaTime <- getDeltaTime + sw.MicroTime
                                getDeltaCount <- getDeltaCount + 1

                                queue.EnqueueMany(ops)
                                targetCount <- set.Count
                                queueAdds <- queue.AddCount
                                queueRemoves <- queue.RemoveCount
                    with
                        | CancelExn -> ()
                        | e -> Log.error "getDelta faulted: %A" e
                finally
                    cb.Dispose()

            let loadThread () =
                try 
                    try
                        let sw = System.Diagnostics.Stopwatch()
                        while config.continueLoader() do
                            if config.frozen.GetValue() then
                                Thread.Sleep 100
                            else
                                let op = queue.Dequeue(cancel.Token)
                                queueAdds <- queue.AddCount
                                queueRemoves <- queue.RemoveCount
                                if config.continueLoader() then
                                    match op with
                                        | Add(_,v) ->
                                            sw.Restart()
                                            let cts = new CancellationTokenSource()
                                            let tcs = TaskCompletionSource<'b>()
                                            if tasks.ContainsKey v then Log.warn "[LoD] duplicate add"

                                            tasks.[v] <- (tcs.Task, cts)

                                            try
                                                let loaded = config.load cts.Token v
                                                lock resultDeltaLock (fun () -> 
                                                    resultDeltas <- HashSetDelta.add (Add loaded) resultDeltas
                                                )
                                                MVar.put resultDeltaReady ()
                                                tcs.SetResult(loaded)
                                            with
                                                | CancelExn -> tcs.SetCanceled()
                                                | e -> tcs.SetException e


                                            sw.Stop()
                                            Interlocked.Increment(&loadCount) |> ignore
                                            Interlocked.Add(&loadTicks, sw.Elapsed.Ticks) |> ignore

                                        | Rem(_,v) ->
                                            sw.Restart()
                                            let mutable tup = Unchecked.defaultof<_>
                                            while not (tasks.TryRemove(v, &tup)) do
                                                Log.warn "[LoD] the craziest thing just happened"
                                                Thread.Yield() |> ignore

                                            let (task, cts) = tup
                                            cts.Cancel()
                                            try
                                                let res = task.Result
                                                lock resultDeltaLock (fun () -> 
                                                    let m = resultDeltas |> HashSetDelta.toHashMap

                                                    let newMap = 
                                                        m |> HashMap.alter res (fun o ->
                                                            match o with
                                                                | Some 1 -> 
                                                                    dead <- HashSet.add res dead
                                                                    None
                                                                | Some o ->
                                                                    Some (o - 1)
                                                                | None ->
                                                                    Some (-1)
                                                        )

                                                    resultDeltas <- HashSetDelta.ofHashMap newMap
                                                )
                                                MVar.put resultDeltaReady ()
                                            with
                                                | CancelExn -> ()
                                                | e -> Log.error "[LoD] load of %A faulted: %A" v e
                                
                                            cts.Dispose()
                                            sw.Stop()
                                            Interlocked.Increment(&remCount) |> ignore
                                            Interlocked.Add(&remTicks, sw.Elapsed.Ticks) |> ignore
                                else
                                    Log.line "processing ended."
                                
                    with
                        | CancelExn ->
                            ()
                        | e ->
                            Log.error "processing faulted: %A" e
                finally
                    removeRef()

            let submitThread () =
                try
                    let sw = System.Diagnostics.Stopwatch()
                        
                    while true do
                        resultDeltaReady.Take(cancel.Token)
                        sw.Stop()
                        if sw.Elapsed < config.submitDelay && config.submitDelay > TimeSpan.Zero then
                            Thread.Sleep(config.submitDelay - sw.Elapsed)

                        let ops = 
                            lock resultDeltaLock (fun () ->
                                let res = resultDeltas
                                resultDeltas <- HashSetDelta.empty
                                res
                            )

                        transact (fun () ->
                            emit ops
                        )
                        sw.Restart()

                        currentCount <- history.State.Count
                with 
                    | CancelExn -> ()
                    | e -> Log.error "submit faulted: %A" e

            let cleanWatch = System.Diagnostics.Stopwatch()
            let cleanupTick(o : obj) =
                cleanWatch.Restart()
                let rem = 
                    lock resultDeltaLock (fun () ->
                        let res = dead
                        dead <- HashSet.empty
                        res
                    )
                for v in rem do config.unload v
                cleanWatch.Stop()

                if rem.Count > 0 then
                    //Interlocked.Add(&remCount, rem.Count) |> ignore
                    Interlocked.Add(&remTicks, cleanWatch.Elapsed.Ticks) |> ignore
                //if rem.Count > 0 then
                //    Log.line "unloaded %A elements (%A)" rem.Count sw.MicroTime

            let getDeltaThread = startThread getDeltaThread "GetDeltaThread"
            let loadThreads = List.init config.numThreads (startThread loadThread "LoadThread%d")
            let submitThread = startThread submitThread "SubmitThread" 
            let cleanupTimer = new Timer(TimerCallback(cleanupTick), null, 1000, 1000)

            let witness = 
                { new IDisposable with
                    member x.Dispose() =
                        if not (isNull reportTimer) then 
                            reportTimer.Dispose()

                        cleanupTimer.Dispose()

                        cancel.Cancel()

                        getDeltaThread.Join()
                        for p in loadThreads do p.Join()
                        submitThread.Join()

                        targetCount <- 0
                        currentCount <- 0
                        queueAdds <- 0
                        queueRemoves <- 0
                        getDeltaTime <- MicroTime.Zero
                        getDeltaCount <- 0
                        loadCount <- 0
                        loadTicks <- 0L
                        remCount <- 0
                        remTicks <- 0L

                        transact (fun () -> reset())
                }

            witness

        member internal x.AddRef() =
            lock x (fun () ->
                if Interlocked.Increment(&refCount) = 1 then
                    Log.line "[Loader] start loading"
                    witness <- start()
            )

        member internal x.RemoveRef() =
            removeRef()

        member x.GetReader() =
            x.AddRef()
            new AsnycLoadSetReader<'a, 'x, 'b>(x, history.NewReader()) :> IHashSetReader<_>

        interface aset<'b> with
            member x.History = Some history 
            member x.IsConstant = false
            member x.Content = history |> AVal.map (CountingHashSet.toHashSet) 
            member x.GetReader() = x.GetReader()

    and private AsnycLoadSetReader<'a, 'x, 'b>(parent : AsyncLoadASet<'a, 'x, 'b>, r : IHashSetReader<'b>) =
        inherit AdaptiveObject()

        member private x.Dispose (disposing : bool) =
            if disposing then GC.SuppressFinalize x
            parent.RemoveRef()

        override x.Finalize() =
            x.Dispose false

        interface IHashSetReader<'b> with
            member x.Trace = CountingHashSet.trace
            member x.GetChanges t = r.GetChanges t
            member x.State = r.State

    let load (mapping : 'x -> ISet<'a>) (config : LoadConfig<'a, 'b>) (input : aval<'x>) : aset<'b> =
        AsyncLoadASet<'a, 'x, 'b>(config, input, mapping) :> aset<_>













