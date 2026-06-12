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
        let heapObjs = Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo"; "HeapColor" ]) (ASet.ofArray inputs)
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

    // GPU transform propagation (Slice A): the chained path must produce the
    // SAME image as the composed derivedFp64 path. Scene = a shared parent
    // rotation over a grid of per-cube translations; composed model = rot·trans,
    // chain = [rot; trans] (root-first). Bit-identical => chain compose + order ok.
    let derivedChainTest () =
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

        let side = 6
        let offsets = [| for x in 0 .. side-1 do for y in 0 .. side-1 -> V3d(float (x-side/2) * 1.4, float (y-side/2) * 1.4, 0.0) |]
        let n = offsets.Length
        let parent = Trafo3d.Rotation(V3d(0.3, 0.6, 0.2).Normalized, 0.7) * Trafo3d.Scale 1.3
        let view = AVal.constant (CameraView.lookAt (V3d(0.0, -14.0, 9.0)) V3d.Zero V3d.OOI |> CameraView.viewTrafo)
        let proj = AVal.constant (Frustum.perspective 70.0 0.1 1.0e9 1.0 |> Frustum.projTrafo)
        let eff = Effect.compose [ Effect.ofFunction DF.shadeFp64; Effect.ofFunction DF.frag ]

        let imageOf (sg : ISg) =
            use task = sg |> Sg.compile runtime signature
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()

        // per-cube chain links [parent; translation] in Trafo3d compose order.
        let chainOf (o : V3d) = [| parent; Trafo3d.Translation o |]
        // composed: fold the SAME links with Trafo3d `*` (canonical definition).
        let composed =
            let models = offsets |> Array.map (fun o -> AVal.constant (chainOf o |> Array.reduce (*)))
            Heap.derivedFp64 runtime IndexedGeometryMode.TriangleList positions normals index eff view proj models
        // chained: the same links, composed on the GPU
        let chained =
            let chains = offsets |> Array.map (fun o -> chainOf o |> Array.map AVal.constant)
            Heap.derivedChainFp64 runtime IndexedGeometryMode.TriangleList positions normals index eff view proj chains

        let ca = imageOf composed
        let cb = imageOf chained
        let maxD, nDiff, nNonBg, total = diff ca cb
        Log.line "derivedChain: n=%d  composed-vs-chained maxDelta=%d diffPixels=%d/%d coverage=%d" n maxD nDiff total nNonBg
        let pass = nNonBg > 1000 && maxD <= 1
        if pass then Log.line "derivedChain: PASS (GPU chain compose == composed ModelTrafo, order correct)"
        else Log.warn "derivedChain: FAIL (maxDelta=%d diffPixels=%d coverage=%d)" maxD nDiff nNonBg
        pass

    // GPU transform propagation (Slice B): the fan-out is gone. N objects share
    // ONE root cval; changing it must re-upload exactly ONE distinct link slot
    // (the root), not N. The win that motivates the whole feature.
    let chainFanoutTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(512, 512))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.4)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>

        let nSide = 30
        let offsets = [| for x in 0 .. nSide-1 do for y in 0 .. nSide-1 -> V3d(float (x-nSide/2) * 0.9, float (y-nSide/2) * 0.9, 0.0) |]
        let n = offsets.Length
        // ONE shared root cval over all N objects (the worst-case fan-out)
        let root = AVal.init (Trafo3d.RotationZ 0.0)
        let view = AVal.constant (CameraView.lookAt (V3d(0.0, -40.0, 30.0)) V3d.Zero V3d.OOI |> CameraView.viewTrafo)
        let proj = AVal.constant (Frustum.perspective 70.0 0.1 1.0e9 1.0 |> Frustum.projTrafo)
        let eff = Effect.compose [ Effect.ofFunction DF.shadeFp64; Effect.ofFunction DF.frag ]
        let chains = offsets |> Array.map (fun o -> [| (root :> aval<Trafo3d>); AVal.constant (Trafo3d.Translation o) |])
        let sg = Heap.derivedChainFp64 runtime IndexedGeometryMode.TriangleList positions normals index eff view proj chains

        use task = sg |> Sg.compile runtime signature
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        try
            out.GetValue() |> ignore                            // initial: all distinct uploaded
            let initial = Heap.lastChainLinkUploads
            transact (fun () -> root.Value <- Trafo3d.RotationZ 0.6)
            out.GetValue() |> ignore                            // re-render after a single shared-root change
            let afterRoot = Heap.lastChainLinkUploads
            Log.line "chainFanout: n=%d distinct=%d initialUploads=%d afterRootChange=%d" n (n+1) initial afterRoot
            let pass = afterRoot = 1 && initial >= n
            if pass then Log.line "chainFanout: PASS (shared-root change uploads 1 link, not %d — fan-out gone)" n
            else Log.warn "chainFanout: FAIL (initial=%d afterRoot=%d, expected afterRoot=1)" initial afterRoot
            pass
        finally out.Release()

    // Graceful fallback: a mixed aset of heapable + un-heapable ROs. Heapable ones
    // collapse to buckets; the un-heapable one (here: no index buffer) must be
    // passed through UNCHANGED (same instance) in the output, not dropped or crashed.
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
        // 1 un-heapable RO: NO index buffer -> not eligible -> must pass through
        let odd =
            let ro = RenderObject()
            ro.Surface <- Surface.Effect eff
            ro.Mode <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices <- None
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = positions.Length, InstanceCount = 1) |])
            ro.Uniforms <- UniformProvider.ofList [
                Symbol.Create "ModelTrafo",    (AVal.constant Trafo3d.Identity :> IAdaptiveValue)
                Symbol.Create "ViewProjTrafo", vp ]
            ro :> IRenderObject

        let input = ASet.ofArray (Array.append heapable [| odd |])
        let outSet = Heap.ofRenderObjects runtime (Set.ofList [ "ModelTrafo" ]) input
        let out = outSet |> ASet.toAVal |> AVal.force |> HashSet.toArray
        let buckets = Heap.lastBucketCount
        let passedThrough = out |> Array.exists (fun o -> System.Object.ReferenceEquals(o, odd))
        Log.line "passthrough: in=5 (4 heapable + 1 odd) -> out=%d buckets=%d oddPassedThrough=%b" out.Length buckets passedThrough
        let pass = buckets = 1 && passedThrough && out.Length = buckets + 1
        if pass then Log.line "passthrough: PASS (heapable collapsed to 1 bucket; un-heapable RO passed through unchanged)"
        else Log.warn "passthrough: FAIL (out=%d buckets=%d passedThrough=%b)" out.Length buckets passedThrough
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
            let heap = Heap.ofRenderObjects runtime (Set.ofList [ "ModelTrafo" ]) (ASet.ofArray objs)
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
        let heapPix = imageOf (Heap.ofRenderObjects runtime (Set.ofList [ "ModelTrafo" ]) (ASet.ofArray objs))
        let buckets = Heap.lastBucketCount
        let maxD, nDiff, nNonBg, total = diff classicPix heapPix
        Log.line "varType: V4f pos + uint16 idx  buckets=%d classic-vs-heap maxDelta=%d diffPixels=%d/%d coverage=%d" buckets maxD nDiff total nNonBg
        let pass = buckets = 1 && nNonBg > 1000 && maxD <= 1
        if pass then Log.line "varType: PASS (V4f positions + uint16 indices heaped == classic; no type assumptions)"
        else Log.warn "varType: FAIL (buckets=%d maxDelta=%d coverage=%d)" buckets maxD nNonBg
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

    // Bindless heap geometry effect (shared by the bring-up tests below): geometry is
    // PULLED from per-object GPU buffers by handle (no vertex buffers bound).
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
        // WORLD-space triangles + a REAL camera (matches bindlessSimple) — the only
        // thing now differing from Heap.bindless is clean effect vs substituteReads rewrite.
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

    // STEP 4: the ACTUAL Heap.bindless, but with trivial 2-triangle geometry instead
    // of boxes. Steps 1-3 proved every mechanism works with a clean shader; this runs
    // the real rewritten-pull effect + ViewProjTrafo path on minimal geometry. If this
    // matches the plain render, the bug is the box geometry/index; if it fails, the bug
    // is Heap.bindless's shader/setup (the rewrite or ViewProj).
    let bindlessSimpleTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(640, 640))
        // NINE triangles in a 3x3 grid (matching the box test's object count/layout),
        // each a simple 3-vertex tri; flat normals toward the camera.
        let nrm = V3f(0.0f, -1.0f, 0.0f)
        let offsets = [| for x in -1 .. 1 do for y in -1 .. 1 -> V3f(float32 x * 1.3f, float32 y * 1.3f, 0.0f) |]
        let baseTri = [| V3f(-0.4f, 0.0f, -0.4f); V3f(0.4f, 0.0f, -0.4f); V3f(0.0f, 0.0f, 0.5f) |]
        let positions = offsets |> Array.map (fun o -> baseTri |> Array.map (fun p -> p + o))
        let normals   = offsets |> Array.map (fun _ -> Array.replicate 3 nrm)
        let indices   = offsets |> Array.map (fun _ -> [| 0;1;2 |])
        let view = CameraView.lookAt (V3d(0.0, -8.0, 6.0)) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo
        let vpTrafo = view * proj
        let eff = Effect.compose [ Effect.ofFunction BH.shade; Effect.ofFunction BH.frag ]
        let imageOf (sg : ISg) =
            use task = sg |> Sg.compile runtime signature
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let refObjs =
            positions |> Array.mapi (fun i pos ->
                let ro = RenderObject()
                ro.Surface <- Surface.Effect eff
                ro.Mode <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, bv pos typeof<V3f>; DefaultSemantic.Normals, bv normals.[i] typeof<V3f> ]
                ro.Indices <- Some (bv indices.[i] typeof<int>)
                ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = 3, InstanceCount = 1) |])
                ro.Uniforms <- UniformProvider.ofList [ Symbol.Create "ViewProjTrafo", (AVal.constant (M44f.op_Explicit vpTrafo.Forward) :> IAdaptiveValue) ]
                ro :> IRenderObject)
        // render the bindless FIRST (before the plain ref compiles the same `eff`),
        // to test whether the original effect's cached shader poisons the rewritten one
        let bindlessPix =
            let attribs = Array.init positions.Length (fun i -> Map.ofList [ DefaultSemantic.Positions, (positions.[i] :> System.Array); DefaultSemantic.Normals, (normals.[i] :> System.Array) ])
            let idxArrs = indices |> Array.map (fun a -> a :> System.Array)
            imageOf (Heap.bindless runtime IndexedGeometryMode.TriangleList eff attribs idxArrs (AVal.constant view) (AVal.constant proj))
        let refPix = imageOf (Sg.renderObjectSet (ASet.ofArray refObjs))
        let savePpm (path : string) (img : PixImage<uint8>) =
            let mm = img.GetMatrix<C4b>()
            let w = int mm.Size.X
            let h = int mm.Size.Y
            use fs = new System.IO.FileStream(path, System.IO.FileMode.Create)
            let hdr = System.Text.Encoding.ASCII.GetBytes(sprintf "P6\n%d %d\n255\n" w h)
            fs.Write(hdr, 0, hdr.Length)
            let buf : byte[] = Array.zeroCreate (w * h * 3)
            let mutable o = 0
            for yy in 0 .. h - 1 do
                for xx in 0 .. w - 1 do
                    let c = mm.[V2l(int64 xx, int64 yy)]
                    buf.[o] <- c.R; buf.[o+1] <- c.G; buf.[o+2] <- c.B; o <- o + 3
            fs.Write(buf, 0, buf.Length)
        savePpm "/tmp/bsimple_ref.ppm" refPix
        savePpm "/tmp/bsimple_heap.ppm" bindlessPix
        let maxD, nDiff, nNonBg, total = diff refPix bindlessPix
        Log.line "bindlessSimple: 9 triangles  plain-vs-bindless maxDelta=%d diffPixels=%d coverage=%d" maxD nDiff nNonBg
        let pass = nNonBg > 500 && maxD <= 1
        if pass then Log.line "bindlessSimple: PASS (Heap.bindless shader/setup correct on trivial geometry)"
        else Log.warn "bindlessSimple: FAIL (maxDelta=%d coverage=%d)" maxD nNonBg
        pass

    let bindlessHeapTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(640, 640))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let bpos = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let bnor = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let bidx = g.IndexArray |> unbox<int[]>
        let offsets = [| for x in -1 .. 1 do for y in -1 .. 1 -> V3f(float32 x * 1.3f, float32 y * 1.3f, 0.0f) |]
        let n = offsets.Length
        // per-object WORLD-space geometry (offset baked in; no model trafo)
        let positions = offsets |> Array.map (fun o -> bpos |> Array.map (fun p -> p + o))
        let normals   = offsets |> Array.map (fun _ -> bnor)
        let indices   = offsets |> Array.map (fun _ -> bidx)
        let view = CameraView.lookAt (V3d(0.0, -8.0, 6.0)) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo
        let vpTrafo = view * proj
        let eff = Effect.compose [ Effect.ofFunction BH.shade; Effect.ofFunction BH.frag ]

        let imageOf (sg : ISg) =
            use task = sg |> Sg.compile runtime signature
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()

        // reference: plain per-object indexed render (real vertex buffers)
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let refObjs =
            positions |> Array.map (fun pos ->
                let ro = RenderObject()
                ro.Surface <- Surface.Effect eff
                ro.Mode <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, bv pos typeof<V3f>; DefaultSemantic.Normals, bv bnor typeof<V3f> ]
                ro.Indices <- Some (bv bidx typeof<int>)
                ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = bidx.Length, InstanceCount = 1) |])
                ro.Uniforms <- UniformProvider.ofList [ Symbol.Create "ViewProjTrafo", (AVal.constant (M44f.op_Explicit vpTrafo.Forward) :> IAdaptiveValue) ]
                ro :> IRenderObject)
        let refPix = imageOf (Sg.renderObjectSet (ASet.ofArray refObjs))

        // bindless: same geometry pulled from per-object buffers by handle
        let bindlessPix =
            let attribs = Array.init positions.Length (fun i -> Map.ofList [ DefaultSemantic.Positions, (positions.[i] :> System.Array); DefaultSemantic.Normals, (normals.[i] :> System.Array) ])
            let idxArrs = indices |> Array.map (fun a -> a :> System.Array)
            imageOf (Heap.bindless runtime IndexedGeometryMode.TriangleList eff attribs idxArrs (AVal.constant view) (AVal.constant proj))

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
        savePpm "/tmp/bindless_ref.ppm" refPix
        savePpm "/tmp/bindless_heap.ppm" bindlessPix
        let maxD, nDiff, nNonBg, total = diff refPix bindlessPix
        Log.line "bindlessHeap: n=%d  plain-vs-bindless maxDelta=%d diffPixels=%d/%d coverage=%d" n maxD nDiff total nNonBg
        let pass = nNonBg > 1000 && maxD <= 1
        if pass then Log.line "bindlessHeap: PASS (vertex-pulled bindless geometry == plain indexed render)"
        else Log.warn "bindlessHeap: FAIL (maxDelta=%d diffPixels=%d coverage=%d)" maxD nDiff nNonBg
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

    // Type-agnostic / integral-attribute proof: V3f Positions + an INTEGRAL V3i "Tint"
    // attribute (decoded via the int view of the same buffer) + uint16 indices. Each
    // box gets a distinct integer tint; seeing all three colors proves int decode,
    // mixed float/int attributes in one buffer, and 16-bit indices all work.
    module BVI =
        type VIn  = { [<Semantic("Positions")>] pos : V3f; [<Semantic("Tint")>] tint : V3i }
        type VOut = { [<Position>] clip : V4f; [<Semantic("Col")>] col : V3f }
        let shade (v : VIn) =
            vertex {
                let vp : M44f = uniform?ViewProjTrafo
                return { clip = vp * V4f(v.pos, 1.0f)
                         col  = V3f(float32 v.tint.X, float32 v.tint.Y, float32 v.tint.Z) / 255.0f }
            }
        let frag (v : VOut) = fragment { return V4f(v.col, 1.0f) }

    let bindlessVarTest () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(640, 640))
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let bpos = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let bidx = g.IndexArray |> unbox<int[]>
        let offsets = [| for x in -1 .. 1 do for y in -1 .. 1 -> V3f(float32 x * 1.3f, float32 y * 1.3f, 0.0f) |]
        let n = offsets.Length
        let palette = [| V3i(230, 40, 40); V3i(40, 210, 80); V3i(60, 120, 235) |]
        let positions = offsets |> Array.map (fun o -> bpos |> Array.map (fun p -> p + o))
        let tints     = offsets |> Array.mapi (fun i _ -> Array.replicate bpos.Length palette.[i % palette.Length])  // V3i per vertex
        let idx16     = offsets |> Array.map (fun _ -> bidx |> Array.map uint16)                                     // uint16 indices
        let attribs   = Array.init n (fun i -> Map.ofList [ DefaultSemantic.Positions, (positions.[i] :> System.Array)
                                                            Symbol.Create "Tint",       (tints.[i]     :> System.Array) ])
        let idxArrs   = idx16 |> Array.map (fun a -> a :> System.Array)
        let view = CameraView.lookAt (V3d(0.0, -8.0, 6.0)) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 100.0 1.0 |> Frustum.projTrafo
        let eff = Effect.compose [ Effect.ofFunction BVI.shade; Effect.ofFunction BVI.frag ]
        let pix =
            use task = Heap.bindless runtime IndexedGeometryMode.TriangleList eff attribs idxArrs (AVal.constant view) (AVal.constant proj) |> Sg.compile runtime signature
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let m = pix.GetMatrix<C4b>()
        let mutable r = 0L
        let mutable gr = 0L
        let mutable b = 0L
        m.ForeachCoord(fun (p : V2l) ->
            let v = m.[p]
            if v.R > 150uy && v.G < 110uy && v.B < 110uy then r  <- r + 1L
            if v.G > 150uy && v.R < 110uy then gr <- gr + 1L
            if v.B > 150uy && v.R < 110uy then b  <- b + 1L)
        Log.line "bindlessVar: V3i tint + uint16 idx -> red=%d green=%d blue=%d px" r gr b
        let pass = r > 300L && gr > 300L && b > 300L
        if pass then Log.line "bindlessVar: PASS (integral V3i attribute decoded + uint16 indices + mixed float/int buffer)"
        else Log.warn "bindlessVar: FAIL (red=%d green=%d blue=%d — integral decode or uint16 index broken)" r gr b
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
        let heapObjs = Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo" ]) (ASet.ofArray ros)
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
        let heapObjs = Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo" ]) (ASet.ofArray heapInputROs)
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
        let heapObjs = Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo" ]) (ASet.ofArray ros)
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
        let heapObjs = Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo" ]) (ASet.ofArray ros)
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
        let heapObjs = Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo"; "HeapColor" ]) (ASet.ofArray ros)
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
        let heapObjs = Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo" ]) (ASet.ofArray ros)
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
        let heapObjs = Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo" ]) (ASet.ofArray ros)
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
        let heapObjs = Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo" ]) (ASet.ofArray ros)
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
        let heap = Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo"; "HeapColor" ]) (ASet.ofArray inputs)
        heap |> ASet.toAVal |> AVal.force |> ignore
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
        let heap = Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo"; "HeapColor" ]) (ASet.ofArray inputs)
        let force () = heap |> ASet.toAVal |> AVal.force |> ignore; Heap.lastBucketCount
        let b0 = force ()
        transact (fun () -> for i in 0 .. n-1 do if i % 2 = 0 then culls.[i].Value <- CullMode.Back)
        let b1 = force ()
        transact (fun () -> for i in 0 .. n-1 do culls.[i].Value <- CullMode.None)
        let b2 = force ()
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
        let heapPix = imageOf (Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo"; "HeapColor" ]) (ASet.ofArray inputs))
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
        let imgInst = imageOf (Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo"; "HeapColor" ]) (ASet.ofArray instancedInputs))
        let instBuckets = Heap.lastBucketCount

        // reference: 8 plain ROs (offset baked into the trafo), instanceCount=1
        let effPlain = Effect.compose [ Effect.ofFunction AI.shadePlain; Effect.ofFunction AI.frag ]
        let plainInputs =
            [| for (p, c) in bases do
                 for i in 0 .. k-1 ->
                   mkRO effPlain [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p * Trafo3d.Translation(float i * 1.5, 0.0, 0.0)).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                                   Symbol.Create "HeapColor",      (AVal.constant (c.ToV4f()) :> IAdaptiveValue)
                                   Symbol.Create "ViewProjTrafo",  viewProj ] 1 |]
        let imgPlain = imageOf (Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo"; "HeapColor" ]) (ASet.ofArray plainInputs))

        let maxD, nDiff, nNonBg, total = diff imgPlain imgInst
        Log.line "already-instanced: 2 ROs x %d instances (%d bucket, gl_DrawID routing) vs 8 plain ROs  maxDelta=%d diffPixels=%d coverage=%d" k instBuckets maxD nDiff nNonBg
        let pass = maxD <= 1 && nNonBg > 500L && instBuckets = 1
        if pass then Log.line "already-instanced: PASS (instanced inputs == non-instanced expansion; per-draw routed by gl_DrawID)"
        else Log.warn "already-instanced: FAIL (maxDelta=%d nNonBg=%d buckets=%d)" maxD nNonBg instBuckets
        pass

    // Verifies per-instance heap rendering (instanceCount > 1): the SAME per-
    // instance data rendered via Heap.instanced (one instanced draw) and via
    // Heap.scene (N indirect sub-draws) must be pixel-identical — both index the
    // same arena, one by gl_InstanceIndex (instance id), the other by firstInstance.
    let instancingTest () =
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
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 16.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj)
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan |]
        let side = 7
        let instances =
            [| for x in 0 .. side-1 do
                 for y in 0 .. side-1 ->
                   let i = x * side + y
                   let p = V3d(float (x-side/2) * 1.4, float (y-side/2) * 1.4, 0.0)
                   Map.ofList [
                     "HeapModelTrafo", Heap.mat4 (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit))
                     "HeapColor",      Heap.v4   (AVal.constant (palette.[i % palette.Length].ToV4f())) ] |]
        let n = instances.Length
        let effect = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        let imageOf (sg : ISg) =
            use task = sg |> Sg.compile runtime signature
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>() finally out.Release()
        let imgInst  = imageOf (Heap.instanced IndexedGeometryMode.TriangleList positions normals index effect instances |> Sg.uniform "ViewProjTrafo" viewProj)
        let imgScene = imageOf (Heap.scene     IndexedGeometryMode.TriangleList positions normals index effect instances |> Sg.uniform "ViewProjTrafo" viewProj)
        let maxD, nDiff, nNonBg, total = diff imgScene imgInst
        Log.line "instancing: %d instances (1 instanced draw)  vs scene (multidraw)  maxDelta=%d diffPixels=%d/%d coverage=%d" n maxD nDiff total nNonBg
        let pass = maxD <= 1 && nNonBg > total / 100L
        if pass then Log.line "instancing: PASS (instanceCount=%d instanced draw == multidraw scene, per-instance arena data)" n
        else Log.warn "instancing: FAIL (maxDelta=%d nNonBg=%d)" maxD nNonBg
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

        let run (label : string) (n : int) (names : Set<string>) (mkRO : int -> IRenderObject) =
            let all = Array.init (n + warmup + frames) mkRO
            let ros = cset (Array.sub all 0 n)
            let heapObjs = Heap.ofRenderObjects runtime names (ros :> aset<_>)
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
        run "textured" n (Set.ofList [ "HeapModelTrafo" ]) mkTexRO

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
        run "bindless-geom" (min n 480) (Set.ofList [ "HeapModelTrafo"; "HeapColor" ]) mkGpuRO
        true
