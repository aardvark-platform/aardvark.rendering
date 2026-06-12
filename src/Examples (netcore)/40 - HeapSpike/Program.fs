(*
    Phase-1 heap spike (Vulkan) — RO-level integration.

    Builds N ordinary RenderObjects (one per cube; per-draw model trafo &
    color in their uniform providers; shared box geometry) and runs them
    through `Heap.ofRenderObjects`, which COLLAPSES them into B bucket render
    objects — one per effect — each drawn as a single indirect multidraw
    against a shared arena, through the auto-rewritten shader.

    The standard CompileRender / CommandTask renders the B bucket objects, so
    the command stream encodes O(buckets) and binds ONE descriptor set per
    bucket instead of N. N=64 cubes -> 1 bucket / 1 indirect draw.
*)

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open FShade
open HeapSpike

// Incremental streaming demo: HeapScene with background add/remove churn.
let runDynamic () =
    Aardvark.Init()
    let win = window { backend Backend.Vulkan; display Display.Mono; debug false; samples 8 }

    let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
    let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
    let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
    let index     = g.IndexArray |> unbox<int[]>

    let viewProj : aval<Trafo3d> = AVal.map2 (fun (v : Trafo3d[]) (p : Trafo3d[]) -> v.[0] * p.[0]) win.View win.Proj
    let symVP = Symbol.Create "ViewProjTrafo"
    let globals =
        { new IUniformProvider with
            member _.TryGetUniform(s, name) = if name = symVP then ValueSome (viewProj :> IAdaptiveValue) else ValueNone
            member _.Dispose() = () }

    let effect = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
    let scene =
        new Heap.HeapScene(win.Runtime, effect, IndexedGeometryMode.TriangleList, positions, normals, index,
                       [| "HeapModelTrafo", typeof<M44f>; "HeapColor", typeof<V4f> |], globals)

    let live = System.Collections.Generic.List<int>()
    let spawn (rnd : RandomSystem) =
        let p = V3d(rnd.UniformDouble() * 12.0 - 6.0, rnd.UniformDouble() * 12.0 - 6.0, rnd.UniformDouble() * 12.0 - 6.0)
        let m = AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue
        let c = AVal.constant (V4f(rnd.UniformV3f(), 1.0f)) :> IAdaptiveValue
        live.Add (scene.Add(Map.ofList [ "HeapModelTrafo", m; "HeapColor", c ]))

    let rnd0 = RandomSystem()
    transact (fun () -> for _ in 1 .. 40 do spawn rnd0)

    // background thread churns add/remove every 80ms (each batch in a transact)
    let thread =
        System.Threading.Thread(System.Threading.ThreadStart(fun () ->
            let rnd = RandomSystem()
            while true do
                System.Threading.Thread.Sleep 80
                transact (fun () ->
                    let removeN = if live.Count > 80 then 12 elif live.Count > 30 then 6 else 0
                    for _ in 1 .. removeN do
                        let i = rnd.UniformInt(live.Count)
                        scene.Remove(live.[i]); live.RemoveAt i
                    let addN = if live.Count < 100 then 1 + rnd.UniformInt(7) else 0
                    for _ in 1 .. addN do spawn rnd)))
    thread.IsBackground <- true
    thread.Start()

    Log.warn "HeapScene dynamic: incremental add/remove churn in background (one bucket, one indirect draw)"
    win.Scene <- scene.Sg
    win.Run()

