namespace Aardvark.Rendering.Tests

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Expecto

module ``TraceGeometry Tests`` =

    let private bufferArray (buffer : IBuffer) =
        match buffer with
        | :? ArrayBuffer as buffer -> buffer.Data
        | _ -> failtestf "Expected ArrayBuffer but got %s" (buffer.GetType().FullName)

    let private expectOutOfRange (parameter : string) (action : unit -> unit) =
        let exceptionValue =
            try
                action()
                failtestf "Expected ArgumentOutOfRangeException for '%s'" parameter
            with
            | :? ArgumentOutOfRangeException as exceptionValue -> exceptionValue

        Expect.equal exceptionValue.ParamName parameter "Exception parameter"
        exceptionValue

    let private vertices =
        [|
            V3f(1.0f, 2.0f, 3.0f)
            V3f(4.0f, 5.0f, 6.0f)
            V3f(7.0f, 8.0f, 9.0f)
            V3f(10.0f, 11.0f, 12.0f)
        |]

    let private indices = [| 3; 1; 4; 1 |]

    let private boxesF =
        [|
            Box3f(V3f(0.0f, 1.0f, 2.0f), V3f(3.0f, 4.0f, 5.0f))
            Box3f(V3f(6.0f, 7.0f, 8.0f), V3f(9.0f, 10.0f, 11.0f))
            Box3f(V3f(12.0f, 13.0f, 14.0f), V3f(15.0f, 16.0f, 17.0f))
            Box3f(V3f(18.0f, 19.0f, 20.0f), V3f(21.0f, 22.0f, 23.0f))
        |]

    let private boxesD =
        [|
            Box3d(V3d(0.0, 1.0, 2.0), V3d(3.0, 4.0, 5.0))
            Box3d(V3d(6.0, 7.0, 8.0), V3d(9.0, 10.0, 11.0))
            Box3d(V3d(12.0, 13.0, 14.0), V3d(15.0, 16.0, 17.0))
            Box3d(V3d(18.0, 19.0, 20.0), V3d(21.0, 22.0, 23.0))
        |]

    let private normalAndDefaultRanges() =
        let allVertices = VertexData(vertices)
        Expect.equal allVertices.Count 4u "Default vertex count"
        Expect.equal allVertices.Offset 0UL "Default vertex offset"
        Expect.equal allVertices.Stride 12UL "Vertex stride"
        Expect.isTrue (Object.ReferenceEquals(bufferArray allVertices.Buffer, vertices)) "Vertex storage"

        let remainingVertices = VertexData(vertices, 1, -1)
        Expect.equal remainingVertices.Count 3u "Remaining vertex count"
        Expect.equal remainingVertices.Offset 12UL "Remaining vertex offset"

        let selectedIndices = IndexData(indices, 1, 2)
        Expect.equal selectedIndices.Type IndexType.Int32 "Index type"
        Expect.equal selectedIndices.Count 2u "Selected index count"
        Expect.equal selectedIndices.Offset 4UL "Selected index offset"
        Expect.isTrue (Object.ReferenceEquals(bufferArray selectedIndices.Buffer, indices)) "Index storage"

        let allIndices = IndexData(indices)
        Expect.equal allIndices.Count 4u "Default index count"
        Expect.equal allIndices.Offset 0UL "Default index offset"

        let selectedBoxes = BoundingBoxes(boxesF, 1, 2, GeometryFlags.Opaque)
        Expect.equal selectedBoxes.Count 2u "Selected Box3f count"
        Expect.equal selectedBoxes.Data.Offset 24UL "Selected Box3f offset"
        Expect.equal selectedBoxes.Data.Stride 24UL "Box3f stride"
        Expect.equal selectedBoxes.Flags GeometryFlags.Opaque "Box3f flags"
        Expect.isTrue (Object.ReferenceEquals(bufferArray selectedBoxes.Data.Buffer, boxesF)) "Box3f storage"

        let remainingBoxes = BoundingBoxes(boxesF, 1, -1)
        Expect.equal remainingBoxes.Count 3u "Remaining Box3f count"
        Expect.equal remainingBoxes.Data.Offset 24UL "Remaining Box3f offset"

    let private exactEndEmptyRanges() =
        for count in [0; -1] do
            let vertexData = VertexData(vertices, vertices.Length, count)
            Expect.equal vertexData.Count 0u "Exact-end vertex count"
            Expect.equal vertexData.Offset 48UL "Exact-end vertex offset"

            let indexData = IndexData(indices, indices.Length, count)
            Expect.equal indexData.Count 0u "Exact-end index count"
            Expect.equal indexData.Offset 16UL "Exact-end index offset"

            let floatBoxes = BoundingBoxes(boxesF, boxesF.Length, count)
            Expect.equal floatBoxes.Count 0u "Exact-end Box3f count"
            Expect.equal floatBoxes.Data.Offset 96UL "Exact-end Box3f offset"

            let doubleBoxes = BoundingBoxes(boxesD, boxesD.Length, count)
            Expect.equal doubleBoxes.Count 0u "Exact-end Box3d count"
            Expect.equal doubleBoxes.Data.Offset 0UL "Compact exact-end Box3d offset"
            Expect.equal (bufferArray doubleBoxes.Data.Buffer).Length 0 "Compact exact-end Box3d storage"

    let private startValidationPrecedesCount() =
        let cases =
            [
                fun () -> VertexData(vertices, -1, -2) |> ignore
                fun () -> IndexData(indices, indices.Length + 1, -2) |> ignore
                fun () -> BoundingBoxes(boxesF, -1, -2) |> ignore
                fun () -> BoundingBoxes(boxesD, boxesD.Length + 1, -2) |> ignore
            ]

        for action in cases do
            expectOutOfRange "startIndex" action |> ignore

    let private invalidNegativeCounts() =
        for count in [-2; Int32.MinValue] do
            let cases =
                [
                    fun () -> VertexData(vertices, 0, count) |> ignore
                    fun () -> IndexData(indices, 0, count) |> ignore
                    fun () -> BoundingBoxes(boxesF, 0, count) |> ignore
                    fun () -> BoundingBoxes(boxesD, 0, count) |> ignore
                ]

            for action in cases do
                let exceptionValue = expectOutOfRange "count" action
                Expect.stringContains exceptionValue.Message (string count) "The diagnostic must retain the invalid count"

    let private overflowingCounts() =
        let vertices = vertices.[0 .. 1]
        let indices = indices.[0 .. 1]
        let boxesF = boxesF.[0 .. 1]
        let boxesD = boxesD.[0 .. 1]
        let cases =
            [
                fun () -> VertexData(vertices, 1, Int32.MaxValue) |> ignore
                fun () -> IndexData(indices, 1, Int32.MaxValue) |> ignore
                fun () -> BoundingBoxes(boxesF, 1, Int32.MaxValue) |> ignore
                fun () -> BoundingBoxes(boxesD, 1, Int32.MaxValue) |> ignore
            ]

        for action in cases do
            let exceptionValue = expectOutOfRange "count" action
            Expect.stringContains exceptionValue.Message (string Int32.MaxValue) "The diagnostic must not overflow the count"
            Expect.stringContains exceptionValue.Message "starting at 1" "The diagnostic must retain the start index"

    let private compactDoubleSubrange() =
        let selected = BoundingBoxes(boxesD, 1, 2, GeometryFlags.IgnoreDuplicateHits)
        let storage = bufferArray selected.Data.Buffer :?> Box3f[]
        let expected = [| Box3f boxesD.[1]; Box3f boxesD.[2] |]

        Expect.equal selected.Count 2u "Selected Box3d count"
        Expect.equal selected.Data.Offset 0UL "Compact Box3d offset"
        Expect.equal selected.Data.Stride 24UL "Compact Box3d stride"
        Expect.equal selected.Flags GeometryFlags.IgnoreDuplicateHits "Box3d flags"
        Expect.equal storage.Length 2 "Compact Box3d storage length"
        Expect.equal storage expected "Compact Box3d contents"

        let all = BoundingBoxes(boxesD)
        let allStorage = bufferArray all.Data.Buffer :?> Box3f[]
        Expect.equal all.Count 4u "Default Box3d count"
        Expect.equal all.Data.Offset 0UL "Default Box3d offset"
        Expect.equal allStorage.Length boxesD.Length "Default Box3d storage length"
        Expect.equal allStorage (boxesD |> Array.map Box3f) "Default Box3d contents"

    [<Tests>]
    let tests =
        testList "Raytracing.TraceGeometry" [
            testCase "Normal and default array ranges" normalAndDefaultRanges
            testCase "Exact-end empty array ranges" exactEndEmptyRanges
            testCase "Start validation precedes count validation" startValidationPrecedesCount
            testCase "Negative array counts other than sentinel are rejected" invalidNegativeCounts
            testCase "Overflowing array counts are rejected" overflowingCounts
            testCase "Box3d subranges use compact converted storage" compactDoubleSubrange
        ]
