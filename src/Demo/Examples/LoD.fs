#if INTERACTIVE
#I @"../../../bin/Debug"
#I @"../../../bin/Release"
#load "LoadReferences.fsx"
#else
namespace Examples
#endif

open System
open System.Collections.Generic

open Aardvark.Base
open Aardvark.Rendering.Interactive
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

module Helpers = 
    let rand = Random()
    let randomPoints (bounds : Box3d) (pointCount : int) =
        let size = bounds.Size
        let randomV3f() = V3d(rand.NextDouble(), rand.NextDouble(), rand.NextDouble()) * size + bounds.Min |> V3f.op_Explicit
        let randomColor() = C4b(rand.NextDouble(), rand.NextDouble(), rand.NextDouble(), 1.0)

        IndexedGeometry(
            Mode = IndexedGeometryMode.PointList,
            IndexedAttributes = 
                SymDict.ofList [
                        DefaultSemantic.Positions, Array.init pointCount (fun _ -> randomV3f()) :> Array
                        DefaultSemantic.Colors, Array.init pointCount (fun _ -> randomColor()) :> Array
                ]
        )

    let randomColor() =
        C4b(128 + rand.Next(127) |> byte, 128 + rand.Next(127) |> byte, 128 + rand.Next(127) |> byte, 255uy)
    let randomColor2 ()  =
        C4b(rand.Next(255) |> byte, rand.Next(255) |> byte, rand.Next(255) |> byte, 255uy)

    let frustum (f : IMod<CameraView>) (proj : IMod<Frustum>) =
        let invViewProj = Mod.map2 (fun v p -> (CameraView.viewTrafo v * Frustum.projTrafo p).Inverse) f proj

        let positions = 
            [|
                V3f(-1.0, -1.0, -1.0)
                V3f(1.0, -1.0, -1.0)
                V3f(1.0, 1.0, -1.0)
                V3f(-1.0, 1.0, -1.0)
                V3f(-1.0, -1.0, 1.0)
                V3f(1.0, -1.0, 1.0)
                V3f(1.0, 1.0, 1.0)
                V3f(-1.0, 1.0, 1.0)
            |]

        let indices =
            [|
                1;2; 2;6; 6;5; 5;1;
                2;3; 3;7; 7;6; 4;5; 
                7;4; 3;0; 0;4; 0;1;
            |]

        let geometry =
            IndexedGeometry(
                Mode = IndexedGeometryMode.LineList,
                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                        DefaultSemantic.Colors, Array.create indices.Length C4b.Red :> Array
                    ]
            )

        geometry
            |> Sg.ofIndexedGeometry
            |> Sg.trafo invViewProj

