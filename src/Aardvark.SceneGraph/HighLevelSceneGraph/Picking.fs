namespace Aardvark.SceneGraph

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.SceneGraph

[<AutoOpen>]
module ``Sg Picking Extensions`` =
    open Aardvark.SceneGraph.Semantics

    
    type PickShape =
        | Box of Box3d
        | Sphere of Sphere3d
        | Cylinder of Cylinder3d
        | Triangles of Bvh<Triangle3d>

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PickShape =
        let bounds (p : PickShape) =
            match p with
                | Box b -> b
                | Sphere s -> s.BoundingBox3d
                | Cylinder c -> c.BoundingBox3d
                | Triangles b -> b.Bounds

    type Pickable = { trafo : Trafo3d; shape : PickShape }
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Pickable =

        let ofShape (shape : PickShape) =
            { trafo = Trafo3d.Identity; shape = shape }

        let transform (t : Trafo3d) (p : Pickable) =
            { p with trafo = p.trafo * t }

        let bounds (p : Pickable) =
            PickShape.bounds(p.shape).Transformed(p.trafo)

        let private intersectTriangle (part : RayPart) (tri : Triangle3d) =
            let mutable t = 0.0
            if tri.Intersects(part.Ray.Ray, part.TMin, part.TMax, &t) then
                Some (RayHit(t, ()))
            else
                None

        let intersect (part : RayPart) (p : Pickable) =
            let ray = part.Ray.Ray.Transformed(p.trafo.Backward)
            let mutable tmin = part.TMin
            let mutable tmax = part.TMax
            //let innerRay = RayPart(FastRay3d ray, part.TMin, part.TMax)
            match p.shape with
                | Box b ->
                    let ray = FastRay3d ray
                    if ray.Intersects(b, &tmin, &tmax) then
                        Some tmin
                    else
                        None

                | Sphere s ->
                    //  | (o + t * d - c) |^2 = r^2
                    // let x = o - c
                    //  | (x + t * d) |^2 = r^2
                    //  <x + t*d | x + t*d> = r^2
                    //  <x|x>  + <t*d|x> + <x|t*d> + <t*d|t*d> = r^2
                    //  t^2*(<d|d>) + t*(2*<d|x>) + (<x|x> - r^2) = 0

                    let x = ray.Origin - s.Center
                    let d = ray.Direction
                    let a = d.LengthSquared
                    let b = 2.0 * Vec.dot d x
                    let c = x.LengthSquared - s.RadiusSquared

                    let s = b*b - 4.0*a*c
                    if s < 0.0 then
                        None
                    else
                        let s = sqrt s
                        let t1 = (-b + s) / (2.0 * a)
                        let t2 = (-b - s) / (2.0 * a)
                        let t = min t1 t2

                        if t >= tmin && t <= tmax then
                            Some t
                        else
                            None

                | Cylinder c ->
                    failwith "not implemented"

                | Triangles bvh ->
                    match bvh.Intersect(FastRay3d ray, part.TMin, part.TMax, intersectTriangle) with
                        | Some hit -> Some hit.T
                        | None -> None

    type PickObject(scope : Ag.Scope, pickable : IMod<Pickable>) =
        member x.Scope = scope
        member x.Pickable = pickable
        
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PickObject =
        let bounds (p : PickObject) =
            p.Pickable |> Mod.map Pickable.bounds

    type PickTree(objects : aset<PickObject>) =
        let bvh = Bvh.ofASet PickObject.bounds objects

        static let intersectLeaf (part : RayPart) (p : PickObject) =
            let pickable = p.Pickable |> Mod.force
            match Pickable.intersect part pickable with
                | Some t -> Some (RayHit(t, p))
                | None -> None
                
        member x.Dispose() =
            bvh.Dispose()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        member x.Intersect(ray : Ray3d, tmin : float, tmax : float) =
            bvh |> Mod.map (fun bvh ->
                Bvh.intersectFull intersectLeaf ray tmin tmax bvh
            )

        member x.Intersect(ray : Ray3d) =
            bvh |> Mod.map (fun bvh ->
                Bvh.intersectFull intersectLeaf ray 0.0 System.Double.PositiveInfinity bvh
            )

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PickTree =
        let ofPickObjects (objects : aset<PickObject>) =
            new PickTree(objects)
            
        let ofSg (sg : ISg) =
            sg?PickObjects() |> ofPickObjects

        let intersectFull (ray : Ray3d) (tmin : float) (tmax : float) (t : PickTree) =
            t.Intersect(ray, tmin, tmax)

        let intersect (ray : Ray3d) (t : PickTree) =
            t.Intersect(ray)

    module Sg =
        type PickableApplicator(pickable : IMod<Pickable>, child : IMod<ISg>) =
            inherit Sg.AbstractApplicator(child)

            member x.Pickable = pickable
            
        type RenderObjectPickableApplicator(child : IMod<ISg>) =
            inherit Sg.AbstractApplicator(child)


        let pickable (shape : PickShape) (sg : ISg) =
            PickableApplicator(Mod.constant (Pickable.ofShape shape), Mod.constant sg) :> ISg
            
        let pickBoundingBox (sg : ISg) =
            let pickable = sg.LocalBoundingBox() |> Mod.map (PickShape.Box >> Pickable.ofShape)
            PickableApplicator(pickable, Mod.constant sg) :> ISg

        let pickRenderObjects (sg : ISg) =
            RenderObjectPickableApplicator(Mod.constant sg) :> ISg



namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.SceneGraph

