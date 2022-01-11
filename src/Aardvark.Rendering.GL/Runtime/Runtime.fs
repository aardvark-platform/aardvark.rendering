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

        member x.Upload<'a when 'a : unmanaged>(texture : ITextureSubResource, source : NativeTensor4<'a>, offset : V3i, size : V3i) : unit =
            let level = texture.Level
            let slice = texture.Slice
            let dst = texture.Texture |> unbox<Texture>

            dst |> ResourceValidation.Textures.validateLevel level
            dst |> ResourceValidation.Textures.validateSlice slice
            dst |> ResourceValidation.Textures.validateWindow level offset size
            ctx.Upload(dst, level, slice, offset, size, source)
  
        member x.Download<'a when 'a : unmanaged>(texture : ITextureSubResource, target : NativeTensor4<'a>, offset : V3i, size : V3i) : unit =
            let level = texture.Level
            let slice = texture.Slice
            let src = texture.Texture |> unbox<Texture>

            src |> ResourceValidation.Textures.validateLevel level
            src |> ResourceValidation.Textures.validateSlice slice
            src |> ResourceValidation.Textures.validateWindow level offset size
            ctx.Download(src, level, slice, offset, size, target)

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
        member x.ContextLock = ctx.ResourceLock :> IDisposable
        member x.CompileRender (signature, engine : BackendConfiguration, set : aset<IRenderObject>) = x.CompileRender(signature, engine,set)
        member x.CompileClear(signature, values) = x.CompileClear(signature, values)

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

        member x.CreateStreamingTexture mipMaps = x.CreateStreamingTexture mipMaps
        member x.DeleteStreamingTexture tex = x.DeleteStreamingTexture tex

        member x.CreateSparseTexture<'a when 'a : unmanaged> (size : V3i, levels : int, slices : int, dim : TextureDimension, format : Col.Format, brickSize : V3i, maxMemory : int64) : ISparseTexture<'a> =
            failwith "not implemented"


        member x.CreateFramebuffer(signature : IFramebufferSignature, bindings : Map<Symbol, IFramebufferOutput>) : IFramebuffer =
            x.CreateFramebuffer(signature, bindings) :> _


        member x.CreateRenderbuffer(size : V2i, format : TextureFormat, samples : int) : IRenderbuffer =
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

        member x.Clear(fbo : IFramebuffer, values : ClearValues) =
            use __ = ctx.ResourceLock

            let fbo = fbo |> unbox<Framebuffer>
            let handle = fbo.Handle
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, handle)
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

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Check "could not unbind framebuffer"

        member x.ClearColor(texture : IBackendTexture, color : ClearColor) =
            if not <| (texture.Format.HasDepth || texture.Format.HasStencil) then
                use __ = ctx.ResourceLock

                let fbo = GL.GenFramebuffer()
                GL.Check "could not create framebuffer"

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo)
                GL.Check "could not bind framebuffer"

                GL.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, texture.Handle |> unbox<int>, 0)
                GL.Check "could not attach framebuffer texture"

                GL.DrawBuffer(DrawBufferMode.ColorAttachment0)
                if texture.Format.IsIntegerFormat then
                    GL.ClearBuffer(ClearBuffer.Color, 0, color.Integer.ToArray())
                else
                    GL.ClearBuffer(ClearBuffer.Color, 0, color.Float.ToArray())
                GL.Check "could not clear buffer"

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
                GL.Check "could not unbind framebuffer"

                GL.DeleteFramebuffer(fbo)
                GL.Check "could not delete framebuffer"

        member x.ClearDepthStencil(texture : IBackendTexture, depth : Option<ClearDepth>, stencil : Option<ClearStencil>) =
            if texture.Format.HasDepth || texture.Format.HasStencil then
                let binding =
                    match (depth, stencil) with
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
                        GL.ClearDepth(float d)
                        GL.ClearStencil(int s)
                        GL.Clear(ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)
                    | Some d, None ->
                        GL.ClearDepth(float d)
                        GL.Clear(ClearBufferMask.DepthBufferBit)
                    | None, Some s ->
                        GL.ClearStencil(int s)
                        GL.Clear(ClearBufferMask.StencilBufferBit)
                    | _ -> ()
                    GL.Check "could not clear"

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

    member x.CreateBuffer(size : nativeint) =
        ctx.CreateBuffer(int size)

    member x.Upload(src : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) =
        use __ = ctx.ResourceLock
        GL.Dispatch.NamedBufferSubData(unbox<int> dst.Handle, dstOffset, size, src)
        GL.Check "could not upload buffer data"
        if RuntimeConfig.SyncUploadsAndFrames then
            GL.Sync()

    member x.Download(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, size : nativeint) =
        use __ = ctx.ResourceLock
        GL.Dispatch.GetNamedBufferSubData(unbox<int> src.Handle, srcOffset, size, dst)
        GL.Check "could not download buffer data"
        if RuntimeConfig.SyncUploadsAndFrames then
            GL.Sync()

    member x.Copy(src : IBackendBuffer, srcOffset : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) =
        use __ = ctx.ResourceLock
        GL.Dispatch.CopyNamedBufferSubData(unbox<int> src.Handle, unbox<int> dst.Handle, srcOffset, dstOffset, size)
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

        { new Object() with
            member _.ToString() = sprintf "{ ColorAttachments = %A; DepthStencilAttachment = %A }" colorAttachments depthStencilAttachment
          interface IFramebufferSignature with
            member _.Runtime = x :> IFramebufferRuntime
            member _.Samples = samples
            member _.ColorAttachments = colorAttachments
            member _.DepthStencilAttachment = depthStencilAttachment
            member _.LayerCount = layers
            member _.PerLayerUniforms = perLayerUniforms 
            member _.Dispose() = () }

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

        if RuntimeConfig.UseNewRenderTask then
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

    member x.CompileClear(signature : IFramebufferSignature, values : aval<ClearValues>) : IRenderTask =
        new RenderTasks.ClearTask(x, ctx, signature, values) :> IRenderTask

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
                    let target = TextureTarget.ofTexture t
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