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
        /// True for an unbounded ('sampler2D X[]', samplerCount = -1) sampler array.
        member x.IsUnboundedSamplerArray =
            match x.Parameter with
            | SamplerParameter p -> p.samplerCount < 0
            | _ -> false
        /// True for an unbounded ('buffer {..} X[]', ssbCount = -1) storage-buffer array.
        member x.IsUnboundedStorageBufferArray =
            match x.Parameter with
            | StorageBufferParameter p -> p.ssbCount < 0
            | _ -> false

        new (device, handle, parameter) = { Device = device; Handle = handle; Parameter = parameter }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DescriptorSetLayoutBinding =

    /// Ceiling for an unbounded (bindless) sampler / storage-buffer array's reserved
    /// descriptor capacity. This is only the layout upper bound (and the per-set count
    /// when variable descriptor count is unavailable); with variable descriptor count
    /// each set allocates just the textures it actually uses. Clamped per device.
    ///
    /// NOT a free knob, deliberately kept at 1024 rather than lifted to the device
    /// limit (~2^20 per-stage storage buffers on desktop):
    ///   * for a NON-constant unbounded binding (e.g. the heap's HeapVertexData /
    ///     HeapVertexDataI aval&lt;IBuffer[]&gt;) the AdaptiveDescriptor cache and
    ///     DescriptorSetResource version mirrors are allocated at this capacity PER
    ///     SET (PreparedRenderObject falls back to b.DescriptorCount when the array
    ///     aval isn't constant), so the CPU cost scales linearly with it;
    ///   * descriptor POOLS budget `perTypeCount * 8` for the array-capable types
    ///     (ContextDescriptorPoolExtensions.CreateDescriptorPool) — a set holding
    ///     several full-capacity unbounded arrays must still fit one pool, so raising
    ///     the ceiling requires scaling that budget in lockstep;
    ///   * without variable descriptor count / update-after-bind every set allocates
    ///     the FULL capacity up front.
    /// Consequence for the heap's vertex-pull path: at most 1024 (slots × attributes)
    /// per bucket. Lifting it = raise this constant + the pool budget + accept the
    /// capacity-proportional per-set mirrors (or make those grow on demand).
    [<Literal>]
    let UnboundedSamplerArrayCeiling = 1024u

    /// Device-clamped capacity for an unbounded sampler-array binding.
    let unboundedSamplerArrayCapacity (device : Device) =
        int (min UnboundedSamplerArrayCeiling device.PhysicalDevice.Limits.Descriptor.MaxPerStageSampledImages)

    /// Device-clamped capacity for an unbounded storage-buffer-array binding.
    let unboundedStorageBufferArrayCapacity (device : Device) =
        int (min UnboundedSamplerArrayCeiling device.PhysicalDevice.Limits.Descriptor.MaxPerStageStorageBuffers)

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
        val public Bindings : array<DescriptorSetLayoutBinding>
        /// Binding number of the binding declared with VARIABLE_DESCRIPTOR_COUNT
        /// (an unbounded sampler array, highest binding number), if any. Sets
        /// allocated from this layout may then specify a per-set count up to that
        /// binding's (capped) descriptorCount.
        val public VariableCountBinding : Option<int>

        override x.Destroy() =
            if x.Handle.IsValid then
                VkRaw.vkDestroyDescriptorSetLayout(x.Device.Handle, x.Handle, NativePtr.zero)
                x.Handle <- VkDescriptorSetLayout.Null

        new(device : Device, handle : VkDescriptorSetLayout, bindings, variableCountBinding) =
            { inherit Resource<_>(device, handle); Bindings = bindings; VariableCountBinding = variableCountBinding }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DescriptorSetLayout =

    let empty (d : Device) = new DescriptorSetLayout(d, VkDescriptorSetLayout.Null, Array.empty, None)

    let create (bindings : array<DescriptorSetLayoutBinding>) (device : Device) =
        // Binding numbers must be strictly increasing (the variable-count
        // selection below relies on max-binding ordering). NOTE: the former
        // invariant "binding = prefix sum of preceding DescriptorCounts"
        // predates unbounded descriptor arrays — an array binding does NOT
        // consume binding numbers in Vulkan, so densely numbered layouts
        // with a DescriptorCount > 1 binding in the middle are valid (and
        // exactly what the heap's bindless paths produce in Debug builds).
        assert (
            bindings |> Array.pairwise |> Array.forall (fun (a, b) -> a.Binding < b.Binding)
        )

        let features =
            device.EnabledFeatures.Descriptors

        // The unbounded sampler array with the highest binding number may use a
        // VARIABLE descriptor count (each set allocates only the textures it uses)
        // when the device supports it and the bindings are update-after-bind. The
        // variable-count binding must have the largest binding number in the set.
        let variableCountBinding =
            if device.UpdateDescriptorsAfterBind && features.BindingVariableDescriptorCount && features.BindingPartiallyBound then
                let unbounded = bindings |> Array.filter (fun b -> b.IsUnboundedSamplerArray || b.IsUnboundedStorageBufferArray)
                if unbounded.Length > 0 then
                    let maxBinding = bindings |> Array.map (fun b -> b.Binding) |> Array.max
                    let candidate = unbounded |> Array.maxBy (fun b -> b.Binding)
                    if candidate.Binding = maxBinding then Some candidate.Binding else None
                else None
            else None

        native {
            let! pBindings = bindings |> Array.map (fun b -> b.Handle)

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

                    // Every unbounded (bindless) array reserves a fixed capacity (1024)
                    // but only writes the N elements actually used, so each must be
                    // PARTIALLY_BOUND (unwritten descriptors stay invalid). This is
                    // INDEPENDENT of VARIABLE_DESCRIPTOR_COUNT, which the spec permits on
                    // only ONE binding per set (the highest). Earlier only the variable-
                    // count binding got PartiallyBound, so a second/third unbounded array
                    // (e.g. HeapPositions + HeapNormals + HeapIndex) mis-bound.
                    if (b.IsUnboundedSamplerArray || b.IsUnboundedStorageBufferArray) && features.BindingPartiallyBound then
                        flags <- flags ||| VkDescriptorBindingFlagsEXT.PartiallyBoundBit

                    if variableCountBinding = Some b.Binding then
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
            return new DescriptorSetLayout(device, handle, bindings, variableCountBinding)
        }