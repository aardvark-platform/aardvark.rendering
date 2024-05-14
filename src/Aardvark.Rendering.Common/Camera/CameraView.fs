namespace Aardvark.Rendering

open System
open Aardvark.Base

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

    static member LookAt(location : V3d, center : V3d, sky : V3d) =
        let fw = center - location |> Vec.Normalized
        let right = Vec.Cross(fw, sky) |> Vec.Normalized
        let up = Vec.Cross(right, fw) |> Vec.Normalized
        CameraView(sky, location, fw, up, right)

    static member LookAt(location : V3d, center : V3d) =
        CameraView.LookAt(location, center, V3d.OOI)

    static member Look(location : V3d, forward : V3d, sky : V3d) =
        let right = Vec.Cross(forward, sky) |> Vec.Normalized
        let up = Vec.Cross(right, forward) |> Vec.Normalized
        CameraView(sky, location, forward, up, right)

    static member Look(location : V3d, forward : V3d) =
        CameraView.Look(location, forward, V3d.OOI)

    static member Orient(location : V3d, orientation : Rot3d, sky : V3d) =
        let frame = M33d.Rotation orientation
        CameraView(sky, location, frame.C1, frame.C2, frame.C0)

    static member Orient(location : V3d, orientation : Rot3d) =
        CameraView.Orient(location, orientation, V3d.OOI)

    member x.WithLocation(l : V3d) =
        CameraView(sky, l, forward, up, right)

    member x.WithForward(fw : V3d) =
        CameraView.Look(location, fw, sky)

    member x.WithRight(right : V3d) =
        let forward  = Vec.Cross(sky, right) |> Vec.Normalized
        let up = Vec.Cross(right, forward) |> Vec.Normalized
        CameraView(sky, location, forward, up, right)

    member x.WithUp(up : V3d) =
        let forward  = Vec.Cross(up, right) |> Vec.Normalized
        let right = Vec.Cross(forward, up) |> Vec.Normalized
        CameraView(sky, location, forward, up, right)

    member x.WithBackward(bw : V3d) =
        x.WithForward (-bw)

    member x.WithLeft(left : V3d) =
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