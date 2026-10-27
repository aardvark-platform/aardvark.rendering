namespace Aardvark.Rendering.Tests.Buffer

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto

module ManagedBufferRecovery =

    type private Site = Source | Upload

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let private throwInjected (error : exn) : unit = raise error

    type private Faults() =
        let error = InvalidOperationException("transient managed write failure")
        let events = ResizeArray<Site * int>()
        let mutable site = Source
        let mutable position = 0
        let mutable ordinal = 0
        let mutable remaining = 0
        let mutable selected = None
        let mutable beforeFailure : int -> unit = ignore
        member _.Error = error
        member _.Events = events
        member _.Selected = selected.Value
        member _.BeforeFailure with set action = beforeFailure <- action
        member _.Arm(at, index, count) =
            site <- at
            position <- index
            remaining <- count
            ordinal <- 0
            selected <- None
            events.Clear()
        member _.Before(at, id) =
            events.Add(at, id)
            if at = site && remaining > 0 then
                if selected.IsNone then
                    ordinal <- ordinal + 1
                    if ordinal = position then selected <- Some id
                if selected = Some id then
                    remaining <- remaining - 1
                    let action = beforeFailure
                    beforeFailure <- ignore
                    action id
                    throwInjected error

    type private CheckedValue<'T>(input : aval<'T>, faults : Faults, id : int) =
        inherit AVal.AbstractVal<'T>()
        override _.Compute(token) =
            let value = input.GetValue token
            faults.Before(Source, id)
            value

    type private HostBuffer(runtime : IBufferRuntime, size : uint64, onDispose : unit -> unit) =
        let data = Array.create (int size) 233uy
        let mutable name = null
        member _.Data = data
        interface IBackendBuffer with
            member _.Runtime = runtime
            member _.Handle = 0UL
            member _.Name with get() = name and set value = name <- value
            member x.Buffer = x :> IBackendBuffer
            member _.Offset = 0UL
            member _.SizeInBytes = size
            member _.Dispose() = onDispose()

    type private HostRuntime(faults : Faults) as this =
        let windows = Dictionary<uint64, int * uint64>()
        let completed = ResizeArray<int * uint64>()
        let mutable current = Unchecked.defaultof<HostBuffer>
        let mutable created = 0
        let mutable disposed = 0
        member _.Allow(id, offset, size) = windows.Add(offset, (id, size))
        member _.Completed = completed
        member _.Data = current.Data
        member _.Created = created
        member _.Disposed = disposed
        interface IBufferRuntime with
            member _.PrepareBuffer(_, _, _) = failwith "Unexpected preparation"
            member _.CreateBuffer(size, _, _) =
                Expect.isLessThanOrEqual size 4096UL "Bounded host backing"
                created <- created + 1
                let buffer = new HostBuffer(this, size, fun () -> disposed <- disposed + 1)
                current <- buffer
                buffer :> IBackendBuffer
            member _.Upload(source, target, offset, size, _) =
                Expect.isTrue (windows.ContainsKey offset) $"Unexpected upload offset {offset}"
                let id, expectedSize = windows.[offset]
                Expect.equal size expectedSize $"writer={id}, offset={offset}: checked upload size"
                Expect.isTrue (offset <= target.SizeInBytes && size <= target.SizeInBytes - offset) "Checked upload backing range"
                faults.Before(Upload, id)
                Marshal.Copy(source, (target :?> HostBuffer).Data, int offset, int size)
                completed.Add(id, offset)
            member _.Copy(source, sourceOffset, target, targetOffset, size, _) =
                Array.Copy((source :?> HostBuffer).Data, int sourceOffset, (target :?> HostBuffer).Data, int targetOffset, int size)
            member _.Download(_, _, _, _) = failwith "Unexpected download"
            member _.DownloadAsync(_, _, _, _) = failwith "Unexpected async download"

    let private bytes (values : int64[]) =
        let result = Array.zeroCreate<byte> (values.Length * sizeof<int64>)
        Buffer.BlockCopy(values, 0, result, 0, result.Length)
        result

    type private Target = { Id : int; Index : uint64; Count : int }

    type private Scene(kind : string, count : int, ranges : int) =
        let faults = Faults()
        let runtime = HostRuntime(faults)
        let buffer = ManagedBuffer.create<int64> runtime BufferUsage.All BufferStorage.Host
        let generations = Array.zeroCreate<int> (count + 1)
        let setters = Array.zeroCreate<int -> unit> (count + 1)
        let active = System.Collections.Generic.HashSet<int>()
        let retiredRanges = System.Collections.Generic.HashSet<uint64>()
        let tokens = Dictionary<int, IDisposable list>()
        let length = if kind = "scalar" then 1 else 4
        let values id generation = Array.init length (fun j -> int64 (100 * id + 1000 * generation + j + 1))
        let inputs =
            Array.init (count + 1) (fun id ->
                if kind = "scalar" then
                    let value = cval (values id 0).[0]
                    setters.[id] <- fun generation -> value.Value <- (values id generation).[0]
                    Choice1Of2(CheckedValue(value, faults, id) :> IAdaptiveValue)
                else
                    let make generation : IBuffer =
                        let data = values id generation
                        if kind = "converted array" then ArrayBuffer(Array.map int data) :> IBuffer
                        else ArrayBuffer(data) :> IBuffer
                    let value = cval (make 0)
                    setters.[id] <- fun generation -> value.Value <- make generation
                    let source = CheckedValue(value, faults, id) :> aval<IBuffer>
                    Choice2Of2(BufferView(source, if kind = "converted array" then typeof<int> else typeof<int64>)))
        let targets = [|for id in 0 .. count do for range in 0 .. ranges - 1 do { Id = id; Index = uint64 (8 + id * 48 + range * 8); Count = length }|]
        let register id =
            let registrations =
                targets |> Array.filter (fun t -> t.Id = id) |> Array.map (fun target ->
                    match inputs.[id] with
                    | Choice1Of2 source -> buffer.Add(source, target.Index)
                    | Choice2Of2 view -> buffer.Add(view, Range1ul(target.Index, target.Index + uint64 target.Count - 1UL))) |> Array.toList
            tokens.Add(id, registrations)
            active.Add id |> ignore
        do
            for target in targets do runtime.Allow(target.Id, target.Index * 8UL, uint64 (target.Count * 8))
            buffer.Acquire()
            buffer.Resize(4096UL, true)
            for id in 0 .. count - 1 do register id
        member _.Faults = faults
        member _.Runtime = runtime
        member _.Targets = targets
        member _.Active = active
        member _.Extra = count
        member _.RegisterExtra() = register count
        member _.Force() = buffer.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore
        member _.Update(id) =
            transact (fun () -> generations.[id] <- generations.[id] + 1; setters.[id] generations.[id])
        member _.UpdateAll() =
            transact (fun () ->
                for id in active do
                    generations.[id] <- generations.[id] + 1
                    setters.[id] generations.[id])
        member _.Remove(id) =
            for token in tokens.[id] do token.Dispose()
            tokens.Remove id |> ignore
            active.Remove id |> ignore
        member _.RetireFirstRange(id) =
            let target = targets |> Array.find (fun t -> t.Id = id)
            tokens.[id].Head.Dispose()
            tokens.[id] <- tokens.[id].Tail
            retiredRanges.Add target.Index |> ignore
            target.Index * 8UL
        member _.Check(context, baseline : byte[]) =
            let expected = Array.create baseline.Length 233uy
            for target in targets do
                if active.Contains target.Id && not (retiredRanges.Contains target.Index) then
                    let data = bytes (values target.Id generations.[target.Id])
                    Array.Copy(data, 0, expected, int target.Index * 8, data.Length)
                else
                    Array.Copy(baseline, int target.Index * 8, expected, int target.Index * 8, target.Count * 8)
            Expect.sequenceEqual runtime.Data expected (context + ": complete live payload and unchanged neighboring bytes")
        member x.Cached(context) =
            let before = runtime.Completed.Count
            let events = faults.Events.Count
            x.Force()
            Expect.equal runtime.Completed.Count before (context + ": unchanged cached read reuploaded")
            Expect.equal faults.Events.Count events (context + ": unchanged cached read reevaluated a source")
        interface IDisposable with
            member _.Dispose() =
                for registrations in tokens.Values do for token in registrations do token.Dispose()
                buffer.Release()
                Expect.equal runtime.Created 2 "Only empty and fixed-capacity backing may be created"
                Expect.equal runtime.Disposed runtime.Created "Every backing was released once"

    module private Cases =
        let private kinds = ["scalar"; "array"; "converted array"]
        let private pristine () = Array.create 4096 233uy
        let private fail context (scene : Scene) =
            let actual = try scene.Force(); None with e -> Some e
            match actual with
            | Some error ->
                Expect.isTrue (obj.ReferenceEquals(error, scene.Faults.Error)) (context + ": original exception identity")
                Expect.stringContains error.StackTrace "throwInjected" (context + ": original throwing frame")
            | None -> failtest (context + ": injected failure did not occur")

        let private batches invalidated =
            for kind in kinds do
                for count in [1; 3; 7] do
                    for site in [Source; Upload] do
                        for position in ([1; (count + 1) / 2; count] |> List.distinct) do
                            for failures in [1; 3] do
                                use scene = new Scene(kind, count, 1)
                                let context = $"{kind}, count={count}, {site}, position={position}, failures={failures}, invalidated={invalidated}"
                                if invalidated then
                                    scene.Force()
                                    scene.Check(context + " initial", pristine())
                                    scene.UpdateAll()
                                scene.Faults.Arm(site, position, failures)
                                for _ in 1 .. failures do fail context scene
                                scene.Force()
                                scene.Check(context, pristine())
                                for target in scene.Targets do
                                    if scene.Active.Contains target.Id then
                                        let uploads = scene.Runtime.Completed |> Seq.filter (fun (_, offset) -> offset = target.Index * 8UL) |> Seq.length
                                        Expect.equal uploads (if invalidated then 2 else 1) (context + ": successful writers must not reupload on retry")
                                scene.Cached context

        let initial () = batches false
        let invalidated () = batches true

        let shared () =
            for kind in ["array"; "converted array"] do
                for site in [Source; Upload] do
                    for position in (if site = Source then [1] else [1; 2; 3]) do
                        for failures in [1; 3] do
                            for invalidated in [false; true] do
                                for retireRange in [false; true] do
                                    use scene = new Scene(kind, 1, 3)
                                    let context = $"shared {kind}, {site}, position={position}, failures={failures}, invalidated={invalidated}, retireRange={retireRange}"
                                    if invalidated then
                                        scene.Force()
                                        scene.UpdateAll()
                                    scene.Faults.Arm(site, position, failures)
                                    for _ in 1 .. failures do fail context scene
                                    let retired = if retireRange then Some(scene.RetireFirstRange 0) else None
                                    let frozen = Array.copy scene.Runtime.Data
                                    let retiredUploads = scene.Runtime.Completed |> Seq.filter (fun (_, offset) -> Some offset = retired) |> Seq.length
                                    scene.Force()
                                    scene.Check(context, frozen)
                                    Expect.equal (scene.Runtime.Completed |> Seq.filter (fun (_, offset) -> Some offset = retired) |> Seq.length) retiredUploads (context + ": retired shared range was uploaded")
                                    scene.Cached context

        let disposal () =
            for kind in kinds do
                for site in [Source; Upload] do
                    for retirement in ["failed"; "unattempted"; "all remaining"; "successful"; "during failure"] do
                        use scene = new Scene(kind, 5, 1)
                        scene.Force()
                        scene.UpdateAll()
                        scene.Faults.Arm(site, 2, 1)
                        if retirement = "during failure" then scene.Faults.BeforeFailure <- scene.Remove
                        let context = $"{kind}, {site}, retirement={retirement}"
                        fail context scene
                        let failed = scene.Faults.Selected
                        let attempted = scene.Faults.Events |> Seq.filter (fun (at, _) -> at = Source) |> Seq.map snd |> Set.ofSeq
                        let unattempted = [0 .. 4] |> List.filter (fun id -> not (attempted.Contains id))
                        let first = scene.Runtime.Completed.[5] |> fst
                        let removed =
                            match retirement with
                            | "failed" -> [failed]
                            | "unattempted" -> [List.head unattempted]
                            | "all remaining" -> failed :: unattempted
                            | "successful" -> [first]
                            | _ -> []
                        for id in removed do scene.Remove id
                        for id in failed :: removed |> List.distinct do
                            if not (scene.Active.Contains id) then scene.Update id
                        let frozen = Array.copy scene.Runtime.Data
                        let retiredCounts = scene.Runtime.Completed |> Seq.countBy fst |> Map.ofSeq
                        scene.Force()
                        scene.Check(context, frozen)
                        for id in [0 .. 4] do
                            if not (scene.Active.Contains id) then
                                let count = scene.Runtime.Completed |> Seq.filter (fun (i, _) -> i = id) |> Seq.length
                                Expect.equal count retiredCounts.[id] (context + ": disposed writer was resurrected")
                        scene.Cached context

        let scheduled () =
            for kind in kinds do
                for site in [Source; Upload] do
                    use scene = new Scene(kind, 4, 1)
                    scene.Force()
                    scene.UpdateAll()
                    scene.Faults.Arm(site, 2, 1)
                    let mutable updated = -1
                    scene.Faults.BeforeFailure <- fun _ ->
                        updated <- scene.Runtime.Completed.[4] |> fst
                        scene.Update updated
                        scene.RegisterExtra()
                    let context = $"scheduled {kind}, {site}"
                    fail context scene
                    scene.Force()
                    scene.Check(context, pristine())
                    Expect.isTrue (scene.Active.Contains scene.Extra) "New registration disappeared"
                    let repeated = scene.Runtime.Completed |> Seq.filter (fun (id, _) -> id = updated) |> Seq.length
                    Expect.equal repeated 3 (context + ": update scheduled for an already successful writer was lost")
                    scene.Cached context

        let controls () =
            for kind in kinds do
                for count in [1; 3; 7] do
                    use scene = new Scene(kind, count, 1)
                    scene.Force()
                    scene.Check($"control {kind}/{count} initial", pristine())
                    scene.Cached "initial cached control"
                    scene.UpdateAll()
                    scene.Force()
                    scene.Check($"control {kind}/{count} updated", pristine())
                    Expect.equal scene.Runtime.Completed.Count (2 * count) "Successful control upload count"
                    scene.Cached "updated cached control"

    let tests (target : TestTarget) =
        [
            "Initial batches recover without input changes",      Cases.initial
            "Invalidated batches recover without input changes",  Cases.invalidated
            "Shared writers retry complete live ranges",          Cases.shared
            "Disposed writers stay retired during recovery",      Cases.disposal
            "Newly scheduled changes survive failed processing",  Cases.scheduled
            "Successful updates and cached reads remain intact",  Cases.controls
        ]
        |> prepareCasesCpu "ManagedBufferRecovery" target
