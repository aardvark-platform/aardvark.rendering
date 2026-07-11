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

// Incremental streaming demo: a CHANGEABLE RenderObject set (cset) run through
// Heap.ofRenderObjects, with background add/remove churn. The heap collapses the
// live cubes into ONE bucket / ONE indirect draw and incrementally re-packs only
// the membership delta — same effect as the old hand-rolled HeapScene, but on the
// general reactive path.
let runDynamic () =
    Aardvark.Init()
    let win = window { backend Backend.Vulkan; display Display.Mono; debug false; samples 8 }

    let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
    let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
    let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
    let index     = g.IndexArray |> unbox<int[]>
    let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
    let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>
                                            DefaultSemantic.Normals,   bv normals   typeof<V3f> ]

    // ONE shared camera trafo aval -> dedups to a single arena region (re-packed
    // once per camera move, regardless of how many cubes are live).
    let viewProj : aval<Trafo3d> = AVal.map2 (fun (v : Trafo3d[]) (p : Trafo3d[]) -> v.[0] * p.[0]) win.View win.Proj

    let effect = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]

    // one cube = one ordinary RenderObject (per-draw model trafo & color in its
    // uniforms); Heap.ofRenderObjects auto-detects them as per-draw arena fields.
    let mkCube (rnd : RandomSystem) : IRenderObject =
        let p = V3d(rnd.UniformDouble() * 12.0 - 6.0, rnd.UniformDouble() * 12.0 - 6.0, rnd.UniformDouble() * 12.0 - 6.0)
        let ro = RenderObject()
        ro.Surface          <- Surface.Effect effect
        ro.Mode             <- IndexedGeometryMode.TriangleList
        ro.VertexAttributes <- vattrs
        ro.Indices          <- Some (bv index typeof<int>)
        ro.DrawCalls        <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
        ro.Uniforms         <- UniformProvider.ofList [
            Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
            Symbol.Create "HeapColor",      (AVal.constant (V4f(rnd.UniformV3f(), 1.0f)) :> IAdaptiveValue)
            Symbol.Create "ViewProjTrafo",  (viewProj :> IAdaptiveValue) ]
        ro :> IRenderObject

    let cubes = cset<IRenderObject>()
    let live  = System.Collections.Generic.List<IRenderObject>()
    let spawn (rnd : RandomSystem) = let ro = mkCube rnd in cubes.Add ro |> ignore; live.Add ro

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
                        cubes.Remove live.[i] |> ignore; live.RemoveAt i
                    let addN = if live.Count < 100 then 1 + rnd.UniformInt(7) else 0
                    for _ in 1 .. addN do spawn rnd)))
    thread.IsBackground <- true
    thread.Start()

    Log.warn "heap dynamic: incremental add/remove churn via Heap.ofRenderObjects (one bucket, one indirect draw)"
    win.Scene <- Sg.renderObjectSet (Heap.ofRenderObjects (win.Runtime.CreateHeapStorage()) (cubes :> aset<IRenderObject>))
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

    if argv |> Array.contains "bench" then
        Bench.run ()
        0
    elif argv |> Array.contains "renderbench" then
        RenderBench.run argv
    elif argv |> Array.contains "fsgather" then
        FsGather.run argv
    elif argv |> Array.contains "startup-bench" then
        StartupBench.run ()
        0
    elif argv |> Array.contains "showcase-rec" then
        Showcase.run true; 0
    elif argv |> Array.contains "showcase" then
        Showcase.run false; 0
    // NOTE the correctness gauntlet (golden/churn/dyngeom/atlas/...) moved to
    // Aardvark.Rendering.Tests ("Heap gauntlet (Vulkan)" Expecto subtree) —
    // run it via the test project; HeapSpike is a demo/bench playground.
    elif argv |> Array.contains "gpumodes-gl" then
        if GpuModes.runGL () then 0 else 1
    elif argv |> Array.contains "gpumodes-win" then
        GpuModes.runWin (); 0
    elif argv |> Array.contains "gpumodes" then
        if GpuModes.run () then 0 else 1
    elif argv |> Array.contains "chainbench" then
        let n = match argv |> Array.tryFindIndex ((=) "--n") with | Some i when i+1 < argv.Length -> int argv.[i+1] | _ -> 20000
        ChainBench.run (not (argv |> Array.contains "--folded")) n
    elif argv |> Array.contains "glsldump" then
        GlslDump.run (); 0
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
    let heapObjects = Heap.ofRenderObjects (win.Runtime.CreateHeapStorage()) inputSet

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
