namespace Aardvark.Rendering.Tests.Utilities

open System
open System.Runtime.CompilerServices
open System.Threading
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.Data.Traceable
open Expecto

module RenderTaskLockTests =

    module private Cases =

        type private Event = Enter of int * ResourceUsage option | Exit of int * ResourceUsage option

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private throwAcquire (error : exn) = raise error
        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private throwRelease (error : exn) = raise error
        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private throwAction (error : exn) = raise error

        type private Recorder() =
            let gate = obj()
            let events = ResizeArray<Event>()
            member _.Add event = lock gate (fun () -> events.Add event)
            member _.Events = lock gate (fun () -> events |> Seq.toList)
            member _.Clear() = lock gate events.Clear

        type private Resource(id : int, recorder : Recorder) =
            let resourceLock = ResourceLock()
            let mutable balances = Map.empty<ResourceUsage option, int>
            member _.Id = id
            member _.Lock = resourceLock
            member _.Balanced = balances |> Map.forall (fun _ count -> count = 0)
            member val AcquireError : exn option = None with get, set
            member val ReleaseError : exn option = None with get, set
            member val AcquireHook : unit -> unit = ignore with get, set
            member val ReleaseHook : unit -> unit = ignore with get, set
            member x.Clear() =
                x.AcquireError <- None; x.ReleaseError <- None
                x.AcquireHook <- ignore; x.ReleaseHook <- ignore
            override _.GetHashCode() = id
            override x.Equals other = obj.ReferenceEquals(x, other)
            interface ILockedResource with
                member _.Lock = resourceLock
                member x.OnLock color =
                    recorder.Add(Enter(id, color))
                    match color, x.AcquireError with
                    | Some ResourceUsage.Render, Some error -> throwAcquire error
                    | _ -> ()
                    let count = Map.tryFind color balances |> Option.defaultValue 0
                    Expect.equal count 0 (sprintf "resource=%d, color=%A: callback starts once" id color)
                    balances <- Map.add color 1 balances
                    x.AcquireHook()
                member x.OnUnlock color =
                    recorder.Add(Exit(id, color))
                    Expect.equal (Map.tryFind color balances) (Some 1) (sprintf "resource=%d, color=%A: callback ends once" id color)
                    balances <- Map.add color 0 balances
                    x.ReleaseHook()
                    match color, x.ReleaseError with
                    | Some ResourceUsage.Render, Some error -> throwRelease error
                    | _ -> ()

        type private Fixture(count : int) =
            let batch = RenderTaskLock()
            let recorder = Recorder()
            let all = Array.init count (fun index -> Resource(index + 1, recorder))
            let mutable membership = CountingHashSet.empty<ILockedResource>
            do for resource in all do
                batch.Add resource
                membership <- membership.Add(resource :> ILockedResource)
            member _.Batch = batch
            member _.Recorder = recorder
            member _.All = all
            member _.Order = CountingHashSet.toArray membership |> Array.map (fun resource -> resource :?> Resource)
            member _.Add(resource : Resource) = batch.Add resource; membership <- membership.Add(resource :> ILockedResource)
            member _.Remove(resource : Resource) = batch.Remove resource; membership <- membership.Remove(resource :> ILockedResource)
            member _.Extra id = Resource(id, recorder)
            member _.Clear() = for resource in all do resource.Clear()
            member _.UnwindOwned() =
                for resource in all do
                    resource.Clear()
                    let handle = resource :> ILockedResource
                    while resource.Lock.Status <> NotEntered do resource.Lock.Exit(handle.OnUnlock)
            interface IDisposable with member x.Dispose() = x.UnwindOwned()

        let private timeout = TimeSpan.FromSeconds 2.0

        let private renderEvents order =
            [for resource : Resource in order do yield Enter(resource.Id, Some ResourceUsage.Render)] @
            [for resource : Resource in order do yield Exit(resource.Id, Some ResourceUsage.Render)]

        let private checkFree context (resources : Resource[]) =
            for resource in resources do
                Expect.equal resource.Lock.Status NotEntered (sprintf "%s, resource=%d: no ownership leaked" context resource.Id)
                Expect.isTrue resource.Balanced (sprintf "%s, resource=%d: callback balance" context resource.Id)

        let private expectErrors context (expected : exn[]) action =
            let mutable failure = None
            try action() with error -> failure <- Some error
            Expect.isSome failure (context + ": expected failure")
            let actual = failure.Value
            if expected.Length = 1 then
                Expect.isTrue (obj.ReferenceEquals(actual, expected.[0])) (context + ": single exception identity")
            else
                Expect.isTrue (actual :? AggregateException) (context + ": multiple failures are aggregated")
                let errors = (actual :?> AggregateException).InnerExceptions
                Expect.equal errors.Count expected.Length (context + ": every failure is retained")
                for index = 0 to expected.Length - 1 do
                    Expect.isTrue (obj.ReferenceEquals(errors.[index], expected.[index])) (sprintf "%s, index=%d: failure order and identity" context index)
            actual

        let private reuse context (fixture : Fixture) =
            fixture.Clear()
            use ready = new ManualResetEventSlim(false)
            let mutable error = None
            let worker = Thread(ThreadStart(fun () ->
                try
                    ready.Set()
                    for resource in fixture.All do
                        let handle = resource :> ILockedResource
                        resource.Lock.Enter(ResourceUsage.Access, handle.OnLock)
                        try () finally resource.Lock.Exit(handle.OnUnlock)
                with failure -> error <- Some failure
            ), IsBackground = true)
            worker.Start()
            try
                Expect.isTrue (ready.Wait timeout) (context + ": reuse worker ready")
                Expect.isTrue (worker.Join timeout) (context + ": cross-thread reuse after unwinding")
                Expect.isNone error (context + ": reuse worker succeeds")
            finally
                // Original-source controls may leave ownership on this thread; release it before joining.
                fixture.UnwindOwned()
                if not (worker.Join timeout) then
                    worker.Interrupt()
                    Expect.isTrue (worker.Join timeout) (context + ": reuse worker cleanup")

        let successfulCountsAndValues () =
            for count in [0; 1; 4; 7] do
                for duplicates in [1; 2; 3] do
                    use fixture = new Fixture(count)
                    for resource in fixture.All do
                        for _ = 2 to duplicates do fixture.Add resource
                    let order = fixture.Order
                    let context = sprintf "count=%d, duplicates=%d" count duplicates
                    let marker = obj()
                    let result = fixture.Batch.Run(fun () ->
                        for resource in fixture.All do Expect.equal resource.Lock.Status (Colored ResourceUsage.Render) (context + ": action ownership")
                        marker
                    )
                    Expect.isTrue (obj.ReferenceEquals(result, marker)) (context + ": reference result")
                    Expect.equal fixture.Recorder.Events (renderEvents order) (context + ": unique snapshot acquisition/release order")
                    checkFree context fixture.All
                    for resource in fixture.All do
                        for _ = 2 to duplicates do fixture.Remove resource
                    fixture.Recorder.Clear()
                    Expect.equal (fixture.Batch.Run(fun () -> struct (42, "value"))) (struct (42, "value")) (context + ": struct return value")
                    Expect.equal fixture.Recorder.Events (renderEvents order) (context + ": remaining counted membership")
                    for resource in fixture.All do fixture.Remove resource
                    fixture.Recorder.Clear()
                    let result : string = fixture.Batch.Run(fun () -> null)
                    Expect.isNull result (context + ": null return value")
                    Expect.isEmpty fixture.Recorder.Events (context + ": removing all counts empties the snapshot")

        let acquisitionFailures () =
            for count in [1; 2; 4; 7] do
                for position = 0 to count - 1 do
                    use fixture = new Fixture(count)
                    let order = fixture.Order
                    let expected = InvalidOperationException "acquisition"
                    order.[position].AcquireError <- Some expected
                    let mutable calls = 0
                    let context = sprintf "count=%d, acquisition=%d" count position
                    let actual = expectErrors context [|expected|] (fun () -> fixture.Batch.Run(fun () -> calls <- calls + 1) |> ignore)
                    Expect.stringContains actual.StackTrace "throwAcquire" (context + ": original acquisition stack")
                    Expect.equal calls 0 (context + ": action never runs after failed acquisition")
                    let events =
                        [for resource in order.[0 .. position] do yield Enter(resource.Id, Some ResourceUsage.Render)] @
                        [for resource in Array.take position order do yield Exit(resource.Id, Some ResourceUsage.Render)]
                    Expect.equal fixture.Recorder.Events events (context + ": only the successful prefix is released")
                    checkFree context fixture.All
                    reuse context fixture

        let releaseFailures () =
            for count in [1; 2; 4; 7] do
                for position = 0 to count - 1 do
                    use fixture = new Fixture(count)
                    let order = fixture.Order
                    let expected = InvalidOperationException "release"
                    order.[position].ReleaseError <- Some expected
                    let context = sprintf "count=%d, release=%d" count position
                    let mutable calls = 0
                    let actual = expectErrors context [|expected|] (fun () -> fixture.Batch.Run(fun () -> calls <- calls + 1; 42) |> ignore)
                    Expect.stringContains actual.StackTrace "throwRelease" (context + ": original release stack")
                    Expect.equal calls 1 (context + ": action runs once")
                    Expect.equal fixture.Recorder.Events (renderEvents order) (context + ": all exits are attempted in order")
                    checkFree context fixture.All
                    reuse context fixture

        let combinedFailures () =
            for count in [4; 7] do
                for phase in ["acquisition"; "action"; "cleanup"] do
                    for cleanupPositions in [[0]; [0; 1]; [0; count - 2]; [0; 1; count - 2]] do
                        use fixture = new Fixture(count)
                        let order = fixture.Order
                        let primary = AggregateException(InvalidOperationException "original nested error")
                        let acquired = if phase = "acquisition" then count - 1 else count
                        if phase = "acquisition" then order.[count - 1].AcquireError <- Some primary
                        let cleanup =
                            cleanupPositions |> List.distinct |> List.sort |> List.map (fun position ->
                                let error = ArgumentException(sprintf "cleanup=%d" position)
                                order.[position].ReleaseError <- Some error
                                error :> exn
                            )
                        let expected = Array.ofList ((if phase = "cleanup" then [] else [primary :> exn]) @ cleanup)
                        let context = sprintf "count=%d, phase=%s, cleanup=%A" count phase cleanupPositions
                        let mutable calls = 0
                        expectErrors context expected (fun () -> fixture.Batch.Run(fun () ->
                            calls <- calls + 1
                            if phase = "action" then throwAction primary
                        )) |> ignore
                        Expect.equal calls (if phase = "acquisition" then 0 else 1) (context + ": action count")
                        let enterCount = if phase = "acquisition" then acquired + 1 else acquired
                        let events =
                            [for resource in Array.take enterCount order do yield Enter(resource.Id, Some ResourceUsage.Render)] @
                            [for resource in Array.take acquired order do yield Exit(resource.Id, Some ResourceUsage.Render)]
                        Expect.equal fixture.Recorder.Events events (context + ": full successful-prefix unwind")
                        for error in cleanup do Expect.stringContains error.StackTrace "throwRelease" (context + ": cleanup stack retained")
                        checkFree context fixture.All
                        reuse context fixture
            use empty = new Fixture(0)
            let expected = InvalidOperationException "empty action"
            let error = expectErrors "empty batch" [|expected|] (fun () -> empty.Batch.Run(fun () -> throwAction expected))
            Expect.stringContains error.StackTrace "throwAction" "Single action failure retains its original stack"

        let snapshotMembership () =
            for phase in ["acquisition"; "action"; "release"] do
                use fixture = new Fixture(4)
                let order = fixture.Order
                let extra = fixture.Extra 99
                let mutate() = fixture.Remove order.[order.Length - 1]; fixture.Add extra
                if phase = "acquisition" then order.[0].AcquireHook <- mutate
                if phase = "release" then order.[0].ReleaseHook <- mutate
                try
                    fixture.Batch.Run(fun () -> if phase = "action" then mutate())
                    Expect.equal fixture.Recorder.Events (renderEvents order) (phase + ": mutations cannot alter the active snapshot")
                    fixture.Clear()
                    fixture.Recorder.Clear()
                    let next = fixture.Order
                    fixture.Batch.Run ignore
                    Expect.equal fixture.Recorder.Events (renderEvents next) (phase + ": next Run sees the updated membership")
                    checkFree phase (Array.append fixture.All [|extra|])
                finally
                    extra.Clear()
                    let handle = extra :> ILockedResource
                    while extra.Lock.Status <> NotEntered do extra.Lock.Exit(handle.OnUnlock)

        let nestedOwnership () =
            for mode in [Some ResourceUsage.Render; Some ResourceUsage.Access; None] do
                for failAction in [false; true] do
                    use fixture = new Fixture(3)
                    let handles = fixture.All |> Array.map (fun resource -> resource :> ILockedResource)
                    for handle in handles do
                        match mode with Some color -> handle.Lock.Enter(color, handle.OnLock) | None -> handle.Lock.Enter(handle.OnLock)
                    let outer = match mode with Some color -> Colored color | None -> Exclusive
                    let expected = InvalidOperationException "nested action"
                    let action() = fixture.Batch.Run(fun () ->
                        fixture.Batch.Run(fun () ->
                            for handle in handles do
                                Expect.equal handle.Lock.Status (if mode.IsNone then Exclusive else Colored ResourceUsage.Render) "Nested Run ownership"
                            if failAction then throwAction expected
                            17
                        )
                    )
                    if failAction then expectErrors "nested action" [|expected|] (fun () -> action() |> ignore) |> ignore
                    else Expect.equal (action()) 17 "Nested return value"
                    for handle in handles do Expect.equal handle.Lock.Status outer "Nested Run restores outer ownership"
                    for handle in handles do handle.Lock.Exit(handle.OnUnlock)
                    checkFree "nested success/action failure" fixture.All
                    reuse "nested success/action failure" fixture
            for phase in ["acquisition"; "release"] do
                use fixture = new Fixture(3)
                let order = fixture.Order
                for resource in fixture.All do
                    let handle = resource :> ILockedResource
                    handle.Lock.Enter(ResourceUsage.Access, handle.OnLock)
                let expected = InvalidOperationException phase
                if phase = "acquisition" then order.[1].AcquireError <- Some expected
                else order.[0].ReleaseError <- Some expected
                expectErrors phase [|expected|] (fun () -> fixture.Batch.Run ignore) |> ignore
                for resource in fixture.All do Expect.equal resource.Lock.Status (Colored ResourceUsage.Access) (phase + ": failed Run restores outer access")
                fixture.Clear()
                for resource in fixture.All do resource.Lock.Exit((resource :> ILockedResource).OnUnlock)
                checkFree phase fixture.All
                reuse phase fixture

        let crossThreadReuse () =
            for phase in ["acquisition"; "release"] do
                use fixture = new Fixture(4)
                let order = fixture.Order
                let expected = InvalidOperationException phase
                if phase = "acquisition" then order.[1].AcquireError <- Some expected
                else order.[0].ReleaseError <- Some expected
                expectErrors phase [|expected|] (fun () -> fixture.Batch.Run ignore) |> ignore
                reuse phase fixture
                checkFree phase fixture.All

    let tests (target : TestTarget) =
        [
            "Successful order counts and return values",     Cases.successfulCountsAndValues
            "Acquisition failures unwind successful prefix", Cases.acquisitionFailures
            "Release failures unwind the full batch",        Cases.releaseFailures
            "Combined failures retain ordered identities",   Cases.combinedFailures
            "Membership mutations preserve the snapshot",    Cases.snapshotMembership
            "Nested ownership restoration",                  Cases.nestedOwnership
            "Cross-thread reuse after callback failures",    Cases.crossThreadReuse
        ]
        |> prepareCasesCpu "RenderTaskLock" target
