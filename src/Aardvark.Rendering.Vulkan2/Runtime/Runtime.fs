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

#nowarn "9"
#nowarn "51"

type Runtime(device : Device, shareTextures : bool, shareBuffers : bool) as this =
    do device.Runtime <- this
    let manager = new ResourceManager(device, None, shareTextures, shareBuffers)

    member x.Device = device
    member x.ResourceManager = manager
    member x.ContextLock = device.ResourceToken :> IDisposable

    member x.DownloadStencil(t : IBackendTexture, level : int, slice : int, target : Matrix<int>) = failf "not implemented"
    member x.DownloadDepth(t : IBackendTexture, level : int, slice : int, target : Matrix<float32>) = failf "not implemented"

    member x.CreateMappedBuffer() = failf "not implemented"
    member x.CreateMappedIndirectBuffer(indexed : bool) = failf "not implemented"
    member x.CreateStreamingTexture (mipMaps : bool) = failf "not implemented"
    member x.DeleteStreamingTexture (texture : IStreamingTexture) = failf "not implemented"


    member x.Download(t : IBackendTexture, level : int, slice : int, target : PixImage) =
        device.DownloadLevel(unbox t, level, slice, target)

    member x.Upload(t : IBackendTexture, level : int, slice : int, source : PixImage) =
        device.UploadLevel(unbox t, level, slice, source)

    member x.PrepareRenderObject(fboSignature : IFramebufferSignature, rj : IRenderObject) =
        manager.PrepareRenderObject(unbox fboSignature, rj) :> IPreparedRenderObject

    member x.CompileRender (renderPass : IFramebufferSignature, engine : BackendConfiguration, set : aset<IRenderObject>) =
        new RenderTasks.RenderTask(manager, unbox renderPass, set, Mod.constant engine, shareTextures, shareBuffers) :> IRenderTask

    member x.CompileClear(signature : IFramebufferSignature, color : IMod<Map<Symbol, C4f>>, depth : IMod<Option<float>>) : IRenderTask =
        let pass = unbox<RenderPass> signature
        let colors = pass.ColorAttachments |> Map.toSeq |> Seq.map (fun (_,(sem,att)) -> sem, color |> Mod.map (Map.find sem)) |> Map.ofSeq
        new RenderTasks.ClearTask(manager, unbox signature, colors, depth, Some (Mod.constant 0u)) :> IRenderTask



    member x.CreateFramebufferSignature(attachments : SymbolDict<AttachmentSignature>, images : Set<Symbol>) =
        let attachments = attachments |> SymDict.toMap
        device.CreateRenderPass(attachments) :> IFramebufferSignature

    member x.DeleteFramebufferSignature(signature : IFramebufferSignature) =
        device.Delete(unbox<RenderPass> signature)

    member x.CreateFramebuffer(signature : IFramebufferSignature, bindings : Map<Symbol, IFramebufferOutput>) : IFramebuffer =
        let views =
            bindings |> Map.map (fun s o ->
                match o with
                    | :? BackendTextureOutputView as view ->
                        device.CreateImageView(unbox view.texture, view.level, 1, view.slice, 1)

                    | :? Image as img ->
                        device.CreateImageView(img, 0, 1, 0, 1)

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
            if isDepth then VkImageLayout.DepthStencilAttachmentOptimal
            else VkImageLayout.ColorAttachmentOptimal

        let usage =
            if isDepth then VkImageUsageFlags.DepthStencilAttachmentBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.SampledBit
            else VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.SampledBit

        device.CreateImage(V3i(size.X, size.Y, 1), levels, count, samples, TextureDimension.Texture2D, format, usage, layout) :> IBackendTexture

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

        device.CreateImage(V3i(size.X, size.Y, 1), levels, 6, samples, TextureDimension.TextureCube, format, usage, layout) :> IBackendTexture



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
            if isDepth then VkImageUsageFlags.DepthStencilAttachmentBit ||| VkImageUsageFlags.TransferSrcBit 
            else VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferSrcBit

        device.CreateImage(V3i(size.X, size.Y, 1), 1, 1, samples, TextureDimension.Texture2D, RenderbufferFormat.toTextureFormat format, usage, layout) :> IRenderbuffer

    member x.GenerateMipMaps(t : IBackendTexture) =
        use token = device.ResourceToken
        token.enqueue {
            do! Command.GenerateMipMaps (unbox t)
        }

    member x.ResolveMultisamples(source : IFramebufferOutput, target : IBackendTexture, trafo : ImageTrafo) =
        use token = device.ResourceToken

        let srcImage, srcLevel, srcSlice =
            match source with
                | :? BackendTextureOutputView as view ->
                    (unbox<Image> view.texture, view.level, view.slice)
                | :? Image as img ->
                    (img, 0, 0)
                | _ ->
                    failf "invalid input for blit: %A" source

        let dstImage = unbox<Image> target

        let srcBox = Box3i(0, 0, 0, srcImage.Size.X - 1, srcImage.Size.Y - 1, 0)
        let dstBox = Box3i(0, 0, 0, dstImage.Size.X - 1, dstImage.Size.Y - 1, 0)
        

        token.enqueue {
            do! Command.Blit(srcImage, srcLevel, srcSlice, srcBox, dstImage, 0, 0, dstBox, VkFilter.Linear)
        }

    member x.Dispose() = 
        manager.Dispose()
        device.Dispose()
        
    interface IDisposable with
        member x.Dispose() = x.Dispose() 

    interface IRuntime with
        member x.ResourceManager = failf "not implemented"

        member x.CreateFramebufferSignature(a,b) = x.CreateFramebufferSignature(a,b)
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

         member x.CreateFramebuffer(signature, bindings) = x.CreateFramebuffer(signature, bindings)
         member x.CreateTexture(size, format, levels, samples, count) = x.CreateTexture(size, format, levels, samples, count)
         member x.CreateTextureCube(size, format, levels, samples) = x.CreateTextureCube(size, format, levels, samples)
         member x.CreateRenderbuffer(size, format, samples) = x.CreateRenderbuffer(size, format, samples)
         member x.CreateMappedBuffer() = x.CreateMappedBuffer()
         member x.CreateMappedIndirectBuffer(indexed) = x.CreateMappedIndirectBuffer(indexed)