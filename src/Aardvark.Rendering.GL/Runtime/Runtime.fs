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

    let mutable ctx : Context = null
    let mutable manager : ResourceManager = null

    let debug = DebugConfig.unbox debug

    let onDispose = Event<unit>()

    member x.Context = ctx

    member x.ContextLock = ctx.ResourceLock :> IDisposable

    member x.ShaderDepthRange = Range1d(-1.0, 1.0)

    /// Returns whether the inputs gl_Layer and gl_ViewportIndex can be used
    /// in fragment shaders. If not a custom output / input must be used for
    /// layered effects.
    member x.SupportsLayeredShaderInputs =
        ctx.Driver.glsl >= Version(4, 3, 0)

    member x.DebugConfig = debug

    member x.DebugLabelsEnabled = debug.DebugLabels

    member x.ShaderCachePath
        with get() = ctx.ShaderCachePath
        and set(value) = ctx.ShaderCachePath <- value

    member x.Initialize(context : Context) =
        if ctx <> null then
            Log.warn "Runtime already initialized"

        ctx <- context
        manager <- new ResourceManager(context, None)

        GL.CheckErrors <- debug.ErrorFlagCheck

        Operators.using context.ResourceLock (fun _ ->

            try
                Log.startTimed "initializing OpenGL runtime"

                let driver = context.Driver
                driver |> Driver.print 4

                // GL_CONTEXT_CORE_PROFILE_BIT 1
                // GL_CONTEXT_COMPATIBILITY_PROFILE_BIT 2
                let profileType = if driver.profileMask = 1 then "Core" elif driver.profileMask = 2 then "Compatibility" else ""

                Log.line "vendor:   %s" driver.vendor
                Log.line "renderer: %s" driver.renderer
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
        if manager <> null then
            manager.Dispose()
            manager <- null

        if ctx <> null then
            onDispose.Trigger()
            ctx.Dispose()
            ctx <- null

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IRuntime with

        member x.DeviceCount = 1
        member x.ShaderDepthRange = x.ShaderDepthRange
        member x.SupportsLayeredShaderInputs = x.SupportsLayeredShaderInputs
        member x.DebugConfig = x.DebugConfig
        member x.DebugLabelsEnabled = x.DebugLabelsEnabled
        member x.ContextLock = x.ContextLock

        member x.Upload<'T when 'T : unmanaged>(texture : ITextureSubResource, source : NativeTensor4<'T>, format : Col.Format, offset : V3i, size : V3i) =
            x.Upload(texture, source, format, offset, size)

        member x.Download<'T when 'T : unmanaged>(texture : ITextureSubResource, target : NativeTensor4<'T>, format : Col.Format, offset : V3i, size : V3i) =
            x.Download(texture, target, format, offset, size)


        member x.OnDispose = x.OnDispose

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

        member x.Blit(src, srcRegion, dst, dstRegion) = x.Blit(src, srcRegion, dst, dstRegion)

        member x.PrepareEffect(signature, effect, topology) = x.PrepareEffect(signature, effect, topology)

        member x.PrepareRenderObject(fboSignature : IFramebufferSignature, rj : IRenderObject) = x.PrepareRenderObject(fboSignature, rj)

        member x.PrepareTexture (t : ITexture) = x.PrepareTexture t :> IBackendTexture

        member x.PrepareBuffer (b : IBuffer, _ : BufferUsage, storage : BufferStorage) = x.PrepareBuffer(b, storage) :> IBackendBuffer

        member x.CreateStreamingTexture mipMaps = x.CreateStreamingTexture mipMaps

        member x.CreateSparseTexture<'T when 'T : unmanaged> (size : V3i, levels : int, slices : int, dimension : TextureDimension, format : Col.Format, brickSize : V3i, maxMemory : int64) : ISparseTexture<'T> =
            raise <| NotImplementedException()

        member x.CreateFramebuffer(signature : IFramebufferSignature, bindings : Map<Symbol, IFramebufferOutput>) : IFramebuffer =
            x.CreateFramebuffer(signature, bindings) :> _

        member x.CreateRenderbuffer(size : V2i, format : TextureFormat, samples : int) : IRenderbuffer =
            x.CreateRenderbuffer(size, format, samples) :> IRenderbuffer

        member x.Copy(src : IFramebuffer, dst : IFramebuffer) =
            x.Copy(src, dst)

        member x.ReadPixels(src : IFramebuffer, sem : Symbol, offset : V2i, size : V2i) =
            x.ReadPixels(src, sem, offset, size)

        member x.Clear(fbo : IFramebuffer, values : ClearValues) =
            x.Clear(fbo, values)

        member x.Clear(texture : IBackendTexture, values : ClearValues) =
            x.Clear(texture, values)

        member x.CreateTextureView(texture, levels, slices, isArray) =
            x.CreateTextureView(texture, levels, slices, isArray)

        member x.CreateGeometryPool(types : Map<Symbol, Type>) =
            x.CreateGeometryPool(types)

        member x.MaxLocalSize =
            x.MaxLocalSize

        member x.CreateComputeShader(shader) =
            x.CreateComputeShader(shader)

        member x.CreateInputBinding(shader, inputs) =
            x.CreateInputBinding(shader, inputs)

        member x.CompileCompute (commands) =
            x.CompileCompute commands

        member x.CreateTimeQuery() =
            x.CreateTimeQuery()

        member x.CreateOcclusionQuery(precise) =
            x.CreateOcclusionQuery(precise)

        member x.CreatePipelineQuery(statistics) =
            x.CreatePipelineQuery(statistics)

        member x.SupportedPipelineStatistics =
            x.SupportedPipelineStatistics

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
            with get() = x.ShaderCachePath
            and set(value) = x.ShaderCachePath <- value

        member x.CreateLodRenderer(config, data) =
            x.CreateLodRenderer(config, data)


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

    member x.Blit(src : IFramebufferOutput, srcRegion : Box3i, dst : IFramebufferOutput, dstRegion : Box3i) =

        let args (region : Box3i) (t : IFramebufferOutput) =
            match t with
            | :? Renderbuffer as rb ->
                rb |> ResourceValidation.Textures.validateBlitRegion 0 region
                Image.Renderbuffer rb, 0, Range1i(0)

            | :? ITextureLevel as tl ->
                tl.Texture |> ResourceValidation.Textures.validateSlices tl.Slices.Min (tl.Slices.Size + 1)
                tl.Texture |> ResourceValidation.Textures.validateLevels tl.Level 1
                tl.Texture |> ResourceValidation.Textures.validateBlitRegion tl.Level region
                Image.Texture (unbox tl.Texture), tl.Level, tl.Slices

            | _ ->
                failf "invalid framebuffer output: %A" t

        let srcImage, srcLevel, srcSlices = args srcRegion src
        let dstImage, dstLevel, dstSlices = args dstRegion dst

        (srcImage.Samples, dstImage.Samples) ||> ResourceValidation.Textures.validateSamplesForCopy' srcImage.Dimension

        if srcRegion.IsValid && srcRegion.Size = dstRegion.Size then
            ctx.Copy(
                srcImage, srcLevel, srcSlices, srcRegion.Min,
                dstImage, dstLevel, dstSlices, dstRegion.Min,
                srcRegion.Size
            )
        else
            if (srcImage.Samples > 1 || dstImage.Samples > 1) && abs srcRegion.Size <> abs dstRegion.Size then
                failf "blitting from or to multisampled buffers is only supported if the source and target region dimensions match"

            ctx.Blit(
                srcImage, srcLevel, srcSlices, srcRegion.Min, srcRegion.Size,
                dstImage, dstLevel, dstSlices, dstRegion.Min, dstRegion.Size,
                true
            )

    member x.Upload<'T when 'T : unmanaged>(texture : ITextureSubResource, source : NativeTensor4<'T>, format : Col.Format,
                                            [<Optional; DefaultParameterValue(V3i())>] offset : V3i,
                                            [<Optional; DefaultParameterValue(V3i())>] size : V3i) : unit =
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

    member x.Download<'T when 'T : unmanaged>(texture : ITextureSubResource, target : NativeTensor4<'T>, format : Col.Format,
                                              [<Optional; DefaultParameterValue(V3i())>] offset : V3i,
                                              [<Optional; DefaultParameterValue(V3i())>] size : V3i) : unit =
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

    member x.CreateBuffer(size : nativeint, [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage) =
        size |> ResourceValidation.Buffers.validateSize
        ctx.CreateBuffer(size, storage)

    member x.Upload(src : nativeint, dst : IBackendBuffer, dstOffset : nativeint, sizeInBytes : nativeint) =
        dst |> ResourceValidation.Buffers.validateRange dstOffset sizeInBytes

        use __ = ctx.ResourceLock
        GL.Dispatch.NamedBufferSubData(int dst.Handle, dstOffset, sizeInBytes, src)
        GL.Check "could not upload buffer data"
        if RuntimeConfig.SyncUploadsAndFrames then
            GL.Sync()

    member x.Download(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, sizeInBytes : nativeint) =
        src |> ResourceValidation.Buffers.validateRange srcOffset sizeInBytes

        use __ = ctx.ResourceLock
        GL.Dispatch.GetNamedBufferSubData(int src.Handle, srcOffset, sizeInBytes, dst)
        GL.Check "could not download buffer data"
        if RuntimeConfig.SyncUploadsAndFrames then
            GL.Sync()

    member x.Copy(src : IBackendBuffer, srcOffset : nativeint, dst : IBackendBuffer, dstOffset : nativeint, sizeInBytes : nativeint) =
        src |> ResourceValidation.Buffers.validateRange srcOffset sizeInBytes
        dst |> ResourceValidation.Buffers.validateRange dstOffset sizeInBytes

        use __ = ctx.ResourceLock
        GL.Dispatch.CopyNamedBufferSubData(int src.Handle, int dst.Handle, srcOffset, dstOffset, sizeInBytes)
        GL.Check "could not copy buffer data"
        if RuntimeConfig.SyncUploadsAndFrames then
            GL.Sync()

    member x.CreateFramebufferSignature(colorAttachments : Map<int, AttachmentSignature>,
                                        depthStencilAttachment : Option<TextureFormat>,
                                        [<Optional; DefaultParameterValue(1)>] samples : int,
                                        [<Optional; DefaultParameterValue(1)>] layers : int,
                                        [<Optional; DefaultParameterValue(null : seq<string>)>] perLayerUniforms : seq<string>) =
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

    member x.PrepareTexture(texture : ITexture) =
        ResourceValidation.Textures.validateForPrepare texture
        ctx.CreateTexture(texture, ValueNone)

    member x.PrepareBuffer (b : IBuffer, [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage) = ctx.CreateBuffer(b, storage)
    member x.PrepareEffect (signature : IFramebufferSignature, effect : FShade.Effect, topology : IndexedGeometryMode) : IBackendSurface =
        Operators.using ctx.ResourceLock (fun d ->
            let _, program = ctx.CreateProgram(signature, Surface.Effect effect, topology)
            AVal.force program :> IBackendSurface
        )

    member x.CreateStreamingTexture(mipMaps : bool) =
        ctx.CreateStreamingTexture(mipMaps) :> IStreamingTexture

    member x.OnDispose = onDispose.Publish

    member x.ResourceManager = manager

    member x.CompileRender (signature : IFramebufferSignature, set : aset<IRenderObject>) =
        let set = ShaderDebugger.hookRenderObjects set

        if RuntimeConfig.UseNewRenderTask then
            new RenderTasks.NewRenderTask(manager, signature, set, debug.DebugRenderTasks) :> IRenderTask
        else
            new RenderTasks.RenderTask(manager, signature, set, debug.DebugRenderTasks) :> IRenderTask

    member x.PrepareRenderObject(signature : IFramebufferSignature, rj : IRenderObject) : IPreparedRenderObject =
        PreparedCommand.ofRenderObject signature manager rj :> IPreparedRenderObject

    member x.CompileClear(signature : IFramebufferSignature, values : aval<ClearValues>) : IRenderTask =
        new RenderTasks.ClearTask(x, ctx, signature, values) :> IRenderTask

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

    member x.DownloadStencil(t : IBackendTexture, target : Matrix<int>,
                             [<Optional; DefaultParameterValue(0)>] level : int,
                             [<Optional; DefaultParameterValue(0)>] slice : int,
                             [<Optional; DefaultParameterValue(V2i())>] offset : V2i) =
        t |> ResourceValidation.Textures.validateLevel level
        t |> ResourceValidation.Textures.validateSlice slice
        t |> ResourceValidation.Textures.validateWindow2D level offset (V2i target.Size)
        t |> ResourceValidation.Textures.validateStencilFormat
        ctx.DownloadStencil(unbox<Texture> t, level, slice, offset, target)

    member x.DownloadDepth(t : IBackendTexture, target : Matrix<float32>,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int,
                           [<Optional; DefaultParameterValue(V2i())>] offset : V2i) =
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

    member x.CreateRenderbuffer(size : V2i, format : TextureFormat,
                                [<Optional; DefaultParameterValue(1)>] samples : int) : Renderbuffer =
        if samples < 1 then raise <| ArgumentException("[Renderbuffer] samples must be greater than 0")
        ctx.CreateRenderbuffer(size, format, samples)

    member x.CreateTextureView(texture : IBackendTexture, levels : Range1i, slices : Range1i, isArray : bool) : IBackendTexture =
        ctx.CreateTextureView(unbox<Texture> texture, levels, slices, isArray) :> IBackendTexture

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

            res.TransformedPixImage(ImageTrafo.MirrorY)
        finally
            gc.Free()

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

            match values.[att.Name] with
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
                match values.[att.Name] with
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

            GL.FramebufferTexture(FramebufferTarget.DrawFramebuffer, binding, int texture.Handle, 0)
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
                match values.[DefaultSemantic.Colors] with
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

    member x.MaxLocalSize =
        ctx.MaxComputeWorkGroupSize

    member x.CreateComputeShader (shader : FShade.ComputeShader) =
        ctx.CreateComputeShader shader

    member x.CreateInputBinding(shader : IComputeShader, inputs : IUniformProvider) =
        ComputeInputBinding(manager, shader, inputs) :> IComputeInputBinding

    member x.CompileCompute (commands : alist<ComputeCommand>) =
        new ComputeTask(manager, commands, debug.DebugComputeTasks) :> IComputeTask

    member x.CreateGeometryPool(types : Map<Symbol, Type>) =
        new SparseBufferGeometryPool(ctx, types) :> IGeometryPool

    member x.CreateTimeQuery() =
        new TimeQuery(ctx) :> ITimeQuery

    member x.CreateOcclusionQuery([<Optional; DefaultParameterValue(true)>] precise : bool) =
        new OcclusionQuery(ctx, precise) :> IOcclusionQuery

    member x.CreatePipelineQuery(statistics : seq<PipelineStatistics>) =
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

    member x.CreateLodRenderer(config : LodRendererConfig, data : aset<LodTreeInstance>) =
        new LodRenderer(x.ResourceManager, config, data) :> IPreparedRenderObject