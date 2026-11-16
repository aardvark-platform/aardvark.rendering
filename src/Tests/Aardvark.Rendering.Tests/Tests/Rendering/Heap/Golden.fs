namespace Aardvark.Rendering.Tests.Rendering.Heap

// Golden-image equivalence (Vulkan): render the same scene two ways and compare
// pixel-by-pixel. The `run` probe covers three scenes:
//
//   1. uniform: classic (N independent render objects, per-draw uniforms in UBOs)
//      vs heap (N render objects -> 1 bucket, uniforms gathered from the arena
//      SSBO). Same effect; the rewrite changes only where per-draw uniforms come
//      from.
//
//   1b. transparent: the same scene with every render object flagged transparent,
//      so both paths go through the OIT (order-independent transparency) store.
//
//   2. textured: classic conventional per-object textures (each render object
//      binds its own sampler2D DiffuseTexture) vs heap bindless (one unbounded
//      `sampler2D Textures[]`, per-object index in the arena, sampled with
//      nonuniformEXT). Different effects, identical visual result: proves the
//      bindless heap path matches plain per-object texturing.
//
// MSAA is off for a deterministic comparison.

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Rendering.Text
open Aardvark.Rendering.Tests
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

    let run (runtime : IRuntime) =

        use signature =
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
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()

        let report (label : string) (classicPix : PixImage<uint8>) (heapPix : PixImage<uint8>) =
            let maxDelta, nDiff, nNonBg, total = diff classicPix heapPix
            Log.line "golden[%s]: -> %d bucket(s)" label Heap.lastBucketCount
            Log.line "golden[%s]: classic vs heap  maxChannelDelta=%d  diffPixels=%d/%d (%.4f%%)  coverage=%d px"
                label maxDelta nDiff total (100.0 * float nDiff / float total) nNonBg
            let pass = maxDelta <= 1 && nNonBg > total / 100L
            if pass then Log.line "golden[%s]: PASS" label
            else Log.warn "golden[%s]: FAIL (maxDelta=%d nNonBg=%d)" label maxDelta nNonBg
            pass

        // Scene 1: uniform-only (same effect; UBO vs arena)
        let effectU = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan |]
        let inputsU =
            gridOf 256 |> Array.map (fun (i, p) ->
                mkRO [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                       Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                       Symbol.Create "ViewProjTrafo",  viewProj ] effectU)
        let classicU = renderToPix (ASet.ofArray inputsU)
        use storage = runtime.CreateHeapStorage()
        let heapU = Heap.ofRenderObjects storage (ASet.ofArray inputsU)
        let passU = report "uniform" classicU (renderToPix heapU)

        // Scene 1b: transparent bucket (the OIT store path). The composed store
        // program gains OIT resources alongside the Heap* buffers, so this guards
        // the pipeline-layout/binding consistency of the rewritten vertex shader
        // (the VUID-VkGraphicsPipelineCreateInfo-layout-07988 class of failures).
        // The content has opaque alpha, so the OIT composite must equal plain
        // depth-tested rendering for classic and heap alike.
        let mkTransparentROs () =
            gridOf 256 |> Array.map (fun (i, p) ->
                let ro =
                    mkRO [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                           Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                           Symbol.Create "ViewProjTrafo",  viewProj ] effectU
                (match ro with :? RenderObject as r -> r.IsTransparent <- true | _ -> ())
                ro)
        let classicO = renderToPix (ASet.ofArray (mkTransparentROs ()))
        use storage = runtime.CreateHeapStorage()
        let heapO = Heap.ofRenderObjects storage (ASet.ofArray (mkTransparentROs ()))
        let passO = report "oit-transparent" classicO (renderToPix heapO)

        // Scene 2: textured (conventional per-object vs bindless heap)
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
        // the textured phase hard-codes the bindless sampler array, the path the
        // heap itself only takes when the runtime supports unbounded sampler
        // arrays (MoltenVK does not: it cannot compile non-uniformly indexed
        // unbounded sampler arrays, which is why the atlas fallback exists; the
        // `atlas-heap` probe covers it).
        let passT =
            if runtime.SupportsUnboundedSamplerArrays then
                let classicTpix = renderToPix (ASet.ofArray classicT)
                use storage = runtime.CreateHeapStorage()
                let heapTobjs = Heap.ofRenderObjects storage (ASet.ofArray heapT)
                report "textured" classicTpix (renderToPix heapTobjs)
            else
                Log.line "golden[textured]: SKIP (no unbounded sampler arrays; the heap uses the atlas here; run `atlas-heap`)"
                true

        let pass = passU && passO && passT
        if pass then Log.line "golden: ALL PASS (uniform + bindless-textured heap == classic)"
        else Log.warn "golden: FAILED"
        pass

    // Auto-detected per-draw heap fields (Heap.ofRenderObjects): the same scene
    // rendered classic and heap must agree within one channel step. Also asserts
    // the classification itself via Heap.lastAutoFields:
    //   * consumed + supplied by the render object + packable -> per-draw field
    //     (ViewProjTrafo, derived from View/Proj, counts as a field too), and
    //   * supplied by the render object but not consumed -> ignored.
    // The detected field set is part of the bucket key, but a same-effect scene
    // with differing field sets cannot be built (see the note in the probe), so
    // the probe asserts detection and pixels only; the scene must land in one bucket.
    let autoFieldsTest (runtime : IRuntime) =
        use signature =
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
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue   // one shared aval (classic reads it directly)
        // View/Proj are the constituents every consumer provides; the heap derives
        // ViewProjTrafo from them (ProjTrafo*ViewTrafo). Shared avals -> one region each.
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
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()

        // shadeFragTint requests Tint at double precision (V3d), which exercises the
        // heap's real-double storage/gather (HeapDataD). One shared aval -> one double region.
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
                       // supplied (packable) but not consumed by the effect ->
                       // auto-detection must ignore it
                       Symbol.Create "NotConsumed",    (AVal.constant i :> IAdaptiveValue) ] effect)

        let classicPix  = renderToPix (ASet.ofArray inputs)
        use storage = runtime.CreateHeapStorage()
        let heapPix     = renderToPix (Heap.ofRenderObjects storage (ASet.ofArray inputs))
        let autoBuckets = Heap.lastBucketCount
        let detected    = Heap.lastAutoFields

        // classification: a name is a field when it is consumed and either derivable
        // by a recipe or supplied in a packable type. ViewProjTrafo is a derived
        // composite -> a per-slot compute output field; its constituents ProjTrafo and
        // ViewTrafo are not fields (internal M44d regions the compute reads), and the
        // supplied-but-unconsumed NotConsumed is ignored. So the field set is the
        // direct uniforms plus the composite output.
        let expected = [| "HeapColor"; "HeapModelTrafo"; "Tint"; "ViewProjTrafo" |]
        let fieldsOk = detected = expected
        if not fieldsOk then Log.warn "autoFields: detected fields %A (expected %A)" detected expected

        let dC, nC, nbg, total = diff classicPix heapPix
        Log.line "autoFields: fields=%A  buckets=%d" detected autoBuckets
        Log.line "autoFields: heap vs classic maxDelta=%d diffPixels=%d/%d  coverage=%d px" dC nC total nbg
        let pixOk = dC <= 1 && nbg > total / 100L && autoBuckets = 1

        // There is no "different field sets split buckets" assertion: a render object
        // that does not supply a consumed uniform cannot render under any path (classic
        // fails with 'Could not find uniform' just the same), and for renderable
        // render objects sharing one effect the detected field set is necessarily
        // equal, so no valid same-effect split scene exists. The field set is still
        // part of the bucket key (Layout carries "|f:<fields>" in factsOf). Bucket
        // counts can only be observed through a real render: the heap builds
        // signature-deferred (on first render-task activation), so forcing the output
        // set does not build any buckets.

        let pass = fieldsOk && pixOk
        if pass then Log.line "autoFields: PASS (auto-detected fields render == classic; NotConsumed ignored)"
        else Log.warn "autoFields: FAIL (fieldsOk=%b pixOk=%b)" fieldsOk pixOk
        pass

    // `Sg.heap`, the scene-graph node around Heap.ofRenderObjects (sgHeapTest). One
    // scene built entirely with ordinary Sg combinators (per-leaf Sg.trafo' +
    // Sg.uniform' boxes, effect + camera applied above), rendered three ways:
    //   * classic: the plain Sg, one render object per leaf
    //   * Sg.heap on the direct render path (the default CompileRender entry)
    //   * Sg.heap on the legacy attribute-grammar path (EnvVar.AARDVARK_LEGACY_SG=1)
    // Both heap renders must match classic within one channel step (same effect
    // both ways; the rewrite changes only where the per-draw uniforms come from),
    // and the N per-leaf render objects must collapse to one bucket (same effect,
    // layout and field set).
    module SGH =
        // reads the Sg-conventional ModelTrafo (per-leaf trafo stack) and the shared
        // ViewProjTrafo separately (no derived ModelViewProjTrafo), so classic and
        // heap run the same in-shader multiply on the same float32 values.
        let vert (v : Shaders.Vertex) =
            vertex {
                let m   : M44f = uniform?ModelTrafo
                let vp  : M44f = uniform?ViewProjTrafo
                let col : V4f  = uniform?HeapColor
                return { v with pos = vp * (m * v.pos); c = col; n = m.TransformDir v.n }
            }

        // reads the color from the Colors vertex attribute (v.c) instead of a
        // uniform: exercises singleton (SingleValueBuffer) and real-buffer
        // attribute decode in one bucket.
        let vertCol (v : Shaders.Vertex) =
            vertex {
                let m   : M44f = uniform?ModelTrafo
                let vp  : M44f = uniform?ViewProjTrafo
                return { v with pos = vp * (m * v.pos); n = m.TransformDir v.n }
            }

        // fragment shader that reads a per-draw uniform (HeapColor), as lighting or
        // animation shaders do with per-object light positions or times. This forces the
        // heap to pass the slot to the fragment stage and gather from HeapData there,
        // alongside the vertex-stage gathers (ModelTrafo and the derived ViewProjTrafo).
        let fragUni (v : Shaders.Vertex) =
            fragment {
                let extra : V4f = uniform?HeapColor
                return { v with c = v.c * 0.5f + extra * 0.5f }
            }

    // Two side-by-side high-subdivision spheres with the page size set so that each
    // sphere fills its own heap page (sphere0 -> page 0, drawn by the bucket render
    // object itself; sphere1 -> page 1, drawn by a per-page clone). Renders classic vs
    // heap on the direct render path, saves both images to the temp directory, and
    // reports the diff per half (left = sphere0, right = sphere1) so a broken page draw
    // can be identified. Each half may differ in at most 5% of its covered pixels.
    let sgSphereTest (runtime : IRuntime) =
        use signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let envI (k : string) (d : int) = match System.Environment.GetEnvironmentVariable k with null | "" -> d | s -> int s
        let level = envI "SPH_LEVEL" 6

        let ig = IndexedGeometryPrimitives.Sphere.solidSubdivisionSphere (Sphere3d(V3d.Zero, 0.8)) level C4b.White
        let nv = (ig.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>).Length
        // size a page so one sphere fits but two do not -> exactly 2 pages
        let pageWords = nv * 12
        EnvVar.AARDVARK_HEAP_PAGE_WORDS <- string pageWords
        Log.line "[sphere] level=%d verts=%d -> AARDVARK_HEAP_PAGE_WORDS=%d (1 sphere/page)" level nv pageWords

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
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()

        let classicPix = renderToPix (scene id)
        use storage = runtime.CreateHeapStorage()
        let heapPix = renderToPix (scene (Sg.heap storage))
        (try classicPix.SaveAsPng (System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sph_classic.png")); heapPix.SaveAsPng (System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sph_heap.png")) with _ -> Log.warn "[sphere] png save unavailable")

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

    let sgHeapTest (runtime : IRuntime) =
        use signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))

        // Scale knobs (default: a 16x16 grid in unit coordinates). Set SG_S (grid side) and
        // SG_SCALE (coordinate/camera multiplier) to reproduce a many-parts scene with large
        // CAD-style coordinates (e.g. SG_S=64 SG_SCALE=200 -> 4096 parts, coordinates up to
        // about +-7.7k).
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

        // effect + camera above the (optional) heap node: attributes flow to the
        // leaves through the scope either way.
        // SG_DYN=1 -> dynamic (non-constant) view/proj avals, exercising the path for
        // dynamic constituents (an adaptive RegionWriter per region, as a free-flying
        // camera needs) instead of the constant path (packed once via StageOnce).
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
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()

        let report (label : string) (classicPix : PixImage<uint8>) (heapPix : PixImage<uint8>) (buckets : int) =
            let maxDelta, nDiff, nNonBg, total = diff classicPix heapPix
            Log.line "sgheap[%s]: -> %d bucket(s)  maxChannelDelta=%d  diffPixels=%d/%d  coverage=%d px"
                label buckets maxDelta nDiff total nNonBg
            let pass = maxDelta <= 1 && nNonBg > total / 100L && buckets = 1
            if pass then Log.line "sgheap[%s]: PASS" label
            else Log.warn "sgheap[%s]: FAIL (maxDelta=%d buckets=%d nNonBg=%d)" label maxDelta buckets nNonBg
            pass

        let classicPix = renderToPix (scene id)

        // the direct render path (the default CompileRender entry)
        use storage = runtime.CreateHeapStorage()
        let heapTsPix = renderToPix (scene (Sg.heap storage))
        let bucketsTs = Heap.lastBucketCount
        let passTs = report "ts" classicPix heapTsPix bucketsTs

        // the legacy attribute-grammar path (EnvVar.AARDVARK_LEGACY_SG)
        EnvVar.AARDVARK_LEGACY_SG <- "1"
        let heapAgPix =
            use storage = runtime.CreateHeapStorage()
            try renderToPix (scene (Sg.heap storage))
            finally EnvVar.AARDVARK_LEGACY_SG <- null
        let bucketsAg = Heap.lastBucketCount
        let passAg = report "ag" classicPix heapAgPix bucketsAg

        // Mixed-type colors -> one bucket
        // the Colors attribute arrives in four different shapes: the default box
        // buffer (C4b[], solidBox's own colors), a C4b singleton, a real C4f[]
        // buffer and a C4f singleton. Element types are not part of the bucket
        // key: the decoder branches on each allocation's header typeId at fetch
        // time (C4b is normalized by /255 from its BGRA memory layout, singletons
        // broadcast their single element) and converts to the shader's V4f input,
        // so all leaves share one bucket; the image must equal the classic
        // per-object render.
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
        use storage = runtime.CreateHeapStorage()
        let heap2 = renderToPix (scene2 (Sg.heap storage))
        let passMixed = report "mixed-types" classic2 heap2 Heap.lastBucketCount

        let pass = passTs && passAg && passMixed
        if pass then Log.line "sgheap: ALL PASS (Sg.heap == classic, %d leaves -> 1 bucket, both dispatch paths; mixed C4b/C4f singleton+buffer colors -> 1 bucket)" (s * s)
        else Log.warn "sgheap: FAILED (ts=%b ag=%b mixed=%b)" passTs passAg passMixed
        pass

    // Offscreen reproduction of the windowed HeapSpike example (2 effects -> 2
    // buckets, mixed box/sphere/torus geometry), classic vs heap, saved as PPM files
    // in the temp directory for visual inspection (e.g. of platform-specific
    // misrenders). Single-sample renders are downloadable directly; the 8x MSAA
    // render is resolved first. This probe only logs the diffs and always passes.
    let demoShotTest (runtime : IRuntime) =
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
            use sgn = runtime.CreateFramebufferSignature([ DefaultSemantic.Colors, TextureFormat.Rgba8; DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ], samples)
            use task = runtime.CompileRender(sgn, objs)
            let out = task |> RenderTask.renderToColor size
            use _ = useResource out
            let tex = out.GetValue()
            if samples > 1 then
                let resolved = runtime.CreateTexture2D(V2i(1024, 1024), TextureFormat.Rgba8, 1, 1)
                runtime.ResolveMultisamples(tex.GetOutputView(), resolved)
                let img = runtime.Download(resolved).AsPixImage<uint8>()
                runtime.DeleteTexture resolved
                img
            else tex.Download().AsPixImage<uint8>()
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
        use signature = runtime.CreateFramebufferSignature [DefaultSemantic.Colors, TextureFormat.Rgba8]
        use storage = runtime.CreateHeapStorage()
        let heapObjs = Heap.ofRenderObjects storage (ASet.ofArray inputs)
        let classic = renderAt 1 (ASet.ofArray inputs)
        let heap1   = renderAt 1 heapObjs
        let heap8   = renderAt 8 heapObjs        // the windowed HeapSpike example uses samples=8
        savePpm (System.IO.Path.Combine(System.IO.Path.GetTempPath(), "demo_classic.ppm")) classic
        savePpm (System.IO.Path.Combine(System.IO.Path.GetTempPath(), "demo_heap.ppm")) heap1
        savePpm (System.IO.Path.Combine(System.IO.Path.GetTempPath(), "demo_heap_msaa.ppm")) heap8
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

    // Precision guard: the heap derives ModelViewProjTrafo in fp64, so the same scene
    // rendered at the origin and at geodetic scale (earth-radius coordinates) must look
    // identical (within one channel step): the huge View/Model translations cancel in
    // double precision. If the derive ever silently drops to float32, the geodetic
    // cubes jitter and the two images diverge, which this probe detects. A constant
    // f32-premultiplied MVP is also rendered, only to assert that the scale is high
    // enough for f32 to genuinely break (more than 1% of the pixels differ), since a
    // pass would otherwise be meaningless.
    let sgPrecisionTest (runtime : IRuntime) =
        use signature =
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
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()
        let effFp64 = [ Effect.ofFunction DF.shadeFp64; Effect.ofFunction DF.frag ]
        // fp64: per-object ModelTrafo; the effect reads the composed ModelViewProjTrafo ->
        // the heap derives it (Proj*View*Model) in double.
        use storage = runtime.CreateHeapStorage()
        let heapFp64 (c : V3d) =
            offsets |> Array.map (fun o -> boxSg |> Sg.trafo' (Trafo3d.Translation(c + o)))
            |> Sg.ofArray |> Sg.heap storage |> Sg.effect effFp64
            |> Sg.viewTrafo (AVal.constant (viewAt c)) |> Sg.projTrafo (AVal.constant proj)
        // f32 reference: supply ModelViewProjTrafo as a constant pre-multiplied f32
        // matrix (no derive) -> the heap just gathers it; jitters at geodetic scale.
        use storage = runtime.CreateHeapStorage()
        let heapF32 (c : V3d) =
            let vpF32 = M44f.op_Explicit ((viewAt c) * proj).Forward
            offsets |> Array.map (fun o ->
                let mF32 = M44f.op_Explicit (Trafo3d.Translation(c + o)).Forward
                boxSg |> Sg.uniform' "ModelViewProjTrafo" (vpF32 * mF32) |> Sg.uniform' "NormalMatrix" M44f.Identity)
            |> Sg.ofArray |> Sg.heap storage |> Sg.effect effFp64
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
            Log.warn "sgprec: FAIL (scaleInvariant=%b f32Broke=%b): heap derive may have dropped to float32" scaleInvariant f32Broke; false

    // Live GPU trafo chain through Heap.ofRenderObjects (the real incremental bucket
    // ingest). Each render object exposes a depth-2 model stack [nodeTrafo; boxLink]
    // as the well-known "ModelTrafoStack" uniform and the CPU-folded "ModelTrafo",
    // so the same input set renders two ways:
    //   * folded: render objects without the stack -> ModelTrafo packed as an arena
    //             region.
    //   * chain : render objects with the stack -> chain-mode bucket -> ModelTrafo
    //             folded on the GPU from the value-deduplicated link arena.
    // The box link is non-identity (a real Scale*Translation) so the compose order
    // is actually exercised (an identity link cannot catch a reversed multiply).
    // Asserts, on the live path:
    //   (a) chain mode engaged (lastChainBuckets = 1 with the stack, 0 without),
    //   (b) the box link value-deduplicates to one slot across all leaves
    //       (distinct links <= n + 2),
    //   (c) the GPU chain image matches the CPU-folded image within one channel step,
    //   (d) editing one node link uploads exactly one link (lastChainLinkUploads = 1),
    //   (e) add/remove churn keeps chain mode (free-list reuse) and coverage is sane.
    let liveChainTest (runtime : IRuntime) =
        use signature =
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

        // non-identity box link, identical value across leaves but a distinct
        // AVal.constant per leaf (as a scene graph that builds a box per leaf does).
        let boxValue = Trafo3d.Scale(0.8, 0.8, 1.4) * Trafo3d.Translation(0.1, 0.1, 0.0)
        let boxLink () : aval<Trafo3d> = AVal.constant boxValue
        let nodeTrafos =
            Array.init n (fun i ->
                AVal.init (Trafo3d.Translation(float (i % nSide) * 1.2, float (i / nSide) * 1.2, 0.0)))
        // the heap folds a stack as ModelTrafo = arr[0] * arr[1] * ... (Trafo3d `*`,
        // the same fold the scene graph's trafo stack uses). With [nodeTrafo; boxLink]
        // that is ModelTrafo = node * box, and the CPU-folded value below is exactly that.
        let stackOf i : aval<Trafo3d>[] = [| (nodeTrafos.[i] :> aval<Trafo3d>); boxLink () |]
        let foldedOf i = AVal.map2 (*) (nodeTrafos.[i] :> aval<Trafo3d>) (boxLink ())   // node*box

        let mkRO (withStack : bool) i =
            let folded = foldedOf i
            let nm = folded |> AVal.map (fun (t : Trafo3d) -> M44f.op_Explicit (M44d (M33d t.Backward.Transposed)))
            // provide the View/Proj constituents (the heap derives ModelViewProjTrafo
            // and NormalMatrix on the GPU); the supplied NormalMatrix is ignored, but
            // kept to prove the derived result matches (Model^-1)^T.
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
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()

        let foldedInputs = Array.init n (mkRO false)
        let chainInputs  = Array.init n (mkRO true)
        use storage = runtime.CreateHeapStorage()
        let foldedPix = imageOf (Heap.ofRenderObjects storage (ASet.ofArray foldedInputs))
        let foldChainBuckets = Heap.lastChainBuckets        // expect 0 (no stack)
        let chainSet = cset (chainInputs :> seq<_>)
        use storage = runtime.CreateHeapStorage()
        let chainHeap = Heap.ofRenderObjects storage (chainSet :> aset<_>)
        // render chainHeap through one persistent task kept alive across the edit and
        // churn, so the heap stays built and an edit is an incremental upload (a
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

        // (d) edit one node link -> one link uploaded
        transact (fun () -> nodeTrafos.[n/2].Value <- Trafo3d.Translation(99.0, 99.0, 5.0))
        renderChain () |> ignore
        let uploads = Heap.lastChainLinkUploads

        // (e) churn: remove 3, add 3 fresh -> chain mode preserved, image sane
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

    // Per-frame resource-leak regression
    // The `lifetime` test checks that device memory returns to baseline after each
    // scene is torn down. It does not catch a leak that accumulates per frame inside
    // one long-lived scene, which is what brings down large heap scenes after a few
    // thousand frames.
    //
    // This probe builds one long-lived heap scene (chain mode engaged) and renders it
    // for FRAMES frames, editing it each frame (default: a single-link trafo value
    // edit, the pure value-edit case; CHURN=1 instead removes and adds one member each
    // frame). It samples three leak metrics every SAMPLE frames:
    //   * device memory allocations (Device.GetMemoryStatistics): buffers / images,
    //   * live Vulkan descriptor sets (DescriptorSet.LiveCount): set machinery,
    //   * live backend Resource handles (Resource.LiveCount): every Resource<'T>
    //     (buffers, images, views, samplers, query pools, uniform buffers, ...).
    // Pass = all three metrics stay bounded (at most 64 above the post-warm-up
    // baseline) over all frames. A per-frame acquire-without-release shows as
    // monotonic growth and fails here while the per-scene `lifetime` test stays green
    // (its teardown hides the accumulation). Knobs: FRAMES (4000), NSIDE (30 -> 900
    // render objects), SAMPLE (250), CHURN=1 (membership churn instead of value edit),
    // BINDLESS=1 (GPU-resident per-slot geometry -> vertex-pull gather path; keep
    // NSIDE small, every slot's attribute buffers count against the unbounded
    // storage-buffer array capacity, RuntimeConfig.UnboundedArrayCapacity).
    let chainLeakProbeTest (runtime : IRuntime) =
        let device = (runtime :?> Aardvark.Rendering.Vulkan.Runtime).Device   // Vulkan-only probe: device statistics
        let frames = match System.Environment.GetEnvironmentVariable "FRAMES" with null | "" -> 4000 | s -> int s
        let sample = match System.Environment.GetEnvironmentVariable "SAMPLE" with null | "" -> 250  | s -> int s
        let nSide  = match System.Environment.GetEnvironmentVariable "NSIDE"  with null | "" -> 30   | s -> int s
        use signature =
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
        // BINDLESS=1 -> each render object gets distinct per-slot vertex source buffers
        // (not the shared constant geometry), forcing the bindless vertex-pull gather path.
        let bindless = match System.Environment.GetEnvironmentVariable "BINDLESS" with "1" -> true | _ -> false
        let gbv (b : IBackendBuffer) t = BufferView(AVal.constant (b :> IBuffer), t)
        // fixed pool of GPU-resident per-slot vertex buffers, created once. Churn
        // reuses these (so the probe allocates zero GPU buffers per edit); any
        // per-frame memory/resource growth is then attributable to the heap, not the
        // probe. The pool is large enough that adjacent churn picks distinct buffers.
        let nbv = nSide * nSide
        // the GPU buffers behind the pool, released when the probe ends
        let bindlessBuffers = System.Collections.Generic.List<IBackendBuffer>()
        let bindlessPool =
            if not bindless then [||]
            else
                Array.init (max 64 (nbv * 2)) (fun k ->
                    let p2 = positions |> Array.map (fun (v : V3f) -> v + V3f(float32 k * 1.0e-4f, 0.0f, 0.0f))
                    let pb = runtime.PrepareBuffer(ArrayBuffer(p2))
                    let nb = runtime.PrepareBuffer(ArrayBuffer(normals))
                    bindlessBuffers.Add pb
                    bindlessBuffers.Add nb
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
        // CHURN=1 -> each frame removes one member and adds a fresh render object
        // (exercises bucket slot recycling, chain-link interning/freeing and the
        // membership delta machinery, none of which the pure trafo loop touches).
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
        use storage = runtime.CreateHeapStorage()
        let heap = Heap.ofRenderObjects storage (memberSet :> aset<_>)
        use task = runtime.CompileRender(signature, heap)
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        let rnd = System.Random(1234)
        let liveDs () = Aardvark.Rendering.Vulkan.DescriptorSet.LiveCount
        let liveRes () = Aardvark.Rendering.Vulkan.Resource.LiveCount
        let memCount () = device.GetMemoryStatistics() |> Array.sumBy (fun h -> int h.Allocations)
        // warm-up: the first renders create all caches/pipelines/atlas; baseline after.
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
                // pure value edit of one link per frame: stable membership; the re-render
                // goes through the per-frame chain-link upload path.
                let i = f % n
                transact (fun () -> nodeTrafos.[i].Value <- Trafo3d.Translation(float (i % nSide) * 1.2, float (i / nSide) * 1.2, float f * 1.0e-4))
            out.GetValue() |> ignore
            let m, d, r = memCount (), liveDs (), liveRes ()
            maxMem <- max maxMem m
            maxDs  <- max maxDs d
            maxRes <- max maxRes r
            if f % sample = 0 || f = frames then
                Log.line "chainLeakProbe: frame %5d  VMA-allocs=%d (%+d)  liveDescriptorSets=%d (%+d)  liveResources=%d (%+d)" f m (m - mem0) d (d - ds0) r (r - res0)
        out.Release()
        for b in bindlessBuffers do b.Dispose()
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
        else Log.warn "chainLeakProbe: FAIL (VMA growth %+d, descriptorSet growth %+d, resource growth %+d [ok<=64] over %d frames: per-frame leak)" memGrowth dsGrowth resGrowth frames
        pass

    // Host-box soak (regression for a crash after thousands of edited frames)
    // N boxes sharing one ArrayBuffer box geometry (host-tight -> the host-packed
    // arena path, not bindless vertex-pull), HeapModelTrafo + HeapColor per-draw
    // uniforms, no ModelTrafoStack (no chain). Renders FRAMES frames; each frame
    // edits EDITK random nodes' trafo (KIND=trafo, the default) or color (KIND=color)
    // in one transaction, and samples the same three leak metrics as the chain leak
    // probe. Pass = runs to completion without an exception and the metrics stay
    // within 64 of the baseline. The suite runs a short version (N=20000, FRAMES=300,
    // EDITK=5000); the full soak is N=100000 FRAMES=8000 EDITK=55184, configurable
    // through the environment variables N, FRAMES, EDITK, KIND (trafo|color) and SAMPLE.
    let hostBoxCrashTest (runtime : IRuntime) =
        let device = (runtime :?> Aardvark.Rendering.Vulkan.Runtime).Device   // Vulkan-only probe: device statistics
        let n      = match System.Environment.GetEnvironmentVariable "N"      with null | "" -> 20000  | s -> int s
        let frames = match System.Environment.GetEnvironmentVariable "FRAMES" with null | "" -> 300    | s -> int s
        let editK  = match System.Environment.GetEnvironmentVariable "EDITK"  with null | "" -> 5000   | s -> int s
        let kind   = match System.Environment.GetEnvironmentVariable "KIND"   with null | "" -> "trafo"| s -> s
        let sample = match System.Environment.GetEnvironmentVariable "SAMPLE" with null | "" -> 50     | s -> int s
        use signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let g = (IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        // one shared host-tight attribute provider + index view: the host-packed path.
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
        use storage = runtime.CreateHeapStorage()
        let heap = Heap.ofRenderObjects storage (ASet.ofArray ros)
        use task = runtime.CompileRender(signature, heap)
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        let liveDs () = Aardvark.Rendering.Vulkan.DescriptorSet.LiveCount
        let liveRes () = Aardvark.Rendering.Vulkan.Resource.LiveCount
        let memCount () = device.GetMemoryStatistics() |> Array.sumBy (fun h -> int h.Allocations)
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
                Log.line "hostBoxCrash: frame %5d (cumEdits=%d)  VMA=%d (%+d)  descSets=%d (%+d)  resources=%d (%+d)  %.1fs" f (f * editK) m (m-mem0) d (d-ds0) r (r-res0) sw.Elapsed.TotalSeconds
        out.Release()
        let memGrowth, dsGrowth, resGrowth = maxMem - mem0, maxDs - ds0, maxRes - res0
        Log.line "hostBoxCrash: SURVIVED %d frames (%d cumulative edits). peak growth: VMA=%+d descSets=%+d resources=%+d" frames (frames * editK) memGrowth dsGrowth resGrowth
        let pass = buckets >= 1 && memGrowth <= 64 && dsGrowth <= 64 && resGrowth <= 64
        if pass then Log.line "hostBoxCrash: PASS (host-box scene runs past old crash point, resources bounded)"
        else Log.warn "hostBoxCrash: FAIL (VMA %+d descSets %+d resources %+d over %d frames)" memGrowth dsGrowth resGrowth frames
        pass

    // Large bindless bucket (regression for an abort in the descriptor write)
    // A bindless vertex-pull bucket binds one storage buffer per slot and attribute
    // in an unbounded storage-buffer array whose capacity is
    // RuntimeConfig.UnboundedArrayCapacity (clamped to the device's per-stage
    // limit; IRuntime.MaxUnboundedStorageBufferArrayElements). The heap must never
    // fill a bucket past that capacity: members that do not fit go to an overflow
    // bucket of the same key. The probe lowers the capacity to 1024 for its own
    // runtime, adds 650 render objects with GPU-resident per-slot geometry x 2
    // attributes = 1300 buffers, and asserts that the render completes, that the heap
    // split them into two buckets (512 slots fill the first) and that memory stays
    // bounded (at most 8 new allocations over 10 further frames). N is configurable.
    let bindlessOverCapacityTest (runtime : IRuntime) =
        let device = (runtime :?> Aardvark.Rendering.Vulkan.Runtime).Device   // Vulkan-only probe: device statistics
        let n = match System.Environment.GetEnvironmentVariable "N" with null | "" -> 650 | s -> int s
        let capacity = 1024
        let configured = Aardvark.Rendering.Vulkan.RuntimeConfig.UnboundedArrayCapacity
        Aardvark.Rendering.Vulkan.RuntimeConfig.UnboundedArrayCapacity <- capacity
        try
            use signature =
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
            // distinct GPU-resident per-slot vertex buffers -> bindless vertex-pull with
            // n*2 array elements. The buffers are released when the probe ends.
            let buffers = System.Collections.Generic.List<IBackendBuffer>()
            let mkRO i =
                let p2 = positions |> Array.map (fun (v : V3f) -> v + V3f(float32 i * 1.0e-4f, 0.0f, 0.0f))
                let pb = runtime.PrepareBuffer(ArrayBuffer(p2))
                let nb = runtime.PrepareBuffer(ArrayBuffer(normals))
                buffers.Add pb
                buffers.Add nb
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
            use storage = runtime.CreateHeapStorage()
            let heap = Heap.ofRenderObjects storage (ASet.ofArray ros)
            use task = runtime.CompileRender(signature, heap)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            let memCount () = device.GetMemoryStatistics() |> Array.sumBy (fun h -> int h.Allocations)
            let expectedBuckets = (n * 2 + capacity - 1) / capacity
            Log.line "bindlessOverCapacity: n=%d (slots*attrs=%d, capacity=%d): expect %d buckets" n (n * 2) capacity expectedBuckets
            let error =
                try
                    for _ in 0 .. 4 do out.GetValue() |> ignore
                    None
                with e ->
                    Some e
            let buckets = Heap.lastBucketCount
            let mem1 = memCount ()
            if error.IsNone then for _ in 0 .. 9 do out.GetValue() |> ignore
            let memGrowth = memCount () - mem1
            out.Release()
            for b in buffers do b.Dispose()
            let pass = error.IsNone && buckets = expectedBuckets && memGrowth <= 8
            match error with
            | Some e -> Log.warn "bindlessOverCapacity: FAIL (render threw %s: %s)" (e.GetType().Name) e.Message
            | None when pass -> Log.line "bindlessOverCapacity: PASS (%d buckets, resources bounded)" buckets
            | None -> Log.warn "bindlessOverCapacity: FAIL (buckets=%d expected %d, memGrowth=%+d)" buckets expectedBuckets memGrowth
            pass
        finally
            Aardvark.Rendering.Vulkan.RuntimeConfig.UnboundedArrayCapacity <- configured

    // Arbitrary-depth live chain through Heap.ofRenderObjects. Each leaf carries a
    // stack of variable length (3 to 6 links) mixing:
    //   * a dynamic root cval shared per group (moving a group is an O(1) link edit
    //     shared by every leaf under it, the general hierarchy case),
    //   * 1 to 4 dynamic per-leaf mid links (distinct cvals),
    //   * a constant leaf link (value-dedup target: identical-valued leaf links
    //     collapse).
    // Stacks of different length still share one bucket (chain structure is per-slot
    // data, not part of the effect/layout). Asserts that the implementation is not
    // limited to depth 2:
    //   (a) the GPU compose of arbitrary-length chains matches the CPU fold within
    //       one channel step,
    //   (b) editing one shared group root uploads exactly one link (O(1) over the
    //       subtree),
    //   (c) chain mode is engaged (one chain bucket) and coverage is sane.
    // The distinct-link count (value-dedup of the constant leaf links) is logged only.
    let liveChainDeepTest (runtime : IRuntime) =
        use signature =
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

        // one dynamic shared root per group (root-most link, shared across leaves).
        let roots = Array.init groups (fun _ -> AVal.init Trafo3d.Identity)
        // a small set of distinct constant leaf-link values (value-dedup target).
        let leafConsts = [| Trafo3d.Scale 0.9; Trafo3d.Scale 1.1; Trafo3d.RotationZ 0.3 |]
        // per-leaf variable-depth stack [root; mid0; mid1; ...; leafConst] in
        // root->leaf order; ModelTrafo (CPU fold) = root * mid0 * ... * leafConst.
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

        // CPU fold of a stack array: arr[0]*arr[1]*...*arr[last].
        let foldOf (arr : aval<Trafo3d>[]) =
            arr |> Array.reduce (fun a b -> AVal.map2 (*) a b)

        let mkRO (withStack : bool) i =
            let folded = foldOf stacks.[i]
            let nm = folded |> AVal.map (fun (t : Trafo3d) -> M44f.op_Explicit (M44d (M33d t.Backward.Transposed)))
            // provide the View/Proj constituents (the heap derives ModelViewProjTrafo
            // and NormalMatrix on the GPU); the supplied NormalMatrix is ignored, but
            // kept to prove the derived result matches (Model^-1)^T.
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
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()

        use storage = runtime.CreateHeapStorage()
        let foldedPix = imageOf (Heap.ofRenderObjects storage (ASet.ofArray (Array.init n (mkRO false))))
        use storage = runtime.CreateHeapStorage()
        let chainHeap = Heap.ofRenderObjects storage (ASet.ofArray (Array.init n (mkRO true)))
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

        // edit one shared group root -> one link upload (moves perGroup leaves).
        transact (fun () -> roots.[2].Value <- Trafo3d.Translation(0.0, 0.0, 6.0))
        renderChain () |> ignore
        chainOut.Release()
        let uploads = Heap.lastChainLinkUploads

        // distinct links = group roots + all mid links (distinct cvals) + at most 3
        // leaf constant values; the count is logged above but not asserted.
        let engaged = chainBuckets = 1
        let correct = nDiff = 0L || maxD <= 1
        let editOk  = uploads = 1
        let coverOk = nbg > total / 100L
        let pass = engaged && correct && editOk && coverOk
        if pass then Log.line "liveChainDeep: PASS (arbitrary-depth chains GPU-fold == CPU fold; shared root edit = 1 link)"
        else Log.warn "liveChainDeep: FAIL (engaged=%b correct=%b edit=%b cover=%b)" engaged correct editOk coverOk
        pass

    // Scene-graph chain end-to-end: a scene built entirely with ordinary Sg
    // combinators (nested Sg.trafo' inner + Sg.trafo dynamic outer per leaf, a real
    // ModelViewProjTrafo effect) rendered through Sg.heap on the direct render path
    // (the default CompileRender entry). The direct path's uniform provider exposes
    // "ModelTrafoStack" alongside the folded ModelTrafo, so the heap engages chain
    // mode. Asserts: (a) the chain render matches the same heap scene with chains
    // disabled (EnvVar.AARDVARK_HEAP_NOCHAIN) within one channel step, (b) chain mode
    // actually engaged (one chain bucket; none with chains disabled), (c) the inner
    // constant link value-deduplicates to one slot, (d) the chain render roughly
    // matches the classic per-object render (fewer than 0.1% differing pixels).
    let sgChainTest (runtime : IRuntime) =
        use signature =
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
        // inner constant link (a real non-identity Scale*Translation, the "box"
        // half) shared by value across leaves; outer dynamic node link per leaf.
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
            // shadeMvp reads only ModelViewProjTrafo (-> ModelTrafo, which the direct
            // render path's provider exposes); NormalMatrix is backend-derived and not
            // a per-object heap field on this path, so the effect avoids it.
            |> Sg.effect [ Effect.ofFunction DF.shadeMvp; Effect.ofFunction DF.frag ]
            |> Sg.viewTrafo (AVal.constant view)
            |> Sg.projTrafo (AVal.constant proj)
        let renderToPix (sg : ISg) =
            use task = runtime.CompileRender(signature, sg)
            let out = task |> RenderTask.renderToColor size
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()

        // chain heap (default) vs folded heap (same MVP shader, only the ModelTrafo
        // source differs: GPU chain compose vs CPU-folded arena region). This isolates
        // the fold from the rasterization path, so the images must agree within one step.
        use storage = runtime.CreateHeapStorage()
        let heapChainPix = renderToPix (scene (Sg.heap storage))
        let chainBuckets = Heap.lastChainBuckets
        let distinct = Heap.lastDistinctLinks
        EnvVar.AARDVARK_HEAP_NOCHAIN <- "1"
        let heapFoldPix =
            use storage = runtime.CreateHeapStorage()
            try renderToPix (scene (Sg.heap storage))
            finally EnvVar.AARDVARK_HEAP_NOCHAIN <- null
        let foldChainBuckets = Heap.lastChainBuckets
        // and a loose sanity check against the classic per-object (non-heap) render.
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

    // Graceful fallback: a mixed aset of heapable and un-heapable render objects.
    // Heapable ones collapse to buckets; un-heapable ones (here: a non-indexed
    // multi-call draw; single-call non-indexed draws are heapable) must be passed
    // through unchanged (same instance) in the output, not dropped or crashed.
    let passthroughTest (runtime : IRuntime) =
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.5)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let eff = Effect.compose [ Effect.ofFunction DF.shadeMvp; Effect.ofFunction DF.frag ]
        let vp = AVal.constant Trafo3d.Identity :> IAdaptiveValue

        // 4 heapable render objects (same effect/geometry/state -> one bucket)
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
        // 1 un-heapable render object: non-indexed with multiple draw calls
        // (single-call non-indexed draws are heapable) -> not eligible -> pass through
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
        // a second un-heapable render object: positions in a non-decodable element
        // type (uint16 scalars; double sources like V3d are storage-decodable) ->
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
        // a heapable render object supplying a consumed uniform in an unpackable type:
        // it stays heapable (the uniform falls through as a shared global) but
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

        use signature = runtime.CreateFramebufferSignature [DefaultSemantic.Colors, TextureFormat.Rgba8]
        let input = ASet.ofArray (Array.concat [ heapable; [| odd; odd2; oddUniform |] ])
        use storage = runtime.CreateHeapStorage()
        let outSet = Heap.ofRenderObjects storage input
        // signature-deferred: expand the signature-dependent render objects against the
        // signature (as a render compile would)
        let expanded = SignatureDependentRenderObject.expand signature outSet
        Heap.Diagnostics <- true
        let out =
            try expanded |> ASet.toAVal |> AVal.force |> HashSet.toArray
            finally Heap.Diagnostics <- false
        // a render task holds the heap's activation while it draws; this probe never
        // renders, so take and drop the activation itself: the last drop frees the
        // buckets, and disposing the storage frees the pages
        let activations = out |> Array.choose (function :? ActivationRenderObject as a -> Some (a.Activate()) | _ -> None)
        let buckets = Heap.lastBucketCount
        let passedThrough = out |> Array.exists (fun o -> System.Object.ReferenceEquals(o, odd))
        let passedThrough2 = out |> Array.exists (fun o -> System.Object.ReferenceEquals(o, odd2))
        // oddUniform stays heapable (its unpackable ModelTrafo just falls through as a
        // global and emits the "UNPACKABLE" diagnostic), so it is collapsed into its own
        // bucket, not passed through as a standalone render object.
        let oddUniformHeaped = not (out |> Array.exists (fun o -> System.Object.ReferenceEquals(o, oddUniform)))
        let msgs = Heap.diagnosticMessages ()
        for m in msgs do Log.line "passthrough: diag: %s" m
        let diagOk =
            msgs |> Array.exists (fun m -> m.Contains "multiple draw calls") &&
            msgs |> Array.exists (fun m -> m.Contains "storage-decoded") &&
            msgs |> Array.exists (fun m -> m.Contains "UNPACKABLE")
        Log.line "passthrough: in=7 (4 heapable + 2 odd + 1 unpackable-uniform) -> out=%d buckets=%d oddPassedThrough=%b/%b oddUniformHeaped=%b diags=%d" out.Length buckets passedThrough passedThrough2 oddUniformHeaped msgs.Length
        // out = 2 buckets + 2 passthroughs (odd, odd2) + 1 derive pre-pass render object.
        // The heapable bucket consumes ModelViewProjTrafo (shadeMvp), derived as
        // ModelTrafo x ViewProjTrafo, so it emits a draw-less derive render object;
        // oddUniform's bucket cannot derive it (unpackable ModelTrafo falls through as a
        // global), so it emits none. Hence buckets + 2 + 1 = 5.
        let pass = buckets = 2 && passedThrough && passedThrough2 && oddUniformHeaped && out.Length = buckets + 3 && diagOk
        for a in activations do a.Dispose()
        if pass then Log.line "passthrough: PASS (heapable collapsed; un-heapable ROs passed through unchanged; Diagnostics emitted deduped reasons)"
        else Log.warn "passthrough: FAIL (out=%d buckets=%d passedThrough=%b/%b oddUniformHeaped=%b)" out.Length buckets passedThrough passedThrough2 oddUniformHeaped
        pass

    // NativeMemoryBuffer geometry is heap-eligible: a user-supplied native buffer
    // is copied into the packed arena exactly like an ArrayBuffer. Render 25 cubes
    // with native-buffer geometry through the heap and compare to the ArrayBuffer
    // path: one bucket, images equal within one channel step.
    let nativeBufTest (runtime : IRuntime) =
        use signature =
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
            use storage = runtime.CreateHeapStorage()
            let heap = Heap.ofRenderObjects storage (ASet.ofArray objs)
            use task = Sg.renderObjectSet heap |> Sg.compile runtime signature
            let out = task |> RenderTask.renderToColor size
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()

        let arrPix = imageOf (mkObjs (abv positions typeof<V3f>) (abv normals typeof<V3f>) (abv index typeof<int>))
        let bucketsArr = Heap.lastBucketCount
        let natPix = imageOf (mkObjs (nbvV3f positions) (nbvV3f normals) (nbvInt index))
        let bucketsNat = Heap.lastBucketCount
        for p in allocs do System.Runtime.InteropServices.Marshal.FreeHGlobal p

        let maxD, nDiff, nNonBg, total = diff arrPix natPix
        Log.line "nativeBuf: bucketsArr=%d bucketsNat=%d  array-vs-native maxDelta=%d diffPixels=%d/%d coverage=%d" bucketsArr bucketsNat maxD nDiff total nNonBg
        let pass = bucketsNat = 1 && nNonBg > 1000L && maxD <= 1
        if pass then Log.line "nativeBuf: PASS (NativeMemoryBuffer geometry heaped -> 1 bucket, identical to ArrayBuffer)"
        else Log.warn "nativeBuf: FAIL (bucketsNat=%d maxDelta=%d coverage=%d)" bucketsNat maxD nNonBg
        pass

    // Type-agnostic geometry: V4f positions + uint16 indices instead of the common
    // V3f/int. The heap render must equal the classic per-object render.
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

    let varTypeTest (runtime : IRuntime) =
        use signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(640, 640))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let pos3 = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals = g.IndexedAttributes.[DefaultSemantic.Normals] |> unbox<V3f[]>
        let idx32 = g.IndexArray |> unbox<int[]>
        // non-default types: V4f positions, uint16 indices
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
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()
        let classicPix = imageOf (ASet.ofArray objs)
        use storage = runtime.CreateHeapStorage()
        let heapPix = imageOf (Heap.ofRenderObjects storage (ASet.ofArray objs))
        let buckets = Heap.lastBucketCount
        let maxD, nDiff, nNonBg, total = diff classicPix heapPix
        Log.line "varType: V4f pos + uint16 idx  buckets=%d classic-vs-heap maxDelta=%d diffPixels=%d/%d coverage=%d" buckets maxD nDiff total nNonBg
        let pass = buckets = 1 && nNonBg > 1000L && maxD <= 1

        // Double sources and per-allocation source types: half the render objects
        // supply Positions as V3d doubles (bit-decoded in the shader, w = 1 fill),
        // half as the V4f above. Source element types are not part of the bucket
        // key (the decode branches on each allocation's header typeId), so the
        // mixed set collapses to one bucket, and since the V3d values hold exactly
        // the V3f positions, the image must equal the all-V4f classic render.
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
        use storage = runtime.CreateHeapStorage()
        let heapMixedPix = imageOf (Heap.ofRenderObjects storage (ASet.ofArray mixed))
        let bucketsM = Heap.lastBucketCount
        let maxDM, nDiffM, _, totalM = diff classicPix heapMixedPix
        Log.line "varType: mixed V3d/V4f pos  buckets=%d vs-classic maxDelta=%d diffPixels=%d/%d" bucketsM maxDM nDiffM totalM
        let passMixed = bucketsM = 1 && maxDM <= 1
        if not passMixed then Log.warn "varType: f64 phase FAIL (buckets=%d maxDelta=%d)" bucketsM maxDM

        let pass = pass && passMixed
        if pass then Log.line "varType: PASS (V4f positions + uint16 indices heaped == classic; mixed V3d/V4f sources -> 1 bucket, f64 decode == classic)"
        else Log.warn "varType: FAIL (buckets=%d maxDelta=%d coverage=%d passMixed=%b)" buckets maxD nNonBg passMixed
        pass

    // Bindless storage-buffer array end-to-end: a shader pulls its position from one
    // of many GPU buffers chosen by a handle (Geom[handle].data[gl_VertexIndex]), with
    // no vertex-input attributes at all. Validates the whole Vulkan chain (FShade
    // ssbCount=-1 -> unbounded SSBO descriptor array -> array binding/write).
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

    let ssboArrayTest (runtime : IRuntime) =
        use signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(256, 256))
        let tri = [| V4f(-0.6f, -0.6f, 0.0f, 1.0f); V4f(0.6f, -0.6f, 0.0f, 1.0f); V4f(0.0f, 0.7f, 0.0f, 1.0f) |]
        let quad = [| V4f(-0.9f, 0.6f, 0.0f, 1.0f); V4f(-0.6f, 0.6f, 0.0f, 1.0f); V4f(-0.75f, 0.9f, 0.0f, 1.0f) |]
        // an array of two distinct GPU storage buffers
        let geom : IBuffer[] = [| ArrayBuffer(tri) :> IBuffer; ArrayBuffer(quad) :> IBuffer |]
        let eff = Effect.compose [ Effect.ofFunction SA.shade; Effect.ofFunction SA.frag ]
        let mk (handle : int) =
            let ro = RenderObject()
            ro.Surface <- Surface.Effect eff
            ro.Mode <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- AttributeProvider.ofList ([] : list<Symbol * BufferView>)   // no vertex attributes: pure pull
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = 3, InstanceCount = 1) |])
            ro.Uniforms <- UniformProvider.ofList [
                Symbol.Create "Geom",   (AVal.constant geom :> IAdaptiveValue)
                Symbol.Create "Handle", (AVal.constant handle :> IAdaptiveValue) ]
            ro :> IRenderObject
        use task = Sg.renderObjectSet (ASet.ofList [ mk 0; mk 1 ]) |> Sg.compile runtime signature
        let out = task |> RenderTask.renderToColor size
        use _ = useResource out
        let pix = out.GetValue().Download().AsPixImage<uint8>()
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

    // Builds on ssboArray with exactly one change: two SSBO arrays (positions in
    // GeomA, colors in GeomB) instead of one. Still a per-object uniform handle and
    // separate draws. Isolates the multi-array descriptor bind/write path: GeomA gives
    // geometry, GeomB gives color; if the second array mis-binds, the color is
    // wrong or black.
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


    let ssboArray2Test (runtime : IRuntime) =
        use signature =
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
        use _ = useResource out
        let pix = out.GetValue().Download().AsPixImage<uint8>()
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
        else Log.warn "ssboArray2: FAIL (green=%d red=%d; a second SSBO array mis-binds)" green redc
        pass

    // Builds on ssboArray2 with one change: the handle now comes from gl_InstanceIndex
    // (routed by the per-object FirstInstance) instead of a uniform. Still separate
    // draws. Isolates gl_InstanceIndex + firstInstance routing before any multidraw
    // is involved.
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

    let ssboArray3Test (runtime : IRuntime) =
        use signature =
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
        use _ = useResource out
        let pix = out.GetValue().Download().AsPixImage<uint8>()
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
        else Log.warn "ssboArray3: FAIL (green=%d red=%d; gl_InstanceIndex/firstInstance handle routing broken)" green redc
        pass

    // Builds on ssboArray3 with one change: the separate draws collapse into a single
    // indexed indirect multidraw (2 entries, per-entry FirstIndex + FirstInstance).
    // Same 2 arrays, gl_InstanceIndex handle, no vertex attributes. This is the
    // minimal form of the bindless geometry setup; it isolates whether the indirect
    // multidraw honours per-command FirstInstance.
    let ssboArray4Test (runtime : IRuntime) =
        use signature =
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
        use _ = useResource out
        let pix = out.GetValue().Download().AsPixImage<uint8>()
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
        else Log.warn "ssboArray4: FAIL (green=%d red=%d; multidraw drops per-command FirstInstance or runs one draw)" green redc
        pass

    // Clean SSBO-array pull shader with a ViewProjTrafo multiply: the same two-entry
    // indirect multidraw as ssboArray4, but with world-space geometry and a real
    // camera supplied through Sg.viewTrafo / Sg.projTrafo.
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

    let ssboArray5Test (runtime : IRuntime) =
        use signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(256, 256))
        // world-space triangles + a real camera; clean SSBO-array pull effect.
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
        use _ = useResource out
        let pix = out.GetValue().Download().AsPixImage<uint8>()
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

    // Positions-only bindless pull (no normals, no lighting) with a clean hand-written
    // shader (no rewrite): a flat principal color per handle (R/G/B) and a magenta
    // clear, so magenta means nothing was drawn and R/G/B means the position pull
    // works under perspective. Box geometry whose plain render is known to work, real
    // camera.
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

    let bindlessCleanBoxTest (runtime : IRuntime) =
        use signature = runtime.CreateFramebufferSignature [ DefaultSemantic.Colors, TextureFormat.Rgba8; DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
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
        // provide the camera through Sg.viewTrafo / Sg.projTrafo rather than Sg.uniform,
        // which the default identity ViewProjTrafo would shadow.
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
        use _ = useResource out
        let pix = out.GetValue().Download().AsPixImage<uint8>()
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
        savePpm (System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bcbox.ppm"))
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

    // Per-object backend textures through the real heap: N render objects with a naive
    // single-sampler effect (`uniform?DiffuseTexture`), each with a different texture.
    // ofRenderObjects must make them bindless automatically (one generated sampler
    // array, indexed by the per-draw handle) so the collapsed bucket matches a classic
    // render with any number of textures. (Clean effect, no TexId input.)
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

    // Same as TH but with point filtering, to show that the heap re-applies the
    // sampler state. With a magnified checker texture, point and linear differ sharply
    // at block boundaries; if the heap dropped the state (used the default), it would
    // not match the classic point-sampled render. (TH = linear and THP = point cannot
    // both match a single default state, so both passing means the state is genuinely
    // preserved.)
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

    let texHeapTest (runtime : IRuntime) =
        use signature =
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
        let viewProjM = AVal.init (view * proj)
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
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()
        let classicPix = renderToPix (ASet.ofArray ros)
        use storage = runtime.CreateHeapStorage()
        let heapObjs = Heap.ofRenderObjects storage (ASet.ofArray ros)
        let heapPix = renderToPix heapObjs
        let maxD, nDiff, nNonBg, total = diff classicPix heapPix
        Log.line "texHeap: %d ROs (per-object texture) -> %d bucket(s)  classic-vs-heap maxDelta=%d diffPixels=%d coverage=%d"
            ros.Length Heap.lastBucketCount maxD nDiff nNonBg
        // require buckets > 0; otherwise the render objects merely passed through and
        // "matching classic" would be trivially (and misleadingly) true. The bindless
        // path is bit-exact vs classic (maxDelta <= 1); the atlas fallback (no descriptor
        // indexing, e.g. MoltenVK / GL / Vulkan 1.0) resamples through bilinear filtering,
        // 2-px gutters and the in-shader mip pyramid, so deltas of about 3/255 are
        // inherent. Use the atlas test's tolerance (24) there.
        let tolerance = if runtime.SupportsUnboundedSamplerArrays then 1 else 24
        let pass = maxD <= tolerance && nNonBg > total / 100L && Heap.lastBucketCount > 0
        if pass then Log.line "texHeap: PASS (per-object textures auto-bindless via ofRenderObjects == classic; %d bucket(s); tol=%d)" Heap.lastBucketCount tolerance
        else Log.warn "texHeap: FAIL (maxDelta=%d tol=%d coverage=%d buckets=%d -> mis-bound or passthrough)" maxD tolerance nNonBg Heap.lastBucketCount
        pass

    // Render-to-texture case: a per-object texture whose identity swaps at runtime
    // (the double-buffer / resize case). Both the classic and the heap task stay
    // compiled across the mutation, so this exercises the incremental descriptor
    // update of the bindless sampler array (plus re-dedup of the generated texture
    // array and index table), not a fresh rebuild. Passes only if the heap tracks the
    // classic render at both states and the swap actually changed the image.
    let texSwapTest (runtime : IRuntime) =
        use signature =
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
        let viewProjM = AVal.init (view * proj)
        let viewProj = viewProjM :> IAdaptiveValue
        let texArray : ITexture[] = Array.init TexCount mkTexture
        let eff = Effect.compose [ Effect.ofFunction TH.shade; Effect.ofFunction TH.frag ]
        let grid =
            let s = 8
            [| for x in 0 .. s - 1 do for y in 0 .. s - 1 -> (x * s + y), V3d(float (x - s/2) * 1.2, float (y - s/2) * 1.2, 0.0) |]
        // one mutable texture cell per object; the identity changes on swap
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
        // distinct render object instances per path, but sharing texCells so both track
        // the swap
        let classicROs    = grid |> Array.map (fun (i, p) -> mkRO i p)
        let heapInputROs  = grid |> Array.map (fun (i, p) -> mkRO i p)
        let mkOut (objs : aset<IRenderObject>) =
            let task = runtime.CompileRender(signature, objs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            task, out
        let classicTask, classicOut = mkOut (ASet.ofArray classicROs)
        use storage = runtime.CreateHeapStorage()
        let heapObjs = Heap.ofRenderObjects storage (ASet.ofArray heapInputROs)
        let heapTask, heapOut = mkOut heapObjs
        let dl (out : IAdaptiveResource<IBackendTexture>) = out.GetValue().Download().AsPixImage<uint8>()
        // state 0
        let classic0 = dl classicOut
        let heap0    = dl heapOut
        let buckets  = Heap.lastBucketCount
        // swap: every cube's texture identity changes
        transact (fun () -> texCells |> Array.iteri (fun i c -> c.Value <- texArray.[(i + 7) % TexCount]))
        // state 1: the same compiled tasks, re-evaluated (incremental update)
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
        else Log.warn "texSwap: FAIL (s0 delta=%d s1 delta=%d swapChanged=%d buckets=%d)" d0 d1 nChanged buckets
        pass

    // Sampler-state preservation: identical to texHeapTest but the effect uses point
    // filtering. The heap must re-apply that state to its generated bindless array;
    // if it used the default (about linear), point vs linear would diverge sharply at
    // the magnified checker boundaries and this would fail. (texHeap = linear passing
    // and texState = point passing together prove the state is carried, not defaulted.)
    let texStateTest (runtime : IRuntime) =
        use signature =
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
        let viewProjM = AVal.init (view * proj)
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
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()
        let classicPix = renderToPix (ASet.ofArray ros)
        use storage = runtime.CreateHeapStorage()
        let heapObjs = Heap.ofRenderObjects storage (ASet.ofArray ros)
        let heapPix = renderToPix heapObjs
        let maxD, nDiff, nNonBg, total = diff classicPix heapPix
        Log.line "texState: %d ROs (POINT filter) -> %d bucket(s)  classic-vs-heap maxDelta=%d diffPixels=%d coverage=%d"
            ros.Length Heap.lastBucketCount maxD nDiff nNonBg
        let pass = maxD <= 1 && nNonBg > total / 100L && Heap.lastBucketCount > 0
        if pass then Log.line "texState: PASS (sampler STATE preserved: point-filtered heap == classic; %d bucket(s))" Heap.lastBucketCount
        else Log.warn "texState: FAIL (maxDelta=%d coverage=%d buckets=%d -> state dropped or passthrough)" maxD nNonBg Heap.lastBucketCount
        pass

    // a cube texture with one uniform color per face; the fragment shader samples it
    // by world normal
    let private mkCubeTexture (i : int) : ITexture =
        let cols = [| C4b(230uy,60uy,60uy,255uy); C4b(60uy,200uy,60uy,255uy); C4b(60uy,120uy,230uy,255uy); C4b(230uy,200uy,40uy,255uy)
                      C4b(210uy,60uy,210uy,255uy); C4b(40uy,210uy,210uy,255uy); C4b(230uy,140uy,40uy,255uy); C4b(180uy,180uy,180uy,255uy) |]
        let face (c : C4b) =
            let img = PixImage<byte>(Col.Format.RGBA, V2i(16, 16))
            img.GetMatrix<C4b>().SetByIndex(fun (_ : int64) -> c) |> ignore
            PixImageMipMap(img :> PixImage)
        // give each of the 6 faces a distinct color (offset by i) so direction matters
        PixTextureCube(PixCube [| for f in 0 .. 5 -> face cols.[(i + f) % cols.Length] |], false) :> ITexture

    // Cubemap path: per-object samplerCube made bindless by ofRenderObjects, sampled
    // by world normal. Shows that the per-type bindless array (samplerCube[]) works.
    let texCubeTest (runtime : IRuntime) =
        use signature =
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
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()
        let classicPix = renderToPix (ASet.ofArray ros)
        use storage = runtime.CreateHeapStorage()
        let heapObjs = Heap.ofRenderObjects storage (ASet.ofArray ros)
        let heapPix = renderToPix heapObjs
        let maxD, nDiff, nNonBg, total = diff classicPix heapPix
        Log.line "texCube: %d ROs (per-object samplerCube) -> %d bucket(s)  classic-vs-heap maxDelta=%d diffPixels=%d coverage=%d"
            ros.Length Heap.lastBucketCount maxD nDiff nNonBg
        let pass = maxD <= 1 && nNonBg > total / 100L && Heap.lastBucketCount > 0
        if pass then Log.line "texCube: PASS (per-object cubemaps auto-bindless via samplerCube[] == classic; %d bucket(s))" Heap.lastBucketCount
        else Log.warn "texCube: FAIL (maxDelta=%d coverage=%d buckets=%d)" maxD nNonBg Heap.lastBucketCount
        pass

    // GPU-resident geometry: positions/normals/index uploaded as backend buffers.
    // ofRenderObjects cannot slice them on the CPU, so it routes the bucket to the
    // bindless vertex-pull path (the objects' buffers bound as HeapVertexData, pulled
    // by handle). Input types equal the buffer types (V3f), so the gather decodes
    // exactly.
    module GG =
        type VIn  = { [<Semantic("Positions")>] pos : V3f; [<Semantic("Normals")>] n : V3f }
        type VOut = { [<Position>] clip : V4f; [<Normal>] wn : V3f; [<Color>] c : V4f }
        // read per-draw uniforms in the vertex stage (gl_DrawID is a vertex-only builtin
        // on GL); pass results as varyings. The GPU-geometry feature under test is the
        // vertex pull of Positions/Normals, independent of where uniforms are gathered.
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
        use signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        // upload geometry to the GPU once (shared by all objects); the default
        // BufferUsage.All makes each usable as both a vertex buffer (classic) and
        // storage (vertex-pull).
        use posGpu = runtime.PrepareBuffer(ArrayBuffer positions :> IBuffer)
        use nrmGpu = runtime.PrepareBuffer(ArrayBuffer normals   :> IBuffer)
        use idxGpu = runtime.PrepareBuffer(ArrayBuffer index     :> IBuffer)
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
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()
        let classicPix = renderToPix (ASet.ofArray ros)   // GPU buffers, fixed-function input
        use storage = runtime.CreateHeapStorage()
        let heapObjs = Heap.ofRenderObjects storage (ASet.ofArray ros)
        let heapPix = renderToPix heapObjs                 // GPU buffers, bindless vertex-pull
        let maxD, nDiff, nNonBg, total = diff classicPix heapPix
        Log.line "%s: %d ROs (GPU-resident geometry) -> %d bucket(s)  classic-vs-heap maxDelta=%d diffPixels=%d coverage=%d"
            label ros.Length Heap.lastBucketCount maxD nDiff nNonBg
        // Vulkan: GPU geometry cannot take the host combined-buffer path, so buckets > 0
        // proves the bindless vertex-pull ran. GL: descriptor indexing is unavailable, so
        // GPU geometry must pass through (buckets = 0) and still render identically
        // through the classic path.
        let bucketsOk = if expectHeaped then Heap.lastBucketCount > 0 else Heap.lastBucketCount = 0
        let pass = maxD <= 1 && nNonBg > total / 100L && bucketsOk
        let how = if expectHeaped then "vertex-pulled via ofRenderObjects == classic" else "passthrough on GL (not heaped) == classic"
        if pass then Log.line "%s: PASS (GPU-resident geometry %s; %d bucket(s))" label how Heap.lastBucketCount
        else Log.warn "%s: FAIL (maxDelta=%d coverage=%d buckets=%d expectHeaped=%b)" label maxD nNonBg Heap.lastBucketCount expectHeaped
        pass

    let gpuGeomTest (runtime : IRuntime) =
        gpuGeomWith "gpuGeom" true runtime

    // CPU-only: verifies the texture atlas builder's border and mip layout precisely
    // (no GPU). Gutter: inner ring = clamp-replicate edge texel, outer ring = wrap
    // opposite edge. Iliffe pyramid: mip k at origin + mipOffset, 2x2 box-averaged.
    let atlasBuildTest (_ : IRuntime) =
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

    // Atlas path: per-object textures sampled from one packed atlas page (the fallback
    // for Vulkan 1.0 / GL / MoltenVK, no descriptor indexing). forceAtlas routes the
    // heap through the atlas even on desktop Vulkan. Compared to the classic per-object
    // render: atlas sampling (repacked texels + in-shader LOD/wrap) is not bit-exact vs
    // hardware sampling, so a few badly-off pixels are allowed but the image must look
    // the same.
    let atlasHeapTest (runtime : IRuntime) =
        use signature =
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
        let viewProjM = AVal.init (view * proj)
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
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()
        let classicPix = renderToPix (ASet.ofArray ros)
        Heap.forceAtlas <- true
        let heapPix, buckets =
            try
                use storage = runtime.CreateHeapStorage()
                let heapObjs = Heap.ofRenderObjects storage (ASet.ofArray ros)
                let heapPix = renderToPix heapObjs
                heapPix, Heap.lastBucketCount
            finally Heap.forceAtlas <- false
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

    // Renders the atlas scene offscreen into a multisampled framebuffer through the
    // heap (forceAtlas), exercising MSAA and the backend's secondary-command-buffer
    // render path. The environment variables SAMPLES (default 8) and N (default 2000)
    // separate MSAA from scale, e.g. on MoltenVK: SAMPLES=8 N=64 tests MSAA at a tiny
    // scale, SAMPLES=1 N=20000 tests scale. Forces the render (no download), so a GPU
    // hang would reproduce here; prints COMPLETED otherwise and always passes.
    let msaaTest (runtime : IRuntime) =
        let samples = match System.Environment.GetEnvironmentVariable "SAMPLES" with null | "" -> 8 | s -> int s
        let n       = match System.Environment.GetEnvironmentVariable "N"       with null | "" -> 2000 | s -> int s
        use signature =
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
        let viewProjM = AVal.init (view * proj)
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
        try
            use storage = runtime.CreateHeapStorage()
            let heapObjs = Heap.ofRenderObjects storage (ASet.ofArray ros)
            Log.line "msaaTest: rendering n=%d samples=%d (atlas, offscreen) ..." n samples
            use task = runtime.CompileRender(signature, heapObjs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            out.GetValue() |> ignore         // force the GPU render; a hang reproduces here
            out.Release()
        finally Heap.forceAtlas <- false
        Log.line "msaaTest: COMPLETED n=%d samples=%d buckets=%d (no hang)" n samples Heap.lastBucketCount
        true

    // Submit stress: hammer N synchronous GPU uploads (each a vkQueueSubmit plus a fence
    // wait on the upload path) with no heap, glyphs or window. Guards against a driver
    // that stops signalling fences after too many submits (suspected on MoltenVK). Logs
    // progress; if it wedges at some count, that is a driver or resource-exhaustion bug.
    // N = iterations (default 5000); always passes when it completes.
    let submitStressTest (runtime : IRuntime) =
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

    // Glyph upload on a loaded device, without a swapchain: build and render a heavy
    // heap scene to load the GPU, then do the glyph GeometryPool upload (PrepareGlyphs)
    // on the loaded device. If this wedges, the Fence.Wait watchdog prints the managed
    // stack, and MoltenVK verbose logging (MVK_CONFIG_LOG_LEVEL=3) prints the Metal
    // command-buffer error right before it, which says what the GPU is stuck on.
    // ITERS = how many times to render the heap before the glyph upload (mimics frames;
    // default 1). N = object count (default 20000). Always passes when it completes.
    let glyphWedgeTest (runtime : IRuntime) =
        let n     = match System.Environment.GetEnvironmentVariable "N"     with null | "" -> 20000 | s -> int s
        let iters = match System.Environment.GetEnvironmentVariable "ITERS" with null | "" -> 1     | s -> int s
        use signature =
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
        let viewProjM = AVal.init (view * proj)              // nudged per iteration to re-submit
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
        try
            use storage = runtime.CreateHeapStorage()
            let heapObjs = Heap.ofRenderObjects storage (ASet.ofArray ros)
            Log.line "glyphWedge: build + render heap n=%d (x%d) offscreen ..." n iters
            use task = runtime.CompileRender(signature, heapObjs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            // re-submit the heap render ITERS times (mutate viewProj each "frame" so it is
            // actually dirty and re-rendered), mimicking a continuous render loop
            for it in 1 .. iters do
                transact (fun () -> viewProjM.Value <- view * proj * Trafo3d.RotationZ(float it * 1.0e-4))
                out.GetValue() |> ignore
            out.Release()
        finally Heap.forceAtlas <- false
        Log.line "glyphWedge: heap render DONE (buckets=%d). Now PrepareGlyphs on the loaded device ..." Heap.lastBucketCount
        let sw = System.Diagnostics.Stopwatch.StartNew()
        runtime.PrepareGlyphs(DefaultFonts.Hack.Regular, [| for c in 0 .. 255 -> char c |])
        Log.line "glyphWedge: PrepareGlyphs DONE in %.2fs, no wedge" sw.Elapsed.TotalSeconds
        true

    // Standalone AtlasPool test: exercises Acquire/Release, dedup, LRU eviction and
    // multiple pages on the reactive pool (HeapAtlasPool.fs) without HeapPool
    // integration. The pass criteria are pure bookkeeping (entry/page counts, dedup
    // returns the same Acquisition, refcount semantics, LRU eviction frees slots so the
    // next Acquire fits without growing). The sub-rectangle upload runs on every
    // Acquire; a backend bug there would assert or raise a Vulkan error here.
    let atlasPoolTest (runtime : IRuntime) =
        // small pages so overflow + eviction trigger quickly
        let pageSz, maxPages = 512, 2
        use pool = new AtlasPool(runtime, pageSz, maxPages)
        Log.line "atlasPool: pageSize=%d maxPages=%d" pageSz maxPages

        // Build N unique (ITexture, PixImage) pairs. The ITexture key is the reference
        // identity, so unique PixTexture2d wrappers make each Acquire a fresh entry.
        let mkPair (i : int) =
            let s = 64 + (i % 4) * 16        // 64, 80, 96, 112
            let img = PixImage<byte>(Col.Format.RGBA, V2i(s, s))
            let m = img.GetMatrix<C4b>()
            let c = C4b(byte ((i * 23) % 256), byte ((i * 53) % 256), byte ((i * 91) % 256), 255uy)
            m.SetByIndex(fun _ -> c) |> ignore
            (PixTexture2d(img) :> ITexture), img
        let n0 = 12
        let pairs = Array.init n0 mkPair

        // Phase 1: acquire all -> unique entries; PageCount must be <= maxPages.
        let acqs = pairs |> Array.map (fun (t, p) -> pool.Acquire(t, p))
        Log.line "atlasPool: phase1 acquired %d entries on %d page(s)" pool.EntryCount pool.PageCount
        if pool.EntryCount <> n0 then Log.warn "atlasPool: FAIL phase1 entry count %d <> %d" pool.EntryCount n0; false |> ignore
        if pool.PageCount > maxPages then Log.warn "atlasPool: FAIL phase1 pageCount %d > maxPages %d" pool.PageCount maxPages; false |> ignore
        let phase1Ok = pool.EntryCount = n0 && pool.PageCount <= maxPages

        // Phase 2: dedup. Re-acquiring pair[0] gives the same Acquisition (PageId +
        // OriginPx); EntryCount is unchanged.
        let (t0, p0) = pairs.[0]
        let acq0' = pool.Acquire(t0, p0)
        let dedupOk = (fst acq0').PageId = (fst acqs.[0]).PageId && (fst acq0').OriginPx = (fst acqs.[0]).OriginPx && pool.EntryCount = n0
        Log.line "atlasPool: dedup re-acquire same slot=%b (entries=%d)" dedupOk pool.EntryCount

        // Phase 3: release everything (count down all refcounts to 0).
        // pair[0] was acquired twice -> release twice. Others once.
        pool.Release t0   // first  release for pair[0]
        for (t, _) in pairs do pool.Release t
        Log.line "atlasPool: phase3 released, entries still cached=%d" pool.EntryCount
        if pool.EntryCount <> n0 then Log.warn "atlasPool: FAIL phase3 release dropped entries (%d)" pool.EntryCount; false |> ignore
        let phase3Ok = pool.EntryCount = n0

        // Phase 4: re-acquire one -> must hit the cache (no eviction yet). Same Acquisition
        // again.
        let acq0'' = pool.Acquire(t0, p0)
        let cachedOk = (fst acq0'').PageId = (fst acqs.[0]).PageId && (fst acq0'').OriginPx = (fst acqs.[0]).OriginPx
        Log.line "atlasPool: cached re-acquire after release=%b" cachedOk
        pool.Release t0

        // Phase 5: force LRU eviction. Add new distinct textures until the pool is full and
        // must evict. With every refcount at 0 they are evictable; the new ones should fit.
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

    // Isolation: a plain (non-heap) offscreen render. If this fails too, the problem
    // is the offscreen render path on the backend, not the heap.
    let plainTest (runtime : IRuntime) =
        use signature =
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

    // Verifies pipeline-state bucketing: render objects with the same effect but a
    // different rasterizer (cull) state must land in separate buckets, while objects
    // with the same state still collapse into one.
    let bucketingTest (runtime : IRuntime) =
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let viewProj = AVal.constant Trafo3d.Identity :> IAdaptiveValue
        let effect = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        // one shared custom rasterizer state (cull back) for half the render objects
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
        use signature = runtime.CreateFramebufferSignature [DefaultSemantic.Colors, TextureFormat.Rgba8]
        use storage = runtime.CreateHeapStorage()
        let heap = Heap.ofRenderObjects storage (ASet.ofArray inputs)
        // signature-deferred: buckets build at compile time, so render once to force them
        do
            use task = runtime.CompileRender(signature, heap)
            let out = task |> RenderTask.renderToColor (AVal.constant (V2i(64, 64)))
            use _ = useResource out
            out.GetValue() |> ignore
        Log.line "bucketing: 16 ROs (8 default + 8 cull-back) -> %d bucket(s)" Heap.lastBucketCount
        let pass = Heap.lastBucketCount = 2
        if pass then Log.line "bucketing: PASS (distinct pipeline state -> distinct buckets)"
        else Log.warn "bucketing: FAIL (expected 2 buckets)"
        pass

    // Verifies dynamic mode rules: a per-object cull-mode aval drives bucketing, so
    // flipping it (in a transact) re-partitions the heap reactively (1 -> 2 buckets)
    // without touching the object set.
    let modeRulesTest (runtime : IRuntime) =
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let viewProj = AVal.constant Trafo3d.Identity :> IAdaptiveValue
        let effect = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        let n = 16
        let culls = Array.init n (fun _ -> AVal.init CullMode.None)   // per-object cull "rule"
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
        use signature = runtime.CreateFramebufferSignature [DefaultSemantic.Colors, TextureFormat.Rgba8]
        use storage = runtime.CreateHeapStorage()
        let heap = Heap.ofRenderObjects storage (ASet.ofArray inputs)
        // signature-deferred: keep one compiled task alive across the flips so the
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

    // Verifies already-instanced inputs to ofRenderObjects: input render objects with
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

    let alreadyInstancedTest (runtime : IRuntime) =
        use signature =
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
            use _ = useResource out
            out.GetValue().Download().AsPixImage<uint8>()

        // instanced: 2 render objects, each instanceCount = 4, offset per instance via
        // gl_InstanceIndex
        let effInst = Effect.compose [ Effect.ofFunction AI.shadeInst; Effect.ofFunction AI.frag ]
        let instancedInputs =
            bases |> Array.map (fun (p, c) ->
                mkRO effInst [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                               Symbol.Create "HeapColor",      (AVal.constant (c.ToV4f()) :> IAdaptiveValue)
                               Symbol.Create "ViewProjTrafo",  viewProj ] k)
        use storage = runtime.CreateHeapStorage()
        let imgInst = imageOf (Heap.ofRenderObjects storage (ASet.ofArray instancedInputs))
        let instBuckets = Heap.lastBucketCount

        // reference: 8 plain render objects (offset baked into the trafo), instanceCount = 1
        let effPlain = Effect.compose [ Effect.ofFunction AI.shadePlain; Effect.ofFunction AI.frag ]
        let plainInputs =
            [| for (p, c) in bases do
                 for i in 0 .. k-1 ->
                   mkRO effPlain [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p * Trafo3d.Translation(float i * 1.5, 0.0, 0.0)).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                                   Symbol.Create "HeapColor",      (AVal.constant (c.ToV4f()) :> IAdaptiveValue)
                                   Symbol.Create "ViewProjTrafo",  viewProj ] 1 |]
        use storage = runtime.CreateHeapStorage()
        let imgPlain = imageOf (Heap.ofRenderObjects storage (ASet.ofArray plainInputs))

        let maxD, nDiff, nNonBg, total = diff imgPlain imgInst
        Log.line "already-instanced: 2 ROs x %d instances (%d bucket, gl_DrawID routing) vs 8 plain ROs  maxDelta=%d diffPixels=%d coverage=%d" k instBuckets maxD nDiff nNonBg
        let pass = maxD <= 1 && nNonBg > 500L && instBuckets = 1
        if pass then Log.line "already-instanced: PASS (instanced inputs == non-instanced expansion; per-draw routed by gl_DrawID)"
        else Log.warn "already-instanced: FAIL (maxDelta=%d nNonBg=%d buckets=%d)" maxD nNonBg instBuckets
        pass

    // Verifies the per-object IsActive visibility gate: deactivating half the draws
    // (in a transact) roughly halves the rendered coverage, with no rebuild.
    let visibilityTest (runtime : IRuntime) =
        use signature =
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
        use storage = runtime.CreateHeapStorage()
        let heap = Heap.ofRenderObjects storage (ASet.ofArray inputs)
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

    // Headless churn probe: per-frame cost of one add plus one remove in a textured
    // bucket (bindless sampler array) and in a bindless-geometry bucket. Informational
    // (prints median/max frame times); always returns true. The environment variable N
    // overrides the bucket size (default 5000).
    let churnProbeTest (runtime : IRuntime) =
        use signature =
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
            use storage = runtime.CreateHeapStorage()
            let heapObjs = Heap.ofRenderObjects storage (ros :> aset<_>)
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

        // textured bucket (per-object DiffuseTexture -> bindless sampler array)
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

        // bindless-geometry bucket (GPU-resident buffers -> vertex-pull)
        use posGpu = runtime.PrepareBuffer(ArrayBuffer positions :> IBuffer)
        use nrmGpu = runtime.PrepareBuffer(ArrayBuffer normals   :> IBuffer)
        use idxGpu = runtime.PrepareBuffer(ArrayBuffer index     :> IBuffer)
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
        // the unbounded SSBO array binding is capped at 1024 descriptors by the device
        // (slots x attribute count), a backend limit of the vertex-pull path, so size
        // the bindless bucket within it (2 attributes -> at most 512 slots).
        run "bindless-geom" (min n 480) mkGpuRO
        true

    // Geometry reclamation probe: distinct per-object geometry, one remove plus one add
    // per frame with a fresh geometry of equal size. The combined packed buffers must
    // stay flat after the initial build (a freed geometry's exact-size ranges are
    // reused in place; Heap.lastPackedGeomBytes) and the churned scene's final image
    // must equal a freshly built scene of the same membership. Two phases: host-packed
    // geometry (vertex + index ranges) and bindless/vertex-pull (index ranges only).
    let geomChurnTest (runtime : IRuntime) =
        use signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(512, 512))
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 16.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let n = 50                  // not divisible by the size/color cycles below,
        let frames = 96             // so a replacement differs visibly from the object it replaces
        let side = 8
        let posOf (i : int) = V3d(float (i % side - side / 2) * 1.4, float (i / side - side / 2) * 1.4, 0.0)
        // fresh (identity-distinct) box arrays per call with the same vertex/index
        // counts (exact-size reuse); the size depends on i so stale-range bugs change
        // pixels.
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
        // per-object GPU geometry, released when the probe ends
        let gpuBuffers = System.Collections.Generic.List<IBackendBuffer>()
        let mkGpuRO (i : int) =
            let (positions, normals, index) = mkGeom i
            let gbv (b : IBackendBuffer) t = BufferView(AVal.constant (b :> IBuffer), t)
            let posGpu = runtime.PrepareBuffer(ArrayBuffer positions :> IBuffer)
            let nrmGpu = runtime.PrepareBuffer(ArrayBuffer normals   :> IBuffer)
            let idxGpu = runtime.PrepareBuffer(ArrayBuffer index     :> IBuffer)
            gpuBuffers.AddRange [ posGpu; nrmGpu; idxGpu ]
            let attrs = AttributeProvider.ofList [ Symbol.Create "Positions", gbv posGpu typeof<V3f>; Symbol.Create "Normals", gbv nrmGpu typeof<V3f> ]
            mkRO i attrs (gbv idxGpu typeof<int>) index.Length

        let runPhase (label : string) (mk : int -> IRenderObject) =
            let all = Array.init (n + frames) mk
            let ros = cset (Array.sub all 0 n)
            use storage = runtime.CreateHeapStorage()
            let heapObjs = Heap.ofRenderObjects storage (ros :> aset<_>)
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
            // reference: the same final membership (all.[frames .. frames+n-1]) built fresh
            let refRos = cset (Array.sub all frames n)
            use storage = runtime.CreateHeapStorage()
            let refObjs = Heap.ofRenderObjects storage (refRos :> aset<_>)
            use refTask = runtime.CompileRender(signature, refObjs)
            let refOut = refTask |> RenderTask.renderToColor size
            refOut.Acquire()
            let refImg = refOut.GetValue().Download().ToPixImage<byte>()
            refOut.Release()
            let (maxDelta, nDiff, nNonBg, total) = diff img refImg
            let flat = maxBytes = bytes0
            // exact-size churn must be served by allocator reuse before any
            // compaction can trigger (no waste accumulates, so none fires)
            let noCompact = Heap.compactionCount = comps0
            Log.line "geomChurn[%s]: packedBytes start=%d maxDuringChurn=%d flat=%b compactions=%d  maxChannelDelta=%d diffPixels=%d/%d coverage=%d"
                label bytes0 maxBytes flat (Heap.compactionCount - comps0) maxDelta nDiff total nNonBg
            let pass = flat && noCompact && maxDelta = 0 && nNonBg > 1000L
            if pass then Log.line "geomChurn[%s]: PASS" label
            else Log.warn "geomChurn[%s]: FAIL" label
            pass

        let hostOk = runPhase "host" mkHostRO
        let gpuOk  = runPhase "bindless" mkGpuRO
        for b in gpuBuffers do b.Dispose()
        let pass = hostOk && gpuOk
        if pass then Log.line "geomChurn: ALL PASS" else Log.warn "geomChurn: FAIL (host=%b bindless=%b)" hostOk gpuOk
        pass

    // Value-level geometry dedup golden: two render objects wrapping the same arrays
    // in fresh BufferViews/avals (exactly what Sg combinators + Primitives.Box
    // produce per leaf) must share one packed geometry allocation: the arena
    // footprint (Heap.lastPackedGeomBytes) must equal the identity-shared baseline
    // (both objects using the very same BufferView instances) and the image must be
    // pixel-identical to it. A fresh-copy control (arrays of the same content that
    // are distinct both by identity and by value) must pack more bytes, proving the
    // assertion is not vacuous.
    let geomValueDedupTest (runtime : IRuntime) =
        use signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(512, 512))
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 6.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let effect = Effect.compose [ Effect.ofFunction GG.shade; Effect.ofFunction GG.frag ]

        // one set of shared box arrays: the value-level identity under test
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
            use storage = runtime.CreateHeapStorage()
            let heapObjs = Heap.ofRenderObjects storage ros
            use task = runtime.CompileRender(signature, heapObjs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            let img = out.GetValue().Download().ToPixImage<byte>()
            let bytes = Heap.lastPackedGeomBytes
            out.Release()
            bytes, img

        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        // A: identity-shared baseline, one BufferView/aval instance per attribute,
        // referenced by both render objects
        let sharedAttrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let sharedIdx = bv index typeof<int>
        let bytesA, imgA = render (fun i -> mkRO i sharedAttrs sharedIdx)
        // B: a fresh BufferView + fresh AVal.constant(ArrayBuffer ...) per render object,
        // all wrapping the same arrays: must dedup at the value level
        let bytesB, imgB =
            render (fun i ->
                let attrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
                mkRO i attrs (bv index typeof<int>))
        // C: control, fresh array copies per render object (value-distinct) must not dedup
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

    // Non-indexed golden: render objects without an index buffer (a single
    // zero-offset Direct draw call) must ride the heap too: the slot's header
    // carries the -1 sentinel and the shader's decodeHeapIndex passes
    // gl_VertexIndex through. Three scenes over the same two-triangle membership:
    // all indexed (reference), mixed indexed + non-indexed in one bucket (per-slot
    // decode branch), and all non-indexed. All must collapse to 1 bucket (no
    // pass-through) and render pixel-identical.
    let nonIndexedTest (runtime : IRuntime) =
        use signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(512, 512))
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 4.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let effect = Effect.compose [ Effect.ofFunction GG.shade; Effect.ofFunction GG.frag ]
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)

        // a quad as two triangles, vertices already unrolled so the indexed
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
            use storage = runtime.CreateHeapStorage()
            let heapObjs = Heap.ofRenderObjects storage (ASet.ofArray [| mk 0; mk 1 |])
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

    // Drift golden test: 320 frames of churn with distinct geometries of random
    // size (cone fans with random rim counts) and random per-object instance
    // counts on the forced slot-attribute fallback (Heap.forceNoDrawId), so
    // every reclamation site drifts: packed vertex ranges, packed index ranges,
    // arena float regions and the per-instance slot-attribute ranges. The
    // coalescing allocators plus waste-triggered compaction must keep every
    // footprint bounded by about 2.5x the live working set (plus 2x the compaction
    // floor, which is lowered for the test so the small buffers compact too),
    // and the final image must be pixel-identical to a freshly built scene of
    // the same final population (compaction rewrote FirstIndex/BaseVertex/
    // FirstInstance/headers/arena offsets correctly). A bulk 75% removal at
    // half-time deterministically trips the compaction trigger (less than half
    // of the extent live).
    let geomDriftTest (runtime : IRuntime) =
        use signature =
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
        // random-size cone fan: m rim vertices + apex, 3m indices; every geometry
        // is distinct by identity and by size, so exact-size reuse alone cannot
        // keep the packed buffers flat (the drift this test bounds).
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
            // k identical instances per draw: the slot-attribute routing (not the
            // image) is what varies; wrong instData slots would fetch the wrong
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
            use storage = runtime.CreateHeapStorage()
            let heapObjs = Heap.ofRenderObjects storage (ros :> aset<_>)
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
                        // bulk shrink: 75% of the population leaves in one pass
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

            // reference: the same final membership built fresh
            let refRos = cset (live.ToArray())
            use storage = runtime.CreateHeapStorage()
            let refObjs = Heap.ofRenderObjects storage (refRos :> aset<_>)
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

    // Lifetime golden test: repeatedly (30x) build a heap scene, a plain uniform
    // bucket plus a textured atlas bucket (forceAtlas), so the draw/header
    // mirrors, the HeapArena and the atlas pool are all covered; render a few
    // frames, then tear it down through the API's disposal entry (empty the
    // input set, so the incremental driver disposes the buckets and the render
    // task's delta processing releases the prepared resources) and dispose the
    // task. After every cycle the device's memory statistics
    // (Device.GetMemoryStatistics(): allocation count and allocated bytes) must
    // return to the post-warmup baseline: the heap-owned AdaptiveBuffers are
    // destroyed by the resource layer's Release (IAdaptiveResource refcounting).
    let lifetimeTest (runtime : IRuntime) =
        let device = (runtime :?> Aardvark.Rendering.Vulkan.Runtime).Device   // Vulkan-only probe: device statistics
        use signature =
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
        // effects built once so shader modules / pipelines are cached across cycles
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

        let stats () =
            let heaps = device.GetMemoryStatistics()
            heaps |> Array.sumBy (fun h -> int h.Allocations), heaps |> Array.sumBy (fun h -> uint64 h.AllocationBytes.Bytes)

        let runCycle (ci : int) =
            // fresh render objects + fresh heap each cycle (fresh geometry array
            // identities would defeat pipeline caching, so geometry is shared;
            // the heap's packed buffers are per-bucket and rebuilt anyway).
            let ros = cset (Array.init 64 (fun i -> if i % 2 = 0 then mkRO i effPlain false else mkRO i effTex true))
            use storage = runtime.CreateHeapStorage()
            let heapObjs = Heap.ofRenderObjects storage (ros :> aset<_>)
            use task = runtime.CompileRender(signature, heapObjs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            // a few frames (camera nudges force re-submission)
            for f in 0 .. 2 do
                transact (fun () -> viewProjM.Value <- CameraView.lookAt (V3d(0.02 * float (ci + f), -1.0, 1.0) * 18.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo |> fun v -> v * proj)
                out.GetValue() |> ignore
            let buckets = Heap.lastBucketCount
            // teardown via the API's disposal entry: empty the input set, so the
            // updater disposes the (now key-less) buckets and the render task's
            // delta processing releases the bucket render object's resources
            // (which, via IAdaptiveResource refcounting, destroys the heap-owned
            // buffers).
            transact (fun () -> ros.Clear())
            out.GetValue() |> ignore
            out.Release()
            buckets

        // force the atlas texture path (desktop Vulkan would go bindless), so a
        // bucket-owned AtlasPool + dummy textures are created/destroyed per cycle.
        Heap.forceAtlas <- true
        try
            // warm-up cycles populate caches (shader modules, pipelines, sampler /
            // descriptor machinery, allocator block pools); baseline taken after them.
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
            // post-cycle stats must return to the baseline (small slack for lazily
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
