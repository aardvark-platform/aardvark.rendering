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

module ResourcesNewNotSo =
    open System.Collections.Generic
    open System.Collections.Concurrent

    type ResourceDescription<'a, 'h> =
        {
            create : 'a -> 'h
            delete : 'h -> unit
            tryUpdate : 'h -> 'a -> bool
        }

    type ResourceCacheEntry<'h> =
        class
            val mutable public Key : obj
            val mutable public RefCount : int
            val mutable public Handle : 'h
            val mutable public IsOwned : bool

            new(k,c,h,o) = { Key = k; RefCount = c; Handle = h; IsOwned = o }
        end

    type ResourceCache<'a, 'h>(desc : ResourceDescription<'a, 'h>) =
        static let isNonTrivial = 
            let ta = typeof<'a>
            if ta.IsEnum then not (typeof<'h>.IsAssignableFrom (ta.GetEnumUnderlyingType()))
            else not (typeof<'h>.IsAssignableFrom ta)

        let cache = Dict<obj, ResourceCacheEntry<'h>>()


        let createEntry (key : obj) =
            match key with
                | :? 'h as h when isNonTrivial -> ResourceCacheEntry(key, 0, h, false)
                | :? 'a as v -> ResourceCacheEntry(key, 0, desc.create v, true)
                | _ -> failwith "bad"

        member x.Get(key : 'a) =
            lock cache (fun () ->
                let entry = cache.GetOrCreate(key :> obj, Func<_,_>(createEntry))
                entry.RefCount <- entry.RefCount + 1
                entry
            )

        member x.Delete(entry : ResourceCacheEntry<'h>) =
            let deleted = 
                lock cache (fun () ->
                    if entry.RefCount = 1 then
                        cache.Remove entry.Key |> ignore
                        if entry.IsOwned then
                            Some entry
                        else
                            None
                    else
                        entry.RefCount <- entry.RefCount - 1
                        None
                )
            match deleted with
                | Some d -> desc.delete d.Handle
                | None -> ()

        member x.Update(entry : ResourceCacheEntry<'h>, newKey : 'a) =
            if entry.Key = (newKey :> obj) then
                entry
            else
                let mutable deleted = None
                let result = 
                    lock cache (fun () ->
                        if entry.RefCount = 1 then
                            cache.Remove entry.Key |> ignore
                            if entry.IsOwned then
                                match cache.TryGetValue newKey with
                                    | (true, newEntry) ->
                                        deleted <- Some entry.Handle
                                        newEntry
                                    | _ ->
                                        if desc.tryUpdate entry.Handle newKey then
                                            entry.Key <- newKey
                                            cache.[newKey] <- entry
                                            entry
                                        else
                                            deleted <- Some entry.Handle
                                            x.Get newKey
                            else
                                x.Get newKey
                                
                        else
                            entry.RefCount <- entry.RefCount - 1
                            x.Get newKey
                    )
                match deleted with
                    | Some d -> desc.delete d
                    | None -> ()

                result

       
    [<AbstractClass>]
    type AdaptiveResource() =
        inherit AdaptiveObject()
        let mutable refCount = 0
        
        let locks = HashSet<RenderTaskLock>()


        abstract member Update : AdaptiveToken -> unit
        abstract member Release : unit -> unit
        abstract member HandleType : Type

        member internal x.Locks = locks

        member x.AddLock(l : RenderTaskLock) =
            lock locks (fun () -> locks.Add l |> ignore)
                
            
        member x.RemoveLock(l : RenderTaskLock) =
            lock locks (fun () -> locks.Remove l |> ignore)

        member x.Dispose() =
            if Interlocked.Decrement(&refCount) = 0 then
                lock x (fun () ->
                    let mutable foo = 0
                    x.Outputs.Consume(&foo) |> ignore
                    x.OutOfDate <- true
                    x.Release()
                )
            
        member x.IncrementRefCount() =
            Interlocked.Increment(&refCount) |> ignore

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    [<AbstractClass>]
    type AdaptiveResource<'h> =
        class
            inherit AdaptiveResource
            
            val mutable public cache : 'h
            val mutable public RefCount : int

            abstract member Compute : AdaptiveToken -> 'h
            
            override x.HandleType = typeof<'h>

            override x.Update(token) =
                x.GetValue(token) |> ignore

            member x.GetValue(t : AdaptiveToken) =
                x.EvaluateAlways t (fun t ->
                    if x.OutOfDate then
                        let v = x.Compute t
                        x.cache <- v
                        v
                    else
                        x.cache
                )

            interface IMod with
                member x.IsConstant = false
                member x.GetValue(t) = x.GetValue(t) :> obj
                
            interface IMod<'h> with
                member x.GetValue(t) = x.GetValue(t)


            new() = { inherit AdaptiveResource(); cache = Unchecked.defaultof<_>; RefCount = 0 }
        end

    [<AbstractClass>]
    type AdaptiveResource<'h, 'n when 'n : unmanaged>() =
        inherit AdaptiveResource<'h>()

        abstract member Pointer : nativeptr<'n>

    type AdaptiveResource<'a, 'h, 'n when 'n : unmanaged>(cache : ResourceCache<'a, 'h>, input : IMod<'a>, view : 'h -> 'n) =
        inherit AdaptiveResource<'h, 'n>()

        let mutable entry : Option<ResourceCacheEntry<'h>> = None
        let ptr = NativePtr.alloc 1
        let mutable oldLocked = None

        member private x.updateLocks (value : obj) =
            match value with
                | :? ILockedResource as n when not (isNull value) ->
                    match oldLocked with
                        | Some o ->
                            if o <> n then 
                                oldLocked <- Some n
                                for l in x.Locks do
                                    l.Remove o
                                    l.Add n
                        | None ->
                            oldLocked <- Some n
                            for l in x.Locks do
                                l.Add n
                | _ ->
                    match oldLocked with
                        | Some o ->
                            oldLocked <- None
                            for l in x.Locks do
                                l.Remove o
                        | _ ->
                            ()


        override x.Pointer = ptr

        override x.Compute(token) =
            let value = input.GetValue token
            x.updateLocks value

            let newEntry = 
                match entry with
                    | Some e -> cache.Update(e, value)
                    | None -> cache.Get(value)
                
            NativePtr.write ptr (view newEntry.Handle)
            newEntry.Handle

        override x.Release() =
            match entry with
                | Some e ->
                    x.updateLocks null
                    entry <- None
                    cache.Delete e
                    entry <- None
                    NativePtr.write ptr Unchecked.defaultof<_>
                | _ ->
                    ()

    type AdaptiveDescriptor =
        | AdaptiveUniformBuffer of int * UniformBuffer
        | AdaptiveCombinedImageSampler of int * array<Option<AdaptiveResource<ImageView, VkImageView> * AdaptiveResource<Sampler, VkSampler>>>

    [<AutoOpen>]
    module private Caches = 
        
        [<AbstractClass>]
        type private AdaptiveHackResource<'h, 'n when 'n : unmanaged>() =
            inherit AdaptiveResource<'h, 'n>()
            let ptr = NativePtr.alloc 1

            member x.Set(h : 'n) = NativePtr.write ptr h
            override x.Pointer = ptr

        type AdaptiveDescriptorSetCache(device : Device, pool : DescriptorPool) =
            let cache = ConcurrentDictionary<DescriptorSetLayout * list<AdaptiveDescriptor>, AdaptiveHackResource<DescriptorSet, VkDescriptorSet>>()

            member x.Create(layout : DescriptorSetLayout, bindings : list<AdaptiveDescriptor>) =
                let res = 
                    cache.GetOrAdd((layout,bindings), fun (layout, bindings) ->
                
                        let mutable current = None
                        { new AdaptiveHackResource<DescriptorSet, VkDescriptorSet>() with
                            override x.Compute(token) =
                                let set = 
                                    match current with
                                        | Some c -> c
                                        | None -> 
                                            let c = pool.Alloc(layout)
                                            current <- Some c
                                            x.Set c.Handle
                                            c

                                let descriptors =
                                    bindings |> List.map (fun d ->
                                        match d with
                                            | AdaptiveDescriptor.AdaptiveUniformBuffer(i,b) -> 
                                                Descriptor.UniformBuffer(i, b)
                                            | AdaptiveDescriptor.AdaptiveCombinedImageSampler(i, imgs) ->
                                                let imgs = imgs |> Array.map (function Some (i,s) -> Some (i.GetValue(token), s.GetValue(token)) | None -> None)
                                                Descriptor.CombinedImageSampler(i, imgs)
                                    )

                                pool.Update(set, List.toArray descriptors)
                                set

                            override x.Release() =
                                match current with
                                    | Some b -> 
                                        current <- None
                                        x.Set VkDescriptorSet.Null
                                        pool.Free b
                                    | _ -> 
                                        ()
                        }
                    )
                res.IncrementRefCount()
                res :> AdaptiveResource<_,_>
  
        type AdaptiveResourceCache<'a, 'h, 'n when 'n : unmanaged>(desc : ResourceDescription<'a, 'h>, view : 'h -> 'n) =
            let cache = ConcurrentDictionary<IMod<'a>, AdaptiveResource<'a, 'h, 'n>>()
            let handleCache = ResourceCache<'a, 'h>(desc)

            member x.Create(input : IMod<'a>) =
                let res = 
                    cache.GetOrAdd(input, fun input ->
                        new AdaptiveResource<'a, 'h, 'n>(handleCache, input, view)
                    )
                res.IncrementRefCount()
                res
    
        type AdaptiveResourceCacheV<'a, 'b, 'h, 'n when 'n : unmanaged>(desc : ResourceDescription<'a * 'b, 'h>, view : 'h -> 'n) =
            let cache = ConcurrentDictionary<IMod<'a> * 'b, AdaptiveResource<'a * 'b, 'h, 'n>>()
            let handleCache = ResourceCache<'a * 'b, 'h>(desc)

            member x.Create(input : IMod<'a>, value : 'b) =
                let res = 
                    cache.GetOrAdd((input, value), fun (input, value) ->
                        let input = input |> Mod.map (fun i -> i,value)
                        new AdaptiveResource<'a * 'b, 'h, 'n>(handleCache, input, view)
                    )
                res.IncrementRefCount()
                res

        type AdaptiveResourceCacheNV<'a, 'b, 'h, 'n when 'n : unmanaged>(desc : ResourceDescription<'a[] * 'b, 'h>, view : 'h -> 'n) =
            let cache = ConcurrentDictionary<list<obj>, AdaptiveResource<'a[] * 'b, 'h, 'n>>()
            let handleCache = ResourceCache<'a[] * 'b, 'h>(desc)

            member x.Create(input : list<IMod<'a>>, values : 'b) =
                let res = 
                    cache.GetOrAdd([input :> obj; values :> obj], fun _ ->
                        let arr = Mod.mapN (fun v -> Seq.toArray v, values) input
                        new AdaptiveResource<'a[] * 'b, 'h, 'n>(handleCache, arr, view)
                    )  
                res.IncrementRefCount()
                res

        type AdaptiveUniformBufferCache(device : Device) =
            let cache = ConcurrentDictionary<list<obj>, AdaptiveResource<UniformBuffer>>()

            static let values (scope : Ag.Scope) (layout : UniformBufferLayout) (u : IUniformProvider) (additional : SymbolDict<IMod>) =
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

            member x.Create(scope : Ag.Scope, layout : UniformBufferLayout, u : IUniformProvider, additional : SymbolDict<IMod>) =
                let values = values scope layout u additional
                let key = (layout :> obj) :: (values |> List.map (fun (_,v) -> v :> obj))

                let res =
                    cache.GetOrAdd(key, fun _ ->
                        let writers = 
                            values |> List.map (fun (target, m) ->
                                match m.GetType() with
                                    | ModOf tSource -> m, UniformWriters.getWriter target.offset target.fieldType tSource
                                    | _ -> failf ""
                            )

                        let mutable current = None
                        { new AdaptiveResource<UniformBuffer>() with
                            override x.Compute(token) =
                                let buffer = 
                                    match current with
                                        | Some c -> c
                                        | None -> 
                                            let c = device.CreateUniformBuffer(layout)
                                            current <- Some c
                                            c

                                for (m,w) in writers do
                                    w.Write(token, m, buffer.Storage.Pointer)
                                device.Upload(buffer)
                                buffer

                            override x.Release() =
                                match current with
                                    | Some b -> 
                                        current <- None
                                        device.Delete b
                                    | _ -> 
                                        ()
                        }
                    )
                res.IncrementRefCount()
                res

        type AdaptivePipelineCache(device : Device) =
            let cache = ConcurrentDictionary<list<obj>, AdaptiveHackResource<Pipeline, VkPipeline>>()

            member x.Create(
                            pass            : RenderPass,
                            program         : AdaptiveResource<ShaderProgram>,
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
                let res = 
                    cache.GetOrAdd(key, fun _ ->
                        let writeMasks = Array.zeroCreate pass.ColorAttachmentCount
                        for (i, (sem,_)) in Map.toSeq pass.ColorAttachments do 
                            if Set.contains sem writeBuffers then writeMasks.[i] <- true
                            else writeMasks.[i] <- false

                        let writeDepth = Set.contains DefaultSemantic.Depth writeBuffers

                        let prog = program.GetValue()
                        let usesDiscard = program.GetValue().HasDiscard
                        let vertexInputState = VertexInputState.create inputs
                        let inputAssembly = Mod.map InputAssemblyState.ofIndexedGeometryMode geometryMode
                        let rasterizerState = Mod.custom (fun self -> RasterizerState.create usesDiscard (depthTest.GetValue self) (cullMode.GetValue self) (fillMode.GetValue self))
                        let colorBlendState = Mod.map (ColorBlendState.create writeMasks pass.ColorAttachmentCount) blendMode
                        let multisampleState = MultisampleState.create prog.SampleShading anyAttachment.samples
                        let depthState = Mod.map (DepthState.create writeDepth) depthTest
                        let stencilState = Mod.map StencilState.create stencilMode

                        let mutable old : Option<Pipeline> = None
                        { new AdaptiveHackResource<Pipeline, VkPipeline>() with
                            override x.Compute(token) =
                                let desc =
                                    {

                                        renderPass              = pass
                                        shaderProgram           = program.GetValue()
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
                                            let n = device.CreateGraphicsPipeline desc
                                            x.Set n.Handle
                                            old <- Some n
                                            n
                                    | None ->
                                        let n = device.CreateGraphicsPipeline desc
                                        x.Set n.Handle
                                        old <- Some n
                                        n

                            override x.Release() =
                                match old with
                                    | Some o ->
                                        device.Delete o
                                        old <- None
                                        x.Set VkPipeline.Null
                                    | _ ->
                                        ()
                        }
                    
                    )
                res.IncrementRefCount()
                res :> AdaptiveResource<_,_>



        let inline simpleCache< ^a, ^h, ^n when ^n : unmanaged and ^h : (member Handle : ^n) > (desc : ResourceDescription<'a, 'h>) =
            let view (h : 'h) = ( ^h : (member Handle : 'n) (h))
            AdaptiveResourceCache<'a, 'h, 'n>(desc, view)

        let inline adaptiveCache<'a, 'h, 'n when 'n : unmanaged> (view : 'h -> 'n) (desc : ResourceDescription<'a, 'h>) =
            AdaptiveResourceCache<'a, 'h, 'n>(desc, view)

        let inline adaptiveCacheV<'a, 'b, 'h, 'n when 'n : unmanaged> (view : 'h -> 'n) (desc : ResourceDescription<'a * 'b, 'h>) =
            AdaptiveResourceCacheV<'a, 'b, 'h, 'n>(desc, view)


        let inline adaptiveArrayCacheV<'a, 'b, 'h, 'n when 'n : unmanaged> (view : 'h -> 'n) (desc : ResourceDescription<'a[] * 'b, 'h>) =
            AdaptiveResourceCacheNV<'a, 'b, 'h, 'n>(desc, view)

    type ResourceManager(device : Device) =
        let descriptorPool = device.CreateDescriptorPool(1 <<< 20, 1 <<< 22)
        do device.OnDispose.Add (fun () -> device.Delete descriptorPool)

        let programCache =
            adaptiveCacheV<ISurface, RenderPass, ShaderProgram, int>
                (fun _ -> 0)
                {
                    create = fun (s,p) -> device.CreateShaderProgram(p, s)
                    tryUpdate = fun _ _ -> false
                    delete = fun p -> device.Delete p
                }

        let pipelineCache =
            AdaptivePipelineCache(device)

        let vertexBufferCache = 
            simpleCache<IBuffer, Buffer,_> 
                {
                    create      = fun b -> device.CreateBuffer(VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.VertexBufferBit, b)
                    tryUpdate   = fun h b -> h.TryUpdate b
                    delete      = fun b -> device.Delete b
                }

        let bufferBindingCache =
            adaptiveArrayCacheV<Buffer, list<int64>, VertexBufferBinding, VertexBufferBinding>
                id
                {
                    create      = fun (b, off) -> new VertexBufferBinding(0, b, List.toArray off)
                    tryUpdate   = fun bb (b,off) -> bb.TryUpdate(0, b, List.toArray off)
                    delete      = fun b -> b.Dispose()
                }

        let indirectBufferCache = 
            adaptiveCacheV<IIndirectBuffer, bool, IndirectBuffer,V2l> 
                (fun b -> V2l(b.Handle.Handle, int64 b.Count))
                {
                    create      = fun (b,indexed) -> device.CreateIndirectBuffer(indexed, b)
                    tryUpdate   = fun _ _ -> false
                    delete      = fun b -> device.Delete b
                }

        let indexBufferCache = 
            simpleCache<IBuffer, Buffer,_> 
                {
                    create      = fun b -> device.CreateBuffer(VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.IndexBufferBit, b)
                    tryUpdate   = fun h b -> h.TryUpdate b
                    delete      = fun b -> device.Delete b
                }

        let indexBufferBindingCache = 
            adaptiveCacheV<Buffer, Type, IndexBufferBinding,_> 
                id
                {
                    create      = fun (b,t) -> new IndexBufferBinding(b.Handle, VkIndexType.ofType t)
                    tryUpdate   = fun h b -> false
                    delete      = fun b -> ()
                }

        let uniformBufferCache =
            AdaptiveUniformBufferCache(device)

        let imageCache =
            simpleCache<ITexture, Image, _>
                {
                    create = fun t -> device.CreateImage(t)
                    tryUpdate = fun _ _ -> false
                    delete = fun h -> device.Delete h
                }

        let imageViewCache =
            simpleCache<Image, ImageView, _>
                {
                    create = fun img -> device.CreateImageView(img, VkComponentMapping.Identity)
                    tryUpdate = fun _ _ -> false
                    delete = fun d -> device.Delete d
                }

        let samplerCache =
            simpleCache<SamplerStateDescription, Sampler,_>
                {
                    create = fun s -> device.CreateSampler(s)
                    tryUpdate = fun _ _ -> false
                    delete = fun s -> device.Delete s
                }

        let directCallCache =
            adaptiveCacheV<list<DrawCallInfo>, bool, DrawCall, DrawCall>
                id
                {
                    create = fun (l,i) -> device.CreateDrawCall(i,l)
                    tryUpdate = fun _ _ -> false
                    delete = fun c -> c.Dispose()
                }

        let indirectCallCache =
            adaptiveCacheV<IndirectBuffer, bool, DrawCall, DrawCall>
                id
                {
                    create = fun (l,i) -> device.CreateDrawCall(i,l)
                    tryUpdate = fun _ _ -> false
                    delete = fun c -> c.Dispose()
                }
            
        let descriptorSetCache = AdaptiveDescriptorSetCache(device, descriptorPool)

        let descriptorSetBindingCache =
            adaptiveArrayCacheV<DescriptorSet, PipelineLayout, DescriptorSetBinding, DescriptorSetBinding>
                id 
                {
                    create = fun (l,layout) -> new DescriptorSetBinding(layout, 0, l)
                    tryUpdate = fun _ _ -> false
                    delete = fun l -> l.Dispose()
                }

        let isActiveCache =
            adaptiveCache
                (fun v -> if v then 1 else 0)
                {
                    create = fun v -> v
                    tryUpdate = fun _ _ -> false
                    delete = fun _ -> ()
                }

        member x.Device = device

        member x.CreateShaderProgram(pass : RenderPass, surface : IMod<ISurface>) =
            programCache.Create(surface, pass) :> AdaptiveResource<_>

        member x.CreateVertexBuffer(m : IMod<IBuffer>) =
            match m with
                | :? SingleValueBuffer as b ->
                    let b = b.Value |> Mod.map (fun v -> ArrayBuffer [|v|] :> IBuffer)
                    x.CreateVertexBuffer(b)
                | _ -> 
                    let res = vertexBufferCache.Create( m) :> AdaptiveResource<Buffer, VkBuffer>
                    res
        member x.CreateVertexBufferBinding(m : list<AdaptiveResource<Buffer, VkBuffer>>, offsets : list<int64>) =
            let m = List.map (fun m -> m :> IMod<_>) m
            bufferBindingCache.Create(m, offsets)

        member x.CreateIndexBuffer(m : IMod<IBuffer>) =
            indexBufferCache.Create(m) :> AdaptiveResource<_,_>

        member x.CreateIndexBufferBinding(m : AdaptiveResource<Buffer, VkBuffer>, t : Type) =
            indexBufferBindingCache.Create(m, t) :> AdaptiveResource<_,_>

        member x.CreateIndirectBuffer(indexed : bool, b : IMod<IIndirectBuffer>) =
            indirectBufferCache.Create(b, indexed) :> AdaptiveResource<_,_>

        member x.CreateUniformBuffer(scope : Ag.Scope, layout : UniformBufferLayout, u : IUniformProvider, additional : SymbolDict<IMod>) =
            uniformBufferCache.Create(scope, layout, u, additional)

        member x.CreateImage(t : IMod<ITexture>) =
            imageCache.Create(t) :> AdaptiveResource<_,_>

        member x.CreateImageView(t : AdaptiveResource<Image, VkImage>) =
            imageViewCache.Create(t) :> AdaptiveResource<_,_>

        member x.CreateSampler(s : IMod<SamplerStateDescription>) =
            samplerCache.Create(s) :> AdaptiveResource<_,_>

        member x.CreateDescriptorSet(layout : DescriptorSetLayout, bindings : list<AdaptiveDescriptor>) =
            descriptorSetCache.Create(layout, bindings)

        member x.CreateDescriptorSetBinding(layout : PipelineLayout, descriptors : list<AdaptiveResource<DescriptorSet, VkDescriptorSet>>) =
            let descriptors = descriptors |> List.map (fun d -> d :> IMod<_>)
            descriptorSetBindingCache.Create(descriptors, layout) :> AdaptiveResource<_,_>

        member x.CreateDrawCall(indexed : bool, calls : IMod<list<DrawCallInfo>>) =
            directCallCache.Create(calls, indexed) :> AdaptiveResource<_,_>

        member x.CreateDrawCall(indexed : bool, indirect : AdaptiveResource<IndirectBuffer, V2l>) =
            indirectCallCache.Create(indirect, indexed) :> AdaptiveResource<_,_>

        member x.CreatePipeline(pass            : RenderPass,
                                program         : AdaptiveResource<ShaderProgram>,
                                inputs          : Map<Symbol, bool * Aardvark.Base.BufferView>,
                                geometryMode    : IMod<IndexedGeometryMode>,
                                fillMode        : IMod<FillMode>,
                                cullMode        : IMod<CullMode>,
                                blendMode       : IMod<BlendMode>,
                                depthTest       : IMod<DepthTestMode>,
                                stencilMode     : IMod<StencilMode>,
                                writeBuffers    : Option<Set<Symbol>>) =
            pipelineCache.Create(pass, program, inputs, geometryMode, fillMode, cullMode, blendMode, depthTest, stencilMode, writeBuffers)

        member x.CreateIsActive(active : IMod<bool>) =
            isActiveCache.Create(active) :> AdaptiveResource<_,_>
