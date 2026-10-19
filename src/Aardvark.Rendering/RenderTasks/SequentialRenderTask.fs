namespace Aardvark.Rendering

open System
open System.Runtime.ExceptionServices
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

/// <summary>A fixed sequence of render tasks.</summary>
/// <remarks>
/// Disposal attempts every child array entry in order, including duplicates, despite failures.
/// A single failure retains its exception identity and stack; multiple failures are aggregated
/// in disposal order without flattening existing aggregates. Parent disposal remains one-shot.
/// </remarks>
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
        let mutable first : ExceptionDispatchInfo = null
        let mutable errors : ResizeArray<exn> = null
        for t in tasks do
            try t.Dispose()
            with error ->
                if isNull first then
                    first <- ExceptionDispatchInfo.Capture error
                else
                    if isNull errors then
                        errors <- ResizeArray<exn>()
                        errors.Add first.SourceException
                    errors.Add error

        if not (isNull errors) then raise (AggregateException(errors))
        elif not (isNull first) then first.Throw()

    override x.PerformUpdate(token : AdaptiveToken, renderToken : RenderToken) =
        for t in tasks do
            t.Update(token, renderToken)

    override x.Perform(token : AdaptiveToken, renderToken : RenderToken, output : OutputDescription) =
        for t in tasks do
            t.Run(token, renderToken, output)

    override x.FramebufferSignature = signature.Value
    override x.Runtime = runtime