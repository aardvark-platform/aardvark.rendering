namespace HeapSpike

// GPU mode partitioning (wombat derived-modes v2, adapted to MultiDrawIndirect).
//
// Fixed-function pipeline state (cull / fill / blend / depth) can't vary per-draw
// within one draw call, so "mode rules on the GPU" means: a compute kernel
// evaluates each draw's mode from per-draw GPU data and ROUTES the draw into a
// pre-built pipeline SLOT. Here a per-object value (reactive threshold) picks
// slot 0 (solid Fill) or slot 1 (wireframe Line). The kernel writes one indirect
// buffer per slot, masking InstanceCount to 0 for draws not in that slot; the
// render issues one indirect multidraw per slot with that slot's pipeline. Same
// compute -> buffer -> render model as the derived uniforms; per-draw uniforms
// are still gathered from the arena by gl_DrawID.

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open FShade
open FShade.Imperative

module GpuModes =

    [<GLSLIntrinsic("gl_DrawID", "GL_ARB_shader_draw_parameters")>]
    let private drawId () : int = onlyInShaderCode "drawId"

    [<AutoOpen>]
    module private U =
        type UniformScope with
            member x.MData : float32[] = uniform?StorageBuffer?MData   // per-draw [M44f model | V4f color], stride 20

    // partition kernel: per draw, slot = (value > threshold) ? 1 : 0; write each
    // slot's indirect entry (gl_DrawID = entry index = draw index), instanceCount
    // masked to 0 unless the draw belongs to that slot. Indexed indirect layout
    // is {indexCount, instanceCount, firstIndex, baseVertex, firstInstance}.
    [<LocalSize(X = 64)>]
    let private partition (n : int) (numSlots : int) (faceVtx : int) (threshold : float32)
                          (value : float32[]) (outp : int[]) =
        compute {
            let i = getGlobalId().X
            if i < n then
                let slot = if value.[i] > threshold then 1 else 0
                for s in 0 .. numSlots - 1 do
                    let o = (s * n + i) * 5
                    outp.[o + 0] <- faceVtx
                    outp.[o + 1] <- (if s = slot then 1 else 0)
                    outp.[o + 2] <- 0
                    outp.[o + 3] <- 0
                    outp.[o + 4] <- 0
        }

    module S =
        type V = { [<Position>] pos : V4f; [<Color>] c : V4f; [<Normal>] n : V3f }
        let shade (v : V) =
            vertex {
                let o = drawId() * 20
                let m =
                    M44f(uniform.MData.[o+0],  uniform.MData.[o+1],  uniform.MData.[o+2],  uniform.MData.[o+3],
                         uniform.MData.[o+4],  uniform.MData.[o+5],  uniform.MData.[o+6],  uniform.MData.[o+7],
                         uniform.MData.[o+8],  uniform.MData.[o+9],  uniform.MData.[o+10], uniform.MData.[o+11],
                         uniform.MData.[o+12], uniform.MData.[o+13], uniform.MData.[o+14], uniform.MData.[o+15])
                let vp : M44f = uniform?ViewProjTrafo
                return { v with pos = vp * (m * v.pos); n = m.TransformDir v.n
                                c = V4f(uniform.MData.[o+16], uniform.MData.[o+17], uniform.MData.[o+18], uniform.MData.[o+19]) }
            }
        let frag (v : V) =
            fragment {
                let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
                let d = 0.35f + 0.65f * max 0.0f (Vec.dot (Vec.normalize v.n) l)
                return V4f(v.c.XYZ * d, 1.0f)
            }

    // shared scene/compute setup; returns (slot ROs, dispatch-on-eval aval, value cval, threshold cval)
    let private build (runtime : IRuntime) =
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.7)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)

        let side = 8
        let n = side * side
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan |]
        let mdata = Array.zeroCreate<float32> (n * 20)
        let values = Array.init n (fun i -> float32 (i % 3) * 0.4f)   // per-object value driving the mode
        for i in 0 .. n - 1 do
            let p = V3d(float (i % side - side/2) * 1.4, float (i / side - side/2) * 1.4, 0.0)
            let m = (Trafo3d.Translation p).Forward |> M44f.op_Explicit
            let o = i * 20
            mdata.[o+0]<-m.M00; mdata.[o+1]<-m.M01; mdata.[o+2]<-m.M02; mdata.[o+3]<-m.M03
            mdata.[o+4]<-m.M10; mdata.[o+5]<-m.M11; mdata.[o+6]<-m.M12; mdata.[o+7]<-m.M13
            mdata.[o+8]<-m.M20; mdata.[o+9]<-m.M21; mdata.[o+10]<-m.M22; mdata.[o+11]<-m.M23
            mdata.[o+12]<-m.M30; mdata.[o+13]<-m.M31; mdata.[o+14]<-m.M32; mdata.[o+15]<-m.M33
            let c = palette.[i % palette.Length].ToV4f()
            mdata.[o+16]<-c.X; mdata.[o+17]<-c.Y; mdata.[o+18]<-c.Z; mdata.[o+19]<-c.W

        let numSlots = 2
        let dataBuf  = runtime.CreateBuffer<float32>(mdata)
        let valBuf   = runtime.CreateBuffer<float32>(values)
        let indirect = runtime.CreateBuffer<int>(numSlots * n * 5, BufferUsage.Indirect ||| BufferUsage.Storage, BufferStorage.Device)
        let threshold = AVal.init 0.5f

        let shader = runtime.CreateComputeShader partition
        let input  = runtime.CreateInputBinding shader
        let groups = (n + shader.LocalSize.X - 1) / shader.LocalSize.X
        let prog   = runtime.CompileCompute [ ComputeCommand.Bind shader; ComputeCommand.SetInput input; ComputeCommand.Dispatch groups ]

        // run the GPU partition (re-dispatched when the threshold marks)
        let partitioned =
            AVal.custom (fun t ->
                input.["n"] <- n
                input.["numSlots"] <- numSlots
                input.["faceVtx"] <- index.Length
                input.["threshold"] <- threshold.GetValue t
                input.["value"] <- valBuf
                input.["outp"] <- indirect
                input.Flush()
                prog.Run()
                indirect :> IBuffer)

        // rewrite per-draw reads -> arena gathers by gl_DrawID (MData / ViewProjTrafo stay)
        let effect = Effect.compose [ Effect.ofFunction S.shade; Effect.ofFunction S.frag ]
        let dataU = (AVal.constant (dataBuf :> IBuffer)) :> IAdaptiveValue
        // one RO per slot; slot 0 = solid fill, slot 1 = wireframe
        let slotRO (s : int) (viewProj : IAdaptiveValue) =
            let ro = RenderObject()
            ro.Surface <- Surface.Effect effect
            ro.Mode <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
            ro.Indices <- Some (bv index typeof<int>)
            ro.RasterizerState <- { RasterizerState.Default with FillMode = AVal.constant (if s = 0 then FillMode.Fill else FillMode.Line) }
            ro.DrawCalls <- DrawCalls.Indirect (partitioned |> AVal.map (fun b -> IndirectBuffer.ofBuffer true (uint64 (s * n * 5 * sizeof<int>)) (5 * sizeof<int>) n b))
            ro.Uniforms <-
                { new IUniformProvider with
                    member _.TryGetUniform(_, name) =
                        if name = Symbol.Create "MData" then ValueSome dataU
                        elif name = Symbol.Create "ViewProjTrafo" then ValueSome viewProj
                        else ValueNone
                    member _.Dispose() = () }
            ro :> IRenderObject
        n, numSlots, index.Length, indirect, partitioned, threshold, slotRO

    /// headless test: dispatch the partition, read back the per-slot masks, verify
    /// each draw lands in exactly one slot per the rule, and the totals match.
    let run () =
        Aardvark.Init()
        use app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication()
        let runtime = app.Runtime
        let n, numSlots, faceVtx, indirect, partitioned, threshold, _slotRO = build runtime
        partitioned |> AVal.force |> ignore   // dispatch
        let raw = indirect.Download()
        // expected slot per draw for threshold 0.5: value[i]=(i%3)*0.4 -> >0.5 when i%3=2
        let mutable ok = true
        let mutable s0 = 0
        let mutable s1 = 0
        for i in 0 .. n - 1 do
            let expect = if (float32 (i % 3) * 0.4f) > 0.5f then 1 else 0
            let ic0 = raw.[(0 * n + i) * 5 + 1]
            let ic1 = raw.[(1 * n + i) * 5 + 1]
            if ic0 = 1 then s0 <- s0 + 1
            if ic1 = 1 then s1 <- s1 + 1
            let got = if ic1 = 1 then 1 elif ic0 = 1 then 0 else -1
            if got <> expect || (ic0 + ic1) <> 1 then ok <- false
        Log.line "gpu-modes: n=%d slots=%d  slot0(fill)=%d slot1(wire)=%d  faceVtx=%d  routing correct=%b" n numSlots s0 s1 faceVtx ok
        let pass = ok && s0 > 0 && s1 > 0
        if pass then Log.line "gpu-modes: PASS (GPU partition routes each draw into exactly one pipeline slot)"
        else Log.warn "gpu-modes: FAIL"
        pass

    /// GL validation: same GPU partition on the OpenGL backend — dispatch the
    /// compute, verify per-slot masks, and render the per-slot pipelines from the
    /// GPU-written indirect buffer (glMultiDrawElementsIndirect + gl_DrawID).
    let runGL () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.OpenGlApplication(false)
        let runtime = app.Runtime :> IRuntime
        Log.line "gl: SupportsMultiDrawIndirectDrawId=%b" runtime.SupportsMultiDrawIndirectDrawId
        let n, numSlots, faceVtx, indirect, partitioned, _threshold, slotRO = build runtime
        partitioned |> AVal.force |> ignore   // GPU partition dispatch on GL
        let raw = indirect.Download()
        let mutable ok = true
        let mutable s0 = 0
        let mutable s1 = 0
        for i in 0 .. n - 1 do
            let expect = if (float32 (i % 3) * 0.4f) > 0.5f then 1 else 0
            let ic0 = raw.[(0 * n + i) * 5 + 1]
            let ic1 = raw.[(1 * n + i) * 5 + 1]
            if ic0 = 1 then s0 <- s0 + 1
            if ic1 = 1 then s1 <- s1 + 1
            let got = if ic1 = 1 then 1 elif ic0 = 1 then 0 else -1
            if got <> expect || (ic0 + ic1) <> 1 then ok <- false
        // render the two GPU-routed pipeline slots and measure coverage
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 13.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        use task = runtime.CompileRender(signature, ASet.ofList [ slotRO 0 viewProj; slotRO 1 viewProj ])
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        let m = out.GetValue().Download().AsPixImage<uint8>().GetMatrix<C4b>()
        let mutable coverage = 0L
        m.ForeachCoord(fun (p : V2l) -> let v = m.[p] in if v.R <> 0uy || v.G <> 0uy || v.B <> 0uy then coverage <- coverage + 1L)
        out.Release()
        Log.line "gpu-modes-gl: n=%d  slot0(fill)=%d slot1(wire)=%d  routing=%b  coverage=%d  faceVtx=%d" n s0 s1 ok coverage faceVtx
        let pass = ok && s0 > 0 && s1 > 0 && coverage > 20000L
        if pass then Log.line "gpu-modes-gl: PASS (GPU partition + per-slot indirect render on the GL backend)"
        else Log.warn "gpu-modes-gl: FAIL (routing=%b coverage=%d)" ok coverage
        pass

    /// windowed: GPU routes cubes to solid vs wireframe pipelines; press T to bump
    /// the threshold (re-partitions on the GPU, no CPU re-bucket).
    let runWin () =
        Aardvark.Init()
        let win = window { backend Backend.Vulkan; display Display.Mono; debug false; samples 8 }
        let runtime = win.Runtime
        let _, _, _, _, _partitioned, threshold, slotRO = build runtime
        let viewProj = AVal.map2 (fun (v : Trafo3d[]) (p : Trafo3d[]) -> v.[0] * p.[0]) win.View win.Proj :> IAdaptiveValue
        win.Keyboard.DownWithRepeats.Values.Add (fun k ->
            if k = Aardvark.Application.Keys.T then
                transact (fun () -> threshold.Value <- (if threshold.Value > 0.5f then 0.1f else 0.9f))
                Log.line "threshold -> %f (GPU re-partition)" threshold.Value)
        win.Scene <- Sg.renderObjectSet (ASet.ofList [ slotRO 0 viewProj; slotRO 1 viewProj ])
        Log.warn "gpu-modes window: cubes routed to solid/wireframe pipelines by a GPU compute kernel (press T to re-threshold)"
        win.Run()