[<AutoOpen>]
module PickingSemantics =

    type ISg with
        member x.PickObjects() : aset<PickObject> = x?PickObjects()
        member x.IsRenderObjectPickable : bool = x?IsRenderObjectPickable

    type private PickingKey =
        {
            index : Option<BufferView>
            positions : Option<BufferView>
            call : IMod<DrawCallInfo>
            mode : IMod<IndexedGeometryMode>
        }

    [<Semantic>]
    type PickObjectSem() =

        static let cache = Dict<PickingKey, Option<IMod<Pickable>>>()

        static let bb (t : Triangle3d) =
            let mutable b = t.BoundingBox3d
            let size = b.Size
            let d = 1.0E-5 * size.NormMax

            if size.X <= 0.0 then
                b.Min.X <- b.Min.X - d
                b.Max.X <- b.Max.X + d

            if size.Y <= 0.0 then
                b.Min.Y <- b.Min.Y - d
                b.Max.Y <- b.Max.Y + d

            if size.Z <= 0.0 then
                b.Min.Z <- b.Min.Z - d
                b.Max.Z <- b.Max.Z + d

            b

        static let getTriangles (mode : IndexedGeometryMode) (index : int[]) (pos : V3d[]) : array<Triangle3d * Box3d> =
            if isNull index then
                let res = Array.zeroCreate (pos.Length / 3)
                for ti in 0 .. res.Length - 1 do
                    let i0 = 3 * ti
                    let i1 = i0 + 1
                    let i2 = i1 + 1

                    let tri = Triangle3d(pos.[i0], pos.[i1], pos.[i2])
                    let bb = bb tri
                    res.[ti] <- (tri, bb)
                res
            else
                let res = Array.zeroCreate (index.Length / 3)
                for ti in 0 .. res.Length - 1 do
                    let i0 = index.[3 * ti + 0]
                    let i1 = index.[3 * ti + 1]
                    let i2 = index.[3 * ti + 2]

                    let tri = Triangle3d(pos.[i0], pos.[i1], pos.[i2])
                    let bb = bb tri
                    res.[ti] <- (tri, bb)
                res

        // TODO: memory leak
        static let createLeafPickable (key : PickingKey) =
            lock cache (fun () ->
                cache.GetOrCreate(key, fun key ->
                    let mode = Mod.force key.mode
                    if mode = IndexedGeometryMode.TriangleList then
                        let call = key.call

                        let index =
                            match key.index with
                                | Some view ->
                                    let converter = PrimitiveValueConverter.getArrayConverter view.ElementType typeof<int>
                                    key.call 
                                        |> Mod.bind (fun call -> BufferView.download call.FirstIndex call.FaceVertexCount view)
                                        |> Mod.map (converter >> unbox<int[]>)
                                        |> Some
                                | None ->
                                    None

                        let positions =
                            match key.positions with
                                | Some view ->
                                    let range =
                                        match index with
                                            | Some idx -> 
                                                idx |> Mod.map (fun idx ->
                                                    let mutable l = System.Int32.MaxValue
                                                    let mutable h = System.Int32.MinValue

                                                    for i in idx do
                                                        l <- min l i
                                                        h <- max l h

                                                    (l, 1 + h - l)
                                                )
                                            | None -> 
                                                call |> Mod.map (fun call -> 
                                                    call.FirstIndex, call.FaceVertexCount
                                                )
                                    let converter = PrimitiveValueConverter.getArrayConverter view.ElementType typeof<V3d>

                                    range 
                                        |> Mod.bind (fun (min,cnt) -> BufferView.download min cnt view)
                                        |> Mod.map (converter >> unbox<V3d[]>)
                                        |> Some

                                | None ->
                                    None

                        match positions with
                            | Some pos ->   
                                let triangles = 
                                    match index with
                                        | Some idx -> Mod.map2 (getTriangles mode) idx pos
                                        | None -> Mod.map (getTriangles mode null) pos

                                let pickable = triangles |> Mod.map (fun t -> t |> Bvh.ofArray |> PickShape.Triangles |> Pickable.ofShape)
                                Some pickable
                            | None ->
                                None
                    else
                        None
                )
            )

        member x.IsRenderObjectPickable(r : Root<ISg>) =
            r.Child?IsRenderObjectPickable <- false

        member x.IsRenderObjectPickable(a : Sg.RenderObjectPickableApplicator) =
            a.Child?IsRenderObjectPickable <- true

        member x.PickObjects(render : Sg.RenderNode) : aset<PickObject> =
            if render.IsRenderObjectPickable then
                let key =
                    {
                        positions = render.VertexAttributes |> Map.tryFind DefaultSemantic.Positions
                        index = render.VertexIndexBuffer
                        call = render.DrawCallInfo
                        mode = render.Mode
                    }

                match createLeafPickable key with
                    | Some pickable ->
                        let ctx = Ag.getContext()
                        let pickable = Mod.map2 Pickable.transform x.ModelTrafo pickable
                        let o = PickObject(ctx, pickable)
                        ASet.single o
                    | None ->
                        ASet.empty
                        
            else
                ASet.empty

        member x.PickObjects(app : IApplicator) : aset<PickObject> =
            aset {
                let! c = app.Child
                yield! c.PickObjects()
            }

        member x.PickObjects(set : IGroup) : aset<PickObject> =
            aset {
                for c in set.Children do
                    yield! c.PickObjects()
            }

        member x.PickObjects(s : ISg) : aset<PickObject> =
            ASet.empty

        member x.PickObjects(pickable : Sg.PickableApplicator) : aset<PickObject> =
            let pickable = Mod.map2 Pickable.transform x.ModelTrafo pickable.Pickable
            let o = PickObject(Ag.getContext(), pickable)
            ASet.single o