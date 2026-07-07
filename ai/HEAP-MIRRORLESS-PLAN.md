# Heap: Mirror-less Arena + O(1) Allocator — Implementation Plan

Status: **PHASES 0–2 IMPLEMENTED, VERIFIED & COMMITTED (a6dee196 on `v57`, shipping since ~0038).**
Measured @700k renderbench: CPU ingest 30.6 → 18.8 s (26.9 µs/part), upload
2.6 → 0.7 s (1.5 GB @ ~2 GB/s), GPU render 13.91 ms (1.50×) unchanged; all
golden suites + 21 heap tests + NEW `churn` suite (HeapSpike `churn`: compaction
bounce, same-cycle block reuse, shrink/regrow — pixel-identical to classic) pass.
Implementation notes vs the plan: (a) ring-growth memcpy out of MAPPED memory is
catastrophic (write-combined readback, first attempt cost 113 s) — the ring is a
CHAIN of chunks, never copied; (b) allocator = `HeapSpace` rewritten in place
(segregated quarter-pow2 stacks + bump tail, own `HeapBlock`, EXACT-size splits —
`entryAligned` depends on block.Size == requested); (c) attr/idx paths force the
buffer aval once (dedup key + len + copy share one GetValue). Remaining ingest
time is DIFFUSE (dicts, adaptive forces, per-entry allocs) — no single lever
left; next big step would be parallel ingest (out of scope). Phase 3 leftovers
(TrafoArena staging, csStaging ring) remain open and low-value.
Goal of this doc: everything a fresh session needs to implement the mirror-less
heap arena without re-deriving the analysis.

## 1. Goal

Remove the CPU-side `float32[]` staging mirror of every `HeapArena` page and make
ingest/edit go **source → (mapped ring) → GPU** with the minimum number of copies,
while replacing the per-allocation best-fit `Management.MemoryManager` cost with an
O(1)-amortized allocator.

Hard constraints (user decisions — do not violate):

1. **Mirror-less**: no persistent host copy of arena payload. (Small *bookkeeping*
   mirrors like the cluster `csStaging : int[]` slot table are fine — that is state,
   not payload.)
2. **O(changed) amortized everywhere.** No operation may be O(live) unless it is
   amortized against Ω(live) prior changes (compaction is the model citizen).
3. **No cold-start price on the first edit.** Explicitly forbidden: a "frozen bulk
   ingest mode" whose allocator/bookkeeping state is materialized lazily when the
   first mutation arrives. All structures must be built incrementally during ingest
   at negligible per-part cost.
