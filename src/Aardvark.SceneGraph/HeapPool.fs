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
//     transform; buckets by effect, packs shared/varied geometry, dirty-tracks
//     the arena (sparse per-frame mutation uploads only changed sub-ranges).
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
        member x.HeapHeaders : int[]     = uniform?StorageBuffer?HeapHeaders
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

/// Opt-in for the heap runtime. Default OFF — apps that want heap-based
/// rendering must set `HeapConfig.Enabled <- true` at startup. While disabled,
/// `Heap.ofRenderObjects` and `HeapScene` throw, so an accidental call doesn't
/// silently change rendering behaviour.
[<RequireQualifiedAccess>]
module HeapConfig =
    let mutable Enabled : bool = false

    let internal requireEnabled (caller : string) =
        if not Enabled then
            failwithf
                "[Aardvark.SceneGraph] %s requires the heap runtime. \
                 Set HeapConfig.Enabled <- true at startup to opt in."
                caller

module Heap =

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
            "ModelViewProjTrafo", <@ (uniform?ViewProjTrafo : M44f) * (uniform?ModelTrafo : M44f) @>.Raw
            "ModelViewTrafo",     <@ (uniform?ViewTrafo     : M44f) * (uniform?ModelTrafo : M44f) @>.Raw
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

        let effect = rewrite (<@ getDrawId() @>) nameToField fieldStride standardDerivedRules effect

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
        let effect = rewrite (Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.InstanceId)) nameToField fieldStride standardDerivedRules effect
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
    // bucket; per-draw heap uniforms named in `heapNames` are present on
    // every RO with a consistent type. Globals (camera etc.) are delegated
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
        elif t = typeof<float32> then 1,  (fun o a off -> a.[off] <- (o :?> float32))
        elif t = typeof<float>   then 1,  (fun o a off -> a.[off] <- float32 (o :?> float))
        elif t = typeof<int>     then 1,  (fun o a off -> a.[off] <- float32 (o :?> int))
        else failwithf "Heap: unsupported per-draw uniform content type %A" t

    /// Size in bytes of a blittable attribute/index element type (-1 if it isn't
    /// blittable — such an RO is then treated as un-heapable and passed through).
    let private elemSize (t : System.Type) =
        try System.Runtime.InteropServices.Marshal.SizeOf t with _ -> -1

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

    /// Build a packed vertex/index buffer view of an arbitrary element type from
    /// raw bytes (the backend derives the vertex/index format from `et`, so no
    /// shader-side decoding is needed for standard formats).
    let private packedView (bytes : byte[]) (et : System.Type) : BufferView =
        let n = bytes.Length / max 1 (elemSize et)
        let a = System.Array.CreateInstance(et, n)
        let gc = System.Runtime.InteropServices.GCHandle.Alloc(a, System.Runtime.InteropServices.GCHandleType.Pinned)
        try System.Runtime.InteropServices.Marshal.Copy(bytes, 0, gc.AddrOfPinnedObject(), bytes.Length)
        finally gc.Free()
        BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), et)

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
    /// heapable RO on the AUTO-DETECT path (diagnostic / for tests). Sorted.
    let mutable lastAutoFields : string[] = [||]

    /// Force the texture-atlas path even where descriptor-indexed sampler arrays ARE
    /// available (for testing the atlas on desktop Vulkan, which reports them supported).
    let mutable forceAtlas = false

    /// Combined packed geometry bytes (indices + attributes) of the most recently
    /// geometry-flushed bucket (diagnostic). Under exact-size distinct-geometry
    /// churn this stays FLAT: a freed geometry's ranges are recycled in place.
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
            if dirty.Count > 0 || full > 0 then
                let ranges = System.Collections.Generic.List<struct(int * int)>(dirty.Count + 1)
                for w in dirty do
                    w.Pack(t, staging)
                    ranges.Add(struct(w.Off, w.Off + w.Size))
                if full > 0 then ranges.Add(struct(0, full))
                ranges.Sort(fun (struct(a, _)) (struct(b, _)) -> compare a b)
                let flush lo hi = x.Write(staging, uint64 (lo * 4), lo, hi - lo, false)
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
    /// the actual storage lives in the ByteStores / staging mirrors, which the
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

    /// Growable byte store for the packed combined geometry buffers (replaces
    /// List&lt;byte&gt;): supports in-place writes at arbitrary offsets (allocator-
    /// placed ranges) and explicit logical-length control so every attribute
    /// store tracks the shared vertex cursor exactly.
    type internal ByteStore(initialBytes : int) =
        let mutable data = Array.zeroCreate<byte> (max 16 (Fun.NextPowerOfTwo (max 1 initialBytes)))
        let mutable count = 0
        member _.Data = data
        member _.Count = count
        member _.EnsureCount(n : int) =
            if n > data.Length then
                let nd = Array.zeroCreate<byte> (Fun.NextPowerOfTwo n)
                System.Array.Copy(data, nd, count)
                data <- nd
            if n > count then count <- n
        member x.WriteAt(off : int, src : byte[]) =
            x.EnsureCount(off + src.Length)
            System.Array.Copy(src, 0, data, off, src.Length)
        member x.WriteAt(off : int, src : byte[], srcOff : int, len : int) =
            x.EnsureCount(off + len)
            System.Array.Copy(src, srcOff, data, off, len)
        member x.ZeroFill(off : int, len : int) =
            if len > 0 then
                x.EnsureCount(off + len)
                System.Array.Clear(data, off, len)

    /// Mutable refcounted arena region (deduped by source-aval identity).
    /// Offset is re-seated by arena compaction. Block is the region's float
    /// range in the arena HeapSpace (re-allocated on compaction).
    type internal RegionEntry =
        { mutable Offset : int; Size : int; Writer : RegionWriter; mutable RefCount : int
          mutable Block : Management.Block<unit> }

    /// Refcounted per-geometry ranges in a bucket's combined buffers (geometries
    /// are deduped by buffer identity). Host geometry owns the vertex range
    /// [BaseVertex, BaseVertex+VtxAlloc) in every packed attribute plus an index
    /// range; bindless geometry only the index range (VtxAlloc = 0). When the
    /// last referencing slot dies the ranges (held as HeapSpace blocks) return
    /// to the bucket's allocators; compaction re-seats FirstIndex/BaseVertex
    /// (and re-allocs the blocks) of live entries. VtxCount is the UNIFORM
    /// vertex count (0 for ragged inputs whose attributes disagree — VtxAlloc
    /// then covers the longest attribute, so the range is still safely
    /// reusable).
    type internal GeomEntry =
        { mutable FirstIndex : int; mutable BaseVertex : int; IndexCount : int
          VtxCount : int; VtxAlloc : int; mutable RefCount : int
          mutable IdxBlock : Management.Block<unit>; mutable VtxBlock : Management.Block<unit> }

    /// Per-member bookkeeping of an incremental bucket: the draw-record slot,
    /// the arena regions it references, its visibility gate, its (structural)
    /// instance count, the identity key of its packed geometry (for refcounted
    /// range reclamation) and — on the MoltenVK slot-attribute path — the offset
    /// of its per-instance range in the slot-attribute buffer (re-seated by
    /// compaction; InstBlock is the backing HeapSpace block).
    type internal HeapSlot =
        { Slot : int; RegionKeys : IAdaptiveValue[]; Active : aval<bool>
          Instances : int; mutable InstOffset : int
          mutable InstBlock : Management.Block<unit>; GeomKey : struct(obj * obj) }

    /// Immutable per-RO facts (STRUCTURE only — surface, geometry layout, uniform
    /// presence; never aval VALUES). Cached per RO in a ConditionalWeakTable so a
    /// membership diff doesn't re-derive them (isHeapable + layoutSig over 20k ROs
    /// per change would dominate the frame). ConstToken is the interned bucket key
    /// when ALL the RO's pipeline-state avals are constant (the common case), null
    /// when any is dynamic (then the key is re-read through the token every run).
    /// Bindless (GPU-resident geometry -> vertex-pull) and Instanced
    /// (InstanceCount > 1) are PART of the bucket key (folded into Layout), so a
    /// bucket's geometry strategy and slot routing are fixed at creation.
    type internal RoFacts =
        { Heapable : bool; Layout : string; ConstToken : obj
          Bindless : bool; Instanced : bool
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

    let private isIntegral (t : System.Type) : bool =
        t = typeof<int> || t = typeof<V2i> || t = typeof<V3i> || t = typeof<V4i>

    /// vertex-pull gather for ofRenderObjects' GPU-geometry buckets: object `slot`'s
    /// attribute `ai` lives at HeapVertexData[slot*numAttrs + ai] — an object-major
    /// flatten of the objects' EXISTING GPU buffers (no copy). Decodes `typ` at element
    /// (vid*strideF + offF); strideF/offF (in floats) come from the BufferView so both
    /// separate-tight and interleaved buffers work. Integral types use the int view.
    /// `handleE` is the per-draw handle expr (gl_InstanceIndex on Vulkan, gl_DrawID on GL).
    let private bindlessGatherFlat (handleE : Expr) (typ : System.Type) (numAttrs : int) (ai : int) (strideF : int) (offF : int) : Expr =
        if typ = typeof<float32> then
            <@@ let b = (%%handleE : int) * numAttrs + ai in uniform.HeapVertexData.[b].[ (%%vidExpr : int) * strideF + offF ] @@>
        elif typ = typeof<V2f> then
            <@@ let b = (%%handleE : int) * numAttrs + ai
                let o = (%%vidExpr : int) * strideF + offF
                V2f(uniform.HeapVertexData.[b].[o], uniform.HeapVertexData.[b].[o+1]) @@>
        elif typ = typeof<V3f> then
            <@@ let b = (%%handleE : int) * numAttrs + ai
                let o = (%%vidExpr : int) * strideF + offF
                V3f(uniform.HeapVertexData.[b].[o], uniform.HeapVertexData.[b].[o+1], uniform.HeapVertexData.[b].[o+2]) @@>
        elif typ = typeof<V4f> then
            <@@ let b = (%%handleE : int) * numAttrs + ai
                let o = (%%vidExpr : int) * strideF + offF
                V4f(uniform.HeapVertexData.[b].[o], uniform.HeapVertexData.[b].[o+1], uniform.HeapVertexData.[b].[o+2], uniform.HeapVertexData.[b].[o+3]) @@>
        elif typ = typeof<int> then
            <@@ let b = (%%handleE : int) * numAttrs + ai in uniform.HeapVertexDataI.[b].[ (%%vidExpr : int) * strideF + offF ] @@>
        elif typ = typeof<V2i> then
            <@@ let b = (%%handleE : int) * numAttrs + ai
                let o = (%%vidExpr : int) * strideF + offF
                V2i(uniform.HeapVertexDataI.[b].[o], uniform.HeapVertexDataI.[b].[o+1]) @@>
        elif typ = typeof<V3i> then
            <@@ let b = (%%handleE : int) * numAttrs + ai
                let o = (%%vidExpr : int) * strideF + offF
                V3i(uniform.HeapVertexDataI.[b].[o], uniform.HeapVertexDataI.[b].[o+1], uniform.HeapVertexDataI.[b].[o+2]) @@>
        else
            <@@ let b = (%%handleE : int) * numAttrs + ai
                let o = (%%vidExpr : int) * strideF + offF
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

    /// Persistent state of ONE bucket — host OR bindless (vertex-pull) geometry,
    /// untextured / bindless-textured / atlas-textured, instanced or not. The
    /// geometry class and instanced-ness are part of the bucket key, so each
    /// bucket's strategy and slot routing are fixed at creation. Set-membership
    /// changes mutate slots / regions / packed geometry / texture tables IN PLACE
    /// (O(changed)) instead of rebuilding the bucket, and the bucket's
    /// RenderObject is created ONCE so its identity is stable across changes (the
    /// render task never recompiles it; only its indirect / header / geometry /
    /// texture resources update).
    type internal IncrementalBucket(runtime : IRuntime, names : string[], nameToField : Map<string, int>,
                                    effect : Effect, ro0 : RenderObject, updater : aval<int>,
                                    useBindlessGeom : bool, instanced : bool,
                                    // the bucket KEY's resolved pipeline-state values
                                    // (cull, frontFacing, fill, blend, depthTest, depthWrite)
                                    pipeKey : CullMode * WindingOrder * FillMode * BlendMode * DepthTest * bool) =
        let fieldStride = names.Length
        let scope = Ag.Scope.Root
        let symData = Symbol.Create "HeapData"
        let symHeaders = Symbol.Create "HeapHeaders"
        let nameSyms = names |> Array.map Symbol.Create
        let heapSyms = System.Collections.Generic.HashSet<Symbol>(nameSyms)

        // ── sampler structure (a function of the EFFECT + runtime, not of the
        //    membership — every member shares the effect via the bucket key) ──
        let samplers = samplerUniforms effect           // (name, texName, type, state)[]
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

        // fixed geometry layout from ro0 (the bucket key includes layoutSig, so
        // every member shares element types / offsets / strides):
        // (ai, name, sym, elementType, elemSize, strideF, offF)
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
        let idxType = match ro0.Indices with Some bv -> bv.ElementType | None -> failwith "Heap.ofRenderObjects: heapable RO must be indexed"
        let idxSize = elemSize idxType

        // ── arena: deduped per-draw uniform regions, refcounted, placed by a
        //    coalescing range allocator (float units) ──
        let arena = HeapArena(runtime, 1024)
        do arena.ExtraDependency <- Some (updater :> IAdaptiveValue)
        let regions = System.Collections.Generic.Dictionary<IAdaptiveValue, RegionEntry>(HashIdentity.Reference)
        let arenaAlloc = HeapSpace()

        // ── geometry. Host buckets: packed byte stores (attributes + indices),
        //    deduped by buffer identity. Bindless buckets: only the combined
        //    LOCAL index buffer is packed (deduped per geometry); the objects'
        //    EXISTING vertex buffers are bound (no copy) into the per-slot
        //    HeapVertexData array. Geometry entries are REFCOUNTED: when the
        //    last referencing slot dies its vertex/index ranges return to the
        //    coalescing allocators (merged with free neighbors, split on reuse),
        //    so a freed 100-vertex range can serve a later 60-vertex geometry.
        //    Residual fragmentation is bounded by threshold-triggered compaction
        //    (see maybeCompact below). ──
        let packedAttr = if useBindlessGeom then [||] else attrInfos |> Array.map (fun _ -> ByteStore 16)
        let mutable packedIdx = ByteStore 16
        let geomCache = System.Collections.Generic.Dictionary<struct(obj * obj), GeomEntry>(HashIdentity.Structural)
        let idxAlloc = HeapSpace()                      // units: indices
        let vtxAlloc = HeapSpace()                      // units: vertices (shared by ALL attribute stores)
        // bytes one vertex occupies across all packed attribute stores (host only)
        let bytesPerVertex = if useBindlessGeom then 0 else attrInfos |> Array.sumBy (fun (_, _, _, _, es, _, _) -> es)
        let mutable geomDirty = false
        let toTyped (bytes : ByteStore) (et : System.Type) (es : int) : IBuffer =
            let n = bytes.Count / max 1 es
            let a = System.Array.CreateInstance(et, n)
            if bytes.Count > 0 then
                let gc = System.Runtime.InteropServices.GCHandle.Alloc(a, System.Runtime.InteropServices.GCHandleType.Pinned)
                try System.Runtime.InteropServices.Marshal.Copy(bytes.Data, 0, gc.AddrOfPinnedObject(), bytes.Count)
                finally gc.Free()
            ArrayBuffer a :> IBuffer
        // CURRENT combined buffers; replaced by FRESH ArrayBuffers only when the
        // geometry grew (otherwise the SAME instance is returned and the resource
        // layer skips the upload — MutableResourceLocation compares values).
        let mutable attrBuffers : IBuffer[] =
            if useBindlessGeom then [||]
            else attrInfos |> Array.map (fun (_, _, _, et, _, _, _) -> ArrayBuffer (System.Array.CreateInstance(et, 0)) :> IBuffer)
        let mutable idxBuffer : IBuffer = ArrayBuffer (System.Array.CreateInstance(idxType, 0)) :> IBuffer
        // bindless: per-(slot, attribute) source buffer avals + the last buffer
        // each position yielded. Tombstoned slots null their aval but KEEP the
        // last buffer — never read (their draw record is InstanceCount = 0), but
        // the SSBO array cell must stay bound to a live buffer.
        let mutable vtxAvals : aval<IBuffer>[] = if useBindlessGeom then Array.zeroCreate (16 * max 1 numAttrs) else [||]
        let mutable vtxLast : IBuffer[] = if useBindlessGeom then Array.zeroCreate (16 * max 1 numAttrs) else [||]

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

        // ── draw records + headers: slot-indexed, growable, free-listed ──
        let mutable entries : DrawCallInfo[] = Array.zeroCreate 16
        let mutable headers : int[] = Array.zeroCreate (16 * max 1 fieldStride)
        let freeSlots = System.Collections.Generic.Stack<int>()
        let mutable highWater = 0
        let slots = System.Collections.Generic.Dictionary<RenderObject, HeapSlot>(HashIdentity.Reference)
        // globals fall-through OWNER: the bucket RO answers the heap/sampler names
        // itself; everything else (camera, lights, scene-scope globals) falls
        // through to a LIVE member's uniform provider. Tracked so the bucket never
        // retains a member that left the set: globals are resolved at COMPILE time
        // of the bucket RO from scene scope (bucket-homogeneous for heapable ROs),
        // so switching the owner is purely a GC measure — already-compiled bindings
        // keep the avals they resolved. A bucket emptied by removals keeps the last
        // owner only until it is disposed (the incremental driver disposes empty
        // buckets in the same update) or refilled (AddInternal re-seats the owner).
        let mutable globalsRO = ro0
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
                let nh = Array.zeroCreate<int> (n * max 1 fieldStride)
                System.Array.Copy(headers, nh, headers.Length)
                headers <- nh
                if useBindlessGeom then
                    let nv = Array.zeroCreate<aval<IBuffer>> (n * max 1 numAttrs)
                    System.Array.Copy(vtxAvals, nv, vtxAvals.Length)
                    vtxAvals <- nv
                    let nl = Array.zeroCreate<IBuffer> (n * max 1 numAttrs)
                    System.Array.Copy(vtxLast, nl, vtxLast.Length)
                    vtxLast <- nl

        let allocRegion (av : IAdaptiveValue) : int =
            match regions.TryGetValue av with
            | true, e -> e.RefCount <- e.RefCount + 1; e.Offset
            | _ ->
                let (sz, pk) = packerFor av.ContentType
                let b = arenaAlloc.Alloc sz
                let off = int b.Offset
                // grows only the staging mirror; the GPU resize is deferred to the
                // arena's own Compute (which depends on the updater whose
                // evaluation we are inside) — no transact happens here.
                arena.EnsureFloats arenaAlloc.Extent
                let w = arena.Add(av, off, sz, pk)
                regions.[av] <- { Offset = off; Size = sz; Writer = w; RefCount = 1; Block = b }
                off

        let freeRegion (av : IAdaptiveValue) =
            match regions.TryGetValue av with
            | true, e ->
                e.RefCount <- e.RefCount - 1
                if e.RefCount = 0 then
                    arena.Remove e.Writer
                    regions.Remove av |> ignore
                    arenaAlloc.Free e.Block
            | _ -> ()

        /// place `bytes` (`count` index elements) into the combined index buffer:
        /// allocator-placed (reused/split free range, else high-water growth).
        /// Returns the block (element offset = Block.Offset).
        let placeIdx (bytes : byte[]) (count : int) : Management.Block<unit> =
            let b = idxAlloc.Alloc count
            packedIdx.EnsureCount (idxAlloc.Extent * idxSize)
            packedIdx.WriteAt(int b.Offset * idxSize, bytes)
            b

        /// host geometry: pack the RO's attributes + indices into the combined
        /// buffers (deduped by buffer identity; refcounted; ranges placed by the
        /// coalescing allocators).
        let geomFor (key : struct(obj * obj)) (ro : RenderObject) : GeomEntry =
            match geomCache.TryGetValue key with
            | true, e -> e.RefCount <- e.RefCount + 1; e
            | _ ->
                let idxBV = match ro.Indices with Some bv -> bv | None -> failwith "Heap.ofRenderObjects: RO has no index buffer"
                let ib = readBytesView idxBV
                let thisIdx = ib.Length / idxSize
                let idxBlock = placeIdx ib thisIdx
                let firstIndex = int idxBlock.Offset
                let attrBytes =
                    attrInfos |> Array.map (fun (_, _, sym, _, _, _, _) ->
                        match ro.VertexAttributes.TryGetAttribute sym with
                        | ValueSome b -> readBytesView b
                        | ValueNone -> failwithf "Heap.ofRenderObjects: RO missing shader input attribute %A" sym)
                let thisVtx =
                    let (_, _, _, _, es0, _, _) = attrInfos.[0]
                    attrBytes.[0].Length / es0
                // ragged inputs (attributes disagreeing on vertex count) allocate
                // the LONGEST attribute's vertex count, so every attribute fits in
                // its own range and can never overwrite a neighboring geometry —
                // which also makes ragged ranges safely reusable/compactable
                // (historically they were append-only and never reclaimed).
                let uniformCounts =
                    let mutable ok = true
                    attrInfos |> Array.iteri (fun i (_, _, _, _, es, _, _) ->
                        if attrBytes.[i].Length <> thisVtx * es then ok <- false)
                    ok
                let vtxUnits =
                    if uniformCounts then thisVtx
                    else
                        let mutable mx = 0
                        attrInfos |> Array.iteri (fun i (_, _, _, _, es, _, _) ->
                            mx <- max mx ((attrBytes.[i].Length + es - 1) / es))
                        mx
                let vtxBlock = vtxAlloc.Alloc vtxUnits
                let baseVertex = int vtxBlock.Offset
                attrInfos |> Array.iteri (fun i (ai, _, _, _, es, _, _) ->
                    let store = packedAttr.[ai]
                    store.EnsureCount (vtxAlloc.Extent * es)
                    store.WriteAt(baseVertex * es, attrBytes.[i])
                    // zero the ragged tail padding (deterministic content for the
                    // reuse path — a fresh build would have zeros there too)
                    store.ZeroFill(baseVertex * es + attrBytes.[i].Length, vtxUnits * es - attrBytes.[i].Length))
                geomDirty <- true
                let e = { FirstIndex = firstIndex; BaseVertex = baseVertex; IndexCount = thisIdx
                          VtxCount = (if uniformCounts then thisVtx else 0); VtxAlloc = vtxUnits; RefCount = 1
                          IdxBlock = idxBlock; VtxBlock = vtxBlock }
                geomCache.[key] <- e
                e

        /// bindless geometry: pack the RO's LOCAL index bytes into the combined
        /// index buffer (deduped by index-buffer identity — indices are local, so
        /// shared index data is shared verbatim; refcounted, exact-size reuse).
        let idxFor (key : struct(obj * obj)) (ro : RenderObject) : GeomEntry =
            match geomCache.TryGetValue key with
            | true, e -> e.RefCount <- e.RefCount + 1; e
            | _ ->
                let ibv = match ro.Indices with Some b -> b | None -> failwith "Heap.ofRenderObjects: RO has no index buffer"
                let ib = readGeomBytes runtime ibv
                let cnt = ib.Length / idxSize
                let b = placeIdx ib cnt
                geomDirty <- true
                let e = { FirstIndex = int b.Offset; BaseVertex = 0; IndexCount = cnt; VtxCount = 0; VtxAlloc = 0; RefCount = 1
                          IdxBlock = b; VtxBlock = null }
                geomCache.[key] <- e
                e

        /// identity key of an RO's geometry in `geomCache`. Host: (first attribute
        /// buffer aval, index buffer aval) — geometries sharing those are assumed to
        /// share ALL attribute buffers (they come from one IndexedGeometry). Bindless:
        /// (index buffer aval, boxed byte offset).
        let geomKeyOf (ro : RenderObject) : struct(obj * obj) =
            let idxBV = match ro.Indices with Some bv -> bv | None -> failwith "Heap.ofRenderObjects: RO has no index buffer"
            if useBindlessGeom then struct(idxBV.Buffer :> obj, box idxBV.Offset)
            else
                let firstAttr =
                    let (_, _, sym, _, _, _, _) = attrInfos.[0]
                    match ro.VertexAttributes.TryGetAttribute sym with
                    | ValueSome b -> b.Buffer :> obj
                    | ValueNone -> null
                struct(firstAttr, idxBV.Buffer :> obj)

        /// drop one reference to a packed geometry; the LAST reference returns its
        /// ranges to the coalescing allocators (the stale bytes stay in the
        /// combined buffers — never drawn, no live record references them — until
        /// reuse or compaction reclaims the range).
        let freeGeom (key : struct(obj * obj)) =
            match geomCache.TryGetValue key with
            | true, e ->
                e.RefCount <- e.RefCount - 1
                if e.RefCount = 0 then
                    geomCache.Remove key |> ignore
                    idxAlloc.Free e.IdxBlock
                    if e.VtxAlloc > 0 then vtxAlloc.Free e.VtxBlock
            | _ -> ()

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
        let fixGeomRecords () =
            // re-point every live slot's draw record at its (possibly re-seated)
            // geometry entry; tombstoned records are already all-zero.
            for KeyValue(_, s) in slots do
                match geomCache.TryGetValue s.GeomKey with
                | true, e ->
                    entries.[s.Slot].FirstIndex <- e.FirstIndex
                    entries.[s.Slot].BaseVertex <- e.BaseVertex
                | _ -> ()
            drawsAllDirty <- true

        let compactIdx () =
            let live = geomCache.Values |> Seq.toArray |> Array.sortBy (fun e -> e.FirstIndex)
            let fresh = ByteStore (idxAlloc.Live * idxSize)
            idxAlloc.Reset()
            // re-alloc in ascending old offset against the fresh space -> tight
            // ascending placement (the manager bump-splits its single free block)
            for e in live do
                let b = idxAlloc.Alloc e.IndexCount
                let off = int b.Offset
                fresh.WriteAt(off * idxSize, packedIdx.Data, e.FirstIndex * idxSize, e.IndexCount * idxSize)
                e.FirstIndex <- off
                e.IdxBlock <- b
            fresh.EnsureCount (idxAlloc.Extent * idxSize)
            packedIdx <- fresh
            geomDirty <- true
            compactionCount <- compactionCount + 1
            fixGeomRecords ()

        let compactVtx () =
            let live =
                geomCache.Values
                |> Seq.filter (fun e -> e.VtxAlloc > 0)
                |> Seq.toArray
                |> Array.sortBy (fun e -> e.BaseVertex)
            let liveUnits = vtxAlloc.Live
            let freshStores = attrInfos |> Array.map (fun (_, _, _, _, es, _, _) -> ByteStore (liveUnits * es))
            vtxAlloc.Reset()
            for e in live do
                let b = vtxAlloc.Alloc e.VtxAlloc
                let off = int b.Offset
                attrInfos |> Array.iteri (fun i (ai, _, _, _, es, _, _) ->
                    freshStores.[i].WriteAt(off * es, packedAttr.[ai].Data, e.BaseVertex * es, e.VtxAlloc * es))
                e.BaseVertex <- off
                e.VtxBlock <- b
            attrInfos |> Array.iteri (fun i (ai, _, _, _, es, _, _) ->
                freshStores.[i].EnsureCount (vtxAlloc.Extent * es)
                packedAttr.[ai] <- freshStores.[i])
            geomDirty <- true
            compactionCount <- compactionCount + 1
            fixGeomRecords ()

        let compactArena () =
            // re-seat regions in ascending old offset so the staging memmove is
            // front-to-back (new offset <= old offset, no overlap hazard) …
            let regs = regions.Values |> Seq.toArray |> Array.sortBy (fun e -> e.Offset)
            arenaAlloc.Reset()
            for e in regs do
                let b = arenaAlloc.Alloc e.Size
                let off = int b.Offset
                e.Block <- b
                if off <> e.Offset then
                    arena.MoveStaging(e.Offset, off, e.Size)
                    e.Offset <- off
                    e.Writer.Off <- off                 // future packs target the new offset
            // … then rewrite every live slot's baked header cells (the per-field
            // region offsets at slot*fieldStride + fi); the whole header table
            // re-uploads once this pass (headersAllDirty).
            for KeyValue(_, s) in slots do
                for i in 0 .. names.Length - 1 do
                    match regions.TryGetValue s.RegionKeys.[i] with
                    | true, e -> headers.[s.Slot * fieldStride + nameToField.[names.[i]]] <- e.Offset
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
            if need idxAlloc idxSize then compactIdx ()
            if not useBindlessGeom && need vtxAlloc bytesPerVertex then compactVtx ()
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
        //    are staged in INDEXED layout (VkDrawIndexedIndirectCommand /
        //    GL DrawElementsIndirectCommand: BaseVertex and FirstInstance
        //    swapped vs the DrawCallInfo struct) and the IndirectBuffer record
        //    carries Indexed = true, so BOTH backends bind the GPU buffer
        //    directly — no layout conversion, no per-version copy. Geometry
        //    returns the SAME buffer instance unless it grew, as before.
        let drawBuf    = MirrorBuffer(runtime, entries.Length * sizeof<DrawCallInfo>, BufferUsage.Indirect)
        let headersBuf = MirrorBuffer(runtime, headers.Length * 4, BufferUsage.Storage)
        let instBuf    = MirrorBuffer(runtime, instData.Length * 4, BufferUsage.Vertex)
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
            let inline stage (s : int) =
                let mutable c = entries.[s]
                DrawCallInfo.ToggleIndexed(&c)
                drawStaging.[s] <- c
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
                let n = highWater * fieldStride
                if n > 0 then headersBuf.Write(headers, 0UL, 0, n)
            elif dirtyHeaders.Count > 0 && fieldStride > 0 then
                let ss = System.Collections.Generic.List<int>(dirtyHeaders)
                dirtyHeaders.Clear()
                ss.Sort()
                // small gaps merge — `headers` is the always-valid source of
                // truth, so a gap's bytes re-upload unchanged (see flushDraws)
                let flush lo hi = headersBuf.Write(headers, uint64 (lo * fieldStride * 4), lo * fieldStride, (hi - lo + 1) * fieldStride)
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
                IndirectBuffer.ofBuffer true 0UL sizeof<DrawCallInfo> highWater (b :> IBuffer))
        let attrAvals = Array.init (if useBindlessGeom then 0 else numAttrs) (fun i -> updater |> AVal.map (fun _ -> attrBuffers.[i]))
        let idxAval = updater |> AVal.map (fun _ -> idxBuffer)
        let instAval = (instBuf :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)
        // bindless vertex-pull: object-major flatten of the slots' buffer avals
        // (HeapVertexData[slot*numAttrs + ai]). Depends on the updater version and
        // re-reads only the live slots' avals (cheap when unchanged); a fresh
        // array is produced per version, tombstoned positions keep the last
        // buffer so the SSBO array binding stays valid.
        let vtxGatherAval =
            AVal.custom (fun t ->
                updater.GetValue t |> ignore
                let n = highWater * numAttrs
                let out = Array.zeroCreate<IBuffer> (max 1 n)
                for pos in 0 .. n - 1 do
                    let av = vtxAvals.[pos]
                    if System.Object.ReferenceEquals(av, null) then out.[pos] <- vtxLast.[pos]
                    else
                        let b = av.GetValue t
                        vtxLast.[pos] <- b
                        out.[pos] <- b
                out)
        let arenaU = ((arena :> aval<IBackendBuffer>) |> AdaptiveResource.mapNonAdaptive (fun b -> b :> IBuffer)) :> IAdaptiveValue
        let headersU = headersAval :> IAdaptiveValue

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

        // bindless: rewrite vertex-input reads into per-handle gathers — ONLY in
        // the VERTEX stage (a later stage's input of the same semantic is an
        // interpolated varying and must keep its value; injecting the handle
        // there would be invalid). Uses the SHADER INPUT type so the gather
        // type matches.
        let geomRewrite : Effect -> Effect =
            if not useBindlessGeom then id
            else
                let handleE = slotE.Raw
                fun e ->
                    e |> Effect.map (fun s ->
                        if s.shaderStage <> ShaderStage.Vertex then s
                        else
                            s |> Shader.substituteReads (fun kind ityp name _ _ ->
                                match kind with
                                | ParameterKind.Input ->
                                    match attrInfos |> Array.tryFind (fun (_, n, _, _, _, _, _) -> n = name) with
                                    | Some (ai, _, _, _, _, strideF, offF) -> Some (bindlessGatherFlat handleE ityp numAttrs ai strideF offF)
                                    | None -> None
                                | _ -> None))

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
                let baseE = rewrite slotE nameToField fieldStride standardDerivedRules effect |> geomRewrite
                if useAtlas then Surface.Effect (baseE |> rewriteAtlasSamples slotE atlasByName)
                elif samplers.Length > 0 then Surface.Effect (baseE |> rewriteSamplers slotE samplerByName |> overrideSamplerStates samplerStateOverrides)
                else Surface.Effect baseE
            ro.DrawCalls <- DrawCalls.Indirect indirectAval
            ro.VertexAttributes <-
                if useBindlessGeom then AttributeProvider.ofList ([] : (Symbol * BufferView) list)
                else AttributeProvider.ofList [ for (ai, _, sym, et, _, _, _) in attrInfos -> sym, BufferView(attrAvals.[ai], et) ]
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
            ro.Indices <- Some (BufferView(idxAval, idxType))
            ro.Uniforms <-
                { new IUniformProvider with
                    member _.TryGetUniform(s, name) =
                        if name = symData then ValueSome arenaU
                        elif name = symHeaders then ValueSome headersU
                        else
                            match texLookup.TryGetValue name with
                            | true, v -> ValueSome v
                            | _ ->
                                if heapSyms.Contains name then ValueNone
                                elif samplerSyms.Contains name then ValueNone
                                else globalsRO.Uniforms.TryGetUniform(s, name)
                    member _.Dispose() = () }
            ro

        member _.RenderObject = bucketRO :> IRenderObject
        member _.Count = slots.Count

        /// flush geometry changes: FRESH ArrayBuffers (full re-upload, amortized —
        /// geometry changes only when an unseen geometry was added, whether
        /// appended or written into a reclaimed range, or when a compaction
        /// rewrote the packed bytes). The instance-slot buffer needs no flush
        /// here: instDirty ranges upload on the inst mirror's next pull.
        member private _.FlushGeometry() =
            if geomDirty then
                geomDirty <- false
                if not useBindlessGeom then
                    attrBuffers <- attrInfos |> Array.map (fun (ai, _, _, et, es, _, _) -> toTyped packedAttr.[ai] et es)
                idxBuffer <- toTyped packedIdx idxType idxSize
            // footprint diagnostics (cheap; published every update)
            lastPackedGeomBytes <- packedIdx.Count + (packedAttr |> Array.sumBy (fun l -> l.Count))
            lastPackedGeomLiveBytes <- idxAlloc.Live * idxSize + vtxAlloc.Live * bytesPerVertex
            lastArenaBytes <- arenaAlloc.Extent * 4
            lastArenaLiveBytes <- arenaAlloc.Live * 4
            lastInstBytes <- instAlloc.Extent * 4
            lastInstLiveBytes <- instAlloc.Live * 4

        member private _.AddInternal(ro : RenderObject) =
            // (re)seat the globals fall-through on a live member
            if slots.Count = 0 then globalsRO <- ro
            let slot = if freeSlots.Count > 0 then freeSlots.Pop() else let s = highWater in highWater <- s + 1; s
            ensureSlot slot
            let keys = Array.zeroCreate<IAdaptiveValue> names.Length
            for i in 0 .. names.Length - 1 do
                let av =
                    match ro.Uniforms.TryGetUniform(scope, nameSyms.[i]) with
                    | ValueSome v -> v
                    | ValueNone -> failwithf "Heap.ofRenderObjects: RO missing per-draw uniform '%s'" names.[i]
                keys.[i] <- av
                headers.[slot * fieldStride + nameToField.[names.[i]]] <- allocRegion av
            if fieldStride > 0 then dirtyHeaders.Add slot |> ignore
            let geomKey = geomKeyOf ro
            let geom =
                if useBindlessGeom then
                    let e = idxFor geomKey ro
                    // register the slot's vertex buffers for the per-handle gather
                    for (ai, _, sym, _, _, _, _) in attrInfos do
                        let bv =
                            match ro.VertexAttributes.TryGetAttribute sym with
                            | ValueSome b -> b
                            | ValueNone -> failwithf "Heap.ofRenderObjects: RO missing shader input attribute %A" sym
                        let pos = slot * numAttrs + ai
                        vtxAvals.[pos] <- bv.Buffer
                        vtxLast.[pos] <- bv.Buffer.GetValue()
                    e
                else geomFor geomKey ro
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
            entries.[slot] <- DrawCallInfo(FaceVertexCount = geom.IndexCount, FirstIndex = geom.FirstIndex, BaseVertex = geom.BaseVertex,
                                           FirstInstance = firstInstance, InstanceCount = instCount)
            dirtyDraws.Add slot |> ignore
            slots.[ro] <- { Slot = slot; RegionKeys = keys; Active = active; Instances = k; InstOffset = firstInstance
                            InstBlock = instBlock; GeomKey = geomKey }

        member private _.RemoveInternal(ro : RenderObject) =
            match slots.TryGetValue ro with
            | true, s ->
                for k in s.RegionKeys do freeRegion k
                freeGeom s.GeomKey
                if useBindlessGeom then
                    for ai in 0 .. numAttrs - 1 do vtxAvals.[s.Slot * numAttrs + ai] <- Unchecked.defaultof<_>
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
                // the globals fall-through must not retain a member that left:
                // switch to any live member (bucket-homogeneous for the resolved
                // globals — see globalsRO above).
                if System.Object.ReferenceEquals(ro, globalsRO) && slots.Count > 0 then
                    globalsRO <- Seq.head slots.Keys
            | _ -> ()

        /// Add ONE new member (no-op if already present). Called from the updater.
        member x.AddOne(ro : RenderObject) =
            if not (slots.ContainsKey ro) then
                x.AddInternal ro
                x.FlushGeometry()

        /// Remove ONE member: tombstone its record, recycle slot + regions.
        /// Waste-triggered compaction (and the buffer swap it implies) runs in
        /// the same updater pass.
        member x.RemoveOne(ro : RenderObject) =
            x.RemoveInternal ro
            maybeCompact ()
            x.FlushGeometry()

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
            x.FlushGeometry()

        /// Release all adaptive references (region writers, texture writers) and
        /// the bucket-owned GPU resources (atlas pages, dummy textures).
        member _.Dispose() =
            for KeyValue(_, e) in regions do arena.Remove e.Writer
            regions.Clear()
            for KeyValue(_, w) in gateWriters do w.Dispose()
            gateWriters.Clear()
            for (_, _, _, table) in bindlessTexTables do table.Dispose()
            match atlasState with
            | Some (pool, dummy, table) ->
                table.Dispose()
                pool.Dispose()
                delDummy dummy
            | None -> ()
            slots.Clear()

    /// Collapse an adaptive set of N render objects into B bucket render objects
    /// (one per effect), each drawn as ONE indirect multidraw against a shared
    /// dirty-tracked arena. `explicitNames = Some s`: the uniforms named in `s`
    /// are gathered per-draw in the rewritten shader; everything else is treated
    /// as a global. `explicitNames = None`: the per-draw field set is AUTO-
    /// DETECTED per RO (see `detectFields` below) and becomes part of the bucket
    /// key. Render objects that aren't heap-eligible (see `isHeapable` below) are
    /// passed through to the output set UNCHANGED — a mixed scene degrades
    /// gracefully.
    let private ofRenderObjectsImpl (runtime : IRuntime) (explicitNames : Set<string> option) (objects : aset<IRenderObject>) : aset<IRenderObject> =
        HeapConfig.requireEnabled "Heap.ofRenderObjects"
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
        // explicit-names call: ONE fixed field set for every heapable RO (the
        // caller restricts/overrides detection — exactly the legacy behavior).
        let explicitFields = explicitNames |> Option.map (fun s -> internFields (s |> Set.toArray |> Array.sort))

        // Bucket key = effect + topology + the VALUES of the per-RO pipeline state
        // (cull / front-face / fill / blend / depth test+write). Reading the state
        // avals through the token makes bucketing REACTIVE: a rule-driven mode value
        // change re-partitions the heap into the right buckets (one indirect draw =
        // one pipeline). This is wombat's per-RO dynamic "mode rules" — the rule is
        // simply each RO's state aval (often derived from its data); constant state
        // never re-partitions. Only mode changes rebuild buckets; per-draw value
        // changes still flow through the arena with no rebuild.
        // geometry layout signature: the shader inputs' actual element types + the
        // index type. Different layouts need different vertex-input pipelines, so
        // they must land in different buckets (and pack consistently).
        let layoutSig (r : RenderObject) =
            let attrs =
                match r.Surface with
                | Surface.Effect e ->
                    e.Inputs |> Map.toList |> List.map (fun (name, _) ->
                        match r.VertexAttributes.TryGetAttribute (Symbol.Create name) with
                        // include offset+stride: the bindless vertex-pull shader bakes them
                        // from ro0, so a bucket must not mix different per-attribute layouts.
                        | ValueSome bv -> sprintf "%s:%s:%d:%d" name bv.ElementType.FullName bv.Offset bv.Stride
                        | ValueNone -> name + ":?") |> String.concat ";"
                | _ -> ""
            let it = match r.Indices with Some bv -> bv.ElementType.FullName | None -> "none"
            attrs + "|" + it
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
            (match bv.Buffer.GetValue() with :? INativeBuffer -> true | _ -> false)
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
                  typeof<V3f>; typeof<V2f>; typeof<float32>; typeof<float>; typeof<int> ])

        // ── per-draw field auto-detection (explicitNames = None) ─────────
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
        // RO stays heapable (exactly what an explicit call omitting that name
        // does), and the read resolves through the bucket's globals fall-through.
        // NOTE the fall-through answers from ONE live member: a consumed+supplied
        // UNPACKABLE uniform that genuinely varies per RO therefore merges on
        // that member's value — same pre-existing limitation as the explicit
        // path; use the explicit overload (or a packable type) if that matters.
        let consumedCache = System.Collections.Generic.Dictionary<string, string[]>()
        let consumedNonSamplerNames (e : Effect) =
            match consumedCache.TryGetValue e.Id with
            | true, v -> v
            | _ ->
                // expand derived rules to a fixpoint (same loop as `rewrite`)
                let hasDerived (eff : Effect) = eff.Uniforms |> Map.exists (fun n _ -> standardDerivedRules.ContainsKey n)
                let mutable cur = e
                let mutable i = 0
                while hasDerived cur && i < 8 do
                    cur <- cur |> Effect.substituteUniforms (fun name _ _ _ -> Map.tryFind name standardDerivedRules)
                    i <- i + 1
                let v =
                    cur.Uniforms |> Map.toArray            // sorted by name
                    |> Array.choose (fun (n, p) ->
                        if typeof<ISampler>.IsAssignableFrom p.uniformType then None else Some n)
                consumedCache.[e.Id] <- v
                v
        let detectFields (r : RenderObject) (e : Effect) =
            consumedNonSamplerNames e
            |> Array.filter (fun n ->
                match r.Uniforms.TryGetUniform(scope, Symbol.Create n) with
                | ValueSome v -> packable.Contains v.ContentType
                | ValueNone -> false)
        // eligible iff: an Effect surface, an indexed (host/tight) draw, every
        // attribute the SHADER reads (effect.Inputs) present host/tight, and every
        // heap uniform present in a packable type. Anything else -> passthrough.
        let isHeapable (o : IRenderObject) =
            match o with
            | :? RenderObject as ro ->
                match ro.Surface with
                | Surface.Effect e ->
                    // geometry: either ALL host-tight (fixed-function combined-buffer path)
                    // OR bindless-eligible (GPU/host buffers vertex-pulled; non-instanced,
                    // since per-draw FirstInstance routes the handle).
                    let attrOk (pred : BufferView -> bool) =
                        e.Inputs |> Map.forall (fun name _ ->
                            match ro.VertexAttributes.TryGetAttribute (Symbol.Create name) with
                            | ValueSome bv -> pred bv
                            | ValueNone -> false)
                    let hostGeom =
                        (match ro.Indices with Some bv -> isHostTight bv | None -> false) && attrOk isHostTight
                    let bindlessGeom =
                        // vertex-pull needs descriptor indexing: a dynamically-indexed
                        // unbounded SSBO array (HeapVertexData[]). Same capability as
                        // unbounded sampler arrays — Vulkan has it, GL does not (GL can't
                        // dynamically index an unsized SSBO array, and sized arrays hit the
                        // tiny SSBO-binding limit). On GL, GPU-resident geometry therefore
                        // falls through to passthrough (the legacy path), rendered as-is.
                        runtime.SupportsUnboundedSamplerArrays &&
                        instanceCountOf ro = 1 &&
                        (match ro.Indices with Some bv -> isReadableIndex bv | None -> false) && attrOk isBindlessAttr
                    (hostGeom || bindlessGeom) &&
                    // explicit-names call: EVERY named uniform must be supplied in
                    // a packable type (missing/odd-typed -> passthrough, as
                    // before). Auto-detect imposes no uniform requirement: a
                    // consumed uniform the RO doesn't supply (or supplies
                    // unpackably) simply isn't detected as a field.
                    (match explicitFields with
                     | Some (ns, _) ->
                         ns |> Array.forall (fun n ->
                             match ro.Uniforms.TryGetUniform(scope, Symbol.Create n) with
                             | ValueSome v -> packable.Contains v.ContentType
                             | ValueNone -> false)
                     | None -> true) &&
                    // textures: every sampler must be a SUPPORTED bindless type (sampler2d
                    // / samplerCube / …) AND the device must support unbounded sampler
                    // arrays. One array per type carries ONE state, so all samplers of a
                    // given type must share their sampler state; otherwise pass through
                    // (also GL, or exotic sampler types).
                    (let samps = e.Uniforms |> Map.toArray |> Array.filter (fun (_, p) -> typeof<ISampler>.IsAssignableFrom p.uniformType)
                     samps.Length = 0 ||
                     ((samps |> Array.forall (fun (_, p) -> isBindlessSamplerType p.uniformType))
                      // textures go through a bindless per-type array (desktop Vulkan) OR a shared
                      // atlas page when unbounded sampler arrays are unavailable (MoltenVK / GL).
                      // The atlas only handles Sampler2d, so keep textured objects heapable there
                      // iff every sampler is a Sampler2d (cube/3d/etc. still need bindless).
                      && (runtime.SupportsUnboundedSamplerArrays
                          || (samps |> Array.forall (fun (_, p) -> p.uniformType = typeof<Sampler2d>)))
                      && (samps
                          |> Array.choose (fun (_, p) ->
                              match p.uniformValue with
                              | UniformValue.Sampler(_, st) -> Some (p.uniformType, st)
                              | UniformValue.SamplerArray a when a.Length > 0 -> Some (p.uniformType, snd a.[0])
                              | _ -> None)
                          |> Array.groupBy fst
                          |> Array.forall (fun (_, g) -> match g with | [||] -> true | _ -> let (_, st0) = g.[0] in g |> Array.forall (fun (_, st) -> st = st0)))))
                | _ -> false
            | _ -> false

        let objsAval = objects |> ASet.toAVal

        // ── incremental driver ───────────────────────────────────────────
        // ONE updater aval per call: it reads the object-set snapshot, groups by
        // (token-reactive) mode key and DIFFS each bucket's membership against
        // its persistent IncrementalBucket — an add/remove is O(changed) instead
        // of O(bucket), for EVERY bucket kind (host or bindless geometry,
        // bindless-textured, atlas, instanced). Every bucket-internal aval
        // (indirect, headers, geometry, textures, arena flush) hangs off the
        // updater, so evaluation order doesn't matter.

        // intern mode keys to unique tokens, so the per-change grouping hashes
        // object references instead of 10-tuples-with-strings (20k ROs/change).
        // keyValues is the reverse map: token -> the RESOLVED mode-key tuple, so a
        // bucket can bake its pipeline state from the KEY's values (a member's
        // dynamic mode aval can then never bend the bucket it leaves — it moves).
        let keyInterner = System.Collections.Generic.Dictionary<_, obj>()
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
                let f =
                    if isHeapable o then
                        let r = o :?> RenderObject
                        // geometry class: all-host-tight -> packed combined buffers;
                        // anything else (heapable => bindless-eligible) -> vertex-pull.
                        let hostGeom =
                            (match r.Indices with Some bv -> isHostTight bv | None -> false) &&
                            (match r.Surface with
                             | Surface.Effect e ->
                                 e.Inputs |> Map.forall (fun name _ ->
                                     match r.VertexAttributes.TryGetAttribute (Symbol.Create name) with
                                     | ValueSome bv -> isHostTight bv
                                     | ValueNone -> false)
                             | _ -> false)
                        let bindless = not hostGeom
                        let inst = instanceCountOf r > 1
                        // per-draw field set: the caller's (explicit) or DETECTED
                        // (effect-consumed ∩ RO-supplied ∩ packable), interned.
                        let (fields, fieldMap) =
                            match explicitFields with
                            | Some fm -> fm
                            | None ->
                                let e = match r.Surface with | Surface.Effect e -> e | _ -> failwith "Heap.ofRenderObjects: expected Surface.Effect"
                                let fm = internFields (detectFields r e)
                                lastAutoFields <- fst fm
                                fm
                        // geometry class + instanced-ness + field set PARTITION
                        // buckets (a bucket RO's surface / routing / geometry
                        // strategy and its baked field layout are fixed at
                        // creation), so fold them into the layout sig.
                        let layout =
                            layoutSig r
                            + (if bindless then "|gpu" else "|host")
                            + (if inst then "|inst" else "")
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
                          Fields = fields
                          FieldMap = fieldMap }
                    else
                        { Heapable = false; Layout = null; ConstToken = null; Bindless = false; Instanced = false
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
        let lastContent = ref HashSet.empty<IRenderObject>
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
            let c = IncrementalBucket(runtime, f0.Fields, f0.FieldMap, effect, r0, updaterRef.Value, f0.Bindless, f0.Instanced,
                                      (cull, ff, fill, blend, dtest, dwrite))
            caches.[key] <- c
            c

        let updater =
            AVal.custom (fun t ->
                let cur = objsAval.GetValue t
                let delta = HashSet.computeDelta lastContent.Value cur
                lastContent.Value <- cur

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
                    for o in cur do
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
                version.Value <- version.Value + 1
                version.Value)
        updaterRef.Value <- updater
        let resultAval =
            updater |> AVal.map (fun _ ->
                let out = Array.zeroCreate<IRenderObject> (caches.Count + passSet.Count)
                let mutable i = 0
                for KeyValue(_, c) in caches do
                    out.[i] <- c.RenderObject
                    i <- i + 1
                for o in passSet do
                    out.[i] <- o
                    i <- i + 1
                out)                                    // collapsed buckets ∪ untouched passthrough
        resultAval |> ASet.ofAVal

    /// Collapse an adaptive set of N render objects into B bucket render objects
    /// (one per effect + pipeline state + geometry layout), each drawn as ONE
    /// indirect multidraw against a shared dirty-tracked arena. The uniforms
    /// named in `heapNames` are gathered per-draw in the rewritten shader;
    /// everything else is treated as a global. The explicit set RESTRICTS /
    /// OVERRIDES auto-detection: an RO missing one of the names (or supplying it
    /// in an unpackable type) is passed through UNCHANGED. Prefer the names-free
    /// overload below unless you need that restriction.
    let ofRenderObjects (runtime : IRuntime) (heapNames : Set<string>) (objects : aset<IRenderObject>) : aset<IRenderObject> =
        ofRenderObjectsImpl runtime (Some heapNames) objects

    /// Names-free variant of `ofRenderObjects`: the per-draw heap fields are
    /// AUTO-DETECTED per RO — every uniform the effect consumes (after derived-
    /// rule expansion, samplers excluded) that the RO's own uniform provider
    /// supplies in a packable type becomes a per-draw heap field; names that fall
    /// through to scene/global scope (camera, lights, …) stay ordinary uniforms,
    /// and sampler/texture uniforms keep the bindless/atlas path. The detected
    /// field set is part of the bucket key, so ROs with different sets land in
    /// different buckets.
    let ofRenderObjectsAuto (runtime : IRuntime) (objects : aset<IRenderObject>) : aset<IRenderObject> =
        ofRenderObjectsImpl runtime None objects

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

    /// One distinct trafo link -> one fp64 slot in the LinkArena. Packs the
    /// link's Forward matrix; marked (via its source) only when that link changes.
    type internal LinkWriter(src : aval<Trafo3d>, slot : int) =
        inherit AdaptiveObject()
        do (src :> IAdaptiveValue).Acquire()
        member _.Slot = slot
        member x.Pack(token : AdaptiveToken, staging : M44d[]) =
            x.EvaluateIfNeeded token () (fun token -> staging.[slot] <- (src.GetValue token).Forward)
        member x.Dispose() =
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

        // flatten chains, DEDUP links by aval identity: a shared parent => one slot.
        let slotOf      = System.Collections.Generic.Dictionary<IAdaptiveValue, int>(HashIdentity.Reference)
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
                    match slotOf.TryGetValue (l :> IAdaptiveValue) with
                    | true, s -> s
                    | _ -> let s = distinct.Count in slotOf.[l :> IAdaptiveValue] <- s; distinct.Add l; s
                idxList.Add slot
                cur <- cur + 1
        let linkIdx = idxList.ToArray()
        let distinctArr = distinct.ToArray()

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

        do HeapConfig.requireEnabled "HeapScene"
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
