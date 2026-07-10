namespace HeapSpike

// Headless startup benchmark: same SG (N objects, identical to showcase), two
// resolution paths:
//   1. Ag path     —  dn?Runtime <- runtime; dn?RenderObjects(Ag.Scope.Root)
//   2. Simple path —  (sg :> ISimpleSg).GetRenderObjects(TS.withRuntime runtime empty)
// Each path runs in isolation (its own SG tree to avoid mutual caching), with
// the resulting `aset<IRenderObject>` forced via `ASet.toAVal |> AVal.force` so
// we measure the full delta-realization cost. Reports min/median/mean over
// repeats + the resulting RO count (must match between paths).

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics  // ISg.RenderObjects(scope) extension
open Aardvark.SceneGraph.Simple
open FSharp.Data.Adaptive
open Aardvark.Application
open Aardvark.Base.Ag
open System
open System.Diagnostics

module StartupBench =

    /// Wraps `sg` in `depth` nested singleton-group nodes. Each level is just
    /// `Sg.ofArray [| inner |]` — a Sg.Set with one child — used to probe how
    /// the two paths scale with SG depth above the leaves.
    let rec private singletonStack (depth : int) (sg : ISg) : ISg =
        if depth <= 0 then sg
        else singletonStack (depth - 1) (Sg.ofArray [| sg |])

    /// Build a fresh n-object SG identical-in-shape to the showcase scene
    /// (Sg.render → vertexBuffers → indexBuffer → trafo' → uniform' → effect),
    /// inside the standard view/proj/camera wrappers. No textures (those would
    /// need the runtime; not relevant to RO resolution timing).
    /// `depth` injects that many singleton-group nodes ABOVE EVERY LEAF before
    /// the leaves are gathered into the top-level Set.
    let private buildScene (n : int) (depth : int) : ISg =
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.7)) C4b.White).ToIndexed()
        let pos = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let nor = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let idx = g.IndexArray |> unbox<int[]>
        let posBV = BufferView(AVal.constant (ArrayBuffer(pos) :> IBuffer), typeof<V3f>)
        let norBV = BufferView(AVal.constant (ArrayBuffer(nor) :> IBuffer), typeof<V3f>)
        let idxBV = BufferView(AVal.constant (ArrayBuffer(idx) :> IBuffer), typeof<int>)
        let faceVertexCount = idx.Length

        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan; C4f.Orange; C4f.HotPink |]
        let effect = FShade.Effect.compose [ FShade.Effect.ofFunction Shaders.shade; FShade.Effect.ofFunction Shaders.shadeFrag ]

        let rnd = RandomSystem()  // not seeded; not relevant to timing
        let span = 70.0

        let objSg (i : int) : ISg =
            let p = V3d(rnd.UniformDouble() - 0.5, rnd.UniformDouble() - 0.5, rnd.UniformDouble() - 0.5) * span
            let model = Trafo3d.Rotation(rnd.UniformV3dDirection(), rnd.UniformDouble() * 6.2832) * Trafo3d.Translation p
            Sg.render IndexedGeometryMode.TriangleList (DrawCallInfo(FaceVertexCount = faceVertexCount, InstanceCount = 1))
            |> Sg.vertexBuffer DefaultSemantic.Positions posBV
            |> Sg.vertexBuffer DefaultSemantic.Normals   norBV
            |> Sg.indexBuffer idxBV
            |> Sg.trafo' model
            |> Sg.uniform' "HeapColor" (palette.[i % palette.Length].ToV4f())
            |> Sg.effect [ effect ]
            |> singletonStack depth

        let view = CameraView.lookAt (V3d(80.0, 80.0, 50.0)) V3d.Zero V3d.OOI |> CameraView.viewTrafo |> AVal.constant
        let proj = Frustum.perspective 70.0 0.1 3000.0 (16.0 / 9.0) |> Frustum.projTrafo |> AVal.constant
        let camLoc = view |> AVal.map (fun t -> V3f (t.Inverse.GetViewPosition()))

        Array.init n objSg
        |> Sg.ofArray
        |> Sg.uniform "CameraLocation" camLoc
        |> Sg.viewTrafo view
        |> Sg.projTrafo proj

    /// Force GC + collect-pending to make the next measurement clean.
    let private clean () =
        GC.Collect(2, GCCollectionMode.Forced, blocking = true)
        GC.WaitForPendingFinalizers()
        GC.Collect(2, GCCollectionMode.Forced, blocking = true)

    let private timeMs (f : unit -> int) =
        clean ()
        let sw = Stopwatch.StartNew()
        let count = f ()
        sw.Stop()
        sw.Elapsed.TotalMilliseconds, count

    /// One Ag-path measurement: build SG, wrap in DynamicNode + ?Runtime, force
    /// the aset. Fresh SG every time so Ag's per-scope caches don't leak across
    /// runs.
    let private agOnce (runtime : IRuntime) (n : int) (depth : int) =
        let sg = buildScene n depth
        timeMs (fun () ->
            let dn = Sg.DynamicNode(AVal.constant sg) :> ISg
            dn?Runtime <- runtime
            let ros = dn.RenderObjects(Ag.Scope.Root)
            ros |> ASet.toAVal |> AVal.force |> HashSet.count
        )

    /// One Simple-path measurement: build SG, force via GetRenderObjects with
    /// Runtime seeded on the TS.
    let private simpleOnce (runtime : IRuntime) (n : int) (depth : int) =
        let sg = buildScene n depth
        timeMs (fun () ->
            let ts = TraversalState.withRuntime runtime TraversalState.empty
            let ros = (sg :?> ISimpleSg).GetRenderObjects ts
            ros |> ASet.toAVal |> AVal.force |> HashSet.count
        )

    let private percentile (sorted : float[]) (p : float) =
        let i = int (p * float (sorted.Length - 1))
        sorted.[i]

    let private report (label : string) (samples : float[]) (count : int) =
        Array.sortInPlace samples
        let min = samples.[0]
        let max = samples.[samples.Length - 1]
        let med = percentile samples 0.5
        let p95 = percentile samples 0.95
        let mean = Array.average samples
        printfn "%-18s  n=%d  min=%8.1fms  med=%8.1fms  mean=%8.1fms  p95=%8.1fms  max=%8.1fms  ros=%d"
            label samples.Length min med mean p95 max count

    let run () =
        Aardvark.Init()
        use app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication()
        let runtime = app.Runtime

        let n =
            match Environment.GetEnvironmentVariable "N" with
            | null | "" -> 20000
            | s -> int s

        let warmups =
            match Environment.GetEnvironmentVariable "WARMUPS" with
            | null | "" -> 1
            | s -> int s

        let iters =
            match Environment.GetEnvironmentVariable "ITERS" with
            | null | "" -> 5
            | s -> int s

        // Either a single DEPTH=<int> or a DEPTHS=<comma-list> sweep.
        let depths =
            match Environment.GetEnvironmentVariable "DEPTHS" with
            | null | "" ->
                match Environment.GetEnvironmentVariable "DEPTH" with
                | null | "" -> [| 0 |]
                | s -> [| int s |]
            | s -> s.Split(',') |> Array.map (fun t -> int (t.Trim()))

        printfn ""
        printfn "[StartupBench] N=%d, warmups=%d, iters=%d, depths=[%s]"
            n warmups iters (depths |> Array.map string |> String.concat "; ")
        printfn ""

        // warm up JIT and any shared static caches once at the smallest depth.
        for _ in 1 .. warmups do
            let _ = agOnce runtime n depths.[0] in ()
            let _ = simpleOnce runtime n depths.[0] in ()

        // For each depth: one Ag block of `iters`, one Simple block of `iters`, report.
        let rows = ResizeArray<int * float * float * float * float * int>()
        for depth in depths do
            printfn "--- depth=%d ---" depth

            let agSamples = Array.zeroCreate<float> iters
            let mutable agCount = 0
            for i in 0 .. iters - 1 do
                let ms, c = agOnce runtime n depth
                agSamples.[i] <- ms
                agCount <- c
                printfn "  ag      iter %d: %7.1f ms  (ros=%d)" i ms c

            let simSamples = Array.zeroCreate<float> iters
            let mutable simCount = 0
            for i in 0 .. iters - 1 do
                let ms, c = simpleOnce runtime n depth
                simSamples.[i] <- ms
                simCount <- c
                printfn "  simple  iter %d: %7.1f ms  (ros=%d)" i ms c

            Array.sortInPlace agSamples
            Array.sortInPlace simSamples
            let agMed  = percentile agSamples 0.5
            let simMed = percentile simSamples 0.5
            let agMin  = agSamples.[0]
            let simMin = simSamples.[0]
            rows.Add(depth, agMin, agMed, simMin, simMed, agCount)

            report (sprintf "Ag      d=%d" depth) agSamples  agCount
            report (sprintf "Simple  d=%d" depth) simSamples simCount
            printfn "speedup (min/min): %.2fx" (agMin / simMin)
            printfn ""

        // summary table at the end
        printfn ""
        printfn "============== summary (N=%d) ==============" n
        printfn "%5s | %12s %12s | %12s %12s | %8s" "depth" "ag-min(ms)" "ag-med(ms)" "sim-min(ms)" "sim-med(ms)" "speedup"
        for (d, aMin, aMed, sMin, sMed, _) in rows do
            printfn "%5d | %12.1f %12.1f | %12.1f %12.1f | %7.2fx" d aMin aMed sMin sMed (aMin / sMin)
