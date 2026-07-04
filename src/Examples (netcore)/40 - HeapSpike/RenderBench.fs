namespace HeapSpike

// GPU render-time benchmark on SYNTHETIC data: what does the heap's per-vertex
// decode/gather actually cost vs the LOWER-BOUND baseline — ONE baked world-space
// soup mesh drawn with a plain VP-only shader (the CadSceneDemo `Baseline` floor)?
// Both paths shade the SAME vertex count non-indexed and the same pixels; the
// heap additionally pays: index decode, attribute pulls, per-draw uniform gathers
// (ModelTrafo is a real DERIVED field — fp64 compute collapse — like production),
// and N indirect draw records instead of 1 draw.
//
//   renderbench [--n 100000] [--size 1024] [--frames 60] [--min-tris 10] [--max-tris 100] [--classic]
//
// Measures REAL GPU time per frame via time queries (RenderToken.Queries — same
// method as CadSceneDemo's Offscreen.run), plus task.Run CPU time. Variants run
// SEQUENTIALLY (each torn down before the next) so peak memory doesn't stack.
//
// Vienna-shaped data: every object carries its OWN unique geometry (the heap
// dedups nothing) with a random triangle count in [--min-tris, --max-tris] —
// like a CAD scene of many small distinct parts.

module RenderBench =

    open System
    open System.Diagnostics
    open Aardvark.Base
    open Aardvark.Rendering
    open Aardvark.SceneGraph
    open FSharp.Data.Adaptive
    open Aardvark.Application
    open FShade

    [<AutoOpen>]
    module private Sh =
        type V = {
            [<Position>] pos : V4f
            [<Normal>]   n   : V3f
            [<Color>]    c   : V4f
        }

        /// heap path: per-object ModelTrafo (derived, compute-collapsed) + shared ViewProjTrafo
        let heapVert (v : V) =
            vertex {
                let wp = uniform.ModelTrafo * v.pos
                return { v with pos = uniform.ViewProjTrafo * wp; n = uniform.ModelTrafo.TransformDir v.n }
            }

        /// like heapVert plus a SECOND per-object matrix gather (`--second-matrix`):
        /// measures the marginal cost of one more per-vertex M44f field read.
        let heapVert2 (v : V) =
            vertex {
                let m2 : M44f = uniform?SecondTrafo
                let wp = uniform.ModelTrafo * (m2 * v.pos)
                return { v with pos = uniform.ViewProjTrafo * wp; n = uniform.ModelTrafo.TransformDir v.n }
            }

        /// baseline path: positions/normals baked to world space, VP only
        let bakedVert (v : V) =
            vertex {
                return { v with pos = uniform.ViewProjTrafo * v.pos }
            }

        let lit (v : V) =
            fragment {
                let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
                let d = 0.25f + 0.75f * max 0.0f (Vec.dot (Vec.normalize v.n) l)
                return V4f(v.c.XYZ * d, 1.0f)
            }

    /// GPU ms/frame (time query) + task.Run CPU ms. Bandwidth-bound passes are very
    /// sensitive to the GPU's memory-clock power state, so: (1) a 30-frame WARMUP
    /// ramps the clocks before anything is timed, (2) `frames` timed frames are
    /// collected in 3 ROUNDS, (3) the MEDIAN round is reported (min in parens shows
    /// the full-clock floor). Heap-vs-baseline ratios use the medians.
    let private measure (runtime : IRuntime) (task : IRenderTask) (fbo : IFramebuffer) (frames : int) =
        let gpuQuery = runtime.CreateTimeQuery()
        let token = { RenderToken.Empty with Queries = [ gpuQuery ] }
        let output = OutputDescription.ofFramebuffer fbo
        task.Run(AdaptiveToken.Top, token, output)
        gpuQuery.GetResult((), reset = true) |> ignore                       // build + first submit
        for _ in 1 .. 30 do                                                  // clock ramp-up
            task.Run(AdaptiveToken.Top, token, output)
            gpuQuery.GetResult((), reset = true) |> ignore
        let round () =
            let sw = Stopwatch()
            let mutable gpu = 0.0
            for _ in 1 .. frames do
                sw.Start()
                task.Run(AdaptiveToken.Top, token, output)
                sw.Stop()
                gpu <- gpu + (gpuQuery.GetResult((), reset = true)).TotalMilliseconds
            gpu / float frames, sw.Elapsed.TotalMilliseconds / float frames
        let rounds = Array.init 3 (fun _ -> round ())
        let byGpu = rounds |> Array.sortBy fst
        let (medGpu, medCpu) = byGpu.[1]
        let (minGpu, _) = byGpu.[0]
        medGpu, medCpu, minGpu

    let run (argv : string[]) =
        let arg (name : string) (dflt : int) =
            match argv |> Array.tryFindIndex ((=) name) with
            | Some i when i + 1 < argv.Length -> int argv.[i + 1]
            | _ -> dflt
        let n        = arg "--n" 100000
        let sizePx   = arg "--size" 1024
        let frames   = arg "--frames" 60
        let minTris  = max 1 (arg "--min-tris" 10)
        let maxTris  = max minTris (arg "--max-tris" 100)
        let classic  = argv |> Array.contains "--classic"
        let secondMatrix = argv |> Array.contains "--second-matrix"

        Aardvark.Init()

        // ── synthetic scene (Vienna-shaped): n objects in a grid, EVERY object its
        //    own UNIQUE geometry (fresh arrays — the heap dedups nothing) with a
        //    random triangle count in [minTris, maxTris]. Each mesh is a cone fan:
        //    apex + ring, T triangles, indexed. ──
        let s = int (ceil (sqrt (float n)))
        let extent = float s * 1.2
        let posOf (i : int) = V3d(float (i % s - s/2) * 1.2, float (i / s - s/2) * 1.2, 0.0)
        let palette = [| C4b(230,60,60); C4b(60,200,60); C4b(60,120,230); C4b(230,200,40); C4b(210,60,210); C4b(40,210,210) |]
        let rnd = RandomSystem 42
        let mkMesh () =
            let t = minTris + rnd.UniformInt (maxTris - minTris + 1)
            let ps = Array.zeroCreate<V3f> (t + 1)
            let ns = Array.zeroCreate<V3f> (t + 1)
            ps.[0] <- V3f(0.0f, 0.0f, 0.45f)
            ns.[0] <- V3f.OOI
            for k in 0 .. t - 1 do
                let a = float32 k / float32 t * float32 Constant.PiTimesTwo
                ps.[k + 1] <- V3f(0.4f * cos a, 0.4f * sin a, 0.0f)
                ns.[k + 1] <- Vec.normalize (V3f(cos a, sin a, 0.7f))
            let idx = Array.zeroCreate<int> (t * 3)
            for k in 0 .. t - 1 do
                idx.[k * 3]     <- 0
                idx.[k * 3 + 1] <- 1 + k
                idx.[k * 3 + 2] <- 1 + ((k + 1) % t)
            ps, ns, idx
        let meshes = Array.init n (fun _ -> mkMesh ())
        let totalDrawnVerts = meshes |> Array.sumBy (fun (_, _, idx) -> int64 idx.Length)
        Log.line "renderbench: n=%d  tris/obj in [%d, %d] (unique geometry each)  %.1f M drawn verts total  %dx%d px  %d frames"
            n minTris maxTris (float totalDrawnVerts / 1e6) sizePx sizePx frames

        let bv (a : Array) (t : Type) = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let heapEffect  =
            Effect.compose [
                (if secondMatrix then Effect.ofFunction Sh.heapVert2 else Effect.ofFunction Sh.heapVert)
                Effect.ofFunction Sh.lit ]
        let bakedEffect = Effect.compose [ Effect.ofFunction Sh.bakedVert; Effect.ofFunction Sh.lit ]

        let mkHeapRO (viewProj : IAdaptiveValue) (i : int) =
            let (ps, ns, idx) = meshes.[i]
            let ro = RenderObject()
            ro.Surface <- Surface.Effect heapEffect
            ro.Mode    <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <-
                AttributeProvider.ofList [
                    DefaultSemantic.Positions, bv ps typeof<V3f>
                    DefaultSemantic.Normals,   bv ns typeof<V3f>
                    // per-object singleton color attribute (length-1 broadcast)
                    DefaultSemantic.Colors,    BufferView(SingleValueBuffer<C4b>(AVal.constant palette.[i % palette.Length]), typeof<C4b>) ]
            ro.Indices   <- Some (bv idx typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = idx.Length, InstanceCount = 1) |])
            ro.Uniforms  <-
                UniformProvider.ofList [
                    yield Symbol.Create "ModelTrafo",    (AVal.constant (Trafo3d.Translation (posOf i)) :> IAdaptiveValue)
                    yield Symbol.Create "ViewProjTrafo", viewProj
                    // fresh aval per RO: DISTINCT region per slot (dedup is by aval
                    // identity), so the gather reads per-object offsets like a real
                    // per-part matrix would
                    if secondMatrix then
                        yield Symbol.Create "SecondTrafo", (AVal.constant M44f.Identity :> IAdaptiveValue) ]
            ro :> IRenderObject

        if argv |> Array.contains "--window" then
            // ── `--window`: the heap scene in a REAL GameWindow with a turntable
            //    camera — for Nsight GPU Trace (present-based frame delimiters,
            //    F11 hotkey) and eyeballing. Runs until the window closes. ──
            let win = window { backend Backend.Vulkan; display Display.Mono; debug false; samples 1 }
            let sw = Stopwatch.StartNew()
            let viewProj =
                (win.Sizes, win.Time) ||> AVal.map2 (fun szw _ ->
                    let a = sw.Elapsed.TotalSeconds * 0.25
                    let eye = V3d(cos a, sin a, 0.55) * extent
                    let view = CameraView.lookAt eye V3d.Zero V3d.OOI |> CameraView.viewTrafo
                    let proj = Frustum.perspective 70.0 0.1 (extent * 10.0) (float szw.X / float szw.Y) |> Frustum.projTrafo
                    view * proj)
            let objs = Array.init n (mkHeapRO (viewProj :> IAdaptiveValue))
            win.Scene <- Sg.renderObjectSet (Heap.ofRenderObjectsAuto (ASet.ofArray objs))
            win.Run()
            0
        else

        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime :> IRuntime
        let size = V2i(sizePx, sizePx)
        use signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]

        let view = CameraView.lookAt (V3d(0.0, -0.9, 0.75) * extent) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 (extent * 10.0) 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue

        let clearVals = clear { color (C4f(0.1f, 0.1f, 0.15f, 1.0f)); depth 1.0 }
        let renderWith (label : string) (objects : aset<IRenderObject>) =
            use colorTex = runtime.CreateTexture2D(size, TextureFormat.Rgba8)
            use depthTex = runtime.CreateTexture2D(size, TextureFormat.Depth24Stencil8)
            use fbo =
                runtime.CreateFramebuffer(signature, [
                    DefaultSemantic.Colors, colorTex.[TextureAspect.Color, 0, 0] :> IFramebufferOutput
                    DefaultSemantic.DepthStencil, depthTex.[TextureAspect.DepthStencil, 0, 0] :> IFramebufferOutput ])
            use task =
                RenderTask.ofList [
                    runtime.CompileClear(signature, clearVals)
                    runtime.CompileRender(signature, objects) ]
            let gpu, cpu, minGpu = measure runtime task fbo frames
            Log.line "renderbench[%s]: GPU %.2f ms/frame (min %.2f)   task.Run CPU %.2f ms/frame" label gpu minGpu cpu
            gpu

        // ── heap: n objects -> bucket indirect draws ──
        let heapObjs = Array.init n (mkHeapRO viewProj)
        let heapGpu = renderWith "heap" (Heap.ofRenderObjectsAuto (ASet.ofArray heapObjs))
        Log.line "renderbench[heap]: %d bucket(s)" Heap.lastBucketCount

        // ── baseline: ONE baked world-space soup mesh, single draw (lower bound) ──
        let totalV = int totalDrawnVerts
        let bp = Array.zeroCreate<V3f> totalV
        let bn = Array.zeroCreate<V3f> totalV
        let bc = Array.zeroCreate<C4b> totalV
        let mutable o = 0
        for i in 0 .. n - 1 do
            let (ps, ns, idx) = meshes.[i]
            let t = V3f (posOf i)
            let col = palette.[i % palette.Length]
            for k in 0 .. idx.Length - 1 do
                bp.[o] <- ps.[idx.[k]] + t
                bn.[o] <- ns.[idx.[k]]
                bc.[o] <- col
                o <- o + 1
        let bakedRO =
            let ro = RenderObject()
            ro.Surface <- Surface.Effect bakedEffect
            ro.Mode    <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <-
                AttributeProvider.ofList [
                    DefaultSemantic.Positions, bv bp typeof<V3f>
                    DefaultSemantic.Normals,   bv bn typeof<V3f>
                    DefaultSemantic.Colors,    bv bc typeof<C4b> ]
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = totalV, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [ Symbol.Create "ViewProjTrafo", viewProj ]
            ro :> IRenderObject
        let baseGpu = renderWith "baked-baseline" (ASet.single bakedRO)

        // ── optional: classic N individual draws (slow to prepare at large n) ──
        if classic then
            renderWith "classic-n-draws" (ASet.ofArray (Array.init n (mkHeapRO viewProj))) |> ignore

        Log.line "renderbench: heap/baseline = %.2fx   (target < 2x)" (heapGpu / baseGpu)
        0
