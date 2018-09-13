namespace Aardvark.Rendering.GL

#nowarn "9"
#nowarn "51"

open System
open System.Threading
open System.Collections.Concurrent
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open OpenTK.Graphics.OpenGL4
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.GL


        
type UniformBufferManager(ctx : Context) =

    let bufferMemory : Management.Memory<Buffer> =

        let alloc (size : nativeint) =
            use __ = ctx.ResourceLock
            let handle = GL.GenBuffer()

            BufferMemoryUsage.addUniformBuffer ctx (int64 size)

            GL.NamedBufferStorage(handle, size, 0n, BufferStorageFlags.DynamicStorageBit)
            GL.Check "could not allocate uniform buffer"

            new Buffer(ctx, size, handle)

        let free (buffer : Buffer) (size : nativeint) =
            GL.DeleteBuffer(buffer.Handle)
            BufferMemoryUsage.removeUniformBuffer ctx (int64 size)
            GL.Check "could not free uniform buffer"

        {
            malloc = alloc
            mfree = free
            mcopy = fun _ _ _ _ -> failwith "not implemented"
            mrealloc = fun _ _ _ -> failwith "not implemented"
        }


    let manager = new Management.ChunkedMemoryManager<_>(bufferMemory, 8n <<< 20)

    //let buffer = 
    //    // TODO: better implementation for uniform buffers (see https://github.com/aardvark-platform/aardvark.rendering/issues/32)
    //    use __ = ctx.ResourceLock
    //    let handle = GL.GenBuffer()
    //    GL.Check "could not create buffer"
    //    new FakeSparseBuffer(ctx, handle, id, id) :> SparseBuffer

    //let manager = MemoryManager.createNop()

    let viewCache = ResourceCache<UniformBufferView, int>(None, None)
    let rw = new ReaderWriterLockSlim()

    member x.CreateUniformBuffer(block : FShade.GLSL.GLSLUniformBuffer, scope : Ag.Scope, u : IUniformProvider, additional : SymbolDict<IMod>) : IResource<UniformBufferView, int> =
        let values =
            block.ubFields 
            |> List.map (fun f ->
                let name = f.ufName
                let sem = Symbol.Create name

                match Uniforms.tryGetDerivedUniform name u with
                    | Some v -> sem, v
                    | None -> 
                        match u.TryGetUniform(scope, sem) with
                            | Some v -> sem, v
                            | None -> 
                                match additional.TryGetValue sem with
                                    | (true, m) -> sem, m
                                    | _ -> failwithf "[GL] could not get uniform: %A" f
            )

        let key = values |> List.map (fun (_,v) -> v :> obj)

        let alignedSize = (block.ubSize + 255) &&& ~~~255

        viewCache.GetOrCreate(
            key,
            fun () ->
                let writers = List.map2 (fun (f : FShade.GLSL.GLSLUniformBufferField) (_,v) -> nativeint f.ufOffset, ShaderParameterWriter.adaptive v (ShaderParameterType.ofGLSLType f.ufType)) block.ubFields values

                let mutable block = Unchecked.defaultof<_>
                let mutable store = 0n
                { new Resource<UniformBufferView, int>(ResourceKind.UniformBuffer) with
                    member x.GetInfo b = 
                        b.Size |> Mem |> ResourceInfo

                    member x.View(b : UniformBufferView) =
                        b.Buffer.Handle

                    member x.Create(token, rt, old) =
                        use __ = ctx.ResourceLock
                        let handle = 
                            match old with
                                | Some old -> old
                                | None ->
                                    block <- manager.Alloc(nativeint alignedSize)
                                    store <- System.Runtime.InteropServices.Marshal.AllocHGlobal alignedSize
                                    //buffer.Commitment(block.Offset, block.Size, true)
                                    UniformBufferView(block.Memory.Value, block.Offset, nativeint block.Size)

                        for (offset,w) in writers do w.Write(token, store + offset)

                        GL.NamedBufferSubData(handle.Buffer.Handle, handle.Offset, handle.Size, store)
                        GL.Check "could not upload uniform buffer"
                        //buffer.WriteUnsafe(handle.Offset, handle.Size, store)
                        handle

                    member x.Destroy h =
                        if not block.IsFree then
                            System.Runtime.InteropServices.Marshal.FreeHGlobal store
                            use __ = ctx.ResourceLock
                            manager.Free block

                }
        )

    member x.Dispose() =
        manager.Dispose()

