(*
    Phase-1 heap spike (Vulkan).

    Same picture as phase 0 (9 cubes, one indirect draw, all per-draw data
    from a shared arena), but now driven through the generic, reactive
    `Heap` module:
      - the rewrite is type-driven (no hardcoded names);
      - per-draw model trafos come from time-driven avals, so the arena
        re-packs reactively every frame — proving the aval -> arena round
        trip (offsets/headers never move);
      - colors are per-cube cvals, deduplicated by identity.
*)

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open FShade
open HeapSpike

module HeapShaders =
    type Vertex =
        { [<Position>] pos : V4f
          [<Color>]    c   : V4f
          [<Normal>]   n   : V3f }

    // ordinary effect: per-draw model trafo & color as plain uniforms,
    // camera read normally (stays a UBO after the rewrite).
    let shade (v : Vertex) =
        vertex {
            let m   : M44f = uniform?HeapModelTrafo
            let col : V4f  = uniform?HeapColor
            let vp  : M44f = uniform?ViewProjTrafo
            return { v with pos = vp * (m * v.pos); c = col; n = m.TransformDir v.n }
        }

    let shadeFrag (v : Vertex) =
        fragment {
            let l  = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
            let nn = Vec.normalize v.n
            let d  = 0.25f + 0.75f * max 0.0f (Vec.dot nn l)
            return V4f(v.c.XYZ * d, 1.0f)
        }

[<EntryPoint>]
let main argv =
    Aardvark.Init()

    let win =
        window {
            backend Backend.Vulkan
            display Display.Mono
            debug false
            samples 8
        }

    // shared box geometry
    let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.8)) C4b.White).ToIndexed()
    let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
    let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
    let index     = g.IndexArray |> unbox<int[]>

    let grid = [| for x in -1 .. 1 do for y in -1 .. 1 -> V3d(float x * 1.5, float y * 1.5, 0.0) |]
    let palette =
        [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold
           C4f.Magenta; C4f.Cyan; C4f.Orange; C4f.White; C4f.HotPink |]

    // per-cube color cvals (deduplicated by identity in the arena)
    let colors = grid |> Array.mapi (fun i _ -> AVal.init (palette.[i % palette.Length].ToV4f()))

    // per-cube model trafo, time-driven -> arena re-packs reactively
    let sw = System.Diagnostics.Stopwatch.StartNew()
    let modelOf (p : V3d) (phase : float) : aval<M44f> =
        win.Time |> AVal.map (fun _ ->
            let t = sw.Elapsed.TotalSeconds
            (Trafo3d.Translation p * Trafo3d.RotationZ(t + phase)).Forward |> M44f.op_Explicit)

    let draws =
        grid |> Array.mapi (fun i p ->
            Map.ofList [
                "HeapModelTrafo", Heap.mat4 (modelOf p (float i * 0.7))
                "HeapColor",      Heap.v4 colors.[i]
            ])

    let effect =
        Effect.compose [ Effect.ofFunction HeapShaders.shade; Effect.ofFunction HeapShaders.shadeFrag ]

    let sg =
        Heap.scene IndexedGeometryMode.TriangleList positions normals index effect draws

    // SPACE: recolor everything (proves sparse reactive uniform mutation)
    win.Keyboard.DownWithRepeats.Values.Add (fun k ->
        if k = Keys.Space then
            let rnd = RandomSystem()
            transact (fun () -> for c in colors do c.Value <- V4f(rnd.UniformV3f(), 1.0f))
            Log.warn "recolored")

    Log.warn "HeapSpike phase 1: %d objects, ONE indirect draw, reactive arena (animated)" draws.Length

    win.Scene <- sg
    win.Run()
    0
