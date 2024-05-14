namespace Aardvark.Rendering

open System
open Aardvark.Base

type Camera =
    {
        cameraView : CameraView
        frustum : Frustum
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Camera =

    let inline create (view : CameraView) (frustum : Frustum) = { cameraView = view; frustum = frustum }
    let inline viewTrafo (cam : Camera) = cam.cameraView |> CameraView.viewTrafo
    let inline projTrafo (cam : Camera) = cam.frustum |> Frustum.projTrafo
    let inline viewProjTrafo (cam : Camera) = (CameraView.viewTrafo cam.cameraView) * (Frustum.projTrafo cam.frustum)
    let inline cameraView (cam : Camera) = cam.cameraView
    let inline frustum (cam : Camera) = cam.frustum

    let inline location (cam : Camera) = cam.cameraView |> CameraView.location
    let inline forward  (cam : Camera) = cam.cameraView |> CameraView.forward
    let inline up       (cam : Camera) = cam.cameraView |> CameraView.up
    let inline right    (cam : Camera) = cam.cameraView |> CameraView.right
    let inline backward (cam : Camera) = cam.cameraView |> CameraView.backward
    let inline down     (cam : Camera) = cam.cameraView |> CameraView.down
    let inline left     (cam : Camera) = cam.cameraView |> CameraView.left
    let inline near     (cam : Camera) = cam.frustum.near
    let inline far      (cam : Camera) = cam.frustum.far

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ViewProjection =

    [<AutoOpen>]
    module private Helpers =
        let inline toPlane (v : V4d) =
            Plane3d(-v.XYZ, v.W)

        let inline maxDir (dir : V3d) (b : Box3d) =
            V4d(
                (if dir.X > 0.0 then b.Max.X else b.Min.X),
                (if dir.Y > 0.0 then b.Max.Y else b.Min.Y),
                (if dir.Z > 0.0 then b.Max.Z else b.Min.Z),
                 1.0
            )

        let inline height (plane : V4d) (b : Box3d) =
            plane.Dot(maxDir plane.XYZ b)


        let intersect (a : Plane3d) (b : Plane3d) (c : Plane3d) =
            let mutable pt = V3d.Zero
            if a.Intersects(b,c, &pt) then
                pt
            else
                failwith "no plane intersection"


        let inline proj (pt : V3d) = V3d(pt.XY / -pt.Z, -pt.Z)

    let containing (viewPos : V3d) (bounds : Box3d) =
        let angularRange =
            bounds.ComputeCorners()
                |> Array.map (fun world ->
                    let dir = world - viewPos
                    dir.SphericalFromCartesian()
                    )
                |> Box2d

        let forward = angularRange.Center.CartesianFromSpherical()

        let mutable closest = V3d.Zero
        let mutable farthest = V3d.Zero
        bounds.GetMinMaxInDirection(forward, &closest, &farthest)
        let near = Vec.Distance(viewPos, closest)
        let far = Vec.Distance(viewPos, farthest)

        let halfAngularSize = angularRange.Size * 0.5
        let l = -near * Fun.Tan(halfAngularSize.X)
        let r = -l
        let t = near * Fun.Tan(halfAngularSize.Y)
        let b = -t

        let sky =
            if Fun.ApproximateEquals(abs forward.Z, 1.0, Constant.PositiveTinyValue) then V3d.OIO
            else V3d.OOI

        let view = CameraView.lookAt viewPos (viewPos + forward) sky
        let proj =
            {
                left = l
                right = r
                top = t
                bottom = b
                near = near
                far = far
                isOrtho = false
            }

        view, proj

    let intersects (b : Box3d) (viewProj : Trafo3d) =
        let fw = viewProj.Forward
        let r0 = fw.R0
        let r1 = fw.R1
        let r2 = fw.R2
        let r3 = fw.R3

        height (r3 + r0) b >= 0.0 &&
        height (r3 - r0) b >= 0.0 &&
        height (r3 + r1) b >= 0.0 &&
        height (r3 - r1) b >= 0.0 &&
        height (r3 + r2) b >= 0.0 &&
        height (r3 - r2) b >= 0.0

    let contains (point : V3d) (viewProj : Trafo3d) =
        let fw = viewProj.Forward
        let r0 = fw.R0
        let r1 = fw.R1
        let r2 = fw.R2
        let r3 = fw.R3
        let p = V4d(point, 1.0)

        Vec.Dot((r3 + r0), p) >= 0.0 &&
        Vec.Dot((r3 - r0), p) >= 0.0 &&
        Vec.Dot((r3 + r1), p) >= 0.0 &&
        Vec.Dot((r3 - r1), p) >= 0.0 &&
        Vec.Dot((r3 + r2), p) >= 0.0 &&
        Vec.Dot((r3 - r2), p) >= 0.0

    let private frustumCorners =
        [|
            V3d(-1.0, -1.0, -1.0)
            V3d( 1.0, -1.0, -1.0)
            V3d( 1.0,  1.0, -1.0)
            V3d(-1.0,  1.0, -1.0)
            V3d(-1.0, -1.0,  1.0)
            V3d( 1.0, -1.0,  1.0)
            V3d( 1.0,  1.0,  1.0)
            V3d(-1.0,  1.0,  1.0)
        |]

    let private cornerIndices =
        [|
            1;2; 2;6; 6;5; 5;1;
            2;3; 3;7; 7;6; 4;5;
            7;4; 3;0; 0;4; 0;1;
        |]

    let toIndexedGeometry (v : CameraView) (p : Frustum) (color : C4b) =
        let invViewProj = (CameraView.viewTrafo v * Frustum.projTrafo p).Inverse
        let positions = frustumCorners |> Array.map (invViewProj.Forward.TransformPosProj >> V3f)

        let attributes = SymbolDict<Array>()
        attributes.[DefaultSemantic.Positions] <- cornerIndices |> Array.map (fun i -> positions.[i])
        attributes.[DefaultSemantic.Colors] <- Array.create cornerIndices.Length color

        IndexedGeometry(
            Mode = IndexedGeometryMode.LineList,
            IndexedAttributes = attributes
        )

    let leftPlane (viewProj : Trafo3d) =
        let r0 = viewProj.Forward.R0
        let r3 = viewProj.Forward.R3
        r3 - r0

    let rightPlane (viewProj : Trafo3d) =
        let r0 = viewProj.Forward.R0
        let r3 = viewProj.Forward.R3
        r3 + r0

    let toHull3d (viewProj : Trafo3d) =
        let r0 = viewProj.Forward.R0
        let r1 = viewProj.Forward.R1
        let r2 = viewProj.Forward.R2
        let r3 = viewProj.Forward.R3

        Hull3d [|
            r3 - r0 |> toPlane  // right
            r3 + r0 |> toPlane  // left
            r3 + r1 |> toPlane  // bottom
            r3 - r1 |> toPlane  // top
            r3 + r2 |> toPlane  // near
            r3 - r2 |> toPlane  // far
        |]

    let inline toFastHull3d (viewProj : Trafo3d) =
        FastHull3d(toHull3d viewProj)

    let intersectsDX (b : Box3d) (viewProj : Trafo3d) =
        let fw = viewProj.Forward
        let r0 = fw.R0
        let r1 = fw.R1
        let r2 = fw.R2
        let r3 = fw.R3

        height (r3 + r0) b >= 0.0 &&
        height (r3 - r0) b >= 0.0 &&
        height (r3 + r1) b >= 0.0 &&
        height (r3 - r1) b >= 0.0 &&
        height (     r2) b >= 0.0 &&
        height (r3 - r2) b >= 0.0

    let containsDX (point : V3d) (viewProj : Trafo3d) =
        let fw = viewProj.Forward
        let r0 = fw.R0
        let r1 = fw.R1
        let r2 = fw.R2
        let r3 = fw.R3
        let p = V4d(point, 1.0)

        Vec.Dot((r3 + r0), p) >= 0.0 &&
        Vec.Dot((r3 - r0), p) >= 0.0 &&
        Vec.Dot((r3 + r1), p) >= 0.0 &&
        Vec.Dot((r3 - r1), p) >= 0.0 &&
        Vec.Dot((     r2), p) >= 0.0 &&
        Vec.Dot((r3 - r2), p) >= 0.0

    let toHull3dDX (viewProj : Trafo3d) =
        let r0 = viewProj.Forward.R0
        let r1 = viewProj.Forward.R1
        let r2 = viewProj.Forward.R2
        let r3 = viewProj.Forward.R3

        Hull3d [|
            r3 + r0 |> toPlane
            r3 - r0 |> toPlane
            r3 + r1 |> toPlane
            r3 - r1 |> toPlane
            r2      |> toPlane
            r3 - r2 |> toPlane
        |]

    let inline toFastHull3dDX (viewProj : Trafo3d) =
        FastHull3d(toHull3dDX viewProj)

    let mergeStereo (lProj : Trafo3d) (rProj : Trafo3d) =
        let p (v : V4d) = Plane3d(v.XYZ, -v.W)

        let lPlane = rightPlane lProj |> p
        let rPlane = leftPlane rProj |> p

        let location = intersect lPlane rPlane Plane3d.YPlane
        let view = Trafo3d.Translation(-location)

        let points =
            Array.concat [|
                frustumCorners |> Array.map lProj.Backward.TransformPosProj
                frustumCorners |> Array.map rProj.Backward.TransformPosProj
            |]

        let projected =
            points |> Array.map (view.Forward.TransformPos >> proj)

        let bounds = Box3d(projected)

        let near = bounds.Min.Z
        let far  = bounds.Max.Z

        let frustum =
            {
                left        = bounds.Min.X * near
                right       = bounds.Max.X * near
                top         = bounds.Max.Y * near
                bottom      = bounds.Min.Y * near
                near        = near
                far         = far
                isOrtho     = false
            }

        let proj = Frustum.projTrafo frustum
        view * proj