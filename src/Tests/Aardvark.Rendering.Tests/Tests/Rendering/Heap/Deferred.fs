namespace Aardvark.Rendering.Tests.Rendering.Heap

// Validation of the signature-deferred heap build (Vulkan): `Heap.ofRenderObjects`
// returns SignatureDependentRenderObjects whose buckets are built at compile time
// against the real framebuffer signature. Five checks, all offscreen with readback:
//
//   1. equivalence: an opaque grid rendered through the heap must be pixel-identical
//      to the classic per-object render.
//   2. transparency: an opaque + half-alpha (IsTransparent) mix through the heap must
//      match the classic render and route through OIT. Also asserts that the opaque
//      pass (intermediate signature) and the transparent pass (user signature) share
//      one build: Heap.buildInvocations grows by exactly 1, not one arena per pass.
//   3. extra attachment: render the heap into a signature that carries an extra
//      `Normals` attachment (Colors + Normals + Depth). Must not crash, Colors must
//      match the {Colors, Depth} render, and the Normals attachment must be written.
//   4. lifecycle: render and dispose the same set across many sizes in a loop (each
//      dispose tears the heap down, each render rebuilds), using the auto-storage
//      variant `Heap.ofRenderObjectsAuto`. No crash and stable memory means the
//      ref-counted activation lifecycle survives resize, teardown and rebuild.
//   5. shared storage: one HeapStorage backing two heaps; tearing one down must leave
//      the other fully functional.

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open FShade

