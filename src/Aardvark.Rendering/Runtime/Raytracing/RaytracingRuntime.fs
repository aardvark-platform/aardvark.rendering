namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

type IRaytracingTask =
    inherit IDisposable
    inherit IAdaptiveObject

    /// The name of the task. Can be null.
    abstract member Name : string with get, set

    /// Updates the resources of the task without running it.
    abstract member Update : token: AdaptiveToken * renderToken: RenderToken -> unit

    /// Updates and runs the task.
    abstract member Run : token: AdaptiveToken * renderToken: RenderToken -> unit

and IRaytracingRuntime =
    inherit IAccelerationStructureRuntime
    inherit ITextureRuntime

    /// Returns whether the runtime supports raytracing.
    abstract member SupportsRaytracing : bool

    /// Returns whether the runtime supports fetching the position of a hit in raytracing shaders.
    abstract member SupportsPositionFetch : bool

    /// Returns whether threads can be reordered in raytracing shaders.
    abstract member SupportsInvocationReorder : bool

    /// Returns whether the runtime supports micromap to augment acceleration structures with data at a subtriangle level.
    abstract member SupportsMicromaps : bool

    /// Returns the maximum number of levels of ray recursion allowed in a trace command.
    abstract member MaxRayRecursionDepth : int

    /// Returns the maximum subdivison level allowed for micromaps with the given format.
    abstract member GetMaxMicromapSubdivisionLevel : format: MicromapFormat -> int

    /// Compiles a raytracing task for the given pipeline and commands.
    abstract member CompileTrace : pipeline: RaytracingPipelineState * commands: alist<RaytracingCommand> -> IRaytracingTask

and [<RequireQualifiedAccess>]
    RaytracingCommand =
    | TraceRaysCmd        of size: V3i
    | SyncBufferCmd       of buffer: IBackendBuffer * srcAccess: ResourceAccess * dstAccess: ResourceAccess
    | SyncTextureCmd      of texture: ITextureRange * layout: TextureLayout * srcAccess: ResourceAccess * dstAccess: ResourceAccess
    | TransformLayoutCmd  of texture: ITextureRange * srcLayout: TextureLayout * dstLayout: TextureLayout

    static member inline TraceRays(size : int) =
        RaytracingCommand.TraceRaysCmd (V3i(size, 1, 1))

    static member inline TraceRays(size : V2i) =
        RaytracingCommand.TraceRaysCmd size.XYI

    static member inline TraceRays(size : V3i) =
        RaytracingCommand.TraceRaysCmd size

    static member inline Sync(buffer : IBackendBuffer,
                              [<Optional; DefaultParameterValue(ResourceAccess.All)>] srcAccess : ResourceAccess,
                              [<Optional; DefaultParameterValue(ResourceAccess.All)>] dstAccess : ResourceAccess) =
        RaytracingCommand.SyncBufferCmd(buffer, srcAccess, dstAccess)

    static member inline Sync(buffer : IBuffer<'T>,
                              [<Optional; DefaultParameterValue(ResourceAccess.All)>] srcAccess : ResourceAccess,
                              [<Optional; DefaultParameterValue(ResourceAccess.All)>] dstAccess : ResourceAccess) =
        RaytracingCommand.SyncBufferCmd(buffer.Buffer, srcAccess, dstAccess)

    static member inline Sync(texture : ITextureRange, layout : TextureLayout,
                              [<Optional; DefaultParameterValue(ResourceAccess.All)>] srcAccess : ResourceAccess,
                              [<Optional; DefaultParameterValue(ResourceAccess.All)>] dstAccess : ResourceAccess) =
        RaytracingCommand.SyncTextureCmd(texture, layout, srcAccess, dstAccess)

    static member inline Sync(texture : IBackendTexture, layout : TextureLayout,
                              [<Optional; DefaultParameterValue(ResourceAccess.All)>] srcAccess : ResourceAccess,
                              [<Optional; DefaultParameterValue(ResourceAccess.All)>] dstAccess : ResourceAccess) =
        RaytracingCommand.SyncTextureCmd(texture.[texture.Format.Aspect, *, *], layout, srcAccess, dstAccess)

    static member inline TransformLayout(texture : IBackendTexture, srcLayout : TextureLayout, dstLayout : TextureLayout) =
        RaytracingCommand.TransformLayoutCmd(texture.[texture.Format.Aspect, *, *], srcLayout, dstLayout)

    static member inline TransformLayout(texture : ITextureRange, srcLayout : TextureLayout, dstLayout : TextureLayout) =
        RaytracingCommand.TransformLayoutCmd(texture, srcLayout, dstLayout)

    static member inline TraceRaysToTexture(texture : IBackendTexture,
                                            [<Optional; DefaultParameterValue(TextureLayout.ShaderRead)>] srcLayout : TextureLayout,
                                            [<Optional; DefaultParameterValue(TextureLayout.ShaderRead)>] dstLayout : TextureLayout) =
        [ RaytracingCommand.TransformLayout(texture, srcLayout, TextureLayout.ShaderWrite)
          RaytracingCommand.TraceRays texture.Size
          RaytracingCommand.TransformLayout(texture, TextureLayout.ShaderWrite, dstLayout) ]


[<AbstractClass; Sealed; Extension>]
type RaytracingTaskExtensions() =

    /// Updates the resources of the task without running it.
    [<Extension>]
    static member inline Update(task : IRaytracingTask) =
        task.Update(AdaptiveToken.Top, RenderToken.Empty)

    /// Updates the resources of the task without running it.
    [<Extension>]
    static member inline Update(task : IRaytracingTask, token : AdaptiveToken) =
        task.Update(token, RenderToken.Empty)

    /// Updates and runs the task.
    [<Extension>]
    static member inline Run(task : IRaytracingTask) =
        task.Run(AdaptiveToken.Top, RenderToken.Empty)

    /// Updates and runs the task.
    [<Extension>]
    static member inline Run(task : IRaytracingTask, token : AdaptiveToken) =
        task.Run(token, RenderToken.Empty)

[<Extension>]
type RaytracingRuntimeExtensions() =

    /// Compiles a raytracing task for the given pipeline and commands.
    [<Extension>]
    static member inline CompileTrace(runtime : IRaytracingRuntime, pipeline : RaytracingPipelineState, commands : aval<#seq<RaytracingCommand>>) =
        runtime.CompileTrace(pipeline, commands |> AList.ofAVal)

    /// Compiles a raytracing task for the given pipeline and commands.
    [<Extension>]
    static member inline CompileTrace(runtime : IRaytracingRuntime, pipeline : RaytracingPipelineState, commands : seq<RaytracingCommand>) =
        runtime.CompileTrace(pipeline, commands |> AList.ofSeq)