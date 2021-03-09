namespace Aardvark.Rendering.GL

open System
open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open FSharp.Data.Adaptive
open FShade
open Microsoft.FSharp.NativeInterop
open Aardvark.Rendering.GL

#nowarn "9"

module private Align =
    let next (a : int) (v : int) =
        if v % a = 0 then v
        else (1 + v / a) * a

    let next2 (a : int) (v : V2i) =
        V2i(next a v.X, next a v.Y)

type Runtime() =

    static let versionRx = System.Text.RegularExpressions.Regex @"([0-9]+\.)*[0-9]+"
    
    let mutable ctx : Context = Unchecked.defaultof<_>
    let mutable manager : ResourceManager = Unchecked.defaultof<_>

    let onDispose = Event<unit>()    

    let compute = lazy ( new GLCompute(ctx) )

    member x.Context = ctx

    member x.Initialize(context : Context, shareTextures : bool, shareBuffers : bool) = 
           if ctx <> null then
               Log.warn "Runtime already initialized"

           ctx <- context
           manager <- ResourceManager(context, None, shareTextures, shareBuffers)

           Operators.using context.ResourceLock (fun _ ->
           
               try
                   Log.startTimed "initializing OpenGL runtime"
           
                   Driver.printDriverInfo 4

                   let driver = context.Driver

                   // GL_CONTEXT_CORE_PROFILE_BIT 1
                   // GL_CONTEXT_COMPATIBILITY_PROFILE_BIT 2
                   let profileType = if driver.profileMask = 1 then " Core" elif driver.profileMask = 2 then " Compatibility" else ""
           
                   Log.line "vendor:   %A" driver.vendor
                   Log.line "renderer: %A" driver.renderer 
                   Log.line "version:  OpenGL %A / GLSL %A %s" driver.version driver.glsl profileType
                                                           
                   let major = driver.version.Major
                   let minor = driver.version.Minor
                   if major < 3 || major = 3 && minor < 3 then
                       failwith "OpenGL driver version less than 3.3"
           
                   GLVM.vmInit()
           
                   // perform test OpenGL call
                   if OpenGl.Pointers.ActiveTexture = 0n then
                       failwith "Essentinal OpenGL procedure missing"
                               
                   OpenGl.Unsafe.ActiveTexture (int OpenTK.Graphics.OpenGL4.TextureUnit.Texture0)
                   OpenTK.Graphics.OpenGL4.GL.Check "first GL call failed"
           
               finally
                   Log.stop()
           )

    member x.Initialize(context : Context) = x.Initialize(context, true, true)

    member x.Dispose() = 
        if ctx <> null then
            onDispose.Trigger()
            ctx.Dispose()
            ctx <- null

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface ILodRuntime with

        member x.CreateLodRenderer(config : LodRendererConfig, data : aset<LodTreeInstance>) =

            let preparedState = PreparedPipelineState.ofPipelineState config.fbo x.ResourceManager config.surface config.state
                            
            //let info : LodRenderingInfo = 
            //    {
            //        LodRenderingInfo.quality = quality
            //        LodRenderingInfo.maxQuality = maxQuality
            //        LodRenderingInfo.renderBounds = renderBounds
            //    }
                            
            new LodRenderer(ctx, x.ResourceManager, preparedState, config, data) :> IPreparedRenderObject

    interface IRuntime with
    
        member x.DeviceCount = 1

        member x.Copy<'a when 'a : unmanaged>(src : NativeTensor4<'a>, fmt : Col.Format, dst : ITextureSubResource, dstOffset : V3i, size : V3i) : unit =
            use __ = ctx.ResourceLock

            let slice = dst.Slice
            let level = dst.Level
            let dst = dst.Texture |> unbox<Texture>
            let dstSize = dst.GetSize level
            let dstOffset = V3i(dstOffset.X, dstSize.Y - (dstOffset.Y + size.Y), dstOffset.Z)

            if dst.Multisamples > 1 then
                failwith "[GL] cannot upload multisampled texture"

            let inline bind (t : TextureTarget) (h : int) (f : unit -> unit) =
                GL.BindTexture(t, h)
                f()
                GL.BindTexture(t, 0)

            let channels = int src.SW

            let rowSize = channels * size.X * sizeof<'a>
            let alignedRowSize = Align.next ctx.PackAlignment rowSize
            if alignedRowSize % sizeof<'a> <> 0 then
                failwithf "[GL] ill-aligned upload"

            let sizeInBytes = nativeint alignedRowSize * nativeint size.Y * nativeint size.Z

            // copy to temp buffer
            let temp = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, temp)
            GL.BufferStorage(BufferTarget.PixelUnpackBuffer, sizeInBytes, 0n, BufferStorageFlags.MapWriteBit)
            let ptr = GL.MapBufferRange(BufferTarget.PixelUnpackBuffer, 0n, sizeInBytes, BufferAccessMask.MapWriteBit)

            let dstTensor =
                let dy = int64 alignedRowSize / int64 sizeof<'a>
                let info =
                    Tensor4Info(
                        0L,
                        V4l(int64 size.X, int64 size.Y, int64 size.Z, src.SW),
                        V4l(int64 channels, dy, dy * int64 size.Z, 1L)
                    )
                NativeTensor4<'a>(NativePtr.ofNativeInt ptr, info)


            let src = src.SubTensor4(V4i.Zero, V4i(size,channels))
            let dstTensor = dstTensor.MirrorY()
            NativeTensor4.copy src dstTensor

            GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer) |> ignore

            let pFmt = PixelFormat.ofColFormat fmt
            let pType = PixelType.ofType typeof<'a>

            match dst.Dimension, dst.IsArray with
                | TextureDimension.Texture1D, false ->
                    bind TextureTarget.Texture1D dst.Handle (fun () ->
                        GL.TexSubImage1D(TextureTarget.Texture1D, level, dstOffset.X, size.X, pFmt, pType, 0n)
                    )

                | TextureDimension.Texture1D, true ->
                    let view = GL.GenTexture()
                    GL.TextureView(view, TextureTarget.Texture1D, dst.Handle, unbox (int dst.Format), level, 1, slice, 1)
                    bind TextureTarget.Texture1D view (fun () ->
                        GL.TexSubImage1D(TextureTarget.Texture1D, 0, dstOffset.X, size.X, pFmt, pType, 0n)
                    )
                    GL.DeleteTexture(view)


                | TextureDimension.Texture2D, false ->
                    bind TextureTarget.Texture2D dst.Handle (fun () ->
                        GL.TexSubImage2D(TextureTarget.Texture2D, level, dstOffset.X, dstOffset.Y, size.X, size.Y, pFmt, pType, 0n)
                    )
                | TextureDimension.Texture2D, true ->
                    let view = GL.GenTexture()
                    GL.TextureView(view, TextureTarget.Texture2D, dst.Handle, unbox (int dst.Format), level, 1, slice, 1)
                    bind TextureTarget.Texture2D view (fun () ->
                        GL.TexSubImage2D(TextureTarget.Texture2D, 0, dstOffset.X, dstOffset.Y, size.X, size.Y, pFmt, pType, 0n)
                    )
                    GL.DeleteTexture(view)

                | TextureDimension.TextureCube, false ->
                    bind TextureTarget.TextureCubeMap dst.Handle (fun () ->
                        let target = int TextureTarget.TextureCubeMapPositiveX + slice % 6 |> unbox<TextureTarget>
                        GL.TexSubImage2D(target, level, dstOffset.X, dstOffset.Y, size.X, size.Y, pFmt, pType, 0n)
                    )

                | TextureDimension.TextureCube, true ->
                    let face = slice % 6
                    let slice = slice / 6
                    let view = GL.GenTexture()
                    GL.TextureView(view, TextureTarget.TextureCubeMap, dst.Handle, unbox (int dst.Format), level, 1, slice, 1)
                    bind TextureTarget.TextureCubeMap view (fun () ->
                        let target = int TextureTarget.TextureCubeMapPositiveX + face |> unbox<TextureTarget>
                        GL.TexSubImage2D(target, 0, dstOffset.X, dstOffset.Y, size.X, size.Y, pFmt, pType, 0n)
                    )
                    GL.DeleteTexture(view)

                | TextureDimension.Texture3D, false ->
                    bind TextureTarget.Texture3D dst.Handle (fun () ->
                        GL.TexSubImage3D(TextureTarget.Texture3D, level, dstOffset.X, dstOffset.Y, dstOffset.Z, size.X, size.Y, size.Z, pFmt, pType, 0n)
                    )

                | TextureDimension.Texture3D, true ->
                    failwith "[GL] cannot upload 3D array"

                | _ ->
                    failwith "[GL] unexpected texture dimension"


            GL.BindBuffer(BufferTarget.PixelUnpackBuffer,0)
            GL.DeleteBuffer(temp)

        member x.Copy<'a when 'a : unmanaged>(src : ITextureSubResource, srcOffset : V3i, dst : NativeTensor4<'a>, fmt : Col.Format, size : V3i) : unit =
            use __ = ctx.ResourceLock

            let slice = src.Slice
            let level = src.Level
            let src = src.Texture |> unbox<Texture>
            let srcSize = src.GetSize level
            let srcOffset = V3i(srcOffset.X, srcSize.Y - (srcOffset.Y + size.Y), srcOffset.Z)

            if src.Multisamples > 1 then
                failwith "[GL] cannot upload multisampled texture"

            let inline bind (t : TextureTarget) (h : int) (f : unit -> unit) =
                GL.BindTexture(t, h)
                f()
                GL.BindTexture(t, 0)

            let channels = int dst.SW

            let rowSize = channels * srcSize.X * sizeof<'a>
            let alignedRowSize = Align.next ctx.PackAlignment rowSize
            if alignedRowSize % sizeof<'a> <> 0 then
                failwithf "[GL] ill-aligned upload"

            let sizeInBytes = nativeint alignedRowSize * nativeint srcSize.Y * nativeint srcSize.Z

            let temp = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.PixelPackBuffer, temp)
            GL.BufferStorage(BufferTarget.PixelPackBuffer, sizeInBytes, 0n, BufferStorageFlags.MapReadBit)

            let inline bind (t : TextureTarget) (h : int) (f : unit -> unit) =
                GL.BindTexture(t, h)
                f()
                GL.BindTexture(t, 0)
                

            let pFmt = PixelFormat.ofColFormat fmt
            let pType = PixelType.ofType typeof<'a>

            match src.Dimension, src.IsArray with
                | TextureDimension.Texture1D, false ->
                    bind TextureTarget.Texture1D src.Handle (fun () ->
                        GL.GetTexImage(TextureTarget.Texture1D, level, pFmt, pType, 0n)
                    )

                | TextureDimension.Texture1D, true ->
                    failwith "not implemented"

                | TextureDimension.Texture2D, false ->
                    bind TextureTarget.Texture2D src.Handle (fun () ->
                        GL.GetTexImage(TextureTarget.Texture2D, level, pFmt, pType, 0n)
                    )

                | _ ->
                    failwith "not implemented"

            // copy from temp buffer
            let ptr = GL.MapBufferRange(BufferTarget.PixelPackBuffer, 0n, sizeInBytes, BufferAccessMask.MapReadBit)

            let srcTensor =
                let dy = int64 alignedRowSize / int64 sizeof<'a>
                let info =
                    Tensor4Info(
                        0L,
                        V4l(int64 srcSize.X, int64 srcSize.Y, int64 srcSize.Z, dst.SW),
                        V4l(int64 channels, dy, dy * int64 srcSize.Z, 1L)
                    )
                NativeTensor4<'a>(NativePtr.ofNativeInt ptr, info)



            let src = srcTensor.SubTensor4(V4i(srcOffset, 0), V4i(size, int dst.SW))
            let dst = dst.SubTensor4(V4i.Zero, V4i(size,channels)).MirrorY()
            NativeTensor4.copy src dst 

            GL.UnmapBuffer(BufferTarget.PixelPackBuffer) |> ignore
            GL.BindBuffer(BufferTarget.PixelPackBuffer,0)
            GL.DeleteBuffer(temp)

        member x.Copy(src : IFramebufferOutput, srcOffset : V3i, dst : IFramebufferOutput, dstOffset : V3i, size : V3i) : unit =
            use __ = ctx.ResourceLock

            let args (t : IFramebufferOutput) =
                match t with
                    | :? Renderbuffer as rb ->
                        rb.Handle, ImageTarget.Renderbuffer, 0, false

                    | :? ITextureLevel as tl ->
                        let t = tl.Texture |> unbox<Texture>
                        let target = TextureTarget.ofTexture t

                        let cnt = max t.Count 1
                        if tl.Slices.Min <> 0 || tl.Slices.Max <> cnt - 1 then
                            let v = GL.GenTexture()
                            GL.TextureView(v, target, t.Handle, unbox (int t.Format), tl.Level, 1, tl.Slices.Min, 1 + tl.Slices.Max - tl.Slices.Min)
                            v, unbox (int target), 0, true

                        else
                            t.Handle, unbox (int target), tl.Level, false
                    | _ ->
                        failwithf "[GL] invalid FramebufferOutput: %A" t

            let srcHandle, srcTarget, srcLevel, srcTemp = args src
            let dstHandle, dstTarget, dstLevel, dstTemp = args dst
            
            let srcOffset = V3i(srcOffset.X, src.Size.Y - (srcOffset.Y + size.Y), srcOffset.Z)
            let dstOffset = V3i(dstOffset.X, dst.Size.Y - (dstOffset.Y + size.Y), dstOffset.Z)

            GL.CopyImageSubData(
                srcHandle, srcTarget, srcLevel, srcOffset.X, srcOffset.Y, srcOffset.Z,
                dstHandle, dstTarget, dstLevel, dstOffset.X, dstOffset.Y, dstOffset.Z,
                size.X, size.Y, size.Z
            )

            if srcTemp then GL.DeleteTexture srcHandle
            if dstTemp then GL.DeleteTexture dstHandle




        member x.OnDispose = onDispose.Publish

        member x.AssembleModule (effect : Effect, signature : IFramebufferSignature, topology : IndexedGeometryMode) =
            signature.Link(effect, Range1d(-1.0, 1.0), false, topology)

        member x.ResourceManager = manager :> IResourceManager

        member x.CreateFramebufferSignature(attachments : Map<Symbol, AttachmentSignature>, layers : int, perLayer : Set<string>) =
            x.CreateFramebufferSignature(attachments, layers, perLayer)

            
        member x.CreateTexture(size : V3i, dim : TextureDimension, format : TextureFormat, slices : int, levels : int, samples : int) =
            x.CreateTexture(size, dim, format, slices, levels, samples) :> IBackendTexture

        member x.DeleteFramebufferSignature(signature : IFramebufferSignature) =
            ()

        member x.Download(t : IBackendTexture, level : int, slice : int, offset : V2i, target : PixImage) =
            x.Download(t, level, slice, offset, target)

        member x.Download(t : IBackendTexture, level : int, slice : int, offset : V3i, target : PixVolume) =
            x.Download(t, level, slice, offset, target)

        member x.Upload(t : IBackendTexture, level : int, slice : int, offset : V2i, source : PixImage) =
            x.Upload(t, level, slice, offset, source)

        member x.DownloadStencil(t : IBackendTexture, level : int, slice : int, offset : V2i, target : Matrix<int>) =
            x.DownloadStencil(t, level, slice, offset, target)

        member x.DownloadDepth(t : IBackendTexture, level : int, slice : int, offset : V2i, target : Matrix<float32>) =
            x.DownloadDepth(t, level, slice, offset, target)

        member x.ResolveMultisamples(source, target, trafo) = x.ResolveMultisamples(source, target, trafo)
        member x.GenerateMipMaps(t : IBackendTexture) = x.GenerateMipMaps t
        member x.ContextLock = ctx.ResourceLock :> IDisposable
        member x.CompileRender (signature, engine : BackendConfiguration, set : aset<IRenderObject>) = x.CompileRender(signature, engine,set)
        member x.CompileClear(signature, color, depth, stencil) = x.CompileClear(signature, color, depth, stencil)
      
        ///NOTE: OpenGL does not care about 
        member x.CreateBuffer(size : nativeint, usage : BufferUsage) = x.CreateBuffer(size) :> IBackendBuffer
        member x.Copy(src : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) = x.Upload(src, dst, dstOffset, size)
        member x.Copy(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, size : nativeint) = x.Download(src, srcOffset, dst, size)
        member x.Copy(src : IBackendBuffer, srcOffset : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) = 
            x.Copy(src, srcOffset, dst, dstOffset, size)
            
        member x.CopyAsync(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, size : nativeint) : unit -> unit =
            failwith ""

        member x.Copy(src : IBackendTexture, srcBaseSlice : int, srcBaseLevel : int, dst : IBackendTexture, dstBaseSlice : int, dstBaseLevel : int, slices : int, levels : int) = x.Copy(src, srcBaseSlice, srcBaseLevel, dst, dstBaseSlice, dstBaseLevel, slices, levels)
        member x.PrepareSurface (signature, s : ISurface) : IBackendSurface = x.PrepareSurface(signature, s)
        member x.DeleteSurface (s : IBackendSurface) = 
            match s with
                | :? Program as p -> x.DeleteSurface p
                | _ -> failwithf "unsupported program-type: %A" s

        member x.PrepareRenderObject(fboSignature : IFramebufferSignature, rj : IRenderObject) = x.PrepareRenderObject(fboSignature, rj)

        member x.PrepareTexture (t : ITexture) = x.PrepareTexture t :> IBackendTexture
        member x.DeleteTexture (t : IBackendTexture) =
            match t with
                | :? Texture as t -> x.DeleteTexture t
                | _ -> failwithf "unsupported texture-type: %A" t

        member x.PrepareBuffer (b : IBuffer, usage : BufferUsage) = x.PrepareBuffer b :> IBackendBuffer
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

        member x.CreateSparseTexture<'a when 'a : unmanaged> (size : V3i, levels : int, slices : int, dim : TextureDimension, format : Col.Format, brickSize : V3i, maxMemory : int64) : ISparseTexture<'a> =
            failwith "not implemented"


        member x.CreateFramebuffer(signature : IFramebufferSignature, bindings : Map<Symbol, IFramebufferOutput>) : IFramebuffer =
            x.CreateFramebuffer(signature, bindings) :> _


        member x.CreateTexture(size : V2i, format : TextureFormat, levels : int, samples : int) : IBackendTexture =
            ctx.CreateTexture2D(size, levels, format, samples) :> _

        member x.CreateTextureArray(size : V2i, format : TextureFormat, levels : int, samples : int, count : int) : IBackendTexture =
            ctx.CreateTexture2DArray(size, count, levels, format, samples) :> _
            
        member x.CreateTextureCube(size : int, format : TextureFormat, levels : int, samples : int) : IBackendTexture =
            x.CreateTextureCube(size, format, levels, samples) :> _

        member x.CreateTextureCubeArray(size : int, format : TextureFormat, levels : int, samples : int, count : int) : IBackendTexture =
            x.CreateTextureCubeArray(size, format, levels, samples, count) :> _

        member x.CreateRenderbuffer(size : V2i, format : RenderbufferFormat, samples : int) : IRenderbuffer =
            x.CreateRenderbuffer(size, format, samples) :> IRenderbuffer
            

        member x.CreateGeometryPool(types : Map<Symbol, Type>) =
            x.CreateGeometryPool(types)

            
        member x.MaxLocalSize = compute.Value.WorkGroupSize
        member x.CreateComputeShader (c : FShade.ComputeShader) = ctx.CompileKernel c :> IComputeShader
        member x.NewInputBinding(c : IComputeShader) = new ComputeShaderInputBinding(unbox c) :> IComputeShaderInputBinding
        member x.DeleteComputeShader (shader : IComputeShader) = ctx.Delete(unbox<GL.ComputeShader> shader)
        member x.Run (commands : list<ComputeCommand>, queries : IQuery) = ctx.Run(commands, queries)
        member x.Compile (commands : list<ComputeCommand>) =
            let x = x :> IComputeRuntime
            { new ComputeProgram<unit>() with
                member __.RunUnit(queries) =
                    x.Run(commands, queries)
                member x.Release() =
                    ()
            }

        member x.Clear(fbo : IFramebuffer, clearColors : Map<Symbol, C4f>, depth : Option<float>, stencil : Option<int>) =
            use __ = ctx.ResourceLock

            let fbo = fbo |> unbox<Framebuffer>
            let handle = fbo.Handle
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, handle)
            GL.Check "could not bind framebuffer"
            
            // assume user has not messed with gl state -> omit resetting color/depth/stencil mask

            // omit check if farmebuffer signature actually contains depth/stencil
            let mutable combinedClearMask = ClearBufferMask.None
            match depth with 
            | Some d -> GL.ClearDepth(d)
                        combinedClearMask <- combinedClearMask ||| ClearBufferMask.DepthBufferBit
            | None -> ()

            match stencil with 
            | Some s -> GL.ClearStencil(s)
                        combinedClearMask <- combinedClearMask ||| ClearBufferMask.StencilBufferBit
            | None -> ()

            if fbo.Signature.ColorAttachments.Count = 1 then
                let single = fbo.Signature.ColorAttachments |> Seq.head
                let name = fst single.Value 
                match Map.tryFind name clearColors with
                | Some c ->
                    GL.ClearColor(c.R, c.G, c.B, c.A)
                    GL.Clear(ClearBufferMask.ColorBufferBit ||| combinedClearMask)
                | None -> failwith "invalid target name"
            else
                // clear depth stencil if requested
                if combinedClearMask <> ClearBufferMask.None then
                    GL.Clear(combinedClearMask)

                // clear each color layer individually
                for x in fbo.Signature.ColorAttachments do
                    let name = fst x.Value
                    match Map.tryFind name clearColors with
                    | Some c ->
                        GL.DrawBuffer(int DrawBufferMode.ColorAttachment0 + x.Key |> unbox)
                        GL.ClearColor(c.R, c.G, c.B, c.A)
                        GL.Clear(ClearBufferMask.ColorBufferBit)
                    | None -> ()
                
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Check "could not unbind framebuffer"

        member x.ClearColor(texture : IBackendTexture, color : C4f) = 
            use __ = ctx.ResourceLock

            let fbo = GL.GenFramebuffer()
            GL.Check "could not create framebuffer"

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo)
            GL.Check "could not bind framebuffer"

            GL.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, texture.Handle |> unbox<int>, 0)
            GL.Check "could not attach framebuffer texture"

            GL.ClearColor(color.R, color.G, color.B, color.A)
            GL.Clear(ClearBufferMask.ColorBufferBit)

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
            GL.Check "could not unbind framebuffer"

            GL.DeleteFramebuffer(fbo)
            GL.Check "could not delete framebuffer"

        member x.ClearDepthStencil(texture : IBackendTexture, depth : Option<float>, stencil : Option<int>) = 

            let binding = match (depth, stencil) with
                          | Some x, Some y -> Some FramebufferAttachment.DepthStencilAttachment
                          | Some x, None -> Some FramebufferAttachment.DepthAttachment
                          | None, Some y -> Some FramebufferAttachment.StencilAttachment
                          | _ -> None

            match binding with
            | Some b ->
                use __ = ctx.ResourceLock

                let fbo = GL.GenFramebuffer()
                GL.Check "could not create framebuffer"

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo)
                GL.Check "could not bind framebuffer"
            
                GL.FramebufferTexture(FramebufferTarget.Framebuffer, b, texture.Handle |> unbox<int>, 0)
                GL.Check "could not attach framebuffer texture"

                match (depth, stencil) with
                | Some d, Some s ->
                    GL.ClearDepth(d)
                    GL.ClearStencil(s)
                    GL.Clear(ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)
                | Some d, None -> 
                    GL.ClearDepth(d)
                    GL.Clear(ClearBufferMask.DepthBufferBit)
                | None, Some s -> 
                    GL.ClearStencil(s)
                    GL.Clear(ClearBufferMask.StencilBufferBit)
                | _ -> ()

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
                GL.Check "could not unbind framebuffer"

                GL.DeleteFramebuffer(fbo)
                GL.Check "could not delete framebuffer"

            | _ -> () // done
        
        member x.CreateTextureView(texture : IBackendTexture, levels : Range1i, slices : Range1i, isArray : bool) : IBackendTexture =
            ctx.CreateTextureView(unbox<Texture> texture, levels, slices, isArray) :> IBackendTexture

        member x.CreateTimeQuery() =
            new TimeQuery(ctx) :> ITimeQuery

        member x.CreateOcclusionQuery(precise) =
            new OcclusionQuery(ctx, precise) :> IOcclusionQuery

        member x.CreatePipelineQuery(statistics) =
            use __ = ctx.ResourceLock

            if GL.ARB_pipeline_statistics_query then
                new PipelineQuery(ctx, statistics) :> IPipelineQuery
            else
                new GeometryQuery(ctx) :> IPipelineQuery

        member x.SupportedPipelineStatistics =
            use __ = ctx.ResourceLock

            if GL.ARB_pipeline_statistics_query then
                PipelineStatistics.All
            else
                Set.singleton ClippingInputPrimitives

        member x.ShaderCachePath
            with get() = ctx.ShaderCachePath
            and set(value) = ctx.ShaderCachePath <- value

    
    member x.Copy(src : IBackendTexture, srcBaseSlice : int, srcBaseLevel : int, dst : IBackendTexture, dstBaseSlice : int, dstBaseLevel : int, slices : int, levels : int) =
        let src = unbox<Texture> src
        let dst = unbox<Texture> dst
        
        let mutable size = src.GetSize srcBaseLevel
        for l in 0 .. levels - 1 do
            let srcLevel = srcBaseLevel + l
            let dstLevel = dstBaseLevel + l
            for s in 0 .. slices - 1 do
                if src.Multisamples = dst.Multisamples then
                    ctx.Copy(src, srcLevel, srcBaseSlice + s, V2i.Zero, dst, dstLevel, dstBaseSlice + s, V2i.Zero, size.XY)
                else
                    ctx.Blit(src, srcLevel, srcBaseSlice + s, Box2i(V2i.Zero, size.XY - V2i.II), dst, dstLevel, dstBaseSlice + s, Box2i(V2i.Zero, size.XY - V2i.II), false)
            size <- size / 2


    member x.CreateBuffer(size : nativeint) =
        ctx.CreateBuffer(int size)

    member x.Upload(src : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) =
        use __ = ctx.ResourceLock
        GL.NamedBufferSubData(unbox<int> dst.Handle, dstOffset, size, src)
        GL.Check "could not upload buffer data"
        if RuntimeConfig.SyncUploadsAndFrames then
            GL.Sync()

    member x.Download(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, size : nativeint) =
        use __ = ctx.ResourceLock
        GL.GetNamedBufferSubData(unbox<int> src.Handle, srcOffset, size, dst)
        GL.Check "could not download buffer data"
        if RuntimeConfig.SyncUploadsAndFrames then
            GL.Sync()

    member x.Copy(src : IBackendBuffer, srcOffset : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) =
        use __ = ctx.ResourceLock
        GL.CopyNamedBufferSubData(unbox<int> src.Handle, unbox<int> dst.Handle, srcOffset, dstOffset, size)
        GL.Check "could not copy buffer data"
        if RuntimeConfig.SyncUploadsAndFrames then
            GL.Sync()

    member x.CreateFramebufferSignature(attachments : Map<Symbol, AttachmentSignature>, layers : int, perLayer : Set<string>) =
        let depth =
            Map.tryFind DefaultSemantic.Depth attachments

        let stencil =
            Map.tryFind DefaultSemantic.Stencil attachments

        let indexedColors =
            attachments
                |> Map.remove DefaultSemantic.Depth
                |> Map.remove DefaultSemantic.Stencil
                |> Map.toList
                |> List.sortWith (fun (a,_) (b,_) -> 
                    if a = DefaultSemantic.Colors then Int32.MinValue
                    elif b = DefaultSemantic.Colors then Int32.MaxValue
                    else String.Compare(a.ToString(), b.ToString())
                   )
                |> List.mapi (fun i t -> (i, t))
                |> Map.ofList

        { new Object() with
            member _.ToString() = sprintf "{ ColorAttachments = %A; DepthAttachment = %A; StencilAttachment = %A }" indexedColors depth stencil
          interface IFramebufferSignature with
            member _.Runtime = x :> IFramebufferRuntime
            member _.ColorAttachments = indexedColors
            member _.DepthAttachment = depth
            member _.StencilAttachment = stencil
            member _.LayerCount = layers
            member _.PerLayerUniforms = perLayer }

    member x.PrepareTexture (t : ITexture) = ctx.CreateTexture t
    member x.PrepareBuffer (b : IBuffer) = ctx.CreateBuffer(b)
    member x.PrepareSurface (signature : IFramebufferSignature, s : ISurface) : IBackendSurface = 
        Operators.using ctx.ResourceLock (fun d -> 
            let surface =
                match s with
                    | :? FShadeSurface as f -> Aardvark.Rendering.Surface.FShadeSimple f.Effect
                    | _ -> Aardvark.Rendering.Surface.Backend s

            if signature.LayerCount > 1 then
                Log.warn("[PrepareSurface] Using Triangle topology.")

            let iface, program = ctx.CreateProgram(signature, surface, IndexedGeometryMode.TriangleList)

            AVal.force program :> IBackendSurface

        )


    member x.DeleteTexture (t : Texture) = 
        ctx.Delete t

    member x.DeleteSurface (p : Program) = 
        ctx.Delete p

    member x.DeleteBuffer (b : Aardvark.Rendering.GL.Buffer) =
        ctx.Delete b

    member x.CreateStreamingTexture(mipMaps : bool) =
        ctx.CreateStreamingTexture(mipMaps) :> IStreamingTexture

    member x.ResourceManager = manager

    member x.DeleteStreamingTexture(t : IStreamingTexture) =
        match t with
            | :? StreamingTexture as t ->
                ctx.Delete(t)
            | _ ->
                failwithf "unsupported streaming texture: %A" t

    member private x.CompileRenderInternal (fboSignature : IFramebufferSignature, engine : aval<BackendConfiguration>, set : aset<IRenderObject>) =
        let set = EffectDebugger.Hook set
        let eng = engine.GetValue()
        let shareTextures = eng.sharing &&& ResourceSharing.Textures <> ResourceSharing.None
        let shareBuffers = eng.sharing &&& ResourceSharing.Buffers <> ResourceSharing.None
            
        if Config.UseNewRenderTask then
            new RenderTasks.NewRenderTask(manager, fboSignature, set, engine, shareTextures, shareBuffers) :> IRenderTask
        else
            new RenderTasks.RenderTask(manager, fboSignature, set, engine, shareTextures, shareBuffers) :> IRenderTask

    member x.PrepareRenderObject(fboSignature : IFramebufferSignature, rj : IRenderObject) : IPreparedRenderObject =
        PreparedCommand.ofRenderObject fboSignature manager rj :> IPreparedRenderObject
        //match rj with
        //     | :? RenderTaskObject as t -> t :> IPreparedRenderObject
        //     | :? RenderObject as rj -> manager.Prepare(fboSignature, rj) :> IPreparedRenderObject
        //     | :? MultiRenderObject as rj -> 
        //        let all = 
        //            rj.Children 
        //                |> List.map (fun ro -> x.PrepareRenderObject(fboSignature, ro))
        //                |> List.collect (fun o ->
        //                    match o with
        //                        | :? PreparedMultiRenderObject as s -> s.Children
        //                        | _ -> [unbox<PreparedRenderObject> o]
        //                )
        //        new PreparedMultiRenderObject(all) :> IPreparedRenderObject

        //     | :? PreparedRenderObject | :? PreparedMultiRenderObject -> failwith "tried to prepare prepared render object"
        //     | _ -> failwith "unknown render object type"

    member x.CompileRender(fboSignature : IFramebufferSignature, engine : aval<BackendConfiguration>, set : aset<IRenderObject>) : IRenderTask =
        x.CompileRenderInternal(fboSignature, engine, set)

    member x.CompileRender(fboSignature : IFramebufferSignature, engine : BackendConfiguration, set : aset<IRenderObject>) : IRenderTask =
        x.CompileRenderInternal(fboSignature, AVal.constant engine, set)

    member x.CompileClear(signature : IFramebufferSignature, color : aval<Map<Symbol, C4f>>, depth : aval<float option>, stencil : aval<int option>) : IRenderTask =
        let clearValues =
            color |> AVal.map (fun colors ->
                signature.ColorAttachments |> Map.choose (fun _ (sem, _) ->
                    colors |> Map.tryFind sem
                )
                |> Map.toList
            )

        new RenderTasks.ClearTask(x, signature, clearValues, depth, stencil, ctx) :> IRenderTask

    member x.ResolveMultisamples(ms : IFramebufferOutput, srcOffset : V2i, ss : IBackendTexture, dstOffset : V2i, dstLayer : int, size : V2i, trafo : ImageTrafo) =
        Operators.using ctx.ResourceLock (fun _ ->
            let mutable oldFbo = 0
            GL.GetInteger(GetPName.FramebufferBinding, &oldFbo);

            let targetTex = ss |> unbox<Texture>
            let readFbo = GL.GenFramebuffer()
            let drawFbo = GL.GenFramebuffer()

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer,readFbo)
            GL.Check "could not bind read framebuffer"
            let mutable srcAtt = FramebufferAttachment.ColorAttachment0

            match ms with
            | :? Renderbuffer as ms ->
                if TextureFormat.hasDepth (RenderbufferFormat.toTextureFormat ms.Format) then srcAtt <- FramebufferAttachment.DepthStencilAttachment
                GL.FramebufferRenderbuffer(FramebufferTarget.ReadFramebuffer, srcAtt, RenderbufferTarget.Renderbuffer, ms.Handle)
                GL.Check "could not set read framebuffer texture"

            | :? ITextureLevel as ms ->
                let baseSlice = ms.Slices.Min
                let slices = 1 + ms.Slices.Max - baseSlice
                let tex = ms.Texture |> unbox<Texture>

                if slices <> 1 then failwith "layer sub-ranges not supported atm."

                if TextureFormat.hasDepth tex.Format then srcAtt <- FramebufferAttachment.DepthStencilAttachment
                if tex.IsArray || tex.Dimension = TextureDimension.TextureCube then
                    GL.FramebufferTextureLayer(FramebufferTarget.ReadFramebuffer, srcAtt, tex.Handle, ms.Level, baseSlice)
                    GL.Check "could not set read framebuffer texture"
                else
                    // NOTE: allow to resolve/copy singlesample textures as well
                    GL.FramebufferTexture2D(FramebufferTarget.ReadFramebuffer, srcAtt, (if tex.IsMultisampled then TextureTarget.Texture2DMultisample else TextureTarget.Texture2D), tex.Handle, ms.Level)
                    GL.Check "could not set read framebuffer texture"

            | _ ->
                failwithf "[GL] cannot resolve %A" ms

            // NOTE: binding src texture with multiple slices using FramebufferTexture(..) and dst as FramebufferTexture(..) only blits first slice
            // TODO: maybe multilayer copy works using FramebufferTexture2D with TextureTarget.TextureArray
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer,drawFbo)
            GL.Check "could not bind write framebuffer"
            if targetTex.IsArray || targetTex.Dimension = TextureDimension.TextureCube then
                GL.FramebufferTextureLayer(FramebufferTarget.DrawFramebuffer, srcAtt, targetTex.Handle, 0, dstLayer)
                GL.Check "could not set write framebuffer texture"
            else
                GL.FramebufferTexture(FramebufferTarget.DrawFramebuffer, srcAtt, targetTex.Handle, 0)
                GL.Check "could not set write framebuffer texture"


            let mutable src = Box2i.FromMinAndSize(srcOffset, size)
            let mutable dst = Box2i.FromMinAndSize(dstOffset, size)

            match trafo with
                | ImageTrafo.Identity -> ()
                | ImageTrafo.MirrorY -> 
                    dst.Min.Y <- dst.Max.Y - 1
                    dst.Max.Y <- -1
                | ImageTrafo.MirrorX ->
                    dst.Min.X <- dst.Max.X - 1
                    dst.Max.X <- -1
                | _ -> 
                    failwith "unsupported image trafo"
                    
            let mask =  
                if srcAtt = FramebufferAttachment.DepthStencilAttachment then 
                    ClearBufferMask.DepthBufferBit
                else 
                    GL.ReadBuffer(ReadBufferMode.ColorAttachment0)
                    GL.DrawBuffer(DrawBufferMode.ColorAttachment0)
                    ClearBufferMask.ColorBufferBit

            GL.BlitFramebuffer(src.Min.X, src.Min.Y, src.Max.X, src.Max.Y, dst.Min.X, dst.Min.Y, dst.Max.X, dst.Max.Y, mask, BlitFramebufferFilter.Nearest)
            GL.Check "could not blit framebuffer"

            GL.FramebufferTexture(FramebufferTarget.ReadFramebuffer, srcAtt, 0, 0)
            GL.FramebufferTexture(FramebufferTarget.DrawFramebuffer, srcAtt, 0, 0)

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
            GL.DeleteFramebuffer readFbo
            GL.DeleteFramebuffer drawFbo

            GL.BindFramebuffer(FramebufferTarget.Framebuffer,oldFbo)
            GL.Check "error cleanup"
        )

    member x.ResolveMultisamples(ms : IFramebufferOutput, ss : IBackendTexture, trafo : ImageTrafo) =
        x.ResolveMultisamples(ms, V2i.Zero, ss, V2i.Zero, 0, ms.Size.XY, trafo)

    member x.GenerateMipMaps(t : IBackendTexture) =
        match t with
            | :? Texture as t ->
                if t.MipMapLevels > 1 then
                    let target = Uploads.getTextureTarget t
                    Operators.using ctx.ResourceLock (fun _ ->
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

    member x.Download(t : IBackendTexture, level : int, slice : int, offset : V2i, target : PixImage) =
        ctx.Download(unbox<Texture> t, level, slice, offset, target)
        
    member x.Download(t : IBackendTexture, level : int, slice : int, offset : V3i, target : PixVolume) : unit =
       failwith "[GL] Volume download not implemented"

    member x.DownloadStencil(t : IBackendTexture, level : int, slice : int, offset : V2i, target : Matrix<int>) =
        ctx.DownloadStencil(unbox<Texture> t, level, slice, offset, target)

    member x.DownloadDepth(t : IBackendTexture, level : int, slice : int, offset : V2i, target : Matrix<float32>) =
        ctx.DownloadDepth(unbox<Texture> t, level, slice, offset, target)

    member x.Upload(t : IBackendTexture, level : int, slice : int, offset : V2i, source : PixImage) =
        ctx.Upload(unbox<Texture> t, level, slice, offset, source)

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
            match signature.DepthAttachment with
                | Some desc ->
                    let b = bindings.[DefaultSemantic.Depth]
                    if b.Format <> desc.format || b.Samples <> desc.samples then
                        failwithf "incompatible DepthAttachment: expected (%A, %A) but got: (%A, %A)" desc.format desc.samples b.Format b.Samples

                    Some b
                | None ->
                    None

        let stencil =
            match signature.StencilAttachment with
                | Some desc ->
                    let b = bindings.[DefaultSemantic.Stencil]
                    if b.Format <> desc.format || b.Samples <> desc.samples then
                        failwithf "incompatible StencilAttachment: expected (%A, %A) but got: (%A, %A)" desc.format desc.samples b.Format b.Samples

                    Some b
                | None ->
                    None

        ctx.CreateFramebuffer(signature, colors, depth, stencil)

    member x.CreateTexture(size : V2i, format : TextureFormat, levels : int, samples : int, count : int) : Texture =
        match count with
            | 1 -> ctx.CreateTexture2D(size, levels, format, samples)
            | _ -> ctx.CreateTexture2DArray(size, count, levels, format, samples)



    member x.CreateTexture(size : V3i, dim : TextureDimension, format : TextureFormat, slices : int, levels : int, samples : int) =
        ctx.CreateTexture(size, dim, format, slices, levels, samples)


    member x.CreateTextureCube(size : int, format : TextureFormat, levels : int, samples : int) : Texture =
        ctx.CreateTextureCube(size, levels, format, samples)

    member x.CreateTextureCubeArray(size : int, format : TextureFormat, levels : int, samples : int, count : int) : Texture =
        ctx.CreateTextureCubeArray(size, levels, format, samples, count)

    member x.CreateRenderbuffer(size : V2i, format : RenderbufferFormat, samples : int) : Renderbuffer =
        ctx.CreateRenderbuffer(size, format, samples)
                
    member x.CreateGeometryPool(types : Map<Symbol, Type>) =
        new SparseBufferGeometryPool(ctx, types) :> IGeometryPool