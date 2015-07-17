namespace Aardvark.Base

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open System.Collections.Generic
open Aardvark.Base.Rendering

type RenderingResult(f : IFramebuffer, stats : FrameStatistics) =
    member x.Framebuffer = f
    member x.Statistics = stats

type IRenderTask =
    inherit IDisposable
    inherit IAdaptiveObject
    abstract member Run : IFramebuffer -> RenderingResult


module RenderTask =
    
    type private EmptyRenderTask() =
        inherit ConstantObject()
        interface IRenderTask with
            member x.Dispose() = ()
            member x.Run(fbo) = RenderingResult(fbo, FrameStatistics.Zero)

    type private SequentialRenderTask(tasks : IRenderTask[]) as this =
        inherit AdaptiveObject()

        do for t in tasks do t.AddOutput this

        interface IRenderTask with
            member x.Run(fbo) =
                base.EvaluateAlways(fun () ->
                    let mutable stats = FrameStatistics.Zero
                    for t in tasks do
                        let res = t.Run(fbo)
                        stats <- stats + res.Statistics
                    RenderingResult(fbo, stats)
                )

            member x.Dispose() =
                for t in tasks do t.RemoveOutput this

    type private ModRenderTask(input : IMod<IRenderTask>) as this =
        inherit AdaptiveObject()
        do input.AddOutput this
        let mutable inner : Option<IRenderTask> = None

        interface IRenderTask with
            member x.Run(fbo) =
                base.EvaluateAlways(fun () ->
                    x.OutOfDate <- true
                    let ni = input |> Mod.force

                    match inner with
                        | Some oi when oi = ni -> ()
                        | _ ->
                            match inner with
                                | Some oi -> oi.RemoveOutput x
                                | _ -> ()

                            ni.AddOutput x

                    inner <- Some ni
                    ni.Run fbo
                )

            member x.Dispose() =
                input.RemoveOutput x
                match inner with
                    | Some i -> 
                        i.RemoveOutput x
                        inner <- None
                    | _ -> ()

    type private AListRenderTask(tasks : alist<IRenderTask>) as this =
        inherit AdaptiveObject()
        let reader = tasks.GetReader()
        do reader.AddOutput this

        let tasks = ReferenceCountingSet()

        let add (t : IRenderTask) =
            if tasks.Add t then
                t.AddOutput this

        let remove (t : IRenderTask) =
            if tasks.Remove t then
                t.RemoveOutput this

        interface IRenderTask with
            member x.Run(fbo) =
                base.EvaluateAlways(fun () ->
                    // TODO: EvaluateAlways should ensure that self is OutOfDate since
                    //       when its not we need a transaction to add outputs
                    this.OutOfDate <- true

                    // adjust the dependencies
                    for d in reader.GetDelta() do
                        match d with
                            | Add(_,t) -> add t
                            | Rem(_,t) -> remove t

                    // run all tasks
                    let mutable stats = FrameStatistics.Zero
                    for (_,t) in reader.Content do
                        let res = t.Run(fbo)
                        stats <- stats + res.Statistics

                    // return the accumulated statistics
                    RenderingResult(fbo, stats)
                )

            member x.Dispose() =
                reader.RemoveOutput this
                reader.Dispose()

                for i in tasks do
                    i.RemoveOutput x
                tasks.Clear()


    let empty = new EmptyRenderTask() :> IRenderTask

    let ofMod (m : IMod<IRenderTask>) : IRenderTask =
        new ModRenderTask(m) :> IRenderTask

    let bind (f : 'a -> IRenderTask) (m : IMod<'a>) : IRenderTask =
        new ModRenderTask(Mod.map f m) :> IRenderTask

    let ofSeq (s : seq<IRenderTask>) =
        new SequentialRenderTask(Seq.toArray s) :> IRenderTask

    let ofList (s : list<IRenderTask>) =
        new SequentialRenderTask(List.toArray s) :> IRenderTask

    let ofArray (s : IRenderTask[]) =
        new SequentialRenderTask(s) :> IRenderTask

    let ofAList (s : alist<IRenderTask>) =
        new AListRenderTask(s) :> IRenderTask

    let ofASet (s : aset<IRenderTask>) =
        new AListRenderTask(s |> ASet.sortWith (fun a b -> 0)) :> IRenderTask

[<AutoOpen>]
module ``RenderTask Builder`` =

    type RenderTaskBuilder() =
        member x.Bind(m : IMod<'a>, f : 'a -> alist<IRenderTask>) =
            alist.Bind(m, f)

        member x.For(s : alist<'a>, f : 'a -> alist<IRenderTask>) =
            alist.For(s,f)


        member x.Yield(t : IRenderTask) = 
            alist.Yield(t)

        member x.YieldFrom(l : alist<IRenderTask>) =
            alist.YieldFrom(l)

        member x.Yield(m : IMod<IRenderTask>) =
            m |> RenderTask.ofMod |> alist.Yield

        member x.Combine(l : alist<IRenderTask>, r : alist<IRenderTask>) =
            alist.Combine(l,r)

        member x.Delay(f : unit -> alist<IRenderTask>) = 
            alist.Delay(f)

        member x.Zero() =
            alist.Zero()

        member x.Run(l : alist<IRenderTask>) =
            RenderTask.ofAList l


    let rendertask = RenderTaskBuilder()
