namespace Aardvark.Rendering.Tests.Utilities

open System
open System.Collections
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto

module CubeMapTests =

    module private Cases =

        let private sizes = [0; 1; 2; 7]
        let private invalidCounts = [715827883; -715827882; -715827881; -1; Int32.MinValue; Int32.MaxValue / 6 + 1]
        let private payload index = 100 + 10 * (index / 6) + index % 6
        let private storage levels = Array.init (6 * levels) payload

        let private coordinates levels =
            let faces = [0; 1; 2; 3; 4; 5; -1; 6; 7; 11; 12; Int32.MinValue; Int32.MaxValue]
            let mipLevels = [0; 1; levels - 1; levels; levels + 1; -1; Int32.MinValue; Int32.MaxValue; 715827883; -715827882; -715827881]
            seq {
                for face in faces do
                    for level in mipLevels do
                        if face < 0 || face > 5 || level < 0 || level >= levels then
                            yield enum<CubeSide> face, level
            } |> Seq.distinct |> Seq.toArray

        let private outcome action =
            try Choice1Of2(action())
            with error -> Choice2Of2(error.GetType())

        let private countError context (levels : int) (error : exn option) =
            match error with
            | Some (:? ArgumentOutOfRangeException as error) ->
                Expect.equal error.ParamName "levels" $"{context}: count parameter"
                Expect.equal error.ActualValue (box levels) $"{context}: rejected count"
            | _ -> failtestf "%s: expected ArgumentOutOfRangeException, got %A" context error

        let private rejectedCount context levels action =
            let error = try action(); None with error -> Some error
            countError context levels error

        let private rejectedSetter context (cube : CubeMap<int>) setter =
            let before = Array.copy cube.Data
            let error = try setter(); None with error -> Some error
            Expect.equal cube.Data before $"{context}: rejected setter leaves every element unchanged"
            match error with
            | Some (:? IndexOutOfRangeException) -> ()
            | _ -> failtestf "%s: expected IndexOutOfRangeException, got %A" context error

        let validCoordinates() =
            for levels in sizes do
                let data = storage levels
                let mutable cube = CubeMap<int>(data)
                Expect.equal cube.Levels levels $"levels={levels}: count"
                Expect.equal cube.IsEmpty (levels = 0) $"levels={levels}: empty"
                for level in 0 .. levels - 1 do
                    for face in 0 .. 5 do
                        let side = enum<CubeSide> face
                        let index = level * 6 + face
                        let context = $"levels={levels}, face={face}, level={level}"
                        Expect.equal cube.[side, level] (payload index) $"{context}: Item value"
                        Expect.equal (cube.TryItem(side, level)) (Some(payload index)) $"{context}: TryItem value"
                        let expected = Array.copy data
                        expected.[index] <- 1000 + index
                        cube.[side, level] <- 1000 + index
                        Expect.equal data expected $"{context}: setter changes only the selected slot"
                if levels > 0 then
                    for face in 0 .. 5 do
                        let side = enum<CubeSide> face
                        Expect.equal cube.[side] cube.[side, 0] $"levels={levels}, face={face}: level-zero Item alias"
                        Expect.equal (cube.TryItem side) (cube.TryItem(side, 0)) $"levels={levels}, face={face}: level-zero TryItem alias"
                        let expected = Array.copy data
                        expected.[face] <- -100 - face
                        cube.[side] <- -100 - face
                        Expect.equal data expected $"levels={levels}, face={face}: level-zero setter alias"
                Expect.sequenceEqual (cube :> seq<int>) data $"levels={levels}: generic enumeration order"
                Expect.sequenceEqual ((cube :> IEnumerable) |> Seq.cast<int>) data $"levels={levels}: non-generic enumeration order"

        let invalidItems() =
            for levels in sizes do
                let mutable cube = CubeMap<int>(storage levels)
                for side, level in coordinates levels do
                    let context = $"levels={levels}, face={int side}, level={level}"
                    rejectedSetter context cube (fun () -> cube.[side, level] <- -12345)
                    Expect.throwsT<IndexOutOfRangeException> (fun () -> cube.[side, level] |> ignore) $"{context}: invalid getter"
                for face in [0; 1; 2; 3; 4; 5; -1; 6; 7; 11; 12; Int32.MinValue; Int32.MaxValue] do
                    if levels = 0 || face < 0 || face > 5 then
                        let side = enum<CubeSide> face
                        let context = $"levels={levels}, face={face}, level-zero overload"
                        rejectedSetter context cube (fun () -> cube.[side] <- -54321)
                        Expect.throwsT<IndexOutOfRangeException> (fun () -> cube.[side] |> ignore) $"{context}: invalid getter"

        let invalidTryItems() =
            for levels in sizes do
                let cube = CubeMap<int>(storage levels)
                for side, level in coordinates levels do
                    Expect.equal (cube.TryItem(side, level)) None $"levels={levels}, face={int side}, level={level}: no aliased TryItem"
                for face in [0; 1; 2; 3; 4; 5; -1; 6; 7; 11; 12; Int32.MinValue; Int32.MaxValue] do
                    if levels = 0 || face < 0 || face > 5 then
                        Expect.equal (cube.TryItem(enum<CubeSide> face)) None $"levels={levels}, face={face}: no level-zero alias"

        let constructionAndResize() =
            for levels in sizes do
                let empty = CubeMap<int>(levels)
                Expect.equal empty.Data (Array.zeroCreate<int> (6 * levels)) $"levels={levels}: zero-filled constructor"
                Expect.equal empty.IsEmpty (levels = 0) $"levels={levels}: zero-level construction"
                let visits = ResizeArray<struct (int * int)>()
                let initialized = CubeMap.init levels (fun side level -> visits.Add(struct (int side, level)); payload (level * 6 + int side))
                Expect.equal initialized.Data (storage levels) $"levels={levels}: initialized data order"
                Expect.sequenceEqual visits (Array.init (6 * levels) (fun i -> struct (i % 6, i / 6))) $"levels={levels}: generator visit order"
                Expect.equal (CubeMap.single levels 17).Data (Array.create (6 * levels) 17) $"levels={levels}: single value"
                for newLevels in sizes do
                    let source = CubeMap<int>(storage levels)
                    let before = Array.copy source.Data
                    let resized = CubeMap<int>(source, newLevels)
                    let expected = Array.init (6 * newLevels) (fun i -> if i < before.Length then before.[i] else 0)
                    Expect.equal resized.Data expected $"levels={levels}, newLevels={newLevels}: retained prefix and zero fill"
                    Expect.equal resized.Levels newLevels $"levels={levels}, newLevels={newLevels}: resized count"
                    Expect.equal source.Data before $"levels={levels}, newLevels={newLevels}: source unchanged"
                    if resized.Data.Length > 0 then
                        resized.Data.[0] <- -99
                        Expect.equal source.Data before $"levels={levels}, newLevels={newLevels}: resize storage is independent"
            let references = CubeMap<string>([| for i in 0 .. 5 -> $"element {i}" |])
            let resized = CubeMap<string>(references, 2)
            Expect.equal resized.Data.[6..] (Array.create 6 null) "new reference slots are null"
            Expect.equal (CubeMap.empty<int>).Levels 0 "empty module value"
            Expect.isTrue (CubeMap.isEmpty (CubeMap.empty<int>)) "empty alias"
            let noGenerator : CubeMap<int> = CubeMap.init 0 Unchecked.defaultof<_>
            Expect.isTrue noGenerator.IsEmpty "zero-level initialization never invokes even a null generator"

        let invalidConstructionCounts() =
            let source = CubeMap<int>(storage 2)
            let before = Array.copy source.Data
            for levels in invalidCounts do
                rejectedCount $"constructor, levels={levels}" levels (fun () -> CubeMap<int>(levels) |> ignore)
                rejectedCount $"resize, levels={levels}" levels (fun () -> CubeMap<int>(source, levels) |> ignore)
                Expect.equal source.Data before $"levels={levels}: rejected resize preserves source"
                rejectedCount $"single, levels={levels}" levels (fun () -> CubeMap.single levels 13 |> ignore)

        let invalidInitializationCounts() =
            for levels in invalidCounts do
                let visits = ResizeArray<struct (int * int)>()
                let error =
                    try
                        CubeMap.init levels (fun side level -> visits.Add(struct (int side, level)); 1) |> ignore
                        None
                    with error -> Some error
                Expect.equal visits.Count 0 $"levels={levels}: rejection precedes every generator invocation"
                countError $"init, levels={levels}" levels error

        let ownershipAndDefaultStructs() =
            let array = storage 2
            let cube = CubeMap<int>(array)
            Expect.isTrue (Object.ReferenceEquals(array, cube.Data)) "array constructor retains storage ownership"
            Expect.isTrue (Object.ReferenceEquals(array, CubeMap.data cube)) "data alias retains storage identity"
            array.[0] <- -19
            Expect.equal cube.[enum<CubeSide> 0] -19 "array changes remain visible"
            let mutable enumerations = 0
            let source = seq { enumerations <- enumerations + 1; for value in array do yield value }
            let copy = CubeMap<int>(source)
            Expect.equal enumerations 1 "sequence materializes once"
            Expect.equal copy.Data array "sequence data order"
            Expect.isFalse (Object.ReferenceEquals(copy.Data, array)) "non-array sequence owns its materialization"
            array.[0] <- -29
            Expect.equal copy.Data.[0] -19 "materialized storage is independent"
            for count in [1; 5; 7; 13] do
                Expect.throws (fun () -> CubeMap<int>(Array.zeroCreate<int> count) |> ignore) $"count={count}: incomplete level remains rejected"
            let mutable missing = Unchecked.defaultof<CubeMap<int>>
            Expect.isNull missing.Data "default struct retains null storage"
            Expect.throwsT<NullReferenceException> (fun () -> missing.Levels |> ignore) "default Levels behavior"
            Expect.throwsT<NullReferenceException> (fun () -> missing.IsEmpty |> ignore) "default IsEmpty behavior"
            for face, level in [(0, 0); (-1, 0); (6, 0); (0, -1); (-1, 1); (0, 715827883)] do
                let side = enum<CubeSide> face
                let context = $"default struct, face={face}, level={level}"
                Expect.throwsT<NullReferenceException> (fun () -> missing.[side, level] |> ignore) $"{context}: getter"
                Expect.throwsT<NullReferenceException> (fun () -> missing.[side, level] <- 1) $"{context}: setter"
                Expect.throwsT<NullReferenceException> (fun () -> missing.[side] |> ignore) $"{context}: level-zero getter"
                Expect.throwsT<NullReferenceException> (fun () -> missing.[side] <- 1) $"{context}: level-zero setter"
                Expect.equal (outcome (fun () -> missing.TryItem(side, level))) (outcome (fun () -> Array.tryItem (level * 6 + face) (null : int[]))) $"{context}: existing null-array TryItem behavior"
                Expect.equal (outcome (fun () -> missing.TryItem side)) (outcome (fun () -> Array.tryItem face (null : int[]))) $"{context}: existing level-zero null-array behavior"
            for levels in [0; 1] do
                Expect.throwsT<NullReferenceException> (fun () -> CubeMap<int>(missing, levels) |> ignore) $"levels={levels}: default resize behavior"
            Expect.throws (fun () -> CubeMap<int>(null : seq<int>) |> ignore) "null sequence remains rejected"

    let tests (target : TestTarget) =
        [
            "Valid coordinates and storage aliases",     Cases.validCoordinates
            "Invalid Item coordinates preserve storage", Cases.invalidItems
            "Invalid TryItem coordinates return None",   Cases.invalidTryItems
            "Construction resizing and generator order", Cases.constructionAndResize
            "Invalid level-count construction",          Cases.invalidConstructionCounts
            "Invalid init counts invoke no callbacks",   Cases.invalidInitializationCounts
            "Storage ownership and default structs",     Cases.ownershipAndDefaultStructs
        ]
        |> prepareCasesCpu "CubeMap" target
