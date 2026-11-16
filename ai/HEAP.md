# GPU Heap

A GPU-resident draw set for scenes with many objects. The heap collapses the render
objects of a subtree, or of any set of render objects, into a few bucket render objects.
Each bucket is drawn with one indirect multi-draw, and the per-object data (transforms,
uniforms, vertex data and indices) lives in a paged storage-buffer arena on the device.
The command stream then encodes one draw per bucket instead of one per object, and the
scene can hold hundreds of thousands of objects.

Source: `src/Aardvark.SceneGraph/Heap/HeapPool.fs` (the heap), `HeapAtlas.fs` and
`HeapAtlasPool.fs` (the texture atlas fallback).

## Entry Points

| API | Purpose |
|-----|---------|
| `runtime.CreateHeapStorage(?pageSizeInBytes)` | Creates a `HeapStorage`, the GPU memory any number of heaps allocate from. The page size defaults to 1 GiB and is clamped to the device's storage-buffer range. |
| `Sg.heap storage sg` | Scene-graph node (`Sg.HeapApplicator`) that collapses the render objects of `sg` through the heap. Non-heapable render objects pass through unchanged. |
| `Heap.ofRenderObjects storage objects` | The same for an `aset<IRenderObject>`. |
| `Heap.ofRenderObjectsAuto objects` | Like `ofRenderObjects` with a private storage that is disposed with the last render task using it. |
| `Heap.ofRenderObjectsPicking storage deregister objects` | Variant for pick rendering: captures each object's `HeapPickId` uniform per slot and calls `deregister` with the id when the slot is freed. |

```fsharp
use storage = runtime.CreateHeapStorage()

let scene =
    objects
    |> ASet.map buildSg
    |> Sg.set
    |> Sg.heap storage
```

The node works on both scene-graph paths (`IRenderSg` and the attribute grammar). The
framebuffer signature is resolved when the render task is compiled: the heap emits
`SignatureDependentRenderObject`s, one for the opaque and one for the transparent
objects, so a heap subtree can take part in the transparency pass and in pick
rendering with extra attachments.

## Ownership

A `HeapStorage` owns the GPU memory of its pages and is `IDisposable`. The creator
disposes it once no heap uses it any more, i.e. after the render tasks built on it are
disposed. The same storage may back several heaps, for example the main render and a
shadow pass, so shared geometry and uniforms live in GPU memory once.
`Heap.ofRenderObjectsAuto` disposes its private storage with the last task that drops
it. Buckets own their transform-chain link arena the same way.

## How It Works

- **Buckets.** Render objects are grouped by effect (and the state that must be equal for
  one draw) into buckets. Each bucket has one representative render object whose shader
  is rewritten so that every per-object input is read from the arena instead of from
  vertex attributes and uniforms. Draws are non-indexed; the vertex shader decodes
  attributes and indices itself. A bucket whose unbounded arrays are full overflows into
  a further bucket of the same key: the vertex-pull array holds one element per slot and
  attribute, and a texture table is bounded by one slot per element, the worst case of
  every slot carrying a distinct texture, because a slot's texture can change later
  (capacities from `IRuntime.MaxUnboundedStorageBufferArrayElements` and
  `MaxUnboundedSamplerArrayElements`).
- **Slots and the arena.** Every object occupies a slot of its bucket. The slot's data
  (attributes, index data, uniform values) is stored as allocations in the storage arena,
  each preceded by a header with the type id, element count and stride, so the shader can
  decode any supported element type at runtime. Per-draw routing reads the slot from
  `gl_InstanceIndex`, which is why the heap is Vulkan-only in practice: GL's
  `gl_InstanceID` omits the base instance.
- **Edits.** Membership changes and content changes edit the arena in place through a
  staging ring; only changed ranges are uploaded. Pages are compacted when enough space
  has been freed.
- **Derived values.** Values such as the composed model transform (from a chain of
  transform links) are computed by compute dispatches on the GPU. On Vulkan the
  dispatches and the draws of a bucket are recorded as one unit (`HeapRenderObject`), so
  every draw reads freshly derived data. On runtimes without shader doubles the derived
  transforms use a two-float emulation (df32).
- **Typed partitions.** Slots whose attribute types are all fixed can be rendered through
  a pipeline specialized with those type ids (the `HeapTid0..7` and `HeapTidIdx`
  specialization constants, see SG-PATTERNS.md). Such a partition gets its own pipeline
  once it holds 64 slots and falls back to the dynamic pipeline, which reads the type id
  from each allocation header, below 16 slots. Both produce the same pixels; the
  specialization only removes the runtime type dispatch.
- **Textures.** Textures are bound through an unbounded sampler array where the runtime
  supports it (`IRuntime.SupportsUnboundedSamplerArrays`) and through a texture atlas
  otherwise.
- **Picking.** Objects can write per-slot pick ids (`Heap.ofRenderObjectsPicking`).

## Requirements

Vulkan in practice (see above). `IRuntime.SupportsMultiDrawIndirectDrawId` and
`IRuntime.SupportsShaderFloat64` select the GPU paths; `IRuntime.SupportsUnboundedSamplerArrays`
selects textures via sampler arrays instead of the atlas.

## Environment Variables

All switches are read through `EnvVar` (see DEBUG.md); their XML docs in
`src/Aardvark.Rendering/Common/EnvVar.fs` are authoritative.

| Variable | Effect |
|----------|--------|
| `AARDVARK_HEAP_DF32` | Use the two-float derived-uniform kernels even with shader doubles. |
| `AARDVARK_HEAP_NO_CLUSTERS` | One draw record per slot instead of clustered records. |
| `AARDVARK_HEAP_NOCHAIN` | Disable the GPU transform chain; use the CPU-folded model transform. |
| `AARDVARK_HEAP_STATIC_GEOM` | Snapshot geometry when it is added; no adaptive re-upload. |
| `AARDVARK_HEAP_EDIT_PROF` | Log the CPU time of the heap's update phases. |
| `AARDVARK_HEAP_PAGE_WORDS` | Page size in 32-bit words (at least 1024), for testing the multi-page path. |
| `AARDVARK_HEAP_NO_SPEC` | Never materialize typed partitions. |
| `AARDVARK_HEAP_DUMP_RECORDS` | Log statistics about the clustered draw records. |

## Tests

`Tests/Rendering/Heap/` holds the `Heap.Uniforms` group (every derived uniform against its CPU
counterpart) and the `Heap.Gauntlet` group of probes that compare heap renders pixel by pixel
with the classic per-object render (see TESTING.md). Both run on Vulkan only.

## Example and Benchmarks

`src/Examples (netcore)/39 - HeapSpike/` is a showcase scene with many objects, textures and
picking, plus benchmarks for startup, rendering, edits, transform chains and the GPU gather
and derive paths, and a GLSL dump of the generated shaders.

## Known Issues

- Booting a very large scene (hundreds of thousands of objects) is bound by the CPU and the
  garbage collector: the per-object render objects are collected on the CPU before the
  heap ingests them, and the arena upload waits on a fence per coalesced run. Server GC
  helps; batching the render-object build and the upload would help more. Correctness is
  not affected.
