namespace Aardvark.Rendering.Vulkan

open EXTMemoryPriority

type MemoryFeatures =
    {
        /// Specifies that accesses to buffers are bounds-checked against the range of the buffer descriptor
        /// (as determined by VkDescriptorBufferInfo::range, VkBufferViewCreateInfo::range, or the size of the buffer).
        RobustBufferAccess: bool

        /// Specifies whether resource memory can be managed at opaque sparse block level instead of at the object level.
        SparseBinding: bool

        /// Specifies whether the device can access partially resident buffers.
        SparseResidencyBuffer: bool

        /// Specifies whether the device can access partially resident 2D images with 1 sample per pixel.
        SparseResidencyImage2D: bool

        /// Specifies whether the device can access partially resident 3D images.
        SparseResidencyImage3D: bool

        /// Specifies whether the physical device can access partially resident 2D images with 2 samples per pixel.
        SparseResidency2Samples: bool

        /// Specifies whether the physical device can access partially resident 2D images with 4 samples per pixel.
        SparseResidency4Samples: bool

        /// Specifies whether the physical device can access partially resident 2D images with 8 samples per pixel.
        SparseResidency8Samples: bool

        /// Specifies whether the physical device can access partially resident 2D images with 16 samples per pixel.
        SparseResidency16Samples: bool

        /// Specifies whether the physical device can correctly access data aliased into multiple locations.
        SparseResidencyAliased: bool

        /// Specifies whether protected memory is supported.
        ProtectedMemory: bool

        /// Specifies whether the implementation supports memory priorities specified at memory allocation time via VkMemoryPriorityAllocateInfoEXT.
        MemoryPriority: bool

        /// Specifies whether the implementation supports accessing buffer memory in shaders as storage buffers via an address queried from vkGetBufferDeviceAddress.
        BufferDeviceAddress : bool

        /// Specifies whether the implementation supports saving and reusing buffer and device addresses, e.g. for trace capture and replay.
        BufferDeviceAddressCaptureReplay: bool

        /// Specifies whether the implementation supports the bufferDeviceAddress, rayTracingPipeline and rayQuery features
        /// for logical devices created with multiple physical devices. If this feature is not supported, buffer and
        /// acceleration structure addresses must not be queried on a logical device created with more than one physical device.
        BufferDeviceAddressMultiDevice: bool
    }

    member internal x.Print(l : ILogger) =
        l.line "robust buffer access:   %A" x.RobustBufferAccess
        l.line "sparse binding:         %A" x.SparseBinding
        l.line "sparse buffers:         %A" x.SparseResidencyBuffer
        l.line "sparse 2D images:       %A" x.SparseResidencyImage2D
        l.line "sparse 2D images (2x):  %A" x.SparseResidency2Samples
        l.line "sparse 2D images (4x):  %A" x.SparseResidency4Samples
        l.line "sparse 2D images (8x):  %A" x.SparseResidency8Samples
        l.line "sparse 2D images (16x): %A" x.SparseResidency16Samples
        l.line "sparse 3D images:       %A" x.SparseResidencyImage3D
        l.line "sparse aliased data:    %A" x.SparseResidencyAliased
        l.line "protected memory:       %A" x.ProtectedMemory
        l.line "memory priority:        %A" x.MemoryPriority
        l.section "buffer device address: " (fun () ->
            l.line "supported:          %A" x.BufferDeviceAddress
            l.line "capture & replay:   %A" x.BufferDeviceAddressCaptureReplay
            l.line "multidevice:        %A" x.BufferDeviceAddressMultiDevice
        )

type DescriptorFeatures =
    {
        /// Indicates whether the implementation supports updating uniform buffer descriptors after a set is bound.
        BindingUniformBufferUpdateAfterBind: bool

        /// Indicates whether the implementation supports updating sampled image descriptors after a set is bound.
        BindingSampledImageUpdateAfterBind: bool

        /// Indicates whether the implementation supports updating storage image descriptors after a set is bound.
        BindingStorageImageUpdateAfterBind: bool

        /// Indicates whether the implementation supports updating storage buffer descriptors after a set is bound.
        BindingStorageBufferUpdateAfterBind: bool

        /// Indicates whether the implementation supports updating uniform texel buffer descriptors after a set is bound.
        BindingUniformTexelBufferUpdateAfterBind: bool

        /// Indicates whether the implementation supports updating storage texel buffer descriptors after a set is bound.
        BindingStorageTexelBufferUpdateAfterBind: bool

        /// Indicates whether the implementation supports updating acceleration structure descriptors after a set is bound.
        BindingAccelerationStructureUpdateAfterBind : bool

        /// Indicates whether the implementation supports updating descriptors while the set is in use.
        BindingUpdateUnusedWhilePending: bool

        /// Indicates whether the implementation supports statically using a descriptor set binding in which some descriptors are not valid.
        BindingPartiallyBound: bool

        /// Indicates whether the implementation supports descriptor sets with a variable-sized last binding.
        BindingVariableDescriptorCount: bool

        /// Indicates whether the implementation supports the SPIR-V RuntimeDescriptorArray capability.
        RuntimeDescriptorArray: bool
    }

    member internal x.Print(l : ILogger) =
        l.section "update after bind: " (fun () ->
            l.line "uniform buffers:         %A" x.BindingUniformBufferUpdateAfterBind
            l.line "sampled images:          %A" x.BindingSampledImageUpdateAfterBind
            l.line "storage images:          %A" x.BindingStorageImageUpdateAfterBind
            l.line "storage buffers:         %A" x.BindingStorageBufferUpdateAfterBind
            l.line "uniform texel buffers:   %A" x.BindingUniformTexelBufferUpdateAfterBind
            l.line "storage texel buffers:   %A" x.BindingStorageTexelBufferUpdateAfterBind
            l.line "acceleration structures: %A" x.BindingAccelerationStructureUpdateAfterBind
        )
        l.line "update unused while pending: %A" x.BindingUpdateUnusedWhilePending
        l.line "partially bound:             %A" x.BindingPartiallyBound
        l.line "variable count:              %A" x.BindingVariableDescriptorCount
        l.line "runtime array:               %A" x.RuntimeDescriptorArray

