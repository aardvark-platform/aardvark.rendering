namespace Aardvark.Rendering

open System
open System.Threading

type FinalizerRenderTask(inner : IRenderTask) =
    inherit AbstractRenderTask()
    let mutable inner = inner

    member private x.Dispose(disposing : bool) =
        let old = Interlocked.Exchange(&inner, EmptyRenderTask.Instance)
        old.Dispose()
        if disposing then GC.SuppressFinalize(x)

    override x.Finalize() =
        try x.Dispose false
        with _ -> ()

    override x.Use f =
        lock x (fun () ->
            inner.Use f
        )

    override x.Release() = x.Dispose true
    override x.Perform(token, t, fbo, queries) = inner.Run(token, t, fbo, queries)
    override x.PerformUpdate(token, t) = inner.Update(token, t)
    override x.FramebufferSignature = inner.FramebufferSignature
    override x.Runtime = inner.Runtime