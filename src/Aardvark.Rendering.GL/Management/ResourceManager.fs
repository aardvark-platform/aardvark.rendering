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
open Aardvark.Base.Incremental.Telemetry

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
        inherit Texture(ctx, 0, TextureDimension.Texture2D, 0, 0, V3i.Zero, 0, TextureFormat.Rgba, 0L, true)

        let mutable refCount = 0

        member x.Acquire() =
            if Interlocked.Increment &refCount = 1 then
                let b = using ctx.ResourceLock (fun _ -> create())
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

        let nullBuffer = Buffer(ctx, 0n, 0)

        member x.Create(data : IBuffer) =
            match data with
                | :? NullBuffer as nb ->nullBuffer
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

        let nullTex = Texture(ctx, 0, TextureDimension.Texture2D, 1, 1, V3i.Zero, 1, TextureFormat.Rgba, 0L, true)

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


type UniformBufferManager(ctx : Context, renderTaskLock : Option<RenderTaskLock>, size : int, fields : list<UniformField>) =

    let alignedSize = (size + 255) &&& ~~~255

    let buffer = new MappedBuffer(ctx)
    do renderTaskLock |> Option.iter buffer.AddLock
    let manager = MemoryManager.createNop()

    let viewCache = ResourceCache<UniformBufferView>(None, renderTaskLock)
    let rw = new ReaderWriterLockSlim()

    member x.CreateUniformBuffer(scope : Ag.Scope, u : IUniformProvider, additional : SymbolDict<obj>) : IResource<UniformBufferView> =
        let values =
            fields 
            |> List.map (fun f ->
                let sem = Symbol.Create f.semantic
                match u.TryGetUniform(scope, sem) with
                    | Some v -> sem, v
                    | None -> 
                        match additional.TryGetValue sem with
                            | (true, (:? IMod as m)) -> sem, m
                            | _ -> failwithf "[GL] could not get uniform: %A" f
            )

        let key = values |> List.map (fun (_,v) -> v :> obj)

        viewCache.GetOrCreate(
            key,
            fun () ->
                let values = values |> List.map (fun (s,v) -> s, v :> IAdaptiveObject) |> Map.ofList
                let writers = UnmanagedUniformWriters.writers true fields values
     
                let mutable block = Unchecked.defaultof<_>
                { new Resource<UniformBufferView>(ResourceKind.UniformBufferView) with
                    member x.Create old =
                        let handle = 
                            match old with
                                | Some old -> old
                                | None ->
                                    block <- manager.Alloc alignedSize
                                    ReaderWriterLock.write rw (fun () ->
                                        if buffer.Capacity <> manager.Capacity then buffer.Resize(manager.Capacity)
                                    )
                                    UniformBufferView(buffer, block.Offset, nativeint block.Size)

                        ReaderWriterLock.read rw (fun () ->
                            buffer.Use(handle.Offset, handle.Size, fun ptr ->
                                for (_,w) in writers do w.Write(x, ptr)
                            )
                        )
                        handle, FrameStatistics.Zero

                    member x.Destroy h =
                        manager.Free block
                        if manager.AllocatedBytes = 0 then
                            buffer.Resize 0

                }
        )

    member x.Dispose() =
        buffer.Dispose()
        manager.Dispose()


