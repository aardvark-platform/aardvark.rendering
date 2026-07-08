# Heap: Typed Assignments — per-field spec-constant tids + assignment-partitioned draws

Status: **STEPS 1–2 IMPLEMENTED & MEASURED (c6bebba3, 2026-07-08).** Decoders take
per-ordinal spec-constant tids (HeapTid0..7 + HeapTidIdx, 0 = runtime); bucket
publishes INTERIM bucket-level inferred tids (unique per field, else 0). All
probes green. Inferred A/B vs AARDVARK_HEAP_NO_SPEC=1:
**5060 @700k 9.16 vs 13.86 (−34%, ≈1.0× vs baked — PARITY); M1 5.43 vs 8.75
(−38%, 1.53× vs baked); RADV @200k 53.9 vs 79.0 same-session (−32%, ratio
3.3→2.33×); 890M 31.65 vs 41.66 (−24%).** Index typing beat the Q&D numbers.
**COMPLETE (2026-07-08, commits 4746222d + 73df3cdd + b70b9dd5): assignment
partitioning (dynamic staging partition + population-gated typed partitions,
materialize 64 / dematerialize 16 / re-materialize under fresh ids),
`mixedtypes` probe (materialize/demat/remat pixel-verified vs classic, PASS on
5060 + RADV + 890M + M1), async pipeline specialization (background compile,
unspecialized-fallback publish, ownsHandle borrow guard). All probes green on
all four machines; renderbench holds: 5060 9.16ms/0.99×, M1 5.43/1.53×, RADV
51.3/2.31×, 890M 31.8.** REMAINING: §3.6 ship chain only (fshade release →
rendering → packaging — user parked deliberately).

## 1. The result this is built on (all measured 2026-07-08)

Q&D typed-fetch experiment (`AARDVARK_HEAP_TYPED_INLINE="Positions:13,Normals:13,Colors:40"`,
renderbench, vs unspecialized baseline):

| machine            | baseline | typed | win  |
|--------------------|---------:|------:|------|
| RTX 5060 @700k     | 13.93    | 10.29 | −26% (≈1.1× vs baked floor) |
| RADV RDNA2 @200k   | 73.7     | 49.6  | −33% (3.3× → 2.2× vs baked) |
| 890M @200k         | 41.8     | 34.1  | −18% |

Context: dropping UNTAKEN arms (spec-constant masks OR real codegen — proven
byte-equivalent on all three compilers, RADV code 3868→2060B with VGPRs 48→48)
was a wash (+10%/−4%/+2%). The win is killing the TAKEN path's tid load + branch
chain + class arithmetic — i.e. typed fetch. These numbers are the FLOOR: the
experiment typed only 3 attribute fields; assignments also type the index and
every remaining laddered field.

## 2. Design (settled with the user — do not relitigate)

- **Per-field spec-constant tids.** Each gathered field reads
  `uniform?SpecConstants?HeapTid_<semantic> : int` (index: `HeapTidIndex`).
  Decoders take the spec tid as a parameter:
  `let tid = if tidSpec <> 0 then tidSpec else uniform.HeapDataI.[r]`.
  Specialized → driver folds to the one arm, **tid never read**. 0 → today's
  runtime ladder, byte-identical (default, and the always-correct state).
  FShade needs NOTHING new: the `SpecConstants` magic scope (branch
  `spec-constants` in fshade, commit 4b060f1) mints one constant_id per
  distinct member name automatically; the heap's rewrite just emits the reads.
- **Keying split (settled):** bucket key = effect (already carries the INPUT
  types / field set / decoder+swizzle choices). Pipeline key = per-field
  SOURCE tids via `RenderObject.SpecConstants` (rendering plumbing exists:
  v57 60f3c48d — SpecializationResource, per-pipeline stage-info copies,
  spec values in the pipeline cache key). Per-member: NOTHING.
- **Assignments, not fallback.** Mixing exists only ACROSS slots (a slot's
  tids are fixed at Add; type changes arrive as realloc/re-add). Slots are
  partitioned by their interned tid VECTOR (= "assignment"); each assignment
  gets its own draws + one fully-typed pipeline (same shader source,
  different spec values). The generic ladder (all tids 0) is only the
  debug/bisect state and the trivial "pipeline not yet compiled" first-frame
  state — steady-state dynamic decode does not exist.
