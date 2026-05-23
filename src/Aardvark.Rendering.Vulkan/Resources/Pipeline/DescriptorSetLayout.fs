namespace Aardvark.Rendering.Vulkan


open Aardvark.Base
open Microsoft.FSharp.NativeInterop
open EXTDescriptorIndexing
open KHRAccelerationStructure

#nowarn "9"

[<AllowNullLiteral>]
type DescriptorSetLayoutBinding =
    class
        val public Device : Device
        val public Handle : VkDescriptorSetLayoutBinding
        val public Parameter : ShaderUniformParameter
        member x.StageFlags = x.Handle.stageFlags
        member x.DescriptorCount = int x.Handle.descriptorCount
        member x.Name = x.Parameter.Name
        member x.Binding = int x.Handle.binding 
        member x.DescriptorType = x.Handle.descriptorType

        new (device, handle, parameter) = { Device = device; Handle = handle; Parameter = parameter }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DescriptorSetLayoutBinding =

    /// Ceiling for an unbounded (bindless) sampler array's reserved descriptor
    /// capacity. This is only the layout upper bound (and the per-set count when
    /// variable descriptor count is unavailable); with variable descriptor count
    /// each set allocates just the textures it actually uses. Clamped per device.
    [<Literal>]
    let UnboundedSamplerArrayCeiling = 1024u

    /// Device-clamped capacity for an unbounded sampler-array binding.
    let unboundedSamplerArrayCapacity (device : Device) =
        int (min UnboundedSamplerArrayCeiling device.PhysicalDevice.Limits.Descriptor.MaxPerStageSampledImages)

    let create (descriptorType : VkDescriptorType) (stages : VkShaderStageFlags) (parameter : ShaderUniformParameter) (device : Device) =
        let count =
            match parameter with
                // samplerCount = -1 marks an unbounded (bindless) sampler array
                // ('sampler2D X[]'). It requires descriptor indexing; reserve a
                // device-clamped capacity (variable descriptor count narrows the
                // per-set allocation; unused slots are null-filled / partially bound).
                | SamplerParameter p ->
                    if p.samplerCount < 0 then
                        let d = device.EnabledFeatures.Descriptors
                        let s = device.EnabledFeatures.Shaders
                        if not (d.RuntimeDescriptorArray && s.SampledImageArrayNonUniformIndexing) then
                            failf "shader uses an unbounded sampler array (bindless) but the device does not support descriptor indexing (runtimeDescriptorArray + shaderSampledImageArrayNonUniformIndexing)"
                        unboundedSamplerArrayCapacity device
                    else p.samplerCount
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
        val public Bindings : array<DescriptorSetLayoutBinding>

        override x.Destroy() =
            if x.Handle.IsValid then
                VkRaw.vkDestroyDescriptorSetLayout(x.Device.Handle, x.Handle, NativePtr.zero)
                x.Handle <- VkDescriptorSetLayout.Null

        new(device : Device, handle : VkDescriptorSetLayout, bindings) =
            { inherit Resource<_>(device, handle); Bindings = bindings }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DescriptorSetLayout =

    let empty (d : Device) = new DescriptorSetLayout(d, VkDescriptorSetLayout.Null, Array.empty)

    let create (bindings : array<DescriptorSetLayoutBinding>) (device : Device) =
        assert (
            let offsets = (0, bindings) ||> Array.scan (fun o b -> o + b.DescriptorCount) |> Array.take bindings.Length
            (bindings, offsets) ||> Array.map2 (fun b o -> b.Binding = o) |> Array.forall id
        )

        let features =
            device.EnabledFeatures.Descriptors

        native {
            let! pBindings = bindings |> Array.map (fun b -> b.Handle)

            let! pBindingFlags =
                bindings |> Array.map (fun b ->
                    if b.DescriptorType = VkDescriptorType.UniformBuffer then
                        features.BindingUniformBufferUpdateAfterBind

                    elif b.DescriptorType = VkDescriptorType.AccelerationStructureKhr then
                        features.BindingAccelerationStructureUpdateAfterBind

                    else
                        // other features are mandatory if VK_EXT_descriptor_indexing is supported
                        true
                )
                |> Array.map (fun updateAfterBind ->
                    if updateAfterBind then VkDescriptorBindingFlagsEXT.UpdateAfterBindBit
                    else VkDescriptorBindingFlagsEXT.None
                )

            let! pBindingFlagsCreateInfo =
                VkDescriptorSetLayoutBindingFlagsCreateInfoEXT(
                    uint32 bindings.Length,
                    pBindingFlags
                )

            let pNext, flags =
                if device.UpdateDescriptorsAfterBind then
                    NativePtr.toNativeInt pBindingFlagsCreateInfo,
                    VkDescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBitExt
                else
                    0n,
                    VkDescriptorSetLayoutCreateFlags.None

            let! pInfo =
                VkDescriptorSetLayoutCreateInfo(
                    pNext, flags,
                    uint32 bindings.Length,
                    pBindings
                )

            let! pHandle = VkDescriptorSetLayout.Null
            VkRaw.vkCreateDescriptorSetLayout(device.Handle, pInfo, NativePtr.zero, pHandle)
                |> check "could not create DescriptorSetLayout"

            let handle = NativePtr.read pHandle
            return new DescriptorSetLayout(device, handle, bindings)
        }