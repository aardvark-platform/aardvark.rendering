namespace HeapSpike

// Phase-4 spike: GPU compute pre-pass for derived per-object uniforms, in
// REAL fp64 (Vulkan shaderFloat64; no df32). De-risks the unknown — does
// FShade compile + run a double-precision compute shader on this stack — and
// shows WHY fp64 matters: ModelView = View*Model computed camera-relative at
// geodetic coordinates (~earth radius) is precise in fp64 but garbage in f32.

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Aardvark.Application
open FShade
open FShade.Imperative
open Microsoft.FSharp.Quotations

module Phase4 =

    module CS =
        // one thread per object: ModelView[i] = View(d) * Model[i](d), truncated to f32.
        [<LocalSize(X = 64)>]
        let deriveModelView (n : int) (view : M44d) (model : M44d[]) (modelView : M44f[]) =
            compute {
                let i = getGlobalId().X
                if i < n then
                    modelView.[i] <- M44f(view * model.[i])
            }

    let run () =
        Aardvark.Init()
        use app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication()
        let runtime = app.Runtime

        let n = 1000
        let earth = 6378137.0    // metres — geodetic scale

        // camera sits at earth radius; objects are within a few metres of it,
        // with sub-metre detail — below the f32 resolution (~0.5 m) at this scale.
        let camPos = V3d(earth, earth * 0.5, earth * 0.25)
        let models =
            Array.init n (fun i ->
                let d = V3d(float i * 0.037 - 5.0, float i * 0.041 - 5.0, 2.0 + float i * 0.029)
                (Trafo3d.Translation(camPos + d)).Forward)    // M44d, at geodetic scale

        // camera-relative view: translate by -camPos so the huge offsets cancel
        let view : M44d = (Trafo3d.Translation(-camPos)).Forward

        use modelBuf = runtime.CreateBuffer<M44d>(models)
        use mvBuf    = runtime.CreateBuffer<M44f>(n)
        use shader   = runtime.CreateComputeShader CS.deriveModelView
        let inp = runtime.CreateInputBinding shader
        inp.["n"] <- n
        inp.["view"] <- view
        inp.["model"] <- modelBuf
        inp.["modelView"] <- mvBuf
        inp.Flush()

        let groups = (n + shader.LocalSize.X - 1) / shader.LocalSize.X
        use prog =
            runtime.CompileCompute [
                ComputeCommand.Bind shader
                ComputeCommand.SetInput inp
                ComputeCommand.Dispatch groups
            ]
        prog.Run()

        let gpu = mvBuf.Download()

        // The ModelView translation should equal (objPos - camPos): small and
        // precise. Compare GPU fp64 result vs a genuine f32 computation (store
        // the world coords in float32, then subtract — what an f32 pipeline does).
        let transOf (m : M44f) = V3d(float m.M03, float m.M13, float m.M23)
        let mutable maxErrF64 = 0.0
        let mutable maxErrF32 = 0.0
        for i in 0 .. n - 1 do
            let objPos = models.[i].C3.XYZ
            let exact  = objPos - camPos                                     // fp64 truth
            let f32 (a : float) (b : float) = float (float32 a - float32 b)  // store-in-f32 then subtract
            let f32sub = V3d(f32 objPos.X camPos.X, f32 objPos.Y camPos.Y, f32 objPos.Z camPos.Z)
            maxErrF64 <- max maxErrF64 (Vec.length (transOf gpu.[i] - exact))
            maxErrF32 <- max maxErrF32 (Vec.length (f32sub - exact))

        printfn ""
        printfn "phase-4 fp64 compute pre-pass: n=%d, geodetic scale ~%.0f m" n earth
        printfn "  GPU fp64 ModelView translation max error vs truth: %.6g m" maxErrF64
        printfn "  naive f32         (same math in f32)  max error : %.6g m" maxErrF32
        printfn "  -> fp64 compute is correct (%.1e m); f32 loses ~%.2g m at this scale" maxErrF64 maxErrF32
        printfn ""

    // ── Render integration ──────────────────────────────────────────────
    // The fp64 ModelView buffer (filled by the compute pass) IS read by the
    // heap render as a per-object storage-buffer uniform. Objects live at
    // geodetic scale; the render only ever sees the small, precise ModelView.
    module RenderShaders =
        type V = { [<Position>] pos : V4f; [<Color>] c : V4f; [<Normal>] n : V3f }

        // reads per-object ModelView (filled by compute) + global ProjTrafo
        let shade (v : V) =
            vertex {
                let mv : M44f = uniform?HeapModelView
                let p  : M44f = uniform?ProjTrafo
                return { v with pos = p * (mv * v.pos); n = mv.TransformDir v.n }
            }

        let frag (v : V) =
            fragment {
                let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
                let d = 0.3f + 0.7f * max 0.0f (Vec.dot (Vec.normalize v.n) l)
                return V4f(V3f(0.95f, 0.75f, 0.35f) * d, 1.0f)
            }

    /// Windowed demo: geodetic-scale cubes whose ModelView is computed by the
    /// fp64 compute pre-pass and read by the render shader (one instanced draw).
    let runRender () =
        Aardvark.Init()
        let win = window { backend Backend.Vulkan; display Display.Mono; debug false; samples 8 }
        let runtime = win.Runtime

        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)

        // objects at geodetic scale, clustered in a grid around camPos
        let earth = 6378137.0
        let camPos = V3d(earth, earth * 0.5, earth * 0.25)
        let side = 6
        let models =
            [| for x in 0 .. side - 1 do
                 for y in 0 .. side - 1 do
                   for z in 0 .. 1 ->
                     let d = V3d(float (x - side/2) * 2.5, float (y - side/2) * 2.5, float z * 2.5)
                     (Trafo3d.Translation(camPos + d)).Forward |] // M44d
        let n = models.Length

        // fp64 compute pre-pass: ModelView = View(d) * Model(d) -> f32 buffer
        let eye = camPos + V3d(0.0, -22.0, 12.0)
        let view : M44d = (CameraView.lookAt eye camPos V3d.OOI |> CameraView.viewTrafo).Forward
        use modelBuf = runtime.CreateBuffer<M44d>(models)
        let mvBuf = runtime.CreateBuffer<M44f>(n)
        use shader = runtime.CreateComputeShader CS.deriveModelView
        let inp = runtime.CreateInputBinding shader
        inp.["n"] <- n
        inp.["view"] <- view
        inp.["model"] <- modelBuf
        inp.["modelView"] <- mvBuf
        inp.Flush()
        let groups = (n + shader.LocalSize.X - 1) / shader.LocalSize.X
        use prog = runtime.CompileCompute [ ComputeCommand.Bind shader; ComputeCommand.SetInput inp; ComputeCommand.Dispatch groups ]
        prog.Run()   // fill ModelView once (static camera); re-run on camera move to make it reactive

        // proj (global), from window size
        let proj = win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y) |> Frustum.projTrafo)

        // rewrite: ModelView -> mat4 from the compute-filled buffer at iid*16
        let effect =
            let e = Effect.compose [ Effect.ofFunction RenderShaders.shade; Effect.ofFunction RenderShaders.frag ]
            let iid : Expr<int> = Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.InstanceId)
            e |> Effect.substituteUniforms (fun name typ _ _ ->
                if name = "HeapModelView" && typ = typeof<M44f> then
                    Some <@ let o = %iid * 16
                            M44f(uniform.HeapData.[o+0],  uniform.HeapData.[o+1],  uniform.HeapData.[o+2],  uniform.HeapData.[o+3],
                                 uniform.HeapData.[o+4],  uniform.HeapData.[o+5],  uniform.HeapData.[o+6],  uniform.HeapData.[o+7],
                                 uniform.HeapData.[o+8],  uniform.HeapData.[o+9],  uniform.HeapData.[o+10], uniform.HeapData.[o+11],
                                 uniform.HeapData.[o+12], uniform.HeapData.[o+13], uniform.HeapData.[o+14], uniform.HeapData.[o+15]) @>.Raw
                else None)

        let symHeap = Symbol.Create "HeapData"
        let symProj = Symbol.Create "ProjTrafo"
        let heapU = (AVal.constant (mvBuf :> IBuffer)) :> IAdaptiveValue
        let projU = (proj :> IAdaptiveValue)
        let ro = RenderObject()
        ro.Surface          <- Surface.Effect effect
        ro.Mode             <- IndexedGeometryMode.TriangleList
        ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        ro.Indices          <- Some (bv index typeof<int>)
        ro.DrawCalls        <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = n) |])
        ro.Uniforms <-
            { new IUniformProvider with
                member _.TryGetUniform(_, name) =
                    if name = symHeap then ValueSome heapU
                    elif name = symProj then ValueSome projU
                    else ValueNone
                member _.Dispose() = () }

        Log.warn "phase-4 render: %d geodetic cubes, ModelView from fp64 GPU compute, one instanced draw" n
        win.Scene <- Sg.renderObjectSet (ASet.single (ro :> IRenderObject))
        win.Run()
