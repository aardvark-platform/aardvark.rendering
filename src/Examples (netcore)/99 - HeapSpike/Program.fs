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

[<EntryPoint>]
let main argv =
    if argv |> Array.contains "bench" then
        Bench.run ()
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
