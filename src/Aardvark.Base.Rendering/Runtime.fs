namespace Aardvark.Base

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open System.Collections.Generic
open Aardvark.Base.Rendering
open System.Runtime.CompilerServices

type IStreamingTexture =
    inherit IMod<ITexture>
    abstract member Update : format : PixFormat * size : V2i * data : nativeint -> unit
    abstract member ReadPixel : pos : V2i -> C4b

type RenderingResult(f : IFramebuffer, stats : FrameStatistics) =
    member x.Framebuffer = f
    member x.Statistics = stats


type IBackendTexture =
    inherit ITexture
    abstract member Samples : int
    abstract member Size : V2i
    abstract member Format : TextureFormat
    abstract member Handle : obj

type IBackendBuffer =
    inherit IBuffer
    abstract member Handle : obj

type IBackendSurface =
    inherit ISurface
    abstract member Handle : obj

type BackendTextureOutputView = { backendTexture : IBackendTexture; level : int; slice : int } with
    interface IFramebufferOutput with
        member x.Samples = x.backendTexture.Samples
        member x.Size = x.backendTexture.Size





type IRenderbuffer =
    inherit IFramebufferOutput
    abstract member Format : RenderbufferFormat
    abstract member Handle : obj

type IPreparedRenderObject =
    inherit IRenderObject
    inherit IDisposable

    abstract member Update : IAdaptiveObject -> unit
    abstract member Original : Option<RenderObject>


type IRuntime =
    abstract member ContextLock : IDisposable


    abstract member CreateTexture : ITexture -> IBackendTexture
    abstract member DeleteTexture : IBackendTexture -> unit

    abstract member CreateSurface : ISurface -> IBackendSurface
    abstract member DeleteSurface : IBackendSurface -> unit

    abstract member CreateBuffer : IBuffer -> IBackendBuffer
    abstract member DeleteBuffer : IBackendBuffer -> unit

    abstract member PrepareRenderObject : IRenderObject -> IPreparedRenderObject



    abstract member CreateRenderbuffer : size : V2i * format : RenderbufferFormat * samples : int -> IRenderbuffer
    abstract member DeleteRenderbuffer : IRenderbuffer -> unit
    
    abstract member CreateFramebuffer : attachments : Map<Symbol, IFramebufferOutput> -> IFramebuffer
    abstract member DeleteFramebuffer : IFramebuffer -> unit
    
    abstract member CreateTexture : size : V2i * format : TextureFormat * levels : int * samples : int * count : int -> IBackendTexture


    abstract member CreateStreamingTexture : mipMaps : bool -> IStreamingTexture
    abstract member DeleteStreamingTexture : IStreamingTexture -> unit

    abstract member CompileClear : clearColor : IMod<Option<C4f>> * clearDepth : IMod<Option<double>> -> IRenderTask
    abstract member CompileRender : BackendConfiguration * aset<IRenderObject> -> IRenderTask
    
//    abstract member ResolveMultisamples : ms : IRenderbuffer * ss : ITexture * trafo : ImageTrafo -> unit
//    abstract member CopyTexture : source : ITexture * target : ITexture * trafo : ImageTrafo -> unit



    [<Obsolete("use non-adaptive overload instead")>]
    abstract member CreateTexture : IMod<ITexture> -> IMod<ITexture>

    [<Obsolete("use non-adaptive overload instead")>]
    abstract member CreateBuffer : IMod<IBuffer> -> IMod<IBuffer>

    [<Obsolete("use non-adaptive overload instead")>]
    abstract member DeleteTexture : IMod<ITexture> -> unit

    [<Obsolete("use non-adaptive overload instead")>]
    abstract member DeleteBuffer : IMod<IBuffer> -> unit


    [<Obsolete("use non-adaptive overload instead")>]
    abstract member CreateTexture : size : IMod<V2i> * format : IMod<TextureFormat> * samples : IMod<int> * count : IMod<int> -> IFramebufferTexture
    
    [<Obsolete("use non-adaptive overload instead")>]
    abstract member CreateRenderbuffer : size : IMod<V2i> * format : IMod<RenderbufferFormat> * samples : IMod<int> -> IFramebufferRenderbuffer
    
    [<Obsolete("use non-adaptive overload instead")>]
    abstract member CreateFramebuffer : attachments : Map<Symbol, IMod<IFramebufferOutput>> -> IFramebuffer

    [<Obsolete("use non-adaptive overload instead")>]
    abstract member ResolveMultisamples : ms : IFramebufferRenderbuffer * ss : IFramebufferTexture * trafo : ImageTrafo -> unit

and IRenderTask =
    inherit IDisposable
    inherit IAdaptiveObject
    abstract member Runtime : Option<IRuntime>
    abstract member Run : IAdaptiveObject * IFramebuffer -> RenderingResult
    abstract member FrameId : uint64

[<Extension>]
type RenderTaskRunExtensions() =
    [<Extension>]
    static member Run(t : IRenderTask, fbo : IFramebuffer) =
        t.Run(null, fbo)

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


type RenderToFramebufferMod(task : IRenderTask, fbo : IMod<IFramebuffer>) =
    inherit Mod.AbstractMod<RenderingResult>()

    member x.Task = task
    member x.Framebuffer = fbo

    override x.Inputs =
        seq {
            yield task :> _
            yield fbo :> _
        }

    override x.Compute() =
        let handle = fbo.GetValue x
        task.Run(x, handle)

type RenderingResultMod(res : RenderToFramebufferMod, semantic : Symbol) =
    inherit Mod.AbstractMod<ITexture>()
    let mutable lastStats = FrameStatistics.Zero

    member x.LastStatistics = lastStats
    member x.Task = res.Task
    member x.Framebuffer = res.Framebuffer
    member x.Semantic = semantic
    member x.Inner = res

    override x.Inputs = Seq.singleton (res :> _)

    override x.Compute() =
        lock res (fun () ->
            let wasOutDated = res.OutOfDate
            let res = res.GetValue x
            if wasOutDated then
                lastStats <- res.Statistics
            else
                lastStats <- FrameStatistics.Zero
                    
            match Map.tryFind semantic res.Framebuffer.Attachments with
                | Some o ->
                    match o with
                        | :? BackendTextureOutputView as o ->
                            o.backendTexture :> ITexture
                        | _ ->
                            failwithf "unexpected output: %A" o
                | None ->
                    failwithf "could not get output: %A" semantic
        )



[<AutoOpen>]
module NullResources =

    let isNullResource (obj : obj) =
        match obj with 
         | :? NullBuffer -> true
         | :? NullTexture -> true
         | _ -> false
         
    let isValidResourceAdaptive (m : IMod) =
      [m :> IAdaptiveObject] |> Mod.mapCustom (fun s ->
            not <| isNullResource (m.GetValue s)
      ) 