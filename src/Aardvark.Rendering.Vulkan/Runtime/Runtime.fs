namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.Incremental
open System.Diagnostics
open System.Collections.Generic
open Aardvark.Base.Runtime
open FShade
#nowarn "9"
#nowarn "51"

type private MappedBuffer(d : Device, store : ResizeBuffer) =
    inherit ConstantMod<IBuffer>(store)

    let onDispose = Event<_>()

    interface IDisposable with
        member x.Dispose() = 
            onDispose.Trigger()
            d |> ResizeBuffer.delete store

    interface ILockedResource with
        member x.Lock = store.Lock
        member x.OnLock u = ()
        member x.OnUnlock u = ()

    interface IMappedBuffer with
        member x.Write(sourcePtr, offset, size) = store.UseWrite(int64 offset, int64 size, fun dst -> Marshal.Copy(sourcePtr, dst, size))
        member x.Read(targetPtr, offset, size) = store.UseWrite(int64 offset, int64 size, fun src -> Marshal.Copy(src, targetPtr, size))
        member x.Capacity = nativeint store.Capacity
        member x.Resize(newCapacity) = store.Resize(int64 newCapacity) 
        member x.OnDispose = onDispose.Publish :> IObservable<_>
        member x.UseRead(offset, size, f) = store.UseRead(int64 offset, int64 size, f)
        member x.UseWrite(offset, size, f) = store.UseWrite(int64 offset, int64 size, f)

type private MappedIndirectBuffer private(device : Device, indexed : bool, store : ResizeBuffer, indirect : IndirectBuffer) =
    inherit ConstantMod<IIndirectBuffer>(indirect)
    static let drawCallSize = int64 sizeof<DrawCallInfo>

    let transform (c : DrawCallInfo) =
        if indexed then 
            let mutable c = c
            Fun.Swap(&c.BaseVertex, &c.FirstInstance)
            c
        else
            c

    new(device : Device, indexed : bool, store : ResizeBuffer) = new MappedIndirectBuffer(device, indexed, store, IndirectBuffer(device, store.Handle, Unchecked.defaultof<_>, 0))

    interface IDisposable with
        member x.Dispose() = 
            device |> ResizeBuffer.delete store

    interface ILockedResource with
        member x.Lock = store.Lock
        member x.OnLock u = ()
        member x.OnUnlock u = ()

    interface IMappedIndirectBuffer with
        member x.Indexed = indexed
        member x.Resize cnt = store.Resize (int64 cnt * drawCallSize)
        member x.Capacity = store.Capacity / drawCallSize |> int
        member x.Count 
            with get() = indirect.Count
            and set c = indirect.Count <- c

        member x.Item
            with get (i : int) =
                let res = store.UseRead(int64 i * drawCallSize, drawCallSize, NativeInt.read<DrawCallInfo>)
                transform res

            and set (i : int) (v : DrawCallInfo) =
                let v = transform v
                store.UseWrite(int64 i * drawCallSize, drawCallSize, fun ptr -> NativeInt.write ptr v)