- **Dynamic partition as STAGING + long tail (settled, user 2026-07-08).**
  The unspecialized pipeline backs a permanent partition (id 0). New slots
  ENTER through it (correct from frame one — no hazard window exists at all)
  and MIGRATE to their typed partition once (a) the assignment is
  materialized and (b) its pipeline is compiled — an O(1) cluster-style
  membership move. Typed partitions are POPULATION-GATED (threshold +
  hysteresis): esoteric assignments with a handful of slots stay dynamic
  forever — no pipeline/partition explosion, today's speed for the tail.
  Thresholds SETTLED (user, 2026-07-08): materialize 64 / dematerialize 16 —
  "single instances don't fragment it and heavily used paths are optimized."
  (A vertex-weighted threshold via vcOfSlot noted as a possible future
  refinement, not needed now.)
- **Async pipeline creation (settled).** The unspecialized pipeline (all tids
  0) is a CORRECT placeholder by construction, so async is risk-free: the
  per-assignment pipeline resource returns the bucket's generic pipeline
  while a background task runs vkCreateGraphicsPipelines (legal off-thread;
  VkPipelineCache is internally synchronized), then flips via the
  post-eval-transact pattern (MarkOutdated -> next frame binds specialized).
  New assignment: a few generic frames, then typed. Never a hitch, never a
  wrong pixel. Optional later: VK_EXT_pipeline_creation_cache_control
  fail-on-compile-required to make "cached -> instant" explicit.
- **Inference only.** Nothing user-specified, ever (user rule). Assignment =
  observed reality per slot.

## 3. Implementation map (all in HeapPool.fs unless noted)

1. **Decoder parameterization**: `decodeHeapV4f/decodeHeapV4i (tidSpec:int) r v`
   with the `if tidSpec <> 0` head; `decodeHeapIndex` likewise
   (`HeapTidIndex`: 1=u32, 2=u16, 0=read header... index header ref -1 =
   non-indexed stays a runtime check). `hostGather` already receives the
   semantic name (threaded 2026-07-08) — emit the scope read per field.
   REMOVE the arm-mask machinery from the earlier wash experiment
   (HeapSpecDisabledF/I gates, armBitsOfTid, observedArms cval,
   AARDVARK_HEAP_NO_SPEC/SPEC_INLINE/TYPED_INLINE env hacks) — superseded.
2. **Slot assignment identity**: at AddInternal, collect the slot's per-field
   tids (attr resolution sites have them; index tid from idxFor; uniform
   fields are NOT laddered — codegen-static — and stay out of the vector).
   Intern vector → small int assignmentId; store per slot (parallel array
   like slotPage). Bucket-level `Dictionary<tidVector, AssignmentState>`.
3. **Draw partitioning** (the big piece — cluster machinery is the template):
   clustered path keys (page, class) capacity regions in ClassSlots; extend
   to (page, assignment, class). Records per (page, assignment) become their
   own indirect ranges so one multidraw per (page, assignment). Membership
   O(1) swap-remove as today. The HEAP_NO_CLUSTERS bisect path stays
   unpartitioned+unspecialized (debug-only, document).
4. **Per-assignment RO clones**: extend the existing per-page clone pass
   (RenderObject.Clone bucketRO + re-pointed DrawCalls) by the assignment
   dimension; each clone gets `SpecConstants <- AVal.constant (Map.ofList
   [("HeapTid_"+sem, tid); ...])`. New assignment → new clones → command
   tree delta through the existing structural machinery.
   HeapRenderObject.Draws: per (page, assignment).
5. **Probes**: existing suite (golden/dyngeom/churn/dynpick/picksplit2) must
   stay green. NEW `mixedtypes` probe: one bucket, one semantic fed by TWO
   source types across members (e.g. C4b + C4f colors), pixel-diff vs
   classic, assert 2 live assignments and that removing one type's members
   collapses to 1. A/B acceptance: renderbench numbers ≥ the Q&D table.
