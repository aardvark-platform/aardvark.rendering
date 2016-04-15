namespace Aardvark.SceneGraph

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental

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

    }

[<AutoOpen>]
module ``PointCloud Sg Extensions`` =

    module Sg = 
        type PointCloud(data : ILodData, config : PointCloudInfo) =
            interface ISg

            member x.Data = data
            member x.Config = config

        let pointCloud (data : ILodData) (info : PointCloudInfo) =
            PointCloud(data, info) :> ISg
            

namespace Aardvark.SceneGraph.Semantics

open System
open System.Threading
open System.Threading.Tasks
open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph


module PointCloudRenderObjectSemantics = 

    [<CustomEquality; NoComparison>]
    type GeometryRef = { node : LodDataNode; geometry : IndexedGeometry; range : Range1i } with

        override x.GetHashCode() = HashCode.Combine(x.node.GetHashCode(), x.range.GetHashCode())
        override x.Equals o =
            match o with
                | :? GeometryRef as o -> 
                    x.node.Equals(o.node) && x.range = o.range
                | _ -> false

    type LoadTask(factory : TaskFactory, data : ILodData, node : LodDataNode, ct : CancellationToken, pool : GeometryPool, activate : GeometryRef -> unit, deactivate : GeometryRef -> unit) =
        let cancel = new CancellationTokenSource()
        let mutable killed = 0
        let mutable refCnt = 1
        let mutable running = true
     
        let task =
            let runner = 
                async {
                    do! Async.Sleep 0

                    let! geometry = data.GetData(node)
                    do! Async.OnCancel(fun () -> pool.Remove geometry |> ignore) |> Async.Ignore
                   
                    let range = pool.Add(geometry) 
                    
                    let res = { node = node; geometry = geometry; range = range }

                    try
                        return res
                    finally
                        running <- false
                        if refCnt > 0 then activate res
                        else deactivate res
                }
            Async.StartAsTask(runner, cancellationToken = cancel.Token)


        member x.Deactivate() =
            if killed = 1 then failwith "cannot deactive killed LoadTask"

            let newCnt = Interlocked.Decrement(&refCnt)
            if newCnt = 0 then
                if not running then 
                    if task.IsCanceled |> not then
                        deactivate task.Result

                    false
                else
                    match task.Status with
                        | TaskStatus.WaitingForActivation | TaskStatus.WaitingToRun ->
                            printfn "aborting the evil that men do"
                            cancel.Cancel()
                            true
                        | _ ->
                            false
            else
                false


        member x.Activate() =
            if killed = 1 then failwith "cannot active killed LoadTask"

            let newCnt = Interlocked.Increment(&refCnt)
            if newCnt = 1 then
                if not running then 
                    if task.IsCanceled |> not then
                        activate task.Result


        member x.Kill(cont : GeometryRef -> unit) =
            if Interlocked.Exchange(&killed, 1) = 0 then
                cancel.Cancel()

                let killNow (t : Task<_>) =
                    cont t.Result

                if task.IsCompleted then cont task.Result
                else task.ContinueWith killNow |> ignore

    module LoadIntoPool =
        
        let load (data : ILodData) (node : LodDataNode) (pool : GeometryPool) = 
            async {
                let! geometry = data.GetData(node)
                do! Async.OnCancel(fun () -> Log.line "rollback, remove from pool."; pool.Remove geometry |> ignore) |> Async.Ignore
                let range = pool.Add(geometry) 
                    
                return { node = node; geometry = geometry; range = range }
            }

    type LoadTaskasdasd(data : ILodData, node : LodDataNode, ct : CancellationToken, pool : GeometryPool, activate : GeometryRef -> unit, deactivate : GeometryRef -> unit) =
        let mutable result = None
        let mutable refCnt = 1

        let run =
            async {
                let! geometry = data.GetData(node)
                do! Async.OnCancel(fun () -> pool.Remove geometry |> ignore) |> Async.Ignore
                let range = pool.Add(geometry) 
                    
                return { node = node; geometry = geometry; range = range }
            }

        member x.Run() =
            let r = Async.RunSynchronously run
            result <- Some r
            activate r

        member x.Deactivate() = 
            if Interlocked.Decrement(&refCnt) = 0 then
                match result with
                    | None -> ()
                    | Some v -> deactivate v

        member x.Activate() = 
            if Interlocked.Increment(&refCnt) = 1 then
                match result with
                    | Some r -> activate r
                    | None -> failwith ""

        member x.Kill cont = result |> Option.iter cont

    type LoadResult = ref<Choice<CancellationTokenSource,GeometryRef>>

    type PointCloudHandler(node : Sg.PointCloud, view : IMod<Trafo3d>, proj : IMod<Trafo3d>, viewportSize : IMod<V2i>, runtime : IRuntime) =
        let cancel = new System.Threading.CancellationTokenSource()

        let pool = GeometryPool.createAsync runtime
        let calls = DrawCallSet(false)
        let inactive = ConcurrentHashQueue<GeometryRef>()
        let mutable inactiveSize = 0L
        let mutable activeSize = 0L
        let mutable activeCount = 0
        let mutable desiredCount = 0

        let mutable pendingRemoves = 0
        let geometries = System.Collections.Concurrent.ConcurrentDictionary<LodDataNode, LoadResult>()

        let activate (n : GeometryRef) =
            let size = n.range.Size

            if inactive.Remove n then
                Interlocked.Add(&inactiveSize, int64 -size) |> ignore

            if calls.Add n.range then
                Interlocked.Add(&activeSize, int64 size) |> ignore
                Interlocked.Increment(&activeCount) |> ignore


        let deactivate (n : GeometryRef) =
            let size = int64 n.range.Size

            if inactive.Enqueue n then
                Interlocked.Add(&inactiveSize, size) |> ignore

            if calls.Remove n.range then
                Interlocked.Add(&activeSize, -size) |> ignore
                Interlocked.Decrement(&activeCount) |> ignore


        member x.Add(n : LodDataNode) =
            Interlocked.Increment(&desiredCount) |> ignore
            let isNew = ref false

            let cts = new System.Threading.CancellationTokenSource()
            let uninitialized = Choice1Of2 cts
            let loadResult = ref uninitialized
            let result = geometries.GetOrAdd(n, System.Func<LodDataNode,LoadResult>(fun _ ->
                isNew := true
                loadResult
            ))
            let geometry = 
                try 
                    Some <| Async.RunSynchronously(LoadIntoPool.load node.Data n pool, cancellationToken = cts.Token)
                with 
                    | :? OperationCanceledException as e -> 
                        Log.line "op cancelled."
                        None
                        
            match geometry with
                | Some geometry ->
                    let validGeometry = Choice2Of2 geometry

                    let ch = Interlocked.Exchange(loadResult,validGeometry)
                    if (not <| Unchecked.equals ch Unchecked.defaultof<_>) && ch = uninitialized (*&& !isNew*) then
                        activate geometry
                    else Log.line "cancelled, not activating: %A" ch
                | _ -> ()


        member x.Remove(n : LodDataNode) =
            match geometries.TryRemove n with
                | (true,v) ->
                    Interlocked.Decrement(&desiredCount) |> ignore
                    match Interlocked.Exchange(v,Unchecked.defaultof<_>) with
                        | Choice1Of2 ct -> 
                            Log.line "cancelling import."
                            ct.Cancel()
                        | Choice2Of2 g -> 
                            deactivate g
                    true
                | _ -> 
                    Log.warn "failed to find node for removal: %A" n.id
                    false



        member x.Activate() =
                
            let wantedNearPlaneDistance =
                Mod.custom (fun self ->
                    let viewportSize = viewportSize.GetValue self
                    let wantedPixelDistance = node.Config.targetPointDistance.GetValue self

                    let size = max viewportSize.X viewportSize.Y
                    2.0 * float wantedPixelDistance / float size
                )

            let content =
                ASet.custom (fun self ->
                    let view = view.GetValue self
                    let proj = proj.GetValue self
                    let wantedNearPlaneDistance = wantedNearPlaneDistance.GetValue self

                    let set = node.Data.Rasterize(view, proj, wantedNearPlaneDistance)

                    let add = set |> Seq.filter (self.Content.Contains >> not) |> Seq.map Add
                    let rem = self.Content |> Seq.filter (set.Contains >> not) |> Seq.map Rem

                    let res = Seq.append add rem |> Seq.toList
                    res
                )


            let deltas = ConcurrentDeltaQueue.ofASet content

            let deltaProcessing =    
                async {
                    do! Async.SwitchToNewThread()
                    while true do
                        let op = deltas.Dequeue()

                        match op with
                            | Add n -> x.Add n |> ignore
                            | Rem n -> x.Remove n |> ignore
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
                        printfn "active = %A / desired = %A / count = %A / inactiveCnt=%d / inactive=%A" activeCount desiredCount geometries.Count inactive.Count inactiveSize
                        ()
                }


//            let t = 
//                [| for i in 0 .. 8 do 
//                    let t = System.Threading.Thread(ThreadStart(deltaProcessing))   // todo cancell
//                    t.Priority <- ThreadPriority.BelowNormal
//                    t.Start()
//                |]
            for i in 1..4 do
                Async.StartAsTask(deltaProcessing, cancellationToken = cancel.Token) |> ignore
            Async.StartAsTask(pruning, cancellationToken = cancel.Token) |> ignore
            Async.StartAsTask(printer, cancellationToken = cancel.Token) |> ignore

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

            let h = PointCloudHandler(l, view, proj, viewportSize, l.Runtime)

            let calls = h.DrawCallInfos

            obj.IndirectBuffer <- calls |> Mod.map (fun a -> ArrayBuffer(a) :> IBuffer)
            obj.Activate <- h.Activate
            obj.VertexAttributes <- h.Attributes
            obj.Mode <- Mod.constant IndexedGeometryMode.PointList

            ASet.single (obj :> IRenderObject)