type Runtime(device : Device, shareTextures : bool, shareBuffers : bool, debug : bool) as this =
    let instance = device.Instance
    do device.Runtime <- this

    let noUser =
        { new IResourceUser with
            member x.AddLocked _ = ()
            member x.RemoveLocked _ = ()
        }

    let manager = new ResourceManager(noUser, device)

    let allPools = System.Collections.Generic.List<DescriptorPool>()
    let threadedPools =
        new ThreadLocal<DescriptorPool>(fun _ ->
            let p = device.CreateDescriptorPool(1 <<< 18, 1 <<< 18)
            lock allPools (fun () -> allPools.Add p)
            p
        )

    do device.OnDispose.Add (fun _ -> 
        allPools |> Seq.iter device.Delete
        allPools.Clear()
    )

    static let shaderStages =
        LookupTable.lookupTable [
            FShade.ShaderStage.Vertex, Aardvark.Base.ShaderStage.Vertex
            FShade.ShaderStage.TessControl, Aardvark.Base.ShaderStage.TessControl
            FShade.ShaderStage.TessEval, Aardvark.Base.ShaderStage.TessEval
            FShade.ShaderStage.Geometry, Aardvark.Base.ShaderStage.Geometry
            FShade.ShaderStage.Fragment, Aardvark.Base.ShaderStage.Fragment
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


    let ignored =
        HashSet.ofList [
            Guid.Parse("{2f3e2b49-7f12-eb9c-578f-c46f5981d022}")
        ]

    let debugBreak (msg : DebugMessage) =
        if Debugger.IsAttached then
            Debugger.Break()


    let debugMessage (msg : DebugMessage) =
        if not (ignored.Contains msg.id) then
            let str = msg.layerPrefix + ": " + msg.message
            match msg.severity with
                | MessageSeverity.Error ->
                    Report.Error("[Vulkan] {0}", str)
                    debugBreak msg

                | MessageSeverity.Warning ->
                    Report.Warn("[Vulkan] {0}", str)

                | MessageSeverity.PerformanceWarning ->
                    Report.Line("[Vulkan] performance: {0}", str)

                | _ ->
                    Report.Line(4, "[Vulkan] {0}", str)

    // install debug output to file (and errors/warnings to console)
    let debugSubscription = 
        if debug then instance.DebugMessages.Subscribe debugMessage
        else { new IDisposable with member x.Dispose() = () }

    let onDispose = Event<unit>()


    member x.DescriptorPool = manager.DescriptorPool
    member x.Device = device
    member x.ResourceManager = manager
    member x.ContextLock = device.Token :> IDisposable

    member x.DownloadStencil(t : IBackendTexture, level : int, slice : int, target : Matrix<int>) = failf "not implemented"
    member x.DownloadDepth(t : IBackendTexture, level : int, slice : int, target : Matrix<float32>) = failf "not implemented"

    member x.CreateMappedBuffer() =
        let store = device |> ResizeBuffer.create VkBufferUsageFlags.VertexBufferBit
        new MappedBuffer(device, store) :> IMappedBuffer

    member x.CreateMappedIndirectBuffer(indexed : bool) =
        let store = device |> ResizeBuffer.create VkBufferUsageFlags.IndirectBufferBit
        new MappedIndirectBuffer(device, indexed, store) :> IMappedIndirectBuffer

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


    member x.Download(t : IBackendTexture, level : int, slice : int, target : PixImage) =
        let image = unbox<Image> t 
        device.DownloadLevel(image.[ImageAspect.Color, level, slice], target)

    member x.Upload(t : IBackendTexture, level : int, slice : int, source : PixImage) =
        let image = unbox<Image> t 
        device.UploadLevel(image.[ImageAspect.Color, level, slice], source)

    member x.PrepareRenderObject(fboSignature : IFramebufferSignature, rj : IRenderObject) =
        manager.PrepareRenderObject(unbox fboSignature, rj) :> IPreparedRenderObject


    member x.CompileRender (renderPass : IFramebufferSignature, engine : BackendConfiguration, set : aset<IRenderObject>) =
        new RenderTask.DependentRenderTask(device, unbox renderPass, set, true, true) :> IRenderTask
        //new RenderTasks.RenderTask(device, unbox renderPass, set, Mod.constant engine, shareTextures, shareBuffers) :> IRenderTask

    member x.CompileClear(signature : IFramebufferSignature, color : IMod<Map<Symbol, C4f>>, depth : IMod<Option<float>>) : IRenderTask =
        let pass = unbox<RenderPass> signature
        let colors = pass.ColorAttachments |> Map.toSeq |> Seq.map (fun (_,(sem,att)) -> sem, color |> Mod.map (Map.find sem)) |> Map.ofSeq
        new RenderTask.ClearTask(device, unbox signature, colors, depth, Some (Mod.constant 0u)) :> IRenderTask



    member x.CreateFramebufferSignature(attachments : SymbolDict<AttachmentSignature>, images : Set<Symbol>, layers : int, perLayer : Set<string>) =
        let attachments = attachments |> SymDict.toMap
        device.CreateRenderPass(attachments, layers, perLayer) :> IFramebufferSignature

    member x.DeleteFramebufferSignature(signature : IFramebufferSignature) =
        device.Delete(unbox<RenderPass> signature)

    member x.CreateFramebuffer(signature : IFramebufferSignature, bindings : Map<Symbol, IFramebufferOutput>) : IFramebuffer =
        let views =
            bindings |> Map.map (fun s o ->
                match o with
                    | :? IBackendTextureOutputView as view ->
                        let img = unbox<Image> view.texture
                        let slices = 1 + view.slices.Max - view.slices.Min
                        device.CreateOutputImageView(img, view.level, 1, view.slices.Min, slices)

                    | :? Image as img ->
                        device.CreateOutputImageView(img, 0, 1, 0, 1)

                    | _ -> failf "invalid framebuffer attachment %A: %A" s o
            )

        device.CreateFramebuffer(unbox signature, views) :> IFramebuffer

    member x.DeleteFramebuffer(fbo : IFramebuffer) =
        let fbo = unbox<Framebuffer> fbo
        fbo.Attachments |> Map.iter (fun _ v -> device.Delete(v))
        device.Delete(fbo)



    member x.PrepareSurface (fboSignature : IFramebufferSignature, surface : ISurface) =
        device.CreateShaderProgram(unbox fboSignature, surface) :> IBackendSurface

    member x.DeleteSurface (bs : IBackendSurface) =
        device.Delete(unbox<ShaderProgram> bs)

    member x.PrepareTexture (t : ITexture) =
        device.CreateImage(t) :> IBackendTexture

    member x.DeleteTexture(t : IBackendTexture) =
        device.Delete(unbox<Image> t)

    member x.DeletRenderbuffer(t : IRenderbuffer) =
        device.Delete(unbox<Image> t)

    member x.PrepareBuffer (t : IBuffer) =
        device.CreateBuffer(VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.VertexBufferBit, t) :> IBackendBuffer

    member x.DeleteBuffer(t : IBackendBuffer) =
        device.Delete(unbox<Buffer> t)

    member x.CreateTexture(size : V2i, format : TextureFormat, levels : int, samples : int, count : int) : IBackendTexture =
        let isDepth =
            match format with
                | TextureFormat.Depth24Stencil8 -> true
                | TextureFormat.Depth32fStencil8 -> true
                | TextureFormat.DepthComponent -> true
                | TextureFormat.DepthComponent16 -> true
                | TextureFormat.DepthComponent24 -> true
                | TextureFormat.DepthComponent32 -> true
                | TextureFormat.DepthComponent32f -> true
                | TextureFormat.DepthStencil -> true
                | _ -> false

        let layout =
            if isDepth then VkImageLayout.ShaderReadOnlyOptimal
            else VkImageLayout.ShaderReadOnlyOptimal

        let usage =
            if isDepth then 
                VkImageUsageFlags.DepthStencilAttachmentBit ||| 
                VkImageUsageFlags.TransferSrcBit ||| 
                VkImageUsageFlags.TransferDstBit |||
                VkImageUsageFlags.SampledBit
            else 
                VkImageUsageFlags.ColorAttachmentBit ||| 
                VkImageUsageFlags.TransferSrcBit ||| 
                VkImageUsageFlags.TransferDstBit |||
                VkImageUsageFlags.SampledBit

        let img = device.CreateImage(V3i(size.X, size.Y, 1), levels, count, samples, TextureDimension.Texture2D, format, usage) 
        device.GraphicsFamily.run {
            do! Command.TransformLayout(img, layout)
        }
        img :> IBackendTexture

    member x.CreateTextureCube(size : V2i, format : TextureFormat, levels : int, samples : int) : IBackendTexture =
        let isDepth =
            match format with
                | TextureFormat.Depth24Stencil8 -> true
                | TextureFormat.Depth32fStencil8 -> true
                | TextureFormat.DepthComponent -> true
                | TextureFormat.DepthComponent16 -> true
                | TextureFormat.DepthComponent24 -> true
                | TextureFormat.DepthComponent32 -> true
                | TextureFormat.DepthComponent32f -> true
                | TextureFormat.DepthStencil -> true
                | _ -> false

        let layout =
            if isDepth then VkImageLayout.DepthStencilAttachmentOptimal
            else VkImageLayout.ColorAttachmentOptimal

        let usage =
            if isDepth then VkImageUsageFlags.DepthStencilAttachmentBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.SampledBit
            else VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.SampledBit

        let img = device.CreateImage(V3i(size.X, size.Y, 1), levels, 6, samples, TextureDimension.TextureCube, format, usage) 
        device.GraphicsFamily.run {
            do! Command.TransformLayout(img, layout)
        }
        img :> IBackendTexture



    member x.CreateRenderbuffer(size : V2i, format : RenderbufferFormat, samples : int) : IRenderbuffer =
        let isDepth =
            match format with
                | RenderbufferFormat.Depth24Stencil8 -> true
                | RenderbufferFormat.Depth32fStencil8 -> true
                | RenderbufferFormat.DepthComponent -> true
                | RenderbufferFormat.DepthComponent16 -> true
                | RenderbufferFormat.DepthComponent24 -> true
                | RenderbufferFormat.DepthComponent32 -> true
                | RenderbufferFormat.DepthComponent32f -> true
                | RenderbufferFormat.DepthStencil -> true
                | _ -> false

        let layout =
            if isDepth then VkImageLayout.DepthStencilAttachmentOptimal
            else VkImageLayout.ColorAttachmentOptimal

        let usage =
            if isDepth then VkImageUsageFlags.DepthStencilAttachmentBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.TransferSrcBit
            else VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.TransferSrcBit

        let img = device.CreateImage(V3i(size.X, size.Y, 1), 1, 1, samples, TextureDimension.Texture2D, RenderbufferFormat.toTextureFormat format, usage) 
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
                | :? IBackendTextureOutputView as view ->
                    let image = unbox<Image> view.texture
                    let flags = VkFormat.toAspect image.Format
                    image.[unbox (int flags), view.level, view.slices.Min .. view.slices.Max]
                | :? Image as img ->
                    let flags = VkFormat.toAspect img.Format
                    img.[unbox (int flags), 0, 0 .. 0]
                | _ ->
                    failf "invalid input for blit: %A" source

        let dst = 
            let img = unbox<Image> target
            img.[src.Aspect, 0, 0 .. src.SliceCount - 1]

        token.Enqueue (Command.ResolveMultisamples(src, dst))

    member x.Dispose() = 
        onDispose.Trigger()
        debugSubscription.Dispose()
        manager.Dispose()
        device.Dispose()
        
    interface IDisposable with
        member x.Dispose() = x.Dispose() 

    member x.CreateBuffer(size : nativeint) =
        let usage =
            VkBufferUsageFlags.TransferSrcBit ||| 
            VkBufferUsageFlags.TransferDstBit ||| 
            VkBufferUsageFlags.StorageBufferBit ||| 
            VkBufferUsageFlags.VertexBufferBit |||
            VkBufferUsageFlags.IndexBufferBit ||| 
            VkBufferUsageFlags.IndirectBufferBit 

        device.CreateBuffer(usage, int64 size)

    member x.Copy(src : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) =
        let temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit (int64 size)
        let dst = unbox<Buffer> dst

        temp.Memory.Mapped(fun ptr -> Marshal.Copy(src, ptr, size))
        device.perform {
            do! Command.Copy(temp, 0L, dst, int64 dstOffset, int64 size)
        }
        device.Delete temp

    member x.Copy(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, size : nativeint) =
        let temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit (int64 size)
        let src = unbox<Buffer> src

        device.perform {
            do! Command.Copy(src, int64 srcOffset, temp, 0L, int64 size)
        }

        temp.Memory.Mapped (fun ptr -> Marshal.Copy(ptr, dst, size))
        device.Delete temp

    member x.Copy(src : IBackendTexture, srcBaseSlice : int, srcBaseLevel : int, dst : IBackendTexture, dstBaseSlice : int, dstBaseLevel : int, slices : int, levels : int) = 
        let src = unbox<Image> src
        let dst = unbox<Image> dst

        device.perform {
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
        }


    interface IRuntime with
        member x.OnDispose = onDispose.Publish
        member x.AssembleEffect (effect : FShade.Effect, signature : IFramebufferSignature) =
            BackendSurface.ofEffectSimple signature effect

        member x.ResourceManager = failf "not implemented"

        member x.CreateFramebufferSignature(a,b,c,d) = x.CreateFramebufferSignature(a,b,c,d)
        member x.DeleteFramebufferSignature(s) = x.DeleteFramebufferSignature(s)

        member x.Download(t : IBackendTexture, level : int, slice : int, target : PixImage) = x.Download(t, level, slice, target)
        member x.Upload(t : IBackendTexture, level : int, slice : int, source : PixImage) = x.Upload(t, level, slice, source)
        member x.DownloadDepth(t : IBackendTexture, level : int, slice : int, target : Matrix<float32>) = x.DownloadDepth(t, level, slice, target)
        member x.DownloadStencil(t : IBackendTexture, level : int, slice : int, target : Matrix<int>) = x.DownloadStencil(t, level, slice, target)

        member x.ResolveMultisamples(source, target, trafo) = x.ResolveMultisamples(source, target, trafo)
        member x.GenerateMipMaps(t) = x.GenerateMipMaps(t)
        member x.ContextLock = x.ContextLock
        member x.CompileRender (signature, engine, set) = x.CompileRender(signature, engine, set)
        member x.CompileClear(signature, color, depth) = x.CompileClear(signature, color, depth)

        member x.PrepareSurface(signature, s) = x.PrepareSurface(signature, s)
        member x.DeleteSurface(s) = x.DeleteSurface(s)
        member x.PrepareRenderObject(fboSignature, rj) = x.PrepareRenderObject(fboSignature, rj)
        member x.PrepareTexture(t) = x.PrepareTexture(t)
        member x.DeleteTexture(t) = x.DeleteTexture(t)
        member x.PrepareBuffer(b) = x.PrepareBuffer(b)
        member x.DeleteBuffer(b) = x.DeleteBuffer(b)

        member x.DeleteRenderbuffer(b) = x.DeletRenderbuffer(b)
        member x.DeleteFramebuffer(f) = x.DeleteFramebuffer(f)

        member x.CreateStreamingTexture(mipMap) = x.CreateStreamingTexture(mipMap)
        member x.DeleteStreamingTexture(t) = x.DeleteStreamingTexture(t)


        member x.CreateSparseTexture<'a when 'a : unmanaged> (size : V3i, levels : int, slices : int, dim : TextureDimension, format : Col.Format, brickSize : V3i, maxMemory : int64) : ISparseTexture<'a> =
            x.CreateSparseTexture<'a>(size, levels, slices, dim, format, brickSize, maxMemory)
        member x.Copy(src : IBackendTexture, srcBaseSlice : int, srcBaseLevel : int, dst : IBackendTexture, dstBaseSlice : int, dstBaseLevel : int, slices : int, levels : int) = x.Copy(src, srcBaseSlice, srcBaseLevel, dst, dstBaseSlice, dstBaseLevel, slices, levels)


        member x.CreateFramebuffer(signature, bindings) = x.CreateFramebuffer(signature, bindings)
        member x.CreateTexture(size, format, levels, samples) = x.CreateTexture(size, format, levels, samples, 1)
        member x.CreateTextureArray(size, format, levels, samples, count) = x.CreateTexture(size, format, levels, samples, count)
        member x.CreateTextureCube(size, format, levels, samples) = x.CreateTextureCube(size, format, levels, samples)
        member x.CreateRenderbuffer(size, format, samples) = x.CreateRenderbuffer(size, format, samples)
        member x.CreateMappedBuffer() = x.CreateMappedBuffer()
        member x.CreateMappedIndirectBuffer(indexed) = x.CreateMappedIndirectBuffer(indexed)
        member x.CreateGeometryPool(types) = new GeometryPoolUtilities.GeometryPool(device, types) :> IGeometryPool



        member x.CreateBuffer(size : nativeint) = x.CreateBuffer(size) :> IBackendBuffer

        member x.Copy(src : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) =
            x.Copy(src, dst, dstOffset, size)

        member x.Copy(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, size : nativeint) =
            x.Copy(src, srcOffset, dst, size)

        member x.MaxLocalSize = device.PhysicalDevice.Limits.Compute.MaxWorkGroupSize

        member x.Compile (c : FShade.ComputeShader) =
            ComputeShader.ofFShade c device :> IComputeShader

        member x.NewInputBinding(c : IComputeShader) =
            ComputeShader.newInputBinding (unbox c) threadedPools.Value :> IComputeShaderInputBinding

        member x.Delete (shader : IComputeShader) =
            ComputeShader.delete (unbox shader)

        member x.Invoke(shader : IComputeShader, groupCount : V3i, inputs : IComputeShaderInputBinding) =
            let shader = unbox<Aardvark.Rendering.Vulkan.ComputeShader> shader
            let inputs = unbox<InputBinding> inputs
            device.perform {
                do! Command.Bind shader
                do! inputs.Bind
                do! Command.Dispatch(groupCount)
            }