namespace HeapSpike

// GPU transformation chain versus CPU-folded ModelTrafo on the live
// Heap.ofRenderObjects path, with a two-level transformation stack per leaf
// (one constant box link plus one dynamic node link). Measurement protocol:
// offscreen framebuffer, GPU time via ITimeQuery through the RenderToken, an
// explicit task.Run per frame, reported as edit / frame / GPU milliseconds over
// log-spaced sweeps:
//
//   trafo sweep (k = 1, 100, 1000): edit k node links per frame. The folded path
//     re-folds box*node per touched leaf (a per-leaf AVal.map2) and re-packs k
//     arena ModelTrafo regions; the chain path re-packs k link slots (no CPU
//     fold) and the GPU re-composes. The chain path is expected to remove the
//     per-leaf CPU refold.
//   churn sweep (r = 1, 100, 1000): remove r and add r leaves per frame. The
//     folded path allocates r fresh box*node map2 values and r ModelTrafo
//     regions; the chain path interns r links (the box deduplicates to the one
//     shared slot) and r chain-index runs. No per-add map2 allocation is
//     expected on the chain path, and the box link stays one slot.
//   orbit sweep: the camera moves every frame while the models stay static.
//
// Options: `--folded` measures the folded path (render objects without the
// stack); by default the chain path is measured (render objects with a
// ModelTrafoStack). `--n <count>` sets the number of leaves (default 20000).
// Same inputs either way.

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Aardvark.Application
open FShade
open System.Diagnostics
open Aardvark.Rendering.Tests.Rendering.Heap

