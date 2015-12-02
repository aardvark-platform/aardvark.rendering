namespace Aardvark.Application

open System
open Aardvark.Base

type CameraView private(sky : V3d, location : V3d, forward : V3d, up : V3d, right : V3d) =
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


type Frustum = { left   : float
                 right  : float
                 bottom : float
                 top    : float
                 near   : float
                 far    : float  }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Frustum =
    let perspective (horizontalFieldOfViewInDegrees : float) (near : float) (far : float) (aspect : float) =
        let d = tan (0.5 * Conversion.RadiansFromDegrees horizontalFieldOfViewInDegrees) * near
        { left = -d; right = +d; bottom = -d / aspect; top = +d / aspect; near = near; far = far}

    let projTrafo { left = l; right = r; top = t; bottom = b; near = n; far = f} : Trafo3d = 
        Trafo3d(
            M44d(
                (2.0 * n) / (r - l),                     0.0,     (r + l) / (r - l),                     0.0,
                                0.0,     (2.0 * n) / (t - b),     (t + b) / (t - b),                     0.0,
                                0.0,                     0.0,           f / (n - f),       (f * n) / (n - f),
                                0.0,                     0.0,                  -1.0,                     0.0
                ),                                                     
                                                                       
            M44d(                                      
                (r - l) / (2.0 * n),                     0.0,                     0.0,     (r + l) / (2.0 * n),
                                0.0,     (t - b) / (2.0 * n),                     0.0,     (t + b) / (2.0 * n),
                                0.0,                     0.0,                     0.0,                    -1.0,
                                0.0,                     0.0,       (n - f) / (f * n),                 1.0 / n
                )
        )


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
        let trafo = toTrafo f
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