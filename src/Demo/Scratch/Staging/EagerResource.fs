module EagerResource

open System
open System.Threading
open System.Collections.Generic
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop

open Aardvark.Base
open Aardvark.Base.Incremental

#nowarn "9"

[<AllowNullLiteral>]
type LevelQueueNode<'a> =
    class
        val mutable public Level : int
        val mutable public Content : HashSet<'a>
        val mutable public Prev : LevelQueueNode<'a>
        val mutable public Next : LevelQueueNode<'a>


        new(l,p,n) = { Level = l; Content = HashSet(); Prev = p; Next = n }
    end

type LevelQueue<'a>() =
    let mutable first : LevelQueueNode<'a> = null
    let mutable last : LevelQueueNode<'a> = null
    let store = SortedDictionaryExt<int, LevelQueueNode<'a>>(Comparer<int>.Default)

    member x.IsEmpty : bool =
        isNull first

    member x.Dequeue() : HashSet<'a> =
        assert (not (isNull first))
        let c = first.Content
        first <- first.Next
        if isNull first then last <- null
        else first.Prev <- null
        c


    member x.Add(level : int, value : 'a) : bool =
        let (p,s,n) = SortedDictionary.neighbourhood level store
        match s with
            | Some s -> 
                s.Content.Add value

            | None ->
                let p = match p with | Some(_,p) -> p | _ -> null
                let n = match n with | Some(_,n) -> n | _ -> null

                let node = LevelQueueNode<'a>(level, p, n)
                node.Content.Add value |> ignore
                store.[level] <- node

                if isNull p then first <- node
                else p.Next <- node

                if isNull n then last <- node
                else n.Prev <- node

                true
                
    member x.Remove(level : int, value : 'a) : bool =
        match store.TryGetValue level with
            | (true, node) ->
                if node.Content.Remove value then
                    if node.Content.Count = 0 then
                        let p = node.Prev
                        let n = node.Next

                        if isNull p then first <- n
                        else p.Next <- n

                        if isNull n then last <- p
                        else n.Prev <- p

                        store.Remove level |> ignore

                    true

                else
                    false
            | _ ->
                false



type UpdateKind =
    | Untouched = 0
    | ContentChanged = 1
    | HandleChanged = 2

type IResource =
    inherit IDisposable

    abstract member Level : int
    abstract member Outputs : ref<hset<IResource>>
    abstract member Update : unit -> UpdateKind
    abstract member NeedsUpdate : input : IResource * inputFlags : UpdateKind -> bool
    abstract member LockedInputs : hset<ILockedResource>

    [<CLIEvent>]
    abstract member OnDispose : IEvent<EventHandler, EventArgs>
    abstract member AddReference : unit -> unit

type IResource<'h> =
    inherit IResource
    abstract member Handle : 'h

type INativeResource<'n when 'n : unmanaged> =
    inherit IResource
    abstract member Pointer : nativeptr<'n>


type IUserResource =
    inherit IResource
    abstract member Subscribe : (unit -> unit) -> IDisposable

[<AbstractClass; Sealed; Extension>]
type IResourceExtensions private() =
    [<Extension>]
    static member AddOutput(this : IResource, output : IResource) =
        Interlocked.Change(&this.Outputs.contents, fun o -> HSet.add output o) |> ignore
        
    [<Extension>]
    static member RemoveOutput(this : IResource, output : IResource) =
        Interlocked.Change(&this.Outputs.contents, fun o -> HSet.remove output o) |> ignore




[<AbstractClass>]
type AbstractResource private(level : int, inputs : list<IResource>) as this =
    let outputs = ref HSet.empty

    let onDispose = Event<EventHandler, EventArgs>()

    let mutable level = level
    let mutable needsUpdate = false
    let mutable refCount = 0

    do for i in inputs do
        if i.Level >= level then level <- 1 + i.Level
        i.AddOutput this


    member x.Level = level
    member x.Outputs = outputs

    member x.AddReference() = Interlocked.Increment(&refCount) |> ignore

    abstract member LockedInputs : hset<ILockedResource>
    abstract member InputChanged : input : IResource * inputFlags : UpdateKind -> bool
    abstract member PerformUpdate : unit -> UpdateKind
    abstract member Destroy : unit -> unit
    
    [<CLIEvent>]
    member x.OnDispose = onDispose.Publish

    member x.Update() =
        if needsUpdate then
            needsUpdate <- false
            x.PerformUpdate()
        else
            UpdateKind.Untouched

    member x.NeedsUpdate(input : IResource, inputFlags : UpdateKind) =
        let n = x.InputChanged(input, inputFlags)
        needsUpdate <- needsUpdate || n
        needsUpdate

    member x.AddOutput(o : IResource) =
        outputs := HSet.add o !outputs

    member private x.Dispose(disposing : bool) =
        if disposing then GC.SuppressFinalize x
        else Log.warn "%A was not disposed properly" x

        if Interlocked.Decrement(&refCount) = 0 then
            for i in inputs do i.RemoveOutput x
            outputs := HSet.empty
            onDispose.Trigger(null, null)
            x.Destroy()

    member x.Dispose() = x.Dispose true
    override x.Finalize() = x.Dispose false

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IResource with
        member x.Level = x.Level
        member x.Outputs = x.Outputs
        member x.Update() = x.Update()
        member x.NeedsUpdate(i,f) = x.NeedsUpdate(i,f)
        member x.LockedInputs = x.LockedInputs
        
        [<CLIEvent>]
        member x.OnDispose = x.OnDispose

    new(inputs : seq<IResource>) =
        let inputs = Seq.toList inputs
        new AbstractResource(0, inputs)

    new(level : int) =
        new AbstractResource(level, [])

[<AbstractClass>]
type AbstractResource<'h> =
    class
        inherit AbstractResource

        abstract member Handle : 'h

        interface IResource<'h> with
            member x.Handle = x.Handle

        new(level : int) = { inherit AbstractResource(level) }
        new(inputs : seq<IResource>) = { inherit AbstractResource(inputs) }
    end

[<AbstractClass>]
type AbstractResource<'h, 'n when 'n : unmanaged> =
    class
        inherit AbstractResource<'h>
        
        interface INativeResource<'n> with
            member x.Pointer = x.Pointer

        abstract member Pointer : nativeptr<'n>

        new(level : int) = { inherit AbstractResource<'h>(level) }
        new(inputs : seq<IResource>) = { inherit AbstractResource<'h>(inputs) }
    end


[<AbstractClass>]
type AdaptiveResource() =
    inherit AbstractResource(0)

    let self = AdaptiveObject()
    let callbacks = HashSet<unit -> unit>()

    let marked() =
        lock callbacks (fun () -> 
            for cb in callbacks do cb()
        )

    
    let mutable subscription = None //self.AddMarkingCallback marked

    let subscribe(cb : unit -> unit) =
        lock callbacks (fun () ->
            if callbacks.Add cb then
                if callbacks.Count = 1 then 
                    subscription <- Some (self.AddMarkingCallback marked)
        )

    let unsubscribe(cb : unit -> unit) =
        lock callbacks (fun () ->
            if callbacks.Remove cb then
                if callbacks.Count = 0 then
                    subscription.Value.Dispose()
                    subscription <- None
        )

    member x.DestroyCallbacks() =
        lock callbacks (fun () -> 
            match subscription with
                | Some s -> 
                    s.Dispose()
                    callbacks.Clear()
                | _ ->
                    ()
        )

        

    // abstract member LockedInputs : hset<ILockedResource>
    // abstract member Destroy : unit -> unit
    abstract member PerformUpdate : AdaptiveToken -> UpdateKind

    override x.PerformUpdate() =
        self.EvaluateIfNeeded AdaptiveToken.Top UpdateKind.Untouched (fun t ->
            x.PerformUpdate t
        )

    override x.InputChanged(_,_) =
        failwith "[AdaptiveResource] cannot be output of another resource"

    interface IUserResource with
        member x.Subscribe(cb : unit -> unit) =
            let s = subscribe cb
            lock self (fun () ->
                if self.OutOfDate then cb()
            )

            { new IDisposable with member x.Dispose() = unsubscribe cb }

[<AbstractClass>]
type AdaptiveResource<'h>() =
    inherit AdaptiveResource()

    abstract member Handle : 'h
    interface IResource<'h> with
        member x.Handle = x.Handle

[<AbstractClass>]
type AdaptiveResource<'h, 'n when 'n : unmanaged>() =
    inherit AdaptiveResource<'h>()

    abstract member Pointer : nativeptr<'n>
    interface INativeResource<'n> with
        member x.Pointer = x.Pointer


module NativePtr =
    let create (value : 'a) =
        let ptr = NativePtr.alloc 1
        NativePtr.write ptr value
        ptr

open Aardvark.Rendering.Vulkan

type AdaptiveDescriptor =
    | AdaptiveUniformBuffer of int * IResource<UniformBuffer>
    | AdaptiveCombinedImageSampler of int * array<Option<IResource<ImageView> * IResource<Sampler>>>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AdaptiveDescriptor =
    let resources (d : AdaptiveDescriptor) =
        match d with
            | AdaptiveUniformBuffer(_,r) -> Seq.singleton (r :> IResource)
            | AdaptiveCombinedImageSampler(_,arr) ->
                arr |> Seq.collect (function Some(i,s) -> seq { yield i :> IResource; yield s :> IResource } | _ -> Seq.empty)



type AdaptiveBufferResource(device : Device, usage : VkBufferUsageFlags, input : IMod<IBuffer>) =
    inherit AdaptiveResource<Buffer, VkBuffer>() 

    let mutable pointer = NativePtr.create VkBuffer.Null
    let mutable last : Option<IBuffer * Buffer> = None
    let mutable locks = HSet.empty

    let tryUpdateInPlace (old : Buffer) (n : IBuffer) =
        false

    override x.Handle = 
        match last with
            | Some (_,h) -> h
            | None -> failwith "[Buffer] cannot get handle for disposed buffer"
            
    override x.Pointer = pointer

    override x.LockedInputs = locks

    override x.Destroy() =
        match last with
            | Some (_,b) ->
                x.DestroyCallbacks()
                device.Delete b
                last <- None
                locks <- HSet.empty
                NativePtr.free pointer
                pointer <- NativePtr.zero
            | _ ->
                ()

    override x.PerformUpdate(token : AdaptiveToken) =
        let current = input.GetValue token

        match current with
            | :? ILockedResource as l ->
                locks <- HSet.ofList [l]
            | _ ->
                locks <- HSet.empty

        match last with
            | None ->
                let n = device.CreateBuffer(usage, current)
                NativePtr.write pointer n.Handle
                last <- Some(current, n)
                UpdateKind.HandleChanged

            | Some(old,_) when old = current ->
                UpdateKind.Untouched

            | Some(_,o) ->
                if tryUpdateInPlace o current then
                    UpdateKind.ContentChanged
                else
                    device.Delete o
                    let n = device.CreateBuffer(usage, current)
                    NativePtr.write pointer n.Handle
                    last <- Some(current, n)
                    UpdateKind.HandleChanged

type DescriptorSetResource(pool : DescriptorPool, layout : DescriptorSetLayout, bindings : AdaptiveDescriptor[]) =
    inherit AbstractResource<DescriptorSet, VkDescriptorSet>(bindings |> Seq.collect AdaptiveDescriptor.resources)

    let mutable set = pool.Alloc(layout)
    let mutable pointer = NativePtr.alloc 1
    do NativePtr.write pointer set.Handle

    let write() =
        let descriptors =
            bindings |> Array.map (fun b ->
                match b with
                    | AdaptiveUniformBuffer(slot, b) ->
                        Descriptor.UniformBuffer(slot, b.Handle)
                    | AdaptiveCombinedImageSampler(slot, args) ->
                        let args = args |> Array.map (Option.map (fun (i,s) -> i.Handle, s.Handle))
                        Descriptor.CombinedImageSampler(slot, args)
            )
        pool.Update(set, descriptors)

    do write()

    override x.Handle = set

    override x.Pointer = pointer

    override x.LockedInputs =
        HSet.empty

    override x.Destroy() =
        pool.Free(set)
        set <- Unchecked.defaultof<_>
        NativePtr.free pointer
        pointer <- NativePtr.zero


    override x.InputChanged(input : IResource, kind : UpdateKind) =
        kind = UpdateKind.HandleChanged

    override x.PerformUpdate() =
        write()
        // CMD buffers need to be re-filled when descriptorsets get updated
        UpdateKind.HandleChanged



type ResourceCache() =
    let entries = System.Collections.Concurrent.ConcurrentDictionary<list<obj>, IResource>()

    member x.GetOrCreate(key : list<obj>, create : unit -> #IResource<'h>) =
        let result = 
            entries.GetOrAdd(key, fun _ ->
                create() :> IResource
            )

        result.AddReference()
        result.OnDispose.Add(fun _ -> entries.TryRemove key |> ignore)
        result |> unbox<IResource<'h>>
        
    member x.GetOrCreateNative(key : list<obj>, create : unit -> #INativeResource<'h>) =
        let result = 
            entries.GetOrAdd(key, fun _ ->
                create() :> IResource
            )

        result.AddReference()
        result.OnDispose.Add(fun _ -> entries.TryRemove key |> ignore)
        result |> unbox<INativeResource<'h>>
        

type ResourceManager(device : Device) =
    
    let bufferCache             = ResourceCache()
    let indexBufferCache        = ResourceCache()

    member x.CreateBuffer(input : IMod<IBuffer>) =
        bufferCache.GetOrCreate([input :> obj], fun () -> new AdaptiveBufferResource(device, VkBufferUsageFlags.VertexBufferBit, input))

    member x.CreateIndexBuffer(input : IMod<IBuffer>) =
        indexBufferCache.GetOrCreate([input :> obj], fun () -> new AdaptiveBufferResource(device, VkBufferUsageFlags.IndexBufferBit, input))



    

 
type ResourceState() =
    let locked = HashSet<ILockedResource>()

    member x.ReplaceLocks(o : hset<ILockedResource>, n : hset<ILockedResource>) =
        ()

    member x.AddLocks(o : hset<ILockedResource>) =
        x.ReplaceLocks(HSet.empty, o)

    member x.RemoveLocks(o : hset<ILockedResource>) =
        x.ReplaceLocks(o, HSet.empty)

type ResourceSet() =
    let all = ReferenceCountingSet<IResource>()
    let state = ResourceState()

    let subscriptions = Dictionary<IUserResource, IDisposable>()
    let mutable dirtyLevel0 = HashSet<IResource>()

    let trigger (r : IResource) =
        lock subscriptions (fun () -> dirtyLevel0.Add r |> ignore)

    let update () =
        let queue = LevelQueue<IResource>()
        let dirty = lock subscriptions (fun () -> Interlocked.Exchange(&dirtyLevel0, HashSet()))
        for d in dirty do
            queue.Add(d.Level, d) |> ignore

        while not queue.IsEmpty do
            let elements = queue.Dequeue()

            for e in elements do
                let ol = e.LockedInputs
                let flags = e.Update()
                let nl = e.LockedInputs
                match flags with
                    | UpdateKind.HandleChanged -> 
                        state.ReplaceLocks(ol,nl)
                    | _ ->
                        ()

                for o in !e.Outputs do
                    if o.NeedsUpdate(e, flags) then
                        if all.Contains o then
                            queue.Add(o.Level, o) |> ignore
    
    member x.Update() =
        update()

    member x.Add(r : IResource) =
        if all.Add r then
            state.AddLocks r.LockedInputs

            match r with
                | :? IUserResource as r ->
                    subscriptions.[r] <- r.Subscribe(fun () -> trigger r)
                | _ ->
                    ()

    member x.Remove(r : IResource) =
        if all.Remove r then
            state.RemoveLocks r.LockedInputs

            match r with
                | :? IUserResource as r ->
                    subscriptions.[r].Dispose()
                    subscriptions.Remove r |> ignore
                | _ ->
                    ()

                    










