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
                ResourceAccess.AccelerationStructureRead,  VkAccessFlags.AccelerationStructureReadBitKhr
                ResourceAccess.AccelerationStructureWrite, VkAccessFlags.AccelerationStructureWriteBitKhr
                ResourceAccess.HostRead,                   VkAccessFlags.HostReadBit
                ResourceAccess.HostWrite,                  VkAccessFlags.HostWriteBit
            ]

        let ofStageFlags =
            Map.ofList [
                VkPipelineStageFlags.HostBit,                           VkAccessFlags.HostReadBit |||
                                                                        VkAccessFlags.HostWriteBit

                VkPipelineStageFlags.DrawIndirectBit,                   VkAccessFlags.IndirectCommandReadBit

                VkPipelineStageFlags.VertexInputBit,                    VkAccessFlags.IndexReadBit |||
                                                                        VkAccessFlags.VertexAttributeReadBit |||
                                                                        VkAccessFlags.UniformReadBit

                VkPipelineStageFlags.VertexShaderBit,                   VkAccessFlags.UniformReadBit |||
                                                                        VkAccessFlags.ShaderReadBit |||
                                                                        VkAccessFlags.ShaderWriteBit

                VkPipelineStageFlags.TessellationControlShaderBit,      VkAccessFlags.UniformReadBit |||
                                                                        VkAccessFlags.ShaderReadBit |||
                                                                        VkAccessFlags.ShaderWriteBit

                VkPipelineStageFlags.TessellationEvaluationShaderBit,   VkAccessFlags.UniformReadBit |||
                                                                        VkAccessFlags.ShaderReadBit |||
                                                                        VkAccessFlags.ShaderWriteBit

                VkPipelineStageFlags.GeometryShaderBit,                 VkAccessFlags.UniformReadBit |||
                                                                        VkAccessFlags.ShaderReadBit |||
                                                                        VkAccessFlags.ShaderWriteBit

                VkPipelineStageFlags.FragmentShaderBit,                 VkAccessFlags.UniformReadBit |||
                                                                        VkAccessFlags.ShaderReadBit |||
                                                                        VkAccessFlags.ShaderWriteBit |||
                                                                        VkAccessFlags.InputAttachmentReadBit

                VkPipelineStageFlags.EarlyFragmentTestsBit,             VkAccessFlags.DepthStencilAttachmentReadBit |||
                                                                        VkAccessFlags.DepthStencilAttachmentWriteBit

                VkPipelineStageFlags.LateFragmentTestsBit,              VkAccessFlags.DepthStencilAttachmentReadBit |||
                                                                        VkAccessFlags.DepthStencilAttachmentWriteBit

                VkPipelineStageFlags.ColorAttachmentOutputBit,          VkAccessFlags.ColorAttachmentReadBit |||
                                                                        VkAccessFlags.ColorAttachmentWriteBit

                VkPipelineStageFlags.ComputeShaderBit,                  VkAccessFlags.UniformReadBit |||
                                                                        VkAccessFlags.ShaderReadBit |||
                                                                        VkAccessFlags.ShaderWriteBit

                VkPipelineStageFlags.TransferBit,                       VkAccessFlags.TransferReadBit |||
                                                                        VkAccessFlags.TransferWriteBit

                VkPipelineStageFlags.AccelerationStructureBuildBitKhr,  VkAccessFlags.ShaderReadBit |||
                                                                        VkAccessFlags.TransferReadBit |||
                                                                        VkAccessFlags.TransferWriteBit

                VkPipelineStageFlags.RayTracingShaderBitKhr,            VkAccessFlags.UniformReadBit |||
                                                                        VkAccessFlags.ShaderReadBit |||
                                                                        VkAccessFlags.ShaderWriteBit
            ]

    let Write =
        VkAccessFlags.ShaderWriteBit |||
        VkAccessFlags.ColorAttachmentWriteBit |||
        VkAccessFlags.DepthStencilAttachmentWriteBit |||
        VkAccessFlags.TransferWriteBit |||
        VkAccessFlags.HostWriteBit |||
        VkAccessFlags.MemoryWriteBit |||
        VkAccessFlags.AccelerationStructureWriteBitKhr

    let ofResourceAccess (access : ResourceAccess) =
        access |> Enum.convertFlags Conversion.ofResourceAccess VkAccessFlags.None

    let ofStageFlags (stages : VkPipelineStageFlags) =
        stages |> Enum.convertFlags Conversion.ofStageFlags (VkAccessFlags.MemoryReadBit ||| VkAccessFlags.MemoryWriteBit)

