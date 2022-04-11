namespace Aardvark.Rendering.Vulkan

open System
open Aardvark.Base


type ImageLimits =
    {
        /// the maximum dimension of an image created with an imageType of VK_IMAGE_TYPE_1D
        MaxDimension1D : int

        /// the maximum dimension of an image created with an imageType of VK_IMAGE_TYPE_2D and without VK_IMAGE_CREATE_CUBE_COMPATIBLE_BIT set in flags.
        MaxDimension2D : V2i

        /// the maximum dimension (width, height, or depth) of an image created with an imageType of VK_IMAGE_TYPE_3D.
        MaxDimension3D : V3i

        /// the maximum dimension (width or height) of an image created with an imageType of VK_IMAGE_TYPE_2D and with VK_IMAGE_CREATE_CUBE_COMPATIBLE_BIT set in flags.
        MaxDimensionCube : V2i

        /// the maximum number of layers (arrayLayers) for an image.
        MaxArrayLayers : int
    }

    member x.Print(l : ILogger) =
        l.line "max 1D size:   %d" x.MaxDimension1D
        l.line "max 2D size:   %A" x.MaxDimension2D
        l.line "max 3D size:   %A" x.MaxDimension3D
        l.line "max Cube size: %A" x.MaxDimensionCube
        l.line "max layers:    %d" x.MaxArrayLayers

type SampledImageLimits =
    {
        /// the sample counts supported for all 2D images created with VK_IMAGE_TILING_OPTIMAL, usage containing VK_IMAGE_USAGE_SAMPLED_BIT, and a non-integer color format.
        ColorSampleCounts : Set<int>

        /// the sample counts supported for all 2D images created with VK_IMAGE_TILING_OPTIMAL, usage containing VK_IMAGE_USAGE_SAMPLED_BIT, and an integer color format.
        IntegerSampleCounts : Set<int>

        /// the sample counts supported for all 2D images created with VK_IMAGE_TILING_OPTIMAL, usage containing VK_IMAGE_USAGE_SAMPLED_BIT, and a depth format.
        DepthSampleCounts : Set<int>

        /// the sample supported for all 2D images created with VK_IMAGE_TILING_OPTIMAL, usage containing VK_IMAGE_USAGE_SAMPLED_BIT, and a stencil format.
        StencilSampleCounts : Set<int>

        /// the sample counts supported for all 2D images created with VK_IMAGE_TILING_OPTIMAL, and usage containing VK_IMAGE_USAGE_STORAGE_BIT.
        StorageSampleCounts : Set<int>
    }

    member x.Print(l : ILogger) =
        l.line "color samples:      %A" (Set.toList x.ColorSampleCounts)
        l.line "integer samples:    %A" (Set.toList x.IntegerSampleCounts)
        l.line "depth samples:      %A" (Set.toList x.DepthSampleCounts)
        l.line "stencil samples:    %A" (Set.toList x.StencilSampleCounts)
        l.line "storage samples:    %A" (Set.toList x.StorageSampleCounts)

type SamplerLimits =
    {
        /// the maximum number of sampler objects, as created by vkCreateSampler, which can simultaneously exist on a device.
        MaxAllocationCount : int

        /// the maximum absolute sampler level of detail bias.
        MaxLodBias : float

        /// the maximum degree of sampler anisotropy.
        MaxAnisotropy : float
    }

    member x.Print(l : ILogger) =
        l.line "max allocations: %A" x.MaxAllocationCount
        l.line "max lod bias:    %A" x.MaxLodBias
        l.line "max anisotropy:  %A" x.MaxAnisotropy

type UniformLimits =
    {
        /// the maximum size for a buffer view that is used with VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER or VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER_DYNAMIC.
        MaxBufferViewRange : Mem

        /// the maximum size for a buffer view that is used with VK_DESCRIPTOR_TYPE_STORAGE_BUFFER or VK_DESCRIPTOR_TYPE_STORAGE_BUFFER_DYNAMIC.
        MaxStorageViewRange : Mem

        /// the maximum number of addressable texels for a buffer view created on a buffer which was created with the VK_BUFFER_USAGE_UNIFORM_TEXEL_BUFFER_BIT or VK_BUFFER_USAGE_STORAGE_TEXEL_BUFFER_BIT set
        MaxTexelBufferElements : int

        /// the maximum size, in bytes, of the pool of push constant memory.
        MaxPushConstantsSize : Mem
    }

    member x.Print(l : ILogger) =
        l.line "max buffer range:   %A" x.MaxBufferViewRange
        l.line "max storage range:  %A" x.MaxStorageViewRange
        l.line "max texel elements: %A" x.MaxTexelBufferElements
        l.line "max push constants: %A" x.MaxPushConstantsSize

