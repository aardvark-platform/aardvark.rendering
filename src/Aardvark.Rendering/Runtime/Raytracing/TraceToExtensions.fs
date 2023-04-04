namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

[<Extension>]
type CompileTraceToExtensions() =

    /// <summary>
    /// Compiles a raytracing task that dispatches rays based on the size of the given texture.
    /// The layout of the texture is transformed from <paramref name="srcLayout"/> to TextureLayout.ShaderWrite before the dispatch.
    /// After the dispatch the layout is transformed to <paramref name="dstLayout"/>.
    /// </summary>
    [<Extension>]
    static member inline CompileTraceToTexture(runtime : IRaytracingRuntime, pipeline : RaytracingPipelineState, target : aval<#IBackendTexture>,
                                               [<Optional; DefaultParameterValue(TextureLayout.ShaderRead)>] srcLayout : TextureLayout,
                                               [<Optional; DefaultParameterValue(TextureLayout.ShaderRead)>] dstLayout : TextureLayout) =
        let commands = target |> AVal.map (fun t -> RaytracingCommand.TraceRaysToTexture(t, srcLayout, dstLayout))
        runtime.CompileTrace(pipeline, commands)

    /// <summary>
    /// Compiles a raytracing task that dispatches rays based on the size of the given texture.
    /// The layout of the texture is transformed from <paramref name="srcLayout"/> to TextureLayout.ShaderWrite before the dispatch.
    /// After the dispatch the layout is transformed to <paramref name="dstLayout"/>.
    /// </summary>
    [<Extension>]
    static member inline CompileTraceToTexture(runtime : IRaytracingRuntime, pipeline : RaytracingPipelineState, target : IBackendTexture,
                                               [<Optional; DefaultParameterValue(TextureLayout.ShaderRead)>] srcLayout : TextureLayout,
                                               [<Optional; DefaultParameterValue(TextureLayout.ShaderRead)>] dstLayout : TextureLayout) =
        let commands = RaytracingCommand.TraceRaysToTexture(target, srcLayout, dstLayout)
        runtime.CompileTrace(pipeline, commands)

[<AutoOpen>]
module AdaptiveTraceToTypes =

    type AdaptiveTraceResult(runtime : IRaytracingRuntime, pipeline : RaytracingPipelineState, target : aval<IBackendTexture>) =
        inherit AdaptiveResource<IBackendTexture>()

        let task : IAdaptiveResource<IRaytracingTask> =
            AdaptiveResource.constant
                (fun _ -> runtime.CompileTraceToTexture(pipeline, target))
                Disposable.dispose

        override x.Create() =
            task.Acquire()

        override x.Destroy() =
            task.Release()

        override x.Compute(t : AdaptiveToken, rt : RenderToken) =
            use __ = rt.Use()
            task.GetValue(t, rt).Run(t, rt)
            target.GetValue(t, rt)

