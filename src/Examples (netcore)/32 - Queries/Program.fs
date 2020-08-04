(*

This example demonstrates how to use GPU queries to collect various statistics including:
 * time spent on the GPU
 * number of samples that passed fragment tests
 * other pipeline statistics like number of vertex shader invocations

Currently, there is only a low-level API that requires the user to pass queries to Run() calls of render tasks and compute programs.

*)

open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Rendering.Text
open System.Threading.Tasks


module Shader =
    open FShade
    open Aardvark.Base.Rendering.Effects

    let private tipColor =
        C4b.VRVisGreen.ToC4d().ToV4d()

    let private tessColor =
        C4b(255, 102, 102).ToC4d().ToV4d()

    let private diffuseSampler =
        sampler2d {
            texture uniform?DiffuseColorTexture
            filter Filter.MinMagMipLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let color (v : Vertex) =
        fragment {
            let texColor = diffuseSampler.Sample(v.tc)
            return texColor * v.c
        }

    [<ReflectedDefinition>]
    let private computeNormal (p0 : V4d) (p1 : V4d) (p2 : V4d) =
        Vec.cross (p1.XYZ - p0.XYZ) (p2.XYZ - p0.XYZ) |> Vec.normalize

    let extrude (v : Triangle<Vertex>) =
        triangle {
            let p0Ext = v.P0.wp.XYZ
            let p1Ext = v.P1.wp.XYZ
            let p2Ext = v.P2.wp.XYZ

            let cExt = (p0Ext + p1Ext + p2Ext) / 3.0

            let nExt = Vec.cross (p1Ext - p0Ext) (p2Ext - p1Ext)
            let lnExt = Vec.length nExt
            let areaExt = 0.5 * lnExt
            let nExt = nExt / lnExt

            let phExt = cExt + nExt * areaExt

            let w0Ext = V4d(p0Ext, 1.0)
            let w1Ext = V4d(p1Ext, 1.0)
            let w2Ext = V4d(p2Ext, 1.0)
            let whExt = V4d(phExt, 1.0)

            yield { v.P0 with wp = w0Ext; pos = uniform.ViewProjTrafo * w0Ext; n = nExt }
            yield { v.P1 with wp = w1Ext; pos = uniform.ViewProjTrafo * w1Ext; n = nExt }
            yield { v.P2 with wp = w2Ext; pos = uniform.ViewProjTrafo * w2Ext; n = nExt }
            restartStrip()

            let nExt = computeNormal w0Ext w1Ext whExt
            yield { v.P0 with wp = w0Ext; pos = uniform.ViewProjTrafo * w0Ext; n = nExt }
            yield { v.P1 with wp = w1Ext; pos = uniform.ViewProjTrafo * w1Ext; n = nExt }
            yield { v.P2 with wp = whExt; pos = uniform.ViewProjTrafo * whExt; n = nExt; c = tipColor }
            restartStrip()

            let nExt = computeNormal w1Ext w2Ext whExt
            yield { v.P0 with wp = w1Ext; pos = uniform.ViewProjTrafo * w1Ext; n = nExt }
            yield { v.P1 with wp = w2Ext; pos = uniform.ViewProjTrafo * w2Ext; n = nExt }
            yield { v.P2 with wp = whExt; pos = uniform.ViewProjTrafo * whExt; n = nExt; c = tipColor }
            restartStrip()

            let nExt = computeNormal w2Ext w0Ext whExt
            yield { v.P0 with wp = w2Ext; pos = uniform.ViewProjTrafo * w2Ext; n = nExt }
            yield { v.P1 with wp = w0Ext; pos = uniform.ViewProjTrafo * w0Ext; n = nExt }
            yield { v.P2 with wp = whExt; pos = uniform.ViewProjTrafo * whExt; n = nExt; c = tipColor }
            restartStrip()
        }

    [<ReflectedDefinition>]
    let private lerpV2d (v0 : V2d) (v1 : V2d) (v2 : V2d) (coord : V3d) =
        v0 * coord.X + v1 * coord.Y + v2 * coord.Z

    [<ReflectedDefinition>]
    let private lerpV3d (v0 : V3d) (v1 : V3d) (v2 : V3d) (coord : V3d) =
        v0 * coord.X + v1 * coord.Y + v2 * coord.Z

    [<ReflectedDefinition>]
    let private lerpV4d (v0 : V4d) (v1 : V4d) (v2 : V4d) (coord : V3d) =
        v0 * coord.X + v1 * coord.Y + v2 * coord.Z

    let tessellate(triangle : Patch<3 N, Vertex>) =
        tessellation  {
            let! coord = tessellateTriangle 1.0 (2.0, 2.0, 2.0)

            // Interpolate Vertex according to the barycentric coordinates from the tessellator
            let pos = lerpV3d triangle.[0].pos.XYZ triangle.[1].pos.XYZ triangle.[2].pos.XYZ coord
            let c   = lerpV4d triangle.[0].c triangle.[1].c triangle.[2].c coord
            let n   = lerpV3d triangle.[0].n triangle.[1].n triangle.[2].n coord |> Vec.normalize
            let tc  = lerpV2d triangle.[0].tc triangle.[1].tc triangle.[2].tc coord

            let amount = coord.X |> min coord.Y |> min coord.Z
            let c = (2.0 * amount) |> lerp c tessColor

            // Transform vertex to world space
            let n   = uniform.NormalMatrix * n
            let wp  = uniform.ModelTrafo * V4d(pos, 1.0)

            // Transform the vertex to screen space
            let pos = uniform.ViewProjTrafo * wp

            return
                { triangle.[0] with
                    wp  = wp
                    pos = pos
                    c   = c
                    n   = n
                    tc  = tc
                }
       }

    [<LocalSize(X = 32)>]
    let compute() =
        compute {
            ()
        }


let statisticName =
    LookupTable.lookupTable [
        InputAssemblyVertices, "Input vertices"
        InputAssemblyPrimitives, "Input primitives"
        VertexShaderInvocations, "Vertex shader invocations"
        GeometryShaderInvocations, "Geometry shader invocations"
        GeometryShaderPrimitives, "Geometry shader primitives"
        ClippingInputPrimitives, "Clipping input primitives"
        ClippingOutputPrimitives, "Clipping output primitives"
        FragmentShaderInvocations, "Fragment shader invocations"
        TesselationControlShaderPatches, "Tesselation control patches"
        TesselationEvaluationShaderInvocations, "Tesselation evaluations"
        ComputeShaderInvocations, "Compute shader invocations"
    ]

let boxSg =
    // lets define the bounds/color for our box
    // NOTE that the color is going to be ignored since we're using a texture
    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b(102, 204, 255)

    // thankfully aardvark defines a primitive box
    Sg.box (AVal.constant color) (AVal.constant box)

        // apply the texture as "DiffuseTexture"
        |> Sg.diffuseTexture DefaultTextures.checkerboard

        // apply a shader ...
        // * transforming all vertices
        // * looking up the DiffuseTexture
        // * applying a simple lighting to the geometry (headlight)
        |> Sg.shader {
            do! Shader.tessellate
            do! Shader.extrude
            do! Shader.color
            do! DefaultSurfaces.simpleLighting
        }

let sceneSg (win : IRenderWindow) =

    let initialView =
        CameraView.LookAt(V3d(10.0,10.0,10.0), V3d.Zero, V3d.OOI)

    let frustum =
        win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 150.0 (float s.X / float s.Y))

    let camera =
        DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    let viewTrafo = camera |> AVal.map CameraView.viewTrafo
    let projTrafo = frustum |> AVal.map Frustum.projTrafo

    boxSg
    |> Sg.viewTrafo viewTrafo
    |> Sg.projTrafo projTrafo

