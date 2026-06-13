### 5.7.0-prerelease0017
- [Sg] Heap: incremental vertex-pull gather — `IncrementalBucket.vtxGatherAval` refreshes only added/removed slots + non-constant sources per structural transaction (O(r)) instead of re-scanning all highWater×numAttrs slots (O(N)). Value-edit paths unchanged.
- [Vulkan] Bindless unbounded storage-buffer / sampler arrays degrade gracefully past their capacity: `StorageBuffers/CombinedImageSampler.GetDescriptors` bind `min(runtimeLen, capacity)` and warn once, instead of an IndexOutOfRange native abort when a bucket exceeds the array cap.
- [Sg] Heap: per-FRAME resource-leak regression golden (`chainleak`) + lock-free live-handle counters (Resource/DescriptorSet LiveCount) — guards the per-frame accumulation the per-scene lifetime test cannot see. Verified flat over thousands of frames at n≤100000.

