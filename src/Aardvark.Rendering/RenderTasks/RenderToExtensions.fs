namespace Aardvark.Base

open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

[<AutoOpen>]
module private AdaptiveRenderToTypes =

    type AdaptiveRenderingResult(task : IRenderTask, target : IAdaptiveResource<IFramebuffer>, queries : IQuery, disposeTask : bool) =
        inherit AdaptiveResource<IFramebuffer>()

        new(task : IRenderTask, target : IAdaptiveResource<IFramebuffer>, disposeTask : bool) =
            new AdaptiveRenderingResult(task, target, Queries.empty, disposeTask)

        new(task : IRenderTask, target : IAdaptiveResource<IFramebuffer>) =
            new AdaptiveRenderingResult(task, target, false)

        override x.Compute(token : AdaptiveToken, t : RenderToken) =
            let fbo = target.GetValue(token, t)
            task.Run(token, t, OutputDescription.ofFramebuffer fbo, queries)
            fbo

        override x.Create() =
            Log.line "result created"
            target.Acquire()

        override x.Destroy() =
            Log.line "result deleted"
            target.Release()
            if disposeTask then
                task.Dispose()

    type AdaptiveOutputTexture(semantic : Symbol, res : IAdaptiveResource<IFramebuffer>) =
        inherit AdaptiveResource<ITexture>()

        override x.Compute(token : AdaptiveToken, t : RenderToken) =
            let res = res.GetValue(token, t)

            match Map.tryFind semantic res.Attachments with
                | Some (:? IBackendTextureOutputView as t) ->
                    t.texture :> ITexture
                | _ ->
                    failwithf "could not get result for semantic %A as texture" semantic

        override x.Create() =
            Log.line "texture created"
            res.Acquire()

        override x.Destroy() =
            Log.line "texture deleted"
            res.Release()

[<AbstractClass; Sealed; Extension>]
type RenderToExtensions private() =

    /// Renders the given task to the given framebuffer.
    /// If dispose is set to true, the render task is disposed when the resulting output is released.
    [<Extension>]
    static member RenderTo(this : IRenderTask, output : IAdaptiveResource<IFramebuffer>, [<Optional; DefaultParameterValue(false)>] dispose : bool) =
        AdaptiveRenderingResult(this, output, dispose) :> IAdaptiveResource<_>

    /// Renders the given task to the given framebuffer.
    /// If dispose is set to true, the render task is disposed when the resulting output is released.
    [<Extension>]
    static member RenderTo(this : IRenderTask, output : IAdaptiveResource<IFramebuffer>, queries : IQuery, [<Optional; DefaultParameterValue(false)>] dispose : bool) =
        AdaptiveRenderingResult(this, output, queries, dispose) :> IAdaptiveResource<_>

    /// Gets the attachment of the framebuffer with the given semantic.
    [<Extension>]
    static member GetOutputTexture (this : IAdaptiveResource<IFramebuffer>, semantic : Symbol) =
        AdaptiveOutputTexture(semantic, this) :> IAdaptiveResource<_>