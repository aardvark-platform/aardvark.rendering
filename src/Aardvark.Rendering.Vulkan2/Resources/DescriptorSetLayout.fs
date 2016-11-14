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

type DescriptorSetLayoutBinding =
    class
        inherit Resource<VkDescriptorSetLayoutBinding>
        val mutable public Parameter : ShaderParameter
        member x.StageFlags = x.Handle.stageFlags
        member x.DescriptorCount = int x.Handle.descriptorCount
        member x.Name = x.Parameter.paramName
        member x.Type = x.Parameter.paramType
        member x.Binding = int x.Handle.binding 

        new (device, handle, parameter) = { inherit Resource<_>(device, handle); Parameter = parameter }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DescriptorSetLayoutBinding =
    let create (descriptorType : VkDescriptorType) (stages : VkShaderStageFlags) (parameter : ShaderParameter) (device : Device) =
        let handle = 
            VkDescriptorSetLayoutBinding(
                parameter |> ShaderParameter.tryGetBinding |> Option.get |> uint32,
                descriptorType,
                parameter |> ShaderParameter.getArraySize |> uint32,
                stages,
                NativePtr.zero
            )
            
        DescriptorSetLayoutBinding(device, handle, parameter)


type DescriptorSetLayout =
    class
        inherit Resource<VkDescriptorSetLayout>
        val mutable public Bindings : list<DescriptorSetLayoutBinding>
        new(device : Device, handle : VkDescriptorSetLayout, bindings) = { inherit Resource<_>(device, handle); Bindings = bindings }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DescriptorSetLayout =
    let create (bindings : list<DescriptorSetLayoutBinding>) (device : Device) =
        let arr = bindings |> List.map (fun b -> b.Handle) |> List.toArray

        arr |> NativePtr.withA (fun pArr ->
            let mutable info =
                VkDescriptorSetLayoutCreateInfo(
                    VkStructureType.DescriptorSetLayoutCreateInfo, 0n,
                    VkDescriptorSetLayoutCreateFlags.MinValue,
                    uint32 arr.Length,
                    pArr
                )

            let mutable handle = VkDescriptorSetLayout.Null
            VkRaw.vkCreateDescriptorSetLayout(device.Handle, &&info, NativePtr.zero, &&handle)
                |> check "could not create DescriptorSetLayout"

            DescriptorSetLayout(device, handle, bindings)
        )

    let delete (layout : DescriptorSetLayout) (device : Device) =
        if layout.Handle.IsValid then
            VkRaw.vkDestroyDescriptorSetLayout(device.Handle, layout.Handle, NativePtr.zero)
            layout.Handle <- VkDescriptorSetLayout.Null

