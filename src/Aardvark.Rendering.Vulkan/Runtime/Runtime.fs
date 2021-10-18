namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open FShade
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open FSharp.Data.Adaptive
open System.Diagnostics
open System.Collections.Generic
#nowarn "9"

type DebugConfig =
    {
        /// Indicates whether stack traces of (almost) all vulkan handles are stored to
        /// print the origin of objects in debug messages. Note: Impacts performance significantly.
        traceHandles : bool
    }

    static member Default =
        { traceHandles = false }

    static member TraceHandles =
        { DebugConfig.Default with traceHandles = true }


type Runtime(device : Device, shareTextures : bool, shareBuffers : bool, debug : DebugConfig option) as this =
    let instance = device.Instance
    do device.Runtime <- this

    let noUser =
        { new IResourceUser with
            member x.AddLocked _ = ()
            member x.RemoveLocked _ = ()
        }

    let manager = new ResourceManager(noUser, device)

//    let allPools = System.Collections.Generic.List<DescriptorPool>()
//    let threadedPools =
//        new ThreadLocal<DescriptorPool>(fun _ ->
//            let p = device.CreateDescriptorPool(1 <<< 18, 1 <<< 18)
//            lock allPools (fun () -> allPools.Add p)
//            p
//        )
//
//    do device.OnDispose.Add (fun _ -> 
//        allPools |> Seq.iter device.Delete
//        allPools.Clear()
//    )

    static let shaderStages =
        LookupTable.lookupTable [
            FShade.ShaderStage.Vertex, Aardvark.Rendering.ShaderStage.Vertex
            FShade.ShaderStage.TessControl, Aardvark.Rendering.ShaderStage.TessControl
            FShade.ShaderStage.TessEval, Aardvark.Rendering.ShaderStage.TessEval
            FShade.ShaderStage.Geometry, Aardvark.Rendering.ShaderStage.Geometry
            FShade.ShaderStage.Fragment, Aardvark.Rendering.ShaderStage.Fragment
        ]

    #if false
    let seen = System.Collections.Concurrent.ConcurrentHashSet()

    let debugBreak (str : string) =
        if Debugger.IsAttached then
            let stack = StackTrace().GetFrames() |> Array.toList |> List.map (fun f -> f.GetMethod().MetadataToken)
            if seen.Add ((stack, str)) then
                Debugger.Break()
    #else
    let debugBreak (str : string) = ()
    #endif


    let ignored : HashSet<Guid> =
        Aardvark.Base.HashSet.empty

    let debugBreak (msg : DebugMessage) =
        if msg.severity = MessageSeverity.Error then
            Debugger.Launch() |> ignore

        if Debugger.IsAttached then
            Debugger.Break()

    let debugMessage (msg : DebugMessage) =
        if device.DebugReportActive then
            if not (ignored.Contains msg.id) then
                let str = msg.layerPrefix + ": " + msg.message
                match msg.severity with
                | MessageSeverity.Error ->
                    Report.Error("[Vulkan] {0}", str)
                    debugBreak msg

                | MessageSeverity.Warning ->
                    Report.Warn("[Vulkan] {0}", str)

                | MessageSeverity.Information ->
                    Report.Line("[Vulkan] {0}", str)

                | _ ->
                    Report.Line("[Vulkan] DEBUG: {0}", str)

    let debugSummary() =
        if device.DebugReportActive then
            let messages = instance.DebugSummary.messageCounts

            if not messages.IsEmpty then
                let summary =
                    messages
                    |> Seq.map (fun (KeyValue(s, n)) -> sprintf "%A: %d" s n)
                    |> Seq.reduce (fun a b -> a + ", " + b)

                Report.Begin("[Vulkan] Message summary")
                Report.Line(2, summary)
                Report.End() |> ignore

    // install debug output to file (and errors/warnings to console)
    let debugSubscription =
        match debug with
        | Some debug ->
            let res = 
                instance.DebugMessages.Subscribe {
                    new IObserver<_> with
                        member x.OnNext(msg) = debugMessage msg
                        member x.OnCompleted() = debugSummary()
                        member x.OnError _ = ()
                }
            instance.SetDebugTracingEnabled(debug.traceHandles)
            instance.RaiseDebugMessage(MessageSeverity.Information, "Enabled debug report")
            res
        | _ ->
            { new IDisposable with member x.Dispose() = () }

    let onDispose = Event<unit>()

    member x.ShaderCachePath
        with get() = device.ShaderCachePath
        and set v = device.ShaderCachePath <- v

    member x.ValidateShaderCaches
        with get() = device.ValidateShaderCaches
        and set v = device.ValidateShaderCaches <- v

    member x.Device = device
    member x.ResourceManager = manager
    member x.ContextLock = device.Token :> IDisposable
    member x.DebugVerbosity
        with get() = instance.DebugVerbosity
        and set v = instance.DebugVerbosity <- v

    member x.CreateStreamingTexture (mipMaps : bool) = failf "not implemented"
    member x.DeleteStreamingTexture (texture : IStreamingTexture) = failf "not implemented"

    member x.CreateSparseTexture<'a when 'a : unmanaged> (size : V3i, levels : int, slices : int, dim : TextureDimension, format : Col.Format, brickSize : V3i, maxMemory : int64) : ISparseTexture<'a> =
        new SparseTextureImplemetation.DoubleBufferedSparseImage<'a>(
            device, 
            size, levels, slices, 
            dim, format,
            VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit,
            brickSize,
            maxMemory
        ) :> ISparseTexture<_>


    member x.Download(t : IBackendTexture, level : int, slice : int, offset : V2i, target : PixImage) =
        t |> ResourceValidation.Textures.validateLevel level
        t |> ResourceValidation.Textures.validateSlice slice
        t |> ResourceValidation.Textures.validateWindow2D level offset target.Size

        let image = unbox<Image> t
        device.DownloadLevel(image.[ImageAspect.Color, level, slice], target, offset)

    member x.Download(t : IBackendTexture, level : int, slice : int, offset : V3i, target : PixVolume) =
        t |> ResourceValidation.Textures.validateLevel level
        t |> ResourceValidation.Textures.validateSlice slice
        t |> ResourceValidation.Textures.validateWindow level offset target.Size

        let image = unbox<Image> t
        device.DownloadLevel(image.[ImageAspect.Color, level, slice], target, offset)

    member x.DownloadStencil(t : IBackendTexture, level : int, slice : int, offset : V2i, target : Matrix<int>) =
        t |> ResourceValidation.Textures.validateLevel level
        t |> ResourceValidation.Textures.validateSlice slice
        t |> ResourceValidation.Textures.validateWindow2D level offset (V2i target.Size)
        t |> ResourceValidation.Textures.validateStencilFormat

        let image = unbox<Image> t
        let pix =
            let img = PixImage<int>()
            img.Volume <- target.AsVolume()
            img

        failwith "Not implemented in Vulkan backend"
        //device.DownloadLevel(image.[ImageAspect.Stencil, level, slice], pix, offset)

    member x.DownloadDepth(t : IBackendTexture, level : int, slice : int, offset : V2i, target : Matrix<float32>) =
        t |> ResourceValidation.Textures.validateLevel level
        t |> ResourceValidation.Textures.validateSlice slice
        t |> ResourceValidation.Textures.validateWindow2D level offset (V2i target.Size)
        t |> ResourceValidation.Textures.validateDepthFormat

        let image = unbox<Image> t
        let pix =
            let img = PixImage<float32>()
            img.Volume <- target.AsVolume()
            img

        failwith "Not implemented in Vulkan backend"
        //device.DownloadLevel(image.[ImageAspect.Depth, level, slice], pix, offset)

    member x.Upload(t : IBackendTexture, level : int, slice : int, offset : V2i, source : PixImage) =
        t |> ResourceValidation.Textures.validateLevel level
        t |> ResourceValidation.Textures.validateSlice slice
        t |> ResourceValidation.Textures.validateWindow2D level offset source.Size

        let image = unbox<Image> t 
        device.UploadLevel(image.[ImageAspect.Color, level, slice], source, offset)

    member x.PrepareRenderObject(fboSignature : IFramebufferSignature, rj : IRenderObject) =
        manager.PrepareRenderObject(unbox fboSignature, rj) :> IPreparedRenderObject

    member x.CompileRender(renderPass : IFramebufferSignature, cmd : RuntimeCommand) =
        new CommandTask(manager, unbox renderPass, cmd)

    member x.CompileRender (renderPass : IFramebufferSignature, set : aset<IRenderObject>) =
        let set = EffectDebugger.Hook set
        new CommandTask(manager, unbox renderPass, RuntimeCommand.Render set) :> IRenderTask

    member x.CompileClear(signature : IFramebufferSignature, color : aval<Map<Symbol, C4f>>, depth : aval<float option>, stencil : aval<int option>) : IRenderTask =
        let colors =
            color |> AVal.map (fun colors ->
                signature.ColorAttachments |> Map.choose (fun _ (sem, _) ->
                    colors |> Map.tryFind sem
                )
            )

        new ClearTask(device, unbox signature, colors, depth, stencil |> AVal.map (Option.map uint32)) :> IRenderTask



    member x.CreateFramebufferSignature(attachments : Map<Symbol, AttachmentSignature>, layers : int, perLayer : Set<string>) =
        device.CreateRenderPass(attachments, layers, perLayer) :> IFramebufferSignature

    member x.DeleteFramebufferSignature(signature : IFramebufferSignature) =
        Disposable.dispose(unbox<RenderPass> signature)

    member x.CreateFramebuffer(signature : IFramebufferSignature, bindings : Map<Symbol, IFramebufferOutput>) : IFramebuffer =
        let views =
            bindings |> Map.map (fun s o ->
                match o with
                | :? Image as img ->
                    device.CreateOutputImageView(img, 0, 1, 0, 1)

                | :? ITextureLevel as l ->
                    let image = unbox<Image> l.Texture
                    device.CreateOutputImageView(image, l.Levels, l.Slices)

                | _ -> failf "invalid framebuffer attachment %A: %A" s o
            )

        device.CreateFramebuffer(unbox signature, views) :> IFramebuffer

    member x.DeleteFramebuffer(fbo : IFramebuffer) =
        let fbo = unbox<Framebuffer> fbo
        fbo.Attachments |> Map.iter (fun _ v -> v.Dispose())
        fbo.Dispose()



    member x.PrepareSurface (signature : IFramebufferSignature, surface : ISurface) =
        device.CreateShaderProgram(unbox<RenderPass> signature, surface) :> IBackendSurface

    member x.DeleteSurface (bs : IBackendSurface) =
        Disposable.dispose (unbox<ShaderProgram> bs)

    member x.PrepareTexture (t : ITexture) =
        device.CreateImage(t) :> IBackendTexture

    member x.DeleteTexture(t : IBackendTexture) =
        Disposable.dispose (unbox<Image> t)

    member x.DeletRenderbuffer(t : IRenderbuffer) =
        Disposable.dispose (unbox<Image> t)

    member x.PrepareBuffer (t : IBuffer, usage : BufferUsage) =
        let flags = Buffer.toVkUsage usage
        device.CreateBuffer(flags, t) :> IBackendBuffer

    member x.DeleteBuffer(t : IBackendBuffer) =
        Disposable.dispose(unbox<Buffer> t)

    member private x.CreateTextureInner(size : V3i, dim : TextureDimension, format : TextureFormat, levels : int, samples : int, count : int) =
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

        let img = device.CreateImage(size, levels, count, samples, dim, format, usage) 
        device.GraphicsFamily.run {
            do! Command.TransformLayout(img, layout)
        }
        img :> IBackendTexture

    member x.CreateTexture(size : V3i, dim : TextureDimension, format : TextureFormat, levels : int, samples : int) : IBackendTexture =
        ResourceValidation.Textures.validateCreationParams dim size levels samples
        x.CreateTextureInner(size, dim, format, levels, samples, 1)

    member x.CreateTextureArray(size : V3i, dim : TextureDimension, format : TextureFormat, levels : int, samples : int, count : int) : IBackendTexture =
        ResourceValidation.Textures.validateCreationParamsArray dim size levels samples count
        x.CreateTextureInner(size, dim, format, levels, samples, count)

    member x.CreateRenderbuffer(size : V2i, format : RenderbufferFormat, samples : int) : IRenderbuffer =
        if samples < 1 then raise <| ArgumentException("[Renderbuffer] samples must be greater than 0")

        let isDepth =
            RenderbufferFormat.hasDepth format || RenderbufferFormat.hasStencil format

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
        device.GraphicsFamily.run {
            do! Command.GenerateMipMaps (unbox t)
        }

    member x.ResolveMultisamples(source : IFramebufferOutput, target : IBackendTexture, trafo : ImageTrafo) =
        use token = device.Token

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

        token.Enqueue (Command.ResolveMultisamples(src, dst))

    member x.Dispose() =
        if not device.IsDisposed then
            onDispose.Trigger()
            manager.Dispose()
            device.Dispose()
            debugSubscription.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose() 

    member x.CreateBuffer(size : nativeint, usage : BufferUsage) =
        let flags = Buffer.toVkUsage usage
        device.CreateBuffer(flags, int64 size)

    member x.Copy(src : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) =
        use temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit (int64 size)
        let dst = unbox<Buffer> dst

        temp.Memory.Mapped(fun ptr -> Marshal.Copy(src, ptr, size))
        device.perform {
            do! Command.Copy(temp, 0L, dst, int64 dstOffset, int64 size)
        }

    member x.Copy(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, size : nativeint) =
        use temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit (int64 size)
        let src = unbox<Buffer> src

        device.perform {
            do! Command.Copy(src, int64 srcOffset, temp, 0L, int64 size)
        }

        temp.Memory.Mapped (fun ptr -> Marshal.Copy(ptr, dst, size))
        
    member x.CopyAsync(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, size : nativeint) =
        let temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit (int64 size)
        let src = unbox<Buffer> src

        let task = device.GraphicsFamily.Start(QueueCommand.ExecuteCommand([], [], Command.Copy(src, int64 srcOffset, temp, 0L, int64 size)))

        (fun () ->
            task.Wait()
            if task.IsFaulted then 
                temp.Dispose()
                raise task.Exception
            else
                temp.Memory.Mapped (fun ptr -> Marshal.Copy(ptr, dst, size))
                temp.Dispose()
        )



    member x.Copy(src : IBackendBuffer, srcOffset : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) =
        let src = unbox<Buffer> src
        let dst = unbox<Buffer> dst

        device.perform {
            do! Command.Copy(src, int64 srcOffset, dst, int64 dstOffset, int64 size)
        }

    member x.Copy(src : IBackendTexture, srcBaseSlice : int, srcBaseLevel : int, dst : IBackendTexture, dstBaseSlice : int, dstBaseLevel : int, slices : int, levels : int) =
        src |> ResourceValidation.Textures.validateSlices srcBaseSlice slices
        src |> ResourceValidation.Textures.validateLevels srcBaseLevel levels
        dst |> ResourceValidation.Textures.validateSlices dstBaseSlice slices
        dst |> ResourceValidation.Textures.validateLevels dstBaseLevel levels
        (src, dst) ||> ResourceValidation.Textures.validateSizes srcBaseLevel dstBaseLevel
        (src, dst) ||> ResourceValidation.Textures.validateSamplesForCopy

        let src = unbox<Image> src
        let dst = unbox<Image> dst

        let srcLayout = src.Layout
        let dstLayout = dst.Layout

        device.perform {
            if srcLayout <> VkImageLayout.TransferSrcOptimal then do! Command.TransformLayout(src, VkImageLayout.TransferSrcOptimal)
            if dstLayout <> VkImageLayout.TransferDstOptimal then do! Command.TransformLayout(dst, VkImageLayout.TransferDstOptimal)
            if src.Samples = dst.Samples then
                do! Command.Copy(
                        src.[ImageAspect.Color, srcBaseLevel .. srcBaseLevel + levels - 1, srcBaseSlice .. srcBaseSlice + slices - 1],
                        dst.[ImageAspect.Color, dstBaseLevel .. dstBaseLevel + levels - 1, dstBaseSlice .. dstBaseSlice + slices - 1]
                    )
            else
                for l in 0 .. levels - 1 do
                    let srcLevel = srcBaseLevel + l
                    let dstLevel = dstBaseLevel + l
                    do! Command.ResolveMultisamples(
                            src.[ImageAspect.Color, srcLevel, srcBaseSlice .. srcBaseSlice + slices - 1],
                            dst.[ImageAspect.Color, dstLevel, dstBaseSlice .. dstBaseSlice + slices - 1]
                        )
            
            if srcLayout <> VkImageLayout.TransferSrcOptimal then do! Command.TransformLayout(src, srcLayout)
            if dstLayout <> VkImageLayout.TransferDstOptimal then do! Command.TransformLayout(dst, dstLayout)
        }


    // upload
    member x.Copy<'a when 'a : unmanaged>(src : NativeTensor4<'a>, fmt : Col.Format, dst : ITextureSubResource, dstOffset : V3i, size : V3i) =
        let srgb = TextureFormat.isSrgb (unbox (int dst.Format))
        use temp = device |> TensorImage.create<'a> size fmt srgb

        let src = src.SubTensor4(V4l.Zero, V4l(int64 size.X, int64 size.Y, int64 size.Z, src.SW)).MirrorY()
        temp.Write(fmt, src)

        let dstOffset = V3i(dstOffset.X, dst.Size.Y - (dstOffset.Y + size.Y), dstOffset.Z)

        
        let dst = ImageSubresource.ofTextureSubResource dst
        let dstImage = dst.Image

        let oldLayout = dstImage.Layout
        device.perform {
            do! Command.TransformLayout(dstImage, VkImageLayout.TransferDstOptimal)
            do! Command.Copy(temp, dst, dstOffset, size)
            do! Command.TransformLayout(dstImage, oldLayout)
        }

    // download
    member x.Copy<'a when 'a : unmanaged>(src : ITextureSubResource, srcOffset : V3i, dst : NativeTensor4<'a>, fmt : Col.Format, size : V3i) =
        let srgb = TextureFormat.isSrgb (unbox (int src.Format))
        use temp = device |> TensorImage.create<'a> size fmt srgb

        let srcOffset = V3i(srcOffset.X, src.Size.Y - (srcOffset.Y + size.Y), srcOffset.Z)
        let src = ImageSubresource.ofTextureSubResource src
        let srcImage = src.Image

        let oldLayout = srcImage.Layout
        device.perform {
            do! Command.TransformLayout(srcImage, VkImageLayout.TransferSrcOptimal)
            do! Command.Copy(src, srcOffset, temp, size)
            do! Command.TransformLayout(srcImage, oldLayout)
        }

        let dst = dst.SubTensor4(V4l.Zero, V4l(int64 size.X, int64 size.Y, int64 size.Z, dst.SW)).MirrorY()
        temp.Read(fmt, dst)

    // copy
    member x.Copy(src : IFramebufferOutput, srcOffset : V3i, dst : IFramebufferOutput, dstOffset : V3i, size : V3i) =
        let src = ImageSubresourceLayers.ofFramebufferOutput src
        let dst = ImageSubresourceLayers.ofFramebufferOutput dst
        
        let srcOffset = V3i(srcOffset.X, src.Size.Y - (srcOffset.Y + size.Y), srcOffset.Z)
        let dstOffset = V3i(dstOffset.X, dst.Size.Y - (dstOffset.Y + size.Y), dstOffset.Z)

        let srcLayout = src.Image.Layout
        let dstLayout = dst.Image.Layout

        device.perform {
            do! Command.TransformLayout(src.Image, VkImageLayout.TransferSrcOptimal)
            do! Command.TransformLayout(dst.Image, VkImageLayout.TransferDstOptimal)
            do! Command.Copy(src, srcOffset, dst, dstOffset, size)
            do! Command.TransformLayout(src.Image, srcLayout)
            do! Command.TransformLayout(dst.Image, dstLayout)
        }

    // Queries
    member x.CreateTimeQuery() =
        new TimeQuery(device) :> ITimeQuery

    member x.CreateOcclusionQuery(precise : bool) =
        new OcclusionQuery(device, precise) :> IOcclusionQuery

    member x.CreatePipelineQuery(statistics : Set<PipelineStatistics>) =
        new PipelineQuery(device, statistics) :> IPipelineQuery

    member x.SupportedPipelineStatistics =
        if x.Device.PhysicalDevice.Features.Queries.PipelineStatistics then
            PipelineStatistics.All
        else
            PipelineStatistics.None


    interface IRuntime with

        member x.DeviceCount = device.PhysicalDevices.Length

        member x.MaxLocalSize = device.PhysicalDevice.Limits.Compute.MaxWorkGroupSize

        member x.CreateComputeShader (c : FShade.ComputeShader) =
            ComputeShader.ofFShade c device :> IComputeShader

        member x.NewInputBinding(c : IComputeShader) =
            ComputeShader.newInputBinding (unbox c) :> IComputeShaderInputBinding

        member x.DeleteComputeShader (shader : IComputeShader) =
            Disposable.dispose (unbox<ComputeShader> shader)

        member x.Run (commands : list<ComputeCommand>, queries : IQuery) =
            ComputeCommand.run commands queries device

        member x.Compile (commands : list<ComputeCommand>) =
            ComputeCommand.compile commands device

        member x.Copy<'a when 'a : unmanaged>(src : NativeTensor4<'a>, fmt : Col.Format, dst : ITextureSubResource, dstOffset : V3i, size : V3i) =
            x.Copy(src, fmt, dst, dstOffset, size)

        member x.Copy<'a when 'a : unmanaged>(src : ITextureSubResource, srcOffset : V3i, dst : NativeTensor4<'a>, fmt : Col.Format, size : V3i) =
            x.Copy(src, srcOffset, dst, fmt, size)
            
        member x.Copy(src : IFramebufferOutput, srcOffset : V3i, dst : IFramebufferOutput, dstOffset : V3i, size : V3i) =
            x.Copy(src, srcOffset, dst, dstOffset, size)

        member x.OnDispose = onDispose.Publish
        member x.AssembleModule (effect : FShade.Effect, signature : IFramebufferSignature, topology : IndexedGeometryMode) =
            signature.Link(effect, Range1d(0.0, 1.0), false, topology)

        member x.ResourceManager = failf "not implemented"

        member x.CreateFramebufferSignature(a,b,c) = x.CreateFramebufferSignature(a,b,c)
        member x.DeleteFramebufferSignature(s) = x.DeleteFramebufferSignature(s)

        member x.Download(t : IBackendTexture, level : int, slice : int, offset : V2i, target : PixImage) =
            x.Download(t, level, slice, offset, target)

        member x.Download(t : IBackendTexture, level : int, slice : int, offset : V3i, target : PixVolume) =
            x.Download(t, level, slice, offset, target)

        member x.Upload(t : IBackendTexture, level : int, slice : int, offset : V2i, source : PixImage) =
            x.Upload(t, level, slice, offset, source)

        member x.DownloadDepth(t : IBackendTexture, level : int, slice : int, offset : V2i, target : Matrix<float32>) =
            x.DownloadDepth(t, level, slice, offset, target)

        member x.DownloadStencil(t : IBackendTexture, level : int, slice : int, offset : V2i, target : Matrix<int>) =
            x.DownloadStencil(t, level, slice, offset, target)

        member x.ResolveMultisamples(source, target, trafo) = x.ResolveMultisamples(source, target, trafo)
        member x.GenerateMipMaps(t) = x.GenerateMipMaps(t)
        member x.ContextLock = x.ContextLock
        member x.CompileRender (signature, engine, set) = x.CompileRender(signature, set)
        member x.CompileClear(signature, color, depth, stencil) = x.CompileClear(signature, color, depth, stencil)

        member x.PrepareSurface(signature, s) = x.PrepareSurface(signature, s)
        member x.DeleteSurface(s) = x.DeleteSurface(s)
        member x.PrepareRenderObject(fboSignature, rj) = x.PrepareRenderObject(fboSignature, rj)
        member x.PrepareTexture(t) = x.PrepareTexture(t)
        member x.DeleteTexture(t) = x.DeleteTexture(t)
        member x.PrepareBuffer(b, u) = x.PrepareBuffer(b, u)
        member x.DeleteBuffer(b) = x.DeleteBuffer(b)

        member x.DeleteRenderbuffer(b) = x.DeletRenderbuffer(b)
        member x.DeleteFramebuffer(f) = x.DeleteFramebuffer(f)

        member x.CreateStreamingTexture(mipMap) = x.CreateStreamingTexture(mipMap)
        member x.DeleteStreamingTexture(t) = x.DeleteStreamingTexture(t)

        member x.CreateSparseTexture<'a when 'a : unmanaged> (size : V3i, levels : int, slices : int, dim : TextureDimension, format : Col.Format, brickSize : V3i, maxMemory : int64) : ISparseTexture<'a> =
            x.CreateSparseTexture<'a>(size, levels, slices, dim, format, brickSize, maxMemory)
        member x.Copy(src : IBackendTexture, srcBaseSlice : int, srcBaseLevel : int, dst : IBackendTexture, dstBaseSlice : int, dstBaseLevel : int, slices : int, levels : int) = x.Copy(src, srcBaseSlice, srcBaseLevel, dst, dstBaseSlice, dstBaseLevel, slices, levels)


        member x.CreateFramebuffer(signature, bindings) = x.CreateFramebuffer(signature, bindings)

        member x.CreateTexture(size : V3i, dim : TextureDimension, format : TextureFormat, levels : int, samples : int) =
            x.CreateTexture(size, dim, format, levels, samples)

        member x.CreateTextureArray(size : V3i, dim : TextureDimension, format : TextureFormat, levels : int, samples : int, count : int) =
            x.CreateTextureArray(size, dim, format, levels, samples, count)


        member x.CreateRenderbuffer(size, format, samples) = x.CreateRenderbuffer(size, format, samples)
        
        member x.CreateGeometryPool(types) = new GeometryPoolUtilities.GeometryPool(device, types) :> IGeometryPool



        member x.CreateBuffer(size : nativeint, usage : BufferUsage) = x.CreateBuffer(size, usage) :> IBackendBuffer

        member x.Copy(src : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) =
            x.Copy(src, dst, dstOffset, size)

        member x.Copy(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, size : nativeint) =
            x.Copy(src, srcOffset, dst, size)

        member x.Copy(src : IBackendBuffer, srcOffset : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) = 
            x.Copy(src, srcOffset, dst, dstOffset, size)

        member x.CopyAsync(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, size : nativeint) =
            x.CopyAsync(src, srcOffset, dst, size)

        
        member x.Clear(fbo : IFramebuffer, clearColors : Map<Symbol,C4f>, depth : Option<float>, stencil : Option<int>) =
            failwith "not implemented"

        member x.ClearColor(texture : IBackendTexture, color : C4f) =
            failwith "not implemented"

        member x.ClearDepthStencil(texture : IBackendTexture, depth : Option<float>, stencil : Option<int>) =
            failwith "not implemented"

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

        member x.ShaderCachePath
            with get() = x.ShaderCachePath
            and set(value) = x.ShaderCachePath <- value