[<AllowNullLiteral>]
type ResourceManager private (parent : Option<ResourceManager>, ctx : Context, renderTaskLock : Option<RenderTaskLock>, shareTextures : bool, shareBuffers : bool) =
    
    let derivedCache (f : ResourceManager -> ResourceCache<'a>) =
        ResourceCache<'a>(Option.map f parent, renderTaskLock)

   
    let bufferManager           = Sharing.BufferManager(ctx, shareBuffers)
    let arrayBufferManager      = Sharing.ArrayBufferManager(ctx, shareBuffers)
    let textureManager          = Sharing.TextureManager(ctx, shareTextures)

    let arrayBufferCache        = derivedCache (fun m -> m.ArrayBufferCache)
    let bufferCache             = derivedCache (fun m -> m.BufferCache)
    let textureCache            = derivedCache (fun m -> m.TextureCache)
    let indirectBufferCache     = derivedCache (fun m -> m.IndirectBufferCache)
    let programCache            = derivedCache (fun m -> m.ProgramCache)
    let samplerCache            = derivedCache (fun m -> m.SamplerCache)
    let vaoCache                = derivedCache (fun m -> m.VAOCache)
    let uniformLocationCache    = derivedCache (fun m -> m.UniformLocationCache)

    let uniformBufferManagers = 
        match parent with
            | Some p -> p.UniformBufferManagers
            | _ -> ConcurrentDictionary<int * list<ActiveUniform>, UniformBufferManager>()

    member private x.ArrayBufferCache       : ResourceCache<Buffer>             = arrayBufferCache
    member private x.BufferCache            : ResourceCache<Buffer>             = bufferCache
    member private x.TextureCache           : ResourceCache<Texture>            = textureCache
    member private x.IndirectBufferCache    : ResourceCache<IndirectBuffer>     = indirectBufferCache
    member private x.ProgramCache           : ResourceCache<Program>            = programCache
    member private x.SamplerCache           : ResourceCache<Sampler>            = samplerCache
    member private x.VAOCache               : ResourceCache<VertexArrayObject>  = vaoCache
    member private x.UniformLocationCache   : ResourceCache<UniformLocation>    = uniformLocationCache
    member private x.UniformBufferManagers                                      = uniformBufferManagers

    member x.RenderTaskLock = renderTaskLock

    new(parent, ctx, lock, shareTextures, shareBuffers) = ResourceManager(Some parent, ctx, lock, shareTextures, shareBuffers)
    new(ctx, lock, shareTextures, shareBuffers) = ResourceManager(None, ctx, lock, shareTextures, shareBuffers)


    member x.Context = ctx

    member x.CreateBuffer(data : IMod<Array>) =
        bufferCache.GetOrCreate<Array>(data, {
            create = fun b      -> arrayBufferManager.Create b
            update = fun h b    -> arrayBufferManager.Update(h, b)
            delete = fun h      -> arrayBufferManager.Delete h
            kind = ResourceKind.Buffer
        })

    member x.CreateBuffer(data : IMod<IBuffer>) =
        match data with
            | :? IAdaptiveBuffer as data ->
                bufferCache.GetOrCreate(
                    [data :> obj],
                    fun () ->
                        let mutable r = Unchecked.defaultof<_>
                        { new Resource<Buffer>(ResourceKind.Buffer) with
                            member x.Create (old : Option<Buffer>) =
                                match old with
                                    | None ->
                                        r <- data.GetReader()
                                        let (nb, _) = r.GetDirtyRanges(x)
                                        ctx.CreateBuffer(nb), FrameStatistics.Zero
                                    | Some old ->
                                        let (nb, ranges) = r.GetDirtyRanges(x)
                                        nb.Use (fun ptr ->
                                            ctx.UploadRanges(old, ptr, ranges)
                                        )
                                        old, FrameStatistics.Zero

                            member x.Destroy(b : Buffer) =
                                ctx.Delete b
                                r.Dispose()
                        }
                )

            | _ ->
                bufferCache.GetOrCreate<IBuffer>(data, {
                    create = fun b      -> bufferManager.Create b
                    update = fun h b    -> bufferManager.Update(h, b)
                    delete = fun h      -> bufferManager.Delete h
                    kind = ResourceKind.Buffer
                })

    member x.CreateTexture(data : IMod<ITexture>) =
        textureCache.GetOrCreate<ITexture>(data, {
            create = fun b      -> textureManager.Create b
            update = fun h b    -> textureManager.Update(h, b)
            delete = fun h      -> textureManager.Delete h
            kind = ResourceKind.Texture
        })

    member x.CreateIndirectBuffer(indexed : bool, data : IMod<IBuffer>) =
        indirectBufferCache.GetOrCreate<IBuffer>(data, {
            create = fun b      -> ctx.CreateIndirect(indexed, b)
            update = fun h b    -> ctx.UploadIndirect(h, indexed, b); h
            delete = fun h      -> ctx.Delete h
            kind = ResourceKind.Buffer
        })

    member x.CreateSurface(signature : IFramebufferSignature, surface : IMod<ISurface>) =
        let create (s : ISurface) =
            match SurfaceCompilers.compile ctx signature s with
                | Success program -> program
                | Error e -> 
                    Log.error "[GL] surface compilation failed: %s" e
                    failwithf "[GL] surface compilation failed: %s" e

        programCache.GetOrCreate<ISurface>(surface, {
            create = fun b      -> create b
            update = fun h b    -> ctx.Delete(h); create b
            delete = fun h      -> ctx.Delete h
            kind = ResourceKind.ShaderProgram
        })

    member x.CreateSampler (sam : IMod<SamplerStateDescription>) =
        samplerCache.GetOrCreate<SamplerStateDescription>(sam, {
            create = fun b      -> ctx.CreateSampler b
            update = fun h b    -> ctx.Update(h,b); h
            delete = fun h      -> ctx.Delete h
            kind = ResourceKind.SamplerState
        })

    member x.CreateVertexArrayObject( bindings : list<int * BufferView * AttributeFrequency * IResource<Buffer>>, index : Option<IResource<Buffer>>) =
        let createView (self : IAdaptiveObject) (index : int, view : BufferView, frequency : AttributeFrequency, buffer : IResource<Buffer>) =
            index, { 
                Type = view.ElementType
                Frequency = frequency
                Normalized = false; 
                Stride = view.Stride
                Offset = view.Offset
                Buffer = buffer.Handle.GetValue self
            }

        vaoCache.GetOrCreate(
            [ bindings :> obj; index :> obj ],
            fun () ->
                { new Resource<VertexArrayObject>(ResourceKind.VertexArrayObject) with
                    member x.Create (old : Option<VertexArrayObject>) =
                        let attributes = bindings |> List.map (createView x)
                        let index = match index with | Some i -> i.Handle.GetValue x |> Some | _ -> None
                        
                        match old with
                            | Some old -> ctx.Delete old
                            | None -> ()

                        let handle = 
                            match index with
                                | Some i -> ctx.CreateVertexArrayObject(i, attributes)
                                | None -> ctx.CreateVertexArrayObject(attributes)

                        handle, FrameStatistics.Zero
                        
                    member x.Destroy vao =
                        ctx.Delete vao
                }
        )

    member x.CreateUniformLocation(scope : Ag.Scope, u : IUniformProvider, uniform : ActiveUniform) =
        match u.TryGetUniform (scope, Sym.ofString uniform.semantic) with
            | Some v ->
                uniformLocationCache.GetOrCreate(
                    [v :> obj],
                    fun () ->
                        let inputs = Map.ofList [Symbol.Create uniform.semantic, v :> IAdaptiveObject]
                        let _,writer = UnmanagedUniformWriters.writers false [uniform.UniformField] inputs |> List.head
     
                        { new Resource<UniformLocation>(ResourceKind.UniformLocation) with
                            member x.Create old =
                                let handle =
                                    match old with 
                                        | Some o -> o
                                        | None -> ctx.CreateUniformLocation(uniform.uniformType.SizeInBytes, uniform.uniformType)
                                
                                writer.Write(x, handle.Data)
                                handle, FrameStatistics.Zero

                            member x.Destroy h =
                                ctx.Delete h
                        }        
                )
                

            | None ->
                failwithf "[GL] could not get uniform: %A" uniform
     
    member x.CreateUniformBuffer(scope : Ag.Scope, layout : UniformBlock, program : Program, u : IUniformProvider) =
        let manager = 
            uniformBufferManagers.GetOrAdd(
                (layout.size, layout.fields), 
                fun (s,f) -> 
                    let uniformFields = layout.fields |> List.map (fun f -> f.UniformField)
                    new UniformBufferManager(ctx, renderTaskLock, s, uniformFields)
            )

        manager.CreateUniformBuffer(scope, u, program.UniformGetters)
      