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
            task.Run(t, rt.WithQuery queries, OutputDescription.ofFramebuffer fbo)
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
            | Some (:? ITextureRange as o) -> o.Texture
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
                task.Run(t, rt.WithQuery queries, OutputDescription.ofFramebuffer fbo)
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
            | Some (:? ITextureRange as o) -> o.Texture
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

    /// Renders the given task to the given framebuffer, after clearing it according to the given clear values.
    [<Extension>]
    static member RenderTo(this : IRenderTask, output : IAdaptiveResource<IFramebuffer>, clearValues : ClearValues, queries : IQuery) =
        let runtime = this.Runtime.Value
        let signature = this.FramebufferSignature.Value

        let task =
            let mutable clear = Unchecked.defaultof<IRenderTask>

            let create() =
                clear <- runtime.CompileClear(signature, clearValues)
                new SequentialRenderTask [| clear; this |] :> IRenderTask

            let destroy (_ : IRenderTask) =
                clear.Dispose()
                clear <- Unchecked.defaultof<IRenderTask>

            AdaptiveResource.constant create destroy

        AdaptiveRenderingResult(task, output, queries) :> IAdaptiveResource<_>

    /// Renders the given task to the given framebuffer, after clearing it according to the given clear values.
    [<Extension>]
    static member RenderTo(this : IRenderTask, output : IAdaptiveResource<IFramebuffer>, clearValues : ClearValues) =
        this.RenderTo(output, clearValues, Queries.none)

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

    /// Renders the given tasks to the given framebuffers, after clearing them according to the given clear values.
    [<Extension>]
    static member RenderTo(this : CubeMap<#IRenderTask>, output : IAdaptiveResource<CubeMap<IFramebuffer>>, clearValues : CubeMap<ClearValues>, queries : IQuery) =
        let task = this.Data.[0]
        let runtime = task.Runtime.Value
        let signature = task.FramebufferSignature.Value

        let compiled =
            let cache = Dict<ClearValues, IRenderTask>()

            let create() =
                (clearValues, this) ||> CubeMap.map2(fun values task ->
                    let clear = cache.GetOrCreate(values, fun _ -> runtime.CompileClear(signature, values))
                    new SequentialRenderTask [|clear; task|] :> IRenderTask
                )

            let destroy (_ : CubeMap<IRenderTask>) =
                for KeyValue(_, t) in cache do
                    t.Dispose()
                cache.Clear()

            AdaptiveResource.constant create destroy

        AdaptiveRenderingResultCube(compiled, output, queries) :> IAdaptiveResource<_>

    /// Renders the given tasks to the given framebuffers, after clearing them according to the given clear values.
    [<Extension>]
    static member RenderTo(this : CubeMap<#IRenderTask>, output : IAdaptiveResource<CubeMap<IFramebuffer>>, clearValues : ClearValues, queries : IQuery) =
        let clear = CubeMap.single this.Levels clearValues
        this.RenderTo(output, clear, queries)

    /// Renders the given tasks to the given framebuffers, after clearing them according to the given clear values.
    [<Extension>]
    static member RenderTo(this : CubeMap<#IRenderTask>, output : IAdaptiveResource<CubeMap<IFramebuffer>>, clearValues : CubeMap<ClearValues>) =
        this.RenderTo(output, clearValues, Queries.none)

    /// Renders the given tasks to the given framebuffers, after clearing them according to the given clear values.
    [<Extension>]
    static member RenderTo(this : CubeMap<#IRenderTask>, output : IAdaptiveResource<CubeMap<IFramebuffer>>, clearValues : ClearValues) =
        this.RenderTo(output, clearValues, Queries.none)

    /// Gets the cube attachment of the framebuffer with the given semantic.
    [<Extension>]
    static member GetOutputTexture(this : IAdaptiveResource<CubeMap<IFramebuffer>>, semantic : Symbol) =
        AdaptiveOutputTextureCube(semantic, this) :> IAdaptiveResource<_>