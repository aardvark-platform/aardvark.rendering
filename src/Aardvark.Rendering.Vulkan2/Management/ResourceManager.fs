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

//type private DrawCallResource(device : Device, indexed : bool, calls : IMod<list<DrawCallInfo>>) =
//    inherit Rendering.Resource<nativeint, nativeint>(ResourceKind.Unknown)
//
//    override x.View h = h
//
//    override x.Create(token : AdaptiveToken, rt : RenderToken, old : Option<nativeint>) =
//        match old with
//            | Some o ->
//                device.UpdateDrawCall(NativePtr.ofNativeInt o, indexed, calls.GetValue token)
//                o
//            | None -> 
//                let ptr = device.CreateDrawCall(indexed, calls.GetValue token)
//                NativePtr.toNativeInt ptr
//
//    override x.Destroy(old : nativeint) =
//        device.Delete(NativePtr.ofNativeInt<DrawCall> old)
//
//    override x.GetInfo _ =
//        ResourceInfo.Zero


[<AllowNullLiteral>]
type ResourceManager private (parent : Option<ResourceManager>, device : Device, renderTaskInfo : Option<IFramebufferSignature * RenderTaskLock>, shareTextures : bool, shareBuffers : bool) =
    let derivedCache (f : ResourceManager -> ResourceCache<'a, 'v>) =
        ResourceCache<'a, 'v>(Option.map f parent, Option.map snd renderTaskInfo)
 
        
    let descriptorPool = 
        match parent with
            | Some p -> p.DescriptorPool
            | _ -> device.CreateDescriptorPool(1 <<< 20, 1 <<< 22)

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
    let indexBufferBindingCache = derivedCache (fun m -> m.IndexBufferBindingCache)

    member private x.BufferCache : ResourceCache<Buffer, VkBuffer> = bufferCache
    member private x.BufferViewCache : ResourceCache<BufferView, VkBufferView> = bufferViewCache
    member private x.IndexBufferCache : ResourceCache<Buffer, VkBuffer> = indexBufferCache
    member private x.IndirectBufferCache : ResourceCache<IndirectBuffer, VkBuffer> = indirectBufferCache
    member private x.ImageCache : ResourceCache<Image, VkImage> = imageCache
    member private x.ImageViewCache : ResourceCache<ImageView, VkImageView> = imageViewCache
    member private x.SurfaceCache : ResourceCache<ShaderProgram, nativeint> = surfaceCache
    member private x.SamplerCache : ResourceCache<Sampler, VkSampler> = samplerCache
    member private x.UniformBufferCache : ResourceCache<UniformBuffer, VkBuffer> = uniformBufferCache
    member private x.PipelineCache : ResourceCache<Pipeline, VkPipeline> = pipelineCache
    member private x.DescriptorSetCache : ResourceCache<DescriptorSet, VkDescriptorSet> = descriptorSetCache
    member private x.DirectCallCache : ResourceCache<DrawCall, DrawCall> = directCallCache
    member private x.IndirectCallCache : ResourceCache<DrawCall, DrawCall> = indirectCallCache
    member private x.VertexBindingCache : ResourceCache<VertexBufferBinding, VertexBufferBinding> = vertexBindingCache
    member private x.DescriptorSetBindingCache : ResourceCache<DescriptorSetBinding, DescriptorSetBinding> = descriptorSetBindingCache
    member private x.IndexBufferBindingCache : ResourceCache<IndexBufferBinding, IndexBufferBinding> = indexBufferBindingCache

    member private x.DescriptorPool : DescriptorPool = descriptorPool

    member x.Device = device

    member x.CreateRenderPass(signature : Map<Symbol, AttachmentSignature>) =
        device.CreateRenderPass(signature)

    member x.CreateBuffer(data : IMod<IBuffer>) =
        match data with
            | :? SingleValueBuffer as data ->
                let update (h : Buffer) (v : V4f) =
                    device.eventually {
                        let temp = device.HostMemory.AllocTemp(device.MinUniformBufferOffsetAlignment, 16L)
                        temp.Mapped(fun ptr -> NativeInt.write ptr v)
                        try do! Command.Copy(temp, h)
                        finally temp.Dispose()
                    }
                    h
                bufferCache.GetOrCreate(data.Value, {
                    create = fun v      -> device.CreateBuffer(VkBufferUsageFlags.VertexBufferBit ||| VkBufferUsageFlags.TransferDstBit, ArrayBuffer [|v|])
                    update = fun h v    -> update h v
                    delete = fun h      -> device.Delete(h)
                    info =   fun h      -> h.Size |> Mem |> ResourceInfo
                    view =   fun h -> h.Handle
                    kind = ResourceKind.Buffer
                })
            | _ -> 
                bufferCache.GetOrCreate<IBuffer>(data, {
                    create = fun b      -> device.CreateBuffer(VkBufferUsageFlags.VertexBufferBit ||| VkBufferUsageFlags.TransferDstBit, b)
                    update = fun h b    -> device.Delete(h); device.CreateBuffer(VkBufferUsageFlags.VertexBufferBit ||| VkBufferUsageFlags.TransferDstBit, b)
                    delete = fun h      -> device.Delete(h)
                    info =   fun h      -> h.Size |> Mem |> ResourceInfo
                    view =   fun h -> h.Handle
                    kind = ResourceKind.Buffer
                })

    member x.CreateBufferView(view : Aardvark.Base.BufferView, data : IResource<Buffer, VkBuffer>) =
        let fmt = VkFormat.ofType view.ElementType
        let offset = view.Offset |> int64

        bufferViewCache.GetOrCreateDependent<Buffer, VkBuffer>(data, [fmt :> obj; offset :> obj], {
            create = fun b      -> device.CreateBufferView(b, fmt, offset, b.Size - offset)
            update = fun h b    -> device.Delete(h); device.CreateBufferView(b, fmt, offset, b.Size - offset)
            delete = fun h      -> device.Delete(h)
            info =   fun h      -> ResourceInfo.Zero
            view =   fun h -> h.Handle
            kind = ResourceKind.UniformLocation
        })

    member x.CreateIndexBuffer(data : IMod<IBuffer>) =
        indexBufferCache.GetOrCreate(data, [], {
            create = fun b      -> device.CreateBuffer(VkBufferUsageFlags.IndexBufferBit ||| VkBufferUsageFlags.TransferDstBit, b)
            update = fun h b    -> device.Delete(h); device.CreateBuffer(VkBufferUsageFlags.IndexBufferBit ||| VkBufferUsageFlags.TransferDstBit, b)
            delete = fun h      -> device.Delete(h)
            info =   fun h      -> h.Size |> Mem |> ResourceInfo
            view =   fun h -> h.Handle
            kind = ResourceKind.Buffer
        })

    member x.CreateIndirectBuffer(indexed : bool, data : IMod<IIndirectBuffer>) =
        indirectBufferCache.GetOrCreate<IIndirectBuffer>(data, [indexed], {
            create = fun b      -> device.CreateIndirectBuffer(indexed, b)
            update = fun h b    -> device.Delete(h); device.CreateIndirectBuffer(indexed, b)
            delete = fun h      -> device.Delete(h)
            info =   fun h      -> h.Size |> Mem |> ResourceInfo
            view =   fun h      -> h.Handle
            kind = ResourceKind.Buffer
        })


    member x.CreateImage(data : IMod<ITexture>) =
        imageCache.GetOrCreate<ITexture>(data, {
            create = fun b      -> device.CreateImage(b)
            update = fun h b    -> device.Delete(h); device.CreateImage(b)
            delete = fun h      -> device.Delete(h)
            info =   fun h      -> h.Memory.Size |> Mem |> ResourceInfo
            view =   fun h      -> h.Handle
            kind = ResourceKind.Texture
        })

    member x.CreateImageView(data : IResource<Image, VkImage>) =
        imageViewCache.GetOrCreateDependent<Image, VkImage>(data, [], {
            create = fun b      -> device.CreateImageView(b)
            update = fun h b    -> device.Delete(h); device.CreateImageView(b)
            delete = fun h      -> device.Delete(h)
            info =   fun h      -> ResourceInfo.Zero
            view =   fun h      -> h.Handle
            kind = ResourceKind.Texture
        })

    member x.CreateSampler (sam : IMod<SamplerStateDescription>) =
        samplerCache.GetOrCreate<SamplerStateDescription>(sam, {
            create = fun b      -> device.CreateSampler b
            update = fun h b    -> device.Delete(h); device.CreateSampler b
            delete = fun h      -> device.Delete h
            info =   fun h      -> ResourceInfo.Zero
            view =   fun h      -> h.Handle
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
            view =   fun h      -> 0n
            kind = ResourceKind.ShaderProgram
        })

    member x.CreateUniformBuffer(scope : Ag.Scope, layout : UniformBufferLayout, u : IUniformProvider, additional : SymbolDict<IMod>) =
        let values =
            layout.fields 
            |> List.map (fun (f) ->
                let sem = Symbol.Create f.name
                match Uniforms.tryGetDerivedUniform f.name u with
                    | Some r -> f, r
                    | None -> 
                        match u.TryGetUniform(scope, sem) with
                            | Some v -> f, v
                            | None -> 
                                match additional.TryGetValue sem with
                                    | (true, m) -> f, m
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
                { new Aardvark.Base.Rendering.Resource<UniformBuffer, VkBuffer>(ResourceKind.UniformBuffer) with

                    member x.View h =
                        h.Handle

                    member x.GetInfo b = 
                        b.Size |> Mem |> ResourceInfo

                    member x.Create(token, rt, old) =
                        let buffer = 
                            match old with
                                | None -> device.CreateUniformBuffer(layout)
                                | Some o -> o
                        for (m,w) in writers do w.Write(token, m, buffer.Storage.Pointer)
                        device.Upload(buffer)
                        buffer

                    member x.Destroy h =
                        device.Delete h
                }
        )

    member x.CreatePipeline(pass            : RenderPass,
                            program         : IResource<ShaderProgram>,
                            inputs          : Map<Symbol, bool * Aardvark.Base.BufferView>,
                            geometryMode    : IMod<IndexedGeometryMode>,
                            fillMode        : IMod<FillMode>,
                            cullMode        : IMod<CullMode>,
                            blendMode       : IMod<BlendMode>,
                            depthTest       : IMod<DepthTestMode>,
                            stencilMode     : IMod<StencilMode>,
                            writeBuffers    : Option<Set<Symbol>>) =

        let writeBuffers =
            match writeBuffers with
                | Some set -> 
                    if Set.isSuperset set pass.Semantics then pass.Semantics
                    else set
                | None ->
                    pass.Semantics
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
                writeBuffers :> obj
            ]

        let anyAttachment = 
            match pass.ColorAttachments |> Map.toSeq |> Seq.tryHead with
                | Some (_,(_,a)) -> a
                | None -> pass.DepthStencilAttachment |> Option.get

        pipelineCache.GetOrCreate(
            key, fun () ->
                let writeMasks = Array.zeroCreate pass.ColorAttachmentCount
                for (i, (sem,_)) in Map.toSeq pass.ColorAttachments do 
                    if Set.contains sem writeBuffers then writeMasks.[i] <- true
                    else writeMasks.[i] <- false

                let writeDepth = Set.contains DefaultSemantic.Depth writeBuffers

                let usesDiscard = program.Handle.GetValue().HasDiscard
                let vertexInputState = VertexInputState.create inputs
                let inputAssembly = Mod.map InputAssemblyState.ofIndexedGeometryMode geometryMode
                let rasterizerState = Mod.custom (fun self -> RasterizerState.create usesDiscard (depthTest.GetValue self) (cullMode.GetValue self) (fillMode.GetValue self))
                let colorBlendState = Mod.map (ColorBlendState.create writeMasks pass.ColorAttachmentCount) blendMode
                let multisampleState = MultisampleState.create anyAttachment.samples
                let depthState = Mod.map (DepthState.create writeDepth) depthTest
                let stencilState = Mod.map StencilState.create stencilMode

                { new Aardvark.Base.Rendering.Resource<Pipeline, VkPipeline>(ResourceKind.ShaderProgram) with
                    member x.View h = h.Handle

                    member x.GetInfo b = 
                        ResourceInfo.Zero

                    member x.Create(token, rt, old) =
                        let stats = program.Update(token, rt)
                        
                        let desc =
                            {
                                renderPass              = pass
                                shaderProgram           = program.Handle.GetValue()
                                vertexInputState        = vertexInputState
                                inputAssembly           = inputAssembly.GetValue token
                                rasterizerState         = rasterizerState.GetValue token
                                colorBlendState         = colorBlendState.GetValue token
                                multisampleState        = multisampleState
                                depthState              = depthState.GetValue token
                                stencilState            = stencilState.GetValue token
                                dynamicStates           = [| VkDynamicState.Viewport; VkDynamicState.Scissor |]
                            }

                        match old with
                            | Some o -> 
                                if o.Description = desc then 
                                    o
                                else
                                    device.Delete o
                                    device.CreateGraphicsPipeline desc
                            | None ->
                                device.CreateGraphicsPipeline desc



                    member x.Destroy h =
                        program.RemoveRef()
                        program.RemoveOutput x
                        device.Delete h
                }
        )

    member x.CreateDescriptorSet(layout : DescriptorSetLayout, descriptors : array<AdaptiveDescriptor>) =
        let desc = descriptors |> Array.toList
        descriptorSetCache.GetOrCreate(
            [layout :> obj; desc :> obj],
            fun () ->
                { new Aardvark.Base.Rendering.Resource<DescriptorSet, VkDescriptorSet>(ResourceKind.UniformLocation) with
                    member x.View h = h.Handle
                    member x.Create(token, rt, old) =
                        let desc =
                            match old with
                                | Some o -> o
                                | None -> descriptorPool.Alloc(layout)
                        
                        let innerToken = RenderToken(rt)
                        let descriptors =
                            descriptors |> Array.map (fun d ->
                                match d with
                                    | AdaptiveUniformBuffer(i, b) -> 
                                        Descriptor.UniformBuffer(i, b)

                                    | AdaptiveCombinedImageSampler(i, viewSam) ->
                                        let vs = Array.zeroCreate viewSam.Length
                                        for i in 0 .. viewSam.Length - 1 do
                                            match viewSam.[i] with
                                                | Some(v,s) ->
                                                    v.Update(token, innerToken)
                                                    s.Update(token, innerToken)
                                                    vs.[i] <- Some (v.Handle.GetValue(), s.Handle.GetValue())
                                                | _ ->
                                                    vs.[i] <- None

                                        Descriptor.CombinedImageSampler(i, vs)
                            )   

                        // update if any of the handles changed or there was no old
                        if Option.isNone old || innerToken.ReplacedResources <> 0 then
                            descriptorPool.Update(desc, descriptors)

                        desc


                    member x.Destroy h =
                        descriptorPool.Free h

                        for d in descriptors do
                            match d with
                                | AdaptiveCombinedImageSampler(_,vs) ->
                                    for vv in vs do
                                        match vv with
                                            | Some(i,s) -> 
                                                i.RemoveRef()
                                                i.RemoveOutput x
                                                s.RemoveRef()
                                                s.RemoveOutput x
                                            | None ->
                                                ()
                                | _ ->
                                    ()

                    member x.GetInfo h =
                        ResourceInfo.Zero

                }
        )


    member x.CreateDrawCall(indexed : bool, calls : IMod<list<DrawCallInfo>>) =
        directCallCache.GetOrCreate( calls, [indexed], {
            create = fun b      -> device.CreateDrawCall(indexed, b)
            update = fun h b    -> device.CreateDrawCall(indexed, b)
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view =   fun h      -> h
            kind = ResourceKind.Unknown
        })

    member x.CreateDrawCall(indexed : bool, calls : IResource<IndirectBuffer, VkBuffer>) =
        indirectCallCache.GetOrCreateDependent(calls, [indexed :> obj], {
            create = fun b      -> device.CreateDrawCall(indexed, b)
            update = fun h b    -> device.CreateDrawCall(indexed, b)
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view =   fun h      -> h
            kind = ResourceKind.Unknown
        })

    member x.CreateVertexBufferBinding(buffers : list<IResource<Buffer, VkBuffer> * int64>) =
        vertexBindingCache.GetOrCreate(
            [buffers :> obj],
            (fun () ->
                let inputs = buffers |> List.map (fun (r,_) -> r :> IResource)

                let create (caller : AdaptiveToken) (token : RenderToken) (old : Option<VertexBufferBinding>) =
                    let buffersAndOffsets = buffers |> List.map (fun (b,o) -> (Mod.force b.Handle, o)) |> List.toArray
                    new VertexBufferBinding(0, buffersAndOffsets)

                let destroy (ptr : VertexBufferBinding) =
                    ()

                let view (p : VertexBufferBinding) = 
                    p

                inputs |> Resource.custom create destroy view |> unbox<Aardvark.Base.Rendering.Resource<VertexBufferBinding, VertexBufferBinding>>
            )
        )

    member x.CreateDescriptorSetBinding(layout : PipelineLayout, bindings : array<IResource<DescriptorSet, VkDescriptorSet>>) =
        let key = bindings |> Array.toList
        descriptorSetBindingCache.GetOrCreate(
            [key :> obj; layout :> obj],
            (fun () ->
                let inputs = bindings |> Array.map (fun r -> r :> IResource)

                let create (caller : AdaptiveToken) (token : RenderToken) (old : Option<DescriptorSetBinding>) =
                    let handles = bindings |> Array.map (fun b -> Mod.force b.Handle)
                    new DescriptorSetBinding(layout, 0, handles)

                let destroy (ptr : DescriptorSetBinding) =
                    ()

                inputs |> Array.toList |> Resource.custom create destroy id |> unbox<Aardvark.Base.Rendering.Resource<DescriptorSetBinding, DescriptorSetBinding>>
            )
        )
        
    member x.CreateIndexBufferBinding(binding : IResource<Buffer, VkBuffer>, t : VkIndexType) =
        indexBufferBindingCache.GetOrCreate(
            [binding :> obj; t :> obj],
            (fun () ->
                let create (caller : AdaptiveToken) (token : RenderToken) (old : Option<IndexBufferBinding>) =
                    new IndexBufferBinding(binding.Handle.GetValue().Handle, t)

                let destroy (ptr : IndexBufferBinding) =
                    ()

                [binding :> IResource] |> Resource.custom create destroy id |> unbox<Aardvark.Base.Rendering.Resource<IndexBufferBinding, IndexBufferBinding>>
            )
        )

    member x.RenderTaskLock = renderTaskInfo

    member x.Dispose() =
        match parent with
            | None -> 
                device.Delete descriptorPool
                bufferCache.Clear()             
                bufferViewCache.Clear() 
                indexBufferCache.Clear() 
                indirectBufferCache.Clear() 
                imageCache.Clear()     
                imageViewCache.Clear()    
                surfaceCache.Clear()           
                samplerCache.Clear() 
                uniformBufferCache.Clear() 
                pipelineCache.Clear()       
                descriptorSetCache.Clear() 
                directCallCache.Clear()
                indirectCallCache.Clear()
                vertexBindingCache.Clear()
                descriptorSetBindingCache.Clear()   
            | Some p ->
                ()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    new(parent, ctx, lock, shareTextures, shareBuffers) = new ResourceManager(Some parent, ctx, lock, shareTextures, shareBuffers)
    new(ctx, lock, shareTextures, shareBuffers) = new ResourceManager(None, ctx, lock, shareTextures, shareBuffers)
