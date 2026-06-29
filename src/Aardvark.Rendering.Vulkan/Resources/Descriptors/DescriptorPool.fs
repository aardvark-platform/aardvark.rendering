namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open EXTDescriptorIndexing

type DescriptorPool =
    class
        inherit Resource<VkDescriptorPool>
        val public MaxSets : int
        val public Counts  : Map<VkDescriptorType, int>
        val mutable private activeSets : int

        member this.IsEmpty = this.activeSets = 0

        member this.FreeSet() =
            dec &this.activeSets
            if this.activeSets = 0 then
                VkRaw.vkResetDescriptorPool(this.Device.Handle, this.Handle, VkDescriptorPoolResetFlags.None)
                |> check "could not reset descriptor pool"

        member this.TryAllocateSet(counts: Map<VkDescriptorType, int>) =
            if this.activeSets < this.MaxSets then
                // Trying to allocate more descriptors than the pool's total descriptor count will not reliably return
                // VK_ERROR_OUT_OF_POOL_MEMORY_KHR -> check it manually
                let exceedsTotal =
                    counts |> Map.exists (fun typ count ->
                        let total = this.Counts |> Map.tryFindV typ |> ValueOption.defaultValue 0
                        count > total
                    )
                if not exceedsTotal then
                    inc &this.activeSets
                    true
                else
                    false
            else
                false

        override x.Destroy() =
            if x.Handle.IsValid then
                VkRaw.vkDestroyDescriptorPool(x.Device.Handle, x.Handle, NativePtr.zero)
                x.Handle <- VkDescriptorPool.Null

        new(device: Device, handle: VkDescriptorPool, maxSets: int, counts: Map<VkDescriptorType, int>) =
            { inherit Resource<_>(device, handle); MaxSets = maxSets; Counts = counts; activeSets = 0 }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DescriptorPool =
    let create (setCount : int) (counts : Map<VkDescriptorType, int>) (device : Device) =
 
        let descriptorCounts =
            counts |> Map.toArray |> Array.map (fun (t,c) ->
                VkDescriptorPoolSize(t, uint32 c)  
            )

        let flags =
            if device.UpdateDescriptorsAfterBind then
                VkDescriptorPoolCreateFlags.UpdateAfterBindBitExt
            else
                VkDescriptorPoolCreateFlags.None

        native {
            let! pDescriptorCounts = descriptorCounts
            let! pInfo =
                VkDescriptorPoolCreateInfo(
                    flags,
                    uint32 setCount,
                    uint32 descriptorCounts.Length,
                    pDescriptorCounts
                )

            let! pHandle = VkDescriptorPool.Null
            VkRaw.vkCreateDescriptorPool(device.Handle, pInfo, NativePtr.zero, pHandle)
                |> check "could not create DescriptorPool"

            return new DescriptorPool(device, !!pHandle, setCount, counts)

        }