let finalSg (output : aval<ITexture>) =
    Sg.fullScreenQuad
    |> Sg.diffuseTexture output
    |> Sg.depthTest ~~DepthTestMode.None
    |> Sg.shader {
        do! DefaultSurfaces.diffuseTexture
    }

let overlaySg (win : IRenderWindow) (time : aval<MicroTime>) (samples : aval<uint64>) (stats : aval<Map<PipelineStatistics, uint64>>) =

    let str =
        AVal.custom (fun t ->
            let time = sprintf "Time: %A" <| time.GetValue t
            let samples = sprintf "Samples: %d" <| samples.GetValue t

            let stats =
               stats.GetValue t
               |> Map.toSeq
               |> Seq.map (fun (s, v) ->
                    let name = statisticName s
                    sprintf "%s: %d" name v
               )
               |> String.concat "\n"

            String.concat "\n" [time; samples; stats]
        )

    let trafo =
        win.Sizes |> AVal.map (fun s ->
            let border = V2d(30.0, 20.0) / V2d s
            let pixels = 40.0 / float s.Y
            Trafo3d.Scale(pixels) *
            Trafo3d.Scale(float s.Y / float s.X, 1.0, 1.0) *
            Trafo3d.Translation(-1.0 + border.X, 1.0 - border.Y - pixels, -1.0)
        )

    Sg.text (Font("Consolas")) C4b.White str
    |> Sg.trafo trafo