type MemoryLimits =
    {
        /// the maximum number of device memory allocations, as created by vkAllocateMemory, which can simultaneously exist.
        MaxAllocationCount : int

        MaxAllocationSize : Mem

        /// the total amount of address space available, in bytes, for sparse memory resources. This is an upper bound on the sum of the size of all sparse resources, regardless of whether any memory is bound to them.
        SparseAddressSpaceSize : Mem

        /// the granularity, in bytes, at which buffer or linear image resources, and optimal image resources can be bound to adjacent offsets in the same VkDeviceMemory object without aliasing.
        BufferImageGranularity : int64

        /// the minimum required alignment, in bytes, of host visible memory allocations within the host address space. When mapping a memory allocation with vkMapMemory, subtracting offset bytes from the returned pointer will always produce an integer multiple of this limit.
        MinMemoryMapAlignment : int64

        /// the minimum required alignment, in bytes, for the offset member of the VkBufferViewCreateInfo structure for texel buffers.
        MinTexelBufferOffsetAlignment : int64

        /// the minimum required alignment, in bytes, for the offset member of the VkDescriptorBufferInfo structure for uniform buffers.
        MinUniformBufferOffsetAlignment : int64

        /// the minimum required alignment, in bytes, for the offset member of the VkDescriptorBufferInfo structure for storage buffers.
        MinStorageBufferOffsetAlignment : int64

        /// the optimal buffer offset alignment in bytes for vkCmdCopyBufferToImage and vkCmdCopyImageToBuffer. The per texel alignment requirements are still enforced, this is just an additional alignment recommendation for optimal performance and power.
        OptimalBufferCopyOffsetAlignment : int64

        /// the optimal buffer row pitch alignment in bytes for vkCmdCopyBufferToImage and vkCmdCopyImageToBuffer. Row pitch is the number of bytes between texels with the same X coordinate in adjacent rows (Y coordinates differ by one). The per texel alignment requirements are still enforced, this is just an additional alignment recommendation for optimal performance and power.
        OptimalBufferCopyRowPitchAlignment : int64

        /// the size and alignment in bytes that bounds concurrent access to host-mapped device memory.
        NonCoherentAtomSize : int64

    }

    member x.Print(l : ILogger) =
        l.line "max allocations:            %d" x.MaxAllocationCount
        l.line "max allocation size:        %A" x.MaxAllocationSize
        l.line "sparse size:                %A" x.SparseAddressSpaceSize
        l.line "buffer/image distance:      %d" x.BufferImageGranularity
        l.line "map alignment:              %d" x.MinMemoryMapAlignment
        l.line "texel offset align:         %d" x.MinTexelBufferOffsetAlignment
        l.line "storage offset align:       %d" x.MinStorageBufferOffsetAlignment
        l.line "buffer/image offset align:  %d" x.OptimalBufferCopyOffsetAlignment
        l.line "buffer/image row align:     %d" x.OptimalBufferCopyRowPitchAlignment
        l.line "non coherent atom size:     %d" x.NonCoherentAtomSize

type DescriptorLimits =
    {
        /// the maximum number of descriptor sets that can be simultaneously used by a pipeline.
        MaxBoundDescriptorSets : int

        /// the maximum number of samplers that can be accessible to a single shader stage in a pipeline layout.
        MaxPerStageSamplers : int

        /// the maximum number of uniform buffers that can be accessible to a single shader stage in a pipeline layout.
        MaxPerStageUniformBuffers : int

        /// the maximum number of storage buffers that can be accessible to a single shader stage in a pipeline layout.
        MaxPerStageStorageBuffers : int

        /// the maximum number of sampled images that can be accessible to a single shader stage in a pipeline layout.
        MaxPerStageSampledImages : int

        /// the maximum number of storage images that can be accessible to a single shader stage in a pipeline layout.
        MaxPerStageStorageImages : int

        /// the maximum number of input attachments that can be accessible to a single shader stage in a pipeline layout.
        MaxPerStageInputAttachments : int

        /// the maximum number of resources that can be accessible to a single shader stage in a pipeline layout.
        MaxPerStageResources : int


        /// the maximum number of samplers that can be included in descriptor bindings in a pipeline layout across all pipeline shader stages and descriptor set numbers.
        MaxSamplers : int

        /// the maximum number of uniform buffers that can be included in descriptor bindings in a pipeline layout across all pipeline shader stages and descriptor set numbers.
        MaxUniformBuffers : int

        /// the maximum number of dynamic uniform buffers that can be included in descriptor bindings in a pipeline layout across all pipeline shader stages and descriptor set numbers.
        MaxUniformBuffersDynamic : int

        /// the maximum number of storage buffers that can be included in descriptor bindings in a pipeline layout across all pipeline shader stages and descriptor set numbers.
        MaxStorageBuffers : int

        /// the maximum number of dynamic storage buffers that can be included in descriptor bindings in a pipeline layout across all pipeline shader stages and descriptor set numbers.
        MaxStorageBuffersDynamic : int

        /// the maximum number of sampled images that can be included in descriptor bindings in a pipeline layout across all pipeline shader stages and descriptor set numbers.
        MaxSampledImages : int

        /// the maximum number of storage images that can be included in descriptor bindings in a pipeline layout across all pipeline shader stages and descriptor set numbers.
        MaxStorageImages : int

        /// the maximum number of input attachments that can be included in descriptor bindings in a pipeline layout across all pipeline shader stages and descriptor set numbers.
        MaxInputAttachments : int

    }

    member x.Print(l : ILogger) =
        l.line "max bound sets:          %d" x.MaxBoundDescriptorSets
        l.line "max samplers:            %d" x.MaxSamplers
        l.line "max uniform buffers:     %d" x.MaxUniformBuffers
        l.line "max dyn-uniform buffers: %d" x.MaxUniformBuffersDynamic
        l.line "max storage buffers:     %d" x.MaxStorageBuffers
        l.line "max dyn-storage buffers: %d" x.MaxStorageBuffersDynamic
        l.line "max sampled images:      %d" x.MaxSampledImages
        l.line "max storage images:      %d" x.MaxStorageImages
        l.line "max input attachments:   %d" x.MaxInputAttachments
        l.section "per stage:" (fun () ->
            l.line "max samplers:          %d" x.MaxPerStageSamplers
            l.line "max uniform buffers:   %d" x.MaxPerStageUniformBuffers
            l.line "max storage buffers:   %d" x.MaxPerStageStorageBuffers
            l.line "max sampled images:    %d" x.MaxPerStageSampledImages
            l.line "max storage images:    %d" x.MaxPerStageStorageImages
            l.line "max input attachments: %d" x.MaxPerStageInputAttachments
            l.line "max resources:         %d" x.MaxPerStageResources
        )

