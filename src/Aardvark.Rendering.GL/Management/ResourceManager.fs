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
open Aardvark.Rendering.GL
open OpenTK.Graphics.OpenGL4
open Aardvark.Base.ShaderReflection

module Sharing =
    
    type RefCountedBuffer(ctx, create : unit -> Buffer, destroy : unit -> unit) =
        inherit Buffer(ctx, 0n, 0)

        let mutable refCount = 0



        member x.Acquire() =
            if Interlocked.Increment &refCount = 1 then
                let b = using ctx.ResourceLock (fun _ -> create())
                x.Handle <- b.Handle
                x.SizeInBytes <- b.SizeInBytes

        member x.Release() =
            if Interlocked.Decrement &refCount = 0 then
                destroy()
                using ctx.ResourceLock (fun _ -> ctx.Delete x)
                x.Handle <- 0
                x.SizeInBytes <- 0n

    type RefCountedTexture(ctx, create : unit -> Texture, destroy : unit -> unit) =
        inherit Texture(ctx, 0, TextureDimension.Texture2D, 0, 0, V3i.Zero, None, TextureFormat.Rgba, 0L, true)

        let mutable refCount = 0

        member x.Acquire() =
            if Interlocked.Increment &refCount = 1 then
                let b = using ctx.ResourceLock (fun _ -> create())
                x.IsArray <- b.IsArray
                x.Handle <- b.Handle
                x.Dimension <- b.Dimension
                x.Multisamples <- b.Multisamples
                x.Size <- b.Size
                x.Count <- b.Count
                x.Format <- b.Format
                x.MipMapLevels <- b.MipMapLevels
                x.SizeInBytes <- b.SizeInBytes
                x.ImmutableFormat <- b.ImmutableFormat

        member x.Release() =
            if Interlocked.Decrement &refCount = 0 then
                destroy()
                using ctx.ResourceLock (fun _ -> ctx.Delete x)
                x.Handle <- 0


    type BufferManager(ctx : Context, active : bool) =
        let cache = ConcurrentDictionary<IBuffer, RefCountedBuffer>()

        let get (b : IBuffer) =
            cache.GetOrAdd(b, fun v -> 
                RefCountedBuffer(
                    ctx,
                    (fun () -> ctx.CreateBuffer b),
                    (fun () -> cache.TryRemove b |> ignore)
                )
            )

        member x.Create(data : IBuffer) =
            match data with
                | _ ->
                    if active then
                        let shared = get data
                        shared.Acquire()
                        shared :> Buffer
                    else
                        ctx.CreateBuffer data

        member x.Update(b : Buffer, data : IBuffer) : Buffer =
            match b with
                | :? RefCountedBuffer as b when active ->
                    
                    let newShared = get data
                    if newShared = b then
                        b :> Buffer
                    else
                        newShared.Acquire()
                        b.Release()
                        newShared :> Buffer
                | _ ->
                    if b.Handle = 0 then
                        x.Create(data)
                    else
                        ctx.Upload(b, data)
                        b

        member x.Delete(b : Buffer) =
            if b.Handle <> 0 then
                if active then
                    match b with
                        | :? RefCountedBuffer as b -> b.Release()
                        | _ -> ctx.Delete b
                else
                    ctx.Delete b

    type ArrayBufferManager(ctx : Context, active : bool) =
        let cache = ConcurrentDictionary<Array, RefCountedBuffer>()
        
        let nullBuffer = Buffer(ctx, 0n, 0)

        let get (b : Array) =
            cache.GetOrAdd(b, fun v -> 
                RefCountedBuffer(
                    ctx,
                    (fun () -> ctx.CreateBuffer b),
                    (fun () -> cache.TryRemove b |> ignore)
                )
            )

        member x.Create(data : Array) =
            if isNull data then
                nullBuffer
            else
                if active then
                    let shared = get data
                    shared.Acquire()
                    shared :> Buffer
                else
                    ctx.CreateBuffer data

        member x.Update(b : Buffer, data : Array) : Buffer =
            match b with
                | :? RefCountedBuffer as b when active ->
                    
                    let newShared = get data
                    if newShared = b then
                        b :> Buffer
                    else
                        newShared.Acquire()
                        b.Release()
                        newShared :> Buffer
                | _ ->
                    if b.Handle = 0 then
                        x.Create data
                    else
                        ctx.Upload(b, data)
                        b

        member x.Delete(b : Buffer) =
            if b.Handle <> 0 then
                if active then
                    match b with
                        | :? RefCountedBuffer as b -> b.Release()
                        | _ -> ctx.Delete b
                else
                    ctx.Delete b

    type TextureManager(ctx : Context, active : bool) =
        let cache = ConcurrentDictionary<ITexture, RefCountedTexture>()

        let nullTex = Texture(ctx, 0, TextureDimension.Texture2D, 1, 1, V3i.Zero, None, TextureFormat.Rgba, 0L, true)

        let get (b : ITexture) =
            cache.GetOrAdd(b, fun v -> 
                RefCountedTexture(
                    ctx,
                    (fun () -> ctx.CreateTexture b),
                    (fun () -> cache.TryRemove b |> ignore)
                )
            )

        member x.Create(data : ITexture) =
            match data with
                | :? NullTexture as t -> nullTex
                | _ ->
                    if active then
                        let shared = get data
                        shared.Acquire()
                        shared :> Texture
                    else
                        ctx.CreateTexture data

        member x.Update(b : Texture, data : ITexture) : Texture =
            match b with
                | :? RefCountedTexture as b when active ->
                    
                    let newShared = get data
                    if newShared = b then
                        b :> Texture
                    else
                        newShared.Acquire()
                        b.Release()
                        newShared :> Texture
                | _ ->
                    if b.Handle = 0 then
                        x.Create(data)
                    else
                        ctx.Upload(b, data)
                        b

        member x.Delete(b : Texture) =
            if b.Handle <> 0 then
                if active then
                    match b with
                        | :? RefCountedTexture as b -> b.Release()
                        | _ -> ctx.Delete b
                else
                    ctx.Delete b


        
