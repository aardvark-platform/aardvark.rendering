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
        // Bindless geometry: arrays of per-object GPU buffers, referenced by handle
        // (gl_DrawID). Vertex-pulling reads HeapPositions[handle].data[index].
        // V4f (not V3f) because a std430 vec3[] element is 16-byte aligned (stride
        // 16) — a tightly-packed V3f[] would be read misaligned. Pull uses .XYZ.
        member x.HeapPositions : V4f[][] = uniform?StorageBuffer?HeapPositions
        member x.HeapNormals   : V4f[][] = uniform?StorageBuffer?HeapNormals
        member x.HeapIndex     : int[][] = uniform?StorageBuffer?HeapIndex

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

    /// Number of buckets produced by the most recent `ofRenderObjects` evaluation
    /// (diagnostic / for logging).
    let mutable lastBucketCount = 0

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

            // Pack ONLY the attributes the shader consumes (effect.Inputs), each
            // with its REAL element type (any type), into shared raw-byte buffers;
            // the index keeps its real type (uint16/uint32/int). The BufferView's
            // ElementType preserves the format (backend derives it — no shader-side
            // decoding). Deduped by geometry identity (first attr + index buffer).
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
            // per-draw firstInstance (local instance is 0, so gl_InstanceIndex =
            // firstInstance = the draw index) — portable to MoltenVK. On GL,
            // gl_InstanceID omits baseInstance, so firstInstance routing breaks;
            // GL uses gl_DrawID (GL 4.6). Instanced sub-draws always need gl_DrawID.
            let isGL = runtime.GetType().FullName.Contains("Aardvark.Rendering.GL")
            let anyInstanced = baseEntries |> Array.exists (fun e -> e.InstanceCount > 1)
            let useDrawId = isGL || anyInstanced
            let slot : Expr<int> =
                if useDrawId then <@ getDrawId() @>
                else Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.InstanceId)
            if not useDrawId then
                for di in 0 .. baseEntries.Length - 1 do baseEntries.[di].FirstInstance <- di

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
            ro.Surface          <- Surface.Effect (rewrite slot nameToField fieldStride standardDerivedRules effect)
            ro.DrawCalls        <- DrawCalls.Indirect indirect
            ro.VertexAttributes <- AttributeProvider.ofList [ for ai in 0 .. attrTypes.Length - 1 -> let (sym, et, _) = attrTypes.[ai] in sym, packedView (packedAttr.[ai].ToArray()) et ]
            ro.Indices          <- Some (packedView (packedIdx.ToArray()) idxType)

            let arenaU   = ((arena :> aval<IBackendBuffer>) |> AVal.map (fun b -> b :> IBuffer)) :> IAdaptiveValue
            let headersU = AVal.constant headers :> IAdaptiveValue
            ro.Uniforms <-
                { new IUniformProvider with
                    member _.TryGetUniform(s, name) =
                        if name = symData then ValueSome arenaU
                        elif name = symHeaders then ValueSome headersU
                        elif Set.contains name heapSyms then ValueNone
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
                        | ValueSome bv -> name + ":" + bv.ElementType.FullName
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
                    (match ro.Indices with Some bv -> isHostTight bv | None -> false) &&
                    (e.Inputs |> Map.forall (fun name _ ->
                        match ro.VertexAttributes.TryGetAttribute (Symbol.Create name) with
                        | ValueSome bv -> isHostTight bv
                        | ValueNone -> false)) &&
                    (names |> Array.forall (fun n ->
                        match ro.Uniforms.TryGetUniform(scope, Symbol.Create n) with
                        | ValueSome v -> packable.Contains v.ContentType
                        | ValueNone -> false))
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

    // ── Bindless geometry: vertex-pulling from GPU buffers by handle ────
    // GPU-resident geometry can't be CPU-packed; instead each object's buffers are
    // referenced by HANDLE (one element of a bindless SSBO array) and the vertex
    // shader PULLS attributes — no fixed-function vertex input, no copy. The per-RO
    // handle is gl_DrawID; the vertex is gl_VertexIndex; for indexed meshes the
    // index is pulled too: HeapPositions[drawId].data[ HeapIndex[drawId].data[vid] ].
    // The effect's vertex-attribute reads (Positions/Normals) are rewritten to these
    // pulls via Shader.substituteReads (ParameterKind.Input). Requires the device's
    // bindless storage-buffer arrays (descriptor indexing).

    let private vidExpr : Expr = Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.VertexId)

    let private pullPositions : Expr =
        <@@ let h = getDrawId() in (uniform.HeapPositions.[h].[ uniform.HeapIndex.[h].[ (%%vidExpr : int) ] ]).XYZ @@>
    let private pullNormals : Expr =
        <@@ let h = getDrawId() in (uniform.HeapNormals.[h].[ uniform.HeapIndex.[h].[ (%%vidExpr : int) ] ]).XYZ @@>

    /// EXPERIMENTAL / WIP. Render N objects whose geometry lives in per-object
    /// buffers referenced by handle (bindless), vertex-PULLED in the (rewritten)
    /// shader — no vertex buffers bound, no packing/copy. The single-SSBO-array
    /// primitive is proven (Golden.ssboArrayTest), but this multi-array path
    /// (Positions+Normals+Index in three unbounded SSBO arrays in one set) renders
    /// incorrectly — only the highest-binding unbounded array gets a variable/
    /// partially-bound descriptor, so the others mis-bind. KNOWN TODO: (a) handle
    /// >1 unbounded SSBO array per set; (b) per the design, do NOT assume V4f —
    /// store flat float[] and decode by the attribute's actual component count;
    /// (c) route via firstInstance (portable) not gl_DrawID (MoltenVK lacks it).
    let bindless (runtime : IRuntime) (mode : IndexedGeometryMode) (effect : Effect)
                 (positions : V3f[][]) (normals : V3f[][]) (indices : int[][])
                 (viewProj : aval<Trafo3d>) : ISg =
        checkSupport false runtime
        let n = positions.Length
        // pad to V4f: std430 vec3[] elements are 16-byte aligned, so a packed V3f[]
        // would be read misaligned. V4f[] (16-byte) matches the vec4[] std430 stride.
        let posBufs = positions |> Array.map (fun a -> ArrayBuffer (a |> Array.map (fun p -> V4f(p, 1.0f))) :> IBuffer)
        let norBufs = normals   |> Array.map (fun a -> ArrayBuffer (a |> Array.map (fun nv -> V4f(nv, 0.0f))) :> IBuffer)
        let idxBufs = indices   |> Array.map (fun a -> ArrayBuffer a :> IBuffer)

        // rewrite the vertex shader's Positions/Normals INPUT reads into bindless pulls
        let eff =
            effect |> Effect.map (fun s ->
                s |> Shader.substituteReads (fun kind _ name _ _ ->
                    match kind, name with
                    | ParameterKind.Input, "Positions" -> Some pullPositions
                    | ParameterKind.Input, "Normals"   -> Some pullNormals
                    | _ -> None))

        // one non-indexed sub-draw per object: gl_VertexIndex runs 0 .. indexCount-1
        // (the index itself is pulled from HeapIndex), gl_DrawID = the object handle.
        let indirect =
            Array.init n (fun di -> DrawCallInfo(FaceVertexCount = indices.[di].Length, FirstIndex = 0, BaseVertex = 0, FirstInstance = 0, InstanceCount = 1))
            |> IndirectBuffer.ofArray

        Sg.indirectDraw mode (AVal.constant indirect)
        |> Sg.uniform "HeapPositions" (AVal.constant posBufs)
        |> Sg.uniform "HeapNormals"   (AVal.constant norBufs)
        |> Sg.uniform "HeapIndex"     (AVal.constant idxBufs)
        |> Sg.uniform "ViewProjTrafo" (viewProj |> AVal.map (fun t -> M44f.op_Explicit t.Forward))
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
