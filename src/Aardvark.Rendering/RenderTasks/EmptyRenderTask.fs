namespace Aardvark.Rendering

open FSharp.Data.Adaptive

type EmptyRenderTask private() =
    inherit ConstantObject()
    let id = RenderTaskId.New()
    static let instance = new EmptyRenderTask() :> IRenderTask
    static member Instance = instance

    interface IRenderTask with
        member x.Id = id
        member x.FramebufferSignature = None
        member x.Dispose() = ()
        member x.Update(caller,t) = ()
        member x.Run(caller, t, fbo) = ()
        member x.Runtime = None
        member x.FrameId = 0UL
        member x.Use f = f()