type VertexLimits =
    {
        /// the maximum number of vertex input attributes that can be specified for a graphics pipeline.
        MaxInputAttributes : int

        /// the maximum number of vertex buffers that can be specified for providing vertex attributes to a graphics pipeline.
        MaxInputBindings : int

        /// the maximum vertex input attribute offset that can be added to the vertex input binding stride.
        MaxInputAttributeOffset : int64

        /// the maximum vertex input binding stride that can be specified in a vertex input binding.
        MaxInputBindingStride : int64

        /// the maximum number of components of output variables which can be output by a vertex shader.
        MaxOutputComponents : int
    }

    member x.Print(l : ILogger) =
        l.line "max in attributes:  %d" x.MaxInputAttributes
        l.line "max in bindings:    %d" x.MaxInputBindings
        l.line "max in offset:      %d" x.MaxInputAttributeOffset
        l.line "max in stride:      %d" x.MaxInputBindingStride
        l.line "max out components: %d" x.MaxOutputComponents

type TessControlLimits =
    {
        /// the maximum number of components of input variables which can be provided as per-vertex inputs to the tessellation control shader stage.
        MaxPerVertexInputComponents : int

        /// the maximum number of components of per-vertex output variables which can be output from the tessellation control shader stage.
        MaxPerVertexOutputComponents : int

        /// the maximum number of components of per-patch output variables which can be output from the tessellation control shader stage.
        MaxPerPatchOutputComponents : int

        /// the maximum total number of components of per-vertex and per-patch output variables which can be output from the tessellation control shader stage.
        MaxTotalOutputComponents : int
    }
    member x.Print(l : ILogger) =
        l.line "max vertex in components:  %d" x.MaxPerVertexInputComponents
        l.line "max vertex out components: %d" x.MaxPerVertexOutputComponents
        l.line "max patch out components:  %d" x.MaxPerPatchOutputComponents
        l.line "max total out components:  %d" x.MaxTotalOutputComponents

type TessEvalLimits =
    {
        /// the maximum number of components of input variables which can be provided as per-vertex inputs to the tessellation evaluation shader stage.
        MaxInputComponents : int

        /// the maximum number of components of per-vertex output variables which can be output from the tessellation evaluation shader stage.
        MaxOutputComponents : int
    }
    member x.Print(l : ILogger) =
        l.line "max in components:  %d" x.MaxInputComponents
        l.line "max out components: %d" x.MaxOutputComponents

type TessellationLimits =
    {
        /// the maximum tessellation generation level supported by the fixed-function tessellation primitive generator.
        MaxGenerationLevel : int

        /// the maximum patch size, in vertices, of patches that can be processed by the tessellation control shader and tessellation primitive generator.
        MaxPatchSize : int

        TessControlLimits : TessControlLimits
        TessEvalLimits : TessEvalLimits
    }

    member x.Print(l : ILogger) =
        l.line "max gen level:  %d" x.MaxGenerationLevel
        l.line "max patch size: %d" x.MaxPatchSize
        l.section "control:" (fun () ->
            x.TessControlLimits.Print(l)
        )
        l.section "evaluation:" (fun () ->
            x.TessEvalLimits.Print(l)
        )

type GeometryLimits =
    {
        /// the maximum invocation count supported for instanced geometry shaders. The value provided in the Invocations execution mode of shader modules must be less than or equal to this limit.
        MaxInvocations : int

        /// the maximum number of components of input variables which can be provided as inputs to the geometry shader stage.
        MaxInputComponents : int

        /// the maximum number of components of output variables which can be output from the geometry shader stage.
        MaxOutputComponents : int

        /// the maximum number of vertices which can be emitted by any geometry shader.
        MaxOutputVertices : int

        /// the maximum total number of components of output, across all emitted vertices, which can be output from the geometry shader stage.
        MaxTotalOutputComponents : int
    }
    member x.Print(l : ILogger) =
        l.line "max invocations:    %d" x.MaxInvocations
        l.line "max out vertices:   %d" x.MaxOutputVertices
        l.line "max in components:  %d" x.MaxInputComponents
        l.section "max out components:" (fun () ->
            l.line "per vertex: %d" x.MaxOutputComponents
            l.line "total:      %d" x.MaxTotalOutputComponents
        )

type FragmentLimits =
    {
        /// the maximum number of components of input variables which can be provided as inputs to the fragment shader stage.
        MaxInputComponents : int

        /// the maximum number of output attachments which can be written to by the fragment shader stage.
        MaxOutputAttachments : int

        /// the maximum number of output attachments which can be written to by the fragment shader stage when blending is enabled and one of the dual source blend modes is in use.
        MaxDualSrcAttachments : int

        /// the total number of storage buffers, storage images, and output buffers which can be used in the fragment shader stage.
        MaxCombinedOutputResources : int

        /// the maximum number of array elements of a variable decorated with the SampleMask built-in decoration.
        MaxSampleMaskWords : int
    }
    member x.Print(l : ILogger) =
        l.line "max in components:     %d" x.MaxInputComponents
        l.line "max out attachments:   %d" x.MaxOutputAttachments
        l.line "max src1 attachments:  %d" x.MaxDualSrcAttachments
        l.line "max out resources:     %d" x.MaxCombinedOutputResources
        l.line "max sample masks:      %d" x.MaxSampleMaskWords

