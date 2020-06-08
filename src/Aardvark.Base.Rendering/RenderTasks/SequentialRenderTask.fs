namespace Aardvark.Base

open FSharp.Data.Adaptive

type SequentialRenderTask(tasks : IRenderTask[]) =
    inherit AbstractRenderTask()

    let signature =
        lazy (
            let signatures = tasks |> Array.choose (fun t -> t.FramebufferSignature)

            if signatures.Length = 0 then None
            elif signatures.Length = 1 then Some signatures.[0]
            else
                let s0 = signatures.[0]
                let all = signatures |> Array.forall (fun s -> s0.IsAssignableFrom s0)
                if all then Some s0
                else failwithf "cannot compose RenderTasks with different FramebufferSignatures: %A" signatures
        )

    let runtime = tasks |> Array.tryPick (fun t -> t.Runtime)
    member x.Tasks = tasks

    override x.Use(f : unit -> 'a) =
        lock x (fun () ->
            let rec run (i : int) =
                if i >= tasks.Length then f()
                else tasks.[i].Use (fun () -> run (i + 1))

            run 0
        )

    override x.Release() =
        for t in tasks do t.Dispose()

    override x.PerformUpdate(token : AdaptiveToken, rt : RenderToken) =
        for t in tasks do
            t.Update(token, rt)

    override x.Perform(token : AdaptiveToken, rt : RenderToken, output : OutputDescription, queries : IQuery) =
        queries.Begin()

        for t in tasks do
            t.Run(token, rt, output, queries)

        queries.End()

    override x.FramebufferSignature = signature.Value
    override x.Runtime = runtime