namespace Aardvark.Rendering.Tests

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering
open Expecto

module ``UniformWriter Tests`` =

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

    [<Tests>]
    let tests =
        testList "UniformWriters" [
            testCase "empty Arr stays inside target and zero-fills it" <| fun _ ->
                verifyArrWrite<N<0>> [||]

            testCase "short Arr writes values and zero-fills missing storage" <| fun _ ->
                verifyArrWrite<N<2>> [| 0x1020304; 0x11223344 |]

            testCase "exact Arr writes every value within target" <| fun _ ->
                verifyArrWrite<N<3>> [| 0x1020304; 0x11223344; 0x55667788 |]

            testCase "long Arr truncates surplus values at target boundary" <| fun _ ->
                verifyArrWrite<N<5>> [| 0x1020304; 0x11223344; 0x55667788; 0x12345678; 0x76543210 |]
        ]
