namespace Aardvark.Rendering.GL

open System
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics
open Aardvark.Base.Incremental


type ChangeableFramebuffer(c : ChangeableResource<Framebuffer>) =
    let getHandle() =
        c.UpdateCPU()
        c.UpdateGPU()
        c.Resource.GetValue() :> IFramebuffer

    interface IFramebuffer with
        member x.Handle = getHandle().Handle
        member x.Size = getHandle().Size
        member x.Attachments = getHandle().Attachments
        member x.Dispose() = c.Dispose()

type ChangeableFramebufferTexture(c : ChangeableResource<Texture>) =
    let getHandle() =
        c.UpdateCPU()
        c.UpdateGPU()
        c.Resource.GetValue()

    interface IFramebufferTexture with
        member x.Handle = getHandle().Handle :> obj
        member x.Samples = getHandle().Multisamples
        member x.Dimension = getHandle().Dimension
        member x.ArraySize = getHandle().Count
        member x.MipMapLevels = getHandle().MipMapLevels
        member x.GetSize level = getHandle().GetSize level
        member x.Dispose() = c.Dispose()
        member x.WantMipMaps = getHandle().MipMapLevels > 1
        member x.Download(level) =
            let handle = getHandle()
            let format = handle.ChannelType |> ChannelType.toDownloadFormat
            handle.Context.Download(handle, format, level)

