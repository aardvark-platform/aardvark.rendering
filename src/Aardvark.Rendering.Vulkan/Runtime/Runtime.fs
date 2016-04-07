namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering
open System.Collections.Generic
open System.Threading
open Aardvark.Rendering.Vulkan
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.Runtime


type Runtime(device : Device) as this =
    inherit Resource(device)

    let context = new Context(device)
    let manager = new ResourceManager(this, context)

    override x.Release() =
        manager.Dispose()
        context.Dispose()


    member x.CompileRender(fbo : IFramebufferSignature, config : BackendConfiguration, renderObjects : aset<IRenderObject>) =
        new RenderTask(manager, unbox fbo, renderObjects, config) :> IRenderTask

    member x.CompileClear(fbo : IFramebufferSignature, clearColors : IMod<Map<Symbol, C4f>>, clearDepth : IMod<Option<double>>) =
        let colors = 
            fbo.ColorAttachments
                |> Map.toSeq
                |> Seq.map (fun (b, (sem,att)) ->
                    adaptive {
                        let! clearColors = clearColors
                        match Map.tryFind sem clearColors with
                            | Some cc -> return cc
                            | _ -> return C4f.Black
                    }
                   )
                |> Seq.toList

        new ClearTask( manager, unbox fbo, colors, clearDepth, None ) :> IRenderTask

    member x.PrepareRenderObject(fboSig, ro) = 
        manager.PrepareRenderObject(unbox fboSig, ro) :> IPreparedRenderObject


    interface IRuntime with
        member x.PrepareRenderObject(fboSig, ro) = x.PrepareRenderObject(fboSig, ro)
        member x.CompileClear(fboSig, col, depth) = x.CompileClear(fboSig, col, depth)
        member x.CompileRender(fboSig, config, ros) = x.CompileRender(fboSig, config, ros)


        member x.ContextLock = { new IDisposable with member x.Dispose() = () }
        member x.CreateFramebufferSignature(signature) = failf "not implemented"
        member x.DeleteFramebufferSignature(signature) = failf "not implemented"

        member x.CreateMappedBuffer() = failf "not implemented"
        member x.CreateTexture(a,b,c,d,e) = failf "not implemented"
        member x.CreateTextureCube(a,b,c,d) = failf "not implemented"
        member x.CreateRenderbuffer(a,c,d) = failf "not implemented"
        member x.PrepareTexture (t : ITexture) : IBackendTexture = failf "not implemented"
        member x.PrepareBuffer (b : IBuffer) : IBackendBuffer = failf "not implemented"
        member x.PrepareSurface (fboSignature : IFramebufferSignature, s : ISurface) = failf "not implemented"


        member x.DeleteTexture (t : IBackendTexture)  = ()
        member x.DeleteBuffer (b : IBackendBuffer) = ()
        member x.DeleteSurface (s : IBackendSurface) = ()
        member x.DeleteRenderbuffer (rb : IRenderbuffer) = ()


        member x.CreateFramebuffer (_,_) = failf "not implemented"
        member x.DeleteFramebuffer(fbo) = ()

        member x.GenerateMipMaps(t : IBackendTexture) = ()
        member x.Download(tex : IBackendTexture, slice : int, level : int, target : PixImage) = ()
        member x.Upload(a,b,c,d) = ()

        member x.CreateStreamingTexture _ = failf "not implemented"
        member x.DeleteStreamingTexture _ = failf "not implemented"


        member x.ResolveMultisamples(_,_,_) = failf "not implemented"
