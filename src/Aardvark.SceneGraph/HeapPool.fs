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
// Two entry points:
//   * Heap.ofRenderObjects — adaptive aset<IRenderObject> -> aset<IRenderObject>
//     transform; buckets by effect, stores host geometry (attributes AND
//     indices, incl. SingleValueBuffer singletons) as per-allocation-headed
//     ranges in the bucket's storage arena — NO fixed-function vertex input;
//     draws are non-indexed and the rewritten vertex shader storage-decodes
//     everything (wombat-style). Dirty-tracks the arena (sparse per-frame
//     mutation uploads only changed sub-ranges).
//   * Heap.HeapScene       — imperative, growable single bucket with O(1)
//     Add/Remove (free-list slots); for streaming workloads.

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
        member x.HeapPageId : int = uniform?HeapPageId
        // GPU trafo-chain: per-slot GPU-folded ModelTrafo (M44f), written by the
        // composeModel compute pass and gathered by gl_InstanceIndex / gl_DrawID.
        member x.HeapModelChain : M44f[] = uniform?StorageBuffer?HeapModelChain
        // Bindless geometry: ONE flat float32 SSBO array indexed by handle
        // (gl_InstanceIndex). Element [h] is object h's interleaved vertex floats;
        // each attribute is decoded by component count at a fixed offset (like the
        // host arena's HeapData) — type-agnostic, any number of attributes, and a
        // flat float[] avoids std430 vec3 16-byte-stride misalignment.
        member x.HeapVertexData  : float32[][] = uniform?StorageBuffer?HeapVertexData
        // the SAME per-object buffers bound a second time as an int view, so integral
        // attributes decode their 4-byte slots as ints (bit pattern is identical).
        member x.HeapVertexDataI : int[][]     = uniform?StorageBuffer?HeapVertexDataI
        // per-(object,sampler) index into the DEDUPED per-type HeapTextures<T> array,
        // at slot*Kt + kt (Kt = that type's sampler count in the effect). One index
        // buffer per supported sampler type.
        member x.HeapTexIndices2d   : int[] = uniform?StorageBuffer?HeapTexIndices2d
        member x.HeapTexIndicesCube : int[] = uniform?StorageBuffer?HeapTexIndicesCube
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

    /// One per-draw uniform binding: how big it is in the arena (in floats),
    /// its aval identity (for dedup), and how to pack its current value.
    type HeapInput =
        { sizeF    : int
          key      : IAdaptiveValue
          packInto : AdaptiveToken -> float32[] -> int -> unit }

    let private cint (v : int) : Expr<int> = Expr.Value v |> Expr.Cast

    // ── smart constructors (extend the type table here + in gatherFor) ──
    let private packM44 (m : M44f) (a : float32[]) (o : int) =
        a.[o+0]  <- m.M00; a.[o+1]  <- m.M01; a.[o+2]  <- m.M02; a.[o+3]  <- m.M03
        a.[o+4]  <- m.M10; a.[o+5]  <- m.M11; a.[o+6]  <- m.M12; a.[o+7]  <- m.M13
        a.[o+8]  <- m.M20; a.[o+9]  <- m.M21; a.[o+10] <- m.M22; a.[o+11] <- m.M23
        a.[o+12] <- m.M30; a.[o+13] <- m.M31; a.[o+14] <- m.M32; a.[o+15] <- m.M33

    let mat4 (av : aval<M44f>) : HeapInput =
        { sizeF = 16; key = av; packInto = fun t a o -> packM44 (av.GetValue t) a o }

    let v4 (av : aval<V4f>) : HeapInput =
        { sizeF = 4; key = av; packInto = fun t a o ->
            let v = av.GetValue t in a.[o] <- v.X; a.[o+1] <- v.Y; a.[o+2] <- v.Z; a.[o+3] <- v.W }

    let v3 (av : aval<V3f>) : HeapInput =
        { sizeF = 3; key = av; packInto = fun t a o ->
            let v = av.GetValue t in a.[o] <- v.X; a.[o+1] <- v.Y; a.[o+2] <- v.Z }

    let f32 (av : aval<float32>) : HeapInput =
        { sizeF = 1; key = av; packInto = fun t a o -> a.[o] <- av.GetValue t }

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

    /// Throw a clear error if `runtime` cannot run the heap path. Pass
    /// `textures = true` to also require unbounded (bindless) sampler arrays.
    let checkSupport (textures : bool) (runtime : IRuntime) =
        if not (isSupported runtime) then
            failwith "Heap: GL backend requires GL 4.6+ with GL_ARB_shader_draw_parameters (gl_DrawID). Vulkan/MoltenVK do not require it (per-instance slot attribute fallback)."
        if textures && not runtime.SupportsUnboundedSamplerArrays then
            failwith "Heap: runtime does not support unbounded (bindless) sampler arrays (descriptor indexing); per-object textures via the heap are unavailable on this device (e.g. the GL backend)."

    /// Type-driven gather: given the base element offset in HeapData, build
    /// the expression that reconstructs a value of `typ`.
    let private gatherFor (typ : System.Type) (off : Expr<int>) : Expr =
        if   typ = typeof<int>     then <@ int (uniform.HeapData.[%off]) @>.Raw
        elif typ = typeof<float32> then <@ uniform.HeapData.[%off] @>.Raw
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

    // ── per-object textures via per-TYPE bindless sampler arrays ───────────
    // Each sampler uniform the effect declares becomes an indexed read of the
    // unbounded array for its sampler TYPE (HeapTextures2d : sampler2d[],
    // HeapTexturesCube : samplerCube[], …). Within a type, object i's kt-th sampler
    // reads HeapTextures<T>.[ HeapTexIndices<T>[slot*Kt + kt] ] — the index buffer
    // dedups, so the array holds only the bucket's distinct textures (≤ array cap).
    // The arrays are module-level (built ONCE) so the sampler CE never sits inside a
    // spliced per-call quotation (which makes the F# Release optimizer grind).
    let private heapTex2d   : Sampler2d[]   = sampler2d   { textureArray uniform?HeapTextures2d   -1 }
    let private heapTexCube : SamplerCube[] = samplerCube { textureArray uniform?HeapTexturesCube -1 }

    /// supported bindless sampler types: F# type ->
    ///   (array texture-semantic, index buffer semantic, FShade uniform KEY).
    /// The FShade key is the module-level VALUE name (e.g. "heapTex2d") under which the
    /// sampler array appears in shaderUniforms — it MUST equal the `let` name below; the
    /// texture semantic (e.g. "HeapTextures2d") is the `uniform?…` name the provider binds.
    let private bindlessTypeInfo (ty : System.Type) : (string * string * string) option =
        if   ty = typeof<Sampler2d>   then Some ("HeapTextures2d",   "HeapTexIndices2d",   "heapTex2d")
        elif ty = typeof<SamplerCube> then Some ("HeapTexturesCube", "HeapTexIndicesCube", "heapTexCube")
        else None
    let private isBindlessSamplerType (ty : System.Type) = (bindlessTypeInfo ty).IsSome

    /// read object `slot`'s kt-th sampler of type `ty` from its per-type bindless array
    let private samplerReadFor (ty : System.Type) (slot : Expr<int>) (kCountT : int) (kt : int) : Expr =
        if   ty = typeof<Sampler2d>   then <@ heapTex2d.[   uniform.HeapTexIndices2d.[   (%slot) * kCountT + kt ] ] @>.Raw
        elif ty = typeof<SamplerCube> then <@ heapTexCube.[ uniform.HeapTexIndicesCube.[ (%slot) * kCountT + kt ] ] @>.Raw
        else failwithf "Heap: unsupported bindless sampler type %A" ty

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

    /// rewrite each sampler read into its per-type bindless array read.
    /// `byName` : samplerName -> (samplerType, kt, Kt-for-that-type)
    let private rewriteSamplers (slot : Expr<int>) (byName : Map<string, System.Type * int * int>) (e : Effect) =
        if Map.isEmpty byName then e
        else
            e |> Effect.substituteUniforms (fun name _ _ _ ->
                match Map.tryFind name byName with
                | Some (ty, kt, kCountT) -> Some (samplerReadFor ty slot kCountT kt)
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
                                | v -> v
                            { p with uniformValue = nv }
                        | None -> p)
                { shader with shaderUniforms = us })

    /// Build a reactive heap-rendered scene graph for a single bucket: one
    /// effect, one shared geometry, N draws differing only by per-draw avals.
    let scene (mode : IndexedGeometryMode)
              (positions : V3f[]) (normals : V3f[]) (index : int[])
              (effect : Effect)
              (draws : Map<string, HeapInput>[]) : ISg =

        // schema = union of per-draw uniform names (assumed uniform across draws)
        let names = draws |> Array.collect (fun d -> Map.toArray d |> Array.map fst) |> Array.distinct
        let fieldStride = names.Length
        let nameToField = names |> Array.mapi (fun i n -> n, i) |> Map.ofArray

        // dedup arena regions by aval identity
        let regionOf = System.Collections.Generic.Dictionary<IAdaptiveValue, int>(HashIdentity.Reference)
        let distinct = System.Collections.Generic.List<HeapInput * int>()
        let mutable cursor = 0
        let headers = Array.zeroCreate<int> (draws.Length * fieldStride)
        draws |> Array.iteri (fun di draw ->
            for KeyValue(name, input) in draw do
                let off =
                    match regionOf.TryGetValue input.key with
                    | true, o -> o
                    | _ ->
                        let o = cursor
                        cursor <- cursor + input.sizeF
                        regionOf.[input.key] <- o
                        distinct.Add(input, o)
                        o
                headers.[di * fieldStride + nameToField.[name]] <- off)
        let totalF = cursor

        // reactive arena: depends on every distinct input aval (via token);
        // a mark on any re-packs the array — offsets/headers never move.
        let arena =
            AVal.custom (fun t ->
                let a = Array.zeroCreate<float32> totalF
                for (input, o) in distinct do input.packInto t a o
                a)

        // route per-draw by gl_DrawID (firstInstance stays 0) so this works on GL
        // too (GL's gl_InstanceID omits baseInstance; gl_DrawID does not).
        let indirect =
            Array.init draws.Length (fun _ ->
                DrawCallInfo(FaceVertexCount = index.Length, FirstIndex = 0, BaseVertex = 0,
                             FirstInstance = 0, InstanceCount = 1))
            |> IndirectBuffer.ofArray

        let effect = rewrite (<@ getDrawId() @>) nameToField fieldStride Map.empty effect

        Sg.indirectDraw mode (AVal.constant indirect)
        |> Sg.vertexBuffer DefaultSemantic.Positions (BufferView(AVal.constant (ArrayBuffer(positions) :> IBuffer), typeof<V3f>))
        |> Sg.vertexBuffer DefaultSemantic.Normals   (BufferView(AVal.constant (ArrayBuffer(normals)   :> IBuffer), typeof<V3f>))
        |> Sg.index' index
        |> Sg.uniform "HeapData"    arena
        |> Sg.uniform "HeapHeaders" (AVal.constant headers)
        |> Sg.effect [ effect ]

    /// Render N INSTANCES of one shared geometry in a SINGLE instanced draw
    /// (instanceCount = N, firstInstance = 0), with per-instance uniforms gathered
    /// from the arena by gl_InstanceIndex (0 .. N-1). Same arena + shader rewrite
    /// as `scene`; the difference is one instanced draw instead of N indirect
    /// sub-draws — i.e. true per-instance heap rendering (instanceCount > 1).
    let instanced (mode : IndexedGeometryMode)
                  (positions : V3f[]) (normals : V3f[]) (index : int[])
                  (effect : Effect)
                  (instances : Map<string, HeapInput>[]) : ISg =
        let names = instances |> Array.collect (fun d -> Map.toArray d |> Array.map fst) |> Array.distinct
        let fieldStride = names.Length
        let nameToField = names |> Array.mapi (fun i n -> n, i) |> Map.ofArray
        let regionOf = System.Collections.Generic.Dictionary<IAdaptiveValue, int>(HashIdentity.Reference)
        let distinct = System.Collections.Generic.List<HeapInput * int>()
        let mutable cursor = 0
        let headers = Array.zeroCreate<int> (instances.Length * fieldStride)
        instances |> Array.iteri (fun ii draw ->
            for KeyValue(name, input) in draw do
                let off =
                    match regionOf.TryGetValue input.key with
                    | true, o -> o
                    | _ -> let o = cursor in cursor <- cursor + input.sizeF; regionOf.[input.key] <- o; distinct.Add(input, o); o
                headers.[ii * fieldStride + nameToField.[name]] <- off)
        let totalF = cursor
        let arena =
            AVal.custom (fun t ->
                let a = Array.zeroCreate<float32> totalF
                for (input, o) in distinct do input.packInto t a o
                a)
        let effect = rewrite (Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.InstanceId)) nameToField fieldStride Map.empty effect
        let n = instances.Length
        // ONE instanced indirect draw: firstInstance 0, instanceCount N. Built
        // declaratively (like `scene`) so ambient Sg.uniform globals merge in.
        let indirect =
            [| DrawCallInfo(FaceVertexCount = index.Length, FirstIndex = 0, BaseVertex = 0, FirstInstance = 0, InstanceCount = n) |]
            |> IndirectBuffer.ofArray
        Sg.indirectDraw mode (AVal.constant indirect)
        |> Sg.vertexBuffer DefaultSemantic.Positions (BufferView(AVal.constant (ArrayBuffer(positions) :> IBuffer), typeof<V3f>))
        |> Sg.vertexBuffer DefaultSemantic.Normals   (BufferView(AVal.constant (ArrayBuffer(normals)   :> IBuffer), typeof<V3f>))
        |> Sg.index' index
        |> Sg.uniform "HeapData"    arena
        |> Sg.uniform "HeapHeaders" (AVal.constant headers)
        |> Sg.effect [ effect ]

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
    // Globals (camera etc.) are delegated
    // to a LIVE member's uniform provider (bucket-homogeneous; re-seated when
    // that member leaves). Membership changes are incremental (per-bucket
    // O(changed) diffs); per-draw value marks flow through the reactive arena
    // with offsets/headers held constant.

    let private packerFor (t : System.Type) : int * (obj -> float32[] -> int -> unit) =
        if   t = typeof<M44f>    then 16, (fun o a off -> packM44 (o :?> M44f) a off)
        elif t = typeof<Trafo3d> then 16, (fun o a off -> packM44 (M44f.op_Explicit (o :?> Trafo3d).Forward) a off)
        elif t = typeof<M44d>    then 16, (fun o a off -> packM44 (M44f.op_Explicit (o :?> M44d)) a off)
        elif t = typeof<V4f>     then 4,  (fun o a off -> let v = o :?> V4f in a.[off]<-v.X; a.[off+1]<-v.Y; a.[off+2]<-v.Z; a.[off+3]<-v.W)
        elif t = typeof<C4f>     then 4,  (fun o a off -> let c = o :?> C4f in a.[off]<-c.R; a.[off+1]<-c.G; a.[off+2]<-c.B; a.[off+3]<-c.A)
        elif t = typeof<V3f>     then 3,  (fun o a off -> let v = o :?> V3f in a.[off]<-v.X; a.[off+1]<-v.Y; a.[off+2]<-v.Z)
        elif t = typeof<V2f>     then 2,  (fun o a off -> let v = o :?> V2f in a.[off]<-v.X; a.[off+1]<-v.Y)
        elif t = typeof<V3d>     then 3,  (fun o a off -> let v = o :?> V3d in a.[off]<-float32 v.X; a.[off+1]<-float32 v.Y; a.[off+2]<-float32 v.Z)
        elif t = typeof<V2d>     then 2,  (fun o a off -> let v = o :?> V2d in a.[off]<-float32 v.X; a.[off+1]<-float32 v.Y)
        elif t = typeof<V4d>     then 4,  (fun o a off -> let v = o :?> V4d in a.[off]<-float32 v.X; a.[off+1]<-float32 v.Y; a.[off+2]<-float32 v.Z; a.[off+3]<-float32 v.W)
        elif t = typeof<M33d>    then 9,  (fun o a off -> let m = o :?> M33d in a.[off]<-float32 m.M00; a.[off+1]<-float32 m.M01; a.[off+2]<-float32 m.M02; a.[off+3]<-float32 m.M10; a.[off+4]<-float32 m.M11; a.[off+5]<-float32 m.M12; a.[off+6]<-float32 m.M20; a.[off+7]<-float32 m.M21; a.[off+8]<-float32 m.M22)
        elif t = typeof<M33f>    then 9,  (fun o a off -> let m = o :?> M33f in a.[off]<-m.M00; a.[off+1]<-m.M01; a.[off+2]<-m.M02; a.[off+3]<-m.M10; a.[off+4]<-m.M11; a.[off+5]<-m.M12; a.[off+6]<-m.M20; a.[off+7]<-m.M21; a.[off+8]<-m.M22)
        elif t = typeof<float32> then 1,  (fun o a off -> a.[off] <- (o :?> float32))
        elif t = typeof<float>   then 1,  (fun o a off -> a.[off] <- float32 (o :?> float))
        elif t = typeof<int>     then 1,  (fun o a off -> a.[off] <- float32 (o :?> int))
        else failwithf "Heap: unsupported per-draw uniform content type %A" t

    /// A uniform the SHADER requests at double precision is stored as REAL doubles:
    /// 2 arena words per scalar (the double's bit pattern), 8-byte aligned so the
    /// native double view (HeapDataD) reads it back exactly — no f32 downcast.
    let private isDoubleUniform (t : System.Type) =
        t = typeof<V2d> || t = typeof<V3d> || t = typeof<V4d> || t = typeof<M33d> || t = typeof<M44d>
    // write one double as 2 consecutive arena words (bit-exact; netstandard2.0 has no
    // Int32BitsToSingle, so reinterpret the 8 bytes as two float32 slots). fp64 path:
    // the GPU's native double view reads these 2 words back as one IEEE double.
    let private wd (a : float32[]) (i : int) (d : float) =
        let b = System.BitConverter.GetBytes d
        a.[i]   <- System.BitConverter.ToSingle(b, 0)
        a.[i+1] <- System.BitConverter.ToSingle(b, 4)
    // df32 path: write the double as a (hi, lo) two-f32 pair — hi = round-to-f32(d),
    // lo = round-to-f32(d − hi) — so the df32 kernels read it as V2f(hi,lo). Same 2
    // words / same 8-byte slot as `wd`; only the CONTENT differs.
    let private wdDf (a : float32[]) (i : int) (d : float) =
        let hi = float32 d
        a.[i]   <- hi
        a.[i+1] <- float32 (d - float hi)
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
    let private doublePackerFor (df32 : bool) (t : System.Type) : int * (obj -> float32[] -> int -> unit) =
        let wd = if df32 then wdDf else wd
        if   t = typeof<V2d>  then 4,  (fun o a off -> let v = asV2d o in wd a off v.X; wd a (off+2) v.Y)
        elif t = typeof<V3d>  then 6,  (fun o a off -> let v = asV3d o in wd a off v.X; wd a (off+2) v.Y; wd a (off+4) v.Z)
        elif t = typeof<V4d>  then 8,  (fun o a off -> let v = asV4d o in wd a off v.X; wd a (off+2) v.Y; wd a (off+4) v.Z; wd a (off+6) v.W)
        elif t = typeof<M33d> then 18, (fun o a off -> let m = asM33d o in wd a off m.M00; wd a (off+2) m.M01; wd a (off+4) m.M02; wd a (off+6) m.M10; wd a (off+8) m.M11; wd a (off+10) m.M12; wd a (off+12) m.M20; wd a (off+14) m.M21; wd a (off+16) m.M22)
        elif t = typeof<M44d> then 32, (fun o a off -> let m = asM44d o in wd a (off+0) m.M00; wd a (off+2) m.M01; wd a (off+4) m.M02; wd a (off+6) m.M03; wd a (off+8) m.M10; wd a (off+10) m.M11; wd a (off+12) m.M12; wd a (off+14) m.M13; wd a (off+16) m.M20; wd a (off+18) m.M21; wd a (off+20) m.M22; wd a (off+22) m.M23; wd a (off+24) m.M30; wd a (off+26) m.M31; wd a (off+28) m.M32; wd a (off+30) m.M33)
        else failwithf "Heap: unsupported double per-draw uniform type %A" t

    /// Size in bytes of a blittable attribute/index element type (-1 if it isn't
    /// blittable — such an RO is then treated as un-heapable and passed through).
    /// Cached per type: Marshal.SizeOf is a marshalling-info lookup and the
    /// eligibility checks call this several times per classified RO.
    let private elemSizeCache = System.Collections.Concurrent.ConcurrentDictionary<System.Type, int>()
    let private elemSize (t : System.Type) =
        elemSizeCache.GetOrAdd(t, fun t -> try System.Runtime.InteropServices.Marshal.SizeOf t with _ -> -1)

    /// Read a host buffer-view's raw bytes (respecting its byte Offset). Works for
    /// ArrayBuffer and any INativeBuffer (incl. a user-supplied NativeMemoryBuffer).
    /// Assumes a tightly-packed view; interleaved/strided and GPU-resident views are
    /// rejected by `isHeapable` so they never reach here. TYPE-AGNOSTIC: bytes only.
    let private readBytesView (bv : BufferView) : byte[] =
        match bv.Buffer.GetValue() with
        | :? INativeBuffer as nb ->
            nb.Use (fun (ptr : nativeint) ->
                let len = int nb.SizeInBytes - bv.Offset
                let arr = Array.zeroCreate<byte> len
                System.Runtime.InteropServices.Marshal.Copy(ptr + nativeint bv.Offset, arr, 0, len)
                arr)
        | b -> failwithf "Heap.ofRenderObjects: expected host (INativeBuffer) geometry, got %A" (b.GetType())

    /// VALUE-level geometry dedup source: CONSTANT buffer avals are forced once
    /// at ingest and ArrayBuffer-backed ones key on the UNDERLYING ARRAY
    /// reference (ArrayBuffer.Equals semantics) — per-leaf fresh BufferView/aval
    /// wrappers around one shared array (exactly what Sg combinators +
    /// Primitives.Box produce) dedup to ONE packed allocation. Non-constant or
    /// non-ArrayBuffer sources keep aval-identity keying (their bytes may differ
    /// per evaluation / per backend handle).
    let private geomDedupSource (bv : BufferView) : obj =
        let b = bv.Buffer
        if b.IsConstant then
            match b.GetValue() with
            | :? ArrayBuffer as ab -> ab.Data :> obj
            | _ -> b :> obj
        else b :> obj

    /// Read a buffer-view's raw bytes whether host (INativeBuffer) or GPU-resident
    /// (IBackendBuffer, downloaded). Used only to COMBINE per-object INDEX buffers
    /// (small); vertex buffers are never downloaded — they're bound for vertex-pull.
    let private readGeomBytes (runtime : IRuntime) (bv : BufferView) : byte[] =
        match bv.Buffer.GetValue() with
        | :? INativeBuffer as nb ->
            nb.Use (fun (ptr : nativeint) ->
                let len = int nb.SizeInBytes - bv.Offset
                let arr = Array.zeroCreate<byte> len
                System.Runtime.InteropServices.Marshal.Copy(ptr + nativeint bv.Offset, arr, 0, len)
                arr)
        | :? IBackendBuffer as gb ->
            let len = int gb.SizeInBytes - bv.Offset
            let arr = Array.zeroCreate<byte> len
            let gc = System.Runtime.InteropServices.GCHandle.Alloc(arr, System.Runtime.InteropServices.GCHandleType.Pinned)
            try runtime.Download(gb, uint64 bv.Offset, gc.AddrOfPinnedObject(), uint64 len)
            finally gc.Free()
            arr
        | b -> failwithf "Heap.ofRenderObjects: index buffer is neither host nor backend buffer (%A)" (b.GetType())

    /// Number of buckets produced by the most recent `ofRenderObjects` evaluation
    /// (diagnostic / for logging).
    let mutable lastBucketCount = 0

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
    type internal RegionWriter(src : IAdaptiveValue, off : int, size : int, pack : obj -> float32[] -> int -> unit) =
        inherit AdaptiveObject()
        do src.Acquire()
        let mutable off = off
        /// the region's arena offset. MUTABLE: arena compaction re-seats live
        /// regions (the writer keeps its subscription; only future packs target
        /// the new offset — the compactor moves the staged bytes itself).
        member _.Off with get () = off and set v = off <- v
        member _.Size = size
        member x.Pack(token : AdaptiveToken, staging : float32[]) =
            x.EvaluateIfNeeded token () (fun token -> pack (src.GetValueUntyped token) staging off)
        member x.Dispose() =
            src.Release()
            src.Outputs.Remove x |> ignore
            x.Outputs.Clear()

    /// Dirty-tracking arena buffer. `InputChangedObject` collects the writers
    /// whose source changed (no per-source marking callbacks); `Compute` packs
    /// only those into a shared staging mirror, COALESCES adjacent dirty
    /// regions into runs, and uploads one sub-range per run. All-dirty -> one
    /// upload (regions are arena-contiguous); sparse -> a few small uploads.
    /// Supports dynamic add/remove of regions and pow2 growth (for HeapScene).
    type internal HeapArena(runtime : IBufferRuntime, initialFloats : int) =
        inherit AdaptiveBuffer(runtime, uint64 (max 1 initialFloats * 4), BufferUsage.Storage, BufferStorage.Host)
        let mutable capacity = max 1 initialFloats
        let mutable staging = Array.zeroCreate<float32> capacity
        let pending = LockedSet<RegionWriter>()
        // a compaction requested a one-shot upload of [0, fullUploadFloats)
        // (handled in Compute alongside the dirty-writer runs)
        let mutable fullUploadFloats = 0
        // one-shot static writes (allocation headers + immutable geometry bytes),
        // staged by the updater's evaluation, uploaded on the next Compute.
        let pendingStatic = System.Collections.Generic.List<struct(int * int)>()
        /// Grow the staging mirror to hold at least n floats. The GPU-side resize is
        /// DEFERRED to the next Compute (ResizeInPlace there — content-preserving),
        /// so this is rule-clean inside adaptive evaluation: no transact/MarkOutdated
        /// happens here. Both call sites guarantee a subsequent re-evaluation: the
        /// incremental updater is itself the arena's ExtraDependency (already
        /// evaluating), and HeapScene.Add runs in a transact and calls Touch().
        member x.EnsureFloats(n : int) =
            if n > capacity then
                let nf = Fun.NextPowerOfTwo n
                let ns = Array.zeroCreate<float32> nf
                System.Array.Copy(staging, ns, capacity)
                staging <- ns
                capacity <- nf
        /// Move `size` floats within the staging mirror (compaction support; the
        /// caller is responsible for re-uploading — see RequestFullUpload).
        member _.MoveStaging(src : int, dst : int, size : int) =
            if src <> dst && size > 0 then System.Array.Copy(staging, src, staging, dst, size)
        /// Shrink the staging mirror (and, deferred to the next Compute, the GPU
        /// buffer) after compaction. Keeps pow2 sizing for amortized regrowth.
        member x.ShrinkFloats(n : int) =
            let nf = max 1024 (Fun.NextPowerOfTwo (max 1 n))
            if nf < capacity then
                let ns = Array.zeroCreate<float32> nf
                System.Array.Copy(staging, ns, nf)
                staging <- ns
                capacity <- nf
        /// Request a one-shot upload of [0, n) floats from staging on the next
        /// Compute (used by compaction after MoveStaging re-seated the regions —
        /// rule-clean: no transact, the arena re-evaluates via ExtraDependency).
        member _.RequestFullUpload(n : int) =
            fullUploadFloats <- max fullUploadFloats n
        /// Write a 4-word per-allocation header (typeId, length, strideBytes, 0)
        /// at word offset `off` (staging only; uploaded on the next Compute).
        member _.WriteHeader(off : int, typeId : int, length : int, strideBytes : int) =
            let inline bits (v : int) = System.BitConverter.ToSingle(System.BitConverter.GetBytes v, 0)
            staging.[off + 0] <- bits typeId
            staging.[off + 1] <- bits length
            staging.[off + 2] <- bits strideBytes
            staging.[off + 3] <- 0.0f
            pendingStatic.Add(struct(off, off + AllocHeaderWords))
        /// Blit immutable bytes (attribute/index data) into staging at word
        /// offset `off`; the covered word range uploads on the next Compute.
        /// A ragged tail word is zero-padded (deterministic content).
        member _.WriteStaticBytes(off : int, src : byte[]) =
            let words = (src.Length + 3) / 4
            if words > 0 then
                if src.Length % 4 <> 0 then staging.[off + words - 1] <- 0.0f
                System.Buffer.BlockCopy(src, 0, staging, off * 4, src.Length)
                pendingStatic.Add(struct(off, off + words))
        /// Stage a ONE-SHOT region write (CONSTANT sources — no RegionWriter
        /// subscription, no per-flush re-evaluation): `pack` writes into the
        /// staging mirror NOW; the covered word range uploads on the next
        /// Compute (same pendingStatic path as headers/static bytes).
        member _.StageOnce(off : int, size : int, pack : float32[] -> unit) =
            pack staging
            pendingStatic.Add(struct(off, off + size))
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
        /// Optional dependency evaluated at the TOP of Compute. The incremental
        /// ofRenderObjects path sets this to its membership updater so that slot /
        /// region mutations (incl. newly added writers in `pending`) are applied
        /// BEFORE the flush below — regardless of which bucket aval the render
        /// task happens to pull first. Reading it through the token also makes
        /// the arena re-flush whenever membership changes (no MarkOutdated /
        /// transact during evaluation needed).
        member val ExtraDependency : IAdaptiveValue option = None with get, set
        override x.Compute(t, rt) =
            match x.ExtraDependency with
            | Some d -> d.GetValueUntyped t |> ignore
            | None -> ()
            // apply any deferred growth (EnsureFloats) or shrink (ShrinkFloats) —
            // content-preserving resize, performed HERE so no transact ever
            // happens during evaluation.
            if uint64 capacity * 4UL <> x.Size then
                x.ResizeInPlace(uint64 capacity * 4UL)
            let dirty = pending.GetAndClear()
            let full = fullUploadFloats
            fullUploadFloats <- 0
            if dirty.Count > 0 || full > 0 || pendingStatic.Count > 0 then
                let ranges = System.Collections.Generic.List<struct(int * int)>(dirty.Count + pendingStatic.Count + 1)
                for w in dirty do
                    w.Pack(t, staging)
                    ranges.Add(struct(w.Off, w.Off + w.Size))
                ranges.AddRange pendingStatic
                pendingStatic.Clear()
                if full > 0 then ranges.Add(struct(0, full))
                ranges.Sort(fun (struct(a, _)) (struct(b, _)) -> compare a b)
                // clamp to the (possibly shrunk) staging capacity: a compaction in
                // the same pass may have re-seated content below stale static ranges
                // (the full upload it requested covers the moved bytes).
                let flush lo hi =
                    let lo = min lo capacity
                    let hi = min hi capacity
                    if hi > lo then x.Write(staging, uint64 (lo * 4), lo, hi - lo, false)
                let mutable lo = let (struct(l, _)) = ranges.[0] in l
                let mutable hi = let (struct(_, h)) = ranges.[0] in h
                for i in 1 .. ranges.Count - 1 do
                    let (struct(o, e)) = ranges.[i]
                    if o <= hi then hi <- max hi e          // contiguous / overlapping -> extend run
                    else flush lo hi; lo <- o; hi <- e      // gap -> emit run, start new
                flush lo hi
            base.Compute(t, rt)
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
        /// evaluate the gate; `write slot count` runs iff re-evaluation was needed
        member x.Update(token : AdaptiveToken, write : int -> int -> unit) =
            x.EvaluateIfNeeded token () (fun token ->
                write slot (if src.GetValue token then instances else 0))
        member x.Dispose() =
            disposed <- true
            (src :> IAdaptiveValue).Release()
            (src :> IAdaptiveValue).Outputs.Remove x |> ignore
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
        inherit AdaptiveBuffer(runtime, uint64 (max 1 initialBytes), usage, BufferStorage.Host)
        let dirtyGates = LockedSet<GateWriter>()
        member val Dependency : IAdaptiveValue option = None with get, set
        member val Flush : AdaptiveToken -> System.Collections.Generic.HashSet<GateWriter> -> unit = (fun _ _ -> ()) with get, set
        /// register a NEW gate writer for evaluation on the next flush
        member _.MarkGate(w : GateWriter) = dirtyGates.Add w |> ignore
        override x.Compute(t, rt) =
            match x.Dependency with
            | Some d -> d.GetValueUntyped t |> ignore
            | None -> ()
            x.Flush t (dirtyGates.GetAndClear())
            base.Compute(t, rt)
        override x.InputChangedObject(_, o) =
            match o with
            | :? GateWriter as w -> dirtyGates.Add w |> ignore
            | _ -> ()

    /// Logical address space for the heap reclamation sites (units are caller-
    /// defined: floats, vertices, indices, instance slots) — packed geometry
    /// vertex + index ranges, arena uniform regions, per-instance slot-attribute
    /// ranges. The allocation policy is Aardvark.Rendering's generic
    /// `Management.MemoryManager` (size-sorted SortedSetExt free list: O(log n)
    /// best-fit with split, both-neighbor coalescing on Free) instantiated over
    /// a VIRTUAL memory (`Memory.nop`, 'a = unit): no real bytes are managed —
    /// the actual storage lives in the arena staging mirrors, which the
    /// call sites grow to `Extent`. Callers hold the returned `Block<unit>` per
    /// allocation and pass it back to `Free`, so the manager coalesces properly.
    /// This wrapper only adds the two counters the compaction trigger and the
    /// buffer sizing need and which the manager does not expose: `Live` (units
    /// in live allocations) and `Extent` (high-water end of the allocated
    /// space; retracts when the tail allocation is freed, so it tracks the
    /// tight cursor, not the manager's pow2 capacity). `Reset` (compaction)
    /// swaps in a fresh manager; the compactors then re-alloc the live entries
    /// tightly in ascending old-offset order.
    type internal HeapSpace() =
        static let mkManager () = new Management.MemoryManager<unit>(Management.Memory.nop, 16n)
        let mutable mm = mkManager ()
        let mutable live = 0
        let mutable extent = 0
        /// high-water end of the allocated address space (in units)
        member _.Extent = extent
        /// units referenced by live allocations
        member _.Live = live
        /// reclaimable units below Extent (the waste)
        member _.Waste = extent - live
        member _.Alloc(size : int) : Management.Block<unit> =
            let b = mm.Alloc(nativeint size)
            live <- live + size
            extent <- max extent (int b.Offset + size)
            b
        member _.Free(b : Management.Block<unit>) =
            if not (isNull b) && not b.IsFree && b.Size > 0n then
                live <- live - int b.Size
                // a block freed at the very END of the space retracts the extent
                // to the start of the resulting free tail (free blocks are never
                // adjacent, so the chain after a tail block is at most one free
                // block before null).
                let newExtent =
                    if isNull b.Next || (b.Next.IsFree && isNull b.Next.Next) then
                        if not (isNull b.Prev) && b.Prev.IsFree then int b.Prev.Offset else int b.Offset
                    else extent
                mm.Free b
                extent <- newExtent
        /// drop everything and start a fresh address space (used by compaction,
        /// which re-allocs the live entries tightly right afterwards)
        member _.Reset() =
            mm.Dispose()
            mm <- mkManager ()
            live <- 0
            extent <- 0

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
          mutable Block : Management.Block<unit>; HeaderWords : int }

    /// Refcounted STATIC allocation in the bucket arena (one vertex attribute's
    /// bytes, or one index range — written once, deduped by VALUE-level source
    /// + byte offset + format typeId). Ref is the allocation's HEADER word offset
    /// (data at Ref + AllocHeaderWords); re-seated by arena compaction. Count
    /// is the element count (the per-slot draw record's FaceVertexCount for
    /// index allocations).
    type internal StaticEntry =
        { mutable Ref : int; SizeF : int; Count : int; mutable RefCount : int
          mutable Block : Management.Block<unit> }

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
    type internal HeapSlot =
        { Slot : int; RegionKeys : IAdaptiveValue[]; Active : aval<bool>
          /// which storage page this slot's group lives on (all its regions are on it);
          /// the slot renders in that page's sub-draw and frees from that page on remove.
          mutable Page : int
          Instances : int; mutable InstOffset : int
          mutable InstBlock : Management.Block<unit>
          /// per consumed attribute (host buckets; empty for bindless)
          AttrKeys : AttrKey[]
          /// the slot's index allocation key (value-level source, byte offset,
          /// index typeId)
          IdxKey : struct(obj * int * int)
          /// derived-uniform compute bookkeeping: uploaded constituent regions to
          /// release (base aval + inverse flag), per-slot output region blocks and
          /// chain-folded Model constituent blocks to free on remove.
          ConstKeys : struct(IAdaptiveValue * bool)[]
          OutBlocks : Management.Block<unit>[]
          FoldBlocks : Management.Block<unit>[] }

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

    /// an input RO may ALREADY be instanced (instanceCount > 1); preserved per-slot.
    /// STRUCTURAL read (forced once, cached in RoFacts): an RO whose Direct call list
    /// changes its instance count later is NOT re-bucketed — the heap treats draw-call
    /// shape as immutable, like the geometry layout.
    let private instanceCountOf (ro : RenderObject) =
        match ro.DrawCalls with
        | DrawCalls.Direct calls ->
            match AVal.force calls with
            | [||] -> 1
            | arr -> max 1 arr.[0].InstanceCount
        | _ -> 1

    /// a NON-indexed slot's vertex count: the RO's Direct draw call (classify
    /// requires Direct, single-call, zero offsets for non-indexed ROs).
    /// STRUCTURAL read like instanceCountOf — forced once at add, never
    /// re-read (the heap treats draw-call shape as immutable).
    let private faceVertexCountOf (ro : RenderObject) =
        match ro.DrawCalls with
        | DrawCalls.Direct calls ->
            match AVal.force calls with
            | [||] -> 0
            | arr -> arr.[0].FaceVertexCount
        | _ -> 0

    // ── bindless vertex-pull helpers (shared by the incremental buckets and the
    //    standalone Heap.bindless) ─────────────────────────────────────────────
    let private vidExpr    : Expr = Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.VertexId)
    // Per-draw handle via gl_InstanceIndex (per-draw FirstInstance) — gl_DrawID does
    // not vary across aardvark's Vulkan indirect multidraw and MoltenVK lacks it.
    let private handleExpr : Expr = Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.InstanceId)

    /// component count of a vertex-attribute type — decode exactly what's there
    let private componentsOf (t : System.Type) : int =
        if   t = typeof<float32> || t = typeof<int> then 1
        elif t = typeof<V2f> || t = typeof<V2i> then 2
        elif t = typeof<V3f> || t = typeof<V3i> then 3
        elif t = typeof<V4f> || t = typeof<V4i> then 4
        else failwithf "Heap: unsupported attribute type %A (expected float32/V2f/V3f/V4f or int/V2i/V3i/V4i)" t


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
        else ValueNone

    /// decode the vertex index for `gl_VertexIndex = v` from the index
    /// allocation whose HEADER lives at arena word offset `r`: u16 elements
    /// (typeId 2) unpack two-per-word, anything else reads a whole word.
    /// NON-indexed slots carry the sentinel ref -1 (no index allocation):
    /// the vertex index passes through unchanged. The branch is coherent per
    /// draw — all vertices of a slot read the same header cell.
    [<ReflectedDefinition>]
    let private decodeHeapIndex (r : int) (v : int) : int =
        if r < 0 then v
        elif uniform.HeapDataI.[r] = 2 then
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
        if e >= 255 then Fun.FloatFromBits(s ||| 0x7F800000)
        elif e <= 0 then 0.0f
        else
            let m = ((hi &&& 0xFFFFF) <<< 3) ||| ((lo >>> 29) &&& 0x7)
            Fun.FloatFromBits((s ||| (e <<< 23) ||| m) + ((lo >>> 28) &&& 1))

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
    let private decodeHeapV4f (r : int) (v : int) : V4f =
        let tid = uniform.HeapDataI.[r]
        let e = v % uniform.HeapDataI.[r + 1]
        if tid = 13 then                                            // f32 x3 (V3f/C3f)
            let o = r + 4 + e * 3
            V4f(uniform.HeapData.[o], uniform.HeapData.[o + 1], uniform.HeapData.[o + 2], 1.0f)
        elif tid = 14 then                                          // f32 x4 (V4f/C4f)
            let o = r + 4 + e * 4
            V4f(uniform.HeapData.[o], uniform.HeapData.[o + 1], uniform.HeapData.[o + 2], uniform.HeapData.[o + 3])
        elif tid = 40 then                                          // normalized C4b (BGRA memory layout)
            let w = uniform.HeapDataI.[r + 4 + e]
            V4f(float32 ((w >>> 16) &&& 0xFF), float32 ((w >>> 8) &&& 0xFF), float32 (w &&& 0xFF), float32 ((w >>> 24) &&& 0xFF)) / 255.0f
        elif tid = 12 then                                          // f32 x2
            let o = r + 4 + e * 2
            V4f(uniform.HeapData.[o], uniform.HeapData.[o + 1], 0.0f, 1.0f)
        elif tid = 11 then                                          // f32 x1
            V4f(uniform.HeapData.[r + 4 + e], 0.0f, 0.0f, 1.0f)
        elif tid = 33 then                                          // f64 x3 (V3d/C3d)
            let o = r + 4 + e * 6
            V4f(decodeHeapF64 o, decodeHeapF64 (o + 2), decodeHeapF64 (o + 4), 1.0f)
        elif tid = 34 then                                          // f64 x4 (V4d/C4d)
            let o = r + 4 + e * 8
            V4f(decodeHeapF64 o, decodeHeapF64 (o + 2), decodeHeapF64 (o + 4), decodeHeapF64 (o + 6))
        elif tid = 32 then                                          // f64 x2
            let o = r + 4 + e * 4
            V4f(decodeHeapF64 o, decodeHeapF64 (o + 2), 0.0f, 1.0f)
        elif tid = 31 then                                          // f64 x1
            V4f(decodeHeapF64 (r + 4 + e * 2), 0.0f, 0.0f, 1.0f)
        elif tid = 23 then                                          // i32 x3 -> float cast
            let o = r + 4 + e * 3
            V4f(float32 uniform.HeapDataI.[o], float32 uniform.HeapDataI.[o + 1], float32 uniform.HeapDataI.[o + 2], 1.0f)
        elif tid = 24 then                                          // i32 x4 -> float cast
            let o = r + 4 + e * 4
            V4f(float32 uniform.HeapDataI.[o], float32 uniform.HeapDataI.[o + 1], float32 uniform.HeapDataI.[o + 2], float32 uniform.HeapDataI.[o + 3])
        elif tid = 22 then                                          // i32 x2 -> float cast
            let o = r + 4 + e * 2
            V4f(float32 uniform.HeapDataI.[o], float32 uniform.HeapDataI.[o + 1], 0.0f, 1.0f)
        elif tid = 21 then                                          // i32 x1 -> float cast
            V4f(float32 uniform.HeapDataI.[r + 4 + e], 0.0f, 0.0f, 1.0f)
        else V4f(0.0f, 0.0f, 0.0f, 1.0f)

    /// int-target twin of decodeHeapV4f: i32 sources pass through, f32/f64
    /// sources truncate (well-defined casts), C4b unpacks to raw 0..255 ints.
    [<ReflectedDefinition>]
    let private decodeHeapV4i (r : int) (v : int) : V4i =
        let tid = uniform.HeapDataI.[r]
        let e = v % uniform.HeapDataI.[r + 1]
        if tid = 23 then                                            // i32 x3
            let o = r + 4 + e * 3
            V4i(uniform.HeapDataI.[o], uniform.HeapDataI.[o + 1], uniform.HeapDataI.[o + 2], 1)
        elif tid = 24 then                                          // i32 x4
            let o = r + 4 + e * 4
            V4i(uniform.HeapDataI.[o], uniform.HeapDataI.[o + 1], uniform.HeapDataI.[o + 2], uniform.HeapDataI.[o + 3])
        elif tid = 22 then                                          // i32 x2
            let o = r + 4 + e * 2
            V4i(uniform.HeapDataI.[o], uniform.HeapDataI.[o + 1], 0, 1)
        elif tid = 21 then                                          // i32 x1
            V4i(uniform.HeapDataI.[r + 4 + e], 0, 0, 1)
        elif tid = 13 then                                          // f32 x3 -> int cast
            let o = r + 4 + e * 3
            V4i(int uniform.HeapData.[o], int uniform.HeapData.[o + 1], int uniform.HeapData.[o + 2], 1)
        elif tid = 14 then                                          // f32 x4 -> int cast
            let o = r + 4 + e * 4
            V4i(int uniform.HeapData.[o], int uniform.HeapData.[o + 1], int uniform.HeapData.[o + 2], int uniform.HeapData.[o + 3])
        elif tid = 12 then                                          // f32 x2 -> int cast
            let o = r + 4 + e * 2
            V4i(int uniform.HeapData.[o], int uniform.HeapData.[o + 1], 0, 1)
        elif tid = 11 then                                          // f32 x1 -> int cast
            V4i(int uniform.HeapData.[r + 4 + e], 0, 0, 1)
        elif tid = 40 then                                          // C4b (BGRA memory layout) -> raw 0..255
            let w = uniform.HeapDataI.[r + 4 + e]
            V4i((w >>> 16) &&& 0xFF, (w >>> 8) &&& 0xFF, w &&& 0xFF, (w >>> 24) &&& 0xFF)
        elif tid = 33 then                                          // f64 x3 -> int cast
            let o = r + 4 + e * 6
            V4i(int (decodeHeapF64 o), int (decodeHeapF64 (o + 2)), int (decodeHeapF64 (o + 4)), 1)
        elif tid = 34 then                                          // f64 x4 -> int cast
            let o = r + 4 + e * 8
            V4i(int (decodeHeapF64 o), int (decodeHeapF64 (o + 2)), int (decodeHeapF64 (o + 4)), int (decodeHeapF64 (o + 6)))
        elif tid = 32 then                                          // f64 x2 -> int cast
            let o = r + 4 + e * 4
            V4i(int (decodeHeapF64 o), int (decodeHeapF64 (o + 2)), 0, 1)
        elif tid = 31 then                                          // f64 x1 -> int cast
            V4i(int (decodeHeapF64 (r + 4 + e * 2)), 0, 0, 1)
        else V4i(0, 0, 0, 1)

    /// per-input attribute gather: ONE call into the typeId-branching decoder,
    /// swizzled down to the shader's input type (the conversion handles widen /
    /// narrow / normalize / casts per SOURCE typeId at fetch time — the input
    /// type is fixed per effect, the source type varies per allocation).
    /// Returns None for unsupported shader input types.
    let private hostGather (inputT : System.Type) (refE : Expr<int>) (vidE : Expr<int>) : Expr option =
        let inline f1 (q : Expr<'a>) = Some q.Raw
        if   inputT = typeof<V4f>     then f1 <@ decodeHeapV4f %refE %vidE @>
        elif inputT = typeof<V3f>     then f1 <@ (decodeHeapV4f %refE %vidE).XYZ @>
        elif inputT = typeof<V2f>     then f1 <@ (decodeHeapV4f %refE %vidE).XY @>
        elif inputT = typeof<float32> then f1 <@ (decodeHeapV4f %refE %vidE).X @>
        elif inputT = typeof<V4i>     then f1 <@ decodeHeapV4i %refE %vidE @>
        elif inputT = typeof<V3i>     then f1 <@ (decodeHeapV4i %refE %vidE).XYZ @>
        elif inputT = typeof<V2i>     then f1 <@ (decodeHeapV4i %refE %vidE).XY @>
        elif inputT = typeof<int>     then f1 <@ (decodeHeapV4i %refE %vidE).X @>
        else None

    /// supported shader INPUT types of the storage decode (the decoder pair
    /// above covers every (source typeId, target) combination)
    let private hostTargetTypes =
        System.Collections.Generic.HashSet<System.Type>(
            [ typeof<float32>; typeof<V2f>; typeof<V3f>; typeof<V4f>
              typeof<int>; typeof<V2i>; typeof<V3i>; typeof<V4i> ])

    /// can host element type `hostT` be storage-decoded into shader input type
    /// `inputT`? Decoding branches per allocation, so the answer FACTORS: the
    /// SOURCE needs a typeId, the TARGET a decoder — no pair table needed.
    let private hostDecodable (hostT : System.Type) (inputT : System.Type) =
        (attrTypeId hostT).IsSome && hostTargetTypes.Contains inputT

    /// generic native-layout packer for singleton-attribute values: blits the
    /// boxed struct's bytes (same layout as a 1-element array of it) into the
    /// arena staging at the region's float offset.
    let private attrPackerFor (t : System.Type) : int * (obj -> float32[] -> int -> unit) =
        let es = elemSize t
        if es <= 0 then failwithf "Heap: singleton attribute type %A is not blittable" t
        let szF = (es + 3) / 4
        szF, fun (o : obj) (a : float32[]) (off : int) ->
            let h = System.Runtime.InteropServices.GCHandle.Alloc(o, System.Runtime.InteropServices.GCHandleType.Pinned)
            try
                let tmp = Array.zeroCreate<byte> (szF * 4)
                System.Runtime.InteropServices.Marshal.Copy(h.AddrOfPinnedObject(), tmp, 0, es)
                System.Buffer.BlockCopy(tmp, 0, a, off * 4, szF * 4)
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

    /// Persistent dedup table of one bucket's distinct textures for ONE bindless
    /// sampler type. Value = (HeapTextures&lt;T&gt;, HeapTexIndices&lt;T&gt;): the distinct-
    /// texture array (refcounted by texture identity; freed indices are reused and
    /// their cells parked on a 1×1 dummy so the descriptor array never references
    /// a dead texture) and the per-(slot, sampler) indices at slot*K + kt, growing
    /// with the slot table. Recomputed when membership changed (read through
    /// `updater`, whose evaluation calls Add/RemoveSlot first) or when a member's
    /// texture aval changed (its writer marks this table) — O(changed) either way.
    type internal BindlessTexTable(updater : aval<int>, k : int, mkDummy : unit -> ITexture, delDummy : ITexture -> unit) =
        inherit AVal.AbstractVal<ITexture[] * int[]>()
        let kk = max 1 k
        let pending = LockedSet<SlotTexWriter>()
        let texArr = System.Collections.Generic.List<ITexture>()
        let refCounts = System.Collections.Generic.List<int>()
        let idxOf = System.Collections.Generic.Dictionary<ITexture, int>(HashIdentity.Reference)
        let freeIdx = System.Collections.Generic.Stack<int>()
        let mutable dummy : ITexture = null
        let getDummy () =
            if isNull dummy then dummy <- mkDummy ()
            dummy
        let acquire (tex : ITexture) : int =
            match idxOf.TryGetValue tex with
            | true, i -> refCounts.[i] <- refCounts.[i] + 1; i
            | _ ->
                let i =
                    if freeIdx.Count > 0 then freeIdx.Pop()
                    else
                        texArr.Add tex
                        refCounts.Add 0
                        texArr.Count - 1
                texArr.[i] <- tex
                refCounts.[i] <- 1
                idxOf.[tex] <- i
                i
        let release (tex : ITexture) =
            if not (isNull tex) then
                match idxOf.TryGetValue tex with
                | true, i ->
                    refCounts.[i] <- refCounts.[i] - 1
                    if refCounts.[i] = 0 then
                        idxOf.Remove tex |> ignore
                        texArr.[i] <- getDummy ()       // keep the array cell on a LIVE texture
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
        override x.Compute(t) =
            updater.GetValue t |> ignore        // apply membership mutations FIRST
            for w in pending.GetAndClear() do
                if not w.IsDisposed then
                    w.Update(t, fun old tex ->
                        release old
                        indices.[w.Pos] <- acquire tex)
            let texs = if texArr.Count = 0 then [| getDummy () |] else texArr.ToArray()
            texs, Array.sub indices 0 (max 1 highPos)
        member x.Dispose() =
            for i in 0 .. writers.Length - 1 do
                let w = writers.[i]
                if not (System.Object.ReferenceEquals(w, null)) then
                    w.Dispose()
                    writers.[i] <- Unchecked.defaultof<_>
            texArr.Clear(); refCounts.Clear(); idxOf.Clear(); freeIdx.Clear()
            if not (isNull dummy) then
                delDummy dummy
                dummy <- null

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

    /// Dirty-tracked fp64 buffer of DISTINCT trafo links (M44d, one 128-byte slot
    /// each). A link aval shared by N chains is ONE slot here, so changing it marks
    /// ONE writer => one slot re-packed + one sub-range upload, regardless of N —
    /// this is what kills the CPU fan-out. Coalesces adjacent dirty slots into runs.
    type internal LinkArena(runtime : IBufferRuntime, distinct : aval<Trafo3d>[]) =
        inherit AdaptiveBuffer(runtime, uint64 (max 1 distinct.Length * 128), BufferUsage.Storage, BufferStorage.Host)
        let staging = Array.zeroCreate<M44d> (max 1 distinct.Length)
        let writers = distinct |> Array.mapi (fun i a -> LinkWriter(a, i))
        let pending = LockedSet<LinkWriter>()
        do for w in writers do pending.Add w |> ignore        // all dirty initially
        override x.Compute(t, rt) =
            let dirty = pending.GetAndClear()
            if dirty.Count > 0 then
                lastChainLinkUploads <- dirty.Count
                let slots = System.Collections.Generic.List<int>(dirty.Count)
                for w in dirty do w.Pack(t, staging); slots.Add w.Slot
                slots.Sort()
                let flush lo hi = x.Write(staging, uint64 (lo * 128), lo, hi - lo + 1, false)
                let mutable lo = slots.[0]
                let mutable hi = slots.[0]
                for i in 1 .. slots.Count - 1 do
                    let s = slots.[i]
                    if s <= hi + 1 then hi <- s             // contiguous -> extend run
                    else flush lo hi; lo <- s; hi <- s      // gap -> emit run, start new
                flush lo hi
            base.Compute(t, rt)
        override x.InputChangedObject(_, o) =
            match o with
            | :? LinkWriter as w -> pending.Add w |> ignore
            | _ -> ()
        member x.Dispose() = for w in writers do w.Dispose()

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
        inherit AdaptiveBuffer(runtime, 256UL, BufferUsage.Storage, BufferStorage.Host)
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
        // one thread per object: compose its chain's links in fp64, then
        // outp[2i]=Proj·View·Model, outp[2i+1]=(Model^-1)^T. Chain links are
        // gathered indirectly through linkIdx so distinct links dedup to one slot.
        [<LocalSize(X = 64)>]
        let composeMvpNm (n : int) (view : M44d) (proj : M44d)
                         (chainOffset : int[]) (chainLen : int[]) (linkIdx : int[])
                         (links : M44d[]) (outp : M44f[]) =
            compute {
                let i = getGlobalId().X
                if i < n then
                    let off = chainOffset.[i]
                    let len = chainLen.[i]
                    // chain links are [L0; …; Ln-1] in compose order (Trafo3d `*`);
                    // (L0·…·Ln-1).Forward = Ln-1.F · … · L0.F, so multiply REVERSED.
                    let mutable m = links.[linkIdx.[off + len - 1]]
                    for k in 1 .. len - 1 do
                        m <- m * links.[linkIdx.[off + len - 1 - k]]
                    outp.[2*i]     <- M44f(proj * view * m)
                    outp.[2*i + 1] <- M44f(m.Inverse.Transposed)
            }

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
                if i < n then
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
                if i < n then
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
        let composeDerived (n : int) (nRec : int) (hstride : int) (records : int[]) =
            compute {
                let slot = getGlobalId().X
                if slot < n && uniform.HeapSlotPage.[slot] = uniform.HeapPageId then
                    let hb = slot * hstride
                    for r in 0 .. nRec - 1 do
                        let rb = r * REC_STRIDE
                        let outOff = uniform.HeapHeaders.[hb + records.[rb + 1]]
                        let a = ldM44 (uniform.HeapHeaders.[hb + records.[rb + 2]])
                        match records.[rb] with
                        | 1 -> HeapWrite.m44 outOff (M44f(a))
                        | 2 -> let b = ldM44 (uniform.HeapHeaders.[hb + records.[rb + 3]])
                               HeapWrite.m44 outOff (M44f(a * b))
                        | 3 -> let b = ldM44 (uniform.HeapHeaders.[hb + records.[rb + 3]])
                               let c = ldM44 (uniform.HeapHeaders.[hb + records.[rb + 4]])
                               HeapWrite.m44 outOff (M44f(a * b * c))
                        | _ ->
                            // NormalMatrix = transpose(Model_backward) upper-3x3.
                            let t = a.Transposed
                            HeapWrite.m33 outOff
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
        let composeDerivedDf32 (n : int) (nRec : int) (hstride : int) (records : int[]) =
            compute {
                let slot = getGlobalId().X
                if slot < n && uniform.HeapSlotPage.[slot] = uniform.HeapPageId then
                    let hb = slot * hstride
                    for r in 0 .. nRec - 1 do
                        let rb = r * REC_STRIDE
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
                                uniform.HeapData.[outOff + k] <- Df32.collapse (Df32.ldEntry offA k)
                        | 2 ->
                            let offB = uniform.HeapHeaders.[hb + records.[rb + 3]]
                            for rr in 0 .. 3 do
                                for c in 0 .. 3 do
                                    let mutable acc = V2f(0.0f, 0.0f)
                                    for t in 0 .. 3 do
                                        acc <- Df32.add acc (Df32.mul (Df32.ldEntry offA (rr * 4 + t)) (Df32.ldEntry offB (t * 4 + c)))
                                    uniform.HeapData.[outOff + rr * 4 + c] <- Df32.collapse acc
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
                                    uniform.HeapData.[outOff + rr * 4 + c] <- Df32.collapse acc
                        | _ ->
                            // NormalMatrix = transpose(A) upper-3x3.  out[i*3+j] = A[j,i].
                            for i in 0 .. 2 do
                                for j in 0 .. 2 do
                                    uniform.HeapData.[outOff + i * 3 + j] <- Df32.collapse (Df32.ldEntry offA (j * 4 + i))
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
    type internal PageArena(runtime : IRuntime) =
        let arena = HeapArena(runtime, 1024)
        let arenaAlloc = HeapSpace()
        let regions = System.Collections.Generic.Dictionary<IAdaptiveValue, RegionEntry>(HashIdentity.Reference)
        let singleRegions = System.Collections.Generic.Dictionary<IAdaptiveValue, RegionEntry>(HashIdentity.Reference)
        let constituentsF = System.Collections.Generic.Dictionary<IAdaptiveValue, RegionEntry>(HashIdentity.Reference)
        let constituentsB = System.Collections.Generic.Dictionary<IAdaptiveValue, RegionEntry>(HashIdentity.Reference)
        // geometry static-attribute + index dedup (by value-level source identity, byte offset,
        // typeId) — MUST be per-page: a shared mesh's attrs live in THIS page's arena, so a slot on
        // another page can't reference them (it binds its own arena). Cross-page = duplicated.
        let geomKeyComparer =
            { new System.Collections.Generic.IEqualityComparer<struct(obj * int * int)> with
                member _.GetHashCode(struct(o, i, t)) =
                    System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode o ^^^ (i * 0x9E3779B1) ^^^ (t * 0x85EBCA6B)
                member _.Equals(struct(a, ai, at), struct(b, bi, bt)) =
                    System.Object.ReferenceEquals(a, b) && ai = bi && at = bt }
        let attrStatic = System.Collections.Generic.Dictionary<struct(obj * int * int), StaticEntry>(geomKeyComparer)
        let idxStatic  = System.Collections.Generic.Dictionary<struct(obj * int * int), StaticEntry>(geomKeyComparer)
        member _.Arena = arena
        member _.ArenaAlloc = arenaAlloc
        member _.Regions = regions
        member _.SingleRegions = singleRegions
        member _.ConstituentsF = constituentsF
        member _.ConstituentsB = constituentsB
        member _.AttrStatic = attrStatic
        member _.IdxStatic = idxStatic

    /// Shader-AGNOSTIC, PAGED, (later) shareable data store: ≤ a handful of `PageArena`s, each
    /// a ≤ pageWords storage buffer. A slot is placed wholly on one page; when the current fill
    /// page would exceed pageWords the store rolls to a new page. `pageWords` (default 2²⁸ = 1 GiB,
    /// keeps off*4 int32-safe + the staging <2 GB + the SSBO <4 GB) is lowerable via
    /// HEAP_PAGE_WORDS for testing the multi-page path on small scenes.
    type internal HeapStorage(runtime : IRuntime) =
        let pageWords =
            match System.Environment.GetEnvironmentVariable "HEAP_PAGE_WORDS" with
            | null | "" -> 1 <<< 28
            | s -> match System.Int32.TryParse s with
                   | true, v when v >= 1024 -> v
                   | _ -> 1 <<< 28
        let pages = System.Collections.Generic.List<PageArena>()
        do pages.Add(PageArena(runtime))
        member _.PageWords = pageWords
        member _.Pages = pages
        member _.Count = pages.Count
        member _.Page(i : int) = pages.[i]
        /// index of the current fill page (the last one)
        member _.CurrentPage = pages.Count - 1
        /// the page index a slot needing ~`words` MORE words should use: the current fill page,
        /// or a fresh page if adding `words` would push it past pageWords. (≥1 page always.)
        member _.PlacePage(words : int) : int =
            let cur = pages.Count - 1
            if pages.[cur].ArenaAlloc.Extent + (max 0 words) > pageWords && pages.[cur].ArenaAlloc.Extent > 0 then
                pages.Add(PageArena(runtime)); pages.Count - 1
            else cur

    type internal IncrementalBucket(runtime : IRuntime, storage : HeapStorage, names : string[], nameToField : Map<string, int>,
                                    effect : Effect, ro0 : RenderObject, updater : aval<int>,
                                    useBindlessGeom : bool, instanced : bool,
                                    // the bucket KEY's resolved pipeline-state values
                                    // (cull, frontFacing, fill, blend, depthTest, depthWrite)
                                    pipeKey : CullMode * WindingOrder * FillMode * BlendMode * DepthTest * bool,
                                    // GPU trafo-chain mode: the members expose a "ModelTrafoStack"
                                    // uniform (the UNFOLDED root->leaf link array). Each slot's
                                    // ModelTrafo is composed on the GPU from a GROWABLE, deduped
                                    // link arena (constants by value, dynamics by identity) and the
                                    // ModelTrafo gather reads the per-slot chainOut buffer instead of
                                    // an arena region. "ModelTrafo" is NOT in `names` in this mode.
                                    chainMode : bool) =
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
            (match ro0.Uniforms.TryGetUniform(Ag.Scope.Root, Symbol.Create b) with ValueSome v -> isTrafoSupply v.ContentType | _ -> false)
            || (b = Derived.MBASE &&
                (match ro0.Uniforms.TryGetUniform(Ag.Scope.Root, Symbol.Create "ModelTrafoStack") with ValueSome _ -> true | _ -> false))
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
        let symDataI = Symbol.Create "HeapDataI"
        let symDataD = Symbol.Create "HeapDataD"   // native double view of the arena (fp64-requested uniforms)
        let symHeaders = Symbol.Create "HeapHeaders"
        let symModelChain = Symbol.Create "HeapModelChain"
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
        let samplersByType =
            samplers
            |> Array.groupBy (fun (_, _, ty, _) -> ty)
            |> Array.map (fun (ty, grp) -> ty, grp |> Array.mapi (fun kt (sn, tn, _, st) -> sn, tn, kt, st))
        // samplerName -> (type, kt, Kt-for-that-type), for the read rewrite
        let samplerByName =
            samplersByType
            |> Array.collect (fun (ty, grp) -> let kCountT = grp.Length in grp |> Array.map (fun (sn, _, kt, _) -> sn, (ty, kt, kCountT)))
            |> Map.ofArray
        // FShade sampler-array KEY -> state (one per type; isHeapable enforces same
        // state per type). Keyed by the FShade uniform key (the module value name).
        let samplerStateOverrides =
            samplersByType
            |> Array.choose (fun (ty, grp) ->
                match bindlessTypeInfo ty with
                | Some (_, _, fkey) when grp.Length > 0 -> let (_, _, _, st) = grp.[0] in Some (fkey, st)
                | _ -> None)
            |> Map.ofArray
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
        let symSlotAttr = Symbol.Create "HeapSlotAttr"
        let slotE : Expr<int> =
            if useDrawId then <@ getDrawId() @>
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
        let headerStride = attrBase + attrCells + 1
        let idxCell = attrBase + attrCells

        // ── arena: deduped per-draw uniform regions, refcounted, placed by a
        //    coalescing range allocator (float units) — now held by the (per-bucket) storage ──
        let mutable arena = storage.Page(0).Arena
        do arena.ExtraDependency <- Some (updater :> IAdaptiveValue)
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

        let allocInst (slot : int) (k : int) : Management.Block<unit> =
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
        let freeInst (b : Management.Block<unit>) =
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
        let chainBlocks = if chainMode then System.Collections.Generic.Dictionary<int, Management.Block<unit>>() else null
        let chainDirtyStruct = if chainMode then System.Collections.Generic.HashSet<int>() else null
        let mutable chainStructAllDirty = false

        // ── draw records + headers: slot-indexed, growable, free-listed ──
        let mutable entries : DrawCallInfo[] = Array.zeroCreate 16
        let mutable headers : int[] = Array.zeroCreate (16 * headerStride)
        // PAGED: which storage page each slot's group lives on (parallel to `entries`); a
        // page's sub-draw renders only its slots (others get a 0-instance record).
        let mutable slotPage : int[] = Array.zeroCreate 16
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

        // ── texture tables (one per bindless sampler TYPE, or one atlas) ──
        let mkDummy2d () = runtime.CreateTexture2D(V2i.II, TextureFormat.Rgba8, levels = 1, samples = 1) :> ITexture
        let mkDummyCube () = runtime.CreateTextureCube(1, TextureFormat.Rgba8, levels = 1) :> ITexture
        let delDummy (t : ITexture) = match t with | :? IBackendTexture as bt -> runtime.DeleteTexture bt | _ -> ()
        // (arrayName, idxName, per-kt texture symbols, table) per sampler TYPE
        let bindlessTexTables =
            if useAtlas then [||]
            else
                samplersByType |> Array.choose (fun (ty, grp) ->
                    match bindlessTypeInfo ty with
                    | Some (arrName, idxName, _) ->
                        let texSyms = grp |> Array.map (fun (_, tn, _, _) -> Symbol.Create tn)
                        let mk = if ty = typeof<SamplerCube> then mkDummyCube else mkDummy2d
                        Some (arrName, idxName, texSyms, BindlessTexTable(updater, grp.Length, mk, delDummy))
                    | None -> None)
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
                let (sz, pk) = if dbl then doublePackerFor df32 requested else packerFor av.ContentType
                // double regions start at an EVEN word (8-byte) so HeapDataD addresses
                // them; over-allocate one word and align the start up.
                let b = arenaAlloc.Alloc (if dbl then sz + 1 else sz)
                let raw = int b.Offset
                let off = if dbl && (raw &&& 1) = 1 then raw + 1 else raw
                // grows only the staging mirror; the GPU resize is deferred to the
                // arena's own Compute (which depends on the updater whose
                // evaluation we are inside) — no transact happens here.
                arena.EnsureFloats arenaAlloc.Extent
                // CONSTANT sources are packed ONCE into staging — no RegionWriter
                // (no adaptive subscription to create at add / dispose at remove,
                // nothing for the flush to re-evaluate). Writer = null marks them.
                let w =
                    if av.IsConstant then
                        arena.StageOnce(off, sz, fun st -> pk (av.GetValueUntyped AdaptiveToken.Top) st off)
                        Unchecked.defaultof<RegionWriter>
                    else arena.Add(av, off, sz, pk)
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
        let packM44dInto (m : M44d) (a : float32[]) (off : int) =
            let wd = if df32 then wdDf else wd
            wd a (off+0)  m.M00; wd a (off+2)  m.M01; wd a (off+4)  m.M02; wd a (off+6)  m.M03
            wd a (off+8)  m.M10; wd a (off+10) m.M11; wd a (off+12) m.M12; wd a (off+14) m.M13
            wd a (off+16) m.M20; wd a (off+18) m.M21; wd a (off+20) m.M22; wd a (off+22) m.M23
            wd a (off+24) m.M30; wd a (off+26) m.M31; wd a (off+28) m.M32; wd a (off+30) m.M33
        let constituentPack (inv : bool) : obj -> float32[] -> int -> unit =
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
        // current fill page: the slot's allocs (uniforms/geometry/constituents) all route to
        // this page; set per-slot in Add/RemoveInternal. PlacePage rolls when the page fills.
        let mutable curPage = 0
        let setPage (i : int) =
            curPage <- i
            let pg = storage.Page i
            arena <- pg.Arena; arenaAlloc <- pg.ArenaAlloc; regions <- pg.Regions
            singleRegions <- pg.SingleRegions; constituentsF <- pg.ConstituentsF; constituentsB <- pg.ConstituentsB
            attrStatic <- pg.AttrStatic; idxStatic <- pg.IdxStatic
            if arena.ExtraDependency.IsNone then arena.ExtraDependency <- Some (updater :> IAdaptiveValue)
        // conservative worst-case word footprint of a slot's group (geometry + per-draw uniforms +
        // constituents), so PlacePage rolls BEFORE a slot that wouldn't fit ⇒ a group never spans pages.
        let estimateSlotWords (ro : RenderObject) : int =
            let vc = faceVertexCountOf ro
            vc * (max 4 (numAttrs * 4)) + (names.Length + numConst + 8) * 32
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
                        arena.StageOnce(off, sz, fun st -> pk (av.GetValueUntyped AdaptiveToken.Top) st off)
                        Unchecked.defaultof<RegionWriter>
                    else arena.Add(av, off, sz, pk)
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
            | _ -> ()
        // an 8-byte-aligned M44d slot the CHAIN fold writes (no aval / writer) — the
        // per-slot Model forward/backward constituent in chainMode.
        let allocFoldConstituent () : int * Management.Block<unit> =
            let sz = 32
            let b = arenaAlloc.Alloc (sz + 1)
            let raw = int b.Offset
            let off = if (raw &&& 1) = 1 then raw + 1 else raw
            arena.EnsureFloats arenaAlloc.Extent
            off, b
        // OUTPUT: a per-slot region the compute writes (no aval / writer), stored as
        // the shader's requested type (f32 M44f = 16 words, M33f = 9, …).
        let allocOutput (requested : System.Type) : int * Management.Block<unit> =
            let dbl = isDoubleUniform requested
            let (sz, _) = if dbl then doublePackerFor df32 requested else packerFor requested
            let b = arenaAlloc.Alloc (if dbl then sz + 1 else sz)
            let raw = int b.Offset
            let off = if dbl && (raw &&& 1) = 1 then raw + 1 else raw
            arena.EnsureFloats arenaAlloc.Extent
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
                        arena.StageOnce(off + AllocHeaderWords, szF, fun st -> pk (av.GetValueUntyped AdaptiveToken.Top) st (off + AllocHeaderWords))
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
                        (key : struct(obj * int * int)) (bytes : byte[]) (typeId : int) (count : int) (strideBytes : int) : StaticEntry =
            match dict.TryGetValue key with
            | true, e -> e.RefCount <- e.RefCount + 1; e
            | _ ->
                let sizeF = AllocHeaderWords + (bytes.Length + 3) / 4
                let b = arenaAlloc.Alloc sizeF
                let off = int b.Offset
                arena.EnsureFloats arenaAlloc.Extent
                arena.WriteHeader(off, typeId, count, strideBytes)
                arena.WriteStaticBytes(off + AllocHeaderWords, bytes)
                let e = { Ref = off; SizeF = sizeF; Count = count; RefCount = 1; Block = b }
                dict.[key] <- e
                e

        let freeStatic (dict : System.Collections.Generic.Dictionary<struct(obj * int * int), StaticEntry>) (key : struct(obj * int * int)) =
            match dict.TryGetValue key with
            | true, e ->
                e.RefCount <- e.RefCount - 1
                if e.RefCount = 0 then
                    dict.Remove key |> ignore
                    arenaAlloc.Free e.Block
            | _ -> ()

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
            let key = struct(geomDedupSource bv, bv.Offset, tid)
            match idxStatic.TryGetValue key with
            | true, e -> e.RefCount <- e.RefCount + 1; key, e
            | _ ->
                let bytes = readGeomBytes runtime bv
                let cnt = bytes.Length / es
                key, allocStatic idxStatic key bytes tid cnt es

        /// one consumed attribute of a new slot: singleton -> adaptive region,
        /// real buffer -> static allocation. Returns (release key, header ref).
        /// The allocation's header carries the RO's OWN element typeId — the
        /// shader decode branches on it at fetch time, so member element types
        /// vary freely within a bucket (they are NOT part of the bucket key).
        let attrFor (ro : RenderObject) (sym : Symbol) : AttrKey * int =
            let bv =
                match ro.VertexAttributes.TryGetAttribute sym with
                | ValueSome b -> b
                | ValueNone -> failwithf "Heap.ofRenderObjects: RO missing shader input attribute %A" sym
            match bv.Buffer with
            | :? ISingleValueBuffer as svb ->
                AttrKey.Single svb.Value, allocSingle svb.Value bv.ElementType
            | _ ->
                let et = bv.ElementType
                let tid =
                    match attrTypeId et with
                    | ValueSome t -> t
                    | ValueNone -> failwithf "Heap: attribute %A element type %A has no storage typeId" sym et
                let key = struct(geomDedupSource bv, bv.Offset, tid)
                match attrStatic.TryGetValue key with
                | true, e -> e.RefCount <- e.RefCount + 1; AttrKey.Static key, e.Ref
                | _ ->
                    let es = elemSize et
                    let bytes = readBytesView bv
                    let e = allocStatic attrStatic key bytes tid (bytes.Length / es) es
                    AttrKey.Static key, e.Ref

        // ── threshold-triggered compaction. After removals, a buffer whose live
        //    bytes dropped below 50% of its high-water (waste > live) AND whose
        //    waste exceeds compactionWasteFloorBytes is rewritten tightly within
        //    the SAME delta pass: live ranges are re-seated, every consumer
        //    offset (draw records' FirstIndex/BaseVertex/FirstInstance, dedup
        //    table entries, arena region offsets + their RegionWriters + the
        //    baked header cells) is rewritten, and the fresh buffer replaces the
        //    old one (ONE full re-upload). All bucket outputs re-derive from the
        //    updater version, so the rewrites are safe within the pass; cost is
        //    O(live) per fire and amortizes like growth doubling (between fires
        //    at least max(live, floor) bytes must be freed). ──
        let compactArena () =
            // collect EVERY arena resident — uniform-field regions, singleton-
            // attribute regions, static attribute/index allocations — and re-seat
            // them in ascending old offset so the staging memmove is front-to-back
            // (new offset <= old offset, no overlap hazard) …
            let res = System.Collections.Generic.List<struct(int * int * (int -> Management.Block<unit> -> unit))>()
            for KeyValue(_, e) in regions do
                let ee = e
                res.Add(struct(ee.Offset, ee.Size, fun off b ->
                    ee.Offset <- off; ee.Block <- b
                    if not (isNull ee.Writer) then ee.Writer.Off <- off))
            for KeyValue(_, e) in singleRegions do
                let ee = e
                res.Add(struct(ee.Offset, ee.Size, fun off b ->
                    ee.Offset <- off; ee.Block <- b
                    if not (isNull ee.Writer) then ee.Writer.Off <- off + AllocHeaderWords))
            for KeyValue(_, e) in attrStatic do
                let ee = e
                res.Add(struct(ee.Ref, ee.SizeF, fun off b -> ee.Ref <- off; ee.Block <- b))
            for KeyValue(_, e) in idxStatic do
                let ee = e
                res.Add(struct(ee.Ref, ee.SizeF, fun off b -> ee.Ref <- off; ee.Block <- b))
            res.Sort(fun (struct(a, _, _)) (struct(b, _, _)) -> compare a b)
            arenaAlloc.Reset()
            for (struct(oldOff, size, reseat)) in res do
                let b = arenaAlloc.Alloc size
                let off = int b.Offset
                if off <> oldOff then arena.MoveStaging(oldOff, off, size)
                reseat off b
            // … then rewrite every live slot's baked header cells (field region
            // offsets, attribute refs, index ref); the whole header table
            // re-uploads once this pass (headersAllDirty).
            for KeyValue(_, slt) in slots do
                let hb = slt.Slot * headerStride
                for i in 0 .. names.Length - 1 do
                    match regions.TryGetValue slt.RegionKeys.[i] with
                    | true, e -> headers.[hb + nameToField.[names.[i]]] <- e.Offset
                    | _ -> ()
                slt.AttrKeys |> Array.iteri (fun ai k ->
                    headers.[hb + attrBase + ai] <-
                        match k with
                        | AttrKey.Single av -> singleRegions.[av].Offset
                        | AttrKey.Static key -> attrStatic.[key].Ref)
                match idxStatic.TryGetValue slt.IdxKey with
                | true, e -> headers.[hb + idxCell] <- e.Ref
                | _ -> ()
            headersAllDirty <- true
            // one full [0, live) re-upload of the moved floats on the arena's next
            // Compute (rule-clean — the arena depends on the updater) + shrink the
            // staging mirror/GPU buffer back toward the live size.
            arena.RequestFullUpload arenaAlloc.Extent
            arena.ShrinkFloats arenaAlloc.Extent
            compactionCount <- compactionCount + 1

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
        let maybeCompact () =
            let inline need (a : HeapSpace) (unitBytes : int) =
                a.Live * 2 < a.Extent &&
                int64 a.Waste * int64 unitBytes > int64 compactionWasteFloorBytes
            if need arenaAlloc 4 then compactArena ()
            if useSlotAttr && need instAlloc 4 then compactInst ()

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
        // slot->page SSBO (for the per-page derive guard). Small; full-rewrite on pull.
        let slotPageBuf = MirrorBuffer(runtime, max 16 slotPage.Length * 4, BufferUsage.Storage)
        let flushSlotPage (_ : AdaptiveToken) (_ : System.Collections.Generic.HashSet<GateWriter>) =
            slotPageBuf.ResizeInPlace(uint64 (max 16 slotPage.Length * 4))
            if highWater > 0 then slotPageBuf.Write(slotPage, 0UL, 0, highWater)
        // CPU staging of the draw records in INDEXED layout (uploaded ranges
        // must be contiguous; entries itself stays in DrawCallInfo layout)
        let mutable drawStaging : DrawCallInfo[] = Array.zeroCreate entries.Length

        let flushDraws (t : AdaptiveToken) (gates : System.Collections.Generic.HashSet<GateWriter>) =
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
                IndirectBuffer.ofBuffer false 0UL sizeof<DrawCallInfo> highWater (b :> IBuffer))
        let instAval = (instBuf :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)
        let slotPageU = ((slotPageBuf :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)) :> IAdaptiveValue
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
            AVal.custom (fun t ->
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
                else
                    for pos in vtxStructDirty do if pos < n then vtxStamp t pos
                    vtxStructDirty.Clear()
                // non-constant source buffers: their backend handle can change
                // without a membership change, so re-read them every pull (few).
                for pos in vtxDynPos do if pos < n then vtxStamp t pos
                // the SSBO binding indexes [0,n): hand back the persistent array
                // when it is exactly sized, else a snapshot (ref-copy, no aval reads).
                if vtxOut.Length = n then vtxOut else Array.sub vtxOut 0 (max 1 n))
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
                let b = runtime.CreateBuffer<int>(max 1 derivedRecords.Length, BufferUsage.Storage)
                if derivedRecords.Length > 0 then b.Upload(derivedRecords, 0, 0, derivedRecords.Length)
                b
        // The derive's input binding is a PURE aval-based provider (NOT a
        // MutableComputeInputBinding): its values track the heap's live buffer/scalar avals
        // directly, so the backend descriptor stays current with NO manual Flush. Flush uses
        // `transact`, which is unsafe here — the derive dispatch runs render-integrated in a
        // SEPARATE resource update (the pre-pass), reading the descriptor asynchronously, so
        // a deferred transact would race. (The chain folds keep Flush + an IMMEDIATE Run, so
        // their values are consumed synchronously in the same eval — no hazard there.)
        let nAvalDerive = if hasDerived then AVal.custom (fun t -> updater.GetValue t |> ignore; highWater) else AVal.constant 0
        // PAGED: one derive input binding per page — binds THAT page's arena + HeapPageId so the
        // guarded shader writes only page-i slots into page i's arena.
        let pageDeriveInputs = System.Collections.Generic.List<IComputeInputBinding>()
        let mkDerivedInput (pageArenaU : IAdaptiveValue) (pid : int) : IComputeInputBinding =
            let provider =
                { new IUniformProvider with
                    member _.TryGetUniform(_, name) =
                        match string name with
                        | "n"            -> ValueSome (nAvalDerive :> IAdaptiveValue)
                        | "nRec"         -> ValueSome (AVal.constant numDerivedRecords :> IAdaptiveValue)
                        | "hstride"      -> ValueSome (AVal.constant headerStride :> IAdaptiveValue)
                        | "records"      -> ValueSome (AVal.constant (recBuf :> IBuffer) :> IAdaptiveValue)
                        | "HeapHeaders"  -> ValueSome headersU
                        | "HeapSlotPage" -> ValueSome slotPageU
                        | "HeapPageId"   -> ValueSome (AVal.constant pid :> IAdaptiveValue)
                        | "HeapDataD"    -> ValueSome pageArenaU
                        | "HeapData"     -> ValueSome pageArenaU
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
        // the per-slot derive runs as a render-integrated pre-pass (a DispatchCmd in
        // the bucket's CommandRenderObject), so no IComputeTask/Run for it — just the
        // dispatch group count, reactive on membership (highWater).
        let derivedGroups =
            if not hasDerived then AVal.constant V3i.III
            else AVal.custom (fun t -> updater.GetValue t |> ignore; V3i(max 1 ((highWater + 63) / 64), 1, 1))
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
            // No derive work happens here anymore — the per-slot derive is a
            // render-integrated pre-pass (DispatchCmd in the bucket CommandRenderObject).
            // derivedU only survives for CHAIN buckets, which must fold the Model chain into
            // the arena (camera-independent, edit-gated) BEFORE the derive reads it; for a
            // non-chain bucket HeapData is simply the arena buffer.
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
                            chOffBuf <- runtime.CreateBuffer<int>(cap, BufferUsage.Storage)
                            chLenBuf <- runtime.CreateBuffer<int>(cap, BufferUsage.Storage)
                            chainCap <- cap
                            chainStructAllDirty <- true
                        let idxExtent = max 1 chIdxAlloc.Extent
                        if idxExtent > chIdxBufCap then
                            let cap = Fun.NextPowerOfTwo idxExtent
                            if not (isNull (box chIdxBuf)) then chIdxBuf.Dispose()
                            chIdxBuf <- runtime.CreateBuffer<int>(cap, BufferUsage.Storage)
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
                            chainInput.["n"]           <- highWater
                            chainInput.["hstride"]     <- headerStride
                            chainInput.["mCell"]       <- modelFwdCell
                            chainInput.["chainOffset"] <- chOffBuf
                            chainInput.["chainLen"]    <- chLenBuf
                            chainInput.["linkIdx"]     <- chIdxBuf
                            chainInput.["links"]       <- linkBuf
                            chainInput.["HeapHeaders"] <- hdrBuf
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
                            chainProg.Run()
                            if chainBwdActive then chainInvProg.Run()
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
                let t = table :> aval<ITexture[] * int[]>
                texLookup.[Symbol.Create arrName] <- (t |> AVal.map fst) :> IAdaptiveValue
                texLookup.[Symbol.Create idxName] <- (t |> AVal.map snd) :> IAdaptiveValue
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
            let vtxE : Expr<int> = Expr.Cast (Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.VertexId))
            let idxRefE : Expr<int> = <@ uniform.HeapHeaders.[ %slotE * %(cint headerStride) + %(cint idxCell) ] @>
            let fieldOff (fi : int) : Expr<int> = <@ uniform.HeapHeaders.[ %slotE * %(cint headerStride) + %(cint fi) ] @>
            fun e ->
                // ONE pass per shader — derived composites are already materialized in
                // their arena output region by the compute pass, so every uniform read
                // (composite or plain) is a single field gather.
                e |> Effect.map (fun sh ->
                    let isVertex = sh.shaderStage = ShaderStage.Vertex
                    let vidVar = Var("heapVid", typeof<int>)
                    let vidE : Expr<int> = Expr.Cast (Expr.Var vidVar)
                    let body =
                        sh.shaderBody.SubstituteReads (fun kind ityp name idx _ ->
                            match kind, idx with
                            | ParameterKind.Uniform, None ->
                                match Map.tryFind name nameToField with
                                | Some fi -> Some (gatherFor ityp (fieldOff fi))
                                | None -> None
                            | ParameterKind.Input, None when isVertex ->
                                match attrInfos |> Array.tryFind (fun (_, n, _, _, _, _, _) -> n = name) with
                                | Some (ai, _, _, _, _, strideF, offF) ->
                                    if useBindlessGeom then
                                        Some (bindlessGatherFlat handleE vidE.Raw ityp numAttrs ai strideF offF)
                                    else
                                        let refE : Expr<int> = <@ uniform.HeapHeaders.[ %slotE * %(cint headerStride) + %(cint (attrBase + ai)) ] @>
                                        match hostGather ityp refE vidE with
                                        | Some g -> Some g
                                        | None -> failwithf "Heap: cannot storage-decode shader input '%s' (%A — supported: float32/V2f/V3f/V4f and int/V2i/V3i/V4i)" name ityp
                                | None -> None
                            | _ -> None)
                    let body = if isVertex then Expr.Let(vidVar, (<@ decodeHeapIndex %idxRefE %vtxE @>).Raw, body) else body
                    Shader.withBody body sh)

        // the bucket's render object — created ONCE; identity is stable across
        // membership changes. Rewritten surface, indirect draws, HeapData /
        // HeapHeaders / texture providers falling through to ro0's globals
        // minus the heap + sampler names.
        let bucketRO =
            let ro = RenderObject.Clone ro0
            ro.IsActive <- AVal.constant true      // per-draw gating lives in the indirect buffer
            // The KEYED pipeline state comes from the bucket KEY's resolved VALUES,
            // never from a member's live avals: a member whose dynamic mode aval
            // changes MOVES buckets (regroup pass) and must not be able to bend the
            // bucket it leaves. Non-keyed state (stencil, color/attachment masks,
            // blend constant, multisample, depth bias/clamp, …) still comes from
            // ro0's clone — the mode key assumes heapable ROs only vary in the keyed
            // subset; ROs differing in non-keyed state merge on ro0's values (same
            // pre-existing limitation as the bucket key itself).
            let (cull, frontFacing, fill, blend, depthTest, depthWrite) = pipeKey
            ro.RasterizerState <-
                { ro.RasterizerState with
                    CullMode = AVal.constant cull
                    FrontFacing = AVal.constant frontFacing
                    FillMode = AVal.constant fill }
            ro.BlendState <- { ro.BlendState with Mode = AVal.constant blend }
            ro.DepthState <-
                { ro.DepthState with
                    Test = AVal.constant depthTest
                    WriteMask = AVal.constant depthWrite }
            ro.Surface <-
                let baseE = heapRewrite effect
                if useAtlas then Surface.Effect (baseE |> rewriteAtlasSamples slotE atlasByName)
                elif samplers.Length > 0 then Surface.Effect (baseE |> rewriteSamplers slotE samplerByName |> overrideSamplerStates samplerStateOverrides)
                else Surface.Effect baseE
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
                        else
                            match texLookup.TryGetValue name with
                            | true, v -> ValueSome v
                            | _ ->
                                // every user uniform is a gathered region (rewritten
                                // out of the shader); nothing falls through to a global.
                                ValueNone
                    member _.Dispose() = () }
            ro

        // The per-slot fp64 derive runs as a render-integrated PRE-PASS: a SEPARATE,
        // draw-less CommandRenderObject carrying ONLY the DispatchCmd. The Vulkan
        // CommandTask lifts every DispatchCommand out of the command tree and replays it
        // (with a compute→vertex barrier) BEFORE the render pass, in the SAME submission as
        // the draws — so there is no separate synchronous compute submission / fence-wait.
        // CRUCIALLY the bucket draw RO is still exposed DIRECTLY (not wrapped in a
        // CommandRenderObject): wrapping the dynamic indirect bucket in OrderedCmd+RenderCmd
        // breaks membership churn (the inner render command does not track the bucket's
        // dynamic draw count → GPU hang). Both ROs come from the same Heap.ofRenderObjects
        // aset → same render task → same submission, so the pre-pass ordering holds.
        // PAGED derive: one DispatchCmd per page (binds page i's arena + HeapPageId=i; the guarded
        // shader writes ONLY page-i slots into page i's arena). They are bundled into ONE
        // CommandRenderObject per bucket (an OrderedCmd over all pages) — the Vulkan backend lifts
        // every nested DispatchCmd to a pre-pass, and a SINGLE pre-pass RO per bucket is what BOTH
        // the Simple (ISimpleSg) and legacy traversals handle correctly (N separate pre-pass ROs
        // per bucket are dropped by the Simple path).
        let deriveCmds = System.Collections.Generic.List<RuntimeCommand>()
        let ensureDeriveROs () =
            if hasDerived then
                while deriveCmds.Count < storage.Count do
                    let i = deriveCmds.Count
                    let pageArenaU = ((storage.Page(i).Arena :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)) :> IAdaptiveValue
                    deriveCmds.Add(RuntimeCommand.Dispatch(derivedShader, derivedGroups, Map.ofList [ "__input", box (mkDerivedInput pageArenaU i) ]))
        let deriveRO : IRenderObject =
            if not hasDerived then Unchecked.defaultof<_>
            else CommandRenderObject(RenderPass.main, scope, RuntimeCommand.Ordered (AList.ofAVal (updater |> AVal.map (fun _ -> deriveCmds :> seq<RuntimeCommand>)))) :> IRenderObject

        // PAGED draw fan-out. page 0 = `bucketRO` (the incremental machinery above, now zeroing
        // non-page-0 slots). pages >0 each get a fresh indirect (its slots only, full-rewrite flush)
        // + a clone of `bucketRO` binding page i's arena. `resultAval` is rebuilt per membership
        // version, so newly-rolled pages appear. FOLLOW-UP: per-page derive — pages >0 bind their
        // plain arena, so any derived/fp64/chain uniform there is page-0-stale until that lands.
        let pageROs = System.Collections.Generic.List<IRenderObject>()
        do pageROs.Add(bucketRO :> IRenderObject)
        let pageDrawBufs = System.Collections.Generic.List<MirrorBuffer>()
        let ensurePageROs () =
            while pageROs.Count < storage.Count do
                let pageIdx = pageROs.Count
                let mutable pstaging = Array.zeroCreate<DrawCallInfo> (max 16 entries.Length)
                let db = MirrorBuffer(runtime, pstaging.Length * sizeof<DrawCallInfo>, BufferUsage.Indirect)
                let flush (_ : AdaptiveToken) (_ : System.Collections.Generic.HashSet<GateWriter>) =
                    if pstaging.Length < entries.Length then pstaging <- Array.zeroCreate entries.Length
                    db.ResizeInPlace(uint64 (pstaging.Length * sizeof<DrawCallInfo>))
                    for s in 0 .. highWater - 1 do
                        pstaging.[s] <- (if slotPage.[s] = pageIdx then entries.[s] else zeroDraw)
                    if highWater > 0 then db.Write(pstaging, 0UL, 0, highWater)
                db.Dependency <- Some (updater :> IAdaptiveValue)
                db.Flush <- flush
                db.Name <- "HeapIndirectPage"
                pageDrawBufs.Add db
                let indirectP =
                    (db :> aval<IBackendBuffer>)
                    |> AdaptiveResource.mapNonAdaptive (fun b -> IndirectBuffer.ofBuffer false 0UL sizeof<DrawCallInfo> highWater (b :> IBuffer))
                let pageArenaU = ((storage.Page(pageIdx).Arena :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)) :> IAdaptiveValue
                let ro = RenderObject.Clone bucketRO
                ro.DrawCalls <- DrawCalls.Indirect indirectP
                ro.Uniforms <-
                    { new IUniformProvider with
                        member _.TryGetUniform(s, name) =
                            if name = symData || name = symDataI || name = symDataD then ValueSome pageArenaU
                            else bucketRO.Uniforms.TryGetUniform(s, name)
                        member _.Dispose() = () }
                pageROs.Add(ro :> IRenderObject)

        // PAGED: one render object per live storage page (each binds that page's arena +
        // its slots' indirect). ensurePageROs lazily creates them; resultAval (rebuilt per
        // membership version) picks up new pages. Page 0 keeps the derive/fold (`derivedU`);
        // pages >0 bind their plain arena (per-page derive is the documented follow-up).
        // built deterministically by SyncPages (called from the membership updater); the
        // members just hand back the current set.
        member x.SyncPages() = ensurePageROs (); ensureDeriveROs ()
        member x.RenderObjects : IRenderObject[] = pageROs.ToArray()
        member x.DeriveROs : IRenderObject[] = if hasDerived then [| deriveRO |] else [||]
        member _.Count = slots.Count
        member _.IsChain = chainMode
        member _.ChainDistinct = if chainMode then chainLinks.DistinctCount else 0

        /// footprint diagnostics (cheap; published every update). Geometry now
        /// lives in the arena, so the "packed geometry" metrics mirror the
        /// arena footprint (kept for tooling/tests: exact-size churn must keep
        /// them FLAT — freed allocations are reused in place).
        member private _.PublishStats() =
            lastPackedGeomBytes <- arenaAlloc.Extent * 4
            lastPackedGeomLiveBytes <- arenaAlloc.Live * 4
            lastArenaBytes <- arenaAlloc.Extent * 4
            lastArenaLiveBytes <- arenaAlloc.Live * 4
            lastInstBytes <- instAlloc.Extent * 4
            lastInstLiveBytes <- instAlloc.Live * 4

        member private _.AddInternal(ro : RenderObject) =
            let slot = if freeSlots.Count > 0 then freeSlots.Pop() else let s = highWater in highWater <- s + 1; s
            ensureSlot slot
            // route this slot's whole group to one page (rolling to a fresh page if the current
            // one is full). Estimate the slot's worst-case words so it always fits the chosen page.
            setPage (storage.PlacePage (estimateSlotWords ro))
            slotPage.[slot] <- curPage
            let regionKeys = System.Collections.Generic.List<IAdaptiveValue>(names.Length)
            let outBlocks = System.Collections.Generic.List<Management.Block<unit>>()
            for i in 0 .. names.Length - 1 do
                if derivedCells.Contains i then
                    // derived composite: a per-slot OUTPUT region the compute writes
                    // (no aval, no RegionWriter); the rewritten shader gathers it.
                    let (off, blk) = allocOutput (fieldRequestedType names.[i])
                    outBlocks.Add blk
                    headers.[slot * headerStride + i] <- off
                else
                    let av =
                        match ro.Uniforms.TryGetUniform(scope, nameSyms.[i]) with
                        | ValueSome v -> v
                        | ValueNone -> failwithf "Heap.ofRenderObjects: RO missing per-draw uniform '%s'" names.[i]
                    regionKeys.Add av
                    headers.[slot * headerStride + i] <- allocRegion av (fieldRequestedType names.[i])
            // derived-uniform CONSTITUENT regions (Model/View/Proj fwd/bwd, M44d):
            // Model in chainMode is the per-slot FOLD output (compute-written); every
            // other constituent is uploaded from the RO's base trafo aval, ref-counted
            // by aval (a shared camera → ONE region, mark re-packs once).
            let constKeys = System.Collections.Generic.List<struct(IAdaptiveValue * bool)>(numConst)
            let foldBlocks = System.Collections.Generic.List<Management.Block<unit>>()
            for k in 0 .. numConst - 1 do
                let c = neededConstituents.[k]
                let off =
                    if c.CBase = Derived.MBASE && chainMode then
                        let (o, blk) = allocFoldConstituent ()
                        foldBlocks.Add blk
                        o
                    else
                        let bav =
                            match ro.Uniforms.TryGetUniform(scope, Symbol.Create c.CBase) with
                            | ValueSome v -> v
                            | ValueNone -> failwithf "Heap.ofRenderObjects: derived uniform needs base trafo '%s' but the RO doesn't supply it" c.CBase
                        constKeys.Add(struct(bav, c.CInv))
                        allocConstituent bav c.CInv
                headers.[slot * headerStride + (fieldStride + k)] <- off
            // GPU trafo-chain: route the slot's UNFOLDED model stack into the link
            // arena (deduped) + a chIdx run; the GPU folds it into the slot's Model
            // forward (and, when consumed, backward) constituent region.
            if chainMode then
                let stack =
                    match ro.Uniforms.TryGetUniform(scope, symModelStack) with
                    | ValueSome (:? aval<aval<Trafo3d>[]> as st) -> AVal.force st
                    | _ -> failwith "Heap.ofRenderObjects: chainMode RO missing aval<aval<Trafo3d>[]> 'ModelTrafoStack'"
                addChainSlot slot stack
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
                        if not bv.Buffer.IsConstant then vtxDynPos.Add pos |> ignore
                    [||]
                else
                    attrInfos |> Array.map (fun (ai, _, sym, _, _, _, _) ->
                        let (key, r) = attrFor ro sym
                        headers.[slot * headerStride + attrBase + ai] <- r
                        key)
            // index allocation — or the -1 sentinel for NON-indexed members
            // (the shader's decodeHeapIndex passes gl_VertexIndex through);
            // their vertex count comes from the RO's Direct draw call.
            let struct(idxKey, idxRef, vertexCount) =
                match ro.Indices with
                | Some _ ->
                    let (k, e) = idxFor ro
                    struct(k, e.Ref, e.Count)
                | None ->
                    struct(noIdxKey, -1, faceVertexCountOf ro)
            headers.[slot * headerStride + idxCell] <- idxRef
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
            let instCount =
                if active.IsConstant then (if AVal.force active then k else 0)
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
            slots.[ro] <- { Slot = slot; Page = curPage; RegionKeys = regionKeys.ToArray(); Active = active; Instances = k; InstOffset = firstInstance
                            InstBlock = instBlock; AttrKeys = attrKeys; IdxKey = idxKey
                            ConstKeys = constKeys.ToArray(); OutBlocks = outBlocks.ToArray(); FoldBlocks = foldBlocks.ToArray() }

        member private _.RemoveInternal(ro : RenderObject) =
            match slots.TryGetValue ro with
            | true, s ->
                // free from the page the slot's group lives on
                setPage s.Page
                if chainMode then removeChainSlot s.Slot
                for k in s.RegionKeys do freeRegion k
                for struct(av, inv) in s.ConstKeys do freeConstituent av inv
                for b in s.OutBlocks do arenaAlloc.Free b
                for b in s.FoldBlocks do arenaAlloc.Free b
                for k in s.AttrKeys do
                    match k with
                    | AttrKey.Single av -> freeSingle av
                    | AttrKey.Static key -> freeStatic attrStatic key
                freeStatic idxStatic s.IdxKey
                if useBindlessGeom then
                    for ai in 0 .. numAttrs - 1 do
                        let pos = s.Slot * numAttrs + ai
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
                freeSlots.Push s.Slot
                slots.Remove ro |> ignore
            | _ -> ()

        /// Add ONE new member (no-op if already present). Called from the updater.
        member x.AddOne(ro : RenderObject) =
            if not (slots.ContainsKey ro) then
                x.AddInternal ro
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
        member x.Update(ros : RenderObject[]) =
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
                if not (slots.ContainsKey ro) then x.AddInternal ro
            x.PublishStats()

        /// Release all adaptive references (region writers, texture writers) and
        /// the bucket-owned GPU resources (atlas pages, dummy textures).
        member _.Dispose() =
            for KeyValue(_, e) in regions do
                if not (isNull e.Writer) then arena.Remove e.Writer
            regions.Clear()
            for KeyValue(_, e) in singleRegions do
                if not (isNull e.Writer) then arena.Remove e.Writer
            singleRegions.Clear()
            attrStatic.Clear()
            idxStatic.Clear()
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
            if hasDerived then (for inp in pageDeriveInputs do disp inp); disp derivedShader; disp recBuf
            if chainActive then
                disp chainProg; disp chainInput; disp chainShader
                if not (isNull (box chOffBuf)) then chOffBuf.Dispose()
                if not (isNull (box chLenBuf)) then chLenBuf.Dispose()
                if not (isNull (box chIdxBuf)) then chIdxBuf.Dispose()
            if chainBwdActive then disp chainInvProg; disp chainInvInput; disp chainInvShader
            slots.Clear()

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
    let private buildHeap (runtime : IRuntime) (objects : aset<IRenderObject>) : aset<IRenderObject> * (unit -> unit) =
        checkSupport false runtime
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

        // Bucket key = effect + topology + the VALUES of the per-RO pipeline state
        // (cull / front-face / fill / blend / depth test+write). Reading the state
        // avals through the token makes bucketing REACTIVE: a rule-driven mode value
        // change re-partitions the heap into the right buckets (one indirect draw =
        // one pipeline). This is wombat's per-RO dynamic "mode rules" — the rule is
        // simply each RO's state aval (often derived from its data); constant state
        // never re-partitions. Only mode changes rebuild buckets; per-draw value
        // changes still flow through the arena with no rebuild.
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
                    match r.VertexAttributes.TryGetAttribute (Symbol.Create name) with
                    | ValueSome bv -> sprintf "%s:%s:%d:%d" name bv.ElementType.FullName bv.Offset bv.Stride
                    | ValueNone -> name + ":?") |> String.concat ";"
            | _ -> ""
        let modeKey (layout : string) (t : AdaptiveToken) (r : RenderObject) =
            let ra = r.RasterizerState
            let eid = match r.Surface with | Surface.Effect e -> e.Id | _ -> "?"
            // IsTransparent partitions buckets so transparent and opaque ROs that otherwise
            // share effect+pipeline state still emit SEPARATE grouped ROs — TransparencyRenderTask
            // routes by RenderObject.IsTransparent (see TransparencyRenderTask.isTransparent),
            // so each bucket's combined output must carry the same flag as its inputs.
            // RenderObject.Clone copies IsTransparent (Pipeline/RenderObject.fs:120) so the
            // bucket's output inherits it automatically from any input in the partition.
            (eid, r.Mode, layout,
             ra.CullMode.GetValue t, ra.FrontFacing.GetValue t, ra.FillMode.GetValue t,
             r.BlendState.Mode.GetValue t,
             r.DepthState.Test.GetValue t, r.DepthState.WriteMask.GetValue t,
             r.IsTransparent)

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
                  typeof<V3d>; typeof<V2d>; typeof<V4d>; typeof<M33d> ])

        // ── per-draw field auto-detection ────────────────────────────────
        // Classification rule (deterministic per RO, memoized in RoFacts like
        // layoutSig): a uniform name becomes a PER-DRAW HEAP FIELD iff
        //   * the effect CONSUMES it — taken AFTER derived-rule expansion, so an
        //     effect reading ModelViewProjTrafo detects its bases (ModelTrafo,
        //     ViewProjTrafo), matching what `rewrite` will actually gather, and
        //   * it is not a sampler (textures keep the bindless/atlas path), and
        //   * the RO's OWN uniform provider supplies it (TryGetUniform succeeds)
        //     in a packable ContentType.
        // Everything else — names falling through to scene/global scope (camera,
        // lights), RO-supplied names the effect never reads, consumed+supplied
        // names of unpackable type — stays an ordinary uniform: NOT a field, the
        // RO stays heapable, and the read resolves through the bucket's globals
        // fall-through.
        // NOTE the fall-through answers from ONE live member: a consumed+supplied
        // UNPACKABLE uniform that genuinely varies per RO therefore merges on
        // that member's value — use a packable type if that matters.
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
            (match r.Uniforms.TryGetUniform(scope, Symbol.Create b) with ValueSome v -> isTrafoSupply v.ContentType | ValueNone -> false)
            || (b = Derived.MBASE &&
                (match r.Uniforms.TryGetUniform(scope, Symbol.Create "ModelTrafoStack") with ValueSome _ -> true | ValueNone -> false))
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
                        if packable.Contains v.ContentType then true
                        else
                            diag (sprintf "uniform '%s' is effect-consumed and RO-supplied but UNPACKABLE (ContentType = %s) — it stays a shared global resolved from ONE bucket member; if it genuinely varies per object, supply it in a packable type (M44f/Trafo3d/M44d/V4f/C4f/V3f/V2f/float32/float/int)." n v.ContentType.Name)
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
                                    match r.Uniforms.TryGetUniform(scope, Symbol.Create c.CBase) with
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
                    | Some (n, p) -> Some (sprintf "sampler '%s' has unsupported type %s (supported: Sampler2d, SamplerCube)" n p.uniformType.Name)
                    | None ->
                        if samps.Length > 0
                           && not supportsUnbounded
                           && not (samps |> Array.forall (fun (_, p) -> p.uniformType = typeof<Sampler2d>)) then
                            Some "per-object textures need descriptor indexing for non-2d samplers (the atlas fallback handles Sampler2d only)"
                        else
                            let mismatch =
                                samps
                                |> Array.choose (fun (n, p) ->
                                    match p.uniformValue with
                                    | UniformValue.Sampler(_, st) -> Some (p.uniformType, (n, st))
                                    | UniformValue.SamplerArray a when a.Length > 0 -> Some (p.uniformType, (n, snd a.[0]))
                                    | _ -> None)
                                |> Array.groupBy fst
                                |> Array.tryPick (fun (ty, g) ->
                                    let (_, (_, st0)) = g.[0]
                                    if g |> Array.forall (fun (_, (_, st)) -> st = st0) then None
                                    else Some (sprintf "samplers of type %s use DIFFERING sampler states (one bindless array per type carries ONE state): %s" ty.Name (g |> Array.map (fst << snd) |> String.concat ", ")))
                            mismatch
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
                            e.Inputs |> Map.toSeq |> Seq.tryPick (fun (name, inputT) ->
                                match ro.VertexAttributes.TryGetAttribute (Symbol.Create name) with
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
                                e.Inputs |> Map.toSeq |> Seq.tryPick (fun (name, _) ->
                                    match ro.VertexAttributes.TryGetAttribute (Symbol.Create name) with
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
        // CHANGES (a true delta — no snapshot read, no HashSet.computeDelta),
        // groups by (token-reactive) mode key and feeds each bucket's
        // persistent IncrementalBucket — an add/remove is O(changed) instead
        // of O(bucket), for EVERY bucket kind (host or bindless geometry,
        // bindless-textured, atlas, instanced). Every bucket-internal aval
        // (indirect, headers, geometry, textures, arena flush) hangs off the
        // updater, so evaluation order doesn't matter.
        let objReader = objects.GetReader()

        // intern mode keys to unique tokens, so the per-change grouping hashes
        // object references instead of 10-tuples-with-strings (20k ROs/change).
        // keyValues is the reverse map: token -> the RESOLVED mode-key tuple, so a
        // bucket can bake its pipeline state from the KEY's values (a member's
        // dynamic mode aval can then never bend the bucket it leaves — it moves).
        // hand-rolled comparer: the F# generic structural comparer on this
        // 10-tuple (two strings + BlendMode + enums) costs µs per intern lookup
        let modeKeyComparer =
            { new System.Collections.Generic.IEqualityComparer<string * IndexedGeometryMode * string * CullMode * WindingOrder * FillMode * BlendMode * DepthTest * bool * bool> with
                member _.GetHashCode((eid, m, layout, cull, ff, fill, blend, dt, dw, tr)) =
                    let mutable h = eid.GetHashCode()
                    h <- h * 31 + layout.GetHashCode()
                    h <- h * 31 + int m
                    h <- h * 31 + int cull
                    h <- h * 31 + int ff
                    h <- h * 31 + int fill
                    h <- h * 31 + blend.GetHashCode()
                    h <- h * 31 + int dt
                    h <- h * 31 + (if dw then 1 else 0)
                    h * 2 + (if tr then 1 else 0)
                member _.Equals((e1, m1, l1, c1, f1, fl1, b1, d1, w1, t1), (e2, m2, l2, c2, f2, fl2, b2, d2, w2, t2)) =
                    m1 = m2 && c1 = c2 && f1 = f2 && fl1 = fl2 && d1 = d2 && w1 = w2 && t1 = t2
                    && b1.Equals b2 && e1 = e2 && l1 = l2 }
        let keyInterner = System.Collections.Generic.Dictionary<_, obj>(modeKeyComparer)
        let keyValues = System.Collections.Generic.Dictionary<obj, string * IndexedGeometryMode * string * CullMode * WindingOrder * FillMode * BlendMode * DepthTest * bool * bool>(HashIdentity.Reference)
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
                        let e = match r.Surface with | Surface.Effect e -> e | _ -> failwith "Heap.ofRenderObjects: expected Surface.Effect"
                        // GPU trafo-chain eligibility: the effect DEPENDS ON Model
                        // (reads ModelTrafo, or a composite whose recipe consumes it)
                        // AND the RO exposes the UNFOLDED stack as aval<aval<Trafo3d>[]>.
                        // Then the slot's Model constituent is GPU-folded into the
                        // arena instead of uploaded.
                        let dependsOnModel =
                            consumedNonSamplerNames e |> Array.exists Derived.dependsOnModel
                        let chain =
                            not disableChain && dependsOnModel &&
                            (match r.Uniforms.TryGetUniform(scope, Symbol.Create "ModelTrafoStack") with
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
                            (if bindless then "gpu:" + bindlessSig r else "host")
                            + (if inst then "|inst" else "")
                            + (if chain then "|chain" else "")
                            + "|f:" + String.concat ";" fields
                        let ra = r.RasterizerState
                        let allConst =
                            ra.CullMode.IsConstant && ra.FrontFacing.IsConstant && ra.FillMode.IsConstant &&
                            r.BlendState.Mode.IsConstant && r.DepthState.Test.IsConstant && r.DepthState.WriteMask.IsConstant
                        { Heapable = true
                          Layout = layout
                          ConstToken = if allConst then internKey (modeKey layout t r) else null
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
        // RO -> its bucket key (only valid while running incrementally, i.e. no
        // dynamic-mode ROs in the set)
        let roBucket = System.Collections.Generic.Dictionary<RenderObject, obj>(HashIdentity.Reference)
        let updaterRef = ref (Unchecked.defaultof<aval<int>>)
        // number of heapable ROs in the set whose mode key is DYNAMIC (any
        // non-constant pipeline-state aval). While > 0 the grouping must be
        // recomputed every evaluation (token-reactive re-partitioning).
        let dynCount = ref 0
        // a full regroup ran (or nothing ran yet): the incremental bookkeeping
        // (roBucket / passSet) must be resynced by one more full pass before
        // delta processing may resume.
        let needFullSync = ref true
        let version = ref 0

        let mkBucket (key : obj) (r0 : RenderObject) (f0 : RoFacts) =
            let effect = match r0.Surface with | Surface.Effect e -> e | _ -> failwith "Heap.ofRenderObjects: expected Surface.Effect"
            let (_, _, _, cull, ff, fill, blend, dtest, dwrite, _) = keyValues.[key]
            // field names/layout come from the FACTS (per-bucket: the field set is
            // part of the bucket key, so every member shares f0's interned set)
            // step 1: one private storage per bucket ⇒ behaviour-identical. (Sharing across
            // buckets/heaps comes later by passing the SAME storage to several buckets.)
            let storage = HeapStorage(runtime)
            let c = IncrementalBucket(runtime, storage, f0.Fields, f0.FieldMap, effect, r0, updaterRef.Value, f0.Bindless, f0.Instanced,
                                      (cull, ff, fill, blend, dtest, dwrite), f0.Chain)
            caches.[key] <- c
            c

        let updater =
            AVal.custom (fun t ->
                let delta = objReader.GetChanges t

                // facts + dynamic-RO census from the delta (cheap: O(changed))
                for op in delta do
                    match op with
                    | Add(_, o) ->
                        let f = factsOf t o
                        if f.Heapable && isNull f.ConstToken then dynCount.Value <- dynCount.Value + 1
                    | Rem(_, o) ->
                        match roFacts.TryGetValue o with
                        | true, f when f.Heapable && isNull f.ConstToken -> dynCount.Value <- dynCount.Value - 1
                        | _ -> ()

                if dynCount.Value > 0 || needFullSync.Value then
                    // ── full regroup (dynamic mode keys present, or resync after
                    //    one): same semantics as before, but every group hits its
                    //    persistent cache (the cache diffs the membership itself).
                    needFullSync.Value <- dynCount.Value > 0
                    roBucket.Clear()
                    passSet.Clear()
                    let groups = System.Collections.Generic.Dictionary<obj, System.Collections.Generic.List<struct(RenderObject * RoFacts)>>(HashIdentity.Reference)
                    for o in objReader.State do
                        let f = factsOf t o
                        if f.Heapable then
                            let r = o :?> RenderObject
                            let key = if isNull f.ConstToken then internKey (modeKey f.Layout t r) else f.ConstToken
                            let lst =
                                match groups.TryGetValue key with
                                | true, l -> l
                                | _ -> let l = System.Collections.Generic.List() in groups.[key] <- l; l
                            lst.Add(struct(r, f))
                        else
                            passSet.Add o |> ignore
                    for KeyValue(key, lst) in groups do
                        let (struct(r0, f0)) = lst.[0]
                        let cache =
                            match caches.TryGetValue key with
                            | true, c -> c
                            | _ -> mkBucket key r0 f0
                        cache.Update (Array.init lst.Count (fun i -> let (struct(r, _)) = lst.[i] in r))
                        for struct(r, _) in lst do roBucket.[r] <- key
                    // dispose caches whose bucket key vanished
                    let dead = caches.Keys |> Seq.filter (fun k -> not (groups.ContainsKey k)) |> Seq.toArray
                    for k in dead do
                        caches.[k].Dispose()
                        caches.Remove k |> ignore
                else
                    // ── incremental: process ONLY the membership delta. Removals
                    //    FIRST so their slots / arena offsets / texture indices are
                    //    reusable by this very update (paired add+remove churn then
                    //    keeps the slot high-water at the live count). ──
                    for op in delta do
                        match op with
                        | Rem(_, o) ->
                            match roFacts.TryGetValue o with
                            | true, f when f.Heapable ->
                                let r = o :?> RenderObject
                                match roBucket.TryGetValue r with
                                | true, key ->
                                    roBucket.Remove r |> ignore
                                    match caches.TryGetValue key with
                                    | true, c ->
                                        c.RemoveOne r
                                        if c.Count = 0 then
                                            c.Dispose()
                                            caches.Remove key |> ignore
                                    | _ -> ()
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
                                let key = f.ConstToken          // non-null: dynCount = 0
                                roBucket.[r] <- key
                                let c =
                                    match caches.TryGetValue key with
                                    | true, c -> c
                                    | _ -> mkBucket key r f
                                c.AddOne r

                lastBucketCount <- caches.Count
                let mutable chainB = 0
                let mutable chainD = 0
                for KeyValue(_, c) in caches do
                    // materialize each bucket's per-page render/derive ROs to match its storage,
                    // DETERMINISTICALLY here in the membership update — so they're present in
                    // `resultAval` before any render builds its command buffer (no lazy/first-frame gap).
                    c.SyncPages()
                    if c.IsChain then chainB <- chainB + 1; chainD <- chainD + c.ChainDistinct
                lastChainBuckets <- chainB
                if chainB > 0 then lastDistinctLinks <- chainD
                version.Value <- version.Value + 1
                version.Value)
        updaterRef.Value <- updater
        let teardown () =
            // free every bucket (GPU buffers + object-count CPU) and drop the reader
            for KeyValue(_, c) in caches do c.Dispose()
            caches.Clear()
            passSet.Clear()
            roBucket.Clear()
            match box objReader with
            | :? System.IDisposable as d -> d.Dispose()
            | _ -> ()
        let resultAval =
            updater |> AVal.map (fun _ ->
                // derive pre-passes ∪ per-page bucket sub-draws ∪ untouched passthrough.
                // a bucket now contributes ONE RO per live storage page (RenderObjects) plus its
                // derive pre-passes (DeriveROs); both arrays grow as the bucket rolls new pages.
                let out = System.Collections.Generic.List<IRenderObject>(caches.Count * 2 + passSet.Count)
                for KeyValue(_, c) in caches do
                    out.AddRange c.DeriveROs
                    out.AddRange c.RenderObjects
                for o in passSet do out.Add o
                out.ToArray())
        (resultAval |> ASet.ofAVal), teardown

    /// Collapse an adaptive set of N render objects into bucket render objects.
    /// Allocates NOTHING up front (ref-count zero): the heap's machinery — input
    /// reader, per-bucket CPU model and ALL GPU buffers — is built lazily on the
    /// FIRST activation (a render task picking up the heap) and torn down COMPLETELY
    /// when the LAST task drops it (no GPU and no object-count-sized state held at
    /// ref-count zero). Re-activation rebuilds from scratch; concurrent tasks share
    /// one machinery via the ref-count. The drawing is carried by the bucket ROs;
    /// the activation itself rides on an ActivationRenderObject that both backends
    /// ignore for rendering and only activate/deactivate.
    let ofRenderObjects (runtime : IRuntime) (objects : aset<IRenderObject>) : aset<IRenderObject> =
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
                | ValueNone -> let r = buildHeap runtime objects in shared <- ValueSome r; fst r)

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
                    | ValueSome td -> td (); transact (fun () -> gen.Value <- gen.Value + 1)
                    | ValueNone -> () }

        let activationRO = ActivationRenderObject(RenderPass.main, Ag.Scope.Root, activate)
        // `bind` owns the inner reader: it forwards the live machinery's incremental
        // deltas, and on a teardown (gen bump → new machinery, or none) it drops the
        // old reader and adopts the new one, emitting the switch itself. Building in
        // the mapping makes the buckets surface in the first evaluation (no lag).
        let buckets = gen |> ASet.bind (fun _ -> ensureBuilt())
        ASet.union (ASet.single (activationRO :> IRenderObject)) buckets

    // ── fp64 derived-uniform compute pre-pass ───────────────────────────
    // Wombat derives per-object trafos (ModelViewProjTrafo, NormalMatrix, ...)
    // in a GPU compute pre-pass at df32 precision; we use REAL fp64 (M44d /
    // dmat4, shaderFloat64). The pre-pass computes the derived matrices once per
    // object per frame (not per vertex) in double precision and writes them as
    // f32 into a heap arena the render gathers by gl_InstanceIndex. Camera-
    // relative math (View * Model) stays precise at geodetic scale where an f32
    // inline ModelViewProj would jitter. Reactive: the arena re-runs the compute
    // whenever any model or the camera changes (AVal.custom over the inputs).

    module private Fp64 =
        // one thread per object: arena[2i] = Proj*View*Model, arena[2i+1] =
        // (Model^-1)^T (NormalMatrix) — all evaluated in fp64, stored as f32.
        [<LocalSize(X = 64)>]
        let derive (n : int) (view : M44d) (proj : M44d) (model : M44d[]) (outp : M44f[]) =
            compute {
                let i = getGlobalId().X
                if i < n then
                    let m = model.[i]
                    outp.[2*i]     <- M44f(proj * view * m)
                    outp.[2*i + 1] <- M44f(m.Inverse.Transposed)
            }

    /// Build a heap-rendered scene whose ModelViewProjTrafo + NormalMatrix are
    /// computed per object by an fp64 GPU compute pre-pass (camera-relative,
    /// geodetic-precise) and gathered in the rewritten shader. `models` are the
    /// per-object world transforms; `view`/`proj` the camera. The effect reads
    /// `uniform?ModelViewProjTrafo` and/or `uniform?NormalMatrix`. One instanced
    /// indirect draw; the compute re-dispatches reactively on input changes.
    let derivedFp64 (runtime : IRuntime) (mode : IndexedGeometryMode)
                    (positions : V3f[]) (normals : V3f[]) (index : int[])
                    (effect : Effect)
                    (view : aval<Trafo3d>) (proj : aval<Trafo3d>) (models : aval<Trafo3d>[]) : ISg =
        checkSupport false runtime
        let n = models.Length
        // NOTE: the compute resources created below (buffers, shader, program, input
        // binding) live as long as the process — the returned ISg has no teardown
        // hook. Intentional: this is a fixed-population demo/research entry point;
        // call it once per scene, not per frame.
        let stride = 32                 // floats per object: MVP(16) + NormalMatrix(16)
        let outBuf   = runtime.CreateBuffer<M44f>(max 1 (2 * n))
        let modelBuf = runtime.CreateBuffer<M44d>(max 1 n)
        let shader   = runtime.CreateComputeShader Fp64.derive
        let input    = runtime.CreateInputBinding shader
        let groups   = (n + shader.LocalSize.X - 1) / shader.LocalSize.X
        let prog     = runtime.CompileCompute [ ComputeCommand.Bind shader; ComputeCommand.SetInput input; ComputeCommand.Dispatch groups ]

        // reactive arena: re-run the fp64 compute when any model / the camera marks
        let arena =
            AVal.custom (fun t ->
                modelBuf.Upload(models |> Array.map (fun m -> (m.GetValue t).Forward))
                input.["n"]     <- n
                input.["view"]  <- (view.GetValue t).Forward
                input.["proj"]  <- (proj.GetValue t).Forward
                input.["model"] <- modelBuf
                input.["outp"]  <- outBuf
                input.Flush()
                prog.Run()
                outBuf :> IBuffer)

        // rewrite the two derived reads into arena gathers (stride 32: MVP@0, NM@16)
        let iid : Expr<int> = Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.InstanceId)
        let eff =
            effect |> Effect.substituteUniforms (fun name typ _ _ ->
                match name with
                | "ModelViewProjTrafo" -> Some (gatherFor typ <@ %iid * stride + 0 @>)
                | "NormalMatrix"       -> Some (gatherFor typ <@ %iid * stride + 16 @>)
                | _ -> None)

        let bv (a : System.Array) tp = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), tp)
        let symHeap = Symbol.Create "HeapData"
        let heapU = arena :> IAdaptiveValue
        let ro = RenderObject()
        ro.Surface          <- Surface.Effect eff
        ro.Mode             <- mode
        ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        ro.Indices          <- Some (bv index typeof<int>)
        ro.DrawCalls        <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = n) |])
        ro.Uniforms <-
            { new IUniformProvider with
                member _.TryGetUniform(_, name) = if name = symHeap then ValueSome heapU else ValueNone
                member _.Dispose() = () }
        Sg.renderObjectSet (ASet.single (ro :> IRenderObject))


    /// Slice-A driver: per-object trafo chains (root-first) composed on the GPU.
    /// `chains.[i]` is object i's ancestor link list; each link is an
    /// `aval<Trafo3d>`. No dedup yet (every link uploaded) — proves the compute
    /// math against the composed `derivedFp64` path. The effect reads
    /// `uniform?ModelViewProjTrafo` and/or `uniform?NormalMatrix`.
    let derivedChainFp64 (runtime : IRuntime) (mode : IndexedGeometryMode)
                         (positions : V3f[]) (normals : V3f[]) (index : int[])
                         (effect : Effect)
                         (view : aval<Trafo3d>) (proj : aval<Trafo3d>)
                         (chains : aval<Trafo3d>[][]) : ISg =
        checkSupport false runtime
        let n = chains.Length
        // NOTE: like derivedFp64, the GPU resources below are process-lifetime
        // (no teardown hook on the returned ISg) — fixed-population entry point.
        let stride = 32                     // floats per object: MVP(16) + NormalMatrix(16)

        // flatten chains, DEDUP links to one arena slot each. TWO dedup keys,
        // mirroring the geometry/region value-dedup landed elsewhere in the heap:
        //   * DYNAMIC links (cval / mapped) key on aval IDENTITY — a shared parent
        //     scope reused across leaves is ONE slot, so editing it marks one slot.
        //   * CONSTANT links key on their Trafo3d VALUE — the per-leaf box link is a
        //     DISTINCT `AVal.constant` instance per Box (dom builds `box |> AVal.map
        //     (Scale*Translation)`), so identity-dedup would keep 20000 copies; by
        //     value they collapse to ONE slot (all boxes share Box3d.Unit's link).
        //     This is the per-leaf-constant-link "folds once on GPU + dedups across
        //     leaves" the chain feeding is meant to deliver.
        let slotById  = System.Collections.Generic.Dictionary<IAdaptiveValue, int>(HashIdentity.Reference)
        let slotByVal = System.Collections.Generic.Dictionary<Trafo3d, int>(HashIdentity.Structural)
        let distinct    = System.Collections.Generic.List<aval<Trafo3d>>()
        let chainOffset = Array.zeroCreate<int> (max 1 n)
        let chainLen    = Array.zeroCreate<int> (max 1 n)
        let idxList     = System.Collections.Generic.List<int>()
        let mutable cur = 0
        for i in 0 .. n - 1 do
            chainOffset.[i] <- cur
            chainLen.[i] <- chains.[i].Length
            for l in chains.[i] do
                let slot =
                    if l.IsConstant then
                        let v = AVal.force l
                        match slotByVal.TryGetValue v with
                        | true, s -> s
                        | _ -> let s = distinct.Count in slotByVal.[v] <- s; distinct.Add (AVal.constant v); s
                    else
                        match slotById.TryGetValue (l :> IAdaptiveValue) with
                        | true, s -> s
                        | _ -> let s = distinct.Count in slotById.[l :> IAdaptiveValue] <- s; distinct.Add l; s
                idxList.Add slot
                cur <- cur + 1
        let linkIdx = idxList.ToArray()
        let distinctArr = distinct.ToArray()
        lastDistinctLinks <- distinctArr.Length

        // distinct links live in a dirty-tracked fp64 arena (shared link = 1 slot,
        // one upload on change); chain structure buffers are static (uploaded once).
        let linkArena = LinkArena(runtime, distinctArr)
        let outBuf  = runtime.CreateBuffer<M44f>(max 1 (2 * n))
        let offBuf  = runtime.CreateBuffer<int>(max 1 n)
        let lenBuf  = runtime.CreateBuffer<int>(max 1 n)
        let idxBuf  = runtime.CreateBuffer<int>(max 1 cur)
        offBuf.Upload chainOffset
        lenBuf.Upload chainLen
        idxBuf.Upload linkIdx

        let shader = runtime.CreateComputeShader Chain.composeMvpNm
        let input  = runtime.CreateInputBinding shader
        let groups = (max 1 n + shader.LocalSize.X - 1) / shader.LocalSize.X
        let prog   = runtime.CompileCompute [ ComputeCommand.Bind shader; ComputeCommand.SetInput input; ComputeCommand.Dispatch groups ]

        // reactive: forcing linkArena uploads only the changed link(s); the dispatch
        // re-runs because this depends on linkArena / view / proj.
        let arena =
            AVal.custom (fun t ->
                let linkBuf = (linkArena :> aval<IBackendBuffer>).GetValue t
                input.["n"]           <- n
                input.["view"]        <- (view.GetValue t).Forward
                input.["proj"]        <- (proj.GetValue t).Forward
                input.["chainOffset"] <- offBuf
                input.["chainLen"]    <- lenBuf
                input.["linkIdx"]     <- idxBuf
                input.["links"]       <- linkBuf
                input.["outp"]        <- outBuf
                input.Flush()
                prog.Run()
                outBuf :> IBuffer)

        let iid : Expr<int> = Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.InstanceId)
        let eff =
            effect |> Effect.substituteUniforms (fun name typ _ _ ->
                match name with
                | "ModelViewProjTrafo" -> Some (gatherFor typ <@ %iid * stride + 0 @>)
                | "NormalMatrix"       -> Some (gatherFor typ <@ %iid * stride + 16 @>)
                | _ -> None)

        let bv (a : System.Array) tp = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), tp)
        let symHeap = Symbol.Create "HeapData"
        let heapU = arena :> IAdaptiveValue
        let ro = RenderObject()
        ro.Surface          <- Surface.Effect eff
        ro.Mode             <- mode
        ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        ro.Indices          <- Some (bv index typeof<int>)
        ro.DrawCalls        <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = n) |])
        ro.Uniforms <-
            { new IUniformProvider with
                member _.TryGetUniform(_, name) = if name = symHeap then ValueSome heapU else ValueNone
                member _.Dispose() = () }
        Sg.renderObjectSet (ASet.single (ro :> IRenderObject))

    // ── Bindless geometry: type-agnostic vertex-pull from GPU buffers by handle ──
    // GPU-resident geometry can't be CPU-packed; instead each object's vertices live
    // in ONE flat float32 SSBO array indexed by HANDLE (gl_InstanceIndex). The vertex
    // shader PULLS each attribute by decoding `componentCount` floats at a fixed
    // per-vertex offset (exactly like the host arena's gatherFor) — no fixed-function
    // vertex input, no copy, NO type assumptions (any float-vector attribute, any
    // number of them). The index buffer stays fixed-function so uint16/uint32 indices
    // work natively. Requires descriptor indexing (unbounded storage-buffer arrays).

    /// reinterpret a blittable float-based array (V2f[]/V3f[]/V4f[]/float32[]) as floats
    let private arrayToFloats (a : System.Array) : float32[] =
        let elemBytes = System.Runtime.InteropServices.Marshal.SizeOf(a.GetType().GetElementType())
        let f = Array.zeroCreate<float32> (a.Length * elemBytes / 4)
        let h = System.Runtime.InteropServices.GCHandle.Alloc(a, System.Runtime.InteropServices.GCHandleType.Pinned)
        try System.Runtime.InteropServices.Marshal.Copy(h.AddrOfPinnedObject(), f, 0, f.Length)
        finally h.Free()
        f

    /// concatenate same-typed System.Arrays (per-object index arrays -> one buffer)
    let private concatArrays (arrs : System.Array[]) (et : System.Type) : System.Array =
        let total = arrs |> Array.sumBy (fun a -> a.Length)
        let res = System.Array.CreateInstance(et, total)
        let mutable o = 0
        for a in arrs do System.Array.Copy(a, 0, res, o, a.Length); o <- o + a.Length
        res

    /// decode attribute `typ` at element offset (vid*stride + fieldOffset) from
    /// HeapVertexData[handle] (float view) or HeapVertexDataI[handle] (int view, same
    /// bytes) — the bindless analogue of the host arena's gatherFor.
    let private bindlessGather (typ : System.Type) (stride : int) (fieldOffset : int) : Expr =
        if typ = typeof<float32> then
            <@@ let hh = (%%handleExpr : int) in uniform.HeapVertexData.[hh].[ (%%vidExpr : int) * stride + fieldOffset ] @@>
        elif typ = typeof<V2f> then
            <@@ let hh = (%%handleExpr : int) in
                let o = (%%vidExpr : int) * stride + fieldOffset
                V2f(uniform.HeapVertexData.[hh].[o], uniform.HeapVertexData.[hh].[o+1]) @@>
        elif typ = typeof<V3f> then
            <@@ let hh = (%%handleExpr : int) in
                let o = (%%vidExpr : int) * stride + fieldOffset
                V3f(uniform.HeapVertexData.[hh].[o], uniform.HeapVertexData.[hh].[o+1], uniform.HeapVertexData.[hh].[o+2]) @@>
        elif typ = typeof<V4f> then
            <@@ let hh = (%%handleExpr : int) in
                let o = (%%vidExpr : int) * stride + fieldOffset
                V4f(uniform.HeapVertexData.[hh].[o], uniform.HeapVertexData.[hh].[o+1], uniform.HeapVertexData.[hh].[o+2], uniform.HeapVertexData.[hh].[o+3]) @@>
        elif typ = typeof<int> then
            <@@ let hh = (%%handleExpr : int) in uniform.HeapVertexDataI.[hh].[ (%%vidExpr : int) * stride + fieldOffset ] @@>
        elif typ = typeof<V2i> then
            <@@ let hh = (%%handleExpr : int) in
                let o = (%%vidExpr : int) * stride + fieldOffset
                V2i(uniform.HeapVertexDataI.[hh].[o], uniform.HeapVertexDataI.[hh].[o+1]) @@>
        elif typ = typeof<V3i> then
            <@@ let hh = (%%handleExpr : int) in
                let o = (%%vidExpr : int) * stride + fieldOffset
                V3i(uniform.HeapVertexDataI.[hh].[o], uniform.HeapVertexDataI.[hh].[o+1], uniform.HeapVertexDataI.[hh].[o+2]) @@>
        else
            <@@ let hh = (%%handleExpr : int) in
                let o = (%%vidExpr : int) * stride + fieldOffset
                V4i(uniform.HeapVertexDataI.[hh].[o], uniform.HeapVertexDataI.[hh].[o+1], uniform.HeapVertexDataI.[hh].[o+2], uniform.HeapVertexDataI.[hh].[o+3]) @@>

    /// Render N objects whose geometry lives in per-object GPU buffers, vertex-PULLED
    /// in the (rewritten) shader — no vertex buffers, no CPU packing. TYPE-AGNOSTIC and
    /// attribute-general: `attribs.[i]` maps each of the effect's vertex-input semantics
    /// to object i's data (V2f[]/V3f[]/V4f[]/float32[]); `indices.[i]` is object i's
    /// LOCAL 0-based index array (int/uint16/uint32). Each object's attributes are
    /// interleaved into one flat float32 SSBO array element, decoded per-attribute by
    /// component count. One indexed indirect multidraw; handle via gl_InstanceIndex.
    /// `view`/`proj` go through the ambient camera path so this SG composes normally.
    let bindless (runtime : IRuntime) (mode : IndexedGeometryMode) (effect : Effect)
                 (attribs : Map<Symbol, System.Array>[]) (indices : System.Array[])
                 (view : aval<Trafo3d>) (proj : aval<Trafo3d>) : ISg =
        checkSupport false runtime
        let n = attribs.Length

        // vertex layout = the effect's input attributes (any count/type), each with a
        // float component count and a cumulative offset within one interleaved vertex.
        let mutable fo = 0
        let fields =
            effect.Inputs |> Map.toArray
            |> Array.map (fun (name, _) ->
                let sym = Symbol.Create name
                let typ = attribs.[0].[sym].GetType().GetElementType()
                let c = componentsOf typ
                let off = fo
                fo <- fo + c
                name, sym, typ, off)
        let vertexStride = fo
        let fieldByName = fields |> Array.map (fun (name, _, typ, off) -> name, (typ, off)) |> Map.ofArray

        // per object: interleave its attributes into one flat float32[] vertex buffer
        let buildObject (m : Map<Symbol, System.Array>) : float32[] =
            let per = fields |> Array.map (fun (_, sym, _, _) -> arrayToFloats m.[sym])
            let cs  = fields |> Array.map (fun (_, _, typ, _) -> componentsOf typ)
            let vtx = per.[0].Length / cs.[0]
            let out = Array.zeroCreate<float32> (vtx * vertexStride)
            for v in 0 .. vtx - 1 do
                let mutable o = 0
                for k in 0 .. per.Length - 1 do
                    let c = cs.[k]
                    for i in 0 .. c - 1 do out.[v * vertexStride + o + i] <- per.[k].[v * c + i]
                    o <- o + c
            out
        let vtxBufs = attribs |> Array.map (fun m -> ArrayBuffer (buildObject m) :> IBuffer)

        // rewrite each vertex-input read into a flat-buffer gather, typed per the layout
        let eff =
            effect |> Effect.map (fun s ->
                s |> Shader.substituteReads (fun kind _ name _ _ ->
                    match kind with
                    | ParameterKind.Input ->
                        match Map.tryFind name fieldByName with
                        | Some (typ, off) -> Some (bindlessGather typ vertexStride off)
                        | None -> None
                    | _ -> None))

        // INDEXED indirect multidraw: one combined index buffer (per-object LOCAL
        // 0-based indices concatenated, real type preserved for uint16/uint32), each
        // sub-draw sliced via FirstIndex and handle-routed via FirstInstance = di.
        let idxType = indices.[0].GetType().GetElementType()
        let combinedIdx = concatArrays indices idxType
        let mutable firstIndex = 0
        let entries =
            Array.init n (fun di ->
                let cnt = indices.[di].Length
                let e = DrawCallInfo(FaceVertexCount = cnt, FirstIndex = firstIndex, BaseVertex = 0, FirstInstance = di, InstanceCount = 1)
                firstIndex <- firstIndex + cnt
                e)
        let indirect = IndirectBuffer.ofArray entries
        let idxBV = BufferView(AVal.constant (ArrayBuffer combinedIdx :> IBuffer), idxType)

        Sg.indirectDraw mode (AVal.constant indirect)
        |> Sg.indexBuffer idxBV
        // the SAME per-object buffers, bound as both a float and an int view, so the
        // shader decodes float attributes via HeapVertexData and integral ones via
        // HeapVertexDataI (identical bytes).
        |> Sg.uniform "HeapVertexData"  (AVal.constant vtxBufs)
        |> Sg.uniform "HeapVertexDataI" (AVal.constant vtxBufs)
        // ViewProjTrafo is a built-in camera semantic — provide it through the camera
        // path (ambient), NOT Sg.uniform "ViewProjTrafo" (which the default identity
        // shadows when no camera is in scope).
        |> Sg.viewTrafo view
        |> Sg.projTrafo proj
        |> Sg.effect [ eff ]

    // ── Heap-local chain emission (authoring) ───────────────────────────
    // A lightweight transform tree, OWNED by the heap (no core Aardvark.SceneGraph
    // semantics touched). `Trafo` scopes are shared by aval identity — author one
    // `cval<Trafo3d>` over many children and it dedups to one link slot, so
    // animating it is O(1). `flattenChains` produces the per-leaf root→leaf chains
    // that `derivedChainFp64` composes on the GPU.
    type ChainNode =
        /// a (possibly shared) transform scope wrapping children
        | Trafo of aval<Trafo3d> * ChainNode list
        /// a renderable leaf with its own local transform
        | Leaf  of aval<Trafo3d>

    /// Flatten a transform tree to one root→leaf chain per leaf (compose order,
    /// matching `Trafo3d` `*`). Adjacent CONSTANT links are folded into one so a
    /// run of static trafos costs a single slot; dynamic links keep their identity
    /// (shared scopes stay one slot across all leaves under them).
    let flattenChains (root : ChainNode) : aval<Trafo3d>[][] =
        // fold a built chain (root-first): collapse maximal runs of AVal.constant
        // links into a single constant, preserving order; keep dynamic links as-is.
        let foldConstants (links : aval<Trafo3d> list) : aval<Trafo3d>[] =
            let out = System.Collections.Generic.List<aval<Trafo3d>>()
            let mutable acc : Trafo3d voption = ValueNone
            let flush () = match acc with ValueSome t -> out.Add(AVal.constant t); acc <- ValueNone | ValueNone -> ()
            for l in links do
                if l.IsConstant then
                    let t = AVal.force l
                    acc <- ValueSome (match acc with ValueSome a -> a * t | ValueNone -> t)
                else flush (); out.Add l
            flush ()
            out.ToArray()
        let result = System.Collections.Generic.List<aval<Trafo3d>[]>()
        // acc is innermost-first; reverse at the leaf to get root→leaf order.
        let rec go (acc : aval<Trafo3d> list) (node : ChainNode) =
            match node with
            | Trafo(t, children) -> for c in children do go (t :: acc) c
            | Leaf lt -> result.Add(foldConstants (List.rev (lt :: acc)))
        go [] root
        result.ToArray()

    // ── Incremental scene (imperative Add/Remove) ───────────────────────
    // The streaming path: ONE bucket, shared geometry, fixed-size per-draw
    // slots in a growable dirty-tracked arena. Add/Remove are O(1) (free-list
    // + dead indirect entry), no header buffer (offset = slot*stride + field).
    // Call Add/Remove inside `transact` (the framework marks buffers there —
    // same convention as ManagedBuffer; can't mark inside an adaptive eval).

    /// An incrementally-mutable single-bucket heap scene: Add/Remove draws at
    /// runtime (O(1), free-list slots in a growable arena). Call Add/Remove
    /// inside `transact`.
    type HeapScene(runtime : IRuntime, effect : Effect, mode : IndexedGeometryMode,
                   positions : V3f[], normals : V3f[], index : int[],
                   schema : (string * System.Type)[], globals : IUniformProvider) =

        do checkSupport false runtime

        // fixed layout from the schema
        let fieldOffset = System.Collections.Generic.Dictionary<string, int>()
        let packerOf = System.Collections.Generic.Dictionary<string, int * (obj -> float32[] -> int -> unit)>()
        let dataStride =
            let mutable o = 0
            for (n, t) in schema do
                let (sz, pk) = packerFor t
                fieldOffset.[n] <- o
                packerOf.[n] <- (sz, pk)
                o <- o + sz
            o

        // header-less rewrite: uniform -> HeapData[ slot*stride + fieldOffset ],
        // slot routed by either:
        //   * gl_DrawID (= sub-draw position = slot) — GL 4.6+ and real Vulkan;
        //     each DrawCallInfo keeps FirstInstance=0.
        //   * gl_InstanceIndex + per-draw FirstInstance=slot — MoltenVK fallback
        //     (MSL has no DrawIndex). Each sub-draw still has InstanceCount=1, so
        //     Metal's [[base_instance]] simply offsets the vertex fetch and the
        //     shader reads slot from gl_InstanceIndex.
        let useDrawId = runtime.SupportsMultiDrawIndirectDrawId
        let effect' =
            let slotE : Expr<int> =
                if useDrawId then <@ getDrawId() @>
                else Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.InstanceId)
            effect |> Effect.substituteUniforms (fun name typ _ _ ->
                match fieldOffset.TryGetValue name with
                | true, fo -> Some (gatherFor typ <@ %slotE * %(cint dataStride) + %(cint fo) @>)
                | _ -> None)

        let initialSlots = 64
        let arena = HeapArena(runtime, dataStride * initialSlots)
        let freeList = System.Collections.Generic.Stack<int>()
        let mutable highWater = 0
        let slotWriters = System.Collections.Generic.Dictionary<int, RegionWriter[]>()
        let mutable entries : DrawCallInfo[] = Array.zeroCreate initialSlots
        let version = AVal.init 0

        // `entries` / `highWater` are read on the render thread (indirectAval)
        // and mutated by the caller's thread (Add/Remove) -> guard with a gate.
        let gate = obj()
        let bv (arr : System.Array) t = BufferView(AVal.constant (ArrayBuffer(arr) :> IBuffer), t)
        let indirectAval = version |> AVal.map (fun _ -> lock gate (fun () -> IndirectBuffer.ofArray (Array.sub entries 0 highWater)))
        // acquisition-propagating (see IncrementalBucket): the render task's
        // Release of the HeapData binding destroys the arena's backend buffer.
        let heapDataU = ((arena :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)) :> IAdaptiveValue
        let symHeap = Symbol.Create "HeapData"

        let ro = RenderObject()
        do
            ro.Surface          <- Surface.Effect effect'
            ro.Mode             <- mode
            ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>
                                                              DefaultSemantic.Normals,   bv normals   typeof<V3f> ]
            ro.Indices          <- Some (bv index typeof<int>)
            ro.DrawCalls        <- DrawCalls.Indirect indirectAval
            ro.Uniforms <-
                { new IUniformProvider with
                    member _.TryGetUniform(s, name) =
                        if name = symHeap then ValueSome heapDataU else globals.TryGetUniform(s, name)
                    member _.Dispose() = () }

        let ensure (slot : int) =
            if slot >= entries.Length then
                let n = Fun.NextPowerOfTwo (slot + 1)
                let ne = Array.zeroCreate n
                System.Array.Copy(entries, ne, entries.Length)
                entries <- ne
            arena.EnsureFloats ((slot + 1) * dataStride)

        /// Add a draw with the given per-draw uniform values. Call in transact.
        member _.Add(uniforms : Map<string, IAdaptiveValue>) : int =
            lock gate (fun () ->
                let slot = if freeList.Count > 0 then freeList.Pop() else let s = highWater in highWater <- highWater + 1; s
                ensure slot
                let ws =
                    schema |> Array.map (fun (n, _) ->
                        let (sz, pk) = packerOf.[n]
                        arena.Add(uniforms.[n], slot * dataStride + fieldOffset.[n], sz, pk))
                slotWriters.[slot] <- ws
                // FirstInstance: 0 on the gl_DrawID path; = slot on the MoltenVK
                // gl_InstanceIndex fallback (so [[base_instance]] offsets the slot).
                let firstInstance = if useDrawId then 0 else slot
                entries.[slot] <- DrawCallInfo(FaceVertexCount = index.Length, FirstIndex = 0, BaseVertex = 0, FirstInstance = firstInstance, InstanceCount = 1)
                arena.Touch()
                version.Value <- version.Value + 1
                slot)

        /// Remove a previously added draw. Call in transact. Idempotent: removing
        /// an unknown / already-removed slot is a no-op (it must NOT be pushed onto
        /// the free list again, or two later Adds would share one slot).
        member _.Remove(slot : int) =
            lock gate (fun () ->
                match slotWriters.TryGetValue slot with
                | true, ws ->
                    for w in ws do arena.Remove w
                    slotWriters.Remove slot |> ignore
                    if slot < entries.Length then
                        entries.[slot] <- DrawCallInfo(FaceVertexCount = 0, FirstInstance = 0, InstanceCount = 0)
                    freeList.Push slot
                    version.Value <- version.Value + 1
                | _ -> ())

        member _.Count = slotWriters.Count
        member _.RenderObject = ro :> IRenderObject
        member x.Sg = Sg.renderObjectSet (ASet.single x.RenderObject)

        /// Release every slot's region writers (and with them their Acquire on the
        /// per-draw source avals). Call in transact, after (or while) removing the
        /// scene from rendering — the indirect buffer collapses to zero draws. The
        /// arena's GPU buffer itself is freed by the render task that acquired it
        /// (AdaptiveResource refcounting) once the scene's RenderObject is dropped.
        member _.Dispose() =
            lock gate (fun () ->
                for KeyValue(_, ws) in slotWriters do
                    for w in ws do arena.Remove w
                slotWriters.Clear()
                freeList.Clear()
                highWater <- 0
                version.Value <- version.Value + 1)

        interface System.IDisposable with
            member x.Dispose() = x.Dispose()


