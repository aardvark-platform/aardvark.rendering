### 5.7.0-prerelease0052
- [Vulkan] buffer sub-range binding: a non-backend IBufferRange bound as SSBO or vertex attribute becomes a BufferRangeDecorator carrying (offset, size) into descriptors and vertex-bind offsets; GL rejects storage ranges loudly.
- [Vulkan] IndirectDrawCallResource reports a change only when the draw call actually differs — every heap edit looked like a command change and triggered pointless re-records.
- [Sg] Heap: pageRO sync gated on (pageCount, partEpoch) — content-only edits skip the pages×partitions walk (~0.7 ms first edit at 68k parts).

### 5.7.0-prerelease0051
- [Sg] Heap: derived-output DEDUP — slots whose recipe + canonical constituent sources match share ONE output region (pay per distinct value, not per object) + DENSE derive-output store (outputs packed tightly instead of scattered through slot groups; RADV-2CU vienna fairNM 75.7 -> 55.6 ms).
- [Sg] Heap: derive dispatch per OUTPUT SHARE, never per slot — 246k threads discovering they own nothing cost 7.1 ms/frame on 2-CU RADV (RGP-verified); vienna camera orbit now derives with ~1 thread.
- [Vulkan] pipeline layout: storage-buffer stage flags now include ALL shader stages of the interface — usage hidden inside shared GLSL helpers got too-narrow flags (VUID-07988, silently empty renders).

### 5.7.0-prerelease0050
- [Sg] Heap: INSTANCE-RATE RECORD ROWS — class-list entries carry the hot per-slot record fields (slot, vc, idxRef, attr refs) as VertexInputRate.Instance attributes; the hardware fetches them at wave launch, removing the ClassSlots->record dependent double-hop for typed AND dynamic partitions (vienna lean vs MDI floor: 5060 1.07x, 4070L 1.09x, M1 0.89x, 890M 1.69x, RADV-2CU 2.7x -> 2.0x).
- [Vulkan] async pipeline specialization: Destroy waits out an in-flight background compile (use-after-free reading freed pipeline state — SIGSEGV on MoltenVK).
- [Vulkan] skip spirv-opt on portability-subset devices — stripped buffer declarations broke MoltenVK's argument-buffer padding (measured performance-neutral on AMD and Metal).
- [Sg] HeapSpike: golden skips its bindless-textured phase where unbounded sampler arrays are unsupported (the heap uses the atlas there — covered by `atlasheap`).

### 5.7.0-prerelease0049
- [Sg] Heap: EXTENT-CLASS folding — typed partitions bake full/singleton per attribute into the spec constants, removing the header length load that serialized every attribute fetch behind a header round (RADV RDNA2 renderbench −15%, ratio to baked 2.31x -> 1.96x); in-place length flips (full <-> singleton) reclassify and migrate the slot.
- [Vulkan] EmptyTimeQuery when the queue family reports timestampValidBits = 0 (KosmicKrisp no longer device-losts on time queries).

### 5.7.0-prerelease0048
- [Vulkan] `AARDVARK_VULKAN=integrated|discrete|<name substring>` selects the physical device whenever the application supplies NO explicit IDeviceChooser (an explicit chooser always wins; unset = previous default behavior; an unmatchable value fails loudly listing the devices).

### 5.7.0-prerelease0047
<!-- publish retrigger: paket-lock fix -->
- [Sg] Heap: TYPED ASSIGNMENTS — a tiered "JIT" for the decode ladder. Per-attribute source typeIds become Vulkan specialization constants (inferred per bucket, never user-specified); slots partition by their tid vector: a dynamic partition (unspecialized, always-correct) stages new slots and hosts rare assignments, populous assignments (>= 64 slots) materialize into typed partitions whose pipelines fold the ladder to one arm — the header tid is never read. Renderbench vs unspecialized: RTX 5060 @700k 9.16 vs 13.86 ms (heap/baked = 0.99x — PARITY), M1 Max −38%, RADV RDNA2 −32% (3.3x -> 2.31x), Radeon 890M −24%.
- [Vulkan] specialization-constant plumbing: RenderObject.SpecConstants (aval<Map<string,int>>, name-resolved via the FShade `SpecConstants` scope interface, requires FShade >= 5.7.15), per-pipeline VkSpecializationInfo with spec values in the pipeline cache key.
- [Vulkan] async pipeline specialization: specialized pipelines compile on a background thread while the unspecialized twin renders — no hitch, no wrong pixel; borrowed handles never destroyed.
- [Sg] HeapSpike: `mixedtypes` probe (typed partitions pixel-verified through materialize/dematerialize/re-materialize) + `fsgather`/`churnperf` probes; renderbench `--churnperf` measures locality decay under churn.
- [Sg] Heap: content-only changes stopped recompiling VKVM commands and full-rewriting pick-id/slot-page mirrors (dirty-slot flushes) — carried over from 0046 line.

### 5.7.0-prerelease0046
- [Vulkan] HeapRenderObjectCommand no longer subscribes to the dispatch group-count avals at compile time (the pre-pass re-records dispatches every frame anyway) — content-only heap changes cause ZERO command recompiles; a command compiles once per structural generation.
- [Sg] Heap: pick-id and slot->page mirrors flush dirty slots as gap-merged sub-ranges instead of full rewrites per updater version (a recolor no longer re-uploads highWater*4 bytes twice).

### 5.7.0-prerelease0045
- [Vulkan] HeapRenderObjectCommand: recompile now RESETS the previous generation — the persistent VKVM stream was APPENDED on every recompile (one per heap membership version, e.g. per adaptive recolor) and the old derive DispatchCommands lingered in the pre-pass replay list. Every click permanently rendered the whole scene (and its derive pass) ONE MORE TIME per frame — the staircase-into-diashow degradation on all platforms (hekla-verified: 6 picks 27.7 -> 5.3 ms/frame).
- [Sg] Heap: dynamic geometry values that FIT the existing allocation re-stage IN PLACE (header count rewritten when it changes) — the select-toggle (full array <-> singleton) never reallocs, no free/alloc churn, no header re-bake.
- [Sg] HeapSpike: `dynpick` probe — repeated adaptive recolors under the two-task picking shape must not accumulate per-frame cost.

### 5.7.0-prerelease0044
- [Sg] Heap: `AARDVARK_HEAP_STATIC_GEOM=1` kill-switch disables the 0043 adaptive machinery (geometry re-upload writers, draw-call/pick-id/model-stack watchers) — snapshot-at-add behavior for bisecting regressions.

### 5.7.0-prerelease0043
- [Sg] Heap: no non-reactive values — everything declared adaptive is respected in O(change) amortized. Non-constant vertex-attribute and index buffer avals re-upload (in place on same size; free/realloc + per-slot header/record re-bake on size change — a length-1 singleton broadcast now flips live, the aardvark.dom recolor path). Draw-call shape (vertex/instance count), pick ids and the ModelTrafoStack structure got per-slot watchers.
- [Sg] HeapSpike: `dyngeom` probe — adaptive attrs/index/draw-calls validated pixel-by-pixel against a classic render (singleton flip, in-place edit, whole-geometry swap, fvc shrink).

### 5.7.0-prerelease0042
- [Sg] Heap: stage-while-clean fix — a second heap build over a SHARED storage that expands lazily inside another render task's pull (e.g. the 0041 pick partition's unpickable heap) staged into an already-flushed arena that nothing marked outdated again, so its buckets never appeared. Staging into a clean arena now schedules a post-evaluation Touch (one-frame latency, same as any add).
- [Sg] HeapSpike: `picksplit2` covers the two-task dom shape (was the gap that let 0041 ship with invisible unpickable partitions).

### 5.7.0-prerelease0041
- [Sg] Heap: `ofRenderObjectsPicking` partitions members by the `HeapPickId` marker — unpickable (Sg.NoEvents) members build a plain IsPickable=false heap over the same storage, so dom routes them into the base pass: pick-through semantics preserved, and no bucket is ever linked against a pick attachment it doesn't write (fixes the "[Vulkan] Could not get attribute 'PickId'" crash).
- [Vulkan] richer missing-attribute error: reports the pipeline's inputs and the surface effect's stages/inputs/outputs/uniforms.
- [Vulkan] `AARDVARK_VULKAN_LIBRARY` overrides the loaded Vulkan library (wins over `VulkanLoader.PreferMoltenVK`); fixed `PreferMoltenVK` setter collapsing the candidate list to only MoltenVK (`filter ((=) MoltenVK)` -> `((<>) MoltenVK)`) — required to run under KosmicKrisp/the Khronos loader on macOS.
- [Sg] HeapSpike: `picksplit`/`picksplit2` golden probes (pick-partition rendering), `HEAPSPIKE_NO_GPU_QUERY=1` renderbench fallback (KosmicKrisp beta device-loses on time queries).

