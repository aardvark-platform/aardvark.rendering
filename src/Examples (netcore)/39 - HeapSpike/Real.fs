namespace HeapSpike

// Real-scene uniforms: the per-object ModelTrafo is a Trafo3d (not a hand-built
// M44f) and the shader reads the standard derived ModelViewProjTrafo. The heap
// composes ModelViewProjTrafo as ViewProjTrafo * ModelTrafo in its compute
// pre-pass; the shared ViewProjTrafo occupies one arena region, so a camera move
// updates that one region and re-runs the compose, while the per-object data is
// left untouched.

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Aardvark.Application
open FShade

module Real =

    module S =
        type V = { [<Position>] pos : V4f; [<Color>] c : V4f; [<Normal>] n : V3f }

        // standard-style shader: reads the derived ModelViewProjTrafo, the per-object
        // ModelTrafo (for normals) and a per-object color.
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
                        // shared camera trafo (one aval for all objects)
                        Symbol.Create "ViewProjTrafo",  (viewProj :> IAdaptiveValue)
                    ]
                ro :> IRenderObject)

        // heap-managed per-object uniforms; ModelViewProjTrafo is derived from
        // ModelTrafo and ViewProjTrafo by the heap's derived-uniform rules.
        let heap = Heap.ofRenderObjects (win.Runtime.CreateHeapStorage()) (ASet.ofArray inputs)
        heap |> ASet.toAVal |> AVal.force |> ignore
        Log.warn "real-uniforms: %d objects (Trafo3d ModelTrafo + derived ModelViewProjTrafo) -> %d bucket(s)" inputs.Length Heap.lastBucketCount

        win.Scene <- Sg.renderObjectSet heap
        win.Run()