[<AutoOpen>]
module internal ``Synchronization Extensions`` =

    let private filterStageAndAccess (neutralStage : VkPipelineStageFlags)
                                     (supported : VkPipelineStageFlags)
                                     (stage : VkPipelineStageFlags) (access : VkAccessFlags) =
        let stage =
            let filtered = stage &&& supported
            if filtered = VkPipelineStageFlags.None then
                neutralStage
            else
                filtered

        let supportedAccess = VkAccessFlags.ofStageFlags stage
        stage, access &&& supportedAccess

    /// Filters source stage and access flags with the given supported stages.
    let filterSrcStageAndAccess (supported : VkPipelineStageFlags) (stage : VkPipelineStageFlags) (access : VkAccessFlags) =
        let stage, access = filterStageAndAccess VkPipelineStageFlags.TopOfPipeBit supported stage access
        stage, access &&& VkAccessFlags.Write

    /// Filters destination stage and access flags with the given supported stages.
    let filterDstStageAndAccess (supported : VkPipelineStageFlags) (stage : VkPipelineStageFlags) (access : VkAccessFlags) =
        filterStageAndAccess VkPipelineStageFlags.BottomOfPipeBit supported stage access

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkImageLayout =
    open KHRSwapchain

    let ofTextureLayout =
        LookupTable.lookupTable [
            TextureLayout.Undefined, VkImageLayout.Undefined
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

    /// Returns the appropriate source access flags for the given image layout.
    let toSrcAccessFlags (layout : VkImageLayout) =
        (toDstAccessFlags layout) &&& VkAccessFlags.Write

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

        let toAccessFlags =
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

    /// Returns the appropriate destination access flags for the given buffer usage flags.
    let toDstAccessFlags (usage : VkBufferUsageFlags) =
        usage |> Enum.convertFlags Conversion.toAccessFlags VkAccessFlags.None

    /// Returns the appropriate source access flags for the given buffer usage flags.
    let toSrcAccessFlags (usage : VkBufferUsageFlags) =
        (toDstAccessFlags usage) &&& VkAccessFlags.Write

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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkShaderStageFlags =
    let ofShaderStage =
        LookupTable.lookupTable [
            ShaderStage.Vertex,         VkShaderStageFlags.VertexBit
            ShaderStage.TessControl,    VkShaderStageFlags.TessellationControlBit
            ShaderStage.TessEval,       VkShaderStageFlags.TessellationEvaluationBit
            ShaderStage.Geometry,       VkShaderStageFlags.GeometryBit
            ShaderStage.Fragment,       VkShaderStageFlags.FragmentBit
            ShaderStage.Compute,        VkShaderStageFlags.ComputeBit
            ShaderStage.RayGeneration,  VkShaderStageFlags.RaygenBitKhr
            ShaderStage.Intersection,   VkShaderStageFlags.IntersectionBitKhr
            ShaderStage.AnyHit,         VkShaderStageFlags.AnyHitBitKhr
            ShaderStage.ClosestHit,     VkShaderStageFlags.ClosestHitBitKhr
            ShaderStage.Miss,           VkShaderStageFlags.MissBitKhr
            ShaderStage.Callable,       VkShaderStageFlags.CallableBitKhr
        ]

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal VkSampleCountFlags =

    let toSet (flags : VkSampleCountFlags) =
        Set.ofList [
            if flags.HasFlag VkSampleCountFlags.D1Bit then yield 1
            if flags.HasFlag VkSampleCountFlags.D2Bit then yield 2
            if flags.HasFlag VkSampleCountFlags.D4Bit then yield 4
            if flags.HasFlag VkSampleCountFlags.D8Bit then yield 8
            if flags.HasFlag VkSampleCountFlags.D16Bit then yield 16
            if flags.HasFlag VkSampleCountFlags.D32Bit then yield 32
            if flags.HasFlag VkSampleCountFlags.D64Bit then yield 64
        ]

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkExternalMemoryHandleTypeFlags =
    open System.Runtime.InteropServices
    open Vulkan11

    let private isWindows = RuntimeInformation.IsOSPlatform OSPlatform.Windows

    /// Platform dependent opaque handle type.
    /// OpaqueWin32Bit on Windows, OpaqueFdBit otherwise.
    let OpaqueBit =
        if isWindows then
            VkExternalMemoryHandleTypeFlags.OpaqueWin32Bit
        else
            VkExternalMemoryHandleTypeFlags.OpaqueFdBit

[<AutoOpen>]
module VkExternalMemoryPropertiesExtensions =
    open Vulkan11

    type VkExternalMemoryProperties with
        member inline x.IsExportable = x.externalMemoryFeatures.HasFlag VkExternalMemoryFeatureFlags.ExportableBit