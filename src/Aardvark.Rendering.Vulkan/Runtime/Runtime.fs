namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open FShade
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.Raytracing
open FSharp.Data.Adaptive
open ComputeTaskInternals

#nowarn "9"

type Runtime(device : Device) as this =
    let instance = device.Instance
    do device.Runtime <- this

    let debug = device.DebugConfig
    let manager = new ResourceManager(device)

    // install debug output to file (and errors/warnings to console)
    let debugSubscription = instance.SetupDebugMessageOutput(debug.DebugReport)

    let onDispose = Event<unit>()

    member x.ShaderCachePath
        with get() = device.ShaderCachePath
        and set v = device.ShaderCachePath <- v

    member x.Device = device
    member x.ResourceManager = manager
    member x.ContextLock = device.Token :> IDisposable

    member x.DeviceCount = device.PhysicalDevices.Length

    member x.ShaderDepthRange = FShadeConfig.depthRange

    member x.SupportsLayeredShaderInputs = true

    member x.DebugConfig = debug

    member x.DebugLabelsEnabled = instance.DebugLabelsEnabled

    member x.CreateStreamingTexture (mipMaps : bool) : IStreamingTexture =
        raise <| NotImplementedException()

    member x.CreateSparseTexture<'T when 'T : unmanaged> (size : V3i, levels : int, slices : int, dimension : TextureDimension, format : Col.Format, brickSize : V3i, maxMemory : int64) : ISparseTexture<'T> =
        raise <| NotImplementedException()
        // TODO: Needs to be ported to the new memory allocator if needed
        // new SparseTextureImplemetation.DoubleBufferedSparseImage<'T>(
        //     device,
        //     size, levels, slices,
        //     dimension, format,
        //     VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit,
        //     brickSize,
        //     maxMemory
        // ) :> ISparseTexture<_>

    member x.DownloadStencil(t : IBackendTexture, target : Matrix<int>,
                             [<Optional; DefaultParameterValue(0)>] level : int,
                             [<Optional; DefaultParameterValue(0)>] slice : int,
                             [<Optional; DefaultParameterValue(V2i())>] offset : V2i) =
        t |> ResourceValidation.Textures.validateLevel level
        t |> ResourceValidation.Textures.validateSlice slice
        t |> ResourceValidation.Textures.validateWindow2D level offset (V2i target.Size)
        t |> ResourceValidation.Textures.validateStencilFormat

        let image = unbox<Image> t
        device.DownloadStencil(image.[TextureAspect.Stencil, level, slice], target, offset)

    member x.DownloadDepth(t : IBackendTexture, target : Matrix<float32>,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int,
                           [<Optional; DefaultParameterValue(V2i())>] offset : V2i) =
        t |> ResourceValidation.Textures.validateLevel level
        t |> ResourceValidation.Textures.validateSlice slice
        t |> ResourceValidation.Textures.validateWindow2D level offset (V2i target.Size)
        t |> ResourceValidation.Textures.validateDepthFormat

        let image = unbox<Image> t
        device.DownloadDepth(image.[TextureAspect.Depth, level, slice], target, offset)

    member x.PrepareRenderObject(fboSignature : IFramebufferSignature, rj : IRenderObject) =
        manager.PrepareRenderObject(unbox fboSignature, rj) :> IPreparedRenderObject

    member x.CompileRender(renderPass : IFramebufferSignature, cmd : RuntimeCommand) =
        new CommandTask(manager, unbox renderPass, cmd)

    member x.CompileRender (renderPass : IFramebufferSignature, set : aset<IRenderObject>) =
        let set = ShaderDebugger.hookRenderObjects set
        new CommandTask(manager, unbox renderPass, RuntimeCommand.Render set) :> IRenderTask

    member x.CompileClear(signature : IFramebufferSignature, values : aval<ClearValues>) : IRenderTask =
        new ClearTask(device, unbox signature, values) :> IRenderTask

    member x.CreateFramebufferSignature(colorAttachments : Map<int, AttachmentSignature>,
                                        depthStencilAttachment : Option<TextureFormat>,
                                        [<Optional; DefaultParameterValue(1)>] samples : int,
                                        [<Optional; DefaultParameterValue(1)>] layers : int,
                                        [<Optional; DefaultParameterValue(null : seq<string>)>] perLayerUniforms : seq<string>) =
        ResourceValidation.Framebuffers.validateSignatureParams colorAttachments depthStencilAttachment samples layers

        let perLayerUniforms =
            if perLayerUniforms = null then Set.empty
            else Set.ofSeq perLayerUniforms

        device.CreateRenderPass(
            colorAttachments, depthStencilAttachment,
            samples, layers, perLayerUniforms
        )
        :> IFramebufferSignature

    member x.CreateFramebuffer(signature : IFramebufferSignature, bindings : Map<Symbol, IFramebufferOutput>) : IFramebuffer =
        ResourceValidation.Framebuffers.validateAttachments signature bindings

        let createImageView (name : Symbol) (output : IFramebufferOutput) =
            match output with
            | :? Image as img ->
                device.CreateOutputImageView(img, 0, 1, 0, 1)

            | :? ITextureLevel as l ->
                let image = unbox<Image> l.Texture
                device.CreateOutputImageView(image, l.Levels, l.Slices)

            | _ -> failf "invalid framebuffer attachment %A: %A" name output

        let views =
            bindings |> Map.choose (fun s o ->
                if signature.Contains s then
                    Some <| createImageView s o
                else
                    None
            )

        device.CreateFramebuffer(unbox signature, views) :> IFramebuffer

    member x.PrepareEffect (signature : IFramebufferSignature, effect : FShade.Effect, topology : IndexedGeometryMode) =
        device.CreateShaderProgram(unbox<RenderPass> signature, effect, topology)

    member x.PrepareTexture (t : ITexture, [<Optional; DefaultParameterValue(false)>] export : bool) =
        ResourceValidation.Textures.validateForPrepare t

        // Note: Image properties are only relevant for NullTexture at the moment.
        // These are used to create the right kind of texture for a given sampler.
        // Other texture types could use those properties for validation in the future.
        // Since the PrepareTexture() API does not pass these properties, we do not allow preparing NullTexture
        device.CreateImage(t, export) :> IBackendTexture

    member x.PrepareBuffer (data : IBuffer,
                            [<Optional; DefaultParameterValue(0UL)>] alignment : uint64,
                            [<Optional; DefaultParameterValue(BufferUsage.All)>] usage : BufferUsage,
                            [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage,
                            [<Optional; DefaultParameterValue(false)>] export : bool) =
        let flags = VkBufferUsageFlags.ofBufferUsage usage
        let memory = if storage = BufferStorage.Device then device.DeviceMemory else device.HostMemory
        memory.CreateBuffer(flags, data, alignment, export) :> IBackendBuffer

    member private x.CreateTextureInner(size : V3i, dim : TextureDimension, format : TextureFormat, levels : int, samples : int, count : int, export : ImageExport) =
        let layout =
            VkImageLayout.ShaderReadOnlyOptimal

        let usage =
            let def =
                VkImageUsageFlags.TransferSrcBit |||
                VkImageUsageFlags.TransferDstBit |||
                VkImageUsageFlags.SampledBit

            if format.HasDepth || format.HasStencil then
                def ||| VkImageUsageFlags.DepthStencilAttachmentBit
            else
                def ||| VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.StorageBit

        let img = device.CreateImage(size, levels, count, samples, dim, format, usage, export)
        device.GraphicsFamily.run {
            do! Command.TransformLayout(img, layout)
        }
        img :> IBackendTexture

    member x.CreateTexture(size : V3i, dim : TextureDimension, format : TextureFormat, levels : int, samples : int,
                           [<Optional; DefaultParameterValue(false)>] export : bool) : IBackendTexture =
        ResourceValidation.Textures.validateCreationParams dim size levels samples
        x.CreateTextureInner(size, dim, format, levels, samples, 1, if export then ImageExport.Enable false else ImageExport.Disable)

    member x.CreateTextureArray(size : V3i, dim : TextureDimension, format : TextureFormat, levels : int, samples : int, count : int,
                                [<Optional; DefaultParameterValue(false)>] export : bool) : IBackendTexture =
        ResourceValidation.Textures.validateCreationParamsArray dim size levels samples count
        x.CreateTextureInner(size, dim, format, levels, samples, count, if export then ImageExport.Enable true else ImageExport.Disable)

    member x.CreateRenderbuffer(size : V2i, format : TextureFormat, samples : int) : IRenderbuffer =
        if samples < 1 then raise <| ArgumentException("[Renderbuffer] samples must be greater than 0")

        let isDepth =
            format.HasDepth || format.HasStencil

        let layout =
            if isDepth then VkImageLayout.DepthStencilAttachmentOptimal
            else VkImageLayout.ColorAttachmentOptimal

        let usage =
            if isDepth then VkImageUsageFlags.DepthStencilAttachmentBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.TransferSrcBit
            else VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.TransferSrcBit

        let img = device.CreateImage(V3i(size.X, size.Y, 1), 1, 1, samples, TextureDimension.Texture2D, format, usage)
        device.GraphicsFamily.run {
            do! Command.TransformLayout(img, layout)
        }
        img :> IRenderbuffer

    member x.GenerateMipMaps(t : IBackendTexture) =
        let img = unbox<Image> t
        let aspect = (img :> IBackendTexture).Format.Aspect

        if img.MipMapLevels > 1 then
            device.GraphicsFamily.run {
                do! Command.GenerateMipMaps(img.[aspect])
            }

    member x.ResolveMultisamples(source : IFramebufferOutput, target : IBackendTexture, trafo : ImageTrafo) =
        let src =
            match source with
            | :? Image as img ->
                let flags = VkFormat.toAspect img.Format
                img.[unbox (int flags), 0, 0 .. 0]

            | :? ITextureLevel as l ->
                let image = unbox<Image> l.Texture
                let flags = VkFormat.toAspect image.Format
                image.[unbox (int flags), l.Level, l.Slices.Min .. l.Slices.Max]

            | _ ->
                failf "invalid input for blit: %A" source

        let dst =
            let img = unbox<Image> target
            img.[src.Aspect, 0, 0 .. src.SliceCount - 1]

        device.eventually {
            let srcLayout = src.Image.Layout
            let dstLayout = dst.Image.Layout

            do! Command.TransformLayout(src, srcLayout, VkImageLayout.TransferSrcOptimal)
            do! Command.TransformLayout(dst, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal)

            do! Command.ResolveMultisamples(
                src, VkImageLayout.TransferSrcOptimal,
                dst, VkImageLayout.TransferDstOptimal
            )

            do! Command.TransformLayout(src, VkImageLayout.TransferSrcOptimal, srcLayout)
            do! Command.TransformLayout(dst, VkImageLayout.TransferDstOptimal, dstLayout)
        }

    member x.Dispose() =
        if not device.IsDisposed then
            onDispose.Trigger()
            manager.Dispose()
            device.Dispose()
            debugSubscription.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    member x.CreateBuffer(size : uint64,
                          [<Optional; DefaultParameterValue(0UL)>] alignment : uint64,
                          [<Optional; DefaultParameterValue(BufferUsage.All)>] usage : BufferUsage,
                          [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage,
                          [<Optional; DefaultParameterValue(false)>] export : bool) =
        let flags = VkBufferUsageFlags.ofBufferUsage usage
        let memory = if storage = BufferStorage.Device then device.DeviceMemory else device.HostMemory
        memory.CreateBuffer(flags, size, alignment, export)

    member x.Upload(src : nativeint, dst : IBackendBuffer, dstOffset : uint64, sizeInBytes : uint64) =
        dst |> ResourceValidation.Buffers.validateRange dstOffset sizeInBytes

        let dst = unbox<Buffer> dst
        Buffer.upload src dst dstOffset sizeInBytes

    member x.Download(src : IBackendBuffer, srcOffset : uint64, dst : nativeint, sizeInBytes : uint64) =
        src |> ResourceValidation.Buffers.validateRange srcOffset sizeInBytes

        let src = unbox<Buffer> src
        Buffer.download src srcOffset dst sizeInBytes

    member x.DownloadAsync(src : IBackendBuffer, srcOffset : uint64, dst : nativeint, sizeInBytes : uint64) =
        src |> ResourceValidation.Buffers.validateRange srcOffset sizeInBytes

        let src = unbox<Buffer> src
        Buffer.downloadAsync src srcOffset dst sizeInBytes

    member x.Copy(src : IBackendBuffer, srcOffset : uint64, dst : IBackendBuffer, dstOffset : uint64, sizeInBytes : uint64) =
        src |> ResourceValidation.Buffers.validateRange srcOffset sizeInBytes
        dst |> ResourceValidation.Buffers.validateRange dstOffset sizeInBytes

        let src = unbox<Buffer> src
        let dst = unbox<Buffer> dst
        Buffer.copy src srcOffset dst dstOffset sizeInBytes

    // upload
    member x.Upload<'T when 'T : unmanaged>(texture : ITextureSubResource, source : NativeTensor4<'T>, format : Col.Format,
                                            [<Optional; DefaultParameterValue(V3i())>] offset : V3i,
                                            [<Optional; DefaultParameterValue(V3i())>] size : V3i) =
        let size =
            if size = V3i.Zero then
                V3i source.Size
            else
                size

        texture.Texture |> ResourceValidation.Textures.validateLevel texture.Level
        texture.Texture |> ResourceValidation.Textures.validateSlice texture.Slice
        texture.Texture |> ResourceValidation.Textures.validateUploadWindow texture.Level offset size

        let image = ImageSubresource.ofTextureSubResource texture
        device.UploadLevel(image, source, format, offset, size)

    // download
    member x.Download<'T when 'T : unmanaged>(texture : ITextureSubResource, target : NativeTensor4<'T>, format : Col.Format,
                                              [<Optional; DefaultParameterValue(V3i())>] offset : V3i,
                                              [<Optional; DefaultParameterValue(V3i())>] size : V3i) =
        let size =
            if size = V3i.Zero then
                V3i target.Size
            else
                size

        texture.Texture |> ResourceValidation.Textures.validateLevel texture.Level
        texture.Texture |> ResourceValidation.Textures.validateSlice texture.Slice
        texture.Texture |> ResourceValidation.Textures.validateWindow texture.Level offset size

        let image = ImageSubresource.ofTextureSubResource texture
        device.DownloadLevel(image, target, format, offset, size)

    // copy
    member x.Copy(src : IBackendTexture, srcBaseSlice : int, srcBaseLevel : int, dst : IBackendTexture, dstBaseSlice : int, dstBaseLevel : int, slices : int, levels : int) =
        src |> ResourceValidation.Textures.validateSlices srcBaseSlice slices
        src |> ResourceValidation.Textures.validateLevels srcBaseLevel levels
        dst |> ResourceValidation.Textures.validateSlices dstBaseSlice slices
        dst |> ResourceValidation.Textures.validateLevels dstBaseLevel levels
        (src, dst) ||> ResourceValidation.Textures.validateSizes srcBaseLevel dstBaseLevel
        (src, dst) ||> ResourceValidation.Textures.validateSamplesForCopy

        Image.copy
            (unbox<Image> src) srcBaseSlice srcBaseLevel
            (unbox<Image> dst) dstBaseSlice dstBaseLevel
            slices levels

    member x.Blit(src : IFramebufferOutput, srcRegion : Box3i, dst : IFramebufferOutput, dstRegion : Box3i) =

        let validate (region : Box3i) (i : ImageSubresourceLayers) =
            let t = i.Image :> IBackendTexture
            t |> ResourceValidation.Textures.validateSlices i.BaseSlice i.SliceCount
            t |> ResourceValidation.Textures.validateLevels i.Level 1
            t |> ResourceValidation.Textures.validateBlitRegion i.Level region

        let src = ImageSubresourceLayers.ofFramebufferOutput src
        let dst = ImageSubresourceLayers.ofFramebufferOutput dst
        src |> validate srcRegion
        dst |> validate dstRegion
        (src.Image.Samples, dst.Image.Samples) ||> ResourceValidation.Textures.validateSamplesForCopy' src.Image.Dimension

        Image.blit src srcRegion dst dstRegion

    member x.Copy(src : IFramebuffer, dst : IFramebuffer) =
        let src = src :?> Framebuffer
        let dst = dst :?> Framebuffer
        device.perform {
            for (KeyValue(name, srcView)) in src.Attachments do
                match dst.Attachments.TryGetValue name with
                | (true, dstView) ->
                    let ap = if name = DefaultSemantic.DepthStencil then TextureAspect.DepthStencil else TextureAspect.Color
                    let lSrc = srcView.Image.Layout
                    let lDst = dstView.Image.Layout
                    do! Command.TransformLayout(srcView.Image, VkImageLayout.TransferSrcOptimal)
                    do! Command.TransformLayout(dstView.Image, VkImageLayout.TransferDstOptimal)
                    do! Command.Blit(srcView.Image.[ap, 0, *], VkImageLayout.TransferSrcOptimal, dstView.Image.[ap, 0, *], VkImageLayout.TransferSrcOptimal, VkFilter.Nearest)
                    do! Command.TransformLayout(srcView.Image, lSrc)
                    do! Command.TransformLayout(dstView.Image, lDst)
                | _ ->
                    ()
        }

    member x.ReadPixels(src : IFramebuffer, sem : Symbol, offset : V2i, size : V2i) =
        let src = src :?> Framebuffer
        let att = src.Attachments.[sem].Image :> IBackendTexture
        x.Download(att, 0, 0, Box2i(offset, offset + size))

    // Clear
    member x.Clear(fbo : IFramebuffer, values : ClearValues) : unit =
        let fbo = unbox<Framebuffer> fbo

        device.perform {
            for KeyValue(name, view) in fbo.Attachments do
                if name = DefaultSemantic.DepthStencil then
                    let format = VkFormat.toTextureFormat view.Image.Format
                    let aspect = TextureFormat.toAspect format

                    match values.Depth, values.Stencil with
                    | Some d, Some s ->
                        do! Command.ClearDepthStencil(view, TextureAspect.DepthStencil &&& aspect, d, s)

                    | Some d, None when aspect.HasFlag TextureAspect.Depth ->
                        do! Command.ClearDepthStencil(view, TextureAspect.Depth, d, 0)

                    | None, Some s when aspect.HasFlag TextureAspect.Stencil  ->
                        do! Command.ClearDepthStencil(view, TextureAspect.Stencil, 0.0, s)

                    | _ -> ()
                else
                    match values.[name] with
                    | Some color -> do! Command.ClearColor(view, TextureAspect.Color, color)
                    | _ -> ()
        }

    member x.Clear(texture : IBackendTexture, values : ClearValues) : unit =
        let image = unbox<Image> texture

        let command =
            if texture.Format.HasDepth || texture.Format.HasStencil then
                let aspect = TextureFormat.toAspect texture.Format

                match values.Depth, values.Stencil with
                | Some d, Some s ->
                    Command.ClearDepthStencil(image.[TextureAspect.DepthStencil &&& aspect], d, s)

                | Some d, None when aspect.HasFlag TextureAspect.Depth ->
                    Command.ClearDepthStencil(image.[TextureAspect.Depth], d, 0)

                | None, Some s when aspect.HasFlag TextureAspect.Stencil  ->
                    Command.ClearDepthStencil(image.[TextureAspect.Stencil], 0.0, s)

                | _ -> Command.Nop
            else
                match values.[DefaultSemantic.Colors] with
                | Some color ->
                    Command.ClearColor(image.[TextureAspect.Color], color)

                | _ -> Command.Nop

        if command <> Command.Nop then
            device.perform {
                do! command
            }

    // Compute
    member x.MaxLocalSize = V3i device.PhysicalDevice.Limits.Compute.MaxWorkGroupSize

    member x.CreateComputeShader(shader : FShade.ComputeShader) =
        device.CreateComputeShader shader

    member x.CreateInputBinding(shader : IComputeShader, inputs : IUniformProvider) : IComputeInputBinding =
        manager.CreateComputeInputBinding(shader, inputs)

    member x.CompileCompute (commands : alist<ComputeCommand>) =
        new ComputeTask(manager, commands) :> IComputeTask

    // Queries
    member x.CreateTimeQuery() =
        new TimeQuery(device) :> ITimeQuery

    member x.CreateOcclusionQuery([<Optional; DefaultParameterValue(true)>] precise : bool) =
        let features = &device.PhysicalDevice.Features.Queries

        if features.InheritedQueries then
            new OcclusionQuery(device, precise && features.OcclusionQueryPrecise) :> IOcclusionQuery
        else
            new EmptyOcclusionQuery() :> IOcclusionQuery

    member x.CreatePipelineQuery(statistics : seq<PipelineStatistics>) =
        let statistics = Set.intersect (Set.ofSeq statistics) x.SupportedPipelineStatistics
        if Set.isEmpty statistics then
            new EmptyPipelineQuery() :> IPipelineQuery
        else
            new PipelineQuery(device, statistics) :> IPipelineQuery

    member x.SupportedPipelineStatistics =
        let features = &device.PhysicalDevice.Features.Queries

        if features.InheritedQueries then
            if features.PipelineStatistics then
                PipelineStatistics.All
            else
                PipelineStatistics.None
        else
            PipelineStatistics.None

    // Raytracing
    member x.SupportsRaytracing =
        x.Device.EnabledFeatures.Raytracing.Pipeline

    member x.SupportsPositionFetch =
        x.Device.EnabledFeatures.Raytracing.PositionFetch

    member x.SupportsInvocationReorder =
        match x.Device.PhysicalDevice.Properties.Limits.Raytracing with
        | Some limits -> limits.InvocationReorderMode = NVRayTracingInvocationReorder.VkRayTracingInvocationReorderModeNV.Reorder
        | _ -> false

    member x.MaxRayRecursionDepth =
        match x.Device.PhysicalDevice.Limits.Raytracing with
        | Some limits -> int limits.MaxRayRecursionDepth
        | _ -> 0

    member x.CreateAccelerationStructure(geometry, usage) =
        if not x.SupportsRaytracing then
            failwithf "[Vulkan] Runtime does not support raytracing"

        let data = AccelerationStructureData.Geometry geometry
        AccelerationStructure.create x.Device usage data :> IAccelerationStructure

    member x.TryUpdateAccelerationStructure(handle : IAccelerationStructure, geometry) =
        if not x.SupportsRaytracing then
            failwithf "[Vulkan] Runtime does not support raytracing"

        let accel = unbox<AccelerationStructure> handle
        let data = AccelerationStructureData.Geometry geometry
        AccelerationStructure.tryUpdate data accel

    member x.CompileTrace(pipeline : RaytracingPipelineState, commands : alist<RaytracingCommand>) =
        if not x.SupportsRaytracing then
            failwithf "[Vulkan] Runtime does not support raytracing"

        new RaytracingTask(manager, pipeline, commands) :> IRaytracingTask

    interface IRuntime with
        member x.ContextLock = x.ContextLock

        member x.DebugConfig = x.DebugConfig

        member x.DebugLabelsEnabled = x.DebugLabelsEnabled

        member x.DeviceCount = x.DeviceCount

        member x.ShaderDepthRange = x.ShaderDepthRange

        member x.SupportsLayeredShaderInputs = x.SupportsLayeredShaderInputs

        member x.MaxLocalSize = x.MaxLocalSize

        member x.CreateComputeShader(shader : FShade.ComputeShader) =
            x.CreateComputeShader shader

        member x.CreateInputBinding(shader : IComputeShader, inputs : IUniformProvider) =
            x.CreateInputBinding(shader, inputs)

        member x.CompileCompute (commands : alist<ComputeCommand>) =
            x.CompileCompute commands

        member x.Upload<'T when 'T : unmanaged>(texture : ITextureSubResource,
                                                source : NativeTensor4<'T>, format : Col.Format, offset : V3i, size : V3i) =
            x.Upload(texture, source, format, offset, size)

        member x.Download<'T when 'T : unmanaged>(texture : ITextureSubResource,
                                                  target : NativeTensor4<'T>, format : Col.Format, offset : V3i, size : V3i) =
            x.Download(texture, target, format, offset, size)

        member x.Copy(src : IBackendTexture, srcBaseSlice : int, srcBaseLevel : int, dst : IBackendTexture, dstBaseSlice : int, dstBaseLevel : int, slices : int, levels : int) =
            x.Copy(src, srcBaseSlice, srcBaseLevel, dst, dstBaseSlice, dstBaseLevel, slices, levels)

        member x.Blit(src : IFramebufferOutput, srcRegion : Box3i, dst : IFramebufferOutput, dstRegion : Box3i) =
            x.Blit(src, srcRegion, dst, dstRegion)

        member x.Copy(src : IFramebuffer, dst : IFramebuffer) =
            x.Copy(src, dst)

        member x.ReadPixels(src : IFramebuffer, sem : Symbol, offset : V2i, size : V2i) =
            x.ReadPixels(src, sem, offset, size)

        member x.OnDispose = onDispose.Publish

        member x.CreateFramebufferSignature(colorAttachments : Map<int, AttachmentSignature>,
                                            depthStencilAttachment : Option<TextureFormat>,
                                            samples : int, layers : int, perLayerUniforms : seq<string>) =
            x.CreateFramebufferSignature(colorAttachments, depthStencilAttachment, samples, layers, perLayerUniforms)

        member x.DownloadDepth(t : IBackendTexture, target : Matrix<float32>, level : int, slice : int, offset : V2i) =
            x.DownloadDepth(t, target, level, slice, offset)

        member x.DownloadStencil(t : IBackendTexture, target : Matrix<int>, level : int, slice : int, offset : V2i) =
            x.DownloadStencil(t, target, level, slice, offset)

        member x.GenerateMipMaps(t) = x.GenerateMipMaps(t)
        member x.CompileRender (signature, set) = x.CompileRender(signature, set)
        member x.CompileClear(signature, values) = x.CompileClear(signature, values)

        member x.PrepareEffect(signature, effect, topology) = x.PrepareEffect(signature, effect, topology)
        member x.PrepareRenderObject(fboSignature, rj) = x.PrepareRenderObject(fboSignature, rj)
        member x.PrepareTexture(t) = x.PrepareTexture(t)
        member x.PrepareBuffer(b, u, s) = x.PrepareBuffer(b, 0UL, u, s)

        member x.CreateStreamingTexture(mipMap) = x.CreateStreamingTexture(mipMap)

        member x.CreateSparseTexture<'a when 'a : unmanaged> (size : V3i, levels : int, slices : int, dim : TextureDimension, format : Col.Format, brickSize : V3i, maxMemory : int64) : ISparseTexture<'a> =
            x.CreateSparseTexture<'a>(size, levels, slices, dim, format, brickSize, maxMemory)

        member x.CreateFramebuffer(signature, bindings) = x.CreateFramebuffer(signature, bindings)

        member x.CreateTexture(size : V3i, dim : TextureDimension, format : TextureFormat, levels : int, samples : int) =
            x.CreateTexture(size, dim, format, levels, samples, false)

        member x.CreateTextureArray(size : V3i, dim : TextureDimension, format : TextureFormat, levels : int, samples : int, count : int) =
            x.CreateTextureArray(size, dim, format, levels, samples, count, false)

        member x.CreateRenderbuffer(size, format, samples) = x.CreateRenderbuffer(size, format, samples)

        member x.CreateGeometryPool(types) = new GeometryPoolUtilities.GeometryPool(device, types) :> IGeometryPool

        member x.CreateBuffer(size : uint64, usage : BufferUsage, storage : BufferStorage) = x.CreateBuffer(size, 0UL, usage, storage) :> IBackendBuffer

        member x.Upload(src : nativeint, dst : IBackendBuffer, dstOffset : uint64, size : uint64) =
            x.Upload(src, dst, dstOffset, size)

        member x.Download(src : IBackendBuffer, srcOffset : uint64, dst : nativeint, size : uint64) =
            x.Download(src, srcOffset, dst, size)

        member x.Copy(src : IBackendBuffer, srcOffset : uint64, dst : IBackendBuffer, dstOffset : uint64, size : uint64) =
            x.Copy(src, srcOffset, dst, dstOffset, size)

        member x.DownloadAsync(src : IBackendBuffer, srcOffset : uint64, dst : nativeint, size : uint64) =
            x.DownloadAsync(src, srcOffset, dst, size)

        member x.Clear(fbo : IFramebuffer, values : ClearValues) : unit =
            x.Clear(fbo, values)

        member x.Clear(texture : IBackendTexture, values : ClearValues) : unit =
            x.Clear(texture, values)

        member x.CreateTextureView(texture : IBackendTexture, levels : Range1i, slices : Range1i, isArray : bool) : IBackendTexture =
            failwith "not implemented"

        member x.CreateTimeQuery() =
            x.CreateTimeQuery()

        member x.CreateOcclusionQuery(precise) =
            x.CreateOcclusionQuery(precise)

        member x.CreatePipelineQuery(statistics) =
            x.CreatePipelineQuery(statistics)

        member x.SupportedPipelineStatistics =
            x.SupportedPipelineStatistics

        member x.SupportsRaytracing =
            x.SupportsRaytracing

        member x.SupportsPositionFetch =
            x.SupportsPositionFetch

        member x.SupportsInvocationReorder =
            x.SupportsInvocationReorder

        member x.MaxRayRecursionDepth =
            x.MaxRayRecursionDepth

        member x.CreateAccelerationStructure(geometry, usage) =
            x.CreateAccelerationStructure(geometry, usage)

        member x.TryUpdateAccelerationStructure(handle, geometry) =
            x.TryUpdateAccelerationStructure(handle, geometry)

        member x.CompileTrace(pipeline, commands) =
            x.CompileTrace(pipeline, commands)

        member x.ShaderCachePath
            with get() = x.ShaderCachePath
            and set(value) = x.ShaderCachePath <- value

        member x.CreateLodRenderer(config, data) : IPreparedRenderObject =
            raise <| NotImplementedException()