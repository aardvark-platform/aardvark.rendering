# Scene-graph buffer-cache GC diagnostic

Opt-in, CPU-only, .NET 8. This standalone project is **not** part of the test project or normal test execution. It measures actual GC pauses, not lookup latency. Results and the design decision belong in [PR #181](https://github.com/aardvark-platform/aardvark.rendering/pull/181), not in this document.

## Build and run

Restore dependencies with `dotnet tool restore && dotnet paket restore`, then:

```sh
dotnet build src/Tests/Aardvark.Rendering.Tests/Tests/Other/SceneGraphBufferCacheGc.fsproj -c Release -warnaserror
```

Run each case in a fresh process with the same environment:

```sh
DOTNET_TieredCompilation=0 DOTNET_gcServer=0 DOTNET_gcConcurrent=0 \
  dotnet bin/SceneGraphBufferCacheGc/Release/net8.0/SceneGraphBufferCacheGc.dll --entries 100000 --types 8
```

On Linux, optionally put `taskset -c <same-CPU>` before `dotnet` for **every** trial. Do not run builds or other benchmarks alongside the measurements. Supported entry counts are 100,000 and 500,000; supported ordinary-array type counts are 1, 8 and 32. Run **one case and one implementation per fresh process**, never load both implementations in the same process.

## Source comparisons

The published per-element cache is `8b570f6f`. The experimental two-table source is local revision `670dd227`, **not an approved production replacement**. Use detached worktrees with identical dependencies, diagnostic source, compiler, runtime, build options and environment. Copy these three diagnostic files into older worktrees, restore with Paket, and build each worktree from source. Do not swap DLLs.

If the local experimental revision is unavailable, recreate its ordinary-array change in a detached copy of `8b570f6f`: remove `ArrayCache<'T>` and its table, add the following alongside the unchanged transform-pair table/factory, and replace `bufferOfArray` as shown. Leave `buffersOfTrafos` and the rest of the file unchanged.

```fsharp
let private arrayCache = ConditionalWeakTable<IAdaptiveValue, BufferView>()

type private ArrayFactory<'T>() =
    static let create =
        ConditionalWeakTable<IAdaptiveValue, BufferView>.CreateValueCallback(fun value ->
            let value = value :?> aval<'T[]>
            let b = value |> AVal.map (fun a -> ArrayBuffer a :> IBuffer)
            BufferView(b, typeof<'T>)
        )
    static member Create = create

let bufferOfArray (value: aval<'T[]>) : BufferView =
    arrayCache.GetValue(value, ArrayFactory<'T>.Create)
```

For the measured comparison, dependency and diagnostic DLLs were byte-identical; only the source-built SceneGraph DLL differed. To reproduce that stricter control, build the baseline first. In the temporary candidate worktree, point SceneGraph's `ProjectReference` for `Aardvark.Rendering` at the baseline's Rendering project. Then build the candidate SceneGraph project and diagnostic project in that order with `-p:BuildProjectReferences=false`. This reuses the same source-built dependencies, not the other SceneGraph implementation. Do correctness rebuilds separately, with each version's normal project references.

Use these flags for both diagnostic builds, from each worktree's root, to normalize revision/path metadata and omit differing PDB checksums:

```sh
-c Release -warnaserror -p:EnableSourceControlManagerQueries=false \
  -p:IncludeSourceRevisionInInformationalVersion=false -p:DebugType=None \
  -p:DebugSymbols=false "-p:PathMap=$PWD=/_/"
```

Verify dependency/driver hashes and runtime-config equality between outputs, and check the JSON `Variant` and table/entry counts rather than trusting directory labels.

For each of the six entry/type combinations, use at least four paired trials. Alternate both workload order and process order. A balanced schedule per case is:

- Odd trials: before, after, control-A, control-B.
- Even trials: control-B, control-A, after, before; reverse the case order too.

Both control labels run the **same per-element executable in separate fresh processes**. Retain all valid trials, including unfavorable results. Report process-level paired comparisons separately from ratios of independently summarized medians. Runtime scheduling and thermal variation still apply.

## Controlled workload and accounting

- Total live entries include **10,000 transform-pair entries** in every case. The remaining entries are ordinary arrays, distributed evenly over the requested types.
- Every key is a separately allocated changeable adaptive source, explicitly rooted until after measurement. Every source holds a one-element array. Phantom-tagged four-byte structs vary the ordinary element type without varying element size or the source/view reference graph. Matrix buffers remain unevaluated.
- Populate through the production helpers and verify canonical publication. Reflect only the BufferView caches to check table counts and separate ordinary/pair populations before and after measurement. The expected table counts are types + 1 versus exactly 2. This inspection deliberately targets these two implementations and fails if their shape changes.
- Initialization, reflection, population checks, retirement of old weak-table containers, and a three-round workload/JIT warm-up are excluded. Sources remain rooted; views are retained by the weak tables, not by a separate strong value array.
- Measured work is **24 rounds × 64 MiB = 1,536 MiB of byte-array payload**: 196,608 temporary 8,192-byte arrays. After each round, request a blocking collection of generation `round % 3`, giving eight induced collections per generation. Automatic and induced pauses are reported **separately**; this is not a claim about production collection frequency.
- Workstation GC in `Batch` mode disables concurrent collection. After each allocation, check the collection counter without allocating; query `GC.GetGCMemoryInfo().PauseDurations` only when the count changes. Record the actual collection generation and index, not the requested generation or wall-clock lookup duration. Reject concurrent/multiple-pause records.
- A fixed observer buffer is allocated before measurement. `GetGCMemoryInfo` allocation is measured separately as `ObserverAllocatedBytes`; `AllocatedBytes` counts current-thread allocations, including it and array headers. The explicit transient workload is identical across variants.
- Compare index gaps and observed per-generation counts against deltas of the inclusive `GC.CollectionCount` counters. `Valid=false` / exit code 2 means observations were missed or accounting disagreed: disclose the limitation, and do not treat the pause distribution as complete. Buffer exhaustion and incorrect cache population fail the run.

The single JSON report contains counts and per-generation pause **median, nearest-rank p95, and maximum in milliseconds** for each trigger. Empty groups have null durations. Raw pause samples stay in memory and are not printed. When aggregating processes, distinguish the median of per-process medians/p95s from a pooled pause percentile; take the maximum over all process maxima and report collection counts. Eight induced collections per generation per process provide only limited tail evidence.

This bounded, rooted, deferred-buffer workload isolates steady-state scanning under transient allocation. It does **not** measure dead-key churn, forced buffer evaluation, background/server GC, frame times, lookup throughput, or every application's object graph. Table count alone is not evidence of a net performance improvement.

## Correctness controls remain separate

Rebuild the existing test project in each source worktree and configuration, then select through VSTest:

```sh
dotnet build src/Tests/Aardvark.Rendering.Tests/Aardvark.Rendering.Tests.fsproj -c Release -t:Rebuild -warnaserror
dotnet vstest bin/Release/net8.0/Aardvark.Rendering.Tests.dll --ListTests
dotnet vstest bin/Release/net8.0/Aardvark.Rendering.Tests.dll \
  '--Tests:SceneGraph buffer cache' \
  --Settings:src/Tests/Aardvark.Rendering.Tests/test.runsettings
```

Repeat with Debug and confirm exactly 65 cases. For the negative control, use another detached copy with only `SgFSharp.fs` restored from original `05cfb9d1`, rebuild the tests, and select `SceneGraph buffer cache.public sharing`. All 12 cases must fail with the original duplicate-key exception (both call orders, constant/changeable sources, lengths 0/1/7); those are **expected baseline failures**, not candidate validation failures. Reuse these fixtures unchanged. The executable's manual runner and `dotnet test --filter` do not provide reliable selection here.

Publish only concise aggregate evidence. Delete temporary reports/logs after summarization unless retention through an approved artifact store was explicitly requested.
