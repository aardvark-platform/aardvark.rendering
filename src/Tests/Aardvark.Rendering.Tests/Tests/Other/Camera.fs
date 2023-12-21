namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Aardvark.Rendering
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

    [<Tests>]
    let tests =
        testList "Camera" [
            testList "Frustum" [
                aspect
                fieldOfView
                withAspect
                withNear
                withFieldOfView
            ]
        ]