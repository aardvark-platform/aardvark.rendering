namespace Aardvark.Rendering.Tests.Other

open System
open Aardvark.Base
open Aardvark.Rendering.Text
open Aardvark.Rendering.Tests
open Expecto

module ShapeListTransforms =

    type private Operation = Translate of V2d | Scale of V2d

    let private inputs () =
        let first = ConcreteShape.fillRectangle C4b.Red (Box2d(-2.0, -1.0, 3.0, 4.0))
        let second = ConcreteShape.fillRectangle C4b.Blue (Box2d(5.0, 2.0, 8.0, 6.0))
        let populated =
            ShapeList.ofList [ { first with z = -8; trafo = M33d.Rotation(0.25) }
                               { second with z = 19; trafo = M33d.Translation(3.0, -7.0) } ]
        let layouts =
            [ "empty", ShapeList.empty
              "populated", { populated with textBounds = Box2d(-11.0, -13.0, 17.0, 19.0); zRange = Range1i(-31, 97) }
              "metadata-only", { ShapeList.empty with bounds = Box2d(-3.0, -2.0, 5.0, 7.0)
                                                      textBounds = Box2d(-11.0, -13.0, 17.0, 19.0)
                                                      zRange = Range1i(-31, 97) } ]
        let frames =
            [ "identity", Trafo3d.Identity
              "noncommuting", Trafo3d.RotationZ(0.37) * Trafo3d.Scale(1.25, 0.75, 2.0) * Trafo3d.Translation(3.0, -5.0, 7.0) ]
        [ for style in [RenderStyle.Normal; RenderStyle.Billboard; RenderStyle.NoBoundary] do
            for flag in [false; true] do
                for layout, input in layouts do
                    for frame, trafo in frames do
                        let label = $"style={style}, flip={flag}, layout={layout}, frame={frame}"
                        yield label, { input with renderStyle = style; flipViewDependent = flag; renderTrafo = trafo } ]

    let private doubleBits context actual expected =
        Expect.equal (BitConverter.DoubleToInt64Bits actual) (BitConverter.DoubleToInt64Bits expected) context

    let private matrix context (actual : M44d) (expected : M44d) =
        for row in 0 .. 3 do
            for col in 0 .. 3 do
                doubleBits $"{context}[{row},{col}]" actual.[row, col] expected.[row, col]

    let private bounds context (actual : Box2d) (expected : Box2d) =
        doubleBits (context + " min x") actual.Min.X expected.Min.X
        doubleBits (context + " min y") actual.Min.Y expected.Min.Y
        doubleBits (context + " max x") actual.Max.X expected.Max.X
        doubleBits (context + " max y") actual.Max.Y expected.Max.Y

    let private state context (actual : ShapeList) (expected : ShapeList) =
        Expect.equal actual.renderStyle expected.renderStyle (context + ": render style")
        Expect.equal actual.flipViewDependent expected.flipViewDependent (context + ": view flag")
        Expect.equal actual.zRange expected.zRange (context + ": z range")
        bounds (context + ": bounds") actual.bounds expected.bounds
        bounds (context + ": text bounds") actual.textBounds expected.textBounds
        matrix (context + ": forward") actual.renderTrafo.Forward expected.renderTrafo.Forward
        matrix (context + ": backward") actual.renderTrafo.Backward expected.renderTrafo.Backward
        Expect.equal actual.concreteShapes.Length expected.concreteShapes.Length (context + ": shape count")
        List.iter2 (fun (a : ConcreteShape) (b : ConcreteShape) ->
            Expect.isTrue (obj.ReferenceEquals(a.shape, b.shape)) (context + ": shape identity")
            Expect.equal a.color b.color (context + ": shape color")
            Expect.equal a.z b.z (context + ": shape z")
            for row in 0 .. 2 do
                for col in 0 .. 2 do
                    doubleBits $"{context}: shape transform[{row},{col}]" a.trafo.[row, col] b.trafo.[row, col]
        ) actual.concreteShapes expected.concreteShapes

    let private expected operation (input : ShapeList) =
        match operation with
        | Translate shift ->
            { input with bounds = input.bounds.Translated shift
                         textBounds = input.textBounds.Translated shift
                         renderTrafo = input.renderTrafo * Trafo3d.Translation(V3d(shift, 0.0)) }
        | Scale factor ->
            // Preserve the existing component-wise corner calculation, including zero/negative scales.
            { input with bounds = Box2d(input.bounds.Min * factor, input.bounds.Max * factor)
                         textBounds = Box2d(input.textBounds.Min * factor, input.textBounds.Max * factor)
                         renderTrafo = input.renderTrafo * Trafo3d.Scale(V3d(factor, 1.0)) }

    let private step context operation (input : ShapeList) =
        let originalList = input.concreteShapes
        let snapshot = { input with concreteShapes = List.map id input.concreteShapes }
        let wanted = expected operation snapshot
        let actual =
            match operation with
            | Translate shift -> ShapeList.translate shift input
            | Scale factor -> ShapeList.scale factor input
        state context actual wanted
        state (context + ": immutable input") input snapshot
        Expect.isFalse (obj.ReferenceEquals(actual, input)) (context + ": fresh result identity, including identity transforms")
        Expect.isTrue (obj.ReferenceEquals(actual.concreteShapes, originalList)) (context + ": concrete list identity")
        Expect.isTrue (obj.ReferenceEquals(input.concreteShapes, originalList)) (context + ": input list identity")
        actual

    module private Cases =
        let translation () =
            for context, input in inputs() do
                for shift in [V2d.Zero; V2d(2.0, -3.0); V2d(-0.25, 0.5)] do
                    step $"{context}, shift={shift}" (Translate shift) input |> ignore

        let scaling () =
            for context, input in inputs() do
                for factor in [V2d.II; V2d(2.0, 0.5); V2d(-2.0, 0.5); V2d(1.0, -1.0); V2d.Zero] do
                    step $"{context}, scale={factor}" (Scale factor) input |> ignore

        let chains () =
            let sequences =
                [ [Translate V2d.Zero; Scale V2d.II; Translate(V2d(3.0, -5.0)); Scale(V2d(2.0, 0.5)); Translate(V2d(-7.0, 4.0))]
                  [Scale(V2d(-2.0, 0.5)); Translate(V2d(3.0, -5.0)); Scale(V2d(1.0, -3.0)); Scale(V2d(0.5, 2.0))] ]
            for context, input in inputs() do
                for operations in sequences do
                    let mutable actual = input
                    let mutable wanted = input
                    for index, operation in List.indexed operations do
                        let label = $"{context}, chain={operations}, step={index}, operation={operation}"
                        wanted <- expected operation wanted
                        actual <- step label operation actual
                        state (label + ": composed result") actual wanted

    let tests (target : TestTarget) =
        [
            "Translation retains rendering mode and metadata", Cases.translation
            "Scaling retains rendering mode and metadata",     Cases.scaling
            "Chained transformations retain rendering mode",   Cases.chains
        ]
        |> prepareCasesCpu "ShapeList.Transforms" target
