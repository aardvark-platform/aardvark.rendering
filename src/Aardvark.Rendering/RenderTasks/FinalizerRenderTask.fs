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
    override x.Perform(token, renderToken, fbo) = inner.Run(token, renderToken, fbo)
    override x.PerformUpdate(token, renderToken) = inner.Update(token, renderToken)
    override x.FramebufferSignature = inner.FramebufferSignature
    override x.Runtime = inner.Runtime