[<Extension>]
type TraceToExtensions() =

    // ================================================================================================================
    // All dimensions
    // ================================================================================================================

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of the given target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="target">The target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member TraceTo(runtime : IRaytracingRuntime, target : aval<#IBackendTexture>, pipeline : RaytracingPipelineState) =
        AdaptiveTraceResult(runtime, pipeline, target |> AVal.cast)

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of a target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="size">The size of the target texture.</param>
    ///<param name="dimension">The dimension of the target texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="semantic">The uniform name of the target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member inline TraceTo(runtime : IRaytracingRuntime, size : aval<V3i>, dimension : TextureDimension,
                                 format : TextureFormat, semantic : Symbol, pipeline : RaytracingPipelineState) =
        let target = runtime.CreateTexture(size, dimension, format)
        let provider = UniformProvider.ofList [semantic, target :> IAdaptiveValue]
        let pipeline = { pipeline with Uniforms = UniformProvider.union pipeline.Uniforms provider }
        runtime.TraceTo(target, pipeline)

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of a target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="size">The size of the target texture.</param>
    ///<param name="dimension">The dimension of the target texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="semantic">The uniform name of the target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member inline TraceTo(runtime : IRaytracingRuntime, size : aval<V3i>, dimension : TextureDimension,
                                 format : TextureFormat, semantic : string, pipeline : RaytracingPipelineState) =
        runtime.TraceTo(size, dimension, format, Sym.ofString semantic, pipeline)

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of a target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="size">The size of the target texture.</param>
    ///<param name="dimension">The dimension of the target texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="semantic">The uniform name of the target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member inline TraceTo(runtime : IRaytracingRuntime, size : V3i, dimension : TextureDimension,
                                 format : TextureFormat, semantic : Symbol, pipeline : RaytracingPipelineState) =
        runtime.TraceTo(AVal.constant size, dimension, format, semantic, pipeline)

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of a target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="size">The size of the target texture.</param>
    ///<param name="dimension">The dimension of the target texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="semantic">The uniform name of the target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member inline TraceTo(runtime : IRaytracingRuntime, size : V3i, dimension : TextureDimension,
                                 format : TextureFormat, semantic : string, pipeline : RaytracingPipelineState) =
        runtime.TraceTo(AVal.constant size, dimension, format, Sym.ofString semantic, pipeline)

    // ================================================================================================================
    // 1D
    // ================================================================================================================

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of a 1D target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="size">The size of the target texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="semantic">The uniform name of the target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member inline TraceTo1D(runtime : IRaytracingRuntime, size : aval<int>, format : TextureFormat, semantic : Symbol, pipeline : RaytracingPipelineState) =
        let size = size |> AVal.mapNonAdaptive (fun s -> V3i(s, 1, 1))
        runtime.TraceTo(size, TextureDimension.Texture1D, format, semantic, pipeline)

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of a 1D target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="size">The size of the target texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="semantic">The uniform name of the target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member inline TraceTo1D(runtime : IRaytracingRuntime, size : aval<int>, format : TextureFormat, semantic : string, pipeline : RaytracingPipelineState) =
        runtime.TraceTo1D(size, format, Sym.ofString semantic, pipeline)

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of a 1D target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="size">The size of the target texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="semantic">The uniform name of the target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member inline TraceTo1D(runtime : IRaytracingRuntime, size : int, format : TextureFormat, semantic : Symbol, pipeline : RaytracingPipelineState) =
        runtime.TraceTo1D(AVal.constant size, format, semantic, pipeline)

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of a 1D target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="size">The size of the target texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="semantic">The uniform name of the target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member inline TraceTo1D(runtime : IRaytracingRuntime, size : int, format : TextureFormat, semantic : string, pipeline : RaytracingPipelineState) =
        runtime.TraceTo1D(AVal.constant size, format, Sym.ofString semantic, pipeline)

    // ================================================================================================================
    // 2D
    // ================================================================================================================

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of a 2D target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="size">The size of the target texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="semantic">The uniform name of the target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member inline TraceTo2D(runtime : IRaytracingRuntime, size : aval<V2i>, format : TextureFormat, semantic : Symbol, pipeline : RaytracingPipelineState) =
        let size = size |> AVal.mapNonAdaptive (fun s -> V3i(s, 1))
        runtime.TraceTo(size, TextureDimension.Texture2D, format, semantic, pipeline)

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of a 2D target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="size">The size of the target texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="semantic">The uniform name of the target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member inline TraceTo2D(runtime : IRaytracingRuntime, size : aval<V2i>, format : TextureFormat, semantic : string, pipeline : RaytracingPipelineState) =
        runtime.TraceTo2D(size, format, Sym.ofString semantic, pipeline)

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of a 2D target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="size">The size of the target texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="semantic">The uniform name of the target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member inline TraceTo2D(runtime : IRaytracingRuntime, size : V2i, format : TextureFormat, semantic : Symbol, pipeline : RaytracingPipelineState) =
        runtime.TraceTo2D(AVal.constant size, format, semantic, pipeline)

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of a 2D target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="size">The size of the target texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="semantic">The uniform name of the target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member inline TraceTo2D(runtime : IRaytracingRuntime, size : V2i, format : TextureFormat, semantic : string, pipeline : RaytracingPipelineState) =
        runtime.TraceTo2D(AVal.constant size, format, Sym.ofString semantic, pipeline)

    // ================================================================================================================
    // 3D
    // ================================================================================================================

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of a 3D target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="size">The size of the target texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="semantic">The uniform name of the target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member inline TraceTo3D(runtime : IRaytracingRuntime, size : aval<V3i>, format : TextureFormat, semantic : Symbol, pipeline : RaytracingPipelineState) =
        runtime.TraceTo(size, TextureDimension.Texture3D, format, semantic, pipeline)

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of a 3D target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="size">The size of the target texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="semantic">The uniform name of the target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member inline TraceTo3D(runtime : IRaytracingRuntime, size : aval<V3i>, format : TextureFormat, semantic : string, pipeline : RaytracingPipelineState) =
        runtime.TraceTo3D(size, format, Sym.ofString semantic, pipeline)

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of a 3D target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="size">The size of the target texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="semantic">The uniform name of the target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member inline TraceTo3D(runtime : IRaytracingRuntime, size : V3i, format : TextureFormat, semantic : Symbol, pipeline : RaytracingPipelineState) =
        runtime.TraceTo3D(AVal.constant size, format, semantic, pipeline)

    ///<summary>Adaptively runs a raytracing task that dispatches rays based on the size of a 3D target texture.</summary>
    ///<param name="runtime">The runtime.</param>
    ///<param name="size">The size of the target texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="semantic">The uniform name of the target texture.</param>
    ///<param name="pipeline">The pipeline description of the raytracing task.</param>
    [<Extension>]
    static member inline TraceTo3D(runtime : IRaytracingRuntime, size : V3i, format : TextureFormat, semantic : string, pipeline : RaytracingPipelineState) =
        runtime.TraceTo3D(AVal.constant size, format, Sym.ofString semantic, pipeline)