# Heap general bindless textures — dynamically-grown descriptor arrays

Design for generalizing the GPU heap's per-object texture support to all 2D/array/cube/shadow
sampler kinds, with incremental updates and no count-driven fragmentation. Concerns
`Aardvark.Rendering.Vulkan` (descriptor machinery) **and** `Aardvark.SceneGraph/HeapPool.fs` (the
heap). Coordinated with Martin (descriptor code is his recent refactor). Status: Phase 1
(dynamic-grow descriptor arrays) SHIPPED — see the "Phase 1 — DONE" section below; Phases 2-5
(more sampler kinds, sampler-state threading, churn audit) still open.

## Goal

Heap-ingested shaders may declare 0..K samplers of kinds **`Sampler2d`, `Sampler2dArray`,
`SamplerCube`, `Sampler2dShadow`, `SamplerCubeShadow`** (everything but 1D and 3D/volume). Each
`RenderObject` supplies its textures as `aval<ITexture>`. The heap collapses such objects as usual
and:

1. allows these sampler kinds whenever the device can (descriptor indexing);
2. add / update / remove of objects in the ingested set is **efficient — no O(N) path**, and leaks
   nothing except possibly a recycled slot;
3. if any limit is hit (can't hold a new texture for a bucket), the heap **never crashes and never
   falls back to classic rendering** — it starts a new bucket/page.

## Mechanism: descriptor *indexing*, not descriptor_buffer

`VK_EXT_descriptor_buffer` is **not** in the backend (only traditional pools/sets). The heap already
uses **descriptor indexing**: one unbounded `sampler2d[] HeapTextures2d` per kind, indexed per-draw
by `HeapTexIndices2d[slot*Kt + kt]` (an SSBO in the arena). On desktop this gives the same
"one big array, index by int, incremental writes" model descriptor_buffer would, so we extend it
rather than build descriptor_buffer (a separate, much larger backend project).

## The decisive constraint: textures must NOT be a second split axis

A bucket's geometry/uniform arena is **paged** (the `pageWords` multi-page path, now clamped to the
device's `maxStorageBufferRange`). If the texture descriptor set had its own small capacity, a
bucket would have **two independent split boundaries** — "texture set full at 100 distinct textures"
vs "arena page full at 110 objects" — that don't line up. Every misaligned boundary spawns another
draw call (`100, 10, 100, 10, …`), destroying the O(1)-draw collapse that is the entire point.

So: **the arena page (storage-buffer range) is the *only* split driver.** Textures live in **one
array per bucket per kind, spanning all of the bucket's arena pages**, and must be sized so they are
*never* the binding limit — the bucket "runs out of page memory first." Concretely:

- the descriptor array's **layout `maxCount` = the device's update-after-bind sampled-image limit**
  (millions; free, since update-after-bind costs nothing until actually allocated);
- the **actual** allocated variable-count grows **pow2** with the deduped texture count, so memory ∝
  real textures and adds amortize O(1).

This is what "make descs huge AND truly dynamic" means: `maxCount` is the free ceiling, the live size
is a grown vector.

## Current state (what exists)

### Backend (`Aardvark.Rendering.Vulkan`)
- `DescriptorPool.fs`: pool created with `UpdateAfterBindBitExt`.
- `DescriptorSetLayout.fs`:
  - `UnboundedSamplerArrayCeiling = 1024u`; `unboundedSamplerArrayCapacity = min 1024u
    Limits.Descriptor.MaxPerStageSampledImages` — so an "unbounded" array is **capped at 1024**, and
    clamped to the **non**-update-after-bind limit.
  - `VARIABLE_DESCRIPTOR_COUNT` is supported (`VariableCountBinding`), but for a non-constant array
    the set is **allocated at the fixed ceiling** (`PreparedRenderObject` falls back to
    `b.DescriptorCount` = 1024) — i.e. fixed 1024 slots, `PARTIALLY_BOUND`, written incrementally.
    Adds are already O(1), but the cap is a hard 1024 and memory is paid for all 1024.

### Heap (`HeapPool.fs`)
- `bindlessTypeInfo` (~375): only `Sampler2d -> (HeapTextures2d, HeapTexIndices2d, heapTex2d)` and
  `SamplerCube -> (…Cube)`.
- `heapTex2d : Sampler2d[]` / `heapTexCube : SamplerCube[]` — unbounded arrays; gather (~382) is
  `heapTex<T>.[ HeapTexIndices<T>[slot*Kt + kt] ]`.
- `SlotTexWriter` (~1512) + a per-type dedup table (~1535) write textures into the array incrementally
  and maintain the per-slot index buffer.
- Atlas fallback (`AtlasPlacementTable`) for `Sampler2d`-only on devices without descriptor indexing.

## The design

### 1. Backend — dynamic, hardware-max unbounded arrays (the core change)
For a non-constant unbounded sampler array:
- **Layout**: declare the binding `VARIABLE_DESCRIPTOR_COUNT` with `maxCount =
  MaxPerStageDescriptorUpdateAfterBindSampledImages` (and `MaxDescriptorSetUpdateAfterBind…`),
  `UPDATE_AFTER_BIND` + `PARTIALLY_BOUND`. (Must be the **last** binding of the set — a layout
  constraint of variable count; the heap's texture arrays should be placed accordingly.)
- **Allocation**: allocate the set at an initial pow2 actual count (e.g. 16 or 64). Track the live
  count.
- **Growth**: when the consumer needs slot ≥ current actual count, allocate a **new** set at the
  next pow2, **rewrite** the live descriptors into it, swap the bound set, free the old. Geometric →
  amortized O(1) per add. Update-after-bind means individual writes need no command re-record; the
  re-allocation on growth does require re-binding the set into the render object.
- Expose the limit as a new `IRuntime` capability, e.g. `MaxSampledImagesUpdateAfterBind : int`
  (mirrors `MaxStorageBufferBytes`), read from
  `Limits.Descriptor.MaxPerStageDescriptorUpdateAfterBindSampledImages` (Vulkan) / GL constant.
- Keep `UnboundedSamplerArrayCeiling` for the *constant*/legacy path; the dynamic path uses the
  hardware ceiling.

This is Martin's territory (`DescriptorSetLayout` / `DescriptorSet` / `PreparedRenderObject`) — the
heap is a consumer.

### 2. Heap — the five kinds
Add to `bindlessTypeInfo`, `heapTex<T>`, the gather, `isBindlessSamplerType`, and the `SlotTexWriter`
table set: `Sampler2dArray`, `Sampler2dShadow`, `SamplerCubeShadow` (2d/cube already done). Each kind
gets its own `HeapTextures<kind>` array + `HeapTexIndices<kind>` SSBO + per-slot id.

### 3. Sampler STATE (filter / wrap / **shadow compare op**)
The heap's `heapTex<kind>` array is one declaration with one sampler state; the consuming effect's
sampler state must match it. Options, in order of preference:
- **Thread the effect's sampler state into the generated heap sampler array per effect** (so the
  rewrite emits an array whose filter/wrap/compare equal the user's). Different states → different
  generated rewrite → naturally different bucket. This is the correct general answer and also fixes
  the latent non-default-state gap for the existing 2d/cube path.
- Shadow specifically: the comparison op lives in the sampler *declaration* (static), so it's part of
  the effect — same effect ⇒ same compare ⇒ already one bucket. The only work is making the heap's
  generated shadow array carry that compare op.

### 4. Per-object textures, dedup, slot recycling
- Each object supplies `aval<ITexture>` per sampler; `SlotTexWriter` reads it.
- **Dedup**: identical textures share one array slot (ref-counted by texture identity). The id handed
  to the draw (`HeapTexIndices<kind>[slot*Kt+kt]`) is the slot.
- **Update**: a texture aval changing rewrites that slot's descriptor — O(1), update-after-bind, no
  re-record.
- **Remove**: when a slot's refcount hits 0, recycle it (free list). No leak beyond a recycled slot;
  the array's live count doesn't grow for churned textures.

### 5. Limit handling
- Textures: with `maxCount` = hardware limit and pow2 growth, the texture count is effectively never
  the wall (millions after dedup) — page memory is hit first. The degenerate >hardware-limit case
  would spill to a new bucket, but it is not a realistic path.
- Arena: the existing multi-page path (device-clamped `pageWords`) is the sole split; never crashes,
  never classic.

## Efficiency / no-O(N) summary
| op | cost | why |
|---|---|---|
| add object | O(textures-of-object) | dedup table insert; descriptor write O(1); growth amortized O(1) |
| update texture | O(1) | rewrite one descriptor (update-after-bind) |
| remove object | O(textures-of-object) | refcount--, recycle slot on 0 |
| array growth | amortized O(1) | pow2 re-alloc + rewrite |
No path scans all N objects.

## Backend changes (Martin / coordinate)
- `IRuntime`: add `MaxSampledImagesUpdateAfterBind` (like `MaxStorageBufferBytes`).
- `DescriptorSetLayout`: dynamic-unbounded layout = update-after-bind + partially-bound + variable
  count at the hardware ceiling; last-binding placement.
- `DescriptorSet` / `PreparedRenderObject`: grow the variable-count set (pow2 re-alloc + rewrite +
  re-bind) instead of fixing at 1024.

## Heap changes
- `bindlessTypeInfo`/`heapTex<T>`/gather/`SlotTexWriter` tables: + 3 kinds.
- Generated sampler array carries the effect's sampler state (filter/wrap/compare).
- Confirm dedup table + slot recycling are O(changed) (audit existing `SlotTexWriter`).

## Risks / open questions
- **Variable-count must be the last set binding** — the heap binds several SSBOs + per-kind sampler
  arrays; only one binding per set can be variable count. Likely need the sampler arrays in their own
  descriptor set (set index), or only the last kind's array variable and others bounded — needs a
  concrete binding-layout plan. **Settle with Martin before Phase 1.**
- Touching Martin's just-landed descriptor refactor — coordinate; rebuild + run the heap suite +
  golden after.
- **Portability**: descriptor indexing is desktop/Vulkan; the atlas fallback stays for non-DI
  devices (and is `Sampler2d`-only — arrays/shadow/cube have no atlas path, so on non-DI devices
  those kinds simply aren't heapable → object passes through, never crashes).
- Update-after-bind re-allocation on growth must re-point the render object's descriptor binding;
  ensure that doesn't tear a frame.

## Phase 1 — DONE (dynamic-grow landed)

All three growables below are implemented and validated: heap suite 17/17 and the full GL+Vulkan
Rendering suite 324/324 (no regression in the core descriptor path). The layout cap is now the device
limit; memory tracks the elements actually in use.

### implementation finding (Martin-confirmed)

Lifting the layout cap is NOT free: three places in the Vulkan resource path pre-size to the
binding `count` (the unbounded ceiling), so a lifted cap makes them allocate ~millions and
`DescriptorSet.update`'s `NativePtr.stackalloc` overflows the stack. To lift the cap, all three must
scale with the LIVE element count, not `count`:

1. **`DescriptorSetResource`** (ResourceManager) — `versions` mirrors + the set itself. Fix: size to
   a live `ActualCount`, grow pow2, re-allocate the set on overflow and retire the old set to a
   `superseded` list disposed at `Destroy` (a grown set is a new handle; the old one may still be in
   flight). The `AdaptiveDescriptor` cache grows the same way (`LiveCount`/`ActualCount`). *(prototyped
   and working; reverted pending the other two.)*
2. **`ImageSamplerArrayResource`** (ResourceManager) — `Array.zeroCreate count` + `Array.replicate
   count` + `for i = 0 to count-1 do set i empty`. Fix: grow `images`/`versionOffsets`/`handle` to the
   live range (max index + 1, pow2), fill new slots with `empty` on grow. **`StorageBufferArrayResource`,
   right below it, is already exactly this** ("resize keeping the prefix; the heap only grows") — use it
   as the template.
3. **`DescriptorSet.update`** — stackallocs to the write-batch size; once (1)+(2) return live-sized
   arrays the batch is small, so no separate fix is needed.

Bonus (Martin): enable `PARTIALLY_BOUND` for normal *bounded* arrays too, so sparse arrays don't need
a descriptor in every slot. Out of scope for the first cut.

Martin confirmed this (DescriptorSetResource + ImageSamplerArrayResource "shouldn't scale linearly
with the descriptor count"). It's his freshly-refactored resource code.

## Phases
1. Backend: dynamic-grown unbounded sampler array (maxCount = hw limit, pow2 actual) + the new
   `IRuntime` cap. Validate with the existing 2d/cube heap path (no behaviour change, just bigger/
   dynamic) — heap suite + golden green.
2. Heap: add `Sampler2dArray` (non-shadow, simplest new kind) end-to-end + golden (array sampling
   heap == classic).
3. Heap: shadow pair (`Sampler2dShadow`, `SamplerCubeShadow`) with the effect's compare op threaded
   into the generated array + golden.
4. Sampler-state threading for all kinds (fixes the latent non-default-state gap).
5. Audit/confirm dedup + slot recycling O(changed) + no leak (churn test).
