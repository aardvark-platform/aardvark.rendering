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
