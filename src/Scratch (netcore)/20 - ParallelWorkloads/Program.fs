open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open System.IO
open System.Threading
open System.Diagnostics
open System.Collections.Concurrent
open System.Threading.Tasks
open System.Threading

open System
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim

open Pipe

type Method = Naive | Pipes of int

[<EntryPoint>]
let main argv = 
    

    Aardvark.Init()

    use app = new OpenGlApplication()
    let win = app.CreateGameWindow(samples = 8)


    let initialView = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
    // the class Frustum describes camera frusta, which can be used to compute a projection matrix.
    let frustum = 
        // the frustum needs to depend on the window size (in oder to get proper aspect ratio)
        win.Sizes 
            // construct a standard perspective frustum (60 degrees horizontal field of view,
            // near plane 0.1, far plane 50.0 and aspect ratio x/y.
            |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

    let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    let sw = System.Diagnostics.Stopwatch.StartNew()

    let sg =
        Sg.box' C4b.White Box3d.Unit 
            // here we use fshade to construct a shader: https://github.com/aardvark-platform/aardvark.docs/wiki/FShadeOverview
            |> Sg.effect [
                    DefaultSurfaces.trafo                 |> toEffect
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting        |> toEffect
                ]
            |> Sg.trafo (win.Time |> AVal.map (fun time -> Trafo3d.RotationZInDegrees sw.Elapsed.TotalSeconds))
            // extract our viewTrafo from the dynamic cameraView and attach it to the scene graphs viewTrafo 
            |> Sg.viewTrafo (cameraView  |> AVal.map CameraView.viewTrafo )
            // compute a projection trafo, given the frustum contained in frustum
            |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo    )


    let renderTask = 
        // compile the scene graph into a render task
        app.Runtime.CompileRender(win.FramebufferSignature, sg)

    let runtime = app.Runtime |> unbox<Aardvark.Rendering.GL.Runtime>

    let method = Pipes 1
    //let loader = PixImageDevil.Loader
    //let loader = PixImageFreeImage.Loader
    let loader = PixImageSharp.Loader

    let cts = new CancellationTokenSource()

    let images = 
        //Directory.EnumerateFiles(@"F:\pro3d\data\20200220_DinosaurQuarry2\Dinosaur_Quarry_2\OPC_000_000\images\Ortho", "*.tif")  
        //Directory.EnumerateFiles(@"I:\OPC\test", "*.tif")  
        Directory.EnumerateFiles(@"I:\OPC\StereoMosaic\StereoMosaic_000_000\Images\Texture0", "*.tif")
        |> Seq.toArray

    let t =
        match method with
        | Naive -> 
        
            let bc = new BlockingCollection<(unit->unit)>()

            let run () =
                let ctx = runtime.Context.CreateContext()
                fun () ->
                    try
                        use __ = (runtime : IRuntime).ContextLock
                        for e in bc.GetConsumingEnumerable(cts.Token) do
                            e()
                    with e -> 
                        Log.warn "%A" e

            let enqueue (f : unit -> 'a) =
                let tcs = new TaskCompletionSource<_>()
                let f () =
                    let r = f()
                    tcs.SetResult r
                bc.Add f
                tcs.Task

            let loadImage (fileName : string) () = 
                let pi = PixImage.Load(fileName, loader)
                let ti =  PixTexture2d(PixImageMipMap [| pi |], true) :> ITexture
                let t = runtime.PrepareTexture(ti)
                runtime.DeleteTexture(t)


            let threads = [0..4] |> List.map (fun _ -> let run = run() in Thread(ThreadStart run))
            for t in threads do t.Start()

            let background = 
                async {
                    while not cts.Token.IsCancellationRequested do  
                        let sw = Stopwatch.StartNew()

                        let! results = 
                            images
                            |> Seq.map (fun file -> 
                                enqueue (loadImage file)
                            )
                            |> Task.WhenAll
                            |> Async.AwaitTask

                        sw.Stop()
                        Log.line "took: %A -> %.1f images per second" (sw.MicroTime / images.Length) (1000.0 / (sw.Elapsed.TotalMilliseconds / float images.Length)) 
                    
                } |> Async.StartAsTask

            background :> Task

        | Pipes variant -> 
    
            let loadAndPrepareTexture (fileName : string) : Pipe<_, unit> = 
                if variant = 0 then
                    pipe {
                        let! loadFromDisk = Pipe.loadTexture loader fileName
                        let! nativeImage = Pipe.prepareForUpload loadFromDisk
                        let! uploaded = Pipe.uploadTexture win.Runtime nativeImage
                        do win.Runtime.DeleteTexture(uploaded)
                        return ()
                    }
                else    
                    pipe {
                        let! fromDisk = Pipe.readAllBytes fileName
                        let! loadFromDisk = Pipe.imageFromBytes loader fromDisk
                        let! nativeImage = Pipe.prepareForUpload loadFromDisk
                        let! uploaded = Pipe.uploadTexture win.Runtime nativeImage
                        do win.Runtime.DeleteTexture(uploaded)
                        return ()
                    }

            let maximizeThroughput, tasks = Workloads.throughput win.Runtime cts.Token

            let background = 
                async {
                    while true do
                        let sw = Stopwatch.StartNew()

                        let results = 
                            images
                            |> Seq.map (fun file -> loadAndPrepareTexture file)
                            |> Pipe.par maximizeThroughput 
 

                        sw.Stop()
                        Log.line "took: %A -> %.1f images per second" (sw.MicroTime / images.Length) (1000.0 / (sw.Elapsed.TotalMilliseconds / float images.Length)) 

                } |> Async.StartAsTask

            tasks


    win.RenderTask <- renderTask
    win.Run()

    cts.Cancel()
    t.Wait()

    0
