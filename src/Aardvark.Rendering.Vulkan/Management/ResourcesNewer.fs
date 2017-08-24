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

module ResourcesNew =
    open System.Threading
    open System.Collections.Generic

    let mutable private currentEpoch = 0L
    let newEpoch() = Interlocked.Increment(&currentEpoch)

    type ResourceUpdateToken =
        class
            val mutable public MaxEpoch : int64
            val mutable public RenderToken : RenderToken

            member x.AddEpoch(e : int64) =
                x.MaxEpoch <- max x.MaxEpoch e

            new(e : int64, rt : RenderToken) = { MaxEpoch = e; RenderToken = rt }
        end

    type IResource =
        inherit IAdaptiveObject
        abstract member Acquire : unit -> unit
        abstract member Release : IAdaptiveObject -> unit
        abstract member Update : AdaptiveToken * ResourceUpdateToken -> hset<ILockedResource>
        abstract member CurrentLocks : hset<ILockedResource>

    type IResource<'h> =
        inherit IResource
        abstract member GetHandle : AdaptiveToken * ResourceUpdateToken -> 'h * hset<ILockedResource>
        
    type IResource<'h, 'n when 'n : unmanaged> =
        inherit IResource<'h>
        abstract member Pointer : nativeptr<'n>

    [<AbstractClass>]
    type AbstractResource<'h, 'n when 'n : unmanaged>() =
        inherit AdaptiveObject()

        let mutable cache = Unchecked.defaultof<'h>
        let mutable locked = HSet.empty
        let mutable refCount = 0

        let mutable pointer : nativeptr<'n> = NativePtr.zero
        let mutable epoch = -1L

        abstract member Compute : AdaptiveToken * ResourceUpdateToken -> 'h * hset<ILockedResource>
        abstract member Destroy : 'h -> unit
        abstract member View : 'h -> 'n
        default x.View _ = Unchecked.defaultof<'n>

        member x.HandleChanged() =
            epoch <- newEpoch()

        member x.GetHandle(token : AdaptiveToken, rt : ResourceUpdateToken) =
            x.EvaluateAlways token (fun token ->
                if x.OutOfDate then
                    let (h,l) = x.Compute(token, rt)
                    rt.AddEpoch epoch
                    NativePtr.write pointer (x.View h)
                    cache <- h 
                    locked <- l
                    (h,l)
                else
                    rt.AddEpoch epoch
                    (cache, locked)
            )
        
        member x.Update(token : AdaptiveToken, rt : ResourceUpdateToken) =
            x.GetHandle(token, rt) |> snd

        member x.Acquire() =
            if Interlocked.Increment(&refCount) = 1 then
                pointer <- NativePtr.alloc 1

        member x.Release(caller : IAdaptiveObject) =
            if Interlocked.Decrement(&refCount) = 0 then
                lock x (fun () ->
                    let mutable foo = 0
                    x.Outputs.Consume(&foo) |> ignore
                    x.OutOfDate <- true
                    let h = cache
                    cache <- Unchecked.defaultof<_>
                    locked <- HSet.empty
                    NativePtr.free pointer
                    pointer <- NativePtr.zero
                    x.Destroy h
                )
            else
                lock x (fun () ->
                    if not (isNull caller) then
                        x.Outputs.Remove(caller) |> ignore
                )

        member x.Pointer = pointer

        interface IResource with
            member x.CurrentLocks = locked
            member x.Acquire() = x.Acquire()
            member x.Release(caller) = x.Release(caller)
            member x.Update(t,rt) = x.Update(t,rt)

        interface IResource<'h> with
            member x.GetHandle(t,rt) = x.GetHandle(t,rt)

        interface IResource<'h, 'n> with
            member x.Pointer = pointer




    type ResourceDescription<'a, 'h> =
        {
            create : 'a -> 'h
            delete : 'h -> unit
            tryUpdate : ResourceUpdateToken -> 'h -> 'a -> bool
        }
        
    type ResourceDescription<'a, 'h, 'n when 'n : unmanaged> =
        {
            create : 'a -> 'h
            delete : 'h -> unit
            tryUpdate : ResourceUpdateToken -> 'h -> 'a -> bool
            view : 'h -> 'n
        }

    type AdaptiveResourceDescription<'a, 'h> =
        {
            acreate : AdaptiveToken -> RenderToken -> 'a -> 'h
            adelete : 'h -> unit
            atryUpdate : AdaptiveToken -> RenderToken -> 'h -> 'a -> bool
        }

    type ResourceCache<'a, 'h, 'n when 'n : unmanaged>(init : IDisposable -> 'a -> AbstractResource<'h, 'n>) =
        let store = System.Collections.Concurrent.ConcurrentDictionary<list<obj>, AbstractResource<'h, 'n>>()

        member private x.CreateResource(input : 'a, additional : list<obj>) =
            let key = (input :> obj) :: additional
            let kill = { new IDisposable with member x.Dispose() = store.TryRemove key |> ignore }
            let res = store.GetOrAdd(key, fun _ -> init kill input)
            res

        member x.Create(input : 'a) =
            x.CreateResource(input, []) :> IResource<_,_>

    type ResourceSet() =
        inherit AdaptiveObject()

        let all = ReferenceCountingSet<IResource>()
        let dirty = HashSet<IResource>()
        let locks = ReferenceCountingSet<ILockedResource>()
        let knownLocks = Dict<IResource, ref<hset<ILockedResource>>>()


        let setLocks (r : IResource) (newLocks : hset<ILockedResource>) =
            let r = knownLocks.GetOrCreate(r, fun _ -> ref HSet.empty)
            let old = !r

            let delta = HSet.computeDelta old newLocks
            for d in delta do
                match d with
                    | Add(_,v) -> locks.Add v |> ignore
                    | Rem(_,v) -> locks.Remove v |> ignore

            r := newLocks

        let mutable maxEpoch = -1L

        let consumeDirty() =
            lock dirty (fun () ->
                let arr = Seq.toArray dirty
                dirty.Clear()
                arr
            )

        override x.InputChanged(_,o) =
            match o with
                | :? IResource as o ->
                    lock dirty (fun () -> dirty.Add o |> ignore)
                | _ ->
                    ()

        member x.Locks = locks :> ISet<_>

        member x.Add(r : IResource) =
            lock x (fun () ->
                if all.Add r then
                    lock r (fun () ->
                        r.Acquire()

                        if r.OutOfDate then 
                            lock dirty (fun () -> dirty.Add r |> ignore)
                        else 
                            locks.UnionWith r.CurrentLocks
                            r.Outputs.Add x |> ignore
                    )
                    maxEpoch <- newEpoch()
            )

        member x.Remove(r : IResource) =
            lock x (fun () ->
                if all.Remove r then
                    match knownLocks.TryRemove r with
                        | (true, l) -> locks.ExceptWith !l
                        | _ -> ()

                    lock dirty (fun () -> dirty.Remove r |> ignore)
                    lock r (fun () ->
                        r.Release(x)
                    )
                    maxEpoch <- newEpoch()
            )

        member x.Update(token : AdaptiveToken, rt : RenderToken) =
            x.EvaluateAlways token (fun token ->
                if x.OutOfDate then
                    let m = maxEpoch
                    let rec doit() =
                        let dirty = consumeDirty()
                        if dirty.Length > 0 then
                            let delayed = List<IResource>(dirty.Length)

                            let rt = ResourceUpdateToken(maxEpoch, rt)
                            for d in dirty do
                                match d with
                                    | :? IResource<DrawCall> ->
                                        let n = d.Update(token, rt)
                                        setLocks d n
                                    | _ ->
                                        delayed.Add d

                            for d in delayed do
                                let n = d.Update(token, rt)
                                setLocks d n

                            let m = maxEpoch
                            maxEpoch <- rt.MaxEpoch
                            doit()

                    doit()
                    m <> maxEpoch
                else
                    false
            )

    [<AbstractClass; Sealed>]
    type ResourceCache private() =
        //static member Create<'a, 'h>(desc : ResourceDescription<'a, 'h>) =

        static member Create<'a, 'h, 'n when 'n : unmanaged>(desc : ResourceDescription<'a, 'h, 'n>) =
            ResourceCache<IMod<'a>, 'h, 'n>(fun witness input ->
                
                let mutable ownsHandle = true
                let mutable old : Option<'a * 'h> = None
                let mutable locks = HSet.empty

                let updateLocks (value : 'a) =
                    let ll = 
                        match value :> obj with 
                            | :? ILockedResource as l -> HSet.ofList [l]
                            | _ -> HSet.empty
                    locks <- ll
                    ll

                let create (value : 'a) =
                    match value :> obj with
                        | :? 'h as h -> 
                            ownsHandle <- false
                            h
                        | _ -> 
                            ownsHandle <- true
                            desc.create value

                { new AbstractResource<'h, 'n>() with

                    override x.View h = desc.view h

                    override x.Compute(t, rt) =
                        let value = input.GetValue t

                        match old with
                            | Some (a,h) when Unchecked.equals a value ->
                                (h, locks)

                            | Some (_,o) ->
                                let l = updateLocks value
                                if ownsHandle && desc.tryUpdate rt o value then
                                    (o, l)
                                else
                                    x.HandleChanged()
                                    if ownsHandle then desc.delete o
                                    let n = create value
                                    old <- Some(value, n)
                                    (n, l)

                            | None ->
                                x.HandleChanged()
                                let l = updateLocks value
                                let n = create value
                                old <- Some(value, n)
                                (n, l)
                                
                    override x.Destroy h = 
                        if ownsHandle then desc.delete h
                        old <- None
                        locks <- HSet.empty
                        ownsHandle <- true
                        witness.Dispose()
                }
            )

        static member Create<'a, 'b, 'h, 'n when 'n : unmanaged>(desc : ResourceDescription<'a * 'b, 'h, 'n>) =
            ResourceCache<IMod<'a> * 'b, 'h, 'n>(fun witness (input,konst) ->
                
                let mutable ownsHandle = true
                let mutable old : Option<'a * 'h> = None
                let mutable locks = HSet.empty

                let updateLocks (value : 'a) =
                    let ll = 
                        match value :> obj with 
                            | :? ILockedResource as l -> HSet.ofList [l]
                            | _ -> HSet.empty
                    locks <- ll
                    ll

                let create (value : 'a * 'b) =
                    match value :> obj with
                        | :? 'h as h -> 
                            ownsHandle <- false
                            h
                        | _ -> 
                            ownsHandle <- true
                            desc.create value

                { new AbstractResource<'h, 'n>() with

                    override x.View h = desc.view h

                    override x.Compute(t, rt) =
                        let value = input.GetValue t

                        match old with
                            | Some (a,h) when Unchecked.equals a value ->
                                (h, locks)

                            | Some (_,o) ->
                                let l = updateLocks value
                                if ownsHandle && desc.tryUpdate rt o (value, konst) then
                                    (o, l)
                                else
                                    x.HandleChanged()
                                    if ownsHandle then desc.delete o
                                    let n = create (value, konst)
                                    old <- Some(value, n)
                                    (n, l)

                            | None ->
                                x.HandleChanged()
                                let l = updateLocks value
                                let n = create (value, konst)
                                old <- Some(value, n)
                                (n, l)
                                
                    override x.Destroy h = 
                        if ownsHandle then desc.delete h
                        old <- None
                        locks <- HSet.empty
                        ownsHandle <- true
                        witness.Dispose()
                }
            )
         
        static member Create<'a, 'h>(desc : ResourceDescription<'a, 'h>) =
            ResourceCache.Create {
                create = desc.create
                tryUpdate = desc.tryUpdate
                delete = desc.delete
                view = fun _ -> 0
            }

        static member CreateCustom<'a, 'h, 'n when 'n : unmanaged>(init : IDisposable -> 'a -> AbstractResource<'h, 'n>) =
            ResourceCache<'a, 'h, 'n>(init)

    type UniformBufferCache(device : Device) =
        let cache = System.Collections.Concurrent.ConcurrentDictionary<list<obj>, AbstractResource<UniformBuffer, VkBuffer>>()
        
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

            cache.GetOrAdd(key, fun _ ->
                let writers = 
                    values |> List.map (fun (target, m) ->
                        match m.GetType() with
                            | ModOf tSource -> m, UniformWriters.getWriter target.offset target.fieldType tSource
                            | _ -> failf ""
                    )

                let mutable current = None

                    
                { new AbstractResource<UniformBuffer, VkBuffer>() with
                    override x.View h = h.Handle

                    override x.Compute(t, rt) =
                        let buffer = 
                            match current with
                                | Some c -> c
                                | None -> 
                                    let c = device.CreateUniformBuffer(layout)
                                    current <- Some c
                                    c

                        for (m,w) in writers do
                            w.Write(t, m, buffer.Storage.Pointer)
                        device.Upload(buffer)
                        buffer, HSet.empty

                    override x.Destroy h =
                        device.Delete h
                }

            ) :> IResource<_>



    type AdaptiveDescriptor =
        | AdaptiveUniformBuffer of int * IResource<UniformBuffer>
        | AdaptiveCombinedImageSampler of int * array<Option<IResource<ImageView, VkImageView> * IResource<Sampler, VkSampler>>>

    

    type ResourceManager(device : Device) =
        let descriptorPool = device.CreateDescriptorPool(1 <<< 20, 1 <<< 22)
        do device.OnDispose.Add (fun () -> device.Delete descriptorPool)
        
        let programCache = 
            ResourceCache.Create<ISurface, RenderPass, ShaderProgram, _> {
                create      = fun (s,p) -> device.CreateShaderProgram(p, s)
                tryUpdate   = fun _ _ _ -> false
                delete      = fun h -> device.Delete h
                view        = fun p -> 0
            }

        let pipelineCache =
            ResourceCache.CreateCustom <| 
                fun (witness : IDisposable) 
                    (pass            : RenderPass, 
                     program         : IResource<ShaderProgram>,
                     inputs          : Map<Symbol, bool * Aardvark.Base.BufferView>,
                     geometryMode    : IMod<IndexedGeometryMode>,
                     fillMode        : IMod<FillMode>,
                     cullMode        : IMod<CullMode>,
                     blendMode       : IMod<BlendMode>,
                     depthTest       : IMod<DepthTestMode>,
                     stencilMode     : IMod<StencilMode>,
                     writeBuffers    : Option<Set<Symbol>>) ->


                    let writeBuffers =
                        match writeBuffers with
                            | Some set -> 
                                if Set.isSuperset set pass.Semantics then pass.Semantics
                                else set
                            | None ->
                                pass.Semantics
                    let anyAttachment = 
                        match pass.ColorAttachments |> Map.toSeq |> Seq.tryHead with
                            | Some (_,(_,a)) -> a
                            | None -> pass.DepthStencilAttachment |> Option.get

                    let writeMasks = Array.zeroCreate pass.ColorAttachmentCount
                    for (i, (sem,_)) in Map.toSeq pass.ColorAttachments do 
                        if Set.contains sem writeBuffers then writeMasks.[i] <- true
                        else writeMasks.[i] <- false

                    let writeDepth = Set.contains DefaultSemantic.Depth writeBuffers
                    
                    let prog, _ = program.GetHandle(AdaptiveToken.Top, ResourceUpdateToken(-1L, RenderToken.Empty))
                    let usesDiscard = prog.HasDiscard
                    let vertexInputState = VertexInputState.create inputs
                    let inputAssembly = Mod.map InputAssemblyState.ofIndexedGeometryMode geometryMode
                    let rasterizerState = Mod.custom (fun self -> RasterizerState.create usesDiscard (depthTest.GetValue self) (cullMode.GetValue self) (fillMode.GetValue self))
                    let colorBlendState = Mod.map (ColorBlendState.create writeMasks pass.ColorAttachmentCount) blendMode
                    let multisampleState = MultisampleState.create prog.SampleShading anyAttachment.samples
                    let depthState = Mod.map (DepthState.create writeDepth) depthTest
                    let stencilState = Mod.map StencilState.create stencilMode

                    let mutable old : Option<Pipeline> = None

                    program.Acquire()
                    { new AbstractResource<Pipeline, VkPipeline>() with
                        override x.View h = h.Handle
                        override x.Compute(t, rt) =
                            let prog, l0 = program.GetHandle(t, rt)
                            let usesDiscard = prog.HasDiscard
                            let desc =
                                {

                                    renderPass              = pass
                                    shaderProgram           = prog
                                    vertexInputState        = vertexInputState
                                    inputAssembly           = inputAssembly.GetValue t
                                    rasterizerState         = rasterizerState.GetValue t
                                    colorBlendState         = colorBlendState.GetValue t
                                    multisampleState        = multisampleState
                                    depthState              = depthState.GetValue t
                                    stencilState            = stencilState.GetValue t
                                    dynamicStates           = [| VkDynamicState.Viewport; VkDynamicState.Scissor |]
                                }

                            match old with
                                | Some o -> device.Delete o
                                | None -> ()

                            x.HandleChanged()
                            let n = device.CreateGraphicsPipeline desc

                            n, l0
                        override x.Destroy h =
                            program.Release x
                            witness.Dispose()
                            device.Delete h
                            old <- None
                    }
                

        let vertexBufferCache =
            let usage = VkBufferUsageFlags.VertexBufferBit ||| VkBufferUsageFlags.TransferDstBit
            ResourceCache.Create<IBuffer, Buffer, VkBuffer> {
                create      = fun d -> device.CreateBuffer(usage, d)
                tryUpdate   = fun _ h d -> h.TryUpdate d
                delete      = fun h -> device.Delete h
                view        = fun h -> h.Handle
            }

        let indexBufferCache =
            let usage = VkBufferUsageFlags.IndexBufferBit ||| VkBufferUsageFlags.TransferDstBit
            ResourceCache.Create<IBuffer, Buffer, VkBuffer> {
                create      = fun d -> device.CreateBuffer(usage, d)
                tryUpdate   = fun _ h d -> h.TryUpdate d
                delete      = fun h -> device.Delete h
                view        = fun h -> h.Handle
            }
            
        let uniformBufferCache =
            UniformBufferCache(device)

        let indirectBufferCache = 
            ResourceCache.Create<IIndirectBuffer, bool, IndirectBuffer, V2l> {
                create      = fun (b,indexed) -> device.CreateIndirectBuffer(indexed, b)
                tryUpdate   = fun _ _ _ -> false
                delete      = fun b -> device.Delete b
                view        = fun h -> V2l(h.Handle.Handle, int64 h.Count)
            }


        let imageCache =
            ResourceCache.Create<ITexture, Image, VkImage> {
                create = fun t -> device.CreateImage(t)
                tryUpdate = fun _ _ _ -> false
                delete = fun h -> device.Delete h
                view = fun h -> h.Handle
            }

        let imageViewCache =
            ResourceCache.CreateCustom <| fun (witness : IDisposable) (img : IResource<Image>) ->
                let mutable old : Option<ImageView> = None

                img.Acquire()
                { new AbstractResource<ImageView, VkImageView>() with
                    override x.Compute(t,rt) =
                        let img, locks = img.GetHandle(t, rt)

                        match old with
                            | Some o when o.Image = img ->
                                o, locks
                            | Some o ->
                                device.Delete o
                                let n = device.CreateImageView(img, VkComponentMapping.Identity)
                                old <- Some n
                                x.HandleChanged()
                                n, locks
                            | None ->
                                let n = device.CreateImageView(img, VkComponentMapping.Identity)
                                old <- Some n
                                x.HandleChanged()
                                n, locks

                    override x.Destroy h =
                        witness.Dispose()
                        img.Release x
                        old <- None

                    override x.View h = h.Handle
                }

        let samplerCache =
            ResourceCache.Create<SamplerStateDescription, Sampler, VkSampler> {
                create = fun s -> device.CreateSampler(s)
                tryUpdate = fun _ _ _ -> false
                delete = fun h -> device.Delete h
                view = fun h -> h.Handle
            }

        let descriptorSetCache =
            ResourceCache.CreateCustom <| fun (witness : IDisposable) (layout : DescriptorSetLayout, bindings : list<AdaptiveDescriptor>) ->
                
                for b in bindings do
                    match b with
                        | AdaptiveUniformBuffer(_,b) -> b.Acquire()
                        | AdaptiveCombinedImageSampler(i,arr) -> 
                            arr |> Array.iter (fun o ->
                                match o with
                                    | Some (i,s) -> i.Acquire(); s.Acquire()
                                    | _ -> ()
                            )
                
                let mutable handle : Option<DescriptorSet> = None
                let mutable lastDescriptors = []

                { new AbstractResource<DescriptorSet, VkDescriptorSet>() with
                    override x.Compute(t,rt) =
                        let handle =
                            match handle with
                                | Some h -> h
                                | None -> 
                                    let h = descriptorPool.Alloc(layout)
                                    handle <- Some h
                                    h

                        let mutable locks = HSet.empty
                        let descriptors =
                            bindings |> List.map (fun d ->
                                match d with
                                    | AdaptiveDescriptor.AdaptiveUniformBuffer(i,b) -> 
                                        let b,l = b.GetHandle(t, rt)
                                        locks <- HSet.union locks l
                                        Descriptor.UniformBuffer(i, b)

                                    | AdaptiveDescriptor.AdaptiveCombinedImageSampler(i, imgs) ->
                                        let imgs = 
                                            imgs |> Array.map (fun d ->
                                                match d with
                                                    | Some (i,s) -> 
                                                        let i, li = i.GetHandle(t,rt)
                                                        let s, ls = s.GetHandle(t,rt)
                                                        locks <- HSet.union locks li
                                                        locks <- HSet.union locks ls

                                                        Some (i, s)
                                                    | None -> 
                                                        None
                                            )
                                        Descriptor.CombinedImageSampler(i, imgs)
                            )
                            
                        if lastDescriptors <> descriptors then
                            lastDescriptors <- descriptors
                            descriptorPool.Update(handle, List.toArray descriptors)
                            x.HandleChanged()

                        handle, locks

                    override x.Destroy h =
                        witness.Dispose()
                        descriptorPool.Free h

                        for b in bindings do
                            match b with
                                | AdaptiveUniformBuffer(_,b) -> b.Release(x)
                                | AdaptiveCombinedImageSampler(i,arr) -> 
                                    arr |> Array.iter (fun o ->
                                        match o with
                                            | Some (i,s) -> i.Release(x); s.Release(x)
                                            | _ -> ()
                                    )
                        
                    override x.View h =
                        h.Handle
                }



        let vertexBufferBindingCache =
            ResourceCache.CreateCustom <| fun (witness : IDisposable) (buffers : list<IResource<Buffer, VkBuffer>>, offsets : list<int64>) ->
                for b in buffers do b.Acquire()
                let buffers = List.toArray buffers
                let offsets = List.toArray offsets
                let mutable handles = Array.zeroCreate buffers.Length

                let mutable old : Option<VertexBufferBinding> = None

                { new AbstractResource<VertexBufferBinding, VertexBufferBinding>() with

                    override x.View h = h

                    override x.Compute(token, rt) =
                        let mutable locks = HSet.empty

                        let newHandles = Array.zeroCreate buffers.Length
                        for i in 0 .. buffers.Length - 1 do
                            let (h,l) = buffers.[i].GetHandle(token, rt)
                            newHandles.[i] <- h
                            locks <- HSet.union l locks

                        match old with
                            | Some o ->
                                if handles <> newHandles then
                                    handles <- newHandles
                                    x.HandleChanged()
                                    o.Dispose()
                                    let binding = new VertexBufferBinding(0, newHandles, offsets)
                                    old <- Some binding
                                    binding, locks
                                else
                                    o, locks
                            | None -> 
                                handles <- newHandles
                                let binding = new VertexBufferBinding(0, newHandles, offsets)
                                old <- Some binding
                                binding, locks
                            

                    override x.Destroy(h) =
                        witness.Dispose()
                        h.Dispose()
                        for b in buffers do b.Release(x)
                }

        let indexBufferBindingCache =
            ResourceCache.CreateCustom <| fun (witness : IDisposable) (b : IResource<Buffer>, indexType : Type) ->
                b.Acquire()
                let mutable oldBuffer = None
                let indexType = VkIndexType.ofType indexType
                { new AbstractResource<IndexBufferBinding, IndexBufferBinding>() with

                    override x.View h = h

                    override x.Compute(token, rt) =
                        let (buffer, locks) = b.GetHandle(token, rt)
                        if oldBuffer <> Some buffer then
                            x.HandleChanged()
                            oldBuffer <- Some buffer

                        let binding = new IndexBufferBinding(buffer.Handle, indexType)
                        binding, locks

                    override x.Destroy(h) =
                        witness.Dispose()
                        b.Release(x)
                }

        let directDrawCallCache =
            ResourceCache.Create<list<DrawCallInfo>, bool, DrawCall, DrawCall> {
                create      = fun (c,i) -> device.CreateDrawCall(i, c)
                tryUpdate   = fun _ _ _ -> false
                delete      = fun c -> c.Dispose()
                view        = id
            }

        let indirectDrawCallCache =
            ResourceCache.CreateCustom <| fun (witness : IDisposable) (calls : IResource<IndirectBuffer>, indexed : bool) ->
                
                let mutable oldBuffer = None
                let mutable old : Option<DrawCall> = None
                calls.Acquire()
                { new AbstractResource<DrawCall, DrawCall>() with

                    override x.View h = h

                    override x.Compute(t, rt) =
                        x.HandleChanged()

                        let b, locks = calls.GetHandle(t, rt)
                        
                        if oldBuffer <> Some b then
                            x.HandleChanged()
                            oldBuffer <- Some b

                        old |> Option.iter (fun d -> d.Dispose())
                        let call = device.CreateDrawCall(indexed, b)
                        old <- Some call
                        call, locks

                    override x.Destroy h =
                        h.Dispose()
                        witness.Dispose()
                        calls.Release(x)
                        old <- None

                }

        let descriptorSetBindingCache =
            ResourceCache.CreateCustom <| fun (witness : IDisposable) (layout : PipelineLayout, sets : list<IResource<DescriptorSet, VkDescriptorSet>>) ->
                
                let mutable oldBuffer = None
                let mutable lastSets = []
                let mutable old : Option<DescriptorSetBinding> = None
                for s in sets do s.Acquire()

                { new AbstractResource<DescriptorSetBinding, DescriptorSetBinding>() with

                    override x.View h = h

                    override x.Compute(t, rt) =
                        let mutable locks = HSet.empty
                        let sets = 
                            sets |> List.map (fun s ->
                                let h,l = s.GetHandle(t, rt)
                                locks <- HSet.union locks l
                                h
                            )

                        if lastSets <> sets then
                            x.HandleChanged()
                            oldBuffer <- Some sets

                        old |> Option.iter (fun d -> d.Dispose())
                        let binding = new DescriptorSetBinding(layout, 0, List.toArray sets)
                        old <- Some binding
                        binding, locks

                    override x.Destroy h =
                        h.Dispose()
                        witness.Dispose()
                        for s in sets do s.Release(x)
                        old <- None

                }

        let isActiveCache =
            ResourceCache.Create<bool, bool, int> {
                create = fun v -> v
                tryUpdate = fun _ _ _ -> false
                delete = ignore
                view = fun v -> if v then 1 else 0
            }

        member x.Device = device

        member x.CreateShaderProgram(pass : RenderPass, surface : IMod<ISurface>) =
            programCache.Create(surface, pass) :> IResource<_>

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
            pipelineCache.Create(pass, program, inputs, geometryMode, fillMode, cullMode, blendMode, depthTest, stencilMode, writeBuffers)


        member x.CreateVertexBuffer(b : IMod<IBuffer>) =
            vertexBufferCache.Create(b)
            
        member x.CreateIndexBuffer(b : IMod<IBuffer>) =
            indexBufferCache.Create(b)

        member x.CreateUniformBuffer(scope : Ag.Scope, layout : UniformBufferLayout, u : IUniformProvider, additional : SymbolDict<IMod>) =
            uniformBufferCache.Create(scope, layout, u, additional)

        member x.CreateIndirectBuffer(b : IMod<IIndirectBuffer>, indexed : bool) =
            indirectBufferCache.Create(b, indexed)


        member x.CreateImage(t : IMod<ITexture>) =
            imageCache.Create(t)

        member x.CreateImageView(t : IResource<Image>) =
            imageViewCache.Create(t)

        member x.CreateSampler(s : IMod<SamplerStateDescription>) =
            samplerCache.Create(s)


        member x.CreateDescriptorSet(layout : DescriptorSetLayout, bindings : list<AdaptiveDescriptor>) =
            descriptorSetCache.Create(layout, bindings)


        member x.CreateDescriptorSetBinding(layout : PipelineLayout, sets : list<IResource<DescriptorSet, VkDescriptorSet>>) =
            descriptorSetBindingCache.Create(layout, sets)

        member x.CreateVertexBufferBinding(buffers : list<IResource<Buffer, VkBuffer>>, offsets : list<int64>) =
            vertexBufferBindingCache.Create(buffers, offsets)

        member x.CreateIndexBufferBinding(b : IResource<Buffer>, t : Type) =
            indexBufferBindingCache.Create(b, t)
            
        member x.CreateDrawCall(calls : IMod<list<DrawCallInfo>>, indexed : bool) =
            directDrawCallCache.Create(calls, indexed)

        member x.CreateDrawCall(calls : IResource<IndirectBuffer>, indexed : bool) =
            indirectDrawCallCache.Create(calls, indexed)

        member x.CreateIsActive(active : IMod<bool>) =
            isActiveCache.Create(active) :> IResource<_,_>
