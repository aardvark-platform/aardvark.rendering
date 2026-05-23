namespace HeapSpike

// Phase-4 spike: GPU compute pre-pass for derived per-object uniforms, in
// REAL fp64 (Vulkan shaderFloat64; no df32). De-risks the unknown — does
// FShade compile + run a double-precision compute shader on this stack — and
// shows WHY fp64 matters: ModelView = View*Model computed camera-relative at
// geodetic coordinates (~earth radius) is precise in fp64 but garbage in f32.

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Application
open FShade

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
        use app = new Aardvark.Application.Slim.VulkanApplication(false)
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
