namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Collections.Concurrent

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.Raytracing
open FSharp.Data.Adaptive
open Microsoft.FSharp.NativeInterop

open EXTConservativeRasterization
open KHRBufferDeviceAddress
open KHRAccelerationStructure

#nowarn "9"

type ResourceInfo<'T> =
    {
        handle  : 'T
        version : int
    }

type ImmutableResourceDescription<'Input, 'Handle> =
    {
        icreate          : 'Input -> 'Handle
        idestroy         : 'Handle -> unit
        ieagerDestroy    : bool
    }

type MutableResourceDescription<'Input, 'Handle> =
    {
        mcreate          : 'Input -> 'Handle
        mdestroy         : 'Handle -> unit
        mtryUpdate       : 'Handle -> 'Input -> bool
    }

type IResourceUser =
    abstract member Acquire : location: IResourceLocation * handle: obj -> unit
    abstract member Release : location: IResourceLocation -> unit

and IManagedResource =
    inherit IAdaptiveResource
    abstract member GetValue : user: IResourceUser * token: AdaptiveToken * renderToken: RenderToken -> obj

and IManagedResource<'T> =
    inherit IManagedResource
    inherit IAdaptiveResource<'T>
    abstract member GetValue : user: IResourceUser * token: AdaptiveToken * renderToken: RenderToken -> 'T

and IResourceLocation =
    inherit IManagedResource
    abstract member Update : user: IResourceUser * token: AdaptiveToken * renderToken: RenderToken -> ResourceInfo<obj>
    abstract member ReferenceCount : int
    abstract member Key : list<obj>
    abstract member Owner : IResourceCache

and IResourceLocation<'T> =
    inherit IResourceLocation
    inherit IManagedResource<'T>
    abstract member Update : user: IResourceUser * token: AdaptiveToken * renderToken: RenderToken -> ResourceInfo<'T>

and IResourceCache =
    abstract member Device : Device
    abstract member Remove : key : list<obj> -> unit

type INativeResourceLocation<'T when 'T : unmanaged> =
    inherit IResourceLocation<'T>
    abstract member Pointer : nativeptr<'T>


