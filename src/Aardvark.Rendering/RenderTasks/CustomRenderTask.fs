namespace Aardvark.Rendering

open FSharp.Data.Adaptive

type CustomRenderTask(f : afun<AdaptiveToken * RenderToken * OutputDescription * IQuery, unit>) as this =
    inherit AbstractRenderTask()

    override x.FramebufferSignature = None
    override x.Perform(token, t, fbo, queries) = f.Evaluate (token,(token, t, fbo, queries))
    override x.Release() = f.Outputs.Remove this |> ignore
    override x.PerformUpdate(token, t) = ()
    override x.Runtime = None
    override x.Use f = lock x f