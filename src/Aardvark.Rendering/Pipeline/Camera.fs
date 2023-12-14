namespace Aardvark.Rendering

open System
open Aardvark.Base
open System.Runtime.CompilerServices

type CameraView(sky : V3d, location : V3d, forward : V3d, up : V3d, right : V3d) =
    let viewTrafo =
        lazy ( Trafo3d.ViewTrafo(location, right, up, -forward) )

    let orientation =
        lazy (
            let frame = M33d.FromCols(right, forward, up) |> Mat.Orthonormalized
            Rot3d.FromM33d frame
        )

    member x.Sky = sky
    member x.Location = location
    member x.Forward = forward
    member x.Up = up
    member x.Right = right
    member x.Backward = -forward
    member x.Down = -up
    member x.Left = -right

    member x.ViewTrafo = viewTrafo.Value
    member x.Orientation = orientation.Value

    override x.ToString() =
        sprintf "CameraView(sky=%A, location=%A, forward=%A, up=%A, right=%A)" sky location forward up right

    static member LookAt (location : V3d, center : V3d, sky : V3d) =
        let fw = center - location |> Vec.normalize
        let right = Vec.cross fw sky |> Vec.normalize
        let up = Vec.cross right fw |> Vec.normalize
        CameraView(sky, location, fw, up, right)

    static member LookAt (location : V3d, center : V3d) =
        CameraView.LookAt(location, center, V3d.OOI)

    static member Look (location : V3d, forward : V3d, sky : V3d) =
        let right = Vec.cross forward sky |> Vec.normalize
        let up = Vec.cross right forward |> Vec.normalize
        CameraView(sky, location, forward, up, right)

    static member Look (location : V3d, forward : V3d) =
        CameraView.Look(location, forward, V3d.OOI)

    static member Orient (location : V3d, orientation : Rot3d, sky : V3d) =
        let frame = M33d.Rotation orientation
        CameraView(sky, location, frame.C1, frame.C2, frame.C0)

    static member Orient (location : V3d, orientation : Rot3d) =
        CameraView.Orient(location, orientation, V3d.OOI)


    member x.WithLocation(l : V3d) =
        CameraView(sky, l, forward, up, right)

    member x.WithForward(fw : V3d) =
        CameraView.Look(location, fw, sky)

    member x.WithRight(right : V3d) =
        let forward  = Vec.cross sky right |> Vec.normalize
        let up = Vec.cross right forward |> Vec.normalize
        CameraView(sky, location, forward, up, right)

    member x.WithUp(up : V3d) =
        let forward  = Vec.cross up right |> Vec.normalize
        let right = Vec.cross forward up |> Vec.normalize
        CameraView(sky, location, forward, up, right)

    member x.WithBackward(bw : V3d) =
        x.WithForward (-bw)

    member x.WithLeft (left : V3d) =
        x.WithRight(-left)

    member x.WithDown(down : V3d) =
        x.WithUp(-down)

    member x.WithOrientation(orientation : Rot3d) =
        CameraView.Orient(location, orientation, sky)


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CameraView =

    let lookAt (location : V3d) (center : V3d) (sky : V3d) =
        CameraView.LookAt(location, center, sky)

    let look (location : V3d) (forward : V3d) (sky : V3d) =
        CameraView.Look(location, forward, sky)

    let orient (location : V3d) (orientation : Rot3d) (sky : V3d) =
        CameraView.Orient(location, orientation, sky)


    let withLocation (location : V3d) (c : CameraView) =
        c.WithLocation location

    let withForward (forward : V3d) (c : CameraView) =
        c.WithForward forward

    let withRight (right : V3d) (c : CameraView) =
        c.WithRight right

    let withUp (up : V3d) (c : CameraView) =
        c.WithUp up

    let withBackward (backward : V3d) (c : CameraView) =
        c.WithBackward backward

    let withLeft (left : V3d) (c : CameraView) =
        c.WithLeft left

    let withDown (down : V3d) (c : CameraView) =
        c.WithDown down

    let withOrientation (orientation : Rot3d) (c : CameraView) =
        c.WithOrientation orientation


    let viewTrafo (c : CameraView) =
        c.ViewTrafo

    let orientation (c : CameraView) =
        c.Orientation

    let ofTrafo (t : Trafo3d) =
        let bw = t.Backward

        let right       = bw.C0.XYZ     // bw.TransformDir(V3d.IOO)
        let up          = bw.C1.XYZ     // bw.TransformDir(V3d.OIO)
        let forward     = -bw.C2.XYZ    // bw.TransformDir(-V3d.OOI)
        let location    = bw.C3.XYZ     // bw.TransformPos(V3d.OOO)

        CameraView(up, location, forward, up, right)

    let tryGetCenterOn (p : Plane3d) (c : CameraView) =
        let r = Ray3d(c.Location, c.Forward)

        let mutable t = Double.PositiveInfinity
        if r.Intersects(p, &t) && t >= 0.0 then
            Some (r.GetPointOnRay t)
        else
            None



    let inline location (c : CameraView) = c.Location
    let inline forward  (c : CameraView) = c.Forward
    let inline right    (c : CameraView) = c.Right
    let inline up       (c : CameraView) = c.Up
    let inline backward (c : CameraView) = c.Backward
    let inline left     (c : CameraView) = c.Left
    let inline down     (c : CameraView) = c.Down





