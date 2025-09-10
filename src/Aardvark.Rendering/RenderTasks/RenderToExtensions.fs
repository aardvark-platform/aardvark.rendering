namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices

[<AutoOpen>]
module private AdaptiveRenderToTypes =

    let constantTask (task : IRenderTask) =
        AdaptiveResource.constant (fun _ -> task) ignore

    // Regular render targets
    type AdaptiveRenderingResult(task : IAdaptiveResource<IRenderTask>, target : IAdaptiveResource<IFramebuffer>) =
        inherit AdaptiveResource<IFramebuffer>()

        new(task : IRenderTask, target : IAdaptiveResource<IFramebuffer>) =
            new AdaptiveRenderingResult(constantTask task, target)

        override x.Compute(t : AdaptiveToken, rt : RenderToken) =
            use __ = rt.Use()

            let fbo = target.GetValue(t, rt)
            let task = task.GetValue(t, rt)
            task.Run(t, rt, OutputDescription.ofFramebuffer fbo)
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
    type AdaptiveRenderingResultCube(tasks : IAdaptiveResource<CubeMap<IRenderTask>>, targets : IAdaptiveResource<CubeMap<IFramebuffer>>) =
        inherit AdaptiveResource<CubeMap<IFramebuffer>>()

        new(tasks : CubeMap<IRenderTask>, targets : IAdaptiveResource<CubeMap<IFramebuffer>>) =
            new AdaptiveRenderingResultCube(constantCubeTask tasks, targets)

        override x.Compute(t : AdaptiveToken, rt : RenderToken) =
            use __ = rt.Use()

            let fbos = targets.GetValue(t, rt)
            let tasks = tasks.GetValue(t, rt)

            tasks |> CubeMap.iter2 (fun fbo task ->
                task.Run(t, rt, OutputDescription.ofFramebuffer fbo)
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

    /// Renders the given task to the given framebuffer, after clearing it according to the given adaptive clear values.
    [<Extension>]
    static member RenderTo(this : IRenderTask, output : IAdaptiveResource<IFramebuffer>, clearValues : aval<ClearValues>) =
        let runtime = this.GetRuntime()
        let signature = this.GetFramebufferSignature()

        let task =
            let mutable clear = Unchecked.defaultof<IRenderTask>

            let create() =
                clear <- runtime.CompileClear(signature, clearValues)
                if notNull this.Name then clear.Name <- $"{this.Name} (Clear)"
                new SequentialRenderTask [| clear; this |] :> IRenderTask

            let destroy (_ : IRenderTask) =
                clear.Dispose()
                clear <- Unchecked.defaultof<IRenderTask>

            AdaptiveResource.constant create destroy

        AdaptiveRenderingResult(task, output) :> IAdaptiveResource<_>

    /// Renders the given task to the given framebuffer, after clearing it according to the given clear values.
    [<Extension>]
    static member RenderTo(this : IRenderTask, output : IAdaptiveResource<IFramebuffer>, clearValues : ClearValues) =
        this.RenderTo(output, AVal.constant clearValues)

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

    /// Renders the given tasks to the given framebuffers, after clearing them according to the given adaptive clear values.
    [<Extension>]
    static member RenderTo(this : CubeMap<#IRenderTask>, output : IAdaptiveResource<CubeMap<IFramebuffer>>, clearValues : CubeMap<aval<ClearValues>>) =
        let task = this.Data.[0]
        let runtime = task.GetRuntime()
        let signature = task.GetFramebufferSignature()

        let compiled =
            let cache = Dict<aval<ClearValues>, IRenderTask>()

            let create() =
                (clearValues, this) ||> CubeMap.map2(fun values task ->
                    let clear = cache.GetOrCreate(values, fun _ -> runtime.CompileClear(signature, values))
                    if notNull task.Name then clear.Name <- $"{task.Name} (Clear)"
                    new SequentialRenderTask [|clear; task|] :> IRenderTask
                )

            let destroy (_ : CubeMap<IRenderTask>) =
                for KeyValue(_, t) in cache do
                    t.Dispose()
                cache.Clear()

            AdaptiveResource.constant create destroy

        AdaptiveRenderingResultCube(compiled, output) :> IAdaptiveResource<_>

    /// Renders the given tasks to the given framebuffers, after clearing them according to the given clear values.
    [<Extension>]
    static member RenderTo(this : CubeMap<#IRenderTask>, output : IAdaptiveResource<CubeMap<IFramebuffer>>, clearValues : CubeMap<ClearValues>) =
        let clear = clearValues |> CubeMap.map AVal.constant
        this.RenderTo(output, clear)

    /// Renders the given tasks to the given framebuffers, after clearing them according to the given adaptive clear values.
    [<Extension>]
    static member RenderTo(this : CubeMap<#IRenderTask>, output : IAdaptiveResource<CubeMap<IFramebuffer>>, clearValues : aval<ClearValues>) =
        let clear = CubeMap.single this.Levels clearValues
        this.RenderTo(output, clear)

    /// Renders the given tasks to the given framebuffers, after clearing them according to the given clear values.
    [<Extension>]
    static member RenderTo(this : CubeMap<#IRenderTask>, output : IAdaptiveResource<CubeMap<IFramebuffer>>, clearValues : ClearValues) =
        this.RenderTo(output, AVal.constant clearValues)

    /// Gets the cube attachment of the framebuffer with the given semantic.
    [<Extension>]
    static member GetOutputTexture(this : IAdaptiveResource<CubeMap<IFramebuffer>>, semantic : Symbol) =
        AdaptiveOutputTextureCube(semantic, this) :> IAdaptiveResource<_>