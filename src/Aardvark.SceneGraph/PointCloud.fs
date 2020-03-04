namespace Aardvark.SceneGraph

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive

open System.Threading
open System.Collections.Concurrent
open System.Collections.Generic

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


    let cache = ConcurrentDictionary<LodDataNode, Stepwise<unit> * CancellationTokenSource>(1,0)
    let runEffects (set : IAdaptiveObject) (c : ConcurrentDeltaPriorityQueue<LodDataNode, _>) (f : CancellationToken -> LodDataNode -> Stepwise<unit>) (undo : LodDataNode -> unit)  =
        //let working = ConcurrentHashSet<'a>()

        let markThings = MVar.empty()

        let marker =
            async {
                while true do
                    do! MVar.takeAsync markThings
                    do! Async.Sleep 50
                    transact (fun () -> set.MarkOutdated())
            }

        Async.Start marker

        let a =
            async {
                do! Async.SwitchToNewThread()

                while true do
                    let d = 
                        
                        let res = c.Dequeue()
                        lock c (fun () ->
                            match res with
                                | Add(_,v) ->
                                    let stepwise,cts = 
                                        cache.GetOrAdd(v, fun v -> 
                                            let cts = new CancellationTokenSource()
                                            let s = f cts.Token v
                                            s,cts
                                        )
                                    Choice1Of2 (v, stepwise, cts)
                                | Rem(_,v) ->
                                    match cache.TryRemove v with
                                        | (true, (_,cts)) -> Choice2Of2(v,cts)
                                        | _ -> 
                                            Log.error "cannot remove %A" v
                                            failwith "asdasdsad"
                        )
                    MVar.put markThings ()

                    match d with
                        | Choice1Of2(v, stepwise, cts) ->
                            match Stepwise.run cts.Token stepwise with
                                | [] -> 
                                    undo v
                                    Log.line "cancelled something"
                                | undoThings -> 
                                    cts.Token.Register(fun () -> List.iter (fun i -> i ()) undoThings) |> ignore
                        | Choice2Of2(v, cts) ->
                            undo v
                            cts.Cancel()
//                    let value = d.Value
//                    
//                    MVar.put markThings ()
//
//                    match d with
//                        | Add(_,v) -> 
//                            let stepwise,cts = 
//                                cache.GetOrAdd(v, fun v -> 
//                                    let cts = new CancellationTokenSource()
//                                    //cts.Token.Register (fun () -> undo v) |> ignore
//                                    let s = f cts.Token v
//                                    s,cts
//                                )
//                            //if cts.Token.IsCancellationRequested then undo v
//                            match Stepwise.run cts.Token stepwise with
//                                | [] -> 
//                                    undo v
//                                    Log.line "cancelled something"
//                                | undoThings -> 
//                                    cts.Token.Register(fun () -> List.iter (fun i -> i ()) undoThings) |> ignore
//                        | Rem(_,v) ->
//                            undo v
//                            let mutable t = Unchecked.defaultof<_>
//                            while not (cache.TryRemove(v, &t)) do
//                                Log.warn "waiting"
//                                ()
//
//                            let (stepwise, cts) = t
//                            cts.Cancel()

//
//                            match cache.TryRemove(v) with
//                                | (true,(stepwise,cts)) ->
//                                    cts.Cancel()
//                                | _ -> 
//                                    
//                                    Log.error "the impossible happened"
//                                    System.Diagnostics.Debugger.Break()

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

    type ProgressReport =
        {
            activeNodeCount     : int
            pendingOperations   : int
        }
    

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Progress =
        let empty = { activeNodeCount = ref 0; expectedNodeCount = ref 0; dataAccessTime = ref 0L; rasterizeTime = ref 0L }

        let toReport (prog : Progress) = { ProgressReport.activeNodeCount = !prog.activeNodeCount; ProgressReport.pendingOperations = !prog.expectedNodeCount }

        let timed (s : ref<_>) f = 
            let sw = System.Diagnostics.Stopwatch()
            sw.Start()
            let r = f ()
            sw.Stop()
            Interlocked.Add(s,sw.Elapsed.Ticks) |> ignore
            r

open LodProgress

open LodProgress

type PointCloudInfo =
    {
        /// the element-types for all available attributes
        attributeTypes : Map<Symbol, Type>
        
        /// rasterizer function
        lodRasterizer : aval<LodData.SetRasterizer>

        /// freeze LOD loading?
        freeze : aval<bool>

        /// an optional custom view trafo
        customView : Option<aval<Trafo3d>>

        /// an optional custom view projection trafo
        customProjection : Option<aval<Trafo3d>>

        /// the maximal percentage of inactive vertices kept in memory
        /// For Example a value of 0.5 means that at most 50% of the vertices in memory are inactive
        maxReuseRatio : float

        /// the minimal number of inactive vertices kept in memory
        minReuseCount : int64

        /// the time interval for the pruning process in ms
        pruneInterval : int

        /// optional surface for bounding boxes of cells that are load in progress.
        // the surface should properly transform instances by using DefaultSemantic.InstanceTrafo
        boundingBoxSurface : Option<aval<ISurface>>

        progressCallback : Option<Action<LoaderProgress>>
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
open System.Collections.Generic

module Helper =

    open System
    open System.Threading
    open System.Threading.Tasks
    open Aardvark.Base
    open Aardvark.Base.Ag
    open Aardvark.Base.Rendering
    open FSharp.Data.Adaptive
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

    module PointCloudRenderer = 
        open Aardvark.Base.Rendering
        open System
        open System.Threading
        open Aardvark.Base
        open FSharp.Data.Adaptive
        open Aardvark.SceneGraph
        open Aardvark.SceneGraph.Semantics
        open Aardvark.Base.Ag

        type private LoadedGeometry(pool : IGeometryPool, ptr : Management.Block<unit>, geometry : Option<IndexedGeometry>) =
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

            let rasterizer = 
                config.lodRasterizer

            let view : aval<Trafo3d> = 
                match config.customView with
                    | Some view -> view
                    | None -> 
                        match ro.Uniforms.TryGetUniform(scope, Symbol.Create "ViewTrafo") with
                            | Some (:? aval<Trafo3d> as v) -> v
                            | _ -> scope?ViewTrafo

            let proj : aval<Trafo3d> = 
                match config.customProjection with
                    | Some proj -> proj
                    | None -> 
                        match ro.Uniforms.TryGetUniform(scope, Symbol.Create "ProjTrafo") with
                            | Some (:? aval<Trafo3d> as v) -> v
                            | _ -> scope?ProjTrafo

            let size : aval<V2i> = 
                match ro.Uniforms.TryGetUniform(scope, Symbol.Create "ViewportSize") with
                    | Some (:? aval<V2i> as v) -> v
                    | _ -> scope?ViewportSize
                    

            
            let vp =
                let both = AVal.map2 (fun a b -> a,b) view proj 
                config.freeze |> AVal.bind(fun frozen ->
                    if frozen then
                        AVal.constant (view.GetValue(), proj.GetValue())
                    else
                        both
                )

            let dependencies =
                AVal.custom (fun token ->
                    let view,proj = vp.GetValue(token)
                    let size = size.GetValue(token)
                    let rasterizer = rasterizer.GetValue(token)

                    for d in data.Dependencies do d.GetValueUntyped token |> ignore

                    view, proj, size, rasterizer
                )

            let rasterize (view : Trafo3d, proj : Trafo3d, size : V2i, rasterizer : LodData.SetRasterizer) =
                data.Rasterize(view, proj, size, rasterizer)

            let pool = runtime.CreateGeometryPool(config.attributeTypes)
            let mutable refCount = 0
            
            let oru = ref true

            let release() =
                if Interlocked.Decrement(&refCount) = 0 then
                    pool.Dispose()
                    oru := false

            let activate() =
                Interlocked.Increment(&refCount) |> ignore
                { new IDisposable with member x.Dispose() = release() }

            let progress (p : LoaderProgress) =
                
                match config.progressCallback with
                | Some f -> f.Invoke p
                | None -> ()
//                    let add = float p.queueAdds / float p.targetCount
//                    let rem = float p.queueRemoves / float p.currentCount
//                    Log.start "progress"
//                    Log.line "overall: %.2f%%" (100.0 - 100.0 * add)
//                    Log.line "memory:  %A" pool.UsedMemory
//                    Log.line "load:    %A" p.avgLoadTime
//                    Log.line "unload:  %A" p.avgRemoveTime
//                    Log.line "raster:  %A" p.avgEvaluateTime
//                    Log.stop()

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
                let load (ct : CancellationToken) (node : ILodDataNode) =
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
                        

                let rasterize = rasterize
                let dependencies = dependencies 
                let paramse = {
                    continueLoader      = fun _ -> !oru
                    load                = load
                    unload              = unload
                    priority            = fun op -> if op.Count < 0 then -op.Value.Level else op.Value.Level
                    numThreads          = 4
                    submitDelay         = TimeSpan.FromMilliseconds 120.0
                    progressInterval    = TimeSpan.FromSeconds 1.0
                    progress            = progress
                    frozen              = config.freeze
                }
                
                let load = Loader.load

                load rasterize paramse dependencies

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
                    |> AVal.map (fun ranges ->
                        let calls = ranges |> RangeSet.toSeq |> Seq.map call |> Seq.toArray
                        IndirectBuffer.ofArray false calls 
                    )

            ro.DrawCalls <- Indirect drawCallBuffer
            ro.Mode <- IndexedGeometryMode.PointList
            ro.VertexAttributes <- vertexAttributes
            ro.Activate <- activate

            ro :> IRenderObject

        [<Rule>]
        type MyPCSem() =
            member x.RenderObjects(m : Sg.PointCloud, scope : Ag.Scope) =
                let obj = createRenderObject scope m.Config m.Data
                ASet.single obj

