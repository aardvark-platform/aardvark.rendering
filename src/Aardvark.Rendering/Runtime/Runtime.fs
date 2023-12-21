namespace Aardvark.Rendering

open System
open System.Threading
open Aardvark.Base
open Aardvark.Rendering.Raytracing
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices
open Microsoft.FSharp.Control

/// Interface for backend-specific debug configurations.
type IDebugConfig =
    interface end

/// Predefined debug configurations based on a level.
/// Levels are interpreted by the backend to select appropriate backend-specific debug settings.
[<RequireQualifiedAccess>]
type DebugLevel =

    /// No debugging is performed.
    | None

    /// Minimal debugging is performed, errors and warnings from API calls are logged.
    | Minimal

    /// More detailed information is logged, an exception is raised when an error occurs.
    | Normal

    /// Full debug information is gathered. This may impact performance significantly.
    | Full

    interface IDebugConfig

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DebugLevel =

    let ofBool (enable : bool) =
        if enable then DebugLevel.Normal else DebugLevel.None


/// Unique ID for render tasks.
[<Struct; StructuredFormatDisplay("{AsString}")>]
type RenderTaskId private (value : int) =
    static let mutable currentId = 0

    static member New() = RenderTaskId(Interlocked.Increment(&currentId))
    static member op_Explicit(id : RenderTaskId) = id.Value

    member private x.Value = value
    member private x.AsString = x.ToString()
    override x.ToString() = string value

type IRenderTask =
    inherit IDisposable
    inherit IAdaptiveObject
    abstract member Id : RenderTaskId
    abstract member FramebufferSignature : Option<IFramebufferSignature>
    abstract member Runtime : Option<IRuntime>
    abstract member Update : AdaptiveToken * RenderToken -> unit
    abstract member Run : AdaptiveToken * RenderToken * OutputDescription -> unit
    abstract member FrameId : uint64
    abstract member Use : (unit -> 'a) -> 'a


and IRuntime =
    inherit IFramebufferRuntime
    inherit IComputeRuntime
    inherit IQueryRuntime
    inherit IRaytracingRuntime
    inherit ILodRuntime

    abstract member DebugConfig : IDebugConfig

    abstract member OnDispose : IEvent<unit>

    [<Obsolete("To be removed.")>]
    abstract member ResourceManager : IResourceManager

    abstract member AssembleModule : FShade.Effect * IFramebufferSignature * IndexedGeometryMode -> FShade.Imperative.Module

    abstract member PrepareSurface : IFramebufferSignature * ISurface -> IBackendSurface

    abstract member PrepareRenderObject : IFramebufferSignature * IRenderObject -> IPreparedRenderObject

    /// Compiles a render task for clearing a framebuffer with the given values.
    abstract member CompileClear : signature : IFramebufferSignature * values : aval<ClearValues> -> IRenderTask

    /// Compiles a render task for the given render objects.
    abstract member CompileRender : signature : IFramebufferSignature * objects : aset<IRenderObject> -> IRenderTask

    abstract member CreateGeometryPool : Map<Symbol, Type> -> IGeometryPool

    /// Gets or sets the path of the shader cache.
    abstract member ShaderCachePath : Option<string> with get, set

[<Extension>]
type RenderTaskRunExtensions() =

    [<Extension>]
    static member Update(t : IRenderTask) =
        t.Update(AdaptiveToken.Top, RenderToken.Empty)

    [<Extension>]
    static member Update(t : IRenderTask, token : AdaptiveToken) =
        t.Update(token, RenderToken.Empty)

    [<Extension>]
    static member Run(t : IRenderTask, token : AdaptiveToken, renderToken : RenderToken, fbo : IFramebuffer) =
        t.Run(token, renderToken, OutputDescription.ofFramebuffer fbo)

    [<Extension>]
    static member Run(t : IRenderTask, fbo : IFramebuffer) =
        t.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer fbo)

    [<Extension>]
    static member Run(t : IRenderTask, fbo : OutputDescription) =
        t.Run(AdaptiveToken.Top, RenderToken.Empty, fbo)

    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : IFramebuffer) =
        t.Run(AdaptiveToken.Top, token, OutputDescription.ofFramebuffer fbo)

    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : OutputDescription) =
        t.Run(AdaptiveToken.Top, token, fbo)

    [<Extension>]
    static member inline GetRuntime(t : IRenderTask) =
        match t.Runtime with
        | Some r -> r
        | _ -> raise <| InvalidOperationException("Render task does not have a runtime.")

    [<Extension>]
    static member inline GetFramebufferSignature(t : IRenderTask) =
        match t.FramebufferSignature with
        | Some s -> s
        | _ -> raise <| InvalidOperationException("Render task does not have a framebuffer signature.")