### 5.7.0-prerelease0040
- [Sg] Heap: MoltenVK GPU-hang fix — derive-output/alignment holes broke upload-region merging, so a bulk flush issued O(parts) VkBufferCopy regions in one command (each = a Metal blit command -> watchdog). Holes are now staged as zero placeholders: 700k regions -> 12; also caps regions per copy at 16k and logs region counts. Bonus: ingest 11.2 -> 8.6 s @700k, mac upload 8x faster.
- [Sg] Heap: fixed a double dispose of the derive-records buffer in bucket teardown (the long-standing "negative reference count (-1)" warning).
- [Vulkan] AARDVARK_TRACE_RESOURCE_REFS=1 records per-resource creation/addref/dispose stacks and dumps the history when a reference count goes negative.

### 5.7.0-prerelease0039
- [Sg] Heap ingest allocation diet (profile-driven): static elemSize factory (per-call Func), box-free BucketKey equality (structural `=` boxed StencilMode/Mask/C4f/Box2i per lookup), interned per-part Symbols, typed Trafo3d constituent pack (no GetValueUntyped boxing), presized page dicts. renderbench @700k Release: 12.0 -> 11.2 s CPU ingest; heap-side garbage ~9.5 -> ~1.5 KB/part.

### 5.7.0-prerelease0038
- [Rendering] IBufferRuntime: multi-region buffer `Copy(src, dst, regions)` (one command, N regions) and `TryGetMappedPointer` (Vulkan: persistent VMA mapping; GL: none).
- [Sg] Heap: MIRROR-LESS arena — the host staging mirror is gone; writes stage into chained mapped host-visible ring chunks (never copied: mapped memory is write-combined) and flush as one multi-region copy per ordered batch. Geometry is a single source->ring memcpy (no intermediate byte[]).
- [Sg] Heap: page compaction is a device-side temp-buffer bounce (no CPU move, no full re-upload); same-cycle block reuse keeps copy order via batch splits.
- [Sg] Heap: O(1) segregated-fit + bump-tail allocator replaces the best-fit SortedSet manager (bump = miss path, no ingest/edit mode split, no first-edit cold start).
- [Sg] Heap ingest @700k renderbench: CPU 30.6 -> 18.8 s, GPU upload 2.6 -> 0.7 s (1.5 GB); render unchanged at 13.9 ms (1.50x). Steady-state host RAM drops by the arena payload size.
- [Sg] HeapSpike: new `churn` golden suite (compaction, same-cycle reuse, shrink/regrow — pixel-identical to classic).

### 5.7.0-prerelease0037
- [Sg] Heap: bulk-upload fix — derive OUTPUT regions (GPU-written, never staged) punched a hole into every part's staged range, so a bulk arena flush degenerated into one Write call per part (~60us each; 68 s for 1.5 GB at 700k parts). Ranges now merge across small gaps (staging is the authoritative mirror, so gap bytes are harmless): 300k Write calls -> 1, upload 26x faster, first-frame ingest at 700k parts ~99 s -> ~33 s. Render times unchanged.

### 5.7.0-prerelease0036
- [Sg] Heap: fixed cluster bulk-ingest regression — building a large scene accumulated one dirty range per added slot, turning the first ClassSlots flush into a million-upload storm (Vienna full-city first frame 121 -> 179 s on 0035). Past 2048 sparse ranges the flush collapses to ONE full rewrite; surviving sparse ranges sort + gap-merge like the draw mirror. Steady-state edits stay O(changed).

### 5.7.0-prerelease0035
- [Sg] Heap: CLUSTERED draw records — slots grouped into 51 padded size classes, one instanced record per (page, class) with a `ClassSlots` gl_InstanceIndex->slot indirection; restores warp residency for many-tiny-object scenes. renderbench @700k unique 10-100-tri objects: 18.3 -> 13.9 ms (1.98x -> 1.50x vs the baked single-draw floor); huge geometry (32 x 400k tris) at 0.99x parity. O(changed) membership/gate updates (capacity regions, single-int dirty writes); oversized slots keep exact per-slot records; Vulkan/MoltenVK non-instanced TriangleList buckets only; `HEAP_NO_CLUSTERS=1` kill switch.

### 5.7.0-prerelease0034
- [Sg] Heap render perf: field/attribute gathers hoisted to one let per (name, type) per stage; singleton broadcast via `min` instead of `% length`; decode ladder = 3 hot arms + one generic arm. renderbench (700k unique 10-100-tri objects): 22.0 -> 18.3 ms/frame (2.37x -> 1.98x vs baked single-draw floor).
- [Sg] Heap: all mirror buffers (headers, draw records, slot attrs, pick ids, chain links) are device-local — no BAR-size surprises on other GPUs.
- [Sg] Heap: binding a LENGTH-1 buffer to an attribute broadcasts like a singleton (tested pixel-identical to `SingleValueBuffer`; the latter stays the ADAPTIVE variant).
- [Sg] HeapSpike `renderbench`: synthetic GPU-time benchmark (heap vs baked floor, Vienna-shaped unique geometry, probes for records/packing/indirection/decode variants, `--window` turntable for Nsight).

### 5.7.0-prerelease0033
- [Sg] Heap API (breaking): storage-first + signature-deferred only — `runtime.CreateHeapStorage(?pageSizeInBytes)`, `Heap.ofRenderObjects storage`, `Heap.ofRenderObjectsAuto` (private storage, lives/dies with the heap), `Heap.ofRenderObjectsPicking storage deregister`, `Sg.heap storage`; the eager signature-taking variants and the `*Deferred*` names are removed.
- [Sg] Heap: one `HeapStorage` backs any number of heaps/passes (e.g. main + shadow) — allocations dedup in shared pages; compaction is page-level with per-bucket participants; a heap teardown releases only its own ref-counts.
- [Sg] Heap: bucket key covers ALL per-RO pipeline state projected onto the signature (per-written-attachment blend, depth/stencil only when the signature has them, RenderPass, viewport/scissor); the bucket RO is baked entirely from key values — nothing merges on an arbitrary member.
- [Sg] Heap: per-RO KeyWatcher dirty-tracking — a pipeline-state aval flip re-keys only the affected ROs (no global regroup path anymore).
- [Sg] Heap: compaction fixes — double regions keep 8-byte alignment, derived-output/constituent/chain-fold blocks are re-seated (were orphaned), header rewrites resolve per-slot pages.

### 5.7.0-prerelease0032
- [Sg] Heap: pick path defers its DCE-link to compile time (`ofRenderObjectsPickingDeferred` + `SignatureDependentRenderObject.IsPickable`) — links against the real pick signature (user semantics + PickId), so extra target attachments like a Normals G-buffer survive picking.

### 5.7.0-prerelease0031
- [Sg] Heap defers its signature-dependent build to compile time via `SignatureDependentRenderObject` (`Heap.ofRenderObjectsDeferred`) — renders into targets with extra attachments (e.g. a Normals G-buffer) without SIGABRT; opaque/transparent split preserves OIT, memoized per attachment-semantics so one arena is shared.

### 5.7.0-prerelease0030
- [Sg] Heap: `HeapRenderObject.IsPickable` is per-bucket, known by construction (the bucket's ROs carry HeapNode's `HeapPickId` marker) instead of the whole heap. A non-pick bucket (e.g. a `Sg.NoEvents` sub-scene) no longer advertises pickability, so the dom stops routing it into the `PickId` pass where the backend forced a phantom `PickId` vertex input → `could not get attribute 'PickId'`. Fixes mixed pickable/non-pickable heaps.

### 5.7.0-prerelease0029
- [Sg] Heap: `linkDCE` is bucket-aware — each bucket links only against the framebuffer attachments its effect actually writes, so a non-pick-writing bucket in a pickable heap isn't forced to output `PickId` (which was synthesised as a phantom vertex input → `RO missing shader input attribute PickId`). This is the real fix for that crash; 0028's claim was premature.

### 5.7.0-prerelease0028
- [Sg] Heap: dom O(1) picking composes cleanly offscreen (`RenderToPickable`) — `PickId` links as a shader output, not a spurious vertex input (0027's build threw `RO missing shader input attribute PickId`).
- [Vulkan] Fixed and improved descriptor pool and set management
- [Vulkan] Fixed checking for enabled features for pipeline queries
- [Vulkan] Fixed issue causing descriptor set not being updated after `Release()` and `Acquire()`
- Added disposed checks for compute and raytracing tasks

### 5.7.0-prerelease0027
- [Sg] Heap: eligibility respects effect DCE — a part is heapable if it supplies the inputs the effect's LIVE outputs need (read from the effect's dependency map vs the framebuffer signature), not every input the effect merely mentions; a read-but-dead attribute (e.g. `Flow` on non-water parts under a shared effect) no longer disqualifies a part and forces the whole scene to individual draws.