[<AutoOpen>]
module internal ResourceUserExtensions =

    module ResourceUser =
        let None =
            { new IResourceUser with
                member x.Acquire(_, _) = ()
                member x.Release(_) = () }

    type IAdaptiveValue with
        member x.GetValueUntyped(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            match x with
            | :? IManagedResource as r -> r.GetValue(user, token, renderToken)
            | _ -> x.GetValue(token, renderToken)

    type IAdaptiveValue<'T> with
        member x.GetValue(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            match x with
            | :? IManagedResource<'T> as r -> r.GetValue(user, token, renderToken)
            | _ -> x.GetValue(token, renderToken)

[<AbstractClass>]
type AbstractResourceLocation<'T>(owner : IResourceCache, key : list<obj>) =
    inherit AdaptiveObject()

    let mutable refCount = 0

    abstract member Create : unit -> unit
    abstract member Destroy : unit -> unit
    abstract member GetHandle : user: IResourceUser * token: AdaptiveToken * renderToken: RenderToken -> ResourceInfo<'T>

    member x.RefCount = refCount

    member x.Acquire() =
        lock x (fun () ->
            inc &refCount
            if refCount = 1 then
                x.Create()
        )

    member x.Release() =
        lock x (fun () ->
            dec &refCount
            if refCount = 0 then
                owner.Remove key
                x.Destroy()
                x.Outputs.Clear()
                x.OutOfDate <- true
        )

    member x.ReleaseAll() =
        lock x (fun () ->
            if refCount > 0 then
                refCount <- 0
                owner.Remove key
                x.Destroy()
                x.Outputs.Clear()
                x.OutOfDate <- true
        )

    member x.Update(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
        x.EvaluateAlways token (fun token ->
            if refCount <= 0 then failwithf "[Resource] no ref count"

            user.Release x
            let info = x.GetHandle(user, token, renderToken)
            user.Acquire(x, info.handle)
            info
        )

    member x.GetValue(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
        x.Update(user, token, renderToken).handle

    interface IAdaptiveValue with
        member x.Accept(visitor) = visitor.Visit(x)
        member x.GetValueUntyped(token) = x.GetValue(ResourceUser.None, token, RenderToken.Empty) :> obj
        member x.ContentType = typeof<'T>

    interface IAdaptiveValue<'T> with
        member x.GetValue(token) = x.GetValue(ResourceUser.None, token, RenderToken.Empty)

    interface IManagedResource with
        member x.Acquire() = x.Acquire()
        member x.Release() = x.Release()
        member x.ReleaseAll() = x.ReleaseAll()
        member x.GetValue(token, renderToken) = x.GetValue(ResourceUser.None, token, renderToken) :> obj
        member x.GetValue(user, token, renderToken) = x.GetValue(user, token, renderToken)

    interface IManagedResource<'T> with
        member x.GetValue(token, renderToken) = x.GetValue(ResourceUser.None, token, renderToken)
        member x.GetValue(user, token, renderToken) = x.GetValue(user, token, renderToken)

    interface IResourceLocation with
        member x.ReferenceCount = refCount
        member x.Update(u, t, rt) =
            let res = x.Update(u, t, rt)
            { handle = res :> obj; version = res.version }

        member x.Owner = owner
        member x.Key = key

    interface IResourceLocation<'T> with
        member x.Update(u, t, rt) = x.Update(u, t, rt)

type private DummyResourceCache(device : Device) =
    interface IResourceCache with
        member x.Device = device
        member x.Remove key = ()


[<AbstractClass>]
type UncachedResourceLocation<'T>(device : Device) =
    inherit AbstractResourceLocation<'T>(DummyResourceCache(device), [])


type ImmutableResourceLocation<'Input, 'Handle>(owner : IResourceCache, key : list<obj>,
                                                input : aval<'Input>, desc : ImmutableResourceDescription<'Input, 'Handle>) =
    inherit AbstractResourceLocation<'Handle>(owner, key)

    let mutable handle : Option<'Input * 'Handle> = None
    let mutable version = 0

    let recreate (user : IResourceUser) (token : AdaptiveToken) (renderToken : RenderToken) =
        let n = input.GetValue(user, token, renderToken)

        match handle with
        | Some(o, h) when Unchecked.equals o n -> h
        | Some(_, h) ->
            desc.idestroy h
            let r = desc.icreate n
            handle <- Some(n,r)
            inc &version
            r
        | None ->
            let r = desc.icreate n
            handle <- Some(n,r)
            inc &version
            r

    member private x.EagerDestroy() =
        if Monitor.TryEnter x then
            try
                handle |> Option.iter (snd >> desc.idestroy)
                handle <- None
            finally
                Monitor.Exit x

    override x.MarkObject() =
        if desc.ieagerDestroy then
            match Transaction.Running with
            | ValueSome t -> t.AddFinalizer x.EagerDestroy
            | _ -> x.EagerDestroy()

        true

    override x.Create() =
        input.Acquire()

    override x.Destroy() =
        input.Outputs.Remove x |> ignore
        match handle with
        | Some (_, h) ->
            desc.idestroy h
            handle <- None
        | None ->
            ()
        input.Release()

    override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
        if x.OutOfDate then
            let handle = recreate user token renderToken
            { handle = handle; version = version }
        else
            match handle with
            | Some(_,h) -> { handle = h; version = version }
            | None -> failwith "[Resource] inconsistent state"

type MutableResourceLocation<'Input, 'Handle>(owner : IResourceCache, key : list<obj>,
                                              input : aval<'Input>, desc : MutableResourceDescription<'Input, 'Handle>) =
    inherit AbstractResourceLocation<'Handle>(owner, key)

    let mutable handle : Option<'Input * 'Handle> = None
    let mutable version = 0

    let recreate (n : 'Input) =
        match handle with
        | Some(_, h) ->
            desc.mdestroy h
            let r = desc.mcreate n
            handle <- Some(n,r)
            r
        | None ->
            let r = desc.mcreate n
            handle <- Some(n,r)
            r

    let update (user : IResourceUser) (token : AdaptiveToken) (renderToken : RenderToken) =
        let n = input.GetValue(user, token, renderToken)

        match handle with
        | None ->
            inc &version
            recreate n

        | Some(oa, oh) when Unchecked.equals oa n ->
            oh

        | Some(_, oh) ->
            if desc.mtryUpdate oh n then
                handle <- Some(n, oh)
                oh
            else
                inc &version
                recreate n

    override x.Create() =
        input.Acquire()

    override x.Destroy() =
        input.Outputs.Remove x |> ignore
        match handle with
        | Some(_, h) ->
            desc.mdestroy h
            handle <- None
        | None ->
            ()
        input.Release()

    override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
        if x.OutOfDate then
            let handle = update user token renderToken
            { handle = handle; version = version }
        else
            match handle with
            | Some(_, h) -> { handle = h; version = version }
            | None -> failwith "[Resource] inconsistent state"

[<AbstractClass>]
type AbstractPointerResource<'T when 'T : unmanaged>(owner : IResourceCache, key : list<obj>) =
    inherit AbstractResourceLocation<'T>(owner, key)

    let mutable ptr = NativePtr.zero
    let mutable version = 0
    let mutable hasHandle = false

    abstract member Compute : user: IResourceUser * token: AdaptiveToken * renderToken: RenderToken -> 'T
    abstract member Free : 'T -> unit
    default x.Free _ = ()

    member x.Pointer = ptr

    member x.HasHandle = hasHandle

    member x.NoChange() =
        dec &version

    override x.Create() =
        ptr <- NativePtr.alloc 1

    override x.Destroy() =
        if hasHandle then
            let v = NativePtr.read ptr
            x.Free v
            NativePtr.free ptr
            hasHandle <- false

    override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
        if x.OutOfDate then
            let value = x.Compute(user, token, renderToken)
            if hasHandle then
                let v = NativePtr.read ptr
                x.Free v

            NativePtr.write ptr value
            hasHandle <- true
            inc &version
            { handle = value; version = version }
        else
            { handle = NativePtr.read ptr; version = version }

    interface INativeResourceLocation<'T> with
        member x.Pointer = x.Pointer

[<AbstractClass>]
type AbstractPointerResourceWithEquality<'T when 'T : unmanaged>(owner : IResourceCache, key : list<obj>) =
    inherit AbstractResourceLocation<'T>(owner, key)

    let mutable ptr = NativePtr.zero
    let mutable version = 0
    let mutable hasHandle = false

    abstract member Compute : user: IResourceUser * token: AdaptiveToken * renderToken: RenderToken -> 'T
    abstract member Free : 'T -> unit
    default x.Free _ = ()

    member x.Pointer = ptr

    override x.Create() =
        ptr <- NativePtr.alloc 1

    override x.Destroy() =
        if hasHandle then
            let v = NativePtr.read ptr
            x.Free v
            NativePtr.free ptr
            hasHandle <- false

    override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
        if x.OutOfDate then
            let value = x.Compute(user, token, renderToken)
            if hasHandle then
                let v = NativePtr.read ptr
                if Unchecked.equals v value then
                    { handle = value; version = version }
                else
                    x.Free v
                    NativePtr.write ptr value
                    inc &version
                    { handle = value; version = version }
            else
                NativePtr.write ptr value
                hasHandle <- true
                inc &version
                { handle = value; version = version }
        else
            { handle = NativePtr.read ptr; version = version }

    interface INativeResourceLocation<'T> with
        member x.Pointer = x.Pointer

type ResourceLocationCache<'T>(device : Device) =
    let store = System.Collections.Concurrent.ConcurrentDictionary<list<obj>, IResourceLocation<'T>>()

    member x.GetOrCreate(key : list<obj>, create : IResourceCache -> list<obj> -> #IResourceLocation<'T>) =
        let res =
            store.GetOrAdd(key, fun key ->
                let res = create x key :> IResourceLocation<_>
                res
            )
        res

    member x.Clear() =
        let res = store.Values |> Seq.toArray
        for r in res do r.ReleaseAll()

    interface IResourceCache with
        member x.Device = device
        member x.Remove key = store.TryRemove key |> ignore

type NativeResourceLocationCache<'T when 'T : unmanaged>(device : Device) =
    let store = System.Collections.Concurrent.ConcurrentDictionary<list<obj>, INativeResourceLocation<'T>>()

    member x.GetOrCreate(key : list<obj>, create : IResourceCache -> list<obj> -> #INativeResourceLocation<'T>) =
        let res =
            store.GetOrAdd(key, fun key ->
                let res = create x key :> INativeResourceLocation<_>
                res
            )
        res

    member x.Clear() =
        let res = store.Values |> Seq.toArray
        for r in res do r.ReleaseAll()

    interface IResourceCache with
        member x.Device = device
        member x.Remove key = store.TryRemove key |> ignore


module Resources =

    type ImageSampler = ImageView * Sampler

    type ImageSamplerArray = ResourceInfo<ImageSampler>[]

    [<Struct>]
    type DescriptorInfo =
        { Version    : int
          Descriptor : Descriptor }

    type IAdaptiveDescriptor =
        inherit IManagedResource<DescriptorInfo[]>
        abstract member Slot : int
        abstract member Count : int
        abstract member UpdateAfterBind : bool

    module AdaptiveDescriptor =

        module Abstract =

            [<AbstractClass>]
            type AdaptiveDescriptor<'T>(slot : int, count : int, resource : IResourceLocation<'T>) =
                inherit AdaptiveObject()

                let cache = Array.replicate count { Version = -1; Descriptor = Unchecked.defaultof<_> }

                let device = resource.Owner.Device
                let updateAfterBindEnabled = device.UpdateDescriptorsAfterBind

                member x.Slot = slot
                member x.Count = count
                member x.Resource = resource

                abstract member GetDescriptors : ResourceInfo<'T> * DescriptorInfo[] -> unit
                abstract member GetUpdateAfterBindFeature : inref<DescriptorFeatures> -> bool

                member x.UpdateAfterBind =
                    updateAfterBindEnabled &&
                    x.GetUpdateAfterBindFeature &device.PhysicalDevice.Features.Descriptors

                member x.Acquire() = resource.Acquire()
                member x.Release() = resource.Release()
                member x.ReleaseAll() = resource.ReleaseAll()

                member x.GetValue(user, t, rt) =
                    x.EvaluateAlways t (fun t ->
                        if x.OutOfDate then
                            let info = resource.Update(user, t, rt)
                            x.GetDescriptors(info, cache)

                        cache
                    )

                member x.Equals(other : AdaptiveDescriptor<'T>) =
                    slot = other.Slot && count = other.Count && resource = other.Resource

                override x.Equals(other : obj) =
                    match other with
                    | :? AdaptiveDescriptor<'T> as o -> x.Equals o
                    | _ -> false

                override x.GetHashCode() =
                    hash (slot, count, resource)

                interface IEquatable<AdaptiveDescriptor<'T>> with
                    member x.Equals other = x.Equals other

                interface IAdaptiveValue with
                    member x.IsConstant = false
                    member x.ContentType = typeof<DescriptorInfo[]>
                    member x.GetValueUntyped(t) = x.GetValue(ResourceUser.None, t, RenderToken.Empty) :> obj
                    member x.Accept (v : IAdaptiveValueVisitor<'R>) = v.Visit x

                interface IAdaptiveValue<DescriptorInfo[]> with
                    member x.GetValue(t) = x.GetValue(ResourceUser.None,t, RenderToken.Empty)

                interface IManagedResource with
                    member x.Acquire() = x.Acquire()
                    member x.Release() = x.Release()
                    member x.ReleaseAll() = x.ReleaseAll()
                    member x.GetValue(t, rt) = x.GetValue(ResourceUser.None, t, rt) :> obj
                    member x.GetValue(u, t, rt) = x.GetValue(u, t, rt) :> obj

                interface IManagedResource<DescriptorInfo[]> with
                    member x.GetValue(t, rt) = x.GetValue(ResourceUser.None, t, rt)
                    member x.GetValue(u, t, rt) = x.GetValue(u, t, rt)

                interface IAdaptiveDescriptor with
                    member x.Slot = x.Slot
                    member x.Count = x.Count
                    member x.UpdateAfterBind = x.UpdateAfterBind

            [<AbstractClass>]
            type AdaptiveSingleDescriptor<'T>(slot : int, resource : IResourceLocation<'T>) =
                inherit AdaptiveDescriptor<'T>(slot, 1, resource)

                abstract member GetDescriptor : 'T -> Descriptor

                override x.GetDescriptors(info, cache) =
                    cache.[0] <- { Version = info.version; Descriptor = x.GetDescriptor info.handle }

        type UniformBuffer(slot : int, buffer : IResourceLocation<_>) =
            inherit Abstract.AdaptiveSingleDescriptor<Vulkan.UniformBuffer>(slot, buffer)

            override x.GetUpdateAfterBindFeature(features) =
                features.BindingUniformBufferUpdateAfterBind

            override x.GetDescriptor(buffer) =
                Descriptor.UniformBuffer(slot, buffer)

        type StorageBuffer(slot : int, buffer : IResourceLocation<_>) =
            inherit Abstract.AdaptiveSingleDescriptor<Buffer>(slot, buffer)

            override x.GetUpdateAfterBindFeature(features) =
                features.BindingStorageBufferUpdateAfterBind

            override x.GetDescriptor(buffer) =
                Descriptor.StorageBuffer(slot, buffer, 0L, buffer.Size)

        type StorageImage(slot : int, image : IResourceLocation<_>) =
            inherit Abstract.AdaptiveSingleDescriptor<ImageView>(slot, image)

            override x.GetUpdateAfterBindFeature(features) =
                features.BindingStorageImageUpdateAfterBind

            override x.GetDescriptor(view) =
                Descriptor.StorageImage(slot, view)

        type AccelerationStructure(slot : int, accel : IResourceLocation<_>) =
            inherit Abstract.AdaptiveSingleDescriptor<Raytracing.AccelerationStructure>(slot, accel)

            override x.GetUpdateAfterBindFeature(features) =
                features.BindingAccelerationStructureUpdateAfterBind

            override x.GetDescriptor(accel) =
                Descriptor.AccelerationStructure(slot, accel)

        type CombinedImageSampler(slot : int, count : int, images : IResourceLocation<_>) =
            inherit Abstract.AdaptiveDescriptor<ImageSamplerArray>(slot, count, images)

            override x.GetUpdateAfterBindFeature(features) =
                features.BindingSampledImageUpdateAfterBind

            override x.GetDescriptors(images, cache) =
                let images = images.handle

                for i = 0 to images.Length - 1 do
                    let v, s = images.[i].handle
                    let desc = Descriptor.CombinedImageSampler(slot, i, v, s, v.Image.SamplerLayout)
                    cache.[i] <- { Version = images.[i].version; Descriptor = desc }

    type BufferResource(owner : IResourceCache, key : list<obj>, device : Device, usage : VkBufferUsageFlags, input : aval<IBuffer>) =
        inherit MutableResourceLocation<IBuffer, Buffer>(
            owner, key,
            input,
            {
                mcreate          = fun (b : IBuffer) -> device.CreateBuffer(usage, b)
                mdestroy         = fun b -> b.Dispose()
                mtryUpdate       = fun (b : Buffer) (v : IBuffer) -> Buffer.tryUpdate v b
            }
        )

    type IndirectBufferResource(owner : IResourceCache, key : list<obj>, device : Device, indexed : bool, input : aval<Aardvark.Rendering.IndirectBuffer>) =
        inherit ImmutableResourceLocation<Aardvark.Rendering.IndirectBuffer, IndirectBuffer>(
            owner, key,
            input,
            {
                icreate = fun (b : Aardvark.Rendering.IndirectBuffer) -> device.CreateIndirectBuffer(indexed, b)
                idestroy = fun b -> b.Dispose()
                ieagerDestroy = true
            }
        )

    type UniformBufferResource(owner : IResourceCache, key : list<obj>, device : Device,
                               layout : FShade.GLSL.GLSLUniformBuffer, writers : list<IAdaptiveValue * UniformWriters.IWriter>) =
        inherit AbstractResourceLocation<UniformBuffer>(owner, key)

        let mutable handle : UniformBuffer = Unchecked.defaultof<_>

        member x.Handle = handle

        override x.Create() =
            handle <- device.CreateUniformBuffer(layout)

        override x.Destroy() =
            if handle <> Unchecked.defaultof<_> then
                handle.Dispose()
                handle <- Unchecked.defaultof<_>

        override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            if x.OutOfDate then
                for (m,w) in writers do
                    w.Write(token, m, handle.Storage.Pointer)

                device.Upload handle

                { handle = handle; version = 0 }
            else
                { handle = handle; version = 0 }

    type ImageResource(owner : IResourceCache, key : list<obj>, device : Device, properties : ImageProperties, input : aval<ITexture>) =
        inherit ImmutableResourceLocation<ITexture, Image>(
            owner, key,
            input,
            {
                icreate = fun (t : ITexture) -> device.CreateImage(t, properties)
                idestroy = fun t -> t.Dispose()
                ieagerDestroy = true
            }
        )

    type SamplerResource(owner : IResourceCache, key : list<obj>, device : Device, input : aval<SamplerState>) =
        inherit ImmutableResourceLocation<SamplerState, Sampler>(
            owner, key,
            input,
            {
                icreate = fun (s : SamplerState) -> device.CreateSampler(s)
                idestroy = fun s -> s.Dispose()
                ieagerDestroy = true
            }
        )

    type DynamicSamplerStateResource(owner : IResourceCache, key : list<obj>, name : Symbol,
                                     state : SamplerState, modifier : aval<Symbol -> SamplerState -> SamplerState>) =
        inherit AbstractResourceLocation<SamplerState>(owner, key)

        let mutable cache = None

        override x.Create() =
            modifier.Acquire()

        override x.Destroy() =
            modifier.Release()

        override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            if x.OutOfDate then
                let f = modifier.GetValue(user, token, renderToken)
                cache <- Some (state |> f name)

            match cache with
            | Some s -> { handle = s; version = 0 }
            | _ -> failwith "[Resource] inconsistent state"

    type ImageSamplerResource(owner : IResourceCache, key : list<obj>,
                              imageView : IResourceLocation<ImageView>,
                              sampler : IResourceLocation<Sampler>) =
        inherit AbstractResourceLocation<ImageSampler>(owner, key)

        let mutable cache = None

        override x.Create() =
            imageView.Acquire()
            sampler.Acquire()

        override x.Destroy() =
            sampler.Release()
            imageView.Release()

        override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            if x.OutOfDate then
                let v = imageView.Update(user, token, renderToken)
                let s = sampler.Update(user, token, renderToken)
                cache <- Some (v, s)

            match cache with
            | Some (v, s) -> { handle = (v.handle, s.handle); version = v.version + s.version }
            | _ -> failwith "[Resource] inconsistent state"


    type ImageSamplerArrayResource(owner : IResourceCache, key : list<obj>, count : int,
                                   empty : IResourceLocation<ImageSampler>, input : amap<int, IResourceLocation<ImageSampler>>) =
        inherit AbstractResourceLocation<ImageSamplerArray>(owner, key)

        static let zeroHandle = { version = -1; handle = Unchecked.defaultof<_> }

        let reader = input.GetReader()

        // Use a pending set and InputChangedObject() to
        // avoid iterating over all image samplers in GetHandle()
        let pending = LockedSet<_>()

        // Maps resources to a binding slot
        // There must be a resource in a slot at all times, initially the image sampler is set for a slots.
        let images = Array.zeroCreate<IResourceLocation<_>> count
        let indices = MultiDict<IResourceLocation<_>, int>()

        // For each bound slot store the last seen version
        // Whenever a version has changed, this resource's version is incremented
        let mutable version = 0

        // Stores version increments to signal changes even when the resource's version did not change.
        // E.g. when two different image samplers with the same version swap position.
        let versionOffsets = Array.replicate count zeroHandle.version

        // The map with evaluated image samplers
        let handle = Array.replicate count zeroHandle

        // Removes a resource from the given index
        let remove (i : int) (r : IResourceLocation<_>) =
            if indices |> MultiDict.remove r i then
                r.Release()
                pending.Remove r |> ignore

        // Adds a resource to the given index
        let set (i : int) (r : IResourceLocation<_>) =
            if indices |> MultiDict.add r i then
                r.Acquire()

            // Save the previous slot version in offset array.
            // If handle is zero, version offsets already saves the previous version.
            if not <| Object.ReferenceEquals(handle.[i], zeroHandle) then
                versionOffsets.[i] <- handle.[i].version
                handle.[i] <- zeroHandle

            pending.Add r |> ignore
            images.[i] <- r

        // Save deltas to process later
        let removals = List(count)
        let additions = List(count)

        let replace i r =
            removals.Add((images.[i], i))
            additions.Add((r, i))

        // Set every slot to empty initially
        do for i = 0 to count - 1 do
            set i empty

        override x.Create() =
            for KeyValue(i, _) in indices do
                i.Acquire()
                pending.Add i |> ignore

            pending.Add empty |> ignore

        override x.Destroy() =
            for KeyValue(i, _) in indices do
                i.Release()

            pending.Clear()

        override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            if x.OutOfDate then

                // Process additions, moves and removals
                // Positive deltas are processed first, removals lead to the empty image sampler being set.
                removals.Clear()
                additions.Clear()

                let deltas = reader.GetChanges token
                for i, op in deltas do
                    match op with
                    | Set r -> replace i r
                    | Remove -> replace i empty

                for r, i in additions do
                    set i r

                for r, i in removals do
                    remove i r

                // Process pending inputs (i.e. image samplers)
                // We check the new version each handle to detect if anything actually changed.
                let mutable changed = deltas.Count > 0

                for image in pending.GetAndClear() do
                    let info = image.Update(user, token, renderToken)

                    for i in indices.[image] do

                        // If handle is zero handle, a new resource has been bound to the slot.
                        // In this case versionOffsets stores the previous version on that slot.
                        // Compute a new offset for the new handle, so the effective version is incremented by 1.
                        if Object.ReferenceEquals(handle.[i], zeroHandle) then
                            let offset = (versionOffsets.[i] - info.version) + 1
                            handle.[i] <- { info with version = info.version + offset }
                            versionOffsets.[i] <- offset
                            changed <- true

                        // Otherwise, the bound resource has not changed.
                        // Check if its version has changed.
                        else
                            let version = info.version + versionOffsets.[i]

                            if version <> handle.[i].version then
                                handle.[i] <- { info with version = version }
                                changed <- true

                if changed then
                    inc &version

            { handle = handle; version = version }

        override x.InputChangedObject(_, object) =
            match object with
            | :? IResourceLocation<ImageSampler> as r -> pending.Add r |> ignore
            | _ -> ()

    type DynamicShaderProgramResource(owner : IResourceCache, key : list<obj>, device : Device, layout : PipelineLayout, input : aval<FShade.Imperative.Module>) =
        inherit ImmutableResourceLocation<FShade.Imperative.Module, ShaderProgram>(
            owner, key,
            input,
            {
                icreate = fun (e : FShade.Imperative.Module) -> layout.AddReference(); ShaderProgram.ofModule e device
                idestroy = fun p -> p.Dispose(); layout.Dispose()
                ieagerDestroy = false
            }
        )

        member x.Layout = layout

    type InputAssemblyStateResource(owner : IResourceCache, key : list<obj>, input : IndexedGeometryMode, program : IResourceLocation<ShaderProgram>) =
        inherit AbstractPointerResourceWithEquality<VkPipelineInputAssemblyStateCreateInfo>(owner, key)

        override x.Create() =
            base.Create()
            program.Acquire()

        override x.Destroy() =
            base.Destroy()
            program.Release()

        override x.Compute(user, token, renderToken) =
            let p = program.Update(user, token, renderToken)
            let res = input |> InputAssemblyState.ofIndexedGeometryMode p.handle.HasTessellation

            VkPipelineInputAssemblyStateCreateInfo(
                VkPipelineInputAssemblyStateCreateFlags.None,
                res.topology,
                (if res.restartEnable then 1u else 0u)
            )

    type VertexInputStateResource(owner : IResourceCache, key : list<obj>, prog : PipelineInfo, input : aval<Map<Symbol, VertexInputDescription>>) =
        inherit AbstractPointerResource<VkPipelineVertexInputStateCreateInfo>(owner, key)
        static let collecti (f : int -> 'a -> list<'b>) (m : list<'a>) =
            m |> List.indexed |> List.collect (fun (i,v) -> f i v)

        override x.Free(state : VkPipelineVertexInputStateCreateInfo) =
            NativePtr.free state.pVertexAttributeDescriptions
            NativePtr.free state.pVertexBindingDescriptions

        override x.Compute(user, token, renderToken) =
            let state = input.GetValue(user, token, renderToken)

            let inputs = prog.pInputs |> List.sortBy (fun p -> p.paramLocation)

            let paramsWithInputs =
                inputs |> List.map (fun p ->
                    let sem = Symbol.Create p.paramSemantic
                    match Map.tryFind sem state with
                        | Some ip ->
                            p.paramLocation, p, ip
                        | None ->
                            failf "could not get vertex input-type for %A" p
                )

            let inputBindings =
                paramsWithInputs |> List.mapi (fun i (loc, p, ip) ->
                    VkVertexInputBindingDescription(
                        uint32 i,
                        uint32 ip.stride,
                        ip.stepRate
                    )
                ) |> List.toArray

            let inputAttributes =
                paramsWithInputs |> collecti (fun bi (loc, p, ip) ->
                    ip.offsets |> List.mapi (fun i off ->
                        VkVertexInputAttributeDescription(
                            uint32 (loc + i),
                            uint32 bi,
                            ip.inputFormat,
                            uint32 off
                        )
                    )
                ) |> List.toArray

            let pInputBindings = NativePtr.alloc inputBindings.Length
            let pInputAttributes = NativePtr.alloc inputAttributes.Length

            for i in 0 .. inputBindings.Length - 1 do
                NativePtr.set pInputBindings i inputBindings.[i]

            for i in 0 .. inputAttributes.Length - 1 do
                NativePtr.set pInputAttributes i inputAttributes.[i]

            VkPipelineVertexInputStateCreateInfo(
                VkPipelineVertexInputStateCreateFlags.None,

                uint32 inputBindings.Length,
                pInputBindings,

                uint32 inputAttributes.Length,
                pInputAttributes
            )


    type DepthStencilStateResource(owner : IResourceCache, key : list<obj>,
                                   depthTest : aval<DepthTest>, depthWrite : aval<bool>,
                                   stencilModeF : aval<StencilMode>, stencilMaskF : aval<StencilMask>,
                                   stencilModeB : aval<StencilMode>, stencilMaskB : aval<StencilMask>) =
        inherit AbstractPointerResourceWithEquality<VkPipelineDepthStencilStateCreateInfo>(owner, key)

        override x.Compute(user, token, renderToken) =
            let depthTest = depthTest.GetValue(user, token, renderToken)
            let depthWrite = depthWrite.GetValue(user, token, renderToken)

            let stencilMaskF = stencilMaskF.GetValue(user, token, renderToken)
            let stencilModeF = stencilModeF.GetValue(user, token, renderToken)
            let stencilMaskB = stencilMaskB.GetValue(user, token, renderToken)
            let stencilModeB = stencilModeB.GetValue(user, token, renderToken)

            let depth = DepthState.create depthWrite depthTest
            let stencil = StencilState.create stencilMaskF stencilMaskB stencilModeF stencilModeB

            VkPipelineDepthStencilStateCreateInfo(
                VkPipelineDepthStencilStateCreateFlags.None,
                (if depth.testEnabled then 1u else 0u),
                (if depth.writeEnabled then 1u else 0u),
                depth.compare,
                (if depth.boundsTest then 1u else 0u),
                (if stencil.enabled then 1u else 0u),
                stencil.front,
                stencil.back,
                float32 depth.depthBounds.Min,
                float32 depth.depthBounds.Max
            )

    type RasterizerStateResource(owner : IResourceCache, key : list<obj>,
                                 depthClamp : aval<bool>, depthBias : aval<DepthBias>,
                                 cull : aval<CullMode>, frontFace : aval<WindingOrder>, fill : aval<FillMode>,
                                 conservativeRaster : aval<bool>) =
        inherit AbstractPointerResourceWithEquality<VkPipelineRasterizationStateCreateInfo>(owner, key)

        override x.Free(info : VkPipelineRasterizationStateCreateInfo) =
            Marshal.FreeHGlobal info.pNext

        override x.Compute(user, token, renderToken) =
            let depthClamp = depthClamp.GetValue(user, token, renderToken)
            let bias = depthBias.GetValue(user, token, renderToken)
            let cull = cull.GetValue(user, token, renderToken)
            let front = frontFace.GetValue(user, token, renderToken)
            let fill = fill.GetValue(user, token, renderToken)
            let conservativeRaster = conservativeRaster.GetValue(user, token, renderToken)
            let state = RasterizerState.create conservativeRaster depthClamp bias cull front fill

            let conservativeRaster =
                VkPipelineRasterizationConservativeStateCreateInfoEXT(
                    VkPipelineRasterizationConservativeStateCreateFlagsEXT.None,
                    (if conservativeRaster then VkConservativeRasterizationModeEXT.Overestimate else VkConservativeRasterizationModeEXT.Disabled),
                    0.0f
                )

            let pConservativeRaster = NativePtr.alloc<VkPipelineRasterizationConservativeStateCreateInfoEXT> 1
            conservativeRaster |> NativePtr.write pConservativeRaster

            VkPipelineRasterizationStateCreateInfo(
                NativePtr.toNativeInt pConservativeRaster,
                VkPipelineRasterizationStateCreateFlags.None,
                (if state.depthClampEnable then 1u else 0u),
                (if state.rasterizerDiscardEnable then 1u else 0u),
                state.polygonMode,
                state.cullMode,
                state.frontFace,
                (if state.depthBiasEnable then 1u else 0u),
                float32 state.depthBiasConstantFactor,
                float32 state.depthBiasClamp,
                float32 state.depthBiasSlopeFactor,
                float32 state.lineWidth
            )

    type ColorBlendStateResource(owner : IResourceCache, key : list<obj>,
                                 writeMasks : aval<ColorMask[]>, blendModes : aval<BlendMode[]>, blendConstant : aval<C4f>) =
        inherit AbstractPointerResourceWithEquality<VkPipelineColorBlendStateCreateInfo>(owner, key)

        override x.Free(h : VkPipelineColorBlendStateCreateInfo) =
            NativePtr.free h.pAttachments

        override x.Compute(user, token, renderToken) =
            let writeMasks = writeMasks.GetValue(user, token, renderToken)
            let blendModes = blendModes.GetValue(user, token, renderToken)
            let blendConstant = blendConstant.GetValue(user, token, renderToken)

            let state = ColorBlendState.create writeMasks blendModes blendConstant
            let pAttStates = NativePtr.alloc writeMasks.Length

            for i in 0 .. state.attachmentStates.Length - 1 do
                let s = state.attachmentStates.[i]
                let att =
                    VkPipelineColorBlendAttachmentState(
                        (if s.enabled then 1u else 0u),
                        s.srcFactor,
                        s.dstFactor,
                        s.operation,
                        s.srcFactorAlpha,
                        s.dstFactorAlpha,
                        s.operationAlpha,
                        s.colorWriteMask
                    )
                NativePtr.set pAttStates i att


            VkPipelineColorBlendStateCreateInfo(
                VkPipelineColorBlendStateCreateFlags.None,
                (if state.logicOpEnable then 1u else 0u),
                state.logicOp,
                uint32 writeMasks.Length,
                pAttStates,
                state.constant
            )

    // TODO: Sample shading
    type MultisampleStateResource(owner : IResourceCache, key : list<obj>, samples : int, enable : aval<bool>) =
        inherit AbstractPointerResourceWithEquality<VkPipelineMultisampleStateCreateInfo>(owner, key)

        override x.Compute(user, token, renderToken) =
            //let enable = enable.GetValue token

            // TODO: Cannot disable MSAA here...
            //let samples = if enable then samples else 1
            let state = MultisampleState.create false samples

            VkPipelineMultisampleStateCreateInfo(
                VkPipelineMultisampleStateCreateFlags.None,
                unbox state.samples,
                (if state.sampleShadingEnable then 1u else 0u),
                float32 state.minSampleShading,
                NativePtr.zero,
                (if state.alphaToCoverageEnable then 1u else 0u),
                (if state.alphaToOneEnable then 1u else 0u)
            )

    type DirectDrawCallResource(owner : IResourceCache, key : list<obj>, indexed : bool, calls : aval<list<DrawCallInfo>>) =
        inherit AbstractPointerResourceWithEquality<DrawCall>(owner, key)

        override x.Free(call : DrawCall) =
            call.Dispose()

        override x.Compute(user, token, renderToken) =
            let calls = calls.GetValue(user, token, renderToken)
            DrawCall.Direct(indexed, List.toArray calls)

    type DescriptorSetResource(owner : IResourceCache, key : list<obj>, layout : DescriptorSetLayout, bindings : IAdaptiveDescriptor[]) =
        inherit AbstractResourceLocation<DescriptorSet>(owner, key)

        let mutable handle = Unchecked.defaultof<DescriptorSet>
        let mutable version = 0

        // Use a pending set and InputChangedObject() to
        // avoid iterating over all descriptors in GetHandle()
        let pending = LockedSet<_>(bindings)

        // Save the array index of each descriptor
        // Note: Index might be different from binding
        do assert (bindings = Array.distinct bindings)
        let indices = bindings |> Array.indexed |> Array.map (fun (i, d) -> d, i) |> HashMap.ofArray

        // Dictionary to collect all descriptor writes in GetHandle()
        let writes = Dictionary<struct (int * int), Descriptor>()

        // We save the last seen version for each descriptor element.
        // If the version didn't change, we won't bother updating the set.
        let versions =
            Array.init bindings.Length (fun i ->
                Array.replicate bindings.[i].Count -1
            )

        override x.Create() =
            for b in bindings do
                b.Acquire()

            handle <- layout.Device.CreateDescriptorSet(layout)

        override x.Destroy() =
            for b in bindings do
                b.Release()

            if handle <> Unchecked.defaultof<_> then
                handle.Dispose()
                handle <- Unchecked.defaultof<_>

        override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            if x.OutOfDate then

                writes.Clear()
                let mutable recompile = false

                // Get pending resources.
                // May contain nested dependencies so we loop until there are no more pending inputs.
                pending |> LockedSet.lock (fun _ ->
                    while pending.Count > 0 do
                        let dirty = pending.GetAndClear()

                        for d in dirty do
                            let i = indices.[d]
                            let infos = d.GetValue(user, token, renderToken)

                            for j = 0 to infos.Length - 1 do
                                if versions.[i].[j] <> infos.[j].Version then
                                    versions.[i].[j] <- infos.[j].Version
                                    writes.[struct (i, j)] <- infos.[j].Descriptor
                                    recompile <- recompile || not d.UpdateAfterBind
                )

                if writes.Count > 0 then
                    handle.Update(writes.Values.ToArray writes.Count)

                    // If we update a descriptor which does not
                    // support update-after-bind, we have to rerecord the command buffer.
                    if recompile then
                        inc &version

            { handle = handle; version = version }

        override x.InputChangedObject(_, object) =
            match object with
            | :? IAdaptiveDescriptor as r -> pending.Add r |> ignore
            | _ -> ()


    type PipelineResource(owner : IResourceCache, key : list<obj>,
                          renderPass : RenderPass,
                          program : IResourceLocation<ShaderProgram>,
                          inputState : INativeResourceLocation<VkPipelineVertexInputStateCreateInfo>,
                          inputAssembly : INativeResourceLocation<VkPipelineInputAssemblyStateCreateInfo>,
                          rasterizerState : INativeResourceLocation<VkPipelineRasterizationStateCreateInfo>,
                          colorBlendState : INativeResourceLocation<VkPipelineColorBlendStateCreateInfo>,
                          depthStencil : INativeResourceLocation<VkPipelineDepthStencilStateCreateInfo>,
                          multisample : INativeResourceLocation<VkPipelineMultisampleStateCreateInfo>) =
        inherit AbstractPointerResource<VkPipeline>(owner, key)

        static let check str err =
            if err <> VkResult.Success then failwithf "[Vulkan] %s" str

        override x.Create() =
            base.Create()
            program.Acquire()
            inputState.Acquire()
            inputAssembly.Acquire()
            rasterizerState.Acquire()
            colorBlendState.Acquire()
            depthStencil.Acquire()
            multisample.Acquire()

        override x.Destroy() =
            base.Destroy()
            program.Release()
            inputState.Release()
            inputAssembly.Release()
            rasterizerState.Release()
            colorBlendState.Release()
            depthStencil.Release()
            multisample.Release()

        override x.Compute(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            let program = program.Update(user, token, renderToken)

            let prog = program.handle
            let device = prog.Device

            let pipeline =
                native {
                    let! pShaderCreateInfos = prog.ShaderCreateInfos

                    let! pViewportState =
                        let vp  =
                            if device.AllCount > 1u then
                                if renderPass.LayerCount > 1 then 1u
                                else device.AllCount
                            else 1u
                        VkPipelineViewportStateCreateInfo(
                            VkPipelineViewportStateCreateFlags.None,

                            uint32 vp,
                            NativePtr.zero,

                            uint32 vp,
                            NativePtr.zero
                        )

                    let dynamicStates = [| VkDynamicState.Viewport; VkDynamicState.Scissor |]
                    let! pDynamicStates = Array.map uint32 dynamicStates

                    let! pTessStateInfo =
                        VkPipelineTessellationStateCreateInfo(
                            VkPipelineTessellationStateCreateFlags.None,
                            uint32 prog.TessellationPatchSize
                        )

                    let pTessState =
                        if prog.HasTessellation then pTessStateInfo
                        else NativePtr.zero

                    let! pDynamicStates =
                        VkPipelineDynamicStateCreateInfo(
                            VkPipelineDynamicStateCreateFlags.None,

                            uint32 dynamicStates.Length,
                            NativePtr.cast pDynamicStates
                        )

                    // TODO: tessellation input-patch-size

                    let inputState = inputState.Update(user, token, renderToken) |> ignore; inputState.Pointer
                    let inputAssembly = inputAssembly.Update(user, token, renderToken) |> ignore; inputAssembly.Pointer
                    let rasterizerState = rasterizerState.Update(user, token, renderToken) |> ignore; rasterizerState.Pointer
                    let depthStencil = depthStencil.Update(user, token, renderToken) |> ignore; depthStencil.Pointer
                    let colorBlendState = colorBlendState.Update(user, token, renderToken) |> ignore; colorBlendState.Pointer
                    let multisample = multisample.Update(user, token, renderToken) |> ignore; multisample.Pointer

                    let basePipeline, derivativeFlag =
                        if not x.HasHandle then
                            VkPipeline.Null, VkPipelineCreateFlags.None
                        else
                            !!x.Pointer, VkPipelineCreateFlags.DerivativeBit

                    let! pHandle = VkPipeline.Null
                    let! pDesc =
                        VkGraphicsPipelineCreateInfo(
                            VkPipelineCreateFlags.AllowDerivativesBit ||| derivativeFlag,
                            uint32 prog.ShaderCreateInfos.Length,
                            pShaderCreateInfos,
                            inputState,
                            inputAssembly,
                            pTessState,
                            pViewportState,
                            rasterizerState,
                            multisample,
                            depthStencil,
                            colorBlendState,
                            pDynamicStates, //dynamic
                            prog.PipelineLayout.Handle,
                            renderPass.Handle,
                            0u,
                            basePipeline,
                            -1
                        )

                    VkRaw.vkCreateGraphicsPipelines(device.Handle, VkPipelineCache.Null, 1u, pDesc, NativePtr.zero, pHandle)
                        |> check "could not create pipeline"

                    return !!pHandle
                }

            pipeline

        override x.Free(p : VkPipeline) =
            VkRaw.vkDestroyPipeline(renderPass.Device.Handle, p, NativePtr.zero)

    type IndirectDrawCallResource(owner : IResourceCache, key : list<obj>, indexed : bool, calls : IResourceLocation<IndirectBuffer>) =
        inherit AbstractPointerResourceWithEquality<DrawCall>(owner, key)

        override x.Create() =
            base.Create()
            calls.Acquire()

        override x.Destroy() =
            base.Destroy()
            calls.Release()

        override x.Compute(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            let calls = calls.Update(user, token, renderToken)
            let call = DrawCall.Indirect(indexed, calls.handle.Handle, calls.handle.Count)
            call

        override x.Free(call : DrawCall) =
            call.Dispose()

    type BufferBindingResource(owner : IResourceCache, key : list<obj>, buffers : list<IResourceLocation<Buffer> * int64>) =
        inherit AbstractPointerResource<VertexBufferBinding>(owner, key)

        let mutable last = []

        override x.Create() =
            base.Create()
            for (b,_) in buffers do b.Acquire()

        override x.Destroy() =
            base.Destroy()
            for (b,_) in buffers do b.Release()

        override x.Compute(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            let calls = buffers |> List.map (fun (b,o) -> b.Update(user, token, renderToken).handle.Handle, o)

            if calls <> last then last <- calls
            else x.NoChange()

            let call = new VertexBufferBinding(0, List.toArray calls)
            call

        override x.Free(b : VertexBufferBinding) =
            b.Dispose()

    type DescriptorSetBindingResource(owner : IResourceCache, key : list<obj>,
                                      bindPoint : VkPipelineBindPoint, layout : PipelineLayout, sets : IResourceLocation<DescriptorSet>[]) =
        inherit AbstractPointerResource<DescriptorSetBinding>(owner, key)

        let mutable setVersions = Array.init sets.Length (fun _ -> -1)
        let mutable target : Option<DescriptorSetBinding> = None

        override x.Create() =
            base.Create()
            for s in sets do s.Acquire()

        override x.Destroy() =
            base.Destroy()
            for s in sets do s.Release()
            setVersions <- Array.init sets.Length (fun _ -> -1)
            match target with
                | Some t -> t.Dispose(); target <- None
                | None -> ()

        override x.Compute(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            let mutable changed = false
            let target =
                match target with
                    | Some t -> t
                    | None ->
                        let t = new DescriptorSetBinding(bindPoint, layout.Handle, 0, sets.Length)
                        target <- Some t
                        t

            for i in 0 .. sets.Length - 1 do
                let info = sets.[i].Update(user, token, renderToken)
                NativePtr.set target.Sets i info.handle.Handle
                if info.version <> setVersions.[i] then
                    setVersions.[i] <- info.version
                    changed <- true

            if not changed then x.NoChange()

            target

        override x.Free(t : DescriptorSetBinding) =
            () //t.Dispose()

    type IndexBufferBindingResource(owner : IResourceCache, key : list<obj>, indexType : VkIndexType, index : IResourceLocation<Buffer>) =
        inherit AbstractPointerResourceWithEquality<IndexBufferBinding>(owner, key)


        override x.Create() =
            base.Create()
            index.Acquire()

        override x.Destroy() =
            base.Destroy()
            index.Release()

        override x.Compute(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            let index = index.Update(user, token, renderToken)
            let ibo = IndexBufferBinding(index.handle.Handle, indexType)
            ibo

        override x.Free(ibo : IndexBufferBinding) =
            //ibo.TryDispose()
            ()

    [<AbstractClass>]
    type AbstractImageViewResource(owner : IResourceCache, key : list<obj>, image : IResourceLocation<Image>, levels : aval<Range1i>, slices : aval<Range1i>) =
        inherit AbstractResourceLocation<ImageView>(owner, key)

        let mutable handle : Option<ImageView> = None
        let mutable version = 0

        abstract member CreateImageView : image: Image * levels: Range1i * slices: Range1i * mapping: VkComponentMapping -> ImageView

        override x.Create() =
            image.Acquire()
            levels.Acquire()
            slices.Acquire()

        override x.Destroy() =
            handle |> Option.iter Disposable.dispose
            handle <- None
            slices.Release()
            levels.Release()
            image.Release()

        override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            if x.OutOfDate then
                let image = image.Update(user, token, renderToken)
                if image.handle.IsNull then raise <| NullReferenceException("[Vulkan] Image handle of view is null")

                let levels = levels.GetValue(user, token, renderToken)
                let slices = slices.GetValue(user, token, renderToken)

                let isIdentical =
                    match handle with
                    | Some h -> h.Image = image.handle && h.MipLevelRange = levels && h.ArrayRange = slices
                    | None -> false

                if isIdentical then
                    { handle = handle.Value; version = version }
                else
                    match handle with
                    | Some h -> h.Dispose()
                    | None -> ()

                    let mapping =
                        if VkFormat.toColFormat image.handle.Format = Col.Format.Gray then
                            VkComponentMapping(VkComponentSwizzle.R, VkComponentSwizzle.R, VkComponentSwizzle.R, VkComponentSwizzle.A)
                        else
                            VkComponentMapping.Identity

                    let h = x.CreateImageView(image.handle, levels, slices, mapping)
                    handle <- Some h
                    inc &version

                    { handle = h; version = version }
            else
                match handle with
                | Some h -> { handle = h; version = version }
                | None -> failwith "[Resource] inconsistent state"

    type ImageViewResource(owner : IResourceCache, key : list<obj>, device : Device,
                           samplerType : FShade.GLSL.GLSLSamplerType, image : IResourceLocation<Image>, levels : aval<Range1i>, slices : aval<Range1i>) =
        inherit AbstractImageViewResource(owner, key, image, levels, slices)

        new (owner : IResourceCache, key : list<obj>, device : Device,
             samplerType : FShade.GLSL.GLSLSamplerType, image : IResourceLocation<Image>) =
            let levels = image |> AVal.map (fun i -> Range1i(0, i.MipMapLevels - 1))
            let slices = image |> AVal.map (fun i -> Range1i(0, i.Layers - 1))
            ImageViewResource(owner, key, device, samplerType, image, levels, slices)

        override x.CreateImageView(image : Image, levels : Range1i, slices : Range1i, mapping : VkComponentMapping) =
            device.CreateInputImageView(image, samplerType, levels, slices, mapping)

    type StorageImageViewResource(owner : IResourceCache, key : list<obj>, device : Device,
                                  imageType : FShade.GLSL.GLSLImageType, image : IResourceLocation<Image>, levels : aval<Range1i>, slices : aval<Range1i>) =
        inherit AbstractImageViewResource(owner, key, image, levels, slices)

        new (owner : IResourceCache, key : list<obj>, device : Device,
             imageType : FShade.GLSL.GLSLImageType, image : IResourceLocation<Image>) =
            let levels = image |> AVal.map (fun i -> Range1i(0, i.MipMapLevels - 1))
            let slices = image |> AVal.map (fun i -> Range1i(0, i.Layers - 1))
            StorageImageViewResource(owner, key, device, imageType, image, levels, slices)

        override x.CreateImageView(image : Image, levels : Range1i, slices : Range1i, mapping : VkComponentMapping) =
            device.CreateStorageView(image, imageType, levels, slices, mapping)

    type IsActiveResource(owner : IResourceCache, key : list<obj>, input : aval<bool>) =
        inherit AbstractPointerResourceWithEquality<int>(owner, key)

        override x.Compute(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            if input.GetValue(user, token, renderToken) then 1 else 0

    module Raytracing =
        open Aardvark.Rendering.Raytracing

        type AccelerationStructureResource(owner : IResourceCache, key : list<obj>, device : Device,
                                           instanceBuffer : IResourceLocation<Buffer>, instanceCount : aval<int>, usage : AccelerationStructureUsage) =
            inherit AbstractResourceLocation<AccelerationStructure>(owner, key)

            let mutable handle : Option<AccelerationStructure> = None
            let mutable version = 0

            let create (data : AccelerationStructureData) =
                let acc = AccelerationStructure.create device true usage data
                handle <- Some acc
                inc &version
                { handle = acc; version = version }

            override x.Create() =
                instanceBuffer.Acquire()

            override x.Destroy() =
                match handle with
                | Some h ->
                    h.Dispose()
                    handle <- None
                | None -> ()

                instanceBuffer.Release()

            override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
                if x.OutOfDate then
                    let buffer = instanceBuffer.Update(user, token, renderToken).handle
                    let count = instanceCount.GetValue(user, token, renderToken)
                    let data = AccelerationStructureData.Instances { Buffer = buffer; Count = uint32 count }

                    match handle with
                    | None -> create data

                    | Some old ->
                        if old |> AccelerationStructure.tryUpdate data then
                            { handle = old; version = version }
                        else
                            old.Dispose()
                            create data
                else
                    match handle with
                    | Some h -> { handle = h; version = version }
                    | None -> failwith "[Resource] inconsistent state"


        type RaytracingPipelineResource(owner : IResourceCache, key : list<obj>,
                                        program : RaytracingProgram, maxRecursionDepth : aval<int>) =
            inherit AbstractResourceLocation<RaytracingPipeline>(owner, key)

            let mutable handle : Option<RaytracingPipelineDescription * RaytracingPipeline> = None
            let mutable version = 0

            let device = program.Device

            let recursionDepthLimit =
                device.Runtime.MaxRayRecursionDepth

            let destroy() =
                handle |> Option.iter (snd >> Disposable.dispose)
                handle <- None

            let create description =
                inc &version
                let basePipeline = handle |> Option.map snd
                let pipeline = description |> RaytracingPipeline.create device basePipeline
                handle <- Some (description, pipeline)
                basePipeline |> Option.iter Disposable.dispose
                { handle = pipeline; version = version }

            override x.Create() =
                ()

            override x.Destroy() =
                destroy()

            override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
                if x.OutOfDate then
                    let depth = maxRecursionDepth.GetValue(user, token, renderToken) |> min recursionDepthLimit
                    let description = { Program = program; MaxRecursionDepth = uint32 depth }

                    match handle with
                    | Some (o, p) when description = o ->
                        { handle = p; version = version }
                    | _ ->
                        create description

                else
                    match handle with
                    | Some (_, h) -> { handle = h; version = version }
                    | None -> failwith "[Resource] inconsistent state"


        type ShaderBindingTableResource(owner : IResourceCache, key : list<obj>,
                                        pipeline : IResourceLocation<RaytracingPipeline>, hitConfigs : aval<Set<HitConfig>>) =
            inherit AbstractResourceLocation<ShaderBindingTable>(owner, key)

            let mutable handle : Option<ShaderBindingTable> = None
            let mutable version = 0

            let destroy() =
                handle |> Option.iter Disposable.dispose
                handle <- None

            let create hitConfigs pipeline =
                inc &version
                let table = ShaderBindingTable.create hitConfigs pipeline
                handle <- Some table
                { handle = table; version = version }

            let update hitConfigs pipeline table =
                let updated = table |> ShaderBindingTable.updateOrRecreate hitConfigs pipeline
                if updated <> table then
                    inc &version
                    handle <- Some updated
                { handle = updated; version = version }

            override x.Create() =
                pipeline.Acquire()

            override x.Destroy() =
                destroy()
                pipeline.Release()

            override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
                if x.OutOfDate then
                    let pipeline = pipeline.Update(user, token, renderToken)
                    let configs = hitConfigs.GetValue(user, token, renderToken)

                    match handle with
                    | Some tbl -> tbl |> update configs pipeline.handle
                    | _ -> create configs pipeline.handle
                else
                    match handle with
                    | Some tbl -> { handle = tbl; version = version }
                    | None -> failwith "[Resource] inconsistent state"


open Resources
open Resources.Raytracing

type ResourceManager(device : Device) =
    //let descriptorPool = device.CreateDescriptorPool(1 <<< 22, 1 <<< 22)

    let bufferCache             = ResourceLocationCache<Buffer>(device)
    let indirectBufferCache     = ResourceLocationCache<IndirectBuffer>(device)
    let indexBufferCache        = ResourceLocationCache<Buffer>(device)
    let descriptorSetCache      = ResourceLocationCache<DescriptorSet>(device)
    let uniformBufferCache      = ResourceLocationCache<UniformBuffer>(device)
    let imageCache              = ResourceLocationCache<Image>(device)
    let imageViewCache          = ResourceLocationCache<ImageView>(device)
    let samplerCache            = ResourceLocationCache<Sampler>(device)
    let samplerStateCache       = ResourceLocationCache<SamplerState>(device)
    let imageSamplerCache       = ResourceLocationCache<ImageSampler>(device)
    let imageSamplerArrayCache  = ResourceLocationCache<ImageSamplerArray>(device)
    let imageSamplerMapCache    = ConcurrentDictionary<IAdaptiveValue, amap<int, IResourceLocation<ImageSampler>>>()
    let dynamicProgramCache     = ResourceLocationCache<ShaderProgram>(device)

    let accelerationStructureCache = ResourceLocationCache<Raytracing.AccelerationStructure>(device)
    let raytracingPipelineCache    = ResourceLocationCache<Raytracing.RaytracingPipeline>(device)
    let shaderBindingTableCache    = ResourceLocationCache<Raytracing.ShaderBindingTable>(device)

    let vertexInputCache        = NativeResourceLocationCache<VkPipelineVertexInputStateCreateInfo>(device)
    let inputAssemblyCache      = NativeResourceLocationCache<VkPipelineInputAssemblyStateCreateInfo>(device)
    let depthStencilCache       = NativeResourceLocationCache<VkPipelineDepthStencilStateCreateInfo>(device)
    let rasterizerStateCache    = NativeResourceLocationCache<VkPipelineRasterizationStateCreateInfo>(device)
    let colorBlendStateCache    = NativeResourceLocationCache<VkPipelineColorBlendStateCreateInfo>(device)
    let multisampleCache        = NativeResourceLocationCache<VkPipelineMultisampleStateCreateInfo>(device)
    let pipelineCache           = NativeResourceLocationCache<VkPipeline>(device)

    let drawCallCache           = NativeResourceLocationCache<DrawCall>(device)
    let bufferBindingCache      = NativeResourceLocationCache<VertexBufferBinding>(device)
    let descriptorBindingCache  = NativeResourceLocationCache<DescriptorSetBinding>(device)
    let indexBindingCache       = NativeResourceLocationCache<IndexBufferBinding>(device)
    let isActiveCache           = NativeResourceLocationCache<int>(device)

    member x.Dispose() =
        bufferCache.Clear()

        indirectBufferCache.Clear()
        indexBufferCache.Clear()
        descriptorSetCache.Clear()
        uniformBufferCache.Clear()
        imageCache.Clear()
        imageViewCache.Clear()
        samplerCache.Clear()
        samplerStateCache.Clear()
        imageSamplerCache.Clear()
        imageSamplerArrayCache.Clear()
        dynamicProgramCache.Clear()

        accelerationStructureCache.Clear()
        raytracingPipelineCache.Clear()
        shaderBindingTableCache.Clear()

        vertexInputCache.Clear()
        inputAssemblyCache.Clear()
        depthStencilCache.Clear()
        rasterizerStateCache.Clear()
        colorBlendStateCache.Clear()
        multisampleCache.Clear()
        pipelineCache.Clear()

        drawCallCache.Clear()
        bufferBindingCache.Clear()
        descriptorBindingCache.Clear()
        indexBindingCache.Clear()
        isActiveCache.Clear()



    member x.Device = device

    member private x.CreateBuffer(input : aval<IBuffer>, usage : VkBufferUsageFlags) =
        bufferCache.GetOrCreate([input :> obj], fun cache key ->
            new BufferResource(cache, key, device, usage, input |> AdaptiveResource.cast) :> IResourceLocation<Buffer>
        )

    member private x.CreateBuffer(input : aval<IBackendBuffer>) =
        bufferCache.GetOrCreate([input :> obj], fun cache key ->
            new BufferResource(cache, key, device, VkBufferUsageFlags.None, input |> AdaptiveResource.cast) :> IResourceLocation<Buffer>
        )

    member x.CreateBuffer(input : aval<IBuffer>) =
        x.CreateBuffer(input, VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.VertexBufferBit)

    member x.CreateIndexBuffer(input : aval<IBuffer>) =
        x.CreateBuffer(input, VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.IndexBufferBit)

    member x.CreateIndirectBuffer(indexed : bool, input : aval<Aardvark.Rendering.IndirectBuffer>) =
        indirectBufferCache.GetOrCreate([indexed :> obj; input :> obj], fun cache key -> new IndirectBufferResource(cache, key, device, indexed, input))

    member x.CreateImage(properties : ImageProperties, input : aval<ITexture>) =
        imageCache.GetOrCreate([properties :> obj; input :> obj], fun cache key -> new ImageResource(cache, key, device, properties, input))

    member x.CreateImage(properties : ImageProperties, input : aval<ITextureLevel>) =
        let input = input |> AdaptiveResource.mapNonAdaptive (fun l -> l.Texture :> ITexture)
        imageCache.GetOrCreate([properties :> obj; input :> obj], fun cache key -> new ImageResource(cache, key, device, properties, input))

    member x.CreateImageView(samplerType : FShade.GLSL.GLSLSamplerType, input : IResourceLocation<Image>) =
        imageViewCache.GetOrCreate([samplerType :> obj; input :> obj], fun cache key -> new ImageViewResource(cache, key, device, samplerType, input))

    member x.CreateImageView(imageType : FShade.GLSL.GLSLImageType, input : IResourceLocation<Image>, levels : aval<Range1i>, slices : aval<Range1i>) =
        imageViewCache.GetOrCreate(
            [imageType :> obj; input :> obj; levels :> obj; slices :> obj],
            fun cache key -> new StorageImageViewResource(cache, key, device, imageType, input, levels, slices)
        )

    member x.CreateImageView(imageType : FShade.GLSL.GLSLImageType, input : IResourceLocation<Image>) =
        imageViewCache.GetOrCreate([imageType :> obj; input :> obj], fun cache key -> new StorageImageViewResource(cache, key, device, imageType, input))

    member x.CreateSampler(data : aval<SamplerState>) =
        samplerCache.GetOrCreate([data :> obj], fun cache key -> new SamplerResource(cache, key, device, data))

    member x.CreateDynamicSamplerState(name : Symbol, state : SamplerState, modifier : aval<Symbol -> SamplerState -> SamplerState>) =
        samplerStateCache.GetOrCreate(
            [name :> obj; state :> obj; modifier :> obj],
            fun cache key -> new DynamicSamplerStateResource(cache, key, name, state, modifier)
        )

    member x.CreateImageSampler(samplerType : FShade.GLSL.GLSLSamplerType,
                                texture : aval<ITexture>, samplerDesc : aval<SamplerState>) =
        let image = x.CreateImage(samplerType.Properties, texture)
        let view = x.CreateImageView(samplerType, image)
        let sampler = x.CreateSampler(samplerDesc)

        imageSamplerCache.GetOrCreate(
            [view :> obj; sampler :> obj],
            fun cache key -> new ImageSamplerResource(cache, key, view, sampler)
        )

    member x.CreateImageSamplerArray(count : int, samplerType : FShade.GLSL.GLSLSamplerType,
                                     textures : aval<array<int * aval<ITexture>>>, samplerDesc : aval<SamplerState>) =

        let empty = x.CreateImageSampler(samplerType, AVal.constant <| NullTexture(), AVal.constant SamplerState.Default)

        let map =
            imageSamplerMapCache.GetOrAdd(textures, fun _ ->
                textures |> AMap.ofAVal |> AMap.map (fun _ texture ->
                    x.CreateImageSampler(samplerType, texture, samplerDesc)
                )
            )

        x.CreateImageSamplerArray(count, empty, map)

    member x.CreateImageSamplerArray(count : int, samplerType : FShade.GLSL.GLSLSamplerType,
                                     textures : aval<ITexture[]>, samplerDesc : aval<SamplerState>) =

        let empty = x.CreateImageSampler(samplerType, AVal.constant <| NullTexture(), AVal.constant SamplerState.Default)

        let map =
            imageSamplerMapCache.GetOrAdd(textures, fun _ ->
                textures |> AVal.map (Array.choosei (fun i texture ->
                    if i < count then
                        Some (i, x.CreateImageSampler(samplerType, AVal.constant texture, samplerDesc))
                    else
                        None
                ))
                |> AMap.ofAVal
            )

        x.CreateImageSamplerArray(count, empty, map)

    member x.CreateImageSamplerArray(count : int, empty : IResourceLocation<ImageSampler>, input : seq<int * IResourceLocation<ImageSampler>>) =
        x.CreateImageSamplerArray(count, empty, AMap.ofSeq input)

    member x.CreateImageSamplerArray(count : int, empty : IResourceLocation<ImageSampler>, input : amap<int, IResourceLocation<ImageSampler>>) =
        imageSamplerArrayCache.GetOrCreate(
            [count :> obj, empty :> obj; input :> obj], fun cache key -> new ImageSamplerArrayResource(cache, key, count, empty, input)
        )

    member x.CreateShaderProgram(pass : RenderPass, data : ISurface) =
        let program = device.CreateShaderProgram(pass, data)

        let resource =
            { new UncachedResourceLocation<ShaderProgram>(pass.Device) with
                override x.Create () = ()
                override x.Destroy () = program.Dispose()
                override x.GetHandle(u, t, rt) = { handle = program; version = 0 }
            } :> IResourceLocation<_>
        resource.Acquire()

        program.PipelineLayout, resource

    member x.CreateShaderProgram(signature : RenderPass, data : FShade.Effect, top : IndexedGeometryMode) =
        let program = device.CreateShaderProgram(signature, data, top)

        if FShade.EffectDebugger.isAttached then
            FShade.EffectDebugger.saveCode data program.Surface

        let resource =
            { new UncachedResourceLocation<ShaderProgram>(signature.Device) with
                override x.Create () = ()
                override x.Destroy () = program.Dispose()
                override x.GetHandle(u, t, rt) = { handle = program; version = 0 }
            } :> IResourceLocation<_>
        resource.Acquire()

        program.PipelineLayout, resource

    member private x.CreateDynamicShaderProgram(pass : RenderPass, compile : FShade.EffectConfig -> FShade.EffectInputLayout * aval<FShade.Imperative.Module>) =
        dynamicProgramCache.GetOrCreate(
            [pass.Layout :> obj; compile :> obj],
            fun cache key ->
                let effectConfig =
                    pass.EffectConfig(
                        PipelineInfo.fshadeConfig.depthRange,
                        PipelineInfo.fshadeConfig.flipHandedness
                    )

                let _, module_ = compile effectConfig
                use initialProgram = device.CreateShaderProgram(AVal.force module_)

                let program = new DynamicShaderProgramResource(cache, key, device, initialProgram.PipelineLayout, module_)
                program.Acquire()
                program
        )
        |> unbox<DynamicShaderProgramResource>

    member x.CreateShaderProgram(pass : RenderPass, data : Aardvark.Rendering.Surface, top : IndexedGeometryMode) =
        match data with
        | Surface.FShadeSimple effect ->
            x.CreateShaderProgram(pass, effect, top)

        | Surface.FShade compile ->
            let program = x.CreateDynamicShaderProgram(pass, compile)
            program.Layout, program

        | Surface.Backend surface ->
            x.CreateShaderProgram(pass, surface)

        | Surface.None ->
            failf "encountered empty surface"

    member x.CreateStorageBuffer(scope : Ag.Scope, layout : FShade.GLSL.GLSLStorageBuffer, u : IUniformProvider, additional : SymbolDict<IAdaptiveValue>) =
        let value =
            let sem = Symbol.Create layout.ssbName

            match Uniforms.tryGetDerivedUniform layout.ssbName u with
            | Some r -> r
            | None ->
                match u.TryGetUniform(scope, sem) with
                | Some v -> v
                | None ->
                    match additional.TryGetValue sem with
                    | (true, m) -> m
                    | _ -> failwithf "[Vulkan] could not get storage buffer: %A" layout.ssbName

        let usage = VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.StorageBufferBit

        bufferCache.GetOrCreate([usage :> obj; value :> obj], fun cache key ->

            let buffer =
                AVal.custom (fun t ->
                    match value.GetValueUntyped t with
                    | :? Array as a -> ArrayBuffer(a) :> IBuffer
                    | :? IBuffer as b -> b
                    | _ -> failf "invalid storage buffer"
                )
            new BufferResource(cache, key, device, usage, buffer)
        )

    member x.CreateUniformBuffer(scope : Ag.Scope, layout : FShade.GLSL.GLSLUniformBuffer, u : IUniformProvider, additional : SymbolDict<IAdaptiveValue>) =
        let values =
            layout.ubFields
            |> List.map (fun (f) ->
                let sem = Symbol.Create f.ufName

                match Uniforms.tryGetDerivedUniform f.ufName u with
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
                let tSource = m.ContentType
                m, UniformWriters.getWriter target.ufOffset target.ufType tSource
            )

        let key = (layout :> obj) :: (values |> List.map (fun (_,v) -> v :> obj))
        uniformBufferCache.GetOrCreate(key, fun cache key -> UniformBufferResource(cache, key, device, layout, writers))

    member x.CreateDescriptorSet(layout : DescriptorSetLayout, bindings : IAdaptiveDescriptor[]) =
        descriptorSetCache.GetOrCreate([layout :> obj; bindings :> obj], fun cache key -> new DescriptorSetResource(cache, key, layout, bindings))

    member x.CreateVertexInputState(program : PipelineInfo, mode : aval<Map<Symbol, VertexInputDescription>>) =
        vertexInputCache.GetOrCreate([program :> obj; mode :> obj], fun cache key -> new VertexInputStateResource(cache, key, program, mode))

    member x.CreateInputAssemblyState(mode : IndexedGeometryMode, program : IResourceLocation<ShaderProgram>) =
        inputAssemblyCache.GetOrCreate([mode :> obj; program :> obj], fun cache key -> new InputAssemblyStateResource(cache, key, mode, program))

    member x.CreateDepthStencilState(depthTest : aval<DepthTest>, depthWrite : aval<bool>,
                                     stencilModeF : aval<StencilMode>, stencilMaskF : aval<StencilMask>,
                                     stencilModeB : aval<StencilMode>, stencilMaskB : aval<StencilMask>) =
        depthStencilCache.GetOrCreate(
            [depthTest :> obj; depthWrite :> obj;
             stencilModeF :> obj; stencilMaskF :> obj;
             stencilModeB :> obj; stencilMaskB :> obj],
            fun cache key -> new DepthStencilStateResource(cache, key,
                                                           depthTest, depthWrite,
                                                           stencilModeF, stencilMaskF,
                                                           stencilModeB, stencilMaskB)
        )

    member x.CreateRasterizerState(depthClamp : aval<bool>, depthBias : aval<DepthBias>,
                                   cull : aval<CullMode>, front : aval<WindingOrder>, fill : aval<FillMode>,
                                   conservativeRaster : aval<bool>) =
        rasterizerStateCache.GetOrCreate(
            [depthClamp :> obj; depthBias :> obj; cull :> obj; front :> obj, fill :> obj; conservativeRaster :> obj],
            fun cache key -> new RasterizerStateResource(cache, key, depthClamp, depthBias, cull, front, fill, conservativeRaster)
        )

    member x.CreateColorBlendState(pass : RenderPass,
                                   globalMask : aval<ColorMask>, attachmentMask : aval<Map<Symbol, ColorMask>>,
                                   globalBlend : aval<BlendMode>, attachmentBlend : aval<Map<Symbol, BlendMode>>,
                                   blendConstant : aval<C4f>) =
        let slots = pass.ColorAttachmentSlots

        let getAttachmentStates fallback values =
            adaptive {
                let! values = values
                let! fallback = fallback

                return Array.init slots (fun i ->
                    pass.ColorAttachments
                    |> Map.tryFind i
                    |> Option.bind (fun att -> values |> Map.tryFind att.Name)
                    |> Option.defaultValue fallback
                )
            }

        colorBlendStateCache.GetOrCreate(
            [pass.ColorAttachments :> obj; globalMask :> obj; attachmentMask :> obj; globalBlend :> obj; attachmentBlend :> obj; blendConstant :> obj],
            fun cache key ->
                let writeMasks = getAttachmentStates globalMask attachmentMask
                let blendModes = getAttachmentStates globalBlend attachmentBlend

                new ColorBlendStateResource(cache, key, writeMasks, blendModes, blendConstant)
        )

    member x.CreateMultisampleState(pass : RenderPass, multisample : aval<bool>) =
        multisampleCache.GetOrCreate(
            [pass.Samples :> obj; multisample :> obj],
            fun cache key ->
                new MultisampleStateResource(cache, key, pass.Samples, multisample)
        )

    member x.CreatePipeline(program         : IResourceLocation<ShaderProgram>,
                            pass            : RenderPass,
                            inputState      : INativeResourceLocation<VkPipelineVertexInputStateCreateInfo>,
                            inputAssembly   : INativeResourceLocation<VkPipelineInputAssemblyStateCreateInfo>,
                            rasterizerState : INativeResourceLocation<VkPipelineRasterizationStateCreateInfo>,
                            colorBlendState : INativeResourceLocation<VkPipelineColorBlendStateCreateInfo>,
                            depthStencil    : INativeResourceLocation<VkPipelineDepthStencilStateCreateInfo>,
                            multisample     : INativeResourceLocation<VkPipelineMultisampleStateCreateInfo>) =

        pipelineCache.GetOrCreate(
            [ program :> obj; pass :> obj; inputState :> obj; inputAssembly :> obj; rasterizerState :> obj; colorBlendState :> obj; depthStencil :> obj; multisample :> obj ],
            fun cache key ->
                new PipelineResource(
                    cache, key,
                    pass,
                    program,
                    inputState,
                    inputAssembly,
                    rasterizerState,
                    colorBlendState,
                    depthStencil,
                    multisample
                )

        )

    member x.CreateAccelerationStructure(instances : aset<Raytracing.ITraceInstance>,
                                         sbt : IResourceLocation<Raytracing.ShaderBindingTable>,
                                         usage : Raytracing.AccelerationStructureUsage) =

        accelerationStructureCache.GetOrCreate(
            [ instances :> obj; sbt :> obj; usage :> obj ],
            fun cache key ->
                let instanceBuffer = InstanceBuffer.create x.Device.Runtime sbt instances
                let buffer = x.CreateBuffer(instanceBuffer)

                new AccelerationStructureResource(cache, key, device, buffer, instanceBuffer.Count, usage)
        )

    member x.CreateRaytracingPipeline(program           : Raytracing.RaytracingProgram,
                                      maxRecursionDepth : aval<int>) =

        raytracingPipelineCache.GetOrCreate(
            [ program :> obj; maxRecursionDepth :> obj ],
            fun cache key ->
                new RaytracingPipelineResource(
                    cache, key,
                    program, maxRecursionDepth
                )
        )

    member x.CreateShaderBindingTable(pipeline : IResourceLocation<Raytracing.RaytracingPipeline>,
                                      hitConfigs : aval<Set<Raytracing.HitConfig>>) =

        shaderBindingTableCache.GetOrCreate(
            [ pipeline :> obj; hitConfigs :> obj ],
            fun cache key ->
                new ShaderBindingTableResource(
                    cache, key, pipeline, hitConfigs
                )
        )


    member x.CreateDrawCall(indexed : bool, calls : aval<list<DrawCallInfo>>) =
        drawCallCache.GetOrCreate([indexed :> obj; calls :> obj], fun cache key -> new DirectDrawCallResource(cache, key, indexed, calls))

    member x.CreateDrawCall(indexed : bool, calls : IResourceLocation<IndirectBuffer>) =
        drawCallCache.GetOrCreate([indexed :> obj; calls :> obj], fun cache key -> new IndirectDrawCallResource(cache, key, indexed, calls))

    member x.CreateVertexBufferBinding(buffers : list<IResourceLocation<Buffer> * int64>) =
        bufferBindingCache.GetOrCreate([buffers :> obj], fun cache key -> new BufferBindingResource(cache, key, buffers))

    member x.CreateDescriptorSetBinding(bindPoint : VkPipelineBindPoint, layout : PipelineLayout, bindings : IResourceLocation<DescriptorSet>[]) =
        descriptorBindingCache.GetOrCreate(
            [bindPoint :> obj; layout :> obj; bindings :> obj],
            fun cache key -> new DescriptorSetBindingResource(cache, key, bindPoint, layout, bindings)
        )

    member x.CreateIndexBufferBinding(binding : IResourceLocation<Buffer>, t : VkIndexType) =
        indexBindingCache.GetOrCreate([binding :> obj; t :> obj], fun cache key -> new IndexBufferBindingResource(cache, key, t, binding))

    member x.CreateIsActive(value : aval<bool>) =
        isActiveCache.GetOrCreate([value :> obj], fun cache key -> IsActiveResource(cache, key, value))

    interface IDisposable with
        member x.Dispose() = x.Dispose()


type ResourceLocationReader(resource : IResourceLocation) =
    inherit AdaptiveObject()

    let mutable lastVersion = 0

    member x.Dispose() =
        lock resource (fun () ->
            resource.Outputs.Remove x |> ignore
        )

    member x.Update(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
        x.EvaluateIfNeeded token false (fun token ->
            let info = resource.Update(user, token, renderToken)
            if info.version <> lastVersion then
                lastVersion <- info.version
                true
            else
                false
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AutoOpen>]
module ``Resource Reader Extensions`` =
    type IResourceLocation with
        member x.GetReader() = new ResourceLocationReader(x)


type ResourceLocationSet() =
    inherit AdaptiveObject()

    // All the resource locations in the set.
    let locations = ReferenceCountingSet<IResourceLocation>()

    // Readers of the resource locations.
    let readers = Dict<IResourceLocation, ResourceLocationReader>()

    // Set of dirty readers for each priority.
    let dirty = LockedSet<ResourceLocationReader>()

    // Dictionary of potentially used resource handles for each location.
    let resources = Dictionary<IResourceLocation, Resource>()

    // Called when a resource location produces a resource handle.
    // Increments its reference count to prevent it from being disposed before or while we use it.
    let acquireResource (l : IResourceLocation) (r : Resource) =
        lock resources (fun _ ->
            r.AddReference()
            resources.[l] <- r
        )

    // Called before a potentially new resource handle is produced for the given location.
    // Releases ownership of the current handle, so it can be disposed before the new handle is created.
    let releaseResource (l : IResourceLocation) =
        lock resources (fun _ ->
            match resources.TryGetValue l with
            | (true, r) -> r.Dispose()
            | _ -> ()
        )

    // Updates all pending resource readers and returns if a resource has changed.
    member private x.Update(token : AdaptiveToken, renderToken : RenderToken) =
        let rec run (changed : bool) =
            let mine = dirty.GetAndClear()

            if mine.Count > 0 then
                let mutable changed = changed

                for r in mine do
                    let c = r.Update(x, token, renderToken)
                    changed <- changed || c

                run changed
            else
                changed

        dirty |> LockedSet.lock (fun _ ->
            run false
        )

    member private x.AddInput(r : IResourceLocation) =
        let reader = r.GetReader()
        lock readers (fun () -> readers.[r] <- reader)
        dirty.Add reader |> ignore
        transact (fun () -> x.MarkOutdated())

    member private x.RemoveInput(r : IResourceLocation) =
        match lock readers (fun () -> readers.TryRemove r) with
        | (true, reader) ->
            reader.Outputs.Remove(x) |> ignore
            dirty.Remove reader |> ignore
            reader.Dispose()
        | _ ->
            ()

    override x.InputChangedObject(_, i) =
        match i with
        | :? ResourceLocationReader as r -> dirty.Add r |> ignore
        | _ -> ()

    member x.Add(r : IResourceLocation) =
        if lock locations (fun () -> locations.Add r) then
            lock r r.Acquire
            x.AddInput r

    member x.Remove(r : IResourceLocation) =
        if lock locations (fun () -> locations.Remove r) then
            lock r r.Release
            x.RemoveInput r

    /// Updates all the resources in the set, calling the given action afterwards.
    /// The boolean flag indicates if any resource has been changed.
    member x.Use(token : AdaptiveToken, renderToken : RenderToken, action : bool -> 'T) =
        x.EvaluateAlways token (fun token ->

            // Try to acquire all resource handles that are potentially used.
            lock resources (fun _ ->
                let invalid = System.Collections.Generic.HashSet()

                for KeyValue(l, r) in resources do
                    if not <| r.TryAddReference() then
                        invalid.Add l |> ignore

                // Mark locations with invalid handles as outdated to ensure
                // their readers are added to the dirty set.
                transact (fun _ ->
                    for l in invalid do
                        l.MarkOutdated()
                        resources.Remove l |> ignore
                )
            )

            try
                let changed = x.Update(token, renderToken)
                action changed

            finally
                // Release all resource handles so they can be disposed again.
                lock resources (fun _ ->
                    for KeyValue(_, r) in resources do
                        r.Dispose()
                )
        )

    interface IResourceUser with
        member x.Acquire(location, handle) =
            match handle with
            | :? Resource as r -> r |> acquireResource location
            | _ -> ()

        member x.Release(location) =
            releaseResource location