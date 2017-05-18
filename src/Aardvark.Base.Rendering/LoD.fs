namespace Aardvark.Base.Rendering

open System.Collections.Generic
open Aardvark.Base
open Aardvark.Base.Incremental

[<CustomEquality; NoComparison>]
type LodDataNode =
    {
        id : obj
        level : int
        bounds : Box3d
        inner : bool
        pointCountTree : int64
        pointCountNode : int64
        render : bool
    }

    override x.GetHashCode() = x.id.GetHashCode()
    override x.Equals o =
        match o with
            | :? LodDataNode as o -> x.id.Equals(o.id)
            | _ -> false

type ILodData =
    abstract member BoundingBox : Box3d
    abstract member Traverse : (LodDataNode -> bool) -> unit
    abstract member Dependencies : list<IMod>
    abstract member GetData : node : LodDataNode -> Async<Option<IndexedGeometry>>

module LodData =
    
    type Decider = Trafo3d -> Trafo3d -> V2i -> LodDataNode -> bool
    
    let defaultLodDecider (targetPixelDistance : float) (viewTrafo : Trafo3d) (projTrafo : Trafo3d) (viewPortSize : V2i) (node : LodDataNode )  =
        let bounds = node.bounds

        let vp = viewTrafo * projTrafo

        let nearPlaneAreaInPixels =
            let npp= bounds.ComputeCorners()
                     |> Array.map (vp.Forward.TransformPosProj >> Vec.xy)
                     |> Polygon2d

            let npp = npp.ComputeConvexHullIndexPolygon().ToPolygon2d().Points
                      |> Seq.map ( fun p -> V2d(0.5 * p.X + 0.5, 0.5 - 0.5 * p.Y) * V2d viewPortSize )
                      |> Polygon2d
                      
            npp.ComputeArea()

        let averagePointDistanceInPixels = sqrt (nearPlaneAreaInPixels / float node.pointCountNode)

        averagePointDistanceInPixels > targetPixelDistance

[<AutoOpen>]
module ``Lod Data Extensions`` =
    open System.Collections.Concurrent

    let inline private maxDir (dir : V3d) (b : Box3d) =
        V4d(
            (if dir.X > 0.0 then b.Max.X else b.Min.X), 
            (if dir.Y > 0.0 then b.Max.Y else b.Min.Y), 
            (if dir.Z > 0.0 then b.Max.Z else b.Min.Z), 
            1.0
        )

    let inline private height (plane : V4d) (b : Box3d) =
        plane.Dot(maxDir plane.XYZ b)

    let inline private extendView (view : CameraView) =
        // TODO: find some magic here (maybe needing movement info)
        view

    let inline private extendFrustum (frustum : Frustum) =
        // TODO: find some magic here
        frustum

    type ILodData with
        member x.Rasterize(viewTrafo : Trafo3d, projTrafo : Trafo3d, decider : LodDataNode -> bool) =
            let result = HashSet<LodDataNode>()
            
            // create a FastHull3d for the (extended) camera
            let hull = viewTrafo * projTrafo |> ViewProjection.toFastHull3d

            // traverse the ILodData building a set of nodes in view respecting
            // the given nearPlaneDistance in [(-1,-1) x (1,1)] space
            x.Traverse(fun node ->
                if hull.Intersects(node.bounds) then
                    if node.inner then
                        if decider node then
                            if node.render then result.Add node |> ignore
                            true
                        else
                            false
                    else
                        result.Add node |> ignore
                        false
                else
                    false
            )

            // return the resulting node-set
            result :> ISet<_>













