namespace HeapSpike

// #4: real-scene uniforms — per-object ModelTrafo as Trafo3d (not hand-built
// M44f), and the standard DERIVED ModelViewProjTrafo. The general derived-
// uniform system (Heap.standardDerivedRules) decomposes ModelViewProjTrafo into
// ViewProjTrafo(global UBO) * ModelTrafo(arena), so a camera move re-uploads one
// UBO and never re-packs the arena.

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Aardvark.Application
open FShade

module Real =

    module S =
        type V = { [<Position>] pos : V4f; [<Color>] c : V4f; [<Normal>] n : V3f }

        // standard-style shader: reads the DERIVED ModelViewProjTrafo + per-object
        // ModelTrafo (for normals) + a per-object color.
        let shade (v : V) =
            vertex {
                let mvp : M44f = uniform?ModelViewProjTrafo
                let m   : M44f = uniform?ModelTrafo
                let col : V4f  = uniform?HeapColor
                return { v with pos = mvp * v.pos; n = m.TransformDir v.n; c = col }
            }

        let frag (v : V) =
            fragment {
                let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
                let d = 0.25f + 0.75f * max 0.0f (Vec.dot (Vec.normalize v.n) l)
                return V4f(v.c.XYZ * d, 1.0f)
            }

    let run () =
        Aardvark.Init()
        let win = window { backend Backend.Vulkan; display Display.Mono; debug false; samples 8 }

        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.7)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]

        // global ViewProjTrafo as Trafo3d (aardvark's standard type)
        let viewProj : aval<Trafo3d> = AVal.map2 (fun (v : Trafo3d[]) (p : Trafo3d[]) -> v.[0] * p.[0]) win.View win.Proj

        let grid = [| for x in -3 .. 3 do for y in -3 .. 3 -> V3d(float x * 1.4, float y * 1.4, 0.0) |]
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan; C4f.Orange |]
        let effect = Effect.compose [ Effect.ofFunction S.shade; Effect.ofFunction S.frag ]

        let inputs =
            grid |> Array.mapi (fun i p ->
                let ro = RenderObject()
                ro.Surface   <- Surface.Effect effect
                ro.Mode      <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- vattrs
                ro.Indices   <- Some (bv index typeof<int>)
                ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
                ro.Uniforms  <-
                    UniformProvider.ofList [
                        // per-object base trafo as Trafo3d (camera-independent -> arena)
                        Symbol.Create "ModelTrafo",     (AVal.constant (Trafo3d.Translation p) :> IAdaptiveValue)
                        Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                        // global camera (UBO)
                        Symbol.Create "ViewProjTrafo",  (viewProj :> IAdaptiveValue)
                    ]
                ro :> IRenderObject)

        // heap-managed per-object uniforms; ModelViewProjTrafo is DERIVED from
        // ModelTrafo (arena) + ViewProjTrafo (global) by the general rule system.
        let heap = Heap.ofRenderObjects win.Runtime (Set.ofList [ "ModelTrafo"; "HeapColor" ]) (ASet.ofArray inputs)
        heap |> ASet.toAVal |> AVal.force |> ignore
        Log.warn "real-uniforms: %d objects (Trafo3d ModelTrafo + derived ModelViewProjTrafo) -> %d bucket(s)" inputs.Length Heap.lastBucketCount

        win.Scene <- Sg.renderObjectSet heap
        win.Run()
