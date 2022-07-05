namespace Aardvark.Rendering.GL

#nowarn "9"
#nowarn "51"

open System
open System.Threading
open System.Collections.Concurrent
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

open Aardvark.Rendering
open FSharp.Data.Adaptive
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.ShaderReflection
open Aardvark.Rendering.GL


        
type UniformBufferManager(ctx : Context) =

    let bufferMemory : Management.Memory<Buffer> =

        let alloc (size : nativeint) =
            if size = 0n then
                new Buffer(ctx, 0n, 0)
            else
                use __ = ctx.ResourceLock
                let handle = GL.Dispatch.CreateBuffer()
                GL.Check "failed to create uniform buffer"

                BufferMemoryUsage.addUniformBuffer ctx (int64 size)

                GL.Dispatch.NamedBufferStorage(handle, size, 0n, BufferStorageFlags.DynamicStorageBit)
                GL.Check "could not allocate uniform buffer"

                new Buffer(ctx, size, handle)

        let free (buffer : Buffer) (size : nativeint) =
            if buffer.Handle <> 0 then
                GL.DeleteBuffer(buffer.Handle)
                BufferMemoryUsage.removeUniformBuffer ctx (int64 size)
                GL.Check "could not free uniform buffer"

        {
            malloc = alloc
            mfree = free
            mcopy = fun _ _ _ _ -> failwith "not implemented"
            mrealloc = fun _ _ _ -> failwith "not implemented"
        }


    let manager = new Management.ChunkedMemoryManager<_>(bufferMemory, 1n <<< 20)

    //let buffer = 
    //    // TODO: better implementation for uniform buffers (see https://github.com/aardvark-platform/aardvark.rendering/issues/32)
    //    use __ = ctx.ResourceLock
    //    let handle = GL.GenBuffer()
    //    GL.Check "could not create buffer"
    //    new FakeSparseBuffer(ctx, handle, id, id) :> SparseBuffer

    //let manager = MemoryManager.createNop()

    let viewCache = ResourceCache<UniformBufferView, int>(None, None)
    let rw = new ReaderWriterLockSlim()

    member x.CreateUniformBuffer(block : FShade.GLSL.GLSLUniformBuffer, scope : Ag.Scope, u : IUniformProvider, additional : SymbolDict<IAdaptiveValue>) : IResource<UniformBufferView, int> =
        let values =
            block.ubFields 
            |> List.map (fun f ->
                let name = f.ufName

                let value = match Uniforms.tryGetDerivedUniform name u with
                            | Some v -> v
                            | None -> 
                                
                                let sem = Symbol.Create name
                                match u.TryGetUniform(scope, sem) with
                                | Some v -> v
                                | None -> 
                                    match additional.TryGetValue sem with
                                    | (true, m) -> m
                                    | _ -> failwithf "[GL] could not get uniform: %A" f

                if Object.ReferenceEquals(value, null) then
                    failwithf "[GL] uniform of %A is null" f

                value
            )

        let key = values |> List.map (fun v -> v :> obj)

        let alignedSize = (block.ubSize + 255) &&& ~~~255 // needs to be multiple of GL_UNIFORM_BUFFER_OFFSET_ALIGNMENT (currently 256)

        viewCache.GetOrCreate(
            key,
            fun () ->
                let writers = List.map2 (fun (f : FShade.GLSL.GLSLUniformBufferField) v -> nativeint f.ufOffset, ShaderParameterWriter.adaptive v (ShaderParameterType.ofGLSLType f.ufType)) block.ubFields values

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

                                    // record BufferView statistic: use block.Size instead of alignedSize -> allows to see overhead due to chunked buffers and alignment
                                    BufferMemoryUsage.addUniformBufferView ctx (int64 block.Size) 

                                    UniformBufferView(block.Memory.Value, block.Offset, nativeint block.Size)

                        for (offset,w) in writers do w.Write(token, store + offset)

                        GL.Dispatch.NamedBufferSubData(handle.Buffer.Handle, handle.Offset, handle.Size, store)
                        GL.Check "could not upload uniform buffer"
                        //buffer.WriteUnsafe(handle.Offset, handle.Size, store)
                        handle

                    member x.Destroy h =
                        if not block.IsFree then
                            System.Runtime.InteropServices.Marshal.FreeHGlobal store
                            store <- 0n
                            BufferMemoryUsage.removeUniformBufferView ctx (int64 block.Size)
                            use __ = ctx.ResourceLock
                            manager.Free block

                }
        )

    member x.Dispose() =
        manager.Dispose()


