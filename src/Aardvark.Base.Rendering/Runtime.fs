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



type IBackendBuffer =
    inherit IBuffer
    abstract member Handle : obj

type IBackendSurface =
    inherit ISurface
    abstract member Handle : obj
    abstract member UniformGetters : SymbolDict<obj>
    abstract member SamplerStates : SymbolDict<SamplerStateDescription>
    abstract member Inputs : list<string * Type>
    abstract member Outputs : list<string * Type>
    abstract member Uniforms : list<string * Type>

type IPreparedRenderObject =
    inherit IRenderObject
    inherit IDisposable

    abstract member Update : IAdaptiveObject -> unit
    abstract member Original : Option<RenderObject>


type IRuntime =
    abstract member ContextLock : IDisposable

    abstract member CreateFramebufferSignature : attachments : SymbolDict<AttachmentSignature> -> IFramebufferSignature
    abstract member DeleteFramebufferSignature : IFramebufferSignature -> unit


    abstract member PrepareBuffer : IBuffer -> IBackendBuffer
    abstract member PrepareTexture : ITexture -> IBackendTexture
    abstract member PrepareSurface : IFramebufferSignature * ISurface -> IBackendSurface
    abstract member PrepareRenderObject : IFramebufferSignature * IRenderObject -> IPreparedRenderObject


    abstract member DeleteBuffer : IBackendBuffer -> unit
    abstract member DeleteTexture : IBackendTexture -> unit
    abstract member DeleteSurface : IBackendSurface -> unit



    abstract member CreateStreamingTexture : mipMaps : bool -> IStreamingTexture
    abstract member CreateTexture : size : V2i * format : TextureFormat * levels : int * samples : int * count : int -> IBackendTexture
    abstract member CreateRenderbuffer : size : V2i * format : RenderbufferFormat * samples : int -> IRenderbuffer
    abstract member CreateFramebuffer : signature : IFramebufferSignature * attachments : Map<Symbol, IFramebufferOutput> -> IFramebuffer

    abstract member DeleteStreamingTexture : IStreamingTexture -> unit
    abstract member DeleteRenderbuffer : IRenderbuffer -> unit
    abstract member DeleteFramebuffer : IFramebuffer -> unit

    abstract member CompileClear : fboSignature : IFramebufferSignature * clearColors : IMod<Map<Symbol, C4f>> * clearDepth : IMod<Option<double>> -> IRenderTask
    abstract member CompileRender : fboSignature : IFramebufferSignature * BackendConfiguration * aset<IRenderObject> -> IRenderTask
    
    abstract member GenerateMipMaps : IBackendTexture -> unit
    abstract member ResolveMultisamples : IFramebufferOutput * IBackendTexture * ImageTrafo -> unit
    abstract member Upload : texture : IBackendTexture * level : int * slice : int * source : PixImage -> unit
    abstract member Download : texture : IBackendTexture * level : int * slice : int * target : PixImage -> unit

and IRenderTask =
    inherit IDisposable
    inherit IAdaptiveObject
    abstract member FramebufferSignature : IFramebufferSignature
    abstract member Runtime : Option<IRuntime>
    abstract member Run : IAdaptiveObject * IFramebuffer -> RenderingResult
    abstract member FrameId : uint64

and [<AllowNullLiteral>] IFramebufferSignature =
    abstract member Runtime : IRuntime
    abstract member ColorAttachments : Map<int, Symbol * AttachmentSignature>
    abstract member DepthStencilAttachment : Option<AttachmentSignature>

    abstract member IsAssignableFrom : other : IFramebufferSignature -> bool


and IFramebuffer =
    inherit IDisposable
    abstract member Signature : IFramebufferSignature
    abstract member Size : V2i
    abstract member GetHandle : IAdaptiveObject -> obj
    abstract member Attachments : Map<Symbol, IFramebufferOutput>

and RenderingResult(f : IFramebuffer, stats : FrameStatistics) =
    member x.Framebuffer = f
    member x.Statistics = stats

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

    abstract member Generate : IRuntime * IFramebufferSignature -> BackendSurface


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
                            o.texture :> ITexture
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