type DrawBufferConfig =
    class
        val mutable public Key : list<bool>
        val mutable public Parent : DrawBufferManager
        val mutable public Signature : IFramebufferSignature
        val mutable public Count : int
        val mutable public Buffers : nativeptr<int>
        val mutable public RefCount : int

        member x.Write(fbo : Framebuffer) =
            x.Signature.ColorAttachments |> Map.iter (fun i (s,_) ->
                if x.Key.[i] then
                    if fbo.Handle = 0 && i = 0 && s = DefaultSemantic.Colors then
                        NativePtr.set x.Buffers i (int OpenTK.Graphics.OpenGL4.FramebufferAttachment.BackLeft)
                    else
                        NativePtr.set x.Buffers i (int OpenTK.Graphics.OpenGL4.FramebufferAttachment.ColorAttachment0 + i)
                else
                    NativePtr.set x.Buffers i 0
            )

        member x.AddRef() = 
            if Interlocked.Increment &x.RefCount = 1 then
                x.Buffers <- NativePtr.alloc x.Count

        member x.RemoveRef() = 
            if Interlocked.Decrement &x.RefCount = 0 then
                NativePtr.free x.Buffers
                x.Buffers <- NativePtr.zero
                x.Parent.DeleteConfig(x)

        new(p, key, s, c) = { Parent = p; Key = key; Signature = s; Count = c; Buffers = NativePtr.zero; RefCount = 0 }

    end

and DrawBufferManager (signature : IFramebufferSignature) =
    let count = signature.ColorAttachments.Count
    let ptrs = ConcurrentDictionary<list<bool>, DrawBufferConfig>()

    member x.Write(fbo : Framebuffer) =
        for (KeyValue(_,dbc)) in ptrs do
            if dbc.RefCount > 0 then
                dbc.Write(fbo)

    member x.CreateConfig(set : Set<Symbol>) =
        let set = signature.ColorAttachments |> Map.toSeq |> Seq.map (fun (i,(s,_)) -> Set.contains s set) |> Seq.toList
        let config = 
            ptrs.GetOrAdd(set, fun set ->
                DrawBufferConfig(x, set, signature, count)
            )

        config.AddRef()
        config

    member internal x.DeleteConfig(c : DrawBufferConfig) =
        ptrs.TryRemove c.Key |> ignore