type ComputeLimits =
    {
        /// the maximum total storage size, in bytes, of all variables declared with the WorkgroupLocal storage class in shader modules (or with the shared storage qualifier in GLSL) in the compute shader stage.
        MaxSharedMemorySize : Mem

        /// the maximum number of local workgroups that can be dispatched by a single dispatch command.
        MaxWorkGroupCount : V3i

        /// the maximum total number of compute shader invocations in a single local workgroup. The product of the X, Y, and Z sizes as specified by the LocalSize execution mode in shader modules and by the object decorated by the WorkgroupSize decoration must be less than or equal to this limit.
        MaxWorkGroupInvocations : int

        /// the maximum size of a local compute workgroup, per dimension. These three values represent the maximum local workgroup size in the X, Y, and Z dimensions, respectively.
        MaxWorkGroupSize : V3i
    }
    member x.Print(l : ILogger) =
        l.line "shared memory:         %A" x.MaxSharedMemorySize
        l.line "max group counts:      %A" x.MaxWorkGroupCount
        l.line "max group invocations: %d" x.MaxWorkGroupInvocations
        l.line "max group size:        %A" x.MaxWorkGroupSize

type ShaderLimits =
    {
        /// the minimum offset value for the ConstOffset image operand of any of the OpImageSample* or OpImageFetch* image instructions.
        MinTexelOffset : int

        /// the maximum offset value for the ConstOffset image operand of any of the OpImageSample* or OpImageFetch* image instructions.
        MaxTexelOffset : int

        /// the minimum offset value for the Offset or ConstOffsets image operands of any of the OpImage*Gather image instructions.
        MinTexelGatherOffset : int

        /// the maximum offset value for the Offset or ConstOffsets image operands of any of the OpImage*Gather image instructions.
        MaxTexelGatherOffset : int

        /// the minimum negative offset value for the offset operand of the InterpolateAtOffset extended instruction.
        MinInterpolationOffset : float

        /// the maximum positive offset value for the offset operand of the InterpolateAtOffset extended instruction.
        MaxInterpolationOffset : float

        /// the number of subpixel fractional bits that the x and y offsets to the InterpolateAtOffset extended instruction may be rounded to as fixed-point values.
        SubPixelInterpolationOffsetBits : int

        /// the maximum number of clip distances that can be used in a single shader stage.
        MaxClipDistances : int

        /// the maximum number of cull distances that can be used in a single shader stage.
        MaxCullDistances : int

        /// the maximum combined number of clip and cull distances that can be used in a single shader stage.
        MaxCombinedClipAndCullDistances : int

    }
    member x.Print(l : ILogger) =
        l.line "max clip distances:      %d" x.MaxClipDistances
        l.line "max cull distances:      %d" x.MaxCullDistances
        l.line "max clip/cull distances: %d" x.MaxCombinedClipAndCullDistances
        l.line "subpixel offset bits:    %d" x.SubPixelInterpolationOffsetBits
        l.line "texel offset:            [%d .. %d]" x.MinTexelOffset x.MaxTexelOffset
        l.line "texel grather offset:    [%d .. %d]" x.MinTexelGatherOffset x.MaxTexelGatherOffset
        l.line "interpolation offset:    [%f .. %f]" x.MinInterpolationOffset x.MaxInterpolationOffset

type PrecisionLimits =
    {
        /// the number of bits of subpixel precision in framebuffer coordinates xf and yf.
        SubPixelPrecisionBits : int

        /// the number of bits of precision in the division along an axis of an image used for minification and magnification filters.
        SubTexelPrecisionBits : int

        /// the number of bits of division that the LOD calculation for mipmap fetching get snapped to when determining the contribution from each mip level to the mip filtered results.
        MipMapPrecisionBits : int

        /// indicates support for timestamps on all graphics and compute queues. If this limit is set to true, all queues that advertise the VK_QUEUE_GRAPHICS_BIT or VK_QUEUE_COMPUTE_BIT in the VkQueueFamilyProperties::queueFlags support VkQueueFamilyProperties::timestampValidBits of at least 36.
        TimestampComputeAndGraphics : bool

        /// the number of nanoseconds required for a timestamp query to be incremented by 1.
        TimestampPeriod : float

        /// the number of discrete priorities that can be assigned to a queue based on the value of each member of VkDeviceQueueCreateInfo::pQueuePriorities. This must be at least 2, and levels must be spread evenly over the range, with at least one level at 1.0, and another at 0.0.
        DiscreteQueuePriorities : int
    }

    member x.Print(l : ILogger) =
        l.line "subpixel bits:    %d" x.SubPixelPrecisionBits
        l.line "subtexel bits:    %d" x.SubTexelPrecisionBits
        l.line "mipmap bits:      %d" x.MipMapPrecisionBits
        l.line "timestamps:       %A" x.TimestampComputeAndGraphics
        l.line "timestamp period: %.3fns" x.TimestampPeriod
        l.line "queue priorities: %d" x.DiscreteQueuePriorities

type DrawLimits =
    {
        /// the maximum index value that can be used for indexed draw calls when using 32-bit indices.
        MaxIndexValue       : uint32

        /// the maximum draw count that is supported for indirect draw calls.
        MaxIndirectCount    : uint32
    }

    member x.Print(l : ILogger) =
        l.line "max index value:    %d" x.MaxIndexValue
        l.line "max indirect count: %d" x.MaxIndirectCount

