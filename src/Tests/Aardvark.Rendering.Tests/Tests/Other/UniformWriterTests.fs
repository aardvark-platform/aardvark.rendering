namespace Aardvark.Rendering.Tests.Other

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto

module UniformWriter =

    module private Arr =

        let private targetCount = 3
        let private stride = 16
        let private elementSize = sizeof<int>
        let private targetSize = (targetCount - 1) * stride + elementSize
        let private guardSize = 64
        let private guardByte = 0xA5uy
        let private targetByte = 0xC7uy

        let private verifyArrWrite<'d when 'd :> INatural> (values : int[]) =
            let target = FShade.GLSL.Array(targetCount, FShade.GLSL.Int(true, 32), stride)
            let writer = UniformWriters.getWriter 0 target typeof<Arr<'d, int>>
            let source = Arr<'d, int>(values)

            Expect.equal writer.TargetSize (nativeint targetSize) "TargetSize must describe exactly the shader array storage"

            let memory = Array.create (guardSize + targetSize + guardSize) guardByte
            Array.fill memory guardSize targetSize targetByte

            let expected = Array.create targetSize targetByte
            let writeCount = min values.Length targetCount

            for i in 0 .. writeCount - 1 do
                Buffer.BlockCopy(BitConverter.GetBytes values.[i], 0, expected, i * stride, elementSize)

            let firstEmptyByte =
                if writeCount > 0 then
                    (writeCount - 1) * stride + elementSize
                else
                    0

            if firstEmptyByte < targetSize then
                Array.Clear(expected, firstEmptyByte, targetSize - firstEmptyByte)

            let handle = GCHandle.Alloc(memory, GCHandleType.Pinned)
            try
                let ptr = handle.AddrOfPinnedObject() + nativeint guardSize
                writer.WriteUnsafeValue(source, ptr)
            finally
                handle.Free()

            Expect.sequenceEqual memory.[0 .. guardSize - 1] (Array.create guardSize guardByte) "The leading guard must remain unchanged"
            Expect.sequenceEqual memory.[guardSize .. guardSize + targetSize - 1] expected "The target payload and zero-filled storage must match"
            Expect.sequenceEqual memory.[guardSize + targetSize ..] (Array.create guardSize guardByte) "The trailing guard must remain unchanged"

        let emptyArr() = verifyArrWrite<N<0>> [||]
        let shortArr() = verifyArrWrite<N<2>> [| 0x1020304; 0x11223344 |]
        let exactArr() = verifyArrWrite<N<3>> [| 0x1020304; 0x11223344; 0x55667788 |]
        let longArr() = verifyArrWrite<N<5>> [| 0x1020304; 0x11223344; 0x55667788; 0x12345678; 0x76543210 |]

    module private Properties =

        [<Struct>]
        type private Single = { Value : int }

        [<Struct>]
        type private Multiple = { Value : int; Scale : float; Stamp : int64 }

        [<Struct>]
        type private Computed =
            { A : int64; B : int64; Padding : V4i }
            member x.Value = int (3L * x.A - x.B)
            member x.Scale = float (x.A + x.B) / 2.0

        [<Struct>]
        type private Nested =
            { Seed : Multiple; Bias : int64 }
            member x.Child =
                { Value = x.Seed.Value + int x.Bias
                  Scale = x.Seed.Scale + float x.Bias
                  Stamp = x.Seed.Stamp - x.Bias }
            member x.Marker = int x.Bias

        type private Reference = { Value : int; Scale : float }

        type private Virtual(value : int, scale : float) =
            abstract Value : int
            default _.Value = value
            abstract Scale : float
            default _.Scale = scale

        type private Derived(value : int, scale : float) =
            inherit Virtual(value, scale)
            override _.Value = value + 17
            override _.Scale = scale * 2.0

        let private getterError = InvalidOperationException("Uniform getter failure")

        [<Struct>]
        type private ThrowingStruct =
            { Padding : V4i; MorePadding : V4i }
            member _.Value : int = raise getterError

        type private ThrowingReference() =
            member _.Value : int = raise getterError

        let private intType = FShade.GLSL.Int(true, 32)
        let private doubleType = FShade.GLSL.Float 64
        let private singleType = FShade.GLSL.Struct("SingleProperty", ["Value", intType, 0], 4)
        let private pairType = FShade.GLSL.Struct("PropertyPair", ["Value", intType, 0; "Scale", doubleType, 16], 32)
        let private multipleType =
            FShade.GLSL.Struct("MultipleProperties", ["Value", intType, 0; "Scale", doubleType, 8; "Stamp", FShade.GLSL.Int(true, 64), 24], 32)

        let private guardSize = 64
        let private guardByte = 0xA5uy
        let private payloadByte = 0xC7uy

        let private write<'T> (mode : string) (writer : UniformWriters.IWriter) (value : 'T)
                             (adaptive : cval<'T>) (ptr : nativeint) =
            match mode with
            | "typed" -> (writer :?> UniformWriters.IWriter<'T>).WriteValue(value, ptr)
            | "boxed" -> writer.WriteUnsafeValue(box value, ptr)
            | "adaptive" -> writer.Write(AdaptiveToken.Top, adaptive, ptr)
            | _ -> failtestf "Unknown write mode: %s" mode

        let private verify<'T> (context : string) target size (samples : list<'T * list<int * byte[]>>) =
            let root = UniformWriters.getWriter 0 target typeof<'T>
            Expect.isTrue (Object.ReferenceEquals(root, UniformWriters.getWriter 0 target typeof<'T>))
                $"{context}: warmed writer lookup must retain identity"
            Expect.isTrue (Object.ReferenceEquals(root, root.WithOffset 0n)) $"{context}: zero offset must retain identity"
            Expect.equal root.ValueType typeof<'T> $"{context}: source type"
            Expect.equal root.TargetSize (nativeint size) $"{context}: target size"

            let adaptive = AVal.init (fst samples.Head)
            for offset in [8; 24] do
                let writer = UniformWriters.getWriter offset target typeof<'T>
                for sample, (value, patches) in List.indexed samples do
                    transact (fun () -> adaptive.Value <- value)
                    for mode in ["typed"; "boxed"; "adaptive"] do
                        let context = $"{context}, sample={sample}, offset={offset}, mode={mode}"
                        let memory = Array.create (guardSize + offset + size + guardSize) guardByte
                        Array.fill memory (guardSize + offset) size payloadByte
                        let expected = Array.copy memory
                        for index, bytes in patches do
                            Buffer.BlockCopy(bytes, 0, expected, guardSize + offset + index, bytes.Length)
                        let handle = GCHandle.Alloc(memory, GCHandleType.Pinned)
                        try
                            try write mode writer value adaptive (handle.AddrOfPinnedObject() + nativeint guardSize)
                            with error -> failtestf "%s: %O" context error
                        finally
                            handle.Free()
                        Expect.sequenceEqual memory expected $"{context}: exact payload, padding, offset prefix and guards"

        let private multipleBytes (value : int) (scale : float) (stamp : int64) =
            [0, BitConverter.GetBytes value; 8, BitConverter.GetBytes scale; 24, BitConverter.GetBytes stamp]

        let singleRecord() =
            verify "single-field struct record" singleType 4 [
                { Value = 0x1020304 }, [0, BitConverter.GetBytes 0x1020304]
                { Value = -173 }, [0, BitConverter.GetBytes -173]
            ]

        let multipleRecord() =
            verify "multi-field struct record" multipleType 32 [
                { Value = 0; Scale = 0.0; Stamp = 0L }, multipleBytes 0 0.0 0L
                { Value = 0x1020304; Scale = 1.25; Stamp = 0x102030405060708L }, multipleBytes 0x1020304 1.25 0x102030405060708L
                { Value = -173; Scale = -7.5; Stamp = -123456789012345L }, multipleBytes -173 -7.5 -123456789012345L
            ]

        let computedGetters() =
            verify "computed struct getters" pairType 32 [
                { A = 0L; B = 0L; Padding = V4i.Zero }, [0, BitConverter.GetBytes 0; 16, BitConverter.GetBytes 0.0]
                { A = 11L; B = 5L; Padding = V4i(101, 202, 303, 404) }, [0, BitConverter.GetBytes 28; 16, BitConverter.GetBytes 8.0]
                { A = -7L; B = 4L; Padding = V4i(404, 303, 202, 101) }, [0, BitConverter.GetBytes -25; 16, BitConverter.GetBytes -1.5]
            ]

        let nestedProperties() =
            let target = FShade.GLSL.Struct("NestedProperties", ["Child", multipleType, 8; "Marker", intType, 48], 64)
            verify "nested struct-property result" target 64 [
                { Seed = { Value = 23; Scale = 1.25; Stamp = 91L }; Bias = 3L },
                    [8, BitConverter.GetBytes 26; 16, BitConverter.GetBytes 4.25; 32, BitConverter.GetBytes 88L; 48, BitConverter.GetBytes 3]
                { Seed = { Value = -13; Scale = -7.5; Stamp = -123L }; Bias = -2L },
                    [8, BitConverter.GetBytes -15; 16, BitConverter.GetBytes -9.5; 32, BitConverter.GetBytes -121L; 48, BitConverter.GetBytes -2]
            ]

        let structArrays() =
            let count, stride, size = 3, 48, 128
            let target = FShade.GLSL.Array(count, multipleType, stride)
            let inputs : Multiple[] = [|
                { Value = 13; Scale = 1.25; Stamp = 91L }
                { Value = -7; Scale = -2.5; Stamp = -123L }
                { Value = 41; Scale = 8.0; Stamp = 123456789L }
                { Value = 999; Scale = 999.0; Stamp = 999L }
                { Value = 888; Scale = 888.0; Stamp = 888L }
            |]
            let literalBytes = [|
                multipleBytes 13 1.25 91L
                multipleBytes -7 -2.5 -123L
                multipleBytes 41 8.0 123456789L
            |]
            for length in [0; 1; 3; 5] do
                let written = min length count
                let patches = [
                    for i in 0 .. written - 1 do
                        for offset, bytes in literalBytes.[i] do
                            yield i * stride + offset, bytes
                    let firstEmpty = written * stride
                    if firstEmpty < size then
                        yield firstEmpty, Array.zeroCreate (size - firstEmpty)
                ]
                verify $"struct array, input length={length}" target size [inputs.[0 .. length - 1], patches]

        let dispatchControls() =
            verify "reference-record property" pairType 32 [
                { Value = 23; Scale = 1.25 }, [0, BitConverter.GetBytes 23; 16, BitConverter.GetBytes 1.25]
                { Value = -13; Scale = -7.5 }, [0, BitConverter.GetBytes -13; 16, BitConverter.GetBytes -7.5]
            ]
            verify<Virtual> "reference virtual getter dispatch" pairType 32 [
                Derived(23, 1.25), [0, BitConverter.GetBytes 40; 16, BitConverter.GetBytes 2.5]
                Derived(-13, -7.5), [0, BitConverter.GetBytes 4; 16, BitConverter.GetBytes -15.0]
            ]
            Expect.isNotNull (typeof<V2i>.GetField "X") "the control must use a public struct field, not an F# val property"
            let target = FShade.GLSL.Struct("PublicFields", ["X", intType, 0; "Y", intType, 16], 32)
            verify "public struct-field access" target 32 [
                V2i(23, 37), [0, BitConverter.GetBytes 23; 16, BitConverter.GetBytes 37]
                V2i(-13, -7), [0, BitConverter.GetBytes -13; 16, BitConverter.GetBytes -7]
            ]

        let private verifyException<'T> (context : string) (value : 'T) =
            let adaptive = AVal.init value
            for offset in [8; 24] do
                let writer = UniformWriters.getWriter offset singleType typeof<'T>
                for mode in ["typed"; "boxed"; "adaptive"] do
                    let context = $"{context}, offset={offset}, mode={mode}"
                    let memory = Array.create (guardSize + offset + 4 + guardSize) guardByte
                    let handle = GCHandle.Alloc(memory, GCHandleType.Pinned)
                    try
                        let mutable caught = None
                        try write mode writer value adaptive (handle.AddrOfPinnedObject() + nativeint guardSize)
                        with error -> caught <- Some error
                        match caught with
                        | Some error -> Expect.isTrue (Object.ReferenceEquals(error, getterError)) $"{context}: preserve getter exception identity"
                        | None -> failtestf "%s: getter exception did not propagate" context
                    finally
                        handle.Free()
                    Expect.sequenceEqual memory (Array.create memory.Length guardByte) $"{context}: no payload or guard mutation"

        let getterExceptions() =
            verifyException "struct getter" { Padding = V4i.Zero; MorePadding = V4i.Zero }
            verifyException "reference getter" (ThrowingReference())

    let tests (target: TestTarget) =
        [
            "empty Arr stays inside target and zero-fills it",        Arr.emptyArr
            "short Arr writes values and zero-fills missing storage", Arr.shortArr
            "exact Arr writes every value within target",             Arr.exactArr
            "long Arr truncates surplus values at target boundary",   Arr.longArr

            "Single-field struct properties",        Properties.singleRecord
            "Multi-field struct properties",         Properties.multipleRecord
            "Computed struct getters",               Properties.computedGetters
            "Nested struct-property results",        Properties.nestedProperties
            "Arrays of struct properties",           Properties.structArrays
            "Reference and field dispatch controls", Properties.dispatchControls
            "Getter exception identity",             Properties.getterExceptions
        ]
        |> prepareCasesCpu "UniformWriters" target
