namespace Aardvark.Rendering.Tests.Other

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto

module ViewProjectionContaining =

    module private Cases =

        let private boxes =
            [ "axis aligned", Box3d(5.0, -1.0, -1.0, 10.0, 1.0, 1.0)
              "wide", Box3d(3.0, -2.0, -0.5, 11.0, 2.0, 0.5)
              "off axis", Box3d(4.0, 1.0, -2.0, 9.0, 4.0, 3.0)
              "diagonal", Box3d(4.0, 3.0, 2.0, 10.0, 7.0, 6.0)
              "negative y", Box3d(2.0, -12.0, -4.0, 6.0, -7.0, 1.0)
              "positive y", Box3d(-2.0, 5.0, -1.0, 1.0, 10.0, 2.0)
              "negative x oblique", Box3d(-9.0, 3.0, -3.0, -5.0, 7.0, 2.0)
              "above", Box3d(2.0, 1.0, 6.0, 5.0, 4.0, 13.0)
              "below", Box3d(1.0, -3.0, -13.0, 4.0, 1.0, -6.0) ]

        let private samples () = seq {
            for name, bounds in boxes do
                for scale in [0.001; 0.125; 1.0; 8.0; 64.0; 1000.0] do
                    for eye in [V3d.Zero; V3d(17.0, -31.0, 11.0); V3d(-125.0, 257.0, -64.0); V3d(0.125, -0.25, 0.5); V3d(1000.0, -2000.0, 500.0)] do
                        let box = Box3d(eye + scale * bounds.Min, eye + scale * bounds.Max)
                        yield $"box={name}, scale={scale}, eye={eye}", eye, box
        }

        // Enumerate the eight endpoints independently of ComputeCorners and the fitting helper.
        let private corners (bounds : Box3d) =
            [| for x in [bounds.Min.X; bounds.Max.X] do
                for y in [bounds.Min.Y; bounds.Max.Y] do
                    for z in [bounds.Min.Z; bounds.Max.Z] do
                        yield V3d(x, y, z) |]

        let private dot (a : V3d) (b : V3d) = a.X * b.X + a.Y * b.Y + a.Z * b.Z

        let private projected (view : CameraView) (point : V3d) =
            let delta = point - view.Location
            V3d(dot view.Right delta, dot view.Up delta, dot view.Forward delta)

        let private close context expected actual =
            let tolerance = 2e-11 * max 1.0 (abs expected)
            Expect.isTrue (Double.IsFinite actual && abs (actual - expected) <= tolerance)
                $"{context}: expected {expected}, actual {actual}, tolerance={tolerance}"

        let clipVolume () =
            let failures = ResizeArray<string>()
            for context, eye, bounds in samples() do
                let view, projection = ViewProjection.containing eye bounds
                let matrix = (view.ViewTrafo * Frustum.projTrafo projection).Forward
                for corner in corners bounds do
                    let clip = matrix * V4d(corner, 1.0)
                    let tolerance = 1e-8 * max 1.0 (abs clip.W)
                    if not clip.IsFinite || clip.W <= 0.0 then
                        failures.Add($"{context}, corner={corner}: invalid homogeneous coordinates {clip}")
                    for name, value in ["horizontal", clip.X; "vertical", clip.Y; "depth", clip.Z] do
                        if value < -clip.W - tolerance || value > clip.W + tolerance then
                            failures.Add($"{context}, corner={corner}, {name}: coordinate={value}, w={clip.W}, tolerance={tolerance}")
            Expect.isEmpty failures (String.concat "\n" failures)

        let depthAndExtents () =
            for context, eye, bounds in samples() do
                let view, projection = ViewProjection.containing eye bounds
                let points = corners bounds |> Array.map (projected view)
                let near = points |> Array.map _.Z |> Array.min
                let far = points |> Array.map _.Z |> Array.max
                Expect.isTrue (near > 0.0 && far > near) $"{context}: fixture wholly in front of the selected camera"
                close (context + ": near camera-space depth") near projection.near
                close (context + ": far camera-space depth") far projection.far
                let horizontal = points |> Array.map (fun p -> abs (p.X / p.Z)) |> Array.max
                let vertical = points |> Array.map (fun p -> abs (p.Y / p.Z)) |> Array.max
                close (context + ": horizontal slope bound") (near * horizontal) projection.right
                close (context + ": vertical slope bound") (near * vertical) projection.top
                Expect.equal projection.left (-projection.right) (context + ": horizontal symmetry")
                Expect.equal projection.bottom (-projection.top) (context + ": vertical symmetry")
                Expect.isFalse projection.isOrtho (context + ": perspective projection")

        // Compatibility reference: retain the original angular center and sky choice,
        // independently of the new depth/slope projection calculation.
        let private selectedView (eye : V3d) (bounds : Box3d) =
            let angularRange = bounds.ComputeCorners() |> Array.map (fun point -> (point - eye).SphericalFromCartesian()) |> Box2d
            let forward = angularRange.Center.CartesianFromSpherical()
            let sky =
                if Fun.ApproximateEquals(abs forward.Z, 1.0, Constant.PositiveTinyValue) then V3d.OIO
                else V3d.OOI
            CameraView.lookAt eye (eye + forward) sky

        let private sameVector context (expected : V3d) (actual : V3d) =
            for axis, a, b in ["x", expected.X, actual.X; "y", expected.Y, actual.Y; "z", expected.Z, actual.Z] do
                Expect.equal (BitConverter.DoubleToInt64Bits b) (BitConverter.DoubleToInt64Bits a)
                    $"{context}, axis={axis}: unchanged view component"

        let viewSelection () =
            for context, eye, bounds in samples() do
                let actual, _ = ViewProjection.containing eye bounds
                let expected = selectedView eye bounds
                sameVector (context + ": location") expected.Location actual.Location
                sameVector (context + ": forward") expected.Forward actual.Forward
                sameVector (context + ": right") expected.Right actual.Right
                sameVector (context + ": up") expected.Up actual.Up
                sameVector (context + ": sky") expected.Sky actual.Sky

        let axialVerticalCorners () =
            for scale in [1.0; 0.125; 16.0] do
                for eye in [V3d.Zero; V3d(17.0, -31.0, 11.0); V3d(-125.0, 257.0, -64.0)] do
                    let bounds = Box3d(eye + scale * V3d(5.0, -1.0, -1.0), eye + scale * V3d(10.0, 1.0, 1.0))
                    let context = $"x=5..10, y/z=-1..1, scale={scale}, eye={eye}"
                    let view, projection = ViewProjection.containing eye bounds
                    // Correcting near alone still leaves the spherical vertical half-angle too small.
                    close (context + ": vertical near extent") scale projection.top
                    close (context + ": horizontal near extent") scale projection.right
                    close (context + ": near depth") (5.0 * scale) projection.near
                    close (context + ": far depth") (10.0 * scale) projection.far
                    sameVector (context + ": forward") V3d.XAxis view.Forward
                    let matrix = (view.ViewTrafo * Frustum.projTrafo projection).Forward
                    for corner in corners bounds do
                        let clip = matrix * V4d(corner, 1.0)
                        if abs (corner.X - (eye.X + 5.0 * scale)) < scale then
                            close (context + ": vertical near corners reach the clip boundary") 1.0 (abs (clip.Y / clip.W))

    let tests (target : TestTarget) =
        [
            "GL clipping encloses every corner",        Cases.clipVolume
            "Depth extrema and symmetric extents",      Cases.depthAndExtents
            "View selection remains unchanged",         Cases.viewSelection
            "Axis fit includes vertical near corners",  Cases.axialVerticalCorners
        ]
        |> prepareCasesCpu "ViewProjection containing" target
