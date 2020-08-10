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

[<AllowNullLiteral>]
type DescriptorSetLayoutBinding =
    class
        val mutable public Device : Device
        val mutable public Handle : VkDescriptorSetLayoutBinding
        val mutable public Parameter : ShaderUniformParameter
        member x.StageFlags = x.Handle.stageFlags
        member x.DescriptorCount = int x.Handle.descriptorCount
        member x.Name = x.Parameter.Name
        member x.Binding = int x.Handle.binding 
        member x.DescriptorType = x.Handle.descriptorType

        new (device, handle, parameter) = { Device = device; Handle = handle; Parameter = parameter }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DescriptorSetLayoutBinding =
    let create (descriptorType : VkDescriptorType) (stages : VkShaderStageFlags) (parameter : ShaderUniformParameter) (device : Device) =
        let count = 
            match parameter with
                | SamplerParameter p -> p.samplerCount
                | _ -> 1

        let handle = 
            VkDescriptorSetLayoutBinding(
                uint32 parameter.Binding,
                descriptorType,
                uint32 count,
                stages,
                NativePtr.zero
            )
            
        DescriptorSetLayoutBinding(device, handle, parameter)


type DescriptorSetLayout =
    class
        inherit Resource<VkDescriptorSetLayout>
        val mutable public Bindings : array<DescriptorSetLayoutBinding>
        new(device : Device, handle : VkDescriptorSetLayout, bindings) = { inherit Resource<_>(device, handle); Bindings = bindings }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DescriptorSetLayout =

    let empty (d : Device) = DescriptorSetLayout(d, VkDescriptorSetLayout.Null, Array.empty)

    let create (bindings : array<DescriptorSetLayoutBinding>) (device : Device) =
        assert (bindings |> Seq.mapi (fun i b -> b.Binding = i) |> Seq.forall id)
        native {
            let! pArr = bindings |> Array.map (fun b -> b.Handle)
            let! pInfo =
                VkDescriptorSetLayoutCreateInfo(
                    VkDescriptorSetLayoutCreateFlags.MinValue,
                    uint32 bindings.Length,
                    pArr
                )
            let! pHandle = VkDescriptorSetLayout.Null
            VkRaw.vkCreateDescriptorSetLayout(device.Handle, pInfo, NativePtr.zero, pHandle)
                |> check "could not create DescriptorSetLayout"

            let handle = NativePtr.read pHandle
            return DescriptorSetLayout(device, handle, bindings)
        }


    let delete (layout : DescriptorSetLayout) (device : Device) =
        if layout.Handle.IsValid then
            VkRaw.vkDestroyDescriptorSetLayout(device.Handle, layout.Handle, NativePtr.zero)
            layout.Handle <- VkDescriptorSetLayout.Null

