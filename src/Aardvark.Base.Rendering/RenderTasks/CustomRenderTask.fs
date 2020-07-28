namespace Aardvark.Base

open FSharp.Data.Adaptive

type CustomRenderTask(f : afun<AdaptiveToken * RenderToken * OutputDescription * TaskSync * IQuery, unit>) as this =
    inherit AbstractRenderTask()

    override x.FramebufferSignature = None
    override x.Perform(token, t, fbo, sync, queries) = f.Evaluate (token,(token, t, fbo, sync, queries))
    override x.Release() = f.Outputs.Remove this |> ignore
    override x.PerformUpdate(token, t) = ()
    override x.Runtime = None
    override x.Use f = lock x f