namespace Aardvark.Rendering.Tests

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading.Tasks
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Expecto

module ``ManagedBuffer Range Tests`` =

    [<Struct>]
    type private Upload =
        {
            Offset : uint64
            Size : uint64
            Data : byte[]
        }

    type private MockBuffer(runtime : IBufferRuntime, size : uint64) =
        let mutable name = null

        interface IBackendBuffer with
            member _.Runtime = runtime
            member _.Handle = 0UL
            member _.Name with get() = name and set value = name <- value
            member x.Buffer = x :> IBackendBuffer
            member _.Offset = 0UL
            member _.SizeInBytes = size
            member _.Dispose() = ()

    type private MockRuntime() as this =
        let createdSizes = ResizeArray<uint64>()
        let uploads = ResizeArray<Upload>()

        member _.CreatedSizes = createdSizes :> IReadOnlyList<_>
        member _.Uploads = uploads :> IReadOnlyList<_>

        interface IBufferRuntime with
            member _.PrepareBuffer(data, _, _) =
                match data with
                | :? IBackendBuffer as buffer -> buffer
                | _ -> failwith "PrepareBuffer is not used by these tests"

            member _.CreateBuffer(size, _, _) =
                createdSizes.Add size
                new MockBuffer(this :> IBufferRuntime, size) :> IBackendBuffer

            member _.Upload(source, _, offset, size, _) =
                if size = 0UL then failwith "ManagedBuffer attempted a zero-byte upload"

                let data =
                    if size <= 1024UL then
                        let result = Array.zeroCreate<byte> (int size)
                        if size > 0UL then Marshal.Copy(source, result, 0, result.Length)
                        result
                    else
                        Array.empty

                uploads.Add { Offset = offset; Size = size; Data = data }

            member _.Download(_, _, _, _) =
                failwith "Download is not used by these tests"

            member _.DownloadAsync(_, _, _, _) =
                failwith "DownloadAsync is not used by these tests"

            member _.Copy(_, _, _, _, _, _) = ()

    module private Helpers =

        let withBuffer<'T, 'R when 'T : unmanaged> (action : MockRuntime -> IManagedBuffer<'T> -> 'R) =
            let runtime = MockRuntime()
            let buffer = ManagedBuffer.create<'T> runtime BufferUsage.All BufferStorage.Host
            buffer.Acquire()
            try action runtime buffer
            finally buffer.Release()

        let force (buffer : IManagedBuffer) =
            buffer.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore

        let runBounded (action : unit -> unit) =
            let task = Task.Run(Action action)
            let timeout = Task.Delay(TimeSpan.FromSeconds 2.0)
            let completed = Task.WhenAny(task, timeout).GetAwaiter().GetResult()
            Expect.isTrue (obj.ReferenceEquals(completed, task)) "Operation did not complete within the timeout"
            task.GetAwaiter().GetResult()

        let expectUpload (offset : uint64) (size : uint64) (data : byte[]) (upload : Upload) =
            Expect.equal upload.Offset offset "Unexpected upload offset"
            Expect.equal upload.Size size "Unexpected upload size"
            Expect.sequenceEqual upload.Data data "Unexpected upload data"

    module private Cases =

        open Helpers

        let zeroSourceRejectedBeforeMutation() =
            withBuffer<byte, unit> (fun runtime buffer ->
                let target = Range1ul(0UL, 3UL)
                Expect.throwsT<ArgumentException>
                    (fun _ -> runBounded (fun () -> buffer.Set(0n, 0UL, target)))
                    "Expected an empty source to be rejected"

                let emptyView = BufferView(Array.empty<byte>)
                Expect.throwsT<ArgumentException>
                    (fun _ -> runBounded (fun () -> buffer.Add(emptyView, target) |> ignore))
                    "Expected an empty constant buffer view to be rejected"

                Expect.equal buffer.Size 0UL "Buffer was resized before rejecting the source"
                Expect.equal runtime.Uploads.Count 0 "Data was uploaded before rejecting the source"
            )

        let exactAndPartialRepetition() =
            withBuffer<byte, unit> (fun runtime buffer ->
                buffer.Set([| 9uy; 8uy; 7uy; 6uy |], Range1ul(0UL, 3UL))
                buffer.Set([| 1uy; 2uy; 3uy |], Range1ul(5UL, 11UL))
                buffer.Set([| 4uy; 5uy; 6uy |], Range1ul(13UL, 14UL))

                Expect.equal runtime.Uploads.Count 5 "Unexpected upload count"
                expectUpload 0UL 4UL [| 9uy; 8uy; 7uy; 6uy |] runtime.Uploads.[0]
                expectUpload 5UL 3UL [| 1uy; 2uy; 3uy |] runtime.Uploads.[1]
                expectUpload 8UL 3UL [| 1uy; 2uy; 3uy |] runtime.Uploads.[2]
                expectUpload 11UL 1UL [| 1uy |] runtime.Uploads.[3]
                expectUpload 13UL 2UL [| 4uy; 5uy |] runtime.Uploads.[4]
            )

        let constantAdditions() =
            withBuffer<byte, unit> (fun runtime buffer ->
                let view = BufferView([| 5uy; 6uy |])
                use viewWriter = buffer.Add(view, Range1ul(2UL, 3UL))

                let value = AVal.constant 7uy :> IAdaptiveValue
                use valueWriter = buffer.Add(value, 5UL)

                Expect.equal runtime.Uploads.Count 2 "Unexpected constant Add upload count"
                expectUpload 2UL 2UL [| 5uy; 6uy |] runtime.Uploads.[0]
                expectUpload 5UL 1UL [| 7uy |] runtime.Uploads.[1]
            )

        let invalidRangesRemainNoOps() =
            withBuffer<byte, unit> (fun runtime buffer ->
                let invalid = Range1ul(4UL, 3UL)
                buffer.Set(0n, 0UL, invalid)
                buffer.Set(Array.empty<byte>, invalid)

                let source = cval (ArrayBuffer [| 1uy |] :> IBuffer)
                let view = BufferView(source :> aval<IBuffer>, typeof<byte>)
                use ignored = buffer.Add(view, invalid)

                Expect.equal buffer.Size 0UL "Invalid range resized the buffer"
                Expect.equal runtime.Uploads.Count 0 "Invalid range uploaded data"
                Expect.isTrue source.Outputs.IsEmpty "Invalid range subscribed to the input"
            )

        let growthAboveMaximumPowerOfTwo() =
            withBuffer<byte, unit> (fun runtime buffer ->
                let index = 1UL <<< 63
                let value = 1uy
                value |> NativePtr.pin (fun pointer ->
                    buffer.Set(pointer.Address, 1UL, Range1ul(index, index))
                )

                let required = index + 1UL
                Expect.equal buffer.Size required "Capacity wrapped above the largest power of two"
                Expect.equal runtime.CreatedSizes.[runtime.CreatedSizes.Count - 1] required "Unexpected backing-buffer size"
                Expect.equal runtime.Uploads.Count 1 "Unexpected upload count"
                expectUpload index 1UL [| value |] runtime.Uploads.[0]
            )

        let largestRepresentableRange() =
            withBuffer<byte, unit> (fun runtime buffer ->
                let value = 1uy
                value |> NativePtr.pin (fun pointer ->
                    buffer.Set(pointer.Address, UInt64.MaxValue, Range1ul(0UL, UInt64.MaxValue - 1UL))
                )

                Expect.equal buffer.Size UInt64.MaxValue "Largest byte range was not allocated exactly"
                Expect.equal runtime.CreatedSizes.[runtime.CreatedSizes.Count - 1] UInt64.MaxValue "Unexpected byte-buffer size"
                Expect.equal runtime.Uploads.Count 1 "Unexpected byte upload count"
                Expect.equal runtime.Uploads.[0].Offset 0UL "Unexpected byte upload offset"
                Expect.equal runtime.Uploads.[0].Size UInt64.MaxValue "Unexpected byte upload size"
            )

            withBuffer<int, unit> (fun runtime buffer ->
                let elementSize = uint64 sizeof<int>
                let maximumElementCount = UInt64.MaxValue / elementSize
                let index = maximumElementCount - 1UL
                let required = maximumElementCount * elementSize
                buffer.Set(1, index)

                Expect.equal buffer.Size required "Largest typed range was not allocated exactly"
                Expect.equal runtime.CreatedSizes.[runtime.CreatedSizes.Count - 1] required "Unexpected typed-buffer size"
                Expect.equal runtime.Uploads.Count 1 "Unexpected typed upload count"
                Expect.equal runtime.Uploads.[0].Offset (index * elementSize) "Unexpected typed upload offset"
                Expect.equal runtime.Uploads.[0].Size elementSize "Unexpected typed upload size"
            )

        let unrepresentableRangesRejectedBeforeMutation() =
            withBuffer<int, unit> (fun runtime buffer ->
                let maximumElementCount = UInt64.MaxValue / uint64 sizeof<int>
                let value = 1

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun _ -> buffer.Set(value, maximumElementCount))
                    "Expected an overflowing index to be rejected"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun _ ->
                        value |> NativePtr.pin (fun pointer ->
                            buffer.Set(pointer.Address, 1UL, Range1ul(0UL, UInt64.MaxValue))
                        )
                    )
                    "Expected an overflowing range to be rejected"

                Expect.equal buffer.Size 0UL "Rejected range resized the buffer"
                Expect.equal runtime.Uploads.Count 0 "Rejected range uploaded data"
            )

        let oversizedBufferViewDoesNotSubscribe() =
            withBuffer<byte, unit> (fun runtime buffer ->
                let source = cval (ArrayBuffer [| 1uy; 2uy |] :> IBuffer)
                let view = BufferView(source :> aval<IBuffer>, typeof<byte>)
                Expect.isTrue source.Outputs.IsEmpty "Unexpected initial adaptive subscription"
                let oversized = Range1ul(0UL, uint64 Int32.MaxValue)

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun _ -> buffer.Add(view, oversized) |> ignore)
                    "Expected an oversized buffer-view target to be rejected"

                Expect.isTrue source.Outputs.IsEmpty "Failed Add retained an adaptive subscription"
                Expect.equal buffer.Size 0UL "Failed Add resized the buffer"
                Expect.equal runtime.Uploads.Count 0 "Failed Add uploaded data"

                let writer = buffer.Add(view, Range1ul(2UL, 3UL))
                force buffer
                Expect.isFalse source.Outputs.IsEmpty "Valid Add did not subscribe to the input"
                Expect.equal runtime.Uploads.Count 1 "Valid Add did not write after a failed Add"
                expectUpload 2UL 2UL [| 1uy; 2uy |] runtime.Uploads.[0]
                writer.Dispose()

                transact (fun _ -> source.Value <- ArrayBuffer [| 3uy; 4uy |] :> IBuffer)
                let reused = buffer.Add(view, Range1ul(0UL, 1UL))
                force buffer
                Expect.equal runtime.Uploads.Count 2 "Reused writer did not write"
                expectUpload 0UL 2UL [| 3uy; 4uy |] runtime.Uploads.[1]
                reused.Dispose()
            )

        let overflowingValueAddDoesNotSubscribe() =
            withBuffer<byte, unit> (fun runtime buffer ->
                let source = cval 7uy
                let input = source :> IAdaptiveValue
                Expect.isTrue source.Outputs.IsEmpty "Unexpected initial adaptive subscription"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun _ -> buffer.Add(input, UInt64.MaxValue) |> ignore)
                    "Expected an overflowing value target to be rejected"

                Expect.isTrue source.Outputs.IsEmpty "Failed value Add retained an adaptive subscription"
                Expect.equal buffer.Size 0UL "Failed value Add resized the buffer"
                Expect.equal runtime.Uploads.Count 0 "Failed value Add uploaded data"

                let writer = buffer.Add(input, 1UL)
                force buffer
                Expect.isFalse source.Outputs.IsEmpty "Valid value Add did not subscribe to the input"
                Expect.equal runtime.Uploads.Count 1 "Valid value Add did not write after a failed Add"
                expectUpload 1UL 1UL [| 7uy |] runtime.Uploads.[0]
                writer.Dispose()
            )

    [<Tests>]
    let tests =
        testList "ManagedBuffer ranges" [
            testCase "Zero source is rejected before mutation"             Cases.zeroSourceRejectedBeforeMutation
            testCase "Exact and partial repetition"                        Cases.exactAndPartialRepetition
            testCase "Constant additions use validated layouts"            Cases.constantAdditions
            testCase "Invalid ranges remain no-ops"                        Cases.invalidRangesRemainNoOps
            testCase "Growth above the maximum power of two"               Cases.growthAboveMaximumPowerOfTwo
            testCase "Largest representable range"                         Cases.largestRepresentableRange
            testCase "Unrepresentable ranges are rejected before mutation" Cases.unrepresentableRangesRejectedBeforeMutation
            testCase "Oversized BufferView Add rolls back"                 Cases.oversizedBufferViewDoesNotSubscribe
            testCase "Overflowing value Add rolls back"                    Cases.overflowingValueAddDoesNotSubscribe
        ]
