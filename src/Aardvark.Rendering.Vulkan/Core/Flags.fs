namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkImageLayout =
    open KHRSwapchain
    open KHRRayTracingPipeline

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

    let toAccessFlags =
        LookupTable.lookupTable [
            VkImageLayout.Undefined,                        VkAccessFlags.None
            VkImageLayout.General,                          VkAccessFlags.All
            VkImageLayout.ColorAttachmentOptimal,           VkAccessFlags.ColorAttachmentWriteBit
            VkImageLayout.DepthStencilAttachmentOptimal,    VkAccessFlags.DepthStencilAttachmentWriteBit
            VkImageLayout.DepthStencilReadOnlyOptimal,      VkAccessFlags.DepthStencilAttachmentReadBit
            VkImageLayout.ShaderReadOnlyOptimal,            VkAccessFlags.ShaderReadBit
            VkImageLayout.TransferSrcOptimal,               VkAccessFlags.TransferReadBit
            VkImageLayout.TransferDstOptimal,               VkAccessFlags.TransferWriteBit
            VkImageLayout.Preinitialized,                   VkAccessFlags.HostWriteBit
            VkImageLayout.PresentSrcKhr,                    VkAccessFlags.MemoryReadBit
        ]

    let private toStageFlags (graphicsPipelineStage : VkPipelineStageFlags) (fragmentTestStage : VkPipelineStageFlags) =
        let shaderStages  =
            graphicsPipelineStage |||
            VkPipelineStageFlags.ComputeShaderBit |||
            VkPipelineStageFlags.RayTracingShaderBitKhr

        LookupTable.lookupTable [
            VkImageLayout.Undefined,                        VkPipelineStageFlags.HostBit
            VkImageLayout.General,                          VkPipelineStageFlags.AllCommandsBit
            VkImageLayout.ColorAttachmentOptimal,           VkPipelineStageFlags.ColorAttachmentOutputBit
            VkImageLayout.DepthStencilAttachmentOptimal,    fragmentTestStage
            VkImageLayout.DepthStencilReadOnlyOptimal,      fragmentTestStage
            VkImageLayout.ShaderReadOnlyOptimal,            shaderStages
            VkImageLayout.TransferSrcOptimal,               VkPipelineStageFlags.TransferBit
            VkImageLayout.TransferDstOptimal,               VkPipelineStageFlags.TransferBit
            VkImageLayout.Preinitialized,                   VkPipelineStageFlags.HostBit
            VkImageLayout.PresentSrcKhr,                    VkPipelineStageFlags.TransferBit
        ]

    let toSrcStageFlags (queue : QueueFlags) =
        toStageFlags VkPipelineStageFlags.FragmentShaderBit VkPipelineStageFlags.LateFragmentTestsBit
        >> QueueFlags.filterStages queue

    let toDstStageFlags (queue : QueueFlags) =
        toStageFlags VkPipelineStageFlags.VertexShaderBit VkPipelineStageFlags.EarlyFragmentTestsBit
        >> QueueFlags.filterStages queue


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkBufferUsageFlags =
    open KHRBufferDeviceAddress
    open KHRAccelerationStructure

    let private conversion = Map.ofList [
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

    let ofBufferUsage (usage : BufferUsage) =
        usage |> Enum.convertFlags conversion VkBufferUsageFlags.None


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkAccessFlags =
    open KHRAccelerationStructure
    open KHRRayTracingPipeline

    module private Conversion =
        let ofResourceAccess = Map.ofList [
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

        let ofBufferUsage = Map.ofList [
            VkBufferUsageFlags.TransferDstBit,                                 VkAccessFlags.TransferWriteBit
            VkBufferUsageFlags.TransferSrcBit,                                 VkAccessFlags.TransferReadBit
            VkBufferUsageFlags.IndexBufferBit,                                 VkAccessFlags.IndexReadBit
            VkBufferUsageFlags.VertexBufferBit,                                VkAccessFlags.VertexAttributeReadBit
            VkBufferUsageFlags.IndirectBufferBit,                              VkAccessFlags.IndirectCommandReadBit
            VkBufferUsageFlags.StorageBufferBit,                               VkAccessFlags.ShaderReadBit
            VkBufferUsageFlags.UniformTexelBufferBit,                          VkAccessFlags.ShaderReadBit
            VkBufferUsageFlags.StorageTexelBufferBit,                          VkAccessFlags.ShaderReadBit
            VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr,  VkAccessFlags.AccelerationStructureReadBitKhr
            VkBufferUsageFlags.AccelerationStructureStorageBitKhr,             VkAccessFlags.AccelerationStructureReadBitKhr |||
                                                                               VkAccessFlags.AccelerationStructureWriteBitKhr

        ]

        let private toStageFlags (graphicsPipelineStage : VkPipelineStageFlags) =
            let shaderStages  =
                graphicsPipelineStage |||
                VkPipelineStageFlags.ComputeShaderBit |||
                VkPipelineStageFlags.RayTracingShaderBitKhr

            Map.ofList [
                VkAccessFlags.HostReadBit ,                      VkPipelineStageFlags.HostBit
                VkAccessFlags.HostWriteBit,                      VkPipelineStageFlags.HostBit
                VkAccessFlags.ShaderReadBit,                     shaderStages
                VkAccessFlags.ShaderWriteBit,                    shaderStages
                VkAccessFlags.TransferReadBit,                   VkPipelineStageFlags.TransferBit
                VkAccessFlags.TransferWriteBit,                  VkPipelineStageFlags.TransferBit
                VkAccessFlags.IndirectCommandReadBit,            VkPipelineStageFlags.DrawIndirectBit
                VkAccessFlags.IndexReadBit,                      VkPipelineStageFlags.VertexInputBit
                VkAccessFlags.VertexAttributeReadBit,            VkPipelineStageFlags.VertexInputBit
                VkAccessFlags.UniformReadBit,                    shaderStages ||| VkPipelineStageFlags.AccelerationStructureBuildBitKhr
                VkAccessFlags.InputAttachmentReadBit,            VkPipelineStageFlags.VertexShaderBit
                VkAccessFlags.ColorAttachmentReadBit,            VkPipelineStageFlags.VertexShaderBit
                VkAccessFlags.ColorAttachmentWriteBit,           VkPipelineStageFlags.FragmentShaderBit
                VkAccessFlags.DepthStencilAttachmentReadBit,     VkPipelineStageFlags.VertexShaderBit
                VkAccessFlags.DepthStencilAttachmentWriteBit,    VkPipelineStageFlags.FragmentShaderBit
                VkAccessFlags.AccelerationStructureReadBitKhr,   shaderStages ||| VkPipelineStageFlags.AccelerationStructureBuildBitKhr
                VkAccessFlags.AccelerationStructureWriteBitKhr,  VkPipelineStageFlags.AccelerationStructureBuildBitKhr
            ]

        let private srcStageFlagsLookup =
            toStageFlags VkPipelineStageFlags.FragmentShaderBit

        let private dstStageFlagsLookup =
            toStageFlags VkPipelineStageFlags.VertexShaderBit

        let toSrcStageFlags (queue : QueueFlags) =
            srcStageFlagsLookup |> Map.map (fun _ -> QueueFlags.filterStages queue)

        let toDstStageFlags (queue : QueueFlags) =
            dstStageFlagsLookup |> Map.map (fun _ -> QueueFlags.filterStages queue)

    let ofResourceAccess (access : ResourceAccess) =
        access |> Enum.convertFlags Conversion.ofResourceAccess VkAccessFlags.None

    let ofBufferUsage (usage : VkBufferUsageFlags) =
        usage |> Enum.convertFlags Conversion.ofBufferUsage VkAccessFlags.None

    let toSrcStageFlags (queue : QueueFlags) (access : VkAccessFlags) =
        access |> Enum.convertFlags (Conversion.toSrcStageFlags queue) VkPipelineStageFlags.None

    let toDstStageFlags (queue : QueueFlags) (access : VkAccessFlags) =
        access |> Enum.convertFlags (Conversion.toDstStageFlags queue) VkPipelineStageFlags.None


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