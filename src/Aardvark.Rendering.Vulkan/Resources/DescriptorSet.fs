namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
// #nowarn "51"


type Descriptor =
    | UniformBuffer of int * UniformBuffer
    | StorageBuffer of int * Buffer * offset : int64 * size : int64
    | CombinedImageSampler of int * array<Option<VkImageLayout * ImageView * Sampler>>
    | StorageImage of int * ImageView

type DescriptorSet =
    class
        inherit Resource<VkDescriptorSet>
        val mutable public Pool : DescriptorPool
        val mutable public Layout : DescriptorSetLayout

        new(device : Device, pool : DescriptorPool, layout : DescriptorSetLayout, handle : VkDescriptorSet) = { inherit Resource<_>(device, handle); Pool = pool; Layout = layout }
    end


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DescriptorSet =

    let tryAlloc (layout : DescriptorSetLayout) (pool : DescriptorPool) =
        lock pool (fun () ->
            let canWork = Interlocked.Change(&pool.SetCount, fun c -> if c <= 0 then 0, false else c-1, true)
            
            if canWork then
                native {
                    let! pLayoutHandle = layout.Handle
                    let! pInfo =
                        VkDescriptorSetAllocateInfo(
                            VkStructureType.DescriptorSetAllocateInfo, 0n, 
                            pool.Handle, 
                            1u, 
                            pLayoutHandle
                        )

                    let! pHandle = VkDescriptorSet.Null
                    let res = VkRaw.vkAllocateDescriptorSets(pool.Device.Handle, pInfo, pHandle)

                    if res = VkResult.VkErrorFragmentedPool then
                        return None
                    else
                        res |> check "could not allocate DescriptorSet"
                        return Some (DescriptorSet(pool.Device, pool, layout, !!pHandle))
                }
            else
                None
        )

    let alloc (layout : DescriptorSetLayout) (pool : DescriptorPool) =
        match tryAlloc layout pool with
            | Some d -> d
            | None -> failf "cannot allocate DescriptorSet (out of slots)"

    let free (desc : DescriptorSet) (pool : DescriptorPool) =
        lock pool (fun () ->
            if desc.Handle.IsValid then
                native {
                    let! pHandle = desc.Handle
                    VkRaw.vkFreeDescriptorSets(pool.Device.Handle, pool.Handle, 1u, pHandle)
                        |> check "could not free DescriptorSet"
                }

                desc.Handle <- VkDescriptorSet.Null
                Interlocked.Increment(&pool.SetCount) |> ignore
        )

    let update (descriptors : array<Descriptor>) (set : DescriptorSet) (pool : DescriptorPool) =
        let device = pool.Device
        let pool = ()

        let layout = set.Layout
        let cnt = descriptors |> Array.sumBy (function CombinedImageSampler(_, arr) -> arr.Length | _ -> 1)
        let mutable bufferInfos = NativePtr.stackalloc cnt
        let mutable imageInfos = NativePtr.stackalloc cnt

        let writes =
            descriptors
                |> Array.collect (fun desc ->
                    match desc with
                        | StorageBuffer (binding, b, offset, size) ->
                            let info = 
                                VkDescriptorBufferInfo(
                                    b.Handle, 
                                    uint64 offset, 
                                    uint64 size
                                )

                            NativePtr.write bufferInfos info
                            let ptr = bufferInfos
                            bufferInfos <- NativePtr.step 1 bufferInfos

                            [|
                                VkWriteDescriptorSet(
                                    VkStructureType.WriteDescriptorSet, 0n,
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
                                    uint64 ub.Storage.Size
                                )

                            NativePtr.write bufferInfos info
                            let ptr = bufferInfos
                            bufferInfos <- NativePtr.step 1 bufferInfos

                            [|
                                VkWriteDescriptorSet(
                                    VkStructureType.WriteDescriptorSet, 0n,
                                    set.Handle,
                                    uint32 binding,
                                    0u, 1u, VkDescriptorType.UniformBuffer,
                                    NativePtr.zero,
                                    ptr,
                                    NativePtr.zero
                                )
                            |]

                        | CombinedImageSampler(binding, arr) ->
                            arr |> Array.choosei (fun i vs ->
                                match vs with
                                    | Some(expectedLayout, view, sam) -> 
                                        let info =
                                            VkDescriptorImageInfo(
                                                sam.Handle,
                                                view.Handle,
                                                expectedLayout
                                            )

                                        NativePtr.write imageInfos info
                                        let ptr = imageInfos
                                        imageInfos <- NativePtr.step 1 imageInfos

                                        let write = 
                                            VkWriteDescriptorSet(
                                                VkStructureType.WriteDescriptorSet, 0n,
                                                set.Handle,
                                                uint32 binding,
                                                uint32 i, 1u, VkDescriptorType.CombinedImageSampler,
                                                ptr,
                                                NativePtr.zero,
                                                NativePtr.zero
                                            )
                                        Some write
                                    | _ ->
                                        None
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
                                    VkStructureType.WriteDescriptorSet, 0n,
                                    set.Handle,
                                    uint32 binding,
                                    0u, 1u, VkDescriptorType.StorageImage,
                                    ptr,
                                    NativePtr.zero,
                                    NativePtr.zero
                                )

                            [| write |]
                   )
        native {
            let! pWrites = writes
            VkRaw.vkUpdateDescriptorSets(device.Handle, uint32 writes.Length, pWrites, 0u, NativePtr.zero) 
        }
type private DescriptorPoolBag(device : Device, perPool : int, resourcesPerPool : int) =
    inherit RefCountedResource()

    let pools = System.Collections.Generic.HashSet<DescriptorPool>()
    let partialSet = System.Collections.Generic.HashSet<DescriptorPool>()
    let mutable partial = []

    let createNew() =
        let pool = device.CreateDescriptorPool(perPool, resourcesPerPool)
        pools.Add pool |> ignore
        partialSet.Add pool |> ignore
        partial <- pool :: partial
        Log.line "[Vulkan] using %d descriptor pools" pools.Count

    member x.CreateDescriptorSet(layout : DescriptorSetLayout) =
        lock pools (fun () ->
            match partial with
                | [] ->
                    createNew()
                    x.CreateDescriptorSet(layout)
                | h :: t ->
                    match DescriptorSet.tryAlloc layout h with
                        | Some set -> 
                            set
                        | None ->
                            partialSet.Remove h |> ignore
                            partial <- t
                            x.CreateDescriptorSet(layout)
        )
                
    member x.Delete (set : DescriptorSet) =
        lock pools (fun () ->
            let pool = set.Pool
            if pools.Contains pool then
                lock pool (fun () ->
                    DescriptorSet.free set pool
                    if pool.SetCount = perPool then
                        if partialSet.Remove pool then partial <- List.filter (fun p -> p <> pool) partial
                        DescriptorPool.delete pool device
                        pools.Remove pool |> ignore
                        Log.line "[Vulkan] using %d descriptor pools" pools.Count

                    else
                        if partialSet.Add pool then
                            partial <- pool :: partial
                )
            else
                failf "cannot free non-pooled DescriptorSet using pool"
        )
       
    member x.Update(set : DescriptorSet, values : array<Descriptor>) =
        set.Pool |> DescriptorSet.update values set
        
    override x.Destroy() =
        pools |> Seq.iter (fun p -> device.Delete p)
        pools.Clear()
        partial <- []
            
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
    static member inline Free(this : DescriptorPool, set : DescriptorSet) =
        this |> DescriptorSet.free set

        
    [<Extension>]
    static member CreateDescriptorSet(this : Device, layout : DescriptorSetLayout) =
        let bag = this.GetCached(DescriptorPoolBag, 0, fun _ -> new DescriptorPoolBag(this, 1024, 1024))
        bag.CreateDescriptorSet layout
        
    [<Extension>]
    static member Delete(this : Device, set : DescriptorSet) =
        let bag = this.GetCached(DescriptorPoolBag, 0, fun _ -> new DescriptorPoolBag(this, 1024, 1024))
        bag.Delete set

    [<Extension>]
    static member Update(set : DescriptorSet, values : array<Descriptor>) =
        set.Pool |> DescriptorSet.update values set
        