namespace Aardvark.Rendering.GL

open System
open System.Threading
open Aardvark.Base
open System.Collections.Concurrent
open Aardvark.Rendering.GL
open Aardvark.Rendering
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering

[<AutoOpen>]
module ResourceManager =
    
    exception ResourceManagerException of string

    type internal ChangeableResourceDescription<'a> = 
        { dependencies : list<IMod>; 
          updateCPU : unit -> unit; 
          updateGPU : unit -> unit; 
          destroy : unit -> unit; 
          resource : IMod<'a> }


    type IChangeableResource =
        inherit IDisposable
        inherit IAdaptiveObject
        //abstract member Dependencies : list<IMod>
        abstract member UpdateCPU : unit -> unit
        abstract member UpdateGPU : unit -> unit
        abstract member Resource : obj
        
    type ChangeableResource<'a> internal(key : list<obj>, parent : ConcurrentDictionary<list<obj>, obj * int>, desc : ChangeableResourceDescription<'a>) as this =
        inherit AdaptiveObject()

        do desc.dependencies |> List.iter (fun a -> a.GetValue() |> ignore; a.AddOutput this)
           this.OutOfDate <- false

        let mutable isDisposed = false
        

        //member x.Dependencies = desc.dependencies
        member x.UpdateCPU() = 
            desc.dependencies |> List.iter (fun a -> a.GetValue() |> ignore)
            desc.updateCPU()

        member x.UpdateGPU() = 
            x.EvaluateIfNeeded () (fun () ->
                x.UpdateCPU()
                desc.updateGPU()
            )

        member x.Resource = desc.resource

        member x.Dispose() =
            if parent = null then
                isDisposed <- true
                desc.destroy()
                desc.dependencies |> List.iter (fun a -> a.RemoveOutput this)

            elif not isDisposed then
                isDisposed <- true
                let _, r =
                    parent.AddOrUpdate(key,
                        (fun key -> x :> obj, 0),
                        (fun key (o,r) -> o, r - 1)
                    )

                if r = 0 then
                    parent.TryRemove key |> ignore
                    desc.destroy()
                    desc.dependencies |> List.iter (fun a -> a.RemoveOutput this)

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        interface IChangeableResource with
            //member x.Dependencies = desc.dependencies
            member x.UpdateCPU() = x.UpdateCPU()
            member x.UpdateGPU() = x.UpdateGPU()
            member x.Resource = desc.resource :> obj

        internal new(desc) = new ChangeableResource<'a>([], null, desc)

    let outOfDate (c : IChangeableResource) =
        c.OutOfDate

    let subscribeDirty (c : IChangeableResource) (set : System.Collections.Generic.HashSet<IChangeableResource>) (dirty : ref<int>) =
        let f() = dirty := 1; lock set (fun () -> set.Add c |> ignore)
        c.AddMarkingCallback(f)

    [<AutoOpen>]
    module private Caching =
        type ResourceCache() =
            let created = ConcurrentDictionary<list<obj>, obj * int>()

            member x.GetOrAdd(key : list<obj>, create : unit -> ChangeableResourceDescription<'a>) =

                let res,_ = 
                    created.AddOrUpdate(key, 
                        (fun key -> 
                            let desc = create()
                            new ChangeableResource<'a>(key, created, desc) :> obj, 1),
                        fun key (o,r) ->
                            (o,r+1)
                    )

                let res = res |> unbox<ChangeableResource<'a>>

                res

        type NamedResourceCache() =
            let caches = ConcurrentDictionary<Symbol, ResourceCache>()

            member x.Item
                with get (name : Symbol) = caches.GetOrAdd(name, fun name -> ResourceCache())

    [<AutoOpen>]
    module private TypePatterns =


        let (|Array|_|) (t : Type) =
            if t.IsArray then
                let e = t.GetType().GetElementType()
                Array e |> Some
            else
                None

        let (|UntypedArray|_|) (t : Type) =
            if t = typeof<Array> then
                Some UntypedArray
            else
                None

        let (|ValueType|_|) (t : Type) =
            if t.IsValueType then
                Some ValueType
            else
                None

    module SurfaceCompilers =
        open System
        open System.Collections.Generic

        let compilers = Dictionary<Type,Context -> ISurface -> Error<Program>>()

        let registerShaderCompiler (compiler : Context -> 'a -> Error<Program>) =
            compilers.Add ( typeof<'a>, fun ctx s -> compiler ctx (unbox<'a> s) )

        let compileBackendSurface (ctx : Context) (b : BackendSurface) =
            match ctx.TryCompileProgram b.Code with
                | Success s ->
                    let remapSemantic (sem : string) =
                        match b.SemanticMap.TryGetValue (Sym.ofString sem) with
                            | (true, sem) -> sem.ToString()
                            | _ -> sem

                    
                    let getSamplerState (sem : string) =
                        match b.SamplerStates.ContainsKey (Sym.ofString sem) with
                            | true -> Some sem
                            | _ -> None

                    let ub = s.UniformBlocks |> List.map (fun b -> { b with fields = b.fields |> List.map (fun f -> { f with semantic = remapSemantic f.semantic }) })
                    let u = s.Uniforms |> List.map (fun f -> let sem = remapSemantic f.semantic in { f with semantic = sem; samplerState = getSamplerState sem})

                    let uniformGetters =
                        b.Uniforms 
                            |> SymDict.toSeq 
                            |> Seq.map (fun (k,v) -> (k, v :> obj)) 
                            |> SymDict.ofSeq

                    Success { s with UniformBlocks = ub; Uniforms = u; SamplerStates = b.SamplerStates; UniformGetters = uniformGetters }

                | Error e -> Error e

        do registerShaderCompiler compileBackendSurface

        do registerShaderCompiler (fun (ctx : Context) (g : IGeneratedSurface) -> 
            let b = g.Generate ctx.Runtime
            compileBackendSurface ctx b
           )

        let compile (ctx : Context) (s : ISurface) =   
            match compilers |> Seq.tryPick (fun ( KeyValue(k,v) ) -> 
                if k.IsAssignableFrom (s.GetType()) then Some <| v ctx s
                else None) with
             | Some k -> k
             | None -> Error "Unknown surface type. "

    module Sharing =
        open System.Collections.Generic

 
        type IResourceHandler<'d, 'r> =
            abstract member Create : 'd -> IMod<'r>
            abstract member Update : IMod<'r> * 'd -> unit
            abstract member Delete : IMod<'r> -> unit

        type SharedResource<'a>(key : obj, r : 'a) =
            let mutable refCount = 0
            let mutable key = key

            member x.ReferenceCount
                with get() = refCount
                and set c = refCount <- c

            member x.Resource = r

            member x.Key
                with get() = key
                and set (k : obj) = key <- k

        type SharedResourceView<'a> private(resource : SharedResource<'a>, handle : ModRef<'a>) =
            inherit AdaptiveDecorator(handle)
            let mutable resource = resource

            member x.SharedResource
                with get() = resource
                and set v = resource <- v

            member x.Handle = handle
            
            interface IMod with
                member x.IsConstant = false
                member x.GetValue() = handle.GetValue() :> obj

            interface IMod<'a> with
                member x.GetValue() = handle.GetValue()

            new(r : SharedResource<'a>) = SharedResourceView(r, Mod.init r.Resource)

        type SharedResourceHandler<'d, 'r>(create : 'd -> 'r, update : 'r * 'd -> unit, delete : 'r -> unit) =
            let cache = Dict<obj, SharedResource<'r>>()

            let getOrCreate (key : 'd) (creator : 'd -> 'r) =
                let res = 
                    cache.GetOrCreate(key, fun _ ->
                        let b = creator key
                        SharedResource(key,b)
                    )

                res.ReferenceCount <- res.ReferenceCount + 1
                SharedResourceView(res)

            member x.Create(arr : 'd) =
                getOrCreate arr (fun arr ->
                    create arr
                )

            member x.Update(b : SharedResourceView<'r>, arr : 'd) =
                let res = b.SharedResource

                if res.ReferenceCount <= 1 then  
                    // we're the only reference to that resource
                    cache.Remove res.Key |> ignore

                    match cache.TryGetValue arr with
                        | (true, r) ->
                            // there already is a resource for the given array
                            // therefore we destroy the old one
                            delete(res.Resource)

                            // and change the SharedResourceView accordingly
                            r.ReferenceCount <- r.ReferenceCount + 1
                            b.SharedResource <- r
                            transact (fun () -> b.Handle.Value <- r.Resource)
                            
                        | _ ->
                            // no other resource has the same key so we
                            // may simply upload and store the SharedResource at its
                            // new "location"
                            update (res.Resource, arr)
                            cache.[arr] <- res
                            res.Key <- arr
                else
                    res.ReferenceCount <- res.ReferenceCount - 1
                    match cache.TryGetValue arr with
                        | (true, r) ->
                            r.ReferenceCount <- r.ReferenceCount + 1

                            b.SharedResource <- r
                            transact (fun () -> b.Handle.Value <- r.Resource)

                        | _ ->
                            let r = SharedResource(arr, create arr)
                            r.ReferenceCount <- 1
                            cache.[arr] <- r

                            b.SharedResource <- r
                            transact (fun () -> b.Handle.Value <- r.Resource)

            member x.Delete(b : SharedResourceView<'r>) =
                let res = b.SharedResource

                b.SharedResource <- Unchecked.defaultof<_>
                b.Handle.UnsafeCache <- Unchecked.defaultof<_>

                if res.ReferenceCount <= 1 then
                    delete res.Resource
                    cache.Remove(res.Key) |> ignore
                else
                    res.ReferenceCount <- res.ReferenceCount - 1

            interface IResourceHandler<'d, 'r> with
                member x.Create(data) = x.Create(data) :> IMod<_>
                member x.Update(res, data) = x.Update(unbox res, data)
                member x.Delete(res) = x.Delete(unbox res)

        type NopResourceHandler<'d, 'r>(create : 'd -> 'r, update : 'r * 'd -> unit, delete : 'r -> unit) =
            interface IResourceHandler<'d, 'r> with
                member x.Create(data) = data |> create |> Mod.constant
                member x.Update(res, data) = update (res.GetValue(), data)
                member x.Delete(res) = delete (res.GetValue())


    [<AllowNullLiteral>]
    type ResourceManager(original : ResourceManager, ctx : Context, shareTextures : bool, shareBuffers : bool) =
        static let semanticIndices = ConcurrentDictionary<Symbol, int>()
        static let mutable currentId = -1

        static let getSemanticIndex (sem : Symbol) =
            semanticIndices.GetOrAdd(sem, fun s ->
                let id = Interlocked.Increment(&currentId)
                id
            )

        // some identifiers for caches
        static let arrayBuffer = Sym.ofString "ArrayBuffer"
        static let dataBuffer = Sym.ofString "DataBuffer"
        static let pixTexture = Sym.ofString "PixTexture"
        static let program = Sym.ofString "Program"
        static let uniformBuffer = Sym.ofString "UniformBuffer"
        static let sampler = Sym.ofString "Sampler"
        static let vao = Sym.ofString "VertexArrayObject"


        let bufferHandler = 
            if shareBuffers then 
                let originalHandler = 
                    match original with
                        | null -> None
                        | o when not o.ShareBuffers -> None
                        | o -> Some o.BufferHandler

                match originalHandler with
                    | Some h -> h
                    | None -> Sharing.SharedResourceHandler<Array, Buffer>(ctx.CreateBuffer, ctx.Upload, ctx.Delete) :> Sharing.IResourceHandler<_,_>
            else 
                Sharing.NopResourceHandler<Array, Buffer>(ctx.CreateBuffer, ctx.Upload, ctx.Delete) :> Sharing.IResourceHandler<_,_>

        let textureHandler = 
            if shareTextures then 
                let originalHandler = 
                    match original with
                        | null -> None
                        | o when not o.ShareTextures -> None
                        | o -> Some o.TextureHandler

                match originalHandler with
                    | Some h -> h
                    | None -> Sharing.SharedResourceHandler<ITexture, Texture>(ctx.CreateTexture, ctx.Upload, ctx.Delete) :> Sharing.IResourceHandler<_,_>
            else 
                Sharing.NopResourceHandler<ITexture, Texture>(ctx.CreateTexture, ctx.Upload, ctx.Delete) :> Sharing.IResourceHandler<_,_>


        // the overall cache holding caches per identifier
        let cache = 
            match original with
                | null -> NamedResourceCache()
                | o -> o.Cache

        let compile (s : ISurface) = SurfaceCompilers.compile ctx s

        let volatileSubscribtion (m : IMod) (cb : (unit -> unit) -> unit) : IDisposable =
            let f = ref Unchecked.defaultof<_>
            let life = ref true

            let subscribe() =
                if !life then
                    lock m (fun () ->
                        m.MarkingCallbacks.Add !f |> ignore
                    )

            f := fun () ->
                cb(subscribe)

            subscribe()

            { new IDisposable with
                member x.Dispose() =
                    life := false
                    m.MarkingCallbacks.Remove !f |> ignore
            }
           
        member private x.BufferHandler = bufferHandler
        member private x.TextureHandler = textureHandler 
        member private x.Original = original
        member private x.Cache = cache
        member x.ShareBuffers = shareBuffers
        member x.ShareTextures = shareTextures 
        member x.Context = ctx

        /// <summary>
        /// creates a buffer from a mod-array simply updating its
        /// content whenever the array changes
        /// </summary>
        member x.CreateBuffer<'a when 'a : struct>(data : IMod<'a[]>) =
            cache.[arrayBuffer].GetOrAdd(
                [data], 
                fun () ->
                    let current = data.GetValue()
                    let handle = bufferHandler.Create(current)
                    { dependencies = [data]
                      updateCPU = fun () -> data.GetValue() |> ignore
                      updateGPU = fun () -> bufferHandler.Update(handle, data.GetValue())
                      destroy = fun () -> bufferHandler.Delete(handle)
                      resource = handle } 
            )

        /// <summary>
        /// creates a buffer from a mod-array simply updating its
        /// content whenever the array changes
        /// </summary>
        member x.CreateBuffer(data : IBuffer) =
            cache.[arrayBuffer].GetOrAdd(
                [data], 
                fun () ->
                    match data with
                        | :? ArrayBuffer as buffer ->
                            let data = buffer.Data
                            let current = data.GetValue()
                            let handle = bufferHandler.Create(current)
                            { dependencies = [data]
                              updateCPU = fun () -> data.GetValue() |> ignore
                              updateGPU = fun () -> bufferHandler.Update(handle, data.GetValue())
                              destroy = fun () -> bufferHandler.Delete(handle)
                              resource = handle }
                        | _ ->
                            failwithf "unknown buffer-data: %A" data
            )

        /// <summary>
        /// creates a buffer from an untyped mod using one of the
        /// available overloads if possible. raises a ResourceManagerException
        /// if the given mod cannot be transformed into a buffer.
        /// </summary>       
        member x.CreateBuffer(data : IMod) =
            let t = data.GetType()
            match t with

                | ModOf(Array(ValueType as t)) ->
                    let m = typeof<ResourceManager>.GetMethod("CreateBuffer", [|typedefof<IMod<_>>.MakeGenericType [|t|] |])
                    m.Invoke(x, [|data :> obj|]) |> unbox<ChangeableResource<Buffer>>

                | ModOf(UntypedArray) ->
                    x.CreateBuffer(ArrayBuffer ( data |> unbox<IMod<Array>> ) )

                | _ ->
                    raise <| ResourceManagerException(sprintf "failed to create buffer for type: %A" t.FullName)
                    
     
        member x.CreateTexture(data : IMod<ITexture>) =
            cache.[pixTexture].GetOrAdd(
                [data],
                fun () ->
                    let current = data.GetValue()

                    let created = ref false
                    let handle = 
                        match current with
                            | :? Texture as t -> ref (Mod.constant t)
                            | _ -> 
                                created := true
                                ref <| textureHandler.Create(current)

                    let handleMod = Mod.init !handle

                    { dependencies = [data]
                      updateCPU = fun () -> data.GetValue() |> ignore
                      updateGPU = fun () -> 
                        match data.GetValue() with
                            | :? Texture as t -> 
                                if !created then
                                    textureHandler.Delete(!handle)
                                    created := false

                                let h = Mod.constant t
                                handle := h
                                transact (fun () -> handleMod.Value <- h)
                            | _ -> 
                                if !created then
                                    textureHandler.Update(!handle, data.GetValue())
                                else
                                    created := true
                                    handle := textureHandler.Create(current)

                                if handleMod.Value <> !handle then 
                                    transact (fun () -> handleMod.Value <- !handle)

                      destroy = fun () -> if !created then textureHandler.Delete(!handle)
                      resource = handleMod |> Mod.bind id }            
            )

        member x.CreateSurface (s : IMod<ISurface>) =
           cache.[program].GetOrAdd(
                [s],
                fun () ->
                    let current = s.GetValue()
                    let compileResult = compile current

                    match compileResult with
                        | Success p ->
                            let handle = Mod.init p

                            { dependencies = [s]
                              updateCPU = fun () -> 
                                match compile <| s.GetValue() with
                                    | Success p -> Mod.change handle p
                                    | Error e -> Log.warn "could not update surface: %A" e

                              updateGPU = fun () -> ()
                              destroy = fun () -> ctx.Delete(p)
                              resource = handle }         
                        | Error e ->
                            failwith e
           )
                
        member x.CreateUniformBuffer (scope : Ag.Scope, layout : UniformBlock, program : Program, u : IUniformProvider, semanticValues : byref<list<string * IMod>>) =
            let getValue (f : ActiveUniform) =
                let sem = f.semantic |> Sym.ofString

                match u.TryGetUniform (scope, sem) with
                    | Some m -> m
                    | _ ->
                        match program.UniformGetters.TryGetValue sem with
                            | (true, (:? IMod as m)) -> m
                            | _ ->
                                failwithf "could not find uniform: %A" f

            let fieldValues = layout.fields |> List.map (fun f -> f, getValue f)
            semanticValues <- fieldValues |> List.map (fun (f,v) -> f.semantic, v)
            let values = fieldValues |> List.map snd


            cache.[uniformBuffer].GetOrAdd(
                (layout :> obj)::(values |> List.map (fun v -> v :> obj)),
                fun () ->

                    let b = ctx.CreateUniformBuffer(layout)
                    
                    let writers = fieldValues |> List.map (fun (u,m) -> m, b.CompileSetter (Sym.ofString u.name) m)
  
                    
                    let dirty = System.Collections.Generic.List()
                    let subscriptions =
                        writers |> List.map (fun (m,w) ->
                            w()
                            volatileSubscribtion m (fun s -> lock dirty (fun () -> dirty.Add (m, w, s)))
                        )

                    ctx.Upload(b)

                    { dependencies = values
                      updateCPU = fun () ->
                        let d =
                            lock dirty (fun () ->
                                let res = dirty |> Seq.toList
                                dirty.Clear()
                                res
                            )

                        if not <| List.isEmpty d then
                            for (m,w,s) in d do
                                w(); s();
                            

                      updateGPU = fun () -> ctx.Upload(b)
                      destroy = fun () -> 
                        subscriptions |> List.iter (fun d -> d.Dispose())
                        dirty.Clear()
                        ctx.Delete(b)
                      resource = Mod.constant b }    
            )

        member x.CreateUniformLocation(scope : Ag.Scope, u : IUniformProvider, uniform : ActiveUniform) =
            match u.TryGetUniform (scope, Sym.ofString uniform.semantic) with
                | Some v ->
                    cache.[uniformBuffer].GetOrAdd(
                        [uniform :> obj; v :> obj],
                        fun () ->
                            let loc = ctx.CreateUniformLocation(uniform.uniformType.SizeInBytes, uniform.uniformType)

                            let write = loc.CompileSetter(v)
                            let writer = [v :> IAdaptiveObject] |> Mod.mapCustom (fun _ -> write())
                            
                            writer.GetValue()

                            { dependencies = [v]
                              updateCPU = fun () ->
                                writer.GetValue()

                              updateGPU = fun () -> ()
                              destroy = fun () -> ()
                              resource = Mod.constant loc }    
                    )
                | _ ->
                    failwithf "could not get uniform: %A" uniform

        member x.CreateSampler (sam : IMod<SamplerStateDescription>) =
            cache.[sampler].GetOrAdd(
                [sam], 
                fun () ->
                    let current = sam.GetValue()
                    let handle = ctx.CreateSampler(current)

                    { dependencies = [sam]
                      updateCPU = fun () -> sam.GetValue() |> ignore
                      updateGPU = fun () -> ctx.Update(handle, sam.GetValue())
                      destroy = fun () -> ctx.Delete(handle)
                      resource = Mod.constant handle }
            )

        member x.CreateVertexArrayObject (bindings : list<int * IMod<AttributeDescription>>, index : ChangeableResource<Buffer>) =
            cache.[vao].GetOrAdd(
                [bindings; index], 
                fun () ->

                    let handle = ctx.CreateVertexArrayObject(index.Resource.GetValue(), bindings |> List.map (fun (i,r) -> i, r |> Mod.force))
                    let attributes = bindings |> List.map snd |> List.map (fun b -> b :> IMod)

                    { dependencies = (index.Resource :> IMod)::attributes
                      updateCPU = fun () -> ()
                      updateGPU = fun () -> ctx.Update(handle, index.Resource.GetValue(), bindings |> List.map (fun (i,r) -> i, r |> Mod.force))
                      destroy = fun () -> ctx.Delete(handle)
                      resource = Mod.constant handle }
            )

        member x.CreateVertexArrayObject (bindings : list<int * IMod<AttributeDescription>>) =
            cache.[vao].GetOrAdd(
                [bindings], 
                fun () ->

                    let handle = ctx.CreateVertexArrayObject(bindings |> List.map (fun (i,r) -> i, r |> Mod.force))
                    let attributes = bindings |> List.map snd |> List.map (fun b -> b :> IMod)

                    { dependencies = attributes
                      updateCPU = fun () -> ()
                      updateGPU = fun () -> ctx.Update(handle,bindings |> List.map (fun (i,r) -> i, r |> Mod.force))
                      destroy = fun () -> ctx.Delete(handle)
                      resource = Mod.constant handle }
            )

        member x.CreateVertexArrayObject (bindings : list<int * IMod<AttributeDescription>>, index : Option<ChangeableResource<Buffer>>) =
            match index with
                | Some index -> x.CreateVertexArrayObject(bindings, index)
                | None -> x.CreateVertexArrayObject(bindings)

        member x.CreateTexture(size : IMod<V2i>, mipLevels : IMod<int>, format : IMod<PixFormat>, samples : IMod<int>) : ChangeableResource<Texture> =
            let handle = ctx.CreateTexture2D(size.GetValue(), mipLevels.GetValue(), ChannelType.ofPixFormat <| format.GetValue(), samples.GetValue())
            
            let desc =
                { dependencies = [size :> IMod; format :> IMod; samples :> IMod]
                  updateCPU = fun () -> ()
                  updateGPU = fun () -> ctx.UpdateTexture2D(handle, size.GetValue(), mipLevels.GetValue(), ChannelType.ofPixFormat <| format.GetValue(), samples.GetValue())
                  destroy = fun () -> ctx.Delete(handle)
                  resource = Mod.constant handle }

            new ChangeableResource<Texture>(desc)

        member x.CreateRenderbuffer(size : IMod<V2i>, format : IMod<RenderbufferFormat>, samples : IMod<int>) : ChangeableResource<Renderbuffer> =
            let handle = ctx.CreateRenderbuffer(size.GetValue(), format.GetValue(), samples.GetValue())
            
            let desc =
                { dependencies = [size :> IMod; format :> IMod; samples :> IMod]
                  updateCPU = fun () -> ()
                  updateGPU = fun () -> ctx.Update(handle, size.GetValue(), format.GetValue(), samples.GetValue())
                  destroy = fun () -> ctx.Delete(handle)
                  resource = Mod.constant handle }

            new ChangeableResource<Renderbuffer>(desc)

        member x.CreateFramebuffer(bindings : list<Symbol * IMod<IFramebufferOutput>>) : ChangeableResource<Aardvark.Rendering.GL.Framebuffer> =
            let dict = SymDict.ofList bindings


            let toInternal (bindings : list<Symbol * IMod<IFramebufferOutput>>) =
                
                let depth =
                    match dict.TryGetValue DefaultSemantic.Depth with
                        | (true, d) ->
                            Some <| d.GetValue()
                        | _ ->
                            None

                let colors = bindings |> List.filter (fun (s,b) -> s <> DefaultSemantic.Depth) |> List.map (fun (s,o) -> getSemanticIndex s, s, (o.GetValue()))

                colors,depth

            let c,d = toInternal bindings
            let handle = ctx.CreateFramebuffer(c, d)

            let desc =
                { dependencies = bindings |> Seq.map (fun (_,v) -> v :> IMod) |> Seq.toList
                  updateCPU = fun () -> ()
                  updateGPU = fun () -> 
                    let c,d = toInternal bindings
                    ctx.Update(handle, c, d)
                  destroy = fun () -> ctx.Delete(handle)
                  resource = Mod.constant handle }

            new ChangeableResource<Aardvark.Rendering.GL.Framebuffer>(desc)

        new(ctx, shareTextures, shareBuffers) = ResourceManager(null, ctx, shareTextures, shareBuffers)