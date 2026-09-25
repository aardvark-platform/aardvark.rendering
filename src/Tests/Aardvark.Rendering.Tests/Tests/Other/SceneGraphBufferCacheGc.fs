// Standalone, opt-in diagnostic. Not compiled into or discovered by the unit-test project.
module SceneGraphBufferCacheGc

open System
open System.Collections
open System.Reflection
open System.Runtime
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Text.Json
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive

module C = SgFSharpHelpers.Caching

// Changing the phantom tag changes the cache's element type, not its layout or object graph.
type private Tag<'T>() = class end
[<Struct>]
type private Element<'Tag> =
    val Value : int
    new(value) = { Value = value }

type private Sources =
    static member Add<'Tag>(roots : IAdaptiveValue[], first : int, count : int) =
        if sizeof<Element<'Tag>> <> 4 then failwith "Unexpected element layout"
        for i in first .. first + count - 1 do
            let source = cval [|Element<'Tag>(i)|]
            let view = C.bufferOfArray source
            if view.ElementType <> typeof<Element<'Tag>> || not (obj.ReferenceEquals(view, C.bufferOfArray source)) then
                failwith "Ordinary view was not canonically published"
            roots.[i] <- source

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private populate entries typeCount pairCount =
    let roots = Array.zeroCreate<IAdaptiveValue> entries
    let mutable tag = typeof<int>
    let tags = Array.init typeCount (fun _ ->
        let current = tag
        tag <- typedefof<Tag<_>>.MakeGenericType tag
        current)
    let ordinary = entries - pairCount
    let add = typeof<Sources>.GetMethod("Add", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    for i in 0 .. typeCount - 1 do
        let first = i * ordinary / typeCount
        let count = (i + 1) * ordinary / typeCount - first
        add.MakeGenericMethod(tags.[i]).Invoke(null, [| roots; first; count |]) |> ignore
    for i in ordinary .. entries - 1 do
        let source = cval [|Trafo3d.Translation(float i, 0.0, 0.0)|]
        let views = C.buffersOfTrafos source
        if not (obj.ReferenceEquals(views, C.buffersOfTrafos source)) then
            failwith "Transform pair was not canonically published"
        roots.[i] <- source
    roots, tags |> Array.map (fun tag -> typedefof<Element<_>>.MakeGenericType tag)

// Inspect only BufferView caches; do not count unrelated scene-graph weak tables.
let private tables (elementTypes : Type[]) =
    typeof<ISg>.Assembly.GetTypes()
    |> Array.collect (fun t ->
        if t.IsGenericTypeDefinition && t.Name.StartsWith("ArrayCache`") then
            elementTypes |> Array.map (fun e -> t.MakeGenericType e)
        elif t.ContainsGenericParameters then [||]
        else [|t|])
    |> Array.collect (fun t -> t.GetFields(BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic))
    |> Array.filter (fun f ->
        f.FieldType.IsGenericType &&
        f.FieldType.GetGenericTypeDefinition() = typedefof<ConditionalWeakTable<_,_>> &&
        let valueType = f.FieldType.GenericTypeArguments.[1]
        valueType = typeof<BufferView> || valueType = typeof<BufferView * BufferView>)
    |> Array.map (fun f -> f.GetValue null)
    |> Array.distinct

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private population (tables : obj[]) =
    let mutable ordinary = 0
    let mutable pairs = 0
    for table in tables do
        let mutable count = 0
        for _ in table :?> IEnumerable do count <- count + 1
        if table.GetType().GenericTypeArguments.[1] = typeof<BufferView> then ordinary <- ordinary + count
        else pairs <- pairs + count
    ordinary, pairs

[<Struct>]
type private Sample = { Generation : int; Induced : bool; PauseMs : float }

let private collect() =
    GC.Collect(2, GCCollectionMode.Forced, true, true)
    GC.WaitForPendingFinalizers()
    GC.Collect(2, GCCollectionMode.Forced, true, true)

let private counts() = [| for generation in 0 .. 2 -> GC.CollectionCount generation |]

let private measure() =
    // Preallocate the observer; neither its buffer nor its reporting allocations belong to the workload.
    let samples = Array.zeroCreate<Sample> 16384
    let mutable used = 0
    let mutable missed = 0L
    let mutable observerBytes = 0L
    let mutable lastCount = GC.CollectionCount 0
    let mutable lastIndex = (GC.GetGCMemoryInfo()).Index
    let observe induced =
        // GetGCMemoryInfo itself allocates. Call it only after a collection, and account for that cost.
        if GC.CollectionCount 0 <> lastCount then
            let before = GC.GetAllocatedBytesForCurrentThread()
            let info = GC.GetGCMemoryInfo()
            observerBytes <- observerBytes + GC.GetAllocatedBytesForCurrentThread() - before
            let pauses = info.PauseDurations
            if info.Concurrent || pauses.Length <> 2 || pauses.[1] <> TimeSpan.Zero then
                failwith "This diagnostic requires one blocking pause per collection"
            if used = samples.Length then failwith "Observation buffer exhausted"
            missed <- missed + max 0L (info.Index - lastIndex - 1L)
            samples.[used] <- { Generation = info.Generation; Induced = induced; PauseMs = pauses.[0].TotalMilliseconds }
            used <- used + 1
            lastIndex <- info.Index
            lastCount <- GC.CollectionCount 0
    let run rounds =
        for round in 0 .. rounds - 1 do
            for _ in 1 .. 8192 do // 64 MiB payload per round, all below the LOH threshold.
                let transient = Array.zeroCreate<byte> 8192
                GC.KeepAlive transient
                observe false
            // Controlled coverage of every generation, reported separately from automatic collections.
            GC.Collect(round % 3, GCCollectionMode.Forced, true, false)
            observe true

    run 3 // Warm the workload, observer, and every generation before measuring.
    collect()
    used <- 0
    missed <- 0L
    observerBytes <- 0L
    lastCount <- GC.CollectionCount 0
    lastIndex <- (GC.GetGCMemoryInfo()).Index
    let startIndex = lastIndex
    let startCounts = counts()
    let before = GC.GetAllocatedBytesForCurrentThread()
    run 24 // Exactly 1,536 MiB of transient payload and eight induced collections per generation.
    let allocated = GC.GetAllocatedBytesForCurrentThread() - before
    let endCounts = counts()
    let inclusive = Array.map2 (-) endCounts startCounts
    let expected = [| inclusive.[0] - inclusive.[1]; inclusive.[1] - inclusive.[2]; inclusive.[2] |]
    let samples = samples.[0 .. used - 1]
    let observed = Array.init 3 (fun g -> samples |> Array.sumBy (fun s -> if s.Generation = g then 1 else 0))
    let valid = missed = 0L && expected = observed && lastIndex - startIndex = int64 used
    let groups = [|
        for induced in [false; true] do
            for generation in 0 .. 2 do
                let values = samples |> Array.choose (fun s -> if s.Generation = generation && s.Induced = induced then Some s.PauseMs else None)
                Array.sortInPlace values
                let n = values.Length
                let metric f = if n = 0 then Nullable<float>() else Nullable(f())
                yield {| Trigger = if induced then "induced" else "automatic"
                         Generation = generation; Collections = n
                         MedianMs = metric (fun () -> (values.[(n - 1) / 2] + values.[n / 2]) / 2.0)
                         P95Ms = metric (fun () -> values.[int (ceil (0.95 * float n)) - 1])
                         MaximumMs = metric (fun () -> values.[n - 1]) |}
    |]
    {| Valid = valid; StartIndex = startIndex; EndIndex = lastIndex; MissedIndices = missed
       ExpectedCollectionsByGeneration = expected; ObservedCollectionsByGeneration = observed
       TransientPayloadBytes = 1536L * 1024L * 1024L; TransientObjects = 24 * 8192
       AllocatedBytes = allocated; ObserverAllocatedBytes = observerBytes; Groups = groups |}

[<EntryPoint>]
let main args =
    match args with
    | [| "--entries"; entries; "--types"; types |] when
        (entries = "100000" || entries = "500000") && (types = "1" || types = "8" || types = "32") ->
        if GCSettings.IsServerGC then failwith "Use workstation GC (DOTNET_gcServer=0)"
        GCSettings.LatencyMode <- GCLatencyMode.Batch
        let entries, typeCount, pairs = int entries, int types, 10000
        let roots, elementTypes = populate entries typeCount pairs
        let tables = tables elementTypes
        let shared = tables |> Array.exists (fun t -> t.GetType().GenericTypeArguments.[0] = typeof<IAdaptiveValue>)
        if tables.Length <> (if shared then 2 else typeCount + 1) then failwith "Unexpected cache table count"
        collect()
        let liveBefore = population tables
        if liveBefore <> (entries - pairs, pairs) then failwith "Unexpected populated entry counts"
        collect() // Remove enumeration garbage and retire old table containers before the workload.
        let result = measure()
        let liveAfter = population tables
        if liveAfter <> liveBefore then failwith "A rooted entry was lost"
        GC.KeepAlive roots
        let report =
            {| Variant = if shared then "two-table" else "per-element"
               Runtime = RuntimeInformation.FrameworkDescription
               Architecture = string RuntimeInformation.ProcessArchitecture
               ServerGC = GCSettings.IsServerGC; LatencyMode = string GCSettings.LatencyMode
               Entries = entries; OrdinaryTypes = typeCount; TransformPairs = pairs
               Tables = tables.Length; LiveBefore = fst liveBefore + snd liveBefore; LiveAfter = fst liveAfter + snd liveAfter
               Measurement = result |}
        printfn "%s" (JsonSerializer.Serialize report)
        if result.Valid then 0 else 2
    | _ ->
        eprintfn "Usage: SceneGraphBufferCacheGc --entries 100000|500000 --types 1|8|32"
        1