// ── Sg.heap — scene-graph node for Heap.ofRenderObjects ─────────────────────
// Collapses the subtree's render objects through `Heap.ofRenderObjects`: the
// child's RenderObjects set is piped through the heap transform with the
// traversal's runtime. Non-heapable ROs pass through unchanged (that is
// `ofRenderObjects`' own behaviour), so a mixed subtree degrades gracefully.
//
// Dual-protocol like every Sg node (mirrors GeometrySetNode):
//   * Ag path        — RenderObjects rule (HeapApplicatorSem below); the runtime
//     comes from the ambient `Runtime` attribute (`scope.Runtime`, seeded by
//     `app?Runtime <- runtime` in RuntimeExtensions.toRenderObjects).
//   * ISimpleSg path — GetRenderObjects; the runtime comes from the explicit
//     TraversalState (`ts.Runtime`, seeded by TraversalState.withRuntime in the
//     CompileRender entry point).
[<AutoOpen>]
module HeapSgExtensions =
    open Aardvark.SceneGraph.Simple

    module Sg =
        type HeapApplicator(child : aval<ISg>) =
            inherit Sg.AbstractApplicator(child)

            // TS-direct — child ROs gathered with the unchanged TraversalState,
            // then collapsed with the TS's runtime.
            interface ISimpleSg with
                member _.GetRenderObjects ts =
                    child
                    |> ASet.bind (fun c -> SimpleDispatch.Get(c, ts))
                    |> Heap.ofRenderObjects ts.Runtime

            new(child : ISg) = HeapApplicator(AVal.constant child)

        /// Collapses the subtree's render objects through `Heap.ofRenderObjects`
        /// (N per-object draws -> one indirect multidraw per bucket). Non-heapable
        /// render objects pass through unchanged.
        let heap (sg : ISg) : ISg = HeapApplicator(sg) :> ISg


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
        // collapse with the scope's ambient runtime. This concrete-type rule wins
        // over the IApplicator rule (most-specific dispatch — cf. NaiveLod.LodSem).
        member x.RenderObjects(h : Sg.HeapApplicator, scope : Ag.Scope) : aset<IRenderObject> =
            let runtime = scope.Runtime
            aset {
                let! c = h.Child
                yield! c.RenderObjects(scope)
            }
            |> Heap.ofRenderObjects runtime
