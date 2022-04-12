namespace Aardvark.SceneGraph

open System
open Aardvark.Base
open Aardvark.Base.Geometry
open FSharp.Data.Adaptive
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

        let private transformBounds (trafo : Trafo3d) (bounds : Box3d) =
            if bounds.IsInvalid then
                Box3d.Invalid
            else
                bounds.ComputeCorners() 
                |> Array.map (fun p -> trafo.Forward.TransformPosProj p)
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
                | None -> None

        let intersect (part : RayPart) (p : Pickable) =
            let local = RayPart(part.Ray.Ray |> transformRay p.trafo.Inverse |> FastRay3d, part.TMin, part.TMax)

            let inline getRealT (localT : Option<float>) =
                match localT with
                | Some localT ->
                    let localPoint = local.Ray.Ray.GetPointOnRay localT
                    let worldPoint = p.trafo.Forward.TransformPosProj localPoint
                    let real = Vec.dot (worldPoint - part.Ray.Ray.Origin) part.Ray.Ray.Direction / Vec.lengthSquared part.Ray.Ray.Direction
                    Some real
                | None ->
                    None

            match p.shape with
                | Box b -> RayPart.Intersects(local, b) |> getRealT
                | Sphere s -> RayPart.Intersects(local, s) |> getRealT
                | Cylinder c -> RayPart.Intersects(local, c) |> getRealT
                | Triangle t -> RayPart.Intersects(local, t) |> getRealT
                | Triangles kdtree ->
                    match KdTree.intersect intersectTriangle local kdtree with
                    | Some hit -> Some hit.T |> getRealT
                    | None -> None
                | TriangleArray arr -> 
                    match arr |> Array.choose ( fun t -> RayPart.Intersects(local, t) ) with
                    | [||] -> None
                    | ts -> ts |> Array.min |> Some |> getRealT
                | Custom(_,intersect) -> intersect local |> getRealT

    type PickObject(scope : Ag.Scope, pickable : aval<Pickable>) =
        member x.Scope = scope
        member x.Pickable = pickable
        
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PickObject =
        let bounds (p : PickObject) =
            p.Pickable |> AVal.map Pickable.bounds

    type PickTree(objects : aset<PickObject>) =
        
        let objects =
            objects |> ASet.filterA (fun o ->
                PickObject.bounds o |> AVal.map (fun b -> b.IsValid)
            )

        //let objects =
        //    objects |> ASet.chooseM (fun o ->
        //        PickObject.bounds o |> AVal.map (fun b ->
        //            if b.IsInvalid then None
        //            else Some (b.EnlargedBy(1E-8), o)
        //        )
        //    )

        let bvh = 
            BvhTree.ofASet (fun a -> PickObject.bounds a |> AVal.map ( fun b -> b.EnlargedBy(1E-8))) objects

        static let intersectLeaf (part : RayPart) (p : PickObject) =
            let pickable = p.Pickable |> AVal.force
            match Pickable.intersect part pickable with
                | Some t -> 
                    let pt = part.Ray.Ray.GetPointOnRay t
                    Some (RayHit(t, (p, pt)))
                | None -> 
                    None
                

        member x.Update() =
            bvh.GetValue() |> ignore

        member x.Intersect(ray : Ray3d, tmin : float, tmax : float) =
            bvh |> AVal.map (fun bvh ->
                bvh.Intersect(intersectLeaf, RayPart(FastRay3d(ray), tmin, tmax))
            )

        member x.Intersect(ray : Ray3d) =
            bvh |> AVal.map (fun bvh ->
                bvh.Intersect(intersectLeaf, RayPart(FastRay3d(ray), 0.0, System.Double.PositiveInfinity))
            )

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PickTree =
        let ofPickObjects (objects : aset<PickObject>) =
            let res = new PickTree(objects)
            res.Update()
            res

        let ofSg (sg : ISg) =
            sg?PickObjects(Ag.Scope.Root) |> ofPickObjects

        let intersectFull (ray : Ray3d) (tmin : float) (tmax : float) (t : PickTree) =
            t.Intersect(ray, tmin, tmax)

        let intersect (ray : Ray3d) (t : PickTree) =
            t.Intersect(ray)

    module Sg =
        type PickableApplicator(pickable : aval<Pickable>, child : aval<ISg>) =
            inherit Sg.AbstractApplicator(child)

            member x.Pickable = pickable
            
        type RequirePickingApplicator(child : aval<ISg>) =
            inherit Sg.AbstractApplicator(child)


        let pickable (shape : PickShape) (sg : ISg) =
            PickableApplicator(AVal.constant (Pickable.ofShape shape), AVal.constant sg) :> ISg
            
        let pickBoundingBox (sg : ISg) =
            let pickable = sg.LocalBoundingBox(Ag.Scope.Root) |> AVal.map (PickShape.Box >> Pickable.ofShape)
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

