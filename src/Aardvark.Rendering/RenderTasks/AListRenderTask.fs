namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open System.Collections.Generic

type AListRenderTask(tasks : alist<IRenderTask>) as this =
    inherit AbstractRenderTask()
    let content = SortedDictionary<Index, IRenderTask>()

    let reader = tasks.GetReader()
    let mutable signature : Option<IFramebufferSignature> = None
    let mutable runtime = None
    let tasks = ReferenceCountingSet()

    let set (i : Index) (t : IRenderTask) =
        match content.TryGetValue i with
            | (true, old) ->
                if tasks.Remove old then
                    old.Dispose()
            | _ ->
                ()

        content.[i] <- t
        if tasks.Add t then
            match t.Runtime with
                | Some r -> runtime <- Some r
                | None -> ()

            let innerSig = t.FramebufferSignature

            match signature, innerSig with
                | Some s, Some i ->
                    if not (s.IsAssignableFrom i) then
                        failwithf "cannot compose RenderTasks with different FramebufferSignatures: %A vs. %A" signature innerSig
                | _-> signature <- innerSig

    let remove (i : Index) =
        match content.TryGetValue i with
            | (true, old) ->

                if tasks.Remove old then
                    old.Dispose()

                content.Remove i |> ignore
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

    override x.PerformUpdate(token, rt) =
        processDeltas token
        for t in reader.State do
            t.Update(token, rt)

    override x.Perform(token, rt, fbo, queries) =
        processDeltas(token)

        queries.Begin()

        // TODO: order may be invalid
        for t in reader.State do
            t.Run(token, rt, fbo, queries)

        queries.End()

    override x.Release() =
        reader.Outputs.Remove this |> ignore
        for i in tasks do
            i.Dispose()
        tasks.Clear()

    override x.Runtime =
        lock this (fun () -> processDeltas(AdaptiveToken.Top))
        runtime