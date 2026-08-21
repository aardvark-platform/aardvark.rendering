namespace Aardvark.Rendering.Tests

open System
open System.Threading
open System.Threading.Tasks
open Expecto

module ``Task Utilities Tests`` =

    module private Helpers =

        let private timeout = TimeSpan.FromSeconds 2.0

        let waitForCompletion (task : Task) =
            let timeoutTask = Task.Delay timeout
            let completed = Task.WhenAny(task, timeoutTask).GetAwaiter().GetResult()
            Expect.isTrue (obj.ReferenceEquals(completed, task)) "Task did not complete within the timeout"

        let getResult (task : Task<'T>) =
            waitForCompletion task
            task.GetAwaiter().GetResult()

        let getException (task : Task<'T>) =
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

        let waitUntil (condition : unit -> bool) =
            Expect.isTrue (SpinWait.SpinUntil(Func<bool>(condition), timeout)) "Condition was not reached within the timeout"

    module private Cases =

        open Helpers

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

    [<Tests>]
    let tests =
        testList "Utilities.Task.bind" [
            testCase "Completed antecedent succeeds"                 Cases.completedAntecedent
            testCase "Asynchronous antecedent and inner succeed"     Cases.asynchronousAntecedentAndInner
            testCase "Synchronous mapper exception faults result"    Cases.synchronousMapperException
            testCase "Null inner task faults result"                 Cases.nullInnerTask
            testCase "Antecedent fault propagates"                   Cases.antecedentFault
            testCase "Antecedent cancellation propagates"            Cases.antecedentCancellation
            testCase "Inner fault propagates"                        Cases.innerFault
            testCase "Inner cancellation propagates"                 Cases.innerCancellation
        ]
