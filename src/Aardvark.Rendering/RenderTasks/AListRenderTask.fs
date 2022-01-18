namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open System.Collections.Generic

type AListRenderTask(tasks : alist<IRenderTask>) as this =
    inherit AbstractRenderTask()
    let content = SortedDictionary<Index, IRenderTask>()

    let reader = tasks.GetReader()
    let tasks = ReferenceCountingSet()

    let mutable signature : Option<IFramebufferSignature> = None

    let updateSignature() =
        signature <-
            Seq.toArray tasks
            |> Array.choose (fun (t : IRenderTask) -> t.FramebufferSignature)
            |> FramebufferSignature.combineMany

    let set (i : Index) (t : IRenderTask) =
        match content.TryGetValue i with
        | (true, old) ->
            if tasks.Remove old then
                old.Dispose()
        | _ ->
            ()

        content.[i] <- t
        if tasks.Add t then
            updateSignature()

    let remove (i : Index) =
        match content.TryGetValue i with
        | (true, old) ->

            if tasks.Remove old then
                old.Dispose()

            content.Remove i |> ignore
            updateSignature()

        | _ ->
            ()

    let processDeltas(token : AdaptiveToken) =
        // TODO: EvaluateAlways should ensure that self is OutOfDate since
        //       when its not we need a transaction to add outputs
        let wasOutOfDate = this.OutOfDate
        this.OutOfDate <- true

        // adjust the dependencies
        for (i,op) in reader.GetChanges(token) |> IndexListDelta.toSeq do
            match op with
                | Set(t) -> set i t
                | Remove -> remove i

        this.OutOfDate <- wasOutOfDate

    override x.Use (f : unit -> 'a) =
        lock x (fun () ->
            processDeltas(AdaptiveToken.Top)
            let l = reader.State |> IndexList.toList

            let rec run (l : list<IRenderTask>) =
                match l with
                    | [] -> f()
                    | h :: rest -> h.Use (fun () -> run rest)

            run l
        )

    override x.FramebufferSignature =
        lock this (fun () -> processDeltas(AdaptiveToken.Top))
        signature

    override x.PerformUpdate(token, renderToken) =
        processDeltas token

        renderToken.Query.Begin()

        for t in reader.State do
            t.Update(token, renderToken)

        renderToken.Query.End()

    override x.Perform(token, renderToken, fbo) =
        processDeltas(token)

        renderToken.Query.Begin()

        // TODO: order may be invalid
        for t in reader.State do
            t.Run(token, renderToken, fbo)

        renderToken.Query.End()

    override x.Release() =
        reader.Outputs.Remove this |> ignore
        for i in tasks do
            i.Dispose()
        tasks.Clear()

    override x.Runtime =
        lock this (fun () ->
            processDeltas(AdaptiveToken.Top)
            tasks |> Seq.tryPick (fun t -> t.Runtime)
        )