6. **Ship chain**: fshade `spec-constants` branch → master + release;
   rendering v57 (60f3c48d + this) + paket bump; heap wiring rides rendering.

## 3b. Demo before/after (CadSceneDemo --offscreen, vienna_d9 = districts 1-9,
##     246,655 parts; mac dataset is an older packing with 200,882 parts)

BEFORE (published 0046 + dom 0017 + fshade 5.7.12), REAL GPU orbit avg:
| machine  | GPU ms | task.Run CPU ms | note |
|----------|-------:|----------------:|------|
| airtop RTX 5060   | 8.5  | 8.9  | |
| hekla RTX 4070 Ti | 5.8  | 7.5  | |
| macbook M1 Max    | 19.4 | 20.1 | 200,882-part dataset |
| zephyrus (default device) | 21.3 | — | demo doesn't print the device; same chooser both runs |

AFTER (0047 typed-assignment JIT), same data (canonical 246,655 parts, mac on
vienna_d9c), same machines, plus the `--baseline` baked floor:
| machine  | BEFORE | AFTER | win | baked floor | ratio before→after |
|----------|-------:|------:|-----|------------:|--------------------|
| airtop RTX 5060    | 8.5  | 5.7  | −33% | 4.2  | 2.02× → 1.36× |
| hekla RTX 4070 Ti  | 5.8  | 4.6  | −21% | 3.8  | 1.53× → 1.21× |
| macbook M1 Max     | 21.4 | 13.4 | −37% | 6.8  | 3.15× → 1.97× |
| zephyrus (default) | 21.3 | 16.4 | −23% | 15.3 | 1.39× → 1.07× |

iGPU + zephyrus-corrected columns (0048, AARDVARK_VULKAN selection; "before" =
AARDVARK_HEAP_NO_SPEC=1 on the same build — behavior-identical to 0046).
ZEPHYRUS CONTAMINATION: the ORIGINAL zephyrus rows (21.3/16.4/15.3) were taken
while a FORGOTTEN windowed demo (the user's, from the previous evening — the
dom self-sustaining-60Hz-loop bug) pinned the 4070 Laptop at 94% for ~20h.
Clean + rested A/B/A reruns (drift-free: A/A and B/B within noise):
| device | before-equiv | JIT | win | floor | ratio |
|--------|-------------:|----:|-----|------:|-------|
| zephyrus RTX 4070 Laptop | 8.1 | 6.5 | −20% | 6.6 | 1.23× → **1.0× (AT floor)** |
| zephyrus 890M (medians of 6 runs; APU noise ±9%) | 113 | 94 | −17% | 46 | 2.46× → 2.04× |
| hekla RDNA2 (RADV) | 126.2 | 80.3 | −36% | 38.3 | 3.30× → 2.10× |
Thermal soak proved a NON-issue (hot-chassis vs rested identical); concurrent
load was the entire distortion. Mesa-software (llvmpipe) passes mixedtypes —
Mesa compiler stack validated; real ANV pending (airtop UHD630 BIOS-disabled).

SHIPPED: FShade 5.7.15 (master), Aardvark.Rendering 5.7.0-prerelease0047 +
0048 (AARDVARK_VULKAN=integrated|discrete|<name> default-chooser override;
explicit choosers always win) (v57), CadSceneDemo bumped + pushed. Publish-workflow gotcha: path-filtered to
RELEASE_NOTES.md — a fix commit that doesn't touch it publishes NOTHING
(retrigger via a notes touch). Blanket `dotnet paket update` broke CI once
(FsCheck 2→3): use TARGETED per-package updates.

