namespace Aardvark.Base

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open System.Collections.Generic
open Aardvark.Base.Rendering

type IStreamingTexture =
    inherit IMod<ITexture>
    abstract member Update : format : PixFormat * size : V2i * data : nativeint -> unit
    abstract member ReadPixel : pos : V2i -> C4b

type RenderingResult(f : IFramebuffer, stats : FrameStatistics) =
    member x.Framebuffer = f
    member x.Statistics = stats


type IBackendTexture =
    inherit ITexture
    abstract member Handle : obj

type IBackendBuffer =
    inherit IBuffer
    abstract member Handle : obj

type IBackendSurface =
    inherit ISurface
    abstract member Handle : obj


type IRuntime =
    abstract member ContextLock : IDisposable

    abstract member CreateTexture : ITexture -> IBackendTexture
    abstract member CreateBuffer : IBuffer -> IBackendBuffer
    abstract member CreateSurface : ISurface -> IBackendSurface
    abstract member DeleteTexture : IBackendTexture -> unit
    abstract member DeleteBuffer : IBackendBuffer -> unit
    abstract member DeleteSurface : IBackendSurface -> unit

    abstract member CreateTexture : IMod<ITexture> -> IMod<ITexture>
    abstract member CreateBuffer : IMod<IBuffer> -> IMod<IBuffer>
    abstract member DeleteTexture : IMod<ITexture> -> unit
    abstract member DeleteBuffer : IMod<IBuffer> -> unit


    abstract member CreateStreamingTexture : mipMaps : bool -> IStreamingTexture
    abstract member DeleteStreamingTexture : IStreamingTexture -> unit

    abstract member CompileClear : clearColor : IMod<C4f> * clearDepth : IMod<double> -> IRenderTask
    abstract member CompileRender : BackendConfiguration * aset<IRenderObject> -> IRenderTask

    abstract member CreateTexture : size : IMod<V2i> * format : IMod<PixFormat> * samples : IMod<int> * count : IMod<int> -> IFramebufferTexture
    abstract member CreateRenderbuffer : size : IMod<V2i> * format : IMod<RenderbufferFormat> * samples : IMod<int> -> IFramebufferRenderbuffer
    abstract member CreateFramebuffer : attachments : Map<Symbol, IMod<IFramebufferOutput>> -> IFramebuffer

    abstract member ResolveMultisamples : ms : IFramebufferRenderbuffer * ss : IFramebufferTexture * trafo : ImageTrafo -> unit

and IRenderTask =
    inherit IDisposable
    inherit IAdaptiveObject
    abstract member Runtime : Option<IRuntime>
    abstract member Run : IFramebuffer -> RenderingResult

type ShaderStage =
    | Vertex = 1
    | TessControl = 2
    | TessEval = 3
    | Geometry = 4
    | Pixel = 5



type BackendSurface(code : string, entryPoints : Dictionary<ShaderStage, string>, uniforms : SymbolDict<IMod>, samplerStates : SymbolDict<SamplerStateDescription>, semanticMap : SymbolDict<Symbol>) =
    interface ISurface
    member x.Code = code
    member x.EntryPoints = entryPoints
    member x.Uniforms = uniforms
    member x.SamplerStates = samplerStates
    member x.SemanticMap = semanticMap

    new(code, entryPoints) = BackendSurface(code, entryPoints, SymDict.empty, SymDict.empty, SymDict.empty)
    new(code, entryPoints, uniforms) = BackendSurface(code, entryPoints, uniforms, SymDict.empty, SymDict.empty)
    new(code, entryPoints, uniforms, samplerStates) = BackendSurface(code, entryPoints, uniforms, samplerStates, SymDict.empty)

type IGeneratedSurface =
    inherit ISurface

    abstract member Generate : IRuntime -> BackendSurface
