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
        /// True for an unbounded ('sampler2D X[]', samplerCount = -1) sampler array or
        /// for an unbounded ('buffer {..} X[]', ssbCount = -1) storage-buffer array.
        member x.IsUnboundedArray =
            match x.Parameter with
            | SamplerParameter p -> p.samplerCount < 0
            | StorageBufferParameter p -> p.ssbCount < 0
            | _ -> false

        new (device, handle, parameter) = { Device = device; Handle = handle; Parameter = parameter }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DescriptorSetLayoutBinding =

    // Capacity of an unbounded (bindless) array binding: RuntimeConfig.UnboundedArrayCapacity
    // clamped to the per-stage descriptor limit of the device. With variable descriptor counts
    // a set allocates only the elements it uses (the resources grow on demand), so the capacity
    // is just the upper bound; otherwise every set reserves the full capacity.
    let private unboundedArrayCapacity (limit : uint32) =
        let capacity = uint32 (max 1 RuntimeConfig.UnboundedArrayCapacity)
        int (min capacity limit)

    /// Device-clamped capacity for an unbounded sampler-array binding.
    let unboundedSamplerArrayCapacity (device : Device) =
        unboundedArrayCapacity device.PhysicalDevice.Limits.Descriptor.MaxPerStageSampledImages

    /// Device-clamped capacity for an unbounded storage-buffer-array binding.
    let unboundedStorageBufferArrayCapacity (device : Device) =
        unboundedArrayCapacity device.PhysicalDevice.Limits.Descriptor.MaxPerStageStorageBuffers

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
                // ssbCount = -1 marks an unbounded (bindless) storage-buffer array
                // ('buffer {..} X[]', from a T[][] storage buffer); reserve a
                // device-clamped capacity (variable descriptor count narrows it).
                | StorageBufferParameter p ->
                    if p.ssbCount < 0 then
                        let d = device.EnabledFeatures.Descriptors
                        let s = device.EnabledFeatures.Shaders
                        if not (d.RuntimeDescriptorArray && s.StorageBufferArrayNonUniformIndexing) then
                            failf "shader uses an unbounded storage-buffer array (bindless) but the device does not support descriptor indexing (runtimeDescriptorArray + shaderStorageBufferArrayNonUniformIndexing)"
                        unboundedStorageBufferArrayCapacity device
                    else max 1 p.ssbCount
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
        val public Bindings : DescriptorSetLayoutBinding[]

        /// Binding number of the binding declared with VARIABLE_DESCRIPTOR_COUNT
        /// (an unbounded sampler or storage-buffer array with the highest binding
        /// number), or -1 if there is none. Sets allocated from this layout may then
        /// specify a per-set count up to that binding's (capped) descriptorCount.
        member this.VariableCountBinding =
            match Array.tryLastV this.Bindings with
            | ValueSome b when b.IsUnboundedArray -> b.Binding
            | _ -> -1

        member this.GetDescriptorCounts(variableCount: int) =
            let variableCountBinding = this.VariableCountBinding

            (Map.empty, this.Bindings) ||> Array.fold (fun result binding ->
                let sum = result |> Map.tryFindV binding.DescriptorType |> ValueOption.defaultValue 0
                let count =
                    if binding.Binding = variableCountBinding then
                        variableCount
                    else
                        binding.DescriptorCount

                result |> Map.add binding.DescriptorType (sum + count)
            )

        override this.Destroy() =
            if this.Handle.IsValid then
                VkRaw.vkDestroyDescriptorSetLayout(this.Device.Handle, this.Handle, NativePtr.zero)
                this.Handle <- VkDescriptorSetLayout.Null

        new(device : Device, handle : VkDescriptorSetLayout, bindings : DescriptorSetLayoutBinding[]) =
            { inherit Resource<_>(device, handle); Bindings = bindings }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DescriptorSetLayout =

    let empty (d : Device) = new DescriptorSetLayout(d, VkDescriptorSetLayout.Null, Array.empty)

    let create (bindings : array<DescriptorSetLayoutBinding>) (device : Device) =
        // Binding numbers must be strictly increasing (the variable-count
        // selection below relies on that ordering). An array binding does not
        // consume binding numbers in Vulkan, so densely numbered layouts with a
        // DescriptorCount > 1 binding in the middle are valid; the stricter
        // invariant "binding = prefix sum of the preceding DescriptorCounts"
        // is not required.
        assert (
            bindings |> Array.pairwise |> Array.forall (fun (a, b) -> a.Binding < b.Binding)
        )

        let features =
            device.EnabledFeatures.Descriptors

        // The unbounded array with the highest binding number may use a variable
        // descriptor count (each set allocates only the elements it uses) when the
        // device supports it and the bindings are update-after-bind. The
        // variable-count binding must have the largest binding number in the set.
        let variableCountBinding =
            if device.UpdateDescriptorsAfterBind && features.BindingVariableDescriptorCount && features.BindingPartiallyBound then
                match Array.tryLastV bindings with
                | ValueSome b when b.IsUnboundedArray -> b.Binding
                | _ -> -1
            else
                -1

        native {
            let! pBindings = bindings |> Array.map _.Handle

            let! pBindingFlags =
                bindings |> Array.map (fun b ->
                    let updateAfterBind =
                        if b.DescriptorType = VkDescriptorType.UniformBuffer then
                            features.BindingUniformBufferUpdateAfterBind

                        elif b.DescriptorType = VkDescriptorType.AccelerationStructureKhr then
                            features.BindingAccelerationStructureUpdateAfterBind

                        else
                            // other features are mandatory if VK_EXT_descriptor_indexing is supported
                            true

                    let mutable flags =
                        if updateAfterBind then VkDescriptorBindingFlagsEXT.UpdateAfterBindBit
                        else VkDescriptorBindingFlagsEXT.None

                    // Every unbounded array reserves a fixed capacity but only writes the
                    // elements actually used, so each must be partially bound (unwritten
                    // descriptors stay invalid). This is independent of the variable
                    // descriptor count, which the spec permits on only one binding per
                    // set (the highest); a second unbounded array in the same set needs
                    // the partially-bound flag as well.
                    if b.IsUnboundedArray && features.BindingPartiallyBound then
                        flags <- flags ||| VkDescriptorBindingFlagsEXT.PartiallyBoundBit

                    if b.Binding = variableCountBinding then
                        flags <- flags ||| VkDescriptorBindingFlagsEXT.VariableDescriptorCountBit

                    flags
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