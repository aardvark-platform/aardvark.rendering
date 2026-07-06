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

    /// pick-partition probe: `ofRenderObjectsPicking` must render BOTH partitions —
    /// members carrying the `HeapPickId` marker (pick heap) AND unmarked members
    /// (plain heap, dom routes those to the base pass). Regression for the dom
    /// NoEvents pick-through fix: the unmarked partition must not vanish.
    let pickSplit () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(512, 512))
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 12.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let effect = FShade.Effect.compose [ FShade.Effect.ofFunction Shaders.shade; FShade.Effect.ofFunction Shaders.shadeFrag ]
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.8)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let mk (i : int) (marked : bool) : IRenderObject =
            let ro = RenderObject()
            ro.Surface <- Surface.Effect effect
            ro.Mode <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>
                                                              DefaultSemantic.Normals, bv normals typeof<V3f> ]
            ro.Indices <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms <- UniformProvider.ofList [
                yield Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation(float (i % 4) * 1.2 - 1.8, float (i / 4) * 1.2 - 0.6, 0.0)).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                yield Symbol.Create "HeapColor",      (AVal.constant ((if marked then C4f.DodgerBlue else C4f.Orange).ToV4f()) :> IAdaptiveValue)
                yield Symbol.Create "ViewProjTrafo",  viewProj
                if marked then yield Symbol.Create "HeapPickId", (AVal.constant i :> IAdaptiveValue) ]
            ro :> IRenderObject
        // 4 marked (blue) + 4 unmarked (orange)
        let input = ASet.ofList [ for i in 0 .. 7 -> mk i (i % 2 = 0) ]
        let heaped = Heap.ofRenderObjectsPicking (runtime.CreateHeapStorage()) ignore input
        use task = runtime.CompileRender(signature, heaped)
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        let pix = try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let m = pix.GetMatrix<C4b>()
        let mutable blue = 0
        let mutable orange = 0
        m.ForeachCoord(fun (c : V2l) ->
            let p = m.[c]
            if int p.B > int p.R + 40 && int p.B > 60 then blue <- blue + 1
            elif int p.R > int p.B + 40 && int p.R > 60 then orange <- orange + 1)
        Log.line "pickSplit: blue px=%d orange px=%d" blue orange
        let ok = blue > 1000 && orange > 1000
        if ok then Log.line "pickSplit: PASS (both partitions render)"
        else Log.warn "pickSplit: FAIL (a partition is missing)"
        ok

    /// dom-shaped variant: split the pick-heap's SDRs by IsPickable into TWO
    /// tasks over SHARED attachments (pick task first, base task second) — the
    /// exact SceneHandler/PickProducer arrangement. The unpickable partition
    /// must still be visible in the shared color buffer.
    module private PS =
        open FShade
        type FIn = { [<Color>] c : V4f }
        type FOut = { [<Color>] c : V4f; [<Semantic("PickId")>] pid : V4f }
        let write (v : FIn) =
            fragment {
                let pid : int = uniform?HeapPickId
                let r : FOut = { c = v.c; pid = V4f(float32 pid, 0.0f, 0.0f, 0.0f) }
                return r
            }

    let pickSplit2 () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let baseSig =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let pickSym = Symbol.Create "PickId"
        let pickSig =
            runtime.CreateFramebufferSignature(
                Map.add baseSig.ColorAttachmentSlots { Name = pickSym; Format = TextureFormat.Rgba32f } baseSig.ColorAttachments,
                baseSig.DepthStencilAttachment)
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 12.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let effect = FShade.Effect.compose [ FShade.Effect.ofFunction Shaders.shade; FShade.Effect.ofFunction Shaders.shadeFrag ]
        // marked members WRITE PickId (dom's HeapNode composes the heap pick chain)
        let pickEffect = FShade.Effect.compose [ FShade.Effect.ofFunction Shaders.shade; FShade.Effect.ofFunction Shaders.shadeFrag; FShade.Effect.ofFunction PS.write ]
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.8)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let mk (i : int) (marked : bool) : IRenderObject =
            let ro = RenderObject()
            ro.Surface <- Surface.Effect (if marked then pickEffect else effect)
            ro.Mode <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>
                                                              DefaultSemantic.Normals, bv normals typeof<V3f> ]
            ro.Indices <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms <- UniformProvider.ofList [
                yield Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation(float (i % 4) * 1.2 - 1.8, float (i / 4) * 1.2 - 0.6, 0.0)).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                yield Symbol.Create "HeapColor",      (AVal.constant ((if marked then C4f.DodgerBlue else C4f.Orange).ToV4f()) :> IAdaptiveValue)
                yield Symbol.Create "ViewProjTrafo",  viewProj
                if marked then yield Symbol.Create "HeapPickId", (AVal.constant i :> IAdaptiveValue) ]
            ro :> IRenderObject
        let input = ASet.ofList [ for i in 0 .. 7 -> mk i (i % 2 = 0) ]
        let heaped = Heap.ofRenderObjectsPicking (runtime.CreateHeapStorage()) ignore input
        let isPickableSdr (ro : IRenderObject) =
            match ro with
            | :? SignatureDependentRenderObject as s -> s.IsPickable
            | _ -> false
        let pickables    = heaped |> ASet.filter isPickableSdr
        let unpickables  = heaped |> ASet.filter (isPickableSdr >> not)
        use pickTask = runtime.CompileRender(pickSig, pickables)
        use baseTask = runtime.CompileRender(baseSig, unpickables)
        // shared attachments (dom: nf/pf over the same renderbuffers)
        let size = V2i(512, 512)
        let colorTex = runtime.CreateTexture2D(size, TextureFormat.Rgba8, 1, 1)
        let pickTex  = runtime.CreateTexture2D(size, TextureFormat.Rgba32f, 1, 1)
        let depthTex = runtime.CreateTexture2D(size, TextureFormat.Depth24Stencil8, 1, 1)
        let pf = runtime.CreateFramebuffer(pickSig, [ DefaultSemantic.Colors, colorTex.[TextureAspect.Color, 0, 0] :> IFramebufferOutput
                                                      pickSym, pickTex.[TextureAspect.Color, 0, 0] :> IFramebufferOutput
                                                      DefaultSemantic.DepthStencil, depthTex.[TextureAspect.DepthStencil, 0, 0] :> IFramebufferOutput ] |> Map.ofList)
        let nf = runtime.CreateFramebuffer(baseSig, [ DefaultSemantic.Colors, colorTex.[TextureAspect.Color, 0, 0] :> IFramebufferOutput
                                                      DefaultSemantic.DepthStencil, depthTex.[TextureAspect.DepthStencil, 0, 0] :> IFramebufferOutput ] |> Map.ofList)
        use clearTask = runtime.CompileClear(pickSig, clear { color C4f.Black; depth 1.0; stencil 0 })
        clearTask.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer pf)
        pickTask.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer pf)
        baseTask.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer nf)
        let pix = runtime.Download(colorTex).AsPixImage<uint8>()
        let m = pix.GetMatrix<C4b>()
        let mutable blue = 0
        let mutable orange = 0
        m.ForeachCoord(fun (c : V2l) ->
            let p = m.[c]
            if int p.B > int p.R + 40 && int p.B > 60 then blue <- blue + 1
            elif int p.R > int p.B + 40 && int p.R > 60 then orange <- orange + 1)
        Log.line "pickSplit2: blue px=%d orange px=%d" blue orange
        let ok = blue > 1000 && orange > 1000
        if ok then Log.line "pickSplit2: PASS (two-task split renders both)"
        else Log.warn "pickSplit2: FAIL (a partition is missing)"
        ok
