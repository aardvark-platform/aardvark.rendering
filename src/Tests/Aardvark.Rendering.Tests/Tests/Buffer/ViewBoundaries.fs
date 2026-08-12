namespace Aardvark.Rendering.Tests

open System
open System.Collections.Generic
open Aardvark.Rendering
open Expecto

module ``Buffer View Boundary Tests`` =

    type private TransferCall =
        | UploadCall of offset : uint64 * sizeInBytes : uint64 * discard : bool
        | DownloadCall of offset : uint64 * sizeInBytes : uint64
        | DownloadAsyncCall of offset : uint64 * sizeInBytes : uint64
        | CopyCall of srcOffset : uint64 * dstOffset : uint64 * sizeInBytes : uint64 * discard : bool

    type private RecordingBuffer(runtime : IBufferRuntime, sizeInBytes : uint64) as this =
        let mutable name = null

        interface IBackendBuffer with
            member _.Runtime = runtime
            member _.Handle = 0UL
            member _.Buffer = this :> IBackendBuffer
            member _.Offset = 0UL
            member _.SizeInBytes = sizeInBytes
            member _.Name with get() = name and set value = name <- value
            member _.Dispose() = ()

    type private CustomRange(buffer : IBackendBuffer, offset : uint64, sizeInBytes : uint64) =
        interface IBufferRange with
            member _.Buffer = buffer
            member _.Offset = offset
            member _.SizeInBytes = sizeInBytes

    type private CustomVector(buffer : IBackendBuffer, origin : int, delta : int, count : int) =
        interface IBufferVector<int> with
            member _.Buffer = buffer
            member _.Origin = origin
            member _.Delta = delta
            member _.Count = count

    type private CustomTypedRange(buffer : IBackendBuffer, offset : uint64, sizeInBytes : uint64,
                                  origin : int, delta : int, count : int) =
        interface IBufferRange with
            member _.Buffer = buffer
            member _.Offset = offset
            member _.SizeInBytes = sizeInBytes

        interface IBufferVector<int> with
            member _.Buffer = buffer
            member _.Origin = origin
            member _.Delta = delta
            member _.Count = count

        interface IBufferRange<int>

    type private RecordingRuntime() as this =
        let calls = ResizeArray<TransferCall>()
        let mutable waitCount = 0

        member _.Calls = calls.ToArray()
        member _.CallCount = calls.Count
        member _.WaitCount = waitCount

        member _.Clear() =
            calls.Clear()
            waitCount <- 0

        member _.CreateBuffer(sizeInBytes : uint64) =
            new RecordingBuffer(this :> IBufferRuntime, sizeInBytes) :> IBackendBuffer

        interface IBufferRuntime with
            member _.PrepareBuffer(data, _, _) =
                match data with
                | :? IBackendBuffer as buffer -> buffer
                | _ -> raise <| NotSupportedException()

            member _.CreateBuffer(sizeInBytes, _, _) =
                this.CreateBuffer sizeInBytes

            member _.Upload(_, _, offset, sizeInBytes, discard) =
                calls.Add <| UploadCall(offset, sizeInBytes, discard)

            member _.Download(_, offset, _, sizeInBytes) =
                calls.Add <| DownloadCall(offset, sizeInBytes)

            member _.DownloadAsync(_, offset, _, sizeInBytes) =
                calls.Add <| DownloadAsyncCall(offset, sizeInBytes)
                fun () -> waitCount <- waitCount + 1

            member _.Copy(_, srcOffset, _, dstOffset, sizeInBytes, discard) =
                calls.Add <| CopyCall(srcOffset, dstOffset, sizeInBytes, discard)

    module private Helpers =

        let expectCalls (runtime : RecordingRuntime) (expected : TransferCall array) =
            Expect.sequenceEqual runtime.Calls expected "unexpected backing-buffer transfer window"

        let expectArgumentException (runtime : RecordingRuntime) (message : string) (action : unit -> unit) =
            let callCount = runtime.CallCount
            let mutable caught : exn option = None

            try
                action()
            with exn ->
                caught <- Some exn

            match caught with
            | Some (:? ArgumentException) -> ()
            | Some exn -> failtestf "%s: expected ArgumentException but got %s" message (exn.GetType().FullName)
            | None -> failtestf "%s: expected ArgumentException but no exception was thrown" message

            Expect.equal runtime.CallCount callCount $"{message}: invalid operation reached the backing runtime"

        let expectVector (origin : int) (delta : int) (count : int) (vector : IBufferVector<int>) =
            Expect.equal vector.Origin origin "unexpected vector origin"
            Expect.equal vector.Delta delta "unexpected vector delta"
            Expect.equal vector.Count count "unexpected vector count"

    module private Cases =

        open Helpers

        let validTransferWindows() =
            let runtime = RecordingRuntime()
            use buffer = runtime.CreateBuffer 64UL

            let raw = (buffer :> IBufferRange).Range(8UL, 16UL).Range(4UL, 8UL)
            raw.Upload(123n, 8UL, true)
            raw.Download(456n, 7UL)
            let wait = raw.DownloadAsync(789n, 6UL)
            wait()

            expectCalls runtime [|
                UploadCall(12UL, 8UL, true)
                DownloadCall(12UL, 7UL)
                DownloadAsyncCall(12UL, 6UL)
            |]
            Expect.equal runtime.WaitCount 1 "the asynchronous transfer waiter must remain usable"

            runtime.Clear()

            let typed = buffer.Coerce<int>().Elements(2, 4).Elements(1, 2)
            typed.Upload([| 10; 20; 30 |], 1, 1, 1, true)

            let target = Array.zeroCreate<int> 4
            typed.Download(target, 1, 2, 1)
            let wait = typed.DownloadAsync(target, 0, 3, 1)
            wait()

            expectCalls runtime [|
                UploadCall(16UL, 4UL, true)
                DownloadCall(16UL, 4UL)
                DownloadAsyncCall(12UL, 4UL)
            |]
            Expect.equal runtime.WaitCount 1 "the typed asynchronous transfer waiter must remain usable"

        let logicalRangeIsolation() =
            let runtime = RecordingRuntime()
            use buffer = runtime.CreateBuffer 64UL

            let rawLeft = (buffer :> IBufferRange).Range(8UL, 8UL)
            expectArgumentException runtime "raw upload crossing a sibling range" (fun () -> rawLeft.Upload(0n, 9UL))
            expectArgumentException runtime "raw download crossing a sibling range" (fun () -> rawLeft.Download(0n, 9UL))
            expectArgumentException runtime "raw asynchronous download crossing a sibling range" (fun () -> rawLeft.DownloadAsync(0n, 9UL)() )

            let typedLeft = buffer.Coerce<int>().Elements(2, 2)
            expectArgumentException runtime "typed upload crossing a sibling range" (fun () ->
                typedLeft.Upload([| 1 |], 0, 2, 1)
            )
            expectArgumentException runtime "typed download crossing a sibling range" (fun () ->
                typedLeft.Download(Array.zeroCreate 4, 2, 0, 1)
            )
            expectArgumentException runtime "typed asynchronous download crossing a sibling range" (fun () ->
                typedLeft.DownloadAsync(Array.zeroCreate 4, 2, 0, 1)()
            )

            Expect.equal runtime.CallCount 0 "invalid logical ranges must never reach the backing runtime"

        let typedIndicesAndExactEnd() =
            let runtime = RecordingRuntime()
            use buffer = runtime.CreateBuffer 64UL
            let range = buffer.Coerce<int>().Elements(2, 2)
            let source = [| 1; 2; 3; 4 |]
            let target = Array.zeroCreate<int> 4

            let invalidUploads = [
                "negative upload count",                         fun () -> range.Upload(source, 0, 0, -1)
                "negative source array index",                    fun () -> range.Upload(source, -1, 0, 1)
                "source array range outside the array",           fun () -> range.Upload(source, source.Length, 0, 1)
                "overflowing source array range",                 fun () -> range.Upload(source, Int32.MaxValue, 0, 2)
                "negative destination view index",                fun () -> range.Upload(source, 0, -1, 1)
                "destination range outside the view",             fun () -> range.Upload(source, 0, range.Count, 1)
                "overflowing destination view range",             fun () -> range.Upload(source, 0, Int32.MaxValue, 2)
            ]

            for message, action in invalidUploads do
                expectArgumentException runtime message action

            let invalidDownloads = [
                "negative download count",                        fun () -> range.Download(target, 0, 0, -1)
                "negative source view index",                      fun () -> range.Download(target, -1, 0, 1)
                "source range outside the view",                   fun () -> range.Download(target, range.Count, 0, 1)
                "overflowing source view range",                   fun () -> range.Download(target, Int32.MaxValue, 0, 2)
                "negative destination array index",                fun () -> range.Download(target, 0, -1, 1)
                "destination range outside the array",             fun () -> range.Download(target, 0, target.Length, 1)
                "overflowing destination array range",             fun () -> range.Download(target, 0, Int32.MaxValue, 2)
            ]

            for message, action in invalidDownloads do
                expectArgumentException runtime message action

            let invalidAsyncDownloads = [
                "negative asynchronous download count",            fun () -> range.DownloadAsync(target, 0, 0, -1)()
                "negative asynchronous source view index",         fun () -> range.DownloadAsync(target, -1, 0, 1)()
                "asynchronous source range outside the view",       fun () -> range.DownloadAsync(target, range.Count, 0, 1)()
                "overflowing asynchronous source view range",       fun () -> range.DownloadAsync(target, Int32.MaxValue, 0, 2)()
                "negative asynchronous destination array index",    fun () -> range.DownloadAsync(target, 0, -1, 1)()
                "asynchronous destination range outside the array", fun () -> range.DownloadAsync(target, 0, target.Length, 1)()
                "overflowing asynchronous destination array range", fun () -> range.DownloadAsync(target, 0, Int32.MaxValue, 2)()
            ]

            for message, action in invalidAsyncDownloads do
                expectArgumentException runtime message action

            range.Upload(source, source.Length, range.Count, 0)
            range.Download(target, range.Count, target.Length, 0)
            range.DownloadAsync(target, range.Count, target.Length, 0)()

            Expect.equal runtime.CallCount 0 "exact-end zero-count operations must not reach the backing runtime"

        let slicingBoundsAndOverflow() =
            let runtime = RecordingRuntime()
            use buffer = runtime.CreateBuffer 64UL
            let raw = buffer :> IBufferRange
            let typed = buffer.Coerce<int>()

            expectArgumentException runtime "overflowing untyped range end" (fun () ->
                raw.Range(UInt64.MaxValue, 2UL) |> ignore
            )
            expectArgumentException runtime "overflowing untyped range size" (fun () ->
                raw.Range(1UL, UInt64.MaxValue) |> ignore
            )
            expectArgumentException runtime "overflowing typed range end" (fun () ->
                typed.Elements(Int32.MaxValue, 2) |> ignore
            )

            ResourceValidation.Buffers.validateRange buffer.SizeInBytes 0UL buffer
            expectArgumentException runtime "overflowing validated buffer offset" (fun () ->
                ResourceValidation.Buffers.validateRange UInt64.MaxValue 2UL buffer
            )
            expectArgumentException runtime "overflowing validated buffer size" (fun () ->
                ResourceValidation.Buffers.validateRange 1UL UInt64.MaxValue buffer
            )

            let emptyRaw = raw.Range(raw.SizeInBytes, 0UL)
            Expect.equal emptyRaw.Offset raw.SizeInBytes "an empty raw range may start exactly at the end"
            Expect.equal emptyRaw.SizeInBytes 0UL "the exact-end raw range must be empty"

            emptyRaw.Upload(0n, 0UL)
            emptyRaw.Download(0n, 0UL)
            emptyRaw.DownloadAsync(0n, 0UL)()
            Expect.equal runtime.CallCount 0 "zero-byte transfers on an exact-end raw range must not reach the backing runtime"

            let emptyTyped = typed.Elements(typed.Count, 0)
            Expect.equal emptyTyped.Origin typed.Count "an empty typed range may start exactly at the end"
            Expect.equal emptyTyped.Count 0 "the exact-end typed range must be empty"

            let defaultSlice = emptyTyped.GetSlice(None, None)
            Expect.equal defaultSlice.Origin emptyTyped.Origin "the default slice of an empty range must preserve its origin"
            Expect.equal defaultSlice.Count 0 "the default slice of an empty range must remain empty"

        let coercionAndMalformedRanges() =
            let runtime = RecordingRuntime()
            let tooManyElements = uint64 Int32.MaxValue + 1UL
            let tooManyBytes = tooManyElements * uint64 sizeof<int>
            use hugeBuffer = runtime.CreateBuffer (tooManyBytes + uint64 sizeof<int>)

            expectArgumentException runtime "oversized typed backend buffer" (fun () ->
                hugeBuffer.Coerce<int>() |> ignore
            )

            let backingOverflow = CustomRange(hugeBuffer, hugeBuffer.SizeInBytes - 1UL, 2UL) :> IBufferRange
            expectArgumentException runtime "raw range exceeding its backing buffer" (fun () ->
                backingOverflow.CoerceRange<int>() |> ignore
            )

            let unrepresentableCount = CustomRange(hugeBuffer, 0UL, tooManyBytes) :> IBufferRange
            expectArgumentException runtime "unrepresentable typed range count" (fun () ->
                unrepresentableCount.CoerceRange<int>() |> ignore
            )

            let unrepresentableOrigin = CustomRange(hugeBuffer, tooManyBytes, uint64 sizeof<int>) :> IBufferRange
            expectArgumentException runtime "unrepresentable typed range origin" (fun () ->
                unrepresentableOrigin.CoerceRange<int>() |> ignore
            )

            let unrepresentableLast =
                CustomRange(hugeBuffer, uint64 Int32.MaxValue * uint64 sizeof<int>, 2UL * uint64 sizeof<int>) :> IBufferRange

            expectArgumentException runtime "unrepresentable typed range endpoint" (fun () ->
                unrepresentableLast.CoerceRange<int>() |> ignore
            )

            let unrepresentableVector =
                CustomVector(hugeBuffer, Int32.MaxValue, 1, 2) :> IBufferVector<int>

            expectArgumentException runtime "unrepresentable vector endpoint" (fun () ->
                unrepresentableVector.SubVector(0, 1, 2) |> ignore
            )

            let unrepresentableTyped =
                CustomTypedRange(
                    hugeBuffer,
                    uint64 Int32.MaxValue * uint64 sizeof<int>,
                    2UL * uint64 sizeof<int>,
                    Int32.MaxValue,
                    1,
                    2
                ) :> IBufferRange<int>

            expectArgumentException runtime "unrepresentable already-typed range endpoint" (fun () ->
                (unrepresentableTyped :> IBufferRange).CoerceRange<int>() |> ignore
            )
            expectArgumentException runtime "unrepresentable typed element endpoint" (fun () ->
                unrepresentableTyped.Elements(0, 2) |> ignore
            )

            use buffer = runtime.CreateBuffer 64UL
            let negativeOrigin =
                CustomTypedRange(buffer, 0UL, uint64 sizeof<int>, -1, 1, 1) :> IBufferRange<int>

            expectArgumentException runtime "already-typed range with a negative origin" (fun () ->
                (negativeOrigin :> IBufferRange).CoerceRange<int>() |> ignore
            )
            expectArgumentException runtime "element slice with a negative physical origin" (fun () ->
                negativeOrigin.Elements(0, 1) |> ignore
            )

        let malformedTransfers() =
            let runtime = RecordingRuntime()
            use buffer = runtime.CreateBuffer 64UL
            let malformed = CustomRange(buffer, buffer.SizeInBytes + 1UL, 0UL) :> IBufferRange

            expectArgumentException runtime "zero-byte upload on malformed raw range" (fun () ->
                malformed.Upload(0n, 0UL)
            )
            expectArgumentException runtime "zero-byte download on malformed raw range" (fun () ->
                malformed.Download(0n, 0UL)
            )
            expectArgumentException runtime "zero-byte asynchronous download on malformed raw range" (fun () ->
                malformed.DownloadAsync(0n, 0UL)()
            )

            Expect.equal runtime.CallCount 0 "malformed zero-byte transfers must never reach the backing runtime"

        let vectorsAndEmptyViews() =
            let runtime = RecordingRuntime()
            use buffer = runtime.CreateBuffer 64UL
            let vector = buffer.Coerce<int>() :> IBufferVector<int>

            let outer = vector.SubVector(2, 3, 4)
            expectVector 2 3 4 outer

            let nested = outer.SubVector(3, -1, 4)
            expectVector 11 -3 4 nested

            vector.SubVector(6, -2, 4) |> expectVector 6 -2 4
            vector.SubVector(5, 0, 20) |> expectVector 5 0 20

            expectArgumentException runtime "negative-delta vector leaving its parent" (fun () ->
                vector.SubVector(0, -1, 2) |> ignore
            )
            expectArgumentException runtime "positive-delta vector leaving its parent" (fun () ->
                vector.SubVector(15, 1, 2) |> ignore
            )
            expectArgumentException runtime "overflowing vector endpoint" (fun () ->
                vector.SubVector(1, Int32.MaxValue, 2) |> ignore
            )

            let hugeDelta = vector.SubVector(0, Int32.MaxValue, 1)
            expectArgumentException runtime "overflowing nested vector delta" (fun () ->
                hugeDelta.SubVector(0, 2, 1) |> ignore
            )

            expectArgumentException runtime "empty vector with a negative start" (fun () ->
                vector.SubVector(-1, 1, 0) |> ignore
            )
            expectArgumentException runtime "empty vector starting beyond the logical end" (fun () ->
                vector.SubVector(vector.Count + 1, 1, 0) |> ignore
            )

            use partialBuffer = runtime.CreateBuffer 6UL
            let partialTail = CustomVector(partialBuffer, 0, 1, 2) :> IBufferVector<int>
            expectArgumentException runtime "vector element extending into a partial trailing element" (fun () ->
                partialTail.SubVector(1, 1, 1) |> ignore
            )

            vector.SubVector(vector.Count, 1, 0) |> expectVector vector.Count 1 0
            vector.Take(0) |> expectVector 0 1 0
            vector.Skip(vector.Count) |> expectVector vector.Count 1 0
            vector.Skip(vector.Count + 10) |> expectVector vector.Count 1 0

            let empty = vector.Take(0)
            empty.Strided(3) |> expectVector 0 3 0

            use emptyBuffer = runtime.CreateBuffer 0UL
            let emptyBufferVector = emptyBuffer.Coerce<int>() :> IBufferVector<int>
            emptyBufferVector.Strided(4) |> expectVector 0 4 0

            expectArgumentException runtime "zero striding delta" (fun () ->
                vector.Strided(0) |> ignore
            )
            expectArgumentException runtime "negative striding delta" (fun () ->
                vector.Strided(-1) |> ignore
            )

    [<Tests>]
    let tests =
        testList "Buffer.ViewBoundaries" [
            testCase "Valid transfer windows" Cases.validTransferWindows
            testCase "Logical sibling-range isolation" Cases.logicalRangeIsolation
            testCase "Typed indices and exact-end zero operations" Cases.typedIndicesAndExactEnd
            testCase "Slicing bounds and overflow" Cases.slicingBoundsAndOverflow
            testCase "Coercion and malformed ranges" Cases.coercionAndMalformedRanges
            testCase "Malformed raw transfers" Cases.malformedTransfers
            testCase "Nested reversed repeated and empty vectors" Cases.vectorsAndEmptyViews
        ]
