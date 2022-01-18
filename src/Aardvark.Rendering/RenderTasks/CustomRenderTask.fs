namespace Aardvark.Rendering

open FSharp.Data.Adaptive

type CustomRenderTask(f : afun<AdaptiveToken * RenderToken * OutputDescription, unit>) as this =
    inherit AbstractRenderTask()

    override x.FramebufferSignature = None
    override x.Perform(token, renderToken, fbo) = f.Evaluate (token, (token, renderToken, fbo))
    override x.Release() = f.Outputs.Remove this |> ignore
    override x.PerformUpdate(token, renderToken) = ()
    override x.Runtime = None
    override x.Use f = lock x f