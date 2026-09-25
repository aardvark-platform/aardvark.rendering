namespace Aardvark.Rendering.Tests

open System
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Expecto

module ``SceneGraph Buffer Cache Tests`` =
    module C = SgFSharpHelpers.Caching

    let private same a b message = Expect.isTrue (obj.ReferenceEquals(a, b)) message
    let private different a b message = Expect.isFalse (obj.ReferenceEquals(a, b)) message
    let private semantic = Symbol.Create "RawTransform"
    let private geometry =
        IndexedGeometry(Mode = IndexedGeometryMode.PointList,
                        IndexedAttributes = SymDict.ofList [DefaultSemantic.Positions, [|V3f.Zero|] :> Array])

    let private trafos count =
        let random = Random(673 + count)
        Array.init count (fun i ->
            Trafo3d.Scale(V3d(0.5 + random.NextDouble(), 1.5 + random.NextDouble(), 2.0 + float i)) *
            Trafo3d.RotationEuler(V3d(random.NextDouble(), random.NextDouble(), random.NextDouble())) *
            Trafo3d.Translation(V3d(float i, -3.0 * float i, 1e4 + float i)))

    let private source constant values : aval<_> = if constant then AVal.constant values else cval values :> aval<_>
    let private rawInstance (value : aval<Trafo3d[]>) =
        let sg = Sg.empty |> Sg.instanceAttribute semantic value :?> Sg.InstanceAttributeApplicator
        sg.Values.[semantic]
    let private instanced value = Sg.instancedGeometry value geometry :?> Sg.InstanceAttributeApplicator
    let private pair (sg : Sg.InstanceAttributeApplicator) =
        sg.Values.[DefaultSemantic.InstanceTrafo], sg.Values.[DefaultSemantic.InstanceTrafoInv]

    let private data<'T> (view : BufferView) =
        Expect.equal view.ElementType typeof<'T> "element type"
        Expect.equal view.Offset 0 "offset"
        Expect.equal view.Stride 0 "packed stride"
        Expect.isTrue view.Normalized "normalization flag"
        Expect.isFalse view.IsSingleValue "array buffer"
        let buffer = AVal.force view.Buffer :?> ArrayBuffer
        Expect.equal buffer.ElementType typeof<'T> "buffer element type"
        buffer.Data :?> 'T[]

    let private check (values : Trafo3d[]) raw (forward, backward) =
        same (data<Trafo3d> raw) values "ordinary buffer retains the input array"
        Expect.equal (data<M44f> forward) (values |> Array.map (fun t -> M44f t.Forward)) "forward conversion"
        Expect.equal (data<M44f> backward) (values |> Array.map (fun t -> M44f t.Backward)) "inverse conversion"
        different forward backward "separate forward/inverse views"
        if values.Length > 0 then
            different (data<M44f> forward) (data<M44f> backward) "separate matrix arrays"

    let rec private drawCall (sg : ISg) =
        match sg with
        | :? Sg.RenderNode as node -> AVal.force node.DrawCallInfo
        | :? IApplicator as node -> drawCall (AVal.force node.Child)
        | _ -> failwith "unexpected instance geometry structure"

    let private concurrently count (work : int -> 'T) =
        use start = new Barrier(count)
        let tasks =
            Array.init count (fun i ->
                Task.Factory.StartNew<'T>((fun () ->
                    if not (start.SignalAndWait(TimeSpan.FromSeconds 10.0)) then failwith "concurrent start timed out"
                    work i), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
        Task.WhenAll(tasks).GetAwaiter().GetResult()

    // The cache factory inspects IsConstant. A gate here can detect serialization of
    // unrelated factories without forcing an adaptive value or relying on timing ratios.
    type private FactoryProbe<'T>(value : 'T, visit : unit -> unit) =
        inherit AdaptiveObject()
        interface IAdaptiveValue<'T> with
            member _.IsConstant = visit(); false
            member _.ContentType = typeof<'T>
            member _.GetValue _ = value
            member _.GetValueUntyped _ = box value
            member x.Accept visitor = visitor.Visit (x :> aval<'T>)

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let private unreachable constant representation force =
        let values = trafos 3
        let value = source constant values
        let refs = ResizeArray<WeakReference>()
        let add (x : obj) = refs.Add(WeakReference x)
        add values
        add value
        if representation <> "pair" then
            let view = C.bufferOfArray value
            add view
            add view.Buffer
            if force then add (AVal.force view.Buffer)
        if representation <> "array" then
            let views = C.buffersOfTrafos value
            let f, b = views
            add views
            for view in [f; b] do
                add view
                add view.Buffer
                if force then add (AVal.force view.Buffer)
        refs.ToArray()

    let private ordinaryContents name (values : 'T[]) =
        testList name [
            for constant in [false; true] do
                testCase $"constant={constant}" <| fun _ ->
                    let value = source constant values
                    let view = C.bufferOfArray value
                    same values (data<'T> view) "typed buffer preserves its source array"
                    same view (C.bufferOfArray value) "typed cache reuse"
                    different view (C.bufferOfArray (source constant values)) "identity, not array equality"
        ]

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let private weakViews (value : aval<Trafo3d[]>) representation =
        if representation = "array" then WeakReference(C.bufferOfArray value)
        else WeakReference(C.buffersOfTrafos value)

    let private collect() =
        GC.Collect()
        GC.WaitForPendingFinalizers()
        GC.Collect()

    [<Tests>]
    let tests =
        testList "SceneGraph buffer cache" [
            ordinaryContents "typed int array" [|1; 2; 3|]
            ordinaryContents "typed vector array" [|V3f.Zero; V3f.IOO|]
            ordinaryContents "typed color array" [|C4b.Red; C4b.Blue|]
            ordinaryContents "typed matrix array" [|M44f.Identity|]
            ordinaryContents "typed reference array" [|"a"; "b"|]
            ordinaryContents "typed empty reference array" (Array.empty<string>)
            for constant in [false; true] do
                for rawFirst in [false; true] do
                    for count in [0; 1; 7] do
                        testCase $"public sharing/constant={constant}/rawFirst={rawFirst}/{count}" <| fun _ ->
                            let values = trafos count
                            let snapshot = Array.copy values
                            let value = source constant values
                            let raw, sg =
                                if rawFirst then let raw = rawInstance value in raw, instanced value
                                else let sg = instanced value in rawInstance value, sg
                            let forward, backward = pair sg
                            check values raw (forward, backward)
                            same raw (rawInstance value) "ordinary view reused"
                            let nextForward, nextBackward = pair (instanced value)
                            same forward nextForward "forward view reused"
                            same backward nextBackward "inverse view reused"
                            Expect.equal (drawCall sg).InstanceCount count "instance count"
                            Expect.equal values snapshot "source unchanged"
            for constant in [false; true] do
                testCase $"vertex/instance/index reuse/constant={constant}" <| fun _ ->
                    let values = [|2; 0; 1|]
                    let value = source constant values
                    let vertex = Sg.empty |> Sg.vertexAttribute DefaultSemantic.Positions value :?> Sg.VertexAttributeApplicator
                    let instance = Sg.empty |> Sg.instanceAttribute DefaultSemantic.Colors value :?> Sg.InstanceAttributeApplicator
                    let index = Sg.empty |> Sg.index value :?> Sg.VertexIndexApplicator
                    let view = vertex.Values.[DefaultSemantic.Positions]
                    same view instance.Values.[DefaultSemantic.Colors] "reuse across semantics and attribute rates"
                    same view index.Value "reuse for indices"
                    same view (C.bufferOfArray value) "public and helper lookups agree"
                    same values (data<int> view) "ordinary contents unchanged"
                for representation in ["array"; "pair"] do
                    testCase $"distinct equal sources/{representation}/constant={constant}" <| fun _ ->
                        let values = trafos 2
                        let a, b = source constant values, source constant values
                        different a b "distinct source objects"
                        if representation = "array" then
                            let x, y = C.bufferOfArray a, C.bufferOfArray b
                            different x y "distinct source identities do not share views"
                            same x (C.bufferOfArray a) "same source does share"
                        else
                            let x, y = C.buffersOfTrafos a, C.buffersOfTrafos b
                            different x y "distinct source identities do not share pairs"
                            different (fst x) (fst y) "distinct forward views"
                            different (snd x) (snd y) "distinct inverse views"
                            same x (C.buffersOfTrafos a) "published pair reused"
            for rawFirst in [false; true] do
                testCase $"adaptive contents and resizing/rawFirst={rawFirst}" <| fun _ ->
                    let value = cval (trafos 3)
                    let raw, sg =
                        if rawFirst then let raw = rawInstance value in raw, instanced value
                        else let sg = instanced value in rawInstance value, sg
                    let views = pair sg
                    for frame, count in List.indexed [3; 3; 1; 0; 7; 2; 0; 5] do
                        let values = trafos count |> Array.map (fun t -> t * Trafo3d.Translation(float frame, 0.0, 0.0))
                        let snapshot = Array.copy values
                        transact (fun () -> value.Value <- values)
                        Expect.equal value.Value values "adaptive contents (structurally equal assignments may be suppressed)"
                        check value.Value raw views
                        Expect.equal (drawCall sg).InstanceCount count "adaptive instance count"
                        same raw (C.bufferOfArray value) "updates do not replace ordinary view"
                        let f, b = C.buffersOfTrafos value
                        same f (fst views) "updates do not replace forward view"
                        same b (snd views) "updates do not replace inverse view"
                        Expect.equal values snapshot "updating and converting do not mutate input"
            for constant in [false; true] do
                for representation in ["array"; "pair"] do
                    testCase $"deferred evaluation/{representation}/constant={constant}" <| fun _ ->
                        let mutable reads = 0
                        let read() = reads <- reads + 1; trafos 2
                        let value = if constant then AVal.delay read else AVal.custom (fun _ -> read())
                        let views = if representation = "array" then [C.bufferOfArray value] else let f, b = C.buffersOfTrafos value in [f; b]
                        Expect.equal reads 0 "lookup does not evaluate the source"
                        for view in views do AVal.force view.Buffer |> ignore
                        Expect.equal reads 1 "derived buffers share the adaptive source evaluation"
                        for view in views do AVal.force view.Buffer |> ignore
                        Expect.equal reads 1 "unchanged reads remain cached"
            for constant in [false; true] do
                for representation in ["array"; "pair"; "mixed"] do
                    testCase $"concurrent cold publication/{representation}/constant={constant}" <| fun _ ->
                        for _ in 1 .. 24 do
                            let value = source constant (trafos 3)
                            let results = concurrently 8 (fun i ->
                                if representation = "array" || (representation = "mixed" && i % 2 = 0) then
                                    Choice1Of2(C.bufferOfArray value)
                                else Choice2Of2(C.buffersOfTrafos value))
                            for result in results do
                                match result with
                                | Choice1Of2 view -> same view (C.bufferOfArray value) "one published ordinary view"
                                | Choice2Of2 views -> same views (C.buffersOfTrafos value) "one published pair"
                            // Factories are permitted to execute more than once on a contended miss.
            for representation in ["array"; "pair"; "mixed"] do
                testCase $"unrelated cold keys are not serialized/{representation}" <| fun _ ->
                    use entered = new CountdownEvent(8)
                    use release = new ManualResetEventSlim(false)
                    let tasks = Array.init 8 (fun i ->
                        let mutable first = 1
                        let value = FactoryProbe(trafos 1, fun () ->
                            if Interlocked.Exchange(&first, 0) = 1 then entered.Signal() |> ignore
                            if not (release.Wait(TimeSpan.FromSeconds 10.0)) then failwith "factory release timed out")
                        Task.Factory.StartNew((fun () ->
                            if representation = "array" || (representation = "mixed" && i % 2 = 0) then C.bufferOfArray value |> ignore
                            else C.buffersOfTrafos value |> ignore), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
                    let allEntered = entered.Wait(TimeSpan.FromSeconds 5.0)
                    release.Set()
                    Task.WhenAll(tasks).GetAwaiter().GetResult()
                    Expect.isTrue allEntered "unrelated factories can all enter before any completes"
            for representation in ["array"; "pair"] do
                testCase $"factory exceptions allow retry/{representation}" <| fun _ ->
                    let error = InvalidOperationException("factory probe")
                    let mutable fail = true
                    let value = FactoryProbe(trafos 1, fun () -> if fail then raise error)
                    let lookup() = if representation = "array" then box (C.bufferOfArray value) else box (C.buffersOfTrafos value)
                    let thrown = try lookup() |> ignore; None with e -> Some e
                    Expect.isTrue (thrown |> Option.exists (fun e -> obj.ReferenceEquals(e, error))) "original failure"
                    fail <- false
                    let result = lookup()
                    same result (lookup()) "retry publishes a reusable result"
            for constant in [false; true] do
                for representation in ["array"; "pair"] do
                    testCase $"warmed lookup allocation/{representation}/constant={constant}" <| fun _ ->
                        let value = source constant (trafos 1)
                        let run = if representation = "array" then fun () -> GC.KeepAlive(C.bufferOfArray value) else fun () -> GC.KeepAlive(C.buffersOfTrafos value)
                        for _ in 1 .. 100 do run()
                        let before = GC.GetAllocatedBytesForCurrentThread()
                        for _ in 1 .. 10000 do run()
                        let allocated = GC.GetAllocatedBytesForCurrentThread() - before
                        Expect.equal allocated 0L "zero allocation on warm cache hits"
            for representation in ["array"; "pair"] do
                testCase $"live source retains its published views/{representation}" <| fun _ ->
                    let value = cval (trafos 1)
                    let views = weakViews value representation
                    for _ in 1 .. 3 do collect()
                    Expect.isTrue views.IsAlive "values stay published while the key is alive"
                    let actual = if representation = "array" then box (C.bufferOfArray value) else box (C.buffersOfTrafos value)
                    same views.Target actual "live-key identity survives GC"
                    GC.KeepAlive value
            for constant in [false; true] do
                for representation in ["array"; "pair"; "mixed"] do
                    for force in [false; true] do
                        testCase $"unreachable source and views collected/{representation}/constant={constant}/forced={force}" <| fun _ ->
                            let live = cval [|17|]
                            let liveView = C.bufferOfArray live
                            let refs = unreachable constant representation force
                            for _ in 1 .. 5 do collect()
                            Expect.isTrue (refs |> Array.forall (fun r -> not r.IsAlive)) "weak cache does not retain source/view cycles"
                            same liveView (C.bufferOfArray live) "unrelated live entry remains usable"
                            GC.KeepAlive live
        ] |> testSequenced