[<EntryPoint>]
let main argv =
    // first we need to initialize Aardvark's core components
    Aardvark.Init()

    // uncomment/comment to switch between the backends
    use app = new VulkanApplication(debug = true)
    //use app = new OpenGlApplication()
    let runtime = app.Runtime :> IRuntime

    // create a game window
    use win = app.CreateGameWindow(samples = 8)
    win.RenderAsFastAsPossible <- true

    // create scene render task
    use sceneTask =
        let scene = sceneSg win
        let clear = runtime.CompileClear(win.FramebufferSignature, ~~C4f.Black, ~~1.0)
        let render = runtime.CompileRender(win.FramebufferSignature, scene)
        RenderTask.ofList [clear; render]

    // create final task
    use renderSync =
        runtime.CreateSync(maxDeviceWaits = 2)

    let outputFramebuffer =
        runtime.CreateFramebuffer(win.FramebufferSignature, ~~V2i(256, 256))

    let outputTexture =
        let clear = runtime.CompileClear(win.FramebufferSignature, ~~C4f.Black, ~~1.0)
        let task = new SequentialRenderTask([|clear; sceneTask|])
        let res = task.RenderTo(outputFramebuffer, TaskSync.signal renderSync, Queries.empty, dispose = true)
        res.GetOutputTexture DefaultSemantic.Colors

    use compositeTask =
        let clear = runtime.CompileClear(win.FramebufferSignature, ~~C4f.Blue, ~~1.0)
        let render = runtime.CompileRender(win.FramebufferSignature, finalSg outputTexture)
        RenderTask.ofList [clear; render]

    // create a dummy compute program
    let computeShader = runtime.CreateComputeShader Shader.compute

    use computeProgram =
        runtime.Compile [
            ComputeCommand.Bind computeShader
            ComputeCommand.Dispatch 1
        ]

    // occlusion queries return how many samples passed the fragement tests.
    // an occlusion query can be precise (default) or approximate.
    // in the latter case, the result may be used to determine if anything was rendered.
    // this might be more effient than using a precise occlusion query.
    use occlusionQuery =
        runtime.CreateOcclusionQuery(precise = true)

    // pipeline queries can be used to retrieve a range of statistics about the rendering pipeline (e.g. the number of primitives that passed the clipping stage).
    // the user has to enable the statistics that they are interested in when the query is created.
    // passing none to the creation method will enable all supported statistics (i.e. runtime.SupportedPipelineStatistics).
    // enabling unsupported statistics will fail silently.
    use pipelineQuery =
        runtime.CreatePipelineQuery()
        //runtime.CreatePipelineQuery(ClippingOutputPrimitives)
        //runtime.CreatePipelineQuery [ClippingInputPrimitives; ClippingOutputPrimitives]

    // time queries are used to measure the time spent by the GPU for rendering.
    use timeQuery =
        runtime.CreateTimeQuery()

    use compositeSync =
        runtime.CreateSync()

    use downloadSync =
        runtime.CreateSync()

    let saveLock = obj()

    // queries are used by passing them to Run() of a render task or compute program.
    // here we use RenderTask.custom to run the scene task manually.
    use task =
        RenderTask.custom (fun (t, rt, o, s, q) ->
            // Run() takes a single IQuery as parameter.
            // in order to pass multiple queries we have to build a Queries struct.
            // here we also include the query that is passed from outside the custom render task (e.g. the window
            // system passes a time query to compute the GPU usage and shows it in the title bar)
            let queries =
                Queries.single q
                |> Queries.add timeQuery
                |> Queries.add pipelineQuery
                |> Queries.add occlusionQuery

            // IQuery.Begin and End are used to denote the scope in which the queries
            // are used. If we are only interested in the statistics of a single render task, these calls can be omitted.
            queries.Begin()

            compositeTask.Run(t, rt, o, TaskSync.create [renderSync] compositeSync, queries)
            computeProgram.Run(TaskSync.none, queries)

            //let output = outputFramebuffer.GetOutputTexture DefaultSemantic.Colors
            //let t = output.GetValue t
            //let _ = runtime.Download(t :?> IBackendTexture)

            queries.End()
        )

    // we save the query results in adaptive values and print them
    // to the screen using a seperate render task for the overlay.
    let time = AVal.init MicroTime.zero
    let samples = AVal.init 0UL
    let stats =
        let init = PipelineStatistics.All |> Seq.map (fun s -> s, 0UL) |> Map.ofSeq
        AVal.init init

    use overlayTask =
        overlaySg win time samples stats
        |> Sg.compile runtime win.FramebufferSignature

    // we retrieve the query results and update the overlay in two separate threads.
    // getting the results of a query is thread-safe.
    // obviously it is also possible to retrieve the results in the same thread (e.g. after Run()).
    let mutable running = true

    let textureDownloader =
        async {
            while running do
                printfn "Starting download..."

                let output = outputFramebuffer.GetOutputTexture DefaultSemantic.Colors

                match output.GetValue() with
                | :? IBackendTexture as t ->
                    let pix = runtime.Download(t, TaskSync.create [renderSync] downloadSync)
                    lock saveLock (fun _ -> pix.SaveAsImage("fbo.png"))
                | _ -> failwith "Nope"

                printfn "Download finished"
        }

    let textureDownloader2 =
        async {
            printfn "Starting download..."

            let output = outputFramebuffer.GetOutputTexture DefaultSemantic.Colors

            match output.GetValue() with
            | :? IBackendTexture as t ->
                let pix = runtime.Download(t, TaskSync.wait [downloadSync])
                lock saveLock (fun _ -> pix.SaveAsImage("fbo2.png"))
            | _ -> failwith "Nope"

            printfn "Download finished"
        }

    let statisticUpdater =
        async {
            while running do
                if renderSync.Wait(MicroTime.ofMilliseconds 300) then

                    if Option.isNone <| occlusionQuery.TryGetResult(reset = true) then
                        printfn "No result! %A" System.DateTime.Now.Second

                    renderSync.Reset()
                //transact (fun _ ->
                //    // results can be queried in one of two ways: TryGetResult and GetResult.
                //    // the former fails if the results are not ready yet (returning None), while the latter
                //    // blocks until the results are ready. note, that even on the same thread after calling Run() the results are not
                //    // guaranteed to be ready, since the GPU executes work asynchronously.
                //    let value = occlusionQuery.GetResult()
                //    samples.Value <- value

                //    // when getting results from pipeline queries you may get results
                //    // for a subset of the enabled statistics. it returns a map that assigns each
                //    // retrieved PipelineStatistics value its result as a uint64 integer. if the overload
                //    // for a single statistic is used, it returns a single uint64.
                //    // retrieving a statistic that is not supported or not enabled will return 0.
                //    let value = pipelineQuery.GetResult()
                //    //... is equivalent to "pipelineQuery.GetResult(pipelineQuery.Statistics)"
                //    stats.Value <- value
                //)

                //do! Async.Sleep 200
        }

    let timeUpdater =
        async {
            while running do
                transact (fun _ ->
                    // setting reset to true for either of those functions, will reset the query and
                    // ensure that a result is not reported twice. try uncommenting the "printfn" below and moving the window (suspending rendering) and observe that
                    // the time statistic is not reported in the console anymore. setting reset = false will report the last result over and over again.
                    // a query can also be reset manually by calling IQuery.Reset(). but, this means another thread
                    // may potentially get the same result if it executes (Try)GetResult before this thread executes Reset().
                    timeQuery.TryGetResult(reset = true)
                    |> Option.iter (fun t ->
                        time.Value <- t
                        //printfn "setting time = %A" t
                    )
                )

                do! Async.Sleep 500
        }

    let asyncTasks =
        [| textureDownloader |]
        |> Array.map (fun t -> t |> Async.StartAsTask :> Task)

    // run the window
    win.RenderTask <- RenderTask.ofList [task; overlayTask]
    win.Run()

    // wait for the update threads to finish.
    running <- false
    Task.WaitAll(asyncTasks)

    runtime.DeleteComputeShader computeShader

    0
