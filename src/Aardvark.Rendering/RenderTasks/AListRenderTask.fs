namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open System.Collections.Generic
open System.Runtime.ExceptionServices

type AListRenderTask(tasks : alist<IRenderTask>) as this =
    inherit AbstractRenderTask()
    let content = SortedDictionary<Index, IRenderTask>()

    let reader = tasks.GetReader()
    let tasks = ReferenceCountingSet()

    let mutable signature : Option<IFramebufferSignature> = None
    let mutable signatureDirty = false

    let disposeAll (items : seq<IRenderTask>) =
        let mutable failure = ValueNone

        for task in items do
            try
                task.Dispose()
            with e ->
                match failure with
                | ValueNone -> failure <- ValueSome (ExceptionDispatchInfo.Capture e)
                | ValueSome _ -> ()

        failure

    let rethrow = function
        | ValueSome (failure : ExceptionDispatchInfo) -> failure.Throw()
        | ValueNone -> ()

    let updateSignature() =
        signature <-
            Seq.asArray tasks
            |> Array.choose (fun (t : IRenderTask) -> t.FramebufferSignature)
            |> FramebufferSignature.combineMany

    let processDeltas(token : AdaptiveToken) =
        // TODO: EvaluateAlways should ensure that self is OutOfDate since
        //       when its not we need a transaction to add outputs
        let wasOutOfDate = this.OutOfDate
        this.OutOfDate <- true

        try
            let deltas = reader.GetChanges token
            if deltas.IsEmpty then
                // Retry a signature update that failed after an earlier batch was applied.
                if signatureDirty then
                    updateSignature()
                    signatureDirty <- false
            else
                // Disposal must be based on the final reference counts of the whole batch.
                // In particular, a task may temporarily lose its last reference during a swap.
                let toDispose = HashSet<IRenderTask>()
                let mutable failure = ValueNone

                try
                    try
                        for (i, op) in deltas do
                            match op with
                            | Set t ->
                                match content.TryGetValue i with
                                | true, old when tasks.Remove old ->
                                    toDispose.Add old |> ignore
                                    signatureDirty <- true
                                | _ ->
                                    ()

                                content.[i] <- t
                                if tasks.Add t then
                                    // The task may have been removed at an earlier index in this batch.
                                    toDispose.Remove t |> ignore
                                    signatureDirty <- true

                            | Remove ->
                                match content.TryGetValue i with
                                | true, old ->
                                    content.Remove i |> ignore
                                    if tasks.Remove old then
                                        toDispose.Add old |> ignore
                                        signatureDirty <- true
                                | _ ->
                                    ()

                        // Read every final distinct task at most once, after all reference counts settled.
                        if signatureDirty then
                            updateSignature()
                            signatureDirty <- false
                    with e ->
                        failure <- ValueSome (ExceptionDispatchInfo.Capture e)
                finally
                    // User callbacks may throw. Attempt every final disposal, but preserve the
                    // exception which interrupted batch processing when there is one.
                    let disposalFailure = disposeAll toDispose
                    match failure with
                    | ValueSome _ -> rethrow failure
                    | ValueNone -> rethrow disposalFailure
        finally
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
        lock this (fun () ->
            processDeltas(AdaptiveToken.Top)
            signature
        )

    override x.PerformUpdate(token, renderToken) =
        processDeltas token

        for t in reader.State do
            t.Update(token, renderToken)

    override x.Perform(token, renderToken, fbo) =
        processDeltas(token)

        // TODO: order may be invalid
        for t in reader.State do
            t.Run(token, renderToken, fbo)

    override x.Release() =
        let current = Seq.asArray tasks
        let mutable failure = ValueNone

        try
            reader.Outputs.Remove this |> ignore
        with e ->
            failure <- ValueSome (ExceptionDispatchInfo.Capture e)

        // Clear owned state before invoking user disposal callbacks, since the base class
        // makes Release one-shot even if one of those callbacks fails.
        tasks.Clear()
        content.Clear()
        signature <- None
        signatureDirty <- false

        let disposalFailure = disposeAll current
        match failure with
        | ValueSome _ -> rethrow failure
        | ValueNone -> rethrow disposalFailure

    override x.Runtime =
        lock this (fun () ->
            processDeltas(AdaptiveToken.Top)
            tasks |> Seq.tryPick (fun t -> t.Runtime)
        )