type UniformBufferManager(ctx : Context, block : ShaderBlock) =
    let size = block.DataSize
    let alignedSize = (size + 255) &&& ~~~255

    let buffer = ctx.CreateSparseBuffer()
    let manager = MemoryManager.createNop()

    let viewCache = ResourceCache<UniformBufferView, int>(None, None)
    let rw = new ReaderWriterLockSlim()

    member x.CreateUniformBuffer(scope : Ag.Scope, u : IUniformProvider, additional : SymbolDict<IMod>) : IResource<UniformBufferView, int> =
        let values =
            block.Fields 
            |> List.map (fun f ->
                let name = ShaderPath.name f.Path
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

        viewCache.GetOrCreate(
            key,
            fun () ->
                let writers = List.map2 (fun f (_,v) -> nativeint f.Offset, ShaderParameterWriter.adaptive v f.Type) block.Fields values

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
                                    block <- manager.Alloc (nativeint alignedSize)
                                    store <- System.Runtime.InteropServices.Marshal.AllocHGlobal alignedSize
                                    buffer.Commitment(block.Offset, block.Size, true)
                                    UniformBufferView(buffer, block.Offset, nativeint block.Size)

                        for (offset,w) in writers do w.Write(token, store + offset)
                        buffer.WriteUnsafe(handle.Offset, handle.Size, store)
                        handle

                    member x.Destroy h =
                        if not block.Free then
                            System.Runtime.InteropServices.Marshal.FreeHGlobal store
                            use __ = ctx.ResourceLock
                            buffer.Commitment(block.Offset, block.Size, false)
                            manager.Free block

                }
        )

    member x.Dispose() =
        ctx.Delete buffer
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