[<AutoOpen>]
module PickingSemantics =

    type ISg with
        member x.PickObjects(scope : Ag.Scope) : aset<PickObject> = x?PickObjects(scope)

    type Ag.Scope with
        member x.RequirePicking : bool = x?RequirePicking

    type private PickingKey =
        {
            index : Option<BufferView>
            positions : Option<BufferView>
            call : aval<DrawCallInfo>
            mode : IndexedGeometryMode
        }

    [<Rule>]
    type PickObjectSem() =

        static let cache = Dict<PickingKey, Option<aval<Pickable>>>()

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
                                        |> AVal.bind (fun call -> BufferView.download call.FirstIndex call.FaceVertexCount view)
                                        |> AVal.map (converter >> unbox<int[]>)
                                        |> Some
                                | None ->
                                    None

                        let positions =
                            match key.positions with
                                | Some view ->
                                    let maxVertexExclusice =
                                        match index with
                                            | Some idx -> 
                                                idx |> AVal.map (fun idx ->
                                                    1 + Array.max idx
                                                )
                                            | None -> 
                                                call |> AVal.map (fun call -> 
                                                    call.FirstIndex + call.FaceVertexCount
                                                )
                                    let converter = PrimitiveValueConverter.getArrayConverter view.ElementType typeof<V3d>

                                    maxVertexExclusice 
                                        |> AVal.bind (fun cnt -> BufferView.download 0 cnt view)
                                        |> AVal.map (converter >> unbox<V3d[]>)
                                        |> Some

                                | None ->
                                    None

                        match positions with
                            | Some pos ->   
                                let triangles = 
                                    match index with
                                        | Some idx -> AVal.map2 (getTriangles mode) idx pos
                                        | None -> AVal.map (getTriangles mode null) pos

           
                                let pickable = 
                                    let spatial =
                                        { new Spatial<Triangle3d>() with
                                            member x.ComputeBounds(ps) = Spatial.triangle.ComputeBounds(ps).EnlargedBy 1E-8
                                            member x.PlaneSide(a,b) = Spatial.triangle.PlaneSide(a,b)
                                        }

                                    triangles |> AVal.map ( 
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

        member x.RequirePicking(r : Root<ISg>, scope : Ag.Scope) =
            r.Child?RequirePicking <- false

        member x.RequirePicking(a : Sg.RequirePickingApplicator, scope : Ag.Scope) =
            a.Child?RequirePicking <- true

        member x.PickObjects(render : Sg.RenderNode, scope : Ag.Scope) : aset<PickObject> =
            if scope.RequirePicking then
                let key =
                    {
                        positions = scope.VertexAttributes |> Map.tryFind DefaultSemantic.Positions
                        index = scope.VertexIndexBuffer
                        call = render.DrawCallInfo
                        mode = render.Mode
                    }

                match createLeafPickable key with
                    | Some pickable ->
                        let pickable = AVal.map2 Pickable.transform scope.ModelTrafo pickable
                        let o = PickObject(scope, pickable)
                        ASet.single o
                    | None ->
                        ASet.empty
                        
            else
                ASet.empty

        member x.PickObjects(app : IApplicator, scope : Ag.Scope) : aset<PickObject> =
            aset {
                let! c = app.Child
                yield! c.PickObjects(scope)
            }

        member x.PickObjects(set : IGroup, scope : Ag.Scope) : aset<PickObject> =
            aset {
                for c in set.Children do
                    yield! c.PickObjects(scope)
            }

        member x.PickObjects(s : ISg, scope : Ag.Scope) : aset<PickObject> =
            ASet.empty

        member x.PickObjects(pickable : Sg.PickableApplicator, scope : Ag.Scope) : aset<PickObject> =
            let pickable = AVal.map2 Pickable.transform scope.ModelTrafo pickable.Pickable
            let o = PickObject(scope, pickable)
            ASet.single o

        member x.PickObjects(node : Sg.RuntimeCommandNode, scope : Ag.Scope) : aset<PickObject> =
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

                    | RenderCommand.ROrderedConstant cmds ->
                        for cmd in cmds do
                            yield! processCommand cmd

                    | RenderCommand.RIfThenElse (condition, ifTrue, ifFalse) ->
                        let! c = condition
                        yield! processCommand (if c then ifTrue else ifFalse)

                    | RenderCommand.RGeometries (config, geometries) ->
                        ()

                    | RenderCommand.RLodTree (config, geometries) ->
                        ()
                }

            processCommand node.Command