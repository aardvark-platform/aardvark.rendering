namespace Aardvark.Base.Rendering

open System
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Incremental

[<AutoOpen>]
module private SimplePickTreeHelpers = 
    module Seq = 
        open System.Collections.Generic

        let rec private mergeSortedAux (cmp : 'a -> 'a -> int) (cl : Option<'a>) (cr : Option<'a>) (l : IEnumerator<'a>) (r : IEnumerator<'a>) =
            seq {
                let cl =
                    match cl with
                        | Some c -> Some c
                        | None -> 
                            if l.MoveNext() then Some l.Current
                            else None

                let cr = 
                    match cr with
                        | Some c -> Some c
                        | None -> 
                            if r.MoveNext() then Some r.Current
                            else None

                match cl, cr with
                    | None, None -> 
                        ()
                    | Some cl, None -> 
                        yield cl
                        yield! mergeSortedAux cmp None None l r

                    | None, Some cr ->
                        yield cr
                        yield! mergeSortedAux cmp None None l r

                    | Some vl, Some vr ->
                        let c = cmp vl vr
                        if c < 0 then
                            yield vl
                            yield! mergeSortedAux cmp None cr l r
                        else
                            yield vr
                            yield! mergeSortedAux cmp cl None l r

            }

        let mergeSorted (cmp : 'a -> 'a -> int) (l : seq<'a>) (r : seq<'a>) =
            seq {
                use le = l.GetEnumerator()
                use re = r.GetEnumerator()
                yield! mergeSortedAux cmp None None le re

            }

    let rec intersections (tryIntersect : RayPart -> 'a -> seq<RayHit<'r>>) (data : 'a[]) (part : RayPart) (node : BvhNode) =
        match node with
            | BvhNode.Leaf id ->
                tryIntersect part data.[id]

            | BvhNode.Node(lBox, rBox, left, right) ->
                let mutable lpart = part
                let mutable rpart = part

                let il = lpart.Ray.Intersects(lBox, &lpart.TMin, &lpart.TMax)
                let ir = lpart.Ray.Intersects(rBox, &rpart.TMin, &rpart.TMax)

                match il, ir with   
                    | false, false -> 
                        Seq.empty

                    | true, false ->
                        intersections tryIntersect data lpart left

                    | false, true ->
                        intersections tryIntersect data rpart right

                    | true, true ->
                        seq {

                            if lpart.TMax <= rpart.TMin then
                                // l strictly before r
                                yield! intersections tryIntersect data lpart left
                                yield! intersections tryIntersect data rpart right

                            elif rpart.TMax <= lpart.TMin then
                                // r strictly before l then
                                yield! intersections tryIntersect data rpart right
                                yield! intersections tryIntersect data lpart left

                            else
                                let li = intersections tryIntersect data lpart left
                                let ri = intersections tryIntersect data rpart right
                                yield! Seq.mergeSorted (fun (lh : RayHit<_>) (rh : RayHit<_>) -> compare lh.T rh.T) li ri
                        }
          
[<Struct>]
type SimplePickPoint =
    {
        DataPosition : V3d
        WorldPosition : V3d
    }

type SimplePickTree(  _bounds : Box3d,
                      _positions : V3f[],
                      _trafo : IMod<Trafo3d>,
                      _dataTrafo : Trafo3d,
                      _attributes : MapExt<Symbol, Array>,
                      _uniforms : MapExt<string, Array>,
                      _children : list<SimplePickTree>) =

    static let transform (trafo : Trafo3d) (part : RayPart) =
        let ray = part.Ray.Ray
        let np = trafo.Forward.TransformPos ray.Origin
        let nd = trafo.Forward.TransformDir ray.Direction
        let l = Vec.length nd
        let nr = FastRay3d(Ray3d(np,nd / l))

        let f = 1.0 / part.Ray.Ray.Direction.Length
        let ntmi = part.TMin * f
        let ntma = part.TMax * f

        RayPart(nr,ntmi,ntma)
        //let mutable newRay = part.Ray.Ray.Transformed(trafo.Forward)
        //let f = part.Ray.Ray.Direction.Length / newRay.Direction.Length
        //newRay.Direction <- newRay.Direction * f
        //RayPart(FastRay3d newRay, part.TMin, part.TMax)

    let _bvh = 
        lazy ( _children |> List.toArray |> Aardvark.Base.Geometry.BvhTree.create (fun c -> c.bounds) )

    member x.bounds = _bounds
    member x.positions = _positions
    member x.attributes = _attributes
    member x.uniforms = _uniforms
    member x.children = _children
    member x.bvh = _bvh.Value
    member x.trafo = _trafo
    member x.dataTrafo = _dataTrafo
            
    member private x.FindInternal(ray : RayPart, radiusD : float, radiusK : float) =
        if ray.Ray.Intersects(x.bounds, &ray.TMin, &ray.TMax) then
            let bvh = _bvh.Value
            match bvh.Root with
            | Some root -> 
                intersections (fun r (t : SimplePickTree) -> t.FindInternal(r, radiusD, radiusK)) bvh.Data ray root
            | None ->
                let hits = 
                    x.positions |> Array.choose ( fun p -> 
                        let p = x.dataTrafo.Forward.TransformPos (V3d p)
                        let o = p - ray.Ray.Ray.Origin
                        let t = Vec.dot o ray.Ray.Ray.Direction
                        if t >= ray.TMin && t <= ray.TMax then
                            let dist = Vec.cross o ray.Ray.Ray.Direction |> Vec.lengthSquared
                            let r = radiusD + t * radiusK
                            if dist < r*r then
                                Some (RayHit(t, { DataPosition = p; WorldPosition = x.dataTrafo.Backward.TransformPos p }))
                            else
                                None
                        else
                            None
                    )
                hits |> Seq.sortBy (fun h -> h.T)

        else
            Seq.empty


    member x.FindPoints(ray : Ray3d,  tMin : float, tMax : float, d : float, k : float) =
        let len = ray.Direction.Length
        let tMin = tMin * len
        let tMax = tMax * len
        let rp = RayPart(FastRay3d(Ray3d(ray.Origin, ray.Direction / len)),tMin,tMax)
        let t = x.trafo.GetValue()
        let rp = transform (t.Inverse * x.dataTrafo) rp
        x.FindInternal(rp, d, k) |> Seq.map (fun hit -> RayHit(hit.T, { hit.Value with WorldPosition = V3d hit.Value.WorldPosition |> t.Forward.TransformPos }))

    member x.FindPoints(ray : Cone3d, tMin : float, tMax : float) =
        x.FindPoints(Ray3d(ray.Origin, ray.Direction), tMin, tMax, 0.0, tan ray.Angle)

    member x.FindPoints(cylinder : Cylinder3d) =
        let dir = cylinder.P1 - cylinder.P0
        let len = dir.Length
        x.FindPoints(Ray3d(cylinder.P0, dir / len), 0.0, len, cylinder.Radius, 0.0)

    member x.FindPoints(ray : Ray3d,  tMin : float, tMax : float, radius : float) =
        x.FindPoints(ray, tMin, tMax, radius, 0.0)
