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

type RefCountedResource<'h>(create : unit -> 'h, delete : 'h -> unit) =
    let mutable refCount = 0
    let mutable handle = Unchecked.defaultof<'h>

    member x.Acquire() = 
        if Interlocked.Increment &refCount = 1 then
            handle <- create()

    member x.Release() = 
        if Interlocked.Decrement &refCount = 0 then
            delete handle
            handle <- Unchecked.defaultof<_>

    member x.Handle =
        if refCount <= 0 then 
            failwith "[RefCountedResource] ref count zero"

        handle

// TODO:
// 1) Buffer/Texture sharing
// 2) NullBuffers
// 3) NullTextures

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


    type BufferManager(ctx : Context) =
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
            let shared = get data
            shared.Acquire()
            shared :> Buffer

        member x.Update(b : Buffer, data : IBuffer) : Buffer =
            match b with
                | :? RefCountedBuffer as b ->
                    
                    let newShared = get data
                    if newShared = b then
                        b :> Buffer
                    else
                        newShared.Acquire()
                        b.Release()
                        newShared :> Buffer
                | _ ->
                    ctx.Upload(b, data)
                    b

        member x.Delete(b : Buffer) =
            match b with
                | :? RefCountedBuffer as b -> b.Release()
                | _ -> ctx.Delete b

    type ArrayBufferManager(ctx : Context) =
        let cache = ConcurrentDictionary<Array, RefCountedBuffer>()

        let get (b : Array) =
            cache.GetOrAdd(b, fun v -> 
                RefCountedBuffer(
                    ctx,
                    (fun () -> ctx.CreateBuffer b),
                    (fun () -> cache.TryRemove b |> ignore)
                )
            )

        member x.Create(data : Array) =
            let shared = get data
            shared.Acquire()
            shared :> Buffer

        member x.Update(b : Buffer, data : Array) : Buffer =
            match b with
                | :? RefCountedBuffer as b ->
                    
                    let newShared = get data
                    if newShared = b then
                        b :> Buffer
                    else
                        newShared.Acquire()
                        b.Release()
                        newShared :> Buffer
                | _ ->
                    ctx.Upload(b, data)
                    b

        member x.Delete(b : Buffer) =
            match b with
                | :? RefCountedBuffer as b -> b.Release()
                | _ -> ctx.Delete b

    type TextureManager(ctx : Context) =
        let cache = ConcurrentDictionary<ITexture, RefCountedTexture>()

        let get (b : ITexture) =
            cache.GetOrAdd(b, fun v -> 
                RefCountedTexture(
                    ctx,
                    (fun () -> printfn "created texture"; ctx.CreateTexture b),
                    (fun () -> printfn "destroyed texture"; cache.TryRemove b |> ignore)
                )
            )

        member x.Create(data : ITexture) =
            let shared = get data
            shared.Acquire()
            shared :> Texture

        member x.Update(b : Texture, data : ITexture) : Texture =
            match b with
                | :? RefCountedTexture as b ->
                    
                    let newShared = get data
                    if newShared = b then
                        b :> Texture
                    else
                        newShared.Acquire()
                        b.Release()
                        newShared :> Texture
                | _ ->
                    ctx.Upload(b, data)
                    b

        member x.Delete(b : Texture) =
            match b with
                | :? RefCountedTexture as b -> b.Release()
                | _ -> ctx.Delete b



type UniformBufferView =
    class
        val mutable public Buffer : IMod<IBuffer>
        val mutable public Offset : nativeint
        val mutable public Size : nativeint

        new(b,o,s) = { Buffer = b; Offset = o; Size = s }

    end