### 5.7.0-prerelease0026
- [Sg/Rendering] Heap: transparent geometry renders transparent again — the multi-page `HeapRenderObject` bundle (0024) hid each draw's `IsTransparent` from the OIT router (`TransparencyRenderTask` type-tests `RenderObject`), so heapified transparent buckets fell into the opaque pass; `HeapRenderObject` now exposes `IsTransparent` and the wrapper routes the bundle through all three OIT passes (transforming each inner draw, re-bundling with the same derives). Regression since 0024.

### 5.7.0-prerelease0025
- [Sg] Heap: device-local arena — was host-visible (PCIe); 33x faster full-scene render, byte-identical.
- [Sg] Heap: DCE effects against the framebuffer signature before heapification so read-but-dead fields aren't stored/gathered; `ofRenderObjects`/`Sg.heap` now take the signature.

### 5.7.0-prerelease0024
- [Sg] Heap: multi-page (paged-arena) scenes now render correctly — the per-page derive dispatches + draws are bundled into one `HeapRenderObject` (the backend records the derive as a compute pre-pass → compute→vertex barrier → draws in one submission, so each page draws against its own fresh derive), and the Model chain fold (`composeModel`) is now per-page (guarded by `HeapSlotPage = HeapPageId`, one dispatch per page binding that page's arena) instead of writing every slot's folded Model into page-0's arena. Fixes the multi-page smear/corruption on any heap spanning >1 page (>2 GB scenes, or a small `HEAP_PAGE_WORDS`); 1-page and orbit perf unchanged.
- [Sg] Heap: removed the obsolete standalone prototype entry points (`Heap.scene` / `instanced` / `bindless` / `derivedFp64` / `derivedChainFp64` / `flattenChains`) — early stepping-stones fully superseded by `ofRenderObjects`.

### 5.7.0-prerelease0023
- [Sg] Heap: `ofRenderObjects` allocates nothing until first use and frees everything (GPU + per-object CPU) when the last render task drops it, rebuilding cleanly on reuse — resource lifetime is ref-counted across tasks via a new backend-ignored `ActivationRenderObject` (carries only activate/deactivate). Fixes faults when a heap is reused by a task after a previous one was disposed.
- [Vulkan] ComputeTask: on portability-subset devices (MoltenVK/Metal) replay compute command streams INLINE into the primary command buffer instead of recording them into a reusable secondary and executing it via `vkCmdExecuteCommands` — which SIGSEGVs in MoltenVK (`MVKCmdExecuteCommands::setContent`). Mirrors the existing `CommandTask` `useInline` render-pass workaround (forceable everywhere via `AARDVARK_INLINE_RENDERPASS=1`). Fixes a hard crash when a heap (or any compute-using) scene renders on Apple Silicon. Non-portability backends are unchanged (still record + execute a secondary).
- [Rendering] Transparency wrapper: detect `VK_EXT_fragment_shader_interlock` / `GL_ARB_fragment_shader_interlock` at runtime and pick the OIT technique accordingly. The exact A-buffer path (interlocked per-pixel k-buffer) is used only where interlock is available; everywhere else the wrapper silently falls back to Weighted-Blended OIT. Previously the A-buffer path tried to run unconditionally — on AMD's Windows proprietary driver and most mobile / iGPU drivers (where interlock is absent) the result was a broken or crashing OIT pass. Set `AARDVARK_OIT=wb` to force Weighted-Blended on a backend that does support interlock (useful for A/B comparison or where the A-buffer path is known slow); `AARDVARK_OIT=abuffer` forces A-buffer where supported.
- [Rendering] Added `IRuntime.FragmentShaderInterlock : bool` — exposes whether ordered per-pixel critical sections are available to shaders. Used by the transparency wrapper to choose its OIT technique; useful for any other consumer that needs the same capability.
- [Rendering] A-buffer slot storage switched from a wide `W·Capacity × H` 2D image to a `W × H × Capacity` 2D-ARRAY image (FShade `UIntImage2dArray<rgba32ui>`). The flat layout hit AMD's `maxImageDimension2D` cap (16384) at window width ≥ 2048, causing outright image-creation failure (`Cannot create R32g32b32a32Uint image with size [20480, 1600, 1] (maximum is [16384, 16384, 1])`) and crashing the showcase on the AMD 890M Windows driver and the 9060XT. The 2D-array layout lifts the cap to per-axis (16k on AMD), matches the per-slot semantics naturally (one layer per fragment slot), and produces simpler indexing in the insert/resolve shaders.

### 5.7.0-prerelease0022
- [Sg] Heap: the per-slot derived-uniform / trafo-stack-collapse compute pass gained a **df32** (double-float = two float32) path, so it runs on backends with no shader `double` type (MoltenVK/Metal). `composeModel`/`composeModelInv`/`composeDerived` have df32 variants that read each constituent as a `(hi,lo)` pair from the f32 arena and fold in df32 (Veltkamp-split TwoProduct + fma-guarded sums); the CPU packs constituents and chain links as `(hi,lo)` to match. The path is chosen per bucket from the new `IRuntime.ShaderDouble`: real fp64 (M44d) where the backend has shaderFloat64, df32 where it does not — or forced everywhere via `Heap.ForceDf32` / the `AARDVARK_HEAP_DF32` env var (for desktop validation). The fp64 path is UNCHANGED and remains the default on desktop; with df32 forced on desktop Vulkan the heap golden suite is byte-identical to fp64 (maxDelta=0, sgheap/livechain/sgchain/… all pass). NOTE: df32 is desktop-validated (algorithm + FShade→SPIR-V lowering); validation on an actual Metal/MoltenVK device (fast-math / contraction survival of the error-free transforms) is still pending. A shader that requests a uniform at `double` precision DIRECTLY (in-shader `M44d`/`V3d` read) is unaffected and stays desktop-fp64-only for now.
- [Rendering] Added `IRuntime.ShaderDouble : bool` — whether shaders can use 64-bit floats (Vulkan = `shaderFloat64`; GL = GLSL ≥ 4.0; false on MoltenVK). Lets consumers branch on shader double support (the heap uses it to pick its df32 vs fp64 derive path).

### 5.7.0-prerelease0021
- [Sg] Heap: the per-slot fp64 derive compute is now RENDER-INTEGRATED — it runs as a pre-pass DispatchCmd in the SAME Vulkan submission as the draws (before the render pass, with a compute→vertex barrier) instead of a separate synchronous `CompileCompute` submission whose per-frame fence-wait dominated the orbit. A camera orbit over a 20k-link chain heap now renders in ~0.49 ms/frame (was ~1.29 ms at 0020; the raw-heap baseline is ~0.76 ms), with O(1) edits intact and the full golden suite byte-identical (maxDelta=0).
- [Vulkan] CommandTask: implemented `RuntimeCommand.DispatchCmd` (previously `failwith "compute not implemented"`). DispatchCommands are lifted out of the render-pass command stream and replayed in a pre-pass before `BeginRenderPass` (compute is illegal inside a render pass); each records bind/dispatch + barriers into a separate compute stream, re-recorded each frame so the replay always derefs the current descriptor/group-count. Non-compute render tasks are byte-identical (the pre-pass is empty unless DispatchCommands are present). The bucket draw RO stays a DIRECT top-level RO (wrapping the dynamic indirect bucket in a CommandRenderObject broke membership churn); the derive runs from a separate draw-less CommandRenderObject in the same render task.

### 5.7.0-prerelease0020
- [Sg] Heap: the per-slot derive compute uses PERSISTENT `CompileCompute` programs (recompiled only when the dispatch count changes) instead of a per-frame `runtime.Run` that rebuilt + submitted + fence-waited a throwaway command buffer — on a camera orbit (composeDerived re-runs each frame) this halved the per-frame CPU overhead.
- [Sg] Heap: the chainMode Model fold is re-dispatched ONLY when the chain structure or a link value changed (a `GrowChainLinks.Generation` counter), never on a pure camera move — the fold output is camera-independent, so an orbit skips it entirely.

### 5.7.0-prerelease0019
- [Sg] Heap: derived camera/normal composites (`ModelViewProjTrafo`, `ModelViewTrafo`, `ViewProjTrafo`, `NormalMatrix`, their `*Inv` forms, the trafo passthroughs) are produced ONCE PER SLOT by an fp64 GPU compute pass and gathered in-shader as a plain field — never composed per vertex. Replaces the previous per-vertex inline derivation (the 0.6→1.9ms regression). `ofRenderObjects` only; works in chain and non-chain mode.
- [Sg] Heap: chainMode folds each slot's Model link chain in fp64 directly into the arena Model constituent (forward AND, for `NormalMatrix`/`*Inv`, the backward half from the links' uploaded `Trafo3d.Backward`). NO shader ever calls `.Inverse`: an inverse is the uploaded backward half, an inverse-of-product is the reverse-order product, `NormalMatrix = transpose(Model⁻¹)` upper-3×3.
- [Sg] Heap: ranked recipe alternatives — `ModelViewProjTrafo` derives `Proj·View·Model` when the constituents are provided, else `ViewProjTrafo·Model` from a supplied combined `ViewProjTrafo`. The heap derives whatever it can from whatever the consumer provides, and never crashes on a missing or wrong-typed constituent (it stays a plain field, with a diagnostic).
- [Sg] Heap: bind user-managed unbounded sampler arrays (`textureArray uniform?Textures` indexed by a per-draw field) through, distinct from per-object bindless of single samplers.

