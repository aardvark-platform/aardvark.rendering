namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base

open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.KHRAccelerationStructure
open Aardvark.Rendering.Vulkan.Raytracing
open Microsoft.FSharp.NativeInterop

#nowarn "9"

type Descriptor =
    | UniformBuffer         of slot: int * buffer: UniformBuffer
    | StorageBuffer         of slot: int * buffer: Buffer * offset: int64 * size: int64
    | CombinedImageSampler  of slot: int * images: array<int * VkImageLayout * ImageView * Sampler>
    | StorageImage          of slot: int * view: ImageView
    | AccelerationStructure of slot: int * accel: AccelerationStructure

type internal DescriptorPoolBag(device : Device, perPool : int, resourcesPerPool : int) =
    inherit CachedResource(device)

    let pools = System.Collections.Generic.HashSet<DescriptorPool>()
    let partialSet = System.Collections.Generic.HashSet<DescriptorPool>()
    let mutable partial = []

    let createNew() =
        let pool = device.CreateDescriptorPool(perPool, resourcesPerPool)
        pools.Add pool |> ignore
        partialSet.Add pool |> ignore
        partial <- pool :: partial
        Log.line "[Vulkan] using %d descriptor pools" pools.Count

    member x.CreateSet(layout : DescriptorSetLayout, tryAllocSet : DescriptorSetLayout -> DescriptorPool -> DescriptorSet option) =
        lock pools (fun () ->
            match partial with
            | [] ->
                createNew()
                x.CreateSet(layout, tryAllocSet)
            | h :: t ->
                match h |> tryAllocSet layout with
                | Some set ->
                    set.SetPoolBag(x)
                    set
                | None ->
                    partialSet.Remove h |> ignore
                    partial <- t
                    x.CreateSet(layout, tryAllocSet)
        )

    member x.RemoveSet (set : DescriptorSet) =
        lock pools (fun () ->
            let pool = set.Pool
            if pools.Contains pool then
                if pool.Capacity = perPool then
                    if partialSet.Remove pool then partial <- List.filter (fun p -> p <> pool) partial
                    pool.Dispose()
                    pools.Remove pool |> ignore
                    Log.line "[Vulkan] using %d descriptor pools" pools.Count
                else
                    if partialSet.Add pool then
                        partial <- pool :: partial
            else
                failf "cannot free non-pooled DescriptorSet using pool"
         )

    override x.Destroy() =
        for p in pools do p.Dispose()
        pools.Clear()
        partial <- []

