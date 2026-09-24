namespace Aardvark.Rendering.Tests.Utilities

open System
open System.Collections.Generic
open System.Threading
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto

module LockedSet =

    module private Cases =

        let private timeout = TimeSpan.FromSeconds 10.0

        let private contents (expected : seq<'T>) (actual : HashSet<'T>) context =
            if not (actual.SetEquals expected) then
                failtestf "%s: expected %A, actual %A" context (Seq.toArray expected) (Seq.toArray actual)

        let private whileClearBlocked (set : LockedSet<'T>) context action =
            // Warm this generic path; Clear is the caller's only blocking operation.
            LockedSet<'T>().Clear()
            let mutable ready = 0
            let mutable error = None
            let worker = Thread(ThreadStart(fun () ->
                Volatile.Write(&ready, 1)
                try set.Clear()
                with e -> error <- Some e
            ), IsBackground = true)
            let mutable started = false
            try
                set.Lock(fun _ ->
                    worker.Start()
                    started <- true
                    let blocked = SpinWait.SpinUntil((fun () ->
                        Volatile.Read(&ready) = 1 &&
                        (worker.ThreadState &&& ThreadState.WaitSleepJoin) <> enum<ThreadState> 0
                    ), timeout)
                    Expect.isTrue blocked $"{context}: Clear waits on the owned monitor"
                    action()
                )
            finally
                // Lock releases the monitor even if the owner's action fails. Always join afterwards.
                if started && not (worker.Join timeout) then
                    try worker.Interrupt() with :? ThreadStateException -> ()
                    Expect.isTrue (worker.Join timeout) $"{context}: Clear worker terminates after monitor release"
            match error with
            | Some e -> failtestf "%s: Clear worker failed: %O" context e
            | None -> ()

        let clearAndReuse() =
            for size in [0; 1; 2; 17; 1024] do
                for rounds in [1; 10; 100] do
                    let set = LockedSet<int>(Seq.init size id)
                    let storage = set.Lock id
                    let capacity = storage.EnsureCapacity 0
                    for round in 1 .. rounds do
                        let context = $"size={size}, rounds={rounds}, round={round}"
                        set.Clear()
                        set.Clear()
                        Expect.equal set.Count 0 $"{context}: repeated Clear is empty"
                        Expect.isTrue (Object.ReferenceEquals(storage, set.Lock id)) $"{context}: Clear retains storage"
                        Expect.equal (storage.EnsureCapacity 0) capacity $"{context}: Clear retains capacity"
                        for i in 0 .. size - 1 do
                            let value = i + round * size
                            Expect.isTrue (set.Add value) $"{context}, value={value}: reuse accepts a new element"
                            Expect.isFalse (set.Add value) $"{context}, value={value}: duplicates remain duplicates"
                        Expect.equal set.Count size $"{context}: reused set size"
                    set.Clear()
                    Expect.equal set.Count 0 $"size={size}, rounds={rounds}: final Clear"

        let warmedClear() =
            for size in [0; 64; 4096] do
                let set = LockedSet<int>(Seq.init size id)
                for _ in 1 .. 100 do set.Clear()
                let before = GC.GetAllocatedBytesForCurrentThread()
                for _ in 1 .. 10000 do set.Clear()
                let allocated = GC.GetAllocatedBytesForCurrentThread() - before
                Expect.equal allocated 0L $"size={size}: warmed Clear does not allocate a bound store delegate"
                Expect.equal set.Count 0 $"size={size}: set remains empty"

        let detachedStorage() =
            for size in [0; 1; 17; 1024] do
                let context = $"size={size}"
                let set = LockedSet<int>(Seq.init size id)
                let storage = set.Lock id
                let capacity = storage.EnsureCapacity 0
                let batch = set.GetAndClear()
                Expect.isTrue (Object.ReferenceEquals(storage, batch)) $"{context}: drain returns the old storage"
                Expect.equal (batch.EnsureCapacity 0) capacity $"{context}: detached capacity is retained"
                contents (Seq.init size id) batch $"{context}: drained elements"
                let replacement = set.Lock id
                Expect.isFalse (Object.ReferenceEquals(batch, replacement)) $"{context}: replacement storage is distinct"
                Expect.equal replacement.Count 0 $"{context}: replacement is empty"
                Expect.equal (replacement.EnsureCapacity 0) 0 $"{context}: detached capacity is not reserved again"
                set.Add -1 |> ignore
                batch.Add -2 |> ignore
                contents [-1] replacement $"{context}: detached mutation does not affect the set"
                set.Clear()
                contents (Seq.append (Seq.init size id) [-2]) batch $"{context}: Clear does not modify the detached batch"
                Expect.equal replacement.Count 0 $"{context}: Clear reaches replacement storage"
                batch.Clear()
                set.Add -3 |> ignore
                Expect.equal batch.Count 0 $"{context}: later Add does not modify the batch"
                contents [-3] (set.Lock id) $"{context}: set remains reusable"

        let blockedClear() =
            for initialCount in [1; 0; 17; 1024] do
                for drains in [1; 2; 5] do
                    for replacementCount in [0; 7] do
                        let context = $"initial={initialCount}, drains={drains}, replacement={replacementCount}"
                        let set = LockedSet<int>(Seq.init initialCount id)
                        let batches = ResizeArray<HashSet<int> * int[]>()
                        let mutable replacement = Unchecked.defaultof<HashSet<int>>
                        whileClearBlocked set context (fun () ->
                            for i in 1 .. drains do
                                let storage = set.Lock id
                                let expected = storage |> Seq.toArray
                                let batch = set.GetAndClear()
                                Expect.isTrue (Object.ReferenceEquals(storage, batch)) $"{context}, drain={i}: zero-copy drain"
                                batches.Add(batch, expected)
                                let count = if i = drains then replacementCount else i + 2
                                for value in 0 .. count - 1 do set.Add (-100 * i - value) |> ignore
                            replacement <- set.Lock id
                            Expect.equal replacement.Count replacementCount $"{context}: owner repopulates while Clear waits"
                            for batch, expected in batches do
                                contents expected batch $"{context}: batch intact before monitor release"
                        )
                        // Count alone misses damage to a detached batch: verify both halves independently.
                        for batch, expected in batches do
                            contents expected batch $"{context}: blocked Clear preserves every detached batch"
                        Expect.equal replacement.Count 0 $"{context}: blocked Clear empties the replacement"
                        Expect.isTrue (Object.ReferenceEquals(replacement, set.Lock id)) $"{context}: Clear does not swap replacement"
                        Expect.isTrue (set.Add 12345) $"{context}: reuse after contended Clear"
                        contents [12345] (set.Lock id) $"{context}: only the new element remains"

        let repeatedContention() =
            let set = LockedSet<int>()
            let batches = ResizeArray<HashSet<int> * int[]>()
            for round in 1 .. 50 do
                let context = $"round={round}"
                let expected = [|round; -round|]
                for value in expected do set.Add value |> ignore
                whileClearBlocked set context (fun () ->
                    batches.Add(set.GetAndClear(), expected)
                    set.Add (1000 + round) |> ignore
                )
                Expect.equal set.Count 0 $"{context}: Clear empties its current store"
                for batch, values in batches do
                    contents values batch $"{context}: earlier batches retain their contents"

        let referenceElements() =
            let values = [null; ""; "same"; "same"; "other"]
            let set = LockedSet<string>(values)
            let mutable batch = Unchecked.defaultof<HashSet<string>>
            whileClearBlocked set "reference and null elements" (fun () ->
                batch <- set.GetAndClear()
                for value in values do set.Add value |> ignore
            )
            let storage = set.Lock id
            Expect.equal storage.Count 0 "Current reference storage is cleared"
            contents values batch "Detached references are unchanged"
            batch.Remove "same" |> ignore
            Expect.isTrue (set.Add "same") "Detached removal is independent"
            contents ["same"] storage "Current set remains independent"

        let seededOperations() =
            for seed in 0 .. 9 do
                let random = Random(7721 + seed)
                let set = LockedSet<int>()
                let expected = HashSet<int>()
                let batches = ResizeArray<HashSet<int> * int[]>()
                for step in 1 .. 1000 do
                    let value = random.Next(-30, 31)
                    let operation = random.Next 5
                    let context = $"seed={seed}, step={step}, operation={operation}, value={value}"
                    match operation with
                    | 0 | 1 -> Expect.equal (set.Add value) (expected.Add value) $"{context}: Add result"
                    | 2 -> Expect.equal (set.Remove value) (expected.Remove value) $"{context}: Remove result"
                    | 3 ->
                        set.Clear()
                        expected.Clear()
                    | _ ->
                        let values = expected |> Seq.toArray
                        let storage = set.Lock id
                        let batch = set.GetAndClear()
                        Expect.isTrue (Object.ReferenceEquals(storage, batch)) $"{context}: zero-copy drain"
                        batches.Add(batch, values)
                        expected.Clear()
                    contents expected (set.Lock id) $"{context}: current contents match the model"
                for batch, values in batches do
                    contents values batch $"seed={seed}: later operations preserve historical batches"

    let tests (target : TestTarget) =
        [
            "Clear retains storage capacity and reuse",      Cases.clearAndReuse
            "Warmed Clear allocates no delegate",            Cases.warmedClear
            "GetAndClear transfers independent storage",     Cases.detachedStorage
            "Blocked Clear selects the current store",       Cases.blockedClear
            "Repeated contention preserves earlier batches", Cases.repeatedContention
            "Reference and null element ownership",          Cases.referenceElements
            "Seeded Clear drain and reuse",                  Cases.seededOperations
        ]
        |> prepareCasesCpu "LockedSet" target
