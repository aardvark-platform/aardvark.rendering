namespace Aardvark.Rendering.Tests.Buffer

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
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

    module Cases =

        let invalidArgs (_: IRuntime) =
            let buffer = ArrayBuffer [| 0; 2; 4; 8 |]
            let elementSize = uint64 buffer.ElementType.CLRSize
            Expect.throwsT<ArgumentOutOfRangeException> (fun _ -> buffer.ToArray(buffer.ElementType, 5UL) |> ignore) "Expected out-of-range"
            Expect.throwsT<ArgumentOutOfRangeException> (fun _ -> buffer.ToArray(buffer.ElementType, 4UL, offset = 1UL * elementSize) |> ignore) "Expected out-of-range"
            Expect.throwsT<ArgumentOutOfRangeException> (fun _ -> buffer.ToArray(buffer.ElementType, 2UL, offset = 1UL * elementSize, stride = 3UL * elementSize) |> ignore) "Expected out-of-range"

        let testToArray (referenceEqual: bool) (expected: 'T[]) (offset: uint64) (stride: uint64) (count: uint64) (buffer: IBuffer) =
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

    let tests (backend : Backend) =
        [
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
        |> prepareCases backend "ToArray"