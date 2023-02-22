namespace Aardvark.Rendering.GL

open System
open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open FSharp.Data.Adaptive
open FShade
open Aardvark.Rendering.GL
open System.Runtime.InteropServices
open ComputeTaskInternals

#nowarn "9"

type Runtime(debug : IDebugConfig) =

    let mutable ctx : Context = Unchecked.defaultof<_>
    let mutable manager : ResourceManager = Unchecked.defaultof<_>

    let debug = DebugConfig.unbox debug

    let onDispose = Event<unit>()

    member x.Context = ctx

    member x.DebugConfig = debug

    member x.Initialize(context : Context) =
        if ctx <> null then
            Log.warn "Runtime already initialized"

        ctx <- context
        manager <- ResourceManager(context, None)

        GL.CheckErrors <- debug.ErrorFlagCheck

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

            finally
                Log.stop()
        )

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
        member x.DebugConfig = x.DebugConfig

        member x.Copy(src : IFramebuffer, dst : IFramebuffer) =
            use __ = ctx.ResourceLock
            let src = src :?> Framebuffer
            let dst = dst :?> Framebuffer
            
            if src.Handle <> dst.Handle then
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, src.Handle)
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, dst.Handle)
            
            
                if dst.Handle = 0 then
                    let att = src.Signature.ColorAttachments |> Map.tryFindKey (fun k v -> v.Name = DefaultSemantic.Colors)
                    let mutable flags = ClearBufferMask.None
                    match att with
                    | Some colorIndex ->
                        GL.ReadBuffer(unbox<ReadBufferMode> (int ReadBufferMode.ColorAttachment0 + colorIndex))
                        GL.DrawBuffer(DrawBufferMode.BackLeft)
                        flags <- ClearBufferMask.ColorBufferBit
                    | None -> 
                        ()
                        
                    GL.BlitFramebuffer(
                        0, 0, src.Size.X, src.Size.Y,
                        0, 0, dst.Size.X, dst.Size.Y,
                        flags ||| ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit,
                        BlitFramebufferFilter.Nearest
                    )
                elif src.Handle = 0 then
                    let att = dst.Signature.ColorAttachments |> Map.tryFindKey (fun k v -> v.Name = DefaultSemantic.Colors)
                    let mutable flags = ClearBufferMask.None
                    match att with
                    | Some colorIndex ->
                        GL.ReadBuffer(ReadBufferMode.BackLeft)
                        GL.DrawBuffer(unbox<DrawBufferMode> (int DrawBufferMode.ColorAttachment0 + colorIndex))
                        flags <- ClearBufferMask.ColorBufferBit
                    | None -> 
                        ()
                        
                    GL.BlitFramebuffer(
                        0, 0, src.Size.X, src.Size.Y,
                        0, 0, dst.Size.X, dst.Size.Y,
                        flags ||| ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit,
                        BlitFramebufferFilter.Nearest
                    )
                else
                    let mutable copyDepth =
                        if Option.isSome src.Signature.DepthStencilAttachment && Option.isSome dst.Signature.DepthStencilAttachment then
                            ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit
                        else
                            ClearBufferMask.None

                    for (dstIndex, dstAtt) in Map.toSeq dst.Signature.ColorAttachments do
                        let srcIndex = src.Signature.ColorAttachments |> Map.tryFindKey (fun k v -> v.Name = dstAtt.Name)
                        match srcIndex with
                        | Some srcIndex ->
                            GL.ReadBuffer(unbox<ReadBufferMode> (int ReadBufferMode.ColorAttachment0 + srcIndex))
                            GL.DrawBuffer(unbox<DrawBufferMode> (int DrawBufferMode.ColorAttachment0 + dstIndex))
                            GL.BlitFramebuffer(
                                0, 0, src.Size.X, src.Size.Y,
                                0, 0, dst.Size.X, dst.Size.Y,
                                ClearBufferMask.ColorBufferBit ||| copyDepth,
                                BlitFramebufferFilter.Nearest
                            )
                            copyDepth <- ClearBufferMask.None
                        | None ->
                            ()
                    if copyDepth <> ClearBufferMask.None then
                        GL.BlitFramebuffer(
                            0, 0, src.Size.X, src.Size.Y,
                            0, 0, dst.Size.X, dst.Size.Y,
                            copyDepth,
                            BlitFramebufferFilter.Nearest
                        )
                    
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
                       
        member x.ReadPixels(src : IFramebuffer, sem : Symbol, offset : V2i, size : V2i) = 
            use __ = ctx.ResourceLock
            let src = src :?> Framebuffer
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, src.Handle)
            
            let format = 
                if src.Handle <> 0 then
                    let (KeyValue(slot, signature)) = src.Signature.Layout.ColorAttachments |> Seq.find (fun (KeyValue(id, att)) -> att.Name = sem)
                    GL.ReadBuffer(unbox (int ReadBufferMode.ColorAttachment0 + slot))
                    signature.Format
                else
                    if sem <> DefaultSemantic.Colors then failwith "bad"
                    GL.ReadBuffer(ReadBufferMode.BackLeft)
                    TextureFormat.Rgba8
                
            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0)
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
            

            let (pfmt, ptype) = TextureFormat.toFormatAndType format
            let cfmt = TextureFormat.toDownloadFormat format
            let res = PixImage.Create(cfmt, int64 size.X, int64 size.Y)
            let gc = GCHandle.Alloc(res.Array, GCHandleType.Pinned)
            try

                GL.ReadPixels(offset.X, src.Size.Y - size.Y - offset.Y, size.X, size.Y, pfmt, ptype, gc.AddrOfPinnedObject())
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
                
                res.Transformed(ImageTrafo.MirrorY)
            finally
                gc.Free()

        member x.Upload<'T when 'T : unmanaged>(texture : ITextureSubResource, source : NativeTensor4<'T>, format : Col.Format, offset : V3i, size : V3i) : unit =
            let size =
                if size = V3i.Zero then
                    V3i source.Size
                else
                    size

            let level = texture.Level
            let slice = texture.Slice
            let dst = texture.Texture |> unbox<Texture>

            dst |> ResourceValidation.Textures.validateLevel level
            dst |> ResourceValidation.Textures.validateSlice slice
            dst |> ResourceValidation.Textures.validateUploadWindow level offset size
            ctx.Upload(dst, level, slice, offset, size, source, format)

        member x.Download<'T when 'T : unmanaged>(texture : ITextureSubResource, target : NativeTensor4<'T>, format : Col.Format, offset : V3i, size : V3i) : unit =
            let size =
                if size = V3i.Zero then
                    V3i target.Size
                else
                    size

            let level = texture.Level
            let slice = texture.Slice
            let src = texture.Texture |> unbox<Texture>

            src |> ResourceValidation.Textures.validateLevel level
            src |> ResourceValidation.Textures.validateSlice slice
            src |> ResourceValidation.Textures.validateWindow level offset size
            ctx.Download(src, level, slice, offset, size, target, format)

        member x.Copy(src : IFramebufferOutput, srcOffset : V3i, dst : IFramebufferOutput, dstOffset : V3i, size : V3i) : unit =

            let args (offset : V3i) (t : IFramebufferOutput) =
                match t with
                | :? Renderbuffer as rb ->
                    rb |> ResourceValidation.Textures.validateWindow 0 offset size
                    Image.Renderbuffer rb, 0, Range1i(0)

                | :? ITextureLevel as tl ->
                    tl.Texture |> ResourceValidation.Textures.validateSlices tl.Slices.Min (tl.Slices.Size + 1)
                    tl.Texture |> ResourceValidation.Textures.validateLevels tl.Level 1
                    tl.Texture |> ResourceValidation.Textures.validateWindow tl.Level offset size
                    Image.Texture (unbox tl.Texture), tl.Level, tl.Slices

                | _ ->
                    failwithf "[GL] invalid FramebufferOutput: %A" t

            let srcImage, srcLevel, srcSlices = args srcOffset src
            let dstImage, dstLevel, dstSlices = args dstOffset dst

            (srcImage.Samples, dstImage.Samples) ||> ResourceValidation.Textures.validateSamplesForCopy' srcImage.Dimension

            ctx.Copy(srcImage, srcLevel, srcSlices, srcOffset, dstImage, dstLevel, dstSlices, dstOffset, size)

        member x.OnDispose = onDispose.Publish

        member x.AssembleModule (effect : Effect, signature : IFramebufferSignature, topology : IndexedGeometryMode) =
            signature.Link(effect, Range1d(-1.0, 1.0), false, topology)

        member x.ResourceManager = manager :> IResourceManager

        member x.CreateFramebufferSignature(colorAttachments : Map<int, AttachmentSignature>,
                                            depthStencilAttachment : Option<TextureFormat>,
                                            samples : int, layers : int, perLayerUniforms : seq<string>) =
            x.CreateFramebufferSignature(colorAttachments, depthStencilAttachment, samples, layers, perLayerUniforms)

        member x.CreateTexture(size : V3i, dim : TextureDimension, format : TextureFormat, levels : int, samples : int) =
            x.CreateTexture(size, dim, format, levels, samples) :> IBackendTexture

        member x.CreateTextureArray(size : V3i, dim : TextureDimension, format : TextureFormat, levels : int, samples : int, count : int) =
            x.CreateTextureArray(size, dim, format, levels, samples, count) :> IBackendTexture

        member x.DownloadStencil(t : IBackendTexture, target : Matrix<int>, level : int, slice : int, offset : V2i) =
            x.DownloadStencil(t, target, level, slice, offset)

        member x.DownloadDepth(t : IBackendTexture, target : Matrix<float32>, level : int, slice : int, offset : V2i) =
            x.DownloadDepth(t, target, level, slice, offset)

        member x.ResolveMultisamples(source, target, trafo) = x.ResolveMultisamples(source, target, trafo)
        member x.GenerateMipMaps(t : IBackendTexture) = x.GenerateMipMaps t
        member x.CompileRender (signature, set : aset<IRenderObject>) = x.CompileRender(signature, set)
        member x.CompileClear(signature, values) = x.CompileClear(signature, values)

        member x.CreateBuffer(size : nativeint, _ : BufferUsage, storage : BufferStorage) = x.CreateBuffer(size, storage) :> IBackendBuffer
        member x.Upload(src : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) = x.Upload(src, dst, dstOffset, size)
        member x.Download(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, size : nativeint) = x.Download(src, srcOffset, dst, size)
        member x.Copy(src : IBackendBuffer, srcOffset : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) =
            x.Copy(src, srcOffset, dst, dstOffset, size)

        member x.DownloadAsync(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, size : nativeint) : unit -> unit =
            raise <| NotImplementedException()

        member x.Copy(src : IBackendTexture, srcBaseSlice : int, srcBaseLevel : int, dst : IBackendTexture, dstBaseSlice : int, dstBaseLevel : int, slices : int, levels : int) = x.Copy(src, srcBaseSlice, srcBaseLevel, dst, dstBaseSlice, dstBaseLevel, slices, levels)
        member x.PrepareSurface (signature, s : ISurface) : IBackendSurface = x.PrepareSurface(signature, s)

        member x.PrepareRenderObject(fboSignature : IFramebufferSignature, rj : IRenderObject) = x.PrepareRenderObject(fboSignature, rj)

        member x.PrepareTexture (t : ITexture) = x.PrepareTexture t :> IBackendTexture

        member x.PrepareBuffer (b : IBuffer, _ : BufferUsage, storage : BufferStorage) = x.PrepareBuffer(b, storage) :> IBackendBuffer

        member x.CreateStreamingTexture mipMaps = x.CreateStreamingTexture mipMaps

        member x.CreateSparseTexture<'a when 'a : unmanaged> (size : V3i, levels : int, slices : int, dim : TextureDimension, format : Col.Format, brickSize : V3i, maxMemory : int64) : ISparseTexture<'a> =
            failwith "not implemented"


        member x.CreateFramebuffer(signature : IFramebufferSignature, bindings : Map<Symbol, IFramebufferOutput>) : IFramebuffer =
            x.CreateFramebuffer(signature, bindings) :> _


        member x.CreateRenderbuffer(size : V2i, format : TextureFormat, samples : int) : IRenderbuffer =
            x.CreateRenderbuffer(size, format, samples) :> IRenderbuffer


        member x.CreateGeometryPool(types : Map<Symbol, Type>) =
            x.CreateGeometryPool(types)

        member x.MaxLocalSize =
            ctx.MaxComputeWorkGroupSize

        member x.CreateComputeShader (shader : FShade.ComputeShader) =
            ctx.CompileKernel shader :> IComputeShader

        member x.NewInputBinding(shader : IComputeShader, inputs : IUniformProvider) =
            let program = unbox<ComputeProgram> shader
            ComputeInputBinding(manager, program, inputs) :> IComputeInputBinding

        member x.CompileCompute (commands : alist<ComputeCommand>) =
            new ComputeTask(manager, commands, debug.DebugComputeTasks) :> IComputeTask

        member x.Clear(fbo : IFramebuffer, values : ClearValues) =
            use __ = ctx.ResourceLock

            let old = GL.GetInteger(GetPName.DrawFramebufferBinding)

            let fbo = fbo |> unbox<Framebuffer>
            let handle = fbo.Handle
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, handle)
            GL.Check "could not bind framebuffer"

            let drawBuffers = DrawBuffers.ofSignature fbo.Signature
            GL.DrawBuffers(drawBuffers.Length, drawBuffers);

            // assume user has not messed with gl state -> omit resetting color/depth/stencil mask

            // omit check if farmebuffer signature actually contains depth/stencil
            let mutable combinedClearMask = ClearBufferMask.None
            match values.Depth with
            | Some d -> GL.ClearDepth(float d)
                        combinedClearMask <- combinedClearMask ||| ClearBufferMask.DepthBufferBit
            | None -> ()

            match values.Stencil with
            | Some s -> GL.ClearStencil(int s)
                        combinedClearMask <- combinedClearMask ||| ClearBufferMask.StencilBufferBit
            | None -> ()

            if fbo.Signature.ColorAttachments.Count = 1 then
                let _, att = fbo.Signature.ColorAttachments |> Map.toList |> List.head

                match values.Colors.[att.Name] with
                | Some c ->
                    if att.Format.IsIntegerFormat then
                        // clear depth stencil if requested
                        if combinedClearMask <> ClearBufferMask.None then
                            GL.Clear(combinedClearMask)

                        // clear color layer individually
                        GL.ClearBuffer(ClearBuffer.Color, 0, c.Integer.ToArray())

                    else
                        GL.ClearColor(c.Float.X, c.Float.Y, c.Float.Z, c.Float.W)
                        GL.Clear(ClearBufferMask.ColorBufferBit ||| combinedClearMask)

                | None -> failwith "invalid target name"
            else
                // clear depth stencil if requested
                if combinedClearMask <> ClearBufferMask.None then
                    GL.Clear(combinedClearMask)

                // clear each color layer individually
                for KeyValue(i, att) in fbo.Signature.ColorAttachments do
                    match values.Colors.[att.Name] with
                    | Some c ->
                        if att.Format.IsIntegerFormat then
                            GL.ClearBuffer(ClearBuffer.Color, i, c.Integer.ToArray())
                        else
                            GL.ClearBuffer(ClearBuffer.Color, i, c.Float.ToArray())
                        GL.Check "could not clear buffer"
                    | None -> ()

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, old);
            GL.Check "could not unbind framebuffer"

        member x.Clear(texture : IBackendTexture, values : ClearValues) =
            use __ = ctx.ResourceLock

            let fbo = GL.GenFramebuffer()
            GL.Check "could not create framebuffer"

            let old = GL.GetInteger(GetPName.DrawFramebufferBinding)

            try
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo)
                GL.Check "could not bind framebuffer"

                let binding =
                    match texture.Format.Aspect with
                    | TextureAspect.Depth        -> FramebufferAttachment.DepthAttachment
                    | TextureAspect.Stencil      -> FramebufferAttachment.StencilAttachment
                    | TextureAspect.DepthStencil -> FramebufferAttachment.DepthStencilAttachment
                    | _                          -> FramebufferAttachment.ColorAttachment0

                GL.FramebufferTexture(FramebufferTarget.DrawFramebuffer, binding, texture.Handle |> unbox<int>, 0)
                GL.Check "could not attach framebuffer texture"

                if binding <> FramebufferAttachment.ColorAttachment0 then
                    let mask =
                        let depthMask =
                            match values.Depth with
                            | Some value when texture.Format.HasDepth ->
                                GL.ClearDepth(float value)
                                ClearBufferMask.DepthBufferBit

                            | _ ->
                                ClearBufferMask.None

                        let stencilMask =
                            match values.Stencil with
                            | Some value when texture.Format.HasStencil ->
                                GL.ClearStencil(int value)
                                ClearBufferMask.StencilBufferBit

                            | _ ->
                                ClearBufferMask.None

                        depthMask ||| stencilMask

                    if mask <> ClearBufferMask.None then
                        GL.Clear(mask)
                else
                    match values.Colors.[DefaultSemantic.Colors] with
                    | Some color ->
                        GL.DrawBuffer(DrawBufferMode.ColorAttachment0)
                        if texture.Format.IsIntegerFormat then
                            GL.ClearBuffer(ClearBuffer.Color, 0, color.Integer.ToArray())
                        else
                            GL.ClearBuffer(ClearBuffer.Color, 0, color.Float.ToArray())

                    | _ ->
                        ()

                GL.Check "could not clear buffer"

            finally
                GL.DeleteFramebuffer(fbo)
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, old)
                GL.Check "could not delete or unbind framebuffer"

        member x.CreateTextureView(texture : IBackendTexture, levels : Range1i, slices : Range1i, isArray : bool) : IBackendTexture =
            ctx.CreateTextureView(unbox<Texture> texture, levels, slices, isArray) :> IBackendTexture

        member x.CreateTimeQuery() =
            new TimeQuery(ctx) :> ITimeQuery

        member x.CreateOcclusionQuery(precise) =
            new OcclusionQuery(ctx, precise) :> IOcclusionQuery

        member x.CreatePipelineQuery(statistics) =
            use __ = ctx.ResourceLock

            if GL.ARB_pipeline_statistics_query then
                new PipelineQuery(ctx, Set.ofSeq statistics) :> IPipelineQuery
            else
                new GeometryQuery(ctx) :> IPipelineQuery

        member x.SupportedPipelineStatistics =
            use __ = ctx.ResourceLock

            if GL.ARB_pipeline_statistics_query then
                PipelineStatistics.All
            else
                Set.singleton ClippingInputPrimitives

        member x.SupportsRaytracing =
            false

        member x.MaxRayRecursionDepth =
            0

        member x.CreateAccelerationStructure(geometry, usage, allowUpdate) =
            failwith "GL backend does not support raytracing"

        member x.TryUpdateAccelerationStructure(handle, geometry) =
            failwith "GL backend does not support raytracing"

        member x.CompileTrace(pipeline, commands) =
            failwith "GL backend does not support raytracing"

        member x.ShaderCachePath
            with get() = ctx.ShaderCachePath
            and set(value) = ctx.ShaderCachePath <- value


    member x.Copy(src : IBackendTexture, srcBaseSlice : int, srcBaseLevel : int, dst : IBackendTexture, dstBaseSlice : int, dstBaseLevel : int, slices : int, levels : int) =
        src |> ResourceValidation.Textures.validateSlices srcBaseSlice slices
        src |> ResourceValidation.Textures.validateLevels srcBaseLevel levels
        dst |> ResourceValidation.Textures.validateSlices dstBaseSlice slices
        dst |> ResourceValidation.Textures.validateLevels dstBaseLevel levels
        (src, dst) ||> ResourceValidation.Textures.validateSizes srcBaseLevel dstBaseLevel
        (src, dst) ||> ResourceValidation.Textures.validateSamplesForCopy

        let src = unbox<Texture> src
        let dst = unbox<Texture> dst

        let srcSlices = Range1i(srcBaseSlice, srcBaseSlice + slices - 1)
        let dstSlices = Range1i(dstBaseSlice, dstBaseSlice + slices - 1)

        for l in 0 .. levels - 1 do
            let srcLevel = srcBaseLevel + l
            let dstLevel = dstBaseLevel + l
            let srcSize = src.GetSize(srcLevel)
            let dstSize = dst.GetSize(dstLevel)
            let size = min srcSize dstSize

            ctx.Copy(src, srcLevel, srcSlices, V3i.Zero, dst, dstLevel, dstSlices, V3i.Zero, size)

    member x.CreateBuffer(size : nativeint, [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage) =
        size |> ResourceValidation.Buffers.validateSize
        ctx.CreateBuffer(size, storage)

    member x.Upload(src : nativeint, dst : IBackendBuffer, dstOffset : nativeint, sizeInBytes : nativeint) =
        dst |> ResourceValidation.Buffers.validateRange dstOffset sizeInBytes

        use __ = ctx.ResourceLock
        GL.Dispatch.NamedBufferSubData(unbox<int> dst.Handle, dstOffset, sizeInBytes, src)
        GL.Check "could not upload buffer data"
        if RuntimeConfig.SyncUploadsAndFrames then
            GL.Sync()

    member x.Download(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, sizeInBytes : nativeint) =
        src |> ResourceValidation.Buffers.validateRange srcOffset sizeInBytes

        use __ = ctx.ResourceLock
        GL.Dispatch.GetNamedBufferSubData(unbox<int> src.Handle, srcOffset, sizeInBytes, dst)
        GL.Check "could not download buffer data"
        if RuntimeConfig.SyncUploadsAndFrames then
            GL.Sync()

    member x.Copy(src : IBackendBuffer, srcOffset : nativeint, dst : IBackendBuffer, dstOffset : nativeint, sizeInBytes : nativeint) =
        src |> ResourceValidation.Buffers.validateRange srcOffset sizeInBytes
        dst |> ResourceValidation.Buffers.validateRange dstOffset sizeInBytes

        use __ = ctx.ResourceLock
        GL.Dispatch.CopyNamedBufferSubData(unbox<int> src.Handle, unbox<int> dst.Handle, srcOffset, dstOffset, sizeInBytes)
        GL.Check "could not copy buffer data"
        if RuntimeConfig.SyncUploadsAndFrames then
            GL.Sync()

    member x.CreateFramebufferSignature(colorAttachments : Map<int, AttachmentSignature>,
                                        depthStencilAttachment : Option<TextureFormat>,
                                        samples : int, layers : int, perLayerUniforms : seq<string>) =
        ResourceValidation.Framebuffers.validateSignatureParams colorAttachments depthStencilAttachment samples layers

        let perLayerUniforms =
            if perLayerUniforms = null then Set.empty
            else Set.ofSeq perLayerUniforms

        if isNull ctx then
            new FramebufferSignature(
                x, colorAttachments, depthStencilAttachment,
                samples, layers, perLayerUniforms
            ) :> IFramebufferSignature
        else
            ctx.CreateFramebufferSignature(
                colorAttachments, depthStencilAttachment,
                samples, layers, perLayerUniforms
            ) :> IFramebufferSignature

    member x.PrepareTexture (t : ITexture) = ctx.CreateTexture t
    member x.PrepareBuffer (b : IBuffer, [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage) = ctx.CreateBuffer(b, storage)
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

    member x.CreateStreamingTexture(mipMaps : bool) =
        ctx.CreateStreamingTexture(mipMaps) :> IStreamingTexture

    member x.ResourceManager = manager

    member x.CompileRender (signature : IFramebufferSignature, set : aset<IRenderObject>) =
        let set = EffectDebugger.Hook set

        if RuntimeConfig.UseNewRenderTask then
            new RenderTasks.NewRenderTask(manager, signature, set) :> IRenderTask
        else
            new RenderTasks.RenderTask(manager, signature, set, debug.DebugRenderTasks) :> IRenderTask

    member x.PrepareRenderObject(signature : IFramebufferSignature, rj : IRenderObject) : IPreparedRenderObject =
        PreparedCommand.ofRenderObject signature manager rj :> IPreparedRenderObject

    member x.CompileClear(signature : IFramebufferSignature, values : aval<ClearValues>) : IRenderTask =
        new RenderTasks.ClearTask(x, ctx, signature, values) :> IRenderTask

    member x.ResolveMultisamples(ms : IFramebufferOutput, srcOffset : V2i, ss : IBackendTexture, dstOffset : V2i, dstLayer : int, size : V2i, trafo : ImageTrafo) =
        Operators.using ctx.ResourceLock (fun _ ->
            let oldRead = GL.GetInteger(GetPName.ReadFramebufferBinding)
            let oldDraw = GL.GetInteger(GetPName.DrawFramebufferBinding)

            let targetTex = ss |> unbox<Texture>
            let readFbo = GL.GenFramebuffer()
            let drawFbo = GL.GenFramebuffer()

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer,readFbo)
            GL.Check "could not bind read framebuffer"
            let mutable srcAtt = FramebufferAttachment.ColorAttachment0

            match ms with
            | :? Renderbuffer as ms ->
                if TextureFormat.hasDepth ms.Format then srcAtt <- FramebufferAttachment.DepthStencilAttachment
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

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, oldRead)
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, oldDraw)
            GL.DeleteFramebuffer readFbo
            GL.DeleteFramebuffer drawFbo
            GL.Check "error cleanup"
        )

    member x.ResolveMultisamples(ms : IFramebufferOutput, ss : IBackendTexture, trafo : ImageTrafo) =
        x.ResolveMultisamples(ms, V2i.Zero, ss, V2i.Zero, 0, ms.Size.XY, trafo)

    member x.GenerateMipMaps(t : IBackendTexture) =
        match t with
        | :? Texture as t ->
            if t.MipMapLevels > 1 then
                let target = TextureTarget.ofTexture t

                Operators.using ctx.ResourceLock (fun _ ->
                    t.CheckMipmapGenerationSupport()

                    GL.BindTexture(target, t.Handle)
                    GL.Check "could not bind texture"

                    GL.GenerateMipmap(unbox (int target))
                    GL.Check "could not generate mipMaps"

                    GL.BindTexture(target, 0)
                    GL.Check "could not unbind texture"
                )

        | _ ->
            failwithf "[GL] unsupported texture: %A" t

    member x.DownloadStencil(t : IBackendTexture, target : Matrix<int>, level : int, slice : int, offset : V2i) =
        t |> ResourceValidation.Textures.validateLevel level
        t |> ResourceValidation.Textures.validateSlice slice
        t |> ResourceValidation.Textures.validateWindow2D level offset (V2i target.Size)
        t |> ResourceValidation.Textures.validateStencilFormat
        ctx.DownloadStencil(unbox<Texture> t, level, slice, offset, target)

    member x.DownloadDepth(t : IBackendTexture, target : Matrix<float32>, level : int, slice : int, offset : V2i) =
        t |> ResourceValidation.Textures.validateLevel level
        t |> ResourceValidation.Textures.validateSlice slice
        t |> ResourceValidation.Textures.validateWindow2D level offset (V2i target.Size)
        t |> ResourceValidation.Textures.validateDepthFormat
        ctx.DownloadDepth(unbox<Texture> t, level, slice, offset, target)

    member x.CreateFramebuffer(signature : IFramebufferSignature, bindings : Map<Symbol, IFramebufferOutput>) =
        ResourceValidation.Framebuffers.validateAttachments signature bindings

        let colors =
            signature.ColorAttachments
            |> Map.toList
            |> List.map (fun (i, att) ->
                i, att.Name, bindings.[att.Name]
            )

        let depthStencil =
            signature.DepthStencilAttachment |> Option.map (fun _ ->
                bindings.[DefaultSemantic.DepthStencil]
            )

        ctx.CreateFramebuffer(signature, colors, depthStencil)

    member x.CreateTexture(size : V3i, dim : TextureDimension, format : TextureFormat, levels : int, samples : int) =
        ResourceValidation.Textures.validateCreationParams dim size levels samples
        ctx.CreateTexture(size, dim, format, 0, levels, samples)

    member x.CreateTextureArray(size : V3i, dim : TextureDimension, format : TextureFormat, levels : int, samples : int, count : int) =
        ResourceValidation.Textures.validateCreationParamsArray dim size levels samples count
        ctx.CreateTexture(size, dim, format, count, levels, samples)


    member x.CreateRenderbuffer(size : V2i, format : TextureFormat, samples : int) : Renderbuffer =
        if samples < 1 then raise <| ArgumentException("[Renderbuffer] samples must be greater than 0")
        ctx.CreateRenderbuffer(size, format, samples)

    member x.CreateGeometryPool(types : Map<Symbol, Type>) =
        new SparseBufferGeometryPool(ctx, types) :> IGeometryPool