type ImageFeatures =
    {

        /// Specifies whether image views with a VkImageViewType of VK_IMAGE_VIEW_TYPE_CUBE_ARRAY can be created, and that the
        /// corresponding SampledCubeArray and ImageCubeArray SPIR-V capabilities can be used in shader code.
        ImageCubeArray: bool

        /// Specifies whether all of the ETC2 and EAC compressed texture formats are supported.
        CompressionETC2: bool

        /// Specifies whether all of the ASTC LDR compressed texture formats are supported.
        CompressionASTC_LDR: bool

        /// Specifies whether all of the BC compressed texture formats are supported.
        CompressionBC: bool
    }

    member internal x.Print(l : ILogger) =
        l.line "cube arrays:              %A" x.ImageCubeArray
        l.line "ETC2 compression:         %A" x.CompressionETC2
        l.line "ASTC LDR compression:     %A" x.CompressionASTC_LDR
        l.line "BC compression:           %A" x.CompressionBC

type SamplerFeatures =
    {
        /// Specifies whether anisotropic filtering is supported.
        Anisotropy: bool

        /// Specifies whether the implementation supports sampler Y′CBCR conversion.
        YcbcrConversion: bool
    }

    member internal x.Print(l : ILogger) =
        l.line "anisotropy:           %A" x.Anisotropy
        l.line "ycbcr:                %A" x.YcbcrConversion

