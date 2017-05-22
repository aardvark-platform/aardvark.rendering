module Program

open System
open System.IO
open Rendering.Examples

open System.Runtime.InteropServices
open System.Diagnostics
open System.Threading
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph


[<EntryPoint>]
[<STAThread>]
let main args =
//
//    let input = cset<int>()
//
//    let busySleep (ms : float) =
//        let sw = System.Diagnostics.Stopwatch.StartNew()
//        while sw.Elapsed.TotalMilliseconds < ms do
//            ()
//
//    let result = 
//        input |> Loader.load {
//            load                = fun ct v -> Log.line "load %A" v; busySleep 5.0; float v
//            unload              = fun r -> Log.line "unload %A" r
//            priority            = fun v -> v
//            numThreads          = Environment.ProcessorCount
//            submitDelay         = TimeSpan.FromMilliseconds 100.0
//            progressInterval    = TimeSpan.MaxValue
//            progress            = fun p -> Log.line "progress: %A" p
//        }
//
//    let s = result |> ASet.unsafeRegisterCallbackKeepDisposable (fun d -> Log.line "got: %A" d) 
//
//    let rand = RandomSystem()
//    for i in 1 .. 100 do
//        Thread.Sleep(100)
//        transact (fun () ->
//            input.SymmetricExceptWith(List.init 10 (fun _ -> rand.UniformInt(100)))
//        )
//
//    Log.line "submit done"
//    Console.ReadLine() |> ignore
//
//    let output = result.Content |> Mod.force |> Seq.toList |> List.map int |> HSet.ofList
//    let input = input |> HSet.ofSeq
//    Log.startTimed "disposing"
//    s.Dispose()
//    Log.stop()
//
//    Log.line "difference: %A" (HSet.computeDelta input output)
//
//    Console.ReadLine() |> ignore
//    Environment.Exit 0




//    let cam = Mod.init V3d.Zero
//    let priority (op : SetOperation<V3d>) =
//        
//        match op with 
//            | Add(_,v) -> V3d.Distance(cam.Value, v) / 100.0
//            | Rem(_,v) -> V3d.Distance(cam.Value, v)
//
//    let queue = ConcurrentDeltaPriorityQueue<V3d, float>(fun s -> priority s)
//
//
//    queue.Enqueue(Add(V3d.III))
//    queue.Enqueue(Add(V3d(2,3,4)))
//    queue.Enqueue(Add(V3d(4,3,4)))
//    queue.Enqueue(Rem(V3d(0,1,2)))
//
//    while queue.Count > 0 do
//        let e = queue.Dequeue()
//        printfn "%A" e
//        
//
//    let rand = RandomSystem()
//
//    let log = @"C:\Users\Schorsch\Desktop\updateHeap.csv"
//    for s in 1000 .. 1000 .. 50000 do
//        printf "%d: " s
//        let queue = ConcurrentDeltaPriorityQueue<V3d, float>(fun s -> priority s)
//        for i in 1 .. s do
//            queue.Enqueue(Add(rand.UniformV3d()))
//    
//        let sw = System.Diagnostics.Stopwatch()
//        sw.Start()
//        for i in 1 .. 100 do
//            transact (fun () -> cam.Value <- rand.UniformV3d())
//            let hist = queue.UpdatePriorities()
//            ()
//        sw.Stop()
//
//        File.AppendAllLines(log, [sprintf "%d;%f" s (sw.Elapsed.TotalMilliseconds / 100.0)])
//        printfn "%A" (sw.MicroTime / 100.0)
//
////
////    while queue.Count > 0 do
////        let e = queue.Dequeue()
////        printfn "%A" e
////
//
//    Environment.Exit 0


    //TimerTest.mmTimer()
    //
    //TimerTest.timerPrecision()
    //TimerTest.minAsyncSleep()
    //TimerTest.minSleep()
    //Environment.Exit 0
    //Examples.Tutorial.run()
    //Examples.Instancing.run()
    //Examples.Render2TexturePrimitive.run()
    //Examples.Render2TextureComposable.run()
    //Examples.Render2TexturePrimiviteChangeableSize.run()
    //Examples.Render2TexturePrimitiveFloat.run()
    //Examples.ComputeTest.run()
    Ag.initialize()
    Aardvark.Init()
    Examples.LoD.run()
    //Examples.Shadows.run()
    //Examples.AssimpInterop.run() 
    //Examples.ShaderSignatureTest.run()
    //Examples.Polygons.run()           attention: this one is currently broken due to package refactoring
    //Examples.TicTacToe.run()          attention: this one is currently broken due to package refactoring
    0
