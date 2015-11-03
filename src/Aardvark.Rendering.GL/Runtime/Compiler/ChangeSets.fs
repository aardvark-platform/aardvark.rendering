namespace Aardvark.Rendering.GL.Compiler

open System
open System.Collections.Generic

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL

type ChangeSet(renderTask : IRenderTask, addInput : IAdaptiveObject -> unit, removeInput : IAdaptiveObject -> unit) =
    static let InstructionUpdateProbe = Symbol.Create "[Instruction] update"
    let l = obj()
    let all = HashSet<IMod<FrameStatistics>>()
    let set = HashSet<IMod<FrameStatistics>>()
    let callbacks = Dictionary<IMod<FrameStatistics>, IDisposable>()
    let sw = System.Diagnostics.Stopwatch()

    let dirty (m : IMod<FrameStatistics>) () =
        lock l (fun () -> 
            set.Add m |> ignore
        )

    member x.Listen(m : IMod<FrameStatistics>) =
        if not m.IsConstant then
            lock m (fun () -> 
                lock l (fun () ->
                    if all.Add m then 
                        addInput m
                        callbacks.[m] <- m.AddMarkingCallback (dirty m)

                    if m.OutOfDate then
                        set.Add m |> ignore
                    else
                        m.Outputs.Add renderTask |> ignore
                )
            )
        else
            m.GetValue(null) |> ignore

    member x.Unlisten (m : IMod<FrameStatistics>) =
        if not m.IsConstant then
            lock m (fun () ->
                lock l (fun () ->
                    if all.Remove m then 
                        removeInput m

                        set.Remove m |> ignore
                        match callbacks.TryGetValue m with
                            | (true, d) -> 
                                d.Dispose()
                                callbacks.Remove m |> ignore
                            | _ -> ()
                )
            )

    member x.Update() =
        Telemetry.timed InstructionUpdateProbe (fun () ->
            let dirtySet = 
                lock l (fun () ->
                    let dirtySet = set |> Seq.toArray
                    set.Clear()
                    dirtySet
                )

            sw.Restart()
            let mutable count = 0
            let mutable resTime = FrameStatistics.Zero
            for d in dirtySet do
                lock d (fun () ->
                    resTime <- resTime + ( d.GetValue renderTask )
                    count <- count + 1
                )
            sw.Stop()
            count, sw.Elapsed, resTime
        )

type ResourceSet(renderTask : IRenderTask, addInput : IAdaptiveObject -> unit, removeInput : IAdaptiveObject -> unit) =
    let l = obj()
    let all = ReferenceCountingSet<IChangeableResource>()
//    let set = HashSet<IChangeableResource>()
//    let callbacks = Dictionary<IChangeableResource, IDisposable>()
//    let sw = System.Diagnostics.Stopwatch()
//
//    let dirty (m : IChangeableResource) () =
//        lock l (fun () -> 
//            set.Add m |> ignore
//        )

    member x.Resources = all

    member x.Listen(m : IChangeableResource) = 
        lock m (fun () -> 
            lock l (fun () ->
                if all.Add m then
                    
                    addInput m
                    //let cb = dirty m
                    //callbacks.[m] <- m.AddMarkingCallback cb

                    if m.OutOfDate then
                        () //set.Add m |> ignore
                    else
                        m.Outputs.Add renderTask |> ignore
            )
        )

    member x.Unlisten (m : IChangeableResource) = 
        lock m (fun () ->
            lock l (fun () ->
                if all.Remove m then
                    removeInput m
                    //set.Remove m |> ignore
//                    match callbacks.TryGetValue m with
//                        | (true, d) -> 
//                            d.Dispose()
//                            callbacks.Remove m |> ignore
//                        | _ -> ()
            )
        )

    member x.Update() = 0, Map.empty, TimeSpan.Zero
//        let dirtyResoruces = 
//            lock l (fun () ->
//                let dirtySet = set |> Seq.toArray
//                set.Clear()
//                dirtySet
//            )
//        sw.Restart()
//        let mutable count = 0
//
//        let mutable counts = Map.empty
//
//        let updateSw = System.Diagnostics.Stopwatch()
//        updateSw.Start()
//        System.Threading.Tasks.Parallel.ForEach(dirtyResoruces, fun (d : IChangeableResource) ->
//            lock d (fun () ->
//                if d.OutOfDate then
//                    let cnt = match Map.tryFind d.Kind counts with | Some v -> v | None -> 0.0
//                    counts <- Map.add d.Kind (cnt + 1.0) counts
//                    count <- count + 1
//
//                    d.UpdateCPU(renderTask)
//                    //d.UpdateGPU(renderTask)
//                else
//                    d.Outputs.Add renderTask |> ignore
//            )
//        ) |> ignore
//        updateSw.Stop()
//
//        Log.line "update took: %.3fµs per resource" (1000.0 * updateSw.Elapsed.TotalMilliseconds / float dirtyResoruces.Length)
//
//        for d in dirtyResoruces do
//            lock d (fun () ->
//                if d.OutOfDate then
////                    let cnt = match Map.tryFind d.Kind counts with | Some v -> v | None -> 0.0
////                    counts <- Map.add d.Kind (cnt + 1.0) counts
////                    count <- count + 1
////
////                    d.UpdateCPU(renderTask)
//                    d.UpdateGPU(renderTask)
//            )
//
//
//        if Config.SyncUploadsAndFrames then OpenTK.Graphics.OpenGL4.GL.Sync()
//        sw.Stop()
//
//        //printfn "GL: %fms vs .NET: %fms" glsw.Elapsed.TotalMilliseconds sw.Elapsed.TotalMilliseconds 
//
//        count,counts,sw.Elapsed

