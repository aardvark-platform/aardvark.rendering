namespace HeapSpike

// Churn / compaction golden test (Vulkan): heavy add/remove against ONE heap
// storage, validated pixel-by-pixel against a classic (non-heap) render of the
// SAME membership after every step. Specifically exercises the mirror-less
// arena's riskiest surfaces:
//   * threshold-triggered page compaction (PageArena.Compact → QueueMoves →
//     device-side temp-buffer bounce on the next flush),
//   * freed-block re-allocation in the SAME cycle (remove+add in one transact →
//     upload regions overlapping a compaction move / earlier writes ⇒ ordered
//     batch splits),
//   * post-compaction shrink + follow-up growth.
// Every object gets UNIQUE geometry (fresh arrays) so removals genuinely free
// arena bytes and re-adds genuinely re-allocate them.

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive

module Churn =

    // max per-channel abs delta + differing / non-background pixel counts
    let private diff (a : PixImage<uint8>) (b : PixImage<uint8>) =
        let am = a.GetMatrix<C4b>()
        let bm = b.GetMatrix<C4b>()
        let mutable maxDelta = 0
        let mutable nDiff = 0L
        let mutable nNonBg = 0L
        am.ForeachCoord(fun (c : V2l) ->
            let p = am.[c]
            let q = bm.[c]
            let d = max (max (abs (int p.R - int q.R)) (abs (int p.G - int q.G)))
                        (max (abs (int p.B - int q.B)) (abs (int p.A - int q.A)))
            if d > 0 then nDiff <- nDiff + 1L
            if d > maxDelta then maxDelta <- d
            if p.R <> 0uy || p.G <> 0uy || p.B <> 0uy then nNonBg <- nNonBg + 1L)
        maxDelta, nDiff, nNonBg, int64 am.Size.X * int64 am.Size.Y

    let run () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))

        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 42.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan |]
        let effect = FShade.Effect.compose [ FShade.Effect.ofFunction Shaders.shade; FShade.Effect.ofFunction Shaders.shadeFrag ]

        // UNIQUE geometry per object: an indexed UV sphere with per-object vertex
        // count (fresh arrays ⇒ no value-level dedup; removals free real bytes).
        let mkRO (i : int) : IRenderObject =
            let rand = RandomSystem(i * 7919 + 13)
            let tess = 12 + rand.UniformInt 14                       // 12..25 ⇒ ~300..1300 verts
            let sphere = IndexedGeometryPrimitives.Sphere.solidPhiThetaSphere (Sphere3d(V3d.Zero, 0.55)) tess C4b.White
            let g = sphere.ToIndexed()
            let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]> |> Array.copy
            let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]> |> Array.copy
            let index     = g.IndexArray |> unbox<int[]> |> Array.copy
            let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
            let s = 28
            let p = V3d(float (i % s - s/2) * 1.2, float (i / s % s - s/2) * 1.2, float (i / (s*s)) * 1.4)
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect effect
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>
                                                              DefaultSemantic.Normals, bv normals typeof<V3f> ]
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj ]
            ro :> IRenderObject

        // cache: the CLASSIC control render must use the SAME RO instances
        let ros = System.Collections.Generic.Dictionary<int, IRenderObject>()
        let roOf i = match ros.TryGetValue i with
                     | true, r -> r
                     | _ -> let r = mkRO i in ros.[i] <- r; r

        let live = cset<int> [ 0 .. 599 ]
        let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (live |> ASet.map roOf)
        use heapTask = runtime.CompileRender(signature, heapObjs)
        let heapOut = heapTask |> RenderTask.renderToColor size
        heapOut.Acquire()

        let renderHeap () = heapOut.GetValue().Download().AsPixImage<uint8>()
        let renderClassic () =
            use t = runtime.CompileRender(signature, live |> ASet.map roOf)
            let out = t |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>()
            finally out.Release()

        let mutable pass = true
        let check (label : string) =
            let h = renderHeap ()
            let c = renderClassic ()
            let maxDelta, nDiff, nNonBg, total = diff c h
            let ok = maxDelta <= 1 && nNonBg > 1000L
            if ok then Log.line "churn[%s]: PASS (coverage=%d, compactions=%d)" label nNonBg Heap.compactionCount
            else Log.warn "churn[%s]: FAIL maxDelta=%d diff=%d/%d coverage=%d" label maxDelta nDiff total nNonBg
            pass <- pass && ok

        check "initial-600"

        // remove 60% — frees well past the waste floor ⇒ page compaction fires
        let c0 = Heap.compactionCount
        transact (fun () -> live.ExceptWith [ for i in 0 .. 599 do if i % 5 < 3 then yield i ])
        check "removed-60pct"
        if Heap.compactionCount > c0 then Log.line "churn: compaction fired (%d -> %d)" c0 Heap.compactionCount
        else Log.warn "churn: WARNING - no compaction fired after 60%% removal (waste below floor?)"

        // re-add fresh objects (fresh geometry -> re-alloc into freed + new space)
        transact (fun () -> live.UnionWith [ 600 .. 899 ])
        check "readded-300-new"

        // SAME-CYCLE remove+add: freed blocks re-allocated in one transact — the
        // ordered-batch (overlap split) path must keep copy order defined
        for round in 1 .. 3 do
            transact (fun () ->
                let dead = live |> ASet.force |> Seq.filter (fun i -> i % 3 = round % 3) |> Seq.toArray
                live.ExceptWith dead
                // drop the RO cache for removed ids so re-adds build FRESH geometry
                for i in dead do ros.Remove i |> ignore
                live.UnionWith [ 900 + round * 200 .. 900 + round * 200 + 149 ])
            check (sprintf "mixed-round-%d" round)

        // shrink hard, then grow again (post-compaction shrink + regrowth)
        let c1 = Heap.compactionCount
        transact (fun () ->
            let keep = live |> ASet.force |> Seq.filter (fun i -> i % 10 = 0) |> Set.ofSeq
            live.ExceptWith (live |> ASet.force |> Seq.filter (fun i -> not (Set.contains i keep)) |> Seq.toArray))
        check "shrunk-to-10pct"
        transact (fun () -> live.UnionWith [ 2000 .. 2399 ])
        check "regrown-400"
        Log.line "churn: total compactions %d (%d before shrink phase)" Heap.compactionCount c1

        heapOut.Release()
        if pass then Log.line "churn: ALL PASS (add/remove/compaction/same-cycle-reuse == classic)"
        else Log.warn "churn: FAILED"
        pass
