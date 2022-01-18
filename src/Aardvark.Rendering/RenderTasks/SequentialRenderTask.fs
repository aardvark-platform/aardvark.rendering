namespace Aardvark.Rendering

open FSharp.Data.Adaptive

module private FramebufferSignature =

    /// Combines many signatures into a single one.
    /// If the input is empty, returns None.
    /// If the signatures are incompatible (i.e. not all equivalent), returns None.
    /// Returns the first signature otherwise.
    let combineMany (signatures : IFramebufferSignature[]) =
        if signatures.Length = 0 then None
        else
            let s0 = signatures.[0]

            if signatures.Length = 1 || signatures |> Array.forall (fun s -> s.IsCompatibleWith s0) then
                Some s0
            else
                None

type SequentialRenderTask(tasks : IRenderTask[]) =
    inherit AbstractRenderTask()

    let signature =
        lazy (
            tasks
            |> Array.choose (fun t -> t.FramebufferSignature)
            |> FramebufferSignature.combineMany
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

    override x.PerformUpdate(token : AdaptiveToken, renderToken : RenderToken) =
        for t in tasks do
            t.Update(token, renderToken)

    override x.Perform(token : AdaptiveToken, renderToken : RenderToken, output : OutputDescription) =
        for t in tasks do
            t.Run(token, renderToken, output)

    override x.FramebufferSignature = signature.Value
    override x.Runtime = runtime