type FramebufferLimits =
    {
        /// the maximum number of active viewports.
        MaxViewports : int

        /// the maximum viewport dimensions in the X (width) and Y (height) dimensions, respectively.
        MaxViewportSize : V2i

        /// the maximum viewport dimensions in the X (width) and Y (height) dimensions, respectively.
        ViewportBounds : Box2d

        /// the number of bits of subpixel precision for viewport bounds. The subpixel precision that floating-point viewport bounds are interpreted at is given by this limit.
        ViewportSubPixelBits : int


        /// the maximum size for a framebuffer.
        MaxSize : V2i

        /// the maximum layer count for a layered framebuffer.
        MaxLayers : int

        /// the maximum number of color attachments that can be used by a subpass in a render pass.
        MaxColorAttachments : int

        /// the color sample counts that are supported for all framebuffer color attachments.
        ColorSampleCounts : Set<int>

        /// the supported depth sample counts for all framebuffer depth/stencil attachments, when the format includes a depth component.
        DepthSampleCounts : Set<int>

        /// he supported stencil sample counts for all framebuffer depth/stencil attachments, when the format includes a stencil component.
        StencilSampleCounts : Set<int>

        /// the supported sample counts for a subpass which uses no attachments.
        NoAttachmentsSampleCounts : Set<int>


    }

    member x.Print(l : ILogger) =
        l.line "max size:   %A" x.MaxSize
        l.line "max layers: %d" x.MaxLayers
        l.line "max colors: %d" x.MaxColorAttachments
        l.section "samples: " (fun () ->
            l.line "color:   %A" x.ColorSampleCounts
            l.line "depth:   %A" x.DepthSampleCounts
            l.line "stencil: %A" x.StencilSampleCounts
            l.line "empty:   %A" x.NoAttachmentsSampleCounts
        )
        l.section "viewports:" (fun () ->
            l.line "max count:      %d" x.MaxViewports
            l.line "max size:       %A" x.MaxViewportSize
            l.line "bounds:         %A" x.ViewportBounds
            l.line "subpixel bits:  %d" x.ViewportSubPixelBits
        )

type RasterizerLimits =
    {
        /// the range of supported sizes for points.
        PointSizeRange : Range1d

        /// the range of supported widths for lines.
        LineWidthRange : Range1d

        /// the granularity of supported point sizes. Not all point sizes in the range defined by pointSizeRange are supported. This limit specifies the granularity (or increment) between successive supported point sizes.
        PointSizeGranularity : float

        /// the granularity of supported line widths. Not all line widths in the range defined by lineWidthRange are supported. This limit specifies the granularity (or increment) between successive supported line widths.
        LineWidthGranularity : float

        /// indicates whether lines are rasterized according to the preferred method of rasterization. If set to false, lines may be rasterized under a relaxed set of rules. If set to true, lines are rasterized as per the strict definition.
        StrictLines : bool

        /// indicates whether rasterization uses the standard sample locations. If set to true, the implementation uses the documented sample locations. If set to false, the implementation may use different sample locations.
        StandardSampleLocations : bool
    }

    member x.Print(l : ILogger) =
        l.line "point size:       [%f .. %f .. %f]" x.PointSizeRange.Min x.PointSizeGranularity x.PointSizeRange.Max
        l.line "line width:       [%f .. %f .. %f]" x.LineWidthRange.Min x.LineWidthGranularity x.LineWidthRange.Max
        l.line "strict lines:     %A" x.StrictLines
        l.line "standard samples: %A" x.StandardSampleLocations

type RaytracingLimits =
    {
        /// Maximum number of geometries in the bottom level acceleration structure.
        MaxGeometryCount                                            : uint64

        /// Maximum number of instances in the top level acceleration structure.
        MaxInstanceCount                                            : uint64

        /// Maximum number of triangles or AABBs in all geometries in the bottom level acceleration structure.
        MaxPrimitiveCount                                           : uint64

        /// Maximum number of acceleration structure bindings that can be accessible to a single shader stage in a pipeline layout.
        /// Descriptor bindings with a descriptor type of VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR count against this limit.
        /// Only descriptor bindings in descriptor set layouts created without the VK_DESCRIPTOR_SET_LAYOUT_CREATE_UPDATE_AFTER_BIND_POOL_BIT bit set count against this limit.
        MaxPerStageDescriptorAccelerationStructures                 : uint32

        /// Similar to maxPerStageDescriptorAccelerationStructures but counts descriptor bindings from
        /// descriptor sets created with or without the VK_DESCRIPTOR_SET_LAYOUT_CREATE_UPDATE_AFTER_BIND_POOL_BIT bit set.
        MaxPerStageDescriptorUpdateAfterBindAccelerationStructures  : uint32

        /// Maximum number of acceleration structure descriptors that can be included in descriptor bindings in a pipeline layout across
        /// all pipeline shader stages and descriptor set numbers. Descriptor bindings with a descriptor type of
        /// VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR count against this limit. Only descriptor bindings in descriptor set layouts
        /// created without the VK_DESCRIPTOR_SET_LAYOUT_CREATE_UPDATE_AFTER_BIND_POOL_BIT bit set count against this limit.
        MaxDescriptorSetAccelerationStructures                      : uint32

        /// Similar to maxDescriptorSetAccelerationStructures but counts descriptor bindings from
        /// descriptor sets created with or without the VK_DESCRIPTOR_SET_LAYOUT_CREATE_UPDATE_AFTER_BIND_POOL_BIT bit set.
        MaxDescriptorSetUpdateAfterBindAccelerationStructures       : uint32

        /// Minimum required alignment, in bytes, for scratch data passed in to an acceleration structure build command.
        MinAccelerationStructureScratchOffsetAlignment              : uint32

        /// Size in bytes of the shader header.
        ShaderGroupHandleSize                                       : uint32

        /// Maximum number of levels of ray recursion allowed in a trace command.
        MaxRayRecursionDepth                                        : uint32

        /// Maximum stride in bytes allowed between shader groups in the shader binding table.
        MaxShaderGroupStride                                        : uint32

        /// Required alignment in bytes for the base of the shader binding table.
        ShaderGroupBaseAlignment                                    : uint32

        /// Number of bytes for the information required to do capture and replay for shader group handles.
        ShaderGroupHandleCaptureReplaySize                          : uint32

        /// Maximum number of ray generation shader invocations which may be produced by a single vkCmdTraceRaysIndirectKHR or vkCmdTraceRaysKHR command.
        MaxRayDispatchInvocationCount                               : uint32

        /// Required alignment in bytes for each shader binding table entry.
        ShaderGroupHandleAlignment                                  : uint32

        /// Maximum size in bytes for a ray attribute structure.
        MaxRayHitAttributeSize                                      : uint32
    }

    member x.Print(l : ILogger) =
        l.line "max ray recursion depth:      %d" x.MaxRayRecursionDepth
        l.line "max ray dispatch invocations: %d" x.MaxRayDispatchInvocationCount
        l.line "max ray hit attribute size:   %d" x.MaxRayHitAttributeSize
        l.section "acceleration structures: " (fun () ->
            l.line "max geometry count:                            %d" x.MaxGeometryCount
            l.line "max instance count:                            %d" x.MaxInstanceCount
            l.line "max primitive count:                           %d" x.MaxPrimitiveCount
            l.line "max descriptors per stage:                     %d" x.MaxPerStageDescriptorAccelerationStructures
            l.line "max descriptors per stage (update after bind): %d" x.MaxPerStageDescriptorUpdateAfterBindAccelerationStructures
            l.line "max descriptors:                               %d" x.MaxDescriptorSetAccelerationStructures
            l.line "max descriptors (updater atfer bind):          %d" x.MaxDescriptorSetUpdateAfterBindAccelerationStructures
            l.line "min scratch offset alignment:                  %d" x.MinAccelerationStructureScratchOffsetAlignment
        )
        l.section "shader binding table: " (fun () ->
            l.line "header size:     %d" x.ShaderGroupHandleSize
            l.line "max stride:      %d" x.MaxShaderGroupStride
            l.line "base alignment:  %d" x.ShaderGroupBaseAlignment
            l.line "entry alignment: %d" x.ShaderGroupHandleAlignment
            l.line "replay size:     %d" x.ShaderGroupHandleCaptureReplaySize
        )

