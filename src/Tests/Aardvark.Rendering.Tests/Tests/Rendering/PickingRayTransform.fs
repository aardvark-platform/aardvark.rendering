namespace Aardvark.Rendering.Tests.Rendering

open System
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.SceneGraph
open Aardvark.Rendering.Tests
open Expecto

module PickingRayTransform =

    module private Cases =
        let private part (ray : Ray3d) lo hi = RayPart(FastRay3d ray, lo, hi)

        let private shapes (center : V3d) (axis : int) =
            let n, u, v =
                match axis with
                | 0 -> V3d.XAxis, V3d.YAxis, V3d.ZAxis
                | 1 -> V3d.YAxis, V3d.ZAxis, V3d.XAxis
                | _ -> V3d.ZAxis, V3d.XAxis, V3d.YAxis
            let c = center + 32.0 * n
            let box = Box3d(c - V3d(8.0), c + V3d(8.0))
            let tri = Triangle3d(c - 16.0 * u - 16.0 * v, c + 16.0 * u - 16.0 * v, c + 16.0 * v)
            [|
                "box", PickShape.Box box, (fun p -> RayPart.Intersects(p, box))
                "triangle", PickShape.Triangle tri, (fun p -> RayPart.Intersects(p, tri))
                "custom", PickShape.Custom(box, fun p -> RayPart.Intersects(p, box)), (fun p -> RayPart.Intersects(p, box))
            |], n, u

        let private sameHit expected actual =
            match expected, actual with
            | None, None -> true
            | Some e, Some a ->
                BitConverter.DoubleToInt64Bits e = BitConverter.DoubleToInt64Bits a ||
                (Double.IsFinite e && Double.IsFinite a && abs (e - a) <= 1E-10 * max 1.0 (abs e))
            | _ -> false

        let private check context expected (ray : RayPart) (pickable : Pickable) =
            let a = Pickable.intersect ray pickable
            let b = Pickable.intersectV ray pickable |> ValueOption.toOption
            Expect.isTrue (sameHit expected a && sameHit expected b)
                $"{context}: direct={expected}, intersect={a}, intersectV={b}"

        let private rays (center : V3d) (n : V3d) (u : V3d) (speed : float) =
            [|
                "hit", Ray3d(center, speed * n), 0.0, 64.0 / speed, true
                "finite hit", Ray3d(center, speed * n), 16.0 / speed, 48.0 / speed, true
                "before shape", Ray3d(center, speed * n), 0.0, 16.0 / speed, false
                "after shape", Ray3d(center, speed * n), 48.0 / speed, 64.0 / speed, false
                "side miss", Ray3d(center + 64.0 * u, speed * n), 0.0, 64.0 / speed, false
                "away miss", Ray3d(center, -speed * n), 0.0, 64.0 / speed, false
            |]

        let largeIdentity () =
            for magnitude in [1E16; -1E16; 18014398509481984.0; -18014398509481984.0] do
                for axis in 0 .. 2 do
                    let center = magnitude * [| V3d.XAxis; V3d.YAxis; V3d.ZAxis |].[axis]
                    let fixtures, n, u = shapes center axis
                    for speed in [0.5; 1.0; 2.0; 4.0] do
                        for label, ray, lo, hi, hit in rays center n u speed do
                            let query = part ray lo hi
                            for name, shape, direct in fixtures do
                                let context = $"identity, magnitude={magnitude}, axis={axis}, speed={speed}, {label}, {name}"
                                let expected = direct query
                                Expect.equal expected.IsSome hit (context + ": direct fixture")
                                check context expected query (Pickable.ofShape shape)

        let private affineFrames =
            let quarterTurn = M44d(0.0, -1.0, 0.0, 0.0,
                                   1.0,  0.0, 0.0, 0.0,
                                   0.0,  0.0, 1.0, 0.0,
                                   0.0,  0.0, 0.0, 1.0)
            [|
                "identity", Trafo3d.Identity
                "rotation", Trafo3d(quarterTurn, quarterTurn.Transposed)
                "reflection", Trafo3d.Scale(-1.0, 1.0, 1.0)
                "nonuniform scale", Trafo3d.Scale(2.0, 0.5, 4.0)
                "rotated reflected scale", Trafo3d.Scale(-2.0, 4.0, 0.5) * Trafo3d(quarterTurn, quarterTurn.Transposed)
            |]

        let private affineMatrix center (frames : (string * Trafo3d)[]) (translations : (string * V3d)[]) =
            for axis in 0 .. 2 do
                let fixtures, n, u = shapes center axis
                for frameName, frame in frames do
                    for translationName, translation in translations do
                        let trafo = frame * Trafo3d.Translation translation
                        for speed in [0.5; 1.0; 2.0; 4.0] do
                            for label, ray, lo, hi, hit in rays center n u speed do
                                let local = part ray lo hi
                                let world = Ray3d(trafo.Forward.TransformPosProj ray.Origin, trafo.Forward.TransformDir ray.Direction)
                                let query = part world lo hi
                                for name, shape, direct in fixtures do
                                    let context = $"center={center}, {frameName}, {translationName}, axis={axis}, speed={speed}, {label}, {name}"
                                    let expected = direct local
                                    Expect.equal expected.IsSome hit (context + ": direct fixture")
                                    check context expected query { trafo = trafo; shape = shape }

        let translatedGeometry () =
            let large = V3d(18014398509481984.0)
            affineMatrix V3d.Zero affineFrames [| "large world", large; "negative large world", -large |]
            // Conversely, small world coordinates may transform into large local ones.
            affineMatrix large [| "identity", Trafo3d.Identity |] [| "large local", -large |]
            affineMatrix -large [| "identity", Trafo3d.Identity |] [| "negative large local", large |]

        let ordinaryCoordinates () =
            let frames =
                Array.append affineFrames [|
                    "oblique rotation", Trafo3d.RotationEuler(V3d(0.37, -0.71, 1.13))
                    "oblique reflected scale", Trafo3d.Scale(-2.0, 0.5, 4.0) * Trafo3d.RotationEuler(V3d(0.37, -0.71, 1.13))
                |]
            for center in [V3d.Zero; V3d(16.0, -8.0, 4.0)] do
                affineMatrix center frames [| "untranslated", V3d.Zero; "translated", V3d(128.0, -64.0, 32.0) |]

        let private checkVector context (expected : V3d) (actual : V3d) =
            for e, a in [expected.X, actual.X; expected.Y, actual.Y; expected.Z, actual.Z] do
                Expect.equal (BitConverter.DoubleToInt64Bits a) (BitConverter.DoubleToInt64Bits e) context

        let projectiveCompatibility () =
            let matrices =
                [|
                    for axis in 0 .. 2 do
                        for value in [Double.Epsilon; 0.015625; -0.015625] do
                            let mutable m = M44d.Identity
                            m.[3, axis] <- value
                            yield $"bottom column={axis}, value={value}", m
                    for w in [0.5; 2.0; Math.BitIncrement 1.0; Math.BitDecrement 1.0] do
                        let mutable m = M44d.Identity
                        m.M33 <- w
                        yield $"bottom w={w}", m
                |]
            let fixtures, _, _ = shapes V3d.Zero 2
            for matrixName, m in matrices do
                let trafo = Trafo3d(m.Inverse, m)
                for origin in [V3d(0.0, 0.0, 4.0); V3d(64.0, 0.0, 4.0); V3d(1E16)] do
                    for speed in [0.5; 2.0] do
                        for lo, hi in [0.0, 128.0; 1.0, 2.0] do
                            let ray = Ray3d(origin, speed * V3d.ZAxis)
                            let query = part ray lo hi
                            let o = m.TransformPosProj origin
                            let d = m.TransformPosProj (origin + ray.Direction) - o
                            let local = part (Ray3d(o, d)) lo hi
                            let context = $"{matrixName}, origin={origin}, speed={speed}, interval={lo}/{hi}"
                            for name, shape, direct in fixtures do
                                let expected =
                                    direct local |> Option.map (fun t ->
                                        let world = trafo.Forward.TransformPosProj (local.Ray.Ray.GetPointOnRay t)
                                        Vec.dot (world - origin) ray.Direction / Vec.lengthSquared ray.Direction)
                                check (context + ", " + name) expected query { trafo = trafo; shape = shape }
                            // Observe the exact fallback ray, including nearly affine matrices.
                            let mutable calls = 0
                            let observe (actual : RayPart) =
                                calls <- calls + 1
                                checkVector (context + ": origin") o actual.Ray.Ray.Origin
                                checkVector (context + ": direction") d actual.Ray.Ray.Direction
                                Expect.equal (actual.TMin, actual.TMax) (lo, hi) (context + ": interval")
                                None
                            check context None query { trafo = trafo; shape = PickShape.Custom(Box3d.Unit, observe) }
                            Expect.equal calls 2 (context + ": callback per API")

    let tests (target : TestTarget) =
        [
            "Large-coordinate identity intersections", Cases.largeIdentity
            "Translated affine intersections",        Cases.translatedGeometry
            "Ordinary affine intersections",          Cases.ordinaryCoordinates
            "Projective calculation compatibility",   Cases.projectiveCompatibility
        ]
        |> prepareCasesCpu "Picking ray transform" target
