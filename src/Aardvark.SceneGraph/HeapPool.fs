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

/// Storage-buffer accessors for the heap arena, for shaders that read it
/// directly (the per-draw header table and the float payload arena). The heap
/// rewrite injects these automatically; expose them so custom/compute shaders
/// can address the arena too.
[<AutoOpen>]
module HeapUniforms =
    type UniformScope with
        member x.HeapData    : float32[] = uniform?StorageBuffer?HeapData
        member x.HeapHeaders : int[]     = uniform?StorageBuffer?HeapHeaders

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
    /// Whether `runtime` can run the heap path at all (per-draw base-instance
    /// multidraw routing). Bindless per-object textures additionally need
    /// `runtime.SupportsUnboundedSamplerArrays`.
    let isSupported (runtime : IRuntime) = runtime.SupportsBaseInstanceMultiDraw

    /// Throw a clear error if `runtime` cannot run the heap path. Pass
    /// `textures = true` to also require unbounded (bindless) sampler arrays.
    let checkSupport (textures : bool) (runtime : IRuntime) =
        if not runtime.SupportsBaseInstanceMultiDraw then
            failwith "Heap: runtime does not support indirect multi-draw with base-instance routing (required for per-draw heap rendering). Currently only the Vulkan backend on capable hardware is supported (GL lacks a base-instance shader intrinsic)."
        if textures && not runtime.SupportsUnboundedSamplerArrays then
            failwith "Heap: runtime does not support unbounded (bindless) sampler arrays (descriptor indexing); per-object textures via the heap are unavailable on this device."

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

    /// Rewrite an effect so that the uniforms named in `layout` become arena
    /// gathers indexed by gl_InstanceIndex; everything else stays a UBO.
    let private rewrite (layout : Map<string, int>) (fieldStride : int) (rules : Map<string, DerivedRule>) (e : Effect) =
        let iid : Expr<int> = Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.InstanceId)
        let off (fi : int) : Expr<int> = <@ uniform.HeapHeaders.[ %iid * %(cint fieldStride) + %(cint fi) ] @>
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

        let indirect =
            Array.init draws.Length (fun i ->
                DrawCallInfo(FaceVertexCount = index.Length, FirstIndex = 0, BaseVertex = 0,
                             FirstInstance = i, InstanceCount = 1))
            |> IndirectBuffer.ofArray

        let effect = rewrite nameToField fieldStride standardDerivedRules effect

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

    /// Number of buckets produced by the most recent `ofRenderObjects` evaluation
    /// (diagnostic / for logging).
    let mutable lastBucketCount = 0

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

            // Pack per-RO geometry into shared buffers. Deduped by (positions,
            // index) buffer identity so SHARED geometry is packed once; VARIED
            // geometry gets its own packed range. Per-RO firstIndex/baseVertex/
            // count drive the indirect buffer (one sub-draw per RO).
            let hostArray (bv : BufferView) : System.Array =
                match bv.Buffer.GetValue() with
                | :? ArrayBuffer as a -> a.Data
                | b -> failwithf "Heap.ofRenderObjects: expected host ArrayBuffer geometry, got %A" (b.GetType())
            let attr (ro : RenderObject) (s : Symbol) =
                match ro.VertexAttributes.TryGetAttribute s with
                | ValueSome bv -> bv
                | ValueNone -> failwithf "Heap.ofRenderObjects: RO missing vertex attribute %A" s

            let packedPos = System.Collections.Generic.List<V3f>()
            let packedNor = System.Collections.Generic.List<V3f>()
            let packedIdx = System.Collections.Generic.List<int>()
            let geomCache = System.Collections.Generic.Dictionary<struct(obj * obj), struct(int * int * int)>(HashIdentity.Structural)

            let indirect =
                ros |> Array.mapi (fun i ro ->
                    let posBV = attr ro DefaultSemantic.Positions
                    let idxBV = match ro.Indices with Some bv -> bv | None -> failwith "Heap.ofRenderObjects: RO has no index buffer (v1 requires indexed geometry)"
                    let key = struct(posBV.Buffer :> obj, idxBV.Buffer :> obj)
                    let (struct(firstIndex, baseVertex, count)) =
                        match geomCache.TryGetValue key with
                        | true, r -> r
                        | _ ->
                            let pos = hostArray posBV :?> V3f[]
                            let nor = hostArray (attr ro DefaultSemantic.Normals) :?> V3f[]
                            let idx = hostArray idxBV :?> int[]
                            let r = struct(packedIdx.Count, packedPos.Count, idx.Length)
                            packedPos.AddRange pos
                            packedNor.AddRange nor
                            packedIdx.AddRange idx
                            geomCache.[key] <- r
                            r
                    DrawCallInfo(FaceVertexCount = count, FirstIndex = firstIndex, BaseVertex = baseVertex, FirstInstance = i, InstanceCount = 1))
                |> IndirectBuffer.ofArray

            let bvOf (arr : System.Array) t = BufferView(AVal.constant (ArrayBuffer(arr) :> IBuffer), t)
            let ro = RenderObject.Clone ro0
            ro.Surface          <- Surface.Effect (rewrite nameToField fieldStride standardDerivedRules effect)
            ro.DrawCalls        <- DrawCalls.Indirect (AVal.constant indirect)
            ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, bvOf (packedPos.ToArray()) typeof<V3f>
                                                              DefaultSemantic.Normals,   bvOf (packedNor.ToArray()) typeof<V3f> ]
            ro.Indices          <- Some (bvOf (packedIdx.ToArray()) typeof<int>)

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

        objects
        |> ASet.toAVal
        |> ASet.bind (fun ros ->
            let buckets =
                ros
                |> HashSet.toArray
                |> Array.choose (fun ro -> match ro with :? RenderObject as r -> Some r | _ -> None)
                |> Array.groupBy (fun r -> match r.Surface with | Surface.Effect e -> e.Id | _ -> "?")
                |> Array.map (fun (_, g) -> buildBucket g)
            lastBucketCount <- buckets.Length
            ASet.ofArray buckets)

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

        // header-less rewrite: uniform -> HeapData[ iid*stride + fieldOffset ]
        let effect' =
            let iid : Expr<int> = Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.InstanceId)
            effect |> Effect.substituteUniforms (fun name typ _ _ ->
                match fieldOffset.TryGetValue name with
                | true, fo -> Some (gatherFor typ <@ %iid * %(cint dataStride) + %(cint fo) @>)
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
                entries.[slot] <- DrawCallInfo(FaceVertexCount = index.Length, FirstIndex = 0, BaseVertex = 0, FirstInstance = slot, InstanceCount = 1)
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
                    entries.[slot] <- DrawCallInfo(FaceVertexCount = 0, FirstInstance = slot, InstanceCount = 0)
                freeList.Push slot
                version.Value <- version.Value + 1)

        member _.Count = slotWriters.Count
        member _.RenderObject = ro :> IRenderObject
        member x.Sg = Sg.renderObjectSet (ASet.single x.RenderObject)