### 5.7.0-prerelease0018
- [Sg] Heap: unified uniform model — every shader-consumed uniform is a ref-counted region keyed by source aval (no global/per-object distinction; a value shared by all draws is one slot with refcount = draw count). Removed the `globalsRO` delegation and UBO-global fall-through. A camera move marks ONE shared region, not N.
- [Sg] Heap: the SHADER is the source of truth — a uniform is stored as exactly the type it requests, the write converting the provided value (provided `M33d`→requested `M33f`, `Trafo3d`→`M44f`, …). A uniform requested at DOUBLE precision (`V2d`/`V3d`/`V4d`/`M33d`/`M44d`) is stored as REAL doubles via a native `HeapDataD` arena view (2 words/scalar, 8-byte aligned) — never f32-widened.
- [Sg] Heap: camera composites (`ViewProjTrafo`/`ModelViewProjTrafo`/`ModelViewTrafo`) are always DERIVED from their `Model`/`View`/`Proj` constituents and composed in fp64 (result converted to the requested type) — matches a CPU double `view*proj` bit-for-bit (golden `maxDelta=0`), and the shared constituents keep a camera move O(1).

### 5.7.0-prerelease0017
- [Sg] Heap: incremental vertex-pull gather — `IncrementalBucket.vtxGatherAval` refreshes only added/removed slots + non-constant sources per structural transaction (O(r)) instead of re-scanning all highWater×numAttrs slots (O(N)). Value-edit paths unchanged.
- [Vulkan] Bindless unbounded storage-buffer / sampler arrays degrade gracefully past their capacity: `StorageBuffers/CombinedImageSampler.GetDescriptors` bind `min(runtimeLen, capacity)` and warn once, instead of an IndexOutOfRange native abort when a bucket exceeds the array cap.
- [Sg] Heap: per-FRAME resource-leak regression golden (`chainleak`) + lock-free live-handle counters (Resource/DescriptorSet LiveCount) — guards the per-frame accumulation the per-scene lifetime test cannot see. Verified flat over thousands of frames at n≤100000.

### 5.7.0-prerelease0016
- [Sg] Heap: GPU trafo-chain wired into the LIVE Heap.ofRenderObjects ingest. A bucket whose effect consumes ModelTrafo and whose ROs expose the UNFOLDED "ModelTrafoStack" uniform (aval<aval<Trafo3d>[]>) composes each slot's ModelTrafo ON THE GPU from a growable, deduped link arena (constant links value-deduped, dynamic links identity-deduped, free-listed) instead of packing a per-slot CPU-folded ModelTrafo region. Works for ARBITRARY chain depth; a shared/constant ancestor link collapses to ONE arena slot across all leaves and editing one link re-folds only on the GPU (O(1) over the subtree). ROs without a stack keep the single-ModelTrafo arena path (graceful). New livechain/livechaindeep/sgchain/domboxchain/hierchain golden tests; AARDVARK_HEAP_NOCHAIN=1 forces the folded path for A/B.
- [Sg] Simple/render Sg exposes the unfolded "ModelTrafoStack" uniform alongside the folded ModelTrafo (TraversalStateUniformProvider) — the GPU trafo-chain consumer engages for ordinary render-Sg scenes (Sg.trafo chains) exactly as for the dom Sg.

### 5.7.0-prerelease0015
- [Sg] Heap: geometry dedup looks through CONSTANT buffer avals to the value level — fresh per-leaf BufferView/aval wrappers around the SAME underlying array now share packed geometry (ArrayBuffer.Equals = array ReferenceEquals); key widened to (array-or-buffer source, byte offset, format typeId). Naturally-written per-node Primitives.Box scenes dedup with no authoring discipline. New geomvalue golden test.
- [Sg] Heap: NON-INDEXED draws are eligible — header index-cell sentinel (-1) makes the vertex fetch use gl_VertexIndex directly; indexed and non-indexed members ride the same bucket. Removes the old supply-Indices passthrough. New noindex golden test; makes Primitives.Box (and any non-indexed geometry) heap-eligible.

### 5.7.0-prerelease0014
- [Sg] Heap: structural-version floor attributed and compressed (131 -> ~105us per add+remove at n=20k; dense churn r=2000: ~70 -> ~60ms): updater consumes true set deltas (GetReader/GetChanges) instead of snapshot+computeDelta; constant uniform/singleton sources stage once without RegionWriter subscriptions; single-pass RO classification with cached sampler/size/feature lookups; hand-rolled comparers for geometry-dedup and mode-key interning replace generic structural hashing. Remainder is attributed (classification 16us, slot ops 15us, two sub-range uploads 12us, two resource-reader updates 15us, transact marking 29us) — further compression is backend surgery, documented.

### 5.7.0-prerelease0013
- [Sg] Heap: typeId-branching attribute decoder — the shader converts each allocation's source type (f32/i32/f64 x1-4, normalized C4b incl. BGRA layout fix, f64 bit-decoded without shaderFloat64) to the effect's input type at fetch (widen with (0,0,0,1), narrow, normalize, cast). Element types leave the host bucket key: mixed-format objects (C4b singletons, C4f buffers, V3d vs V4f positions) share ONE bucket. Unsupported pairs -> precise Heap.Diagnostics. GPU cost +2-3% on the gather, CPU slightly improved.

### 5.7.0-prerelease0012
- [Sg] Heap: storage-decoded geometry — the fixed-function vertex path is GONE. Attributes and INDICES decode from the storage arena via per-allocation headers (typeId/length/stride; wombat-style); draws are non-indexed; singletons (SingleValueBuffer, e.g. Primitives.Box colors) are length-1 allocations decoded by the same fetch and ride the same bucket as real buffers; u16/u32 indices mix per bucket. GPU-resident buffers stay zero-copy (bindless array). Measured: GPU time IMPROVES 25-28% despite the post-transform-cache loss; CPU churn improves (eligibility probing memoized).
- [Sg] `Heap.Diagnostics` — opt-in, deduped, actionable log lines for every pass-through reason (+ `Heap.diagnosticMessages()`).

### 5.7.0-prerelease0011
- [Sg] `Sg.heap : ISg -> ISg` — collapse a subtree through the heap with one combinator (Ag + ISimpleSg dispatch paths; non-heapable objects pass through). Pixel-identity golden-tested on both paths.
- [Sg] `HeapConfig.Enabled` removed — calling `Heap.ofRenderObjects`/`Sg.heap` IS the opt-in. Remaining knobs live in `module Heap`.

### 5.7.0-prerelease0010
- [Sg] `Heap.ofRenderObjects` takes no name set anymore — auto-detected per-draw fields are THE behavior (per-draw fields = the uniforms your objects supply; shared avals dedup to one arena region). The explicit-names variant and `ofRenderObjectsAuto` are removed.

### 5.7.0-prerelease0009
- [Sg] `Heap.ofRenderObjectsAuto` — per-draw heap fields are detected automatically: every effect-consumed (incl. derived-rule bases), packable uniform supplied by the RO's own provider becomes a field; scene-scope uniforms stay ordinary. Field sets are interned into the bucket key; shared avals dedup to one arena region. Explicit-names `ofRenderObjects` unchanged as the restricting variant. New `autofields` golden test: classic vs explicit vs auto pixel-identical.

### 5.7.0-prerelease0008
- [Sg] Heap: buffer lifetimes go through `IAdaptiveResource` — all heap buffer avals (arena, draw-record/header/instance mirrors, HeapScene data) use `AdaptiveResource.mapNonAdaptive` instead of interface-stripping `AVal.map`, so the render task's Acquire/Release refcounting destroys a disposed bucket's GPU buffers. New `lifetime` golden test (30 create/render/dispose cycles, VMA allocation stats): pre-fix +6 allocations/cycle, post-fix returns to a zero baseline every cycle. New `Device.MemoryStatistics` for the test.

### 5.7.0-prerelease0007
- [Sg] Heap: the last O(N)-per-structural-version effort is gone — draw records (indirect), headers and the MoltenVK instance buffer live in stable GPU-resident `MirrorBuffer`s (identity-stable backend buffers, dirty-sub-range uploads from the delta pass, gap-merged); `IsActive` toggles write single cells (O(toggled)). Both backends bind the buffers directly (indexed indirect record = `DrawCallInfo.ToggleIndexed`; no GL fallback needed). Measured per-version overhead vs population: ~2.8 ms -> ~0.2 ms at 200k objects, flat in N.
- [Vulkan] DescriptorSetLayout: Debug assert updated for unbounded descriptor arrays (binding numbers must be strictly increasing; the prefix-sum-of-DescriptorCounts invariant predates descriptor arrays).

