### 5.7.0-prerelease0008
- [Sg] Heap: buffer lifetimes go through `IAdaptiveResource` — all heap buffer avals (arena, draw-record/header/instance mirrors, HeapScene data) use `AdaptiveResource.mapNonAdaptive` instead of interface-stripping `AVal.map`, so the render task's Acquire/Release refcounting destroys a disposed bucket's GPU buffers. New `lifetime` golden test (30 create/render/dispose cycles, VMA allocation stats): pre-fix +6 allocations/cycle, post-fix returns to a zero baseline every cycle. New `Device.MemoryStatistics` for the test.

