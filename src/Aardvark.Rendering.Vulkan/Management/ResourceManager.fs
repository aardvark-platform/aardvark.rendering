namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.Raytracing
open FSharp.Data.Adaptive
open Microsoft.FSharp.NativeInterop

open EXTConservativeRasterization

#nowarn "9"
#nowarn "51"

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

and IConstantResourceLocation<'T> =
    inherit IResourceLocation
    abstract member Handle : 'T

and IResourceCache =
    abstract member Device : Device
    abstract member Remove : key : list<obj> -> unit

type INativeResourcePointer<'T when 'T : unmanaged> =
    inherit IResourceLocation
    abstract member Pointer : nativeptr<'T>

type INativeResourceLocation<'T, 'V when 'V : unmanaged> =
    inherit IResourceLocation<'T>
    inherit INativeResourcePointer<'V>

type INativeResourceLocation<'T when 'T : unmanaged> =
    INativeResourceLocation<'T, 'T>


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

type ImmutableResourceLocation<'Input, 'Handle>(owner : IResourceCache, key : list<obj>,
                                                input : aval<'Input>, desc : ImmutableResourceDescription<'Input, 'Handle>) =
    inherit AbstractResourceLocation<'Handle>(owner, key)

    let mutable handle : ValueOption<'Input * 'Handle> = ValueNone
    let mutable version = 0

    let recreate (user : IResourceUser) (token : AdaptiveToken) (renderToken : RenderToken) =
        let n = input.GetValue(user, token, renderToken)

        match handle with
        | ValueSome(o, h) when Unchecked.equals o n -> h
        | ValueSome(_, h) ->
            desc.idestroy h
            let r = desc.icreate n
            handle <- ValueSome(n,r)
            inc &version
            r
        | ValueNone ->
            let r = desc.icreate n
            handle <- ValueSome(n,r)
            inc &version
            r

    member private x.EagerDestroy() =
        if Monitor.TryEnter x then
            try
                // This is executed at the end of the transaction that marked the object.
                // At this point, we may already have updated (i.e. OutOfDate = false).
                // In that case we would be destroying the new handle.
                if x.OutOfDate then
                    handle |> ValueOption.iter (snd >> desc.idestroy)
                    handle <- ValueNone
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
        | ValueSome (_, h) ->
            desc.idestroy h
            handle <- ValueNone
        | ValueNone ->
            ()
        input.Release()

    override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
        if x.OutOfDate then
            let handle = recreate user token renderToken
            { handle = handle; version = version }
        else
            match handle with
            | ValueSome(_,h) -> { handle = h; version = version }
            | ValueNone -> failwith "[Resource] inconsistent state"

type MutableResourceLocation<'Input, 'Handle>(owner : IResourceCache, key : list<obj>,
                                              input : aval<'Input>, desc : MutableResourceDescription<'Input, 'Handle>) =
    inherit AbstractResourceLocation<'Handle>(owner, key)

    let mutable handle : ValueOption<'Input * 'Handle> = ValueNone
    let mutable version = 0

    let recreate (n : 'Input) =
        match handle with
        | ValueSome(_, h) ->
            desc.mdestroy h
            let r = desc.mcreate n
            handle <- ValueSome(n,r)
            r
        | ValueNone ->
            let r = desc.mcreate n
            handle <- ValueSome(n,r)
            r

    let update (user : IResourceUser) (token : AdaptiveToken) (renderToken : RenderToken) =
        let n = input.GetValue(user, token, renderToken)

        match handle with
        | ValueNone ->
            inc &version
            recreate n

        | ValueSome(oa, oh) when Unchecked.equals oa n ->
            oh

        | ValueSome(_, oh) ->
            if desc.mtryUpdate oh n then
                handle <- ValueSome(n, oh)
                oh
            else
                inc &version
                recreate n

    override x.Create() =
        input.Acquire()

    override x.Destroy() =
        input.Outputs.Remove x |> ignore
        match handle with
        | ValueSome(_, h) ->
            desc.mdestroy h
            handle <- ValueNone
        | ValueNone ->
            ()
        input.Release()

    override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
        if x.OutOfDate then
            let handle = update user token renderToken
            { handle = handle; version = version }
        else
            match handle with
            | ValueSome(_, h) -> { handle = h; version = version }
            | ValueNone -> failwith "[Resource] inconsistent state"

[<AbstractClass>]
type AbstractPointerResource<'T when 'T : unmanaged>(owner : IResourceCache, key : list<obj>) =
    inherit AbstractResourceLocation<'T>(owner, key)

    let mutable ptr = NativePtr.zero
    let mutable version = 0
    let mutable hasHandle = false

    abstract member Update : handle: 'T byref *  user: IResourceUser * token: AdaptiveToken * renderToken: RenderToken -> bool
    abstract member Free : handle: 'T inref -> unit
    default _.Free _ = ()

    member x.Pointer = ptr
    member x.HasHandle = hasHandle
    member x.Handle = NativePtr.toByRef ptr

    override x.Create() =
        ptr <- NativePtr.alloc 1

    override x.Destroy() =
        if hasHandle then
            x.Free &x.Handle

        NativePtr.free ptr
        hasHandle <- false

    override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
        if x.OutOfDate then
            let changed = x.Update(&x.Handle, user, token, renderToken)
            if changed || not hasHandle then inc &version
            hasHandle <- true

        { handle = NativePtr.read ptr; version = version }

    interface INativeResourceLocation<'T> with
        member x.Pointer = x.Pointer

[<AbstractClass>]
type AbstractResourceLocationWithPointer<'T, 'V when 'T :> Resource<'V> and 'V : equality and 'V : unmanaged>(owner : IResourceCache, key : list<obj>) =
    inherit AbstractResourceLocation<'T>(owner, key)

    let mutable ptr = NativePtr.zero<'V>

    abstract member Compute : user: IResourceUser * token: AdaptiveToken * renderToken: RenderToken -> ResourceInfo<'T>

    override x.Create() =
        ptr <- NativePtr.alloc 1

    override x.Destroy() =
        if not <| NativePtr.isNull ptr then
            NativePtr.free ptr
            ptr <- NativePtr.zero

    override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
        let info = x.Compute(user, token, renderToken)
        info.handle.Handle |> NativePtr.write ptr
        info

    interface INativeResourceLocation<'T, 'V> with
        member x.Pointer = ptr


module Resources =

    [<Struct>]
    type ImageSampler =
        internal {
            Image   : ImageView
            Sampler : Sampler
        }

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
                Descriptor.StorageBuffer(slot, buffer, 0UL, buffer.Size)

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
                    let { Image = v; Sampler = s } = images.[i].handle
                    let desc = Descriptor.CombinedImageSampler(slot, i, v, s, v.Image.SamplerLayout)
                    cache.[i] <- { Version = images.[i].version; Descriptor = desc }

    type BufferResource(owner : IResourceCache, key : list<obj>, name : string, device : Device, usage : VkBufferUsageFlags, input : aval<IBuffer>) =
        inherit MutableResourceLocation<IBuffer, Buffer>(
            owner, key,
            input,
            {
                mcreate          = fun (b : IBuffer) -> let r = device.CreateBuffer(usage, b) in (if name <> null && r.Name = null then r.Name <- name); r
                mdestroy         = _.Dispose()
                mtryUpdate       = fun (b : Buffer) (v : IBuffer) -> Buffer.tryUpdate v b
            }
        )

    type IndirectBufferResource(owner : IResourceCache, key : list<obj>, name : string, device : Device, indexed : bool, input : aval<Aardvark.Rendering.IndirectBuffer>) =
        inherit ImmutableResourceLocation<Aardvark.Rendering.IndirectBuffer, IndirectBuffer>(
            owner, key,
            input,
            {
                icreate         = fun b -> let r = device.CreateIndirectBuffer(indexed, b) in (if name <> null && r.Name = null then r.Name <- name); r
                idestroy        = _.Dispose()
                ieagerDestroy   = false
            }
        )

    type UniformBufferResource(owner : IResourceCache, key : list<obj>, device : Device,
                               layout : FShade.GLSL.GLSLUniformBuffer, writers : struct (IAdaptiveValue * UniformWriters.IWriter)[]) =
        inherit AbstractResourceLocation<UniformBuffer>(owner, key)

        let mutable handle : UniformBuffer = Unchecked.defaultof<_>
        let name = if device.DebugLabelsEnabled then $"{layout.ubName} (Uniform Buffer)" else null

        member x.Handle = handle

        override x.Create() =
            handle <- device.CreateUniformBuffer(layout)
            handle.Name <- name

        override x.Destroy() =
            if handle <> Unchecked.defaultof<_> then
                handle.Dispose()
                handle <- Unchecked.defaultof<_>

        override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            if x.OutOfDate then
                for value, writer in writers do
                    writer.Write(token, value, handle.Storage.Pointer)

                device.Upload handle

                { handle = handle; version = 0 }
            else
                { handle = handle; version = 0 }

    type PushConstantsResource(owner : IResourceCache, key : list<obj>,
                               layout : PushConstantsLayout, writers : struct (IAdaptiveValue * UniformWriters.IWriter)[]) =
        inherit AbstractResourceLocation<PushConstants>(owner, key)

        let mutable handle = Unchecked.defaultof<PushConstants>
        let mutable version = 0

        override x.Create() =
            handle <- new PushConstants(layout)

        override x.Destroy() =
            if handle <> Unchecked.defaultof<_> then
                handle.Dispose()
                handle <- Unchecked.defaultof<_>

        override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            if x.OutOfDate then
                for value, writer in writers do
                    writer.Write(token, value, handle.Pointer.Address)

                inc &version

            { handle = handle; version = version }

        interface IConstantResourceLocation<PushConstants> with
            member _.Handle = handle

    type ImageResource(owner : IResourceCache, key : list<obj>, name : string, device : Device, properties : ImageProperties, input : aval<ITexture>) =
        inherit ImmutableResourceLocation<ITexture, Image>(
            owner, key,
            input,
            {
                icreate         = fun t -> let r = device.CreateImage(t, properties) in (if name <> null && r.Name = null then r.Name <- name); r
                idestroy        = _.Dispose()
                ieagerDestroy   = true
            }
        )

    type DynamicSamplerStateResource(owner : IResourceCache, key : list<obj>,
                                     state : SamplerState, modifier : aval<SamplerState -> SamplerState>) =
        inherit AbstractResourceLocation<SamplerState>(owner, key)

        let mutable cache = ValueNone

        override x.Create() =
            modifier.Acquire()

        override x.Destroy() =
            modifier.Release()

        override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            if x.OutOfDate then
                let f = modifier.GetValue(user, token, renderToken)
                cache <- ValueSome (f state)

            match cache with
            | ValueSome s -> { handle = s; version = 0 }
            | _ -> failwith "[Resource] inconsistent state"

    type ImageSamplerResource(owner : IResourceCache, key : list<obj>, device : Device, imageView : IResourceLocation<ImageView>, samplerDesc : aval<SamplerState>) =
        inherit AbstractResourceLocation<ImageSampler>(owner, key)

        let mutable samplerVersion = 0
        let mutable sampler : Sampler voption = ValueNone

        let createSampler (format : VkFormat) (samplerDesc : SamplerState) =
            let handle = device.CreateSampler(samplerDesc, format)
            sampler <- ValueSome handle
            inc &samplerVersion
            handle

        let mutable cache : ResourceInfo<ImageSampler> = Unchecked.defaultof<_>

        override x.Create() =
            imageView.Acquire()
            samplerDesc.Acquire()

        override x.Destroy() =
            sampler |> ValueOption.iter _.Dispose()
            sampler <- ValueNone
            cache <- Unchecked.defaultof<_>
            samplerDesc.Release()
            imageView.Release()

        override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            if x.OutOfDate then
                let v = imageView.Update(user, token, renderToken)
                let s = samplerDesc.GetValue(user, token, renderToken)
                let fmt = v.handle.Image.Format

                let sampler =
                    match sampler with
                    | ValueSome old when old.Format = fmt && old.Description = s -> old

                    | ValueSome old ->
                        old.Dispose()
                        createSampler fmt s

                    | ValueNone ->
                        createSampler fmt s

                let handle = { Image = v.handle; Sampler = sampler }
                cache <- { handle = handle; version = v.version + samplerVersion }

            cache

    type ImageSamplerArrayResource(owner : IResourceCache, key : list<obj>, count : int,
                                   empty : IResourceLocation<ImageSampler>, input : amap<int, IResourceLocation<ImageSampler>>) as this =
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
                r.Outputs.Remove this |> ignore
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
                let removals = List()
                let additions = List()

                let deltas = reader.GetChanges token
                for i, op in deltas do
                    let r =
                        match op with
                        | Set r -> r
                        | Remove -> empty

                    removals.Add(struct (images.[i], i))
                    additions.Add(struct (r, i))

                for r, i in additions do
                    set i r

                for r, i in removals do
                    remove i r

                // Process pending inputs (i.e. image samplers)
                // We check the new version each handle to detect if anything actually changed.
                let mutable changed = deltas.Count > 0

                for image in pending.GetAndClear() do
                    if indices.Contains image then
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
        inherit AbstractPointerResource<VkPipelineInputAssemblyStateCreateInfo>(owner, key)

        override x.Create() =
            base.Create()
            program.Acquire()

        override x.Destroy() =
            program.Release()
            base.Destroy()

        override x.Update(handle, user, token, renderToken) =
            let info = program.Update(user, token, renderToken)
            let state = input |> InputAssemblyState.ofIndexedGeometryMode info.handle.HasTessellation

            let result =
                VkPipelineInputAssemblyStateCreateInfo(
                    VkPipelineInputAssemblyStateCreateFlags.None,
                    state.topology,
                    VkBool32.ofBool state.restartEnable
                )

            if result <> handle then
                handle <- result
                true
            else
                false

    type VertexInputStateResource(owner : IResourceCache, key : list<obj>, prog : PipelineInfo, input : aval<Map<Symbol, VertexInputDescription>>) =
        inherit AbstractPointerResource<VkPipelineVertexInputStateCreateInfo>(owner, key)

        override x.Update(handle, user, token, renderToken) =
            let state = input.GetValue(user, token, renderToken)

            let inputs = prog.pInputs |> List.sortBy _.paramLocation

            let paramsWithInputs =
                inputs |> List.map (fun p ->
                    let sem = Symbol.Create p.paramSemantic
                    match Map.tryFind sem state with
                    | Some desc -> struct (p.paramLocation, desc)
                    | None -> failf "could not get vertex input-type for %A" p
                )

            let inputBindings =
                paramsWithInputs |> List.mapi (fun i struct (_, desc) ->
                    VkVertexInputBindingDescription(
                        uint32 i,
                        uint32 desc.stride,
                        desc.stepRate
                    )
                ) |> List.toArray

            let inputAttributes =
                paramsWithInputs |> List.collecti (fun binding struct (location, desc) ->
                    desc.offsets |> List.mapi (fun i offset ->
                        VkVertexInputAttributeDescription(
                            uint32 (location + i),
                            uint32 binding,
                            desc.inputFormat,
                            uint32 offset
                        )
                    )
                ) |> List.toArray

            let pInputBindings = NativePtr.alloc inputBindings.Length
            let pInputAttributes = NativePtr.alloc inputAttributes.Length

            for i in 0 .. inputBindings.Length - 1 do
                NativePtr.set pInputBindings i inputBindings.[i]

            for i in 0 .. inputAttributes.Length - 1 do
                NativePtr.set pInputAttributes i inputAttributes.[i]

            if x.HasHandle then
                x.Free &handle

            handle <-
                VkPipelineVertexInputStateCreateInfo(
                    VkPipelineVertexInputStateCreateFlags.None,

                    uint32 inputBindings.Length,
                    pInputBindings,

                    uint32 inputAttributes.Length,
                    pInputAttributes
                )

            true

        override x.Free(handle) =
            NativePtr.free handle.pVertexBindingDescriptions
            NativePtr.free handle.pVertexAttributeDescriptions


    type DepthStencilStateResource(owner : IResourceCache, key : list<obj>,
                                   depthTest : aval<DepthTest>, depthWrite : aval<bool>,
                                   stencilModeF : aval<StencilMode>, stencilMaskF : aval<StencilMask>,
                                   stencilModeB : aval<StencilMode>, stencilMaskB : aval<StencilMask>) =
        inherit AbstractPointerResource<VkPipelineDepthStencilStateCreateInfo>(owner, key)

        override x.Update(handle, user, token, renderToken) =
            let depthTest = depthTest.GetValue(user, token, renderToken)
            let depthWrite = depthWrite.GetValue(user, token, renderToken)

            let stencilMaskF = stencilMaskF.GetValue(user, token, renderToken)
            let stencilModeF = stencilModeF.GetValue(user, token, renderToken)
            let stencilMaskB = stencilMaskB.GetValue(user, token, renderToken)
            let stencilModeB = stencilModeB.GetValue(user, token, renderToken)

            let depth = DepthState.create depthWrite depthTest
            let stencil = StencilState.create stencilMaskF stencilMaskB stencilModeF stencilModeB

            let result =
                VkPipelineDepthStencilStateCreateInfo(
                    VkPipelineDepthStencilStateCreateFlags.None,
                    VkBool32.ofBool depth.testEnabled,
                    VkBool32.ofBool depth.writeEnabled,
                    depth.compare,
                    VkBool32.ofBool depth.boundsTest,
                    VkBool32.ofBool stencil.enabled,
                    stencil.front,
                    stencil.back,
                    float32 depth.depthBounds.Min,
                    float32 depth.depthBounds.Max
                )

            if result <> handle then
                handle <- result
                true
            else
                false

    type RasterizerStateResource(owner : IResourceCache, key : list<obj>,
                                 depthClamp : aval<bool>, depthBias : aval<DepthBias>,
                                 cull : aval<CullMode>, frontFace : aval<WindingOrder>, fill : aval<FillMode>,
                                 conservativeRaster : aval<bool>) =
        inherit AbstractPointerResource<VkPipelineRasterizationStateCreateInfo>(owner, key)

        let supportsConservativeRaster = owner.Device.IsExtensionEnabled EXTConservativeRasterization.Name
        let mutable conservativeState = VkPipelineRasterizationConservativeStateCreateInfoEXT.Empty

        override x.Update(handle, user, token, renderToken) =
            let depthClamp = depthClamp.GetValue(user, token, renderToken)
            let bias = depthBias.GetValue(user, token, renderToken)
            let cull = cull.GetValue(user, token, renderToken)
            let front = frontFace.GetValue(user, token, renderToken)
            let fill = fill.GetValue(user, token, renderToken)
            let conservativeRaster = conservativeRaster.GetValue(user, token, renderToken)
            let state = RasterizerState.create conservativeRaster depthClamp bias cull front fill

            let pConservativeState =
                if supportsConservativeRaster && conservativeRaster then
                    conservativeState <-
                        VkPipelineRasterizationConservativeStateCreateInfoEXT(
                            VkPipelineRasterizationConservativeStateCreateFlagsEXT.None,
                            VkConservativeRasterizationModeEXT.Overestimate,
                            0.0f
                        )

                    &&conservativeState
                else
                    NativePtr.zero

            handle <-
                VkPipelineRasterizationStateCreateInfo(
                    pConservativeState.Address,
                    VkPipelineRasterizationStateCreateFlags.None,
                    VkBool32.ofBool state.depthClampEnable,
                    VkBool32.ofBool state.rasterizerDiscardEnable,
                    state.polygonMode,
                    state.cullMode,
                    state.frontFace,
                    VkBool32.ofBool state.depthBiasEnable,
                    float32 state.depthBiasConstantFactor,
                    float32 state.depthBiasClamp,
                    float32 state.depthBiasSlopeFactor,
                    float32 state.lineWidth
                )

            true

    type ColorBlendStateResource(owner : IResourceCache, key : list<obj>,
                                 writeMasks : aval<ColorMask[]>, blendModes : aval<BlendMode[]>, blendConstant : aval<C4f>, blendSupported : bool[]) =
        inherit AbstractPointerResource<VkPipelineColorBlendStateCreateInfo>(owner, key)

        let attachmentCount = blendSupported.Length
        let mutable pAttachmentStates = NativePtr.zero<VkPipelineColorBlendAttachmentState>

        override x.Create() =
            base.Create()
            pAttachmentStates <- NativePtr.alloc attachmentCount

        override x.Destroy() =
            NativePtr.free pAttachmentStates
            base.Destroy()

        override x.Update(handle, user, token, renderToken) =
            let writeMasks = writeMasks.GetValue(user, token, renderToken)
            let blendModes = blendModes.GetValue(user, token, renderToken)
            let blendConstant = blendConstant.GetValue(user, token, renderToken)
            let state = ColorBlendState.create writeMasks blendModes blendConstant

            for i in 0 .. attachmentCount - 1 do
                let s = state.attachmentStates.[i]
                pAttachmentStates.[i] <-
                    VkPipelineColorBlendAttachmentState(
                        VkBool32.ofBool (s.enabled && blendSupported.[i]),
                        s.srcFactor,
                        s.dstFactor,
                        s.operation,
                        s.srcFactorAlpha,
                        s.dstFactorAlpha,
                        s.operationAlpha,
                        s.colorWriteMask
                    )

            handle <-
                VkPipelineColorBlendStateCreateInfo(
                    VkPipelineColorBlendStateCreateFlags.None,
                    (if state.logicOpEnable then 1u else 0u),
                    state.logicOp,
                    uint32 writeMasks.Length,
                    pAttachmentStates,
                    state.constant
                )

            true

    // TODO: Sample shading
    type MultisampleStateResource(owner : IResourceCache, key : list<obj>, samples : int, enable : aval<bool>) =
        inherit AbstractPointerResource<VkPipelineMultisampleStateCreateInfo>(owner, key)

        override x.Update(handle, user, token, renderToken) =
            //let enable = enable.GetValue token

            // TODO: Cannot disable MSAA here...
            //let samples = if enable then samples else 1
            let state = MultisampleState.create false samples

            let result =
                VkPipelineMultisampleStateCreateInfo(
                    VkPipelineMultisampleStateCreateFlags.None,
                    enum state.samples,
                    VkBool32.ofBool state.sampleShadingEnable,
                    float32 state.minSampleShading,
                    NativePtr.zero,
                    VkBool32.ofBool state.alphaToCoverageEnable,
                    VkBool32.ofBool state.alphaToOneEnable
                )

            if result <> handle then
                handle <- result
                true
            else
                false

    type DirectDrawCallResource(owner : IResourceCache, key : list<obj>, indexed : bool, calls : aval<DrawCallInfo[]>) =
        inherit AbstractPointerResource<DrawCall>(owner, key)

        override x.Free(handle : DrawCall inref) =
            handle.Dispose()

        override x.Update(handle, user, token, renderToken) =
            let calls = calls.GetValue(user, token, renderToken)
            if x.HasHandle then x.Free &handle
            handle <- DrawCall.Direct(indexed, calls)
            true

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

        let device = renderPass.Device

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

        override x.Update(handle : VkPipeline byref, user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            let prog = program.Update(user, token, renderToken).handle

            use pShaderCreateInfos = fixed prog.ShaderCreateInfos

            let mutable viewportState =
                let vp  =
                    if device.IsDeviceGroup then
                        if renderPass.LayerCount > 1 then 1u
                        else uint32 device.PhysicalDevices.Length
                    else
                        1u

                VkPipelineViewportStateCreateInfo(
                    VkPipelineViewportStateCreateFlags.None,
                    uint32 vp, NativePtr.zero,
                    uint32 vp, NativePtr.zero
                )

            let dynamicStates = [| VkDynamicState.Viewport; VkDynamicState.Scissor |]
            use pDynamicStates = fixed dynamicStates

            let mutable tessStateInfo =
                VkPipelineTessellationStateCreateInfo(
                    VkPipelineTessellationStateCreateFlags.None,
                    uint32 prog.TessellationPatchSize
                )

            let pTessState =
                if prog.HasTessellation then &&tessStateInfo
                else NativePtr.zero

            let mutable dynamicStateCreateInfo =
                VkPipelineDynamicStateCreateInfo(
                    VkPipelineDynamicStateCreateFlags.None,
                    uint32 dynamicStates.Length,
                    pDynamicStates
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
                    handle, VkPipelineCreateFlags.DerivativeBit

            let mutable result = VkPipeline.Null
            let mutable createInfo =
                VkGraphicsPipelineCreateInfo(
                    VkPipelineCreateFlags.AllowDerivativesBit ||| derivativeFlag,
                    uint32 prog.ShaderCreateInfos.Length,
                    pShaderCreateInfos,
                    inputState,
                    inputAssembly,
                    pTessState,
                    &&viewportState,
                    rasterizerState,
                    multisample,
                    depthStencil,
                    colorBlendState,
                    &&dynamicStateCreateInfo, //dynamic
                    prog.PipelineLayout.Handle,
                    renderPass.Handle,
                    0u,
                    basePipeline,
                    -1
                )

            VkRaw.vkCreateGraphicsPipelines(device.Handle, VkPipelineCache.Null, 1u, &&createInfo, NativePtr.zero, &&result)
                |> check "could not create pipeline"

            if x.HasHandle then
                x.Free &handle

            handle <- result
            true

        override x.Free(handle : VkPipeline inref) =
            VkRaw.vkDestroyPipeline(renderPass.Device.Handle, handle, NativePtr.zero)

    type IndirectDrawCallResource(owner : IResourceCache, key : list<obj>, indexed : bool, calls : IResourceLocation<IndirectBuffer>) =
        inherit AbstractPointerResource<DrawCall>(owner, key)

        override x.Create() =
            base.Create()
            calls.Acquire()

        override x.Destroy() =
            base.Destroy()
            calls.Release()

        override x.Update(handle : DrawCall byref, user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            let calls = calls.Update(user, token, renderToken)
            if x.HasHandle then x.Free &handle
            handle <- DrawCall.Indirect(indexed, calls.handle.Handle, calls.handle.Count)
            true

        override x.Free(handle : DrawCall inref) =
            handle.Dispose()

    type BufferBindingResource(owner : IResourceCache, key : list<obj>, buffers : IResourceLocation<Buffer>[]) =
        inherit AbstractPointerResource<VertexBufferBinding>(owner, key)

        override x.Create() =
            base.Create()
            for b in buffers do b.Acquire()
            x.Handle <- new VertexBufferBinding(count = buffers.Length)
            for i = 0 to buffers.Length - 1 do x.Handle.Offsets.[i] <- 0UL

        override x.Destroy() =
            x.Handle.Dispose()
            for b in buffers do b.Release()
            base.Destroy()

        override x.Update(handle : VertexBufferBinding byref, user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            let mutable changed = false

            for i = 0 to buffers.Length - 1 do
                let buffer = buffers.[i].Update(user, token, renderToken).handle.Handle

                if handle.Buffers.[i] <> buffer then
                    handle.Buffers.[i] <- buffer
                    changed <- true

            changed

    type DescriptorSetBindingResource(owner : IResourceCache, key : list<obj>,
                                      bindPoint : VkPipelineBindPoint, layout : PipelineLayout, sets : IResourceLocation<DescriptorSet>[]) =
        inherit AbstractPointerResource<DescriptorSetBinding>(owner, key)

        let mutable versions = Array.replicate sets.Length -1

        override x.Create() =
            base.Create()
            for s in sets do s.Acquire()
            x.Handle <- new DescriptorSetBinding(bindPoint, layout.Handle, count = sets.Length)

        override x.Destroy() =
            x.Handle.Dispose()
            Array.fill versions 0 versions.Length -1
            for s in sets do s.Release()
            base.Destroy()

        override x.Update(handle : DescriptorSetBinding byref, user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            let mutable changed = false

            for i in 0 .. sets.Length - 1 do
                let info = sets.[i].Update(user, token, renderToken)
                handle.Sets[i] <- info.handle.Handle

                if info.version <> versions.[i] then
                    versions.[i] <- info.version
                    changed <- true

            changed

    type IndexBufferBindingResource(owner : IResourceCache, key : list<obj>, indexType : VkIndexType, indexBuffer : IResourceLocation<Buffer>) =
        inherit AbstractPointerResource<IndexBufferBinding>(owner, key)

        override x.Create() =
            base.Create()
            indexBuffer.Acquire()

        override x.Destroy() =
            base.Destroy()
            indexBuffer.Release()

        override x.Update(handle : IndexBufferBinding byref, user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            let indexBuffer = indexBuffer.Update(user, token, renderToken).handle.Handle
            if handle.Buffer <> indexBuffer then
                handle <- IndexBufferBinding(indexBuffer, indexType)
                true
            else
                false

    [<AbstractClass>]
    type AbstractImageViewResource(owner : IResourceCache, key : list<obj>, image : IResourceLocation<Image>, levels : aval<Range1i>, slices : aval<Range1i>) =
        inherit AbstractResourceLocation<ImageView>(owner, key)

        let mutable handle : ImageView voption = ValueNone
        let mutable version = 0

        abstract member CreateImageView : image: Image * levels: Range1i * slices: Range1i * mapping: VkComponentMapping -> ImageView

        override x.Create() =
            image.Acquire()
            levels.Acquire()
            slices.Acquire()

        override x.Destroy() =
            handle |> ValueOption.iter Disposable.dispose
            handle <- ValueNone
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
                    | ValueSome h -> h.Image = image.handle && h.MipLevelRange = levels && h.ArrayRange = slices
                    | ValueNone -> false

                if isIdentical then
                    { handle = handle.Value; version = version }
                else
                    match handle with
                    | ValueSome h -> h.Dispose()
                    | ValueNone -> ()

                    let mapping =
                        if VkFormat.toColFormat image.handle.Format = Col.Format.Gray then
                            VkComponentMapping(VkComponentSwizzle.R, VkComponentSwizzle.R, VkComponentSwizzle.R, VkComponentSwizzle.A)
                        else
                            VkComponentMapping.Identity

                    let h = x.CreateImageView(image.handle, levels, slices, mapping)
                    handle <- ValueSome h
                    inc &version

                    { handle = h; version = version }
            else
                match handle with
                | ValueSome h -> { handle = h; version = version }
                | ValueNone -> failwith "[Resource] inconsistent state"

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
        inherit AbstractPointerResource<int>(owner, key)

        override x.Update(handle : int byref, user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
            handle <- if input.GetValue(user, token, renderToken) then 1 else 0
            true

    module Raytracing =
        open Aardvark.Rendering.Raytracing

        type AccelerationStructureResource(owner : IResourceCache, key : list<obj>, name : string, device : Device,
                                           instanceBuffer : IResourceLocation<Buffer>, instanceCount : aval<int>, usage : AccelerationStructureUsage) =
            inherit AbstractResourceLocation<AccelerationStructure>(owner, key)

            let mutable handle : AccelerationStructure voption = ValueNone
            let mutable version = 0

            let create (data : AccelerationStructureData) =
                let acc = AccelerationStructure.create device usage data
                if name <> null then acc.Name <- name
                handle <- ValueSome acc
                inc &version
                { handle = acc; version = version }

            override x.Create() =
                instanceBuffer.Acquire()

            override x.Destroy() =
                match handle with
                | ValueSome h ->
                    h.Dispose()
                    handle <- ValueNone
                | ValueNone -> ()

                instanceBuffer.Release()

            override x.GetHandle(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
                if x.OutOfDate then
                    let buffer = instanceBuffer.Update(user, token, renderToken).handle
                    let count = instanceCount.GetValue(user, token, renderToken)
                    let data = AccelerationStructureData.Instances (uint32 count, buffer)

                    match handle with
                    | ValueNone -> create data

                    | ValueSome old ->
                        if old |> AccelerationStructure.tryUpdate data then
                            { handle = old; version = version }
                        else
                            old.Dispose()
                            create data
                else
                    match handle with
                    | ValueSome h -> { handle = h; version = version }
                    | ValueNone -> failwith "[Resource] inconsistent state"


        type RaytracingPipelineResource(owner : IResourceCache, key : list<obj>,
                                        program : RaytracingProgram, maxRecursionDepth : aval<int>) =
            inherit AbstractResourceLocationWithPointer<RaytracingPipeline, VkPipeline>(owner, key)

            let mutable handle : (RaytracingPipelineDescription * RaytracingPipeline) voption = ValueNone
            let mutable version = 0

            let device = program.Device

            let recursionDepthLimit =
                device.Runtime.MaxRayRecursionDepth

            let destroy() =
                handle |> ValueOption.iter (snd >> Disposable.dispose)
                handle <- ValueNone

            let create description =
                inc &version
                let basePipeline = handle |> ValueOption.map snd
                let pipeline = description |> RaytracingPipeline.create device basePipeline
                handle <- ValueSome (description, pipeline)
                basePipeline |> ValueOption.iter Disposable.dispose
                { handle = pipeline; version = version }

            override x.Create() =
                base.Create()

            override x.Destroy() =
                destroy()
                base.Destroy()

            override x.Compute(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
                if x.OutOfDate then
                    let depth = maxRecursionDepth.GetValue(user, token, renderToken) |> min recursionDepthLimit
                    let description = { Program = program; MaxRecursionDepth = uint32 depth }

                    match handle with
                    | ValueSome (o, p) when description = o ->
                        { handle = p; version = version }
                    | _ ->
                        create description

                else
                    match handle with
                    | ValueSome (_, h) -> { handle = h; version = version }
                    | ValueNone -> failwith "[Resource] inconsistent state"


        type ShaderBindingTableResource(owner : IResourceCache, key : list<obj>,
                                        pipeline : IResourceLocation<RaytracingPipeline>, hitConfigs : aval<Set<Symbol[]>>) =
            inherit AbstractResourceLocationWithPointer<ShaderBindingTable, ShaderBindingTableHandle>(owner, key)

            let mutable handle : ShaderBindingTable voption = ValueNone
            let mutable version = 0

            let destroy() =
                handle |> ValueOption.iter Disposable.dispose
                handle <- ValueNone

            let create hitConfigs pipeline =
                inc &version
                let table = ShaderBindingTable.create hitConfigs pipeline
                handle <- ValueSome table
                { handle = table; version = version }

            let update hitConfigs pipeline table =
                let updated = table |> ShaderBindingTable.updateOrRecreate hitConfigs pipeline
                if updated <> table then
                    inc &version
                    handle <- ValueSome updated
                { handle = updated; version = version }

            override x.Create() =
                base.Create()
                pipeline.Acquire()

            override x.Destroy() =
                destroy()
                pipeline.Release()
                base.Destroy()

            override x.Compute(user : IResourceUser, token : AdaptiveToken, renderToken : RenderToken) =
                if x.OutOfDate then
                    let pipeline = pipeline.Update(user, token, renderToken)
                    let configs = hitConfigs.GetValue(user, token, renderToken)

                    match handle with
                    | ValueSome tbl -> tbl |> update configs pipeline.handle
                    | _ -> create configs pipeline.handle
                else
                    match handle with
                    | ValueSome tbl -> { handle = tbl; version = version }
                    | ValueNone -> failwith "[Resource] inconsistent state"


module internal ResourceCaches =
    open System.Runtime.CompilerServices

    type DummyResourceCache(device : Device) =
        interface IResourceCache with
            member x.Device = device
            member x.Remove key = ()

    type ResourceCache<'T when 'T :> IResourceLocation>(device : Device) =
        let store = ConcurrentDictionary<list<obj>, 'T>()

        member x.GetOrCreate(key : list<obj>, create : IResourceCache -> list<obj> -> 'T) =
            store.GetOrAdd(key, create x)

        member x.Clear() =
            let res = store.Values |> Seq.toArray
            for r in res do r.ReleaseAll()

        interface IResourceCache with
            member x.Device = device
            member x.Remove key = store.TryRemove key |> ignore

    type ResourceLocationCache<'T>(device : Device) =
        inherit ResourceCache<IResourceLocation<'T>>(device)

    type ConstantResourceLocationCache<'T>(device : Device) =
        inherit ResourceCache<IConstantResourceLocation<'T>>(device)

    type NativeResourceLocationCache<'T, 'V when 'V : unmanaged>(device : Device) =
        inherit ResourceCache<INativeResourceLocation<'T, 'V>>(device)

    type NativeResourceLocationCache<'T when 'T : unmanaged> =
        NativeResourceLocationCache<'T, 'T>

    type ImageSamplerMapCache() =
        let store = ConditionalWeakTable<IAdaptiveValue, amap<int, IResourceLocation<Resources.ImageSampler>>>()

        member x.GetOrAdd(textures : IAdaptiveValue, create : IAdaptiveValue -> _) =
            lock store (fun _ ->
                match store.TryGetValue textures with
                | (true, map) -> map
                | _ ->
                    let map = create textures
                    store.Add(textures, map)
                    map
            )


open Resources
open Resources.Raytracing
open ResourceCaches

type ResourceManager(device : Device) =

    let dummyCache              = DummyResourceCache(device)

    let bufferCache             = ResourceLocationCache<Buffer>(device)
    let indirectBufferCache     = ResourceLocationCache<IndirectBuffer>(device)
    let indexBufferCache        = ResourceLocationCache<Buffer>(device)
    let descriptorSetCache      = ResourceLocationCache<DescriptorSet>(device)
    let uniformBufferCache      = ResourceLocationCache<UniformBuffer>(device)
    let pushConstantsCache      = ConstantResourceLocationCache<PushConstants>(device)
    let imageCache              = ResourceLocationCache<Image>(device)
    let imageViewCache          = ResourceLocationCache<ImageView>(device)
    let samplerCache            = ResourceLocationCache<Sampler>(device)
    let samplerStateCache       = ResourceLocationCache<SamplerState>(device)
    let imageSamplerCache       = ResourceLocationCache<ImageSampler>(device)
    let imageSamplerArrayCache  = ResourceLocationCache<ImageSamplerArray>(device)
    let imageSamplerMapCache    = ImageSamplerMapCache()
    let dynamicProgramCache     = ResourceLocationCache<ShaderProgram>(device)

    let accelerationStructureCache = ResourceLocationCache<Raytracing.AccelerationStructure>(device)
    let raytracingPipelineCache    = NativeResourceLocationCache<Raytracing.RaytracingPipeline, VkPipeline>(device)
    let shaderBindingTableCache    = NativeResourceLocationCache<Raytracing.ShaderBindingTable, ShaderBindingTableHandle>(device)

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

    [<Literal>]
    let IndexBufferUsage = VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.IndexBufferBit

    [<Literal>]
    let VertexBufferUsage = VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.VertexBufferBit

    [<Literal>]
    let StorageBufferUsage = VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.StorageBufferBit

    member x.Dispose() =
        bufferCache.Clear()

        indirectBufferCache.Clear()
        indexBufferCache.Clear()
        descriptorSetCache.Clear()
        uniformBufferCache.Clear()
        pushConstantsCache.Clear()
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

    member private x.CreateBuffer(name : string, input : aval<IBuffer>, usage : VkBufferUsageFlags) =
        bufferCache.GetOrCreate([input :> obj; usage :> obj], fun cache key ->
            BufferResource(cache, key, name, device, usage, input) :> IResourceLocation<Buffer>
        )

    member private x.CreateBuffer(input : aval<#IBuffer>, usage : VkBufferUsageFlags) =
        bufferCache.GetOrCreate([input :> obj], fun cache key ->
            BufferResource(cache, key, null, device, usage, input |> AdaptiveResource.cast) :> IResourceLocation<Buffer>
        )

    member x.CreateVertexBuffer(name : Symbol, input : aval<IBuffer>) =
        let name = if device.DebugLabelsEnabled then $"{name} (Vertex Buffer)" else null
        x.CreateBuffer(name, input, VertexBufferUsage)

    member x.CreateStorageBuffer(name : Symbol, input : aval<IBuffer>) =
        let name = if device.DebugLabelsEnabled then $"{name} (Storage Buffer)" else null
        x.CreateBuffer(name, input, StorageBufferUsage)

    member x.CreateStorageBuffer(name : Symbol, input : aval<Array>) =
        bufferCache.GetOrCreate([input :> obj], fun cache key ->
            let buffer = input |> AdaptiveResource.map (fun arr -> ArrayBuffer arr :> IBuffer)
            let name = if device.DebugLabelsEnabled then $"{name} (Storage Buffer)" else null
            BufferResource(cache, key, name, device, StorageBufferUsage, buffer) :> IResourceLocation<Buffer>
        )

    member x.CreateIndexBuffer(input : aval<IBuffer>) =
        let name = if device.Instance.DebugLabelsEnabled then "Index Buffer" else null
        x.CreateBuffer(name, input, IndexBufferUsage)

    member x.CreateIndirectBuffer(indexed : bool, input : aval<Aardvark.Rendering.IndirectBuffer>) =
        indirectBufferCache.GetOrCreate(
            [indexed :> obj; input :> obj],
            let name = if device.Instance.DebugLabelsEnabled then "Indirect Buffer" else null
            fun cache key -> IndirectBufferResource(cache, key, name, device, indexed, input)
        )

    member private x.CreateImage(name : string, properties : ImageProperties, input : aval<ITexture>) =
        imageCache.GetOrCreate(
            [properties :> obj; input :> obj],
            fun cache key -> ImageResource(cache, key, name, device, properties, input)
        )

    member private x.CreateImage(name : string, properties : ImageProperties, input : aval<ITextureLevel>) =
        let input = input |> AdaptiveResource.mapNonAdaptive (fun l -> l.Texture :> ITexture)
        imageCache.GetOrCreate(
            [properties :> obj; input :> obj],
            fun cache key -> ImageResource(cache, key, name, device, properties, input)
        )

    member x.CreateStorageImage(name : Symbol, properties : ImageProperties, input : aval<ITexture>) =
        let name = if device.DebugLabelsEnabled then $"{name} (Storage Image)" else null
        x.CreateImage(name, properties, input)

    member x.CreateStorageImage(name : Symbol, properties : ImageProperties, input : aval<ITextureLevel>) =
        let name = if device.DebugLabelsEnabled then $"{name} (Storage Image)" else null
        x.CreateImage(name, properties, input)

    member x.CreateImageView(samplerType : FShade.GLSL.GLSLSamplerType, input : IResourceLocation<Image>, levels : aval<Range1i>, slices : aval<Range1i>) =
        imageViewCache.GetOrCreate(
            [samplerType :> obj; input :> obj; levels :> obj; slices :> obj],
            fun cache key -> ImageViewResource(cache, key, device, samplerType, input, levels, slices)
        )

    member x.CreateImageView(samplerType : FShade.GLSL.GLSLSamplerType, input : IResourceLocation<Image>) =
        imageViewCache.GetOrCreate([samplerType :> obj; input :> obj], fun cache key -> ImageViewResource(cache, key, device, samplerType, input))

    member x.CreateImageView(imageType : FShade.GLSL.GLSLImageType, input : IResourceLocation<Image>, levels : aval<Range1i>, slices : aval<Range1i>) =
        imageViewCache.GetOrCreate(
            [imageType :> obj; input :> obj; levels :> obj; slices :> obj],
            fun cache key -> StorageImageViewResource(cache, key, device, imageType, input, levels, slices)
        )

    member x.CreateImageView(imageType : FShade.GLSL.GLSLImageType, input : IResourceLocation<Image>) =
        imageViewCache.GetOrCreate([imageType :> obj; input :> obj], fun cache key -> StorageImageViewResource(cache, key, device, imageType, input))

    member x.CreateDynamicSamplerState(state : SamplerState, modifier : aval<SamplerState -> SamplerState>) =
        samplerStateCache.GetOrCreate(
            [state :> obj; modifier :> obj],
            fun cache key -> DynamicSamplerStateResource(cache, key, state, modifier)
        )

    member private x.CreateImageSampler(name : string, samplerType : FShade.GLSL.GLSLSamplerType,
                                        texture : aval<ITexture>, samplerDesc : aval<SamplerState>) =
        let image = x.CreateImage(name, samplerType.Properties, texture)
        let view = x.CreateImageView(samplerType, image)

        imageSamplerCache.GetOrCreate(
            [view :> obj; samplerDesc :> obj],
            fun cache key -> ImageSamplerResource(cache, key, device, view, samplerDesc)
        )

    member private x.CreateImageSampler(name : string, samplerType : FShade.GLSL.GLSLSamplerType,
                                        level : aval<ITextureLevel>, samplerDesc : aval<SamplerState>) =
        let levels = level |> AVal.mapNonAdaptive _.Levels
        let slices = level |> AVal.mapNonAdaptive _.Slices
        let image = x.CreateImage(name, samplerType.Properties, level)
        let view = x.CreateImageView(samplerType, image, levels, slices)

        imageSamplerCache.GetOrCreate(
            [view :> obj; samplerDesc :> obj],
            fun cache key -> ImageSamplerResource(cache, key, device, view, samplerDesc)
        )

    member x.CreateImageSampler(textureName : Symbol, samplerType : FShade.GLSL.GLSLSamplerType,
                                texture : aval<ITexture>, samplerDesc : aval<SamplerState>) =
        let name = if device.DebugLabelsEnabled then $"{textureName} (Sampled Image)" else null
        x.CreateImageSampler(name, samplerType, texture, samplerDesc)

    member x.CreateImageSampler(textureName : Symbol, samplerType : FShade.GLSL.GLSLSamplerType,
                                level : aval<ITextureLevel>, samplerDesc : aval<SamplerState>) =
        let name = if device.DebugLabelsEnabled then $"{textureName} (Sampled Image)" else null
        x.CreateImageSampler(name, samplerType, level, samplerDesc)

    member x.CreateNullImageSampler(samplerType : FShade.GLSL.GLSLSamplerType) =
        x.CreateImageSampler(null, samplerType, nullTextureConst, AVal.constant SamplerState.Default)

    member x.CreateImageSamplerArray(textureName : Symbol, count : int, samplerType : FShade.GLSL.GLSLSamplerType,
                                     textures : aval<array<int * aval<ITexture>>>, samplerDesc : aval<SamplerState>) =

        let empty = x.CreateNullImageSampler(samplerType)

        let map =
            imageSamplerMapCache.GetOrAdd(textures, fun _ ->
                textures |> AMap.ofAVal |> AMap.map (fun i texture ->
                    let name = if device.DebugLabelsEnabled then $"{textureName}[{i}] (Sampled Image)" else null
                    x.CreateImageSampler(name, samplerType, texture, samplerDesc)
                )
            )

        x.CreateImageSamplerArray(count, empty, map)

    member x.CreateImageSamplerArray(textureName : Symbol, count : int, samplerType : FShade.GLSL.GLSLSamplerType,
                                     textures : aval<ITexture[]>, samplerDesc : aval<SamplerState>) =

        let empty = x.CreateNullImageSampler(samplerType)

        let map =
            imageSamplerMapCache.GetOrAdd(textures, fun _ ->
                textures |> AVal.map (Array.choosei (fun i texture ->
                    if i < count then
                        let name = if device.DebugLabelsEnabled then $"{textureName}[{i}] (Sampled Image)" else null
                        Some (i, x.CreateImageSampler(name, samplerType, AVal.constant texture, samplerDesc))
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
            [count :> obj, empty :> obj; input :> obj], fun cache key -> ImageSamplerArrayResource(cache, key, count, empty, input)
        )

    member x.CreateShaderProgram(pass : RenderPass, program : ShaderProgram) =
        program.AddReference()

        let resource =
            { new AbstractResourceLocation<ShaderProgram>(dummyCache, []) with
                override x.Create () = ()
                override x.Destroy () = program.Dispose()
                override x.GetHandle(u, t, rt) = { handle = program; version = 0 }
            } :> IResourceLocation<_>
        resource.Acquire()

        program.PipelineLayout, resource

    member x.CreateShaderProgram(signature : RenderPass, data : FShade.Effect, top : IndexedGeometryMode) =
        let program = device.CreateShaderProgram(signature, data, top)

        let resource =
            { new AbstractResourceLocation<ShaderProgram>(dummyCache, []) with
                override x.Create () = ()
                override x.Destroy () = program.Dispose()
                override x.GetHandle(u, t, rt) = { handle = program; version = 0 }
            } :> IResourceLocation<_>
        resource.Acquire()

        program.PipelineLayout, resource

    member private x.CreateDynamicShaderProgram(pass : RenderPass, top : IndexedGeometryMode,
                                                compile : Func<IFramebufferSignature, IndexedGeometryMode, DynamicSurface>) =
        dynamicProgramCache.GetOrCreate(
            [compile :> obj; top :> obj; pass.Layout :> obj],
            fun cache key ->
                let _, module_ = compile.Invoke(pass, top)
                use initialProgram = device.CreateShaderProgram(AVal.force module_)

                let program = DynamicShaderProgramResource(cache, key, device, initialProgram.PipelineLayout, module_)
                program.Acquire()
                program
        )
        |> unbox<DynamicShaderProgramResource>

    member x.CreateShaderProgram(pass : RenderPass, data : Aardvark.Rendering.Surface, top : IndexedGeometryMode) =
        match data with
        | Surface.Effect effect ->
            x.CreateShaderProgram(pass, effect, top)

        | Surface.Dynamic compile ->
            let program = x.CreateDynamicShaderProgram(pass, top, compile)
            program.Layout, program

        | Surface.Backend (:? ShaderProgram as program) ->
            x.CreateShaderProgram(pass, program)

        | Surface.Backend unknown ->
            failf "invalid backend surface: %A" unknown

        | Surface.None ->
            failf "encountered empty surface"

    member inline private x.GetUniformBufferValues(layout : FShade.GLSL.GLSLUniformBuffer, uniforms : IUniformProvider) =
        layout.ubFields
        |> List.map (fun (f) ->
            let name = f.ufName

            let struct (field, value) =
                match Uniforms.tryGetDerivedUniform f.ufName uniforms with
                | ValueSome r -> f, r
                | ValueNone ->
                    match uniforms.TryGetUniform(Ag.Scope.Root, Symbol.Create name) with
                    | ValueSome v -> f, v
                    | ValueNone ->
                        failf "could not find uniform '%s'" name

            if Object.ReferenceEquals(value, null) then
                failf "uniform '%s' is null" name

            struct (field, value)
        )

    member inline private x.GetUniformWriters(values : struct (FShade.GLSL.GLSLUniformBufferField * IAdaptiveValue) list) =
        values |> List.map (fun struct (target, value) ->
            let writer =
                try
                    value.ContentType |> UniformWriters.getWriter target.ufOffset target.ufType
                with
                | :? Aardvark.Base.PrimitiveValueConverter.InvalidConversionException as exn ->
                    failf "cannot convert uniform '%s' from %A to %A" target.ufName exn.Source exn.Target

            struct (value, writer)
        )
        |> List.toArray

    member x.CreateUniformBuffer(layout : FShade.GLSL.GLSLUniformBuffer, uniforms : IUniformProvider) =
        let values = x.GetUniformBufferValues(layout, uniforms)

        let key = (layout :> obj) :: (values |> List.map (sndv >> box))
        uniformBufferCache.GetOrCreate(key, fun cache key ->
            let writers = x.GetUniformWriters values
            UniformBufferResource(cache, key, device, layout, writers)
        )

    member x.CreatePushConstants(layout : PushConstantsLayout, uniforms : IUniformProvider) =
        let values = x.GetUniformBufferValues(layout.Buffer, uniforms)

        let key = (layout :> obj) :: (values |> List.map (sndv >> box))
        pushConstantsCache.GetOrCreate(key, fun cache key ->
            let writers = x.GetUniformWriters values
            PushConstantsResource(cache, key, layout, writers)
        )

    member x.CreateDescriptorSet(layout : DescriptorSetLayout, bindings : IAdaptiveDescriptor[]) =
        descriptorSetCache.GetOrCreate([layout :> obj; bindings :> obj], fun cache key -> DescriptorSetResource(cache, key, layout, bindings))

    member x.CreateVertexInputState(program : PipelineInfo, mode : aval<Map<Symbol, VertexInputDescription>>) =
        vertexInputCache.GetOrCreate([program :> obj; mode :> obj], fun cache key -> VertexInputStateResource(cache, key, program, mode))

    member inline x.CreateVertexInputState(program : PipelineInfo, mode : Map<Symbol, VertexInputDescription>) =
        x.CreateVertexInputState(program, AVal.constant mode)

    member x.CreateInputAssemblyState(mode : IndexedGeometryMode, program : IResourceLocation<ShaderProgram>) =
        inputAssemblyCache.GetOrCreate([mode :> obj; program :> obj], fun cache key -> InputAssemblyStateResource(cache, key, mode, program))

    member x.CreateDepthStencilState(depthTest : aval<DepthTest>, depthWrite : aval<bool>,
                                     stencilModeF : aval<StencilMode>, stencilMaskF : aval<StencilMask>,
                                     stencilModeB : aval<StencilMode>, stencilMaskB : aval<StencilMask>) =
        depthStencilCache.GetOrCreate(
            [depthTest :> obj; depthWrite :> obj;
             stencilModeF :> obj; stencilMaskF :> obj;
             stencilModeB :> obj; stencilMaskB :> obj],
            fun cache key -> DepthStencilStateResource(cache, key,
                                                       depthTest, depthWrite,
                                                       stencilModeF, stencilMaskF,
                                                       stencilModeB, stencilMaskB)
        )

    member x.CreateRasterizerState(depthClamp : aval<bool>, depthBias : aval<DepthBias>,
                                   cull : aval<CullMode>, front : aval<WindingOrder>, fill : aval<FillMode>,
                                   conservativeRaster : aval<bool>) =
        rasterizerStateCache.GetOrCreate(
            [depthClamp :> obj; depthBias :> obj; cull :> obj; front :> obj, fill :> obj; conservativeRaster :> obj],
            fun cache key -> RasterizerStateResource(cache, key, depthClamp, depthBias, cull, front, fill, conservativeRaster)
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
                    |> Option.bind (fun (name, _) -> values |> Map.tryFind name)
                    |> Option.defaultValue fallback
                )
            }

        colorBlendStateCache.GetOrCreate(
            [pass.ColorAttachments :> obj; globalMask :> obj; attachmentMask :> obj; globalBlend :> obj; attachmentBlend :> obj; blendConstant :> obj],
            fun cache key ->
                let writeMasks = getAttachmentStates globalMask attachmentMask
                let blendModes = getAttachmentStates globalBlend attachmentBlend

                let blendSupported =
                    Array.init slots (fun i ->
                        pass.ColorAttachments
                        |> Map.tryFind i
                        |> Option.map (fun (_, fmt) ->
                            let features = pass.Device.PhysicalDevice.GetFormatFeatures(VkImageTiling.Optimal, fmt)
                            features.HasFlag VkFormatFeatureFlags.ColorAttachmentBlendBit
                        )
                        |> Option.defaultValue false
                    )

                ColorBlendStateResource(cache, key, writeMasks, blendModes, blendConstant, blendSupported)
        )

    member x.CreateMultisampleState(pass : RenderPass, multisample : aval<bool>) =
        multisampleCache.GetOrCreate(
            [pass.Samples :> obj; multisample :> obj],
            fun cache key -> MultisampleStateResource(cache, key, pass.Samples, multisample)
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
                PipelineResource(
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

    member x.CreateAccelerationStructure(name : Symbol,
                                         instances : aset<Raytracing.ITraceInstance>,
                                         sbt : IResourceLocation<Raytracing.ShaderBindingTable>,
                                         usage : Raytracing.AccelerationStructureUsage) =

        accelerationStructureCache.GetOrCreate(
            [ instances :> obj; sbt :> obj; usage :> obj ],
            fun cache key ->
                let instanceBuffer = InstanceBuffer.create x.Device sbt instances
                let buffer = x.CreateBuffer(instanceBuffer, VkBufferUsageFlags.None)
                let name =
                    if device.DebugLabelsEnabled then
                        let name = Sym.toString name
                        instanceBuffer.Name <- $"Instance Buffer ({name})"
                        name
                    else
                        null

                AccelerationStructureResource(cache, key, name, device, buffer, instanceBuffer.Count, usage)
        )

    member x.CreateRaytracingPipeline(program           : Raytracing.RaytracingProgram,
                                      maxRecursionDepth : aval<int>) =

        raytracingPipelineCache.GetOrCreate(
            [ program :> obj; maxRecursionDepth :> obj ],
            fun cache key ->
                RaytracingPipelineResource(
                    cache, key,
                    program, maxRecursionDepth
                )
        )

    member x.CreateShaderBindingTable(pipeline : IResourceLocation<Raytracing.RaytracingPipeline>,
                                      hitConfigs : aval<Set<Symbol[]>>) =

        shaderBindingTableCache.GetOrCreate(
            [ pipeline :> obj; hitConfigs :> obj ],
            fun cache key ->
                ShaderBindingTableResource(
                    cache, key, pipeline, hitConfigs
                )
        )


    member x.CreateDrawCall(indexed : bool, calls : aval<DrawCallInfo[]>) =
        drawCallCache.GetOrCreate([indexed :> obj; calls :> obj], fun cache key -> DirectDrawCallResource(cache, key, indexed, calls))

    member x.CreateDrawCall(indexed : bool, calls : IResourceLocation<IndirectBuffer>) =
        drawCallCache.GetOrCreate([indexed :> obj; calls :> obj], fun cache key -> IndirectDrawCallResource(cache, key, indexed, calls))

    member x.CreateVertexBufferBinding(buffers : IResourceLocation<Buffer>[]) =
        bufferBindingCache.GetOrCreate([buffers :> obj], fun cache key -> BufferBindingResource(cache, key, buffers))

    member x.CreateDescriptorSetBinding(bindPoint : VkPipelineBindPoint, layout : PipelineLayout, bindings : IResourceLocation<DescriptorSet>[]) =
        descriptorBindingCache.GetOrCreate(
            [bindPoint :> obj; layout :> obj; bindings :> obj],
            fun cache key -> DescriptorSetBindingResource(cache, key, bindPoint, layout, bindings)
        )

    member x.CreateIndexBufferBinding(binding : IResourceLocation<Buffer>, t : VkIndexType) =
        indexBindingCache.GetOrCreate([binding :> obj; t :> obj], fun cache key -> IndexBufferBindingResource(cache, key, t, binding))

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