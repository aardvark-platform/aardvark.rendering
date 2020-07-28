namespace Aardvark.Base

type BeforeAfterRenderTask(before : Option<unit -> unit>, after : Option<unit -> unit>, inner : IRenderTask) =
    inherit AbstractRenderTask()

    member x.Before = before
    member x.After = after
    member x.Inner = inner

    override x.Use f =
        lock x (fun () ->
            inner.Use f
        )

    override x.FramebufferSignature = inner.FramebufferSignature
    override x.PerformUpdate(token, t) = inner.Update(token,t)
    override x.Perform(token, t, fbo, sync, queries) =
        match before with
        | Some before -> before()
        | None -> ()

        let res = inner.Run(token, t, fbo, sync, queries)

        match after with
        | Some after -> after()
        | None -> ()

        res

    override x.Release() = inner.Dispose()
    override x.Runtime = inner.Runtime