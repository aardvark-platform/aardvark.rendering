namespace Aardvark.Rendering.Tests

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open Aardvark.Rendering
open Expecto

module ``ColoredLock Tests`` =

    type private CallbackEvent<'a> =
        | Locked of Option<'a>
        | Unlocked of Option<'a>

    type private CallbackKey<'a> =
        | ExclusiveKey
        | ColorKey of 'a

    type private CallbackRecorder<'a when 'a : equality>() =
        let gate = obj()
        let events = ResizeArray<CallbackEvent<'a>>()
        let balances = Dictionary<CallbackKey<'a>, int>()
        let mutable violation = false

        let key color =
            match color with
            | Some color -> ColorKey color
            | None -> ExclusiveKey

        let getBalance color =
            match balances.TryGetValue (key color) with
            | true, value -> value
            | _ -> 0

        member _.OnLock(color : Option<'a>) =
            lock gate (fun () ->
                let balance = getBalance color
                if balance <> 0 then violation <- true
                balances.[key color] <- balance + 1
                events.Add(Locked color)
            )

        member _.OnUnlock(color : Option<'a>) =
            lock gate (fun () ->
                let balance = getBalance color
                if balance <> 1 then violation <- true
                balances.[key color] <- balance - 1
                events.Add(Unlocked color)
            )

        member _.Events =
            lock gate (fun () -> events.ToArray())

        member _.HasViolation =
            lock gate (fun () -> violation)

        member _.IsBalanced =
            lock gate (fun () -> balances.Values |> Seq.forall ((=) 0))

    [<AutoOpen>]
    module private Helpers =

        let timeout = TimeSpan.FromSeconds 10.0

        let waitCountdown (name : string) (value : CountdownEvent) =
            Expect.isTrue (value.Wait timeout) $"Timed out waiting for {name}"

        let waitEvent (name : string) (value : ManualResetEventSlim) =
            Expect.isTrue (value.Wait timeout) $"Timed out waiting for {name}"

        let joinThread (name : string) (thread : Thread) =
            Expect.isTrue (thread.Join timeout) $"Timed out joining {name}"

        let startWorker (name : string) (errors : ConcurrentQueue<exn>) (completed : CountdownEvent) (action : unit -> unit) =
            let thread =
                Thread(ThreadStart(fun () ->
                    try
                        try action()
                        with e -> errors.Enqueue e
                    finally
                        completed.Signal() |> ignore
                ))

            thread.Name <- name
            thread.IsBackground <- true
            thread.Start()
            thread

        let assertNoErrors (errors : ConcurrentQueue<exn>) =
            let errors = errors.ToArray()
            if errors.Length > 0 then
                failtestf "Worker failed: %A" errors

        let assertRecorder (recorder : CallbackRecorder<'a>) (expected : CallbackEvent<'a> list) =
            Expect.isFalse recorder.HasViolation "Callbacks overlapped, duplicated, or became negative"
            Expect.isTrue recorder.IsBalanced "Callback balance leaked"
            Expect.sequenceEqual recorder.Events expected "Unexpected callback order"

        let updateMaximum (location : byref<int>) (value : int) =
            let mutable current = Volatile.Read &location
            while value > current && Interlocked.CompareExchange(&location, value, current) <> current do
                current <- Volatile.Read &location

    module private Cases =

        let singleColored() =
            let coloredLock = ResourceLock()
            let recorder = CallbackRecorder<ResourceUsage>()
            let resource =
                { new ILockedResource with
                    member _.Lock = coloredLock
                    member _.OnLock color = recorder.OnLock color
                    member _.OnUnlock color = recorder.OnUnlock color
                }

            LockedResource.render resource (fun () ->
                Expect.equal coloredLock.Status (Colored ResourceUsage.Render) "Render ownership is not visible"
            )

            Expect.equal coloredLock.Status NotEntered "Render ownership leaked"
            assertRecorder recorder [ Locked (Some ResourceUsage.Render); Unlocked (Some ResourceUsage.Render) ]

        let sameColorNesting() =
            let coloredLock = ColoredLock<int>()
            let recorder = CallbackRecorder<int>()

            coloredLock.Enter(1, recorder.OnLock)
            Expect.equal coloredLock.Status (Colored 1) "Outer ownership is missing"

            coloredLock.Enter(1, recorder.OnLock)
            Expect.equal coloredLock.Status (Colored 1) "Nested ownership is missing"

            coloredLock.Exit recorder.OnUnlock
            Expect.equal coloredLock.Status (Colored 1) "The first exit released the outer ownership"

            coloredLock.Exit recorder.OnUnlock
            Expect.equal coloredLock.Status NotEntered "Same-color ownership leaked"

            assertRecorder recorder [ Locked (Some 1); Unlocked (Some 1) ]

        let differentColorNesting() =
            let coloredLock = ColoredLock<int>()
            let recorder = CallbackRecorder<int>()

            coloredLock.Enter(1, recorder.OnLock)
            Expect.equal coloredLock.Status (Colored 1) "Outer ownership is missing"

            coloredLock.Enter(2, recorder.OnLock)
            Expect.equal coloredLock.Status (Colored 2) "Inner ownership is missing"

            coloredLock.Exit recorder.OnUnlock
            Expect.equal coloredLock.Status (Colored 1) "Outer ownership was not restored"

            coloredLock.Exit recorder.OnUnlock
            Expect.equal coloredLock.Status NotEntered "Different-color ownership leaked"

            assertRecorder recorder [
                Locked (Some 1)
                Locked (Some 2)
                Unlocked (Some 2)
                Unlocked (Some 1)
            ]

        let structuralColorNesting() =
            let coloredLock = ColoredLock<int array>()
            let recorder = CallbackRecorder<int array>()
            let outer = [| 1 |]
            let equivalentOuter = [| 1 |]
            let inner = [| 2 |]

            Expect.isFalse (Object.ReferenceEquals(outer, equivalentOuter)) "Test colors unexpectedly share an identity"

            coloredLock.Enter(outer, recorder.OnLock)
            coloredLock.Enter(inner, recorder.OnLock)
            coloredLock.Enter(equivalentOuter, recorder.OnLock)
            Expect.equal coloredLock.Status (Colored equivalentOuter) "Structurally equal ownership was not restored"

            coloredLock.Exit recorder.OnUnlock
            Expect.equal coloredLock.Status (Colored inner) "Inner ownership was not restored"
            coloredLock.Exit recorder.OnUnlock
            Expect.equal coloredLock.Status (Colored outer) "Outer ownership was not restored"
            coloredLock.Exit recorder.OnUnlock

            Expect.equal coloredLock.Status NotEntered "Structural-color ownership leaked"
            assertRecorder recorder [
                Locked (Some outer)
                Locked (Some inner)
                Unlocked (Some inner)
                Unlocked (Some outer)
            ]

        let nonReflexiveColorNesting() =
            let coloredLock = ColoredLock<float>()
            let events = ResizeArray<string>()

            let onLock = function
                | Some value when Double.IsNaN value -> events.Add "lock nan"
                | Some _ -> events.Add "lock value"
                | None -> events.Add "lock exclusive"

            let onUnlock = function
                | Some value when Double.IsNaN value -> events.Add "unlock nan"
                | Some _ -> events.Add "unlock value"
                | None -> events.Add "unlock exclusive"

            coloredLock.Enter(nan, onLock)
            coloredLock.Enter(1.0, onLock)
            coloredLock.Exit onUnlock

            match coloredLock.Status with
            | Colored value -> Expect.isTrue (Double.IsNaN value) "NaN outer ownership was not restored"
            | status -> failtestf "Unexpected restored status: %A" status

            coloredLock.Exit onUnlock
            Expect.equal coloredLock.Status NotEntered "Non-reflexive ownership leaked"
            Expect.sequenceEqual events [ "lock nan"; "lock value"; "unlock value"; "unlock nan" ] "Unexpected non-reflexive callback order"

        let nullColorNesting() =
            let coloredLock = ColoredLock<string>()
            let recorder = CallbackRecorder<string>()

            coloredLock.Enter(null, recorder.OnLock)
            coloredLock.Enter("inner", recorder.OnLock)
            coloredLock.Exit recorder.OnUnlock
            Expect.equal coloredLock.Status (Colored null) "Null outer color was not restored"
            coloredLock.Exit recorder.OnUnlock

            Expect.equal coloredLock.Status NotEntered "Null-colored ownership leaked"
            assertRecorder recorder [
                Locked (Some null)
                Locked (Some "inner")
                Unlocked (Some "inner")
                Unlocked (Some null)
            ]

        let coloredExclusiveNesting() =
            let coloredLock = ColoredLock<int>()
            let recorder = CallbackRecorder<int>()

            coloredLock.Enter(1, recorder.OnLock)
            coloredLock.Enter recorder.OnLock
            Expect.equal coloredLock.Status Exclusive "Exclusive ownership is missing"

            coloredLock.Exit recorder.OnUnlock
            Expect.equal coloredLock.Status (Colored 1) "Colored ownership was not restored"

            coloredLock.Exit recorder.OnUnlock
            Expect.equal coloredLock.Status NotEntered "Colored/exclusive ownership leaked"

            assertRecorder recorder [
                Locked (Some 1)
                Locked None
                Unlocked None
                Unlocked (Some 1)
            ]

        let exclusiveNesting() =
            let coloredLock = ColoredLock<int>()
            let recorder = CallbackRecorder<int>()

            coloredLock.Enter recorder.OnLock
            Expect.equal coloredLock.Status Exclusive "Exclusive ownership is missing"

            coloredLock.Enter(1, recorder.OnLock)
            coloredLock.Enter recorder.OnLock
            Expect.equal coloredLock.Status Exclusive "Nested calls changed exclusive ownership"

            coloredLock.Exit recorder.OnUnlock
            coloredLock.Exit recorder.OnUnlock
            Expect.equal coloredLock.Status Exclusive "Nested exits released exclusive ownership"

            coloredLock.Exit recorder.OnUnlock
            Expect.equal coloredLock.Status NotEntered "Exclusive ownership leaked"

            assertRecorder recorder [ Locked None; Unlocked None ]

        let protectedActionExceptions() =
            let coloredLock = ColoredLock<int>()

            Expect.throws (fun () ->
                coloredLock.Use(1, fun () -> raise (InvalidOperationException "colored action"))
            ) "Colored action did not throw"
            Expect.equal coloredLock.Status NotEntered "Colored action failure leaked ownership"

            Expect.throws (fun () ->
                coloredLock.Use(fun () -> raise (InvalidOperationException "exclusive action"))
            ) "Exclusive action did not throw"
            Expect.equal coloredLock.Status NotEntered "Exclusive action failure leaked ownership"

            coloredLock.Use(1, fun () ->
                Expect.throws (fun () ->
                    coloredLock.Use(2, fun () -> raise (InvalidOperationException "nested action"))
                ) "Nested action did not throw"
                Expect.equal coloredLock.Status (Colored 1) "Nested action failure lost outer ownership"
            )

            coloredLock.Use(3, ignore)
            Expect.equal coloredLock.Status NotEntered "Lock could not be reused after action failures"

            let resourceLock = ResourceLock()
            let recorder = CallbackRecorder<ResourceUsage>()
            let resource =
                { new ILockedResource with
                    member _.Lock = resourceLock
                    member _.OnLock color = recorder.OnLock color
                    member _.OnUnlock color = recorder.OnUnlock color
                }

            Expect.throws (fun () ->
                LockedResource.render resource (fun () -> raise (InvalidOperationException "render action"))
            ) "Render action did not throw"

            Expect.throws (fun () ->
                LockedResource.access resource (fun () -> raise (InvalidOperationException "access action"))
            ) "Access action did not throw"

            Expect.throws (fun () ->
                LockedResource.update resource (fun () -> raise (InvalidOperationException "update action"))
            ) "Update action did not throw"

            Expect.equal resourceLock.Status NotEntered "Resource action failure leaked ownership"
            assertRecorder recorder [
                Locked (Some ResourceUsage.Render)
                Unlocked (Some ResourceUsage.Render)
                Locked (Some ResourceUsage.Access)
                Unlocked (Some ResourceUsage.Access)
                Locked None
                Unlocked None
            ]

        let callbackExceptions() =
            let coloredLock = ColoredLock<int>()
            let recorder = CallbackRecorder<int>()
            let errors = ConcurrentQueue<exn>()
            use completed = new CountdownEvent(1)

            let worker =
                startWorker "ColoredLock callback failures" errors completed (fun () ->
                    coloredLock.Enter(1, recorder.OnLock)
                    try
                        Expect.throws (fun () ->
                            coloredLock.Enter(2, fun _ -> raise (InvalidOperationException "colored lock callback"))
                        ) "Colored OnLock did not throw"
                        Expect.equal coloredLock.Status (Colored 1) "Colored OnLock failure lost outer ownership"

                        coloredLock.Enter(2, recorder.OnLock)
                        Expect.throws (fun () ->
                            coloredLock.Exit(fun color ->
                                recorder.OnUnlock color
                                raise (InvalidOperationException "colored unlock callback")
                            )
                        ) "Colored OnUnlock did not throw"
                        Expect.equal coloredLock.Status (Colored 1) "Colored OnUnlock failure lost outer ownership"

                        Expect.throws (fun () ->
                            coloredLock.Enter(fun _ -> raise (InvalidOperationException "exclusive lock callback"))
                        ) "Exclusive OnLock did not throw"
                        Expect.equal coloredLock.Status (Colored 1) "Exclusive OnLock failure lost outer ownership"

                        coloredLock.Enter recorder.OnLock
                        Expect.throws (fun () ->
                            coloredLock.Exit(fun color ->
                                recorder.OnUnlock color
                                raise (InvalidOperationException "exclusive unlock callback")
                            )
                        ) "Exclusive OnUnlock did not throw"
                        Expect.equal coloredLock.Status (Colored 1) "Exclusive OnUnlock failure lost outer ownership"
                    finally
                        coloredLock.Exit recorder.OnUnlock

                    Expect.equal coloredLock.Status NotEntered "Callback failure leaked worker ownership"
                )

            waitCountdown "callback-failure worker" completed
            joinThread "callback-failure worker" worker
            assertNoErrors errors

            use reused = new CountdownEvent(1)
            let reuseWorker =
                startWorker "ColoredLock callback reuse" errors reused (fun () ->
                    coloredLock.Use(3, ignore)
                    Expect.equal coloredLock.Status NotEntered "Cross-thread reuse leaked ownership"
                )

            waitCountdown "cross-thread reuse" reused
            joinThread "cross-thread reuse" reuseWorker
            assertNoErrors errors

            assertRecorder recorder [
                Locked (Some 1)
                Locked (Some 2)
                Unlocked (Some 2)
                Locked None
                Unlocked None
                Unlocked (Some 1)
            ]

        let crossLockCallbacks() =
            let firstLock = ColoredLock<int>()
            let secondLock = ColoredLock<int>()
            let errors = ConcurrentQueue<exn>()
            use callbacksReady = new CountdownEvent(2)
            use startCrossLock = new ManualResetEventSlim(false)
            use completed = new CountdownEvent(2)

            let action (own : ColoredLock<int>) (other : ColoredLock<int>) =
                own.Enter(1, fun _ ->
                    callbacksReady.Signal() |> ignore
                    startCrossLock.Wait()
                    other.Use(1, ignore)
                )

                try
                    Expect.equal own.Status (Colored 1) "Callback acquisition is missing"
                finally
                    own.Exit ignore

                Expect.equal own.Status NotEntered "Cross-lock callback leaked ownership"

            let first = startWorker "ColoredLock cross callback 1" errors completed (fun () -> action firstLock secondLock)
            let second = startWorker "ColoredLock cross callback 2" errors completed (fun () -> action secondLock firstLock)

            try
                waitCountdown "cross-lock callbacks" callbacksReady
                startCrossLock.Set()
                waitCountdown "cross-lock workers" completed
                joinThread "cross-lock worker 1" first
                joinThread "cross-lock worker 2" second
            finally
                startCrossLock.Set()
                completed.Wait timeout |> ignore
                first.Join timeout |> ignore
                second.Join timeout |> ignore

            assertNoErrors errors

        let crossThreadFinalCallback() =
            let resourceLock = ResourceLock()
            let recorder = CallbackRecorder<ResourceUsage>()
            use lifecycleGate = new SemaphoreSlim(1, 1)
            let errors = ConcurrentQueue<exn>()
            use firstEntered = new ManualResetEventSlim(false)
            use secondEntered = new ManualResetEventSlim(false)
            use releaseFirst = new ManualResetEventSlim(false)
            use firstExited = new ManualResetEventSlim(false)
            use releaseSecond = new ManualResetEventSlim(false)
            use completed = new CountdownEvent(2)
            let mutable firstCallbackThread = -1
            let mutable finalCallbackThread = -1

            let onLock color =
                lifecycleGate.Wait()
                Volatile.Write(&firstCallbackThread, Thread.CurrentThread.ManagedThreadId)
                recorder.OnLock color

            let onUnlock color =
                Volatile.Write(&finalCallbackThread, Thread.CurrentThread.ManagedThreadId)
                recorder.OnUnlock color
                lifecycleGate.Release() |> ignore

            let first =
                startWorker "ColoredLock first shared owner" errors completed (fun () ->
                    resourceLock.Enter(ResourceUsage.Render, onLock)
                    try
                        firstEntered.Set()
                        releaseFirst.Wait()
                    finally
                        resourceLock.Exit onUnlock
                        firstExited.Set()
                )

            let second =
                startWorker "ColoredLock final shared owner" errors completed (fun () ->
                    waitEvent "first shared owner" firstEntered
                    resourceLock.Enter(ResourceUsage.Render, onLock)
                    try
                        secondEntered.Set()
                        releaseSecond.Wait()
                    finally
                        resourceLock.Exit onUnlock
                )

            try
                waitEvent "second shared owner" secondEntered
                releaseFirst.Set()
                waitEvent "first shared owner exit" firstExited
                releaseSecond.Set()
                waitCountdown "cross-thread lifecycle workers" completed
                joinThread "first shared owner" first
                joinThread "final shared owner" second
            finally
                releaseFirst.Set()
                releaseSecond.Set()
                completed.Wait timeout |> ignore
                first.Join timeout |> ignore
                second.Join timeout |> ignore

            assertNoErrors errors
            Expect.notEqual (Volatile.Read &firstCallbackThread) (Volatile.Read &finalCallbackThread) "Lifecycle callbacks unexpectedly used one thread"
            Expect.equal lifecycleGate.CurrentCount 1 "Cross-thread lifecycle gate was not released"
            Expect.equal resourceLock.Status NotEntered "Cross-thread lifecycle ownership leaked"
            assertRecorder recorder [ Locked (Some ResourceUsage.Render); Unlocked (Some ResourceUsage.Render) ]

        let failedGenerationWithJoiner() =
            let coloredLock = ColoredLock<int>()
            let errors = ConcurrentQueue<exn>()
            use callbackEntered = new ManualResetEventSlim(false)
            use joinerEntered = new ManualResetEventSlim(false)
            use allowFailure = new ManualResetEventSlim(false)
            use failureObserved = new ManualResetEventSlim(false)
            use releaseJoiner = new ManualResetEventSlim(false)
            use lateAttempting = new ManualResetEventSlim(false)
            use lateEntered = new ManualResetEventSlim(false)
            use releaseLate = new ManualResetEventSlim(false)
            use completed = new CountdownEvent(3)
            let mutable lockCalls = 0
            let mutable unlockCalls = 0

            let initializer =
                startWorker "ColoredLock failing initializer" errors completed (fun () ->
                    try
                        coloredLock.Enter(1, fun _ ->
                            Interlocked.Increment &lockCalls |> ignore
                            callbackEntered.Set()
                            allowFailure.Wait()
                            raise (InvalidOperationException "initialization failed")
                        )
                        failtest "Failing OnLock unexpectedly returned"
                    with :? InvalidOperationException as e when e.Message = "initialization failed" ->
                        failureObserved.Set()

                    Expect.equal coloredLock.Status NotEntered "Failed initializer retained ownership"
                )

            let joiner =
                startWorker "ColoredLock initialization joiner" errors completed (fun () ->
                    waitEvent "initialization callback" callbackEntered
                    coloredLock.Enter(1, fun _ -> Interlocked.Increment &lockCalls |> ignore)
                    try
                        joinerEntered.Set()
                        releaseJoiner.Wait()
                    finally
                        coloredLock.Exit(fun _ -> Interlocked.Increment &unlockCalls |> ignore)

                    Expect.equal coloredLock.Status NotEntered "Failed-generation joiner retained ownership"
                )

            let late =
                startWorker "ColoredLock post-failure entrant" errors completed (fun () ->
                    waitEvent "initialization failure" failureObserved
                    lateAttempting.Set()
                    coloredLock.Enter(1, fun _ -> Interlocked.Increment &lockCalls |> ignore)
                    try
                        lateEntered.Set()
                        releaseLate.Wait()
                    finally
                        coloredLock.Exit(fun _ -> Interlocked.Increment &unlockCalls |> ignore)

                    Expect.equal coloredLock.Status NotEntered "Post-failure entrant retained ownership"
                )

            try
                waitEvent "same-color initialization joiner" joinerEntered
                allowFailure.Set()
                waitEvent "initialization failure" failureObserved
                waitEvent "post-failure entry attempt" lateAttempting
                let waiting = SpinWait.SpinUntil((fun () -> late.ThreadState.HasFlag ThreadState.WaitSleepJoin), timeout)
                Expect.isTrue waiting "The post-failure entrant did not block"
                Expect.isFalse (lateEntered.Wait 100) "A new owner joined a failed generation"
                releaseJoiner.Set()
                waitEvent "fresh post-failure generation" lateEntered
                releaseLate.Set()
                waitCountdown "failed-generation workers" completed
                joinThread "failing initializer" initializer
                joinThread "initialization joiner" joiner
                joinThread "post-failure entrant" late
            finally
                allowFailure.Set()
                releaseJoiner.Set()
                releaseLate.Set()
                completed.Wait timeout |> ignore
                initializer.Join timeout |> ignore
                joiner.Join timeout |> ignore
                late.Join timeout |> ignore

            assertNoErrors errors
            Expect.equal (Volatile.Read &lockCalls) 2 "The failed generation did not retry exactly once after draining"
            Expect.equal (Volatile.Read &unlockCalls) 1 "The failed and replacement generations were not balanced"

            coloredLock.Enter(1, fun _ -> Interlocked.Increment &lockCalls |> ignore)
            coloredLock.Exit(fun _ -> Interlocked.Increment &unlockCalls |> ignore)
            Expect.equal coloredLock.Status NotEntered "Lock was not reusable after a failed generation"
            Expect.equal (Volatile.Read &lockCalls) 3 "A later fresh generation did not invoke OnLock"
            Expect.equal (Volatile.Read &unlockCalls) 2 "The later fresh generation did not balance OnUnlock"

        let sharedOuterSameInnerRound() =
            let coloredLock = ColoredLock<int>()
            let recorder = CallbackRecorder<int>()
            let errors = ConcurrentQueue<exn>()
            use outerReady = new CountdownEvent(2)
            use startInner = new ManualResetEventSlim(false)
            use innerReady = new CountdownEvent(2)
            use releaseInner = new ManualResetEventSlim(false)
            use completed = new CountdownEvent(2)
            let mutable innerCount = 0
            let mutable maximumInnerCount = 0

            let action() =
                coloredLock.Enter(0, recorder.OnLock)
                try
                    Expect.equal coloredLock.Status (Colored 0) "Shared outer ownership is missing"
                    outerReady.Signal() |> ignore
                    startInner.Wait()

                    coloredLock.Enter(1, recorder.OnLock)
                    try
                        Expect.equal coloredLock.Status (Colored 1) "Shared inner ownership is missing"
                        let current = Interlocked.Increment &innerCount
                        updateMaximum &maximumInnerCount current
                        innerReady.Signal() |> ignore
                        releaseInner.Wait()
                    finally
                        Interlocked.Decrement &innerCount |> ignore
                        coloredLock.Exit recorder.OnUnlock

                    Expect.equal coloredLock.Status (Colored 0) "Shared outer ownership was not restored"
                finally
                    coloredLock.Exit recorder.OnUnlock

                Expect.equal coloredLock.Status NotEntered "Shared ownership leaked"

            let first = startWorker "ColoredLock same inner 1" errors completed action
            let second = startWorker "ColoredLock same inner 2" errors completed action

            let mutable bothEntered = false
            try
                waitCountdown "shared outer ownership" outerReady
                startInner.Set()
                bothEntered <- innerReady.Wait (TimeSpan.FromSeconds 10.0)
                releaseInner.Set()
                waitCountdown "same-inner workers" completed
                joinThread "same-inner worker 1" first
                joinThread "same-inner worker 2" second
            finally
                startInner.Set()
                releaseInner.Set()
                completed.Wait timeout |> ignore
                first.Join timeout |> ignore
                second.Join timeout |> ignore

            Expect.isTrue bothEntered "Same-color transition did not wake the eligible waiter"
            Expect.equal maximumInnerCount 2 "Same-color inner actions did not overlap"
            assertNoErrors errors
            assertRecorder recorder [ Locked (Some 0); Locked (Some 1); Unlocked (Some 1); Unlocked (Some 0) ]

        let sharedOuterSameInner() =
            for _ in 1 .. 8 do sharedOuterSameInnerRound()

        let sharedOuterDifferentInnerRound() =
            let coloredLock = ColoredLock<int>()
            let recorder = CallbackRecorder<int>()
            let errors = ConcurrentQueue<exn>()
            use outerReady = new CountdownEvent(2)
            use startInner = new ManualResetEventSlim(false)
            use completed = new CountdownEvent(2)
            let entered = [| new ManualResetEventSlim(false); new ManualResetEventSlim(false) |]
            let release = [| new ManualResetEventSlim(false); new ManualResetEventSlim(false) |]
            let mutable innerCount = 0
            let mutable maximumInnerCount = 0

            let action index =
                let innerColor = index + 1
                coloredLock.Enter(0, recorder.OnLock)
                try
                    Expect.equal coloredLock.Status (Colored 0) "Shared outer ownership is missing"
                    outerReady.Signal() |> ignore
                    startInner.Wait()

                    coloredLock.Enter(innerColor, recorder.OnLock)
                    try
                        Expect.equal coloredLock.Status (Colored innerColor) "Different inner ownership is missing"
                        let current = Interlocked.Increment &innerCount
                        updateMaximum &maximumInnerCount current
                        entered.[index].Set()
                        release.[index].Wait()
                    finally
                        Interlocked.Decrement &innerCount |> ignore
                        coloredLock.Exit recorder.OnUnlock

                    Expect.equal coloredLock.Status (Colored 0) "Shared outer ownership was not restored"
                finally
                    coloredLock.Exit recorder.OnUnlock

                Expect.equal coloredLock.Status NotEntered "Different-inner ownership leaked"

            let first = startWorker "ColoredLock different inner 1" errors completed (fun () -> action 0)
            let second = startWorker "ColoredLock different inner 2" errors completed (fun () -> action 1)

            let mutable firstIndex = -1
            let mutable secondIndex = -1
            try
                waitCountdown "shared outer ownership" outerReady
                startInner.Set()

                let handles = entered |> Array.map (fun value -> value.WaitHandle)
                firstIndex <- WaitHandle.WaitAny(handles, TimeSpan.FromSeconds 10.0)
                Expect.isLessThan firstIndex 2 "Neither different inner color became active"
                secondIndex <- 1 - firstIndex
                Expect.isFalse entered.[secondIndex].IsSet "Different inner colors overlapped"

                release.[firstIndex].Set()
                waitEvent "second different inner color" entered.[secondIndex]
                release.[secondIndex].Set()

                waitCountdown "different-inner workers" completed
                joinThread "different-inner worker 1" first
                joinThread "different-inner worker 2" second
            finally
                startInner.Set()
                release |> Array.iter (fun value -> value.Set())
                completed.Wait timeout |> ignore
                first.Join timeout |> ignore
                second.Join timeout |> ignore
                entered |> Array.iter (fun value -> value.Dispose())
                release |> Array.iter (fun value -> value.Dispose())

            Expect.equal maximumInnerCount 1 "Different inner actions overlapped"
            assertNoErrors errors

            let firstColor = firstIndex + 1
            let secondColor = secondIndex + 1
            assertRecorder recorder [
                Locked (Some 0)
                Locked (Some firstColor)
                Unlocked (Some firstColor)
                Locked (Some secondColor)
                Unlocked (Some secondColor)
                Unlocked (Some 0)
            ]

        let sharedOuterDifferentInner() =
            for _ in 1 .. 8 do sharedOuterDifferentInnerRound()

        let interruptedRestoration() =
            let coloredLock = ColoredLock<int>()
            let recorder = CallbackRecorder<int>()
            let errors = ConcurrentQueue<exn>()
            use innerActive = new ManualResetEventSlim(false)
            use sharedInner = new ManualResetEventSlim(false)
            use beginRestore = new ManualResetEventSlim(false)
            use releaseInner = new ManualResetEventSlim(false)
            use interruptionObserved = new ManualResetEventSlim(false)
            use completed = new CountdownEvent(2)

            let first =
                startWorker "ColoredLock interrupted restoration" errors completed (fun () ->
                    coloredLock.Enter(0, recorder.OnLock)
                    try
                        coloredLock.Enter(1, recorder.OnLock)
                        innerActive.Set()
                        waitEvent "shared inner ownership" sharedInner
                        beginRestore.Set()

                        try
                            coloredLock.Exit recorder.OnUnlock
                            failtest "Interrupted restoration did not throw"
                        with :? ThreadInterruptedException ->
                            interruptionObserved.Set()

                        Expect.equal coloredLock.Status (Colored 0) "Interrupted restoration lost outer ownership"
                    finally
                        if coloredLock.Status <> NotEntered then
                            coloredLock.Exit recorder.OnUnlock

                    Expect.equal coloredLock.Status NotEntered "Interrupted restoration leaked ownership"
                )

            let second =
                startWorker "ColoredLock restoration blocker" errors completed (fun () ->
                    waitEvent "active inner ownership" innerActive
                    coloredLock.Enter(1, recorder.OnLock)
                    try
                        sharedInner.Set()
                        releaseInner.Wait()
                    finally
                        coloredLock.Exit recorder.OnUnlock
                )

            try
                waitEvent "restoration start" beginRestore
                let waiting = SpinWait.SpinUntil((fun () -> first.ThreadState.HasFlag ThreadState.WaitSleepJoin), timeout)
                Expect.isTrue waiting "The restoring thread did not block"
                first.Interrupt()
                releaseInner.Set()

                waitCountdown "interruption workers" completed
                joinThread "interrupted restoration worker" first
                joinThread "restoration blocker" second
            finally
                releaseInner.Set()
                completed.Wait timeout |> ignore
                first.Join timeout |> ignore
                second.Join timeout |> ignore

            waitEvent "observed restoration interruption" interruptionObserved
            assertNoErrors errors
            assertRecorder recorder [
                Locked (Some 0)
                Locked (Some 1)
                Unlocked (Some 1)
                Unlocked (Some 0)
            ]

    [<Tests>]
    let tests =
        testList "Utilities.ColoredLock" [
            testCase "Single colored acquisition balances callbacks" Cases.singleColored
            testCase "Same-color nesting coalesces callbacks" Cases.sameColorNesting
            testCase "Different-color nesting restores outer ownership" Cases.differentColorNesting
            testCase "Structurally equal colors share logical ownership" Cases.structuralColorNesting
            testCase "Non-reflexive colors restore by identity" Cases.nonReflexiveColorNesting
            testCase "Null color nesting restores outer ownership" Cases.nullColorNesting
            testCase "Colored and exclusive nesting restores ownership" Cases.coloredExclusiveNesting
            testCase "Exclusive nesting remains exclusive" Cases.exclusiveNesting
            testCase "Protected action exceptions unwind ownership" Cases.protectedActionExceptions
            testCase "Callback exceptions leave the lock reusable" Cases.callbackExceptions
            testCase "Colored callbacks preserve cross-lock compatibility" Cases.crossLockCallbacks
            testCase "First and final callbacks may use different threads" Cases.crossThreadFinalCallback
            testCase "Failed generations drain and retry" Cases.failedGenerationWithJoiner
            testCase "Shared holders switch to the same inner color" Cases.sharedOuterSameInner
            testCase "Shared holders switch to different inner colors" Cases.sharedOuterDifferentInner
            testCase "Interrupted restoration preserves outer ownership" Cases.interruptedRestoration
        ]
        |> testSequenced
