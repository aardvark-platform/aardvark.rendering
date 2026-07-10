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
HEAPSPIKE_NO_GPU_QUERY=1 / CADSCENE_NO_GPU_QUERY=1 (KK has NO timestamps:
queue family reports timestampValidBits = 0 — legal under full conformance,
timestamps are optional per queue family; aardvark wrote them anyway =
VUID-vkCmdWriteTimestamp-timestampValidBits-00829 -> device lost. FIXED in
1a9ba225: CreateTimeQuery returns EmptyTimeQuery when timestampBits = 0, so
queries-on merely reads zero; validated under KK). All KK numbers below are
blocking task.Run CPU (~= GPU + ~1 ms, includes inline re-emit CPU).

Correctness: golden + mixedtypes PASS (second Mesa compiler validates typed
partitions on Apple silicon). DEMO d1-9 clean-room floors, KK vs (MVK CPU col):
one-draw 8.9 (7.8) | MDI floor 23.0 (24.6) | heap lean 20.0 (12.5) ->
heap/achievable 0.87x — heap BEATS classic MDI on KK too. Full demo: JIT 21.2
vs no-spec 29.3 = −28% (MVK −37%). Readings: the per-record MDI tax is nearly
identical on both layers (23.0 vs 24.6) -> the Apple "heap beats classic"
result is Vulkan-on-Metal INHERENT, not a MoltenVK artifact. KK's gather-heavy
codegen (Mesa NIR->MSL) trails SPIRV-Cross by ~60% (heap lean 20.0 vs 12.5)
while plain streaming is near parity (8.9 vs 7.8).

### arcbench: downloadable benchmark package (2026-07-08)
For collecting table rows from machines we don't own (first target: a friend's
Intel Arc). https://georg.haaser.me/arcbench.zip (win-x64, 913 MB) and
arcbench-linux.tar.gz (linux-x64, 813 MB) — self-contained (bundled .NET),
zero args/env/installs: extract, run run.cmd / ./run.sh, send back results.txt.
Contents: CadSceneDemo + HeapSpike publishes + vienna d1-9 (3.7 GB unpacked).
The runner ENUMERATES GPUs via the demo's --listgpus mode (prints
GPU|name|type; skips Cpu-type = llvmpipe) and runs one section per device via
AARDVARK_VULKAN=<name>. Per section: golden + mixedtypes probes, renderbench
200k JIT/no-spec, demo floors (--baseline: one-draw + MDI), demo heap
lean/JIT/no-spec — a complete driver-matrix row per GPU.
Verified end-to-end: zephyrus (dual-GPU Windows; "(TM)" name survives batch
parsing; 890M floors 43.2/35.4, heap 93.3/94.4/107.4; 4070L floors 6.3/4.7,
heap 6.2/6.7/8.2 — all match the table) and airtop (single-GPU Linux; 5060
floors 4.2/4.2, heap 5.1/5.7/8.5 — exact match). Fallout fixed along the way:
demo saved PNGs to hardcoded /tmp (crash on stock Windows -> Path.GetTempPath,
try-wrapped). Package sources: ~/arcbench/pkg + pkg-linux on airtop.

