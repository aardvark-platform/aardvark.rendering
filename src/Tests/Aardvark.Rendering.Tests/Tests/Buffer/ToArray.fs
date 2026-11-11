namespace Aardvark.Rendering.Tests.Buffer

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto
open System

module BufferToArray =

    type private NativeArrayBuffer<'T when 'T : unmanaged>(data: 'T[]) =
        let buffer = ArrayBuffer data
        let nativeBuffer = buffer :> INativeBuffer

        member _.ElementType = buffer.ElementType

        interface INativeBuffer with
            member _.SizeInBytes = nativeBuffer.SizeInBytes
            member _.Use(f) = nativeBuffer.Use(f)

    module private Cases =

        let private testToArray (referenceEqual: bool) (expected: 'T[]) (offset: uint64) (stride: uint64) (count: uint64) (buffer: IBuffer) =
            let result = buffer.ToArray<'T>(count = count, offset = offset, stride = stride)
            Expect.equal result expected "Result not equal"
            Expect.equal (obj.ReferenceEquals(result, expected)) referenceEqual "Unexpected reference equality"

        let arrayBufferFull (_: IRuntime) =
            let array = [| 0; 2; 4; 8 |]
            ArrayBuffer array |> testToArray true array 0UL 0UL 42UL

        let arrayBufferRange (_: IRuntime) =
            let buffer = ArrayBuffer [| 0; 2; 4; 8 |]
            let elementSize = uint64 buffer.ElementType.CLRSize
            buffer |> testToArray false [| 2; 4 |] (1UL * elementSize) 0UL 2UL

        let arrayBufferStrided (_: IRuntime) =
            let buffer = ArrayBuffer [| 0; 2; 4; 8 |]
            let elementSize = uint64 buffer.ElementType.CLRSize
            buffer |> testToArray false [| 2; 8 |] (1UL * elementSize) (2UL * elementSize) 2UL

        let arrayBufferOutOfRange (_: IRuntime) =
            let buffer = ArrayBuffer [| 0; 2; 4; 8 |]
            let elementSize = uint64 buffer.ElementType.CLRSize
            buffer |> testToArray false Array.empty<int> (4UL * elementSize) elementSize 1UL
            buffer |> testToArray false [| 4 |] (2UL * elementSize) (2UL * elementSize) 2UL

        let arrayBufferReinterpreted (_: IRuntime) =
            let input = [| 0; 2; 4; 8 |]
            let output = input |> Array.map Fun.FloatFromBits
            ArrayBuffer input |> testToArray false output 0UL 0UL 42UL

        let nativeBufferFull (_: IRuntime) =
            let array = [| 0; 2; 4; 8 |]
            NativeArrayBuffer array |> testToArray false array 0UL 0UL 42UL

        let nativeBufferRange (_: IRuntime) =
            let buffer = NativeArrayBuffer [| 0; 2; 4; 8 |]
            let elementSize = uint64 buffer.ElementType.CLRSize
            buffer |> testToArray false [| 2; 4 |] (1UL * elementSize) 0UL 2UL

        let nativeBufferStrided (_: IRuntime) =
            let buffer = NativeArrayBuffer [| 0; 2; 4; 8 |]
            let elementSize = uint64 buffer.ElementType.CLRSize
            buffer |> testToArray false [| 2; 8 |] (1UL * elementSize) (2UL * elementSize) 2UL

        let nativeBufferOutOfRange (_: IRuntime) =
            let buffer = NativeArrayBuffer [| 0; 2; 4; 8 |]
            let elementSize = uint64 buffer.ElementType.CLRSize
            buffer |> testToArray false Array.empty<int> (4UL * elementSize) elementSize 1UL
            buffer |> testToArray false [| 4 |] (2UL * elementSize) (2UL * elementSize) 2UL

        let nativeBufferReinterpreted (_: IRuntime) =
            let input = [| 0; 2; 4; 8 |]
            let output = input |> Array.map Fun.FloatFromBits
            NativeArrayBuffer input |> testToArray false output 0UL 0UL 42UL

        let backendBufferFull (runtime: IRuntime) =
            let array = [| 0; 2; 4; 8; 16; 32 |]
            use buffer = runtime.PrepareBuffer(ArrayBuffer array)
            buffer |> testToArray false array 0UL 0UL 42UL

        let backendBufferRange (runtime: IRuntime) =
            let array = ArrayBuffer [| 0; 2; 4; 8; 16; 32 |]
            let elementSize = uint64 array.ElementType.CLRSize
            use buffer = runtime.PrepareBuffer(array)
            buffer |> testToArray false [| 8; 16 |] (3UL * elementSize) 0UL 2UL

        let backendBufferStrided (runtime: IRuntime) =
            let array = ArrayBuffer [| 0; 2; 4; 8; 16; 32 |]
            let elementSize = uint64 array.ElementType.CLRSize
            use buffer = runtime.PrepareBuffer(array)
            buffer |> testToArray false [| 2; 16 |] (1UL * elementSize) (3UL * elementSize) 2UL

        let backendBufferOutOfRange (runtime: IRuntime) =
            let array = ArrayBuffer [| 0; 2; 4; 8; 16; 32 |]
            let elementSize = uint64 array.ElementType.CLRSize
            use buffer = runtime.PrepareBuffer(array)
            buffer |> testToArray false Array.empty<int> (6UL * elementSize) elementSize 1UL
            buffer |> testToArray false [| 4 |] (2UL * elementSize) (4UL * elementSize) 2UL

        let backendBufferReinterpreted (runtime: IRuntime) =
            let array = ArrayBuffer [| 0; 2; 4; 8; 16; 32 |]
            use buffer = runtime.PrepareBuffer(array)
            let output = array.Data |> unbox<int[]> |> Array.map Fun.FloatFromBits
            buffer |> testToArray false output 0UL 0UL 42UL

    module private EmptyCases =
        type private NativeMetadata(size : uint64) =
            let mutable reads, uses = 0, 0
            member _.Reads = reads
            member _.Accesses = uses
            interface INativeBuffer with
                member _.SizeInBytes = reads <- reads + 1; size
                member _.Use(_) = uses <- uses + 1; failwith "Native content must not be accessed"

        type private BackendMetadata(size : uint64) =
            let mutable reads, accesses = 0, 0
            let reject() = accesses <- accesses + 1; failwith "Backend content must not be accessed"
            member _.Reads = reads
            member _.Accesses = accesses
            interface IBackendBuffer with
                member _.SizeInBytes = reads <- reads + 1; size
                member _.Runtime = reject()
                member _.Handle = reject()
                member _.Name with get() = reject() and set _ = reject()
                member _.Buffer = reject()
                member _.Offset = reject()
                member _.Dispose() = reject()

        let private requests elementSize size =
            [ for count in [0UL; 1UL; UInt64.MaxValue] do
                for offset in [0UL; 1UL; size / 2UL; (if size = 0UL then 0UL else size - 1UL);
                               size; size + 1UL; size + elementSize; uint64 Int64.MaxValue; UInt64.MaxValue] do
                    for stride in [0UL; elementSize; elementSize + 1UL; 2UL * elementSize; UInt64.MaxValue] do
                        if count = 0UL || offset > size || size - offset < elementSize then
                            yield count, offset, stride ]
            |> List.distinct

        let private collect (failures : ResizeArray<string>) context action =
            try action() with error -> failures.Add(context + ": " + error.Message)

        let private checkEmpty context (t : Type) count offset stride (buffer : IBuffer) =
            let result = buffer.ToArray(t, count, offset, stride)
            Expect.equal (result.GetType()) (t.MakeArrayType()) (context + ": exact array type")
            Expect.equal result.Length 0 (context + ": no complete elements")
            result

        let metadataOnly (_ : IRuntime) =
            let failures = ResizeArray<string>()
            for t in [typeof<byte>; typeof<int>; typeof<int64>; typeof<V3f>] do
                for size in [0UL; 1UL; 3UL; 7UL; 16UL; 23UL] do
                    for count, offset, stride in requests (uint64 t.CLRSize) size do
                        let context = $"type={t}, size={size}, count={count}, offset={offset}, stride={stride}"
                        let native = NativeMetadata size
                        collect failures ("native " + context) (fun () ->
                            checkEmpty context t count offset stride native |> ignore
                            Expect.equal native.Reads 1 (context + ": one size read")
                            Expect.equal native.Accesses 0 (context + ": no Use invocation"))
                        let backend = new BackendMetadata(size)
                        collect failures ("backend " + context) (fun () ->
                            checkEmpty context t count offset stride backend |> ignore
                            Expect.equal backend.Reads 1 (context + ": one size read")
                            Expect.equal backend.Accesses 0 (context + ": no runtime handle or content access"))
            Expect.isEmpty failures (String.concat "\n" failures)

        let arrays (_ : IRuntime) =
            let failures = ResizeArray<string>()
            for t in [typeof<int>; typeof<single>; typeof<int64>; typeof<V3f>; typeof<string>] do
                for length in [0; 3; 4] do
                    let data = Array.CreateInstance(t, length)
                    let buffer = ArrayBuffer data
                    let elementSize = uint64 t.CLRSize
                    for count, offset, stride in requests elementSize buffer.SizeInBytes do
                        let context = $"same-type={t}, length={length}, count={count}, offset={offset}, stride={stride}"
                        collect failures context (fun () ->
                            let result = checkEmpty context t count offset stride buffer
                            let reused = length = 0 && offset = 0UL && (stride = 0UL || stride = elementSize)
                            Expect.equal (obj.ReferenceEquals(data, result)) reused (context + ": whole-array reuse"))
            for data in [Array.zeroCreate<byte> 0 :> Array; Array.zeroCreate<byte> 19 :> Array; [| box 1; box 2 |] :> Array] do
                let buffer = ArrayBuffer data
                for t in [typeof<int>; typeof<single>; typeof<int64>; typeof<V3f>] do
                    for count, offset, stride in requests (uint64 t.CLRSize) buffer.SizeInBytes do
                        let context = $"reinterpreted {data.GetType()} as {t}, count={count}, offset={offset}, stride={stride}"
                        collect failures context (fun () -> checkEmpty context t count offset stride buffer |> ignore)
            Expect.isEmpty failures (String.concat "\n" failures)

        let controls (runtime : IRuntime) =
            for run in [Cases.arrayBufferFull; Cases.arrayBufferRange; Cases.arrayBufferStrided;
                        Cases.arrayBufferOutOfRange; Cases.arrayBufferReinterpreted; Cases.nativeBufferFull;
                        Cases.nativeBufferRange; Cases.nativeBufferStrided; Cases.nativeBufferOutOfRange; Cases.nativeBufferReinterpreted] do
                run runtime
            let data = [| 11; 22; 33; 44 |]
            let array = ArrayBuffer data
            Expect.isTrue (obj.ReferenceEquals(data, array.ToArray<int>())) "Generic whole-array reuse"
            Expect.equal (array.ToArray<int>(count = 0UL, offset = UInt64.MaxValue)) Array.empty "Generic explicit empty"
            Expect.equal (array.ToArray<int>(offset = 15UL, stride = 9UL)) Array.empty "Generic incomplete trailing element"
            let empty = Array.zeroCreate<int> 0
            Expect.isTrue (obj.ReferenceEquals(empty, (ArrayBuffer empty).ToArray<int>(count = 0UL))) "Generic empty whole-array reuse"
            let unsupported = { new IBuffer }
            for count in [0UL; 1UL; UInt64.MaxValue] do
                Expect.throwsT<NotSupportedException> (fun () -> unsupported.ToArray<int>(count = count) |> ignore) $"Unsupported buffer, count={count}"
            let native = NativeMetadata 16UL
            Expect.throws (fun () -> native.ToArray<int>(count = 1UL) |> ignore) "Nonempty native still requests content"
            Expect.equal native.Accesses 1 "Nonempty native Use retained"
            let backend = new BackendMetadata(16UL)
            Expect.throws (fun () -> backend.ToArray<int>(count = 1UL, stride = 8UL) |> ignore) "Nonempty backend still requests content"
            Expect.equal backend.Accesses 1 "Nonempty backend runtime dispatch retained"

        let backend (runtime : IRuntime) =
            use buffer = runtime.PrepareBuffer(ArrayBuffer(Array.init 23 byte))
            for t in [typeof<byte>; typeof<int>; typeof<int64>; typeof<V3f>] do
                for count, offset, stride in requests (uint64 t.CLRSize) buffer.SizeInBytes do
                    let context = $"GPU type={t}, count={count}, offset={offset}, stride={stride}"
                    checkEmpty context t count offset stride buffer |> ignore
            Expect.equal (buffer.ToArray<byte>()) (Array.init 23 byte) "Nonempty content remains intact after empty reads"

    let tests (target: TestTarget) =
        [
            if target = TestTarget.Cpu then
                "Empty reads require metadata only",       EmptyCases.metadataOnly
                "Empty arrays retain types and identity",  EmptyCases.arrays
                "Ordinary reads and errors remain intact", EmptyCases.controls

            if target.IsGpu then
                "Empty ranges leave content intact", EmptyCases.backend

                "ArrayBuffer full",             Cases.arrayBufferFull
                "ArrayBuffer range",            Cases.arrayBufferRange
                "ArrayBuffer strided",          Cases.arrayBufferStrided
                "ArrayBuffer out-of-range",     Cases.arrayBufferOutOfRange
                "ArrayBuffer reinterpreted",    Cases.arrayBufferReinterpreted

                "INativeBuffer full",           Cases.nativeBufferFull
                "INativeBuffer range",          Cases.nativeBufferRange
                "INativeBuffer strided",        Cases.nativeBufferStrided
                "INativeBuffer out-of-range",   Cases.nativeBufferOutOfRange
                "INativeBuffer reinterpreted",  Cases.nativeBufferReinterpreted

                "IBackendBuffer full",          Cases.backendBufferFull
                "IBackendBuffer range",         Cases.backendBufferRange
                "IBackendBuffer strided",       Cases.backendBufferStrided
                "IBackendBuffer out-of-range",  Cases.backendBufferOutOfRange
                "IBackendBuffer reinterpreted", Cases.backendBufferReinterpreted
        ]
        |> prepareCases "ToArray" target