module Deferred =

    // two-output fragment: Color + a view-independent Normals G-buffer value, so the
    // extra-attachment test can prove the Normals target is actually written.
    module DS =
        type FragOutN = { [<Color>] color : V4f; [<Semantic("Normals")>] nrm : V4f }

        let fragN (v : Shaders.Vertex) =
            fragment {
                let l  = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
                let nn = Vec.normalize v.n
                let d  = 0.25f + 0.75f * max 0.0f (Vec.dot nn l)
                let r : FragOutN = { color = V4f(v.c.XYZ * d, 1.0f); nrm = V4f(nn * 0.5f + V3f(0.5f, 0.5f, 0.5f), 1.0f) }
                return r
            }

        // half-alpha transparent fragment (a distinct effect, so its own bucket)
        let fragTransp (v : Shaders.Vertex) =
            fragment {
                let l  = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
                let nn = Vec.normalize v.n
                let d  = 0.25f + 0.75f * max 0.0f (Vec.dot nn l)
                return V4f(v.c.XYZ * d, 0.5f)
            }

    let private diff (a : PixImage<uint8>) (b : PixImage<uint8>) =
        let av = a.GetMatrix<C4b>()
        let bv = b.GetMatrix<C4b>()
        let mutable maxDelta = 0
        let mutable nDiff = 0L
        let mutable nNonBg = 0L
        let total = av.Size.X * av.Size.Y
        av.ForeachCoord(fun (c : V2l) ->
            let x = av.[c]
            let y = bv.[c]
            let dr = abs (int x.R - int y.R)
            let dg = abs (int x.G - int y.G)
            let db = abs (int x.B - int y.B)
            let d = max dr (max dg db)
            if d > maxDelta then maxDelta <- d
            if d > 1 then nDiff <- nDiff + 1L
            if int x.R + int x.G + int x.B > 12 then nNonBg <- nNonBg + 1L)
        maxDelta, nDiff, nNonBg, int64 total

    let run (runtime : IRuntime) =

        use sigCD =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
            ]
        // extra-attachment signature: Colors + Normals (Rgba16f, a float G-buffer format)
        // + Depth
        use sigCND =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.Normals, TextureFormat.Rgba16f
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
            ]
        // same extra attachment but Rgba8 so the harness can read it back as uint8
        // (Vulkan Download -> PixImage<uint8> does not support the Half channel type).
        use sigCND8 =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.Normals, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
            ]

        let size = cval (V2i(512, 512))

        // shared geometry
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]

        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 18.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue

        let mkRO (transparent : bool) (p : V3d) (color : C4f) (effect : Effect) =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect effect
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "HeapColor",      (AVal.constant (color.ToV4f()) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj ]
            ro.IsTransparent <- transparent
            ro :> IRenderObject

        let renderColor (fbSig : IFramebufferSignature) (objs : aset<IRenderObject>) =
            use task = runtime.CompileRender(fbSig, objs)
            let out = task |> RenderTask.renderToColor size
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()

        let renderSem (fbSig : IFramebufferSignature) (sem : Symbol) (objs : aset<IRenderObject>) =
            use task = runtime.CompileRender(fbSig, objs)
            let out = task |> RenderTask.renderSemantics (Set.ofList [ DefaultSemantic.Colors; sem ]) size |> Map.find sem
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()

        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan |]
        let gridOf n effect transparent =
            let s = int (ceil (sqrt (float n)))
            Array.init n (fun i ->
                let x = i % s
                let y = i / s
                mkRO transparent (V3d(float (x - s/2) * 1.2, float (y - s/2) * 1.2, 0.0)) palette.[i % palette.Length] effect)

        let effectLit    = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        let effectN      = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction DS.fragN ]
        let effectTransp = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction DS.fragTransp ]

        let report label maxDelta nDiff nNonBg total =
            Log.line "deferred[%s]: maxChannelDelta=%d  diffPixels=%d/%d (%.4f%%)  coverage=%d px"
                label maxDelta nDiff total (100.0 * float nDiff / float total) nNonBg
            let pass = maxDelta <= 1 && nNonBg > total / 100L
            if pass then Log.line "deferred[%s]: PASS" label else Log.warn "deferred[%s]: FAIL" label
            pass

        // 1. opaque equivalence
        let opaque = gridOf 256 effectLit false |> ASet.ofArray
        let eagerPix    = renderColor sigCD opaque    // classic individual draws as reference
        use storage = runtime.CreateHeapStorage()
        let deferredPix = renderColor sigCD (Heap.ofRenderObjects storage opaque)
        let d1 = diff eagerPix deferredPix
        let pass1 = (let (md, nd, nbg, tot) = d1 in report "opaque-equiv" md nd nbg tot)

        // 2. transparency and the shared per-signature build
        let mixed =
            Array.append
                (gridOf 128 effectLit false)
                (gridOf 128 effectTransp true)
            |> ASet.ofArray
        let eagerT    = renderColor sigCD mixed       // classic individual draws as reference
        let before = Heap.buildInvocations
        use storage = runtime.CreateHeapStorage()
        let deferredT = renderColor sigCD (Heap.ofRenderObjects storage mixed)
        let builds = Heap.buildInvocations - before
        let d2 = diff eagerT deferredT
        let pass2px = (let (md, nd, nbg, tot) = d2 in report "transparent-equiv" md nd nbg tot)
        // one build shared by the opaque pass (intermediate signature), the transparent
        // pass and the direct path (both user signature).
        Log.line "deferred[shared-persig]: buildHeap invocations for one transparent deferred render = %d (expect 1)" builds
        let pass2build = (builds = 1)
        if not pass2build then Log.warn "deferred[shared-persig]: FAIL: expected 1 build, got %d (memo not collapsing intermediate/user sig => 2x VRAM)" builds
        let pass2 = pass2px && pass2build

        // 3. extra attachment (Normals G-buffer)
        let opaqueN = gridOf 256 effectN false |> ASet.ofArray
        // (a) does not crash rendering into {Colors, Normals, Depth}
        use storage = runtime.CreateHeapStorage()
        let deferredNColor = renderColor sigCND (Heap.ofRenderObjects storage opaqueN)
        // (b) Colors identical to the {Colors, Depth} render of the same scene
        use storage = runtime.CreateHeapStorage()
        let deferredColorOnly = renderColor sigCD (Heap.ofRenderObjects storage opaqueN)
        let d3c = diff deferredColorOnly deferredNColor
        let pass3c = (let (md, nd, nbg, tot) = d3c in report "extra-attach-color" md nd nbg tot)
        // (c) the Normals attachment is actually written (non-cleared coverage); read from
        // the Rgba8 variant of the signature so the uint8 download works. The Rgba16f
        // render above already proved the float G-buffer format renders without crashing.
        use storage = runtime.CreateHeapStorage()
        let normalsPix = renderSem sigCND8 DefaultSemantic.Normals (Heap.ofRenderObjects storage opaqueN)
        let nv = normalsPix.GetMatrix<C4b>()
        let mutable nWritten = 0L
        nv.ForeachCoord(fun (c : V2l) -> let px = nv.[c] in if int px.R + int px.G + int px.B > 12 then nWritten <- nWritten + 1L)
        Log.line "deferred[extra-attach-normals]: written normal pixels = %d" nWritten
        let pass3n = nWritten > (int64 (nv.Size.X * nv.Size.Y)) / 100L
        if pass3n then Log.line "deferred[extra-attach-normals]: PASS" else Log.warn "deferred[extra-attach-normals]: FAIL: Normals target not written"
        let pass3 = pass3c && pass3n

        // 4. lifecycle: resize / teardown / rebuild loop. Uses the auto-storage variant,
        //    so every teardown/rebuild cycle also creates and drops a private HeapStorage;
        //    that lifecycle must not leak or crash.
        let deferredLoop = Heap.ofRenderObjectsAuto opaque
        System.GC.Collect(); System.GC.WaitForPendingFinalizers()
        let rss0 = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64
        let sizes = [| 256; 384; 512; 768; 512; 256 |]
        let mutable crashed = false
        try
            for iter in 1 .. 6 do
                for s in sizes do
                    transact (fun () -> size.Value <- V2i(s, s))
                    renderColor sigCD deferredLoop |> ignore
        with e ->
            crashed <- true
            Log.warn "deferred[lifecycle]: CRASH during resize loop: %s" e.Message
        System.GC.Collect(); System.GC.WaitForPendingFinalizers()
        let rss1 = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64
        let grewMB = float (rss1 - rss0) / (1024.0 * 1024.0)
        Log.line "deferred[lifecycle]: 36 resize+render cycles, RSS delta = %.1f MB" grewMB
        let pass4 = not crashed && grewMB < 200.0
        if pass4 then Log.line "deferred[lifecycle]: PASS" else Log.warn "deferred[lifecycle]: FAIL (crashed=%b grewMB=%.1f)" crashed grewMB

        transact (fun () -> size.Value <- V2i(512, 512))

        // 5. one HeapStorage shared by two heaps (the shadow-mapping shape): both heaps'
        //    allocations dedup in the same pages; both render correctly while alive
        //    together, and tearing one down (task disposal releases its ref-counts) must
        //    leave the other fully functional.
        use sharedStore = runtime.CreateHeapStorage()
        let refPix = renderColor sigCD opaque
        let heapA = Heap.ofRenderObjects sharedStore opaque
        let heapB = Heap.ofRenderObjects sharedStore opaque
        let taskA = runtime.CompileRender(sigCD, heapA)
        let taskB = runtime.CompileRender(sigCD, heapB)
        let snap (task : IRenderTask) =
            let out = task |> RenderTask.renderToColor size
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()
        let pixA = snap taskA
        let pixB = snap taskB
        taskA.Dispose()                                  // heap A tears down; storage must survive
        let pixB2 = snap taskB
        taskB.Dispose()
        let ok label pix =
            let (md, nd, nbg, tot) = diff refPix pix
            report label md nd nbg tot
        let pass5 = ok "shared-storage-A" pixA && ok "shared-storage-B" pixB && ok "shared-storage-B-after-A-teardown" pixB2

        let allPass = pass1 && pass2 && pass3 && pass4 && pass5
        if allPass then Log.line "deferred: ALL PASS (opaque-equiv + transparent+shared-persig + extra-attachment + lifecycle + shared-storage)"
        else Log.warn "deferred: FAILED"
        allPass
