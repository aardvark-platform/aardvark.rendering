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

        let private largeLayouts =
            [ typeof<byte>, 0
              typeof<int>, 0
              typeof<int>, 8
              typeof<int>, 1
              typeof<int>, 3
              typeof<V3f>, 24
              typeof<double>, Int32.MaxValue ]

        let private sizeForCount (elementType : Type) offset stride count tail =
            let firstEnd = uint64 offset + uint64 elementType.CLRSize
            let step = if stride = 0 then uint64 elementType.CLRSize else uint64 stride
            if count = 0UL then firstEnd - 1UL
            else firstEnd + (count - 1UL) * step + tail

        let private expectOverflow (errors : ResizeArray<string>) context count action =
            try
                let actual : int = action()
                errors.Add $"{context}: expected ArgumentException for count {count}, got {actual}"
            with :? ArgumentException as error ->
                Expect.equal (error.GetType()) typeof<ArgumentException> (context + ": exact exception type")
                Expect.equal error.Message $"[Buffer] element count {count} exceeds the supported maximum of {Int32.MaxValue}." (context + ": buffer diagnostic")
                Expect.isNull error.ParamName (context + ": existing buffer-error convention")

        let representableLargeLayouts() =
            for backend in [false; true] do
                for elementType, stride in largeLayouts do
                    for offset in [0; 7; Int32.MaxValue] do
                        let step = if stride = 0 then elementType.CLRSize else stride
                        for expected in [0UL; 1UL; uint64 Int32.MaxValue - 1UL; uint64 Int32.MaxValue] do
                            for tail in [0UL; uint64 step - 1UL] |> List.distinct do
                                let size = sizeForCount elementType offset stride expected tail
                                let mutable reads = 0
                                let buffer = metadataBuffer backend size (fun () -> reads <- reads + 1)
                                let count = BufferView(buffer, elementType, offset, stride) |> BufferView.getCount
                                let context = $"backend={backend}, type={elementType}, offset={offset}, stride={stride}, count={expected}, tail={tail}"
                                for _ in 1 .. 3 do Expect.equal (AVal.force count) (int expected) context
                                Expect.equal reads 1 (context + ": cached metadata-only evaluation")

        let oversizedLayouts() =
            let errors = ResizeArray<string>()
            for backend in [false; true] do
                for elementType, stride in largeLayouts do
                    for offset in [0; 7; Int32.MaxValue] do
                        for expected in [uint64 Int32.MaxValue + 1UL; uint64 Int32.MaxValue + 2UL; 1UL <<< 32; (1UL <<< 32) + 3UL] do
                            let size = sizeForCount elementType offset stride expected 0UL
                            let context = $"backend={backend}, type={elementType}, offset={offset}, stride={stride}"
                            let buffer = metadataBuffer backend size ignore
                            expectOverflow errors context expected (fun () -> BufferView(buffer, elementType, offset, stride) |> BufferView.getCount |> AVal.force)
                let buffer = metadataBuffer backend UInt64.MaxValue ignore
                expectOverflow errors $"backend={backend}, UInt64 maximum byte buffer" UInt64.MaxValue (fun () -> BufferView(buffer, typeof<byte>) |> BufferView.getCount |> AVal.force)
            Expect.isTrue (errors.Count = 0) (String.concat "\n" errors)

        let oversizedAdaptiveReplacement() =
            let errors = ResizeArray<string>()
            for elementType, stride in largeLayouts do
                for offset in [0; 7; Int32.MaxValue] do
                    let mutable reads, evaluations = 0, 0
                    let source = cval (metadataBuffer false 0UL (fun () -> reads <- reads + 1))
                    let buffer = AVal.custom (fun token -> evaluations <- evaluations + 1; source.GetValue token)
                    let count = BufferView(buffer, elementType, offset, stride) |> BufferView.getCount
                    Expect.equal (reads, evaluations) (0, 0) "Creating an adaptive count does not evaluate storage"
                    for expected, backend in [3UL, false; uint64 Int32.MaxValue, true; uint64 Int32.MaxValue + 1UL, false;
                                              3UL, true; 1UL <<< 32, true; uint64 Int32.MaxValue - 1UL, false;
                                              (1UL <<< 32) + 3UL, true; 0UL, false; 4UL, true] do
                        let size = sizeForCount elementType offset stride expected 0UL
                        let beforeReads, beforeEvaluations = reads, evaluations
                        transact (fun () -> source.Value <- metadataBuffer backend size (fun () -> reads <- reads + 1))
                        let context = $"type={elementType}, offset={offset}, stride={stride}, count={expected}, backend={backend}"
                        Expect.equal (reads, evaluations) (beforeReads, beforeEvaluations) (context + ": replacement remains lazy")
                        Expect.isTrue count.OutOfDate (context + ": invalidated count")
                        if expected > uint64 Int32.MaxValue then
                            expectOverflow errors context expected (fun () -> AVal.force count)
                        else
                            for _ in 1 .. 3 do Expect.equal (AVal.force count) (int expected) (context + ": cached valid result after replacement")
                        Expect.equal (reads, evaluations) (beforeReads + 1, beforeEvaluations + 1) (context + ": one metadata read and dependency evaluation")
            Expect.isTrue (errors.Count = 0) (String.concat "\n" errors)

        let oversizedDrawCounts() =
            let errors = ResizeArray<string>()
            for backend in [false; true] do
                for indexed in [false; true] do
                    let elementType = if indexed then typeof<uint32> else typeof<V3f>
                    let offset, stride = 7, 2 * elementType.CLRSize
                    let mutable reads = 0
                    let storage = cval (metadataBuffer backend 0UL (fun () -> reads <- reads + 1))
                    let buffer = AVal.custom (fun token -> storage.GetValue token)
                    let view = BufferView(buffer, elementType, offset, stride)
                    let calls bounded =
                        let node =
                            if bounded then Sg.render IndexedGeometryMode.TriangleList (DrawCallInfo(FaceVertexCount = 3, InstanceCount = 1)) :> ISg
                            else Sg.draw IndexedGeometryMode.TriangleList
                        let graph =
                            if indexed then
                                node |> Sg.vertexArray DefaultSemantic.Positions [|V3f.Zero; V3f.XAxis; V3f.YAxis|] |> Sg.indexBuffer view
                            else node |> Sg.vertexBuffer DefaultSemantic.Positions view
                        let renderObject = graph.RenderObjects(Ag.Scope.Root) |> ASet.force |> Seq.exactlyOne :?> RenderObject
                        match renderObject.DrawCalls with
                        | DrawCalls.Direct value -> value
                        | _ -> failtest "Expected a direct draw without a graphics resource"
                    let automatic, bounded = calls false, calls true
                    Expect.equal reads 0 "Building draw calls does not inspect buffer metadata"
                    for expected in [3UL; uint64 Int32.MaxValue; uint64 Int32.MaxValue + 1UL; 1UL <<< 32; (1UL <<< 32) + 3UL; 3UL] do
                        transact (fun () -> storage.Value <- metadataBuffer backend (sizeForCount elementType offset stride expected 0UL) (fun () -> reads <- reads + 1))
                        let context = $"backend={backend}, indexed={indexed}, count={expected}"
                        let before = reads
                        let info = AVal.force bounded |> Array.exactlyOne
                        Expect.equal (info.FaceVertexCount, info.InstanceCount) (3, 1) (context + ": explicitly bounded draw remains usable")
                        Expect.equal reads before (context + ": bounded draw does not evaluate automatic count")
                        let force() = (AVal.force automatic |> Array.exactlyOne).FaceVertexCount
                        if expected > uint64 Int32.MaxValue then expectOverflow errors context expected force
                        else Expect.equal (force()) (int expected) (context + ": automatic count and recovery")
            Expect.isTrue (errors.Count = 0) (String.concat "\n" errors)

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
            "representable large layouts retain their counts", Cases.representableLargeLayouts
            "oversized layout counts are rejected",            Cases.oversizedLayouts
            "oversized adaptive replacements recover",         Cases.oversizedAdaptiveReplacement
            "automatic and bounded oversized draw counts",     Cases.oversizedDrawCounts
            "single values ignore layout and payload changes", Cases.singleValues
            "automatic scene graph draw counts",               Cases.automaticDrawCounts
        ]
        |> prepareCasesCpu "BufferView.Count" target