type DeviceLimits =
    {
        Image           : ImageLimits
        SampledImage    : SampledImageLimits
        Sampler         : SamplerLimits
        Uniform         : UniformLimits
        Memory          : MemoryLimits
        Descriptor      : DescriptorLimits

        Vertex          : VertexLimits
        Tessellation    : TessellationLimits
        Geometry        : GeometryLimits
        Fragment        : FragmentLimits
        Compute         : ComputeLimits
        Shader          : ShaderLimits

        Precision       : PrecisionLimits
        Draw            : DrawLimits
        Framebuffer     : FramebufferLimits
        Rasterizer      : RasterizerLimits
        Raytracing      : RaytracingLimits option
    }

    member x.Print(l : ILogger) =
        l.section "image:" (fun () -> x.Image.Print(l))
        l.section "sampled image:" (fun () -> x.SampledImage.Print(l))
        l.section "sampler:" (fun () -> x.Sampler.Print(l))
        l.section "uniform:" (fun () -> x.Uniform.Print(l))
        l.section "memory:" (fun () -> x.Memory.Print(l))
        l.section "descriptors:" (fun () -> x.Descriptor.Print(l))
        l.section "vertex shader:" (fun () -> x.Vertex.Print(l))
        l.section "tessellation shader:" (fun () -> x.Tessellation.Print(l))
        l.section "geometry shader:" (fun () -> x.Geometry.Print(l))
        l.section "fragment shader:" (fun () -> x.Fragment.Print(l))
        l.section "shader sampling:" (fun () -> x.Shader.Print(l))
        l.section "compute shader:" (fun () -> x.Compute.Print(l))
        l.section "precision:" (fun () -> x.Precision.Print(l))
        l.section "draw:" (fun () -> x.Draw.Print(l))
        l.section "framebuffer:" (fun () -> x.Framebuffer.Print(l))
        l.section "rasterizer:" (fun () -> x.Rasterizer.Print(l))
        x.Raytracing |> Option.iter (fun rt -> l.section "raytracing:" (fun () ->  rt.Print(l)))


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DeviceLimits =
    open KHRRayTracingPipeline
    open KHRAccelerationStructure

    [<AutoOpen>]
    module private Helpers =
        let ofVkSampleCountFlags (flags : VkSampleCountFlags) =
            Set.ofList [
                if flags.HasFlag VkSampleCountFlags.D1Bit then yield 1
                if flags.HasFlag VkSampleCountFlags.D2Bit then yield 2
                if flags.HasFlag VkSampleCountFlags.D4Bit then yield 4
                if flags.HasFlag VkSampleCountFlags.D8Bit then yield 8
                if flags.HasFlag VkSampleCountFlags.D16Bit then yield 16
                if flags.HasFlag VkSampleCountFlags.D32Bit then yield 32
                if flags.HasFlag VkSampleCountFlags.D64Bit then yield 64
            ]

    let create (maxAllocationSize : Mem)
               (raytracingProperties : Option<VkPhysicalDeviceRayTracingPipelinePropertiesKHR * VkPhysicalDeviceAccelerationStructurePropertiesKHR>)
               (limits : VkPhysicalDeviceLimits) =
        {
            Image =
                {
                    MaxDimension1D      = int limits.maxImageDimension1D
                    MaxDimension2D      = V2i(int limits.maxImageDimension2D, int limits.maxImageDimension2D)
                    MaxDimension3D      = V3i(int limits.maxImageDimension3D, int limits.maxImageDimension3D, int limits.maxImageDimension3D)
                    MaxDimensionCube    = V2i(int limits.maxImageDimensionCube, int limits.maxImageDimensionCube)
                    MaxArrayLayers      = int limits.maxImageArrayLayers
                }

            SampledImage =
                {
                    ColorSampleCounts       = ofVkSampleCountFlags limits.sampledImageColorSampleCounts
                    IntegerSampleCounts     = ofVkSampleCountFlags limits.sampledImageIntegerSampleCounts
                    DepthSampleCounts       = ofVkSampleCountFlags limits.sampledImageDepthSampleCounts
                    StencilSampleCounts     = ofVkSampleCountFlags limits.sampledImageStencilSampleCounts
                    StorageSampleCounts     = ofVkSampleCountFlags limits.storageImageSampleCounts
                }

            Sampler =
                {
                    MaxAllocationCount  = int limits.maxSamplerAllocationCount
                    MaxLodBias          = float limits.maxSamplerLodBias
                    MaxAnisotropy       = float limits.maxSamplerAnisotropy
                }

            Uniform =
                {
                    MaxBufferViewRange      = Mem limits.maxUniformBufferRange
                    MaxStorageViewRange     = Mem limits.maxStorageBufferRange
                    MaxTexelBufferElements  = int limits.maxTexelBufferElements
                    MaxPushConstantsSize    = Mem limits.maxPushConstantsSize
                }

            Memory =
                {
                    MaxAllocationCount                  = int limits.maxMemoryAllocationCount
                    MaxAllocationSize                   = maxAllocationSize
                    SparseAddressSpaceSize              = Mem (limits.sparseAddressSpaceSize &&& 0x7FFFFFFFFFFFFFFFUL)
                    BufferImageGranularity              = int64 limits.bufferImageGranularity
                    MinMemoryMapAlignment               = int64 limits.minMemoryMapAlignment
                    MinTexelBufferOffsetAlignment       = int64 limits.minTexelBufferOffsetAlignment
                    MinUniformBufferOffsetAlignment     = int64 limits.minUniformBufferOffsetAlignment
                    MinStorageBufferOffsetAlignment     = int64 limits.minStorageBufferOffsetAlignment
                    OptimalBufferCopyOffsetAlignment    = int64 limits.optimalBufferCopyOffsetAlignment
                    OptimalBufferCopyRowPitchAlignment  = int64 limits.optimalBufferCopyRowPitchAlignment
                    NonCoherentAtomSize                 = int64 limits.nonCoherentAtomSize

                }

            Descriptor =
                {
                    MaxBoundDescriptorSets      = int limits.maxBoundDescriptorSets
                    MaxPerStageSamplers         = int limits.maxPerStageDescriptorSamplers
                    MaxPerStageUniformBuffers   = int limits.maxPerStageDescriptorUniformBuffers
                    MaxPerStageStorageBuffers   = int limits.maxPerStageDescriptorStorageBuffers
                    MaxPerStageSampledImages    = int limits.maxPerStageDescriptorSampledImages
                    MaxPerStageStorageImages    = int limits.maxPerStageDescriptorStorageImages
                    MaxPerStageInputAttachments = int limits.maxPerStageDescriptorInputAttachments
                    MaxPerStageResources        = int limits.maxPerStageResources
                    MaxSamplers                 = int limits.maxDescriptorSetSamplers
                    MaxUniformBuffers           = int limits.maxDescriptorSetUniformBuffers
                    MaxUniformBuffersDynamic    = int limits.maxDescriptorSetUniformBuffersDynamic
                    MaxStorageBuffers           = int limits.maxDescriptorSetStorageBuffers
                    MaxStorageBuffersDynamic    = int limits.maxDescriptorSetStorageBuffersDynamic
                    MaxSampledImages            = int limits.maxDescriptorSetSampledImages
                    MaxStorageImages            = int limits.maxDescriptorSetStorageImages
                    MaxInputAttachments         = int limits.maxDescriptorSetInputAttachments

                }

            Vertex =
                {
                    MaxInputAttributes      = int limits.maxVertexInputAttributes
                    MaxInputBindings        = int limits.maxVertexInputBindings
                    MaxInputAttributeOffset = int64 limits.maxVertexInputAttributeOffset
                    MaxInputBindingStride   = int64 limits.maxVertexInputBindingStride
                    MaxOutputComponents     = int limits.maxVertexOutputComponents
                }

            Tessellation =
                {
                    MaxGenerationLevel  = int limits.maxTessellationGenerationLevel
                    MaxPatchSize        = int limits.maxTessellationPatchSize
                    TessControlLimits =
                        {
                            MaxPerVertexInputComponents     = int limits.maxTessellationControlPerVertexInputComponents
                            MaxPerVertexOutputComponents    = int limits.maxTessellationControlPerVertexOutputComponents
                            MaxPerPatchOutputComponents     = int limits.maxTessellationControlPerPatchOutputComponents
                            MaxTotalOutputComponents        = int limits.maxTessellationControlTotalOutputComponents
                        }
                    TessEvalLimits =
                        {
                            MaxInputComponents  = int limits.maxTessellationEvaluationInputComponents
                            MaxOutputComponents = int limits.maxTessellationEvaluationOutputComponents
                        }
                }

            Geometry =
                {
                    MaxInvocations              = int limits.maxGeometryShaderInvocations
                    MaxInputComponents          = int limits.maxGeometryInputComponents
                    MaxOutputComponents         = int limits.maxGeometryOutputComponents
                    MaxOutputVertices           = int limits.maxGeometryOutputVertices
                    MaxTotalOutputComponents    = int limits.maxGeometryTotalOutputComponents
                }

            Fragment =
                {
                    MaxInputComponents          = int limits.maxFragmentInputComponents
                    MaxOutputAttachments        = int limits.maxFragmentOutputAttachments
                    MaxDualSrcAttachments       = int limits.maxFragmentDualSrcAttachments
                    MaxCombinedOutputResources  = int limits.maxFragmentCombinedOutputResources
                    MaxSampleMaskWords          = int limits.maxSampleMaskWords
                }

            Compute =
                {
                    MaxSharedMemorySize     = Mem limits.maxComputeSharedMemorySize
                    MaxWorkGroupCount       = V3i(int limits.maxComputeWorkGroupCount.X, int limits.maxComputeWorkGroupCount.Y, int limits.maxComputeWorkGroupCount.Z)
                    MaxWorkGroupInvocations = int limits.maxComputeWorkGroupInvocations
                    MaxWorkGroupSize        = V3i(int limits.maxComputeWorkGroupSize.X, int limits.maxComputeWorkGroupSize.Y, int limits.maxComputeWorkGroupSize.Z)
                }

            Shader =
                {
                    MinTexelOffset                  = int limits.minTexelOffset
                    MaxTexelOffset                  = int limits.maxTexelOffset
                    MinTexelGatherOffset            = int limits.minTexelGatherOffset
                    MaxTexelGatherOffset            = int limits.maxTexelGatherOffset
                    MinInterpolationOffset          = float limits.minInterpolationOffset
                    MaxInterpolationOffset          = float limits.maxInterpolationOffset
                    SubPixelInterpolationOffsetBits = int limits.subPixelInterpolationOffsetBits
                    MaxClipDistances                = int limits.maxClipDistances
                    MaxCullDistances                = int limits.maxCullDistances
                    MaxCombinedClipAndCullDistances = int limits.maxCombinedClipAndCullDistances

                }

            Precision =
                {
                    SubPixelPrecisionBits       = int limits.subPixelPrecisionBits
                    SubTexelPrecisionBits       = int limits.subTexelPrecisionBits
                    MipMapPrecisionBits         = int limits.mipmapPrecisionBits
                    TimestampComputeAndGraphics = limits.timestampComputeAndGraphics <> 0u
                    TimestampPeriod             = float limits.timestampPeriod
                    DiscreteQueuePriorities     = int limits.discreteQueuePriorities
                }

            Draw =
                {
                    MaxIndexValue       = limits.maxDrawIndexedIndexValue
                    MaxIndirectCount    = limits.maxDrawIndirectCount
                }

            Framebuffer =
                {
                    MaxViewports                = int limits.maxViewports
                    MaxViewportSize             = V2i(int limits.maxViewportDimensions.X, int limits.maxViewportDimensions.Y)
                    ViewportBounds              = Box2d(float limits.viewportBoundsRange.X, float limits.viewportBoundsRange.X, float limits.viewportBoundsRange.Y, float limits.viewportBoundsRange.Y)
                    ViewportSubPixelBits        = int limits.viewportSubPixelBits
                    MaxSize                     = V2i(int limits.maxFramebufferWidth, int limits.maxFramebufferHeight)
                    MaxLayers                   = int limits.maxFramebufferLayers
                    MaxColorAttachments         = int limits.maxColorAttachments
                    ColorSampleCounts           = ofVkSampleCountFlags limits.framebufferColorSampleCounts
                    DepthSampleCounts           = ofVkSampleCountFlags limits.framebufferDepthSampleCounts
                    StencilSampleCounts         = ofVkSampleCountFlags limits.framebufferStencilSampleCounts
                    NoAttachmentsSampleCounts   = ofVkSampleCountFlags limits.framebufferNoAttachmentsSampleCounts
                }

            Rasterizer =
                {
                    PointSizeRange          = Range1d(float limits.pointSizeRange.X, float limits.pointSizeRange.Y)
                    LineWidthRange          = Range1d(float limits.lineWidthRange.X, float limits.lineWidthRange.Y)
                    PointSizeGranularity    = float limits.pointSizeGranularity
                    LineWidthGranularity    = float limits.lineWidthGranularity
                    StrictLines             = limits.strictLines <> 0u
                    StandardSampleLocations = limits.standardSampleLocations <> 0u
                }

            Raytracing =
                raytracingProperties |> Option.map (fun (pipeline, accel) ->
                    {
                        MaxGeometryCount                                            = accel.maxGeometryCount
                        MaxInstanceCount                                            = accel.maxInstanceCount
                        MaxPrimitiveCount                                           = accel.maxPrimitiveCount
                        MaxPerStageDescriptorAccelerationStructures                 = accel.maxPerStageDescriptorAccelerationStructures
                        MaxPerStageDescriptorUpdateAfterBindAccelerationStructures  = accel.maxPerStageDescriptorUpdateAfterBindAccelerationStructures
                        MaxDescriptorSetAccelerationStructures                      = accel.maxDescriptorSetAccelerationStructures
                        MaxDescriptorSetUpdateAfterBindAccelerationStructures       = accel.maxDescriptorSetUpdateAfterBindAccelerationStructures
                        MinAccelerationStructureScratchOffsetAlignment              = accel.minAccelerationStructureScratchOffsetAlignment
                        ShaderGroupHandleSize                                       = pipeline.shaderGroupHandleSize
                        MaxRayRecursionDepth                                        = pipeline.maxRayRecursionDepth
                        MaxShaderGroupStride                                        = pipeline.maxShaderGroupStride
                        ShaderGroupBaseAlignment                                    = pipeline.shaderGroupBaseAlignment
                        ShaderGroupHandleCaptureReplaySize                          = pipeline.shaderGroupHandleCaptureReplaySize
                        MaxRayDispatchInvocationCount                               = pipeline.maxRayDispatchInvocationCount
                        ShaderGroupHandleAlignment                                  = pipeline.shaderGroupHandleAlignment
                        MaxRayHitAttributeSize                                      = pipeline.maxRayHitAttributeSize
                    }
                )
        }