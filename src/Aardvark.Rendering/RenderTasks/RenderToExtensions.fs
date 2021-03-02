namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices

[<AutoOpen>]
module private AdaptiveRenderToTypes =

    let constantTask (task : IRenderTask) =
        AdaptiveResource.constant (fun _ -> task) ignore

    // Regular render targets
    type AdaptiveRenderingResult(task : IAdaptiveResource<IRenderTask>, target : IAdaptiveResource<IFramebuffer>, queries : IQuery) =
        inherit AdaptiveResource<IFramebuffer>()

        new(task : IAdaptiveResource<IRenderTask>, target : IAdaptiveResource<IFramebuffer>) =
            new AdaptiveRenderingResult(task, target, Queries.none)

        new(task : IRenderTask, target : IAdaptiveResource<IFramebuffer>, queries : IQuery) =
            new AdaptiveRenderingResult(constantTask task, target, queries)

        new(task : IRenderTask, target : IAdaptiveResource<IFramebuffer>) =
            new AdaptiveRenderingResult(constantTask task, target, Queries.none)

        override x.Compute(t : AdaptiveToken, rt : RenderToken) =
            let fbo = target.GetValue(t, rt)
            let task = task.GetValue(t, rt)
            task.Run(t, rt, OutputDescription.ofFramebuffer fbo, queries)
            fbo

        override x.Create() =
            task.Acquire()
            target.Acquire()

        override x.Destroy() =
            target.Release()
            task.Release()

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


    let constantCubeTask (tasks : CubeMap<IRenderTask>) =
        AdaptiveResource.constant (fun _ -> tasks) ignore

    // Cube render targets
    type AdaptiveRenderingResultCube(tasks : IAdaptiveResource<CubeMap<IRenderTask>>, targets : IAdaptiveResource<CubeMap<IFramebuffer>>, queries : IQuery) =
        inherit AdaptiveResource<CubeMap<IFramebuffer>>()

        new(tasks : IAdaptiveResource<CubeMap<IRenderTask>>, targets : IAdaptiveResource<CubeMap<IFramebuffer>>) =
            new AdaptiveRenderingResultCube(tasks, targets, Queries.none)

        new(tasks : CubeMap<IRenderTask>, targets : IAdaptiveResource<CubeMap<IFramebuffer>>, queries : IQuery) =
            new AdaptiveRenderingResultCube(constantCubeTask tasks, targets, queries)

        new(tasks : CubeMap<IRenderTask>, targets : IAdaptiveResource<CubeMap<IFramebuffer>>) =
            new AdaptiveRenderingResultCube(constantCubeTask tasks, targets, Queries.none)

        override x.Compute(t : AdaptiveToken, rt : RenderToken) =
            let fbos = targets.GetValue(t, rt)
            let tasks = tasks.GetValue(t, rt)

            tasks |> CubeMap.iter2 (fun fbo task ->
                task.Run(t, rt, OutputDescription.ofFramebuffer fbo, queries)
            ) fbos

            fbos

        override x.Create() =
            tasks.Acquire()
            targets.Acquire()

        override x.Destroy() =
            targets.Release()
            tasks.Release()

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
    [<Extension>]
    static member RenderTo(this : IRenderTask, output : IAdaptiveResource<IFramebuffer>) =
        AdaptiveRenderingResult(this, output) :> IAdaptiveResource<_>

    /// Renders the given task to the given framebuffer.
    [<Extension>]
    static member RenderTo(this : IRenderTask, output : IAdaptiveResource<IFramebuffer>, queries : IQuery) =
        AdaptiveRenderingResult(this, output, queries) :> IAdaptiveResource<_>

    /// Gets the attachment of the framebuffer with the given semantic.
    [<Extension>]
    static member GetOutputTexture(this : IAdaptiveResource<IFramebuffer>, semantic : Symbol) =
        AdaptiveOutputTexture(semantic, this) :> IAdaptiveResource<_>


    // ================================================================================================================
    // Cube
    // ================================================================================================================

    /// Renders the given tasks to the given framebuffers.
    [<Extension>]
    static member RenderTo(this : CubeMap<#IRenderTask>, output : IAdaptiveResource<CubeMap<IFramebuffer>>) =
        AdaptiveRenderingResultCube(this |> CubeMap.map (fun x -> x :> IRenderTask), output) :> IAdaptiveResource<_>

    /// Renders the given tasks to the given framebuffers.
    [<Extension>]
    static member RenderTo(this : CubeMap<#IRenderTask>, output : IAdaptiveResource<CubeMap<IFramebuffer>>, queries : IQuery) =
        AdaptiveRenderingResultCube(this |> CubeMap.map (fun x -> x :> IRenderTask), output, queries) :> IAdaptiveResource<_>

    /// Gets the cube attachment of the framebuffer with the given semantic.
    [<Extension>]
    static member GetOutputTexture(this : IAdaptiveResource<CubeMap<IFramebuffer>>, semantic : Symbol) =
        AdaptiveOutputTextureCube(semantic, this) :> IAdaptiveResource<_>