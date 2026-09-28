namespace Aardvark.Rendering.Tests

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application
open FSharp.Data.Adaptive
open Expecto

module ``Camera Tests`` =

    module private Expect =

        let frustumClose (accuracy : Accuracy) (a : Frustum) (b : Frustum) (message : string) =
            Expect.floatClose accuracy a.near b.near (message + " (near)")
            Expect.floatClose accuracy a.far b.far (message + " (far)")
            Expect.floatClose accuracy a.left b.left (message + " (left)")
            Expect.floatClose accuracy a.right b.right (message + " (right)")
            Expect.floatClose accuracy a.top b.top (message + " (top)")
            Expect.floatClose accuracy a.bottom b.bottom (message + " (bottom)")
            Expect.equal a.isOrtho b.isOrtho (message + " (isOrtho)")

    module Frustum =
        let aspect =
            test "aspect" {
                let f = Frustum.perspective 75.0 0.1 100.0 1.77
                let a = Frustum.aspect f
                Expect.floatClose Accuracy.high a 1.77 "Aspect is wrong"
            }

        let fieldOfView =
            test "fieldOfView" {
                let f = Frustum.perspective 75.0 0.1 100.0 1.77
                let fov = Frustum.horizontalFieldOfViewInDegrees f
                Expect.floatClose Accuracy.high fov 75.0 "Field of view is wrong"
            }

        let withAspect =
            test "withAspect" {
                let a = Frustum.perspective 75.0 0.1 100.0 1.77
                let b = Frustum.perspective 75.0 0.1 100.0 1.5
                let c = a |> Frustum.withAspect 1.5
                Expect.frustumClose Accuracy.high b c "Frustums do not match"
            }

        let withNear =
            test "withNear" {
                let a = Frustum.perspective 75.0 0.1 100.0 1.77
                let b = Frustum.perspective 75.0 0.01 100.0 1.77
                let c = a |> Frustum.withNear 0.01
                Expect.frustumClose Accuracy.high b c "Frustums do not match"
            }

        let withFieldOfView =
            test "withFieldOfView" {
                let a = Frustum.perspective 75.0 0.1 100.0 1.77
                let b = Frustum.perspective 90.0 0.1 100.0 1.77
                let c = a |> Frustum.withHorizontalFieldOfViewInDegrees 90.0
                Expect.frustumClose Accuracy.high b c "Frustums do not match"
            }

    module Orbit =
        let private vectorClose tolerance (expected : V3d) (actual : V3d) message =
            Expect.isTrue (actual.IsFinite && (actual - expected).Length <= tolerance)
                $"{message}: expected {expected}, got {actual}"

        let private viewClose tolerance (expected : CameraView) (actual : CameraView) =
            vectorClose tolerance expected.Location actual.Location "location"
            vectorClose tolerance expected.Forward actual.Forward "forward"
            vectorClose tolerance expected.Right actual.Right "right"
            vectorClose tolerance expected.Up actual.Up "up"
            Expect.equal actual.Sky expected.Sky "sky is preserved"

        type private Orbit(initial : CameraView, center : aval<V3d>, ?start : V2i) =
            let mouse = EventMouse(false)
            let mutable position = defaultArg start (V2i(320, 240))
            let pixel() = PixelPosition(position, 1920, 1080)
            let time = cval (DateTime(2020, 1, 1))
            do mouse.Move(pixel())
            let view = AVal.integrate initial time [DefaultCameraController.controlOrbitAround mouse center]
            do AVal.force view |> ignore
            member _.Read() = AVal.force view
            member _.MoveWithoutRead(delta : V2i) =
                position <- position + delta
                mouse.Move(pixel())
            member x.Move(delta : V2i) =
                x.MoveWithoutRead delta
                x.Read()
            member x.Down(button : MouseButtons) =
                mouse.Down(pixel(), button)
                x.Read()
            member x.Up(button : MouseButtons, ?delta : V2i) =
                position <- position + defaultArg delta V2i.Zero
                mouse.Up(pixel(), button)
                x.Read()
            member x.Tick() =
                transact (fun () -> time.Value <- time.Value.AddMilliseconds 16.0)
                x.Read()

        // Rodrigues' formula provides a reference independent of the production matrix product.
        // The product applies the sky rotation first, then the previous view's right-axis rotation.
        let private referenceStep (center : V3d) (delta : V2i) (cam : CameraView) =
            let rotate (axis : V3d) angle (v : V3d) =
                let c, s = cos angle, sin angle
                v * c + Vec.Cross(axis, v) * s + axis * (Vec.Dot(axis, v) * (1.0 - c))
            let offset =
                cam.Location - center
                |> rotate cam.Sky (-0.01 * float delta.X)
                |> rotate cam.Right (-0.01 * float delta.Y)
            CameraView.Look(center + offset, (-offset).Normalized, cam.Sky)

        let private legacyStep (delta : V2i) (cam : CameraView) =
            let rotation = M44d.Rotation(cam.Right, float delta.Y * -0.01) * M44d.Rotation(cam.Sky, float delta.X * -0.01)
            let location = rotation.TransformDir cam.Location
            cam.WithLocation(location).WithForward((V3d.Zero - location).Normalized)

        let private checkOrbit (center : V3d) (radius : float) (sky : V3d) (cam : CameraView) =
            let tolerance = 1e-9 * max 1.0 radius
            Expect.isTrue (abs ((cam.Location - center).Length - radius) <= tolerance) "orbit radius is preserved"
            vectorClose 1e-9 (center - cam.Location).Normalized cam.Forward "camera faces center"
            for direction in [cam.Forward; cam.Right; cam.Up] do
                Expect.isTrue (direction.IsFinite && abs (direction.Length - 1.0) <= 1e-10) "unit camera basis"
            Expect.isTrue (abs (Vec.Dot(cam.Right, cam.Forward)) <= 1e-10) "right perpendicular to forward"
            Expect.isTrue (abs (Vec.Dot(cam.Up, cam.Forward)) <= 1e-10) "up perpendicular to forward"
            Expect.equal cam.Sky sky "unchanged sky vector"

        let private drags = [
            "horizontal", [|V2i(20, 0)|]
            "vertical", [|V2i(0, 10)|]
            "combined", [|V2i(20, 10)|]
            "negative", [|V2i(-12, -9)|]
            "repeated", Array.init 80 (fun i -> if i % 2 = 0 then V2i(7, 3) else V2i(-4, -2))
        ]

        let tests =
            testList "Orbit" [
                for name, deltas in drags do
                    testCase $"{name}" <| fun _ ->
                        for center in [V3d.Zero; V3d(100.0, 200.0, 30.0); V3d(-100.0, -200.0, -30.0); V3d(13.25, -47.5, 6.75)] do
                            for offset in [V3d(0.0, -10.0, 3.0); V3d(-7.0, 4.0, -2.0)] do
                                for sky in [V3d.ZAxis; V3d(1.0, -2.0, 4.0).Normalized] do
                                    let initial = CameraView.LookAt(center + offset, center, sky)
                                    let orbit = Orbit(initial, AVal.constant center)
                                    let mutable expected = orbit.Down MouseButtons.Left
                                    viewClose 1e-12 initial expected
                                    for delta in deltas do
                                        expected <- referenceStep center delta expected
                                        let actual = orbit.Move delta
                                        viewClose (1e-9 * offset.Length) expected actual
                                        checkOrbit center offset.Length sky actual

                testCase $"translation equivariance" <| fun _ ->
                    for shift in [V3d(100.0, 200.0, 30.0); V3d(-900.0, 13.5, -210.0); V3d(0.125, -0.25, 0.5)] do
                        for name, deltas in drags do
                            let initial = CameraView.LookAt(V3d(0.0, -10.0, 3.0), V3d.Zero, V3d.ZAxis)
                            let origin = Orbit(initial, AVal.constant V3d.Zero)
                            let translated = Orbit(initial.WithLocation(initial.Location + shift), AVal.constant shift, V2i(12, 34))
                            origin.Down MouseButtons.Left |> ignore
                            translated.Down MouseButtons.Left |> ignore
                            for delta in deltas do
                                let a = origin.Move delta
                                let b = translated.Move delta
                                viewClose 1e-9 (a.WithLocation(a.Location + shift)) b

                testCase $"origin compatibility" <| fun _ ->
                    for sky in [V3d.ZAxis; V3d(1.0, 2.0, 3.0).Normalized; V3d(0.0, 0.0, 2.0)] do
                        for name, deltas in drags do
                            let initial = CameraView.LookAt(V3d(2.0, -10.0, 3.0), V3d.Zero, sky)
                            let orbit = Orbit(initial, AVal.constant V3d.Zero)
                            let mutable expected = legacyStep V2i.Zero initial
                            viewClose 1e-12 expected (orbit.Down MouseButtons.Left)
                            for delta in deltas do
                                expected <- legacyStep delta expected
                                viewClose (1e-10 * max 1.0 expected.Location.Length) expected (orbit.Move delta)

                testCase "press, release and inactive movement do not accumulate deltas" <| fun _ ->
                    let center = V3d(100.0, 200.0, 30.0)
                    let initial = CameraView.LookAt(center + V3d(0.0, -10.0, 3.0), center, V3d.ZAxis)
                    let orbit = Orbit(initial, AVal.constant center)
                    Expect.isTrue (Object.ReferenceEquals(initial, orbit.Move(V2i(45, -32)))) "inactive movement retains view"
                    viewClose 1e-12 initial (orbit.Down MouseButtons.Left)
                    let moved = orbit.Move(V2i(20, 10))
                    viewClose 1e-10 (referenceStep center (V2i(20, 10)) initial) moved
                    Expect.isTrue (Object.ReferenceEquals(moved, orbit.Read())) "forcing twice does not replay movement"
                    Expect.isTrue (Object.ReferenceEquals(moved, orbit.Tick())) "time alone does not move orbit"
                    Expect.isTrue (Object.ReferenceEquals(moved, orbit.Up(MouseButtons.Left, V2i(8, 13)))) "release movement is gated off"
                    Expect.isTrue (Object.ReferenceEquals(moved, orbit.Move(V2i(-30, 14)))) "released movement retains view"
                    viewClose 1e-10 moved (orbit.Down MouseButtons.Left)
                    viewClose 1e-10 (referenceStep center (V2i(-5, 6)) moved) (orbit.Move(V2i(-5, 6)))

                testCase $"only the left button activates orbit" <| fun _ ->
                    for button in [MouseButtons.Right; MouseButtons.Middle; MouseButtons.Button4; MouseButtons.Right ||| MouseButtons.Middle] do
                        let center = V3d(-17.0, 23.0, -4.0)
                        let initial = CameraView.LookAt(center + V3d(0.0, -10.0, 3.0), center, V3d.ZAxis)
                        let orbit = Orbit(initial, AVal.constant center)
                        Expect.isTrue (Object.ReferenceEquals(initial, orbit.Down button)) "other button does not activate"
                        Expect.isTrue (Object.ReferenceEquals(initial, orbit.Move(V2i(12, 13)))) "other-button drag does not move"
                        orbit.Down MouseButtons.Left |> ignore
                        let moved = orbit.Move(V2i(-4, 3))
                        viewClose 1e-10 (referenceStep center (V2i(-4, 3)) initial) moved
                        orbit.Up MouseButtons.Left |> ignore
                        Expect.isTrue (Object.ReferenceEquals(moved, orbit.Move(V2i(7, -3)))) "other buttons do not keep orbit active"

                testCase $"adaptive center change" <| fun _ ->
                    for active in [false; true] do
                        let center = cval (V3d(100.0, 200.0, 30.0))
                        let initial = CameraView.LookAt(center.Value + V3d(0.0, -10.0, 3.0), center.Value, V3d.ZAxis)
                        let orbit = Orbit(initial, center)
                        let before = if active then orbit.Down MouseButtons.Left else orbit.Read()
                        let next = V3d(-20.0, 70.0, 5.0)
                        transact (fun () -> center.Value <- next)
                        let changed = orbit.Read()
                        vectorClose 1e-12 before.Location changed.Location "center update does not translate the camera"
                        if active then
                            vectorClose 1e-12 (next - before.Location).Normalized changed.Forward "active center dependency updates facing"
                        else
                            Expect.isTrue (Object.ReferenceEquals(before, changed)) "inactive center change preserves view"
                        let ready = orbit.Down MouseButtons.Left
                        let actual = orbit.Move(V2i(20, 10))
                        viewClose 1e-10 (referenceStep next (V2i(20, 10)) ready) actual
                        checkOrbit next (ready.Location - next).Length initial.Sky actual

                testCase "center rebind retains existing mouse-step initialization" <| fun _ ->
                    let center = cval V3d.Zero
                    let initial = CameraView.LookAt(V3d(2.0, -10.0, 3.0), center.Value, V3d.ZAxis)
                    let orbit = Orbit(initial, center)
                    orbit.Down MouseButtons.Left |> ignore
                    let before = orbit.Move(V2i(4, 2))
                    transact (fun () -> center.Value <- V3d(10.0, 20.0, -3.0))
                    orbit.MoveWithoutRead(V2i(40, -30))
                    let rebound = orbit.Read()
                    // A new step snapshots the current position, rather than replaying a pending delta.
                    vectorClose 1e-12 before.Location rebound.Location "rebind snapshots mouse position"
                    vectorClose 1e-12 (center.Value - before.Location).Normalized rebound.Forward "rebound facing"
                    viewClose 1e-10 (referenceStep center.Value (V2i(5, 3)) rebound) (orbit.Move(V2i(5, 3)))

                testCase "forward is normalized before adding a large world translation" <| fun _ ->
                    let center = V3d(1e12, -1e12, 1e12)
                    let initial = CameraView.LookAt(center + V3d(0.0, -10.0, 3.0), center, V3d.ZAxis)
                    let orbit = Orbit(initial, AVal.constant center)
                    orbit.Down MouseButtons.Left |> ignore
                    let expected = referenceStep center (V2i(20, 10)) initial
                    let actual = orbit.Move(V2i(20, 10))
                    vectorClose 0.001 expected.Location actual.Location "world-space position at representable precision"
                    vectorClose 1e-12 expected.Forward actual.Forward "translation does not lose forward precision"
                    vectorClose 1e-12 expected.Right actual.Right "right precision"
                    vectorClose 1e-12 expected.Up actual.Up "up precision"
                    Expect.equal actual.Sky initial.Sky "sky preserved"

                testCase $"seeded drags" <| fun _ ->
                    for seed in 0 .. 9 do
                        let random = Random(9127 + seed)
                        let center = V3d(random.NextDouble() * 200.0 - 100.0, random.NextDouble() * 200.0 - 100.0, random.NextDouble() * 200.0 - 100.0)
                        let initial = CameraView.LookAt(center + V3d(3.0, -15.0, 7.0), center, V3d.ZAxis)
                        let orbit = Orbit(initial, AVal.constant center)
                        let mutable expected = orbit.Down MouseButtons.Left
                        for _ in 1 .. 100 do
                            let delta = V2i(random.Next(-20, 21), random.Next(-15, 16))
                            expected <- referenceStep center delta expected
                            let actual = orbit.Move delta
                            viewClose 1e-8 expected actual
                            checkOrbit center (initial.Location - center).Length initial.Sky actual
            ]

    [<Tests>]
    let tests =
        testList "Camera" [
            testList "Frustum" [
                Frustum.aspect
                Frustum.fieldOfView
                Frustum.withAspect
                Frustum.withNear
                Frustum.withFieldOfView
            ]
            Orbit.tests
        ]