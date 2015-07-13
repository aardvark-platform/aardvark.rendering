namespace Aardvark.Base

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open System.Collections.Generic
open Aardvark.Base.Rendering

type RenderingResult(f : IFramebuffer, stats : FrameStatistics) =
    member x.Framebuffer = f
    member x.Statistics = stats

type IRenderTask =
    inherit IDisposable
    inherit IAdaptiveObject
    abstract member Run : IFramebuffer -> RenderingResult

type IStreamingTexture =
    inherit IMod<ITexture>
    abstract member Update : format : PixFormat * size : V2i * data : nativeint -> unit
    abstract member ReadPixel : pos : V2i -> C4b

[<Flags>]
type ExecutionEngine =
    | None              = 0x000
    | Native            = 0x001
    | Managed           = 0x002
    | Unmanaged         = 0x004

    | Optimized         = 0x010
    | RuntimeOptimized  = 0x020
    | Unoptimized       = 0x040

    | Default           = 0x011 // Native | Optimized

type IRuntime =

    abstract member CreateTexture : ITexture -> ITexture
    abstract member CreateBuffer : IBuffer -> IBuffer
    abstract member DeleteTexture : ITexture -> unit
    abstract member DeleteBuffer : IBuffer -> unit

    abstract member CreateStreamingTexture : mipMaps : bool -> IStreamingTexture
    abstract member DeleteStreamingTexture : IStreamingTexture -> unit

    abstract member CompileClear : IMod<C4f> * IMod<double> -> IRenderTask
    abstract member CompileRender : ExecutionEngine * aset<RenderJob> -> IRenderTask

    abstract member CreateTexture : IMod<V2i> * IMod<PixFormat> * IMod<int> * IMod<int> -> IFramebufferTexture
    abstract member CreateRenderbuffer : IMod<V2i> * IMod<PixFormat> * IMod<int> -> IFramebufferRenderbuffer

    abstract member CreateFramebuffer : Map<Symbol, IMod<IFramebufferOutput>> -> IFramebuffer


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
