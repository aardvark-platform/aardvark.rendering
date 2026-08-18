namespace Aardvark.SceneGraph

open Aardvark.Base
open Aardvark.Base.Geometry
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph

[<AutoOpen>]
module ``Sg Picking Extensions`` =
    open Aardvark.SceneGraph.Semantics

    type PickShape =
        | Box           of Box3d
        | Sphere        of Sphere3d
        | Cylinder      of Cylinder3d
        | Triangle      of Triangle3d
        | Triangles     of KdTree<Triangle3d>
        | TriangleArray of Triangle3d[]
        | Custom        of Box3d * (RayPart -> float option)

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PickShape =
        let bounds (p : PickShape) =
            match p with
            | Box b            -> b
            | Sphere s         -> s.BoundingBox3d
            | Cylinder c       -> c.BoundingBox3d
            | Triangle t       -> t.BoundingBox3d
            | Triangles b      -> b.Bounds
            | TriangleArray ts -> Box3d(ts |> Array.map _.BoundingBox3d)
            | Custom(b, _)     -> b

    type Pickable = { trafo : Trafo3d; shape : PickShape }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Pickable =

        let private transformBounds (trafo : Trafo3d) (bounds : Box3d) =
            if bounds.IsInvalid then
                Box3d.Invalid
            else
                bounds.ComputeCorners() 
                |> Array.map trafo.Forward.TransformPosProj
                |> Box3d

        let private transformRay (trafo : Trafo3d) (ray : Ray3d) =
            let o = trafo.Forward.TransformPosProj ray.Origin
            let d = trafo.Forward.TransformPosProj (ray.Origin + ray.Direction) - o
            Ray3d(o, d)

        let ofShape (shape : PickShape) =
            { trafo = Trafo3d.Identity; shape = shape }

        let transform (t : Trafo3d) (p : Pickable) =
            { p with trafo = p.trafo * t }

        let bounds (p : Pickable) =
            PickShape.bounds(p.shape) |> transformBounds p.trafo

        let private intersectTriangle (part : RayPart) (tri : Triangle3d) =
            match RayPart.Intersects(part, tri) with
            | Some t -> Some (RayHit(t, ()))
            | _ -> None

        let intersect (part : RayPart) (p : Pickable) =
            let local = RayPart(part.Ray.Ray |> transformRay p.trafo.Inverse |> FastRay3d, part.TMin, part.TMax)

            let inline getRealT (localT : float option) =
                match localT with
                | Some localT ->
                    let localPoint = local.Ray.Ray.GetPointOnRay localT
                    let worldPoint = p.trafo.Forward.TransformPosProj localPoint
                    let real = Vec.dot (worldPoint - part.Ray.Ray.Origin) part.Ray.Ray.Direction / Vec.lengthSquared part.Ray.Ray.Direction
                    Some real
                | _ ->
                    None

            match p.shape with
            | Box b -> RayPart.Intersects(local, b) |> getRealT
            | Sphere s -> RayPart.Intersects(local, s) |> getRealT
            | Cylinder c -> RayPart.Intersects(local, c) |> getRealT
            | Triangle t -> RayPart.Intersects(local, t) |> getRealT
            | Triangles kdtree -> KdTree.intersect intersectTriangle local kdtree |> Option.map _.T |> getRealT
            | TriangleArray arr ->
                let hits = arr |> Array.chooseV (fun t -> RayPart.IntersectsV(local, t))
                if hits.Length > 0 then
                    hits |> Array.min |> Some |> getRealT
                else
                    None
            | Custom(_, intersect) -> intersect local |> getRealT

        let private intersectTriangleV (part : RayPart) (tri : Triangle3d) =
            match RayPart.IntersectsV(part, tri) with
            | ValueSome t -> ValueSome (RayHit(t, ()))
            | _ -> ValueNone

        let intersectV (part : RayPart) (p : Pickable) =
            let local = RayPart(part.Ray.Ray |> transformRay p.trafo.Inverse |> FastRay3d, part.TMin, part.TMax)

            let inline getRealT (localT : float voption) =
                match localT with
                | ValueSome localT ->
                    let localPoint = local.Ray.Ray.GetPointOnRay localT
                    let worldPoint = p.trafo.Forward.TransformPosProj localPoint
                    let real = Vec.dot (worldPoint - part.Ray.Ray.Origin) part.Ray.Ray.Direction / Vec.lengthSquared part.Ray.Ray.Direction
                    ValueSome real
                | _ ->
                    ValueNone

            match p.shape with
            | Box b -> RayPart.IntersectsV(local, b) |> getRealT
            | Sphere s -> RayPart.IntersectsV(local, s) |> getRealT
            | Cylinder c -> RayPart.IntersectsV(local, c) |> getRealT
            | Triangle t -> RayPart.IntersectsV(local, t) |> getRealT
            | Triangles kdtree -> KdTree.intersectV intersectTriangleV local kdtree |> ValueOption.map _.T |> getRealT
            | TriangleArray arr ->
                let hits = arr |> Array.chooseV (fun t -> RayPart.IntersectsV(local, t))
                if hits.Length > 0 then
                    hits |> Array.min |> ValueSome |> getRealT
                else
                    ValueNone
            | Custom(_, intersect) -> intersect local |> Option.toValueOption |> getRealT

    type PickObject(scope : Scope, pickable : aval<Pickable>) =
        member x.Scope = scope
        member x.Pickable = pickable

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PickObject =
        let bounds (p : PickObject) =
            p.Pickable |> AVal.map Pickable.bounds

    type PickTree(objects : aset<PickObject>) =
        let objects =
            objects |> ASet.filterA (fun o ->
                PickObject.bounds o |> AVal.map _.IsValid
            )

        let bvh = 
            BvhTree.ofASet (PickObject.bounds >> AVal.map _.EnlargedBy(1E-8)) objects

        static let intersectLeaf (part : RayPart) (p : PickObject) =
            let pickable = p.Pickable |> AVal.force
            match Pickable.intersect part pickable with
            | Some t ->
                let pt = part.Ray.Ray.GetPointOnRay t
                Some <| RayHit(t, (p, pt))
            | _ ->
                None

        static let intersectLeafV (part : RayPart) (p : PickObject) =
            let pickable = p.Pickable |> AVal.force
            match Pickable.intersectV part pickable with
            | ValueSome t ->
                let pt = part.Ray.Ray.GetPointOnRay t
                ValueSome (RayHit(t, struct (p, pt)))
            | _ ->
                ValueNone

        member x.Update() =
            bvh.GetValue() |> ignore

        member x.Intersect(ray : Ray3d, tmin : float, tmax : float) =
            bvh |> AVal.map _.Intersect(intersectLeaf, RayPart(FastRay3d(ray), tmin, tmax))

        member x.Intersect(ray : Ray3d) =
            bvh |> AVal.map _.Intersect(intersectLeaf, RayPart(FastRay3d(ray), 0.0, infinity))

        member x.IntersectV(ray : Ray3d, tmin : float, tmax : float) =
            bvh |> AVal.map _.IntersectV(intersectLeafV, RayPart(FastRay3d(ray), tmin, tmax))

        member x.IntersectV(ray : Ray3d) =
            bvh |> AVal.map _.IntersectV(intersectLeafV, RayPart(FastRay3d(ray), 0.0, infinity))

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PickTree =
        let ofPickObjects (objects : aset<PickObject>) =
            let res = PickTree(objects)
            res.Update()
            res

        let ofSg (sg : ISg) =
            sg?PickObjects(Scope.Root) |> ofPickObjects

        let intersectFull (ray : Ray3d) (tmin : float) (tmax : float) (t : PickTree) =
            t.Intersect(ray, tmin, tmax)

        let intersectFullV (ray : Ray3d) (tmin : float) (tmax : float) (t : PickTree) =
            t.IntersectV(ray, tmin, tmax)

        let intersect (ray : Ray3d) (t : PickTree) =
            t.Intersect(ray)

        let intersectV (ray : Ray3d) (t : PickTree) =
            t.IntersectV(ray)

    module Sg =
        type PickableApplicator(pickable : aval<Pickable>, child : aval<ISg>) =
            inherit Sg.AbstractApplicator(child)

            member x.Pickable = pickable

        type RequirePickingApplicator(child : aval<ISg>) =
            inherit Sg.AbstractApplicator(child)

        let pickable (shape : PickShape) (sg : ISg) =
            PickableApplicator(AVal.constant (Pickable.ofShape shape), AVal.constant sg) :> ISg

        let pickBoundingBox (sg : ISg) =
            let pickable = sg.LocalBoundingBox(Scope.Root) |> AVal.map (PickShape.Box >> Pickable.ofShape)
            PickableApplicator(pickable, AVal.constant sg) :> ISg

        let requirePicking (sg : ISg) =
            RequirePickingApplicator(AVal.constant sg) :> ISg

namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Geometry
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.Rendering
open Aardvark.SceneGraph
open System.Collections.Generic

[<AutoOpen>]
module PickingSemantics =

    type ISg with
        member x.PickObjects(scope : Scope) : aset<PickObject> = x?PickObjects(scope)

    type Scope with
        member x.RequirePicking : bool = x?RequirePicking

    type private PickingKey =
        {
            index     : BufferView option
            positions : BufferView voption
            call      : aval<DrawCallInfo>
            mode      : IndexedGeometryMode
        }

    [<Struct>]
    type private VertexRange =
        {
            first     : int
            count     : int
            indexBase : int
        }

    [<Rule>]
    type PickObjectSem() =

        static let cache = Dictionary<PickingKey, aval<Pickable> voption>()

        static let triangleSpatial =
            { new Spatial<Triangle3d>() with
                member _.ComputeBounds(ps) = Spatial.triangle.ComputeBounds(ps).EnlargedBy 1E-8
                member _.PlaneSide(a, b) = Spatial.triangle.PlaneSide(a, b)
            }

        static let createPickable (triangles : Triangle3d[]) =
            triangles
            |> KdTree.build triangleSpatial KdBuildInfo.Default
            |> PickShape.Triangles
            |> Pickable.ofShape

        static let resolveCall (scopeCount : int) (call : DrawCallInfo) =
            if call.FaceVertexCount < 0 then
                let mutable result = call
                result.FaceVertexCount <- scopeCount
                result.BaseVertex <- 0
                result
            else
                call

        static let getDrawRange (call : DrawCallInfo) =
            if call.FaceVertexCount <= 0 then
                ValueNone
            else
                let last = int64 call.FirstIndex + int64 call.FaceVertexCount - 1L
                if call.FirstIndex < 0 || last > int64 System.Int32.MaxValue then
                    raise <| System.ArgumentOutOfRangeException(
                        "call",
                        $"Draw range [{call.FirstIndex}, {last}] cannot be represented."
                    )

                ValueSome struct (call.FirstIndex, call.FaceVertexCount)

        static let getVertexRange (baseVertex : int) (index : int[]) =
            if index.Length = 0 then
                ValueNone
            else
                let mutable minIndex = index.[0]
                let mutable maxIndex = minIndex

                for i = 1 to index.Length - 1 do
                    let value = index.[i]
                    if value < minIndex then minIndex <- value
                    if value > maxIndex then maxIndex <- value

                let first = int64 baseVertex + int64 minIndex
                let last = int64 baseVertex + int64 maxIndex
                let count = last - first + 1L

                if first < 0L || last > int64 System.Int32.MaxValue || count > int64 System.Int32.MaxValue then
                    raise <| System.ArgumentOutOfRangeException(
                        "call",
                        $"Indexed vertex range [{first}, {last}] cannot be represented."
                    )

                ValueSome {
                    first = int first
                    count = int count
                    indexBase = minIndex
                }

        static let downloadRange (token : AdaptiveToken) (startIndex : int) (count : int) (view : BufferView) =
            match view.SingleValue with
            | Some value ->
                value.Accept {
                    new IAdaptiveValueVisitor<System.Array> with
                        member _.Visit(value) = Array.replicate count (value.GetValue token) :> System.Array
                }
            | None ->
                let elementSize = view.ElementType.CLRSize
                let elementStride = if view.Stride = 0 then elementSize else view.Stride
                let offset = int64 view.Offset + int64 elementStride * int64 startIndex

                if startIndex < 0 || count < 0 || view.Offset < 0 || elementStride <= 0 || offset < 0L then
                    raise <| System.ArgumentOutOfRangeException(
                        "view",
                        $"Buffer view range (start = {startIndex}, count = {count}) cannot be represented."
                    )

                view.Buffer.GetValue(token).ToArray(
                    view.ElementType,
                    uint64 count,
                    uint64 offset,
                    uint64 view.Stride
                )

        static let getTriangles (mode : IndexedGeometryMode) (index : int[]) (indexBase : int) (pos : V3d[]) : Triangle3d[] =
            let inline at (i : int) =
                pos.[int (int64 index.[i] - int64 indexBase)]

            match mode, index with
            | IndexedGeometryMode.TriangleList, null ->
                let get (ti : int) =
                    let i0 = 3 * ti
                    Triangle3d(pos.[i0], pos.[i0 + 1], pos.[i0 + 2])

                Array.init (pos.Length / 3) get

            | IndexedGeometryMode.TriangleList, index ->
                let get (ti : int) =
                    let i0 = 3 * ti
                    Triangle3d(at i0, at (i0 + 1), at (i0 + 2))

                Array.init (index.Length / 3) get

            | IndexedGeometryMode.TriangleStrip, null ->
                let get (ti : int) =
                    if ti % 2 = 0 then
                        Triangle3d(pos.[ti], pos.[ti + 1], pos.[ti + 2])
                    else
                        Triangle3d(pos.[ti + 1], pos.[ti], pos.[ti + 2])

                Array.init (max 0 (pos.Length - 2)) get

            | IndexedGeometryMode.TriangleStrip, index ->
                let get (ti : int) =
                    if ti % 2 = 0 then
                        Triangle3d(at ti, at (ti + 1), at (ti + 2))
                    else
                        Triangle3d(at (ti + 1), at ti, at (ti + 2))

                Array.init (max 0 (index.Length - 2)) get

            | IndexedGeometryMode.TriangleAdjacencyList, null ->
                let get (ti : int) =
                    let i0 = 6 * ti
                    Triangle3d(pos.[i0], pos.[i0 + 2], pos.[i0 + 4])

                Array.init (pos.Length / 6) get

            | IndexedGeometryMode.TriangleAdjacencyList, index ->
                let get (ti : int) =
                    let i0 = 6 * ti
                    Triangle3d(at i0, at (i0 + 2), at (i0 + 4))

                Array.init (index.Length / 6) get

            | _ ->
                Array.empty

        // TODO: memory leak
        static let createLeafPickable (scopeCount : aval<int>) (key : PickingKey) =
            lock cache (fun () ->
                cache.GetCreate(key, fun key ->
                    match key.mode with
                    | IndexedGeometryMode.TriangleList
                    | IndexedGeometryMode.TriangleStrip
                    | IndexedGeometryMode.TriangleAdjacencyList ->
                        match key.positions with
                        | ValueSome positionView ->
                            let positionConverter = PrimitiveValueConverter.getArrayConverter positionView.ElementType typeof<V3d>

                            let pickable =
                                match key.index with
                                | Some indexView ->
                                    let indexConverter = PrimitiveValueConverter.getArrayConverter indexView.ElementType typeof<int>

                                    AVal.custom (fun token ->
                                        let call =
                                            let call = key.call.GetValue token
                                            if call.FaceVertexCount < 0 then
                                                resolveCall (scopeCount.GetValue token) call
                                            else
                                                call

                                        let triangles =
                                            match getDrawRange call with
                                            | ValueSome struct (first, count) ->
                                                let index =
                                                    downloadRange token first count indexView
                                                    |> indexConverter
                                                    |> unbox<int[]>

                                                match getVertexRange call.BaseVertex index with
                                                | ValueSome range ->
                                                    let positions =
                                                        downloadRange token range.first range.count positionView
                                                        |> positionConverter
                                                        |> unbox<V3d[]>

                                                    getTriangles key.mode index range.indexBase positions
                                                | ValueNone ->
                                                    Array.empty
                                            | ValueNone ->
                                                Array.empty

                                        createPickable triangles
                                    )

                                | None ->
                                    AVal.custom (fun token ->
                                        let call =
                                            let call = key.call.GetValue token
                                            if call.FaceVertexCount < 0 then
                                                resolveCall (scopeCount.GetValue token) call
                                            else
                                                call

                                        let triangles =
                                            match getDrawRange call with
                                            | ValueSome struct (first, count) ->
                                                let positions =
                                                    downloadRange token first count positionView
                                                    |> positionConverter
                                                    |> unbox<V3d[]>

                                                getTriangles key.mode null 0 positions
                                            | ValueNone ->
                                                Array.empty

                                        createPickable triangles
                                    )

                            ValueSome pickable
                        | ValueNone ->
                            ValueNone
                    | _ ->
                        Log.warn "[Pickable] Cannot get triangles for RenderNode (Mode = %A)" key.mode
                        ValueNone
                )
            )

        member x.RequirePicking(r : Root<ISg>, _ : Scope) =
            r.Child?RequirePicking <- false

        member x.RequirePicking(a : Sg.RequirePickingApplicator, _ : Scope) =
            a.Child?RequirePicking <- true

        member x.PickObjects(render : Sg.RenderNode, scope : Scope) : aset<PickObject> =
            if scope.RequirePicking then
                let key =
                    {
                        positions = scope.VertexAttributes |> Map.tryFindV DefaultSemantic.Positions
                        index     = scope.VertexIndexBuffer
                        call      = render.DrawCallInfo
                        mode      = render.Mode
                    }

                match createLeafPickable scope.FaceVertexCount key with
                | ValueSome pickable ->
                    let pickable = AVal.map2 Pickable.transform scope.ModelTrafo pickable
                    let o = PickObject(scope, pickable)
                    ASet.single o

                | ValueNone ->
                    ASet.empty
            else
                ASet.empty

        member x.PickObjects(app : IApplicator, scope : Scope) : aset<PickObject> =
            aset {
                let! c = app.Child
                yield! c.PickObjects(scope)
            }

        member x.PickObjects(set : IGroup, scope : Scope) : aset<PickObject> =
            aset {
                for c in set.Children do
                    yield! c.PickObjects(scope)
            }

        member x.PickObjects(_ : ISg, _ : Scope) : aset<PickObject> =
            ASet.empty

        member x.PickObjects(pickable : Sg.PickableApplicator, scope : Scope) : aset<PickObject> =
            let pickable = AVal.map2 Pickable.transform scope.ModelTrafo pickable.Pickable
            let o = PickObject(scope, pickable)
            ASet.single o

        member x.PickObjects(node : Sg.RuntimeCommandNode, scope : Scope) : aset<PickObject> =
            let rec processCommand (cmd : RenderCommand) =
                aset {
                    match cmd with
                    | RenderCommand.REmpty | RenderCommand.RClear _ -> ()
                    | RenderCommand.RUnorderedScenes set ->
                        for sg in set do
                            yield! sg.PickObjects(scope)

                    | RenderCommand.ROrdered cmds ->
                        for cmd in cmds do
                            yield! processCommand cmd

                    | RenderCommand.RIfThenElse (condition, ifTrue, ifFalse) ->
                        let! c = condition
                        yield! processCommand (if c then ifTrue else ifFalse)

                    | RenderCommand.RGeometries _ ->
                        ()

                    | RenderCommand.RLodTree _ ->
                        ()
                }

            processCommand node.Command