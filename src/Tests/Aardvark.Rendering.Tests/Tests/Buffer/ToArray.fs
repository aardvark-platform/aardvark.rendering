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
            member _.Pin() = nativeBuffer.Pin()
            member _.Unpin() = nativeBuffer.Unpin()
            member _.Use(f) = nativeBuffer.Use(f)

    module Cases =

        let invalidArgs (_: IRuntime) =
            let buffer = ArrayBuffer [| 0; 2; 4; 8 |]
            let elementSize = uint64 buffer.ElementType.CLRSize
            Expect.throwsT<ArgumentOutOfRangeException> (fun _ -> buffer.ToArray(buffer.ElementType, 5UL) |> ignore) "Expected out-of-range"
            Expect.throwsT<ArgumentOutOfRangeException> (fun _ -> buffer.ToArray(buffer.ElementType, 4UL, offset = 1UL * elementSize) |> ignore) "Expected out-of-range"
            Expect.throwsT<ArgumentOutOfRangeException> (fun _ -> buffer.ToArray(buffer.ElementType, 2UL, offset = 1UL * elementSize, stride = 3UL * elementSize) |> ignore) "Expected out-of-range"

        let testToArray (referenceEqual: bool) (expected: 'T[]) (offset: uint64) (stride: uint64) (buffer: IBuffer) =
            let result = buffer.ToArray<'T>(uint64 expected.Length, offset = offset, stride = stride)
            Expect.equal result expected "Result not equal"
            Expect.equal (obj.ReferenceEquals(result, expected)) referenceEqual "Unexpected reference equality"

        let arrayBufferFull (_: IRuntime) =
            let array = [| 0; 2; 4; 8 |]
            ArrayBuffer array |> testToArray true array 0UL 0UL

        let arrayBufferRange (_: IRuntime) =
            let buffer = ArrayBuffer [| 0; 2; 4; 8 |]
            let elementSize = uint64 buffer.ElementType.CLRSize
            buffer |> testToArray false [| 2; 4 |] (1UL * elementSize) 0UL

        let arrayBufferStrided (_: IRuntime) =
            let buffer = ArrayBuffer [| 0; 2; 4; 8 |]
            let elementSize = uint64 buffer.ElementType.CLRSize
            buffer |> testToArray false [| 2; 8 |] (1UL * elementSize) (2UL * elementSize)

        let nativeBufferFull (_: IRuntime) =
            let array = [| 0; 2; 4; 8 |]
            NativeArrayBuffer array |> testToArray false array 0UL 0UL

        let nativeBufferRange (_: IRuntime) =
            let buffer = NativeArrayBuffer [| 0; 2; 4; 8 |]
            let elementSize = uint64 buffer.ElementType.CLRSize
            buffer |> testToArray false [| 2; 4 |] (1UL * elementSize) 0UL

        let nativeBufferStrided (_: IRuntime) =
            let buffer = NativeArrayBuffer [| 0; 2; 4; 8 |]
            let elementSize = uint64 buffer.ElementType.CLRSize
            buffer |> testToArray false [| 2; 8 |] (1UL * elementSize) (2UL * elementSize)

        let backendBufferFull (runtime: IRuntime) =
            let array = [| 0; 2; 4; 8; 16; 32 |]
            use buffer = runtime.PrepareBuffer(ArrayBuffer array)
            buffer |> testToArray false array 0UL 0UL

        let backendBufferRange (runtime: IRuntime) =
            let array = ArrayBuffer [| 0; 2; 4; 8; 16; 32 |]
            let elementSize = uint64 array.ElementType.CLRSize
            use buffer = runtime.PrepareBuffer(array)
            buffer |> testToArray false [| 8; 16 |] (3UL * elementSize) 0UL

        let backendBufferStrided (runtime: IRuntime) =
            let array = ArrayBuffer [| 0; 2; 4; 8; 16; 32 |]
            let elementSize = uint64 array.ElementType.CLRSize
            use buffer = runtime.PrepareBuffer(array)
            buffer |> testToArray false [| 2; 16 |] (1UL * elementSize) (3UL * elementSize)

    let tests (backend : Backend) =
        [
            "Invalid arguments",        Cases.invalidArgs

            "ArrayBuffer full",         Cases.arrayBufferFull
            "ArrayBuffer range",        Cases.arrayBufferRange
            "ArrayBuffer strided",      Cases.arrayBufferStrided

            "INativeBuffer full",       Cases.nativeBufferFull
            "INativeBuffer range",      Cases.nativeBufferRange
            "INativeBuffer strided",    Cases.nativeBufferStrided

            "IBackendBuffer full",      Cases.backendBufferFull
            "IBackendBuffer range",     Cases.backendBufferRange
            "IBackendBuffer strided",   Cases.backendBufferStrided

        ]
        |> prepareCases backend "ToArray"