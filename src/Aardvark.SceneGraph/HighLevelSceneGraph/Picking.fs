namespace Aardvark.SceneGraph

open System
open Aardvark.Base
open Aardvark.Base.Geometry
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
        | Triangle of Triangle3d
        | Triangles of KdTree<Triangle3d>
        | TriangleArray of Triangle3d[]
        | Custom of Box3d * (RayPart -> Option<float>)

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PickShape =
        let bounds (p : PickShape) =
            match p with
                | Box b -> b
                | Sphere s -> s.BoundingBox3d
                | Cylinder c -> c.BoundingBox3d
                | Triangle t -> t.BoundingBox3d
                | Triangles b -> b.Bounds
                | TriangleArray ts -> Box3d(ts |> Array.map ( fun t -> t.BoundingBox3d ))
                | Custom(b,_) -> b

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
            match RayPart.Intersects(part, tri) with
                | Some t -> Some (RayHit(t, ()))
                | None -> None

        let intersect (part : RayPart) (p : Pickable) =
            let part = RayPart(part.Ray.Ray.Transformed(p.trafo.Backward) |> FastRay3d, part.TMin, part.TMax)

            match p.shape with
                | Box b -> RayPart.Intersects(part, b)
                | Sphere s -> RayPart.Intersects(part, s)
                | Cylinder c -> RayPart.Intersects(part, c)
                | Triangle t -> RayPart.Intersects(part, t)
                | Triangles kdtree ->
                    match KdTree.intersect intersectTriangle part kdtree with
                        | Some hit -> Some hit.T
                        | None -> None
                | TriangleArray arr -> 
                    match arr |> Array.choose ( fun t -> RayPart.Intersects(part, t) ) with
                    | [||] -> None
                    | ts -> ts |> Array.min |> Some
                | Custom(_,intersect) -> intersect part 

    type PickObject(scope : Ag.Scope, pickable : IMod<Pickable>) =
        member x.Scope = scope
        member x.Pickable = pickable
        
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PickObject =
        let bounds (p : PickObject) =
            p.Pickable |> Mod.map Pickable.bounds

    type PickTree(objects : aset<PickObject>) =
        let bvh = 
            BvhTree.ofASet (fun a -> PickObject.bounds a |> Mod.map ( fun b -> b.EnlargedBy(1E-8))) objects

        static let intersectLeaf (part : RayPart) (p : PickObject) =
            let pickable = p.Pickable |> Mod.force
            match Pickable.intersect part pickable with
                | Some t -> 
                    let pt = part.Ray.Ray.GetPointOnRay t
                    Some (RayHit(t, (p, pt)))
                | None -> None
                
        member x.Dispose() =
            bvh.Dispose()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        member x.Update() =
            bvh.GetValue() |> ignore

        member x.Intersect(ray : Ray3d, tmin : float, tmax : float) =
            bvh |> Mod.map (fun bvh ->
                bvh.Intersect(intersectLeaf, RayPart(FastRay3d(ray), tmin, tmax))
            )

        member x.Intersect(ray : Ray3d) =
            bvh |> Mod.map (fun bvh ->
                bvh.Intersect(intersectLeaf, RayPart(FastRay3d(ray), 0.0, System.Double.PositiveInfinity))
            )

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PickTree =
        let ofPickObjects (objects : aset<PickObject>) =
            let res = new PickTree(objects)
            res.Update()
            res

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
            
        type RequirePickingApplicator(child : IMod<ISg>) =
            inherit Sg.AbstractApplicator(child)


        let pickable (shape : PickShape) (sg : ISg) =
            PickableApplicator(Mod.constant (Pickable.ofShape shape), Mod.constant sg) :> ISg
            
        let pickBoundingBox (sg : ISg) =
            let pickable = sg.LocalBoundingBox() |> Mod.map (PickShape.Box >> Pickable.ofShape)
            PickableApplicator(pickable, Mod.constant sg) :> ISg

        let requirePicking (sg : ISg) =
            RequirePickingApplicator(Mod.constant sg) :> ISg



namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.SceneGraph

