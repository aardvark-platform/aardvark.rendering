namespace Examples


open System
open Aardvark.Base
open FSharp.Data.Adaptive

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open FSharp.Data.Adaptive.Operators
open Aardvark.GPGPU
open Aardvark.Rendering
open Aardvark.Rendering.ShaderReflection
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
        let toTarget (n : int) (src : int[]) (bits : int[]) (targets : int[]) (result : int[]) =
            compute {
                let id = getGlobalId().X
                if id < n then
                    let inputValue = src.[id]
                    let targetLocation = targets.[id]-1
                    if bits.[id] = 1 then
                        result.[targetLocation] <- inputValue
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
                [<Semantic("Offset")>]      o : V4d
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

        let instanceOffset (v : Vertex) =
            vertex {
                let magic : float = uniform?Magic
                let scale : float = uniform?Scale 

                let scale1 = scale * (1.0 + Fun.Log2 v.mass)

                return { v with pos = V4d(scale1 * v.pos.XYZ, v.pos.W) + V4d(v.o.XYZ,magic); c = V4d(hsv2rgb (v.mass / 100.0) 1.0 1.0, 1.0) }
            }

        let point (p : Point<Vertex>) =
            line {
                let t : float = uniform?Magic

                

                let vel = p.Value.vel
                let wp0 =  V4d(p.Value.wp.XYZ - 0.05 * vel, 1.0 + (t * 1.0E-50))

                let color = hsv2rgb (p.Value.mass / 100.0) 1.0 1.0

                yield { p.Value with c = V4d(color, 1.0) }
                yield { p.Value with wp = wp0; pos = uniform.ViewProjTrafo * wp0; c = V4d.OOOI }

            }

    open Aardvark.Application.OpenVR


    let benchmark (iter : int) (alternatives : list<string * (unit -> 'a)>) =
        let alternatives = Map.ofList alternatives
        let results = alternatives |> Map.map (fun _ _ -> Array.zeroCreate iter)

        for (name, run) in Map.toSeq alternatives do
            let res = results.[name]
            
            for i in 0 .. min (iter - 1) 10 do
                res.[i] <- run()

            let sw = System.Diagnostics.Stopwatch()
            sw.Start() 
            for i in 0 .. iter - 1 do
                res.[i] <- run()
            sw.Stop()
            Log.line "%s: %A" name (sw.MicroTime / iter)

        let res = results |> Map.toSeq |> Seq.map snd |> Seq.toArray
        let cnt = res.Length

        let mutable errorIndex = -1
        for i in 0 .. iter - 1 do
            let v = res.[0].[i]
            for j in 1 .. cnt - 1 do
                if res.[j].[i] <> v then errorIndex <- i

        if errorIndex >= 0 then
            let values = results |> Map.map (fun _ a -> a.[errorIndex])
            Log.warn "ERROR"
            for (name, value) in Map.toSeq values do
                Log.warn "%s: %A" name value
        else
            Log.line "OK"


                    
                








    let run() =
        use app = new VulkanApplication(true)
        //use app = new OpenGlApplication(true)
        let runtime = app.Runtime :> IRuntime
        let win = app.CreateSimpleRenderWindow(8) 
        let run() = win.Run()
        let view = 
            CameraView.lookAt (V3d(4,4,4)) V3d.Zero V3d.OOI
                |> DefaultCameraController.control win.Mouse win.Keyboard win.Time 
                |> AVal.map CameraView.viewTrafo
        let proj =
            win.Sizes 
                |> AVal.map (fun s -> Frustum.perspective 60.0 0.05 1000.0 (float s.X / float s.Y))
                |> AVal.map Frustum.projTrafo
                :> IAdaptiveValue
        let win = win :> IRenderTarget
        let subscribe (f : unit -> unit) = ()
        
        let par = ParallelPrimitives(runtime)

        let checkerboardPix = 
            let pi = PixVolume<byte>(Col.Format.RGBA, V3i(64,64,64))
            pi.GetVolume<C4b>().SetByCoord(fun (c : V3l) ->
                let c = c / 8L
                if (c.X + c.Y + c.Z) % 2L = 0L then
                    if c.X = 0L && c.Y = 0L && c.Z = 0L then
                        C4b.Red
                    else
                        C4b.White
                else
                    C4b.Black
            ) |> ignore
            pi

        //checkerboardPix.SaveAsImage @"input.png"

        let checkerboard =
            PixTexture3d(checkerboardPix, TextureParams.empty)  :> ITexture
            //PixTexture2d( [| checkerboardPix :> PixImage |], false) :> ITexture

        let img = runtime.PrepareTexture(checkerboard)
        let dst = runtime.CreateTexture(img.Size, TextureDimension.Texture3D, TextureFormat.Rgba32f, 1, 1)

        par.Scan(<@ (+) @>, img.[TextureAspect.Color, 0, 0], dst.[TextureAspect.Color, 0, 0])

        
//
//        
//
//
//        let img = runtime.Download(dst) |> unbox<PixImage<float32>>
//        let mat = img.GetMatrix<C4f>()
//
//        let diffImg = PixImage<byte>(Col.Format.RGBA, img.Size)
//        let diffMat = diffImg.GetMatrix<C4b>()
//
//
//        Log.line "diff oida"
//        diffMat.SetByCoord(fun (c : V2l) ->
//            let x = c.X
//            let y = c.Y
//            let px  = if x > 0L then mat.[x - 1L, y].ToV4f() else V4f.Zero
//            let py  = if y < mat.Size.Y - 1L then mat.[x, y + 1L].ToV4f() else V4f.Zero
//            let pxy = if x > 0L && y < mat.Size.Y - 1L then mat.[x - 1L, y + 1L].ToV4f() else V4f.Zero
//            let s   = mat.[x, y].ToV4f()
//
//            let v = s - px - py + pxy
//
//            v.ToC4f().ToC4b()
//        ) |> ignore
//        diffImg.SaveAsImage @"sepp.png"


        Environment.Exit 0




//
//
//        let data = PixImage<float32>(Col.Format.RGBA, V2i(1234, 3241))
//        let rand = RandomSystem()
//        data.GetMatrix<C4f>().SetByCoord(fun (c : V2l) ->
//            let r = rand.UniformFloat() // * 0.5f + 0.5f
//            let g = rand.UniformFloat() // * 0.5f + 0.5f
//            let b = rand.UniformFloat() // * 0.5f + 0.5f
//            C4f(r,g,b,1.0f)
//            //C4b(rand.UniformInt(256), rand.UniformInt(256), rand.UniformInt(256), 255)
//        ) |> ignore
//
//        
//        let f = data.Size.X * data.Size.Y |> float32
//        let cmp = 
//            let data = data.GetMatrix<C4f>()
//            let mutable sum = V4f.Zero
//            data.ForeachIndex(fun i ->
//                let v = data.[i] |> V4f
//                sum <- sum + v / f //(V4f v / V4f(255.0, 255.0, 255.0, 255.0))
//            )
//            sum
//
//
//        let img = runtime.PrepareTexture(PixTexture2d(PixImageMipMap [| data :> PixImage |], TextureParams.mipmapped))
//
//        let res = par.MapReduce(<@ fun _ (v : V4f) -> v / 3999394.0f @>, <@ (+) @>, img.[TextureAspect.Color, 0, 0])
//        printfn "CPU: %A" cmp
//        printfn "GPU: %A" res
//        
//        let data = PixVolume<float32>(Col.Format.RGBA, V3i(128,128,128))
//        
//        let rand = RandomSystem()
//        data.GetVolume<C4f>().SetByCoord(fun (c : V3l) ->
//            let r = rand.UniformFloat() // * 0.5f + 0.5f
//            let g = rand.UniformFloat() // * 0.5f + 0.5f
//            let b = rand.UniformFloat() // * 0.5f + 0.5f
//            C4f(r,g,b,1.0f)
//            //C4b(rand.UniformInt(256), rand.UniformInt(256), rand.UniformInt(256), 255)
//        ) |> ignore
//        
//        let cmp = 
//            let data = data.GetVolume<C4f>()
//            let mutable sum = V4f.Zero
//            data.ForeachIndex(fun i ->
//                let v = data.[i] |> V4f
//                sum <- sum + v / 2097152.0f //(V4f v / V4f(255.0, 255.0, 255.0, 255.0))
//            )
//            sum
//        
//        let img = runtime.PrepareTexture(PixTexture3d(data, TextureParams.mipmapped))
//        let res = par.MapReduce(<@ fun _ (v : V4f) -> v / 2097152.0f @>, <@ (+) @>, img.[TextureAspect.Color, 0, 0])
//        printfn "CPU: %A" cmp
//        printfn "GPU: %A" res

       // System.Environment.Exit 0

        let testArr = [| 100; 5; 10; 20; 1000; 3 |]
        let input = runtime.CreateBuffer<int>(testArr)
        let bits = runtime.CreateBuffer<int>(input.Count)
        let bitsum = runtime.CreateBuffer<int>(input.Count)
        let result = runtime.CreateBuffer<int>(testArr)

        par.CompileMap(<@ fun i e -> if e < 10 then 1 else 0 @>, input, bits).Run()
        let scanned = par.Scan(<@ (+) @>, bits, bitsum)
        let targetWriteShader = runtime.CreateComputeShader Shaders.toTarget
        let targetWrite = runtime.NewInputBinding targetWriteShader
        targetWrite.["src"] <- input
        targetWrite.["n"] <- input.Count
        targetWrite.["bits"] <- bits
        targetWrite.["targets"] <- bitsum
        targetWrite.["result"] <- result
        targetWrite.Flush()
        let ceilDiv (v : int) (d : int) =
            if v % d = 0 then v / d
            else 1 + v / d
        let mk =
             [
                ComputeCommand.Bind(targetWriteShader)
                ComputeCommand.SetInput targetWrite
                ComputeCommand.Dispatch (ceilDiv (int input.Count) 64)
             ]
        let program =
            runtime.CompileCompute mk
        program.Run()

        let bits2 =  bits.Download()
        printfn "%A" bits2

        let scanned2 =  bitsum.Download()
        printfn "%A" scanned2
        let max = scanned2.[scanned2.Length-1]
        let result2 = result.[0..max-1].Download()
        printfn "%A" result2

        let cnt = 1 <<< 20
        let aa = Array.create cnt 1
        let ba = runtime.CreateBuffer<int>(aa)
        let bb = runtime.CreateBuffer<int> cnt

        let rand = Random()
        let mutable i = 0
        while true do
            
            let realCnt = 
                if i < DictConstant.PrimeSizes.Length && DictConstant.PrimeSizes.[i] <= uint32 cnt then
                    int DictConstant.PrimeSizes.[i]
                
                else 
                    abs (rand.Next(cnt)) + 1

            let map = <@ fun i a -> a @>
            let add = <@ (+) @>

            let ba = ba.[0..realCnt-1]
            let bb = bb.[0..realCnt-1]

            let bboida = ba.Download()


            use scan = par.CompileScan(add,ba,bb)

            let reference () =
                let res = Array.zeroCreate ba.Count
                let mutable sum = 0
                for i in 0 .. ba.Count - 1 do
                    sum <- sum + aa.[i]
                    res.[i] <- sum
                res
                    
            scan.Run()
            let arr = bb.Download()
            let ref = reference()
            if arr <> ref then
                Log.warn "cnt:  %A" realCnt
                Log.warn "scan: %A" arr.[arr.Length-1]
                Console.ReadLine() |> ignore
                //Log.warn "fold: %A" r
            else
                Log.line "OK (%A)" realCnt

            i <- i + 1
//        benchmark 100 [
//            
//            //"cpu", reference
//            //"gpu", suma.Run
//            //"gpui", fun () -> par.MapReduce(map, add, ba)
//        ]



//        let scanab = par.CompileScan(<@ (+) @>, ba, bb)
//        scanab.Run()
//
//        let data = bb.Download()
//        let expected = Array.init data.Length (fun i -> 1 + i)
//        if data <> expected then
//            printfn "bad"
//        else
//            printfn "good"
//            printfn "%A" data
        Environment.Exit 0


//        let app = new VulkanVRApplicationLayered(false)
//        let runtime = app.Runtime :> IRuntime
//        let win = app :> IRenderTarget
//        let view = app.Info.viewTrafos
//        let proj = app.Info.projTrafos :> IAdaptiveValue
//        let run () = app.Run()
//        let subscribe (f : unit -> unit) =
//            app.Controllers |> Array.iter (fun c ->
//                c.Axis |> Array.iter (fun a -> 
//                    a.Press.Add (f)
//                )
//            )

        let update = runtime.CreateComputeShader Shaders.updateAcceleration
        let step = runtime.CreateComputeShader Shaders.step

        let rand = RandomSystem()
        let particeCount = 1000
        let positions = runtime.CreateBuffer<V4f>(Array.init particeCount (fun _ -> V4d(rand.UniformV3dDirection() * 3.0, 1.0) |> V4f))
        let velocities = runtime.CreateBuffer<V4f>(Array.zeroCreate particeCount)
        let accelerations = runtime.CreateBuffer<V4f>(Array.zeroCreate particeCount)
        let masses = runtime.CreateBuffer<float32>(Array.init particeCount (fun _ -> 1.0f))

        positions.Upload([| V4f(-1.0f, 0.5f, 0.0f, 1.0f);  V4f(1.0f, -0.5f, 0.0f, 1.0f); |])
        masses.Upload([| 150.0f; 150.0f |])
        velocities.Upload([| V4f(0.2f, 0.0f, 0.0f, 1.0f); V4f(-0.2f, 0.0f, 0.0f, 1.0f) |])


        subscribe (fun () ->
            positions.Upload(Array.init particeCount (fun _ -> V4d(rand.UniformV3dDirection() * 3.0, 1.0) |> V4f))
            velocities.Upload(Array.zeroCreate particeCount)
            positions.Upload([| V4f(-1.0f, 0.5f, 0.0f, 1.0f);  V4f(1.0f, -0.5f, 0.0f, 1.0f); |])
            velocities.Upload([| V4f(0.2f, 0.0f, 0.0f, 1.0f); V4f(-0.2f, 0.0f, 0.0f, 1.0f) |])
        )

        
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

        let groupSize = 
            if particeCount % update.LocalSize.X = 0 then 
                particeCount / update.LocalSize.X
            else
                1 + particeCount / update.LocalSize.X

        let compiled = false

        let commands =
            [
                ComputeCommand.Bind update
                ComputeCommand.SetInput updateInputs
                ComputeCommand.Dispatch groupSize

                ComputeCommand.Bind step
                ComputeCommand.SetInput stepInputs
                ComputeCommand.Dispatch groupSize
            ]


        let program =
            runtime.CompileCompute commands

        let magic =
            let sw = System.Diagnostics.Stopwatch()

            win.Time |> AVal.map (fun _ ->
                let dt = sw.Elapsed.TotalSeconds
                sw.Restart()

                if dt < 0.2 then
                    let maxStep = 0.01
                    let mutable t = 0.0
                    while t < dt do
                        let rdt = min maxStep (dt - t)
                        stepInputs.["dt"] <-rdt
                        stepInputs.Flush()
                        if compiled then
                            program.Run()
                        else
                            runtime.Run commands
                        t <- t + rdt
                
                else
                    printfn "bad: %A" dt

                0.0
                ///positions :> IBuffer

            )



        let u (n : String) (m : IAdaptiveValue) (s : ISg) =
            Sg.UniformApplicator(n, m, s) :> ISg

        let sphere = Primitives.unitSphere 5
        let pos = sphere.IndexedAttributes.[DefaultSemantic.Positions]
        let norm = sphere.IndexedAttributes.[DefaultSemantic.Normals]

        let call = DrawCallInfo(FaceVertexCount = pos.Length, InstanceCount = particeCount)
        
        let instanceBuffer (name : Symbol) (view : BufferView) (s : ISg) =
            Sg.InstanceAttributeApplicator(name, view, s) :> ISg

        win.RenderTask <-
            Sg.render IndexedGeometryMode.TriangleList call
                |> Sg.vertexBuffer DefaultSemantic.Positions (BufferView(AVal.constant (ArrayBuffer pos :> IBuffer), typeof<V3f>))
                |> Sg.vertexBuffer DefaultSemantic.Normals (BufferView(AVal.constant (ArrayBuffer norm :> IBuffer), typeof<V3f>))
                |> instanceBuffer (Symbol.Create "Offset") (BufferView(AVal.constant (positions :> IBuffer), typeof<V4f>))
                |> instanceBuffer (Symbol.Create "Mass") (BufferView(AVal.constant (masses :> IBuffer), typeof<float32>))
                |> Sg.translate 0.0 0.0 1.0
                |> Sg.shader {
                    do! Shaders.instanceOffset
                    do! DefaultSurfaces.trafo
                    //do! DefaultSurfaces.constantColor C4f.Red
                    do! DefaultSurfaces.simpleLighting
                }
                |> u "ViewTrafo" view
                |> u "ProjTrafo" proj
                |> Sg.viewTrafo view //(view |> AVal.map (Array.item 0))
                |> Sg.uniform "Scale" (AVal.constant 0.05)
                |> Sg.uniform "Magic" magic
                |> Sg.compile runtime win.FramebufferSignature

        run()
        positions.Dispose()
        accelerations.Dispose()
        velocities.Dispose()
        updateInputs.Dispose()
        stepInputs.Dispose()
        runtime.DeleteComputeShader update
        runtime.DeleteComputeShader step