4. Everything else stands: no per-bucket shader specialization, GL heap path is
   known-broken (keep it compiling, don't invest), MoltenVK must keep working.

## 2. Measured baseline (2026-07-05, RTX 5060, renderbench @700k / Vienna @1.2M)

- GPU render: renderbench 13.86 ms (1.50× vs baked soup); Vienna 36.6 ms/frame.
  **Render is done — this effort is about ingest time and host RAM.**
- Ingest @700k: CPU ~30.6 s (~43.7 µs/part), GPU upload 2.6 s / 1.5 GB (post-0037
  gap-merge fix). Vienna total ingest 62 s.
- Instrumented split (uncommitted stopwatch buckets in `AddInternal`, still in the
  working tree; 300k parts): **geometry 63% | fields/constituents 30% | rest 7%**
  — printed as `[startup] ingest N parts so far: … (fields | geom | rest)`.
- Copies today: every attribute travels **three** copies —
  `readBytesView` (`Marshal.Copy` source→fresh `byte[]`) → `WriteStaticBytes`
  (`Buffer.BlockCopy` byte[]→staging mirror) → `AdaptiveBuffer.Write`
  (staging→backend upload path). Plus ~2.8M transient `byte[]`s of GC pressure.
- The copy itself is only ~2 µs of the 21 µs/part geometry bucket; the rest is
  `Management` SortedSet allocs, dedup dict ops, closure/`GCHandle` overhead in the
  `StageOnce` packers. **The allocator rework matters as much as the copy path.**
- Per-`Write` runtime call costs ~60 µs (measured in the 0037 upload-storm fix:
  300k Writes = 68 s). Any design that issues one runtime call per part is dead on
  arrival; batching is mandatory.

## 3. Inventory: everything that touches the mirror

All in `src/Aardvark.SceneGraph/HeapPool.fs` (line numbers at 0037):

| Touch point | ~Line | What it does | Mirror-less replacement |
|---|---|---|---|
| `HeapArena.staging` field | 959 | the mirror | delete |
| `EnsureFloats` / `ShrinkFloats` | 967/984 | grow/shrink mirror, GPU resize deferred to Compute | keep only the deferred-GPU-resize bookkeeping (capacity int) |
| `MoveStaging` + `RequestFullUpload` | 982/995 | compaction: CPU memmove + full re-upload | GPU device→device region copies (see §5.4) |
| `WriteHeader` | 1000 | 4-word alloc header into mirror | write into upload ring |
| `WriteStaticBytes` | 1007 | geometry/index bytes into mirror (via intermediate `byte[]` from `readBytesView`, line ~731) | **direct source-ptr → ring copy**, no `byte[]` |
| `StageOnce` | 1017 | constant uniform/constituent packs into mirror | pack into ring (pointer-writer variants of the packers) |
| `RegionWriter.Pack(t, staging)` | 940/1061 | adaptive region re-packs (camera, dynamic trafos) | pack into ring; already O(changed) |
| `Compute` flush | 1043–1090 | coalesce dirty ranges, `x.Write(staging, …)` per run, 4 KB gap-tolerant merge | multi-region buffer copy from ring (see §5.3) — the gap-merge trick dies with the mirror, region batching replaces it |
| deferred `ResizeInPlace` | 1055 | GPU resize from `capacity` | unchanged — see §4: already content-preserving on GPU |
| Compaction caller (`PageArena.Compact`, uses `MoveStaging`) | ~2688 | re-seat surviving blocks | emit GPU copy-region list instead |
| `TrafoArena`-style `staging : M44d[]` / `stagingDf` | ~1991 | derived-uniform input staging | same ring treatment (separate, smaller; can be a later phase) |
| Cluster `csStaging : int[]` | ~3107 | ClassSlots CPU table | **keep** — it is authoritative bookkeeping (swap-remove needs to read it), 4 B/slot |

Nothing ever *reads* arena payload back from the mirror except `MoveStaging`
(compaction) and the gap-merge re-upload. No download path exists. Derive-output
regions (GPU-written fp64/df32 results) are already never staged — mirror-less is
*natural* for them.

## 4a. PHASE 0 RECON RESULTS (2026-07-05 — verified in Vulkan backend source)

- **Sync semantics**: `device.perform` = `t.Sync(); t.Dispose()`
  (`src/Aardvark.Rendering.Vulkan/Core/Commands.fs:341`) — `runtime.Copy` and
  `runtime.Upload` to device buffers BLOCK until the GPU copy completes. Ring
  reuse after flush needs no fences.
- **The 60 µs/Write explained** (`Buffer.upload`, Vulkan `Buffer.fs:505`): each
  call to a device-local dst CREATES A FRESH staging Buffer (vkCreateBuffer +
  VMA alloc), memcpys into its mapping, `device.perform` (submit + fence wait),
  destroys it. Never call per-part.
- **VMA host allocations are persistently mapped** (`DevicePtr.fs:90` — uses
  `pMappedData`; `Mapped` = pointer + `Vma.flushAllocation`, no-op on coherent).
  A `BufferStorage.Host` buffer already has a persistent mapping internally;
  `runtime.Upload` to it is a plain memcpy, NO submit.
- **Multi-region copy template exists**: `Command.Copy(src, dst, ranges : Range1l[])`
  (Vulkan `Buffer.fs:180`) — one `vkCmdCopyBuffer` with N `VkBufferCopy`. It uses
  same src/dst offsets; a `(srcOff, dstOff, size)[]` variant is a 15-line addition.
- **ResizeInPlace confirmed device-side**: sets `size`; next `ComputeHandle`
  creates the new buffer, `runtime.Copy(old, 0, new, 0, min)` on device, deletes
  old (`AdaptiveBuffer.fs:176-186`). No host round-trip.
- **PLAN CORRECTION 1 — multi-region copy moves to Phase 1.** Without the mirror
  there is nothing to fill derive-region holes with, and (nearly) every part has
  a derive hole in its uniform region → arena-contiguity merging degenerates to
  ~2 runs/part → 60 µs each = the 0037 disaster again. The flush MUST be one
  multi-region ring→arena copy. Ergo the IBufferRuntime additions are Phase 1
  prerequisites, not Phase 3 polish.
- **PLAN CORRECTION 2 — ring peak size.** Every byte staged between two flushes
  must sit in the ring, so bulk ingest peaks at ~payload size (1.5 GB Vienna),
  host-visible. Grow by doubling, SHRINK back to steady-state (64 MB) after
  flush. Transient ≠ persistent mirror; constraint 1 is about steady-state RAM.
- **PLAN CORRECTION 3 — compaction bounces through a temp buffer.** The arena's
  handle is private to AdaptiveBuffer and same-buffer overlapping copies are UB:
  compact = CreateBuffer(temp, packedSize) → multi-region Copy(arena→temp,
  collapsing) → Copy(temp→arena, linear) → delete temp → ResizeInPlace shrink
  (linear old→new copy preserves the packed prefix). 2× GPU-GPU traffic on the
  moved bytes — negligible.

## 4. Runtime API facts (verified in source)

- `IBufferRuntime.Copy(src, srcOff, dst, dstOff, size, discard)` exists
  (`src/Aardvark.Rendering/Resources/Buffers/Buffers.fs:143`) — device→device.
- `AdaptiveBuffer.ResizeInPlace` already preserves content via
  `runtime.Copy(old, 0, resized, 0, min oldSize newSize)`
  (`src/Aardvark.Rendering/Resources/Adaptive/AdaptiveBuffer.fs:182`). **Resize
  needs no mirror today.** Verify the copy path is used on the Vulkan backend and
  does not round-trip host memory.
- `IBufferRuntime.Upload(src : nativeint, dst, dstOffset, size)` exists — host-ptr
  upload, no managed array needed.
- `CreateBuffer(size, usage, storage)` takes `BufferStorage.Host` — host-visible
  backend buffer.
- **Missing** from the abstraction (must be added in this repo, we own it):
  1. a way to get a **persistent mapping** of a Host buffer (Vulkan:
     HOST_VISIBLE|COHERENT, `vkMapMemory` once), e.g.
     `IBufferRuntime.TryMapBuffer : IBackendBuffer -> Option<nativeint>` or a
     dedicated `IMappedBuffer`;
  2. a **multi-region copy**:
     `Copy(src : IBackendBuffer, dst : IBackendBuffer, regions : (srcOff * dstOff * size)[])`
     that records ONE `vkCmdCopyBuffer` with N `VkBufferCopy` regions (per-region
     cost ~ns, vs ~60 µs per separate runtime call). GL implementation: loop of
     `glCopyBufferSubData` (correctness only; GL heap is non-goal).

  Fallback if (1) turns out ugly: a pinned managed ring array + `Upload(ptr, …)`
  per contiguous run gives most of the win (kills the mirror + one copy) without
  new mapping API — acceptable Phase-1 shape, see §6.

## 5. Target architecture

### 5.1 Upload ring

Per `HeapStorage` (shared across its pages): one persistently-mapped host-visible
ring buffer (start 64 MB, grow by doubling; growth allocates a new ring, old one
retires after its in-flight copies complete). All writes that used to hit the
mirror become:

```
allocate ringCursor span  ->  copy/pack bytes at ringPtr+cursor  ->  append region (ringOff, arenaOff, words)
```

- Geometry: `INativeBuffer.Use(fun ptr -> memcpy(ringPtr+cur, ptr+viewOffset, len))`
  — ONE copy, zero managed arrays. (`readBytesView` and its `byte[]` die.)
- Headers / StageOnce packs: pointer-writer variants (`NativePtr.write` /
  `Marshal.WriteInt32`-style) of the existing pack helpers. This also kills the
  per-part closure + `GCHandle` pinning overhead in the fields bucket (30% of
  ingest) — precompile pack delegates per layout where possible.
- `RegionWriter.Pack` gets a `nativeint` overload; dirty adaptive regions pack
  into the ring each flush exactly like today into the mirror.

Ring lifetime rule: a ring span may be reused only after the copy command reading
it has completed. **Verify first** whether `runtime.Copy`/render-eval is
synchronous on the Vulkan backend (the existing upload path suggests writes are
complete when Compute returns). If synchronous: trivial (reset cursor per flush).
If async: fence-tagged ring segments (classic double/triple buffer).

### 5.2 Flush = one multi-region copy per page

`HeapArena.Compute` collects the region list appended since the last flush
(plus dirty `RegionWriter` re-packs), sorts by `arenaOff`, merges regions that are
contiguous in **both** ring and arena, then issues ONE multi-region
`Copy(ring, arena, regions)` per page. Bulk ingest writes parts back-to-back in
both spaces, so bulk flush ≈ a handful of regions even with derive-output holes —
the 0037 4 KB gap-merge hack (which relied on "mirror is authoritative, gap bytes
harmless") is no longer needed and must be removed with the mirror.

### 5.3 Resize

Unchanged mechanism: deferred `ResizeInPlace` in Compute — already a GPU
device→device copy (§4). Only the mirror-grow (`EnsureFloats` array copy)
disappears. Shrink likewise.

### 5.4 Compaction on the GPU

`PageArena.Compact` today: compute new packed layout → `MoveStaging` per block →
`RequestFullUpload`. Mirror-less version:

- compute the same packed layout (pure bookkeeping, unchanged);
- surviving blocks preserve order, so consecutive survivors form runs — emit one
  copy region per *run* (= number of dead gaps + 1, typically small), NOT per block;
- copy **old buffer → new buffer** (allocate at packed size, copy runs, swap, retire
  old after fence). Same-buffer overlapping `vkCmdCopyBuffer` regions are undefined
  behavior in Vulkan — never move within one buffer.
- header rewrites (RewriteHeaders participants) go through the normal ring path.
- Amortization argument stays as-is: compaction runs only after Ω(live) frees.

### 5.5 Allocator: segregated free lists + bump tail (replaces per-alloc best-fit)

Replace the page's `Management.MemoryManager` (SortedSet best-fit, ~µs per op,
the single biggest non-copy cost) with, per page:

- `tail : int` — bump pointer over virgin space. Alloc miss → `tail`, `tail += size`.
- `freeBuckets : Stack<int>[]` — segregated by size class. **Reuse the cluster
  ladder** (`clusterClassSizes`, ~51 classes, 9/8 steps) or plain pow2-with-quarter
  steps; exactness is not required because blocks are split on reuse.
- `Alloc(size)`: probe the smallest bucket whose class ≥ size; on hit, split the
  remainder back into its bucket (remainder < smallest class → count it as waste,
  it is reclaimed by compaction); on miss, bump.
- `Free(off, size)`: push onto the class bucket. O(1). Track `deadWords`; when
  `deadWords / tail` exceeds the existing compaction threshold, trigger §5.4.
- **Cooperation answer**: the bump pointer is not a separate "ingest mode" — it is
  the miss path of the one allocator that exists from page creation. Churn frees
  feed buckets; later ingest reuses them; fragmentation is bounded by compaction.
  No mode switch → no cold-start conversion → constraint 3 satisfied by
  construction. Empty bucket array at page creation costs ~nothing.
- Keep the existing `Block`/alignment invariants (`entryAligned`, 4-word header
  granularity). The allocator swap must be invisible to `PageArena.Compact`'s
  resident collection.

### 5.6 What stays host-side (explicitly OK)

- `csStaging : int[]` (ClassSlots) — bookkeeping, needed for swap-remove reads.
  Its upload can optionally move to the ring later; low value.
- Dedup dictionaries, header cell values (`headers : int[]` slot table), block
  bookkeeping — all state, not payload.
- `TrafoArena` M44d staging — payload technically, but small and rewritten per
  change; migrate in a later phase via the same ring.

## 6. Phases (each shippable + measured; gates in §7)

**Phase 0 — recon: DONE, see §4a.** All three questions answered; two plan
corrections folded into the phases below.

**Phase 1 — mirror-less arena (API additions + ring + flush + compaction):**
1. `IBufferRuntime` additions (this repo owns it):
   `[<Struct>] BufferCopyRegion = { SrcOffset : uint64; DstOffset : uint64; SizeInBytes : uint64 }`;
   `Copy(src, dst, regions : BufferCopyRegion[])` (Vulkan: one vkCmdCopyBuffer +
   perform; GL: glCopyBufferSubData loop); `TryGetMappedPointer : IBackendBuffer -> nativeint voption`
   (Vulkan: `pMappedData` when host-visible; GL: ValueNone).
2. `UploadRing` in HeapPool: mapped `BufferStorage.Host` buffer (fallback: pinned
   `byte[]` + per-run `Upload` when no mapping — GL), doubling growth, shrink to
   64 MB after flush, cursor + region list `(ringOff, arenaOff, words)`.
3. `HeapArena` rewrite: `WriteHeader`/`WriteStaticBytes`/`StageOnce`/`RegionWriter.Pack`
   target ring pointers (geometry = source-ptr → ring memcpy, `readBytesView` +
   its `byte[]` die; pointer-writer pack delegates de-closure the fields bucket);
   `Compute` = base resize first, then ONE multi-region Copy(ring → handle) per
   page; mirror + gap-merge + `RequestFullUpload`/`MoveStaging` deleted.
4. Compaction per §4a correction 3 (temp-buffer bounce).
Expected: geometry copies 3→1, GC storm gone, host RAM −1.5 GB steady-state @
Vienna, upload runs collapse to 1 command/page.

**Phase 2 — allocator swap (§5.5):** biggest CPU win. Pure
`HeapPool.fs`-internal. Measure the `[startup]` split before/after.

**Phase 3 — leftovers:** `TrafoArena` staging onto the ring; optional `csStaging`
upload path; remove the then-dead gap-merge code and `RequestFullUpload`.

Projected endpoint (single-threaded): ~43.7 → ~15–20 µs/part ⇒ 700k in ~10–13 s,
Vienna ingest ~25–35 s. Parallel payload copies remain available on top later
(ring spans are naturally parallel-writable), but are OUT OF SCOPE here.

## 7. Verification gates (run after every phase)

- Golden suite: `src/Examples (netcore)/40 - HeapSpike` — `golden` (all cases incl.
  bucketing/modeRules/passthrough), `deferred` (incl. `shared-storage`), `atlasheap`.
- Vulkan heap tests: `Aardvark.Rendering.Tests` HeapUniforms group (incl.
  "Singleton via length-1 buffer", heterogeneousGeometry with the oversized mesh).
  Reminder: `--filter` needs `runManuallyInMain=false` or it silently runs everything.
- `renderbench --n 700000`: GPU ms must hold at ~13.9 (render is untouched — any
  regression means a broken upload), ingest split printed by the `[startup]` lines.
- Churn correctness: no existing test exercises heavy add/remove→compaction→re-ingest
  against the new allocator. **Write one** (golden-style: build scene, remove 60%,
  add new geometry until compaction fires, compare image + assert allocator
  invariants). This is the riskiest surface of the whole plan.
- Vienna (CadSceneDemo) before/after per phase — check the box is free first
  (`pgrep -af CadSceneDemo; nvidia-smi`), contention invalidates numbers.
- Builds need `dangerouslyDisableSandbox: true` (fsc segfaults in the sandbox).

## 8. Known traps

- Per-runtime-call overhead ~60 µs — never issue per-part calls; batch always.
- Vulkan same-buffer copy overlap is UB — compaction copies into a NEW buffer.
- Ring reuse before copy completion — resolve sync semantics in Phase 0.
- `HeapArena` growth/shrink must stay rule-clean (no transact inside adaptive
  evaluation) — the deferred-resize-in-Compute pattern must be preserved.
- Derive-output regions are GPU-written and must NEVER be covered by an upload
  region (with the mirror gone, an accidental overlap uploads garbage over
  computed fp64 data — today's gap-merge deliberately overwrote them harmlessly
  because derive re-runs; that safety net disappears).
- Sampler/atlas/cluster rewrite ordering, `AddDependency` evaluation order, and
  the updater-token threading (`AddInternal(t, …)`) are all load-bearing; the
  arena rewrite must not touch them.
- The uncommitted ingest-split instrumentation (stIngestFieldsMs/GeomMs) is in the
  working tree — keep it through the campaign, drop or gate it before release.
