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
        use app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication()
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
        use app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication()
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
        use app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication()
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
        // dom shape: TWO tasks over shared attachments; simplified — both use the
        // BASE signature so no effect needs to write PickId (the question here is
        // whether the unpickable partition's task draws AT ALL)
        use pickTask = runtime.CompileRender(baseSig, pickables)
        use baseTask = runtime.CompileRender(baseSig, unpickables)
        // shared attachments (dom: nf/pf over the same renderbuffers)
        let size = V2i(512, 512)
        let colorTex = runtime.CreateTexture2D(size, TextureFormat.Rgba8, 1, 1)
        let pickTex  = runtime.CreateTexture2D(size, TextureFormat.Rgba32f, 1, 1)
        let depthTex = runtime.CreateTexture2D(size, TextureFormat.Depth24Stencil8, 1, 1)
        let pf = runtime.CreateFramebuffer(baseSig, [ DefaultSemantic.Colors, colorTex.[TextureAspect.Color, 0, 0] :> IFramebufferOutput
                                                      DefaultSemantic.DepthStencil, depthTex.[TextureAspect.DepthStencil, 0, 0] :> IFramebufferOutput ] |> Map.ofList)
        let nf = runtime.CreateFramebuffer(baseSig, [ DefaultSemantic.Colors, colorTex.[TextureAspect.Color, 0, 0] :> IFramebufferOutput
                                                      DefaultSemantic.DepthStencil, depthTex.[TextureAspect.DepthStencil, 0, 0] :> IFramebufferOutput ] |> Map.ofList)
        use clearTask = runtime.CompileClear(baseSig, clear { color C4f.Black; depth 1.0; stencil 0 })
        // a build expanding inside a task pull flushes ONE FRAME LATER (the
        // stage-while-clean Touch) — render two frames like a real loop would
        for _ in 1 .. 2 do
            clearTask.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer pf)
            pickTask.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer pf)
            baseTask.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer nf)
            System.Threading.Thread.Sleep 50
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

    /// ADAPTIVE-GEOMETRY probe: everything declared adaptive must be respected —
    /// non-constant attribute / index / draw-call avals re-upload O(changed):
    ///   * colors flip per-vertex array <-> length-1 SINGLETON (size-change
    ///     realloc + per-slot header re-bake; the heap broadcasts the singleton),
    ///   * positions re-stage IN PLACE (same byte length),
    ///   * whole-geometry swap to a different tessellation (attrs + index all
    ///     realloc; the index length drives the slot's draw-record vertex count),
    ///   * a NON-indexed member's fvc shrinks (record-only update).
    /// Validated pixel-by-pixel against a classic render after every step. The
    /// classic control uses PAIRED ROs sharing the same adaptive cells — except
    /// colors, where the classic side expands the singleton to a full array
    /// (classic pipelines can't broadcast a 1-element vertex attribute).
    let dynGeom () =
        Aardvark.Init()
        use app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication()
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 22.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let effect = FShade.Effect.compose [ FShade.Effect.ofFunction Shaders.shadeVtx; FShade.Effect.ofFunction Shaders.shadeFrag ]
        let palette = [| C4b(230uy,80uy,60uy); C4b(90uy,200uy,90uy); C4b(70uy,120uy,230uy); C4b(240uy,200uy,60uy) |]
        let lightBlue = C4b(115uy, 199uy, 255uy)

        let sphereArrays (tess : int) =
            let g = (IndexedGeometryPrimitives.Sphere.solidPhiThetaSphere (Sphere3d(V3d.Zero, 0.55)) tess C4b.White).ToIndexed()
            let pos = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]> |> Array.copy
            let nrm = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]> |> Array.copy
            let idx = g.IndexArray |> unbox<int[]> |> Array.copy
            pos, nrm, idx

        let n = 48
        // per-object adaptive cells (SHARED by the heap RO and its classic twin)
        let geo   = Array.init n (fun i -> sphereArrays (12 + i % 10))
        let posC  = Array.init n (fun i -> let (p, _, _) = geo.[i] in cval p)
        let nrmC  = Array.init n (fun i -> let (_, nn, _) = geo.[i] in cval nn)
        let idxC  = Array.init n (fun i -> let (_, _, ix) = geo.[i] in cval ix)
        let selC  = Array.init n (fun _ -> cval false)
        let fullCols = Array.init n (fun i ->
            let (p, _, _) = geo.[i]
            Array.init p.Length (fun v -> palette.[(i + v / 16) % palette.Length]))

        let bvOf (av : aval<'a[]>) (t : System.Type) =
            BufferView(av |> AVal.map (fun a -> ArrayBuffer(a :> System.Array) :> IBuffer), t)

        let mkPair (i : int) : IRenderObject * IRenderObject =
            let s = 8
            let p = V3d(float (i % s - s/2) * 1.3, float (i / s - s/2) * 1.3, 0.0)
            let calls = idxC.[i] |> AVal.map (fun ix -> [| DrawCallInfo(FaceVertexCount = ix.Length, InstanceCount = 1) |])
            let mk (cols : aval<C4b[]>) =
                let ro = RenderObject()
                ro.Surface <- Surface.Effect effect
                ro.Mode    <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- AttributeProvider.ofList [
                    DefaultSemantic.Positions, bvOf posC.[i] typeof<V3f>
                    DefaultSemantic.Normals,   bvOf nrmC.[i] typeof<V3f>
                    DefaultSemantic.Colors,    bvOf cols typeof<C4b> ]
                ro.Indices   <- Some (bvOf idxC.[i] typeof<int>)
                ro.DrawCalls <- DrawCalls.Direct calls
                ro.Uniforms  <- UniformProvider.ofList [
                    Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                    Symbol.Create "ViewProjTrafo",  viewProj ]
                ro :> IRenderObject
            // heap: SINGLETON on select (the heap broadcasts via vid % length);
            // classic twin: full-size array of the same color (no broadcast there)
            let heapCols    = (selC.[i], posC.[i]) ||> AVal.map2 (fun sel _ -> if sel then [| lightBlue |] else fullCols.[i])
            let classicCols = (selC.[i], posC.[i]) ||> AVal.map2 (fun sel p -> if sel then Array.create p.Length lightBlue else fullCols.[i])
            mk heapCols, mk classicCols

        let pairs = Array.init n mkPair
        // one NON-indexed member: expanded triangles, adaptive fvc (record-only path)
        let flatFvc =
            let (p0, n0, ix) = sphereArrays 14
            let fp = ix |> Array.map (fun j -> p0.[j])
            let fn = ix |> Array.map (fun j -> n0.[j])
            let fc = Array.init fp.Length (fun v -> palette.[v / 12 % palette.Length])
            let fvc = cval fp.Length
            let calls = fvc |> AVal.map (fun k -> [| DrawCallInfo(FaceVertexCount = k, InstanceCount = 1) |])
            let mk () =
                let ro = RenderObject()
                ro.Surface <- Surface.Effect effect
                ro.Mode    <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- AttributeProvider.ofList [
                    DefaultSemantic.Positions, BufferView(AVal.constant (ArrayBuffer fp :> IBuffer), typeof<V3f>)
                    DefaultSemantic.Normals,   BufferView(AVal.constant (ArrayBuffer fn :> IBuffer), typeof<V3f>)
                    DefaultSemantic.Colors,    BufferView(AVal.constant (ArrayBuffer fc :> IBuffer), typeof<C4b>) ]
                ro.DrawCalls <- DrawCalls.Direct calls
                ro.Uniforms  <- UniformProvider.ofList [
                    Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation (V3d(0.0, 5.5, 0.0))).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                    Symbol.Create "ViewProjTrafo",  viewProj ]
                ro :> IRenderObject
            fvc, mk (), mk ()

        let (fvcCell, flatHeap, flatClassic) = flatFvc
        let heapSet    = ASet.ofArray (Array.append (pairs |> Array.map fst) [| flatHeap |])
        let classicSet = ASet.ofArray (Array.append (pairs |> Array.map snd) [| flatClassic |])

        let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) heapSet
        use heapTask = runtime.CompileRender(signature, heapObjs)
        let heapOut = heapTask |> RenderTask.renderToColor size
        heapOut.Acquire()
        use classicTask = runtime.CompileRender(signature, classicSet)
        let classicOut = classicTask |> RenderTask.renderToColor size
        classicOut.Acquire()

        let mutable pass = true
        let check (label : string) =
            let h = heapOut.GetValue().Download().AsPixImage<uint8>()
            let c = classicOut.GetValue().Download().AsPixImage<uint8>()
            let maxDelta, nDiff, nNonBg, total = diff c h
            let ok = maxDelta <= 1 && nNonBg > 1000L
            if ok then Log.line "dynGeom[%s]: PASS (coverage=%d)" label nNonBg
            else Log.warn "dynGeom[%s]: FAIL maxDelta=%d diff=%d/%d coverage=%d" label maxDelta nDiff total nNonBg
            pass <- pass && ok

        check "initial"

        // 1. SELECT half: heap colors flip to a length-1 singleton (size-change
        //    realloc + header re-bake), classic twin gets the expanded array
        transact (fun () -> for i in 0 .. n - 1 do if i % 2 = 0 then selC.[i].Value <- true)
        check "select-half-singleton"

        // 2. deselect: back to the full per-vertex arrays (realloc back)
        transact (fun () -> for i in 0 .. n - 1 do selC.[i].Value <- false)
        check "deselect"

        // 3. positions IN PLACE (same length, scaled) — same-size re-stage path
        transact (fun () ->
            for i in 0 .. n - 1 do
                if i % 3 = 0 then posC.[i].Value <- posC.[i].Value |> Array.map (fun v -> v * 1.35f))
        check "positions-in-place"

        // 4. whole-geometry swap to a different tessellation: positions/normals/
        //    index all change LENGTH (attr + index realloc; the index count drives
        //    the draw record). Colors stay derived from the ORIGINAL vertex count
        //    on the classic side, so switch those objects to selected (uniform
        //    color) first — both sides then derive colors from the NEW positions.
        transact (fun () ->
            for i in 0 .. n - 1 do
                if i % 4 = 1 then
                    let (p, nn, ix) = sphereArrays (20 + i % 5)
                    selC.[i].Value <- true
                    posC.[i].Value <- p
                    nrmC.[i].Value <- nn
                    idxC.[i].Value <- ix)
        check "geometry-swap"

        // 5. non-indexed fvc shrink to half (record-only)
        transact (fun () -> fvcCell.Value <- (fvcCell.Value / 6) * 3)
        check "fvc-shrink"

        heapOut.Release()
        classicOut.Release()
        if pass then Log.line "dynGeom: ALL PASS (adaptive attrs/index/draw-calls == classic)"
        else Log.warn "dynGeom: FAILED"
        pass

    /// TOGGLE-CHURN perf probe: repeated singleton<->full color flips (the demo's
    /// select/deselect) must stay O(change) and NOT degrade — no arena growth, no
    /// fragmentation creep, no accumulating adaptive state. Renders every round
    /// (arena flush included) and reports first-vs-last round timings + arena size.
    let dynPerf () =
        Aardvark.Init()
        use app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication()
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(512, 512))
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 42.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let effect = FShade.Effect.compose [ FShade.Effect.ofFunction Shaders.shadeVtx; FShade.Effect.ofFunction Shaders.shadeFrag ]
        let lightBlue = C4b(115uy, 199uy, 255uy)

        let n = 4096
        let selC = Array.init n (fun _ -> cval false)
        let mkRO (i : int) : IRenderObject =
            let rand = RandomSystem(i * 7919 + 13)
            let tess = 10 + rand.UniformInt 8
            let g = (IndexedGeometryPrimitives.Sphere.solidPhiThetaSphere (Sphere3d(V3d.Zero, 0.55)) tess C4b.White).ToIndexed()
            let pos = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]> |> Array.copy
            let nrm = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]> |> Array.copy
            let idx = g.IndexArray |> unbox<int[]> |> Array.copy
            let full = Array.init pos.Length (fun v -> C4b(byte (60 + (i * 13) % 180), byte (60 + v % 160), 90uy))
            let cols = selC.[i] |> AVal.map (fun sel -> if sel then [| lightBlue |] else full)
            let s = 64
            let p = V3d(float (i % s - s/2) * 1.2, float (i / s - s/2) * 1.2, 0.0)
            let bvA (av : aval<'a[]>) t = BufferView(av |> AVal.map (fun a -> ArrayBuffer(a :> System.Array) :> IBuffer), t)
            let ro = RenderObject()
            ro.Surface <- Surface.Effect effect
            ro.Mode    <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- AttributeProvider.ofList [
                DefaultSemantic.Positions, BufferView(AVal.constant (ArrayBuffer pos :> IBuffer), typeof<V3f>)
                DefaultSemantic.Normals,   BufferView(AVal.constant (ArrayBuffer nrm :> IBuffer), typeof<V3f>)
                DefaultSemantic.Colors,    bvA cols typeof<C4b> ]
            ro.Indices   <- Some (BufferView(AVal.constant (ArrayBuffer idx :> IBuffer), typeof<int>))
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = idx.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj ]
            ro :> IRenderObject

        let storage = runtime.CreateHeapStorage()
        let heapObjs = Heap.ofRenderObjects storage (ASet.ofArray (Array.init n mkRO))
        use task = runtime.CompileRender(signature, heapObjs)
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        out.GetValue() |> ignore   // ingest + first frame

        let sw = System.Diagnostics.Stopwatch()
        let round (r : int) =
            sw.Restart()
            transact (fun () -> for i in 0 .. n - 1 do if i % 2 = r % 2 then selC.[i].Value <- not selC.[i].Value)
            out.GetValue() |> ignore
            sw.Elapsed.TotalMilliseconds

        let rounds = 400
        let times = Array.init rounds round
        let head = Array.averageBy id times.[0..19]
        let tail = Array.averageBy id times.[rounds-20..]
        Log.line "dynPerf: %d objects, %d toggle rounds" n rounds
        Log.line "dynPerf: first20 avg %.2f ms | last20 avg %.2f ms | max %.2f ms" head tail (Array.max times)
        let ok = tail < head * 2.0 + 2.0
        if ok then Log.line "dynPerf: PASS (no degradation)"
        else Log.warn "dynPerf: FAIL — per-toggle cost grew %.2f -> %.2f ms" head tail
        out.Release()
        ok

    /// TOGGLE-ACCUMULATION probe on the DEMO SHAPE: picking build (two partitions
    /// over one shared storage, two tasks) + adaptive singleton<->full color flips
    /// of ONE object, repeatedly. Measures per-toggle wall time — the CadSceneDemo
    /// report is "1st click fine, 2nd feelable, 3rd terrible", so a compounding
    /// cost must show up within a handful of toggles.
    let dynPick () =
        Aardvark.Init()
        use app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication()
        let runtime = app.Runtime
        let baseSig =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 42.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let effect = FShade.Effect.compose [ FShade.Effect.ofFunction Shaders.shadeVtx; FShade.Effect.ofFunction Shaders.shadeFrag ]
        let pickEffect = FShade.Effect.compose [ FShade.Effect.ofFunction Shaders.shadeVtx; FShade.Effect.ofFunction Shaders.shadeFrag; FShade.Effect.ofFunction PS.write ]
        let lightBlue = C4b(115uy, 199uy, 255uy)

        let n = 2048
        let selC = Array.init n (fun _ -> cval false)
        let mkRO (i : int) : IRenderObject =
            let rand = RandomSystem(i * 7919 + 13)
            let tess = 10 + rand.UniformInt 8
            let g = (IndexedGeometryPrimitives.Sphere.solidPhiThetaSphere (Sphere3d(V3d.Zero, 0.55)) tess C4b.White).ToIndexed()
            let pos = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]> |> Array.copy
            let nrm = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]> |> Array.copy
            let idx = g.IndexArray |> unbox<int[]> |> Array.copy
            let full = Array.init pos.Length (fun v -> C4b(byte (60 + (i * 13) % 180), byte (60 + v % 160), 90uy))
            let cols = selC.[i] |> AVal.map (fun sel -> if sel then [| lightBlue |] else full)
            let s = 46
            let p = V3d(float (i % s - s/2) * 1.2, float (i / s - s/2) * 1.2, 0.0)
            let bvA (av : aval<'a[]>) t = BufferView(av |> AVal.map (fun a -> ArrayBuffer(a :> System.Array) :> IBuffer), t)
            let ro = RenderObject()
            ro.Surface <- Surface.Effect (if i % 2 = 0 then pickEffect else effect)
            ro.Mode    <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- AttributeProvider.ofList [
                DefaultSemantic.Positions, BufferView(AVal.constant (ArrayBuffer pos :> IBuffer), typeof<V3f>)
                DefaultSemantic.Normals,   BufferView(AVal.constant (ArrayBuffer nrm :> IBuffer), typeof<V3f>)
                DefaultSemantic.Colors,    bvA cols typeof<C4b> ]
            ro.Indices   <- Some (BufferView(AVal.constant (ArrayBuffer idx :> IBuffer), typeof<int>))
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = idx.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                yield Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                yield Symbol.Create "ViewProjTrafo",  viewProj
                if i % 2 = 0 then yield Symbol.Create "HeapPickId", (AVal.constant i :> IAdaptiveValue) ]
            ro :> IRenderObject

        let heaped = Heap.ofRenderObjectsPicking (runtime.CreateHeapStorage()) ignore (ASet.ofList [ for i in 0 .. n - 1 -> mkRO i ])
        let isPickableSdr (ro : IRenderObject) =
            match ro with
            | :? SignatureDependentRenderObject as s -> s.IsPickable
            | _ -> false
        use pickTask = runtime.CompileRender(baseSig, heaped |> ASet.filter isPickableSdr)
        use baseTask = runtime.CompileRender(baseSig, heaped |> ASet.filter (isPickableSdr >> not))
        let size = V2i(512, 512)
        let colorTex = runtime.CreateTexture2D(size, TextureFormat.Rgba8, 1, 1)
        let depthTex = runtime.CreateTexture2D(size, TextureFormat.Depth24Stencil8, 1, 1)
        let fb = runtime.CreateFramebuffer(baseSig, [ DefaultSemantic.Colors, colorTex.[TextureAspect.Color, 0, 0] :> IFramebufferOutput
                                                      DefaultSemantic.DepthStencil, depthTex.[TextureAspect.DepthStencil, 0, 0] :> IFramebufferOutput ] |> Map.ofList)
        use clearTask = runtime.CompileClear(baseSig, clear { color C4f.Black; depth 1.0; stencil 0 })
        let frame () =
            clearTask.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer fb)
            pickTask.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer fb)
            baseTask.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer fb)
        for _ in 1 .. 3 do frame ()

        // toggle ONE pickable object repeatedly (the demo click), render after each,
        // and ALSO time plain no-change frames between toggles (the "stutter")
        let sw = System.Diagnostics.Stopwatch()
        let mutable pass = true
        let mutable firstToggle = 0.0
        for t in 1 .. 12 do
            sw.Restart()
            transact (fun () -> selC.[0].Value <- not selC.[0].Value)
            frame ()
            let tog = sw.Elapsed.TotalMilliseconds
            sw.Restart()
            for _ in 1 .. 5 do frame ()
            let idle5 = sw.Elapsed.TotalMilliseconds
            if t = 1 then firstToggle <- tog
            Log.line "dynPick toggle %2d: toggle+frame %.2f ms | 5 idle frames %.2f ms" t tog idle5
            if t > 3 && (tog > firstToggle * 4.0 + 4.0 || idle5 > 100.0) then pass <- false
        if pass then Log.line "dynPick: PASS (no per-toggle accumulation)"
        else Log.warn "dynPick: FAIL — toggles or idle frames degrade"
        pass

    /// TYPED-ASSIGNMENT partitions (ai/HEAP-TYPED-ASSIGNMENTS-PLAN.md): one
    /// bucket whose Colors semantic is fed by THREE source types — two popular
    /// groups (C4b / C4f, above the materialization threshold) and one small
    /// V3f tail (stays in the dynamic partition). Pixel-verified against the
    /// classic render; partition counts asserted through materialization,
    /// DEmaterialization (remove one group) and RE-materialization (re-add).
    let mixedTypes () =
        Aardvark.Init()
        use app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication()
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 26.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let effect = FShade.Effect.compose [ FShade.Effect.ofFunction Shaders.shadeVtx; FShade.Effect.ofFunction Shaders.shadeFrag ]
        let palette = [| C4b(230uy,80uy,60uy); C4b(90uy,200uy,90uy); C4b(70uy,120uy,230uy); C4b(240uy,200uy,60uy) |]

        let sphereArrays (tess : int) =
            let g = (IndexedGeometryPrimitives.Sphere.solidPhiThetaSphere (Sphere3d(V3d.Zero, 0.55)) tess C4b.White).ToIndexed()
            let pos = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]> |> Array.copy
            let nrm = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]> |> Array.copy
            let idx = g.IndexArray |> unbox<int[]> |> Array.copy
            pos, nrm, idx

        // 90 C4b + 90 C4f + 20 V3f color sources; grid placement, one bucket
        let n = 200
        let geo = Array.init n (fun i -> sphereArrays (10 + i % 8))
        let mkPair (i : int) : IRenderObject * IRenderObject =
            let s = 15
            let p = V3d(float (i % s - s/2) * 1.35, float (i / s - s/2) * 1.35, 0.0)
            let (pos, nrm, idx) = geo.[i]
            let colBuf : System.Array * System.Type =
                if i < 90 then
                    (Array.init pos.Length (fun v -> palette.[(i + v / 16) % 4]) :> System.Array), typeof<C4b>
                elif i < 180 then
                    (Array.init pos.Length (fun v -> palette.[(i + v / 12) % 4].ToC4f()) :> System.Array), typeof<C4f>
                else
                    (Array.init pos.Length (fun v ->
                        let c = palette.[(i + v / 8) % 4].ToC4f() in V3f(c.R, c.G, c.B)) :> System.Array), typeof<V3f>
            let (colArr, colT) = colBuf
            let mk () =
                let ro = RenderObject()
                ro.Surface <- Surface.Effect effect
                ro.Mode    <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- AttributeProvider.ofList [
                    DefaultSemantic.Positions, BufferView(AVal.constant (ArrayBuffer pos :> IBuffer), typeof<V3f>)
                    DefaultSemantic.Normals,   BufferView(AVal.constant (ArrayBuffer nrm :> IBuffer), typeof<V3f>)
                    DefaultSemantic.Colors,    BufferView(AVal.constant (ArrayBuffer colArr :> IBuffer), colT) ]
                ro.Indices   <- Some (BufferView(AVal.constant (ArrayBuffer idx :> IBuffer), typeof<int>))
                ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = idx.Length, InstanceCount = 1) |])
                ro.Uniforms  <- UniformProvider.ofList [
                    Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                    Symbol.Create "ViewProjTrafo",  viewProj ]
                ro :> IRenderObject
            mk (), mk ()

        let pairs = Array.init n mkPair
        let heapLive    = cset (pairs |> Array.map fst)
        let classicLive = cset (pairs |> Array.map snd)

        let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) heapLive
        use heapTask = runtime.CompileRender(signature, heapObjs)
        let heapOut = heapTask |> RenderTask.renderToColor size
        heapOut.Acquire()
        use classicTask = runtime.CompileRender(signature, classicLive)
        let classicOut = classicTask |> RenderTask.renderToColor size
        classicOut.Acquire()

        let mutable pass = true
        let check (label : string) (expectedParts : int) (expectedDyn : int) =
            let h = heapOut.GetValue().Download().AsPixImage<uint8>()
            let c = classicOut.GetValue().Download().AsPixImage<uint8>()
            let maxDelta, nDiff, nNonBg, total = diff c h
            let pixOk = maxDelta <= 1 && nNonBg > 1000L
            let partOk = Heap.lastMaterializedPartitions = expectedParts && Heap.lastDynamicResidents = expectedDyn
            if pixOk && partOk then
                Log.line "mixedTypes[%s]: PASS (coverage=%d, partitions=%d, dynamic=%d)" label nNonBg Heap.lastMaterializedPartitions Heap.lastDynamicResidents
            else
                Log.warn "mixedTypes[%s]: FAIL pix(maxDelta=%d diff=%d/%d coverage=%d) partitions=%d (exp %d) dynamic=%d (exp %d)"
                    label maxDelta nDiff total nNonBg Heap.lastMaterializedPartitions expectedParts Heap.lastDynamicResidents expectedDyn
            pass <- pass && pixOk && partOk

        // both 90-groups materialize; the 20 V3f stay dynamic
        check "initial" 2 20

        // remove the C4f group -> its partition DEmaterializes
        transact (fun () ->
            for i in 90 .. 179 do
                heapLive.Remove (fst pairs.[i]) |> ignore
                classicLive.Remove (snd pairs.[i]) |> ignore)
        check "remove-c4f" 1 20

        // re-add -> RE-materializes under a FRESH partition id (double-draw guard)
        transact (fun () ->
            for i in 90 .. 179 do
                heapLive.Add (fst pairs.[i]) |> ignore
                classicLive.Add (snd pairs.[i]) |> ignore)
        check "readd-c4f" 2 20

        if pass then Log.line "mixedTypes: ALL PASS (typed partitions == classic; materialize/demat/remat verified)"
        else Log.warn "mixedTypes: FAIL"
        pass
