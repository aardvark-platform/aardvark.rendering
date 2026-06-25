# Heap: paged, shared `HeapStorage` — design & plan

Branch **v57** (= published `5.7.0-prerelease0023`; the clean base is commit `1474e663`).
File: `src/Aardvark.SceneGraph/HeapPool.fs`. The earlier **chunked-arena attempt is a dead end**
(reverted; kept as tag `chunking-attempt` = 3dedeb39 for reference). This document is the source of
truth — read it to resume.

## The two goals (one mechanism)
1. **>4 GB scenes.** Full Vienna's arena is ~3.5 GB (764 397 parts, 67.8 M verts, 22.6 M tris). The
   original failure was an int32 **byte** overflow (`off*4`) at 2 GB — not a GPU limit.
2. **Shared geometry/uniforms across heaps.** Render one scene twice with different shaders without
   storing the shared data twice (a coworker's ask).

They collapse into ONE abstraction: a **paged, deduped, refcounted store** (`HeapStorage`) plus
**per-(heap × page) sub-draws**. Goal (1) = a store with >1 page. Goal (2) = >1 heap on one store.

## Why NOT chunk-switch, why NOT bindless (settled with data — do not revisit)
The chunked-arena attempt split ONE arena into ≤8 SSBOs and did a per-read 8-way switch
(`c=off>>>28; if c==0 HeapData0[l] elif…`) in the shader. It was **correct** (golden 20/20 single +
multi-chunk, full Vienna rendered) but:
- **It ~2× the GPU render cost EVEN single-chunk.** Measured on the orbit benchmark (heap-editor-
  benchmark, RTX 5060, gpuMs): geforce 0.16→0.35 ms (2.2×), geforce-parts 0.47→0.96 ms (2.06×). The
  per-read shift+mask+branch + 8 SSBOs in the descriptor set (register/occupancy) sit on the hot
  per-vertex gather path and penalize EVERY heap user, not just >4 GB.
- **Bindless (`HeapData[c][l]`, descriptor-indexed array) is killed by MoltenVK.** M1 Max /
  MoltenVK (VulkanSDK 1.3.231) reports `shaderStorageBufferArrayNonUniformIndexing = false`
  (and `…Native = false`). The heap's multidraw puts different objects (→ chunks) in one warp, so the
  chunk index is NON-uniform → bindless would be invalid exactly in the multi-chunk case we need on
  the Mac. Only `shaderStorageBufferArrayDynamicIndexing=true` (uniform index) is available there.
- **MoltenVK also caps `maxPerStageDescriptorStorageBuffers = 31`** → 3 views × 8 = 24 SSBOs strains it.
- **`maxStorageBufferRange ≤ 4 GB` on EVERY Vulkan target** (uint32 cap) — confirms a single buffer
  can't exceed 4 GB, so paging is needed >4 GB regardless; but it ALSO means ≤4 GB needs no split.

GPU limits captured: airtop RTX 5060 = nonUniform true+Native, 1 048 576 SSBOs, 4 GB range. hekla =
1 M–4 G / 4 GB. M1/MoltenVK = nonUniform FALSE, 31 SSBOs, 4 GB range.

**Conclusion:** keep the ORIGINAL single-buffer gather (`HeapData[off]`, direct, no switch, no
bindless) and never put chunk logic in the shader. A draw binds ONE page buffer. Multi-page = multiple
draws. This is portable (MoltenVK/GL), ≤31 buffers, and **baseline perf by construction**.

## CLEAN BASELINE to match (orbit, RTX 5060, gpuMs median)
geforce (2 497 parts) ≈ **0.16 ms**; geforce-parts (68 452) ≈ **0.47 ms** steady-state (run-to-run
GPU-clock variance ±2×, take the low/steady value). The paged build must land here for ≤1-page scenes.
Run: `cd ~/projects/heap-editor-benchmark/heap; dotnet bin/Release/net8.0/Bench.dll --heap --model
../assets/geforce-parts --orbit --frames 60 --warmup 20 --out /tmp/x.csv`; gpuMs = CSV col 5.

## THE DESIGN

### `HeapStorage` (shader-agnostic data placement + lifetime)
Owns: **pages** (each a storage buffer ≤ pageSize; default **1 GiB / 2²⁸ words** — keeps `off*4`
int32-safe, the .NET staging `float32[]` under 2 GB, the SSBO under the 4 GB range), a **per-region
dedup map**, a **per-page allocator**, and **per-page dirty-tracking** (each page IS a today-style
`HeapArena`: `pending` writers + `pendingStatic`, coalesced sub-range upload on `Compute`).

- **Dedup is per-REGION (per attribute / per uniform), keyed by aval/value identity.** A region may be
  **duplicated across pages** → the map is `regionIdentity → { pageIndex → offset }`.
- **Refcount is per (region, page) copy.** Free a copy when its page's refs hit 0; free a page when empty.
- **`Place(group)`** — a *group* = one object's regions; it MUST land wholly on one page (its draw reads
  one buffer):
  1. per-region dedup lookup → which page(s) each existing region is on;
  2. all existing regions on one page (or none) → target = that page (or the current fill page);
     allocate new regions there. (happy: max sharing)
  3. spread across ≥2 pages → pick a target page (heuristic: the one already holding the most of this
     group's bytes AND that can fit the remainder) and **duplicate** the stragglers into it; if no
     single page fits the whole group, **open a new page** and place the whole group there.
  returns `(pageIndex, offsets[])`; bumps the per-(region,page) refcount.
- Region size must be ≤ pageSize (no single attribute/uniform bigger than a page — fine at 1 GiB).

### Heap (one effect/shader; owns the draws)
References a `HeapStorage`. Per page it touches: **one indirect/multidraw buffer** (the sub-draw) +
the **per-slot header table** (slot → field offsets into that page; shader-schema-specific) + the
effect/pipeline + the **per-page derive dispatch** (fp64/df32/chain compose, reading & writing that
page's copies). Maps each of its objects to `(page, slot)` via `storage.Place`. Renders **one sub-draw
per page**, binding that page as `HeapData`/`HeapDataI`/`HeapDataD` and using the **unchanged gather**.

### Updates — demand-pulled per-page flush (answers "who updates?")
NOT eager-from-storage (would write copies no live draw needs), NOT per-draw-tracked (bookkeeping
nightmare). Instead, the **today model replicated per page**:
- each region copy = a `RegionWriter` **on its page**, subscribed to the source aval (`Place` creates it);
- source changes → the aval marks EVERY copy's writer → each lands in **its own page's** dirty set
  (`InputChangedObject`), no central bookkeeping;
- a heap's **draw pulls the page buffers it renders** → each pulled page's `Compute` packs+uploads
  **only its dirty copies** (and runs its derive dispatch). A page no live draw pulls never evaluates
  (no wasted upload). A page two heaps share flushes **once** per change (Aardvark adaptive caching).
- Flush granularity is the PAGE (all-dirty), same as today's single arena — **no worse than status quo**;
  **pageSize is the dial** (smaller = finer flush + more draws; bigger = coarser + fewer draws).

### Concurrency — forbidden by convention
One render task evaluates/updates a given `HeapStorage` at a time (page resize reallocates a buffer a
concurrent draw is reading; dedup map / allocator / dirty sets are mutated unlocked; per-page `Compute`
isn't re-entrant). Aardvark's per-transaction single-threaded eval already gives this. Relaxable later
(mutation lock + double-buffer-on-resize) without an API change.

### API
```fsharp
let storage = HeapStorage.create runtime
let heapA = Heap.ofRenderObjects storage runtime objsA   // shader A
let heapB = Heap.ofRenderObjects storage runtime objsB   // shader B, shares with A via `storage`
```
The existing `Heap.ofRenderObjects runtime objs` overload makes a private storage (source-compatible).
`storage` is the lifetime owner.

## IMPLEMENTATION ORDER (golden-gated, build sandbox-OFF)
Golden suite (must stay 20/20): `cd ~/projects/aardvark.rendering-heap`,
`dotnet "bin/Debug/net8.0/40 - HeapSpike.dll" <name>` for: plain sgheap sgchain livechain chain buckets
vis modes fp64 passthru nativebuf geomvalue noindex msaa livechaindeep geomchurn autofields gpugeom
texheap atlasheap. Perf gate: the orbit baseline above (must stay ~0.16 / ~0.47 ms).
1. **Single-page storage refactor (no behavior change).** Extract today's per-bucket arena + dedup +
   allocator into a `HeapStorage` holding ONE page; bucket `Place`s through it; gather unchanged.
   Golden 20/20 + orbit at baseline. (This is the safe scaffold — one page = today.)
2. **Multi-page `Place`** (per-region dedup → {page→offset}, per-object co-location, new-page roll) +
   **per-page sub-draws** (split the bucket's indirect buffer + header table + derive per page). Fix the
   int32 `off*4` byte math to be page-LOCAL (≤1 GiB → already int32-safe). Test: a synthetic >1-page
   scene (a `HEAP_PAGE_WORDS` env knob to force tiny pages on golden scenes, like the old CHUNK_SHIFT
   trick) → golden 20/20 with multiple pages; assert a 2-page render == 1-page render.
3. **Cross-heap sharing (B).** Public `HeapStorage` + the `ofRenderObjects storage` overload; two heaps
   over one storage; per-(region,page) refcount across heaps; lifetime/dispose. Test: two heaps, shared
   geometry, different shaders → one storage, both correct, shared regions placed once.
4. **Mac + Vienna.** Full-Vienna load on MoltenVK (multi-page) + desktop; confirm baseline-ish perf.

## VALIDATION RIG (already set up)
- **Benchmark:** `~/projects/heap-editor-benchmark/heap` (pure offscreen, `gpuMs` via ITimeQuery).
  `Bench.fsproj` was bumped 0015→0023 for the A/B. orbit = `--orbit`. Models: `assets/geforce` (2.5k),
  `assets/geforce-parts` (68k). To A/B a local heap: build the heap Release, `cp bin/Release/
  netstandard2.0/Aardvark.SceneGraph.dll` over the bench's `bin/Release/net8.0/`, run.
- **Demo override (airtop):** `~/projects/CadSceneDemo` compiles against published 0023 and a post-build
  `OverrideHeapDll` target drops the local heap Release DLL into its output (toggle `/p:HeapOverride=
  false`; path via `HEAP_DLL`). Aardium set fullscreen. Run full Vienna:
  `DISPLAY=:0 dotnet src/CadSceneDemo/bin/Debug/net8.0/CadSceneDemo.dll ~/vienna_full`.
- **Full Vienna model:** `~/vienna_full` (downloaded: positions/normals/colors/flow/indices .bin +
  manifest, ~3.7 GB, 764 397 parts). Built by `CadSceneDemo/tools/build_vienna.py <out> --full`.

## Confirmed facts (don't re-investigate)
- maxStorageBufferRange ≤ 4 GB (uint32) everywhere. MoltenVK: nonUniform SSBO indexing FALSE, 31 SSBOs.
  Desktop NVIDIA/AMD: nonUniform true+Native, ≥1 M SSBOs. → single-buffer-per-draw is the portable floor.
- The heap already falls back to a texture ATLAS on MoltenVK (no bindless samplers) — consistent signal
  that MoltenVK descriptor-indexing is too limited to rely on for the arena.
- pageSize default 1 GiB (2²⁸ words): int32-safe `off*4`, <2 GB .NET array, <4 GB SSBO, bounds resize.

## STEP 2 — implementation map (the core; do with a fresh budget)
DONE: step 1 (commit 6110fac8) — `HeapStorage` holds arena/arenaAlloc/regions/singleRegions/
constituentsF/B; bucket fetches them; one private storage per bucket; golden 20/20. Gather unchanged.

Step 2 makes the storage PAGED and splits the draw. Key code (clean 0023 line numbers, +~18 from step1):
- **`allocRegion` (2791)** + **`freeRegion` (2817)**: the per-uniform dedup/alloc. Pattern: `regions.
  TryGetValue av` → refcount++/Offset, else `arenaAlloc.Alloc(sz)` → `int b.Offset` → `arena.
  EnsureFloats`/`StageOnce`|`Add` → `regions.[av] = {Offset;Size;Writer;RefCount;Block;HeaderWords}`.
  Same shape for single-value attrs (`singleRegions`, ~2858+), geometry (`arenaAlloc.Alloc` at
  2884/2894/3048), and derived constituents (`constituentsF/B`, packM44dInto ~2835).
- **`RegionEntry` (1063):** `{mutable Offset; Size; Writer; mutable RefCount; mutable Block; HeaderWords}`.
- **Draw/header (3109+):** `entries : DrawCallInfo[]` (per-slot multidraw), `headers : int[]` (slot→
  field offsets), `drawBuf`/`headersBuf` (MirrorBuffers), one RenderObject with `DrawCalls.Indirect`
  + `HeapData`=arena binding.

RESTRUCTURE:
1. **HeapStorage → pages.** `pages : ResizeArray<Page>`, `Page = { Arena:HeapArena; Alloc:HeapSpace }`.
   Dedup maps become `aval → ResizeArray<RegionEntry>` (one per page-copy) or `aval → Dictionary<int,
   RegionEntry>` (page→entry). `RegionEntry` gains `Page:int`; `Offset` is page-LOCAL.
2. **`Place(group)` replaces per-field `allocRegion`.** Restructure `AddInternal` to COLLECT a slot's
   regions (uniforms+geometry+constituents) first, then place the GROUP atomically: per-region dedup →
   set of pages its existing copies are on; all on one page (or none) → target=that/current-fill page;
   spread → pick target page that fits the remainder (heuristic: most-bytes-already-there) and duplicate
   stragglers; no fit → new page. Returns each region's (page,localOffset); slot.Page = target. Refcount
   per (region,page).
3. **Slot → page.** Each slot records its Page. The per-slot header stores PAGE-LOCAL offsets.
4. **Draw split.** The bucket emits ONE RenderObject PER PAGE: page P's RO binds page P's Arena as
   HeapData/I/D + page P's header sub-table + an indirect buffer of page P's slots. `ofRenderObjects`
   already returns an aset<IRenderObject> — emit N. The derive/chain compute also runs PER PAGE (reads/
   writes that page's copies; its input binding = that page's buffer).
5. **Flush/update** is already per-page once each page is its own HeapArena with its own Compute,
   pulled by that page's RO (the demand-pull model — no extra work).
6. Byte math: page-local off ≤ pageSize (1 GiB) ⇒ `off*4` int32-safe (no change needed).

TEST: `HEAP_PAGE_WORDS` env knob (default 2^28) to force tiny pages on golden scenes → golden 20/20
multi-page; assert 2-page render == 1-page. Then orbit at baseline (gather unchanged ⇒ must match
~0.16/~0.47 ms). Then Vienna on airtop + Mac.

RISK: the AddInternal collect-then-Place restructure + the per-page RO/derive split are the hard parts;
everything else (per-page HeapArena/Compute, the gather) is unchanged. Single-page path must stay
byte-identical (golden gate).

## STEP 2 PROGRESS
- **2a DONE (commit ade438fc), golden 20/20.** `HeapStorage` (step-1 type) → renamed **`PageArena`**;
  new **`HeapStorage`** holds `pages : List<PageArena>` + `pageWords` (env HEAP_PAGE_WORDS, default 2²⁸)
  + `Page(i)`/`CurrentPage`/`PlacePage(words)` (rolls when current page's Extent+words>pageWords). The
  bucket's `arena`/`arenaAlloc`/`regions`/`singleRegions`/`constituentsF·B` are now **mutable currents**
  set by `setPage i` (rebinds them to page i's structures + sets that page's arena.ExtraDependency).
  `HeapSlot` gained `mutable Page`. `AddInternal` calls `setPage (storage.PlacePage 0)` [est 0 ⇒ page 0
  for now] + records `Page=curPage`; `RemoveInternal` calls `setPage s.Page` before freeing. ⇒ the data
  side is fully page-routed; with est 0 + small scenes it stays 1 page = behaviour-identical.

- **2b TODO — enable rolling + per-page RO fan-out (the draw layer).**
  1. **Real estimate** in AddInternal: replace `PlacePage 0` with the slot's worst-case word size
     (`faceVertexCountOf ro × Σ attr strides + names.Length×~32 + numConst×64`), conservative/over-est so
     the slot always fits the chosen page (a region/slot must be ≤ pageWords).
  2. **Per-page draw.** Keep `headers`/`headersBuf` GLOBAL (slot→PAGE-LOCAL offsets; the draw record's
     FirstInstance already carries the global slot, so a global header table works). Maintain
     `pageSlots : ResizeArray<int>[]` (slots per page). Per page: an indirect buffer = compacted
     `entries[s]` for `s ∈ pageSlots[P]` (its own MirrorBuffer + dirty flush), a `derivedU_P` binding
     page P's `Arena` (HeapData/I/D) + triggering page P's derive, and a `deriveRO_P`. Build N `bucketRO`s
     (one per page) — `member RenderObject` → `member RenderObjects : IRenderObject[]`; same for DeriveRO.
  3. **ofRenderObjects** (collects `.RenderObject`/`.DeriveRO` from buckets) → collect the arrays.
  4. The current single-RO build is at 3554 (`bucketRO`), derive at ~3629 (`deriveRO`), `derivedU`
     ~3460, indirect/drawBuf ~3113. Each becomes per-page; the gather/headers are UNCHANGED.
  5. TEST: `HEAP_PAGE_WORDS=<small>` forces multi-page on golden → golden 20/20 multi-page; orbit at
     baseline (gather unchanged); then Vienna airtop+Mac.
  RISK: the per-page indirect + dynamic pageSlots management + N-RO emission is the bulk; headers/gather
  unchanged. No green intermediate inside 2b (single-RO → N-RO is atomic) — single page must stay
  byte-identical (golden gate).

## STEP 2b PROGRESS (branch heap-step2b)
DONE & golden 20/20 (1-page) at every commit; multi-page (HEAP_PAGE_WORDS=16384) = 17/20:
- 9563d9e0 per-page draw fan-out: slotPage routing, per-page indirect buffer + cloned per-page RO
  (binds Page(i).Arena), reactive RenderObjects/DeriveROs arrays in resultAval, rolling estimate.
- 2b17e69b page-0 RO binds Page(0).Arena (not the mutable current that the add-path re-points).
- fbbd7f49 per-page geometry dedup: attrStatic/idxStatic moved into PageArena (shared-mesh cross-page
  corruption: sgheap 134k→19k wrong px).
MULTI-PAGE 17/20 PASS incl plain/buckets/geom/textures/gpugeom/fp64/CHAIN/geomchurn. 3 fail:
- sgheap: ~14% px still wrong (maxDelta 217) at multi-page — a residual per-page leak in the
  singleton/single-value or gate path (geometry now correct; bulk fixed). TODO: find the last
  global-offset holder (candidates: gates not re-run in pages>0 flush; allocOutput/fold; or a
  single-value subtlety). Static fp64+chain PASS multi-page, so the derive isn't fundamentally broken.
- livechain/livechaindeep: LIVE chain + churn + multi-page (static chain passes). The per-page chain
  FOLD / derive dispatch is page-0-scoped; needs per-page derive (one dispatch per page, page-i arena
  + slots). This is the remaining big follow-up (Vienna fp64 will need it too).
KNOWN COARSENESS (acceptable for now): pages>0 use a full-rewrite indirect flush (not incremental);
PublishStats/compaction iterate only the current page's dicts.

## STEP 2b — STATE OF PLAY (branch heap-step2b; v57 clean at the 2a base)
The per-page render+derive layer is built and CLEAN (deterministic, no patches left). Commit chain:
9563d9e0 per-page draw fan-out → 2b17e69b page-0 binds Page(0).Arena → fbbd7f49 per-page geometry
dedup (attrStatic/idxStatic→PageArena) → 02b21fd9 per-page DERIVE (HeapSlotPage/HeapPageId guard) →
f9946b68 deterministic SyncPages (kill lazy/eager) + diagnostics removed → 5c81d660 merge per-page
derive dispatches into ONE OrderedCmd CommandRenderObject/bucket.

### Architecture (the clean form, as built)
- `HeapStorage` = `pages:List<PageArena>` + `pageWords` (env HEAP_PAGE_WORDS, default 2^28) + `PlacePage`.
- `PageArena` = one page's arena + allocator + ALL dedup dicts (regions/single/constituentsF·B/
  attrStatic/idxStatic). Dedup is per-page; cross-page = duplicated (accepted).
- bucket: mutable `arena`/`regions`/… are the CURRENT FILL PAGE, set by `setPage` ONLY in the alloc
  path (AddInternal/RemoveInternal). `HeapSlot.Page` + `slotPage:int[]` record each slot's page.
- RENDER: one RenderObject per page (page 0 = `bucketRO`, pages>0 = `RenderObject.Clone bucketRO` with
  DrawCalls=page-i indirect, Uniforms: symData/I/D→Page(i).Arena else delegate). Page-0 binding is
  `arenaU=Page(0).Arena` explicitly (NOT the mutable). Header table is GLOBAL (page-LOCAL offsets); each
  page's flush zeroes non-page-i slots' draw records.
- DERIVE: `composeDerived`/`Df32` guarded `if slot<n && HeapSlotPage[slot]=HeapPageId`; one DispatchCmd
  per page (binds Page(i).Arena + HeapPageId=i + slotPageBuf), all bundled into ONE
  `CommandRenderObject(OrderedCmd(AList.ofAVal updater→deriveCmds))` per bucket.
- DETERMINISTIC creation: `member SyncPages()` (= ensurePageROs+ensureDeriveROs) called from the
  membership updater AFTER the delta, BEFORE version bump ⇒ pages are in `resultAval` before render.

### Validation
- 1-page golden 20/20 at EVERY commit.
- multi-page (HEAP_PAGE_WORDS=16384): 17/20. `[ag]` (legacy Ag traversal) renders multi-page CORRECTLY
  incl sgheap/fp64/chain/textures/geomchurn. Fixed bugs en route: shared-mesh cross-page corruption
  (geometry dedup was global) and page-0 binding the mutable current; derive page-correctness.

### THE TWO REMAINING FAILURES + what we PROVED about them
1. **`sgheap[ts]` / `[mixed]` (and any multi-page on the ISimpleSg "Simple" path = the DEFAULT, which
   Aardvark.Dom/Vienna uses).** A clean rectangular HOLE = page>0's ~32 boxes (geometry correct,
   coverage matches; they're absent/off-screen). `[ag]` (same heap, legacy traversal) is perfect.
   RULED OUT (verified in source, not guessed):
   - NOT ordering: DispatchCmds hoisted before BeginRenderPass (CommandTask.fs:2234, OrderedCmd 2212
     recurses) ⇒ every page's derive precedes every draw; draws are UnorderedRenderObjectCommand
     (z-buffered, order-independent).
   - NOT backend dedup: UnorderedRenderObjectCommand.cache = Dict<IRenderObject,_> keyed by Id;
     RenderObject.Clone (RenderObject.fs:134) assigns a FRESH Id ⇒ page clones are distinct.
   - NOT lazy/first-frame timing alone: deterministic SyncPages didn't fix it; merging N derive ROs
     into one OrderedCmd didn't fix it.
   LEAD (next step, ~1 file): the two Sg integration points both pipe child→Heap.ofRenderObjects
   (HeapPool.fs ~5185 ISimpleSg.GetRenderObjects vs ~5215 Ag RenderObjects), so the heap OUTPUT is
   identical. The divergence is in how `runtime.CompileRender` (Simple) vs the Ag task FORCE/snapshot
   the heap result-aset (`resultAval |> ASet.ofAVal`). Hypothesis: CompileRender builds its command
   from the aset reader BEFORE the first force of `updater` (so before SyncPages has rolled page>0 into
   the set) and doesn't re-pull; the Ag path forces scope/runtime eval first. TEST: does the Simple
   render-task pick up an aset element ADDED during the first force? If not, the heap must guarantee all
   pages are present on the FIRST read (e.g. force `updater` when the result aset reader is created, or
   make page materialization not depend on a reactive read at all). Find: `runtime.CompileRender` entry
   + how it reads the sg's ISimpleSg.GetRenderObjects aset.
2. **`livechain` / `livechaindeep`.** The CHAIN FOLD (composeModel, in `derivedU`/chainProg) is still
   page-0-scoped (binds arenaBuf=Page(0)). Needs the SAME per-page treatment as composeDerived: a
   HeapSlotPage/HeapPageId guard in `composeModel`/`composeModelDf32` + per-page chainInput binding
   Page(i).Arena, run per page (or folded into the per-page derive OrderedCmd). Static `chain` passes
   multi-page (its folds land on page 0); only the live/churn variants surface it.

### KNOWN-GOOD invariants to preserve
- Draws MUST stay raw per-page ROs (NOT wrapped in OrderedCmd/RenderCmd — HeapPool.fs:~3624 warns
  wrapping the dynamic-indirect bucket breaks membership churn → GPU hang). Derives CAN be wrapped.
- pages>0 currently use a FULL-REWRITE indirect flush (re-stage all slots each pull). Fine for >4 GB;
  for orbit-perf parity make it incremental (per-page dirty sets) — but only matters under churn/orbit.
