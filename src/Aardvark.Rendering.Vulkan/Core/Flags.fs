namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering
open KHRAccelerationStructure
open KHRRayTracingPipeline

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal VkPipelineStageFlags =

    /// Bitwise combinination of all supported shader stages.
    let AllShaderStages =
        VkPipelineStageFlags.RayTracingShaderBitKhr |||
        VkPipelineStageFlags.VertexShaderBit |||
        VkPipelineStageFlags.TessellationControlShaderBit |||
        VkPipelineStageFlags.TessellationEvaluationShaderBit |||
        VkPipelineStageFlags.GeometryShaderBit |||
        VkPipelineStageFlags.FragmentShaderBit |||
        VkPipelineStageFlags.ComputeShaderBit

    module private Conversion =

        let ofQueueFlags =
            let general =
                VkPipelineStageFlags.TopOfPipeBit |||
                VkPipelineStageFlags.BottomOfPipeBit |||
                VkPipelineStageFlags.HostBit |||
                VkPipelineStageFlags.AllCommandsBit

            let transfer =
                general ||| VkPipelineStageFlags.TransferBit

            let graphics =
                transfer |||
                VkPipelineStageFlags.DrawIndirectBit |||
                VkPipelineStageFlags.VertexInputBit |||
                VkPipelineStageFlags.VertexShaderBit |||
                VkPipelineStageFlags.TessellationControlShaderBit |||
                VkPipelineStageFlags.TessellationEvaluationShaderBit |||
                VkPipelineStageFlags.GeometryShaderBit |||
                VkPipelineStageFlags.FragmentShaderBit |||
                VkPipelineStageFlags.EarlyFragmentTestsBit |||
                VkPipelineStageFlags.LateFragmentTestsBit |||
                VkPipelineStageFlags.ColorAttachmentOutputBit |||
                VkPipelineStageFlags.AllGraphicsBit

            let compute =
                transfer |||
                VkPipelineStageFlags.DrawIndirectBit |||
                VkPipelineStageFlags.ComputeShaderBit |||
                VkPipelineStageFlags.AccelerationStructureBuildBitKhr |||
                VkPipelineStageFlags.RayTracingShaderBitKhr

            Map.ofList [
               QueueFlags.Graphics, graphics
               QueueFlags.Compute, compute
               QueueFlags.Transfer, transfer
               QueueFlags.SparseBinding, general
            ]

    let ofQueueFlags (flags : QueueFlags) =
        flags |> Enum.convertFlags Conversion.ofQueueFlags VkPipelineStageFlags.None

