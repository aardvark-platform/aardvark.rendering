namespace Aardvark.Rendering.Tests.Raytracing

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto

module AdaptiveAccelerationStructure =

    module private Cases =

        type private Handle(geometry : TraceGeometry, usage : AccelerationStructureUsage, onDispose : unit -> unit) =
            let mutable data = geometry
            let mutable disposed = false
            let mutable name = null
            let mutable names = 0
            let mutable disposals = 0
            let mutable retiredUpdates = 0
            let mutable retiredNames = 0
            let mutable retiredDisposals = 0

            member _.Geometry = data
            member _.IsDisposed = disposed
            member _.Names = names
            member _.Disposals = disposals
            member _.RetiredAccess = struct (retiredUpdates, retiredNames, retiredDisposals)
            member _.TryUpdate(geometry : TraceGeometry, succeeds : bool) =
                if disposed then retiredUpdates <- retiredUpdates + 1
                if succeeds then data <- geometry
                succeeds

            interface IAccelerationStructure with
                member _.Usage = usage
                member _.GeometryCount = data.Count
                member _.SizeInBytes = uint64 data.Count * 64UL
                member _.Name
                    with get() = name
                    and set value =
                        names <- names + 1
                        if disposed then retiredNames <- retiredNames + 1
                        name <- value
                member _.Dispose() =
                    disposals <- disposals + 1
                    if disposed then retiredDisposals <- retiredDisposals + 1
                    onDispose()
                    disposed <- true

        type private Runtime() =
            let requests = ResizeArray<struct (TraceGeometry * AccelerationStructureUsage)>()
            let updates = ResizeArray<struct (IAccelerationStructure * TraceGeometry)>()
            let handles = ResizeArray<Handle>()

            member val CreationError : exn option = None with get, set
            member val DisposalError : exn option = None with get, set
            member val AllowUpdate = false with get, set
            member val UpdateIdentical = false with get, set
            member _.Requests = requests
            member _.Updates = updates
            member _.Handles = handles

            interface IAccelerationStructureRuntime with
                member this.CreateAccelerationStructure(geometry, usage) =
                    requests.Add(struct (geometry, usage))
                    Expect.isTrue (handles |> Seq.forall _.IsDisposed) "creation follows successful retirement"
                    match this.CreationError with
                    | Some error -> raise error
                    | None ->
                        let handle =
                            new Handle(geometry, usage, fun () ->
                                match this.DisposalError with
                                | Some error -> raise error
                                | None -> ())
                        handles.Add handle
                        handle :> IAccelerationStructure
                member this.TryUpdateAccelerationStructure(handle, geometry) =
                    updates.Add(struct (handle, geometry))
                    let current = handle :?> Handle
                    let succeeds = this.AllowUpdate || (this.UpdateIdentical && Object.ReferenceEquals(current.Geometry, geometry))
                    current.TryUpdate(geometry, succeeds)
                member _.PrepareMicromap _ = failwith "Unexpected micromap preparation"

        let private geometry isAabb count =
            if isAabb then
                Array.init count (fun i ->
                    BoundingBoxes([| Box3f(V3f(float32 i), V3f(float32 i + 1.0f)) |], flags = GeometryFlags.Opaque))
                |> TraceGeometry.AABBs
            else
                Array.init count (fun i ->
                    let vertices = [| V3f(float32 i, 0.0f, 0.0f); V3f(float32 i, 1.0f, 0.0f); V3f(float32 i, 0.0f, 1.0f) |]
                    TriangleMesh(vertices, [| 0; 1; 2 |], Trafo3d.Identity, GeometryFlags.IgnoreDuplicateHits))
                |> TraceGeometry.Triangles

        type private Fixture(isAabb : bool, usage : AccelerationStructureUsage, owners : int) =
            let runtime = Runtime()
            let original = geometry isAabb 1
            let changed = geometry isAabb 2
            let third = geometry isAabb 3
            let input = cval original
            let token = RenderToken.Zero
            let resource = (runtime :> IAccelerationStructureRuntime).CreateAccelerationStructure(input, usage)
            do for _ in 1 .. owners do resource.Acquire()

            member _.Runtime = runtime
            member _.Resource = resource
            member _.Token = token
            member _.Owners = owners
            member _.Usage = usage
            member _.Input = input :> aval<TraceGeometry>
            member _.Data = input.Value
            member _.Get() = resource.GetValue(AdaptiveToken.Top, token)
            member _.Change() = transact (fun () -> input.Value <- changed)
            member _.Restore() = transact (fun () -> input.Value <- original)
            member _.Third() = transact (fun () -> input.Value <- third)
            member _.Invalidate() = transact resource.MarkOutdated

            interface IDisposable with
                member _.Dispose() =
                    runtime.CreationError <- None
                    runtime.DisposalError <- None
                    resource.ReleaseAll()

        let private forFixtures action =
            for isAabb in [false; true] do
                for usage in [AccelerationStructureUsage.None; AccelerationStructureUsage.Static;
                              AccelerationStructureUsage.Dynamic ||| AccelerationStructureUsage.Update;
                              AccelerationStructureUsage.Static ||| AccelerationStructureUsage.Update ||| AccelerationStructureUsage.Compact] do
                    for owners in [1; 2; 3] do
                        let context = $"AABBs={isAabb}, usage={usage}, owners={owners}"
                        use fixture = new Fixture(isAabb, usage, owners)
                        action context fixture

        let private expectFailure context (expected : exn) action =
            let actual = try action(); None with error -> Some error
            match actual with
            | Some error -> Expect.isTrue (Object.ReferenceEquals(error, expected)) $"{context}: exception identity ({error})"
            | None -> failtestf "%s: expected injected creation/disposal failure" context

        let private checkLive context (fixture : Fixture) (handle : IAccelerationStructure) =
            let current = handle :?> Handle
            Expect.isFalse current.IsDisposed $"{context}: returned a retired handle"
            Expect.isTrue (Object.ReferenceEquals(current.Geometry, fixture.Data)) $"{context}: geometry forwarding"
            Expect.equal handle.GeometryCount fixture.Data.Count $"{context}: geometry count"
            Expect.equal fixture.Resource.Usage fixture.Usage $"{context}: usage property"
            Expect.isTrue (Object.ReferenceEquals(fixture.Resource.Geometry, fixture.Input)) $"{context}: geometry property"
            Expect.equal handle.Usage fixture.Usage $"{context}: usage forwarding"
            Expect.equal handle.Name fixture.Resource.Name $"{context}: name forwarding"
            current

        let private checkRequests context (fixture : Fixture) =
            let struct (data, usage) = fixture.Runtime.Requests.[fixture.Runtime.Requests.Count - 1]
            Expect.isTrue (Object.ReferenceEquals(data, fixture.Data)) $"{context}: creation receives exact geometry"
            Expect.equal usage fixture.Usage $"{context}: creation receives exact usage"

        let private checkRetiredAccess context (fixture : Fixture) =
            for index in 0 .. fixture.Runtime.Handles.Count - 1 do
                Expect.equal fixture.Runtime.Handles.[index].RetiredAccess (struct (0, 0, 0))
                    $"{context}, handle={index}: retired update/name/disposal calls"

        let private releaseOwners (fixture : Fixture) =
            for _ in 1 .. fixture.Owners do fixture.Resource.Release()

        let cachedUpdatesAndReplacement() =
            forFixtures (fun context fixture ->
                Expect.equal fixture.Runtime.Requests.Count 0 $"{context}: Acquire does not create"
                Expect.isTrue (Object.ReferenceEquals(fixture.Resource.Runtime, fixture.Runtime)) $"{context}: runtime property"
                fixture.Resource.Name <- "initial"
                let first = fixture.Get()
                let previous = checkLive context fixture first
                checkRequests context fixture
                for read in 1 .. 8 do
                    Expect.isTrue (Object.ReferenceEquals(first, fixture.Get())) $"{context}, read={read}: cached identity"
                Expect.equal fixture.Runtime.Updates.Count 0 $"{context}: cached reads do not update"
                Expect.equal fixture.Runtime.Requests.Count 1 $"{context}: cached reads do not create"

                fixture.Runtime.AllowUpdate <- true
                fixture.Change()
                Expect.isTrue (Object.ReferenceEquals(first, fixture.Get())) $"{context}: in-place identity"
                checkLive context fixture first |> ignore
                let struct (updated, data) = fixture.Runtime.Updates.[0]
                Expect.isTrue (Object.ReferenceEquals(updated, first) && Object.ReferenceEquals(data, fixture.Data)) $"{context}: update forwarding"
                fixture.Invalidate()
                Expect.isTrue (Object.ReferenceEquals(first, fixture.Get())) $"{context}: unchanged geometry update"
                fixture.Resource.Name <- "replacement"
                Expect.equal first.Name "replacement" $"{context}: live naming"
                fixture.Runtime.AllowUpdate <- false
                fixture.Third()
                let second = fixture.Get()
                let current = checkLive context fixture second
                checkRequests context fixture
                Expect.isFalse (Object.ReferenceEquals(first, second)) $"{context}: replacement identity"
                Expect.equal previous.Disposals 1 $"{context}: old handle retired once"
                Expect.equal fixture.Token.TotalCreatedResources 1 $"{context}: successful initial statistics"
                Expect.equal fixture.Token.TotalInPlaceUpdates 2 $"{context}: successful update statistics"
                Expect.equal fixture.Token.TotalReplacedResources 1 $"{context}: successful replacement statistics"
                Expect.equal fixture.Token.CreatedResources.[ResourceKind.AccelerationStructure] 1 $"{context}: creation kind"
                Expect.equal fixture.Token.InPlaceUpdates.[ResourceKind.AccelerationStructure] 2 $"{context}: update kind"
                Expect.equal fixture.Token.ReplacedResources.[ResourceKind.AccelerationStructure] 1 $"{context}: replacement kind"
                releaseOwners fixture
                Expect.equal current.Disposals 1 $"{context}: final owner retires current handle"
                checkRetiredAccess context fixture)

        let failedReplacementRetries() =
            for failures in [1; 3] do
                forFixtures (fun context fixture ->
                    let context = $"{context}, failures={failures}"
                    fixture.Resource.Name <- "retry"
                    let previous = fixture.Get() |> checkLive context fixture
                    fixture.Change()
                    for attempt in 1 .. failures do
                        let error = InvalidOperationException($"build attempt {attempt}")
                        fixture.Runtime.CreationError <- Some error
                        expectFailure $"{context}, attempt={attempt}" error (fun () -> fixture.Get() |> ignore)
                        Expect.isTrue previous.IsDisposed $"{context}: old handle successfully retired"
                        Expect.equal previous.Disposals 1 $"{context}, attempt={attempt}: one successful retirement"
                        Expect.equal fixture.Runtime.Updates.Count 1 $"{context}, attempt={attempt}: no update of retired handle"
                        checkRequests context fixture
                        checkRetiredAccess context fixture
                    fixture.Runtime.CreationError <- None
                    let current = fixture.Get() |> checkLive context fixture
                    checkRequests context fixture
                    Expect.equal fixture.Runtime.Requests.Count (failures + 2) $"{context}: creation attempts"
                    Expect.equal fixture.Runtime.Handles.Count 2 $"{context}: only successful creations publish handles"
                    releaseOwners fixture
                    Expect.equal current.Disposals 1 $"{context}: recovered handle retired once"
                    checkRetiredAccess context fixture)

        let restoredGeometry() =
            forFixtures (fun context fixture ->
                fixture.Runtime.UpdateIdentical <- true
                let previous = fixture.Get() |> checkLive context fixture
                fixture.Change()
                let error = InvalidOperationException("replacement")
                fixture.Runtime.CreationError <- Some error
                expectFailure context error (fun () -> fixture.Get() |> ignore)
                fixture.Restore()
                fixture.Runtime.CreationError <- None
                let current = fixture.Get() |> checkLive context fixture
                Expect.isFalse (Object.ReferenceEquals(previous, current)) $"{context}: restoring geometry cannot resurrect a retired handle"
                Expect.equal fixture.Runtime.Requests.Count 3 $"{context}: restored geometry receives fresh creation"
                Expect.equal fixture.Runtime.Updates.Count 1 $"{context}: restoration skips the retired handle"
                checkRequests context fixture
                checkRetiredAccess context fixture)

        let namingAfterFailure() =
            forFixtures (fun context fixture ->
                fixture.Resource.Name <- "original"
                let previous = fixture.Get() |> checkLive context fixture
                fixture.Change()
                let error = InvalidOperationException("replacement")
                fixture.Runtime.CreationError <- Some error
                expectFailure context error (fun () -> fixture.Get() |> ignore)
                let names = previous.Names
                for name in [null; "retry"; ""] do
                    fixture.Resource.Name <- name
                    Expect.equal fixture.Resource.Name name $"{context}, name={name}: pending name retained"
                    Expect.equal previous.Names names $"{context}, name={name}: retired handle is not named"
                    checkRetiredAccess context fixture
                Expect.equal fixture.Runtime.Requests.Count 2 $"{context}: naming does not create"
                fixture.Runtime.CreationError <- None
                let handle = fixture.Get()
                checkLive context fixture handle |> ignore
                fixture.Resource.Name <- "live"
                Expect.equal handle.Name "live" $"{context}: recovered live naming"
                checkRetiredAccess context fixture)

        let releaseAndReacquire() =
            for releaseAll in [false; true] do
                forFixtures (fun context fixture ->
                    let context = $"{context}, ReleaseAll={releaseAll}"
                    let previous = fixture.Get() |> checkLive context fixture
                    fixture.Change()
                    let error = InvalidOperationException("replacement")
                    fixture.Runtime.CreationError <- Some error
                    expectFailure context error (fun () -> fixture.Get() |> ignore)
                    if releaseAll then fixture.Resource.ReleaseAll()
                    else releaseOwners fixture
                    fixture.Resource.ReleaseAll()
                    Expect.equal previous.Disposals 1 $"{context}: final release does not revisit retired ownership"
                    Expect.equal fixture.Runtime.Requests.Count 2 $"{context}: release does not create"
                    checkRetiredAccess context fixture
                    fixture.Runtime.CreationError <- None
                    fixture.Resource.Name <- "reacquired"
                    for _ in 1 .. fixture.Owners do fixture.Resource.Acquire()
                    let handle = fixture.Get()
                    let current = checkLive context fixture handle
                    for owner in 1 .. fixture.Owners - 1 do
                        fixture.Resource.Release()
                        Expect.equal current.Disposals 0 $"{context}, remaining={fixture.Owners - owner}: another owner retains handle"
                        Expect.isTrue (Object.ReferenceEquals(handle, fixture.Get())) $"{context}: remaining owner's cached identity"
                    fixture.Resource.Release()
                    Expect.equal current.Disposals 1 $"{context}: reacquired handle retired once"
                    Expect.equal fixture.Runtime.Requests.Count 3 $"{context}: reacquisition creates once"
                    checkRequests context fixture
                    checkRetiredAccess context fixture)

        let initialFailure() =
            for reacquire in [false; true] do
                forFixtures (fun context fixture ->
                    let context = $"{context}, reacquire={reacquire}"
                    for attempt in 1 .. 3 do
                        let error = InvalidOperationException($"initial attempt {attempt}")
                        fixture.Runtime.CreationError <- Some error
                        expectFailure $"{context}, attempt={attempt}" error (fun () -> fixture.Get() |> ignore)
                        Expect.equal fixture.Runtime.Handles.Count 0 $"{context}: no initial publication"
                        Expect.equal fixture.Runtime.Updates.Count 0 $"{context}: no initial update"
                        checkRequests context fixture
                    fixture.Resource.Name <- "initial recovery"
                    if reacquire then
                        fixture.Resource.ReleaseAll()
                        for _ in 1 .. fixture.Owners do fixture.Resource.Acquire()
                    fixture.Runtime.CreationError <- None
                    let current = fixture.Get() |> checkLive context fixture
                    Expect.equal fixture.Runtime.Requests.Count 4 $"{context}: initial retries"
                    releaseOwners fixture
                    Expect.equal current.Disposals 1 $"{context}: initial recovery final release"
                    checkRetiredAccess context fixture)

        let disposalFailure() =
            forFixtures (fun context fixture ->
                let first = fixture.Get()
                let previous = checkLive context fixture first
                fixture.Change()
                let error = InvalidOperationException("retirement")
                fixture.Runtime.DisposalError <- Some error
                expectFailure context error (fun () -> fixture.Get() |> ignore)
                Expect.isFalse previous.IsDisposed $"{context}: disposal failure retains live ownership"
                Expect.equal fixture.Runtime.Requests.Count 1 $"{context}: no creation after disposal failure"
                fixture.Resource.Name <- "still live"
                Expect.equal first.Name "still live" $"{context}: failed disposal retains naming ownership"
                fixture.Runtime.DisposalError <- None
                fixture.Runtime.AllowUpdate <- true
                Expect.isTrue (Object.ReferenceEquals(first, fixture.Get())) $"{context}: retry updates retained ownership"
                checkLive context fixture first |> ignore
                fixture.Runtime.AllowUpdate <- false
                fixture.Third()
                let current = fixture.Get() |> checkLive context fixture
                Expect.equal previous.Disposals 2 $"{context}: unsuccessful disposal followed by successful retirement"
                Expect.equal fixture.Runtime.Requests.Count 2 $"{context}: successful replacement after retained update"
                releaseOwners fixture
                Expect.equal current.Disposals 1 $"{context}: disposal recovery final release"
                checkRetiredAccess context fixture)

    let tests (target : TestTarget) =
        [
            "Cached reads updates and replacement statistics",   Cases.cachedUpdatesAndReplacement
            "Failed rebuild creation permits repeated retries",  Cases.failedReplacementRetries
            "Restored geometry cannot revive retired handles",   Cases.restoredGeometry
            "Naming after failed rebuild retains pending name",  Cases.namingAfterFailure
            "Final release and reacquisition after failure",     Cases.releaseAndReacquire
            "Initial creation failures preserve retry behavior", Cases.initialFailure
            "Failed disposal retains live ownership",            Cases.disposalFailure
        ]
        |> prepareCasesCpu "AdaptiveAccelerationStructure" target