### 5.7.0-prerelease0006
- [Sg] Heap: the hand-rolled range allocator is gone — all four heap allocation sites (geometry vertex/index ranges, arena uniform regions, instance-attribute ranges) now use the existing generic `Management.MemoryManager` (size-sorted free list, O(log n) best-fit, both-neighbor coalescing) over a virtual `Memory` instance; a thin wrapper adds the live/extent counters the compaction trigger needs. Net code removal; behavior and `geomdrift`/`geomchurn` guarantees unchanged.

### 5.7.0-prerelease0005
- [Sg] Heap: proper space reclamation — one shared coalescing `RangeAllocator` (sorted free ranges, coalesce-on-free, best-fit with split, cursor retraction at the tail) replaces the exact-size free lists at all four sites: geometry vertex ranges, geometry index ranges, arena uniform regions, instance-attribute ranges. Ragged host geometries are zero-padded to the longest attribute so their ranges are reusable instead of leaked.
- [Sg] Heap: automatic threshold-triggered compaction — after removals, a buffer whose live bytes < 1/2 cursor AND waste > `HeapConfig.compactionWasteFloorBytes` (default 4 MB) compacts in the same delta pass: staging memmove, header/draw-record/region-offset rewrite, GPU shrink. O(live) per fire, amortized like growth doubling. New `geomdrift` golden test (random-size, random-instance churn, 320 frames): buffers bounded by 2.5x live throughout, 0 pixel delta; `geomchurn` stays byte-flat with zero compactions (exact reuse short-circuits first).