### RGA static analysis of the demo heap VS (2026-07-08, RGA 2.14.2, gfx1150 = 890M)
Method: carve post-opt SPIR-V from the aardvark shader cache
(~/.local/share/Aardvark/Cache/Shaders/Vulkan/*.effect — scan for magic
07230203, cut at last OpFunctionEnd; the GLSL source is in the same blob,
compile with -DVertex). Typed variant emulated via spirv-opt
--set-spec-const-default-value "0:13 1:21 2:40 3:13 4:2" --freeze-spec-const
(real demo tids: V3f=13 int=21 C4b=40 idx-u16=2).

| variant | VGPR | SGPR | spills | scratch | ISA bytes | mem ops |
|---|---|---|---|---|---|---|
| generic (shipped, post-spirv-opt) | 32/256 | 44 | 0 | 0 | 5924 | 85 |
| generic (UNOPTIMIZED glslang)     | 32/256 | 44 | 0 | 0 | 5924 | — |
| typed (spec consts frozen)        | 34/256 | 28 | 0 | 0 | 1088 | 24 |

Three verdicts: (1) our spirv-opt optimizeDefault is IRRELEVANT on AMD — the
backend produces byte-identical ISA from opt and unopt SPIR-V; (2) register
pressure is NOT the APU problem — 32-34 of 256 VGPRs, zero spills, max
theoretical occupancy in BOTH variants, so no cheap register fix exists;
(3) the JIT quantified at ISA level: -82% code (5924->1088 B), 85->24 memory
ops, which is why it pays everywhere. With occupancy maxed and waves
available, the remaining 2.5x APU gap is pure memory-system latency on
scattered gathers -> locality-by-compaction CONFIRMED as the lever (and/or
reducing the ~24 loads/vertex). Next dynamic step: SQTT capture on hekla RADV
(MESA_VK_TRACE=rgp) to read the stall composition; blocked while PoE2 owns
that GPU.

### SQTT/RGP dynamic capture on hekla iGPU (2026-07-08)
Recipe: demo --window on DISPLAY=:0 (RADV counts frames by PRESENT — the
offscreen harness never presents, so windowed mode is required),
AARDVARK_VULKAN=integrated MESA_VK_TRACE=rgp MESA_VK_TRACE_TRIGGER=/tmp/rgp_trigger;
touch the trigger mid-orbit -> /tmp/dotnet_*.rgp (~271 MB). Viewed in RGP 2.7
(RadeonDeveloperToolSuite linux tgz from gpuopen.com/rdts-linux) on airtop :0.
Capture: airtop ~/arcbench/hekla_igpu_heap.rgp.

Result (Instruction timing, big typed-partition vkCmdDrawIndirect, VS):
FOUR s_waitcnt vmcnt(0) instructions consume ~54% of shader time at ~900 clk
average latency each (903/887/976/912) — the waits after each dependent
buffer_load gather round (header, index, attributes). 981 waves at 5-8.7k clk
each; VMEM 19.6k vs VALU 90k (memory pump, trivial ALU); HW utilization ~27%.
Combined with RGA (no spills, max occupancy): the APU gap is FULLY explained —
waves exist but all park ~900 clk on DRAM gathers, 4 dependent rounds per
vertex. Levers, now quantified: (a) locality/compaction to turn DRAM latency
into cache hits, (b) merging/reducing the 4 dependent gather ROUNDS — each
round eliminated saves ~900 clk x waves on cache-less APUs.

### Extent-class folding (dee52321, 2026-07-08) — chain shortening, phase 1
The SQTT-indicated fix: the typed arm still loaded the header LENGTH for the
singleton-broadcast min, serializing every attribute fetch behind a header
round. Now a per-field 2-bit EXTENT class rides the spec constants (bits 6-7
of HeapTidN): 0 = runtime clamp (dynamic partition/staging/GL — byte-identical
to before), 1 = FULL folds e = vid, 2 = SINGLETON folds e = 0. Assignment keys
became INTERNED vectors (8x8-bit fields outgrew the int64 bit-pack).
Classification at ingest (extendTids): len 1 -> singleton (always safe);
len >= drawn count AND non-indexed -> full (indexed slots address by decoded
index values — only the runtime clamp guards those); matrix fields (tid > 40)
never fold. In-place length edits (demo recolor toggles full <-> singleton on
the makeDynamic in-place path!) and vc changes notify ALL DynRefs ->
recomputeAssign re-derives the key from per-slot raw tid/len shadows and
migrates residency + cluster listing.

Measured: RADV iGPU (hekla RAPHAEL_MENDOCINO) renderbench 200k typed 47->40 ms
(-15%, ratio to baked 2.31x -> 1.96x), no-spec 67.5; 5060 stays at baked
parity (2.63). All probes green on NVIDIA. KNOWN QUIRK: mixedtypes FAILS its
exact-match pixel criterion on RADV even FULLY DYNAMIC (~100 edge pixels,
maxDelta ~150) — pre-existing classic-vs-heap rasterization variance on this
driver, not a regression (bisected: folds off + NO_SPEC both fail identically).
Demo-level numbers (890M / lean floors) need the next rendering release +
demo bump. Remaining chain: non-indexed typed slots should now be ONE
dependent vector round (record reads are draw-uniform -> SMEM).

### Extent folding SHIPPED + measured (0049, 2026-07-09)
Rendering 5.7.0-prerelease0049 (extent folding + EmptyTimeQuery), demo bumped.
Renderbench 200k typed/no-spec/baked: 5060 2.63/3.97/2.65 (parity) | 4070Ti
1.65/2.29/1.97 (BEATS baked 0.84x) | 4070L 3.18/4.14/3.74 (beats baked 0.85x)
| M1 4.98/8.75/3.54 (-43% total JIT) | 890M 30.9/41.7/21.6 (1.43x, was 2.1x)
| RADV 2CU 40.1/67.5/20.5 (1.96x). Probes ALL PASS everywhere.

DEMO d1-9 (lean vs same-day MDI floor = the honest heap/achievable):
5060 4.7 vs 4.2 = 1.12x (was 1.21x) | 4070Ti 3.3 vs 3.5 = 0.94x BEATS FLOOR
(was 1.23x) | 4070L 6.1 vs 4.5* | M1 11.3 vs 12.6 = 0.90x (was 0.94x) |
890M 68.8 vs 35.4 = 1.94x (was 2.69x — yesterday's heap numbers were
thermally soaked; same-day floors identical 42.7/35.4) | RADV 2CU 73.6 vs
28.3* ~2.5x (env drift ~7%, FULL A/B -14% normalized). Full-demo JIT A/B:
5060 5.1/8.5 | 4070Ti 3.5/4.7 | 4070L 6.5/8.1 | M1 12.1/21.9 | 890M 81.1/90.7.
(* floors not re-run same-day on those boxes.)

Learned the hard way AGAIN: HeapSpike falls through to the WINDOWED showcase
on unknown args — a stale tree (mac: partial rsync kept an old Program.fs;
zephyrus: git fetch origin v57 + checkout v57 does NOT create the local branch,
checkout FETCH_HEAD) runs a forever-window that looks exactly like a compiler
hang. Diagnose with dotnet-stack (managed frames), not sample. Also: stuck
processes hold DLL locks -> 36 MSB3027 copy errors on rebuild.

Next lever (designed): INSTANCE-RATE-ATTRIBUTE records. gl_DrawID is useless
under clustering (one record draws a whole class as instances -> slot id is
per-instance, waves span slots -> not even subgroup-uniform). Instead bind the
hot record fields (attr data offsets) as a VertexInputRate.Instance buffer
ordered by class-list position (rewritten under the existing csStaging dirty
policy): hardware fetches them at wave launch (address linear in instance id,
NO dependent load) -> kills the ClassSlots->record double-hop -> ONE dependent
vector round = the VBO chain shape. Core Vulkan everywhere + GL, no features.

### MDI record-granularity sweep + the great floor correction (2026-07-09)
CADSCENE_MDI_SWEEP=1: same fair VB split into K equal triangle-aligned ranges,
K = 1..262144 + per-part. Findings per architecture:
- Desktop NVIDIA (5060 Blackwell, 4070Ti Ada): DEAD FLAT 1..264k — records
  free, MDI == one-draw. The 4070Ti "MDI beats one-draw" (4.1/3.5) was
  CONTAMINATION (sunshine/steam streaming on hekla): clean floors are 2.5/2.5.
- 4070 LAPTOP: mild real effect — one-draw 4.87 -> 4.32 at K>=64, flat after.
- M1 (MoltenVK): flat 6.17 to ~4k, then LINEAR per-record tax (~22 ns/record):
  65k=7.7, 264k=12.1 == the per-part floor. Quantifies why clustering is
  load-bearing on Apple.
- 890M (RDNA3.5): the REAL anomaly, opposite of Apple — records HELP
  monotonically: 42.1 flat to 4k, 16k=40.0, 65k=34.8, 264k EQUAL chunks=30.7
  (beats ragged per-part 35.9 — uniform grain > ragged grain). One-draw is
  the 890M's WORST case (-27% recoverable by splitting).
- RADV 2CU: flat (slight uptick only at 264k = 67-vert records).
- 890M ORDER CONTROLS (clock/turbo hypothesis REFUTED): re-measuring at the
  END of the warmed run: K=1-again 41.4 / K=1-again2 41.2 (== initial 41.9),
  K=262144-again 28.9 (== 28.8). The grain effect is real and stable: -31%
  for 264k equal chunks vs one draw on RDNA3.5, no order dependence.

MORE CORRECTIONS from verified runs: zephyrus D:\heap-bench\CadSceneDemo was
NOT a git repo (pulls failed silently) -> this morning's "0049" demo run there
was actually 0048. Verified-0049 rerun == the 0048 numbers (68.1/79.6/91.0 vs
68.8/81.1/90.7) => EXTENT FOLDING IS DEMO-NEUTRAL ON THE 890M (the earlier
-27% claim was environment); it IS real on M1 (13.4->12.1, verified) and in
renderbench A/Bs. hekla heap numbers were never contaminated (clean triple
identical: 3.3/3.4/4.7), only its FLOORS were; clean 4070Ti renderbench
heap-beats-baked (1.65 vs 1.97) reproduces — scene-dependent, genuine.

FINAL corrected table (clean same-session floors, lean vs MDI floor):
5060 4.7 vs 4.2 = 1.12x | 4070Ti 3.3 vs 2.5 = 1.32x | 4070L 6.3 vs 5.3 =
1.19x (best-grain 4.3 -> 1.46x) | M1 11.3 vs 12.5 = 0.90x | 890M 68.1 vs
35.3 = 1.93x (best-grain 30.7 -> 2.2x) | RADV 2CU 73.6 vs 27.3 = 2.7x.
Lesson cemented: NEVER trust cross-day numbers on shared/laptop boxes — only
same-session A/Bs; hekla floors were poisoned for a full day of tables.
grep -c trap hit a THIRD time (sweep commit silently skipped).

### Instance-rate record rows (3dc06dea, 2026-07-09) — chain shortening, phase 2
The designed lever, built: ClassSlots entries widen from {slot} to
[slot; vc; idxRef; attrRef0..N-1] (rowWords = 3 + min 8 numAttrs), bound as
HeapRec0..N VertexInputRate.Instance int attributes over the SAME MirrorBuffer
(usage +Vertex, per-attr offset, stride rowWords*4). Records' FirstInstance
addresses the row; the hardware fetches it at wave launch -> the
ClassSlots->record dependent double-hop leaves the shader chain for BOTH typed
and dynamic partitions. Shader: slotVar/vtx-clamp/idxRef/attr refs read
HeapRec* inputs (FShade auto-plumbs them to FS as flat varyings for uniform
gathers — same mechanism as the old HeapSlotAttr MoltenVK fallback).
Maintenance: rowFill rides classAdd/classRemove/relayout/ensureRoom + refresh
hooks in GeomMoved and compaction RewriteHeaders (slot->entry via
clusterClsOf/clusterPosOf). Zero new buffers; rows REPLACE ClassSlots.

Renderbench 200k typed/no-spec vs baked: 5060 2.56/3.77 vs 2.65 (0.97x) |
4070Ti 1.63/2.16 vs 1.97 (0.83x) | 4070L 3.14/3.86 vs 3.74 (0.84x) | 890M
28.1/41.5 vs 21.8 (1.29x, was 1.43x) | RADV 2CU 34.6/53.9 vs 22.1 (1.57x, was
1.96x; dynamic -20%). M1 PENDING (mac off — validate MoltenVK instance attrs
before shipping 0050). Probes ALL PASS (AMD-Windows + NVIDIA; RADV keeps its
known pre-existing edge-pixel variance). Campaign totals on APUs:
890M 2.1x -> 1.29x, RADV-2CU 3.3x -> 1.57x vs baked.

### Instance rows — VIENNA numbers (DLL overlay, 2026-07-09 late)
Rows measured on real content by overlaying the locally built
Aardvark.SceneGraph.dll (v57 head == 0049 + rows, binary-compatible) into the
0049 demo output on each machine. Lean / full / no-spec:
5060 4.5/5.0/7.9 (was 4.7/5.1/8.5) | 4070Ti 3.2/3.3/4.3 | 4070L 5.8/6.2/7.9 |
890M 59.5/78.7/88.9 (lean was 68.1) | RADV-2CU 54.4/56.5/101.5 (lean was 73.6,
-26%!). Lean vs MDI floor: 5060 1.07x, 4070Ti 1.28x, 4070L 1.09x, 890M 1.69x,
RADV-2CU 1.99x, M1 0.90x (0049, rows pending mac power-on).
The editable heap now sits within ~10% of the achievable classic pipeline on
desktop/laptop NVIDIA, beats it on Apple, and is under 2x even on a 2-CU
display adapter.

### MoltenVK validation of the rows + the three-bug untangling (2026-07-09)
The mac's "golden crash" unwound into THREE independent issues (914b9dfa):
1. ASYNC-SPEC USE-AFTER-FREE (latent since 0047, all platforms, crashy only on
   MVK): PipelineResource.Destroy released the native pipeline-state cells
   while the background vkCreateGraphicsPipelines still read them (SIGSEGV in
   MVK initSampleLocations). Fix: Destroy waits out the in-flight bgTask.
2. spirv-opt strips UNREFERENCED buffer declarations -> a stage whose
   descriptor view has holes breaks MVK argument-buffer padding. Fix: skip
   optimizeDefault on portability devices (backend re-opts; AMD proved
   byte-identical; MVK A/B-measured neutral — vienna lean 11.1 opt vs 11.0
   no-opt, same rows build, cache cleared between runs).
3. golden[textured] HARD-CODES the bindless sampler array — a path the heap
   NEVER takes on MVK (SupportsUnboundedSamplerArrays gate -> atlas; its own
   comment documents that MVK cannot compile unbounded arrays). The probe was
   wrong, not the product: it now SKIPs where unbounded is unsupported;
   atlasheap covers the real MVK path (PASS badPixels=0 with rows).
   NOTE: MVK golden PASS lines were MISSING all week (silent crashes read as
   "output buffering"); the remembered ALL PASS was KosmicKrisp (bindless
   compiles fine under Mesa). Also: the mac's SYSTEM ICD is MoltenVK 1.2.0
   (SDK 1.3.231, 2022!) — the padding error reproduces on 1.4.350's MVK too.
   macOS aardvark cache lives at ~/Library/Application Support/Aardvark (NOT
   ~/.local/share) — clearing the wrong one re-tests stale binaries.

M1 rows numbers: renderbench typed 4.84 / no-spec 8.36 (was 4.98/8.75);
vienna lean 11.1 / no-spec 20.9 (was 11.3/21.9). FINAL rows table (lean vs
MDI floor): 5060 1.07x | 4070Ti 1.28x | 4070L 1.09x | M1 0.89x | 890M 1.69x |
RADV-2CU 1.99x. Ship gate for 0050 CLEARED.

### SQTT round 2 (rows, 0050, hekla iGPU, 2026-07-09): the derive pass is the story
Most-expensive events in the captured frame: vkCmdDispatch(3854,1,1) =
8,800 us (!!) vs the heap draws at 218/207 us. 3854*64 = 246,656 = ALL slots:
the fp64 derive recompose runs EVERY FRAME under camera motion because
NormalMatrix is CAMERA-RELATIVE -> camera dirties every slot. On the 2-CU part
that's ~8.8 ms/frame resolution-independent (~16% of the 54 ms lean frame);
its own profile is memory-latency-bound (waits 7,985 / 7,663 clk). The floors
have NO derive pass — this is pure heap overhead in the comparison.
VS verdict: the instance row = ONE wait now (seven idxen loads, single
vmcnt(6) at ~1,194 clk, ~25% of VS) — rows work; remaining VS shape is the
irreducible row+attribute two rounds on a cache-less part.
NEXT LEVER (better than any fetch work): make NormalMatrix CAMERA-INDEPENDENT
(derive from Model only — normals are directions, translation cancellation
does not apply; fold view rotation into the global ViewProj gather). Derive
then runs only on EDITS -> the per-frame 8.8 ms vanishes on APUs.
Capture: airtop ~/arcbench/hekla_igpu_rows.rgp (windowed 0050 demo, 1024x768).

### Fair per-object NormalMatrix (demo a241743) + the dedup imperative (2026-07-09)
User call: no rigid-trafo shortcut — the demo now transforms normals by
transpose(ModelTrafoInv) (heap derives it per slot via the existing
DNormal/DMatMul[bwd MBASE] recipes, camera-independent; floors bind the
scene-graph identity ModelTrafoInv = same instructions, zero extra memory).
Render verified correct. COST (same-session floors + lean/no-spec):
5060 lean 4.5->4.9 (+9%) | 4070L 5.8->6.6 | 890M 59.5->96.8 (+63%!!) |
RADV-2CU 54.4->75.7 (+39%) | 4070Ti CONTAMINATED again (spread 3.8-7.9,
floors 3.3/4.1 vs clean 2.5/2.5 — box in use, discard) | M1 pending (mac off).
The one mat4 gather = 64 B x 53M verts ~= 3.4 GB/frame nominal; caches eat it
on NVIDIA, APUs pay ~+21/+37 ms.
THE KICKER: all 246,655 vienna parts share ONE identity Model aval — the heap
dedups the INPUT region but materializes 246k IDENTICAL ModelTrafoInv OUTPUT
regions, gathered from 246k distinct addresses. Same disease as the per-slot
ViewProj derive storm (8.8 ms/frame on 2-CU). ONE cure for both:
DERIVED-OUTPUT DEDUP by (recipe, constituent regions) — collapse output
regions when ALL constituents match; refcount like base uniform regions.
In this scene ModelTrafoInv AND ViewProjTrafo collapse to one hot cell each
(gathers cache-resident, derive dispatch ~1 group on camera move); scenes
with distinct trafos pay per DISTINCT VALUE, not per object — pay for
entropy, not object count. Top implementation priority.

### The TWO-PAIRING benchmark redesign + dense-store results (2026-07-09 late)
User redefined the comparison into matched pairs (the old single table mixed
costs): PAIR 1 "static" = baked one-draw vs heap with NO per-object trafos
(HEAP_STATIC=1, trafoStatic: decode + one global ViewProj) — pure
streaming-vs-gather. PAIR 2 "editable" = MDI with per-object Model/Inv as
INSTANCE-RATE attributes (baseline-mdi-inst, FirstInstance-indexed — the
strongest classic delivery) vs heap as-is (fair per-object NM).

Results (same-session floors, clean caches, dense store d0d22dfc):
PAIR 1: 5060 4.2-5.1 vs 4.4 (~1.0x) | 890M 47.3 vs 67.5 (1.43x) | RADV-2CU
27.7 vs 40.8 (1.47x) — the irreducible gather rent, now isolated.
PAIR 2: 5060 6.9 vs 4.4 (0.64x — HEAP WINS) | 890M 42.7 vs 82.1 (1.92x) |
RADV-2CU 30.3 vs 56.6 (1.87x).
KEY MECHANICAL FINDING: classic delivers per-object matrices ONCE PER
INSTANCE (hw fetch: +2.6-4.2 ms) — the heap delivers them ONCE PER VERTEX
(+14.6-16 ms) = ~215x the traffic at vienna's 72 tris/part. NEXT LEVER
(designed): promote hot derived outputs (NM/Model) INTO THE INSTANCE ROWS as
VALUES — per-instance hardware fetch matching classic exactly; needs the
derive kernel writing via a slot->row map + re-derive on relist.
Dense-store round context: 5060 lean 4.4 (better than pre-fairNM!), iGPU
75.7->57.9 (fairNM cost 21->3.5ms), 890M 96.8->85.6 (env ±10%; same-session
floors also hot). M1 pending (mac off).

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

### Entry store ("vals") v1 — built, measured, OPT-IN (2026-07-10, c075a13f)
Three-buffer directive implemented: arena (authoritative, derive outputs back
from the deleted dense store) + rows (instance attrs) + HeapVals (packed
per-entry uniforms, compile-time kind-separated lane layout, fan-out CS
refill). Full gauntlet pixel-exact in all variants. Two debugging traps:
(1) FShade splice-loses-storage-scope AGAIN in the vals gather (null[...]);
storage reads must sit literally in the returned quotation. (2) Cross-buffer
AdaptiveBuffer.ResizeInPlace (entryBuf resized from flushClassSlots) never
reallocates the handle — vals stuck at first-frame size, re-materialized
partitions wrote OOB → invisible, PERSISTENT; every capacity-tracking
MirrorBuffer needs its own Flush. Perf verdict (vienna d9):
no-vals 5060 3.8/5.2, RADV 35.3/75.0 (static/fairNM) | vals-SSBO 4.0/6.5,
57.0/311.7 | vals-attr 5.9/13.5, 55.6/306.8. The v1 per-frame brute fan-out
dominates APUs; replicating shared values forfeits dedup (vienna degenerate:
all Model identity); NVIDIA prefers SSBO over 12-18 attr lanes and needs no
vals at all (beats both floors). Dense-store reversion cost RADV fairNM
56.6->75.0 (5060 flat). v2 design: dirty-gated fan-out (zero dispatch clean
frames) + derived composites stay arena-side (compile-time rule); HEAP_VALS=1
enables meanwhile.

### Edit-latency: the 0.6ms fixed cost + the content-gate design (2026-07-10)
heap-editor-benchmark regression suite (0023 raw vs 0051 rerun) flagged inplace
3-22x / subdiv 7-11x; HEAP_EDIT_PROF=1 instrumentation (3a5d8e42) decomposed it:
compaction @build (fine), syncPages one-time, per-part staging linear 3-8us
(healthy). TRUE regression = ~0.6ms FIXED vk-side cost per edited frame:
updateResources walk (~0.7ms; every mirror marks every updater version via
Dependency -> all descriptor resources dirty) + unconditional command re-record
(marking is push + value-blind; resultAval identity-memoization measured
USELESS, reverted). Stats-mode truth: 0.63ms @k16 -> 3.6ms @k1024 (0023 sweep
3.17 @k1024 — big-k parity; the 3-22x table cells were single-shot noise).
FIX DESIGN v2 (user-refined; fresh session): adaptivity is NOT abandoned —
it is AGGREGATED. ONE gate adaptive object (depends on the updater, registered
as a single resource in the task's set) runs the flushes for exactly the
buffers whose existing dirty flags are set (csDirty/dirtyHeaders/staging
pending; dirty != changed, the gate checks). The mirrors themselves lose their
per-buffer Dependency wiring (the 0.6ms was an artifact of the
AdaptiveBuffer-with-Dependency pattern: every version marked ~50
buffer/descriptor nodes and the marked-but-unchanged walk IS the cost).
RESIZE = the only true handle change, rare + structural: bump the bucket
epoch when any buffer must grow -> HeapRenderObject identity changes -> full
re-prepare picks up fresh handles; ResizeInPlace inside the gate is then
legal (nobody needs notification). Expected ~0.2-0.3ms/edit; 0.05
(0023 mapped-mirror) unreachable without giving back device-local storage.
Acceptance: stats edit <=0.3ms @k<=64, gauntlet pixel-exact, vienna render
numbers unchanged.

### Edit-cost portable fixes — implementation spec (2026-07-10, no ReBAR)
Target: 0.6ms/edit -> ~0.2ms. Three independent pieces:
(1) COMMAND actual-change: content edits mark the command tree (push is
value-blind); updateCommands returns "was marked" and forces a re-record on
EMPTY deltas. Fix: resultAval identity-memoization (same array when membership
unchanged) + the top set-command's Update reports whether any reader delta was
non-empty; CommandTask re-records only then (~0.2ms back).
(2) AGGREGATE GATE + CONSTANT HANDLES: descriptor resources must stop waking
per version. Mirrors expose AVal.constant handles (constants never mark);
the bucket provider serves the CURRENT constant (prepared ROs capture theirs).
ONE gate node (Dependency=updater; registered via bucketRO.IsActive =
gate-map-true) runs all flushes (dirty flags already exist; recsVersion guard
96343e3d already skips clean record regen). GROWTH = bufEpoch bump inside the
updater: buildHeapRO + partROs rebuild with FRESH RO instances + new constants
-> prepare-cache miss -> new descriptors pull new handles (pow2-rare; watch
old/new RO resource-refcount overlap so buffers aren't destroyed mid-swap).
ResizeInPlace happens at updater end (before resultAval), so the first flush
after growth writes into the right-sized handle (~0.2ms back).
(3) WRITE BATCHING: headers/rows dirty ranges go through ONE staging chunk +
one copy submission (the arena ring pattern) instead of ~27us-per-Write calls
(~0.1ms back).
Acceptance: same-session stats A/B (env-gated switches during bring-up),
gauntlet pixel-exact, vienna render numbers unchanged. NEVER compare stats
across machine states (today's lesson).

### Edit-latency A/B result (2026-07-10, locked clocks) + bench protocol
recsVersion guard (96343e3d) + actual-change command reporting (2d094a94):
fixed base 0.5-0.64 -> 0.39-0.48 ms (1.2-1.55x, CI +-0.02-0.07), large-k
identical. MEASUREMENT PROTOCOL (mandatory for micro-benches): (1) lock GPU
clocks (sudo nvidia-smi -lgc 3090,3090; -rgc after) — tiny frames never ramp
the governor, idle 180MHz made steady frames 4.4ms instead of 0.55 and
poisoned an afternoon of numbers; (2) check nvidia-smi pmon for chrome/
sunshine/compute; (3) A/B within one session only. Remaining to ~0.2ms:
aggregate gate (readers 4->1) + write batching (spec above).

### Base-cost gate — locked-clock decomposition + variant map (2026-07-10 end)
Per-edit extra at locked clocks: +0.5ms updateResources (4-6 readers wake;
flush work only ~0.2 of it) + ~0.35ms dynWriter invocation. updateCommands/
re-record turned out STEADY-state (0.44ms/100ms window with zero edits; also
1 dirty reader + a re-record EVERY steady frame — separate cleanup item).
Gate variants mapped: (1) constant handles + epoch re-prepare — BLOCKED on
PrepareRenderObject caching per RO instance (growth needs fresh RO clones +
refcount-safe swap); (2) de-graph + imperative flush — breaks growth
notification (the vals lesson); (3) MERGE the five small mirrors
(headers/rows/slotPage/pickIds/shareRecs) into ONE buffer with section
offsets — growth machinery unchanged (one Dependency/reader/handle swap),
readers 5->1; needs SSBO SUB-RANGE bindings through the uniform provider
(Vulkan descriptor offsets fine; aardvark plumbing = the one unknown).
RECOMMENDED: variant 3, fresh session, gauntlet + locked-clock stats from
minute one. Also queue: the steady-frame 1-dirty-reader/re-record cleanup,
parallel ingest (the edits/frame cap: churn 74us/part single-threaded).

### Base-cost verdict — merge premise REFUTED by per-reader profiling (2026-07-10)
Per-reader timing in ResourceLocationSet.Update (HEAP_EDIT_PROF=1 now names
each dirty reader) showed the 4-6 wakes are NOT per-mirror BufferResources:
they are IndirectDrawCallResource x2 + DescriptorSetBindingResource +
BufferBindingResource, and the mirror flushes ride INSIDE the first poll
(descriptor-set 200us = arena+headers flush + ~30us machinery). Merging five
mirrors into one buffer would have kept all four wakes — DROPPED. Landed
instead (90b0663c, 09af70bf):
  * IndirectDrawCallResource compared before reporting — it returned
    changed=true on EVERY wake, making each edit look like a command change
    (now changed=false on content edits; no spurious re-record).
  * ensurePageROs gated on (storage.Count, partEpoch) like buildHeapRO —
    the pages x partitions dict walk (0.7ms first batch at 68k parts) now
    runs only on page growth / partition (de)materialization.
  * Vulkan buffer SUB-RANGE binding kept as infrastructure: a non-backend
    IBufferRange bound as SSBO/vertex becomes a BufferRangeDecorator
    (descriptor carries offset/size; vertex binding offsets honored); GL
    rejects storage ranges loudly. Unused today; enables section binding.
Steady frames confirmed CLEAN: zero dirty readers, reRecord=0.08ms/100ms
window is lap-accounting noise over ~900 no-op frames, not re-records (the
earlier \"re-record every steady frame\" reading was wrong).
Locked A/B (geforce-parts inplace, 3 runs): k=16 3.97→3.17 (first-batch),
k=64 1.17→1.10, k>=256 unchanged (within noise). Remaining per-batch cost is
REAL work: dynWriters ~22us/writer (parallel-ingest lever) + arena/header
staging + ~60us machinery. Full gauntlet green (autofields splitOk=false is
PRE-EXISTING on baseline: expects 5 output ROs/2 buckets, gets 2/1 — stale
expectation from the storage-first rework, worth a separate look).