and DescriptorSet =
    class
        inherit Resource<VkDescriptorSet>
        val public Pool : DescriptorPool
        val public Layout : DescriptorSetLayout

        val mutable private poolBag : Option<DescriptorPoolBag>
        val mutable private boundResources : Resource[]

        member internal x.SetPoolBag(bag : DescriptorPoolBag) =
            x.poolBag <- Some bag

        member x.BindResources(resources : Resource[]) =
            x.boundResources |> Array.iter Disposable.dispose
            x.boundResources <- resources

        override x.Destroy() =
            lock x.Pool (fun () ->
                if x.Handle.IsValid then
                    native {
                        let! pHandle = x.Handle
                        VkRaw.vkFreeDescriptorSets(x.Device.Handle, x.Pool.Handle, 1u, pHandle)
                            |> check "could not free DescriptorSet"
                    }

                    x.Handle <- VkDescriptorSet.Null
                    x.Pool.FreeSet()

                    x.poolBag |> Option.iter (fun b -> b.RemoveSet(x))
            )

            x.boundResources |> Array.iter Disposable.dispose

        new(device : Device, pool : DescriptorPool, layout : DescriptorSetLayout, handle : VkDescriptorSet) =
            { inherit Resource<_>(device, handle); Pool = pool; Layout = layout; poolBag = None; boundResources = [||] }
    end


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DescriptorSet =

    let tryAlloc (layout : DescriptorSetLayout) (pool : DescriptorPool) =
        lock pool (fun () ->
            if pool.TryAllocateSet() then
                native {
                    let! pLayoutHandle = layout.Handle
                    let! pInfo =
                        VkDescriptorSetAllocateInfo(
                            pool.Handle,
                            1u,
                            pLayoutHandle
                        )

                    let! pHandle = VkDescriptorSet.Null
                    let res = VkRaw.vkAllocateDescriptorSets(pool.Device.Handle, pInfo, pHandle)

                    if res = VkResult.ErrorFragmentedPool then
                        return None
                    else
                        res |> check "could not allocate DescriptorSet"
                        return Some (new DescriptorSet(pool.Device, pool, layout, !!pHandle))
                }
            else
                None
        )

    let alloc (layout : DescriptorSetLayout) (pool : DescriptorPool) =
        match tryAlloc layout pool with
            | Some d -> d
            | None -> failf "cannot allocate DescriptorSet (out of slots)"

    let update (descriptors : array<Descriptor>) (set : DescriptorSet) (pool : DescriptorPool) =
        let device = pool.Device

        let mutable imageInfos =
            let cnt = descriptors |> Array.sumBy (function CombinedImageSampler(_, arr) -> arr.Length | StorageImage _ -> 1 | _ -> 0)
            NativePtr.stackalloc cnt

        let mutable bufferInfos =
            let cnt = descriptors |> Array.sumBy (function StorageBuffer _ | UniformBuffer _ -> 1 | _ -> 0)
            NativePtr.stackalloc cnt

        let accelCount =
            descriptors |> Array.sumBy (function AccelerationStructure _ -> 1 | _ -> 0)

        let mutable accelWrites  = NativePtr.stackalloc accelCount
        let mutable accelHandles = NativePtr.stackalloc accelCount

        let resources : Resource[] =
            descriptors |> Array.collect (function
                | StorageBuffer (_, b, _, _) ->
                    [| b |]
                | UniformBuffer (_, b) ->
                    [| b |]
                | CombinedImageSampler (_, arr) ->
                    arr |> Array.collect (fun (_, _, v, s) -> [| v; s |] )
                | StorageImage (_, v) ->
                    [| v |]
                | AccelerationStructure (_, a) ->
                    [| a |]
            )

        let writes =
            descriptors
            |> Array.collect (fun desc ->
                match desc with
                | StorageBuffer (binding, b, offset, size) ->
                    let info =
                        VkDescriptorBufferInfo(
                            b.Handle,
                            uint64 offset,
                            if size > 0L then uint64 size else VkWholeSize
                        )

                    NativePtr.write bufferInfos info
                    let ptr = bufferInfos
                    bufferInfos <- NativePtr.step 1 bufferInfos

                    [|
                        VkWriteDescriptorSet(
                            set.Handle,
                            uint32 binding,
                            0u, 1u, VkDescriptorType.StorageBuffer,
                            NativePtr.zero,
                            ptr,
                            NativePtr.zero
                        )
                    |]

                | UniformBuffer (binding, ub) ->
                    let info =
                        VkDescriptorBufferInfo(
                            ub.Handle,
                            0UL,
                            if ub.Storage.Size > 0 then uint64 ub.Storage.Size else VkWholeSize
                        )

                    NativePtr.write bufferInfos info
                    let ptr = bufferInfos
                    bufferInfos <- NativePtr.step 1 bufferInfos

                    [|
                        VkWriteDescriptorSet(
                            set.Handle,
                            uint32 binding,
                            0u, 1u, VkDescriptorType.UniformBuffer,
                            NativePtr.zero,
                            ptr,
                            NativePtr.zero
                        )
                    |]

                | CombinedImageSampler(binding, arr) ->
                    arr |> Array.map (fun (i, expectedLayout, view, sam) ->
                        let info =
                            VkDescriptorImageInfo(
                                sam.Handle,
                                view.Handle,
                                expectedLayout
                            )

                        NativePtr.write imageInfos info
                        let ptr = imageInfos
                        imageInfos <- NativePtr.step 1 imageInfos

                        VkWriteDescriptorSet(
                            set.Handle,
                            uint32 binding,
                            uint32 i, 1u, VkDescriptorType.CombinedImageSampler,
                            ptr,
                            NativePtr.zero,
                            NativePtr.zero
                        )
                    )

                | StorageImage(binding, view) ->
                    let info =
                        VkDescriptorImageInfo(
                            VkSampler.Null,
                            view.Handle,
                            VkImageLayout.General
                        )

                    NativePtr.write imageInfos info
                    let ptr = imageInfos
                    imageInfos <- NativePtr.step 1 imageInfos

                    let write =
                        VkWriteDescriptorSet(
                            set.Handle,
                            uint32 binding,
                            0u, 1u, VkDescriptorType.StorageImage,
                            ptr,
                            NativePtr.zero,
                            NativePtr.zero
                        )

                    [| write |]

                | AccelerationStructure(binding, accel) ->
                    NativePtr.write accelHandles accel.Handle
                    let pHandle = accelHandles
                    accelHandles <- NativePtr.step 1 accelHandles

                    let writeAccel =
                        VkWriteDescriptorSetAccelerationStructureKHR(1u, pHandle)

                    NativePtr.write accelWrites writeAccel
                    let ptr = accelWrites
                    accelWrites <- NativePtr.step 1 accelWrites

                    let write =
                        VkWriteDescriptorSet(
                            NativePtr.toNativeInt ptr,
                            set.Handle,
                            uint32 binding,
                            0u, 1u, VkDescriptorType.AccelerationStructureKhr,
                            NativePtr.zero,
                            NativePtr.zero,
                            NativePtr.zero
                        )

                    [| write |]
                )

        resources |> Array.iter (fun r -> r.AddReference())

        native {
            let! pWrites = writes
            VkRaw.vkUpdateDescriptorSets(device.Handle, uint32 writes.Length, pWrites, 0u, NativePtr.zero)
        }

        set.BindResources(resources)

[<AbstractClass; Sealed; Extension>]
type ContextDescriptorSetExtensions private() =
    static let DescriptorPoolBag = Symbol.Create "DescriptorPoolBag"

    [<Extension>]
    static member inline Alloc(this : DescriptorPool, layout : DescriptorSetLayout) =
        this |> DescriptorSet.alloc layout

    [<Extension>]
    static member inline Update(this : DescriptorPool, set : DescriptorSet, values : array<Descriptor>) =
        this |> DescriptorSet.update values set


    [<Extension>]
    static member CreateDescriptorSet(this : Device, layout : DescriptorSetLayout) =
        use bag = this.GetCached(DescriptorPoolBag, 0, fun _ -> new DescriptorPoolBag(this, 1024, 1024))
        bag.CreateSet(layout, DescriptorSet.tryAlloc)

    [<Extension>]
    static member Update(set : DescriptorSet, values : array<Descriptor>) =
        set.Pool |> DescriptorSet.update values set
