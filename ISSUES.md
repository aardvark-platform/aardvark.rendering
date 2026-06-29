# GPU heap — known issues

Tracks OPEN / deferred items for the heap (`src/Aardvark.SceneGraph/HeapPool.fs`,
`Heap.ofRenderObjects`). Resolved items move to `RELEASE_NOTES.md`.

## Texture support is not general — only Sampler2d / SamplerCube

Per-object (bindless) textures route through `texHeapBinding`, which maps only
`Sampler2d -> HeapTextures2d` and `SamplerCube -> HeapTexturesCube`. Missing sampler kinds:
**`Sampler2dArray`, `Sampler3d`, `SamplerCubeArray`, the SHADOW samplers**
(`Sampler2dShadow`/`SamplerCubeShadow`/…), and **integer/uint samplers**. The atlas fallback
(non-descriptor-indexing devices) is **`Sampler2d`-only** as well.

Each shader sampler *type* needs its own bindless array binding (the sample op is statically
typed — you can't runtime-switch image dimension within one array), so the fix is per-type:
add a `HeapTextures<T>` array + `HeapTexIndices<T>` index buffer + a `texHeapBinding` entry for
each new kind, exactly like the 2d/cube pair. Shadow samplers additionally need the compare
state threaded through. Bounded by `runtime.SupportsUnboundedSamplerArrays` (Vulkan descriptor
indexing); the atlas path can only ever cover `Sampler2d`.

## Boot is GC-bound at scale (startup latency only — correctness fine)

Full-Vienna (764k parts) boot is ~118 s by default, ~33 s with Server GC alone. The cost is the
aardvark.dom `ASet.collect` over 764k singleton render objects (RO build) plus arena ingest, not
the GPU. Leaving as-is for now. Mitigations when revisited: Server GC, batching the RO build,
and the arena upload (currently a synchronous fence per coalesced run — see the upload path in
`HeapArena.Compute`; a batched submit would cut the multi-second 3 GB upload).
