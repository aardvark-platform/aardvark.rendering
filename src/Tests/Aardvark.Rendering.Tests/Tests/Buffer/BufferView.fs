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

    module private Download =

        let private storage wrapped (data : Array) onUse : IBuffer =
            let buffer = ArrayBuffer data
            if wrapped then
                let native = buffer :> INativeBuffer
                { new INativeBuffer with
                    member _.SizeInBytes = native.SizeInBytes
                    member _.Use action = onUse(); native.Use action }
            else buffer

        let private suffix start count (values : int[]) =
            values |> Array.skip (min (max start 0) values.Length)
                   |> fun rest -> if count < 0 then rest else Array.truncate count rest

        let private check context start count (view : BufferView) expected =
            let result = BufferView.download start count view |> AVal.force
            Expect.equal (result :?> int[]) expected $"{context}, start={start}, count={count}: logical values"

        let logicalStarts() =
            for offset, stride, data, values in [
                0, 8, [|11; -901; 22; -902; 33|], [|11; 22; 33|]
                4, 8, [|-800; 11; -901; 22; -902; 33|], [|11; 22; 33|]
                8, 12, [|-800; -801; 11; -901; -902; 22; -903; -904; 33|], [|11; 22; 33|]
                4, 8, [|-800; 11; -901; 22; -902|], [|11; 22|]
            ] do
                for wrapped in [false; true] do
                    let view = BufferView(storage wrapped data ignore, typeof<int>, offset, stride)
                    for start in [1; 2] do
                        for count in [0; 1; 2; Int32.MaxValue; -1; -7; Int32.MinValue] do
                            check $"wrapped={wrapped}, offset={offset}, stride={stride}, data=%A{data}"
                                start count view (suffix start count values)

        let overlappingElements() =
            let data = Array.init 11 (fun i -> byte (7 + 13 * i))
            for offset, stride, positions in [1, 1, [|1 .. 7|]; 1, 2, [|1; 3; 5; 7|]; 3, 2, [|3; 5; 7|]] do
                // Read independently specified byte windows, not a second download.
                let values = positions |> Array.map (fun p -> BitConverter.ToInt32(data, p))
                for wrapped in [false; true] do
                    let view = BufferView(storage wrapped data ignore, typeof<int>, offset, stride)
                    for start in [1; 2] do
                        for count in [0; 1; 2; -1; -11; Int32.MaxValue] do
                            check $"wrapped={wrapped}, byte offset={offset}, overlapping stride={stride}"
                                start count view (suffix start count values)

        type private Resource(source : aval<IBuffer>) =
            inherit AdaptiveResource<IBuffer>()
            let mutable creates = 0
            let mutable destroys = 0
            member _.Creates = creates
            member _.Destroys = destroys
            override _.Create() = creates <- creates + 1
            override _.Destroy() = destroys <- destroys + 1
            override _.Compute(token, _) = source.GetValue token

        let replacement() =
            for wrapped in [false; true] do
                for resourceBacked in [false; true] do
                    for count in [2; -1; -9] do
                        let mutable uses = 0
                        let mutable evaluations = 0
                        let make data = storage wrapped data (fun () -> uses <- uses + 1)
                        let source = cval (make [|-800; 11; -901; 22; -902; 33|])
                        let input = AVal.custom (fun token -> evaluations <- evaluations + 1; source.GetValue token)
                        let resource = Resource input
                        let buffer = if resourceBacked then resource :> aval<IBuffer> else input
                        let result = BufferView(buffer, typeof<int>, 4, 8) |> BufferView.download 1 count
                        let context = $"wrapped={wrapped}, resource={resourceBacked}, count={count}"
                        Expect.equal evaluations 0 $"{context}: lazy construction"
                        Expect.equal (result :? IAdaptiveResource) resourceBacked $"{context}: resource mapping preserved"
                        result.Acquire()
                        try
                            Expect.equal resource.Creates (if resourceBacked then 1 else 0) $"{context}: acquire forwarded"
                            for data, values in [
                                [|-800; 11; -901; 22; -902; 33|], [|11; 22; 33|]
                                [|-800; 71; -901; 83|], [|71; 83|]
                                [|-800; 93|], [|93|]
                                [|-800; 1; -901; 2; -902; 3; -903; 4|], [|1; 2; 3; 4|]
                            ] do
                                transact (fun () -> source.Value <- make data)
                                let before = evaluations
                                let downloaded = AVal.force result
                                Expect.equal (downloaded :?> int[]) (suffix 1 count values) $"{context}: replacement=%A{data}"
                                Expect.equal evaluations (before + 1) $"{context}: evaluates once"
                                let used = uses
                                for _ in 1 .. 3 do
                                    Expect.isTrue (obj.ReferenceEquals(AVal.force result, downloaded)) $"{context}: cached array"
                                Expect.equal evaluations (before + 1) $"{context}: cached input"
                                Expect.equal uses used $"{context}: cached native storage"
                        finally
                            result.Release()
                        Expect.equal resource.Destroys (if resourceBacked then 1 else 0) $"{context}: release forwarded"

        let boundaries() =
            let data = [|13; 27; 45; 88|]
            for wrapped in [false; true] do
                for stride in [0; 4] do
                    for offset, values in [0, [|13; 27; 45; 88|]; 4, [|27; 45; 88|]] do
                        let view = BufferView(storage wrapped data ignore, typeof<int>, offset, stride)
                        for start in [Int32.MinValue; -1; 0; 1; values.Length] do
                            for count in [0; 1; 2; -1; -17; Int32.MaxValue] do
                                check $"wrapped={wrapped}, packed stride={stride}, offset={offset}"
                                    start count view (suffix start count values)
                        if offset = 0 then
                            let result = BufferView.download 0 -1 view |> AVal.force
                            Expect.equal (obj.ReferenceEquals(result, data)) (not wrapped)
                                $"wrapped={wrapped}, stride={stride}: full packed-array identity"
                let strided = BufferView(storage wrapped [|11; -901; 22; -902; 33|] ignore, typeof<int>, 0, 8)
                for start in [0; -1; Int32.MinValue] do
                    for count in [0; 1; -1; -7] do
                        check $"wrapped={wrapped}, zero or clamped start" start count strided (suffix start count [|11; 22; 33|])
                // A 32-bit stride/index product would wrap to zero and incorrectly return 41.
                let wide = BufferView(storage wrapped [|41|] ignore, typeof<int>, 0, 65536)
                for count in [0; 1; -1] do check $"wrapped={wrapped}, wide product" 65536 count wide [||]

        let singleValues() =
            let source = cval 17
            let buffer = SingleValueBuffer<int>(source)
            for view in [BufferView(buffer :> ISingleValueBuffer)
                         BufferView(buffer :> aval<IBuffer>, typeof<V3f>, Int32.MaxValue, Int32.MaxValue, false)] do
                for start in [Int32.MinValue; 0; 7; Int32.MaxValue] do
                    for count in [0; 1; 5; -1; -7; Int32.MinValue] do
                        let result = BufferView.download start count view
                        for value in [19; 31] do
                            transact (fun () -> source.Value <- value)
                            let expected = Array.replicate (if count < 0 then 1 else count) value
                            Expect.equal (AVal.force result :?> int[]) expected $"single value={value}, start={start}, count={count}"

    let tests (target: TestTarget) =
        [
            "Count.packed elements and exhausted storage",           Cases.packedElements
            "Count.strided elements and unpadded tails",             Cases.stridedElements
            "Count.native and backend metadata only",                Cases.metadataOnly
            "Count.adaptive replacement and cached evaluation",      Cases.adaptiveReplacement
            "Count.single values ignore layout and payload changes", Cases.singleValues
            "Count.automatic scene graph draw counts",               Cases.automaticDrawCounts

            "Download.Logical starts in interleaved layouts",   Download.logicalStarts
            "Download.Overlapping elements and byte offsets",   Download.overlappingElements
            "Download.Adaptive replacement and cached reads",   Download.replacement
            "Download.Packed and boundary controls",            Download.boundaries
            "Download.Single-value replication",               Download.singleValues
        ]
        |> prepareCasesCpu "BufferView" target