[<EntryPoint>]
let main argv =
    // macOS only: load aardvark's BUNDLED MoltenVK over a system-installed Vulkan
    // SDK. (On Linux this would drop libvulkan.so and fail, so guard it.)
    if System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform System.Runtime.InteropServices.OSPlatform.OSX then
        Aardvark.Rendering.Vulkan.VulkanLoader.PreferMoltenVK <- true

    // The ISimpleSg-direct render path is on by default; AARDVARK_SIMPLE_SG=0
    // forces back to the legacy `app?Runtime <- runtime; .RenderObjects(scope)`
    // entry-point if anything regresses.
    if System.Environment.GetEnvironmentVariable("AARDVARK_SIMPLE_SG") = "0" then
        Aardvark.SceneGraph.Simple.SimpleConfig.Enabled <- false
        printfn "[heap] SimpleSg path DISABLED (legacy Ag entry)"

    // The heap runtime is opt-in (default off). HeapSpike *is* the demo for it,
    // so flip it on unconditionally here.
    Aardvark.SceneGraph.HeapConfig.Enabled <- true
    if argv |> Array.contains "bench" then
        Bench.run ()
        0
    elif argv |> Array.contains "startup-bench" then
        StartupBench.run ()
        0
    elif argv |> Array.contains "showcase-rec" then
        Showcase.run true; 0
    elif argv |> Array.contains "showcase" then
        Showcase.run false; 0
    elif argv |> Array.contains "plain" then
        if Golden.plainTest () then 0 else 1
    elif argv |> Array.contains "golden" then
        if Golden.run () then 0 else 1
    elif argv |> Array.contains "vis" then
        if Golden.visibilityTest () then 0 else 1
    elif argv |> Array.contains "buckets" then
        if Golden.bucketingTest () then 0 else 1
    elif argv |> Array.contains "modes" then
        if Golden.modeRulesTest () then 0 else 1
    elif argv |> Array.contains "gpumodes-gl" then
        if GpuModes.runGL () then 0 else 1
    elif argv |> Array.contains "gpumodes-win" then
        GpuModes.runWin (); 0
    elif argv |> Array.contains "gpumodes" then
        if GpuModes.run () then 0 else 1
    elif argv |> Array.contains "fp64" then
        if Golden.derivedFp64Test () then 0 else 1
    elif argv |> Array.contains "chain" then
        if Golden.derivedChainTest () then 0 else 1
    elif argv |> Array.contains "chainfan" then
        if Golden.chainFanoutTest () then 0 else 1
    elif argv |> Array.contains "chaindemo" then
        ChainDemo.run (); 0
    elif argv |> Array.contains "passthru" then
        if Golden.passthroughTest () then 0 else 1
    elif argv |> Array.contains "nativebuf" then
        if Golden.nativeBufTest () then 0 else 1
    elif argv |> Array.contains "vartype" then
        if Golden.varTypeTest () then 0 else 1
    elif argv |> Array.contains "demoshot" then
        if Golden.demoShotTest () then 0 else 1
    elif argv |> Array.contains "glsldump" then
        GlslDump.run (); 0
    elif argv |> Array.contains "texheap" then
        if Golden.texHeapTest () then 0 else 1
    elif argv |> Array.contains "texswap" then
        if Golden.texSwapTest () then 0 else 1
    elif argv |> Array.contains "texstate" then
        if Golden.texStateTest () then 0 else 1
    elif argv |> Array.contains "texcube" then
        if Golden.texCubeTest () then 0 else 1
    elif argv |> Array.contains "gpugeom-gl" then
        if Golden.gpuGeomTestGL () then 0 else 1
    elif argv |> Array.contains "gpugeom" then
        if Golden.gpuGeomTest () then 0 else 1
    elif argv |> Array.contains "atlas" then
        if Golden.atlasBuildTest () then 0 else 1
    elif argv |> Array.contains "atlasheap" then
        if Golden.atlasHeapTest () then 0 else 1
    elif argv |> Array.contains "msaa" then
        if Golden.msaaTest () then 0 else 1
    elif argv |> Array.contains "churnprobe" then
        if Golden.churnProbeTest () then 0 else 1
    elif argv |> Array.contains "geomchurn" then
        if Golden.geomChurnTest () then 0 else 1
    elif argv |> Array.contains "geomdrift" then
        if Golden.geomDriftTest () then 0 else 1
    elif argv |> Array.contains "lifetime" then
        if Golden.lifetimeTest () then 0 else 1
    elif argv |> Array.contains "submitstress" then
        if Golden.submitStressTest () then 0 else 1
    elif argv |> Array.contains "glyphwedge" then
        if Golden.glyphWedgeTest () then 0 else 1
    elif argv |> Array.contains "atlaspool" then
        if Golden.atlasPoolTest () then 0 else 1
    elif argv |> Array.contains "bindlessvar" then
        if Golden.bindlessVarTest () then 0 else 1
    elif argv |> Array.contains "bcbox" then
        if Golden.bindlessCleanBoxTest () then 0 else 1
    elif argv |> Array.contains "bindlesssimple" then
        if Golden.bindlessSimpleTest () then 0 else 1
    elif argv |> Array.contains "ssboarray5" then
        if Golden.ssboArray5Test () then 0 else 1
    elif argv |> Array.contains "ssboarray4" then
        if Golden.ssboArray4Test () then 0 else 1
    elif argv |> Array.contains "ssboarray3" then
        if Golden.ssboArray3Test () then 0 else 1
    elif argv |> Array.contains "ssboarray2" then
        if Golden.ssboArray2Test () then 0 else 1
    elif argv |> Array.contains "ssboarray" then
        if Golden.ssboArrayTest () then 0 else 1
    elif argv |> Array.contains "bindless" then
        if Golden.bindlessHeapTest () then 0 else 1
    elif argv |> Array.contains "inst" then
        if Golden.instancingTest () then 0 else 1
    elif argv |> Array.contains "instbucket" then
        if Golden.alreadyInstancedTest () then 0 else 1
    elif argv |> Array.contains "gl" then
        if Golden.glHeapTest () then 0 else 1
    elif argv |> Array.contains "glbindless-win" then
        GLBindless.runWin (); 0
    elif argv |> Array.contains "glbindless" then
        if GLBindless.run () then 0 else 1
    elif argv |> Array.contains "dynamic" then
        runDynamic ()
        0
    elif argv |> Array.contains "textures" then
        Textures.run ()
        0
    elif argv |> Array.contains "real" then
        Real.run ()
        0
    elif argv |> Array.contains "phase4r" then
        Phase4.runRender ()
        0
    elif argv |> Array.contains "phase4" then
        Phase4.run ()
        0
    else

    Aardvark.Init()

    let win =
        window {
            backend Backend.Vulkan
            display Display.Mono
            debug false
            samples 8
        }

    // VARIED geometry — box / sphere / torus. Each gets its own BufferViews;
    // Heap.ofRenderObjects packs them into shared buffers (deduped by identity)
    // with per-RO draw ranges, still ONE indirect draw for the bucket.
    let geometry (ig : IndexedGeometry) =
        let g = ig.ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let attrs = AttributeProvider.ofList [
                        DefaultSemantic.Positions, BufferView(AVal.constant (ArrayBuffer(positions) :> IBuffer), typeof<V3f>)
                        DefaultSemantic.Normals,   BufferView(AVal.constant (ArrayBuffer(normals)   :> IBuffer), typeof<V3f>) ]
        let idxBV = BufferView(AVal.constant (ArrayBuffer(index) :> IBuffer), typeof<int>)
        attrs, idxBV, index.Length

    let shapes =
        [| geometry (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White)
           geometry (IndexedGeometryPrimitives.Sphere.solidSubdivisionSphere (Sphere3d(V3d.Zero, 0.4)) 3 C4b.White)
           geometry (IndexedGeometryPrimitives.solidTorus (Torus3d(V3d.Zero, V3d.OOI, 0.35, 0.13)) C4b.White 16 12) |]

    // camera -> ViewProjTrafo (global; left as a UBO by the rewrite)
    let viewProj : aval<Trafo3d> =
        AVal.map2 (fun (v : Trafo3d[]) (p : Trafo3d[]) -> v.[0] * p.[0]) win.View win.Proj

    // grid of cubes
    let side = 8
    let palette =
        [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold
           C4f.Magenta; C4f.Cyan; C4f.Orange; C4f.HotPink |]
    let grid =
        [| for x in 0 .. side - 1 do
             for y in 0 .. side - 1 ->
               V3d(float (x - side/2) * 1.2, float (y - side/2) * 1.2, 0.0) |]

    let colors = grid |> Array.mapi (fun i _ -> AVal.init (palette.[i % palette.Length].ToV4f()))

    let sw = System.Diagnostics.Stopwatch.StartNew()
    let modelOf (p : V3d) (phase : float) : aval<M44f> =
        win.Time |> AVal.map (fun _ ->
            let t = sw.Elapsed.TotalSeconds
            (Trafo3d.Translation p * Trafo3d.RotationZ(0.5 * t + phase)).Forward |> M44f.op_Explicit)

    // two distinct effects -> two buckets -> two indirect draws
    let effectLit = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
    let effectRim = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFragRim ]

    // N ordinary render objects
    let inputs =
        grid |> Array.mapi (fun i p ->
            let (attrs, idxBV, faceVertexCount) = shapes.[i % shapes.Length]
            let ro = RenderObject()
            ro.Surface   <- Surface.Effect (if i % 2 = 0 then effectLit else effectRim)
            ro.Mode      <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- attrs
            ro.Indices   <- Some idxBV
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = faceVertexCount, InstanceCount = 1) |])
            ro.Uniforms  <-
                UniformProvider.ofList [
                    Symbol.Create "HeapModelTrafo", (modelOf p (float i * 0.3) :> IAdaptiveValue)
                    Symbol.Create "HeapColor",      (colors.[i] :> IAdaptiveValue)
                    Symbol.Create "ViewProjTrafo",  (viewProj :> IAdaptiveValue)
                ]
            ro :> IRenderObject)

    let inputSet = ASet.ofArray inputs

    // THE INTEGRATION: N independent ROs -> B bucket ROs (one indirect draw each)
    let heapObjects = Heap.ofRenderObjects win.Runtime (Set.ofList [ "HeapModelTrafo"; "HeapColor" ]) inputSet

    win.Keyboard.DownWithRepeats.Values.Add (fun k ->
        if k = Keys.Space then
            let rnd = RandomSystem()
            transact (fun () -> for c in colors do c.Value <- V4f(rnd.UniformV3f(), 1.0f))
            Log.warn "recolored")

    win.Scene <- Sg.renderObjectSet heapObjects
    // force evaluation so the bucket count is known for the log
    heapObjects |> ASet.toAVal |> AVal.force |> ignore
    Log.warn "HeapSpike phase-1 RO integration: %d input ROs -> %d bucket RO(s) / indirect draw(s)" inputs.Length Heap.lastBucketCount

    win.Run()
    0
