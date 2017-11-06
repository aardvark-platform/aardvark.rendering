namespace Examples


open System
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.Text
open System.Runtime.InteropServices
open Aardvark.SceneGraph

module ComputeShader =

    module Shaders =
        open FShade

        [<LocalSize(X = 64)>]
        let normalized (a : V4d[]) (b : V4d[]) =
            compute {
                let i = getGlobalId().X
                b.[i] <- V4d(Vec.normalize a.[i].XYZ, 1.0)
            }

        let G = 0.01
        
        [<LocalSize(X = 64)>]
        let updateAcceleration (n : int) (pos : V4d[]) (acc : V4d[]) (masses : float[]) =
            compute {
                let i = getGlobalId().X
                if i < n then
                    let p = pos.[i].XYZ

                    let mi = masses.[i]
                    let mutable F = V3d.Zero
                    for j in 0 .. n - 1 do
                        if i <> j then
                            let o = pos.[j].XYZ

                            let diff = o - p
                            let l = Vec.lengthSquared diff
                            let dist = sqrt l
                            if dist > 0.2 then
                                let dir = diff / dist
                                F <- F + dir * ((mi * masses.[j] * G) / l)
                            
                    acc.[i] <- V4d(F / masses.[i], 0.0)

            }
            
        [<LocalSize(X = 64)>]
        let step (n : int) (dt : float) (pos : V4d[]) (vel : V4d[]) (acc : V4d[]) =
            compute {
                let i = getGlobalId().X
                if i < n then
                    let p = pos.[i].XYZ
                    let v = vel.[i].XYZ
                    let a = acc.[i].XYZ

                    let p = p + v * dt //+ a * (0.5 * dt * dt)
                    let v = v + dt * a

                    pos.[i] <- V4d(p, 1.0)
                    vel.[i] <- V4d(v, 0.0)
            }


        type Vertex =
            {
                [<WorldPosition>]           wp : V4d
                [<Position>]                pos : V4d
                [<Semantic("Velocity")>]    vel : V3d
                [<Semantic("Mass")>]        mass  : float
                [<Color>]                   c  : V4d
            }

        [<ReflectedDefinition>]
        let hsv2rgb (h : float) (s : float) (v : float) =
            let s = clamp 0.0 1.0 s
            let v = clamp 0.0 1.0 v

            let h = h % 1.0
            let h = if h < 0.0 then h + 1.0 else h
            let hi = floor ( h * 6.0 ) |> int
            let f = h * 6.0 - float hi
            let p = v * (1.0 - s)
            let q = v * (1.0 - s * f)
            let t = v * (1.0 - s * ( 1.0 - f ))
            match hi with
                | 1 -> V3d(q,v,p)
                | 2 -> V3d(p,v,t)
                | 3 -> V3d(p,q,v)
                | 4 -> V3d(t,p,v)
                | 5 -> V3d(v,p,q)
                | _ -> V3d(v,t,p)

        let point (p : Point<Vertex>) =
            line {
                let t : float = uniform?Magic

                

                let vel = p.Value.vel
                let wp0 =  V4d(p.Value.wp.XYZ - 0.05 * vel, 1.0 + (t * 1.0E-50))

                let color = hsv2rgb (p.Value.mass / 100.0) 1.0 1.0

                yield { p.Value with c = V4d(color, 1.0) }
                yield { p.Value with wp = wp0; pos = uniform.ViewProjTrafo * wp0; c = V4d.OOOI }

            }

    let run() =
        use app = new VulkanApplication(false)
        let runtime = app.Runtime :> IRuntime



        let update = runtime.CompileCompute Shaders.updateAcceleration
        let step = runtime.CompileCompute Shaders.step

        let rand = RandomSystem()
        let particeCount = 200
        let positions = runtime.CreateBuffer<V4f>(Array.init particeCount (fun _ -> V4d(rand.UniformV3dDirection() * 3.0, 1.0) |> V4f))
        let velocities = runtime.CreateBuffer<V4f>(Array.zeroCreate particeCount)
        let accelerations = runtime.CreateBuffer<V4f>(Array.zeroCreate particeCount)
        let masses = runtime.CreateBuffer<float32>(Array.init particeCount (fun _ -> 1.0f))

        positions.Upload([| V4f(-1.0f, 0.5f, 0.0f, 1.0f);  V4f(1.0f, -0.5f, 0.0f, 1.0f); |])
        masses.Upload([| 150.0f; 150.0f |])
        velocities.Upload([| V4f(0.2f, 0.0f, 0.0f, 1.0f); V4f(-0.2f, 0.0f, 0.0f, 1.0f) |])


        let win = app.CreateSimpleRenderWindow(8)

        let updateInputs = runtime.NewInputBinding update
        updateInputs.["pos"] <- positions
        updateInputs.["acc"] <- accelerations
        updateInputs.["masses"] <- masses
        updateInputs.["n"] <- particeCount
        updateInputs.Flush()

        let stepInputs = runtime.NewInputBinding step
        stepInputs.["pos"] <- positions
        stepInputs.["vel"] <- velocities
        stepInputs.["acc"] <- accelerations
        stepInputs.["n"] <- particeCount
        stepInputs.["dt"] <- 0.0
        stepInputs.Flush()

        let magic =
            let sw = System.Diagnostics.Stopwatch()
            let groupSize = 
                if particeCount % update.LocalSize.X = 0 then 
                    particeCount / update.LocalSize.X
                else
                    1 + particeCount / update.LocalSize.X


            win.Time |> Mod.map (fun _ ->
                let dt = sw.Elapsed.TotalSeconds
                sw.Restart()

                if dt < 0.2 then
                    let maxStep = 0.01
                    let mutable t = 0.0
                    while t < dt do
                        let rdt = min maxStep (dt - t)
                        stepInputs.["dt"] <-rdt
                        stepInputs.Flush()

                        runtime.Invoke(update, groupSize, updateInputs)
                        runtime.Invoke(step, groupSize, stepInputs)
                        t <- t + rdt
                
                else
                    printfn "bad: %A" dt

                0.0
                ///positions :> IBuffer

            )



        let view = 
            CameraView.lookAt (V3d(4,4,4)) V3d.Zero V3d.OOI
                |> DefaultCameraController.control win.Mouse win.Keyboard win.Time 
                |> Mod.map CameraView.viewTrafo

        let proj =
            win.Sizes 
                |> Mod.map (fun s -> Frustum.perspective 60.0 0.05 1000.0 (float s.X / float s.Y))
                |> Mod.map Frustum.projTrafo

        let call = DrawCallInfo(FaceVertexCount = 1024, InstanceCount = 1)
        
        win.RenderTask <-
            Sg.render IndexedGeometryMode.PointList call
                |> Sg.vertexBuffer DefaultSemantic.Positions (BufferView(Mod.constant (positions :> IBuffer), typeof<V4f>))
                |> Sg.vertexBuffer (Symbol.Create "Velocity") (BufferView(Mod.constant (velocities :> IBuffer), typeof<V4f>))
                |> Sg.vertexBuffer (Symbol.Create "Mass") (BufferView(Mod.constant (masses :> IBuffer), typeof<float32>))
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! Shaders.point 
                    do! DefaultSurfaces.thickLine 
                    //do! DefaultSurfaces.constantColor C4f.Red
                }
                |> Sg.viewTrafo view
                |> Sg.projTrafo proj
                |> Sg.uniform "PointSize" (Mod.constant 5.0)
                |> Sg.uniform "LineWidth" (Mod.constant 5.0)
                |> Sg.uniform "Magic" magic
                |> Sg.compile runtime win.FramebufferSignature

        win.Run()
        positions.Dispose()
        accelerations.Dispose()
        velocities.Dispose()
        updateInputs.Dispose()
        stepInputs.Dispose()
        runtime.Delete update
        runtime.Delete step