type ShaderFeatures =
    {
        /// Specifies whether geometry shaders are supported.
        GeometryShader: bool

        /// Specifies whether tessellation control and evaluation shaders are supported.
        TessellationShader: bool

        /// Specifies whether storage buffers and images support stores and atomic operations in the vertex, tessellation, and geometry shader stages.
        VertexPipelineStoresAndAtomics: bool

        /// Specifies whether storage buffers and images support stores and atomic operations in the fragment shader stage.
        FragmentStoresAndAtomics: bool

        /// Specifies whether the PointSize built-in decoration is available in the tessellation control, tessellation evaluation, and geometry shader stages.
        TessellationAndGeometryPointSize: bool

        /// Specifies whether the extended set of image gather instructions are available in shader code.
        ImageGatherExtended: bool

        /// Specifies whether all the storage image extended formats are supported.
        StorageImageExtendedFormats: bool

        /// Specifies whether multisampled storage images are supported.
        StorageImageMultisample: bool

        /// Specifies whether storage images require a format qualifier to be specified when reading from storage images.
        StorageImageReadWithoutFormat: bool

        /// Specifies whether storage images require a format qualifier to be specified when writing to storage images.
        StorageImageWriteWithoutFormat: bool

        /// Specifies whether arrays of uniform buffers can be indexed by dynamically uniform integer expressions in shader code.
        UniformBufferArrayDynamicIndexing: bool

        /// Specifies whether arrays of samplers or sampled images can be indexed by dynamically uniform integer expressions in shader code.
        SampledImageArrayDynamicIndexing: bool

        /// Specifies whether arrays of storage buffers can be indexed by dynamically uniform integer expressions in shader code.
        StorageBufferArrayDynamicIndexing: bool

        /// Specifies whether arrays of storage images can be indexed by dynamically uniform integer expressions in shader code.
        StorageImageArrayDynamicIndexing: bool

        /// Indicates whether arrays of input attachments can be indexed by dynamically uniform integer expressions in shader code.
        InputAttachmentArrayDynamicIndexing: bool

        /// Indicates whether arrays of uniform texel buffers can be indexed by dynamically uniform integer expressions in shader code.
        UniformTexelBufferArrayDynamicIndexing: bool

        /// Indicates whether arrays of storage texel buffers can be indexed by dynamically uniform integer expressions in shader code.
        StorageTexelBufferArrayDynamicIndexing: bool

        /// Specifies whether arrays of uniform buffers can be indexed by dynamically non-uniform integer expressions in shader code.
        UniformBufferArrayNonUniformIndexing: bool

        /// Specifies whether arrays of samplers or sampled images can be indexed by dynamically non-uniform integer expressions in shader code.
        SampledImageArrayNonUniformIndexing: bool

        /// Specifies whether arrays of storage buffers can be indexed by dynamically non-uniform integer expressions in shader code.
        StorageBufferArrayNonUniformIndexing: bool

        /// Specifies whether arrays of storage images can be indexed by dynamically non-uniform integer expressions in shader code.
        StorageImageArrayNonUniformIndexing: bool

        /// Indicates whether arrays of input attachments can be indexed by dynamically non-uniform integer expressions in shader code.
        InputAttachmentArrayNonUniformIndexing: bool

        /// Indicates whether arrays of uniform texel buffers can be indexed by dynamically non-uniform integer expressions in shader code.
        UniformTexelBufferArrayNonUniformIndexing: bool

        /// Indicates whether arrays of storage texel buffers can be indexed by dynamically non-uniform integer expressions in shader code.
        StorageTexelBufferArrayNonUniformIndexing: bool

        /// Specifies whether objects in the StorageBuffer, ShaderRecordBufferKHR, or PhysicalStorageBuffer storage class with the
        /// Block decoration can have 8-bit integer members.
        StorageBuffer8BitAccess: bool

        /// Specifies whether objects in the StorageBuffer, ShaderRecordBufferKHR, or PhysicalStorageBuffer storage class with the
        /// Block decoration can have 16-bit integer and 16-bit floating-point members.
        StorageBuffer16BitAccess: bool

        /// Specifies whether objects in the Uniform storage class with the Block decoration and in the
        /// StorageBuffer, ShaderRecordBufferKHR, or PhysicalStorageBuffer storage class with the same decoration can have 8-bit integer members.
        UniformAndStorageBuffer8BitAccess: bool

        /// Specifies whether objects in the Uniform storage class with the Block decoration and in the
        /// StorageBuffer, ShaderRecordBufferKHR, or PhysicalStorageBuffer storage class with the same decoration can have 16-bit integer and 16-bit floating-point members.
        UniformAndStorageBuffer16BitAccess: bool

        /// Specifies whether objects in the PushConstant storage class can have 8-bit integer members.
        StoragePushConstant8: bool

        /// Specifies whether objects in the PushConstant storage class can have 16-bit integer and 16-bit floating-point members.
        StoragePushConstant16: bool

        /// Specifies whether objects in the Input and Output storage classes can have 16-bit integer and 16-bit floating-point members.
        StorageInputOutput16: bool

        /// Specifies whether clip distances are supported in shader code.
        ClipDistance: bool

        /// Specifies whether cull distances are supported in shader code.
        CullDistance: bool

        /// Specifies whether 16-bit floats (halves) are supported in shader code.
        Float16: bool

        /// Specifies whether 64-bit floats (doubles) are supported in shader code.
        Float64: bool

        /// Specifies whether 8-bit integers (signed and unsigned) are supported in shader code.
        Int8: bool

        /// Specifies whether 16-bit integers (signed and unsigned) are supported in shader code.
        Int16: bool

        /// Specifies whether 64-bit integers (signed and unsigned) are supported in shader code.
        Int64: bool

        /// Specifies whether image operations that return resource residency information are supported in shader code.
        ResourceResidency: bool

        /// Specifies whether image operations specifying the minimum resource LOD are supported in shader code.
        ResourceMinLod: bool

        /// Specifies whether the implementation supports the SPIR-V VariablePointers capability.
        VariablePointers: bool

        /// Specifies whether the implementation supports the SPIR-V VariablePointersStorageBuffer capability.
        VariablePointersStorageBuffer: bool

        /// Specifies whether shader draw parameters are supported.
        DrawParameters: bool
    }

    member internal x.Print(l : ILogger) =
        l.line "geometry:                          %A" x.GeometryShader
        l.line "tesselation:                       %A" x.TessellationShader
        l.line "geometry / tesselation point size: %A" x.TessellationAndGeometryPointSize
        l.line "vertex stores / atomics:           %A" x.VertexPipelineStoresAndAtomics
        l.line "fragment stores / atomics:         %A" x.FragmentStoresAndAtomics
        l.line "image gather:                 %A" x.ImageGatherExtended
        l.section "storage images: " (fun () ->
            l.line "extended formats:           %A" x.StorageImageExtendedFormats
            l.line "multisampled:               %A" x.StorageImageMultisample
            l.line "read without format:        %A" x.StorageImageReadWithoutFormat
            l.line "write without format:       %A" x.StorageImageWriteWithoutFormat
        )
        l.section "dynamic indexing: " (fun () ->
            l.line "uniform bufferss:           %A" x.UniformBufferArrayDynamicIndexing
            l.line "sampled images:             %A" x.SampledImageArrayDynamicIndexing
            l.line "storage buffers:            %A" x.StorageBufferArrayDynamicIndexing
            l.line "storage images:             %A" x.StorageImageArrayDynamicIndexing
            l.line "input attachments:          %A" x.InputAttachmentArrayDynamicIndexing
            l.line "uniform texel buffers:      %A" x.UniformTexelBufferArrayDynamicIndexing
            l.line "storage texel buffers:      %A" x.StorageTexelBufferArrayDynamicIndexing
        )
        l.section "non-uniform indexing: " (fun() ->
            l.line "uniform buffers:            %A" x.UniformBufferArrayNonUniformIndexing
            l.line "sampled images:             %A" x.SampledImageArrayNonUniformIndexing
            l.line "storage buffers:            %A" x.StorageBufferArrayNonUniformIndexing
            l.line "storage images:             %A" x.StorageImageArrayNonUniformIndexing
            l.line "input attachments:          %A" x.InputAttachmentArrayNonUniformIndexing
            l.line "uniform texel buffers:      %A" x.UniformTexelBufferArrayNonUniformIndexing
            l.line "storage texel buffers:      %A" x.StorageTexelBufferArrayNonUniformIndexing
        )
        l.section "8-bit members: " (fun () ->
            l.line "storage buffers:            %A" x.StorageBuffer8BitAccess
            l.line "uniform / storage buffers:  %A" x.UniformAndStorageBuffer8BitAccess
            l.line "push constants:             %A" x.StoragePushConstant8
        )
        l.section "16-bit members: " (fun () ->
            l.line "storage buffers:            %A" x.StorageBuffer16BitAccess
            l.line "uniform / storage buffers:  %A" x.UniformAndStorageBuffer16BitAccess
            l.line "push constants:             %A" x.StoragePushConstant16
            l.line "input / output:             %A" x.StorageInputOutput16
        )
        l.line "clip distance:                %A" x.ClipDistance
        l.line "cull distance:                %A" x.CullDistance
        l.section "special types: " (fun () ->
            l.line "float16:                    %A" x.Float16
            l.line "float64:                    %A" x.Float64
            l.line "int8:                       %A" x.Int8
            l.line "int16:                      %A" x.Int16
            l.line "int64:                      %A" x.Int64
        )
        l.section "resources: " (fun () ->
            l.line "residency:                  %A" x.ResourceResidency
            l.line "min lod:                    %A" x.ResourceMinLod
        )
        l.line "variable pointers:                  %A" x.VariablePointers
        l.line "variable pointers (storage buffer): %A" x.VariablePointersStorageBuffer
        l.line "draw parameters:                    %A" x.DrawParameters

type QueryFeatures =
    {
        /// Specifies whether occlusion queries returning actual sample counts are supported.
        OcclusionQueryPrecise: bool

        /// Specifies whether the pipeline statistics queries are supported.
        PipelineStatistics: bool

        /// Specifies whether a secondary command buffer may be executed while a query is active.
        InheritedQueries: bool
    }

    member internal x.Print(l : ILogger) =
        l.line "precise occlusion:   %A" x.OcclusionQueryPrecise
        l.line "pipeline statistics: %A" x.PipelineStatistics
        l.line "inherited queries:   %A" x.InheritedQueries

