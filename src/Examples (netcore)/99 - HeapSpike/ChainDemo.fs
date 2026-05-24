namespace HeapSpike

// End-to-end GPU transform propagation demo. A heap-local transform tree (one
// shared, animated rotation per cluster, over many leaf cubes) is flattened to
// per-leaf chains and composed on the GPU. Animating the P cluster parents marks
// P link slots, NOT the N leaves — so the per-frame CPU upload is O(parents),
// not O(cubes). The log prints the distinct link uploads per frame to prove it.

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive

module ChainDemo =

    module S =
        open FShade
        type V =
            { [<Position>] pos : V4f
              [<Normal>]   n   : V3f }
        let shade (v : V) =
            vertex {
                let mvp : M44f = uniform?ModelViewProjTrafo
                let nm  : M44f = uniform?NormalMatrix
                return { v with pos = mvp * v.pos; n = (nm * V4f(v.n, 0.0f)).XYZ }
            }
        let frag (v : V) =
            fragment {
                let l = Vec.normalize (V3f(0.4f, 0.7f, 0.6f))
                let d = 0.2f + 0.8f * max 0.0f (Vec.dot (Vec.normalize v.n) l)
                return V4f(V3f(0.55f, 0.75f, 0.98f) * d, 1.0f)
            }

    let run () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
        let win = app.CreateGameWindow(samples = 8)
        let runtime = app.Runtime

        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.5)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>

        // P animated cluster parents (shared cvals), each over K leaf cubes.
        let pSide = 10
        let kSide = 10
        let parentCount = pSide * pSide
        let rnd = RandomSystem()
        // base ring/grid position + initial angle for each parent
        let parentPos = [| for x in 0 .. pSide-1 do for y in 0 .. pSide-1 -> V3d(float (x-pSide/2) * 6.0, float (y-pSide/2) * 6.0, 0.0) |]
        let parentSpin = parentPos |> Array.map (fun _ -> rnd.UniformDouble() * 6.2832)
        let parents = parentPos |> Array.mapi (fun i p -> AVal.init (Trafo3d.RotationZ parentSpin.[i] * Trafo3d.Translation p))
        // leaf-local cube positions within a cluster (constant -> folded)
        let leafLocal = [| for x in 0 .. kSide-1 do for y in 0 .. kSide-1 -> V3d(float (x-kSide/2) * 0.6, float (y-kSide/2) * 0.6, 0.0) |]

        let tree =
            Heap.Trafo(AVal.constant Trafo3d.Identity,
                [ for pi in 0 .. parentCount-1 ->
                    Heap.Trafo(parents.[pi] :> aval<Trafo3d>,
                        [ for l in leafLocal -> Heap.Leaf (AVal.constant (Trafo3d.Translation l)) ]) ])
        let chains = Heap.flattenChains tree
        let n = chains.Length

        let initialView = CameraView.lookAt (V3d(0.0, -70.0, 55.0)) V3d.Zero V3d.OOI
        let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView
        let view = cameraView |> AVal.map CameraView.viewTrafo
        let frustum = win.Sizes |> AVal.map (fun s -> Frustum.perspective 70.0 0.1 5000.0 (float s.X / float s.Y))
        let proj = frustum |> AVal.map Frustum.projTrafo

        let eff = FShade.Effect.compose [ FShade.Effect.ofFunction S.shade; FShade.Effect.ofFunction S.frag ]
        let sg = Heap.derivedChainFp64 runtime IndexedGeometryMode.TriangleList positions normals index eff view proj chains

        // animate the P parents every frame: marks P link slots, not N leaves.
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let mutable lastLog = 0.0
        win.AfterRender.Add (fun () ->
            let t = sw.Elapsed.TotalSeconds
            transact (fun () ->
                for i in 0 .. parentCount-1 do
                    parents.[i].Value <- Trafo3d.RotationZ (parentSpin.[i] + t * 0.8) * Trafo3d.Translation parentPos.[i])
            if t - lastLog > 1.0 then
                lastLog <- t
                Log.line "chaindemo: %d cubes, %d animated parents -> %d link uploads/frame" n parentCount Heap.lastChainLinkUploads)

        Log.warn "ChainDemo: %d cubes under %d shared animated parents (GPU transform propagation)" n parentCount
        win.RenderTask <- (sg |> Sg.compile runtime win.FramebufferSignature)
        win.Run()
