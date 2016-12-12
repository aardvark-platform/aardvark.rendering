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

type DescriptorPool =
    class
        inherit Resource<VkDescriptorPool>
        val mutable public SetCount : int
        val mutable public Counts : int[]

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

        descriptorCounts |> NativePtr.withA (fun pDescriptorCounts ->
            let mutable info =
                VkDescriptorPoolCreateInfo(
                    VkStructureType.DescriptorPoolCreateInfo, 0n,
                    VkDescriptorPoolCreateFlags.FreeDescriptorSetBit,
                    uint32 setCount,
                    uint32 descriptorCounts.Length,
                    pDescriptorCounts
                )

            let mutable handle = VkDescriptorPool.Null
            VkRaw.vkCreateDescriptorPool(device.Handle, &&info, NativePtr.zero, &&handle)
                |> check "could not create DescriptorPool"
        
            DescriptorPool(device, handle, setCount, counts)
        )


    let delete (pool : DescriptorPool) (device : Device) =
        if pool.Handle.IsValid then 
            VkRaw.vkDestroyDescriptorPool(device.Handle, pool.Handle, NativePtr.zero)
            pool.Handle <- VkDescriptorPool.Null


[<AbstractClass; Sealed; Extension>]
type ContextDescriptorPoolExtensions private() =
    [<Extension>]
    static member inline CreateDescriptorPool(this : Device, setCount : int, perTypeCount) =
        let counts =
            Map.ofList [
                VkDescriptorType.UniformBuffer, perTypeCount
                VkDescriptorType.CombinedImageSampler, perTypeCount
            ]

        this |> DescriptorPool.create setCount counts

    [<Extension>]
    static member inline Delete(this : Device, pool : DescriptorPool) =
        this |> DescriptorPool.delete pool
