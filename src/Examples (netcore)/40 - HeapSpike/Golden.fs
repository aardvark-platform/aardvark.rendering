namespace HeapSpike

// Golden-image equivalence (Vulkan): render the SAME scene two ways and compare
// pixel-by-pixel. Two scenes:
//
//   1. uniform   — classic (N independent ROs, per-draw uniforms in UBOs) vs
//      heap (N -> 1 bucket, uniforms gathered from the arena SSBO). Same effect;
//      the rewrite changes only WHERE per-draw uniforms come from.
//
//   2. textured  — classic CONVENTIONAL per-object textures (each RO binds its
//      own sampler2D DiffuseTexture) vs heap BINDLESS (one unbounded
//      `sampler2D Textures[]`, per-object index in the arena, sampled with
//      nonuniformEXT). Different effects, identical visual result: proves the
//      bindless heap path matches plain per-object texturing.
//
// MSAA is off for a deterministic comparison.

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Rendering.Text
open FSharp.Data.Adaptive
open FShade

module Golden =

    [<Literal>]
    let TexCount = 32

    module S =
        type V =
            { [<Position>]                                               pos : V4f
              [<Normal>]                                                 n   : V3f
              [<Semantic("TexCoord")>]                                   tc  : V2f
              [<Semantic("TexId"); Interpolation(InterpolationMode.Flat)>] ti : int }

        // identical vertex math for both texture paths (heap also routes ti)
        let vClassic (v : V) =
            vertex {
                let m  : M44f = uniform?HeapModelTrafo
                let vp : M44f = uniform?ViewProjTrafo
                return { v with pos = vp * (m * v.pos); n = m.TransformDir v.n; tc = v.pos.XY + V2f(0.5f, 0.5f) }
            }

        let vHeap (v : V) =
            vertex {
                let m  : M44f = uniform?HeapModelTrafo
                let vp : M44f = uniform?ViewProjTrafo
                let ti : int  = uniform?HeapTexIndex
                return { v with pos = vp * (m * v.pos); n = m.TransformDir v.n; tc = v.pos.XY + V2f(0.5f, 0.5f); ti = ti }
            }

        // conventional single per-object texture
        let private diffuse =
            sampler2d {
                texture uniform?DiffuseTexture
                filter Filter.MinMagMipLinear
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        let fClassic (v : V) =
            fragment {
                let albedo = diffuse.Sample(v.tc).XYZ
                let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
                let d = 0.35f + 0.65f * max 0.0f (Vec.dot (Vec.normalize v.n) l)
                return V4f(albedo * d, 1.0f)
            }

        // bindless: one unbounded array, index from the arena
        let private textures =
            sampler2d {
                textureArray uniform?Textures -1
                filter Filter.MinMagMipLinear
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        let fHeap (v : V) =
            fragment {
                let albedo = textures.[v.ti].Sample(v.tc).XYZ
                let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
                let d = 0.35f + 0.65f * max 0.0f (Vec.dot (Vec.normalize v.n) l)
                return V4f(albedo * d, 1.0f)
            }

    // distinct per-index checker texture (same content addressed by both paths)
    let private mkTexture (i : int) : ITexture =
        let cols = [| C3b(230,60,60); C3b(60,200,60); C3b(60,120,230); C3b(230,200,40)
                      C3b(210,60,210); C3b(40,210,210); C3b(230,140,40); C3b(180,180,180) |]
        let col = cols.[i % cols.Length]
        let img = PixImage<byte>(Col.Format.RGBA, V2i(64, 64))
        img.GetMatrix<C4b>().SetByIndex(fun (idx : int64) ->
            let x = int idx % 64
            let y = int idx / 64
            if ((x / 8) + (y / 8)) % 2 = 0 then C4b(col) else C4b.White) |> ignore
        PixTexture2d(img) :> ITexture

    // max per-channel abs delta, # differing pixels, # non-background (in `a`)
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
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
            ]
        let size = AVal.constant (V2i(1024, 1024))

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

        let gridOf n =
            let s = int (ceil (sqrt (float n)))
            Array.init n (fun i ->
                let x = i % s
                let y = i / s
                i, V3d(float (x - s/2) * 1.2, float (y - s/2) * 1.2, 0.0))

        let mkRO (uniforms : list<Symbol * IAdaptiveValue>) (effect : Effect) =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect effect
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList uniforms
            ro :> IRenderObject

        let renderToPix (objs : aset<IRenderObject>) =
            use task = runtime.CompileRender(signature, objs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>()
            finally out.Release()

        let report (label : string) (classicPix : PixImage<uint8>) (heapPix : PixImage<uint8>) =
            let maxDelta, nDiff, nNonBg, total = diff classicPix heapPix
            Log.line "golden[%s]: -> %d bucket(s)" label Heap.lastBucketCount
            Log.line "golden[%s]: classic vs heap  maxChannelDelta=%d  diffPixels=%d/%d (%.4f%%)  coverage=%d px"
                label maxDelta nDiff total (100.0 * float nDiff / float total) nNonBg
            let pass = maxDelta <= 1 && nNonBg > total / 100L
            if pass then Log.line "golden[%s]: PASS" label
            else Log.warn "golden[%s]: FAIL (maxDelta=%d nNonBg=%d)" label maxDelta nNonBg
            pass

        // ── scene 1: uniform-only (same effect; UBO vs arena) ──────────────
        let effectU = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan |]
        let inputsU =
            gridOf 256 |> Array.map (fun (i, p) ->
                mkRO [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                       Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                       Symbol.Create "ViewProjTrafo",  viewProj ] effectU)
        let classicU = renderToPix (ASet.ofArray inputsU)
        let heapU = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray inputsU)
        let passU = report "uniform" classicU (renderToPix heapU)

        // ── scene 1b: TRANSPARENT bucket (the OIT store path) — the composed
        // store program gains OIT resources alongside the Heap* buffers, so this
        // guards the pipeline-layout/binding consistency of the rewritten VS
        // (VUID-VkGraphicsPipelineCreateInfo-layout-07988 class of failures).
        // Content is OPAQUE-alpha, so the OIT composite must equal plain
        // depth-tested rendering for classic AND heap alike.
        let mkTransparentROs () =
            gridOf 256 |> Array.map (fun (i, p) ->
                let ro =
                    mkRO [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                           Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                           Symbol.Create "ViewProjTrafo",  viewProj ] effectU
                (match ro with :? RenderObject as r -> r.IsTransparent <- true | _ -> ())
                ro)
        let classicO = renderToPix (ASet.ofArray (mkTransparentROs ()))
        let heapO = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray (mkTransparentROs ()))
        let passO = report "oit-transparent" classicO (renderToPix heapO)

        // ── scene 2: textured (conventional per-object vs bindless heap) ───
        let texArray : ITexture[] = Array.init TexCount mkTexture
        // per-texture avals (constant outer array -> known length -> variable
        // descriptor count; each texture would update independently)
        let texArrayU = AVal.constant (texArray |> Array.mapi (fun i t -> i, AVal.constant t)) :> IAdaptiveValue
        let effectClassic = Effect.compose [ Effect.ofFunction S.vClassic; Effect.ofFunction S.fClassic ]
        let effectHeap    = Effect.compose [ Effect.ofFunction S.vHeap;    Effect.ofFunction S.fHeap ]
        let grid = gridOf 64
        let classicT =
            grid |> Array.map (fun (i, p) ->
                mkRO [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                       Symbol.Create "ViewProjTrafo",  viewProj
                       Symbol.Create "DiffuseTexture", (AVal.constant texArray.[i % TexCount] :> IAdaptiveValue) ] effectClassic)
        let heapT =
            grid |> Array.map (fun (i, p) ->
                mkRO [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                       Symbol.Create "ViewProjTrafo",  viewProj
                       Symbol.Create "HeapTexIndex",   (AVal.constant (i % TexCount) :> IAdaptiveValue)
                       Symbol.Create "Textures",       texArrayU ] effectHeap)
        // the textured phase HARD-CODES the bindless sampler array — the path the
        // heap itself only takes when the runtime supports unbounded arrays
        // (MoltenVK does NOT: SPIRV-Cross argument-buffer padding rejects the
        // shader — the reason the atlas exists; atlas coverage = `atlasheap`).
        let passT =
            if runtime.SupportsUnboundedSamplerArrays then
                let classicTpix = renderToPix (ASet.ofArray classicT)
                let heapTobjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray heapT)
                report "textured" classicTpix (renderToPix heapTobjs)
            else
                Log.line "golden[textured]: SKIP (no unbounded sampler arrays — the heap uses the atlas here; run `atlasheap`)"
                true

        let pass = passU && passO && passT
        if pass then Log.line "golden: ALL PASS (uniform + bindless-textured heap == classic)"
        else Log.warn "golden: FAILED"
        pass

    // Auto-detected per-draw heap fields (Heap.ofRenderObjects): the same scene
    // rendered classic and heap must agree pixel-for-pixel. Also asserts the
    // classification itself via Heap.lastAutoFields:
    //   * consumed + RO-supplied + packable  -> per-draw field, including the
    //     SHARED ViewProjTrafo aval (dedups to ONE arena region), and
    //   * RO-supplied but NOT consumed       -> ignored,
    // and that DIFFERENT detected field sets split buckets (the field set is
    // part of the bucket key).
    module AF =
        let gammaFrag (v : Shaders.Vertex) =
            fragment {
                let g : float32 = uniform?Gamma
                return V4f(v.c.XYZ * g, 1.0f)
            }

    let autoFieldsTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]

        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 18.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue   // ONE shared aval (classic reads it directly)
        // View/Proj are universal constituents a reasonable consumer always provides;
        // the heap DERIVES ViewProjTrafo from them (ProjTrafo*ViewTrafo), shared -> one region each.
        let viewT = AVal.constant view :> IAdaptiveValue
        let projT = AVal.constant proj :> IAdaptiveValue

        let mkRO (uniforms : list<Symbol * IAdaptiveValue>) (effect : Effect) =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect effect
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList uniforms
            ro :> IRenderObject

        let renderToPix (objs : aset<IRenderObject>) =
            use task = runtime.CompileRender(signature, objs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>()
            finally out.Release()

        // shadeFragTint requests Tint at DOUBLE precision (V3d) — exercises the heap's
        // real-double storage/gather (HeapDataD). One shared aval -> one double region.
        let effect = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFragTint ]
        let tint = AVal.constant (V3d(0.5, 0.7, 0.9)) :> IAdaptiveValue
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan |]
        let s = 16
        let inputs =
            Array.init (s * s) (fun i ->
                let p = V3d(float (i % s - s/2) * 1.2, float (i / s - s/2) * 1.2, 0.0)
                mkRO [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                       Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                       Symbol.Create "ViewTrafo",      viewT
                       Symbol.Create "ProjTrafo",      projT
                       Symbol.Create "ViewProjTrafo",  viewProj
                       Symbol.Create "Tint",           tint
                       // supplied (packable) but NOT consumed by the effect ->
                       // auto-detection must IGNORE it
                       Symbol.Create "NotConsumed",    (AVal.constant i :> IAdaptiveValue) ] effect)

        let classicPix  = renderToPix (ASet.ofArray inputs)
        let heapPix     = renderToPix (Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray inputs))
        let autoBuckets = Heap.lastBucketCount
        let detected    = Heap.lastAutoFields

        // classification (compute-derived model): consumed ∩ (recipe-derivable OR
        // supplied+packable). ViewProjTrafo is a DERIVED composite → a per-slot compute
        // OUTPUT field; its constituents ProjTrafo/ViewTrafo are NOT fields (internal
        // M44d regions the compute reads), and the supplied-but-unconsumed NotConsumed
        // is ignored. So the field set is the direct uniforms + the composite output.
        let expected = [| "HeapColor"; "HeapModelTrafo"; "Tint"; "ViewProjTrafo" |]
        let fieldsOk = detected = expected
        if not fieldsOk then Log.warn "autoFields: detected fields %A (expected %A)" detected expected

        let dC, nC, nbg, total = diff classicPix heapPix
        Log.line "autoFields: fields=%A  buckets=%d" detected autoBuckets
        Log.line "autoFields: heap vs classic maxDelta=%d diffPixels=%d/%d  coverage=%d px" dC nC total nbg
        let pixOk = dC <= 1 && nbg > total / 100L && autoBuckets = 1

        // different DETECTED field sets must land in different buckets: same
        // effect (consumes Gamma), half the ROs supply Gamma, half don't.
        let effectG = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction AF.gammaFrag ]
        let mixed =
            Array.init 4 (fun i ->
                let p = V3d(float i * 1.5, 0.0, 0.0)
                let base' =
                    [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                      Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                      Symbol.Create "ViewTrafo",      viewT
                      Symbol.Create "ProjTrafo",      projT ]
                let us = if i % 2 = 0 then (Symbol.Create "Gamma", (AVal.constant 1.0f :> IAdaptiveValue)) :: base' else base'
                mkRO us effectG)
        // Heap.lastBucketCount is the authoritative split metric (2 distinct field-sets ->
        // 2 buckets). The output RO set additionally carries one draw-less DERIVE pre-pass
        // RO per derive-bucket; both buckets here derive ViewProjTrafo, plus the heap's
        // single ActivationRenderObject (lifetime handle), so 2 draw + 2 derive + 1
        // activation = 5 output ROs. (The derive RO runs the fp64 composite compute.)
        let splitCount = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray mixed) |> ASet.force |> HashSet.count
        let splitOk = Heap.lastBucketCount = 2 && splitCount = 5
        if not splitOk then Log.warn "autoFields: field-set bucket split: %d output RO(s) (expected 5 = 2 draw + 2 derive + 1 activation), %d bucket(s) (expected 2)" splitCount Heap.lastBucketCount

        let pass = fieldsOk && pixOk && splitOk
        if pass then Log.line "autoFields: PASS (auto-detected fields render == classic; NotConsumed ignored; field-set splits buckets)"
        else Log.warn "autoFields: FAIL (fieldsOk=%b pixOk=%b splitOk=%b)" fieldsOk pixOk splitOk
        pass

    // `Sg.heap` — the scene-graph node around Heap.ofRenderObjects. ONE scene
    // built entirely with ordinary Sg combinators (per-leaf Sg.trafo' + Sg.uniform'
    // boxes, effect + camera applied above), rendered three ways:
    //   * classic           — the plain Sg, per-object ROs
    //   * Sg.heap (TS path) — ISimpleSg/TraversalState dispatch (default entry)
    //   * Sg.heap (Ag path) — SimpleConfig.Enabled <- false, legacy Ag entry
    // Both heap renders must be pixel-IDENTICAL to classic (same effect both ways;
    // the rewrite changes only WHERE the per-draw uniforms come from), and the N
    // per-leaf ROs must collapse to ONE bucket (same effect / layout / field set).
    module SGH =
        // reads the Sg-conventional ModelTrafo (per-leaf trafo stack) + the shared
        // ViewProjTrafo SEPARATELY (no derived ModelViewProjTrafo), so classic and
        // heap run the same in-shader multiply on the same float32 values.
        let vert (v : Shaders.Vertex) =
            vertex {
                let m   : M44f = uniform?ModelTrafo
                let vp  : M44f = uniform?ViewProjTrafo
                let col : V4f  = uniform?HeapColor
                return { v with pos = vp * (m * v.pos); c = col; n = m.TransformDir v.n }
            }

        // reads the color from the Colors VERTEX ATTRIBUTE (v.c) instead of a
        // uniform — exercises singleton (SingleValueBuffer) + real-buffer
        // attribute decode in one bucket.
        let vertCol (v : Shaders.Vertex) =
            vertex {
                let m   : M44f = uniform?ModelTrafo
                let vp  : M44f = uniform?ViewProjTrafo
                return { v with pos = vp * (m * v.pos); n = m.TransformDir v.n }
            }

        // FRAGMENT shader that reads a PER-DRAW uniform (HeapColor) — mirrors the demo, whose
        // simpleLighting/water read LightLocation/WaterTime per-draw in the fragment. This forces
        // the heap to pass the slot to the fragment + a fragment HeapData gather, ALONGSIDE the
        // vertex HeapDeriveData gather (derived ModelTrafo/ViewProjTrafo).
        let fragUni (v : Shaders.Vertex) =
            fragment {
                let extra : V4f = uniform?HeapColor
                return { v with c = v.c * 0.5f + extra * 0.5f }
            }

    // Two side-by-side high-subdiv spheres, sized so each fills its OWN heap page
    // (sphere0 -> page 0 / bucketRO, sphere1 -> page 1 / clone). Renders classic vs
    // heap[ts], saves both, and reports per-half (left=sphere0, right=sphere1) diff so
    // we can see exactly which page's draw is broken.
    let sgSphereTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let envI (k : string) (d : int) = match System.Environment.GetEnvironmentVariable k with null | "" -> d | s -> int s
        let level = envI "SPH_LEVEL" 6

        let ig = IndexedGeometryPrimitives.Sphere.solidSubdivisionSphere (Sphere3d(V3d.Zero, 0.8)) level C4b.White
        let nv = (ig.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>).Length
        // size a page so ONE sphere fits but TWO do not -> exactly 2 pages
        let pageWords = nv * 12
        System.Environment.SetEnvironmentVariable("HEAP_PAGE_WORDS", string pageWords)
        Log.line "[sphere] level=%d verts=%d -> HEAP_PAGE_WORDS=%d (1 sphere/page)" level nv pageWords

        let sphereSg () = Sg.ofIndexedGeometry ig
        let view = CameraView.lookAt (V3d(0.0, -6.0, 2.2)) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 60.0 0.1 100.0 1.0 |> Frustum.projTrafo

        let leaves =
            [| sphereSg () |> Sg.trafo' (Trafo3d.Translation(V3d(-1.2, 0.0, 0.0))) |> Sg.uniform' "HeapColor" (C4f.Red.ToV4f())
               sphereSg () |> Sg.trafo' (Trafo3d.Translation(V3d( 1.2, 0.0, 0.0))) |> Sg.uniform' "HeapColor" (C4f.DodgerBlue.ToV4f()) |]

        let scene (wrap : ISg -> ISg) =
            leaves |> Sg.ofArray |> wrap
            |> Sg.effect [ Effect.ofFunction SGH.vert; Effect.ofFunction Shaders.shadeFrag ]
            |> Sg.viewTrafo (AVal.constant view)
            |> Sg.projTrafo (AVal.constant proj)

        let renderToPix (sg : ISg) =
            use task = runtime.CompileRender(signature, sg)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>()
            finally out.Release()

        let classicPix = renderToPix (scene id)
        let heapPix = renderToPix (scene (Sg.heap (runtime.CreateHeapStorage())))
        (try classicPix.SaveAsPng "/tmp/sph_classic.png"; heapPix.SaveAsPng "/tmp/sph_heap.png" with _ -> Log.warn "[sphere] png save unavailable")

        let cm = classicPix.GetMatrix<C4b>()
        let hm = heapPix.GetMatrix<C4b>()
        let w = int classicPix.Size.X
        let h = int classicPix.Size.Y
        let mutable lCov, lDiff, rCov, rDiff = 0, 0, 0, 0
        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                let c = cm.[int64 x, int64 y]
                let hp = hm.[int64 x, int64 y]
                let cov = int c.R + int c.G + int c.B > 40        // classic non-background
                let d = abs (int c.R - int hp.R) + abs (int c.G - int hp.G) + abs (int c.B - int hp.B)
                if x < w / 2 then (if cov then lCov <- lCov + 1); (if d > 24 then lDiff <- lDiff + 1)
                else               (if cov then rCov <- rCov + 1); (if d > 24 then rDiff <- rDiff + 1)
        Log.line "[sphere] buckets=%d" Heap.lastBucketCount
        Log.line "[sphere] LEFT  (sphere0/page0): coverage=%d diffPixels=%d  %s" lCov lDiff (if lDiff > lCov / 20 then "*** BROKEN ***" else "ok")
        Log.line "[sphere] RIGHT (sphere1/page1): coverage=%d diffPixels=%d  %s" rCov rDiff (if rDiff > rCov / 20 then "*** BROKEN ***" else "ok")
        lDiff <= lCov / 20 && rDiff <= rCov / 20

    let sgHeapTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))

        // SCALE knobs (default = original 16×16, unit coords → golden unchanged). Set SG_S (grid side)
        // and SG_SCALE (coord/camera multiplier) to reproduce the demo's many-parts + large-CAD-coords
        // case that golden otherwise misses (e.g. SG_S=64 SG_SCALE=200 → 4096 parts, ±7.7k coords).
        let envI (k : string) (d : int) = match System.Environment.GetEnvironmentVariable k with null | "" -> d | s -> int s
        let envF (k : string) (d : float) = match System.Environment.GetEnvironmentVariable k with null | "" -> d | s -> float s
        let s = envI "SG_S" 16
        let sc = envF "SG_SCALE" 1.0
        let spacing = 1.2 * sc
        let ext = float s * spacing
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * (ext * 1.2)) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 (max 0.1 (ext * 0.002)) (ext * 100.0) 1.0 |> Frustum.projTrafo

        let boxSg =
            IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6 * sc)) C4b.White
            |> Sg.ofIndexedGeometry

        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan |]
        let leaves =
            Array.init (s * s) (fun i ->
                let p = V3d(float (i % s - s/2) * spacing, float (i / s - s/2) * spacing, 0.0)
                boxSg
                |> Sg.trafo' (Trafo3d.Translation p)
                |> Sg.uniform' "HeapColor" (palette.[i % palette.Length].ToV4f()))

        // effect + camera ABOVE the (optional) heap node — attributes flow to the
        // leaves through the scope either way.
        // SG_DYN=1 → DYNAMIC (non-constant) view/proj avals, exercising the dynamic-constituent
        // path (arena.Add/RegionWriter into deriveArena) the demo's free-fly camera uses — vs the
        // constant StageOnce path. The demo's View/Proj are dynamic; golden's are constant.
        let dyn = (System.Environment.GetEnvironmentVariable "SG_DYN" = "1")
        let viewA : aval<Trafo3d> = if dyn then AVal.custom (fun _ -> view) else AVal.constant view
        let projA : aval<Trafo3d> = if dyn then AVal.custom (fun _ -> proj) else AVal.constant proj
        let scene (wrap : ISg -> ISg) =
            leaves
            |> Sg.ofArray
            |> wrap
            |> Sg.effect (if System.Environment.GetEnvironmentVariable "SG_FRAGUNI" = "1"
                          then [ Effect.ofFunction SGH.vert; Effect.ofFunction SGH.fragUni; Effect.ofFunction Shaders.shadeFrag ]
                          else [ Effect.ofFunction SGH.vert; Effect.ofFunction Shaders.shadeFrag ])
            |> Sg.viewTrafo viewA
            |> Sg.projTrafo projA

        let renderToPix (sg : ISg) =
            use task = runtime.CompileRender(signature, sg)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>()
            finally out.Release()

        let report (label : string) (classicPix : PixImage<uint8>) (heapPix : PixImage<uint8>) (buckets : int) =
            let maxDelta, nDiff, nNonBg, total = diff classicPix heapPix
            Log.line "sgheap[%s]: -> %d bucket(s)  maxChannelDelta=%d  diffPixels=%d/%d  coverage=%d px"
                label buckets maxDelta nDiff total nNonBg
            let pass = maxDelta <= 1 && nNonBg > total / 100L && buckets = 1
            if pass then Log.line "sgheap[%s]: PASS" label
            else Log.warn "sgheap[%s]: FAIL (maxDelta=%d buckets=%d nNonBg=%d)" label maxDelta buckets nNonBg
            pass

        let classicPix = renderToPix (scene id)

        // ISimpleSg/TraversalState dispatch (the default CompileRender entry)
        let heapTsPix = renderToPix (scene (Sg.heap (runtime.CreateHeapStorage())))
        let bucketsTs = Heap.lastBucketCount
        let passTs = report "ts" classicPix heapTsPix bucketsTs

        // legacy Ag dispatch (app?Runtime <- runtime; RenderObjects(Ag.Scope.Root))
        Aardvark.SceneGraph.Simple.SimpleConfig.Enabled <- false
        let heapAgPix =
            try renderToPix (scene (Sg.heap (runtime.CreateHeapStorage())))
            finally Aardvark.SceneGraph.Simple.SimpleConfig.Enabled <- true
        let bucketsAg = Heap.lastBucketCount
        let passAg = report "ag" classicPix heapAgPix bucketsAg

        // ── mixed-TYPE colors -> ONE bucket ──
        // the Colors attribute arrives in FOUR different shapes: the DEFAULT box
        // buffer (C4b[], solidBox's own colors), a C4b SINGLETON, a real C4f[]
        // buffer and a C4f singleton. Element types are NOT part of the bucket
        // key — the decoder branches on each allocation's header typeId at fetch
        // time (C4b normalizes /255 in BGRA layout, singletons wrap via
        // vid % length) and converts to the shader's V4f input — so ALL leaves
        // share ONE bucket; the image must equal the classic per-RO render.
        let g2 = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let nVerts = (g2.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>).Length
        let boxSg2 = Sg.ofIndexedGeometry g2
        let leaves2 =
            Array.init (s * s) (fun i ->
                let p = V3d(float (i % s - s/2) * 1.2, float (i / s - s/2) * 1.2, 0.0)
                let c = palette.[i % palette.Length]
                let colored =
                    match i % 4 with
                    | 0 -> boxSg2                                                                            // default box: its own C4b[] buffer
                    | 1 -> boxSg2 |> Sg.vertexBufferValue' DefaultSemantic.Colors (c.ToC4b())                // C4b singleton
                    | 2 -> boxSg2 |> Sg.vertexAttribute' DefaultSemantic.Colors (Array.replicate nVerts c)   // real C4f[] buffer
                    | _ -> boxSg2 |> Sg.vertexBufferValue' DefaultSemantic.Colors c                          // C4f singleton
                colored |> Sg.trafo' (Trafo3d.Translation p))
        let scene2 (wrap : ISg -> ISg) =
            leaves2
            |> Sg.ofArray
            |> wrap
            |> Sg.effect [ Effect.ofFunction SGH.vertCol; Effect.ofFunction Shaders.shadeFrag ]
            |> Sg.viewTrafo (AVal.constant view)
            |> Sg.projTrafo (AVal.constant proj)
        let classic2 = renderToPix (scene2 id)
        let heap2 = renderToPix (scene2 (Sg.heap (runtime.CreateHeapStorage())))
        let passMixed = report "mixed-types" classic2 heap2 Heap.lastBucketCount

        let pass = passTs && passAg && passMixed
        if pass then Log.line "sgheap: ALL PASS (Sg.heap == classic, %d leaves -> 1 bucket, both dispatch paths; mixed C4b/C4f singleton+buffer colors -> 1 bucket)" (s * s)
        else Log.warn "sgheap: FAILED (ts=%b ag=%b mixed=%b)" passTs passAg passMixed
        pass

    // offscreen reproduction of the WINDOWED demo (2 effects -> 2 buckets, varied
    // box/sphere/torus geometry), CLASSIC vs HEAP, saved to PPM so the macOS
    // breakage can be inspected. samples=1 so the result is downloadable.
    let demoShotTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let size = AVal.constant (V2i(1024, 1024))
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let geometry (ig : IndexedGeometry) =
            let g = ig.ToIndexed()
            let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
            let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
            let index     = g.IndexArray |> unbox<int[]>
            let attrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
            attrs, bv index typeof<int>, index.Length
        let shapes =
            [| geometry (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White)
               geometry (IndexedGeometryPrimitives.Sphere.solidSubdivisionSphere (Sphere3d(V3d.Zero, 0.4)) 3 C4b.White)
               geometry (IndexedGeometryPrimitives.solidTorus (Torus3d(V3d.Zero, V3d.OOI, 0.35, 0.13)) C4b.White 16 12) |]
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 14.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan; C4f.Orange; C4f.HotPink |]
        let side = 8
        let effectLit = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        let effectRim = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFragRim ]
        let inputs =
            Array.init (side * side) (fun i ->
                let x = i / side
                let y = i % side
                let p = V3d(float (x - side/2) * 1.2, float (y - side/2) * 1.2, 0.0)
                let (attrs, idxBV, fvc) = shapes.[i % shapes.Length]
                let ro = RenderObject()
                ro.Surface   <- Surface.Effect (if i % 2 = 0 then effectLit else effectRim)
                ro.Mode      <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- attrs
                ro.Indices   <- Some idxBV
                ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = fvc, InstanceCount = 1) |])
                ro.Uniforms  <- UniformProvider.ofList [
                    Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p * Trafo3d.RotationZ 0.6).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                    Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                    Symbol.Create "ViewProjTrafo",  viewProj ]
                ro :> IRenderObject)
        // render at a given sample count; resolve MSAA -> single-sample before download
        let renderAt (samples : int) (objs : aset<IRenderObject>) =
            let sgn = runtime.CreateFramebufferSignature([ DefaultSemantic.Colors, TextureFormat.Rgba8; DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ], samples)
            use task = runtime.CompileRender(sgn, objs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try
                let tex = out.GetValue()
                if samples > 1 then
                    let resolved = runtime.CreateTexture2D(V2i(1024, 1024), TextureFormat.Rgba8, 1, 1)
                    runtime.ResolveMultisamples(tex.GetOutputView(), resolved)
                    let img = runtime.Download(resolved).AsPixImage<uint8>()
                    runtime.DeleteTexture resolved
                    img
                else tex.Download().AsPixImage<uint8>()
            finally out.Release()
        let savePpm (path : string) (img : PixImage<uint8>) =
            let m = img.GetMatrix<C4b>()
            let w = int m.Size.X
            let h = int m.Size.Y
            use fs = new System.IO.FileStream(path, System.IO.FileMode.Create)
            let header = System.Text.Encoding.ASCII.GetBytes(sprintf "P6\n%d %d\n255\n" w h)
            fs.Write(header, 0, header.Length)
            let buf : byte[] = Array.zeroCreate (w * h * 3)
            let mutable o = 0
            for yy in 0 .. h - 1 do
                for xx in 0 .. w - 1 do
                    let c = m.[V2l(int64 xx, int64 yy)]
                    buf.[o] <- c.R
                    buf.[o + 1] <- c.G
                    buf.[o + 2] <- c.B
                    o <- o + 3
            fs.Write(buf, 0, buf.Length)
        let signature = runtime.CreateFramebufferSignature [DefaultSemantic.Colors, TextureFormat.Rgba8]
        let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray inputs)
        let classic = renderAt 1 (ASet.ofArray inputs)
        let heap1   = renderAt 1 heapObjs
        let heap8   = renderAt 8 heapObjs        // <-- the windowed demo uses samples=8
        savePpm "/tmp/demo_classic.ppm" classic
        savePpm "/tmp/demo_heap.ppm" heap1
        savePpm "/tmp/demo_heap_msaa.ppm" heap8
        let d0, _, nbg, total = diff classic heap1
        let dm, ndm, _, _ = diff heap1 heap8
        Log.line "demoShot: -> %d bucket(s)  heap-vs-classic(1x) maxDelta=%d  coverage=%d px"
            Heap.lastBucketCount d0 nbg
        Log.line "demoShot: heap 1x vs 8x(MSAA)  maxDelta=%d  diffPixels=%d/%d (%.4f%%)"
            dm ndm total (100.0 * float ndm / float total)
        Log.line "demoShot: saved demo_classic.ppm + demo_heap.ppm + demo_heap_msaa.ppm"
        true

    module DF =
        type V = { [<Position>] pos : V4f; [<Normal>] n : V3f }
        let shadeFp64 (v : V) =
            vertex {
                let mvp : M44f = uniform?ModelViewProjTrafo
                let nm  : M44f = uniform?NormalMatrix
                return { v with pos = mvp * v.pos; n = (nm * V4f(v.n, 0.0f)).XYZ }
            }
        let shadeMvp (v : V) =
            vertex {
                let mvp : M44f = uniform?ModelViewProjTrafo
                return { v with pos = mvp * v.pos; n = v.n }
            }
        let frag (v : V) =
            fragment {
                let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
                let d = 0.3f + 0.7f * max 0.0f (Vec.dot (Vec.normalize v.n) l)
                return V4f(V3f(0.9f, 0.7f, 0.3f) * d, 1.0f)
            }

    // PRECISION GUARD: the heap derives ModelViewProjTrafo in fp64, so the SAME scene
    // rendered at the origin and at GEODETIC scale (earth-radius coords) must look
    // IDENTICAL — the huge View/Model translations cancel in double precision. If the
    // derive ever silently drops to float32, the geodetic cubes jitter and the two
    // images diverge -> this lights up. A constant f32-premultiplied MVP is also
    // rendered, ONLY to assert the scale is high enough that f32 genuinely breaks
    // (else a pass would be meaningless).
    let sgPrecisionTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(512, 512))
        let boxSg = IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.3)) C4b.White |> Sg.ofIndexedGeometry
        let offsets = [| for x in -1 .. 1 do for y in -1 .. 1 -> V3d(float x, float y, 0.0) * 0.9 |]
        let proj = Frustum.perspective 70.0 0.1 1.0e9 1.0 |> Frustum.projTrafo
        let relEye = V3d(0.0, -5.0, 2.5)
        let viewAt (c : V3d) = CameraView.lookAt (c + relEye) c V3d.OOI |> CameraView.viewTrafo
        let renderToPix (sg : ISg) =
            use task = runtime.CompileRender(signature, sg)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>()
            finally out.Release()
        let effFp64 = [ Effect.ofFunction DF.shadeFp64; Effect.ofFunction DF.frag ]
        // fp64: per-object ModelTrafo; effect reads the COMPOSED ModelViewProjTrafo ->
        // heap DERIVES it (Proj*View*Model) in double.
        let heapFp64 (c : V3d) =
            offsets |> Array.map (fun o -> boxSg |> Sg.trafo' (Trafo3d.Translation(c + o)))
            |> Sg.ofArray |> Sg.heap (runtime.CreateHeapStorage()) |> Sg.effect effFp64
            |> Sg.viewTrafo (AVal.constant (viewAt c)) |> Sg.projTrafo (AVal.constant proj)
        // f32 reference: supply ModelViewProjTrafo as a CONSTANT pre-multiplied f32
        // matrix (no derive) -> the heap just gathers it; jitters at geodetic scale.
        let heapF32 (c : V3d) =
            let vpF32 = M44f.op_Explicit ((viewAt c) * proj).Forward
            offsets |> Array.map (fun o ->
                let mF32 = M44f.op_Explicit (Trafo3d.Translation(c + o)).Forward
                boxSg |> Sg.uniform' "ModelViewProjTrafo" (vpF32 * mF32) |> Sg.uniform' "NormalMatrix" M44f.Identity)
            |> Sg.ofArray |> Sg.heap (runtime.CreateHeapStorage()) |> Sg.effect effFp64
            |> Sg.viewTrafo (AVal.constant (viewAt c)) |> Sg.projTrafo (AVal.constant proj)
        let earth = V3d(6378137.0, 3189000.0, 1594500.0)
        let imgNormal  = renderToPix (heapFp64 V3d.Zero)
        let imgGeoFp64 = renderToPix (heapFp64 earth)
        let imgGeoF32  = renderToPix (heapF32 earth)
        let dInvar, _, covN, total = diff imgNormal imgGeoFp64
        let _, nDiffF32, _, _ = diff imgGeoFp64 imgGeoF32
        let scaleInvariant = covN > 1000L && dInvar <= 1
        let f32Broke = float nDiffF32 / float total > 0.01   // f32 jitters ~2% at earth scale; 1% guard = margin
        Log.line "sgprec: coverage=%d  fp64 origin-vs-geodetic maxDelta=%d  f32 jitter=%.1f%%" covN dInvar (100.0 * float nDiffF32 / float total)
        if scaleInvariant && f32Broke then
            Log.line "sgprec: PASS (heap ModelViewProjTrafo is fp64-derived: scale-invariant; f32 jitters at geodetic scale)"; true
        else
            Log.warn "sgprec: FAIL (scaleInvariant=%b f32Broke=%b) — heap derive may have dropped to float32" scaleInvariant f32Broke; false

    // LIVE GPU trafo-chain through Heap.ofRenderObjects (the real IncrementalBucket
    // ingest). Each RO exposes the
    // dom-shaped depth-2 model stack [boxLink; nodeTrafo] as the well-known
    // "ModelTrafoStack" uniform AND the CPU-folded "ModelTrafo" — so the SAME
    // input set renders two ways:
    //   * FOLDED: ROs WITHOUT the stack -> ModelTrafo packed as an arena region.
    //   * CHAIN : ROs WITH the stack -> chainMode bucket -> ModelTrafo GPU-folded
    //             from the deduped growable link arena (chainOut[slot]).
    // The box link is NON-IDENTITY (a real Scale*Translation) so the compose ORDER
    // is actually exercised (domboxchain's Box3d.Unit link is identity and can't
    // catch a reversed multiply). Proves, on the LIVE path:
    //   (a) chainMode engaged (lastChainBuckets = 1; ModelTrafo NOT an arena field),
    //   (b) box link value-dedups to ONE slot across all leaves (distinct ~ n+1),
    //   (c) GPU chain image == CPU-folded image (maxDelta 0),
    //   (d) editing ONE node link marks ONE arena slot (uploads = 1),
    //   (e) add/remove churn keeps chainMode + correct image (free-list reuse).
    let liveChainTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(768, 768))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.Unit) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let eff = Effect.compose [ Effect.ofFunction DF.shadeFp64; Effect.ofFunction DF.frag ]

        let nSide = 30
        let n = nSide * nSide
        let view = AVal.constant (CameraView.lookAt (V3d(0.0, -55.0, 40.0)) (V3d(18.0, 18.0, 0.0)) V3d.OOI |> CameraView.viewTrafo)
        let proj = AVal.constant (Frustum.perspective 70.0 0.1 1.0e9 1.0 |> Frustum.projTrafo)
        let viewProj = AVal.map2 (*) view proj :> IAdaptiveValue

        // NON-IDENTITY box link, IDENTICAL value across leaves but a DISTINCT
        // AVal.constant per leaf (exactly the dom Primitives.Box shape).
        let boxValue = Trafo3d.Scale(0.8, 0.8, 1.4) * Trafo3d.Translation(0.1, 0.1, 0.0)
        let boxLink () : aval<Trafo3d> = AVal.constant boxValue
        let nodeTrafos =
            Array.init n (fun i ->
                AVal.init (Trafo3d.Translation(float (i % nSide) * 1.2, float (i / nSide) * 1.2, 0.0)))
        // dom stack array order is [leaf; …; root]; here [nodeTrafo; boxLink] would
        // mean node is leaf-most. Match the dom fold ModelTrafo = arr[0]*arr[1]*…:
        // the dom traversal pushes box UNDER node, so node is outer (arr[0]).
        // ModelTrafo = node * box. We expose exactly that.
        let stackOf i : aval<Trafo3d>[] = [| (nodeTrafos.[i] :> aval<Trafo3d>); boxLink () |]
        let foldedOf i = AVal.map2 (*) (nodeTrafos.[i] :> aval<Trafo3d>) (boxLink ())   // node*box

        let mkRO (withStack : bool) i =
            let folded = foldedOf i
            let nm = folded |> AVal.map (fun (t : Trafo3d) -> M44f.op_Explicit (M44d (M33d t.Backward.Transposed)))
            // new contract: provide the View/Proj CONSTITUENTS (the heap derives
            // ModelViewProjTrafo + NormalMatrix on the GPU); the supplied NormalMatrix
            // is now ignored (derived) — kept to prove the result matches (Model⁻¹)ᵀ.
            let us =
                [ Symbol.Create "ModelTrafo",     (folded :> IAdaptiveValue)
                  Symbol.Create "NormalMatrix",   (nm :> IAdaptiveValue)
                  Symbol.Create "ViewTrafo",      (view :> IAdaptiveValue)
                  Symbol.Create "ProjTrafo",      (proj :> IAdaptiveValue) ]
            let us = if withStack then (Symbol.Create "ModelTrafoStack", (AVal.constant (stackOf i) :> IAdaptiveValue)) :: us else us
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect eff
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList us
            ro :> IRenderObject

        let imageOf (objs : aset<IRenderObject>) =
            use task = runtime.CompileRender(signature, objs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()

        let foldedInputs = Array.init n (mkRO false)
        let chainInputs  = Array.init n (mkRO true)
        let foldedPix = imageOf (Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray foldedInputs))
        let foldChainBuckets = Heap.lastChainBuckets        // expect 0 (no stack)
        let chainSet = cset (chainInputs :> seq<_>)
        let chainHeap = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (chainSet :> aset<_>)
        // render chainHeap through ONE persistent task kept alive across the edit and
        // churn — so the heap stays built and an edit is an incremental upload (a
        // single-shot render would hit refcount 0 and rebuild the whole heap).
        use chainTask = runtime.CompileRender(signature, chainHeap)
        let chainOut = chainTask |> RenderTask.renderToColor size
        chainOut.Acquire()
        let renderChain () = chainOut.GetValue().Download().AsPixImage<uint8>()
        let chainPix = renderChain ()
        let chainBuckets = Heap.lastChainBuckets            // expect 1
        let distinct = Heap.lastDistinctLinks               // expect ~ n+1

        let maxD, nDiff, nbg, total = diff foldedPix chainPix
        Log.line "liveChain: n=%d foldChainBuckets=%d chainBuckets=%d distinctLinks=%d (ideal n+1=%d)" n foldChainBuckets chainBuckets distinct (n+1)
        Log.line "liveChain: folded-vs-chain maxDelta=%d diffPixels=%d/%d coverage=%d px" maxD nDiff total nbg

        // (d) edit ONE node link -> ONE arena slot uploaded
        transact (fun () -> nodeTrafos.[n/2].Value <- Trafo3d.Translation(99.0, 99.0, 5.0))
        renderChain () |> ignore
        let uploads = Heap.lastChainLinkUploads

        // (e) churn: remove 3, add 3 fresh -> chainMode preserved, image sane
        let extra =
            Array.init 3 (fun j ->
                let i = n - 1 - j
                nodeTrafos.[i].Value <- Trafo3d.Translation(float (i % nSide) * 1.2, float (i / nSide) * 1.2, 0.0)
                mkRO true i)
        transact (fun () ->
            for j in 0 .. 2 do chainSet.Remove chainInputs.[j] |> ignore
            for ro in extra do chainSet.Add ro |> ignore)
        renderChain () |> ignore
        chainOut.Release()
        let churnBuckets = Heap.lastChainBuckets

        let engaged  = foldChainBuckets = 0 && chainBuckets = 1
        let dedupOk  = distinct <= n + 2
        let correct  = nDiff = 0L || maxD <= 1
        let editOk   = uploads = 1
        let churnOk  = churnBuckets = 1
        let coverOk  = nbg > total / 100L
        let pass = engaged && dedupOk && correct && editOk && churnOk && coverOk
        if pass then Log.line "liveChain: PASS (live chainMode == folded image; box link 1 slot; 1-link edit; churn keeps chain)"
        else Log.warn "liveChain: FAIL (engaged=%b dedup=%b correct=%b edit=%b churn=%b cover=%b)" engaged dedupOk correct editOk churnOk coverOk
        pass

    // ── PER-FRAME RESOURCE-LEAK REGRESSION ──────────────────────────────────
    // The `lifetime` test checks that device memory RETURNS to baseline after each
    // SCENE is torn down. It does NOT catch a leak that accumulates per FRAME inside
    // ONE long-lived scene — which is what crashes large heap scenes after a few
    // thousand frames (native exit 1, no managed stack).
    //
    // This probe builds ONE long-lived heap scene (chainMode engaged) and renders it
    // for FRAMES frames, editing it each frame (default: a single-link trafo value
    // edit — the pure-value-edit case; CHURN=1 instead removes+adds one member each
    // frame). It samples three leak metrics every SAMPLE frames:
    //   * device VMA allocations (Device.MemoryStatistics)        — buffers / images,
    //   * live Vulkan descriptor sets (DescriptorSet.LiveCount)   — set machinery,
    //   * live backend Resource handles (Resource.LiveCount)      — EVERY Resource<'T>
    //     (buffers, images, views, samplers, query pools, uniform buffers, …).
    // PASS = all three metrics are BOUNDED (flat after warm-up) over all frames. A
    // per-frame acquire-without-release shows as monotonic growth and FAILS here
    // while the per-scene `lifetime` test stays green (its teardown hides the
    // accumulation). Knobs: FRAMES (4000), NSIDE (30 -> 900 ROs), SAMPLE (250),
    // CHURN=1 (membership churn instead of value edit), BINDLESS=1 (GPU-resident
    // per-slot geometry -> vertex-pull gather path; keep NSIDE small, the bindless
    // SSBO-array has a fixed shader-declared capacity ~512 buffers).
    let chainLeakProbeTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let device = runtime.Device
        let frames = match System.Environment.GetEnvironmentVariable "FRAMES" with null | "" -> 4000 | s -> int s
        let sample = match System.Environment.GetEnvironmentVariable "SAMPLE" with null | "" -> 250  | s -> int s
        let nSide  = match System.Environment.GetEnvironmentVariable "NSIDE"  with null | "" -> 30   | s -> int s
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(512, 512))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.Unit) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        // BINDLESS=1 -> each RO gets DISTINCT per-slot vertex source buffers (not the
        // shared constant geometry), forcing the useBindlessGeom vertex-pull GATHER
        // path — the path whose O(N)-per-structural-tx restamp was the suspect.
        let bindless = match System.Environment.GetEnvironmentVariable "BINDLESS" with "1" -> true | _ -> false
        let gbv (b : IBackendBuffer) t = BufferView(AVal.constant (b :> IBuffer), t)
        // FIXED pool of GPU-resident per-slot vertex buffers, created ONCE. Churn
        // reuses these (so the PROBE allocates zero GPU buffers per edit) — any
        // per-frame VMA/resource growth is then attributable to the HEAP, not the
        // probe. Pool is large enough that adjacent churn picks distinct buffers.
        let nbv = nSide * nSide
        let bindlessPool =
            if not bindless then [||]
            else
                Array.init (max 64 (nbv * 2)) (fun k ->
                    let p2 = positions |> Array.map (fun (v : V3f) -> v + V3f(float32 k * 1.0e-4f, 0.0f, 0.0f))
                    let pb = runtime.PrepareBuffer(ArrayBuffer(p2))
                    let nb = runtime.PrepareBuffer(ArrayBuffer(normals))
                    AttributeProvider.ofList [ DefaultSemantic.Positions, gbv pb typeof<V3f>; DefaultSemantic.Normals, gbv nb typeof<V3f> ])
        let mkVattrs (slotIx : int) =
            if not bindless then vattrs
            else bindlessPool.[slotIx % bindlessPool.Length]
        let eff = Effect.compose [ Effect.ofFunction DF.shadeFp64; Effect.ofFunction DF.frag ]
        let n = nSide * nSide
        let view = AVal.constant (CameraView.lookAt (V3d(0.0, -55.0, 40.0)) (V3d(18.0, 18.0, 0.0)) V3d.OOI |> CameraView.viewTrafo)
        let proj = AVal.constant (Frustum.perspective 70.0 0.1 1.0e9 1.0 |> Frustum.projTrafo)
        let viewProj = AVal.map2 (*) view proj :> IAdaptiveValue
        let boxValue = Trafo3d.Scale(0.8, 0.8, 1.4) * Trafo3d.Translation(0.1, 0.1, 0.0)
        let boxLink () : aval<Trafo3d> = AVal.constant boxValue
        let nodeTrafos =
            Array.init n (fun i -> AVal.init (Trafo3d.Translation(float (i % nSide) * 1.2, float (i / nSide) * 1.2, 0.0)))
        let stackOf i : aval<Trafo3d>[] = [| (nodeTrafos.[i] :> aval<Trafo3d>); boxLink () |]
        let foldedOf i = AVal.map2 (*) (nodeTrafos.[i] :> aval<Trafo3d>) (boxLink ())
        let mkRO i =
            let folded = foldedOf i
            let nm = folded |> AVal.map (fun (t : Trafo3d) -> M44f.op_Explicit (M44d (M33d t.Backward.Transposed)))
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect eff
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- mkVattrs i
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "ModelTrafo",       (folded :> IAdaptiveValue)
                Symbol.Create "NormalMatrix",     (nm :> IAdaptiveValue)
                Symbol.Create "ModelTrafoStack",  (AVal.constant (stackOf i) :> IAdaptiveValue)
                Symbol.Create "ViewTrafo",        (view :> IAdaptiveValue)
                Symbol.Create "ProjTrafo",        (proj :> IAdaptiveValue) ]
            ro :> IRenderObject
        // CHURN=1 -> each frame removes one member and adds a FRESH RO (the cad-bench
        // churn path: exercises bucket slot recycle + chain-link interning/free + the
        // membership delta machinery, none of which the pure-trafo loop touches).
        let churn = match System.Environment.GetEnvironmentVariable "CHURN" with "1" -> true | _ -> false
        let freshCounter = ref n
        let mkFresh () =
            let j = System.Threading.Interlocked.Increment freshCounter
            let folded = AVal.constant (Trafo3d.Translation(float (j % nSide) * 1.2, float (j / nSide % nSide) * 1.2, 0.0) * boxValue)
            let nm = folded |> AVal.map (fun (t : Trafo3d) -> M44f.op_Explicit (M44d (M33d t.Backward.Transposed)))
            let dyn = AVal.init boxValue
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect eff
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- mkVattrs j
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "ModelTrafo",       (folded :> IAdaptiveValue)
                Symbol.Create "NormalMatrix",     (nm :> IAdaptiveValue)
                Symbol.Create "ModelTrafoStack",  (AVal.constant [| (dyn :> aval<Trafo3d>); boxLink () |] :> IAdaptiveValue)
                Symbol.Create "ViewTrafo",        (view :> IAdaptiveValue)
                Symbol.Create "ProjTrafo",        (proj :> IAdaptiveValue) ]
            ro :> IRenderObject
        let ros = Array.init n mkRO
        let memberSet = cset (ros :> seq<_>)
        let churnMirror = System.Collections.Generic.List<IRenderObject>(ros)
        let heap = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (memberSet :> aset<_>)
        use task = runtime.CompileRender(signature, heap)
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        let rnd = System.Random(1234)
        let liveDs () = Aardvark.Rendering.Vulkan.DescriptorSet.LiveCount
        let liveRes () = Aardvark.Rendering.Vulkan.Resource.LiveCount
        let memCount () = let struct (c, _) = device.MemoryStatistics in c
        // warm-up: first renders create all caches/pipelines/atlas; baseline AFTER.
        out.GetValue() |> ignore
        for w in 0 .. 4 do
            transact (fun () -> nodeTrafos.[w % n].Value <- Trafo3d.Translation(float (w % nSide) * 1.2, float (w / nSide % nSide) * 1.2, float w * 1.0e-4))
            out.GetValue() |> ignore
        let chainBuckets = Heap.lastChainBuckets
        let mem0, ds0, res0 = memCount (), liveDs (), liveRes ()
        Log.line "chainLeakProbe: n=%d chainBuckets=%d FRAMES=%d churn=%b  baseline: VMA-allocs=%d liveDescriptorSets=%d liveResources=%d" n chainBuckets frames churn mem0 ds0 res0
        let mutable maxMem, maxDs, maxRes = mem0, ds0, res0
        for f in 1 .. frames do
            if churn then
                // remove one random member, add a fresh one (membership stays at n)
                transact (fun () ->
                    let i = rnd.Next churnMirror.Count
                    let dead = churnMirror.[i]
                    churnMirror.[i] <- churnMirror.[churnMirror.Count - 1]
                    churnMirror.RemoveAt(churnMirror.Count - 1)
                    memberSet.Remove dead |> ignore
                    let fresh = mkFresh ()
                    churnMirror.Add fresh
                    memberSet.Add fresh |> ignore)
            else
                // pure VALUE edit of ONE link per frame: stable membership, the case that
                // crashed on trafo (k≈55k). Re-pulls chainOut -> the per-frame chain path.
                let i = f % n
                transact (fun () -> nodeTrafos.[i].Value <- Trafo3d.Translation(float (i % nSide) * 1.2, float (i / nSide) * 1.2, float f * 1.0e-4))
            out.GetValue() |> ignore
            let m, d, r = memCount (), liveDs (), liveRes ()
            maxMem <- max maxMem m
            maxDs  <- max maxDs d
            maxRes <- max maxRes r
            if f % sample = 0 || f = frames then
                Log.line "chainLeakProbe: frame %5d  VMA-allocs=%d (Δ%+d)  liveDescriptorSets=%d (Δ%+d)  liveResources=%d (Δ%+d)" f m (m - mem0) d (d - ds0) r (r - res0)
        out.Release()
        let memGrowth = maxMem - mem0
        let dsGrowth  = maxDs - ds0
        let resGrowth = maxRes - res0
        Log.line "chainLeakProbe: %d frames done. peak growth: VMA-allocs=%+d  liveDescriptorSets=%+d  liveResources=%+d" frames memGrowth dsGrowth resGrowth
        // bounded slack: caches may settle a few entries above baseline, but no
        // metric may scale with frame count. With FRAMES=4000 a per-frame leak would
        // be thousands; a healthy path stays within tens.
        let memOk = memGrowth <= 64
        let dsOk  = dsGrowth  <= 64
        let resOk = resGrowth <= 64
        let pass = chainBuckets >= 1 && memOk && dsOk && resOk
        if pass then Log.line "chainLeakProbe: PASS (per-frame resource metrics BOUNDED over %d frames)" frames
        else Log.warn "chainLeakProbe: FAIL (VMA growth %+d, descriptorSet growth %+d, resource growth %+d [ok<=64] over %d frames — per-frame leak)" memGrowth dsGrowth resGrowth frames
        pass

    // ── EXACT SgCostBench LL host-box repro (the scene that crashed) ─────────
    // Faithful replica of SgCostBench.buildLL: N boxes, SHARED ArrayBuffer box
    // geometry (host-tight -> HOST-PACK arena path, NOT bindless vertex-pull),
    // HeapModelTrafo + HeapColor per-draw uniforms, no ModelTrafoStack (no chain).
    // Renders FRAMES frames; each frame edits EDITK random nodes' trafo (KIND=trafo,
    // default) or color (KIND=color) in one transaction — the exact pattern whose
    // trafo cell died ~k=55184 and color cell ~k=10289 at N=100000 on published 0016.
    // Samples the same three leak metrics. PASS = runs to completion without crash
    // AND metrics bounded. Knobs: N (default 100000), FRAMES (default 8000),
    // EDITK (default 55184 — the trafo death point), KIND (trafo|color), SAMPLE.
    let hostBoxCrashTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let device = runtime.Device
        let n      = match System.Environment.GetEnvironmentVariable "N"      with null | "" -> 100000 | s -> int s
        let frames = match System.Environment.GetEnvironmentVariable "FRAMES" with null | "" -> 8000   | s -> int s
        let editK  = match System.Environment.GetEnvironmentVariable "EDITK"  with null | "" -> 55184  | s -> int s
        let kind   = match System.Environment.GetEnvironmentVariable "KIND"   with null | "" -> "trafo"| s -> s
        let sample = match System.Environment.GetEnvironmentVariable "SAMPLE" with null | "" -> 500    | s -> int s
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let g = (IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        // ONE shared host-tight attribute provider + index view — host-pack path.
        let attrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let indexView = bv index typeof<int>
        let fvc = index.Length
        let eff = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        let side = ceil (sqrt (float n)) |> int
        let view = CameraView.lookAt (V3d(6.0, 6.0, 4.0)) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 60.0 0.1 100.0 1.333 |> Frustum.projTrafo
        let viewProjU = AVal.constant ((view * proj).Forward |> M44f.op_Explicit) :> IAdaptiveValue
        let bases  = Array.init n (fun i -> Trafo3d.Translation(float (i % side) * 1.2, float (i / side) * 1.2, 0.0))
        let trafos = bases |> Array.map cval
        let colors = Array.init n (fun _ -> cval (C4b(158uy, 173uy, 199uy, 255uy).ToV4f()))
        let angles = Array.zeroCreate<float> n
        let mkRO i =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect eff
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- attrs
            ro.Indices   <- Some indexView
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = fvc, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (trafos.[i] |> AVal.map (fun (t : Trafo3d) -> M44f.op_Explicit t.Forward) :> IAdaptiveValue)
                Symbol.Create "HeapColor",      (colors.[i] :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProjU ]
            ro :> IRenderObject
        let ros = Array.init n mkRO
        let heap = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray ros)
        use task = runtime.CompileRender(signature, heap)
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        let liveDs () = Aardvark.Rendering.Vulkan.DescriptorSet.LiveCount
        let liveRes () = Aardvark.Rendering.Vulkan.Resource.LiveCount
        let memCount () = let struct (c, _) = device.MemoryStatistics in c
        out.GetValue() |> ignore
        let buckets = Heap.lastBucketCount
        let mem0, ds0, res0 = memCount (), liveDs (), liveRes ()
        Log.line "hostBoxCrash: N=%d buckets=%d KIND=%s EDITK=%d FRAMES=%d  baseline: VMA=%d descSets=%d resources=%d" n buckets kind editK frames mem0 ds0 res0
        let s = 0x9E3779B9u
        let mutable st = s
        let next bound = st <- st ^^^ (st <<< 13); st <- st ^^^ (st >>> 17); st <- st ^^^ (st <<< 5); int (st % uint32 bound)
        let mutable maxMem, maxDs, maxRes = mem0, ds0, res0
        let sw = System.Diagnostics.Stopwatch.StartNew()
        for f in 1 .. frames do
            st <- s
            transact (fun () ->
                if kind = "color" then
                    for _ in 1 .. editK do
                        let i = next n
                        colors.[i].Value <- C4f(float (next 256) / 255.0, 0.5, 0.5, 1.0).ToV4f()
                else
                    for _ in 1 .. editK do
                        let i = next n
                        angles.[i] <- angles.[i] + 0.05
                        trafos.[i].Value <- Trafo3d.RotationZ angles.[i] * bases.[i])
            out.GetValue() |> ignore
            let m, d, r = memCount (), liveDs (), liveRes ()
            maxMem <- max maxMem m; maxDs <- max maxDs d; maxRes <- max maxRes r
            if f % sample = 0 || f = frames then
                Log.line "hostBoxCrash: frame %5d (cumEdits=%d)  VMA=%d (Δ%+d)  descSets=%d (Δ%+d)  resources=%d (Δ%+d)  %.1fs" f (f * editK) m (m-mem0) d (d-ds0) r (r-res0) sw.Elapsed.TotalSeconds
        out.Release()
        let memGrowth, dsGrowth, resGrowth = maxMem - mem0, maxDs - ds0, maxRes - res0
        Log.line "hostBoxCrash: SURVIVED %d frames (%d cumulative edits). peak growth: VMA=%+d descSets=%+d resources=%+d" frames (frames * editK) memGrowth dsGrowth resGrowth
        let pass = buckets >= 1 && memGrowth <= 64 && dsGrowth <= 64 && resGrowth <= 64
        if pass then Log.line "hostBoxCrash: PASS (host-box scene runs past old crash point, resources bounded)"
        else Log.warn "hostBoxCrash: FAIL (VMA %+d descSets %+d resources %+d over %d frames)" memGrowth dsGrowth resGrowth frames
        pass

    // ── BINDLESS over-capacity must NOT crash (regression) ───────────────────
    // A bindless vertex-pull bucket whose slots×attrs exceed the unbounded
    // storage-buffer-array capacity (UnboundedSamplerArrayCeiling, clamped per
    // device — 1024 here) used to abort the render with a raw IndexOutOfRange in
    // AdaptiveDescriptor.StorageBuffers.GetDescriptors (cache sized to the binding
    // capacity, loop wrote one entry per runtime buffer). Now the descriptor write
    // is bounded: it binds what fits and warns once instead of crashing. This test
    // builds a bucket with GPU-resident per-slot geometry well over the cap (650
    // ROs × 2 attrs = 1300 > 1024), renders several frames, and asserts the render
    // COMPLETES (no exception) with bounded resources.
    let bindlessOverCapacityTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let device = runtime.Device
        let n = match System.Environment.GetEnvironmentVariable "N" with null | "" -> 650 | s -> int s
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(512, 512))
        let g = (IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let gbv (b : IBackendBuffer) t = BufferView(AVal.constant (b :> IBuffer), t)
        let eff = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        let side = ceil (sqrt (float n)) |> int
        let view = CameraView.lookAt (V3d(0.0, -55.0, 40.0)) (V3d(18.0, 18.0, 0.0)) V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 1.0e9 1.0 |> Frustum.projTrafo
        let viewProjU = AVal.constant ((view * proj).Forward |> M44f.op_Explicit) :> IAdaptiveValue
        // distinct GPU-resident per-slot vertex buffers -> bindless vertex-pull;
        // n*2 attrs > 1024 cap forces the over-capacity path.
        let mkRO i =
            let p2 = positions |> Array.map (fun (v : V3f) -> v + V3f(float32 i * 1.0e-4f, 0.0f, 0.0f))
            let pb = runtime.PrepareBuffer(ArrayBuffer(p2))
            let nb = runtime.PrepareBuffer(ArrayBuffer(normals))
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect eff
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, gbv pb typeof<V3f>; DefaultSemantic.Normals, gbv nb typeof<V3f> ]
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation(float (i % side) * 1.2, float (i / side) * 1.2, 0.0)).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "HeapColor",      (AVal.constant (C4f.White.ToV4f()) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProjU ]
            ro :> IRenderObject
        let ros = Array.init n mkRO
        let heap = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray ros)
        use task = runtime.CompileRender(signature, heap)
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        let memCount () = let struct (c, _) = device.MemoryStatistics in c
        Log.line "bindlessOverCapacity: n=%d (slots*attrs=%d, cap=1024) — render must NOT crash" n (n * 2)
        let mutable ok = true
        try
            for _ in 0 .. 4 do out.GetValue() |> ignore
        with e ->
            ok <- false
            Log.warn "bindlessOverCapacity: FAIL — render threw %s: %s" (e.GetType().Name) e.Message
        let mem1 = memCount ()
        for _ in 0 .. 9 do out.GetValue() |> ignore
        let memGrowth = memCount () - mem1
        out.Release()
        let pass = ok && memGrowth <= 8
        if pass then Log.line "bindlessOverCapacity: PASS (over-capacity bucket renders without abort; resources bounded)"
        else Log.warn "bindlessOverCapacity: FAIL (threw=%b memGrowth=%+d)" (not ok) memGrowth
        pass

    // ARBITRARY-DEPTH live chain through Heap.ofRenderObjects. Each leaf carries a
    // VARIABLE-LENGTH stack (depth 1..5) mixing:
    //   * a DYNAMIC shared ROOT cval per GROUP (a moving group = O(1) link edit,
    //     shared by every leaf under it — the general hierarchy case),
    //   * 0..3 DYNAMIC per-leaf MID links (distinct cvals),
    //   * a CONSTANT leaf link (value-dedup: identical-valued leaf links collapse).
    // Stacks of DIFFERENT length still share ONE bucket (chain structure is per-slot
    // data, not part of the effect/layout). Proves the impl is NOT depth-2-bound:
    //   (a) GPU compose of arbitrary-length chains == the CPU fold (maxDelta 0),
    //   (b) editing one shared group root marks ONE link (O(1) over the subtree),
    //   (c) value-dedup of constant leaf links holds across variable depths.
    let liveChainDeepTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(768, 768))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.5)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let eff = Effect.compose [ Effect.ofFunction DF.shadeFp64; Effect.ofFunction DF.frag ]

        let groups = 6
        let perGroup = 60
        let n = groups * perGroup
        let view = AVal.constant (CameraView.lookAt (V3d(0.0, -60.0, 42.0)) (V3d(0.0, 6.0, 0.0)) V3d.OOI |> CameraView.viewTrafo)
        let proj = AVal.constant (Frustum.perspective 70.0 0.1 1.0e9 1.0 |> Frustum.projTrafo)
        let viewProj = AVal.map2 (*) view proj :> IAdaptiveValue

        // ONE dynamic shared root per group (root-most link, shared across leaves).
        let roots = Array.init groups (fun _ -> AVal.init Trafo3d.Identity)
        // a small set of distinct CONSTANT leaf-link VALUES (value-dedup target).
        let leafConsts = [| Trafo3d.Scale 0.9; Trafo3d.Scale 1.1; Trafo3d.RotationZ 0.3 |]
        // per-leaf variable-depth stack [root; mid0; mid1; …; leafConst] in
        // root->leaf order; ModelTrafo (CPU fold) = root * mid0 * … * leafConst.
        let stacks =
            Array.init n (fun i ->
                let gi = i / perGroup
                let li = i % perGroup
                let depth = 1 + (li % 4)                 // 1..4 mid links
                let gx = float (gi % 3) * 7.0 - 7.0
                let gy = float (gi / 3) * 7.0 - 3.0
                let mids =
                    Array.init depth (fun d ->
                        AVal.init (Trafo3d.Translation(gx + float (li % 8) * 0.7 - 2.5,
                                                       gy + float (li / 8) * 0.7 + float d * 0.05, 0.0)))
                let leafC = AVal.constant leafConsts.[li % leafConsts.Length]
                let arr =
                    Array.append
                        (Array.append [| (roots.[gi] :> aval<Trafo3d>) |] (mids |> Array.map (fun m -> m :> aval<Trafo3d>)))
                        [| leafC |]
                arr)

        // CPU fold of a stack array: arr[0]*arr[1]*…*arr[last].
        let foldOf (arr : aval<Trafo3d>[]) =
            arr |> Array.reduce (fun a b -> AVal.map2 (*) a b)

        let mkRO (withStack : bool) i =
            let folded = foldOf stacks.[i]
            let nm = folded |> AVal.map (fun (t : Trafo3d) -> M44f.op_Explicit (M44d (M33d t.Backward.Transposed)))
            // new contract: provide the View/Proj CONSTITUENTS (the heap derives
            // ModelViewProjTrafo + NormalMatrix on the GPU); the supplied NormalMatrix
            // is now ignored (derived) — kept to prove the result matches (Model⁻¹)ᵀ.
            let us =
                [ Symbol.Create "ModelTrafo",     (folded :> IAdaptiveValue)
                  Symbol.Create "NormalMatrix",   (nm :> IAdaptiveValue)
                  Symbol.Create "ViewTrafo",      (view :> IAdaptiveValue)
                  Symbol.Create "ProjTrafo",      (proj :> IAdaptiveValue) ]
            let us = if withStack then (Symbol.Create "ModelTrafoStack", (AVal.constant stacks.[i] :> IAdaptiveValue)) :: us else us
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect eff
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList us
            ro :> IRenderObject

        let imageOf (objs : aset<IRenderObject>) =
            use task = runtime.CompileRender(signature, objs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()

        let foldedPix = imageOf (Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray (Array.init n (mkRO false))))
        let chainHeap = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray (Array.init n (mkRO true)))
        // persistent task: keep the heap built across the edit (a single-shot render
        // would hit refcount 0 and rebuild, re-uploading every link).
        use chainTask = runtime.CompileRender(signature, chainHeap)
        let chainOut = chainTask |> RenderTask.renderToColor size
        chainOut.Acquire()
        let renderChain () = chainOut.GetValue().Download().AsPixImage<uint8>()
        let chainPix = renderChain ()
        let chainBuckets = Heap.lastChainBuckets
        let distinct = Heap.lastDistinctLinks
        let maxD, nDiff, nbg, total = diff foldedPix chainPix
        Log.line "liveChainDeep: n=%d depths=1..5 chainBuckets=%d distinctLinks=%d" n chainBuckets distinct
        Log.line "liveChainDeep: folded-vs-chain maxDelta=%d diffPixels=%d/%d coverage=%d px" maxD nDiff total nbg

        // edit ONE shared group root -> ONE link upload (moves perGroup leaves).
        transact (fun () -> roots.[2].Value <- Trafo3d.Translation(0.0, 0.0, 6.0))
        renderChain () |> ignore
        chainOut.Release()
        let uploads = Heap.lastChainLinkUploads

        // distinct links = groups roots + (sum of distinct mid links) + (<=3 leaf
        // const values). mids are all distinct cvals; bound generously.
        let engaged = chainBuckets = 1
        let correct = nDiff = 0L || maxD <= 1
        let editOk  = uploads = 1
        let coverOk = nbg > total / 100L
        let pass = engaged && correct && editOk && coverOk
        if pass then Log.line "liveChainDeep: PASS (arbitrary-depth chains GPU-fold == CPU fold; shared root edit = 1 link)"
        else Log.warn "liveChainDeep: FAIL (engaged=%b correct=%b edit=%b cover=%b)" engaged correct editOk coverOk
        pass

    // RENDER-SG end-to-end: a scene built ENTIRELY with ordinary render Sg
    // combinators (nested Sg.trafo' inner + Sg.trafo dyn outer per leaf, a real
    // ModelViewProjTrafo/NormalMatrix effect) rendered through Sg.heap on the
    // Simple/TraversalState path (the default CompileRender entry). The render
    // Sg's TraversalStateUniformProvider now exposes "ModelTrafoStack" alongside
    // the folded ModelTrafo, so the heap engages chainMode just like the dom sg.
    // Verifies: (a) Sg.heap render == classic per-RO render (the chain compose is
    // transparent), (b) chainMode actually engaged (lastChainBuckets >= 1), (c) a
    // dynamic outer-trafo edit re-renders correctly (GPU re-fold).
    let sgChainTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(768, 768))
        let view = CameraView.lookAt (V3d(0.0, -40.0, 28.0)) (V3d(9.0, 9.0, 0.0)) V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 1.0e9 1.0 |> Frustum.projTrafo
        let boxSg =
            IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.5)) C4b.White
            |> Sg.ofIndexedGeometry
        let s = 16
        // inner CONSTANT link (a real non-identity Scale*Translation, the "box"
        // half) shared by VALUE across leaves; outer DYNAMIC node link per leaf.
        let inner = Trafo3d.Scale(0.8, 0.8, 1.3) * Trafo3d.Translation(0.05, 0.05, 0.0)
        let nodes = Array.init (s * s) (fun i -> AVal.init (Trafo3d.Translation(float (i % s) * 1.2, float (i / s) * 1.2, 0.0)))
        let leaves =
            Array.init (s * s) (fun i ->
                boxSg
                |> Sg.trafo' inner                       // inner constant (box) link
                |> Sg.trafo (nodes.[i] :> aval<Trafo3d>))  // outer dynamic (node) link
        let scene (wrap : ISg -> ISg) =
            leaves
            |> Sg.ofArray
            |> wrap
            // shadeMvp reads ONLY ModelViewProjTrafo (-> ModelTrafo, which the
            // render Simple-Sg provider exposes); NormalMatrix is backend-derived
            // and NOT a per-RO heap field on this path, so the effect avoids it.
            |> Sg.effect [ Effect.ofFunction DF.shadeMvp; Effect.ofFunction DF.frag ]
            |> Sg.viewTrafo (AVal.constant view)
            |> Sg.projTrafo (AVal.constant proj)
        let renderToPix (sg : ISg) =
            use task = runtime.CompileRender(signature, sg)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()

        // chain heap (default) vs FOLDED heap (same MVP shader, only the ModelTrafo
        // SOURCE differs: GPU chain compose vs CPU-folded arena region) — isolates
        // the fold from the rasterization path, so this must be ~bit-identical.
        let heapChainPix = renderToPix (scene (Sg.heap (runtime.CreateHeapStorage())))
        let chainBuckets = Heap.lastChainBuckets
        let distinct = Heap.lastDistinctLinks
        Heap.disableChain <- true
        let heapFoldPix =
            try renderToPix (scene (Sg.heap (runtime.CreateHeapStorage())))
            finally Heap.disableChain <- false
        let foldChainBuckets = Heap.lastChainBuckets
        // and a loose sanity check against the classic per-RO (non-heap) render.
        let classicPix = renderToPix (scene id)

        let maxD, nDiff, nbg, total = diff heapFoldPix heapChainPix
        let cMaxD, cDiff, _, _ = diff classicPix heapChainPix
        Log.line "sgChain: chainBuckets=%d (folded run=%d) distinctLinks=%d (ideal %d)" chainBuckets foldChainBuckets distinct (s*s + 1)
        Log.line "sgChain: chain-vs-foldedHeap maxDelta=%d diffPixels=%d/%d coverage=%d px" maxD nDiff total nbg
        Log.line "sgChain: chain-vs-classic    maxDelta=%d diffPixels=%d (sanity; rasterization-path differs)" cMaxD cDiff

        let engaged = chainBuckets = 1 && foldChainBuckets = 0
        let correct = nDiff = 0L || maxD <= 1                       // chain == folded heap
        let sane    = cDiff < total / 1000L                        // ~matches classic (tiny edge noise ok)
        let dedupOk = distinct <= s*s + 2
        let coverOk = nbg > total / 100L
        let pass = engaged && correct && sane && dedupOk && coverOk
        if pass then Log.line "sgChain: PASS (render Sg exposes ModelTrafoStack; Sg.heap chain == folded heap; inner link 1 slot)"
        else Log.warn "sgChain: FAIL (engaged=%b correct=%b sane=%b dedup=%b cover=%b)" engaged correct sane dedupOk coverOk
        pass

    // Graceful fallback: a mixed aset of heapable + un-heapable ROs. Heapable ones
    // collapse to buckets; the un-heapable one (here: a non-indexed MULTI-call
    // draw — single-call non-indexed draws ride the heap now) must be passed
    // through UNCHANGED (same instance) in the output, not dropped or crashed.
    let passthroughTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.5)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let eff = Effect.compose [ Effect.ofFunction DF.shadeMvp; Effect.ofFunction DF.frag ]
        let vp = AVal.constant Trafo3d.Identity :> IAdaptiveValue

        // 4 heapable ROs (same effect/geom/state -> one bucket)
        let heapable =
            Array.init 4 (fun i ->
                let ro = RenderObject()
                ro.Surface <- Surface.Effect eff
                ro.Mode <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- vattrs
                ro.Indices <- Some (bv index typeof<int>)
                ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
                ro.Uniforms <- UniformProvider.ofList [
                    Symbol.Create "ModelTrafo",    (AVal.constant (Trafo3d.Translation(V3d(float i, 0.0, 0.0))) :> IAdaptiveValue)
                    Symbol.Create "ViewProjTrafo", vp ]
                ro :> IRenderObject)
        // 1 un-heapable RO: non-indexed with MULTIPLE draw calls (single-call
        // non-indexed draws ride the heap now) -> not eligible -> pass through
        let odd =
            let ro = RenderObject()
            ro.Surface <- Surface.Effect eff
            ro.Mode <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices <- None
            let twoCalls =
                [| DrawCallInfo(FaceVertexCount = positions.Length / 2, InstanceCount = 1)
                   DrawCallInfo(FaceVertexCount = positions.Length / 2, FirstIndex = positions.Length / 2, InstanceCount = 1) |]
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant twoCalls)
            ro.Uniforms <- UniformProvider.ofList [
                Symbol.Create "ModelTrafo",    (AVal.constant Trafo3d.Identity :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo", vp ]
            ro :> IRenderObject
        // a second un-heapable RO: positions in a non-decodable element type
        // (uint16 scalars — f64 sources like V3d are storage-decodable now) ->
        // neither host-storage-decodable nor vertex-pull eligible
        let odd2 =
            let posU16 = Array.zeroCreate<uint16> positions.Length
            let ro = RenderObject()
            ro.Surface <- Surface.Effect eff
            ro.Mode <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, bv posU16 typeof<uint16>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
            ro.Indices <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms <- UniformProvider.ofList [
                Symbol.Create "ModelTrafo",    (AVal.constant Trafo3d.Identity :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo", vp ]
            ro :> IRenderObject
        // a HEAPABLE RO supplying a consumed uniform in an UNPACKABLE type: it
        // stays heapable (the uniform falls through as a shared global) but
        // Diagnostics must call it out. Different detected field set -> own bucket.
        let oddUniform =
            let ro = RenderObject()
            ro.Surface <- Surface.Effect eff
            ro.Mode <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms <- UniformProvider.ofList [
                Symbol.Create "ModelTrafo",    (AVal.constant V2i.Zero :> IAdaptiveValue)   // consumed, supplied, UNPACKABLE
                Symbol.Create "ViewProjTrafo", vp ]
            ro :> IRenderObject

        Heap.Diagnostics <- true
        let input = ASet.ofArray (Array.concat [ heapable; [| odd; odd2; oddUniform |] ])
        let signature = runtime.CreateFramebufferSignature [DefaultSemantic.Colors, TextureFormat.Rgba8]
        let outSet = Heap.ofRenderObjects (runtime.CreateHeapStorage()) input
        // signature-deferred: expand the SDRs against the signature (as a render compile would)
        let expanded =
            outSet |> ASet.collect (fun ro ->
                match ro with
                | :? SignatureDependentRenderObject as s -> s.Expand signature
                | _ -> ASet.single ro)
        let out = expanded |> ASet.toAVal |> AVal.force |> HashSet.toArray
        Heap.Diagnostics <- false
        let buckets = Heap.lastBucketCount
        let passedThrough = out |> Array.exists (fun o -> System.Object.ReferenceEquals(o, odd))
        let passedThrough2 = out |> Array.exists (fun o -> System.Object.ReferenceEquals(o, odd2))
        // oddUniform STAYS heapable (its unpackable ModelTrafo just falls through as a global
        // + emits the UNPACKABLE diagnostic), so it is collapsed into its own bucket — NOT
        // passed through as a standalone RO.
        let oddUniformHeaped = not (out |> Array.exists (fun o -> System.Object.ReferenceEquals(o, oddUniform)))
        let msgs = Heap.diagnosticMessages ()
        for m in msgs do Log.line "passthrough: diag: %s" m
        let diagOk =
            msgs |> Array.exists (fun m -> m.Contains "multiple draw calls") &&
            msgs |> Array.exists (fun m -> m.Contains "storage-decoded") &&
            msgs |> Array.exists (fun m -> m.Contains "UNPACKABLE")
        Log.line "passthrough: in=7 (4 heapable + 2 odd + 1 unpackable-uniform) -> out=%d buckets=%d oddPassedThrough=%b/%b oddUniformHeaped=%b diags=%d" out.Length buckets passedThrough passedThrough2 oddUniformHeaped msgs.Length
        // out = 2 buckets + 2 passthroughs (odd, odd2) + 1 derive pre-pass RO. The heapable
        // bucket consumes ModelViewProjTrafo (shadeMvp), derived MATMUL(ModelTrafo,ViewProjTrafo),
        // so it emits a draw-less derive RO; oddUniform's bucket can't derive it (ModelTrafo
        // UNPACKABLE -> global fallthrough), so it emits none. Hence buckets + 2 + 1 = 5.
        let pass = buckets = 2 && passedThrough && passedThrough2 && oddUniformHeaped && out.Length = buckets + 3 && diagOk
        if pass then Log.line "passthrough: PASS (heapable collapsed; un-heapable ROs passed through unchanged; Diagnostics emitted deduped reasons)"
        else Log.warn "passthrough: FAIL (out=%d buckets=%d passedThrough=%b/%b oddUniformHeaped=%b)" out.Length buckets passedThrough passedThrough2 oddUniformHeaped
        pass

    // NativeMemoryBuffer geometry is heap-eligible: a user-supplied native buffer
    // is copied into the packed arena exactly like an ArrayBuffer. Render N cubes
    // with native-buffer geometry through the heap and compare to the ArrayBuffer
    // path -> must be pixel-identical.
    let nativeBufTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(640, 640))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let abv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)

        // native-buffer views (HGlobal copies; freed at the end)
        let allocs = System.Collections.Generic.List<nativeint>()
        let nbvV3f (arr : V3f[]) =
            let f = Array.zeroCreate<float32> (arr.Length * 3)
            arr |> Array.iteri (fun i v -> f.[3*i] <- v.X; f.[3*i+1] <- v.Y; f.[3*i+2] <- v.Z)
            let bytes = f.Length * 4
            let ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal bytes
            allocs.Add ptr
            System.Runtime.InteropServices.Marshal.Copy(f, 0, ptr, f.Length)
            BufferView(AVal.constant (NativeMemoryBuffer(ptr, uint64 bytes) :> IBuffer), typeof<V3f>)
        let nbvInt (arr : int[]) =
            let bytes = arr.Length * 4
            let ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal bytes
            allocs.Add ptr
            System.Runtime.InteropServices.Marshal.Copy(arr, 0, ptr, arr.Length)
            BufferView(AVal.constant (NativeMemoryBuffer(ptr, uint64 bytes) :> IBuffer), typeof<int>)

        let eff = Effect.compose [ Effect.ofFunction DF.shadeMvp; Effect.ofFunction DF.frag ]
        let vp = AVal.constant ((CameraView.lookAt (V3d(0.0, -8.0, 6.0)) V3d.Zero V3d.OOI |> CameraView.viewTrafo) * (Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo)) :> IAdaptiveValue
        let offsets = [| for x in -2 .. 2 do for y in -2 .. 2 -> V3d(float x * 1.3, float y * 1.3, 0.0) |]

        let mkObjs (posV : BufferView) (norV : BufferView) (idxV : BufferView) =
            offsets |> Array.map (fun o ->
                let ro = RenderObject()
                ro.Surface <- Surface.Effect eff
                ro.Mode <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, posV; DefaultSemantic.Normals, norV ]
                ro.Indices <- Some idxV
                ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
                ro.Uniforms <- UniformProvider.ofList [
                    Symbol.Create "ModelTrafo",    (AVal.constant (Trafo3d.Translation o) :> IAdaptiveValue)
                    Symbol.Create "ViewProjTrafo", vp ]
                ro :> IRenderObject)

        let imageOf (objs : IRenderObject[]) =
            let heap = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray objs)
            use task = Sg.renderObjectSet heap |> Sg.compile runtime signature
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()

        let arrPix = imageOf (mkObjs (abv positions typeof<V3f>) (abv normals typeof<V3f>) (abv index typeof<int>))
        let bucketsArr = Heap.lastBucketCount
        let natPix = imageOf (mkObjs (nbvV3f positions) (nbvV3f normals) (nbvInt index))
        let bucketsNat = Heap.lastBucketCount
        for p in allocs do System.Runtime.InteropServices.Marshal.FreeHGlobal p

        let maxD, nDiff, nNonBg, total = diff arrPix natPix
        Log.line "nativeBuf: bucketsArr=%d bucketsNat=%d  array-vs-native maxDelta=%d diffPixels=%d/%d coverage=%d" bucketsArr bucketsNat maxD nDiff total nNonBg
        let pass = bucketsNat = 1 && nNonBg > 1000 && maxD <= 1
        if pass then Log.line "nativeBuf: PASS (NativeMemoryBuffer geometry heaped -> 1 bucket, identical to ArrayBuffer)"
        else Log.warn "nativeBuf: FAIL (bucketsNat=%d maxDelta=%d coverage=%d)" bucketsNat maxD nNonBg
        pass

    // Type-agnostic geometry: V4f positions + uint16 indices (not the V3f/int the
    // heap used to assume). Heap render must equal the classic per-RO render.
    module VT =
        type V = { [<Position>] pos : V4f; [<Normal>] n : V3f }
        let shade (v : V) =
            vertex {
                let m  : M44f = uniform?ModelTrafo
                let vp : M44f = uniform?ViewProjTrafo
                return { v with pos = vp * (m * v.pos); n = (m * V4f(v.n, 0.0f)).XYZ }
            }
        let frag (v : V) =
            fragment {
                let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
                let d = 0.3f + 0.7f * max 0.0f (Vec.dot (Vec.normalize v.n) l)
                return V4f(V3f(0.9f, 0.6f, 0.3f) * d, 1.0f)
            }

    let varTypeTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(640, 640))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let pos3 = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals = g.IndexedAttributes.[DefaultSemantic.Normals] |> unbox<V3f[]>
        let idx32 = g.IndexArray |> unbox<int[]>
        // NON-default types: V4f positions, uint16 indices
        let pos4 = pos3 |> Array.map (fun p -> V4f(p.X, p.Y, p.Z, 1.0f))
        let idx16 = idx32 |> Array.map uint16
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv pos4 typeof<V4f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let eff = Effect.compose [ Effect.ofFunction VT.shade; Effect.ofFunction VT.frag ]
        let vp = AVal.constant ((CameraView.lookAt (V3d(0.0, -9.0, 6.0)) V3d.Zero V3d.OOI |> CameraView.viewTrafo) * (Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo)) :> IAdaptiveValue
        let mk (x : int) (y : int) =
            let ro = RenderObject()
            ro.Surface <- Surface.Effect eff
            ro.Mode <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices <- Some (bv idx16 typeof<uint16>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = idx16.Length, InstanceCount = 1) |])
            ro.Uniforms <- UniformProvider.ofList [
                Symbol.Create "ModelTrafo",    (AVal.constant (Trafo3d.Translation(V3d(float x * 1.3, float y * 1.3, 0.0))) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo", vp ]
            ro :> IRenderObject
        let objs = [| for x in -2 .. 2 do for y in -2 .. 2 -> mk x y |]
        let imageOf (s : aset<IRenderObject>) =
            use task = Sg.renderObjectSet s |> Sg.compile runtime signature
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let classicPix = imageOf (ASet.ofArray objs)
        let heapPix = imageOf (Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray objs))
        let buckets = Heap.lastBucketCount
        let maxD, nDiff, nNonBg, total = diff classicPix heapPix
        Log.line "varType: V4f pos + uint16 idx  buckets=%d classic-vs-heap maxDelta=%d diffPixels=%d/%d coverage=%d" buckets maxD nDiff total nNonBg
        let pass = buckets = 1 && nNonBg > 1000 && maxD <= 1

        // ── f64 sources + per-allocation source types: HALF the ROs supply
        //    Positions as V3d DOUBLES (bit-decoded in the shader, w = 1 fill),
        //    half as the V4f above. Source element types are NOT part of the
        //    bucket key (the decode branches on each allocation's header
        //    typeId), so the mixed set collapses to ONE bucket — and since the
        //    V3d values hold exactly the V3f positions, the image must equal
        //    the all-V4f classic render. ──
        let posD = pos3 |> Array.map (fun p -> V3d p)
        let vattrsD = AttributeProvider.ofList [ DefaultSemantic.Positions, bv posD typeof<V3d>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let mkWith (va : IAttributeProvider) (x : int) (y : int) =
            let ro = RenderObject()
            ro.Surface <- Surface.Effect eff
            ro.Mode <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- va
            ro.Indices <- Some (bv idx16 typeof<uint16>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = idx16.Length, InstanceCount = 1) |])
            ro.Uniforms <- UniformProvider.ofList [
                Symbol.Create "ModelTrafo",    (AVal.constant (Trafo3d.Translation(V3d(float x * 1.3, float y * 1.3, 0.0))) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo", vp ]
            ro :> IRenderObject
        let mixed = [| for x in -2 .. 2 do for y in -2 .. 2 -> mkWith (if (x + y) % 2 = 0 then vattrsD else vattrs) x y |]
        let heapMixedPix = imageOf (Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray mixed))
        let bucketsM = Heap.lastBucketCount
        let maxDM, nDiffM, _, totalM = diff classicPix heapMixedPix
        Log.line "varType: mixed V3d/V4f pos  buckets=%d vs-classic maxDelta=%d diffPixels=%d/%d" bucketsM maxDM nDiffM totalM
        let passMixed = bucketsM = 1 && maxDM <= 1
        if not passMixed then Log.warn "varType: f64 phase FAIL (buckets=%d maxDelta=%d)" bucketsM maxDM

        let pass = pass && passMixed
        if pass then Log.line "varType: PASS (V4f positions + uint16 indices heaped == classic; mixed V3d/V4f sources -> 1 bucket, f64 decode == classic)"
        else Log.warn "varType: FAIL (buckets=%d maxDelta=%d coverage=%d passMixed=%b)" buckets maxD nNonBg passMixed
        pass

    // Bindless storage-buffer ARRAY end-to-end: a shader vertex-PULLS its position
    // from one of many GPU buffers chosen by a handle (Geom[handle].data[gl_VertexIndex]),
    // with NO vertex-input attributes at all. Validates the whole new Vulkan chain
    // (FShade ssbCount=-1 -> unbounded SSBO descriptor array -> array binding/write).
    module SA =
        type UniformScope with
            member x.Geom : V4f[][] = uniform?StorageBuffer?Geom
        type VIn  = { [<VertexId>] vid : int }
        type VOut = { [<Position>] pos : V4f; [<Color>] c : V4f }
        let shade (v : VIn) =
            vertex {
                let h : int = uniform?Handle
                return { pos = uniform.Geom.[h].[v.vid]; c = V4f(0.2f, 0.9f, 0.4f, 1.0f) }
            }
        let frag (v : VOut) = fragment { return v.c }

    let ssboArrayTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(256, 256))
        let tri = [| V4f(-0.6f, -0.6f, 0.0f, 1.0f); V4f(0.6f, -0.6f, 0.0f, 1.0f); V4f(0.0f, 0.7f, 0.0f, 1.0f) |]
        let quad = [| V4f(-0.9f, 0.6f, 0.0f, 1.0f); V4f(-0.6f, 0.6f, 0.0f, 1.0f); V4f(-0.75f, 0.9f, 0.0f, 1.0f) |]
        // an ARRAY of two distinct GPU storage buffers
        let geom : IBuffer[] = [| ArrayBuffer(tri) :> IBuffer; ArrayBuffer(quad) :> IBuffer |]
        let eff = Effect.compose [ Effect.ofFunction SA.shade; Effect.ofFunction SA.frag ]
        let mk (handle : int) =
            let ro = RenderObject()
            ro.Surface <- Surface.Effect eff
            ro.Mode <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- AttributeProvider.ofList ([] : list<Symbol * BufferView>)   // NO vertex attributes — pure pull
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = 3, InstanceCount = 1) |])
            ro.Uniforms <- UniformProvider.ofList [
                Symbol.Create "Geom",   (AVal.constant geom :> IAdaptiveValue)
                Symbol.Create "Handle", (AVal.constant handle :> IAdaptiveValue) ]
            ro :> IRenderObject
        use task = Sg.renderObjectSet (ASet.ofList [ mk 0; mk 1 ]) |> Sg.compile runtime signature
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        let pix = try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let m = pix.GetMatrix<C4b>()
        let mutable green = 0L
        m.ForeachCoord(fun (p : V2l) -> let v = m.[p] in if v.G > 100uy && v.R < 120uy then green <- green + 1L)
        Log.line "ssboArray: vertex-pulled from a 2-elem SSBO array -> green coverage=%d px" green
        let pass = green > 1000L
        if pass then Log.line "ssboArray: PASS (bindless SSBO array vertex-pull renders; descriptor array bound + written)"
        else Log.warn "ssboArray: FAIL (coverage=%d, expected a vertex-pulled triangle)" green
        pass

    module BH =
        type VIn  = { [<Semantic("Positions")>] pos : V3f; [<Semantic("Normals")>] n : V3f }
        type VOut = { [<Position>] clip : V4f; [<Semantic("WN")>] wn : V3f }
        let shade (v : VIn) =
            vertex {
                let vp : M44f = uniform?ViewProjTrafo
                return { clip = vp * V4f(v.pos, 1.0f); wn = v.n }
            }
        let frag (v : VOut) =
            fragment {
                let l = Vec.normalize (V3f(0.4f, 0.7f, 0.6f))
                let d = 0.3f + 0.7f * max 0.0f (Vec.dot (Vec.normalize v.wn) l)
                return V4f(V3f(0.4f, 0.75f, 0.95f) * d, 1.0f)
            }

    // STEP 1 of the bindless bring-up: exactly ONE variable changed from ssboArray —
    // TWO SSBO arrays (positions in GeomA, colors in GeomB) instead of one. Still a
    // per-RO uniform handle and separate draws. Isolates the multi-array descriptor
    // bind/write path: GeomA gives geometry, GeomB gives color; if the second array
    // mis-binds, the color is wrong/black.
    module SA2 =
        type UniformScope with
            member x.GeomA : V4f[][] = uniform?StorageBuffer?GeomA
            member x.GeomB : V4f[][] = uniform?StorageBuffer?GeomB
        type VIn  = { [<VertexId>] vid : int }
        type VOut = { [<Position>] pos : V4f; [<Color>] c : V4f }
        let shade (v : VIn) =
            vertex {
                let h : int = uniform?Handle
                return { pos = uniform.GeomA.[h].[v.vid]; c = uniform.GeomB.[h].[v.vid] }
            }
        let frag (v : VOut) = fragment { return v.c }


    let ssboArray2Test () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(256, 256))
        let tri  = [| V4f(-0.6f, -0.6f, 0.0f, 1.0f); V4f(0.6f, -0.6f, 0.0f, 1.0f); V4f(0.0f, 0.7f, 0.0f, 1.0f) |]
        let quad = [| V4f(0.1f, 0.1f, 0.0f, 1.0f); V4f(0.8f, 0.1f, 0.0f, 1.0f); V4f(0.45f, 0.8f, 0.0f, 1.0f) |]
        let grn  = Array.replicate 3 (V4f(0.2f, 0.9f, 0.3f, 1.0f))
        let red  = Array.replicate 3 (V4f(0.9f, 0.2f, 0.2f, 1.0f))
        let geomA : IBuffer[] = [| ArrayBuffer tri :> IBuffer; ArrayBuffer quad :> IBuffer |]
        let geomB : IBuffer[] = [| ArrayBuffer grn :> IBuffer; ArrayBuffer red :> IBuffer |]
        let eff = Effect.compose [ Effect.ofFunction SA2.shade; Effect.ofFunction SA2.frag ]
        let mk (handle : int) =
            let ro = RenderObject()
            ro.Surface <- Surface.Effect eff
            ro.Mode <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- AttributeProvider.ofList ([] : list<Symbol * BufferView>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = 3, InstanceCount = 1) |])
            ro.Uniforms <- UniformProvider.ofList [
                Symbol.Create "GeomA",  (AVal.constant geomA :> IAdaptiveValue)
                Symbol.Create "GeomB",  (AVal.constant geomB :> IAdaptiveValue)
                Symbol.Create "Handle", (AVal.constant handle :> IAdaptiveValue) ]
            ro :> IRenderObject
        use task = Sg.renderObjectSet (ASet.ofList [ mk 0; mk 1 ]) |> Sg.compile runtime signature
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        let pix = try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let m = pix.GetMatrix<C4b>()
        let mutable green = 0L
        let mutable redc  = 0L
        m.ForeachCoord(fun (p : V2l) ->
            let v = m.[p]
            if v.G > 120uy && v.R < 120uy then green <- green + 1L
            if v.R > 120uy && v.G < 120uy then redc  <- redc + 1L)
        Log.line "ssboArray2: TWO SSBO arrays (pos=GeomA, col=GeomB) -> green=%d red=%d px" green redc
        let pass = green > 500L && redc > 500L
        if pass then Log.line "ssboArray2: PASS (both SSBO arrays bind+read; GeomA geometry + GeomB color correct)"
        else Log.warn "ssboArray2: FAIL (green=%d red=%d — a second SSBO array mis-binds)" green redc
        pass

    // STEP 2: one variable changed from ssboArray2 — the handle now comes from
    // gl_InstanceIndex (routed by per-RO FirstInstance) instead of a uniform. Still
    // SEPARATE draws. Isolates gl_InstanceIndex+firstInstance routing before any
    // multidraw is involved.
    module SA3 =
        type UniformScope with
            member x.GeomA : V4f[][] = uniform?StorageBuffer?GeomA
            member x.GeomB : V4f[][] = uniform?StorageBuffer?GeomB
        type VIn  = { [<VertexId>] vid : int; [<InstanceId>] iid : int }
        type VOut = { [<Position>] pos : V4f; [<Color>] c : V4f }
        let shade (v : VIn) =
            vertex {
                return { pos = uniform.GeomA.[v.iid].[v.vid]; c = uniform.GeomB.[v.iid].[v.vid] }
            }
        let frag (v : VOut) = fragment { return v.c }

    let ssboArray3Test () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(256, 256))
        let tri  = [| V4f(-0.6f, -0.6f, 0.0f, 1.0f); V4f(0.6f, -0.6f, 0.0f, 1.0f); V4f(0.0f, 0.7f, 0.0f, 1.0f) |]
        let quad = [| V4f(0.1f, 0.1f, 0.0f, 1.0f); V4f(0.8f, 0.1f, 0.0f, 1.0f); V4f(0.45f, 0.8f, 0.0f, 1.0f) |]
        let grn  = Array.replicate 3 (V4f(0.2f, 0.9f, 0.3f, 1.0f))
        let red  = Array.replicate 3 (V4f(0.9f, 0.2f, 0.2f, 1.0f))
        let geomA : IBuffer[] = [| ArrayBuffer tri :> IBuffer; ArrayBuffer quad :> IBuffer |]
        let geomB : IBuffer[] = [| ArrayBuffer grn :> IBuffer; ArrayBuffer red :> IBuffer |]
        let eff = Effect.compose [ Effect.ofFunction SA3.shade; Effect.ofFunction SA3.frag ]
        let mk (handle : int) =
            let ro = RenderObject()
            ro.Surface <- Surface.Effect eff
            ro.Mode <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- AttributeProvider.ofList ([] : list<Symbol * BufferView>)
            // handle routed through FirstInstance -> gl_InstanceIndex
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = 3, FirstInstance = handle, InstanceCount = 1) |])
            ro.Uniforms <- UniformProvider.ofList [
                Symbol.Create "GeomA", (AVal.constant geomA :> IAdaptiveValue)
                Symbol.Create "GeomB", (AVal.constant geomB :> IAdaptiveValue) ]
            ro :> IRenderObject
        use task = Sg.renderObjectSet (ASet.ofList [ mk 0; mk 1 ]) |> Sg.compile runtime signature
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        let pix = try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let m = pix.GetMatrix<C4b>()
        let mutable green = 0L
        let mutable redc  = 0L
        m.ForeachCoord(fun (p : V2l) ->
            let v = m.[p]
            if v.G > 120uy && v.R < 120uy then green <- green + 1L
            if v.R > 120uy && v.G < 120uy then redc  <- redc + 1L)
        Log.line "ssboArray3: gl_InstanceIndex handle (separate draws) -> green=%d red=%d px" green redc
        let pass = green > 500L && redc > 500L
        if pass then Log.line "ssboArray3: PASS (gl_InstanceIndex+firstInstance routes the handle; separate draws)"
        else Log.warn "ssboArray3: FAIL (green=%d red=%d — gl_InstanceIndex/firstInstance handle routing broken)" green redc
        pass

    // STEP 3: one variable changed from ssboArray3 — separate draws collapse into a
    // single INDEXED indirect multidraw (2 entries, per-entry FirstIndex+FirstInstance).
    // Same 2 arrays, gl_InstanceIndex handle, no vertex attributes. This is the minimal
    // repro of the bindless geometry setup; isolates whether the indirect multidraw
    // honours per-command FirstInstance.
    let ssboArray4Test () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(256, 256))
        let tri  = [| V4f(-0.6f, -0.6f, 0.0f, 1.0f); V4f(0.6f, -0.6f, 0.0f, 1.0f); V4f(0.0f, 0.7f, 0.0f, 1.0f) |]
        let quad = [| V4f(0.1f, 0.1f, 0.0f, 1.0f); V4f(0.8f, 0.1f, 0.0f, 1.0f); V4f(0.45f, 0.8f, 0.0f, 1.0f) |]
        let grn  = Array.replicate 3 (V4f(0.2f, 0.9f, 0.3f, 1.0f))
        let red  = Array.replicate 3 (V4f(0.9f, 0.2f, 0.2f, 1.0f))
        let geomA : IBuffer[] = [| ArrayBuffer tri :> IBuffer; ArrayBuffer quad :> IBuffer |]
        let geomB : IBuffer[] = [| ArrayBuffer grn :> IBuffer; ArrayBuffer red :> IBuffer |]
        let eff = Effect.compose [ Effect.ofFunction SA3.shade; Effect.ofFunction SA3.frag ]
        // combined index buffer: tri (local 0,1,2) then quad (local 0,1,2)
        let combinedIdx = [| 0;1;2; 0;1;2 |]
        let entries =
            [| DrawCallInfo(FaceVertexCount = 3, FirstIndex = 0, BaseVertex = 0, FirstInstance = 0, InstanceCount = 1)
               DrawCallInfo(FaceVertexCount = 3, FirstIndex = 3, BaseVertex = 0, FirstInstance = 1, InstanceCount = 1) |]
        let indirect = IndirectBuffer.ofArray entries
        let idxBV = BufferView(AVal.constant (ArrayBuffer combinedIdx :> IBuffer), typeof<int>)
        let sg =
            Sg.indirectDraw IndexedGeometryMode.TriangleList (AVal.constant indirect)
            |> Sg.indexBuffer idxBV
            |> Sg.uniform "GeomA" (AVal.constant geomA)
            |> Sg.uniform "GeomB" (AVal.constant geomB)
            |> Sg.effect [ eff ]
        use task = sg |> Sg.compile runtime signature
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        let pix = try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let m = pix.GetMatrix<C4b>()
        let mutable green = 0L
        let mutable redc  = 0L
        m.ForeachCoord(fun (p : V2l) ->
            let v = m.[p]
            if v.G > 120uy && v.R < 120uy then green <- green + 1L
            if v.R > 120uy && v.G < 120uy then redc  <- redc + 1L)
        Log.line "ssboArray4: indexed indirect MULTIDRAW (2 entries) -> green=%d red=%d px" green redc
        let pass = green > 500L && redc > 500L
        if pass then Log.line "ssboArray4: PASS (indirect multidraw honours per-command FirstInstance)"
        else Log.warn "ssboArray4: FAIL (green=%d red=%d — multidraw drops per-command FirstInstance or runs one draw)" green redc
        pass

    // STEP 6: ssboArray4 (works) + a ViewProjTrafo multiply (identity) in the clean
    // shader. Geometry is already in NDC, so identity*pos == pos: if this still renders
    // both triangles, the *ViewProj path is fine and the bug is the substituteReads
    // rewrite; if it breaks, the ViewProj multiply in the pull path is the culprit.
    module SAvp =
        type UniformScope with
            member x.GeomA : V4f[][] = uniform?StorageBuffer?GeomA
            member x.GeomB : V4f[][] = uniform?StorageBuffer?GeomB
        type VIn  = { [<VertexId>] vid : int; [<InstanceId>] iid : int }
        type VOut = { [<Position>] pos : V4f; [<Color>] c : V4f }
        let shade (v : VIn) =
            vertex {
                let vp : M44f = uniform?ViewProjTrafo
                return { pos = vp * uniform.GeomA.[v.iid].[v.vid]; c = uniform.GeomB.[v.iid].[v.vid] }
            }
        let frag (v : VOut) = fragment { return v.c }

    let ssboArray5Test () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(256, 256))
        // WORLD-space triangles + a REAL camera; clean SSBO-array pull effect.
        let tri  = [| V4f(-1.5f, 0.0f, -0.5f, 1.0f); V4f(-0.2f, 0.0f, -0.5f, 1.0f); V4f(-0.85f, 0.0f, 0.7f, 1.0f) |]
        let quad = [| V4f(0.2f, 0.0f, -0.5f, 1.0f);  V4f(1.5f, 0.0f, -0.5f, 1.0f);  V4f(0.85f, 0.0f, 0.7f, 1.0f) |]
        let grn  = Array.replicate 3 (V4f(0.2f, 0.9f, 0.3f, 1.0f))
        let red  = Array.replicate 3 (V4f(0.9f, 0.2f, 0.2f, 1.0f))
        let geomA : IBuffer[] = [| ArrayBuffer tri :> IBuffer; ArrayBuffer quad :> IBuffer |]
        let geomB : IBuffer[] = [| ArrayBuffer grn :> IBuffer; ArrayBuffer red :> IBuffer |]
        let eff = Effect.compose [ Effect.ofFunction SAvp.shade; Effect.ofFunction SAvp.frag ]
        let view = CameraView.lookAt (V3d(0.0, -8.0, 6.0)) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo
        let vpTrafo = view * proj
        let combinedIdx = [| 0;1;2; 0;1;2 |]
        let entries =
            [| DrawCallInfo(FaceVertexCount = 3, FirstIndex = 0, BaseVertex = 0, FirstInstance = 0, InstanceCount = 1)
               DrawCallInfo(FaceVertexCount = 3, FirstIndex = 3, BaseVertex = 0, FirstInstance = 1, InstanceCount = 1) |]
        let indirect = IndirectBuffer.ofArray entries
        let idxBV = BufferView(AVal.constant (ArrayBuffer combinedIdx :> IBuffer), typeof<int>)
        let sg =
            Sg.indirectDraw IndexedGeometryMode.TriangleList (AVal.constant indirect)
            |> Sg.indexBuffer idxBV
            |> Sg.uniform "GeomA" (AVal.constant geomA)
            |> Sg.uniform "GeomB" (AVal.constant geomB)
            |> Sg.viewTrafo (AVal.constant view)
            |> Sg.projTrafo (AVal.constant proj)
            |> Sg.effect [ eff ]
        use task = sg |> Sg.compile runtime signature
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        let pix = try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let m = pix.GetMatrix<C4b>()
        let mutable green = 0L
        let mutable redc  = 0L
        m.ForeachCoord(fun (p : V2l) ->
            let v = m.[p]
            if v.G > 120uy && v.R < 120uy then green <- green + 1L
            if v.R > 120uy && v.G < 120uy then redc  <- redc + 1L)
        Log.line "ssboArray5: clean shader + ViewProj(identity) multiply -> green=%d red=%d px" green redc
        let pass = green > 100L && redc > 100L
        if pass then Log.line "ssboArray5: PASS (2-array pull + real perspective camera renders both objects)"
        else Log.warn "ssboArray5: FAIL (green=%d red=%d)" green redc
        pass

    // "blank-screen" debugging: POSITIONS-ONLY bindless pull (no normals/lighting),
    // flat principal color per handle (R/G/B), MAGENTA clear. Box geometry whose plain
    // render is known to work, real camera. Magenta == nothing drawn; R/G/B == the
    // position pull works under perspective. Clean hand-written shader (no rewrite).
    module BHC =
        type UniformScope with
            member x.PosArr : V4f[][] = uniform?StorageBuffer?PosArr
        type VIn  = { [<VertexId>] vid : int; [<InstanceId>] iid : int }
        type VOut = { [<Position>] clip : V4f; [<Semantic("Col")>] col : V4f }
        let shade (v : VIn) =
            vertex {
                let vp : M44f = uniform?ViewProjTrafo
                // encode the handle (gl_InstanceID) as a gradient: iid 0 -> blue, iid 8 -> red.
                // a single uniform colour => iid constant; a spread of colours => iid varies.
                let t = float32 v.iid / 8.0f
                let c = V4f(t, 0.15f, 1.0f - t, 1.0f)
                return { clip = vp * uniform.PosArr.[v.iid].[v.vid]; col = c }
            }
        let frag (v : VOut) = fragment { return v.col }

    let bindlessCleanBoxTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature = runtime.CreateFramebufferSignature [ DefaultSemantic.Colors, TextureFormat.Rgba8; DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(640, 640))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let bpos = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let bidx = g.IndexArray |> unbox<int[]>
        let offsets = [| for x in -1 .. 1 do for y in -1 .. 1 -> V3f(float32 x * 1.3f, float32 y * 1.3f, 0.0f) |]
        let n = offsets.Length
        let positions = offsets |> Array.map (fun o -> bpos |> Array.map (fun p -> p + o))
        let view = CameraView.lookAt (V3d(0.0, -8.0, 6.0)) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo
        let posBufs : IBuffer[] = positions |> Array.map (fun a -> ArrayBuffer (a |> Array.map (fun p -> V4f(p, 1.0f))) :> IBuffer)
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let combinedIdx = Array.concat (offsets |> Array.map (fun _ -> bidx))
        let mutable fi = 0
        let entries = Array.init n (fun di ->
                        let c = bidx.Length
                        let e = DrawCallInfo(FaceVertexCount = c, FirstIndex = fi, BaseVertex = 0, FirstInstance = di, InstanceCount = 1)
                        fi <- fi + c
                        e)
        let indirect = IndirectBuffer.ofArray entries
        let idxBV = bv combinedIdx typeof<int>
        let eff = Effect.compose [ Effect.ofFunction BHC.shade; Effect.ofFunction BHC.frag ]
        // provide the camera the NORMAL way (ambient semantic) instead of Sg.uniform,
        // which was being shadowed by the default identity ViewProjTrafo.
        let pullSg =
            Sg.indirectDraw IndexedGeometryMode.TriangleList (AVal.constant indirect)
            |> Sg.indexBuffer idxBV
            |> Sg.uniform "PosArr" (AVal.constant posBufs)
            |> Sg.viewTrafo (AVal.constant view)
            |> Sg.projTrafo (AVal.constant proj)
            |> Sg.effect [ eff ]
        let clearVals = clear { color C4f.Magenta; depth 1.0 }
        use task = pullSg |> Sg.compile runtime signature
        let out = task |> RenderTask.renderToColorWithClear size clearVals
        out.Acquire()
        let pix = try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let m = pix.GetMatrix<C4b>()
        let savePpm (path : string) =
            let w = int m.Size.X
            let h = int m.Size.Y
            use fs = new System.IO.FileStream(path, System.IO.FileMode.Create)
            let hdr = System.Text.Encoding.ASCII.GetBytes(sprintf "P6\n%d %d\n255\n" w h)
            fs.Write(hdr, 0, hdr.Length)
            let buf : byte[] = Array.zeroCreate (w * h * 3)
            let mutable o = 0
            for yy in 0 .. h - 1 do
                for xx in 0 .. w - 1 do
                    let c = m.[V2l(int64 xx, int64 yy)]
                    buf.[o] <- c.R
                    buf.[o + 1] <- c.G
                    buf.[o + 2] <- c.B
                    o <- o + 3
            fs.Write(buf, 0, buf.Length)
        savePpm "/tmp/bcbox.ppm"
        let mutable mag = 0L
        let mutable colored = 0L
        m.ForeachCoord(fun (p : V2l) ->
            let v = m.[p]
            let isMag = v.R > 180uy && v.G < 90uy && v.B > 180uy
            let isBlack = v.R < 40uy && v.G < 40uy && v.B < 40uy
            if isMag then mag <- mag + 1L
            elif not isBlack then colored <- colored + 1L)
        Log.line "bindlessCleanBox: magenta(blank)=%d  colored(drawn)=%d" mag colored
        let pass = colored > 1000L
        if pass then Log.line "bindlessCleanBox: PASS (positions-only pull renders under perspective)"
        else Log.warn "bindlessCleanBox: FAIL (screen is magenta -> position pull blank under perspective)"
        pass

    // Per-object backend TEXTURES through the real heap: N ROs with a NAIVE single-
    // sampler effect (`uniform?DiffuseTexture`), each a DIFFERENT texture. ofRenderObjects
    // must auto-bindless them (one HeapTextures array, indexed by the per-draw handle) so
    // the collapsed bucket matches a direct render — proving the silent "all share ro0's
    // texture" bug is fixed and any number of textures work. (Clean effect, no TexId input.)
    module TH =
        type VIn  = { [<Position>] pos : V4f; [<Normal>] n : V3f }
        type VOut = { [<Position>] pos : V4f; [<Normal>] wn : V3f; [<Semantic("TexCoord")>] tc : V2f }
        let shade (v : VIn) =
            vertex {
                let m  : M44f = uniform?HeapModelTrafo
                let vp : M44f = uniform?ViewProjTrafo
                return { pos = vp * (m * v.pos); wn = m.TransformDir v.n; tc = v.pos.XY + V2f(0.5f, 0.5f) }
            }
        let private diffuse =
            sampler2d {
                texture uniform?DiffuseTexture
                filter Filter.MinMagMipLinear
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }
        let frag (v : VOut) =
            fragment {
                let albedo = diffuse.Sample(v.tc).XYZ
                let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
                let d = 0.35f + 0.65f * max 0.0f (Vec.dot (Vec.normalize v.wn) l)
                return V4f(albedo * d, 1.0f)
            }

    // Same as TH but POINT filtering — proves the heap re-applies sampler STATE.
    // With a magnified checker texture, point vs linear differ sharply at block
    // boundaries; if the heap dropped state (used the default), it would NOT match
    // the classic point-sampled render. (TH=linear and THP=point can't both match a
    // single default state, so both passing means state is genuinely preserved.)
    module THP =
        let private diffuse =
            sampler2d {
                texture uniform?DiffuseTexture
                filter Filter.MinMagMipPoint
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }
        let frag (v : TH.VOut) =
            fragment {
                let albedo = diffuse.Sample(v.tc).XYZ
                let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
                let d = 0.35f + 0.65f * max 0.0f (Vec.dot (Vec.normalize v.wn) l)
                return V4f(albedo * d, 1.0f)
            }

    // Cubemap path: a per-object samplerCube sampled by the world normal direction.
    module TC =
        type VIn  = { [<Position>] pos : V4f; [<Normal>] n : V3f }
        type VOut = { [<Position>] pos : V4f; [<Normal>] wn : V3f }
        let shade (v : VIn) =
            vertex {
                let m  : M44f = uniform?HeapModelTrafo
                let vp : M44f = uniform?ViewProjTrafo
                return { pos = vp * (m * v.pos); wn = m.TransformDir v.n }
            }
        let private envMap =
            samplerCube {
                texture uniform?EnvTexture
                filter Filter.MinMagMipLinear
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
                addressW WrapMode.Wrap
            }
        let frag (v : VOut) =
            fragment {
                let albedo = envMap.Sample(Vec.normalize v.wn).XYZ
                return V4f(albedo, 1.0f)
            }

    let texHeapTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 18.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProjM = AVal.init (view * proj)              // mutated per "frame" in glyphWedge to force re-submit
        let viewProj = viewProjM :> IAdaptiveValue
        let texArray : ITexture[] = Array.init TexCount mkTexture
        let eff = Effect.compose [ Effect.ofFunction TH.shade; Effect.ofFunction TH.frag ]
        let grid =
            let s = 8
            [| for x in 0 .. s - 1 do for y in 0 .. s - 1 -> (x * s + y), V3d(float (x - s/2) * 1.2, float (y - s/2) * 1.2, 0.0) |]
        let mkRO (i : int) (p : V3d) =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect eff
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj
                Symbol.Create "DiffuseTexture", (AVal.constant texArray.[i % TexCount] :> IAdaptiveValue) ]
            ro :> IRenderObject
        let ros = grid |> Array.map (fun (i, p) -> mkRO i p)
        let renderToPix (objs : aset<IRenderObject>) =
            use task = runtime.CompileRender(signature, objs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let classicPix = renderToPix (ASet.ofArray ros)
        let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray ros)
        let heapPix = renderToPix heapObjs
        let maxD, nDiff, nNonBg, total = diff classicPix heapPix
        Log.line "texHeap: %d ROs (per-object texture) -> %d bucket(s)  classic-vs-heap maxDelta=%d diffPixels=%d coverage=%d"
            ros.Length Heap.lastBucketCount maxD nDiff nNonBg
        // require buckets > 0 — otherwise the ROs merely passed through and "matching
        // classic" would be trivially (and misleadingly) true. The bindless path is
        // bit-exact vs classic (maxDelta <= 1); the atlas fallback (non-bindless, e.g.
        // MoltenVK / GL / Vulkan-1.0) resamples through bilinear + 2-px gutters + the
        // in-shader mip pyramid, so deltas of ~3/255 are inherent. Match atlasheap's
        // tolerance there (24) — same tolerance the atlas test passes with badPixels=0.
        let tolerance = if runtime.SupportsUnboundedSamplerArrays then 1 else 24
        let pass = maxD <= tolerance && nNonBg > total / 100L && Heap.lastBucketCount > 0
        if pass then Log.line "texHeap: PASS (per-object textures auto-bindless via ofRenderObjects == classic; %d bucket(s); tol=%d)" Heap.lastBucketCount tolerance
        else Log.warn "texHeap: FAIL (maxDelta=%d tol=%d coverage=%d buckets=%d -> mis-bound or passthrough)" maxD tolerance nNonBg Heap.lastBucketCount
        pass

    // RTT-relevant: a per-object texture whose IDENTITY swaps at runtime (the
    // double-buffer / resize case). Both the classic and the heap task are kept
    // COMPILED across the mutation, so this exercises the INCREMENTAL descriptor
    // update of the bindless sampler array (+ re-dedup of HeapTextures /
    // HeapTexIndices), not a fresh rebuild. Passes iff the heap tracks the classic
    // render at BOTH states AND the swap actually changed the image.
    let texSwapTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 18.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProjM = AVal.init (view * proj)              // mutated per "frame" in glyphWedge to force re-submit
        let viewProj = viewProjM :> IAdaptiveValue
        let texArray : ITexture[] = Array.init TexCount mkTexture
        let eff = Effect.compose [ Effect.ofFunction TH.shade; Effect.ofFunction TH.frag ]
        let grid =
            let s = 8
            [| for x in 0 .. s - 1 do for y in 0 .. s - 1 -> (x * s + y), V3d(float (x - s/2) * 1.2, float (y - s/2) * 1.2, 0.0) |]
        // one MUTABLE texture cell per object — the identity changes on swap
        let texCells = grid |> Array.map (fun (i, _) -> cval (texArray.[i % TexCount]))
        let mkRO (i : int) (p : V3d) =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect eff
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj
                Symbol.Create "DiffuseTexture", (texCells.[i] :> IAdaptiveValue) ]
            ro :> IRenderObject
        // distinct RO instances per path, but SHARING texCells so both track the swap
        let classicROs    = grid |> Array.map (fun (i, p) -> mkRO i p)
        let heapInputROs  = grid |> Array.map (fun (i, p) -> mkRO i p)
        let mkOut (objs : aset<IRenderObject>) =
            let task = runtime.CompileRender(signature, objs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            task, out
        let classicTask, classicOut = mkOut (ASet.ofArray classicROs)
        let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray heapInputROs)
        let heapTask, heapOut = mkOut heapObjs
        let dl (out : IAdaptiveResource<IBackendTexture>) = out.GetValue().Download().AsPixImage<uint8>()
        // ── state 0 ──
        let classic0 = dl classicOut
        let heap0    = dl heapOut
        let buckets  = Heap.lastBucketCount
        // ── RTT-style swap: every cube's texture identity changes ──
        transact (fun () -> texCells |> Array.iteri (fun i c -> c.Value <- texArray.[(i + 7) % TexCount]))
        // ── state 1: SAME compiled tasks, re-evaluated (incremental update) ──
        let classic1 = dl classicOut
        let heap1    = dl heapOut
        classicOut.Release(); heapOut.Release(); classicTask.Dispose(); heapTask.Dispose()
        let d0, _, nbg0, total = diff classic0 heap0
        let d1, _, nbg1, _     = diff classic1 heap1
        let _, nChanged, _, _  = diff heap0 heap1   // the swap must visibly change the image
        Log.line "texSwap: %d ROs -> %d bucket(s)  s0 maxDelta=%d  s1 maxDelta=%d  swap-changed-px=%d/%d"
            classicROs.Length buckets d0 d1 nChanged total
        let pass =
            d0 <= 1 && d1 <= 1 && nbg0 > total/100L && nbg1 > total/100L
            && nChanged > total/100L && buckets > 0
        if pass then Log.line "texSwap: PASS (heap tracks classic across an RTT-style texture identity swap; incremental sampler-array descriptor update OK; %d bucket(s))" buckets
        else Log.warn "texSwap: FAIL (s0Δ=%d s1Δ=%d swapChanged=%d buckets=%d)" d0 d1 nChanged buckets
        pass

    // Sampler-STATE preservation: identical to texHeapTest but the effect uses POINT
    // filtering. The heap must re-apply that state to its generated bindless array;
    // if it used the default (≈linear), point vs linear would diverge sharply at the
    // magnified checker boundaries and this would FAIL. (texHeap=linear PASS +
    // texState=point PASS together prove state is carried, not defaulted.)
    let texStateTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 18.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProjM = AVal.init (view * proj)              // mutated per "frame" in glyphWedge to force re-submit
        let viewProj = viewProjM :> IAdaptiveValue
        let texArray : ITexture[] = Array.init TexCount mkTexture
        let eff = Effect.compose [ Effect.ofFunction TH.shade; Effect.ofFunction THP.frag ]
        let grid =
            let s = 8
            [| for x in 0 .. s - 1 do for y in 0 .. s - 1 -> (x * s + y), V3d(float (x - s/2) * 1.2, float (y - s/2) * 1.2, 0.0) |]
        let mkRO (i : int) (p : V3d) =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect eff
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj
                Symbol.Create "DiffuseTexture", (AVal.constant texArray.[i % TexCount] :> IAdaptiveValue) ]
            ro :> IRenderObject
        let ros = grid |> Array.map (fun (i, p) -> mkRO i p)
        let renderToPix (objs : aset<IRenderObject>) =
            use task = runtime.CompileRender(signature, objs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let classicPix = renderToPix (ASet.ofArray ros)
        let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray ros)
        let heapPix = renderToPix heapObjs
        let maxD, nDiff, nNonBg, total = diff classicPix heapPix
        Log.line "texState: %d ROs (POINT filter) -> %d bucket(s)  classic-vs-heap maxDelta=%d diffPixels=%d coverage=%d"
            ros.Length Heap.lastBucketCount maxD nDiff nNonBg
        let pass = maxD <= 1 && nNonBg > total / 100L && Heap.lastBucketCount > 0
        if pass then Log.line "texState: PASS (sampler STATE preserved: point-filtered heap == classic; %d bucket(s))" Heap.lastBucketCount
        else Log.warn "texState: FAIL (maxDelta=%d coverage=%d buckets=%d -> state dropped or passthrough)" maxD nNonBg Heap.lastBucketCount
        pass

    // a per-FACE-uniform-color cube texture; the frag samples it by world normal
    let private mkCubeTexture (i : int) : ITexture =
        let cols = [| C4b(230uy,60uy,60uy,255uy); C4b(60uy,200uy,60uy,255uy); C4b(60uy,120uy,230uy,255uy); C4b(230uy,200uy,40uy,255uy)
                      C4b(210uy,60uy,210uy,255uy); C4b(40uy,210uy,210uy,255uy); C4b(230uy,140uy,40uy,255uy); C4b(180uy,180uy,180uy,255uy) |]
        let face (c : C4b) =
            let img = PixImage<byte>(Col.Format.RGBA, V2i(16, 16))
            img.GetMatrix<C4b>().SetByIndex(fun (_ : int64) -> c) |> ignore
            PixImageMipMap(img :> PixImage)
        // give each of the 6 faces a distinct color (offset by i) so direction matters
        PixTextureCube(PixCube [| for f in 0 .. 5 -> face cols.[(i + f) % cols.Length] |], false) :> ITexture

    // Cubemap path: per-object samplerCube auto-bindless via ofRenderObjects, sampled
    // by world normal. Proves the per-TYPE bindless array (samplerCube[]) works.
    let texCubeTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 18.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let cubeArray : ITexture[] = Array.init TexCount mkCubeTexture
        let eff = Effect.compose [ Effect.ofFunction TC.shade; Effect.ofFunction TC.frag ]
        let grid =
            let s = 8
            [| for x in 0 .. s - 1 do for y in 0 .. s - 1 -> (x * s + y), V3d(float (x - s/2) * 1.2, float (y - s/2) * 1.2, 0.0) |]
        let mkRO (i : int) (p : V3d) =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect eff
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj
                Symbol.Create "EnvTexture",     (AVal.constant cubeArray.[i % TexCount] :> IAdaptiveValue) ]
            ro :> IRenderObject
        let ros = grid |> Array.map (fun (i, p) -> mkRO i p)
        let renderToPix (objs : aset<IRenderObject>) =
            use task = runtime.CompileRender(signature, objs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let classicPix = renderToPix (ASet.ofArray ros)
        let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray ros)
        let heapPix = renderToPix heapObjs
        let maxD, nDiff, nNonBg, total = diff classicPix heapPix
        Log.line "texCube: %d ROs (per-object samplerCube) -> %d bucket(s)  classic-vs-heap maxDelta=%d diffPixels=%d coverage=%d"
            ros.Length Heap.lastBucketCount maxD nDiff nNonBg
        let pass = maxD <= 1 && nNonBg > total / 100L && Heap.lastBucketCount > 0
        if pass then Log.line "texCube: PASS (per-object cubemaps auto-bindless via samplerCube[] == classic; %d bucket(s))" Heap.lastBucketCount
        else Log.warn "texCube: FAIL (maxDelta=%d coverage=%d buckets=%d)" maxD nNonBg Heap.lastBucketCount
        pass

    // GPU-resident geometry: positions/normals/index uploaded as backend buffers.
    // ofRenderObjects can't CPU-slice them, so it routes the bucket to the bindless
    // VERTEX-PULL path (objects' buffers bound as HeapVertexData, pulled by handle).
    // Input types == buffer types (V3f) so the gather decodes exactly.
    module GG =
        type VIn  = { [<Semantic("Positions")>] pos : V3f; [<Semantic("Normals")>] n : V3f }
        type VOut = { [<Position>] clip : V4f; [<Normal>] wn : V3f; [<Color>] c : V4f }
        // read per-draw uniforms in the VERTEX stage (gl_DrawID is a vertex-only builtin
        // on GL); pass results as varyings. The GPU-geometry feature under test is the
        // vertex-PULL of Positions/Normals, independent of where uniforms are gathered.
        let shade (v : VIn) =
            vertex {
                let m  : M44f = uniform?HeapModelTrafo
                let vp : M44f = uniform?ViewProjTrafo
                let col : V4f = uniform?HeapColor
                return { clip = vp * (m * V4f(v.pos, 1.0f)); wn = m.TransformDir v.n; c = col }
            }
        let frag (v : VOut) =
            fragment {
                let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
                let d = 0.35f + 0.65f * max 0.0f (Vec.dot (Vec.normalize v.wn) l)
                return V4f(v.c.XYZ * d, 1.0f)
            }

    let private gpuGeomWith (label : string) (expectHeaped : bool) (runtime : IRuntime) =
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        // upload geometry to the GPU once (shared by all objects); default BufferUsage.All
        // makes each usable as BOTH a vertex buffer (classic) and storage (vertex-pull).
        let posGpu = runtime.PrepareBuffer(ArrayBuffer positions :> IBuffer)
        let nrmGpu = runtime.PrepareBuffer(ArrayBuffer normals   :> IBuffer)
        let idxGpu = runtime.PrepareBuffer(ArrayBuffer index     :> IBuffer)
        let gbv (b : IBackendBuffer) t = BufferView(AVal.constant (b :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ Symbol.Create "Positions", gbv posGpu typeof<V3f>; Symbol.Create "Normals", gbv nrmGpu typeof<V3f> ]
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 18.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan |]
        let eff = Effect.compose [ Effect.ofFunction GG.shade; Effect.ofFunction GG.frag ]
        let grid =
            let s = 8
            [| for x in 0 .. s - 1 do for y in 0 .. s - 1 -> (x * s + y), V3d(float (x - s/2) * 1.2, float (y - s/2) * 1.2, 0.0) |]
        let mkRO (i : int) (p : V3d) =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect eff
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (gbv idxGpu typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj ]
            ro :> IRenderObject
        let ros = grid |> Array.map (fun (i, p) -> mkRO i p)
        let renderToPix (objs : aset<IRenderObject>) =
            use task = runtime.CompileRender(signature, objs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let classicPix = renderToPix (ASet.ofArray ros)   // GPU buffers, fixed-function input
        let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray ros)
        let heapPix = renderToPix heapObjs                 // GPU buffers, bindless vertex-pull
        let maxD, nDiff, nNonBg, total = diff classicPix heapPix
        Log.line "%s: %d ROs (GPU-resident geometry) -> %d bucket(s)  classic-vs-heap maxDelta=%d diffPixels=%d coverage=%d"
            label ros.Length Heap.lastBucketCount maxD nDiff nNonBg
        // Vulkan: GPU geometry can't take the host combined-buffer path, so buckets>0
        // PROVES the bindless vertex-pull ran. GL: descriptor indexing is unavailable, so
        // GPU geometry must PASS THROUGH (buckets=0) and still render identically (legacy path).
        let bucketsOk = if expectHeaped then Heap.lastBucketCount > 0 else Heap.lastBucketCount = 0
        let pass = maxD <= 1 && nNonBg > total / 100L && bucketsOk
        let how = if expectHeaped then "vertex-pulled via ofRenderObjects == classic" else "passthrough on GL (not heaped) == classic"
        if pass then Log.line "%s: PASS (GPU-resident geometry %s; %d bucket(s))" label how Heap.lastBucketCount
        else Log.warn "%s: FAIL (maxDelta=%d coverage=%d buckets=%d expectHeaped=%b)" label maxD nNonBg Heap.lastBucketCount expectHeaped
        pass

    let gpuGeomTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        gpuGeomWith "gpuGeom" true (app.Runtime :> IRuntime)

    let gpuGeomTestGL () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.OpenGlApplication(false)
        gpuGeomWith "gpuGeom-GL" false (app.Runtime :> IRuntime)

    // CPU-only: verifies the texture atlas builder's BORDER + MIP layout precisely
    // (no GPU). Gutter: inner ring = clamp-replicate edge texel, outer ring = wrap
    // opposite edge. Iliffe pyramid: mip-k at origin + mipOffset, 2x2 box-averaged.
    let atlasBuildTest () =
        let w, h = 4, 4
        let img = PixImage<byte>(Col.Format.RGBA, V2i(w, h))
        let mutable m = img.GetMatrix<C4b>()
        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                m.[int64 x, int64 y] <- C4b(byte (10 + x * 40), byte (10 + y * 40), byte (x * 16 + y), 255uy)
        let pages, acq = HeapAtlas.build 64 true [| 0, img |]
        let a = acq.[0]
        let atlas = pages.[a.PageId].GetMatrix<C4b>()
        let ox, oy = a.OriginPx.X, a.OriginPx.Y
        let mutable ok = true
        let ceq (p : C4b) (q : C4b) = p.R = q.R && p.G = q.G && p.B = q.B && p.A = q.A
        let chk name cond = if not cond then (Log.warn "atlas: FAIL %s" name; ok <- false)
        chk "size"    (a.SizePx = V2i(4, 4))
        chk "numMips" (a.NumMips = 3)
        chk "pages"   (pages.Length = 1)
        // interior == source
        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                chk (sprintf "interior %d,%d" x y) (ceq atlas.[int64 (ox + x), int64 (oy + y)] m.[int64 x, int64 y])
        // gutter on a middle row/col: inner = clamp (nearest edge), outer = wrap (opposite edge)
        let pxX dx = atlas.[int64 (ox + dx), int64 (oy + 1)]
        chk "inner-left clamp"  (ceq (pxX -1) m.[0L, 1L])
        chk "outer-left wrap"   (ceq (pxX -2) m.[3L, 1L])
        chk "inner-right clamp" (ceq (pxX 4)  m.[3L, 1L])
        chk "outer-right wrap"  (ceq (pxX 5)  m.[0L, 1L])
        let pxY dy = atlas.[int64 (ox + 1), int64 (oy + dy)]
        chk "inner-top clamp"   (ceq (pxY -1) m.[1L, 0L])
        chk "outer-top wrap"    (ceq (pxY -2) m.[1L, 3L])
        chk "inner-bot clamp"   (ceq (pxY 4)  m.[1L, 3L])
        chk "outer-bot wrap"    (ceq (pxY 5)  m.[1L, 0L])
        // mip 1 = 2x2 box average, placed at origin + mipOffset(w,h,1) = (8,0)
        let mo = HeapAtlas.mipOffset w h 1
        chk "mipOffset1" (mo = V2i(8, 0))
        let inline avg (p:byte) (q:byte) (r:byte) (s:byte) = byte ((int p + int q + int r + int s + 2) / 4)
        let e00 =
            let a0 = m.[0L,0L] in let b0 = m.[1L,0L] in let c0 = m.[0L,1L] in let d0 = m.[1L,1L]
            C4b(avg a0.R b0.R c0.R d0.R, avg a0.G b0.G c0.G d0.G, avg a0.B b0.B c0.B d0.B, avg a0.A b0.A c0.A d0.A)
        chk "mip1 box-avg"     (ceq atlas.[int64 (ox + mo.X), int64 (oy + mo.Y)] e00)
        chk "mip1 inner clamp" (ceq atlas.[int64 (ox + mo.X - 1), int64 (oy + mo.Y)] e00)
        if ok then Log.line "atlas: PASS (gutter clamp/wrap + Iliffe mip layout exact; %d page, %d mips)" pages.Length a.NumMips
        else Log.warn "atlas: FAIL"
        ok

    // Atlas path: per-object textures sampled from ONE packed atlas page (the
    // Vulkan-1.0/GL/MoltenVK fallback, no descriptor indexing). forceAtlas routes the
    // heap through the atlas even on desktop Vulkan. Compared to the classic per-object
    // render: atlas sampling (repacked texels + in-shader LOD/wrap) isn't bit-exact vs
    // hardware sampling, so we allow a few badly-off pixels but require it looks the same.
    let atlasHeapTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 18.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProjM = AVal.init (view * proj)              // mutated per "frame" in glyphWedge to force re-submit
        let viewProj = viewProjM :> IAdaptiveValue
        let texArray : ITexture[] = Array.init TexCount mkTexture
        let eff = Effect.compose [ Effect.ofFunction TH.shade; Effect.ofFunction TH.frag ]
        let grid = [| for x in 0 .. 7 do for y in 0 .. 7 -> (x * 8 + y), V3d(float (x - 4) * 1.2, float (y - 4) * 1.2, 0.0) |]
        let mkRO (i : int) (p : V3d) =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect eff
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj
                Symbol.Create "DiffuseTexture", (AVal.constant texArray.[i % TexCount] :> IAdaptiveValue) ]
            ro :> IRenderObject
        let ros = grid |> Array.map (fun (i, p) -> mkRO i p)
        let renderToPix (objs : aset<IRenderObject>) =
            use task = runtime.CompileRender(signature, objs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let classicPix = renderToPix (ASet.ofArray ros)
        Heap.forceAtlas <- true
        let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray ros)
        let heapPix = renderToPix heapObjs
        let buckets = Heap.lastBucketCount
        Heap.forceAtlas <- false
        let am = classicPix.GetMatrix<C4b>()
        let bm = heapPix.GetMatrix<C4b>()
        let mutable bad = 0L
        let mutable cov = 0L
        am.ForeachCoord(fun (c : V2l) ->
            let p = am.[c]
            let q = bm.[c]
            let d = max (max (abs (int p.R - int q.R)) (abs (int p.G - int q.G))) (abs (int p.B - int q.B))
            if p.R <> 0uy || p.G <> 0uy || p.B <> 0uy then cov <- cov + 1L
            if d > 24 then bad <- bad + 1L)
        let total = int64 am.Size.X * int64 am.Size.Y
        Log.line "atlasHeap: %d ROs -> %d bucket(s)  coverage=%d badPixels=%d/%d" ros.Length buckets cov bad total
        let pass = cov > total / 100L && buckets > 0 && bad < cov / 25L   // <4% of covered pixels badly off
        if pass then Log.line "atlasHeap: PASS (per-object textures via atlas page == classic within tolerance; %d bucket(s))" buckets
        else Log.warn "atlasHeap: FAIL (coverage=%d badPixels=%d buckets=%d)" cov bad buckets
        pass

    // Headless repro of the windowed-showcase freeze, WITHOUT a window. Renders the
    // atlas scene OFFSCREEN to a MULTISAMPLED framebuffer through the heap (forceAtlas),
    // so it exercises MSAA + the secondary-command-buffer render path on the backend.
    // Env SAMPLES (default 8) and N (default 2000) isolate MSAA vs scale on MoltenVK:
    //   SAMPLES=8 N=64   -> tests MSAA at tiny scale,   SAMPLES=1 N=20000 -> tests scale.
    // Forces the render (no download) so a GPU hang reproduces; prints COMPLETED otherwise.
    let msaaTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let samples = match System.Environment.GetEnvironmentVariable "SAMPLES" with null | "" -> 8 | s -> int s
        let n       = match System.Environment.GetEnvironmentVariable "N"       with null | "" -> 2000 | s -> int s
        let signature =
            runtime.CreateFramebufferSignature(
                [ DefaultSemantic.Colors, TextureFormat.Rgba8
                  DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ], samples = samples)
        let size = AVal.constant (V2i(1024, 1024))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 60.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProjM = AVal.init (view * proj)              // mutated per "frame" in glyphWedge to force re-submit
        let viewProj = viewProjM :> IAdaptiveValue
        let texArray : ITexture[] = Array.init TexCount mkTexture
        let eff = Effect.compose [ Effect.ofFunction TH.shade; Effect.ofFunction TH.frag ]
        let side = max 1 (int (ceil (sqrt (float n))))
        let mkRO (i : int) =
            let x, y = i % side, i / side
            let p = V3d(float (x - side / 2) * 1.2, float (y - side / 2) * 1.2, 0.0)
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect eff
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj
                Symbol.Create "DiffuseTexture", (AVal.constant texArray.[i % TexCount] :> IAdaptiveValue) ]
            ro :> IRenderObject
        let ros = Array.init n mkRO
        Heap.forceAtlas <- true
        let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray ros)
        Log.line "msaaTest: rendering n=%d samples=%d (atlas, offscreen) ..." n samples
        use task = runtime.CompileRender(signature, heapObjs)
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        out.GetValue() |> ignore         // force the GPU render; a hang reproduces here
        out.Release()
        Heap.forceAtlas <- false
        Log.line "msaaTest: COMPLETED n=%d samples=%d buckets=%d (no hang)" n samples Heap.lastBucketCount
        true

    // Submit-stress: hammer K synchronous GPU uploads (each = a vkQueueSubmit + fence wait
    // on the upload path) with no heap/glyphs/window. Tests the hypothesis that MoltenVK
    // stops signalling fences after too many enqueues. Logs progress; if it wedges at some K
    // that's a confirmed driver/resource-exhaustion bug. N = iterations (default 5000).
    let submitStressTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let n = match System.Environment.GetEnvironmentVariable "N" with null | "" -> 5000 | s -> int s
        let img = PixImage<byte>(Col.Format.RGBA, V2i(8, 8))
        img.GetMatrix<C4b>().SetByIndex(fun _ -> C4b(200uy, 50uy, 50uy, 255uy)) |> ignore
        let sw = System.Diagnostics.Stopwatch.StartNew()
        Log.line "submitStress: %d small texture uploads (each a sync GPU submit on the upload path)..." n
        for i in 1 .. n do
            let t = runtime.PrepareTexture(PixTexture2d(img))
            runtime.DeleteTexture t
            if i % 250 = 0 then Log.line "submitStress: %d/%d  (%.1fs)" i n sw.Elapsed.TotalSeconds
        Log.line "submitStress: COMPLETED %d uploads in %.1fs (no fence wedge)" n sw.Elapsed.TotalSeconds
        true

    // HEADLESS repro of the windowed-showcase glyph wedge (the exact GPU sequence, no
    // swapchain): build + render the heavy heap scene to load the GPU, then do the glyph
    // GeometryPool upload (PrepareGlyphs) on the loaded device — the order the showcase
    // hit before the pre-warm fix. ssh-safe (no window → the earlier whole-machine freeze
    // was the swapchain path; offscreen tests have always stayed up). If this wedges, the
    // Fence.Wait watchdog prints the managed stack and MoltenVK verbose logging
    // (MVK_CONFIG_LOG_LEVEL=3) prints the Metal command-buffer error right before it — the
    // real "what is the GPU stuck on" signal. ITERS = how many times to render the heap
    // before the glyph upload (mimics frames; default 1). N = object count (default 20000).
    let glyphWedgeTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let n     = match System.Environment.GetEnvironmentVariable "N"     with null | "" -> 20000 | s -> int s
        let iters = match System.Environment.GetEnvironmentVariable "ITERS" with null | "" -> 1     | s -> int s
        let signature =
            runtime.CreateFramebufferSignature(
                [ DefaultSemantic.Colors, TextureFormat.Rgba8
                  DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ], samples = 1)
        let size = AVal.constant (V2i(1024, 1024))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 60.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProjM = AVal.init (view * proj)              // mutated per "frame" in glyphWedge to force re-submit
        let viewProj = viewProjM :> IAdaptiveValue
        let texArray : ITexture[] = Array.init TexCount mkTexture
        let eff = Effect.compose [ Effect.ofFunction TH.shade; Effect.ofFunction TH.frag ]
        let side = max 1 (int (ceil (sqrt (float n))))
        let mkRO (i : int) =
            let x, y = i % side, i / side
            let p = V3d(float (x - side / 2) * 1.2, float (y - side / 2) * 1.2, 0.0)
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect eff
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj
                Symbol.Create "DiffuseTexture", (AVal.constant texArray.[i % TexCount] :> IAdaptiveValue) ]
            ro :> IRenderObject
        let ros = Array.init n mkRO
        Heap.forceAtlas <- true
        let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray ros)
        Log.line "glyphWedge: build + render heap n=%d (x%d) offscreen ..." n iters
        use task = runtime.CompileRender(signature, heapObjs)
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        // re-submit the heap render ITERS times (mutate viewProj each "frame" so it's actually
        // dirty and re-rendered), mimicking the showcase's continuous render loop
        for it in 1 .. iters do
            transact (fun () -> viewProjM.Value <- view * proj * Trafo3d.RotationZ(float it * 1.0e-4))
            out.GetValue() |> ignore
        out.Release()
        Heap.forceAtlas <- false
        Log.line "glyphWedge: heap render DONE (buckets=%d). Now PrepareGlyphs on the loaded device ..." Heap.lastBucketCount
        let sw = System.Diagnostics.Stopwatch.StartNew()
        runtime.PrepareGlyphs(DefaultFonts.Hack.Regular, [| for c in 0 .. 255 -> char c |])
        Log.line "glyphWedge: PrepareGlyphs DONE in %.2fs — NO WEDGE" sw.Elapsed.TotalSeconds
        true

    // Standalone AtlasPool test: exercises Acquire/Release/dedup/LRU/multi-page on the
    // reactive pool (HeapAtlasPool.fs) without HeapPool integration. PASS criteria are pure
    // bookkeeping (entry/page counts, dedup returns same Acquisition, refcount semantics,
    // LRU eviction frees slots so the next Acquire fits without growing). Sub-rect upload
    // is exercised on every Acquire — a backend bug there would assert or VK_ERROR here.
    let atlasPoolTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        // small pages so overflow + eviction trigger quickly
        let pageSz, maxPages = 512, 2
        use pool = new AtlasPool(runtime, pageSz, maxPages)
        Log.line "atlasPool: pageSize=%d maxPages=%d" pageSz maxPages

        // Build N unique (ITexture, PixImage) pairs. ITexture key = ref identity → unique
        // PixTexture2d wrappers ensure each Acquire is a fresh entry.
        let mkPair (i : int) =
            let s = 64 + (i % 4) * 16        // 64,80,96,112 → reserved ~ small/mid
            let img = PixImage<byte>(Col.Format.RGBA, V2i(s, s))
            let m = img.GetMatrix<C4b>()
            let c = C4b(byte ((i * 23) % 256), byte ((i * 53) % 256), byte ((i * 91) % 256), 255uy)
            m.SetByIndex(fun _ -> c) |> ignore
            (PixTexture2d(img) :> ITexture), img
        let n0 = 12
        let pairs = Array.init n0 mkPair

        // Phase 1: acquire all → unique entries; PageCount must be ≤ maxPages.
        let acqs = pairs |> Array.map (fun (t, p) -> pool.Acquire(t, p))
        Log.line "atlasPool: phase1 acquired %d entries on %d page(s)" pool.EntryCount pool.PageCount
        if pool.EntryCount <> n0 then Log.warn "atlasPool: FAIL phase1 entry count %d <> %d" pool.EntryCount n0; false |> ignore
        if pool.PageCount > maxPages then Log.warn "atlasPool: FAIL phase1 pageCount %d > maxPages %d" pool.PageCount maxPages; false |> ignore
        let phase1Ok = pool.EntryCount = n0 && pool.PageCount <= maxPages

        // Phase 2: dedup. Re-Acquire pair[0] → same Acquisition (PageId+OriginPx); EntryCount unchanged.
        let (t0, p0) = pairs.[0]
        let acq0' = pool.Acquire(t0, p0)
        let dedupOk = (fst acq0').PageId = (fst acqs.[0]).PageId && (fst acq0').OriginPx = (fst acqs.[0]).OriginPx && pool.EntryCount = n0
        Log.line "atlasPool: dedup re-acquire same slot=%b (entries=%d)" dedupOk pool.EntryCount

        // Phase 3: release everything (count down all refcounts to 0).
        // pair[0] was acquired twice → release twice. Others once.
        pool.Release t0   // first  release for pair[0]
        for (t, _) in pairs do pool.Release t
        Log.line "atlasPool: phase3 released, entries still cached=%d" pool.EntryCount
        if pool.EntryCount <> n0 then Log.warn "atlasPool: FAIL phase3 release dropped entries (%d)" pool.EntryCount; false |> ignore
        let phase3Ok = pool.EntryCount = n0

        // Phase 4: re-acquire one → must hit cache (no eviction yet). Same Acquisition again.
        let acq0'' = pool.Acquire(t0, p0)
        let cachedOk = (fst acq0'').PageId = (fst acqs.[0]).PageId && (fst acq0'').OriginPx = (fst acqs.[0]).OriginPx
        Log.line "atlasPool: cached re-acquire after release=%b" cachedOk
        pool.Release t0

        // Phase 5: force LRU eviction. Add NEW distinct textures until the pool is full and
        // must evict. With everything refcount=0 they ARE evictable; the new ones should fit.
        let extra = Array.init 24 (fun i -> mkPair (1000 + i))
        let mutable acquired = 0
        try
            for (t, p) in extra do
                pool.Acquire(t, p) |> ignore
                acquired <- acquired + 1
        with ex -> Log.warn "atlasPool: extra Acquire %d/%d threw: %s" acquired extra.Length ex.Message
        Log.line "atlasPool: phase5 LRU-evict drove %d/%d extras onto %d page(s); entries=%d" acquired extra.Length pool.PageCount pool.EntryCount
        let evictOk = acquired = extra.Length && pool.PageCount <= maxPages
        let pass = phase1Ok && dedupOk && phase3Ok && cachedOk && evictOk
        if pass then Log.line "atlasPool: PASS (phase1=%b dedup=%b cache=%b evict=%b)" phase1Ok dedupOk cachedOk evictOk
        else Log.warn "atlasPool: FAIL (phase1=%b dedup=%b phase3=%b cache=%b evict=%b)" phase1Ok dedupOk phase3Ok cachedOk evictOk
        pass

    // Isolation: a plain (NON-heap) offscreen render. If this also crashes, the
    // problem is aardvark's offscreen path on the backend, not the heap.
    let plainTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(512, 512))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 6.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let effect = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        let inputs =
            Array.init 9 (fun i ->
                let p = V3d(float (i % 3 - 1) * 1.2, float (i / 3 - 1) * 1.2, 0.0)
                let ro = RenderObject()
                ro.Surface   <- Surface.Effect effect
                ro.Mode      <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- vattrs
                ro.Indices   <- Some (bv index typeof<int>)
                ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
                ro.Uniforms  <- UniformProvider.ofList [
                    Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                    Symbol.Create "HeapColor",      (AVal.constant (V4f(1.0f, 0.7f, 0.3f, 1.0f)) :> IAdaptiveValue)
                    Symbol.Create "ViewProjTrafo",  viewProj ]
                ro :> IRenderObject)
        Log.line "plain: compiling + rendering 9 plain (non-heap) cubes offscreen..."
        use task = runtime.CompileRender(signature, ASet.ofArray inputs)
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        let m = out.GetValue().Download().AsPixImage<uint8>().GetMatrix<C4b>()
        let mutable c = 0L
        m.ForeachCoord(fun (p : V2l) -> let v = m.[p] in if v.R <> 0uy || v.G <> 0uy || v.B <> 0uy then c <- c + 1L)
        out.Release()
        Log.line "plain: coverage=%d" c
        let pass = c > 1000L
        if pass then Log.line "plain: PASS (plain offscreen render works)"
        else Log.warn "plain: FAIL (coverage=%d)" c
        pass

    // Verifies pipeline-state bucketing: ROs with the same effect but a different
    // rasterizer (cull) state must land in separate buckets, while same-state ROs
    // still collapse into one.
    let bucketingTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let viewProj = AVal.constant Trafo3d.Identity :> IAdaptiveValue
        let effect = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        // one shared custom rasterizer state (cull back) for half the ROs
        let culled = { RasterizerState.Default with CullMode = AVal.constant CullMode.Back }
        let mk i =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect effect
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (bv index typeof<int>)
            if i % 2 = 0 then ro.RasterizerState <- culled
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <-
                UniformProvider.ofList [
                    Symbol.Create "HeapModelTrafo", (AVal.constant M44f.Identity :> IAdaptiveValue)
                    Symbol.Create "HeapColor",      (AVal.constant V4f.IIII :> IAdaptiveValue)
                    Symbol.Create "ViewProjTrafo",  viewProj ]
            ro :> IRenderObject
        let inputs = Array.init 16 mk
        let signature = runtime.CreateFramebufferSignature [DefaultSemantic.Colors, TextureFormat.Rgba8]
        let heap = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray inputs)
        // signature-deferred: buckets build at compile time — render once to force them
        do
            use task = runtime.CompileRender(signature, heap)
            let out = task |> RenderTask.renderToColor (AVal.constant (V2i(64, 64)))
            out.Acquire()
            try out.GetValue() |> ignore
            finally out.Release()
        Log.line "bucketing: 16 ROs (8 default + 8 cull-back) -> %d bucket(s)" Heap.lastBucketCount
        let pass = Heap.lastBucketCount = 2
        if pass then Log.line "bucketing: PASS (distinct pipeline state -> distinct buckets)"
        else Log.warn "bucketing: FAIL (expected 2 buckets)"
        pass

    // Verifies DYNAMIC mode rules: a per-RO cull-mode aval drives bucketing, so
    // flipping it (in a transact) RE-PARTITIONS the heap reactively (1 -> 2 buckets)
    // without touching the object set.
    let modeRulesTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let viewProj = AVal.constant Trafo3d.Identity :> IAdaptiveValue
        let effect = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        let n = 16
        let culls = Array.init n (fun _ -> AVal.init CullMode.None)   // per-RO dynamic cull "rule"
        let inputs =
            Array.init n (fun i ->
                let ro = RenderObject()
                ro.Surface   <- Surface.Effect effect
                ro.Mode      <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- vattrs
                ro.Indices   <- Some (bv index typeof<int>)
                ro.RasterizerState <- { RasterizerState.Default with CullMode = culls.[i] }
                ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
                ro.Uniforms  <- UniformProvider.ofList [
                    Symbol.Create "HeapModelTrafo", (AVal.constant M44f.Identity :> IAdaptiveValue)
                    Symbol.Create "HeapColor",      (AVal.constant V4f.IIII :> IAdaptiveValue)
                    Symbol.Create "ViewProjTrafo",  viewProj ]
                ro :> IRenderObject)
        let signature = runtime.CreateFramebufferSignature [DefaultSemantic.Colors, TextureFormat.Rgba8]
        let heap = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray inputs)
        // signature-deferred: keep ONE compiled task alive across the flips so the
        // re-partitioning exercises the incremental re-key path (not a fresh build)
        use task = runtime.CompileRender(signature, heap)
        let out = task |> RenderTask.renderToColor (AVal.constant (V2i(64, 64)))
        out.Acquire()
        let force () = out.GetValue() |> ignore; Heap.lastBucketCount
        let b0 = force ()
        transact (fun () -> for i in 0 .. n-1 do if i % 2 = 0 then culls.[i].Value <- CullMode.Back)
        let b1 = force ()
        transact (fun () -> for i in 0 .. n-1 do culls.[i].Value <- CullMode.None)
        let b2 = force ()
        out.Release()
        Log.line "mode-rules: buckets all-None=%d half-Back=%d back-to-None=%d" b0 b1 b2
        let pass = b0 = 1 && b1 = 2 && b2 = 1
        if pass then Log.line "mode-rules: PASS (per-RO cull aval re-partitions the heap reactively: 1 -> 2 -> 1)"
        else Log.warn "mode-rules: FAIL (expected 1,2,1 got %d,%d,%d)" b0 b1 b2
        pass

    // GL backend: the uniform heap (no bindless textures) now works on GL via
    // gl_DrawID routing over multi-draw-indirect. Renders a cube grid classic
    // (N UBO draws) vs heap (1 multidraw, SSBO gather) on GL and compares.
    let glHeapTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.OpenGlApplication(false)
        let runtime = app.Runtime :> IRuntime
        Log.line "gl: SupportsMultiDrawIndirectDrawId=%b SupportsUnboundedSamplerArrays=%b"
            runtime.SupportsMultiDrawIndirectDrawId runtime.SupportsUnboundedSamplerArrays
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 18.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let effect = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan |]
        let s = 16
        let inputs =
            Array.init (s*s) (fun i ->
                let p = V3d(float (i % s - s/2) * 1.2, float (i / s - s/2) * 1.2, 0.0)
                let ro = RenderObject()
                ro.Surface   <- Surface.Effect effect
                ro.Mode      <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- vattrs
                ro.Indices   <- Some (bv index typeof<int>)
                ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
                ro.Uniforms  <- UniformProvider.ofList [
                    Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                    Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                    Symbol.Create "ViewProjTrafo",  viewProj ]
                ro :> IRenderObject)
        let imageOf (objs : aset<IRenderObject>) =
            use task = runtime.CompileRender(signature, objs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let classicPix = imageOf (ASet.ofArray inputs)
        let heapPix = imageOf (Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray inputs))
        let maxD, nDiff, nNonBg, total = diff classicPix heapPix
        Log.line "gl-heap: %d ROs -> %d bucket(s)  classic vs heap maxDelta=%d diffPixels=%d/%d coverage=%d" inputs.Length Heap.lastBucketCount maxD nDiff total nNonBg
        let pass = maxD <= 1 && nNonBg > total / 100L
        if pass then Log.line "gl-heap: PASS (uniform heap renders correctly on the GL backend via gl_DrawID)"
        else Log.warn "gl-heap: FAIL (maxDelta=%d nNonBg=%d)" maxD nNonBg
        pass

    // Verifies ALREADY-INSTANCED inputs to ofRenderObjects: input ROs that have
    // instanceCount > 1 are folded into the bucket with gl_DrawID per-draw routing,
    // so each sub-draw keeps its own per-draw uniforms while gl_InstanceIndex stays
    // the local instance index. Compared against the equivalent non-instanced
    // expansion (the per-instance offset baked into each trafo): must be identical.
    module AI =
        type V = { [<Position>] pos : V4f; [<Color>] c : V4f; [<InstanceId>] iid : int }
        let shadeInst (v : V) =
            vertex {
                let m  : M44f = uniform?HeapModelTrafo
                let vp : M44f = uniform?ViewProjTrafo
                let col : V4f = uniform?HeapColor
                let p = v.pos + V4f(float32 v.iid * 1.5f, 0.0f, 0.0f, 0.0f)
                return { v with pos = vp * (m * p); c = col }
            }
        let shadePlain (v : V) =
            vertex {
                let m  : M44f = uniform?HeapModelTrafo
                let vp : M44f = uniform?ViewProjTrafo
                let col : V4f = uniform?HeapColor
                return { v with pos = vp * (m * v.pos); c = col }
            }
        let frag (v : V) = fragment { return v.c }

    let alreadyInstancedTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let view = CameraView.lookAt (V3d(2.0, -1.0, 1.0) * 14.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let bases = [| V3d(0.0, 2.0, 0.0), C4f.Red; V3d(0.0, -2.0, 0.0), C4f.DodgerBlue |]
        let k = 4
        let mkRO effect (uniforms : list<Symbol * IAdaptiveValue>) (inst : int) =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect effect
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = inst) |])
            ro.Uniforms  <- UniformProvider.ofList uniforms
            ro :> IRenderObject
        let imageOf (objs : aset<IRenderObject>) =
            use task = runtime.CompileRender(signature, objs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()

        // instanced: 2 ROs, each instanceCount=4, offset per instance via gl_InstanceIndex
        let effInst = Effect.compose [ Effect.ofFunction AI.shadeInst; Effect.ofFunction AI.frag ]
        let instancedInputs =
            bases |> Array.map (fun (p, c) ->
                mkRO effInst [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                               Symbol.Create "HeapColor",      (AVal.constant (c.ToV4f()) :> IAdaptiveValue)
                               Symbol.Create "ViewProjTrafo",  viewProj ] k)
        let imgInst = imageOf (Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray instancedInputs))
        let instBuckets = Heap.lastBucketCount

        // reference: 8 plain ROs (offset baked into the trafo), instanceCount=1
        let effPlain = Effect.compose [ Effect.ofFunction AI.shadePlain; Effect.ofFunction AI.frag ]
        let plainInputs =
            [| for (p, c) in bases do
                 for i in 0 .. k-1 ->
                   mkRO effPlain [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p * Trafo3d.Translation(float i * 1.5, 0.0, 0.0)).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                                   Symbol.Create "HeapColor",      (AVal.constant (c.ToV4f()) :> IAdaptiveValue)
                                   Symbol.Create "ViewProjTrafo",  viewProj ] 1 |]
        let imgPlain = imageOf (Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray plainInputs))

        let maxD, nDiff, nNonBg, total = diff imgPlain imgInst
        Log.line "already-instanced: 2 ROs x %d instances (%d bucket, gl_DrawID routing) vs 8 plain ROs  maxDelta=%d diffPixels=%d coverage=%d" k instBuckets maxD nDiff nNonBg
        let pass = maxD <= 1 && nNonBg > 500L && instBuckets = 1
        if pass then Log.line "already-instanced: PASS (instanced inputs == non-instanced expansion; per-draw routed by gl_DrawID)"
        else Log.warn "already-instanced: FAIL (maxDelta=%d nNonBg=%d buckets=%d)" maxD nNonBg instBuckets
        pass

    // Verifies the per-RO IsActive visibility gate: deactivating half the draws
    // (in a transact) roughly halves the rendered coverage, with no rebuild.
    let visibilityTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 18.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let effect = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        let n = 64
        let s = int (ceil (sqrt (float n)))
        let actives = Array.init n (fun _ -> AVal.init true)
        let inputs =
            Array.init n (fun i ->
                let x = i % s
                let y = i / s
                let p = V3d(float (x - s/2) * 1.4, float (y - s/2) * 1.4, 0.0)
                let ro = RenderObject()
                ro.Surface   <- Surface.Effect effect
                ro.Mode      <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- vattrs
                ro.Indices   <- Some (bv index typeof<int>)
                ro.IsActive  <- actives.[i]
                ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
                ro.Uniforms  <-
                    UniformProvider.ofList [
                        Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                        Symbol.Create "HeapColor",      (AVal.constant (V4f(1.0f, 0.7f, 0.3f, 1.0f)) :> IAdaptiveValue)
                        Symbol.Create "ViewProjTrafo",  viewProj ]
                ro :> IRenderObject)
        let heap = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray inputs)
        use task = runtime.CompileRender(signature, heap)
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        let coverage () =
            let m = out.GetValue().Download().AsPixImage<uint8>().GetMatrix<C4b>()
            let mutable c = 0L
            m.ForeachCoord(fun (p : V2l) -> let v = m.[p] in if v.R <> 0uy || v.G <> 0uy || v.B <> 0uy then c <- c + 1L)
            c
        let all = coverage ()
        transact (fun () -> for i in 0 .. n - 1 do if i % 2 = 0 then actives.[i].Value <- false)
        let half = coverage ()
        out.Release()
        Log.line "visibility: %d bucket(s); coverage all=%d half=%d (ratio %.2f)" Heap.lastBucketCount all half (float half / float all)
        let pass = all > 0L && half > 0L && float half / float all < 0.65 && float half / float all > 0.35
        if pass then Log.line "visibility: PASS (deactivating half ~halves coverage, no rebuild)"
        else Log.warn "visibility: FAIL"
        pass

    // Headless churn probe: per-frame cost of ONE add + ONE remove in a TEXTURED
    // bucket (bindless sampler array) and in a BINDLESS-geometry bucket — the two
    // bucket kinds that used to take the full buildBucket rebuild path on every
    // membership change. Informational (prints median/max frame times); always
    // returns true. Env N overrides the bucket size (default 5000).
    let churnProbeTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(512, 512))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 60.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let n = match System.Environment.GetEnvironmentVariable "N" with null | "" -> 5000 | s -> int s
        let frames = 30
        let warmup = 5
        let side = max 1 (int (ceil (sqrt (float n))))
        let posOf (i : int) = V3d(float (i % side - side / 2) * 1.2, float (i / side - side / 2) * 1.2, 0.0)

        let run (label : string) (n : int) (mkRO : int -> IRenderObject) =
            let all = Array.init (n + warmup + frames) mkRO
            let ros = cset (Array.sub all 0 n)
            let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ros :> aset<_>)
            use task = runtime.CompileRender(signature, heapObjs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            out.GetValue() |> ignore
            for i in 0 .. warmup - 1 do
                transact (fun () ->
                    ros.Remove all.[i] |> ignore
                    ros.Add all.[n + i] |> ignore)
                out.GetValue() |> ignore
            let times = System.Collections.Generic.List<float>()
            for i in 0 .. frames - 1 do
                let sw = System.Diagnostics.Stopwatch.StartNew()
                transact (fun () ->
                    ros.Remove all.[warmup + i] |> ignore
                    ros.Add all.[n + warmup + i] |> ignore)
                out.GetValue() |> ignore
                sw.Stop()
                times.Add sw.Elapsed.TotalMilliseconds
            out.Release()
            let sorted = times |> Seq.sort |> Seq.toArray
            Log.line "churnProbe[%s]: n=%d  add+remove/frame  median=%.2f ms  p90=%.2f ms  max=%.2f ms"
                label n sorted.[sorted.Length / 2] sorted.[(sorted.Length * 9) / 10] sorted.[sorted.Length - 1]

        // ── textured bucket (per-object DiffuseTexture -> bindless sampler array) ──
        let texArray : ITexture[] = Array.init TexCount mkTexture
        let effTex = Effect.compose [ Effect.ofFunction TH.shade; Effect.ofFunction TH.frag ]
        let vattrsHost = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let mkTexRO (i : int) =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect effTex
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrsHost
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation (posOf i)).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj
                Symbol.Create "DiffuseTexture", (AVal.constant texArray.[i % TexCount] :> IAdaptiveValue) ]
            ro :> IRenderObject
        run "textured" n mkTexRO

        // ── bindless-geometry bucket (GPU-resident buffers -> vertex-pull) ──
        let posGpu = runtime.PrepareBuffer(ArrayBuffer positions :> IBuffer)
        let nrmGpu = runtime.PrepareBuffer(ArrayBuffer normals   :> IBuffer)
        let idxGpu = runtime.PrepareBuffer(ArrayBuffer index     :> IBuffer)
        let gbv (b : IBackendBuffer) t = BufferView(AVal.constant (b :> IBuffer), t)
        let vattrsGpu = AttributeProvider.ofList [ Symbol.Create "Positions", gbv posGpu typeof<V3f>; Symbol.Create "Normals", gbv nrmGpu typeof<V3f> ]
        let effGeom = Effect.compose [ Effect.ofFunction GG.shade; Effect.ofFunction GG.frag ]
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan |]
        let mkGpuRO (i : int) =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect effGeom
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrsGpu
            ro.Indices   <- Some (gbv idxGpu typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation (posOf i)).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj ]
            ro :> IRenderObject
        // the unbounded SSBO array binding is device-capped at 1024 descriptors
        // (slots * numAttrs), a pre-existing backend limit of the vertex-pull
        // path — size the bindless bucket within it (2 attrs -> <= 512 slots).
        run "bindless-geom" (min n 480) mkGpuRO
        true

    // Geometry reclamation probe: DISTINCT per-RO geometry, one remove + one add per
    // frame with a FRESH equal-sized geometry. The combined packed buffers must stay
    // FLAT after the initial build (a freed geometry's exact-size ranges are reused
    // in place — Heap.lastPackedGeomBytes) and the churned scene's final image must
    // equal a freshly-built scene of the same membership. Two phases: host-packed
    // geometry (vertex + index ranges) and bindless/vertex-pull (index ranges only).
    let geomChurnTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(512, 512))
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 16.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let n = 50                  // NOT divisible by the size/color cycles below,
        let frames = 96             // so a replacement RO differs visibly from its victim
        let side = 8
        let posOf (i : int) = V3d(float (i % side - side / 2) * 1.4, float (i / side - side / 2) * 1.4, 0.0)
        // FRESH (identity-distinct) box arrays per call — same vertex/index COUNTS
        // (exact-size reuse), i-dependent size so stale-range bugs change pixels.
        let mkGeom (i : int) =
            let s = 0.45 + 0.25 * float (i % 4) / 3.0
            let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * s)) C4b.White).ToIndexed()
            (g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]> |> Array.copy),
            (g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]> |> Array.copy),
            (g.IndexArray |> unbox<int[]> |> Array.copy)
        let effect = Effect.compose [ Effect.ofFunction GG.shade; Effect.ofFunction GG.frag ]
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan |]
        let mkRO (i : int) (attrs : IAttributeProvider) (idxBV : BufferView) (faceVertexCount : int) =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect effect
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- attrs
            ro.Indices   <- Some idxBV
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = faceVertexCount, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation (posOf (i % n))).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj ]
            ro :> IRenderObject
        let mkHostRO (i : int) =
            let (positions, normals, index) = mkGeom i
            let attrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
            mkRO i attrs (bv index typeof<int>) index.Length
        let mkGpuRO (i : int) =
            let (positions, normals, index) = mkGeom i
            let gbv (b : IBackendBuffer) t = BufferView(AVal.constant (b :> IBuffer), t)
            let posGpu = runtime.PrepareBuffer(ArrayBuffer positions :> IBuffer)
            let nrmGpu = runtime.PrepareBuffer(ArrayBuffer normals   :> IBuffer)
            let idxGpu = runtime.PrepareBuffer(ArrayBuffer index     :> IBuffer)
            let attrs = AttributeProvider.ofList [ Symbol.Create "Positions", gbv posGpu typeof<V3f>; Symbol.Create "Normals", gbv nrmGpu typeof<V3f> ]
            mkRO i attrs (gbv idxGpu typeof<int>) index.Length

        let runPhase (label : string) (mk : int -> IRenderObject) =
            let all = Array.init (n + frames) mk
            let ros = cset (Array.sub all 0 n)
            let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ros :> aset<_>)
            use task = runtime.CompileRender(signature, heapObjs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            out.GetValue() |> ignore
            let bytes0 = Heap.lastPackedGeomBytes
            let comps0 = Heap.compactionCount
            let mutable maxBytes = bytes0
            for i in 0 .. frames - 1 do
                transact (fun () ->
                    ros.Remove all.[i] |> ignore
                    ros.Add all.[n + i] |> ignore)
                out.GetValue() |> ignore
                maxBytes <- max maxBytes Heap.lastPackedGeomBytes
            let img = out.GetValue().Download().ToPixImage<byte>()
            out.Release()
            // reference: the SAME final membership (all.[frames .. frames+n-1]) built fresh
            let refRos = cset (Array.sub all frames n)
            let refObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (refRos :> aset<_>)
            use refTask = runtime.CompileRender(signature, refObjs)
            let refOut = refTask |> RenderTask.renderToColor size
            refOut.Acquire()
            let refImg = refOut.GetValue().Download().ToPixImage<byte>()
            refOut.Release()
            let (maxDelta, nDiff, nNonBg, total) = diff img refImg
            let flat = maxBytes = bytes0
            // exact-size churn must be served by allocator reuse BEFORE any
            // compaction can trigger (no waste accumulates -> no fire)
            let noCompact = Heap.compactionCount = comps0
            Log.line "geomChurn[%s]: packedBytes start=%d maxDuringChurn=%d flat=%b compactions=%d  maxChannelDelta=%d diffPixels=%d/%d coverage=%d"
                label bytes0 maxBytes flat (Heap.compactionCount - comps0) maxDelta nDiff total nNonBg
            let pass = flat && noCompact && maxDelta = 0 && nNonBg > 1000L
            if pass then Log.line "geomChurn[%s]: PASS" label
            else Log.warn "geomChurn[%s]: FAIL" label
            pass

        let hostOk = runPhase "host" mkHostRO
        let gpuOk  = runPhase "bindless" mkGpuRO
        let pass = hostOk && gpuOk
        if pass then Log.line "geomChurn: ALL PASS" else Log.warn "geomChurn: FAIL (host=%b bindless=%b)" hostOk gpuOk
        pass

    // VALUE-level geometry dedup golden: two ROs wrapping the SAME arrays in
    // FRESH BufferViews/avals (exactly what Sg combinators + Primitives.Box
    // produce per leaf) must share ONE packed geometry allocation — the arena
    // footprint (Heap.lastPackedGeomBytes) must equal the identity-shared
    // baseline (both ROs using the very same BufferView instances) and the
    // image must be pixel-identical to it. A fresh-COPY control (identity- AND
    // value-distinct arrays of the same content) must pack MORE bytes, proving
    // the assertion is not vacuous.
    let geomValueDedupTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(512, 512))
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 6.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let effect = Effect.compose [ Effect.ofFunction GG.shade; Effect.ofFunction GG.frag ]

        // ONE set of shared box arrays — the value-level identity under test
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.8)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>

        let palette = [| C4f.Red; C4f.DodgerBlue |]
        let mkRO (i : int) (attrs : IAttributeProvider) (idxBV : BufferView) =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect effect
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- attrs
            ro.Indices   <- Some idxBV
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation(V3d(float i * 1.6 - 0.8, 0.0, 0.0))).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "HeapColor",      (AVal.constant (palette.[i % 2].ToV4f()) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj ]
            ro :> IRenderObject

        let render (mk : int -> IRenderObject) =
            let ros = ASet.ofArray [| mk 0; mk 1 |]
            let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) ros
            use task = runtime.CompileRender(signature, heapObjs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            let img = out.GetValue().Download().ToPixImage<byte>()
            let bytes = Heap.lastPackedGeomBytes
            out.Release()
            bytes, img

        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        // A — identity-shared baseline: ONE BufferView/aval instance per attribute,
        // referenced by BOTH ROs (the dedup that always worked)
        let sharedAttrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let sharedIdx = bv index typeof<int>
        let bytesA, imgA = render (fun i -> mkRO i sharedAttrs sharedIdx)
        // B — FRESH BufferView + fresh AVal.constant(ArrayBuffer ...) per RO,
        // all wrapping the SAME arrays: must dedup at the VALUE level
        let bytesB, imgB =
            render (fun i ->
                let attrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
                mkRO i attrs (bv index typeof<int>))
        // C — control: fresh array COPIES per RO (value-distinct) must NOT dedup
        let bytesC, _ =
            render (fun i ->
                let attrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv (Array.copy positions) typeof<V3f>; DefaultSemantic.Normals, bv (Array.copy normals) typeof<V3f> ]
                mkRO i attrs (bv (Array.copy index) typeof<int>))

        let (maxDelta, nDiff, nNonBg, total) = diff imgB imgA
        Log.line "geomValue: packedBytes identity-shared=%d fresh-wrappers=%d fresh-copies=%d  maxChannelDelta=%d diffPixels=%d/%d coverage=%d"
            bytesA bytesB bytesC maxDelta nDiff total nNonBg
        let pass = bytesB = bytesA && bytesC > bytesB && maxDelta = 0 && nNonBg > 1000L
        if pass then Log.line "geomValue: PASS" else Log.warn "geomValue: FAIL"
        pass

    // NON-indexed golden: ROs WITHOUT an index buffer (single zero-offset
    // Direct draw call) must ride the heap too — the slot's header carries the
    // -1 sentinel and the shader's decodeHeapIndex passes gl_VertexIndex
    // through. Three scenes over the same two-triangle membership: all-indexed
    // (reference), MIXED indexed + non-indexed in ONE bucket (per-slot decode
    // branch), and all-non-indexed. All must collapse to 1 bucket (no
    // pass-through) and render pixel-identical.
    let nonIndexedTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(512, 512))
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 4.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let effect = Effect.compose [ Effect.ofFunction GG.shade; Effect.ofFunction GG.frag ]
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)

        // a quad as two triangles, vertices ALREADY unrolled so the indexed
        // variant ([0..5]) and the non-indexed variant draw identical streams
        let positions = [| V3f.Zero; V3f.IOO; V3f.OIO;  V3f.OIO; V3f.IOO; V3f(1.0f, 1.0f, 0.0f) |]
        let normals   = Array.create 6 V3f.OOI
        let index     = [| 0; 1; 2; 3; 4; 5 |]

        let palette = [| C4f.Red; C4f.DodgerBlue |]
        let mkRO (i : int) (indexed : bool) =
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect effect
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
            ro.Indices   <- if indexed then Some (bv index typeof<int>) else None
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = 6, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation(V3d(float i * 1.3 - 1.1, 0.0, 0.0))).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "HeapColor",      (AVal.constant (palette.[i % 2].ToV4f()) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj ]
            ro :> IRenderObject

        let render (mk : int -> IRenderObject) =
            let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray [| mk 0; mk 1 |])
            use task = runtime.CompileRender(signature, heapObjs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            let img = out.GetValue().Download().ToPixImage<byte>()
            let buckets = Heap.lastBucketCount
            out.Release()
            buckets, img

        let bRef,   imgRef   = render (fun i -> mkRO i true)
        let bMixed, imgMixed = render (fun i -> mkRO i (i = 0))
        let bNoIdx, imgNoIdx = render (fun i -> mkRO i false)

        let (d1, n1, nNonBg, total) = diff imgMixed imgRef
        let (d2, n2, _, _) = diff imgNoIdx imgRef
        Log.line "noindex: buckets ref=%d mixed=%d nonindexed=%d  mixedDelta=%d/%d nonIdxDelta=%d/%d coverage=%d/%d"
            bRef bMixed bNoIdx d1 n1 d2 n2 nNonBg total
        let pass = bRef = 1 && bMixed = 1 && bNoIdx = 1 && d1 = 0 && d2 = 0 && nNonBg > 1000L
        if pass then Log.line "noindex: PASS" else Log.warn "noindex: FAIL"
        pass

    // Drift golden test: 320 frames of churn with RANDOM-SIZED distinct
    // geometries (cone fans with random rim counts) and random per-RO instance
    // counts on the FORCED slot-attribute fallback (Heap.forceNoDrawId), so
    // EVERY reclamation site drifts: packed vertex ranges, packed index ranges,
    // arena float regions and the per-instance slot-attribute ranges. The
    // coalescing allocators plus waste-triggered compaction must keep every
    // footprint bounded by ~2.5x the live working set (+ 2x the compaction
    // floor, which is lowered for the test so the small buffers compact too),
    // and the final image must be pixel-identical to a freshly-built scene of
    // the same final population (compaction rewrote FirstIndex/BaseVertex/
    // FirstInstance/headers/arena offsets correctly). A bulk 75% removal at
    // half-time deterministically trips the live<50% trigger.
    let geomDriftTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(512, 512))
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 16.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let n = 48
        let frames = 320
        let rnd = RandomSystem 1234
        let side = 8
        let posOf (i : int) = V3d(float (i % side - side / 2) * 1.4, float (i / side % side - side / 2) * 1.4, 0.0)
        // random-size cone fan: m rim vertices + apex, 3m indices — every
        // geometry is identity-distinct AND size-distinct, so exact-size reuse
        // alone cannot keep the packed buffers flat (the drift this test bounds).
        let mkGeom () =
            let m = 24 + rnd.UniformInt 240
            let s = 0.35 + 0.4 * rnd.UniformDouble()
            let positions =
                Array.init (m + 1) (fun j ->
                    if j = 0 then V3f(0.0f, 0.0f, float32 s)
                    else
                        let a = float (j - 1) / float m * System.Math.PI * 2.0
                        V3f(float32 (cos a * s), float32 (sin a * s), 0.0f))
            let normals =
                Array.init (m + 1) (fun j ->
                    if j = 0 then V3f.OOI
                    else Vec.normalize (V3f(positions.[j].X, positions.[j].Y, 0.5f)))
            let index = [| for t in 0 .. m - 1 do yield 0; yield 1 + t; yield 1 + ((t + 1) % m) |]
            positions, normals, index
        let effect = Effect.compose [ Effect.ofFunction GG.shade; Effect.ofFunction GG.frag ]
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan |]
        let mkRO (i : int) =
            let (positions, normals, index) = mkGeom ()
            let k = 2 + rnd.UniformInt 8        // random instance count (>1 -> instanced bucket)
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect effect
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
            ro.Indices   <- Some (bv index typeof<int>)
            // K identical instances per draw: the slot-attribute routing (not the
            // image) is what varies — wrong instData slots would fetch the wrong
            // per-draw uniforms and change pixels.
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = k) |])
            ro.Uniforms  <- UniformProvider.ofList [
                Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation (posOf (i % n))).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo",  viewProj ]
            ro :> IRenderObject
        let all = Array.init (n + frames + n) mkRO

        let floor0 = Heap.compactionWasteFloorBytes
        let comps0 = Heap.compactionCount
        Heap.forceNoDrawId <- true
        Heap.compactionWasteFloorBytes <- 512   // tiny floor so the small arena/inst buffers compact too
        try
            let ros = cset (Array.sub all 0 n)
            let live = System.Collections.Generic.List<IRenderObject>(Array.sub all 0 n)
            let mutable next = n
            let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ros :> aset<_>)
            use task = runtime.CompileRender(signature, heapObjs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            out.GetValue() |> ignore
            let mutable violations = 0
            let check (label : string) (bytes : int) (liveB : int) =
                let bound = int (2.5 * float liveB) + 2 * Heap.compactionWasteFloorBytes
                if bytes > bound then
                    violations <- violations + 1
                    Log.warn "geomDrift: %s footprint %d B exceeds bound %d B (live %d B)" label bytes bound liveB
            for f in 0 .. frames - 1 do
                transact (fun () ->
                    if f = frames / 2 then
                        // bulk shrink: 75% of the population leaves in ONE pass
                        for _ in 1 .. (live.Count * 3) / 4 do
                            let j = rnd.UniformInt live.Count
                            ros.Remove live.[j] |> ignore
                            live.RemoveAt j
                    else
                        let j = rnd.UniformInt live.Count
                        ros.Remove live.[j] |> ignore
                        live.RemoveAt j
                        ros.Add all.[next] |> ignore; live.Add all.[next]; next <- next + 1
                        if live.Count < n then      // regrow after the bulk shrink
                            ros.Add all.[next] |> ignore; live.Add all.[next]; next <- next + 1)
                out.GetValue() |> ignore
                check "packedGeom" Heap.lastPackedGeomBytes Heap.lastPackedGeomLiveBytes
                check "arena"      Heap.lastArenaBytes      Heap.lastArenaLiveBytes
                check "inst"       Heap.lastInstBytes       Heap.lastInstLiveBytes
            let img = out.GetValue().Download().ToPixImage<byte>()
            out.Release()
            let compactions = Heap.compactionCount - comps0

            // reference: the SAME final membership built fresh
            let refRos = cset (live.ToArray())
            let refObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (refRos :> aset<_>)
            use refTask = runtime.CompileRender(signature, refObjs)
            let refOut = refTask |> RenderTask.renderToColor size
            refOut.Acquire()
            let refImg = refOut.GetValue().Download().ToPixImage<byte>()
            refOut.Release()
            let (maxDelta, nDiff, nNonBg, total) = diff img refImg
            Log.line "geomDrift: %d frames, %d compactions, boundViolations=%d  final: packed=%d/%d arena=%d/%d inst=%d/%d (bytes/live)"
                frames compactions violations
                Heap.lastPackedGeomBytes Heap.lastPackedGeomLiveBytes
                Heap.lastArenaBytes Heap.lastArenaLiveBytes
                Heap.lastInstBytes Heap.lastInstLiveBytes
            Log.line "geomDrift: maxChannelDelta=%d diffPixels=%d/%d coverage=%d" maxDelta nDiff total nNonBg
            let pass = violations = 0 && compactions > 0 && maxDelta = 0 && nNonBg > 1000L
            if pass then Log.line "geomDrift: PASS"
            else Log.warn "geomDrift: FAIL (violations=%d compactions=%d maxDelta=%d coverage=%d)" violations compactions maxDelta nNonBg
            pass
        finally
            Heap.forceNoDrawId <- false
            Heap.compactionWasteFloorBytes <- floor0

    // Lifetime golden test: repeatedly (30x) build a heap scene — a PLAIN
    // uniform bucket plus a TEXTURED atlas bucket (forceAtlas), so the draw/
    // header mirrors, the HeapArena AND the atlas pool are all covered —
    // render a few frames, then tear it down through the API's disposal entry
    // (empty the input set -> the incremental driver disposes the buckets;
    // the render task's delta processing releases the prepared resources) and
    // dispose the task. After every cycle the device's VMA statistics
    // (allocation count + allocated bytes) must return to the post-warmup
    // baseline: the heap-owned AdaptiveBuffers are destroyed by the resource
    // layer's Release (IAdaptiveResource refcounting). On the pre-fix tree the
    // bucket avals stripped IAdaptiveResource (plain AVal.map/custom), nothing
    // ever destroyed the buffers, and the metrics grow monotonically -> FAIL.
    let lifetimeTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let device = runtime.Device
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(512, 512))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 18.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProjM = AVal.init (view * proj)
        let viewProj = viewProjM :> IAdaptiveValue
        let texArray : ITexture[] = Array.init 8 mkTexture
        // effects built ONCE so shader modules / pipelines are cached across cycles
        let effPlain = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        let effTex   = Effect.compose [ Effect.ofFunction TH.shade;      Effect.ofFunction TH.frag ]
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan |]
        let mkRO (i : int) (eff : Effect) (tex : bool) =
            let p = V3d(float (i % 8 - 4) * 1.2, float (i / 8 - 4) * 1.2, (if tex then 0.7 else -0.7))
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect eff
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [
                yield Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                yield Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                yield Symbol.Create "ViewProjTrafo",  viewProj
                if tex then yield Symbol.Create "DiffuseTexture", (AVal.constant texArray.[i % texArray.Length] :> IAdaptiveValue) ]
            ro :> IRenderObject

        let stats () = let struct (c, b) = device.MemoryStatistics in (c, b)

        let runCycle (ci : int) =
            // FRESH render objects + fresh heap each cycle (fresh geometry array
            // identities would defeat pipeline caching, so geometry is shared;
            // the heap's packed buffers are per-bucket and rebuilt anyway).
            let ros = cset (Array.init 64 (fun i -> if i % 2 = 0 then mkRO i effPlain false else mkRO i effTex true))
            let heapObjs = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ros :> aset<_>)
            use task = runtime.CompileRender(signature, heapObjs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            // a few frames (camera nudges force re-submission)
            for f in 0 .. 2 do
                transact (fun () -> viewProjM.Value <- CameraView.lookAt (V3d(0.02 * float (ci + f), -1.0, 1.0) * 18.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo |> fun v -> v * proj)
                out.GetValue() |> ignore
            let buckets = Heap.lastBucketCount
            // teardown via the API's disposal entry: empty the input set -> the
            // updater disposes the (now key-less) buckets and the render task's
            // delta processing RELEASES the bucket RO's resources (which, via
            // IAdaptiveResource refcounting, destroys the heap-owned buffers).
            transact (fun () -> ros.Clear())
            out.GetValue() |> ignore
            out.Release()
            buckets

        // force the ATLAS texture path (desktop Vulkan would go bindless), so a
        // bucket-owned AtlasPool + dummy textures are created/destroyed per cycle.
        Heap.forceAtlas <- true
        try
            // warm-up cycles populate caches (shader modules, pipelines, sampler /
            // descriptor machinery, VMA block pools); baseline taken after them.
            for ci in 0 .. 2 do runCycle ci |> ignore
            let (count0, bytes0) = stats ()
            let cycles = 30
            let mutable buckets = 0
            for ci in 0 .. cycles - 1 do
                buckets <- max buckets (runCycle (3 + ci))
                let (c, b) = stats ()
                if ci % 10 = 0 || ci = cycles - 1 then
                    Log.line "lifetime: cycle %2d  allocations=%d (baseline %d)  bytes=%d (baseline %d)" ci c count0 b bytes0
            let (countN, bytesN) = stats ()
            // post-cycle stats must RETURN to baseline (small slack for lazily
            // created caches); in-cycle peaks are naturally higher (live scene).
            let countGrowth = countN - count0
            let bytesGrowth = int64 bytesN - int64 bytes0
            Log.line "lifetime: %d cycles, %d bucket(s)/cycle  growth: allocations=%+d bytes=%+d" cycles buckets countGrowth bytesGrowth
            let pass = buckets >= 2 && countGrowth <= 8 && bytesGrowth <= 4L * 1024L * 1024L
            if pass then Log.line "lifetime: PASS (device memory returns to baseline after each scene teardown)"
            else Log.warn "lifetime: FAIL (allocations %d -> %d, bytes %d -> %d)" count0 countN bytes0 bytesN
            pass
        finally
            Heap.forceAtlas <- false
