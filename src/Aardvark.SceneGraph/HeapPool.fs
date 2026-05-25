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
        /// the atlas page dimensions in pixels (to normalize atlas-pixel coords)
        member x.HeapAtlasPxSize : V2f   = uniform?HeapAtlasPxSize

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
    let isSupported (runtime : IRuntime) = runtime.SupportsMultiDrawIndirectDrawId

    /// Throw a clear error if `runtime` cannot run the heap path. Pass
    /// `textures = true` to also require unbounded (bindless) sampler arrays.
    let checkSupport (textures : bool) (runtime : IRuntime) =
        if not runtime.SupportsMultiDrawIndirectDrawId then
            failwith "Heap: runtime does not support multi-draw-indirect with a per-draw gl_DrawID (required for heap routing). Needs Vulkan, or GL 4.6+ with GL_ARB_shader_draw_parameters."
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
    let private heapAtlas =
        sampler2d { texture uniform?HeapAtlasTex; filter Filter.MinMagLinear; addressU WrapMode.Clamp; addressV WrapMode.Clamp }

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
    let private atlasMipAt (tex : Sampler2d) (origin : V2f) (size : V2f) (k : int) (uv : V2f) (addrU : int) (addrV : int) : V4f =
        let mo = atlasMipOrigin origin size k
        let ms = V2f(max 1.0f (floor (size.X / float32 (1 <<< k))), max 1.0f (floor (size.Y / float32 (1 <<< k))))
        let p  = uniform.HeapAtlasPxSize.X
        let px = atlasAxis uv.X mo.X ms.X addrU
        // aardvark uploads PixImages bottom-left; acq origins are top-left -> feed (1-v) and flip page-Y.
        let py = atlasAxis (1.0f - uv.Y) mo.Y ms.Y addrV
        tex.SampleLevel(V2f(px, p - py) / p, 0.0f)

    /// sample an object's atlas tile. origin/size in atlas px; fmt packs
    /// numMips&lt;&lt;1 | addrU&lt;&lt;4 | addrV&lt;&lt;6. Manual LOD from screen-space derivatives.
    [<ReflectedDefinition>]
    let private atlasSample (tex : Sampler2d) (origin : V2f) (size : V2f) (fmt : int) (uv : V2f) : V4f =
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
        let c0    = atlasMipAt tex origin size k0 uv addrU addrV
        let c1    = atlasMipAt tex origin size k1 uv addrU addrV
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
                        let uvE    = Expr.Cast<V2f>(rw uvArg)
                        // reference heapAtlas DIRECTLY in the spliced expr (not only inside an
                        // inlined ReflectedDefinition) so FShade reflects it into the binding interface.
                        <@@ atlasSample heapAtlas %origin %size %fmt %uvE @@>
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
    // v1 assumptions: inputs are `RenderObject`s sharing geometry within a
    // bucket; per-draw heap uniforms named in `heapNames` are present on
    // every RO with a consistent type. Globals (camera etc.) are delegated
    // to the first RO's uniform provider. Bucketing on set change is coarse
    // (rebuild affected buckets); per-draw value marks flow through the
    // reactive arena with offsets/headers held constant.

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

    /// Force the texture-atlas path even where descriptor-indexed sampler arrays ARE
    /// available (for testing the atlas on desktop Vulkan, which reports them supported).
    let mutable forceAtlas = false

    /// Extract a host PixImage&lt;byte&gt; (RGBA) from an ITexture for atlas packing.
    let private toAtlasPixImage (t : ITexture) : PixImage<byte> =
        match t with
        | :? PixTexture2d as pt -> pt.PixImageMipMap.[0].ToPixImage<byte>()
        | _ -> failwithf "Heap atlas: unsupported ITexture %A (host PixTexture2d only)" (t.GetType())

    /// Pick a single square page size that fits all reserved (gutter+mip) rects without
    /// rotation, clamped to the atlas page cap.
    let private atlasPageSizeFor (pixs : (int * PixImage<byte>)[]) : int =
        let area =
            pixs |> Array.sumBy (fun (_, p) ->
                let w, h = int p.Size.X, int p.Size.Y
                let s = HeapAtlas.reservedSize true (HeapAtlas.defaultMipCount w h) w h
                float (s.X * s.Y))
        let est = Fun.NextPowerOfTwo (max 256 (int (ceil (sqrt (area * 1.6)))))
        min HeapAtlas.PageSize est

    /// Distinct trafo-link slots uploaded on the most recent chain-arena flush
    /// (diagnostic). A shared-root change over N objects should be 1, not N.
    let mutable lastChainLinkUploads = 0

    /// Adaptive writer for one arena region. Reads its source aval and packs
    /// the floats into the arena's shared staging at its offset. Marked (via
    /// the source) only when that source changes.
    type internal RegionWriter(src : IAdaptiveValue, off : int, size : int, pack : obj -> float32[] -> int -> unit) =
        inherit AdaptiveObject()
        do src.Acquire()
        member _.Off = off
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
        /// Grow the buffer + staging to hold at least n floats (call in transact).
        member x.EnsureFloats(n : int) =
            if n > capacity then
                let nf = Fun.NextPowerOfTwo n
                x.Resize(uint64 (nf * 4))           // copies existing GPU content
                let ns = Array.zeroCreate<float32> nf
                System.Array.Copy(staging, ns, capacity)
                staging <- ns
                capacity <- nf
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
        override x.Compute(t, rt) =
            let dirty = pending.GetAndClear()
            if dirty.Count > 0 then
                let ranges = System.Collections.Generic.List<struct(int * int)>(dirty.Count)
                for w in dirty do
                    w.Pack(t, staging)
                    ranges.Add(struct(w.Off, w.Off + w.Size))
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

    /// Collapse an adaptive set of N render objects into B bucket render objects
    /// (one per effect), each drawn as ONE indirect multidraw against a shared
    /// dirty-tracked arena. The uniforms named in `heapNames` are gathered
    /// per-draw in the rewritten shader; everything else is treated as a global.
    // ── bindless vertex-pull helpers (shared by ofRenderObjects' GPU-geometry
    //    buckets and the standalone Heap.bindless) ────────────────────────────
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

    /// Render objects that aren't heap-eligible (see `isHeapable` below) are passed
    /// through to the output set UNCHANGED — so a mixed scene degrades gracefully.
    let ofRenderObjects (runtime : IRuntime) (heapNames : Set<string>) (objects : aset<IRenderObject>) : aset<IRenderObject> =
        checkSupport false runtime
        let names = heapNames |> Set.toArray |> Array.sort
        let fieldStride = names.Length
        let nameToField = names |> Array.mapi (fun i n -> n, i) |> Map.ofArray
        let heapSyms = heapNames |> Set.map Symbol.Create
        let symData = Symbol.Create "HeapData"
        let symHeaders = Symbol.Create "HeapHeaders"
        let scope = Ag.Scope.Root

        let uni (ro : RenderObject) (n : string) =
            match ro.Uniforms.TryGetUniform(scope, Symbol.Create n) with
            | ValueSome v -> v
            | ValueNone -> failwithf "Heap.ofRenderObjects: RO missing per-draw uniform '%s'" n

        // an input RO may ALREADY be instanced (instanceCount > 1); preserve it.
        // Per-draw routing is by gl_DrawID, so each sub-draw keeps firstInstance 0
        // and gl_InstanceIndex stays the RO's local instance index (0 .. K-1).
        let instanceCountOf (ro : RenderObject) =
            match ro.DrawCalls with
            | DrawCalls.Direct calls ->
                match AVal.force calls with
                | [||] -> 1
                | arr -> max 1 arr.[0].InstanceCount
            | _ -> 1

        let buildBucket (ros : RenderObject[]) : IRenderObject =
            let ro0 = ros.[0]
            let effect = match ro0.Surface with | Surface.Effect e -> e | _ -> failwith "Heap.ofRenderObjects: expected Surface.Effect"

            // per-object textures: the effect's sampler uniforms become indexed reads of
            // the per-TYPE bindless array (slot*Kt + kt); gathered below. Group by type so
            // each type gets its own array + index buffer; assign kt within each group.
            let samplers      = samplerUniforms effect      // (name, texName, type, state)[]
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
            // state per type). Keyed by the FShade uniform key (the module value name),
            // which is what shaderUniforms uses — NOT the texture semantic.
            let samplerStateOverrides =
                samplersByType
                |> Array.choose (fun (ty, grp) ->
                    match bindlessTypeInfo ty with
                    | Some (_, _, fkey) when grp.Length > 0 -> let (_, _, _, st) = grp.[0] in Some (fkey, st)
                    | _ -> None)
                |> Map.ofArray
            // the bucket provider must pass through neither the sampler binding names nor
            // the texture uniform names — both are folded into the per-type arrays.
            let samplerSyms = samplers |> Array.collect (fun (sn, tn, _, _) -> [| Symbol.Create sn; Symbol.Create tn |]) |> Set.ofArray

            // ── atlas fallback (Vulkan-1.0 / GL / MoltenVK: ONE sampler) ──
            // Use the texture atlas for Sampler2d textures when descriptor-indexed sampler
            // arrays are unavailable (GL / Vulkan-1.0) or forced. v1: only when EVERY
            // sampler is Sampler2d (cube etc. keep the bindless path). The bucket's distinct
            // textures are packed into ONE atlas page (gutters + Iliffe mips) and each
            // object's sampler.Sample(uv) is rewritten to atlasSample over that page.
            let atlas2d = samplers |> Array.filter (fun (_, _, ty, _) -> ty = typeof<Sampler2d>)
            let useAtlas =
                atlas2d.Length > 0 && atlas2d.Length = samplers.Length &&
                (forceAtlas || not runtime.SupportsUnboundedSamplerArrays)
            let atlasK = atlas2d.Length
            let atlasByName = if useAtlas then atlas2d |> Array.mapi (fun kt (sn, _, _, _) -> sn, (kt, atlasK)) |> Map.ofArray else Map.empty
            // (pageTex, pageSizePx, origins[], sizes[], fmts[]) — per (object, sampler) at slot*K+k.
            let atlasData : (aval<ITexture> * aval<V2f> * aval<V4f[]> * aval<V4f[]> * aval<int[]>) option =
                if not useAtlas then None
                else
                    let texAvals =
                        ros |> Array.collect (fun ro ->
                            atlas2d |> Array.map (fun (_, tn, _, _) ->
                                match ro.Uniforms.TryGetUniform(scope, Symbol.Create tn) with
                                | ValueSome v -> v
                                | ValueNone -> failwithf "Heap.ofRenderObjects: atlas texture %A missing" tn))
                    let states = atlas2d |> Array.map (fun (_, _, _, st) -> st)
                    let addrCode (w : WrapMode option) = match w with | Some WrapMode.Wrap -> 1 | Some WrapMode.Mirror -> 2 | _ -> 0
                    let built =
                        AVal.custom (fun t ->
                            let texs = texAvals |> Array.map (fun (av : IAdaptiveValue) -> av.GetValueUntyped t :?> ITexture)
                            let distinct = System.Collections.Generic.List<ITexture>()
                            let idxOf = System.Collections.Generic.Dictionary<ITexture, int>(HashIdentity.Reference)
                            let perTex = texs |> Array.map (fun tex -> match idxOf.TryGetValue tex with | true, i -> i | _ -> let i = distinct.Count in idxOf.[tex] <- i; distinct.Add tex; i)
                            let pixs = distinct |> Seq.mapi (fun i tx -> i, toAtlasPixImage tx) |> Seq.toArray
                            let pageSz = atlasPageSizeFor pixs
                            let pages, acq =
                                let p1, a1 = HeapAtlas.build pageSz true pixs
                                if p1.Length <= 1 then p1, a1 else HeapAtlas.build HeapAtlas.PageSize true pixs
                            let pageTex = runtime.PrepareTexture(PixTexture2d(pages.[0])) :> ITexture
                            let realSz = pages.[0].Size.X
                            let origins = Array.zeroCreate<V4f> texs.Length
                            let sizes   = Array.zeroCreate<V4f> texs.Length
                            let fmts    = Array.zeroCreate<int>  texs.Length
                            texs |> Array.iteri (fun i _ ->
                                let a = acq.[perTex.[i]]
                                let st = states.[i % atlasK]
                                origins.[i] <- V4f(float32 a.OriginPx.X, float32 a.OriginPx.Y, 0.0f, 0.0f)
                                sizes.[i]   <- V4f(float32 a.SizePx.X,   float32 a.SizePx.Y,   0.0f, 0.0f)
                                fmts.[i]    <- (a.NumMips <<< 1) ||| (addrCode st.AddressU <<< 4) ||| (addrCode st.AddressV <<< 6))
                            pageTex, V2f(float32 realSz, float32 realSz), origins, sizes, fmts)
                    Some (built |> AVal.map (fun (p, _, _, _, _) -> p),
                          built |> AVal.map (fun (_, s, _, _, _) -> s),
                          built |> AVal.map (fun (_, _, o, _, _) -> o),
                          built |> AVal.map (fun (_, _, _, z, _) -> z),
                          built |> AVal.map (fun (_, _, _, _, f) -> f))

            // name -> (fieldIdx, size, packer), types from ro0
            let info = names |> Array.map (fun n -> let (sz, pk) = packerFor (uni ro0 n).ContentType in n, (nameToField.[n], sz, pk)) |> Map.ofArray

            // dedup arena regions by aval identity
            let regionOf = System.Collections.Generic.Dictionary<IAdaptiveValue, int>(HashIdentity.Reference)
            let distinct = System.Collections.Generic.List<IAdaptiveValue * int * int * (obj -> float32[] -> int -> unit)>()
            let mutable cursor = 0
            let headers = Array.zeroCreate<int> (ros.Length * fieldStride)
            ros |> Array.iteri (fun di ro ->
                for n in names do
                    let (fi, sz, pk) = info.[n]
                    let av = uni ro n
                    let off =
                        match regionOf.TryGetValue av with
                        | true, o -> o
                        | _ -> let o = cursor in cursor <- cursor + sz; regionOf.[av] <- o; distinct.Add(av, o, sz, pk); o
                    headers.[di * fieldStride + fi] <- off)

            let totalF = cursor
            // arena = custom dirty-tracking AdaptiveBuffer: one lightweight writer
            // per distinct region (reused staging, direct sub-range upload).
            let arena = HeapArena(runtime, totalF)
            for (av, o, sz, pk) in distinct do arena.Add(av, o, sz, pk) |> ignore

            // Two geometry strategies. If the bucket has ANY GPU-resident vertex/index
            // buffer it can't be CPU-sliced into a combined buffer, so it uses bindless
            // VERTEX-PULL: each object's existing buffers are bound (no copy) into the
            // per-handle HeapVertexData array and the shader pulls attributes by
            // gl_InstanceIndex. An all-host bucket keeps the efficient fixed-function
            // combined-buffer path. `geomRewrite` is the extra shader rewrite (vertex-pull
            // for bindless, identity for host); `vtxBindings` binds HeapVertexData/I.
            let isGpuBuffer (bv : BufferView) = match bv.Buffer.GetValue() with :? IBackendBuffer -> true | _ -> false
            let roHasGpuGeom (ro : RenderObject) =
                (match ro.Indices with Some bv -> isGpuBuffer bv | None -> false)
                || (effect.Inputs |> Map.exists (fun name _ ->
                        match ro.VertexAttributes.TryGetAttribute (Symbol.Create name) with
                        | ValueSome bv -> isGpuBuffer bv | ValueNone -> false))
            let useBindless = ros |> Array.exists roHasGpuGeom
            let symVD  = Symbol.Create "HeapVertexData"
            let symVDI = Symbol.Create "HeapVertexDataI"

            let geometry : Expr<int> * DrawCallInfo[] * (Effect -> Effect) * (Symbol * BufferView) list * BufferView * (Symbol * IAdaptiveValue) list =
                if useBindless then
                    // attribute layout (effect.Inputs order): per-attr type + the float
                    // stride/offset from ro0's BufferView (so separate-tight AND interleaved
                    // buffers work; layoutSig keys offset/stride so a bucket never mixes them).
                    // per-draw handle = gl_InstanceIndex (Vulkan, via FirstInstance=di) or
                    // gl_DrawID (GL: gl_InstanceID omits baseInstance, so route by draw id
                    // and keep FirstInstance=0) — same rule the host path uses.
                    let isGLb = runtime.GetType().FullName.Contains("Aardvark.Rendering.GL")
                    let slotE : Expr<int> = if isGLb then <@ getDrawId() @> else Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.InstanceId)
                    let handleE : Expr = slotE.Raw
                    let attrInfos =
                        effect.Inputs |> Map.toArray
                        |> Array.mapi (fun ai (name, _) ->
                            let sym = Symbol.Create name
                            let bv = match ro0.VertexAttributes.TryGetAttribute sym with ValueSome b -> b | ValueNone -> failwithf "Heap.ofRenderObjects: RO missing shader input attribute %A" sym
                            let es = elemSize bv.ElementType
                            let strideF = (if bv.Stride = 0 then es else bv.Stride) / 4
                            let offF = bv.Offset / 4
                            ai, name, sym, bv.ElementType, strideF, offF)
                    let numAttrs = attrInfos.Length
                    let idxType = match ro0.Indices with Some bv -> bv.ElementType | None -> failwith "Heap.ofRenderObjects: heapable RO must be indexed"
                    let idxSize = elemSize idxType
                    // combined index buffer: per-object LOCAL 0-based indices concatenated
                    // (small; downloaded if GPU-resident). FirstInstance=di routes the handle.
                    let packedIdx = System.Collections.Generic.List<byte>()
                    let mutable firstIndex = 0
                    let entries =
                        ros |> Array.mapi (fun di ro ->
                            let ibv = match ro.Indices with Some b -> b | None -> failwith "Heap.ofRenderObjects: RO has no index buffer"
                            let ib = readGeomBytes runtime ibv
                            let cnt = ib.Length / idxSize
                            let fi = firstIndex
                            packedIdx.AddRange ib
                            firstIndex <- firstIndex + cnt
                            DrawCallInfo(FaceVertexCount = cnt, FirstIndex = fi, BaseVertex = 0, FirstInstance = (if isGLb then 0 else di), InstanceCount = 1))
                    let idxBV = packedView (packedIdx.ToArray()) idxType
                    // object-major flatten of the objects' EXISTING buffers (reactive):
                    // HeapVertexData[di*numAttrs + ai] = object di's attribute ai buffer.
                    let vtxBufAvals =
                        ros |> Array.collect (fun ro ->
                            attrInfos |> Array.map (fun (_, _, sym, _, _, _) ->
                                match ro.VertexAttributes.TryGetAttribute sym with
                                | ValueSome bv -> bv.Buffer
                                | ValueNone -> failwithf "Heap.ofRenderObjects: RO missing shader input attribute %A" sym))
                    let vtxDataU = (AVal.custom (fun t -> vtxBufAvals |> Array.map (fun b -> b.GetValue t)) :> IAdaptiveValue)
                    // rewrite each vertex-input read into a per-handle flat-buffer gather
                    // Rewrite vertex-input reads into per-handle gathers — ONLY in the
                    // VERTEX stage. Vertex attributes are read there; a later stage's input
                    // of the same semantic (e.g. an interpolated Normal varying) must keep
                    // its interpolated value, and must NOT get gl_DrawID/gl_InstanceIndex
                    // injected (invalid outside the vertex stage, and re-running
                    // substituteReads there drops the draw-id extension the uniform gather
                    // needs). Use the SHADER INPUT type (ityp) so the gather type matches.
                    let geomRewrite (e : Effect) =
                        e |> Effect.map (fun s ->
                            if s.shaderStage <> ShaderStage.Vertex then s
                            else
                                s |> Shader.substituteReads (fun kind ityp name _ _ ->
                                    match kind with
                                    | ParameterKind.Input ->
                                        match attrInfos |> Array.tryFind (fun (_, n, _, _, _, _) -> n = name) with
                                        | Some (ai, _, _, _, strideF, offF) -> Some (bindlessGatherFlat handleE ityp numAttrs ai strideF offF)
                                        | None -> None
                                    | _ -> None))
                    slotE, entries, geomRewrite, [], idxBV, [ symVD, vtxDataU; symVDI, vtxDataU ]
                else
                    // ── all-host bucket: fixed-function combined-buffer path ──
                    // Pack ONLY the attributes the shader consumes (effect.Inputs), each
                    // with its REAL element type, into shared raw-byte buffers; the index
                    // keeps its real type. Deduped by geometry identity (first attr + index).
                    let attrTypes =
                        effect.Inputs |> Map.toArray
                        |> Array.map (fun (name, _) ->
                            let sym = Symbol.Create name
                            match ro0.VertexAttributes.TryGetAttribute sym with
                            | ValueSome bv -> sym, bv.ElementType, elemSize bv.ElementType
                            | ValueNone -> failwithf "Heap.ofRenderObjects: RO missing shader input attribute %A" sym)
                    let idxType = match ro0.Indices with Some bv -> bv.ElementType | None -> failwith "Heap.ofRenderObjects: heapable RO must be indexed"
                    let idxSize = elemSize idxType
                    let packedAttr = attrTypes |> Array.map (fun _ -> System.Collections.Generic.List<byte>())
                    let packedIdx  = System.Collections.Generic.List<byte>()
                    let geomCache  = System.Collections.Generic.Dictionary<struct(obj * obj), struct(int * int * int)>(HashIdentity.Structural)
                    let mutable vtxCount = 0
                    let mutable idxCount = 0
                    let baseEntries =
                        ros |> Array.map (fun ro ->
                            let idxBV = match ro.Indices with Some bv -> bv | None -> failwith "Heap.ofRenderObjects: RO has no index buffer"
                            let firstAttr = let (sym, _, _) = attrTypes.[0] in (match ro.VertexAttributes.TryGetAttribute sym with ValueSome b -> b.Buffer :> obj | ValueNone -> null)
                            let key = struct(firstAttr, idxBV.Buffer :> obj)
                            let (struct(firstIndex, baseVertex, count)) =
                                match geomCache.TryGetValue key with
                                | true, r -> r
                                | _ ->
                                    let firstIndex = idxCount
                                    let baseVertex = vtxCount
                                    let ib = readBytesView idxBV
                                    let thisIdx = ib.Length / idxSize
                                    packedIdx.AddRange ib
                                    idxCount <- idxCount + thisIdx
                                    let mutable thisVtx = 0
                                    attrTypes |> Array.iteri (fun ai (sym, _, es) ->
                                        let bv = match ro.VertexAttributes.TryGetAttribute sym with ValueSome b -> b | ValueNone -> failwithf "Heap.ofRenderObjects: RO missing shader input attribute %A" sym
                                        let bytes = readBytesView bv
                                        packedAttr.[ai].AddRange bytes
                                        thisVtx <- bytes.Length / es)
                                    vtxCount <- vtxCount + thisVtx
                                    let r = struct(firstIndex, baseVertex, thisIdx)
                                    geomCache.[key] <- r
                                    r
                            DrawCallInfo(FaceVertexCount = count, FirstIndex = firstIndex, BaseVertex = baseVertex, FirstInstance = 0, InstanceCount = instanceCountOf ro))
                    // Per-draw routing. gl_DrawID is UNSUPPORTED in MSL (MoltenVK), so on
                    // VULKAN, when no sub-draw is instanced, route by gl_InstanceIndex + a
                    // per-draw firstInstance. On GL gl_InstanceID omits baseInstance, so GL
                    // uses gl_DrawID (GL 4.6); instanced sub-draws always need gl_DrawID.
                    let isGL = runtime.GetType().FullName.Contains("Aardvark.Rendering.GL")
                    let anyInstanced = baseEntries |> Array.exists (fun e -> e.InstanceCount > 1)
                    let useDrawId = isGL || anyInstanced
                    let slot : Expr<int> =
                        if useDrawId then <@ getDrawId() @>
                        else Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.InstanceId)
                    if not useDrawId then
                        for di in 0 .. baseEntries.Length - 1 do baseEntries.[di].FirstInstance <- di
                    let vtxAttribs = [ for ai in 0 .. attrTypes.Length - 1 -> let (sym, et, _) = attrTypes.[ai] in sym, packedView (packedAttr.[ai].ToArray()) et ]
                    slot, baseEntries, id, vtxAttribs, (packedView (packedIdx.ToArray()) idxType), []
            let (slot, baseEntries, geomRewrite, vtxAttribsList, indicesBV, vtxBindings) = geometry

            // per-RO visibility gate: IsActive = false -> InstanceCount 0 (the draw
            // emits nothing), no bucket/arena churn. Reactive only when some RO has
            // a non-constant IsActive; all-constant-active buckets stay a constant.
            let actives = ros |> Array.map (fun ro -> ro.IsActive)
            let indirect =
                if actives |> Array.forall (fun a -> a.IsConstant && AVal.force a) then
                    AVal.constant (IndirectBuffer.ofArray baseEntries)
                else
                    AVal.custom (fun t ->
                        let entries = Array.copy baseEntries
                        for i in 0 .. entries.Length - 1 do
                            if not (actives.[i].GetValue t) then entries.[i].InstanceCount <- 0
                        IndirectBuffer.ofArray entries)

            let ro = RenderObject.Clone ro0
            ro.IsActive         <- AVal.constant true   // bucket always active; per-draw gating is in the indirect buffer
            ro.Surface          <-
                let baseE = rewrite slot nameToField fieldStride standardDerivedRules effect |> geomRewrite
                if useAtlas then Surface.Effect (baseE |> rewriteAtlasSamples slot atlasByName)
                else Surface.Effect (baseE |> rewriteSamplers slot samplerByName |> overrideSamplerStates samplerStateOverrides)
            ro.DrawCalls        <- DrawCalls.Indirect indirect
            ro.VertexAttributes <- AttributeProvider.ofList vtxAttribsList
            ro.Indices          <- Some indicesBV

            let arenaU   = ((arena :> aval<IBackendBuffer>) |> AVal.map (fun b -> b :> IBuffer)) :> IAdaptiveValue
            let headersU = AVal.constant headers :> IAdaptiveValue
            // For each sampler TYPE present, gather that type's textures (in kt order) and
            // DEDUP them: HeapTextures<T> holds only the bucket's distinct textures and
            // HeapTexIndices<T>.[slot*Kt+kt] points each draw at its texture. Keeps the
            // unbounded array within its cap even when many objects share few textures.
            // Reactive: re-reads + re-dedups when a per-object texture aval changes.
            let texLookup = System.Collections.Generic.Dictionary<Symbol, IAdaptiveValue>(HashIdentity.Structural)
            // atlas path: bind the single page + per-object placement; skip the bindless arrays.
            match atlasData with
            | Some (pageTex, pxSize, origins, sizes, fmts) ->
                texLookup.[Symbol.Create "HeapAtlasTex"]    <- pageTex :> IAdaptiveValue
                texLookup.[Symbol.Create "HeapAtlasPxSize"] <- pxSize  :> IAdaptiveValue
                texLookup.[Symbol.Create "HeapAtlasOrigin"] <- origins :> IAdaptiveValue
                texLookup.[Symbol.Create "HeapAtlasSize"]   <- sizes   :> IAdaptiveValue
                texLookup.[Symbol.Create "HeapAtlasFmt"]    <- fmts    :> IAdaptiveValue
            | None -> ()
            for (ty, grp) in (if useAtlas then [||] else samplersByType) do
                match bindlessTypeInfo ty with
                | None -> ()
                | Some (arrName, idxName, _) ->
                    let texAvals =
                        ros |> Array.collect (fun ro ->
                            grp |> Array.map (fun (_, tn, _, _) ->
                                match ro.Uniforms.TryGetUniform(scope, Symbol.Create tn) with
                                | ValueSome v -> v
                                | ValueNone -> failwithf "Heap.ofRenderObjects: texture uniform %A missing" tn))
                    let d =
                        AVal.custom (fun t ->
                            let texs = texAvals |> Array.map (fun (av : IAdaptiveValue) -> av.GetValueUntyped t :?> ITexture)
                            let distinct = System.Collections.Generic.List<ITexture>()
                            let idxOf = System.Collections.Generic.Dictionary<ITexture, int>(HashIdentity.Reference)
                            let indices =
                                texs |> Array.map (fun tex ->
                                    match idxOf.TryGetValue tex with
                                    | true, i -> i
                                    | _ -> let i = distinct.Count in idxOf.[tex] <- i; distinct.Add tex; i)
                            distinct.ToArray(), indices)
                    texLookup.[Symbol.Create arrName] <- (d |> AVal.map fst) :> IAdaptiveValue
                    texLookup.[Symbol.Create idxName] <- (d |> AVal.map snd) :> IAdaptiveValue
            // bindless geometry: bind the objects' GPU vertex buffers as HeapVertexData/I
            for (sym, v) in vtxBindings do texLookup.[sym] <- v
            ro.Uniforms <-
                { new IUniformProvider with
                    member _.TryGetUniform(s, name) =
                        if name = symData then ValueSome arenaU
                        elif name = symHeaders then ValueSome headersU
                        else
                            match texLookup.TryGetValue name with
                            | true, v -> ValueSome v
                            | _ ->
                                if Set.contains name heapSyms then ValueNone
                                elif Set.contains name samplerSyms then ValueNone
                                else ro0.Uniforms.TryGetUniform(s, name)
                    member _.Dispose() = () }
            ro :> IRenderObject

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
        let modeKey (t : AdaptiveToken) (r : RenderObject) =
            let ra = r.RasterizerState
            let eid = match r.Surface with | Surface.Effect e -> e.Id | _ -> "?"
            (eid, r.Mode, layoutSig r,
             ra.CullMode.GetValue t, ra.FrontFacing.GetValue t, ra.FillMode.GetValue t,
             r.BlendState.Mode.GetValue t,
             r.DepthState.Test.GetValue t, r.DepthState.WriteMask.GetValue t)

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
                    (names |> Array.forall (fun n ->
                        match ro.Uniforms.TryGetUniform(scope, Symbol.Create n) with
                        | ValueSome v -> packable.Contains v.ContentType
                        | ValueNone -> false)) &&
                    // textures: every sampler must be a SUPPORTED bindless type (sampler2d
                    // / samplerCube / …) AND the device must support unbounded sampler
                    // arrays. One array per type carries ONE state, so all samplers of a
                    // given type must share their sampler state; otherwise pass through
                    // (also GL, or exotic sampler types).
                    (let samps = e.Uniforms |> Map.toArray |> Array.filter (fun (_, p) -> typeof<ISampler>.IsAssignableFrom p.uniformType)
                     samps.Length = 0 ||
                     (runtime.SupportsUnboundedSamplerArrays
                      && (samps |> Array.forall (fun (_, p) -> isBindlessSamplerType p.uniformType))
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
        let resultAval =
            AVal.custom (fun t ->
                let heapable, rest = objsAval.GetValue t |> HashSet.toArray |> Array.partition isHeapable
                let buckets =
                    heapable
                    |> Array.choose (fun ro -> match ro with :? RenderObject as r -> Some r | _ -> None)
                    |> Array.groupBy (modeKey t)
                    |> Array.map (fun (_, g) -> buildBucket g)
                lastBucketCount <- buckets.Length
                Array.append buckets rest)              // collapsed buckets ∪ untouched passthrough
        resultAval |> ASet.ofAVal

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
        // slot routed by gl_DrawID (= the sub-draw's position = the slot), so this
        // works on GL too (firstInstance stays 0).
        let effect' =
            let did : Expr<int> = <@ getDrawId() @>
            effect |> Effect.substituteUniforms (fun name typ _ _ ->
                match fieldOffset.TryGetValue name with
                | true, fo -> Some (gatherFor typ <@ %did * %(cint dataStride) + %(cint fo) @>)
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
        let heapDataU = ((arena :> aval<IBackendBuffer>) |> AVal.map (fun b -> b :> IBuffer)) :> IAdaptiveValue
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
                entries.[slot] <- DrawCallInfo(FaceVertexCount = index.Length, FirstIndex = 0, BaseVertex = 0, FirstInstance = 0, InstanceCount = 1)
                arena.Touch()
                version.Value <- version.Value + 1
                slot)

        /// Remove a previously added draw. Call in transact.
        member _.Remove(slot : int) =
            lock gate (fun () ->
                match slotWriters.TryGetValue slot with
                | true, ws ->
                    for w in ws do arena.Remove w
                    slotWriters.Remove slot |> ignore
                | _ -> ()
                if slot < entries.Length then
                    entries.[slot] <- DrawCallInfo(FaceVertexCount = 0, FirstInstance = 0, InstanceCount = 0)
                freeList.Push slot
                version.Value <- version.Value + 1)

        member _.Count = slotWriters.Count
        member _.RenderObject = ro :> IRenderObject
        member x.Sg = Sg.renderObjectSet (ASet.single x.RenderObject)
