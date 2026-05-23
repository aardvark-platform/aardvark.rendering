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
        let heapU = Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo"; "HeapColor" ]) (ASet.ofArray inputsU)
        let passU = report "uniform" classicU (renderToPix heapU)

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
        let classicTpix = renderToPix (ASet.ofArray classicT)
        let heapTobjs = Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo"; "HeapTexIndex" ]) (ASet.ofArray heapT)
        let passT = report "textured" classicTpix (renderToPix heapTobjs)

        let pass = passU && passT
        if pass then Log.line "golden: ALL PASS (uniform + bindless-textured heap == classic)"
        else Log.warn "golden: FAILED"
        pass

    // fp64 derived-uniform compute pre-pass test. Renders the SAME camera-relative
    // cube grid (a) at normal scale and (b) at geodetic scale (~earth radius) via
    // Heap.derivedFp64 (fp64 ModelViewProjTrafo + NormalMatrix), and (c) at geodetic
    // scale via the f32-inline heap. Camera-relative => (a) and (b) must look the
    // same (fp64 stays precise); (c) breaks (f32 loses precision at that scale).
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

    let derivedFp64Test () =
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

        // camera-relative grid: cubes at (origin + offset), eye at (origin + relEye)
        let side = 6
        let offsets = [| for x in 0 .. side-1 do for y in 0 .. side-1 -> V3d(float (x-side/2) * 1.4, float (y-side/2) * 1.4, 0.0) |]
        let n = offsets.Length
        let relEye = V3d(0.0, -1.0, 0.6) * 14.0
        let proj = Frustum.perspective 70.0 0.1 1.0e9 1.0 |> Frustum.projTrafo

        let imageOf (sg : ISg) =
            use task = sg |> Sg.compile runtime signature
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let coverage (img : PixImage<uint8>) =
            let m = img.GetMatrix<C4b>()
            let mutable c = 0L
            m.ForeachCoord(fun (p : V2l) -> let v = m.[p] in if v.R <> 0uy || v.G <> 0uy || v.B <> 0uy then c <- c + 1L)
            c

        let effFp64 = Effect.compose [ Effect.ofFunction DF.shadeFp64; Effect.ofFunction DF.frag ]

        // (a) fp64 at normal scale (origin); (b) fp64 at geodetic scale — same
        // camera-relative scene, so a precise pipeline yields the SAME image.
        let sceneFp64 (origin : V3d) =
            let view = AVal.constant (CameraView.lookAt (origin + relEye) origin V3d.OOI |> CameraView.viewTrafo)
            let models = offsets |> Array.map (fun o -> AVal.constant (Trafo3d.Translation(origin + o)))
            Heap.derivedFp64 runtime IndexedGeometryMode.TriangleList positions normals index effFp64 view (AVal.constant proj) models
        let earth = V3d(6378137.0, 3189000.0, 1594500.0)
        let imgNormal  = imageOf (sceneFp64 V3d.Zero)
        let imgGeoFp64 = imageOf (sceneFp64 earth)

        // (c) f32 inline heap at geodetic scale (ModelViewProjTrafo derived in f32)
        let imgGeoF32 =
            let viewProj = AVal.constant ((CameraView.lookAt (earth + relEye) earth V3d.OOI |> CameraView.viewTrafo) * proj)
            let effMvp = Effect.compose [ Effect.ofFunction DF.shadeMvp; Effect.ofFunction DF.frag ]
            let inputs =
                offsets |> Array.map (fun o ->
                    let ro = RenderObject()
                    ro.Surface <- Surface.Effect effMvp
                    ro.Mode <- IndexedGeometryMode.TriangleList
                    ro.VertexAttributes <- vattrs
                    ro.Indices <- Some (bv index typeof<int>)
                    ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
                    ro.Uniforms <- UniformProvider.ofList [
                        Symbol.Create "ModelTrafo",    (AVal.constant (Trafo3d.Translation(earth + o)) :> IAdaptiveValue)
                        Symbol.Create "ViewProjTrafo", (viewProj :> IAdaptiveValue) ]
                    ro :> IRenderObject)
            imageOf (Sg.renderObjectSet (Heap.ofRenderObjects runtime (Set.ofList [ "ModelTrafo" ]) (ASet.ofArray inputs)))

        let covNormal  = coverage imgNormal
        let covGeoFp64 = coverage imgGeoFp64
        // fp64 geodetic must equal fp64 normal (scale-invariant, precise);
        // f32 inline at geodetic must DIFFER from it (jittered cubes).
        let dInvar, _, _, total = diff imgNormal imgGeoFp64
        let _, nDiffF32, _, _ = diff imgGeoFp64 imgGeoF32
        Log.line "derivedFp64: n=%d  cov normal=%d geo-fp64=%d  fp64 normal-vs-geo maxDelta=%d  f32 diffPixels=%d/%d (%.1f%%)"
            n covNormal covGeoFp64 dInvar nDiffF32 total (100.0 * float nDiffF32 / float total)
        let scaleInvariant = covNormal > 1000L && dInvar <= 1
        let f32Broke = float nDiffF32 / float total > 0.02
        let pass = scaleInvariant && f32Broke
        if pass then Log.line "derivedFp64: PASS (fp64 compute bit-identical across scale; f32 inline jitters at geodetic scale)"
        else Log.warn "derivedFp64: FAIL (scaleInvariant=%b f32Broke=%b)" scaleInvariant f32Broke
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
        let heap = Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo"; "HeapColor" ]) (ASet.ofArray inputs)
        heap |> ASet.toAVal |> AVal.force |> ignore
        Log.line "bucketing: 16 ROs (8 default + 8 cull-back) -> %d bucket(s)" Heap.lastBucketCount
        let pass = Heap.lastBucketCount = 2
        if pass then Log.line "bucketing: PASS (distinct pipeline state -> distinct buckets)"
        else Log.warn "bucketing: FAIL (expected 2 buckets)"
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
        let heap = Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo"; "HeapColor" ]) (ASet.ofArray inputs)
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
