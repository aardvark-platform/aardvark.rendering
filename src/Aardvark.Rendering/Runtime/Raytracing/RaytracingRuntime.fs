namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System
open System.Runtime.CompilerServices

type IRaytracingTask =
    inherit IDisposable
    inherit IAdaptiveObject
    abstract member Update : token: AdaptiveToken * query: IQuery -> unit
    abstract member Run : token: AdaptiveToken * query: IQuery -> unit

and IRaytracingRuntime =
    inherit IAccelerationStructureRuntime

    /// Returns whether the runtime supports raytracing.
    abstract member SupportsRaytracing : bool

    /// Returns the maximum number of levels of ray recursion allowed in a trace command.
    abstract member MaxRayRecursionDepth : int

    /// Compiles a raytracing task for the given pipeline and commands.
    abstract member CompileTrace : pipeline: RaytracingPipelineState * commands: alist<RaytracingCommand> -> IRaytracingTask

and [<RequireQualifiedAccess>] RaytracingCommand =
    | TraceRaysCmd       of size: V3i
    | SyncBufferCmd      of buffer: IBackendBuffer * src: ResourceAccess * dst: ResourceAccess
    | SyncTextureCmd     of texture: IBackendTexture * src: ResourceAccess * dst: ResourceAccess
    | TransformLayoutCmd of texture: IBackendTexture * src: TextureLayout * dst: TextureLayout

    static member TraceRays(size : int) =
        RaytracingCommand.TraceRaysCmd(V3i(size, 1, 1))

    static member TraceRays(size : V2i) =
        RaytracingCommand.TraceRaysCmd(V3i(size, 1))

    static member TraceRays(size : V3i) =
        RaytracingCommand.TraceRaysCmd(size)

    static member TraceRaysToTexture(texture : IBackendTexture) = [
        RaytracingCommand.TransformLayout(texture, TextureLayout.ShaderRead, TextureLayout.ShaderWrite)
        RaytracingCommand.TraceRays(texture.Size)
        RaytracingCommand.TransformLayout(texture, TextureLayout.ShaderWrite, TextureLayout.ShaderRead)
    ]

    static member TransformLayout(texture : IBackendTexture, src : TextureLayout, dst : TextureLayout) =
        RaytracingCommand.TransformLayoutCmd(texture, src, dst)


[<Extension>]
type RaytracingTaskExtensions() =

    [<Extension>]
    static member Update(this : IRaytracingTask) =
        this.Update(AdaptiveToken.Top, Queries.none)

    [<Extension>]
    static member Update(this : IRaytracingTask, token : AdaptiveToken) =
        this.Update(token, Queries.none)

    [<Extension>]
    static member Run(this : IRaytracingTask) =
        this.Run(AdaptiveToken.Top, Queries.none)

    [<Extension>]
    static member Run(this : IRaytracingTask, token : AdaptiveToken) =
        this.Run(token, Queries.none)

[<Extension>]
type RaytracingRuntimeExtensions() =

    [<Extension>]
    static member CompileTrace(this : IRaytracingRuntime, pipeline: RaytracingPipelineState, commands: aval<RaytracingCommand list>) =
        this.CompileTrace(pipeline, commands |> AList.ofAVal)

    [<Extension>]
    static member CompileTrace(this : IRaytracingRuntime, pipeline: RaytracingPipelineState, commands: List<RaytracingCommand>) =
        this.CompileTrace(pipeline, commands |> AList.ofList)

    [<Extension>]
    static member CompileTraceToTexture(this : IRaytracingRuntime, pipeline: RaytracingPipelineState, target: aval<IBackendTexture>) =
        let commands = target |> AVal.map RaytracingCommand.TraceRaysToTexture
        this.CompileTrace(pipeline, commands |> AList.ofAVal)

    [<Extension>]
    static member CompileTraceToTexture(this : IRaytracingRuntime, pipeline: RaytracingPipelineState, target: IBackendTexture) =
        let commands = target |> RaytracingCommand.TraceRaysToTexture
        this.CompileTrace(pipeline, commands |> AList.ofList)