type CastResource<'a, 'b when 'a : equality and 'b : equality>(inner : IResource<'a>) =
    inherit AdaptiveDecorator(inner)
    static let th = typeof<'b>
    let handle = inner.Handle |> Mod.cast
    member x.Inner = inner

    override x.GetHashCode() = inner.GetHashCode()
    override x.Equals o = 
        match o with
            | :? CastResource<'a,'b> as o -> inner.Equals o.Inner
            | _ -> false

    interface IResource with  
        member x.HandleType = th
        member x.Dispose() = inner.Dispose()
        member x.AddRef() = inner.AddRef()
        member x.RemoveRef() = inner.RemoveRef()
        member x.Update(caller, token) = inner.Update(caller, token)
        member x.Info = inner.Info
        member x.IsDisposed = inner.IsDisposed
        member x.Kind = inner.Kind

    interface IResource<'b> with
        member x.Handle = handle

[<Struct>]
type TextureBinding =
    {
        offset : int
        count : int
        targets : nativeptr<int>
        textures : nativeptr<int>
        samplers : nativeptr<int>
    }

[<AllowNullLiteral>]
type ResourceManager private (parent : Option<ResourceManager>, ctx : Context, renderTaskInfo : Option<IFramebufferSignature * RenderTaskLock>, shareTextures : bool, shareBuffers : bool) =
    
    let drawBufferManager = // ISSUE: leak? nobody frees those DrawBufferConfigs
        match renderTaskInfo with
            | Some (signature, _) -> DrawBufferManager(signature) |> Some
            | _ -> None

    let derivedCache (f : ResourceManager -> ResourceCache<'a, 'b>) =
        ResourceCache<'a, 'b>(Option.map f parent, Option.map snd renderTaskInfo)

    let bufferManager           = match parent with | Some p -> p.BufferManager
                                                    | None -> Sharing.BufferManager(ctx, shareBuffers)
    let textureManager          = match parent with | Some p -> p.TextureManager
                                                    | None -> Sharing.TextureManager(ctx, shareTextures)

    let bufferCache             = derivedCache (fun m -> m.BufferCache)
    let textureCache            = derivedCache (fun m -> m.TextureCache)
    let indirectBufferCache     = derivedCache (fun m -> m.IndirectBufferCache)
    let programHandleCache      = ResourceCache<Program, int>(None, Option.map snd renderTaskInfo)
    let samplerCache            = derivedCache (fun m -> m.SamplerCache)
    let vertexInputCache        = derivedCache (fun m -> m.VertexInputCache)
    let uniformLocationCache    = derivedCache (fun m -> m.UniformLocationCache)

    let isActiveCache           = derivedCache (fun m -> m.IsActiveCache)
    let beginModeCache          = derivedCache (fun m -> m.BeginModeCache)
    let drawCallInfoCache       = derivedCache (fun m -> m.DrawCallInfoCache)
    let depthTestCache          = derivedCache (fun m -> m.DepthTestCache)
    let cullModeCache           = derivedCache (fun m -> m.CullModeCache)
    let polygonModeCache        = derivedCache (fun m -> m.PolygonModeCache)
    let blendModeCache          = derivedCache (fun m -> m.BlendModeCache)
    let stencilModeCache        = derivedCache (fun m -> m.StencilModeCache)
    let flagCache               = derivedCache (fun m -> m.FlagCache)

    
    let textureBindingCache     = derivedCache (fun m -> m.TextureBindingCache)

    let uniformBufferManager = UniformBufferManager ctx

    let hasTessDrawModeCache = 
        ConcurrentDictionary<IndexedGeometryMode, UnaryCache<IMod<bool>, IMod<GLBeginMode>>>()
        
    let getTessDrawModeCache (mode : IndexedGeometryMode) =
        hasTessDrawModeCache.GetOrAdd(mode, fun mode ->
            UnaryCache(Mod.map (fun t -> ctx.ToBeginMode(mode, t)))
        )

    let samplerDescriptionCache = UnaryCache(fun (samplerState : FShade.SamplerState) -> samplerState.SamplerStateDescription)
    
    member private x.BufferManager = bufferManager
    member private x.TextureManager = textureManager

    member private x.BufferCache            : ResourceCache<Buffer, int>                    = bufferCache
    member private x.TextureCache           : ResourceCache<Texture, V2i>                   = textureCache
    member private x.IndirectBufferCache    : ResourceCache<IndirectBuffer, V2i>            = indirectBufferCache
    member private x.SamplerCache           : ResourceCache<Sampler, int>                   = samplerCache
    member private x.VertexInputCache       : ResourceCache<VertexInputBindingHandle, int>  = vertexInputCache
    member private x.UniformLocationCache   : ResourceCache<UniformLocation, nativeint>     = uniformLocationCache
                                                                                    
    member private x.IsActiveCache          : ResourceCache<bool, int>                      = isActiveCache
    member private x.BeginModeCache         : ResourceCache<GLBeginMode, GLBeginMode>       = beginModeCache
    member private x.DrawCallInfoCache      : ResourceCache<DrawCallInfoList, DrawCallInfoList> = drawCallInfoCache
    member private x.DepthTestCache         : ResourceCache<DepthTestInfo, DepthTestInfo>   = depthTestCache
    member private x.CullModeCache          : ResourceCache<int, int>                       = cullModeCache
    member private x.PolygonModeCache       : ResourceCache<int, int>                       = polygonModeCache
    member private x.BlendModeCache         : ResourceCache<GLBlendMode, GLBlendMode>       = blendModeCache
    member private x.StencilModeCache       : ResourceCache<GLStencilMode, GLStencilMode>   = stencilModeCache
    member private x.FlagCache              : ResourceCache<bool, int>                      = flagCache
    member private x.TextureBindingCache    : ResourceCache<TextureBinding, TextureBinding> = textureBindingCache

    member x.RenderTaskLock = renderTaskInfo

    new(parent, lock, shareTextures, shareBuffers) = ResourceManager(Some parent, parent.Context, lock, shareTextures, shareBuffers)
    new(ctx, lock, shareTextures, shareBuffers) = ResourceManager(None, ctx, lock, shareTextures, shareBuffers)

    interface IResourceManager with
        member x.CreateSurface(signature, surf) =
            failwith "[GL] IResourceManager impl"
//            let res = x.CreateSurface(signature, surf)
//            new CastResource<_, _>(res) :> IResource<_>

        member x.CreateBuffer (data : IMod<IBuffer>) =
            let res = x.CreateBuffer(data)
            new CastResource<_, _>(res) :> IResource<_>

        member x.CreateTexture (data : IMod<ITexture>) =
            let res = x.CreateTexture(data)
            new CastResource<_, _>(res) :> IResource<_>



    member x.DrawBufferManager = drawBufferManager.Value
    member x.Context = ctx
        
    member x.CreateBuffer(data : IMod<IBuffer>) =
        match data with
            | :? IAdaptiveBuffer as data ->
                bufferCache.GetOrCreate(
                    [data :> obj],
                    fun () ->
                        let mutable r = Unchecked.defaultof<_>
                        { new Resource<Buffer, int>(ResourceKind.Buffer) with
                            
                            member x.View b =
                                b.Handle

                            member x.GetInfo b = 
                                b.SizeInBytes |> Mem |> ResourceInfo

                            member x.Create (token : AdaptiveToken, rt : RenderToken, old : Option<Buffer>) =
                                match old with
                                    | None ->
                                        r <- data.GetReader()
                                        let (nb, _) = r.GetDirtyRanges(token)
                                        ctx.CreateBuffer(nb)
                                    | Some old ->
                                        let (nb, ranges) = r.GetDirtyRanges(token)
                                        nb.Use (fun ptr ->
                                            ctx.UploadRanges(old, ptr, ranges)
                                        )
                                        old

                            member x.Destroy(b : Buffer) =
                                ctx.Delete b
                                r.Dispose()
                        }
                )

            | :? SingleValueBuffer as v ->
                bufferCache.GetOrCreate(Mod.constant 0, {
                    create = fun b      -> new Buffer(ctx, 0n, 0)
                    update = fun h b    -> h
                    delete = fun h      -> ()
                    info =   fun h      -> ResourceInfo.Zero
                    view =   fun h      -> h.Handle
                    kind = ResourceKind.Buffer
                })

            | _ ->
                bufferCache.GetOrCreate<IBuffer>(data, {
                    create = fun b      -> bufferManager.Create b
                    update = fun h b    -> bufferManager.Update(h, b)
                    delete = fun h      -> bufferManager.Delete h
                    info =   fun h      -> h.SizeInBytes |> Mem |> ResourceInfo
                    view =   fun h      -> h.Handle
                    kind = ResourceKind.Buffer
                })

    member x.CreateTexture(data : IMod<ITexture>) =
        textureCache.GetOrCreate<ITexture>(data, {
            create = fun b      -> textureManager.Create b
            update = fun h b    -> textureManager.Update(h, b)
            delete = fun h      -> textureManager.Delete h
            info =   fun h      -> h.SizeInBytes |> Mem |> ResourceInfo
            view =   fun r      -> V2i(r.Handle, Translations.toGLTarget r.Dimension r.IsArray r.Multisamples)
            kind = ResourceKind.Texture
        })

    member x.CreateIndirectBuffer(indexed : bool, data : IMod<IIndirectBuffer>) =
        indirectBufferCache.GetOrCreate<IIndirectBuffer>(data, [indexed :> obj], {
            create = fun b   -> ctx.CreateIndirect(indexed, b)
            update = fun h b -> ctx.UploadIndirect(h, indexed, b); h
            delete = fun h   -> ctx.Delete h
            info =   fun h   -> h.Buffer.SizeInBytes |> Mem |> ResourceInfo
            view =   fun h   -> V2i(h.Buffer.Handle, h.Count)
            kind = ResourceKind.IndirectBuffer
        })

    member x.GetSamplerStateDescription(samplerState : FShade.SamplerState) =
        samplerDescriptionCache.Invoke(samplerState)


    member x.CreateSurface(signature : IFramebufferSignature, surface : Aardvark.Base.Surface, topology : IndexedGeometryMode) =

        let (iface, result) = ctx.CreateProgram(signature, surface, topology)

        let programHandle = 
            programHandleCache.GetOrCreate<Program>(result, {
                create = fun b      -> b
                update = fun h b    -> b
                delete = fun h      -> ()
                info =   fun h      -> ResourceInfo.Zero
                view =   fun h      -> h.Handle
                kind = ResourceKind.ShaderProgram
            })

        iface, programHandle


    member x.CreateSampler (sam : IMod<SamplerStateDescription>) =
        samplerCache.GetOrCreate<SamplerStateDescription>(sam, {
            create = fun b      -> ctx.CreateSampler b
            update = fun h b    -> ctx.Update(h,b); h
            delete = fun h      -> ctx.Delete h
            info =   fun h      -> ResourceInfo.Zero
            view =   fun h      -> h.Handle
            kind = ResourceKind.SamplerState
        })

    member x.CreateTextureBinding(bindings : Map<int, IResource<Texture, V2i> * IResource<Sampler, int>>) =
        textureBindingCache.GetOrCreate(
            [bindings :> obj],
            fun () ->
                { new Resource<TextureBinding, TextureBinding>(ResourceKind.Unknown) with

                    member x.View a = a

                    member x.GetInfo _ = ResourceInfo.Zero

                    member x.Create (token : AdaptiveToken, rt : RenderToken, old : Option<TextureBinding>) =
                        if Map.isEmpty bindings then
                            {
                                offset = 0
                                count = 0
                                textures = NativePtr.zero
                                targets = NativePtr.zero
                                samplers = NativePtr.zero
                            }
                        else
                            let (lastId,_) = bindings |> Map.toSeq |> Seq.last
                            let slotCount = lastId + 1

                            let bindingHandle = 
                                match old with
                                    | Some o -> 
                                        o
                                    | _ ->

                                        bindings |> Map.iter (fun _ (t, s) ->
                                            t.AddRef()
                                            s.AddRef()
                                        )

                                        let offset = 0
                                        let count = slotCount
                                        let targets = NativePtr.alloc slotCount 
                                        let samplers = NativePtr.alloc slotCount 
                                        let textures = NativePtr.alloc slotCount 
                                        {
                                            offset = offset
                                            count = count
                                            targets = targets
                                            samplers = samplers
                                            textures = textures
                                        }

                            let bindings = bindings |> Map.map (fun _ (t, s) -> t.Update(token, rt); s.Update(token, rt); (NativePtr.read t.Pointer, NativePtr.read s.Pointer))

                            for i in 0 .. slotCount - 1 do
                                match Map.tryFind i bindings with
                                    | Some (t,s) ->
                                        NativePtr.set bindingHandle.textures i t.X
                                        NativePtr.set bindingHandle.targets i t.Y
                                        NativePtr.set bindingHandle.samplers i s
                                    | _ ->
                                        NativePtr.set bindingHandle.textures i 0
                                        NativePtr.set bindingHandle.targets i (int TextureTarget.Texture2D)
                                        NativePtr.set bindingHandle.samplers i 0
                                    

                            bindingHandle

                    member x.Destroy (b : TextureBinding) =
                        if not (Map.isEmpty bindings) then
                            NativePtr.free b.targets
                            NativePtr.free b.textures
                            NativePtr.free b.samplers

                            bindings |> Map.iter (fun _ (t, s) ->
                                t.RemoveRef()
                                s.RemoveRef()
                            )


                }
        )

    member x.CreateVertexInputBinding( bindings : list<int * BufferView * AttributeFrequency * IResource<Buffer, int>>, index : Option<OpenGl.Enums.IndexType * IResource<Buffer, int>>) =
        let createView (self : AdaptiveToken) (index : int, view : BufferView, frequency : AttributeFrequency, buffer : IResource<Buffer>) =
            match view.SingleValue with
                | Some value ->
                    index, {
                        Type = view.ElementType
                        Frequency = frequency
                        Normalized = false; 
                        Stride = view.Stride
                        Offset = view.Offset
                        Content = Right (value.GetValue self)
                    } 

                | _ ->
                    index, { 
                        Type = view.ElementType
                        Frequency = frequency
                        Normalized = false; 
                        Stride = view.Stride
                        Offset = view.Offset
                        Content = Left (buffer.Handle.GetValue self)
                    }

        vertexInputCache.GetOrCreate(
            [ bindings :> obj; index :> obj ],
            fun () ->
                { new Resource<VertexInputBindingHandle, int>(ResourceKind.VertexArrayObject) with

                    member x.View a = 0

                    member x.GetInfo _ = ResourceInfo.Zero

                    member x.Create (token : AdaptiveToken, rt : RenderToken, old : Option<VertexInputBindingHandle>) =
                        let attributes = bindings |> List.map (createView token)
                        let index = match index with | Some (_,i) -> i.Handle.GetValue token |> Some | _ -> None
                        match old with
                            | Some old ->
                                ctx.Update(old, index, attributes)
                                old
                            | None ->
                                ctx.CreateVertexInputBinding(index, attributes)
                        
                    member x.Destroy vao =
                        ctx.Delete vao
                }
        )

    member x.CreateUniformLocation(scope : Ag.Scope, u : IUniformProvider, uniform : ShaderParameter) =
        let name = ShaderPath.name uniform.Path
        let sem = Symbol.Create name
        match u.TryGetUniform (scope, sem) with
            | Some v ->
                uniformLocationCache.GetOrCreate(
                    [v :> obj],
                    fun () ->
                        let inputs = Map.ofList [sem, v :> IAdaptiveObject]
                        let writer = ShaderParameterWriter.adaptive v uniform.Type

                        { new Resource<UniformLocation, nativeint>(ResourceKind.UniformLocation) with
                            
                            member x.View h = h.Data

                            member x.GetInfo h =
                                h.Size |> Mem |> ResourceInfo

                            member x.Create(token, rt, old) =
                                let handle =
                                    match old with 
                                        | Some o -> o
                                        | None -> ctx.CreateUniformLocation(ShaderParameterType.sizeof uniform.Type, uniform.Type)
                                
                                writer.Write(token, handle.Data)
                                handle

                            member x.Destroy h =
                                ctx.Delete h
                        }        
                )
                

            | None ->
                failwithf "[GL] could not get uniform: %A" uniform
     
    member x.CreateUniformBuffer(scope : Ag.Scope, layout : FShade.GLSL.GLSLUniformBuffer, u : IUniformProvider) =
   
        uniformBufferManager.CreateUniformBuffer(layout, scope, u, SymDict.empty)
 
 
      
    member x.CreateIsActive(value : IMod<bool>) =
        isActiveCache.GetOrCreate(value, {
            create = fun b      -> b
            update = fun h b    -> b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view =   fun h      -> if h then 1 else 0
            kind = ResourceKind.Unknown
        })
      
    member x.CreateBeginMode(hasTess : IMod<bool>, drawMode : IndexedGeometryMode) =
        let mode = getTessDrawModeCache(drawMode).Invoke(hasTess)
        beginModeCache.GetOrCreate(mode, {
            create = fun b      -> b
            update = fun h b    -> b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateDrawCallInfoList(value : IMod<list<DrawCallInfo>>) =
        drawCallInfoCache.GetOrCreate(value, {
            create = fun b      -> ctx.CreateDrawCallInfoList(List.toArray b)
            update = fun h b    -> ctx.Update(h,List.toArray b)
            delete = fun h      -> ctx.Delete h
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateDepthTest(value : IMod<DepthTestMode>) =
        depthTestCache.GetOrCreate(value, {
            create = fun b      -> ctx.ToDepthTest b
            update = fun h b    -> ctx.ToDepthTest b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateCullMode(value : IMod<CullMode>) =
        cullModeCache.GetOrCreate(value, {
            create = fun b      -> ctx.ToCullMode b
            update = fun h b    -> ctx.ToCullMode b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreatePolygonMode(value : IMod<FillMode>) =
        polygonModeCache.GetOrCreate(value, {
            create = fun b      -> ctx.ToPolygonMode(b)
            update = fun h b    -> ctx.ToPolygonMode(b)
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view =  id
            kind = ResourceKind.Unknown
        })

    member x.CreateBlendMode(value : IMod<BlendMode>) =
        blendModeCache.GetOrCreate(value, {
            create = fun b      -> ctx.ToBlendMode b
            update = fun h b    -> ctx.ToBlendMode b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateStencilMode(value : IMod<StencilMode>) =
        stencilModeCache.GetOrCreate(value, {
            create = fun b      -> ctx.ToStencilMode b
            update = fun h b    -> ctx.ToStencilMode b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateFlag (value : IMod<bool>) =
        flagCache.GetOrCreate(value, {
            create = fun b      -> b
            update = fun h b    -> b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view =   fun v -> if v then 1 else 0
            kind = ResourceKind.Unknown
        })


    member x.Release() = 
        
        uniformBufferManager.Dispose()