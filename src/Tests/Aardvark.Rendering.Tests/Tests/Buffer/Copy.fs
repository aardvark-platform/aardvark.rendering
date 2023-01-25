namespace Aardvark.Rendering.Tests.Buffer

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open FSharp.Data.Adaptive
open FShade
open Expecto
open System

module BufferCopy =

    module Cases =

        let private testCopy (totalCount : int)
                             (getRandomValue : unit -> 'T)
                             (srcStart : int)
                             (dstStart : int)
                             (rangeCount : int)
                             (copyData : IBackendBuffer -> IBackendBuffer -> unit)
                             (runtime : IRuntime) =

            use src = runtime.CreateBuffer<'T>(totalCount)
            let data = Array.init totalCount (ignore >> getRandomValue)
            src.Upload(data)

            let initialValue = getRandomValue()
            use dst = runtime.CreateBuffer<'T>(totalCount)
            dst.[0 .. totalCount - 1] <- initialValue
            copyData src.Buffer dst.Buffer

            let result = dst.Download()

            // Check values before range
            for i = 0 to dstStart - 1 do
                Expect.equal result.[i] initialValue "unexpected value before modified range"

            // Check values after range
            for i = dstStart + rangeCount to totalCount - 1 do
                Expect.equal result.[i] initialValue "unexpected value after modified range"

            // Check modified values in range
            for i = 0 to rangeCount - 1 do
                Expect.equal result.[dstStart + i] data.[srcStart + i] "unexpected value in modified range"


        let private testNativeCopy (totalCount : int) (srcStart : int) (dstStart : int) (rangeCount : int) =
            let copy (src : IBackendBuffer) (dst : IBackendBuffer) =
                src.CopyTo(nativeint srcStart, dst, nativeint dstStart, nativeint rangeCount)

            testCopy totalCount Rnd.uint8 srcStart dstStart rangeCount copy

        let private testBufferRangeCopy (getRandomValue : unit -> 'T) (totalCount : int) (srcStart : int) (dstStart : int) (rangeCount : int) =
            let copy (src : IBackendBuffer) (dst : IBackendBuffer) =
                let src = src.Coerce<'T>()
                let dst = dst.Coerce<'T>()
                src.[srcStart .. srcStart + rangeCount - 1].CopyTo(dst.[dstStart .. dstStart + rangeCount - 1])

            testCopy totalCount getRandomValue srcStart dstStart rangeCount copy

        let invalidArgs (runtime : IRuntime) =
            let src = runtime.CreateBuffer(128n)
            let dst = runtime.CreateBuffer(128n)

            Expect.throwsT<ArgumentException> (fun _ -> runtime.CreateBuffer(-1n) |> ignore) "Expected ArgumentException due to negative size on create"

            Expect.throwsT<ArgumentException> (fun _ -> src.CopyTo(-1n, dst, 0n, 0n)) "Expected ArgumentException due to negative src offset on copy"
            Expect.throwsT<ArgumentException> (fun _ -> src.CopyTo(0n, dst, -1n, 0n)) "Expected ArgumentException due to negative dst offset on copy"
            Expect.throwsT<ArgumentException> (fun _ -> src.CopyTo(0n, dst, 0n, -1n)) "Expected ArgumentException due to negative size on copy"
            Expect.throwsT<ArgumentException> (fun _ -> src.CopyTo(128n, dst, 0n, 1n)) "Expected ArgumentException due to out-of-bounds src range on copy"
            Expect.throwsT<ArgumentException> (fun _ -> src.CopyTo(0n, dst, 128n, 1n)) "Expected ArgumentException due to out-of-bounds dst range on copy"

        let native                  = testNativeCopy 2345 0 0 2345
        let nativeSubrange          = testNativeCopy 2345 57 89 345

        let arrayUint8BufferRange   = testBufferRangeCopy Rnd.uint8 7533 5432 2341 1243
        let arrayUint16BufferRange  = testBufferRangeCopy Rnd.uint16 7533 5432 2341 1243
        let arrayUint32BufferRange  = testBufferRangeCopy Rnd.uint32 7533 5432 2341 1243
        let arrayUint64BufferRange  = testBufferRangeCopy Rnd.uint64 7533 5432 2341 1243

    let tests (backend : Backend) =
        [
            "Invalid arguments",    Cases.invalidArgs

            "Native",               Cases.native
            "Native subrange",      Cases.nativeSubrange

            "Buffer range uint8",   Cases.arrayUint8BufferRange
            "Buffer range uint16",  Cases.arrayUint16BufferRange
            "Buffer range uint32",  Cases.arrayUint32BufferRange
            "Buffer range uint64",  Cases.arrayUint64BufferRange
        ]
        |> prepareCases backend "Copy"