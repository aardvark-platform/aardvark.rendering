﻿namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System.Runtime.CompilerServices
open EXTDescriptorIndexing
open KHRAccelerationStructure

type DescriptorPool =
    class
        inherit Resource<VkDescriptorPool>
        val public Counts : int[]
        val mutable private capacity : int

        member x.Capacity =
            x.capacity

        member x.FreeSet() =
            inc &x.capacity

        member x.TryAllocateSet() =
            if x.capacity <= 0 then false
            else
                dec &x.capacity
                true

        override x.Destroy() =
            if x.Handle.IsValid then
                VkRaw.vkDestroyDescriptorPool(x.Device.Handle, x.Handle, NativePtr.zero)
                x.Handle <- VkDescriptorPool.Null

        new(device : Device, handle : VkDescriptorPool, total : int, counts : Map<VkDescriptorType, int>) = 
            let arr = Array.init 11 (fun i -> match Map.tryFind (unbox i) counts with | Some v -> v | None -> 0)
            { inherit Resource<_>(device, handle); capacity = total; Counts = arr }
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
                VkDescriptorPoolCreateFlags.FreeDescriptorSetBit |||
                VkDescriptorPoolCreateFlags.UpdateAfterBindBitExt
            else
                VkDescriptorPoolCreateFlags.FreeDescriptorSetBit

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

[<AbstractClass; Sealed; Extension>]
type ContextDescriptorPoolExtensions private() =
    [<Extension>]
    static member inline CreateDescriptorPool(this : Device, setCount : int, perTypeCount) =
        let counts =
            Map.ofList [
                VkDescriptorType.UniformBuffer, perTypeCount
                VkDescriptorType.StorageBuffer, perTypeCount
                VkDescriptorType.CombinedImageSampler, perTypeCount
                VkDescriptorType.StorageImage, perTypeCount
                if this.IsExtensionEnabled KHRAccelerationStructure.Name then
                    VkDescriptorType.AccelerationStructureKhr, perTypeCount
            ]

        this |> DescriptorPool.create setCount counts