type Frustum =
    {
        left   : float
        right  : float
        bottom : float
        top    : float
        near   : float
        far    : float
        isOrtho : bool
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Frustum =
    let perspective (horizontalFieldOfViewInDegrees : float) (near : float) (far : float) (aspect : float) =
        let d = tan (0.5 * Conversion.RadiansFromDegrees horizontalFieldOfViewInDegrees) * near
        { left = -d; right = +d; bottom = -d / aspect; top = +d / aspect; near = near; far = far; isOrtho = false }

    let ortho (b : Box3d) =
        {
            left = b.Min.X
            right = b.Max.X
            bottom = b.Min.Y
            top = b.Max.Y
            near = b.Min.Z
            far = b.Max.Z
            isOrtho = true
        }

    let projTrafo {left = l; right = r; top = t; bottom = b; near = n; far = f; isOrtho = isOrtho } : Trafo3d =
        if isOrtho then
            Trafo3d.OrthoProjectionGL(l, r, b, t, n, f)
        else
            Trafo3d.PerspectiveProjectionGL(l, r, b, t, n, f)

    let private isTrafoOrtho (t : Trafo3d) =
        t.Forward.M30.IsTiny() && t.Forward.M31.IsTiny() && t.Forward.M32.IsTiny()

    let ofTrafo (t : Trafo3d) =
        let isOrtho = isTrafoOrtho t
        let m = t.Forward
        if not isOrtho then

            let r = (1.0 + m.M22) / (m.M22 - 1.0)
            let far     = (r - 1.0) * m.M23 / (2.0 * r)
            let near    = r * far
            let top     = (1.0 + m.M12) * near / m.M11
            let bottom  = (m.M12 - 1.0) * near / m.M11
            let left    = (m.M02 - 1.0) * near / m.M00
            let right   = (1.0 + m.M02) * near / m.M00


            {
                isOrtho = false
                left = left
                right = right
                top = top
                bottom = bottom
                near = near
                far = far
            }
        else
            let left        = -(1.0 + m.M03) / m.M00
            let right       = (1.0 - m.M03) / m.M00
            let bottom      = -(1.0 + m.M13) / m.M11
            let top         = (1.0 - m.M13) / m.M11
            let far         = -(1.0 + m.M23) / m.M22
            let near        = (1.0 - m.M23) / m.M22
            {
                isOrtho = true
                left = left
                right = right
                top = top
                bottom = bottom
                near = near
                far = far
            }

    let withNear (near : float) (f : Frustum) =
        if f.isOrtho then
            { f with near = near }
        else
            let factor = near / f.near
            {
                isOrtho = false
                near = near
                far = f.far
                left = factor * f.left
                right = factor * f.right
                top = factor * f.top
                bottom = factor * f.bottom
            }

    let withFar (far : float) (f : Frustum) =
        { f with far = far }

    let aspect { left = l; right = r; top = t; bottom = b } =
        (r - l) / (t - b)

    let withAspect (newAspect : float) ( { left = l; right = r; top = t; bottom = b } as f )  =
        let factor = aspect f / newAspect
        { f with top = factor * t; bottom = factor * b }

    let withHorizontalFieldOfViewInDegrees (angleInDegrees : float) (frustum : Frustum) =
        if frustum.isOrtho then
            frustum
        else
            let aspect = aspect frustum
            perspective angleInDegrees frustum.near frustum.far aspect

    let horizontalFieldOfViewInDegrees { left = l; right = r; near = near } =
        let l,r = atan2 l near, atan2 r near
        Conversion.DegreesFromRadians(-l + r)

    let inline near   (f : Frustum) = f.near
    let inline far    (f : Frustum) = f.far
    let inline left   (f : Frustum) = f.left
    let inline right  (f : Frustum) = f.right
    let inline bottom (f : Frustum) = f.bottom
    let inline top    (f : Frustum) = f.top

    let pickRayDirection (pp : PixelPosition) (f : Frustum) =
        let n = pp.NormalizedPosition
        let ndc = V3d(2.0 * n.X - 1.0, 1.0 - 2.0 * n.Y, 0.0)
        let trafo = projTrafo f
        let dir = trafo.Backward.TransformPosProj ndc |> Vec.normalize
        dir


type Camera =
    {
        cameraView : CameraView
        frustum : Frustum
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Camera =

    let create (view : CameraView) (f : Frustum) =
        { cameraView = view; frustum = f }

    let pickRay (cam : Camera) (pp : PixelPosition) =
        let dir = cam.frustum |> Frustum.pickRayDirection pp
        let worldDir = cam.cameraView.ViewTrafo.Backward.TransformDir dir
        Ray3d(cam.cameraView.Location, Vec.normalize worldDir)

    let tryGetPickPointOnPlane (cam : Camera) (plane : Plane3d) (pp : PixelPosition) =
        let r = pickRay cam pp

        let mutable t = Double.PositiveInfinity
        if r.Intersects(plane, &t) && t >= 0.0 then
            Some (r.GetPointOnRay t)
        else
            None


    let inline viewTrafo (cam : Camera) = cam.cameraView |> CameraView.viewTrafo

    let inline projTrafo (cam : Camera) = cam.frustum |> Frustum.projTrafo

    let viewProjTrafo (cam : Camera) = (CameraView.viewTrafo cam.cameraView) * (Frustum.projTrafo cam.frustum)

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
        let near = Vec.distance viewPos closest
        let far = Vec.distance viewPos farthest



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

        Vec.dot (r3 + r0) p >= 0.0 &&
        Vec.dot (r3 - r0) p >= 0.0 &&
        Vec.dot (r3 + r1) p >= 0.0 &&
        Vec.dot (r3 - r1) p >= 0.0 &&
        Vec.dot (r3 + r2) p >= 0.0 &&
        Vec.dot (r3 - r2) p >= 0.0

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

        let positions = frustumCorners |> Array.map (invViewProj.Forward.TransformPosProj)

        IndexedGeometry(
            Mode = IndexedGeometryMode.LineList,
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, cornerIndices |> Array.map (fun i -> positions.[i].ToV3f()) :> Array
                    DefaultSemantic.Colors, Array.create cornerIndices.Length color :> Array
                ]
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

        Vec.dot (r3 + r0) p >= 0.0 &&
        Vec.dot (r3 - r0) p >= 0.0 &&
        Vec.dot (r3 + r1) p >= 0.0 &&
        Vec.dot (r3 - r1) p >= 0.0 &&
        Vec.dot (     r2) p >= 0.0 &&
        Vec.dot (r3 - r2) p >= 0.0

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



[<Extension;AutoOpen>]
type CameraCSharpExtensions() =
    [<Extension>]
    static member ProjTrafo(f : Frustum) = Frustum.projTrafo f
