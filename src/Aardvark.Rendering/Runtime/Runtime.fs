namespace Aardvark.Rendering

open System
open Aardvark.Base
open Aardvark.Rendering.Raytracing
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices
open Microsoft.FSharp.Control

/// Determines the detail of debugging information gathered and how it is reported.
type DebugLevel =

    /// No debugging is performed.
    | None = 0

    /// Minimal debugging is performed, errors and warnings from API calls are logged.
    // GL: Enables GL.Check with logging
    // Verbosity: Warning
    | Minimal = 1

    /// More detailed information is logged, an exception is raised when an error occurs.
    // GL: Enables GL.Check with exceptions
    // Verbosity: Information
    | Normal = 2

    /// Full debug information is gathered. This may impact performance significantly.
    // GL: Enables GL.Check with exceptions
    // Vulkan: Enables handle tracing
    // Render tasks: Compiled with debug = true
    // Verbosity: Debug
    | Full = 3

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DebugLevel =
    
    let ofBool (enable : bool) =
        if enable then DebugLevel.Normal else DebugLevel.None

    
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

    abstract member DebugLevel : DebugLevel

    abstract member OnDispose : IEvent<unit>
    abstract member ResourceManager : IResourceManager

    abstract member AssembleModule : FShade.Effect * IFramebufferSignature * IndexedGeometryMode -> FShade.Imperative.Module

    abstract member PrepareSurface : IFramebufferSignature * ISurface -> IBackendSurface
    abstract member DeleteSurface : IBackendSurface -> unit

    abstract member PrepareRenderObject : IFramebufferSignature * IRenderObject -> IPreparedRenderObject

    /// Compiles a render task for clearing a framebuffer with the given values.
    abstract member CompileClear : signature : IFramebufferSignature * values : aval<ClearValues> -> IRenderTask

    /// Compiles a render task for the given render objects.
    abstract member CompileRender : signature : IFramebufferSignature *
                                    objects : aset<IRenderObject> *
                                    debug : bool -> IRenderTask

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
