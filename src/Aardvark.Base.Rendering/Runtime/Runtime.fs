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
    abstract member Run : AdaptiveToken * RenderToken * OutputDescription -> unit
    abstract member FrameId : uint64
    abstract member Use : (unit -> 'a) -> 'a


and IRuntime =
    inherit IFramebufferRuntime
    inherit IComputeRuntime

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

[<Extension>]
type RenderTaskRunExtensions() =
    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : IFramebuffer) =
        t.Run(AdaptiveToken.Top, token, OutputDescription.ofFramebuffer fbo)

    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : OutputDescription) =
        t.Run(AdaptiveToken.Top, token, fbo)
