open System
open FShade
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering
open Aardvark.Rendering.NanoVg
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open System.Threading.Tasks
open System.Threading
open Aardvark.Base.Rendering

let random = Random()

let randomV3f() =
    V3f(random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0)

let newPoints (count : int) =
    let positions = Array.init count (fun _ -> randomV3f())
    let colors = Array.create count C4b.Red
    let ig = 
        IndexedGeometry(
            Mode = IndexedGeometryMode.PointList, 
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, positions :> Array
                    DefaultSemantic.Colors, colors :> Array
                ]
        )

    ig |> Sg.ofIndexedGeometry


let running = new ManualResetEventSlim()

[<EntryPoint>]
let main argv = 
    use app = new OpenGlApplication()
    use win = app.CreateSimpleRenderWindow()

    Aardvark.Init()


    let objectCount = 1000000
    let parallelLoads = 20
    let objectSize = 100
    let minLoadTimeinMS = 1
    let maxLoadTimeInMS = 1

    let leafs = CSet.ofList [newPoints 100]


    let start (f : unit -> unit) =
        let t = new Thread(ThreadStart(f))
        t.Start()
        t

    let fac = TaskFactory(TaskCreationOptions.LongRunning,TaskContinuationOptions.AttachedToParent)

    let startTask (f : unit -> unit) =
        Task.Factory.StartNew(f, TaskCreationOptions.LongRunning)

    let pfor (s : seq<'a>) (f : 'a -> unit) =
        s |> Seq.toList
          |> List.map (fun v -> startTask (fun () -> f v))
          |> List.iter (fun t -> t.Wait())


    let inputCounter = ref 0
    let start() =
        Task.Factory.StartNew(fun () ->
            for _ in 0..(objectCount / parallelLoads) do
                running.Wait()
                pfor [1..parallelLoads] (fun _ ->
                    let points = newPoints objectSize

                    // simulate something long-running
                    Thread.Sleep(random.Next(minLoadTimeinMS, maxLoadTimeInMS))


                    //Log.startTimed "adding"
                    // submit the new point-set
                    transact (fun () ->
                        leafs.Add points |> ignore
                    )
                    Interlocked.Increment(&inputCounter.contents) |> ignore
                    Log.line "added: %A" !inputCounter
                    //Log.stop()
                )
        ) |> ignore

    start()

    let initialView = CameraView.LookAt(V3d(3,3,3), V3d.Zero)
    let view = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView
    let perspective = win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 10.0 (float s.X / float s.Y))

    let sg =
        leafs 
            |> Sg.set
            |> Sg.loadAsync win.FramebufferSignature
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.vertexColor |> toEffect]
            |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo)

    let task = app.Runtime.CompileRender(win.FramebufferSignature, BackendConfiguration.UnmanagedOptimized, sg)

    win.Keyboard.Down.Values.Subscribe (fun k ->
        if k = Keys.Enter then
            if running.IsSet then 
                printfn "stopping"
                running.Reset()
            else 
                printfn "starting"
                running.Set()

    ) |> ignore

    win.RenderTask <- task |> DefaultOverlays.withStatistics
    win.Run()
    0 
