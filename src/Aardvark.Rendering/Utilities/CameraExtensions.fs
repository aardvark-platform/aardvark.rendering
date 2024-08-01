namespace Aardvark.Rendering

open System
open Aardvark.Base

[<AutoOpen>]
module CameraExtensions =

    module Frustum =
        let pickRayDirection (pp : PixelPosition) (f : Frustum) =
            let n = pp.NormalizedPosition
            let ndc = V3d(2.0 * n.X - 1.0, 1.0 - 2.0 * n.Y, 0.0)
            let trafo = Frustum.projTrafo f
            let dir = trafo.Backward.TransformPosProj ndc |> Vec.Normalized
            dir

    module Camera =
        let pickRay (cam : Camera) (pp : PixelPosition) =
            let dir = cam.frustum |> Frustum.pickRayDirection pp
            let worldDir = cam.cameraView.ViewTrafo.Backward.TransformDir dir
            Ray3d(cam.cameraView.Location, Vec.Normalized worldDir)

        let tryGetPickPointOnPlane (cam : Camera) (plane : Plane3d) (pp : PixelPosition) =
            let r = pickRay cam pp

            let mutable t = Double.PositiveInfinity
            if r.Intersects(plane, &t) && t >= 0.0 then
                Some (r.GetPointOnRay t)
            else
                None