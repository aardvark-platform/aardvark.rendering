namespace Aardvark.SceneGraph

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental

open System.Threading
open System.Collections.Concurrent

module StepwiseProgress =
    

    type StepState =
        {
            onCancel : list<unit -> unit>
            onError : list<exn -> unit>
        }

    type Stepwise<'a> = 
        | Cancelled
        | Error of exn
        | Result of 'a
        | Continue of (StepState -> StepState * Stepwise<'a>)

    module Stepwise =
        let rec bind (f : 'a -> Stepwise<'b>) (m : Stepwise<'a>) =
            match m with
                | Cancelled -> Cancelled
                | Error e -> Error e
                | Result v -> f v
                | Continue run ->
                    Continue (fun s ->
                        let s, v = run s
                        s, bind f v
                    )

        let rec combine (l : Stepwise<unit>) (r : Stepwise<'a>) =
            match l with
                | Continue l ->
                    Continue (fun s ->
                        let s, cl = l s
                        s, combine cl r
                    )
                | Result () -> r
                | Error e -> Error e
                | Cancelled -> Cancelled

        let result v = Continue (fun s ->  s, Result v)
        let error exn = Error exn
        let cancel = Cancelled


        let register (undo : unit -> unit) : Stepwise<unit> =
            Continue (fun s ->
                let s = { s with onCancel = undo::s.onCancel }
                s, Result ()
            )

//        let finalize (finallyActions : unit -> unit) : Stepwise<'a> =
//            Continue (fun s ->
//                let s = { s with finallyActions = finallyActions::s.finallyActions }
//                s, Result ()
//            )

        let step (c : Stepwise<'a>) (s : StepState) =
            match c with
                | Cancelled -> s, Cancelled
                | Result a -> s, Result a
                | Error e -> s, Error e
                | Continue c -> c s

        let rec private runAll (l : list<'a -> unit>) (arg : 'a) =
            match l with
                | [] -> ()
                | h::rest -> h arg; runAll rest arg

        let run (ct : System.Threading.CancellationToken) (c : Stepwise<unit>)=
            let rec run (c : Stepwise<'a>) (s : StepState) =
                if ct.IsCancellationRequested then 
                    runAll s.onCancel ()
                    []
                else
                    match c with
                        | Cancelled -> 
                            runAll s.onCancel ()
                            []

                        | Error e -> 
                            runAll s.onError e
                            []

                        | Continue c -> 
                            let (s, v) = c s
                            run v s

                        | Result a -> s.onCancel
            
            run c { onCancel = []; onError = []; }
            

        let rec stepKnot (m : Stepwise<'a>) (ct : System.Threading.CancellationToken)  =
            let mutable s = { onCancel = []; onError = []; }
            let current = ref m
            let f () =
                if ct.IsCancellationRequested then 
                    match !current with
                        | Cancelled -> !current
                        | _ ->
                            runAll s.onCancel ()
                            let r = Cancelled
                            current := r 
                            r
                else
                    let (state,r) = step !current s
                    s <- state
                    current := r
                    r
            f, current

    type StepwiseBuilder() =
        member x.Bind(m,f) = Stepwise.bind f m
        member x.Return v = Stepwise.result v
        member x.ReturnFrom(f : Stepwise<'a>) = f
        member x.Combine(l,r) = Stepwise.combine l r
        member x.Delay(f) = f ()
        member x.Zero() = ()
       
        
    let stepwise = StepwiseBuilder()

    module Test =   

        let a =
            stepwise {
                let! _ = Stepwise.register (fun () -> printfn "cancel 1")
                let! a = stepwise { return 10 }
                let! _ = Stepwise.register (fun () -> printfn "cancel 2")
                let! b = Stepwise.result 2
                return a + b 
            }

        let cts = new System.Threading.CancellationTokenSource()

        let proceed,r = Stepwise.stepKnot a cts.Token
        let r2 = proceed ()
        let r3 = proceed ()
        let r4 = proceed ()
        cts.Cancel()
        let r5 = proceed ()
        printfn "%A" r
        ()


module StepwiseQueueExection =
    open StepwiseProgress


    let runEffects (c : ConcurrentDeltaQueue<'a>) (f : CancellationToken -> 'a -> Stepwise<unit>) (undo : 'a -> unit)  =
        let cache = ConcurrentDictionary<'a, Stepwise<unit> * CancellationTokenSource>(1,0)
        //let working = ConcurrentHashSet<'a>()
        let a =
            async {
                while true do
                    let! d = c.DequeueAsync()
                    let value = d.Value
                    

                    match d with
                        | Add v -> 
                            let stepwise,cts = cache.GetOrAdd(v, fun v -> 
                                let cts = new CancellationTokenSource()
                                //cts.Token.Register (fun () -> undo v) |> ignore
                                let s = f cts.Token v
                                s,cts)
                            //if cts.Token.IsCancellationRequested then undo v
                            match Stepwise.run cts.Token stepwise with
                                | [] -> 
                                    undo v
                                    Log.line "cancelled something"
                                | undoThings -> 
                                    cts.Token.Register(fun () -> List.iter (fun i -> i ()) undoThings) |> ignore
                        | Rem v ->
                            undo v
                            match cache.TryRemove(v) with
                                | (true,(stepwise,cts)) ->
                                    cts.Cancel()
                                | _ -> 
                                    Log.error "the impossible happened"
                                    System.Diagnostics.Debugger.Break()

            }
        a, (fun () -> cache.Count)


module LodProgress =

    [<CustomEquality; NoComparison>]
    type GeometryRef = { node : LodDataNode; geometry : IndexedGeometry; range : Range1i } with

        override x.GetHashCode() = HashCode.Combine(x.node.GetHashCode(), x.range.GetHashCode())
        override x.Equals o =
            match o with
                | :? GeometryRef as o -> 
                    x.node.Equals(o.node) && x.range = o.range
                | _ -> false


    type Progress =
        {   
            activeNodeCount     : ref<int>
            expectedNodeCount   : ref<int>
            dataAccessTime      : ref<int64> // ticks
            rasterizeTime       : ref<int64> // ticks
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Progress =
        let empty = { activeNodeCount = ref 0; expectedNodeCount = ref 0; dataAccessTime = ref 0L; rasterizeTime = ref 0L }


        let timed (s : ref<_>) f = 
            let sw = System.Diagnostics.Stopwatch()
            sw.Start()
            let r = f ()
            sw.Stop()
            Interlocked.Add(s,sw.Elapsed.Ticks) |> ignore
            r

open LodProgress

type PointCloudInfo =
    {
        /// the element-types for all available attributes
        attributeTypes : Map<Symbol, Type>

        /// the target point distance in pixels
        targetPointDistance : IMod<float>

        /// an optional custom view trafo
        customView : Option<IMod<Trafo3d>>

        /// an optional custom view projection trafo
        customProjection : Option<IMod<Trafo3d>>

        /// the maximal percentage of inactive vertices kept in memory
        /// For Example a value of 0.5 means that at most 50% of the vertices in memory are inactive
        maxReuseRatio : float

        /// the minimal number of inactive vertices kept in memory
        minReuseCount : int64

        /// the time interval for the pruning process in ms
        pruneInterval : int

        /// optional surface for bounding boxes of cells that are load in progress.
        // the surface should properly transform instances by using DefaultSemantic.InstanceTrafo
        boundingBoxSurface : Option<IMod<ISurface>>
    }

[<AutoOpen>]
module ``PointCloud Sg Extensions`` =

    open LodProgress

    module Sg = 
        type PointCloud(data : ILodData, config : PointCloudInfo, progress : Progress) =
            interface ISg

            member x.Data = data
            member x.Config = config
            member x.Progress = progress

        let pointCloud (data : ILodData) (info : PointCloudInfo) =
            PointCloud(data, info, Progress.empty) :> ISg

        let pointCloud' (data : ILodData) (info : PointCloudInfo) (progress : Progress) =
            PointCloud(data, info, progress) :> ISg




module CancellationUtilities =
        
    let runWithCompensations (outRef : ref<'a>) (xs : list<('a -> 'a) * ('a -> unit) >) =  
        let rec doWork xs disposables =
            async {
                match xs with
                    | (m,comp)::xs ->
                            let resultValue = ref None
                            let! d = 
                                Async.OnCancel(fun () -> 
                                    let r = !resultValue
                                    match r with
                                        | Some (v,(d:IDisposable)) -> 
                                            comp v
                                            d.Dispose()
                                        | None -> ()
                                )
                            let r = m !outRef
                            do resultValue := Some (r,d); outRef := r
                            return! doWork xs (d :: disposables)
                    | [] -> return () 
            }
        doWork xs []
                

namespace Aardvark.SceneGraph.Semantics

module Helper =

    open System
    open System.Threading
    open System.Threading.Tasks
    open Aardvark.Base
    open Aardvark.Base.Ag
    open Aardvark.Base.Rendering
    open Aardvark.Base.Incremental
    open Aardvark.SceneGraph
    open Aardvark.SceneGraph.Semantics
    open LodProgress

    let ig ( box : Box3d ) : IndexedGeometry =
        
        let pa = [|
            V3f(box.Min.X, box.Min.Y, box.Min.Z);
            V3f(box.Max.X, box.Min.Y, box.Min.Z);
            V3f(box.Max.X, box.Max.Y, box.Min.Z);
            V3f(box.Min.X, box.Max.Y, box.Min.Z);
            V3f(box.Min.X, box.Min.Y, box.Max.Z);
            V3f(box.Max.X, box.Min.Y, box.Max.Z);
            V3f(box.Max.X, box.Max.Y, box.Max.Z);
            V3f(box.Min.X, box.Max.Y, box.Max.Z);
            |]

        let pos = [|
                pa.[0]; pa.[1]; pa.[1]; pa.[2]; pa.[2]; pa.[3]; pa.[3]; pa.[0];
                pa.[4]; pa.[5]; pa.[5]; pa.[6]; pa.[6]; pa.[7]; pa.[7]; pa.[4];
                pa.[0]; pa.[4]; pa.[1]; pa.[5]; pa.[2]; pa.[6]; pa.[3]; pa.[7];
                |]
        
        let attrs = [
                        (DefaultSemantic.Positions, pos :> Array)
                        (DefaultSemantic.Colors, (C4b.Red |> Array.replicate (pos |> Array.length)) :> Array)
                    ] |> SymDict.ofList
            
        
        IndexedGeometry(
                    Mode = IndexedGeometryMode.LineList,
                    IndexedAttributes = attrs
                )

module PointCloudRenderObjectSemantics = 

    open System
    open System.Threading
    open System.Threading.Tasks
    open Aardvark.Base
    open Aardvark.Base.Ag
    open Aardvark.Base.Rendering
    open Aardvark.Base.Incremental
    open Aardvark.SceneGraph
    open LodProgress
    open StepwiseProgress


    type LoadResult = CancellationTokenSource * ref<IndexedGeometry * GeometryRef>

    type PointCloudHandler(node : Sg.PointCloud, view : IMod<Trafo3d>, proj : IMod<Trafo3d>, viewportSize : IMod<V2i>, progress : LodProgress.Progress, runtime : IRuntime) =
        let queueCount = 8
        let cancel = new System.Threading.CancellationTokenSource()
        let l = obj()
        let mutable currentId = 0

        //let d = System.Collections.Concurrent.ConcurrentDictionary<LodDataNode,int>()

//        let getId (n : LodDataNode) =
////            d.GetOrAdd(n, fun n ->
////                Interlocked.Increment &currentId
////            )
//            if n.uniqueId <> 0 then n.uniqueId
//            else 
//                let id = Interlocked.Increment(&currentId)
//                n.uniqueId <- id
//                id


        let pool = GeometryPool.createAsync runtime
        let calls = DrawCallSet(true)
        let mutable activeSize = 0L


        let pruning = false
        let inactive = ConcurrentHashQueue<GeometryRef>()
        let mutable inactiveSize = 0L

        let workingSet = CSet.empty


        let activate (n : GeometryRef) =
            let size = n.range.Size

            if pruning then
                if inactive.Remove n then
                    Interlocked.Add(&inactiveSize, int64 -size) |> ignore

            if n.range.Size >= 0 then
                if calls.Add n.range then
                    Interlocked.Add(&activeSize, int64 size) |> ignore
                    Interlocked.Increment(progress.activeNodeCount) |> ignore
                else
                    Log.line "could not add calls"

        let deactivate (n : GeometryRef) =
            let size = int64 n.range.Size

            if pruning then
                if inactive.Enqueue n then
                    Interlocked.Add(&inactiveSize, size) |> ignore

            if n.range.Size >= 0 then
                if calls.Remove n.range then
                    Interlocked.Add(&activeSize, -size) |> ignore
                    Interlocked.Decrement(progress.activeNodeCount) |> ignore
                else 
                    Log.line "could not remove calls"

        let removeFromWorkingSet n = 
            if node.Config.boundingBoxSurface.IsSome  then
                lock l (fun () -> 
                    transact (fun () -> workingSet.Remove n |> ignore)
                )

        member x.WorkingSet = 
            workingSet :> aset<_>

        member x.Activate() =
                
            let wantedNearPlaneDistance =
                Mod.custom (fun self ->
                    let viewportSize = viewportSize.GetValue self
                    let wantedPixelDistance = node.Config.targetPointDistance.GetValue self

                    let size = max viewportSize.X viewportSize.Y
                    2.0 * float wantedPixelDistance / float size
                )


            let deltas = Array.init queueCount (fun _ -> new ConcurrentDeltaQueue<_>())
            let r = MVar<_>()

            let run =
                let mutable currentId = 0
                let content = Dict<_,_>()

                let getId v =
                    content.GetOrCreate(v, fun _ -> 
                        Interlocked.Increment(&currentId)
                    )

                let removeId v =
                    match content.TryRemove v with
                        | (true, id) -> id
                        | _ -> failwith "removal of unknown object"

                async {
                    do! Async.SwitchToNewThread()

                    while true do

                        do! r.TakeAsync()
                        
                        let v = view.GetValue ()
                        let p = proj.GetValue ()
                        let wantedNearPlaneDistance = wantedNearPlaneDistance.GetValue ()
                    
                        for a in node.Data.Dependencies do a.GetValue () |> ignore

                        let set = Progress.timed progress.rasterizeTime (fun () ->
                            node.Data.Rasterize(v, p, wantedNearPlaneDistance)
                        ) 

                        let add = System.Collections.Generic.HashSet<_>( set     |> Seq.filter (content.ContainsKey >> not) )
                        let rem = System.Collections.Generic.HashSet<_>( content.Keys |> Seq.filter (set.Contains >> not)     )

                        for v in add do
                            let id = getId v
                            if deltas.[id % queueCount].Add v then
                                if node.Config.boundingBoxSurface.IsSome  then
                                    lock l (fun () ->  
                                        if not <| workingSet.Contains v then
                                            transact (fun () -> CSet.add v workingSet |> ignore)
                                    )
                            else
                                ()

                        for v in rem do
                            let id = removeId v
                            if deltas.[id % queueCount].Remove v then 
                                ()
                            else
                                lock l (fun () ->  
                                    if workingSet.Contains v then
                                        transact (fun () -> CSet.remove v workingSet |> ignore)
                                )

                }

            let subV = view.AddMarkingCallback (fun () -> r.Put ()) 
            let subP = proj.AddMarkingCallback (fun () -> r.Put ())
            let subD = wantedNearPlaneDistance.AddMarkingCallback (fun () -> r.Put ())
            for a in node.Data.Dependencies do a.AddMarkingCallback (fun () -> r.Put ()) |> ignore

            r.Put()


            let effect (ct : CancellationToken) (n : LodDataNode) =
                stepwise {
                    let data =  
                        try 
                            Async.RunSynchronously(node.Data.GetData(n), cancellationToken = ct) |> Some
                        with | e -> 
                            removeFromWorkingSet n
                            Log.warn "data got cancelled"; 
                            None
                    match data with
                        | Some (Some v) ->
                            do! Stepwise.register (fun () -> pool.Remove v |> ignore)
                            let range = pool.Add v
                            let gref = { geometry = v; range = range; node = n }
                            let mutable activated = false
                            do! Stepwise.register (fun () -> if activated then deactivate gref)
                            let () =
                                activate gref
                                activated <- true
                                removeFromWorkingSet n
                            return ()
                        | None -> return! Stepwise.cancel
                        | Some None -> return ()
                }


            
      
            let pruningTask =
                async {
                    while true do
                        let mutable cnt = 0

                        let shouldContinue () =
                            if inactiveSize > node.Config.minReuseCount then 
                                let ratio = float inactiveSize / float (inactiveSize + activeSize)
                                if ratio > node.Config.maxReuseRatio then true
                                else false
                            else
                                false

                        while shouldContinue () do
                            match inactive.TryDequeue() with
                                | (true, v) ->
                                    pool.Remove v.geometry |> ignore
                                    cnt <- cnt + 1
                                | _ ->
                                    Log.warn "inactive: %A / count : %A" inactiveSize inactive.Count 
                                    inactiveSize <- 0L
                            
                        do! Async.Sleep node.Config.pruneInterval
                }

            let mutable deltaProcessors = 0
            let printer =
                async {
                    while true do
                        do! Async.Sleep(1000)
                        //printfn "workers: %d / active = %A / desired = %A / inactiveCnt=%d / inactive=%A / rasterizeTime=%f [seconds] / count: %d / working: %d" deltaProcessors progress.activeNodeCount.Value progress.expectedNodeCount.Value inactive.Count inactiveSize (float progress.rasterizeTime.Value / float TimeSpan.TicksPerSecond) pool.Count workingSet.Count
                        //printfn "workers: %d / active = %A / desired = %A / inactiveCnt=%d / inactive=%A / rasterizeTime=%f [seconds] / count: %d / working: %d" deltaProcessors progress.activeNodeCount.Value progress.expectedNodeCount.Value inactive.Count inactiveSize (float progress.rasterizeTime.Value / float TimeSpan.TicksPerSecond) pool.Count workingSet.Count
                        ()
                }


            for i in 0..queueCount-1 do
                let deltaProcessing,info =  StepwiseQueueExection.runEffects deltas.[i] effect removeFromWorkingSet
                let safeDeltas =
                    async {
                        deltaProcessors <- deltaProcessors + 1
                        try
                            try
                                return! deltaProcessing
                            with e -> Log.error "delta processor died!!!"
                        finally 
                            Log.warn "ending delta processing"
                            deltaProcessors <- deltaProcessors - 1
                    }
                Async.StartAsTask(safeDeltas, cancellationToken = cancel.Token) |> ignore
            
            if pruning then Async.StartAsTask(pruningTask, cancellationToken = cancel.Token) |> ignore
            Async.StartAsTask(printer, cancellationToken = cancel.Token) |> ignore
            Async.StartAsTask(run, cancellationToken = cancel.Token) |> ignore
            

            { new IDisposable with 
                member x.Dispose() =
                    cancel.Cancel()
                    subV.Dispose()
                    subP.Dispose()
                    subD.Dispose()
            }


        member x.Attributes =
            { new IAttributeProvider with
                member x.TryGetAttribute sem =
                    match Map.tryFind sem node.Config.attributeTypes with
                        | Some t ->
                            let b = pool.GetBuffer sem
                            BufferView(b, t) |> Some
                        | None ->
                            None

                member x.All = Seq.empty
                member x.Dispose() = ()
            }

        member x.DrawCallInfos =
            calls |> DrawCallSet.toMod

        
    [<Semantic>]
    type PointCloudSemantics() =
        member x.RenderObjects(l : Sg.PointCloud) =
            let obj = RenderObject.create()

            let view = 
                match l.Config.customView with
                    | Some v -> v
                    | None -> l.ViewTrafo

            let proj = 
                match l.Config.customProjection with
                    | Some p -> p
                    | None -> l.ProjTrafo

            let viewportSize =
                match obj.Uniforms.TryGetUniform(obj.AttributeScope, Symbol.Create "ViewportSize") with
                    | Some (:? IMod<V2i> as vs) -> vs
                    | _ -> failwith "[PointCloud] could not get viewport size (please apply to scenegraph)"

            let h = PointCloudHandler(l, view, proj, viewportSize, l.Progress, l.Runtime)

            let calls = h.DrawCallInfos

            obj.IndirectBuffer <- calls |> Mod.map IndirectBuffer.ofArray
            obj.Activate <- h.Activate
            obj.VertexAttributes <- h.Attributes
            obj.Mode <- Mod.constant IndexedGeometryMode.PointList
            obj.Surface <- l.Surface

            match l.Config.boundingBoxSurface with
                | None -> ASet.single (obj :> IRenderObject)
                | Some surf ->
                    let trafos = 
                        h.WorkingSet 
                            |> ASet.toMod 
                            |> Mod.map ( fun a -> a |> Seq.toArray |> Array.map ( fun v -> 
                                let box = v.bounds
                                let shift = Trafo3d.Translation(box.Center)
                                let bias = Trafo3d.Translation(-V3d.III * 0.5)
                                let scale = Trafo3d.Scale( box.SizeX, box.SizeY, box.SizeZ )
                                bias*scale*shift ))
                    
                    let unitbox = (Box3d.Unit |> Helper.ig)

                    let iSg = 
                        unitbox |> Sg.instancedGeometry trafos 
                                |> Sg.surface surf

                    let ros = Semantics.RenderObjectSemantics.Semantic.renderObjects iSg

                    aset {
                        yield obj :> IRenderObject
                        yield! ros
                    }



