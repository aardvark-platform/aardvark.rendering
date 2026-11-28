namespace Aardvark.Rendering.Tests.Utilities

open System
open System.Threading
open System.Threading.Tasks
open Aardvark.Rendering.Tests
open Expecto

module TaskUtilities =

    module private Cases =

        let private timeout = TimeSpan.FromSeconds 2.0

        let private waitForCompletion (task : Task) =
            let timeoutTask = Task.Delay timeout
            let completed = Task.WhenAny(task, timeoutTask).GetAwaiter().GetResult()
            Expect.isTrue (obj.ReferenceEquals(completed, task)) "Task did not complete within the timeout"

        let private getResult (task : Task<'T>) =
            waitForCompletion task
            task.GetAwaiter().GetResult()

        let private getException (task : Task<'T>) =
            waitForCompletion task
            let error =
                try
                    task.GetAwaiter().GetResult() |> ignore
                    None
                with e ->
                    Some e

            match error with
                | Some e -> e
                | None -> failtest "Expected the task to fault"

        let private waitUntil (condition : unit -> bool) =
            Expect.isTrue (SpinWait.SpinUntil(Func<bool>(condition), timeout)) "Condition was not reached within the timeout"

        let completedAntecedent() =
            let mutable calls = 0

            let result =
                Task.FromResult 21
                |> Task.bind (fun value ->
                    Interlocked.Increment &calls |> ignore
                    Task.FromResult (value * 2)
                )

            Expect.equal (getResult result) 42 "Unexpected result"
            Expect.equal (Volatile.Read &calls) 1 "Mapper was not invoked exactly once"

        let asynchronousAntecedentAndInner() =
            let source = TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)
            let inner = TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)
            let mutable calls = 0
            let mutable mappedValue = 0

            let result =
                source.Task
                |> Task.bind (fun value ->
                    Volatile.Write(&mappedValue, value)
                    Interlocked.Increment &calls |> ignore
                    inner.Task
                )

            Expect.isFalse result.IsCompleted "Bind completed before the antecedent"
            Expect.equal (Volatile.Read &calls) 0 "Mapper ran before the antecedent completed"

            source.SetResult 21
            waitUntil (fun () -> Volatile.Read &calls = 1)

            Expect.equal (Volatile.Read &mappedValue) 21 "Mapper received the wrong value"
            Expect.isFalse result.IsCompleted "Bind completed before the inner task"

            inner.SetResult 42
            Expect.equal (getResult result) 42 "Unexpected asynchronous result"
            Expect.equal (Volatile.Read &calls) 1 "Mapper was not invoked exactly once"

        let synchronousMapperException() =
            let expected = InvalidOperationException "mapper failed"
            let mutable calls = 0

            let result : Task<int> =
                Task.FromResult 1
                |> Task.bind (fun _ ->
                    Interlocked.Increment &calls |> ignore
                    raise expected
                )

            let actual = getException result
            Expect.isTrue (obj.ReferenceEquals(actual, expected)) "Mapper exception identity was not preserved"
            Expect.equal (Volatile.Read &calls) 1 "Mapper was not invoked exactly once"

        let nullInnerTask() =
            let mutable calls = 0

            let result : Task<int> =
                Task.FromResult 1
                |> Task.bind (fun _ ->
                    Interlocked.Increment &calls |> ignore
                    null
                )

            let error = getException result
            Expect.isTrue (error :? InvalidOperationException) "Unexpected exception for a null inner task"
            Expect.equal error.Message "The task returned by the mapping function was null." "Unexpected null-inner exception message"
            Expect.equal (Volatile.Read &calls) 1 "Mapper was not invoked exactly once"

        let antecedentFault() =
            let expected = InvalidOperationException "source failed"
            let source = Task.FromException<int> expected
            let mutable calls = 0

            let result =
                source
                |> Task.bind (fun value ->
                    Interlocked.Increment &calls |> ignore
                    Task.FromResult value
                )

            let actual = getException result
            Expect.isTrue (actual :? AggregateException) "Antecedent fault was not propagated as an aggregate"
            Expect.isTrue (obj.ReferenceEquals((actual :?> AggregateException).InnerException, expected)) "Antecedent exception changed"
            Expect.equal (Volatile.Read &calls) 0 "Mapper ran for a faulted antecedent"

        let antecedentCancellation() =
            use cancellation = new CancellationTokenSource()
            cancellation.Cancel()
            let source = Task.FromCanceled<int> cancellation.Token
            let mutable calls = 0

            let result =
                source
                |> Task.bind (fun value ->
                    Interlocked.Increment &calls |> ignore
                    Task.FromResult value
                )

            waitForCompletion result
            Expect.isTrue result.IsCanceled "Antecedent cancellation was not propagated"
            Expect.equal (Volatile.Read &calls) 0 "Mapper ran for a canceled antecedent"

        let innerFault() =
            let expected = InvalidOperationException "inner failed"
            let inner = Task.FromException<int> expected
            let mutable calls = 0

            let result =
                Task.FromResult 1
                |> Task.bind (fun _ ->
                    Interlocked.Increment &calls |> ignore
                    inner
                )

            let actual = getException result
            Expect.isTrue (actual :? AggregateException) "Inner fault was not propagated as an aggregate"
            Expect.isTrue (obj.ReferenceEquals((actual :?> AggregateException).InnerException, expected)) "Inner exception changed"
            Expect.equal (Volatile.Read &calls) 1 "Mapper was not invoked exactly once"

        let innerCancellation() =
            use cancellation = new CancellationTokenSource()
            cancellation.Cancel()
            let inner = Task.FromCanceled<int> cancellation.Token
            let mutable calls = 0

            let result =
                Task.FromResult 1
                |> Task.bind (fun _ ->
                    Interlocked.Increment &calls |> ignore
                    inner
                )

            waitForCompletion result
            Expect.isTrue result.IsCanceled "Inner cancellation was not propagated"
            Expect.equal (Volatile.Read &calls) 1 "Mapper was not invoked exactly once"

        type private FaultTree = Branch of FaultTree list | Leaf of exn

        let rec private faultTree (error : exn) =
            match error with
            | :? AggregateException as aggregate -> Branch (aggregate.InnerExceptions |> Seq.map faultTree |> Seq.toList)
            | _ -> Leaf error

        let private taskError (task : Task) =
            waitForCompletion task
            let result = try task.GetAwaiter().GetResult(); None with error -> Some error
            match result with
            | Some error -> error
            | None -> failtest "Expected an unsuccessful task"

        let private countFor name = if name = "map2" || name = "zip" then 2 elif name = "mapN" then 3 else 1

        let private apply name mapping (inputs : Task<int>[]) : Task =
            match name with
            | "map" -> Task.map mapping inputs.[0] :> Task
            | "mapInline" -> Task.mapInline mapping inputs.[0] :> Task
            | "map2" -> Task.map2 (fun a b -> mapping (a + b)) inputs.[0] inputs.[1] :> Task
            | "mapN" -> Task.mapN (Array.sum >> mapping) inputs :> Task
            | "zip" -> Task.zip inputs.[0] inputs.[1] :> Task
            | "lift" -> Task.lift inputs.[0] :> Task
            | _ -> Task.ignore inputs.[0] :> Task

        let private complete deferred action =
            if deferred then
                let worker = Task.Run(Action action)
                waitForCompletion worker
                worker.GetAwaiter().GetResult()
            else action()

        let private canceled context (result : Task) =
            waitForCompletion result
            Expect.equal result.Status TaskStatus.Canceled (context + ": canceled status")
            Expect.isFalse result.IsFaulted (context + ": not faulted")
            Expect.isNull result.Exception (context + ": no fault aggregate")
            Expect.isTrue (taskError result :? OperationCanceledException) (context + ": canceled await")

        let canceledInputs() =
            for name in ["map"; "mapInline"; "map2"; "mapN"; "lift"; "ignore"] do
                for deferred in [false; true] do
                    for cancelIndex in 0 .. countFor name - 1 do
                        let context = $"operation={name}, deferred={deferred}, canceled index={cancelIndex}"
                        let sources = Array.init (countFor name) (fun _ -> TaskCompletionSource<int>())
                        let mutable calls = 0
                        let finish() =
                            for i in 0 .. sources.Length - 1 do
                                if i = cancelIndex then sources.[i].SetCanceled()
                                else sources.[i].SetResult(i + 3)
                        if not deferred then finish()
                        let result = apply name (fun x -> Interlocked.Increment &calls |> ignore; x) (sources |> Array.map (fun s -> s.Task))
                        if deferred then
                            Expect.isFalse result.IsCompleted (context + ": pending inputs")
                            complete true finish
                        canceled context result
                        Expect.equal (Volatile.Read &calls) 0 (context + ": mapping suppressed")

        let combinedOutcomes() =
            for name in ["map2"; "mapN"; "zip"] do
                for deferred in [false; true] do
                    for left in 0 .. 3 do
                        for right in 0 .. 3 do
                            for rotation in 0 .. countFor name - 1 do
                                let codes = if name = "mapN" then [|left; right; 1|] else [|left; right|]
                                let codes = Array.init codes.Length (fun i -> codes.[(i + rotation) % codes.Length])
                                let states = codes |> Array.map string |> String.concat ","
                                let context = $"operation={name}, deferred={deferred}, states={states} (success/cancel/fault/OCE-fault)"
                                let sources = Array.init codes.Length (fun _ -> TaskCompletionSource<int>())
                                let markers = Array.init codes.Length (fun i -> if codes.[i] = 3 then OperationCanceledException $"fault {i}" :> exn else InvalidOperationException $"fault {i}" :> exn)
                                let mutable calls = 0
                                let finish (result : Task option) =
                                    for i in codes.Length - 1 .. -1 .. 0 do
                                        match codes.[i] with
                                        | 0 -> sources.[i].SetResult(i + 3)
                                        | 1 -> sources.[i].SetCanceled()
                                        | _ -> sources.[i].SetException markers.[i]
                                        if i > 0 then result |> Option.iter (fun t -> Expect.isFalse t.IsCompleted (context + ": waits for every input"))
                                if not deferred then finish None
                                let result = apply name (fun x -> Interlocked.Increment &calls |> ignore; x) (sources |> Array.map (fun s -> s.Task))
                                if deferred then complete true (fun () -> finish (Some result))
                                let faults = Array.zip codes markers |> Array.choose (fun (code, error) -> if code >= 2 then Some error else None) |> Array.toList
                                if not faults.IsEmpty then
                                    let expected =
                                        if name = "mapN" then Branch (List.map Leaf faults)
                                        elif faults.Length = 1 then Branch [Leaf faults.Head]
                                        else Branch (List.map (fun error -> Branch [Leaf error]) faults)
                                    let actual = taskError result
                                    Expect.isTrue result.IsFaulted (context + ": faults take precedence")
                                    Expect.equal (faultTree actual) expected (context + ": fault nesting order and identities")
                                    Expect.equal (faultTree result.Exception) (Branch [expected]) (context + ": returned fault contents")
                                elif Array.contains 1 codes then canceled context result
                                else
                                    waitForCompletion result
                                    Expect.equal result.Status TaskStatus.RanToCompletion (context + ": success")
                                let expectedCalls = if faults.IsEmpty && not (Array.contains 1 codes) && name <> "zip" then 1 else 0
                                Expect.equal (Volatile.Read &calls) expectedCalls (context + ": mapping count")

        let faultsAreNotCancellation() =
            for name in ["map"; "mapInline"; "map2"; "mapN"; "lift"; "ignore"] do
                for deferred in [false; true] do
                    for kind in [0; 1; 2] do
                        let context = $"operation={name}, deferred={deferred}, exception kind={kind}"
                        let marker : exn =
                            if kind = 0 then InvalidOperationException context
                            elif kind = 1 then OperationCanceledException context
                            else AggregateException(OperationCanceledException context)
                        let sources = Array.init (countFor name) (fun _ -> TaskCompletionSource<int>())
                        let mutable calls = 0
                        let finish() =
                            sources.[0].SetException marker
                            for i in 1 .. sources.Length - 1 do sources.[i].SetResult i
                        if not deferred then finish()
                        let result = apply name (fun x -> Interlocked.Increment &calls |> ignore; x) (sources |> Array.map (fun s -> s.Task))
                        if deferred then complete true finish
                        Expect.equal (faultTree (taskError result)) (Branch [faultTree marker]) (context + ": faulted input remains a fault")
                        Expect.isTrue result.IsFaulted (context + ": fault status")
                        Expect.equal (Volatile.Read &calls) 0 (context + ": faulted input suppresses mapping")

                        if name <> "lift" && name <> "ignore" then
                            let sources = Array.init (countFor name) (fun _ -> TaskCompletionSource<int>())
                            let mutable calls = 0
                            let finish() = for source in sources do source.SetResult 3
                            if not deferred then finish()
                            let result = apply name (fun _ -> Interlocked.Increment &calls |> ignore; raise marker) (sources |> Array.map (fun s -> s.Task))
                            if deferred then complete true finish
                            let actual = taskError result
                            Expect.isTrue (obj.ReferenceEquals(actual, marker)) (context + ": mapper exception identity")
                            Expect.isTrue result.IsFaulted (context + ": mapper OCE is not cancellation")
                            Expect.equal (Volatile.Read &calls) 1 (context + ": one mapping invocation")

        let successfulCombinators() =
            for name in ["map"; "mapInline"; "map2"; "mapN"; "lift"; "ignore"] do
                for deferred in [false; true] do
                    let context = $"operation={name}, deferred={deferred}"
                    let sources = Array.init (countFor name) (fun _ -> TaskCompletionSource<int>())
                    let mutable calls = 0
                    let finish() = for i in 0 .. sources.Length - 1 do sources.[i].SetResult(i + 3)
                    if not deferred then finish()
                    let result = apply name (fun x -> Interlocked.Increment &calls |> ignore; x * 2 + 1) (sources |> Array.map (fun s -> s.Task))
                    if deferred then complete true finish
                    waitForCompletion result
                    Expect.equal result.Status TaskStatus.RanToCompletion (context + ": success status")
                    if name <> "lift" && name <> "ignore" then
                        Expect.equal ((result :?> Task<int>).Result) (Array.init sources.Length (fun i -> i + 3) |> Array.sum |> fun x -> x * 2 + 1) (context + ": value")
                        Expect.equal (Volatile.Read &calls) 1 (context + ": mapping once")
                    else Expect.equal (Volatile.Read &calls) 0 (context + ": no user mapping")
            Expect.equal (getResult (Task.map string (Task.FromResult 7))) "7" "Type-changing map"
            Expect.equal (getResult (Task.mapInline string (Task.FromResult 9))) "9" "Type-changing inline map"
            Expect.equal (getResult (Task.map2 (fun (x : int) (y : string) -> y + string x) (Task.FromResult 7) (Task.FromResult "v"))) "v7" "Heterogeneous map2"
            let mutable enumerations = 0
            let mutable calls = 0
            let inputs = seq { enumerations <- enumerations + 1; yield Task.FromResult "a"; yield Task.FromResult "b" }
            Expect.equal (getResult (Task.mapN (fun values -> calls <- calls + 1; String.concat "" values) inputs)) "ab" "Type-changing mapN"
            Expect.equal (enumerations, calls) (1, 1) "Sequence consumed and mapping invoked once"
            let mutable emptyCalls = 0
            Expect.equal (getResult (Task.mapN (fun (values : int[]) -> emptyCalls <- emptyCalls + 1; values.Length) Seq.empty)) 0 "Empty mapN succeeds"
            Expect.equal emptyCalls 1 "Empty mapN invokes mapping once"

        let private listOutcome name value (build : (int -> Task<'T>) -> int list -> Task<int list>) deferred count cancelIndex faultIndex =
            let context = $"list={name}, deferred={deferred}, count={count}, canceled index={cancelIndex}, fault index={faultIndex}"
            let sources = Array.init count (fun _ -> TaskCompletionSource<'T>())
            let marker = OperationCanceledException "faulted element, not cancellation"
            let seen = ResizeArray<int>()
            let finish() =
                for i in count - 1 .. -1 .. 0 do
                    if i = faultIndex then sources.[i].SetException marker
                    elif i = cancelIndex then sources.[i].SetCanceled()
                    else sources.[i].SetResult(value i)
            if not deferred then finish()
            let result = build (fun i -> seen.Add i; sources.[i].Task) [0 .. count - 1]
            if name = "collectT" && count = 1 then
                Expect.isTrue (obj.ReferenceEquals(result, sources.[0].Task)) (context + ": singleton identity retained")
            if deferred then complete true finish
            if faultIndex >= 0 then
                let actual = taskError result
                Expect.isTrue result.IsFaulted (context + ": fault beats cancellation")
                let rec leaves tree = match tree with Leaf error -> [error] | Branch children -> List.collect leaves children
                Expect.equal (leaves (faultTree actual)) [marker :> exn] (context + ": fault marker preserved")
            elif cancelIndex >= 0 then canceled context result
            else
                Expect.equal (getResult result) [0 .. count - 1] (context + ": list values")
            let order = if count = 3 && name <> "mapT" then [1; 2; 0] else [0 .. count - 1]
            Expect.equal (Seq.toList seen) order (context + ": producer count and order")

        let listsAndZip() =
            for deferred in [false; true] do
                for count in [0; 1; 2; 3] do
                    for cancelIndex in -1 .. count - 1 do
                        for faultIndex in [-1; if count > 1 && cancelIndex >= 0 then (cancelIndex + 1) % count] do
                            listOutcome "mapT" id List.mapT deferred count cancelIndex faultIndex
                            listOutcome "collectT" List.singleton List.collectT deferred count cancelIndex faultIndex
                            listOutcome "chooseT" Some List.chooseT deferred count cancelIndex faultIndex
                            listOutcome "filterT" (fun _ -> true) List.filterT deferred count cancelIndex faultIndex
                for cancelIndex in [0; 1] do
                    let sources = Array.init 2 (fun _ -> TaskCompletionSource<int>())
                    let finish() =
                        for i in 0 .. 1 do
                            if i = cancelIndex then sources.[i].SetCanceled() else sources.[i].SetResult i
                    if not deferred then finish()
                    let result = Task.zip sources.[0].Task sources.[1].Task
                    if deferred then complete true finish
                    canceled $"zip deferred={deferred}, canceled index={cancelIndex}" result

        let continuationScheduling() =
            let pair = ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, 1)
            let scheduler = pair.ExclusiveScheduler
            try
                for name in ["map"; "mapInline"; "map2"; "mapN"; "lift"; "ignore"] do
                    for deferred in [false; true] do
                        let context = $"operation={name}, deferred={deferred}"
                        let sources = Array.init (countFor name) (fun _ -> TaskCompletionSource<int>())
                        if not deferred then for source in sources do source.SetResult 3
                        let mutable observed = Unchecked.defaultof<TaskScheduler>
                        let create = Task.Factory.StartNew((fun () ->
                            let result = apply name (fun x -> observed <- TaskScheduler.Current; x) (sources |> Array.map (fun s -> s.Task))
                            if not deferred then
                                let inlineExpected = name = "mapInline" || name = "lift" || name = "ignore"
                                Expect.equal result.IsCompleted inlineExpected (context + ": continuation option retained")
                            result), CancellationToken.None, TaskCreationOptions.None, scheduler)
                        let result = getResult create
                        if deferred then for source in sources do source.SetResult 3
                        waitForCompletion result
                        Expect.equal result.Status TaskStatus.RanToCompletion (context + ": scheduled completion")
                        if name <> "lift" && name <> "ignore" then
                            Expect.isTrue (obj.ReferenceEquals(observed, scheduler)) (context + ": current scheduler retained")
            finally
                pair.Complete()
                waitForCompletion pair.Completion

        let attachedChildren() =
            for name in ["map"; "mapInline"; "map2"; "mapN"] do
                for childFaults in [false; true] do
                    use entered = new ManualResetEventSlim(false)
                    use release = new ManualResetEventSlim(false)
                    let context = $"operation={name}, child faults={childFaults}"
                    let marker = InvalidOperationException context
                    let sources = Array.init (countFor name) (fun _ -> TaskCompletionSource<int>())
                    let mutable child = Unchecked.defaultof<Task>
                    let mapping x =
                        let childAction() =
                            entered.Set()
                            if not (release.Wait timeout) then failwith "Child release timeout"
                            if childFaults then raise marker
                        child <- Task.Factory.StartNew(Action childAction, CancellationToken.None, TaskCreationOptions.AttachedToParent, TaskScheduler.Default)
                        x
                    let result = apply name mapping (sources |> Array.map (fun s -> s.Task))
                    try
                        for source in sources do source.SetResult 3
                        Expect.isTrue (entered.Wait timeout) (context + ": child started")
                        Expect.isFalse result.IsCompleted (context + ": waits for attached child")
                    finally release.Set()
                    waitForCompletion result
                    waitForCompletion child
                    if childFaults then
                        Expect.isTrue result.IsFaulted (context + ": child fault retained")
                        Expect.equal (faultTree (taskError result)) (Branch [Leaf marker]) (context + ": child fault contents")
                    else Expect.equal result.Status TaskStatus.RanToCompletion (context + ": child success retained")

    let tests (target: TestTarget) =
        [
            "Completed antecedent succeeds",              Cases.completedAntecedent
            "Asynchronous antecedent and inner succeed",  Cases.asynchronousAntecedentAndInner
            "Synchronous mapper exception faults result", Cases.synchronousMapperException
            "Null inner task faults result",              Cases.nullInnerTask
            "Antecedent fault propagates",                Cases.antecedentFault
            "Antecedent cancellation propagates",         Cases.antecedentCancellation
            "Inner fault propagates",                     Cases.innerFault
            "Inner cancellation propagates",              Cases.innerCancellation
        ]
        |> prepareCasesCpu "Task.bind" target

    let cancellationTests (target : TestTarget) =
        [
            "Canceled inputs suppress mappings",                  Cases.canceledInputs
            "Combined outcomes preserve precedence and contents", Cases.combinedOutcomes
            "Faults and mapper exceptions are not cancellation",  Cases.faultsAreNotCancellation
            "Successful values and invocation counts",            Cases.successfulCombinators
            "Zip and list cancellation is arity independent",     Cases.listsAndZip
            "Continuation scheduler and inline options",          Cases.continuationScheduling
            "Attached children retain completion and faults",     Cases.attachedChildren
        ]
        |> prepareCasesCpu "Task.Combinators" target
