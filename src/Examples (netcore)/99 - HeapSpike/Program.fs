(*
    Phase-0 heap spike (Vulkan).

    Proves the "heap" render path end-to-end on the GPU:
      - an ORDINARY effect reads its per-draw data (HeapModelTrafo, HeapColor)
        as plain uniforms;
      - a mechanical rewrite (Effect.substituteUniforms) redirects those reads
        to gathers out of a shared storage-buffer "arena" (HeapData), indexed
        per-draw via gl_InstanceIndex (firstInstance routing);
      - the camera uniform (ViewProjTrafo) is left untouched -> stays a UBO;
      - N independent draws of one shared box are issued as ONE
        vkCmdDrawIndexedIndirect (multidraw), each with FirstInstance = slot.

    If we see N boxes at distinct grid positions with distinct colors, every
    bit of per-draw state came from the arena through the rewritten shader.
*)

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open FShade
open FShade.Imperative
open Microsoft.FSharp.Quotations

module HeapShaders =

    // The arena: one f32 data buffer + one i32 header buffer (per-draw offsets).
    type UniformScope with
        member x.HeapData    : float32[] = uniform?StorageBuffer?HeapData
        member x.HeapHeaders : int[]     = uniform?StorageBuffer?HeapHeaders

    type Vertex =
        { [<Position>] pos : V4f
          [<Color>]    c   : V4f
          [<Normal>]   n   : V3f }

    // Perfectly ordinary shader. Reads per-draw model trafo & color as plain
    // uniforms; reads the camera normally.
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

module Heap =
    open HeapShaders

    // floats per draw row in HeapData: 16 (mat4) + 4 (color)
    let dataStride = 20
    // header fields per draw
    let fieldStride = 2

    /// The rewrite: redirect HeapModelTrafo / HeapColor uniform reads to arena
    /// gathers indexed by gl_InstanceIndex. Everything else (ViewProjTrafo)
    /// passes through untouched.
    let rewrite (e : Effect) =
        let layout = Map.ofList [ "HeapModelTrafo", 0; "HeapColor", 1 ]
        let iid : Expr<int> = Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.InstanceId)
        let cint (v : int) : Expr<int> = Expr.Value v |> Expr.Cast
        e |> Effect.substituteUniforms (fun name typ _ _ ->
            match Map.tryFind name layout with
            | None -> None
            | Some fi ->
                let off : Expr<int> = <@ uniform.HeapHeaders.[ %iid * 2 + %(cint fi) ] @>
                if typ = typeof<M44f> then
                    Some <@ let o = %off in
                            M44f(uniform.HeapData.[o+0],  uniform.HeapData.[o+1],  uniform.HeapData.[o+2],  uniform.HeapData.[o+3],
                                 uniform.HeapData.[o+4],  uniform.HeapData.[o+5],  uniform.HeapData.[o+6],  uniform.HeapData.[o+7],
                                 uniform.HeapData.[o+8],  uniform.HeapData.[o+9],  uniform.HeapData.[o+10], uniform.HeapData.[o+11],
                                 uniform.HeapData.[o+12], uniform.HeapData.[o+13], uniform.HeapData.[o+14], uniform.HeapData.[o+15]) @>.Raw
                elif typ = typeof<V4f> then
                    Some <@ let o = %off in V4f(uniform.HeapData.[o+0], uniform.HeapData.[o+1], uniform.HeapData.[o+2], uniform.HeapData.[o+3]) @>.Raw
                else
                    None)

    let private packM44 (m : M44f) (dst : float32[]) (o : int) =
        dst.[o+0]  <- m.M00; dst.[o+1]  <- m.M01; dst.[o+2]  <- m.M02; dst.[o+3]  <- m.M03
        dst.[o+4]  <- m.M10; dst.[o+5]  <- m.M11; dst.[o+6]  <- m.M12; dst.[o+7]  <- m.M13
        dst.[o+8]  <- m.M20; dst.[o+9]  <- m.M21; dst.[o+10] <- m.M22; dst.[o+11] <- m.M23
        dst.[o+12] <- m.M30; dst.[o+13] <- m.M31; dst.[o+14] <- m.M32; dst.[o+15] <- m.M33

    /// Build the arena (data + headers) for N draws.
    let build (trafos : M44f[]) (colors : C4f[]) =
        let n = trafos.Length
        let data    = Array.zeroCreate<float32> (n * dataStride)
        let headers = Array.zeroCreate<int> (n * fieldStride)
        for i in 0 .. n - 1 do
            let baseOff = i * dataStride
            packM44 trafos.[i] data baseOff
            let c = colors.[i % colors.Length]
            data.[baseOff+16] <- c.R; data.[baseOff+17] <- c.G
            data.[baseOff+18] <- c.B; data.[baseOff+19] <- c.A
            headers.[i*fieldStride+0] <- baseOff        // HeapModelTrafo offset
            headers.[i*fieldStride+1] <- baseOff + 16   // HeapColor offset
        data, headers

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
    let g = IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.8)) C4b.White
    let g = g.ToIndexed()
    let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
    let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
    let indices   = g.IndexArray |> unbox<int[]>
    let faceVertexCount = indices.Length

    // N independent draws, laid out on a grid, each a distinct color.
    let grid = [| for x in -1 .. 1 do for y in -1 .. 1 -> V3d(float x * 1.5, float y * 1.5, 0.0) |]
    let trafos = grid |> Array.map (fun p -> Trafo3d.Translation p |> (fun t -> t.Forward |> M44f.op_Explicit))
    let palette =
        [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold
           C4f.Magenta; C4f.Cyan; C4f.Orange; C4f.White; C4f.HotPink |]
    let n = trafos.Length

    let data, headers = Heap.build trafos palette

    // one multidraw-indirect: one sub-draw per object, firstInstance = slot.
    let indirect =
        Array.init n (fun i ->
            DrawCallInfo(
                FaceVertexCount = faceVertexCount,
                FirstIndex = 0, BaseVertex = 0,
                FirstInstance = i, InstanceCount = 1))
        |> IndirectBuffer.ofArray

    let effect =
        Effect.compose [ Effect.ofFunction HeapShaders.shade; Effect.ofFunction HeapShaders.shadeFrag ]
        |> Heap.rewrite

    let sg =
        Sg.indirectDraw IndexedGeometryMode.TriangleList (AVal.constant indirect)
        |> Sg.vertexBuffer DefaultSemantic.Positions (BufferView(AVal.constant (ArrayBuffer(positions) :> IBuffer), typeof<V3f>))
        |> Sg.vertexBuffer DefaultSemantic.Normals   (BufferView(AVal.constant (ArrayBuffer(normals)   :> IBuffer), typeof<V3f>))
        |> Sg.index' indices
        |> Sg.uniform "HeapData"    (AVal.constant data)
        |> Sg.uniform "HeapHeaders" (AVal.constant headers)
        |> Sg.effect [ effect ]

    Log.warn "HeapSpike: %d objects in ONE indirect draw, all per-draw data from the arena" n

    win.Scene <- sg
    win.Run()
    0
