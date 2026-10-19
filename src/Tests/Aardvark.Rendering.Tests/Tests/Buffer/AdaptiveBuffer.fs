namespace Aardvark.Rendering.Tests.Buffer

open System
open System.Runtime.InteropServices
open Aardvark.Base
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
            | Upload of int * uint64 * uint64 * uint64 * bool
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
            member val SourceBounds : (nativeint * uint64) option = None with get, set
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
                member x.Upload(src, dst, offset, count, discard) =
                    let address, length =
                        match x.SourceBounds with
                        | Some (address, length) -> uint64 address, length
                        | None -> unexpected()
                    let relative = if uint64 src >= address then uint64 src - address else UInt64.MaxValue
                    let buffer = dst :?> Buffer
                    events.Add(Upload(buffer.Id, relative, offset, count, discard))
                    // Original-source probes can forward invalid pointers. Refuse
                    // every read unless it lies inside the independently pinned array.
                    Expect.isTrue (relative <= length && count <= length - relative) "Refusing an invalid source-pointer read"
                    Expect.equal buffer.Disposals 0 "Uploading to a disposed buffer"
                    Expect.isTrue (offset <= dst.SizeInBytes && count <= dst.SizeInBytes - offset) "Upload destination bounds"
                    Marshal.Copy(src, buffer.Data, int offset, int count)
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

        type private ArrayMatrix =
            abstract Run<'T when 'T : unmanaged> : 'T[] -> unit

        let private arrayTypes (action : ArrayMatrix) =
            action.Run(Array.init 17 (fun i -> byte (i * 11 + 3)))
            action.Run(Array.init 17 (fun i -> int16 (i * 71 - 500)))
            action.Run(Array.init 17 (fun i -> i * 313 - 2000))
            action.Run(Array.init 17 (fun i -> int64 i * 100000003L - 2000000000L))
            action.Run(Array.init 17 (fun i -> V3f(float32 i + 0.25f, -float32 i, 7.0f)))
            action.Run(Array.init 17 (fun i -> V2d(float i + 0.5, -float i)))
            action.Run(Array.init 17 (fun i -> V4d(float i, 0.125, -float i, 17.0)))

        let private bytes<'T when 'T : unmanaged> (data : 'T[]) =
            let result = Array.zeroCreate<byte> (data.Length * sizeof<'T>)
            let pin = GCHandle.Alloc(data, GCHandleType.Pinned)
            try
                if result.Length > 0 then Marshal.Copy(pin.AddrOfPinnedObject(), result, 0, result.Length)
                result
            finally pin.Free()

        let private withSource<'T when 'T : unmanaged> (runtime : Runtime) (data : 'T[]) action =
            let pin = GCHandle.Alloc(data, GCHandleType.Pinned)
            runtime.SourceBounds <- Some(pin.AddrOfPinnedObject(), uint64 data.Length * uint64 sizeof<'T>)
            try action()
            finally
                runtime.SourceBounds <- None
                pin.Free()

        let private arrayState (fixture : Fixture) =
            fixture.Runtime.DefaultCreates, fixture.Resource.Size,
            (fixture.Runtime.Buffers |> Seq.map (fun b -> b, Array.copy b.Data, b.Disposals) |> Seq.toArray)

        let private noArrayWrite context (fixture : Fixture) (state : int * uint64 * (Buffer * byte[] * int)[]) =
            let creates, size, saved = state
            Expect.equal fixture.Runtime.DefaultCreates creates (context + ": no storage allocation")
            Expect.equal fixture.Runtime.Buffers.Count saved.Length (context + ": no new handles")
            Expect.equal fixture.Runtime.Events.Count 0 (context + ": no upload or other backend operation")
            Expect.equal fixture.Resource.Size size (context + ": logical size unchanged")
            for buffer, data, disposals in saved do
                Expect.equal buffer.Data data (context + ": live bytes unchanged")
                Expect.equal buffer.Disposals disposals (context + ": live ownership unchanged")

        let private invalidArrayRanges negative =
            arrayTypes { new ArrayMatrix with
                member _.Run<'T when 'T : unmanaged>(samples : 'T[]) =
                    for length in (if negative then [1; 0; 7; 17] else [7; 0; 1; 17]) do
                        let data = Array.take length samples
                        let ranges =
                            if negative then
                                [0, -1; 0, Int32.MinValue; length, -1; -1, 0; -1, 1;
                                 Int32.MinValue, 0; -1, Int32.MaxValue; Int32.MinValue, Int32.MaxValue]
                            else
                                [0, length + 1; length, 1; length + 1, 0; length + 1, 1;
                                 Int32.MaxValue, 0; Int32.MaxValue, 1; 1, Int32.MaxValue;
                                 Int32.MaxValue, Int32.MaxValue; length, Int32.MaxValue]
                        for start, count in ranges do
                            for live in [false; true] do
                                for discard in [false; true] do
                                    let context = sprintf "type=%s, length=%d, start=%d, count=%d, live=%b, discard=%b" typeof<'T>.Name length start count live discard
                                    use fixture = new Fixture(uint64 (length * sizeof<'T> + 64), false)
                                    if live then fixture.Seed() |> ignore
                                    fixture.Runtime.Events.Clear()
                                    let state = arrayState fixture
                                    let actual =
                                        try
                                            withSource fixture.Runtime data (fun () ->
                                                AdaptiveBufferExtensions.Write<'T>(fixture.Resource :> IAdaptiveBuffer, data, 0UL, start, count, discard))
                                            None
                                        with error -> Some error
                                    noArrayWrite context fixture state
                                    match actual with
                                    | Some (:? ArgumentException as error) ->
                                        Expect.equal (error.GetType()) typeof<ArgumentException> (context + ": ArgumentException convention")
                                        Expect.isTrue (error.Message.StartsWith("[Buffer] ") && error.Message.EndsWith(".")) (context + ": buffer diagnostic convention")
                                    | _ -> failtestf "%s: expected ArgumentException, got %A" context actual }

        let negativeArrayRanges() = invalidArrayRanges true
        let oversizedArrayRanges() = invalidArrayRanges false

        let private arrayWrite<'T when 'T : unmanaged> (data : 'T[]) start count offset discard live route =
            let context = sprintf "type=%s, length=%d, start=%d, count=%d, offset=%d, discard=%b, live=%b, route=%s" typeof<'T>.Name data.Length start count offset discard live route
            let source = bytes data
            let capacity = source.Length + 64
            use fixture = new Fixture(uint64 capacity, false)
            let saved = if live then Array.copy (fixture.Seed().Data) else Array.create capacity 0xCDuy
            fixture.Runtime.Events.Clear()
            let target = fixture.Resource :> IAdaptiveBuffer
            withSource fixture.Runtime data (fun () ->
                match route with
                | "whole" -> AdaptiveBufferExtensions.Write<'T>(target, data, offset, discard)
                | "default" -> AdaptiveBufferExtensions.Write<'T>(target, data)
                | _ -> AdaptiveBufferExtensions.Write<'T>(target, data, offset, start, count, discard))
            Expect.equal fixture.Runtime.DefaultCreates 1 (context + ": create only when storage is missing")
            Expect.equal fixture.Runtime.Buffers.Count 1 (context + ": reuse live storage")
            let result = fixture.Runtime.Buffers.[0]
            let expected = Array.copy saved
            Array.Copy(source, start * sizeof<'T>, expected, int offset, count * sizeof<'T>)
            Expect.equal result.Data expected (context + ": exact payload, byte offset and untouched guards")
            Expect.equal (bytes data) source (context + ": source not modified")
            let uploads = fixture.Runtime.Events |> Seq.choose (function Upload(id, src, dst, size, flag) -> Some(id, src, dst, size, flag) | _ -> None) |> Seq.toArray
            Expect.equal uploads [|result.Id, uint64 start * uint64 sizeof<'T>, offset, uint64 count * uint64 sizeof<'T>, discard|]
                (context + ": source pointer, byte count and discard forwarding")
            if not live then fixture.Resource.Acquire()
            fixture.Resource.ReleaseAll()
            balanced context fixture

        let arrayPayloads() =
            arrayTypes { new ArrayMatrix with
                member _.Run<'T when 'T : unmanaged>(samples : 'T[]) =
                    for length in [1; 7; 17] do
                        let data = Array.take length samples
                        for start, count in [0, length; 0, 1; length / 2, length - length / 2; length - 1, 1] do
                            for offset in [0UL; 1UL; 7UL; 24UL] do
                                for discard in [false; true] do
                                    for live in [false; true] do
                                        arrayWrite data start count offset discard live "range" }

        let wholeArrays() =
            arrayTypes { new ArrayMatrix with
                member _.Run<'T when 'T : unmanaged>(samples : 'T[]) =
                    for length in [1; 7; 17] do
                        let data = Array.take length samples
                        for live in [false; true] do
                            arrayWrite data 0 length 0UL false live "default"
                            for offset in [0UL; 3UL; 24UL] do
                                for discard in [false; true] do
                                    arrayWrite data 0 length offset discard live "whole" }

        let zeroArrayWrites() =
            arrayTypes { new ArrayMatrix with
                member _.Run<'T when 'T : unmanaged>(samples : 'T[]) =
                    for length in [0; 1; 17] do
                        let data = Array.take length samples
                        for live in [false; true] do
                            use fixture = new Fixture(uint64 (length * sizeof<'T> + 64), false)
                            if live then fixture.Seed() |> ignore
                            fixture.Runtime.Events.Clear()
                            let state = arrayState fixture
                            for start = 0 to length do
                                for offset in [0UL; 1UL; UInt64.MaxValue] do
                                    for discard in [false; true] do
                                        let context = sprintf "type=%s, length=%d, start=%d, offset=%d, discard=%b, live=%b" typeof<'T>.Name length start offset discard live
                                        withSource fixture.Runtime data (fun () ->
                                            AdaptiveBufferExtensions.Write<'T>(fixture.Resource :> IAdaptiveBuffer, data, offset, start, 0, discard))
                                        noArrayWrite context fixture state
                                        if length = 0 then
                                            withSource fixture.Runtime data (fun () ->
                                                AdaptiveBufferExtensions.Write<'T>(fixture.Resource :> IAdaptiveBuffer, data, offset, discard))
                                            noArrayWrite (context + ": whole empty array") fixture state }

    let tests (target : TestTarget) =
        [
            "Initial allocation and name failures retry cleanly",  Cases.initialFailures
            "Lazy resize failures retain old data and retry",      Cases.lazyFailures
            "Failed immediate resize invalidates cached storage",  Cases.immediateFailures
            "Final release after failure and reacquisition",       Cases.releaseAfterFailure
            "Successful preservation and discard ordering",        Cases.successfulResizes
            "Names flags caching and creation override controls",  Cases.controls

            "Array sources.Negative ranges rejected",          Cases.negativeArrayRanges
            "Array sources.Out-of-bounds ranges rejected",     Cases.oversizedArrayRanges
            "Array sources.Valid payload and forwarding",      Cases.arrayPayloads
            "Array sources.Whole-array delegation",            Cases.wholeArrays
            "Array sources.Zero count does not materialize",   Cases.zeroArrayWrites
        ]
        |> prepareCasesCpu "AdaptiveBuffer" target