[<AutoOpen>]
module internal ``Synchronization Extensions`` =

    let private filterStageAndAccess (neutralStage : VkPipelineStageFlags)
                                     (supported : VkPipelineStageFlags)
                                     (stage : VkPipelineStageFlags) (access : VkAccessFlags) =
            let filtered = stage &&& supported
            if filtered = VkPipelineStageFlags.None then
                neutralStage, VkAccessFlags.None
            else
                filtered, access

    /// Filters stage and access flags with the given supported stages.
    let filterSrcStageAndAccess = filterStageAndAccess VkPipelineStageFlags.TopOfPipeBit

    /// Filters stage and access flags with the given supported stages.
    let filterDstStageAndAccess = filterStageAndAccess VkPipelineStageFlags.BottomOfPipeBit

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkAccessFlags =

    module private Conversion =
        let ofResourceAccess =
            Map.ofList [
                ResourceAccess.ShaderRead,                 VkAccessFlags.ShaderReadBit
                ResourceAccess.ShaderWrite,                VkAccessFlags.ShaderWriteBit
                ResourceAccess.TransferRead,               VkAccessFlags.TransferReadBit
                ResourceAccess.TransferWrite,              VkAccessFlags.TransferWriteBit
                ResourceAccess.IndirectCommandRead,        VkAccessFlags.IndirectCommandReadBit
                ResourceAccess.IndexRead,                  VkAccessFlags.IndexReadBit
                ResourceAccess.VertexAttributeRead,        VkAccessFlags.VertexAttributeReadBit
                ResourceAccess.UniformRead,                VkAccessFlags.UniformReadBit
                ResourceAccess.InputRead,                  VkAccessFlags.InputAttachmentReadBit
                ResourceAccess.ColorRead,                  VkAccessFlags.ColorAttachmentReadBit
                ResourceAccess.ColorWrite,                 VkAccessFlags.ColorAttachmentWriteBit
                ResourceAccess.DepthStencilRead,           VkAccessFlags.DepthStencilAttachmentReadBit
                ResourceAccess.DepthStencilWrite,          VkAccessFlags.DepthStencilAttachmentWriteBit
                ResourceAccess.AccelerationStructureRead , VkAccessFlags.AccelerationStructureReadBitKhr
                ResourceAccess.AccelerationStructureWrite, VkAccessFlags.AccelerationStructureWriteBitKhr
            ]

    let ofResourceAccess (access : ResourceAccess) =
        access |> Enum.convertFlags Conversion.ofResourceAccess VkAccessFlags.None


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkImageLayout =
    open KHRSwapchain

    let ofTextureLayout =
        LookupTable.lookupTable [
            TextureLayout.Undefined, VkImageLayout.Undefined
            TextureLayout.Sample, VkImageLayout.ShaderReadOnlyOptimal
            TextureLayout.ShaderRead, VkImageLayout.ShaderReadOnlyOptimal
            TextureLayout.ShaderReadWrite, VkImageLayout.General
            TextureLayout.ShaderWrite, VkImageLayout.General
            TextureLayout.TransferRead, VkImageLayout.TransferSrcOptimal
            TextureLayout.TransferWrite, VkImageLayout.TransferDstOptimal
            TextureLayout.ColorAttachment, VkImageLayout.ColorAttachmentOptimal
            TextureLayout.DepthStencil, VkImageLayout.DepthStencilAttachmentOptimal
            TextureLayout.DepthStencilRead, VkImageLayout.DepthStencilReadOnlyOptimal
            TextureLayout.General, VkImageLayout.General
            TextureLayout.Present, VkImageLayout.PresentSrcKhr
        ]

    let private toStageFlags (neutral : VkPipelineStageFlags) =
        LookupTable.lookupTable [
            VkImageLayout.Undefined,                     neutral
            VkImageLayout.General,                       VkPipelineStageFlags.AllCommandsBit
            VkImageLayout.ColorAttachmentOptimal,        VkPipelineStageFlags.ColorAttachmentOutputBit
            VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags.EarlyFragmentTestsBit ||| VkPipelineStageFlags.LateFragmentTestsBit
            VkImageLayout.DepthStencilReadOnlyOptimal,   VkPipelineStageFlags.EarlyFragmentTestsBit ||| VkPipelineStageFlags.LateFragmentTestsBit
            VkImageLayout.ShaderReadOnlyOptimal,         VkPipelineStageFlags.AllShaderStages
            VkImageLayout.TransferSrcOptimal,            VkPipelineStageFlags.TransferBit
            VkImageLayout.TransferDstOptimal,            VkPipelineStageFlags.TransferBit
            VkImageLayout.Preinitialized,                VkPipelineStageFlags.HostBit
            VkImageLayout.PresentSrcKhr,                 neutral
        ]

    /// Returns the appropriate source stage flags for the given image layout.
    let toSrcStageFlags (layout : VkImageLayout) =
        layout |> toStageFlags VkPipelineStageFlags.TopOfPipeBit

    /// Returns the appropriate destination stage flags for the given image layout.
    let toDstStageFlags (layout : VkImageLayout) =
        layout |> toStageFlags VkPipelineStageFlags.BottomOfPipeBit

    /// Returns the appropriate source access flags for the given image layout.
    let toSrcAccessFlags =
        LookupTable.lookupTable [
            VkImageLayout.Undefined,                     VkAccessFlags.None
            VkImageLayout.General,                       VkAccessFlags.MemoryWriteBit
            VkImageLayout.ColorAttachmentOptimal,        VkAccessFlags.ColorAttachmentWriteBit
            VkImageLayout.DepthStencilAttachmentOptimal, VkAccessFlags.DepthStencilAttachmentWriteBit
            VkImageLayout.DepthStencilReadOnlyOptimal,   VkAccessFlags.None
            VkImageLayout.ShaderReadOnlyOptimal,         VkAccessFlags.None
            VkImageLayout.TransferSrcOptimal,            VkAccessFlags.None
            VkImageLayout.TransferDstOptimal,            VkAccessFlags.TransferWriteBit
            VkImageLayout.Preinitialized,                VkAccessFlags.HostWriteBit
            VkImageLayout.PresentSrcKhr,                 VkAccessFlags.None
        ]

    /// Returns the appropriate destination access flags for the given image layout.
    let toDstAccessFlags =
        LookupTable.lookupTable [
            VkImageLayout.Undefined,                     VkAccessFlags.None
            VkImageLayout.General,                       VkAccessFlags.MemoryReadBit |||
                                                         VkAccessFlags.MemoryWriteBit
            VkImageLayout.ColorAttachmentOptimal,        VkAccessFlags.ColorAttachmentReadBit |||
                                                         VkAccessFlags.ColorAttachmentWriteBit
            VkImageLayout.DepthStencilAttachmentOptimal, VkAccessFlags.DepthStencilAttachmentReadBit |||
                                                         VkAccessFlags.DepthStencilAttachmentWriteBit
            VkImageLayout.DepthStencilReadOnlyOptimal,   VkAccessFlags.DepthStencilAttachmentReadBit
            VkImageLayout.ShaderReadOnlyOptimal,         VkAccessFlags.ShaderReadBit
            VkImageLayout.TransferSrcOptimal,            VkAccessFlags.TransferReadBit
            VkImageLayout.TransferDstOptimal,            VkAccessFlags.TransferWriteBit
            VkImageLayout.Preinitialized,                VkAccessFlags.HostWriteBit
            VkImageLayout.PresentSrcKhr,                 VkAccessFlags.None
        ]


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkBufferUsageFlags =
    open KHRBufferDeviceAddress

    module private Conversion =

        let ofBufferUsage =
            Map.ofList [
                BufferUsage.Index,                  VkBufferUsageFlags.IndexBufferBit
                BufferUsage.Vertex,                 VkBufferUsageFlags.VertexBufferBit
                BufferUsage.Uniform,                VkBufferUsageFlags.UniformBufferBit
                BufferUsage.Indirect,               VkBufferUsageFlags.IndirectBufferBit
                BufferUsage.Storage,                VkBufferUsageFlags.StorageBufferBit
                BufferUsage.Read,                   VkBufferUsageFlags.TransferSrcBit
                BufferUsage.Write,                  VkBufferUsageFlags.TransferDstBit
                BufferUsage.AccelerationStructure,  VkBufferUsageFlags.ShaderDeviceAddressBitKhr |||
                                                    VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr
            ]

        let toStageFlags =
            Map.ofList [
                VkBufferUsageFlags.TransferDstBit,                                 VkPipelineStageFlags.TransferBit
                VkBufferUsageFlags.TransferSrcBit,                                 VkPipelineStageFlags.TransferBit
                VkBufferUsageFlags.IndexBufferBit,                                 VkPipelineStageFlags.VertexInputBit
                VkBufferUsageFlags.VertexBufferBit,                                VkPipelineStageFlags.VertexInputBit
                VkBufferUsageFlags.IndirectBufferBit,                              VkPipelineStageFlags.DrawIndirectBit |||
                                                                                   VkPipelineStageFlags.AccelerationStructureBuildBitKhr
                VkBufferUsageFlags.UniformBufferBit,                               VkPipelineStageFlags.AllShaderStages
                VkBufferUsageFlags.StorageBufferBit,                               VkPipelineStageFlags.AllShaderStages
                VkBufferUsageFlags.UniformTexelBufferBit,                          VkPipelineStageFlags.AllShaderStages
                VkBufferUsageFlags.StorageTexelBufferBit,                          VkPipelineStageFlags.AllShaderStages
                VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr,  VkPipelineStageFlags.AllShaderStages |||
                                                                                   VkPipelineStageFlags.AccelerationStructureBuildBitKhr
                VkBufferUsageFlags.AccelerationStructureStorageBitKhr,             VkPipelineStageFlags.AllShaderStages |||
                                                                                   VkPipelineStageFlags.AccelerationStructureBuildBitKhr
            ]

        let toSrcAccessFlags =
            Map.ofList [
                VkBufferUsageFlags.TransferDstBit,                                 VkAccessFlags.TransferWriteBit
                VkBufferUsageFlags.TransferSrcBit,                                 VkAccessFlags.None
                VkBufferUsageFlags.IndexBufferBit,                                 VkAccessFlags.None
                VkBufferUsageFlags.VertexBufferBit,                                VkAccessFlags.None
                VkBufferUsageFlags.IndirectBufferBit,                              VkAccessFlags.None
                VkBufferUsageFlags.UniformBufferBit,                               VkAccessFlags.None
                VkBufferUsageFlags.StorageBufferBit,                               VkAccessFlags.ShaderWriteBit
                VkBufferUsageFlags.UniformTexelBufferBit,                          VkAccessFlags.None
                VkBufferUsageFlags.StorageTexelBufferBit,                          VkAccessFlags.ShaderWriteBit
                VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr,  VkAccessFlags.None
                VkBufferUsageFlags.AccelerationStructureStorageBitKhr,             VkAccessFlags.AccelerationStructureWriteBitKhr
            ]

        let toDstAccessFlags =
            Map.ofList [
                VkBufferUsageFlags.TransferDstBit,                                 VkAccessFlags.TransferWriteBit
                VkBufferUsageFlags.TransferSrcBit,                                 VkAccessFlags.TransferReadBit
                VkBufferUsageFlags.IndexBufferBit,                                 VkAccessFlags.IndexReadBit
                VkBufferUsageFlags.VertexBufferBit,                                VkAccessFlags.VertexAttributeReadBit
                VkBufferUsageFlags.IndirectBufferBit,                              VkAccessFlags.IndirectCommandReadBit
                VkBufferUsageFlags.UniformBufferBit,                               VkAccessFlags.UniformReadBit
                VkBufferUsageFlags.StorageBufferBit,                               VkAccessFlags.ShaderReadBit ||| VkAccessFlags.ShaderWriteBit
                VkBufferUsageFlags.UniformTexelBufferBit,                          VkAccessFlags.ShaderReadBit
                VkBufferUsageFlags.StorageTexelBufferBit,                          VkAccessFlags.ShaderReadBit ||| VkAccessFlags.ShaderWriteBit
                VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr,  VkAccessFlags.AccelerationStructureReadBitKhr
                VkBufferUsageFlags.AccelerationStructureStorageBitKhr,             VkAccessFlags.AccelerationStructureReadBitKhr |||
                                                                                   VkAccessFlags.AccelerationStructureWriteBitKhr
            ]

    let private toStageFlags (neutral : VkPipelineStageFlags) (usage : VkBufferUsageFlags) =
        let flags = usage |> Enum.convertFlags Conversion.toStageFlags VkPipelineStageFlags.None
        if flags = VkPipelineStageFlags.None then neutral else flags

    /// Returns the appropriate source stage flags for the given buffer usage.
    let toSrcStageFlags (usage : VkBufferUsageFlags) =
        usage |> toStageFlags VkPipelineStageFlags.TopOfPipeBit

    /// Returns the appropriate destination stage flags for the given buffer usage.
    let toDstStageFlags (usage : VkBufferUsageFlags) =
        usage |> toStageFlags VkPipelineStageFlags.BottomOfPipeBit

    let ofBufferUsage (usage : BufferUsage) =
        usage |> Enum.convertFlags Conversion.ofBufferUsage VkBufferUsageFlags.None

    /// Returns the appropriate source access flags for the given buffer usage flags.
    let toSrcAccessFlags (usage : VkBufferUsageFlags) =
        usage |> Enum.convertFlags Conversion.toSrcAccessFlags VkAccessFlags.None

    /// Returns the appropriate destination access flags for the given buffer usage flags.
    let toDstAccessFlags (usage : VkBufferUsageFlags) =
        usage |> Enum.convertFlags Conversion.toDstAccessFlags VkAccessFlags.None


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkImageUsageFlags =
    open Vulkan11

    let private formatFeatureUsage = [
            VkFormatFeatureFlags.SampledImageBit, VkImageUsageFlags.SampledBit
            VkFormatFeatureFlags.StorageImageBit, VkImageUsageFlags.StorageBit
            VkFormatFeatureFlags.ColorAttachmentBit, VkImageUsageFlags.ColorAttachmentBit
            VkFormatFeatureFlags.DepthStencilAttachmentBit, VkImageUsageFlags.DepthStencilAttachmentBit
            VkFormatFeatureFlags.TransferSrcBit, VkImageUsageFlags.TransferSrcBit
            VkFormatFeatureFlags.TransferDstBit, VkImageUsageFlags.TransferDstBit
        ]

    let getUnsupported (features : VkFormatFeatureFlags) =
        (VkImageUsageFlags.None, formatFeatureUsage) ||> List.fold (fun r (f, u) ->
            if not <| features.HasFlag f then r ||| u else r
        )

    let filterSupported (features : VkFormatFeatureFlags) (usage : VkImageUsageFlags) =
        let unsupported = getUnsupported features
        usage &&& ~~~unsupported