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

        override x.GetHashCode() = x.node.GetHashCode()
        override x.Equals o =
            match o with
                | :? GeometryRef as o -> x.node.Equals(o.node)
                | _ -> false

    type PointCloudHandler(node : Sg.PointCloud, view : IMod<Trafo3d>, proj : IMod<Trafo3d>, viewportSize : IMod<V2i>) =
        let cancel = new System.Threading.CancellationTokenSource()

        let pool = GeometryPool.create()
        let calls = DrawCallSet(true)
        let inactive = ConcurrentHashQueue<GeometryRef>()
        let mutable inactiveSize = 0L
        let mutable activeSize = 0L

        let levelCounts = Array.zeroCreate 128


        let geometriesRW = new ReaderWriterLockSlim()
        let geometries = Dict<LodDataNode, GeometryRef>()

        let activate (n : GeometryRef) =
            let size = n.range.Size
            if inactive.Remove n then
                Interlocked.Add(&inactiveSize, int64 -size) |> ignore

            Interlocked.Add(&activeSize, int64 size) |> ignore
            levelCounts.[n.node.level] <- levelCounts.[n.node.level] + 1


            calls.Add n.range |> ignore

        let deactivate (n : GeometryRef) =
            calls.Remove n.range |> ignore
            inactive.Enqueue n |> ignore
            let size = int64 n.range.Size
            Interlocked.Add(&inactiveSize, size) |> ignore
            Interlocked.Add(&activeSize, -size) |> ignore
            levelCounts.[n.node.level] <- levelCounts.[n.node.level] - 1


        member x.Add(n : LodDataNode) =
            let result = 
                ReaderWriterLock.write geometriesRW (fun () ->
                    geometries.GetOrCreate(n, fun n ->
                        let g = node.Data.GetData n |> Async.RunSynchronously
                        let range = pool.Add(g)
                        { node = n; geometry = g; range = range }
                    )
                )

            activate result

            result

        member x.Remove(n : LodDataNode) =
            ReaderWriterLock.read geometriesRW (fun () ->
                match geometries.TryGetValue n with
                    | (true, t) -> deactivate t
                    | _ -> ()
            )


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
                    while true do
                        let! op = deltas.DequeueAsync()

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

                        while shouldContinue() do
                            match inactive.TryDequeue() with
                                | (true, v) ->
                                    ReaderWriterLock.write geometriesRW (fun () ->
                                        match geometries.TryRemove v.node with
                                            | (true, v) ->
                                                let r = pool.Remove v.geometry
                                                Interlocked.Add(&inactiveSize, int64 -v.range.Size) |> ignore
                                                cnt <- cnt + 1
                                            | _ ->
                                                Log.warn "failed to remove node: %A" v.node.id
                                    )

                                | _ ->
                                    ()
                            
                        do! Async.Sleep node.Config.pruneInterval
                }

            let defragmentation =
                async {
                    while true do
                        ()
                }

            Async.StartAsTask(deltaProcessing, cancellationToken = cancel.Token) |> ignore
            //Async.StartAsTask(printer, cancellationToken = cancel.Token) |> ignore
            Async.StartAsTask(pruning, cancellationToken = cancel.Token) |> ignore

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

            let h = PointCloudHandler(l, view, proj, viewportSize)

            let calls = h.DrawCallInfos

            obj.IndirectBuffer <- calls |> Mod.map (fun a -> ArrayBuffer(a) :> IBuffer)
            obj.IndirectCount <- calls |> Mod.map (fun a -> a.Length)
            obj.Activate <- h.Activate
            obj.VertexAttributes <- h.Attributes
            obj.Mode <- Mod.constant IndexedGeometryMode.PointList

            ASet.single (obj :> IRenderObject)

