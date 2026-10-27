namespace Aardvark.Rendering.Tests.IndexedGeometry

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Expecto

module Torus =

    module private Cases =
        let private color = C4b(13uy, 71uy, 193uy, 109uy)
        let private standard = Torus3d(V3d.Zero, V3d.ZAxis, 2.0, 0.5)
        let private builders =
            [
                "solid", IndexedGeometryMode.TriangleList, IndexedGeometryPrimitives.Torus.solidTorus
                "solid alias", IndexedGeometryMode.TriangleList, IndexedGeometryPrimitives.solidTorus
                "wire", IndexedGeometryMode.LineList, IndexedGeometryPrimitives.Torus.wireframeTorus
                "wire alias", IndexedGeometryMode.LineList, IndexedGeometryPrimitives.wireframeTorus
            ]

        // Sample each cell independently rather than copying the production grid/list algorithm.
        let private expected (torus : Torus3d) major minor mode =
            let circle = torus.GetMajorCircle()
            let point i j =
                let majorPoint = circle.GetPoint(float i / float major * Constant.PiTimesTwo)
                let radial = (majorPoint - torus.Position).Normalized * torus.MinorRadius
                let axial = torus.Direction.Normalized * torus.MinorRadius
                let angle = float j / float minor * Constant.PiTimesTwo
                majorPoint + radial * cos angle + axial * sin angle
            let cells = max 0 major * max 0 minor
            let positions = Array.zeroCreate<V3f> (4 * cells)
            let normals = Array.zeroCreate<V3f> (4 * cells)
            let pattern = if mode = IndexedGeometryMode.TriangleList then [|0; 1; 2; 0; 2; 3|] else [|0; 1; 1; 2; 2; 3; 3; 0|]
            let indices = Array.init (pattern.Length * cells) (fun k -> 4 * (k / pattern.Length) + pattern.[k % pattern.Length])
            for cell in 0 .. cells - 1 do
                let i, j = cell / minor + 1, cell % minor + 1
                let p0, p1, p2, p3 = point i j, point i (j - 1), point (i - 1) (j - 1), point (i - 1) j
                let corners = [|p0; p1; p2; p3|]
                let normal = V3f(Vec.Cross(p1 - p0, p2 - p0).Normalized)
                for corner in 0 .. 3 do
                    positions.[4 * cell + corner] <- V3f corners.[corner]
                    normals.[4 * cell + corner] <- normal
            indices, positions, normals

        let private sameVector context (expected : V3f) (actual : V3f) =
            for axis, e, a in ["X", expected.X, actual.X; "Y", expected.Y, actual.Y; "Z", expected.Z, actual.Z] do
                Expect.equal (BitConverter.SingleToInt32Bits a) (BitConverter.SingleToInt32Bits e) $"{context}/{axis}: float bits"

        let private check context torus major minor mode chosenColor (geometry : IndexedGeometry) =
            let indices, positions, normals = expected torus major minor mode
            Expect.equal geometry.Mode mode $"{context}: primitive mode"
            Expect.equal geometry.IndexedAttributes.Count 3 $"{context}: complete attribute set"
            Expect.equal (geometry.IndexArray.GetType()) typeof<int[]> $"{context}: index type"
            Expect.sequenceEqual (geometry.IndexArray :?> int[]) indices $"{context}: complete index ordering and winding"
            Expect.equal geometry.VertexCount positions.Length $"{context}: vertex count"
            Expect.equal geometry.FaceVertexCount indices.Length $"{context}: draw count"
            Expect.isTrue (isNull geometry.SingleAttributes) $"{context}: single attributes"
            let actualPositions = geometry.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]
            let actualNormals = geometry.IndexedAttributes.[DefaultSemantic.Normals] :?> V3f[]
            let actualColors = geometry.IndexedAttributes.[DefaultSemantic.Colors] :?> C4b[]
            Expect.equal actualPositions.Length positions.Length $"{context}: position count"
            Expect.equal actualNormals.Length normals.Length $"{context}: normal count"
            Expect.sequenceEqual actualColors (Array.replicate positions.Length chosenColor) $"{context}: colors"
            for i in 0 .. positions.Length - 1 do
                sameVector $"{context}, position={i}" positions.[i] actualPositions.[i]
                sameVector $"{context}, normal={i}" normals.[i] actualNormals.[i]
            for index in indices do Expect.isTrue (index >= 0 && index < positions.Length) $"{context}: index bounds"

        let layout () =
            let tori =
                [standard
                 Torus3d(V3d(13.5, -7.25, 19.0), V3d.XAxis, 3.0, 0.75)
                 Torus3d(V3d(-8.0, 2.0, 0.125), V3d(1.0, -2.0, 3.0).Normalized, 0.5, 0.125)
                 Torus3d(V3d(16777216.5, -16777216.25, 5.0), V3d(-2.0, 3.0, -4.0), 2.0, 0.5)]
            for name, mode, build in builders do
                for torus in tori do
                    for major, minor in [1, 1; 1, 3; 2, 1; 2, 2; 3, 2; 4, 7; 7, 4; 8, 8] do
                        for chosenColor in [color; C4b(0uy, 255uy, 17uy, 0uy)] do
                            check $"{name}, torus={torus}, tess={major}/{minor}, color={chosenColor}" torus major minor mode chosenColor (build torus chosenColor major minor)
                let first = build standard color 4 3
                let second = build standard color 4 3
                for semantic in [DefaultSemantic.Positions; DefaultSemantic.Normals; DefaultSemantic.Colors] do
                    Expect.isFalse (obj.ReferenceEquals(first.IndexedAttributes.[semantic], second.IndexedAttributes.[semantic])) $"{name}: attribute arrays are independent"
                Expect.isFalse (obj.ReferenceEquals(first.IndexArray, second.IndexArray)) $"{name}: index arrays are independent"
                (first.IndexArray :?> int[]).[0] <- -1
                (first.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]).[0] <- V3f.Zero
                (first.IndexedAttributes.[DefaultSemantic.Normals] :?> V3f[]).[0] <- V3f.Zero
                (first.IndexedAttributes.[DefaultSemantic.Colors] :?> C4b[]).[0] <- C4b.Black
                check (name + " independent output") standard 4 3 mode color second

        let degenerate () =
            let tori =
                [Torus3d(V3d.Zero, V3d.ZAxis, 0.0, 0.0)
                 Torus3d(V3d(1.0, 2.0, 3.0), V3d.ZAxis, 2.0, 0.0)
                 Torus3d(V3d.Zero, V3d.ZAxis, 0.0, 0.5)
                 Torus3d(V3d.Zero, V3d.ZAxis, -2.0, 0.5)
                 Torus3d(V3d.Zero, V3d.ZAxis, 2.0, -0.5)
                 Torus3d(V3d.Zero, V3d.ZAxis, 0.5, 2.0)
                 Torus3d(V3d.Zero, V3d.Zero, 2.0, 0.5)
                 Torus3d(V3d.Zero, V3d.ZAxis, Double.NaN, 0.5)
                 Torus3d(V3d.Zero, V3d.ZAxis, 2.0, Double.PositiveInfinity)]
            for name, mode, build in builders do
                for torus in tori do
                    for major, minor in [1, 1; 2, 1; 3, 2; 7, 5] do
                        check $"{name}, degenerate={torus}, tess={major}/{minor}" torus major minor mode color (build torus color major minor)
                for major, minor in [0, 0; 0, 7; 7, 0; -1, 7; 7, -1; -3, -2; Int32.MinValue, 2; 2, Int32.MinValue; -1, Int32.MaxValue] do
                    check $"{name}, empty tess={major}/{minor}" standard major minor mode color (build standard color major minor)

        let allocation () =
            for name, mode, build in builders do
                for major, minor in [8, 4; 32, 16; 64, 32] do
                    for _ in 1 .. 3 do build standard color major minor |> GC.KeepAlive
                    let cells = int64 major * int64 minor
                    let payload = cells * (if mode = IndexedGeometryMode.TriangleList then 136L else 144L)
                    let pointGrid = int64 (major + 1) * int64 (minor + 1) * 24L
                    let budget = payload + pointGrid + cells * 24L + 65536L
                    for repeat in 1 .. 2 do
                        let before = GC.GetAllocatedBytesForCurrentThread()
                        let geometry = build standard color major minor
                        let allocated = GC.GetAllocatedBytesForCurrentThread() - before
                        GC.KeepAlive geometry
                        Expect.isLessThanOrEqual allocated budget $"{name}, tess={major}/{minor}, repeat={repeat}: construction storage, payload={payload}, grid={pointGrid}"

        let checkedCounts () =
            // The original would attempt enormous grids: never run this case in original-source sensitivity checks.
            for name, _, build in builders do
                for major, minor in [Int32.MaxValue, Int32.MaxValue; 536870912, 1; 400000000, 1] do
                    let before = GC.GetAllocatedBytesForCurrentThread()
                    Expect.throwsT<OverflowException> (fun () -> build standard color major minor |> ignore) $"{name}, tess={major}/{minor}: checked output count"
                    let allocated = GC.GetAllocatedBytesForCurrentThread() - before
                    Expect.isLessThan allocated 1048576L $"{name}, tess={major}/{minor}: rejected before grid allocation"

    let tests (target : TestTarget) =
        [
            "Complete solid and wire layouts",          Cases.layout
            "Empty and degenerate compatibility",       Cases.degenerate
            "Construction storage is proportional",     Cases.allocation
            "Output counts reject overflow safely",     Cases.checkedCounts
        ]
        |> prepareCasesCpu "Torus" target
