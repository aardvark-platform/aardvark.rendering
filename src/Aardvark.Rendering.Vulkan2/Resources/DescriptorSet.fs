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