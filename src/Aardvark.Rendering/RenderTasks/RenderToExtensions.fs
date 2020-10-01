namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

[<AutoOpen>]
module private AdaptiveRenderToTypes =

    // Regular render targets
    type AdaptiveRenderingResult(task : IRenderTask, target : IAdaptiveResource<IFramebuffer>, queries : IQuery, disposeTask : bool) =
        inherit AdaptiveResource<IFramebuffer>()

        new(task : IRenderTask, target : IAdaptiveResource<IFramebuffer>, disposeTask : bool) =
            new AdaptiveRenderingResult(task, target, Queries.empty, disposeTask)

        new(task : IRenderTask, target : IAdaptiveResource<IFramebuffer>) =
            new AdaptiveRenderingResult(task, target, false)

        override x.Compute(t : AdaptiveToken, rt : RenderToken) =
            let fbo = target.GetValue(t, rt)
            task.Run(t, rt, OutputDescription.ofFramebuffer fbo, queries)
            fbo

        override x.Create() =
            target.Acquire()

        override x.Destroy() =
            target.Release()
            if disposeTask then
                task.Dispose()

    type AdaptiveOutputTexture(semantic : Symbol, res : IAdaptiveResource<IFramebuffer>) =
        inherit AdaptiveResource<IBackendTexture>()

        override x.Compute(t : AdaptiveToken, rt : RenderToken) =
            let res = res.GetValue(t, rt)

            match Map.tryFind semantic res.Attachments with
                | Some (:? IBackendTextureOutputView as o) ->
                    o.texture
                | _ ->
                    failwithf "could not get result for semantic %A as texture" semantic

        override x.Create() =
            res.Acquire()

        override x.Destroy() =
            res.Release()


    // Cube render targets
    type AdaptiveRenderingResultCube(tasks : CubeMap<IRenderTask>, targets : IAdaptiveResource<CubeMap<IFramebuffer>>, queries : IQuery, disposeTasks : bool) =
        inherit AdaptiveResource<CubeMap<IFramebuffer>>()

        new(tasks : CubeMap<IRenderTask>, targets : IAdaptiveResource<CubeMap<IFramebuffer>>, disposeTasks : bool) =
            new AdaptiveRenderingResultCube(tasks, targets, Queries.empty, disposeTasks)

        new(tasks : CubeMap<IRenderTask>, targets : IAdaptiveResource<CubeMap<IFramebuffer>>) =
            new AdaptiveRenderingResultCube(tasks, targets, false)

        override x.Compute(t : AdaptiveToken, rt : RenderToken) =
            let fbos = targets.GetValue(t, rt)

            tasks |> CubeMap.iter2 (fun fbo task ->
                task.Run(t, rt, OutputDescription.ofFramebuffer fbo, queries)
            ) fbos

            fbos

        override x.Create() =
            targets.Acquire()

        override x.Destroy() =
            targets.Release()
            if disposeTasks then
                tasks |> CubeMap.iter Disposable.dispose

    type AdaptiveOutputTextureCube(semantic : Symbol, res : IAdaptiveResource<CubeMap<IFramebuffer>>) =
        inherit AdaptiveResource<IBackendTexture>()

        override x.Compute(t : AdaptiveToken, rt : RenderToken) =
            let res = res.GetValue(t, rt) |> CubeMap.data

            match Map.tryFind semantic res.[0].Attachments with
                | Some (:? IBackendTextureOutputView as o) ->
                    o.texture
                | _ ->
                    failwithf "could not get result for semantic %A as texture" semantic

        override x.Create() =
            res.Acquire()

        override x.Destroy() =
            res.Release()

[<AbstractClass; Sealed; Extension>]
type RenderToExtensions private() =

    // ================================================================================================================
    //  Normal
    // ================================================================================================================

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
    static member GetOutputTexture(this : IAdaptiveResource<IFramebuffer>, semantic : Symbol) =
        AdaptiveOutputTexture(semantic, this) :> IAdaptiveResource<_>


    // ================================================================================================================
    // Cube
    // ================================================================================================================

    /// Renders the given tasks to the given framebuffers.
    /// If dispose is set to true, the render tasks are disposed when the resulting output is released.
    [<Extension>]
    static member RenderTo(this : CubeMap<#IRenderTask>,
                           output : IAdaptiveResource<CubeMap<IFramebuffer>>,
                           [<Optional; DefaultParameterValue(false)>] dispose : bool) =
        AdaptiveRenderingResultCube(this |> CubeMap.map (fun x -> x :> IRenderTask), output, dispose) :> IAdaptiveResource<_>

    /// Renders the given tasks to the given framebuffers.
    /// If dispose is set to true, the render tasks are disposed when the resulting output is released.
    [<Extension>]
    static member RenderTo(this : CubeMap<#IRenderTask>,
                           output : IAdaptiveResource<CubeMap<IFramebuffer>>,
                           queries : IQuery,
                           [<Optional; DefaultParameterValue(false)>] dispose : bool) =
        AdaptiveRenderingResultCube(this |> CubeMap.map (fun x -> x :> IRenderTask), output, queries, dispose) :> IAdaptiveResource<_>

    /// Gets the cube attachment of the framebuffer with the given semantic.
    [<Extension>]
    static member GetOutputTexture(this : IAdaptiveResource<CubeMap<IFramebuffer>>, semantic : Symbol) =
        AdaptiveOutputTextureCube(semantic, this) :> IAdaptiveResource<_>