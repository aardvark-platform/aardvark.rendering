### 5.7.0-prerelease0018
- [Sg] Heap: unified uniform model — every shader-consumed uniform is a ref-counted region keyed by source aval (no global/per-object distinction; a value shared by all draws is one slot with refcount = draw count). Removed the `globalsRO` delegation and UBO-global fall-through. A camera move marks ONE shared region, not N.
- [Sg] Heap: the SHADER is the source of truth — a uniform is stored as exactly the type it requests, the write converting the provided value (provided `M33d`→requested `M33f`, `Trafo3d`→`M44f`, …). A uniform requested at DOUBLE precision (`V2d`/`V3d`/`V4d`/`M33d`/`M44d`) is stored as REAL doubles via a native `HeapDataD` arena view (2 words/scalar, 8-byte aligned) — never f32-widened.
- [Sg] Heap: camera composites (`ViewProjTrafo`/`ModelViewProjTrafo`/`ModelViewTrafo`) are always DERIVED from their `Model`/`View`/`Proj` constituents and composed in fp64 (result converted to the requested type) — matches a CPU double `view*proj` bit-for-bit (golden `maxDelta=0`), and the shared constituents keep a camera move O(1).

### 5.7.0-prerelease0017
- [Sg] Heap: incremental vertex-pull gather — `IncrementalBucket.vtxGatherAval` refreshes only added/removed slots + non-constant sources per structural transaction (O(r)) instead of re-scanning all highWater×numAttrs slots (O(N)). Value-edit paths unchanged.
- [Vulkan] Bindless unbounded storage-buffer / sampler arrays degrade gracefully past their capacity: `StorageBuffers/CombinedImageSampler.GetDescriptors` bind `min(runtimeLen, capacity)` and warn once, instead of an IndexOutOfRange native abort when a bucket exceeds the array cap.
- [Sg] Heap: per-FRAME resource-leak regression golden (`chainleak`) + lock-free live-handle counters (Resource/DescriptorSet LiveCount) — guards the per-frame accumulation the per-scene lifetime test cannot see. Verified flat over thousands of frames at n≤100000.