### 5.7.0-prerelease0004
- [Sg] Heap: ALL bucket kinds are now incremental (`buildBucket` removed) — bindless vertex-pull geometry (growable HeapVertexData with slot reuse), bindless texture arrays (refcounted distinct-texture dedup with stable indices), atlas buckets (one AtlasPool per bucket lifetime, per-delta Acquire/Release), instanced ROs incl. the MoltenVK slot-attribute fallback (per-size instance-range freelist). Textured-bucket add+remove: ~337 ms -> ~1.2 ms; bindless-geometry: ~299 ms -> ~0.56 ms.
- [Sg] Heap hygiene sweep: no transact/MarkOutdated during adaptive evaluation anywhere in heap code (deferred arena resize via `AdaptiveBuffer.ResizeInPlace`; pull-published atlas pages); buckets no longer retain their first RO after it leaves; packed geometry ranges are refcounted and reused (byte-flat under distinct-geometry churn, new `geomchurn` golden test); dynamic-mode buckets bake pipeline state from the bucket key (a member's mode change moves it between buckets instead of bending the bucket it leaves); `HeapScene` double-remove made idempotent and disposal added.
- [Vulkan] `CreateStorageBufferArray` was non-adaptive (forced once) — slot rebinds under churn never reached the descriptor set; now fully adaptive with per-element versions and buffer-identity dedup.
- [Vulkan] Unbounded-array descriptor ceiling (1024) documented at `DescriptorSetLayout` with what lifting it requires.

### 5.7.0-prerelease0003
- [Sg] `Heap.ofRenderObjects` is now INCREMENTAL for set-membership changes: simple buckets (all-host geometry, no samplers, non-instanced) keep a persistent per-bucket cache — slot freelist with InstanceCount=0 tombstones, refcounted per-aval arena regions, append-only packed geometry with reference-stable buffers, stable bucket-RO identity — and process set deltas instead of rebuilding from the snapshot. One add+remove in a 20k-object bucket: ~360 ms -> ~1.45 ms (~15 us per changed object, linear). Non-simple buckets (atlas / bindless / instanced) keep the rebuild path but now rebuild only when their own membership changes. Token-reactive mode-rule re-bucketing, reactive IsActive gating and all HeapSpike golden tests preserved.

### 5.7.0-prerelease0002
- [Sg] Heap runtime: bucketed indirect-multidraw of render objects sharing an effect, per-draw uniforms gathered from a shared SSBO arena. Opt-in via `HeapConfig.Enabled <- true` (default OFF). `Heap.ofRenderObjects` collapses an `aset<IRenderObject>` into a few bucket render objects with dirty-tracked sparse arena updates; `HeapScene` is an imperative growable single bucket with O(1) Add/Remove. Bindless geometry through `HeapVertexData`; bindless textures with a Vulkan-1.0 / MoltenVK atlas-page fallback (`HeapAtlas`, `HeapAtlasPool`, reactive multi-page with LRU and dedup). Currently Vulkan-only.
- [Sg] `ISimpleSg` direct-construction render path: every `Sg.*` node implements an explicit `TraversalState` carrying the Ag-attribute set; ~7-8× faster SG resolution at 20k objects vs the legacy attribute-grammar entry. On by default (`SimpleConfig.Enabled = true`); set `AARDVARK_SIMPLE_SG=0` (and `SimpleConfig.Enabled <- false`) to fall back to the Ag path.
- [Rendering] A-buffer order-independent transparency: exact per-pixel k-buffer via `GL_ARB_fragment_shader_interlock`, with per-sample mask resolve for MSAA. `TransparencyRenderTask.technique` toggles between the existing `WeightedBlended` and the new `ABuffer` path. Diagnostic via `AARDVARK_ABUFFER_DEBUG=1` (coverage) / `=2` (sample-id).
- [Vulkan] MSAA: `MultisampleState` honours `ShaderProgram.SampleShading`; `minSampleShading=1.0` when sample-shading enabled. `gl_SampleID`/`gl_SamplePosition` detection fixed in `ShaderProgram.fragmentInfo`.
- [Vulkan] Fragment-shader interlock + storage-write render-pass dependency wiring.
- [Vulkan] Fix framebuffer copy on multisampled images (A-buffer OIT + MSAA).
- [Vulkan] MoltenVK upload wedge fixed by pinning `VkBufferCopy` (F# tail-call escape, dotnet/fsharp#18689).
- [Vulkan] `&&` (byref-of-temp) → `fixed &` sweep across Core sync/commands/device/queues, Memory allocator/external-memory, resources (image/sampler/pipeline/swapchain), raytracing (accel-struct/micromap/pipeline) — dotnet/fsharp#18689 compatibility.
- [Vulkan] `ResourceLocationSet.Use`: reuse `invalidScratch` HashSet + skip `transact` when nothing invalid.
- [Vulkan] Conservative-state `pNext` use-after-move fix (sub-struct stored in unmanaged memory).
- [Application] `VulkanLoader.PreferMoltenVK` to load aardvark's bundled MoltenVK over a system Vulkan SDK on macOS.
- [GL] Added `Context.GetDebugMessages`.
- [GL] Added `Context.OnDispose`.
- [GL] Represent `NullTexture` with a proper texture object instead of a texture with handle 0.
- [deps] FShade `5.7.3 → 5.7.9`: GLSL binding-allocator clamps unbounded-array step to 1 (was decrementing the global counter); unbounded sampler / image / SSBO arrays via `count = -1`; `nonuniformEXT` for bindless storage-buffer / sampler array indexing; storage-buffer `ssbCount` reported in the interface.

### 5.7.0-prerelease0001
- [Vulkan] Reworked loading of Vulkan library
- [Vulkan] Added support for MoltenVK
- Added ComputeCommand.SetConstantCmd
- Added ComputeCommand.DispatchIndirectCmd

### 5.6.5
- Fixed support for 64-bit attributes and uniforms
- Fixed various issues with `GlobalBoundingBox` and `LocalBoundingBox`. Both attributes are now equivalent.
- Fixed `PickObjects` attribute for render nodes with `TriangleStrip` and `TriangleAdjacencyList` topologies
- Added `PickTree` intersection methods using `ValueOption` rather than `Option`
- Made `IBuffer.ToArray` and `BufferView.download` robust to out-of-range arguments
- [Sg] Fixed broken Ag rule for `FaceVertexCount`
- [Sg] Added `rotation` and `rotation'`
- Replaced `Marshal.Copy`/`Marshal.Set` calls in `UniformWriters.NewWriters` with managed `Buffer.MemoryCopy`/`Span<byte>.Clear` so wasm builds (which lack `msvcrt.dll`/`libc`) can write array uniforms

### 5.6.4
- [GL] Dispose MultimediaTimer in LodRenderer to avoid resource exhaustion
- [GL] Fixed M22f, M23f, M33f geometry attributes in LodRenderer

### 5.6.3
- [Vulkan] Improved handling of unavailable or disabled features
- [Vulkan] Added missing synchronization for buffer uploads
- [Vulkan] Query format properties on demand
- [Vulkan] Removed logging of shader interface
- [Application] Dispose window when using `show` builder
- [Application.OpenVR.GL] Fixed `samples` parameter of `OpenGlVRApplicationLayered` constructor being ignored
- [Application.OpenVR.GL] Fixed copy for non-multisampled framebuffers
- [Sg] Added `Sg.uniforms`

### 5.6.2
- Added support for enum types as vertex and instance attributes
- Implemented download / upload for PixImage as 3D slices
- Improved support for non-2D framebuffer outputs
- [GL] Create debug context when debug output is enabled
- [GL] Fixed issue with clearing textures with unsigned integer formats
- [Vulkan] Fixed regression with swapchain creation

### 5.6.1
- [GeometryPool] culling shader workaround (FShade write bug)
- [Vulkan] Fixed swapchain creation for Wayland
- [GLFW] Avoid printing error due to unsupported window icon

### 5.6.0
- https://github.com/aardvark-platform/aardvark.rendering/wiki/Aardvark-Rendering-5.6-changelog

### 5.6.0-prerelease0010
- Changed `DrawCalls.Direct` from list to array
- Reworked `BufferView` constructors
- Reworked `TexureParams` as enum and improved documentation
- Added size and format validation for `PixTexture2d` and `PixTextureCube`
- Added offset parameter for indirect buffers
- Added framebuffer clear extensions
- Added support for dynamic viewport and scissor
- Added `discardOnResize` parameter for AdaptiveBuffer
- Added support for color-based vertex attributes
- Added Aardvark.Rendering.ImGui
- [Raytracing] Reworked `GeometryMode`
- [Raytracing] Added comments and overloads for geometry-related types
- [Sg] Fixed automatic computation of `FaceVertexCount`
- [Sg] Added `Sg.indirectDraw'`
- [Vulkan] Fixed interleaved attributes
- [Vulkan] Improved detection of debug printf messages
- [Vulkan] Disabled render task recompilation message for `DebugLevel.Normal`
- [Vulkan] Fixed computation of shader file cache name
- [GL] Deleted old render task implementation
- [GL] Fixed handling of nested runtime commands
- [GL] Fixed `AbstractRenderTask.HookRenderObject` with render commands

### 5.6.0-prerelease0009
- [Vulkan] Fixed memory leaks related to cstr
- [Vulkan] Added micromap pipeline creation flag
- [Application.Utilities] Removed preventDisposal parameter in ISimpleRenderWindow.Run

### 5.6.0-prerelease0008
- [Vulkan] Fixed case names of enums with a version suffix
- [Vulkan] Added support for opacity micromaps
- [Vulkan] Improved detection of debug printf messages
- [Vulkan] Added AdaptiveBoundingBoxes.FromCenterAndRadius
- [Vulkan] Added unmanaged constraints for TraceObject methods

### 5.6.0-prerelease0007
- [Vulkan] Added check if color attachment supports blending
- [Vulkan] Added ValidationLayerConfig.RaytracingValidation
- [Vulkan] Added support for custom sampler border colors
- [Vulkan] Added support for acceleration structure compaction
- [Vulkan] Renamed RaytracingSceneDescription to RaytracingScene
- [Vulkan] Fixed compatibility check for acceleration structure updates

### 5.6.0-prerelease0006
- Added IRuntime.SupportsPositionFetch and SupportsInvocationReorder
- [Vulkan] Added support for VK_NV_ray_tracing_invocation_reorder
- [Vulkan] Added support for VK_NV_ray_tracing_validation
- [GPGPU] Fixed FShade-related issue in Jpeg compressor

### 5.6.0-prerelease0005
- Updated FShade to 5.7.0-prerelease0003

### 5.6.0-prerelease0004
- Changed type of Handle property in resource interfaces to uint64
- Removed obsolete code
- Removed RenderTask.cache and RenderTask.postProcess
- Changed return type of IUniformProvider.TryGetUniform and IAttributeProvider.TryGetAttribute to ValueOption
- Changed return type of IGeometryPool.TryGetBufferView to ValueOption
- Replaced CreateTextureAttachment() with GetOutputView()
- Removed INativeBuffer Pin() and Unpin()
- Optimized constant path in IManagedBuffer.Add
- Added IBuffer.ToArray
- [ManagedPool] Reworked handling of attribute dictionaries (PooledGeometry is removed)
- [ManagedTracePool] Added uniform provider for storage buffers
- [Raytracing] Various optimizations and API adjustments
- [Raytracing] Added IndexType.Int16 and IndexType.Int32
- [GL] Removed Type.GLSize

### 5.6.0-prerelease0003
- Added validation for framebuffer signatures of prepared render objects
- Added debug labels for render tasks, textures, buffers, and render buffers
- [Vulkan] Fixed reference counting in device token
- [Vulkan] Fixed alignment and size issues with empty buffers
- [Vulkan] Added debug config flag for generating shader debug info
- [Vulkan] Replaced obsolete VK_EXT_validation_features with VK_EXT_layer_settings
- [Vulkan] Fixed ImageSamplerArrayResource leaking deltas
- [GL] Removed UnmanagedFunctions.wrap usage

### 5.6.0-prerelease0002
- Switched to Aardvark.Data.Assimp
- [Vulkan] Fixed infinite recursion in external memory allocation
- [Vulkan] Removed warning when allocating external memory fails
- [Vulkan] Made ILogger and Logger internal
- [Vulkan] Added device chooser API
- [Vulkan] Fixed raytracing buffer alignment issues
- [Sg] Fixed issue with multiple dynamic sampler states
- [Sg] Simplified samplerState applicator

### 5.6.0-prerelease0001
- [Vulkan] Improved queue submission
- [Vulkan] Integrated VMA for memory management

### 5.5.17  
- [GL] fixed quadbuffer stereo rendering
- [GL] Fixed blend modes not being toggled properly per attachment
- [Vulkan] Fixed VK_ERROR_OUT_OF_POOL_MEMORY error on some platforms when using raytracing
- [Vulkan] Added check for format features when creating a render pass

### 5.5.16
- now using `glEnablei/glDisablei` for BlendModes.

### 5.5.15
- updated package FSharp.Data.Adaptive 1.2.19
- Improved error reporting for buffer creation and updating
- [Vulkan] Respect export flag for empty buffers
- [Vulkan] Print detailed memory information when allocation fails (uses VK_EXT_memory_budget if available)
- [Vulkan] Avoid passing VkExportMemoryAllocateInfo when not exporting memory
- [Vulkan] Added Device.PrintMemoryUsage()

### 5.5.14
- updated dependency FSharp.Data.Adaptive 1.2.19
- [Vulkan] Changed config location of device chooser to Aardvark cache directory
- Added DownloadDepth() and DownloadStencil() overloads for IBackendTexture with an explicit target parameter
- Fixed simpleLighting and stableLight shaders to use ambient term
- Fixed race conditions with compact buffers and Vulkan image sampler arrays
- Improved error reporting for null values as textures

### 5.5.13
- [OpenGL/WPF/ThreadedRenderControl] re-activate classic render control

### 5.5.12
- [OpenGL/WPF/ThreadedRenderControl] frame throttle

### 5.5.11
- [LodTreeNode] uniforms concurrent access fix

### 5.5.10
- [OpenGL/WPF/ThreadedRenderControl] fixed resize

### 5.5.9
- [OpenGL/WPF] Threaded rendering control

### 5.5.8
- MultiTreeNode: fixed picking

### 5.5.7
- LodTreeNode: added MultiTreeNode support

### 5.5.6
- [Assimp] Animations: fixed quaternion interpretation

### 5.5.5
- OpenGL/WPF control uses tasks for rendering (avoiding stack-inlining due to STAThread)

### 5.5.4
- OpenGL/WPF control uses `OnPainRender` again

### 5.5.3
- OpenGL/WPF control no longer uses `OnPainRender` by default

### 5.5.2
- added PoolGeometry as alternative to ManagedPool with SymbolDict (more efficient attribute lookups)
- added reference equality check to BufferView Equals
- using SortedSetExt value option methods
- avoid exception in upload/download/write when count=0
- marked MemoryManagementUtilities.FreeList as obsolete (duplicate of Aardvark.Base FreeList)
- updated Aardvark.Base to 5.3.5
- updated Aardvark.Build to 2.0.2
- updated aardpack to 2.0.3

### 5.5.1
- Improved adaptive converter caching
- Optimized GCHandle.Alloc usage
- [Assimp] Fixed remap.xml for Linux
- [GL] Optimized construction of attribute bindings
- [Text] Use FramebufferLayout as surface cache key
- [Text] Added type aliases

### 5.5.0
- https://github.com/aardvark-platform/aardvark.rendering/wiki/Aardvark-Rendering-5.5-changelog

### 5.5.0-prerelease0002
- Renamed `PixImageCube` to `PixCube`
- Renamed `Aardvark.SceneGraph.IO` to `Aardvark.SceneGraph.Assimp`

### 5.5.0-prerelease0001
- Initial prerelease

### 5.4.12
- [GL] Fixed potential memory leak after ContextHandle is disposed
- Optimized generic dispatch
- Fixed potential leaks with ConcurrentDictionary.GetOrAdd

### 5.4.11
- [Application.WPF.GL] SharingRenderControl implementation now uses Silk.NET.Direct3D9 instead of SharpDX
- Removed SharpDX dependency
- Re-added dynamic shader caches
- Fixed multi-threading issue in PrimitiveValueConverter
- [Sg] Use single value attributes for IndexedGeometry
- [IndexedGeometry] Fixed Union() and added ToIndexed() overload
- [IndexedGeometry] Added overload Clone() for deep copy

### 5.4.10
- [OpenVR] changed GL texture submit to 2 textures (previously side by side, issue with Quest 3)
- [GL] Improved querying of supported sample counts
- [GL] Fixed double disposal of Context
- [GLFW] Fixed OpenTK context interop
- [Vulkan] Fixed conservative raster validation error

### 5.4.10-prerelease0006
- rebuild glvm for ARM64

### 5.4.10-prerelease0005
- [Text] added option to disable sample shading

### 5.4.10-prerelease0004
- [GL] added flag to disable multidraw (experimental)

### 5.4.10-prerelease0003
- [FontResolve] fixed null family name failure

### 5.4.10-prerelease0002
- [PathSegment] minor fixes

### 5.4.10-prerelease0001
- [Text] improved Font resolver for Windows and MacOS
- [PathSegment] fixed several PathSegment tools and added a few new ones

### 5.4.9
- [LodRenderer] Handle exceptions in background threads
- [GL] Implemented GLSL shader caches for platforms that do not support program binaries (e.g. MacOS)

### 5.4.9-prerelease0001
- [GL] experimental support for quad-buffer stereo(is back again?)

### 5.4.8
- [GL] Fixed locking order of GlobalResourceLock and context locks to avoid potential deadlocks
- [GL] Added workaround for layered rendering and GLSL < 430
- [GL] Made context creation and sharing more robust (see RuntimeConfig.RobustContextSharing)
- [GL] Improved disposal of ContextHandle
- [GLVM / VKVM] Updated ARM64 binaries
- [GLFW] Fixed context resource leaks
- [GLFW] Reset GetCurrentContext on disposal
- [WinForms / WPF] Removed double dispose of context

### 5.4.7
- Fixed Frustum.withAspect and Frustum.withHorizontalFieldOfViewInDegrees
- [GL] Fixed InvalidEnum error due to GL_POINT_SPRITE
- [GL] Removed validation via proxy textures (resulted in errors on AMD with multisampled textures)
- [GL] Removed swizzle for multisampled textures (not supported)
- [GL] Added simple parameter device limit checks for textures and renderbuffers
- [GL] Improved texture memory usage tracking
- [GL] Made retrieval of program binaries more robust
- [GL] Improved driver information and error formatting
- [GL] Disabled Dispose() for Program
- [GL] Fixed resource leaks in ContextHandleOpenTK.create
- [GL] Fixed ComputeCommand.SetBufferCmd
- [GL] Fixed issue with texture targets and multisampling
- [Vulkan] Fixed swapchain creation if maxImages is zero
- [Vulkan] Fixed issue with image format queries and external memory
- [Vulkan] Improved error formatting
- [GLFW] Use no error context only when indicated by debug config
- Added IRenderTask.GetRuntime() and IRenderTask.GetFramebufferSignature()

### 5.4.6
- [ContextHandles] GL.Enable(EnableCap.PointSprite)
- [ManagedPool] Avoid evaluating draw call set if not active
- Fix BlendMode.Blend source alpha factor

### 5.4.5
- [GeometryPool] Fixed wrongly disposed shader caches

### 5.4.4
- Exceptions are caught and logged when updating shaders with the debugger
- [GL] Fixed resource management issue with compute shaders and shader debugger, resulting in invalid operation errors
- [GL] Fixed issue with preparing exported buffers
- [GL] Print before debugger break in DebugCommandStream
- [Vulkan] Fixed validation error related to memory export

### 5.4.3
- Updated to FShade 5.5
- Added support for debugging raytracing effects and compute shaders with the FShade ShaderDebugger
- Fixed issues with dirty sets in OrderedCommand (GL / Vulkan)
- [GL] Increased verbosity level of outdated resource warning
- [GL] Improved warning about missing internal format query support 

### 5.4.2
- [Vulkan] Fixed issue in SBT update
- [Sg] Added C# Surface overload
- Improved shade compile error reporting and code printing

### 5.4.1
- Fixed net6.0 target for WinForms and WPF

### 5.4.0
- https://github.com/aardvark-platform/aardvark.docs/wiki/Aardvark-Rendering-5.4-changelog

### 5.4.0-prerelease0004
- Renamed NewInputBinding to CreateInputBinding
- Reverted renaming of provider ofDict methods
- Restored IAttributeProvider.All
- Added Signature property to ManagedPool and ManagedTracePool
- Added obsolete extensions for renamed buffer copy methods
- [GL] Removed duplicate context tracking
- [Vulkan] Fixed aspect for depth / stencil samplers
- [Vulkan] Fixed shader stage computation for dynamic effects

### 5.4.0-prerelease0003
- Restored IComputeRuntime.ContextLock

### 5.4.0-prerelease0002
- Added validation for sampler state translation
- Added texture filter reduction
- Added Blit, reworked Copy and ResolveMultisamples
- [GL] Added RuntimeConfig.AllowConcurrentResourceAccess
- [Vulkan] Fixed issue with concurrent eager destroy

### 5.4.0-prerelease0001
- Initial prerelease for 5.4

### 5.3.8
- [GL] Fixed update issues with OrderedCommand

### 5.3.7
- Various optimizations
- Added RenderTask.renderTo variants with adaptive clear values
- Fixed RenderPass.before to respect given order
- [AbstractRenderTask] Make Dispose mutually exclusive with Update and Run
- [GL] Fixed ObjectDisposedException related to invalid epilogue prev pointer
- [GLFW] Fixed vsync initialization
- [GLFW] Disabled unknown joystick axis warning
- [Vulkan] Fixed issue with pipeline statistics being wrongfully selected

### 5.3.6
- Opc: more robust patchhierarchy caching: https://github.com/pro3d-space/PRo3D/issues/283

### 5.3.5
- OpcPaths now more robust (images vs Images, patches vs Patches) - https://github.com/pro3d-space/PRo3D/issues/280
- Added support for loading mipmaps from file and stream textures
- Added RenderTask.renderToWithAdaptiveClear
- [Vulkan] Fixed synchronization issue with image and buffer uploads
- [Vulkan] Implemented direct texture and framebuffer clear
- [GL] Fixed issue with directly clearing depth-only textures

### 5.3.4
- Added default component swizzle that duplicates the red channel to the green and blue channels for grayscale formats
- [Application.Slim.GL] Fixed context initialization 
- [Vulkan] Fixed deadlock in concurrent descriptor set management

### 5.3.3
- Added union operation for IndexedGeometry
- Added IndexedGeometry primitives for arrows and coordinate crosses
- [Sg] Fixed and improved active flag cache
- [Vulkan] Fixed issue with logging shader cache reads

### 5.3.2
- [GL] Fixed access violation and other issues related to internal format queries
- [GL] Implemented shader caches for compute shaders
- [Vulkan] Implemented shader caches for raytracing shaders
- Improved error logging when shader cache access fails
- Reworked shader caches to use FShade-based interface serialization instead of FsPickler
- Made shader cache directory creation lazy

### 5.3.1
- Fixed export (sharing) bug on MacOS (pNext chain needs to be empty)
- Added hardware support validation for mipmap generation
- [GL] Fixed mipmap generation for compressed file and stream textures
- [GL] Fixed issue with compressed texture download
- [Vulkan] Implemented mipmap compressed PixTexture2d upload
- [Vulkan] Fixed prepare of Stream- / FileTexture with compression

### 5.3.0
- https://github.com/aardvark-platform/aardvark.docs/wiki/Aardvark-Rendering-5.3-changelog

### 5.3.0-prerelease0005
- Implemented debug configurations for enabling backend-specific debug features
- Added validation for color attachment formats when creating framebuffer signatures
- Added proper support for unsigned integer color attachments

### 5.3.0-prerelease0004
- [Vulkan] Implemented RaytracingTask.Update
- [Vulkan] Implemented CommandTask.PerformUpdate
- [Vulkan] Reworked resource manager to prevent disposal of resources in use
- [Vulkan] Use separate device tokens for graphics and compute families
- [Vulkan] Lock pending set during update loop of ResourceLocationSet
- [Vulkan] Added basic support for validation features
- [Raytracing] Fixed issue with acceleration structure building and device tokens
- [GL] Fixed memory usage tracking for imported resources
- Added IRenderTask.Update overloads
- Updated to FShade 5.3 prerelease

### 5.3.0-prerelease0003
- [Vulkan] Fixed issue with duplicate descriptor writes
- [Vulkan] Trim excess elements from image sampler array
- [Vulkan] Fixed extensions being wrongly reported as unavailable
- [Vulkan] Lock pending set during update loop of DescriptorSetResource

### 5.3.0-prerelease0002
- [Vulkan] Fixed issue in DescriptorSetResource related to nested dependencies
- [Vulkan] Reworked RaytracingTask to prevent unnecessary recompilation
- [Vulkan] Implemented update-after-bind descriptors to prevent recompilation of render tasks
- [Vulkan] Added RuntimeConfig.SuppressUpdateAfterBind
- [Vulkan] Fixed issues with dynamic image sampler arrays
- Implemented shrinking of AdaptiveCompactBuffer
- Fixed issue with addition and removal order in AdaptiveCompactBuffer
- Added IRaytracingTask.Update() overloads
- Changed parameter order of Sg.pool

### 5.3.0-prerelease0001
- Initial prerelease for 5.3

### 5.2.17
- Improved error handling of DDS parser
- [Sg] Added delay
- [Sg] Added reference counting for onActivation
- [Application.Slim] Fixed error code printing

### 5.2.16
- [Application.Slim.GL] Fix issue with sample count for non-multisampled windows

### 5.2.15
- Font constructor with System.IO.Stream

### 5.2.14
- Framebuffer Copy/ReadPixels 

### 5.2.13
- switched to aardvark.assembler

### 5.2.12
- reverted Vulkan queue creation
- enabled sharing extensions by default (windows/linux)

### 5.2.11
- disabled useNoError (linux intel, steamdeck compat)

### 5.2.10
- text rendering workaround linux(nvidia)

### 5.2.9
- Arch/Fedora working
- moved to Aardvark.Assembler

### 5.2.9-prerelease0002
- added missing FragmentProgram.Update

### 5.2.9-prerelease0001
- test release
- moved to Aardvark.Assembler

### 5.2.8
- fixed GLFW init problem

### 5.2.7
- [Vulkan] requesting all queues for device

### 5.2.6
- [Vulkan] updated vk.xml to latest version (1.3)
- [GL] improved error handling when retrieving uniforms
- [Sg] Fixed runtime-dependent texture caching
- [GL] Remove render task commands from dirty set

### 5.2.5
- [Text] Winding order of triangles is consistent, degenerated triangles get removed
- [Vulkan] Added image limits checks for layers, levels and size
- [GL] Added texture size limit checks
- [GLFW] Fixed issues with MacOS and other platforms with poor GL support

### 5.2.4
- [Vulkan] Implemented host-side texture compression
- Added RenderTo overloads with adaptive clear values
- Added checks for maximum multisamples when creating framebuffer signatures and textures
- Implemented PickObjects for RenderCommand
- [GLFW] Fixed issue with non-positive window size
- [GL] Fixed streaming texture issues
- [GL] Use RGB internal format for BGR texture formats
- [GLFW] Add hideCocoaMenuBar parameter

### 5.2.3
- [ManagedPool] Fixed memory leak
- Improved block compression decoding and copying
- [GL] Implemented host-side texture compression

### 5.2.2
- [GL] relaxed framebuffer signature compability requirements
- [Vulkan] using any compatible QueueFamily for CopyEngine

### 5.2.1
- implemented ARM64 assembler
- fixed issue with Retina displays
- [Vulkan] fixed issue with platforms not supporting queries
- [Vulkan] fixed issue with platforms not supporting geometry or tessellation shaders

### 5.2.0
https://github.com/aardvark-platform/aardvark.docs/wiki/Aardvark-5.2-changelog

### 5.2.0-prerelease0002
- improved C# interop for ClearValues
- added setter for Call of ManagedDrawCall
- made texture clear API consistent with framebuffer clearing

### 5.2.0-prerelease0001
- Initial prerelease for 5.2

### 5.1.22
- [ManagedPool] no removal of empty PoolNodes from set of RenderObjects: dependency on IndirectBuffer input was causing performance issues when marking/re-evaluating complete set of RenderObjects

### 5.1.21
- fixed ComputeShader problem in Vulkan

### 5.1.20
- deterministic Id for instanced-effects

### 5.1.19
- updated Base & FShade

### 5.1.18

- disabled multisampling for text outline - fix for https://github.com/aardvark-platform/aardvark.rendering/issues/86

### 5.1.17
- fixed package dependeny to FSharp.Data.Adaptive
- [Vulkan] fixed package dependency to GLSLangSharp
- [GL] implemented UploadBufferCmd and CopyImageCmd

### 5.1.16
- [GL] fixed IRuntime.Clear/ClearColor
- [Sg] added C# ColorOutput overload

### 5.1.15
- fixed GL compute shader image bindings

### 5.1.14
- switched to official AssimpNet

### 5.1.13
- updated mac glfw lib & option to control cocoa menu bar

### 5.1.12
- fixed texture download in absense of bufferstorage

### 5.1.11
- fixed osx64 glvm build

### 5.1.10
- fix for segfaults on GL without direct state access

### 5.1.9
- updated FSharp.Core >= 4.7.0
- updated to newest Base/FShade/Adaptive packages
- removed System.Reactive

### 5.1.8
- added argument validation for texture copying
- added argument validation for texture download and upload
- [GL] changed Shader caches to depend on context / runtime
- [GL] fixed copy of cubemaps
- [GL] fixed RenderingLock

### 5.1.7
- added types to specify clear values more easily
- added IRenderTask.RenderTo() overloads with clear values
- added RenderTask.render* variants with clear values
- added map and bind functions for IAdaptiveResource
- DepthTest.None and DepthTest.Always are no longer aliases (revert to < 5.1.0 behavior)
- fixed various bugs related to cube texture arrays
- texture creation functions now validate parameters
- [GL] fixed nop RenderingLock 
- [GL] render control size is ensured to be valid now
- [GL] fixed bug with draw buffers and prepared surfaces with signatures different from the render task
### 5.1.6
- reworked low-level texture API
- added functions for creating (adaptive) 1D and 3D textures
- removed IBackendTextureOutputView and BackendTextureOutputView
- fixed management and disposal of renderTo tasks
- proper out-of-date marking for adaptive resources
- reworked TextureFilter and SamplerState regarding anisotropic filtering
- [GL] fixed bug in Context.Blit()
- [GL] fixed copy and download of texture array slices
- [SgFSharp] removed unnecessary SRTP usage
- [SgFSharp] added Sg.lines'
- [Vulkan] implemented dynamic sampler states

### 5.1.5
- fixed Silk.NET.Core depenedency
- fixed renderToColorCube 

### 5.1.4
- updated packages

### 5.1.3
- [Vulkan] CommandTask no longer disposes ResourceManager
- [Sg] Added instancing utilities for IndexedGeometry 

### 5.1.2
- fixed thread abort exn on linux

### 5.1.1
- https://hackmd.io/58CqcVmnRoGq-X5gIrNThg

### 5.0.17
 - [GL] OpenVR support for GL
 
### 5.0.15
 - [GL] replaced EXT_direct_state_access with ARB_direct_state_access
 - [GL] fixed crashes when using core profile
 - [GL] RenderTask.Dispose no longer needs a transaction (https://github.com/aardvark-platform/aardvark.rendering/issues/60)

### 5.0.14
 - [GL] More robust parsing of GL and GLSL versions

### 5.0.3
 - [Text] fixed compatibility with render passes

### 5.0.0
 - [Base] updated aardvark to v5
 - [Base] reworked Buffer API: added BufferUsage flags
 - [Base] added indirect draw stride
 - [Base] refactored RenderObject drawcalls
 - [Base] removed IResizeBuffer, IMappedBuffer, IMappedIndirectBuffer
 - [Sg] reworked ManagedPool

### 4.12.4
 - [GL] fixed mip level calculation in texture upload

### 4.12.3
 - [Base] updated base packages

### 4.12.2
 - [Base] fixed GLVM loading for all plattforms

### 4.12.1
 - [Base] updated GLVM for linux

### 4.12.0
 - [GL] removed warnings from LodRenderer
 - [GL] added support for NormalUV texture (2-channel float images)

### 4.11.15
 - [GL] fixed BufferRuntime.Clear

### 4.11.12
 - [GL] added quad-buffered stereo support to GameWindow

### 4.11.8
 - [Base] fixed RenderTask.custom
 - [GL] fixed size 0 UniformBuffer alloc

### 4.11.7
 - [Base] updated packages / fixed memory leak

### 4.11.5
 - [Base] reverted memory leak fix

### 4.11.4
 - [Base] rmeoved hooking mechanism of dynamic uniforms (no need anymore, allowed overwrite of view/proj trafo)
 - [Base] moved Caches (UnaryCache, BinaryCache) to Base.FSharp
 - [Sg] fixed memory leak when using derived attirbutes (e.g. ModelViewTrafo, ModelViewProjTrafo)
 - [GL] fixed texture array uniforms 

### 4.11.3
 - LoD Render: removed debug ouput

### 4.11.2
 - [GL] fixed buffer resource stats
 - [GL] fixed unmanaged memory leak of VAO
 