type UniformBufferManager(ctx : Context, size : int, fields : list<ActiveUniform>) =
  
    static let singleStats = { FrameStatistics.Zero with ResourceUpdateCount = 1.0; ResourceUpdateCounts = Map.ofList [ResourceKind.UniformBufferView, 1.0] }
    
    let alignedSize = (size + 255) &&& ~~~255

    let buffer = new MappedBuffer(ctx)
    let manager = MemoryManager.createNop()

    let viewCache = ResourceCache<UniformBufferView>()
    let rw = new ReaderWriterLockSlim()

    member x.CreateUniformBuffer(scope : Ag.Scope, u : IUniformProvider, additional : SymbolDict<obj>) : IResource<UniformBufferView> =
        let values =
            fields |> List.map (fun f ->
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
                let uniformFields = fields |> List.map (fun a -> a.UniformField)
                let writers = UnmanagedUniformWriters.writers true uniformFields values
     
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
                        handle, singleStats

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
type ResourceManager private (parent : Option<ResourceManager>, ctx : Context, shareTextures : bool, shareBuffers : bool) =
    static let updateStats (kind : ResourceKind) =
        { FrameStatistics.Zero with ResourceUpdateCount = 1.0; ResourceUpdateCounts = Map.ofList [kind, 1.0] }

    static let bufferUpdateStats = updateStats ResourceKind.Buffer
    static let textureUpdateStats = updateStats ResourceKind.Texture
    static let programUpdateStats = updateStats ResourceKind.ShaderProgram
    static let samplerUpdateStats = updateStats ResourceKind.SamplerState
    static let vaoUpdateStats = updateStats ResourceKind.VertexArrayObject
    static let uniformLocationUpdateStats = updateStats ResourceKind.UniformLocation

    let bufferManager = Sharing.BufferManager(ctx)
    let arrayBufferManager = Sharing.ArrayBufferManager(ctx)
    let textureManager = Sharing.TextureManager(ctx)

    let arrayBufferCache = ResourceCache<Buffer>()
    let bufferCache = ResourceCache<Buffer>()
    let textureCache = ResourceCache<Texture>()
    let indirectBufferCache = ResourceCache<IndirectBuffer>()
    let programCache = ResourceCache<Program>()
    let samplerCache = ResourceCache<Sampler>()
    let vaoCache = ResourceCache<VertexArrayObject>()
    let uniformLocationCache = ResourceCache<UniformLocation>()

    let uniformBufferManagers = ConcurrentDictionary<int * list<ActiveUniform>, UniformBufferManager>()

    new(parent, ctx, shareTextures, shareBuffers) = ResourceManager(Some parent, ctx, shareTextures, shareBuffers)
    new(ctx, shareTextures, shareBuffers) = ResourceManager(None, ctx, shareTextures, shareBuffers)

    member x.Context = ctx

    member x.CreateBuffer(data : IMod<Array>) =
        bufferCache.GetOrCreate<Array>(data, {
            create = fun b      -> arrayBufferManager.Create b
            update = fun h b    -> arrayBufferManager.Update(h, b)
            delete = fun h      -> arrayBufferManager.Delete h
            stats  = bufferUpdateStats
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
                                        ctx.CreateBuffer(nb), bufferUpdateStats
                                    | Some old ->
                                        let (nb, ranges) = r.GetDirtyRanges(x)
                                        nb.Use (fun ptr ->
                                            ctx.UploadRanges(old, ptr, ranges)
                                        )
                                        old, bufferUpdateStats

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
                    stats  = bufferUpdateStats
                    kind = ResourceKind.Buffer
                })

    member x.CreateTexture(data : IMod<ITexture>) =
        textureCache.GetOrCreate<ITexture>(data, {
            create = fun b      -> textureManager.Create b
            update = fun h b    -> textureManager.Update(h, b)
            delete = fun h      -> textureManager.Delete h
            stats  = textureUpdateStats
            kind = ResourceKind.Texture
        })

    member x.CreateIndirectBuffer(indexed : bool, data : IMod<IBuffer>) =
        indirectBufferCache.GetOrCreate<IBuffer>(data, {
            create = fun b      -> ctx.CreateIndirect(indexed, b)
            update = fun h b    -> ctx.UploadIndirect(h, indexed, b); h
            delete = fun h      -> ctx.Delete h
            stats  = bufferUpdateStats
            kind = ResourceKind.Buffer
        })

    member x.CreateSurface(signature : IFramebufferSignature, surface : IMod<ISurface>) =
        let create (s : ISurface) =
            match SurfaceCompilers.compile ctx signature s with
                | Success program -> program
                | Error e -> failwithf "[GL] surface compilation failed: %s" e

        programCache.GetOrCreate<ISurface>(surface, {
            create = fun b      -> create b
            update = fun h b    -> ctx.Delete(h); create b
            delete = fun h      -> ctx.Delete h
            stats  = programUpdateStats
            kind = ResourceKind.ShaderProgram
        })

    member x.CreateSampler (sam : IMod<SamplerStateDescription>) =
        samplerCache.GetOrCreate<SamplerStateDescription>(sam, {
            create = fun b      -> ctx.CreateSampler b
            update = fun h b    -> ctx.Update(h,b); h
            delete = fun h      -> ctx.Delete h
            stats  = samplerUpdateStats
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

                        handle, vaoUpdateStats
                        
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
                                handle, uniformLocationUpdateStats

                            member x.Destroy h =
                                ctx.Delete h
                        }        
                )
                

            | None ->
                failwithf "[GL] could not get uniform: %A" uniform
     
    member x.CreateUniformBuffer(scope : Ag.Scope, layout : UniformBlock, program : Program, u : IUniformProvider) =
        let manager = 
            uniformBufferManagers.GetOrAdd((layout.size, layout.fields), fun (s,f) -> new UniformBufferManager(ctx, s, f))

        manager.CreateUniformBuffer(scope, u, program.UniformGetters)
      