namespace HeapSpike

// Phase-1 heap module (still SG-level; not yet wired into CommandTask).
//
// Generalises the phase-0 spike:
//   * type-driven rewrite (any float-family uniform type, derived from the
//     effect's own usage — no hardcoded names);
//   * aval-identity deduplication (draws sharing an aval share one arena
//     region — the "10k ROs, one ViewProjTrafo allocation" property);
//   * a reactive arena (AVal) — when any per-draw aval marks, the arena
//     re-packs (offsets/headers stay constant: refs never move).
//
// Geometry is shared across the bucket's draws for now (per-draw geometry
// packing is a later refinement); draws differ only by their per-draw
// uniform avals.

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open FShade
open FShade.Imperative
open Microsoft.FSharp.Quotations

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

    /// Type-driven gather: given the base element offset in HeapData, build
    /// the expression that reconstructs a value of `typ`.
    let private gatherFor (typ : System.Type) (off : Expr<int>) : Expr =
        if   typ = typeof<float32> then <@ uniform.HeapData.[%off] @>.Raw
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

    /// Rewrite an effect so that the uniforms named in `layout` become arena
    /// gathers indexed by gl_InstanceIndex; everything else stays a UBO.
    let private rewrite (layout : Map<string, int>) (fieldStride : int) (e : Effect) =
        let iid : Expr<int> = Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.InstanceId)
        e |> Effect.substituteUniforms (fun name typ _ _ ->
            match Map.tryFind name layout with
            | None -> None
            | Some fi ->
                let off : Expr<int> = <@ uniform.HeapHeaders.[ %iid * %(cint fieldStride) + %(cint fi) ] @>
                Some (gatherFor typ off))

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

        let effect = rewrite nameToField fieldStride effect

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
        else failwithf "Heap: unsupported per-draw uniform content type %A" t

    /// One bucket draw count, for reporting.
    let mutable lastBucketCount = 0

    let ofRenderObjects (heapNames : Set<string>) (objects : aset<IRenderObject>) : aset<IRenderObject> =
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
            let distinct = System.Collections.Generic.List<IAdaptiveValue * int * (obj -> float32[] -> int -> unit)>()
            let mutable cursor = 0
            let headers = Array.zeroCreate<int> (ros.Length * fieldStride)
            ros |> Array.iteri (fun di ro ->
                for n in names do
                    let (fi, sz, pk) = info.[n]
                    let av = uni ro n
                    let off =
                        match regionOf.TryGetValue av with
                        | true, o -> o
                        | _ -> let o = cursor in cursor <- cursor + sz; regionOf.[av] <- o; distinct.Add(av, o, pk); o
                    headers.[di * fieldStride + fi] <- off)
            let totalF = cursor

            let arena =
                AVal.custom (fun t ->
                    let a = Array.zeroCreate<float32> totalF
                    for (av, o, pk) in distinct do pk (av.GetValueUntyped t) a o
                    a)

            let fvc, fi0, bv0 =
                match ro0.DrawCalls with
                | DrawCalls.Direct calls -> let c = (AVal.force calls).[0] in c.FaceVertexCount, c.FirstIndex, c.BaseVertex
                | DrawCalls.Indirect _ -> failwith "Heap.ofRenderObjects: indirect input not supported (v1)"

            let indirect =
                Array.init ros.Length (fun i ->
                    DrawCallInfo(FaceVertexCount = fvc, FirstIndex = fi0, BaseVertex = bv0, FirstInstance = i, InstanceCount = 1))
                |> IndirectBuffer.ofArray

            let ro = RenderObject.Clone ro0
            ro.Surface     <- Surface.Effect (rewrite nameToField fieldStride effect)
            ro.DrawCalls   <- DrawCalls.Indirect (AVal.constant indirect)
            ro.VertexAttributes <- ro0.VertexAttributes
            ro.Indices     <- ro0.Indices

            let arenaU   = arena :> IAdaptiveValue
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
