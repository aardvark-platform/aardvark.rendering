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
#nowarn "51"


type Descriptor =
    | UniformBuffer of int * UniformBuffer
    | CombinedImageSampler of int * array<Option<ImageView * Sampler>>

type AdaptiveDescriptor =
    | AdaptiveUniformBuffer of int * UniformBuffer
    | AdaptiveCombinedImageSampler of int * array<Option<IResource<ImageView> * IResource<Sampler>>>

type DescriptorSet =
    class
        inherit Resource<VkDescriptorSet>
        val mutable public Pool : DescriptorPool
        val mutable public Layout : DescriptorSetLayout

        new(device : Device, pool : DescriptorPool, layout : DescriptorSetLayout, handle : VkDescriptorSet) = { inherit Resource<_>(device, handle); Pool = pool; Layout = layout }
    end


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DescriptorSet =
    let alloc (layout : DescriptorSetLayout) (pool : DescriptorPool) =
        let worked = Interlocked.Change(&pool.SetCount, fun c -> if c <= 0 then 0, false else c-1, true)
        if worked then
            let mutable info =
                VkDescriptorSetAllocateInfo(
                    VkStructureType.DescriptorSetAllocateInfo, 0n, 
                    pool.Handle, 
                    1u, 
                    &&layout.Handle
                )

            let mutable handle = VkDescriptorSet.Null
            VkRaw.vkAllocateDescriptorSets(pool.Device.Handle, &&info, &&handle)
                |> check "could not allocate DescriptorSet"

            DescriptorSet(pool.Device, pool, layout, handle)
        else
            failf "cannot allocate DescriptorSet (out of slots)"

    let free (desc : DescriptorSet) (pool : DescriptorPool) =
        if desc.Handle.IsValid then
            VkRaw.vkFreeDescriptorSets(pool.Device.Handle, pool.Handle, 1u, &&desc.Handle)
                |> check "could not free DescriptorSet"

            desc.Handle <- VkDescriptorSet.Null

    let update (descriptors : array<Descriptor>) (set : DescriptorSet) (pool : DescriptorPool) =
        let device = pool.Device
        let layout = set.Layout
        let cnt = descriptors |> Array.sumBy (function CombinedImageSampler(_, arr) -> arr.Length | _ -> 1)
        let mutable bufferInfos = NativePtr.stackalloc cnt
        let mutable imageInfos = NativePtr.stackalloc cnt

        let writes =
            descriptors
                |> Array.collect (fun desc ->
                    match desc with
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
                                    | Some(view, sam) -> 
                                        let info =
                                            VkDescriptorImageInfo(
                                                sam.Handle,
                                                view.Handle,
                                                view.Image.Layout
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

                   )

        let pWrites = NativePtr.pushStackArray writes
        VkRaw.vkUpdateDescriptorSets(device.Handle, uint32 writes.Length, pWrites, 0u, NativePtr.zero) 
            
[<AbstractClass; Sealed; Extension>]
type ContextDescriptorSetExtensions private() =
    [<Extension>]
    static member inline Alloc(this : DescriptorPool, layout : DescriptorSetLayout) =
        this |> DescriptorSet.alloc layout

    [<Extension>]
    static member inline Update(this : DescriptorPool, set : DescriptorSet, values : array<Descriptor>) =
        this |> DescriptorSet.update values set

    [<Extension>]
    static member inline Free(this : DescriptorPool, set : DescriptorSet) =
        this |> DescriptorSet.free set
