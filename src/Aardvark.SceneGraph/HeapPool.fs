namespace Aardvark.SceneGraph

// Heap rendering — collapse many per-object render objects that differ only in
// their per-draw uniforms into a few "bucket" render objects, each drawn as ONE
// indirect multidraw against a shared storage-buffer arena. The per-draw uniform
// values are gathered in the (auto-rewritten) shader via gl_InstanceIndex, so
// the command stream encodes O(buckets) and binds one descriptor set per bucket
// instead of one per object.
//
// Backend-neutral in its dependencies (IRuntime / RenderObject / AdaptiveBuffer
// / FShade / Sg — no Vulkan types), but currently VULKAN-ONLY in practice:
// per-draw routing reads the slot via gl_InstanceIndex (= gl_InstanceID +
// baseInstance under Vulkan semantics). On GL, gl_InstanceID omits baseInstance
// and FShade has no base-instance intrinsic, so every draw would read slot 0.
// A GL port needs a FShade gl_BaseInstance/gl_DrawID intrinsic
// (ARB_shader_draw_parameters); texture bindless additionally needs GL
// extensions (ARB_bindless_texture / NV_gpu_shader5).
//
// Entry point:
//   * Heap.ofRenderObjects — adaptive aset<IRenderObject> -> aset<IRenderObject>
//     transform; buckets by effect, stores host geometry (attributes AND
//     indices, incl. SingleValueBuffer singletons) as per-allocation-headed
//     ranges in the bucket's storage arena — NO fixed-function vertex input;
//     draws are non-indexed and the rewritten vertex shader storage-decodes
//     everything (wombat-style). Dirty-tracks the arena (sparse per-frame
//     mutation uploads only changed sub-ranges). Streaming/churn is just a
//     changeable aset (cset) — membership deltas re-pack incrementally.
//     `Sg.heap signature` wraps it as a scene-graph node.

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open FShade
open FShade.Imperative
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.NativeInterop

#nowarn "9"

/// Storage-buffer accessors for the heap arena, for shaders that read it
/// directly (the per-draw header table and the float payload arena). The heap
/// rewrite injects these automatically; expose them so custom/compute shaders
/// can address the arena too.
[<AutoOpen>]
module HeapUniforms =
    type UniformScope with
        member x.HeapData    : float32[] = uniform?StorageBuffer?HeapData
        // the SAME arena buffer bound a second time as an int view: per-allocation
        // headers (typeId/length/stride), index data and integral attributes decode
        // their 4-byte words as ints (bit pattern is identical).
        member x.HeapDataI   : int[]     = uniform?StorageBuffer?HeapDataI
        /// TYPED-ASSIGNMENT spec constants (ai/HEAP-TYPED-ASSIGNMENTS-PLAN.md):
        /// per-ATTRIBUTE source typeIds by attribute ordinal, + the index typeId.
        /// 0 = unknown -> the decoder reads the header tid at runtime (today's
        /// ladder, always correct). A concrete tid folds the decoder to ONE
        /// typed arm at pipeline compile and the header tid is NEVER read.
        /// On Vulkan these are pipeline specialization constants; on GL they
        /// degrade to ordinary uniforms served by the bucket provider.
        member x.HeapTid0 : int = uniform?SpecConstants?HeapTid0
        member x.HeapTid1 : int = uniform?SpecConstants?HeapTid1
        member x.HeapTid2 : int = uniform?SpecConstants?HeapTid2
        member x.HeapTid3 : int = uniform?SpecConstants?HeapTid3
        member x.HeapTid4 : int = uniform?SpecConstants?HeapTid4
        member x.HeapTid5 : int = uniform?SpecConstants?HeapTid5
        member x.HeapTid6 : int = uniform?SpecConstants?HeapTid6
        member x.HeapTid7 : int = uniform?SpecConstants?HeapTid7
        member x.HeapTidIdx : int = uniform?SpecConstants?HeapTidIdx
        // the SAME arena buffer bound a THIRD time as a native double view: a uniform
        // the shader requests at DOUBLE precision (M33d/M44d/V*d) is stored as real
        // doubles (8-byte aligned, 2 words/scalar) and read here with full precision —
        // never f32-widened. (shaderFloat64; the heap already uses double storage
        // buffers in its fp64 compose path.)
        member x.HeapDataD   : float[]   = uniform?StorageBuffer?HeapDataD
        member x.HeapHeaders : int[]     = uniform?StorageBuffer?HeapHeaders
        // PAGED derive: slot->page map + the page the per-page derive dispatch is computing.
        // A page's derive binds its own arena, so it must skip slots on other pages (whose
        // header offsets are page-local to a different page — writing them here corrupts).
        member x.HeapSlotPage : int[] = uniform?StorageBuffer?HeapSlotPage
        /// PER-OUTPUT derive dispatch list: [ownerSlot; planIdx] per live SHARE
        /// (distinct derived value) — the derive kernel runs one thread per share
        /// instead of one per slot (246k threads discovering they own nothing cost
        /// 7.1 ms/frame on a 2-CU APU).
        member x.HeapShareRecs : int[] = uniform?StorageBuffer?HeapShareRecs
        /// DENSE derived-uniform store (bucket-global, NOT paged): derived
        /// composite outputs live tightly packed in their own buffer so the
        /// per-vertex gathers hit dense lines instead of cells scattered through
        /// ~4KB slot groups in the geometry arena. Float and double views of the
        /// SAME buffer (like HeapData/HeapDataD).
        member x.HeapUni  : float32[] = uniform?StorageBuffer?HeapUni
        member x.HeapUniD : float[]   = uniform?StorageBuffer?HeapUniD
        member x.HeapPageId : int = uniform?HeapPageId
        // PICKING: dom-sourced per-slot pick id, gathered by gl_InstanceIndex; the dom
        // heap pick-shader writes this into the pick buffer. Only bound when picking.
        member x.HeapPickIds : int[] = uniform?StorageBuffer?HeapPickIds
        // (the GPU trafo-chain folds each slot's ModelTrafo directly into the
        // arena — HeapData/HeapDataD — so there is no separate chain buffer.)
        // CLUSTERED buckets: gl_InstanceIndex -> slot (per size-class instanced
        // records; see clusterClassSizes)
        member x.HeapClassSlots : int[] = uniform?StorageBuffer?HeapClassSlots
        // Bindless geometry: ONE flat float32 SSBO array indexed by handle
        // (gl_InstanceIndex). Element [h] is object h's interleaved vertex floats;
        // each attribute is decoded by component count at a fixed offset (like the
        // host arena's HeapData) — type-agnostic, any number of attributes, and a
        // flat float[] avoids std430 vec3 16-byte-stride misalignment.
        member x.HeapVertexData  : float32[][] = uniform?StorageBuffer?HeapVertexData
        // the SAME per-object buffers bound a second time as an int view, so integral
        // attributes decode their 4-byte slots as ints (bit pattern is identical).
        member x.HeapVertexDataI : int[][]     = uniform?StorageBuffer?HeapVertexDataI
        // (per-object texture indices are GENERATED per input sampler —
        // "HeapTexArr<si>" / "HeapTexIdx<si>", built dynamically by the rewrite —
        // so they have no UniformScope members here.)
        // ── texture-atlas fallback (Vulkan-1.0 / GL / MoltenVK: ONE sampler) ──
        // Per-object atlas placement (indexed by slot*K + k): mip-0 interior origin and
        // size in atlas PIXELS, and packed format bits (numMips<<1 | addrU<<4 | addrV<<6).
        member x.HeapAtlasOrigin : V4f[] = uniform?StorageBuffer?HeapAtlasOrigin   // xy used
        member x.HeapAtlasSize   : V4f[] = uniform?StorageBuffer?HeapAtlasSize     // xy used
        member x.HeapAtlasFmt    : int[] = uniform?StorageBuffer?HeapAtlasFmt
        /// per-(object,sampler) page index (which atlas page their tile lives on); selects
        /// one of heapAtlas0..heapAtlas7 in the shader's switch-ladder.
        member x.HeapAtlasPageId : int[] = uniform?StorageBuffer?HeapAtlasPageId
        /// the atlas page dimensions in pixels (to normalize atlas-pixel coords)
        member x.HeapAtlasPxSize : V2f   = uniform?HeapAtlasPxSize

module Heap =

    /// Global override for the heap's derived-uniform precision path. When true the
    /// heap uses its df32 (two-f32) compute kernels even on a backend that HAS
    /// shaderFloat64 — for forcing / validating the MoltenVK path on the desktop.
    /// When false (default) the path is chosen per bucket from `IRuntime.ShaderDouble`:
    /// real fp64 (M44d) where the backend has shader doubles, df32 where it does not
    /// (MoltenVK/Metal). Settable via the AARDVARK_HEAP_DF32 env var (1/true/on) or
    /// directly at runtime before a bucket is built.
    let mutable ForceDf32 =
        match System.Environment.GetEnvironmentVariable "AARDVARK_HEAP_DF32" with
        | null | "" -> false
        | s -> let s = s.Trim().ToLowerInvariant() in s = "1" || s = "true" || s = "on"

    /// The df32 (two-f32) derived-uniform path is used for a runtime when it is
    /// forced, or when the backend genuinely lacks shader doubles. fp64 can never be
    /// "forced on" a backend without shaderFloat64 — there `ShaderDouble` is false and
    /// df32 is the only option regardless of the override.
    let internal useDf32 (runtime : IRuntime) = ForceDf32 || not runtime.ShaderDouble

    let private cint (v : int) : Expr<int> = Expr.Value v |> Expr.Cast

    // ── pointer writers: all per-draw packing goes STRAIGHT into the arena's
    //    upload ring (mapped host-visible memory or a pinned fallback array) at a
    //    word offset relative to the span base. Doubles rely on the ring aligning
    //    every span to the arena offset's 8-byte parity (see HeapArena.StageWords).
    let inline private wf (p : nativeint) (i : int) (v : float32) =
        NativePtr.write (NativePtr.ofNativeInt<float32> (p + nativeint (i <<< 2))) v
    let inline private memcpy (src : nativeint) (dst : nativeint) (bytes : int) =
        System.Buffer.MemoryCopy(src.ToPointer(), dst.ToPointer(), int64 bytes, int64 bytes)

    // ── matrix packing (extend the type table in gatherFor / packerFor) ──
    let private packM44 (m : M44f) (a : nativeint) (o : int) =
        wf a (o+0)  m.M00; wf a (o+1)  m.M01; wf a (o+2)  m.M02; wf a (o+3)  m.M03
        wf a (o+4)  m.M10; wf a (o+5)  m.M11; wf a (o+6)  m.M12; wf a (o+7)  m.M13
        wf a (o+8)  m.M20; wf a (o+9)  m.M21; wf a (o+10) m.M22; wf a (o+11) m.M23
        wf a (o+12) m.M30; wf a (o+13) m.M31; wf a (o+14) m.M32; wf a (o+15) m.M33

    // ── runtime support ─────────────────────────────────────────────────
    /// Whether `runtime` can run the heap path at all (multi-draw-indirect with
    /// per-draw gl_DrawID routing — works on Vulkan and GL 4.6+). Bindless
    /// per-object textures additionally need `runtime.SupportsUnboundedSamplerArrays`
    /// (Vulkan descriptor indexing; not available on GL).
    /// Whether `runtime` can run the heap path. GL backends require gl_DrawID
    /// (GL 4.6+ / GL_ARB_shader_draw_parameters) because gl_InstanceID omits
    /// gl_BaseInstance there. Vulkan does NOT require gl_DrawID: the non-instanced
    /// path uses gl_InstanceIndex with per-draw FirstInstance, and the instanced
    /// path falls back to a per-instance vertex attribute carrying slot (so the
    /// heap also works on MoltenVK, which advertises DrawParameters but MSL has no
    /// DrawIndex).
    let isSupported (runtime : IRuntime) =
        let isGL = runtime.GetType().FullName.Contains("Aardvark.Rendering.GL")
        if isGL then runtime.SupportsMultiDrawIndirectDrawId else true

    /// Throw a clear error if `runtime` cannot run the heap path. (Per-object
    /// textures degrade per effect — bindless where supported, atlas fallback
    /// otherwise, isHeapable rejection as the last resort — so they are not a
    /// hard requirement here.)
    let checkSupport (runtime : IRuntime) =
        if not (isSupported runtime) then
            failwith "Heap: GL backend requires GL 4.6+ with GL_ARB_shader_draw_parameters (gl_DrawID). Vulkan/MoltenVK do not require it (per-instance slot attribute fallback)."

    // include non-public so PRIVATE record uniforms are walked too.
    let private recordFlags = System.Reflection.BindingFlags.Public ||| System.Reflection.BindingFlags.NonPublic

    /// arena word footprint of a packable LEAF type — MUST match packerFor's sizes.
    let private leafWords (t : System.Type) : int =
        if   t = typeof<M44f> || t = typeof<Trafo3d> || t = typeof<M44d> then 16
        elif t = typeof<M33f> || t = typeof<M33d> then 9
        elif t = typeof<V4f>  || t = typeof<C4f>  || t = typeof<V4d> || t = typeof<V4i> then 4
        elif t = typeof<V3f>  || t = typeof<V3d>  || t = typeof<V3i> then 3
        elif t = typeof<V2f>  || t = typeof<V2d>  || t = typeof<V2i> then 2
        else 1

    /// FShade fixed array `Arr<N<len>, elem>` -> (len, elem), else None. (Same length
    /// extraction FShade itself uses, via Peano.)
    let private tryArr (t : System.Type) : (int * System.Type) option =
        if t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Arr<_, _>> then
            let ga = t.GetGenericArguments()
            Some (Peano.getSize ga.[0], ga.[1])
        else None

    /// A per-object uniform whose SHADER-requested type is an F# RECORD (struct or
    /// class) or a fixed array `Arr<N,'T>` is packed/gathered element-by-element by
    /// the composite walker rather than the leaf table — generalising the heap over
    /// arbitrary uniform shapes. The arena layout is PRIVATE to pack+gather (tight,
    /// no std140 padding); FShade's own member/index access resolves on the
    /// reconstructed value. (DU follows.)
    let private isCompositeType (t : System.Type) : bool =
        Microsoft.FSharp.Reflection.FSharpType.IsRecord(t, recordFlags)
        || Microsoft.FSharp.Reflection.FSharpType.IsUnion(t, recordFlags)
        || (tryArr t).IsSome

    /// tight arena word footprint of a (leaf or composite) requested type.
    let rec private wordsOfType (t : System.Type) : int =
        match tryArr t with
        | Some (len, elem) -> len * wordsOfType elem
        | None ->
            if Microsoft.FSharp.Reflection.FSharpType.IsUnion(t, recordFlags) then
                // tag word + every case's fields concatenated
                1 + (Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(t, recordFlags)
                     |> Array.sumBy (fun ci -> ci.GetFields() |> Array.sumBy (fun f -> wordsOfType f.PropertyType)))
            elif Microsoft.FSharp.Reflection.FSharpType.IsRecord(t, recordFlags) then
                Microsoft.FSharp.Reflection.FSharpType.GetRecordFields(t, recordFlags)
                |> Array.sumBy (fun f -> wordsOfType f.PropertyType)
            else leafWords t

    /// Type-driven LEAF gather: given the base element offset, build the expression
    /// that reconstructs a value of leaf `typ`. (Composites go through gatherFor.)
    let private gatherLeaf (typ : System.Type) (off : Expr<int>) : Expr =
        // int/uint/bool/int-vectors are stored BIT-EXACT in the int arena view
        // (HeapDataI), not float-widened — no 2^24-mantissa loss. uint reads the
        // same int word and reinterprets (GLSL `uint(int)` is mod-2^32 = bitcast);
        // bool is a 0/1 word. (packerFor writes the matching bit pattern via `wi`.)
        if   typ = typeof<int>     then <@ uniform.HeapDataI.[%off] @>.Raw
        elif typ = typeof<uint32>  then <@ uint (uniform.HeapDataI.[%off]) @>.Raw
        elif typ = typeof<bool>    then <@ uniform.HeapDataI.[%off] <> 0 @>.Raw
        elif typ = typeof<V2i> then <@ let o = %off in V2i(uniform.HeapDataI.[o], uniform.HeapDataI.[o+1]) @>.Raw
        elif typ = typeof<V3i> then <@ let o = %off in V3i(uniform.HeapDataI.[o], uniform.HeapDataI.[o+1], uniform.HeapDataI.[o+2]) @>.Raw
        elif typ = typeof<V4i> then <@ let o = %off in V4i(uniform.HeapDataI.[o], uniform.HeapDataI.[o+1], uniform.HeapDataI.[o+2], uniform.HeapDataI.[o+3]) @>.Raw
        elif typ = typeof<float32> then <@ uniform.HeapData.[%off] @>.Raw
        elif typ = typeof<C4f> then <@ let o = %off in C4f(uniform.HeapData.[o], uniform.HeapData.[o+1], uniform.HeapData.[o+2], uniform.HeapData.[o+3]) @>.Raw
        elif typ = typeof<V2f> then <@ let o = %off in V2f(uniform.HeapData.[o], uniform.HeapData.[o+1]) @>.Raw
        elif typ = typeof<V3f> then <@ let o = %off in V3f(uniform.HeapData.[o], uniform.HeapData.[o+1], uniform.HeapData.[o+2]) @>.Raw
        elif typ = typeof<V4f> then <@ let o = %off in V4f(uniform.HeapData.[o], uniform.HeapData.[o+1], uniform.HeapData.[o+2], uniform.HeapData.[o+3]) @>.Raw
        elif typ = typeof<M33f> then
            <@ let o = %off in
               M33f(uniform.HeapData.[o+0], uniform.HeapData.[o+1], uniform.HeapData.[o+2],
                    uniform.HeapData.[o+3], uniform.HeapData.[o+4], uniform.HeapData.[o+5],
                    uniform.HeapData.[o+6], uniform.HeapData.[o+7], uniform.HeapData.[o+8]) @>.Raw
        // DOUBLE-precision requests get REAL doubles from the native double arena view
        // (HeapDataD), never f32 widened. The header stores a WORD (float-index) offset;
        // the region is 8-byte aligned, so the double index is off >>> 1.
        elif typ = typeof<V2d> then <@ let d = %off >>> 1 in V2d(uniform.HeapDataD.[d], uniform.HeapDataD.[d+1]) @>.Raw
        elif typ = typeof<V3d> then <@ let d = %off >>> 1 in V3d(uniform.HeapDataD.[d], uniform.HeapDataD.[d+1], uniform.HeapDataD.[d+2]) @>.Raw
        elif typ = typeof<V4d> then <@ let d = %off >>> 1 in V4d(uniform.HeapDataD.[d], uniform.HeapDataD.[d+1], uniform.HeapDataD.[d+2], uniform.HeapDataD.[d+3]) @>.Raw
        elif typ = typeof<M33d> then
            <@ let d = %off >>> 1 in
               M33d(uniform.HeapDataD.[d+0], uniform.HeapDataD.[d+1], uniform.HeapDataD.[d+2],
                    uniform.HeapDataD.[d+3], uniform.HeapDataD.[d+4], uniform.HeapDataD.[d+5],
                    uniform.HeapDataD.[d+6], uniform.HeapDataD.[d+7], uniform.HeapDataD.[d+8]) @>.Raw
        elif typ = typeof<M44d> then
            <@ let d = %off >>> 1 in
               M44d(uniform.HeapDataD.[d+0],  uniform.HeapDataD.[d+1],  uniform.HeapDataD.[d+2],  uniform.HeapDataD.[d+3],
                    uniform.HeapDataD.[d+4],  uniform.HeapDataD.[d+5],  uniform.HeapDataD.[d+6],  uniform.HeapDataD.[d+7],
                    uniform.HeapDataD.[d+8],  uniform.HeapDataD.[d+9],  uniform.HeapDataD.[d+10], uniform.HeapDataD.[d+11],
                    uniform.HeapDataD.[d+12], uniform.HeapDataD.[d+13], uniform.HeapDataD.[d+14], uniform.HeapDataD.[d+15]) @>.Raw
        elif typ = typeof<M44f> then
            <@ let o = %off in
               M44f(uniform.HeapData.[o+0],  uniform.HeapData.[o+1],  uniform.HeapData.[o+2],  uniform.HeapData.[o+3],
                    uniform.HeapData.[o+4],  uniform.HeapData.[o+5],  uniform.HeapData.[o+6],  uniform.HeapData.[o+7],
                    uniform.HeapData.[o+8],  uniform.HeapData.[o+9],  uniform.HeapData.[o+10], uniform.HeapData.[o+11],
                    uniform.HeapData.[o+12], uniform.HeapData.[o+13], uniform.HeapData.[o+14], uniform.HeapData.[o+15]) @>.Raw
        else failwithf "Heap: unsupported per-draw uniform type %A" typ

    /// gather a value of `typ` (leaf OR composite) from the arena at word `off`.
    let rec private gatherFor (typ : System.Type) (off : Expr<int>) : Expr =
        if isCompositeType typ then compositeGather typ off
        else gatherLeaf typ off

    /// reconstruct a RECORD value: gather each field at its tight word offset, then
    /// build the record. A surrounding `uniform.Rec.Field` access simplifies to that
    /// field's gather; the whole-record materialisation is optimised away by FShade.
    and private compositeGather (typ : System.Type) (off : Expr<int>) : Expr =
        match tryArr typ with
        | Some (len, elem) ->
            // gather each element at its tight offset; build an FShade fixed array so a
            // surrounding `uniform.Arr.[i]` (constant OR runtime index) resolves on it.
            let ew = wordsOfType elem
            let elems =
                [ for i in 0 .. len - 1 ->
                    let o = i * ew
                    gatherFor elem (<@ %off + o @>) ]
            Expr.NewFixedArray(elem, elems)
        | None ->
        if Microsoft.FSharp.Reflection.FSharpType.IsUnion(typ, recordFlags) then
            // DU layout: tag word at `off`, then every case's fields concatenated
            // (case order, field order). Read the tag and rebuild the ACTIVE case as a
            // nested `if tag = k then CaseK(...) else …`; FShade lowers the union.
            let tagE : Expr<int> = <@ uniform.HeapDataI.[%off] @>
            let mutable c = 1
            let built =
                Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(typ, recordFlags)
                |> Array.map (fun ci ->
                    let args =
                        ci.GetFields()
                        |> Array.map (fun f ->
                            let co = c
                            let g = gatherFor f.PropertyType (<@ %off + co @>)
                            c <- c + wordsOfType f.PropertyType
                            g)
                        |> Array.toList
                    ci.Tag, Expr.NewUnionCase(ci, args))
            let mutable e = snd built.[built.Length - 1]
            for k in built.Length - 2 .. -1 .. 0 do
                let (tg, ce) = built.[k]
                e <- Expr.IfThenElse((<@ %tagE = tg @>).Raw, ce, e)
            e
        else
        let fields = Microsoft.FSharp.Reflection.FSharpType.GetRecordFields(typ, recordFlags)
        let mutable c = 0
        let args =
            fields
            |> Array.map (fun f ->
                let co = c                                   // fix the offset for this field
                let g = gatherFor f.PropertyType (<@ %off + co @>)
                c <- c + wordsOfType f.PropertyType
                g)
            |> Array.toList
        Expr.NewRecord(typ, args)

    // ── Derived-uniform system (general, à la wombat §7) ─────────────────
    // A derived uniform is a pure function of OTHER uniforms, written as an
    // expression over `uniform?Name` reads. The rewriter expands these rules to
    // a fixpoint (so a rule whose base is itself derived resolves too), then a
    // single pass turns every heap-managed read into a per-object arena gather;
    // anything left is a plain global (UBO). Rules emit only uniform reads, never
    // gathers, so each gather is a top-level substitution created exactly once —
    // FShade resolves those correctly, whereas splicing a gather into a rule
    // expression loses its storage-buffer scope. The standard trafo derivations
    // are just DATA; add your own the same way.
    type DerivedRule = Expr

    /// Standard trafo derivations. ModelTrafo is per-object (arena); the
    /// camera-dependent factors stay globals (UBO, one upload per camera move).

    /// gather a DERIVED-composite value from the DENSE uniform store (HeapUni /
    /// HeapUniD) at word `off`. Derived outputs are matrix-typed (the compose
    /// kernels write M44f/M33f, double-requested reads via the double view).
    let private uniGatherFor (typ : System.Type) (off : Expr<int>) : Expr =
        if typ = typeof<M44f> then
            (<@ let o = %off in
                M44f(uniform.HeapUni.[o+0],  uniform.HeapUni.[o+1],  uniform.HeapUni.[o+2],  uniform.HeapUni.[o+3],
                     uniform.HeapUni.[o+4],  uniform.HeapUni.[o+5],  uniform.HeapUni.[o+6],  uniform.HeapUni.[o+7],
                     uniform.HeapUni.[o+8],  uniform.HeapUni.[o+9],  uniform.HeapUni.[o+10], uniform.HeapUni.[o+11],
                     uniform.HeapUni.[o+12], uniform.HeapUni.[o+13], uniform.HeapUni.[o+14], uniform.HeapUni.[o+15]) @>).Raw
        elif typ = typeof<M33f> then
            (<@ let o = %off in
                M33f(uniform.HeapUni.[o+0], uniform.HeapUni.[o+1], uniform.HeapUni.[o+2],
                     uniform.HeapUni.[o+3], uniform.HeapUni.[o+4], uniform.HeapUni.[o+5],
                     uniform.HeapUni.[o+6], uniform.HeapUni.[o+7], uniform.HeapUni.[o+8]) @>).Raw
        elif typ = typeof<M44d> then
            (<@ let d = %off >>> 1 in
                M44d(uniform.HeapUniD.[d+0],  uniform.HeapUniD.[d+1],  uniform.HeapUniD.[d+2],  uniform.HeapUniD.[d+3],
                     uniform.HeapUniD.[d+4],  uniform.HeapUniD.[d+5],  uniform.HeapUniD.[d+6],  uniform.HeapUniD.[d+7],
                     uniform.HeapUniD.[d+8],  uniform.HeapUniD.[d+9],  uniform.HeapUniD.[d+10], uniform.HeapUniD.[d+11],
                     uniform.HeapUniD.[d+12], uniform.HeapUniD.[d+13], uniform.HeapUniD.[d+14], uniform.HeapUniD.[d+15]) @>).Raw
        elif typ = typeof<M33d> then
            (<@ let d = %off >>> 1 in
                M33d(uniform.HeapUniD.[d+0], uniform.HeapUniD.[d+1], uniform.HeapUniD.[d+2],
                     uniform.HeapUniD.[d+3], uniform.HeapUniD.[d+4], uniform.HeapUniD.[d+5],
                     uniform.HeapUniD.[d+6], uniform.HeapUniD.[d+7], uniform.HeapUniD.[d+8]) @>).Raw
        else failwithf "Heap: derived uniform requested as %A — dense uniform store supports matrix types (M33f/M44f/M33d/M44d)" typ
    let standardDerivedRules : Map<string, DerivedRule> =
        Map.ofList [
            // ViewProjTrafo is derived from its constituents so the heap never requests
            // it per-RO: ViewTrafo/ProjTrafo (like ModelTrafo) are universal constituents
            // a reasonable consumer always provides, and they're shared avals -> one
            // ref-counted region each -> a camera move is O(1) regardless of the provider.
            // (Trafo3d's `View*Proj` has .Forward = Proj.F*View.F, hence ProjTrafo*ViewTrafo.)
            // Non-heap consumers that read ViewProjTrafo directly still get it from the
            // dom's memoized cache.
            // composed in DOUBLE (constituents read as M44d -> real-double regions ->
            // fp64 compose), result converted to the shader's requested M44f. This
            // matches a CPU-side double `view*proj` (then downcast) exactly, instead of
            // the f32 in-shader product which drifts ~1ulp at triangle edges.
            "ViewProjTrafo",      <@ M44f((uniform?ProjTrafo : M44d) * (uniform?ViewTrafo : M44d)) @>.Raw
            "ModelViewProjTrafo", <@ M44f((uniform?ProjTrafo : M44d) * (uniform?ViewTrafo : M44d) * (uniform?ModelTrafo : M44d)) @>.Raw
            "ModelViewTrafo",     <@ M44f((uniform?ViewTrafo : M44d) * (uniform?ModelTrafo : M44d)) @>.Raw
        ]

    /// gl_DrawID (the sub-draw index within a multi-draw), via a GLSL intrinsic —
    /// no FShade fork. Used to route per-draw heap uniforms when the sub-draws may
    /// themselves be instanced (instanceCount > 1), so firstInstance stays 0 and
    /// gl_InstanceIndex remains each draw's LOCAL instance index. Requires
    /// VK_KHR_shader_draw_parameters (shaderDrawParameters, Vulkan 1.1 core).
    [<GLSLIntrinsic("gl_DrawID", "GL_ARB_shader_draw_parameters")>]
    let private getDrawId () : int = onlyInShaderCode "getDrawId"

    /// Rewrite an effect so that the uniforms named in `layout` become arena
    /// gathers indexed by `slot` (gl_InstanceIndex or gl_DrawID); everything else
    /// stays a UBO.
    let private rewrite (slot : Expr<int>) (layout : Map<string, int>) (fieldStride : int) (rules : Map<string, DerivedRule>) (e : Effect) =
        let off (fi : int) : Expr<int> = <@ uniform.HeapHeaders.[ %slot * %(cint fieldStride) + %(cint fi) ] @>
        // 1) Expand derived rules to a fixpoint. Rules emit only plain uniform?X
        //    reads, so this introduces no gathers — a rule whose base is itself
        //    derived is resolved on the next iteration.
        let expandDerived (eff : Effect) =
            eff |> Effect.substituteUniforms (fun name _ _ _ -> Map.tryFind name rules)
        let hasDerived (eff : Effect) = eff.Uniforms |> Map.exists (fun n _ -> rules.ContainsKey n)
        let mutable cur = e
        let mutable i = 0
        while hasDerived cur && i < 8 do cur <- expandDerived cur; i <- i + 1
        // 2) Single pass: every heap-managed read becomes a top-level arena
        //    gather (created exactly once, all StorageBuffer scope — no mixing).
        cur |> Effect.substituteUniforms (fun name typ _ _ ->
            match Map.tryFind name layout with
            | Some fi -> Some (gatherFor typ (off fi))
            | None -> None)

    // ── per-object textures via per-SAMPLER bindless arrays ────────────────
    // NO static sampler arrays. Each input sampler is rewritten to read its OWN generated
    // bindless array uniform "HeapTexArr<si>" (si = the sampler's index in the effect),
    // indexed by its own "HeapTexIdx<si>" storage buffer. The array carries that sampler's
    // OWN state (set in effect.Uniforms by overrideSamplerStates). No pool, no limit, and
    // same-type samplers with different filter/wrap/compare are all correct.
    let private samplerStatic (ty : System.Type) (name : string) : obj =
        match ty.GetProperty(name, System.Reflection.BindingFlags.Public ||| System.Reflection.BindingFlags.Static) with
        | null -> null
        | p -> p.GetValue(null)
    /// any FShade sampler EXCEPT multisampled and 1d — the rewrite + per-sampler arrays are
    /// generic over the rest (2d/3d/cube, array, shadow, int/uint), so all just work.
    let private isBindlessSamplerType (ty : System.Type) =
        typeof<ISampler>.IsAssignableFrom ty &&
        (match samplerStatic ty "IsMultisampled" with :? bool as ms -> not ms | _ -> false) &&
        (match samplerStatic ty "Dimension" with
         | :? SamplerDimension as d -> d = SamplerDimension.Sampler2d || d = SamplerDimension.Sampler3d || d = SamplerDimension.SamplerCube
         | _ -> false)
    let private heapTexArrName (si : int) = sprintf "HeapTexArr%d" si
    let private heapTexIdxName (si : int) = sprintf "HeapTexIdx%d" si

    // op_Dynamic (the `uniform?…` operator) + GetArray, lifted from probe quotations, so
    // the rewrite can build dynamic-NAME uniform / storage-buffer reads.
    let private uniformProbe, opDynDef =
        match <@@ (uniform?HeapProbe : int[]) @@> with
        | Patterns.Call(None, mi, [u; _]) -> u, mi.GetGenericMethodDefinition()
        | _ -> failwith "Heap: could not lift op_Dynamic"
    let private getArrDef =
        match <@@ ([| 0 |]).[0] @@> with
        | Patterns.Call(None, mi, _) -> mi.GetGenericMethodDefinition()
        | _ -> failwith "Heap: could not lift GetArray"
    let private opDyn (ret : System.Type) (scope : Expr) (name : string) =
        Expr.Call(opDynDef.MakeGenericMethod ret, [ scope; Expr.Value name ])

    /// read object `slot`'s si-th sampler (type `ty`) from ITS OWN generated array HeapTexArr<si>
    let private samplerReadFor (ty : System.Type) (slot : Expr<int>) (si : int) : Expr =
        // index: (uniform?StorageBuffer?HeapTexIdx<si> : int[]).[slot]
        let sbScope = opDyn typeof<UniformScope> uniformProbe "StorageBuffer"
        let idxArr  = Expr.Cast<int[]>(opDyn typeof<int[]> sbScope (heapTexIdxName si))
        let idxE    = <@ (%idxArr).[%slot] @>
        // (uniform?HeapTexArr<si> : ty[]).[ idxE ] — op_Dynamic emits the right `sampler[]`,
        // but as a UBO member (Attribute); overrideSamplerStates re-tags it as a SamplerArray
        // uniform so it becomes a top-level sampler binding with its own state.
        let arrRead = opDyn (ty.MakeArrayType()) uniformProbe (heapTexArrName si)
        Expr.Call(getArrDef.MakeGenericMethod ty, [ arrRead; idxE.Raw ])

    // ── texture-atlas sampling (single-page, ONE sampler) ──────────────────
    // The Vulkan-1.0 / GL / MoltenVK texture path: one bound sampler over a packed
    // atlas page (HeapAtlas). Mip selection + wrap are done IN-SHADER over the
    // embedded Iliffe pyramid + 2-px gutters, so the page texture itself is one mip
    // level and one sampler — no descriptor indexing, no per-texture sampler. Faithful
    // port of wombat's atlasSample. Per-object placement (origin/size px, fmt) is
    // gathered from the arena by slot and passed in by the Sample-call rewrite.
    // Up to 8 atlas pages — heapAtlas0..heapAtlas7 — selected per-(object,sampler) by
    // HeapAtlasPageId. All 8 must be referenced in the source so FShade reflects them as
    // sampler bindings; unused slots bind to a 1×1 dummy texture at runtime.
    let private heapAtlas0 = sampler2d { texture uniform?HeapAtlasTex0; filter Filter.MinMagLinear; addressU WrapMode.Clamp; addressV WrapMode.Clamp }
    let private heapAtlas1 = sampler2d { texture uniform?HeapAtlasTex1; filter Filter.MinMagLinear; addressU WrapMode.Clamp; addressV WrapMode.Clamp }
    let private heapAtlas2 = sampler2d { texture uniform?HeapAtlasTex2; filter Filter.MinMagLinear; addressU WrapMode.Clamp; addressV WrapMode.Clamp }
    let private heapAtlas3 = sampler2d { texture uniform?HeapAtlasTex3; filter Filter.MinMagLinear; addressU WrapMode.Clamp; addressV WrapMode.Clamp }
    let private heapAtlas4 = sampler2d { texture uniform?HeapAtlasTex4; filter Filter.MinMagLinear; addressU WrapMode.Clamp; addressV WrapMode.Clamp }
    let private heapAtlas5 = sampler2d { texture uniform?HeapAtlasTex5; filter Filter.MinMagLinear; addressU WrapMode.Clamp; addressV WrapMode.Clamp }
    let private heapAtlas6 = sampler2d { texture uniform?HeapAtlasTex6; filter Filter.MinMagLinear; addressU WrapMode.Clamp; addressV WrapMode.Clamp }
    let private heapAtlas7 = sampler2d { texture uniform?HeapAtlasTex7; filter Filter.MinMagLinear; addressU WrapMode.Clamp; addressV WrapMode.Clamp }

    [<ReflectedDefinition>]
    let private atlasMirror (u : float32) =
        let t = u - floor (u * 0.5f) * 2.0f
        1.0f - abs (t - 1.0f)

    // one axis → atlas-pixel coord per wrap mode (0=clamp, 1=repeat, 2=mirror).
    // repeat shifts ±1px near an edge so hardware bilinear lands on the outer wrap gutter.
    [<ReflectedDefinition>]
    let private atlasAxis (uv : float32) (mo : float32) (ms : float32) (mode : int) =
        if mode = 0 then mo + (clamp 0.0f 1.0f uv) * ms
        elif mode = 2 then mo + (atlasMirror uv) * ms
        else
            let f = uv - floor uv
            let mutable p = mo + f * ms
            if p - mo < 0.5f then p <- p - 1.0f
            elif (mo + ms) - p < 0.5f then p <- p + 1.0f
            p

    // mip-k interior origin in atlas px, walking the Iliffe pyramid (matches HeapAtlas).
    [<ReflectedDefinition>]
    let private atlasMipOrigin (origin : V2f) (size : V2f) (k : int) : V2f =
        if k = 0 then origin
        else
            let x = origin.X + size.X + 4.0f
            let mutable y = origin.Y
            let mutable j = 1
            while j < k do
                y <- y + (max 1.0f (floor (size.Y / float32 (1 <<< j)))) + 4.0f
                j <- j + 1
            V2f(x, y)

    [<ReflectedDefinition>]
    let private atlasMipAt (pageId : int) (origin : V2f) (size : V2f) (k : int) (uv : V2f) (addrU : int) (addrV : int) : V4f =
        let mo = atlasMipOrigin origin size k
        let ms = V2f(max 1.0f (floor (size.X / float32 (1 <<< k))), max 1.0f (floor (size.Y / float32 (1 <<< k))))
        let p  = uniform.HeapAtlasPxSize.X
        let px = atlasAxis uv.X mo.X ms.X addrU
        // aardvark uploads PixImages bottom-left; acq origins are top-left -> feed (1-v) and flip page-Y.
        let py = atlasAxis (1.0f - uv.Y) mo.Y ms.Y addrV
        let coord = V2f(px, p - py) / p
        // switch-ladder over up to 8 atlas pages. All 8 samplers MUST be referenced so
        // FShade reflects them; runtime binds the unused ones to a 1×1 dummy texture.
        if   pageId = 0 then heapAtlas0.SampleLevel(coord, 0.0f)
        elif pageId = 1 then heapAtlas1.SampleLevel(coord, 0.0f)
        elif pageId = 2 then heapAtlas2.SampleLevel(coord, 0.0f)
        elif pageId = 3 then heapAtlas3.SampleLevel(coord, 0.0f)
        elif pageId = 4 then heapAtlas4.SampleLevel(coord, 0.0f)
        elif pageId = 5 then heapAtlas5.SampleLevel(coord, 0.0f)
        elif pageId = 6 then heapAtlas6.SampleLevel(coord, 0.0f)
        else                 heapAtlas7.SampleLevel(coord, 0.0f)

    /// sample an object's atlas tile. origin/size in atlas px; fmt packs
    /// numMips&lt;&lt;1 | addrU&lt;&lt;4 | addrV&lt;&lt;6. Manual LOD from screen-space derivatives.
    /// `pageId` selects which of heapAtlas0..7 holds the tile.
    [<ReflectedDefinition>]
    let private atlasSample (pageId : int) (origin : V2f) (size : V2f) (fmt : int) (uv : V2f) : V4f =
        let numMips = (fmt >>> 1) &&& 0x7
        let addrU   = (fmt >>> 4) &&& 0x3
        let addrV   = (fmt >>> 6) &&& 0x3
        // manual LOD from screen-space derivatives of the tile-texel coordinate.
        let duvdx = ddx (uv * size)
        let duvdy = ddy (uv * size)
        let rho   = max (Vec.length duvdx) (Vec.length duvdy)
        let maxLod = float32 (numMips - 1)
        let lod   = clamp 0.0f maxLod (log (max rho 1e-6f) / log 2.0f)
        let k0    = int (floor lod)
        let k1    = min (k0 + 1) (int maxLod)
        let f     = lod - float32 k0
        let c0    = atlasMipAt pageId origin size k0 uv addrU addrV
        let c1    = atlasMipAt pageId origin size k1 uv addrU addrV
        c0 * (1.0f - f) + c1 * f

    // The "FShade trickery": rewrite each `sampler.Sample(uv)` CALL into an
    // `atlasSample(origin, size, fmt, uv)` call. In the shader body a sampler read is a
    // `Call(None, ReadInput, [Uniform; "name"; idx])`, and `.Sample` is a `Call(Some
    // thatRead, Sample, [uv])`. We match that, pull the sampler name, and replace the
    // whole call with atlasSample fed the per-object placement gathered from the arena
    // (HeapAtlasOrigin/Size/Fmt at slot*K + k). `byName` : samplerName -> (kt, K).
    let private rewriteAtlasSamples (slot : Expr<int>) (byName : Map<string, int * int>) (e : Effect) =
        if Map.isEmpty byName then e
        else
            e |> Effect.map (fun shader ->
                let rec rw (ex : Expr) : Expr =
                    match ex with
                    | Patterns.Call(Some (Patterns.Call(None, ri, [_; Patterns.Value((:? string as nm), _); _])), mi, [uvArg])
                            when (mi.Name = "Sample" || mi.Name = "SampleLevel") && ri.Name = "ReadInput" && Map.containsKey nm byName ->
                        let (kt, kCount) = byName.[nm]
                        let idx    = <@ (%slot) * kCount + kt @>
                        let origin = <@ uniform.HeapAtlasOrigin.[%idx].XY @>
                        let size   = <@ uniform.HeapAtlasSize.[%idx].XY @>
                        let fmt    = <@ uniform.HeapAtlasFmt.[%idx] @>
                        let pageId = <@ uniform.HeapAtlasPageId.[%idx] @>
                        let uvE    = Expr.Cast<V2f>(rw uvArg)
                        // pageId selects heapAtlas0..7 inside atlasSample's switch-ladder; all
                        // 8 samplers are referenced from atlasMipAt so FShade reflects them.
                        <@@ atlasSample %pageId %origin %size %fmt %uvE @@>
                    | ExprShape.ShapeVar _ -> ex
                    | ExprShape.ShapeLambda(v, b) -> Expr.Lambda(v, rw b)
                    | ExprShape.ShapeCombination(o, args) -> ExprShape.RebuildShapeCombination(o, List.map rw args)
                Shader.withBody (rw shader.shaderBody) shader)

    /// the effect's sampler uniforms (supported types only), in stable order:
    /// (samplerName, textureName, samplerType, samplerState). The sampler's NAME is the
    /// shader binding (e.g. "diffuse"); the TEXTURE it reads (e.g. "DiffuseTexture", from
    /// UniformValue.Sampler) is what the RO provides; the STATE (filter/wrap/…) must be
    /// re-applied to the generated array — see overrideSamplerStates.
    let private samplerUniforms (e : Effect) : (string * string * System.Type * SamplerState)[] =
        e.Uniforms |> Map.toArray
        |> Array.choose (fun (n, p) ->
            if isBindlessSamplerType p.uniformType then
                let tn, st =
                    match p.uniformValue with
                    | UniformValue.Sampler(tn, st) -> tn, st
                    | UniformValue.SamplerArray arr when arr.Length > 0 -> fst arr.[0], snd arr.[0]
                    | _ -> n, SamplerState.empty
                Some (n, tn, p.uniformType, st)
            else None)

    /// rewrite each sampler read into ITS OWN generated bindless array read.
    /// `byName` : samplerName -> (samplerType, si)
    let private rewriteSamplers (slot : Expr<int>) (byName : Map<string, System.Type * int>) (e : Effect) =
        if Map.isEmpty byName then e
        else
            e |> Effect.substituteUniforms (fun name _ _ _ ->
                match Map.tryFind name byName with
                | Some (ty, si) -> Some (samplerReadFor ty slot si)
                | None -> None)

    /// re-apply the original sampler STATE to the generated per-type array uniforms.
    /// Codegen reads SamplerState from effect.Uniforms; the module-level arrays carry a
    /// DEFAULT (empty) state, so without this the heap would silently sample with the
    /// wrong filter/wrap. `states` : arrayUniformName -> desired state.
    let private overrideSamplerStates (states : Map<string, SamplerState>) (e : Effect) =
        if Map.isEmpty states then e
        else
            e |> Effect.map (fun shader ->
                let us =
                    shader.shaderUniforms |> Map.map (fun n p ->
                        match Map.tryFind n states with
                        | Some st ->
                            let nv =
                                match p.uniformValue with
                                | UniformValue.SamplerArray arr -> UniformValue.SamplerArray (arr |> Array.map (fun (tn, _) -> tn, st))
                                | UniformValue.Sampler(tn, _)   -> UniformValue.Sampler(tn, st)
                                // op_Dynamic gave a generic Attribute uniform — re-tag it as an
                                // (unbounded) sampler array so FShade binds it top-level, not in a UBO.
                                | _ -> UniformValue.SamplerArray [| (n, st) |]
                            { p with uniformValue = nv }
                        | None -> p)
                { shader with shaderUniforms = us })

    // ── RO-level integration ────────────────────────────────────────────
    // The actual encode-win path: collapse an aset<IRenderObject> of N draws
    // into B bucket render objects (one per effect), each rendered as ONE
    // indirect multidraw against a shared arena. Reuses the standard
    // CompileRender / CommandTask machinery (so the command stream encodes
    // O(buckets), and binds ONE descriptor set per bucket instead of N).
    //
    // Assumptions: inputs are `RenderObject`s sharing geometry layout within a
    // bucket; the per-draw heap fields (auto-detected, part of the bucket key)
    // are present on every member with a consistent type by construction.
    // There is NO global/per-object uniform distinction: every effect-consumed,
    // RO-supplied uniform is a ref-counted arena region (a scene-wide value is one
    // region with refcount = member count); anything the RO does not supply stays a
    // plain uniform read resolved by the backend/task scope, never by a member.
    // Membership changes are incremental (per-bucket
    // O(changed) diffs); per-draw value marks flow through the reactive arena
    // with offsets/headers held constant.

    /// write an int into a ring word slot (bit pattern; the int arena view
    /// HeapDataI reads it back exactly) — used for int/uint/bool/int-vector fields.
    let inline private wi (a : nativeint) (i : int) (n : int) =
        NativePtr.write (NativePtr.ofNativeInt<int> (a + nativeint (i <<< 2))) n

    let private packerFor (t : System.Type) : int * (obj -> nativeint -> int -> unit) =
        if   t = typeof<M44f>    then 16, (fun o a off -> packM44 (o :?> M44f) a off)
        elif t = typeof<Trafo3d> then 16, (fun o a off -> packM44 (M44f.op_Explicit (o :?> Trafo3d).Forward) a off)
        elif t = typeof<M44d>    then 16, (fun o a off -> packM44 (M44f.op_Explicit (o :?> M44d)) a off)
        elif t = typeof<V4f>     then 4,  (fun o a off -> let v = o :?> V4f in wf a off v.X; wf a (off+1) v.Y; wf a (off+2) v.Z; wf a (off+3) v.W)
        elif t = typeof<C4f>     then 4,  (fun o a off -> let c = o :?> C4f in wf a off c.R; wf a (off+1) c.G; wf a (off+2) c.B; wf a (off+3) c.A)
        elif t = typeof<V3f>     then 3,  (fun o a off -> let v = o :?> V3f in wf a off v.X; wf a (off+1) v.Y; wf a (off+2) v.Z)
        elif t = typeof<V2f>     then 2,  (fun o a off -> let v = o :?> V2f in wf a off v.X; wf a (off+1) v.Y)
        elif t = typeof<V3d>     then 3,  (fun o a off -> let v = o :?> V3d in wf a off (float32 v.X); wf a (off+1) (float32 v.Y); wf a (off+2) (float32 v.Z))
        elif t = typeof<V2d>     then 2,  (fun o a off -> let v = o :?> V2d in wf a off (float32 v.X); wf a (off+1) (float32 v.Y))
        elif t = typeof<V4d>     then 4,  (fun o a off -> let v = o :?> V4d in wf a off (float32 v.X); wf a (off+1) (float32 v.Y); wf a (off+2) (float32 v.Z); wf a (off+3) (float32 v.W))
        elif t = typeof<M33d>    then 9,  (fun o a off -> let m = o :?> M33d in wf a off (float32 m.M00); wf a (off+1) (float32 m.M01); wf a (off+2) (float32 m.M02); wf a (off+3) (float32 m.M10); wf a (off+4) (float32 m.M11); wf a (off+5) (float32 m.M12); wf a (off+6) (float32 m.M20); wf a (off+7) (float32 m.M21); wf a (off+8) (float32 m.M22))
        elif t = typeof<M33f>    then 9,  (fun o a off -> let m = o :?> M33f in wf a off m.M00; wf a (off+1) m.M01; wf a (off+2) m.M02; wf a (off+3) m.M10; wf a (off+4) m.M11; wf a (off+5) m.M12; wf a (off+6) m.M20; wf a (off+7) m.M21; wf a (off+8) m.M22)
        elif t = typeof<float32> then 1,  (fun o a off -> wf a off (o :?> float32))
        elif t = typeof<float>   then 1,  (fun o a off -> wf a off (float32 (o :?> float)))
        // BIT-EXACT integral fields -> the int arena view (no float32 mantissa loss)
        elif t = typeof<int>     then 1,  (fun o a off -> wi a off (o :?> int))
        elif t = typeof<uint32>  then 1,  (fun o a off -> wi a off (int (o :?> uint32)))
        elif t = typeof<bool>    then 1,  (fun o a off -> wi a off (if (o :?> bool) then 1 else 0))
        elif t = typeof<V2i>     then 2,  (fun o a off -> let v = o :?> V2i in wi a off v.X; wi a (off+1) v.Y)
        elif t = typeof<V3i>     then 3,  (fun o a off -> let v = o :?> V3i in wi a off v.X; wi a (off+1) v.Y; wi a (off+2) v.Z)
        elif t = typeof<V4i>     then 4,  (fun o a off -> let v = o :?> V4i in wi a off v.X; wi a (off+1) v.Y; wi a (off+2) v.Z; wi a (off+3) v.W)
        else failwithf "Heap: unsupported per-draw uniform content type %A" t

    /// A uniform the SHADER requests at double precision is stored as REAL doubles:
    /// 2 arena words per scalar (the double's bit pattern), 8-byte aligned so the
    /// native double view (HeapDataD) reads it back exactly — no f32 downcast.
    let private isDoubleUniform (t : System.Type) =
        t = typeof<V2d> || t = typeof<V3d> || t = typeof<V4d> || t = typeof<M33d> || t = typeof<M44d>
    // write one double as 2 consecutive arena words (bit-exact; netstandard2.0 has no
    // Int32BitsToSingle, so reinterpret the 8 bytes as two float32 slots). fp64 path:
    // the GPU's native double view reads these 2 words back as one IEEE double.
    let inline private wd (a : nativeint) (i : int) (d : float) =
        NativePtr.write (NativePtr.ofNativeInt<float> (a + nativeint (i <<< 2))) d
    // df32 path: write the double as a (hi, lo) two-f32 pair — hi = round-to-f32(d),
    // lo = round-to-f32(d − hi) — so the df32 kernels read it as V2f(hi,lo). Same 2
    // words / same 8-byte slot as `wd`; only the CONTENT differs.
    let private wdDf (a : nativeint) (i : int) (d : float) =
        let hi = float32 d
        wf a i hi
        wf a (i+1) (float32 (d - float hi))
    // coerce a PROVIDED boxed uniform value to the shader's REQUESTED double type — the
    // write converts to what the shader asked for, at full precision (upcast f32
    // siblings; extract Trafo3d.Forward). This is what lets the derived ModelView/MVP
    // compose chain run in double from constituents the consumer provides as Trafo3d.
    let private asV2d (o:obj) = match o with | :? V2d as v -> v | :? V2f as v -> V2d v | _ -> failwithf "Heap: cannot convert %s to V2d" (o.GetType().Name)
    let private asV3d (o:obj) = match o with | :? V3d as v -> v | :? V3f as v -> V3d v | _ -> failwithf "Heap: cannot convert %s to V3d" (o.GetType().Name)
    let private asV4d (o:obj) = match o with | :? V4d as v -> v | :? V4f as v -> V4d v | _ -> failwithf "Heap: cannot convert %s to V4d" (o.GetType().Name)
    let private asM33d (o:obj) = match o with | :? M33d as m -> m | :? M33f as m -> M33d m | _ -> failwithf "Heap: cannot convert %s to M33d" (o.GetType().Name)
    let private asM44d (o:obj) = match o with | :? M44d as m -> m | :? M44f as m -> M44d m | :? Trafo3d as t -> t.Forward | _ -> failwithf "Heap: cannot convert %s to M44d" (o.GetType().Name)
    /// (words, pack) for a double-REQUESTED type — words = 2 * scalarCount. The pack
    /// coerces whatever the provider gave to the requested double type. `df32` picks
    /// the (hi,lo) two-f32 encoding (MoltenVK, no shaderFloat64) over the IEEE double
    /// bit pattern; the WORD layout (2 words/scalar) is identical either way.
    let private doublePackerFor (df32 : bool) (t : System.Type) : int * (obj -> nativeint -> int -> unit) =
        let wd = if df32 then wdDf else wd
        if   t = typeof<V2d>  then 4,  (fun o a off -> let v = asV2d o in wd a off v.X; wd a (off+2) v.Y)
        elif t = typeof<V3d>  then 6,  (fun o a off -> let v = asV3d o in wd a off v.X; wd a (off+2) v.Y; wd a (off+4) v.Z)
        elif t = typeof<V4d>  then 8,  (fun o a off -> let v = asV4d o in wd a off v.X; wd a (off+2) v.Y; wd a (off+4) v.Z; wd a (off+6) v.W)
        elif t = typeof<M33d> then 18, (fun o a off -> let m = asM33d o in wd a off m.M00; wd a (off+2) m.M01; wd a (off+4) m.M02; wd a (off+6) m.M10; wd a (off+8) m.M11; wd a (off+10) m.M12; wd a (off+12) m.M20; wd a (off+14) m.M21; wd a (off+16) m.M22)
        elif t = typeof<M44d> then 32, (fun o a off -> let m = asM44d o in wd a (off+0) m.M00; wd a (off+2) m.M01; wd a (off+4) m.M02; wd a (off+6) m.M03; wd a (off+8) m.M10; wd a (off+10) m.M11; wd a (off+12) m.M12; wd a (off+14) m.M13; wd a (off+16) m.M20; wd a (off+18) m.M21; wd a (off+20) m.M22; wd a (off+22) m.M23; wd a (off+24) m.M30; wd a (off+26) m.M31; wd a (off+28) m.M32; wd a (off+30) m.M33)
        else failwithf "Heap: unsupported double per-draw uniform type %A" t

    /// pack a (leaf OR composite) value into staging at word `off`, keyed on the
    /// shader-REQUESTED type so the layout matches `gatherFor`. RECORDS recurse
    /// field-by-field (tight, same order as the gather); leaves use packerFor. The
    /// supplied value must structurally match the requested record type.
    let rec private compositePacker (t : System.Type) : int * (obj -> nativeint -> int -> unit) =
        match tryArr t with
        | Some (len, elem) ->
            // supplied value is a .NET array (T[]) or an Arr<N,T> — read `len` elements
            // and pack each at its element offset, matching compositeGather's layout.
            let (ew, epk) = compositePacker elem
            let itemProp = t.GetProperty("Item")
            len * ew, (fun o a off ->
                let get : int -> obj =
                    match o with
                    | :? System.Array as arr -> fun i -> arr.GetValue i
                    | _ -> fun i -> itemProp.GetValue(o, [| box i |])
                for i in 0 .. len - 1 do epk (get i) a (off + i * ew))
        | None ->
        if Microsoft.FSharp.Reflection.FSharpType.IsUnion(t, recordFlags) then
            // write the tag at word 0, then the ACTIVE case's fields at their reserved
            // offsets (matching compositeGather); inactive cases' slots stay zero.
            let tagReader = Microsoft.FSharp.Reflection.FSharpValue.PreComputeUnionTagReader(t, recordFlags)
            let mutable c = 1
            let cases =
                Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(t, recordFlags)
                |> Array.map (fun ci ->
                    let reader = Microsoft.FSharp.Reflection.FSharpValue.PreComputeUnionReader(ci, recordFlags)
                    let parts =
                        ci.GetFields()
                        |> Array.map (fun f ->
                            let o = c
                            let (w, pk) = compositePacker f.PropertyType
                            c <- c + w
                            o, pk)
                    ci.Tag, reader, parts)
            c, (fun o a off ->
                let tag = tagReader o
                wi a off tag
                let (_, reader, parts) = cases |> Array.find (fun (tg, _, _) -> tg = tag)
                let vals = reader o
                parts |> Array.iteri (fun j (rel, pk) -> pk vals.[j] a (off + rel)))
        elif Microsoft.FSharp.Reflection.FSharpType.IsRecord(t, recordFlags) then
            let reader = Microsoft.FSharp.Reflection.FSharpValue.PreComputeRecordReader(t, recordFlags)
            let parts =
                Microsoft.FSharp.Reflection.FSharpType.GetRecordFields(t, recordFlags)
                |> Array.map (fun f -> compositePacker f.PropertyType)
            let total = parts |> Array.sumBy fst
            total, (fun o a off ->
                let vals = reader o
                let mutable c = off
                for i in 0 .. parts.Length - 1 do
                    let (w, pk) = parts.[i]
                    pk vals.[i] a c
                    c <- c + w)
        else packerFor t

    /// Symbol.Create allocates per call — per-part call sites go through this
    /// cache instead (name sets are tiny: effect inputs, derived bases).
    let private symCache = System.Collections.Concurrent.ConcurrentDictionary<string, Symbol>()
    let private symFactory = System.Func<string, Symbol>(fun s -> Symbol.Create s)
    let internal cachedSym (s : string) = symCache.GetOrAdd(s, symFactory)

    /// Size in bytes of a blittable attribute/index element type (-1 if it isn't
    /// blittable — such an RO is then treated as un-heapable and passed through).
    /// Cached per type: Marshal.SizeOf is a marshalling-info lookup and the
    /// eligibility checks call this several times per classified RO.
    let private elemSizeCache = System.Collections.Concurrent.ConcurrentDictionary<System.Type, int>()
    // static factory: `GetOrAdd(t, fun ...)` at the call site allocates a fresh
    // Func per CALL (F# closure conversion) — ~115 MB over a large ingest.
    let private elemSizeFactory =
        System.Func<System.Type, int>(fun t -> try System.Runtime.InteropServices.Marshal.SizeOf t with _ -> -1)
    let private elemSize (t : System.Type) =
        elemSizeCache.GetOrAdd(t, elemSizeFactory)

    /// Byte length of a buffer-view's data (respecting its byte Offset). Assumes a
    /// tightly-packed view; interleaved/strided views are rejected by `isHeapable`.
    let private geomByteLen' (value : IBuffer) (bv : BufferView) : int =
        match value with
        | :? INativeBuffer as nb -> int nb.SizeInBytes - bv.Offset
        | :? IBackendBuffer as gb -> int gb.SizeInBytes - bv.Offset
        | b -> failwithf "Heap.ofRenderObjects: geometry buffer is neither host nor backend buffer (%A)" (b.GetType())

    /// VALUE-level geometry dedup source: CONSTANT buffer avals are forced once
    /// at ingest and ArrayBuffer-backed ones key on the UNDERLYING ARRAY
    /// reference (ArrayBuffer.Equals semantics) — per-leaf fresh BufferView/aval
    /// wrappers around one shared array (exactly what Sg combinators +
    /// Primitives.Box produce) dedup to ONE packed allocation. Non-constant or
    /// non-ArrayBuffer sources keep aval-identity keying (their bytes may differ
    /// per evaluation / per backend handle).
    let private geomDedupSource' (value : IBuffer) (bv : BufferView) : obj =
        let b = bv.Buffer
        if b.IsConstant then
            match value with
            | :? ArrayBuffer as ab -> ab.Data :> obj
            | _ -> b :> obj
        else b :> obj

    /// Write a buffer-view's raw bytes STRAIGHT to `dst` (a ring span): host
    /// (INativeBuffer) sources are one memcpy, GPU-resident (IBackendBuffer, index
    /// buffers only) download directly into the span — no intermediate byte[].
    let private stageGeomBytes' (runtime : IRuntime) (value : IBuffer) (bv : BufferView) (len : int) (dst : nativeint) =
        match value with
        | :? INativeBuffer as nb ->
            nb.Use (fun (ptr : nativeint) -> memcpy (ptr + nativeint bv.Offset) dst len)
        | :? IBackendBuffer as gb ->
            runtime.Download(gb, uint64 bv.Offset, dst, uint64 len)
        | b -> failwithf "Heap.ofRenderObjects: geometry buffer is neither host nor backend buffer (%A)" (b.GetType())

    /// Number of buckets produced by the most recent `ofRenderObjects` evaluation
    /// (diagnostic / for logging).
    let mutable lastBucketCount = 0
    /// diagnostics: live MATERIALIZED typed partitions (sum over buckets) and
    /// slots resident in the dynamic partitions — published per update.
    let mutable lastMaterializedPartitions = 0
    let mutable lastDynamicResidents = 0

    /// Count of `buildHeap` invocations = distinct GPU arenas built (diagnostic /
    /// for the deferred-path test: the shared-PerSig memo must collapse the opaque
    /// (intermediate sig) + transparent (user sig) expands to ONE build, not two).
    let mutable buildInvocations = 0

    // STARTUP instrumentation: CPU ingest (AddInternal: geometry copy + per-slot setup)
    // vs GPU upload (arena Compute). One-shot logged once the first big upload lands.
    let mutable internal stIngestMs = 0.0
    let mutable internal stIngestN = 0
    // ingest breakdown (diagnostic): fields/constituents | geometry (attrs+index) | rest
    let mutable internal stIngestFieldsMs = 0.0
    let mutable internal stIngestGeomMs = 0.0
    let mutable internal stIngestCopyMs = 0.0      // geom sub-bucket: source->ring copies (incl. Use/pin)
    let mutable internal stIngestStageMs = 0.0     // geom sub-bucket: StageWords bookkeeping
    let mutable internal stUploadMs = 0.0
    let mutable internal stUploadBytes = 0L
    let mutable internal stLogged = false

    /// The per-draw heap-field names detected for the most recently classified
    /// heapable RO (diagnostic / for tests). Sorted.
    let mutable lastAutoFields : string[] = [||]

    /// When true, the heap logs ONE deduped, actionable line per pass-through
    /// reason (why an RO was not heap-eligible: attribute/buffer type, missing
    /// effect input, sampler mismatch, non-Effect surface, …) and per consumed-
    /// but-unpackable per-draw uniform. Default false (zero logging overhead).
    let mutable Diagnostics = false
    let private diagSeen = System.Collections.Generic.HashSet<string>()
    let internal diag (msg : string) =
        if Diagnostics then
            let fresh = lock diagSeen (fun () -> diagSeen.Add msg)
            if fresh then Log.warn "[Heap] %s" msg
    /// the deduped diagnostic messages emitted so far (testing/tooling)
    let diagnosticMessages () : string[] =
        lock diagSeen (fun () -> Seq.toArray diagSeen)

    /// Force the texture-atlas path even where descriptor-indexed sampler arrays ARE
    /// available (for testing the atlas on desktop Vulkan, which reports them supported).
    let mutable forceAtlas = false

    /// Storage-arena footprint (bytes) of the most recently updated bucket
    /// (diagnostic). Geometry (attribute/index allocations), singleton
    /// attributes and per-draw uniform regions all live in ONE arena, so this
    /// equals lastArenaBytes. Under exact-size distinct-geometry churn it stays
    /// FLAT: a freed allocation's ranges are recycled in place.
    let mutable lastPackedGeomBytes = 0

    // ── reclamation knobs + diagnostics (per-bucket values, written by the most
    //    recently updated bucket — like lastPackedGeomBytes) ──
    /// Compaction trigger floor: a bucket buffer (packed geometry / arena floats /
    /// per-instance slot attributes) is compacted during the delta pass when its
    /// live bytes fall below 50% of its high-water AND the waste exceeds this
    /// absolute floor. Compaction is O(live buffer bytes) and re-uploads that
    /// buffer once — the same cost class as growth doubling: between two fires the
    /// waste (= freed bytes since the last fire) must exceed max(live, floor), so
    /// the copy cost amortizes over at least as many freed bytes.
    let mutable compactionWasteFloorBytes = 4 * 1024 * 1024
    /// Cumulative number of buffer compactions performed (diagnostic). Exact-size
    /// churn must never bump this (reuse is hit before any compaction triggers).
    let mutable compactionCount = 0
    /// LIVE bytes of the most recently flushed bucket's packed geometry (the
    /// referenced subset of lastPackedGeomBytes).
    let mutable lastPackedGeomLiveBytes = 0
    /// uniform-arena address-space bytes (high-water cursor) of the most recently
    /// flushed bucket / the live (referenced) subset thereof.
    let mutable lastArenaBytes = 0
    let mutable lastArenaLiveBytes = 0
    /// per-instance slot-attribute buffer bytes (MoltenVK instanced fallback) of
    /// the most recently flushed bucket / the live subset thereof.
    let mutable lastInstBytes = 0
    let mutable lastInstLiveBytes = 0
    /// Test knob: pretend gl_DrawID is unavailable, forcing the MoltenVK
    /// per-instance slot-attribute fallback for instanced buckets on any backend
    /// (affects only buckets created while set).
    let mutable forceNoDrawId = false

    /// Disable CLUSTERED draw records (size-class instanced draws) — falls back to
    /// one record per slot. For A/B measurement; also settable via HEAP_NO_CLUSTERS=1.
    let mutable DisableClusters =
        match System.Environment.GetEnvironmentVariable "HEAP_NO_CLUSTERS" with
        | null | "" -> false
        | s -> let s = s.Trim().ToLowerInvariant() in s = "1" || s = "true" || s = "on"

    /// Padded drawn-vertex-count ladder for CLUSTERED records. All sizes are
    /// multiples of 3 (TriangleList), so a slot's padding lanes form whole
    /// degenerate triangles (every lane clamps to the slot's last vertex -> zero
    /// area, culled). ~9/8 steps bound padding waste at ~12% worst in the geometric
    /// regime (records are ~free, so the dense ladder costs nothing);
    /// slots above the cap keep an exact per-slot record.
    let internal clusterClassSizes =
        let sizes = System.Collections.Generic.List<int>()
        let mutable c = 3
        while c <= 4608 do
            sizes.Add c
            c <- max (c + 3) ((c * 9 / 8 + 2) / 3 * 3)
        sizes.ToArray()

    /// index of the smallest class >= vc, or -1 when vc exceeds the cap (per-slot record)
    let internal clusterClassOf (vc : int) =
        let mutable i = 0
        while i < clusterClassSizes.Length && clusterClassSizes.[i] < vc do i <- i + 1
        if i < clusterClassSizes.Length then i else -1

    /// Extract a host PixImage&lt;byte&gt; (RGBA) from an ITexture for atlas packing.
    let private toAtlasPixImage (t : ITexture) : PixImage<byte> =
        match t with
        | :? PixTexture2d as pt -> pt.PixImageMipMap.[0].ToPixImage<byte>()
        | _ -> failwithf "Heap atlas: unsupported ITexture %A (host PixTexture2d only)" (t.GetType())

    /// Distinct trafo-link slots uploaded on the most recent chain-arena flush
    /// (diagnostic). A shared-root change over N objects should be 1, not N.
    let mutable lastChainLinkUploads = 0

    /// Number of DISTINCT trafo-link slots in the most recently built chain arena
    /// (diagnostic). With value-dedup, N leaves each carrying an identical constant
    /// box link + a distinct dynamic node link give ~N+1 distinct slots, not 2N.
    let mutable lastDistinctLinks = 0

    /// Number of buckets in the LIVE ofRenderObjects path that took the GPU
    /// trafo-chain (chainMode) on the most recent evaluation (diagnostic).
    let mutable lastChainBuckets = 0

    /// Force-disable the GPU trafo-chain (chainMode) even for ROs that expose
    /// ModelTrafoStack — they fall back to the CPU-folded ModelTrafo arena region.
    /// A knob for A/B measurement of the chain vs folded path on the SAME inputs.
    /// Defaults from AARDVARK_HEAP_NOCHAIN=1; settable directly.
    let mutable disableChain =
        System.Environment.GetEnvironmentVariable "AARDVARK_HEAP_NOCHAIN" = "1"

    /// Kill-switch for the 0043 adaptive machinery (geometry re-upload writers,
    /// draw-call / pick-id / model-stack watchers): reverts to snapshot-at-add
    /// behavior for BISECTING regressions. Defaults from
    /// AARDVARK_HEAP_STATIC_GEOM=1; settable directly.
    let mutable disableDynGeom =
        System.Environment.GetEnvironmentVariable "AARDVARK_HEAP_STATIC_GEOM" = "1"

    // ── per-allocation headers (wombat parity: pools.ts writeAttribute) ──────
    // Every host geometry allocation in the bucket arena (vertex attribute,
    // singleton attribute, index range) starts with a 4-word header
    //   word0 = typeId   (encoding class; BOTH the index decode and the
    //                     attribute decode branch on it at fetch time — the
    //                     source element type is per allocation, never part
    //                     of the bucket key; see attrTypeId / decodeHeapV4f)
    //   word1 = length   (element count; 1 for singletons — the attribute
    //                     fetch broadcasts via `vid % length`, wombat-style)
    //   word2 = stride   (bytes per element; 0 for singletons)
    //   word3 = 0        (pad — data starts 16 bytes after the header start)
    // Allocations are NOT globally 16-byte aligned (the decode is scalar word
    // reads, unlike wombat's vec4 loads), so no alignment padding is needed.
    [<Literal>]
    let internal AllocHeaderWords = 4
    /// index-allocation typeIds (header word0): 1 = 32-bit, 2 = 16-bit elements
    [<Literal>]
    let internal IdxType32 = 1
    [<Literal>]
    let internal IdxType16 = 2

    /// Adaptive writer for one arena region. Reads its source aval and packs
    /// the floats into the arena's shared staging at its offset. Marked (via
    /// the source) only when that source changes.
    /// HEAP_EDIT_PROF=1 — accumulate CPU ms per named phase across the frame
    /// (updater phases + every mirror flush); dumped at the next updater entry.
    module internal EditProf =
        let enabled = System.Environment.GetEnvironmentVariable "HEAP_EDIT_PROF" = "1"
        let private times = System.Collections.Generic.Dictionary<string, float>()
        let addMs (name : string) (ms : float) =
            if enabled then
                lock times (fun () ->
                    times.[name] <- (match times.TryGetValue name with | true, v -> v | _ -> 0.0) + ms)
        let inline time (name : string) (f : unit -> 'a) : 'a =
            if not enabled then f ()
            else
                let t0 = System.Diagnostics.Stopwatch.GetTimestamp()
                let r = f ()
                let ms = float (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / float System.Diagnostics.Stopwatch.Frequency
                lock times (fun () ->
                    times.[name] <- (match times.TryGetValue name with | true, v -> v | _ -> 0.0) + ms)
                r
        let private counts = System.Collections.Generic.Dictionary<string, int * int>()
        let count (name : string) (words : int) =
            if enabled then
                lock counts (fun () ->
                    let (c, w) = match counts.TryGetValue name with | true, v -> v | _ -> (0, 0)
                    counts.[name] <- (c + 1, w + words))
        let dump () =
            if enabled then
                lock times (fun () ->
                    let str =
                        times
                        |> Seq.sortByDescending (fun kv -> kv.Value)
                        |> Seq.filter (fun kv -> kv.Value >= 0.05)
                        |> Seq.map (fun kv -> sprintf "%s=%.2f" kv.Key kv.Value)
                        |> String.concat " "
                    let cstr =
                        lock counts (fun () ->
                            let r =
                                counts
                                |> Seq.sortByDescending (fun kv -> fst kv.Value)
                                |> Seq.map (fun kv -> sprintf "%s=%dx/%dw" kv.Key (fst kv.Value) (snd kv.Value))
                                |> String.concat " "
                            counts.Clear(); r)
                    if str <> "" || cstr <> "" then Log.line "[editprof] %s | %s" str cstr
                    times.Clear())

    type internal RegionWriter(src : IAdaptiveValue, off : int, size : int, pack : obj -> nativeint -> int -> unit) =
        inherit AdaptiveObject()
        do src.Acquire()
        let mutable off = off
        /// the region's arena offset. MUTABLE: arena compaction re-seats live
        /// regions (the writer keeps its subscription; only future packs target
        /// the new offset — the compactor moves the staged bytes itself).
        member _.Off with get () = off and set v = off <- v
        member _.Size = size
        /// pack the region's words through `ptr` (the region's OWN ring span base).
        member x.Pack(token : AdaptiveToken, ptr : nativeint) =
            x.EvaluateIfNeeded token () (fun token -> pack (src.GetValueUntyped token) ptr 0)
        member x.Dispose() =
            src.Release()
            src.Outputs.Remove x |> ignore
            x.Outputs.Clear()

    /// MIRROR-LESS dirty-tracking arena buffer. There is no host copy of arena
    /// payload: every write stages into an UPLOAD RING — a persistently mapped
    /// host-visible buffer (pinned-array + per-region Upload fallback when the
    /// backend exposes no mapping) — as (ringOff, arenaOff, words) regions, and
    /// `Compute` issues ONE multi-region ring→arena copy per ordered batch. A
    /// batch splits only when a later write overlaps an earlier one in the same
    /// cycle (freed block re-allocated — copy order must stay defined) or when a
    /// compaction move-set is queued. Resize preserves content device-side
    /// (AdaptiveBuffer's copy-on-grow); compaction moves are a device-side
    /// temp-buffer bounce (same-buffer overlapping copies are UB). The ring
    /// grows transiently during bulk ingest and shrinks back after the flush.
    type internal HeapArena(runtime : IBufferRuntime, initialFloats : int) =
        // DEVICE-LOCAL: the arena is read per-vertex every frame; host-visible put it across PCIe
        // (~1 GB/s) and cost 33x on the render (968 -> 29 ms full Vienna).
        inherit AdaptiveBuffer(runtime, uint64 (max 1 initialFloats * 4), BufferUsage.Storage, BufferStorage.Device)
        let mutable capacity = max 1 initialFloats
        let pending = LockedSet<RegionWriter>()

        // ── upload ring: a CHAIN of host-visible chunks. Mapped memory is
        //    write-combined — reading it back (e.g. for a growth memcpy) runs at
        //    a fraction of cached-RAM speed — so the ring NEVER copies: overflow
        //    allocates a new (doubled) chunk and staging continues there. Chunk
        //    pointers stay stable for the whole cycle; the flush issues one
        //    multi-region copy per (chunk, batch) and frees all but one
        //    steady-state chunk afterwards.
        let ringSteadyWords = 4 <<< 20                     // 16 MB steady-state
        // one chunk: mapped backend buffer (or null + pinned fallback array), ptr, capacity
        let ringChunks = System.Collections.Generic.List<struct(IBackendBuffer * System.Runtime.InteropServices.GCHandle * nativeint * int)>()
        let mutable ringPtr = 0n                           // current chunk write pointer
        let mutable ringWords = 0                          // current chunk capacity (words)
        let mutable cursor = 0                             // fill of the CURRENT chunk (words)

        /// allocate a fresh chunk of `n` words and make it current.
        let ringAddChunk (n : int) =
            let b = runtime.CreateBuffer(uint64 n * 4UL, BufferUsage.ReadWrite, BufferStorage.Host)
            let struct(buf, pin, p) =
                match runtime.TryGetMappedPointer b with
                | ValueSome p -> struct(b, Unchecked.defaultof<System.Runtime.InteropServices.GCHandle>, p)
                | ValueNone ->
                    b.Dispose()
                    let arr = Array.zeroCreate<float32> n
                    let pin = System.Runtime.InteropServices.GCHandle.Alloc(arr, System.Runtime.InteropServices.GCHandleType.Pinned)
                    struct(Unchecked.defaultof<IBackendBuffer>, pin, pin.AddrOfPinnedObject())
            ringChunks.Add(struct(buf, pin, p, n))
            ringPtr <- p
            ringWords <- n
            cursor <- 0

        let ringFreeChunk (struct(buf, pin, _, _) : struct(IBackendBuffer * System.Runtime.InteropServices.GCHandle * nativeint * int)) =
            if not (obj.ReferenceEquals(buf, null)) then buf.Dispose()
            elif pin.IsAllocated then pin.Free()

        let ringFree () =
            for c in ringChunks do ringFreeChunk c
            ringChunks.Clear()
            ringPtr <- 0n
            ringWords <- 0
            cursor <- 0

        // ── staged regions + ordered op stream ──────────────────────────
        // regions in write order, merged when adjacent in BOTH spaces. `ops`
        // interleaves batch cuts with compaction move-sets: entry (e, moves)
        // means "copy regions [prevE, e) as one submission, then apply `moves`
        // (null for a plain overlap cut)". Final segment [lastE, Count) is implicit.
        let regions  = System.Collections.Generic.List<struct(int * int * int * int)>()   // (chunk, ringOff, arenaOff, words)
        let ops      = System.Collections.Generic.List<struct(int * (struct(int * int * int))[])>()
        let mutable lastCut = 0                            // regions.Count at the last op cut
        // covered arena intervals of the CURRENT batch (sorted, disjoint)
        let covered  = System.Collections.Generic.List<struct(int * int)>()

        let covLowerBound (lo : int) =
            let mutable l = 0
            let mutable h = covered.Count
            while l < h do
                let m = (l + h) / 2
                let (struct(s, _)) = covered.[m]
                if s < lo then l <- m + 1 else h <- m
            l
        let covOverlaps (lo : int) (hi : int) =
            let i = covLowerBound lo
            (i < covered.Count && (let (struct(s, _)) = covered.[i] in s < hi))
            || (i > 0 && (let (struct(_, e)) = covered.[i - 1] in e > lo))
        let covAdd (lo : int) (hi : int) =
            if covered.Count > 0 && (let (struct(_, le)) = covered.[covered.Count - 1] in lo >= le) then
                let (struct(ls, le)) = covered.[covered.Count - 1]
                if lo = le then covered.[covered.Count - 1] <- struct(ls, hi)
                else covered.Add(struct(lo, hi))
            elif covered.Count = 0 then covered.Add(struct(lo, hi))
            else covered.Insert(covLowerBound lo, struct(lo, hi))

        /// Grow the arena's (GPU) capacity to hold at least n floats — the resize
        /// itself is DEFERRED to the next Compute (device-side, content-preserving),
        /// so this is rule-clean inside adaptive evaluation.
        member x.EnsureFloats(n : int) =
            if n > capacity then capacity <- Fun.NextPowerOfTwo n
        /// Shrink the arena capacity (deferred to the next Compute, applied AFTER
        /// the queued compaction moves packed the content below the new size).
        member x.ShrinkFloats(n : int) =
            let nf = max 1024 (Fun.NextPowerOfTwo (max 1 n))
            if nf < capacity then capacity <- nf

        // Staging can happen OUTSIDE this arena's own evaluation: a SECOND heap
        // build over a SHARED storage expands lazily inside ANOTHER render
        // task's pull — possibly AFTER this arena already flushed for the frame.
        // Nothing marks the (clean) arena then, so the staged regions would sit
        // in the ring forever and the new build's buckets never appear. Detect
        // stage-while-clean and schedule ONE post-evaluation Touch: the transact
        // from a pool thread serializes with the adaptive lock, so it lands
        // right after the current evaluation and the next frame flushes.
        member val private TouchScheduled = ref 0 with get
        member private x.ScheduleTouchIfClean() =
            if not x.OutOfDate && System.Threading.Interlocked.Exchange(x.TouchScheduled, 1) = 0 then
                System.Threading.Tasks.Task.Run(fun () ->
                    transact (fun () -> x.MarkOutdated())) |> ignore

        /// Reserve a ring span for arena words [off, off+words) and return its
        /// write pointer. The caller writes it IMMEDIATELY; the pointer is valid
        /// only until the next StageWords call (ring growth may relocate it). The
        /// span start matches `off`'s 8-byte parity so double writes stay aligned.
        member x.StageWords(off : int, words : int) : nativeint =
            if ringChunks.Count = 0 then ringAddChunk (max ringSteadyWords (Fun.NextPowerOfTwo (words + 1)))
            let mutable start = if (cursor &&& 1) <> (off &&& 1) then cursor + 1 else cursor
            if start + words > ringWords then
                // chunk full: chain a new (doubled) one — NEVER copy out of mapped
                // (write-combined) memory
                ringAddChunk (max (min (ringWords * 2) (256 <<< 20)) (Fun.NextPowerOfTwo (words + 1)))
                start <- if (off &&& 1) = 1 then 1 else 0
            if covOverlaps off (off + words) then
                ops.Add(struct(regions.Count, null))
                lastCut <- regions.Count
                covered.Clear()
            covAdd off (off + words)
            let chunk = ringChunks.Count - 1
            let mutable merged = false
            if regions.Count > lastCut then
                let (struct(c0, r0, a0, w0)) = regions.[regions.Count - 1]
                if c0 = chunk && r0 + w0 = start && a0 + w0 = off then
                    regions.[regions.Count - 1] <- struct(c0, r0, a0, w0 + words)
                    merged <- true
            if not merged then regions.Add(struct(chunk, start, off, words))
            cursor <- start + words
            x.ScheduleTouchIfClean()
            ringPtr + nativeint (start <<< 2)

        /// Write a 4-word per-allocation header (typeId, length, strideBytes, 0).
        member x.WriteHeader(off : int, typeId : int, length : int, strideBytes : int) =
            let p = x.StageWords(off, AllocHeaderWords)
            wi p 0 typeId
            wi p 1 length
            wi p 2 strideBytes
            wi p 3 0

        /// Stage a ONE-SHOT region write (CONSTANT sources — no RegionWriter
        /// subscription): `pack` writes the region's words through the span
        /// pointer NOW; uploaded on the next Compute.
        member x.StageOnce(off : int, size : int, pack : nativeint -> unit) =
            pack (x.StageWords(off, size))

        /// Stage ZEROS for a GPU-written region (derive outputs, chain folds) at
        /// ALLOCATION time. The zeros are placeholders — the derive pass rewrites
        /// the region after the upload (new slots are always derive-dirty) — but
        /// staging them keeps a slot's allocations CONTIGUOUS in ring and arena:
        /// without this every slot punches a hole that breaks region merging, and
        /// a bulk flush degenerates to O(parts) copy regions (MoltenVK executes
        /// each VkBufferCopy as a separate Metal blit command -> GPU hang).
        member x.StageZero(off : int, size : int) =
            let p = x.StageWords(off, size)
            for i in 0 .. size - 1 do wi p i 0

        /// Queue a compaction move-set (oldOff, newOff, contentWords): applied on
        /// the next Compute AFTER everything staged so far and BEFORE anything
        /// staged later (a device-side temp-buffer bounce — never an overlapping
        /// same-buffer copy).
        member x.QueueMoves(moves : (struct(int * int * int))[]) =
            if moves.Length > 0 then
                ops.Add(struct(regions.Count, moves))
                lastCut <- regions.Count
                covered.Clear()

        /// Add a region writer; returns it so it can be removed later.
        member x.Add(src, off, size, pack) : RegionWriter =
            let w = RegionWriter(src, off, size, pack)
            pending.Add w |> ignore
            w
        member x.Remove(w : RegionWriter) =
            pending.Remove w |> ignore
            w.Dispose()
        /// Mark outdated so the next eval flushes pending writes (call in transact).
        member x.Touch() = x.MarkOutdated()
        /// Dependencies evaluated at the TOP of Compute. Every heap allocating from
        /// this (shared) arena adds its membership updater so that slot / region
        /// mutations (incl. newly added writers in `pending`) are applied BEFORE the
        /// flush below — regardless of which bucket aval the render task happens to
        /// pull first. Reading them through the token also makes the arena re-flush
        /// whenever any of the heaps' memberships change (no MarkOutdated / transact
        /// during evaluation needed).
        member val private Dependencies : System.Collections.Generic.List<IAdaptiveValue> = System.Collections.Generic.List() with get
        /// add a membership updater (idempotent by reference)
        member x.AddDependency(d : IAdaptiveValue) =
            if not (x.Dependencies.Contains d) then x.Dependencies.Add d
        override x.Compute(t, rt) =
            let __profT0 = System.Diagnostics.Stopwatch.GetTimestamp()
            x.TouchScheduled.Value <- 0
            for i in 0 .. x.Dependencies.Count - 1 do
                x.Dependencies.[i].GetValueUntyped t |> ignore
            let __uplT0 = System.Diagnostics.Stopwatch.GetTimestamp()
            // pack dirty region writers into the ring — AFTER all updater staging,
            // so a writer re-seated by a same-cycle compaction packs at its NEW
            // offset in a batch after the move-set.
            let dirty = pending.GetAndClear()
            for w in dirty do
                if w.Size > 0 then
                    w.Pack(t, x.StageWords(w.Off, w.Size))
            // GROW first (device-side content-preserving) so copies target full size
            if uint64 capacity * 4UL > x.Size then x.ResizeInPlace(uint64 capacity * 4UL)
            let h = base.Compute(t, rt)
            let mutable __flushed = 0
            let mutable __runs = 0
            let mutable __regions = 0
            let flushBatch (fromIdx : int) (toIdx : int) =
                // regions' chunk index is monotone: emit one copy per chunk sub-range,
                // capped at 16k regions per submission — MoltenVK turns every
                // VkBufferCopy into a separate Metal blit command, so an unbounded
                // region list in ONE command buffer can trip the GPU watchdog.
                let maxRegionsPerCopy = 16384
                let mutable i = fromIdx
                while i < toIdx do
                    let (struct(c, _, _, _)) = regions.[i]
                    let mutable j = i
                    while j < toIdx
                          && j - i < maxRegionsPerCopy
                          && (let (struct(cj, _, _, _)) = regions.[j] in cj = c) do j <- j + 1
                    let (struct(buf, _, p, _)) = ringChunks.[c]
                    __runs <- __runs + 1
                    __regions <- __regions + (j - i)
                    if not (obj.ReferenceEquals(buf, null)) then
                        let arr = Array.zeroCreate<BufferCopyRegion> (j - i)
                        for k in i .. j - 1 do
                            let (struct(_, r, a, w)) = regions.[k]
                            __flushed <- __flushed + w
                            arr.[k - i] <- { SrcOffset = uint64 r * 4UL; DstOffset = uint64 a * 4UL; SizeInBytes = uint64 w * 4UL }
                        runtime.Copy(buf, h, arr)
                    else
                        // no mapping (GL): per-region uploads from the pinned chunk
                        for k in i .. j - 1 do
                            let (struct(_, r, a, w)) = regions.[k]
                            __flushed <- __flushed + w
                            runtime.Upload(p + nativeint (r <<< 2), h, uint64 a * 4UL, uint64 w * 4UL)
                    i <- j
            let applyMoves (moves : (struct(int * int * int))[]) =
                // merge adjacent moves into runs (residents re-alloc front-to-back)
                let runs = System.Collections.Generic.List<struct(int * int * int)>()
                for (struct(o, nw, w)) in moves do
                    if runs.Count > 0 then
                        let (struct(po, pn, pw)) = runs.[runs.Count - 1]
                        if po + pw = o && pn + pw = nw then runs.[runs.Count - 1] <- struct(po, pn, pw + w)
                        else runs.Add(struct(o, nw, w))
                    else runs.Add(struct(o, nw, w))
                let total = runs |> Seq.sumBy (fun (struct(_, _, w)) -> w)
                if total > 0 then
                    use temp = runtime.CreateBuffer(uint64 total * 4UL, BufferUsage.ReadWrite, BufferStorage.Device)
                    let toTemp   = Array.zeroCreate<BufferCopyRegion> runs.Count
                    let fromTemp = Array.zeroCreate<BufferCopyRegion> runs.Count
                    let mutable c = 0UL
                    for i in 0 .. runs.Count - 1 do
                        let (struct(o, nw, w)) = runs.[i]
                        toTemp.[i]   <- { SrcOffset = uint64 o * 4UL; DstOffset = c; SizeInBytes = uint64 w * 4UL }
                        fromTemp.[i] <- { SrcOffset = c; DstOffset = uint64 nw * 4UL; SizeInBytes = uint64 w * 4UL }
                        c <- c + uint64 w * 4UL
                    runtime.Copy(h, temp, toTemp)
                    runtime.Copy(temp, h, fromTemp)
            let mutable segStart = 0
            for (struct(e, moves)) in ops do
                flushBatch segStart e
                segStart <- e
                if not (isNull moves) then applyMoves moves
            flushBatch segStart regions.Count
            // deferred SHRINK last (compaction packed the content below the new size)
            let h =
                if uint64 capacity * 4UL < x.Size then
                    x.ResizeInPlace(uint64 capacity * 4UL)
                    base.Compute(t, rt)
                else h
            // reset the ring: keep nothing allocated past a bulk flush (the next
            // cycle lazily re-creates a steady-state chunk)
            regions.Clear(); ops.Clear(); covered.Clear()
            lastCut <- 0
            cursor <- 0
            if ringChunks.Count > 1 || ringWords > ringSteadyWords then ringFree ()
            // log per BIG upload (each page arena) with running totals; small camera-move
            // re-stages (< 3 MB) are skipped so the totals reflect the geometry upload.
            if __flushed * 4 > 3_000_000 then
                stUploadMs <- stUploadMs + float (System.Diagnostics.Stopwatch.GetTimestamp() - __uplT0) * 1000.0 / float System.Diagnostics.Stopwatch.Frequency
                stUploadBytes <- stUploadBytes + int64 __flushed * 4L
                if EditProf.enabled || Diagnostics then
                    Log.line "[startup] upload batches this flush: %d (%.1f MB, %d regions)" __runs (float __flushed * 4.0 / 1e6) __regions
                    Log.line "[startup] ingest %d parts: %.0f ms | GPU upload (cum) %.1f MB: %.0f ms" stIngestN stIngestMs (float stUploadBytes / 1e6) stUploadMs
            EditProf.addMs "arena:compute" (float (System.Diagnostics.Stopwatch.GetTimestamp() - __profT0) * 1000.0 / float System.Diagnostics.Stopwatch.Frequency)
            h
        override x.Destroy() =
            ringFree ()
            regions.Clear(); ops.Clear(); covered.Clear()
            lastCut <- 0
            base.Destroy()
        override x.InputChangedObject(_, o) =
            match o with
            | :? RegionWriter as w -> pending.Add w |> ignore
            | _ -> ()

    /// Adaptive per-slot visibility gate of an incremental bucket: reads the
    /// member's dynamic IsActive and yields the slot's effective instance
    /// count. Marked (via the source) only when that gate changes, so the
    /// draw-record mirror re-stages exactly the TOGGLED slots — O(toggled),
    /// not O(members with dynamic visibility).
    type internal GateWriter(src : aval<bool>, slot : int, instances : int) =
        inherit AdaptiveObject()
        do (src :> IAdaptiveValue).Acquire()
        let mutable disposed = false
        member _.Slot = slot
        member _.IsDisposed = disposed
        /// the slot's gated-on instance count. MUTABLE: an RO whose Direct
        /// draw-call instance count changes adaptively updates it in place.
        member val Instances = instances with get, set
        /// CLUSTERED buckets: the updater evaluates gate flips (class membership must
        /// change BEFORE any buffer flush); non-clustered buckets leave this null and
        /// keep the draw-mirror path.
        member val OnCluster : System.Action<AdaptiveToken> = null with get, set
        /// evaluate the gate; `write slot count` runs iff re-evaluation was needed
        member x.Update(token : AdaptiveToken, write : int -> int -> unit) =
            x.EvaluateIfNeeded token () (fun token ->
                write slot (if src.GetValue token then x.Instances else 0))
        member x.Dispose() =
            disposed <- true
            (src :> IAdaptiveValue).Release()
            (src :> IAdaptiveValue).Outputs.Remove x |> ignore
            x.Outputs.Clear()

    /// Generic per-slot adaptive watcher for values the heap used to snapshot at
    /// add time (geometry buffer avals, draw-call shape, pick ids, model-stack
    /// structure). Marked (via the source) only when ITS value changes; the
    /// UPDATER collects marked writers into a dirty set (InputChangedObject, same
    /// idiom as KeyWatcher / cluster GateWriters) and invokes `OnChange` — which
    /// applies the change O(affected) inside the membership update, BEFORE any
    /// arena / mirror flush serializes state.
    type internal DynWriter(src : IAdaptiveValue) =
        inherit AdaptiveObject()
        do src.Acquire()
        member val IsDisposed = false with get, set
        /// set when the ADD already consumed the current value (geometry bytes
        /// staged by the allocation itself): the first apply — which only runs
        /// because a fresh AdaptiveObject starts OutOfDate — skips its work and
        /// just establishes the subscription. Without it, ingest would stage
        /// every dynamic-source geometry TWICE.
        member val Fresh = false with get, set
        /// bucket-provided applier; must route through `Update` so the writer
        /// (re)subscribes to its source through the caller's token.
        member val OnChange : System.Action<AdaptiveToken> = null with get, set
        /// evaluate the source; `apply` runs iff re-evaluation was needed
        member x.Update(token : AdaptiveToken, apply : AdaptiveToken -> obj -> unit) =
            x.EvaluateIfNeeded token () (fun tok -> apply tok (src.GetValueUntyped tok))
        member x.Dispose() =
            x.IsDisposed <- true
            src.Release()
            src.Outputs.Remove x |> ignore
            x.Outputs.Clear()

    /// how a bucket receives "your slot's geometry allocation moved / changed
    /// element count" from a (page-shared, possibly cross-bucket) dynamic
    /// geometry entry. `cell` is the slot's header cell holding the allocation
    /// ref; `isIndex` routes the vertex-count consequences (vcCell + draw
    /// record + cluster reclassification).
    type internal IGeomSink =
        abstract member GeomMoved : slot : int * cell : int * newRef : int * newCount : int * isIndex : bool -> unit

    /// Per-RO watcher over exactly the avals its bucket key reads (created ONLY for
    /// ROs whose key is not all-constant — constant keys are interned once and never
    /// re-evaluated). The updater evaluates it through its own token, making the
    /// watcher an input of the updater: when a watched state aval flips, the
    /// updater's InputChangedObject collects the watcher into a dirty set and ONLY
    /// the affected ROs (all sharing that aval) are re-keyed and moved between
    /// buckets — the rest of the heap is untouched, there is NO global regroup.
    /// Same idiom as GateWriter/SlotTexWriter, one level up.
    type internal KeyWatcher(ro : RenderObject) =
        inherit AdaptiveObject()
        let mutable current : obj = null    // the interned key token (valid after the first Update)
        member _.Ro = ro
        member val IsDisposed = false with get, set
        /// (re)compute the RO's interned key token iff outdated; `compute` reads the
        /// key's state avals through the WATCHER's token, (re)subscribing it.
        member x.Update(token : AdaptiveToken, compute : AdaptiveToken -> obj) : obj =
            x.EvaluateIfNeeded token current (fun tok ->
                current <- compute tok
                current)
        member x.Dispose() =
            x.IsDisposed <- true
            x.Outputs.Clear()

    /// Stable-identity GPU mirror of one of a bucket's per-slot tables (draw
    /// records / headers / per-instance slot attributes). The bucket's delta
    /// pass mutates the CPU array and records dirty ranges; Compute — pulled
    /// by the consuming aval — first evaluates `Dependency` (the membership
    /// updater, whose evaluation performs those mutations) and then runs the
    /// bucket-provided `Flush`, which uploads ONLY the dirty sub-ranges via
    /// Write. The backend buffer instance changes ONLY on growth, so the
    /// resource layer recognizes the unchanged handle and re-prepares /
    /// re-uploads nothing on content-only versions (the same contract the
    /// geometry buffers and HeapArena already rely on). Gate writers (dynamic
    /// per-slot IsActive) mark this buffer directly and are collected like
    /// HeapArena's region writers — no transact during evaluation anywhere.
    type internal MirrorBuffer(runtime : IBufferRuntime, initialBytes : int, usage : BufferUsage) =
        // DEVICE-local like the arena: the headers mirror is read PER VERTEX and the
        // draw records by the command processor. BufferStorage.Host would land in
        // BAR memory only where VMA finds device-local|host-visible space — on GPUs
        // without (enough) BAR it silently falls back to system memory and every
        // shader read crosses PCIe (the prerelease0025 arena bug). Device is
        // predictable everywhere; the dirty-sub-range Write path stages uploads
        // exactly like HeapArena already does.
        inherit AdaptiveBuffer(runtime, uint64 (max 1 initialBytes), usage, BufferStorage.Device)
        let dirtyGates = LockedSet<GateWriter>()
        member val Dependency : IAdaptiveValue option = None with get, set
        member val Flush : AdaptiveToken -> System.Collections.Generic.HashSet<GateWriter> -> unit = (fun _ _ -> ()) with get, set
        /// register a NEW gate writer for evaluation on the next flush
        member _.MarkGate(w : GateWriter) = dirtyGates.Add w |> ignore
        /// shadows AdaptiveBuffer.Write to count per-buffer write calls under HEAP_EDIT_PROF
        member x.Write(data : 'a[], offsetBytes : uint64, start : int, count : int) =
            EditProf.count (sprintf "wr:%s" (if isNull x.Name then "?" else x.Name)) count
            (x :> AdaptiveBuffer).Write(data, offsetBytes, start, count)
        override x.Compute(t, rt) =
            match x.Dependency with
            | Some d -> d.GetValueUntyped t |> ignore
            | None -> ()
            EditProf.time (sprintf "flush:%s" (if isNull x.Name then "?" else x.Name)) (fun () ->
                x.Flush t (dirtyGates.GetAndClear()))
            base.Compute(t, rt)
        override x.InputChangedObject(_, o) =
            match o with
            | :? GateWriter as w -> dirtyGates.Add w |> ignore
            | _ -> ()

    /// One allocation of a HeapSpace: [Offset, Offset+Size) in caller units.
    /// Size is EXACTLY the requested size — call sites detect 8-byte-aligned
    /// content via `block.Size > entry.Size` (the +1 over-allocation), so the
    /// allocator must never hand out a larger block than asked (splits are exact).
    [<AllowNullLiteral>]
    type internal HeapBlock(offset : int, size : int) =
        member val Offset = offset with get, set
        member _.Size = size
        member val IsFree = false with get, set

    /// Logical address space for the heap reclamation sites (units are caller-
    /// defined: floats, vertices, indices, instance slots). SEGREGATED-FIT +
    /// BUMP-TAIL allocator: freed blocks live in quarter-pow2 size-class stacks;
    /// Alloc takes the first block from the smallest class whose members are
    /// guaranteed >= size (splitting the exact remainder back into its class)
    /// and BUMPS the virgin tail on a miss. Both paths are O(1) amortized —
    /// bulk ingest (no frees yet) is a pure bump, churn reuses freed blocks —
    /// and the bump pointer is the MISS PATH of the one allocator that exists
    /// from page creation: no ingest/edit mode split, no first-edit cold start.
    /// There is NO coalescing; fragmentation is bounded by the threshold-
    /// triggered page compaction (Reset + tight re-alloc), which the Live /
    /// Extent counters drive. A block freed at the very END retracts the tail.
    type internal HeapSpace() =
        // quarter-pow2 size classes (4 per octave): class of `s`, rounding DOWN —
        // blocks stored in class c all have size >= classMin.[c]
        static let classOfDown (s : int) =
            let mutable v = s
            let mutable k = 0
            if v >= 0x10000 then k <- 16; v <- v >>> 16
            if v >= 0x100 then k <- k + 8; v <- v >>> 8
            if v >= 0x10 then k <- k + 4; v <- v >>> 4
            if v >= 4 then k <- k + 2; v <- v >>> 2
            if v >= 2 then k <- k + 1
            4 * k + ((s >>> (max 0 (k - 2))) &&& 3)
        // exact minimum size of class c (Int32.MaxValue for unpopulated classes)
        static let classMin =
            let arr = Array.create 128 System.Int32.MaxValue
            arr.[1] <- 1
            arr.[6] <- 2
            arr.[7] <- 3
            for k in 2 .. 30 do
                for q in 0 .. 3 do
                    let c = 4 * k + q
                    if c < 128 then arr.[c] <- (4 + q) <<< (k - 2)
            arr
        static let maxClass = 123                                  // 4*30+3

        let stacks : System.Collections.Generic.Stack<HeapBlock>[] = Array.zeroCreate 128
        // EXACT-SIZE free index: same-size churn (the dominant edit pattern) must
        // reuse freed blocks even when the size is not a quarter-pow2 class minimum.
        // The class search alone starts at c0+1 for such sizes — class c0 members
        // are only guaranteed >= classMin[c0], not >= size — so an equal-size freed
        // block was INVISIBLE and churn bump-grew the arena (geom-churn: 42->101KB
        // flat-violation). Every free block lives in BOTH indices; taking it through
        // one leaves a stale entry in the other, invalidated lazily via IsFree.
        let exact = System.Collections.Generic.Dictionary<int, System.Collections.Generic.Stack<HeapBlock>>()
        let mutable freeCount = 0
        let mutable tail = 0
        let mutable live = 0

        let pushFree (b : HeapBlock) =
            b.IsFree <- true
            let c = classOfDown b.Size
            let st =
                match stacks.[c] with
                | null -> let st = System.Collections.Generic.Stack<HeapBlock>() in stacks.[c] <- st; st
                | st -> st
            st.Push b
            let ex =
                match exact.TryGetValue b.Size with
                | true, ex -> ex
                | _ -> let ex = System.Collections.Generic.Stack<HeapBlock>() in exact.[b.Size] <- ex; ex
            ex.Push b
            freeCount <- freeCount + 1

        /// high-water end of the allocated address space (in units)
        member _.Extent = tail
        /// units referenced by live allocations
        member _.Live = live
        /// reclaimable units below Extent (the waste)
        member _.Waste = tail - live

        member _.Alloc(size : int) : HeapBlock =
            let size = max 1 size
            live <- live + size
            let mutable found : HeapBlock = null
            if freeCount > 0 then
                // exact-size reuse first (skip entries already taken via the class path)
                match exact.TryGetValue size with
                | true, ex ->
                    while isNull found && ex.Count > 0 do
                        let b = ex.Pop()
                        if b.IsFree then found <- b
                | _ -> ()
                if isNull found then
                    // smallest class whose members are all >= size
                    let c0 = classOfDown size
                    let mutable c = if classMin.[c0] = size then c0 else c0 + 1
                    while isNull found && c <= maxClass do
                        let st = stacks.[c]
                        if not (isNull st) && st.Count > 0 then
                            // skip entries already taken via the exact path
                            let b = st.Pop()
                            if b.IsFree then found <- b
                        else c <- c + 1
            if isNull found then
                // miss -> bump the virgin tail
                let b = HeapBlock(tail, size)
                tail <- tail + size
                b
            else
                freeCount <- freeCount - 1
                // ALWAYS clear IsFree — it also invalidates the block's stale twin
                // entry in the other index (split case: the popped block is dead,
                // its space lives on in the returned block + remainder)
                found.IsFree <- false
                if found.Size = size then
                    found
                else
                    // exact split: remainder returns to its class
                    pushFree (HeapBlock(found.Offset + size, found.Size - size))
                    HeapBlock(found.Offset, size)

        member _.Free(b : HeapBlock) =
            if not (isNull b) && not b.IsFree && b.Size > 0 then
                live <- live - b.Size
                if b.Offset + b.Size = tail then
                    // tail free: retract the bump pointer (blocks are disjoint, so
                    // every stashed free block stays strictly below the new tail)
                    b.IsFree <- true
                    tail <- b.Offset
                else
                    pushFree b

        /// drop everything and start a fresh address space (used by compaction,
        /// which re-allocs the live entries tightly right afterwards)
        member _.Reset() =
            for st in stacks do
                if not (isNull st) then st.Clear()
            exact.Clear()
            freeCount <- 0
            tail <- 0
            live <- 0

    /// Mutable refcounted arena region (deduped by source-aval identity).
    /// Offset is re-seated by arena compaction. Block is the region's float
    /// range in the arena HeapSpace (re-allocated on compaction). Size is the
    /// TOTAL allocation size (incl. header words); HeaderWords is 0 for raw
    /// uniform-field regions and AllocHeaderWords for singleton-attribute
    /// allocations (whose writer packs at Offset + HeaderWords). Writer is
    /// null for CONSTANT sources (packed once via HeapArena.StageOnce — no
    /// subscription, nothing to re-evaluate or dispose).
    type internal RegionEntry =
        { mutable Offset : int; Size : int; Writer : RegionWriter; mutable RefCount : int
          mutable Block : HeapBlock; HeaderWords : int }

    /// Refcounted STATIC allocation in the bucket arena (one vertex attribute's
    /// bytes, or one index range — written once, deduped by VALUE-level source
    /// + byte offset + format typeId). Ref is the allocation's HEADER word offset
    /// (data at Ref + AllocHeaderWords); re-seated by arena compaction. Count
    /// is the element count (the per-slot draw record's FaceVertexCount for
    /// index allocations).
    type internal StaticEntry =
        { mutable Ref : int; mutable SizeF : int; mutable Count : int; mutable RefCount : int
          mutable Block : HeapBlock
          /// DYNAMIC source (non-constant buffer aval): the adaptive re-stage /
          /// realloc writer (null for constant sources — written once, no
          /// subscription) and the referencing (sink, slot, headerCell) set the
          /// realloc path re-bakes — O(sharers) = O(affected).
          mutable Writer : DynWriter
          mutable DynRefs : System.Collections.Generic.HashSet<struct(IGeomSink * int * int)> }

    /// how a slot references one of its attribute allocations (for release +
    /// compaction header rewrite)
    [<RequireQualifiedAccess>]
    type internal AttrKey =
        /// static host buffer: keyed by (value-level source — underlying array
        /// for constant ArrayBuffer avals, aval identity otherwise — byte
        /// offset, storage typeId)
        | Static of struct(obj * int * int)
        /// SingleValueBuffer attribute: keyed by the inner value aval
        | Single of IAdaptiveValue

    /// Per-member bookkeeping of an incremental bucket: the draw-record slot,
    /// the arena regions it references, its visibility gate, its (structural)
    /// instance count, the identity key of its packed geometry (for refcounted
    /// range reclamation) and — on the MoltenVK slot-attribute path — the offset
    /// of its per-instance range in the slot-attribute buffer (re-seated by
    /// compaction; InstBlock is the backing HeapSpace block).
    /// One typed-assignment partition ("JIT tier"): the slots whose per-field
    /// source-tid vector equals Key render through a pipeline specialized with
    /// TidMap. Id 0 = the DYNAMIC partition (interpreter tier): unspecialized
    /// pipeline, staging area for fresh slots and permanent home of the long
    /// tail (population < materialization threshold).
    type internal HeapPartition =
        { Key : int64
          mutable Id : int
          TidMap : Map<string, int>
          mutable Count : int
          Slots : System.Collections.Generic.List<int>
          mutable Materialized : bool }

    /// A DEDUPED derived-output region: slots whose recipe (plan index) and
    /// canonical constituent sources match share ONE output region (refcounted
    /// via Members); exactly one member (Owner) computes it in the derive pass
    /// (per-slot ownership-mask header cell). Non-dedupable outputs (chain-folded
    /// Model constituents are per slot) get a private single-member share.
    type internal DerivedShare =
        { Key : struct(int * int * obj * obj * obj)   // page, planIdx, canonical constituent avals
          Page : int
          Dedup : bool
          mutable Block : HeapBlock
          mutable Offset : int
          Members : System.Collections.Generic.HashSet<int>
          mutable Owner : int
          /// position in the bucket's per-output dispatch list (HeapShareRecs)
          mutable ListIdx : int }

    type internal HeapSlot =
        { Slot : int; RegionKeys : IAdaptiveValue[]; Active : aval<bool>
          /// which storage page this slot's group lives on (all its regions are on it);
          /// the slot renders in that page's sub-draw and frees from that page on remove.
          mutable Page : int
          mutable Instances : int; mutable InstOffset : int
          mutable InstBlock : HeapBlock
          /// per consumed attribute (host buckets; empty for bindless)
          AttrKeys : AttrKey[]
          /// the slot's index allocation key (value-level source, byte offset,
          /// index typeId)
          IdxKey : struct(obj * int * int)
          /// derived-uniform compute bookkeeping: uploaded constituent regions to
          /// release (base aval + inverse flag), per-slot output region blocks and
          /// chain-folded Model constituent blocks to free on remove.
          ConstKeys : struct(IAdaptiveValue * bool)[]
          Shares : DerivedShare[]
          FoldBlocks : HeapBlock[] }

    /// Immutable per-RO facts (STRUCTURE only — surface, geometry layout, uniform
    /// presence; never aval VALUES). Cached per RO in a ConditionalWeakTable so a
    /// membership diff doesn't re-derive them (isHeapable + layout sig over 20k ROs
    /// per change would dominate the frame). ConstToken is the interned bucket key
    /// when ALL the RO's pipeline-state avals are constant (the common case), null
    /// when any is dynamic (then the key is re-read through the token every run).
    /// Bindless (GPU-resident geometry -> vertex-pull) and Instanced
    /// (InstanceCount > 1) are PART of the bucket key (folded into Layout), so a
    /// bucket's geometry strategy and slot routing are fixed at creation.
    type internal RoFacts =
        { Heapable : bool; Layout : string; ConstToken : obj
          Bindless : bool; Instanced : bool
          /// GPU trafo-chain: the RO exposes an unfolded "ModelTrafoStack" and the
          /// effect consumes ModelTrafo, so its ModelTrafo is composed on the GPU
          /// from the deduped link arena rather than packed as an arena region.
          /// Part of the bucket key (folded into Layout).
          Chain : bool
          /// per-draw heap-field names (sorted) + name -> field index, INTERNED
          /// per distinct set (ROs sharing a set share one array/map instance).
          /// Explicit-names calls: the caller's set, same for every heapable RO.
          /// Auto-detect calls: the DETECTED set (effect-consumed ∩ RO-supplied ∩
          /// packable); folded into Layout, so it is part of the bucket key — the
          /// rewritten shader bakes the field layout, so ROs with different field
          /// sets must land in different buckets.
          Fields : string[]; FieldMap : Map<string, int> }

    /// does this blend mode consume the constant blend color? (decides whether
    /// BlendState.ConstantColor participates in the bucket key at all)
    let private usesBlendConstant (m : BlendMode) =
        let isConst (f : BlendFactor) =
            f = BlendFactor.ConstantColor || f = BlendFactor.InvConstantColor ||
            f = BlendFactor.ConstantAlpha || f = BlendFactor.InvConstantAlpha
        m.Enabled &&
        (isConst m.SourceColorFactor || isConst m.SourceAlphaFactor ||
         isConst m.DestinationColorFactor || isConst m.DestinationAlphaFactor)

    /// The COMPLETE bucket key: effect + geometry layout + topology + render pass +
    /// the resolved VALUES of every piece of per-RO pipeline state that can affect
    /// the render (read token-reactively in `modeKey`, so a value change MOVES the
    /// RO to the right bucket — no state ever merges on an arbitrary member).
    ///
    /// State that CANNOT affect the framebuffer signature is structurally ABSENT:
    ///   * blend state is projected onto the color attachments the effect actually
    ///     writes (signature ∩ effect outputs), resolved to the EFFECTIVE per-
    ///     attachment (mode, write mask) — per-attachment override or global
    ///     fallback, so equivalent expressions collapse to the same bucket,
    ///   * the blend constant is keyed only when an effective mode consumes it,
    ///   * depth / stencil state vanish when the signature has no such attachment.
    ///
    /// Equality/hash are hand-written: the generic structural comparer on a record
    /// this size costs µs per intern lookup (same reason the previous tuple key
    /// carried a hand-rolled comparer).
    [<CustomEquality; NoComparison>]
    type internal BucketKey =
        { EffectId : string
          Topology : IndexedGeometryMode
          /// geometry strategy + instanced-ness + chain + per-draw field set (factsOf)
          Layout : string
          Pass : RenderPass
          Cull : CullMode
          FrontFacing : WindingOrder
          Fill : FillMode
          Multisample : bool
          ConservativeRaster : bool
          IsTransparent : bool
          /// effective (mode, write mask) per written signature color attachment, sorted by name
          Blend : (string * BlendMode * ColorMask)[]
          /// Some iff a mode in Blend consumes the constant color/alpha
          BlendConstant : C4f option
          /// (test, bias, writeMask, clamp); None when the signature has no depth
          Depth : (DepthTest * DepthBias * bool * bool) option
          /// (modeFront, maskFront, modeBack, maskBack); None when the signature has no stencil
          Stencil : (StencilMode * StencilMask * StencilMode * StencilMask) option
          /// per-RO viewport/scissor overrides, resolved (None = task-provided)
          Viewport : Box2i option
          Scissor : Box2i option }

        override x.GetHashCode() =
            let mutable h = x.EffectId.GetHashCode()
            h <- h * 31 + x.Layout.GetHashCode()
            h <- h * 31 + int x.Topology
            h <- h * 31 + x.Pass.GetHashCode()
            h <- h * 31 + int x.Cull
            h <- h * 31 + int x.FrontFacing
            h <- h * 31 + int x.Fill
            for (n, m, mask) in x.Blend do
                h <- h * 31 + n.GetHashCode()
                h <- h * 31 + m.GetHashCode()
                h <- h * 31 + int mask
            h <- h * 31 + (match x.BlendConstant with Some c -> c.GetHashCode() | None -> -1)
            h <- h * 31 + (match x.Depth with
                           | Some (t, b, w, c) ->
                               (int t) ^^^ b.GetHashCode() ^^^ (if w then 0x10000 else 0) ^^^ (if c then 0x20000 else 0)
                           | None -> -1)
            h <- h * 31 + (match x.Stencil with Some s -> s.GetHashCode() | None -> -1)
            h <- h * 31 + (match x.Viewport with Some b -> b.GetHashCode() | None -> -1)
            h <- h * 31 + (match x.Scissor with Some b -> b.GetHashCode() | None -> -1)
            h * 8 + ((if x.Multisample then 1 else 0)
                     ||| (if x.ConservativeRaster then 2 else 0)
                     ||| (if x.IsTransparent then 4 else 0))

        override x.Equals(o : obj) =
            match o with
            | :? BucketKey as y ->
                x.Topology = y.Topology
                && x.Cull = y.Cull && x.FrontFacing = y.FrontFacing && x.Fill = y.Fill
                && x.Multisample = y.Multisample && x.ConservativeRaster = y.ConservativeRaster
                && x.IsTransparent = y.IsTransparent
                && x.Pass = y.Pass
                && x.EffectId = y.EffectId && x.Layout = y.Layout
                && x.Blend.Length = y.Blend.Length
                && Array.forall2
                    (fun (n1 : string, m1 : BlendMode, k1 : ColorMask) (n2, m2, k2) ->
                        n1 = n2 && m1.Equals m2 && k1 = k2)
                    x.Blend y.Blend
                // box-free: structural `=` on option-of-struct(-tuple) boxes every
                // member per comparison (StencilMode/StencilMask/C4f/Box2i)
                && (match x.BlendConstant, y.BlendConstant with
                    | Some a, Some b -> a.Equals b
                    | None, None -> true | _ -> false)
                && (match x.Depth, y.Depth with
                    | Some (t1, b1, w1, c1), Some (t2, b2, w2, c2) -> t1 = t2 && b1.Equals b2 && w1 = w2 && c1 = c2
                    | None, None -> true | _ -> false)
                && (match x.Stencil, y.Stencil with
                    | Some (mf1, kf1, mb1, kb1), Some (mf2, kf2, mb2, kb2) ->
                        mf1.Equals mf2 && kf1.Equals kf2 && mb1.Equals mb2 && kb1.Equals kb2
                    | None, None -> true | _ -> false)
                && (match x.Viewport, y.Viewport with
                    | Some a, Some b -> a.Equals b
                    | None, None -> true | _ -> false)
                && (match x.Scissor, y.Scissor with
                    | Some a, Some b -> a.Equals b
                    | None, None -> true | _ -> false)
            | _ -> false

    /// an input RO may ALREADY be instanced (instanceCount > 1); preserved per-slot.
    /// Forced here for the INITIAL value; a non-constant Direct call list gets a
    /// per-slot DynWriter at add time that applies later count changes in place.
    let private instanceCountOf (ro : RenderObject) =
        match ro.DrawCalls with
        | DrawCalls.Direct calls ->
            match AVal.force calls with
            | [||] -> 1
            | arr -> max 1 arr.[0].InstanceCount
        | _ -> 1

    /// a NON-indexed slot's vertex count: the RO's Direct draw call (classify
    /// requires Direct, single-call, zero offsets for non-indexed ROs).
    /// Forced here for the INITIAL value; a non-constant call list gets a
    /// per-slot DynWriter at add time (vcCell + record + cluster reclass).
    let private faceVertexCountOf (ro : RenderObject) =
        match ro.DrawCalls with
        | DrawCalls.Direct calls ->
            match AVal.force calls with
            | [||] -> 0
            | arr -> arr.[0].FaceVertexCount
        | _ -> 0

    /// attribute typeIds (header word0): the DECODER branches on these at FETCH
    /// time and converts to the shader's input type, so the SOURCE element type
    /// is per ALLOCATION, not part of the bucket key — one bucket freely mixes
    /// e.g. C4b singleton colors with C4f buffers next to default V3f boxes.
    /// Encoding: 10+N = f32 xN (float32/V2f/V3f/V4f, C3f/C4f), 20+N = i32 xN,
    /// 30+N = f64 xN (float/V2d/V3d/V4d, C3d/C4d — bit-decoded, no
    /// shaderFloat64 dependency), 40 = normalized C4b. The per-element stride
    /// is implied by the typeId (host allocations are tightly packed).
    /// (INDEX allocations use the separate ids 1/2 above.)
    let private attrTypeId (t : System.Type) : int voption =
        if   t = typeof<float32> then ValueSome 11
        elif t = typeof<V2f>     then ValueSome 12
        elif t = typeof<V3f> || t = typeof<C3f> then ValueSome 13
        elif t = typeof<V4f> || t = typeof<C4f> then ValueSome 14
        elif t = typeof<int>     then ValueSome 21
        elif t = typeof<V2i>     then ValueSome 22
        elif t = typeof<V3i>     then ValueSome 23
        elif t = typeof<V4i>     then ValueSome 24
        elif t = typeof<float>   then ValueSome 31
        elif t = typeof<V2d>     then ValueSome 32
        elif t = typeof<V3d> || t = typeof<C3d> then ValueSome 33
        elif t = typeof<V4d> || t = typeof<C4d> then ValueSome 34
        elif t = typeof<C4b>     then ValueSome 40
        // f32 matrix attributes — tight row-major floats, decoded row-wise.
        elif t = typeof<M22f>    then ValueSome 50
        elif t = typeof<M33f>    then ValueSome 52
        elif t = typeof<M44f>    then ValueSome 54
        else ValueNone

    /// decode the vertex index for `gl_VertexIndex = v` from the index
    /// allocation whose HEADER lives at arena word offset `r`: u16 elements
    /// (typeId 2) unpack two-per-word, anything else reads a whole word.
    /// NON-indexed slots carry the sentinel ref -1 (no index allocation):
    /// the vertex index passes through unchanged. The branch is coherent per
    /// draw — all vertices of a slot read the same header cell.
    [<ReflectedDefinition>]
    let private decodeHeapIndex (tidSpec : int) (r : int) (v : int) : int =
        if r < 0 then v
        else
            // tidSpec: pipeline-time index typeId (0 -> read the header)
            let t = if tidSpec <> 0 then tidSpec else uniform.HeapDataI.[r]
            if t = 2 then
                (uniform.HeapDataI.[r + 4 + (v >>> 1)] >>> ((v &&& 1) <<< 4)) &&& 0xFFFF
            else
                uniform.HeapDataI.[r + 4 + v]

    /// reconstruct ONE f32 from a little-endian f64 at int-view word offset `p`
    /// (lo word, hi word) — pure bit manipulation, NO shaderFloat64 dependency:
    /// rebias the exponent (1023 -> 127), keep the top 23 mantissa bits with
    /// guard-bit rounding. Zero/denormal/underflow -> 0, overflow/NaN -> ±inf.
    [<ReflectedDefinition>]
    let private decodeHeapF64 (p : int) : float32 =
        let lo = uniform.HeapDataI.[p]
        let hi = uniform.HeapDataI.[p + 1]
        let e = ((hi >>> 20) &&& 0x7FF) - 896
        let s = (hi >>> 31) <<< 31
        // branchless: compute the normal case, select the two edge cases
        let m = ((hi &&& 0xFFFFF) <<< 3) ||| ((lo >>> 29) &&& 0x7)
        let norm = Fun.FloatFromBits((s ||| (e <<< 23) ||| m) + ((lo >>> 28) &&& 1))
        if e >= 255 then Fun.FloatFromBits(s ||| 0x7F800000) elif e <= 0 then 0.0f else norm

    /// storage-decoded attribute fetch (wombat's loadAttributeByRef,
    /// generalized): BRANCH on the allocation header's typeId at fetch time and
    /// CONVERT the source element to a float4 — widen with (0,0,0,1) fill
    /// (fixed-function parity), normalize C4b (/255), cast int sources,
    /// bit-decode f64 sources. Narrowing happens at the call site (swizzle).
    /// The element index wraps via `vid % length` (header word1), so length-1
    /// singleton allocations broadcast through the SAME fetch; the per-element
    /// stride is implied by the typeId (allocations are tight), which
    /// singletons never misaddress (element 0). The branch is COHERENT per
    /// draw — all vertices of a draw read the same header.
    [<ReflectedDefinition>]
    let private decodeHeapV4f (tidSpec : int) (r : int) (v : int) : V4f =
        // tidSpec is a SPECIALIZATION CONSTANT (per-attribute source typeId,
        // 0 = unknown): a concrete value folds this whole ladder to ONE typed
        // arm at pipeline compile and the header tid is never loaded; 0 keeps
        // today's runtime ladder, byte-identical.
        // bits 6-7 of tidSpec are the pipeline-time EXTENT class: 2 = SINGLETON
        // folds e = 0, 1 = FULL folds e = vid — both remove the header LENGTH
        // load, so the data fetch no longer waits on a header round; 0 keeps
        // the runtime min-against-length clamp (and tidSpec = 0 additionally
        // loads the tid — the fully dynamic path, byte-identical to before).
        let tid = if tidSpec <> 0 then tidSpec &&& 63 else uniform.HeapDataI.[r]
        // `min` handles the length-1 singleton broadcast (indices are always
        // < length otherwise) in ONE instruction — no modulo, no select chain
        let e =
            if tidSpec >= 128 then 0
            elif tidSpec >= 64 then v
            else min v (uniform.HeapDataI.[r + 1] - 1)
        if tid = 13 then                                            // f32 x3 (V3f/C3f) — HOT
            let o = r + 4 + e * 3
            V4f(uniform.HeapData.[o], uniform.HeapData.[o + 1], uniform.HeapData.[o + 2], 1.0f)
        elif tid = 14 then                                          // f32 x4 (V4f/C4f) — HOT
            let o = r + 4 + e * 4
            V4f(uniform.HeapData.[o], uniform.HeapData.[o + 1], uniform.HeapData.[o + 2], uniform.HeapData.[o + 3])
        elif tid = 40 then                                          // normalized C4b (BGRA memory layout) — HOT
            let w = uniform.HeapDataI.[r + 4 + e]
            V4f(float32 ((w >>> 16) &&& 0xFF), float32 ((w >>> 8) &&& 0xFF), float32 (w &&& 0xFF), float32 ((w >>> 24) &&& 0xFF)) / 255.0f
        else
            // ONE generic arm for every remaining source (f32 x1/x2, i32 x1..x4,
            // f64 x1..x4): class/component-count arithmetic + predicated loads.
            // The old per-type arms never RAN for hot content, but their inlined
            // footprint (esp. 10 copies of the f64 bit-decode) cost measurable
            // register pressure — collapsed here, the f64 tree inlines 4x total.
            let cls = tid / 10                                      // 1 = f32, 2 = i32, 3 = f64
            let comps = tid - cls * 10
            let o = r + 4 + e * comps * (if cls = 3 then 2 else 1)
            let c0 =
                if cls = 3 then decodeHeapF64 o
                elif cls = 2 then float32 uniform.HeapDataI.[o]
                else uniform.HeapData.[o]
            let c1 =
                if comps < 2 then 0.0f
                elif cls = 3 then decodeHeapF64 (o + 2)
                elif cls = 2 then float32 uniform.HeapDataI.[o + 1]
                else uniform.HeapData.[o + 1]
            let c2 =
                if comps < 3 then 0.0f
                elif cls = 3 then decodeHeapF64 (o + 4)
                elif cls = 2 then float32 uniform.HeapDataI.[o + 2]
                else uniform.HeapData.[o + 2]
            let c3 =
                if comps < 4 then 1.0f
                elif cls = 3 then decodeHeapF64 (o + 6)
                elif cls = 2 then float32 uniform.HeapDataI.[o + 3]
                else uniform.HeapData.[o + 3]
            V4f(c0, c1, c2, c3)

    /// int-target twin of decodeHeapV4f: i32 sources pass through, f32/f64
    /// sources truncate (well-defined casts), C4b unpacks to raw 0..255 ints.
    [<ReflectedDefinition>]
    let private decodeHeapV4i (tidSpec : int) (r : int) (v : int) : V4i =
        let tid = if tidSpec <> 0 then tidSpec &&& 63 else uniform.HeapDataI.[r]
        let e =
            if tidSpec >= 128 then 0
            elif tidSpec >= 64 then v
            else min v (uniform.HeapDataI.[r + 1] - 1)
        if tid = 23 then                                            // i32 x3 — HOT
            let o = r + 4 + e * 3
            V4i(uniform.HeapDataI.[o], uniform.HeapDataI.[o + 1], uniform.HeapDataI.[o + 2], 1)
        elif tid = 24 then                                          // i32 x4 — HOT
            let o = r + 4 + e * 4
            V4i(uniform.HeapDataI.[o], uniform.HeapDataI.[o + 1], uniform.HeapDataI.[o + 2], uniform.HeapDataI.[o + 3])
        elif tid = 40 then                                          // C4b (BGRA memory layout) -> raw 0..255
            let w = uniform.HeapDataI.[r + 4 + e]
            V4i((w >>> 16) &&& 0xFF, (w >>> 8) &&& 0xFF, w &&& 0xFF, (w >>> 24) &&& 0xFF)
        else
            // ONE generic arm: i32 x1/x2, f32 x1..x4 (cast), f64 x1..x4 (cast)
            let cls = tid / 10
            let comps = tid - cls * 10
            let o = r + 4 + e * comps * (if cls = 3 then 2 else 1)
            let c0 =
                if cls = 3 then int (decodeHeapF64 o)
                elif cls = 1 then int uniform.HeapData.[o]
                else uniform.HeapDataI.[o]
            let c1 =
                if comps < 2 then 0
                elif cls = 3 then int (decodeHeapF64 (o + 2))
                elif cls = 1 then int uniform.HeapData.[o + 1]
                else uniform.HeapDataI.[o + 1]
            let c2 =
                if comps < 3 then 0
                elif cls = 3 then int (decodeHeapF64 (o + 4))
                elif cls = 1 then int uniform.HeapData.[o + 2]
                else uniform.HeapDataI.[o + 2]
            let c3 =
                if comps < 4 then 1
                elif cls = 3 then int (decodeHeapF64 (o + 6))
                elif cls = 1 then int uniform.HeapData.[o + 3]
                else uniform.HeapDataI.[o + 3]
            V4i(c0, c1, c2, c3)


    // f32 matrix attribute decoders: the matrix is stored tight (rows*cols floats
    // per element, row-major), so a row-wise read reconstructs it. Source==target
    // matrix shape (a matrix attribute isn't widened/cast across sizes).
    [<ReflectedDefinition>]
    let private decodeHeapM22f (r : int) (v : int) : M22f =
        let o = r + 4 + (min v (uniform.HeapDataI.[r + 1] - 1)) * 4
        M22f(uniform.HeapData.[o+0], uniform.HeapData.[o+1],
             uniform.HeapData.[o+2], uniform.HeapData.[o+3])
    [<ReflectedDefinition>]
    let private decodeHeapM33f (r : int) (v : int) : M33f =
        let o = r + 4 + (min v (uniform.HeapDataI.[r + 1] - 1)) * 9
        M33f(uniform.HeapData.[o+0], uniform.HeapData.[o+1], uniform.HeapData.[o+2],
             uniform.HeapData.[o+3], uniform.HeapData.[o+4], uniform.HeapData.[o+5],
             uniform.HeapData.[o+6], uniform.HeapData.[o+7], uniform.HeapData.[o+8])
    [<ReflectedDefinition>]
    let private decodeHeapM44f (r : int) (v : int) : M44f =
        let o = r + 4 + (min v (uniform.HeapDataI.[r + 1] - 1)) * 16
        M44f(uniform.HeapData.[o+0],  uniform.HeapData.[o+1],  uniform.HeapData.[o+2],  uniform.HeapData.[o+3],
             uniform.HeapData.[o+4],  uniform.HeapData.[o+5],  uniform.HeapData.[o+6],  uniform.HeapData.[o+7],
             uniform.HeapData.[o+8],  uniform.HeapData.[o+9],  uniform.HeapData.[o+10], uniform.HeapData.[o+11],
             uniform.HeapData.[o+12], uniform.HeapData.[o+13], uniform.HeapData.[o+14], uniform.HeapData.[o+15])

    /// per-input attribute gather: ONE call into the typeId-branching decoder,
    /// swizzled down to the shader's input type (the conversion handles widen /
    /// narrow / normalize / casts per SOURCE typeId at fetch time — the input
    /// type is fixed per effect, the source type varies per allocation).
    /// Returns None for unsupported shader input types.
    /// per-attribute-ordinal spec-constant reads (quotation splices for
    /// hostGather); ordinals beyond the pool splice constant 0 = runtime decode.
    let private heapTidReads : Expr<int>[] =
        [| <@ uniform.HeapTid0 @>; <@ uniform.HeapTid1 @>; <@ uniform.HeapTid2 @>; <@ uniform.HeapTid3 @>
           <@ uniform.HeapTid4 @>; <@ uniform.HeapTid5 @>; <@ uniform.HeapTid6 @>; <@ uniform.HeapTid7 @> |]
    let private heapTidRead (ai : int) : Expr<int> =
        if ai >= 0 && ai < heapTidReads.Length then heapTidReads.[ai] else <@ 0 @>

    /// tidE: the attribute's per-ordinal spec-constant tid (heapTidRead) — a
    /// concrete pipeline value folds the decode to one typed arm; 0 = runtime.
    let private hostGather (tidE : Expr<int>) (inputT : System.Type) (refE : Expr<int>) (vidE : Expr<int>) : Expr option =
        let inline f1 (q : Expr<'a>) = Some q.Raw
        if   inputT = typeof<V4f>     then f1 <@ decodeHeapV4f %tidE %refE %vidE @>
        elif inputT = typeof<V3f>     then f1 <@ (decodeHeapV4f %tidE %refE %vidE).XYZ @>
        elif inputT = typeof<V2f>     then f1 <@ (decodeHeapV4f %tidE %refE %vidE).XY @>
        elif inputT = typeof<float32> then f1 <@ (decodeHeapV4f %tidE %refE %vidE).X @>
        elif inputT = typeof<V4i>     then f1 <@ decodeHeapV4i %tidE %refE %vidE @>
        elif inputT = typeof<V3i>     then f1 <@ (decodeHeapV4i %tidE %refE %vidE).XYZ @>
        elif inputT = typeof<V2i>     then f1 <@ (decodeHeapV4i %tidE %refE %vidE).XY @>
        elif inputT = typeof<int>     then f1 <@ (decodeHeapV4i %tidE %refE %vidE).X @>
        elif inputT = typeof<M22f>    then f1 <@ decodeHeapM22f %refE %vidE @>
        elif inputT = typeof<M33f>    then f1 <@ decodeHeapM33f %refE %vidE @>
        elif inputT = typeof<M44f>    then f1 <@ decodeHeapM44f %refE %vidE @>
        else None

    /// supported shader INPUT types of the storage decode (the decoder pair
    /// above covers every (source typeId, target) combination)
    let private hostTargetTypes =
        System.Collections.Generic.HashSet<System.Type>(
            [ typeof<float32>; typeof<V2f>; typeof<V3f>; typeof<V4f>
              typeof<int>; typeof<V2i>; typeof<V3i>; typeof<V4i>
              typeof<M22f>; typeof<M33f>; typeof<M44f> ])

    /// can host element type `hostT` be storage-decoded into shader input type
    /// `inputT`? Decoding branches per allocation, so the answer FACTORS: the
    /// SOURCE needs a typeId, the TARGET a decoder — no pair table needed.
    let private hostDecodable (hostT : System.Type) (inputT : System.Type) =
        (attrTypeId hostT).IsSome && hostTargetTypes.Contains inputT

    /// generic native-layout packer for singleton-attribute values: blits the
    /// boxed struct's bytes (same layout as a 1-element array of it) straight
    /// into the upload ring at the region's word offset (ragged tail zeroed).
    let private attrPackerFor (t : System.Type) : int * (obj -> nativeint -> int -> unit) =
        let es = elemSize t
        if es <= 0 then failwithf "Heap: singleton attribute type %A is not blittable" t
        let szF = (es + 3) / 4
        szF, fun (o : obj) (a : nativeint) (off : int) ->
            if es % 4 <> 0 then wi a (off + szF - 1) 0
            let h = System.Runtime.InteropServices.GCHandle.Alloc(o, System.Runtime.InteropServices.GCHandleType.Pinned)
            try memcpy (h.AddrOfPinnedObject()) (a + nativeint (off <<< 2)) es
            finally h.Free()

    /// vertex-pull gather for ofRenderObjects' GPU-geometry buckets: object `slot`'s
    /// attribute `ai` lives at HeapVertexData[slot*numAttrs + ai] — an object-major
    /// flatten of the objects' EXISTING GPU buffers (no copy). Decodes `typ` at element
    /// (vid*strideF + offF); strideF/offF (in floats) come from the BufferView so both
    /// separate-tight and interleaved buffers work. Integral types use the int view.
    /// `handleE` is the per-draw handle expr (gl_InstanceIndex on Vulkan, gl_DrawID on GL).
    let private bindlessGatherFlat (handleE : Expr) (vidE : Expr) (typ : System.Type) (numAttrs : int) (ai : int) (strideF : int) (offF : int) : Expr =
        if typ = typeof<float32> then
            <@@ let b = (%%handleE : int) * numAttrs + ai in uniform.HeapVertexData.[b].[ (%%vidE : int) * strideF + offF ] @@>
        elif typ = typeof<V2f> then
            <@@ let b = (%%handleE : int) * numAttrs + ai
                let o = (%%vidE : int) * strideF + offF
                V2f(uniform.HeapVertexData.[b].[o], uniform.HeapVertexData.[b].[o+1]) @@>
        elif typ = typeof<V3f> then
            <@@ let b = (%%handleE : int) * numAttrs + ai
                let o = (%%vidE : int) * strideF + offF
                V3f(uniform.HeapVertexData.[b].[o], uniform.HeapVertexData.[b].[o+1], uniform.HeapVertexData.[b].[o+2]) @@>
        elif typ = typeof<V4f> then
            <@@ let b = (%%handleE : int) * numAttrs + ai
                let o = (%%vidE : int) * strideF + offF
                V4f(uniform.HeapVertexData.[b].[o], uniform.HeapVertexData.[b].[o+1], uniform.HeapVertexData.[b].[o+2], uniform.HeapVertexData.[b].[o+3]) @@>
        elif typ = typeof<int> then
            <@@ let b = (%%handleE : int) * numAttrs + ai in uniform.HeapVertexDataI.[b].[ (%%vidE : int) * strideF + offF ] @@>
        elif typ = typeof<V2i> then
            <@@ let b = (%%handleE : int) * numAttrs + ai
                let o = (%%vidE : int) * strideF + offF
                V2i(uniform.HeapVertexDataI.[b].[o], uniform.HeapVertexDataI.[b].[o+1]) @@>
        elif typ = typeof<V3i> then
            <@@ let b = (%%handleE : int) * numAttrs + ai
                let o = (%%vidE : int) * strideF + offF
                V3i(uniform.HeapVertexDataI.[b].[o], uniform.HeapVertexDataI.[b].[o+1], uniform.HeapVertexDataI.[b].[o+2]) @@>
        else
            <@@ let b = (%%handleE : int) * numAttrs + ai
                let o = (%%vidE : int) * strideF + offF
                V4i(uniform.HeapVertexDataI.[b].[o], uniform.HeapVertexDataI.[b].[o+1], uniform.HeapVertexDataI.[b].[o+2], uniform.HeapVertexDataI.[b].[o+3]) @@>

    /// Adaptive per-(slot, sampler) texture reference for the incremental texture
    /// tables: reads its source texture aval; marked (via the source) only when
    /// that aval changes. `Pos` is the fixed position in the per-bucket index /
    /// placement arrays (slot * K + k).
    type internal SlotTexWriter(src : IAdaptiveValue, pos : int) =
        inherit AdaptiveObject()
        do src.Acquire()
        let mutable current : ITexture = null
        let mutable disposed = false
        member _.Pos = pos
        member _.Current = current
        member _.IsDisposed = disposed
        /// evaluate the source; `changed old new` runs iff the texture identity changed.
        member x.Update(token : AdaptiveToken, changed : ITexture -> ITexture -> unit) =
            x.EvaluateIfNeeded token () (fun token ->
                let tex = src.GetValueUntyped token :?> ITexture
                if not (System.Object.ReferenceEquals(tex, current)) then
                    let old = current
                    current <- tex
                    changed old tex)
        member x.Dispose() =
            disposed <- true
            src.Release()
            src.Outputs.Remove x |> ignore
            x.Outputs.Clear()

    /// Persistent dedup table of one bucket's distinct textures for ONE input
    /// sampler. Value = (HeapTexArr&lt;si&gt;, HeapTexIdx&lt;si&gt;): the distinct-
    /// texture array (refcounted by texture identity; freed indices are reused and
    /// their cells parked on a 1×1 dummy so the descriptor array never references
    /// a dead texture) and the per-(slot, sampler) indices at slot*K + kt, growing
    /// with the slot table. Recomputed when membership changed (read through
    /// `updater`, whose evaluation calls Add/RemoveSlot first) or when a member's
    /// texture aval changed (its writer marks this table) — O(changed) either way.
    type internal BindlessTexTable(updater : aval<int>, k : int) as this =
        inherit AVal.AbstractVal<HashMapDelta<int, ITexture>>()
        let kk = max 1 k
        let pending = LockedSet<SlotTexWriter>()
        let refCounts = System.Collections.Generic.List<int>()
        let idxOf = System.Collections.Generic.Dictionary<ITexture, int>(HashIdentity.Reference)
        let freeIdx = System.Collections.Generic.Stack<int>()
        // index -> op since the last Compute = the textures amap's delta (O(changed)). A freed
        // cell emits Remove; the backend backs the now-unbound slot with its null sampler and no
        // live draw ever indexes it — so there is no dummy and every sampler type works with no
        // per-type cell.
        let pendingDelta = System.Collections.Generic.Dictionary<int, ElementOperation<ITexture>>()
        let acquire (tex : ITexture) : int =
            match idxOf.TryGetValue tex with
            | true, i -> refCounts.[i] <- refCounts.[i] + 1; i
            | _ ->
                let i =
                    if freeIdx.Count > 0 then freeIdx.Pop()
                    else (refCounts.Add 0; refCounts.Count - 1)
                refCounts.[i] <- 1
                idxOf.[tex] <- i
                pendingDelta.[i] <- ElementOperation.Set tex
                i
        let release (tex : ITexture) =
            if not (isNull tex) then
                match idxOf.TryGetValue tex with
                | true, i ->
                    refCounts.[i] <- refCounts.[i] - 1
                    if refCounts.[i] = 0 then
                        idxOf.Remove tex |> ignore
                        pendingDelta.[i] <- ElementOperation.Remove   // free the cell (backend nulls it)
                        freeIdx.Push i
                | _ -> ()
        let mutable writers : SlotTexWriter[] = Array.zeroCreate (16 * kk)
        let mutable indices : int[] = Array.zeroCreate (16 * kk)
        let mutable highPos = 0
        let ensure (n : int) =
            if n > writers.Length then
                let c = Fun.NextPowerOfTwo n
                let nw = Array.zeroCreate<SlotTexWriter> c
                System.Array.Copy(writers, nw, writers.Length)
                writers <- nw
                let ni = Array.zeroCreate<int> c
                System.Array.Copy(indices, ni, indices.Length)
                indices <- ni
        // textures as an incremental MAP (index -> texture): Compute returns its delta so
        // updates are O(changed), not a full-array rebuild. The index buffer rides the same
        // Compute (AVal.map forces it, then reads the freshly-updated array).
        let textures : amap<int, ITexture> = AMap.custom (fun token _ -> this.GetValue token)
        let indicesAval : aval<int[]> = (this :> aval<_>) |> AVal.map (fun _ -> Array.sub indices 0 (max 1 highPos))
        /// register slot's K texture avals (called from the updater's evaluation)
        member _.AddSlot(slot : int, srcs : IAdaptiveValue[]) =
            let basePos = slot * kk
            ensure (basePos + kk)
            for kt in 0 .. k - 1 do
                let w = SlotTexWriter(srcs.[kt], basePos + kt)
                writers.[basePos + kt] <- w
                pending.Add w |> ignore
            highPos <- max highPos (basePos + kk)
        /// drop slot's writers + refs (its index cells go stale — never read,
        /// the slot's draw record is an InstanceCount=0 tombstone)
        member _.RemoveSlot(slot : int) =
            let basePos = slot * kk
            for kt in 0 .. k - 1 do
                let w = writers.[basePos + kt]
                if not (System.Object.ReferenceEquals(w, null)) then
                    pending.Remove w |> ignore
                    release w.Current
                    w.Dispose()
                    writers.[basePos + kt] <- Unchecked.defaultof<_>
        override x.InputChangedObject(_, o) =
            match o with
            | :? SlotTexWriter as w -> pending.Add w |> ignore
            | _ -> ()
        /// distinct-texture array as an incremental amap (index -> texture)
        member _.Textures = textures
        /// per-(slot, sampler) indices at slot*K + kt (rides the textures Compute)
        member _.Indices = indicesAval
        override x.Compute(t) =
            updater.GetValue t |> ignore        // apply membership mutations FIRST
            for w in pending.GetAndClear() do
                if not w.IsDisposed then
                    w.Update(t, fun old tex ->
                        release old
                        indices.[w.Pos] <- acquire tex)
            let d = HashMap.ofSeq (pendingDelta |> Seq.map (fun kv -> kv.Key, kv.Value))
            pendingDelta.Clear()
            HashMapDelta d
        member x.Dispose() =
            for i in 0 .. writers.Length - 1 do
                let w = writers.[i]
                if not (System.Object.ReferenceEquals(w, null)) then
                    w.Dispose()
                    writers.[i] <- Unchecked.defaultof<_>
            refCounts.Clear(); idxOf.Clear(); freeIdx.Clear(); pendingDelta.Clear()

    /// Persistent per-slot atlas placement for an atlas bucket: ONE AtlasPool per
    /// bucket (lifetime = bucket); slot adds Acquire, slot removes Release (the
    /// pool refcounts + dedups by texture identity, so shared textures upload
    /// once). Per-(slot, sampler) placement arrays (origin / size / fmt / pageId
    /// at slot*K + k) grow with the slot table. Membership deltas and per-slot
    /// texture-identity changes re-place only the affected positions — held tiles
    /// never move (the pool evicts only refcount-0 entries), so a placement
    /// written at Acquire time stays valid until Release.
    type internal AtlasPlacementTable(updater : aval<int>, pool : AtlasPool, states : SamplerState[], k : int) =
        inherit AVal.AbstractVal<V4f[] * V4f[] * int[] * int[]>()
        let kk = max 1 k
        let pending = LockedSet<SlotTexWriter>()
        let mutable writers : SlotTexWriter[] = Array.zeroCreate (16 * kk)
        let mutable origins : V4f[] = Array.zeroCreate (16 * kk)
        let mutable sizes   : V4f[] = Array.zeroCreate (16 * kk)
        let mutable fmts    : int[] = Array.zeroCreate (16 * kk)
        let mutable pageIds : int[] = Array.zeroCreate (16 * kk)
        let mutable highPos = 0
        let addrCode (w : WrapMode option) = match w with | Some WrapMode.Wrap -> 1 | Some WrapMode.Mirror -> 2 | _ -> 0
        let ensure (n : int) =
            if n > writers.Length then
                let c = Fun.NextPowerOfTwo n
                let nw = Array.zeroCreate<SlotTexWriter> c
                System.Array.Copy(writers, nw, writers.Length)
                writers <- nw
                let no = Array.zeroCreate<V4f> c
                System.Array.Copy(origins, no, origins.Length)
                origins <- no
                let ns = Array.zeroCreate<V4f> c
                System.Array.Copy(sizes, ns, sizes.Length)
                sizes <- ns
                let nf = Array.zeroCreate<int> c
                System.Array.Copy(fmts, nf, fmts.Length)
                fmts <- nf
                let np = Array.zeroCreate<int> c
                System.Array.Copy(pageIds, np, pageIds.Length)
                pageIds <- np
        /// register slot's K texture avals (called from the updater's evaluation)
        member _.AddSlot(slot : int, srcs : IAdaptiveValue[]) =
            let basePos = slot * kk
            ensure (basePos + kk)
            for kt in 0 .. k - 1 do
                let w = SlotTexWriter(srcs.[kt], basePos + kt)
                writers.[basePos + kt] <- w
                pending.Add w |> ignore
            highPos <- max highPos (basePos + kk)
        /// release slot's atlas tiles (placement cells go stale — never read,
        /// the slot's draw record is an InstanceCount=0 tombstone)
        member _.RemoveSlot(slot : int) =
            let basePos = slot * kk
            for kt in 0 .. k - 1 do
                let w = writers.[basePos + kt]
                if not (System.Object.ReferenceEquals(w, null)) then
                    pending.Remove w |> ignore
                    if not (isNull w.Current) then pool.Release w.Current
                    w.Dispose()
                    writers.[basePos + kt] <- Unchecked.defaultof<_>
        override x.InputChangedObject(_, o) =
            match o with
            | :? SlotTexWriter as w -> pending.Add w |> ignore
            | _ -> ()
        override x.Compute(t) =
            updater.GetValue t |> ignore        // apply membership mutations FIRST
            for w in pending.GetAndClear() do
                if not w.IsDisposed then
                    w.Update(t, fun old tex ->
                        if not (isNull old) then pool.Release old
                        let (a, pid) = pool.Acquire(tex, toAtlasPixImage tex)
                        let st = states.[w.Pos % kk]
                        origins.[w.Pos] <- V4f(float32 a.OriginPx.X, float32 a.OriginPx.Y, 0.0f, 0.0f)
                        sizes.[w.Pos]   <- V4f(float32 a.SizePx.X,   float32 a.SizePx.Y,   0.0f, 0.0f)
                        fmts.[w.Pos]    <- (a.NumMips <<< 1) ||| (addrCode st.AddressU <<< 4) ||| (addrCode st.AddressV <<< 6)
                        pageIds.[w.Pos] <- pid)
            let n = max 1 highPos
            Array.sub origins 0 n, Array.sub sizes 0 n, Array.sub fmts 0 n, Array.sub pageIds 0 n
        member x.Dispose() =
            for i in 0 .. writers.Length - 1 do
                let w = writers.[i]
                if not (System.Object.ReferenceEquals(w, null)) then
                    if not (isNull w.Current) then pool.Release w.Current
                    w.Dispose()
                    writers.[i] <- Unchecked.defaultof<_>

    /// Pack an M44d as 16 df32 entries (row-major, V2f hi/lo) into `a` from float
    /// offset `foff`: entry k=(r*4+c) → floats [foff+2k]=hi, [foff+2k+1]=lo. Matches
    /// the df32 kernels' V2f-indexed reads of a link half (see composeModelDf32).
    let private packM44Df (a : float32[]) (foff : int) (m : M44d) =
        let put k (d : float) =
            let hi = float32 d
            a.[foff + 2 * k]     <- hi
            a.[foff + 2 * k + 1] <- float32 (d - float hi)
        put 0 m.M00;  put 1 m.M01;  put 2 m.M02;  put 3 m.M03
        put 4 m.M10;  put 5 m.M11;  put 6 m.M12;  put 7 m.M13
        put 8 m.M20;  put 9 m.M21;  put 10 m.M22; put 11 m.M23
        put 12 m.M30; put 13 m.M31; put 14 m.M32; put 15 m.M33

    /// One distinct trafo link -> one fp64 slot in the LinkArena. Packs the
    /// link's Forward matrix; marked (via its source) only when that link changes.
    type internal LinkWriter(src : aval<Trafo3d>, slot : int) =
        inherit AdaptiveObject()
        let mutable disposed = false
        do (src :> IAdaptiveValue).Acquire()
        member _.Slot = slot
        member _.IsDisposed = disposed
        member x.Pack(token : AdaptiveToken, staging : M44d[]) =
            x.EvaluateIfNeeded token () (fun token -> staging.[slot] <- (src.GetValue token).Forward)
        /// pack BOTH halves into an interleaved arena (slot s → [2s]=Forward,
        /// [2s+1]=Backward). Used by GrowChainLinks so the GPU can fold the backward
        /// Model (NormalMatrix / *Inv) from the uploaded Backward halves — no shader
        /// `.Inverse`. One GetValue serves both.
        member x.PackBoth(token : AdaptiveToken, staging : M44d[]) =
            x.EvaluateIfNeeded token () (fun token ->
                let t = src.GetValue token
                staging.[2*slot]   <- t.Forward
                staging.[2*slot+1] <- t.Backward)
        /// df32 variant of PackBoth: slot s → forward at float offset 64s, backward at
        /// 64s+32 (each half = 16 V2f = 32 floats; 64 floats = 256 bytes/slot, same
        /// byte layout the fp64 staging has).
        member x.PackBothDf(token : AdaptiveToken, staging : float32[]) =
            x.EvaluateIfNeeded token () (fun token ->
                let t = src.GetValue token
                packM44Df staging (64 * slot)      t.Forward
                packM44Df staging (64 * slot + 32) t.Backward)
        member x.Dispose() =
            if not disposed then
                disposed <- true
                (src :> IAdaptiveValue).Release()
                (src :> IAdaptiveValue).Outputs.Remove x |> ignore
                x.Outputs.Clear()

    // ── GROWABLE incremental trafo-link arena (live IncrementalBucket ingest) ──
    // The static LinkArena above takes the full distinct[] up front. The LIVE
    // heap path adds/removes links one RO at a time, so the arena must GROW and
    // FREE-LIST slots. Dedup keys mirror the value-vs-identity split landed in
    // derivedChainFp64 / the geometry path:
    //   * CONSTANT links key on their Trafo3d VALUE — the per-leaf box link is a
    //     DISTINCT AVal.constant per Box with an IDENTICAL value across all
    //     leaves, so N leaves' box links collapse to ONE slot. A constant slot
    //     carries NO writer (packed once); refcounted, freed when the last leaf
    //     referencing that value leaves.
    //   * DYNAMIC links key on aval IDENTITY — a shared parent scope reused
    //     across leaves is ONE slot, so editing it marks ONE slot (its LinkWriter
    //     fires once); a distinct per-leaf cval is its own slot.
    // Freed slots return to a free-list and are reused; the slot's M44d is
    // re-packed lazily on reuse. The GPU buffer grows pow2 (ResizeInPlace,
    // content-preserving, deferred to Compute — rule-clean).
    type internal ChainLinkEntry =
        { Slot : int; mutable RefCount : int; Writer : LinkWriter }

    // Links are stored INTERLEAVED — slot s occupies M44d slots [2s]=Forward,
    // [2s+1]=Backward (256 bytes/link) — so the SAME buffer feeds both the forward
    // Model fold (composeModel reads 2*idx) and the backward fold (composeModelInv
    // reads 2*idx+1), the backward half being the uploaded Trafo3d.Backward.
    type internal GrowChainLinks(runtime : IBufferRuntime, df32 : bool) =
        // DEVICE-local (see MirrorBuffer): the chain fold reads every link per
        // compute thread; Host storage is only fast where BAR memory happens to fit.
        inherit AdaptiveBuffer(runtime, 256UL, BufferUsage.Storage, BufferStorage.Device)
        // fp64: 2 M44d per slot (Forward, Backward). df32: 64 f32 per slot (two
        // 16-entry V2f halves). Only the active array is grown; both layouts are
        // 256 bytes/slot so the buffer byte size and slot offsets are identical.
        let mutable staging = Array.zeroCreate<M44d> (if df32 then 0 else 2)
        let mutable stagingDf = Array.zeroCreate<float32> (if df32 then 64 else 0)
        let mutable capacity = 1
        let byVal = System.Collections.Generic.Dictionary<Trafo3d, ChainLinkEntry>(HashIdentity.Structural)
        let byId  = System.Collections.Generic.Dictionary<IAdaptiveValue, ChainLinkEntry>(HashIdentity.Reference)
        let entryBySlot = System.Collections.Generic.Dictionary<int, ChainLinkEntry>()
        let pending = LockedSet<LinkWriter>()          // dynamic slots needing a re-pack
        let pendingOnce = System.Collections.Generic.HashSet<int>()   // constant slots packed once
        let freeSlots = System.Collections.Generic.Stack<int>()
        let mutable highWater = 0
        // bumps whenever a Compute actually uploads changed link values — lets the
        // derived dispatch tell a Model edit (re-fold needed) from a camera move.
        let mutable generation = 0

        let ensureCap (n : int) =
            if n > capacity then
                let nf = Fun.NextPowerOfTwo n
                if df32 then
                    let ns = Array.zeroCreate<float32> (64 * nf)
                    System.Array.Copy(stagingDf, ns, 64 * capacity)
                    stagingDf <- ns
                else
                    let ns = Array.zeroCreate<M44d> (2 * nf)
                    System.Array.Copy(staging, ns, 2 * capacity)
                    staging <- ns
                capacity <- nf

        let allocSlot () =
            if freeSlots.Count > 0 then freeSlots.Pop()
            else let s = highWater in highWater <- s + 1; ensureCap highWater; s

        /// Intern a link to a slot (refcount++). CONSTANT -> value-deduped + packed
        /// once into staging; DYNAMIC -> identity-deduped with a LinkWriter.
        member _.Intern(link : aval<Trafo3d>) : int =
            if link.IsConstant then
                let v = AVal.force link
                match byVal.TryGetValue v with
                | true, e -> e.RefCount <- e.RefCount + 1; e.Slot
                | _ ->
                    let s = allocSlot ()
                    if df32 then
                        packM44Df stagingDf (64 * s)      v.Forward
                        packM44Df stagingDf (64 * s + 32) v.Backward
                    else
                        staging.[2*s]   <- v.Forward
                        staging.[2*s+1] <- v.Backward
                    pendingOnce.Add s |> ignore
                    let e = { Slot = s; RefCount = 1; Writer = Unchecked.defaultof<LinkWriter> }
                    byVal.[v] <- e; entryBySlot.[s] <- e; s
            else
                match byId.TryGetValue (link :> IAdaptiveValue) with
                | true, e -> e.RefCount <- e.RefCount + 1; e.Slot
                | _ ->
                    let s = allocSlot ()
                    // pack on next Compute; the Pack call there (under the arena
                    // token) establishes the link->arena edge, so a later change
                    // routes through InputChangedObject (same as the static arena).
                    let w = LinkWriter(link, s)
                    pending.Add w |> ignore
                    let e = { Slot = s; RefCount = 1; Writer = w }
                    byId.[link :> IAdaptiveValue] <- e; entryBySlot.[s] <- e; s

        /// Release one reference to the slot interned for `link`.
        member _.Release(link : aval<Trafo3d>) =
            let found, e =
                if link.IsConstant then byVal.TryGetValue (AVal.force link)
                else byId.TryGetValue (link :> IAdaptiveValue)
            if found then
                e.RefCount <- e.RefCount - 1
                if e.RefCount = 0 then
                    entryBySlot.Remove e.Slot |> ignore
                    if not (isNull (box e.Writer)) then
                        pending.Remove e.Writer |> ignore
                        e.Writer.Dispose()
                    pendingOnce.Remove e.Slot |> ignore
                    freeSlots.Push e.Slot
                    if link.IsConstant then byVal.Remove (AVal.force link) |> ignore
                    else byId.Remove (link :> IAdaptiveValue) |> ignore

        /// distinct live links (diagnostic / linkArena byte size lower bound)
        member _.DistinctCount = entryBySlot.Count
        member _.HighWater = highWater
        /// bumped on every Compute that uploads changed link values.
        member _.Generation = generation

        override x.Compute(t, rt) =
            x.ResizeInPlace(uint64 (max 1 capacity * 256))
            let dirty = pending.GetAndClear()
            let slots = System.Collections.Generic.List<int>()
            for s in pendingOnce do slots.Add s
            pendingOnce.Clear()
            for w in dirty do
                if not w.IsDisposed then (if df32 then w.PackBothDf(t, stagingDf) else w.PackBoth(t, staging)); slots.Add w.Slot
            if slots.Count > 0 then
                generation <- generation + 1
                lastChainLinkUploads <- slots.Count
                slots.Sort()
                // each link slot s = 256 bytes (2 interleaved halves). fp64: write
                // M44d-element offset 2*lo, count 2*(run). df32: f32-element offset
                // 64*lo, count 64*(run). Byte offset lo*256 is the same for both.
                let flush lo hi =
                    if df32 then x.Write(stagingDf, uint64 (lo * 256), 64 * lo, 64 * (hi - lo + 1), false)
                    else x.Write(staging, uint64 (lo * 256), 2 * lo, 2 * (hi - lo + 1), false)
                let mutable lo = slots.[0]
                let mutable hi = slots.[0]
                for i in 1 .. slots.Count - 1 do
                    let s = slots.[i]
                    if s <= hi + 1 then hi <- s
                    else flush lo hi; lo <- s; hi <- s
                flush lo hi
            base.Compute(t, rt)
        override x.InputChangedObject(_, o) =
            match o with
            | :? LinkWriter as w -> pending.Add w |> ignore
            | _ -> ()

    /// Storage writers used by the chain fold + derived compute kernel — row-major
    /// into the arena, matching `gatherFor`'s read layout (M44f: 16 contiguous
    /// M00..M33; M33f: 9 contiguous M00..M22; M44d: into the 8-byte-aligned DOUBLE
    /// view at word offset woff → double index woff>>>1). [<ReflectedDefinition>] so
    /// FShade lowers them into the compute body.
    [<RequireQualifiedAccess>]
    module internal HeapWrite =
        // Write straight into the arena storage buffers (uniform?StorageBuffer?…),
        // accessed DIRECTLY — never passed as a function parameter (FShade can't
        // resolve a storage buffer through an array arg; it would emit an unsized
        // local). m44/m33 → HeapData (f32, row-major matching gatherFor); m44dInto →
        // the 8-byte-aligned HeapDataD double view at word offset woff (idx = woff>>>1).
        [<ReflectedDefinition>]
        let m44 (off : int) (m : M44f) =
            uniform.HeapData.[off+0]<-m.M00;  uniform.HeapData.[off+1]<-m.M01;  uniform.HeapData.[off+2]<-m.M02;  uniform.HeapData.[off+3]<-m.M03
            uniform.HeapData.[off+4]<-m.M10;  uniform.HeapData.[off+5]<-m.M11;  uniform.HeapData.[off+6]<-m.M12;  uniform.HeapData.[off+7]<-m.M13
            uniform.HeapData.[off+8]<-m.M20;  uniform.HeapData.[off+9]<-m.M21;  uniform.HeapData.[off+10]<-m.M22; uniform.HeapData.[off+11]<-m.M23
            uniform.HeapData.[off+12]<-m.M30; uniform.HeapData.[off+13]<-m.M31; uniform.HeapData.[off+14]<-m.M32; uniform.HeapData.[off+15]<-m.M33
        [<ReflectedDefinition>]
        let m33 (off : int) (m : M33f) =
            uniform.HeapData.[off+0]<-m.M00; uniform.HeapData.[off+1]<-m.M01; uniform.HeapData.[off+2]<-m.M02
            uniform.HeapData.[off+3]<-m.M10; uniform.HeapData.[off+4]<-m.M11; uniform.HeapData.[off+5]<-m.M12
            uniform.HeapData.[off+6]<-m.M20; uniform.HeapData.[off+7]<-m.M21; uniform.HeapData.[off+8]<-m.M22
        // DENSE uniform-store variants (derived-composite outputs live in the
        // bucket-global HeapUni buffer, not the paged geometry arena)
        [<ReflectedDefinition>]
        let uniM44 (off : int) (m : M44f) =
            uniform.HeapUni.[off+0]<-m.M00;  uniform.HeapUni.[off+1]<-m.M01;  uniform.HeapUni.[off+2]<-m.M02;  uniform.HeapUni.[off+3]<-m.M03
            uniform.HeapUni.[off+4]<-m.M10;  uniform.HeapUni.[off+5]<-m.M11;  uniform.HeapUni.[off+6]<-m.M12;  uniform.HeapUni.[off+7]<-m.M13
            uniform.HeapUni.[off+8]<-m.M20;  uniform.HeapUni.[off+9]<-m.M21;  uniform.HeapUni.[off+10]<-m.M22; uniform.HeapUni.[off+11]<-m.M23
            uniform.HeapUni.[off+12]<-m.M30; uniform.HeapUni.[off+13]<-m.M31; uniform.HeapUni.[off+14]<-m.M32; uniform.HeapUni.[off+15]<-m.M33
        [<ReflectedDefinition>]
        let uniM33 (off : int) (m : M33f) =
            uniform.HeapUni.[off+0]<-m.M00; uniform.HeapUni.[off+1]<-m.M01; uniform.HeapUni.[off+2]<-m.M02
            uniform.HeapUni.[off+3]<-m.M10; uniform.HeapUni.[off+4]<-m.M11; uniform.HeapUni.[off+5]<-m.M12
            uniform.HeapUni.[off+6]<-m.M20; uniform.HeapUni.[off+7]<-m.M21; uniform.HeapUni.[off+8]<-m.M22
        [<ReflectedDefinition>]
        let m44dInto (woff : int) (m : M44d) =
            let o = woff >>> 1
            uniform.HeapDataD.[o+0]<-m.M00;  uniform.HeapDataD.[o+1]<-m.M01;  uniform.HeapDataD.[o+2]<-m.M02;  uniform.HeapDataD.[o+3]<-m.M03
            uniform.HeapDataD.[o+4]<-m.M10;  uniform.HeapDataD.[o+5]<-m.M11;  uniform.HeapDataD.[o+6]<-m.M12;  uniform.HeapDataD.[o+7]<-m.M13
            uniform.HeapDataD.[o+8]<-m.M20;  uniform.HeapDataD.[o+9]<-m.M21;  uniform.HeapDataD.[o+10]<-m.M22; uniform.HeapDataD.[o+11]<-m.M23
            uniform.HeapDataD.[o+12]<-m.M30; uniform.HeapDataD.[o+13]<-m.M31; uniform.HeapDataD.[o+14]<-m.M32; uniform.HeapDataD.[o+15]<-m.M33

    // ── df32 (double-float = two f32) — near-double precision WITHOUT ───────
    // shaderFloat64, for MoltenVK/Metal where shaders have NO `double` at all
    // (neither arithmetic nor storage). A df32 scalar is a V2f (hi=X, lo=Y) with
    // value ≈ hi+lo. A df32 mat4 is an Arr<N<16>, V2f> (row-major). These kernels
    // are selected over the M44d ones when the device lacks shaderFloat64. PORTED
    // VERBATIM from wombat.rendering's proven WGSL df32 (derivedUniforms/codegen.ts:
    // DF32_LIB) — including the Veltkamp-split TwoProduct, NOT an fma TwoProduct.
    //
    // WHY split, not fma: these error-free transforms only work if the compiler
    // cannot algebraically simplify the error terms. An fma TwoProduct
    // (p = a·b; err = fma(a,b,−p)) is catastrophic under a fast-math value-numbering
    // pass: knowing p ≡ a·b, it folds err to EXACTLY 0 → pure f32, all precision
    // gone. The split form routes `hi` through a floatBitsToUint/uintBitsToFloat
    // round-trip; that bitcast is an OPTIMIZATION BARRIER — the compiler can't see
    // `hi` as a function of `a`, so it can't reassociate `a − hi` or `A.x·B.x − p`.
    // The rounding-critical SUMS still go through `fma(1.0, s, −a)` for the same
    // reason. ⚠ Residual risk remains if the Metal backend applies fp CONTRACTION /
    // fast-math reassociation around these (Metal defaults fast-math ON); that is a
    // MoltenVK-config / NoContraction concern verified by inspecting the emitted MSL,
    // not something a shader-source trick alone can guarantee. See the project memo.
    //
    // The arena WORD layout is identical to the fp64 double region: entry k of a
    // df32 mat4 at word offset `woff` lives at words [woff+2k]=hi, [woff+2k+1]=lo
    // (the fp64 path read these same 2 words as one IEEE double; df32 reads them as
    // a (hi,lo) pair) — so the CPU constituent offsets are shared across both paths.
    // ([<Inline>] is intentionally omitted: FShade ignores it for compute shaders.)
    [<RequireQualifiedAccess>]
    module internal Df32 =
        // raw GLSL fused multiply-add, emitted verbatim.
        [<GLSLIntrinsic("fma({0}, {1}, {2})")>]
        let fma (a : float32) (b : float32) (c : float32) : float32 = onlyInShaderCode "fma"

        /// Veltkamp split of an f32 into (hi, lo) with hi holding the top 11 mantissa
        /// bits. The bitcast round-trip is an optimization barrier (see module note).
        [<ReflectedDefinition>]
        let split12 (a : float32) : V2f =
            let hi = Bitwise.UIntBitsToFloat (Bitwise.FloatBitsToUInt a &&& 0xFFFFE000u)
            V2f(hi, a - hi)

        /// Knuth TwoSum: (s, err) with s = round(a+b) and a+b = s+err exactly.
        [<ReflectedDefinition>]
        let twoSum (a : float32) (b : float32) : V2f =
            let s  = a + b
            let bb = fma 1.0f s (-a)
            let t1 = fma 1.0f s (-bb)
            let t2 = fma 1.0f a (-t1)
            let t3 = fma 1.0f b (-bb)
            V2f(s, t2 + t3)

        /// Dekker QuickTwoSum: valid when |a| ≥ |b| (the df_add carries guarantee it).
        [<ReflectedDefinition>]
        let quickTwoSum (a : float32) (b : float32) : V2f =
            let s = a + b
            let t = fma 1.0f s (-a)
            V2f(s, fma 1.0f b (-t))

        /// Dekker/Veltkamp TwoProduct via split (NOT fma — see module note):
        /// (p, err) with p = round(a·b) and a·b = p+err exactly.
        [<ReflectedDefinition>]
        let twoProd (a : float32) (b : float32) : V2f =
            let p = a * b
            let aa = split12 a
            let bb = split12 b
            let err = ((aa.X * bb.X - p) + aa.X * bb.Y + aa.Y * bb.X) + aa.Y * bb.Y
            V2f(p, err)

        /// df32 add: a + b, both as V2f(hi,lo).
        [<ReflectedDefinition>]
        let add (a : V2f) (b : V2f) : V2f =
            let s = twoSum a.X b.X
            let t = twoSum a.Y b.Y
            let s3 = quickTwoSum s.X (s.Y + t.X)
            quickTwoSum s3.X (s3.Y + t.Y)

        /// df32 multiply: a · b.
        [<ReflectedDefinition>]
        let mul (a : V2f) (b : V2f) : V2f =
            let p = twoProd a.X b.X
            let cross1 = fma a.X b.Y p.Y
            let cross  = fma a.Y b.X cross1
            quickTwoSum p.X cross

        /// promote a plain f32 to df32 (lo = 0).
        [<ReflectedDefinition>]
        let ofF (x : float32) : V2f = V2f(x, 0.0f)

        /// collapse a df32 back to the nearest f32 (the value the render shader gets).
        [<ReflectedDefinition>]
        let collapse (a : V2f) : float32 = a.X + a.Y

        // ── arena access (df32 reinterpretation of the double region) ──────
        // Read entry k=(r*4+c) of a df32 mat4 stored at WORD offset `woff` in the
        // f32 arena: words [woff+2k]=hi, [woff+2k+1]=lo.
        [<ReflectedDefinition>]
        let ldEntry (woff : int) (k : int) : V2f =
            let o = woff + 2 * k
            V2f(uniform.HeapData.[o], uniform.HeapData.[o+1])

        /// write entry k of a df32 mat4 back at WORD offset `woff`, KEEPING both
        /// halves (full df32 precision) — for an intermediate constituent (e.g. the
        /// chain-folded Model) a downstream df32 pass re-reads. Mirrors wombat's
        /// write_constituent_entry.
        [<ReflectedDefinition>]
        let stEntry (woff : int) (k : int) (e : V2f) =
            let o = woff + 2 * k
            uniform.HeapData.[o]   <- e.X
            uniform.HeapData.[o+1] <- e.Y

    // ── GPU transform propagation (wombat §7 "modelChain") ──────────────
    // Instead of CPU-composing each RO's ModelTrafo — a shared parent over N
    // objects marks N composites => N arena uploads, the worst adaptive fan-out —
    // emit each RO's ancestor CHAIN of trafo links and compose Model = L0·…·Ln on
    // the GPU (fp64). Distinct links are stored ONCE (shared parent = one link),
    // so changing a shared parent marks ONE link => one upload + one dispatch,
    // regardless of N. Links are root-first ([root, …, leaf]) so the forward
    // product equals aardvark's ModelTrafo. A compute pass writes per-object MVP +
    // NormalMatrix (fp64, camera-relative) gathered by gl_InstanceIndex — same
    // arena layout as derivedFp64.
    module private Chain =
        // LIVE-bucket variant: one thread per slot composes the slot's chain in
        // fp64 and writes the folded ModelTrafo as a real M44d into the slot's Model
        // FORWARD constituent region (arena DOUBLE view at the `mCell` header offset)
        // — the unified derived compute pass then reads it like any other arena
        // constituent (no special-cased dedicated buffer, no M44f round-trip that
        // would drop the geodetic precision the fp64 fold just computed). Tombstoned
        // slots (len = 0) write identity. linkIdx is fed slot-major so the chain's
        // links sit at [off, off+len); chains are fed root-LAST (reversed model
        // stack), so the reversed multiply below reproduces the CPU fold
        // arr[0].F · arr[1].F · … (see the bucket ingest for the ordering).
        [<LocalSize(X = 64)>]
        let composeModel (n : int) (hstride : int) (mCell : int)
                         (chainOffset : int[]) (chainLen : int[]) (linkIdx : int[])
                         (links : M44d[]) =
            compute {
                let i = getGlobalId().X
                if i < n && uniform.HeapSlotPage.[i] = uniform.HeapPageId then
                    let len = chainLen.[i]
                    let mutable m = M44d.Identity
                    if len > 0 then
                        let off = chainOffset.[i]
                        m <- links.[2 * linkIdx.[off + len - 1]]
                        for k in 1 .. len - 1 do
                            m <- m * links.[2 * linkIdx.[off + len - 1 - k]]
                    HeapWrite.m44dInto uniform.HeapHeaders.[i * hstride + mCell] m
            }

        // BACKWARD model fold (chainMode): the per-slot ModelTrafo INVERSE, used by
        // NormalMatrix and the *Inv composites, written as M44d into the slot's Model
        // BACKWARD constituent region (arena DOUBLE view at `mCell`). (L0·…·Llen-1)⁻¹
        // = Llen-1⁻¹·…·L0⁻¹, and each link's inverse is its UPLOADED `.Backward` half
        // (links carry Forward; linksB carry Backward) — never a shader `.Inverse`.
        // Forward folds the links reversed (Ln-1.F·…·L0.F); the backward therefore
        // folds them in ARRAY order (L0.B·…·Ln-1.B), which is exactly (Forward)⁻¹.
        [<LocalSize(X = 64)>]
        let composeModelInv (n : int) (hstride : int) (mCell : int)
                            (chainOffset : int[]) (chainLen : int[]) (linkIdx : int[])
                            (links : M44d[]) =
            compute {
                let i = getGlobalId().X
                if i < n && uniform.HeapSlotPage.[i] = uniform.HeapPageId then
                    let len = chainLen.[i]
                    let mutable m = M44d.Identity
                    if len > 0 then
                        let off = chainOffset.[i]
                        // backward halves at 2*idx+1, folded in ARRAY order (= inverse
                        // of the forward product).
                        m <- links.[2 * linkIdx.[off] + 1]
                        for k in 1 .. len - 1 do
                            m <- m * links.[2 * linkIdx.[off + k] + 1]
                    HeapWrite.m44dInto uniform.HeapHeaders.[i * hstride + mCell] m
            }

        // ── df32 variants (no shaderFloat64) ──────────────────────────────
        // Identical fold to the M44d kernels above, but the link arena is a FLAT
        // df32 buffer: `links : V2f[]`, 16 V2f entries (row-major (hi,lo)) per mat4,
        // link slot s → forward at element (2s)*16, backward at (2s+1)*16. The
        // folded Model is written df32 (both halves) into the slot's Model
        // FORWARD/BACKWARD constituent region so the df32 derived pass re-reads it
        // at full precision (= the fp64 path's m44dInto, in df32). recStride / link
        // dedup / chain ordering are shared with the fp64 kernels.
        //
        // The matmul is INLINED in each kernel: `links` is a storage buffer, and
        // FShade can't resolve a storage buffer passed through a function parameter
        // (it would emit an unsized local — see HeapWrite's note), so it must be
        // indexed directly in the kernel body. Df32.add/mul take V2f SCALARS, which
        // pass through fine. (FShade ignores [<Inline>] for compute; correctness
        // does not depend on it.) The running product P and scratch Q are local
        // df32 mat4s (Arr<N<16>, V2f>, row-major).
        [<LocalSize(X = 64)>]
        let composeModelDf32 (n : int) (hstride : int) (mCell : int)
                             (chainOffset : int[]) (chainLen : int[]) (linkIdx : int[])
                             (links : V2f[]) =
            compute {
                let i = getGlobalId().X
                if i < n then
                    let len = chainLen.[i]
                    let p = Arr<N<16>, V2f>()
                    if len > 0 then
                        let off = chainOffset.[i]
                        // P = first link (forward half, reversed order: off+len-1).
                        let b0 = 2 * linkIdx.[off + len - 1] * 16
                        for k in 0 .. 15 do p.[k] <- links.[b0 + k]
                        for kk in 1 .. len - 1 do
                            let b = 2 * linkIdx.[off + len - 1 - kk] * 16
                            let q = Arr<N<16>, V2f>()
                            for r in 0 .. 3 do
                                for c in 0 .. 3 do
                                    let mutable acc = V2f(0.0f, 0.0f)
                                    for t in 0 .. 3 do
                                        acc <- Df32.add acc (Df32.mul p.[r * 4 + t] links.[b + t * 4 + c])
                                    q.[r * 4 + c] <- acc
                            for k in 0 .. 15 do p.[k] <- q.[k]
                    else
                        for k in 0 .. 15 do p.[k] <- V2f(0.0f, 0.0f)
                        p.[0] <- V2f(1.0f, 0.0f); p.[5] <- V2f(1.0f, 0.0f)
                        p.[10] <- V2f(1.0f, 0.0f); p.[15] <- V2f(1.0f, 0.0f)
                    let woff = uniform.HeapHeaders.[i * hstride + mCell]
                    for k in 0 .. 15 do Df32.stEntry woff k p.[k]
            }

        [<LocalSize(X = 64)>]
        let composeModelInvDf32 (n : int) (hstride : int) (mCell : int)
                                (chainOffset : int[]) (chainLen : int[]) (linkIdx : int[])
                                (links : V2f[]) =
            compute {
                let i = getGlobalId().X
                if i < n then
                    let len = chainLen.[i]
                    let p = Arr<N<16>, V2f>()
                    if len > 0 then
                        let off = chainOffset.[i]
                        // backward halves at (2*idx+1)*16, folded in ARRAY order.
                        let b0 = (2 * linkIdx.[off] + 1) * 16
                        for k in 0 .. 15 do p.[k] <- links.[b0 + k]
                        for kk in 1 .. len - 1 do
                            let b = (2 * linkIdx.[off + kk] + 1) * 16
                            let q = Arr<N<16>, V2f>()
                            for r in 0 .. 3 do
                                for c in 0 .. 3 do
                                    let mutable acc = V2f(0.0f, 0.0f)
                                    for t in 0 .. 3 do
                                        acc <- Df32.add acc (Df32.mul p.[r * 4 + t] links.[b + t * 4 + c])
                                    q.[r * 4 + c] <- acc
                            for k in 0 .. 15 do p.[k] <- q.[k]
                    else
                        for k in 0 .. 15 do p.[k] <- V2f(0.0f, 0.0f)
                        p.[0] <- V2f(1.0f, 0.0f); p.[5] <- V2f(1.0f, 0.0f)
                        p.[10] <- V2f(1.0f, 0.0f); p.[15] <- V2f(1.0f, 0.0f)
                    let woff = uniform.HeapHeaders.[i * hstride + mCell]
                    for k in 0 .. 15 do Df32.stEntry woff k p.[k]
            }

    // ── ofRenderObjects derived-uniform COMPUTE pass (wombat §7, real fp64) ─────
    // The incremental heap derives camera/normal composites ONCE PER SLOT in an
    // fp64 compute pre-pass — NEVER per vertex. A consumed derived uniform
    // (ModelViewProjTrafo, ModelViewTrafo, ViewProjTrafo, NormalMatrix, their
    // *Inv forms, …) is produced from the base-trafo constituents (Model per slot;
    // View/Proj per frame) and written to an arena f32 region the rewritten shader
    // gathers like any other field. fp64 throughout (M44d), matching a CPU double
    // compose bit-for-bit. NO shader ever calls `.Inverse`: an inverse is the
    // uploaded `.Backward` half, and an inverse-of-product is the reverse-order
    // product of backward halves ((P·V·M)⁻¹ = M⁻¹·V⁻¹·P⁻¹); NormalMatrix is
    // transpose(Model_backward) upper-3×3. v0 ships the standard recipes with one
    // fixed kernel arm each; the rule table + per-record (ruleId, outCell) layout
    // are the seam a user-supplied rule plugs into later (a new arm / generic
    // lowering) without reshaping the records or dispatch.

    /// A base-trafo constituent: one of Model/View/Proj, forward or backward half.
    type internal Constituent = { CBase : string; CInv : bool }
    /// How a derived uniform is produced from its constituents by the compute pass.
    type internal DerivedOp =
        | DMatMul of Constituent list        // out = matrix product in listed (multiplication) order
        | DNormal of Constituent             // out = transpose(constituent) upper-3x3 (mat3)

    [<RequireQualifiedAccess>]
    module internal Derived =
        let MBASE = "ModelTrafo"
        let VBASE = "ViewTrafo"
        let PBASE = "ProjTrafo"
        let VPBASE = "ViewProjTrafo"   // a combined View·Proj a consumer may supply directly
        let fwd b = { CBase = b; CInv = false }
        let bwd b = { CBase = b; CInv = true }

        // Kernel arms — the value the kernel switches on. Each arm reads its inputs
        // generically from per-record constituent CELLS (no constituent is special-
        // cased: Model, View, Proj — forward or backward — are all ref-counted-by-
        // aval arena M44d regions, View/Proj shared to one slot each). A future
        // user rule is a new arm id (or a generic-lowering arm) over the same record
        // layout, not a reshape.
        let ARM_COLLAPSE = 1    // out = in0 (a single constituent, fwd/bwd)
        let ARM_MATMUL2  = 2    // out = in0 * in1
        let ARM_MATMUL3  = 3    // out = in0 * in1 * in2
        let ARM_NORMAL   = 9    // out = transpose(in0) upper-3x3 (mat3)

        /// The standard recipes, keyed by the derived uniform name each produces.
        /// The base trafos collapse a constituent (forward, or the uploaded BACKWARD
        /// half for the *Inv passthroughs). An inverse-of-product is the reverse-order
        /// product of backward halves ((P·V·M)⁻¹ = M⁻¹·V⁻¹·P⁻¹) — never a `.Inverse`.
        /// NormalMatrix = transpose(Model_backward) upper-3×3.
        // Each name maps to RANKED alternative recipes; the bucket picks the first
        // whose constituents are all available for the RO. So ModelViewProjTrafo
        // prefers Proj·View·Model (constituents provided) but falls back to
        // ViewProjTrafo·Model when only a combined ViewProjTrafo is supplied — the
        // heap derives whatever it can from what the consumer actually provides.
        let standard : Map<string, (int * DerivedOp) list> =
            Map.ofList [
                "ModelTrafo",            [ ARM_COLLAPSE, DMatMul [ fwd MBASE ] ]
                "ModelTrafoInv",         [ ARM_COLLAPSE, DMatMul [ bwd MBASE ] ]
                "ViewTrafo",             [ ARM_COLLAPSE, DMatMul [ fwd VBASE ] ]
                "ViewTrafoInv",          [ ARM_COLLAPSE, DMatMul [ bwd VBASE ] ]
                "ProjTrafo",             [ ARM_COLLAPSE, DMatMul [ fwd PBASE ] ]
                "ProjTrafoInv",          [ ARM_COLLAPSE, DMatMul [ bwd PBASE ] ]
                "ViewProjTrafo",         [ ARM_MATMUL2,  DMatMul [ fwd PBASE; fwd VBASE ] ]
                "ViewProjTrafoInv",      [ ARM_MATMUL2,  DMatMul [ bwd VBASE; bwd PBASE ] ]
                "ModelViewTrafo",        [ ARM_MATMUL2,  DMatMul [ fwd VBASE; fwd MBASE ] ]
                "ModelViewTrafoInv",     [ ARM_MATMUL2,  DMatMul [ bwd MBASE; bwd VBASE ] ]
                "ModelViewProjTrafo",    [ (ARM_MATMUL3, DMatMul [ fwd PBASE; fwd VBASE; fwd MBASE ])
                                           (ARM_MATMUL2, DMatMul [ fwd VPBASE; fwd MBASE ]) ]
                "ModelViewProjTrafoInv", [ (ARM_MATMUL3, DMatMul [ bwd MBASE; bwd VBASE; bwd PBASE ])
                                           (ARM_MATMUL2, DMatMul [ bwd MBASE; bwd VPBASE ]) ]
                "NormalMatrix",          [ ARM_NORMAL,   DNormal (bwd MBASE) ]
            ]

        let isDerived (n : string) = Map.containsKey n standard
        let tryRules (n : string) = Map.tryFind n standard
        /// the constituents a derived name consumes, in multiplication order.
        let constituentsOf (op : DerivedOp) = match op with | DMatMul cs -> cs | DNormal c -> [ c ]
        /// the first alternative recipe whose constituents are ALL available for the
        /// RO (per `avail baseName`), or None if the name isn't derivable here.
        let pickRule (avail : string -> bool) (n : string) : (int * DerivedOp) option =
            match tryRules n with
            | Some alts -> alts |> List.tryFind (fun (_, op) -> constituentsOf op |> List.forall (fun c -> avail c.CBase))
            | None -> None
        /// does any alternative for `n` consume Model (chain-eligibility signal)?
        let dependsOnModel (n : string) =
            match tryRules n with
            | Some alts -> alts |> List.exists (fun (_, op) -> constituentsOf op |> List.exists (fun c -> c.CBase = MBASE))
            | None -> false

        // one thread per slot. Each record is [arm; outCell; inCell0; inCell1;
        // inCell2] (unused input cells = the slot's own header base, harmlessly
        // re-read). Every constituent — Model/View/Proj, forward/backward — is an
        // arena M44d region whose per-slot WORD offset lives in this slot's header
        // at the named cell; View/Proj are shared regions, so their cell holds the
        // same offset in every slot. `dataD`/`dataF` alias the SAME arena buffer
        // (double-read / f32-write); output regions are disjoint from the inputs, so
        // there is no in-pass read-after-write hazard. recStride = 5.
        [<ReflectedDefinition>]
        let private ldM44 (woff : int) : M44d =
            let o = woff >>> 1
            M44d(uniform.HeapDataD.[o+0],  uniform.HeapDataD.[o+1],  uniform.HeapDataD.[o+2],  uniform.HeapDataD.[o+3],
                 uniform.HeapDataD.[o+4],  uniform.HeapDataD.[o+5],  uniform.HeapDataD.[o+6],  uniform.HeapDataD.[o+7],
                 uniform.HeapDataD.[o+8],  uniform.HeapDataD.[o+9],  uniform.HeapDataD.[o+10], uniform.HeapDataD.[o+11],
                 uniform.HeapDataD.[o+12], uniform.HeapDataD.[o+13], uniform.HeapDataD.[o+14], uniform.HeapDataD.[o+15])

        [<Literal>]
        let REC_STRIDE = 5

        // The arena (HeapDataD double-read view / HeapData f32-write view) and the
        // header table are the SAME storage buffers the render shader uses
        // (uniform?StorageBuffer?…), bound by name; `records` is the only entry-point
        // array param (int[] binds cleanly, unlike a float[] entry param which FShade
        // would emit as an unsized uniform array). recStride = 5.
        [<LocalSize(X = 64)>]
        let composeDerived (n : int) (hstride : int) (records : int[]) =
            compute {
                let i = getGlobalId().X
                if i < n then
                    // PER-OUTPUT dispatch: one thread per SHARE (distinct derived value),
                    // listed in HeapShareRecs as [ownerSlot; planIdx]. All offsets still
                    // resolve through the owner's header cells (compaction-safe).
                    let slot = uniform.HeapShareRecs.[i * 2]
                    if uniform.HeapSlotPage.[slot] = uniform.HeapPageId then
                        let hb = slot * hstride
                        let rb = uniform.HeapShareRecs.[i * 2 + 1] * REC_STRIDE
                        let outOff = uniform.HeapHeaders.[hb + records.[rb + 1]]
                        let a = ldM44 (uniform.HeapHeaders.[hb + records.[rb + 2]])
                        match records.[rb] with
                        | 1 -> HeapWrite.uniM44 outOff (M44f(a))
                        | 2 -> let b = ldM44 (uniform.HeapHeaders.[hb + records.[rb + 3]])
                               HeapWrite.uniM44 outOff (M44f(a * b))
                        | 3 -> let b = ldM44 (uniform.HeapHeaders.[hb + records.[rb + 3]])
                               let c = ldM44 (uniform.HeapHeaders.[hb + records.[rb + 4]])
                               HeapWrite.uniM44 outOff (M44f(a * b * c))
                        | _ ->
                            // NormalMatrix = transpose(Model_backward) upper-3x3.
                            let t = a.Transposed
                            HeapWrite.uniM33 outOff
                                (M33f(float32 t.M00, float32 t.M01, float32 t.M02,
                                      float32 t.M10, float32 t.M11, float32 t.M12,
                                      float32 t.M20, float32 t.M21, float32 t.M22))
            }

        // df32 variant of composeDerived (no shaderFloat64): identical record
        // dispatch and arena layout, but constituents are read as df32 (V2f hi/lo)
        // via Df32.ldEntry at the SAME per-slot word offsets, the products run in
        // df32 (Df32.mul/add), and each result is collapsed to f32 (Df32.collapse)
        // into the render-consumed output region — exactly the M44f the fp64 path
        // wrote. Matmul is inlined (df32 mat4 = row-major, entry (r,c) at r*4+c);
        // arm 3 holds the A·B intermediate in a local df32 mat4 so no precision is
        // dropped before ·C. NormalMatrix writes out[i*3+j] = A[j,i] collapsed.
        [<LocalSize(X = 64)>]
        let composeDerivedDf32 (n : int) (hstride : int) (records : int[]) =
            compute {
                let i = getGlobalId().X
                if i < n then
                    // per-output dispatch — see composeDerived.
                    let slot = uniform.HeapShareRecs.[i * 2]
                    if uniform.HeapSlotPage.[slot] = uniform.HeapPageId then
                        let hb = slot * hstride
                        let rb = uniform.HeapShareRecs.[i * 2 + 1] * REC_STRIDE
                        let outOff = uniform.HeapHeaders.[hb + records.[rb + 1]]
                        let offA = uniform.HeapHeaders.[hb + records.[rb + 2]]
                        // Reads/writes mirror the fp64 composeDerived EXACTLY (row-major
                        // M(r,c) at arena word r*4+c): fp64 reads constituents via ldM44 as
                        // logical row-major (FShade's matrix flip is internal to the M44d
                        // value and doesn't alter the scalar buffer layout), multiplies
                        // naively, and writes row-major — and the df32 constituents are
                        // packed (wdDf) at the same layout. NormalMatrix = transpose(A)
                        // upper-3x3: out[i*3+j] = A(j,i).
                        match records.[rb] with
                        | 1 ->
                            for k in 0 .. 15 do
                                uniform.HeapUni.[outOff + k] <- Df32.collapse (Df32.ldEntry offA k)
                        | 2 ->
                            let offB = uniform.HeapHeaders.[hb + records.[rb + 3]]
                            for rr in 0 .. 3 do
                                for c in 0 .. 3 do
                                    let mutable acc = V2f(0.0f, 0.0f)
                                    for t in 0 .. 3 do
                                        acc <- Df32.add acc (Df32.mul (Df32.ldEntry offA (rr * 4 + t)) (Df32.ldEntry offB (t * 4 + c)))
                                    uniform.HeapUni.[outOff + rr * 4 + c] <- Df32.collapse acc
                        | 3 ->
                            let offB = uniform.HeapHeaders.[hb + records.[rb + 3]]
                            let offC = uniform.HeapHeaders.[hb + records.[rb + 4]]
                            // P = A·B in df32 (local, row-major), then out = P·C.
                            let p = Arr<N<16>, V2f>()
                            for rr in 0 .. 3 do
                                for c in 0 .. 3 do
                                    let mutable acc = V2f(0.0f, 0.0f)
                                    for t in 0 .. 3 do
                                        acc <- Df32.add acc (Df32.mul (Df32.ldEntry offA (rr * 4 + t)) (Df32.ldEntry offB (t * 4 + c)))
                                    p.[rr * 4 + c] <- acc
                            for rr in 0 .. 3 do
                                for c in 0 .. 3 do
                                    let mutable acc = V2f(0.0f, 0.0f)
                                    for t in 0 .. 3 do
                                        acc <- Df32.add acc (Df32.mul p.[rr * 4 + t] (Df32.ldEntry offC (t * 4 + c)))
                                    uniform.HeapUni.[outOff + rr * 4 + c] <- Df32.collapse acc
                        | _ ->
                            // NormalMatrix = transpose(A) upper-3x3.  out[i*3+j] = A[j,i].
                            for i in 0 .. 2 do
                                for j in 0 .. 2 do
                                    uniform.HeapUni.[outOff + i * 3 + j] <- Df32.collapse (Df32.ldEntry offA (j * 4 + i))
            }

    /// Persistent state of ONE bucket — host OR bindless (vertex-pull) geometry,
    /// untextured / bindless-textured / atlas-textured, instanced or not. The
    /// geometry class and instanced-ness are part of the bucket key, so each
    /// bucket's strategy and slot routing are fixed at creation. Set-membership
    /// changes mutate slots / regions / packed geometry / texture tables IN PLACE
    /// (O(changed)) instead of rebuilding the bucket, and the bucket's
    /// RenderObject is created ONCE so its identity is stable across changes (the
    /// render task never recompiles it; only its indirect / header / geometry /
    /// texture resources update).
    /// ONE PAGE of the store: a self-contained mini-arena (the data buffer + word allocator +
    /// the per-source dedup maps for per-draw uniforms / single-value attributes / derived
    /// constituents). A region NEVER spans pages, and a slot's whole group lives on one page,
    /// so a page's draw binds exactly this `Arena` and gathers page-LOCAL offsets — the
    /// original single-buffer gather, unchanged (no switch, no bindless). Dedup is WITHIN a
    /// page (a uniform shared across pages is duplicated — cheap; co-location refinement later).
    /// One arena resident to re-seat during page compaction: its current offset (the
    /// sort key — residents re-alloc front-to-back so moves never overlap), the words
    /// to re-allocate (the original BLOCK size, preserving any 8-byte-alignment
    /// slack), whether the content starts 8-byte-aligned within the block, and the
    /// reseat callback receiving (alignedNewOffset, newBlock).
    type internal Resident = (struct (int * int * bool * (int -> HeapBlock -> unit)))

    /// One bucket's stake in a page (shared storage: several buckets — possibly from
    /// several heaps — allocate from the same page). Compaction must move EVERY
    /// resident and fix EVERY consumer, so each participating bucket contributes:
    ///   * its per-slot arena blocks that live outside the page dicts (derived-uniform
    ///     OUTPUT regions, chain-fold Model constituents), and
    ///   * a header rewrite that re-bakes all of its slots' header cells from the
    ///     (now re-seated) dict entries and marks its header mirror dirty.
    type internal IPageParticipant =
        /// append the bucket's per-slot residents living on page `page`
        abstract CollectResidents : page : int * residents : System.Collections.Generic.List<Resident> -> unit
        /// re-bake every slot's header cells (regions/attrs/index/constituents) and
        /// mark the bucket's header mirror fully dirty
        abstract RewriteHeaders : unit -> unit

    type internal PageArena(runtime : IRuntime) =
        let arena = HeapArena(runtime, 1024)
        let arenaAlloc = HeapSpace()
        // page-scoped fixed initial capacities: pages are GB-scale, a 16k-slot
        // table is noise — and it kills bulk ingest's resize/rehash churn.
        let regions = System.Collections.Generic.Dictionary<IAdaptiveValue, RegionEntry>(1 <<< 14, HashIdentity.Reference)
        let singleRegions = System.Collections.Generic.Dictionary<IAdaptiveValue, RegionEntry>(1 <<< 10, HashIdentity.Reference)
        let constituentsF = System.Collections.Generic.Dictionary<IAdaptiveValue, RegionEntry>(1 <<< 14, HashIdentity.Reference)
        let constituentsB = System.Collections.Generic.Dictionary<IAdaptiveValue, RegionEntry>(1 <<< 14, HashIdentity.Reference)
        // canonical aval per (inv, VALUE) for CONSTANT constituents: per-part
        // `AVal.constant t` wrappers of the SAME trafo value must dedup to ONE
        // constituent region (and, downstream, ONE derived output region).
        let constituentsCanon = System.Collections.Generic.Dictionary<struct(bool * obj), IAdaptiveValue>()
        // geometry static-attribute + index dedup (by value-level source identity, byte offset,
        // typeId) — MUST be per-page: a shared mesh's attrs live in THIS page's arena, so a slot on
        // another page can't reference them (it binds its own arena). Cross-page = duplicated.
        let geomKeyComparer =
            { new System.Collections.Generic.IEqualityComparer<struct(obj * int * int)> with
                member _.GetHashCode(struct(o, i, t)) =
                    System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode o ^^^ (i * 0x9E3779B1) ^^^ (t * 0x85EBCA6B)
                member _.Equals(struct(a, ai, at), struct(b, bi, bt)) =
                    System.Object.ReferenceEquals(a, b) && ai = bi && at = bt }
        let attrStatic = System.Collections.Generic.Dictionary<struct(obj * int * int), StaticEntry>(1 <<< 15, geomKeyComparer)
        let idxStatic  = System.Collections.Generic.Dictionary<struct(obj * int * int), StaticEntry>(1 <<< 14, geomKeyComparer)
        // the buckets currently allocating from / rendering this page
        let participants = System.Collections.Generic.HashSet<IPageParticipant>(HashIdentity.Reference)
        member _.Arena = arena
        member _.ArenaAlloc = arenaAlloc
        member _.Regions = regions
        member _.SingleRegions = singleRegions
        member _.ConstituentsF = constituentsF
        member _.ConstituentsB = constituentsB
        member _.ConstituentsCanon = constituentsCanon
        member _.AttrStatic = attrStatic
        member _.IdxStatic = idxStatic
        member _.Register(p : IPageParticipant) = participants.Add p |> ignore
        member _.Unregister(p : IPageParticipant) = participants.Remove p |> ignore

        /// Threshold-triggered arena compaction of THIS page. Collects EVERY resident —
        /// the page dicts (uniform-field regions, singleton attributes, derived
        /// CONSTITUENTS, static attribute/index allocations) plus every participant's
        /// per-slot blocks (derived outputs, chain folds) — re-allocates them tightly
        /// in ascending old-offset order (front-to-back memmove, no overlap), preserves
        /// 8-byte alignment of double content, then has every participant re-bake its
        /// header cells. One full [0, live) re-upload on the arena's next Compute.
        member _.Compact(pageIdx : int) =
            let res = System.Collections.Generic.List<Resident>()
            let inline entryAligned (e : RegionEntry) = int e.Block.Size > e.Size   // over-allocated ⇔ 8-byte-aligned content
            let addRegion (e : RegionEntry) =
                let ee = e
                res.Add(struct(ee.Offset, int ee.Block.Size, entryAligned ee, fun off b ->
                    ee.Offset <- off; ee.Block <- b
                    if not (isNull ee.Writer) then ee.Writer.Off <- off + ee.HeaderWords))
            for KeyValue(_, e) in regions do addRegion e
            for KeyValue(_, e) in singleRegions do addRegion e
            for KeyValue(_, e) in constituentsF do addRegion e
            for KeyValue(_, e) in constituentsB do addRegion e
            for KeyValue(_, e) in attrStatic do
                let ee = e
                res.Add(struct(ee.Ref, ee.SizeF, false, fun off b -> ee.Ref <- off; ee.Block <- b))
            for KeyValue(_, e) in idxStatic do
                let ee = e
                res.Add(struct(ee.Ref, ee.SizeF, false, fun off b -> ee.Ref <- off; ee.Block <- b))
            for p in participants do p.CollectResidents(pageIdx, res)
            res.Sort(fun (struct(a, _, _, _)) (struct(b, _, _, _)) -> compare a b)
            arenaAlloc.Reset()
            // collect the (oldOff, newOff, contentWords) moves and queue them as ONE
            // ordered op on the arena: the device-side bounce copy runs on the next
            // Compute, AFTER uploads staged before this compaction and BEFORE any
            // staged after it.
            let moves = System.Collections.Generic.List<struct(int * int * int)>(res.Count)
            for (struct(oldOff, words, align8, reseat)) in res do
                let b = arenaAlloc.Alloc words
                let raw = int b.Offset
                let off = if align8 && (raw &&& 1) = 1 then raw + 1 else raw
                let contentWords = words - (if align8 then 1 else 0)
                if off <> oldOff then moves.Add(struct(oldOff, off, contentWords))
                reseat off b
            arena.QueueMoves(moves.ToArray())
            for p in participants do p.RewriteHeaders()
            arena.ShrinkFloats arenaAlloc.Extent
            compactionCount <- compactionCount + 1

        /// trigger check (cheap integer compares); run after removals.
        member x.MaybeCompact(pageIdx : int) =
            if arenaAlloc.Live * 2 < arenaAlloc.Extent
               && int64 arenaAlloc.Waste * 4L > int64 compactionWasteFloorBytes then
                if EditProf.enabled then Log.line "[editprof] COMPACT page=%d live=%d extent=%d waste=%d" pageIdx arenaAlloc.Live arenaAlloc.Extent arenaAlloc.Waste
                x.Compact pageIdx

    /// Shader-AGNOSTIC, PAGED, SHAREABLE data store: ≤ a handful of page arenas, each
    /// one ≤ pageWords storage buffer. A slot group is placed wholly on one page; when
    /// the current fill page would exceed pageWords the store rolls to a new page.
    ///
    /// ONE storage serves MANY buckets and MANY heaps (`Heap.ofRenderObjects storage …`
    /// called once per pass — e.g. the main render and a shadow pass — over the same or
    /// different objects): all their allocations dedup by aval / value-level source in
    /// the shared pages, so shared geometry and uniforms live in GPU memory ONCE.
    /// Lifetime: the storage belongs to the CALLER; a heap teardown releases exactly
    /// its own ref-counts and never clears the store.
    ///
    /// `pageSizeInBytes` (default 2³⁰ = 1 GiB: keeps off*4 int32-safe, the staging
    /// < 2 GB and the SSBO < 4 GB) is clamped to the device's storage-buffer range and
    /// overridable via HEAP_PAGE_WORDS (words) for testing the multi-page path.
    type HeapStorage(runtime : IRuntime, pageSizeInBytes : int64) =
        let pageWords =
            let want =
                match System.Environment.GetEnvironmentVariable "HEAP_PAGE_WORDS" with
                | null | "" -> int64 (max 4096L pageSizeInBytes / 4L)
                | s -> match System.Int64.TryParse s with
                       | true, v when v >= 1024L -> v
                       | _ -> int64 (max 4096L pageSizeInBytes / 4L)
            // CLAMP to what a storage-buffer binding can address on THIS device: a page
            // is bound as one SSBO, so pageWords*4 must fit maxStorageBufferRange. On
            // MoltenVK/Metal and mobile that can be far below the 1 GiB desktop default.
            let deviceWords = runtime.MaxStorageBufferBytes / 4L
            max 1024 (int (min want deviceWords))
        let pages = System.Collections.Generic.List<PageArena>()
        do pages.Add(PageArena(runtime))
        new (runtime : IRuntime) = HeapStorage(runtime, 1L <<< 30)
        member _.Runtime = runtime
        member internal _.PageWords = pageWords
        member internal _.Count = pages.Count
        member internal _.Page(i : int) = pages.[i]
        /// index of the current fill page (the last one)
        member internal _.CurrentPage = pages.Count - 1
        /// the page index a slot needing ~`words` MORE words should use: the current fill page,
        /// or a fresh page if adding `words` would push it past pageWords. (≥1 page always.)
        member internal _.PlacePage(words : int) : int =
            let cur = pages.Count - 1
            if pages.[cur].ArenaAlloc.Extent + (max 0 words) > pageWords && pages.[cur].ArenaAlloc.Extent > 0 then
                pages.Add(PageArena(runtime)); pages.Count - 1
            else cur
        /// compaction trigger over all pages; run by buckets after removals.
        member internal _.MaybeCompact() =
            for i in 0 .. pages.Count - 1 do pages.[i].MaybeCompact i
        /// drop a participant from every page (bucket disposal)
        member internal _.Unregister(p : IPageParticipant) =
            for i in 0 .. pages.Count - 1 do pages.[i].Unregister p

    type internal IncrementalBucket(runtime : IRuntime, storage : HeapStorage, names : string[], nameToField : Map<string, int>,
                                    effect : Effect, ro0 : RenderObject, updater : aval<int>,
                                    useBindlessGeom : bool, instanced : bool,
                                    // the bucket KEY: ALL keyed pipeline state resolved to VALUES —
                                    // baked constant onto the bucket RO, so nothing pipeline-related
                                    // is ever inherited from a member
                                    pipeKey : BucketKey,
                                    // signature color attachments the bucket's effect does NOT write:
                                    // explicitly write-masked off on the bucket RO
                                    maskedAttachments : Symbol[],
                                    // GPU trafo-chain mode: the members expose a "ModelTrafoStack"
                                    // uniform (the UNFOLDED root->leaf link array). Each slot's
                                    // ModelTrafo is composed on the GPU from a GROWABLE, deduped
                                    // link arena (constants by value, dynamics by identity) and the
                                    // ModelTrafo gather reads the per-slot chainOut buffer instead of
                                    // an arena region. "ModelTrafo" is NOT in `names` in this mode.
                                    chainMode : bool,
                                    // PICKING: gates ALL per-slot pick-id machinery (the AddInternal
                                    // capture, the pickIdBuf flush + exposure). Only true when the heap
                                    // is entered via `ofRenderObjectsPicking` (the dom heap node) — a
                                    // non-dom / non-picking heap pays nothing.
                                    picking : bool,
                                    // PICKING: is THIS bucket a pick bucket, known BY CONSTRUCTION (its ROs
                                    // carry HeapNode's `HeapPickId` marker uniform)? Only a pick bucket may
                                    // advertise IsPickable — the dom routes those into the PickId pass. A
                                    // non-pick bucket (e.g. a `Sg.NoEvents` sub-scene that never got the
                                    // pick chain) routed there would trip the backend into forcing a
                                    // `PickId` passthrough (phantom vertex input) → "could not get
                                    // attribute 'PickId'"; it goes to the plain pass instead.
                                    pickable : bool,
                                    // PICKING: invoked with a slot's pick id when that slot is freed,
                                    // so the dom side releases the id (ref-counted). No-op off picking.
                                    deregister : int -> unit) =
        let fieldStride = names.Length
        // df32 (two-f32) precision path vs real fp64 — chosen ONCE for the whole
        // bucket from the runtime (forced override OR no shaderFloat64). Threaded
        // into the constituent packers (hi/lo split vs IEEE double bytes), the chain
        // link arena (V2f layout vs M44d), and the compute-kernel selection so the
        // CPU byte layout always matches the kernel that reads it.
        let df32 = useDf32 runtime
        // ── derived-uniform compute plan (wombat §7, fp64) ───────────────
        // Shader-consumed names that are standard recipes are COMPUTE OUTPUTS:
        // produced once per slot in fp64 by `Derived.composeDerived` and gathered
        // like any field. Their constituents (Model/View/Proj, forward/backward)
        // are arena M44d regions placed in each slot's header AFTER the field cells
        // (cells [fieldStride, fieldStride+numConst)); Model is per-slot (chain-
        // folded in chainMode, else uploaded from the RO's ModelTrafo), View/Proj
        // are ref-counted by aval (shared → one slot, a camera move marks one
        // region → O(1)). Records are static per bucket: [arm; outCell; in0; in1;
        // in2] over header CELLS (REC_STRIDE = 5), the kernel reads each input's
        // per-slot arena offset from the slot's header at that cell.
        // a base trafo is available for derivation if the RO supplies it in a TRAFO
        // type (Trafo3d/M44d/M44f — packable into the fp64 constituent), or (for Model)
        // exposes the unfolded ModelTrafoStack (chainMode fold). A consumed name
        // supplied in some OTHER type (e.g. a V2i) is NOT a constituent — the recipe
        // simply isn't derivable and the name falls back to a plain/diagnosed field.
        let isTrafoSupply (t : System.Type) = t = typeof<Trafo3d> || t = typeof<M44d> || t = typeof<M44f>
        let ro0BaseSupplied (b : string) =
            (match ro0.Uniforms.TryGetUniform(Ag.Scope.Root, cachedSym b) with ValueSome v -> isTrafoSupply v.ContentType | _ -> false)
            || (b = Derived.MBASE &&
                (match ro0.Uniforms.TryGetUniform(Ag.Scope.Root, cachedSym "ModelTrafoStack") with ValueSome _ -> true | _ -> false))
        let derivedPlan =                       // (fieldCell, arm, constituents in mul order)
            names |> Array.mapi (fun i n -> i, n)
                  |> Array.choose (fun (i, n) ->
                      // pick the first recipe alternative whose constituents are all
                      // available; otherwise (e.g. a combined ViewProjTrafo supplied
                      // without View/Proj AND no VP·M fallback applies) it stays a plain
                      // direct field gathering the supplied value — derive when we can,
                      // never crash.
                      match Derived.pickRule ro0BaseSupplied n with
                      | Some (arm, op) -> Some (i, arm, Derived.constituentsOf op |> List.toArray)
                      | None -> None)
        let neededConstituents =                // distinct (base,inv), stable first-seen order
            derivedPlan |> Array.collect (fun (_, _, cs) -> cs) |> Array.distinct
        let constCell : System.Collections.Generic.IDictionary<Constituent, int> =
            neededConstituents |> Array.mapi (fun k c -> c, fieldStride + k) |> dict
        let numConst = neededConstituents.Length
        let hasDerived = derivedPlan.Length > 0
        // Model forward / backward constituent cells (chainMode fold targets); -1 if
        // no consumed recipe needs that half.
        let cellOf (c : Constituent) = match constCell.TryGetValue c with | true, k -> k | _ -> -1
        let modelFwdCell = cellOf (Derived.fwd Derived.MBASE)
        let modelBwdCell = cellOf (Derived.bwd Derived.MBASE)
        // static records: one per derived output, packed [arm; outCell; in0..in2].
        let derivedRecords =
            derivedPlan |> Array.collect (fun (fieldCell, arm, cs) ->
                let cell k = if k < cs.Length then constCell.[cs.[k]] else constCell.[cs.[0]]
                [| arm; fieldCell; cell 0; cell 1; cell 2 |])
        let numDerivedRecords = derivedPlan.Length
        let derivedCells = derivedPlan |> Array.map (fun (c, _, _) -> c) |> Set.ofArray
        let scope = Ag.Scope.Root
        let symData = Symbol.Create "HeapData"
        let symPickIds = Symbol.Create "HeapPickIds"
        let symDataI = Symbol.Create "HeapDataI"
        let symDataD = Symbol.Create "HeapDataD"   // native double view of the arena (fp64-requested uniforms)
        let symHeaders = Symbol.Create "HeapHeaders"
        let symModelStack = Symbol.Create "ModelTrafoStack"
        let nameSyms = names |> Array.map Symbol.Create
        let heapSyms = System.Collections.Generic.HashSet<Symbol>(nameSyms)

        // ── sampler structure (a function of the EFFECT + runtime, not of the
        //    membership — every member shares the effect via the bucket key) ──
        let samplers = samplerUniforms effect           // (name, texName, type, state)[]
        // USER-managed unbounded sampler ARRAYS (e.g. `Sampler2d[] Textures`, indexed
        // by a per-draw HeapTexIndex) — distinct from the heap's auto-bindless of
        // single per-object samplers. They are NOT per-draw fields and NOT rewritten;
        // the heap simply binds the RO-supplied shared array through (bucket-
        // homogeneous), the per-draw index being an ordinary gathered field.
        let samplerNameSet = samplers |> Array.map (fun (n, _, _, _) -> n) |> Set.ofArray
        // user-managed unbounded sampler ARRAYS (e.g. `Sampler2d[] textures` from
        // `textureArray uniform?Textures`, indexed in-shader by a per-draw field) that
        // the per-object bindless path did NOT claim. Returns the TEXTURE SEMANTIC
        // ("Textures") — the name the render binds and the RO supplies — NOT the FShade
        // value name ("textures").
        let userSamplerArrays =
            effect.Uniforms |> Map.toArray
            |> Array.choose (fun (n, p) ->
                match p.uniformValue with
                | UniformValue.SamplerArray arr when arr.Length > 0 && not (samplerNameSet.Contains n) -> Some (fst arr.[0])
                | _ -> None)
            |> Array.distinct
        // Each input sampler gets a GLOBAL index si (its position) and its OWN generated
        // array "HeapTexArr<si>" + index "HeapTexIdx<si>" carrying its own state.
        let samplersIndexed = samplers |> Array.mapi (fun si (sn, tn, ty, st) -> si, sn, tn, ty, st)
        // samplerName -> (type, si), for the read rewrite
        let samplerByName =
            samplersIndexed |> Array.map (fun (si, sn, _, ty, _) -> sn, (ty, si)) |> Map.ofArray
        // generated array uniform name -> its sampler's state (applied by overrideSamplerStates)
        let samplerStateOverrides =
            samplersIndexed |> Array.map (fun (si, _, _, _, st) -> heapTexArrName si, st) |> Map.ofArray
        // the bucket provider must pass through neither the sampler binding names
        // nor the texture uniform names — both are folded into the per-type arrays.
        let samplerSyms = System.Collections.Generic.HashSet<Symbol>(samplers |> Array.collect (fun (sn, tn, _, _) -> [| Symbol.Create sn; Symbol.Create tn |]))
        // atlas fallback (Vulkan-1.0 / GL / MoltenVK: ONE sampler) — used when
        // descriptor-indexed sampler arrays are unavailable (or forced) and EVERY
        // sampler is a Sampler2d (guaranteed heapable only then, see isHeapable).
        let atlas2d = samplers |> Array.filter (fun (_, _, ty, _) -> ty = typeof<Sampler2d>)
        let useAtlas =
            atlas2d.Length > 0 && atlas2d.Length = samplers.Length &&
            (forceAtlas || not runtime.SupportsUnboundedSamplerArrays)
        let atlasK = atlas2d.Length
        let atlasByName = if useAtlas then atlas2d |> Array.mapi (fun kt (sn, _, _, _) -> sn, (kt, atlasK)) |> Map.ofArray else Map.empty
        let atlasTexSyms = atlas2d |> Array.map (fun (_, tn, _, _) -> Symbol.Create tn)

        // ── per-draw slot routing. Three paths (buildBucket's decision logic,
        //    now STRUCTURAL — instanced-ness is part of the bucket key):
        //      (a) gl_DrawID — GL (gl_InstanceID omits baseInstance there), and
        //          Vulkan-with-drawid for INSTANCED buckets (FirstInstance must
        //          stay 0 so gl_InstanceIndex is the draw-LOCAL instance index).
        //      (b) gl_InstanceIndex + per-draw FirstInstance = slot — Vulkan
        //          fast path for non-instanced buckets (incl. MoltenVK).
        //      (c) per-instance HeapSlotAttr vertex attribute — MoltenVK fallback
        //          for instanced buckets (MSL has no DrawIndex).
        //    gl_DrawID counts draw RECORDS — including InstanceCount=0 tombstones —
        //    so slot = record index stays correct on all paths.
        let isGL = runtime.GetType().FullName.Contains("Aardvark.Rendering.GL")
        let drawIdWorks = runtime.SupportsMultiDrawIndirectDrawId && not forceNoDrawId
        let useDrawId = (isGL || instanced) && drawIdWorks
        let useSlotAttr = instanced && not drawIdWorks
        //  (d) CLUSTERED (Vulkan/MoltenVK non-instanced host TriangleList buckets):
        //      slots grouped into padded SIZE CLASSES, one INSTANCED record per
        //      (page, class) — gl_InstanceIndex indexes the HeapClassSlots SSBO.
        //      Small records starve warp residency on latency-bound gathers
        //      (probe: n-records vs 1-instanced = 1.36x with the real shader);
        //      the class record restores it. Slots above the class cap keep an
        //      exact per-slot record THROUGH the same ClassSlots indirection.
        let useClusters =
            not DisableClusters && not isGL && not instanced && not useBindlessGeom
            && pipeKey.Topology = IndexedGeometryMode.TriangleList
        let symSlotAttr = Symbol.Create "HeapSlotAttr"
        let symClassSlots = Symbol.Create "HeapClassSlots"
        let slotVar = Var("heapSlot", typeof<int>)
        let slotE : Expr<int> =
            if useClusters then Expr.Cast (Expr.Var slotVar)
            elif useDrawId then <@ getDrawId() @>
            elif useSlotAttr then Expr.ReadInput<int>(ParameterKind.Input, "HeapSlotAttr")
            else Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.InstanceId)

        // consumed-attribute table: (ai, name, sym, elementType, elemSize,
        // strideF, offF). For HOST buckets only ai/name/sym matter — both the
        // attribute ELEMENT TYPES and the index element type are decoded PER
        // ALLOCATION via their headers' typeIds, so neither is part of the
        // bucket key (ro0's et/es here are informational). elementType/strideF/
        // offF (from ro0's BufferViews) are used by the BINDLESS vertex-pull
        // gather only (baked into its shader and folded into the bindless
        // bucket key).
        let attrInfos =
            effect.Inputs |> Map.toArray
            |> Array.mapi (fun ai (name, _) ->
                let sym = Symbol.Create name
                match ro0.VertexAttributes.TryGetAttribute sym with
                | ValueSome bv ->
                    let es = elemSize bv.ElementType
                    ai, name, sym, bv.ElementType, es, (if bv.Stride = 0 then es else bv.Stride) / 4, bv.Offset / 4
                | ValueNone -> failwithf "Heap.ofRenderObjects: RO missing shader input attribute %A" sym)
        let numAttrs = attrInfos.Length

        // ── per-slot header table layout: [0, fieldStride) per-draw uniform
        //    region offsets, then (host buckets only) one allocation REF per
        //    consumed attribute, then ONE index-allocation ref. ──
        // header cells: [0,fieldStride) per-draw field region offsets (incl. derived
        // OUTPUT regions), then [fieldStride, fieldStride+numConst) derived-uniform
        // CONSTITUENT region offsets (Model/View/Proj fwd/bwd), then one allocation
        // REF per consumed attribute (host buckets), then ONE index-allocation ref.
        let attrBase = fieldStride + numConst
        let attrCells = if useBindlessGeom then 0 else numAttrs
        // + idx cell + vc cell (drawn-vertex count: the CLUSTER clamp kills padding
        // lanes via min(gl_VertexIndex, vc-1) — whole degenerate triangles)
        // + derive-OWNERSHIP mask cell (LAST cell, shaders read hstride-1): bit j
        // set = this slot computes derived output j (deduped outputs are computed
        // by exactly one member slot)
        let headerStride = attrBase + attrCells + 3
        let idxCell = attrBase + attrCells
        let vcCell = idxCell + 1
        let ownCell = vcCell + 1

        // ── arena: deduped per-draw uniform regions, refcounted, placed by a
        //    coalescing range allocator (float units) — now held by the (per-bucket) storage ──
        let mutable arena = storage.Page(0).Arena
        do arena.AddDependency (updater :> IAdaptiveValue)
        let mutable regions = storage.Page(0).Regions
        let mutable arenaAlloc = storage.Page(0).ArenaAlloc

        // ── geometry. EVERYTHING host-readable lives as ALLOCATIONS in the
        //    bucket's storage arena (the same HeapData buffer the per-draw
        //    uniform regions live in): per-attribute byte ranges, singleton
        //    (SingleValueBuffer) attribute values and index ranges, each with a
        //    4-word header (typeId/length/strideBytes/pad, wombat parity). The
        //    fixed-function vertex input path is GONE — the rewritten vertex
        //    shader storage-decodes attributes AND indices (draw records are
        //    NON-indexed; vertexCount = index count). Allocations are
        //    REFCOUNTED and deduped by source identity; freed ranges return to
        //    the coalescing arena allocator; residual fragmentation is bounded
        //    by threshold-triggered compaction (maybeCompact).
        //    Bindless buckets keep the per-object SSBO descriptor array for the
        //    VERTEX data (GPU-resident buffers are bound zero-copy, never
        //    downloaded); only their INDEX bytes are arena allocations. ──
        // dedup keys are (VALUE-level source, byte offset, format typeId): the
        // source is the UNDERLYING ARRAY for constant ArrayBuffer-backed views
        // (geomDedupSource — fresh per-leaf BufferView/aval wrappers around one
        // shared array dedup), the buffer aval otherwise. A hand-rolled comparer
        // (reference hash + ints) avoids the generic structural-hashing path that
        // showed up in the per-add/remove cost of churn profiles.
        // PER-PAGE now (held by PageArena): geometry attrs/indices dedup WITHIN a page so a
        // slot's mesh is in the page its draw binds. Mutable currents, set by setPage.
        let mutable attrStatic = storage.Page(0).AttrStatic
        let mutable idxStatic  = storage.Page(0).IdxStatic
        // singleton-attribute regions (adaptive, header + RegionWriter at ref+4),
        // deduped by the inner value aval — DISTINCT from `regions` (a uniform
        // field and a singleton attribute sharing an aval would need different
        // layouts).
        let mutable singleRegions = storage.Page(0).SingleRegions
        // bindless: per-(slot, attribute) source buffer avals + the last buffer
        // each position yielded. Tombstoned slots null their aval but KEEP the
        // last buffer — never read (their draw record is InstanceCount = 0), but
        // the SSBO array cell must stay bound to a live buffer.
        let mutable vtxAvals : aval<IBuffer>[] = if useBindlessGeom then Array.zeroCreate (16 * max 1 numAttrs) else [||]
        let mutable vtxLast : IBuffer[] = if useBindlessGeom then Array.zeroCreate (16 * max 1 numAttrs) else [||]
        // INCREMENTAL vertex-pull gather state: the output SSBO-array is refreshed
        // ONLY at positions whose slot was added/removed since the last pull
        // (vtxStructDirty) plus positions whose source buffer aval is non-constant
        // (vtxDynPos, re-read every pull). A churn of r slots then refreshes the
        // gather in O(r) — the previous code re-scanned ALL highWater*numAttrs
        // positions and allocated a fresh array on EVERY membership transaction
        // (O(N)-per-structural-tx for bindless buckets with distinct per-slot
        // buffers; the loop was iterations-O(N) even when each cell was cheap).
        let vtxStructDirty = if useBindlessGeom then System.Collections.Generic.HashSet<int>() else null
        let vtxDynPos = if useBindlessGeom then System.Collections.Generic.HashSet<int>() else null
        // value-dirty gather positions: a dynamic source buffer's handle changed.
        // RECORDED by vtxGatherAval.InputChangedObject — which runs OUTSIDE the object
        // lock (rule: InputChanged is unsynchronised) — so this set carries its OWN
        // monitor (`lock vtxValDirty …`). vtxPosOf maps a tracked (non-constant) buffer
        // aval → its gather position (1:1; bindless buckets are distinct-per-slot, decl
        // above). The pull then drains vtxValDirty O(changed) instead of re-reading all
        // of vtxDynPos every pull — only the buffers that actually marked us are re-pulled
        // (re-establishing their consumed edges); the rest keep their edges untouched.
        let vtxValDirty = if useBindlessGeom then System.Collections.Generic.HashSet<int>() else null
        let vtxPosOf = if useBindlessGeom then System.Collections.Generic.Dictionary<IAdaptiveObject, int>(HashIdentity.Reference) else null
        let mutable vtxOut : IBuffer[] = if useBindlessGeom then Array.zeroCreate (max 1 (16 * max 1 numAttrs)) else [||]
        let mutable vtxOutHighWater = -1

        // ── MoltenVK instanced fallback: growable per-instance slot buffer.
        //    The draw of slot s with K instances owns instData[off .. off+K)
        //    (each element = s) and gets FirstInstance = off, so Metal's
        //    [[base_instance]] offsets the per-instance fetch. Ranges come from
        //    a coalescing allocator (freed neighbors merge, a bigger free range
        //    is split for a smaller request); residual drift is bounded by
        //    threshold compaction (maybeCompact). ──
        let mutable instData : int[] = Array.zeroCreate 16
        let instAlloc = HeapSpace()                     // units: instances (ints)

        // ── dirty tracking for the stable mirror buffers (draw records /
        //    headers / slot attributes): the delta pass records WHAT changed,
        //    the flushes below upload exactly those sub-ranges; compactions
        //    request a full re-stage instead. ──
        let dirtyDraws = System.Collections.Generic.HashSet<int>()      // slots
        let mutable drawsAllDirty = false
        let dirtyHeaders = System.Collections.Generic.HashSet<int>()    // slots
        let mutable headersAllDirty = false
        let instDirty = System.Collections.Generic.List<struct(int * int)>()  // [start, end) int ranges
        let mutable instAllDirty = false

        let allocInst (slot : int) (k : int) : HeapBlock =
            let b = instAlloc.Alloc k
            if instAlloc.Extent > instData.Length then
                let n = Fun.NextPowerOfTwo instAlloc.Extent
                let nd = Array.zeroCreate<int> n
                System.Array.Copy(instData, nd, instData.Length)
                instData <- nd
            let off = int b.Offset
            for i in 0 .. k - 1 do instData.[off + i] <- slot
            instDirty.Add(struct(off, off + k))
            b
        let freeInst (b : HeapBlock) =
            instAlloc.Free b

        // ── GPU trafo-chain state (chainMode only) ───────────────────────
        // chainLinks: growable deduped fp64 link arena (one slot per distinct
        //   link, constants value-keyed / dynamics identity-keyed).
        // chIdx: slot-major linkIdx — each slot owns a contiguous run [off,off+len)
        //   of link slot indices, allocated from a coalescing free-list (chIdxAlloc)
        //   and re-used on remove. chOffset/chLen are per-draw-slot.
        // chainDirtyStruct: draw-slots whose chain STRUCTURE (offset/len/idx)
        //   changed and must re-upload before the next dispatch.
        let chainLinks = if chainMode then GrowChainLinks(runtime, df32) else Unchecked.defaultof<GrowChainLinks>
        let mutable chOffset : int[] = if chainMode then Array.zeroCreate 16 else [||]
        let mutable chLen    : int[] = if chainMode then Array.zeroCreate 16 else [||]
        let mutable chIdx    : int[] = if chainMode then Array.zeroCreate 16 else [||]
        let chIdxAlloc = if chainMode then HeapSpace() else Unchecked.defaultof<HeapSpace>
        let chainLinkKeys = if chainMode then System.Collections.Generic.Dictionary<int, aval<Trafo3d>[]>() else null
        let chainBlocks = if chainMode then System.Collections.Generic.Dictionary<int, HeapBlock>() else null
        let chainDirtyStruct = if chainMode then System.Collections.Generic.HashSet<int>() else null
        let mutable chainStructAllDirty = false

        // ── draw records + headers: slot-indexed, growable, free-listed ──
        let mutable entries : DrawCallInfo[] = Array.zeroCreate 16
        let mutable headers : int[] = Array.zeroCreate (16 * headerStride)
        // PAGED: which storage page each slot's group lives on (parallel to `entries`); a
        // page's sub-draw renders only its slots (others get a 0-instance record).
        let mutable slotPage : int[] = Array.zeroCreate 16
        // PICKING: dom-sourced per-slot pick id (parallel to slotPage); set in AddInternal
        // from the RO's "HeapPickId" uniform (-1 = unpickable), flushed to pickIdBuf, exposed
        // as the "HeapPickIds" SSBO read by the dom heap pick-shader via gl_InstanceIndex.
        let mutable pickIds : int[] = Array.zeroCreate 16
        // dirty tracking for slotPage/pickIds (same contract as dirtyHeaders):
        // write sites mark the slot, the flush uploads gap-merged sub-ranges —
        // a content-only updater version must not re-upload highWater*4 bytes.
        let slotPageDirty = System.Collections.Generic.HashSet<int>()   // slots
        let mutable slotPageAllDirty = false
        let pickIdsDirty = System.Collections.Generic.HashSet<int>()    // slots
        let mutable pickIdsAllDirty = false
        // ── TYPED-ASSIGNMENT PARTITIONS (ai/HEAP-TYPED-ASSIGNMENTS-PLAN.md §2):
        //    a slot's per-field source tids + index tid pack into an int64
        //    assignment KEY (6 bits/field, 9 fields). Partition 0 = DYNAMIC
        //    (unspecialized pipeline, always present): the STAGING area for
        //    freshly-added slots and the permanent home of the long tail.
        //    Assignments MATERIALIZE into their own partition (typed pipeline
        //    via constant spec values) when their population crosses
        //    `materializeAt`; hysteresis at `dematerializeAt` avoids flapping.
        //    Slots migrate via O(1) cluster-style membership moves inside the
        //    updater. The async pipeline resource renders a not-yet-compiled
        //    typed pipeline through the generic handle, so migration timing
        //    needs NO coupling to pipeline state — never a wrong pixel. ──
        let materializeAt = 64
        let dematerializeAt = 16
        // AARDVARK_HEAP_NO_SPEC=1: bisect switch — never materialize typed
        // partitions, everything renders through the dynamic pipeline.
        let noSpec = System.Environment.GetEnvironmentVariable "AARDVARK_HEAP_NO_SPEC" = "1"
        // per-field assignment value: 6-bit tid + a 2-bit EXTENT class in bits
        // 6-7 (0 = runtime clamp against the header length, 1 = FULL folds
        // e = vid, 2 = SINGLETON folds e = 0). The full vector no longer fits
        // an int64 bit-pack, so assignment keys are INTERNED vector ids.
        let internTable = System.Collections.Generic.Dictionary<struct(int64 * int64), int64>()
        let internAssign (fieldVals : int[]) (idxTid : int) : int64 =
            let mutable k1 = 0L
            for i in 0 .. min 7 (fieldVals.Length - 1) do
                k1 <- k1 ||| (int64 (fieldVals.[i] &&& 0xFF) <<< (8 * i))
            if k1 = 0L && idxTid = 0 then 0L
            else
                let kk = struct(k1, int64 idxTid)
                match internTable.TryGetValue kk with
                | true, id -> id
                | _ ->
                    let id = int64 (internTable.Count + 1)
                    internTable.[kk] <- id
                    id
        let mapOfTids (fieldVals : int[]) (idxTid : int) : Map<string, int> =
            let mutable m = Map.empty
            for i in 0 .. min 7 (fieldVals.Length - 1) do
                if fieldVals.[i] > 0 then m <- Map.add (sprintf "HeapTid%d" i) fieldVals.[i] m
            if idxTid > 0 then m <- Map.add "HeapTidIdx" idxTid m
            m
        /// combine the slot's raw per-field tids + allocation lengths into the
        /// assignment's field values. SINGLETON (length 1) folds e = 0 — always
        /// safe. FULL (length >= drawn count, NON-indexed slots only — indexed
        /// slots address attributes by decoded index values, which only the
        /// runtime clamp can guard) folds e = vid. Everything else keeps the
        /// runtime clamp with the tid still folded. Matrix fields (tid > 40)
        /// never fold extent — their decoders don't consume the spec constant.
        let extendTids (rawTids : int[]) (lens : int[]) (vc : int) (indexed : bool) : int[] =
            let r = Array.copy rawTids
            for i in 0 .. r.Length - 1 do
                if r.[i] > 0 && r.[i] <= 40 then
                    if lens.[i] = 1 then r.[i] <- r.[i] ||| 0x80
                    elif not indexed && lens.[i] >= vc then r.[i] <- r.[i] ||| 0x40
            r
        // partition registry: key -> state. Id 0 is the implicit dynamic partition.
        let partitions = System.Collections.Generic.Dictionary<int64, HeapPartition>()
        let partById = System.Collections.Generic.List<HeapPartition>()      // index = partition id (0 = dynamic sentinel at index 0)
        do partById.Add { Key = 0L; Id = 0; TidMap = Map.empty; Count = 0; Slots = System.Collections.Generic.List<int>(); Materialized = true }
        // epoch bumps on materialize/dematerialize -> HeapRenderObject rebuild
        let mutable partEpoch = 0
        let mutable slotAssign : int64[] = Array.zeroCreate 16    // slot -> assignment key
        let mutable slotPart   : int[]   = Array.zeroCreate 16    // slot -> partition RESIDENCY (0 = dynamic)
        let mutable slotAsgPos : int[]   = Array.zeroCreate 16    // slot -> position in its assignment's slot list
        // raw per-field source tids + allocation lengths (extent reclassification
        // on in-place length changes needs the full picture, not just the key)
        let mutable slotFieldTid : int[] = Array.zeroCreate (16 * 8)
        let mutable slotFieldLen : int[] = Array.zeroCreate (16 * 8)
        let mutable slotIdxTidA  : int[] = Array.zeroCreate 16
        // migration needs classAdd/classRemove (defined below with the cluster
        // state) — wired via this mutable hook to keep definition order simple.
        let mutable relistSlot : int -> int -> unit = fun _ _ -> ()   // slot, newPart
        let migratePartition (p : HeapPartition) (target : int) =
            for i in 0 .. p.Slots.Count - 1 do
                relistSlot p.Slots.[i] target
        let materialize (p : HeapPartition) =
            if not p.Materialized then
                p.Id <- partById.Count
                partById.Add p
                p.Materialized <- true
                migratePartition p p.Id
                partEpoch <- partEpoch + 1
        let dematerialize (p : HeapPartition) =
            if p.Materialized && p.Id > 0 then
                migratePartition p 0
                p.Materialized <- false
                p.Id <- -1
                partEpoch <- partEpoch + 1
        let asgAdd (slot : int) (key : int64) (tidMap : Lazy<Map<string, int>>) =
            slotAssign.[slot] <- key
            if key = 0L then
                slotPart.[slot] <- 0
                slotAsgPos.[slot] <- -1
            else
                let p =
                    match partitions.TryGetValue key with
                    | true, p -> p
                    | _ ->
                        let p = { Key = key; Id = -1; TidMap = tidMap.Value; Count = 0
                                  Slots = System.Collections.Generic.List<int>(); Materialized = false }
                        partitions.[key] <- p
                        p
                slotAsgPos.[slot] <- p.Slots.Count
                p.Slots.Add slot
                p.Count <- p.Count + 1
                slotPart.[slot] <- (if p.Materialized then p.Id else 0)
                if not p.Materialized && p.Count >= materializeAt && useClusters && not noSpec then materialize p
        let asgRemove (slot : int) =
            let key = slotAssign.[slot]
            if key <> 0L then
                match partitions.TryGetValue key with
                | true, p ->
                    let pos = slotAsgPos.[slot]
                    let last = p.Slots.[p.Slots.Count - 1]
                    p.Slots.[pos] <- last
                    slotAsgPos.[last] <- pos
                    p.Slots.RemoveAt(p.Slots.Count - 1)
                    p.Count <- p.Count - 1
                    if p.Materialized && p.Count < dematerializeAt then dematerialize p
                | _ -> ()
            slotAssign.[slot] <- 0L
            slotPart.[slot] <- 0
        // ── CLUSTER state (useClusters): per (page, class) live-slot lists; the
        //    last pseudo-class holds OVERSIZED slots (exact per-slot records).
        //    ClassSlots buffer + records are FULL-REWRITTEN per flush (tiny /
        //    same policy as slotPageBuf); membership ops are O(1) swap-remove. ──
        let numClasses = clusterClassSizes.Length
        // Each (page, class) owns a CAPACITY REGION in the ClassSlots buffer with a
        // STABLE base (records reference it via FirstInstance): membership changes
        // are O(1) single-int writes into the CPU mirror + a dirty range. A full
        // region doubles into fresh space at the cursor (amortized); when leaked
        // space exceeds the live capacity the whole buffer RELAYOUTS (full rewrite,
        // amortized like growth). All mutation happens inside the updater.
        let classLists = System.Collections.Generic.List<System.Collections.Generic.List<int>>()
        let classBase  = System.Collections.Generic.List<int>()
        let classCap   = System.Collections.Generic.List<int>()
        let mutable csCursor = 0                                  // high-water of the region space
        // bumped whenever the CLUSTER RECORD SET can have changed (class membership,
        // region bases, oversized vc): the indirect flushes regenerate records ONLY
        // when this moved — a content-only edit no longer rebuilds+re-uploads every
        // (page,partition) record list each version (was ~0.2 ms per edit).
        let mutable recsVersion = 0
        let mutable csLiveCaps = 0                                // sum of LIVE region capacities
        // INSTANCE-RATE RECORD ROWS: each class-list entry is a ROW of hot
        // per-slot record fields — [slot; vc; idxRef; attrRef0..N-1] — bound as
        // VertexInputRate.Instance attributes (HeapRec0..). The hardware fetches
        // the row at wave launch (address linear in gl_InstanceIndex, applied
        // via the records' FirstInstance): the ClassSlots->record dependent
        // double-hop leaves the shader's critical path entirely. classBase /
        // classCap / csCursor stay in ENTRY units; csStaging is in WORDS.
        let rowAttrs = min 8 numAttrs
        let rowWords = 3 + rowAttrs
        let mutable rowFill : int -> int -> unit = fun _ _ -> ()  // entry, slot (wired below headers)
        let mutable csStaging : int[] = Array.zeroCreate (64 * rowWords)   // authoritative CPU mirror (words)
        let csDirty = System.Collections.Generic.List<struct(int * int)>()
        let mutable csFullDirty = false
        /// sparse-edit ranges only pay off while they stay FEW: bulk ingest (one
        /// range per added slot) must not turn the first flush into a million
        /// upload calls — past the cap, collapse to one full rewrite.
        let csMarkDirty (o : int) (n : int) =
            if not csFullDirty then
                if csDirty.Count >= 2048 then
                    csDirty.Clear()
                    csFullDirty <- true
                else csDirty.Add(struct(o, n))
        // (page, partition) -> base index of its (numClasses+1) class-list block.
        // Blocks allocate on demand; the reverse map drives record generation.
        let classBlockOf = System.Collections.Generic.Dictionary<struct(int * int), int>()
        let classBlocks = System.Collections.Generic.List<struct(int * int)>()   // blockIdx -> (page, part)
        let classIdxOfList (page : int) (part : int) (cls : int) =
            let key = struct(page, part)
            let blockBase =
                match classBlockOf.TryGetValue key with
                | true, b -> b
                | _ ->
                    let b = classLists.Count
                    for _ in 0 .. numClasses do
                        classLists.Add(System.Collections.Generic.List<int>())
                        classBase.Add 0
                        classCap.Add 0
                    classBlockOf.[key] <- b
                    classBlocks.Add key
                    b
            blockBase + cls
        let csEnsureStaging (nEntries : int) =
            if csStaging.Length < nEntries * rowWords then
                let ns = Array.zeroCreate<int> (Fun.NextPowerOfTwo (nEntries * rowWords))
                System.Array.Copy(csStaging, ns, csStaging.Length)
                csStaging <- ns
        /// re-pack every region tightly (drops leaked space); full rewrite
        let csRelayout () =
            let mutable o = 0
            for idx in 0 .. classLists.Count - 1 do
                let l = classLists.[idx]
                let cap = if l.Count = 0 then 0 else Fun.NextPowerOfTwo (max 16 l.Count)
                classBase.[idx] <- o
                classCap.[idx] <- cap
                csEnsureStaging (o + cap)
                for j in 0 .. l.Count - 1 do rowFill (o + j) l.[j]
                o <- o + cap
            csCursor <- o
            csLiveCaps <- o
            csDirty.Clear()
            csFullDirty <- true
            recsVersion <- recsVersion + 1
        /// make room for one more member of region `idx`: opportunistic RELAYOUT when
        /// leaked space dominates, then (still-full regions after a tight relayout
        /// included) grow-and-move to fresh space at the cursor.
        let csEnsureRoom (idx : int) =
            if classLists.[idx].Count >= classCap.[idx] then
                if csCursor > 2 * csLiveCaps + 1024 then csRelayout ()
                let l = classLists.[idx]
                if l.Count >= classCap.[idx] then
                    let newCap = Fun.NextPowerOfTwo (max 16 (l.Count + 1))
                    csLiveCaps <- csLiveCaps - classCap.[idx] + newCap
                    let nb = csCursor
                    csCursor <- csCursor + newCap
                    csEnsureStaging csCursor
                    for j in 0 .. l.Count - 1 do rowFill (nb + j) l.[j]
                    if l.Count > 0 then csMarkDirty (nb * rowWords) (l.Count * rowWords)
                    classBase.[idx] <- nb
                    classCap.[idx] <- newCap
                    recsVersion <- recsVersion + 1
        let mutable clusterClsOf : int[] = Array.create 16 -1     // slot -> class idx (numClasses = oversized; -1 = not listed)
        let mutable clusterPosOf : int[] = Array.zeroCreate 16    // slot -> position in its class list
        let mutable vcOfSlot     : int[] = Array.zeroCreate 16    // slot -> drawn-vertex count
        // wire the row writer (headers/vcOfSlot in scope from here on)
        do rowFill <- fun entry slot ->
            let o = entry * rowWords
            csStaging.[o] <- slot
            csStaging.[o + 1] <- vcOfSlot.[slot]
            csStaging.[o + 2] <- headers.[slot * headerStride + idxCell]
            for i in 0 .. rowAttrs - 1 do
                csStaging.[o + 3 + i] <- headers.[slot * headerStride + attrBase + i]
        let classAdd (slot : int) =
            if clusterClsOf.[slot] < 0 then
                let cls = match clusterClassOf vcOfSlot.[slot] with | -1 -> numClasses | c -> c
                let idx = classIdxOfList slotPage.[slot] slotPart.[slot] cls
                csEnsureRoom idx
                let l = classLists.[idx]
                clusterClsOf.[slot] <- cls
                clusterPosOf.[slot] <- l.Count
                rowFill (classBase.[idx] + l.Count) slot
                csMarkDirty ((classBase.[idx] + l.Count) * rowWords) rowWords
                l.Add slot
                recsVersion <- recsVersion + 1
        let classRemove (slot : int) =
            let cls = clusterClsOf.[slot]
            if cls >= 0 then
                let idx = classIdxOfList slotPage.[slot] slotPart.[slot] cls
                let l = classLists.[idx]
                let pos = clusterPosOf.[slot]
                let last = l.[l.Count - 1]
                l.[pos] <- last
                clusterPosOf.[last] <- pos
                l.RemoveAt(l.Count - 1)
                rowFill (classBase.[idx] + pos) last
                csMarkDirty ((classBase.[idx] + pos) * rowWords) rowWords
                clusterClsOf.[slot] <- -1
                recsVersion <- recsVersion + 1
        /// a listed slot's header cells (attr/idx refs) or vc changed: rewrite
        /// its instance-record row in place (O(1), same dirty-range flush).
        let refreshRow (slot : int) =
            let cls = clusterClsOf.[slot]
            if cls >= 0 then
                let idx = classIdxOfList slotPage.[slot] slotPart.[slot] cls
                let entry = classBase.[idx] + clusterPosOf.[slot]
                rowFill entry slot
                csMarkDirty (entry * rowWords) rowWords
        do relistSlot <- fun slot target ->
            let listed = clusterClsOf.[slot] >= 0
            if listed then classRemove slot
            slotPart.[slot] <- target
            if listed then classAdd slot
        /// re-derive the slot's assignment key from its raw tids + CURRENT
        /// allocation lengths / drawn count and migrate its partition residency
        /// + cluster listing if the key changed. Called when an in-place edit
        /// flips an extent class (the demo's recolor toggles full <-> singleton)
        /// or the drawn-vertex count changes.
        let recomputeAssign (slot : int) =
            let raw = Array.init 8 (fun i -> slotFieldTid.[slot * 8 + i])
            let lens = Array.init 8 (fun i -> slotFieldLen.[slot * 8 + i])
            let idxTid = slotIdxTidA.[slot]
            let tids = extendTids raw lens vcOfSlot.[slot] (idxTid > 0)
            let key = internAssign tids idxTid
            if key <> slotAssign.[slot] then
                let listed = clusterClsOf.[slot] >= 0
                if listed then classRemove slot
                asgRemove slot
                asgAdd slot key (lazy (mapOfTids tids idxTid))
                if listed then classAdd slot
        let symPickId = Symbol.Create "HeapPickId"
        let zeroDraw = DrawCallInfo(FaceVertexCount = 0, FirstIndex = 0, BaseVertex = 0, FirstInstance = 0, InstanceCount = 0)
        let freeSlots = System.Collections.Generic.Stack<int>()
        let mutable highWater = 0
        let slots = System.Collections.Generic.Dictionary<RenderObject, HeapSlot>(HashIdentity.Reference)
        // NO globals: there is no global/per-object distinction. Every uniform the
        // shader reads is a ref-counted-by-aval region (regions/singleRegions); a
        // value shared by all objects is just a slot with refcount = object count.
        // The bucket RO answers ONLY the heap-internal names (arena/headers/chain/
        // textures); anything else is a region gathered in the rewritten shader, so
        // the provider never resolves user uniforms (camera/lights/model included).
        // slots whose IsActive is NON-constant (constant gates are baked into the
        // entry at add time) — each gets a GateWriter marked only when ITS gate
        // changes; the draw mirror re-stages exactly the toggled slots.
        let gateWriters = System.Collections.Generic.Dictionary<int, GateWriter>()
        // per-slot adaptive watchers for values the heap used to snapshot at add
        // time (draw-call shape, pick id, model-stack structure) — disposed with
        // the slot. Geometry writers are ENTRY-owned (freed at refcount 0).
        let slotDynWriters = System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<DynWriter>>()
        let regSlotWriter (slot : int) (w : DynWriter) =
            match slotDynWriters.TryGetValue slot with
            | true, l -> l.Add w
            | _ ->
                let l = System.Collections.Generic.List<DynWriter>()
                l.Add w
                slotDynWriters.[slot] <- l

        // ── texture tables: ONE per input sampler (si). A freed cell emits a Remove and the
        // backend nulls the unbound slot, so there is no per-type dummy — any sampler type works.
        // (the atlas FALLBACK still needs a real 2d dummy to pad its fixed-size page array.)
        let mkDummy2d () = runtime.CreateTexture2D(V2i.II, TextureFormat.Rgba8, levels = 1, samples = 1) :> ITexture
        let delDummy (t : ITexture) = match t with :? IBackendTexture as bt -> runtime.DeleteTexture bt | _ -> ()
        let bindlessTexTables =
            if useAtlas then [||]
            else
                samplersIndexed |> Array.map (fun (si, _sn, tn, _ty, _st) ->
                    heapTexArrName si, heapTexIdxName si, [| Symbol.Create tn |], BindlessTexTable(updater, 1))
        // ONE AtlasPool per bucket (lifetime = bucket). The dummy backs the unused
        // slots of the padded 8-page sampler array.
        let atlasState =
            if useAtlas then
                let pool = new AtlasPool(runtime, HeapAtlas.PageSize, HeapAtlas.MaxPagesPerFormat)
                let dummy = mkDummy2d ()
                let states = atlas2d |> Array.map (fun (_, _, _, st) -> st)
                Some (pool, dummy, AtlasPlacementTable(updater, pool, states, atlasK))
            else None

        let ensureSlot (slot : int) =
            if slot >= entries.Length then
                let n = Fun.NextPowerOfTwo (slot + 1)
                let ne = Array.zeroCreate<DrawCallInfo> n
                System.Array.Copy(entries, ne, entries.Length)
                entries <- ne
                let np = Array.zeroCreate<int> n
                System.Array.Copy(slotPage, np, slotPage.Length)
                slotPage <- np
                let npk = Array.zeroCreate<int> n
                System.Array.Copy(pickIds, npk, pickIds.Length)
                pickIds <- npk
                let ncc = Array.create n -1
                System.Array.Copy(clusterClsOf, ncc, clusterClsOf.Length)
                clusterClsOf <- ncc
                let nsa = Array.zeroCreate<int64> n
                System.Array.Copy(slotAssign, nsa, slotAssign.Length)
                slotAssign <- nsa
                let nsp = Array.zeroCreate<int> n
                System.Array.Copy(slotPart, nsp, slotPart.Length)
                slotPart <- nsp
                let nap = Array.zeroCreate<int> n
                System.Array.Copy(slotAsgPos, nap, slotAsgPos.Length)
                slotAsgPos <- nap
                let nft = Array.zeroCreate<int> (n * 8)
                System.Array.Copy(slotFieldTid, nft, slotFieldTid.Length)
                slotFieldTid <- nft
                let nfl = Array.zeroCreate<int> (n * 8)
                System.Array.Copy(slotFieldLen, nfl, slotFieldLen.Length)
                slotFieldLen <- nfl
                let nit = Array.zeroCreate<int> n
                System.Array.Copy(slotIdxTidA, nit, slotIdxTidA.Length)
                slotIdxTidA <- nit
                let ncp = Array.zeroCreate<int> n
                System.Array.Copy(clusterPosOf, ncp, clusterPosOf.Length)
                clusterPosOf <- ncp
                let nvc = Array.zeroCreate<int> n
                System.Array.Copy(vcOfSlot, nvc, vcOfSlot.Length)
                vcOfSlot <- nvc
                let nh = Array.zeroCreate<int> (n * headerStride)
                System.Array.Copy(headers, nh, headers.Length)
                headers <- nh
                if useBindlessGeom then
                    let nv = Array.zeroCreate<aval<IBuffer>> (n * max 1 numAttrs)
                    System.Array.Copy(vtxAvals, nv, vtxAvals.Length)
                    vtxAvals <- nv
                    let nl = Array.zeroCreate<IBuffer> (n * max 1 numAttrs)
                    System.Array.Copy(vtxLast, nl, vtxLast.Length)
                    vtxLast <- nl
                if chainMode then
                    let no = Array.zeroCreate<int> n in System.Array.Copy(chOffset, no, chOffset.Length); chOffset <- no
                    let nl = Array.zeroCreate<int> n in System.Array.Copy(chLen, nl, chLen.Length); chLen <- nl

        // grow chIdx (slot-major link-index runs) to hold at least n ints
        let ensureChIdx (n : int) =
            if n > chIdx.Length then
                let nf = Fun.NextPowerOfTwo n
                let ni = Array.zeroCreate<int> nf
                System.Array.Copy(chIdx, ni, chIdx.Length)
                chIdx <- ni

        // intern the slot's model stack into the link arena + a contiguous chIdx
        // run, IN ARRAY ORDER. ModelTrafoStack is the array `arr` for which the CPU
        // fold is ModelTrafo = arr[0]·arr[1]·…·arr[last] (Trafo3d `*`, see
        // TraversalState.trafoOfStack). composeModel reads the run as [L0;…;Llen-1]
        // = arr and folds m = Llen-1·…·L0 as MATRICES (links store `.Forward`),
        // which equals (arr[0]·…·arr[last]).Forward — i.e. feeding in array order
        // reproduces the CPU fold's Forward exactly (verified bit-identical, the
        // non-identity-box liveChain golden). composeMvpNm's "reversed" comment
        // refers to the same fact; the box being identity in domboxchain hid it.
        let addChainSlot (slot : int) (stack : aval<Trafo3d>[]) =
            let len = stack.Length
            chLen.[slot] <- len
            if len > 0 then
                let b = chIdxAlloc.Alloc len
                let off = int b.Offset
                ensureChIdx chIdxAlloc.Extent
                chOffset.[slot] <- off
                chainBlocks.[slot] <- b
                // feed in array order; intern each link (refcount/dedup)
                for k in 0 .. len - 1 do
                    let link = stack.[k]
                    chIdx.[off + k] <- chainLinks.Intern link
                chainLinkKeys.[slot] <- stack
            else
                chOffset.[slot] <- 0
            chainDirtyStruct.Add slot |> ignore

        let removeChainSlot (slot : int) =
            match chainLinkKeys.TryGetValue slot with
            | true, stack ->
                for link in stack do chainLinks.Release link
                chainLinkKeys.Remove slot |> ignore
            | _ -> ()
            match chainBlocks.TryGetValue slot with
            | true, b -> chIdxAlloc.Free b; chainBlocks.Remove slot |> ignore
            | _ -> ()
            chLen.[slot] <- 0
            chOffset.[slot] <- 0
            chainDirtyStruct.Add slot |> ignore

        // each field's SHADER-requested type, read straight from the effect's
        // declared uniform types. Derived composites are NOT expanded away (they are
        // compute OUTPUTS, gathered like any field): a shader reading
        // `ModelViewProjTrafo : M44f` gets an f32 output region, `NormalMatrix : M33f`
        // a mat3 region, etc.
        let fieldRequestedType : string -> System.Type =
            let m = effect.Uniforms |> Map.map (fun _ p -> p.uniformType)
            fun name -> match Map.tryFind name m with | Some t -> t | None -> typeof<float32>

        // `requested` is the shader's declared type for this field; it decides STORAGE:
        // f32-requested -> f32 words (packerFor converts the provided value, incl.
        // double->f32); double-requested -> REAL doubles (2 words/scalar, 8-byte aligned
        // for the native HeapDataD view). Either way the slot holds what the shader asked.
        let allocRegion (av : IAdaptiveValue) (requested : System.Type) : int =
            match regions.TryGetValue av with
            | true, e -> e.RefCount <- e.RefCount + 1; e.Offset
            | _ ->
                let dbl = isDoubleUniform requested
                // f32/double leaves key on the SUPPLIED type (so Trafo3d/M44d coerce to
                // the requested M44f); composites key on the REQUESTED record type so the
                // tight field layout matches gatherFor exactly.
                let (sz, pk) =
                    if dbl then doublePackerFor df32 requested
                    elif isCompositeType requested then compositePacker requested
                    else packerFor av.ContentType
                // double regions start at an EVEN word (8-byte) so HeapDataD addresses
                // them; over-allocate one word and align the start up.
                let b = arenaAlloc.Alloc (if dbl then sz + 1 else sz)
                let raw = int b.Offset
                let off = if dbl && (raw &&& 1) = 1 then raw + 1 else raw
                // the GPU resize is deferred to the arena's own Compute (which
                // depends on the updater whose evaluation we are inside) — no
                // transact happens here.
                arena.EnsureFloats arenaAlloc.Extent
                // CONSTANT sources are packed ONCE into staging — no RegionWriter
                // (no adaptive subscription to create at add / dispose at remove,
                // nothing for the flush to re-evaluate). Writer = null marks them.
                let w =
                    if av.IsConstant then
                        if dbl then
                            // ONE span for the whole over-allocated block: zero the
                            // alignment slack in-span — an unstaged 1-word hole breaks
                            // the flush's region merging (see StageZero)
                            arena.StageOnce(raw, sz + 1, fun p ->
                                wi p (if off > raw then 0 else sz) 0
                                pk (av.GetValueUntyped AdaptiveToken.Top) p (off - raw))
                        else
                            arena.StageOnce(off, sz, fun p -> pk (av.GetValueUntyped AdaptiveToken.Top) p 0)
                        Unchecked.defaultof<RegionWriter>
                    else
                        // writer re-packs its exact range; stage the slack once here
                        if dbl then arena.StageZero((if off > raw then raw else raw + sz), 1)
                        arena.Add(av, off, sz, pk)
                regions.[av] <- { Offset = off; Size = sz; Writer = w; RefCount = 1; Block = b; HeaderWords = 0 }
                off

        let freeRegion (av : IAdaptiveValue) =
            match regions.TryGetValue av with
            | true, e ->
                e.RefCount <- e.RefCount - 1
                if e.RefCount = 0 then
                    if not (isNull e.Writer) then arena.Remove e.Writer
                    regions.Remove av |> ignore
                    arenaAlloc.Free e.Block
            | _ -> ()

        // ── derived-uniform regions ──────────────────────────────────────
        // CONSTITUENT: a base trafo's forward / backward half as a real M44d (32
        // words, 8-byte aligned for the double view), uploaded from the RO's
        // ViewTrafo/ProjTrafo/ModelTrafo aval. Ref-counted by (aval, inv) so a
        // camera shared across draws is ONE region (its mark re-packs once). The
        // backward half is the uploaded `Trafo3d.Backward` — never a `.Inverse`.
        // df32 mode packs each scalar as a (hi,lo) two-f32 pair (wdDf), matching
        // composeDerivedDf32's df32 reads; fp64 packs the IEEE double bytes (wd).
        let packM44dInto (m : M44d) (a : nativeint) (off : int) =
            let wd = if df32 then wdDf else wd
            wd a (off+0)  m.M00; wd a (off+2)  m.M01; wd a (off+4)  m.M02; wd a (off+6)  m.M03
            wd a (off+8)  m.M10; wd a (off+10) m.M11; wd a (off+12) m.M12; wd a (off+14) m.M13
            wd a (off+16) m.M20; wd a (off+18) m.M21; wd a (off+20) m.M22; wd a (off+22) m.M23
            wd a (off+24) m.M30; wd a (off+26) m.M31; wd a (off+28) m.M32; wd a (off+30) m.M33
        let constituentPack (inv : bool) : obj -> nativeint -> int -> unit =
            fun o a off ->
                let m =
                    match o with
                    | :? Trafo3d as t -> if inv then t.Backward else t.Forward
                    | :? M44d as m -> if inv then failwith "Heap: derived inverse needs a Trafo3d constituent (no .Inverse); got M44d" else m
                    | :? M44f as m -> if inv then failwith "Heap: derived inverse needs a Trafo3d constituent (no .Inverse); got M44f" else M44d m
                    | _ -> failwithf "Heap: derived constituent must be Trafo3d/M44d/M44f, got %s" (o.GetType().Name)
                packM44dInto m a off
        let mutable constituentsF = storage.Page(0).ConstituentsF
        let mutable constituentsB = storage.Page(0).ConstituentsB
        let mutable constituentsCanon = storage.Page(0).ConstituentsCanon
        // current fill page: the slot's allocs (uniforms/geometry/constituents) all route to
        // this page; set per-slot in Add/RemoveInternal. PlacePage rolls when the page fills.
        let mutable curPage = 0
        let setPage (i : int) =
            curPage <- i
            let pg = storage.Page i
            arena <- pg.Arena; arenaAlloc <- pg.ArenaAlloc; regions <- pg.Regions
            singleRegions <- pg.SingleRegions; constituentsF <- pg.ConstituentsF; constituentsB <- pg.ConstituentsB
            constituentsCanon <- pg.ConstituentsCanon
            attrStatic <- pg.AttrStatic; idxStatic <- pg.IdxStatic
            arena.AddDependency (updater :> IAdaptiveValue)
        // conservative worst-case word footprint of a slot's group (geometry + per-draw uniforms +
        // constituents), so PlacePage rolls BEFORE a slot that wouldn't fit ⇒ a group never spans pages.
        let estimateSlotWords (ro : RenderObject) : int =
            let vc = faceVertexCountOf ro
            vc * (max 4 (numAttrs * 4)) + (names.Length + numConst + 8) * 32
        /// CONSTANT constituents dedup by VALUE (per page): distinct AVal.constant
        /// wrappers of the same trafo resolve to one canonical aval and thus one
        /// region — and downstream one deduped derived-output region.
        let canonConstituent (av : IAdaptiveValue) (inv : bool) : IAdaptiveValue =
            if not av.IsConstant then av
            else
                let key = struct(inv, av.GetValueUntyped AdaptiveToken.Top)
                match constituentsCanon.TryGetValue key with
                | true, c -> c
                | _ -> constituentsCanon.[key] <- av; av
        let allocConstituent (av : IAdaptiveValue) (inv : bool) : int =
            let d = if inv then constituentsB else constituentsF
            match d.TryGetValue av with
            | true, e -> e.RefCount <- e.RefCount + 1; e.Offset
            | _ ->
                let sz = 32
                let b = arenaAlloc.Alloc (sz + 1)
                let raw = int b.Offset
                let off = if (raw &&& 1) = 1 then raw + 1 else raw
                arena.EnsureFloats arenaAlloc.Extent
                let pk = constituentPack inv
                let w =
                    if av.IsConstant then
                        // ONE span for the whole block, slack zeroed in-span (see
                        // StageZero); typed fast path — GetValueUntyped boxes the Trafo3d
                        (match av with
                         | :? aval<Trafo3d> as tv ->
                             let tr = tv.GetValue AdaptiveToken.Top
                             arena.StageOnce(raw, sz + 1, fun p ->
                                 wi p (if off > raw then 0 else sz) 0
                                 packM44dInto (if inv then tr.Backward else tr.Forward) p (off - raw))
                         | _ ->
                             arena.StageOnce(raw, sz + 1, fun p ->
                                 wi p (if off > raw then 0 else sz) 0
                                 pk (av.GetValueUntyped AdaptiveToken.Top) p (off - raw)))
                        Unchecked.defaultof<RegionWriter>
                    else
                        arena.StageZero((if off > raw then raw else raw + sz), 1)
                        arena.Add(av, off, sz, pk)
                d.[av] <- { Offset = off; Size = sz; Writer = w; RefCount = 1; Block = b; HeaderWords = 0 }
                off
        let freeConstituent (av : IAdaptiveValue) (inv : bool) =
            let d = if inv then constituentsB else constituentsF
            match d.TryGetValue av with
            | true, e ->
                e.RefCount <- e.RefCount - 1
                if e.RefCount = 0 then
                    if not (isNull e.Writer) then arena.Remove e.Writer
                    d.Remove av |> ignore
                    arenaAlloc.Free e.Block
                    if av.IsConstant then
                        constituentsCanon.Remove(struct(inv, av.GetValueUntyped AdaptiveToken.Top)) |> ignore
            | _ -> ()
        // an 8-byte-aligned M44d slot the CHAIN fold writes (no aval / writer) — the
        // per-slot Model forward/backward constituent in chainMode.
        let allocFoldConstituent () : int * HeapBlock =
            let sz = 32
            let b = arenaAlloc.Alloc (sz + 1)
            let raw = int b.Offset
            let off = if (raw &&& 1) = 1 then raw + 1 else raw
            arena.EnsureFloats arenaAlloc.Extent
            arena.StageZero(raw, sz + 1)     // placeholder incl. align slack (see StageZero)
            off, b
        // OUTPUT: a region the derive compute writes (no aval / writer), stored as
        // the shader's requested type (f32 M44f = 16 words, M33f = 9, …).
        // derived-output shares: dedup dict (page, planIdx, canonical constituents)
        // -> share, plus the full registry.
        let derivedShares = System.Collections.Generic.Dictionary<struct(int * int * obj * obj * obj), DerivedShare>()
        let allShares = System.Collections.Generic.HashSet<DerivedShare>(HashIdentity.Reference)
        // ── PER-OUTPUT derive dispatch list: [ownerSlot; planIdx] per live share.
        //    One kernel thread per SHARE (distinct derived value) — never per slot.
        //    Swap-remove keeps it dense; owner transfer patches in place. ──
        let shareList = System.Collections.Generic.List<DerivedShare>()
        let mutable shareStaging : int[] = Array.zeroCreate 256
        let shareDirtyIdx = System.Collections.Generic.HashSet<int>()
        let mutable shareAllDirty = true
        let sharePlanIdx (sh : DerivedShare) = let struct(_, j, _, _, _) = sh.Key in j
        let shareWrite (sh : DerivedShare) =
            let o = sh.ListIdx * 2
            if shareStaging.Length < o + 2 then
                let ns = Array.zeroCreate<int> (Fun.NextPowerOfTwo (o + 2))
                System.Array.Copy(shareStaging, ns, shareStaging.Length)
                shareStaging <- ns
            shareStaging.[o] <- sh.Owner
            shareStaging.[o + 1] <- sharePlanIdx sh
            if not shareAllDirty then
                if shareDirtyIdx.Count >= 4096 then shareDirtyIdx.Clear(); shareAllDirty <- true
                else shareDirtyIdx.Add sh.ListIdx |> ignore
        let shareAdd (sh : DerivedShare) =
            sh.ListIdx <- shareList.Count
            shareList.Add sh
            shareWrite sh
        let shareRemove (sh : DerivedShare) =
            let last = shareList.[shareList.Count - 1]
            shareList.[sh.ListIdx] <- last
            last.ListIdx <- sh.ListIdx
            shareList.RemoveAt(shareList.Count - 1)
            if not (System.Object.ReferenceEquals(last, sh)) then shareWrite last
            sh.ListIdx <- -1
        // DENSE uniform store: derived outputs live tightly packed in their own
        // bucket-global buffer (never the paged geometry arena) — consecutive
        // slots' composites land in adjacent cache lines instead of one per
        // ~4KB slot group, and the store never participates in page compaction.
        // GPU-written by the derive kernels, gathered via HeapUni/HeapUniD.
        // 64MB initial capacity; the Flush handler grows it to uniAlloc.Extent on demand
        let uniBuf = MirrorBuffer(runtime, 64 <<< 20, BufferUsage.Storage)
        let uniAlloc = HeapSpace()
        let allocOutput (requested : System.Type) : int * HeapBlock =
            let dbl = isDoubleUniform requested
            let (sz, _) = if dbl then doublePackerFor df32 requested else packerFor requested
            let b = uniAlloc.Alloc (if dbl then sz + 1 else sz)
            let raw = int b.Offset
            let off = if dbl && (raw &&& 1) = 1 then raw + 1 else raw
            off, b

        /// SINGLETON attribute (SingleValueBuffer): a header + an adaptive
        /// region writer packing the value's native bytes at ref+4. length = 1,
        /// stride = 0 — the shader's `vid % length` fetch broadcasts it, and an
        /// aval change re-packs O(1) (one writer, one sub-range upload).
        let allocSingle (av : IAdaptiveValue) (et : System.Type) : int =
            match singleRegions.TryGetValue av with
            | true, e -> e.RefCount <- e.RefCount + 1; e.Offset
            | _ ->
                let tid =
                    match attrTypeId et with
                    | ValueSome t -> t
                    | ValueNone -> failwithf "Heap: singleton attribute element type %A has no storage typeId" et
                let (szF, pk) = attrPackerFor av.ContentType
                let sizeF = AllocHeaderWords + szF
                let b = arenaAlloc.Alloc sizeF
                let off = int b.Offset
                arena.EnsureFloats arenaAlloc.Extent
                arena.WriteHeader(off, tid, 1, 0)
                // constant singleton value -> one-shot staging write (see allocRegion)
                let w =
                    if av.IsConstant then
                        arena.StageOnce(off + AllocHeaderWords, szF, fun p -> pk (av.GetValueUntyped AdaptiveToken.Top) p 0)
                        Unchecked.defaultof<RegionWriter>
                    else arena.Add(av, off + AllocHeaderWords, szF, pk)
                singleRegions.[av] <- { Offset = off; Size = sizeF; Writer = w; RefCount = 1; Block = b; HeaderWords = AllocHeaderWords }
                off

        let freeSingle (av : IAdaptiveValue) =
            match singleRegions.TryGetValue av with
            | true, e ->
                e.RefCount <- e.RefCount - 1
                if e.RefCount = 0 then
                    if not (isNull e.Writer) then arena.Remove e.Writer
                    singleRegions.Remove av |> ignore
                    arenaAlloc.Free e.Block
            | _ -> ()

        /// STATIC allocation (immutable bytes + header), refcounted/deduped in
        /// `dict` by source identity. Returns the cached entry on a hit.
        let allocStatic (dict : System.Collections.Generic.Dictionary<struct(obj * int * int), StaticEntry>)
                        (key : struct(obj * int * int)) (byteLen : int) (writeBytes : nativeint -> unit)
                        (typeId : int) (count : int) (strideBytes : int) : StaticEntry =
            match dict.TryGetValue key with
            | true, e -> e.RefCount <- e.RefCount + 1; e
            | _ ->
                let words = (byteLen + 3) / 4
                let sizeF = AllocHeaderWords + words
                let b = arenaAlloc.Alloc sizeF
                let off = int b.Offset
                arena.EnsureFloats arenaAlloc.Extent
                let t0 = System.Diagnostics.Stopwatch.GetTimestamp()
                arena.WriteHeader(off, typeId, count, strideBytes)
                // payload straight into the ring span (ragged tail word zeroed first)
                let p = arena.StageWords(off + AllocHeaderWords, words)
                stIngestStageMs <- stIngestStageMs + float (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / float System.Diagnostics.Stopwatch.Frequency
                if byteLen % 4 <> 0 then wi p (words - 1) 0
                writeBytes p
                let e = { Ref = off; SizeF = sizeF; Count = count; RefCount = 1; Block = b
                          Writer = Unchecked.defaultof<DynWriter>; DynRefs = null }
                dict.[key] <- e
                e

        let freeStatic (dict : System.Collections.Generic.Dictionary<struct(obj * int * int), StaticEntry>) (key : struct(obj * int * int)) =
            match dict.TryGetValue key with
            | true, e ->
                e.RefCount <- e.RefCount - 1
                if e.RefCount = 0 then
                    if not (isNull e.Writer) then e.Writer.Dispose()
                    dict.Remove key |> ignore
                    arenaAlloc.Free e.Block
            | _ -> ()

        /// Upgrade a freshly-allocated geometry entry whose SOURCE aval is
        /// non-constant to a DYNAMIC entry: an adaptive writer re-stages the
        /// payload in place on a same-size value change (O(bytes)) and
        /// free/realloc-s + re-writes the header + re-bakes every referencing
        /// slot (O(sharers), via IGeomSink.GeomMoved) on a size change. The
        /// entry's bytes were already staged by the allocation, so the writer
        /// starts Fresh (its first apply only subscribes). Captures the entry's
        /// OWN page — compaction may re-seat the block (the pack reads e.Ref
        /// fresh), but an allocation never migrates pages.
        let makeDynamic (e : StaticEntry) (bv : BufferView) (typeId : int) (esBytes : int) (isIndex : bool) =
            let pg = storage.Page curPage
            let w = DynWriter(bv.Buffer)
            w.Fresh <- true
            e.Writer <- w
            e.DynRefs <- System.Collections.Generic.HashSet<struct(IGeomSink * int * int)>()
            w.OnChange <- System.Action<AdaptiveToken>(fun tok ->
                w.Update(tok, fun _ (o : obj) ->
                    if w.Fresh then w.Fresh <- false
                    else
                        let value = o :?> IBuffer
                        let byteLen = geomByteLen' value bv
                        let words = (byteLen + 3) / 4
                        if AllocHeaderWords + words <= int e.Block.Size then
                            // IN-PLACE: the new payload fits the existing block — keep
                            // the allocation (no free/alloc, no slot header re-bake; the
                            // header cell offsets stay valid), rewrite the header's
                            // element count when it changed, and re-stage the payload.
                            // The demo's select toggle (full array <-> singleton) lives
                            // entirely on this path, in both directions.
                            let count = byteLen / esBytes
                            if count <> e.Count then
                                e.Count <- count
                                pg.Arena.WriteHeader(e.Ref, typeId, count, esBytes)
                                // notify ATTRIBUTE refs too (ref unchanged): a length
                                // change can flip the extent class (full <-> singleton)
                                // and the slot's typed-partition assignment with it
                                for struct(sink, slot, cell) in e.DynRefs do
                                    sink.GeomMoved(slot, cell, e.Ref, count, isIndex)
                            EditProf.count "dyn:inplace" words
                            let p = pg.Arena.StageWords(e.Ref + AllocHeaderWords, words)
                            if byteLen % 4 <> 0 then wi p (words - 1) 0
                            stageGeomBytes' runtime value bv byteLen p
                        else
                            EditProf.count "dyn:realloc" words
                            pg.ArenaAlloc.Free e.Block
                            let sizeF = AllocHeaderWords + words
                            let b = pg.ArenaAlloc.Alloc sizeF
                            e.Block <- b
                            e.Ref <- int b.Offset
                            e.SizeF <- sizeF
                            e.Count <- byteLen / esBytes
                            pg.Arena.EnsureFloats pg.ArenaAlloc.Extent
                            pg.Arena.WriteHeader(e.Ref, typeId, e.Count, esBytes)
                            let p = pg.Arena.StageWords(e.Ref + AllocHeaderWords, words)
                            if byteLen % 4 <> 0 then wi p (words - 1) 0
                            stageGeomBytes' runtime value bv byteLen p
                            for struct(sink, slot, cell) in e.DynRefs do
                                sink.GeomMoved(slot, cell, e.Ref, e.Count, isIndex)))
            w

        /// the slot's index allocation: raw index bytes (host or downloaded GPU
        /// buffer) with a header carrying the ELEMENT TYPE (u16 vs 32-bit) —
        /// the shader's index decode branches on it, so one bucket freely
        /// mixes 16- and 32-bit-indexed members.
        /// sentinel slot key for NON-indexed members: never inserted into
        /// idxStatic, so freeStatic on remove is a clean no-op and the
        /// compaction header rewrite skips it (TryGetValue miss).
        let noIdxKey : struct(obj * int * int) = struct(null, 0, 0)

        let idxFor (ro : RenderObject) : struct(obj * int * int) * StaticEntry =
            let bv = match ro.Indices with Some b -> b | None -> failwith "Heap.ofRenderObjects: RO has no index buffer"
            let es = elemSize bv.ElementType
            let tid = if es = 2 then IdxType16 else IdxType32
            // resolve the buffer aval ONCE per index source (dedup key + length + copy)
            let value = bv.Buffer.GetValue()
            let key = struct(geomDedupSource' value bv, bv.Offset, tid)
            match idxStatic.TryGetValue key with
            | true, e -> e.RefCount <- e.RefCount + 1; key, e
            | _ ->
                let len = geomByteLen' value bv
                let e = allocStatic idxStatic key len (stageGeomBytes' runtime value bv len) tid (len / es) es
                if not bv.Buffer.IsConstant && not disableDynGeom then makeDynamic e bv tid es true |> ignore
                key, e

        /// one consumed attribute of a new slot: singleton -> adaptive region,
        /// real buffer -> static allocation. Returns (release key, header ref).
        /// The allocation's header carries the RO's OWN element typeId — the
        /// shader decode branches on it at fetch time, so member element types
        /// vary freely within a bucket (they are NOT part of the bucket key).
        let attrFor (ro : RenderObject) (sym : Symbol) : AttrKey * int * int * int =
            let bv =
                match ro.VertexAttributes.TryGetAttribute sym with
                | ValueSome b -> b
                | ValueNone -> failwithf "Heap.ofRenderObjects: RO missing shader input attribute %A" sym
            // the SOURCE tid this member feeds the field (spec-constant inference)
            let srcTid =
                match attrTypeId bv.ElementType with
                | ValueSome t -> t
                | ValueNone -> 0
            match bv.Buffer with
            | :? ISingleValueBuffer as svb ->
                AttrKey.Single svb.Value, allocSingle svb.Value bv.ElementType, srcTid, 1
            | _ ->
                let et = bv.ElementType
                let tid =
                    match attrTypeId et with
                    | ValueSome t -> t
                    | ValueNone -> failwithf "Heap: attribute %A element type %A has no storage typeId" sym et
                // resolve the buffer aval ONCE per attribute (dedup key + length + copy)
                let value = bv.Buffer.GetValue()
                let key = struct(geomDedupSource' value bv, bv.Offset, tid)
                match attrStatic.TryGetValue key with
                | true, e -> e.RefCount <- e.RefCount + 1; AttrKey.Static key, e.Ref, srcTid, e.Count
                | _ ->
                    let es = elemSize et
                    let len = geomByteLen' value bv
                    let e = allocStatic attrStatic key len (stageGeomBytes' runtime value bv len) tid (len / es) es
                    if not bv.Buffer.IsConstant && not disableDynGeom then makeDynamic e bv tid es false |> ignore
                    AttrKey.Static key, e.Ref, srcTid, e.Count

        // ── threshold-triggered compaction is PAGE-level now (PageArena.Compact):
        //    a page's dicts hold entries from EVERY bucket sharing the storage, so
        //    the page collects all dict residents itself and asks each registered
        //    participant (bucket) for its per-slot blocks + a header re-bake — see
        //    the IPageParticipant implementation at the end of this type. Here we
        //    only keep the per-slot-block bookkeeping the participant needs. ──
        // AddInternal fills OutBlocks in ascending derived-cell order and FoldBlocks
        // in ascending constituent order, so the j-th block maps to the j-th cell:
        let derivedCellOrder = Set.toArray derivedCells             // sorted ascending
        let plainCells =                                            // RegionKeys.[j] <-> plainCells.[j]
            Array.init names.Length id |> Array.filter (fun i -> not (Set.contains i derivedCells))
        let foldConstIdx =                                          // FoldBlocks.[j] <-> constituent cell fieldStride + foldConstIdx.[j]
            [| for k in 0 .. numConst - 1 do
                 if neededConstituents.[k].CBase = Derived.MBASE && chainMode then yield k |]
        let plainConstIdx =                                         // ConstKeys.[j] <-> constituent cell fieldStride + plainConstIdx.[j]
            [| for k in 0 .. numConst - 1 do
                 if not (neededConstituents.[k].CBase = Derived.MBASE && chainMode) then yield k |]
        // derived OUTPUT regions of a double-requested type are 8-byte-aligned
        let outAligned = derivedCellOrder |> Array.map (fun c -> isDoubleUniform (fieldRequestedType names.[c]))

        let compactInst () =
            let nd = Array.zeroCreate<int> (max 16 (Fun.NextPowerOfTwo (max 1 instAlloc.Live)))
            instAlloc.Reset()
            for KeyValue(_, s) in slots do
                let b = instAlloc.Alloc s.Instances
                let off = int b.Offset
                for i in 0 .. s.Instances - 1 do nd.[off + i] <- s.Slot
                s.InstOffset <- off
                s.InstBlock <- b
                entries.[s.Slot].FirstInstance <- off
            instData <- nd
            instAllDirty <- true
            drawsAllDirty <- true       // FirstInstance of every live record moved
            compactionCount <- compactionCount + 1

        /// trigger check, run after removals (cheap: a few integer compares).
        /// Arena compaction is storage-level (every page checks its own waste and
        /// compacts with ALL participating buckets); the per-instance slot-attribute
        /// buffer is bucket-owned and compacts locally.
        let maybeCompact () =
            storage.MaybeCompact()
            if useSlotAttr
               && instAlloc.Live * 2 < instAlloc.Extent
               && int64 instAlloc.Waste * 4L > int64 compactionWasteFloorBytes then compactInst ()

        // ── stable mirror buffers + reactive views over the mutable state. All
        //    are driven by `updater` (via MirrorBuffer.Dependency), so they
        //    refresh exactly when membership changed and evaluation order
        //    doesn't matter. Draw records, headers and slot attributes live in
        //    OWNED backend buffers whose identity is stable (growth aside): the
        //    flushes upload only the recorded dirty sub-ranges, and the
        //    resource layer sees the unchanged handle and re-prepares nothing —
        //    a structural version costs O(changed), not O(slots). Draw records
        //    are NON-indexed (vertexCount = the slot's index count; the vertex
        //    shader decodes indices from storage), which IS the DrawCallInfo
        //    struct layout, and the IndirectBuffer record carries
        //    Indexed = false, so BOTH backends bind the GPU buffer directly —
        //    no layout conversion, no per-version copy.
        let drawBuf    = MirrorBuffer(runtime, entries.Length * sizeof<DrawCallInfo>, BufferUsage.Indirect)
        let headersBuf = MirrorBuffer(runtime, headers.Length * 4, BufferUsage.Storage)
        let instBuf    = MirrorBuffer(runtime, instData.Length * 4, BufferUsage.Vertex)
        // slot->page SSBO (for the per-page derive guard). Dirty-slot flush like headers.
        let slotPageBuf = MirrorBuffer(runtime, max 16 slotPage.Length * 4, BufferUsage.Storage)
        // per-output derive dispatch list ([ownerSlot; planIdx] per share)
        let shareRecBuf = MirrorBuffer(runtime, 1024, BufferUsage.Storage)
        let flushShareRecs (_ : AdaptiveToken) (_ : System.Collections.Generic.HashSet<GateWriter>) =
            shareRecBuf.ResizeInPlace(uint64 (max 256 (shareList.Count * 2)) * 4UL)
            if shareAllDirty then
                shareAllDirty <- false
                shareDirtyIdx.Clear()
                if shareList.Count > 0 then shareRecBuf.Write(shareStaging, 0UL, 0, shareList.Count * 2)
            elif shareDirtyIdx.Count > 0 then
                let ss = System.Collections.Generic.List<int>(shareDirtyIdx)
                shareDirtyIdx.Clear()
                ss.Sort()
                let flush lo hi = shareRecBuf.Write(shareStaging, uint64 (lo * 8), lo * 2, (hi - lo + 1) * 2)
                let mutable lo = ss.[0]
                let mutable hi = ss.[0]
                for i in 1 .. ss.Count - 1 do
                    let x = ss.[i]
                    if x <= hi + 32 then hi <- x
                    else flush lo hi; lo <- x; hi <- x
                flush lo hi
        // shared dirty-slot flush for the two int-per-slot mirrors (slotPage /
        // pickIds): allDirty → one full write; otherwise gap-merged sub-ranges
        // over the sorted dirty slots (the CPU array is the always-valid source
        // of truth, so a gap's bytes re-upload unchanged — see flushHeaders).
        let flushIntPerSlot (buf : MirrorBuffer) (data : int[]) (dirty : System.Collections.Generic.HashSet<int>) (allDirty : byref<bool>) =
            buf.ResizeInPlace(uint64 (max 16 data.Length * 4))
            if allDirty then
                allDirty <- false
                dirty.Clear()
                if highWater > 0 then buf.Write(data, 0UL, 0, highWater)
            elif dirty.Count > 0 then
                let ss = System.Collections.Generic.List<int>(dirty)
                dirty.Clear()
                ss.Sort()
                let flush lo hi = buf.Write(data, uint64 (lo * 4), lo, hi - lo + 1)
                let mutable lo = ss.[0]
                let mutable hi = ss.[0]
                for i in 1 .. ss.Count - 1 do
                    let s = ss.[i]
                    if s <= hi + 64 then hi <- s
                    else flush lo hi; lo <- s; hi <- s
                flush lo hi
        let flushSlotPage (_ : AdaptiveToken) (_ : System.Collections.Generic.HashSet<GateWriter>) =
            flushIntPerSlot slotPageBuf slotPage slotPageDirty &slotPageAllDirty
        // PICKING: per-slot pick-id SSBO (parallel to slotPageBuf; dirty-slot flush).
        let pickIdBuf = MirrorBuffer(runtime, max 16 pickIds.Length * 4, BufferUsage.Storage)
        let flushPickIds (_ : AdaptiveToken) (_ : System.Collections.Generic.HashSet<GateWriter>) =
            flushIntPerSlot pickIdBuf pickIds pickIdsDirty &pickIdsAllDirty
        // CPU staging of the draw records in INDEXED layout (uploaded ranges
        // must be contiguous; entries itself stays in DrawCallInfo layout)
        let mutable drawStaging : DrawCallInfo[] = Array.zeroCreate entries.Length

        // ── CLUSTER records: one instanced record per non-empty (page, class) +
        //    exact per-slot records for oversized slots — ALL routed through the
        //    ClassSlots indirection (gl_InstanceIndex is GLOBAL via FirstInstance).
        //    Bases are a deterministic walk over the lists; every flush derives from
        //    the same post-updater state, so flush order is irrelevant. ──
        let clusterRecordsFor (page : int) (part : int) (dst : System.Collections.Generic.List<DrawCallInfo>) =
            dst.Clear()
            match classBlockOf.TryGetValue(struct(page, part)) with
            | false, _ -> ()
            | true, blockBase ->
                for cls in 0 .. numClasses do
                    let idx = blockBase + cls
                    let l = classLists.[idx]
                    if l.Count > 0 then
                        let basev = classBase.[idx]
                        if cls < numClasses then
                            dst.Add(DrawCallInfo(FaceVertexCount = clusterClassSizes.[cls], FirstIndex = 0, BaseVertex = 0,
                                                 FirstInstance = basev, InstanceCount = l.Count))
                        else
                            for j in 0 .. l.Count - 1 do
                                dst.Add(DrawCallInfo(FaceVertexCount = vcOfSlot.[l.[j]], FirstIndex = 0, BaseVertex = 0,
                                                     FirstInstance = basev + j, InstanceCount = 1))
        // record counts per (page, partition) — read by the indirect-count avals
        let clusterRecCounts2 = System.Collections.Generic.Dictionary<struct(int * int), int>()
        let clusterRecCount (page : int) (part : int) =
            match clusterRecCounts2.TryGetValue(struct(page, part)) with
            | true, n -> n
            | _ -> 0
        let setClusterRecCount (page : int) (part : int) (n : int) =
            clusterRecCounts2.[struct(page, part)] <- n
        // gl_InstanceIndex -> slot. O(changed): membership edits recorded as dirty
        // single-int ranges into the CPU mirror; only region growth / relayout pays
        // a bigger (amortized) upload.
        let classSlotsBuf = MirrorBuffer(runtime, 64 * 4, BufferUsage.Storage ||| BufferUsage.Vertex)
        let flushClassSlots (_ : AdaptiveToken) (_ : System.Collections.Generic.HashSet<GateWriter>) =
            classSlotsBuf.ResizeInPlace(uint64 (max 64 csStaging.Length * 4))
            if csFullDirty then
                csFullDirty <- false
                csDirty.Clear()
                if csCursor > 0 then classSlotsBuf.Write(csStaging, 0UL, 0, csCursor * rowWords)
            elif csDirty.Count > 0 then
                csDirty.Sort(fun (struct(a, _)) (struct(b, _)) -> compare a b)
                // small gaps merge — csStaging is the always-valid source of truth
                let flush lo hi = classSlotsBuf.Write(csStaging, uint64 (lo * 4), lo, hi - lo)
                let mutable lo = let (struct(o, _)) = csDirty.[0] in o
                let mutable hi = let (struct(o, n)) = csDirty.[0] in o + n
                for i in 1 .. csDirty.Count - 1 do
                    let (struct(o, n)) = csDirty.[i]
                    if o <= hi + 256 then hi <- max hi (o + n)
                    else flush lo hi; lo <- o; hi <- o + n
                flush lo hi
                csDirty.Clear()
        let clusterRecs = System.Collections.Generic.List<DrawCallInfo>()
        let mutable lastRecsVersion0 = -1

        let rec flushDraws (t : AdaptiveToken) (gates : System.Collections.Generic.HashSet<GateWriter>) =
            if useClusters then
                // record set derived from the class lists (membership settled in the
                // updater) — the bucketRO carries (page 0, DYNAMIC partition 0).
                // Regenerate ONLY when the record-affecting state moved (recsVersion):
                // content-only edits skip the rebuild + upload entirely.
                if lastRecsVersion0 <> recsVersion then
                    lastRecsVersion0 <- recsVersion
                    clusterRecordsFor 0 0 clusterRecs
                    setClusterRecCount 0 0 clusterRecs.Count
                    if drawStaging.Length < clusterRecs.Count then
                        drawStaging <- Array.zeroCreate (Fun.NextPowerOfTwo (max 16 clusterRecs.Count))
                    for i in 0 .. clusterRecs.Count - 1 do drawStaging.[i] <- clusterRecs.[i]
                    drawBuf.ResizeInPlace(uint64 (max 16 drawStaging.Length * sizeof<DrawCallInfo>))
                    if clusterRecs.Count > 0 then drawBuf.Write(drawStaging, 0UL, 0, clusterRecs.Count)
            else
            flushDrawsPerSlot t gates

        and flushDrawsPerSlot (t : AdaptiveToken) (gates : System.Collections.Generic.HashSet<GateWriter>) =
            // toggled dynamic gates -> InstanceCount of exactly those slots
            for w in gates do
                if not w.IsDisposed then
                    w.Update(t, fun slot count ->
                        if entries.[slot].InstanceCount <> count then
                            entries.[slot].InstanceCount <- count
                            dirtyDraws.Add slot |> ignore)
            if drawStaging.Length < entries.Length then
                let ns = Array.zeroCreate<DrawCallInfo> entries.Length
                System.Array.Copy(drawStaging, ns, drawStaging.Length)
                drawStaging <- ns
            let stride = sizeof<DrawCallInfo>
            drawBuf.ResizeInPlace(uint64 (drawStaging.Length * stride))
            // draws are NON-indexed (the shader storage-decodes the indices), so
            // the DrawCallInfo layout IS the native VkDrawIndirectCommand /
            // GL DrawArraysIndirectCommand layout — staged verbatim.
            // page 0's sub-draw: only its slots draw; slots on other pages get a 0-instance record.
            let inline stage (s : int) =
                drawStaging.[s] <- if slotPage.[s] = 0 then entries.[s] else zeroDraw
            if drawsAllDirty then
                drawsAllDirty <- false
                dirtyDraws.Clear()
                if highWater > 0 then
                    for s in 0 .. highWater - 1 do stage s
                    drawBuf.Write(drawStaging, 0UL, 0, highWater)
            elif dirtyDraws.Count > 0 then
                let ss = System.Collections.Generic.List<int>(dirtyDraws)
                dirtyDraws.Clear()
                ss.Sort()
                // runs separated by SMALL gaps merge: the staging mirror is
                // always valid (a slot is re-staged whenever its entry
                // changes), so re-uploading a gap's bytes is harmless and far
                // cheaper than per-run upload-call overhead under dense churn.
                let flush lo hi = drawBuf.Write(drawStaging, uint64 (lo * stride), lo, hi - lo + 1)
                let mutable lo = ss.[0]
                let mutable hi = ss.[0]
                stage ss.[0]
                for i in 1 .. ss.Count - 1 do
                    let s = ss.[i]
                    stage s
                    if s <= hi + 64 then hi <- s
                    else flush lo hi; lo <- s; hi <- s
                flush lo hi

        let flushHeaders (_ : AdaptiveToken) (_ : System.Collections.Generic.HashSet<GateWriter>) =
            headersBuf.ResizeInPlace(uint64 (headers.Length * 4))
            if headersAllDirty then
                headersAllDirty <- false
                dirtyHeaders.Clear()
                let n = highWater * headerStride
                if n > 0 then headersBuf.Write(headers, 0UL, 0, n)
            elif dirtyHeaders.Count > 0 then
                let ss = System.Collections.Generic.List<int>(dirtyHeaders)
                dirtyHeaders.Clear()
                ss.Sort()
                // small gaps merge — `headers` is the always-valid source of
                // truth, so a gap's bytes re-upload unchanged (see flushDraws)
                let flush lo hi = headersBuf.Write(headers, uint64 (lo * headerStride * 4), lo * headerStride, (hi - lo + 1) * headerStride)
                let mutable lo = ss.[0]
                let mutable hi = ss.[0]
                for i in 1 .. ss.Count - 1 do
                    let s = ss.[i]
                    if s <= hi + 64 then hi <- s
                    else flush lo hi; lo <- s; hi <- s
                flush lo hi
            else dirtyHeaders.Clear()

        let flushInst (_ : AdaptiveToken) (_ : System.Collections.Generic.HashSet<GateWriter>) =
            instBuf.ResizeInPlace(uint64 (instData.Length * 4))
            if instAllDirty then
                instAllDirty <- false
                instDirty.Clear()
                if instAlloc.Extent > 0 then instBuf.Write(instData, 0UL, 0, instAlloc.Extent)
            elif instDirty.Count > 0 then
                instDirty.Sort(fun (struct(a, _)) (struct(b, _)) -> compare a b)
                // small gaps merge — instData is the always-valid source of truth
                let flush lo hi = instBuf.Write(instData, uint64 (lo * 4), lo, hi - lo)
                let mutable lo = let (struct(l, _)) = instDirty.[0] in l
                let mutable hi = let (struct(_, h)) = instDirty.[0] in h
                for i in 1 .. instDirty.Count - 1 do
                    let (struct(o, e)) = instDirty.[i]
                    if o <= hi + 256 then hi <- max hi e
                    else flush lo hi; lo <- o; hi <- e
                flush lo hi
                instDirty.Clear()

        do
            let dep = Some (updater :> IAdaptiveValue)
            drawBuf.Dependency <- dep
            drawBuf.Flush <- flushDraws
            drawBuf.Name <- "HeapIndirect"
            headersBuf.Dependency <- dep
            headersBuf.Flush <- flushHeaders
            headersBuf.Name <- "HeapHeaders"
            instBuf.Dependency <- dep
            instBuf.Flush <- flushInst
            instBuf.Name <- "HeapSlotAttr"
            slotPageBuf.Dependency <- dep
            slotPageBuf.Flush <- flushSlotPage
            slotPageBuf.Name <- "HeapSlotPage"
            if hasDerived then
                shareRecBuf.Dependency <- dep
                shareRecBuf.Flush <- flushShareRecs
                shareRecBuf.Name <- "HeapShareRecs"
            if useClusters then
                classSlotsBuf.Dependency <- dep
                classSlotsBuf.Flush <- flushClassSlots
                classSlotsBuf.Name <- "HeapClassSlots"
            if hasDerived then
                uniBuf.Dependency <- dep
                uniBuf.Flush <- (fun _ _ ->
                    if Diagnostics then Log.line "[heap-dbg] uniBuf flush extent=%d" uniAlloc.Extent
                    uniBuf.ResizeInPlace(uint64 (max 256 uniAlloc.Extent) * 4UL))
                uniBuf.Name <- "HeapUni"
            if picking then
                pickIdBuf.Dependency <- dep
                pickIdBuf.Flush <- flushPickIds
                pickIdBuf.Name <- "HeapPickIds"

        // ACQUISITION-PROPAGATING views over the bucket-owned AdaptiveBuffers:
        // AdaptiveResource.mapNonAdaptive keeps the IAdaptiveResource interface
        // (plain AVal.map would strip it), so the backends' resource locations —
        // which Acquire on prepare and Release on dispose — refcount the
        // underlying MirrorBuffer / HeapArena and DESTROY its backend buffer
        // when the bucket RO leaves the render task. The mapping is re-run on
        // every demanded pull (cheap: an upcast / a record alloc), which the
        // indirect view RELIES on: highWater can change without the backend
        // handle changing, and the fresh record picks it up.
        let headersAval = (headersBuf :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)
        let indirectAval =
            (drawBuf :> aval<IBackendBuffer>)
            |> AdaptiveResource.mapNonAdaptive (fun b ->
                let cnt = if useClusters then clusterRecCount 0 0 else highWater
                IndirectBuffer.ofBuffer false 0UL sizeof<DrawCallInfo> cnt (b :> IBuffer))
        let instAval = (instBuf :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)
        let slotPageU = ((slotPageBuf :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)) :> IAdaptiveValue
        let classRowsAval = (classSlotsBuf :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)
        let classSlotsU = classRowsAval :> IAdaptiveValue
        let uniBufU = ((uniBuf :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)) :> IAdaptiveValue
        let symUni = Symbol.Create "HeapUni"
        let symUniD = Symbol.Create "HeapUniD"
        let pickIdU = ((pickIdBuf :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)) :> IAdaptiveValue
        // bindless vertex-pull: object-major flatten of the slots' buffer avals
        // (HeapVertexData[slot*numAttrs + ai]). Depends on the updater version and
        // re-reads only the live slots' avals (cheap when unchanged); a fresh
        // array is produced per version, tombstoned positions keep the last
        // buffer so the SSBO array binding stays valid.
        let vtxStamp (t : AdaptiveToken) (pos : int) =
            let av = vtxAvals.[pos]
            if System.Object.ReferenceEquals(av, null) then vtxOut.[pos] <- vtxLast.[pos]
            else let b = av.GetValue t in vtxLast.[pos] <- b; vtxOut.[pos] <- b
        let vtxGatherAval =
            { new AVal.AbstractVal<IBuffer[]>() with
                // a dynamic source buffer marked us → record WHICH position changed.
                // runs OUTSIDE x's lock, so vtxValDirty has its own monitor. inputs we
                // don't track (e.g. `updater`) aren't in vtxPosOf and are ignored here —
                // the structural path drives membership via vtxStructDirty.
                override _.InputChangedObject(_, o) =
                    match vtxPosOf.TryGetValue o with
                    | true, pos -> lock vtxValDirty (fun () -> vtxValDirty.Add pos |> ignore)
                    | _ -> ()
                override _.Compute(t) =
                    updater.GetValue t |> ignore
                    let n = highWater * numAttrs
                    // grow the persistent output (rare; pow2-amortized by the vtxAvals
                    // doubling). A grow forces a one-time full restamp of live cells.
                    if vtxOut.Length < max 1 n then
                        let no = Array.zeroCreate<IBuffer> (Fun.NextPowerOfTwo (max 1 n))
                        System.Array.Copy(vtxOut, no, vtxOut.Length)
                        vtxOut <- no
                        vtxOutHighWater <- -1
                    // structural refresh: highWater only changes on grow/shrink (churn
                    // reuses freed slots, so highWater is stable) — then a full restamp
                    // is needed once; otherwise restamp exactly the slots added/removed
                    // since the last pull (O(touched)).
                    if vtxOutHighWater <> highWater then
                        vtxOutHighWater <- highWater
                        for pos in 0 .. n - 1 do vtxStamp t pos
                        vtxStructDirty.Clear()
                        lock vtxValDirty (fun () -> vtxValDirty.Clear())   // full restamp subsumes value-dirty
                    else
                        for pos in vtxStructDirty do if pos < n then vtxStamp t pos
                        vtxStructDirty.Clear()
                        // re-stamp ONLY the dynamic buffers that actually marked us
                        // (recorded in InputChangedObject) — re-pulling re-establishes
                        // their consumed edges. Unchanged buffers never marked us, so
                        // their edges persist untouched. O(changed), not O(dynamic).
                        let dirty = lock vtxValDirty (fun () -> let d = Seq.toArray vtxValDirty in vtxValDirty.Clear(); d)
                        for pos in dirty do if pos < n then vtxStamp t pos
                    // the SSBO binding indexes [0,n): hand back the persistent array
                    // when it is exactly sized, else a snapshot (ref-copy, no aval reads).
                    if vtxOut.Length = n then vtxOut else Array.sub vtxOut 0 (max 1 n) } :> aval<IBuffer[]>
        // page 0's HeapData binding: PAGE 0's arena explicitly (NOT the mutable `arena`, which the
        // add path re-points at the current fill page). pages >0 bind their own arena in ensurePageROs.
        let arenaU = ((storage.Page(0).Arena :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)) :> IAdaptiveValue
        let headersU = headersAval :> IAdaptiveValue

        // ── derived-uniform COMPUTE dispatch (fp64, once per slot) ────────
        // Two compute passes write straight into the arena the render gathers (no
        // per-vertex derivation):
        //   (1) chainMode only — fold each slot's Model link chain in fp64 and write
        //       the result as M44d into the slot's Model FORWARD constituent region.
        //   (2) composeDerived — for every consumed composite, read its constituents
        //       (Model/View/Proj fwd/bwd) from the arena double view and write the
        //       result (f32) to the slot's output region.
        // Hung off arenaU's pull (constituents staged), the membership updater (slot
        // add/remove ⇒ structure re-upload), and — chainMode — the link arena (a
        // shared parent edit ⇒ ONE link upload ⇒ re-dispatch). A camera move marks a
        // shared View/Proj constituent ⇒ arena re-stage ⇒ re-dispatch, O(1) CPU.
        // ACQUISITION-PROPAGATING & rule-clean (no transact in evaluation).
        let chainActive = chainMode && modelFwdCell >= 0
        // backward fold: chainMode + a consumed recipe that needs Model⁻¹ (NormalMatrix
        // / *Inv). Folds the links' uploaded Backward halves in array order.
        let chainBwdActive = chainMode && modelBwdCell >= 0
        // `df32` is the bucket-wide path flag (hoisted to the top of IncrementalBucket).
        // NB: the df32 / fp64 kernels have DIFFERENT signatures (links : V2f[] vs
        // M44d[]), so each CreateComputeShader call must be its own branch — they
        // unify at the IComputeShader result, not at the function value.
        let chainShader =
            if not chainActive then Unchecked.defaultof<_>
            elif df32 then runtime.CreateComputeShader Chain.composeModelDf32
            else runtime.CreateComputeShader Chain.composeModel
        let chainInput  = if chainActive then runtime.CreateInputBinding chainShader else Unchecked.defaultof<_>
        let chainInvShader =
            if not chainBwdActive then Unchecked.defaultof<_>
            elif df32 then runtime.CreateComputeShader Chain.composeModelInvDf32
            else runtime.CreateComputeShader Chain.composeModelInv
        let chainInvInput  = if chainBwdActive then runtime.CreateInputBinding chainInvShader else Unchecked.defaultof<_>
        let derivedShader =
            if not hasDerived then Unchecked.defaultof<_>
            elif df32 then runtime.CreateComputeShader Derived.composeDerivedDf32
            else runtime.CreateComputeShader Derived.composeDerived
        // the derive's records buffer is bucket-constant: upload it ONCE, eagerly, so the
        // pure-aval provider below can hand it out as a constant (no lazy init inside an eval).
        let recBuf : IBuffer<int> =
            if not hasDerived then Unchecked.defaultof<_>
            else
                let b = runtime.CreateBuffer<int>(max 1 derivedRecords.Length, BufferUsage.Write ||| BufferUsage.Storage)
                if derivedRecords.Length > 0 then b.Upload(derivedRecords, 0, 0, derivedRecords.Length)
                b
        // The derive's input binding is a PURE aval-based provider (NOT a
        // MutableComputeInputBinding): its values track the heap's live buffer/scalar avals
        // directly, so the backend descriptor stays current with NO manual Flush. Flush uses
        // `transact`, which is unsafe here — the derive dispatch runs render-integrated in a
        // SEPARATE resource update (the pre-pass), reading the descriptor asynchronously, so
        // a deferred transact would race. (The chain folds keep Flush + an IMMEDIATE Run, so
        // their values are consumed synchronously in the same eval — no hazard there.)
        // PER-OUTPUT dispatch: n = live SHARE count (distinct derived values), not
        // slots — 246k threads discovering they own nothing cost 7.1 ms/frame on a
        // 2-CU APU while ONE thread computed the single deduped ViewProj.
        let nAvalDerive = if hasDerived then AVal.custom (fun t -> updater.GetValue t |> ignore; shareList.Count) else AVal.constant 0
        let shareRecsU = ((shareRecBuf :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)) :> IAdaptiveValue
        // PAGED: one derive input binding per page — binds THAT page's arena + HeapPageId so the
        // guarded shader writes only page-i slots into page i's arena.
        let pageDeriveInputs = System.Collections.Generic.List<IComputeInputBinding>()
        let mkDerivedInput (pageArenaU : IAdaptiveValue) (pid : int) : IComputeInputBinding =
            let provider =
                { new IUniformProvider with
                    member _.TryGetUniform(_, name) =
                        match string name with
                        | "n"            -> ValueSome (nAvalDerive :> IAdaptiveValue)
                        | "hstride"      -> ValueSome (AVal.constant headerStride :> IAdaptiveValue)
                        | "records"      -> ValueSome (AVal.constant (recBuf :> IBuffer) :> IAdaptiveValue)
                        | "HeapShareRecs" -> ValueSome shareRecsU
                        | "HeapHeaders"  -> ValueSome headersU
                        | "HeapSlotPage" -> ValueSome slotPageU
                        | "HeapPageId"   -> ValueSome (AVal.constant pid :> IAdaptiveValue)
                        | "HeapDataD"    -> ValueSome pageArenaU
                        | "HeapData"     -> ValueSome pageArenaU
                        | "HeapUni"      -> ValueSome uniBufU
                        | _              -> ValueNone
                    member _.Dispose() = () }
            let inp = runtime.CreateInputBinding(derivedShader, provider)
            pageDeriveInputs.Add inp
            inp
        let mutable chOffBuf : IBuffer<int> = Unchecked.defaultof<_>
        let mutable chLenBuf : IBuffer<int> = Unchecked.defaultof<_>
        let mutable chIdxBuf : IBuffer<int> = Unchecked.defaultof<_>
        let mutable chainCap = 0
        let mutable chIdxBufCap = 0
        // PERSISTENT compute programs (compile once, recompile only when the dispatch
        // group count changes) — `runtime.Run` builds + submits + WAITS on a throwaway
        // command buffer every call, which on a camera orbit (composeDerived re-runs
        // each frame) dominated the frame time. CompileCompute amortizes the build;
        // the input bindings are mutable, so updating values + Run re-executes.
        // the per-slot derive runs as a render-integrated pre-pass inside the bucket's
        // HeapRenderObject (no IComputeTask/Run) — this is just its dispatch group
        // count, reactive on membership (highWater).
        let derivedGroups =
            if not hasDerived then AVal.constant V3i.III
            else AVal.custom (fun t -> updater.GetValue t |> ignore; V3i(max 1 ((shareList.Count + 63) / 64), 1, 1))
        let mutable chainProg : IComputeTask = Unchecked.defaultof<_>
        let mutable chainInvProg : IComputeTask = Unchecked.defaultof<_>
        let mutable chainProgG = -1
        // the chain fold output (Model fwd/bwd constituents) is camera-INDEPENDENT;
        // re-dispatch it ONLY when the chain structure or a link value changed, never
        // on a pure camera move. addChain/removeChain set the structure-dirty flag;
        // GrowChainLinks bumps its Generation on a link upload.
        let mutable chainFoldStale = chainActive
        let mutable lastLinkGen = -1
        let derivedU =
            // derivedU survives only for CHAIN buckets: it folds the Model chain into
            // EACH page's arena (camera-independent, edit-gated, guarded per page) before
            // the derive reads it. Non-chain: HeapData is simply the arena buffer.
            if not hasDerived || not chainActive then arenaU
            else
                AVal.custom (fun t ->
                    updater.GetValue t |> ignore
                    // arena buffer with constituents staged (View/Proj uploaded;
                    // Model space allocated) + the per-slot header offsets, current.
                    let arenaBuf = (storage.Page(0).Arena :> aval<IBackendBuffer>).GetValue t
                    let hdrBuf   = (headersBuf :> aval<IBackendBuffer>).GetValue t
                    // (1) chain fold → Model fwd/bwd constituents — re-dispatched ONLY
                    // when the chain structure or a link value changed (the fold output
                    // is camera-independent, so a pure camera move skips it entirely).
                    if chainActive then
                        let linkBuf = (chainLinks :> aval<IBackendBuffer>).GetValue t
                        if chainLinks.Generation <> lastLinkGen then lastLinkGen <- chainLinks.Generation; chainFoldStale <- true
                        let n = max 1 highWater
                        if n > chainCap then
                            let cap = Fun.NextPowerOfTwo n
                            if not (isNull (box chOffBuf)) then chOffBuf.Dispose(); chLenBuf.Dispose()
                            chOffBuf <- runtime.CreateBuffer<int>(cap, BufferUsage.Write ||| BufferUsage.Storage)
                            chLenBuf <- runtime.CreateBuffer<int>(cap, BufferUsage.Write ||| BufferUsage.Storage)
                            chainCap <- cap
                            chainStructAllDirty <- true
                        let idxExtent = max 1 chIdxAlloc.Extent
                        if idxExtent > chIdxBufCap then
                            let cap = Fun.NextPowerOfTwo idxExtent
                            if not (isNull (box chIdxBuf)) then chIdxBuf.Dispose()
                            chIdxBuf <- runtime.CreateBuffer<int>(cap, BufferUsage.Write ||| BufferUsage.Storage)
                            chIdxBufCap <- cap
                            chainStructAllDirty <- true
                        if chainStructAllDirty || chainDirtyStruct.Count > 0 then
                            chainStructAllDirty <- false
                            chainDirtyStruct.Clear()
                            chOffBuf.Upload(chOffset, 0, 0, highWater)
                            chLenBuf.Upload(chLen, 0, 0, highWater)
                            if chIdxAlloc.Extent > 0 then chIdxBuf.Upload(chIdx, 0, 0, chIdxAlloc.Extent)
                            chainFoldStale <- true
                        if chainFoldStale then
                            chainFoldStale <- false
                            let g = (highWater + chainShader.LocalSize.X - 1) / chainShader.LocalSize.X
                            let slotPageVal = (slotPageBuf :> aval<IBackendBuffer>).GetValue t
                            chainInput.["n"]           <- highWater
                            chainInput.["hstride"]     <- headerStride
                            chainInput.["mCell"]       <- modelFwdCell
                            chainInput.["chainOffset"] <- chOffBuf
                            chainInput.["chainLen"]    <- chLenBuf
                            chainInput.["linkIdx"]     <- chIdxBuf
                            chainInput.["links"]       <- linkBuf
                            chainInput.["HeapHeaders"] <- hdrBuf
                            chainInput.["HeapSlotPage"] <- slotPageVal
                            chainInput.["HeapPageId"]  <- 0
                            // df32 kernel writes the folded Model via the f32 arena view
                            // (HeapData, hi/lo pairs); fp64 via the native double view.
                            if df32 then chainInput.["HeapData"] <- arenaBuf
                            else chainInput.["HeapDataD"] <- arenaBuf
                            chainInput.Flush()
                            if chainBwdActive then
                                chainInvInput.["n"]           <- highWater
                                chainInvInput.["hstride"]     <- headerStride
                                chainInvInput.["mCell"]       <- modelBwdCell
                                chainInvInput.["chainOffset"] <- chOffBuf
                                chainInvInput.["chainLen"]    <- chLenBuf
                                chainInvInput.["linkIdx"]     <- chIdxBuf
                                chainInvInput.["links"]       <- linkBuf
                                chainInvInput.["HeapHeaders"] <- hdrBuf
                                chainInvInput.["HeapSlotPage"] <- slotPageVal
                                chainInvInput.["HeapPageId"]  <- 0
                                if df32 then chainInvInput.["HeapData"] <- arenaBuf
                                else chainInvInput.["HeapDataD"] <- arenaBuf
                                chainInvInput.Flush()
                            if chainProgG <> g || isNull (box chainProg) then
                                if not (isNull (box chainProg)) then chainProg.Dispose()
                                chainProg <- runtime.CompileCompute [ ComputeCommand.Bind chainShader; ComputeCommand.SetInput chainInput; ComputeCommand.Dispatch (max 1 g) ]
                                if chainBwdActive then
                                    if not (isNull (box chainInvProg)) then chainInvProg.Dispose()
                                    chainInvProg <- runtime.CompileCompute [ ComputeCommand.Bind chainInvShader; ComputeCommand.SetInput chainInvInput; ComputeCommand.Dispatch (max 1 g) ]
                                chainProgG <- g
                            // PER-PAGE fold: each page's slots fold into THAT page's arena (kernel
                            // guards HeapSlotPage = HeapPageId), so page>0 slots no longer clobber
                            // page 0. Mirrors composeDerived's per-page dispatch.
                            for pg in 0 .. storage.Count - 1 do
                                let pArena = (storage.Page(pg).Arena :> aval<IBackendBuffer>).GetValue t
                                chainInput.["HeapPageId"] <- pg
                                if df32 then chainInput.["HeapData"] <- pArena else chainInput.["HeapDataD"] <- pArena
                                chainInput.Flush()
                                chainProg.Run()
                                if chainBwdActive then
                                    chainInvInput.["HeapPageId"] <- pg
                                    if df32 then chainInvInput.["HeapData"] <- pArena else chainInvInput.["HeapDataD"] <- pArena
                                    chainInvInput.Flush()
                                    chainInvProg.Run()
                    // the Model chain is now folded into the arena; the render-integrated
                    // derive pre-pass reads it. HeapData = the arena buffer.
                    arenaBuf :> IBuffer) :> IAdaptiveValue

        // texture / atlas / vertex-pull uniform bindings of the bucket RO
        let texLookup = System.Collections.Generic.Dictionary<Symbol, IAdaptiveValue>(HashIdentity.Structural)
        do
            match atlasState with
            | Some (pool, dummy, table) ->
                let t = table :> aval<V4f[] * V4f[] * int[] * int[]>
                // padded pages[8]: pulled AFTER the placement table — whose Compute
                // performs the Acquires that may CREATE pages — so page publication
                // needs no transact inside evaluation (rule-clean); the table
                // dependency re-pulls this whenever membership or a member's texture
                // changed, i.e. whenever a page can possibly have been added. Unused
                // tail slots point at the per-bucket dummy texture.
                let padded =
                    AVal.custom (fun tok ->
                        t.GetValue tok |> ignore
                        let arr = pool.PageArray
                        let out = Array.zeroCreate<ITexture> HeapAtlas.MaxPagesPerFormat
                        for i in 0 .. HeapAtlas.MaxPagesPerFormat - 1 do
                            out.[i] <- if i < arr.Length then arr.[i] :> ITexture else dummy
                        out)
                for i in 0 .. HeapAtlas.MaxPagesPerFormat - 1 do
                    let pi = i
                    texLookup.[Symbol.Create (sprintf "HeapAtlasTex%d" pi)] <- (padded |> AVal.map (fun arr -> arr.[pi])) :> IAdaptiveValue
                texLookup.[Symbol.Create "HeapAtlasPxSize"] <- AVal.constant (V2f(float32 HeapAtlas.PageSize, float32 HeapAtlas.PageSize)) :> IAdaptiveValue
                texLookup.[Symbol.Create "HeapAtlasOrigin"] <- (t |> AVal.map (fun (o, _, _, _) -> o)) :> IAdaptiveValue
                texLookup.[Symbol.Create "HeapAtlasSize"]   <- (t |> AVal.map (fun (_, z, _, _) -> z)) :> IAdaptiveValue
                texLookup.[Symbol.Create "HeapAtlasFmt"]    <- (t |> AVal.map (fun (_, _, f, _) -> f)) :> IAdaptiveValue
                texLookup.[Symbol.Create "HeapAtlasPageId"] <- (t |> AVal.map (fun (_, _, _, p) -> p)) :> IAdaptiveValue
            | None -> ()
            for (arrName, idxName, _, table) in bindlessTexTables do
                // textures as an amap (incremental, O(changed)); the backend's prepare path
                // detects the aval<amap<…>> and skips AMap.ofAVal. Indices stay an aval<int[]>.
                texLookup.[Symbol.Create arrName] <- AVal.constant table.Textures :> IAdaptiveValue
                texLookup.[Symbol.Create idxName] <- table.Indices :> IAdaptiveValue
            if useBindlessGeom then
                // the SAME per-slot buffers bound as both a float and an int view
                let u = vtxGatherAval :> IAdaptiveValue
                texLookup.[Symbol.Create "HeapVertexData"] <- u
                texLookup.[Symbol.Create "HeapVertexDataI"] <- u
            // user-managed sampler arrays: bind the (shared) RO-supplied array through.
            for n in userSamplerArrays do
                match ro0.Uniforms.TryGetUniform(scope, Symbol.Create n) with
                | ValueSome v -> texLookup.[Symbol.Create n] <- v
                | ValueNone -> ()

        // ── the heap shader rewrite: ONE SubstituteReads pass per shader ──
        // Per-draw uniform fields (all stages) and the storage-decoded geometry
        // (vertex stage only: attributes + the let-bound decoded vertex index)
        // are substituted in a SINGLE pass. This matters: FShade's preprocessor
        // records a uniform's SCOPE from the accessor expression, and a later
        // pass mixing fresh `uniform?StorageBuffer?HeapData` reads with the
        // previous pass's already-desugared ReadInput nodes of the same name
        // trips its conflicting-scope validation. Derived composites are NOT
        // expanded/inlined here — they are produced by the per-slot fp64 compute
        // pass and stored in their own arena OUTPUT region, so a `ModelViewProjTrafo`
        // / `NormalMatrix` / `ModelTrafo` read is just another field gather (its cell
        // is `nameToField.[name]`), never a per-vertex matrix product.
        //   vid = decodeHeapIndex(headers[slot].idxRef, gl_VertexIndex) — the
        // index ELEMENT TYPE comes from the allocation header, not the bucket
        // key. Attribute reads become
        //   * host buckets:     header-driven arena gathers branching on the
        //                       allocation's typeId (decodeHeapV4f/V4i): the
        //                       SOURCE element type is per allocation, auto-
        //                       converted to the shader input type; length
        //                       from the header wraps singletons,
        //   * bindless buckets: per-handle SSBO-array vertex-pull (the
        //                       objects' EXISTING GPU buffers, zero-copy).
        let heapRewrite : Effect -> Effect =
            let handleE = slotE.Raw
            let vtxRawE : Expr<int> = Expr.Cast (Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.VertexId))
            // INSTANCE-RATE RECORD ROW (clusters): the hot per-slot record fields
            // arrive as HeapRec0.. instance attributes — hardware-fetched at wave
            // launch (FirstInstance-addressed), NO dependent load in the chain.
            let recIn (i : int) : Expr<int> = Expr.Cast (Expr.ReadInput<int>(ParameterKind.Input, sprintf "HeapRec%d" i))
            // CLUSTERED records are padded to the class size: clamp the vertex cursor
            // to the slot's real count — padding lanes all re-shade the LAST vertex,
            // so padding triangles are zero-area and culled.
            let vtxE : Expr<int> =
                if useClusters then <@ min %vtxRawE (%(recIn 1) - 1) @>
                else vtxRawE
            let idxRefE : Expr<int> =
                if useClusters then recIn 2
                else <@ uniform.HeapHeaders.[ %slotE * %(cint headerStride) + %(cint idxCell) ] @>
            let fieldOff (fi : int) : Expr<int> = <@ uniform.HeapHeaders.[ %slotE * %(cint headerStride) + %(cint fi) ] @>
            fun e ->
                // PICKING: rewrite a `uniform.HeapPickId` read (from the dom heap pick-fragment)
                // into the per-slot HeapPickIds SSBO gather — BY NAME, using the same slotE the heap
                // uses for every other uniform (FShade assigns the actual in/out locations). So the
                // pick fragment stays handle-agnostic; the rewrite owns the slot.
                let e =
                    if picking then
                        e |> Effect.substituteUniforms (fun name _ _ _ ->
                            if name = "HeapPickId" then Some (<@@ (uniform.HeapPickIds : int[]).[ %slotE ] @@>) else None)
                    else e
                // ONE pass per shader — derived composites are already materialized in
                // their arena output region by the compute pass, so every uniform read
                // (composite or plain) is a single field gather.
                e |> Effect.map (fun sh ->
                    let isVertex = sh.shaderStage = ShaderStage.Vertex
                    let vidVar = Var("heapVid", typeof<int>)
                    let vidE : Expr<int> = Expr.Cast (Expr.Var vidVar)
                    // HOIST every gather into ONE let per (name, type) per stage.
                    // SubstituteReads splices its result into EVERY read site, so a
                    // field read N times (e.g. ModelTrafo for the position AND the
                    // normal) would otherwise re-materialize the whole gather N times
                    // — for a matrix that's N×16 scalar loads + N mat4 constructions
                    // the GLSL compiler does not reliably CSE (measured ~6ms/frame at
                    // 700k objects / 115M verts). Reads become a Var; the gathers are
                    // bound once at the top of the body (after `heapVid`, which the
                    // attribute gathers reference).
                    let hoisted = System.Collections.Generic.Dictionary<struct(string * System.Type), Var>()
                    let hoistOrder = System.Collections.Generic.List<Var * Expr>()
                    let hoist (name : string) (ityp : System.Type) (mk : unit -> Expr) : Expr =
                        let key = struct(name, ityp)
                        match hoisted.TryGetValue key with
                        | true, v -> Expr.Var v
                        | _ ->
                            let v = Var("heap_" + name, ityp)
                            hoisted.[key] <- v
                            hoistOrder.Add(v, mk ())
                            Expr.Var v
                    let body =
                        sh.shaderBody.SubstituteReads (fun kind ityp name idx _ ->
                            match kind, idx with
                            | ParameterKind.Uniform, None ->
                                match Map.tryFind name nameToField with
                                | Some fi ->
                                    let g = if derivedCells.Contains fi then uniGatherFor else gatherFor
                                    Some (hoist name ityp (fun () -> g ityp (fieldOff fi)))
                                | None -> None
                            | ParameterKind.Input, None when isVertex ->
                                match attrInfos |> Array.tryFind (fun (_, n, _, _, _, _, _) -> n = name) with
                                | Some (ai, _, _, _, _, strideF, offF) ->
                                    let mk () =
                                        if useBindlessGeom then
                                            bindlessGatherFlat handleE vidE.Raw ityp numAttrs ai strideF offF
                                        else
                                            let refE : Expr<int> =
                                                if useClusters && ai < rowAttrs then recIn (3 + ai)
                                                else <@ uniform.HeapHeaders.[ %slotE * %(cint headerStride) + %(cint (attrBase + ai)) ] @>
                                            match hostGather (heapTidRead ai) ityp refE vidE with
                                            | Some g -> g
                                            | None -> failwithf "Heap: cannot storage-decode shader input '%s' (%A — supported: float32/V2f/V3f/V4f and int/V2i/V3i/V4i)" name ityp
                                    Some (hoist name ityp mk)
                                | None -> None
                            | _ -> None)
                    // bind the hoisted gathers (mutually independent — they reference
                    // only slot/vid), then `heapVid` OUTERMOST so attribute gathers see it
                    let body = Seq.foldBack (fun (v, ge) b -> Expr.Let(v, ge, b)) hoistOrder body
                    let body = if isVertex then Expr.Let(vidVar, (<@ decodeHeapIndex uniform.HeapTidIdx %idxRefE %vtxE @>).Raw, body) else body
                    // CLUSTERED slot routing: bind the ClassSlots indirection ONCE,
                    // OUTERMOST (the vid let + every gather reference it via slotE)
                    let body =
                        if useClusters then
                            Expr.Let(slotVar, (recIn 0).Raw, body)
                        else body
                    Shader.withBody body sh)

        // the bucket's render object — created ONCE; identity is stable across
        // membership changes. Rewritten surface, indirect draws, and a uniform
        // provider answering ONLY heap-internal names (arena/headers/textures/
        // atlas/user sampler arrays) — user uniforms are gathered regions, never
        // resolved from a member.
        // GL fallback lookups for the DYNAMIC partition: all tids 0 (Vulkan
        // never asks — the members live in the SpecConstants scope, which has
        // no UBO in its program interface). Typed clones override per name.
        let specUniformLookup =
            let d = System.Collections.Generic.Dictionary<Symbol, IAdaptiveValue>()
            let zero = AVal.constant 0 :> IAdaptiveValue
            for i in 0 .. 7 do d.[Symbol.Create (sprintf "HeapTid%d" i)] <- zero
            d.[Symbol.Create "HeapTidIdx"] <- zero
            d

        let bucketRO =
            let ro = RenderObject.Clone ro0
            ro.IsActive <- AVal.constant true      // per-draw gating lives in the indirect buffer
            ro.SpecConstants <- AVal.constant Map.empty     // DYNAMIC partition: unspecialized
            // ALL pipeline state comes from the bucket KEY's resolved VALUES, baked
            // as constants — never from a member's live avals: a member whose
            // dynamic state aval changes MOVES buckets (regroup pass) and must not
            // be able to bend the bucket it leaves. Blend state is per-attachment:
            // the key carries the effective (mode, mask) for every signature color
            // attachment the effect writes; unwritten signature attachments are
            // explicitly write-masked off, so their pixels keep the cleared value
            // by construction (not by unwritten-shader-output undefinedness). The
            // globals (Mode/ColorWriteMask) are then never consulted — every
            // signature attachment has an explicit entry — and are set to defaults.
            ro.RenderPass <- pipeKey.Pass
            ro.RasterizerState <-
                { CullMode = AVal.constant pipeKey.Cull
                  FrontFacing = AVal.constant pipeKey.FrontFacing
                  FillMode = AVal.constant pipeKey.Fill
                  Multisample = AVal.constant pipeKey.Multisample
                  ConservativeRaster = AVal.constant pipeKey.ConservativeRaster }
            ro.BlendState <-
                let modes = pipeKey.Blend |> Array.map (fun (n, m, _) -> Symbol.Create n, m) |> Map.ofArray
                let masks = pipeKey.Blend |> Array.map (fun (n, _, mask) -> Symbol.Create n, mask) |> Map.ofArray
                let masks = maskedAttachments |> Array.fold (fun m s -> Map.add s ColorMask.None m) masks
                { Mode                = AVal.constant BlendMode.None
                  ColorWriteMask      = AVal.constant ColorMask.All
                  ConstantColor       = AVal.constant (defaultArg pipeKey.BlendConstant C4f.Black)
                  AttachmentMode      = AVal.constant modes
                  AttachmentWriteMask = AVal.constant masks }
            ro.DepthState <-
                match pipeKey.Depth with
                | Some (test, bias, write, clamp) ->
                    { Test = AVal.constant test
                      Bias = AVal.constant bias
                      WriteMask = AVal.constant write
                      Clamp = AVal.constant clamp }
                | None -> DepthState.Default    // signature has no depth attachment
            ro.StencilState <-
                match pipeKey.Stencil with
                | Some (modeF, maskF, modeB, maskB) ->
                    { ModeFront = AVal.constant modeF
                      WriteMaskFront = AVal.constant maskF
                      ModeBack = AVal.constant modeB
                      WriteMaskBack = AVal.constant maskB }
                | None -> StencilState.Default  // signature has no stencil attachment
            ro.ViewportState <-
                { Viewport = pipeKey.Viewport |> Option.map AVal.constant
                  Scissor  = pipeKey.Scissor  |> Option.map AVal.constant }
            ro.Surface <-
                let baseE = heapRewrite effect
                let withSamplers =
                    if useAtlas then baseE |> rewriteAtlasSamples slotE atlasByName
                    elif samplers.Length > 0 then baseE |> rewriteSamplers slotE samplerByName |> overrideSamplerStates samplerStateOverrides
                    else baseE
                // CLUSTERED: later passes (sampler/atlas rewrites) splice slotVar into
                // stages whose (unused) binding was already dropped — bind the
                // ClassSlots indirection wherever slotVar is still FREE.
                let final =
                    if not useClusters then withSamplers
                    else
                        withSamplers |> Effect.map (fun sh ->
                            if sh.shaderBody.GetFreeVars() |> Seq.exists (fun v -> v = slotVar) then
                                let slotRead = (Expr.ReadInput<int>(ParameterKind.Input, "HeapRec0")).Raw
                                Shader.withBody (Expr.Let(slotVar, slotRead, sh.shaderBody)) sh
                            else sh)
                Surface.Effect final
            ro.DrawCalls <- DrawCalls.Indirect indirectAval
            // NO fixed-function vertex input: attributes are storage-decoded
            // (host: arena allocations; bindless: SSBO descriptor array).
            ro.VertexAttributes <- AttributeProvider.ofList ([] : (Symbol * BufferView) list)
            // MoltenVK+instanced fallback: bind the per-instance slot attribute.
            // Every other path gets an EMPTY instance provider: isHeapable requires
            // every shader input to resolve from VertexAttributes, so no heapable
            // effect can read an instance attribute — keeping ro0's cloned provider
            // would only retain the dead ro0 (and would bind ro0's per-RO data for
            // ALL members if it ever overlapped a semantic).
            if useSlotAttr then
                ro.InstanceAttributes <- AttributeProvider.ofList [ symSlotAttr, BufferView(instAval, typeof<int>) ]
            elif useClusters then
                // the instance-record row: HeapRec0..N as int attributes striding
                // over the (page,partition,class)-regioned rows buffer — the
                // records' FirstInstance addresses the row, hardware fetches it
                ro.InstanceAttributes <-
                    AttributeProvider.ofList
                        [ for i in 0 .. rowWords - 1 ->
                            Symbol.Create (sprintf "HeapRec%d" i), BufferView(classRowsAval, typeof<int>, i * 4, rowWords * 4) ]
            else
                ro.InstanceAttributes <- AttributeProvider.ofList ([] : (Symbol * BufferView) list)
            // indices are storage-decoded too: draws are NON-indexed
            ro.Indices <- None
            ro.Uniforms <-
                { new IUniformProvider with
                    member _.TryGetUniform(s, name) =
                        // HeapData + HeapDataI: the SAME arena buffer, bound as a
                        // float and as an int view (headers / indices / integral
                        // attributes decode the int view).
                        // HeapData/I/D bind `derivedU` (= the arena buffer AFTER the
                        // per-slot fp64 compute has written every composite into it),
                        // so demanding the render triggers the derive pass; identical
                        // to arenaU when the bucket has no derived uniforms.
                        if name = symData || name = symDataI || name = symDataD then ValueSome derivedU
                        elif name = symHeaders then ValueSome headersU
                        // GL fallback: the spec-constant tids as live uniforms
                        // (same values, runtime-branched instead of dead-coded)
                        elif specUniformLookup.ContainsKey name then ValueSome specUniformLookup.[name]
                        elif picking && name = symPickIds then ValueSome pickIdU
                        elif useClusters && name = symClassSlots then ValueSome classSlotsU
                        elif hasDerived && (name = symUni || name = symUniD) then ValueSome uniBufU
                        else
                            match texLookup.TryGetValue name with
                            | true, v -> ValueSome v
                            | _ ->
                                // every user uniform is a gathered region (rewritten
                                // out of the shader); nothing falls through to a global.
                                ValueNone
                    member _.Dispose() = () }
            ro

        // The bucket is ONE HeapRenderObject carrying its per-page derive dispatches +
        // per-page draws. The Vulkan backend records all derives as a compute pre-pass →
        // compute→vertex barrier → all draws, in ONE command buffer/submission: so each
        // page draws against its OWN fresh derive (no page>0 staleness) and no render-task
        // split (e.g. Dom's pickable/non-pickable) can separate a derive from its draws.
        // The draws are recorded directly — NOT wrapped in OrderedCmd+RenderCmd, which
        // would break the dynamic indirect bucket's membership churn (GPU hang).
        // PAGED: one derive dispatch per page binds page i's arena + HeapPageId=i; the
        // guarded shader writes ONLY page-i slots into page i's arena.
        // PER-PAGE derive specs: one (shader, groups, prepared input) per live page. The page-i input
        // binds page i's arena; the guarded shader writes ONLY page-i slots into page i's arena. These
        // are carried by the bucket's HeapRenderObject; the Vulkan backend records each as a compute
        // pre-pass (ending in a compute→vertex barrier) in the SAME submission as the bucket's draws,
        // so page i is always drawn against its own freshly-derived arena (kills the [ts] page>0 stale).
        let deriveSpecs = System.Collections.Generic.List<IComputeShader * aval<V3i> * Map<string, obj>>()
        let ensureDeriveROs () =
            if hasDerived then
                while deriveSpecs.Count < storage.Count do
                    let i = deriveSpecs.Count
                    let pageArenaU = ((storage.Page(i).Arena :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)) :> IAdaptiveValue
                    deriveSpecs.Add(derivedShader, derivedGroups, Map.ofList [ "__input", box (mkDerivedInput pageArenaU i) ])

        // PAGED draw fan-out. page 0 = `bucketRO` (the incremental machinery above, now zeroing
        // non-page-0 slots). pages >0 each get a fresh indirect (its slots only, full-rewrite flush)
        // + a clone of `bucketRO` binding page i's arena. `resultAval` is rebuilt per membership
        // version, so newly-rolled pages appear. FOLLOW-UP: per-page derive — pages >0 bind their
        // plain arena, so any derived/fp64/chain uniform there is page-0-stale until that lands.
        // ── (page, partition) RO clones. (0,0) = bucketRO (the incremental
        //    machinery above). Every OTHER combination gets its own indirect
        //    buffer (that partition's records only) + a clone: page > 0 binds
        //    that page's arena; partition > 0 carries the assignment's CONSTANT
        //    spec-value map (typed pipeline) + GL uniform overrides. Clones are
        //    created lazily and never destroyed (a dematerialized partition's
        //    lists empty out -> 0 records; the epoch-keyed HeapRenderObject
        //    rebuild drops it from the draw list). ──
        let partROs = System.Collections.Generic.Dictionary<struct(int * int), IRenderObject>()
        let mkPartRO (pageIdx : int) (partIdx : int) =
            let mutable pstaging = Array.zeroCreate<DrawCallInfo> (max 16 entries.Length)
            let db = MirrorBuffer(runtime, pstaging.Length * sizeof<DrawCallInfo>, BufferUsage.Indirect)
            let mutable lastRecsV = -1
            let flush (_ : AdaptiveToken) (_ : System.Collections.Generic.HashSet<GateWriter>) =
                if useClusters then
                  if lastRecsV <> recsVersion then
                    lastRecsV <- recsVersion
                    let recs = System.Collections.Generic.List<DrawCallInfo>()
                    clusterRecordsFor pageIdx partIdx recs
                    setClusterRecCount pageIdx partIdx recs.Count
                    if System.Environment.GetEnvironmentVariable "HEAP_DUMP_RECORDS" = "1" then
                        let mutable z0i = 0
                        let mutable z0v = 0
                        let mutable maxFi = 0
                        for r in recs do
                            if r.InstanceCount = 0 then z0i <- z0i + 1
                            if r.FaceVertexCount = 0 then z0v <- z0v + 1
                            maxFi <- max maxFi r.FirstInstance
                        Log.line "[recdump] page=%d part=%d records=%d zeroInst=%d zeroFvc=%d maxFirstInstance=%d" pageIdx partIdx recs.Count z0i z0v maxFi
                    if pstaging.Length < recs.Count then pstaging <- Array.zeroCreate (Fun.NextPowerOfTwo (max 16 recs.Count))
                    db.ResizeInPlace(uint64 (max 16 pstaging.Length * sizeof<DrawCallInfo>))
                    for i in 0 .. recs.Count - 1 do pstaging.[i] <- recs.[i]
                    if recs.Count > 0 then db.Write(pstaging, 0UL, 0, recs.Count)
                else
                if pstaging.Length < entries.Length then pstaging <- Array.zeroCreate entries.Length
                db.ResizeInPlace(uint64 (pstaging.Length * sizeof<DrawCallInfo>))
                for s in 0 .. highWater - 1 do
                    pstaging.[s] <- (if slotPage.[s] = pageIdx then entries.[s] else zeroDraw)
                if highWater > 0 then db.Write(pstaging, 0UL, 0, highWater)
            db.Dependency <- Some (updater :> IAdaptiveValue)
            db.Flush <- flush
            db.Name <- "HeapIndirectPart"
            let indirectP =
                (db :> aval<IBackendBuffer>)
                |> AdaptiveResource.mapNonAdaptive (fun b ->
                    let cnt = if useClusters then clusterRecCount pageIdx partIdx else highWater
                    IndirectBuffer.ofBuffer false 0UL sizeof<DrawCallInfo> cnt (b :> IBuffer))
            let pageArenaU =
                if pageIdx = 0 then ValueNone
                else ValueSome (((storage.Page(pageIdx).Arena :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)) :> IAdaptiveValue)
            let part = if partIdx > 0 && partIdx < partById.Count then partById.[partIdx] else Unchecked.defaultof<HeapPartition>
            let specGl =
                let d = System.Collections.Generic.Dictionary<Symbol, IAdaptiveValue>()
                if partIdx > 0 then
                    for KeyValue(n, v) in part.TidMap do d.[Symbol.Create n] <- (AVal.constant v :> IAdaptiveValue)
                d
            let ro = RenderObject.Clone bucketRO
            ro.DrawCalls <- DrawCalls.Indirect indirectP
            // TYPED partition: the assignment's constant spec values (the "JIT tier")
            if partIdx > 0 then ro.SpecConstants <- AVal.constant part.TidMap
            ro.Uniforms <-
                { new IUniformProvider with
                    member _.TryGetUniform(s, name) =
                        if pageArenaU.IsSome && (name = symData || name = symDataI || name = symDataD) then pageArenaU
                        elif specGl.ContainsKey name then ValueSome specGl.[name]
                        else bucketRO.Uniforms.TryGetUniform(s, name)
                    member _.Dispose() = () }
            ro :> IRenderObject
        // same gate key as buildHeapRO: the clone set only changes when a page appears
        // or a partition (de)materializes — content-only edits skip the pages×partitions
        // dictionary walk entirely (it was ~0.7ms per edit batch at 68k parts).
        let mutable pageROsPageCount = -1
        let mutable pageROsEpoch = -1
        let ensurePageROs () =
          if pageROsPageCount <> storage.Count || pageROsEpoch <> partEpoch then
            pageROsPageCount <- storage.Count
            pageROsEpoch <- partEpoch
            // prune clones of DEAD partitions (dematerialized / re-materialized
            // under a fresh id): dropping the dict reference releases the clone
            // and its indirect MirrorBuffer through the normal resource refcounts
            // (the epoch-keyed HeapRenderObject rebuild already removed them from
            // the draw list).
            if partROs.Count > 0 then
                let dead =
                    [ for KeyValue(k, _) in partROs do
                        let struct(_, pid) = k
                        if pid > 0 && (pid >= partById.Count || not partById.[pid].Materialized || partById.[pid].Id <> pid) then
                            yield k ]
                for k in dead do partROs.Remove k |> ignore
            for pageIdx in 0 .. storage.Count - 1 do
                // dynamic partition clone per page (page 0 = bucketRO itself)
                if pageIdx > 0 && not (partROs.ContainsKey(struct(pageIdx, 0))) then
                    partROs.[struct(pageIdx, 0)] <- mkPartRO pageIdx 0
                // typed partition clones (materialized assignments only)
                if useClusters then
                    for pid in 1 .. partById.Count - 1 do
                        // Id = pid guards against RE-materialized partitions whose old
                        // index still points at them (they get a fresh id each time)
                        if partById.[pid].Materialized && partById.[pid].Id = pid && not (partROs.ContainsKey(struct(pageIdx, pid))) then
                            partROs.[struct(pageIdx, pid)] <- mkPartRO pageIdx pid

        // ONE HeapRenderObject per bucket, bundling the per-page derives + page draws so the backend
        // records derive(page i)→barrier→draw(page i) as one submission. Rebuilt only when the page
        // count grows (stable Id while unchanged ⇒ no per-frame re-prepare; new Id on growth ⇒ the
        // new page's derive+draw get prepared). SyncPages (membership updater) materialises the page
        // lists FIRST, so the snapshot here always sees every live page.
        let mutable heapROCache : IRenderObject = Unchecked.defaultof<_>
        let mutable heapROPageCount = -1
        let mutable heapROEpoch = -1
        // draw order: dynamic partition first (page-major), then materialized
        // typed partitions — deterministic so the command recording is stable
        let currentDraws () =
            let l = System.Collections.Generic.List<IRenderObject>()
            l.Add (bucketRO :> IRenderObject)
            for pageIdx in 0 .. storage.Count - 1 do
                if pageIdx > 0 then
                    match partROs.TryGetValue(struct(pageIdx, 0)) with
                    | true, ro -> l.Add ro
                    | _ -> ()
            for pid in 1 .. partById.Count - 1 do
                if partById.[pid].Materialized && partById.[pid].Id = pid then
                    for pageIdx in 0 .. storage.Count - 1 do
                        match partROs.TryGetValue(struct(pageIdx, pid)) with
                        | true, ro -> l.Add ro
                        | _ -> ()
            l
        let buildHeapRO () =
            if heapROPageCount <> storage.Count || heapROEpoch <> partEpoch then
                heapROPageCount <- storage.Count
                heapROEpoch <- partEpoch
                let derives = if hasDerived then List.ofSeq deriveSpecs else []
                let draws = List.ofSeq (currentDraws ())
                let hro = HeapRenderObject(RenderPass.main, scope, derives, draws)
                // pickability is a construction-time property of the bucket (its ROs carry the
                // `HeapPickId` marker); only a real pick bucket is routed into the dom's PickId pass.
                hro.IsPickable <- picking && pickable
                heapROCache <- hro :> IRenderObject
            heapROCache

        // PAGED: one render object per live storage page (each binds that page's arena +
        // its slots' indirect). ensurePageROs lazily creates them; resultAval (rebuilt per
        // membership version) picks up new pages. built deterministically by SyncPages
        // (called from the membership updater); the members just hand back the current set.
        member x.SyncPages() =
            EditProf.time "sync:pageROs" ensurePageROs
            EditProf.time "sync:deriveROs" ensureDeriveROs
        member x.HeapRO : IRenderObject = buildHeapRO ()
        member x.RenderObjects : IRenderObject[] = (currentDraws ()).ToArray()
        member x.DeriveROs : IRenderObject[] = [||]
        member _.Count = slots.Count
        member _.IsChain = chainMode
        member _.ChainDistinct = if chainMode then chainLinks.DistinctCount else 0

        /// footprint diagnostics (cheap; published every update). Geometry now
        /// lives in the arena, so the "packed geometry" metrics mirror the
        /// arena footprint (kept for tooling/tests: exact-size churn must keep
        /// them FLAT — freed allocations are reused in place).
        member private _.PublishStats() =
            let mutable mat = 0
            let mutable typedSlots = 0
            for pid in 1 .. partById.Count - 1 do
                let p = partById.[pid]
                if p.Materialized && p.Id = pid then
                    mat <- mat + 1
                    typedSlots <- typedSlots + p.Count
            lastMaterializedPartitions <- mat
            lastDynamicResidents <- slots.Count - typedSlots
            lastPackedGeomBytes <- arenaAlloc.Extent * 4
            lastPackedGeomLiveBytes <- arenaAlloc.Live * 4
            lastArenaBytes <- arenaAlloc.Extent * 4
            lastArenaLiveBytes <- arenaAlloc.Live * 4
            lastInstBytes <- instAlloc.Extent * 4
            lastInstLiveBytes <- instAlloc.Live * 4

        member private x.AddInternal(t : AdaptiveToken, ro : RenderObject) =
            let __ingT0 = System.Diagnostics.Stopwatch.GetTimestamp()
            let slot = if freeSlots.Count > 0 then freeSlots.Pop() else let s = highWater in highWater <- s + 1; s
            ensureSlot slot
            // route this slot's whole group to one page (rolling to a fresh page if the current
            // one is full). Estimate the slot's worst-case words so it always fits the chosen page.
            setPage (storage.PlacePage (estimateSlotWords ro))
            // participate in the (shared) page's compaction: contribute this bucket's
            // per-slot blocks and re-bake headers when the page moves things
            storage.Page(curPage).Register (x :> IPageParticipant)
            slotPage.[slot] <- curPage
            slotPageDirty.Add slot |> ignore
            // PICKING: the dom-sourced pick id (-1 = unpickable). Almost always
            // constant per slot; a non-constant id gets a watcher that updates the
            // CPU cell AND marks the slot dirty (the mirror flushes dirty slots only).
            if picking then
                match ro.Uniforms.TryGetUniform(scope, symPickId) with
                | ValueSome v ->
                    pickIds.[slot] <- (try v.GetValueUntyped(AdaptiveToken.Top) :?> int with _ -> -1)
                    pickIdsDirty.Add slot |> ignore
                    if not v.IsConstant && not disableDynGeom then
                        let w = DynWriter(v)
                        w.OnChange <- System.Action<AdaptiveToken>(fun tok ->
                            w.Update(tok, fun _ o ->
                                pickIds.[slot] <- (try o :?> int with _ -> -1)
                                pickIdsDirty.Add slot |> ignore))
                        regSlotWriter slot w
                        w.OnChange.Invoke t
                | ValueNone ->
                    pickIds.[slot] <- -1
                    pickIdsDirty.Add slot |> ignore
            let __secT0 = System.Diagnostics.Stopwatch.GetTimestamp()
            let regionKeys = System.Collections.Generic.List<IAdaptiveValue>(names.Length)
            // derived-uniform CONSTITUENT sources, resolved + CANONICALIZED first —
            // the derived-output dedup below keys on them. null = chain-folded
            // Model (per-slot compute output, never shared).
            let constAvals = Array.zeroCreate<IAdaptiveValue> numConst
            for k in 0 .. numConst - 1 do
                let c = neededConstituents.[k]
                if not (c.CBase = Derived.MBASE && chainMode) then
                    let bav =
                        match ro.Uniforms.TryGetUniform(scope, cachedSym c.CBase) with
                        | ValueSome v -> v
                        | ValueNone -> failwithf "Heap.ofRenderObjects: derived uniform needs base trafo '%s' but the RO doesn't supply it" c.CBase
                    constAvals.[k] <- canonConstituent bav c.CInv
            // DERIVED-OUTPUT DEDUP: slots whose recipe + canonical constituents match
            // share ONE output region; the FIRST member owns it (computes it in the
            // derive pass — ownership mask below). Pay per DISTINCT VALUE, not per
            // object: 246k parts sharing one Model derive ONE ModelTrafoInv.
            let outShares = System.Collections.Generic.List<DerivedShare>(numDerivedRecords)
            let mutable ownMask = 0
            for i in 0 .. names.Length - 1 do
                if derivedCells.Contains i then
                    let j = outShares.Count
                    let (_, _, cs) = derivedPlan.[j]
                    let ck (k : int) : obj =
                        if k < cs.Length then box constAvals.[constCell.[cs.[k]] - fieldStride] else null
                    let dedupable = cs |> Array.forall (fun c -> not (c.CBase = Derived.MBASE && chainMode))
                    let key = struct(curPage, j, ck 0, ck 1, ck 2)
                    let share =
                        if dedupable then
                            match derivedShares.TryGetValue key with
                            | true, sh ->
                                sh.Members.Add slot |> ignore
                                sh
                            | _ ->
                                let (off, blk) = allocOutput (fieldRequestedType names.[i])
                                let sh = { Key = key; Page = curPage; Dedup = true; Block = blk; Offset = off
                                           Members = System.Collections.Generic.HashSet([slot]); Owner = slot; ListIdx = -1 }
                                derivedShares.[key] <- sh
                                allShares.Add sh |> ignore
                                shareAdd sh
                                ownMask <- ownMask ||| (1 <<< j)
                                sh
                        else
                            let (off, blk) = allocOutput (fieldRequestedType names.[i])
                            let sh = { Key = key; Page = curPage; Dedup = false; Block = blk; Offset = off
                                       Members = System.Collections.Generic.HashSet([slot]); Owner = slot; ListIdx = -1 }
                            allShares.Add sh |> ignore
                            shareAdd sh
                            ownMask <- ownMask ||| (1 <<< j)
                            sh
                    outShares.Add share
                    headers.[slot * headerStride + i] <- share.Offset
                else
                    let av =
                        match ro.Uniforms.TryGetUniform(scope, nameSyms.[i]) with
                        | ValueSome v -> v
                        | ValueNone -> failwithf "Heap.ofRenderObjects: RO missing per-draw uniform '%s'" names.[i]
                    regionKeys.Add av
                    headers.[slot * headerStride + i] <- allocRegion av (fieldRequestedType names.[i])
            headers.[slot * headerStride + ownCell] <- ownMask
            if Diagnostics && slot < 3 then
                Log.line "[heap-dbg] slot %d ownMask=%d shares=%s" slot ownMask
                    (outShares |> Seq.map (fun sh -> sprintf "%d(own=%d,ded=%b)" sh.Offset sh.Owner sh.Dedup) |> String.concat ",")
            // derived-uniform CONSTITUENT regions (Model/View/Proj fwd/bwd, M44d):
            // Model in chainMode is the per-slot FOLD output (compute-written); every
            // other constituent is uploaded from the (canonical) base trafo aval,
            // ref-counted (shared camera / shared constant trafo → ONE region).
            let constKeys = System.Collections.Generic.List<struct(IAdaptiveValue * bool)>(numConst)
            let foldBlocks = System.Collections.Generic.List<HeapBlock>()
            for k in 0 .. numConst - 1 do
                let c = neededConstituents.[k]
                let off =
                    if c.CBase = Derived.MBASE && chainMode then
                        let (o, blk) = allocFoldConstituent ()
                        foldBlocks.Add blk
                        o
                    else
                        let cav = constAvals.[k]
                        constKeys.Add(struct(cav, c.CInv))
                        allocConstituent cav c.CInv
                headers.[slot * headerStride + (fieldStride + k)] <- off
            // GPU trafo-chain: route the slot's UNFOLDED model stack into the link
            // arena (deduped) + a chIdx run; the GPU folds it into the slot's Model
            // forward (and, when consumed, backward) constituent region.
            if chainMode then
                let st =
                    match ro.Uniforms.TryGetUniform(scope, symModelStack) with
                    | ValueSome (:? aval<aval<Trafo3d>[]> as st) -> st
                    | _ -> failwith "Heap.ofRenderObjects: chainMode RO missing aval<aval<Trafo3d>[]> 'ModelTrafoStack'"
                addChainSlot slot (AVal.force st)
                // the stack ELEMENTS are adaptive via the link arena; the stack
                // STRUCTURE (re-parenting) gets a watcher that re-routes the chain
                if not st.IsConstant && not disableDynGeom then
                    let w = DynWriter(st)
                    w.Fresh <- true
                    w.OnChange <- System.Action<AdaptiveToken>(fun tok ->
                        w.Update(tok, fun _ o ->
                            if w.Fresh then w.Fresh <- false
                            else
                                removeChainSlot slot
                                addChainSlot slot (o :?> aval<Trafo3d>[])))
                    regSlotWriter slot w
                    w.OnChange.Invoke t
            let __secT1 = System.Diagnostics.Stopwatch.GetTimestamp()
            stIngestFieldsMs <- stIngestFieldsMs + float (__secT1 - __secT0) * 1000.0 / float System.Diagnostics.Stopwatch.Frequency
            // TYPED ASSIGNMENTS: this slot's per-field source tids (packed into
            // its assignment key below; partition residency starts DYNAMIC)
            let slotTids = Array.zeroCreate<int> 8
            let slotLens = Array.zeroCreate<int> 8
            let mutable slotIdxTid = 0
            // geometry: per-attribute arena allocations (host) or the per-slot
            // SSBO-array registration (bindless), plus the index allocation.
            let attrKeys =
                if useBindlessGeom then
                    // register the slot's vertex buffers for the per-handle gather
                    for (ai, _, sym, _, _, _, _) in attrInfos do
                        let bv =
                            match ro.VertexAttributes.TryGetAttribute sym with
                            | ValueSome b -> b
                            | ValueNone -> failwithf "Heap.ofRenderObjects: RO missing shader input attribute %A" sym
                        let pos = slot * numAttrs + ai
                        vtxAvals.[pos] <- bv.Buffer
                        vtxLast.[pos] <- bv.Buffer.GetValue()
                        vtxStructDirty.Add pos |> ignore
                        // a non-constant source buffer aval must be re-read on every
                        // gather pull (handle can change without a membership change);
                        // constant ones are stamped once here / on structural refresh.
                        if not bv.Buffer.IsConstant then
                            vtxDynPos.Add pos |> ignore
                            vtxPosOf.[bv.Buffer :> IAdaptiveObject] <- pos
                    [||]
                else
                    attrInfos |> Array.map (fun (ai, _, sym, _, _, _, _) ->
                        let (key, r, srcTid, srcLen) = attrFor ro sym
                        (if ai < 8 then
                            slotTids.[ai] <- srcTid
                            slotLens.[ai] <- srcLen)
                        headers.[slot * headerStride + attrBase + ai] <- r
                        // dynamic-source allocation: register this slot's header cell
                        // for the realloc re-bake and subscribe THIS bucket's updater
                        // (idempotent evaluation — a clean writer just adds the edge)
                        (match key with
                         | AttrKey.Static k ->
                             (match attrStatic.TryGetValue k with
                              | true, e when not (isNull e.Writer) ->
                                  e.DynRefs.Add(struct(x :> IGeomSink, slot, attrBase + ai)) |> ignore
                                  e.Writer.OnChange.Invoke t
                              | _ -> ())
                         | _ -> ())
                        key)
            // index allocation — or the -1 sentinel for NON-indexed members
            // (the shader's decodeHeapIndex passes gl_VertexIndex through);
            // their vertex count comes from the RO's Direct draw call.
            let struct(idxKey, idxRef, vertexCount) =
                match ro.Indices with
                | Some bvIdx ->
                    slotIdxTid <- (if bvIdx.ElementType = typeof<int16> || bvIdx.ElementType = typeof<uint16> then 2 else 1)
                    let (k, e) = idxFor ro
                    if not (isNull e.Writer) then
                        e.DynRefs.Add(struct(x :> IGeomSink, slot, idxCell)) |> ignore
                        e.Writer.OnChange.Invoke t
                    struct(k, e.Ref, e.Count)
                | None ->
                    struct(noIdxKey, -1, faceVertexCountOf ro)
            stIngestGeomMs <- stIngestGeomMs + float (System.Diagnostics.Stopwatch.GetTimestamp() - __secT1) * 1000.0 / float System.Diagnostics.Stopwatch.Frequency
            headers.[slot * headerStride + idxCell] <- idxRef
            headers.[slot * headerStride + vcCell] <- vertexCount
            vcOfSlot.[slot] <- vertexCount
            recsVersion <- recsVersion + 1
            dirtyHeaders.Add slot |> ignore
            // register the slot's textures (bindless per-type tables / atlas)
            for (_, _, texSyms, table) in bindlessTexTables do
                table.AddSlot(slot, texSyms |> Array.map (fun tn ->
                    match ro.Uniforms.TryGetUniform(scope, tn) with
                    | ValueSome v -> v
                    | ValueNone -> failwithf "Heap.ofRenderObjects: texture uniform %A missing" tn))
            match atlasState with
            | Some (_, _, table) ->
                table.AddSlot(slot, atlasTexSyms |> Array.map (fun tn ->
                    match ro.Uniforms.TryGetUniform(scope, tn) with
                    | ValueSome v -> v
                    | ValueNone -> failwithf "Heap.ofRenderObjects: atlas texture %A missing" tn))
            | None -> ()
            // constant visibility is baked into the record; a dynamic gate gets
            // a GateWriter (pending on the draw mirror, so the first flush
            // reads its actual value) re-staged only when IT toggles
            let k = if instanced then instanceCountOf ro else 1
            let active = ro.IsActive
            // TYPED ASSIGNMENTS: register the slot's assignment (residency starts
            // dynamic; crossing the threshold materializes + migrates) — BEFORE any
            // classAdd path so cluster listing sees the final slotPart. Raw tids +
            // lengths are kept per slot so in-place length edits (full <->
            // singleton) can reclassify the extent and migrate (recomputeAssign).
            for i in 0 .. 7 do
                slotFieldTid.[slot * 8 + i] <- slotTids.[i]
                slotFieldLen.[slot * 8 + i] <- slotLens.[i]
            slotIdxTidA.[slot] <- slotIdxTid
            let asgTids = extendTids slotTids slotLens vertexCount (slotIdxTid > 0)
            let asgKey = internAssign asgTids slotIdxTid
            asgAdd slot asgKey (lazy (mapOfTids asgTids slotIdxTid))
            let instCount =
                if useClusters then
                    // CLUSTERED: gating = class membership; the record set is derived
                    // from the class lists at flush time. Dynamic gates are evaluated
                    // by the UPDATER (OnCluster) so membership settles before flushes.
                    if active.IsConstant then
                        (if AVal.force active then classAdd slot)
                    else
                        let w = GateWriter(active, slot, 1)
                        w.OnCluster <- System.Action<AdaptiveToken>(fun tok ->
                            w.Update(tok, fun sl kk -> if kk > 0 then classAdd sl else classRemove sl))
                        gateWriters.[slot] <- w
                        w.OnCluster.Invoke t          // subscribes the gate to the UPDATER
                    1
                elif active.IsConstant then (if AVal.force active then k else 0)
                else
                    let w = GateWriter(active, slot, k)
                    gateWriters.[slot] <- w
                    drawBuf.MarkGate w
                    k
            let instBlock = if useSlotAttr then allocInst slot k else null
            let firstInstance =
                if useSlotAttr then int instBlock.Offset
                elif useDrawId then 0
                else slot
            // NON-indexed record: vertexCount = the slot's INDEX count (the
            // shader maps gl_VertexIndex through the index allocation), or
            // the RO's own draw-call vertex count for index-free members.
            entries.[slot] <- DrawCallInfo(FaceVertexCount = vertexCount, FirstIndex = 0, BaseVertex = 0,
                                           FirstInstance = firstInstance, InstanceCount = instCount)
            dirtyDraws.Add slot |> ignore
            let hs = { Slot = slot; Page = curPage; RegionKeys = regionKeys.ToArray(); Active = active; Instances = k; InstOffset = firstInstance
                       InstBlock = instBlock; AttrKeys = attrKeys; IdxKey = idxKey
                       ConstKeys = constKeys.ToArray(); Shares = outShares.ToArray(); FoldBlocks = foldBlocks.ToArray() }
            slots.[ro] <- hs
            // draw-call SHAPE watcher: a non-constant Direct call list updates the
            // slot's vertex count (non-indexed members; indexed counts come from
            // the index allocation) and — on instanced buckets — its instance
            // count, in place, O(1) per change.
            (match ro.DrawCalls with
             | DrawCalls.Direct calls when not calls.IsConstant && (ro.Indices.IsNone || instanced) && not disableDynGeom ->
                 let indexed = ro.Indices.IsSome
                 let w = DynWriter(calls)
                 w.OnChange <- System.Action<AdaptiveToken>(fun tok ->
                     w.Update(tok, fun _ o ->
                         let arr = o :?> DrawCallInfo[]
                         if not indexed then
                             let vc = if arr.Length = 0 then 0 else arr.[0].FaceVertexCount
                             if vcOfSlot.[slot] <> vc then
                                 headers.[slot * headerStride + vcCell] <- vc
                                 vcOfSlot.[slot] <- vc
                                 recsVersion <- recsVersion + 1
                                 dirtyHeaders.Add slot |> ignore
                                 if useClusters then
                                     (if clusterClsOf.[slot] >= 0 then classRemove slot; classAdd slot)
                                 else
                                     entries.[slot].FaceVertexCount <- vc
                                     dirtyDraws.Add slot |> ignore
                         if instanced then
                             let k' = match arr with [||] -> 1 | a -> max 1 a.[0].InstanceCount
                             if k' <> hs.Instances then
                                 hs.Instances <- k'
                                 (match gateWriters.TryGetValue slot with
                                  | true, gw -> gw.Instances <- k'
                                  | _ -> ())
                                 if useSlotAttr then
                                     freeInst hs.InstBlock
                                     let nb = allocInst slot k'
                                     hs.InstBlock <- nb
                                     hs.InstOffset <- int nb.Offset
                                     entries.[slot].FirstInstance <- int nb.Offset
                                 // gated-ON slots re-stage their record with the new count
                                 if entries.[slot].InstanceCount <> 0 then entries.[slot].InstanceCount <- k'
                                 dirtyDraws.Add slot |> ignore))
                 regSlotWriter slot w
                 w.OnChange.Invoke t
             | _ -> ())
            stIngestN <- stIngestN + 1
            stIngestMs <- stIngestMs + float (System.Diagnostics.Stopwatch.GetTimestamp() - __ingT0) * 1000.0 / float System.Diagnostics.Stopwatch.Frequency
            if stIngestN % 100000 = 0 && (EditProf.enabled || Diagnostics) then Log.line "[startup] ingest %d parts so far: %.0f ms (fields %.0f | geom %.0f [copy %.0f, stage %.0f] | rest %.0f)" stIngestN stIngestMs stIngestFieldsMs stIngestGeomMs stIngestCopyMs stIngestStageMs (stIngestMs - stIngestFieldsMs - stIngestGeomMs)

        member private x.RemoveInternal(ro : RenderObject) =
            match slots.TryGetValue ro with
            | true, s ->
                // free from the page the slot's group lives on
                setPage s.Page
                if chainMode then removeChainSlot s.Slot
                for k in s.RegionKeys do freeRegion k
                for struct(av, inv) in s.ConstKeys do freeConstituent av inv
                for j in 0 .. s.Shares.Length - 1 do
                    let sh = s.Shares.[j]
                    sh.Members.Remove s.Slot |> ignore
                    if sh.Members.Count = 0 then
                        if sh.Dedup then derivedShares.Remove sh.Key |> ignore
                        allShares.Remove sh |> ignore
                        shareRemove sh
                        uniAlloc.Free sh.Block
                    elif sh.Owner = s.Slot then
                        // transfer ownership — any member computes the same value
                        let mutable no = -1
                        for m in sh.Members do (if no < 0 then no <- m)
                        sh.Owner <- no
                        shareWrite sh
                        headers.[no * headerStride + ownCell] <- headers.[no * headerStride + ownCell] ||| (1 <<< j)
                        dirtyHeaders.Add no |> ignore
                for b in s.FoldBlocks do arenaAlloc.Free b
                s.AttrKeys |> Array.iteri (fun ai k ->
                    match k with
                    | AttrKey.Single av -> freeSingle av
                    | AttrKey.Static key ->
                        // drop this slot's realloc back-ref BEFORE the free (which
                        // may retire the entry + its writer at refcount 0)
                        (match attrStatic.TryGetValue key with
                         | true, e when not (isNull e.Writer) ->
                             e.DynRefs.Remove(struct(x :> IGeomSink, s.Slot, attrBase + ai)) |> ignore
                         | _ -> ())
                        freeStatic attrStatic key)
                (match idxStatic.TryGetValue s.IdxKey with
                 | true, e when not (isNull e.Writer) ->
                     e.DynRefs.Remove(struct(x :> IGeomSink, s.Slot, idxCell)) |> ignore
                 | _ -> ())
                freeStatic idxStatic s.IdxKey
                // per-slot adaptive watchers (draw-call shape / pick id / model stack)
                (match slotDynWriters.TryGetValue s.Slot with
                 | true, l ->
                     for w in l do w.Dispose()
                     slotDynWriters.Remove s.Slot |> ignore
                 | _ -> ())
                if useBindlessGeom then
                    for ai in 0 .. numAttrs - 1 do
                        let pos = s.Slot * numAttrs + ai
                        // drop the aval→pos entry so a late mark on the (now dead) source
                        // can't dirty a recycled position; the tombstoned cell is never read.
                        let oldAv = vtxAvals.[pos]
                        if not (System.Object.ReferenceEquals(oldAv, null)) then vtxPosOf.Remove (oldAv :> IAdaptiveObject) |> ignore
                        vtxAvals.[pos] <- Unchecked.defaultof<_>
                        // tombstone: cell keeps its last live buffer (draw record is
                        // InstanceCount=0 so the SSBO cell is never read) — restamp it
                        // and drop it from the dynamic re-read set.
                        vtxStructDirty.Add pos |> ignore
                        vtxDynPos.Remove pos |> ignore
                for (_, _, _, table) in bindlessTexTables do table.RemoveSlot s.Slot
                match atlasState with
                | Some (_, _, table) -> table.RemoveSlot s.Slot
                | None -> ()
                if useSlotAttr then freeInst s.InstBlock
                entries.[s.Slot] <- DrawCallInfo(FaceVertexCount = 0, FirstIndex = 0, BaseVertex = 0, FirstInstance = 0, InstanceCount = 0)
                dirtyDraws.Add s.Slot |> ignore
                match gateWriters.TryGetValue s.Slot with
                | true, w -> w.Dispose(); gateWriters.Remove s.Slot |> ignore
                | _ -> ()
                if useClusters then classRemove s.Slot
                asgRemove s.Slot
                // PICKING: release the dom pick id for this slot before it's recycled
                if picking && pickIds.[s.Slot] >= 0 then deregister pickIds.[s.Slot]
                freeSlots.Push s.Slot
                slots.Remove ro |> ignore
            | _ -> ()

        /// Add ONE new member (no-op if already present). Called from the updater.
        member x.AddOne(t : AdaptiveToken, ro : RenderObject) =
            if not (slots.ContainsKey ro) then
                x.AddInternal(t, ro)
                x.PublishStats()

        /// Remove ONE member: tombstone its record, recycle slot + regions.
        /// Waste-triggered compaction (and the buffer swap it implies) runs in
        /// the same updater pass.
        member x.RemoveOne(ro : RenderObject) =
            x.RemoveInternal ro
            maybeCompact ()
            x.PublishStats()

        /// the CURRENT members (snapshot)
        member _.Members = slots.Keys |> Seq.toArray

        /// Diff `ros` (the bucket's CURRENT members) against the held membership:
        /// removed ROs are tombstoned (InstanceCount = 0, slot + regions + texture
        /// refs recycled); new ROs take a slot, alloc/refcount their arena regions
        /// and texture refs and (if their geometry is unseen) append to the packed
        /// buffers. Called from the updater's evaluation only.
        member x.Update(t : AdaptiveToken, ros : RenderObject[]) =
            // removals first so their slots/offsets are reusable by this very update
            let current = System.Collections.Generic.HashSet<RenderObject>(ros, HashIdentity.Reference)
            let mutable dead : System.Collections.Generic.List<RenderObject> = null
            for KeyValue(ro, _) in slots do
                if not (current.Contains ro) then
                    if isNull dead then dead <- System.Collections.Generic.List()
                    dead.Add ro
            if not (isNull dead) then
                for ro in dead do x.RemoveInternal ro
                maybeCompact ()
            for ro in ros do
                if not (slots.ContainsKey ro) then x.AddInternal(t, ro)
            x.PublishStats()

        /// Release all adaptive references (region writers, texture writers) and
        /// the bucket-owned GPU resources (atlas pages, dummy textures). The storage
        /// is SHARED: this releases exactly this bucket's ref-counts (per-slot
        /// removal, freeing regions/constituents/statics whose count hits zero) and
        /// NEVER clears the page dicts — other buckets'/heaps' entries live on.
        member x.Dispose() =
            for ro in slots.Keys |> Seq.toArray do x.RemoveInternal ro
            storage.Unregister (x :> IPageParticipant)
            for KeyValue(_, w) in gateWriters do w.Dispose()
            gateWriters.Clear()
            for (_, _, _, table) in bindlessTexTables do table.Dispose()
            match atlasState with
            | Some (pool, dummy, table) ->
                table.Dispose()
                pool.Dispose()
                delDummy dummy
            | None -> ()
            if not (isNull (box recBuf)) then recBuf.Dispose()
            // release the derived-uniform compute resources (else the runtime's
            // resource cache keeps live references to the ComputeProgram).
            let disp (o : obj) = match o with | :? System.IDisposable as d -> d.Dispose() | _ -> ()
            // (recBuf is disposed ONCE above — a second disp here stole the
            // ResourceManager's reference and drove the count to -1 at teardown)
            if hasDerived then (for inp in pageDeriveInputs do disp inp); disp derivedShader
            if chainActive then
                disp chainProg; disp chainInput; disp chainShader
                if not (isNull (box chOffBuf)) then chOffBuf.Dispose()
                if not (isNull (box chLenBuf)) then chLenBuf.Dispose()
                if not (isNull (box chIdxBuf)) then chIdxBuf.Dispose()
            if chainBwdActive then disp chainInvProg; disp chainInvInput; disp chainInvShader
            slots.Clear()

        interface IGeomSink with
            /// a dynamic geometry entry this slot references was REALLOCATED
            /// (size change): re-bake the slot's header cell; an index entry also
            /// carries the slot's drawn-vertex count (vcCell + draw record /
            /// cluster class). Runs inside the updater evaluation — before any
            /// mirror or arena flush serializes state.
            member _.GeomMoved(slot, cell, newRef, newCount, isIndex) =
                headers.[slot * headerStride + cell] <- newRef
                if isIndex then
                    headers.[slot * headerStride + vcCell] <- newCount
                    vcOfSlot.[slot] <- newCount
                    recsVersion <- recsVersion + 1
                    // a drawn-count change can invalidate FULL extent classes
                    recomputeAssign slot
                    if useClusters then
                        (if clusterClsOf.[slot] >= 0 then classRemove slot; classAdd slot)
                    else
                        entries.[slot].FaceVertexCount <- newCount
                        dirtyDraws.Add slot |> ignore
                elif cell >= attrBase && cell - attrBase < 8 then
                    // attribute length change: reclassify the extent (the demo's
                    // recolor toggles a color array between full and singleton)
                    let ai = cell - attrBase
                    if slotFieldLen.[slot * 8 + ai] <> newCount then
                        slotFieldLen.[slot * 8 + ai] <- newCount
                        recomputeAssign slot
                // instance-record row mirrors attr/idx refs + vc — refresh it
                if useClusters then refreshRow slot
                dirtyHeaders.Add slot |> ignore

        // shared-page compaction stake: contribute this bucket's per-slot arena
        // blocks (derived outputs, chain folds — they live in slots, not in the
        // page dicts) and re-bake every slot's header cells after a move.
        interface IPageParticipant with
            member _.CollectResidents(page, res) =
                // derived outputs live in the DENSE uniform store (bucket-global,
                // never page-compacted) — only fold blocks participate here.
                for KeyValue(_, s) in slots do
                    if s.Page = page then
                        let hb = s.Slot * headerStride
                        for j in 0 .. s.FoldBlocks.Length - 1 do
                            let cell = fieldStride + foldConstIdx.[j]
                            let sj, jj = s, j
                            res.Add(struct(headers.[hb + cell], int s.FoldBlocks.[j].Size, true, fun off b ->
                                headers.[hb + cell] <- off
                                sj.FoldBlocks.[jj] <- b))
            member _.RewriteHeaders() =
                EditProf.count "COMPACTION-rewriteHeaders" 1
                // re-bake from the slot's OWN page's dicts (never the current fill
                // page): plain field cells from RegionKeys, non-fold constituent
                // cells from ConstKeys, attribute/index cells from the dedup tables.
                // Derived-output and fold cells were re-seated inline above.
                for KeyValue(_, s) in slots do
                    let pg = storage.Page s.Page
                    let hb = s.Slot * headerStride
                    for j in 0 .. s.RegionKeys.Length - 1 do
                        match pg.Regions.TryGetValue s.RegionKeys.[j] with
                        | true, e -> headers.[hb + plainCells.[j]] <- e.Offset
                        | _ -> ()
                    for j in 0 .. s.ConstKeys.Length - 1 do
                        let struct(av, inv) = s.ConstKeys.[j]
                        let d = if inv then pg.ConstituentsB else pg.ConstituentsF
                        match d.TryGetValue av with
                        | true, e -> headers.[hb + fieldStride + plainConstIdx.[j]] <- e.Offset
                        | _ -> ()
                    s.AttrKeys |> Array.iteri (fun ai k ->
                        headers.[hb + attrBase + ai] <-
                            match k with
                            | AttrKey.Single av -> pg.SingleRegions.[av].Offset
                            | AttrKey.Static key -> pg.AttrStatic.[key].Ref)
                    match pg.IdxStatic.TryGetValue s.IdxKey with
                    | true, e -> headers.[hb + idxCell] <- e.Ref
                    | _ -> ()
                    // derived-output cells re-bake from the (possibly shared) shares
                    for j in 0 .. s.Shares.Length - 1 do
                        headers.[hb + derivedCellOrder.[j]] <- s.Shares.[j].Offset
                    // compaction moved allocations -> re-bake the instance-record row
                    if useClusters then refreshRow s.Slot
                headersAllDirty <- true

    /// Collapse an adaptive set of N render objects into B bucket render objects
    /// (one per effect + pipeline state + geometry layout + field set), each
    /// drawn as ONE indirect multidraw against a shared dirty-tracked arena.
    /// Per-draw heap fields = the uniforms your objects supply: AUTO-DETECTED
    /// per RO (see `detectFields` below) and part of the bucket key; uniforms
    /// shared via one aval (camera, lights, …) dedup to one arena region or stay
    /// ordinary globals. Render objects that aren't heap-eligible (see
    /// `isHeapable` below) are passed through to the output set UNCHANGED — a
    /// mixed scene degrades gracefully.
    // Builds the heap's machinery (input reader + per-bucket CPU model + GPU
    // buffers) and returns its bucket-RO set together with a teardown that frees
    // EVERYTHING (every IncrementalBucket's GPU + object-count CPU, the reader).
    // Called lazily on first activation; teardown runs when the last task drops it.
    let private buildHeap (storage : HeapStorage) (picking : bool) (deregister : int -> unit) (signature : IFramebufferSignature) (objects : aset<IRenderObject>) : aset<IRenderObject> * (unit -> unit) =
        let runtime = signature.Runtime :?> IRuntime
        // DCE the effect BEFORE heapification: heapification turns every consumed attribute into an
        // SSBO gather (the heap VS has zero vertex inputs regardless), so a read-but-dead attribute
        // — consumed by the VS but feeding no live framebuffer output — would be INGESTED into the
        // arena and gathered per vertex for nothing. Linking against the (tight) signature here
        // shrinks `consumedNonSamplerNames`, so the field set the heap actually STORES stays lean.
        // Memoised by effect id; the same DCE'd effect drives field detection AND the gather rewrite.
        let linkDCE (effect : Effect) : Effect =
            // BUCKET-AWARE: link only against the signature attachments THIS effect actually writes.
            // A bucket whose effect doesn't produce e.g. `PickId` must not be forced to output it —
            // that synthesises a read-modify-write passthrough (a phantom `PickId` vertex input, which
            // then fails the attribute gather). The unwritten attachment just keeps its cleared value
            // for that bucket's pixels (normal multi-attachment rendering).
            let outputs =
                signature.ColorAttachments |> Map.toList
                |> List.choose (fun (_, att) ->
                    let n = string att.Name
                    if Map.containsKey n effect.Outputs then Some (n, att.Type) else None)
                |> Map.ofList
            let rec linkShaders (needed : Map<string, System.Type>) = function
                | [] -> []
                | cur :: before ->
                    let cur' = Shader.withOutputs (Map.union needed (Shader.systemOutputs cur)) cur
                    cur' :: linkShaders (Shader.neededInputs cur') before
            let dceShaders =
                effect
                |> Effect.addIfNotPresent ShaderStage.Fragment (Shader.passing outputs)
                |> Effect.addIfNotPresent ShaderStage.Vertex   (Shader.passing Map.empty)
                |> Effect.toList |> List.rev |> linkShaders outputs
            // Effect.ofList would stamp Effect.NewId() = a fresh GUID; the backend caches compiled
            // programs by effect Id, so a per-call id defeats reuse (recompiles every heap/run) and
            // could even alias the INPUT effect's id. Build a DETERMINISTIC id keyed on the input
            // effect + the signature outputs, namespaced with "heap".
            let sigKey = outputs |> Map.toList |> List.map (fun (n, t) -> n + ":" + t.Name) |> String.concat ","
            let map = dceShaders |> List.map (fun s -> s.shaderStage, s) |> Map.ofList
            Effect(sprintf "%s_heap_%s" effect.Id sigKey, Lazy<_>.CreateFromValue map, [])
        let linkCache = System.Collections.Generic.Dictionary<string, Effect>()
        let linkedEffect (e : Effect) =
            lock linkCache (fun () ->
                match linkCache.TryGetValue e.Id with
                | true, le -> le
                | _ -> let le = linkDCE e in linkCache.[e.Id] <- le; le)

        // ── heap eligibility tests only NECESSARY inputs ─────────────────────
        // The framebuffer-signature output semantics. An effect input is "necessary" only if it
        // feeds one of these. We read that straight off the effect's dependency map (free — no
        // DCE / no link): resolveTop gives output -> inputs, so the needed set is the union of
        // the inputs of the signature's outputs. A dead `{ v with … }` passthrough (e.g. Flow)
        // feeds no output, so it never appears here and can't reject an otherwise-heapable RO.
        let sigOutNames =
            signature.ColorAttachments |> Map.toSeq |> Seq.map (fun (_, att) -> string att.Name) |> Set.ofSeq
        let neededCache = System.Collections.Generic.Dictionary<_, Set<string>>()
        let neededInputsOf (e : Effect) : Set<string> =
            match neededCache.TryGetValue e.Id with
            | true, s -> s
            | _ ->
                let resolved = FShade.EffectDeps.resolveTop e.Dependencies
                let s =
                    sigOutNames
                    |> Seq.collect (fun out ->
                        match Map.tryFind out resolved with
                        | Some (d : FShade.OutputDeps) -> d.Inputs |> Map.toSeq |> Seq.map fst
                        | None -> Seq.empty)
                    |> Set.ofSeq
                neededCache.[e.Id] <- s
                s
        checkSupport runtime
        let scope = Ag.Scope.Root

        // ── per-draw field sets ──────────────────────────────────────────
        // Distinct field sets are interned (sorted names -> ONE shared array +
        // name->index map): per-RO facts then carry two references, and identical
        // sets compare cheaply. The joined string is folded into the bucket key.
        let fieldSetInterner = System.Collections.Generic.Dictionary<string, string[] * Map<string, int>>()
        let internFields (ns : string[]) =
            let k = String.concat ";" ns
            match fieldSetInterner.TryGetValue k with
            | true, v -> v
            | _ ->
                let v = ns, (ns |> Array.mapi (fun i n -> n, i) |> Map.ofArray)
                fieldSetInterner.[k] <- v
                v

        // Bucket key = effect + topology + render pass + the VALUES of ALL per-RO
        // pipeline state (raster / blend / depth / stencil / viewport), PROJECTED
        // onto the framebuffer signature — see BucketKey: state that cannot affect
        // the render (blend modes of unwritten attachments, depth/stencil without
        // such an attachment, an unconsumed blend constant) never partitions.
        // Bucketing is REACTIVE and per-RO dirty-tracked: each dynamic-key RO gets a
        // KeyWatcher over exactly the avals its key reads; a state value change
        // re-keys ONLY the affected ROs (all sharing the flipped aval) and moves
        // them to the right bucket (one indirect draw = one pipeline) — the rest of
        // the heap is untouched, there is no global regroup. This is wombat's per-RO
        // dynamic "mode rules" — the rule is simply each RO's state aval (often
        // derived from its data); constant state is interned once and never
        // re-evaluated. Per-draw value changes still flow through the arena with no
        // bucket change at all.
        // ── signature facts for the key projection ──
        // sorted signature color attachment names (Set.toArray is sorted)
        let sigColorNames = Set.toArray sigOutNames
        let hasDepth, hasStencil =
            match signature.DepthStencilAttachment with
            | Some fmt -> fmt.HasDepth, fmt.HasStencil
            | None -> false, false
        // the signature color attachments an effect actually writes ((name, symbol),
        // sorted; cached per effect id) — the only attachments whose blend state can
        // matter for that effect's buckets. Same projection linkDCE links against.
        let writtenAttachmentsCache = System.Collections.Generic.Dictionary<string, (string * Symbol)[]>()
        let writtenAttachments (e : Effect) =
            match writtenAttachmentsCache.TryGetValue e.Id with
            | true, v -> v
            | _ ->
                let v =
                    sigColorNames
                    |> Array.filter (fun n -> Map.containsKey n e.Outputs)
                    |> Array.map (fun n -> n, Symbol.Create n)
                writtenAttachmentsCache.[e.Id] <- v
                v
        // geometry layout signature: HOST buckets carry NO per-attribute
        // element types at all — the shader decode branches on each
        // allocation's header typeId at fetch time and auto-converts the
        // SOURCE element to the shader's input type, so ROs with e.g. C4b
        // singleton colors, C4f buffers and default C4b-colored boxes share
        // ONE bucket (the index element type was already per-allocation).
        // What still partitions buckets (folded into Layout in factsOf):
        // host-vs-bindless geometry strategy, instanced-ness, and the
        // per-draw field set. BINDLESS buckets keep per-attribute element
        // type + offset + stride (their vertex-pull gather bakes all three
        // from ro0's BufferViews).
        let bindlessSig (r : RenderObject) =
            match r.Surface with
            | Surface.Effect e ->
                e.Inputs |> Map.toList |> List.map (fun (name, _) ->
                    match r.VertexAttributes.TryGetAttribute (cachedSym name) with
                    | ValueSome bv -> sprintf "%s:%s:%d:%d" name bv.ElementType.FullName bv.Offset bv.Stride
                    | ValueNone -> name + ":?") |> String.concat ";"
            | _ -> ""
        let modeKey (layout : string) (t : AdaptiveToken) (r : RenderObject) : BucketKey =
            // classify guarantees Surface.Effect for every RO that reaches the key
            let e = match r.Surface with | Surface.Effect e -> e | _ -> failwith "Heap: modeKey requires Surface.Effect"
            let ra = r.RasterizerState
            let bs = r.BlendState
            let ds = r.DepthState
            let ss = r.StencilState
            // effective per-attachment blend state: per-attachment override, else the
            // global fallback — the same resolution the backends apply, so two ROs
            // expressing the same state differently land in the same bucket.
            let attModes = bs.AttachmentMode.GetValue t
            let attMasks = bs.AttachmentWriteMask.GetValue t
            let globalMode = bs.Mode.GetValue t
            let globalMask = bs.ColorWriteMask.GetValue t
            let blend =
                writtenAttachments e |> Array.map (fun (n, sym) ->
                    let mode = match Map.tryFind sym attModes with Some m -> m | None -> globalMode
                    let mask = match Map.tryFind sym attMasks with Some m -> m | None -> globalMask
                    n, mode, mask)
            // IsTransparent partitions buckets so transparent and opaque ROs that otherwise
            // share effect+pipeline state still emit SEPARATE grouped ROs — TransparencyRenderTask
            // routes by RenderObject.IsTransparent (see TransparencyRenderTask.isTransparent),
            // so each bucket's combined output must carry the same flag as its inputs.
            // RenderObject.Clone copies IsTransparent (Pipeline/RenderObject.fs:120) so the
            // bucket's output inherits it automatically from any input in the partition.
            { EffectId = e.Id
              Topology = r.Mode
              Layout = layout
              Pass = r.RenderPass
              Cull = ra.CullMode.GetValue t
              FrontFacing = ra.FrontFacing.GetValue t
              Fill = ra.FillMode.GetValue t
              Multisample = ra.Multisample.GetValue t
              ConservativeRaster = ra.ConservativeRaster.GetValue t
              IsTransparent = r.IsTransparent
              Blend = blend
              BlendConstant =
                if blend |> Array.exists (fun (_, m, _) -> usesBlendConstant m) then Some (bs.ConstantColor.GetValue t)
                else None
              Depth =
                if hasDepth then Some (ds.Test.GetValue t, ds.Bias.GetValue t, ds.WriteMask.GetValue t, ds.Clamp.GetValue t)
                else None
              Stencil =
                if hasStencil then Some (ss.ModeFront.GetValue t, ss.WriteMaskFront.GetValue t, ss.ModeBack.GetValue t, ss.WriteMaskBack.GetValue t)
                else None
              Viewport = r.ViewportState.Viewport |> Option.map (fun v -> v.GetValue t)
              Scissor  = r.ViewportState.Scissor  |> Option.map (fun v -> v.GetValue t) }

        // Eligibility: only a concrete indexed Effect RenderObject with host-array
        // (positions/normals/index) geometry and ALL heap uniforms present in a
        // packable type can be collapsed. Anything else — a non-RenderObject, a
        // command/multi RO, non-indexed or GPU-buffer geometry, a missing/odd-typed
        // heap uniform — is passed through UNCHANGED. Never dropped, never crashed.
        // host-readable AND tightly packed: ArrayBuffer / any INativeBuffer (incl. a
        // user-supplied NativeMemoryBuffer), blittable element type, no interleave
        // (stride 0 or = element size). GPU-only or interleaved -> not eligible.
        let isHostTight (bv : BufferView) =
            let es = elemSize bv.ElementType
            es > 0 && (bv.Stride = 0 || bv.Stride = es) &&
            (match bv.Buffer with
             | :? ISingleValueBuffer -> true        // adaptive singleton (length-1 allocation)
             | b -> (match b.GetValue() with :? INativeBuffer -> true | _ -> false))
        // bindless vertex-pull eligibility: a supported (float/int vector) attribute type,
        // 4-byte-aligned offset/stride (so it reinterprets as float[]/int[]). The buffer may
        // be host OR GPU-resident (bound, not copied). Indices may be host or GPU (combined).
        let isBindlessAttr (bv : BufferView) =
            let es = elemSize bv.ElementType
            let t = bv.ElementType
            (t = typeof<float32> || t = typeof<V2f> || t = typeof<V3f> || t = typeof<V4f> ||
             t = typeof<int> || t = typeof<V2i> || t = typeof<V3i> || t = typeof<V4i>) &&
            es > 0 && bv.Offset % 4 = 0 && (bv.Stride = 0 || bv.Stride % 4 = 0)
        let isReadableIndex (bv : BufferView) =
            let es = elemSize bv.ElementType
            es > 0 && (bv.Stride = 0 || bv.Stride = es) &&
            (match bv.Buffer.GetValue() with :? INativeBuffer -> true | :? IBackendBuffer -> true | _ -> false)
        let packable =
            System.Collections.Generic.HashSet<System.Type>(
                [ typeof<M44f>; typeof<Trafo3d>; typeof<M44d>; typeof<V4f>; typeof<C4f>
                  typeof<V3f>; typeof<V2f>; typeof<float32>; typeof<float>; typeof<int>
                  typeof<V3d>; typeof<V2d>; typeof<V4d>; typeof<M33d>
                  typeof<uint32>; typeof<bool>; typeof<V2i>; typeof<V3i>; typeof<V4i> ])

        // ── per-draw field auto-detection ────────────────────────────────
        // Classification rule (deterministic per RO, memoized in RoFacts like
        // layoutSig): a uniform name becomes a PER-DRAW HEAP FIELD iff
        //   * the effect CONSUMES it — taken AFTER derived-rule expansion, so an
        //     effect reading ModelViewProjTrafo detects its bases (ModelTrafo,
        //     ViewProjTrafo), matching what `rewrite` will actually gather, and
        //   * it is not a sampler (textures keep the bindless/atlas path), and
        //   * the RO's OWN uniform provider supplies it (TryGetUniform succeeds)
        //     in a packable ContentType.
        // Everything else — names the RO does not supply (RO-supplied names the
        // effect never reads simply don't matter) — stays an ordinary uniform:
        // NOT a field, the RO stays heapable, and the read resolves through the
        // backend/task scope (the bucket provider answers ValueNone for it).
        // NOTE a consumed+supplied UNPACKABLE uniform is NOT gathered either: its
        // RO-supplied value is effectively IGNORED (the plain read must resolve
        // from task scope or fails to bind) — supply a packable type instead.
        let consumedCache = System.Collections.Generic.Dictionary<string, string[]>()
        let consumedNonSamplerNames (e : Effect) =
            match consumedCache.TryGetValue e.Id with
            | true, v -> v
            | _ ->
                // the raw consumed non-sampler uniforms (NOT expanded): a derived
                // composite stays as-is — it becomes a compute OUTPUT, not its inlined
                // constituents. (Constituents are planned separately by the bucket.)
                let v =
                    e.Uniforms |> Map.toArray            // sorted by name
                    |> Array.choose (fun (n, p) ->
                        if typeof<ISampler>.IsAssignableFrom p.uniformType then None else Some n)
                consumedCache.[e.Id] <- v
                v
        // a derived composite is supported iff the RO supplies every base trafo its
        // recipe consumes (Model/View/Proj — the heap derives the composite from them
        // in fp64; the backward halves come from those same Trafo3d avals).
        let isTrafoSupply (t : System.Type) = t = typeof<Trafo3d> || t = typeof<M44d> || t = typeof<M44f>
        let baseSupplied (r : RenderObject) (b : string) =
            (match r.Uniforms.TryGetUniform(scope, cachedSym b) with ValueSome v -> isTrafoSupply v.ContentType | ValueNone -> false)
            || (b = Derived.MBASE &&
                (match r.Uniforms.TryGetUniform(scope, cachedSym "ModelTrafoStack") with ValueSome _ -> true | ValueNone -> false))
        let detectFields (r : RenderObject) (e : Effect) =
            consumedNonSamplerNames e
            |> Array.filter (fun n ->
                // a derived composite whose constituents are available (some recipe
                // alternative is satisfiable) is a COMPUTE OUTPUT; otherwise the name
                // must itself be RO-supplied + packable to be a (plain) field — covers
                // ordinary uniforms AND a recipe name supplied as a combined value.
                let derivable = Derived.pickRule (baseSupplied r) n |> Option.isSome
                if derivable then true
                else
                    match r.Uniforms.TryGetUniform(scope, Symbol.Create n) with
                    | ValueSome v ->
                        // requested type (what the shader declares) drives composite
                        // eligibility; the supplied ContentType drives leaf eligibility.
                        let requested = match e.Uniforms.TryFind n with | Some p -> p.uniformType | None -> v.ContentType
                        if packable.Contains v.ContentType || isCompositeType requested then true
                        else
                            diag (sprintf "uniform '%s' is effect-consumed and RO-supplied but UNPACKABLE (ContentType = %s) — the RO-supplied value is IGNORED (the read must resolve from task/backend scope or fails to bind); supply a packable type (M44f/Trafo3d/M44d/V4f/C4f/V3f/V2f/float32/float/int) to make it a per-draw field." n v.ContentType.Name)
                            false
                    | ValueNone ->
                        // a derived composite that did NOT derive and is NOT supplied:
                        // if a recipe constituent (Model/View/Proj/ViewProjTrafo) IS
                        // supplied but in a non-trafo (UNPACKABLE) type, say so — else
                        // a malformed Model silently disables derivation.
                        match Derived.tryRules n with
                        | Some alts ->
                            alts |> List.iter (fun (_, op) ->
                                Derived.constituentsOf op |> List.iter (fun c ->
                                    match r.Uniforms.TryGetUniform(scope, cachedSym c.CBase) with
                                    | ValueSome cv when not (isTrafoSupply cv.ContentType) ->
                                        diag (sprintf "derived uniform '%s' cannot be derived: its constituent '%s' is supplied in an UNPACKABLE type (%s — need Trafo3d/M44d/M44f)." n c.CBase cv.ContentType.Name)
                                    | _ -> ()))
                        | None -> ()
                        false)
        // device capability is constant — read it once, not per classified RO
        // (the Vulkan property re-walks extension/feature tables on every call)
        let supportsUnbounded = runtime.SupportsUnboundedSamplerArrays
        // the sampler eligibility verdict depends only on the EFFECT (uniform
        // declarations incl. sampler states) and the runtime caps — cache it
        // per effect id so per-RO classification skips the Map walks.
        let samplerIssueCache = System.Collections.Generic.Dictionary<string, string option>()
        let samplerIssueOf (e : Effect) : string option =
            match samplerIssueCache.TryGetValue e.Id with
            | true, v -> v
            | _ ->
                let v =
                    let samps = e.Uniforms |> Map.toArray |> Array.filter (fun (_, p) -> typeof<ISampler>.IsAssignableFrom p.uniformType)
                    let badType = samps |> Array.tryFind (fun (_, p) -> not (isBindlessSamplerType p.uniformType))
                    match badType with
                    | Some (n, p) -> Some (sprintf "sampler '%s' has unsupported type %s (supported: Sampler2d, Sampler2dArray, SamplerCube, Sampler2dShadow, SamplerCubeShadow)" n p.uniformType.Name)
                    | None ->
                        if samps.Length > 0
                           && not supportsUnbounded
                           && not (samps |> Array.forall (fun (_, p) -> p.uniformType = typeof<Sampler2d>)) then
                            Some "per-object textures need descriptor indexing for non-2d samplers (the atlas fallback handles Sampler2d only)"
                        else
                            // each input sampler gets its OWN generated array carrying its OWN
                            // state, so differing states / compare ops are fine — no rejection.
                            None
                samplerIssueCache.[e.Id] <- v
                v
        // eligible iff: an Effect surface, an INDEXED draw with a 2-/4-byte
        // readable index buffer OR a NON-indexed single-call Direct draw,
        // every attribute the SHADER reads
        // (effect.Inputs) either host-storage-decodable (incl. singletons) or
        // bindless vertex-pull eligible, and supported samplers. Anything else
        // -> passthrough, with a deduped diagnostic line when Heap.Diagnostics.
        // Returns (ineligibility reason, all-host-tight): the flag is the
        // host-vs-bindless geometry routing decision (hostIssue = None), so
        // factsOf never re-walks the attribute table.
        let classify (o : IRenderObject) : struct(string option * bool) =
            match o with
            | :? RenderObject as ro ->
                match ro.Surface with
                | Surface.Effect e ->
                    // Eligibility tests only the inputs the effect ACTUALLY NEEDS — those feeding a
                    // framebuffer-signature output — read from the effect's FREE dependency map
                    // (e.Dependencies; no DCE / no link). A dead input, e.g. a `{ v with … }` Flow
                    // passthrough the fragment never reads, feeds no signature output, so it isn't
                    // required and must NOT gate heap eligibility (else the whole scene falls through
                    // to individual draws for an attribute the shader doesn't consume). Cached/effect.
                    let needed = neededInputsOf e
                    // ── indices: OPTIONAL. Indexed draws need a 2-/4-byte
                    //    readable index buffer (host INativeBuffer or
                    //    downloadable backend buffer); NON-indexed draws ride
                    //    the heap too — the slot's header carries the -1
                    //    sentinel and decodeHeapIndex passes gl_VertexIndex
                    //    through. Their vertex count is read STRUCTURALLY from
                    //    the Direct draw call, so a single zero-offset call is
                    //    required. ──
                    let idxIssue =
                        match ro.Indices with
                        | None ->
                            match ro.DrawCalls with
                            | DrawCalls.Direct calls ->
                                match AVal.force calls with
                                | [| c |] when c.FirstIndex = 0 && c.BaseVertex = 0 -> None
                                | [||] -> None
                                | [| _ |] -> Some "non-indexed RO has a draw call with nonzero FirstIndex/BaseVertex (the heap draws [0, FaceVertexCount))"
                                | _ -> Some "non-indexed RO has multiple draw calls (the heap packs one record per RO)"
                            | _ -> Some "non-indexed RO with Indirect draw calls (vertex count unknowable at add; supply Direct calls or Indices)"
                        | Some ibv ->
                            let ies = elemSize ibv.ElementType
                            if ies <> 2 && ies <> 4 then
                                Some (sprintf "index element type %s unsupported (need a 2- or 4-byte integer type)" ibv.ElementType.Name)
                            elif not (isReadableIndex ibv) then
                                Some (sprintf "index buffer of type %s is neither host-readable nor a backend buffer" (ibv.Buffer.GetValue().GetType().Name))
                            else None
                    match idxIssue with
                    | Some _ -> struct(idxIssue, false)
                    | None ->
                        // ── attributes: HOST storage decode OR bindless vertex-pull ──
                        let hostIssue =
                            e.Inputs |> Map.toSeq |> Seq.filter (fun (name, _) -> needed.Contains name) |> Seq.tryPick (fun (name, inputT) ->
                                match ro.VertexAttributes.TryGetAttribute (cachedSym name) with
                                | ValueNone -> Some (sprintf "effect input '%s' has no vertex attribute on the RO" name)
                                | ValueSome bv ->
                                    if not (isHostTight bv) then
                                        Some (sprintf "attribute '%s' (%s) is not host-readable/tightly-packed (buffer %s, stride %d)" name bv.ElementType.Name (bv.Buffer.GetValue().GetType().Name) bv.Stride)
                                    elif not (hostDecodable bv.ElementType inputT) then
                                        Some (sprintf "attribute '%s' element type %s cannot be storage-decoded into shader input %s (supported sources: float32/V2f/V3f/V4f, C3f/C4f/C4b, int/V2i/V3i/V4i, float/V2d/V3d/V4d/C3d/C4d; supported inputs: float32/V2f/V3f/V4f, int/V2i/V3i/V4i)" name bv.ElementType.Name inputT.Name)
                                    else None)
                        // evaluated LAZILY: when the host path is eligible the
                        // bindless fallback's verdict is irrelevant (factsOf
                        // routes host-tight geometry to the packed path anyway)
                        let bindlessIssue () =
                            if not supportsUnbounded then
                                Some "vertex-pull needs descriptor indexing (unbounded SSBO arrays) — unavailable on this runtime (e.g. GL)"
                            elif instanceCountOf ro <> 1 then
                                Some "vertex-pull does not support pre-instanced draws (per-draw FirstInstance routes the handle)"
                            else
                                e.Inputs |> Map.toSeq |> Seq.filter (fun (name, _) -> needed.Contains name) |> Seq.tryPick (fun (name, _) ->
                                    match ro.VertexAttributes.TryGetAttribute (cachedSym name) with
                                    | ValueNone -> Some (sprintf "effect input '%s' has no vertex attribute on the RO" name)
                                    | ValueSome bv ->
                                        if isBindlessAttr bv then None
                                        else Some (sprintf "attribute '%s' (%s, offset %d, stride %d) is not vertex-pull eligible (need a float/int vector type with 4-byte-aligned offset/stride)" name bv.ElementType.Name bv.Offset bv.Stride))
                        let geomIssue =
                            match hostIssue with
                            | None -> None
                            | Some h ->
                                match bindlessIssue () with
                                | Some b -> Some (sprintf "%s; bindless fallback also ineligible: %s" h b)
                                | None -> None
                        match geomIssue with
                        | Some _ -> struct(geomIssue, false)
                        | None ->
                            // ── samplers: bindless per-type arrays / atlas fallback
                            //    (effect-level verdict, cached per effect id) ──
                            struct(samplerIssueOf e, hostIssue.IsNone)
                | s -> struct(Some (sprintf "surface is not an FShade Effect (%A) — only Surface.Effect render objects are heapable" (s.GetType().Name)), false)
            | _ -> struct(Some (sprintf "not a concrete RenderObject (%s) — command/multi render objects pass through" (o.GetType().Name)), false)

        // ── incremental driver ───────────────────────────────────────────
        // ONE updater aval per call: it consumes the object-set reader's
        // CHANGES (a true delta — no snapshot read, no HashSet.computeDelta)
        // plus the drained dirty-key set (per-RO KeyWatchers) and feeds each
        // bucket's persistent IncrementalBucket — an add/remove/key-flip is
        // O(changed) instead of O(bucket), for EVERY bucket kind (host or
        // bindless geometry, bindless-textured, atlas, instanced). Every
        // bucket-internal aval (indirect, headers, geometry, textures, arena
        // flush) hangs off the updater, so evaluation order doesn't matter.
        let objReader = objects.GetReader()

        // intern bucket keys to unique tokens, so the per-change grouping hashes
        // object references instead of full BucketKey records (20k ROs/change).
        // keyValues is the reverse map: token -> the RESOLVED key, so a bucket can
        // bake its pipeline state from the KEY's values (a member's dynamic state
        // aval can then never bend the bucket it leaves — it moves). BucketKey
        // carries its own hand-written equality/hash.
        let keyInterner = System.Collections.Generic.Dictionary<BucketKey, obj>()
        let keyValues = System.Collections.Generic.Dictionary<obj, BucketKey>(HashIdentity.Reference)
        let internKey k =
            match keyInterner.TryGetValue k with
            | true, tok -> tok
            | _ -> let tok = obj() in keyInterner.[k] <- tok; keyValues.[tok] <- k; tok

        let roFacts = System.Runtime.CompilerServices.ConditionalWeakTable<IRenderObject, RoFacts>()
        let factsOf (t : AdaptiveToken) (o : IRenderObject) =
            match roFacts.TryGetValue o with
            | true, f -> f
            | _ ->
                let struct(reason, hostGeom) = classify o
                let heapable =
                    match reason with
                    | None -> true
                    | Some why ->
                        diag (sprintf "pass-through: %s" why)
                        false
                let f =
                    if heapable then
                        let r = o :?> RenderObject
                        // geometry class (from classify): all-host-tight ->
                        // packed combined buffers; anything else (heapable =>
                        // bindless-eligible) -> vertex-pull.
                        let bindless = not hostGeom
                        let inst = instanceCountOf r > 1
                        let e = linkedEffect (match r.Surface with | Surface.Effect e -> e | _ -> failwith "Heap.ofRenderObjects: expected Surface.Effect")
                        // GPU trafo-chain eligibility: the effect DEPENDS ON Model
                        // (reads ModelTrafo, or a composite whose recipe consumes it)
                        // AND the RO exposes the UNFOLDED stack as aval<aval<Trafo3d>[]>.
                        // Then the slot's Model constituent is GPU-folded into the
                        // arena instead of uploaded.
                        let dependsOnModel =
                            consumedNonSamplerNames e |> Array.exists Derived.dependsOnModel
                        let chain =
                            not disableChain && dependsOnModel &&
                            (match r.Uniforms.TryGetUniform(scope, cachedSym "ModelTrafoStack") with
                             | ValueSome (:? aval<aval<Trafo3d>[]>) -> true
                             | _ -> false)
                        // per-draw field set: DETECTED (effect-consumed ∩ derivable/
                        // RO-supplied ∩ packable), interned. Derived composites (incl.
                        // ModelTrafo) stay as OUTPUT fields in BOTH modes — the chain
                        // only changes how the Model CONSTITUENT is produced.
                        let (fields, fieldMap) =
                            let detected = detectFields r e
                            let fm = internFields detected
                            lastAutoFields <- fst fm
                            fm
                        // geometry class + instanced-ness + field set PARTITION
                        // buckets (a bucket RO's surface / routing / geometry
                        // strategy and its baked field layout are fixed at
                        // creation), so fold them into the layout sig.
                        let layout =
                            if bindless then "gpu:" + bindlessSig r + (if inst then "|inst" else "")
                            elif inst then "host|inst"
                            else "host"
                            + (if chain then "|chain" else "")
                            + "|f:" + String.concat ";" fields
                        // the key is CONSTANT iff every aval it reads is — where depth /
                        // stencil / blend-constant avals only participate when the
                        // signature (or a constant blend mode) makes them relevant, so
                        // an irrelevant dynamic aval never forces the token-reactive
                        // full-regroup path.
                        let allConst =
                            let ra = r.RasterizerState
                            let bs = r.BlendState
                            let ds = r.DepthState
                            let ss = r.StencilState
                            ra.CullMode.IsConstant && ra.FrontFacing.IsConstant && ra.FillMode.IsConstant
                            && ra.Multisample.IsConstant && ra.ConservativeRaster.IsConstant
                            && bs.Mode.IsConstant && bs.ColorWriteMask.IsConstant
                            && bs.AttachmentMode.IsConstant && bs.AttachmentWriteMask.IsConstant
                            && (not hasDepth || (ds.Test.IsConstant && ds.Bias.IsConstant && ds.WriteMask.IsConstant && ds.Clamp.IsConstant))
                            && (not hasStencil || (ss.ModeFront.IsConstant && ss.WriteMaskFront.IsConstant && ss.ModeBack.IsConstant && ss.WriteMaskBack.IsConstant))
                            && (match r.ViewportState.Viewport with Some v -> v.IsConstant | None -> true)
                            && (match r.ViewportState.Scissor with Some v -> v.IsConstant | None -> true)
                        let constToken =
                            if not allConst then null
                            else
                                let k = modeKey layout t r
                                // the blend constant is keyed only when a (now-constant)
                                // effective mode consumes it — then IT must be constant too
                                if k.BlendConstant.IsSome && not r.BlendState.ConstantColor.IsConstant then null
                                else internKey k
                        { Heapable = true
                          Layout = layout
                          ConstToken = constToken
                          Bindless = bindless
                          Instanced = inst
                          Chain = chain
                          Fields = fields
                          FieldMap = fieldMap }
                    else
                        { Heapable = false; Layout = null; ConstToken = null; Bindless = false; Instanced = false
                          Chain = false
                          Fields = [||]; FieldMap = Map.empty }
                roFacts.Add(o, f)
                f

        // persistent driver state (mutated only inside the updater's evaluation)
        let caches = System.Collections.Generic.Dictionary<obj, IncrementalBucket>(HashIdentity.Reference)
        let passSet = System.Collections.Generic.HashSet<IRenderObject>(HashIdentity.Reference)
        // RO -> its (interned) bucket key token
        let roBucket = System.Collections.Generic.Dictionary<RenderObject, obj>(HashIdentity.Reference)
        // dynamic-key ROs -> their KeyWatcher (constant-key ROs have none); a
        // watcher marked by one of its state avals lands in dirtyKeys via the
        // updater's InputChangedObject, and the updater re-keys ONLY those ROs.
        let keyWatchers = System.Collections.Generic.Dictionary<RenderObject, KeyWatcher>(HashIdentity.Reference)
        let dirtyKeys = LockedSet<KeyWatcher>()
        // CLUSTERED buckets evaluate their IsActive gates HERE (class membership must
        // be settled before any buffer flush serializes it); a marked gate lands in
        // this set via InputChangedObject below.
        let dirtyClusterGates = LockedSet<GateWriter>()
        // adaptive watchers over formerly-snapshot values (geometry buffers,
        // draw-call shape, pick ids, model-stack structure); a marked writer
        // lands here and its OnChange applies the delta inside the updater.
        let dirtyDynWriters = LockedSet<DynWriter>()
        let updaterRef = ref (Unchecked.defaultof<aval<int>>)
        let version = ref 0

        let mkBucket (key : obj) (r0 : RenderObject) (f0 : RoFacts) =
            let e0 = match r0.Surface with | Surface.Effect e -> e | _ -> failwith "Heap.ofRenderObjects: expected Surface.Effect"
            let effect = linkedEffect e0
            let k = keyValues.[key]
            // signature color attachments this bucket's effect does NOT write: the
            // bucket RO write-masks them off explicitly (their pixels keep the
            // cleared value by construction, see the BlendState bake in bucketRO)
            let masked =
                let written = writtenAttachments e0
                sigColorNames
                |> Array.filter (fun n -> written |> Array.forall (fun (wn, _) -> wn <> n))
                |> Array.map Symbol.Create
            // field names/layout come from the FACTS (per-bucket: the field set is
            // part of the bucket key, so every member shares f0's interned set).
            // Every bucket allocates from the CALLER's shared storage — cross-bucket
            // and cross-heap dedup of geometry, uniforms and constituents.
            // Pickability is known BY CONSTRUCTION: HeapNode attaches the `HeapPickId` uniform to
            // exactly the ROs it pick-composed. A bucket carrying that marker is a pick bucket (route
            // it into the dom's PickId pass); one without it (e.g. a `Sg.NoEvents` sub-scene) is not.
            let pickable =
                match r0.Uniforms.TryGetUniform(Ag.Scope.Root, Symbol.Create "HeapPickId") with
                | ValueSome _ -> true
                | _ -> false
            let c = IncrementalBucket(runtime, storage, f0.Fields, f0.FieldMap, effect, r0, updaterRef.Value, f0.Bindless, f0.Instanced,
                                      k, masked, f0.Chain, picking, pickable, deregister)
            caches.[key] <- c
            c

        let updater =
            { new AVal.AbstractVal<int>() with
                // a KeyWatcher marked by one of its state avals lands in the dirty
                // set; everything else (objReader, region writers, …) marks normally
                override x.InputChangedObject(_, o) =
                    match o with
                    | :? KeyWatcher as w -> dirtyKeys.Add w |> ignore
                    | :? GateWriter as g -> dirtyClusterGates.Add g |> ignore
                    | :? DynWriter as d -> dirtyDynWriters.Add d |> ignore
                    | _ -> ()
                override x.Compute(t) =
                    EditProf.dump ()
                    let delta = EditProf.time "upd:delta" (fun () -> objReader.GetChanges t)

                    // the RO's interned key token: constant keys were interned once in
                    // factsOf; a dynamic key evaluates through the RO's KeyWatcher
                    // (created here on first add), whose token read (re)subscribes it.
                    let keyTokenOf (r : RenderObject) (f : RoFacts) =
                        if not (isNull f.ConstToken) then f.ConstToken
                        else
                            let w =
                                match keyWatchers.TryGetValue r with
                                | true, w -> w
                                | _ ->
                                    let w = KeyWatcher r
                                    keyWatchers.[r] <- w
                                    w
                            w.Update(t, fun tok -> internKey (modeKey f.Layout tok r))
                    let addTo (key : obj) (r : RenderObject) (f : RoFacts) =
                        roBucket.[r] <- key
                        let c =
                            match caches.TryGetValue key with
                            | true, c -> c
                            | _ -> mkBucket key r f
                        c.AddOne(t, r)
                    let removeFrom (key : obj) (r : RenderObject) =
                        roBucket.Remove r |> ignore
                        match caches.TryGetValue key with
                        | true, c ->
                            c.RemoveOne r
                            if c.Count = 0 then
                                c.Dispose()
                                caches.Remove key |> ignore
                        | _ -> ()

                    // ── membership delta: removals FIRST so their slots / arena
                    //    offsets / texture indices are reusable by this very update
                    //    (paired add+remove churn then keeps the slot high-water at
                    //    the live count). ──
                    for op in delta do
                        match op with
                        | Rem(_, o) ->
                            match roFacts.TryGetValue o with
                            | true, f when f.Heapable ->
                                let r = o :?> RenderObject
                                (match keyWatchers.TryGetValue r with
                                 | true, w ->
                                     w.Dispose()
                                     keyWatchers.Remove r |> ignore
                                 | _ -> ())
                                match roBucket.TryGetValue r with
                                | true, key -> removeFrom key r
                                | _ -> ()
                            | _ -> passSet.Remove o |> ignore
                        | Add _ -> ()
                    for op in delta do
                        match op with
                        | Rem _ -> ()
                        | Add(_, o) ->
                            let f = factsOf t o
                            if not f.Heapable then passSet.Add o |> ignore
                            else
                                let r = o :?> RenderObject
                                addTo (keyTokenOf r f) r f

                    // ── dirty keys: re-key ONLY the ROs whose watched state avals
                    //    flipped (all ROs sharing a flipped aval are collected); an
                    //    RO whose key actually changed MOVES buckets, the rest of
                    //    the heap is untouched. There is no full-regroup path. ──
                    EditProf.time "upd:dirtyKeys" (fun () ->
                     for w in dirtyKeys.GetAndClear() do
                        if not w.IsDisposed then       // removed by this very delta
                            let r = w.Ro
                            match roBucket.TryGetValue r, roFacts.TryGetValue r with
                            | (true, oldKey), (true, f) ->
                                let newKey = w.Update(t, fun tok -> internKey (modeKey f.Layout tok r))
                                if not (System.Object.ReferenceEquals(newKey, oldKey)) then
                                    removeFrom oldKey r
                                    addTo newKey r f
                            | _ -> ())

                    // cluster gate flips: apply class add/removes NOW (before flushes)
                    EditProf.time "upd:gates" (fun () ->
                     for g in dirtyClusterGates.GetAndClear() do
                        if not g.IsDisposed && not (isNull g.OnCluster) then g.OnCluster.Invoke t)

                    // adaptive value flips (geometry bytes, draw-call shape, pick
                    // ids, model-stack structure): apply O(changed) NOW — arena
                    // staging + header/record mutations land before any flush
                    EditProf.time "upd:dynWriters" (fun () ->
                     for d in dirtyDynWriters.GetAndClear() do
                        if not d.IsDisposed && not (isNull d.OnChange) then d.OnChange.Invoke t)

                    lastBucketCount <- caches.Count
                    let mutable chainB = 0
                    let mutable chainD = 0
                    EditProf.time "upd:syncPages" (fun () ->
                     for KeyValue(_, c) in caches do
                        // materialize each bucket's per-page render/derive ROs to match its storage,
                        // DETERMINISTICALLY here in the membership update — so they're present in
                        // `resultAval` before any render builds its command buffer (no lazy/first-frame gap).
                        c.SyncPages()
                        if c.IsChain then chainB <- chainB + 1; chainD <- chainD + c.ChainDistinct)
                    lastChainBuckets <- chainB
                    if chainB > 0 then lastDistinctLinks <- chainD
                    version.Value <- version.Value + 1
                    version.Value } :> aval<int>
        updaterRef.Value <- updater
        let teardown () =
            // free every bucket (GPU buffers + object-count CPU) and drop the reader
            for KeyValue(_, c) in caches do c.Dispose()
            caches.Clear()
            passSet.Clear()
            roBucket.Clear()
            for KeyValue(_, w) in keyWatchers do w.Dispose()
            keyWatchers.Clear()
            dirtyKeys.GetAndClear() |> ignore
            dirtyDynWriters.GetAndClear() |> ignore
            match box objReader with
            | :? System.IDisposable as d -> d.Dispose()
            | _ -> ()
        let mutable __resultN = 0
        let mutable __lastHash = 0
        let mutable __lastArr : IRenderObject[] = [||]
        let resultAval =
            updater |> AVal.map (fun _ ->
                // derive pre-passes ∪ per-page bucket sub-draws ∪ untouched passthrough.
                // a bucket now contributes ONE HeapRenderObject bundling its per-page derives + page
                // draws — the backend records derive(page i)→barrier→draw(page i) as one submission,
                // so no render-task split can separate them and no page>0 derive goes stale.
                let out = System.Collections.Generic.List<IRenderObject>(caches.Count + passSet.Count)
                for KeyValue(_, c) in caches do
                    out.Add c.HeapRO
                for o in passSet do out.Add o
                let arr = out.ToArray()
                __resultN <- __resultN + 1
                let h = if arr.Length > 0 then LanguagePrimitives.PhysicalHash arr.[0] else 0
                if __resultN % 10 = 0 || h <> __lastHash then Log.line "[result] #%d: %d ROs, firstHash=%d changed=%b" __resultN arr.Length h (h <> __lastHash)
                __lastHash <- h
                // IDENTITY-STABLE output: content-only edits bump the version but
                // produce the SAME RO instances — return the previous array object
                // so the downstream set-diff emits an EMPTY delta and the command
                // tree's actual-change reporting skips the re-record.
                let same =
                    arr.Length = __lastArr.Length
                    && (let mutable eq = true
                        for i in 0 .. arr.Length - 1 do
                            if not (System.Object.ReferenceEquals(arr.[i], __lastArr.[i])) then eq <- false
                        eq)
                if same then __lastArr
                else
                    __lastArr <- arr
                    arr)
        (resultAval |> ASet.ofAVal), teardown

    /// Collapse an adaptive set of N render objects into bucket render objects.
    /// Allocates NOTHING up front (ref-count zero): the heap's machinery — input
    /// reader, per-bucket CPU model and ALL GPU buffers — is built lazily on the
    /// FIRST activation (a render task picking up the heap) and torn down COMPLETELY
    /// when the LAST task drops it (releasing exactly its own ref-counts in the
    /// shared storage). Re-activation rebuilds from scratch; concurrent tasks share
    /// one machinery via the ref-count. The drawing is carried by the bucket ROs;
    /// the activation itself rides on an ActivationRenderObject that both backends
    /// ignore for rendering and only activate/deactivate.
    let private ofRenderObjectsCore (getStorage : IRuntime -> HeapStorage) (releaseStorage : unit -> unit) (picking : bool) (deregister : int -> unit) (signature : IFramebufferSignature) (objects : aset<IRenderObject>) : aset<IRenderObject> =
        let gate = obj()
        let mutable activeTasks = 0
        let mutable shared : (aset<IRenderObject> * (unit -> unit)) voption = ValueNone
        // bumped on teardown so the (shared) bucket reader re-evaluates and drops
        // the stale buckets; the next task to pull then rebuilds from scratch.
        let gen = cval 0

        // Build lazily on pull; idempotent — rebuilds after a teardown set it to None.
        let ensureBuilt () =
            lock gate (fun () ->
                match shared with
                | ValueSome (s, _) -> s
                | ValueNone ->
                    buildInvocations <- buildInvocations + 1
                    let r = buildHeap (getStorage (signature.Runtime :?> IRuntime)) picking deregister signature objects
                    shared <- ValueSome r
                    fst r)

        // Deterministic teardown: each task holds the ActivationRenderObject's
        // activation and disposes it when it drops the heap; the LAST drop frees
        // everything (GPU + object-count CPU) and bumps `gen` so the bucket reader
        // re-syncs to empty. A later task rebuilds from scratch on its first pull.
        let activate () =
            lock gate (fun () -> activeTasks <- activeTasks + 1)
            { new System.IDisposable with
                member _.Dispose() =
                    let td =
                        lock gate (fun () ->
                            activeTasks <- max 0 (activeTasks - 1)
                            if activeTasks = 0 then
                                match shared with
                                | ValueSome (_, td) -> shared <- ValueNone; ValueSome td
                                | ValueNone -> ValueNone
                            else ValueNone)
                    match td with
                    | ValueSome td ->
                        td ()
                        releaseStorage ()      // auto-storage bookkeeping (no-op for caller-owned)
                        transact (fun () -> gen.Value <- gen.Value + 1)
                    | ValueNone -> () }

        let activationRO = ActivationRenderObject(RenderPass.main, Ag.Scope.Root, activate)
        // `bind` owns the inner reader: it forwards the live machinery's incremental
        // deltas, and on a teardown (gen bump → new machinery, or none) it drops the
        // old reader and adopts the new one, emitting the switch itself. Building in
        // the mapping makes the buckets surface in the first evaluation (no lag).
        let buckets = gen |> ASet.bind (fun _ -> ensureBuilt())
        ASet.union (ASet.single (activationRO :> IRenderObject)) buckets

    /// SIGNATURE-DEFERRED collapse. The caller cannot (and must not) bake a framebuffer
    /// signature — the real render target may carry attachments it can't know (e.g. a
    /// Normals G-buffer added by a post-processing pass). So the heap returns
    /// `SignatureDependentRenderObject`s that build LAZILY at compile time against the
    /// REAL signature; the heap's attribute-DCE then matches the backend's
    /// shader-output-DCE exactly.
    ///
    /// Emits an OPAQUE + a TRANSPARENT variant (both always present, reactive): the OIT split
    /// in `TransparencyRenderTask` runs on the UN-expanded set, so the heap's transparent
    /// buckets must be reachable through a `SignatureDependentRenderObject` flagged
    /// `IsTransparent = true`, or they'd route to the opaque pass (transparency regression).
    /// When there are no transparent buckets the transparent variant's `Expand` yields an
    /// empty set → the WrappedTask's direct-opaque fast path kicks in (no OIT cost).
    ///
    /// The per-signature build is MEMOIZED by ATTACHMENT SEMANTICS (sorted color
    /// (name,format)), NOT by signature identity: the opaque pass compiles against the
    /// intermediate FBO signature and the transparent pass against the user signature, but
    /// those carry IDENTICAL color attachments — so both collapse to ONE `ofRenderObjectsCore`
    /// build = ONE ref-counted activation lifecycle (the shared `ActivationRenderObject`
    /// rides BOTH variants' Expand, so `activeTasks` counts every task and teardown fires
    /// only when the last one drops). The dom pick path uses this core too: its pick
    /// signature (user semantics + `PickId` attachment) keys to its own build. ALL builds
    /// allocate from the caller's shared `HeapStorage`.
    let private deferredCore (getStorage : IRuntime -> HeapStorage) (releaseStorage : unit -> unit) (picking : bool) (deregister : int -> unit) (objects : aset<IRenderObject>) : aset<IRenderObject> =
        let gate = obj()
        let memo = System.Collections.Generic.Dictionary<string, aset<IRenderObject>>()
        let build (signature : IFramebufferSignature) : aset<IRenderObject> =
            // attachment-semantics key: the ONLY signature aspect the heap build depends on is
            // the set of color attachment (NAME, format) — linkDCE (HeapPool linkDCE) and
            // heap-eligibility route by att.Name and IGNORE the slot. So key by sorted
            // (name, format), NOT slot: the intermediate FBO signature may reassign slots vs the
            // user signature (CreateFramebufferSignature packs contiguously) yet carries the same
            // named attachments → same key → ONE shared build. Depth/samples don't affect the
            // heapified ROs either, so they're excluded too.
            let key =
                signature.ColorAttachments
                |> Map.toList
                |> List.map (fun (_, att) -> sprintf "%s:%A" (string att.Name) att.Format)
                |> List.sort
                |> String.concat ","
            lock gate (fun () ->
                match memo.TryGetValue key with
                | true, s -> s
                | _ ->
                    Log.line "[Heap] deferred build: picking=%b sig=[%s]" picking key
                    let s = ofRenderObjectsCore getStorage releaseStorage picking deregister signature objects
                    memo.[key] <- s
                    s)
        // route buckets/passthrough by transparency; the ActivationRenderObject rides BOTH
        // variants so every compiling task ref-counts the shared build.
        let isActivation (ro : IRenderObject) = ro :? ActivationRenderObject
        let bucketTransparent (ro : IRenderObject) =
            match ro with
            | :? HeapRenderObject as h -> h.IsTransparent
            | :? RenderObject as r -> r.IsTransparent
            | _ -> false
        let opaque =
            SignatureDependentRenderObject(
                RenderPass.main, Ag.Scope.Root, false,
                fun signature -> build signature |> ASet.filter (fun ro -> isActivation ro || not (bucketTransparent ro)))
        let transparent =
            SignatureDependentRenderObject(
                RenderPass.main, Ag.Scope.Root, true,
                fun signature -> build signature |> ASet.filter (fun ro -> isActivation ro || bucketTransparent ro))
        // pickable is decided here (before expansion) so PickProducer routes these into the
        // PickId-attachment compile; the signature (user semantics + PickId) reaches Expand.
        opaque.IsPickable <- picking
        transparent.IsPickable <- picking
        ASet.ofList [ opaque :> IRenderObject; transparent :> IRenderObject ]

    /// THE heap entry point: collapse render objects into bucket render objects
    /// (N per-object draws → one indirect multidraw per bucket), allocating from
    /// the caller's `storage` (create one via `runtime.CreateHeapStorage()`). The
    /// SAME storage may back any number of heaps — e.g. the main render and a
    /// shadow pass — so shared geometry/uniforms live in GPU memory once. The
    /// framebuffer signature is resolved at COMPILE time (signature-deferred);
    /// non-heapable render objects pass through unchanged.
    let ofRenderObjects (storage : HeapStorage) (objects : aset<IRenderObject>) : aset<IRenderObject> =
        deferredCore (fun _ -> storage) ignore false ignore objects

    /// Like `ofRenderObjects`, but with a PRIVATE auto-managed storage: created
    /// lazily at the first build (the runtime comes off the compile-time signature)
    /// and dropped when the last render task releases the heap — the storage lives
    /// and dies with the heap. Use the explicit `ofRenderObjects storage` form to
    /// share one storage across several heaps/passes (e.g. shadow mapping).
    let ofRenderObjectsAuto (objects : aset<IRenderObject>) : aset<IRenderObject> =
        let gate = obj()
        let mutable store : HeapStorage voption = ValueNone
        let mutable refs = 0
        let get (runtime : IRuntime) =
            lock gate (fun () ->
                refs <- refs + 1
                match store with
                | ValueSome s -> s
                | ValueNone -> let s = HeapStorage(runtime) in store <- ValueSome s; s)
        let release () =
            lock gate (fun () ->
                refs <- max 0 (refs - 1)
                if refs = 0 then store <- ValueNone)
        deferredCore get release false ignore objects

    /// The PICKING variant (entered via the dom heap node): each input RO's
    /// "HeapPickId" uniform is captured per-slot into the "HeapPickIds" SSBO the dom
    /// heap pick-shader writes into the pick buffer; `deregister` is called with a
    /// slot's pick id when that slot is freed. Signature-deferred like
    /// `ofRenderObjects` — the pick signature (user semantics + `PickId`) reaches the
    /// build at compile time, so extra attachments survive.
    let ofRenderObjectsPicking (storage : HeapStorage) (deregister : int -> unit) (objects : aset<IRenderObject>) : aset<IRenderObject> =
        // PARTITION by pickability — known BY CONSTRUCTION from the `HeapPickId`
        // marker uniform (HeapNode attaches it to exactly the members it
        // pick-composed). The dom pick system's semantics live in its ROUTING:
        // pickable geometry renders FIRST into the PickId pass, unpickable
        // (Sg.NoEvents) geometry renders AFTER into the shared color+depth
        // through a signature WITHOUT the pick attachment — it occludes
        // visually but leaves the pick ids of geometry behind it intact
        // (pick-THROUGH). So unpickable members build a SEPARATE (plain,
        // IsPickable=false) heap over the SAME storage: dom routes those SDRs
        // into the base pass. Routing them into the PickId pass instead would
        // either occlude picks (id write) or crash (the backend links a
        // non-PickId-writing bucket against the pick pass by auto-passing the
        // output through the stages — a phantom `PickId` VERTEX INPUT).
        // Partitioning the INPUT (two builds) rather than filtering one build's
        // SDRs also keeps every slot ingested exactly once.
        let pickSym = Symbol.Create "HeapPickId"
        let isPickMarked (ro : IRenderObject) =
            match ro with
            | :? RenderObject as r ->
                (match r.Uniforms.TryGetUniform(Ag.Scope.Root, pickSym) with
                 | ValueSome _ -> true
                 | _ -> false)
            | _ -> false
        ASet.union
            (deferredCore (fun _ -> storage) ignore true deregister (objects |> ASet.filter isPickMarked))
            (deferredCore (fun _ -> storage) ignore false ignore (objects |> ASet.filter (isPickMarked >> not)))

    // ── fp64 derived-uniform compute pre-pass ───────────────────────────
    // Wombat derives per-object trafos (ModelViewProjTrafo, NormalMatrix, ...)
    // in a GPU compute pre-pass at df32 precision; we use REAL fp64 (M44d /
    // dmat4, shaderFloat64). The pre-pass computes the derived matrices once per
    // object per frame (not per vertex) in double precision and writes them as
    // f32 into a heap arena the render gathers by gl_InstanceIndex. Camera-
    // relative math (View * Model) stays precise at geodetic scale where an f32
    // inline ModelViewProj would jitter. Reactive: the arena re-runs the compute
    // whenever any model or the camera changes (AVal.custom over the inputs).


/// Runtime factory for the shareable heap storage.
[<AbstractClass; Sealed; System.Runtime.CompilerServices.Extension>]
type HeapStorageRuntimeExtensions private() =
    /// Create a shareable heap data store. Pass it to any number of
    /// `Heap.ofRenderObjects` / `Heap.ofRenderObjectsPicking` calls (e.g. the main
    /// render and a shadow pass) — their allocations dedup in the shared pages.
    /// `pageSizeInBytes` (default 1 GiB) is clamped to the device's
    /// storage-buffer range.
    [<System.Runtime.CompilerServices.Extension>]
    static member CreateHeapStorage(runtime : IRuntime,
                                    [<System.Runtime.InteropServices.Optional;
                                      System.Runtime.InteropServices.DefaultParameterValue(0L)>] pageSizeInBytes : int64) : Heap.HeapStorage =
        if pageSizeInBytes > 0L then Heap.HeapStorage(runtime, pageSizeInBytes) else Heap.HeapStorage(runtime)


// ── Sg.heap — scene-graph node for Heap.ofRenderObjects ─────────────────────
// Collapses the subtree's render objects through `Heap.ofRenderObjects` against
// the given (shareable) storage. Non-heapable ROs pass through unchanged (that
// is `ofRenderObjects`' own behaviour), so a mixed subtree degrades gracefully.
//
// Dual-protocol like every Sg node (mirrors GeometrySetNode):
//   * Ag path        — RenderObjects rule (HeapApplicatorSem below)
//   * ISimpleSg path — GetRenderObjects
[<AutoOpen>]
module HeapSgExtensions =
    open Aardvark.SceneGraph.Simple

    module Sg =
        type HeapApplicator(storage : Heap.HeapStorage, child : aval<ISg>) =
            inherit Sg.AbstractApplicator(child)

            /// the (shareable) storage the subtree's heap allocates from
            member _.Storage = storage

            // TS-direct — child ROs gathered with the unchanged TraversalState, then collapsed.
            interface ISimpleSg with
                member _.GetRenderObjects ts =
                    child
                    |> ASet.bind (fun c -> SimpleDispatch.Get(c, ts))
                    |> Heap.ofRenderObjects storage

            new(storage : Heap.HeapStorage, child : ISg) = HeapApplicator(storage, AVal.constant child)

        /// Collapses the subtree's render objects through `Heap.ofRenderObjects`
        /// (N per-object draws -> one indirect multidraw per bucket), allocating
        /// from `storage` (create one via `runtime.CreateHeapStorage()`; share it
        /// across heaps/passes at will). Non-heapable render objects pass through
        /// unchanged. The framebuffer signature is resolved at compile time.
        let heap (storage : Heap.HeapStorage) (sg : ISg) : ISg = HeapApplicator(storage, AVal.constant sg) :> ISg


namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive

module HeapApplicatorSemantics =

    [<Rule>]
    type HeapApplicatorSem() =

        // Ag path: same child traversal as the generic IApplicator rule
        // (RenderObjectSem.RenderObjects in Semantics/RenderObject.fs), then the
        // collapse against the node's storage. This concrete-type rule wins
        // over the IApplicator rule (most-specific dispatch — cf. NaiveLod.LodSem).
        member x.RenderObjects(h : Sg.HeapApplicator, scope : Ag.Scope) : aset<IRenderObject> =
            aset {
                let! c = h.Child
                yield! c.RenderObjects(scope)
            }
            |> Heap.ofRenderObjects h.Storage
