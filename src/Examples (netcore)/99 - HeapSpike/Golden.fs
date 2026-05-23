namespace HeapSpike

// Golden-image equivalence: render the SAME scene twice — once classic (N
// independent render objects, per-draw uniforms in UBOs), once heap (N -> B
// bucket ROs, per-draw uniforms gathered from the shared arena via the
// auto-rewritten shader) — read both back and compare pixel-by-pixel.
//
// Both paths use the identical effect (Shaders.shade/shadeFrag); the heap
// rewrite changes only WHERE the per-draw uniforms come from (UBO -> SSBO
// gather), not the math, so the images must be (near) identical. MSAA is off
// for a deterministic comparison.

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Aardvark.Application
open FShade

module Golden =

    // max per-channel abs delta and fraction of differing pixels between two
    // RGBA8 images of equal size
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
            // count non-background (anything the cubes actually drew on top of clear)
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
        let posBV = BufferView(AVal.constant (ArrayBuffer(positions) :> IBuffer), typeof<V3f>)
        let norBV = BufferView(AVal.constant (ArrayBuffer(normals)   :> IBuffer), typeof<V3f>)
        let idxBV = BufferView(AVal.constant (ArrayBuffer(index)     :> IBuffer), typeof<int>)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, posBV; DefaultSemantic.Normals, norBV ]

        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 18.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj)

        let effect = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan |]

        let n = 256
        let s = int (ceil (sqrt (float n)))
        let trafoAt i =
            let x = i % s
            let y = i / s
            (Trafo3d.Translation(float (x - s/2) * 1.2, float (y - s/2) * 1.2, 0.0)
             * Trafo3d.RotationZ(float i * 0.13)).Forward |> M44f.op_Explicit

        let inputs =
            Array.init n (fun i ->
                let ro = RenderObject()
                ro.Surface   <- Surface.Effect effect
                ro.Mode      <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- vattrs
                ro.Indices   <- Some idxBV
                ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
                ro.Uniforms  <-
                    UniformProvider.ofList [
                        Symbol.Create "HeapModelTrafo", (AVal.constant (trafoAt i) :> IAdaptiveValue)
                        Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                        Symbol.Create "ViewProjTrafo",  (viewProj :> IAdaptiveValue)
                    ]
                ro :> IRenderObject)

        let renderToPix (objs : aset<IRenderObject>) =
            use task = runtime.CompileRender(signature, objs)
            let out = task |> RenderTask.renderToColor size
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>()
            finally out.Release()

        let classicPix = renderToPix (ASet.ofArray inputs)
        let heapObjs   = Heap.ofRenderObjects runtime (Set.ofList [ "HeapModelTrafo"; "HeapColor" ]) (ASet.ofArray inputs)
        let heapPix    = renderToPix heapObjs

        let maxDelta, nDiff, nNonBg, total = diff classicPix heapPix

        Log.line "golden: %d objects -> %d bucket(s)" n Heap.lastBucketCount
        Log.line "golden: classic vs heap  maxChannelDelta=%d  diffPixels=%d/%d (%.4f%%)  coverage=%d px"
            maxDelta nDiff total (100.0 * float nDiff / float total) nNonBg

        // best-effort persist for inspection (needs an image encoder; ignore if none)
        let dir = System.IO.Path.GetTempPath()
        let pc = System.IO.Path.Combine(dir, "heap-golden-classic.png")
        let ph = System.IO.Path.Combine(dir, "heap-golden-heap.png")
        try classicPix.SaveAsPng pc; heapPix.SaveAsPng ph; Log.line "golden: wrote %s and %s" pc ph
        with _ -> Log.line "golden: (image encoder unavailable; skipped png export)"

        // pass criterion: pixel-identical up to a tiny rounding tolerance, and
        // the scene actually drew something (guards against two blank images)
        let pass = maxDelta <= 1 && nNonBg > total / 100L
        if pass then Log.line "golden: PASS (heap render is pixel-equivalent to classic)"
        else Log.warn  "golden: FAIL (maxDelta=%d nNonBg=%d)" maxDelta nNonBg
        pass
