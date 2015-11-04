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

type InputSet(o : IAdaptiveObject) =
    let l = obj()
    let inputs = ReferenceCountingSet<IAdaptiveObject>()

    member x.Add(m : IAdaptiveObject) = 
        lock l (fun () ->
            if inputs.Add m then
                m.Outputs.Add o |> ignore
        )

    member x.Remove (m : IAdaptiveObject) = 
        lock l (fun () ->
            if inputs.Remove m then
                m.Outputs.Remove o |> ignore
        )

