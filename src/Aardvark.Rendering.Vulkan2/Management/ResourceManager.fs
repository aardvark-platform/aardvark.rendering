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

#nowarn "9"
#nowarn "51"

[<AllowNullLiteral>]
type ResourceManager private (parent : Option<ResourceManager>, device : Device, renderTaskInfo : Option<IFramebufferSignature * RenderTaskLock>, shareTextures : bool, shareBuffers : bool) =
    let derivedCache (f : ResourceManager -> ResourceCache<'a>) =
        ResourceCache<'a>(Option.map f parent, Option.map snd renderTaskInfo)
        
    let descriptorPool     = device.CreateDescriptorPool(1 <<< 20, 1 <<< 22)

    let bufferCache             = derivedCache (fun m -> m.BufferCache)
    let bufferViewCache         = derivedCache (fun m -> m.BufferViewCache)
    let indexBufferCache        = derivedCache (fun m -> m.IndexBufferCache)
    let indirectBufferCache     = derivedCache (fun m -> m.IndirectBufferCache)
    let imageCache              = derivedCache (fun m -> m.ImageCache)
    let imageViewCache          = derivedCache (fun m -> m.ImageViewCache)
    let surfaceCache            = derivedCache (fun m -> m.SurfaceCache)
    let samplerCache            = derivedCache (fun m -> m.SamplerCache)
    let uniformBufferCache      = derivedCache (fun m -> m.UniformBufferCache)
    let pipelineCache           = derivedCache (fun m -> m.PipelineCache)
    let descriptorSetCache      = derivedCache (fun m -> m.DescriptorSetCache)

    let directCallCache = derivedCache (fun m -> m.DirectCallCache)
    let indirectCallCache = derivedCache (fun m -> m.IndirectCallCache)
    let vertexBindingCache = derivedCache (fun m -> m.VertexBindingCache)
    let descriptorSetBindingCache = derivedCache (fun m -> m.DescriptorSetBindingCache)

    member private x.BufferCache : ResourceCache<Buffer> = bufferCache
    member private x.BufferViewCache : ResourceCache<BufferView> = bufferViewCache
    member private x.IndexBufferCache : ResourceCache<nativeptr<VkBuffer>> = indexBufferCache
    member private x.IndirectBufferCache : ResourceCache<IndirectBuffer> = indirectBufferCache
    member private x.ImageCache : ResourceCache<Image> = imageCache
    member private x.ImageViewCache : ResourceCache<ImageView> = imageViewCache
    member private x.SurfaceCache : ResourceCache<ShaderProgram> = surfaceCache
    member private x.SamplerCache : ResourceCache<Sampler> = samplerCache
    member private x.UniformBufferCache : ResourceCache<UniformBuffer> = uniformBufferCache
    member private x.PipelineCache : ResourceCache<nativeptr<VkPipeline>> = pipelineCache
    member private x.DescriptorSetCache : ResourceCache<DescriptorSet> = descriptorSetCache
    member private x.DirectCallCache : ResourceCache<nativeptr<DrawCall>> = directCallCache
    member private x.IndirectCallCache : ResourceCache<nativeptr<DrawCall>> = indirectCallCache
    member private x.VertexBindingCache : ResourceCache<nativeptr<VertexBufferBinding>> = vertexBindingCache
    member private x.DescriptorSetBindingCache : ResourceCache<nativeptr<DescriptorSetBinding>> = descriptorSetBindingCache

    member x.Device = device

    member x.CreateRenderPass(signature : Map<Symbol, AttachmentSignature>) =
        device.CreateRenderPass(signature)

    member x.CreateBuffer(data : IMod<IBuffer>) =
        bufferCache.GetOrCreate<IBuffer>(data, {
            create = fun b      -> device.CreateBuffer(VkBufferUsageFlags.VertexBufferBit ||| VkBufferUsageFlags.TransferDstBit, b)
            update = fun h b    -> device.Delete(h); device.CreateBuffer(VkBufferUsageFlags.VertexBufferBit ||| VkBufferUsageFlags.TransferDstBit, b)
            delete = fun h      -> device.Delete(h)
            info =   fun h      -> h.Size |> Mem |> ResourceInfo
            kind = ResourceKind.Buffer
        })

    member x.CreateBufferView(view : Aardvark.Base.BufferView, data : IResource<Buffer>) =
        let fmt = VkFormat.ofType view.ElementType
        let offset = view.Offset |> int64

        bufferViewCache.GetOrCreateDependent<Buffer>(data, [fmt :> obj; offset :> obj], {
            create = fun b      -> device.CreateBufferView(b, fmt, offset, b.Size - offset)
            update = fun h b    -> device.Delete(h); device.CreateBufferView(b, fmt, offset, b.Size - offset)
            delete = fun h      -> device.Delete(h)
            info =   fun h      -> ResourceInfo.Zero
            kind = ResourceKind.UniformLocation
        })

    member x.CreateIndexBuffer(data : IMod<IBuffer>) =
        indexBufferCache.GetOrCreateVulkan(data, [], {
            create = fun b      -> device.CreateBuffer(VkBufferUsageFlags.IndexBufferBit ||| VkBufferUsageFlags.TransferDstBit, b)
            update = fun h b    -> device.Delete(h); device.CreateBuffer(VkBufferUsageFlags.IndexBufferBit ||| VkBufferUsageFlags.TransferDstBit, b)
            delete = fun h      -> device.Delete(h)
            info =   fun h      -> h.Size |> Mem |> ResourceInfo
            kind = ResourceKind.Buffer
        })

    member x.CreateIndirectBuffer(data : IMod<IIndirectBuffer>) =
        indirectBufferCache.GetOrCreate<IIndirectBuffer>(data, {
            create = fun b      -> device.CreateIndirectBuffer(b)
            update = fun h b    -> device.Delete(h); device.CreateIndirectBuffer(b)
            delete = fun h      -> device.Delete(h)
            info =   fun h      -> h.Size |> Mem |> ResourceInfo
            kind = ResourceKind.Buffer
        })


    member x.CreateImage(data : IMod<ITexture>) =
        imageCache.GetOrCreate<ITexture>(data, {
            create = fun b      -> device.CreateImage(b)
            update = fun h b    -> device.Delete(h); device.CreateImage(b)
            delete = fun h      -> device.Delete(h)
            info =   fun h      -> h.Memory.Size |> Mem |> ResourceInfo
            kind = ResourceKind.Texture
        })

    member x.CreateImageView(data : IResource<Image>) =
        imageViewCache.GetOrCreateDependent<Image>(data, [], {
            create = fun b      -> device.CreateImageView(b)
            update = fun h b    -> device.Delete(h); device.CreateImageView(b)
            delete = fun h      -> device.Delete(h)
            info =   fun h      -> ResourceInfo.Zero
            kind = ResourceKind.Texture
        })

    member x.CreateSampler (sam : IMod<SamplerStateDescription>) =
        samplerCache.GetOrCreate<SamplerStateDescription>(sam, {
            create = fun b      -> device.CreateSampler b
            update = fun h b    -> device.Delete(h); device.CreateSampler b
            delete = fun h      -> device.Delete h
            info =   fun h      -> ResourceInfo.Zero
            kind = ResourceKind.SamplerState
        })

    member x.CreateShaderProgram(signature : IFramebufferSignature, surface : IMod<ISurface>) =
        let renderPass =
            match signature with
                | :? RenderPass as p -> p
                | _ -> failf "invalid signature: %A" signature
        surfaceCache.GetOrCreate<ISurface>(surface, [signature :> obj], {
            create = fun b      -> device.CreateShaderProgram(renderPass, b)
            update = fun h b    -> device.Delete(h); device.CreateShaderProgram(renderPass, b)
            delete = fun h      -> device.Delete(h);
            info =   fun h      -> ResourceInfo.Zero
            kind = ResourceKind.ShaderProgram
        })

    member x.CreateUniformBuffer(scope : Ag.Scope, layout : UniformBufferLayout, u : IUniformProvider, additional : SymbolDict<obj>) =
        let values =
            layout.fields 
            |> List.map (fun (f) ->
                let sem = Symbol.Create f.name
                match u.TryGetUniform(scope, sem) with
                    | Some v -> f, v
                    | None -> 
                        match additional.TryGetValue sem with
                            | (true, (:? IMod as m)) -> f, m
                            | _ -> failwithf "[Vulkan] could not get uniform: %A" f
            )

        let writers = 
            values |> List.map (fun (target, m) ->
                match m.GetType() with
                    | ModOf tSource -> m, UniformWriters.getWriter target.offset target.fieldType tSource
                    | _ -> failf ""
            )

        let key = (layout :> obj) :: (values |> List.map (fun (_,v) -> v :> obj))

        uniformBufferCache.GetOrCreate(
            key, fun () ->
                { new Aardvark.Base.Rendering.Resource<UniformBuffer>(ResourceKind.UniformBuffer) with
                    member x.GetInfo b = 
                        b.Size |> Mem |> ResourceInfo

                    member x.Create old =
                        let buffer = 
                            match old with
                                | None -> device.CreateUniformBuffer(layout)
                                | Some o -> o
                        for (m,w) in writers do w.Write(x, m, buffer.Storage.Pointer)
                        buffer, FrameStatistics.Zero

                    member x.Destroy h =
                        device.Delete h
                }
        )

    member x.CreatePipeline(pass            : RenderPass,
                            program         : IResource<ShaderProgram>,
                            inputs          : Map<string, bool * Type>,
                            geometryMode    : IMod<IndexedGeometryMode>,
                            fillMode        : IMod<FillMode>,
                            cullMode        : IMod<CullMode>,
                            blendMode       : IMod<BlendMode>,
                            depthTest       : IMod<DepthTestMode>,
                            stencilMode     : IMod<StencilMode>) =
        let key =
            [ 
                pass :> obj; 
                program :> obj
                inputs :> obj
                geometryMode :> obj
                fillMode :> obj
                cullMode :> obj
                blendMode :> obj
                depthTest :> obj
                stencilMode :> obj
            ]

        let anyAttachment = pass.ColorAttachments |> Map.toSeq |> Seq.head |> snd |> snd
        pipelineCache.GetOrCreateVulkan(
            key, fun () ->
                { new Aardvark.Base.Rendering.Resource<Pipeline>(ResourceKind.ShaderProgram) with
                    member x.GetInfo b = 
                        ResourceInfo.Zero

                    member x.Create old =
                        let stats = program.Update(x)
                        
                        let desc =
                            {
                                renderPass              = pass
                                shaderProgram           = program.Handle.GetValue()
                                vertexInputState        = VertexInputState.create inputs
                                inputAssembly           = InputAssemblyState.ofIndexedGeometryMode (geometryMode.GetValue x)
                                rasterizerState         = RasterizerState.create (cullMode.GetValue x) (fillMode.GetValue x)
                                colorBlendState         = ColorBlendState.create pass.ColorAttachmentCount (blendMode.GetValue x)
                                multisampleState        = MultisampleState.create anyAttachment.samples
                                depthState              = DepthState.create (depthTest.GetValue x)
                                stencilState            = StencilState.create (stencilMode.GetValue x)
                                dynamicStates           = [||]
                            }

                        match old with
                            | Some o -> 
                                if o.Description = desc then 
                                    o, FrameStatistics.Zero
                                else
                                    device.Delete o
                                    device.CreateGraphicsPipeline desc, FrameStatistics.Zero
                            | None ->
                                device.CreateGraphicsPipeline desc, FrameStatistics.Zero



                    member x.Destroy h =
                        program.RemoveRef()
                        program.RemoveOutput x
                        device.Delete h
                }
        )

    member x.CreateDescriptorSet(layout : DescriptorSetLayout,
                                 buffers : Map<int, IResource<UniformBuffer>>, 
                                 images : Map<int, IResource<ImageView> * IResource<Sampler>>) =
        descriptorSetCache.GetOrCreate(
            [layout :> obj; buffers :> obj; images :> obj],
            fun () ->
                { new Aardvark.Base.Rendering.Resource<DescriptorSet>(ResourceKind.UniformLocation) with
                    member x.Create old =
                        let desc =
                            match old with
                                | Some o -> o
                                | None -> descriptorPool.Alloc(layout)
                        

                        let mutable stats = FrameStatistics.Zero
                        

                        let buffers = 
                            buffers |> Map.map (fun _ r -> 
                                stats <- stats + r.Update(x)
                                Descriptor.UniformBuffer(r.Handle.GetValue())
                            )

                        let images = 
                            images |> Map.map (fun _ (v,s) ->
                                stats <- stats + v.Update(x)
                                stats <- stats + s.Update(x)
                                Descriptor.SampledImage(v.Handle.GetValue(), s.Handle.GetValue())
                            )
                        
                        descriptorPool.Update(desc, Map.union buffers images)
                        desc, stats


                    member x.Destroy h =
                        descriptorPool.Free h
                        for (_,b) in Map.toSeq buffers do
                            b.RemoveRef()
                            b.RemoveOutput x

                        for (_,(i,s)) in Map.toSeq images do
                            i.RemoveRef()
                            i.RemoveOutput x
                            s.RemoveRef()
                            s.RemoveOutput x

                    member x.GetInfo h =
                        ResourceInfo.Zero

                }
        )


    member x.CreateDrawCall(indexed : bool, calls : IMod<list<DrawCallInfo>>) =
        directCallCache.GetOrCreate(calls, [indexed :> obj], {
            create = fun b      -> device.CreateDrawCall(indexed, b)
            update = fun h b    -> device.UpdateDrawCall(h, indexed, b); h
            delete = fun h      -> device.Delete(h)
            info =   fun h      -> ResourceInfo.Zero
            kind = ResourceKind.Unknown
        })

    member x.CreateDrawCall(indexed : bool, calls : IResource<IndirectBuffer>) =
        directCallCache.GetOrCreateDependent(calls, [indexed :> obj], {
            create = fun b      -> device.CreateDrawCall(indexed, b)
            update = fun h b    -> device.UpdateDrawCall(h, indexed, b); h
            delete = fun h      -> device.Delete(h)
            info =   fun h      -> ResourceInfo.Zero
            kind = ResourceKind.Unknown
        })

    member x.CreateVertexBufferBinding(buffers : list<IResource<Buffer> * int64>) =
        vertexBindingCache.GetOrCreate(
            [buffers :> obj],
            (fun () ->
                let inputs = buffers |> List.map (fun (r,_) -> r :> IResource)

                let create (old : Option<nativeptr<VertexBufferBinding>>) =
                    let buffersAndOffsets = buffers |> List.map (fun (b,o) -> (Mod.force b.Handle, o)) |> List.toArray
                    match old with
                        | None ->
                            device.CreateVertexBufferBinding(0, buffersAndOffsets)
                        | Some old ->
                            device.UpdateVertexBufferBinding(old, 0, buffersAndOffsets)
                            old

                let destroy (ptr : nativeptr<VertexBufferBinding>) =
                    device.Delete(ptr)

                inputs |> Resource.custom create destroy |> unbox<Aardvark.Base.Rendering.Resource<nativeptr<VertexBufferBinding>>>
            )
        )

    member x.CreateDescriptorSetBinding(layout : PipelineLayout, bindings : list<IResource<DescriptorSet>>) =
        descriptorSetBindingCache.GetOrCreate(
            [bindings :> obj; layout :> obj],
            (fun () ->
                let inputs = bindings |> List.map (fun r -> r :> IResource)

                let create (old : Option<nativeptr<DescriptorSetBinding>>) =
                    let handles = bindings |> List.map (fun b -> Mod.force b.Handle) |> List.toArray
                    match old with
                        | None ->
                            device.CreateDescriptorSetBinding(layout, 0, handles)
                        | Some old ->
                            device.UpdateDescriptorSetBinding(old, layout, 0, handles)
                            old

                let destroy (ptr : nativeptr<DescriptorSetBinding>) =
                    device.Delete(ptr)

                inputs |> Resource.custom create destroy |> unbox<Aardvark.Base.Rendering.Resource<nativeptr<DescriptorSetBinding>>>
            )
        )

    member x.RenderTaskLock = renderTaskInfo

    new(parent, ctx, lock, shareTextures, shareBuffers) = ResourceManager(Some parent, ctx, lock, shareTextures, shareBuffers)
    new(ctx, lock, shareTextures, shareBuffers) = ResourceManager(None, ctx, lock, shareTextures, shareBuffers)
