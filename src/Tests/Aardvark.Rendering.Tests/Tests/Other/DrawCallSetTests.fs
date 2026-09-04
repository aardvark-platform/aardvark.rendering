namespace Aardvark.Rendering.Tests

open System
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering
open Expecto

module ``DrawCallSet Tests`` =

    let private fields (call : DrawCallInfo) =
        struct (call.FirstIndex, call.FaceVertexCount, call.FirstInstance, call.InstanceCount, call.BaseVertex)

    let private rangeFields (range : Range1i) =
        struct (range.Min, range.Size + 1, 0, 1, 0)

    let private expectedRanges collapseAdjacent (active : HashSet<Range1i>) =
        if collapseAdjacent then
            active
            |> Seq.filter (fun range -> range.IsValid)
            |> Seq.fold (fun union range -> RangeSet1i.add range union) RangeSet1i.empty
            |> Seq.toArray
        else
            active |> Seq.toArray

    let private validate message collapseAdjacent (active : HashSet<Range1i>) (set : DrawCallSet) =
        let expected =
            expectedRanges collapseAdjacent active
            |> Array.map rangeFields
            |> Array.sort

        let actual =
            FSharp.Data.Adaptive.AVal.force (set :> FSharp.Data.Adaptive.aval<_>)
            |> Array.map fields
            |> Array.sort

        Expect.sequenceEqual actual expected message

    let private add message (active : HashSet<Range1i>) (set : DrawCallSet) range =
        let expected = active.Add range
        let actual = set.Add range
        Expect.equal actual expected (message + " add result")

    let private remove message (active : HashSet<Range1i>) (set : DrawCallSet) range =
        let expected = active.Remove range
        let actual = set.Remove range
        Expect.equal actual expected (message + " remove result")

    module Cases =

        let crossingRemovalOrders() =
            let left = Range1i(0, 5)
            let right = Range1i(3, 7)

            for first, second in [left, right; right, left] do
                let set = DrawCallSet true
                let active = HashSet<Range1i>()

                add "crossing first" active set left
                validate "crossing after first add" true active set
                add "crossing second" active set right
                validate "crossing union" true active set

                remove "crossing first removal" active set first
                validate "crossing retained coverage" true active set
                remove "crossing second removal" active set second
                validate "crossing empty" true active set

        let nestedRemovalOrders() =
            let outer = Range1i(0, 12)
            let middle = Range1i(2, 10)
            let inner = Range1i(4, 7)

            for removalOrder in [[inner; middle; outer]; [outer; inner; middle]; [middle; outer; inner]] do
                let set = DrawCallSet true
                let active = HashSet<Range1i>()

                for range in [inner; outer; middle] do
                    add "nested" active set range
                    validate "nested add" true active set

                for range in removalOrder do
                    remove "nested" active set range
                    validate "nested removal" true active set

        let chainedAndAdjacent() =
            let ranges =
                [
                    Range1i(0, 2)
                    Range1i(2, 4)
                    Range1i(4, 6)
                    Range1i(7, 9)
                ]

            let set = DrawCallSet true
            let active = HashSet<Range1i>()
            for range in ranges do
                add "chain" active set range
                validate "chained add" true active set

            remove "chain middle" active set ranges.[1]
            validate "chained middle removal" true active set
            remove "chain left" active set ranges.[0]
            validate "chained left removal" true active set
            remove "chain right" active set ranges.[2]
            validate "adjacent tail retained" true active set
            remove "chain tail" active set ranges.[3]
            validate "chain empty" true active set

        let disjointInsertionAndRemoval() =
            let ranges =
                [
                    Range1i(20, 22)
                    Range1i(0, 2)
                    Range1i(40, 45)
                    Range1i(-20, -17)
                    Range1i(10, 12)
                    Range1i(3, 5)
                ]

            let set = DrawCallSet true
            let active = HashSet<Range1i>()
            for range in ranges do
                add "disjoint" active set range
                validate "disjoint insertion" true active set

            for range in [ranges.[2]; ranges.[0]; ranges.[3]; ranges.[5]; ranges.[1]; ranges.[4]] do
                remove "disjoint" active set range
                validate "disjoint removal" true active set

        let integerBoundaries() =
            let minValue = Int32.MinValue
            let maxValue = Int32.MaxValue
            let ranges =
                [
                    Range1i(minValue, minValue + 2)
                    Range1i(minValue + 2, minValue + 4)
                    Range1i(maxValue - 4, maxValue - 2)
                    Range1i(maxValue - 2, maxValue)
                ]

            let set = DrawCallSet true
            let active = HashSet<Range1i>()
            for range in ranges do
                add "boundary" active set range
            validate "both integer boundaries" true active set

            remove "minimum crossing" active set ranges.[0]
            validate "minimum boundary retained" true active set
            remove "maximum crossing" active set ranges.[3]
            validate "maximum boundary retained" true active set

            let full = Range1i(minValue, maxValue)
            let maximum = Range1i(maxValue, maxValue)
            add "full integer range" active set full
            add "maximum singleton" active set maximum
            validate "full integer range fields" true active set
            remove "full integer range" active set full
            validate "coverage at Int32.MaxValue retained" true active set

        let nonCollapsedExactRanges() =
            let set = DrawCallSet false
            let active = HashSet<Range1i>()
            let outer = Range1i(-5, 8)
            let crossing = Range1i(4, 12)
            let invalid = Range1i.Invalid

            add "non-collapsed outer" active set outer
            add "non-collapsed crossing" active set crossing
            add "non-collapsed invalid" active set invalid
            validate "non-collapsed exact output" false active set

            remove "non-collapsed outer" active set outer
            validate "non-collapsed exact removal" false active set
            remove "non-collapsed invalid" active set invalid
            validate "non-collapsed invalid removal" false active set

        let duplicatesAndNoOps() =
            for collapseAdjacent in [false; true] do
                let set = DrawCallSet collapseAdjacent
                let active = HashSet<Range1i>()
                let range = Range1i(1, 5)
                let invalid = Range1i.Invalid

                add "initial" active set range
                add "duplicate" active set range
                remove "missing" active set (Range1i(10, 12))
                add "invalid" active set invalid
                add "duplicate invalid" active set invalid
                validate "duplicates and no-ops" collapseAdjacent active set

                remove "range" active set range
                remove "duplicate removal" active set range
                remove "invalid" active set invalid
                remove "duplicate invalid removal" active set invalid
                validate "duplicates and no-ops empty" collapseAdjacent active set

        let safeAndUnsafeInvalidation() =
            let set = DrawCallSet true
            let active = HashSet<Range1i>()
            let adaptive = set :> FSharp.Data.Adaptive.aval<_>
            let adaptiveObject = set :> FSharp.Data.Adaptive.IAdaptiveObject
            let first = Range1i(0, 4)
            let second = Range1i(3, 8)

            validate "initial adaptive value" true active set
            Expect.isFalse adaptiveObject.OutOfDate "Initial force did not validate the adaptive value"

            Expect.isTrue (set.AddUnsafe first) "Unsafe add failed"
            active.Add first |> ignore
            Expect.isFalse adaptiveObject.OutOfDate "Unsafe add unexpectedly invalidated the adaptive value"
            Expect.isEmpty (FSharp.Data.Adaptive.AVal.force adaptive) "Unsafe add became visible without invalidation"

            add "notifying add" active set second
            Expect.isTrue adaptiveObject.OutOfDate "Successful safe add did not invalidate the adaptive value"
            validate "unsafe add visible after safe add" true active set
            Expect.isFalse adaptiveObject.OutOfDate "Force did not validate the adaptive value"

            Expect.isFalse (set.AddUnsafe second) "Duplicate unsafe add unexpectedly succeeded"
            Expect.isFalse adaptiveObject.OutOfDate "Duplicate unsafe add unexpectedly invalidated"
            add "duplicate safe add" active set second
            Expect.isFalse adaptiveObject.OutOfDate "Duplicate safe add unexpectedly invalidated"

            Expect.isTrue (set.RemoveUnsafe first) "Unsafe removal failed"
            active.Remove first |> ignore
            Expect.isFalse adaptiveObject.OutOfDate "Unsafe removal unexpectedly invalidated the adaptive value"
            let cached = FSharp.Data.Adaptive.AVal.force adaptive |> Array.map fields |> Array.sort
            let stale = [| rangeFields (Range1i(0, 8)) |]
            Expect.sequenceEqual cached stale "Unsafe removal unexpectedly changed the cached value"

            remove "notifying removal" active set second
            Expect.isTrue adaptiveObject.OutOfDate "Successful safe removal did not invalidate the adaptive value"
            validate "unsafe removal visible after safe removal" true active set

            Expect.isFalse (set.RemoveUnsafe first) "Missing unsafe removal unexpectedly succeeded"
            Expect.isFalse adaptiveObject.OutOfDate "Missing unsafe removal unexpectedly invalidated"

        let randomizedReference collapseAdjacent seed =
            let random = Random seed
            let set = DrawCallSet collapseAdjacent
            let active = HashSet<Range1i>()
            let adaptiveObject = set :> FSharp.Data.Adaptive.IAdaptiveObject
            let ranges =
                Array.init 80 (fun _ ->
                    let start = random.Next(-40, 41)
                    Range1i(start, start + random.Next(0, 16))
                )

            validate "random initial" collapseAdjacent active set

            for step in 0 .. 799 do
                let range = ranges.[random.Next ranges.Length]
                let useUnsafe = random.Next(4) = 0
                let isAdd = random.Next(2) = 0

                let expected = if isAdd then active.Add range else active.Remove range
                let actual =
                    if isAdd then
                        if useUnsafe then set.AddUnsafe range else set.Add range
                    else
                        if useUnsafe then set.RemoveUnsafe range else set.Remove range

                Expect.equal actual expected (sprintf "random step %d mutation result" step)

                if useUnsafe then
                    Expect.isFalse adaptiveObject.OutOfDate (sprintf "random step %d unsafe invalidation" step)
                    if expected then
                        // A unique temporary safe mutation exposes the unsafe state without requiring a public manual invalidation API.
                        let sentinel = Range1i(1000000 + 2 * step, 1000000 + 2 * step)
                        Expect.isTrue (active.Add sentinel) "Reference sentinel add failed"
                        Expect.isTrue (set.Add sentinel) "Sentinel add failed"
                        validate (sprintf "random unsafe step %d" step) collapseAdjacent active set
                        Expect.isTrue (active.Remove sentinel) "Reference sentinel removal failed"
                        Expect.isTrue (set.Remove sentinel) "Sentinel removal failed"
                validate (sprintf "random step %d" step) collapseAdjacent active set

    [<Tests>]
    let tests =
        testList "DrawCallSet" [
            testCase "crossing ranges preserve coverage in either removal order" Cases.crossingRemovalOrders
            testCase "nested ranges preserve coverage in different removal orders" Cases.nestedRemovalOrders
            testCase "chained and adjacent ranges retain remaining spans" Cases.chainedAndAdjacent
            testCase "disjoint ranges support adversarial insertion and removal" Cases.disjointInsertionAndRemoval
            testCase "integer-boundary intervals do not overflow coverage endpoints" Cases.integerBoundaries
            testCase "non-collapsed sets emit exact active ranges" Cases.nonCollapsedExactRanges
            testCase "duplicates, missing removals, and invalid ranges are exact no-ops" Cases.duplicatesAndNoOps
            testCase "safe and unsafe mutations preserve adaptive invalidation" Cases.safeAndUnsafeInvalidation
            testCase "fixed-seed collapsed reference operations" (fun () -> Cases.randomizedReference true 0x5E7)
            testCase "fixed-seed non-collapsed reference operations" (fun () -> Cases.randomizedReference false 0x5E7)
        ]
