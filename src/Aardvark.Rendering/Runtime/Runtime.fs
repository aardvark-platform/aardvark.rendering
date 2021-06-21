namespace Aardvark.Rendering

open System
open Aardvark.Base
open Aardvark.Rendering.Raytracing
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
    abstract member Run : AdaptiveToken * RenderToken * OutputDescription * IQuery -> unit
    abstract member FrameId : uint64
    abstract member Use : (unit -> 'a) -> 'a


and IRuntime =
    inherit IFramebufferRuntime
    inherit IComputeRuntime
    inherit IQueryRuntime
    inherit IRaytracingRuntime

    abstract member OnDispose : IEvent<unit>
    abstract member ResourceManager : IResourceManager

    abstract member AssembleModule : FShade.Effect * IFramebufferSignature * IndexedGeometryMode -> FShade.Imperative.Module

    abstract member PrepareSurface : IFramebufferSignature * ISurface -> IBackendSurface
    abstract member DeleteSurface : IBackendSurface -> unit

    abstract member PrepareRenderObject : IFramebufferSignature * IRenderObject -> IPreparedRenderObject

    /// Clears the given color attachments, and (optionally) the depth and stencil attachments to the specified values.
    abstract member CompileClear : fboSignature : IFramebufferSignature * clearColors : aval<Map<Symbol, C4f>> * clearDepth : aval<float option> * clearStencil : aval<int option> -> IRenderTask

    /// Compiles a render task for the given render objects.
    abstract member CompileRender : fboSignature : IFramebufferSignature * BackendConfiguration * aset<IRenderObject> -> IRenderTask

    abstract member CreateGeometryPool : Map<Symbol, Type> -> IGeometryPool

    /// Gets or sets the path of the shader cache.
    abstract member ShaderCachePath : Option<string> with get, set

[<Extension>]
type RenderTaskRunExtensions() =
    // Overloads with queries
    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : IFramebuffer, queries : IQuery) =
        t.Run(AdaptiveToken.Top, token, OutputDescription.ofFramebuffer fbo, queries)

    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : OutputDescription, queries : IQuery) =
        t.Run(AdaptiveToken.Top, token, fbo, queries)

    // Overloads without queries
    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : IFramebuffer) =
        t.Run(AdaptiveToken.Top, token, OutputDescription.ofFramebuffer fbo, Queries.none)

    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : OutputDescription) =
        t.Run(AdaptiveToken.Top, token, fbo, Queries.none)

    [<Extension>]
    static member Run(t : IRenderTask, token : AdaptiveToken, renderToken : RenderToken, fbo : OutputDescription) =
        t.Run(token, renderToken, fbo, Queries.none)
