namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System
open System.Runtime.CompilerServices

type IRaytracingTask =
    inherit IDisposable
    abstract member Run : token: AdaptiveToken * query: IQuery -> unit

and IRaytracingRuntime =
    inherit IAccelerationStructureRuntime

    abstract member CompileTrace : pipeline: RaytracingPipelineState * commands: alist<RaytracingCommand> -> IRaytracingTask

and [<RequireQualifiedAccess>] RaytracingCommand =
    | TraceRaysCmd       of raygen : Option<Symbol> * size : V3i    
    | SyncBufferCmd      of buffer : IBackendBuffer * src : Set<ResourceAccess> * dst : Set<ResourceAccess>
    | SyncTextureCmd     of texture : IBackendTexture * src : Set<ResourceAccess> * dst : Set<ResourceAccess>
    | TransformLayoutCmd of texture : IBackendTexture * layout : TextureLayout

    static member TraceRays(raygen : Option<Symbol>, size : int) =
        RaytracingCommand.TraceRaysCmd(raygen, V3i(size, 1, 1))

    static member TraceRays(raygen : Option<Symbol>, size : V2i) =
        RaytracingCommand.TraceRaysCmd(raygen, V3i(size, 1))

    static member TraceRays(raygen : Option<Symbol>, size : V3i) =
        RaytracingCommand.TraceRaysCmd(raygen, size)


    static member TraceRays(raygen : Symbol, size : int) =
        RaytracingCommand.TraceRaysCmd(Some raygen, V3i(size, 1, 1))

    static member TraceRays(raygen : Symbol, size : V2i) =
        RaytracingCommand.TraceRaysCmd(Some raygen, V3i(size, 1))

    static member TraceRays(raygen : Symbol, size : V3i) =
        RaytracingCommand.TraceRaysCmd(Some raygen, size)


    static member TraceRays(size : int) =
        RaytracingCommand.TraceRaysCmd(None, V3i(size, 1, 1))

    static member TraceRays(size : V2i) =
        RaytracingCommand.TraceRaysCmd(None, V3i(size, 1))

    static member TraceRays(size : V3i) =
        RaytracingCommand.TraceRaysCmd(None, size)


    static member TraceRaysToTexture(raygen : Option<Symbol>, texture : IBackendTexture) = [
        RaytracingCommand.TransformLayout(texture, TextureLayout.ShaderWrite)
        RaytracingCommand.TraceRays(raygen, texture.Size)
        RaytracingCommand.TransformLayout(texture, TextureLayout.ShaderRead)
    ]

    static member TraceRaysToTexture(raygen : Symbol, texture : IBackendTexture) =
        RaytracingCommand.TraceRaysToTexture(Some raygen, texture)

    static member TraceRaysToTexture(texture : IBackendTexture) =
        RaytracingCommand.TraceRaysToTexture(None, texture)


    static member TransformLayout(texture : IBackendTexture, layout : TextureLayout) =
        RaytracingCommand.TransformLayoutCmd(texture, layout)

    static member Sync(buffer : IBackendBuffer, src : Set<ResourceAccess>, dst : Set<ResourceAccess>) =
        RaytracingCommand.SyncBufferCmd(buffer, src, dst)

    static member Sync(buffer : IBackendBuffer, src : seq<ResourceAccess>, dst : seq<ResourceAccess>) =
        RaytracingCommand.SyncBufferCmd(buffer, Set.ofSeq src, Set.ofSeq dst)

    static member Sync(buffer : IBackendBuffer, src : ResourceAccess, dst : ResourceAccess) =
        RaytracingCommand.SyncBufferCmd(buffer, Set.singleton src, Set.singleton dst)

    static member Sync(texture : IBackendTexture, src : Set<ResourceAccess>, dst : Set<ResourceAccess>) =
        RaytracingCommand.SyncTextureCmd(texture, src, dst)

    static member Sync(texture : IBackendTexture, src : seq<ResourceAccess>, dst : seq<ResourceAccess>) =
        RaytracingCommand.SyncTextureCmd(texture, Set.ofSeq src, Set.ofSeq dst)

    static member Sync(texture : IBackendTexture, src : ResourceAccess, dst : ResourceAccess) =
        RaytracingCommand.SyncTextureCmd(texture, Set.singleton src, Set.singleton dst)


[<Extension>]
type RaytracingTaskExtensions() =

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