type ChangeableRenderbuffer(c : ChangeableResource<Renderbuffer>) =
    let getHandle() =
        c.UpdateCPU()
        c.UpdateGPU()
        c.Resource.GetValue()

    interface IFramebufferRenderbuffer with
        member x.Handle = getHandle().Handle :> obj
        member x.Size = getHandle().Size
        member x.Samples = getHandle().Samples
        member x.Dispose() = c.Dispose()

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
        member x.ResolveMultisamples(source, target, trafo) = x.ResolveMultisamples(source, target, trafo)
        member x.ContextLock = ctx.ResourceLock
        member x.CompileRender (engine : BackendConfiguration, set : aset<RenderObject>) = x.CompileRender(engine,set)
        member x.CompileClear(color, depth) = x.CompileClear(color, depth)
        member x.CreateTexture(size, format, levels, samples) = x.CreateTexture(size, format, levels, samples)
        member x.CreateRenderbuffer(size, format, samples) = x.CreateRenderbuffer(size, format, samples)
        member x.CreateFramebuffer bindings = x.CreateFramebuffer bindings
        
        member x.CreateSurface s = x.CreateSurface s :> IBackendSurface
        member x.DeleteSurface s = 
            match s with
                | :? Program as p -> x.DeleteSurface p
                | _ -> failwithf "unsupported program-type: %A" s

        member x.CreateTexture t = x.CreateTexture t :> IBackendTexture
        member x.DeleteTexture t =
            match t with
                | :? Texture as t -> x.DeleteTexture t
                | _ -> failwithf "unsupported texture-type: %A" t

        member x.CreateBuffer b = x.CreateBuffer b :> IBackendBuffer
        member x.DeleteBuffer b = 
            match b with
                | :? Aardvark.Rendering.GL.Buffer as b -> x.DeleteBuffer b
                | _ -> failwithf "unsupported buffer-type: %A" b

        member x.CreateStreamingTexture mipMaps = x.CreateStreamingTexture mipMaps
        member x.DeleteStreamingTexture tex = x.DeleteStreamingTexture tex

    member x.CreateTexture (t : ITexture) = ctx.CreateTexture t
    member x.CreateBuffer (b : IBuffer) : Aardvark.Rendering.GL.Buffer = failwith "not implemented"
    member x.CreateSurface (s : ISurface) = 
        match SurfaceCompilers.compile ctx s with
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

    member private x.CompileRenderInternal (engine : IMod<BackendConfiguration>, set : aset<RenderObject>) =
        let eng = engine.GetValue()
        let shareTextures = eng.sharing &&& ResourceSharing.Textures <> ResourceSharing.None
        let shareBuffers = eng.sharing &&& ResourceSharing.Buffers <> ResourceSharing.None
            
        let man = ResourceManager(manager, ctx, shareTextures, shareBuffers)
        new RenderTasks.RenderTask(x, ctx, man, engine, set)

    member x.CompileRender(engine : IMod<BackendConfiguration>, set : aset<RenderObject>) : IRenderTask =
        x.CompileRenderInternal(engine, set) :> IRenderTask

    member x.CompileRender(engine : BackendConfiguration, set : aset<RenderObject>) : IRenderTask =
        x.CompileRenderInternal(Mod.constant engine, set) :> IRenderTask

    member x.CompileClear(color : IMod<C4f>, depth : IMod<float>) : IRenderTask =
        new RenderTasks.ClearTask(x, color, depth, ctx) :> IRenderTask

    member x.ResolveMultisamples(ms : IFramebufferRenderbuffer, ss : IFramebufferTexture, trafo : ImageTrafo) =
        using ctx.ResourceLock (fun _ ->
            let mutable oldFbo = 0
            OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.FramebufferBinding, &oldFbo);

            match ms.Handle,ss.Handle with
                | (:? int as rb), (:? int as tex) ->
                        
                        
                    let size = ms.Size
                    let readFbo = OpenGL.GL.GenFramebuffer()
                    let drawFbo = OpenGL.GL.GenFramebuffer()

                        
                    OpenGL.GL.BindFramebuffer(OpenGL.FramebufferTarget.ReadFramebuffer,readFbo)
                    OpenGL.GL.FramebufferRenderbuffer(OpenGL.FramebufferTarget.ReadFramebuffer, OpenGL.FramebufferAttachment.ColorAttachment0, OpenGL.RenderbufferTarget.Renderbuffer, rb)

                    OpenGL.GL.BindFramebuffer(OpenGL.FramebufferTarget.DrawFramebuffer,drawFbo)
                    OpenGL.GL.FramebufferTexture(OpenGL.FramebufferTarget.DrawFramebuffer, OpenGL.FramebufferAttachment.ColorAttachment0, tex, 0)

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
                    

                    OpenGL.GL.BlitFramebuffer(src.Min.X, src.Min.Y, src.Max.X, src.Max.Y, dst.Min.X, dst.Min.Y, dst.Max.X, dst.Max.Y, OpenGL.ClearBufferMask.ColorBufferBit, OpenGL.BlitFramebufferFilter.Nearest)

                    OpenGL.GL.FramebufferRenderbuffer(OpenGL.FramebufferTarget.ReadFramebuffer, OpenGL.FramebufferAttachment.ColorAttachment0, OpenGL.RenderbufferTarget.Renderbuffer, 0)
                    OpenGL.GL.FramebufferTexture(OpenGL.FramebufferTarget.DrawFramebuffer, OpenGL.FramebufferAttachment.ColorAttachment0, 0, 0)

                    OpenGL.GL.BindFramebuffer(OpenGL.FramebufferTarget.ReadFramebuffer, 0)
                    OpenGL.GL.BindFramebuffer(OpenGL.FramebufferTarget.DrawFramebuffer, 0)
                    OpenGL.GL.DeleteFramebuffer readFbo
                    OpenGL.GL.DeleteFramebuffer drawFbo

                    OpenGL.GL.BindFramebuffer(OpenGL.FramebufferTarget.Framebuffer,oldFbo)

                | _ -> failwith "not implemented"
        )

    member x.CreateFramebuffer(bindings : Map<Symbol, IMod<IFramebufferOutput>>) =
        let fbo = manager.CreateFramebuffer(bindings |> Map.toList)
        new ChangeableFramebuffer(fbo) :> IFramebuffer

    member x.CreateTexture(size : IMod<V2i>, format : IMod<PixFormat>, mipMaps : IMod<int>, samples : IMod<int>) =
        let tex = manager.CreateTexture(size, mipMaps, format, samples)

        new ChangeableFramebufferTexture(tex) :> IFramebufferTexture

    member x.CreateRenderbuffer(size : IMod<V2i>, format : IMod<RenderbufferFormat>, samples : IMod<int>) =
        let rb = manager.CreateRenderbuffer(size, format, samples)

        new ChangeableRenderbuffer(rb) :> IFramebufferRenderbuffer

    member x.ResolveMultisamples(ms : IFramebufferRenderbuffer, srcRegion : Box2i, ss : IFramebufferTexture, targetRegion : Box2i) =
            using ctx.ResourceLock (fun _ ->
                let mutable oldFbo = 0
                OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.FramebufferBinding, &oldFbo);


                match ms.Handle,ss.Handle with
                    | (:? int as rb), (:? int as tex) ->
                        
                        let size = ms.Size
                        let readFbo = OpenGL.GL.GenFramebuffer()
                        let drawFbo = OpenGL.GL.GenFramebuffer()

                        OpenGL.GL.BindFramebuffer(OpenGL.FramebufferTarget.ReadFramebuffer,readFbo)
                        OpenGL.GL.FramebufferRenderbuffer(OpenGL.FramebufferTarget.ReadFramebuffer, OpenGL.FramebufferAttachment.ColorAttachment0, OpenGL.RenderbufferTarget.Renderbuffer, rb)

                        OpenGL.GL.BindFramebuffer(OpenGL.FramebufferTarget.DrawFramebuffer,drawFbo)
                        OpenGL.GL.FramebufferTexture(OpenGL.FramebufferTarget.DrawFramebuffer, OpenGL.FramebufferAttachment.ColorAttachment0, tex, 0)

                        let src = srcRegion
                        let dst = targetRegion

                        if srcRegion.Size = targetRegion.Size then
                            OpenGL.GL.BlitFramebuffer(src.Min.X, src.Min.Y, src.Max.X, src.Max.Y, dst.Min.X, dst.Min.Y, dst.Max.X, dst.Max.Y, OpenGL.ClearBufferMask.ColorBufferBit, OpenGL.BlitFramebufferFilter.Nearest)
                        else
                            OpenGL.GL.BlitFramebuffer(src.Min.X, src.Min.Y, src.Max.X, src.Max.Y, dst.Min.X, dst.Min.Y, dst.Max.X, dst.Max.Y, OpenGL.ClearBufferMask.ColorBufferBit, OpenGL.BlitFramebufferFilter.Linear)

                        OpenGL.GL.FramebufferRenderbuffer(OpenGL.FramebufferTarget.ReadFramebuffer, OpenGL.FramebufferAttachment.ColorAttachment0, OpenGL.RenderbufferTarget.Renderbuffer, 0)
                        OpenGL.GL.FramebufferTexture(OpenGL.FramebufferTarget.DrawFramebuffer, OpenGL.FramebufferAttachment.ColorAttachment0, 0, 0)

                        OpenGL.GL.BindFramebuffer(OpenGL.FramebufferTarget.ReadFramebuffer, 0)
                        OpenGL.GL.BindFramebuffer(OpenGL.FramebufferTarget.DrawFramebuffer, 0)
                        OpenGL.GL.DeleteFramebuffer readFbo
                        OpenGL.GL.DeleteFramebuffer drawFbo

                        OpenGL.GL.BindFramebuffer(OpenGL.FramebufferTarget.Framebuffer,oldFbo)

                    | _ -> failwith "not implemented"
            )


    new() = new Runtime(null)