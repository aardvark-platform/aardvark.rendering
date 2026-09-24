namespace Aardvark.Rendering.Tests.Utilities

open Aardvark.Rendering
open FSharp.Data.Adaptive
open Expecto
open System
open System.Collections.Generic
open System.Reflection
open System.Runtime.CompilerServices

module CompactSet =

    [<AutoOpen>]
    module private Common =

        let data = [| "foo"; "bar"; "a"; "b"; "c"; "d" |]

        let isValid (set : amap<'T, int>) =
            let map = set |> AMap.force
            let values = map |> HashMap.toValueList |> List.sort

            values = [0 .. values.Length - 1]

        // The model has no persistent key array. It follows the established three-phase
        // delta order, using the source reader's order and the runtime's tail-set order.
        type Model(initial : seq<int>) =
            let input = cset<int>(initial)
            let compact = ASet.compact input
            let sourceReader = input.GetReader()
            let mapReader = compact.GetReader()
            let members = System.Collections.Generic.HashSet<int>(initial)
            let mutable expected = HashMap.empty<int, int>

            member _.State = expected

            member _.Check() =
                let ops = sourceReader.GetChanges AdaptiveToken.Top
                let added = [ for op in ops do match op with Add(_, key) -> key | _ -> () ]
                let removed = [ for op in ops do match op with Rem(_, key) -> key | _ -> () ]
                let old = expected
                let count = members.Count
                let reverse = old |> HashMap.toList |> List.map (fun (key, index) -> index, key) |> dict
                let tail = System.Collections.Generic.HashSet<int>(seq { count .. old.Count - 1 })
                let holes = Queue<int>()
                let changes = ResizeArray<int * ElementOperation<int>>()

                for key in removed do
                    changes.Add(key, Remove)
                    let index = old.[key]
                    if index < count then holes.Enqueue index
                    else tail.Remove index |> ignore
                for index in tail do
                    changes.Add(reverse.[index], Set (holes.Dequeue()))
                for index in old.Count .. count - 1 do holes.Enqueue index
                for key in added do changes.Add(key, Set (holes.Dequeue()))
                Expect.equal holes.Count 0 "Every hole is filled"

                for key, op in changes do
                    expected <-
                        match op with
                        | Remove -> HashMap.remove key expected
                        | Set index -> HashMap.add key index expected

                let actualDelta = mapReader.GetChanges AdaptiveToken.Top
                Expect.equal actualDelta (HashMapDelta.ofSeq changes) "Exact removal, movement, and addition deltas"
                let actual = AMap.force compact
                Expect.equal actual expected "Exact index assignment"
                Expect.equal mapReader.State actual "Reader and forced map agree"
                Expect.sequenceEqual
                    (actual |> HashMap.toList |> List.map fst |> List.sort)
                    (members |> Seq.sort) "Exact membership"
                Expect.sequenceEqual
                    (actual |> HashMap.toValueList |> List.sort)
                    [0 .. count - 1] "Dense, unique indices"
                old |> HashMap.iter (fun key index ->
                    if members.Contains key && index < count then
                        Expect.equal actual.[key] index "Surviving prefix indices do not change"
                )
                Expect.equal (mapReader.GetChanges AdaptiveToken.Top) HashMapDelta.empty "Unchanged read has no delta"

            member x.Update(removed : seq<int>, added : seq<int>) =
                transact (fun () ->
                    for key in removed do
                        input.Remove key |> ignore
                        members.Remove key |> ignore
                    for key in added do
                        input.Add key |> ignore
                        members.Add key |> ignore
                )
                x.Check()

    module private Cases =

        let validity() =
            let input = cset<string>(data)
            let compact = input |> ASet.compact

            Expect.isTrue (isValid compact) "set not valid"

        let remove() =
            let input = cset<string>(data)
            let compact = input |> ASet.compact

            Expect.isTrue (isValid compact) "set not valid before remove"

            transact (fun _ ->
                input.Value <- input.Value |> HashSet.remove "bar" |> HashSet.remove "c"
            )

            Expect.isTrue (isValid compact) "set not valid after remove"

        let addAndRemove() =
            let input = cset<string>(data)
            let compact = input |> ASet.compact

            Expect.isTrue (isValid compact) "set not valid before add and remove"

            transact (fun _ ->
                input.Value <- input.Value |> HashSet.remove "bar" |> HashSet.remove "c" |> HashSet.add "new"
            )

            Expect.isTrue (isValid compact) "set not valid after add and remove"

        let boundaries() =
            let model = Model Seq.empty
            model.Check()
            // Cross several growth boundaries in both directions, without assuming key order.
            for key = 0 to 256 do model.Update([], [key])
            for key = 256 downto 0 do model.Update([key], [])
            for key = 0 to 80 do model.Update([], [key])

        let removals() =
            for indices in [[0]; [31]; [0; 2; 31]; [1; 3; 28; 30; 31]; [24 .. 31]] do
                let model = Model [0 .. 31]
                model.Check()
                let keys = model.State |> HashMap.toList |> List.filter (fun (_, i) -> List.contains i indices) |> List.map fst
                model.Update(keys, [])

        let mixedBatches() =
            let model = Model [0 .. 31]
            model.Check()
            model.Update([0; 2; 5; 9; 28; 31], [100; 101])
            model.Update([1; 3], [102; 103; 104; 105; 106; 107])
            for iteration = 0 to 31 do
                let keys = model.State |> HashMap.toList |> List.sortBy snd |> List.map fst
                model.Update([List.head keys; List.last keys], [200 + 2 * iteration; 201 + 2 * iteration])
            // Removing/re-adding the same member and redundant operations must remain no-ops.
            let key = model.State |> HashMap.toList |> List.head |> fst
            model.Update([key; -1], [key])
            model.Update([], [key])

        let bulkAndRefill() =
            let model = Model [0 .. 4095]
            model.Check()
            model.Update([0 .. 3999], [])
            model.Update([], [5000 .. 13191])
            model.Update([5000 .. 13180], [])
            for size in [0; 1; 32; 1025; 3; 0; 257] do
                let current = model.State |> HashMap.toList |> List.map fst
                model.Update(current, [])
                model.Update([], [20000 .. 20000 + size - 1])

        let randomized() =
            for seed in [0; 17; 918273] do
                let random = Random seed
                let model = Model [0 .. 63]
                model.Check()
                for iteration = 0 to 999 do
                    if iteration % 113 = 0 then
                        model.Update(model.State |> HashMap.toList |> List.map fst, [])
                    else
                        let removed = Array.init (random.Next(0, 25)) (fun _ -> random.Next 256)
                        let added = Array.init (random.Next(0, 25)) (fun _ -> random.Next 256)
                        model.Update(removed, added)

        let capacityPolicy() =
            let moduleType = typeof<TextureFormat>.Assembly.GetType("Aardvark.Rendering.CompactASetExtensions+ASetModule", true)
            let method = moduleType.GetMethod("capacityForCount", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
            Expect.isNotNull method "Private capacity policy is available for arithmetic-only boundary tests"
            let capacity oldCount count = method.Invoke(null, [|box oldCount; box count|]) :?> int
            for oldCapacity, count, expected in
                [0, 0, 0; 0, 1, 1; 0, 100000, 100000; 1, 2, 2; 16, 17, 32; 16, 1000, 1000;
                 64, 33, 64; 64, 17, 64; 64, 16, 16; 64, 0, 0; 63, 16, 63; 63, 15, 15;
                 0x7FEFFFFF / 2, 0x7FEFFFFF / 2 + 1, 0x7FEFFFFF - 1;
                 0x7FEFFFFF / 2 + 1, 0x7FEFFFFF / 2 + 2, 0x7FEFFFFF / 2 + 2;
                 Int32.MaxValue / 2, Int32.MaxValue / 2 + 1, Int32.MaxValue / 2 + 1;
                 Int32.MaxValue / 2 + 1, Int32.MaxValue / 2 + 2, Int32.MaxValue / 2 + 2;
                 Int32.MaxValue, Int32.MaxValue, Int32.MaxValue;
                 Int32.MaxValue, Int32.MaxValue / 4 + 1, Int32.MaxValue;
                 Int32.MaxValue, Int32.MaxValue / 4, Int32.MaxValue / 4] do
                Expect.equal (capacity oldCapacity count) expected $"Capacity {oldCapacity}, count {count}"

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private allocationPerUpdate count =
            let input = cset<int>(seq {0 .. count - 1})
            let compact = ASet.compact input
            AMap.force compact |> ignore
            let churn() =
                transact (fun () -> input.Add count |> ignore)
                AMap.force compact |> ignore
                transact (fun () -> input.Remove count |> ignore)
                AMap.force compact |> ignore
            for _ = 1 to 16 do churn()
            let before = GC.GetAllocatedBytesForCurrentThread()
            for _ = 1 to 32 do churn()
            let allocated = GC.GetAllocatedBytesForCurrentThread() - before
            GC.KeepAlive compact
            allocated / 64L

        let allocationScaling() =
            let small = allocationPerUpdate 64
            let large = allocationPerUpdate 131072
            // Allow tree-depth, runtime, and Debug overhead, but not a full key-array copy per update.
            Expect.isLessThan large (4L * small + 4096L) $"Allocation must not scale with membership ({small} vs {large} B/update)"
            Expect.isLessThan large 16384L "Warmed single-element updates have a generous fixed allocation ceiling"

        [<Sealed>]
        type private Key(id : int) =
            member _.Id = id
            override _.GetHashCode() = id
            override _.Equals other = match other with :? Key as other -> id = other.Id | _ -> false

        [<Struct>]
        type private StructKey =
            { Id : int
              Reference : Key }

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private removeKeys (create : int -> 'T) (target : 'T -> obj) mode =
            let input = cset<'T>(Seq.init 64 create)
            let compact = ASet.compact input
            AMap.force compact |> ignore
            transact (fun () -> input.Add(create 64) |> ignore)
            let current = AMap.force compact
            let victims =
                current |> HashMap.toList |> List.choose (fun (key, index) ->
                    if mode = "empty" || (mode = "bulk" && index >= 4) || (mode = "retained" && (index = 0 || index = 5 || index = 64)) then Some key
                    else None
                )
            let weak = victims |> List.map (target >> WeakReference) |> List.toArray
            transact (fun () -> for key in victims do input.Remove key |> ignore)
            AMap.force compact |> ignore
            // Advance adaptive histories past the removal delta; do not retain old snapshots/read deltas.
            let barrier = create 1000
            transact (fun () -> input.Add barrier |> ignore)
            AMap.force compact |> ignore
            transact (fun () -> input.Remove barrier |> ignore)
            AMap.force compact |> ignore
            input, compact, weak

        let private collectibility create target mode () =
            let input, compact, weak = removeKeys create target mode
            for _ = 1 to 3 do
                GC.Collect()
                GC.WaitForPendingFinalizers()
                GC.Collect()
            try
                for reference in weak do Expect.isFalse reference.IsAlive "Removed keys must not be retained by compact storage"
            finally
                GC.KeepAlive compact
                GC.KeepAlive input

        let collectibilityReferenceKeys mode = collectibility Key box mode

        let collectibilityStructKeys mode = collectibility (fun id -> { Id = id; Reference = Key id }) (fun key -> box key.Reference) mode

    let tests =
        testList "CompactSet" [
            testCase "Validity"                                 Cases.validity
            testCase "Remove"                                   Cases.remove
            testCase "Add and remove"                           Cases.addAndRemove
            testCase "Capacity boundaries"                      Cases.boundaries
            testCase "Interior and tail removals"               Cases.removals
            testCase "Mixed and equal-count batches"            Cases.mixedBatches
            testCase "Bulk shrink and empty-refill cycles"      Cases.bulkAndRefill
            testCase "Seeded reference model and exact deltas"  Cases.randomized
            testCase "Overflow-safe capacity policy"            Cases.capacityPolicy
            testCase "Allocation scaling"                       Cases.allocationScaling
            for mode in ["retained"; "bulk"; "empty"] do
                testCase $"Reference keys collectible after {mode} removal"                   <| Cases.collectibilityReferenceKeys mode
                testCase $"Reference-containing struct keys collectible after {mode} removal" <| Cases.collectibilityStructKeys mode
        ]