namespace Aardvark.Rendering.Tests

open System
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Expecto

module ``Cylinder Normal Tests`` =

    module P = IndexedGeometryPrimitives

    type private Parameters =
        { Center : V3d; Axis : V3d; Height : float; Bottom : float; Top : float; Tessellation : int }

    let private color = C4b(13uy, 71uy, 193uy, 109uy)
    let private defaults =
        { Center = V3d.Zero; Axis = V3d.ZAxis; Height = 2.0; Bottom = 2.0; Top = 0.5; Tessellation = 8 }
    let private modes = [IndexedGeometryMode.TriangleList; IndexedGeometryMode.LineList]

    let private build mode p =
        let f = if mode = IndexedGeometryMode.TriangleList then P.Cylinder.solidCylinder else P.Cylinder.wireframeCylinder
        f p.Center p.Axis p.Height p.Bottom p.Top p.Tessellation color

    // Original scalar builder, retained as a compatibility oracle for layout and degenerate inputs.
    // Normal correctness is checked separately against tangents and an analytic slope.
    let private legacy mode p =
        let vertices = List<V3d>()
        let normals = List<V3d>()
        let indices = List<int>()
        let addIndices (values : int[]) = for value in values do indices.Add value
        let n = p.Tessellation
        let axisNormalized = p.Axis.Normalized
        let trafo = Trafo3d.FromNormalFrame(V3d.Zero, axisNormalized)
        for i in 0 .. n - 1 do
            let angle = float i * Constant.PiTimesTwo / float n
            let radial = trafo.Forward.TransformPos(V3d(Fun.Cos angle, Fun.Sin angle, 0.0))
            vertices.Add(radial * p.Top + p.Axis * p.Height)
            normals.Add radial
            vertices.Add(radial * p.Bottom)
            normals.Add radial
            if mode = IndexedGeometryMode.TriangleList then
                addIndices [|i * 2; i * 2 + 1; (i * 2 + 2) % (n * 2);
                             i * 2 + 1; (i * 2 + 3) % (n * 2); (i * 2 + 2) % (n * 2)|]
            else
                addIndices [|i * 2; i * 2 + 1|]
        for i in 0 .. n - 1 do
            vertices.Add vertices.[i * 2]
            normals.Add axisNormalized
            vertices.Add vertices.[i * 2 + 1]
            normals.Add -axisNormalized
            if mode = IndexedGeometryMode.TriangleList then
                addIndices [|i * 2 + n * 2; ((i * 2 + 2) % (n * 2)) + n * 2; n * 4;
                             ((i * 2 + 3) % (n * 2)) + n * 2; i * 2 + 1 + n * 2; n * 4 + 1|]
            else
                addIndices [|i * 2 + n * 2; ((i * 2 + 2) % (n * 2)) + n * 2;
                             ((i * 2 + 2) % (n * 2)) + n * 2; n * 4;
                             ((i * 2 + 3) % (n * 2)) + n * 2; i * 2 + 1 + n * 2;
                             i * 2 + 1 + n * 2; n * 4 + 1|]
        vertices.Add(p.Axis * p.Height)
        normals.Add axisNormalized
        vertices.Add V3d.Zero
        normals.Add -axisNormalized
        let positions = vertices |> Seq.toArray |> Array.map (Trafo3d.Translation(p.Center).Forward.TransformPos >> V3f)
        let normals = normals |> Seq.map V3f |> Seq.toArray
        IndexedGeometry(
            Mode = mode,
            IndexArray = (indices.ToArray() :> Array),
            IndexedAttributes = SymDict.ofList [
                DefaultSemantic.Positions, positions :> Array
                DefaultSemantic.Normals, normals :> Array
                DefaultSemantic.Colors, Array.replicate positions.Length color :> Array
            ])

    let private positions (g : IndexedGeometry) = g.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]
    let private normals (g : IndexedGeometry) = g.IndexedAttributes.[DefaultSemantic.Normals] :?> V3f[]

    let private sameFloat (expected : float32) (actual : float32) message =
        Expect.equal (BitConverter.SingleToInt32Bits actual) (BitConverter.SingleToInt32Bits expected) message
    let private sameVector (expected : V3f) (actual : V3f) message =
        sameFloat expected.X actual.X (message + "/X")
        sameFloat expected.Y actual.Y (message + "/Y")
        sameFloat expected.Z actual.Z (message + "/Z")
    let private close expected actual tolerance message =
        Expect.isTrue (Double.IsFinite actual && abs (actual - expected) <= tolerance)
            $"{message}: expected {expected}, got {actual}"

    let private layout (expected : IndexedGeometry) (actual : IndexedGeometry) =
        Expect.equal actual.Mode expected.Mode "primitive mode"
        Expect.equal actual.VertexCount expected.VertexCount "vertex count"
        Expect.equal actual.FaceVertexCount expected.FaceVertexCount "draw count"
        Expect.equal (actual.IndexArray.GetType()) typeof<int[]> "index element type"
        Expect.sequenceEqual (actual.IndexArray :?> int[]) (expected.IndexArray :?> int[]) "indices and winding"
        Expect.equal actual.IndexedAttributes.Count 3 "attribute count"
        Expect.equal (actual.IndexedAttributes.[DefaultSemantic.Positions].GetType()) typeof<V3f[]> "position element type"
        Expect.equal (actual.IndexedAttributes.[DefaultSemantic.Normals].GetType()) typeof<V3f[]> "normal element type"
        Expect.equal (actual.IndexedAttributes.[DefaultSemantic.Colors].GetType()) typeof<C4b[]> "color element type"
        Expect.equal actual.SingleAttributes expected.SingleAttributes "single attributes"
        Expect.sequenceEqual (actual.IndexedAttributes.[DefaultSemantic.Colors] :?> C4b[])
            (expected.IndexedAttributes.[DefaultSemantic.Colors] :?> C4b[]) "colors"
        Array.iter2 (fun e a -> sameVector e a "unchanged position") (positions expected) (positions actual)

    let private caps p expected actual =
        let before = normals expected
        let after = normals actual
        Expect.equal after.Length before.Length "normal count"
        for i in 2 * max 0 p.Tessellation .. after.Length - 1 do
            sameVector before.[i] after.[i] "unchanged cap normal"

    let private checkRegular mode p =
        let actual = build mode p
        let before = legacy mode p
        layout before actual
        caps p before actual
        let ns = normals actual
        let frame = Trafo3d.FromNormalFrame(V3d.Zero, p.Axis.Normalized)
        let displacement = p.Axis * p.Height
        let axial = displacement.Normalized
        let length = displacement.Length
        let difference = p.Bottom - p.Top
        let slopeAngle = atan2 difference length
        for i in 0 .. p.Tessellation - 1 do
            let angle = float i * Constant.PiTimesTwo / float p.Tessellation
            let radial = frame.Forward.TransformDir(V3d(cos angle, sin angle, 0.0))
            let side = displacement - radial * difference
            let circumferential = Vec.Cross(axial, radial)
            let expected = Vec.Cross(circumferential, side).Normalized
            let n = V3d ns.[i * 2]
            sameVector ns.[i * 2] ns.[i * 2 + 1] "smooth side pair"
            close 1.0 n.Length 2e-6 "unit normal"
            close 0.0 (Vec.Dot(n, side.Normalized)) 2e-6 "perpendicular to side tangent"
            close 0.0 (Vec.Dot(n, circumferential)) 2e-6 "perpendicular to circumferential tangent"
            Expect.isGreaterThan (Vec.Dot(n, radial)) 0.0 "outward radial orientation"
            close 1.0 (Vec.Dot(n, expected)) 2e-6 "cross-product reference"
            close (cos slopeAngle) (Vec.Dot(n, radial)) 2e-6 "analytic radial slope"
            close (sin slopeAngle) (Vec.Dot(n, axial)) 2e-6 "analytic axial slope"
        if p.Bottom = p.Top then
            Array.iter2 (fun e a -> sameVector e a "exact straight-cylinder normal") (normals before) ns

    let tests =
        testList "Cylinder normals" [
            for mode in modes do
                for name, bottom, top in ["straight", 1.5, 1.5; "narrowing", 2.0, 0.5; "widening", 0.5, 2.0; "cone", 1.0, 0.0; "inverted cone", 0.0, 1.0] do
                    for axisName, axis in ["Z", V3d.ZAxis; "X", V3d.XAxis; "rotated", V3d(1.0, 2.0, 3.0).Normalized; "non-unit", V3d(-2.0, 3.0, -4.0)] do
                        for height in [1.0; -0.75] do
                            for tess in [3; 8; 32] do
                                testCase $"{name}/{axisName}/h={height}/tess={tess}/{mode}" <| fun _ ->
                                    checkRegular mode { Center = V3d(13.5, -7.25, 19.0); Axis = axis; Height = height
                                                        Bottom = bottom; Top = top; Tessellation = tess }

            for mode in modes do
                for tess in [1; 2; 7; 256] do
                    testCase $"layout and caps/tess={tess}/{mode}" <| fun _ ->
                        let p = { defaults with Tessellation = tess; Axis = V3d(2.0, -3.0, 4.0); Height = -2.5 }
                        let before = legacy mode p
                        let after = build mode p
                        layout before after
                        caps p before after
                for height in [-3.0; 1.0] do
                    testCase $"translation does not change normals/h={height}/{mode}" <| fun _ ->
                        let p = { defaults with Axis = V3d(1.0, -2.0, 3.0); Height = height }
                        let origin = build mode p
                        let moved = { p with Center = V3d(19.0, 27.0, -123.0) }
                        let translated = build mode moved
                        layout (legacy mode moved) translated
                        Array.iter2 (fun e a -> sameVector e a "translation-invariant normal") (normals origin) (normals translated)

            for mode in modes do
                for name, change in [
                    "zero height", fun p -> { p with Height = 0.0 }
                    "negative zero height", fun p -> { p with Height = -0.0 }
                    "zero axis", fun p -> { p with Axis = V3d.Zero }
                    "tiny axis", fun p -> { p with Axis = V3d(1e-300, -2e-300, 3e-300) }
                    "huge axis", fun p -> { p with Axis = V3d(1e300, -2e300, 3e300) }
                    "negative bottom", fun p -> { p with Bottom = -2.0 }
                    "negative top", fun p -> { p with Top = -0.5 }
                    "negative radii", fun p -> { p with Bottom = -2.0; Top = -0.5 }
                    "zero radii", fun p -> { p with Bottom = 0.0; Top = 0.0 }
                    "NaN bottom", fun p -> { p with Bottom = Double.NaN }
                    "NaN top", fun p -> { p with Top = Double.NaN }
                    "infinite bottom", fun p -> { p with Bottom = Double.PositiveInfinity }
                    "infinite top", fun p -> { p with Top = Double.PositiveInfinity }
                    "equal infinite radii", fun p -> { p with Bottom = Double.PositiveInfinity; Top = Double.PositiveInfinity }
                    "negative infinite radius", fun p -> { p with Bottom = Double.NegativeInfinity }
                    "NaN axis", fun p -> { p with Axis = V3d(Double.NaN, 1.0, 2.0) }
                    "infinite axis", fun p -> { p with Axis = V3d(1.0, 2.0, Double.PositiveInfinity) }
                    "NaN height", fun p -> { p with Height = Double.NaN }
                    "infinite height", fun p -> { p with Height = Double.PositiveInfinity }
                    "negative infinite height", fun p -> { p with Height = Double.NegativeInfinity }
                    "NaN center", fun p -> { p with Center = V3d(Double.NaN, 1.0, 2.0) }
                    "infinite center", fun p -> { p with Center = V3d(1.0, 2.0, Double.PositiveInfinity) }
                    "displacement underflow", fun p -> { p with Axis = V3d(1e-100, 2e-100, 3e-100); Height = 1e-300 }
                    "displacement overflow", fun p -> { p with Axis = V3d(1e100, 2e100, 3e100); Height = 1e300 }
                    "one-sample tessellation", fun p -> { p with Tessellation = 1 }
                    "two-sample tessellation", fun p -> { p with Tessellation = 2 }
                    "empty tessellation", fun p -> { p with Tessellation = 0 }
                    "negative tessellation", fun p -> { p with Tessellation = -4 }
                ] do
                    testCase $"degenerate compatibility/{name}/{mode}" <| fun _ ->
                        let p = change defaults
                        let before = legacy mode p
                        let after = build mode p
                        layout before after
                        Array.iter2 (fun e a -> sameVector e a "unchanged degenerate normal") (normals before) (normals after)

            for mode in modes do
                for name, axis, height, bottom, top, radialWeight, axialWeight in [
                    "huge slope components", V3d.ZAxis, 1e200, 1e200, 0.0, sqrt 0.5, sqrt 0.5
                    "tiny slope components", V3d.ZAxis, 1e-200, 1e-200, 0.0, sqrt 0.5, sqrt 0.5
                    "subnormal slope components", V3d.ZAxis, Double.Epsilon, Double.Epsilon, 0.0, sqrt 0.5, sqrt 0.5
                    "steep limit", V3d.ZAxis, 1e-300, 1e300, 0.0, 0.0, 1.0
                    "shallow limit", V3d.ZAxis, 1e300, 1e-300, 0.0, 1.0, 0.0
                    "large widening", V3d.ZAxis, -1e200, 0.0, 1e200, sqrt 0.5, -sqrt 0.5
                    "length overflow", V3d(1.0, 1.0, 1.0), 1e308, 1e308, 0.0, sqrt 0.75, 0.5
                    "non-unit scale", V3d(0.0, 0.0, 1e150), 1e150, 1e300, 0.0, sqrt 0.5, sqrt 0.5
                    "small non-unit scale", V3d(0.0, 0.0, 1e-150), 1e-150, 1e-300, 0.0, sqrt 0.5, sqrt 0.5
                ] do
                    testCase $"stable coefficients/{name}/{mode}" <| fun _ ->
                        let p = { defaults with Axis = axis; Height = height; Bottom = bottom; Top = top }
                        let before = legacy mode p
                        let after = build mode p
                        layout before after
                        caps p before after
                        let e = (if height < 0.0 then -axis else axis).Normalized
                        let frame = Trafo3d.FromNormalFrame(V3d.Zero, axis.Normalized)
                        for i in 0 .. p.Tessellation - 1 do
                            let angle = float i * Constant.PiTimesTwo / float p.Tessellation
                            let radial = frame.Forward.TransformDir(V3d(cos angle, sin angle, 0.0))
                            let expected = radial * radialWeight + e * axialWeight
                            let actual = V3d (normals after).[i * 2]
                            close 1.0 actual.Length 2e-6 "finite unit normal"
                            close 0.0 (actual - expected).Length 2e-6 "stable analytic limit"

            for mode in modes do
                for tess in [32; 1024] do
                    for name, top in ["straight", 1.0; "tapered", 0.25; "cone", 0.0] do
                        testCase $"normal storage allocation/{name}/tess={tess}/{mode}" <| fun _ ->
                            let p = { defaults with Bottom = 1.0; Top = top; Tessellation = tess }
                            let mutable result = build mode p
                            for _ in 1 .. 5 do result <- build mode p
                            let start = GC.GetAllocatedBytesForCurrentThread()
                            for _ in 1 .. 5 do result <- build mode p
                            let allocated = (GC.GetAllocatedBytesForCurrentThread() - start) / 5L
                            GC.KeepAlive result
                            // Allows the existing position/index builders and geometric list growth,
                            // but not a double-precision normal list plus per-element sequence conversion.
                            Expect.isLessThanOrEqual allocated (1000L * int64 tess + 8192L) "bounded normal-path allocation"

            for seed in 0 .. 9 do
                testCase $"randomized geometry and normals/seed={seed}" <| fun _ ->
                    let random = Random(50173 + seed)
                    let scalar() = 8.0 * random.NextDouble() - 4.0
                    for _ in 1 .. 50 do
                        let axis = V3d(scalar(), scalar(), scalar())
                        let height = (0.05 + abs (scalar())) * (if random.Next(2) = 0 then -1.0 else 1.0)
                        let p = { Center = V3d(scalar(), scalar(), scalar()); Axis = axis; Height = height
                                  Bottom = random.NextDouble() * 4.0; Top = random.NextDouble() * 4.0
                                  Tessellation = random.Next(3, 81) }
                        for mode in modes do checkRegular mode p

            for mode in modes do
                testCase $"cylinder and cone aliases/{mode}" <| fun _ ->
                    let p = { defaults with Axis = V3d(1.0, -2.0, 3.0); Height = -2.0 }
                    let cylinder = if mode = IndexedGeometryMode.TriangleList then P.solidCylinder else P.wireframeCylinder
                    let actual = cylinder p.Center p.Axis p.Height p.Bottom p.Top p.Tessellation color
                    let direct = build mode p
                    layout direct actual
                    Array.iter2 (fun e a -> sameVector e a "cylinder alias") (normals direct) (normals actual)
                    let cone = if mode = IndexedGeometryMode.TriangleList then P.Cone.solidCone else P.Cone.wireframeCone
                    let alias = if mode = IndexedGeometryMode.TriangleList then P.solidCone else P.wireframeCone
                    let expected = build mode { p with Top = 0.0 }
                    for actual in [cone p.Center p.Axis p.Height p.Bottom p.Tessellation color
                                   alias p.Center p.Axis p.Height p.Bottom p.Tessellation color] do
                        layout expected actual
                        Array.iter2 (fun e a -> sameVector e a "cone shares cylinder normals") (normals expected) (normals actual)

            for tess in [3; 8; 32] do
                testCase $"arrow inherits cone correction/tess={tess}" <| fun _ ->
                    let axis = V3d(1.0, 2.0, -3.0)
                    let center = V3d(5.0, -9.0, 7.0)
                    let shaft = { defaults with Center = center; Axis = axis; Height = 1.5; Bottom = 0.1; Top = 0.1; Tessellation = tess }
                    let head = { shaft with Center = center + axis * shaft.Height; Height = 0.25; Bottom = 0.1 + 0.2; Top = 0.0 }
                    let oldHead = legacy IndexedGeometryMode.TriangleList head
                    let headColor = C4b(211uy, 31uy, 79uy, 241uy)
                    oldHead.IndexedAttributes.[DefaultSemantic.Colors] <- Array.replicate oldHead.VertexCount headColor
                    let before = IndexedGeometry.union oldHead (legacy IndexedGeometryMode.TriangleList shaft)
                    let actual = P.arrow center axis shaft.Height shaft.Bottom color head.Height 0.2 headColor tess
                    layout before actual
                    let corrected = IndexedGeometry.union (build IndexedGeometryMode.TriangleList head) (build IndexedGeometryMode.TriangleList shaft)
                    Array.iter2 (fun e a -> sameVector e a "arrow head and shaft normals") (normals corrected) (normals actual)
                    Expect.isTrue ((normals actual).[0] <> (normals before).[0]) "head slope changes"
        ]
