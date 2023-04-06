module StreamingTest

open System
open System.IO
open System.Collections.Generic
open System.Threading
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.SceneGraph.Semantics
open System.Threading.Tasks
open System.Collections.Concurrent
open System.Threading

let test () =
    Aardvark.Init()

    let app = new OpenGlApplication()
    let runtime = app.Runtime

    let images = 
        Directory.EnumerateFiles(@"F:\pro3d\data\StereoMosaic\StereoMosaic_000_000", "*.dds", SearchOption.AllDirectories) 
        |> Seq.atMost 200 
        |> Seq.toArray

    let win = app.CreateSimpleRenderWindow()
    let tex = NullTexture() :> ITexture |> cval
    let nextFrame = new SemaphoreSlim(1)
    let firstFrame = new SemaphoreSlim(0)

    let useContextLock = true
    let prepare = true
    let threads = 1
    let mippedLoader = true
    let pfim = true

    let objects = cmap HashMap.empty
    let keys = LinkedList<string>()

    let mutable framesRendered = 0

    let createObj (t : Trafo3d) (tex : ITexture)=
        let sg = 
            Sg.fullScreenQuad
            |> Sg.trafo' t
            |> Sg.diffuseTexture' tex
            |> Sg.shader  {
                do! DefaultSurfaces.diffuseTexture
            }
        sg

    let changer () =
        let r = new RandomSystem()
        firstFrame.Wait()

        let sw = System.Diagnostics.Stopwatch.StartNew()
        framesRendered <- 0
        let images = images |> Seq.mapi (fun i e -> i,e) |> Seq.toArray
        let sourceImages = new BlockingCollection<_>()
        for i in images do sourceImages.Add i
        sourceImages.CompleteAdding()

        let runThread (workerId : int) () =
            use __ = 
                if useContextLock then 
                    runtime.Context.RenderingLock(runtime.Context.CreateContext()) :> IDisposable
                else { new IDisposable with member x.Dispose() = () }

            for (i : int, img : string) in sourceImages.GetConsumingEnumerable() do 
                let newTexture =
                    match pfim with
                    | true -> 
                        if mippedLoader then
                            PixTexture2d(PixImagePfim.LoadWithMipmap(img))
                        else
                            PixTexture2d(PixImagePfim.Load(img), true)
                    | _ -> 
                        let pi = PixImageDevil.Loader.LoadFromFile(img)
                        PixTexture2d(PixImageMipMap ([|pi|]), TextureParams.mipmapped)

                let newKey = Guid.NewGuid() |> string
                let preparedTexture, dispose = 
                    if prepare then
                        let preparedTexture = runtime.PrepareTexture(newTexture)
                        let dispose () = runtime.DeleteTexture(preparedTexture)
                        preparedTexture :> ITexture, dispose
                    else
                        newTexture, ignore

                let t = Trafo3d.Scale (r.UniformDouble() + 0.2) * Trafo3d.Translation(r.UniformV3d() * 0.2)
                let obj = createObj t preparedTexture

                let remove =
                    lock keys (fun _ -> 
                        // remote last
                        let remove =
                            if keys.Count > 30 && keys.Last <> null then
                                let (_, dispose) = objects[keys.Last.Value]
                                dispose()
                                let last = keys.Last.Value
                                keys.RemoveLast()
                                Some last
                            else
                                None

                        // add to front
                        keys.AddFirst(newKey) |> ignore
                        remove
                    )

                transact (fun _ -> 
                    objects.Add(newKey, (obj, dispose)) |> ignore
                    match remove with
                    | None -> ()
                    | Some r -> 
                        objects.Remove r |> ignore
                )

                Log.line "[thread %d] image:  %d" workerId i
            
        let threads =
            [
                for i in 0 .. threads - 1 do
                    let t = Thread(ThreadStart (runThread i))
                    t.Start()
                    t
            ]

        for t in threads do t.Join()
        let fps = float framesRendered / float sw.Elapsed.TotalSeconds 
        Log.line "%d frames took %A (images/s: %A), fps: %f" images.Length (sw.Elapsed |> MicroTime) (float images.Length / float sw.Elapsed.TotalSeconds) fps
    
    let t = Thread(ThreadStart changer)
    t.Start()

    let sg = 
        Sg.fullScreenQuad
        |> Sg.diffuseTexture tex
        |> Sg.shader  {
            do! DefaultSurfaces.diffuseTexture
        }

    // Given eye, target and sky vector we compute our initial camera pose
    let initialView = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
    // the class Frustum describes camera frusta, which can be used to compute a projection matrix.
    let frustum = 
        // the frustum needs to depend on the window size (in oder to get proper aspect ratio)
        win.Sizes 
            // construct a standard perspective frustum (60 degrees horizontal field of view,
            // near plane 0.1, far plane 50.0 and aspect ratio x/y.
            |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

    // create a controlled camera using the window mouse and keyboard input devices
    // the window also provides a so called time mod, which serves as tick signal to create
    // animations - seealso: https://github.com/aardvark-platform/aardvark.docs/wiki/animation
    let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    let normalScene = 
        let sw = System.Diagnostics.Stopwatch.StartNew()
        Sg.box' C4b.White Box3d.Unit
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.simpleLighting
        }
        |> Sg.trafo (win.Time |> AVal.map (fun _ -> Interlocked.Increment(&framesRendered);  Trafo3d.RotationZ(sw.Elapsed.TotalSeconds)))
        |> Sg.viewTrafo (cameraView |> AVal.map CameraView.viewTrafo)
        |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo)
        |> Sg.pass (RenderPass.after "" RenderPassOrder.Arbitrary RenderPass.main)

    let scene = 
        Sg.set (objects |> AMap.toASetValues |> ASet.map fst)
        |> Sg.depthWrite' false
        |> Sg.andAlso normalScene

            
    win.RenderTask <- 
        RenderTask.ofList [
            app.Runtime.CompileRender(win.FramebufferSignature, scene)
            RenderTask.custom (fun _ -> 
                firstFrame.Release() |> ignore
                //nextFrame.Release() |> ignore
            )
        ]

    win.Run()
    System.Environment.Exit 0