type DepthFeatures =
    {
        /// Specifies whether depth clamping is supported.
        Clamp: bool

        /// Specifies whether depth bias clamping is supported.
        BiasClamp: bool

        /// Specifies whether depth bounds tests are supported.
        BoundsTest: bool
    }

    member internal x.Print(l : ILogger) =
        l.line "clamping:      %A" x.Clamp
        l.line "bias clamping: %A" x.BiasClamp
        l.line "bounds test:   %A" x.BoundsTest

type BlendFeatures =
    {
        /// Specifies whether the VkPipelineColorBlendAttachmentState settings are controlled independently per-attachment.
        IndependentBlend: bool

        /// Specifies whether blend operations which take two sources are supported.
        DualSrcBlend: bool

        /// Specifies whether logic operations are supported.
        LogicOp: bool
    }

    member internal x.Print(l : ILogger) =
        l.line "per-attachment:   %A" x.IndependentBlend
        l.line "dual-source:      %A" x.DualSrcBlend
        l.line "logic operations: %A" x.LogicOp

type DrawingFeatures =
    {
        /// Specifies the full 32-bit range of indices is supported for indexed draw calls when using a VkIndexType of VK_INDEX_TYPE_UINT32.
        FullDrawIndexUint32: bool

        /// Specifies whether multiple draw indirect is supported.
        MultiDrawIndirect: bool

        /// Specifies whether indirect draw calls support the firstInstance parameter.
        DrawIndirectFirstInstance: bool
    }

    member internal x.Print(l : ILogger) =
        l.line "full 32-bit indices:          %A" x.FullDrawIndexUint32
        l.line "multi draw indirect:          %A" x.MultiDrawIndirect
        l.line "draw indirect first instance: %A" x.DrawIndirectFirstInstance

type MultiviewFeatures =
    {
        /// Specifies whether more than one viewport is supported.
        MultiViewport: bool
    }

    member internal x.Print(l : ILogger) =
        l.line "multiple viewports:             %A" x.MultiViewport

type RasterizerFeatures =
    {
        /// Specifies whether lines with width other than 1.0 are supported.
        WideLines: bool

        /// Specifies whether points with size greater than 1.0 are supported.
        LargePoints: bool

        /// Specifies whether point and wireframe fill modes are supported.
        FillModeNonSolid: bool

        /// Specifies whether Sample Shading and multisample interpolation are supported.
        SampleRateShading: bool

        /// Specifies whether the implementation is able to replace the alpha value of the color fragment output from
        /// the fragment shader with the maximum representable alpha value for fixed-point colors or 1.0 for floating-point colors.
        AlphaToOne: bool

        /// Specifies whether all pipelines that will be bound to a command buffer during a subpass which uses no attachments must
        /// have the same value for VkPipelineMultisampleStateCreateInfo.rasterizationSamples.
        VariableMultisampleRate: bool
    }

    member internal x.Print(l : ILogger) =
        l.line "wide lines:                %A" x.WideLines
        l.line "large points:              %A" x.LargePoints
        l.line "non-solid fill mode:       %A" x.FillModeNonSolid
        l.line "sample rate shading:       %A" x.SampleRateShading
        l.line "alpha to one:              %A" x.AlphaToOne
        l.line "variable multisample rate: %A" x.VariableMultisampleRate

type GraphicsPipelineFeatures =
    {
        Depth: DepthFeatures
        Blending: BlendFeatures
        Drawing: DrawingFeatures
        Multiview: MultiviewFeatures
        Rasterizer: RasterizerFeatures
    }

    member internal x.Print(l : ILogger) =
        l.section "depth:" (fun () -> x.Depth.Print(l))
        l.section "blending:" (fun () -> x.Blending.Print(l))
        l.section "drawing:" (fun () -> x.Drawing.Print(l))
        l.section "multiview: " (fun () -> x.Multiview.Print(l))
        l.section "rasterizer:" (fun () -> x.Rasterizer.Print(l))

type RaytracingFeatures =
    {
        /// Indicates whether the implementation supports the ray tracing pipeline functionality.
        Pipeline : bool

        /// Indicates whether the implementation supports fetching the object space vertex positions of a hit triangle.
        PositionFetch : bool

        /// Indicates whether the implementation supports ray query (OpRayQueryProceedKHR) functionality.
        RayQuery : bool

        /// Indicates whether the implementation supports saving and reusing shader group handles, e.g. for trace capture and replay.
        ShaderGroupHandleCaptureReplay : bool

        /// Indicates whether the implementation supports reuse of shader group handles being arbitrarily mixed with creation of non-reused shader group handles.
        ShaderGroupHandleCaptureReplayMixed : bool

        /// Indicates whether the implementation supports indirect trace ray commands, e.g. vkCmdTraceRaysIndirectKHR.
        TraceRaysIndirect : bool

        /// Indicates whether the implementation supports primitive culling during ray traversal.
        RayTraversalPrimitiveCulling : bool

        /// Indicates whether the implementation supports the acceleration structure functionality.
        AccelerationStructure : bool

        /// Indicates whether the implementation supports saving and reusing acceleration structure device addresses, e.g. for trace capture and replay.
        AccelerationStructureCaptureReplay : bool

        /// Indicates whether the implementation supports indirect acceleration structure build commands, e.g. vkCmdBuildAccelerationStructuresIndirectKHR.
        AccelerationStructureIndirectBuild : bool

        /// Indicates whether the implementation supports host side acceleration structure commands.
        AccelerationStructureHostCommands : bool
    }

    member internal x.Print(l : ILogger) =
        l.line "pipeline:                           %A" x.Pipeline
        l.line "position fetch:                     %A" x.PositionFetch
        l.line "ray queries:                        %A" x.RayQuery
        l.section "shader group handles: " (fun () ->
            l.line "capture & replay:                 %A" x.ShaderGroupHandleCaptureReplay
            l.line "capture & replay (mixed):         %A" x.ShaderGroupHandleCaptureReplayMixed
        )
        l.line "trace rays indirect:                %A" x.TraceRaysIndirect
        l.line "ray traversal primitive culling:    %A" x.RayTraversalPrimitiveCulling
        l.section "acceleration structures: " (fun () ->
            l.line "supported:                    %A" x.AccelerationStructure
            l.line "capture & replay:             %A" x.AccelerationStructureCaptureReplay
            l.line "indirect build:               %A" x.AccelerationStructureIndirectBuild
            l.line "host commands:                %A" x.AccelerationStructureHostCommands
        )