### FAIR floors — the campaign's closing table (2026-07-08)
Baseline reworked for SAME-WORK parity (in-shader oct decode, water chain,
identity Model/NormalMatrix mat muls executed, flow column carried, 32 B/vert
expanded = 1.70 GB/frame) + a SECOND floor: plain MDI, one record per part
(per-part identity preserved = the ACHIEVABLE classic pipeline). Heap payload:
0.97 GB/frame (43% fewer bytes — oct + singleton colors survive; a bake must
expand). GPU ms, d1-9:
| device | one-draw | MDI floor | heap JIT | heap/MDI |
|--------|---------:|----------:|---------:|----------|
| RTX 5060        | 5.6  | 5.6  | 5.7  | 1.02× |
| RTX 4070 Ti     | 7.0  | 4.9  | 4.6  | **0.94× — heap FASTEST** |
| RTX 4070 Laptop | 7.1  | 6.8  | 6.5  | **0.96× — heap FASTEST** |
| M1 Max          | 8.2  | 13.2 | 13.4 | 1.02× (MoltenVK per-record tax hits the MDI floor; heap's clustering dodges it) |
| hekla RDNA2     | 44.1 | 43.3 | 80.3 | 1.85× |
| 890M            | 63.2 | 54.1 | ~94  | 1.74× |
VERDICT: the fully-editable heap renders at or BELOW the achievable classic
pipeline on every non-AMD-integrated device (byte savings from preserved
structure outweigh residual gather latency). AMD integrated at ~1.8× = the
latency-bound gather gap = exactly the parked LOCALITY lever's home turf.
Also: MDI-vs-one-draw is free (5060), FASTER (4070Ti/L — distributor
parallelism), −2% (RADV), −60% GPU/2x CPU (MoltenVK).

### CLEAN-ROOM floors (flow/water dropped EVERYWHERE, heap = LEAN_SHADER=1):
the definitive gather-vs-stream comparison — same shader work, same 20 B/vert
floor payload (heap 0.85+singletons ≈ 0.97 incl. metric slack), no per-content
asymmetries left except the singleton-color rate (0.09 GB, inherent):
| device | one-draw | MDI floor | heap lean | heap/MDI |
|--------|---------:|----------:|----------:|----------|
| RTX 5060        | 4.2  | 4.2  | 5.1  | 1.21× |
| RTX 4070 Ti     | 4.1  | 3.5  | 4.3  | 1.23× |
| RTX 4070 Laptop | 5.8  | 4.5  | 6.2  | 1.38× |
| M1 Max          | 6.5  | 12.6 | 11.8 | **0.94× — heap BEATS the achievable classic pipeline** |
| hekla RDNA2     | 27.9 | 28.3 | 70.5 | 2.49× |
| 890M            | 41.7 | 35.1 | 94.5 | 2.69× |
Three regimes: NVIDIA pays 1.2–1.4× pure gather-latency rent; Apple/MoltenVK's
per-record tax makes classic per-part MDI WORSE than the heap (clustering is
load-bearing); AMD APUs at ~2.5× = the latency-gather gap in its purest form —
the parked LOCALITY-by-compaction lever's precisely-quantified target. NOTE the
earlier "heap beats floors on NVIDIA" table was flow-subsidized (the monolithic
bake carried a 0.64 GB zero-flow column a split classic pipeline wouldn't) —
kept above for the record; THIS table is the honest one. MDI-vs-one-draw:
FASTER on discrete NVIDIA (distributor parallelism), neutral 5060/RADV,
2x GPU + 2x CPU cost on MoltenVK.

### M1 as partial 2x2 evidence (user insight, 2026-07-08)
Against the ONE-DRAW floor (emulation-free everywhere): NVIDIA dGPUs 1.05-1.21x,
M1 Max 1.82x, AMD APUs 2.27-2.53x. Apple shares no software with AMD, yet lands
BETWEEN — the gather-cost gradient tracks MEMORY-SYSTEM gather-friendliness
(big dGPU L2 -> SLC/TBDR -> small-cache shared-DRAM APU), not vendor drivers.
Supports "integrated memory system" over "AMD driver"; locality lever remains
the indicated medicine. (Confound: TBDR also penalizes vertex-heavy work.)
CAVEAT on the Apple MDI floor: emulated by MoltenVK — fair vs Vulkan-on-Mac
engines (= any aardvark deployment), unfair vs native-Metal ICB engines (which
would land between 6.5 and 12.6). Quote accordingly.

### KosmicKrisp column (Mesa-on-Metal, SDK 1.4.350.1, 2026-07-08)
Setup: AARDVARK_VULKAN_LIBRARY=<sdk>/lib/libvulkan.dylib + VK_DRIVER_FILES=
<sdk>/share/vulkan/icd.d/libkosmickrisp_icd.json + AARDVARK_INLINE_RENDERPASS=1
(KK exposes NO portability_subset -> the useInline gate misses -> its beta
crashes in vk_common_CmdExecuteCommands; crash-report-confirmed) +
HEAPSPIKE_NO_GPU_QUERY=1 (queries still device-lose). Results @200k (CPU-
blocking measure): golden + mixedtypes PASS (second Mesa compiler validates
typed partitions on Apple silicon); baked 4.10 vs MoltenVK 4.19 = PARITY;
heap JIT 8.24 vs 6.16 (KK slower on gather-heavy paths — CONFOUNDED by inline
re-emit CPU); JIT win −25% under KK. Follow-up if KK matters: driver-name
check in the useInline gate + demo-harness no-query fallback.

### Open experiment: integrated-vs-driver 2x2 (designed, unrun)
The AMD-APU 2.5-2.7x gap is confounded: memory-system (APU latency, no big L2)
vs AMD compiler/arch. Discriminator: a DISCRETE AMD (e.g. used RX 6600) — if it
benches ~1.2-1.4x like the NVIDIA dGPUs, it's the memory system (locality lever
= medicine); if ~2x, it's the compiler/arch. An Arc dGPU (A380/A750) adds
ANV-on-hardware + Intel discrete. Rig: the user's UM890 Pro (OCuLink) + eGPU
dock — also closes the clean-APU row via its own 780M. Everything scripted;
AARDVARK_VULKAN selects; one command per card.

### Baseline anatomy (know what the floor measures)
CadSceneDemo `--baseline` = ALL parts CPU-baked into ONE world-space mesh
(ModelTrafo folded into positions, oct normals CPU-decoded to V3f, singleton
colors EXPANDED), one draw, stock trafo shader, a single ViewProjTrafo uniform.
ALU is deliberately EVEN with the heap: the stock shader still executes the
(identity) ModelTrafo and NormalMatrix multiplies per vertex (UBO values are
not driver-folded) — so the floor is slightly INFLATED (~5%, flattering
"heap at floor" claims); a strict floor would skip Model/NormalMatrix.
Payload (exact accounting, d1-9): baseline 1.49 GB/frame (28 B/vert) vs heap
0.97 GB/frame (pos 12 + NormalOct 4; 110k/247k parts singleton-colored) — the
editable representation moves 35% FEWER bytes than the baked one.

## 4. Gotchas already learned (2026-07-08 session)

- Spec constants ≡ codegen dead-code: PROVEN identical (5060 12.49/12.50,
  RADV 70.3/70.44, 890M 41.09/41.01) — don't re-benchmark the mechanism.
- The Q&D typed hack keyed on INPUT type first and decoded Positions (V4f
  input, tid-13 source) as C4b → 3.2× SLOWDOWN from garbage geometry. The
  input-type dimension is the EFFECT's (bucket key); source typing must key
  on the FIELD. Watch this in the real implementation.
- FShade emits ONE GLSL for all stages → per-name constant_id stability is
  automatic. GL backend: SpecConstants members fall through to a real UBO —
  the bucket must serve them via its uniform provider (all-zero = generic).
- RADV_DEBUG=shaderstats is the ISA-level verification tool (code size /
  VGPRs per pipeline compile).
- Build-check trap: `grep -cE "error FS"` exits 0 on MATCHES — a `&&` chain
  once ran probes against a STALE binary after a failed build.
- Overlay dev flow: local fshade dlls → ~/.nuget/packages/fshade.*/5.7.12/
  lib/netstandard2.0/ (restore pristine before pristine-package builds).

## 5. Current tree state (2026-07-08 end of session)

- fshade: branch `spec-constants` (4b060f1) — SpecConstants scope, interface
  field + serialization, smoke-tested. NOT pushed.
- rendering v57: 60f3c48d (spec plumbing + mask-gating experiment + env
  hacks). The mask-gating parts get REPLACED by this plan; the plumbing
  (RenderObject.SpecConstants, SpecializationResource, pipeline key) stays.
- Wash verdict + all A/B numbers: memory `spec-constants-verdict`,
  `fs-gather-not-a-lever`, `heap-render-perf-decomposition`.