module ChainBench =

    let mutable private rngState = 0x9E3779B9u
    let private rnd (bound : int) =
        let mutable x = rngState
        x <- x ^^^ (x <<< 13)
        x <- x ^^^ (x >>> 17)
        x <- x ^^^ (x <<< 5)
        rngState <- x
        int (x % uint32 bound)

    let run (chain : bool) (n : int) =
        Aardvark.Init()
        use app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication()
        let runtime = app.Runtime :> IRuntime
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let sizeI = 1024
        let size = V2i(sizeI, sizeI)

        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.Unit) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let eff = Effect.compose [ Effect.ofFunction Golden.DF.shadeFp64; Effect.ofFunction Golden.DF.frag ]

        let side = int (ceil (sqrt (float n)))
        let center = V3d.Zero
        let up = V3d.OOI
        let dist = float side * 1.5
        // View is a cval (the orbit sweep rotates it); View and Proj are supplied
        // separately so ViewProjTrafo / ModelViewProjTrafo are derived on the GPU;
        // a camera move then re-runs the per-slot compose.
        let view = AVal.init (CameraView.lookAt (V3d(0.0, -1.0, 1.0) * dist) center up |> CameraView.viewTrafo)
        let proj = AVal.constant (Frustum.perspective 70.0 0.1 1.0e9 1.0 |> Frustum.projTrafo)

        // box shape: a distinct AVal.constant per leaf with an identical value;
        // value deduplication collapses them to one arena/link slot.
        let boxValue = Trafo3d.Scale(0.8, 0.8, 1.4) * Trafo3d.Translation(0.1, 0.1, 0.0)
        let boxLink () : aval<Trafo3d> = AVal.constant boxValue
        let baseTrafo i = Trafo3d.Translation(float (i % side) * 1.2, float (i / side) * 1.2, 0.0)

        // each leaf: a node cval, the folded ModelTrafo (node*box, the CPU fold the
        // folded path pays per touched leaf), the normal matrix and, on the chain
        // path, the unfolded stack.
        let mkLeaf (i : int) =
            let node = AVal.init (baseTrafo i)
            let folded = AVal.map2 (*) (node :> aval<Trafo3d>) (boxLink ())
            let nm = folded |> AVal.map (fun (t : Trafo3d) -> M44f.op_Explicit (M44d (M33d t.Backward.Transposed)))
            let us =
                [ Symbol.Create "ModelTrafo",    (folded :> IAdaptiveValue)
                  Symbol.Create "NormalMatrix",  (nm :> IAdaptiveValue)
                  Symbol.Create "ViewTrafo",     (view :> IAdaptiveValue)
                  Symbol.Create "ProjTrafo",     (proj :> IAdaptiveValue) ]
            let us =
                if chain then (Symbol.Create "ModelTrafoStack", (AVal.constant [| (node :> aval<Trafo3d>); boxLink () |] :> IAdaptiveValue)) :: us
                else us
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect eff
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices   <- Some (bv index typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList us
            struct(node, ro :> IRenderObject)

        let leaves = Array.init n mkLeaf
        let nodes  = leaves |> Array.map (fun (struct(nd, _)) -> nd)
        let ros    = leaves |> Array.map (fun (struct(_, ro)) -> ro)
        let liveSet = cset (ros :> seq<_>)
        let objects = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (liveSet :> aset<_>)

        let colorTex = runtime.CreateTexture2D(size, TextureFormat.Rgba8)
        let depthTex = runtime.CreateTexture2D(size, TextureFormat.Depth24Stencil8)
        let fbo =
            runtime.CreateFramebuffer(signature, Map.ofList [
                DefaultSemantic.Colors,       colorTex.GetOutputView()
                DefaultSemantic.DepthStencil, depthTex.GetOutputView() ])
        let output = OutputDescription.ofFramebuffer fbo
        use task =
            RenderTask.ofList [
                runtime.CompileClear(signature, clear { color (C4f(0.06, 0.07, 0.09)); depth 1.0 })
                runtime.CompileRender(signature, objects) ]
        let gpuQuery = runtime.CreateTimeQuery()
        let token = { RenderToken.Empty with Queries = [ gpuQuery ] }
        let renderFrame () =
            task.Run(AdaptiveToken.Top, token, output)
            (gpuQuery.GetResult((), reset = true)).TotalMilliseconds

        let sw = Stopwatch.StartNew()
        let _ = renderFrame ()
        Log.line "chainBench[%s]: n=%d first frame %.0f ms  buckets=%d chainBuckets=%d distinctLinks=%d"
            (if chain then "chain" else "folded") n sw.Elapsed.TotalMilliseconds
            Heap.lastBucketCount Heap.lastChainBuckets (if chain then Heap.lastDistinctLinks else 0)

        let warmup = 20
        let frames = 80

        // trafo sweep
        let angles = Array.zeroCreate<float> n
        let doTrafo (k : int) =
            transact (fun () ->
                for _ in 1 .. k do
                    let i = rnd n
                    angles.[i] <- angles.[i] + 0.05
                    nodes.[i].Value <- Trafo3d.RotationZ angles.[i] * baseTrafo i)
        Log.line "chainBench[%s]: trafo sweep" (if chain then "chain" else "folded")
        for k in [ 1; 100; 1000 ] do
            let mutable edit = 0.0
            let mutable frame = 0.0
            let mutable gpu = 0.0
            for f in 1 .. warmup + frames do
                sw.Restart(); doTrafo k
                let e = sw.Elapsed.TotalMilliseconds
                sw.Restart()
                let gp = renderFrame ()
                let fr = e + sw.Elapsed.TotalMilliseconds
                if f > warmup then edit <- edit + e; frame <- frame + fr; gpu <- gpu + gp
            Log.line "  k=%-5d edit %.3f ms  frame %.3f ms  gpu %.3f ms" k (edit / float frames) (frame / float frames) (gpu / float frames)

        // churn sweep
        let mutable pool = System.Collections.Generic.List<IRenderObject>(ros)
        let doChurn (r : int) =
            transact (fun () ->
                for _ in 1 .. r do
                    let idx = rnd pool.Count
                    let old = pool.[idx]
                    pool.RemoveAt idx
                    liveSet.Remove old |> ignore
                    let i = rnd n
                    let (struct(_, fresh)) = mkLeaf i
                    pool.Add fresh
                    liveSet.Add fresh |> ignore)
        Log.line "chainBench[%s]: churn sweep" (if chain then "chain" else "folded")
        for r in [ 1; 100; 1000 ] do
            let mutable edit = 0.0
            let mutable frame = 0.0
            let mutable gpu = 0.0
            for f in 1 .. warmup + frames do
                sw.Restart(); doChurn r
                let e = sw.Elapsed.TotalMilliseconds
                sw.Restart()
                let gp = renderFrame ()
                let fr = e + sw.Elapsed.TotalMilliseconds
                if f > warmup then edit <- edit + e; frame <- frame + fr; gpu <- gpu + gp
            Log.line "  r=%-5d edit %.3f ms  frame %.3f ms  gpu %.3f ms  chainBuckets=%d distinct=%d"
                r (edit / float frames) (frame / float frames) (gpu / float frames) Heap.lastChainBuckets (if chain then Heap.lastDistinctLinks else 0)

        // orbit sweep: the camera moves every frame, the models are static. View/Proj
        // are derived constituents, so a camera move re-runs the compose per slot but
        // skips the chain fold (the model is unchanged).
        Log.line "chainBench[%s]: orbit sweep" (if chain then "chain" else "folded")
        let mutable oe = 0.0
        let mutable ofr = 0.0
        let mutable ogp = 0.0
        for f in 1 .. warmup + frames do
            let ang = float f * 0.03
            let eye = center + V3d(cos ang, sin ang, 0.7) * dist
            sw.Restart()
            transact (fun () -> view.Value <- CameraView.lookAt eye center up |> CameraView.viewTrafo)
            let e = sw.Elapsed.TotalMilliseconds
            sw.Restart()
            let gp = renderFrame ()
            let fr = e + sw.Elapsed.TotalMilliseconds
            if f > warmup then oe <- oe + e; ofr <- ofr + fr; ogp <- ogp + gp
        Log.line "  orbit  edit %.3f ms  frame %.3f ms  gpu %.3f ms" (oe / float frames) (ofr / float frames) (ogp / float frames)
        0