type CastResource<'a, 'b when 'a : equality and 'b : equality>(inner : IResource<'a>) =
    inherit AdaptiveObject()
    static let th = typeof<'b>
    let handle = inner.Handle |> AVal.map unbox

    member x.Inner = inner

    override x.GetHashCode() = inner.GetHashCode()
    override x.Equals o = 
        match o with
            | :? CastResource<'a,'b> as o -> inner.Equals o.Inner
            | _ -> false

    member x.Update(token : AdaptiveToken, rt : RenderToken) =
        x.EvaluateIfNeeded token () (fun token ->
            inner.Update(token, rt)
        )

    interface IResource with  
        member x.Id = inner.Id
        member x.HandleType = th
        member x.Dispose() = inner.Dispose()
        member x.AddRef() = inner.AddRef()
        member x.RemoveRef() = inner.RemoveRef()
        member x.Update(caller, token) = x.Update(caller, token)
        member x.Info = inner.Info
        member x.IsDisposed = inner.IsDisposed
        member x.Kind = inner.Kind

    interface IResource<'b> with
        member x.Handle = handle

[<Struct>]
type private AttachmentConfig<'a> =
    {
        signature    : IFramebufferSignature
        defaultValue : aval<'a>
        attachments  : aval<Map<Symbol, 'a>>
    }

[<Struct>]
type TextureBinding =
    {
        offset : int
        count : int
        targets : nativeptr<int>
        textures : nativeptr<int>
        samplers : nativeptr<int>
    }

type InterfaceSlots = 
    {
        // interface definition sorted by slot
        samplers : (string * FShade.GLSL.GLSLSampler)[]
        uniformBuffers : (string * FShade.GLSL.GLSLUniformBuffer)[]
        storageBuffers : (string * FShade.GLSL.GLSLStorageBuffer)[]
    }

[<AllowNullLiteral>]
type ResourceManager private (parent : Option<ResourceManager>, ctx : Context, renderTaskInfo : Option<IFramebufferSignature * RenderTaskLock>, shareTextures : bool, shareBuffers : bool) =

    let derivedCache (f : ResourceManager -> ResourceCache<'a, 'b>) =
        ResourceCache<'a, 'b>(Option.map f parent, Option.map snd renderTaskInfo)
    //let derivedCache (f : ResourceManager -> ResourceCache<'a, 'b>) =
    //    match parent with
    //    | Some p -> f p
    //    | None -> ResourceCache<'a, 'b>(None, None)

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
    let depthBiasCache          = derivedCache (fun m -> m.DepthBiasCache)
    let cullModeCache           = derivedCache (fun m -> m.CullModeCache)
    let frontFaceCache          = derivedCache (fun m -> m.FrontFaceCache)
    let polygonModeCache        = derivedCache (fun m -> m.PolygonModeCache)
    let blendModeCache          = derivedCache (fun m -> m.BlendModeCache)
    let colorMaskCache          = derivedCache (fun m -> m.ColorMaskCache)
    let stencilModeCache        = derivedCache (fun m -> m.StencilModeCache)
    let stencilMaskCache        = derivedCache (fun m -> m.StencilMaskCache)
    let flagCache               = derivedCache (fun m -> m.FlagCache)
    let colorCache              = derivedCache (fun m -> m.ColorCache)
    
    let textureBindingCache     = derivedCache (fun m -> m.TextureBindingCache)

    let uniformBufferManager = UniformBufferManager ctx

    let blendModeConfigCache =
        ConcurrentDictionary<AttachmentConfig<BlendMode>, aval<GLBlendMode[]>>()

    let colorMaskConfigCache =
        ConcurrentDictionary<AttachmentConfig<ColorMask>, aval<GLColorMask[]>>()

    let hasTessDrawModeCache = 
        ConcurrentDictionary<IndexedGeometryMode, UnaryCache<aval<Program>, aval<GLBeginMode>>>()
        
    let getTessDrawModeCache (mode : IndexedGeometryMode) =
        hasTessDrawModeCache.GetOrAdd(mode, fun mode ->
            UnaryCache(AVal.map (fun t -> ctx.ToBeginMode(mode, t.HasTessellation)))
        )

    let ifaceSlotCache = ConcurrentDictionary<FShade.GLSL.GLSLProgramInterface, InterfaceSlots>()

    let textureArrayCache = UnaryCache<aval<ITexture[]>, ConcurrentDictionary<int, List<IResource<Texture,V2i>>>>(fun ta -> ConcurrentDictionary<int, List<IResource<Texture,V2i>>>())

    let staticSamplerStateCache = ConcurrentDictionary<FShade.SamplerState, aval<SamplerState>>()
    let dynamicSamplerStateCache = ConcurrentDictionary<Symbol * SamplerState, UnaryCache<aval<(Symbol -> SamplerState -> SamplerState)>, aval<SamplerState>>>()
    let samplerDescriptionCache = ConcurrentDictionary<FShade.SamplerState, SamplerState>() 
    
    member private x.BufferManager = bufferManager
    member private x.TextureManager = textureManager

    member private x.BufferCache              : ResourceCache<Buffer, int>                    = bufferCache
    member private x.TextureCache             : ResourceCache<Texture, V2i>                   = textureCache
    member private x.IndirectBufferCache      : ResourceCache<GLIndirectBuffer, IndirectDrawArgs> = indirectBufferCache
    member private x.SamplerCache             : ResourceCache<Sampler, int>                   = samplerCache
    member private x.VertexInputCache         : ResourceCache<VertexInputBindingHandle, int>  = vertexInputCache
    member private x.UniformLocationCache     : ResourceCache<UniformLocation, nativeint>     = uniformLocationCache
                                                                                      
    member private x.IsActiveCache            : ResourceCache<bool, int>                      = isActiveCache
    member private x.BeginModeCache           : ResourceCache<GLBeginMode, GLBeginMode>       = beginModeCache
    member private x.DrawCallInfoCache        : ResourceCache<DrawCallInfoList, DrawCallInfoList> = drawCallInfoCache
    member private x.DepthTestCache           : ResourceCache<int, int>                       = depthTestCache
    member private x.DepthBiasCache           : ResourceCache<DepthBiasInfo, DepthBiasInfo>   = depthBiasCache
    member private x.CullModeCache            : ResourceCache<int, int>                       = cullModeCache
    member private x.FrontFaceCache           : ResourceCache<int, int>                       = frontFaceCache
    member private x.PolygonModeCache         : ResourceCache<int, int>                       = polygonModeCache
    member private x.BlendModeCache           : ResourceCache<nativeptr<GLBlendMode>, nativeint> = blendModeCache
    member private x.ColorMaskCache           : ResourceCache<nativeptr<GLColorMask>, nativeint> = colorMaskCache
    member private x.StencilModeCache         : ResourceCache<GLStencilMode, GLStencilMode>   = stencilModeCache
    member private x.StencilMaskCache         : ResourceCache<uint32, uint32>                 = stencilMaskCache
    member private x.FlagCache                : ResourceCache<bool, int>                      = flagCache
    member private x.ColorCache               : ResourceCache<C4f, C4f>                       = colorCache
    member private x.TextureBindingCache      : ResourceCache<TextureBinding, TextureBinding> = textureBindingCache

    member x.RenderTaskLock = renderTaskInfo

    new(parent, lock, shareTextures, shareBuffers) = ResourceManager(Some parent, parent.Context, lock, shareTextures, shareBuffers)
    new(ctx, lock, shareTextures, shareBuffers) = ResourceManager(None, ctx, lock, shareTextures, shareBuffers)

    interface IResourceManager with
        member x.CreateSurface(signature, surf) =
            failwith "[GL] IResourceManager impl"
//            let res = x.CreateSurface(signature, surf)
//            new CastResource<_, _>(res) :> IResource<_>

        member x.CreateBuffer (data : aval<IBuffer>) =
            let res = x.CreateBuffer(data)
            new CastResource<_, _>(res) :> IResource<_>

        member x.CreateTexture (data : aval<ITexture>) =
            let res = x.CreateTexture(data)
            new CastResource<_, _>(res) :> IResource<_>



    member x.Context = ctx
        
    member x.CreateBuffer(data : aval<IBuffer>) =
        match data with
        | :? SingleValueBuffer as v ->
            bufferCache.GetOrCreate(AVal.constant 0, fun () -> {
                create = fun b      -> new Buffer(ctx, 0n, 0)
                update = fun h b    -> h
                delete = fun h      -> ()
                info =   fun h      -> ResourceInfo.Zero
                view =   fun h      -> h.Handle
                kind = ResourceKind.Buffer
            })

        | _ ->
            bufferCache.GetOrCreate<IBuffer>(data, fun () -> {
                create = fun b      -> bufferManager.Create b
                update = fun h b    -> bufferManager.Update(h, b)
                delete = fun h      -> bufferManager.Delete h
                info =   fun h      -> h.SizeInBytes |> Mem |> ResourceInfo
                view =   fun h      -> h.Handle
                kind = ResourceKind.Buffer
            })

    member x.CreateTexture(data : aval<ITexture>) : IResource<Texture, V2i> =
        textureCache.GetOrCreate<ITexture>(data, fun () -> {
            create = fun b      -> textureManager.Create b
            update = fun h b    -> textureManager.Update(h, b)
            delete = fun h      -> textureManager.Delete h
            info =   fun h      -> h.SizeInBytes |> Mem |> ResourceInfo
            view =   fun r      -> V2i(r.Handle, Translations.toGLTarget r.Dimension r.IsArray r.Multisamples)
            kind = ResourceKind.Texture
        })

    member x.CreateTexture'(data : aval<IBackendTexture>) : IResource<Texture, V2i> =
        textureCache.GetOrCreate<IBackendTexture>(data, fun () -> {
            create = fun b      -> textureManager.Create b
            update = fun h b    -> textureManager.Update(h, b)
            delete = fun h      -> textureManager.Delete h
            info =   fun h      -> h.SizeInBytes |> Mem |> ResourceInfo
            view =   fun r      -> V2i(r.Handle, Translations.toGLTarget r.Dimension r.IsArray r.Multisamples)
            kind = ResourceKind.Texture
        })

    member x.CreateIndirectBuffer(indexed : bool, data : aval<IndirectBuffer>) =
        
        // TODO: proper cache for transformIndirectData -> if transform necessary duplicates are uploaded at the moment

        let transformIndirectData (buffer : IBuffer) =
            match buffer with
            | :? ArrayBuffer as ab ->
                match ab.Data with
                | :? (DrawCallInfo[]) as data -> 
                    let transformed = data |> Array.map (fun dc -> 
                                DrawCallInfo(
                                    FaceVertexCount = dc.FaceVertexCount,
                                    InstanceCount = dc.InstanceCount,
                                    FirstIndex = dc.FirstIndex,
                                    BaseVertex = dc.FirstInstance,
                                    FirstInstance = dc.BaseVertex
                                ))
                    ArrayBuffer(transformed) :> IBuffer
                | _ -> failwith "[GL] IndirectBuffer data supposed to be DrawCallInfo[]"
            | :? SingleValueBuffer as sb -> failwith "TODO"
            | _ -> 
                buffer

        indirectBufferCache.GetOrCreate<IndirectBuffer>(data, [indexed :> obj], fun () -> {
            create = fun b ->
                let (buffer, own) =
                    match b.Buffer with
                    | :? Buffer ->
                        if b.Indexed <> indexed then
                            if b.Indexed then failwithf "[GL] Expected non-indexed data but indirect buffer contains indexed data."
                            else failwithf "[GL] Expected indexed data but indirect buffer contains non-indexed data."

                        b.Buffer :?> Buffer, false

                    | _ ->
                        let layoutedData = if indexed then transformIndirectData b.Buffer else b.Buffer
                        bufferManager.Create(layoutedData), true

                GLIndirectBuffer(buffer, b.Count, b.Stride, indexed, own)

            update = fun h b ->
                if h.Indexed <> b.Indexed then
                    failwith "[GL] cannot change Indexed option of IndirectBuffer"

                let buffer =
                    match b.Buffer with
                    | :? Buffer ->
                        if h.OwnResource then
                            failwith "[GL] cannot change IndirectBuffer type"
                        b.Buffer :?> Buffer

                    | _ ->
                        if not h.OwnResource then
                            failwith "[GL] cannot change IndirectBuffer type"
                        let layoutedData = if indexed then transformIndirectData b.Buffer else b.Buffer
                        bufferManager.Update(h.Buffer, layoutedData)

                if h.Buffer = buffer && h.Count = b.Count && h.Stride = b.Stride then // OwnResource and Indexed cannot change
                    h // return old to remain reference equal -> will be counted as InPlaceUpdate (no real performance difference)
                else
                    GLIndirectBuffer(buffer, b.Count, b.Stride, b.Indexed, h.OwnResource)

            delete = fun h   -> if h.OwnResource then bufferManager.Delete(h.Buffer)
            info =   fun h   -> h.Buffer.SizeInBytes |> Mem |> ResourceInfo
            view =   fun h   -> IndirectDrawArgs(h.Buffer.Handle, h.Count, h.Stride)
            kind = ResourceKind.IndirectBuffer
        })

    member x.GetSamplerStateDescription(samplerState : FShade.SamplerState) =
        samplerDescriptionCache.GetOrAdd(samplerState, fun sam -> sam.SamplerState)

    member x.GetDynamicSamplerState(texName : Symbol, samplerState : SamplerState, modifier : aval<(Symbol -> SamplerState -> SamplerState)>) : aval<SamplerState> =
        dynamicSamplerStateCache.GetOrAdd((texName, samplerState), fun (sym, sam) ->
            UnaryCache(fun modi -> modi |> AVal.map (fun f -> f sym sam))
        ).Invoke(modifier)

    member x.GetStaticSamplerState(samplerState : FShade.SamplerState) =
        staticSamplerStateCache.GetOrAdd(samplerState, fun sam -> AVal.constant (sam.SamplerState))

    member x.GetInterfaceSlots(iface : FShade.GLSL.GLSLProgramInterface) = 
        ifaceSlotCache.GetOrAdd(iface, (fun iface ->
                { samplers = iface.samplers |> MapExt.toSeq |> Seq.sortBy (fun (_, sam) -> sam.samplerBinding) |> Seq.toArray
                  uniformBuffers = iface.uniformBuffers |> MapExt.toSeq |> Seq.sortBy (fun (_, ub) -> ub.ubBinding) |> Seq.toArray
                  storageBuffers = iface.storageBuffers |> MapExt.toSeq |> Seq.sortBy (fun (_, sb) -> sb.ssbBinding) |> Seq.toArray }
            ))

    member x.CreateSurface(signature : IFramebufferSignature, surface : Aardvark.Rendering.Surface, topology : IndexedGeometryMode) =

        let (iface, result) = ctx.CreateProgram(signature, surface, topology)

        let programHandle = 
            programHandleCache.GetOrCreate<Program>(result, fun () -> {
                create = fun b      -> b
                update = fun h b    -> b
                delete = fun h      -> ()
                info =   fun h      -> ResourceInfo.Zero
                view =   fun h      -> h.Handle
                kind = ResourceKind.ShaderProgram
            })

        iface, programHandle

    member x.CreateTextureArray (slotCount : int, texArr : aval<ITexture[]>) : List<IResource<Texture, V2i>> =
        
        let slotCountCache = textureArrayCache.Invoke(texArr)
        slotCountCache.GetOrAdd(slotCount, fun slotCount -> 
                List.init slotCount (fun i ->
                        x.CreateTexture(texArr |> AVal.map (fun (t : ITexture[]) -> if i < t.Length then t.[i] else NullTexture() :> _)))
            )

    member x.CreateSampler (sam : aval<SamplerState>) =
        samplerCache.GetOrCreate<SamplerState>(sam, fun () -> {
            create = fun b      -> ctx.CreateSampler b
            update = fun h b    -> ctx.Update(h,b); h
            delete = fun h      -> ctx.Delete h
            info =   fun h      -> ResourceInfo.Zero
            view =   fun h      -> h.Handle
            kind = ResourceKind.SamplerState
        })

    member x.CreateTextureBinding(bindings : Range1i * List<Option<IResource<Texture, V2i> * IResource<Sampler, int>>>) =
        textureBindingCache.GetOrCreate(
            [bindings :> obj],
            fun () ->
                { new Resource<TextureBinding, TextureBinding>(ResourceKind.Unknown) with

                    member x.View a = a

                    member x.GetInfo _ = ResourceInfo.Zero

                    member x.Create (token : AdaptiveToken, rt : RenderToken, old : Option<TextureBinding>) =

                        let (slots, bindings) = bindings

                        let slotCount = slots.Size + 1
                        if slotCount <= 0 then
                            failwith "invalid slot range"
                            
                        let bindingHandle = 
                            match old with
                                | Some o -> 
                                    o
                                | _ ->

                                    bindings |> List.iter (fun b ->
                                        match b with 
                                        | Some (t, s) ->
                                            t.AddRef()
                                            s.AddRef()
                                        | None -> ()
                                    )

                                    let offset = slots.Min
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
                          
                        let mutable slotTex = bindings
                        for i in 0..slotCount-1 do
                            if slotTex.IsEmpty then
                                // invalid texture binding count
                                ()
                            else
                                match slotTex.Head with 
                                | Some (t, s) ->
                                    t.Update(token, rt) 
                                    s.Update(token, rt)

                                    let tt = NativePtr.read t.Pointer
                                    let ss = NativePtr.read s.Pointer

                                    NativePtr.set bindingHandle.textures i (tt.X)
                                    NativePtr.set bindingHandle.targets i (tt.Y)
                                    NativePtr.set bindingHandle.samplers i ss
                                    
                                | None -> 
                                    // write 0 texture handle to slot
                                    NativePtr.set bindingHandle.textures i 0
                                    NativePtr.set bindingHandle.targets i (int TextureTarget.Texture2D)
                                    NativePtr.set bindingHandle.samplers i 0        

                                slotTex <- slotTex.Tail

                        bindingHandle

                    member x.Destroy (b : TextureBinding) =

                        let (slots, bindings) = bindings
                        if slots.Size >= 0 then
                            
                            NativePtr.free b.targets
                            NativePtr.free b.textures
                            NativePtr.free b.samplers

                            bindings |> List.iter (fun b ->
                                match b with 
                                | Some (t, s) ->
                                    t.RemoveRef()
                                    s.RemoveRef()
                                | None -> ()
                            )

                }
        )

    member x.CreateTextureBinding'(bindings : Range1i * List<IResource<Texture, V2i>> * IResource<Sampler, int>) =
        textureBindingCache.GetOrCreate(
            [bindings :> obj],
            fun () ->
                { new Resource<TextureBinding, TextureBinding>(ResourceKind.Unknown) with

                    member x.View a = a

                    member x.GetInfo _ = ResourceInfo.Zero

                    member x.Create (token : AdaptiveToken, rt : RenderToken, old : Option<TextureBinding>) =

                        let (slots, texArr, sam) = bindings

                        let slotCount = slots.Size + 1
                        if slotCount <= 0 then
                            failwith "invalid slot range"
                            
                        let bindingHandle = 
                            match old with
                                | Some o -> 
                                    o
                                | _ ->

                                    sam.AddRef()
                                    texArr |> List.iter (fun t -> t.AddRef())

                                    let offset = slots.Min
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
                          
                        let mutable slotTex = texArr

                        sam.Update(token, rt)
                        let ss = NativePtr.read sam.Pointer

                        for i in 0..slotCount-1 do
                            if slotTex.IsEmpty then
                                // write 0 texture handle to slot
                                NativePtr.set bindingHandle.textures i 0
                                NativePtr.set bindingHandle.targets i (int TextureTarget.Texture2D)
                                NativePtr.set bindingHandle.samplers i 0        
                                // invalid texture binding count
                                ()
                            else
                                let t = slotTex.Head
                                t.Update(token, rt) 
                                    
                                let tt = NativePtr.read t.Pointer

                                NativePtr.set bindingHandle.textures i (tt.X)
                                NativePtr.set bindingHandle.targets i (tt.Y)
                                NativePtr.set bindingHandle.samplers i ss

                                slotTex <- slotTex.Tail

                        bindingHandle

                    member x.Destroy (b : TextureBinding) =

                        let (slots, texArr, sam) = bindings
                        if slots.Size >= 0 then
                            
                            NativePtr.free b.targets
                            NativePtr.free b.textures
                            NativePtr.free b.samplers

                            sam.RemoveRef()

                            texArr |> List.iter (fun t -> t.RemoveRef())

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
 
 
      
    member x.CreateIsActive(value : aval<bool>) =
        isActiveCache.GetOrCreate(value, fun () -> {
            create = fun b      -> b
            update = fun h b    -> b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view =   fun h      -> if h then 1 else 0
            kind = ResourceKind.Unknown
        })
      
    member x.CreateBeginMode(prog : aval<Program>, drawMode : IndexedGeometryMode) =
        let mode = getTessDrawModeCache(drawMode).Invoke(prog)
        beginModeCache.GetOrCreate(mode, fun () -> {
            create = fun b      -> b
            update = fun h b    -> b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateDrawCallInfoList(value : aval<list<DrawCallInfo>>) =
        drawCallInfoCache.GetOrCreate(value, fun () -> {
            create = fun b      -> ctx.CreateDrawCallInfoList(List.toArray b)
            update = fun h b    -> ctx.Update(h,List.toArray b)
            delete = fun h      -> ctx.Delete h
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateDepthTest(value : aval<DepthTest>) =
        depthTestCache.GetOrCreate(value, fun () -> {
            create = fun b      -> ctx.ToDepthTest b
            update = fun h b    -> ctx.ToDepthTest b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateDepthBias(value : aval<DepthBias>) =
        depthBiasCache.GetOrCreate(value, fun () -> {
            create = fun b      -> ctx.ToDepthBias b
            update = fun h b    -> ctx.ToDepthBias b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateCullMode(value : aval<CullMode>) =
        cullModeCache.GetOrCreate(value, fun () -> {
            create = fun b      -> ctx.ToCullMode b
            update = fun h b    -> ctx.ToCullMode b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateFrontFace(mode : aval<WindingOrder>) =
        frontFaceCache.GetOrCreate(mode, fun () -> {
            create = fun b      -> ctx.ToFrontFace b
            update = fun h b    -> ctx.ToFrontFace b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreatePolygonMode(value : aval<FillMode>) =
        polygonModeCache.GetOrCreate(value, fun () -> {
            create = fun b      -> ctx.ToPolygonMode(b)
            update = fun h b    -> ctx.ToPolygonMode(b)
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view =  id
            kind = ResourceKind.Unknown
        })

    member private x.CreatePerAttachmentValues<'T, 'U>(cache : ConcurrentDictionary<AttachmentConfig<'T>, aval<'U[]>>, mapping : 'T -> 'U,
                                                       signature : IFramebufferSignature, def : aval<'T>, attachments : aval<Map<Symbol, 'T>>) =
        let config = { signature = signature; defaultValue = def; attachments = attachments }

        cache.GetOrAdd(config, fun cfg ->
            let attachments = cfg.signature.ColorAttachments
            let slots = cfg.signature.ColorAttachmentSlots

            (cfg.defaultValue, cfg.attachments) ||> AVal.map2 (fun def modes ->
                Array.init slots (fun i ->
                    attachments
                    |> Map.tryFind i
                    |> Option.bind (fun att -> modes |> Map.tryFind att.Name)
                    |> Option.defaultValue def
                    |> mapping          
                )
            )
        )

    member x.CreateBlendModes(signature : IFramebufferSignature, mode : aval<BlendMode>, attachments : aval<Map<Symbol, BlendMode>>) =
        let value =
            x.CreatePerAttachmentValues(blendModeConfigCache, ctx.ToBlendMode, signature, mode, attachments)

        blendModeCache.GetOrCreate(value, fun () -> {
            create = fun b      -> NativePtr.allocArray b
            update = fun h b    -> h |> NativePtr.setArray b; h
            delete = fun h      -> NativePtr.free h
            info =   fun h      -> ResourceInfo.Zero
            view = NativePtr.toNativeInt
            kind = ResourceKind.Unknown
        })

    member x.CreateColorMasks(signature : IFramebufferSignature, mask : aval<ColorMask>, attachments : aval<Map<Symbol, ColorMask>>) =
        let value =
            x.CreatePerAttachmentValues(colorMaskConfigCache, ctx.ToColorMask, signature, mask, attachments)

        colorMaskCache.GetOrCreate(value, fun () -> {
            create = fun b      -> NativePtr.allocArray b
            update = fun h b    -> h |> NativePtr.setArray b; h
            delete = fun h      -> NativePtr.free h
            info =   fun h      -> ResourceInfo.Zero
            view = NativePtr.toNativeInt
            kind = ResourceKind.Unknown
        })

    member x.CreateStencilMode(value : aval<StencilMode>) =
        stencilModeCache.GetOrCreate(value, fun () -> {
            create = fun b      -> ctx.ToStencilMode b
            update = fun h b    -> ctx.ToStencilMode b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateStencilMask(value : aval<StencilMask>) =
        stencilMaskCache.GetOrCreate(value, fun () -> {
            create = fun b      -> uint32 b
            update = fun h b    -> uint32 b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateFlag (value : aval<bool>) =
        flagCache.GetOrCreate(value, fun () -> {
            create = fun b      -> b
            update = fun h b    -> b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view =   fun v -> if v then 1 else 0
            kind = ResourceKind.Unknown
        })

    member x.CreateColor (value : aval<C4f>) =
        colorCache.GetOrCreate(value, fun () -> {
            create = fun b      -> b
            update = fun h b    -> b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view =   id
            kind = ResourceKind.Unknown
        })


    member x.Release() = 
        
        uniformBufferManager.Dispose()