[<AllowNullLiteral>]
type ResourceManager private (parent : Option<ResourceManager>, ctx : Context, renderTaskInfo : Option<IFramebufferSignature * RenderTaskLock>, shareTextures : bool, shareBuffers : bool) =
    
    let drawBufferManager = 
        match renderTaskInfo with
            | Some (signature, _) -> DrawBufferManager(signature) |> Some
            | _ -> None

    let derivedCache (f : ResourceManager -> ResourceCache<'a, 'b>) =
        ResourceCache<'a, 'b>(Option.map f parent, Option.map snd renderTaskInfo)

   
    let bufferManager           = Sharing.BufferManager(ctx, shareBuffers)
    let arrayBufferManager      = Sharing.ArrayBufferManager(ctx, shareBuffers)
    let textureManager          = Sharing.TextureManager(ctx, shareTextures)

    let arrayBufferCache        = derivedCache (fun m -> m.ArrayBufferCache)
    let bufferCache             = derivedCache (fun m -> m.BufferCache)
    let textureCache            = derivedCache (fun m -> m.TextureCache)
    let indirectBufferCache     = derivedCache (fun m -> m.IndirectBufferCache)
    let programCache            = derivedCache (fun m -> m.ProgramCache)
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

    let uniformBufferManagers = ConcurrentDictionary<ShaderBlock, UniformBufferManager>()

    member private x.ArrayBufferCache       : ResourceCache<Buffer, int>                    = arrayBufferCache
    member private x.BufferCache            : ResourceCache<Buffer, int>                    = bufferCache
    member private x.TextureCache           : ResourceCache<Texture, V2i>                   = textureCache
    member private x.IndirectBufferCache    : ResourceCache<IndirectBuffer, V2i>            = indirectBufferCache
    member private x.ProgramCache           : ResourceCache<Program, int>                   = programCache
    member private x.SamplerCache           : ResourceCache<Sampler, int>                   = samplerCache
    member private x.VertexInputCache       : ResourceCache<VertexInputBindingHandle, int>  = vertexInputCache
    member private x.UniformLocationCache   : ResourceCache<UniformLocation, nativeint>     = uniformLocationCache
    member private x.UniformBufferManagers                                                  = uniformBufferManagers
                                                                                    
    member private x.IsActiveCache          : ResourceCache<bool, int>         = isActiveCache
    member private x.BeginModeCache         : ResourceCache<GLBeginMode, GLBeginMode>        = beginModeCache
    member private x.DrawCallInfoCache      : ResourceCache<DrawCallInfoList, DrawCallInfoList> = drawCallInfoCache
    member private x.DepthTestCache         : ResourceCache<DepthTestInfo, DepthTestInfo>    = depthTestCache
    member private x.CullModeCache          : ResourceCache<int, int>         = cullModeCache
    member private x.PolygonModeCache       : ResourceCache<int, int>      = polygonModeCache
    member private x.BlendModeCache         : ResourceCache<GLBlendMode, GLBlendMode>        = blendModeCache
    member private x.StencilModeCache       : ResourceCache<GLStencilMode, GLStencilMode>      = stencilModeCache
    member private x.FlagCache              : ResourceCache<bool, int>      = flagCache

    member x.RenderTaskLock = renderTaskInfo

    new(parent, ctx, lock, shareTextures, shareBuffers) = ResourceManager(Some parent, ctx, lock, shareTextures, shareBuffers)
    new(ctx, lock, shareTextures, shareBuffers) = ResourceManager(None, ctx, lock, shareTextures, shareBuffers)

    interface IResourceManager with
        member x.CreateSurface(signature, surf) =
            let res = x.CreateSurface(signature, surf)
            new CastResource<_, _>(res) :> IResource<_>

        member x.CreateBuffer (data : IMod<IBuffer>) =
            let res = x.CreateBuffer(data)
            new CastResource<_, _>(res) :> IResource<_>

        member x.CreateTexture (data : IMod<ITexture>) =
            let res = x.CreateTexture(data)
            new CastResource<_, _>(res) :> IResource<_>



    member x.DrawBufferManager = drawBufferManager.Value
    member x.Context = ctx

    member x.CreateBuffer(data : IMod<Array>) =
        bufferCache.GetOrCreate<Array>(data, {
            create = fun b      -> arrayBufferManager.Create b
            update = fun h b    -> arrayBufferManager.Update(h, b)
            delete = fun h      -> arrayBufferManager.Delete h
            info =   fun h      -> h.SizeInBytes |> Mem |> ResourceInfo
            view =   fun h      -> h.Handle
            kind = ResourceKind.Buffer
        })

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
                    create = fun b      -> Buffer(ctx, 0n, 0)
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

    member x.CreateSurface(signature : IFramebufferSignature, surface : IMod<ISurface>) =
        let create (s : ISurface) =
            match SurfaceCompilers.compile ctx signature s with
                | Success program -> program
                | Error e -> 
                    Log.error "[GL] surface compilation failed: %s" e
                    failwithf "[GL] surface compilation failed: %s" e

        programCache.GetOrCreate<ISurface>(surface, [signature :> obj], {
            create = fun b      -> create b
            update = fun h b    -> ctx.Delete(h); create b
            delete = fun h      -> ctx.Delete h
            info =   fun h      -> ResourceInfo.Zero
            view =   fun h      -> h.Handle
            kind = ResourceKind.ShaderProgram
        })

    member x.CreateSampler (sam : IMod<SamplerStateDescription>) =
        samplerCache.GetOrCreate<SamplerStateDescription>(sam, {
            create = fun b      -> ctx.CreateSampler b
            update = fun h b    -> ctx.Update(h,b); h
            delete = fun h      -> ctx.Delete h
            info =   fun h      -> ResourceInfo.Zero
            view =   fun h      -> h.Handle
            kind = ResourceKind.SamplerState
        })

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
     
    member x.CreateUniformBuffer(scope : Ag.Scope, layout : ShaderBlock, program : Program, u : IUniformProvider) =
        let manager = 
            uniformBufferManagers.GetOrAdd(
                (layout), 
                fun block -> 
                    new UniformBufferManager(ctx,block)
                    //new PersistentlyMappedUniformManager(ctx, s, uniformFields)
            )

        manager.CreateUniformBuffer(scope, u, program.UniformGetters)
 

 
      
    member x.CreateIsActive(value : IMod<bool>) =
        isActiveCache.GetOrCreate(value, {
            create = fun b      -> b
            update = fun h b    -> b
            delete = fun h      -> ()
            info =   fun h      -> ResourceInfo.Zero
            view =   fun h      -> if h then 1 else 0
            kind = ResourceKind.Unknown
        })
      
    member x.CreateBeginMode(hasTess : bool, value : IMod<IndexedGeometryMode>) =
        beginModeCache.GetOrCreate(value, {
            create = fun b      -> ctx.ToBeginMode(b, hasTess)
            update = fun h b    -> ctx.ToBeginMode(b, hasTess)
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
