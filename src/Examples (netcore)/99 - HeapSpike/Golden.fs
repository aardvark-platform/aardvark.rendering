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
        let texArrayU = AVal.constant texArray :> IAdaptiveValue
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
