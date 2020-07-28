namespace Aardvark.Base

open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

[<AutoOpen>]
module private AdaptiveRenderToTypes =

    type AdaptiveRenderingResult(task : IRenderTask, target : IOutputMod<IFramebuffer>, sync : TaskSync, queries : IQuery, disposeTask : bool) =
        inherit AbstractOutputMod<IFramebuffer>()

        new(task : IRenderTask, target : IOutputMod<IFramebuffer>, disposeTask : bool) =
            new AdaptiveRenderingResult(task, target, TaskSync.none, Queries.empty, disposeTask)

        new(task : IRenderTask, target : IOutputMod<IFramebuffer>) =
            new AdaptiveRenderingResult(task, target, false)

        override x.Compute(token : AdaptiveToken, t : RenderToken) =
            let fbo = target.GetValue(token, t)
            task.Run(token, t, OutputDescription.ofFramebuffer fbo, sync, queries)
            fbo

        override x.Create() =
            Log.line "result created"
            target.Acquire()

        override x.Destroy() =
            Log.line "result deleted"
            target.Release()
            if disposeTask then
                task.Dispose()

    type AdaptiveOutputTexture(semantic : Symbol, res : IOutputMod<IFramebuffer>) =
        inherit AbstractOutputMod<ITexture>()

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
    static member RenderTo(this : IRenderTask, output : IOutputMod<IFramebuffer>, [<Optional; DefaultParameterValue(false)>] dispose : bool) =
        AdaptiveRenderingResult(this, output, dispose) :> IOutputMod<_>

    /// Renders the given task to the given framebuffer.
    /// If dispose is set to true, the render task is disposed when the resulting output is released.
    [<Extension>]
    static member RenderTo(this : IRenderTask, output : IOutputMod<IFramebuffer>, sync : TaskSync, queries : IQuery, [<Optional; DefaultParameterValue(false)>] dispose : bool) =
        AdaptiveRenderingResult(this, output, sync, queries, dispose) :> IOutputMod<_>

    [<Extension>]
    static member GetOutputTexture (this : IOutputMod<IFramebuffer>, semantic : Symbol) =
        AdaptiveOutputTexture(semantic, this) :> IOutputMod<_>