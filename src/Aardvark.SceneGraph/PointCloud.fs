namespace Aardvark.SceneGraph

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental

open System.Threading
open System.Collections.Concurrent

module StepwiseProgress =
    
    type Undo = list<unit -> unit>

    type CompState =
        {
            onCancel : list<unit -> unit>
            onError : list<exn -> unit>
        }

    type Comp<'a> = 
        | Cancelled
        | Error of exn
        | Result of 'a
        | Continue of (CompState -> CompState * Comp<'a>)

    module Stepwise =
        let rec bind (f : 'a -> Comp<'b>) (m : Comp<'a>) =
            match m with
                | Cancelled -> Cancelled
                | Error e -> Error e
                | Result v -> f v
                | Continue run ->
                    Continue (fun s ->
                        let s, v = run s
                        s, bind f v
                    )

        let rec combine (l : Comp<unit>) (r : Comp<'a>) =
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


        let register (undo : unit -> unit) : Comp<unit> =
            Continue (fun s ->
                let s = { s with onCancel = undo::s.onCancel }
                s, Result ()
            )

        let step (c : Comp<'a>) (s : CompState) =
            match c with
                | Cancelled -> s, Cancelled
                | Result a -> s, Result a
                | Error e -> s, Error e
                | Continue c -> c s

        let rec private runAll (l : list<'a -> unit>) (arg : 'a) =
            match l with
                | [] -> ()
                | h::rest -> h arg; runAll rest arg

        let run (c : Comp<'a>) =
            let rec run (c : Comp<'a>) (s : CompState) =
                match c with
                    | Cancelled -> 
                        runAll s.onCancel ()
                        None

                    | Error e -> 
                        runAll s.onError e
                        None

                    | Continue c -> 
                        let (s, v) = c s
                        run v s

                    | Result a -> Some a
            
            run c { onCancel = []; onError = []; }
            

        let rec stepKnot (m : Comp<'a>) (ct : System.Threading.CancellationToken)  =
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
        
    let stepwise = StepwiseBuilder()

    module Test =   

        let a =
            stepwise {
                let! _ = Stepwise.register (fun () -> printfn "cancel 1")
                let! a = Stepwise.result 1
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

        /// optional surface for bounding boxes of cells that are not loaded yet
        /// shader vertex must have ZZZInstancedTrafo semantic
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


    type LoadResult = CancellationTokenSource * ref<IndexedGeometry * GeometryRef>

    type PointCloudHandler(node : Sg.PointCloud, view : IMod<Trafo3d>, proj : IMod<Trafo3d>, viewportSize : IMod<V2i>, progress : LodProgress.Progress, runtime : IRuntime) =
        let cancel = new System.Threading.CancellationTokenSource()

        let pool = GeometryPool.createAsync runtime
        let calls = DrawCallSet(false)
        let inactive = ConcurrentHashQueue<GeometryRef>()
        let mutable inactiveSize = 0L
        let mutable activeSize = 0L

        let workingSets = CSet.empty

        let mutable pendingRemoves = 0
        let geometries = System.Collections.Concurrent.ConcurrentDictionary<LodDataNode, LoadResult>()


        let activate (n : GeometryRef) =
            let size = n.range.Size

            if inactive.Remove n then
                Interlocked.Add(&inactiveSize, int64 -size) |> ignore

            if calls.Add n.range then
                Interlocked.Add(&activeSize, int64 size) |> ignore
                Interlocked.Increment(progress.activeNodeCount) |> ignore


        let deactivate (n : GeometryRef) =
            let size = int64 n.range.Size

            if inactive.Enqueue n then
                Interlocked.Add(&inactiveSize, size) |> ignore

            if calls.Remove n.range then
                Interlocked.Add(&activeSize, -size) |> ignore
                Interlocked.Decrement(progress.activeNodeCount) |> ignore
            else printfn "could not remove calls"

        member x.Add(n : LodDataNode) =
            Interlocked.Increment(progress.expectedNodeCount) |> ignore

            let cts = new System.Threading.CancellationTokenSource()
            let loadData _ =
//                async {
                    let geometry = Async.RunSynchronously(node.Data.GetData(n), cancellationToken = cts.Token)
                    geometry,Unchecked.defaultof<_>
//                }

            let undoLoad (geo,ref) = () 

            let addToPool (ig,r) =
//                async {
                    let range = pool.Add ig
                    ig, { node = n; geometry = ig; range = range }
//                }

            let removeFromPool (ig,r) = 
                pool.Remove ig |> ignore

            let addToRender (ig,r) =
//                async { 
                    do activate r
                    do transact ( fun () -> CSet.remove r.node workingSets |> ignore )
                    ig,r
//                }

            let removeFromRender (ig,r) = deactivate r

            let effects =
                [
                    loadData,    undoLoad
                    addToPool,   removeFromPool
                    addToRender, removeFromRender
                ]

            let result = ref Unchecked.defaultof<_>
            let r = lock geometries (fun _ -> geometries.GetOrAdd(n, (cts, result)))
            try
                Async.RunSynchronously(CancellationUtilities.runWithCompensations result effects, cancellationToken = cts.Token)
            with | :? OperationCanceledException as o -> ()
            r
                    
        member x.Remove(n : LodDataNode) =
            lock geometries (fun _ -> 
                  match geometries.TryRemove n with
                    | (true,(cts,r)) ->
                        Interlocked.Decrement(progress.expectedNodeCount) |> ignore
                        cts.Cancel()
                        //cts.Dispose()
                    | _ -> 
                        Log.warn "could not remove lod node"
                  )

        member x.WorkingSet = 
            workingSets :> aset<_>

        member x.Activate() =
                
            let wantedNearPlaneDistance =
                Mod.custom (fun self ->
                    let viewportSize = viewportSize.GetValue self
                    let wantedPixelDistance = node.Config.targetPointDistance.GetValue self

                    let size = max viewportSize.X viewportSize.Y
                    2.0 * float wantedPixelDistance / float size
                )


            let deltas = new ConcurrentDeltaQueue<_>()
            let r = MVar<_>()

            let run =
                let content = System.Collections.Generic.HashSet<_>()
                async {
                    do! Async.SwitchToNewThread()

                    while true do
                        
                        let v = view.GetValue ()
                        let p = proj.GetValue ()
                        let wantedNearPlaneDistance = wantedNearPlaneDistance.GetValue ()
                    
                        for a in node.Data.Dependencies do a.GetValue () |> ignore

                        let set = Progress.timed progress.rasterizeTime (fun () ->
                            node.Data.Rasterize(v, p, wantedNearPlaneDistance)
                        ) 

                        let add = System.Collections.Generic.HashSet<_>( set     |> Seq.filter (content.Contains >> not)  )
                        let rem = System.Collections.Generic.HashSet<_>( content |> Seq.filter (set.Contains >> not)      )

                        // prune deltas manually
                        let result = System.Collections.Generic.List()
                        for a in add do
                            if rem.Contains a then ()
                            else result.Add (Add a)
                        for r in rem do
                            if add.Contains r then ()
                            else result.Add (Rem r)
                    
                        for r in result do 
                            deltas.Enqueue r
                            match r with 
                                | Add v -> 
                                    content.Add v |> ignore
                                    transact ( fun () -> CSet.add v workingSets |> ignore )
                                | Rem v -> 
                                    content.Remove v |> ignore
                                    transact ( fun () -> CSet.remove v workingSets |> ignore )
                }

            view.AddMarkingCallback (fun () -> r.Put ()) |> ignore
            proj.AddMarkingCallback (fun () -> r.Put ()) |> ignore
            wantedNearPlaneDistance.AddMarkingCallback (fun () -> r.Put ()) |> ignore
            for a in node.Data.Dependencies do a.AddMarkingCallback (fun () -> r.Put ()) |> ignore

            r.Put()

            let deltaProcessing =    
                async {
                    do! Async.SwitchToNewThread()
                    while true do
                        let op = deltas.Dequeue()

                        match op with
                            | Add n -> lock n (fun _ -> x.Add n |> ignore)
                            | Rem n -> lock n (fun _ -> x.Remove n |> ignore)
                }

      
            let pruning =
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

            let printer =
                async {
                    while true do
                        do! Async.Sleep(1000)
                        printfn "active = %A / desired = %A / count = %A / inactiveCnt=%d / inactive=%A / rasterizeTime=%f [seconds]" progress.activeNodeCount.Value progress.expectedNodeCount.Value geometries.Count inactive.Count inactiveSize (float progress.rasterizeTime.Value / float TimeSpan.TicksPerSecond)
                        ()
                }


            for i in 1..1 do
                Async.StartAsTask(deltaProcessing, cancellationToken = cancel.Token) |> ignore
            //Async.StartAsTask(pruning, cancellationToken = cancel.Token) |> ignore
            Async.StartAsTask(printer, cancellationToken = cancel.Token) |> ignore
            Async.StartAsTask(run, cancellationToken = cancel.Token) |> ignore
            

            { new IDisposable with member x.Dispose() = () }


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

            obj.IndirectBuffer <- calls |> Mod.map (fun a -> ArrayBuffer(a) :> IBuffer)
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

            

