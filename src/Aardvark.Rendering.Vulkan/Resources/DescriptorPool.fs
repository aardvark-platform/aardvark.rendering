namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base

open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
// #nowarn "51"

type DescriptorPool =
    class
        inherit Resource<VkDescriptorPool>
        val mutable public SetCount : int
        val mutable public Counts : int[]

        override x.Destroy() =
            if x.Handle.IsValid then
                VkRaw.vkDestroyDescriptorPool(x.Device.Handle, x.Handle, NativePtr.zero)
                x.Handle <- VkDescriptorPool.Null

        new(device : Device, handle : VkDescriptorPool, total : int, counts : Map<VkDescriptorType, int>) = 
            let arr = Array.init 11 (fun i -> match Map.tryFind (unbox i) counts with | Some v -> v | None -> 0)
            { inherit Resource<_>(device, handle); SetCount = total; Counts = arr }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DescriptorPool =
    let create (setCount : int) (counts : Map<VkDescriptorType, int>) (device : Device) =
 
        let descriptorCounts =
            counts |> Map.toArray |> Array.map (fun (t,c) ->
                VkDescriptorPoolSize(t, uint32 c)  
            )

        native {
            let! pDescriptorCounts = descriptorCounts
            let! pInfo =
                VkDescriptorPoolCreateInfo(
                    VkDescriptorPoolCreateFlags.FreeDescriptorSetBit,
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
            ]

        this |> DescriptorPool.create setCount counts
