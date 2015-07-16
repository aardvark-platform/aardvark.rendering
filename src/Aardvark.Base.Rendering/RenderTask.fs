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


    let bind (f : 'a -> IRenderTask) (m : IMod<'a>) : IRenderTask =
        new ModRenderTask(Mod.map f m) :> IRenderTask

    let ofSeq (s : seq<IRenderTask>) =
        new SequentialRenderTask(Seq.toArray s) :> IRenderTask

    let ofList (s : list<IRenderTask>) =
        new SequentialRenderTask(List.toArray s) :> IRenderTask

    let ofArray (s : IRenderTask[]) =
        new SequentialRenderTask(s) :> IRenderTask

