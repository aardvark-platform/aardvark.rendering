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

[<Flags>]
type ExecutionEngine =
    | None              = 0x000
    | Native            = 0x001
    | Managed           = 0x002
    | Unmanaged         = 0x004

    | Optimized         = 0x010
    | RuntimeOptimized  = 0x020
    | Unoptimized       = 0x040

    | Debug             = 0x100

    | Default           = 0x011 // Native | Optimized

module BackendConfig =
    type ExecutionEngine =
        | Debug = 0
        | Managed = 1
        | Unmanaged = 2
        | Native = 3

    type RedundancyRemoval =
        | None = 0
        | Runtime = 1
        | Static = 2

    [<Flags>]
    type ResourceSharing =
        | None      = 0x00
        | Buffers   = 0x01
        | Textures  = 0x02
        | Full      = 0x03

    type Sorting =
        | Dynamic of cmp : IComparer<RenderJob>
        | Static of cmp : IComparer<RenderJob>
        | Grouping of projections : (list<RenderJob -> IAdaptiveObject>)

    type Config = { 
        execution : ExecutionEngine
        redundancy : RedundancyRemoval
        sharing : ResourceSharing
        sorting : Sorting
    }

    module Projections =
        let private empty = Mod.init () :> IAdaptiveObject

        let surface (rj : RenderJob) =
            rj.Surface :> IAdaptiveObject

        let diffuseTexture (rj : RenderJob) =
            match rj.Uniforms.TryGetUniform (rj.AttributeScope, DefaultSemantic.DiffuseColorCoordinates) with
                | Some t -> t :> IAdaptiveObject
                | _ -> empty

        let indices (rj : RenderJob) =
            match rj.Indices with
                | null -> empty
                | i -> i :> IAdaptiveObject

        let standard = [ surface; diffuseTexture; indices ]

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Config =

        let native = 
            { 
                execution       = ExecutionEngine.Native
                redundancy      = RedundancyRemoval.Static
                sharing         = ResourceSharing.Textures
                sorting         = Sorting.Grouping Projections.standard 
            }

        let runtime = 
            { 
                execution       = ExecutionEngine.Unmanaged
                redundancy      = RedundancyRemoval.Runtime
                sharing         = ResourceSharing.Textures
                sorting         = Sorting.Grouping Projections.standard 
            }

        let managed = 
            { 
                execution       = ExecutionEngine.Managed
                redundancy      = RedundancyRemoval.Static
                sharing         = ResourceSharing.Textures
                sorting         = Sorting.Grouping Projections.standard 
            }

type RenderingResult(f : IFramebuffer, stats : FrameStatistics) =
    member x.Framebuffer = f
    member x.Statistics = stats

type IRuntime =
    abstract member ContextLock : IDisposable

    abstract member CreateTexture : ITexture -> ITexture
    abstract member CreateBuffer : IBuffer -> IBuffer
    abstract member DeleteTexture : ITexture -> unit
    abstract member DeleteBuffer : IBuffer -> unit

    abstract member CreateStreamingTexture : mipMaps : bool -> IStreamingTexture
    abstract member DeleteStreamingTexture : IStreamingTexture -> unit

    abstract member CompileClear : clearColor : IMod<C4f> * clearDepth : IMod<double> -> IRenderTask
    abstract member CompileRender : ExecutionEngine * aset<RenderJob> -> IRenderTask

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
