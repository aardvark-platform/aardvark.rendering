namespace HeapSpike

// Phase-3: bindless-style per-object textures. Each object stores a texture
// INDEX in the arena; the fragment samples Textures.[index] from a shared
// texture array. N objects with different textures -> ONE draw, no per-object
// descriptor sets, no atlas. (Index read in the vertex stage via
// gl_InstanceIndex and passed as a flat varying — fragment can't read it.)

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Aardvark.Application
open FShade

module Textures =

    [<Literal>]
    let TexCount = 32

    module S =
        type Vertex =
            { [<Position>]                                  pos : V4f
              [<Normal>]                                    n   : V3f
              [<Semantic("TexCoord")>]                      tc  : V2f
              [<Semantic("TexId"); Interpolation(InterpolationMode.Flat)>] ti : int }

        let shade (v : Vertex) =
            vertex {
                let m  : M44f = uniform?HeapModelTrafo
                let vp : M44f = uniform?ViewProjTrafo
                let ti : int  = uniform?HeapTexIndex          // per-object, from arena
                return { v with
                            pos = vp * (m * v.pos)
                            n   = m.TransformDir v.n
                            tc  = v.pos.XY + V2f(0.5f, 0.5f)  // crude planar uv from local pos
                            ti  = ti }
            }

        // -1 == unbounded (bindless) runtime-sized array -> `uniform sampler2D Textures[]`
        let textures =
            sampler2d {
                textureArray uniform?Textures -1
                filter Filter.MinMagMipLinear
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        let frag (v : Vertex) =
            fragment {
                let albedo = textures.[v.ti].Sample(v.tc).XYZ
                let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
                let d = 0.35f + 0.65f * max 0.0f (Vec.dot (Vec.normalize v.n) l)
                return V4f(albedo * d, 1.0f)
            }

    let private mkTexture (i : int) : ITexture =
        let cols = [| C3b(230,60,60); C3b(60,200,60); C3b(60,120,230); C3b(230,200,40)
                      C3b(210,60,210); C3b(40,210,210); C3b(230,140,40); C3b(230,230,230) |]
        let col = cols.[i % cols.Length]
        let img = PixImage<byte>(Col.Format.RGBA, V2i(64, 64))
        img.GetMatrix<C4b>().SetByIndex(fun (idx : int64) ->
            let x = int idx % 64
            let y = int idx / 64
            if ((x / 8) + (y / 8)) % 2 = 0 then C4b(col) else C4b.White) |> ignore
        PixTexture2d(img) :> ITexture

    let run () =
        Aardvark.Init()
        // Use `Aardvark.Rendering.Vulkan.DebugConfig.Normal` to print the
        // compiled GLSL (shows `textures[nonuniformEXT(...)]`) + run validation.
        let win = window { backend Backend.Vulkan; display Display.Mono; debug false; samples 8 }

        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.8)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]

        let texArray : ITexture[] = Array.init TexCount mkTexture
        let viewProj : aval<Trafo3d> = AVal.map2 (fun (v : Trafo3d[]) (p : Trafo3d[]) -> v.[0] * p.[0]) win.View win.Proj

        let grid = [| for x in 0 .. 7 do for y in 0 .. 7 -> V3d(float (x - 4) * 1.3, float (y - 4) * 1.3, 0.0) |]
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
                        Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                        Symbol.Create "HeapTexIndex",   (AVal.constant (i % TexCount) :> IAdaptiveValue)
                        Symbol.Create "ViewProjTrafo",  (viewProj :> IAdaptiveValue)
                        Symbol.Create "Textures",       (AVal.constant texArray :> IAdaptiveValue)
                    ]
                ro :> IRenderObject)

        // per-object texture index lives in the arena; the texture array is a
        // shared global (delegated). N objects -> one bucket / one indirect draw.
        let heap = Heap.ofRenderObjects (win.Runtime.CreateHeapStorage()) (ASet.ofArray inputs)
        heap |> ASet.toAVal |> AVal.force |> ignore
        Log.warn "phase-3 textures: %d objects, %d textures via array index in the arena -> %d bucket(s)" inputs.Length TexCount Heap.lastBucketCount

        win.Scene <- Sg.renderObjectSet heap
        win.Run()
