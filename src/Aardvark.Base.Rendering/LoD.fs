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
        granularity : float

        [<DefaultValue>]
        mutable uniqueId : int
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
    abstract member GetData : node : LodDataNode -> Async<IndexedGeometry>

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
        member x.Rasterize(viewTrafo : Trafo3d, projTrafo : Trafo3d, wantedNearPlaneDistance : float) =
            let result = HashSet<LodDataNode>()


            // extend view and proj a bit to reduce the amount of missing
            // cells in the rasterization when moving
            //let view        = viewTrafo |> CameraView.ofTrafo |> extendView
            let frustum     = projTrafo |> Frustum.ofTrafo |> extendFrustum
//            let viewTrafo = CameraView.viewTrafo view
//            let projTrafo = Frustum.projTrafo frustum
           
            // create a FastHull3d for the (extended) camera
            let hull = viewTrafo * projTrafo |> ViewProjection.toFastHull3d

            // traverse the ILodData building a set of nodes in view respecting
            // the given nearPlaneDistance in [(-1,-1) x (1,1)] space
            x.Traverse(fun node ->
                if hull.Intersects(node.bounds) then
                    if node.inner then
                        let bounds = node.bounds

                        let depthRange =
                            bounds.ComputeCorners()
                                |> Array.map viewTrafo.Forward.TransformPos
                                |> Array.map (fun v -> -v.Z)
                                |> Range1d

                        let depthRange = Range1d(clamp frustum.near frustum.far depthRange.Min, clamp frustum.near frustum.far depthRange.Max)
                        let projAvgDistance =
                            abs (node.granularity / depthRange.Min)

                        // add all inner nodes to the result too
                        result.Add node |> ignore
                        if projAvgDistance > wantedNearPlaneDistance then
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













