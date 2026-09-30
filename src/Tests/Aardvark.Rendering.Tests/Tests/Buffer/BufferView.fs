namespace Aardvark.Rendering.Tests.Buffer

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open FSharp.Data.Adaptive
open Expecto

module BufferView =

    module private Cases =

        // These buffers have no accessible storage. Counting must only request SizeInBytes.
        let private metadataBuffer backend size onSize : IBuffer =
            if backend then
                { new IBackendBuffer with
                    member _.SizeInBytes = onSize(); size
                    member _.Buffer = failwith "Unexpected backing buffer access"
                    member _.Offset = failwith "Unexpected buffer range access"
                    member _.Runtime = failwith "Unexpected runtime access"
                    member _.Handle = failwith "Unexpected handle access"
                    member _.Name
                        with get() = failwith "Unexpected name access"
                        and set _ = failwith "Unexpected name change"
                    member _.Dispose() = failwith "Unexpected disposal" }
            else
                { new INativeBuffer with
                    member _.SizeInBytes = onSize(); size
                    member _.Use _ = failwith "Unexpected buffer content access" }

        let private checkDownload offset stride (data : int[]) expected =
            let buffer = ArrayBuffer data
            let view = BufferView(buffer, typeof<int>, offset, stride)
            let context = sprintf "data=%A, offset=%d, stride=%d" data offset stride
            let downloaded = buffer.ToArray<int>(offset = uint64 offset, stride = uint64 stride)
            Expect.equal downloaded expected $"{context}: actual ToArray contents"
            Expect.equal (BufferView.getCount view |> AVal.force) downloaded.Length $"{context}: count agrees with ToArray"

        let private layouts strided =
            for elementType in [typeof<byte>; typeof<int>; typeof<double>; typeof<V3f>] do
                let elementSize = elementType.CLRSize
                let strides =
                    if strided then [1; max 1 (elementSize - 1); elementSize + 1; 2 * elementSize; Int32.MaxValue]
                    else [0; elementSize]
                for size in [0; 1; elementSize - 1; elementSize; 2 * elementSize; 3 * elementSize - 1; 4 * elementSize + 3] |> List.distinct do
                    let buffer = ArrayBuffer(Array.init size byte)
                    for offset in [0; 1; elementSize; max 0 (size - 1); size; size + 1; Int32.MaxValue] |> List.distinct do
                        for stride in strides |> List.distinct do
                            let step = if stride = 0 then elementSize else stride
                            // Enumerate valid starting bytes rather than duplicating the count formula.
                            let expected =
                                [0 .. size - 1] |> List.sumBy (fun start ->
                                    if start >= offset && (start - offset) % step = 0 && start + elementSize <= size then 1 else 0)
                            let view = BufferView(buffer, elementType, offset, stride)
                            Expect.equal (BufferView.getCount view |> AVal.force) expected
                                $"type={elementType}, size={size}, offset={offset}, stride={stride}: complete elements"

        let packedElements() =
            checkDownload 0 0 [|11; 22; 33|] [|11; 22; 33|]
            checkDownload 4 4 [|11; 22; 33|] [|22; 33|]
            checkDownload 12 0 [|11; 22; 33|] [||]
            layouts false

        let stridedElements() =
            checkDownload 0 8 [|11; -1; 22; -1; 33|] [|11; 22; 33|]
            checkDownload 4 8 [|-1; 11; -1; 22; -1; 33|] [|11; 22; 33|]
            checkDownload 4 8 [|-1; 11; -1; 22; -1|] [|11; 22|]
            layouts true

        let metadataOnly() =
            let large = Int32.MaxValue
            let lastElement = uint64 large + uint64 (large - 1) * uint64 large + 4UL
            for backend in [false; true] do
                for size, offset, stride, expected in [
                    0UL, 0, 0, 0; 3UL, 0, 8, 0; 4UL, 0, 8, 1; 20UL, 0, 8, 3
                    24UL, 4, 8, 3; 23UL, 4, 8, 2; 4UL, 4, 8, 0; 4UL, large, 8, 0
                    lastElement, large, large, large
                    lastElement - 1UL, large, large, large - 1
                ] do
                    let mutable reads = 0
                    let buffer = metadataBuffer backend size (fun () -> reads <- reads + 1)
                    let count = BufferView(buffer, typeof<int>, offset, stride) |> BufferView.getCount
                    let context = $"backend={backend}, size={size}, offset={offset}, stride={stride}"
                    Expect.equal (AVal.force count) expected context
                    let afterFirst = reads
                    Expect.equal afterFirst 1 $"{context}: one metadata read"
                    for _ in 1 .. 4 do Expect.equal (AVal.force count) expected context
                    Expect.equal reads afterFirst $"{context}: cached count does not revisit metadata"

        let adaptiveReplacement() =
            for stride in [0; 8] do
                let mutable evaluations = 0
                let mutable reads = 0
                let source = cval (metadataBuffer false 24UL (fun () -> reads <- reads + 1))
                let buffer = AVal.custom (fun token -> evaluations <- evaluations + 1; source.GetValue token)
                let count = BufferView(buffer, typeof<int>, 4, stride) |> BufferView.getCount
                Expect.equal evaluations 0 $"stride={stride}: construction is lazy"
                for size, backend, packed, interleaved in [24UL, false, 5, 3; 40UL, true, 9, 5; 8UL, false, 1, 1; 7UL, true, 0, 0; 4UL, false, 0, 0; 0UL, true, 0, 0] do
                    transact (fun () -> source.Value <- metadataBuffer backend size (fun () -> reads <- reads + 1))
                    let before = evaluations
                    let expected = if stride = 0 then packed else interleaved
                    let context = $"stride={stride}, replacement size={size}, backend={backend}"
                    Expect.isTrue count.OutOfDate $"{context}: replacement invalidates the count"
                    Expect.equal (AVal.force count) expected context
                    Expect.equal evaluations (before + 1) $"{context}: dependency evaluated once"
                    Expect.equal reads evaluations $"{context}: metadata only"
                    for _ in 1 .. 4 do Expect.equal (AVal.force count) expected context
                    Expect.equal evaluations (before + 1) $"{context}: clean reads remain cached"
                    Expect.equal reads evaluations $"{context}: clean reads do not query buffer size"

        let singleValues() =
            let mutable evaluations = 0
            let source = cval 17
            let value = source |> AVal.map (fun v -> evaluations <- evaluations + 1; v)
            let buffer = SingleValueBuffer<int>(value)
            for view in [BufferView(buffer :> ISingleValueBuffer)
                         BufferView(buffer :> aval<IBuffer>, typeof<V3f>, Int32.MaxValue, Int32.MaxValue, false)] do
                let count = BufferView.getCount view
                Expect.isTrue view.IsSingleValue "Single-value representation retained"
                Expect.isTrue count.IsConstant "Single-value count remains constant"
                for next in [19; 23; 31] do
                    transact (fun () -> source.Value <- next)
                    Expect.equal (AVal.force count) 1 $"single value={next}"
                    Expect.equal evaluations 0 "Counting does not evaluate the single value"

        let automaticDrawCounts() =
            // Three V3f positions interleaved with unused V3f data, without padding after the last position.
            let initial = [|V3f.OOO; V3f.III; V3f.IOO; V3f.III; V3f.OIO|]
            let storage = cval<IBuffer> (ArrayBuffer initial)
            let positions = BufferView(storage, typeof<V3f>, 0, 2 * sizeof<V3f>)
            let objects =
                Sg.draw IndexedGeometryMode.TriangleList
                |> Sg.vertexBuffer DefaultSemantic.Positions positions
                |> fun sg -> sg.RenderObjects(Ag.Scope.Root)
                |> ASet.force
            Expect.equal objects.Count 1 "One render object without preparing a graphics resource"
            let renderObject = objects |> Seq.exactlyOne :?> RenderObject
            match renderObject.DrawCalls with
            | DrawCalls.Direct calls ->
                for data, expected in [
                    initial, 3
                    Array.append initial [|V3f.III|], 3
                    [|V3f.OOO|], 1
                    [||], 0
                ] do
                    transact (fun () -> storage.Value <- ArrayBuffer data)
                    let info = AVal.force calls |> Array.exactlyOne
                    Expect.equal info.FaceVertexCount expected $"storage vectors={data.Length}: automatic vertex count"
                    Expect.equal (info.FirstIndex, info.BaseVertex, info.FirstInstance, info.InstanceCount) (0, 0, 0, 1)
                        "Other draw parameters are unchanged"
            | _ -> failtest "Expected direct draw calls"

    let tests (target: TestTarget) =
        [
            "packed elements and exhausted storage",           Cases.packedElements
            "strided elements and unpadded tails",             Cases.stridedElements
            "native and backend metadata only",                Cases.metadataOnly
            "adaptive replacement and cached evaluation",      Cases.adaptiveReplacement
            "single values ignore layout and payload changes", Cases.singleValues
            "automatic scene graph draw counts",               Cases.automaticDrawCounts
        ]
        |> prepareCasesCpu "BufferView.Count" target
