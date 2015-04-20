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
    let forward (c : CameraView) = c.Forward
    let right (c : CameraView) = c.Right
    let up (c : CameraView) = c.Up
    let backward (c : CameraView) = c.Backward
    let left (c : CameraView) = c.Left
    let down (c : CameraView) = c.Down
