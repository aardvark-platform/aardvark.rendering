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

    let location (c : CameraView) = c.Location
    let forward  (c : CameraView) = c.Forward
    let right    (c : CameraView) = c.Right
    let up       (c : CameraView) = c.Up
    let backward (c : CameraView) = c.Backward
    let left     (c : CameraView) = c.Left
    let down     (c : CameraView) = c.Down


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

    let toTrafo { left = l; right = r; top = t; bottom = b; near = n; far = f} : Trafo3d = 
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

    let horizontalFieldOfViewInDegrees { left = l; right = r; near = near } = 
        let l,r = atan2 l near, atan2 r near
        Conversion.DegreesFromRadians(-l + r)

    let near   (f : Frustum) = f.near
    let far    (f : Frustum) = f.far
    let left   (f : Frustum) = f.left
    let right  (f : Frustum) = f.right
    let bottom (f : Frustum) = f.bottom
    let top    (f : Frustum) = f.top

    let aspect { left = l; right = r; top = t; bottom = b } = (r - l) / (t - b)
    let withAspect (newAspect : float) ( { left = l; right = r; top = t; bottom = b } as f )  = 
        let factor = 1.0 - (newAspect / aspect f)
        { f with right = factor * l + r; left  = factor * r + l }

    let unproject { near = n } (xyOnPlane : V2d) = Ray3d(V3d.Zero, V3d(xyOnPlane, -n))
        
    