namespace Aardvark.Rendering

open System
open System.Runtime.CompilerServices
open Aardvark.Base

[<AutoOpen>]
module CameraExtensions =

    module Frustum =
        /// Returns the unit view-space picking direction; orthographic rays always point along -Z.
        let pickRayDirection (pp : PixelPosition) (f : Frustum) =
            if f.isOrtho then
                -V3d.ZAxis
            else
                let n = pp.NormalizedPosition
                let ndc = V3d(2.0 * n.X - 1.0, 1.0 - 2.0 * n.Y, 0.0)
                let trafo = Frustum.projTrafo f
                let dir = trafo.Backward.TransformPosProj ndc |> Vec.Normalized
                dir

    module Camera =
        /// Returns a world-space ray with unit direction through the pixel center.
        /// Perspective rays start at the camera location; orthographic rays start on the near plane (view-space z = -near).
        // Preserve inlining of the perspective path despite the additional orthographic origin calculation.
        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        let pickRay (cam : Camera) (pp : PixelPosition) =
            let dir = cam.frustum |> Frustum.pickRayDirection pp
            let inverseView = cam.cameraView.ViewTrafo.Backward
            let origin =
                if cam.frustum.isOrtho then
                    let f = cam.frustum
                    let n = pp.NormalizedPosition
                    let viewOrigin = V3d(f.left + n.X * (f.right - f.left), f.top + n.Y * (f.bottom - f.top), -f.near)
                    inverseView.TransformPos viewOrigin
                else
                    cam.cameraView.Location
            let worldDir = inverseView.TransformDir dir
            Ray3d(origin, Vec.Normalized worldDir)

        /// Intersects the forward half-ray (t >= 0), including its origin but without far-plane clipping.
        /// Planes behind the origin are rejected; for orthographic cameras the origin lies on the near plane, not at the eye.
        let tryGetPickPointOnPlane (cam : Camera) (plane : Plane3d) (pp : PixelPosition) =
            let r = pickRay cam pp

            let mutable t = Double.PositiveInfinity
            if r.Intersects(plane, &t) && t >= 0.0 then
                Some (r.GetPointOnRay t)
            else
                None