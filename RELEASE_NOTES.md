### 5.7.0-prerelease0010
- [Sg] `Heap.ofRenderObjects` takes no name set anymore — auto-detected per-draw fields are THE behavior (per-draw fields = the uniforms your objects supply; shared avals dedup to one arena region). The explicit-names variant and `ofRenderObjectsAuto` are removed.

