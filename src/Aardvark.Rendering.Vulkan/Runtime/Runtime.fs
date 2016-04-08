namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering
open System.Collections.Generic
open System.Threading
open Aardvark.Rendering.Vulkan
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.Runtime


type private FramebufferSignature(runtime : IRuntime, res : Resource<RenderPass>) =

    let signature = lazy ( res.Handle.GetValue() :> IFramebufferSignature )
    
    member x.Dispose() = res.Dispose()

    interface IDisposable with
        member x.Dispose() = res.Dispose()

    interface IFramebufferSignature with
        member x.IsAssignableFrom other = signature.Value.IsAssignableFrom other
        member x.ColorAttachments = signature.Value.ColorAttachments
        member x.DepthAttachment = signature.Value.DepthAttachment
        member x.StencilAttachment = signature.Value.StencilAttachment
        member x.Runtime = runtime

//type private BackendTexture(res : Resource<Image>) =
//    let img = lazy ( res.Handle.GetValue() :> IBackendTexture )
//
//    member x.Dispose() =
//        res.Dispose()
//
//    interface IDisposable with
//        member x.Dispose() = x.Dispose()
//
//    interface IBackendTexture with
//        member x.Count = img.Value.Count
//        member x.Dimension = img.Value.Dimension
//        member x.Format = img.Value.Format
//        member x.Handle = img.Value.Handle
//        member x.Size = img.Value.Size
//        member x.MipMapLevels = img.Value.MipMapLevels
//        member x.WantMipMaps = img.Value.WantMipMaps
//        member x.Samples = img.Value.Samples
//