[<AutoOpen>]
module NewLoDImpl =
    open System.Threading

    type LoaderProgress =
        {
            targetCount     : int
            currentCount    : int
            queueCount      : int
            avgLoadTime     : MicroTime
            avgRemoveTime   : MicroTime
            avgEvaluateTime : MicroTime
        }

    type LoadConfig<'a, 'b> =
        {
            load                : CancellationToken -> 'a -> 'b
            unload              : 'b -> unit
            priority            : SetOperation<'a> -> int
            numThreads          : int
            submitDelay         : TimeSpan
            progressInterval    : TimeSpan
            progress            : LoaderProgress -> unit
        }

    module Loader =
        open System.Threading.Tasks
        open System.Collections.Concurrent
        open Aardvark.SceneGraph

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

                override x.Mark() =
                    MVar.put mvar ()
                    true

                member x.Dispose() =
                    r.Dispose()

                interface IDisposable with
                    member x.Dispose() = x.Dispose()

                member x.GetOperations(ct : CancellationToken) =
                    try
                        mvar.Take(ct)
                        x.EvaluateAlways AdaptiveToken.Top (fun token ->
                            evalTime.Restart()
                            let ops = r.GetOperations(token)
                            evalTime.Stop()
                            Some ops
                        )
                    with CancelExn -> 
                        None

            type aset<'a> with
                member x.GetAsyncReader() =
                    new AsyncSetReader<'a>(x)


        let private startThread (f : unit -> unit) fmt =
            Printf.kprintf (fun str -> 
                let t = new Thread(ThreadStart(f), IsBackground = true, Name = str)
                t.Start()
                t
            ) fmt

        type AsyncLoadASet<'a, 'x, 'b>(config : LoadConfig<'a, 'b>, input : IMod<'x>, mapping : 'x -> ISet<'a>) =
            static let noDisposable = { new IDisposable with member x.Dispose() = () }

            let resultDeltaLock = obj()
            let mutable dead = HSet.empty

            let finalize (ops : hdeltaset<'b>) =
                if ops.Count > 0 then
                    lock resultDeltaLock (fun () ->
                        let old = dead.Count
                        for op in ops do
                            match op with
                                | Rem(_,v) -> dead <- HSet.add v dead
                                | _ -> ()
                    )

            let history = new History<hrefset<'b>, hdeltaset<'b>>(HRefSet.traceNoRefCount, finalize)

            let reset() =
                history.Perform(HRefSet.computeDelta history.State HRefSet.empty) |> ignore

            let emit (ops : hdeltaset<'b>) =
                if ops.Count > 0 then
                    history.Perform ops |> ignore



            let mutable targetCount = 0
            let mutable currentCount = 0
            let mutable queueCount = 0
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
                    queueCount = queueCount
                    avgLoadTime = loadTime
                    avgRemoveTime = remTime
                    avgEvaluateTime = pullTime
                }

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
                let mutable resultDeltas = HDeltaSet.empty

                let getDeltaThread () =
                    let changed = MVar.create()
                    let cb = input.AddMarkingCallback(fun () -> MVar.put changed ())
                    try
                        try
                            let mutable last = HashSet() :> ISet<_>
                            let sw = System.Diagnostics.Stopwatch()
                            while true do
                                changed.Take(cancel.Token)

                                sw.Restart()
                                let res = input.GetValue()
                                let set = mapping res
                                let add = set |> Seq.filter (last.Contains >> not) |> Seq.map Add
                                let rem = last |> Seq.filter (set.Contains >> not) |> Seq.map Rem
                                let ops = HDeltaSet.combine (HDeltaSet.ofSeq add) (HDeltaSet.ofSeq rem)
                                last <- set
                                targetCount <- set.Count
                                sw.Stop()

                                getDeltaTime <- getDeltaTime + sw.MicroTime
                                getDeltaCount <- getDeltaCount + 1

                                queue.EnqueueMany(ops)
                                queueCount <- queue.Count
                        with
                            | CancelExn -> ()
                            | e -> Log.error "getDelta faulted: %A" e
                    finally
                        cb.Dispose()

                let loadThread () =
                    try
                        let sw = System.Diagnostics.Stopwatch()

                        while true do
                            let op = queue.Dequeue(cancel.Token)
                            match op with
                                | Add(_,v) ->
                                    sw.Restart()
                                    let cts = new CancellationTokenSource()
                                    let tcs = TaskCompletionSource<'b>()
                                    if tasks.ContainsKey v then Log.warn "duplicate add"

                                    tasks.[v] <- (tcs.Task, cts)

                                    try
                                        let loaded = config.load cts.Token v
                                        lock resultDeltaLock (fun () -> 
                                            resultDeltas <- HDeltaSet.add (Add loaded) resultDeltas
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
                                            let m = resultDeltas |> HDeltaSet.toHMap

                                            let newMap = 
                                                m |> HMap.alter res (fun o ->
                                                    match o with
                                                        | Some 1 -> 
                                                            dead <- HSet.add res dead
                                                            None
                                                        | Some o ->
                                                            Some (o - 1)
                                                        | None ->
                                                            Some (-1)
                                                )

                                            resultDeltas <- HDeltaSet.ofHMap newMap
                                        )
                                        MVar.put resultDeltaReady ()
                                    with
                                        | CancelExn -> ()
                                        | e -> Log.error "[LoD] load of %A faulted: %A" v e
                                
                                    cts.Dispose()
                                    sw.Stop()
                                    Interlocked.Increment(&remCount) |> ignore
                                    Interlocked.Add(&remTicks, sw.Elapsed.Ticks) |> ignore


                    with
                        | CancelExn ->
                            ()
                        | e ->
                            Log.error "processing faulted: %A" e

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
                                    resultDeltas <- HDeltaSet.empty
                                    res
                                )

                            transact (fun () ->
                                emit ops
                            )
                            sw.Restart()

                            currentCount <- history.State.Count
                            queueCount <- queue.Count
                    with 
                        | CancelExn -> ()
                        | e -> Log.error "submit faulted: %A" e

                let cleanupTick(o : obj) =
                    let sw = System.Diagnostics.Stopwatch.StartNew()
                    let rem = 
                        lock resultDeltaLock (fun () ->
                            let res = dead
                            dead <- HSet.empty
                            res
                        )

                    for v in rem do config.unload v
                    sw.Stop()
                    if rem.Count > 0 then
                        Log.line "unloaded %A elements (%A)" rem.Count sw.MicroTime

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
                            queueCount <- 0
                            getDeltaTime <- MicroTime.Zero
                            getDeltaCount <- 0
                            loadCount <- 0
                            loadTicks <- 0L
                            remCount <- 0
                            remTicks <- 0L

                            transact (fun () -> reset())
                    }

                witness

            let mutable refCount = 0
            let mutable witness = noDisposable

            member internal x.AddRef() =
                lock x (fun () ->
                    if Interlocked.Increment(&refCount) = 1 then
                        Log.line "[Loader] start loading"
                        witness <- start()
                )

            member internal x.RemoveRef() =
                lock x (fun () ->
                    if Interlocked.Decrement(&refCount) = 0 then
                        Log.start "[Loader] stop loading"
                        witness.Dispose()
                        witness <- noDisposable 
                        Log.stop()
                )

            member x.GetReader() =
                x.AddRef()
                new AsnycLoadSetReader<'a, 'x, 'b>(x, history.NewReader()) :> ISetReader<_>

            interface aset<'b> with
                member x.IsConstant = false
                member x.Content = history :> IMod<_>
                member x.GetReader() = x.GetReader()

        and private AsnycLoadSetReader<'a, 'x, 'b>(parent : AsyncLoadASet<'a, 'x, 'b>, r : ISetReader<'b>) =
            inherit AdaptiveDecorator(r)

            member private x.Dispose (disposing : bool) =
                if disposing then GC.SuppressFinalize x
                r.Dispose()
                parent.RemoveRef()

            override x.Finalize() =
                x.Dispose false

            interface ISetReader<'b> with
                member x.GetOperations t = 
                    r.GetOperations t

                member x.Dispose() = x.Dispose true
                member x.State = r.State

        let load (mapping : 'x -> ISet<'a>) (config : LoadConfig<'a, 'b>) (input : IMod<'x>) : aset<'b> =
            AsyncLoadASet<'a, 'x, 'b>(config, input, mapping) :> aset<_>


    type MyPC(data, config, progress) =
        inherit Sg.PointCloud(data, config, progress)

    module PointCloudRenderer = 
        open Aardvark.Base.Rendering
        open Aardvark.Base
        open Aardvark.SceneGraph
        open Aardvark.SceneGraph.Semantics
        open Aardvark.Base.Ag

        type private LoadedGeometry(pool : IGeometryPool, ptr : managedptr, geometry : Option<IndexedGeometry>) =
            let range =
                match geometry with
                    | Some g -> Range1i(int ptr.Offset, int ptr.Offset + int ptr.Size - 1)
                    | None -> Range1i.Invalid

            static let empty = LoadedGeometry(Unchecked.defaultof<_>, Unchecked.defaultof<_>, None)

            static member Empty = empty

            member x.Range = range

            member x.Pointer = 
                match geometry with
                    | Some _ -> Some ptr
                    | _ -> None

            member x.Geometry = geometry

        let private faceVertexCount (g : IndexedGeometry) =
            if g.IndexedAttributes.Count = 0 then 
                0
            else
                let arr = g.IndexedAttributes.Values |> Seq.head
                arr.Length


        let createRenderObject (scope : Ag.Scope) (config : PointCloudInfo) (data : ILodData) =
            let runtime : IRuntime = 
                scope?Runtime

            let ro = RenderObject.ofScope scope


            let decider = 
                config.lodDecider

            let view : IMod<Trafo3d> = 
                match config.customView with
                    | Some view -> view
                    | None -> 
                        match ro.Uniforms.TryGetUniform(scope, Symbol.Create "ViewTrafo") with
                            | Some (:? IMod<Trafo3d> as v) -> v
                            | _ -> scope?ViewTrafo

            let proj : IMod<Trafo3d> = 
                match config.customProjection with
                    | Some proj -> proj
                    | None -> 
                        match ro.Uniforms.TryGetUniform(scope, Symbol.Create "ProjTrafo") with
                            | Some (:? IMod<Trafo3d> as v) -> v
                            | _ -> scope?ProjTrafo

            let size : IMod<V2i> = 
                match ro.Uniforms.TryGetUniform(scope, Symbol.Create "ViewportSize") with
                    | Some (:? IMod<V2i> as v) -> v
                    | _ -> scope?ViewportSize

            let dependencies =
                Mod.custom (fun token ->
                    let view = view.GetValue(token)
                    let proj = proj.GetValue(token)
                    let size = size.GetValue(token)
                    let decider = decider.GetValue(token)
                    view, proj, size, decider
                )

            let rasterize (view : Trafo3d, proj : Trafo3d, size : V2i, decider : LodData.Decider) =
                data.Rasterize(view, proj, decider view proj size)

            let pool = runtime.CreateGeometryPool(config.attributeTypes)
            let mutable refCount = 0


            let release() =
                if Interlocked.Decrement(&refCount) = 0 then
                    pool.Dispose()

            let activate() =
                Interlocked.Increment(&refCount) |> ignore
                { new IDisposable with member x.Dispose() = release() }

            let progress (p : LoaderProgress) =
                Log.line "memory: %A" pool.UsedMemory

            let vertexAttributes =
                config.attributeTypes 
                |> Map.toSeq
                |> Seq.choose (fun (sem,_) ->
                    match pool.TryGetBufferView sem with
                        | Some view -> Some (sem, view)
                        | None -> None
                   )
                |> Map.ofSeq
                |> AttributeProvider.ofMap
                |> AttributeProvider.onDispose release

            let loadedGeometries = 
                let load (ct : CancellationToken) (node : LodDataNode) =
                    let geometry = Async.RunSynchronously(data.GetData(node), cancellationToken = ct)

                    use __ = runtime.ContextLock
                    match geometry with
                        | Some g ->
                            let fvc = faceVertexCount g
                            let ptr = pool.Alloc(fvc, g)
                            LoadedGeometry(pool, ptr, geometry)

                        | None ->
                            LoadedGeometry.Empty

                let unload (g : LoadedGeometry) =
                    match g.Pointer with
                        | Some ptr -> pool.Free ptr
                        | None -> ()

                dependencies |> Loader.load rasterize {
                    load                = load
                    unload              = unload
                    priority            = fun op -> if op.Count < 0 then -op.Value.level else op.Value.level
                    numThreads          = 4
                    submitDelay         = TimeSpan.FromMilliseconds 50.0
                    progressInterval    = TimeSpan.FromSeconds 1.0
                    progress            = progress
                }

            let drawCallBuffer = 
                let add (set : RangeSet) (v : LoadedGeometry) =
                    let range = v.Range
                    if range.IsValid then RangeSet.insert range set
                    else set

                let sub (set : RangeSet) (v : LoadedGeometry) =
                    let range = v.Range
                    if range.IsValid then RangeSet.remove range set
                    else set

                let call (r : Range1i) =
                    DrawCallInfo(
                        FaceVertexCount = (r.Max - r.Min + 1),
                        InstanceCount = 1,
                        FirstIndex = r.Min,
                        FirstInstance = 0,
                        BaseVertex = 0
                    )

                loadedGeometries 
                    |> ASet.foldGroup add sub RangeSet.empty
                    |> Mod.map (fun ranges ->
                        let calls = ranges |> RangeSet.toSeq |> Seq.map call |> Seq.toArray
                        IndirectBuffer(ArrayBuffer calls, calls.Length) :> IIndirectBuffer
                    )

            ro.IndirectBuffer <- drawCallBuffer
            ro.Mode <- Mod.constant IndexedGeometryMode.PointList
            ro.VertexAttributes <- vertexAttributes
            ro.Activate <- activate

            ro :> IRenderObject

        [<Semantic>]
        type MyPCSem() =
            member x.RenderObjects(m : MyPC) =
                let scope = Ag.getContext()
                let obj = createRenderObject scope m.Config m.Data
                ASet.single obj


module LoD = 

    Interactive.Renderer <- RendererConfiguration.GL
    //FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])

    let win = Interactive.Window


    // ===================================================================================
    // example usage
    // ===================================================================================
    type DummyDataProvider(root : Box3d) =
    
        interface ILodData with
            member x.BoundingBox = root

            member x.Traverse f =
                let rec traverse (level : int) (b : Box3d) =
                    let box = b
                    let n = 600
                    let node = { id = b; level = level; bounds = box; inner = true; pointCountNode = 100L; pointCountTree = 100L; render = true}

                    if f node then
                        let center = b.Center

                        let children =
                            let l = b.Min
                            let u = b.Max
                            let c = center
                            [
                                Box3d(V3d(l.X, l.Y, l.Z), V3d(c.X, c.Y, c.Z))
                                Box3d(V3d(c.X, l.Y, l.Z), V3d(u.X, c.Y, c.Z))
                                Box3d(V3d(l.X, c.Y, l.Z), V3d(c.X, u.Y, c.Z))
                                Box3d(V3d(c.X, c.Y, l.Z), V3d(u.X, u.Y, c.Z))
                                Box3d(V3d(l.X, l.Y, c.Z), V3d(c.X, c.Y, u.Z))
                                Box3d(V3d(c.X, l.Y, c.Z), V3d(u.X, c.Y, u.Z))
                                Box3d(V3d(l.X, c.Y, c.Z), V3d(c.X, u.Y, u.Z))
                                Box3d(V3d(c.X, c.Y, c.Z), V3d(u.X, u.Y, u.Z))
                            ]

                        children |> List.iter (traverse (level + 1))
                    else
                        ()
                traverse 0 root

            member x.Dependencies = []

            member x.GetData (cell : LodDataNode) =
                async {
                    //do! Async.SwitchToThreadPool()
                    let box = cell.bounds

                    let points =
                        [|
                            for x in 0 .. 9 do
                                for y in 0 .. 9 do
                                    for z in 0 .. 9 do
                                        //if x = 0 || x = 9 || y = 0 || y = 9 || z = 0 || z = 9 then
                                            yield V3d(x,y,z)*0.1*box.Size + box.Min |> V3f.op_Explicit
                                            
                        |]

                    //let points = 
                    //    [| for x in 0 .. 9 do
                    //         for y in 0 .. 9 do
                    //            for z in 0 .. 9 do
                    //                yield V3d(x,y,z)*0.1*box.Size + box.Min |> V3f.op_Explicit
                    //     |]
                    let colors = Array.create points.Length (Helpers.randomColor())
                    //let points = Helpers.randomPoints cell.bounds 1000
                    //let b = Helpers.box (Helpers.randomColor()) cell.bounds
//                  
                    //do! Async.Sleep(100)
                    let mutable a = 0

//                    for i in 0..(1 <<< 20) do a <- a + 1
//
//                    let a = 
//                        let mutable a = 0
//                        for i in 0..(1 <<< 20) do a <- a + 1
//                        a

                    return Some <| IndexedGeometry(Mode = unbox a, IndexedAttributes = SymDict.ofList [ DefaultSemantic.Positions, points :> Array; DefaultSemantic.Colors, colors :> System.Array])
                }

    let data = DummyDataProvider(Box3d(V3d.OOO, 20.0 * V3d.III)) :> ILodData

    [<AutoOpen>]
    module Camera =
        type Mode =
            | Main
            | Test

        let mode = Mod.init Main

        let currentMain = ref (CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI)
        let currentTest = ref (CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI)

        let mainCam =
            adaptive {
                let! mode = mode
                match mode with
                    | Main ->
                        let! m = DefaultCameraController.control win.Mouse win.Keyboard win.Time !currentMain
                        currentMain := m
                        return m
                    | _ ->
                        return !currentMain
            }

        let gridCam =
            adaptive {
                let! mode = mode
                match mode with
                    | Test ->
                        let! m = DefaultCameraController.control win.Mouse win.Keyboard win.Time !currentTest
                        currentTest := m
                        return m
                    | _ ->
                        return !currentTest
            }

        let view =
            adaptive {
                let! mode = mode
                match mode with
                    | Main -> return! mainCam
                    | Test -> return! gridCam
            }

        win.Keyboard.KeyDown(Keys.Space).Values.Add(fun _ ->
            transact (fun () ->
                match mode.Value with
                    | Main -> Mod.change mode Test
                    | Test -> Mod.change mode Main

                printfn "mode: %A" mode.Value
            )
        )

        win.Keyboard.KeyDown(Keys.P).Values.Add(fun _ ->
            let task = win.RenderTask
            
            printfn "%A (%A)" task task.OutOfDate
            printfn "%A" view
        )

        let mainProj = Interactive.DefaultFrustum  
        let gridProj = Frustum.perspective 60.0 1.0 50.0 1.0 |> Mod.constant

        let proj =
            adaptive {
                let! mode = mode 
                match mode with
                    | Main -> return! mainProj
                    | Test -> return! gridProj
            }

    module Instanced =
        open FShade
        open Aardvark.SceneGraph.Semantics
        type Vertex = { 
                [<Position>]      pos   : V4d 
                [<Color>]         col   : V4d
                [<PointSize>] blubb : float
            }

        let trafo (v : Vertex) =
            vertex {
                return { 
                    v with blubb = 1.0
                           col = V4d(v.col.XYZ,0.5)
                }
            }
            
    let eff =
        let effects = [
            Instanced.trafo |> toEffect           
            DefaultSurfaces.vertexColor  |> toEffect         
        ]
        let e = FShade.Effect.compose effects
        FShadeSurface(e) :> ISurface 
//
//    let surf = 
//        win.Runtime.PrepareSurface(
//            win.FramebufferSignature,
//            eff
//        ) :> ISurface |> Mod.constant

    let useMyPC = true

    let progress = 
        {   
            LodProgress.activeNodeCount     = ref 0
            LodProgress.expectedNodeCount   = ref 0
            LodProgress.dataAccessTime      = ref 0L
            LodProgress.rasterizeTime       = ref 0L
        }

    let pointCloud data config =
        if useMyPC then MyPC(data, config, progress) :> ISg
        else Sg.pointCloud data config

    let cloud =
        pointCloud data {
            lodDecider              = Mod.constant (LodData.defaultLodDecider 20.0)
            maxReuseRatio           = 0.5
            minReuseCount           = 1L <<< 20
            pruneInterval           = 500
            customView              = Some (gridCam |> Mod.map CameraView.viewTrafo)
            customProjection        = Some (gridProj |> Mod.map Frustum.projTrafo)
            attributeTypes =
                Map.ofList [
                    DefaultSemantic.Positions, typeof<V3f>
                    DefaultSemantic.Colors, typeof<C4b>
                ]
            boundingBoxSurface      = None //Some surf
            progressCallback        = Action<_>(ignore)
        } 
                     
    let sg = 
        Sg.group' [
            cloud
                |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect 
                    Instanced.trafo |> toEffect                  
                    DefaultSurfaces.vertexColor  |> toEffect         
                    //DefaultSurfaces.pointSprite  |> toEffect     
                    //DefaultSurfaces.pointSpriteFragment  |> toEffect 
                ]
            Helpers.frustum gridCam gridProj

            data.BoundingBox.EnlargedByRelativeEps(0.005)
                |> Sg.wireBox' C4b.VRVisGreen 

        ]

    let final =
        sg |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect                
                DefaultSurfaces.vertexColor  |> toEffect 
                ]
            |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo ) 
            |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo    )
            |> Sg.uniform "PointSize" (Mod.constant 4.0)
            |> Sg.uniform "ViewportSize" win.Sizes
    
    let run() =
        //Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])
        Interactive.SceneGraph <- final
        Interactive.RunMainLoop()



open LoD

#if INTERACTIVE
Interactive.SceneGraph <- final
#else
#endif