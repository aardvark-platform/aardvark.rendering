namespace Aardvark.Rendering

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering.Raytracing
open FSharp.Data.Adaptive
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

/// How render objects marked IsTransparent are rendered by CompileRender.
type Transparency =
    /// No transparency pass; transparent objects are rendered like opaque ones.
    | None = 0
    /// The best technique the runtime supports: ABuffer where fragment-shader interlock
    /// is available, WeightedBlended otherwise.
    | Auto = 1
    /// Weighted-blended order-independent transparency: approximate, runs everywhere.
    | WeightedBlended = 2
    /// Exact per-pixel A-buffer. Requires fragment-shader interlock (see
    /// IRuntime.FragmentShaderInterlock); requesting it on a runtime without interlock
    /// throws NotSupportedException.
    | ABuffer = 3

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
    abstract member Name : string with get, set


and IRuntime =
    inherit IFramebufferRuntime
    inherit IComputeRuntime
    inherit IQueryRuntime
    inherit IRaytracingRuntime
    inherit ILodRuntime

    abstract member DebugConfig : IDebugConfig

    /// Returns whether debug labels and markers are enabled.
    abstract member DebugLabelsEnabled : bool

    /// Returns whether fragment-shader pixel interlock is supported (per-pixel
    /// critical sections in the fragment shader: VK_EXT_fragment_shader_interlock
    /// on Vulkan, GL_ARB_fragment_shader_interlock on desktop GL). The exact
    /// A-buffer OIT path needs this; without it the transparency wrapper falls
    /// back from Transparency.Auto to weighted-blended OIT.
    abstract member FragmentShaderInterlock : bool

    abstract member OnDispose : IEvent<unit>

    /// Compiles an effect for the given framebuffer signature and topology.
    /// The topology is ignored unless the signature is layered.
    abstract member PrepareEffect : signature: IFramebufferSignature * effect: FShade.Effect *
                                    [<Optional; DefaultParameterValue(IndexedGeometryMode.TriangleList)>] topology: IndexedGeometryMode -> IBackendSurface

    abstract member PrepareRenderObject : IFramebufferSignature * IRenderObject -> IPreparedRenderObject

    /// Compiles a render task for clearing a framebuffer with the given values.
    abstract member CompileClear : signature : IFramebufferSignature * values : aval<ClearValues> -> IRenderTask

    /// <summary>
    /// Compiles a render task for the given render objects.
    /// </summary>
    /// <param name="signature">The framebuffer signature the task renders to.</param>
    /// <param name="objects">The render objects to render.</param>
    /// <param name="transparency">How render objects marked
    /// <see cref="RenderObject.IsTransparent"/> are rendered. With <see cref="Transparency.None"/>
    /// they are rendered like opaque ones. Default is <see cref="Transparency.Auto"/>.</param>
    abstract member CompileRender : signature : IFramebufferSignature * objects : aset<IRenderObject> *
                                    [<Optional; DefaultParameterValue(Transparency.Auto)>] transparency : Transparency -> IRenderTask

    abstract member CreateGeometryPool : Map<Symbol, Type> -> IGeometryPool

    /// Gets or sets the path of the shader cache.
    abstract member ShaderCachePath : Option<string> with get, set

[<AbstractClass; Sealed; Extension>]
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
    static member Run(t : IRenderTask, output : OutputDescription) =
        t.Run(AdaptiveToken.Top, RenderToken.Empty, output)

    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : IFramebuffer) =
        t.Run(AdaptiveToken.Top, token, OutputDescription.ofFramebuffer fbo)

    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, output : OutputDescription) =
        t.Run(AdaptiveToken.Top, token, output)

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