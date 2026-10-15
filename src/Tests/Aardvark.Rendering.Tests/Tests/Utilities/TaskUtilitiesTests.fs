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

    module private Polling =

        type private Outcome = Success | Fault | Cancellation

        let private timeout = TimeSpan.FromSeconds 5.0

        let private waitForCompletion context (task : Task) =
            Expect.isTrue (SpinWait.SpinUntil((fun () -> task.IsCompleted), timeout)) (context + ": completion timed out")

        let private checkFault context (expected : exn[]) (error : exn) =
            Expect.isTrue (error :? AggregateException) (context + ": fault retains its aggregate")
            let actual = (error :?> AggregateException).InnerExceptions
            Expect.equal actual.Count expected.Length (context + ": aggregate contents")
            for index = 0 to expected.Length - 1 do
                Expect.isTrue (obj.ReferenceEquals(actual.[index], expected.[index])) (sprintf "%s: original exception %d" context index)

        let private checkSuccess context (task : Task<'T>) expected =
            for read = 1 to 8 do
                match Task.pollResult task with
                | Some (Completed value) -> Expect.equal value expected (sprintf "%s, read=%d: value" context read)
                | result -> failtestf "%s, read=%d: expected success, got %A" context read result

        let private checkTerminal context outcome expected errors result =
            match outcome, result with
            | Success, Some (Completed value) -> Expect.equal value expected (context + ": successful value")
            | Fault, Some (Faulted error) -> checkFault context errors error
            | Cancellation, Some Cancelled -> ()
            | _ -> failtestf "%s: wrong terminal result %A" context result

        let pendingAndSuccess () =
            let source = TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)
            let reader = Task.Run(fun () ->
                for read = 1 to 1000 do
                    Expect.isNone (Task.pollResult source.Task) (sprintf "pending, read=%d: nonblocking None" read)
            )
            try
                waitForCompletion "pending polling" reader
                reader.GetAwaiter().GetResult()
                Expect.isFalse source.Task.IsCompleted "polling leaves the pending task unchanged"
            finally
                source.TrySetResult 42 |> ignore
                waitForCompletion "pending reader cleanup" reader
                if reader.IsFaulted then reader.Exception |> ignore
            checkSuccess "completed source" source.Task 42
            for value in [Int32.MinValue; -1; 0; 1; Int32.MaxValue] do
                checkSuccess (sprintf "integer=%d" value) (Task.FromResult value) value
            let reference = obj()
            checkSuccess "reference identity" (Task.FromResult reference) reference
            checkSuccess "null successful value" (Task.FromResult<string> null) null
            checkSuccess "unit successful value" (Task.FromResult ()) ()
            Expect.throwsT<NullReferenceException> (fun () -> Task.pollResult (null : Task<int>) |> ignore) "null task behavior remains unchanged"

        let faultAndCancellation () =
            let first = InvalidOperationException "first"
            let second = ArgumentException "second"
            let nested = AggregateException(first, second)
            let canceledError = OperationCanceledException "faulted, not canceled"
            for index, errors in [
                0, [| first :> exn |]
                1, [| first :> exn; second :> exn |]
                2, [| nested :> exn; first :> exn |]
                3, [| canceledError :> exn |]
            ] do
                let source = TaskCompletionSource<int>()
                source.SetException errors
                for read = 1 to 8 do
                    let context = sprintf "fault=%d, read=%d" index read
                    match Task.pollResult source.Task with
                    | Some (Faulted error) -> checkFault context errors error
                    | result -> failtestf "%s: expected fault, got %A" context result
            use cancellation = new CancellationTokenSource()
            cancellation.Cancel()
            let source = TaskCompletionSource<int>()
            source.SetCanceled()
            for index, task in [0, source.Task; 1, Task.FromCanceled<int> cancellation.Token] do
                for read = 1 to 8 do
                    checkTerminal (sprintf "cancellation=%d, read=%d" index read) Cancellation 0 [||] (Task.pollResult task)

        let concurrentCompletion () =
            let failures = ResizeArray<string>()
            for outcome in [Success; Fault; Cancellation] do
                let mutable recordedFailures = 0
                use start = new AutoResetEvent(false)
                use finished = new AutoResetEvent(false)
                let mutable stopping = 0
                let mutable current = Unchecked.defaultof<TaskCompletionSource<int>>
                let mutable value = 0
                let mutable errors : exn[] = [||]
                let mutable workerError = None
                let worker = Thread(ThreadStart(fun () ->
                    try
                        while start.WaitOne(timeout) && Volatile.Read(&stopping) = 0 do
                            // Vary the completion window without assuming a sleep establishes overlap.
                            Thread.SpinWait(value &&& 63)
                            match outcome with
                            | Success -> current.SetResult value
                            | Fault -> current.SetException errors
                            | Cancellation -> current.SetCanceled()
                            finished.Set() |> ignore
                    with error -> workerError <- Some error
                ), IsBackground = true)
                worker.Start()
                try
                    for trial = 1 to 4096 do
                        let source = TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)
                        let original = InvalidOperationException(sprintf "outcome=%A, trial=%d" outcome trial)
                        let nested = AggregateException(ArgumentException "nested original")
                        let expectedErrors = [| original :> exn; nested :> exn |]
                        let context = sprintf "outcome=%A, trial=%d" outcome trial
                        current <- source
                        value <- trial
                        errors <- expectedErrors
                        Expect.isNone (Task.pollResult source.Task) (context + ": initially pending")
                        start.Set() |> ignore
                        let mutable result = None
                        let mutable pollingError = None
                        let deadline = System.Diagnostics.Stopwatch.StartNew()
                        while Option.isNone result && deadline.Elapsed < timeout do
                            try result <- Task.pollResult source.Task
                            with error -> pollingError <- Some error
                        Expect.isSome result (context + ": polling completes within the bound")
                        Expect.isTrue (finished.WaitOne(timeout)) (context + ": completion worker finishes")
                        checkTerminal context outcome trial expectedErrors result
                        for read = 1 to 4 do
                            checkTerminal (sprintf "%s, settled read=%d" context read) outcome trial expectedErrors (Task.pollResult source.Task)
                        match pollingError with
                        | Some error when recordedFailures < 2 ->
                            failures.Add(sprintf "%s: polling threw %A" context error)
                            recordedFailures <- recordedFailures + 1
                        | _ -> ()
                finally
                    Volatile.Write(&stopping, 1)
                    start.Set() |> ignore
                    Expect.isTrue (worker.Join(timeout)) (sprintf "outcome=%A: completion worker cleanup" outcome)
                Expect.isNone workerError (sprintf "outcome=%A: completion worker error" outcome)
            Expect.isEmpty failures ("polling must return states rather than throw during completion: " + String.concat "\n" failures)

    let pollTests (target : TestTarget) =
        [
            "Pending successful and null inputs",  Polling.pendingAndSuccess
            "Fault aggregates and cancellation",   Polling.faultAndCancellation
            "Concurrent completion while polling", Polling.concurrentCompletion
        ]
        |> prepareCasesCpu "Task.pollResult" target

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
