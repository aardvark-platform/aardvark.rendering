namespace Aardvark.Base

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices
open Microsoft.FSharp.Control

type IRenderTask =
    inherit IDisposable
    inherit IAdaptiveObject
    abstract member Id : int
    abstract member FramebufferSignature : Option<IFramebufferSignature>
    abstract member Runtime : Option<IRuntime>
    abstract member Update : AdaptiveToken * RenderToken -> unit
    abstract member Run : AdaptiveToken * RenderToken * OutputDescription * TaskSync * IQuery -> unit
    abstract member FrameId : uint64
    abstract member Use : (unit -> 'a) -> 'a


and IRuntime =
    inherit IFramebufferRuntime
    inherit IComputeRuntime
    inherit IQueryRuntime

    abstract member OnDispose : IEvent<unit>
    abstract member ResourceManager : IResourceManager

    abstract member AssembleEffect : FShade.Effect * IFramebufferSignature * IndexedGeometryMode -> BackendSurface
    abstract member AssembleModule : FShade.Effect * IFramebufferSignature * IndexedGeometryMode -> FShade.Imperative.Module

    abstract member PrepareSurface : IFramebufferSignature * ISurface -> IBackendSurface
    abstract member DeleteSurface : IBackendSurface -> unit

    abstract member PrepareRenderObject : IFramebufferSignature * IRenderObject -> IPreparedRenderObject

    abstract member CompileClear : fboSignature : IFramebufferSignature * clearColors : aval<Map<Symbol, C4f>> * clearDepth : aval<Option<double>> -> IRenderTask
    abstract member CompileRender : fboSignature : IFramebufferSignature * BackendConfiguration * aset<IRenderObject> -> IRenderTask

    abstract member CreateGeometryPool : Map<Symbol, Type> -> IGeometryPool

    /// Creates a sync object.
    /// maxDeviceWaits determines how many device operations can wait for the sync object concurrently.
    abstract member CreateSync : maxDeviceWaits : int -> ISync

[<Extension>]
type RenderTaskRunExtensions() =

    [<Extension>]
    static member Run(t : IRenderTask, token : AdaptiveToken, renderToken : RenderToken, fbo : OutputDescription, queries : IQuery) =
        t.Run(token, renderToken, fbo, TaskSync.none, queries)

    // Overloads with queries
    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : IFramebuffer, sync : TaskSync, queries : IQuery) =
        t.Run(AdaptiveToken.Top, token, OutputDescription.ofFramebuffer fbo, sync, queries)

    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : OutputDescription, sync : TaskSync, queries : IQuery) =
        t.Run(AdaptiveToken.Top, token, fbo, sync, queries)

    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : IFramebuffer, queries : IQuery) =
        t.Run(AdaptiveToken.Top, token, OutputDescription.ofFramebuffer fbo, queries)

    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : OutputDescription, queries : IQuery) =
        t.Run(AdaptiveToken.Top, token, fbo, queries)

    // Overloads without queries
    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : IFramebuffer, sync : TaskSync) =
        t.Run(AdaptiveToken.Top, token, OutputDescription.ofFramebuffer fbo, sync, Queries.empty)

    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : OutputDescription, sync : TaskSync) =
        t.Run(AdaptiveToken.Top, token, fbo, sync, Queries.empty)

    [<Extension>]
    static member Run(t : IRenderTask, token : AdaptiveToken, renderToken : RenderToken, fbo : OutputDescription, sync : TaskSync) =
        t.Run(token, renderToken, fbo, sync, Queries.empty)

    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : IFramebuffer) =
        t.Run(AdaptiveToken.Top, token, OutputDescription.ofFramebuffer fbo, Queries.empty)

    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : OutputDescription) =
        t.Run(AdaptiveToken.Top, token, fbo, Queries.empty)

    [<Extension>]
    static member Run(t : IRenderTask, token : AdaptiveToken, renderToken : RenderToken, fbo : OutputDescription) =
        t.Run(token, renderToken, fbo, Queries.empty)