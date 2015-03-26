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

type Runtime(ctx : Context) =

    static let versionRx = System.Text.RegularExpressions.Regex @"([0-9]+\.)*[0-9]+"

    let mutable ctx = ctx
    let mutable manager = if ctx <> null then ResourceManager(ctx) else null

    let firstGLCall () = 
        Log.startTimed "initializing OpenGL runtime"

        using ctx.ResourceLock (fun _ ->
                OpenTK.Graphics.OpenGL4.GL.GetError() |> ignore
                OpenGl.Unsafe.ActiveTexture (int OpenTK.Graphics.OpenGL4.TextureUnit.Texture0)
                OpenTK.Graphics.OpenGL4.GL.Check "first GL call failed"
                
               
                Log.line "vendor:   %A" ctx.Driver.vendor
                Log.line "renderer: %A" ctx.Driver.renderer 
                Log.line "version:  OpenGL %A / GLSL %A" ctx.Driver.version ctx.Driver.glsl
            )

        Log.stop()

    do if ctx <> null then
        firstGLCall ()
        //currentRuntime <- Some (this :> IRuntime)

    member x.SupportsUniformBuffers =
        ExecutionContext.uniformBuffersSupported

    member x.Context
        with get() = ctx
        and set c = 
            ctx <- c
            manager <- ResourceManager(ctx)
            //compiler <- Compiler.Compiler(x, c)
            firstGLCall ()
            //currentRuntime <- Some (x :> IRuntime)


    member x.Dispose() = 
        if ctx <> null then
            ctx.Dispose()
            ctx <- null
        

    interface IDisposable with
        member x.Dispose() = x.Dispose() 

    interface IRuntime with
        member x.CompileRender (set : aset<RenderJob>) = x.CompileRender set
        member x.CompileClear(color, depth) = x.CompileClear(color, depth)
        member x.CreateTexture(size, format, levels, samples) = x.CreateTexture(size, format, levels, samples)
        member x.CreateRenderbuffer(size, format, samples) = x.CreateRenderbuffer(size, format, samples)
        member x.CreateFramebuffer bindings = x.CreateFramebuffer bindings

    member private x.CompileRenderInternal (set : aset<RenderJob>) =
        let task = new RenderTasks.RenderTask(x, ctx, manager, set)
//        let subscriptions = Dictionary()
//
//        set |> ASet.registerCallback (fun d ->
//            for d in d do   
//                match d with
//                    | Add a ->
//                        if a.RenderPass <> null then
//                            let oldPass = ref System.UInt64.MaxValue
//                            let s = a.RenderPass |> Mod.registerCallback (fun k ->
//                                if !oldPass <> k  // phantom change here might lead to duplicate additions.
//                                    then
//                                        oldPass := k
//                                        task.Add(k,a)
//
//                                        match subscriptions.TryGetValue a with
//                                            | (true,(s,old)) ->
//                                                task.Remove(old, a)
//                                                subscriptions.[a] <- (s,k)
//                                            | _ -> ()
//                                    else 
//                                        printfn "changed pass to old value (phantom)"
//                            
//                            )
//                            let sortKey = a.RenderPass.GetValue()
//                            subscriptions.[a] <- (s, sortKey)
//                        else
//                            task.Add(0UL, a)
//
//                    | Rem a ->
//                        if a.RenderPass <> null then
//                            match subscriptions.TryGetValue a with
//                                | (true,(d,k)) ->
//                                    task.Remove(k, a)
//                                    d.Dispose()
//                                    subscriptions.Remove a |> ignore
//                                | _ -> ()
//                        else
//                            task.Remove(0UL, a)
//
//        ) |> ignore
        task

    member x.CompileRender(set : aset<RenderJob>) : IRenderTask =
        x.CompileRenderInternal(set) :> IRenderTask // newbackend

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

    member x.CreateRenderbuffer(size : IMod<V2i>, format : IMod<PixFormat>, samples : IMod<int>) =
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