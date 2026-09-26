namespace Aardvark.Rendering.Tests

open System
open Aardvark.Rendering
open Expecto

module ``IndirectBuffer Tests`` =

    let private calls length =
        Array.init length (fun i ->
            let mutable call = DrawCallInfo(3 * (i + 1))
            call.InstanceCount <- i + 2
            call.FirstIndex <- i + 5
            call.FirstInstance <- i + 11
            call.BaseVertex <- -i - 7
            call)

    let private expectDescriptor reuseArray indexed first count (source : DrawCallInfo[]) expected (result : IndirectBuffer) =
        Expect.equal result.Count count "Draw count"
        Expect.equal result.Offset (uint64 first * uint64 sizeof<DrawCallInfo>) "Byte offset"
        Expect.equal result.Stride sizeof<DrawCallInfo> "Draw stride"
        Expect.equal result.Indexed indexed "Indexed layout flag"
        match result.Buffer with
        | :? ArrayBuffer as buffer ->
            Expect.equal buffer.ElementType typeof<DrawCallInfo> "Storage element type"
            Expect.equal (buffer.Data :?> DrawCallInfo[]) expected "Entire source contents are retained, not sliced or swapped"
            if reuseArray then
                Expect.isTrue (Object.ReferenceEquals(buffer.Data, source)) "Source array is reused"
        | _ -> failtest "Expected array-backed storage"
        Expect.equal source expected "Constructor does not modify the source"

    let private constructorTests name reuseArray range full =
        testList name [
            testList "ranges" [
                for indexed in [false; true] do
                    for length in [0; 1; 4] do
                        testCase $"indexed={indexed} length={length}" <| fun () ->
                            let source = calls length
                            let expected = Array.copy source
                            let bounds =
                                [Int32.MinValue; -1; 0; 1; 2; length - 1; length; length + 1; Int32.MaxValue - 1; Int32.MaxValue]
                                |> List.distinct

                            for first in bounds do
                                for count in bounds do
                                    let valid = first >= 0 && count >= 0 && int64 first + int64 count <= int64 length
                                    if valid then
                                        range indexed first count source
                                        |> expectDescriptor reuseArray indexed first count source expected
                                    else
                                        // Construct descriptors only: even the original overflow bug must not reach native copies.
                                        let error =
                                            try
                                                range indexed first count source |> ignore
                                                failtestf "Accepted invalid range (first = %d, count = %d, length = %d)" first count length
                                            with
                                            | :? ArgumentOutOfRangeException as error -> error
                                        Expect.isNull error.ParamName "Exception parameter remains unspecified"
                                        Expect.isNull error.ActualValue "Exception actual value remains unspecified"
                                        Expect.equal error.Message
                                            $"Draw call range exceeds input array (first = {first}, count = {count}, array length = {length})"
                                            "Original range diagnostic"
            ]
            testList "full" [
                for length in [0; 1; 4] do
                    testCase $"length={length}" <| fun () ->
                        let source = calls length
                        let expected = Array.copy source
                        full source |> expectDescriptor reuseArray false 0 length source expected
            ]
        ]

    [<Tests>]
    let tests =
        testList "IndirectBuffer" [
            constructorTests "array" true IndirectBuffer.ofArray' IndirectBuffer.ofArray
            constructorTests "list" false
                (fun indexed first count calls -> IndirectBuffer.ofList' indexed first count (Array.toList calls))
                (Array.toList >> IndirectBuffer.ofList)
            constructorTests "sequence" false
                (fun indexed first count calls -> IndirectBuffer.ofSeq' indexed first count (seq { for call in calls -> call }))
                (fun calls -> IndirectBuffer.ofSeq (seq { for call in calls -> call }))
            constructorTests "array sequence" true
                (fun indexed first count calls -> IndirectBuffer.ofSeq' indexed first count (calls :> seq<_>))
                (fun calls -> IndirectBuffer.ofSeq (calls :> seq<_>))
        ]
