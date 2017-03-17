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

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PickShape =
        let bounds (p : PickShape) =
            match p with
                | Box b -> b
                | Sphere s -> s.BoundingBox3d
                | Cylinder c -> c.BoundingBox3d

    type Pickable = { trafo : Trafo3d; shape : PickShape }
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Pickable =

        let ofShape (shape : PickShape) =
            { trafo = Trafo3d.Identity; shape = shape }

        let transform (t : Trafo3d) (p : Pickable) =
            { p with trafo = p.trafo * t }

        let bounds (p : Pickable) =
            PickShape.bounds(p.shape).Transformed(p.trafo)

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
            
        let pickable (shape : PickShape) (sg : ISg) =
            PickableApplicator(Mod.constant (Pickable.ofShape shape), Mod.constant sg) :> ISg
            
        let pickBoundingBox (sg : ISg) =
            let pickable = sg.LocalBoundingBox() |> Mod.map (PickShape.Box >> Pickable.ofShape)
            PickableApplicator(pickable, Mod.constant sg) :> ISg

namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.SceneGraph

[<AutoOpen>]
module PickingSemantics =

    type ISg with
        member x.PickObjects() : aset<PickObject> = x?PickObjects()

    [<Semantic>]
    type PickObjectSem() =
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