[<AutoOpen>]
module PickingSemantics =

    type ISg with
        member x.PickObjects() : aset<PickObject> = x?PickObjects()
        member x.RequirePicking : bool = x?RequirePicking

    type private PickingKey =
        {
            index : Option<BufferView>
            positions : Option<BufferView>
            call : IMod<DrawCallInfo>
            mode : IndexedGeometryMode
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

        static let getTriangles (mode : IndexedGeometryMode) (index : int[]) (pos : V3d[]) : array<Triangle3d> =
            match mode, index with
                | IndexedGeometryMode.TriangleList, null ->
                    let get (ti : int) =
                        let i0 = 3 * ti
                        Triangle3d(pos.[i0], pos.[i0 + 1], pos.[i0 + 2])

                    Array.init (pos.Length / 3) get

                | IndexedGeometryMode.TriangleList, index ->
                    let get (ti : int) =
                        let i0 = 3 * ti
                        Triangle3d(pos.[index.[i0]], pos.[index.[i0 + 1]], pos.[index.[i0 + 2]])

                    Array.init (index.Length / 3) get

                | IndexedGeometryMode.TriangleStrip, null ->
                    let get (ti : int) =
                        if ti % 2 = 0 then
                            Triangle3d(pos.[ti], pos.[ti + 1], pos.[ti + 2])
                        else
                            Triangle3d(pos.[ti + 1], pos.[ti], pos.[ti + 2])
                            
                    Array.init (pos.Length - 2) get
                         
                | IndexedGeometryMode.TriangleStrip, index ->
                    let get (ti : int) =
                        if ti % 2 = 0 then
                            Triangle3d(pos.[index.[ti]], pos.[index.[ti + 1]], pos.[index.[ti + 2]])
                        else
                            Triangle3d(pos.[index.[ti + 1]], pos.[index.[ti]], pos.[index.[ti + 2]])
                            
                    Array.init (index.Length - 2) get   

                | IndexedGeometryMode.TriangleAdjacencyList, null ->
                    let get (ti : int) =
                        let i0 = 6 * ti
                        Triangle3d(pos.[i0], pos.[i0 + 2], pos.[i0 + 4])

                    Array.init (pos.Length / 6) get
                    
                | IndexedGeometryMode.TriangleAdjacencyList, index ->
                    let get (ti : int) =
                        let i0 = 6 * ti
                        Triangle3d(pos.[index.[i0]], pos.[index.[i0 + 2]], pos.[index.[i0 + 4]])

                    Array.init (index.Length / 6) get

                | _ ->
                    failwithf "[Pickable] cannot get triangles for RenderNode (Mode = %A)" mode

        // TODO: memory leak
        static let createLeafPickable (key : PickingKey) =
            lock cache (fun () ->
                cache.GetOrCreate(key, fun key ->
                    let mode = key.mode
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
                                    let maxVertexExclusice =
                                        match index with
                                            | Some idx -> 
                                                idx |> Mod.map (fun idx ->
                                                    1 + Array.max idx
                                                )
                                            | None -> 
                                                call |> Mod.map (fun call -> 
                                                    call.FirstIndex + call.FaceVertexCount
                                                )
                                    let converter = PrimitiveValueConverter.getArrayConverter view.ElementType typeof<V3d>

                                    maxVertexExclusice 
                                        |> Mod.bind (fun cnt -> BufferView.download 0 cnt view)
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

           
                                let pickable = 
                                    let spatial =
                                        { new Spatial<Triangle3d>() with
                                            member x.ComputeBounds(ps) = Spatial.triangle.ComputeBounds(ps).EnlargedBy 1E-8
                                            member x.PlaneSide(a,b) = Spatial.triangle.PlaneSide(a,b)
                                        }

                                    triangles |> Mod.map ( 
                                        KdTree.build spatial KdBuildInfo.Default >> 
                                        PickShape.Triangles >>
                                        Pickable.ofShape
                                    )

                                Some pickable
                            | None ->
                                None
                    else
                        None
                )
            )

        member x.RequirePicking(r : Root<ISg>) =
            r.Child?RequirePicking <- false

        member x.RequirePicking(a : Sg.RequirePickingApplicator) =
            a.Child?RequirePicking <- true

        member x.PickObjects(render : Sg.RenderNode) : aset<PickObject> =
            if render.RequirePicking then
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