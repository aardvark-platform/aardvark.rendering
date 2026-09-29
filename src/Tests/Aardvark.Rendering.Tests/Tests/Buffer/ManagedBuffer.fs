namespace Aardvark.Rendering.Tests.Buffer

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading.Tasks
open Expecto

module ManagedBuffer =

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

        let test<'T when 'T : unmanaged> (runtime: IRuntime) (action: IManagedBuffer<'T> -> unit) =
            let buffer = runtime.CreateManagedBuffer<'T>()
            buffer.Acquire()
            try action buffer
            finally buffer.Release()

        let testMock<'T when 'T : unmanaged> (action : MockRuntime -> IManagedBuffer<'T> -> unit) =
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

    module Cases =
        open Helpers

        let set (runtime: IRuntime) =
            test<V4f> runtime (fun buffer ->
                let d1 = V4f(1, 2, 3, 4)
                let d3 = V4f(4, 3, 2, 1)

                buffer.Set(d1, 1UL)
                buffer.Set(d3, 3UL)
                let data = buffer.GetValue().Coerce<V4f>().Download()

                Expect.isGreaterThanOrEqual data.Length 4 "Unexpected size"
                Expect.equal data.[1] d1 "Unexpected data at index 1"
                Expect.equal data.[3] d3 "Unexpected data at index 3"
            )

        let setArray (runtime: IRuntime) =
            test<V4f> runtime (fun buffer ->
                let a = [| V4f.One; V4f.Half |]
                let b = [| V4f.IOII; V4f.IIIO; V4f.IIOO |]

                buffer.Set(a, Range1ul(0UL, 0UL))
                buffer.Set(a, Range1ul(1UL, 2UL))
                buffer.Set(b, Range1ul(5UL, 9UL)) // data length < range -> repeat values
                let data = buffer.GetValue().Coerce<V4f>().Download()

                Expect.isGreaterThanOrEqual data.Length 10 "Unexpected size"
                Expect.equal data.[0] a.[0] "Unexpected data at index 0"
                Expect.equal data.[1] a.[0] "Unexpected data at index 1"
                Expect.equal data.[2] a.[1] "Unexpected data at index 2"

                for i = 5 to 9 do
                    let j = (i - 5) % b.Length
                    Expect.equal data.[i] b.[j] $"Unexpected data at index {i}"
            )

        let add (runtime: IRuntime) =
            test<V4f> runtime (fun buffer ->
                let a = cval V4f.IIOO
                let b = cval V4d.Half

                let _  = buffer.Add(a, 1UL)
                let db = buffer.Add(b, 3UL)

                let check (a: V4f) (b: V4d) =
                    let data = buffer.GetValue().Coerce<V4f>().Download()
                    Expect.isGreaterThanOrEqual data.Length 4 "Unexpected size"
                    Expect.equal data.[1] a "Unexpected data at index 1"
                    Expect.equal data.[3] (V4f b) "Unexpected data at index 3"

                check a.Value b.Value

                // Changes should propagate
                transact (fun _ ->
                    a.Value <- a.Value + 1.0f
                    b.Value <- b.Value + 5.0
                )
                check a.Value b.Value

                // Dispose writer for b -> change to b should not propagate
                db.Dispose()
                let pb = b.Value
                transact (fun _ ->
                    a.Value <- a.Value * 2.0f
                    b.Value <- b.Value * 2.0
                )
                check a.Value pb
            )

        let addBuffer (runtime: IRuntime) =
            test<V4f> runtime (fun buffer ->
                let a = cval [| V4f.IIOO; V4f.IOIO |]
                let b = cval [| V4d.Half; V4d.OOII |]
                let ba = BufferView(a |> AVal.map (fun arr -> ArrayBuffer arr :> IBuffer), typeof<V4f>)
                let bb = BufferView(b |> AVal.map (fun arr -> ArrayBuffer arr :> IBuffer), typeof<V4d>)

                let _  = buffer.Add(ba, Range1ul(1UL, 2UL))
                let db = buffer.Add(bb, Range1ul(5UL, 6UL))

                let check (a: V4f[]) (b: V4d[]) =
                    let data = buffer.GetValue().Coerce<V4f>().Download()
                    Expect.isGreaterThanOrEqual data.Length 7 "Unexpected size"
                    Expect.equal data.[1] a.[0] "Unexpected data at index 1"
                    Expect.equal data.[2] a.[1] "Unexpected data at index 2"
                    Expect.equal data.[5] (V4f b.[0]) "Unexpected data at index 5"
                    Expect.equal data.[6] (V4f b.[1]) "Unexpected data at index 6"

                check a.Value b.Value

                // Changes should propagate
                transact (fun _ ->
                    a.Value <- a.Value |> Array.map ((+) 1.0f)
                    b.Value <- b.Value |> Array.map ((+) 5.0)
                )
                check a.Value b.Value

                // Dispose writer for b -> change to b should not propagate
                db.Dispose()
                let pb = b.Value
                transact (fun _ ->
                    a.Value <- a.Value |> Array.map ((*) 2.0f)
                    b.Value <- b.Value |> Array.map ((*) 2.0)
                )
                check a.Value pb
            )

        let zeroSourceIgnored _ =
            testMock<byte> (fun runtime buffer ->
                let target = Range1ul(0UL, 3UL)
                runBounded (fun () -> buffer.Set(0n, 0UL, target))

                let emptyView = BufferView(Array.empty<byte>)
                runBounded (fun () -> buffer.Add(emptyView, target) |> ignore)

                Expect.equal buffer.Size 0UL "Buffer was resized before rejecting the source"
                Expect.equal runtime.Uploads.Count 0 "Data was uploaded before rejecting the source"
            )

        let exactAndPartialRepetition _ =
            testMock<byte> (fun runtime buffer ->
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

        let constantAdditions _ =
            testMock<byte> (fun runtime buffer ->
                let view = BufferView([| 5uy; 6uy |])
                use viewWriter = buffer.Add(view, Range1ul(2UL, 3UL))

                let value = AVal.constant 7uy :> IAdaptiveValue
                use valueWriter = buffer.Add(value, 5UL)

                Expect.equal runtime.Uploads.Count 2 "Unexpected constant Add upload count"
                expectUpload 2UL 2UL [| 5uy; 6uy |] runtime.Uploads.[0]
                expectUpload 5UL 1UL [| 7uy |] runtime.Uploads.[1]
            )

        let invalidRangesRemainNoOps _ =
            testMock<byte> (fun runtime buffer ->
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

        let growthAboveMaximumPowerOfTwo _ =
            testMock<byte> (fun runtime buffer ->
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

        let largestRepresentableRange _ =
            testMock<byte> (fun runtime buffer ->
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

            testMock<int> (fun runtime buffer ->
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

        let unrepresentableRangesRejectedBeforeMutation _ =
            testMock<int> (fun runtime buffer ->
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

        let oversizedBufferViewDoesNotSubscribe _ =
            testMock<byte> (fun runtime buffer ->
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

        let overflowingValueAddDoesNotSubscribe _ =
            testMock<byte> (fun runtime buffer ->
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

    let tests (target: TestTarget) =
        [
            if target = TestTarget.Cpu then
                "Zero source is ignored",                              Cases.zeroSourceIgnored
                "Exact and partial repetition",                        Cases.exactAndPartialRepetition
                "Constant additions use validated layouts",            Cases.constantAdditions
                "Invalid ranges remain no-ops",                        Cases.invalidRangesRemainNoOps
                "Growth above the maximum power of two",               Cases.growthAboveMaximumPowerOfTwo
                "Largest representable range",                         Cases.largestRepresentableRange
                "Unrepresentable ranges are rejected before mutation", Cases.unrepresentableRangesRejectedBeforeMutation
                "Oversized BufferView Add rolls back",                 Cases.oversizedBufferViewDoesNotSubscribe
                "Overflowing value Add rolls back",                    Cases.overflowingValueAddDoesNotSubscribe

            elif target.IsGpu then
                "Set",        Cases.set
                "Set array",  Cases.setArray
                "Add",        Cases.add
                "Add buffer", Cases.addBuffer
        ]
        |> prepareCases "ManagedBuffer" target