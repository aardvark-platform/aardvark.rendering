namespace Aardvark.Rendering.GL

open System
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base.Incremental

type FramebufferSignature(colors : Map<int, Symbol * AttachmentSignature>, depthStencil : Option<AttachmentSignature>) =
   
    member x.ColorAttachments = colors
    member x.DepthStencilAttachment = depthStencil

    interface IFramebufferSignature with
        member x.ColorAttachments = colors
        member x.DepthStencilAttachment = depthStencil



type Runtime(ctx : Context, shareTextures : bool, shareBuffers : bool) =

    static let versionRx = System.Text.RegularExpressions.Regex @"([0-9]+\.)*[0-9]+"

    let mutable ctx = ctx
    let mutable manager = if ctx <> null then ResourceManager(ctx, shareTextures, shareBuffers) else null

    new(ctx) = new Runtime(ctx, false, false)

    member x.SupportsUniformBuffers =
        ExecutionContext.uniformBuffersSupported

    member x.Context
        with get() = ctx
        and set c = 
            ctx <- c
            manager <- ResourceManager(ctx, shareTextures, shareBuffers)
            //compiler <- Compiler.Compiler(x, c)
            //currentRuntime <- Some (x :> IRuntime)


    member x.Dispose() = 
        if ctx <> null then
            ctx.Dispose()
            ctx <- null
        

    interface IDisposable with
        member x.Dispose() = x.Dispose() 

    interface IRuntime with

        member x.CreateFramebufferSignature(attachments : SymbolDict<AttachmentSignature>) =
            x.CreateFramebufferSignature(attachments) :> IFramebufferSignature

        member x.DeleteFramebufferSignature(signature : IFramebufferSignature) =
            ()

        member x.Download(t : IBackendTexture, level : int, slice : int, target : PixImage) = x.Download(t, level, slice, target)
        member x.Upload(t : IBackendTexture, level : int, slice : int, source : PixImage) = x.Upload(t, level, slice, source)
  
        member x.ResolveMultisamples(source, target, trafo) = x.ResolveMultisamples(source, target, trafo)
        member x.GenerateMipMaps(t : IBackendTexture) = x.GenerateMipMaps t
        member x.ContextLock = ctx.ResourceLock
        member x.CompileRender (signature, engine : BackendConfiguration, set : aset<IRenderObject>) = x.CompileRender(signature, engine,set)
        member x.CompileClear(signature, color, depth) = x.CompileClear(signature, color, depth)
      
        member x.PrepareSurface (signature, s : ISurface) = x.PrepareSurface(signature, s) :> IBackendSurface
        member x.DeleteSurface (s : IBackendSurface) = 
            match s with
                | :? Program as p -> x.DeleteSurface p
                | _ -> failwithf "unsupported program-type: %A" s

        member x.PrepareRenderObject(fboSignature : IFramebufferSignature, rj : IRenderObject) = x.PrepareRenderObject(fboSignature, rj) :> _

        member x.PrepareTexture (t : ITexture) = x.PrepareTexture t :> IBackendTexture
        member x.DeleteTexture (t : IBackendTexture) =
            match t with
                | :? Texture as t -> x.DeleteTexture t
                | _ -> failwithf "unsupported texture-type: %A" t

        member x.PrepareBuffer (b : IBuffer) = x.PrepareBuffer b :> IBackendBuffer
        member x.DeleteBuffer (b : IBackendBuffer) = 
            match b with
                | :? Aardvark.Rendering.GL.Buffer as b -> x.DeleteBuffer b
                | _ -> failwithf "unsupported buffer-type: %A" b


        member x.DeleteRenderbuffer (b : IRenderbuffer) =
            match b with
                | :? Aardvark.Rendering.GL.Renderbuffer as b -> ctx.Delete b
                | _ -> failwithf "unsupported renderbuffer-type: %A" b

        member x.DeleteFramebuffer(f : IFramebuffer) =
            match f with
                | :? Aardvark.Rendering.GL.Framebuffer as b -> ctx.Delete b
                | _ -> failwithf "unsupported framebuffer-type: %A" f

        member x.CreateStreamingTexture mipMaps = x.CreateStreamingTexture mipMaps
        member x.DeleteStreamingTexture tex = x.DeleteStreamingTexture tex

        member x.CreateFramebuffer(signature : IFramebufferSignature, bindings : Map<Symbol, IFramebufferOutput>) : IFramebuffer =
            x.CreateFramebuffer(signature, bindings) :> _

        member x.CreateTexture(size : V2i, format : TextureFormat, levels : int, samples : int, count : int) : IBackendTexture =
            x.CreateTexture(size, format, levels, samples, count) :> _

        member x.CreateRenderbuffer(size : V2i, format : RenderbufferFormat, samples : int) : IRenderbuffer =
            x.CreateRenderbuffer(size, format, samples) :> IRenderbuffer


    member x.CreateFramebufferSignature(attachments : SymbolDict<AttachmentSignature>) =
        let attachments = Map.ofSeq (SymDict.toSeq attachments)

        let depthStencil =
            Map.tryFind DefaultSemantic.Depth attachments

        let indexedColors =
            attachments
                |> Map.remove DefaultSemantic.Depth
                |> Map.toList
                |> List.sortWith (fun (a,_) (b,_) -> 
                    if a = DefaultSemantic.Colors then Int32.MinValue
                    elif b = DefaultSemantic.Colors then Int32.MaxValue
                    else String.Compare(a.ToString(), b.ToString())
                   )
                |> List.mapi (fun i t -> (i, t))
                |> Map.ofList

        FramebufferSignature(indexedColors, depthStencil)

    member x.PrepareTexture (t : ITexture) = ctx.CreateTexture t
    member x.PrepareBuffer (b : IBuffer) = ctx.CreateBuffer(b)
    member x.PrepareSurface (signature : IFramebufferSignature, s : ISurface) = 
        match SurfaceCompilers.compile ctx signature s with
            | Success prog -> prog
            | Error e -> failwith e

    member x.DeleteTexture (t : Texture) = 
        ctx.Delete t

    member x.DeleteSurface (p : Program) = 
        ctx.Delete p

    member x.DeleteBuffer (b : Aardvark.Rendering.GL.Buffer) =
        ctx.Delete b

    member x.CreateStreamingTexture(mipMaps : bool) =
        ctx.CreateStreamingTexture(mipMaps) :> IStreamingTexture

    member x.DeleteStreamingTexture(t : IStreamingTexture) =
        match t with
            | :? StreamingTexture as t ->
                ctx.Delete(t)
            | _ ->
                failwithf "unsupported streaming texture: %A" t

    member private x.CompileRenderInternal (fboSignature : IFramebufferSignature, engine : IMod<BackendConfiguration>, set : aset<IRenderObject>) =
        let eng = engine.GetValue()
        let shareTextures = eng.sharing &&& ResourceSharing.Textures <> ResourceSharing.None
        let shareBuffers = eng.sharing &&& ResourceSharing.Buffers <> ResourceSharing.None
            
        let man = ResourceManager(manager, ctx, shareTextures, shareBuffers)
        new RenderTask(x, fboSignature, ctx, man, engine, set)

    member x.PrepareRenderObject(fboSignature : IFramebufferSignature, rj : IRenderObject) =
        match rj with
             | :? RenderObject as rj -> manager.Prepare(fboSignature, rj)
             | :? PreparedRenderObject -> failwith "tried to prepare prepared render object"
             | _ -> failwith "unknown render object type"

    member x.CompileRender(fboSignature : IFramebufferSignature, engine : IMod<BackendConfiguration>, set : aset<IRenderObject>) : IRenderTask =
        x.CompileRenderInternal(fboSignature, engine, set) :> IRenderTask

    member x.CompileRender(fboSignature : IFramebufferSignature, engine : BackendConfiguration, set : aset<IRenderObject>) : IRenderTask =
        x.CompileRenderInternal(fboSignature, Mod.constant engine, set) :> IRenderTask

    member x.CompileClear(fboSignature : IFramebufferSignature, color : IMod<Option<C4f>>, depth : IMod<Option<float>>) : IRenderTask =
        new ClearTask(x, fboSignature, color, depth, ctx) :> IRenderTask

    member x.ResolveMultisamples(ms : IFramebufferOutput, ss : IBackendTexture, trafo : ImageTrafo) =
        using ctx.ResourceLock (fun _ ->
            let mutable oldFbo = 0
            OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.FramebufferBinding, &oldFbo);

            let tex = ss |> unbox<Texture>
            let size = ms.Size
            let readFbo = OpenGL.GL.GenFramebuffer()
            let drawFbo = OpenGL.GL.GenFramebuffer()

            OpenGL.GL.BindFramebuffer(OpenGL.FramebufferTarget.ReadFramebuffer,readFbo)
            match ms with
                | :? BackendTextureOutputView as ms ->
                    let tex = ms.texture |> unbox<Texture>
                    GL.FramebufferTexture2D(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, tex.Handle, ms.level)
                    
                | :? Renderbuffer as ms ->
                    GL.FramebufferRenderbuffer(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, ms.Handle)

                | _ ->
                    failwithf "[GL] cannot resolve %A" ms

 
            let size = ms.Size
            let readFbo = OpenGL.GL.GenFramebuffer()
            let drawFbo = OpenGL.GL.GenFramebuffer()

              
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer,drawFbo)
            GL.FramebufferTexture(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, tex.Handle, 0)

            let mutable src = Box2i(0, 0, size.X, size.Y)
            let mutable dst = Box2i(0, 0, size.X, size.Y)

            match trafo with
                | ImageTrafo.Rot0 -> ()
                | ImageTrafo.MirrorY -> 
                    dst.Min.Y <- dst.Max.Y - 1
                    dst.Max.Y <- -1
                | ImageTrafo.MirrorX ->
                    dst.Min.X <- dst.Max.X - 1
                    dst.Max.X <- -1
                | _ -> failwith "unsupported image trafo"
                    

            GL.BlitFramebuffer(src.Min.X, src.Min.Y, src.Max.X, src.Max.Y, dst.Min.X, dst.Min.Y, dst.Max.X, dst.Max.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest)

            GL.FramebufferRenderbuffer(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, 0)
            GL.FramebufferTexture(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, 0, 0)

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
            GL.DeleteFramebuffer readFbo
            GL.DeleteFramebuffer drawFbo

            GL.BindFramebuffer(FramebufferTarget.Framebuffer,oldFbo)

        )

    member x.GenerateMipMaps(t : IBackendTexture) =
        match t with
            | :? Texture as t ->
                if t.MipMapLevels > 1 then
                    let target = ExecutionContext.getTextureTarget t
                    using ctx.ResourceLock (fun _ ->
                        GL.BindTexture(target, t.Handle)
                        GL.Check "could not bind texture"


                        GL.GenerateMipmap(unbox (int target))
                        GL.Check "could not generate mipMaps"

                        GL.BindTexture(target, 0)
                        GL.Check "could not unbind texture"
                    )
                else
                    failwith "[GL] cannot generate mipMaps for non-mipmapped texture"

            | _ ->
                failwithf "[GL] unsupported texture: %A" t

    member x.Download(t : IBackendTexture, level : int, slice : int, target : PixImage) =
        ctx.Download(unbox<Texture> t, level, slice, target)

    member x.Upload(t : IBackendTexture, level : int, slice : int, source : PixImage) =
        ctx.Download(unbox<Texture> t, level, slice, source)

    member x.CreateFramebuffer(signature : IFramebufferSignature, bindings : Map<Symbol, IFramebufferOutput>) : Framebuffer =

        let colors =
            signature.ColorAttachments
                |> Map.toList
                |> List.map (fun (i,(s,desc)) ->
                    let b = bindings.[s]
                    if b.Format <> desc.format || b.Samples <> desc.samples then
                        failwithf "incompatible ColorAttachment: expected (%A, %A) but got: (%A, %A)" desc.format desc.samples b.Format b.Samples
                    (i, s, bindings.[s])
                   )

        let depth =
            match signature.DepthStencilAttachment with
                | Some desc ->
                    let b = bindings.[DefaultSemantic.Depth]
                    if b.Format <> desc.format || b.Samples <> desc.samples then
                        failwithf "incompatible DepthAttachment: expected (%A, %A) but got: (%A, %A)" desc.format desc.samples b.Format b.Samples

                    Some b
                | None ->
                    None

        ctx.CreateFramebuffer(signature, colors, depth)

    member x.CreateTexture(size : V2i, format : TextureFormat, levels : int, samples : int, count : int) : Texture =
        match count with
            | 1 -> ctx.CreateTexture2D(size, levels, format, samples)
            | _ -> ctx.CreateTexture3D(V3i(size.X, size.Y, count), levels, format, samples)

    member x.CreateRenderbuffer(size : V2i, format : RenderbufferFormat, samples : int) : Renderbuffer =
        ctx.CreateRenderbuffer(size, format, samples)

    new() = new Runtime(null)