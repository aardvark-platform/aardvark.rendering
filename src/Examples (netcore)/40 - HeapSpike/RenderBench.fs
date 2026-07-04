namespace HeapSpike

// GPU render-time benchmark on SYNTHETIC data: what does the heap's per-vertex
// decode/gather actually cost vs the LOWER-BOUND baseline — ONE baked world-space
// soup mesh drawn with a plain VP-only shader (the CadSceneDemo `Baseline` floor)?
// Both paths shade the SAME vertex count non-indexed and the same pixels; the
// heap additionally pays: index decode, attribute pulls, per-draw uniform gathers
// (ModelTrafo is a real DERIVED field — fp64 compute collapse — like production),
// and N indirect draw records instead of 1 draw.
//
//   renderbench [--n 100000] [--size 1024] [--frames 60] [--distinct 64] [--classic]
//
// Measures REAL GPU time per frame via time queries (RenderToken.Queries — same
// method as CadSceneDemo's Offscreen.run), plus task.Run CPU time. Variants run
// SEQUENTIALLY (each torn down before the next) so peak memory doesn't stack.
//
// Caveat for reading the numbers: the heap DEDUPS geometry by source identity
// (`--distinct` controls how many distinct meshes the objects cycle through), so
// its vertex-data footprint is tiny while the baked baseline streams the full
// soup buffer — the benchmark isolates per-vertex PIPELINE cost, not bandwidth
// for unique-geometry scenes.

module RenderBench =

    open System
    open System.Diagnostics
    open Aardvark.Base
    open Aardvark.Rendering
    open Aardvark.SceneGraph
    open FSharp.Data.Adaptive
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

    /// avg GPU ms/frame (time query) + avg task.Run CPU ms over `frames` runs after one warm-up.
    let private measure (runtime : IRuntime) (task : IRenderTask) (fbo : IFramebuffer) (frames : int) =
        let gpuQuery = runtime.CreateTimeQuery()
        let token = { RenderToken.Empty with Queries = [ gpuQuery ] }
        let output = OutputDescription.ofFramebuffer fbo
        task.Run(AdaptiveToken.Top, token, output)
        gpuQuery.GetResult((), reset = true) |> ignore                       // warm (build + first submit)
        let sw = Stopwatch()
        let mutable gpu = 0.0
        for _ in 1 .. frames do
            sw.Start()
            task.Run(AdaptiveToken.Top, token, output)
            sw.Stop()
            gpu <- gpu + (gpuQuery.GetResult((), reset = true)).TotalMilliseconds
        gpu / float frames, sw.Elapsed.TotalMilliseconds / float frames

    let run (argv : string[]) =
        let arg (name : string) (dflt : int) =
            match argv |> Array.tryFindIndex ((=) name) with
            | Some i when i + 1 < argv.Length -> int argv.[i + 1]
            | _ -> dflt
        let n        = arg "--n" 100000
        let sizePx   = arg "--size" 1024
        let frames   = arg "--frames" 60
        let distinct = max 1 (arg "--distinct" 64)
        let classic  = argv |> Array.contains "--classic"

        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime :> IRuntime
        let size = V2i(sizePx, sizePx)
        use signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]

        // ── synthetic scene: n small boxes in a grid, `distinct` distinct meshes ──
        let s = int (ceil (sqrt (float n)))
        let extent = float s * 1.2
        let posOf (i : int) = V3d(float (i % s - s/2) * 1.2, float (i / s - s/2) * 1.2, 0.0)
        let palette = [| C4b(230,60,60); C4b(60,200,60); C4b(60,120,230); C4b(230,200,40); C4b(210,60,210); C4b(40,210,210) |]
        // distinct meshes = cloned arrays (distinct source identity -> no heap dedup across them)
        let meshes =
            Array.init distinct (fun _ ->
                let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.8)) C4b.White).ToIndexed()
                (g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]> |> Array.copy),
                (g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]> |> Array.copy),
                (g.IndexArray |> unbox<int[]> |> Array.copy))
        let (p0, _, i0) = meshes.[0]
        let vertsPerObj = i0.Length
        Log.line "renderbench: n=%d  %d verts/obj  %.1f M verts total  %d distinct meshes  %dx%d px  %d frames"
            n vertsPerObj (float n * float vertsPerObj / 1e6) distinct sizePx sizePx frames
        ignore p0

        let view = CameraView.lookAt (V3d(0.0, -0.9, 0.75) * extent) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 (extent * 10.0) 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue

        let bv (a : Array) (t : Type) = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let heapEffect  = Effect.compose [ Effect.ofFunction Sh.heapVert;  Effect.ofFunction Sh.lit ]
        let bakedEffect = Effect.compose [ Effect.ofFunction Sh.bakedVert; Effect.ofFunction Sh.lit ]

        let mkHeapRO (i : int) =
            let (ps, ns, idx) = meshes.[i % distinct]
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
                    Symbol.Create "ModelTrafo",    (AVal.constant (Trafo3d.Translation (posOf i)) :> IAdaptiveValue)
                    Symbol.Create "ViewProjTrafo", viewProj ]
            ro :> IRenderObject

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
            let gpu, cpu = measure runtime task fbo frames
            Log.line "renderbench[%s]: GPU %.2f ms/frame   task.Run CPU %.2f ms/frame" label gpu cpu
            gpu

        // ── heap: n objects -> bucket indirect draws ──
        let heapObjs = Array.init n mkHeapRO
        let heapGpu = renderWith "heap" (Heap.ofRenderObjectsAuto (ASet.ofArray heapObjs))
        Log.line "renderbench[heap]: %d bucket(s)" Heap.lastBucketCount

        // ── baseline: ONE baked world-space soup mesh, single draw (lower bound) ──
        let totalV = n * vertsPerObj
        let bp = Array.zeroCreate<V3f> totalV
        let bn = Array.zeroCreate<V3f> totalV
        let bc = Array.zeroCreate<C4b> totalV
        let mutable o = 0
        for i in 0 .. n - 1 do
            let (ps, ns, idx) = meshes.[i % distinct]
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
            renderWith "classic-n-draws" (ASet.ofArray (Array.init n mkHeapRO)) |> ignore

        Log.line "renderbench: heap/baseline = %.2fx   (target < 2x)" (heapGpu / baseGpu)
        0
