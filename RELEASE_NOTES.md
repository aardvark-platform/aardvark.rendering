### 5.7.0-prerelease0012
- [Sg] Heap: storage-decoded geometry — the fixed-function vertex path is GONE. Attributes and INDICES decode from the storage arena via per-allocation headers (typeId/length/stride; wombat-style); draws are non-indexed; singletons (SingleValueBuffer, e.g. Primitives.Box colors) are length-1 allocations decoded by the same fetch and ride the same bucket as real buffers; u16/u32 indices mix per bucket. GPU-resident buffers stay zero-copy (bindless array). Measured: GPU time IMPROVES 25-28% despite the post-transform-cache loss; CPU churn improves (eligibility probing memoized).
- [Sg] `Heap.Diagnostics` — opt-in, deduped, actionable log lines for every pass-through reason (+ `Heap.diagnosticMessages()`).