type DeviceFeatures =
    {
        Memory: MemoryFeatures
        Descriptors: DescriptorFeatures
        Images: ImageFeatures
        Samplers: SamplerFeatures

        Shaders: ShaderFeatures

        Queries: QueryFeatures

        GraphicsPipeline : GraphicsPipelineFeatures

        Raytracing : RaytracingFeatures
    }

    member internal x.Print(l : ILogger) =
        l.section "memory:" (fun () -> x.Memory.Print(l))
        l.section "descriptors:" (fun () -> x.Descriptors.Print(l))
        l.section "images:" (fun () -> x.Images.Print(l))
        l.section "samplers: " (fun () -> x.Samplers.Print(l))
        l.section "shaders:" (fun () -> x.Shaders.Print(l))
        l.section "queries:" (fun () -> x.Queries.Print(l))
        l.section "graphics pipeline:" (fun () -> x.GraphicsPipeline.Print(l))
        l.section "raytracing:" (fun () -> x.Raytracing.Print(l))

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DeviceFeatures =
    open KHRRayTracingPipeline
    open KHRRayTracingPositionFetch
    open KHRRayQuery
    open KHRAccelerationStructure
    open KHRBufferDeviceAddress
    open KHRShaderFloat16Int8
    open KHR8bitStorage
    open EXTDescriptorIndexing
    open Vulkan11

    let private toBool (value : VkBool32) =
        value <> 0u

    let private toVkBool (value : bool) =
        if value then 1u else 0u

    let internal toNativeChain (features : DeviceFeatures) =
        let mem =
            VkPhysicalDeviceProtectedMemoryFeatures(
                toVkBool features.Memory.ProtectedMemory
            )

        let memp =
            VkPhysicalDeviceMemoryPriorityFeaturesEXT(
                toVkBool features.Memory.MemoryPriority
            )

        let ycbcr =
            VkPhysicalDeviceSamplerYcbcrConversionFeatures(
                toVkBool features.Samplers.YcbcrConversion
            )

        let s8 =
            VkPhysicalDevice8BitStorageFeaturesKHR(
                toVkBool features.Shaders.StorageBuffer8BitAccess,
                toVkBool features.Shaders.UniformAndStorageBuffer8BitAccess,
                toVkBool features.Shaders.StoragePushConstant8
            )

        let s16 =
            VkPhysicalDevice16BitStorageFeatures(
                toVkBool features.Shaders.StorageBuffer16BitAccess,
                toVkBool features.Shaders.UniformAndStorageBuffer16BitAccess,
                toVkBool features.Shaders.StoragePushConstant16,
                toVkBool features.Shaders.StorageInputOutput16
            )

        let f16i8 =
            VkPhysicalDeviceFloat16Int8FeaturesKHR(
                toVkBool features.Shaders.Float16,
                toVkBool features.Shaders.Int8
            )

        let vp =
            VkPhysicalDeviceVariablePointersFeatures(
                toVkBool features.Shaders.VariablePointersStorageBuffer,
                toVkBool features.Shaders.VariablePointers
            )

        let dp =
            VkPhysicalDeviceShaderDrawParametersFeatures(
                toVkBool features.Shaders.DrawParameters
            )

        let idx =
            VkPhysicalDeviceDescriptorIndexingFeaturesEXT(
                toVkBool features.Shaders.InputAttachmentArrayDynamicIndexing,
                toVkBool features.Shaders.UniformTexelBufferArrayDynamicIndexing,
                toVkBool features.Shaders.StorageTexelBufferArrayDynamicIndexing,
                toVkBool features.Shaders.UniformBufferArrayNonUniformIndexing,
                toVkBool features.Shaders.SampledImageArrayNonUniformIndexing,
                toVkBool features.Shaders.StorageBufferArrayNonUniformIndexing,
                toVkBool features.Shaders.StorageImageArrayNonUniformIndexing,
                toVkBool features.Shaders.InputAttachmentArrayNonUniformIndexing,
                toVkBool features.Shaders.UniformTexelBufferArrayNonUniformIndexing,
                toVkBool features.Shaders.StorageTexelBufferArrayNonUniformIndexing,
                toVkBool features.Descriptors.BindingUniformBufferUpdateAfterBind,
                toVkBool features.Descriptors.BindingSampledImageUpdateAfterBind,
                toVkBool features.Descriptors.BindingStorageImageUpdateAfterBind,
                toVkBool features.Descriptors.BindingStorageBufferUpdateAfterBind,
                toVkBool features.Descriptors.BindingUniformTexelBufferUpdateAfterBind,
                toVkBool features.Descriptors.BindingStorageTexelBufferUpdateAfterBind,
                toVkBool features.Descriptors.BindingUpdateUnusedWhilePending,
                toVkBool features.Descriptors.BindingPartiallyBound,
                toVkBool features.Descriptors.BindingVariableDescriptorCount,
                toVkBool features.Descriptors.RuntimeDescriptorArray
            )

        let rtp =
            VkPhysicalDeviceRayTracingPipelineFeaturesKHR(
                toVkBool features.Raytracing.Pipeline,
                toVkBool features.Raytracing.ShaderGroupHandleCaptureReplay,
                toVkBool features.Raytracing.ShaderGroupHandleCaptureReplayMixed,
                toVkBool features.Raytracing.TraceRaysIndirect,
                toVkBool features.Raytracing.RayTraversalPrimitiveCulling
            )

        let rtpos =
            VkPhysicalDeviceRayTracingPositionFetchFeaturesKHR(
                toVkBool features.Raytracing.PositionFetch
            )

        let acc =
            VkPhysicalDeviceAccelerationStructureFeaturesKHR(
                toVkBool features.Raytracing.AccelerationStructure,
                toVkBool features.Raytracing.AccelerationStructureCaptureReplay,
                toVkBool features.Raytracing.AccelerationStructureIndirectBuild,
                toVkBool features.Raytracing.AccelerationStructureHostCommands,
                toVkBool features.Descriptors.BindingAccelerationStructureUpdateAfterBind
            )

        let rq =
            VkPhysicalDeviceRayQueryFeaturesKHR(
                toVkBool features.Raytracing.RayQuery
            )

        let bda =
            VkPhysicalDeviceBufferDeviceAddressFeaturesKHR(
                toVkBool features.Memory.BufferDeviceAddress,
                toVkBool features.Memory.BufferDeviceAddressCaptureReplay,
                toVkBool features.Memory.BufferDeviceAddressMultiDevice
            )

        let features =
            VkPhysicalDeviceFeatures2(
                VkPhysicalDeviceFeatures(
                    toVkBool features.Memory.RobustBufferAccess,
                    toVkBool features.GraphicsPipeline.Drawing.FullDrawIndexUint32,
                    toVkBool features.Images.ImageCubeArray,
                    toVkBool features.GraphicsPipeline.Blending.IndependentBlend,
                    toVkBool features.Shaders.GeometryShader,
                    toVkBool features.Shaders.TessellationShader,
                    toVkBool features.GraphicsPipeline.Rasterizer.SampleRateShading,
                    toVkBool features.GraphicsPipeline.Blending.DualSrcBlend,
                    toVkBool features.GraphicsPipeline.Blending.LogicOp,
                    toVkBool features.GraphicsPipeline.Drawing.MultiDrawIndirect,
                    toVkBool features.GraphicsPipeline.Drawing.DrawIndirectFirstInstance,
                    toVkBool features.GraphicsPipeline.Depth.Clamp,
                    toVkBool features.GraphicsPipeline.Depth.BiasClamp,
                    toVkBool features.GraphicsPipeline.Rasterizer.FillModeNonSolid,
                    toVkBool features.GraphicsPipeline.Depth.BoundsTest,
                    toVkBool features.GraphicsPipeline.Rasterizer.WideLines,
                    toVkBool features.GraphicsPipeline.Rasterizer.LargePoints,
                    toVkBool features.GraphicsPipeline.Rasterizer.AlphaToOne,
                    toVkBool features.GraphicsPipeline.Multiview.MultiViewport,
                    toVkBool features.Samplers.Anisotropy,
                    toVkBool features.Images.CompressionETC2,
                    toVkBool features.Images.CompressionASTC_LDR,
                    toVkBool features.Images.CompressionBC,
                    toVkBool features.Queries.OcclusionQueryPrecise,
                    toVkBool features.Queries.PipelineStatistics,
                    toVkBool features.Shaders.VertexPipelineStoresAndAtomics,
                    toVkBool features.Shaders.FragmentStoresAndAtomics,
                    toVkBool features.Shaders.TessellationAndGeometryPointSize,
                    toVkBool features.Shaders.ImageGatherExtended,
                    toVkBool features.Shaders.StorageImageExtendedFormats,
                    toVkBool features.Shaders.StorageImageMultisample,
                    toVkBool features.Shaders.StorageImageReadWithoutFormat,
                    toVkBool features.Shaders.StorageImageWriteWithoutFormat,
                    toVkBool features.Shaders.UniformBufferArrayDynamicIndexing,
                    toVkBool features.Shaders.SampledImageArrayDynamicIndexing,
                    toVkBool features.Shaders.StorageBufferArrayDynamicIndexing,
                    toVkBool features.Shaders.StorageImageArrayDynamicIndexing,
                    toVkBool features.Shaders.ClipDistance,
                    toVkBool features.Shaders.CullDistance,
                    toVkBool features.Shaders.Float64,
                    toVkBool features.Shaders.Int64,
                    toVkBool features.Shaders.Int16,
                    toVkBool features.Shaders.ResourceResidency,
                    toVkBool features.Shaders.ResourceMinLod,
                    toVkBool features.Memory.SparseBinding,
                    toVkBool features.Memory.SparseResidencyBuffer,
                    toVkBool features.Memory.SparseResidencyImage2D,
                    toVkBool features.Memory.SparseResidencyImage3D,
                    toVkBool features.Memory.SparseResidency2Samples,
                    toVkBool features.Memory.SparseResidency4Samples,
                    toVkBool features.Memory.SparseResidency8Samples,
                    toVkBool features.Memory.SparseResidency16Samples,
                    toVkBool features.Memory.SparseResidencyAliased,
                    toVkBool features.GraphicsPipeline.Rasterizer.VariableMultisampleRate,
                    toVkBool features.Queries.InheritedQueries
                )
            )

        VkStructChain.empty()
        |> VkStructChain.add mem
        |> if not memp.IsEmpty then VkStructChain.add memp else id
        |> VkStructChain.add ycbcr
        |> if not s8.IsEmpty then VkStructChain.add s8 else id
        |> VkStructChain.add s16
        |> if not f16i8.IsEmpty then VkStructChain.add f16i8 else id
        |> VkStructChain.add vp
        |> VkStructChain.add dp
        |> if not idx.IsEmpty then VkStructChain.add idx else id
        |> if not rtp.IsEmpty then VkStructChain.add rtp else id
        |> if not rtpos.IsEmpty then VkStructChain.add rtpos else id
        |> if not acc.IsEmpty then VkStructChain.add acc else id
        |> if not rq.IsEmpty  then VkStructChain.add rq  else id
        |> if not bda.IsEmpty then VkStructChain.add bda else id
        |> VkStructChain.add features

    let create (protectedMemoryFeatures : VkPhysicalDeviceProtectedMemoryFeatures)
               (memoryPriorityFeatures : VkPhysicalDeviceMemoryPriorityFeaturesEXT)
               (samplerYcbcrConversionFeatures : VkPhysicalDeviceSamplerYcbcrConversionFeatures)
               (storage8BitFeatures : VkPhysicalDevice8BitStorageFeaturesKHR)
               (storage16BitFeatures : VkPhysicalDevice16BitStorageFeatures)
               (float16int8Features : VkPhysicalDeviceFloat16Int8FeaturesKHR)
               (variablePointerFeatures : VkPhysicalDeviceVariablePointersFeatures)
               (shaderDrawParametersFeatures : VkPhysicalDeviceShaderDrawParametersFeatures)
               (descriptorIndexingFeatures : VkPhysicalDeviceDescriptorIndexingFeaturesEXT)
               (raytracingPipelineFeatures : VkPhysicalDeviceRayTracingPipelineFeaturesKHR)
               (raytracingPositionFetchFeatures : VkPhysicalDeviceRayTracingPositionFetchFeaturesKHR)
               (accelerationStructureFeatures : VkPhysicalDeviceAccelerationStructureFeaturesKHR)
               (rayQueryFeatures : VkPhysicalDeviceRayQueryFeaturesKHR)
               (bufferDeviceAddressFeatures : VkPhysicalDeviceBufferDeviceAddressFeaturesKHR)
               (features : VkPhysicalDeviceFeatures) =

        {
            Memory =
                {
                    RobustBufferAccess =                            toBool features.robustBufferAccess
                    SparseBinding =                                 toBool features.sparseBinding
                    SparseResidencyBuffer =                         toBool features.sparseResidencyBuffer
                    SparseResidencyImage2D =                        toBool features.sparseResidencyImage2D
                    SparseResidencyImage3D =                        toBool features.sparseResidencyImage3D
                    SparseResidency2Samples =                       toBool features.sparseResidency2Samples
                    SparseResidency4Samples =                       toBool features.sparseResidency4Samples
                    SparseResidency8Samples =                       toBool features.sparseResidency8Samples
                    SparseResidency16Samples =                      toBool features.sparseResidency16Samples
                    SparseResidencyAliased =                        toBool features.sparseResidencyAliased
                    ProtectedMemory =                               toBool protectedMemoryFeatures.protectedMemory
                    MemoryPriority =                                toBool memoryPriorityFeatures.memoryPriority
                    BufferDeviceAddress =                           toBool bufferDeviceAddressFeatures.bufferDeviceAddress
                    BufferDeviceAddressCaptureReplay =              toBool bufferDeviceAddressFeatures.bufferDeviceAddressCaptureReplay
                    BufferDeviceAddressMultiDevice =                toBool bufferDeviceAddressFeatures.bufferDeviceAddressMultiDevice
                }

            Descriptors =
                {
                    BindingUniformBufferUpdateAfterBind =         toBool descriptorIndexingFeatures.descriptorBindingUniformBufferUpdateAfterBind
                    BindingSampledImageUpdateAfterBind =          toBool descriptorIndexingFeatures.descriptorBindingSampledImageUpdateAfterBind
                    BindingStorageImageUpdateAfterBind =          toBool descriptorIndexingFeatures.descriptorBindingStorageImageUpdateAfterBind
                    BindingStorageBufferUpdateAfterBind =         toBool descriptorIndexingFeatures.descriptorBindingStorageBufferUpdateAfterBind
                    BindingUniformTexelBufferUpdateAfterBind =    toBool descriptorIndexingFeatures.descriptorBindingUniformTexelBufferUpdateAfterBind
                    BindingStorageTexelBufferUpdateAfterBind =    toBool descriptorIndexingFeatures.descriptorBindingStorageTexelBufferUpdateAfterBind
                    BindingAccelerationStructureUpdateAfterBind = toBool accelerationStructureFeatures.descriptorBindingAccelerationStructureUpdateAfterBind
                    BindingUpdateUnusedWhilePending =             toBool descriptorIndexingFeatures.descriptorBindingUpdateUnusedWhilePending
                    BindingPartiallyBound =                       toBool descriptorIndexingFeatures.descriptorBindingPartiallyBound
                    BindingVariableDescriptorCount =              toBool descriptorIndexingFeatures.descriptorBindingVariableDescriptorCount
                    RuntimeDescriptorArray =                      toBool descriptorIndexingFeatures.runtimeDescriptorArray
                }

            Images =
                {
                    ImageCubeArray =              toBool features.imageCubeArray
                    CompressionETC2 =             toBool features.textureCompressionETC2
                    CompressionASTC_LDR =         toBool features.textureCompressionASTC_LDR
                    CompressionBC =               toBool features.textureCompressionBC
                }

            Samplers =
                {
                    Anisotropy =        toBool features.samplerAnisotropy
                    YcbcrConversion =   toBool samplerYcbcrConversionFeatures.samplerYcbcrConversion
                }

            Shaders =
                {
                    GeometryShader =                            toBool features.geometryShader
                    TessellationShader =                        toBool features.tessellationShader
                    VertexPipelineStoresAndAtomics =            toBool features.vertexPipelineStoresAndAtomics
                    FragmentStoresAndAtomics =                  toBool features.fragmentStoresAndAtomics
                    TessellationAndGeometryPointSize =          toBool features.shaderTessellationAndGeometryPointSize
                    ImageGatherExtended =                       toBool features.shaderImageGatherExtended
                    StorageImageExtendedFormats =               toBool features.shaderStorageImageExtendedFormats
                    StorageImageMultisample =                   toBool features.shaderStorageImageMultisample
                    StorageImageReadWithoutFormat =             toBool features.shaderStorageImageReadWithoutFormat
                    StorageImageWriteWithoutFormat =            toBool features.shaderStorageImageWriteWithoutFormat
                    UniformBufferArrayDynamicIndexing =         toBool features.shaderUniformBufferArrayDynamicIndexing
                    SampledImageArrayDynamicIndexing =          toBool features.shaderSampledImageArrayDynamicIndexing
                    StorageBufferArrayDynamicIndexing =         toBool features.shaderStorageBufferArrayDynamicIndexing
                    StorageImageArrayDynamicIndexing =          toBool features.shaderStorageImageArrayDynamicIndexing
                    InputAttachmentArrayDynamicIndexing =       toBool descriptorIndexingFeatures.shaderInputAttachmentArrayDynamicIndexing
                    UniformTexelBufferArrayDynamicIndexing =    toBool descriptorIndexingFeatures.shaderUniformTexelBufferArrayDynamicIndexing
                    StorageTexelBufferArrayDynamicIndexing =    toBool descriptorIndexingFeatures.shaderStorageTexelBufferArrayDynamicIndexing
                    UniformBufferArrayNonUniformIndexing =      toBool descriptorIndexingFeatures.shaderUniformBufferArrayNonUniformIndexing
                    SampledImageArrayNonUniformIndexing =       toBool descriptorIndexingFeatures.shaderSampledImageArrayNonUniformIndexing
                    StorageBufferArrayNonUniformIndexing =      toBool descriptorIndexingFeatures.shaderStorageBufferArrayNonUniformIndexing
                    StorageImageArrayNonUniformIndexing =       toBool descriptorIndexingFeatures.shaderStorageImageArrayNonUniformIndexing
                    InputAttachmentArrayNonUniformIndexing =    toBool descriptorIndexingFeatures.shaderInputAttachmentArrayNonUniformIndexing
                    UniformTexelBufferArrayNonUniformIndexing = toBool descriptorIndexingFeatures.shaderUniformTexelBufferArrayNonUniformIndexing
                    StorageTexelBufferArrayNonUniformIndexing = toBool descriptorIndexingFeatures.shaderStorageTexelBufferArrayNonUniformIndexing
                    StorageBuffer8BitAccess =                   toBool storage8BitFeatures.storageBuffer8BitAccess
                    StorageBuffer16BitAccess =                  toBool storage16BitFeatures.storageBuffer16BitAccess
                    UniformAndStorageBuffer8BitAccess =         toBool storage8BitFeatures.uniformAndStorageBuffer8BitAccess
                    UniformAndStorageBuffer16BitAccess =        toBool storage16BitFeatures.uniformAndStorageBuffer16BitAccess
                    StoragePushConstant8 =                      toBool storage8BitFeatures.storagePushConstant8
                    StoragePushConstant16 =                     toBool storage16BitFeatures.storagePushConstant16
                    StorageInputOutput16 =                      toBool storage16BitFeatures.storageInputOutput16
                    ClipDistance =                              toBool features.shaderClipDistance
                    CullDistance =                              toBool features.shaderCullDistance
                    Float16 =                                   toBool float16int8Features.shaderFloat16
                    Float64 =                                   toBool features.shaderFloat64
                    Int8 =                                      toBool float16int8Features.shaderInt8
                    Int16 =                                     toBool features.shaderInt16
                    Int64 =                                     toBool features.shaderInt64
                    ResourceResidency =                         toBool features.shaderResourceResidency
                    ResourceMinLod =                            toBool features.shaderResourceMinLod
                    VariablePointers =                          toBool variablePointerFeatures.variablePointers
                    VariablePointersStorageBuffer =             toBool variablePointerFeatures.variablePointersStorageBuffer
                    DrawParameters =                            toBool shaderDrawParametersFeatures.shaderDrawParameters
                }

            Queries =
                {
                    OcclusionQueryPrecise = toBool features.occlusionQueryPrecise
                    PipelineStatistics =    toBool features.pipelineStatisticsQuery
                    InheritedQueries =      toBool features.inheritedQueries
                }

            GraphicsPipeline =
                {
                    Depth =
                        {
                            Clamp =      toBool features.depthClamp
                            BiasClamp =  toBool features.depthBiasClamp
                            BoundsTest = toBool features.depthBounds
                        }

                    Blending =
                        {
                            IndependentBlend = toBool features.independentBlend
                            DualSrcBlend =     toBool features.dualSrcBlend
                            LogicOp =          toBool features.logicOp
                        }

                    Drawing =
                        {
                            FullDrawIndexUint32 =       toBool features.fullDrawIndexUint32
                            MultiDrawIndirect =         toBool features.multiDrawIndirect
                            DrawIndirectFirstInstance = toBool features.drawIndirectFirstInstance
                        }

                    Multiview =
                        {
                            MultiViewport =               toBool features.multiViewport
                        }

                    Rasterizer =
                        {
                             WideLines =               toBool features.wideLines
                             LargePoints =             toBool features.largePoints
                             FillModeNonSolid =        toBool features.fillModeNonSolid
                             SampleRateShading =       toBool features.sampleRateShading
                             AlphaToOne =              toBool features.alphaToOne
                             VariableMultisampleRate = toBool features.variableMultisampleRate
                        }
                }

            Raytracing =
                {
                    Pipeline =                            toBool raytracingPipelineFeatures.rayTracingPipeline
                    PositionFetch =                       toBool raytracingPositionFetchFeatures.rayTracingPositionFetch
                    RayQuery =                            toBool rayQueryFeatures.rayQuery
                    ShaderGroupHandleCaptureReplay =      toBool raytracingPipelineFeatures.rayTracingPipelineShaderGroupHandleCaptureReplay
                    ShaderGroupHandleCaptureReplayMixed = toBool raytracingPipelineFeatures.rayTracingPipelineShaderGroupHandleCaptureReplayMixed
                    TraceRaysIndirect =                   toBool raytracingPipelineFeatures.rayTracingPipelineTraceRaysIndirect
                    RayTraversalPrimitiveCulling =        toBool raytracingPipelineFeatures.rayTraversalPrimitiveCulling
                    AccelerationStructure =               toBool accelerationStructureFeatures.accelerationStructure
                    AccelerationStructureCaptureReplay =  toBool accelerationStructureFeatures.accelerationStructureCaptureReplay
                    AccelerationStructureIndirectBuild =  toBool accelerationStructureFeatures.accelerationStructureIndirectBuild
                    AccelerationStructureHostCommands =   toBool accelerationStructureFeatures.accelerationStructureHostCommands
                }
        }