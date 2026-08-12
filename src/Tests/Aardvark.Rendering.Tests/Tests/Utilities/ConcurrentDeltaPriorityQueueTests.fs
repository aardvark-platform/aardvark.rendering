namespace Aardvark.Rendering.Tests.Utilities

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto
open FSharp.Data.Adaptive

module ConcurrentDeltaPriorityQueue =

    let private operation value count = SetOperation(value, count)

    let private apply (model : Dictionary<int, int>) (item : SetOperation<int>) =
        if item.Count <> 0 then
            let mutable oldCount = 0
            model.TryGetValue(item.Value, &oldCount) |> ignore
            let newCount = oldCount + item.Count
            if newCount = 0 then model.Remove item.Value |> ignore
            else model.[item.Value] <- newCount

    let private checkState context (queue : ConcurrentDeltaPriorityQueue<int, int>) (model : Dictionary<int, int>) =
        let mutable adds = 0
        let mutable rems = 0
        for count in model.Values do
            if count > 0 then adds <- adds + 1
            elif count < 0 then rems <- rems + 1
        Expect.equal queue.Count model.Count $"{context}: total count"
        Expect.equal queue.AddCount adds $"{context}: positive count"
        Expect.equal queue.RemoveCount rems $"{context}: negative count"
        Expect.equal (queue.AddCount + queue.RemoveCount) queue.Count $"{context}: count partitions"

    let private dequeueExpected context priority (queue : ConcurrentDeltaPriorityQueue<int, int>) (model : Dictionary<int, int>) =
        let mutable expectedValue = 0
        let mutable expectedCount = 0
        let mutable expectedPriority = Int32.MaxValue
        for KeyValue(value, count) in model do
            let currentPriority = priority (operation value count)
            if currentPriority < expectedPriority then
                expectedValue <- value
                expectedCount <- count
                expectedPriority <- currentPriority
        let actual = queue.Dequeue()
        Expect.equal actual.Value expectedValue $"{context}: minimum-priority value"
        Expect.equal actual.Count expectedCount $"{context}: coalesced count"
        model.Remove expectedValue |> ignore

    let private drainValues (queue : ConcurrentDeltaPriorityQueue<int, int>) =
        Array.init queue.Count (fun _ -> queue.Dequeue().Value)

    let private enqueue batch (queue : ConcurrentDeltaPriorityQueue<int, int>) items =
        if batch then queue.EnqueueMany items
        else for item in items do queue.Enqueue item

    type ConcurrentDeltaPriorityQueue<'K, 'V when 'V : comparison> with
        member this.Lock =
            let fi = this.GetType().GetField("lockObj", Reflection.BindingFlags.NonPublic ||| Reflection.BindingFlags.Instance)
            Expect.isNotNull fi "Cannot find lockObj field"
            fi.GetValue this

    type private ThrowingPriority(value : int, error : unit -> exn) =
        member _.Value = value
        override _.Equals other =
            match other with
            | :? ThrowingPriority as other -> value = other.Value
            | _ -> false
        override _.GetHashCode() = value.GetHashCode()
        interface IComparable with
            member _.CompareTo other =
                let failure = error()
                if not (isNull failure) then raise failure
                compare value (other :?> ThrowingPriority).Value

    let private timeout = TimeSpan.FromSeconds 10.0

    let private monitorReleaseRound token failure =
        let context = $"token={token}, failure={failure}"
        let original = InvalidOperationException(context)
        let mutable comparisonError : exn = null
        let queue = ConcurrentDeltaPriorityQueue<int, ThrowingPriority>(fun item -> ThrowingPriority(item.Value, fun () -> comparisonError))
        if failure = "comparison" then
            queue.EnqueueMany [for value in 1 .. 4 -> operation value 1]
            comparisonError <- original
        use cancellation = new CancellationTokenSource()
        if failure = "cancellation" then cancellation.Cancel()
        use attempting = new ManualResetEventSlim(false)
        use completed = new ManualResetEventSlim(false)
        let errors = ConcurrentQueue<exn>()
        let worker =
            Thread(ThreadStart(fun () ->
                try
                    try
                        let mutable actual : exn = null
                        // Nothing blocks between readiness and the dequeue call.
                        attempting.Set()
                        try
                            if token then queue.Dequeue cancellation.Token |> ignore
                            else queue.Dequeue() |> ignore
                        with error -> actual <- error
                        match failure with
                        | "comparison" -> Expect.isTrue (Object.ReferenceEquals(actual, original)) $"{context}: comparison exception identity"
                        | "interruption" -> Expect.isTrue (actual :? ThreadInterruptedException) $"{context}: interruption replaced by {actual}"
                        | "cancellation" -> Expect.isTrue (actual :? OperationCanceledException) $"{context}: cancellation replaced by {actual}"
                        | _ -> failtestf "%s: unknown failure" context
                        Expect.isFalse (Monitor.IsEntered queue.Lock) $"{context}: dequeue retained its monitor"
                    finally
                        // Original-source controls leak this monitor. Release it
                        // on its owning thread even when the assertion fails.
                        if Monitor.IsEntered queue.Lock then Monitor.Exit queue.Lock
                with error -> errors.Enqueue error
                completed.Set()
            ))
        worker.IsBackground <- true
        worker.Start()
        try
            Expect.isTrue (attempting.Wait timeout) $"{context}: dequeue did not start"
            if failure = "interruption" then
                let waiting = SpinWait.SpinUntil((fun () -> worker.ThreadState.HasFlag ThreadState.WaitSleepJoin), timeout)
                Expect.isTrue waiting $"{context}: dequeue did not wait"
                worker.Interrupt()
            Expect.isTrue (completed.Wait timeout) $"{context}: dequeue did not finish"
            Expect.isTrue (worker.Join timeout) $"{context}: dequeue worker did not finish"
        finally
            if worker.IsAlive then worker.Interrupt()
            completed.Wait timeout |> ignore
            worker.Join timeout |> ignore
        if not errors.IsEmpty then failtestf "%s: worker failed: %A" context (errors.ToArray())

        use reused = new ManualResetEventSlim(false)
        let reuseWorker =
            Thread(ThreadStart(fun () ->
                try
                    comparisonError <- null
                    queue.Enqueue <| operation 1 1
                with
                    error -> errors.Enqueue error
                reused.Set()
            ))
        reuseWorker.IsBackground <- true
        reuseWorker.Start()
        try
            Expect.isTrue (reused.Wait timeout) $"{context}: another thread could not reuse the monitor"
            Expect.isTrue (reuseWorker.Join timeout) $"{context}: monitor reuse worker did not finish"
        finally
            reused.Wait timeout |> ignore
            reuseWorker.Join timeout |> ignore
        if not errors.IsEmpty then failtestf "%s: reuse failed: %A" context (errors.ToArray())
        Expect.notEqual worker.ManagedThreadId reuseWorker.ManagedThreadId $"{context}: reuse used the dequeue thread"

    module private Cases =

        let ordering() =
            for batch in [false; true] do
                let queue = ConcurrentDeltaPriorityQueue<int, int>(fun item -> item.Value)
                enqueue batch queue [for value in [1; 5; 2; 6; 7; 3; 4] -> operation value 1]
                Expect.sequenceEqual (drainValues queue) [|1; 2; 3; 4; 5; 6; 7|] $"batch={batch}: min-heap dequeue order"

        let equalPriority() =
            let cases = [
                [|0; 1; 1; 2|], [|0; 2; 1; 3|]
                [|0; 0; 0; 0|], [|0; 3; 2; 1|]
                [|0; 1; 2; 1|], [|0; 3; 1; 2|]
                [|0|],          [|0|]
                [||],           [||]
            ]
            for batch in [false; true] do
                for priorities, expected in cases do
                    let queue = ConcurrentDeltaPriorityQueue<int, int>(fun item -> priorities.[item.Value])
                    enqueue batch queue [for value in 0 .. priorities.Length - 1 -> operation value 1]
                    Expect.sequenceEqual (drainValues queue) expected $"batch={batch}, priorities={priorities}: tie ordering"

        let increasedPriority() =
            for batch in [false; true] do
                let priorities = [|1; 5; 2; 6; 7; 3; 4|]
                let queue = ConcurrentDeltaPriorityQueue<int, int>(fun item ->
                    if item.Value = 0 then item.Count else priorities.[item.Value]
                )
                queue.EnqueueMany [for value in 0 .. 6 -> operation value 1]
                enqueue batch queue [operation 0 7]
                Expect.sequenceEqual (drainValues queue) [|2; 5; 6; 1; 3; 4; 0|] $"batch={batch}: increased priority repairs the smaller right child"

        let zeroCount() =
            for batch in [false; true] do
                let context = $"batch={batch}"
                let mutable priorityCalls = 0
                let queue = ConcurrentDeltaPriorityQueue<int, int>(fun item ->
                    priorityCalls <- priorityCalls + 1
                    item.Value
                )
                let model = Dictionary<int, int>()
                enqueue batch queue [operation 10 0; operation 11 0]
                checkState context queue model
                Expect.equal priorityCalls 0 $"{context}: empty zero operations evaluated priority"
                for item in [operation 3 1; operation 1 1; operation 7 -2] do
                    queue.Enqueue item
                    apply model item
                enqueue batch queue [operation 0 0; operation 3 0; operation 7 0; operation 99 0]
                checkState context queue model
                Expect.equal priorityCalls 3 $"{context}: populated zero operations evaluated priority"
                Expect.sequenceEqual (drainValues queue) [|1; 3; 7|] $"{context}: zero operations changed ordering"

        let counts() =
            for batch in [false; true] do
                let context = $"batch={batch}"
                let queue = ConcurrentDeltaPriorityQueue<int, int>(fun item -> abs item.Count)
                let model = Dictionary<int, int>()
                for value, counts in [1, [2; -5; 3; -2; 5]; 2, [-1; 3; -5; 3; 0; -1]] do
                    for count in counts do
                        let item = operation value count
                        enqueue batch queue [item]
                        apply model item
                        checkState $"{context}, value={value}, delta={count}" queue model
                while model.Count > 0 do
                    dequeueExpected context (fun item -> abs item.Count) queue model
                    checkState context queue model
                enqueue batch queue [operation 3 2; operation 3 2; operation 4 -3; operation 3 -4; operation 4 3]
                checkState context queue model
                queue.Enqueue(operation 5 1)
                Expect.equal (queue.Dequeue()) (operation 5 1) $"{context}: reuse after coalesced cancellation"
                checkState context queue model

        let private randomizedRound seed =
            let random = Random(seed)
            let priorities = Array.init 32 id
            for i = priorities.Length - 1 downto 1 do
                let j = random.Next(i + 1)
                let t = priorities.[i]
                priorities.[i] <- priorities.[j]
                priorities.[j] <- t
            let priority (item : SetOperation<int>) = abs item.Count * priorities.Length + priorities.[item.Value]
            let queue = ConcurrentDeltaPriorityQueue<int, int>(priority)
            let model = Dictionary<int, int>()
            let add batch items =
                enqueue batch queue items
                for item in items do apply model item
            add true [|operation 7 2; operation 7 2; operation 8 -3|]
            checkState $"seed={seed}: duplicates" queue model
            for item in [operation 7 -4; operation 8 3; operation 9 -2; operation 9 5; operation 9 -6; operation 9 3] do
                add false [|item|]
                checkState $"seed={seed}: initial transition {item}" queue model
            let randomOperation() = operation (random.Next priorities.Length) (random.Next(-4, 5))
            for step in 1 .. 4000 do
                let context = $"seed={seed}, step={step}"
                match random.Next 100 with
                | action when action < 45 -> add false [|randomOperation()|]
                | action when action < 80 -> add true (Array.init (random.Next(1, 9)) (fun _ -> randomOperation()))
                | _ when model.Count > 0 -> dequeueExpected context priority queue model
                | _ -> add false [|randomOperation()|]
                checkState context queue model
            while model.Count > 0 do
                dequeueExpected $"seed={seed}: drain" priority queue model
                checkState $"seed={seed}: drain" queue model

        let randomized() =
            for seed in [12345; 67890; 104729] do randomizedRound seed

        let monitorRelease() =
            for token in [false; true] do
                for failure in ["comparison"; "interruption"] do monitorReleaseRound token failure
            monitorReleaseRound true "cancellation"

    let tests (target : TestTarget) =
        [
            "Dequeue maintains minimum-priority order",       Cases.ordering
            "Equal priorities preserve established ties",     Cases.equalPriority
            "Increased priorities repair the smallest child", Cases.increasedPriority
            "Zero operations do not evaluate priority",       Cases.zeroCount
            "Coalescing preserves count transitions",         Cases.counts
            "Seeded operations match the reference model",    Cases.randomized
            "Dequeue exceptions release the monitor",         Cases.monitorRelease
        ]
        |> prepareCasesCpu "ConcurrentDeltaPriorityQueue" target
