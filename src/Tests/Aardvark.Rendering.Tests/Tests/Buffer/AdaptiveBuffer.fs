namespace Aardvark.Rendering.Tests.Buffer

open System
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto

module AdaptiveBufferTests =

    module private Cases =

        type private Site = Allocation | Naming | Copy
        type private Event =
            | Allocate of uint64 * BufferUsage * BufferStorage
            | Name of int * string
            | CopyPrefix of int * int * uint64
            | Dispose of int

        type private Buffer(runtime : IBufferRuntime, id : int, size : uint64, events : ResizeArray<Event>, namingError : unit -> exn option) =
            let data = Array.create (int size) 0xCDuy
            let mutable name = null
            member _.Id = id
            member _.Data = data
            member val Disposals = 0 with get, set
            interface IBackendBuffer with
                member _.Runtime = runtime
                member x.Buffer = x :> IBackendBuffer
                member _.Offset = 0UL
                member _.SizeInBytes = size
                member _.Handle = uint64 id
                member x.Name
                    with get() = name
                    and set value =
                        Expect.equal x.Disposals 0 "Naming a disposed buffer"
                        events.Add(Name(id, value))
                        name <- value
                        match namingError() with Some error -> raise error | None -> ()
                member x.Dispose() =
                    events.Add(Dispose id)
                    x.Disposals <- x.Disposals + 1

        let private unexpected() = failwith "Unexpected runtime operation"

        type private Runtime() =
            let events = ResizeArray<Event>()
            let buffers = ResizeArray<Buffer>()
            member val Failure : (Site * exn) option = None with get, set
            member val DefaultCreates = 0 with get, set
            member _.Events = events
            member _.Buffers = buffers
            member x.Error(site) =
                match x.Failure with Some (s, error) when s = site -> Some error | _ -> None
            member x.Allocate(size, usage, storage) =
                events.Add(Allocate(size, usage, storage))
                match x.Error Allocation with
                | Some error -> raise error
                | None ->
                    let buffer = new Buffer(x, buffers.Count + 1, size, events, fun () -> x.Error Naming)
                    buffers.Add buffer
                    buffer :> IBackendBuffer
            interface IBufferRuntime with
                member x.CreateBuffer(size, usage, storage) =
                    x.DefaultCreates <- x.DefaultCreates + 1
                    x.Allocate(size, usage, storage)
                member _.PrepareBuffer(_, _, _) = unexpected()
                member _.Upload(_, _, _, _, _) = unexpected()
                member _.Download(_, _, _, _) = unexpected()
                member _.DownloadAsync(_, _, _, _) = unexpected()
                member x.Copy(src, srcOffset, dst, dstOffset, count, discard) =
                    let a, b = src :?> Buffer, dst :?> Buffer
                    Expect.equal a.Disposals 0 "Copying from a disposed buffer"
                    Expect.equal b.Disposals 0 "Copying to a disposed buffer"
                    Expect.equal (srcOffset, dstOffset, discard) (0UL, 0UL, false) "Copy-prefix forwarding"
                    Expect.equal count (min src.SizeInBytes dst.SizeInBytes) "Copy-prefix size"
                    events.Add(CopyPrefix(a.Id, b.Id, count))
                    // A failing copy may already have written its destination.
                    Array.Copy(a.Data, b.Data, int count)
                    match x.Error Copy with Some error -> raise error | None -> ()

        type private Overridden(runtime : Runtime, size, usage, storage, discard) =
            inherit AdaptiveBuffer(runtime, size, usage, storage, discard)
            member val Calls = 0 with get, set
            override x.CreateHandle(size, usage, storage) =
                x.Calls <- x.Calls + 1
                runtime.Allocate(size, usage, storage)

        type private Fixture(size, discard, usage, storage, overridden) =
            let runtime = Runtime()
            let resource =
                if overridden then new Overridden(runtime, size, usage, storage, discard) :> AdaptiveBuffer
                else new AdaptiveBuffer(runtime, size, usage, storage, discard)
            new(size, discard) = new Fixture(size, discard, BufferUsage.Vertex, BufferStorage.Host, false)
            member _.Runtime = runtime
            member _.Resource = resource
            member _.Usage = usage ||| (if discard then BufferUsage.Write else BufferUsage.ReadWrite)
            member _.Storage = storage
            member _.Get() = resource.GetValue(AdaptiveToken.Top) :?> Buffer
            member x.Seed() =
                resource.Name <- "owned"
                resource.Acquire()
                let buffer = x.Get()
                for i in 0 .. buffer.Data.Length - 1 do buffer.Data.[i] <- byte (i * 7 + 3)
                buffer
            interface IDisposable with
                member _.Dispose() =
                    runtime.Failure <- None
                    resource.ReleaseAll()
                    // Original-source controls can leave unowned candidates behind.
                    for buffer in runtime.Buffers do
                        if buffer.Disposals = 0 then (buffer :> IBackendBuffer).Dispose()

        let private transitions = [16UL, 32UL; 16UL, 8UL; 16UL, 0UL; 0UL, 16UL]
        let private sites discard = if discard then [Allocation; Naming] else [Allocation; Naming; Copy]
        let private failure context (expected : exn) action =
            let actual = try action(); None with error -> Some error
            match actual with
            | None -> failtestf "%s: failure was bypassed" context
            | Some error -> Expect.isTrue (obj.ReferenceEquals(expected, error)) $"{context}: exception identity ({error})"

        let private balanced context (fixture : Fixture) =
            for b in fixture.Runtime.Buffers do
                Expect.equal b.Disposals 1 $"{context}, buffer={b.Id}: final disposal"
            Expect.equal fixture.Resource.Size 0UL $"{context}: size reset"

        let private failed context (fixture : Fixture) (old : Buffer) saved requested site attempt =
            Expect.equal fixture.Resource.Size requested $"{context}: requested size retained"
            Expect.equal old.Disposals 0 $"{context}: previous handle retired"
            Expect.equal old.Data saved $"{context}: previous bytes changed"
            Expect.equal fixture.Runtime.Buffers.Count (1 + (if site = Allocation then 0 else attempt)) $"{context}: candidate count"
            for b in fixture.Runtime.Buffers do
                if b.Id <> old.Id then Expect.equal b.Disposals 1 $"{context}, candidate={b.Id}: uncommitted candidate leaked"

        let private recovered context (fixture : Fixture) (old : Buffer) (saved : byte[]) requested discard =
            let result = fixture.Get()
            let handle = result :> IBackendBuffer
            Expect.equal handle.SizeInBytes requested $"{context}: retry size"
            Expect.equal result.Disposals 0 $"{context}: live result"
            Expect.isFalse (obj.ReferenceEquals(result, old)) $"{context}: retry returned stale storage"
            Expect.equal old.Disposals 1 $"{context}: old handle retirement"
            Expect.equal handle.Name fixture.Resource.Name $"{context}: retry name"
            Expect.isTrue (obj.ReferenceEquals(handle.Runtime, fixture.Runtime)) $"{context}: runtime"
            let prefix = if discard then 0 else min saved.Length result.Data.Length
            let expected = Array.init result.Data.Length (fun i -> if i < prefix then saved.[i] else 0xCDuy)
            Expect.equal result.Data expected $"{context}: preserved prefix / fresh tail"
            for _ in 1 .. 4 do Expect.isTrue (obj.ReferenceEquals(fixture.Get(), result)) $"{context}: retry cache"
            result

        let initialFailures() =
            for size in [0UL; 16UL] do
                for site in [Allocation; Naming] do
                    let context = $"initial size={size}, site={site}"
                    use fixture = new Fixture(size, false)
                    fixture.Resource.Name <- "initial"
                    let error = InvalidOperationException(context)
                    fixture.Runtime.Failure <- Some(site, error)
                    failure context error fixture.Resource.Acquire
                    for attempt in 1 .. 3 do
                        failure $"{context}, retry={attempt}" error (fun () -> fixture.Get() |> ignore)
                        for b in fixture.Runtime.Buffers do Expect.equal b.Disposals 1 $"{context}, candidate={b.Id}: initial candidate disposal"
                    fixture.Runtime.Failure <- None
                    let result = fixture.Get()
                    Expect.equal (result :> IBackendBuffer).SizeInBytes size $"{context}: initial retry size"
                    Expect.equal (result :> IBackendBuffer).Name "initial" $"{context}: initial retry name"
                    fixture.Resource.Release()
                    balanced context fixture

        let lazyFailures() =
            for initial, requested in transitions do
                for discard in [false; true] do
                    for site in sites discard do
                        let context = $"lazy {initial}->{requested}, discard={discard}, site={site}"
                        use fixture = new Fixture(initial, discard)
                        let old = fixture.Seed()
                        let saved = Array.copy old.Data
                        fixture.Resource.Resize(requested)
                        let error = InvalidOperationException(context)
                        fixture.Runtime.Failure <- Some(site, error)
                        for attempt in 1 .. 3 do
                            failure $"{context}, attempt={attempt}" error (fun () -> fixture.Get() |> ignore)
                            failed context fixture old saved requested site attempt
                        fixture.Runtime.Failure <- None
                        recovered context fixture old saved requested discard |> ignore
                        fixture.Resource.Release()
                        balanced context fixture

        let immediateFailures() =
            for initial, requested in transitions do
                for discard in [false; true] do
                    for site in sites discard do
                        let context = $"immediate {initial}->{requested}, discard={discard}, site={site}"
                        use fixture = new Fixture(initial, discard)
                        let old = fixture.Seed()
                        let saved = Array.copy old.Data
                        let size = (fixture.Resource :> aval<IBackendBuffer>) |> AVal.map _.SizeInBytes
                        Expect.equal (AVal.force size) initial $"{context}: primed dependent cache"
                        let error = InvalidOperationException(context)
                        fixture.Runtime.Failure <- Some(site, error)
                        failure context error (fun () -> fixture.Resource.Resize(requested, true))
                        failed context fixture old saved requested site 1
                        for attempt in 2 .. 3 do
                            failure $"{context}, attempt={attempt}" error (fun () -> fixture.Get() |> ignore)
                            failed context fixture old saved requested site attempt
                        fixture.Runtime.Failure <- None
                        Expect.equal (AVal.force size) requested $"{context}: dependent cache retried requested size"
                        recovered context fixture old saved requested discard |> ignore
                        fixture.Resource.Release()
                        balanced context fixture

        let releaseAfterFailure() =
            for immediate in [false; true] do
                for initial, requested in transitions do
                    for site in [Allocation; Naming; Copy] do
                        for releaseAll in [false; true] do
                            let context = $"release {initial}->{requested}, immediate={immediate}, site={site}, all={releaseAll}"
                            use fixture = new Fixture(initial, false)
                            let old = fixture.Seed()
                            let saved = Array.copy old.Data
                            fixture.Resource.Acquire()
                            let error = InvalidOperationException(context)
                            fixture.Runtime.Failure <- Some(site, error)
                            if immediate then failure context error (fun () -> fixture.Resource.Resize(requested, true))
                            else
                                fixture.Resource.Resize(requested)
                                failure context error (fun () -> fixture.Get() |> ignore)
                            failed context fixture old saved requested site 1
                            if releaseAll then fixture.Resource.ReleaseAll()
                            else
                                fixture.Resource.Release()
                                Expect.equal old.Disposals 0 $"{context}: remaining owner"
                                fixture.Resource.Release()
                            balanced context fixture
                            fixture.Resource.ReleaseAll()
                            balanced context fixture
                            fixture.Runtime.Failure <- None
                            fixture.Resource.Acquire()
                            let fresh = fixture.Get()
                            Expect.equal (fresh :> IBackendBuffer).SizeInBytes 0UL $"{context}: reacquisition size"
                            Expect.isFalse (obj.ReferenceEquals(old, fresh)) $"{context}: reacquired retired storage"
                            fixture.Resource.Release()
                            balanced context fixture

        let successfulResizes() =
            for immediate in [false; true] do
                for initial, requested in transitions do
                    for discard in [false; true] do
                        let context = $"success {initial}->{requested}, immediate={immediate}, discard={discard}"
                        use fixture = new Fixture(initial, discard)
                        let old = fixture.Seed()
                        let saved = Array.copy old.Data
                        fixture.Runtime.Events.Clear()
                        fixture.Resource.Resize(requested, immediate)
                        if not immediate then Expect.equal fixture.Runtime.Events.Count 0 $"{context}: lazy resize materialized early"
                        let result = recovered context fixture old saved requested discard
                        let expected = [
                            Allocate(requested, fixture.Usage, fixture.Storage)
                            Name(result.Id, "owned")
                            if not discard then CopyPrefix(old.Id, result.Id, min initial requested)
                            Dispose old.Id
                        ]
                        Expect.sequenceEqual fixture.Runtime.Events expected $"{context}: allocate/name/copy/retire ordering"
                        fixture.Resource.Release()
                        balanced context fixture

        let controls() =
            for overridden in [false; true] do
                for usage in [BufferUsage.None; BufferUsage.Indirect ||| BufferUsage.Vertex] do
                    for storage in [BufferStorage.Host; BufferStorage.Device] do
                        for discard in [false; true] do
                            let context = $"override={overridden}, usage={usage}, storage={storage}, discard={discard}"
                            use fixture = new Fixture(16UL, discard, usage, storage, overridden)
                            let first = fixture.Seed()
                            let requests = fixture.Runtime.Events.Count
                            for immediate in [false; true] do
                                fixture.Resource.Resize(16UL, immediate)
                                for _ in 1 .. 4 do Expect.isTrue (obj.ReferenceEquals(fixture.Get(), first)) $"{context}: same-size cache"
                            Expect.equal fixture.Runtime.Events.Count requests $"{context}: same-size resize had side effects"
                            fixture.Resource.Name <- "renamed"
                            Expect.equal (first :> IBackendBuffer).Name "renamed" $"{context}: live naming"
                            fixture.Resource.Resize(32UL, true)
                            let current = fixture.Get()
                            Expect.equal (current :> IBackendBuffer).Name "renamed" $"{context}: replacement naming"
                            Expect.equal fixture.Resource.Name "renamed" $"{context}: logical name"
                            let requests = fixture.Runtime.Events.Count
                            transact fixture.Resource.MarkOutdated
                            Expect.isTrue (obj.ReferenceEquals(fixture.Get(), current)) $"{context}: unchanged invalidation cache"
                            Expect.equal fixture.Runtime.Events.Count requests $"{context}: unchanged invalidation allocated"
                            for event in fixture.Runtime.Events do
                                match event with
                                | Allocate(_, u, s) -> Expect.equal (u, s) (fixture.Usage, fixture.Storage) $"{context}: creation flags"
                                | _ -> ()
                            if overridden then
                                Expect.equal (fixture.Resource :?> Overridden).Calls 2 $"{context}: override dispatch"
                                Expect.equal fixture.Runtime.DefaultCreates 0 $"{context}: default creation bypassed override"
                            else Expect.equal fixture.Runtime.DefaultCreates 2 $"{context}: default creation"
                            fixture.Resource.Release()
                            balanced context fixture

    let tests (target : TestTarget) =
        [
            "Initial allocation and name failures retry cleanly",  Cases.initialFailures
            "Lazy resize failures retain old data and retry",      Cases.lazyFailures
            "Failed immediate resize invalidates cached storage",  Cases.immediateFailures
            "Final release after failure and reacquisition",       Cases.releaseAfterFailure
            "Successful preservation and discard ordering",        Cases.successfulResizes
            "Names flags caching and creation override controls",  Cases.controls
        ]
        |> prepareCasesCpu "AdaptiveBuffer" target
