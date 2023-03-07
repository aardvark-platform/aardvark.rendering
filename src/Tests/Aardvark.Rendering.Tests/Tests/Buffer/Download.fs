namespace Aardvark.Rendering.Tests.Buffer

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open FSharp.Data.Adaptive
open FShade
open Expecto
open System

module BufferDownload =

    module Cases =

        let private testDownload (totalCount : int)
                                 (getRandomValue : unit -> 'T)
                                 (rangeStart : int)
                                 (rangeCount : int)
                                 (downloadData : IBackendBuffer -> 'T[] -> unit)
                                 (runtime : IRuntime) =

            use buffer = runtime.CreateBuffer<'T>(totalCount)
            let data = Array.init totalCount (ignore >> getRandomValue)
            buffer.Upload(data)

            let result = Array.zeroCreate<'T> rangeCount
            downloadData buffer.Buffer result

            let expected = Span<'T>(data).Slice(rangeStart, rangeCount)
            for i = 0 to rangeCount - 1 do
                Expect.equal expected.[i] result.[i] "unexpected value in downloaded data"

        let private testNativeDownload (totalCount : int) (rangeStart : int) (rangeCount : int) =
            let download (buffer : IBackendBuffer) (dst : uint8[]) =
                pinned dst (fun dst ->
                    buffer.Download(nativeint rangeStart, dst, nativeint rangeCount)
                )

            testDownload totalCount Rnd.uint8 rangeStart rangeCount download

        let private testArrayDownload (getRandomValue : unit -> 'T) (totalCount : int) (rangeStart : int) (rangeCount : int) =
            let download (buffer : IBackendBuffer) (dst : 'T[]) =
                let buffer = buffer.Coerce<'T>()
                buffer.Download(dst, rangeStart, 0, rangeCount)

            testDownload totalCount getRandomValue rangeStart rangeCount download

        let private testArrayBufferRangeDownload (getRandomValue : unit -> 'T) (totalCount : int) (rangeStart : int) (rangeCount : int) =
            let download (buffer : IBackendBuffer) (dst : 'T[]) =
                let buffer = buffer.Coerce<'T>()
                buffer.[rangeStart .. rangeStart + rangeCount - 1].Download(dst)

            testDownload totalCount getRandomValue rangeStart rangeCount download

        let invalidArgs (runtime : IRuntime) =
            use buffer = runtime.CreateBuffer<uint8>(128)
            let data = Array.zeroCreate<uint8> 8

            Expect.throwsT<ArgumentException> (fun _ -> runtime.CreateBuffer(-1n) |> ignore) "Expected ArgumentException due to negative size on create"
            Expect.throwsT<ArgumentException> (fun _ -> buffer.[-1 .. 2] |> ignore) "Expected ArgumentException due to negative index on slice"
            Expect.throwsT<ArgumentException> (fun _ -> buffer.[3 .. 2] |> ignore) "Expected ArgumentException due to invalid slice"
            Expect.throwsT<ArgumentException> (fun _ -> buffer.[3 .. 128] |> ignore) "Expected ArgumentException due to out-of-bounds slice"

            Expect.throwsT<ArgumentException> (fun _ -> buffer.Buffer.Download(-1n, 0n, 12n)) "Expected ArgumentException due to negative src offset on download"
            Expect.throwsT<ArgumentException> (fun _ -> buffer.Buffer.Download(0n, 0n, -1n)) "Expected ArgumentException due to negative size on download"
            Expect.throwsT<ArgumentException> (fun _ -> buffer.Buffer.Download(128n, 0n, 1n)) "Expected ArgumentException due to out-of-bounds src range on download"

            Expect.throwsT<ArgumentException> (fun _ -> buffer.Download(data, 0, -1, 1)) "Expected ArgumentException due to negative dst array index on download"
            Expect.throwsT<ArgumentException> (fun _ -> buffer.Download(data, 0, 0, -1)) "Expected ArgumentException due to negative array size on download"
            Expect.throwsT<ArgumentException> (fun _ -> buffer.Download(data, 0, 8, 1)) "Expected ArgumentException due out-of-bounds array region on download"

        let native                  = testNativeDownload 2345 0 2345
        let nativeSubrange          = testNativeDownload 2345 57 345

        let arrayUint8              = testArrayDownload Rnd.uint8 7533 0 7533
        let arrayUint8Subrange      = testArrayDownload Rnd.uint8 7533 5432 1243
        let arrayUint8BufferRange   = testArrayBufferRangeDownload Rnd.uint8 7533 5432 1243

        let arrayUint16             = testArrayDownload Rnd.uint16 7533 0 7533
        let arrayUint16Subrange     = testArrayDownload Rnd.uint16 7533 5432 1243
        let arrayUint16BufferRange  = testArrayBufferRangeDownload Rnd.uint16 7533 5432 1243


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
        |> prepareCases backend "Download"