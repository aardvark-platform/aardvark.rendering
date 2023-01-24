namespace Aardvark.Rendering.Tests.Buffer

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open FSharp.Data.Adaptive
open FShade
open Expecto
open System

module BufferUpload =

    module Cases =

        let private testUpload (totalCount : int)
                               (initialValue : 'T)
                               (rangeData : 'T[])
                               (rangeStart : int)
                               (uploadData : IBackendBuffer -> unit)
                               (runtime : IRuntime) =

            use buffer = runtime.CreateBuffer<'T>(totalCount)
            buffer.[0 .. totalCount - 1] <- initialValue

            uploadData buffer.Buffer

            let result = buffer.Download()
            Expect.equal result.Length totalCount "unexpected result length"

            // Check values before range
            for i = 0 to rangeStart - 1 do
                Expect.equal result.[i] initialValue "unexpected value before modified range"

            // Check values after range
            for i = rangeStart + rangeData.Length to totalCount - 1 do
                Expect.equal result.[i] initialValue "unexpected value before modified range"

            // Check modified values in range
            let range = Span<'T>(result).Slice(rangeStart, rangeData.Length)
            for i = 0 to range.Length - 1 do
                Expect.equal range.[i] rangeData.[i] "unexpected value in modified range"

        let private testNativeUpload (totalCount : int) (rangeStart : int) (rangeCount : int) =
            let rangeData = Array.init<uint8> rangeCount (ignore >> Rnd.uint8)
            let initialValue = 127uy

            let upload (buffer : IBackendBuffer) =
                pinned rangeData (fun src ->
                    buffer.Upload(nativeint rangeStart, src, nativeint rangeCount)
                )

            testUpload totalCount initialValue rangeData rangeStart upload

        let private testArrayUpload (getRandomValue : unit -> 'T) (totalCount : int) (rangeStart : int) (rangeCount : int) =
            let rangeData = Array.init<'T> rangeCount (ignore >> getRandomValue)
            let initialValue = getRandomValue()

            let upload (buffer : IBackendBuffer) =
                let buffer = buffer.Coerce<'T>()
                buffer.Upload(rangeData, 0, rangeStart, rangeCount)

            testUpload totalCount initialValue rangeData rangeStart upload

        let private testArrayBufferRangeUpload (getRandomValue : unit -> 'T) (totalCount : int) (rangeStart : int) (rangeCount : int) =
            let rangeData = Array.init<'T> rangeCount (ignore >> getRandomValue)
            let initialValue = getRandomValue()

            let upload (buffer : IBackendBuffer) =
                let buffer = buffer.Coerce<'T>()
                buffer.[rangeStart .. rangeStart + rangeCount - 1].Upload(rangeData)

            testUpload totalCount initialValue rangeData rangeStart upload

        let invalidArgs (runtime : IRuntime) =
            let buffer = runtime.CreateBuffer<uint8>(128)
            let data = Array.zeroCreate<uint8> 8

            Expect.throwsT<ArgumentException> (fun _ -> runtime.CreateBuffer(-1n) |> ignore) "Expected ArgumentException due to negative size on create"

            Expect.throwsT<ArgumentException> (fun _ -> buffer.Buffer.Upload(-1n, 0n, 12n)) "Expected ArgumentException due to negative dst offset on upload"
            Expect.throwsT<ArgumentException> (fun _ -> buffer.Buffer.Upload(0n, 0n, -1n)) "Expected ArgumentException due to negative size on upload"
            Expect.throwsT<ArgumentException> (fun _ -> buffer.Buffer.Upload(128n, 0n, 1n)) "Expected ArgumentException due to out-of-bounds dst range on upload"

            Expect.throwsT<ArgumentException> (fun _ -> buffer.Upload(data, -1, 0, 1)) "Expected ArgumentException due to negative src array index on upload"
            Expect.throwsT<ArgumentException> (fun _ -> buffer.Upload(data, 0, 0, -1)) "Expected ArgumentException due to negative array size on upload"
            Expect.throwsT<ArgumentException> (fun _ -> buffer.Upload(data, 8, 0, 1)) "Expected ArgumentException due to out-of-bounds array region on upload"
            Expect.throwsT<ArgumentException> (fun _ -> buffer.Elements(126).Upload(data)) "Expected ArgumentException due to out-of-bounds for array length on upload"

        let native                  = testNativeUpload 2345 0 2345
        let nativeSubrange          = testNativeUpload 2345 57 345

        let arrayUint8              = testArrayUpload Rnd.uint8 7533 0 7533
        let arrayUint8Subrange      = testArrayUpload Rnd.uint8 7533 5432 1243
        let arrayUint8BufferRange   = testArrayBufferRangeUpload Rnd.uint8 7533 5432 1243

        let arrayUint16             = testArrayUpload Rnd.uint16 7533 0 7533
        let arrayUint16Subrange     = testArrayUpload Rnd.uint16 7533 5432 1243
        let arrayUint16BufferRange  = testArrayBufferRangeUpload Rnd.uint16 7533 5432 1243


    let tests (backend : Backend) =
        [
            "Invalid arguments",         Cases.invalidArgs

            "Native",                    Cases.native
            "Native subrange",           Cases.nativeSubrange

            "Array uint8",               Cases.arrayUint8
            "Array uint8 subrange",      Cases.arrayUint8Subrange
            "Array uint8 buffer range",  Cases.arrayUint8BufferRange

            "Array uint16",              Cases.arrayUint16
            "Array uint16 subrange",     Cases.arrayUint16Subrange
            "Array uint16 buffer range", Cases.arrayUint16BufferRange
        ]
        |> prepareCases backend "Upload"