type Runtime(device : Device) as this =
    inherit Resource(device)

    let context = new Context(device)
    let manager = new ResourceManager(this, context)

    member x.Context = context
    member x.Manager = manager

    override x.Release() =
        manager.Dispose()
        context.Dispose()


    member x.CompileRender(fbo : IFramebufferSignature, config : BackendConfiguration, renderObjects : aset<IRenderObject>) =
        new RenderTask(manager, unbox fbo, renderObjects, config) :> IRenderTask

    member x.CompileClear(fbo : IFramebufferSignature, clearColors : IMod<Map<Symbol, C4f>>, clearDepth : IMod<Option<double>>) =
        let colors = 
            fbo.ColorAttachments
                |> Map.toSeq
                |> Seq.map (fun (b, (sem,att)) ->
                    adaptive {
                        let! clearColors = clearColors
                        match Map.tryFind sem clearColors with
                            | Some cc -> return cc
                            | _ -> return C4f.Black
                    }
                   )
                |> Seq.toList

        new ClearTask( manager, unbox fbo, colors, clearDepth, None ) :> IRenderTask

    member x.PrepareRenderObject(fboSig, ro) = 
        manager.PrepareRenderObject(unbox fboSig, ro) :> IPreparedRenderObject


    member x.CreateFramebufferSignature(attachments : SymbolDict<AttachmentSignature>) =
        let pass = manager.CreateRenderPass(SymDict.toMap attachments)
        new FramebufferSignature(x, pass) :> IFramebufferSignature

    member x.DeleteFramebufferSignature(signature : IFramebufferSignature) =
        match signature with
            | :? FramebufferSignature as s -> s.Dispose()
            | _ -> failf "unexpected FramebufferSignature: %A" signature


    member x.CreateTexture(size : V2i, format : TextureFormat, levels : int, samples : int, count : int) =
        context.CreateImage(
            VkImageType.D2d,
            VkFormat.ofTextureFormat format,
            TextureDimension.Texture2D,
            V3i(size.X, size.Y, 1), 
            levels,
            count,
            samples,
            VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.SampledBit,
            VkImageLayout.ColorAttachmentOptimal,
            VkImageTiling.Optimal
        )
        
    member x.DeleteTexture(tex : Image) =
        context.Delete tex


    member x.CreateRenderbuffer(size : V2i, format : RenderbufferFormat, samples : int) =
        context.CreateImage(
            VkImageType.D2d,
            VkFormat.ofRenderbufferFormat format,
            TextureDimension.Texture2D,
            V3i(size.X, size.Y, 1), 
            1,
            1,
            samples,
            VkImageUsageFlags.ColorAttachmentBit,
            VkImageLayout.ColorAttachmentOptimal,
            VkImageTiling.Optimal
        )

    member x.DeleteRenderbuffer(tex : Image) =
        context.Delete tex


    interface IRuntime with
        member x.ContextLock = { new IDisposable with member x.Dispose() = () }

        // compilation

        member x.PrepareRenderObject(fboSig, ro) = x.PrepareRenderObject(fboSig, ro)
        member x.CompileClear(fboSig, col, depth) = x.CompileClear(fboSig, col, depth)
        member x.CompileRender(fboSig, config, ros) = x.CompileRender(fboSig, config, ros)


        // framebuffer signatures

        member x.CreateFramebufferSignature(signature) = 
            x.CreateFramebufferSignature(signature)

        member x.DeleteFramebufferSignature(signature) = 
            x.DeleteFramebufferSignature(signature)


        // textures

        member x.CreateTexture(size,format,levels,samples,count) = 
            x.CreateTexture(size, format, levels, samples, count) :> IBackendTexture

        member x.CreateTextureCube(size,format,levels,samples) = 
            context.CreateImage(
                VkImageType.D2d,
                VkFormat.ofTextureFormat format,
                TextureDimension.TextureCube,
                V3i(size.X, size.Y, 1),
                levels,
                6, samples,
                VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.SampledBit,
                VkImageLayout.ColorAttachmentOptimal,
                VkImageTiling.Optimal
            ) :> IBackendTexture

        member x.PrepareTexture (t : ITexture) = 
            context.CreateImage(t) :> IBackendTexture

        member x.DeleteTexture (t : IBackendTexture) =
            match t with
                | :? Image as img -> context.Delete img
                | _ -> failf "unexpected Texture: %A" t


        // streaming textures

        member x.CreateStreamingTexture _ = failf "not implemented"
        member x.DeleteStreamingTexture _ = failf "not implemented"


        // renderbuffers

        member x.CreateRenderbuffer(size,format,samples) = 
            x.CreateRenderbuffer(size, format, samples) :> IRenderbuffer

        member x.DeleteRenderbuffer (rb) =
            match rb with
                | :? Image as img -> context.Delete img
                | _ -> failf "unexpected Renderbuffer: %A" rb


        // buffers

        member x.CreateMappedBuffer() = failf "not implemented"

        member x.PrepareBuffer (b) =
            context.CreateBuffer(None, b, VkBufferUsageFlags.VertexBufferBit) :> IBackendBuffer

        member x.DeleteBuffer (b) =
            match b with
                | :? Buffer as b -> context.Delete b
                | _ -> failf "unexpected Buffer: %A" b


        // surfaces 

        member x.PrepareSurface (fboSignature : IFramebufferSignature, s : ISurface) =
            match fboSignature with 
                | :? RenderPass as pass ->
                    context.Device.CreateShaderProgram(x, s, pass) :> IBackendSurface
                | _ ->
                    failf "unexpected FramebufferSignature: %A" fboSignature

        member x.DeleteSurface (s : IBackendSurface) =
            match s with
                | :? ShaderProgram as p ->
                    context.Device.Delete(p)
                | _ ->
                    failf "unexpected BackendSurface: %A" s


        // framebuffers

        member x.CreateFramebuffer (signature, attachments) = 
            match signature with
                | :? RenderPass as pass ->
                    
                    let createView (o : IFramebufferOutput) =
                        match o with
                            | :? Image as img -> context.CreateImageOutputView(img)
                            | :? BackendTextureOutputView as view ->
                                match view.texture with
                                    | :? Image as img -> context.CreateImageOutputView(img, view.level, view.slice)
                                    | t -> failf "unexpected BackendTexture: %A" t
                            | v -> failf "unexpected FramebufferOutput: %A" v

                    let views =
                        pass.ColorAttachments 
                            |> Seq.map (fun (sem, att) -> createView attachments.[sem])
                            |> Seq.toList

                    let depthView =
                        pass.DepthAttachment
                            |> Option.map (fun att -> createView attachments.[DefaultSemantic.Depth])

                    context.CreateFramebuffer(pass, views @ Option.toList depthView) :> IFramebuffer

                | _ -> 
                    failf "unexpected FramebufferSignature: %A" signature

        member x.DeleteFramebuffer(fbo) = 
            match fbo with
                | :? Framebuffer as f -> 
                    f.Attachments |> List.iter (fun v -> context.Delete v)
                    context.Delete f

                | _ -> 
                    failf "unexpected Framebuffer: %A" fbo



        // utilities

        member x.Download(tex : IBackendTexture, slice : int, level : int, target : PixImage) =
            match tex with
                | :? Image as img ->
                    let sub = ImageSubResource(img, level, Range1i(slice, slice), V3i.Zero)
                    sub.Download(target) |> context.DefaultQueue.RunSynchronously
                | _ ->
                    failf "unexpected BackendTexture: %A" tex

        member x.Upload(tex : IBackendTexture, slice : int, level : int, source : PixImage) =
            match tex with
                | :? Image as img ->
                    let sub = ImageSubResource(img, level, Range1i(slice, slice), V3i.Zero)
                    sub.Upload(source) |> context.DefaultQueue.RunSynchronously
                | _ ->
                    failf "unexpected BackendTexture: %A" tex

        member x.GenerateMipMaps(t : IBackendTexture) =
            match t with
                | :? Image as img ->
                    img.GenerateMipMaps() |> context.DefaultQueue.RunSynchronously
                | _ ->
                    failf "unexpexted BackendTexture: %A" t

        member x.ResolveMultisamples(source, target, trafo) = 
            let sourceView = 
                match source with
                    | :? BackendTextureOutputView as view ->
                        match view.texture with
                            | :? Image as img -> ImageSubResource(img, view.level, Range1i(view.slice, view.slice), V3i.Zero)
                            | t -> failf "unexpected BackendTexture: %A" t

                    | :? Image as img -> ImageSubResource(img)

                    | v -> failf "unexpected FramebufferOutput: %A" v
 
            let targetView = 
                match target with
                    | :? Image as img -> ImageSubResource(img)
                    | t -> failf "unexpected BackendTexture: %A" t
            
            if trafo <> ImageTrafo.Rot0 then
                failf "ImageTrafos not implemented atm"

            sourceView.BlitTo(targetView, VkFilter.Nearest) |> context.DefaultQueue.RunSynchronously

