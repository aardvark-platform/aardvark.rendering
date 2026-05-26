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

    /// Build a fresh 20k-object SG identical-in-shape to the showcase scene
    /// (Sg.render → vertexBuffers → indexBuffer → trafo' → uniform' → effect),
    /// inside the standard view/proj/camera wrappers. No textures (those would
    /// need the runtime; not relevant to RO resolution timing).
    let private buildScene (n : int) : ISg =
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
    let private agOnce (runtime : IRuntime) (n : int) =
        let sg = buildScene n
        timeMs (fun () ->
            let dn = Sg.DynamicNode(AVal.constant sg) :> ISg
            dn?Runtime <- runtime
            let ros = dn.RenderObjects(Ag.Scope.Root)
            ros |> ASet.toAVal |> AVal.force |> HashSet.count
        )

    /// One Simple-path measurement: build SG, force via GetRenderObjects with
    /// Runtime seeded on the TS.
    let private simpleOnce (runtime : IRuntime) (n : int) =
        let sg = buildScene n
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
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
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

        printfn ""
        printfn "[StartupBench] N=%d, warmups=%d, iters=%d" n warmups iters
        printfn ""

        // warm up JIT and any shared static caches
        for _ in 1 .. warmups do
            let _ = agOnce runtime n in ()
            let _ = simpleOnce runtime n in ()

        // Ag-path samples
        let agSamples  = Array.zeroCreate<float> iters
        let mutable agCount  = 0
        for i in 0 .. iters - 1 do
            let ms, c = agOnce runtime n
            agSamples.[i] <- ms
            agCount <- c
            printfn "  ag      iter %d: %7.1f ms  (ros=%d)" i ms c

        // Simple-path samples
        let simSamples = Array.zeroCreate<float> iters
        let mutable simCount = 0
        for i in 0 .. iters - 1 do
            let ms, c = simpleOnce runtime n
            simSamples.[i] <- ms
            simCount <- c
            printfn "  simple  iter %d: %7.1f ms  (ros=%d)" i ms c

        printfn ""
        report "Ag       (legacy)" agSamples  agCount
        report "Simple   (TS)    " simSamples simCount
        printfn ""
        let agMin  = Array.min agSamples
        let simMin = Array.min simSamples
        let ratio  = agMin / simMin
        printfn "speedup (min/min): %.2fx" ratio
