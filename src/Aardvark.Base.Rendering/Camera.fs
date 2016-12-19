namespace Aardvark.Base

open System
open Aardvark.Base
open System.Runtime.CompilerServices

type CameraView(sky : V3d, location : V3d, forward : V3d, up : V3d, right : V3d) =
    let viewTrafo = lazy ( Trafo3d.ViewTrafo(location, right, up, -forward) )

    member x.Sky = sky
    member x.Location = location
    member x.Forward = forward
    member x.Up = up
    member x.Right = right
    member x.Backward = -forward
    member x.Down = -up
    member x.Left = -right

    member x.ViewTrafo = viewTrafo.Value

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


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CameraView =
    
    let lookAt (location : V3d) (center : V3d) (sky : V3d) =
        CameraView.LookAt(location, center, sky)

    let look (location : V3d) (forward : V3d) (sky : V3d) =
        CameraView.Look(location, forward, sky)


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



    let viewTrafo (c : CameraView) =
        c.ViewTrafo

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
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Frustum =
    let perspective (horizontalFieldOfViewInDegrees : float) (near : float) (far : float) (aspect : float) =
        let d = tan (0.5 * Conversion.RadiansFromDegrees horizontalFieldOfViewInDegrees) * near
        { left = -d; right = +d; bottom = -d / aspect; top = +d / aspect; near = near; far = far}

    let ortho (b : Box3d) =
        { 
            left = b.Min.X
            right = b.Max.X
            bottom = b.Min.Y
            top = b.Max.Y
            near = b.Min.Z
            far = b.Max.Z
        }

    let projTrafo {left = l; right = r; top = t; bottom = b; near = n; far = f} : Trafo3d = 
        Trafo3d(
            M44d(
                (2.0 * n) / (r - l),                     0.0,         (r + l) / (r - l),                        0.0,
                                0.0,     (2.0 * n) / (t - b),         (t + b) / (t - b),                        0.0,
                                0.0,                     0.0,         (f + n) / (n - f),    (2.0 * f * n) / (n - f),
                                0.0,                     0.0,                      -1.0,                        0.0
                ),                                                     
                                                                       
            M44d(                                      
                (r - l) / (2.0 * n),                     0.0,                       0.0,        (r + l) / (2.0 * n),
                                0.0,     (t - b) / (2.0 * n),                       0.0,        (t + b) / (2.0 * n),
                                0.0,                     0.0,                       0.0,                       -1.0,
                                0.0,                     0.0,   (n - f) / (2.0 * f * n),     (f + n) / (2.0 * f * n)
                )
        )

    let orthoTrafo {left = l; right = r; top = t; bottom = b; near = n; far = f} : Trafo3d = 
        Trafo3d(
            M44d(
                2.0 / (r - l),               0.0,               0.0,      (r + l) / (l - r),
                          0.0,     2.0 / (t - b),               0.0,      (t + b) / (b - t),
                          0.0,               0.0,      2.0 / (n - f),     (f + n) / (n - f),
                          0.0,               0.0,               0.0,      1.0
            ),                                                     
                                                                       
            M44d(
                (r - l) / 2.0,               0.0,               0.0,      (r + l) / 2.0,
                          0.0,     (t - b) / 2.0,               0.0,      (t + b) / 2.0,
                          0.0,               0.0,     (n - f) / 2.0,     -(f + n) / 2.0,
                          0.0,               0.0,               0.0,      1.0

            )
        )



    let ofTrafo (t : Trafo3d) =
        let bw = t.Backward
        let far = 1.0 / (bw.M32 + bw.M33)
        let near = 1.0 / bw.M33
        let cx = bw.M03 * near
        let cy = bw.M13 * near
        let hsx = bw.M00 * near
        let hsy = bw.M11 * near

        {
            left = cx - hsx
            right = cx + hsx
            top = cy + hsy
            bottom = cy - hsy
            near = near
            far = far
        }

    [<Obsolete("use projTrafo instead")>]
    let toTrafo f : Trafo3d = projTrafo f

    let horizontalFieldOfViewInDegrees { left = l; right = r; near = near } = 
        let l,r = atan2 l near, atan2 r near
        Conversion.DegreesFromRadians(-l + r)

    let inline near   (f : Frustum) = f.near
    let inline far    (f : Frustum) = f.far
    let inline left   (f : Frustum) = f.left
    let inline right  (f : Frustum) = f.right
    let inline bottom (f : Frustum) = f.bottom
    let inline top    (f : Frustum) = f.top

    let aspect { left = l; right = r; top = t; bottom = b } = (r - l) / (t - b)
    let withAspect (newAspect : float) ( { left = l; right = r; top = t; bottom = b } as f )  = 
        let factor = 1.0 - (newAspect / aspect f)
        { f with right = factor * l + r; left  = factor * r + l }

    [<Obsolete("use pickRayDirection instead")>]
    let unproject { near = n } (xyOnPlane : V2d) = Ray3d(V3d.Zero, V3d(xyOnPlane, -n))

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
        let near = V3d.Distance(viewPos, closest)
        let far = V3d.Distance(viewPos, farthest)



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


    let toHull3d (viewProj : Trafo3d) =
        let r0 = viewProj.Forward.R0
        let r1 = viewProj.Forward.R1
        let r2 = viewProj.Forward.R2
        let r3 = viewProj.Forward.R3


        Hull3d [|
            r3 + r0 |> toPlane  // left
            r3 - r0 |> toPlane  // right
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

[<Extension;AutoOpen>]
type CameraCSharpExtensions() =
    [<Extension>]
    static member ProjTrafo(f : Frustum) = Frustum.projTrafo f
