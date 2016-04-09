namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering

#nowarn "9"
#nowarn "51"

type Descriptor =
    | UniformBuffer of UniformBuffer
    | SampledImage of ImageView * Sampler

type DescriptorPool = 
    class 
        val mutable public Context : Context
        val mutable public Handle : VkDescriptorPool
        val mutable public Counts : Map<VkDescriptorType, int>

        new(ctx,h,c) = { Context = ctx; Handle = h; Counts = c }
    end

type DescriptorSet = 
    class 
        //val mutable public ShaderProgram : ShaderProgram
        val mutable public Pool : DescriptorPool
        val mutable public Handle : VkDescriptorSet
        val mutable public Layout : DescriptorSetLayout

        new(pool, h, layout) = { Pool = pool; Handle = h; Layout = layout }
    end

[<AbstractClass; Sealed; Extension>]
type DescriptorPoolExtensions private() =
    
    [<Extension>]
    static member CreateDescriptorPool(this : Context, count : int) =
        let descriptorCounts =
            [| 
                VkDescriptorPoolSize(VkDescriptorType.UniformBuffer, uint32 count)
                VkDescriptorPoolSize(VkDescriptorType.SampledImage, uint32 count)
                VkDescriptorPoolSize(VkDescriptorType.CombinedImageSampler, uint32 count)
            |]

        let pDescriptorCounts = NativePtr.pushStackArray descriptorCounts
        let mutable info =
            VkDescriptorPoolCreateInfo(
                VkStructureType.DescriptorPoolCreateInfo,
                0n, VkDescriptorPoolCreateFlags.None,
                uint32 (3 * count),
                uint32 descriptorCounts.Length,
                pDescriptorCounts
            )

        let mutable handle = VkDescriptorPool.Null
        VkRaw.vkCreateDescriptorPool(this.Device.Handle, &&info, NativePtr.zero, &&handle) 
            |> check "vkCreateDescriptorPool"

        let counts =
            Map.ofList [
                VkDescriptorType.UniformBuffer, count
                VkDescriptorType.SampledImage, count
                VkDescriptorType.CombinedImageSampler, count
            ]

        DescriptorPool(this, handle, counts)

    [<Extension>]
    static member Delete(this : Context, pool : DescriptorPool) =
        if pool.Handle.IsValid then
            VkRaw.vkDestroyDescriptorPool(this.Device.Handle, pool.Handle, NativePtr.zero)
            pool.Handle <- VkDescriptorPool.Null

[<AbstractClass; Sealed; Extension>]
type DescriptorSetExtensions private() =

    [<Extension>]
    static member CreateDescriptorSet(this : DescriptorPool, layout : DescriptorSetLayout) =
        let mutable layoutHandle = layout.Handle

        let mutable desc = VkDescriptorSet.Null

        let mutable info =
            VkDescriptorSetAllocateInfo(
                VkStructureType.DescriptorSetAllocateInfo,
                0n, this.Handle,
                1u, &&layoutHandle
            )

        VkRaw.vkAllocateDescriptorSets(this.Context.Device.Handle, &&info, &&desc) 
            |> check "vkAllocDescriptorSets"
            
        DescriptorSet(this, desc, layout)  

    [<Extension>]
    static member Update(this : DescriptorSet, elements : Map<int, Descriptor>) =
        let ctx = this.Pool.Context
        let layout = this.Layout
        let cnt = elements.Count
        let mutable bufferInfos = NativePtr.stackalloc cnt
        let mutable imageInfos = NativePtr.stackalloc cnt

        let writes =
            elements
                |> Map.toSeq
                |> Seq.map (fun (binding, desc) ->
                    match desc with
                        | UniformBuffer ub ->
                            let info = 
                                VkDescriptorBufferInfo(
                                    ub.Handle, 
                                    0UL, 
                                    uint64 ub.Storage.Size
                                )

                            NativePtr.write bufferInfos info
                            let ptr = bufferInfos
                            bufferInfos <- NativePtr.step 1 bufferInfos

                            VkWriteDescriptorSet(
                                VkStructureType.WriteDescriptorSet, 0n,
                                this.Handle,
                                uint32 binding,
                                0u, 1u, VkDescriptorType.UniformBuffer,
                                NativePtr.zero,
                                ptr,
                                NativePtr.zero
                            )
                        | SampledImage(view, sam) ->
                            let info =
                                VkDescriptorImageInfo(
                                    sam.Handle,
                                    view.Handle,
                                    view.Image.Layout
                                )

                            NativePtr.write imageInfos info
                            let ptr = imageInfos
                            imageInfos <- NativePtr.step 1 imageInfos

                            VkWriteDescriptorSet(
                                VkStructureType.WriteDescriptorSet, 0n,
                                this.Handle,
                                uint32 binding,
                                0u, 1u, VkDescriptorType.SampledImage,
                                ptr,
                                NativePtr.zero,
                                NativePtr.zero
                            )

                   )
                |> Seq.toArray

        let pWrites = NativePtr.pushStackArray writes
        VkRaw.vkUpdateDescriptorSets(ctx.Device.Handle, uint32 writes.Length, pWrites, 0u, NativePtr.zero) 
            
    [<Extension>]
    static member Delete(this : DescriptorPool, set : DescriptorSet) =
        if set.Handle.IsValid then
            let mutable handle = set.Handle
            VkRaw.vkFreeDescriptorSets(this.Context.Device.Handle, this.Handle, 1u, &&handle) 
                |> check "vkFreeDescriptorSets"
            set.Handle <- VkDescriptorSet.Null