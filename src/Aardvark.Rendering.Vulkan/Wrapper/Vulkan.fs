namespace Aardvark.Rendering.Vulkan

#nowarn "1337"

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open System.Security
open Aardvark.Base

#nowarn "9"
#nowarn "51"

type PFN_vkAllocationFunction = nativeint
type PFN_vkReallocationFunction = nativeint
type PFN_vkInternalAllocationNotification = nativeint
type PFN_vkInternalFreeNotification = nativeint
type PFN_vkFreeFunction = nativeint 
type PFN_vkVoidFunction = nativeint

// missing in vk.xml
type VkCmdBufferCreateFlags = | MinValue = 0
type VkEventCreateFlags = | MinValue = 0
type VkSemaphoreCreateFlags = | MinValue = 0
type VkShaderCreateFlags = | MinValue = 0
type VkShaderModuleCreateFlags = | MinValue = 0
type VkMemoryMapFlags = | MinValue = 0
type VkDisplaySurfaceCreateFlagsKHR = | MinValue = 0
type VkSwapchainCreateFlagsKHR = | MinValue = 0
type VkSwapchainCreateFlagBitsKHR = | MinValue = 0
type VkPipelineLayoutCreateFlags = | MinValue = 0
type VkBufferViewCreateFlags = | MinValue = 0
type VkPipelineShaderStageCreateFlags = | MinValue = 0
type VkDescriptorSetLayoutCreateFlags = | MinValue = 0
type VkDeviceQueueCreateFlags = | MinValue = 0
type VkInstanceCreateFlags = | MinValue = 0
type VkImageViewCreateFlags = | MinValue = 0
type VkDeviceCreateFlags = | MinValue = 0
type VkFramebufferCreateFlags = | MinValue = 0
type VkDescriptorPoolResetFlags = | MinValue = 0
type VkPipelineVertexInputStateCreateFlags = | MinValue = 0
type VkPipelineInputAssemblyStateCreateFlags = | MinValue = 0
type VkPipelineTesselationStateCreateFlags = | MinValue = 0
type VkPipelineViewportStateCreateFlags = | MinValue = 0
type VkPipelineRasterizationStateCreateFlags = | MinValue = 0
type VkPipelineMultisampleStateCreateFlags = | MinValue = 0
type VkPipelineDepthStencilStateCreateFlags = | MinValue = 0
type VkPipelineColorBlendStateCreateFlags = | MinValue = 0
type VkPipelineDynamicStateCreateFlags = | MinValue = 0
type VkPipelineCacheCreateFlags = | MinValue = 0
type VkQueryPoolCreateFlags = | MinValue = 0
type VkSubpassDescriptionFlags = | MinValue = 0
type VkRenderPassCreateFlags = | MinValue = 0
type VkSamplerCreateFlags = | MinValue = 0

type VkAndroidSurfaceCreateFlagsKHR = | MinValue = 0
type VkDisplayModeCreateFlagsKHR = | MinValue = 0
type VkPipelineTessellationStateCreateFlags = | MinValue = 0
type VkXcbSurfaceCreateFlagsKHR = | MinValue = 0
type VkXlibSurfaceCreateFlagsKHR = | MinValue = 0
type VkWin32SurfaceCreateFlagsKHR = | MinValue = 0
type VkWaylandSurfaceCreateFlagsKHR = | MinValue = 0
type VkMirSurfaceCreateFlagsKHR = | MinValue = 0
type PFN_vkDebugReportCallbackEXT = nativeint
type PFN_vkDebugUtilsMessengerCallbackEXT = nativeint

type VkExternalMemoryHandleTypeFlagsNV = | MinValue = 0
type VkExternalMemoryFeatureFlagsNV = | MinValue = 0
type VkIndirectCommandsLayoutUsageFlagsNVX = | MinValue = 0
type VkObjectEntryUsageFlagsNVX = | MinValue = 0

type VkDescriptorUpdateTemplateCreateFlags = | MinValue = 0
type VkExternalFenceHandleTypeFlagsKHR = | MinValue = 0
type VkExternalMemoryHandleTypeFlagsKHR = | MinValue = 0
type VkExternalSemaphoreHandleTypeFlagsKHR = | MinValue = 0
type VkExternalMemoryFeatureFlagsKHR = | MinValue = 0
type VkExternalFenceFeatureFlagsKHR = | MinValue = 0
type VkExternalSemaphoreFeatureFlagsKHR = | MinValue = 0
type VkIOSSurfaceCreateFlagsMVK = | MinValue = 0
type VkFenceImportFlagsKHR = | MinValue = 0
type VkSemaphoreImportFlagsKHR = | MinValue = 0
type VkMacOSSurfaceCreateFlagsMVK = | MinValue = 0
type VkMemoryAllocateFlagsKHX = | MinValue = 0
type VkPipelineCoverageModulationStateCreateFlagsNV = | MinValue = 0
type VkPipelineCoverageToColorStateCreateFlagsNV = | MinValue = 0
type VkPipelineDiscardRectangleStateCreateFlagsEXT = | MinValue = 0
type VkPipelineViewportSwizzleStateCreateFlagsNV = | MinValue = 0
type VkSurfaceCounterFlagsEXT = | MinValue = 0
type VkValidationCacheCreateFlagsEXT = | MinValue = 0
type VkViSurfaceCreateFlagsNN = | MinValue = 0
type VkPeerMemoryFeatureFlagsKHX = | MinValue = 0
type VkCommandPoolTrimFlags = | MinValue = 0
type VkPipelineRasterizationConservativeStateCreateFlagsEXT = | MinValue = 0
type VkDebugUtilsMessengerCallbackDataFlagsEXT = | MinValue = 0
type VkDebugUtilsMessengerCreateFlagsEXT = | MinValue = 0
type VkInstance = nativeint
type VkPhysicalDevice = nativeint
type VkDevice = nativeint
type VkQueue = nativeint
type VkCommandBuffer = nativeint
[<StructLayout(LayoutKind.Sequential)>]
type VkDeviceMemory = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkDeviceMemory(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkCommandPool = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkCommandPool(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBuffer = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkBuffer(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBufferView = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkBufferView(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImage = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkImage(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageView = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkImageView(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkShaderModule = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkShaderModule(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipeline = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkPipeline(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineLayout = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkPipelineLayout(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSampler = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkSampler(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorSet = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkDescriptorSet(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorSetLayout = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkDescriptorSetLayout(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorPool = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkDescriptorPool(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkFence = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkFence(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSemaphore = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkSemaphore(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkEvent = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkEvent(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkQueryPool = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkQueryPool(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkFramebuffer = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkFramebuffer(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkRenderPass = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkRenderPass(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineCache = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkPipelineCache(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkObjectTableNVX = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkObjectTableNVX(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkIndirectCommandsLayoutNVX = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkIndirectCommandsLayoutNVX(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorUpdateTemplate = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkDescriptorUpdateTemplate(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSamplerYcbcrConversion = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkSamplerYcbcrConversion(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkValidationCacheEXT = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkValidationCacheEXT(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDisplayKHR = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkDisplayKHR(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDisplayModeKHR = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkDisplayModeKHR(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSurfaceKHR = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkSurfaceKHR(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSwapchainKHR = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkSwapchainKHR(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDebugReportCallbackEXT = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkDebugReportCallbackEXT(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDebugUtilsMessengerEXT = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkDebugUtilsMessengerEXT(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end


type VkSampleMask = uint32
type VkBool32 = uint32
type VkFlags = uint32
type VkDeviceSize = uint64
type VkImageLayout = 
    | DepthAttachmentStencilReadOnlyOptimal = 1000117001
    | DepthReadOnlyStencilAttachmentOptimal = 1000117000
    | Undefined = 0
    | General = 1
    | ColorAttachmentOptimal = 2
    | DepthStencilAttachmentOptimal = 3
    | DepthStencilReadOnlyOptimal = 4
    | ShaderReadOnlyOptimal = 5
    | TransferSrcOptimal = 6
    | TransferDstOptimal = 7
    | Preinitialized = 8

type VkAttachmentLoadOp = 
    | Load = 0
    | Clear = 1
    | DontCare = 2

type VkAttachmentStoreOp = 
    | Store = 0
    | DontCare = 1

type VkImageType = 
    | D1d = 0
    | D2d = 1
    | D3d = 2

type VkImageTiling = 
    | Optimal = 0
    | Linear = 1

type VkImageViewType = 
    | D1d = 0
    | D2d = 1
    | D3d = 2
    | Cube = 3
    | D1dArray = 4
    | D2dArray = 5
    | CubeArray = 6

type VkCommandBufferLevel = 
    | Primary = 0
    | Secondary = 1

type VkComponentSwizzle = 
    | Identity = 0
    | Zero = 1
    | One = 2
    | R = 3
    | G = 4
    | B = 5
    | A = 6

type VkDescriptorType = 
    | Sampler = 0
    | CombinedImageSampler = 1
    | SampledImage = 2
    | StorageImage = 3
    | UniformTexelBuffer = 4
    | StorageTexelBuffer = 5
    | UniformBuffer = 6
    | StorageBuffer = 7
    | UniformBufferDynamic = 8
    | StorageBufferDynamic = 9
    | InputAttachment = 10

type VkQueryType = 
    | Occlusion = 0
    | PipelineStatistics = 1
    | Timestamp = 2

type VkBorderColor = 
    | FloatTransparentBlack = 0
    | IntTransparentBlack = 1
    | FloatOpaqueBlack = 2
    | IntOpaqueBlack = 3
    | FloatOpaqueWhite = 4
    | IntOpaqueWhite = 5

type VkPipelineBindPoint = 
    | Graphics = 0
    | Compute = 1

type VkPipelineCacheHeaderVersion = 
    | One = 1

type VkPrimitiveTopology = 
    | PointList = 0
    | LineList = 1
    | LineStrip = 2
    | TriangleList = 3
    | TriangleStrip = 4
    | TriangleFan = 5
    | LineListWithAdjacency = 6
    | LineStripWithAdjacency = 7
    | TriangleListWithAdjacency = 8
    | TriangleStripWithAdjacency = 9
    | PatchList = 10

type VkSharingMode = 
    | Exclusive = 0
    | Concurrent = 1

type VkIndexType = 
    | Uint16 = 0
    | Uint32 = 1

type VkFilter = 
    | Nearest = 0
    | Linear = 1

type VkSamplerMipmapMode = 
    | Nearest = 0
    | Linear = 1

type VkSamplerAddressMode = 
    | Repeat = 0
    | MirroredRepeat = 1
    | ClampToEdge = 2
    | ClampToBorder = 3

type VkCompareOp = 
    | Never = 0
    | Less = 1
    | Equal = 2
    | LessOrEqual = 3
    | Greater = 4
    | NotEqual = 5
    | GreaterOrEqual = 6
    | Always = 7

type VkPolygonMode = 
    | Fill = 0
    | Line = 1
    | Point = 2

[<Flags>]
type VkCullModeFlags = 
    | None = 0
    | FrontBit = 0x00000001
    | BackBit = 0x00000002
    | FrontAndBack = 3

type VkFrontFace = 
    | CounterClockwise = 0
    | Clockwise = 1

type VkBlendFactor = 
    | Zero = 0
    | One = 1
    | SrcColor = 2
    | OneMinusSrcColor = 3
    | DstColor = 4
    | OneMinusDstColor = 5
    | SrcAlpha = 6
    | OneMinusSrcAlpha = 7
    | DstAlpha = 8
    | OneMinusDstAlpha = 9
    | ConstantColor = 10
    | OneMinusConstantColor = 11
    | ConstantAlpha = 12
    | OneMinusConstantAlpha = 13
    | SrcAlphaSaturate = 14
    | Src1Color = 15
    | OneMinusSrc1Color = 16
    | Src1Alpha = 17
    | OneMinusSrc1Alpha = 18

type VkBlendOp = 
    | Add = 0
    | Subtract = 1
    | ReverseSubtract = 2
    | Min = 3
    | Max = 4

type VkStencilOp = 
    | Keep = 0
    | Zero = 1
    | Replace = 2
    | IncrementAndClamp = 3
    | DecrementAndClamp = 4
    | Invert = 5
    | IncrementAndWrap = 6
    | DecrementAndWrap = 7

type VkLogicOp = 
    | Clear = 0
    | And = 1
    | AndReverse = 2
    | Copy = 3
    | AndInverted = 4
    | NoOp = 5
    | Xor = 6
    | Or = 7
    | Nor = 8
    | Equivalent = 9
    | Invert = 10
    | OrReverse = 11
    | CopyInverted = 12
    | OrInverted = 13
    | Nand = 14
    | Set = 15

type VkInternalAllocationType = 
    | Executable = 0

type VkSystemAllocationScope = 
    | Command = 0
    | Object = 1
    | Cache = 2
    | Device = 3
    | Instance = 4

type VkPhysicalDeviceType = 
    | Other = 0
    | IntegratedGpu = 1
    | DiscreteGpu = 2
    | VirtualGpu = 3
    | Cpu = 4

type VkVertexInputRate = 
    | Vertex = 0
    | Instance = 1

type VkFormat = 
    | G16B16R163plane444Unorm = 1000156033
    | G16B16r162plane422Unorm = 1000156032
    | G16B16R163plane422Unorm = 1000156031
    | G16B16r162plane420Unorm = 1000156030
    | G16B16R163plane420Unorm = 1000156029
    | B16g16r16g16422Unorm = 1000156028
    | G16b16g16r16422Unorm = 1000156027
    | G12x4B12x4R12x43plane444Unorm3pack16 = 1000156026
    | G12x4B12x4r12x42plane422Unorm3pack16 = 1000156025
    | G12x4B12x4R12x43plane422Unorm3pack16 = 1000156024
    | G12x4B12x4r12x42plane420Unorm3pack16 = 1000156023
    | G12x4B12x4R12x43plane420Unorm3pack16 = 1000156022
    | B12x4g12x4r12x4g12x4422Unorm4pack16 = 1000156021
    | G12x4b12x4g12x4r12x4422Unorm4pack16 = 1000156020
    | R12x4g12x4b12x4a12x4Unorm4pack16 = 1000156019
    | R12x4g12x4Unorm2pack16 = 1000156018
    | R12x4UnormPack16 = 1000156017
    | G10x6B10x6R10x63plane444Unorm3pack16 = 1000156016
    | G10x6B10x6r10x62plane422Unorm3pack16 = 1000156015
    | G10x6B10x6R10x63plane422Unorm3pack16 = 1000156014
    | G10x6B10x6r10x62plane420Unorm3pack16 = 1000156013
    | G10x6B10x6R10x63plane420Unorm3pack16 = 1000156012
    | B10x6g10x6r10x6g10x6422Unorm4pack16 = 1000156011
    | G10x6b10x6g10x6r10x6422Unorm4pack16 = 1000156010
    | R10x6g10x6b10x6a10x6Unorm4pack16 = 1000156009
    | R10x6g10x6Unorm2pack16 = 1000156008
    | R10x6UnormPack16 = 1000156007
    | G8B8R83plane444Unorm = 1000156006
    | G8B8r82plane422Unorm = 1000156005
    | G8B8R83plane422Unorm = 1000156004
    | G8B8r82plane420Unorm = 1000156003
    | G8B8R83plane420Unorm = 1000156002
    | B8g8r8g8422Unorm = 1000156001
    | G8b8g8r8422Unorm = 1000156000
    | Undefined = 0
    | R4g4UnormPack8 = 1
    | R4g4b4a4UnormPack16 = 2
    | B4g4r4a4UnormPack16 = 3
    | R5g6b5UnormPack16 = 4
    | B5g6r5UnormPack16 = 5
    | R5g5b5a1UnormPack16 = 6
    | B5g5r5a1UnormPack16 = 7
    | A1r5g5b5UnormPack16 = 8
    | R8Unorm = 9
    | R8Snorm = 10
    | R8Uscaled = 11
    | R8Sscaled = 12
    | R8Uint = 13
    | R8Sint = 14
    | R8Srgb = 15
    | R8g8Unorm = 16
    | R8g8Snorm = 17
    | R8g8Uscaled = 18
    | R8g8Sscaled = 19
    | R8g8Uint = 20
    | R8g8Sint = 21
    | R8g8Srgb = 22
    | R8g8b8Unorm = 23
    | R8g8b8Snorm = 24
    | R8g8b8Uscaled = 25
    | R8g8b8Sscaled = 26
    | R8g8b8Uint = 27
    | R8g8b8Sint = 28
    | R8g8b8Srgb = 29
    | B8g8r8Unorm = 30
    | B8g8r8Snorm = 31
    | B8g8r8Uscaled = 32
    | B8g8r8Sscaled = 33
    | B8g8r8Uint = 34
    | B8g8r8Sint = 35
    | B8g8r8Srgb = 36
    | R8g8b8a8Unorm = 37
    | R8g8b8a8Snorm = 38
    | R8g8b8a8Uscaled = 39
    | R8g8b8a8Sscaled = 40
    | R8g8b8a8Uint = 41
    | R8g8b8a8Sint = 42
    | R8g8b8a8Srgb = 43
    | B8g8r8a8Unorm = 44
    | B8g8r8a8Snorm = 45
    | B8g8r8a8Uscaled = 46
    | B8g8r8a8Sscaled = 47
    | B8g8r8a8Uint = 48
    | B8g8r8a8Sint = 49
    | B8g8r8a8Srgb = 50
    | A8b8g8r8UnormPack32 = 51
    | A8b8g8r8SnormPack32 = 52
    | A8b8g8r8UscaledPack32 = 53
    | A8b8g8r8SscaledPack32 = 54
    | A8b8g8r8UintPack32 = 55
    | A8b8g8r8SintPack32 = 56
    | A8b8g8r8SrgbPack32 = 57
    | A2r10g10b10UnormPack32 = 58
    | A2r10g10b10SnormPack32 = 59
    | A2r10g10b10UscaledPack32 = 60
    | A2r10g10b10SscaledPack32 = 61
    | A2r10g10b10UintPack32 = 62
    | A2r10g10b10SintPack32 = 63
    | A2b10g10r10UnormPack32 = 64
    | A2b10g10r10SnormPack32 = 65
    | A2b10g10r10UscaledPack32 = 66
    | A2b10g10r10SscaledPack32 = 67
    | A2b10g10r10UintPack32 = 68
    | A2b10g10r10SintPack32 = 69
    | R16Unorm = 70
    | R16Snorm = 71
    | R16Uscaled = 72
    | R16Sscaled = 73
    | R16Uint = 74
    | R16Sint = 75
    | R16Sfloat = 76
    | R16g16Unorm = 77
    | R16g16Snorm = 78
    | R16g16Uscaled = 79
    | R16g16Sscaled = 80
    | R16g16Uint = 81
    | R16g16Sint = 82
    | R16g16Sfloat = 83
    | R16g16b16Unorm = 84
    | R16g16b16Snorm = 85
    | R16g16b16Uscaled = 86
    | R16g16b16Sscaled = 87
    | R16g16b16Uint = 88
    | R16g16b16Sint = 89
    | R16g16b16Sfloat = 90
    | R16g16b16a16Unorm = 91
    | R16g16b16a16Snorm = 92
    | R16g16b16a16Uscaled = 93
    | R16g16b16a16Sscaled = 94
    | R16g16b16a16Uint = 95
    | R16g16b16a16Sint = 96
    | R16g16b16a16Sfloat = 97
    | R32Uint = 98
    | R32Sint = 99
    | R32Sfloat = 100
    | R32g32Uint = 101
    | R32g32Sint = 102
    | R32g32Sfloat = 103
    | R32g32b32Uint = 104
    | R32g32b32Sint = 105
    | R32g32b32Sfloat = 106
    | R32g32b32a32Uint = 107
    | R32g32b32a32Sint = 108
    | R32g32b32a32Sfloat = 109
    | R64Uint = 110
    | R64Sint = 111
    | R64Sfloat = 112
    | R64g64Uint = 113
    | R64g64Sint = 114
    | R64g64Sfloat = 115
    | R64g64b64Uint = 116
    | R64g64b64Sint = 117
    | R64g64b64Sfloat = 118
    | R64g64b64a64Uint = 119
    | R64g64b64a64Sint = 120
    | R64g64b64a64Sfloat = 121
    | B10g11r11UfloatPack32 = 122
    | E5b9g9r9UfloatPack32 = 123
    | D16Unorm = 124
    | X8D24UnormPack32 = 125
    | D32Sfloat = 126
    | S8Uint = 127
    | D16UnormS8Uint = 128
    | D24UnormS8Uint = 129
    | D32SfloatS8Uint = 130
    | Bc1RgbUnormBlock = 131
    | Bc1RgbSrgbBlock = 132
    | Bc1RgbaUnormBlock = 133
    | Bc1RgbaSrgbBlock = 134
    | Bc2UnormBlock = 135
    | Bc2SrgbBlock = 136
    | Bc3UnormBlock = 137
    | Bc3SrgbBlock = 138
    | Bc4UnormBlock = 139
    | Bc4SnormBlock = 140
    | Bc5UnormBlock = 141
    | Bc5SnormBlock = 142
    | Bc6hUfloatBlock = 143
    | Bc6hSfloatBlock = 144
    | Bc7UnormBlock = 145
    | Bc7SrgbBlock = 146
    | Etc2R8g8b8UnormBlock = 147
    | Etc2R8g8b8SrgbBlock = 148
    | Etc2R8g8b8a1UnormBlock = 149
    | Etc2R8g8b8a1SrgbBlock = 150
    | Etc2R8g8b8a8UnormBlock = 151
    | Etc2R8g8b8a8SrgbBlock = 152
    | EacR11UnormBlock = 153
    | EacR11SnormBlock = 154
    | EacR11g11UnormBlock = 155
    | EacR11g11SnormBlock = 156
    | Astc44UnormBlock = 157
    | Astc44SrgbBlock = 158
    | Astc54UnormBlock = 159
    | Astc54SrgbBlock = 160
    | Astc55UnormBlock = 161
    | Astc55SrgbBlock = 162
    | Astc65UnormBlock = 163
    | Astc65SrgbBlock = 164
    | Astc66UnormBlock = 165
    | Astc66SrgbBlock = 166
    | Astc85UnormBlock = 167
    | Astc85SrgbBlock = 168
    | Astc86UnormBlock = 169
    | Astc86SrgbBlock = 170
    | Astc88UnormBlock = 171
    | Astc88SrgbBlock = 172
    | Astc105UnormBlock = 173
    | Astc105SrgbBlock = 174
    | Astc106UnormBlock = 175
    | Astc106SrgbBlock = 176
    | Astc108UnormBlock = 177
    | Astc108SrgbBlock = 178
    | Astc1010UnormBlock = 179
    | Astc1010SrgbBlock = 180
    | Astc1210UnormBlock = 181
    | Astc1210SrgbBlock = 182
    | Astc1212UnormBlock = 183
    | Astc1212SrgbBlock = 184

type VkStructureType = 
    | PhysicalDeviceShaderDrawParameterFeatures = 1000063000
    | DescriptorSetLayoutSupport = 1000168001
    | PhysicalDeviceMaintenance3Properties = 1000168000
    | ExternalSemaphoreProperties = 1000076001
    | PhysicalDeviceExternalSemaphoreInfo = 1000076000
    | ExportSemaphoreCreateInfo = 1000077000
    | ExportFenceCreateInfo = 1000113000
    | ExternalFenceProperties = 1000112001
    | PhysicalDeviceExternalFenceInfo = 1000112000
    | ExportMemoryAllocateInfo = 1000072002
    | ExternalMemoryImageCreateInfo = 1000072001
    | ExternalMemoryBufferCreateInfo = 1000072000
    | PhysicalDeviceIdProperties = 1000071004
    | ExternalBufferProperties = 1000071003
    | PhysicalDeviceExternalBufferInfo = 1000071002
    | ExternalImageFormatProperties = 1000071001
    | PhysicalDeviceExternalImageFormatInfo = 1000071000
    | DescriptorUpdateTemplateCreateInfo = 1000085000
    | SamplerYcbcrConversionImageFormatProperties = 1000156005
    | PhysicalDeviceSamplerYcbcrConversionFeatures = 1000156004
    | ImagePlaneMemoryRequirementsInfo = 1000156003
    | BindImagePlaneMemoryInfo = 1000156002
    | SamplerYcbcrConversionInfo = 1000156001
    | SamplerYcbcrConversionCreateInfo = 1000156000
    | DeviceQueueInfo2 = 1000145003
    | PhysicalDeviceProtectedMemoryProperties = 1000145002
    | PhysicalDeviceProtectedMemoryFeatures = 1000145001
    | ProtectedSubmitInfo = 1000145000
    | PhysicalDeviceVariablePointerFeatures = 1000120000
    | PhysicalDeviceMultiviewProperties = 1000053002
    | PhysicalDeviceMultiviewFeatures = 1000053001
    | RenderPassMultiviewCreateInfo = 1000053000
    | PipelineTessellationDomainOriginStateCreateInfo = 1000117003
    | ImageViewUsageCreateInfo = 1000117002
    | RenderPassInputAttachmentAspectCreateInfo = 1000117001
    | PhysicalDevicePointClippingProperties = 1000117000
    | PhysicalDeviceSparseImageFormatInfo2 = 1000059008
    | SparseImageFormatProperties2 = 1000059007
    | PhysicalDeviceMemoryProperties2 = 1000059006
    | QueueFamilyProperties2 = 1000059005
    | PhysicalDeviceImageFormatInfo2 = 1000059004
    | ImageFormatProperties2 = 1000059003
    | FormatProperties2 = 1000059002
    | PhysicalDeviceProperties2 = 1000059001
    | PhysicalDeviceFeatures2 = 1000059000
    | SparseImageMemoryRequirements2 = 1000146004
    | MemoryRequirements2 = 1000146003
    | ImageSparseMemoryRequirementsInfo2 = 1000146002
    | ImageMemoryRequirementsInfo2 = 1000146001
    | BufferMemoryRequirementsInfo2 = 1000146000
    | DeviceGroupDeviceCreateInfo = 1000070001
    | PhysicalDeviceGroupProperties = 1000070000
    | BindImageMemoryDeviceGroupInfo = 1000060014
    | BindBufferMemoryDeviceGroupInfo = 1000060013
    | DeviceGroupBindSparseInfo = 1000060006
    | DeviceGroupSubmitInfo = 1000060005
    | DeviceGroupCommandBufferBeginInfo = 1000060004
    | DeviceGroupRenderPassBeginInfo = 1000060003
    | MemoryAllocateFlagsInfo = 1000060000
    | MemoryDedicatedAllocateInfo = 1000127001
    | MemoryDedicatedRequirements = 1000127000
    | PhysicalDevice16bitStorageFeatures = 1000083000
    | BindImageMemoryInfo = 1000157001
    | BindBufferMemoryInfo = 1000157000
    | PhysicalDeviceSubgroupProperties = 1000094000
    | ApplicationInfo = 0
    | InstanceCreateInfo = 1
    | DeviceQueueCreateInfo = 2
    | DeviceCreateInfo = 3
    | SubmitInfo = 4
    | MemoryAllocateInfo = 5
    | MappedMemoryRange = 6
    | BindSparseInfo = 7
    | FenceCreateInfo = 8
    | SemaphoreCreateInfo = 9
    | EventCreateInfo = 10
    | QueryPoolCreateInfo = 11
    | BufferCreateInfo = 12
    | BufferViewCreateInfo = 13
    | ImageCreateInfo = 14
    | ImageViewCreateInfo = 15
    | ShaderModuleCreateInfo = 16
    | PipelineCacheCreateInfo = 17
    | PipelineShaderStageCreateInfo = 18
    | PipelineVertexInputStateCreateInfo = 19
    | PipelineInputAssemblyStateCreateInfo = 20
    | PipelineTessellationStateCreateInfo = 21
    | PipelineViewportStateCreateInfo = 22
    | PipelineRasterizationStateCreateInfo = 23
    | PipelineMultisampleStateCreateInfo = 24
    | PipelineDepthStencilStateCreateInfo = 25
    | PipelineColorBlendStateCreateInfo = 26
    | PipelineDynamicStateCreateInfo = 27
    | GraphicsPipelineCreateInfo = 28
    | ComputePipelineCreateInfo = 29
    | PipelineLayoutCreateInfo = 30
    | SamplerCreateInfo = 31
    | DescriptorSetLayoutCreateInfo = 32
    | DescriptorPoolCreateInfo = 33
    | DescriptorSetAllocateInfo = 34
    | WriteDescriptorSet = 35
    | CopyDescriptorSet = 36
    | FramebufferCreateInfo = 37
    | RenderPassCreateInfo = 38
    | CommandPoolCreateInfo = 39
    | CommandBufferAllocateInfo = 40
    | CommandBufferInheritanceInfo = 41
    | CommandBufferBeginInfo = 42
    | RenderPassBeginInfo = 43
    | BufferMemoryBarrier = 44
    | ImageMemoryBarrier = 45
    | MemoryBarrier = 46
    | LoaderInstanceCreateInfo = 47
    | LoaderDeviceCreateInfo = 48

type VkSubpassContents = 
    | Inline = 0
    | SecondaryCommandBuffers = 1

type VkResult = 
    | VkErrorInvalidExternalHandle = -1000072003
    | VkErrorOutOfPoolMemory = -1000069000
    | VkSuccess = 0
    | VkNotReady = 1
    | VkTimeout = 2
    | VkEventSet = 3
    | VkEventReset = 4
    | VkIncomplete = 5
    | VkErrorOutOfHostMemory = -1
    | VkErrorOutOfDeviceMemory = -2
    | VkErrorInitializationFailed = -3
    | VkErrorDeviceLost = -4
    | VkErrorMemoryMapFailed = -5
    | VkErrorLayerNotPresent = -6
    | VkErrorExtensionNotPresent = -7
    | VkErrorFeatureNotPresent = -8
    | VkErrorIncompatibleDriver = -9
    | VkErrorTooManyObjects = -10
    | VkErrorFormatNotSupported = -11
    | VkErrorFragmentedPool = -12

type VkDynamicState = 
    | Viewport = 0
    | Scissor = 1
    | LineWidth = 2
    | DepthBias = 3
    | BlendConstants = 4
    | DepthBounds = 5
    | StencilCompareMask = 6
    | StencilWriteMask = 7
    | StencilReference = 8

type VkDescriptorUpdateTemplateType = 
    | DescriptorSet = 0

type VkObjectType = 
    | DescriptorUpdateTemplate = 1000085000
    | SamplerYcbcrConversion = 1000156000
    | Unknown = 0
    | Instance = 1
    | PhysicalDevice = 2
    | Device = 3
    | Queue = 4
    | Semaphore = 5
    | CommandBuffer = 6
    | Fence = 7
    | DeviceMemory = 8
    | Buffer = 9
    | Image = 10
    | Event = 11
    | QueryPool = 12
    | BufferView = 13
    | ImageView = 14
    | ShaderModule = 15
    | PipelineCache = 16
    | PipelineLayout = 17
    | RenderPass = 18
    | Pipeline = 19
    | DescriptorSetLayout = 20
    | Sampler = 21
    | DescriptorPool = 22
    | DescriptorSet = 23
    | Framebuffer = 24
    | CommandPool = 25

[<Flags>]
type VkQueueFlags = 
    | None = 0
    | ProtectedBit = 0x00000010
    | GraphicsBit = 0x00000001
    | ComputeBit = 0x00000002
    | TransferBit = 0x00000004
    | SparseBindingBit = 0x00000008

[<Flags>]
type VkMemoryPropertyFlags = 
    | None = 0
    | ProtectedBit = 0x00000020
    | DeviceLocalBit = 0x00000001
    | HostVisibleBit = 0x00000002
    | HostCoherentBit = 0x00000004
    | HostCachedBit = 0x00000008
    | LazilyAllocatedBit = 0x00000010

[<Flags>]
type VkMemoryHeapFlags = 
    | None = 0
    | MultiInstanceBit = 0x00000002
    | DeviceLocalBit = 0x00000001

[<Flags>]
type VkAccessFlags = 
    | None = 0
    | IndirectCommandReadBit = 0x00000001
    | IndexReadBit = 0x00000002
    | VertexAttributeReadBit = 0x00000004
    | UniformReadBit = 0x00000008
    | InputAttachmentReadBit = 0x00000010
    | ShaderReadBit = 0x00000020
    | ShaderWriteBit = 0x00000040
    | ColorAttachmentReadBit = 0x00000080
    | ColorAttachmentWriteBit = 0x00000100
    | DepthStencilAttachmentReadBit = 0x00000200
    | DepthStencilAttachmentWriteBit = 0x00000400
    | TransferReadBit = 0x00000800
    | TransferWriteBit = 0x00001000
    | HostReadBit = 0x00002000
    | HostWriteBit = 0x00004000
    | MemoryReadBit = 0x00008000
    | MemoryWriteBit = 0x00010000

[<Flags>]
type VkBufferUsageFlags = 
    | None = 0
    | TransferSrcBit = 0x00000001
    | TransferDstBit = 0x00000002
    | UniformTexelBufferBit = 0x00000004
    | StorageTexelBufferBit = 0x00000008
    | UniformBufferBit = 0x00000010
    | StorageBufferBit = 0x00000020
    | IndexBufferBit = 0x00000040
    | VertexBufferBit = 0x00000080
    | IndirectBufferBit = 0x00000100

[<Flags>]
type VkBufferCreateFlags = 
    | None = 0
    | ProtectedBit = 0x00000008
    | SparseBindingBit = 0x00000001
    | SparseResidencyBit = 0x00000002
    | SparseAliasedBit = 0x00000004

[<Flags>]
type VkShaderStageFlags = 
    | None = 0
    | VertexBit = 0x00000001
    | TessellationControlBit = 0x00000002
    | TessellationEvaluationBit = 0x00000004
    | GeometryBit = 0x00000008
    | FragmentBit = 0x00000010
    | ComputeBit = 0x00000020
    | AllGraphics = 31
    | All = 2147483647

[<Flags>]
type VkImageUsageFlags = 
    | None = 0
    | TransferSrcBit = 0x00000001
    | TransferDstBit = 0x00000002
    | SampledBit = 0x00000004
    | StorageBit = 0x00000008
    | ColorAttachmentBit = 0x00000010
    | DepthStencilAttachmentBit = 0x00000020
    | TransientAttachmentBit = 0x00000040
    | InputAttachmentBit = 0x00000080

[<Flags>]
type VkImageCreateFlags = 
    | None = 0
    | DisjointBit = 0x00000200
    | ProtectedBit = 0x00000800
    | ExtendedUsageBit = 0x00000100
    | BlockTexelViewCompatibleBit = 0x00000080
    | D2dArrayCompatibleBit = 0x00000020
    | SplitInstanceBindRegionsBit = 0x00000040
    | AliasBit = 0x00000400
    | SparseBindingBit = 0x00000001
    | SparseResidencyBit = 0x00000002
    | SparseAliasedBit = 0x00000004
    | MutableFormatBit = 0x00000008
    | CubeCompatibleBit = 0x00000010

[<Flags>]
type VkPipelineCreateFlags = 
    | None = 0
    | DispatchBase = 0x00000010
    | ViewIndexFromDeviceIndexBit = 0x00000008
    | DisableOptimizationBit = 0x00000001
    | AllowDerivativesBit = 0x00000002
    | DerivativeBit = 0x00000004

[<Flags>]
type VkColorComponentFlags = 
    | None = 0
    | RBit = 0x00000001
    | GBit = 0x00000002
    | BBit = 0x00000004
    | ABit = 0x00000008

[<Flags>]
type VkFenceCreateFlags = 
    | None = 0
    | SignaledBit = 0x00000001

[<Flags>]
type VkFormatFeatureFlags = 
    | None = 0
    | CositedChromaSamplesBit = 0x00800000
    | DisjointBit = 0x00400000
    | SampledImageYcbcrConversionChromaReconstructionExplicitForceableBit = 0x00200000
    | SampledImageYcbcrConversionChromaReconstructionExplicitBit = 0x00100000
    | SampledImageYcbcrConversionSeparateReconstructionFilterBit = 0x00080000
    | SampledImageYcbcrConversionLinearFilterBit = 0x00040000
    | MidpointChromaSamplesBit = 0x00020000
    | TransferDstBit = 0x00008000
    | TransferSrcBit = 0x00004000
    | SampledImageBit = 0x00000001
    | StorageImageBit = 0x00000002
    | StorageImageAtomicBit = 0x00000004
    | UniformTexelBufferBit = 0x00000008
    | StorageTexelBufferBit = 0x00000010
    | StorageTexelBufferAtomicBit = 0x00000020
    | VertexBufferBit = 0x00000040
    | ColorAttachmentBit = 0x00000080
    | ColorAttachmentBlendBit = 0x00000100
    | DepthStencilAttachmentBit = 0x00000200
    | BlitSrcBit = 0x00000400
    | BlitDstBit = 0x00000800
    | SampledImageFilterLinearBit = 0x00001000

[<Flags>]
type VkQueryControlFlags = 
    | None = 0
    | PreciseBit = 0x00000001

[<Flags>]
type VkQueryResultFlags = 
    | None = 0
    | D64Bit = 0x00000001
    | WaitBit = 0x00000002
    | WithAvailabilityBit = 0x00000004
    | PartialBit = 0x00000008

[<Flags>]
type VkCommandBufferUsageFlags = 
    | None = 0
    | OneTimeSubmitBit = 0x00000001
    | RenderPassContinueBit = 0x00000002
    | SimultaneousUseBit = 0x00000004

[<Flags>]
type VkQueryPipelineStatisticFlags = 
    | None = 0
    | InputAssemblyVerticesBit = 0x00000001
    | InputAssemblyPrimitivesBit = 0x00000002
    | VertexShaderInvocationsBit = 0x00000004
    | GeometryShaderInvocationsBit = 0x00000008
    | GeometryShaderPrimitivesBit = 0x00000010
    | ClippingInvocationsBit = 0x00000020
    | ClippingPrimitivesBit = 0x00000040
    | FragmentShaderInvocationsBit = 0x00000080
    | TessellationControlShaderPatchesBit = 0x00000100
    | TessellationEvaluationShaderInvocationsBit = 0x00000200
    | ComputeShaderInvocationsBit = 0x00000400

[<Flags>]
type VkImageAspectFlags = 
    | None = 0
    | Plane2Bit = 0x00000040
    | Plane1Bit = 0x00000020
    | Plane0Bit = 0x00000010
    | ColorBit = 0x00000001
    | DepthBit = 0x00000002
    | StencilBit = 0x00000004
    | MetadataBit = 0x00000008

[<Flags>]
type VkSparseImageFormatFlags = 
    | None = 0
    | SingleMiptailBit = 0x00000001
    | AlignedMipSizeBit = 0x00000002
    | NonstandardBlockSizeBit = 0x00000004

[<Flags>]
type VkSparseMemoryBindFlags = 
    | None = 0
    | MetadataBit = 0x00000001

[<Flags>]
type VkPipelineStageFlags = 
    | None = 0
    | TopOfPipeBit = 0x00000001
    | DrawIndirectBit = 0x00000002
    | VertexInputBit = 0x00000004
    | VertexShaderBit = 0x00000008
    | TessellationControlShaderBit = 0x00000010
    | TessellationEvaluationShaderBit = 0x00000020
    | GeometryShaderBit = 0x00000040
    | FragmentShaderBit = 0x00000080
    | EarlyFragmentTestsBit = 0x00000100
    | LateFragmentTestsBit = 0x00000200
    | ColorAttachmentOutputBit = 0x00000400
    | ComputeShaderBit = 0x00000800
    | TransferBit = 0x00001000
    | BottomOfPipeBit = 0x00002000
    | HostBit = 0x00004000
    | AllGraphicsBit = 0x00008000
    | AllCommandsBit = 0x00010000

[<Flags>]
type VkCommandPoolCreateFlags = 
    | None = 0
    | ProtectedBit = 0x00000004
    | TransientBit = 0x00000001
    | ResetCommandBufferBit = 0x00000002

[<Flags>]
type VkCommandPoolResetFlags = 
    | None = 0
    | ReleaseResourcesBit = 0x00000001

[<Flags>]
type VkCommandBufferResetFlags = 
    | None = 0
    | ReleaseResourcesBit = 0x00000001

[<Flags>]
type VkSampleCountFlags = 
    | None = 0
    | D1Bit = 0x00000001
    | D2Bit = 0x00000002
    | D4Bit = 0x00000004
    | D8Bit = 0x00000008
    | D16Bit = 0x00000010
    | D32Bit = 0x00000020
    | D64Bit = 0x00000040

[<Flags>]
type VkAttachmentDescriptionFlags = 
    | None = 0
    | MayAliasBit = 0x00000001

[<Flags>]
type VkStencilFaceFlags = 
    | None = 0
    | FrontBit = 0x00000001
    | BackBit = 0x00000002
    | VkStencilFrontAndBack = 3

[<Flags>]
type VkDescriptorPoolCreateFlags = 
    | None = 0
    | FreeDescriptorSetBit = 0x00000001

[<Flags>]
type VkDependencyFlags = 
    | None = 0
    | ViewLocalBit = 0x00000002
    | DeviceGroupBit = 0x00000004
    | ByRegionBit = 0x00000001

type VkPresentModeKHR = 
    | VkPresentModeImmediateKhr = 0
    | VkPresentModeMailboxKhr = 1
    | VkPresentModeFifoKhr = 2
    | VkPresentModeFifoRelaxedKhr = 3

type VkColorSpaceKHR = 
    | VkColorSpaceSrgbNonlinearKhr = 0

[<Flags>]
type VkCompositeAlphaFlagsKHR = 
    | None = 0
    | VkCompositeAlphaOpaqueBitKhr = 0x00000001
    | VkCompositeAlphaPreMultipliedBitKhr = 0x00000002
    | VkCompositeAlphaPostMultipliedBitKhr = 0x00000004
    | VkCompositeAlphaInheritBitKhr = 0x00000008

[<Flags>]
type VkSurfaceTransformFlagsKHR = 
    | None = 0
    | VkSurfaceTransformIdentityBitKhr = 0x00000001
    | VkSurfaceTransformRotate90BitKhr = 0x00000002
    | VkSurfaceTransformRotate180BitKhr = 0x00000004
    | VkSurfaceTransformRotate270BitKhr = 0x00000008
    | VkSurfaceTransformHorizontalMirrorBitKhr = 0x00000010
    | VkSurfaceTransformHorizontalMirrorRotate90BitKhr = 0x00000020
    | VkSurfaceTransformHorizontalMirrorRotate180BitKhr = 0x00000040
    | VkSurfaceTransformHorizontalMirrorRotate270BitKhr = 0x00000080
    | VkSurfaceTransformInheritBitKhr = 0x00000100

[<Flags>]
type VkDebugReportFlagsEXT = 
    | None = 0
    | VkDebugReportInformationBitExt = 0x00000001
    | VkDebugReportWarningBitExt = 0x00000002
    | VkDebugReportPerformanceWarningBitExt = 0x00000004
    | VkDebugReportErrorBitExt = 0x00000008
    | VkDebugReportDebugBitExt = 0x00000010

type VkValidationCheckEXT = 
    | VkValidationCheckAllExt = 0
    | VkValidationCheckShadersExt = 1

[<Flags>]
type VkSubgroupFeatureFlags = 
    | None = 0
    | BasicBit = 0x00000001
    | VoteBit = 0x00000002
    | ArithmeticBit = 0x00000004
    | BallotBit = 0x00000008
    | ShuffleBit = 0x00000010
    | ShuffleRelativeBit = 0x00000020
    | ClusteredBit = 0x00000040
    | QuadBit = 0x00000080

[<Flags>]
type VkExternalMemoryHandleTypeFlags = 
    | None = 0
    | OpaqueFdBit = 0x00000001
    | OpaqueWin32Bit = 0x00000002
    | OpaqueWin32KmtBit = 0x00000004
    | D3d11TextureBit = 0x00000008
    | D3d11TextureKmtBit = 0x00000010
    | D3d12HeapBit = 0x00000020
    | D3d12ResourceBit = 0x00000040

[<Flags>]
type VkExternalMemoryFeatureFlags = 
    | None = 0
    | DedicatedOnlyBit = 0x00000001
    | ExportableBit = 0x00000002
    | ImportableBit = 0x00000004

[<Flags>]
type VkExternalSemaphoreHandleTypeFlags = 
    | None = 0
    | OpaqueFdBit = 0x00000001
    | OpaqueWin32Bit = 0x00000002
    | OpaqueWin32KmtBit = 0x00000004
    | D3d12FenceBit = 0x00000008
    | SyncFdBit = 0x00000010

[<Flags>]
type VkExternalSemaphoreFeatureFlags = 
    | None = 0
    | ExportableBit = 0x00000001
    | ImportableBit = 0x00000002

[<Flags>]
type VkSemaphoreImportFlags = 
    | None = 0
    | TemporaryBit = 0x00000001

[<Flags>]
type VkExternalFenceHandleTypeFlags = 
    | None = 0
    | OpaqueFdBit = 0x00000001
    | OpaqueWin32Bit = 0x00000002
    | OpaqueWin32KmtBit = 0x00000004
    | SyncFdBit = 0x00000008

[<Flags>]
type VkExternalFenceFeatureFlags = 
    | None = 0
    | ExportableBit = 0x00000001
    | ImportableBit = 0x00000002

[<Flags>]
type VkFenceImportFlags = 
    | None = 0
    | TemporaryBit = 0x00000001

[<Flags>]
type VkPeerMemoryFeatureFlags = 
    | None = 0
    | CopySrcBit = 0x00000001
    | CopyDstBit = 0x00000002
    | GenericSrcBit = 0x00000004
    | GenericDstBit = 0x00000008

[<Flags>]
type VkMemoryAllocateFlags = 
    | None = 0
    | DeviceMaskBit = 0x00000001

[<Flags>]
type VkDeviceGroupPresentModeFlagsKHR = 
    | None = 0
    | VkDeviceGroupPresentModeLocalBitKhr = 0x00000001
    | VkDeviceGroupPresentModeRemoteBitKhr = 0x00000002
    | VkDeviceGroupPresentModeSumBitKhr = 0x00000004
    | VkDeviceGroupPresentModeLocalMultiDeviceBitKhr = 0x00000008

type VkPointClippingBehavior = 
    | AllClipPlanes = 0
    | UserClipPlanesOnly = 1

type VkTessellationDomainOrigin = 
    | UpperLeft = 0
    | LowerLeft = 1

type VkSamplerYcbcrModelConversion = 
    | RgbIdentity = 0
    | YcbcrIdentity = 1
    | Ycbcr709 = 2
    | Ycbcr601 = 3
    | Ycbcr2020 = 4

type VkSamplerYcbcrRange = 
    | ItuFull = 0
    | ItuNarrow = 1

type VkChromaLocation = 
    | CositedEven = 0
    | Midpoint = 1

[<Flags>]
type VkDebugUtilsMessageSeverityFlagsEXT = 
    | None = 0
    | VkDebugUtilsMessageSeverityVerboseBitExt = 0x00000001
    | VkDebugUtilsMessageSeverityInfoBitExt = 0x00000010
    | VkDebugUtilsMessageSeverityWarningBitExt = 0x00000100
    | VkDebugUtilsMessageSeverityErrorBitExt = 0x00001000

[<Flags>]
type VkDebugUtilsMessageTypeFlagsEXT = 
    | None = 0
    | VkDebugUtilsMessageTypeGeneralBitExt = 0x00000001
    | VkDebugUtilsMessageTypeValidationBitExt = 0x00000002
    | VkDebugUtilsMessageTypePerformanceBitExt = 0x00000004

[<Flags>]
type VkDescriptorBindingFlagsEXT = 
    | None = 0
    | VkDescriptorBindingUpdateAfterBindBitExt = 0x00000001
    | VkDescriptorBindingUpdateUnusedWhilePendingBitExt = 0x00000002
    | VkDescriptorBindingPartiallyBoundBitExt = 0x00000004
    | VkDescriptorBindingVariableDescriptorCountBitExt = 0x00000008

[<StructLayout(LayoutKind.Explicit, Size = 128)>]
type uint32_32 =
    struct
        [<FieldOffset(0)>]
        val mutable public First : uint32
        
        member x.Item
            with get (i : int) : uint32 =
                if i < 0 || i > 31 then raise <| IndexOutOfRangeException()
                let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt
                NativePtr.get ptr i
            and set (i : int) (value : uint32) =
                if i < 0 || i > 31 then raise <| IndexOutOfRangeException()
                let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt
                NativePtr.set ptr i value

        member x.Length = 32

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = let x = x in (Seq.init 32 (fun i -> x.[i])).GetEnumerator() :> System.Collections.IEnumerator
        interface System.Collections.Generic.IEnumerable<uint32> with
            member x.GetEnumerator() = let x = x in (Seq.init 32 (fun i -> x.[i])).GetEnumerator()
    end

[<StructLayout(LayoutKind.Explicit, Size = 8)>]
type byte_8 =
    struct
        [<FieldOffset(0)>]
        val mutable public First : byte
        
        member x.Item
            with get (i : int) : byte =
                if i < 0 || i > 7 then raise <| IndexOutOfRangeException()
                let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt
                NativePtr.get ptr i
            and set (i : int) (value : byte) =
                if i < 0 || i > 7 then raise <| IndexOutOfRangeException()
                let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt
                NativePtr.set ptr i value

        member x.Length = 8

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = let x = x in (Seq.init 8 (fun i -> x.[i])).GetEnumerator() :> System.Collections.IEnumerator
        interface System.Collections.Generic.IEnumerable<byte> with
            member x.GetEnumerator() = let x = x in (Seq.init 8 (fun i -> x.[i])).GetEnumerator()
    end

[<StructLayout(LayoutKind.Explicit, Size = 256)>]
type VkPhysicalDevice_32 =
    struct
        [<FieldOffset(0)>]
        val mutable public First : VkPhysicalDevice
        
        member x.Item
            with get (i : int) : VkPhysicalDevice =
                if i < 0 || i > 31 then raise <| IndexOutOfRangeException()
                let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt
                NativePtr.get ptr i
            and set (i : int) (value : VkPhysicalDevice) =
                if i < 0 || i > 31 then raise <| IndexOutOfRangeException()
                let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt
                NativePtr.set ptr i value

        member x.Length = 32

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = let x = x in (Seq.init 32 (fun i -> x.[i])).GetEnumerator() :> System.Collections.IEnumerator
        interface System.Collections.Generic.IEnumerable<VkPhysicalDevice> with
            member x.GetEnumerator() = let x = x in (Seq.init 32 (fun i -> x.[i])).GetEnumerator()
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkAllocationCallbacks = 
    struct
        val mutable public pUserData : nativeint
        val mutable public pfnAllocation : PFN_vkAllocationFunction
        val mutable public pfnReallocation : PFN_vkReallocationFunction
        val mutable public pfnFree : PFN_vkFreeFunction
        val mutable public pfnInternalAllocation : PFN_vkInternalAllocationNotification
        val mutable public pfnInternalFree : PFN_vkInternalFreeNotification

        new(pUserData : nativeint
          , pfnAllocation : PFN_vkAllocationFunction
          , pfnReallocation : PFN_vkReallocationFunction
          , pfnFree : PFN_vkFreeFunction
          , pfnInternalAllocation : PFN_vkInternalAllocationNotification
          , pfnInternalFree : PFN_vkInternalFreeNotification
          ) =
            {
                pUserData = pUserData
                pfnAllocation = pfnAllocation
                pfnReallocation = pfnReallocation
                pfnFree = pfnFree
                pfnInternalAllocation = pfnInternalAllocation
                pfnInternalFree = pfnInternalFree
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "pUserData = %A" x.pUserData
                sprintf "pfnAllocation = %A" x.pfnAllocation
                sprintf "pfnReallocation = %A" x.pfnReallocation
                sprintf "pfnFree = %A" x.pfnFree
                sprintf "pfnInternalAllocation = %A" x.pfnInternalAllocation
                sprintf "pfnInternalFree = %A" x.pfnInternalFree
            ] |> sprintf "VkAllocationCallbacks { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkApplicationInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public pApplicationName : cstr
        val mutable public applicationVersion : uint32
        val mutable public pEngineName : cstr
        val mutable public engineVersion : uint32
        val mutable public apiVersion : uint32

        new(sType : VkStructureType
          , pNext : nativeint
          , pApplicationName : cstr
          , applicationVersion : uint32
          , pEngineName : cstr
          , engineVersion : uint32
          , apiVersion : uint32
          ) =
            {
                sType = sType
                pNext = pNext
                pApplicationName = pApplicationName
                applicationVersion = applicationVersion
                pEngineName = pEngineName
                engineVersion = engineVersion
                apiVersion = apiVersion
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "pApplicationName = %A" x.pApplicationName
                sprintf "applicationVersion = %A" x.applicationVersion
                sprintf "pEngineName = %A" x.pEngineName
                sprintf "engineVersion = %A" x.engineVersion
                sprintf "apiVersion = %A" x.apiVersion
            ] |> sprintf "VkApplicationInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkAttachmentDescription = 
    struct
        val mutable public flags : VkAttachmentDescriptionFlags
        val mutable public format : VkFormat
        val mutable public samples : VkSampleCountFlags
        val mutable public loadOp : VkAttachmentLoadOp
        val mutable public storeOp : VkAttachmentStoreOp
        val mutable public stencilLoadOp : VkAttachmentLoadOp
        val mutable public stencilStoreOp : VkAttachmentStoreOp
        val mutable public initialLayout : VkImageLayout
        val mutable public finalLayout : VkImageLayout

        new(flags : VkAttachmentDescriptionFlags
          , format : VkFormat
          , samples : VkSampleCountFlags
          , loadOp : VkAttachmentLoadOp
          , storeOp : VkAttachmentStoreOp
          , stencilLoadOp : VkAttachmentLoadOp
          , stencilStoreOp : VkAttachmentStoreOp
          , initialLayout : VkImageLayout
          , finalLayout : VkImageLayout
          ) =
            {
                flags = flags
                format = format
                samples = samples
                loadOp = loadOp
                storeOp = storeOp
                stencilLoadOp = stencilLoadOp
                stencilStoreOp = stencilStoreOp
                initialLayout = initialLayout
                finalLayout = finalLayout
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "flags = %A" x.flags
                sprintf "format = %A" x.format
                sprintf "samples = %A" x.samples
                sprintf "loadOp = %A" x.loadOp
                sprintf "storeOp = %A" x.storeOp
                sprintf "stencilLoadOp = %A" x.stencilLoadOp
                sprintf "stencilStoreOp = %A" x.stencilStoreOp
                sprintf "initialLayout = %A" x.initialLayout
                sprintf "finalLayout = %A" x.finalLayout
            ] |> sprintf "VkAttachmentDescription { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkAttachmentReference = 
    struct
        val mutable public attachment : uint32
        val mutable public layout : VkImageLayout

        new(attachment : uint32
          , layout : VkImageLayout
          ) =
            {
                attachment = attachment
                layout = layout
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "attachment = %A" x.attachment
                sprintf "layout = %A" x.layout
            ] |> sprintf "VkAttachmentReference { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBindBufferMemoryDeviceGroupInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public deviceIndexCount : uint32
        val mutable public pDeviceIndices : nativeptr<uint32>

        new(sType : VkStructureType
          , pNext : nativeint
          , deviceIndexCount : uint32
          , pDeviceIndices : nativeptr<uint32>
          ) =
            {
                sType = sType
                pNext = pNext
                deviceIndexCount = deviceIndexCount
                pDeviceIndices = pDeviceIndices
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "deviceIndexCount = %A" x.deviceIndexCount
                sprintf "pDeviceIndices = %A" x.pDeviceIndices
            ] |> sprintf "VkBindBufferMemoryDeviceGroupInfo { %s }"
    end

type VkBindBufferMemoryDeviceGroupInfoKHR = VkBindBufferMemoryDeviceGroupInfo
[<StructLayout(LayoutKind.Sequential)>]
type VkBindBufferMemoryInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public buffer : VkBuffer
        val mutable public memory : VkDeviceMemory
        val mutable public memoryOffset : VkDeviceSize

        new(sType : VkStructureType
          , pNext : nativeint
          , buffer : VkBuffer
          , memory : VkDeviceMemory
          , memoryOffset : VkDeviceSize
          ) =
            {
                sType = sType
                pNext = pNext
                buffer = buffer
                memory = memory
                memoryOffset = memoryOffset
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "buffer = %A" x.buffer
                sprintf "memory = %A" x.memory
                sprintf "memoryOffset = %A" x.memoryOffset
            ] |> sprintf "VkBindBufferMemoryInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkOffset2D = 
    struct
        val mutable public x : int
        val mutable public y : int

        new(x : int
          , y : int
          ) =
            {
                x = x
                y = y
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "x = %A" x.x
                sprintf "y = %A" x.y
            ] |> sprintf "VkOffset2D { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkExtent2D = 
    struct
        val mutable public width : uint32
        val mutable public height : uint32

        new(width : uint32
          , height : uint32
          ) =
            {
                width = width
                height = height
            }
        new(w : int, h : int) = VkExtent2D(uint32 w,uint32 h)
        override x.ToString() =
            String.concat "; " [
                sprintf "width = %A" x.width
                sprintf "height = %A" x.height
            ] |> sprintf "VkExtent2D { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkRect2D = 
    struct
        val mutable public offset : VkOffset2D
        val mutable public extent : VkExtent2D

        new(offset : VkOffset2D
          , extent : VkExtent2D
          ) =
            {
                offset = offset
                extent = extent
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "offset = %A" x.offset
                sprintf "extent = %A" x.extent
            ] |> sprintf "VkRect2D { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBindImageMemoryDeviceGroupInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public deviceIndexCount : uint32
        val mutable public pDeviceIndices : nativeptr<uint32>
        val mutable public splitInstanceBindRegionCount : uint32
        val mutable public pSplitInstanceBindRegions : nativeptr<VkRect2D>

        new(sType : VkStructureType
          , pNext : nativeint
          , deviceIndexCount : uint32
          , pDeviceIndices : nativeptr<uint32>
          , splitInstanceBindRegionCount : uint32
          , pSplitInstanceBindRegions : nativeptr<VkRect2D>
          ) =
            {
                sType = sType
                pNext = pNext
                deviceIndexCount = deviceIndexCount
                pDeviceIndices = pDeviceIndices
                splitInstanceBindRegionCount = splitInstanceBindRegionCount
                pSplitInstanceBindRegions = pSplitInstanceBindRegions
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "deviceIndexCount = %A" x.deviceIndexCount
                sprintf "pDeviceIndices = %A" x.pDeviceIndices
                sprintf "splitInstanceBindRegionCount = %A" x.splitInstanceBindRegionCount
                sprintf "pSplitInstanceBindRegions = %A" x.pSplitInstanceBindRegions
            ] |> sprintf "VkBindImageMemoryDeviceGroupInfo { %s }"
    end

type VkBindImageMemoryDeviceGroupInfoKHR = VkBindImageMemoryDeviceGroupInfo
[<StructLayout(LayoutKind.Sequential)>]
type VkBindImageMemoryInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public image : VkImage
        val mutable public memory : VkDeviceMemory
        val mutable public memoryOffset : VkDeviceSize

        new(sType : VkStructureType
          , pNext : nativeint
          , image : VkImage
          , memory : VkDeviceMemory
          , memoryOffset : VkDeviceSize
          ) =
            {
                sType = sType
                pNext = pNext
                image = image
                memory = memory
                memoryOffset = memoryOffset
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "image = %A" x.image
                sprintf "memory = %A" x.memory
                sprintf "memoryOffset = %A" x.memoryOffset
            ] |> sprintf "VkBindImageMemoryInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBindImagePlaneMemoryInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public planeAspect : VkImageAspectFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , planeAspect : VkImageAspectFlags
          ) =
            {
                sType = sType
                pNext = pNext
                planeAspect = planeAspect
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "planeAspect = %A" x.planeAspect
            ] |> sprintf "VkBindImagePlaneMemoryInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSparseMemoryBind = 
    struct
        val mutable public resourceOffset : VkDeviceSize
        val mutable public size : VkDeviceSize
        val mutable public memory : VkDeviceMemory
        val mutable public memoryOffset : VkDeviceSize
        val mutable public flags : VkSparseMemoryBindFlags

        new(resourceOffset : VkDeviceSize
          , size : VkDeviceSize
          , memory : VkDeviceMemory
          , memoryOffset : VkDeviceSize
          , flags : VkSparseMemoryBindFlags
          ) =
            {
                resourceOffset = resourceOffset
                size = size
                memory = memory
                memoryOffset = memoryOffset
                flags = flags
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "resourceOffset = %A" x.resourceOffset
                sprintf "size = %A" x.size
                sprintf "memory = %A" x.memory
                sprintf "memoryOffset = %A" x.memoryOffset
                sprintf "flags = %A" x.flags
            ] |> sprintf "VkSparseMemoryBind { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSparseBufferMemoryBindInfo = 
    struct
        val mutable public buffer : VkBuffer
        val mutable public bindCount : uint32
        val mutable public pBinds : nativeptr<VkSparseMemoryBind>

        new(buffer : VkBuffer
          , bindCount : uint32
          , pBinds : nativeptr<VkSparseMemoryBind>
          ) =
            {
                buffer = buffer
                bindCount = bindCount
                pBinds = pBinds
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "buffer = %A" x.buffer
                sprintf "bindCount = %A" x.bindCount
                sprintf "pBinds = %A" x.pBinds
            ] |> sprintf "VkSparseBufferMemoryBindInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSparseImageOpaqueMemoryBindInfo = 
    struct
        val mutable public image : VkImage
        val mutable public bindCount : uint32
        val mutable public pBinds : nativeptr<VkSparseMemoryBind>

        new(image : VkImage
          , bindCount : uint32
          , pBinds : nativeptr<VkSparseMemoryBind>
          ) =
            {
                image = image
                bindCount = bindCount
                pBinds = pBinds
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "image = %A" x.image
                sprintf "bindCount = %A" x.bindCount
                sprintf "pBinds = %A" x.pBinds
            ] |> sprintf "VkSparseImageOpaqueMemoryBindInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageSubresource = 
    struct
        val mutable public aspectMask : VkImageAspectFlags
        val mutable public mipLevel : uint32
        val mutable public arrayLayer : uint32

        new(aspectMask : VkImageAspectFlags
          , mipLevel : uint32
          , arrayLayer : uint32
          ) =
            {
                aspectMask = aspectMask
                mipLevel = mipLevel
                arrayLayer = arrayLayer
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "aspectMask = %A" x.aspectMask
                sprintf "mipLevel = %A" x.mipLevel
                sprintf "arrayLayer = %A" x.arrayLayer
            ] |> sprintf "VkImageSubresource { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkOffset3D = 
    struct
        val mutable public x : int
        val mutable public y : int
        val mutable public z : int

        new(x : int
          , y : int
          , z : int
          ) =
            {
                x = x
                y = y
                z = z
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "x = %A" x.x
                sprintf "y = %A" x.y
                sprintf "z = %A" x.z
            ] |> sprintf "VkOffset3D { %s }"
    end

[<StructLayout(LayoutKind.Explicit, Size = 24)>]
type VkOffset3D_2 =
    struct
        [<FieldOffset(0)>]
        val mutable public First : VkOffset3D
        
        member x.Item
            with get (i : int) : VkOffset3D =
                if i < 0 || i > 1 then raise <| IndexOutOfRangeException()
                let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt
                NativePtr.get ptr i
            and set (i : int) (value : VkOffset3D) =
                if i < 0 || i > 1 then raise <| IndexOutOfRangeException()
                let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt
                NativePtr.set ptr i value

        member x.Length = 2

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = let x = x in (Seq.init 2 (fun i -> x.[i])).GetEnumerator() :> System.Collections.IEnumerator
        interface System.Collections.Generic.IEnumerable<VkOffset3D> with
            member x.GetEnumerator() = let x = x in (Seq.init 2 (fun i -> x.[i])).GetEnumerator()
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkExtent3D = 
    struct
        val mutable public width : uint32
        val mutable public height : uint32
        val mutable public depth : uint32

        new(width : uint32
          , height : uint32
          , depth : uint32
          ) =
            {
                width = width
                height = height
                depth = depth
            }
        new(w : int, h : int, d : int) = VkExtent3D(uint32 w,uint32 h,uint32 d)
        override x.ToString() =
            String.concat "; " [
                sprintf "width = %A" x.width
                sprintf "height = %A" x.height
                sprintf "depth = %A" x.depth
            ] |> sprintf "VkExtent3D { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSparseImageMemoryBind = 
    struct
        val mutable public subresource : VkImageSubresource
        val mutable public offset : VkOffset3D
        val mutable public extent : VkExtent3D
        val mutable public memory : VkDeviceMemory
        val mutable public memoryOffset : VkDeviceSize
        val mutable public flags : VkSparseMemoryBindFlags

        new(subresource : VkImageSubresource
          , offset : VkOffset3D
          , extent : VkExtent3D
          , memory : VkDeviceMemory
          , memoryOffset : VkDeviceSize
          , flags : VkSparseMemoryBindFlags
          ) =
            {
                subresource = subresource
                offset = offset
                extent = extent
                memory = memory
                memoryOffset = memoryOffset
                flags = flags
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "subresource = %A" x.subresource
                sprintf "offset = %A" x.offset
                sprintf "extent = %A" x.extent
                sprintf "memory = %A" x.memory
                sprintf "memoryOffset = %A" x.memoryOffset
                sprintf "flags = %A" x.flags
            ] |> sprintf "VkSparseImageMemoryBind { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSparseImageMemoryBindInfo = 
    struct
        val mutable public image : VkImage
        val mutable public bindCount : uint32
        val mutable public pBinds : nativeptr<VkSparseImageMemoryBind>

        new(image : VkImage
          , bindCount : uint32
          , pBinds : nativeptr<VkSparseImageMemoryBind>
          ) =
            {
                image = image
                bindCount = bindCount
                pBinds = pBinds
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "image = %A" x.image
                sprintf "bindCount = %A" x.bindCount
                sprintf "pBinds = %A" x.pBinds
            ] |> sprintf "VkSparseImageMemoryBindInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBindSparseInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public waitSemaphoreCount : uint32
        val mutable public pWaitSemaphores : nativeptr<VkSemaphore>
        val mutable public bufferBindCount : uint32
        val mutable public pBufferBinds : nativeptr<VkSparseBufferMemoryBindInfo>
        val mutable public imageOpaqueBindCount : uint32
        val mutable public pImageOpaqueBinds : nativeptr<VkSparseImageOpaqueMemoryBindInfo>
        val mutable public imageBindCount : uint32
        val mutable public pImageBinds : nativeptr<VkSparseImageMemoryBindInfo>
        val mutable public signalSemaphoreCount : uint32
        val mutable public pSignalSemaphores : nativeptr<VkSemaphore>

        new(sType : VkStructureType
          , pNext : nativeint
          , waitSemaphoreCount : uint32
          , pWaitSemaphores : nativeptr<VkSemaphore>
          , bufferBindCount : uint32
          , pBufferBinds : nativeptr<VkSparseBufferMemoryBindInfo>
          , imageOpaqueBindCount : uint32
          , pImageOpaqueBinds : nativeptr<VkSparseImageOpaqueMemoryBindInfo>
          , imageBindCount : uint32
          , pImageBinds : nativeptr<VkSparseImageMemoryBindInfo>
          , signalSemaphoreCount : uint32
          , pSignalSemaphores : nativeptr<VkSemaphore>
          ) =
            {
                sType = sType
                pNext = pNext
                waitSemaphoreCount = waitSemaphoreCount
                pWaitSemaphores = pWaitSemaphores
                bufferBindCount = bufferBindCount
                pBufferBinds = pBufferBinds
                imageOpaqueBindCount = imageOpaqueBindCount
                pImageOpaqueBinds = pImageOpaqueBinds
                imageBindCount = imageBindCount
                pImageBinds = pImageBinds
                signalSemaphoreCount = signalSemaphoreCount
                pSignalSemaphores = pSignalSemaphores
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "waitSemaphoreCount = %A" x.waitSemaphoreCount
                sprintf "pWaitSemaphores = %A" x.pWaitSemaphores
                sprintf "bufferBindCount = %A" x.bufferBindCount
                sprintf "pBufferBinds = %A" x.pBufferBinds
                sprintf "imageOpaqueBindCount = %A" x.imageOpaqueBindCount
                sprintf "pImageOpaqueBinds = %A" x.pImageOpaqueBinds
                sprintf "imageBindCount = %A" x.imageBindCount
                sprintf "pImageBinds = %A" x.pImageBinds
                sprintf "signalSemaphoreCount = %A" x.signalSemaphoreCount
                sprintf "pSignalSemaphores = %A" x.pSignalSemaphores
            ] |> sprintf "VkBindSparseInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBufferCopy = 
    struct
        val mutable public srcOffset : VkDeviceSize
        val mutable public dstOffset : VkDeviceSize
        val mutable public size : VkDeviceSize

        new(srcOffset : VkDeviceSize
          , dstOffset : VkDeviceSize
          , size : VkDeviceSize
          ) =
            {
                srcOffset = srcOffset
                dstOffset = dstOffset
                size = size
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "srcOffset = %A" x.srcOffset
                sprintf "dstOffset = %A" x.dstOffset
                sprintf "size = %A" x.size
            ] |> sprintf "VkBufferCopy { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBufferCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkBufferCreateFlags
        val mutable public size : VkDeviceSize
        val mutable public usage : VkBufferUsageFlags
        val mutable public sharingMode : VkSharingMode
        val mutable public queueFamilyIndexCount : uint32
        val mutable public pQueueFamilyIndices : nativeptr<uint32>

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkBufferCreateFlags
          , size : VkDeviceSize
          , usage : VkBufferUsageFlags
          , sharingMode : VkSharingMode
          , queueFamilyIndexCount : uint32
          , pQueueFamilyIndices : nativeptr<uint32>
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                size = size
                usage = usage
                sharingMode = sharingMode
                queueFamilyIndexCount = queueFamilyIndexCount
                pQueueFamilyIndices = pQueueFamilyIndices
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "size = %A" x.size
                sprintf "usage = %A" x.usage
                sprintf "sharingMode = %A" x.sharingMode
                sprintf "queueFamilyIndexCount = %A" x.queueFamilyIndexCount
                sprintf "pQueueFamilyIndices = %A" x.pQueueFamilyIndices
            ] |> sprintf "VkBufferCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageSubresourceLayers = 
    struct
        val mutable public aspectMask : VkImageAspectFlags
        val mutable public mipLevel : uint32
        val mutable public baseArrayLayer : uint32
        val mutable public layerCount : uint32

        new(aspectMask : VkImageAspectFlags
          , mipLevel : uint32
          , baseArrayLayer : uint32
          , layerCount : uint32
          ) =
            {
                aspectMask = aspectMask
                mipLevel = mipLevel
                baseArrayLayer = baseArrayLayer
                layerCount = layerCount
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "aspectMask = %A" x.aspectMask
                sprintf "mipLevel = %A" x.mipLevel
                sprintf "baseArrayLayer = %A" x.baseArrayLayer
                sprintf "layerCount = %A" x.layerCount
            ] |> sprintf "VkImageSubresourceLayers { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBufferImageCopy = 
    struct
        val mutable public bufferOffset : VkDeviceSize
        val mutable public bufferRowLength : uint32
        val mutable public bufferImageHeight : uint32
        val mutable public imageSubresource : VkImageSubresourceLayers
        val mutable public imageOffset : VkOffset3D
        val mutable public imageExtent : VkExtent3D

        new(bufferOffset : VkDeviceSize
          , bufferRowLength : uint32
          , bufferImageHeight : uint32
          , imageSubresource : VkImageSubresourceLayers
          , imageOffset : VkOffset3D
          , imageExtent : VkExtent3D
          ) =
            {
                bufferOffset = bufferOffset
                bufferRowLength = bufferRowLength
                bufferImageHeight = bufferImageHeight
                imageSubresource = imageSubresource
                imageOffset = imageOffset
                imageExtent = imageExtent
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "bufferOffset = %A" x.bufferOffset
                sprintf "bufferRowLength = %A" x.bufferRowLength
                sprintf "bufferImageHeight = %A" x.bufferImageHeight
                sprintf "imageSubresource = %A" x.imageSubresource
                sprintf "imageOffset = %A" x.imageOffset
                sprintf "imageExtent = %A" x.imageExtent
            ] |> sprintf "VkBufferImageCopy { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBufferMemoryBarrier = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public srcAccessMask : VkAccessFlags
        val mutable public dstAccessMask : VkAccessFlags
        val mutable public srcQueueFamilyIndex : uint32
        val mutable public dstQueueFamilyIndex : uint32
        val mutable public buffer : VkBuffer
        val mutable public offset : VkDeviceSize
        val mutable public size : VkDeviceSize

        new(sType : VkStructureType
          , pNext : nativeint
          , srcAccessMask : VkAccessFlags
          , dstAccessMask : VkAccessFlags
          , srcQueueFamilyIndex : uint32
          , dstQueueFamilyIndex : uint32
          , buffer : VkBuffer
          , offset : VkDeviceSize
          , size : VkDeviceSize
          ) =
            {
                sType = sType
                pNext = pNext
                srcAccessMask = srcAccessMask
                dstAccessMask = dstAccessMask
                srcQueueFamilyIndex = srcQueueFamilyIndex
                dstQueueFamilyIndex = dstQueueFamilyIndex
                buffer = buffer
                offset = offset
                size = size
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "srcAccessMask = %A" x.srcAccessMask
                sprintf "dstAccessMask = %A" x.dstAccessMask
                sprintf "srcQueueFamilyIndex = %A" x.srcQueueFamilyIndex
                sprintf "dstQueueFamilyIndex = %A" x.dstQueueFamilyIndex
                sprintf "buffer = %A" x.buffer
                sprintf "offset = %A" x.offset
                sprintf "size = %A" x.size
            ] |> sprintf "VkBufferMemoryBarrier { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBufferMemoryRequirementsInfo2 = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public buffer : VkBuffer

        new(sType : VkStructureType
          , pNext : nativeint
          , buffer : VkBuffer
          ) =
            {
                sType = sType
                pNext = pNext
                buffer = buffer
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "buffer = %A" x.buffer
            ] |> sprintf "VkBufferMemoryRequirementsInfo2 { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBufferViewCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkBufferViewCreateFlags
        val mutable public buffer : VkBuffer
        val mutable public format : VkFormat
        val mutable public offset : VkDeviceSize
        val mutable public range : VkDeviceSize

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkBufferViewCreateFlags
          , buffer : VkBuffer
          , format : VkFormat
          , offset : VkDeviceSize
          , range : VkDeviceSize
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                buffer = buffer
                format = format
                offset = offset
                range = range
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "buffer = %A" x.buffer
                sprintf "format = %A" x.format
                sprintf "offset = %A" x.offset
                sprintf "range = %A" x.range
            ] |> sprintf "VkBufferViewCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Explicit)>]
type VkClearColorValue = 
    struct
        [<FieldOffset(0)>]
        val mutable public float32 : V4f
        [<FieldOffset(0)>]
        val mutable public int32 : V4i
        [<FieldOffset(0)>]
        val mutable public uint32 : V4ui
        override x.ToString() =
            String.concat "; " [
                sprintf "float32 = %A" x.float32
                sprintf "int32 = %A" x.int32
                sprintf "uint32 = %A" x.uint32
            ] |> sprintf "VkClearColorValue { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkClearDepthStencilValue = 
    struct
        val mutable public depth : float32
        val mutable public stencil : uint32

        new(depth : float32
          , stencil : uint32
          ) =
            {
                depth = depth
                stencil = stencil
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "depth = %A" x.depth
                sprintf "stencil = %A" x.stencil
            ] |> sprintf "VkClearDepthStencilValue { %s }"
    end

[<StructLayout(LayoutKind.Explicit)>]
type VkClearValue = 
    struct
        [<FieldOffset(0)>]
        val mutable public color : VkClearColorValue
        [<FieldOffset(0)>]
        val mutable public depthStencil : VkClearDepthStencilValue
        override x.ToString() =
            String.concat "; " [
                sprintf "color = %A" x.color
                sprintf "depthStencil = %A" x.depthStencil
            ] |> sprintf "VkClearValue { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkClearAttachment = 
    struct
        val mutable public aspectMask : VkImageAspectFlags
        val mutable public colorAttachment : uint32
        val mutable public clearValue : VkClearValue

        new(aspectMask : VkImageAspectFlags
          , colorAttachment : uint32
          , clearValue : VkClearValue
          ) =
            {
                aspectMask = aspectMask
                colorAttachment = colorAttachment
                clearValue = clearValue
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "aspectMask = %A" x.aspectMask
                sprintf "colorAttachment = %A" x.colorAttachment
                sprintf "clearValue = %A" x.clearValue
            ] |> sprintf "VkClearAttachment { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkClearRect = 
    struct
        val mutable public rect : VkRect2D
        val mutable public baseArrayLayer : uint32
        val mutable public layerCount : uint32

        new(rect : VkRect2D
          , baseArrayLayer : uint32
          , layerCount : uint32
          ) =
            {
                rect = rect
                baseArrayLayer = baseArrayLayer
                layerCount = layerCount
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "rect = %A" x.rect
                sprintf "baseArrayLayer = %A" x.baseArrayLayer
                sprintf "layerCount = %A" x.layerCount
            ] |> sprintf "VkClearRect { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkCommandBufferAllocateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public commandPool : VkCommandPool
        val mutable public level : VkCommandBufferLevel
        val mutable public commandBufferCount : uint32

        new(sType : VkStructureType
          , pNext : nativeint
          , commandPool : VkCommandPool
          , level : VkCommandBufferLevel
          , commandBufferCount : uint32
          ) =
            {
                sType = sType
                pNext = pNext
                commandPool = commandPool
                level = level
                commandBufferCount = commandBufferCount
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "commandPool = %A" x.commandPool
                sprintf "level = %A" x.level
                sprintf "commandBufferCount = %A" x.commandBufferCount
            ] |> sprintf "VkCommandBufferAllocateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkCommandBufferInheritanceInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public renderPass : VkRenderPass
        val mutable public subpass : uint32
        val mutable public framebuffer : VkFramebuffer
        val mutable public occlusionQueryEnable : VkBool32
        val mutable public queryFlags : VkQueryControlFlags
        val mutable public pipelineStatistics : VkQueryPipelineStatisticFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , renderPass : VkRenderPass
          , subpass : uint32
          , framebuffer : VkFramebuffer
          , occlusionQueryEnable : VkBool32
          , queryFlags : VkQueryControlFlags
          , pipelineStatistics : VkQueryPipelineStatisticFlags
          ) =
            {
                sType = sType
                pNext = pNext
                renderPass = renderPass
                subpass = subpass
                framebuffer = framebuffer
                occlusionQueryEnable = occlusionQueryEnable
                queryFlags = queryFlags
                pipelineStatistics = pipelineStatistics
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "renderPass = %A" x.renderPass
                sprintf "subpass = %A" x.subpass
                sprintf "framebuffer = %A" x.framebuffer
                sprintf "occlusionQueryEnable = %A" x.occlusionQueryEnable
                sprintf "queryFlags = %A" x.queryFlags
                sprintf "pipelineStatistics = %A" x.pipelineStatistics
            ] |> sprintf "VkCommandBufferInheritanceInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkCommandBufferBeginInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkCommandBufferUsageFlags
        val mutable public pInheritanceInfo : nativeptr<VkCommandBufferInheritanceInfo>

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkCommandBufferUsageFlags
          , pInheritanceInfo : nativeptr<VkCommandBufferInheritanceInfo>
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                pInheritanceInfo = pInheritanceInfo
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "pInheritanceInfo = %A" x.pInheritanceInfo
            ] |> sprintf "VkCommandBufferBeginInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkCommandPoolCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkCommandPoolCreateFlags
        val mutable public queueFamilyIndex : uint32

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkCommandPoolCreateFlags
          , queueFamilyIndex : uint32
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                queueFamilyIndex = queueFamilyIndex
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "queueFamilyIndex = %A" x.queueFamilyIndex
            ] |> sprintf "VkCommandPoolCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkComponentMapping = 
    struct
        val mutable public r : VkComponentSwizzle
        val mutable public g : VkComponentSwizzle
        val mutable public b : VkComponentSwizzle
        val mutable public a : VkComponentSwizzle

        new(r : VkComponentSwizzle
          , g : VkComponentSwizzle
          , b : VkComponentSwizzle
          , a : VkComponentSwizzle
          ) =
            {
                r = r
                g = g
                b = b
                a = a
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "r = %A" x.r
                sprintf "g = %A" x.g
                sprintf "b = %A" x.b
                sprintf "a = %A" x.a
            ] |> sprintf "VkComponentMapping { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSpecializationMapEntry = 
    struct
        val mutable public constantID : uint32
        val mutable public offset : uint32
        val mutable public size : uint64

        new(constantID : uint32
          , offset : uint32
          , size : uint64
          ) =
            {
                constantID = constantID
                offset = offset
                size = size
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "constantID = %A" x.constantID
                sprintf "offset = %A" x.offset
                sprintf "size = %A" x.size
            ] |> sprintf "VkSpecializationMapEntry { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSpecializationInfo = 
    struct
        val mutable public mapEntryCount : uint32
        val mutable public pMapEntries : nativeptr<VkSpecializationMapEntry>
        val mutable public dataSize : uint64
        val mutable public pData : nativeint

        new(mapEntryCount : uint32
          , pMapEntries : nativeptr<VkSpecializationMapEntry>
          , dataSize : uint64
          , pData : nativeint
          ) =
            {
                mapEntryCount = mapEntryCount
                pMapEntries = pMapEntries
                dataSize = dataSize
                pData = pData
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "mapEntryCount = %A" x.mapEntryCount
                sprintf "pMapEntries = %A" x.pMapEntries
                sprintf "dataSize = %A" x.dataSize
                sprintf "pData = %A" x.pData
            ] |> sprintf "VkSpecializationInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineShaderStageCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineShaderStageCreateFlags
        val mutable public stage : VkShaderStageFlags
        val mutable public _module : VkShaderModule
        val mutable public pName : cstr
        val mutable public pSpecializationInfo : nativeptr<VkSpecializationInfo>

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkPipelineShaderStageCreateFlags
          , stage : VkShaderStageFlags
          , _module : VkShaderModule
          , pName : cstr
          , pSpecializationInfo : nativeptr<VkSpecializationInfo>
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                stage = stage
                _module = _module
                pName = pName
                pSpecializationInfo = pSpecializationInfo
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "stage = %A" x.stage
                sprintf "_module = %A" x._module
                sprintf "pName = %A" x.pName
                sprintf "pSpecializationInfo = %A" x.pSpecializationInfo
            ] |> sprintf "VkPipelineShaderStageCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkComputePipelineCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineCreateFlags
        val mutable public stage : VkPipelineShaderStageCreateInfo
        val mutable public layout : VkPipelineLayout
        val mutable public basePipelineHandle : VkPipeline
        val mutable public basePipelineIndex : int

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkPipelineCreateFlags
          , stage : VkPipelineShaderStageCreateInfo
          , layout : VkPipelineLayout
          , basePipelineHandle : VkPipeline
          , basePipelineIndex : int
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                stage = stage
                layout = layout
                basePipelineHandle = basePipelineHandle
                basePipelineIndex = basePipelineIndex
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "stage = %A" x.stage
                sprintf "layout = %A" x.layout
                sprintf "basePipelineHandle = %A" x.basePipelineHandle
                sprintf "basePipelineIndex = %A" x.basePipelineIndex
            ] |> sprintf "VkComputePipelineCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkCopyDescriptorSet = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public srcSet : VkDescriptorSet
        val mutable public srcBinding : uint32
        val mutable public srcArrayElement : uint32
        val mutable public dstSet : VkDescriptorSet
        val mutable public dstBinding : uint32
        val mutable public dstArrayElement : uint32
        val mutable public descriptorCount : uint32

        new(sType : VkStructureType
          , pNext : nativeint
          , srcSet : VkDescriptorSet
          , srcBinding : uint32
          , srcArrayElement : uint32
          , dstSet : VkDescriptorSet
          , dstBinding : uint32
          , dstArrayElement : uint32
          , descriptorCount : uint32
          ) =
            {
                sType = sType
                pNext = pNext
                srcSet = srcSet
                srcBinding = srcBinding
                srcArrayElement = srcArrayElement
                dstSet = dstSet
                dstBinding = dstBinding
                dstArrayElement = dstArrayElement
                descriptorCount = descriptorCount
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "srcSet = %A" x.srcSet
                sprintf "srcBinding = %A" x.srcBinding
                sprintf "srcArrayElement = %A" x.srcArrayElement
                sprintf "dstSet = %A" x.dstSet
                sprintf "dstBinding = %A" x.dstBinding
                sprintf "dstArrayElement = %A" x.dstArrayElement
                sprintf "descriptorCount = %A" x.descriptorCount
            ] |> sprintf "VkCopyDescriptorSet { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorBufferInfo = 
    struct
        val mutable public buffer : VkBuffer
        val mutable public offset : VkDeviceSize
        val mutable public range : VkDeviceSize

        new(buffer : VkBuffer
          , offset : VkDeviceSize
          , range : VkDeviceSize
          ) =
            {
                buffer = buffer
                offset = offset
                range = range
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "buffer = %A" x.buffer
                sprintf "offset = %A" x.offset
                sprintf "range = %A" x.range
            ] |> sprintf "VkDescriptorBufferInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorImageInfo = 
    struct
        val mutable public sampler : VkSampler
        val mutable public imageView : VkImageView
        val mutable public imageLayout : VkImageLayout

        new(sampler : VkSampler
          , imageView : VkImageView
          , imageLayout : VkImageLayout
          ) =
            {
                sampler = sampler
                imageView = imageView
                imageLayout = imageLayout
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sampler = %A" x.sampler
                sprintf "imageView = %A" x.imageView
                sprintf "imageLayout = %A" x.imageLayout
            ] |> sprintf "VkDescriptorImageInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorPoolSize = 
    struct
        val mutable public _type : VkDescriptorType
        val mutable public descriptorCount : uint32

        new(_type : VkDescriptorType
          , descriptorCount : uint32
          ) =
            {
                _type = _type
                descriptorCount = descriptorCount
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "_type = %A" x._type
                sprintf "descriptorCount = %A" x.descriptorCount
            ] |> sprintf "VkDescriptorPoolSize { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorPoolCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkDescriptorPoolCreateFlags
        val mutable public maxSets : uint32
        val mutable public poolSizeCount : uint32
        val mutable public pPoolSizes : nativeptr<VkDescriptorPoolSize>

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkDescriptorPoolCreateFlags
          , maxSets : uint32
          , poolSizeCount : uint32
          , pPoolSizes : nativeptr<VkDescriptorPoolSize>
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                maxSets = maxSets
                poolSizeCount = poolSizeCount
                pPoolSizes = pPoolSizes
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "maxSets = %A" x.maxSets
                sprintf "poolSizeCount = %A" x.poolSizeCount
                sprintf "pPoolSizes = %A" x.pPoolSizes
            ] |> sprintf "VkDescriptorPoolCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorSetAllocateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public descriptorPool : VkDescriptorPool
        val mutable public descriptorSetCount : uint32
        val mutable public pSetLayouts : nativeptr<VkDescriptorSetLayout>

        new(sType : VkStructureType
          , pNext : nativeint
          , descriptorPool : VkDescriptorPool
          , descriptorSetCount : uint32
          , pSetLayouts : nativeptr<VkDescriptorSetLayout>
          ) =
            {
                sType = sType
                pNext = pNext
                descriptorPool = descriptorPool
                descriptorSetCount = descriptorSetCount
                pSetLayouts = pSetLayouts
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "descriptorPool = %A" x.descriptorPool
                sprintf "descriptorSetCount = %A" x.descriptorSetCount
                sprintf "pSetLayouts = %A" x.pSetLayouts
            ] |> sprintf "VkDescriptorSetAllocateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorSetLayoutBinding = 
    struct
        val mutable public binding : uint32
        val mutable public descriptorType : VkDescriptorType
        val mutable public descriptorCount : uint32
        val mutable public stageFlags : VkShaderStageFlags
        val mutable public pImmutableSamplers : nativeptr<VkSampler>

        new(binding : uint32
          , descriptorType : VkDescriptorType
          , descriptorCount : uint32
          , stageFlags : VkShaderStageFlags
          , pImmutableSamplers : nativeptr<VkSampler>
          ) =
            {
                binding = binding
                descriptorType = descriptorType
                descriptorCount = descriptorCount
                stageFlags = stageFlags
                pImmutableSamplers = pImmutableSamplers
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "binding = %A" x.binding
                sprintf "descriptorType = %A" x.descriptorType
                sprintf "descriptorCount = %A" x.descriptorCount
                sprintf "stageFlags = %A" x.stageFlags
                sprintf "pImmutableSamplers = %A" x.pImmutableSamplers
            ] |> sprintf "VkDescriptorSetLayoutBinding { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorSetLayoutCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkDescriptorSetLayoutCreateFlags
        val mutable public bindingCount : uint32
        val mutable public pBindings : nativeptr<VkDescriptorSetLayoutBinding>

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkDescriptorSetLayoutCreateFlags
          , bindingCount : uint32
          , pBindings : nativeptr<VkDescriptorSetLayoutBinding>
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                bindingCount = bindingCount
                pBindings = pBindings
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "bindingCount = %A" x.bindingCount
                sprintf "pBindings = %A" x.pBindings
            ] |> sprintf "VkDescriptorSetLayoutCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorSetLayoutSupport = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public supported : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , supported : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                supported = supported
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "supported = %A" x.supported
            ] |> sprintf "VkDescriptorSetLayoutSupport { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorUpdateTemplateEntry = 
    struct
        val mutable public dstBinding : uint32
        val mutable public dstArrayElement : uint32
        val mutable public descriptorCount : uint32
        val mutable public descriptorType : VkDescriptorType
        val mutable public offset : uint64
        val mutable public stride : uint64

        new(dstBinding : uint32
          , dstArrayElement : uint32
          , descriptorCount : uint32
          , descriptorType : VkDescriptorType
          , offset : uint64
          , stride : uint64
          ) =
            {
                dstBinding = dstBinding
                dstArrayElement = dstArrayElement
                descriptorCount = descriptorCount
                descriptorType = descriptorType
                offset = offset
                stride = stride
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "dstBinding = %A" x.dstBinding
                sprintf "dstArrayElement = %A" x.dstArrayElement
                sprintf "descriptorCount = %A" x.descriptorCount
                sprintf "descriptorType = %A" x.descriptorType
                sprintf "offset = %A" x.offset
                sprintf "stride = %A" x.stride
            ] |> sprintf "VkDescriptorUpdateTemplateEntry { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorUpdateTemplateCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkDescriptorUpdateTemplateCreateFlags
        val mutable public descriptorUpdateEntryCount : uint32
        val mutable public pDescriptorUpdateEntries : nativeptr<VkDescriptorUpdateTemplateEntry>
        val mutable public templateType : VkDescriptorUpdateTemplateType
        val mutable public descriptorSetLayout : VkDescriptorSetLayout
        val mutable public pipelineBindPoint : VkPipelineBindPoint
        val mutable public pipelineLayout : VkPipelineLayout
        val mutable public set : uint32

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkDescriptorUpdateTemplateCreateFlags
          , descriptorUpdateEntryCount : uint32
          , pDescriptorUpdateEntries : nativeptr<VkDescriptorUpdateTemplateEntry>
          , templateType : VkDescriptorUpdateTemplateType
          , descriptorSetLayout : VkDescriptorSetLayout
          , pipelineBindPoint : VkPipelineBindPoint
          , pipelineLayout : VkPipelineLayout
          , set : uint32
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                descriptorUpdateEntryCount = descriptorUpdateEntryCount
                pDescriptorUpdateEntries = pDescriptorUpdateEntries
                templateType = templateType
                descriptorSetLayout = descriptorSetLayout
                pipelineBindPoint = pipelineBindPoint
                pipelineLayout = pipelineLayout
                set = set
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "descriptorUpdateEntryCount = %A" x.descriptorUpdateEntryCount
                sprintf "pDescriptorUpdateEntries = %A" x.pDescriptorUpdateEntries
                sprintf "templateType = %A" x.templateType
                sprintf "descriptorSetLayout = %A" x.descriptorSetLayout
                sprintf "pipelineBindPoint = %A" x.pipelineBindPoint
                sprintf "pipelineLayout = %A" x.pipelineLayout
                sprintf "set = %A" x.set
            ] |> sprintf "VkDescriptorUpdateTemplateCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDeviceQueueCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkDeviceQueueCreateFlags
        val mutable public queueFamilyIndex : uint32
        val mutable public queueCount : uint32
        val mutable public pQueuePriorities : nativeptr<float32>

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkDeviceQueueCreateFlags
          , queueFamilyIndex : uint32
          , queueCount : uint32
          , pQueuePriorities : nativeptr<float32>
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                queueFamilyIndex = queueFamilyIndex
                queueCount = queueCount
                pQueuePriorities = pQueuePriorities
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "queueFamilyIndex = %A" x.queueFamilyIndex
                sprintf "queueCount = %A" x.queueCount
                sprintf "pQueuePriorities = %A" x.pQueuePriorities
            ] |> sprintf "VkDeviceQueueCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceFeatures = 
    struct
        val mutable public robustBufferAccess : VkBool32
        val mutable public fullDrawIndexUint32 : VkBool32
        val mutable public imageCubeArray : VkBool32
        val mutable public independentBlend : VkBool32
        val mutable public geometryShader : VkBool32
        val mutable public tessellationShader : VkBool32
        val mutable public sampleRateShading : VkBool32
        val mutable public dualSrcBlend : VkBool32
        val mutable public logicOp : VkBool32
        val mutable public multiDrawIndirect : VkBool32
        val mutable public drawIndirectFirstInstance : VkBool32
        val mutable public depthClamp : VkBool32
        val mutable public depthBiasClamp : VkBool32
        val mutable public fillModeNonSolid : VkBool32
        val mutable public depthBounds : VkBool32
        val mutable public wideLines : VkBool32
        val mutable public largePoints : VkBool32
        val mutable public alphaToOne : VkBool32
        val mutable public multiViewport : VkBool32
        val mutable public samplerAnisotropy : VkBool32
        val mutable public textureCompressionETC2 : VkBool32
        val mutable public textureCompressionASTC_LDR : VkBool32
        val mutable public textureCompressionBC : VkBool32
        val mutable public occlusionQueryPrecise : VkBool32
        val mutable public pipelineStatisticsQuery : VkBool32
        val mutable public vertexPipelineStoresAndAtomics : VkBool32
        val mutable public fragmentStoresAndAtomics : VkBool32
        val mutable public shaderTessellationAndGeometryPointSize : VkBool32
        val mutable public shaderImageGatherExtended : VkBool32
        val mutable public shaderStorageImageExtendedFormats : VkBool32
        val mutable public shaderStorageImageMultisample : VkBool32
        val mutable public shaderStorageImageReadWithoutFormat : VkBool32
        val mutable public shaderStorageImageWriteWithoutFormat : VkBool32
        val mutable public shaderUniformBufferArrayDynamicIndexing : VkBool32
        val mutable public shaderSampledImageArrayDynamicIndexing : VkBool32
        val mutable public shaderStorageBufferArrayDynamicIndexing : VkBool32
        val mutable public shaderStorageImageArrayDynamicIndexing : VkBool32
        val mutable public shaderClipDistance : VkBool32
        val mutable public shaderCullDistance : VkBool32
        val mutable public shaderFloat64 : VkBool32
        val mutable public shaderInt64 : VkBool32
        val mutable public shaderInt16 : VkBool32
        val mutable public shaderResourceResidency : VkBool32
        val mutable public shaderResourceMinLod : VkBool32
        val mutable public sparseBinding : VkBool32
        val mutable public sparseResidencyBuffer : VkBool32
        val mutable public sparseResidencyImage2D : VkBool32
        val mutable public sparseResidencyImage3D : VkBool32
        val mutable public sparseResidency2Samples : VkBool32
        val mutable public sparseResidency4Samples : VkBool32
        val mutable public sparseResidency8Samples : VkBool32
        val mutable public sparseResidency16Samples : VkBool32
        val mutable public sparseResidencyAliased : VkBool32
        val mutable public variableMultisampleRate : VkBool32
        val mutable public inheritedQueries : VkBool32

        new(robustBufferAccess : VkBool32
          , fullDrawIndexUint32 : VkBool32
          , imageCubeArray : VkBool32
          , independentBlend : VkBool32
          , geometryShader : VkBool32
          , tessellationShader : VkBool32
          , sampleRateShading : VkBool32
          , dualSrcBlend : VkBool32
          , logicOp : VkBool32
          , multiDrawIndirect : VkBool32
          , drawIndirectFirstInstance : VkBool32
          , depthClamp : VkBool32
          , depthBiasClamp : VkBool32
          , fillModeNonSolid : VkBool32
          , depthBounds : VkBool32
          , wideLines : VkBool32
          , largePoints : VkBool32
          , alphaToOne : VkBool32
          , multiViewport : VkBool32
          , samplerAnisotropy : VkBool32
          , textureCompressionETC2 : VkBool32
          , textureCompressionASTC_LDR : VkBool32
          , textureCompressionBC : VkBool32
          , occlusionQueryPrecise : VkBool32
          , pipelineStatisticsQuery : VkBool32
          , vertexPipelineStoresAndAtomics : VkBool32
          , fragmentStoresAndAtomics : VkBool32
          , shaderTessellationAndGeometryPointSize : VkBool32
          , shaderImageGatherExtended : VkBool32
          , shaderStorageImageExtendedFormats : VkBool32
          , shaderStorageImageMultisample : VkBool32
          , shaderStorageImageReadWithoutFormat : VkBool32
          , shaderStorageImageWriteWithoutFormat : VkBool32
          , shaderUniformBufferArrayDynamicIndexing : VkBool32
          , shaderSampledImageArrayDynamicIndexing : VkBool32
          , shaderStorageBufferArrayDynamicIndexing : VkBool32
          , shaderStorageImageArrayDynamicIndexing : VkBool32
          , shaderClipDistance : VkBool32
          , shaderCullDistance : VkBool32
          , shaderFloat64 : VkBool32
          , shaderInt64 : VkBool32
          , shaderInt16 : VkBool32
          , shaderResourceResidency : VkBool32
          , shaderResourceMinLod : VkBool32
          , sparseBinding : VkBool32
          , sparseResidencyBuffer : VkBool32
          , sparseResidencyImage2D : VkBool32
          , sparseResidencyImage3D : VkBool32
          , sparseResidency2Samples : VkBool32
          , sparseResidency4Samples : VkBool32
          , sparseResidency8Samples : VkBool32
          , sparseResidency16Samples : VkBool32
          , sparseResidencyAliased : VkBool32
          , variableMultisampleRate : VkBool32
          , inheritedQueries : VkBool32
          ) =
            {
                robustBufferAccess = robustBufferAccess
                fullDrawIndexUint32 = fullDrawIndexUint32
                imageCubeArray = imageCubeArray
                independentBlend = independentBlend
                geometryShader = geometryShader
                tessellationShader = tessellationShader
                sampleRateShading = sampleRateShading
                dualSrcBlend = dualSrcBlend
                logicOp = logicOp
                multiDrawIndirect = multiDrawIndirect
                drawIndirectFirstInstance = drawIndirectFirstInstance
                depthClamp = depthClamp
                depthBiasClamp = depthBiasClamp
                fillModeNonSolid = fillModeNonSolid
                depthBounds = depthBounds
                wideLines = wideLines
                largePoints = largePoints
                alphaToOne = alphaToOne
                multiViewport = multiViewport
                samplerAnisotropy = samplerAnisotropy
                textureCompressionETC2 = textureCompressionETC2
                textureCompressionASTC_LDR = textureCompressionASTC_LDR
                textureCompressionBC = textureCompressionBC
                occlusionQueryPrecise = occlusionQueryPrecise
                pipelineStatisticsQuery = pipelineStatisticsQuery
                vertexPipelineStoresAndAtomics = vertexPipelineStoresAndAtomics
                fragmentStoresAndAtomics = fragmentStoresAndAtomics
                shaderTessellationAndGeometryPointSize = shaderTessellationAndGeometryPointSize
                shaderImageGatherExtended = shaderImageGatherExtended
                shaderStorageImageExtendedFormats = shaderStorageImageExtendedFormats
                shaderStorageImageMultisample = shaderStorageImageMultisample
                shaderStorageImageReadWithoutFormat = shaderStorageImageReadWithoutFormat
                shaderStorageImageWriteWithoutFormat = shaderStorageImageWriteWithoutFormat
                shaderUniformBufferArrayDynamicIndexing = shaderUniformBufferArrayDynamicIndexing
                shaderSampledImageArrayDynamicIndexing = shaderSampledImageArrayDynamicIndexing
                shaderStorageBufferArrayDynamicIndexing = shaderStorageBufferArrayDynamicIndexing
                shaderStorageImageArrayDynamicIndexing = shaderStorageImageArrayDynamicIndexing
                shaderClipDistance = shaderClipDistance
                shaderCullDistance = shaderCullDistance
                shaderFloat64 = shaderFloat64
                shaderInt64 = shaderInt64
                shaderInt16 = shaderInt16
                shaderResourceResidency = shaderResourceResidency
                shaderResourceMinLod = shaderResourceMinLod
                sparseBinding = sparseBinding
                sparseResidencyBuffer = sparseResidencyBuffer
                sparseResidencyImage2D = sparseResidencyImage2D
                sparseResidencyImage3D = sparseResidencyImage3D
                sparseResidency2Samples = sparseResidency2Samples
                sparseResidency4Samples = sparseResidency4Samples
                sparseResidency8Samples = sparseResidency8Samples
                sparseResidency16Samples = sparseResidency16Samples
                sparseResidencyAliased = sparseResidencyAliased
                variableMultisampleRate = variableMultisampleRate
                inheritedQueries = inheritedQueries
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "robustBufferAccess = %A" x.robustBufferAccess
                sprintf "fullDrawIndexUint32 = %A" x.fullDrawIndexUint32
                sprintf "imageCubeArray = %A" x.imageCubeArray
                sprintf "independentBlend = %A" x.independentBlend
                sprintf "geometryShader = %A" x.geometryShader
                sprintf "tessellationShader = %A" x.tessellationShader
                sprintf "sampleRateShading = %A" x.sampleRateShading
                sprintf "dualSrcBlend = %A" x.dualSrcBlend
                sprintf "logicOp = %A" x.logicOp
                sprintf "multiDrawIndirect = %A" x.multiDrawIndirect
                sprintf "drawIndirectFirstInstance = %A" x.drawIndirectFirstInstance
                sprintf "depthClamp = %A" x.depthClamp
                sprintf "depthBiasClamp = %A" x.depthBiasClamp
                sprintf "fillModeNonSolid = %A" x.fillModeNonSolid
                sprintf "depthBounds = %A" x.depthBounds
                sprintf "wideLines = %A" x.wideLines
                sprintf "largePoints = %A" x.largePoints
                sprintf "alphaToOne = %A" x.alphaToOne
                sprintf "multiViewport = %A" x.multiViewport
                sprintf "samplerAnisotropy = %A" x.samplerAnisotropy
                sprintf "textureCompressionETC2 = %A" x.textureCompressionETC2
                sprintf "textureCompressionASTC_LDR = %A" x.textureCompressionASTC_LDR
                sprintf "textureCompressionBC = %A" x.textureCompressionBC
                sprintf "occlusionQueryPrecise = %A" x.occlusionQueryPrecise
                sprintf "pipelineStatisticsQuery = %A" x.pipelineStatisticsQuery
                sprintf "vertexPipelineStoresAndAtomics = %A" x.vertexPipelineStoresAndAtomics
                sprintf "fragmentStoresAndAtomics = %A" x.fragmentStoresAndAtomics
                sprintf "shaderTessellationAndGeometryPointSize = %A" x.shaderTessellationAndGeometryPointSize
                sprintf "shaderImageGatherExtended = %A" x.shaderImageGatherExtended
                sprintf "shaderStorageImageExtendedFormats = %A" x.shaderStorageImageExtendedFormats
                sprintf "shaderStorageImageMultisample = %A" x.shaderStorageImageMultisample
                sprintf "shaderStorageImageReadWithoutFormat = %A" x.shaderStorageImageReadWithoutFormat
                sprintf "shaderStorageImageWriteWithoutFormat = %A" x.shaderStorageImageWriteWithoutFormat
                sprintf "shaderUniformBufferArrayDynamicIndexing = %A" x.shaderUniformBufferArrayDynamicIndexing
                sprintf "shaderSampledImageArrayDynamicIndexing = %A" x.shaderSampledImageArrayDynamicIndexing
                sprintf "shaderStorageBufferArrayDynamicIndexing = %A" x.shaderStorageBufferArrayDynamicIndexing
                sprintf "shaderStorageImageArrayDynamicIndexing = %A" x.shaderStorageImageArrayDynamicIndexing
                sprintf "shaderClipDistance = %A" x.shaderClipDistance
                sprintf "shaderCullDistance = %A" x.shaderCullDistance
                sprintf "shaderFloat64 = %A" x.shaderFloat64
                sprintf "shaderInt64 = %A" x.shaderInt64
                sprintf "shaderInt16 = %A" x.shaderInt16
                sprintf "shaderResourceResidency = %A" x.shaderResourceResidency
                sprintf "shaderResourceMinLod = %A" x.shaderResourceMinLod
                sprintf "sparseBinding = %A" x.sparseBinding
                sprintf "sparseResidencyBuffer = %A" x.sparseResidencyBuffer
                sprintf "sparseResidencyImage2D = %A" x.sparseResidencyImage2D
                sprintf "sparseResidencyImage3D = %A" x.sparseResidencyImage3D
                sprintf "sparseResidency2Samples = %A" x.sparseResidency2Samples
                sprintf "sparseResidency4Samples = %A" x.sparseResidency4Samples
                sprintf "sparseResidency8Samples = %A" x.sparseResidency8Samples
                sprintf "sparseResidency16Samples = %A" x.sparseResidency16Samples
                sprintf "sparseResidencyAliased = %A" x.sparseResidencyAliased
                sprintf "variableMultisampleRate = %A" x.variableMultisampleRate
                sprintf "inheritedQueries = %A" x.inheritedQueries
            ] |> sprintf "VkPhysicalDeviceFeatures { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDeviceCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkDeviceCreateFlags
        val mutable public queueCreateInfoCount : uint32
        val mutable public pQueueCreateInfos : nativeptr<VkDeviceQueueCreateInfo>
        val mutable public enabledLayerCount : uint32
        val mutable public ppEnabledLayerNames : nativeptr<cstr>
        val mutable public enabledExtensionCount : uint32
        val mutable public ppEnabledExtensionNames : nativeptr<cstr>
        val mutable public pEnabledFeatures : nativeptr<VkPhysicalDeviceFeatures>

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkDeviceCreateFlags
          , queueCreateInfoCount : uint32
          , pQueueCreateInfos : nativeptr<VkDeviceQueueCreateInfo>
          , enabledLayerCount : uint32
          , ppEnabledLayerNames : nativeptr<cstr>
          , enabledExtensionCount : uint32
          , ppEnabledExtensionNames : nativeptr<cstr>
          , pEnabledFeatures : nativeptr<VkPhysicalDeviceFeatures>
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                queueCreateInfoCount = queueCreateInfoCount
                pQueueCreateInfos = pQueueCreateInfos
                enabledLayerCount = enabledLayerCount
                ppEnabledLayerNames = ppEnabledLayerNames
                enabledExtensionCount = enabledExtensionCount
                ppEnabledExtensionNames = ppEnabledExtensionNames
                pEnabledFeatures = pEnabledFeatures
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "queueCreateInfoCount = %A" x.queueCreateInfoCount
                sprintf "pQueueCreateInfos = %A" x.pQueueCreateInfos
                sprintf "enabledLayerCount = %A" x.enabledLayerCount
                sprintf "ppEnabledLayerNames = %A" x.ppEnabledLayerNames
                sprintf "enabledExtensionCount = %A" x.enabledExtensionCount
                sprintf "ppEnabledExtensionNames = %A" x.ppEnabledExtensionNames
                sprintf "pEnabledFeatures = %A" x.pEnabledFeatures
            ] |> sprintf "VkDeviceCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDeviceGroupBindSparseInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public resourceDeviceIndex : uint32
        val mutable public memoryDeviceIndex : uint32

        new(sType : VkStructureType
          , pNext : nativeint
          , resourceDeviceIndex : uint32
          , memoryDeviceIndex : uint32
          ) =
            {
                sType = sType
                pNext = pNext
                resourceDeviceIndex = resourceDeviceIndex
                memoryDeviceIndex = memoryDeviceIndex
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "resourceDeviceIndex = %A" x.resourceDeviceIndex
                sprintf "memoryDeviceIndex = %A" x.memoryDeviceIndex
            ] |> sprintf "VkDeviceGroupBindSparseInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDeviceGroupCommandBufferBeginInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public deviceMask : uint32

        new(sType : VkStructureType
          , pNext : nativeint
          , deviceMask : uint32
          ) =
            {
                sType = sType
                pNext = pNext
                deviceMask = deviceMask
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "deviceMask = %A" x.deviceMask
            ] |> sprintf "VkDeviceGroupCommandBufferBeginInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDeviceGroupDeviceCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public physicalDeviceCount : uint32
        val mutable public pPhysicalDevices : nativeptr<VkPhysicalDevice>

        new(sType : VkStructureType
          , pNext : nativeint
          , physicalDeviceCount : uint32
          , pPhysicalDevices : nativeptr<VkPhysicalDevice>
          ) =
            {
                sType = sType
                pNext = pNext
                physicalDeviceCount = physicalDeviceCount
                pPhysicalDevices = pPhysicalDevices
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "physicalDeviceCount = %A" x.physicalDeviceCount
                sprintf "pPhysicalDevices = %A" x.pPhysicalDevices
            ] |> sprintf "VkDeviceGroupDeviceCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDeviceGroupPresentCapabilitiesKHR = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public presentMask : uint32_32
        val mutable public modes : VkDeviceGroupPresentModeFlagsKHR

        new(sType : VkStructureType
          , pNext : nativeint
          , presentMask : uint32_32
          , modes : VkDeviceGroupPresentModeFlagsKHR
          ) =
            {
                sType = sType
                pNext = pNext
                presentMask = presentMask
                modes = modes
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "presentMask = %A" x.presentMask
                sprintf "modes = %A" x.modes
            ] |> sprintf "VkDeviceGroupPresentCapabilitiesKHR { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDeviceGroupRenderPassBeginInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public deviceMask : uint32
        val mutable public deviceRenderAreaCount : uint32
        val mutable public pDeviceRenderAreas : nativeptr<VkRect2D>

        new(sType : VkStructureType
          , pNext : nativeint
          , deviceMask : uint32
          , deviceRenderAreaCount : uint32
          , pDeviceRenderAreas : nativeptr<VkRect2D>
          ) =
            {
                sType = sType
                pNext = pNext
                deviceMask = deviceMask
                deviceRenderAreaCount = deviceRenderAreaCount
                pDeviceRenderAreas = pDeviceRenderAreas
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "deviceMask = %A" x.deviceMask
                sprintf "deviceRenderAreaCount = %A" x.deviceRenderAreaCount
                sprintf "pDeviceRenderAreas = %A" x.pDeviceRenderAreas
            ] |> sprintf "VkDeviceGroupRenderPassBeginInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDeviceGroupSubmitInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public waitSemaphoreCount : uint32
        val mutable public pWaitSemaphoreDeviceIndices : nativeptr<uint32>
        val mutable public commandBufferCount : uint32
        val mutable public pCommandBufferDeviceMasks : nativeptr<uint32>
        val mutable public signalSemaphoreCount : uint32
        val mutable public pSignalSemaphoreDeviceIndices : nativeptr<uint32>

        new(sType : VkStructureType
          , pNext : nativeint
          , waitSemaphoreCount : uint32
          , pWaitSemaphoreDeviceIndices : nativeptr<uint32>
          , commandBufferCount : uint32
          , pCommandBufferDeviceMasks : nativeptr<uint32>
          , signalSemaphoreCount : uint32
          , pSignalSemaphoreDeviceIndices : nativeptr<uint32>
          ) =
            {
                sType = sType
                pNext = pNext
                waitSemaphoreCount = waitSemaphoreCount
                pWaitSemaphoreDeviceIndices = pWaitSemaphoreDeviceIndices
                commandBufferCount = commandBufferCount
                pCommandBufferDeviceMasks = pCommandBufferDeviceMasks
                signalSemaphoreCount = signalSemaphoreCount
                pSignalSemaphoreDeviceIndices = pSignalSemaphoreDeviceIndices
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "waitSemaphoreCount = %A" x.waitSemaphoreCount
                sprintf "pWaitSemaphoreDeviceIndices = %A" x.pWaitSemaphoreDeviceIndices
                sprintf "commandBufferCount = %A" x.commandBufferCount
                sprintf "pCommandBufferDeviceMasks = %A" x.pCommandBufferDeviceMasks
                sprintf "signalSemaphoreCount = %A" x.signalSemaphoreCount
                sprintf "pSignalSemaphoreDeviceIndices = %A" x.pSignalSemaphoreDeviceIndices
            ] |> sprintf "VkDeviceGroupSubmitInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDeviceQueueInfo2 = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkDeviceQueueCreateFlags
        val mutable public queueFamilyIndex : uint32
        val mutable public queueIndex : uint32

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkDeviceQueueCreateFlags
          , queueFamilyIndex : uint32
          , queueIndex : uint32
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                queueFamilyIndex = queueFamilyIndex
                queueIndex = queueIndex
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "queueFamilyIndex = %A" x.queueFamilyIndex
                sprintf "queueIndex = %A" x.queueIndex
            ] |> sprintf "VkDeviceQueueInfo2 { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDispatchIndirectCommand = 
    struct
        val mutable public x : uint32
        val mutable public y : uint32
        val mutable public z : uint32

        new(x : uint32
          , y : uint32
          , z : uint32
          ) =
            {
                x = x
                y = y
                z = z
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "x = %A" x.x
                sprintf "y = %A" x.y
                sprintf "z = %A" x.z
            ] |> sprintf "VkDispatchIndirectCommand { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDrawIndexedIndirectCommand = 
    struct
        val mutable public indexCount : uint32
        val mutable public instanceCount : uint32
        val mutable public firstIndex : uint32
        val mutable public vertexOffset : int
        val mutable public firstInstance : uint32

        new(indexCount : uint32
          , instanceCount : uint32
          , firstIndex : uint32
          , vertexOffset : int
          , firstInstance : uint32
          ) =
            {
                indexCount = indexCount
                instanceCount = instanceCount
                firstIndex = firstIndex
                vertexOffset = vertexOffset
                firstInstance = firstInstance
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "indexCount = %A" x.indexCount
                sprintf "instanceCount = %A" x.instanceCount
                sprintf "firstIndex = %A" x.firstIndex
                sprintf "vertexOffset = %A" x.vertexOffset
                sprintf "firstInstance = %A" x.firstInstance
            ] |> sprintf "VkDrawIndexedIndirectCommand { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDrawIndirectCommand = 
    struct
        val mutable public vertexCount : uint32
        val mutable public instanceCount : uint32
        val mutable public firstVertex : uint32
        val mutable public firstInstance : uint32

        new(vertexCount : uint32
          , instanceCount : uint32
          , firstVertex : uint32
          , firstInstance : uint32
          ) =
            {
                vertexCount = vertexCount
                instanceCount = instanceCount
                firstVertex = firstVertex
                firstInstance = firstInstance
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "vertexCount = %A" x.vertexCount
                sprintf "instanceCount = %A" x.instanceCount
                sprintf "firstVertex = %A" x.firstVertex
                sprintf "firstInstance = %A" x.firstInstance
            ] |> sprintf "VkDrawIndirectCommand { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkEventCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkEventCreateFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkEventCreateFlags
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
            ] |> sprintf "VkEventCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkExportFenceCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public handleTypes : VkExternalFenceHandleTypeFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , handleTypes : VkExternalFenceHandleTypeFlags
          ) =
            {
                sType = sType
                pNext = pNext
                handleTypes = handleTypes
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "handleTypes = %A" x.handleTypes
            ] |> sprintf "VkExportFenceCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkExportMemoryAllocateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public handleTypes : VkExternalMemoryHandleTypeFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , handleTypes : VkExternalMemoryHandleTypeFlags
          ) =
            {
                sType = sType
                pNext = pNext
                handleTypes = handleTypes
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "handleTypes = %A" x.handleTypes
            ] |> sprintf "VkExportMemoryAllocateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkExportSemaphoreCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public handleTypes : VkExternalSemaphoreHandleTypeFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , handleTypes : VkExternalSemaphoreHandleTypeFlags
          ) =
            {
                sType = sType
                pNext = pNext
                handleTypes = handleTypes
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "handleTypes = %A" x.handleTypes
            ] |> sprintf "VkExportSemaphoreCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkExtensionProperties = 
    struct
        val mutable public extensionName : String256
        val mutable public specVersion : uint32

        new(extensionName : String256
          , specVersion : uint32
          ) =
            {
                extensionName = extensionName
                specVersion = specVersion
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "extensionName = %A" x.extensionName
                sprintf "specVersion = %A" x.specVersion
            ] |> sprintf "VkExtensionProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkExternalMemoryProperties = 
    struct
        val mutable public externalMemoryFeatures : VkExternalMemoryFeatureFlags
        val mutable public exportFromImportedHandleTypes : VkExternalMemoryHandleTypeFlags
        val mutable public compatibleHandleTypes : VkExternalMemoryHandleTypeFlags

        new(externalMemoryFeatures : VkExternalMemoryFeatureFlags
          , exportFromImportedHandleTypes : VkExternalMemoryHandleTypeFlags
          , compatibleHandleTypes : VkExternalMemoryHandleTypeFlags
          ) =
            {
                externalMemoryFeatures = externalMemoryFeatures
                exportFromImportedHandleTypes = exportFromImportedHandleTypes
                compatibleHandleTypes = compatibleHandleTypes
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "externalMemoryFeatures = %A" x.externalMemoryFeatures
                sprintf "exportFromImportedHandleTypes = %A" x.exportFromImportedHandleTypes
                sprintf "compatibleHandleTypes = %A" x.compatibleHandleTypes
            ] |> sprintf "VkExternalMemoryProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkExternalBufferProperties = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public externalMemoryProperties : VkExternalMemoryProperties

        new(sType : VkStructureType
          , pNext : nativeint
          , externalMemoryProperties : VkExternalMemoryProperties
          ) =
            {
                sType = sType
                pNext = pNext
                externalMemoryProperties = externalMemoryProperties
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "externalMemoryProperties = %A" x.externalMemoryProperties
            ] |> sprintf "VkExternalBufferProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkExternalFenceProperties = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public exportFromImportedHandleTypes : VkExternalFenceHandleTypeFlags
        val mutable public compatibleHandleTypes : VkExternalFenceHandleTypeFlags
        val mutable public externalFenceFeatures : VkExternalFenceFeatureFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , exportFromImportedHandleTypes : VkExternalFenceHandleTypeFlags
          , compatibleHandleTypes : VkExternalFenceHandleTypeFlags
          , externalFenceFeatures : VkExternalFenceFeatureFlags
          ) =
            {
                sType = sType
                pNext = pNext
                exportFromImportedHandleTypes = exportFromImportedHandleTypes
                compatibleHandleTypes = compatibleHandleTypes
                externalFenceFeatures = externalFenceFeatures
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "exportFromImportedHandleTypes = %A" x.exportFromImportedHandleTypes
                sprintf "compatibleHandleTypes = %A" x.compatibleHandleTypes
                sprintf "externalFenceFeatures = %A" x.externalFenceFeatures
            ] |> sprintf "VkExternalFenceProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkExternalImageFormatProperties = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public externalMemoryProperties : VkExternalMemoryProperties

        new(sType : VkStructureType
          , pNext : nativeint
          , externalMemoryProperties : VkExternalMemoryProperties
          ) =
            {
                sType = sType
                pNext = pNext
                externalMemoryProperties = externalMemoryProperties
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "externalMemoryProperties = %A" x.externalMemoryProperties
            ] |> sprintf "VkExternalImageFormatProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkExternalMemoryBufferCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public handleTypes : VkExternalMemoryHandleTypeFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , handleTypes : VkExternalMemoryHandleTypeFlags
          ) =
            {
                sType = sType
                pNext = pNext
                handleTypes = handleTypes
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "handleTypes = %A" x.handleTypes
            ] |> sprintf "VkExternalMemoryBufferCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkExternalMemoryImageCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public handleTypes : VkExternalMemoryHandleTypeFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , handleTypes : VkExternalMemoryHandleTypeFlags
          ) =
            {
                sType = sType
                pNext = pNext
                handleTypes = handleTypes
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "handleTypes = %A" x.handleTypes
            ] |> sprintf "VkExternalMemoryImageCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkExternalSemaphoreProperties = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public exportFromImportedHandleTypes : VkExternalSemaphoreHandleTypeFlags
        val mutable public compatibleHandleTypes : VkExternalSemaphoreHandleTypeFlags
        val mutable public externalSemaphoreFeatures : VkExternalSemaphoreFeatureFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , exportFromImportedHandleTypes : VkExternalSemaphoreHandleTypeFlags
          , compatibleHandleTypes : VkExternalSemaphoreHandleTypeFlags
          , externalSemaphoreFeatures : VkExternalSemaphoreFeatureFlags
          ) =
            {
                sType = sType
                pNext = pNext
                exportFromImportedHandleTypes = exportFromImportedHandleTypes
                compatibleHandleTypes = compatibleHandleTypes
                externalSemaphoreFeatures = externalSemaphoreFeatures
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "exportFromImportedHandleTypes = %A" x.exportFromImportedHandleTypes
                sprintf "compatibleHandleTypes = %A" x.compatibleHandleTypes
                sprintf "externalSemaphoreFeatures = %A" x.externalSemaphoreFeatures
            ] |> sprintf "VkExternalSemaphoreProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkFenceCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkFenceCreateFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkFenceCreateFlags
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
            ] |> sprintf "VkFenceCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkFormatProperties = 
    struct
        val mutable public linearTilingFeatures : VkFormatFeatureFlags
        val mutable public optimalTilingFeatures : VkFormatFeatureFlags
        val mutable public bufferFeatures : VkFormatFeatureFlags

        new(linearTilingFeatures : VkFormatFeatureFlags
          , optimalTilingFeatures : VkFormatFeatureFlags
          , bufferFeatures : VkFormatFeatureFlags
          ) =
            {
                linearTilingFeatures = linearTilingFeatures
                optimalTilingFeatures = optimalTilingFeatures
                bufferFeatures = bufferFeatures
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "linearTilingFeatures = %A" x.linearTilingFeatures
                sprintf "optimalTilingFeatures = %A" x.optimalTilingFeatures
                sprintf "bufferFeatures = %A" x.bufferFeatures
            ] |> sprintf "VkFormatProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkFormatProperties2 = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public formatProperties : VkFormatProperties

        new(sType : VkStructureType
          , pNext : nativeint
          , formatProperties : VkFormatProperties
          ) =
            {
                sType = sType
                pNext = pNext
                formatProperties = formatProperties
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "formatProperties = %A" x.formatProperties
            ] |> sprintf "VkFormatProperties2 { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkFramebufferCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkFramebufferCreateFlags
        val mutable public renderPass : VkRenderPass
        val mutable public attachmentCount : uint32
        val mutable public pAttachments : nativeptr<VkImageView>
        val mutable public width : uint32
        val mutable public height : uint32
        val mutable public layers : uint32

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkFramebufferCreateFlags
          , renderPass : VkRenderPass
          , attachmentCount : uint32
          , pAttachments : nativeptr<VkImageView>
          , width : uint32
          , height : uint32
          , layers : uint32
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                renderPass = renderPass
                attachmentCount = attachmentCount
                pAttachments = pAttachments
                width = width
                height = height
                layers = layers
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "renderPass = %A" x.renderPass
                sprintf "attachmentCount = %A" x.attachmentCount
                sprintf "pAttachments = %A" x.pAttachments
                sprintf "width = %A" x.width
                sprintf "height = %A" x.height
                sprintf "layers = %A" x.layers
            ] |> sprintf "VkFramebufferCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkVertexInputBindingDescription = 
    struct
        val mutable public binding : uint32
        val mutable public stride : uint32
        val mutable public inputRate : VkVertexInputRate

        new(binding : uint32
          , stride : uint32
          , inputRate : VkVertexInputRate
          ) =
            {
                binding = binding
                stride = stride
                inputRate = inputRate
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "binding = %A" x.binding
                sprintf "stride = %A" x.stride
                sprintf "inputRate = %A" x.inputRate
            ] |> sprintf "VkVertexInputBindingDescription { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkVertexInputAttributeDescription = 
    struct
        val mutable public location : uint32
        val mutable public binding : uint32
        val mutable public format : VkFormat
        val mutable public offset : uint32

        new(location : uint32
          , binding : uint32
          , format : VkFormat
          , offset : uint32
          ) =
            {
                location = location
                binding = binding
                format = format
                offset = offset
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "location = %A" x.location
                sprintf "binding = %A" x.binding
                sprintf "format = %A" x.format
                sprintf "offset = %A" x.offset
            ] |> sprintf "VkVertexInputAttributeDescription { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineVertexInputStateCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineVertexInputStateCreateFlags
        val mutable public vertexBindingDescriptionCount : uint32
        val mutable public pVertexBindingDescriptions : nativeptr<VkVertexInputBindingDescription>
        val mutable public vertexAttributeDescriptionCount : uint32
        val mutable public pVertexAttributeDescriptions : nativeptr<VkVertexInputAttributeDescription>

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkPipelineVertexInputStateCreateFlags
          , vertexBindingDescriptionCount : uint32
          , pVertexBindingDescriptions : nativeptr<VkVertexInputBindingDescription>
          , vertexAttributeDescriptionCount : uint32
          , pVertexAttributeDescriptions : nativeptr<VkVertexInputAttributeDescription>
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                vertexBindingDescriptionCount = vertexBindingDescriptionCount
                pVertexBindingDescriptions = pVertexBindingDescriptions
                vertexAttributeDescriptionCount = vertexAttributeDescriptionCount
                pVertexAttributeDescriptions = pVertexAttributeDescriptions
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "vertexBindingDescriptionCount = %A" x.vertexBindingDescriptionCount
                sprintf "pVertexBindingDescriptions = %A" x.pVertexBindingDescriptions
                sprintf "vertexAttributeDescriptionCount = %A" x.vertexAttributeDescriptionCount
                sprintf "pVertexAttributeDescriptions = %A" x.pVertexAttributeDescriptions
            ] |> sprintf "VkPipelineVertexInputStateCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineInputAssemblyStateCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineInputAssemblyStateCreateFlags
        val mutable public topology : VkPrimitiveTopology
        val mutable public primitiveRestartEnable : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkPipelineInputAssemblyStateCreateFlags
          , topology : VkPrimitiveTopology
          , primitiveRestartEnable : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                topology = topology
                primitiveRestartEnable = primitiveRestartEnable
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "topology = %A" x.topology
                sprintf "primitiveRestartEnable = %A" x.primitiveRestartEnable
            ] |> sprintf "VkPipelineInputAssemblyStateCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineTessellationStateCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineTessellationStateCreateFlags
        val mutable public patchControlPoints : uint32

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkPipelineTessellationStateCreateFlags
          , patchControlPoints : uint32
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                patchControlPoints = patchControlPoints
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "patchControlPoints = %A" x.patchControlPoints
            ] |> sprintf "VkPipelineTessellationStateCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkViewport = 
    struct
        val mutable public x : float32
        val mutable public y : float32
        val mutable public width : float32
        val mutable public height : float32
        val mutable public minDepth : float32
        val mutable public maxDepth : float32

        new(x : float32
          , y : float32
          , width : float32
          , height : float32
          , minDepth : float32
          , maxDepth : float32
          ) =
            {
                x = x
                y = y
                width = width
                height = height
                minDepth = minDepth
                maxDepth = maxDepth
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "x = %A" x.x
                sprintf "y = %A" x.y
                sprintf "width = %A" x.width
                sprintf "height = %A" x.height
                sprintf "minDepth = %A" x.minDepth
                sprintf "maxDepth = %A" x.maxDepth
            ] |> sprintf "VkViewport { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineViewportStateCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineViewportStateCreateFlags
        val mutable public viewportCount : uint32
        val mutable public pViewports : nativeptr<VkViewport>
        val mutable public scissorCount : uint32
        val mutable public pScissors : nativeptr<VkRect2D>

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkPipelineViewportStateCreateFlags
          , viewportCount : uint32
          , pViewports : nativeptr<VkViewport>
          , scissorCount : uint32
          , pScissors : nativeptr<VkRect2D>
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                viewportCount = viewportCount
                pViewports = pViewports
                scissorCount = scissorCount
                pScissors = pScissors
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "viewportCount = %A" x.viewportCount
                sprintf "pViewports = %A" x.pViewports
                sprintf "scissorCount = %A" x.scissorCount
                sprintf "pScissors = %A" x.pScissors
            ] |> sprintf "VkPipelineViewportStateCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineRasterizationStateCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineRasterizationStateCreateFlags
        val mutable public depthClampEnable : VkBool32
        val mutable public rasterizerDiscardEnable : VkBool32
        val mutable public polygonMode : VkPolygonMode
        val mutable public cullMode : VkCullModeFlags
        val mutable public frontFace : VkFrontFace
        val mutable public depthBiasEnable : VkBool32
        val mutable public depthBiasConstantFactor : float32
        val mutable public depthBiasClamp : float32
        val mutable public depthBiasSlopeFactor : float32
        val mutable public lineWidth : float32

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkPipelineRasterizationStateCreateFlags
          , depthClampEnable : VkBool32
          , rasterizerDiscardEnable : VkBool32
          , polygonMode : VkPolygonMode
          , cullMode : VkCullModeFlags
          , frontFace : VkFrontFace
          , depthBiasEnable : VkBool32
          , depthBiasConstantFactor : float32
          , depthBiasClamp : float32
          , depthBiasSlopeFactor : float32
          , lineWidth : float32
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                depthClampEnable = depthClampEnable
                rasterizerDiscardEnable = rasterizerDiscardEnable
                polygonMode = polygonMode
                cullMode = cullMode
                frontFace = frontFace
                depthBiasEnable = depthBiasEnable
                depthBiasConstantFactor = depthBiasConstantFactor
                depthBiasClamp = depthBiasClamp
                depthBiasSlopeFactor = depthBiasSlopeFactor
                lineWidth = lineWidth
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "depthClampEnable = %A" x.depthClampEnable
                sprintf "rasterizerDiscardEnable = %A" x.rasterizerDiscardEnable
                sprintf "polygonMode = %A" x.polygonMode
                sprintf "cullMode = %A" x.cullMode
                sprintf "frontFace = %A" x.frontFace
                sprintf "depthBiasEnable = %A" x.depthBiasEnable
                sprintf "depthBiasConstantFactor = %A" x.depthBiasConstantFactor
                sprintf "depthBiasClamp = %A" x.depthBiasClamp
                sprintf "depthBiasSlopeFactor = %A" x.depthBiasSlopeFactor
                sprintf "lineWidth = %A" x.lineWidth
            ] |> sprintf "VkPipelineRasterizationStateCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineMultisampleStateCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineMultisampleStateCreateFlags
        val mutable public rasterizationSamples : VkSampleCountFlags
        val mutable public sampleShadingEnable : VkBool32
        val mutable public minSampleShading : float32
        val mutable public pSampleMask : nativeptr<VkSampleMask>
        val mutable public alphaToCoverageEnable : VkBool32
        val mutable public alphaToOneEnable : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkPipelineMultisampleStateCreateFlags
          , rasterizationSamples : VkSampleCountFlags
          , sampleShadingEnable : VkBool32
          , minSampleShading : float32
          , pSampleMask : nativeptr<VkSampleMask>
          , alphaToCoverageEnable : VkBool32
          , alphaToOneEnable : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                rasterizationSamples = rasterizationSamples
                sampleShadingEnable = sampleShadingEnable
                minSampleShading = minSampleShading
                pSampleMask = pSampleMask
                alphaToCoverageEnable = alphaToCoverageEnable
                alphaToOneEnable = alphaToOneEnable
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "rasterizationSamples = %A" x.rasterizationSamples
                sprintf "sampleShadingEnable = %A" x.sampleShadingEnable
                sprintf "minSampleShading = %A" x.minSampleShading
                sprintf "pSampleMask = %A" x.pSampleMask
                sprintf "alphaToCoverageEnable = %A" x.alphaToCoverageEnable
                sprintf "alphaToOneEnable = %A" x.alphaToOneEnable
            ] |> sprintf "VkPipelineMultisampleStateCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkStencilOpState = 
    struct
        val mutable public failOp : VkStencilOp
        val mutable public passOp : VkStencilOp
        val mutable public depthFailOp : VkStencilOp
        val mutable public compareOp : VkCompareOp
        val mutable public compareMask : uint32
        val mutable public writeMask : uint32
        val mutable public reference : uint32

        new(failOp : VkStencilOp
          , passOp : VkStencilOp
          , depthFailOp : VkStencilOp
          , compareOp : VkCompareOp
          , compareMask : uint32
          , writeMask : uint32
          , reference : uint32
          ) =
            {
                failOp = failOp
                passOp = passOp
                depthFailOp = depthFailOp
                compareOp = compareOp
                compareMask = compareMask
                writeMask = writeMask
                reference = reference
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "failOp = %A" x.failOp
                sprintf "passOp = %A" x.passOp
                sprintf "depthFailOp = %A" x.depthFailOp
                sprintf "compareOp = %A" x.compareOp
                sprintf "compareMask = %A" x.compareMask
                sprintf "writeMask = %A" x.writeMask
                sprintf "reference = %A" x.reference
            ] |> sprintf "VkStencilOpState { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineDepthStencilStateCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineDepthStencilStateCreateFlags
        val mutable public depthTestEnable : VkBool32
        val mutable public depthWriteEnable : VkBool32
        val mutable public depthCompareOp : VkCompareOp
        val mutable public depthBoundsTestEnable : VkBool32
        val mutable public stencilTestEnable : VkBool32
        val mutable public front : VkStencilOpState
        val mutable public back : VkStencilOpState
        val mutable public minDepthBounds : float32
        val mutable public maxDepthBounds : float32

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkPipelineDepthStencilStateCreateFlags
          , depthTestEnable : VkBool32
          , depthWriteEnable : VkBool32
          , depthCompareOp : VkCompareOp
          , depthBoundsTestEnable : VkBool32
          , stencilTestEnable : VkBool32
          , front : VkStencilOpState
          , back : VkStencilOpState
          , minDepthBounds : float32
          , maxDepthBounds : float32
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                depthTestEnable = depthTestEnable
                depthWriteEnable = depthWriteEnable
                depthCompareOp = depthCompareOp
                depthBoundsTestEnable = depthBoundsTestEnable
                stencilTestEnable = stencilTestEnable
                front = front
                back = back
                minDepthBounds = minDepthBounds
                maxDepthBounds = maxDepthBounds
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "depthTestEnable = %A" x.depthTestEnable
                sprintf "depthWriteEnable = %A" x.depthWriteEnable
                sprintf "depthCompareOp = %A" x.depthCompareOp
                sprintf "depthBoundsTestEnable = %A" x.depthBoundsTestEnable
                sprintf "stencilTestEnable = %A" x.stencilTestEnable
                sprintf "front = %A" x.front
                sprintf "back = %A" x.back
                sprintf "minDepthBounds = %A" x.minDepthBounds
                sprintf "maxDepthBounds = %A" x.maxDepthBounds
            ] |> sprintf "VkPipelineDepthStencilStateCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineColorBlendAttachmentState = 
    struct
        val mutable public blendEnable : VkBool32
        val mutable public srcColorBlendFactor : VkBlendFactor
        val mutable public dstColorBlendFactor : VkBlendFactor
        val mutable public colorBlendOp : VkBlendOp
        val mutable public srcAlphaBlendFactor : VkBlendFactor
        val mutable public dstAlphaBlendFactor : VkBlendFactor
        val mutable public alphaBlendOp : VkBlendOp
        val mutable public colorWriteMask : VkColorComponentFlags

        new(blendEnable : VkBool32
          , srcColorBlendFactor : VkBlendFactor
          , dstColorBlendFactor : VkBlendFactor
          , colorBlendOp : VkBlendOp
          , srcAlphaBlendFactor : VkBlendFactor
          , dstAlphaBlendFactor : VkBlendFactor
          , alphaBlendOp : VkBlendOp
          , colorWriteMask : VkColorComponentFlags
          ) =
            {
                blendEnable = blendEnable
                srcColorBlendFactor = srcColorBlendFactor
                dstColorBlendFactor = dstColorBlendFactor
                colorBlendOp = colorBlendOp
                srcAlphaBlendFactor = srcAlphaBlendFactor
                dstAlphaBlendFactor = dstAlphaBlendFactor
                alphaBlendOp = alphaBlendOp
                colorWriteMask = colorWriteMask
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "blendEnable = %A" x.blendEnable
                sprintf "srcColorBlendFactor = %A" x.srcColorBlendFactor
                sprintf "dstColorBlendFactor = %A" x.dstColorBlendFactor
                sprintf "colorBlendOp = %A" x.colorBlendOp
                sprintf "srcAlphaBlendFactor = %A" x.srcAlphaBlendFactor
                sprintf "dstAlphaBlendFactor = %A" x.dstAlphaBlendFactor
                sprintf "alphaBlendOp = %A" x.alphaBlendOp
                sprintf "colorWriteMask = %A" x.colorWriteMask
            ] |> sprintf "VkPipelineColorBlendAttachmentState { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineColorBlendStateCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineColorBlendStateCreateFlags
        val mutable public logicOpEnable : VkBool32
        val mutable public logicOp : VkLogicOp
        val mutable public attachmentCount : uint32
        val mutable public pAttachments : nativeptr<VkPipelineColorBlendAttachmentState>
        val mutable public blendConstants : V4f

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkPipelineColorBlendStateCreateFlags
          , logicOpEnable : VkBool32
          , logicOp : VkLogicOp
          , attachmentCount : uint32
          , pAttachments : nativeptr<VkPipelineColorBlendAttachmentState>
          , blendConstants : V4f
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                logicOpEnable = logicOpEnable
                logicOp = logicOp
                attachmentCount = attachmentCount
                pAttachments = pAttachments
                blendConstants = blendConstants
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "logicOpEnable = %A" x.logicOpEnable
                sprintf "logicOp = %A" x.logicOp
                sprintf "attachmentCount = %A" x.attachmentCount
                sprintf "pAttachments = %A" x.pAttachments
                sprintf "blendConstants = %A" x.blendConstants
            ] |> sprintf "VkPipelineColorBlendStateCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineDynamicStateCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineDynamicStateCreateFlags
        val mutable public dynamicStateCount : uint32
        val mutable public pDynamicStates : nativeptr<VkDynamicState>

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkPipelineDynamicStateCreateFlags
          , dynamicStateCount : uint32
          , pDynamicStates : nativeptr<VkDynamicState>
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                dynamicStateCount = dynamicStateCount
                pDynamicStates = pDynamicStates
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "dynamicStateCount = %A" x.dynamicStateCount
                sprintf "pDynamicStates = %A" x.pDynamicStates
            ] |> sprintf "VkPipelineDynamicStateCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkGraphicsPipelineCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineCreateFlags
        val mutable public stageCount : uint32
        val mutable public pStages : nativeptr<VkPipelineShaderStageCreateInfo>
        val mutable public pVertexInputState : nativeptr<VkPipelineVertexInputStateCreateInfo>
        val mutable public pInputAssemblyState : nativeptr<VkPipelineInputAssemblyStateCreateInfo>
        val mutable public pTessellationState : nativeptr<VkPipelineTessellationStateCreateInfo>
        val mutable public pViewportState : nativeptr<VkPipelineViewportStateCreateInfo>
        val mutable public pRasterizationState : nativeptr<VkPipelineRasterizationStateCreateInfo>
        val mutable public pMultisampleState : nativeptr<VkPipelineMultisampleStateCreateInfo>
        val mutable public pDepthStencilState : nativeptr<VkPipelineDepthStencilStateCreateInfo>
        val mutable public pColorBlendState : nativeptr<VkPipelineColorBlendStateCreateInfo>
        val mutable public pDynamicState : nativeptr<VkPipelineDynamicStateCreateInfo>
        val mutable public layout : VkPipelineLayout
        val mutable public renderPass : VkRenderPass
        val mutable public subpass : uint32
        val mutable public basePipelineHandle : VkPipeline
        val mutable public basePipelineIndex : int

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkPipelineCreateFlags
          , stageCount : uint32
          , pStages : nativeptr<VkPipelineShaderStageCreateInfo>
          , pVertexInputState : nativeptr<VkPipelineVertexInputStateCreateInfo>
          , pInputAssemblyState : nativeptr<VkPipelineInputAssemblyStateCreateInfo>
          , pTessellationState : nativeptr<VkPipelineTessellationStateCreateInfo>
          , pViewportState : nativeptr<VkPipelineViewportStateCreateInfo>
          , pRasterizationState : nativeptr<VkPipelineRasterizationStateCreateInfo>
          , pMultisampleState : nativeptr<VkPipelineMultisampleStateCreateInfo>
          , pDepthStencilState : nativeptr<VkPipelineDepthStencilStateCreateInfo>
          , pColorBlendState : nativeptr<VkPipelineColorBlendStateCreateInfo>
          , pDynamicState : nativeptr<VkPipelineDynamicStateCreateInfo>
          , layout : VkPipelineLayout
          , renderPass : VkRenderPass
          , subpass : uint32
          , basePipelineHandle : VkPipeline
          , basePipelineIndex : int
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                stageCount = stageCount
                pStages = pStages
                pVertexInputState = pVertexInputState
                pInputAssemblyState = pInputAssemblyState
                pTessellationState = pTessellationState
                pViewportState = pViewportState
                pRasterizationState = pRasterizationState
                pMultisampleState = pMultisampleState
                pDepthStencilState = pDepthStencilState
                pColorBlendState = pColorBlendState
                pDynamicState = pDynamicState
                layout = layout
                renderPass = renderPass
                subpass = subpass
                basePipelineHandle = basePipelineHandle
                basePipelineIndex = basePipelineIndex
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "stageCount = %A" x.stageCount
                sprintf "pStages = %A" x.pStages
                sprintf "pVertexInputState = %A" x.pVertexInputState
                sprintf "pInputAssemblyState = %A" x.pInputAssemblyState
                sprintf "pTessellationState = %A" x.pTessellationState
                sprintf "pViewportState = %A" x.pViewportState
                sprintf "pRasterizationState = %A" x.pRasterizationState
                sprintf "pMultisampleState = %A" x.pMultisampleState
                sprintf "pDepthStencilState = %A" x.pDepthStencilState
                sprintf "pColorBlendState = %A" x.pColorBlendState
                sprintf "pDynamicState = %A" x.pDynamicState
                sprintf "layout = %A" x.layout
                sprintf "renderPass = %A" x.renderPass
                sprintf "subpass = %A" x.subpass
                sprintf "basePipelineHandle = %A" x.basePipelineHandle
                sprintf "basePipelineIndex = %A" x.basePipelineIndex
            ] |> sprintf "VkGraphicsPipelineCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageBlit = 
    struct
        val mutable public srcSubresource : VkImageSubresourceLayers
        val mutable public srcOffsets : VkOffset3D_2
        val mutable public dstSubresource : VkImageSubresourceLayers
        val mutable public dstOffsets : VkOffset3D_2

        new(srcSubresource : VkImageSubresourceLayers
          , srcOffsets : VkOffset3D_2
          , dstSubresource : VkImageSubresourceLayers
          , dstOffsets : VkOffset3D_2
          ) =
            {
                srcSubresource = srcSubresource
                srcOffsets = srcOffsets
                dstSubresource = dstSubresource
                dstOffsets = dstOffsets
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "srcSubresource = %A" x.srcSubresource
                sprintf "srcOffsets = %A" x.srcOffsets
                sprintf "dstSubresource = %A" x.dstSubresource
                sprintf "dstOffsets = %A" x.dstOffsets
            ] |> sprintf "VkImageBlit { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageCopy = 
    struct
        val mutable public srcSubresource : VkImageSubresourceLayers
        val mutable public srcOffset : VkOffset3D
        val mutable public dstSubresource : VkImageSubresourceLayers
        val mutable public dstOffset : VkOffset3D
        val mutable public extent : VkExtent3D

        new(srcSubresource : VkImageSubresourceLayers
          , srcOffset : VkOffset3D
          , dstSubresource : VkImageSubresourceLayers
          , dstOffset : VkOffset3D
          , extent : VkExtent3D
          ) =
            {
                srcSubresource = srcSubresource
                srcOffset = srcOffset
                dstSubresource = dstSubresource
                dstOffset = dstOffset
                extent = extent
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "srcSubresource = %A" x.srcSubresource
                sprintf "srcOffset = %A" x.srcOffset
                sprintf "dstSubresource = %A" x.dstSubresource
                sprintf "dstOffset = %A" x.dstOffset
                sprintf "extent = %A" x.extent
            ] |> sprintf "VkImageCopy { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkImageCreateFlags
        val mutable public imageType : VkImageType
        val mutable public format : VkFormat
        val mutable public extent : VkExtent3D
        val mutable public mipLevels : uint32
        val mutable public arrayLayers : uint32
        val mutable public samples : VkSampleCountFlags
        val mutable public tiling : VkImageTiling
        val mutable public usage : VkImageUsageFlags
        val mutable public sharingMode : VkSharingMode
        val mutable public queueFamilyIndexCount : uint32
        val mutable public pQueueFamilyIndices : nativeptr<uint32>
        val mutable public initialLayout : VkImageLayout

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkImageCreateFlags
          , imageType : VkImageType
          , format : VkFormat
          , extent : VkExtent3D
          , mipLevels : uint32
          , arrayLayers : uint32
          , samples : VkSampleCountFlags
          , tiling : VkImageTiling
          , usage : VkImageUsageFlags
          , sharingMode : VkSharingMode
          , queueFamilyIndexCount : uint32
          , pQueueFamilyIndices : nativeptr<uint32>
          , initialLayout : VkImageLayout
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                imageType = imageType
                format = format
                extent = extent
                mipLevels = mipLevels
                arrayLayers = arrayLayers
                samples = samples
                tiling = tiling
                usage = usage
                sharingMode = sharingMode
                queueFamilyIndexCount = queueFamilyIndexCount
                pQueueFamilyIndices = pQueueFamilyIndices
                initialLayout = initialLayout
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "imageType = %A" x.imageType
                sprintf "format = %A" x.format
                sprintf "extent = %A" x.extent
                sprintf "mipLevels = %A" x.mipLevels
                sprintf "arrayLayers = %A" x.arrayLayers
                sprintf "samples = %A" x.samples
                sprintf "tiling = %A" x.tiling
                sprintf "usage = %A" x.usage
                sprintf "sharingMode = %A" x.sharingMode
                sprintf "queueFamilyIndexCount = %A" x.queueFamilyIndexCount
                sprintf "pQueueFamilyIndices = %A" x.pQueueFamilyIndices
                sprintf "initialLayout = %A" x.initialLayout
            ] |> sprintf "VkImageCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageFormatProperties = 
    struct
        val mutable public maxExtent : VkExtent3D
        val mutable public maxMipLevels : uint32
        val mutable public maxArrayLayers : uint32
        val mutable public sampleCounts : VkSampleCountFlags
        val mutable public maxResourceSize : VkDeviceSize

        new(maxExtent : VkExtent3D
          , maxMipLevels : uint32
          , maxArrayLayers : uint32
          , sampleCounts : VkSampleCountFlags
          , maxResourceSize : VkDeviceSize
          ) =
            {
                maxExtent = maxExtent
                maxMipLevels = maxMipLevels
                maxArrayLayers = maxArrayLayers
                sampleCounts = sampleCounts
                maxResourceSize = maxResourceSize
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "maxExtent = %A" x.maxExtent
                sprintf "maxMipLevels = %A" x.maxMipLevels
                sprintf "maxArrayLayers = %A" x.maxArrayLayers
                sprintf "sampleCounts = %A" x.sampleCounts
                sprintf "maxResourceSize = %A" x.maxResourceSize
            ] |> sprintf "VkImageFormatProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageFormatProperties2 = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public imageFormatProperties : VkImageFormatProperties

        new(sType : VkStructureType
          , pNext : nativeint
          , imageFormatProperties : VkImageFormatProperties
          ) =
            {
                sType = sType
                pNext = pNext
                imageFormatProperties = imageFormatProperties
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "imageFormatProperties = %A" x.imageFormatProperties
            ] |> sprintf "VkImageFormatProperties2 { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageSubresourceRange = 
    struct
        val mutable public aspectMask : VkImageAspectFlags
        val mutable public baseMipLevel : uint32
        val mutable public levelCount : uint32
        val mutable public baseArrayLayer : uint32
        val mutable public layerCount : uint32

        new(aspectMask : VkImageAspectFlags
          , baseMipLevel : uint32
          , levelCount : uint32
          , baseArrayLayer : uint32
          , layerCount : uint32
          ) =
            {
                aspectMask = aspectMask
                baseMipLevel = baseMipLevel
                levelCount = levelCount
                baseArrayLayer = baseArrayLayer
                layerCount = layerCount
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "aspectMask = %A" x.aspectMask
                sprintf "baseMipLevel = %A" x.baseMipLevel
                sprintf "levelCount = %A" x.levelCount
                sprintf "baseArrayLayer = %A" x.baseArrayLayer
                sprintf "layerCount = %A" x.layerCount
            ] |> sprintf "VkImageSubresourceRange { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageMemoryBarrier = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public srcAccessMask : VkAccessFlags
        val mutable public dstAccessMask : VkAccessFlags
        val mutable public oldLayout : VkImageLayout
        val mutable public newLayout : VkImageLayout
        val mutable public srcQueueFamilyIndex : uint32
        val mutable public dstQueueFamilyIndex : uint32
        val mutable public image : VkImage
        val mutable public subresourceRange : VkImageSubresourceRange

        new(sType : VkStructureType
          , pNext : nativeint
          , srcAccessMask : VkAccessFlags
          , dstAccessMask : VkAccessFlags
          , oldLayout : VkImageLayout
          , newLayout : VkImageLayout
          , srcQueueFamilyIndex : uint32
          , dstQueueFamilyIndex : uint32
          , image : VkImage
          , subresourceRange : VkImageSubresourceRange
          ) =
            {
                sType = sType
                pNext = pNext
                srcAccessMask = srcAccessMask
                dstAccessMask = dstAccessMask
                oldLayout = oldLayout
                newLayout = newLayout
                srcQueueFamilyIndex = srcQueueFamilyIndex
                dstQueueFamilyIndex = dstQueueFamilyIndex
                image = image
                subresourceRange = subresourceRange
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "srcAccessMask = %A" x.srcAccessMask
                sprintf "dstAccessMask = %A" x.dstAccessMask
                sprintf "oldLayout = %A" x.oldLayout
                sprintf "newLayout = %A" x.newLayout
                sprintf "srcQueueFamilyIndex = %A" x.srcQueueFamilyIndex
                sprintf "dstQueueFamilyIndex = %A" x.dstQueueFamilyIndex
                sprintf "image = %A" x.image
                sprintf "subresourceRange = %A" x.subresourceRange
            ] |> sprintf "VkImageMemoryBarrier { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageMemoryRequirementsInfo2 = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public image : VkImage

        new(sType : VkStructureType
          , pNext : nativeint
          , image : VkImage
          ) =
            {
                sType = sType
                pNext = pNext
                image = image
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "image = %A" x.image
            ] |> sprintf "VkImageMemoryRequirementsInfo2 { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImagePlaneMemoryRequirementsInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public planeAspect : VkImageAspectFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , planeAspect : VkImageAspectFlags
          ) =
            {
                sType = sType
                pNext = pNext
                planeAspect = planeAspect
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "planeAspect = %A" x.planeAspect
            ] |> sprintf "VkImagePlaneMemoryRequirementsInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageResolve = 
    struct
        val mutable public srcSubresource : VkImageSubresourceLayers
        val mutable public srcOffset : VkOffset3D
        val mutable public dstSubresource : VkImageSubresourceLayers
        val mutable public dstOffset : VkOffset3D
        val mutable public extent : VkExtent3D

        new(srcSubresource : VkImageSubresourceLayers
          , srcOffset : VkOffset3D
          , dstSubresource : VkImageSubresourceLayers
          , dstOffset : VkOffset3D
          , extent : VkExtent3D
          ) =
            {
                srcSubresource = srcSubresource
                srcOffset = srcOffset
                dstSubresource = dstSubresource
                dstOffset = dstOffset
                extent = extent
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "srcSubresource = %A" x.srcSubresource
                sprintf "srcOffset = %A" x.srcOffset
                sprintf "dstSubresource = %A" x.dstSubresource
                sprintf "dstOffset = %A" x.dstOffset
                sprintf "extent = %A" x.extent
            ] |> sprintf "VkImageResolve { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageSparseMemoryRequirementsInfo2 = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public image : VkImage

        new(sType : VkStructureType
          , pNext : nativeint
          , image : VkImage
          ) =
            {
                sType = sType
                pNext = pNext
                image = image
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "image = %A" x.image
            ] |> sprintf "VkImageSparseMemoryRequirementsInfo2 { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageViewCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkImageViewCreateFlags
        val mutable public image : VkImage
        val mutable public viewType : VkImageViewType
        val mutable public format : VkFormat
        val mutable public components : VkComponentMapping
        val mutable public subresourceRange : VkImageSubresourceRange

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkImageViewCreateFlags
          , image : VkImage
          , viewType : VkImageViewType
          , format : VkFormat
          , components : VkComponentMapping
          , subresourceRange : VkImageSubresourceRange
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                image = image
                viewType = viewType
                format = format
                components = components
                subresourceRange = subresourceRange
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "image = %A" x.image
                sprintf "viewType = %A" x.viewType
                sprintf "format = %A" x.format
                sprintf "components = %A" x.components
                sprintf "subresourceRange = %A" x.subresourceRange
            ] |> sprintf "VkImageViewCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageViewUsageCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public usage : VkImageUsageFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , usage : VkImageUsageFlags
          ) =
            {
                sType = sType
                pNext = pNext
                usage = usage
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "usage = %A" x.usage
            ] |> sprintf "VkImageViewUsageCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkInputAttachmentAspectReference = 
    struct
        val mutable public subpass : uint32
        val mutable public inputAttachmentIndex : uint32
        val mutable public aspectMask : VkImageAspectFlags

        new(subpass : uint32
          , inputAttachmentIndex : uint32
          , aspectMask : VkImageAspectFlags
          ) =
            {
                subpass = subpass
                inputAttachmentIndex = inputAttachmentIndex
                aspectMask = aspectMask
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "subpass = %A" x.subpass
                sprintf "inputAttachmentIndex = %A" x.inputAttachmentIndex
                sprintf "aspectMask = %A" x.aspectMask
            ] |> sprintf "VkInputAttachmentAspectReference { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkInstanceCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkInstanceCreateFlags
        val mutable public pApplicationInfo : nativeptr<VkApplicationInfo>
        val mutable public enabledLayerCount : uint32
        val mutable public ppEnabledLayerNames : nativeptr<cstr>
        val mutable public enabledExtensionCount : uint32
        val mutable public ppEnabledExtensionNames : nativeptr<cstr>

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkInstanceCreateFlags
          , pApplicationInfo : nativeptr<VkApplicationInfo>
          , enabledLayerCount : uint32
          , ppEnabledLayerNames : nativeptr<cstr>
          , enabledExtensionCount : uint32
          , ppEnabledExtensionNames : nativeptr<cstr>
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                pApplicationInfo = pApplicationInfo
                enabledLayerCount = enabledLayerCount
                ppEnabledLayerNames = ppEnabledLayerNames
                enabledExtensionCount = enabledExtensionCount
                ppEnabledExtensionNames = ppEnabledExtensionNames
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "pApplicationInfo = %A" x.pApplicationInfo
                sprintf "enabledLayerCount = %A" x.enabledLayerCount
                sprintf "ppEnabledLayerNames = %A" x.ppEnabledLayerNames
                sprintf "enabledExtensionCount = %A" x.enabledExtensionCount
                sprintf "ppEnabledExtensionNames = %A" x.ppEnabledExtensionNames
            ] |> sprintf "VkInstanceCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkLayerProperties = 
    struct
        val mutable public layerName : String256
        val mutable public specVersion : uint32
        val mutable public implementationVersion : uint32
        val mutable public description : String256

        new(layerName : String256
          , specVersion : uint32
          , implementationVersion : uint32
          , description : String256
          ) =
            {
                layerName = layerName
                specVersion = specVersion
                implementationVersion = implementationVersion
                description = description
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "layerName = %A" x.layerName
                sprintf "specVersion = %A" x.specVersion
                sprintf "implementationVersion = %A" x.implementationVersion
                sprintf "description = %A" x.description
            ] |> sprintf "VkLayerProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkMappedMemoryRange = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public memory : VkDeviceMemory
        val mutable public offset : VkDeviceSize
        val mutable public size : VkDeviceSize

        new(sType : VkStructureType
          , pNext : nativeint
          , memory : VkDeviceMemory
          , offset : VkDeviceSize
          , size : VkDeviceSize
          ) =
            {
                sType = sType
                pNext = pNext
                memory = memory
                offset = offset
                size = size
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "memory = %A" x.memory
                sprintf "offset = %A" x.offset
                sprintf "size = %A" x.size
            ] |> sprintf "VkMappedMemoryRange { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkMemoryAllocateFlagsInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkMemoryAllocateFlags
        val mutable public deviceMask : uint32

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkMemoryAllocateFlags
          , deviceMask : uint32
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                deviceMask = deviceMask
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "deviceMask = %A" x.deviceMask
            ] |> sprintf "VkMemoryAllocateFlagsInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkMemoryAllocateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public allocationSize : VkDeviceSize
        val mutable public memoryTypeIndex : uint32

        new(sType : VkStructureType
          , pNext : nativeint
          , allocationSize : VkDeviceSize
          , memoryTypeIndex : uint32
          ) =
            {
                sType = sType
                pNext = pNext
                allocationSize = allocationSize
                memoryTypeIndex = memoryTypeIndex
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "allocationSize = %A" x.allocationSize
                sprintf "memoryTypeIndex = %A" x.memoryTypeIndex
            ] |> sprintf "VkMemoryAllocateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkMemoryBarrier = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public srcAccessMask : VkAccessFlags
        val mutable public dstAccessMask : VkAccessFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , srcAccessMask : VkAccessFlags
          , dstAccessMask : VkAccessFlags
          ) =
            {
                sType = sType
                pNext = pNext
                srcAccessMask = srcAccessMask
                dstAccessMask = dstAccessMask
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "srcAccessMask = %A" x.srcAccessMask
                sprintf "dstAccessMask = %A" x.dstAccessMask
            ] |> sprintf "VkMemoryBarrier { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkMemoryDedicatedAllocateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public image : VkImage
        val mutable public buffer : VkBuffer

        new(sType : VkStructureType
          , pNext : nativeint
          , image : VkImage
          , buffer : VkBuffer
          ) =
            {
                sType = sType
                pNext = pNext
                image = image
                buffer = buffer
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "image = %A" x.image
                sprintf "buffer = %A" x.buffer
            ] |> sprintf "VkMemoryDedicatedAllocateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkMemoryDedicatedRequirements = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public prefersDedicatedAllocation : VkBool32
        val mutable public requiresDedicatedAllocation : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , prefersDedicatedAllocation : VkBool32
          , requiresDedicatedAllocation : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                prefersDedicatedAllocation = prefersDedicatedAllocation
                requiresDedicatedAllocation = requiresDedicatedAllocation
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "prefersDedicatedAllocation = %A" x.prefersDedicatedAllocation
                sprintf "requiresDedicatedAllocation = %A" x.requiresDedicatedAllocation
            ] |> sprintf "VkMemoryDedicatedRequirements { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkMemoryHeap = 
    struct
        val mutable public size : VkDeviceSize
        val mutable public flags : VkMemoryHeapFlags

        new(size : VkDeviceSize
          , flags : VkMemoryHeapFlags
          ) =
            {
                size = size
                flags = flags
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "size = %A" x.size
                sprintf "flags = %A" x.flags
            ] |> sprintf "VkMemoryHeap { %s }"
    end

[<StructLayout(LayoutKind.Explicit, Size = 256)>]
type VkMemoryHeap_16 =
    struct
        [<FieldOffset(0)>]
        val mutable public First : VkMemoryHeap
        
        member x.Item
            with get (i : int) : VkMemoryHeap =
                if i < 0 || i > 15 then raise <| IndexOutOfRangeException()
                let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt
                NativePtr.get ptr i
            and set (i : int) (value : VkMemoryHeap) =
                if i < 0 || i > 15 then raise <| IndexOutOfRangeException()
                let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt
                NativePtr.set ptr i value

        member x.Length = 16

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = let x = x in (Seq.init 16 (fun i -> x.[i])).GetEnumerator() :> System.Collections.IEnumerator
        interface System.Collections.Generic.IEnumerable<VkMemoryHeap> with
            member x.GetEnumerator() = let x = x in (Seq.init 16 (fun i -> x.[i])).GetEnumerator()
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkMemoryRequirements = 
    struct
        val mutable public size : VkDeviceSize
        val mutable public alignment : VkDeviceSize
        val mutable public memoryTypeBits : uint32

        new(size : VkDeviceSize
          , alignment : VkDeviceSize
          , memoryTypeBits : uint32
          ) =
            {
                size = size
                alignment = alignment
                memoryTypeBits = memoryTypeBits
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "size = %A" x.size
                sprintf "alignment = %A" x.alignment
                sprintf "memoryTypeBits = %A" x.memoryTypeBits
            ] |> sprintf "VkMemoryRequirements { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkMemoryRequirements2 = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public memoryRequirements : VkMemoryRequirements

        new(sType : VkStructureType
          , pNext : nativeint
          , memoryRequirements : VkMemoryRequirements
          ) =
            {
                sType = sType
                pNext = pNext
                memoryRequirements = memoryRequirements
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "memoryRequirements = %A" x.memoryRequirements
            ] |> sprintf "VkMemoryRequirements2 { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkMemoryType = 
    struct
        val mutable public propertyFlags : VkMemoryPropertyFlags
        val mutable public heapIndex : uint32

        new(propertyFlags : VkMemoryPropertyFlags
          , heapIndex : uint32
          ) =
            {
                propertyFlags = propertyFlags
                heapIndex = heapIndex
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "propertyFlags = %A" x.propertyFlags
                sprintf "heapIndex = %A" x.heapIndex
            ] |> sprintf "VkMemoryType { %s }"
    end

[<StructLayout(LayoutKind.Explicit, Size = 256)>]
type VkMemoryType_32 =
    struct
        [<FieldOffset(0)>]
        val mutable public First : VkMemoryType
        
        member x.Item
            with get (i : int) : VkMemoryType =
                if i < 0 || i > 31 then raise <| IndexOutOfRangeException()
                let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt
                NativePtr.get ptr i
            and set (i : int) (value : VkMemoryType) =
                if i < 0 || i > 31 then raise <| IndexOutOfRangeException()
                let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt
                NativePtr.set ptr i value

        member x.Length = 32

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = let x = x in (Seq.init 32 (fun i -> x.[i])).GetEnumerator() :> System.Collections.IEnumerator
        interface System.Collections.Generic.IEnumerable<VkMemoryType> with
            member x.GetEnumerator() = let x = x in (Seq.init 32 (fun i -> x.[i])).GetEnumerator()
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDevice16BitStorageFeatures = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public storageBuffer16BitAccess : VkBool32
        val mutable public uniformAndStorageBuffer16BitAccess : VkBool32
        val mutable public storagePushConstant16 : VkBool32
        val mutable public storageInputOutput16 : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , storageBuffer16BitAccess : VkBool32
          , uniformAndStorageBuffer16BitAccess : VkBool32
          , storagePushConstant16 : VkBool32
          , storageInputOutput16 : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                storageBuffer16BitAccess = storageBuffer16BitAccess
                uniformAndStorageBuffer16BitAccess = uniformAndStorageBuffer16BitAccess
                storagePushConstant16 = storagePushConstant16
                storageInputOutput16 = storageInputOutput16
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "storageBuffer16BitAccess = %A" x.storageBuffer16BitAccess
                sprintf "uniformAndStorageBuffer16BitAccess = %A" x.uniformAndStorageBuffer16BitAccess
                sprintf "storagePushConstant16 = %A" x.storagePushConstant16
                sprintf "storageInputOutput16 = %A" x.storageInputOutput16
            ] |> sprintf "VkPhysicalDevice16BitStorageFeatures { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceExternalBufferInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkBufferCreateFlags
        val mutable public usage : VkBufferUsageFlags
        val mutable public handleType : VkExternalMemoryHandleTypeFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkBufferCreateFlags
          , usage : VkBufferUsageFlags
          , handleType : VkExternalMemoryHandleTypeFlags
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                usage = usage
                handleType = handleType
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "usage = %A" x.usage
                sprintf "handleType = %A" x.handleType
            ] |> sprintf "VkPhysicalDeviceExternalBufferInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceExternalFenceInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public handleType : VkExternalFenceHandleTypeFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , handleType : VkExternalFenceHandleTypeFlags
          ) =
            {
                sType = sType
                pNext = pNext
                handleType = handleType
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "handleType = %A" x.handleType
            ] |> sprintf "VkPhysicalDeviceExternalFenceInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceExternalImageFormatInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public handleType : VkExternalMemoryHandleTypeFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , handleType : VkExternalMemoryHandleTypeFlags
          ) =
            {
                sType = sType
                pNext = pNext
                handleType = handleType
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "handleType = %A" x.handleType
            ] |> sprintf "VkPhysicalDeviceExternalImageFormatInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceExternalSemaphoreInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public handleType : VkExternalSemaphoreHandleTypeFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , handleType : VkExternalSemaphoreHandleTypeFlags
          ) =
            {
                sType = sType
                pNext = pNext
                handleType = handleType
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "handleType = %A" x.handleType
            ] |> sprintf "VkPhysicalDeviceExternalSemaphoreInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceFeatures2 = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public features : VkPhysicalDeviceFeatures

        new(sType : VkStructureType
          , pNext : nativeint
          , features : VkPhysicalDeviceFeatures
          ) =
            {
                sType = sType
                pNext = pNext
                features = features
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "features = %A" x.features
            ] |> sprintf "VkPhysicalDeviceFeatures2 { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceGroupProperties = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public physicalDeviceCount : uint32
        val mutable public physicalDevices : VkPhysicalDevice_32
        val mutable public subsetAllocation : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , physicalDeviceCount : uint32
          , physicalDevices : VkPhysicalDevice_32
          , subsetAllocation : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                physicalDeviceCount = physicalDeviceCount
                physicalDevices = physicalDevices
                subsetAllocation = subsetAllocation
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "physicalDeviceCount = %A" x.physicalDeviceCount
                sprintf "physicalDevices = %A" x.physicalDevices
                sprintf "subsetAllocation = %A" x.subsetAllocation
            ] |> sprintf "VkPhysicalDeviceGroupProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceIDProperties = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public deviceUUID : Guid
        val mutable public driverUUID : Guid
        val mutable public deviceLUID : byte_8
        val mutable public deviceNodeMask : uint32
        val mutable public deviceLUIDValid : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , deviceUUID : Guid
          , driverUUID : Guid
          , deviceLUID : byte_8
          , deviceNodeMask : uint32
          , deviceLUIDValid : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                deviceUUID = deviceUUID
                driverUUID = driverUUID
                deviceLUID = deviceLUID
                deviceNodeMask = deviceNodeMask
                deviceLUIDValid = deviceLUIDValid
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "deviceUUID = %A" x.deviceUUID
                sprintf "driverUUID = %A" x.driverUUID
                sprintf "deviceLUID = %A" x.deviceLUID
                sprintf "deviceNodeMask = %A" x.deviceNodeMask
                sprintf "deviceLUIDValid = %A" x.deviceLUIDValid
            ] |> sprintf "VkPhysicalDeviceIDProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceImageFormatInfo2 = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public format : VkFormat
        val mutable public _type : VkImageType
        val mutable public tiling : VkImageTiling
        val mutable public usage : VkImageUsageFlags
        val mutable public flags : VkImageCreateFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , format : VkFormat
          , _type : VkImageType
          , tiling : VkImageTiling
          , usage : VkImageUsageFlags
          , flags : VkImageCreateFlags
          ) =
            {
                sType = sType
                pNext = pNext
                format = format
                _type = _type
                tiling = tiling
                usage = usage
                flags = flags
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "format = %A" x.format
                sprintf "_type = %A" x._type
                sprintf "tiling = %A" x.tiling
                sprintf "usage = %A" x.usage
                sprintf "flags = %A" x.flags
            ] |> sprintf "VkPhysicalDeviceImageFormatInfo2 { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceLimits = 
    struct
        val mutable public maxImageDimension1D : uint32
        val mutable public maxImageDimension2D : uint32
        val mutable public maxImageDimension3D : uint32
        val mutable public maxImageDimensionCube : uint32
        val mutable public maxImageArrayLayers : uint32
        val mutable public maxTexelBufferElements : uint32
        val mutable public maxUniformBufferRange : uint32
        val mutable public maxStorageBufferRange : uint32
        val mutable public maxPushConstantsSize : uint32
        val mutable public maxMemoryAllocationCount : uint32
        val mutable public maxSamplerAllocationCount : uint32
        val mutable public bufferImageGranularity : VkDeviceSize
        val mutable public sparseAddressSpaceSize : VkDeviceSize
        val mutable public maxBoundDescriptorSets : uint32
        val mutable public maxPerStageDescriptorSamplers : uint32
        val mutable public maxPerStageDescriptorUniformBuffers : uint32
        val mutable public maxPerStageDescriptorStorageBuffers : uint32
        val mutable public maxPerStageDescriptorSampledImages : uint32
        val mutable public maxPerStageDescriptorStorageImages : uint32
        val mutable public maxPerStageDescriptorInputAttachments : uint32
        val mutable public maxPerStageResources : uint32
        val mutable public maxDescriptorSetSamplers : uint32
        val mutable public maxDescriptorSetUniformBuffers : uint32
        val mutable public maxDescriptorSetUniformBuffersDynamic : uint32
        val mutable public maxDescriptorSetStorageBuffers : uint32
        val mutable public maxDescriptorSetStorageBuffersDynamic : uint32
        val mutable public maxDescriptorSetSampledImages : uint32
        val mutable public maxDescriptorSetStorageImages : uint32
        val mutable public maxDescriptorSetInputAttachments : uint32
        val mutable public maxVertexInputAttributes : uint32
        val mutable public maxVertexInputBindings : uint32
        val mutable public maxVertexInputAttributeOffset : uint32
        val mutable public maxVertexInputBindingStride : uint32
        val mutable public maxVertexOutputComponents : uint32
        val mutable public maxTessellationGenerationLevel : uint32
        val mutable public maxTessellationPatchSize : uint32
        val mutable public maxTessellationControlPerVertexInputComponents : uint32
        val mutable public maxTessellationControlPerVertexOutputComponents : uint32
        val mutable public maxTessellationControlPerPatchOutputComponents : uint32
        val mutable public maxTessellationControlTotalOutputComponents : uint32
        val mutable public maxTessellationEvaluationInputComponents : uint32
        val mutable public maxTessellationEvaluationOutputComponents : uint32
        val mutable public maxGeometryShaderInvocations : uint32
        val mutable public maxGeometryInputComponents : uint32
        val mutable public maxGeometryOutputComponents : uint32
        val mutable public maxGeometryOutputVertices : uint32
        val mutable public maxGeometryTotalOutputComponents : uint32
        val mutable public maxFragmentInputComponents : uint32
        val mutable public maxFragmentOutputAttachments : uint32
        val mutable public maxFragmentDualSrcAttachments : uint32
        val mutable public maxFragmentCombinedOutputResources : uint32
        val mutable public maxComputeSharedMemorySize : uint32
        val mutable public maxComputeWorkGroupCount : V3ui
        val mutable public maxComputeWorkGroupInvocations : uint32
        val mutable public maxComputeWorkGroupSize : V3ui
        val mutable public subPixelPrecisionBits : uint32
        val mutable public subTexelPrecisionBits : uint32
        val mutable public mipmapPrecisionBits : uint32
        val mutable public maxDrawIndexedIndexValue : uint32
        val mutable public maxDrawIndirectCount : uint32
        val mutable public maxSamplerLodBias : float32
        val mutable public maxSamplerAnisotropy : float32
        val mutable public maxViewports : uint32
        val mutable public maxViewportDimensions : V2ui
        val mutable public viewportBoundsRange : V2f
        val mutable public viewportSubPixelBits : uint32
        val mutable public minMemoryMapAlignment : uint64
        val mutable public minTexelBufferOffsetAlignment : VkDeviceSize
        val mutable public minUniformBufferOffsetAlignment : VkDeviceSize
        val mutable public minStorageBufferOffsetAlignment : VkDeviceSize
        val mutable public minTexelOffset : int
        val mutable public maxTexelOffset : uint32
        val mutable public minTexelGatherOffset : int
        val mutable public maxTexelGatherOffset : uint32
        val mutable public minInterpolationOffset : float32
        val mutable public maxInterpolationOffset : float32
        val mutable public subPixelInterpolationOffsetBits : uint32
        val mutable public maxFramebufferWidth : uint32
        val mutable public maxFramebufferHeight : uint32
        val mutable public maxFramebufferLayers : uint32
        val mutable public framebufferColorSampleCounts : VkSampleCountFlags
        val mutable public framebufferDepthSampleCounts : VkSampleCountFlags
        val mutable public framebufferStencilSampleCounts : VkSampleCountFlags
        val mutable public framebufferNoAttachmentsSampleCounts : VkSampleCountFlags
        val mutable public maxColorAttachments : uint32
        val mutable public sampledImageColorSampleCounts : VkSampleCountFlags
        val mutable public sampledImageIntegerSampleCounts : VkSampleCountFlags
        val mutable public sampledImageDepthSampleCounts : VkSampleCountFlags
        val mutable public sampledImageStencilSampleCounts : VkSampleCountFlags
        val mutable public storageImageSampleCounts : VkSampleCountFlags
        val mutable public maxSampleMaskWords : uint32
        val mutable public timestampComputeAndGraphics : VkBool32
        val mutable public timestampPeriod : float32
        val mutable public maxClipDistances : uint32
        val mutable public maxCullDistances : uint32
        val mutable public maxCombinedClipAndCullDistances : uint32
        val mutable public discreteQueuePriorities : uint32
        val mutable public pointSizeRange : V2f
        val mutable public lineWidthRange : V2f
        val mutable public pointSizeGranularity : float32
        val mutable public lineWidthGranularity : float32
        val mutable public strictLines : VkBool32
        val mutable public standardSampleLocations : VkBool32
        val mutable public optimalBufferCopyOffsetAlignment : VkDeviceSize
        val mutable public optimalBufferCopyRowPitchAlignment : VkDeviceSize
        val mutable public nonCoherentAtomSize : VkDeviceSize

        new(maxImageDimension1D : uint32
          , maxImageDimension2D : uint32
          , maxImageDimension3D : uint32
          , maxImageDimensionCube : uint32
          , maxImageArrayLayers : uint32
          , maxTexelBufferElements : uint32
          , maxUniformBufferRange : uint32
          , maxStorageBufferRange : uint32
          , maxPushConstantsSize : uint32
          , maxMemoryAllocationCount : uint32
          , maxSamplerAllocationCount : uint32
          , bufferImageGranularity : VkDeviceSize
          , sparseAddressSpaceSize : VkDeviceSize
          , maxBoundDescriptorSets : uint32
          , maxPerStageDescriptorSamplers : uint32
          , maxPerStageDescriptorUniformBuffers : uint32
          , maxPerStageDescriptorStorageBuffers : uint32
          , maxPerStageDescriptorSampledImages : uint32
          , maxPerStageDescriptorStorageImages : uint32
          , maxPerStageDescriptorInputAttachments : uint32
          , maxPerStageResources : uint32
          , maxDescriptorSetSamplers : uint32
          , maxDescriptorSetUniformBuffers : uint32
          , maxDescriptorSetUniformBuffersDynamic : uint32
          , maxDescriptorSetStorageBuffers : uint32
          , maxDescriptorSetStorageBuffersDynamic : uint32
          , maxDescriptorSetSampledImages : uint32
          , maxDescriptorSetStorageImages : uint32
          , maxDescriptorSetInputAttachments : uint32
          , maxVertexInputAttributes : uint32
          , maxVertexInputBindings : uint32
          , maxVertexInputAttributeOffset : uint32
          , maxVertexInputBindingStride : uint32
          , maxVertexOutputComponents : uint32
          , maxTessellationGenerationLevel : uint32
          , maxTessellationPatchSize : uint32
          , maxTessellationControlPerVertexInputComponents : uint32
          , maxTessellationControlPerVertexOutputComponents : uint32
          , maxTessellationControlPerPatchOutputComponents : uint32
          , maxTessellationControlTotalOutputComponents : uint32
          , maxTessellationEvaluationInputComponents : uint32
          , maxTessellationEvaluationOutputComponents : uint32
          , maxGeometryShaderInvocations : uint32
          , maxGeometryInputComponents : uint32
          , maxGeometryOutputComponents : uint32
          , maxGeometryOutputVertices : uint32
          , maxGeometryTotalOutputComponents : uint32
          , maxFragmentInputComponents : uint32
          , maxFragmentOutputAttachments : uint32
          , maxFragmentDualSrcAttachments : uint32
          , maxFragmentCombinedOutputResources : uint32
          , maxComputeSharedMemorySize : uint32
          , maxComputeWorkGroupCount : V3ui
          , maxComputeWorkGroupInvocations : uint32
          , maxComputeWorkGroupSize : V3ui
          , subPixelPrecisionBits : uint32
          , subTexelPrecisionBits : uint32
          , mipmapPrecisionBits : uint32
          , maxDrawIndexedIndexValue : uint32
          , maxDrawIndirectCount : uint32
          , maxSamplerLodBias : float32
          , maxSamplerAnisotropy : float32
          , maxViewports : uint32
          , maxViewportDimensions : V2ui
          , viewportBoundsRange : V2f
          , viewportSubPixelBits : uint32
          , minMemoryMapAlignment : uint64
          , minTexelBufferOffsetAlignment : VkDeviceSize
          , minUniformBufferOffsetAlignment : VkDeviceSize
          , minStorageBufferOffsetAlignment : VkDeviceSize
          , minTexelOffset : int
          , maxTexelOffset : uint32
          , minTexelGatherOffset : int
          , maxTexelGatherOffset : uint32
          , minInterpolationOffset : float32
          , maxInterpolationOffset : float32
          , subPixelInterpolationOffsetBits : uint32
          , maxFramebufferWidth : uint32
          , maxFramebufferHeight : uint32
          , maxFramebufferLayers : uint32
          , framebufferColorSampleCounts : VkSampleCountFlags
          , framebufferDepthSampleCounts : VkSampleCountFlags
          , framebufferStencilSampleCounts : VkSampleCountFlags
          , framebufferNoAttachmentsSampleCounts : VkSampleCountFlags
          , maxColorAttachments : uint32
          , sampledImageColorSampleCounts : VkSampleCountFlags
          , sampledImageIntegerSampleCounts : VkSampleCountFlags
          , sampledImageDepthSampleCounts : VkSampleCountFlags
          , sampledImageStencilSampleCounts : VkSampleCountFlags
          , storageImageSampleCounts : VkSampleCountFlags
          , maxSampleMaskWords : uint32
          , timestampComputeAndGraphics : VkBool32
          , timestampPeriod : float32
          , maxClipDistances : uint32
          , maxCullDistances : uint32
          , maxCombinedClipAndCullDistances : uint32
          , discreteQueuePriorities : uint32
          , pointSizeRange : V2f
          , lineWidthRange : V2f
          , pointSizeGranularity : float32
          , lineWidthGranularity : float32
          , strictLines : VkBool32
          , standardSampleLocations : VkBool32
          , optimalBufferCopyOffsetAlignment : VkDeviceSize
          , optimalBufferCopyRowPitchAlignment : VkDeviceSize
          , nonCoherentAtomSize : VkDeviceSize
          ) =
            {
                maxImageDimension1D = maxImageDimension1D
                maxImageDimension2D = maxImageDimension2D
                maxImageDimension3D = maxImageDimension3D
                maxImageDimensionCube = maxImageDimensionCube
                maxImageArrayLayers = maxImageArrayLayers
                maxTexelBufferElements = maxTexelBufferElements
                maxUniformBufferRange = maxUniformBufferRange
                maxStorageBufferRange = maxStorageBufferRange
                maxPushConstantsSize = maxPushConstantsSize
                maxMemoryAllocationCount = maxMemoryAllocationCount
                maxSamplerAllocationCount = maxSamplerAllocationCount
                bufferImageGranularity = bufferImageGranularity
                sparseAddressSpaceSize = sparseAddressSpaceSize
                maxBoundDescriptorSets = maxBoundDescriptorSets
                maxPerStageDescriptorSamplers = maxPerStageDescriptorSamplers
                maxPerStageDescriptorUniformBuffers = maxPerStageDescriptorUniformBuffers
                maxPerStageDescriptorStorageBuffers = maxPerStageDescriptorStorageBuffers
                maxPerStageDescriptorSampledImages = maxPerStageDescriptorSampledImages
                maxPerStageDescriptorStorageImages = maxPerStageDescriptorStorageImages
                maxPerStageDescriptorInputAttachments = maxPerStageDescriptorInputAttachments
                maxPerStageResources = maxPerStageResources
                maxDescriptorSetSamplers = maxDescriptorSetSamplers
                maxDescriptorSetUniformBuffers = maxDescriptorSetUniformBuffers
                maxDescriptorSetUniformBuffersDynamic = maxDescriptorSetUniformBuffersDynamic
                maxDescriptorSetStorageBuffers = maxDescriptorSetStorageBuffers
                maxDescriptorSetStorageBuffersDynamic = maxDescriptorSetStorageBuffersDynamic
                maxDescriptorSetSampledImages = maxDescriptorSetSampledImages
                maxDescriptorSetStorageImages = maxDescriptorSetStorageImages
                maxDescriptorSetInputAttachments = maxDescriptorSetInputAttachments
                maxVertexInputAttributes = maxVertexInputAttributes
                maxVertexInputBindings = maxVertexInputBindings
                maxVertexInputAttributeOffset = maxVertexInputAttributeOffset
                maxVertexInputBindingStride = maxVertexInputBindingStride
                maxVertexOutputComponents = maxVertexOutputComponents
                maxTessellationGenerationLevel = maxTessellationGenerationLevel
                maxTessellationPatchSize = maxTessellationPatchSize
                maxTessellationControlPerVertexInputComponents = maxTessellationControlPerVertexInputComponents
                maxTessellationControlPerVertexOutputComponents = maxTessellationControlPerVertexOutputComponents
                maxTessellationControlPerPatchOutputComponents = maxTessellationControlPerPatchOutputComponents
                maxTessellationControlTotalOutputComponents = maxTessellationControlTotalOutputComponents
                maxTessellationEvaluationInputComponents = maxTessellationEvaluationInputComponents
                maxTessellationEvaluationOutputComponents = maxTessellationEvaluationOutputComponents
                maxGeometryShaderInvocations = maxGeometryShaderInvocations
                maxGeometryInputComponents = maxGeometryInputComponents
                maxGeometryOutputComponents = maxGeometryOutputComponents
                maxGeometryOutputVertices = maxGeometryOutputVertices
                maxGeometryTotalOutputComponents = maxGeometryTotalOutputComponents
                maxFragmentInputComponents = maxFragmentInputComponents
                maxFragmentOutputAttachments = maxFragmentOutputAttachments
                maxFragmentDualSrcAttachments = maxFragmentDualSrcAttachments
                maxFragmentCombinedOutputResources = maxFragmentCombinedOutputResources
                maxComputeSharedMemorySize = maxComputeSharedMemorySize
                maxComputeWorkGroupCount = maxComputeWorkGroupCount
                maxComputeWorkGroupInvocations = maxComputeWorkGroupInvocations
                maxComputeWorkGroupSize = maxComputeWorkGroupSize
                subPixelPrecisionBits = subPixelPrecisionBits
                subTexelPrecisionBits = subTexelPrecisionBits
                mipmapPrecisionBits = mipmapPrecisionBits
                maxDrawIndexedIndexValue = maxDrawIndexedIndexValue
                maxDrawIndirectCount = maxDrawIndirectCount
                maxSamplerLodBias = maxSamplerLodBias
                maxSamplerAnisotropy = maxSamplerAnisotropy
                maxViewports = maxViewports
                maxViewportDimensions = maxViewportDimensions
                viewportBoundsRange = viewportBoundsRange
                viewportSubPixelBits = viewportSubPixelBits
                minMemoryMapAlignment = minMemoryMapAlignment
                minTexelBufferOffsetAlignment = minTexelBufferOffsetAlignment
                minUniformBufferOffsetAlignment = minUniformBufferOffsetAlignment
                minStorageBufferOffsetAlignment = minStorageBufferOffsetAlignment
                minTexelOffset = minTexelOffset
                maxTexelOffset = maxTexelOffset
                minTexelGatherOffset = minTexelGatherOffset
                maxTexelGatherOffset = maxTexelGatherOffset
                minInterpolationOffset = minInterpolationOffset
                maxInterpolationOffset = maxInterpolationOffset
                subPixelInterpolationOffsetBits = subPixelInterpolationOffsetBits
                maxFramebufferWidth = maxFramebufferWidth
                maxFramebufferHeight = maxFramebufferHeight
                maxFramebufferLayers = maxFramebufferLayers
                framebufferColorSampleCounts = framebufferColorSampleCounts
                framebufferDepthSampleCounts = framebufferDepthSampleCounts
                framebufferStencilSampleCounts = framebufferStencilSampleCounts
                framebufferNoAttachmentsSampleCounts = framebufferNoAttachmentsSampleCounts
                maxColorAttachments = maxColorAttachments
                sampledImageColorSampleCounts = sampledImageColorSampleCounts
                sampledImageIntegerSampleCounts = sampledImageIntegerSampleCounts
                sampledImageDepthSampleCounts = sampledImageDepthSampleCounts
                sampledImageStencilSampleCounts = sampledImageStencilSampleCounts
                storageImageSampleCounts = storageImageSampleCounts
                maxSampleMaskWords = maxSampleMaskWords
                timestampComputeAndGraphics = timestampComputeAndGraphics
                timestampPeriod = timestampPeriod
                maxClipDistances = maxClipDistances
                maxCullDistances = maxCullDistances
                maxCombinedClipAndCullDistances = maxCombinedClipAndCullDistances
                discreteQueuePriorities = discreteQueuePriorities
                pointSizeRange = pointSizeRange
                lineWidthRange = lineWidthRange
                pointSizeGranularity = pointSizeGranularity
                lineWidthGranularity = lineWidthGranularity
                strictLines = strictLines
                standardSampleLocations = standardSampleLocations
                optimalBufferCopyOffsetAlignment = optimalBufferCopyOffsetAlignment
                optimalBufferCopyRowPitchAlignment = optimalBufferCopyRowPitchAlignment
                nonCoherentAtomSize = nonCoherentAtomSize
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "maxImageDimension1D = %A" x.maxImageDimension1D
                sprintf "maxImageDimension2D = %A" x.maxImageDimension2D
                sprintf "maxImageDimension3D = %A" x.maxImageDimension3D
                sprintf "maxImageDimensionCube = %A" x.maxImageDimensionCube
                sprintf "maxImageArrayLayers = %A" x.maxImageArrayLayers
                sprintf "maxTexelBufferElements = %A" x.maxTexelBufferElements
                sprintf "maxUniformBufferRange = %A" x.maxUniformBufferRange
                sprintf "maxStorageBufferRange = %A" x.maxStorageBufferRange
                sprintf "maxPushConstantsSize = %A" x.maxPushConstantsSize
                sprintf "maxMemoryAllocationCount = %A" x.maxMemoryAllocationCount
                sprintf "maxSamplerAllocationCount = %A" x.maxSamplerAllocationCount
                sprintf "bufferImageGranularity = %A" x.bufferImageGranularity
                sprintf "sparseAddressSpaceSize = %A" x.sparseAddressSpaceSize
                sprintf "maxBoundDescriptorSets = %A" x.maxBoundDescriptorSets
                sprintf "maxPerStageDescriptorSamplers = %A" x.maxPerStageDescriptorSamplers
                sprintf "maxPerStageDescriptorUniformBuffers = %A" x.maxPerStageDescriptorUniformBuffers
                sprintf "maxPerStageDescriptorStorageBuffers = %A" x.maxPerStageDescriptorStorageBuffers
                sprintf "maxPerStageDescriptorSampledImages = %A" x.maxPerStageDescriptorSampledImages
                sprintf "maxPerStageDescriptorStorageImages = %A" x.maxPerStageDescriptorStorageImages
                sprintf "maxPerStageDescriptorInputAttachments = %A" x.maxPerStageDescriptorInputAttachments
                sprintf "maxPerStageResources = %A" x.maxPerStageResources
                sprintf "maxDescriptorSetSamplers = %A" x.maxDescriptorSetSamplers
                sprintf "maxDescriptorSetUniformBuffers = %A" x.maxDescriptorSetUniformBuffers
                sprintf "maxDescriptorSetUniformBuffersDynamic = %A" x.maxDescriptorSetUniformBuffersDynamic
                sprintf "maxDescriptorSetStorageBuffers = %A" x.maxDescriptorSetStorageBuffers
                sprintf "maxDescriptorSetStorageBuffersDynamic = %A" x.maxDescriptorSetStorageBuffersDynamic
                sprintf "maxDescriptorSetSampledImages = %A" x.maxDescriptorSetSampledImages
                sprintf "maxDescriptorSetStorageImages = %A" x.maxDescriptorSetStorageImages
                sprintf "maxDescriptorSetInputAttachments = %A" x.maxDescriptorSetInputAttachments
                sprintf "maxVertexInputAttributes = %A" x.maxVertexInputAttributes
                sprintf "maxVertexInputBindings = %A" x.maxVertexInputBindings
                sprintf "maxVertexInputAttributeOffset = %A" x.maxVertexInputAttributeOffset
                sprintf "maxVertexInputBindingStride = %A" x.maxVertexInputBindingStride
                sprintf "maxVertexOutputComponents = %A" x.maxVertexOutputComponents
                sprintf "maxTessellationGenerationLevel = %A" x.maxTessellationGenerationLevel
                sprintf "maxTessellationPatchSize = %A" x.maxTessellationPatchSize
                sprintf "maxTessellationControlPerVertexInputComponents = %A" x.maxTessellationControlPerVertexInputComponents
                sprintf "maxTessellationControlPerVertexOutputComponents = %A" x.maxTessellationControlPerVertexOutputComponents
                sprintf "maxTessellationControlPerPatchOutputComponents = %A" x.maxTessellationControlPerPatchOutputComponents
                sprintf "maxTessellationControlTotalOutputComponents = %A" x.maxTessellationControlTotalOutputComponents
                sprintf "maxTessellationEvaluationInputComponents = %A" x.maxTessellationEvaluationInputComponents
                sprintf "maxTessellationEvaluationOutputComponents = %A" x.maxTessellationEvaluationOutputComponents
                sprintf "maxGeometryShaderInvocations = %A" x.maxGeometryShaderInvocations
                sprintf "maxGeometryInputComponents = %A" x.maxGeometryInputComponents
                sprintf "maxGeometryOutputComponents = %A" x.maxGeometryOutputComponents
                sprintf "maxGeometryOutputVertices = %A" x.maxGeometryOutputVertices
                sprintf "maxGeometryTotalOutputComponents = %A" x.maxGeometryTotalOutputComponents
                sprintf "maxFragmentInputComponents = %A" x.maxFragmentInputComponents
                sprintf "maxFragmentOutputAttachments = %A" x.maxFragmentOutputAttachments
                sprintf "maxFragmentDualSrcAttachments = %A" x.maxFragmentDualSrcAttachments
                sprintf "maxFragmentCombinedOutputResources = %A" x.maxFragmentCombinedOutputResources
                sprintf "maxComputeSharedMemorySize = %A" x.maxComputeSharedMemorySize
                sprintf "maxComputeWorkGroupCount = %A" x.maxComputeWorkGroupCount
                sprintf "maxComputeWorkGroupInvocations = %A" x.maxComputeWorkGroupInvocations
                sprintf "maxComputeWorkGroupSize = %A" x.maxComputeWorkGroupSize
                sprintf "subPixelPrecisionBits = %A" x.subPixelPrecisionBits
                sprintf "subTexelPrecisionBits = %A" x.subTexelPrecisionBits
                sprintf "mipmapPrecisionBits = %A" x.mipmapPrecisionBits
                sprintf "maxDrawIndexedIndexValue = %A" x.maxDrawIndexedIndexValue
                sprintf "maxDrawIndirectCount = %A" x.maxDrawIndirectCount
                sprintf "maxSamplerLodBias = %A" x.maxSamplerLodBias
                sprintf "maxSamplerAnisotropy = %A" x.maxSamplerAnisotropy
                sprintf "maxViewports = %A" x.maxViewports
                sprintf "maxViewportDimensions = %A" x.maxViewportDimensions
                sprintf "viewportBoundsRange = %A" x.viewportBoundsRange
                sprintf "viewportSubPixelBits = %A" x.viewportSubPixelBits
                sprintf "minMemoryMapAlignment = %A" x.minMemoryMapAlignment
                sprintf "minTexelBufferOffsetAlignment = %A" x.minTexelBufferOffsetAlignment
                sprintf "minUniformBufferOffsetAlignment = %A" x.minUniformBufferOffsetAlignment
                sprintf "minStorageBufferOffsetAlignment = %A" x.minStorageBufferOffsetAlignment
                sprintf "minTexelOffset = %A" x.minTexelOffset
                sprintf "maxTexelOffset = %A" x.maxTexelOffset
                sprintf "minTexelGatherOffset = %A" x.minTexelGatherOffset
                sprintf "maxTexelGatherOffset = %A" x.maxTexelGatherOffset
                sprintf "minInterpolationOffset = %A" x.minInterpolationOffset
                sprintf "maxInterpolationOffset = %A" x.maxInterpolationOffset
                sprintf "subPixelInterpolationOffsetBits = %A" x.subPixelInterpolationOffsetBits
                sprintf "maxFramebufferWidth = %A" x.maxFramebufferWidth
                sprintf "maxFramebufferHeight = %A" x.maxFramebufferHeight
                sprintf "maxFramebufferLayers = %A" x.maxFramebufferLayers
                sprintf "framebufferColorSampleCounts = %A" x.framebufferColorSampleCounts
                sprintf "framebufferDepthSampleCounts = %A" x.framebufferDepthSampleCounts
                sprintf "framebufferStencilSampleCounts = %A" x.framebufferStencilSampleCounts
                sprintf "framebufferNoAttachmentsSampleCounts = %A" x.framebufferNoAttachmentsSampleCounts
                sprintf "maxColorAttachments = %A" x.maxColorAttachments
                sprintf "sampledImageColorSampleCounts = %A" x.sampledImageColorSampleCounts
                sprintf "sampledImageIntegerSampleCounts = %A" x.sampledImageIntegerSampleCounts
                sprintf "sampledImageDepthSampleCounts = %A" x.sampledImageDepthSampleCounts
                sprintf "sampledImageStencilSampleCounts = %A" x.sampledImageStencilSampleCounts
                sprintf "storageImageSampleCounts = %A" x.storageImageSampleCounts
                sprintf "maxSampleMaskWords = %A" x.maxSampleMaskWords
                sprintf "timestampComputeAndGraphics = %A" x.timestampComputeAndGraphics
                sprintf "timestampPeriod = %A" x.timestampPeriod
                sprintf "maxClipDistances = %A" x.maxClipDistances
                sprintf "maxCullDistances = %A" x.maxCullDistances
                sprintf "maxCombinedClipAndCullDistances = %A" x.maxCombinedClipAndCullDistances
                sprintf "discreteQueuePriorities = %A" x.discreteQueuePriorities
                sprintf "pointSizeRange = %A" x.pointSizeRange
                sprintf "lineWidthRange = %A" x.lineWidthRange
                sprintf "pointSizeGranularity = %A" x.pointSizeGranularity
                sprintf "lineWidthGranularity = %A" x.lineWidthGranularity
                sprintf "strictLines = %A" x.strictLines
                sprintf "standardSampleLocations = %A" x.standardSampleLocations
                sprintf "optimalBufferCopyOffsetAlignment = %A" x.optimalBufferCopyOffsetAlignment
                sprintf "optimalBufferCopyRowPitchAlignment = %A" x.optimalBufferCopyRowPitchAlignment
                sprintf "nonCoherentAtomSize = %A" x.nonCoherentAtomSize
            ] |> sprintf "VkPhysicalDeviceLimits { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceMaintenance3Properties = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public maxPerSetDescriptors : uint32
        val mutable public maxMemoryAllocationSize : VkDeviceSize

        new(sType : VkStructureType
          , pNext : nativeint
          , maxPerSetDescriptors : uint32
          , maxMemoryAllocationSize : VkDeviceSize
          ) =
            {
                sType = sType
                pNext = pNext
                maxPerSetDescriptors = maxPerSetDescriptors
                maxMemoryAllocationSize = maxMemoryAllocationSize
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "maxPerSetDescriptors = %A" x.maxPerSetDescriptors
                sprintf "maxMemoryAllocationSize = %A" x.maxMemoryAllocationSize
            ] |> sprintf "VkPhysicalDeviceMaintenance3Properties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceMemoryProperties = 
    struct
        val mutable public memoryTypeCount : uint32
        val mutable public memoryTypes : VkMemoryType_32
        val mutable public memoryHeapCount : uint32
        val mutable public memoryHeaps : VkMemoryHeap_16

        new(memoryTypeCount : uint32
          , memoryTypes : VkMemoryType_32
          , memoryHeapCount : uint32
          , memoryHeaps : VkMemoryHeap_16
          ) =
            {
                memoryTypeCount = memoryTypeCount
                memoryTypes = memoryTypes
                memoryHeapCount = memoryHeapCount
                memoryHeaps = memoryHeaps
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "memoryTypeCount = %A" x.memoryTypeCount
                sprintf "memoryTypes = %A" x.memoryTypes
                sprintf "memoryHeapCount = %A" x.memoryHeapCount
                sprintf "memoryHeaps = %A" x.memoryHeaps
            ] |> sprintf "VkPhysicalDeviceMemoryProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceMemoryProperties2 = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public memoryProperties : VkPhysicalDeviceMemoryProperties

        new(sType : VkStructureType
          , pNext : nativeint
          , memoryProperties : VkPhysicalDeviceMemoryProperties
          ) =
            {
                sType = sType
                pNext = pNext
                memoryProperties = memoryProperties
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "memoryProperties = %A" x.memoryProperties
            ] |> sprintf "VkPhysicalDeviceMemoryProperties2 { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceMultiviewFeatures = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public multiview : VkBool32
        val mutable public multiviewGeometryShader : VkBool32
        val mutable public multiviewTessellationShader : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , multiview : VkBool32
          , multiviewGeometryShader : VkBool32
          , multiviewTessellationShader : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                multiview = multiview
                multiviewGeometryShader = multiviewGeometryShader
                multiviewTessellationShader = multiviewTessellationShader
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "multiview = %A" x.multiview
                sprintf "multiviewGeometryShader = %A" x.multiviewGeometryShader
                sprintf "multiviewTessellationShader = %A" x.multiviewTessellationShader
            ] |> sprintf "VkPhysicalDeviceMultiviewFeatures { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceMultiviewProperties = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public maxMultiviewViewCount : uint32
        val mutable public maxMultiviewInstanceIndex : uint32

        new(sType : VkStructureType
          , pNext : nativeint
          , maxMultiviewViewCount : uint32
          , maxMultiviewInstanceIndex : uint32
          ) =
            {
                sType = sType
                pNext = pNext
                maxMultiviewViewCount = maxMultiviewViewCount
                maxMultiviewInstanceIndex = maxMultiviewInstanceIndex
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "maxMultiviewViewCount = %A" x.maxMultiviewViewCount
                sprintf "maxMultiviewInstanceIndex = %A" x.maxMultiviewInstanceIndex
            ] |> sprintf "VkPhysicalDeviceMultiviewProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDevicePointClippingProperties = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public pointClippingBehavior : VkPointClippingBehavior

        new(sType : VkStructureType
          , pNext : nativeint
          , pointClippingBehavior : VkPointClippingBehavior
          ) =
            {
                sType = sType
                pNext = pNext
                pointClippingBehavior = pointClippingBehavior
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "pointClippingBehavior = %A" x.pointClippingBehavior
            ] |> sprintf "VkPhysicalDevicePointClippingProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceSparseProperties = 
    struct
        val mutable public residencyStandard2DBlockShape : VkBool32
        val mutable public residencyStandard2DMultisampleBlockShape : VkBool32
        val mutable public residencyStandard3DBlockShape : VkBool32
        val mutable public residencyAlignedMipSize : VkBool32
        val mutable public residencyNonResidentStrict : VkBool32

        new(residencyStandard2DBlockShape : VkBool32
          , residencyStandard2DMultisampleBlockShape : VkBool32
          , residencyStandard3DBlockShape : VkBool32
          , residencyAlignedMipSize : VkBool32
          , residencyNonResidentStrict : VkBool32
          ) =
            {
                residencyStandard2DBlockShape = residencyStandard2DBlockShape
                residencyStandard2DMultisampleBlockShape = residencyStandard2DMultisampleBlockShape
                residencyStandard3DBlockShape = residencyStandard3DBlockShape
                residencyAlignedMipSize = residencyAlignedMipSize
                residencyNonResidentStrict = residencyNonResidentStrict
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "residencyStandard2DBlockShape = %A" x.residencyStandard2DBlockShape
                sprintf "residencyStandard2DMultisampleBlockShape = %A" x.residencyStandard2DMultisampleBlockShape
                sprintf "residencyStandard3DBlockShape = %A" x.residencyStandard3DBlockShape
                sprintf "residencyAlignedMipSize = %A" x.residencyAlignedMipSize
                sprintf "residencyNonResidentStrict = %A" x.residencyNonResidentStrict
            ] |> sprintf "VkPhysicalDeviceSparseProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceProperties = 
    struct
        val mutable public apiVersion : uint32
        val mutable public driverVersion : uint32
        val mutable public vendorID : uint32
        val mutable public deviceID : uint32
        val mutable public deviceType : VkPhysicalDeviceType
        val mutable public deviceName : String256
        val mutable public pipelineCacheUUID : Guid
        val mutable public limits : VkPhysicalDeviceLimits
        val mutable public sparseProperties : VkPhysicalDeviceSparseProperties

        new(apiVersion : uint32
          , driverVersion : uint32
          , vendorID : uint32
          , deviceID : uint32
          , deviceType : VkPhysicalDeviceType
          , deviceName : String256
          , pipelineCacheUUID : Guid
          , limits : VkPhysicalDeviceLimits
          , sparseProperties : VkPhysicalDeviceSparseProperties
          ) =
            {
                apiVersion = apiVersion
                driverVersion = driverVersion
                vendorID = vendorID
                deviceID = deviceID
                deviceType = deviceType
                deviceName = deviceName
                pipelineCacheUUID = pipelineCacheUUID
                limits = limits
                sparseProperties = sparseProperties
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "apiVersion = %A" x.apiVersion
                sprintf "driverVersion = %A" x.driverVersion
                sprintf "vendorID = %A" x.vendorID
                sprintf "deviceID = %A" x.deviceID
                sprintf "deviceType = %A" x.deviceType
                sprintf "deviceName = %A" x.deviceName
                sprintf "pipelineCacheUUID = %A" x.pipelineCacheUUID
                sprintf "limits = %A" x.limits
                sprintf "sparseProperties = %A" x.sparseProperties
            ] |> sprintf "VkPhysicalDeviceProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceProperties2 = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public properties : VkPhysicalDeviceProperties

        new(sType : VkStructureType
          , pNext : nativeint
          , properties : VkPhysicalDeviceProperties
          ) =
            {
                sType = sType
                pNext = pNext
                properties = properties
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "properties = %A" x.properties
            ] |> sprintf "VkPhysicalDeviceProperties2 { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceProtectedMemoryFeatures = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public protectedMemory : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , protectedMemory : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                protectedMemory = protectedMemory
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "protectedMemory = %A" x.protectedMemory
            ] |> sprintf "VkPhysicalDeviceProtectedMemoryFeatures { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceProtectedMemoryProperties = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public protectedNoFault : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , protectedNoFault : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                protectedNoFault = protectedNoFault
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "protectedNoFault = %A" x.protectedNoFault
            ] |> sprintf "VkPhysicalDeviceProtectedMemoryProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceSamplerYcbcrConversionFeatures = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public samplerYcbcrConversion : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , samplerYcbcrConversion : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                samplerYcbcrConversion = samplerYcbcrConversion
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "samplerYcbcrConversion = %A" x.samplerYcbcrConversion
            ] |> sprintf "VkPhysicalDeviceSamplerYcbcrConversionFeatures { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceShaderDrawParameterFeatures = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public shaderDrawParameters : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , shaderDrawParameters : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                shaderDrawParameters = shaderDrawParameters
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "shaderDrawParameters = %A" x.shaderDrawParameters
            ] |> sprintf "VkPhysicalDeviceShaderDrawParameterFeatures { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceSparseImageFormatInfo2 = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public format : VkFormat
        val mutable public _type : VkImageType
        val mutable public samples : VkSampleCountFlags
        val mutable public usage : VkImageUsageFlags
        val mutable public tiling : VkImageTiling

        new(sType : VkStructureType
          , pNext : nativeint
          , format : VkFormat
          , _type : VkImageType
          , samples : VkSampleCountFlags
          , usage : VkImageUsageFlags
          , tiling : VkImageTiling
          ) =
            {
                sType = sType
                pNext = pNext
                format = format
                _type = _type
                samples = samples
                usage = usage
                tiling = tiling
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "format = %A" x.format
                sprintf "_type = %A" x._type
                sprintf "samples = %A" x.samples
                sprintf "usage = %A" x.usage
                sprintf "tiling = %A" x.tiling
            ] |> sprintf "VkPhysicalDeviceSparseImageFormatInfo2 { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceSubgroupProperties = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public subgroupSize : uint32
        val mutable public supportedStages : VkShaderStageFlags
        val mutable public supportedOperations : VkSubgroupFeatureFlags
        val mutable public quadOperationsInAllStages : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , subgroupSize : uint32
          , supportedStages : VkShaderStageFlags
          , supportedOperations : VkSubgroupFeatureFlags
          , quadOperationsInAllStages : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                subgroupSize = subgroupSize
                supportedStages = supportedStages
                supportedOperations = supportedOperations
                quadOperationsInAllStages = quadOperationsInAllStages
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "subgroupSize = %A" x.subgroupSize
                sprintf "supportedStages = %A" x.supportedStages
                sprintf "supportedOperations = %A" x.supportedOperations
                sprintf "quadOperationsInAllStages = %A" x.quadOperationsInAllStages
            ] |> sprintf "VkPhysicalDeviceSubgroupProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceVariablePointerFeatures = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public variablePointersStorageBuffer : VkBool32
        val mutable public variablePointers : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , variablePointersStorageBuffer : VkBool32
          , variablePointers : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                variablePointersStorageBuffer = variablePointersStorageBuffer
                variablePointers = variablePointers
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "variablePointersStorageBuffer = %A" x.variablePointersStorageBuffer
                sprintf "variablePointers = %A" x.variablePointers
            ] |> sprintf "VkPhysicalDeviceVariablePointerFeatures { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineCacheCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineCacheCreateFlags
        val mutable public initialDataSize : uint64
        val mutable public pInitialData : nativeint

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkPipelineCacheCreateFlags
          , initialDataSize : uint64
          , pInitialData : nativeint
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                initialDataSize = initialDataSize
                pInitialData = pInitialData
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "initialDataSize = %A" x.initialDataSize
                sprintf "pInitialData = %A" x.pInitialData
            ] |> sprintf "VkPipelineCacheCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPushConstantRange = 
    struct
        val mutable public stageFlags : VkShaderStageFlags
        val mutable public offset : uint32
        val mutable public size : uint32

        new(stageFlags : VkShaderStageFlags
          , offset : uint32
          , size : uint32
          ) =
            {
                stageFlags = stageFlags
                offset = offset
                size = size
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "stageFlags = %A" x.stageFlags
                sprintf "offset = %A" x.offset
                sprintf "size = %A" x.size
            ] |> sprintf "VkPushConstantRange { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineLayoutCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineLayoutCreateFlags
        val mutable public setLayoutCount : uint32
        val mutable public pSetLayouts : nativeptr<VkDescriptorSetLayout>
        val mutable public pushConstantRangeCount : uint32
        val mutable public pPushConstantRanges : nativeptr<VkPushConstantRange>

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkPipelineLayoutCreateFlags
          , setLayoutCount : uint32
          , pSetLayouts : nativeptr<VkDescriptorSetLayout>
          , pushConstantRangeCount : uint32
          , pPushConstantRanges : nativeptr<VkPushConstantRange>
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                setLayoutCount = setLayoutCount
                pSetLayouts = pSetLayouts
                pushConstantRangeCount = pushConstantRangeCount
                pPushConstantRanges = pPushConstantRanges
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "setLayoutCount = %A" x.setLayoutCount
                sprintf "pSetLayouts = %A" x.pSetLayouts
                sprintf "pushConstantRangeCount = %A" x.pushConstantRangeCount
                sprintf "pPushConstantRanges = %A" x.pPushConstantRanges
            ] |> sprintf "VkPipelineLayoutCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineTessellationDomainOriginStateCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public domainOrigin : VkTessellationDomainOrigin

        new(sType : VkStructureType
          , pNext : nativeint
          , domainOrigin : VkTessellationDomainOrigin
          ) =
            {
                sType = sType
                pNext = pNext
                domainOrigin = domainOrigin
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "domainOrigin = %A" x.domainOrigin
            ] |> sprintf "VkPipelineTessellationDomainOriginStateCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPresentInfoKHR = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public waitSemaphoreCount : uint32
        val mutable public pWaitSemaphores : nativeptr<VkSemaphore>
        val mutable public swapchainCount : uint32
        val mutable public pSwapchains : nativeptr<VkSwapchainKHR>
        val mutable public pImageIndices : nativeptr<uint32>
        val mutable public pResults : nativeptr<VkResult>

        new(sType : VkStructureType
          , pNext : nativeint
          , waitSemaphoreCount : uint32
          , pWaitSemaphores : nativeptr<VkSemaphore>
          , swapchainCount : uint32
          , pSwapchains : nativeptr<VkSwapchainKHR>
          , pImageIndices : nativeptr<uint32>
          , pResults : nativeptr<VkResult>
          ) =
            {
                sType = sType
                pNext = pNext
                waitSemaphoreCount = waitSemaphoreCount
                pWaitSemaphores = pWaitSemaphores
                swapchainCount = swapchainCount
                pSwapchains = pSwapchains
                pImageIndices = pImageIndices
                pResults = pResults
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "waitSemaphoreCount = %A" x.waitSemaphoreCount
                sprintf "pWaitSemaphores = %A" x.pWaitSemaphores
                sprintf "swapchainCount = %A" x.swapchainCount
                sprintf "pSwapchains = %A" x.pSwapchains
                sprintf "pImageIndices = %A" x.pImageIndices
                sprintf "pResults = %A" x.pResults
            ] |> sprintf "VkPresentInfoKHR { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkProtectedSubmitInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public protectedSubmit : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , protectedSubmit : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                protectedSubmit = protectedSubmit
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "protectedSubmit = %A" x.protectedSubmit
            ] |> sprintf "VkProtectedSubmitInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkQueryPoolCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkQueryPoolCreateFlags
        val mutable public queryType : VkQueryType
        val mutable public queryCount : uint32
        val mutable public pipelineStatistics : VkQueryPipelineStatisticFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkQueryPoolCreateFlags
          , queryType : VkQueryType
          , queryCount : uint32
          , pipelineStatistics : VkQueryPipelineStatisticFlags
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                queryType = queryType
                queryCount = queryCount
                pipelineStatistics = pipelineStatistics
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "queryType = %A" x.queryType
                sprintf "queryCount = %A" x.queryCount
                sprintf "pipelineStatistics = %A" x.pipelineStatistics
            ] |> sprintf "VkQueryPoolCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkQueueFamilyProperties = 
    struct
        val mutable public queueFlags : VkQueueFlags
        val mutable public queueCount : uint32
        val mutable public timestampValidBits : uint32
        val mutable public minImageTransferGranularity : VkExtent3D

        new(queueFlags : VkQueueFlags
          , queueCount : uint32
          , timestampValidBits : uint32
          , minImageTransferGranularity : VkExtent3D
          ) =
            {
                queueFlags = queueFlags
                queueCount = queueCount
                timestampValidBits = timestampValidBits
                minImageTransferGranularity = minImageTransferGranularity
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "queueFlags = %A" x.queueFlags
                sprintf "queueCount = %A" x.queueCount
                sprintf "timestampValidBits = %A" x.timestampValidBits
                sprintf "minImageTransferGranularity = %A" x.minImageTransferGranularity
            ] |> sprintf "VkQueueFamilyProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkQueueFamilyProperties2 = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public queueFamilyProperties : VkQueueFamilyProperties

        new(sType : VkStructureType
          , pNext : nativeint
          , queueFamilyProperties : VkQueueFamilyProperties
          ) =
            {
                sType = sType
                pNext = pNext
                queueFamilyProperties = queueFamilyProperties
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "queueFamilyProperties = %A" x.queueFamilyProperties
            ] |> sprintf "VkQueueFamilyProperties2 { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkRenderPassBeginInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public renderPass : VkRenderPass
        val mutable public framebuffer : VkFramebuffer
        val mutable public renderArea : VkRect2D
        val mutable public clearValueCount : uint32
        val mutable public pClearValues : nativeptr<VkClearValue>

        new(sType : VkStructureType
          , pNext : nativeint
          , renderPass : VkRenderPass
          , framebuffer : VkFramebuffer
          , renderArea : VkRect2D
          , clearValueCount : uint32
          , pClearValues : nativeptr<VkClearValue>
          ) =
            {
                sType = sType
                pNext = pNext
                renderPass = renderPass
                framebuffer = framebuffer
                renderArea = renderArea
                clearValueCount = clearValueCount
                pClearValues = pClearValues
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "renderPass = %A" x.renderPass
                sprintf "framebuffer = %A" x.framebuffer
                sprintf "renderArea = %A" x.renderArea
                sprintf "clearValueCount = %A" x.clearValueCount
                sprintf "pClearValues = %A" x.pClearValues
            ] |> sprintf "VkRenderPassBeginInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSubpassDescription = 
    struct
        val mutable public flags : VkSubpassDescriptionFlags
        val mutable public pipelineBindPoint : VkPipelineBindPoint
        val mutable public inputAttachmentCount : uint32
        val mutable public pInputAttachments : nativeptr<VkAttachmentReference>
        val mutable public colorAttachmentCount : uint32
        val mutable public pColorAttachments : nativeptr<VkAttachmentReference>
        val mutable public pResolveAttachments : nativeptr<VkAttachmentReference>
        val mutable public pDepthStencilAttachment : nativeptr<VkAttachmentReference>
        val mutable public preserveAttachmentCount : uint32
        val mutable public pPreserveAttachments : nativeptr<uint32>

        new(flags : VkSubpassDescriptionFlags
          , pipelineBindPoint : VkPipelineBindPoint
          , inputAttachmentCount : uint32
          , pInputAttachments : nativeptr<VkAttachmentReference>
          , colorAttachmentCount : uint32
          , pColorAttachments : nativeptr<VkAttachmentReference>
          , pResolveAttachments : nativeptr<VkAttachmentReference>
          , pDepthStencilAttachment : nativeptr<VkAttachmentReference>
          , preserveAttachmentCount : uint32
          , pPreserveAttachments : nativeptr<uint32>
          ) =
            {
                flags = flags
                pipelineBindPoint = pipelineBindPoint
                inputAttachmentCount = inputAttachmentCount
                pInputAttachments = pInputAttachments
                colorAttachmentCount = colorAttachmentCount
                pColorAttachments = pColorAttachments
                pResolveAttachments = pResolveAttachments
                pDepthStencilAttachment = pDepthStencilAttachment
                preserveAttachmentCount = preserveAttachmentCount
                pPreserveAttachments = pPreserveAttachments
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "flags = %A" x.flags
                sprintf "pipelineBindPoint = %A" x.pipelineBindPoint
                sprintf "inputAttachmentCount = %A" x.inputAttachmentCount
                sprintf "pInputAttachments = %A" x.pInputAttachments
                sprintf "colorAttachmentCount = %A" x.colorAttachmentCount
                sprintf "pColorAttachments = %A" x.pColorAttachments
                sprintf "pResolveAttachments = %A" x.pResolveAttachments
                sprintf "pDepthStencilAttachment = %A" x.pDepthStencilAttachment
                sprintf "preserveAttachmentCount = %A" x.preserveAttachmentCount
                sprintf "pPreserveAttachments = %A" x.pPreserveAttachments
            ] |> sprintf "VkSubpassDescription { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSubpassDependency = 
    struct
        val mutable public srcSubpass : uint32
        val mutable public dstSubpass : uint32
        val mutable public srcStageMask : VkPipelineStageFlags
        val mutable public dstStageMask : VkPipelineStageFlags
        val mutable public srcAccessMask : VkAccessFlags
        val mutable public dstAccessMask : VkAccessFlags
        val mutable public dependencyFlags : VkDependencyFlags

        new(srcSubpass : uint32
          , dstSubpass : uint32
          , srcStageMask : VkPipelineStageFlags
          , dstStageMask : VkPipelineStageFlags
          , srcAccessMask : VkAccessFlags
          , dstAccessMask : VkAccessFlags
          , dependencyFlags : VkDependencyFlags
          ) =
            {
                srcSubpass = srcSubpass
                dstSubpass = dstSubpass
                srcStageMask = srcStageMask
                dstStageMask = dstStageMask
                srcAccessMask = srcAccessMask
                dstAccessMask = dstAccessMask
                dependencyFlags = dependencyFlags
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "srcSubpass = %A" x.srcSubpass
                sprintf "dstSubpass = %A" x.dstSubpass
                sprintf "srcStageMask = %A" x.srcStageMask
                sprintf "dstStageMask = %A" x.dstStageMask
                sprintf "srcAccessMask = %A" x.srcAccessMask
                sprintf "dstAccessMask = %A" x.dstAccessMask
                sprintf "dependencyFlags = %A" x.dependencyFlags
            ] |> sprintf "VkSubpassDependency { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkRenderPassCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkRenderPassCreateFlags
        val mutable public attachmentCount : uint32
        val mutable public pAttachments : nativeptr<VkAttachmentDescription>
        val mutable public subpassCount : uint32
        val mutable public pSubpasses : nativeptr<VkSubpassDescription>
        val mutable public dependencyCount : uint32
        val mutable public pDependencies : nativeptr<VkSubpassDependency>

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkRenderPassCreateFlags
          , attachmentCount : uint32
          , pAttachments : nativeptr<VkAttachmentDescription>
          , subpassCount : uint32
          , pSubpasses : nativeptr<VkSubpassDescription>
          , dependencyCount : uint32
          , pDependencies : nativeptr<VkSubpassDependency>
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                attachmentCount = attachmentCount
                pAttachments = pAttachments
                subpassCount = subpassCount
                pSubpasses = pSubpasses
                dependencyCount = dependencyCount
                pDependencies = pDependencies
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "attachmentCount = %A" x.attachmentCount
                sprintf "pAttachments = %A" x.pAttachments
                sprintf "subpassCount = %A" x.subpassCount
                sprintf "pSubpasses = %A" x.pSubpasses
                sprintf "dependencyCount = %A" x.dependencyCount
                sprintf "pDependencies = %A" x.pDependencies
            ] |> sprintf "VkRenderPassCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkRenderPassInputAttachmentAspectCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public aspectReferenceCount : uint32
        val mutable public pAspectReferences : nativeptr<VkInputAttachmentAspectReference>

        new(sType : VkStructureType
          , pNext : nativeint
          , aspectReferenceCount : uint32
          , pAspectReferences : nativeptr<VkInputAttachmentAspectReference>
          ) =
            {
                sType = sType
                pNext = pNext
                aspectReferenceCount = aspectReferenceCount
                pAspectReferences = pAspectReferences
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "aspectReferenceCount = %A" x.aspectReferenceCount
                sprintf "pAspectReferences = %A" x.pAspectReferences
            ] |> sprintf "VkRenderPassInputAttachmentAspectCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkRenderPassMultiviewCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public subpassCount : uint32
        val mutable public pViewMasks : nativeptr<uint32>
        val mutable public dependencyCount : uint32
        val mutable public pViewOffsets : nativeptr<int>
        val mutable public correlationMaskCount : uint32
        val mutable public pCorrelationMasks : nativeptr<uint32>

        new(sType : VkStructureType
          , pNext : nativeint
          , subpassCount : uint32
          , pViewMasks : nativeptr<uint32>
          , dependencyCount : uint32
          , pViewOffsets : nativeptr<int>
          , correlationMaskCount : uint32
          , pCorrelationMasks : nativeptr<uint32>
          ) =
            {
                sType = sType
                pNext = pNext
                subpassCount = subpassCount
                pViewMasks = pViewMasks
                dependencyCount = dependencyCount
                pViewOffsets = pViewOffsets
                correlationMaskCount = correlationMaskCount
                pCorrelationMasks = pCorrelationMasks
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "subpassCount = %A" x.subpassCount
                sprintf "pViewMasks = %A" x.pViewMasks
                sprintf "dependencyCount = %A" x.dependencyCount
                sprintf "pViewOffsets = %A" x.pViewOffsets
                sprintf "correlationMaskCount = %A" x.correlationMaskCount
                sprintf "pCorrelationMasks = %A" x.pCorrelationMasks
            ] |> sprintf "VkRenderPassMultiviewCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSamplerCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkSamplerCreateFlags
        val mutable public magFilter : VkFilter
        val mutable public minFilter : VkFilter
        val mutable public mipmapMode : VkSamplerMipmapMode
        val mutable public addressModeU : VkSamplerAddressMode
        val mutable public addressModeV : VkSamplerAddressMode
        val mutable public addressModeW : VkSamplerAddressMode
        val mutable public mipLodBias : float32
        val mutable public anisotropyEnable : VkBool32
        val mutable public maxAnisotropy : float32
        val mutable public compareEnable : VkBool32
        val mutable public compareOp : VkCompareOp
        val mutable public minLod : float32
        val mutable public maxLod : float32
        val mutable public borderColor : VkBorderColor
        val mutable public unnormalizedCoordinates : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkSamplerCreateFlags
          , magFilter : VkFilter
          , minFilter : VkFilter
          , mipmapMode : VkSamplerMipmapMode
          , addressModeU : VkSamplerAddressMode
          , addressModeV : VkSamplerAddressMode
          , addressModeW : VkSamplerAddressMode
          , mipLodBias : float32
          , anisotropyEnable : VkBool32
          , maxAnisotropy : float32
          , compareEnable : VkBool32
          , compareOp : VkCompareOp
          , minLod : float32
          , maxLod : float32
          , borderColor : VkBorderColor
          , unnormalizedCoordinates : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                magFilter = magFilter
                minFilter = minFilter
                mipmapMode = mipmapMode
                addressModeU = addressModeU
                addressModeV = addressModeV
                addressModeW = addressModeW
                mipLodBias = mipLodBias
                anisotropyEnable = anisotropyEnable
                maxAnisotropy = maxAnisotropy
                compareEnable = compareEnable
                compareOp = compareOp
                minLod = minLod
                maxLod = maxLod
                borderColor = borderColor
                unnormalizedCoordinates = unnormalizedCoordinates
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "magFilter = %A" x.magFilter
                sprintf "minFilter = %A" x.minFilter
                sprintf "mipmapMode = %A" x.mipmapMode
                sprintf "addressModeU = %A" x.addressModeU
                sprintf "addressModeV = %A" x.addressModeV
                sprintf "addressModeW = %A" x.addressModeW
                sprintf "mipLodBias = %A" x.mipLodBias
                sprintf "anisotropyEnable = %A" x.anisotropyEnable
                sprintf "maxAnisotropy = %A" x.maxAnisotropy
                sprintf "compareEnable = %A" x.compareEnable
                sprintf "compareOp = %A" x.compareOp
                sprintf "minLod = %A" x.minLod
                sprintf "maxLod = %A" x.maxLod
                sprintf "borderColor = %A" x.borderColor
                sprintf "unnormalizedCoordinates = %A" x.unnormalizedCoordinates
            ] |> sprintf "VkSamplerCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSamplerYcbcrConversionCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public format : VkFormat
        val mutable public ycbcrModel : VkSamplerYcbcrModelConversion
        val mutable public ycbcrRange : VkSamplerYcbcrRange
        val mutable public components : VkComponentMapping
        val mutable public xChromaOffset : VkChromaLocation
        val mutable public yChromaOffset : VkChromaLocation
        val mutable public chromaFilter : VkFilter
        val mutable public forceExplicitReconstruction : VkBool32

        new(sType : VkStructureType
          , pNext : nativeint
          , format : VkFormat
          , ycbcrModel : VkSamplerYcbcrModelConversion
          , ycbcrRange : VkSamplerYcbcrRange
          , components : VkComponentMapping
          , xChromaOffset : VkChromaLocation
          , yChromaOffset : VkChromaLocation
          , chromaFilter : VkFilter
          , forceExplicitReconstruction : VkBool32
          ) =
            {
                sType = sType
                pNext = pNext
                format = format
                ycbcrModel = ycbcrModel
                ycbcrRange = ycbcrRange
                components = components
                xChromaOffset = xChromaOffset
                yChromaOffset = yChromaOffset
                chromaFilter = chromaFilter
                forceExplicitReconstruction = forceExplicitReconstruction
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "format = %A" x.format
                sprintf "ycbcrModel = %A" x.ycbcrModel
                sprintf "ycbcrRange = %A" x.ycbcrRange
                sprintf "components = %A" x.components
                sprintf "xChromaOffset = %A" x.xChromaOffset
                sprintf "yChromaOffset = %A" x.yChromaOffset
                sprintf "chromaFilter = %A" x.chromaFilter
                sprintf "forceExplicitReconstruction = %A" x.forceExplicitReconstruction
            ] |> sprintf "VkSamplerYcbcrConversionCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSamplerYcbcrConversionImageFormatProperties = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public combinedImageSamplerDescriptorCount : uint32

        new(sType : VkStructureType
          , pNext : nativeint
          , combinedImageSamplerDescriptorCount : uint32
          ) =
            {
                sType = sType
                pNext = pNext
                combinedImageSamplerDescriptorCount = combinedImageSamplerDescriptorCount
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "combinedImageSamplerDescriptorCount = %A" x.combinedImageSamplerDescriptorCount
            ] |> sprintf "VkSamplerYcbcrConversionImageFormatProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSamplerYcbcrConversionInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public conversion : VkSamplerYcbcrConversion

        new(sType : VkStructureType
          , pNext : nativeint
          , conversion : VkSamplerYcbcrConversion
          ) =
            {
                sType = sType
                pNext = pNext
                conversion = conversion
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "conversion = %A" x.conversion
            ] |> sprintf "VkSamplerYcbcrConversionInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSemaphoreCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkSemaphoreCreateFlags

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkSemaphoreCreateFlags
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
            ] |> sprintf "VkSemaphoreCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkShaderModuleCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkShaderModuleCreateFlags
        val mutable public codeSize : uint64
        val mutable public pCode : nativeptr<uint32>

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkShaderModuleCreateFlags
          , codeSize : uint64
          , pCode : nativeptr<uint32>
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                codeSize = codeSize
                pCode = pCode
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "codeSize = %A" x.codeSize
                sprintf "pCode = %A" x.pCode
            ] |> sprintf "VkShaderModuleCreateInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSparseImageFormatProperties = 
    struct
        val mutable public aspectMask : VkImageAspectFlags
        val mutable public imageGranularity : VkExtent3D
        val mutable public flags : VkSparseImageFormatFlags

        new(aspectMask : VkImageAspectFlags
          , imageGranularity : VkExtent3D
          , flags : VkSparseImageFormatFlags
          ) =
            {
                aspectMask = aspectMask
                imageGranularity = imageGranularity
                flags = flags
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "aspectMask = %A" x.aspectMask
                sprintf "imageGranularity = %A" x.imageGranularity
                sprintf "flags = %A" x.flags
            ] |> sprintf "VkSparseImageFormatProperties { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSparseImageFormatProperties2 = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public properties : VkSparseImageFormatProperties

        new(sType : VkStructureType
          , pNext : nativeint
          , properties : VkSparseImageFormatProperties
          ) =
            {
                sType = sType
                pNext = pNext
                properties = properties
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "properties = %A" x.properties
            ] |> sprintf "VkSparseImageFormatProperties2 { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSparseImageMemoryRequirements = 
    struct
        val mutable public formatProperties : VkSparseImageFormatProperties
        val mutable public imageMipTailFirstLod : uint32
        val mutable public imageMipTailSize : VkDeviceSize
        val mutable public imageMipTailOffset : VkDeviceSize
        val mutable public imageMipTailStride : VkDeviceSize

        new(formatProperties : VkSparseImageFormatProperties
          , imageMipTailFirstLod : uint32
          , imageMipTailSize : VkDeviceSize
          , imageMipTailOffset : VkDeviceSize
          , imageMipTailStride : VkDeviceSize
          ) =
            {
                formatProperties = formatProperties
                imageMipTailFirstLod = imageMipTailFirstLod
                imageMipTailSize = imageMipTailSize
                imageMipTailOffset = imageMipTailOffset
                imageMipTailStride = imageMipTailStride
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "formatProperties = %A" x.formatProperties
                sprintf "imageMipTailFirstLod = %A" x.imageMipTailFirstLod
                sprintf "imageMipTailSize = %A" x.imageMipTailSize
                sprintf "imageMipTailOffset = %A" x.imageMipTailOffset
                sprintf "imageMipTailStride = %A" x.imageMipTailStride
            ] |> sprintf "VkSparseImageMemoryRequirements { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSparseImageMemoryRequirements2 = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public memoryRequirements : VkSparseImageMemoryRequirements

        new(sType : VkStructureType
          , pNext : nativeint
          , memoryRequirements : VkSparseImageMemoryRequirements
          ) =
            {
                sType = sType
                pNext = pNext
                memoryRequirements = memoryRequirements
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "memoryRequirements = %A" x.memoryRequirements
            ] |> sprintf "VkSparseImageMemoryRequirements2 { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSubmitInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public waitSemaphoreCount : uint32
        val mutable public pWaitSemaphores : nativeptr<VkSemaphore>
        val mutable public pWaitDstStageMask : nativeptr<VkPipelineStageFlags>
        val mutable public commandBufferCount : uint32
        val mutable public pCommandBuffers : nativeptr<VkCommandBuffer>
        val mutable public signalSemaphoreCount : uint32
        val mutable public pSignalSemaphores : nativeptr<VkSemaphore>

        new(sType : VkStructureType
          , pNext : nativeint
          , waitSemaphoreCount : uint32
          , pWaitSemaphores : nativeptr<VkSemaphore>
          , pWaitDstStageMask : nativeptr<VkPipelineStageFlags>
          , commandBufferCount : uint32
          , pCommandBuffers : nativeptr<VkCommandBuffer>
          , signalSemaphoreCount : uint32
          , pSignalSemaphores : nativeptr<VkSemaphore>
          ) =
            {
                sType = sType
                pNext = pNext
                waitSemaphoreCount = waitSemaphoreCount
                pWaitSemaphores = pWaitSemaphores
                pWaitDstStageMask = pWaitDstStageMask
                commandBufferCount = commandBufferCount
                pCommandBuffers = pCommandBuffers
                signalSemaphoreCount = signalSemaphoreCount
                pSignalSemaphores = pSignalSemaphores
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "waitSemaphoreCount = %A" x.waitSemaphoreCount
                sprintf "pWaitSemaphores = %A" x.pWaitSemaphores
                sprintf "pWaitDstStageMask = %A" x.pWaitDstStageMask
                sprintf "commandBufferCount = %A" x.commandBufferCount
                sprintf "pCommandBuffers = %A" x.pCommandBuffers
                sprintf "signalSemaphoreCount = %A" x.signalSemaphoreCount
                sprintf "pSignalSemaphores = %A" x.pSignalSemaphores
            ] |> sprintf "VkSubmitInfo { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSubresourceLayout = 
    struct
        val mutable public offset : VkDeviceSize
        val mutable public size : VkDeviceSize
        val mutable public rowPitch : VkDeviceSize
        val mutable public arrayPitch : VkDeviceSize
        val mutable public depthPitch : VkDeviceSize

        new(offset : VkDeviceSize
          , size : VkDeviceSize
          , rowPitch : VkDeviceSize
          , arrayPitch : VkDeviceSize
          , depthPitch : VkDeviceSize
          ) =
            {
                offset = offset
                size = size
                rowPitch = rowPitch
                arrayPitch = arrayPitch
                depthPitch = depthPitch
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "offset = %A" x.offset
                sprintf "size = %A" x.size
                sprintf "rowPitch = %A" x.rowPitch
                sprintf "arrayPitch = %A" x.arrayPitch
                sprintf "depthPitch = %A" x.depthPitch
            ] |> sprintf "VkSubresourceLayout { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSurfaceCapabilitiesKHR = 
    struct
        val mutable public minImageCount : uint32
        val mutable public maxImageCount : uint32
        val mutable public currentExtent : VkExtent2D
        val mutable public minImageExtent : VkExtent2D
        val mutable public maxImageExtent : VkExtent2D
        val mutable public maxImageArrayLayers : uint32
        val mutable public supportedTransforms : VkSurfaceTransformFlagsKHR
        val mutable public currentTransform : VkSurfaceTransformFlagsKHR
        val mutable public supportedCompositeAlpha : VkCompositeAlphaFlagsKHR
        val mutable public supportedUsageFlags : VkImageUsageFlags

        new(minImageCount : uint32
          , maxImageCount : uint32
          , currentExtent : VkExtent2D
          , minImageExtent : VkExtent2D
          , maxImageExtent : VkExtent2D
          , maxImageArrayLayers : uint32
          , supportedTransforms : VkSurfaceTransformFlagsKHR
          , currentTransform : VkSurfaceTransformFlagsKHR
          , supportedCompositeAlpha : VkCompositeAlphaFlagsKHR
          , supportedUsageFlags : VkImageUsageFlags
          ) =
            {
                minImageCount = minImageCount
                maxImageCount = maxImageCount
                currentExtent = currentExtent
                minImageExtent = minImageExtent
                maxImageExtent = maxImageExtent
                maxImageArrayLayers = maxImageArrayLayers
                supportedTransforms = supportedTransforms
                currentTransform = currentTransform
                supportedCompositeAlpha = supportedCompositeAlpha
                supportedUsageFlags = supportedUsageFlags
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "minImageCount = %A" x.minImageCount
                sprintf "maxImageCount = %A" x.maxImageCount
                sprintf "currentExtent = %A" x.currentExtent
                sprintf "minImageExtent = %A" x.minImageExtent
                sprintf "maxImageExtent = %A" x.maxImageExtent
                sprintf "maxImageArrayLayers = %A" x.maxImageArrayLayers
                sprintf "supportedTransforms = %A" x.supportedTransforms
                sprintf "currentTransform = %A" x.currentTransform
                sprintf "supportedCompositeAlpha = %A" x.supportedCompositeAlpha
                sprintf "supportedUsageFlags = %A" x.supportedUsageFlags
            ] |> sprintf "VkSurfaceCapabilitiesKHR { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSurfaceFormatKHR = 
    struct
        val mutable public format : VkFormat
        val mutable public colorSpace : VkColorSpaceKHR

        new(format : VkFormat
          , colorSpace : VkColorSpaceKHR
          ) =
            {
                format = format
                colorSpace = colorSpace
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "format = %A" x.format
                sprintf "colorSpace = %A" x.colorSpace
            ] |> sprintf "VkSurfaceFormatKHR { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSwapchainCreateInfoKHR = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkSwapchainCreateFlagsKHR
        val mutable public surface : VkSurfaceKHR
        val mutable public minImageCount : uint32
        val mutable public imageFormat : VkFormat
        val mutable public imageColorSpace : VkColorSpaceKHR
        val mutable public imageExtent : VkExtent2D
        val mutable public imageArrayLayers : uint32
        val mutable public imageUsage : VkImageUsageFlags
        val mutable public imageSharingMode : VkSharingMode
        val mutable public queueFamilyIndexCount : uint32
        val mutable public pQueueFamilyIndices : nativeptr<uint32>
        val mutable public preTransform : VkSurfaceTransformFlagsKHR
        val mutable public compositeAlpha : VkCompositeAlphaFlagsKHR
        val mutable public presentMode : VkPresentModeKHR
        val mutable public clipped : VkBool32
        val mutable public oldSwapchain : VkSwapchainKHR

        new(sType : VkStructureType
          , pNext : nativeint
          , flags : VkSwapchainCreateFlagsKHR
          , surface : VkSurfaceKHR
          , minImageCount : uint32
          , imageFormat : VkFormat
          , imageColorSpace : VkColorSpaceKHR
          , imageExtent : VkExtent2D
          , imageArrayLayers : uint32
          , imageUsage : VkImageUsageFlags
          , imageSharingMode : VkSharingMode
          , queueFamilyIndexCount : uint32
          , pQueueFamilyIndices : nativeptr<uint32>
          , preTransform : VkSurfaceTransformFlagsKHR
          , compositeAlpha : VkCompositeAlphaFlagsKHR
          , presentMode : VkPresentModeKHR
          , clipped : VkBool32
          , oldSwapchain : VkSwapchainKHR
          ) =
            {
                sType = sType
                pNext = pNext
                flags = flags
                surface = surface
                minImageCount = minImageCount
                imageFormat = imageFormat
                imageColorSpace = imageColorSpace
                imageExtent = imageExtent
                imageArrayLayers = imageArrayLayers
                imageUsage = imageUsage
                imageSharingMode = imageSharingMode
                queueFamilyIndexCount = queueFamilyIndexCount
                pQueueFamilyIndices = pQueueFamilyIndices
                preTransform = preTransform
                compositeAlpha = compositeAlpha
                presentMode = presentMode
                clipped = clipped
                oldSwapchain = oldSwapchain
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "flags = %A" x.flags
                sprintf "surface = %A" x.surface
                sprintf "minImageCount = %A" x.minImageCount
                sprintf "imageFormat = %A" x.imageFormat
                sprintf "imageColorSpace = %A" x.imageColorSpace
                sprintf "imageExtent = %A" x.imageExtent
                sprintf "imageArrayLayers = %A" x.imageArrayLayers
                sprintf "imageUsage = %A" x.imageUsage
                sprintf "imageSharingMode = %A" x.imageSharingMode
                sprintf "queueFamilyIndexCount = %A" x.queueFamilyIndexCount
                sprintf "pQueueFamilyIndices = %A" x.pQueueFamilyIndices
                sprintf "preTransform = %A" x.preTransform
                sprintf "compositeAlpha = %A" x.compositeAlpha
                sprintf "presentMode = %A" x.presentMode
                sprintf "clipped = %A" x.clipped
                sprintf "oldSwapchain = %A" x.oldSwapchain
            ] |> sprintf "VkSwapchainCreateInfoKHR { %s }"
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkWriteDescriptorSet = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public dstSet : VkDescriptorSet
        val mutable public dstBinding : uint32
        val mutable public dstArrayElement : uint32
        val mutable public descriptorCount : uint32
        val mutable public descriptorType : VkDescriptorType
        val mutable public pImageInfo : nativeptr<VkDescriptorImageInfo>
        val mutable public pBufferInfo : nativeptr<VkDescriptorBufferInfo>
        val mutable public pTexelBufferView : nativeptr<VkBufferView>

        new(sType : VkStructureType
          , pNext : nativeint
          , dstSet : VkDescriptorSet
          , dstBinding : uint32
          , dstArrayElement : uint32
          , descriptorCount : uint32
          , descriptorType : VkDescriptorType
          , pImageInfo : nativeptr<VkDescriptorImageInfo>
          , pBufferInfo : nativeptr<VkDescriptorBufferInfo>
          , pTexelBufferView : nativeptr<VkBufferView>
          ) =
            {
                sType = sType
                pNext = pNext
                dstSet = dstSet
                dstBinding = dstBinding
                dstArrayElement = dstArrayElement
                descriptorCount = descriptorCount
                descriptorType = descriptorType
                pImageInfo = pImageInfo
                pBufferInfo = pBufferInfo
                pTexelBufferView = pTexelBufferView
            }
        override x.ToString() =
            String.concat "; " [
                sprintf "sType = %A" x.sType
                sprintf "pNext = %A" x.pNext
                sprintf "dstSet = %A" x.dstSet
                sprintf "dstBinding = %A" x.dstBinding
                sprintf "dstArrayElement = %A" x.dstArrayElement
                sprintf "descriptorCount = %A" x.descriptorCount
                sprintf "descriptorType = %A" x.descriptorType
                sprintf "pImageInfo = %A" x.pImageInfo
                sprintf "pBufferInfo = %A" x.pBufferInfo
                sprintf "pTexelBufferView = %A" x.pTexelBufferView
            ] |> sprintf "VkWriteDescriptorSet { %s }"
    end

module VkRaw = 
    [<CompilerMessage("activeInstance is for internal use only", 1337, IsError=false, IsHidden=true)>]
    let mutable internal activeInstance : VkInstance = 0n
    [<Literal>]
    let lib = "vulkan-1"

    [<DllImport(lib, EntryPoint="vkCreateInstance");SuppressUnmanagedCodeSecurity>]
    extern VkResult private _vkCreateInstance(VkInstanceCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkInstance* pInstance)
    let vkCreateInstance(pCreateInfo : nativeptr<VkInstanceCreateInfo>, pAllocator : nativeptr<VkAllocationCallbacks>, pInstance : nativeptr<VkInstance>) = 
        let res = _vkCreateInstance(pCreateInfo, pAllocator, pInstance)
        if res = VkResult.VkSuccess then
            activeInstance <- NativePtr.read pInstance
        res
    
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyInstance(VkInstance instance, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkEnumeratePhysicalDevices(VkInstance instance, uint32* pPhysicalDeviceCount, VkPhysicalDevice* pPhysicalDevices)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern PFN_vkVoidFunction vkGetDeviceProcAddr(VkDevice device, string pName)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern PFN_vkVoidFunction vkGetInstanceProcAddr(VkInstance instance, string pName)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetPhysicalDeviceProperties(VkPhysicalDevice physicalDevice, VkPhysicalDeviceProperties* pProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetPhysicalDeviceQueueFamilyProperties(VkPhysicalDevice physicalDevice, uint32* pQueueFamilyPropertyCount, VkQueueFamilyProperties* pQueueFamilyProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetPhysicalDeviceMemoryProperties(VkPhysicalDevice physicalDevice, VkPhysicalDeviceMemoryProperties* pMemoryProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetPhysicalDeviceFeatures(VkPhysicalDevice physicalDevice, VkPhysicalDeviceFeatures* pFeatures)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetPhysicalDeviceFormatProperties(VkPhysicalDevice physicalDevice, VkFormat format, VkFormatProperties* pFormatProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkGetPhysicalDeviceImageFormatProperties(VkPhysicalDevice physicalDevice, VkFormat format, VkImageType _type, VkImageTiling tiling, VkImageUsageFlags usage, VkImageCreateFlags flags, VkImageFormatProperties* pImageFormatProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateDevice(VkPhysicalDevice physicalDevice, VkDeviceCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkDevice* pDevice)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyDevice(VkDevice device, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkEnumerateInstanceVersion(uint32* pApiVersion)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkEnumerateInstanceLayerProperties(uint32* pPropertyCount, VkLayerProperties* pProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkEnumerateInstanceExtensionProperties(string pLayerName, uint32* pPropertyCount, VkExtensionProperties* pProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkEnumerateDeviceLayerProperties(VkPhysicalDevice physicalDevice, uint32* pPropertyCount, VkLayerProperties* pProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkEnumerateDeviceExtensionProperties(VkPhysicalDevice physicalDevice, string pLayerName, uint32* pPropertyCount, VkExtensionProperties* pProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetDeviceQueue(VkDevice device, uint32 queueFamilyIndex, uint32 queueIndex, VkQueue* pQueue)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkQueueSubmit(VkQueue queue, uint32 submitCount, VkSubmitInfo* pSubmits, VkFence fence)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkQueueWaitIdle(VkQueue queue)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkDeviceWaitIdle(VkDevice device)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkAllocateMemory(VkDevice device, VkMemoryAllocateInfo* pAllocateInfo, VkAllocationCallbacks* pAllocator, VkDeviceMemory* pMemory)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkFreeMemory(VkDevice device, VkDeviceMemory memory, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkMapMemory(VkDevice device, VkDeviceMemory memory, VkDeviceSize offset, VkDeviceSize size, VkMemoryMapFlags flags, nativeint* ppData)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkUnmapMemory(VkDevice device, VkDeviceMemory memory)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkFlushMappedMemoryRanges(VkDevice device, uint32 memoryRangeCount, VkMappedMemoryRange* pMemoryRanges)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkInvalidateMappedMemoryRanges(VkDevice device, uint32 memoryRangeCount, VkMappedMemoryRange* pMemoryRanges)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetDeviceMemoryCommitment(VkDevice device, VkDeviceMemory memory, VkDeviceSize* pCommittedMemoryInBytes)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetBufferMemoryRequirements(VkDevice device, VkBuffer buffer, VkMemoryRequirements* pMemoryRequirements)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkBindBufferMemory(VkDevice device, VkBuffer buffer, VkDeviceMemory memory, VkDeviceSize memoryOffset)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetImageMemoryRequirements(VkDevice device, VkImage image, VkMemoryRequirements* pMemoryRequirements)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkBindImageMemory(VkDevice device, VkImage image, VkDeviceMemory memory, VkDeviceSize memoryOffset)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetImageSparseMemoryRequirements(VkDevice device, VkImage image, uint32* pSparseMemoryRequirementCount, VkSparseImageMemoryRequirements* pSparseMemoryRequirements)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetPhysicalDeviceSparseImageFormatProperties(VkPhysicalDevice physicalDevice, VkFormat format, VkImageType _type, VkSampleCountFlags samples, VkImageUsageFlags usage, VkImageTiling tiling, uint32* pPropertyCount, VkSparseImageFormatProperties* pProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkQueueBindSparse(VkQueue queue, uint32 bindInfoCount, VkBindSparseInfo* pBindInfo, VkFence fence)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateFence(VkDevice device, VkFenceCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkFence* pFence)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyFence(VkDevice device, VkFence fence, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkResetFences(VkDevice device, uint32 fenceCount, VkFence* pFences)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkGetFenceStatus(VkDevice device, VkFence fence)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkWaitForFences(VkDevice device, uint32 fenceCount, VkFence* pFences, VkBool32 waitAll, uint64 timeout)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateSemaphore(VkDevice device, VkSemaphoreCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkSemaphore* pSemaphore)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroySemaphore(VkDevice device, VkSemaphore semaphore, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateEvent(VkDevice device, VkEventCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkEvent* pEvent)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyEvent(VkDevice device, VkEvent event, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkGetEventStatus(VkDevice device, VkEvent event)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkSetEvent(VkDevice device, VkEvent event)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkResetEvent(VkDevice device, VkEvent event)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateQueryPool(VkDevice device, VkQueryPoolCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkQueryPool* pQueryPool)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyQueryPool(VkDevice device, VkQueryPool queryPool, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkGetQueryPoolResults(VkDevice device, VkQueryPool queryPool, uint32 firstQuery, uint32 queryCount, uint64 dataSize, nativeint pData, VkDeviceSize stride, VkQueryResultFlags flags)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateBuffer(VkDevice device, VkBufferCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkBuffer* pBuffer)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyBuffer(VkDevice device, VkBuffer buffer, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateBufferView(VkDevice device, VkBufferViewCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkBufferView* pView)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyBufferView(VkDevice device, VkBufferView bufferView, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateImage(VkDevice device, VkImageCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkImage* pImage)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyImage(VkDevice device, VkImage image, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetImageSubresourceLayout(VkDevice device, VkImage image, VkImageSubresource* pSubresource, VkSubresourceLayout* pLayout)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateImageView(VkDevice device, VkImageViewCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkImageView* pView)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyImageView(VkDevice device, VkImageView imageView, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateShaderModule(VkDevice device, VkShaderModuleCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkShaderModule* pShaderModule)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyShaderModule(VkDevice device, VkShaderModule shaderModule, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreatePipelineCache(VkDevice device, VkPipelineCacheCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkPipelineCache* pPipelineCache)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyPipelineCache(VkDevice device, VkPipelineCache pipelineCache, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkGetPipelineCacheData(VkDevice device, VkPipelineCache pipelineCache, uint64* pDataSize, nativeint pData)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkMergePipelineCaches(VkDevice device, VkPipelineCache dstCache, uint32 srcCacheCount, VkPipelineCache* pSrcCaches)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateGraphicsPipelines(VkDevice device, VkPipelineCache pipelineCache, uint32 createInfoCount, VkGraphicsPipelineCreateInfo* pCreateInfos, VkAllocationCallbacks* pAllocator, VkPipeline* pPipelines)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateComputePipelines(VkDevice device, VkPipelineCache pipelineCache, uint32 createInfoCount, VkComputePipelineCreateInfo* pCreateInfos, VkAllocationCallbacks* pAllocator, VkPipeline* pPipelines)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyPipeline(VkDevice device, VkPipeline pipeline, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreatePipelineLayout(VkDevice device, VkPipelineLayoutCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkPipelineLayout* pPipelineLayout)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyPipelineLayout(VkDevice device, VkPipelineLayout pipelineLayout, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateSampler(VkDevice device, VkSamplerCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkSampler* pSampler)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroySampler(VkDevice device, VkSampler sampler, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateDescriptorSetLayout(VkDevice device, VkDescriptorSetLayoutCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkDescriptorSetLayout* pSetLayout)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyDescriptorSetLayout(VkDevice device, VkDescriptorSetLayout descriptorSetLayout, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateDescriptorPool(VkDevice device, VkDescriptorPoolCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkDescriptorPool* pDescriptorPool)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyDescriptorPool(VkDevice device, VkDescriptorPool descriptorPool, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkResetDescriptorPool(VkDevice device, VkDescriptorPool descriptorPool, VkDescriptorPoolResetFlags flags)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkAllocateDescriptorSets(VkDevice device, VkDescriptorSetAllocateInfo* pAllocateInfo, VkDescriptorSet* pDescriptorSets)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkFreeDescriptorSets(VkDevice device, VkDescriptorPool descriptorPool, uint32 descriptorSetCount, VkDescriptorSet* pDescriptorSets)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkUpdateDescriptorSets(VkDevice device, uint32 descriptorWriteCount, VkWriteDescriptorSet* pDescriptorWrites, uint32 descriptorCopyCount, VkCopyDescriptorSet* pDescriptorCopies)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateFramebuffer(VkDevice device, VkFramebufferCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkFramebuffer* pFramebuffer)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyFramebuffer(VkDevice device, VkFramebuffer framebuffer, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateRenderPass(VkDevice device, VkRenderPassCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkRenderPass* pRenderPass)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyRenderPass(VkDevice device, VkRenderPass renderPass, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetRenderAreaGranularity(VkDevice device, VkRenderPass renderPass, VkExtent2D* pGranularity)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateCommandPool(VkDevice device, VkCommandPoolCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkCommandPool* pCommandPool)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyCommandPool(VkDevice device, VkCommandPool commandPool, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkResetCommandPool(VkDevice device, VkCommandPool commandPool, VkCommandPoolResetFlags flags)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkAllocateCommandBuffers(VkDevice device, VkCommandBufferAllocateInfo* pAllocateInfo, VkCommandBuffer* pCommandBuffers)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkFreeCommandBuffers(VkDevice device, VkCommandPool commandPool, uint32 commandBufferCount, VkCommandBuffer* pCommandBuffers)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkBeginCommandBuffer(VkCommandBuffer commandBuffer, VkCommandBufferBeginInfo* pBeginInfo)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkEndCommandBuffer(VkCommandBuffer commandBuffer)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkResetCommandBuffer(VkCommandBuffer commandBuffer, VkCommandBufferResetFlags flags)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdBindPipeline(VkCommandBuffer commandBuffer, VkPipelineBindPoint pipelineBindPoint, VkPipeline pipeline)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdSetViewport(VkCommandBuffer commandBuffer, uint32 firstViewport, uint32 viewportCount, VkViewport* pViewports)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdSetScissor(VkCommandBuffer commandBuffer, uint32 firstScissor, uint32 scissorCount, VkRect2D* pScissors)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdSetLineWidth(VkCommandBuffer commandBuffer, float32 lineWidth)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdSetDepthBias(VkCommandBuffer commandBuffer, float32 depthBiasConstantFactor, float32 depthBiasClamp, float32 depthBiasSlopeFactor)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdSetBlendConstants(VkCommandBuffer commandBuffer, V4f blendConstants)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdSetDepthBounds(VkCommandBuffer commandBuffer, float32 minDepthBounds, float32 maxDepthBounds)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdSetStencilCompareMask(VkCommandBuffer commandBuffer, VkStencilFaceFlags faceMask, uint32 compareMask)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdSetStencilWriteMask(VkCommandBuffer commandBuffer, VkStencilFaceFlags faceMask, uint32 writeMask)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdSetStencilReference(VkCommandBuffer commandBuffer, VkStencilFaceFlags faceMask, uint32 reference)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdBindDescriptorSets(VkCommandBuffer commandBuffer, VkPipelineBindPoint pipelineBindPoint, VkPipelineLayout layout, uint32 firstSet, uint32 descriptorSetCount, VkDescriptorSet* pDescriptorSets, uint32 dynamicOffsetCount, uint32* pDynamicOffsets)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdBindIndexBuffer(VkCommandBuffer commandBuffer, VkBuffer buffer, VkDeviceSize offset, VkIndexType indexType)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdBindVertexBuffers(VkCommandBuffer commandBuffer, uint32 firstBinding, uint32 bindingCount, VkBuffer* pBuffers, VkDeviceSize* pOffsets)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdDraw(VkCommandBuffer commandBuffer, uint32 vertexCount, uint32 instanceCount, uint32 firstVertex, uint32 firstInstance)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdDrawIndexed(VkCommandBuffer commandBuffer, uint32 indexCount, uint32 instanceCount, uint32 firstIndex, int vertexOffset, uint32 firstInstance)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdDrawIndirect(VkCommandBuffer commandBuffer, VkBuffer buffer, VkDeviceSize offset, uint32 drawCount, uint32 stride)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdDrawIndexedIndirect(VkCommandBuffer commandBuffer, VkBuffer buffer, VkDeviceSize offset, uint32 drawCount, uint32 stride)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdDispatch(VkCommandBuffer commandBuffer, uint32 groupCountX, uint32 groupCountY, uint32 groupCountZ)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdDispatchIndirect(VkCommandBuffer commandBuffer, VkBuffer buffer, VkDeviceSize offset)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdCopyBuffer(VkCommandBuffer commandBuffer, VkBuffer srcBuffer, VkBuffer dstBuffer, uint32 regionCount, VkBufferCopy* pRegions)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdCopyImage(VkCommandBuffer commandBuffer, VkImage srcImage, VkImageLayout srcImageLayout, VkImage dstImage, VkImageLayout dstImageLayout, uint32 regionCount, VkImageCopy* pRegions)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdBlitImage(VkCommandBuffer commandBuffer, VkImage srcImage, VkImageLayout srcImageLayout, VkImage dstImage, VkImageLayout dstImageLayout, uint32 regionCount, VkImageBlit* pRegions, VkFilter filter)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdCopyBufferToImage(VkCommandBuffer commandBuffer, VkBuffer srcBuffer, VkImage dstImage, VkImageLayout dstImageLayout, uint32 regionCount, VkBufferImageCopy* pRegions)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdCopyImageToBuffer(VkCommandBuffer commandBuffer, VkImage srcImage, VkImageLayout srcImageLayout, VkBuffer dstBuffer, uint32 regionCount, VkBufferImageCopy* pRegions)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdUpdateBuffer(VkCommandBuffer commandBuffer, VkBuffer dstBuffer, VkDeviceSize dstOffset, VkDeviceSize dataSize, nativeint pData)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdFillBuffer(VkCommandBuffer commandBuffer, VkBuffer dstBuffer, VkDeviceSize dstOffset, VkDeviceSize size, uint32 data)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdClearColorImage(VkCommandBuffer commandBuffer, VkImage image, VkImageLayout imageLayout, VkClearColorValue* pColor, uint32 rangeCount, VkImageSubresourceRange* pRanges)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdClearDepthStencilImage(VkCommandBuffer commandBuffer, VkImage image, VkImageLayout imageLayout, VkClearDepthStencilValue* pDepthStencil, uint32 rangeCount, VkImageSubresourceRange* pRanges)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdClearAttachments(VkCommandBuffer commandBuffer, uint32 attachmentCount, VkClearAttachment* pAttachments, uint32 rectCount, VkClearRect* pRects)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdResolveImage(VkCommandBuffer commandBuffer, VkImage srcImage, VkImageLayout srcImageLayout, VkImage dstImage, VkImageLayout dstImageLayout, uint32 regionCount, VkImageResolve* pRegions)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdSetEvent(VkCommandBuffer commandBuffer, VkEvent event, VkPipelineStageFlags stageMask)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdResetEvent(VkCommandBuffer commandBuffer, VkEvent event, VkPipelineStageFlags stageMask)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdWaitEvents(VkCommandBuffer commandBuffer, uint32 eventCount, VkEvent* pEvents, VkPipelineStageFlags srcStageMask, VkPipelineStageFlags dstStageMask, uint32 memoryBarrierCount, VkMemoryBarrier* pMemoryBarriers, uint32 bufferMemoryBarrierCount, VkBufferMemoryBarrier* pBufferMemoryBarriers, uint32 imageMemoryBarrierCount, VkImageMemoryBarrier* pImageMemoryBarriers)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdPipelineBarrier(VkCommandBuffer commandBuffer, VkPipelineStageFlags srcStageMask, VkPipelineStageFlags dstStageMask, VkDependencyFlags dependencyFlags, uint32 memoryBarrierCount, VkMemoryBarrier* pMemoryBarriers, uint32 bufferMemoryBarrierCount, VkBufferMemoryBarrier* pBufferMemoryBarriers, uint32 imageMemoryBarrierCount, VkImageMemoryBarrier* pImageMemoryBarriers)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdBeginQuery(VkCommandBuffer commandBuffer, VkQueryPool queryPool, uint32 query, VkQueryControlFlags flags)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdEndQuery(VkCommandBuffer commandBuffer, VkQueryPool queryPool, uint32 query)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdResetQueryPool(VkCommandBuffer commandBuffer, VkQueryPool queryPool, uint32 firstQuery, uint32 queryCount)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdWriteTimestamp(VkCommandBuffer commandBuffer, VkPipelineStageFlags pipelineStage, VkQueryPool queryPool, uint32 query)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdCopyQueryPoolResults(VkCommandBuffer commandBuffer, VkQueryPool queryPool, uint32 firstQuery, uint32 queryCount, VkBuffer dstBuffer, VkDeviceSize dstOffset, VkDeviceSize stride, VkQueryResultFlags flags)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdPushConstants(VkCommandBuffer commandBuffer, VkPipelineLayout layout, VkShaderStageFlags stageFlags, uint32 offset, uint32 size, nativeint pValues)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdBeginRenderPass(VkCommandBuffer commandBuffer, VkRenderPassBeginInfo* pRenderPassBegin, VkSubpassContents contents)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdNextSubpass(VkCommandBuffer commandBuffer, VkSubpassContents contents)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdEndRenderPass(VkCommandBuffer commandBuffer)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdExecuteCommands(VkCommandBuffer commandBuffer, uint32 commandBufferCount, VkCommandBuffer* pCommandBuffers)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetPhysicalDeviceFeatures2(VkPhysicalDevice physicalDevice, VkPhysicalDeviceFeatures2* pFeatures)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetPhysicalDeviceProperties2(VkPhysicalDevice physicalDevice, VkPhysicalDeviceProperties2* pProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetPhysicalDeviceFormatProperties2(VkPhysicalDevice physicalDevice, VkFormat format, VkFormatProperties2* pFormatProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkGetPhysicalDeviceImageFormatProperties2(VkPhysicalDevice physicalDevice, VkPhysicalDeviceImageFormatInfo2* pImageFormatInfo, VkImageFormatProperties2* pImageFormatProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetPhysicalDeviceQueueFamilyProperties2(VkPhysicalDevice physicalDevice, uint32* pQueueFamilyPropertyCount, VkQueueFamilyProperties2* pQueueFamilyProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetPhysicalDeviceMemoryProperties2(VkPhysicalDevice physicalDevice, VkPhysicalDeviceMemoryProperties2* pMemoryProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetPhysicalDeviceSparseImageFormatProperties2(VkPhysicalDevice physicalDevice, VkPhysicalDeviceSparseImageFormatInfo2* pFormatInfo, uint32* pPropertyCount, VkSparseImageFormatProperties2* pProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkTrimCommandPool(VkDevice device, VkCommandPool commandPool, VkCommandPoolTrimFlags flags)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetPhysicalDeviceExternalBufferProperties(VkPhysicalDevice physicalDevice, VkPhysicalDeviceExternalBufferInfo* pExternalBufferInfo, VkExternalBufferProperties* pExternalBufferProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetPhysicalDeviceExternalSemaphoreProperties(VkPhysicalDevice physicalDevice, VkPhysicalDeviceExternalSemaphoreInfo* pExternalSemaphoreInfo, VkExternalSemaphoreProperties* pExternalSemaphoreProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetPhysicalDeviceExternalFenceProperties(VkPhysicalDevice physicalDevice, VkPhysicalDeviceExternalFenceInfo* pExternalFenceInfo, VkExternalFenceProperties* pExternalFenceProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkEnumeratePhysicalDeviceGroups(VkInstance instance, uint32* pPhysicalDeviceGroupCount, VkPhysicalDeviceGroupProperties* pPhysicalDeviceGroupProperties)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetDeviceGroupPeerMemoryFeatures(VkDevice device, uint32 heapIndex, uint32 localDeviceIndex, uint32 remoteDeviceIndex, VkPeerMemoryFeatureFlags* pPeerMemoryFeatures)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkBindBufferMemory2(VkDevice device, uint32 bindInfoCount, VkBindBufferMemoryInfo* pBindInfos)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkBindImageMemory2(VkDevice device, uint32 bindInfoCount, VkBindImageMemoryInfo* pBindInfos)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdSetDeviceMask(VkCommandBuffer commandBuffer, uint32 deviceMask)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkGetDeviceGroupPresentCapabilitiesKHR(VkDevice device, VkDeviceGroupPresentCapabilitiesKHR* pDeviceGroupPresentCapabilities)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkGetDeviceGroupSurfacePresentModesKHR(VkDevice device, VkSurfaceKHR surface, VkDeviceGroupPresentModeFlagsKHR* pModes)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdDispatchBase(VkCommandBuffer commandBuffer, uint32 baseGroupX, uint32 baseGroupY, uint32 baseGroupZ, uint32 groupCountX, uint32 groupCountY, uint32 groupCountZ)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkGetPhysicalDevicePresentRectanglesKHR(VkPhysicalDevice physicalDevice, VkSurfaceKHR surface, uint32* pRectCount, VkRect2D* pRects)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateDescriptorUpdateTemplate(VkDevice device, VkDescriptorUpdateTemplateCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkDescriptorUpdateTemplate* pDescriptorUpdateTemplate)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroyDescriptorUpdateTemplate(VkDevice device, VkDescriptorUpdateTemplate descriptorUpdateTemplate, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkUpdateDescriptorSetWithTemplate(VkDevice device, VkDescriptorSet descriptorSet, VkDescriptorUpdateTemplate descriptorUpdateTemplate, nativeint pData)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkCmdPushDescriptorSetWithTemplateKHR(VkCommandBuffer commandBuffer, VkDescriptorUpdateTemplate descriptorUpdateTemplate, VkPipelineLayout layout, uint32 set, nativeint pData)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetBufferMemoryRequirements2(VkDevice device, VkBufferMemoryRequirementsInfo2* pInfo, VkMemoryRequirements2* pMemoryRequirements)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetImageMemoryRequirements2(VkDevice device, VkImageMemoryRequirementsInfo2* pInfo, VkMemoryRequirements2* pMemoryRequirements)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetImageSparseMemoryRequirements2(VkDevice device, VkImageSparseMemoryRequirementsInfo2* pInfo, uint32* pSparseMemoryRequirementCount, VkSparseImageMemoryRequirements2* pSparseMemoryRequirements)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkCreateSamplerYcbcrConversion(VkDevice device, VkSamplerYcbcrConversionCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkSamplerYcbcrConversion* pYcbcrConversion)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkDestroySamplerYcbcrConversion(VkDevice device, VkSamplerYcbcrConversion ycbcrConversion, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetDeviceQueue2(VkDevice device, VkDeviceQueueInfo2* pQueueInfo, VkQueue* pQueue)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern void vkGetDescriptorSetLayoutSupport(VkDevice device, VkDescriptorSetLayoutCreateInfo* pCreateInfo, VkDescriptorSetLayoutSupport* pSupport)
    
    [<CompilerMessage("vkImportInstanceDelegate is for internal use only", 1337, IsError=false, IsHidden=true)>]
    let vkImportInstanceDelegate<'a>(name : string) = 
        let ptr = vkGetInstanceProcAddr(activeInstance, name)
        if ptr = 0n then
            Log.warn "could not load function: %s" name
            Unchecked.defaultof<'a>
        else
            Report.Line(3, sprintf "loaded function %s (0x%08X)" name ptr)
            Marshal.GetDelegateForFunctionPointer(ptr, typeof<'a>) |> unbox<'a>

module EXTDebugReport =
    let Name = "VK_EXT_debug_report"
    let Number = 12
    
    type VkDebugReportObjectTypeEXT = 
        | VkDebugReportObjectTypeUnknownExt = 0
        | VkDebugReportObjectTypeInstanceExt = 1
        | VkDebugReportObjectTypePhysicalDeviceExt = 2
        | VkDebugReportObjectTypeDeviceExt = 3
        | VkDebugReportObjectTypeQueueExt = 4
        | VkDebugReportObjectTypeSemaphoreExt = 5
        | VkDebugReportObjectTypeCommandBufferExt = 6
        | VkDebugReportObjectTypeFenceExt = 7
        | VkDebugReportObjectTypeDeviceMemoryExt = 8
        | VkDebugReportObjectTypeBufferExt = 9
        | VkDebugReportObjectTypeImageExt = 10
        | VkDebugReportObjectTypeEventExt = 11
        | VkDebugReportObjectTypeQueryPoolExt = 12
        | VkDebugReportObjectTypeBufferViewExt = 13
        | VkDebugReportObjectTypeImageViewExt = 14
        | VkDebugReportObjectTypeShaderModuleExt = 15
        | VkDebugReportObjectTypePipelineCacheExt = 16
        | VkDebugReportObjectTypePipelineLayoutExt = 17
        | VkDebugReportObjectTypeRenderPassExt = 18
        | VkDebugReportObjectTypePipelineExt = 19
        | VkDebugReportObjectTypeDescriptorSetLayoutExt = 20
        | VkDebugReportObjectTypeSamplerExt = 21
        | VkDebugReportObjectTypeDescriptorPoolExt = 22
        | VkDebugReportObjectTypeDescriptorSetExt = 23
        | VkDebugReportObjectTypeFramebufferExt = 24
        | VkDebugReportObjectTypeCommandPoolExt = 25
        | VkDebugReportObjectTypeSurfaceKhrExt = 26
        | VkDebugReportObjectTypeSwapchainKhrExt = 27
        | VkDebugReportObjectTypeDebugReportCallbackExtExt = 28
        | VkDebugReportObjectTypeDisplayKhrExt = 29
        | VkDebugReportObjectTypeDisplayModeKhrExt = 30
        | VkDebugReportObjectTypeObjectTableNvxExt = 31
        | VkDebugReportObjectTypeIndirectCommandsLayoutNvxExt = 32
        | VkDebugReportObjectTypeValidationCacheExtExt = 33
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDebugReportCallbackCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkDebugReportFlagsEXT
            val mutable public pfnCallback : PFN_vkDebugReportCallbackEXT
            val mutable public pUserData : nativeint
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkDebugReportFlagsEXT
              , pfnCallback : PFN_vkDebugReportCallbackEXT
              , pUserData : nativeint
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    pfnCallback = pfnCallback
                    pUserData = pUserData
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "pfnCallback = %A" x.pfnCallback
                    sprintf "pUserData = %A" x.pUserData
                ] |> sprintf "VkDebugReportCallbackCreateInfoEXT { %s }"
        end
    
    
    type VkObjectType with
         static member inline DebugReportCallbackExt = unbox<VkObjectType> 1000011000
    type VkResult with
         static member inline VkErrorValidationFailedExt = unbox<VkResult> -1000011001
    type VkStructureType with
         static member inline DebugReportCallbackCreateInfoExt = unbox<VkStructureType> 1000011000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateDebugReportCallbackEXTDel = delegate of VkInstance * nativeptr<VkDebugReportCallbackCreateInfoEXT> * nativeptr<VkAllocationCallbacks> * nativeptr<VkDebugReportCallbackEXT> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkDestroyDebugReportCallbackEXTDel = delegate of VkInstance * VkDebugReportCallbackEXT * nativeptr<VkAllocationCallbacks> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkDebugReportMessageEXTDel = delegate of VkInstance * VkDebugReportFlagsEXT * VkDebugReportObjectTypeEXT * uint64 * uint64 * int * cstr * cstr -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_EXT_debug_report")
            static let s_vkCreateDebugReportCallbackEXTDel = VkRaw.vkImportInstanceDelegate<VkCreateDebugReportCallbackEXTDel> "vkCreateDebugReportCallbackEXT"
            static let s_vkDestroyDebugReportCallbackEXTDel = VkRaw.vkImportInstanceDelegate<VkDestroyDebugReportCallbackEXTDel> "vkDestroyDebugReportCallbackEXT"
            static let s_vkDebugReportMessageEXTDel = VkRaw.vkImportInstanceDelegate<VkDebugReportMessageEXTDel> "vkDebugReportMessageEXT"
            static do Report.End(3) |> ignore
            static member vkCreateDebugReportCallbackEXT = s_vkCreateDebugReportCallbackEXTDel
            static member vkDestroyDebugReportCallbackEXT = s_vkDestroyDebugReportCallbackEXTDel
            static member vkDebugReportMessageEXT = s_vkDebugReportMessageEXTDel
        let vkCreateDebugReportCallbackEXT(instance : VkInstance, pCreateInfo : nativeptr<VkDebugReportCallbackCreateInfoEXT>, pAllocator : nativeptr<VkAllocationCallbacks>, pCallback : nativeptr<VkDebugReportCallbackEXT>) = Loader<unit>.vkCreateDebugReportCallbackEXT.Invoke(instance, pCreateInfo, pAllocator, pCallback)
        let vkDestroyDebugReportCallbackEXT(instance : VkInstance, callback : VkDebugReportCallbackEXT, pAllocator : nativeptr<VkAllocationCallbacks>) = Loader<unit>.vkDestroyDebugReportCallbackEXT.Invoke(instance, callback, pAllocator)
        let vkDebugReportMessageEXT(instance : VkInstance, flags : VkDebugReportFlagsEXT, objectType : VkDebugReportObjectTypeEXT, _object : uint64, location : uint64, messageCode : int, pLayerPrefix : cstr, pMessage : cstr) = Loader<unit>.vkDebugReportMessageEXT.Invoke(instance, flags, objectType, _object, location, messageCode, pLayerPrefix, pMessage)

module AMDBufferMarker =
    let Name = "VK_AMD_buffer_marker"
    let Number = 180
    
    open EXTDebugReport
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdWriteBufferMarkerAMDDel = delegate of VkCommandBuffer * VkPipelineStageFlags * VkBuffer * VkDeviceSize * uint32 -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_AMD_buffer_marker")
            static let s_vkCmdWriteBufferMarkerAMDDel = VkRaw.vkImportInstanceDelegate<VkCmdWriteBufferMarkerAMDDel> "vkCmdWriteBufferMarkerAMD"
            static do Report.End(3) |> ignore
            static member vkCmdWriteBufferMarkerAMD = s_vkCmdWriteBufferMarkerAMDDel
        let vkCmdWriteBufferMarkerAMD(commandBuffer : VkCommandBuffer, pipelineStage : VkPipelineStageFlags, dstBuffer : VkBuffer, dstOffset : VkDeviceSize, marker : uint32) = Loader<unit>.vkCmdWriteBufferMarkerAMD.Invoke(commandBuffer, pipelineStage, dstBuffer, dstOffset, marker)

module AMDDrawIndirectCount =
    let Name = "VK_AMD_draw_indirect_count"
    let Number = 34
    
    open EXTDebugReport
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdDrawIndirectCountAMDDel = delegate of VkCommandBuffer * VkBuffer * VkDeviceSize * VkBuffer * VkDeviceSize * uint32 * uint32 -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdDrawIndexedIndirectCountAMDDel = delegate of VkCommandBuffer * VkBuffer * VkDeviceSize * VkBuffer * VkDeviceSize * uint32 * uint32 -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_AMD_draw_indirect_count")
            static let s_vkCmdDrawIndirectCountAMDDel = VkRaw.vkImportInstanceDelegate<VkCmdDrawIndirectCountAMDDel> "vkCmdDrawIndirectCountAMD"
            static let s_vkCmdDrawIndexedIndirectCountAMDDel = VkRaw.vkImportInstanceDelegate<VkCmdDrawIndexedIndirectCountAMDDel> "vkCmdDrawIndexedIndirectCountAMD"
            static do Report.End(3) |> ignore
            static member vkCmdDrawIndirectCountAMD = s_vkCmdDrawIndirectCountAMDDel
            static member vkCmdDrawIndexedIndirectCountAMD = s_vkCmdDrawIndexedIndirectCountAMDDel
        let vkCmdDrawIndirectCountAMD(commandBuffer : VkCommandBuffer, buffer : VkBuffer, offset : VkDeviceSize, countBuffer : VkBuffer, countBufferOffset : VkDeviceSize, maxDrawCount : uint32, stride : uint32) = Loader<unit>.vkCmdDrawIndirectCountAMD.Invoke(commandBuffer, buffer, offset, countBuffer, countBufferOffset, maxDrawCount, stride)
        let vkCmdDrawIndexedIndirectCountAMD(commandBuffer : VkCommandBuffer, buffer : VkBuffer, offset : VkDeviceSize, countBuffer : VkBuffer, countBufferOffset : VkDeviceSize, maxDrawCount : uint32, stride : uint32) = Loader<unit>.vkCmdDrawIndexedIndirectCountAMD.Invoke(commandBuffer, buffer, offset, countBuffer, countBufferOffset, maxDrawCount, stride)

module AMDGcnShader =
    let Name = "VK_AMD_gcn_shader"
    let Number = 26
    
    open EXTDebugReport
    
    
    
    

module AMDGpuShaderHalfFloat =
    let Name = "VK_AMD_gpu_shader_half_float"
    let Number = 37
    
    open EXTDebugReport
    
    
    
    

module AMDGpuShaderInt16 =
    let Name = "VK_AMD_gpu_shader_int16"
    let Number = 133
    
    open EXTDebugReport
    
    
    
    

module AMDMixedAttachmentSamples =
    let Name = "VK_AMD_mixed_attachment_samples"
    let Number = 137
    
    open EXTDebugReport
    
    
    
    

module AMDNegativeViewportHeight =
    let Name = "VK_AMD_negative_viewport_height"
    let Number = 36
    
    open EXTDebugReport
    
    
    
    

module AMDRasterizationOrder =
    let Name = "VK_AMD_rasterization_order"
    let Number = 19
    
    open EXTDebugReport
    
    type VkRasterizationOrderAMD = 
        | VkRasterizationOrderStrictAmd = 0
        | VkRasterizationOrderRelaxedAmd = 1
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPipelineRasterizationStateRasterizationOrderAMD = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public rasterizationOrder : VkRasterizationOrderAMD
    
            new(sType : VkStructureType
              , pNext : nativeint
              , rasterizationOrder : VkRasterizationOrderAMD
              ) =
                {
                    sType = sType
                    pNext = pNext
                    rasterizationOrder = rasterizationOrder
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "rasterizationOrder = %A" x.rasterizationOrder
                ] |> sprintf "VkPipelineRasterizationStateRasterizationOrderAMD { %s }"
        end
    
    
    type VkStructureType with
         static member inline PipelineRasterizationStateRasterizationOrderAmd = unbox<VkStructureType> 1000018000
    

module AMDShaderBallot =
    let Name = "VK_AMD_shader_ballot"
    let Number = 38
    
    open EXTDebugReport
    
    
    
    

module KHRGetPhysicalDeviceProperties2 =
    let Name = "VK_KHR_get_physical_device_properties2"
    let Number = 60
    
    open EXTDebugReport
    
    
    type VkFormatProperties2KHR = VkFormatProperties2
    type VkImageFormatProperties2KHR = VkImageFormatProperties2
    type VkPhysicalDeviceFeatures2KHR = VkPhysicalDeviceFeatures2
    type VkPhysicalDeviceImageFormatInfo2KHR = VkPhysicalDeviceImageFormatInfo2
    type VkPhysicalDeviceMemoryProperties2KHR = VkPhysicalDeviceMemoryProperties2
    type VkPhysicalDeviceProperties2KHR = VkPhysicalDeviceProperties2
    type VkPhysicalDeviceSparseImageFormatInfo2KHR = VkPhysicalDeviceSparseImageFormatInfo2
    type VkQueueFamilyProperties2KHR = VkQueueFamilyProperties2
    type VkSparseImageFormatProperties2KHR = VkSparseImageFormatProperties2
    
    

module AMDShaderCoreProperties =
    let Name = "VK_AMD_shader_core_properties"
    let Number = 186
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name ]
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceShaderCorePropertiesAMD = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public shaderEngineCount : uint32
            val mutable public shaderArraysPerEngineCount : uint32
            val mutable public computeUnitsPerShaderArray : uint32
            val mutable public simdPerComputeUnit : uint32
            val mutable public wavefrontsPerSimd : uint32
            val mutable public wavefrontSize : uint32
            val mutable public sgprsPerSimd : uint32
            val mutable public minSgprAllocation : uint32
            val mutable public maxSgprAllocation : uint32
            val mutable public sgprAllocationGranularity : uint32
            val mutable public vgprsPerSimd : uint32
            val mutable public minVgprAllocation : uint32
            val mutable public maxVgprAllocation : uint32
            val mutable public vgprAllocationGranularity : uint32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , shaderEngineCount : uint32
              , shaderArraysPerEngineCount : uint32
              , computeUnitsPerShaderArray : uint32
              , simdPerComputeUnit : uint32
              , wavefrontsPerSimd : uint32
              , wavefrontSize : uint32
              , sgprsPerSimd : uint32
              , minSgprAllocation : uint32
              , maxSgprAllocation : uint32
              , sgprAllocationGranularity : uint32
              , vgprsPerSimd : uint32
              , minVgprAllocation : uint32
              , maxVgprAllocation : uint32
              , vgprAllocationGranularity : uint32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    shaderEngineCount = shaderEngineCount
                    shaderArraysPerEngineCount = shaderArraysPerEngineCount
                    computeUnitsPerShaderArray = computeUnitsPerShaderArray
                    simdPerComputeUnit = simdPerComputeUnit
                    wavefrontsPerSimd = wavefrontsPerSimd
                    wavefrontSize = wavefrontSize
                    sgprsPerSimd = sgprsPerSimd
                    minSgprAllocation = minSgprAllocation
                    maxSgprAllocation = maxSgprAllocation
                    sgprAllocationGranularity = sgprAllocationGranularity
                    vgprsPerSimd = vgprsPerSimd
                    minVgprAllocation = minVgprAllocation
                    maxVgprAllocation = maxVgprAllocation
                    vgprAllocationGranularity = vgprAllocationGranularity
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "shaderEngineCount = %A" x.shaderEngineCount
                    sprintf "shaderArraysPerEngineCount = %A" x.shaderArraysPerEngineCount
                    sprintf "computeUnitsPerShaderArray = %A" x.computeUnitsPerShaderArray
                    sprintf "simdPerComputeUnit = %A" x.simdPerComputeUnit
                    sprintf "wavefrontsPerSimd = %A" x.wavefrontsPerSimd
                    sprintf "wavefrontSize = %A" x.wavefrontSize
                    sprintf "sgprsPerSimd = %A" x.sgprsPerSimd
                    sprintf "minSgprAllocation = %A" x.minSgprAllocation
                    sprintf "maxSgprAllocation = %A" x.maxSgprAllocation
                    sprintf "sgprAllocationGranularity = %A" x.sgprAllocationGranularity
                    sprintf "vgprsPerSimd = %A" x.vgprsPerSimd
                    sprintf "minVgprAllocation = %A" x.minVgprAllocation
                    sprintf "maxVgprAllocation = %A" x.maxVgprAllocation
                    sprintf "vgprAllocationGranularity = %A" x.vgprAllocationGranularity
                ] |> sprintf "VkPhysicalDeviceShaderCorePropertiesAMD { %s }"
        end
    
    
    type VkStructureType with
         static member inline PhysicalDeviceShaderCorePropertiesAmd = unbox<VkStructureType> 1000185000
    

module AMDShaderExplicitVertexParameter =
    let Name = "VK_AMD_shader_explicit_vertex_parameter"
    let Number = 22
    
    open EXTDebugReport
    
    
    
    

module AMDShaderFragmentMask =
    let Name = "VK_AMD_shader_fragment_mask"
    let Number = 138
    
    open EXTDebugReport
    
    
    
    

module AMDShaderImageLoadStoreLod =
    let Name = "VK_AMD_shader_image_load_store_lod"
    let Number = 47
    
    open EXTDebugReport
    
    
    
    

module AMDShaderInfo =
    let Name = "VK_AMD_shader_info"
    let Number = 43
    
    open EXTDebugReport
    
    type VkShaderInfoTypeAMD = 
        | VkShaderInfoTypeStatisticsAmd = 0
        | VkShaderInfoTypeBinaryAmd = 1
        | VkShaderInfoTypeDisassemblyAmd = 2
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkShaderResourceUsageAMD = 
        struct
            val mutable public numUsedVgprs : uint32
            val mutable public numUsedSgprs : uint32
            val mutable public ldsSizePerLocalWorkGroup : uint32
            val mutable public ldsUsageSizeInBytes : uint64
            val mutable public scratchMemUsageInBytes : uint64
    
            new(numUsedVgprs : uint32
              , numUsedSgprs : uint32
              , ldsSizePerLocalWorkGroup : uint32
              , ldsUsageSizeInBytes : uint64
              , scratchMemUsageInBytes : uint64
              ) =
                {
                    numUsedVgprs = numUsedVgprs
                    numUsedSgprs = numUsedSgprs
                    ldsSizePerLocalWorkGroup = ldsSizePerLocalWorkGroup
                    ldsUsageSizeInBytes = ldsUsageSizeInBytes
                    scratchMemUsageInBytes = scratchMemUsageInBytes
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "numUsedVgprs = %A" x.numUsedVgprs
                    sprintf "numUsedSgprs = %A" x.numUsedSgprs
                    sprintf "ldsSizePerLocalWorkGroup = %A" x.ldsSizePerLocalWorkGroup
                    sprintf "ldsUsageSizeInBytes = %A" x.ldsUsageSizeInBytes
                    sprintf "scratchMemUsageInBytes = %A" x.scratchMemUsageInBytes
                ] |> sprintf "VkShaderResourceUsageAMD { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkShaderStatisticsInfoAMD = 
        struct
            val mutable public shaderStageMask : VkShaderStageFlags
            val mutable public resourceUsage : VkShaderResourceUsageAMD
            val mutable public numPhysicalVgprs : uint32
            val mutable public numPhysicalSgprs : uint32
            val mutable public numAvailableVgprs : uint32
            val mutable public numAvailableSgprs : uint32
            val mutable public computeWorkGroupSize : V3ui
    
            new(shaderStageMask : VkShaderStageFlags
              , resourceUsage : VkShaderResourceUsageAMD
              , numPhysicalVgprs : uint32
              , numPhysicalSgprs : uint32
              , numAvailableVgprs : uint32
              , numAvailableSgprs : uint32
              , computeWorkGroupSize : V3ui
              ) =
                {
                    shaderStageMask = shaderStageMask
                    resourceUsage = resourceUsage
                    numPhysicalVgprs = numPhysicalVgprs
                    numPhysicalSgprs = numPhysicalSgprs
                    numAvailableVgprs = numAvailableVgprs
                    numAvailableSgprs = numAvailableSgprs
                    computeWorkGroupSize = computeWorkGroupSize
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "shaderStageMask = %A" x.shaderStageMask
                    sprintf "resourceUsage = %A" x.resourceUsage
                    sprintf "numPhysicalVgprs = %A" x.numPhysicalVgprs
                    sprintf "numPhysicalSgprs = %A" x.numPhysicalSgprs
                    sprintf "numAvailableVgprs = %A" x.numAvailableVgprs
                    sprintf "numAvailableSgprs = %A" x.numAvailableSgprs
                    sprintf "computeWorkGroupSize = %A" x.computeWorkGroupSize
                ] |> sprintf "VkShaderStatisticsInfoAMD { %s }"
        end
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetShaderInfoAMDDel = delegate of VkDevice * VkPipeline * VkShaderStageFlags * VkShaderInfoTypeAMD * nativeptr<uint64> * nativeint -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_AMD_shader_info")
            static let s_vkGetShaderInfoAMDDel = VkRaw.vkImportInstanceDelegate<VkGetShaderInfoAMDDel> "vkGetShaderInfoAMD"
            static do Report.End(3) |> ignore
            static member vkGetShaderInfoAMD = s_vkGetShaderInfoAMDDel
        let vkGetShaderInfoAMD(device : VkDevice, pipeline : VkPipeline, shaderStage : VkShaderStageFlags, infoType : VkShaderInfoTypeAMD, pInfoSize : nativeptr<uint64>, pInfo : nativeint) = Loader<unit>.vkGetShaderInfoAMD.Invoke(device, pipeline, shaderStage, infoType, pInfoSize, pInfo)

module AMDShaderTrinaryMinmax =
    let Name = "VK_AMD_shader_trinary_minmax"
    let Number = 21
    
    open EXTDebugReport
    
    
    
    

module AMDTextureGatherBiasLod =
    let Name = "VK_AMD_texture_gather_bias_lod"
    let Number = 42
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name ]
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkTextureLODGatherFormatPropertiesAMD = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public supportsTextureGatherLODBiasAMD : VkBool32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , supportsTextureGatherLODBiasAMD : VkBool32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    supportsTextureGatherLODBiasAMD = supportsTextureGatherLODBiasAMD
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "supportsTextureGatherLODBiasAMD = %A" x.supportsTextureGatherLODBiasAMD
                ] |> sprintf "VkTextureLODGatherFormatPropertiesAMD { %s }"
        end
    
    
    type VkStructureType with
         static member inline TextureLodGatherFormatPropertiesAmd = unbox<VkStructureType> 1000041000
    

module KHRMaintenance1 =
    let Name = "VK_KHR_maintenance1"
    let Number = 70
    
    open EXTDebugReport
    
    
    
    

module KHRBindMemory2 =
    let Name = "VK_KHR_bind_memory2"
    let Number = 158
    
    open EXTDebugReport
    
    
    type VkBindBufferMemoryInfoKHR = VkBindBufferMemoryInfo
    type VkBindImageMemoryInfoKHR = VkBindImageMemoryInfo
    
    

module KHRGetMemoryRequirements2 =
    let Name = "VK_KHR_get_memory_requirements2"
    let Number = 147
    
    open EXTDebugReport
    
    
    type VkBufferMemoryRequirementsInfo2KHR = VkBufferMemoryRequirementsInfo2
    type VkImageMemoryRequirementsInfo2KHR = VkImageMemoryRequirementsInfo2
    type VkImageSparseMemoryRequirementsInfo2KHR = VkImageSparseMemoryRequirementsInfo2
    type VkMemoryRequirements2KHR = VkMemoryRequirements2
    type VkSparseImageMemoryRequirements2KHR = VkSparseImageMemoryRequirements2
    
    

module KHRSamplerYcbcrConversion =
    let Name = "VK_KHR_sampler_ycbcr_conversion"
    let Number = 157
    
    let Required = [ KHRBindMemory2.Name; KHRGetMemoryRequirements2.Name; KHRGetPhysicalDeviceProperties2.Name; KHRMaintenance1.Name ]
    open KHRBindMemory2
    open KHRGetMemoryRequirements2
    open KHRGetPhysicalDeviceProperties2
    open KHRMaintenance1
    open EXTDebugReport
    
    
    type VkBindImagePlaneMemoryInfoKHR = VkBindImagePlaneMemoryInfo
    type VkImagePlaneMemoryRequirementsInfoKHR = VkImagePlaneMemoryRequirementsInfo
    type VkPhysicalDeviceSamplerYcbcrConversionFeaturesKHR = VkPhysicalDeviceSamplerYcbcrConversionFeatures
    type VkSamplerYcbcrConversionCreateInfoKHR = VkSamplerYcbcrConversionCreateInfo
    type VkSamplerYcbcrConversionImageFormatPropertiesKHR = VkSamplerYcbcrConversionImageFormatProperties
    type VkSamplerYcbcrConversionInfoKHR = VkSamplerYcbcrConversionInfo
    
    
    
    module EXTDebugReport =
        
        
        type VkDebugReportObjectTypeEXT with
             static member inline VkDebugReportObjectTypeSamplerYcbcrConversionExt = unbox<VkDebugReportObjectTypeEXT> 999998000
        

module KHRExternalMemoryCapabilities =
    let Name = "VK_KHR_external_memory_capabilities"
    let Number = 72
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name ]
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    type VkExternalBufferPropertiesKHR = VkExternalBufferProperties
    type VkExternalImageFormatPropertiesKHR = VkExternalImageFormatProperties
    type VkExternalMemoryPropertiesKHR = VkExternalMemoryProperties
    type VkPhysicalDeviceExternalBufferInfoKHR = VkPhysicalDeviceExternalBufferInfo
    type VkPhysicalDeviceExternalImageFormatInfoKHR = VkPhysicalDeviceExternalImageFormatInfo
    type VkPhysicalDeviceIDPropertiesKHR = VkPhysicalDeviceIDProperties
    
    

module KHRExternalMemory =
    let Name = "VK_KHR_external_memory"
    let Number = 73
    
    let Required = [ KHRExternalMemoryCapabilities.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalMemoryCapabilities
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    type VkExportMemoryAllocateInfoKHR = VkExportMemoryAllocateInfo
    type VkExternalMemoryBufferCreateInfoKHR = VkExternalMemoryBufferCreateInfo
    type VkExternalMemoryImageCreateInfoKHR = VkExternalMemoryImageCreateInfo
    
    

module EXTQueueFamilyForeign =
    let Name = "VK_EXT_queue_family_foreign"
    let Number = 127
    
    let Required = [ KHRExternalMemory.Name; KHRExternalMemoryCapabilities.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalMemory
    open KHRExternalMemoryCapabilities
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    
    

module ANDROIDExternalMemoryAndroidHardwareBuffer =
    let Name = "VK_ANDROID_external_memory_android_hardware_buffer"
    let Number = 130
    
    let Required = [ EXTQueueFamilyForeign.Name; KHRBindMemory2.Name; KHRExternalMemory.Name; KHRExternalMemoryCapabilities.Name; KHRGetMemoryRequirements2.Name; KHRGetPhysicalDeviceProperties2.Name; KHRMaintenance1.Name; KHRSamplerYcbcrConversion.Name ]
    open EXTQueueFamilyForeign
    open KHRBindMemory2
    open KHRExternalMemory
    open KHRExternalMemoryCapabilities
    open KHRGetMemoryRequirements2
    open KHRGetPhysicalDeviceProperties2
    open KHRMaintenance1
    open KHRSamplerYcbcrConversion
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkAndroidHardwareBufferFormatPropertiesANDROID = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public format : VkFormat
            val mutable public externalFormat : uint64
            val mutable public formatFeatures : VkFormatFeatureFlags
            val mutable public samplerYcbcrConversionComponents : VkComponentMapping
            val mutable public suggestedYcbcrModel : VkSamplerYcbcrModelConversion
            val mutable public suggestedYcbcrRange : VkSamplerYcbcrRange
            val mutable public suggestedXChromaOffset : VkChromaLocation
            val mutable public suggestedYChromaOffset : VkChromaLocation
    
            new(sType : VkStructureType
              , pNext : nativeint
              , format : VkFormat
              , externalFormat : uint64
              , formatFeatures : VkFormatFeatureFlags
              , samplerYcbcrConversionComponents : VkComponentMapping
              , suggestedYcbcrModel : VkSamplerYcbcrModelConversion
              , suggestedYcbcrRange : VkSamplerYcbcrRange
              , suggestedXChromaOffset : VkChromaLocation
              , suggestedYChromaOffset : VkChromaLocation
              ) =
                {
                    sType = sType
                    pNext = pNext
                    format = format
                    externalFormat = externalFormat
                    formatFeatures = formatFeatures
                    samplerYcbcrConversionComponents = samplerYcbcrConversionComponents
                    suggestedYcbcrModel = suggestedYcbcrModel
                    suggestedYcbcrRange = suggestedYcbcrRange
                    suggestedXChromaOffset = suggestedXChromaOffset
                    suggestedYChromaOffset = suggestedYChromaOffset
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "format = %A" x.format
                    sprintf "externalFormat = %A" x.externalFormat
                    sprintf "formatFeatures = %A" x.formatFeatures
                    sprintf "samplerYcbcrConversionComponents = %A" x.samplerYcbcrConversionComponents
                    sprintf "suggestedYcbcrModel = %A" x.suggestedYcbcrModel
                    sprintf "suggestedYcbcrRange = %A" x.suggestedYcbcrRange
                    sprintf "suggestedXChromaOffset = %A" x.suggestedXChromaOffset
                    sprintf "suggestedYChromaOffset = %A" x.suggestedYChromaOffset
                ] |> sprintf "VkAndroidHardwareBufferFormatPropertiesANDROID { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkAndroidHardwareBufferPropertiesANDROID = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public allocationSize : VkDeviceSize
            val mutable public memoryTypeBits : uint32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , allocationSize : VkDeviceSize
              , memoryTypeBits : uint32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    allocationSize = allocationSize
                    memoryTypeBits = memoryTypeBits
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "allocationSize = %A" x.allocationSize
                    sprintf "memoryTypeBits = %A" x.memoryTypeBits
                ] |> sprintf "VkAndroidHardwareBufferPropertiesANDROID { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkAndroidHardwareBufferUsageANDROID = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public androidHardwareBufferUsage : uint64
    
            new(sType : VkStructureType
              , pNext : nativeint
              , androidHardwareBufferUsage : uint64
              ) =
                {
                    sType = sType
                    pNext = pNext
                    androidHardwareBufferUsage = androidHardwareBufferUsage
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "androidHardwareBufferUsage = %A" x.androidHardwareBufferUsage
                ] |> sprintf "VkAndroidHardwareBufferUsageANDROID { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExternalFormatANDROID = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public externalFormat : uint64
    
            new(sType : VkStructureType
              , pNext : nativeint
              , externalFormat : uint64
              ) =
                {
                    sType = sType
                    pNext = pNext
                    externalFormat = externalFormat
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "externalFormat = %A" x.externalFormat
                ] |> sprintf "VkExternalFormatANDROID { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImportAndroidHardwareBufferInfoANDROID = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public buffer : nativeptr<nativeint>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , buffer : nativeptr<nativeint>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    buffer = buffer
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "buffer = %A" x.buffer
                ] |> sprintf "VkImportAndroidHardwareBufferInfoANDROID { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMemoryGetAndroidHardwareBufferInfoANDROID = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public memory : VkDeviceMemory
    
            new(sType : VkStructureType
              , pNext : nativeint
              , memory : VkDeviceMemory
              ) =
                {
                    sType = sType
                    pNext = pNext
                    memory = memory
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "memory = %A" x.memory
                ] |> sprintf "VkMemoryGetAndroidHardwareBufferInfoANDROID { %s }"
        end
    
    
    type VkExternalMemoryHandleTypeFlags with
         static member inline AndroidHardwareBufferBitAndroid = unbox<VkExternalMemoryHandleTypeFlags> 1024
    type VkStructureType with
         static member inline AndroidHardwareBufferUsageAndroid = unbox<VkStructureType> 1000129000
         static member inline AndroidHardwareBufferPropertiesAndroid = unbox<VkStructureType> 1000129001
         static member inline AndroidHardwareBufferFormatPropertiesAndroid = unbox<VkStructureType> 1000129002
         static member inline ImportAndroidHardwareBufferInfoAndroid = unbox<VkStructureType> 1000129003
         static member inline MemoryGetAndroidHardwareBufferInfoAndroid = unbox<VkStructureType> 1000129004
         static member inline ExternalFormatAndroid = unbox<VkStructureType> 1000129005
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetAndroidHardwareBufferPropertiesANDROIDDel = delegate of VkDevice * nativeptr<nativeint> * nativeptr<VkAndroidHardwareBufferPropertiesANDROID> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetMemoryAndroidHardwareBufferANDROIDDel = delegate of VkDevice * nativeptr<VkMemoryGetAndroidHardwareBufferInfoANDROID> * nativeptr<nativeptr<nativeint>> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_ANDROID_external_memory_android_hardware_buffer")
            static let s_vkGetAndroidHardwareBufferPropertiesANDROIDDel = VkRaw.vkImportInstanceDelegate<VkGetAndroidHardwareBufferPropertiesANDROIDDel> "vkGetAndroidHardwareBufferPropertiesANDROID"
            static let s_vkGetMemoryAndroidHardwareBufferANDROIDDel = VkRaw.vkImportInstanceDelegate<VkGetMemoryAndroidHardwareBufferANDROIDDel> "vkGetMemoryAndroidHardwareBufferANDROID"
            static do Report.End(3) |> ignore
            static member vkGetAndroidHardwareBufferPropertiesANDROID = s_vkGetAndroidHardwareBufferPropertiesANDROIDDel
            static member vkGetMemoryAndroidHardwareBufferANDROID = s_vkGetMemoryAndroidHardwareBufferANDROIDDel
        let vkGetAndroidHardwareBufferPropertiesANDROID(device : VkDevice, buffer : nativeptr<nativeint>, pProperties : nativeptr<VkAndroidHardwareBufferPropertiesANDROID>) = Loader<unit>.vkGetAndroidHardwareBufferPropertiesANDROID.Invoke(device, buffer, pProperties)
        let vkGetMemoryAndroidHardwareBufferANDROID(device : VkDevice, pInfo : nativeptr<VkMemoryGetAndroidHardwareBufferInfoANDROID>, pBuffer : nativeptr<nativeptr<nativeint>>) = Loader<unit>.vkGetMemoryAndroidHardwareBufferANDROID.Invoke(device, pInfo, pBuffer)

module ANDROIDNativeBuffer =
    let Name = "VK_ANDROID_native_buffer"
    let Number = 11
    
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkNativeBufferANDROID = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handle : nativeint
            val mutable public stride : int
            val mutable public format : int
            val mutable public usage : int
    
            new(sType : VkStructureType
              , pNext : nativeint
              , handle : nativeint
              , stride : int
              , format : int
              , usage : int
              ) =
                {
                    sType = sType
                    pNext = pNext
                    handle = handle
                    stride = stride
                    format = format
                    usage = usage
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "handle = %A" x.handle
                    sprintf "stride = %A" x.stride
                    sprintf "format = %A" x.format
                    sprintf "usage = %A" x.usage
                ] |> sprintf "VkNativeBufferANDROID { %s }"
        end
    
    
    type VkStructureType with
         static member inline NativeBufferAndroid = unbox<VkStructureType> 1000010000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetSwapchainGrallocUsageANDROIDDel = delegate of VkDevice * VkFormat * VkImageUsageFlags * nativeptr<int> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkAcquireImageANDROIDDel = delegate of VkDevice * VkImage * int * VkSemaphore * VkFence -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkQueueSignalReleaseImageANDROIDDel = delegate of VkQueue * uint32 * nativeptr<VkSemaphore> * VkImage * nativeptr<int> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_ANDROID_native_buffer")
            static let s_vkGetSwapchainGrallocUsageANDROIDDel = VkRaw.vkImportInstanceDelegate<VkGetSwapchainGrallocUsageANDROIDDel> "vkGetSwapchainGrallocUsageANDROID"
            static let s_vkAcquireImageANDROIDDel = VkRaw.vkImportInstanceDelegate<VkAcquireImageANDROIDDel> "vkAcquireImageANDROID"
            static let s_vkQueueSignalReleaseImageANDROIDDel = VkRaw.vkImportInstanceDelegate<VkQueueSignalReleaseImageANDROIDDel> "vkQueueSignalReleaseImageANDROID"
            static do Report.End(3) |> ignore
            static member vkGetSwapchainGrallocUsageANDROID = s_vkGetSwapchainGrallocUsageANDROIDDel
            static member vkAcquireImageANDROID = s_vkAcquireImageANDROIDDel
            static member vkQueueSignalReleaseImageANDROID = s_vkQueueSignalReleaseImageANDROIDDel
        let vkGetSwapchainGrallocUsageANDROID(device : VkDevice, format : VkFormat, imageUsage : VkImageUsageFlags, grallocUsage : nativeptr<int>) = Loader<unit>.vkGetSwapchainGrallocUsageANDROID.Invoke(device, format, imageUsage, grallocUsage)
        let vkAcquireImageANDROID(device : VkDevice, image : VkImage, nativeFenceFd : int, semaphore : VkSemaphore, fence : VkFence) = Loader<unit>.vkAcquireImageANDROID.Invoke(device, image, nativeFenceFd, semaphore, fence)
        let vkQueueSignalReleaseImageANDROID(queue : VkQueue, waitSemaphoreCount : uint32, pWaitSemaphores : nativeptr<VkSemaphore>, image : VkImage, pNativeFenceFd : nativeptr<int>) = Loader<unit>.vkQueueSignalReleaseImageANDROID.Invoke(queue, waitSemaphoreCount, pWaitSemaphores, image, pNativeFenceFd)

module KHRSurface =
    let Name = "VK_KHR_surface"
    let Number = 1
    
    open EXTDebugReport
    
    
    
    type VkObjectType with
         static member inline SurfaceKhr = unbox<VkObjectType> 1000000000
    type VkResult with
         static member inline VkErrorSurfaceLostKhr = unbox<VkResult> -1000000000
         static member inline VkErrorNativeWindowInUseKhr = unbox<VkResult> -1000000001
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkDestroySurfaceKHRDel = delegate of VkInstance * VkSurfaceKHR * nativeptr<VkAllocationCallbacks> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceSurfaceSupportKHRDel = delegate of VkPhysicalDevice * uint32 * VkSurfaceKHR * nativeptr<VkBool32> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceSurfaceCapabilitiesKHRDel = delegate of VkPhysicalDevice * VkSurfaceKHR * nativeptr<VkSurfaceCapabilitiesKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceSurfaceFormatsKHRDel = delegate of VkPhysicalDevice * VkSurfaceKHR * nativeptr<uint32> * nativeptr<VkSurfaceFormatKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceSurfacePresentModesKHRDel = delegate of VkPhysicalDevice * VkSurfaceKHR * nativeptr<uint32> * nativeptr<VkPresentModeKHR> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_surface")
            static let s_vkDestroySurfaceKHRDel = VkRaw.vkImportInstanceDelegate<VkDestroySurfaceKHRDel> "vkDestroySurfaceKHR"
            static let s_vkGetPhysicalDeviceSurfaceSupportKHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceSurfaceSupportKHRDel> "vkGetPhysicalDeviceSurfaceSupportKHR"
            static let s_vkGetPhysicalDeviceSurfaceCapabilitiesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceSurfaceCapabilitiesKHRDel> "vkGetPhysicalDeviceSurfaceCapabilitiesKHR"
            static let s_vkGetPhysicalDeviceSurfaceFormatsKHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceSurfaceFormatsKHRDel> "vkGetPhysicalDeviceSurfaceFormatsKHR"
            static let s_vkGetPhysicalDeviceSurfacePresentModesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceSurfacePresentModesKHRDel> "vkGetPhysicalDeviceSurfacePresentModesKHR"
            static do Report.End(3) |> ignore
            static member vkDestroySurfaceKHR = s_vkDestroySurfaceKHRDel
            static member vkGetPhysicalDeviceSurfaceSupportKHR = s_vkGetPhysicalDeviceSurfaceSupportKHRDel
            static member vkGetPhysicalDeviceSurfaceCapabilitiesKHR = s_vkGetPhysicalDeviceSurfaceCapabilitiesKHRDel
            static member vkGetPhysicalDeviceSurfaceFormatsKHR = s_vkGetPhysicalDeviceSurfaceFormatsKHRDel
            static member vkGetPhysicalDeviceSurfacePresentModesKHR = s_vkGetPhysicalDeviceSurfacePresentModesKHRDel
        let vkDestroySurfaceKHR(instance : VkInstance, surface : VkSurfaceKHR, pAllocator : nativeptr<VkAllocationCallbacks>) = Loader<unit>.vkDestroySurfaceKHR.Invoke(instance, surface, pAllocator)
        let vkGetPhysicalDeviceSurfaceSupportKHR(physicalDevice : VkPhysicalDevice, queueFamilyIndex : uint32, surface : VkSurfaceKHR, pSupported : nativeptr<VkBool32>) = Loader<unit>.vkGetPhysicalDeviceSurfaceSupportKHR.Invoke(physicalDevice, queueFamilyIndex, surface, pSupported)
        let vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physicalDevice : VkPhysicalDevice, surface : VkSurfaceKHR, pSurfaceCapabilities : nativeptr<VkSurfaceCapabilitiesKHR>) = Loader<unit>.vkGetPhysicalDeviceSurfaceCapabilitiesKHR.Invoke(physicalDevice, surface, pSurfaceCapabilities)
        let vkGetPhysicalDeviceSurfaceFormatsKHR(physicalDevice : VkPhysicalDevice, surface : VkSurfaceKHR, pSurfaceFormatCount : nativeptr<uint32>, pSurfaceFormats : nativeptr<VkSurfaceFormatKHR>) = Loader<unit>.vkGetPhysicalDeviceSurfaceFormatsKHR.Invoke(physicalDevice, surface, pSurfaceFormatCount, pSurfaceFormats)
        let vkGetPhysicalDeviceSurfacePresentModesKHR(physicalDevice : VkPhysicalDevice, surface : VkSurfaceKHR, pPresentModeCount : nativeptr<uint32>, pPresentModes : nativeptr<VkPresentModeKHR>) = Loader<unit>.vkGetPhysicalDeviceSurfacePresentModesKHR.Invoke(physicalDevice, surface, pPresentModeCount, pPresentModes)

module KHRDisplay =
    let Name = "VK_KHR_display"
    let Number = 3
    
    let Required = [ KHRSurface.Name ]
    open KHRSurface
    open EXTDebugReport
    
    [<Flags>]
    type VkDisplayPlaneAlphaFlagsKHR = 
        | None = 0
        | VkDisplayPlaneAlphaOpaqueBitKhr = 0x00000001
        | VkDisplayPlaneAlphaGlobalBitKhr = 0x00000002
        | VkDisplayPlaneAlphaPerPixelBitKhr = 0x00000004
        | VkDisplayPlaneAlphaPerPixelPremultipliedBitKhr = 0x00000008
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDisplayModeParametersKHR = 
        struct
            val mutable public visibleRegion : VkExtent2D
            val mutable public refreshRate : uint32
    
            new(visibleRegion : VkExtent2D
              , refreshRate : uint32
              ) =
                {
                    visibleRegion = visibleRegion
                    refreshRate = refreshRate
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "visibleRegion = %A" x.visibleRegion
                    sprintf "refreshRate = %A" x.refreshRate
                ] |> sprintf "VkDisplayModeParametersKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDisplayModeCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkDisplayModeCreateFlagsKHR
            val mutable public parameters : VkDisplayModeParametersKHR
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkDisplayModeCreateFlagsKHR
              , parameters : VkDisplayModeParametersKHR
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    parameters = parameters
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "parameters = %A" x.parameters
                ] |> sprintf "VkDisplayModeCreateInfoKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDisplayModePropertiesKHR = 
        struct
            val mutable public displayMode : VkDisplayModeKHR
            val mutable public parameters : VkDisplayModeParametersKHR
    
            new(displayMode : VkDisplayModeKHR
              , parameters : VkDisplayModeParametersKHR
              ) =
                {
                    displayMode = displayMode
                    parameters = parameters
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "displayMode = %A" x.displayMode
                    sprintf "parameters = %A" x.parameters
                ] |> sprintf "VkDisplayModePropertiesKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDisplayPlaneCapabilitiesKHR = 
        struct
            val mutable public supportedAlpha : VkDisplayPlaneAlphaFlagsKHR
            val mutable public minSrcPosition : VkOffset2D
            val mutable public maxSrcPosition : VkOffset2D
            val mutable public minSrcExtent : VkExtent2D
            val mutable public maxSrcExtent : VkExtent2D
            val mutable public minDstPosition : VkOffset2D
            val mutable public maxDstPosition : VkOffset2D
            val mutable public minDstExtent : VkExtent2D
            val mutable public maxDstExtent : VkExtent2D
    
            new(supportedAlpha : VkDisplayPlaneAlphaFlagsKHR
              , minSrcPosition : VkOffset2D
              , maxSrcPosition : VkOffset2D
              , minSrcExtent : VkExtent2D
              , maxSrcExtent : VkExtent2D
              , minDstPosition : VkOffset2D
              , maxDstPosition : VkOffset2D
              , minDstExtent : VkExtent2D
              , maxDstExtent : VkExtent2D
              ) =
                {
                    supportedAlpha = supportedAlpha
                    minSrcPosition = minSrcPosition
                    maxSrcPosition = maxSrcPosition
                    minSrcExtent = minSrcExtent
                    maxSrcExtent = maxSrcExtent
                    minDstPosition = minDstPosition
                    maxDstPosition = maxDstPosition
                    minDstExtent = minDstExtent
                    maxDstExtent = maxDstExtent
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "supportedAlpha = %A" x.supportedAlpha
                    sprintf "minSrcPosition = %A" x.minSrcPosition
                    sprintf "maxSrcPosition = %A" x.maxSrcPosition
                    sprintf "minSrcExtent = %A" x.minSrcExtent
                    sprintf "maxSrcExtent = %A" x.maxSrcExtent
                    sprintf "minDstPosition = %A" x.minDstPosition
                    sprintf "maxDstPosition = %A" x.maxDstPosition
                    sprintf "minDstExtent = %A" x.minDstExtent
                    sprintf "maxDstExtent = %A" x.maxDstExtent
                ] |> sprintf "VkDisplayPlaneCapabilitiesKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDisplayPlanePropertiesKHR = 
        struct
            val mutable public currentDisplay : VkDisplayKHR
            val mutable public currentStackIndex : uint32
    
            new(currentDisplay : VkDisplayKHR
              , currentStackIndex : uint32
              ) =
                {
                    currentDisplay = currentDisplay
                    currentStackIndex = currentStackIndex
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "currentDisplay = %A" x.currentDisplay
                    sprintf "currentStackIndex = %A" x.currentStackIndex
                ] |> sprintf "VkDisplayPlanePropertiesKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDisplayPropertiesKHR = 
        struct
            val mutable public display : VkDisplayKHR
            val mutable public displayName : cstr
            val mutable public physicalDimensions : VkExtent2D
            val mutable public physicalResolution : VkExtent2D
            val mutable public supportedTransforms : VkSurfaceTransformFlagsKHR
            val mutable public planeReorderPossible : VkBool32
            val mutable public persistentContent : VkBool32
    
            new(display : VkDisplayKHR
              , displayName : cstr
              , physicalDimensions : VkExtent2D
              , physicalResolution : VkExtent2D
              , supportedTransforms : VkSurfaceTransformFlagsKHR
              , planeReorderPossible : VkBool32
              , persistentContent : VkBool32
              ) =
                {
                    display = display
                    displayName = displayName
                    physicalDimensions = physicalDimensions
                    physicalResolution = physicalResolution
                    supportedTransforms = supportedTransforms
                    planeReorderPossible = planeReorderPossible
                    persistentContent = persistentContent
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "display = %A" x.display
                    sprintf "displayName = %A" x.displayName
                    sprintf "physicalDimensions = %A" x.physicalDimensions
                    sprintf "physicalResolution = %A" x.physicalResolution
                    sprintf "supportedTransforms = %A" x.supportedTransforms
                    sprintf "planeReorderPossible = %A" x.planeReorderPossible
                    sprintf "persistentContent = %A" x.persistentContent
                ] |> sprintf "VkDisplayPropertiesKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDisplaySurfaceCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkDisplaySurfaceCreateFlagsKHR
            val mutable public displayMode : VkDisplayModeKHR
            val mutable public planeIndex : uint32
            val mutable public planeStackIndex : uint32
            val mutable public transform : VkSurfaceTransformFlagsKHR
            val mutable public globalAlpha : float32
            val mutable public alphaMode : VkDisplayPlaneAlphaFlagsKHR
            val mutable public imageExtent : VkExtent2D
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkDisplaySurfaceCreateFlagsKHR
              , displayMode : VkDisplayModeKHR
              , planeIndex : uint32
              , planeStackIndex : uint32
              , transform : VkSurfaceTransformFlagsKHR
              , globalAlpha : float32
              , alphaMode : VkDisplayPlaneAlphaFlagsKHR
              , imageExtent : VkExtent2D
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    displayMode = displayMode
                    planeIndex = planeIndex
                    planeStackIndex = planeStackIndex
                    transform = transform
                    globalAlpha = globalAlpha
                    alphaMode = alphaMode
                    imageExtent = imageExtent
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "displayMode = %A" x.displayMode
                    sprintf "planeIndex = %A" x.planeIndex
                    sprintf "planeStackIndex = %A" x.planeStackIndex
                    sprintf "transform = %A" x.transform
                    sprintf "globalAlpha = %A" x.globalAlpha
                    sprintf "alphaMode = %A" x.alphaMode
                    sprintf "imageExtent = %A" x.imageExtent
                ] |> sprintf "VkDisplaySurfaceCreateInfoKHR { %s }"
        end
    
    
    type VkObjectType with
         static member inline DisplayKhr = unbox<VkObjectType> 1000002000
         static member inline DisplayModeKhr = unbox<VkObjectType> 1000002001
    type VkStructureType with
         static member inline DisplayModeCreateInfoKhr = unbox<VkStructureType> 1000002000
         static member inline DisplaySurfaceCreateInfoKhr = unbox<VkStructureType> 1000002001
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceDisplayPropertiesKHRDel = delegate of VkPhysicalDevice * nativeptr<uint32> * nativeptr<VkDisplayPropertiesKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceDisplayPlanePropertiesKHRDel = delegate of VkPhysicalDevice * nativeptr<uint32> * nativeptr<VkDisplayPlanePropertiesKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetDisplayPlaneSupportedDisplaysKHRDel = delegate of VkPhysicalDevice * uint32 * nativeptr<uint32> * nativeptr<VkDisplayKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetDisplayModePropertiesKHRDel = delegate of VkPhysicalDevice * VkDisplayKHR * nativeptr<uint32> * nativeptr<VkDisplayModePropertiesKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateDisplayModeKHRDel = delegate of VkPhysicalDevice * VkDisplayKHR * nativeptr<VkDisplayModeCreateInfoKHR> * nativeptr<VkAllocationCallbacks> * nativeptr<VkDisplayModeKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetDisplayPlaneCapabilitiesKHRDel = delegate of VkPhysicalDevice * VkDisplayModeKHR * uint32 * nativeptr<VkDisplayPlaneCapabilitiesKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateDisplayPlaneSurfaceKHRDel = delegate of VkInstance * nativeptr<VkDisplaySurfaceCreateInfoKHR> * nativeptr<VkAllocationCallbacks> * nativeptr<VkSurfaceKHR> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_display")
            static let s_vkGetPhysicalDeviceDisplayPropertiesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceDisplayPropertiesKHRDel> "vkGetPhysicalDeviceDisplayPropertiesKHR"
            static let s_vkGetPhysicalDeviceDisplayPlanePropertiesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceDisplayPlanePropertiesKHRDel> "vkGetPhysicalDeviceDisplayPlanePropertiesKHR"
            static let s_vkGetDisplayPlaneSupportedDisplaysKHRDel = VkRaw.vkImportInstanceDelegate<VkGetDisplayPlaneSupportedDisplaysKHRDel> "vkGetDisplayPlaneSupportedDisplaysKHR"
            static let s_vkGetDisplayModePropertiesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetDisplayModePropertiesKHRDel> "vkGetDisplayModePropertiesKHR"
            static let s_vkCreateDisplayModeKHRDel = VkRaw.vkImportInstanceDelegate<VkCreateDisplayModeKHRDel> "vkCreateDisplayModeKHR"
            static let s_vkGetDisplayPlaneCapabilitiesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetDisplayPlaneCapabilitiesKHRDel> "vkGetDisplayPlaneCapabilitiesKHR"
            static let s_vkCreateDisplayPlaneSurfaceKHRDel = VkRaw.vkImportInstanceDelegate<VkCreateDisplayPlaneSurfaceKHRDel> "vkCreateDisplayPlaneSurfaceKHR"
            static do Report.End(3) |> ignore
            static member vkGetPhysicalDeviceDisplayPropertiesKHR = s_vkGetPhysicalDeviceDisplayPropertiesKHRDel
            static member vkGetPhysicalDeviceDisplayPlanePropertiesKHR = s_vkGetPhysicalDeviceDisplayPlanePropertiesKHRDel
            static member vkGetDisplayPlaneSupportedDisplaysKHR = s_vkGetDisplayPlaneSupportedDisplaysKHRDel
            static member vkGetDisplayModePropertiesKHR = s_vkGetDisplayModePropertiesKHRDel
            static member vkCreateDisplayModeKHR = s_vkCreateDisplayModeKHRDel
            static member vkGetDisplayPlaneCapabilitiesKHR = s_vkGetDisplayPlaneCapabilitiesKHRDel
            static member vkCreateDisplayPlaneSurfaceKHR = s_vkCreateDisplayPlaneSurfaceKHRDel
        let vkGetPhysicalDeviceDisplayPropertiesKHR(physicalDevice : VkPhysicalDevice, pPropertyCount : nativeptr<uint32>, pProperties : nativeptr<VkDisplayPropertiesKHR>) = Loader<unit>.vkGetPhysicalDeviceDisplayPropertiesKHR.Invoke(physicalDevice, pPropertyCount, pProperties)
        let vkGetPhysicalDeviceDisplayPlanePropertiesKHR(physicalDevice : VkPhysicalDevice, pPropertyCount : nativeptr<uint32>, pProperties : nativeptr<VkDisplayPlanePropertiesKHR>) = Loader<unit>.vkGetPhysicalDeviceDisplayPlanePropertiesKHR.Invoke(physicalDevice, pPropertyCount, pProperties)
        let vkGetDisplayPlaneSupportedDisplaysKHR(physicalDevice : VkPhysicalDevice, planeIndex : uint32, pDisplayCount : nativeptr<uint32>, pDisplays : nativeptr<VkDisplayKHR>) = Loader<unit>.vkGetDisplayPlaneSupportedDisplaysKHR.Invoke(physicalDevice, planeIndex, pDisplayCount, pDisplays)
        let vkGetDisplayModePropertiesKHR(physicalDevice : VkPhysicalDevice, display : VkDisplayKHR, pPropertyCount : nativeptr<uint32>, pProperties : nativeptr<VkDisplayModePropertiesKHR>) = Loader<unit>.vkGetDisplayModePropertiesKHR.Invoke(physicalDevice, display, pPropertyCount, pProperties)
        let vkCreateDisplayModeKHR(physicalDevice : VkPhysicalDevice, display : VkDisplayKHR, pCreateInfo : nativeptr<VkDisplayModeCreateInfoKHR>, pAllocator : nativeptr<VkAllocationCallbacks>, pMode : nativeptr<VkDisplayModeKHR>) = Loader<unit>.vkCreateDisplayModeKHR.Invoke(physicalDevice, display, pCreateInfo, pAllocator, pMode)
        let vkGetDisplayPlaneCapabilitiesKHR(physicalDevice : VkPhysicalDevice, mode : VkDisplayModeKHR, planeIndex : uint32, pCapabilities : nativeptr<VkDisplayPlaneCapabilitiesKHR>) = Loader<unit>.vkGetDisplayPlaneCapabilitiesKHR.Invoke(physicalDevice, mode, planeIndex, pCapabilities)
        let vkCreateDisplayPlaneSurfaceKHR(instance : VkInstance, pCreateInfo : nativeptr<VkDisplaySurfaceCreateInfoKHR>, pAllocator : nativeptr<VkAllocationCallbacks>, pSurface : nativeptr<VkSurfaceKHR>) = Loader<unit>.vkCreateDisplayPlaneSurfaceKHR.Invoke(instance, pCreateInfo, pAllocator, pSurface)

module EXTDirectModeDisplay =
    let Name = "VK_EXT_direct_mode_display"
    let Number = 89
    
    let Required = [ KHRDisplay.Name; KHRSurface.Name ]
    open KHRDisplay
    open KHRSurface
    open EXTDebugReport
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkReleaseDisplayEXTDel = delegate of VkPhysicalDevice * VkDisplayKHR -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_EXT_direct_mode_display")
            static let s_vkReleaseDisplayEXTDel = VkRaw.vkImportInstanceDelegate<VkReleaseDisplayEXTDel> "vkReleaseDisplayEXT"
            static do Report.End(3) |> ignore
            static member vkReleaseDisplayEXT = s_vkReleaseDisplayEXTDel
        let vkReleaseDisplayEXT(physicalDevice : VkPhysicalDevice, display : VkDisplayKHR) = Loader<unit>.vkReleaseDisplayEXT.Invoke(physicalDevice, display)

module EXTAcquireXlibDisplay =
    let Name = "VK_EXT_acquire_xlib_display"
    let Number = 90
    
    let Required = [ EXTDirectModeDisplay.Name; KHRDisplay.Name; KHRSurface.Name ]
    open EXTDirectModeDisplay
    open KHRDisplay
    open KHRSurface
    open EXTDebugReport
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkAcquireXlibDisplayEXTDel = delegate of VkPhysicalDevice * nativeptr<nativeint> * VkDisplayKHR -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetRandROutputDisplayEXTDel = delegate of VkPhysicalDevice * nativeptr<nativeint> * nativeint * nativeptr<VkDisplayKHR> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_EXT_acquire_xlib_display")
            static let s_vkAcquireXlibDisplayEXTDel = VkRaw.vkImportInstanceDelegate<VkAcquireXlibDisplayEXTDel> "vkAcquireXlibDisplayEXT"
            static let s_vkGetRandROutputDisplayEXTDel = VkRaw.vkImportInstanceDelegate<VkGetRandROutputDisplayEXTDel> "vkGetRandROutputDisplayEXT"
            static do Report.End(3) |> ignore
            static member vkAcquireXlibDisplayEXT = s_vkAcquireXlibDisplayEXTDel
            static member vkGetRandROutputDisplayEXT = s_vkGetRandROutputDisplayEXTDel
        let vkAcquireXlibDisplayEXT(physicalDevice : VkPhysicalDevice, dpy : nativeptr<nativeint>, display : VkDisplayKHR) = Loader<unit>.vkAcquireXlibDisplayEXT.Invoke(physicalDevice, dpy, display)
        let vkGetRandROutputDisplayEXT(physicalDevice : VkPhysicalDevice, dpy : nativeptr<nativeint>, rrOutput : nativeint, pDisplay : nativeptr<VkDisplayKHR>) = Loader<unit>.vkGetRandROutputDisplayEXT.Invoke(physicalDevice, dpy, rrOutput, pDisplay)

module EXTBlendOperationAdvanced =
    let Name = "VK_EXT_blend_operation_advanced"
    let Number = 149
    
    open EXTDebugReport
    
    type VkBlendOverlapEXT = 
        | VkBlendOverlapUncorrelatedExt = 0
        | VkBlendOverlapDisjointExt = 1
        | VkBlendOverlapConjointExt = 2
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceBlendOperationAdvancedFeaturesEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public advancedBlendCoherentOperations : VkBool32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , advancedBlendCoherentOperations : VkBool32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    advancedBlendCoherentOperations = advancedBlendCoherentOperations
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "advancedBlendCoherentOperations = %A" x.advancedBlendCoherentOperations
                ] |> sprintf "VkPhysicalDeviceBlendOperationAdvancedFeaturesEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceBlendOperationAdvancedPropertiesEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public advancedBlendMaxColorAttachments : uint32
            val mutable public advancedBlendIndependentBlend : VkBool32
            val mutable public advancedBlendNonPremultipliedSrcColor : VkBool32
            val mutable public advancedBlendNonPremultipliedDstColor : VkBool32
            val mutable public advancedBlendCorrelatedOverlap : VkBool32
            val mutable public advancedBlendAllOperations : VkBool32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , advancedBlendMaxColorAttachments : uint32
              , advancedBlendIndependentBlend : VkBool32
              , advancedBlendNonPremultipliedSrcColor : VkBool32
              , advancedBlendNonPremultipliedDstColor : VkBool32
              , advancedBlendCorrelatedOverlap : VkBool32
              , advancedBlendAllOperations : VkBool32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    advancedBlendMaxColorAttachments = advancedBlendMaxColorAttachments
                    advancedBlendIndependentBlend = advancedBlendIndependentBlend
                    advancedBlendNonPremultipliedSrcColor = advancedBlendNonPremultipliedSrcColor
                    advancedBlendNonPremultipliedDstColor = advancedBlendNonPremultipliedDstColor
                    advancedBlendCorrelatedOverlap = advancedBlendCorrelatedOverlap
                    advancedBlendAllOperations = advancedBlendAllOperations
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "advancedBlendMaxColorAttachments = %A" x.advancedBlendMaxColorAttachments
                    sprintf "advancedBlendIndependentBlend = %A" x.advancedBlendIndependentBlend
                    sprintf "advancedBlendNonPremultipliedSrcColor = %A" x.advancedBlendNonPremultipliedSrcColor
                    sprintf "advancedBlendNonPremultipliedDstColor = %A" x.advancedBlendNonPremultipliedDstColor
                    sprintf "advancedBlendCorrelatedOverlap = %A" x.advancedBlendCorrelatedOverlap
                    sprintf "advancedBlendAllOperations = %A" x.advancedBlendAllOperations
                ] |> sprintf "VkPhysicalDeviceBlendOperationAdvancedPropertiesEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPipelineColorBlendAdvancedStateCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public srcPremultiplied : VkBool32
            val mutable public dstPremultiplied : VkBool32
            val mutable public blendOverlap : VkBlendOverlapEXT
    
            new(sType : VkStructureType
              , pNext : nativeint
              , srcPremultiplied : VkBool32
              , dstPremultiplied : VkBool32
              , blendOverlap : VkBlendOverlapEXT
              ) =
                {
                    sType = sType
                    pNext = pNext
                    srcPremultiplied = srcPremultiplied
                    dstPremultiplied = dstPremultiplied
                    blendOverlap = blendOverlap
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "srcPremultiplied = %A" x.srcPremultiplied
                    sprintf "dstPremultiplied = %A" x.dstPremultiplied
                    sprintf "blendOverlap = %A" x.blendOverlap
                ] |> sprintf "VkPipelineColorBlendAdvancedStateCreateInfoEXT { %s }"
        end
    
    
    type VkAccessFlags with
         static member inline ColorAttachmentReadNoncoherentBitExt = unbox<VkAccessFlags> 524288
    type VkBlendOp with
         static member inline ZeroExt = unbox<VkBlendOp> 1000148000
         static member inline SrcExt = unbox<VkBlendOp> 1000148001
         static member inline DstExt = unbox<VkBlendOp> 1000148002
         static member inline SrcOverExt = unbox<VkBlendOp> 1000148003
         static member inline DstOverExt = unbox<VkBlendOp> 1000148004
         static member inline SrcInExt = unbox<VkBlendOp> 1000148005
         static member inline DstInExt = unbox<VkBlendOp> 1000148006
         static member inline SrcOutExt = unbox<VkBlendOp> 1000148007
         static member inline DstOutExt = unbox<VkBlendOp> 1000148008
         static member inline SrcAtopExt = unbox<VkBlendOp> 1000148009
         static member inline DstAtopExt = unbox<VkBlendOp> 1000148010
         static member inline XorExt = unbox<VkBlendOp> 1000148011
         static member inline MultiplyExt = unbox<VkBlendOp> 1000148012
         static member inline ScreenExt = unbox<VkBlendOp> 1000148013
         static member inline OverlayExt = unbox<VkBlendOp> 1000148014
         static member inline DarkenExt = unbox<VkBlendOp> 1000148015
         static member inline LightenExt = unbox<VkBlendOp> 1000148016
         static member inline ColordodgeExt = unbox<VkBlendOp> 1000148017
         static member inline ColorburnExt = unbox<VkBlendOp> 1000148018
         static member inline HardlightExt = unbox<VkBlendOp> 1000148019
         static member inline SoftlightExt = unbox<VkBlendOp> 1000148020
         static member inline DifferenceExt = unbox<VkBlendOp> 1000148021
         static member inline ExclusionExt = unbox<VkBlendOp> 1000148022
         static member inline InvertExt = unbox<VkBlendOp> 1000148023
         static member inline InvertRgbExt = unbox<VkBlendOp> 1000148024
         static member inline LineardodgeExt = unbox<VkBlendOp> 1000148025
         static member inline LinearburnExt = unbox<VkBlendOp> 1000148026
         static member inline VividlightExt = unbox<VkBlendOp> 1000148027
         static member inline LinearlightExt = unbox<VkBlendOp> 1000148028
         static member inline PinlightExt = unbox<VkBlendOp> 1000148029
         static member inline HardmixExt = unbox<VkBlendOp> 1000148030
         static member inline HslHueExt = unbox<VkBlendOp> 1000148031
         static member inline HslSaturationExt = unbox<VkBlendOp> 1000148032
         static member inline HslColorExt = unbox<VkBlendOp> 1000148033
         static member inline HslLuminosityExt = unbox<VkBlendOp> 1000148034
         static member inline PlusExt = unbox<VkBlendOp> 1000148035
         static member inline PlusClampedExt = unbox<VkBlendOp> 1000148036
         static member inline PlusClampedAlphaExt = unbox<VkBlendOp> 1000148037
         static member inline PlusDarkerExt = unbox<VkBlendOp> 1000148038
         static member inline MinusExt = unbox<VkBlendOp> 1000148039
         static member inline MinusClampedExt = unbox<VkBlendOp> 1000148040
         static member inline ContrastExt = unbox<VkBlendOp> 1000148041
         static member inline InvertOvgExt = unbox<VkBlendOp> 1000148042
         static member inline RedExt = unbox<VkBlendOp> 1000148043
         static member inline GreenExt = unbox<VkBlendOp> 1000148044
         static member inline BlueExt = unbox<VkBlendOp> 1000148045
    type VkStructureType with
         static member inline PhysicalDeviceBlendOperationAdvancedFeaturesExt = unbox<VkStructureType> 1000148000
         static member inline PhysicalDeviceBlendOperationAdvancedPropertiesExt = unbox<VkStructureType> 1000148001
         static member inline PipelineColorBlendAdvancedStateCreateInfoExt = unbox<VkStructureType> 1000148002
    

module EXTConservativeRasterization =
    let Name = "VK_EXT_conservative_rasterization"
    let Number = 102
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name ]
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    type VkConservativeRasterizationModeEXT = 
        | VkConservativeRasterizationModeDisabledExt = 0
        | VkConservativeRasterizationModeOverestimateExt = 1
        | VkConservativeRasterizationModeUnderestimateExt = 2
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceConservativeRasterizationPropertiesEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public primitiveOverestimationSize : float32
            val mutable public maxExtraPrimitiveOverestimationSize : float32
            val mutable public extraPrimitiveOverestimationSizeGranularity : float32
            val mutable public primitiveUnderestimation : VkBool32
            val mutable public conservativePointAndLineRasterization : VkBool32
            val mutable public degenerateTrianglesRasterized : VkBool32
            val mutable public degenerateLinesRasterized : VkBool32
            val mutable public fullyCoveredFragmentShaderInputVariable : VkBool32
            val mutable public conservativeRasterizationPostDepthCoverage : VkBool32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , primitiveOverestimationSize : float32
              , maxExtraPrimitiveOverestimationSize : float32
              , extraPrimitiveOverestimationSizeGranularity : float32
              , primitiveUnderestimation : VkBool32
              , conservativePointAndLineRasterization : VkBool32
              , degenerateTrianglesRasterized : VkBool32
              , degenerateLinesRasterized : VkBool32
              , fullyCoveredFragmentShaderInputVariable : VkBool32
              , conservativeRasterizationPostDepthCoverage : VkBool32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    primitiveOverestimationSize = primitiveOverestimationSize
                    maxExtraPrimitiveOverestimationSize = maxExtraPrimitiveOverestimationSize
                    extraPrimitiveOverestimationSizeGranularity = extraPrimitiveOverestimationSizeGranularity
                    primitiveUnderestimation = primitiveUnderestimation
                    conservativePointAndLineRasterization = conservativePointAndLineRasterization
                    degenerateTrianglesRasterized = degenerateTrianglesRasterized
                    degenerateLinesRasterized = degenerateLinesRasterized
                    fullyCoveredFragmentShaderInputVariable = fullyCoveredFragmentShaderInputVariable
                    conservativeRasterizationPostDepthCoverage = conservativeRasterizationPostDepthCoverage
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "primitiveOverestimationSize = %A" x.primitiveOverestimationSize
                    sprintf "maxExtraPrimitiveOverestimationSize = %A" x.maxExtraPrimitiveOverestimationSize
                    sprintf "extraPrimitiveOverestimationSizeGranularity = %A" x.extraPrimitiveOverestimationSizeGranularity
                    sprintf "primitiveUnderestimation = %A" x.primitiveUnderestimation
                    sprintf "conservativePointAndLineRasterization = %A" x.conservativePointAndLineRasterization
                    sprintf "degenerateTrianglesRasterized = %A" x.degenerateTrianglesRasterized
                    sprintf "degenerateLinesRasterized = %A" x.degenerateLinesRasterized
                    sprintf "fullyCoveredFragmentShaderInputVariable = %A" x.fullyCoveredFragmentShaderInputVariable
                    sprintf "conservativeRasterizationPostDepthCoverage = %A" x.conservativeRasterizationPostDepthCoverage
                ] |> sprintf "VkPhysicalDeviceConservativeRasterizationPropertiesEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPipelineRasterizationConservativeStateCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkPipelineRasterizationConservativeStateCreateFlagsEXT
            val mutable public conservativeRasterizationMode : VkConservativeRasterizationModeEXT
            val mutable public extraPrimitiveOverestimationSize : float32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkPipelineRasterizationConservativeStateCreateFlagsEXT
              , conservativeRasterizationMode : VkConservativeRasterizationModeEXT
              , extraPrimitiveOverestimationSize : float32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    conservativeRasterizationMode = conservativeRasterizationMode
                    extraPrimitiveOverestimationSize = extraPrimitiveOverestimationSize
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "conservativeRasterizationMode = %A" x.conservativeRasterizationMode
                    sprintf "extraPrimitiveOverestimationSize = %A" x.extraPrimitiveOverestimationSize
                ] |> sprintf "VkPipelineRasterizationConservativeStateCreateInfoEXT { %s }"
        end
    
    
    type VkStructureType with
         static member inline PhysicalDeviceConservativeRasterizationPropertiesExt = unbox<VkStructureType> 1000101000
         static member inline PipelineRasterizationConservativeStateCreateInfoExt = unbox<VkStructureType> 1000101001
    

module EXTDebugMarker =
    let Name = "VK_EXT_debug_marker"
    let Number = 23
    
    let Required = [ EXTDebugReport.Name ]
    open EXTDebugReport
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDebugMarkerMarkerInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public pMarkerName : cstr
            val mutable public color : V4f
    
            new(sType : VkStructureType
              , pNext : nativeint
              , pMarkerName : cstr
              , color : V4f
              ) =
                {
                    sType = sType
                    pNext = pNext
                    pMarkerName = pMarkerName
                    color = color
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "pMarkerName = %A" x.pMarkerName
                    sprintf "color = %A" x.color
                ] |> sprintf "VkDebugMarkerMarkerInfoEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDebugMarkerObjectNameInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public objectType : VkDebugReportObjectTypeEXT
            val mutable public _object : uint64
            val mutable public pObjectName : cstr
    
            new(sType : VkStructureType
              , pNext : nativeint
              , objectType : VkDebugReportObjectTypeEXT
              , _object : uint64
              , pObjectName : cstr
              ) =
                {
                    sType = sType
                    pNext = pNext
                    objectType = objectType
                    _object = _object
                    pObjectName = pObjectName
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "objectType = %A" x.objectType
                    sprintf "_object = %A" x._object
                    sprintf "pObjectName = %A" x.pObjectName
                ] |> sprintf "VkDebugMarkerObjectNameInfoEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDebugMarkerObjectTagInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public objectType : VkDebugReportObjectTypeEXT
            val mutable public _object : uint64
            val mutable public tagName : uint64
            val mutable public tagSize : uint64
            val mutable public pTag : nativeint
    
            new(sType : VkStructureType
              , pNext : nativeint
              , objectType : VkDebugReportObjectTypeEXT
              , _object : uint64
              , tagName : uint64
              , tagSize : uint64
              , pTag : nativeint
              ) =
                {
                    sType = sType
                    pNext = pNext
                    objectType = objectType
                    _object = _object
                    tagName = tagName
                    tagSize = tagSize
                    pTag = pTag
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "objectType = %A" x.objectType
                    sprintf "_object = %A" x._object
                    sprintf "tagName = %A" x.tagName
                    sprintf "tagSize = %A" x.tagSize
                    sprintf "pTag = %A" x.pTag
                ] |> sprintf "VkDebugMarkerObjectTagInfoEXT { %s }"
        end
    
    
    type VkStructureType with
         static member inline DebugMarkerObjectNameInfoExt = unbox<VkStructureType> 1000022000
         static member inline DebugMarkerObjectTagInfoExt = unbox<VkStructureType> 1000022001
         static member inline DebugMarkerMarkerInfoExt = unbox<VkStructureType> 1000022002
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkDebugMarkerSetObjectTagEXTDel = delegate of VkDevice * nativeptr<VkDebugMarkerObjectTagInfoEXT> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkDebugMarkerSetObjectNameEXTDel = delegate of VkDevice * nativeptr<VkDebugMarkerObjectNameInfoEXT> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdDebugMarkerBeginEXTDel = delegate of VkCommandBuffer * nativeptr<VkDebugMarkerMarkerInfoEXT> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdDebugMarkerEndEXTDel = delegate of VkCommandBuffer -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdDebugMarkerInsertEXTDel = delegate of VkCommandBuffer * nativeptr<VkDebugMarkerMarkerInfoEXT> -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_EXT_debug_marker")
            static let s_vkDebugMarkerSetObjectTagEXTDel = VkRaw.vkImportInstanceDelegate<VkDebugMarkerSetObjectTagEXTDel> "vkDebugMarkerSetObjectTagEXT"
            static let s_vkDebugMarkerSetObjectNameEXTDel = VkRaw.vkImportInstanceDelegate<VkDebugMarkerSetObjectNameEXTDel> "vkDebugMarkerSetObjectNameEXT"
            static let s_vkCmdDebugMarkerBeginEXTDel = VkRaw.vkImportInstanceDelegate<VkCmdDebugMarkerBeginEXTDel> "vkCmdDebugMarkerBeginEXT"
            static let s_vkCmdDebugMarkerEndEXTDel = VkRaw.vkImportInstanceDelegate<VkCmdDebugMarkerEndEXTDel> "vkCmdDebugMarkerEndEXT"
            static let s_vkCmdDebugMarkerInsertEXTDel = VkRaw.vkImportInstanceDelegate<VkCmdDebugMarkerInsertEXTDel> "vkCmdDebugMarkerInsertEXT"
            static do Report.End(3) |> ignore
            static member vkDebugMarkerSetObjectTagEXT = s_vkDebugMarkerSetObjectTagEXTDel
            static member vkDebugMarkerSetObjectNameEXT = s_vkDebugMarkerSetObjectNameEXTDel
            static member vkCmdDebugMarkerBeginEXT = s_vkCmdDebugMarkerBeginEXTDel
            static member vkCmdDebugMarkerEndEXT = s_vkCmdDebugMarkerEndEXTDel
            static member vkCmdDebugMarkerInsertEXT = s_vkCmdDebugMarkerInsertEXTDel
        let vkDebugMarkerSetObjectTagEXT(device : VkDevice, pTagInfo : nativeptr<VkDebugMarkerObjectTagInfoEXT>) = Loader<unit>.vkDebugMarkerSetObjectTagEXT.Invoke(device, pTagInfo)
        let vkDebugMarkerSetObjectNameEXT(device : VkDevice, pNameInfo : nativeptr<VkDebugMarkerObjectNameInfoEXT>) = Loader<unit>.vkDebugMarkerSetObjectNameEXT.Invoke(device, pNameInfo)
        let vkCmdDebugMarkerBeginEXT(commandBuffer : VkCommandBuffer, pMarkerInfo : nativeptr<VkDebugMarkerMarkerInfoEXT>) = Loader<unit>.vkCmdDebugMarkerBeginEXT.Invoke(commandBuffer, pMarkerInfo)
        let vkCmdDebugMarkerEndEXT(commandBuffer : VkCommandBuffer) = Loader<unit>.vkCmdDebugMarkerEndEXT.Invoke(commandBuffer)
        let vkCmdDebugMarkerInsertEXT(commandBuffer : VkCommandBuffer, pMarkerInfo : nativeptr<VkDebugMarkerMarkerInfoEXT>) = Loader<unit>.vkCmdDebugMarkerInsertEXT.Invoke(commandBuffer, pMarkerInfo)

module EXTDebugUtils =
    let Name = "VK_EXT_debug_utils"
    let Number = 129
    
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDebugUtilsLabelEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public pLabelName : cstr
            val mutable public color : V4f
    
            new(sType : VkStructureType
              , pNext : nativeint
              , pLabelName : cstr
              , color : V4f
              ) =
                {
                    sType = sType
                    pNext = pNext
                    pLabelName = pLabelName
                    color = color
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "pLabelName = %A" x.pLabelName
                    sprintf "color = %A" x.color
                ] |> sprintf "VkDebugUtilsLabelEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDebugUtilsObjectNameInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public objectType : VkObjectType
            val mutable public objectHandle : uint64
            val mutable public pObjectName : cstr
    
            new(sType : VkStructureType
              , pNext : nativeint
              , objectType : VkObjectType
              , objectHandle : uint64
              , pObjectName : cstr
              ) =
                {
                    sType = sType
                    pNext = pNext
                    objectType = objectType
                    objectHandle = objectHandle
                    pObjectName = pObjectName
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "objectType = %A" x.objectType
                    sprintf "objectHandle = %A" x.objectHandle
                    sprintf "pObjectName = %A" x.pObjectName
                ] |> sprintf "VkDebugUtilsObjectNameInfoEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDebugUtilsMessengerCallbackDataEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkDebugUtilsMessengerCallbackDataFlagsEXT
            val mutable public pMessageIdName : cstr
            val mutable public messageIdNumber : int
            val mutable public pMessage : cstr
            val mutable public queueLabelCount : uint32
            val mutable public pQueueLabels : nativeptr<VkDebugUtilsLabelEXT>
            val mutable public cmdBufLabelCount : uint32
            val mutable public pCmdBufLabels : nativeptr<VkDebugUtilsLabelEXT>
            val mutable public objectCount : uint32
            val mutable public pObjects : nativeptr<VkDebugUtilsObjectNameInfoEXT>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkDebugUtilsMessengerCallbackDataFlagsEXT
              , pMessageIdName : cstr
              , messageIdNumber : int
              , pMessage : cstr
              , queueLabelCount : uint32
              , pQueueLabels : nativeptr<VkDebugUtilsLabelEXT>
              , cmdBufLabelCount : uint32
              , pCmdBufLabels : nativeptr<VkDebugUtilsLabelEXT>
              , objectCount : uint32
              , pObjects : nativeptr<VkDebugUtilsObjectNameInfoEXT>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    pMessageIdName = pMessageIdName
                    messageIdNumber = messageIdNumber
                    pMessage = pMessage
                    queueLabelCount = queueLabelCount
                    pQueueLabels = pQueueLabels
                    cmdBufLabelCount = cmdBufLabelCount
                    pCmdBufLabels = pCmdBufLabels
                    objectCount = objectCount
                    pObjects = pObjects
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "pMessageIdName = %A" x.pMessageIdName
                    sprintf "messageIdNumber = %A" x.messageIdNumber
                    sprintf "pMessage = %A" x.pMessage
                    sprintf "queueLabelCount = %A" x.queueLabelCount
                    sprintf "pQueueLabels = %A" x.pQueueLabels
                    sprintf "cmdBufLabelCount = %A" x.cmdBufLabelCount
                    sprintf "pCmdBufLabels = %A" x.pCmdBufLabels
                    sprintf "objectCount = %A" x.objectCount
                    sprintf "pObjects = %A" x.pObjects
                ] |> sprintf "VkDebugUtilsMessengerCallbackDataEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDebugUtilsMessengerCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkDebugUtilsMessengerCreateFlagsEXT
            val mutable public messageSeverity : VkDebugUtilsMessageSeverityFlagsEXT
            val mutable public messageType : VkDebugUtilsMessageTypeFlagsEXT
            val mutable public pfnUserCallback : PFN_vkDebugUtilsMessengerCallbackEXT
            val mutable public pUserData : nativeint
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkDebugUtilsMessengerCreateFlagsEXT
              , messageSeverity : VkDebugUtilsMessageSeverityFlagsEXT
              , messageType : VkDebugUtilsMessageTypeFlagsEXT
              , pfnUserCallback : PFN_vkDebugUtilsMessengerCallbackEXT
              , pUserData : nativeint
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    messageSeverity = messageSeverity
                    messageType = messageType
                    pfnUserCallback = pfnUserCallback
                    pUserData = pUserData
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "messageSeverity = %A" x.messageSeverity
                    sprintf "messageType = %A" x.messageType
                    sprintf "pfnUserCallback = %A" x.pfnUserCallback
                    sprintf "pUserData = %A" x.pUserData
                ] |> sprintf "VkDebugUtilsMessengerCreateInfoEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDebugUtilsObjectTagInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public objectType : VkObjectType
            val mutable public objectHandle : uint64
            val mutable public tagName : uint64
            val mutable public tagSize : uint64
            val mutable public pTag : nativeint
    
            new(sType : VkStructureType
              , pNext : nativeint
              , objectType : VkObjectType
              , objectHandle : uint64
              , tagName : uint64
              , tagSize : uint64
              , pTag : nativeint
              ) =
                {
                    sType = sType
                    pNext = pNext
                    objectType = objectType
                    objectHandle = objectHandle
                    tagName = tagName
                    tagSize = tagSize
                    pTag = pTag
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "objectType = %A" x.objectType
                    sprintf "objectHandle = %A" x.objectHandle
                    sprintf "tagName = %A" x.tagName
                    sprintf "tagSize = %A" x.tagSize
                    sprintf "pTag = %A" x.pTag
                ] |> sprintf "VkDebugUtilsObjectTagInfoEXT { %s }"
        end
    
    
    type VkObjectType with
         static member inline DebugUtilsMessengerExt = unbox<VkObjectType> 1000128000
    type VkStructureType with
         static member inline DebugUtilsObjectNameInfoExt = unbox<VkStructureType> 1000128000
         static member inline DebugUtilsObjectTagInfoExt = unbox<VkStructureType> 1000128001
         static member inline DebugUtilsLabelExt = unbox<VkStructureType> 1000128002
         static member inline DebugUtilsMessengerCallbackDataExt = unbox<VkStructureType> 1000128003
         static member inline DebugUtilsMessengerCreateInfoExt = unbox<VkStructureType> 1000128004
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkSetDebugUtilsObjectNameEXTDel = delegate of VkDevice * nativeptr<VkDebugUtilsObjectNameInfoEXT> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkSetDebugUtilsObjectTagEXTDel = delegate of VkDevice * nativeptr<VkDebugUtilsObjectTagInfoEXT> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkQueueBeginDebugUtilsLabelEXTDel = delegate of VkQueue * nativeptr<VkDebugUtilsLabelEXT> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkQueueEndDebugUtilsLabelEXTDel = delegate of VkQueue -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkQueueInsertDebugUtilsLabelEXTDel = delegate of VkQueue * nativeptr<VkDebugUtilsLabelEXT> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdBeginDebugUtilsLabelEXTDel = delegate of VkCommandBuffer * nativeptr<VkDebugUtilsLabelEXT> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdEndDebugUtilsLabelEXTDel = delegate of VkCommandBuffer -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdInsertDebugUtilsLabelEXTDel = delegate of VkCommandBuffer * nativeptr<VkDebugUtilsLabelEXT> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateDebugUtilsMessengerEXTDel = delegate of VkInstance * nativeptr<VkDebugUtilsMessengerCreateInfoEXT> * nativeptr<VkAllocationCallbacks> * nativeptr<VkDebugUtilsMessengerEXT> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkDestroyDebugUtilsMessengerEXTDel = delegate of VkInstance * VkDebugUtilsMessengerEXT * nativeptr<VkAllocationCallbacks> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkSubmitDebugUtilsMessageEXTDel = delegate of VkInstance * VkDebugUtilsMessageSeverityFlagsEXT * VkDebugUtilsMessageTypeFlagsEXT * nativeptr<VkDebugUtilsMessengerCallbackDataEXT> -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_EXT_debug_utils")
            static let s_vkSetDebugUtilsObjectNameEXTDel = VkRaw.vkImportInstanceDelegate<VkSetDebugUtilsObjectNameEXTDel> "vkSetDebugUtilsObjectNameEXT"
            static let s_vkSetDebugUtilsObjectTagEXTDel = VkRaw.vkImportInstanceDelegate<VkSetDebugUtilsObjectTagEXTDel> "vkSetDebugUtilsObjectTagEXT"
            static let s_vkQueueBeginDebugUtilsLabelEXTDel = VkRaw.vkImportInstanceDelegate<VkQueueBeginDebugUtilsLabelEXTDel> "vkQueueBeginDebugUtilsLabelEXT"
            static let s_vkQueueEndDebugUtilsLabelEXTDel = VkRaw.vkImportInstanceDelegate<VkQueueEndDebugUtilsLabelEXTDel> "vkQueueEndDebugUtilsLabelEXT"
            static let s_vkQueueInsertDebugUtilsLabelEXTDel = VkRaw.vkImportInstanceDelegate<VkQueueInsertDebugUtilsLabelEXTDel> "vkQueueInsertDebugUtilsLabelEXT"
            static let s_vkCmdBeginDebugUtilsLabelEXTDel = VkRaw.vkImportInstanceDelegate<VkCmdBeginDebugUtilsLabelEXTDel> "vkCmdBeginDebugUtilsLabelEXT"
            static let s_vkCmdEndDebugUtilsLabelEXTDel = VkRaw.vkImportInstanceDelegate<VkCmdEndDebugUtilsLabelEXTDel> "vkCmdEndDebugUtilsLabelEXT"
            static let s_vkCmdInsertDebugUtilsLabelEXTDel = VkRaw.vkImportInstanceDelegate<VkCmdInsertDebugUtilsLabelEXTDel> "vkCmdInsertDebugUtilsLabelEXT"
            static let s_vkCreateDebugUtilsMessengerEXTDel = VkRaw.vkImportInstanceDelegate<VkCreateDebugUtilsMessengerEXTDel> "vkCreateDebugUtilsMessengerEXT"
            static let s_vkDestroyDebugUtilsMessengerEXTDel = VkRaw.vkImportInstanceDelegate<VkDestroyDebugUtilsMessengerEXTDel> "vkDestroyDebugUtilsMessengerEXT"
            static let s_vkSubmitDebugUtilsMessageEXTDel = VkRaw.vkImportInstanceDelegate<VkSubmitDebugUtilsMessageEXTDel> "vkSubmitDebugUtilsMessageEXT"
            static do Report.End(3) |> ignore
            static member vkSetDebugUtilsObjectNameEXT = s_vkSetDebugUtilsObjectNameEXTDel
            static member vkSetDebugUtilsObjectTagEXT = s_vkSetDebugUtilsObjectTagEXTDel
            static member vkQueueBeginDebugUtilsLabelEXT = s_vkQueueBeginDebugUtilsLabelEXTDel
            static member vkQueueEndDebugUtilsLabelEXT = s_vkQueueEndDebugUtilsLabelEXTDel
            static member vkQueueInsertDebugUtilsLabelEXT = s_vkQueueInsertDebugUtilsLabelEXTDel
            static member vkCmdBeginDebugUtilsLabelEXT = s_vkCmdBeginDebugUtilsLabelEXTDel
            static member vkCmdEndDebugUtilsLabelEXT = s_vkCmdEndDebugUtilsLabelEXTDel
            static member vkCmdInsertDebugUtilsLabelEXT = s_vkCmdInsertDebugUtilsLabelEXTDel
            static member vkCreateDebugUtilsMessengerEXT = s_vkCreateDebugUtilsMessengerEXTDel
            static member vkDestroyDebugUtilsMessengerEXT = s_vkDestroyDebugUtilsMessengerEXTDel
            static member vkSubmitDebugUtilsMessageEXT = s_vkSubmitDebugUtilsMessageEXTDel
        let vkSetDebugUtilsObjectNameEXT(device : VkDevice, pNameInfo : nativeptr<VkDebugUtilsObjectNameInfoEXT>) = Loader<unit>.vkSetDebugUtilsObjectNameEXT.Invoke(device, pNameInfo)
        let vkSetDebugUtilsObjectTagEXT(device : VkDevice, pTagInfo : nativeptr<VkDebugUtilsObjectTagInfoEXT>) = Loader<unit>.vkSetDebugUtilsObjectTagEXT.Invoke(device, pTagInfo)
        let vkQueueBeginDebugUtilsLabelEXT(queue : VkQueue, pLabelInfo : nativeptr<VkDebugUtilsLabelEXT>) = Loader<unit>.vkQueueBeginDebugUtilsLabelEXT.Invoke(queue, pLabelInfo)
        let vkQueueEndDebugUtilsLabelEXT(queue : VkQueue) = Loader<unit>.vkQueueEndDebugUtilsLabelEXT.Invoke(queue)
        let vkQueueInsertDebugUtilsLabelEXT(queue : VkQueue, pLabelInfo : nativeptr<VkDebugUtilsLabelEXT>) = Loader<unit>.vkQueueInsertDebugUtilsLabelEXT.Invoke(queue, pLabelInfo)
        let vkCmdBeginDebugUtilsLabelEXT(commandBuffer : VkCommandBuffer, pLabelInfo : nativeptr<VkDebugUtilsLabelEXT>) = Loader<unit>.vkCmdBeginDebugUtilsLabelEXT.Invoke(commandBuffer, pLabelInfo)
        let vkCmdEndDebugUtilsLabelEXT(commandBuffer : VkCommandBuffer) = Loader<unit>.vkCmdEndDebugUtilsLabelEXT.Invoke(commandBuffer)
        let vkCmdInsertDebugUtilsLabelEXT(commandBuffer : VkCommandBuffer, pLabelInfo : nativeptr<VkDebugUtilsLabelEXT>) = Loader<unit>.vkCmdInsertDebugUtilsLabelEXT.Invoke(commandBuffer, pLabelInfo)
        let vkCreateDebugUtilsMessengerEXT(instance : VkInstance, pCreateInfo : nativeptr<VkDebugUtilsMessengerCreateInfoEXT>, pAllocator : nativeptr<VkAllocationCallbacks>, pMessenger : nativeptr<VkDebugUtilsMessengerEXT>) = Loader<unit>.vkCreateDebugUtilsMessengerEXT.Invoke(instance, pCreateInfo, pAllocator, pMessenger)
        let vkDestroyDebugUtilsMessengerEXT(instance : VkInstance, messenger : VkDebugUtilsMessengerEXT, pAllocator : nativeptr<VkAllocationCallbacks>) = Loader<unit>.vkDestroyDebugUtilsMessengerEXT.Invoke(instance, messenger, pAllocator)
        let vkSubmitDebugUtilsMessageEXT(instance : VkInstance, messageSeverity : VkDebugUtilsMessageSeverityFlagsEXT, messageTypes : VkDebugUtilsMessageTypeFlagsEXT, pCallbackData : nativeptr<VkDebugUtilsMessengerCallbackDataEXT>) = Loader<unit>.vkSubmitDebugUtilsMessageEXT.Invoke(instance, messageSeverity, messageTypes, pCallbackData)

module EXTDepthRangeUnrestricted =
    let Name = "VK_EXT_depth_range_unrestricted"
    let Number = 14
    
    open EXTDebugReport
    
    
    
    

module KHRMaintenance3 =
    let Name = "VK_KHR_maintenance3"
    let Number = 169
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name ]
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    type VkDescriptorSetLayoutSupportKHR = VkDescriptorSetLayoutSupport
    type VkPhysicalDeviceMaintenance3PropertiesKHR = VkPhysicalDeviceMaintenance3Properties
    
    

module EXTDescriptorIndexing =
    let Name = "VK_EXT_descriptor_indexing"
    let Number = 162
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name; KHRMaintenance3.Name ]
    open KHRGetPhysicalDeviceProperties2
    open KHRMaintenance3
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDescriptorSetLayoutBindingFlagsCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public bindingCount : uint32
            val mutable public pBindingFlags : nativeptr<VkDescriptorBindingFlagsEXT>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , bindingCount : uint32
              , pBindingFlags : nativeptr<VkDescriptorBindingFlagsEXT>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    bindingCount = bindingCount
                    pBindingFlags = pBindingFlags
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "bindingCount = %A" x.bindingCount
                    sprintf "pBindingFlags = %A" x.pBindingFlags
                ] |> sprintf "VkDescriptorSetLayoutBindingFlagsCreateInfoEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDescriptorSetVariableDescriptorCountAllocateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public descriptorSetCount : uint32
            val mutable public pDescriptorCounts : nativeptr<uint32>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , descriptorSetCount : uint32
              , pDescriptorCounts : nativeptr<uint32>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    descriptorSetCount = descriptorSetCount
                    pDescriptorCounts = pDescriptorCounts
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "descriptorSetCount = %A" x.descriptorSetCount
                    sprintf "pDescriptorCounts = %A" x.pDescriptorCounts
                ] |> sprintf "VkDescriptorSetVariableDescriptorCountAllocateInfoEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDescriptorSetVariableDescriptorCountLayoutSupportEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public maxVariableDescriptorCount : uint32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , maxVariableDescriptorCount : uint32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    maxVariableDescriptorCount = maxVariableDescriptorCount
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "maxVariableDescriptorCount = %A" x.maxVariableDescriptorCount
                ] |> sprintf "VkDescriptorSetVariableDescriptorCountLayoutSupportEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceDescriptorIndexingFeaturesEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public shaderInputAttachmentArrayDynamicIndexing : VkBool32
            val mutable public shaderUniformTexelBufferArrayDynamicIndexing : VkBool32
            val mutable public shaderStorageTexelBufferArrayDynamicIndexing : VkBool32
            val mutable public shaderUniformBufferArrayNonUniformIndexing : VkBool32
            val mutable public shaderSampledImageArrayNonUniformIndexing : VkBool32
            val mutable public shaderStorageBufferArrayNonUniformIndexing : VkBool32
            val mutable public shaderStorageImageArrayNonUniformIndexing : VkBool32
            val mutable public shaderInputAttachmentArrayNonUniformIndexing : VkBool32
            val mutable public shaderUniformTexelBufferArrayNonUniformIndexing : VkBool32
            val mutable public shaderStorageTexelBufferArrayNonUniformIndexing : VkBool32
            val mutable public descriptorBindingUniformBufferUpdateAfterBind : VkBool32
            val mutable public descriptorBindingSampledImageUpdateAfterBind : VkBool32
            val mutable public descriptorBindingStorageImageUpdateAfterBind : VkBool32
            val mutable public descriptorBindingStorageBufferUpdateAfterBind : VkBool32
            val mutable public descriptorBindingUniformTexelBufferUpdateAfterBind : VkBool32
            val mutable public descriptorBindingStorageTexelBufferUpdateAfterBind : VkBool32
            val mutable public descriptorBindingUpdateUnusedWhilePending : VkBool32
            val mutable public descriptorBindingPartiallyBound : VkBool32
            val mutable public descriptorBindingVariableDescriptorCount : VkBool32
            val mutable public runtimeDescriptorArray : VkBool32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , shaderInputAttachmentArrayDynamicIndexing : VkBool32
              , shaderUniformTexelBufferArrayDynamicIndexing : VkBool32
              , shaderStorageTexelBufferArrayDynamicIndexing : VkBool32
              , shaderUniformBufferArrayNonUniformIndexing : VkBool32
              , shaderSampledImageArrayNonUniformIndexing : VkBool32
              , shaderStorageBufferArrayNonUniformIndexing : VkBool32
              , shaderStorageImageArrayNonUniformIndexing : VkBool32
              , shaderInputAttachmentArrayNonUniformIndexing : VkBool32
              , shaderUniformTexelBufferArrayNonUniformIndexing : VkBool32
              , shaderStorageTexelBufferArrayNonUniformIndexing : VkBool32
              , descriptorBindingUniformBufferUpdateAfterBind : VkBool32
              , descriptorBindingSampledImageUpdateAfterBind : VkBool32
              , descriptorBindingStorageImageUpdateAfterBind : VkBool32
              , descriptorBindingStorageBufferUpdateAfterBind : VkBool32
              , descriptorBindingUniformTexelBufferUpdateAfterBind : VkBool32
              , descriptorBindingStorageTexelBufferUpdateAfterBind : VkBool32
              , descriptorBindingUpdateUnusedWhilePending : VkBool32
              , descriptorBindingPartiallyBound : VkBool32
              , descriptorBindingVariableDescriptorCount : VkBool32
              , runtimeDescriptorArray : VkBool32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    shaderInputAttachmentArrayDynamicIndexing = shaderInputAttachmentArrayDynamicIndexing
                    shaderUniformTexelBufferArrayDynamicIndexing = shaderUniformTexelBufferArrayDynamicIndexing
                    shaderStorageTexelBufferArrayDynamicIndexing = shaderStorageTexelBufferArrayDynamicIndexing
                    shaderUniformBufferArrayNonUniformIndexing = shaderUniformBufferArrayNonUniformIndexing
                    shaderSampledImageArrayNonUniformIndexing = shaderSampledImageArrayNonUniformIndexing
                    shaderStorageBufferArrayNonUniformIndexing = shaderStorageBufferArrayNonUniformIndexing
                    shaderStorageImageArrayNonUniformIndexing = shaderStorageImageArrayNonUniformIndexing
                    shaderInputAttachmentArrayNonUniformIndexing = shaderInputAttachmentArrayNonUniformIndexing
                    shaderUniformTexelBufferArrayNonUniformIndexing = shaderUniformTexelBufferArrayNonUniformIndexing
                    shaderStorageTexelBufferArrayNonUniformIndexing = shaderStorageTexelBufferArrayNonUniformIndexing
                    descriptorBindingUniformBufferUpdateAfterBind = descriptorBindingUniformBufferUpdateAfterBind
                    descriptorBindingSampledImageUpdateAfterBind = descriptorBindingSampledImageUpdateAfterBind
                    descriptorBindingStorageImageUpdateAfterBind = descriptorBindingStorageImageUpdateAfterBind
                    descriptorBindingStorageBufferUpdateAfterBind = descriptorBindingStorageBufferUpdateAfterBind
                    descriptorBindingUniformTexelBufferUpdateAfterBind = descriptorBindingUniformTexelBufferUpdateAfterBind
                    descriptorBindingStorageTexelBufferUpdateAfterBind = descriptorBindingStorageTexelBufferUpdateAfterBind
                    descriptorBindingUpdateUnusedWhilePending = descriptorBindingUpdateUnusedWhilePending
                    descriptorBindingPartiallyBound = descriptorBindingPartiallyBound
                    descriptorBindingVariableDescriptorCount = descriptorBindingVariableDescriptorCount
                    runtimeDescriptorArray = runtimeDescriptorArray
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "shaderInputAttachmentArrayDynamicIndexing = %A" x.shaderInputAttachmentArrayDynamicIndexing
                    sprintf "shaderUniformTexelBufferArrayDynamicIndexing = %A" x.shaderUniformTexelBufferArrayDynamicIndexing
                    sprintf "shaderStorageTexelBufferArrayDynamicIndexing = %A" x.shaderStorageTexelBufferArrayDynamicIndexing
                    sprintf "shaderUniformBufferArrayNonUniformIndexing = %A" x.shaderUniformBufferArrayNonUniformIndexing
                    sprintf "shaderSampledImageArrayNonUniformIndexing = %A" x.shaderSampledImageArrayNonUniformIndexing
                    sprintf "shaderStorageBufferArrayNonUniformIndexing = %A" x.shaderStorageBufferArrayNonUniformIndexing
                    sprintf "shaderStorageImageArrayNonUniformIndexing = %A" x.shaderStorageImageArrayNonUniformIndexing
                    sprintf "shaderInputAttachmentArrayNonUniformIndexing = %A" x.shaderInputAttachmentArrayNonUniformIndexing
                    sprintf "shaderUniformTexelBufferArrayNonUniformIndexing = %A" x.shaderUniformTexelBufferArrayNonUniformIndexing
                    sprintf "shaderStorageTexelBufferArrayNonUniformIndexing = %A" x.shaderStorageTexelBufferArrayNonUniformIndexing
                    sprintf "descriptorBindingUniformBufferUpdateAfterBind = %A" x.descriptorBindingUniformBufferUpdateAfterBind
                    sprintf "descriptorBindingSampledImageUpdateAfterBind = %A" x.descriptorBindingSampledImageUpdateAfterBind
                    sprintf "descriptorBindingStorageImageUpdateAfterBind = %A" x.descriptorBindingStorageImageUpdateAfterBind
                    sprintf "descriptorBindingStorageBufferUpdateAfterBind = %A" x.descriptorBindingStorageBufferUpdateAfterBind
                    sprintf "descriptorBindingUniformTexelBufferUpdateAfterBind = %A" x.descriptorBindingUniformTexelBufferUpdateAfterBind
                    sprintf "descriptorBindingStorageTexelBufferUpdateAfterBind = %A" x.descriptorBindingStorageTexelBufferUpdateAfterBind
                    sprintf "descriptorBindingUpdateUnusedWhilePending = %A" x.descriptorBindingUpdateUnusedWhilePending
                    sprintf "descriptorBindingPartiallyBound = %A" x.descriptorBindingPartiallyBound
                    sprintf "descriptorBindingVariableDescriptorCount = %A" x.descriptorBindingVariableDescriptorCount
                    sprintf "runtimeDescriptorArray = %A" x.runtimeDescriptorArray
                ] |> sprintf "VkPhysicalDeviceDescriptorIndexingFeaturesEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceDescriptorIndexingPropertiesEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public maxUpdateAfterBindDescriptorsInAllPools : uint32
            val mutable public shaderUniformBufferArrayNonUniformIndexingNative : VkBool32
            val mutable public shaderSampledImageArrayNonUniformIndexingNative : VkBool32
            val mutable public shaderStorageBufferArrayNonUniformIndexingNative : VkBool32
            val mutable public shaderStorageImageArrayNonUniformIndexingNative : VkBool32
            val mutable public shaderInputAttachmentArrayNonUniformIndexingNative : VkBool32
            val mutable public robustBufferAccessUpdateAfterBind : VkBool32
            val mutable public quadDivergentImplicitLod : VkBool32
            val mutable public maxPerStageDescriptorUpdateAfterBindSamplers : uint32
            val mutable public maxPerStageDescriptorUpdateAfterBindUniformBuffers : uint32
            val mutable public maxPerStageDescriptorUpdateAfterBindStorageBuffers : uint32
            val mutable public maxPerStageDescriptorUpdateAfterBindSampledImages : uint32
            val mutable public maxPerStageDescriptorUpdateAfterBindStorageImages : uint32
            val mutable public maxPerStageDescriptorUpdateAfterBindInputAttachments : uint32
            val mutable public maxPerStageUpdateAfterBindResources : uint32
            val mutable public maxDescriptorSetUpdateAfterBindSamplers : uint32
            val mutable public maxDescriptorSetUpdateAfterBindUniformBuffers : uint32
            val mutable public maxDescriptorSetUpdateAfterBindUniformBuffersDynamic : uint32
            val mutable public maxDescriptorSetUpdateAfterBindStorageBuffers : uint32
            val mutable public maxDescriptorSetUpdateAfterBindStorageBuffersDynamic : uint32
            val mutable public maxDescriptorSetUpdateAfterBindSampledImages : uint32
            val mutable public maxDescriptorSetUpdateAfterBindStorageImages : uint32
            val mutable public maxDescriptorSetUpdateAfterBindInputAttachments : uint32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , maxUpdateAfterBindDescriptorsInAllPools : uint32
              , shaderUniformBufferArrayNonUniformIndexingNative : VkBool32
              , shaderSampledImageArrayNonUniformIndexingNative : VkBool32
              , shaderStorageBufferArrayNonUniformIndexingNative : VkBool32
              , shaderStorageImageArrayNonUniformIndexingNative : VkBool32
              , shaderInputAttachmentArrayNonUniformIndexingNative : VkBool32
              , robustBufferAccessUpdateAfterBind : VkBool32
              , quadDivergentImplicitLod : VkBool32
              , maxPerStageDescriptorUpdateAfterBindSamplers : uint32
              , maxPerStageDescriptorUpdateAfterBindUniformBuffers : uint32
              , maxPerStageDescriptorUpdateAfterBindStorageBuffers : uint32
              , maxPerStageDescriptorUpdateAfterBindSampledImages : uint32
              , maxPerStageDescriptorUpdateAfterBindStorageImages : uint32
              , maxPerStageDescriptorUpdateAfterBindInputAttachments : uint32
              , maxPerStageUpdateAfterBindResources : uint32
              , maxDescriptorSetUpdateAfterBindSamplers : uint32
              , maxDescriptorSetUpdateAfterBindUniformBuffers : uint32
              , maxDescriptorSetUpdateAfterBindUniformBuffersDynamic : uint32
              , maxDescriptorSetUpdateAfterBindStorageBuffers : uint32
              , maxDescriptorSetUpdateAfterBindStorageBuffersDynamic : uint32
              , maxDescriptorSetUpdateAfterBindSampledImages : uint32
              , maxDescriptorSetUpdateAfterBindStorageImages : uint32
              , maxDescriptorSetUpdateAfterBindInputAttachments : uint32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    maxUpdateAfterBindDescriptorsInAllPools = maxUpdateAfterBindDescriptorsInAllPools
                    shaderUniformBufferArrayNonUniformIndexingNative = shaderUniformBufferArrayNonUniformIndexingNative
                    shaderSampledImageArrayNonUniformIndexingNative = shaderSampledImageArrayNonUniformIndexingNative
                    shaderStorageBufferArrayNonUniformIndexingNative = shaderStorageBufferArrayNonUniformIndexingNative
                    shaderStorageImageArrayNonUniformIndexingNative = shaderStorageImageArrayNonUniformIndexingNative
                    shaderInputAttachmentArrayNonUniformIndexingNative = shaderInputAttachmentArrayNonUniformIndexingNative
                    robustBufferAccessUpdateAfterBind = robustBufferAccessUpdateAfterBind
                    quadDivergentImplicitLod = quadDivergentImplicitLod
                    maxPerStageDescriptorUpdateAfterBindSamplers = maxPerStageDescriptorUpdateAfterBindSamplers
                    maxPerStageDescriptorUpdateAfterBindUniformBuffers = maxPerStageDescriptorUpdateAfterBindUniformBuffers
                    maxPerStageDescriptorUpdateAfterBindStorageBuffers = maxPerStageDescriptorUpdateAfterBindStorageBuffers
                    maxPerStageDescriptorUpdateAfterBindSampledImages = maxPerStageDescriptorUpdateAfterBindSampledImages
                    maxPerStageDescriptorUpdateAfterBindStorageImages = maxPerStageDescriptorUpdateAfterBindStorageImages
                    maxPerStageDescriptorUpdateAfterBindInputAttachments = maxPerStageDescriptorUpdateAfterBindInputAttachments
                    maxPerStageUpdateAfterBindResources = maxPerStageUpdateAfterBindResources
                    maxDescriptorSetUpdateAfterBindSamplers = maxDescriptorSetUpdateAfterBindSamplers
                    maxDescriptorSetUpdateAfterBindUniformBuffers = maxDescriptorSetUpdateAfterBindUniformBuffers
                    maxDescriptorSetUpdateAfterBindUniformBuffersDynamic = maxDescriptorSetUpdateAfterBindUniformBuffersDynamic
                    maxDescriptorSetUpdateAfterBindStorageBuffers = maxDescriptorSetUpdateAfterBindStorageBuffers
                    maxDescriptorSetUpdateAfterBindStorageBuffersDynamic = maxDescriptorSetUpdateAfterBindStorageBuffersDynamic
                    maxDescriptorSetUpdateAfterBindSampledImages = maxDescriptorSetUpdateAfterBindSampledImages
                    maxDescriptorSetUpdateAfterBindStorageImages = maxDescriptorSetUpdateAfterBindStorageImages
                    maxDescriptorSetUpdateAfterBindInputAttachments = maxDescriptorSetUpdateAfterBindInputAttachments
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "maxUpdateAfterBindDescriptorsInAllPools = %A" x.maxUpdateAfterBindDescriptorsInAllPools
                    sprintf "shaderUniformBufferArrayNonUniformIndexingNative = %A" x.shaderUniformBufferArrayNonUniformIndexingNative
                    sprintf "shaderSampledImageArrayNonUniformIndexingNative = %A" x.shaderSampledImageArrayNonUniformIndexingNative
                    sprintf "shaderStorageBufferArrayNonUniformIndexingNative = %A" x.shaderStorageBufferArrayNonUniformIndexingNative
                    sprintf "shaderStorageImageArrayNonUniformIndexingNative = %A" x.shaderStorageImageArrayNonUniformIndexingNative
                    sprintf "shaderInputAttachmentArrayNonUniformIndexingNative = %A" x.shaderInputAttachmentArrayNonUniformIndexingNative
                    sprintf "robustBufferAccessUpdateAfterBind = %A" x.robustBufferAccessUpdateAfterBind
                    sprintf "quadDivergentImplicitLod = %A" x.quadDivergentImplicitLod
                    sprintf "maxPerStageDescriptorUpdateAfterBindSamplers = %A" x.maxPerStageDescriptorUpdateAfterBindSamplers
                    sprintf "maxPerStageDescriptorUpdateAfterBindUniformBuffers = %A" x.maxPerStageDescriptorUpdateAfterBindUniformBuffers
                    sprintf "maxPerStageDescriptorUpdateAfterBindStorageBuffers = %A" x.maxPerStageDescriptorUpdateAfterBindStorageBuffers
                    sprintf "maxPerStageDescriptorUpdateAfterBindSampledImages = %A" x.maxPerStageDescriptorUpdateAfterBindSampledImages
                    sprintf "maxPerStageDescriptorUpdateAfterBindStorageImages = %A" x.maxPerStageDescriptorUpdateAfterBindStorageImages
                    sprintf "maxPerStageDescriptorUpdateAfterBindInputAttachments = %A" x.maxPerStageDescriptorUpdateAfterBindInputAttachments
                    sprintf "maxPerStageUpdateAfterBindResources = %A" x.maxPerStageUpdateAfterBindResources
                    sprintf "maxDescriptorSetUpdateAfterBindSamplers = %A" x.maxDescriptorSetUpdateAfterBindSamplers
                    sprintf "maxDescriptorSetUpdateAfterBindUniformBuffers = %A" x.maxDescriptorSetUpdateAfterBindUniformBuffers
                    sprintf "maxDescriptorSetUpdateAfterBindUniformBuffersDynamic = %A" x.maxDescriptorSetUpdateAfterBindUniformBuffersDynamic
                    sprintf "maxDescriptorSetUpdateAfterBindStorageBuffers = %A" x.maxDescriptorSetUpdateAfterBindStorageBuffers
                    sprintf "maxDescriptorSetUpdateAfterBindStorageBuffersDynamic = %A" x.maxDescriptorSetUpdateAfterBindStorageBuffersDynamic
                    sprintf "maxDescriptorSetUpdateAfterBindSampledImages = %A" x.maxDescriptorSetUpdateAfterBindSampledImages
                    sprintf "maxDescriptorSetUpdateAfterBindStorageImages = %A" x.maxDescriptorSetUpdateAfterBindStorageImages
                    sprintf "maxDescriptorSetUpdateAfterBindInputAttachments = %A" x.maxDescriptorSetUpdateAfterBindInputAttachments
                ] |> sprintf "VkPhysicalDeviceDescriptorIndexingPropertiesEXT { %s }"
        end
    
    
    type VkDescriptorPoolCreateFlags with
         static member inline UpdateAfterBindBitExt = unbox<VkDescriptorPoolCreateFlags> 2
    type VkDescriptorSetLayoutCreateFlags with
         static member inline UpdateAfterBindPoolBitExt = unbox<VkDescriptorSetLayoutCreateFlags> 2
    type VkResult with
         static member inline VkErrorFragmentationExt = unbox<VkResult> -1000161000
    type VkStructureType with
         static member inline DescriptorSetLayoutBindingFlagsCreateInfoExt = unbox<VkStructureType> 1000161000
         static member inline PhysicalDeviceDescriptorIndexingFeaturesExt = unbox<VkStructureType> 1000161001
         static member inline PhysicalDeviceDescriptorIndexingPropertiesExt = unbox<VkStructureType> 1000161002
         static member inline DescriptorSetVariableDescriptorCountAllocateInfoExt = unbox<VkStructureType> 1000161003
         static member inline DescriptorSetVariableDescriptorCountLayoutSupportExt = unbox<VkStructureType> 1000161004
    

module EXTDiscardRectangles =
    let Name = "VK_EXT_discard_rectangles"
    let Number = 100
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name ]
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    type VkDiscardRectangleModeEXT = 
        | VkDiscardRectangleModeInclusiveExt = 0
        | VkDiscardRectangleModeExclusiveExt = 1
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceDiscardRectanglePropertiesEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public maxDiscardRectangles : uint32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , maxDiscardRectangles : uint32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    maxDiscardRectangles = maxDiscardRectangles
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "maxDiscardRectangles = %A" x.maxDiscardRectangles
                ] |> sprintf "VkPhysicalDeviceDiscardRectanglePropertiesEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPipelineDiscardRectangleStateCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkPipelineDiscardRectangleStateCreateFlagsEXT
            val mutable public discardRectangleMode : VkDiscardRectangleModeEXT
            val mutable public discardRectangleCount : uint32
            val mutable public pDiscardRectangles : nativeptr<VkRect2D>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkPipelineDiscardRectangleStateCreateFlagsEXT
              , discardRectangleMode : VkDiscardRectangleModeEXT
              , discardRectangleCount : uint32
              , pDiscardRectangles : nativeptr<VkRect2D>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    discardRectangleMode = discardRectangleMode
                    discardRectangleCount = discardRectangleCount
                    pDiscardRectangles = pDiscardRectangles
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "discardRectangleMode = %A" x.discardRectangleMode
                    sprintf "discardRectangleCount = %A" x.discardRectangleCount
                    sprintf "pDiscardRectangles = %A" x.pDiscardRectangles
                ] |> sprintf "VkPipelineDiscardRectangleStateCreateInfoEXT { %s }"
        end
    
    
    type VkDynamicState with
         static member inline DiscardRectangleExt = unbox<VkDynamicState> 1000099000
    type VkStructureType with
         static member inline PhysicalDeviceDiscardRectanglePropertiesExt = unbox<VkStructureType> 1000099000
         static member inline PipelineDiscardRectangleStateCreateInfoExt = unbox<VkStructureType> 1000099001
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdSetDiscardRectangleEXTDel = delegate of VkCommandBuffer * uint32 * uint32 * nativeptr<VkRect2D> -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_EXT_discard_rectangles")
            static let s_vkCmdSetDiscardRectangleEXTDel = VkRaw.vkImportInstanceDelegate<VkCmdSetDiscardRectangleEXTDel> "vkCmdSetDiscardRectangleEXT"
            static do Report.End(3) |> ignore
            static member vkCmdSetDiscardRectangleEXT = s_vkCmdSetDiscardRectangleEXTDel
        let vkCmdSetDiscardRectangleEXT(commandBuffer : VkCommandBuffer, firstDiscardRectangle : uint32, discardRectangleCount : uint32, pDiscardRectangles : nativeptr<VkRect2D>) = Loader<unit>.vkCmdSetDiscardRectangleEXT.Invoke(commandBuffer, firstDiscardRectangle, discardRectangleCount, pDiscardRectangles)

module EXTDisplaySurfaceCounter =
    let Name = "VK_EXT_display_surface_counter"
    let Number = 91
    
    let Required = [ KHRDisplay.Name; KHRSurface.Name ]
    open KHRDisplay
    open KHRSurface
    open EXTDebugReport
    
    [<Flags>]
    type VkSurfaceCounterFlagsEXT = 
        | None = 0
        | VkSurfaceCounterVblankExt = 0x00000001
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSurfaceCapabilities2EXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public minImageCount : uint32
            val mutable public maxImageCount : uint32
            val mutable public currentExtent : VkExtent2D
            val mutable public minImageExtent : VkExtent2D
            val mutable public maxImageExtent : VkExtent2D
            val mutable public maxImageArrayLayers : uint32
            val mutable public supportedTransforms : VkSurfaceTransformFlagsKHR
            val mutable public currentTransform : VkSurfaceTransformFlagsKHR
            val mutable public supportedCompositeAlpha : VkCompositeAlphaFlagsKHR
            val mutable public supportedUsageFlags : VkImageUsageFlags
            val mutable public supportedSurfaceCounters : VkSurfaceCounterFlagsEXT
    
            new(sType : VkStructureType
              , pNext : nativeint
              , minImageCount : uint32
              , maxImageCount : uint32
              , currentExtent : VkExtent2D
              , minImageExtent : VkExtent2D
              , maxImageExtent : VkExtent2D
              , maxImageArrayLayers : uint32
              , supportedTransforms : VkSurfaceTransformFlagsKHR
              , currentTransform : VkSurfaceTransformFlagsKHR
              , supportedCompositeAlpha : VkCompositeAlphaFlagsKHR
              , supportedUsageFlags : VkImageUsageFlags
              , supportedSurfaceCounters : VkSurfaceCounterFlagsEXT
              ) =
                {
                    sType = sType
                    pNext = pNext
                    minImageCount = minImageCount
                    maxImageCount = maxImageCount
                    currentExtent = currentExtent
                    minImageExtent = minImageExtent
                    maxImageExtent = maxImageExtent
                    maxImageArrayLayers = maxImageArrayLayers
                    supportedTransforms = supportedTransforms
                    currentTransform = currentTransform
                    supportedCompositeAlpha = supportedCompositeAlpha
                    supportedUsageFlags = supportedUsageFlags
                    supportedSurfaceCounters = supportedSurfaceCounters
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "minImageCount = %A" x.minImageCount
                    sprintf "maxImageCount = %A" x.maxImageCount
                    sprintf "currentExtent = %A" x.currentExtent
                    sprintf "minImageExtent = %A" x.minImageExtent
                    sprintf "maxImageExtent = %A" x.maxImageExtent
                    sprintf "maxImageArrayLayers = %A" x.maxImageArrayLayers
                    sprintf "supportedTransforms = %A" x.supportedTransforms
                    sprintf "currentTransform = %A" x.currentTransform
                    sprintf "supportedCompositeAlpha = %A" x.supportedCompositeAlpha
                    sprintf "supportedUsageFlags = %A" x.supportedUsageFlags
                    sprintf "supportedSurfaceCounters = %A" x.supportedSurfaceCounters
                ] |> sprintf "VkSurfaceCapabilities2EXT { %s }"
        end
    
    
    type VkStructureType with
         static member inline SurfaceCapabilities2Ext = unbox<VkStructureType> 1000090000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceSurfaceCapabilities2EXTDel = delegate of VkPhysicalDevice * VkSurfaceKHR * nativeptr<VkSurfaceCapabilities2EXT> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_EXT_display_surface_counter")
            static let s_vkGetPhysicalDeviceSurfaceCapabilities2EXTDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceSurfaceCapabilities2EXTDel> "vkGetPhysicalDeviceSurfaceCapabilities2EXT"
            static do Report.End(3) |> ignore
            static member vkGetPhysicalDeviceSurfaceCapabilities2EXT = s_vkGetPhysicalDeviceSurfaceCapabilities2EXTDel
        let vkGetPhysicalDeviceSurfaceCapabilities2EXT(physicalDevice : VkPhysicalDevice, surface : VkSurfaceKHR, pSurfaceCapabilities : nativeptr<VkSurfaceCapabilities2EXT>) = Loader<unit>.vkGetPhysicalDeviceSurfaceCapabilities2EXT.Invoke(physicalDevice, surface, pSurfaceCapabilities)

module KHRSwapchain =
    let Name = "VK_KHR_swapchain"
    let Number = 2
    
    let Required = [ KHRSurface.Name ]
    open KHRSurface
    open EXTDebugReport
    
    
    
    type VkImageLayout with
         static member inline PresentSrcKhr = unbox<VkImageLayout> 1000001002
    type VkObjectType with
         static member inline SwapchainKhr = unbox<VkObjectType> 1000001000
    type VkResult with
         static member inline VkSuboptimalKhr = unbox<VkResult> 1000001003
         static member inline VkErrorOutOfDateKhr = unbox<VkResult> -1000001004
    type VkStructureType with
         static member inline SwapchainCreateInfoKhr = unbox<VkStructureType> 1000001000
         static member inline PresentInfoKhr = unbox<VkStructureType> 1000001001
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateSwapchainKHRDel = delegate of VkDevice * nativeptr<VkSwapchainCreateInfoKHR> * nativeptr<VkAllocationCallbacks> * nativeptr<VkSwapchainKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkDestroySwapchainKHRDel = delegate of VkDevice * VkSwapchainKHR * nativeptr<VkAllocationCallbacks> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetSwapchainImagesKHRDel = delegate of VkDevice * VkSwapchainKHR * nativeptr<uint32> * nativeptr<VkImage> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkAcquireNextImageKHRDel = delegate of VkDevice * VkSwapchainKHR * uint64 * VkSemaphore * VkFence * nativeptr<uint32> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkQueuePresentKHRDel = delegate of VkQueue * nativeptr<VkPresentInfoKHR> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_swapchain")
            static let s_vkCreateSwapchainKHRDel = VkRaw.vkImportInstanceDelegate<VkCreateSwapchainKHRDel> "vkCreateSwapchainKHR"
            static let s_vkDestroySwapchainKHRDel = VkRaw.vkImportInstanceDelegate<VkDestroySwapchainKHRDel> "vkDestroySwapchainKHR"
            static let s_vkGetSwapchainImagesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetSwapchainImagesKHRDel> "vkGetSwapchainImagesKHR"
            static let s_vkAcquireNextImageKHRDel = VkRaw.vkImportInstanceDelegate<VkAcquireNextImageKHRDel> "vkAcquireNextImageKHR"
            static let s_vkQueuePresentKHRDel = VkRaw.vkImportInstanceDelegate<VkQueuePresentKHRDel> "vkQueuePresentKHR"
            static do Report.End(3) |> ignore
            static member vkCreateSwapchainKHR = s_vkCreateSwapchainKHRDel
            static member vkDestroySwapchainKHR = s_vkDestroySwapchainKHRDel
            static member vkGetSwapchainImagesKHR = s_vkGetSwapchainImagesKHRDel
            static member vkAcquireNextImageKHR = s_vkAcquireNextImageKHRDel
            static member vkQueuePresentKHR = s_vkQueuePresentKHRDel
        let vkCreateSwapchainKHR(device : VkDevice, pCreateInfo : nativeptr<VkSwapchainCreateInfoKHR>, pAllocator : nativeptr<VkAllocationCallbacks>, pSwapchain : nativeptr<VkSwapchainKHR>) = Loader<unit>.vkCreateSwapchainKHR.Invoke(device, pCreateInfo, pAllocator, pSwapchain)
        let vkDestroySwapchainKHR(device : VkDevice, swapchain : VkSwapchainKHR, pAllocator : nativeptr<VkAllocationCallbacks>) = Loader<unit>.vkDestroySwapchainKHR.Invoke(device, swapchain, pAllocator)
        let vkGetSwapchainImagesKHR(device : VkDevice, swapchain : VkSwapchainKHR, pSwapchainImageCount : nativeptr<uint32>, pSwapchainImages : nativeptr<VkImage>) = Loader<unit>.vkGetSwapchainImagesKHR.Invoke(device, swapchain, pSwapchainImageCount, pSwapchainImages)
        let vkAcquireNextImageKHR(device : VkDevice, swapchain : VkSwapchainKHR, timeout : uint64, semaphore : VkSemaphore, fence : VkFence, pImageIndex : nativeptr<uint32>) = Loader<unit>.vkAcquireNextImageKHR.Invoke(device, swapchain, timeout, semaphore, fence, pImageIndex)
        let vkQueuePresentKHR(queue : VkQueue, pPresentInfo : nativeptr<VkPresentInfoKHR>) = Loader<unit>.vkQueuePresentKHR.Invoke(queue, pPresentInfo)

module EXTDisplayControl =
    let Name = "VK_EXT_display_control"
    let Number = 92
    
    let Required = [ EXTDisplaySurfaceCounter.Name; KHRDisplay.Name; KHRSurface.Name; KHRSwapchain.Name ]
    open EXTDisplaySurfaceCounter
    open KHRDisplay
    open KHRSurface
    open KHRSwapchain
    open EXTDebugReport
    
    type VkDisplayPowerStateEXT = 
        | VkDisplayPowerStateOffExt = 0
        | VkDisplayPowerStateSuspendExt = 1
        | VkDisplayPowerStateOnExt = 2
    
    type VkDeviceEventTypeEXT = 
        | VkDeviceEventTypeDisplayHotplugExt = 0
    
    type VkDisplayEventTypeEXT = 
        | VkDisplayEventTypeFirstPixelOutExt = 0
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDeviceEventInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public deviceEvent : VkDeviceEventTypeEXT
    
            new(sType : VkStructureType
              , pNext : nativeint
              , deviceEvent : VkDeviceEventTypeEXT
              ) =
                {
                    sType = sType
                    pNext = pNext
                    deviceEvent = deviceEvent
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "deviceEvent = %A" x.deviceEvent
                ] |> sprintf "VkDeviceEventInfoEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDisplayEventInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public displayEvent : VkDisplayEventTypeEXT
    
            new(sType : VkStructureType
              , pNext : nativeint
              , displayEvent : VkDisplayEventTypeEXT
              ) =
                {
                    sType = sType
                    pNext = pNext
                    displayEvent = displayEvent
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "displayEvent = %A" x.displayEvent
                ] |> sprintf "VkDisplayEventInfoEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDisplayPowerInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public powerState : VkDisplayPowerStateEXT
    
            new(sType : VkStructureType
              , pNext : nativeint
              , powerState : VkDisplayPowerStateEXT
              ) =
                {
                    sType = sType
                    pNext = pNext
                    powerState = powerState
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "powerState = %A" x.powerState
                ] |> sprintf "VkDisplayPowerInfoEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSwapchainCounterCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public surfaceCounters : VkSurfaceCounterFlagsEXT
    
            new(sType : VkStructureType
              , pNext : nativeint
              , surfaceCounters : VkSurfaceCounterFlagsEXT
              ) =
                {
                    sType = sType
                    pNext = pNext
                    surfaceCounters = surfaceCounters
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "surfaceCounters = %A" x.surfaceCounters
                ] |> sprintf "VkSwapchainCounterCreateInfoEXT { %s }"
        end
    
    
    type VkStructureType with
         static member inline DisplayPowerInfoExt = unbox<VkStructureType> 1000091000
         static member inline DeviceEventInfoExt = unbox<VkStructureType> 1000091001
         static member inline DisplayEventInfoExt = unbox<VkStructureType> 1000091002
         static member inline SwapchainCounterCreateInfoExt = unbox<VkStructureType> 1000091003
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkDisplayPowerControlEXTDel = delegate of VkDevice * VkDisplayKHR * nativeptr<VkDisplayPowerInfoEXT> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkRegisterDeviceEventEXTDel = delegate of VkDevice * nativeptr<VkDeviceEventInfoEXT> * nativeptr<VkAllocationCallbacks> * nativeptr<VkFence> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkRegisterDisplayEventEXTDel = delegate of VkDevice * VkDisplayKHR * nativeptr<VkDisplayEventInfoEXT> * nativeptr<VkAllocationCallbacks> * nativeptr<VkFence> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetSwapchainCounterEXTDel = delegate of VkDevice * VkSwapchainKHR * VkSurfaceCounterFlagsEXT * nativeptr<uint64> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_EXT_display_control")
            static let s_vkDisplayPowerControlEXTDel = VkRaw.vkImportInstanceDelegate<VkDisplayPowerControlEXTDel> "vkDisplayPowerControlEXT"
            static let s_vkRegisterDeviceEventEXTDel = VkRaw.vkImportInstanceDelegate<VkRegisterDeviceEventEXTDel> "vkRegisterDeviceEventEXT"
            static let s_vkRegisterDisplayEventEXTDel = VkRaw.vkImportInstanceDelegate<VkRegisterDisplayEventEXTDel> "vkRegisterDisplayEventEXT"
            static let s_vkGetSwapchainCounterEXTDel = VkRaw.vkImportInstanceDelegate<VkGetSwapchainCounterEXTDel> "vkGetSwapchainCounterEXT"
            static do Report.End(3) |> ignore
            static member vkDisplayPowerControlEXT = s_vkDisplayPowerControlEXTDel
            static member vkRegisterDeviceEventEXT = s_vkRegisterDeviceEventEXTDel
            static member vkRegisterDisplayEventEXT = s_vkRegisterDisplayEventEXTDel
            static member vkGetSwapchainCounterEXT = s_vkGetSwapchainCounterEXTDel
        let vkDisplayPowerControlEXT(device : VkDevice, display : VkDisplayKHR, pDisplayPowerInfo : nativeptr<VkDisplayPowerInfoEXT>) = Loader<unit>.vkDisplayPowerControlEXT.Invoke(device, display, pDisplayPowerInfo)
        let vkRegisterDeviceEventEXT(device : VkDevice, pDeviceEventInfo : nativeptr<VkDeviceEventInfoEXT>, pAllocator : nativeptr<VkAllocationCallbacks>, pFence : nativeptr<VkFence>) = Loader<unit>.vkRegisterDeviceEventEXT.Invoke(device, pDeviceEventInfo, pAllocator, pFence)
        let vkRegisterDisplayEventEXT(device : VkDevice, display : VkDisplayKHR, pDisplayEventInfo : nativeptr<VkDisplayEventInfoEXT>, pAllocator : nativeptr<VkAllocationCallbacks>, pFence : nativeptr<VkFence>) = Loader<unit>.vkRegisterDisplayEventEXT.Invoke(device, display, pDisplayEventInfo, pAllocator, pFence)
        let vkGetSwapchainCounterEXT(device : VkDevice, swapchain : VkSwapchainKHR, counter : VkSurfaceCounterFlagsEXT, pCounterValue : nativeptr<uint64>) = Loader<unit>.vkGetSwapchainCounterEXT.Invoke(device, swapchain, counter, pCounterValue)

module KHRExternalMemoryFd =
    let Name = "VK_KHR_external_memory_fd"
    let Number = 75
    
    let Required = [ KHRExternalMemory.Name; KHRExternalMemoryCapabilities.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalMemory
    open KHRExternalMemoryCapabilities
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImportMemoryFdInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleType : VkExternalMemoryHandleTypeFlags
            val mutable public fd : int
    
            new(sType : VkStructureType
              , pNext : nativeint
              , handleType : VkExternalMemoryHandleTypeFlags
              , fd : int
              ) =
                {
                    sType = sType
                    pNext = pNext
                    handleType = handleType
                    fd = fd
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "handleType = %A" x.handleType
                    sprintf "fd = %A" x.fd
                ] |> sprintf "VkImportMemoryFdInfoKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMemoryFdPropertiesKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public memoryTypeBits : uint32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , memoryTypeBits : uint32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    memoryTypeBits = memoryTypeBits
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "memoryTypeBits = %A" x.memoryTypeBits
                ] |> sprintf "VkMemoryFdPropertiesKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMemoryGetFdInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public memory : VkDeviceMemory
            val mutable public handleType : VkExternalMemoryHandleTypeFlags
    
            new(sType : VkStructureType
              , pNext : nativeint
              , memory : VkDeviceMemory
              , handleType : VkExternalMemoryHandleTypeFlags
              ) =
                {
                    sType = sType
                    pNext = pNext
                    memory = memory
                    handleType = handleType
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "memory = %A" x.memory
                    sprintf "handleType = %A" x.handleType
                ] |> sprintf "VkMemoryGetFdInfoKHR { %s }"
        end
    
    
    type VkStructureType with
         static member inline ImportMemoryFdInfoKhr = unbox<VkStructureType> 1000074000
         static member inline MemoryFdPropertiesKhr = unbox<VkStructureType> 1000074001
         static member inline MemoryGetFdInfoKhr = unbox<VkStructureType> 1000074002
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetMemoryFdKHRDel = delegate of VkDevice * nativeptr<VkMemoryGetFdInfoKHR> * nativeptr<int> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetMemoryFdPropertiesKHRDel = delegate of VkDevice * VkExternalMemoryHandleTypeFlags * int * nativeptr<VkMemoryFdPropertiesKHR> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_external_memory_fd")
            static let s_vkGetMemoryFdKHRDel = VkRaw.vkImportInstanceDelegate<VkGetMemoryFdKHRDel> "vkGetMemoryFdKHR"
            static let s_vkGetMemoryFdPropertiesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetMemoryFdPropertiesKHRDel> "vkGetMemoryFdPropertiesKHR"
            static do Report.End(3) |> ignore
            static member vkGetMemoryFdKHR = s_vkGetMemoryFdKHRDel
            static member vkGetMemoryFdPropertiesKHR = s_vkGetMemoryFdPropertiesKHRDel
        let vkGetMemoryFdKHR(device : VkDevice, pGetFdInfo : nativeptr<VkMemoryGetFdInfoKHR>, pFd : nativeptr<int>) = Loader<unit>.vkGetMemoryFdKHR.Invoke(device, pGetFdInfo, pFd)
        let vkGetMemoryFdPropertiesKHR(device : VkDevice, handleType : VkExternalMemoryHandleTypeFlags, fd : int, pMemoryFdProperties : nativeptr<VkMemoryFdPropertiesKHR>) = Loader<unit>.vkGetMemoryFdPropertiesKHR.Invoke(device, handleType, fd, pMemoryFdProperties)

module EXTExternalMemoryDmaBuf =
    let Name = "VK_EXT_external_memory_dma_buf"
    let Number = 126
    
    let Required = [ KHRExternalMemory.Name; KHRExternalMemoryCapabilities.Name; KHRExternalMemoryFd.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalMemory
    open KHRExternalMemoryCapabilities
    open KHRExternalMemoryFd
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    
    type VkExternalMemoryHandleTypeFlags with
         static member inline DmaBufBitExt = unbox<VkExternalMemoryHandleTypeFlags> 512
    

module EXTExternalMemoryHost =
    let Name = "VK_EXT_external_memory_host"
    let Number = 179
    
    let Required = [ KHRExternalMemory.Name; KHRExternalMemoryCapabilities.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalMemory
    open KHRExternalMemoryCapabilities
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImportMemoryHostPointerInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleType : VkExternalMemoryHandleTypeFlags
            val mutable public pHostPointer : nativeint
    
            new(sType : VkStructureType
              , pNext : nativeint
              , handleType : VkExternalMemoryHandleTypeFlags
              , pHostPointer : nativeint
              ) =
                {
                    sType = sType
                    pNext = pNext
                    handleType = handleType
                    pHostPointer = pHostPointer
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "handleType = %A" x.handleType
                    sprintf "pHostPointer = %A" x.pHostPointer
                ] |> sprintf "VkImportMemoryHostPointerInfoEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMemoryHostPointerPropertiesEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public memoryTypeBits : uint32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , memoryTypeBits : uint32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    memoryTypeBits = memoryTypeBits
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "memoryTypeBits = %A" x.memoryTypeBits
                ] |> sprintf "VkMemoryHostPointerPropertiesEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceExternalMemoryHostPropertiesEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public minImportedHostPointerAlignment : VkDeviceSize
    
            new(sType : VkStructureType
              , pNext : nativeint
              , minImportedHostPointerAlignment : VkDeviceSize
              ) =
                {
                    sType = sType
                    pNext = pNext
                    minImportedHostPointerAlignment = minImportedHostPointerAlignment
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "minImportedHostPointerAlignment = %A" x.minImportedHostPointerAlignment
                ] |> sprintf "VkPhysicalDeviceExternalMemoryHostPropertiesEXT { %s }"
        end
    
    
    type VkExternalMemoryHandleTypeFlags with
         static member inline HostAllocationBitExt = unbox<VkExternalMemoryHandleTypeFlags> 128
         static member inline HostMappedForeignMemoryBitExt = unbox<VkExternalMemoryHandleTypeFlags> 256
    type VkStructureType with
         static member inline ImportMemoryHostPointerInfoExt = unbox<VkStructureType> 1000178000
         static member inline MemoryHostPointerPropertiesExt = unbox<VkStructureType> 1000178001
         static member inline PhysicalDeviceExternalMemoryHostPropertiesExt = unbox<VkStructureType> 1000178002
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetMemoryHostPointerPropertiesEXTDel = delegate of VkDevice * VkExternalMemoryHandleTypeFlags * nativeint * nativeptr<VkMemoryHostPointerPropertiesEXT> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_EXT_external_memory_host")
            static let s_vkGetMemoryHostPointerPropertiesEXTDel = VkRaw.vkImportInstanceDelegate<VkGetMemoryHostPointerPropertiesEXTDel> "vkGetMemoryHostPointerPropertiesEXT"
            static do Report.End(3) |> ignore
            static member vkGetMemoryHostPointerPropertiesEXT = s_vkGetMemoryHostPointerPropertiesEXTDel
        let vkGetMemoryHostPointerPropertiesEXT(device : VkDevice, handleType : VkExternalMemoryHandleTypeFlags, pHostPointer : nativeint, pMemoryHostPointerProperties : nativeptr<VkMemoryHostPointerPropertiesEXT>) = Loader<unit>.vkGetMemoryHostPointerPropertiesEXT.Invoke(device, handleType, pHostPointer, pMemoryHostPointerProperties)

module EXTGlobalPriority =
    let Name = "VK_EXT_global_priority"
    let Number = 175
    
    open EXTDebugReport
    
    type VkQueueGlobalPriorityEXT = 
        | VkQueueGlobalPriorityLowExt = 128
        | VkQueueGlobalPriorityMediumExt = 256
        | VkQueueGlobalPriorityHighExt = 512
        | VkQueueGlobalPriorityRealtimeExt = 1024
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDeviceQueueGlobalPriorityCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public globalPriority : VkQueueGlobalPriorityEXT
    
            new(sType : VkStructureType
              , pNext : nativeint
              , globalPriority : VkQueueGlobalPriorityEXT
              ) =
                {
                    sType = sType
                    pNext = pNext
                    globalPriority = globalPriority
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "globalPriority = %A" x.globalPriority
                ] |> sprintf "VkDeviceQueueGlobalPriorityCreateInfoEXT { %s }"
        end
    
    
    type VkResult with
         static member inline VkErrorNotPermittedExt = unbox<VkResult> -1000174001
    type VkStructureType with
         static member inline DeviceQueueGlobalPriorityCreateInfoExt = unbox<VkStructureType> 1000174000
    

module EXTHdrMetadata =
    let Name = "VK_EXT_hdr_metadata"
    let Number = 106
    
    let Required = [ KHRSurface.Name; KHRSwapchain.Name ]
    open KHRSurface
    open KHRSwapchain
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkXYColorEXT = 
        struct
            val mutable public x : float32
            val mutable public y : float32
    
            new(x : float32
              , y : float32
              ) =
                {
                    x = x
                    y = y
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "x = %A" x.x
                    sprintf "y = %A" x.y
                ] |> sprintf "VkXYColorEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkHdrMetadataEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public displayPrimaryRed : VkXYColorEXT
            val mutable public displayPrimaryGreen : VkXYColorEXT
            val mutable public displayPrimaryBlue : VkXYColorEXT
            val mutable public whitePoint : VkXYColorEXT
            val mutable public maxLuminance : float32
            val mutable public minLuminance : float32
            val mutable public maxContentLightLevel : float32
            val mutable public maxFrameAverageLightLevel : float32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , displayPrimaryRed : VkXYColorEXT
              , displayPrimaryGreen : VkXYColorEXT
              , displayPrimaryBlue : VkXYColorEXT
              , whitePoint : VkXYColorEXT
              , maxLuminance : float32
              , minLuminance : float32
              , maxContentLightLevel : float32
              , maxFrameAverageLightLevel : float32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    displayPrimaryRed = displayPrimaryRed
                    displayPrimaryGreen = displayPrimaryGreen
                    displayPrimaryBlue = displayPrimaryBlue
                    whitePoint = whitePoint
                    maxLuminance = maxLuminance
                    minLuminance = minLuminance
                    maxContentLightLevel = maxContentLightLevel
                    maxFrameAverageLightLevel = maxFrameAverageLightLevel
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "displayPrimaryRed = %A" x.displayPrimaryRed
                    sprintf "displayPrimaryGreen = %A" x.displayPrimaryGreen
                    sprintf "displayPrimaryBlue = %A" x.displayPrimaryBlue
                    sprintf "whitePoint = %A" x.whitePoint
                    sprintf "maxLuminance = %A" x.maxLuminance
                    sprintf "minLuminance = %A" x.minLuminance
                    sprintf "maxContentLightLevel = %A" x.maxContentLightLevel
                    sprintf "maxFrameAverageLightLevel = %A" x.maxFrameAverageLightLevel
                ] |> sprintf "VkHdrMetadataEXT { %s }"
        end
    
    
    type VkStructureType with
         static member inline HdrMetadataExt = unbox<VkStructureType> 1000105000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkSetHdrMetadataEXTDel = delegate of VkDevice * uint32 * nativeptr<VkSwapchainKHR> * nativeptr<VkHdrMetadataEXT> -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_EXT_hdr_metadata")
            static let s_vkSetHdrMetadataEXTDel = VkRaw.vkImportInstanceDelegate<VkSetHdrMetadataEXTDel> "vkSetHdrMetadataEXT"
            static do Report.End(3) |> ignore
            static member vkSetHdrMetadataEXT = s_vkSetHdrMetadataEXTDel
        let vkSetHdrMetadataEXT(device : VkDevice, swapchainCount : uint32, pSwapchains : nativeptr<VkSwapchainKHR>, pMetadata : nativeptr<VkHdrMetadataEXT>) = Loader<unit>.vkSetHdrMetadataEXT.Invoke(device, swapchainCount, pSwapchains, pMetadata)

module EXTPostDepthCoverage =
    let Name = "VK_EXT_post_depth_coverage"
    let Number = 156
    
    open EXTDebugReport
    
    
    
    

module EXTSampleLocations =
    let Name = "VK_EXT_sample_locations"
    let Number = 144
    
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSampleLocationEXT = 
        struct
            val mutable public x : float32
            val mutable public y : float32
    
            new(x : float32
              , y : float32
              ) =
                {
                    x = x
                    y = y
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "x = %A" x.x
                    sprintf "y = %A" x.y
                ] |> sprintf "VkSampleLocationEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSampleLocationsInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public sampleLocationsPerPixel : VkSampleCountFlags
            val mutable public sampleLocationGridSize : VkExtent2D
            val mutable public sampleLocationsCount : uint32
            val mutable public pSampleLocations : nativeptr<VkSampleLocationEXT>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , sampleLocationsPerPixel : VkSampleCountFlags
              , sampleLocationGridSize : VkExtent2D
              , sampleLocationsCount : uint32
              , pSampleLocations : nativeptr<VkSampleLocationEXT>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    sampleLocationsPerPixel = sampleLocationsPerPixel
                    sampleLocationGridSize = sampleLocationGridSize
                    sampleLocationsCount = sampleLocationsCount
                    pSampleLocations = pSampleLocations
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "sampleLocationsPerPixel = %A" x.sampleLocationsPerPixel
                    sprintf "sampleLocationGridSize = %A" x.sampleLocationGridSize
                    sprintf "sampleLocationsCount = %A" x.sampleLocationsCount
                    sprintf "pSampleLocations = %A" x.pSampleLocations
                ] |> sprintf "VkSampleLocationsInfoEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkAttachmentSampleLocationsEXT = 
        struct
            val mutable public attachmentIndex : uint32
            val mutable public sampleLocationsInfo : VkSampleLocationsInfoEXT
    
            new(attachmentIndex : uint32
              , sampleLocationsInfo : VkSampleLocationsInfoEXT
              ) =
                {
                    attachmentIndex = attachmentIndex
                    sampleLocationsInfo = sampleLocationsInfo
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "attachmentIndex = %A" x.attachmentIndex
                    sprintf "sampleLocationsInfo = %A" x.sampleLocationsInfo
                ] |> sprintf "VkAttachmentSampleLocationsEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMultisamplePropertiesEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public maxSampleLocationGridSize : VkExtent2D
    
            new(sType : VkStructureType
              , pNext : nativeint
              , maxSampleLocationGridSize : VkExtent2D
              ) =
                {
                    sType = sType
                    pNext = pNext
                    maxSampleLocationGridSize = maxSampleLocationGridSize
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "maxSampleLocationGridSize = %A" x.maxSampleLocationGridSize
                ] |> sprintf "VkMultisamplePropertiesEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceSampleLocationsPropertiesEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public sampleLocationSampleCounts : VkSampleCountFlags
            val mutable public maxSampleLocationGridSize : VkExtent2D
            val mutable public sampleLocationCoordinateRange : V2f
            val mutable public sampleLocationSubPixelBits : uint32
            val mutable public variableSampleLocations : VkBool32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , sampleLocationSampleCounts : VkSampleCountFlags
              , maxSampleLocationGridSize : VkExtent2D
              , sampleLocationCoordinateRange : V2f
              , sampleLocationSubPixelBits : uint32
              , variableSampleLocations : VkBool32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    sampleLocationSampleCounts = sampleLocationSampleCounts
                    maxSampleLocationGridSize = maxSampleLocationGridSize
                    sampleLocationCoordinateRange = sampleLocationCoordinateRange
                    sampleLocationSubPixelBits = sampleLocationSubPixelBits
                    variableSampleLocations = variableSampleLocations
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "sampleLocationSampleCounts = %A" x.sampleLocationSampleCounts
                    sprintf "maxSampleLocationGridSize = %A" x.maxSampleLocationGridSize
                    sprintf "sampleLocationCoordinateRange = %A" x.sampleLocationCoordinateRange
                    sprintf "sampleLocationSubPixelBits = %A" x.sampleLocationSubPixelBits
                    sprintf "variableSampleLocations = %A" x.variableSampleLocations
                ] |> sprintf "VkPhysicalDeviceSampleLocationsPropertiesEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPipelineSampleLocationsStateCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public sampleLocationsEnable : VkBool32
            val mutable public sampleLocationsInfo : VkSampleLocationsInfoEXT
    
            new(sType : VkStructureType
              , pNext : nativeint
              , sampleLocationsEnable : VkBool32
              , sampleLocationsInfo : VkSampleLocationsInfoEXT
              ) =
                {
                    sType = sType
                    pNext = pNext
                    sampleLocationsEnable = sampleLocationsEnable
                    sampleLocationsInfo = sampleLocationsInfo
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "sampleLocationsEnable = %A" x.sampleLocationsEnable
                    sprintf "sampleLocationsInfo = %A" x.sampleLocationsInfo
                ] |> sprintf "VkPipelineSampleLocationsStateCreateInfoEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSubpassSampleLocationsEXT = 
        struct
            val mutable public subpassIndex : uint32
            val mutable public sampleLocationsInfo : VkSampleLocationsInfoEXT
    
            new(subpassIndex : uint32
              , sampleLocationsInfo : VkSampleLocationsInfoEXT
              ) =
                {
                    subpassIndex = subpassIndex
                    sampleLocationsInfo = sampleLocationsInfo
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "subpassIndex = %A" x.subpassIndex
                    sprintf "sampleLocationsInfo = %A" x.sampleLocationsInfo
                ] |> sprintf "VkSubpassSampleLocationsEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkRenderPassSampleLocationsBeginInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public attachmentInitialSampleLocationsCount : uint32
            val mutable public pAttachmentInitialSampleLocations : nativeptr<VkAttachmentSampleLocationsEXT>
            val mutable public postSubpassSampleLocationsCount : uint32
            val mutable public pPostSubpassSampleLocations : nativeptr<VkSubpassSampleLocationsEXT>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , attachmentInitialSampleLocationsCount : uint32
              , pAttachmentInitialSampleLocations : nativeptr<VkAttachmentSampleLocationsEXT>
              , postSubpassSampleLocationsCount : uint32
              , pPostSubpassSampleLocations : nativeptr<VkSubpassSampleLocationsEXT>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    attachmentInitialSampleLocationsCount = attachmentInitialSampleLocationsCount
                    pAttachmentInitialSampleLocations = pAttachmentInitialSampleLocations
                    postSubpassSampleLocationsCount = postSubpassSampleLocationsCount
                    pPostSubpassSampleLocations = pPostSubpassSampleLocations
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "attachmentInitialSampleLocationsCount = %A" x.attachmentInitialSampleLocationsCount
                    sprintf "pAttachmentInitialSampleLocations = %A" x.pAttachmentInitialSampleLocations
                    sprintf "postSubpassSampleLocationsCount = %A" x.postSubpassSampleLocationsCount
                    sprintf "pPostSubpassSampleLocations = %A" x.pPostSubpassSampleLocations
                ] |> sprintf "VkRenderPassSampleLocationsBeginInfoEXT { %s }"
        end
    
    
    type VkDynamicState with
         static member inline SampleLocationsExt = unbox<VkDynamicState> 1000143000
    type VkImageCreateFlags with
         static member inline SampleLocationsCompatibleDepthBitExt = unbox<VkImageCreateFlags> 4096
    type VkStructureType with
         static member inline SampleLocationsInfoExt = unbox<VkStructureType> 1000143000
         static member inline RenderPassSampleLocationsBeginInfoExt = unbox<VkStructureType> 1000143001
         static member inline PipelineSampleLocationsStateCreateInfoExt = unbox<VkStructureType> 1000143002
         static member inline PhysicalDeviceSampleLocationsPropertiesExt = unbox<VkStructureType> 1000143003
         static member inline MultisamplePropertiesExt = unbox<VkStructureType> 1000143004
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdSetSampleLocationsEXTDel = delegate of VkCommandBuffer * nativeptr<VkSampleLocationsInfoEXT> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceMultisamplePropertiesEXTDel = delegate of VkPhysicalDevice * VkSampleCountFlags * nativeptr<VkMultisamplePropertiesEXT> -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_EXT_sample_locations")
            static let s_vkCmdSetSampleLocationsEXTDel = VkRaw.vkImportInstanceDelegate<VkCmdSetSampleLocationsEXTDel> "vkCmdSetSampleLocationsEXT"
            static let s_vkGetPhysicalDeviceMultisamplePropertiesEXTDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceMultisamplePropertiesEXTDel> "vkGetPhysicalDeviceMultisamplePropertiesEXT"
            static do Report.End(3) |> ignore
            static member vkCmdSetSampleLocationsEXT = s_vkCmdSetSampleLocationsEXTDel
            static member vkGetPhysicalDeviceMultisamplePropertiesEXT = s_vkGetPhysicalDeviceMultisamplePropertiesEXTDel
        let vkCmdSetSampleLocationsEXT(commandBuffer : VkCommandBuffer, pSampleLocationsInfo : nativeptr<VkSampleLocationsInfoEXT>) = Loader<unit>.vkCmdSetSampleLocationsEXT.Invoke(commandBuffer, pSampleLocationsInfo)
        let vkGetPhysicalDeviceMultisamplePropertiesEXT(physicalDevice : VkPhysicalDevice, samples : VkSampleCountFlags, pMultisampleProperties : nativeptr<VkMultisamplePropertiesEXT>) = Loader<unit>.vkGetPhysicalDeviceMultisamplePropertiesEXT.Invoke(physicalDevice, samples, pMultisampleProperties)

module EXTSamplerFilterMinmax =
    let Name = "VK_EXT_sampler_filter_minmax"
    let Number = 131
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name ]
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    type VkSamplerReductionModeEXT = 
        | VkSamplerReductionModeWeightedAverageExt = 0
        | VkSamplerReductionModeMinExt = 1
        | VkSamplerReductionModeMaxExt = 2
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceSamplerFilterMinmaxPropertiesEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public filterMinmaxSingleComponentFormats : VkBool32
            val mutable public filterMinmaxImageComponentMapping : VkBool32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , filterMinmaxSingleComponentFormats : VkBool32
              , filterMinmaxImageComponentMapping : VkBool32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    filterMinmaxSingleComponentFormats = filterMinmaxSingleComponentFormats
                    filterMinmaxImageComponentMapping = filterMinmaxImageComponentMapping
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "filterMinmaxSingleComponentFormats = %A" x.filterMinmaxSingleComponentFormats
                    sprintf "filterMinmaxImageComponentMapping = %A" x.filterMinmaxImageComponentMapping
                ] |> sprintf "VkPhysicalDeviceSamplerFilterMinmaxPropertiesEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSamplerReductionModeCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public reductionMode : VkSamplerReductionModeEXT
    
            new(sType : VkStructureType
              , pNext : nativeint
              , reductionMode : VkSamplerReductionModeEXT
              ) =
                {
                    sType = sType
                    pNext = pNext
                    reductionMode = reductionMode
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "reductionMode = %A" x.reductionMode
                ] |> sprintf "VkSamplerReductionModeCreateInfoEXT { %s }"
        end
    
    
    type VkFormatFeatureFlags with
         static member inline SampledImageFilterMinmaxBitExt = unbox<VkFormatFeatureFlags> 65536
    type VkStructureType with
         static member inline PhysicalDeviceSamplerFilterMinmaxPropertiesExt = unbox<VkStructureType> 1000130000
         static member inline SamplerReductionModeCreateInfoExt = unbox<VkStructureType> 1000130001
    

module EXTShaderStencilExport =
    let Name = "VK_EXT_shader_stencil_export"
    let Number = 141
    
    open EXTDebugReport
    
    
    
    

module EXTShaderSubgroupBallot =
    let Name = "VK_EXT_shader_subgroup_ballot"
    let Number = 65
    
    open EXTDebugReport
    
    
    
    

module EXTShaderSubgroupVote =
    let Name = "VK_EXT_shader_subgroup_vote"
    let Number = 66
    
    open EXTDebugReport
    
    
    
    

module EXTShaderViewportIndexLayer =
    let Name = "VK_EXT_shader_viewport_index_layer"
    let Number = 163
    
    open EXTDebugReport
    
    
    
    

module EXTSwapchainColorspace =
    let Name = "VK_EXT_swapchain_colorspace"
    let Number = 105
    
    let Required = [ KHRSurface.Name ]
    open KHRSurface
    open EXTDebugReport
    
    
    
    type VkColorSpaceKHR with
         static member inline VkColorSpaceDisplayP3NonlinearExt = unbox<VkColorSpaceKHR> 1000104001
         static member inline VkColorSpaceExtendedSrgbLinearExt = unbox<VkColorSpaceKHR> 1000104002
         static member inline VkColorSpaceDciP3LinearExt = unbox<VkColorSpaceKHR> 1000104003
         static member inline VkColorSpaceDciP3NonlinearExt = unbox<VkColorSpaceKHR> 1000104004
         static member inline VkColorSpaceBt709LinearExt = unbox<VkColorSpaceKHR> 1000104005
         static member inline VkColorSpaceBt709NonlinearExt = unbox<VkColorSpaceKHR> 1000104006
         static member inline VkColorSpaceBt2020LinearExt = unbox<VkColorSpaceKHR> 1000104007
         static member inline VkColorSpaceHdr10St2084Ext = unbox<VkColorSpaceKHR> 1000104008
         static member inline VkColorSpaceDolbyvisionExt = unbox<VkColorSpaceKHR> 1000104009
         static member inline VkColorSpaceHdr10HlgExt = unbox<VkColorSpaceKHR> 1000104010
         static member inline VkColorSpaceAdobergbLinearExt = unbox<VkColorSpaceKHR> 1000104011
         static member inline VkColorSpaceAdobergbNonlinearExt = unbox<VkColorSpaceKHR> 1000104012
         static member inline VkColorSpacePassThroughExt = unbox<VkColorSpaceKHR> 1000104013
         static member inline VkColorSpaceExtendedSrgbNonlinearExt = unbox<VkColorSpaceKHR> 1000104014
    

module EXTValidationCache =
    let Name = "VK_EXT_validation_cache"
    let Number = 161
    
    open EXTDebugReport
    
    type VkValidationCacheHeaderVersionEXT = 
        | VkValidationCacheHeaderVersionOneExt = 1
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkShaderModuleValidationCacheCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public validationCache : VkValidationCacheEXT
    
            new(sType : VkStructureType
              , pNext : nativeint
              , validationCache : VkValidationCacheEXT
              ) =
                {
                    sType = sType
                    pNext = pNext
                    validationCache = validationCache
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "validationCache = %A" x.validationCache
                ] |> sprintf "VkShaderModuleValidationCacheCreateInfoEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkValidationCacheCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkValidationCacheCreateFlagsEXT
            val mutable public initialDataSize : uint64
            val mutable public pInitialData : nativeint
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkValidationCacheCreateFlagsEXT
              , initialDataSize : uint64
              , pInitialData : nativeint
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    initialDataSize = initialDataSize
                    pInitialData = pInitialData
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "initialDataSize = %A" x.initialDataSize
                    sprintf "pInitialData = %A" x.pInitialData
                ] |> sprintf "VkValidationCacheCreateInfoEXT { %s }"
        end
    
    
    type VkObjectType with
         static member inline ValidationCacheExt = unbox<VkObjectType> 1000160000
    type VkStructureType with
         static member inline ValidationCacheCreateInfoExt = unbox<VkStructureType> 1000160000
         static member inline ShaderModuleValidationCacheCreateInfoExt = unbox<VkStructureType> 1000160001
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateValidationCacheEXTDel = delegate of VkDevice * nativeptr<VkValidationCacheCreateInfoEXT> * nativeptr<VkAllocationCallbacks> * nativeptr<VkValidationCacheEXT> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkDestroyValidationCacheEXTDel = delegate of VkDevice * VkValidationCacheEXT * nativeptr<VkAllocationCallbacks> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkMergeValidationCachesEXTDel = delegate of VkDevice * VkValidationCacheEXT * uint32 * nativeptr<VkValidationCacheEXT> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetValidationCacheDataEXTDel = delegate of VkDevice * VkValidationCacheEXT * nativeptr<uint64> * nativeint -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_EXT_validation_cache")
            static let s_vkCreateValidationCacheEXTDel = VkRaw.vkImportInstanceDelegate<VkCreateValidationCacheEXTDel> "vkCreateValidationCacheEXT"
            static let s_vkDestroyValidationCacheEXTDel = VkRaw.vkImportInstanceDelegate<VkDestroyValidationCacheEXTDel> "vkDestroyValidationCacheEXT"
            static let s_vkMergeValidationCachesEXTDel = VkRaw.vkImportInstanceDelegate<VkMergeValidationCachesEXTDel> "vkMergeValidationCachesEXT"
            static let s_vkGetValidationCacheDataEXTDel = VkRaw.vkImportInstanceDelegate<VkGetValidationCacheDataEXTDel> "vkGetValidationCacheDataEXT"
            static do Report.End(3) |> ignore
            static member vkCreateValidationCacheEXT = s_vkCreateValidationCacheEXTDel
            static member vkDestroyValidationCacheEXT = s_vkDestroyValidationCacheEXTDel
            static member vkMergeValidationCachesEXT = s_vkMergeValidationCachesEXTDel
            static member vkGetValidationCacheDataEXT = s_vkGetValidationCacheDataEXTDel
        let vkCreateValidationCacheEXT(device : VkDevice, pCreateInfo : nativeptr<VkValidationCacheCreateInfoEXT>, pAllocator : nativeptr<VkAllocationCallbacks>, pValidationCache : nativeptr<VkValidationCacheEXT>) = Loader<unit>.vkCreateValidationCacheEXT.Invoke(device, pCreateInfo, pAllocator, pValidationCache)
        let vkDestroyValidationCacheEXT(device : VkDevice, validationCache : VkValidationCacheEXT, pAllocator : nativeptr<VkAllocationCallbacks>) = Loader<unit>.vkDestroyValidationCacheEXT.Invoke(device, validationCache, pAllocator)
        let vkMergeValidationCachesEXT(device : VkDevice, dstCache : VkValidationCacheEXT, srcCacheCount : uint32, pSrcCaches : nativeptr<VkValidationCacheEXT>) = Loader<unit>.vkMergeValidationCachesEXT.Invoke(device, dstCache, srcCacheCount, pSrcCaches)
        let vkGetValidationCacheDataEXT(device : VkDevice, validationCache : VkValidationCacheEXT, pDataSize : nativeptr<uint64>, pData : nativeint) = Loader<unit>.vkGetValidationCacheDataEXT.Invoke(device, validationCache, pDataSize, pData)

module EXTValidationFlags =
    let Name = "VK_EXT_validation_flags"
    let Number = 62
    
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkValidationFlagsEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public disabledValidationCheckCount : uint32
            val mutable public pDisabledValidationChecks : nativeptr<VkValidationCheckEXT>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , disabledValidationCheckCount : uint32
              , pDisabledValidationChecks : nativeptr<VkValidationCheckEXT>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    disabledValidationCheckCount = disabledValidationCheckCount
                    pDisabledValidationChecks = pDisabledValidationChecks
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "disabledValidationCheckCount = %A" x.disabledValidationCheckCount
                    sprintf "pDisabledValidationChecks = %A" x.pDisabledValidationChecks
                ] |> sprintf "VkValidationFlagsEXT { %s }"
        end
    
    
    type VkStructureType with
         static member inline ValidationFlagsExt = unbox<VkStructureType> 1000061000
    

module EXTVertexAttributeDivisor =
    let Name = "VK_EXT_vertex_attribute_divisor"
    let Number = 191
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name ]
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceVertexAttributeDivisorPropertiesEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public maxVertexAttribDivisor : uint32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , maxVertexAttribDivisor : uint32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    maxVertexAttribDivisor = maxVertexAttribDivisor
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "maxVertexAttribDivisor = %A" x.maxVertexAttribDivisor
                ] |> sprintf "VkPhysicalDeviceVertexAttributeDivisorPropertiesEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkVertexInputBindingDivisorDescriptionEXT = 
        struct
            val mutable public binding : uint32
            val mutable public divisor : uint32
    
            new(binding : uint32
              , divisor : uint32
              ) =
                {
                    binding = binding
                    divisor = divisor
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "binding = %A" x.binding
                    sprintf "divisor = %A" x.divisor
                ] |> sprintf "VkVertexInputBindingDivisorDescriptionEXT { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPipelineVertexInputDivisorStateCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public vertexBindingDivisorCount : uint32
            val mutable public pVertexBindingDivisors : nativeptr<VkVertexInputBindingDivisorDescriptionEXT>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , vertexBindingDivisorCount : uint32
              , pVertexBindingDivisors : nativeptr<VkVertexInputBindingDivisorDescriptionEXT>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    vertexBindingDivisorCount = vertexBindingDivisorCount
                    pVertexBindingDivisors = pVertexBindingDivisors
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "vertexBindingDivisorCount = %A" x.vertexBindingDivisorCount
                    sprintf "pVertexBindingDivisors = %A" x.pVertexBindingDivisors
                ] |> sprintf "VkPipelineVertexInputDivisorStateCreateInfoEXT { %s }"
        end
    
    
    type VkStructureType with
         static member inline PhysicalDeviceVertexAttributeDivisorPropertiesExt = unbox<VkStructureType> 1000190000
         static member inline PipelineVertexInputDivisorStateCreateInfoExt = unbox<VkStructureType> 1000190001
    

module GOOGLEDisplayTiming =
    let Name = "VK_GOOGLE_display_timing"
    let Number = 93
    
    let Required = [ KHRSurface.Name; KHRSwapchain.Name ]
    open KHRSurface
    open KHRSwapchain
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPastPresentationTimingGOOGLE = 
        struct
            val mutable public presentID : uint32
            val mutable public desiredPresentTime : uint64
            val mutable public actualPresentTime : uint64
            val mutable public earliestPresentTime : uint64
            val mutable public presentMargin : uint64
    
            new(presentID : uint32
              , desiredPresentTime : uint64
              , actualPresentTime : uint64
              , earliestPresentTime : uint64
              , presentMargin : uint64
              ) =
                {
                    presentID = presentID
                    desiredPresentTime = desiredPresentTime
                    actualPresentTime = actualPresentTime
                    earliestPresentTime = earliestPresentTime
                    presentMargin = presentMargin
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "presentID = %A" x.presentID
                    sprintf "desiredPresentTime = %A" x.desiredPresentTime
                    sprintf "actualPresentTime = %A" x.actualPresentTime
                    sprintf "earliestPresentTime = %A" x.earliestPresentTime
                    sprintf "presentMargin = %A" x.presentMargin
                ] |> sprintf "VkPastPresentationTimingGOOGLE { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPresentTimeGOOGLE = 
        struct
            val mutable public presentID : uint32
            val mutable public desiredPresentTime : uint64
    
            new(presentID : uint32
              , desiredPresentTime : uint64
              ) =
                {
                    presentID = presentID
                    desiredPresentTime = desiredPresentTime
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "presentID = %A" x.presentID
                    sprintf "desiredPresentTime = %A" x.desiredPresentTime
                ] |> sprintf "VkPresentTimeGOOGLE { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPresentTimesInfoGOOGLE = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public swapchainCount : uint32
            val mutable public pTimes : nativeptr<VkPresentTimeGOOGLE>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , swapchainCount : uint32
              , pTimes : nativeptr<VkPresentTimeGOOGLE>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    swapchainCount = swapchainCount
                    pTimes = pTimes
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "swapchainCount = %A" x.swapchainCount
                    sprintf "pTimes = %A" x.pTimes
                ] |> sprintf "VkPresentTimesInfoGOOGLE { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkRefreshCycleDurationGOOGLE = 
        struct
            val mutable public refreshDuration : uint64
    
            new(refreshDuration : uint64
              ) =
                {
                    refreshDuration = refreshDuration
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "refreshDuration = %A" x.refreshDuration
                ] |> sprintf "VkRefreshCycleDurationGOOGLE { %s }"
        end
    
    
    type VkStructureType with
         static member inline PresentTimesInfoGoogle = unbox<VkStructureType> 1000092000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetRefreshCycleDurationGOOGLEDel = delegate of VkDevice * VkSwapchainKHR * nativeptr<VkRefreshCycleDurationGOOGLE> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPastPresentationTimingGOOGLEDel = delegate of VkDevice * VkSwapchainKHR * nativeptr<uint32> * nativeptr<VkPastPresentationTimingGOOGLE> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_GOOGLE_display_timing")
            static let s_vkGetRefreshCycleDurationGOOGLEDel = VkRaw.vkImportInstanceDelegate<VkGetRefreshCycleDurationGOOGLEDel> "vkGetRefreshCycleDurationGOOGLE"
            static let s_vkGetPastPresentationTimingGOOGLEDel = VkRaw.vkImportInstanceDelegate<VkGetPastPresentationTimingGOOGLEDel> "vkGetPastPresentationTimingGOOGLE"
            static do Report.End(3) |> ignore
            static member vkGetRefreshCycleDurationGOOGLE = s_vkGetRefreshCycleDurationGOOGLEDel
            static member vkGetPastPresentationTimingGOOGLE = s_vkGetPastPresentationTimingGOOGLEDel
        let vkGetRefreshCycleDurationGOOGLE(device : VkDevice, swapchain : VkSwapchainKHR, pDisplayTimingProperties : nativeptr<VkRefreshCycleDurationGOOGLE>) = Loader<unit>.vkGetRefreshCycleDurationGOOGLE.Invoke(device, swapchain, pDisplayTimingProperties)
        let vkGetPastPresentationTimingGOOGLE(device : VkDevice, swapchain : VkSwapchainKHR, pPresentationTimingCount : nativeptr<uint32>, pPresentationTimings : nativeptr<VkPastPresentationTimingGOOGLE>) = Loader<unit>.vkGetPastPresentationTimingGOOGLE.Invoke(device, swapchain, pPresentationTimingCount, pPresentationTimings)

module IMGFilterCubic =
    let Name = "VK_IMG_filter_cubic"
    let Number = 16
    
    open EXTDebugReport
    
    
    
    type VkFilter with
         static member inline CubicImg = unbox<VkFilter> 1000015000
    type VkFormatFeatureFlags with
         static member inline SampledImageFilterCubicBitImg = unbox<VkFormatFeatureFlags> 8192
    

module IMGFormatPvrtc =
    let Name = "VK_IMG_format_pvrtc"
    let Number = 55
    
    open EXTDebugReport
    
    
    
    type VkFormat with
         static member inline Pvrtc12bppUnormBlockImg = unbox<VkFormat> 1000054000
         static member inline Pvrtc14bppUnormBlockImg = unbox<VkFormat> 1000054001
         static member inline Pvrtc22bppUnormBlockImg = unbox<VkFormat> 1000054002
         static member inline Pvrtc24bppUnormBlockImg = unbox<VkFormat> 1000054003
         static member inline Pvrtc12bppSrgbBlockImg = unbox<VkFormat> 1000054004
         static member inline Pvrtc14bppSrgbBlockImg = unbox<VkFormat> 1000054005
         static member inline Pvrtc22bppSrgbBlockImg = unbox<VkFormat> 1000054006
         static member inline Pvrtc24bppSrgbBlockImg = unbox<VkFormat> 1000054007
    

module KHRStorageBufferStorageClass =
    let Name = "VK_KHR_storage_buffer_storage_class"
    let Number = 132
    
    open EXTDebugReport
    
    
    
    

module KHR16bitStorage =
    let Name = "VK_KHR_16bit_storage"
    let Number = 84
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name; KHRStorageBufferStorageClass.Name ]
    open KHRGetPhysicalDeviceProperties2
    open KHRStorageBufferStorageClass
    open EXTDebugReport
    
    
    type VkPhysicalDevice16BitStorageFeaturesKHR = VkPhysicalDevice16BitStorageFeatures
    
    

module KHRAndroidSurface =
    let Name = "VK_KHR_android_surface"
    let Number = 9
    
    let Required = [ KHRSurface.Name ]
    open KHRSurface
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkAndroidSurfaceCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkAndroidSurfaceCreateFlagsKHR
            val mutable public window : nativeptr<nativeint>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkAndroidSurfaceCreateFlagsKHR
              , window : nativeptr<nativeint>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    window = window
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "window = %A" x.window
                ] |> sprintf "VkAndroidSurfaceCreateInfoKHR { %s }"
        end
    
    
    type VkStructureType with
         static member inline AndroidSurfaceCreateInfoKhr = unbox<VkStructureType> 1000008000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateAndroidSurfaceKHRDel = delegate of VkInstance * nativeptr<VkAndroidSurfaceCreateInfoKHR> * nativeptr<VkAllocationCallbacks> * nativeptr<VkSurfaceKHR> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_android_surface")
            static let s_vkCreateAndroidSurfaceKHRDel = VkRaw.vkImportInstanceDelegate<VkCreateAndroidSurfaceKHRDel> "vkCreateAndroidSurfaceKHR"
            static do Report.End(3) |> ignore
            static member vkCreateAndroidSurfaceKHR = s_vkCreateAndroidSurfaceKHRDel
        let vkCreateAndroidSurfaceKHR(instance : VkInstance, pCreateInfo : nativeptr<VkAndroidSurfaceCreateInfoKHR>, pAllocator : nativeptr<VkAllocationCallbacks>, pSurface : nativeptr<VkSurfaceKHR>) = Loader<unit>.vkCreateAndroidSurfaceKHR.Invoke(instance, pCreateInfo, pAllocator, pSurface)

module KHRDedicatedAllocation =
    let Name = "VK_KHR_dedicated_allocation"
    let Number = 128
    
    let Required = [ KHRGetMemoryRequirements2.Name ]
    open KHRGetMemoryRequirements2
    open EXTDebugReport
    
    
    type VkMemoryDedicatedAllocateInfoKHR = VkMemoryDedicatedAllocateInfo
    type VkMemoryDedicatedRequirementsKHR = VkMemoryDedicatedRequirements
    
    

module KHRDescriptorUpdateTemplate =
    let Name = "VK_KHR_descriptor_update_template"
    let Number = 86
    
    open EXTDebugReport
    
    
    type VkDescriptorUpdateTemplateCreateInfoKHR = VkDescriptorUpdateTemplateCreateInfo
    type VkDescriptorUpdateTemplateEntryKHR = VkDescriptorUpdateTemplateEntry
    
    
    
    module KHRPushDescriptor =
        open KHRGetPhysicalDeviceProperties2
        open EXTDebugReport
        
        
        
        
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module VkRaw =
            [<SuppressUnmanagedCodeSecurity>]
            type VkCmdPushDescriptorSetWithTemplateKHRDel = delegate of VkCommandBuffer * VkDescriptorUpdateTemplate * VkPipelineLayout * uint32 * nativeint -> unit
            
            [<AbstractClass; Sealed>]
            type private Loader<'d> private() =
                static do Report.Begin(3, "[Vulkan] loading VK_KHR_push_descriptor")
                static let s_vkCmdPushDescriptorSetWithTemplateKHRDel = VkRaw.vkImportInstanceDelegate<VkCmdPushDescriptorSetWithTemplateKHRDel> "vkCmdPushDescriptorSetWithTemplateKHR"
                static do Report.End(3) |> ignore
                static member vkCmdPushDescriptorSetWithTemplateKHR = s_vkCmdPushDescriptorSetWithTemplateKHRDel
            let vkCmdPushDescriptorSetWithTemplateKHR(commandBuffer : VkCommandBuffer, descriptorUpdateTemplate : VkDescriptorUpdateTemplate, layout : VkPipelineLayout, set : uint32, pData : nativeint) = Loader<unit>.vkCmdPushDescriptorSetWithTemplateKHR.Invoke(commandBuffer, descriptorUpdateTemplate, layout, set, pData)

module KHRDeviceGroupCreation =
    let Name = "VK_KHR_device_group_creation"
    let Number = 71
    
    open EXTDebugReport
    
    
    type VkDeviceGroupDeviceCreateInfoKHR = VkDeviceGroupDeviceCreateInfo
    type VkPhysicalDeviceGroupPropertiesKHR = VkPhysicalDeviceGroupProperties
    
    

module KHRDeviceGroup =
    let Name = "VK_KHR_device_group"
    let Number = 61
    
    let Required = [ KHRDeviceGroupCreation.Name ]
    open KHRDeviceGroupCreation
    open EXTDebugReport
    
    
    type VkDeviceGroupBindSparseInfoKHR = VkDeviceGroupBindSparseInfo
    type VkDeviceGroupCommandBufferBeginInfoKHR = VkDeviceGroupCommandBufferBeginInfo
    type VkDeviceGroupRenderPassBeginInfoKHR = VkDeviceGroupRenderPassBeginInfo
    type VkDeviceGroupSubmitInfoKHR = VkDeviceGroupSubmitInfo
    type VkMemoryAllocateFlagsInfoKHR = VkMemoryAllocateFlagsInfo
    
    
    
    module KHRBindMemory2 =
        open EXTDebugReport
        
        
        type VkBindBufferMemoryDeviceGroupInfoKHR = VkBindBufferMemoryDeviceGroupInfo
        type VkBindImageMemoryDeviceGroupInfoKHR = VkBindImageMemoryDeviceGroupInfo
        
        
    
    module KHRSurface =
        open EXTDebugReport
        
        [<Flags>]
        type VkDeviceGroupPresentModeFlagsKHR = 
            | None = 0
            | VkDeviceGroupPresentModeLocalBitKhr = 0x00000001
            | VkDeviceGroupPresentModeRemoteBitKhr = 0x00000002
            | VkDeviceGroupPresentModeSumBitKhr = 0x00000004
            | VkDeviceGroupPresentModeLocalMultiDeviceBitKhr = 0x00000008
        
        
        [<StructLayout(LayoutKind.Sequential)>]
        type VkDeviceGroupPresentCapabilitiesKHR = 
            struct
                val mutable public sType : VkStructureType
                val mutable public pNext : nativeint
                val mutable public presentMask : uint32_32
                val mutable public modes : VkDeviceGroupPresentModeFlagsKHR
        
                new(sType : VkStructureType
                  , pNext : nativeint
                  , presentMask : uint32_32
                  , modes : VkDeviceGroupPresentModeFlagsKHR
                  ) =
                    {
                        sType = sType
                        pNext = pNext
                        presentMask = presentMask
                        modes = modes
                    }
                override x.ToString() =
                    String.concat "; " [
                        sprintf "sType = %A" x.sType
                        sprintf "pNext = %A" x.pNext
                        sprintf "presentMask = %A" x.presentMask
                        sprintf "modes = %A" x.modes
                    ] |> sprintf "VkDeviceGroupPresentCapabilitiesKHR { %s }"
            end
        
        
        type VkStructureType with
             static member inline DeviceGroupPresentCapabilitiesKhr = unbox<VkStructureType> 999998007
        
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module VkRaw =
            [<SuppressUnmanagedCodeSecurity>]
            type VkGetDeviceGroupPresentCapabilitiesKHRDel = delegate of VkDevice * nativeptr<VkDeviceGroupPresentCapabilitiesKHR> -> VkResult
            [<SuppressUnmanagedCodeSecurity>]
            type VkGetDeviceGroupSurfacePresentModesKHRDel = delegate of VkDevice * VkSurfaceKHR * nativeptr<VkDeviceGroupPresentModeFlagsKHR> -> VkResult
            [<SuppressUnmanagedCodeSecurity>]
            type VkGetPhysicalDevicePresentRectanglesKHRDel = delegate of VkPhysicalDevice * VkSurfaceKHR * nativeptr<uint32> * nativeptr<VkRect2D> -> VkResult
            
            [<AbstractClass; Sealed>]
            type private Loader<'d> private() =
                static do Report.Begin(3, "[Vulkan] loading VK_KHR_surface")
                static let s_vkGetDeviceGroupPresentCapabilitiesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetDeviceGroupPresentCapabilitiesKHRDel> "vkGetDeviceGroupPresentCapabilitiesKHR"
                static let s_vkGetDeviceGroupSurfacePresentModesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetDeviceGroupSurfacePresentModesKHRDel> "vkGetDeviceGroupSurfacePresentModesKHR"
                static let s_vkGetPhysicalDevicePresentRectanglesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDevicePresentRectanglesKHRDel> "vkGetPhysicalDevicePresentRectanglesKHR"
                static do Report.End(3) |> ignore
                static member vkGetDeviceGroupPresentCapabilitiesKHR = s_vkGetDeviceGroupPresentCapabilitiesKHRDel
                static member vkGetDeviceGroupSurfacePresentModesKHR = s_vkGetDeviceGroupSurfacePresentModesKHRDel
                static member vkGetPhysicalDevicePresentRectanglesKHR = s_vkGetPhysicalDevicePresentRectanglesKHRDel
            let vkGetDeviceGroupPresentCapabilitiesKHR(device : VkDevice, pDeviceGroupPresentCapabilities : nativeptr<VkDeviceGroupPresentCapabilitiesKHR>) = Loader<unit>.vkGetDeviceGroupPresentCapabilitiesKHR.Invoke(device, pDeviceGroupPresentCapabilities)
            let vkGetDeviceGroupSurfacePresentModesKHR(device : VkDevice, surface : VkSurfaceKHR, pModes : nativeptr<VkDeviceGroupPresentModeFlagsKHR>) = Loader<unit>.vkGetDeviceGroupSurfacePresentModesKHR.Invoke(device, surface, pModes)
            let vkGetPhysicalDevicePresentRectanglesKHR(physicalDevice : VkPhysicalDevice, surface : VkSurfaceKHR, pRectCount : nativeptr<uint32>, pRects : nativeptr<VkRect2D>) = Loader<unit>.vkGetPhysicalDevicePresentRectanglesKHR.Invoke(physicalDevice, surface, pRectCount, pRects)
    
    module KHRSwapchain =
        open KHRSurface
        open EXTDebugReport
        
        
        [<StructLayout(LayoutKind.Sequential)>]
        type VkAcquireNextImageInfoKHR = 
            struct
                val mutable public sType : VkStructureType
                val mutable public pNext : nativeint
                val mutable public swapchain : VkSwapchainKHR
                val mutable public timeout : uint64
                val mutable public semaphore : VkSemaphore
                val mutable public fence : VkFence
                val mutable public deviceMask : uint32
        
                new(sType : VkStructureType
                  , pNext : nativeint
                  , swapchain : VkSwapchainKHR
                  , timeout : uint64
                  , semaphore : VkSemaphore
                  , fence : VkFence
                  , deviceMask : uint32
                  ) =
                    {
                        sType = sType
                        pNext = pNext
                        swapchain = swapchain
                        timeout = timeout
                        semaphore = semaphore
                        fence = fence
                        deviceMask = deviceMask
                    }
                override x.ToString() =
                    String.concat "; " [
                        sprintf "sType = %A" x.sType
                        sprintf "pNext = %A" x.pNext
                        sprintf "swapchain = %A" x.swapchain
                        sprintf "timeout = %A" x.timeout
                        sprintf "semaphore = %A" x.semaphore
                        sprintf "fence = %A" x.fence
                        sprintf "deviceMask = %A" x.deviceMask
                    ] |> sprintf "VkAcquireNextImageInfoKHR { %s }"
            end
        
        [<StructLayout(LayoutKind.Sequential)>]
        type VkBindImageMemorySwapchainInfoKHR = 
            struct
                val mutable public sType : VkStructureType
                val mutable public pNext : nativeint
                val mutable public swapchain : VkSwapchainKHR
                val mutable public imageIndex : uint32
        
                new(sType : VkStructureType
                  , pNext : nativeint
                  , swapchain : VkSwapchainKHR
                  , imageIndex : uint32
                  ) =
                    {
                        sType = sType
                        pNext = pNext
                        swapchain = swapchain
                        imageIndex = imageIndex
                    }
                override x.ToString() =
                    String.concat "; " [
                        sprintf "sType = %A" x.sType
                        sprintf "pNext = %A" x.pNext
                        sprintf "swapchain = %A" x.swapchain
                        sprintf "imageIndex = %A" x.imageIndex
                    ] |> sprintf "VkBindImageMemorySwapchainInfoKHR { %s }"
            end
        
        [<StructLayout(LayoutKind.Sequential)>]
        type VkDeviceGroupPresentInfoKHR = 
            struct
                val mutable public sType : VkStructureType
                val mutable public pNext : nativeint
                val mutable public swapchainCount : uint32
                val mutable public pDeviceMasks : nativeptr<uint32>
                val mutable public mode : VkDeviceGroupPresentModeFlagsKHR
        
                new(sType : VkStructureType
                  , pNext : nativeint
                  , swapchainCount : uint32
                  , pDeviceMasks : nativeptr<uint32>
                  , mode : VkDeviceGroupPresentModeFlagsKHR
                  ) =
                    {
                        sType = sType
                        pNext = pNext
                        swapchainCount = swapchainCount
                        pDeviceMasks = pDeviceMasks
                        mode = mode
                    }
                override x.ToString() =
                    String.concat "; " [
                        sprintf "sType = %A" x.sType
                        sprintf "pNext = %A" x.pNext
                        sprintf "swapchainCount = %A" x.swapchainCount
                        sprintf "pDeviceMasks = %A" x.pDeviceMasks
                        sprintf "mode = %A" x.mode
                    ] |> sprintf "VkDeviceGroupPresentInfoKHR { %s }"
            end
        
        [<StructLayout(LayoutKind.Sequential)>]
        type VkDeviceGroupSwapchainCreateInfoKHR = 
            struct
                val mutable public sType : VkStructureType
                val mutable public pNext : nativeint
                val mutable public modes : VkDeviceGroupPresentModeFlagsKHR
        
                new(sType : VkStructureType
                  , pNext : nativeint
                  , modes : VkDeviceGroupPresentModeFlagsKHR
                  ) =
                    {
                        sType = sType
                        pNext = pNext
                        modes = modes
                    }
                override x.ToString() =
                    String.concat "; " [
                        sprintf "sType = %A" x.sType
                        sprintf "pNext = %A" x.pNext
                        sprintf "modes = %A" x.modes
                    ] |> sprintf "VkDeviceGroupSwapchainCreateInfoKHR { %s }"
            end
        
        [<StructLayout(LayoutKind.Sequential)>]
        type VkImageSwapchainCreateInfoKHR = 
            struct
                val mutable public sType : VkStructureType
                val mutable public pNext : nativeint
                val mutable public swapchain : VkSwapchainKHR
        
                new(sType : VkStructureType
                  , pNext : nativeint
                  , swapchain : VkSwapchainKHR
                  ) =
                    {
                        sType = sType
                        pNext = pNext
                        swapchain = swapchain
                    }
                override x.ToString() =
                    String.concat "; " [
                        sprintf "sType = %A" x.sType
                        sprintf "pNext = %A" x.pNext
                        sprintf "swapchain = %A" x.swapchain
                    ] |> sprintf "VkImageSwapchainCreateInfoKHR { %s }"
            end
        
        
        type VkStructureType with
             static member inline ImageSwapchainCreateInfoKhr = unbox<VkStructureType> 999998008
             static member inline BindImageMemorySwapchainInfoKhr = unbox<VkStructureType> 999998009
             static member inline AcquireNextImageInfoKhr = unbox<VkStructureType> 999998010
             static member inline DeviceGroupPresentInfoKhr = unbox<VkStructureType> 999998011
             static member inline DeviceGroupSwapchainCreateInfoKhr = unbox<VkStructureType> 999998012
        type VkSwapchainCreateFlagsKHR with
             static member inline VkSwapchainCreateSplitInstanceBindRegionsBitKhr = unbox<VkSwapchainCreateFlagsKHR> 1
        
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module VkRaw =
            [<SuppressUnmanagedCodeSecurity>]
            type VkAcquireNextImage2KHRDel = delegate of VkDevice * nativeptr<VkAcquireNextImageInfoKHR> * nativeptr<uint32> -> VkResult
            
            [<AbstractClass; Sealed>]
            type private Loader<'d> private() =
                static do Report.Begin(3, "[Vulkan] loading VK_KHR_swapchain")
                static let s_vkAcquireNextImage2KHRDel = VkRaw.vkImportInstanceDelegate<VkAcquireNextImage2KHRDel> "vkAcquireNextImage2KHR"
                static do Report.End(3) |> ignore
                static member vkAcquireNextImage2KHR = s_vkAcquireNextImage2KHRDel
            let vkAcquireNextImage2KHR(device : VkDevice, pAcquireInfo : nativeptr<VkAcquireNextImageInfoKHR>, pImageIndex : nativeptr<uint32>) = Loader<unit>.vkAcquireNextImage2KHR.Invoke(device, pAcquireInfo, pImageIndex)

module KHRDisplaySwapchain =
    let Name = "VK_KHR_display_swapchain"
    let Number = 4
    
    let Required = [ KHRDisplay.Name; KHRSurface.Name; KHRSwapchain.Name ]
    open KHRDisplay
    open KHRSurface
    open KHRSwapchain
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDisplayPresentInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public srcRect : VkRect2D
            val mutable public dstRect : VkRect2D
            val mutable public persistent : VkBool32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , srcRect : VkRect2D
              , dstRect : VkRect2D
              , persistent : VkBool32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    srcRect = srcRect
                    dstRect = dstRect
                    persistent = persistent
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "srcRect = %A" x.srcRect
                    sprintf "dstRect = %A" x.dstRect
                    sprintf "persistent = %A" x.persistent
                ] |> sprintf "VkDisplayPresentInfoKHR { %s }"
        end
    
    
    type VkResult with
         static member inline VkErrorIncompatibleDisplayKhr = unbox<VkResult> -1000003001
    type VkStructureType with
         static member inline DisplayPresentInfoKhr = unbox<VkStructureType> 1000003000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateSharedSwapchainsKHRDel = delegate of VkDevice * uint32 * nativeptr<VkSwapchainCreateInfoKHR> * nativeptr<VkAllocationCallbacks> * nativeptr<VkSwapchainKHR> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_display_swapchain")
            static let s_vkCreateSharedSwapchainsKHRDel = VkRaw.vkImportInstanceDelegate<VkCreateSharedSwapchainsKHRDel> "vkCreateSharedSwapchainsKHR"
            static do Report.End(3) |> ignore
            static member vkCreateSharedSwapchainsKHR = s_vkCreateSharedSwapchainsKHRDel
        let vkCreateSharedSwapchainsKHR(device : VkDevice, swapchainCount : uint32, pCreateInfos : nativeptr<VkSwapchainCreateInfoKHR>, pAllocator : nativeptr<VkAllocationCallbacks>, pSwapchains : nativeptr<VkSwapchainKHR>) = Loader<unit>.vkCreateSharedSwapchainsKHR.Invoke(device, swapchainCount, pCreateInfos, pAllocator, pSwapchains)

module KHRExternalFenceCapabilities =
    let Name = "VK_KHR_external_fence_capabilities"
    let Number = 113
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name ]
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    type VkExternalFencePropertiesKHR = VkExternalFenceProperties
    type VkPhysicalDeviceExternalFenceInfoKHR = VkPhysicalDeviceExternalFenceInfo
    
    

module KHRExternalFence =
    let Name = "VK_KHR_external_fence"
    let Number = 114
    
    let Required = [ KHRExternalFenceCapabilities.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalFenceCapabilities
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    type VkExportFenceCreateInfoKHR = VkExportFenceCreateInfo
    
    

module KHRExternalFenceFd =
    let Name = "VK_KHR_external_fence_fd"
    let Number = 116
    
    let Required = [ KHRExternalFence.Name; KHRExternalFenceCapabilities.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalFence
    open KHRExternalFenceCapabilities
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkFenceGetFdInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public fence : VkFence
            val mutable public handleType : VkExternalFenceHandleTypeFlags
    
            new(sType : VkStructureType
              , pNext : nativeint
              , fence : VkFence
              , handleType : VkExternalFenceHandleTypeFlags
              ) =
                {
                    sType = sType
                    pNext = pNext
                    fence = fence
                    handleType = handleType
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "fence = %A" x.fence
                    sprintf "handleType = %A" x.handleType
                ] |> sprintf "VkFenceGetFdInfoKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImportFenceFdInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public fence : VkFence
            val mutable public flags : VkFenceImportFlags
            val mutable public handleType : VkExternalFenceHandleTypeFlags
            val mutable public fd : int
    
            new(sType : VkStructureType
              , pNext : nativeint
              , fence : VkFence
              , flags : VkFenceImportFlags
              , handleType : VkExternalFenceHandleTypeFlags
              , fd : int
              ) =
                {
                    sType = sType
                    pNext = pNext
                    fence = fence
                    flags = flags
                    handleType = handleType
                    fd = fd
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "fence = %A" x.fence
                    sprintf "flags = %A" x.flags
                    sprintf "handleType = %A" x.handleType
                    sprintf "fd = %A" x.fd
                ] |> sprintf "VkImportFenceFdInfoKHR { %s }"
        end
    
    
    type VkStructureType with
         static member inline ImportFenceFdInfoKhr = unbox<VkStructureType> 1000115000
         static member inline FenceGetFdInfoKhr = unbox<VkStructureType> 1000115001
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkImportFenceFdKHRDel = delegate of VkDevice * nativeptr<VkImportFenceFdInfoKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetFenceFdKHRDel = delegate of VkDevice * nativeptr<VkFenceGetFdInfoKHR> * nativeptr<int> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_external_fence_fd")
            static let s_vkImportFenceFdKHRDel = VkRaw.vkImportInstanceDelegate<VkImportFenceFdKHRDel> "vkImportFenceFdKHR"
            static let s_vkGetFenceFdKHRDel = VkRaw.vkImportInstanceDelegate<VkGetFenceFdKHRDel> "vkGetFenceFdKHR"
            static do Report.End(3) |> ignore
            static member vkImportFenceFdKHR = s_vkImportFenceFdKHRDel
            static member vkGetFenceFdKHR = s_vkGetFenceFdKHRDel
        let vkImportFenceFdKHR(device : VkDevice, pImportFenceFdInfo : nativeptr<VkImportFenceFdInfoKHR>) = Loader<unit>.vkImportFenceFdKHR.Invoke(device, pImportFenceFdInfo)
        let vkGetFenceFdKHR(device : VkDevice, pGetFdInfo : nativeptr<VkFenceGetFdInfoKHR>, pFd : nativeptr<int>) = Loader<unit>.vkGetFenceFdKHR.Invoke(device, pGetFdInfo, pFd)

module KHRExternalFenceWin32 =
    let Name = "VK_KHR_external_fence_win32"
    let Number = 115
    
    let Required = [ KHRExternalFence.Name; KHRExternalFenceCapabilities.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalFence
    open KHRExternalFenceCapabilities
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExportFenceWin32HandleInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public pAttributes : nativeptr<nativeint>
            val mutable public dwAccess : uint32
            val mutable public name : cstr
    
            new(sType : VkStructureType
              , pNext : nativeint
              , pAttributes : nativeptr<nativeint>
              , dwAccess : uint32
              , name : cstr
              ) =
                {
                    sType = sType
                    pNext = pNext
                    pAttributes = pAttributes
                    dwAccess = dwAccess
                    name = name
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "pAttributes = %A" x.pAttributes
                    sprintf "dwAccess = %A" x.dwAccess
                    sprintf "name = %A" x.name
                ] |> sprintf "VkExportFenceWin32HandleInfoKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkFenceGetWin32HandleInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public fence : VkFence
            val mutable public handleType : VkExternalFenceHandleTypeFlags
    
            new(sType : VkStructureType
              , pNext : nativeint
              , fence : VkFence
              , handleType : VkExternalFenceHandleTypeFlags
              ) =
                {
                    sType = sType
                    pNext = pNext
                    fence = fence
                    handleType = handleType
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "fence = %A" x.fence
                    sprintf "handleType = %A" x.handleType
                ] |> sprintf "VkFenceGetWin32HandleInfoKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImportFenceWin32HandleInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public fence : VkFence
            val mutable public flags : VkFenceImportFlags
            val mutable public handleType : VkExternalFenceHandleTypeFlags
            val mutable public handle : nativeint
            val mutable public name : cstr
    
            new(sType : VkStructureType
              , pNext : nativeint
              , fence : VkFence
              , flags : VkFenceImportFlags
              , handleType : VkExternalFenceHandleTypeFlags
              , handle : nativeint
              , name : cstr
              ) =
                {
                    sType = sType
                    pNext = pNext
                    fence = fence
                    flags = flags
                    handleType = handleType
                    handle = handle
                    name = name
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "fence = %A" x.fence
                    sprintf "flags = %A" x.flags
                    sprintf "handleType = %A" x.handleType
                    sprintf "handle = %A" x.handle
                    sprintf "name = %A" x.name
                ] |> sprintf "VkImportFenceWin32HandleInfoKHR { %s }"
        end
    
    
    type VkStructureType with
         static member inline ImportFenceWin32HandleInfoKhr = unbox<VkStructureType> 1000114000
         static member inline ExportFenceWin32HandleInfoKhr = unbox<VkStructureType> 1000114001
         static member inline FenceGetWin32HandleInfoKhr = unbox<VkStructureType> 1000114002
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkImportFenceWin32HandleKHRDel = delegate of VkDevice * nativeptr<VkImportFenceWin32HandleInfoKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetFenceWin32HandleKHRDel = delegate of VkDevice * nativeptr<VkFenceGetWin32HandleInfoKHR> * nativeptr<nativeint> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_external_fence_win32")
            static let s_vkImportFenceWin32HandleKHRDel = VkRaw.vkImportInstanceDelegate<VkImportFenceWin32HandleKHRDel> "vkImportFenceWin32HandleKHR"
            static let s_vkGetFenceWin32HandleKHRDel = VkRaw.vkImportInstanceDelegate<VkGetFenceWin32HandleKHRDel> "vkGetFenceWin32HandleKHR"
            static do Report.End(3) |> ignore
            static member vkImportFenceWin32HandleKHR = s_vkImportFenceWin32HandleKHRDel
            static member vkGetFenceWin32HandleKHR = s_vkGetFenceWin32HandleKHRDel
        let vkImportFenceWin32HandleKHR(device : VkDevice, pImportFenceWin32HandleInfo : nativeptr<VkImportFenceWin32HandleInfoKHR>) = Loader<unit>.vkImportFenceWin32HandleKHR.Invoke(device, pImportFenceWin32HandleInfo)
        let vkGetFenceWin32HandleKHR(device : VkDevice, pGetWin32HandleInfo : nativeptr<VkFenceGetWin32HandleInfoKHR>, pHandle : nativeptr<nativeint>) = Loader<unit>.vkGetFenceWin32HandleKHR.Invoke(device, pGetWin32HandleInfo, pHandle)

module KHRExternalMemoryWin32 =
    let Name = "VK_KHR_external_memory_win32"
    let Number = 74
    
    let Required = [ KHRExternalMemory.Name; KHRExternalMemoryCapabilities.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalMemory
    open KHRExternalMemoryCapabilities
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExportMemoryWin32HandleInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public pAttributes : nativeptr<nativeint>
            val mutable public dwAccess : uint32
            val mutable public name : cstr
    
            new(sType : VkStructureType
              , pNext : nativeint
              , pAttributes : nativeptr<nativeint>
              , dwAccess : uint32
              , name : cstr
              ) =
                {
                    sType = sType
                    pNext = pNext
                    pAttributes = pAttributes
                    dwAccess = dwAccess
                    name = name
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "pAttributes = %A" x.pAttributes
                    sprintf "dwAccess = %A" x.dwAccess
                    sprintf "name = %A" x.name
                ] |> sprintf "VkExportMemoryWin32HandleInfoKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImportMemoryWin32HandleInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleType : VkExternalMemoryHandleTypeFlags
            val mutable public handle : nativeint
            val mutable public name : cstr
    
            new(sType : VkStructureType
              , pNext : nativeint
              , handleType : VkExternalMemoryHandleTypeFlags
              , handle : nativeint
              , name : cstr
              ) =
                {
                    sType = sType
                    pNext = pNext
                    handleType = handleType
                    handle = handle
                    name = name
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "handleType = %A" x.handleType
                    sprintf "handle = %A" x.handle
                    sprintf "name = %A" x.name
                ] |> sprintf "VkImportMemoryWin32HandleInfoKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMemoryGetWin32HandleInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public memory : VkDeviceMemory
            val mutable public handleType : VkExternalMemoryHandleTypeFlags
    
            new(sType : VkStructureType
              , pNext : nativeint
              , memory : VkDeviceMemory
              , handleType : VkExternalMemoryHandleTypeFlags
              ) =
                {
                    sType = sType
                    pNext = pNext
                    memory = memory
                    handleType = handleType
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "memory = %A" x.memory
                    sprintf "handleType = %A" x.handleType
                ] |> sprintf "VkMemoryGetWin32HandleInfoKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMemoryWin32HandlePropertiesKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public memoryTypeBits : uint32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , memoryTypeBits : uint32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    memoryTypeBits = memoryTypeBits
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "memoryTypeBits = %A" x.memoryTypeBits
                ] |> sprintf "VkMemoryWin32HandlePropertiesKHR { %s }"
        end
    
    
    type VkStructureType with
         static member inline ImportMemoryWin32HandleInfoKhr = unbox<VkStructureType> 1000073000
         static member inline ExportMemoryWin32HandleInfoKhr = unbox<VkStructureType> 1000073001
         static member inline MemoryWin32HandlePropertiesKhr = unbox<VkStructureType> 1000073002
         static member inline MemoryGetWin32HandleInfoKhr = unbox<VkStructureType> 1000073003
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetMemoryWin32HandleKHRDel = delegate of VkDevice * nativeptr<VkMemoryGetWin32HandleInfoKHR> * nativeptr<nativeint> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetMemoryWin32HandlePropertiesKHRDel = delegate of VkDevice * VkExternalMemoryHandleTypeFlags * nativeint * nativeptr<VkMemoryWin32HandlePropertiesKHR> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_external_memory_win32")
            static let s_vkGetMemoryWin32HandleKHRDel = VkRaw.vkImportInstanceDelegate<VkGetMemoryWin32HandleKHRDel> "vkGetMemoryWin32HandleKHR"
            static let s_vkGetMemoryWin32HandlePropertiesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetMemoryWin32HandlePropertiesKHRDel> "vkGetMemoryWin32HandlePropertiesKHR"
            static do Report.End(3) |> ignore
            static member vkGetMemoryWin32HandleKHR = s_vkGetMemoryWin32HandleKHRDel
            static member vkGetMemoryWin32HandlePropertiesKHR = s_vkGetMemoryWin32HandlePropertiesKHRDel
        let vkGetMemoryWin32HandleKHR(device : VkDevice, pGetWin32HandleInfo : nativeptr<VkMemoryGetWin32HandleInfoKHR>, pHandle : nativeptr<nativeint>) = Loader<unit>.vkGetMemoryWin32HandleKHR.Invoke(device, pGetWin32HandleInfo, pHandle)
        let vkGetMemoryWin32HandlePropertiesKHR(device : VkDevice, handleType : VkExternalMemoryHandleTypeFlags, handle : nativeint, pMemoryWin32HandleProperties : nativeptr<VkMemoryWin32HandlePropertiesKHR>) = Loader<unit>.vkGetMemoryWin32HandlePropertiesKHR.Invoke(device, handleType, handle, pMemoryWin32HandleProperties)

module KHRExternalSemaphoreCapabilities =
    let Name = "VK_KHR_external_semaphore_capabilities"
    let Number = 77
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name ]
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    type VkExternalSemaphorePropertiesKHR = VkExternalSemaphoreProperties
    type VkPhysicalDeviceExternalSemaphoreInfoKHR = VkPhysicalDeviceExternalSemaphoreInfo
    
    

module KHRExternalSemaphore =
    let Name = "VK_KHR_external_semaphore"
    let Number = 78
    
    let Required = [ KHRExternalSemaphoreCapabilities.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalSemaphoreCapabilities
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    type VkExportSemaphoreCreateInfoKHR = VkExportSemaphoreCreateInfo
    
    

module KHRExternalSemaphoreFd =
    let Name = "VK_KHR_external_semaphore_fd"
    let Number = 80
    
    let Required = [ KHRExternalSemaphore.Name; KHRExternalSemaphoreCapabilities.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalSemaphore
    open KHRExternalSemaphoreCapabilities
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImportSemaphoreFdInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public semaphore : VkSemaphore
            val mutable public flags : VkSemaphoreImportFlags
            val mutable public handleType : VkExternalSemaphoreHandleTypeFlags
            val mutable public fd : int
    
            new(sType : VkStructureType
              , pNext : nativeint
              , semaphore : VkSemaphore
              , flags : VkSemaphoreImportFlags
              , handleType : VkExternalSemaphoreHandleTypeFlags
              , fd : int
              ) =
                {
                    sType = sType
                    pNext = pNext
                    semaphore = semaphore
                    flags = flags
                    handleType = handleType
                    fd = fd
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "semaphore = %A" x.semaphore
                    sprintf "flags = %A" x.flags
                    sprintf "handleType = %A" x.handleType
                    sprintf "fd = %A" x.fd
                ] |> sprintf "VkImportSemaphoreFdInfoKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSemaphoreGetFdInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public semaphore : VkSemaphore
            val mutable public handleType : VkExternalSemaphoreHandleTypeFlags
    
            new(sType : VkStructureType
              , pNext : nativeint
              , semaphore : VkSemaphore
              , handleType : VkExternalSemaphoreHandleTypeFlags
              ) =
                {
                    sType = sType
                    pNext = pNext
                    semaphore = semaphore
                    handleType = handleType
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "semaphore = %A" x.semaphore
                    sprintf "handleType = %A" x.handleType
                ] |> sprintf "VkSemaphoreGetFdInfoKHR { %s }"
        end
    
    
    type VkStructureType with
         static member inline ImportSemaphoreFdInfoKhr = unbox<VkStructureType> 1000079000
         static member inline SemaphoreGetFdInfoKhr = unbox<VkStructureType> 1000079001
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkImportSemaphoreFdKHRDel = delegate of VkDevice * nativeptr<VkImportSemaphoreFdInfoKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetSemaphoreFdKHRDel = delegate of VkDevice * nativeptr<VkSemaphoreGetFdInfoKHR> * nativeptr<int> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_external_semaphore_fd")
            static let s_vkImportSemaphoreFdKHRDel = VkRaw.vkImportInstanceDelegate<VkImportSemaphoreFdKHRDel> "vkImportSemaphoreFdKHR"
            static let s_vkGetSemaphoreFdKHRDel = VkRaw.vkImportInstanceDelegate<VkGetSemaphoreFdKHRDel> "vkGetSemaphoreFdKHR"
            static do Report.End(3) |> ignore
            static member vkImportSemaphoreFdKHR = s_vkImportSemaphoreFdKHRDel
            static member vkGetSemaphoreFdKHR = s_vkGetSemaphoreFdKHRDel
        let vkImportSemaphoreFdKHR(device : VkDevice, pImportSemaphoreFdInfo : nativeptr<VkImportSemaphoreFdInfoKHR>) = Loader<unit>.vkImportSemaphoreFdKHR.Invoke(device, pImportSemaphoreFdInfo)
        let vkGetSemaphoreFdKHR(device : VkDevice, pGetFdInfo : nativeptr<VkSemaphoreGetFdInfoKHR>, pFd : nativeptr<int>) = Loader<unit>.vkGetSemaphoreFdKHR.Invoke(device, pGetFdInfo, pFd)

module KHRExternalSemaphoreWin32 =
    let Name = "VK_KHR_external_semaphore_win32"
    let Number = 79
    
    let Required = [ KHRExternalSemaphore.Name; KHRExternalSemaphoreCapabilities.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalSemaphore
    open KHRExternalSemaphoreCapabilities
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkD3D12FenceSubmitInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public waitSemaphoreValuesCount : uint32
            val mutable public pWaitSemaphoreValues : nativeptr<uint64>
            val mutable public signalSemaphoreValuesCount : uint32
            val mutable public pSignalSemaphoreValues : nativeptr<uint64>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , waitSemaphoreValuesCount : uint32
              , pWaitSemaphoreValues : nativeptr<uint64>
              , signalSemaphoreValuesCount : uint32
              , pSignalSemaphoreValues : nativeptr<uint64>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    waitSemaphoreValuesCount = waitSemaphoreValuesCount
                    pWaitSemaphoreValues = pWaitSemaphoreValues
                    signalSemaphoreValuesCount = signalSemaphoreValuesCount
                    pSignalSemaphoreValues = pSignalSemaphoreValues
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "waitSemaphoreValuesCount = %A" x.waitSemaphoreValuesCount
                    sprintf "pWaitSemaphoreValues = %A" x.pWaitSemaphoreValues
                    sprintf "signalSemaphoreValuesCount = %A" x.signalSemaphoreValuesCount
                    sprintf "pSignalSemaphoreValues = %A" x.pSignalSemaphoreValues
                ] |> sprintf "VkD3D12FenceSubmitInfoKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExportSemaphoreWin32HandleInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public pAttributes : nativeptr<nativeint>
            val mutable public dwAccess : uint32
            val mutable public name : cstr
    
            new(sType : VkStructureType
              , pNext : nativeint
              , pAttributes : nativeptr<nativeint>
              , dwAccess : uint32
              , name : cstr
              ) =
                {
                    sType = sType
                    pNext = pNext
                    pAttributes = pAttributes
                    dwAccess = dwAccess
                    name = name
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "pAttributes = %A" x.pAttributes
                    sprintf "dwAccess = %A" x.dwAccess
                    sprintf "name = %A" x.name
                ] |> sprintf "VkExportSemaphoreWin32HandleInfoKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImportSemaphoreWin32HandleInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public semaphore : VkSemaphore
            val mutable public flags : VkSemaphoreImportFlags
            val mutable public handleType : VkExternalSemaphoreHandleTypeFlags
            val mutable public handle : nativeint
            val mutable public name : cstr
    
            new(sType : VkStructureType
              , pNext : nativeint
              , semaphore : VkSemaphore
              , flags : VkSemaphoreImportFlags
              , handleType : VkExternalSemaphoreHandleTypeFlags
              , handle : nativeint
              , name : cstr
              ) =
                {
                    sType = sType
                    pNext = pNext
                    semaphore = semaphore
                    flags = flags
                    handleType = handleType
                    handle = handle
                    name = name
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "semaphore = %A" x.semaphore
                    sprintf "flags = %A" x.flags
                    sprintf "handleType = %A" x.handleType
                    sprintf "handle = %A" x.handle
                    sprintf "name = %A" x.name
                ] |> sprintf "VkImportSemaphoreWin32HandleInfoKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSemaphoreGetWin32HandleInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public semaphore : VkSemaphore
            val mutable public handleType : VkExternalSemaphoreHandleTypeFlags
    
            new(sType : VkStructureType
              , pNext : nativeint
              , semaphore : VkSemaphore
              , handleType : VkExternalSemaphoreHandleTypeFlags
              ) =
                {
                    sType = sType
                    pNext = pNext
                    semaphore = semaphore
                    handleType = handleType
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "semaphore = %A" x.semaphore
                    sprintf "handleType = %A" x.handleType
                ] |> sprintf "VkSemaphoreGetWin32HandleInfoKHR { %s }"
        end
    
    
    type VkStructureType with
         static member inline ImportSemaphoreWin32HandleInfoKhr = unbox<VkStructureType> 1000078000
         static member inline ExportSemaphoreWin32HandleInfoKhr = unbox<VkStructureType> 1000078001
         static member inline D3d12FenceSubmitInfoKhr = unbox<VkStructureType> 1000078002
         static member inline SemaphoreGetWin32HandleInfoKhr = unbox<VkStructureType> 1000078003
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkImportSemaphoreWin32HandleKHRDel = delegate of VkDevice * nativeptr<VkImportSemaphoreWin32HandleInfoKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetSemaphoreWin32HandleKHRDel = delegate of VkDevice * nativeptr<VkSemaphoreGetWin32HandleInfoKHR> * nativeptr<nativeint> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_external_semaphore_win32")
            static let s_vkImportSemaphoreWin32HandleKHRDel = VkRaw.vkImportInstanceDelegate<VkImportSemaphoreWin32HandleKHRDel> "vkImportSemaphoreWin32HandleKHR"
            static let s_vkGetSemaphoreWin32HandleKHRDel = VkRaw.vkImportInstanceDelegate<VkGetSemaphoreWin32HandleKHRDel> "vkGetSemaphoreWin32HandleKHR"
            static do Report.End(3) |> ignore
            static member vkImportSemaphoreWin32HandleKHR = s_vkImportSemaphoreWin32HandleKHRDel
            static member vkGetSemaphoreWin32HandleKHR = s_vkGetSemaphoreWin32HandleKHRDel
        let vkImportSemaphoreWin32HandleKHR(device : VkDevice, pImportSemaphoreWin32HandleInfo : nativeptr<VkImportSemaphoreWin32HandleInfoKHR>) = Loader<unit>.vkImportSemaphoreWin32HandleKHR.Invoke(device, pImportSemaphoreWin32HandleInfo)
        let vkGetSemaphoreWin32HandleKHR(device : VkDevice, pGetWin32HandleInfo : nativeptr<VkSemaphoreGetWin32HandleInfoKHR>, pHandle : nativeptr<nativeint>) = Loader<unit>.vkGetSemaphoreWin32HandleKHR.Invoke(device, pGetWin32HandleInfo, pHandle)

module KHRGetSurfaceCapabilities2 =
    let Name = "VK_KHR_get_surface_capabilities2"
    let Number = 120
    
    let Required = [ KHRSurface.Name ]
    open KHRSurface
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceSurfaceInfo2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public surface : VkSurfaceKHR
    
            new(sType : VkStructureType
              , pNext : nativeint
              , surface : VkSurfaceKHR
              ) =
                {
                    sType = sType
                    pNext = pNext
                    surface = surface
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "surface = %A" x.surface
                ] |> sprintf "VkPhysicalDeviceSurfaceInfo2KHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSurfaceCapabilities2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public surfaceCapabilities : VkSurfaceCapabilitiesKHR
    
            new(sType : VkStructureType
              , pNext : nativeint
              , surfaceCapabilities : VkSurfaceCapabilitiesKHR
              ) =
                {
                    sType = sType
                    pNext = pNext
                    surfaceCapabilities = surfaceCapabilities
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "surfaceCapabilities = %A" x.surfaceCapabilities
                ] |> sprintf "VkSurfaceCapabilities2KHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSurfaceFormat2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public surfaceFormat : VkSurfaceFormatKHR
    
            new(sType : VkStructureType
              , pNext : nativeint
              , surfaceFormat : VkSurfaceFormatKHR
              ) =
                {
                    sType = sType
                    pNext = pNext
                    surfaceFormat = surfaceFormat
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "surfaceFormat = %A" x.surfaceFormat
                ] |> sprintf "VkSurfaceFormat2KHR { %s }"
        end
    
    
    type VkStructureType with
         static member inline PhysicalDeviceSurfaceInfo2Khr = unbox<VkStructureType> 1000119000
         static member inline SurfaceCapabilities2Khr = unbox<VkStructureType> 1000119001
         static member inline SurfaceFormat2Khr = unbox<VkStructureType> 1000119002
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceSurfaceCapabilities2KHRDel = delegate of VkPhysicalDevice * nativeptr<VkPhysicalDeviceSurfaceInfo2KHR> * nativeptr<VkSurfaceCapabilities2KHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceSurfaceFormats2KHRDel = delegate of VkPhysicalDevice * nativeptr<VkPhysicalDeviceSurfaceInfo2KHR> * nativeptr<uint32> * nativeptr<VkSurfaceFormat2KHR> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_get_surface_capabilities2")
            static let s_vkGetPhysicalDeviceSurfaceCapabilities2KHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceSurfaceCapabilities2KHRDel> "vkGetPhysicalDeviceSurfaceCapabilities2KHR"
            static let s_vkGetPhysicalDeviceSurfaceFormats2KHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceSurfaceFormats2KHRDel> "vkGetPhysicalDeviceSurfaceFormats2KHR"
            static do Report.End(3) |> ignore
            static member vkGetPhysicalDeviceSurfaceCapabilities2KHR = s_vkGetPhysicalDeviceSurfaceCapabilities2KHRDel
            static member vkGetPhysicalDeviceSurfaceFormats2KHR = s_vkGetPhysicalDeviceSurfaceFormats2KHRDel
        let vkGetPhysicalDeviceSurfaceCapabilities2KHR(physicalDevice : VkPhysicalDevice, pSurfaceInfo : nativeptr<VkPhysicalDeviceSurfaceInfo2KHR>, pSurfaceCapabilities : nativeptr<VkSurfaceCapabilities2KHR>) = Loader<unit>.vkGetPhysicalDeviceSurfaceCapabilities2KHR.Invoke(physicalDevice, pSurfaceInfo, pSurfaceCapabilities)
        let vkGetPhysicalDeviceSurfaceFormats2KHR(physicalDevice : VkPhysicalDevice, pSurfaceInfo : nativeptr<VkPhysicalDeviceSurfaceInfo2KHR>, pSurfaceFormatCount : nativeptr<uint32>, pSurfaceFormats : nativeptr<VkSurfaceFormat2KHR>) = Loader<unit>.vkGetPhysicalDeviceSurfaceFormats2KHR.Invoke(physicalDevice, pSurfaceInfo, pSurfaceFormatCount, pSurfaceFormats)

module KHRImageFormatList =
    let Name = "VK_KHR_image_format_list"
    let Number = 148
    
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImageFormatListCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public viewFormatCount : uint32
            val mutable public pViewFormats : nativeptr<VkFormat>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , viewFormatCount : uint32
              , pViewFormats : nativeptr<VkFormat>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    viewFormatCount = viewFormatCount
                    pViewFormats = pViewFormats
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "viewFormatCount = %A" x.viewFormatCount
                    sprintf "pViewFormats = %A" x.pViewFormats
                ] |> sprintf "VkImageFormatListCreateInfoKHR { %s }"
        end
    
    
    type VkStructureType with
         static member inline ImageFormatListCreateInfoKhr = unbox<VkStructureType> 1000147000
    

module KHRIncrementalPresent =
    let Name = "VK_KHR_incremental_present"
    let Number = 85
    
    let Required = [ KHRSurface.Name; KHRSwapchain.Name ]
    open KHRSurface
    open KHRSwapchain
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkRectLayerKHR = 
        struct
            val mutable public offset : VkOffset2D
            val mutable public extent : VkExtent2D
            val mutable public layer : uint32
    
            new(offset : VkOffset2D
              , extent : VkExtent2D
              , layer : uint32
              ) =
                {
                    offset = offset
                    extent = extent
                    layer = layer
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "offset = %A" x.offset
                    sprintf "extent = %A" x.extent
                    sprintf "layer = %A" x.layer
                ] |> sprintf "VkRectLayerKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPresentRegionKHR = 
        struct
            val mutable public rectangleCount : uint32
            val mutable public pRectangles : nativeptr<VkRectLayerKHR>
    
            new(rectangleCount : uint32
              , pRectangles : nativeptr<VkRectLayerKHR>
              ) =
                {
                    rectangleCount = rectangleCount
                    pRectangles = pRectangles
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "rectangleCount = %A" x.rectangleCount
                    sprintf "pRectangles = %A" x.pRectangles
                ] |> sprintf "VkPresentRegionKHR { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPresentRegionsKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public swapchainCount : uint32
            val mutable public pRegions : nativeptr<VkPresentRegionKHR>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , swapchainCount : uint32
              , pRegions : nativeptr<VkPresentRegionKHR>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    swapchainCount = swapchainCount
                    pRegions = pRegions
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "swapchainCount = %A" x.swapchainCount
                    sprintf "pRegions = %A" x.pRegions
                ] |> sprintf "VkPresentRegionsKHR { %s }"
        end
    
    
    type VkStructureType with
         static member inline PresentRegionsKhr = unbox<VkStructureType> 1000084000
    

module KHRMaintenance2 =
    let Name = "VK_KHR_maintenance2"
    let Number = 118
    
    open EXTDebugReport
    
    
    type VkImageViewUsageCreateInfoKHR = VkImageViewUsageCreateInfo
    type VkInputAttachmentAspectReferenceKHR = VkInputAttachmentAspectReference
    type VkPhysicalDevicePointClippingPropertiesKHR = VkPhysicalDevicePointClippingProperties
    type VkPipelineTessellationDomainOriginStateCreateInfoKHR = VkPipelineTessellationDomainOriginStateCreateInfo
    type VkRenderPassInputAttachmentAspectCreateInfoKHR = VkRenderPassInputAttachmentAspectCreateInfo
    
    

module KHRMirSurface =
    let Name = "VK_KHR_mir_surface"
    let Number = 8
    
    let Required = [ KHRSurface.Name ]
    open KHRSurface
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMirSurfaceCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkMirSurfaceCreateFlagsKHR
            val mutable public connection : nativeptr<nativeint>
            val mutable public mirSurface : nativeptr<nativeint>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkMirSurfaceCreateFlagsKHR
              , connection : nativeptr<nativeint>
              , mirSurface : nativeptr<nativeint>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    connection = connection
                    mirSurface = mirSurface
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "connection = %A" x.connection
                    sprintf "mirSurface = %A" x.mirSurface
                ] |> sprintf "VkMirSurfaceCreateInfoKHR { %s }"
        end
    
    
    type VkStructureType with
         static member inline MirSurfaceCreateInfoKhr = unbox<VkStructureType> 1000007000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateMirSurfaceKHRDel = delegate of VkInstance * nativeptr<VkMirSurfaceCreateInfoKHR> * nativeptr<VkAllocationCallbacks> * nativeptr<VkSurfaceKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceMirPresentationSupportKHRDel = delegate of VkPhysicalDevice * uint32 * nativeptr<nativeint> -> VkBool32
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_mir_surface")
            static let s_vkCreateMirSurfaceKHRDel = VkRaw.vkImportInstanceDelegate<VkCreateMirSurfaceKHRDel> "vkCreateMirSurfaceKHR"
            static let s_vkGetPhysicalDeviceMirPresentationSupportKHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceMirPresentationSupportKHRDel> "vkGetPhysicalDeviceMirPresentationSupportKHR"
            static do Report.End(3) |> ignore
            static member vkCreateMirSurfaceKHR = s_vkCreateMirSurfaceKHRDel
            static member vkGetPhysicalDeviceMirPresentationSupportKHR = s_vkGetPhysicalDeviceMirPresentationSupportKHRDel
        let vkCreateMirSurfaceKHR(instance : VkInstance, pCreateInfo : nativeptr<VkMirSurfaceCreateInfoKHR>, pAllocator : nativeptr<VkAllocationCallbacks>, pSurface : nativeptr<VkSurfaceKHR>) = Loader<unit>.vkCreateMirSurfaceKHR.Invoke(instance, pCreateInfo, pAllocator, pSurface)
        let vkGetPhysicalDeviceMirPresentationSupportKHR(physicalDevice : VkPhysicalDevice, queueFamilyIndex : uint32, connection : nativeptr<nativeint>) = Loader<unit>.vkGetPhysicalDeviceMirPresentationSupportKHR.Invoke(physicalDevice, queueFamilyIndex, connection)

module KHRMultiview =
    let Name = "VK_KHR_multiview"
    let Number = 54
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name ]
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    type VkPhysicalDeviceMultiviewFeaturesKHR = VkPhysicalDeviceMultiviewFeatures
    type VkPhysicalDeviceMultiviewPropertiesKHR = VkPhysicalDeviceMultiviewProperties
    type VkRenderPassMultiviewCreateInfoKHR = VkRenderPassMultiviewCreateInfo
    
    

module KHRPushDescriptor =
    let Name = "VK_KHR_push_descriptor"
    let Number = 81
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name ]
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDevicePushDescriptorPropertiesKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public maxPushDescriptors : uint32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , maxPushDescriptors : uint32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    maxPushDescriptors = maxPushDescriptors
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "maxPushDescriptors = %A" x.maxPushDescriptors
                ] |> sprintf "VkPhysicalDevicePushDescriptorPropertiesKHR { %s }"
        end
    
    
    type VkDescriptorSetLayoutCreateFlags with
         static member inline PushDescriptorBitKhr = unbox<VkDescriptorSetLayoutCreateFlags> 1
    type VkStructureType with
         static member inline PhysicalDevicePushDescriptorPropertiesKhr = unbox<VkStructureType> 1000080000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdPushDescriptorSetKHRDel = delegate of VkCommandBuffer * VkPipelineBindPoint * VkPipelineLayout * uint32 * uint32 * nativeptr<VkWriteDescriptorSet> -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_push_descriptor")
            static let s_vkCmdPushDescriptorSetKHRDel = VkRaw.vkImportInstanceDelegate<VkCmdPushDescriptorSetKHRDel> "vkCmdPushDescriptorSetKHR"
            static do Report.End(3) |> ignore
            static member vkCmdPushDescriptorSetKHR = s_vkCmdPushDescriptorSetKHRDel
        let vkCmdPushDescriptorSetKHR(commandBuffer : VkCommandBuffer, pipelineBindPoint : VkPipelineBindPoint, layout : VkPipelineLayout, set : uint32, descriptorWriteCount : uint32, pDescriptorWrites : nativeptr<VkWriteDescriptorSet>) = Loader<unit>.vkCmdPushDescriptorSetKHR.Invoke(commandBuffer, pipelineBindPoint, layout, set, descriptorWriteCount, pDescriptorWrites)

module KHRRelaxedBlockLayout =
    let Name = "VK_KHR_relaxed_block_layout"
    let Number = 145
    
    open EXTDebugReport
    
    
    
    

module KHRSamplerMirrorClampToEdge =
    let Name = "VK_KHR_sampler_mirror_clamp_to_edge"
    let Number = 15
    
    open EXTDebugReport
    
    
    
    

module KHRShaderDrawParameters =
    let Name = "VK_KHR_shader_draw_parameters"
    let Number = 64
    
    open EXTDebugReport
    
    
    
    

module KHRSharedPresentableImage =
    let Name = "VK_KHR_shared_presentable_image"
    let Number = 112
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name; KHRGetSurfaceCapabilities2.Name; KHRSurface.Name; KHRSwapchain.Name ]
    open KHRGetPhysicalDeviceProperties2
    open KHRGetSurfaceCapabilities2
    open KHRSurface
    open KHRSwapchain
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSharedPresentSurfaceCapabilitiesKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public sharedPresentSupportedUsageFlags : VkImageUsageFlags
    
            new(sType : VkStructureType
              , pNext : nativeint
              , sharedPresentSupportedUsageFlags : VkImageUsageFlags
              ) =
                {
                    sType = sType
                    pNext = pNext
                    sharedPresentSupportedUsageFlags = sharedPresentSupportedUsageFlags
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "sharedPresentSupportedUsageFlags = %A" x.sharedPresentSupportedUsageFlags
                ] |> sprintf "VkSharedPresentSurfaceCapabilitiesKHR { %s }"
        end
    
    
    type VkImageLayout with
         static member inline SharedPresentKhr = unbox<VkImageLayout> 1000111000
    type VkPresentModeKHR with
         static member inline VkPresentModeSharedDemandRefreshKhr = unbox<VkPresentModeKHR> 1000111000
         static member inline VkPresentModeSharedContinuousRefreshKhr = unbox<VkPresentModeKHR> 1000111001
    type VkStructureType with
         static member inline SharedPresentSurfaceCapabilitiesKhr = unbox<VkStructureType> 1000111000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetSwapchainStatusKHRDel = delegate of VkDevice * VkSwapchainKHR -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_shared_presentable_image")
            static let s_vkGetSwapchainStatusKHRDel = VkRaw.vkImportInstanceDelegate<VkGetSwapchainStatusKHRDel> "vkGetSwapchainStatusKHR"
            static do Report.End(3) |> ignore
            static member vkGetSwapchainStatusKHR = s_vkGetSwapchainStatusKHRDel
        let vkGetSwapchainStatusKHR(device : VkDevice, swapchain : VkSwapchainKHR) = Loader<unit>.vkGetSwapchainStatusKHR.Invoke(device, swapchain)

module KHRVariablePointers =
    let Name = "VK_KHR_variable_pointers"
    let Number = 121
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name; KHRStorageBufferStorageClass.Name ]
    open KHRGetPhysicalDeviceProperties2
    open KHRStorageBufferStorageClass
    open EXTDebugReport
    
    
    type VkPhysicalDeviceVariablePointerFeaturesKHR = VkPhysicalDeviceVariablePointerFeatures
    
    

module KHRWaylandSurface =
    let Name = "VK_KHR_wayland_surface"
    let Number = 7
    
    let Required = [ KHRSurface.Name ]
    open KHRSurface
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkWaylandSurfaceCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkWaylandSurfaceCreateFlagsKHR
            val mutable public display : nativeptr<nativeint>
            val mutable public surface : nativeptr<nativeint>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkWaylandSurfaceCreateFlagsKHR
              , display : nativeptr<nativeint>
              , surface : nativeptr<nativeint>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    display = display
                    surface = surface
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "display = %A" x.display
                    sprintf "surface = %A" x.surface
                ] |> sprintf "VkWaylandSurfaceCreateInfoKHR { %s }"
        end
    
    
    type VkStructureType with
         static member inline WaylandSurfaceCreateInfoKhr = unbox<VkStructureType> 1000006000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateWaylandSurfaceKHRDel = delegate of VkInstance * nativeptr<VkWaylandSurfaceCreateInfoKHR> * nativeptr<VkAllocationCallbacks> * nativeptr<VkSurfaceKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceWaylandPresentationSupportKHRDel = delegate of VkPhysicalDevice * uint32 * nativeptr<nativeint> -> VkBool32
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_wayland_surface")
            static let s_vkCreateWaylandSurfaceKHRDel = VkRaw.vkImportInstanceDelegate<VkCreateWaylandSurfaceKHRDel> "vkCreateWaylandSurfaceKHR"
            static let s_vkGetPhysicalDeviceWaylandPresentationSupportKHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceWaylandPresentationSupportKHRDel> "vkGetPhysicalDeviceWaylandPresentationSupportKHR"
            static do Report.End(3) |> ignore
            static member vkCreateWaylandSurfaceKHR = s_vkCreateWaylandSurfaceKHRDel
            static member vkGetPhysicalDeviceWaylandPresentationSupportKHR = s_vkGetPhysicalDeviceWaylandPresentationSupportKHRDel
        let vkCreateWaylandSurfaceKHR(instance : VkInstance, pCreateInfo : nativeptr<VkWaylandSurfaceCreateInfoKHR>, pAllocator : nativeptr<VkAllocationCallbacks>, pSurface : nativeptr<VkSurfaceKHR>) = Loader<unit>.vkCreateWaylandSurfaceKHR.Invoke(instance, pCreateInfo, pAllocator, pSurface)
        let vkGetPhysicalDeviceWaylandPresentationSupportKHR(physicalDevice : VkPhysicalDevice, queueFamilyIndex : uint32, display : nativeptr<nativeint>) = Loader<unit>.vkGetPhysicalDeviceWaylandPresentationSupportKHR.Invoke(physicalDevice, queueFamilyIndex, display)

module KHRWin32KeyedMutex =
    let Name = "VK_KHR_win32_keyed_mutex"
    let Number = 76
    
    let Required = [ KHRExternalMemory.Name; KHRExternalMemoryCapabilities.Name; KHRExternalMemoryWin32.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalMemory
    open KHRExternalMemoryCapabilities
    open KHRExternalMemoryWin32
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkWin32KeyedMutexAcquireReleaseInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public acquireCount : uint32
            val mutable public pAcquireSyncs : nativeptr<VkDeviceMemory>
            val mutable public pAcquireKeys : nativeptr<uint64>
            val mutable public pAcquireTimeouts : nativeptr<uint32>
            val mutable public releaseCount : uint32
            val mutable public pReleaseSyncs : nativeptr<VkDeviceMemory>
            val mutable public pReleaseKeys : nativeptr<uint64>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , acquireCount : uint32
              , pAcquireSyncs : nativeptr<VkDeviceMemory>
              , pAcquireKeys : nativeptr<uint64>
              , pAcquireTimeouts : nativeptr<uint32>
              , releaseCount : uint32
              , pReleaseSyncs : nativeptr<VkDeviceMemory>
              , pReleaseKeys : nativeptr<uint64>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    acquireCount = acquireCount
                    pAcquireSyncs = pAcquireSyncs
                    pAcquireKeys = pAcquireKeys
                    pAcquireTimeouts = pAcquireTimeouts
                    releaseCount = releaseCount
                    pReleaseSyncs = pReleaseSyncs
                    pReleaseKeys = pReleaseKeys
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "acquireCount = %A" x.acquireCount
                    sprintf "pAcquireSyncs = %A" x.pAcquireSyncs
                    sprintf "pAcquireKeys = %A" x.pAcquireKeys
                    sprintf "pAcquireTimeouts = %A" x.pAcquireTimeouts
                    sprintf "releaseCount = %A" x.releaseCount
                    sprintf "pReleaseSyncs = %A" x.pReleaseSyncs
                    sprintf "pReleaseKeys = %A" x.pReleaseKeys
                ] |> sprintf "VkWin32KeyedMutexAcquireReleaseInfoKHR { %s }"
        end
    
    
    type VkStructureType with
         static member inline Win32KeyedMutexAcquireReleaseInfoKhr = unbox<VkStructureType> 1000075000
    

module KHRWin32Surface =
    let Name = "VK_KHR_win32_surface"
    let Number = 10
    
    let Required = [ KHRSurface.Name ]
    open KHRSurface
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkWin32SurfaceCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkWin32SurfaceCreateFlagsKHR
            val mutable public hinstance : nativeint
            val mutable public hwnd : nativeint
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkWin32SurfaceCreateFlagsKHR
              , hinstance : nativeint
              , hwnd : nativeint
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    hinstance = hinstance
                    hwnd = hwnd
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "hinstance = %A" x.hinstance
                    sprintf "hwnd = %A" x.hwnd
                ] |> sprintf "VkWin32SurfaceCreateInfoKHR { %s }"
        end
    
    
    type VkStructureType with
         static member inline Win32SurfaceCreateInfoKhr = unbox<VkStructureType> 1000009000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateWin32SurfaceKHRDel = delegate of VkInstance * nativeptr<VkWin32SurfaceCreateInfoKHR> * nativeptr<VkAllocationCallbacks> * nativeptr<VkSurfaceKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceWin32PresentationSupportKHRDel = delegate of VkPhysicalDevice * uint32 -> VkBool32
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_win32_surface")
            static let s_vkCreateWin32SurfaceKHRDel = VkRaw.vkImportInstanceDelegate<VkCreateWin32SurfaceKHRDel> "vkCreateWin32SurfaceKHR"
            static let s_vkGetPhysicalDeviceWin32PresentationSupportKHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceWin32PresentationSupportKHRDel> "vkGetPhysicalDeviceWin32PresentationSupportKHR"
            static do Report.End(3) |> ignore
            static member vkCreateWin32SurfaceKHR = s_vkCreateWin32SurfaceKHRDel
            static member vkGetPhysicalDeviceWin32PresentationSupportKHR = s_vkGetPhysicalDeviceWin32PresentationSupportKHRDel
        let vkCreateWin32SurfaceKHR(instance : VkInstance, pCreateInfo : nativeptr<VkWin32SurfaceCreateInfoKHR>, pAllocator : nativeptr<VkAllocationCallbacks>, pSurface : nativeptr<VkSurfaceKHR>) = Loader<unit>.vkCreateWin32SurfaceKHR.Invoke(instance, pCreateInfo, pAllocator, pSurface)
        let vkGetPhysicalDeviceWin32PresentationSupportKHR(physicalDevice : VkPhysicalDevice, queueFamilyIndex : uint32) = Loader<unit>.vkGetPhysicalDeviceWin32PresentationSupportKHR.Invoke(physicalDevice, queueFamilyIndex)

module KHRXcbSurface =
    let Name = "VK_KHR_xcb_surface"
    let Number = 6
    
    let Required = [ KHRSurface.Name ]
    open KHRSurface
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkXcbSurfaceCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkXcbSurfaceCreateFlagsKHR
            val mutable public connection : nativeptr<nativeint>
            val mutable public window : nativeint
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkXcbSurfaceCreateFlagsKHR
              , connection : nativeptr<nativeint>
              , window : nativeint
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    connection = connection
                    window = window
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "connection = %A" x.connection
                    sprintf "window = %A" x.window
                ] |> sprintf "VkXcbSurfaceCreateInfoKHR { %s }"
        end
    
    
    type VkStructureType with
         static member inline XcbSurfaceCreateInfoKhr = unbox<VkStructureType> 1000005000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateXcbSurfaceKHRDel = delegate of VkInstance * nativeptr<VkXcbSurfaceCreateInfoKHR> * nativeptr<VkAllocationCallbacks> * nativeptr<VkSurfaceKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceXcbPresentationSupportKHRDel = delegate of VkPhysicalDevice * uint32 * nativeptr<nativeint> * nativeint -> VkBool32
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_xcb_surface")
            static let s_vkCreateXcbSurfaceKHRDel = VkRaw.vkImportInstanceDelegate<VkCreateXcbSurfaceKHRDel> "vkCreateXcbSurfaceKHR"
            static let s_vkGetPhysicalDeviceXcbPresentationSupportKHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceXcbPresentationSupportKHRDel> "vkGetPhysicalDeviceXcbPresentationSupportKHR"
            static do Report.End(3) |> ignore
            static member vkCreateXcbSurfaceKHR = s_vkCreateXcbSurfaceKHRDel
            static member vkGetPhysicalDeviceXcbPresentationSupportKHR = s_vkGetPhysicalDeviceXcbPresentationSupportKHRDel
        let vkCreateXcbSurfaceKHR(instance : VkInstance, pCreateInfo : nativeptr<VkXcbSurfaceCreateInfoKHR>, pAllocator : nativeptr<VkAllocationCallbacks>, pSurface : nativeptr<VkSurfaceKHR>) = Loader<unit>.vkCreateXcbSurfaceKHR.Invoke(instance, pCreateInfo, pAllocator, pSurface)
        let vkGetPhysicalDeviceXcbPresentationSupportKHR(physicalDevice : VkPhysicalDevice, queueFamilyIndex : uint32, connection : nativeptr<nativeint>, visual_id : nativeint) = Loader<unit>.vkGetPhysicalDeviceXcbPresentationSupportKHR.Invoke(physicalDevice, queueFamilyIndex, connection, visual_id)

module KHRXlibSurface =
    let Name = "VK_KHR_xlib_surface"
    let Number = 5
    
    let Required = [ KHRSurface.Name ]
    open KHRSurface
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkXlibSurfaceCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkXlibSurfaceCreateFlagsKHR
            val mutable public dpy : nativeptr<nativeint>
            val mutable public window : nativeint
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkXlibSurfaceCreateFlagsKHR
              , dpy : nativeptr<nativeint>
              , window : nativeint
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    dpy = dpy
                    window = window
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "dpy = %A" x.dpy
                    sprintf "window = %A" x.window
                ] |> sprintf "VkXlibSurfaceCreateInfoKHR { %s }"
        end
    
    
    type VkStructureType with
         static member inline XlibSurfaceCreateInfoKhr = unbox<VkStructureType> 1000004000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateXlibSurfaceKHRDel = delegate of VkInstance * nativeptr<VkXlibSurfaceCreateInfoKHR> * nativeptr<VkAllocationCallbacks> * nativeptr<VkSurfaceKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceXlibPresentationSupportKHRDel = delegate of VkPhysicalDevice * uint32 * nativeptr<nativeint> * nativeint -> VkBool32
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_xlib_surface")
            static let s_vkCreateXlibSurfaceKHRDel = VkRaw.vkImportInstanceDelegate<VkCreateXlibSurfaceKHRDel> "vkCreateXlibSurfaceKHR"
            static let s_vkGetPhysicalDeviceXlibPresentationSupportKHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceXlibPresentationSupportKHRDel> "vkGetPhysicalDeviceXlibPresentationSupportKHR"
            static do Report.End(3) |> ignore
            static member vkCreateXlibSurfaceKHR = s_vkCreateXlibSurfaceKHRDel
            static member vkGetPhysicalDeviceXlibPresentationSupportKHR = s_vkGetPhysicalDeviceXlibPresentationSupportKHRDel
        let vkCreateXlibSurfaceKHR(instance : VkInstance, pCreateInfo : nativeptr<VkXlibSurfaceCreateInfoKHR>, pAllocator : nativeptr<VkAllocationCallbacks>, pSurface : nativeptr<VkSurfaceKHR>) = Loader<unit>.vkCreateXlibSurfaceKHR.Invoke(instance, pCreateInfo, pAllocator, pSurface)
        let vkGetPhysicalDeviceXlibPresentationSupportKHR(physicalDevice : VkPhysicalDevice, queueFamilyIndex : uint32, dpy : nativeptr<nativeint>, visualID : nativeint) = Loader<unit>.vkGetPhysicalDeviceXlibPresentationSupportKHR.Invoke(physicalDevice, queueFamilyIndex, dpy, visualID)

module MVKIosSurface =
    let Name = "VK_MVK_ios_surface"
    let Number = 123
    
    let Required = [ KHRSurface.Name ]
    open KHRSurface
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkIOSSurfaceCreateInfoMVK = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkIOSSurfaceCreateFlagsMVK
            val mutable public pView : nativeint
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkIOSSurfaceCreateFlagsMVK
              , pView : nativeint
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    pView = pView
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "pView = %A" x.pView
                ] |> sprintf "VkIOSSurfaceCreateInfoMVK { %s }"
        end
    
    
    type VkStructureType with
         static member inline IosSurfaceCreateInfoMvk = unbox<VkStructureType> 1000122000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateIOSSurfaceMVKDel = delegate of VkInstance * nativeptr<VkIOSSurfaceCreateInfoMVK> * nativeptr<VkAllocationCallbacks> * nativeptr<VkSurfaceKHR> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_MVK_ios_surface")
            static let s_vkCreateIOSSurfaceMVKDel = VkRaw.vkImportInstanceDelegate<VkCreateIOSSurfaceMVKDel> "vkCreateIOSSurfaceMVK"
            static do Report.End(3) |> ignore
            static member vkCreateIOSSurfaceMVK = s_vkCreateIOSSurfaceMVKDel
        let vkCreateIOSSurfaceMVK(instance : VkInstance, pCreateInfo : nativeptr<VkIOSSurfaceCreateInfoMVK>, pAllocator : nativeptr<VkAllocationCallbacks>, pSurface : nativeptr<VkSurfaceKHR>) = Loader<unit>.vkCreateIOSSurfaceMVK.Invoke(instance, pCreateInfo, pAllocator, pSurface)

module MVKMacosSurface =
    let Name = "VK_MVK_macos_surface"
    let Number = 124
    
    let Required = [ KHRSurface.Name ]
    open KHRSurface
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMacOSSurfaceCreateInfoMVK = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkMacOSSurfaceCreateFlagsMVK
            val mutable public pView : nativeint
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkMacOSSurfaceCreateFlagsMVK
              , pView : nativeint
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    pView = pView
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "pView = %A" x.pView
                ] |> sprintf "VkMacOSSurfaceCreateInfoMVK { %s }"
        end
    
    
    type VkStructureType with
         static member inline MacosSurfaceCreateInfoMvk = unbox<VkStructureType> 1000123000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateMacOSSurfaceMVKDel = delegate of VkInstance * nativeptr<VkMacOSSurfaceCreateInfoMVK> * nativeptr<VkAllocationCallbacks> * nativeptr<VkSurfaceKHR> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_MVK_macos_surface")
            static let s_vkCreateMacOSSurfaceMVKDel = VkRaw.vkImportInstanceDelegate<VkCreateMacOSSurfaceMVKDel> "vkCreateMacOSSurfaceMVK"
            static do Report.End(3) |> ignore
            static member vkCreateMacOSSurfaceMVK = s_vkCreateMacOSSurfaceMVKDel
        let vkCreateMacOSSurfaceMVK(instance : VkInstance, pCreateInfo : nativeptr<VkMacOSSurfaceCreateInfoMVK>, pAllocator : nativeptr<VkAllocationCallbacks>, pSurface : nativeptr<VkSurfaceKHR>) = Loader<unit>.vkCreateMacOSSurfaceMVK.Invoke(instance, pCreateInfo, pAllocator, pSurface)

module MVKMoltenvk =
    let Name = "VK_MVK_moltenvk"
    let Number = 125
    
    open EXTDebugReport
    
    
    
    

module NNViSurface =
    let Name = "VK_NN_vi_surface"
    let Number = 63
    
    let Required = [ KHRSurface.Name ]
    open KHRSurface
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkViSurfaceCreateInfoNN = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkViSurfaceCreateFlagsNN
            val mutable public window : nativeint
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkViSurfaceCreateFlagsNN
              , window : nativeint
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    window = window
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "window = %A" x.window
                ] |> sprintf "VkViSurfaceCreateInfoNN { %s }"
        end
    
    
    type VkStructureType with
         static member inline ViSurfaceCreateInfoNn = unbox<VkStructureType> 1000062000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateViSurfaceNNDel = delegate of VkInstance * nativeptr<VkViSurfaceCreateInfoNN> * nativeptr<VkAllocationCallbacks> * nativeptr<VkSurfaceKHR> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_NN_vi_surface")
            static let s_vkCreateViSurfaceNNDel = VkRaw.vkImportInstanceDelegate<VkCreateViSurfaceNNDel> "vkCreateViSurfaceNN"
            static do Report.End(3) |> ignore
            static member vkCreateViSurfaceNN = s_vkCreateViSurfaceNNDel
        let vkCreateViSurfaceNN(instance : VkInstance, pCreateInfo : nativeptr<VkViSurfaceCreateInfoNN>, pAllocator : nativeptr<VkAllocationCallbacks>, pSurface : nativeptr<VkSurfaceKHR>) = Loader<unit>.vkCreateViSurfaceNN.Invoke(instance, pCreateInfo, pAllocator, pSurface)

module NVClipSpaceWScaling =
    let Name = "VK_NV_clip_space_w_scaling"
    let Number = 88
    
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkViewportWScalingNV = 
        struct
            val mutable public xcoeff : float32
            val mutable public ycoeff : float32
    
            new(xcoeff : float32
              , ycoeff : float32
              ) =
                {
                    xcoeff = xcoeff
                    ycoeff = ycoeff
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "xcoeff = %A" x.xcoeff
                    sprintf "ycoeff = %A" x.ycoeff
                ] |> sprintf "VkViewportWScalingNV { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPipelineViewportWScalingStateCreateInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public viewportWScalingEnable : VkBool32
            val mutable public viewportCount : uint32
            val mutable public pViewportWScalings : nativeptr<VkViewportWScalingNV>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , viewportWScalingEnable : VkBool32
              , viewportCount : uint32
              , pViewportWScalings : nativeptr<VkViewportWScalingNV>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    viewportWScalingEnable = viewportWScalingEnable
                    viewportCount = viewportCount
                    pViewportWScalings = pViewportWScalings
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "viewportWScalingEnable = %A" x.viewportWScalingEnable
                    sprintf "viewportCount = %A" x.viewportCount
                    sprintf "pViewportWScalings = %A" x.pViewportWScalings
                ] |> sprintf "VkPipelineViewportWScalingStateCreateInfoNV { %s }"
        end
    
    
    type VkDynamicState with
         static member inline ViewportWScalingNv = unbox<VkDynamicState> 1000087000
    type VkStructureType with
         static member inline PipelineViewportWScalingStateCreateInfoNv = unbox<VkStructureType> 1000087000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdSetViewportWScalingNVDel = delegate of VkCommandBuffer * uint32 * uint32 * nativeptr<VkViewportWScalingNV> -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_NV_clip_space_w_scaling")
            static let s_vkCmdSetViewportWScalingNVDel = VkRaw.vkImportInstanceDelegate<VkCmdSetViewportWScalingNVDel> "vkCmdSetViewportWScalingNV"
            static do Report.End(3) |> ignore
            static member vkCmdSetViewportWScalingNV = s_vkCmdSetViewportWScalingNVDel
        let vkCmdSetViewportWScalingNV(commandBuffer : VkCommandBuffer, firstViewport : uint32, viewportCount : uint32, pViewportWScalings : nativeptr<VkViewportWScalingNV>) = Loader<unit>.vkCmdSetViewportWScalingNV.Invoke(commandBuffer, firstViewport, viewportCount, pViewportWScalings)

module NVDedicatedAllocation =
    let Name = "VK_NV_dedicated_allocation"
    let Number = 27
    
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDedicatedAllocationBufferCreateInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public dedicatedAllocation : VkBool32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , dedicatedAllocation : VkBool32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    dedicatedAllocation = dedicatedAllocation
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "dedicatedAllocation = %A" x.dedicatedAllocation
                ] |> sprintf "VkDedicatedAllocationBufferCreateInfoNV { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDedicatedAllocationImageCreateInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public dedicatedAllocation : VkBool32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , dedicatedAllocation : VkBool32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    dedicatedAllocation = dedicatedAllocation
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "dedicatedAllocation = %A" x.dedicatedAllocation
                ] |> sprintf "VkDedicatedAllocationImageCreateInfoNV { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDedicatedAllocationMemoryAllocateInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public image : VkImage
            val mutable public buffer : VkBuffer
    
            new(sType : VkStructureType
              , pNext : nativeint
              , image : VkImage
              , buffer : VkBuffer
              ) =
                {
                    sType = sType
                    pNext = pNext
                    image = image
                    buffer = buffer
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "image = %A" x.image
                    sprintf "buffer = %A" x.buffer
                ] |> sprintf "VkDedicatedAllocationMemoryAllocateInfoNV { %s }"
        end
    
    
    type VkStructureType with
         static member inline DedicatedAllocationImageCreateInfoNv = unbox<VkStructureType> 1000026000
         static member inline DedicatedAllocationBufferCreateInfoNv = unbox<VkStructureType> 1000026001
         static member inline DedicatedAllocationMemoryAllocateInfoNv = unbox<VkStructureType> 1000026002
    

module NVExternalMemoryCapabilities =
    let Name = "VK_NV_external_memory_capabilities"
    let Number = 56
    
    open EXTDebugReport
    
    [<Flags>]
    type VkExternalMemoryHandleTypeFlagsNV = 
        | None = 0
        | VkExternalMemoryHandleTypeOpaqueWin32BitNv = 0x00000001
        | VkExternalMemoryHandleTypeOpaqueWin32KmtBitNv = 0x00000002
        | VkExternalMemoryHandleTypeD3d11ImageBitNv = 0x00000004
        | VkExternalMemoryHandleTypeD3d11ImageKmtBitNv = 0x00000008
    
    [<Flags>]
    type VkExternalMemoryFeatureFlagsNV = 
        | None = 0
        | VkExternalMemoryFeatureDedicatedOnlyBitNv = 0x00000001
        | VkExternalMemoryFeatureExportableBitNv = 0x00000002
        | VkExternalMemoryFeatureImportableBitNv = 0x00000004
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExternalImageFormatPropertiesNV = 
        struct
            val mutable public imageFormatProperties : VkImageFormatProperties
            val mutable public externalMemoryFeatures : VkExternalMemoryFeatureFlagsNV
            val mutable public exportFromImportedHandleTypes : VkExternalMemoryHandleTypeFlagsNV
            val mutable public compatibleHandleTypes : VkExternalMemoryHandleTypeFlagsNV
    
            new(imageFormatProperties : VkImageFormatProperties
              , externalMemoryFeatures : VkExternalMemoryFeatureFlagsNV
              , exportFromImportedHandleTypes : VkExternalMemoryHandleTypeFlagsNV
              , compatibleHandleTypes : VkExternalMemoryHandleTypeFlagsNV
              ) =
                {
                    imageFormatProperties = imageFormatProperties
                    externalMemoryFeatures = externalMemoryFeatures
                    exportFromImportedHandleTypes = exportFromImportedHandleTypes
                    compatibleHandleTypes = compatibleHandleTypes
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "imageFormatProperties = %A" x.imageFormatProperties
                    sprintf "externalMemoryFeatures = %A" x.externalMemoryFeatures
                    sprintf "exportFromImportedHandleTypes = %A" x.exportFromImportedHandleTypes
                    sprintf "compatibleHandleTypes = %A" x.compatibleHandleTypes
                ] |> sprintf "VkExternalImageFormatPropertiesNV { %s }"
        end
    
    
    

module NVExternalMemory =
    let Name = "VK_NV_external_memory"
    let Number = 57
    
    let Required = [ NVExternalMemoryCapabilities.Name ]
    open NVExternalMemoryCapabilities
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExportMemoryAllocateInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleTypes : VkExternalMemoryHandleTypeFlagsNV
    
            new(sType : VkStructureType
              , pNext : nativeint
              , handleTypes : VkExternalMemoryHandleTypeFlagsNV
              ) =
                {
                    sType = sType
                    pNext = pNext
                    handleTypes = handleTypes
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "handleTypes = %A" x.handleTypes
                ] |> sprintf "VkExportMemoryAllocateInfoNV { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExternalMemoryImageCreateInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleTypes : VkExternalMemoryHandleTypeFlagsNV
    
            new(sType : VkStructureType
              , pNext : nativeint
              , handleTypes : VkExternalMemoryHandleTypeFlagsNV
              ) =
                {
                    sType = sType
                    pNext = pNext
                    handleTypes = handleTypes
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "handleTypes = %A" x.handleTypes
                ] |> sprintf "VkExternalMemoryImageCreateInfoNV { %s }"
        end
    
    
    type VkStructureType with
         static member inline ExternalMemoryImageCreateInfoNv = unbox<VkStructureType> 1000056000
         static member inline ExportMemoryAllocateInfoNv = unbox<VkStructureType> 1000056001
    

module NVExternalMemoryWin32 =
    let Name = "VK_NV_external_memory_win32"
    let Number = 58
    
    let Required = [ NVExternalMemory.Name; NVExternalMemoryCapabilities.Name ]
    open NVExternalMemory
    open NVExternalMemoryCapabilities
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExportMemoryWin32HandleInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public pAttributes : nativeptr<nativeint>
            val mutable public dwAccess : uint32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , pAttributes : nativeptr<nativeint>
              , dwAccess : uint32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    pAttributes = pAttributes
                    dwAccess = dwAccess
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "pAttributes = %A" x.pAttributes
                    sprintf "dwAccess = %A" x.dwAccess
                ] |> sprintf "VkExportMemoryWin32HandleInfoNV { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImportMemoryWin32HandleInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleType : VkExternalMemoryHandleTypeFlagsNV
            val mutable public handle : nativeint
    
            new(sType : VkStructureType
              , pNext : nativeint
              , handleType : VkExternalMemoryHandleTypeFlagsNV
              , handle : nativeint
              ) =
                {
                    sType = sType
                    pNext = pNext
                    handleType = handleType
                    handle = handle
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "handleType = %A" x.handleType
                    sprintf "handle = %A" x.handle
                ] |> sprintf "VkImportMemoryWin32HandleInfoNV { %s }"
        end
    
    
    type VkStructureType with
         static member inline ImportMemoryWin32HandleInfoNv = unbox<VkStructureType> 1000057000
         static member inline ExportMemoryWin32HandleInfoNv = unbox<VkStructureType> 1000057001
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetMemoryWin32HandleNVDel = delegate of VkDevice * VkDeviceMemory * VkExternalMemoryHandleTypeFlagsNV * nativeptr<nativeint> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_NV_external_memory_win32")
            static let s_vkGetMemoryWin32HandleNVDel = VkRaw.vkImportInstanceDelegate<VkGetMemoryWin32HandleNVDel> "vkGetMemoryWin32HandleNV"
            static do Report.End(3) |> ignore
            static member vkGetMemoryWin32HandleNV = s_vkGetMemoryWin32HandleNVDel
        let vkGetMemoryWin32HandleNV(device : VkDevice, memory : VkDeviceMemory, handleType : VkExternalMemoryHandleTypeFlagsNV, pHandle : nativeptr<nativeint>) = Loader<unit>.vkGetMemoryWin32HandleNV.Invoke(device, memory, handleType, pHandle)

module NVFillRectangle =
    let Name = "VK_NV_fill_rectangle"
    let Number = 154
    
    open EXTDebugReport
    
    
    
    type VkPolygonMode with
         static member inline FillRectangleNv = unbox<VkPolygonMode> 1000153000
    

module NVFragmentCoverageToColor =
    let Name = "VK_NV_fragment_coverage_to_color"
    let Number = 150
    
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPipelineCoverageToColorStateCreateInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkPipelineCoverageToColorStateCreateFlagsNV
            val mutable public coverageToColorEnable : VkBool32
            val mutable public coverageToColorLocation : uint32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkPipelineCoverageToColorStateCreateFlagsNV
              , coverageToColorEnable : VkBool32
              , coverageToColorLocation : uint32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    coverageToColorEnable = coverageToColorEnable
                    coverageToColorLocation = coverageToColorLocation
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "coverageToColorEnable = %A" x.coverageToColorEnable
                    sprintf "coverageToColorLocation = %A" x.coverageToColorLocation
                ] |> sprintf "VkPipelineCoverageToColorStateCreateInfoNV { %s }"
        end
    
    
    type VkStructureType with
         static member inline PipelineCoverageToColorStateCreateInfoNv = unbox<VkStructureType> 1000149000
    

module NVFramebufferMixedSamples =
    let Name = "VK_NV_framebuffer_mixed_samples"
    let Number = 153
    
    open EXTDebugReport
    
    type VkCoverageModulationModeNV = 
        | VkCoverageModulationModeNoneNv = 0
        | VkCoverageModulationModeRgbNv = 1
        | VkCoverageModulationModeAlphaNv = 2
        | VkCoverageModulationModeRgbaNv = 3
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPipelineCoverageModulationStateCreateInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkPipelineCoverageModulationStateCreateFlagsNV
            val mutable public coverageModulationMode : VkCoverageModulationModeNV
            val mutable public coverageModulationTableEnable : VkBool32
            val mutable public coverageModulationTableCount : uint32
            val mutable public pCoverageModulationTable : nativeptr<float32>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkPipelineCoverageModulationStateCreateFlagsNV
              , coverageModulationMode : VkCoverageModulationModeNV
              , coverageModulationTableEnable : VkBool32
              , coverageModulationTableCount : uint32
              , pCoverageModulationTable : nativeptr<float32>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    coverageModulationMode = coverageModulationMode
                    coverageModulationTableEnable = coverageModulationTableEnable
                    coverageModulationTableCount = coverageModulationTableCount
                    pCoverageModulationTable = pCoverageModulationTable
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "coverageModulationMode = %A" x.coverageModulationMode
                    sprintf "coverageModulationTableEnable = %A" x.coverageModulationTableEnable
                    sprintf "coverageModulationTableCount = %A" x.coverageModulationTableCount
                    sprintf "pCoverageModulationTable = %A" x.pCoverageModulationTable
                ] |> sprintf "VkPipelineCoverageModulationStateCreateInfoNV { %s }"
        end
    
    
    type VkStructureType with
         static member inline PipelineCoverageModulationStateCreateInfoNv = unbox<VkStructureType> 1000152000
    

module NVGeometryShaderPassthrough =
    let Name = "VK_NV_geometry_shader_passthrough"
    let Number = 96
    
    open EXTDebugReport
    
    
    
    

module NVGlslShader =
    let Name = "VK_NV_glsl_shader"
    let Number = 13
    
    open EXTDebugReport
    
    
    
    type VkResult with
         static member inline VkErrorInvalidShaderNv = unbox<VkResult> -1000012000
    

module NVSampleMaskOverrideCoverage =
    let Name = "VK_NV_sample_mask_override_coverage"
    let Number = 95
    
    open EXTDebugReport
    
    
    
    

module NVShaderSubgroupPartitioned =
    let Name = "VK_NV_shader_subgroup_partitioned"
    let Number = 199
    
    open EXTDebugReport
    
    
    
    type VkSubgroupFeatureFlags with
         static member inline PartitionedBitNv = unbox<VkSubgroupFeatureFlags> 256
    

module NVViewportArray2 =
    let Name = "VK_NV_viewport_array2"
    let Number = 97
    
    open EXTDebugReport
    
    
    
    

module NVViewportSwizzle =
    let Name = "VK_NV_viewport_swizzle"
    let Number = 99
    
    open EXTDebugReport
    
    type VkViewportCoordinateSwizzleNV = 
        | VkViewportCoordinateSwizzlePositiveXNv = 0
        | VkViewportCoordinateSwizzleNegativeXNv = 1
        | VkViewportCoordinateSwizzlePositiveYNv = 2
        | VkViewportCoordinateSwizzleNegativeYNv = 3
        | VkViewportCoordinateSwizzlePositiveZNv = 4
        | VkViewportCoordinateSwizzleNegativeZNv = 5
        | VkViewportCoordinateSwizzlePositiveWNv = 6
        | VkViewportCoordinateSwizzleNegativeWNv = 7
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkViewportSwizzleNV = 
        struct
            val mutable public x : VkViewportCoordinateSwizzleNV
            val mutable public y : VkViewportCoordinateSwizzleNV
            val mutable public z : VkViewportCoordinateSwizzleNV
            val mutable public w : VkViewportCoordinateSwizzleNV
    
            new(x : VkViewportCoordinateSwizzleNV
              , y : VkViewportCoordinateSwizzleNV
              , z : VkViewportCoordinateSwizzleNV
              , w : VkViewportCoordinateSwizzleNV
              ) =
                {
                    x = x
                    y = y
                    z = z
                    w = w
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "x = %A" x.x
                    sprintf "y = %A" x.y
                    sprintf "z = %A" x.z
                    sprintf "w = %A" x.w
                ] |> sprintf "VkViewportSwizzleNV { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPipelineViewportSwizzleStateCreateInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkPipelineViewportSwizzleStateCreateFlagsNV
            val mutable public viewportCount : uint32
            val mutable public pViewportSwizzles : nativeptr<VkViewportSwizzleNV>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , flags : VkPipelineViewportSwizzleStateCreateFlagsNV
              , viewportCount : uint32
              , pViewportSwizzles : nativeptr<VkViewportSwizzleNV>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    flags = flags
                    viewportCount = viewportCount
                    pViewportSwizzles = pViewportSwizzles
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "flags = %A" x.flags
                    sprintf "viewportCount = %A" x.viewportCount
                    sprintf "pViewportSwizzles = %A" x.pViewportSwizzles
                ] |> sprintf "VkPipelineViewportSwizzleStateCreateInfoNV { %s }"
        end
    
    
    type VkStructureType with
         static member inline PipelineViewportSwizzleStateCreateInfoNv = unbox<VkStructureType> 1000098000
    

module NVWin32KeyedMutex =
    let Name = "VK_NV_win32_keyed_mutex"
    let Number = 59
    
    let Required = [ NVExternalMemory.Name; NVExternalMemoryCapabilities.Name; NVExternalMemoryWin32.Name ]
    open NVExternalMemory
    open NVExternalMemoryCapabilities
    open NVExternalMemoryWin32
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkWin32KeyedMutexAcquireReleaseInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public acquireCount : uint32
            val mutable public pAcquireSyncs : nativeptr<VkDeviceMemory>
            val mutable public pAcquireKeys : nativeptr<uint64>
            val mutable public pAcquireTimeoutMilliseconds : nativeptr<uint32>
            val mutable public releaseCount : uint32
            val mutable public pReleaseSyncs : nativeptr<VkDeviceMemory>
            val mutable public pReleaseKeys : nativeptr<uint64>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , acquireCount : uint32
              , pAcquireSyncs : nativeptr<VkDeviceMemory>
              , pAcquireKeys : nativeptr<uint64>
              , pAcquireTimeoutMilliseconds : nativeptr<uint32>
              , releaseCount : uint32
              , pReleaseSyncs : nativeptr<VkDeviceMemory>
              , pReleaseKeys : nativeptr<uint64>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    acquireCount = acquireCount
                    pAcquireSyncs = pAcquireSyncs
                    pAcquireKeys = pAcquireKeys
                    pAcquireTimeoutMilliseconds = pAcquireTimeoutMilliseconds
                    releaseCount = releaseCount
                    pReleaseSyncs = pReleaseSyncs
                    pReleaseKeys = pReleaseKeys
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "acquireCount = %A" x.acquireCount
                    sprintf "pAcquireSyncs = %A" x.pAcquireSyncs
                    sprintf "pAcquireKeys = %A" x.pAcquireKeys
                    sprintf "pAcquireTimeoutMilliseconds = %A" x.pAcquireTimeoutMilliseconds
                    sprintf "releaseCount = %A" x.releaseCount
                    sprintf "pReleaseSyncs = %A" x.pReleaseSyncs
                    sprintf "pReleaseKeys = %A" x.pReleaseKeys
                ] |> sprintf "VkWin32KeyedMutexAcquireReleaseInfoNV { %s }"
        end
    
    
    type VkStructureType with
         static member inline Win32KeyedMutexAcquireReleaseInfoNv = unbox<VkStructureType> 1000058000
    

module NVXDeviceGeneratedCommands =
    let Name = "VK_NVX_device_generated_commands"
    let Number = 87
    
    open EXTDebugReport
    
    [<Flags>]
    type VkIndirectCommandsLayoutUsageFlagsNVX = 
        | None = 0
        | VkIndirectCommandsLayoutUsageUnorderedSequencesBitNvx = 0x00000001
        | VkIndirectCommandsLayoutUsageSparseSequencesBitNvx = 0x00000002
        | VkIndirectCommandsLayoutUsageEmptyExecutionsBitNvx = 0x00000004
        | VkIndirectCommandsLayoutUsageIndexedSequencesBitNvx = 0x00000008
    
    type VkIndirectCommandsTokenTypeNVX = 
        | VkIndirectCommandsTokenTypePipelineNvx = 0
        | VkIndirectCommandsTokenTypeDescriptorSetNvx = 1
        | VkIndirectCommandsTokenTypeIndexBufferNvx = 2
        | VkIndirectCommandsTokenTypeVertexBufferNvx = 3
        | VkIndirectCommandsTokenTypePushConstantNvx = 4
        | VkIndirectCommandsTokenTypeDrawIndexedNvx = 5
        | VkIndirectCommandsTokenTypeDrawNvx = 6
        | VkIndirectCommandsTokenTypeDispatchNvx = 7
    
    [<Flags>]
    type VkObjectEntryUsageFlagsNVX = 
        | None = 0
        | VkObjectEntryUsageGraphicsBitNvx = 0x00000001
        | VkObjectEntryUsageComputeBitNvx = 0x00000002
    
    type VkObjectEntryTypeNVX = 
        | VkObjectEntryTypeDescriptorSetNvx = 0
        | VkObjectEntryTypePipelineNvx = 1
        | VkObjectEntryTypeIndexBufferNvx = 2
        | VkObjectEntryTypeVertexBufferNvx = 3
        | VkObjectEntryTypePushConstantNvx = 4
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkIndirectCommandsTokenNVX = 
        struct
            val mutable public tokenType : VkIndirectCommandsTokenTypeNVX
            val mutable public buffer : VkBuffer
            val mutable public offset : VkDeviceSize
    
            new(tokenType : VkIndirectCommandsTokenTypeNVX
              , buffer : VkBuffer
              , offset : VkDeviceSize
              ) =
                {
                    tokenType = tokenType
                    buffer = buffer
                    offset = offset
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "tokenType = %A" x.tokenType
                    sprintf "buffer = %A" x.buffer
                    sprintf "offset = %A" x.offset
                ] |> sprintf "VkIndirectCommandsTokenNVX { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkCmdProcessCommandsInfoNVX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public objectTable : VkObjectTableNVX
            val mutable public indirectCommandsLayout : VkIndirectCommandsLayoutNVX
            val mutable public indirectCommandsTokenCount : uint32
            val mutable public pIndirectCommandsTokens : nativeptr<VkIndirectCommandsTokenNVX>
            val mutable public maxSequencesCount : uint32
            val mutable public targetCommandBuffer : VkCommandBuffer
            val mutable public sequencesCountBuffer : VkBuffer
            val mutable public sequencesCountOffset : VkDeviceSize
            val mutable public sequencesIndexBuffer : VkBuffer
            val mutable public sequencesIndexOffset : VkDeviceSize
    
            new(sType : VkStructureType
              , pNext : nativeint
              , objectTable : VkObjectTableNVX
              , indirectCommandsLayout : VkIndirectCommandsLayoutNVX
              , indirectCommandsTokenCount : uint32
              , pIndirectCommandsTokens : nativeptr<VkIndirectCommandsTokenNVX>
              , maxSequencesCount : uint32
              , targetCommandBuffer : VkCommandBuffer
              , sequencesCountBuffer : VkBuffer
              , sequencesCountOffset : VkDeviceSize
              , sequencesIndexBuffer : VkBuffer
              , sequencesIndexOffset : VkDeviceSize
              ) =
                {
                    sType = sType
                    pNext = pNext
                    objectTable = objectTable
                    indirectCommandsLayout = indirectCommandsLayout
                    indirectCommandsTokenCount = indirectCommandsTokenCount
                    pIndirectCommandsTokens = pIndirectCommandsTokens
                    maxSequencesCount = maxSequencesCount
                    targetCommandBuffer = targetCommandBuffer
                    sequencesCountBuffer = sequencesCountBuffer
                    sequencesCountOffset = sequencesCountOffset
                    sequencesIndexBuffer = sequencesIndexBuffer
                    sequencesIndexOffset = sequencesIndexOffset
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "objectTable = %A" x.objectTable
                    sprintf "indirectCommandsLayout = %A" x.indirectCommandsLayout
                    sprintf "indirectCommandsTokenCount = %A" x.indirectCommandsTokenCount
                    sprintf "pIndirectCommandsTokens = %A" x.pIndirectCommandsTokens
                    sprintf "maxSequencesCount = %A" x.maxSequencesCount
                    sprintf "targetCommandBuffer = %A" x.targetCommandBuffer
                    sprintf "sequencesCountBuffer = %A" x.sequencesCountBuffer
                    sprintf "sequencesCountOffset = %A" x.sequencesCountOffset
                    sprintf "sequencesIndexBuffer = %A" x.sequencesIndexBuffer
                    sprintf "sequencesIndexOffset = %A" x.sequencesIndexOffset
                ] |> sprintf "VkCmdProcessCommandsInfoNVX { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkCmdReserveSpaceForCommandsInfoNVX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public objectTable : VkObjectTableNVX
            val mutable public indirectCommandsLayout : VkIndirectCommandsLayoutNVX
            val mutable public maxSequencesCount : uint32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , objectTable : VkObjectTableNVX
              , indirectCommandsLayout : VkIndirectCommandsLayoutNVX
              , maxSequencesCount : uint32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    objectTable = objectTable
                    indirectCommandsLayout = indirectCommandsLayout
                    maxSequencesCount = maxSequencesCount
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "objectTable = %A" x.objectTable
                    sprintf "indirectCommandsLayout = %A" x.indirectCommandsLayout
                    sprintf "maxSequencesCount = %A" x.maxSequencesCount
                ] |> sprintf "VkCmdReserveSpaceForCommandsInfoNVX { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDeviceGeneratedCommandsFeaturesNVX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public computeBindingPointSupport : VkBool32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , computeBindingPointSupport : VkBool32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    computeBindingPointSupport = computeBindingPointSupport
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "computeBindingPointSupport = %A" x.computeBindingPointSupport
                ] |> sprintf "VkDeviceGeneratedCommandsFeaturesNVX { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDeviceGeneratedCommandsLimitsNVX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public maxIndirectCommandsLayoutTokenCount : uint32
            val mutable public maxObjectEntryCounts : uint32
            val mutable public minSequenceCountBufferOffsetAlignment : uint32
            val mutable public minSequenceIndexBufferOffsetAlignment : uint32
            val mutable public minCommandsTokenBufferOffsetAlignment : uint32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , maxIndirectCommandsLayoutTokenCount : uint32
              , maxObjectEntryCounts : uint32
              , minSequenceCountBufferOffsetAlignment : uint32
              , minSequenceIndexBufferOffsetAlignment : uint32
              , minCommandsTokenBufferOffsetAlignment : uint32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    maxIndirectCommandsLayoutTokenCount = maxIndirectCommandsLayoutTokenCount
                    maxObjectEntryCounts = maxObjectEntryCounts
                    minSequenceCountBufferOffsetAlignment = minSequenceCountBufferOffsetAlignment
                    minSequenceIndexBufferOffsetAlignment = minSequenceIndexBufferOffsetAlignment
                    minCommandsTokenBufferOffsetAlignment = minCommandsTokenBufferOffsetAlignment
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "maxIndirectCommandsLayoutTokenCount = %A" x.maxIndirectCommandsLayoutTokenCount
                    sprintf "maxObjectEntryCounts = %A" x.maxObjectEntryCounts
                    sprintf "minSequenceCountBufferOffsetAlignment = %A" x.minSequenceCountBufferOffsetAlignment
                    sprintf "minSequenceIndexBufferOffsetAlignment = %A" x.minSequenceIndexBufferOffsetAlignment
                    sprintf "minCommandsTokenBufferOffsetAlignment = %A" x.minCommandsTokenBufferOffsetAlignment
                ] |> sprintf "VkDeviceGeneratedCommandsLimitsNVX { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkIndirectCommandsLayoutTokenNVX = 
        struct
            val mutable public tokenType : VkIndirectCommandsTokenTypeNVX
            val mutable public bindingUnit : uint32
            val mutable public dynamicCount : uint32
            val mutable public divisor : uint32
    
            new(tokenType : VkIndirectCommandsTokenTypeNVX
              , bindingUnit : uint32
              , dynamicCount : uint32
              , divisor : uint32
              ) =
                {
                    tokenType = tokenType
                    bindingUnit = bindingUnit
                    dynamicCount = dynamicCount
                    divisor = divisor
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "tokenType = %A" x.tokenType
                    sprintf "bindingUnit = %A" x.bindingUnit
                    sprintf "dynamicCount = %A" x.dynamicCount
                    sprintf "divisor = %A" x.divisor
                ] |> sprintf "VkIndirectCommandsLayoutTokenNVX { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkIndirectCommandsLayoutCreateInfoNVX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public pipelineBindPoint : VkPipelineBindPoint
            val mutable public flags : VkIndirectCommandsLayoutUsageFlagsNVX
            val mutable public tokenCount : uint32
            val mutable public pTokens : nativeptr<VkIndirectCommandsLayoutTokenNVX>
    
            new(sType : VkStructureType
              , pNext : nativeint
              , pipelineBindPoint : VkPipelineBindPoint
              , flags : VkIndirectCommandsLayoutUsageFlagsNVX
              , tokenCount : uint32
              , pTokens : nativeptr<VkIndirectCommandsLayoutTokenNVX>
              ) =
                {
                    sType = sType
                    pNext = pNext
                    pipelineBindPoint = pipelineBindPoint
                    flags = flags
                    tokenCount = tokenCount
                    pTokens = pTokens
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "pipelineBindPoint = %A" x.pipelineBindPoint
                    sprintf "flags = %A" x.flags
                    sprintf "tokenCount = %A" x.tokenCount
                    sprintf "pTokens = %A" x.pTokens
                ] |> sprintf "VkIndirectCommandsLayoutCreateInfoNVX { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkObjectTableCreateInfoNVX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public objectCount : uint32
            val mutable public pObjectEntryTypes : nativeptr<VkObjectEntryTypeNVX>
            val mutable public pObjectEntryCounts : nativeptr<uint32>
            val mutable public pObjectEntryUsageFlags : nativeptr<VkObjectEntryUsageFlagsNVX>
            val mutable public maxUniformBuffersPerDescriptor : uint32
            val mutable public maxStorageBuffersPerDescriptor : uint32
            val mutable public maxStorageImagesPerDescriptor : uint32
            val mutable public maxSampledImagesPerDescriptor : uint32
            val mutable public maxPipelineLayouts : uint32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , objectCount : uint32
              , pObjectEntryTypes : nativeptr<VkObjectEntryTypeNVX>
              , pObjectEntryCounts : nativeptr<uint32>
              , pObjectEntryUsageFlags : nativeptr<VkObjectEntryUsageFlagsNVX>
              , maxUniformBuffersPerDescriptor : uint32
              , maxStorageBuffersPerDescriptor : uint32
              , maxStorageImagesPerDescriptor : uint32
              , maxSampledImagesPerDescriptor : uint32
              , maxPipelineLayouts : uint32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    objectCount = objectCount
                    pObjectEntryTypes = pObjectEntryTypes
                    pObjectEntryCounts = pObjectEntryCounts
                    pObjectEntryUsageFlags = pObjectEntryUsageFlags
                    maxUniformBuffersPerDescriptor = maxUniformBuffersPerDescriptor
                    maxStorageBuffersPerDescriptor = maxStorageBuffersPerDescriptor
                    maxStorageImagesPerDescriptor = maxStorageImagesPerDescriptor
                    maxSampledImagesPerDescriptor = maxSampledImagesPerDescriptor
                    maxPipelineLayouts = maxPipelineLayouts
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "objectCount = %A" x.objectCount
                    sprintf "pObjectEntryTypes = %A" x.pObjectEntryTypes
                    sprintf "pObjectEntryCounts = %A" x.pObjectEntryCounts
                    sprintf "pObjectEntryUsageFlags = %A" x.pObjectEntryUsageFlags
                    sprintf "maxUniformBuffersPerDescriptor = %A" x.maxUniformBuffersPerDescriptor
                    sprintf "maxStorageBuffersPerDescriptor = %A" x.maxStorageBuffersPerDescriptor
                    sprintf "maxStorageImagesPerDescriptor = %A" x.maxStorageImagesPerDescriptor
                    sprintf "maxSampledImagesPerDescriptor = %A" x.maxSampledImagesPerDescriptor
                    sprintf "maxPipelineLayouts = %A" x.maxPipelineLayouts
                ] |> sprintf "VkObjectTableCreateInfoNVX { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkObjectTableDescriptorSetEntryNVX = 
        struct
            val mutable public _type : VkObjectEntryTypeNVX
            val mutable public flags : VkObjectEntryUsageFlagsNVX
            val mutable public pipelineLayout : VkPipelineLayout
            val mutable public descriptorSet : VkDescriptorSet
    
            new(_type : VkObjectEntryTypeNVX
              , flags : VkObjectEntryUsageFlagsNVX
              , pipelineLayout : VkPipelineLayout
              , descriptorSet : VkDescriptorSet
              ) =
                {
                    _type = _type
                    flags = flags
                    pipelineLayout = pipelineLayout
                    descriptorSet = descriptorSet
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "_type = %A" x._type
                    sprintf "flags = %A" x.flags
                    sprintf "pipelineLayout = %A" x.pipelineLayout
                    sprintf "descriptorSet = %A" x.descriptorSet
                ] |> sprintf "VkObjectTableDescriptorSetEntryNVX { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkObjectTableEntryNVX = 
        struct
            val mutable public _type : VkObjectEntryTypeNVX
            val mutable public flags : VkObjectEntryUsageFlagsNVX
    
            new(_type : VkObjectEntryTypeNVX
              , flags : VkObjectEntryUsageFlagsNVX
              ) =
                {
                    _type = _type
                    flags = flags
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "_type = %A" x._type
                    sprintf "flags = %A" x.flags
                ] |> sprintf "VkObjectTableEntryNVX { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkObjectTableIndexBufferEntryNVX = 
        struct
            val mutable public _type : VkObjectEntryTypeNVX
            val mutable public flags : VkObjectEntryUsageFlagsNVX
            val mutable public buffer : VkBuffer
            val mutable public indexType : VkIndexType
    
            new(_type : VkObjectEntryTypeNVX
              , flags : VkObjectEntryUsageFlagsNVX
              , buffer : VkBuffer
              , indexType : VkIndexType
              ) =
                {
                    _type = _type
                    flags = flags
                    buffer = buffer
                    indexType = indexType
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "_type = %A" x._type
                    sprintf "flags = %A" x.flags
                    sprintf "buffer = %A" x.buffer
                    sprintf "indexType = %A" x.indexType
                ] |> sprintf "VkObjectTableIndexBufferEntryNVX { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkObjectTablePipelineEntryNVX = 
        struct
            val mutable public _type : VkObjectEntryTypeNVX
            val mutable public flags : VkObjectEntryUsageFlagsNVX
            val mutable public pipeline : VkPipeline
    
            new(_type : VkObjectEntryTypeNVX
              , flags : VkObjectEntryUsageFlagsNVX
              , pipeline : VkPipeline
              ) =
                {
                    _type = _type
                    flags = flags
                    pipeline = pipeline
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "_type = %A" x._type
                    sprintf "flags = %A" x.flags
                    sprintf "pipeline = %A" x.pipeline
                ] |> sprintf "VkObjectTablePipelineEntryNVX { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkObjectTablePushConstantEntryNVX = 
        struct
            val mutable public _type : VkObjectEntryTypeNVX
            val mutable public flags : VkObjectEntryUsageFlagsNVX
            val mutable public pipelineLayout : VkPipelineLayout
            val mutable public stageFlags : VkShaderStageFlags
    
            new(_type : VkObjectEntryTypeNVX
              , flags : VkObjectEntryUsageFlagsNVX
              , pipelineLayout : VkPipelineLayout
              , stageFlags : VkShaderStageFlags
              ) =
                {
                    _type = _type
                    flags = flags
                    pipelineLayout = pipelineLayout
                    stageFlags = stageFlags
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "_type = %A" x._type
                    sprintf "flags = %A" x.flags
                    sprintf "pipelineLayout = %A" x.pipelineLayout
                    sprintf "stageFlags = %A" x.stageFlags
                ] |> sprintf "VkObjectTablePushConstantEntryNVX { %s }"
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkObjectTableVertexBufferEntryNVX = 
        struct
            val mutable public _type : VkObjectEntryTypeNVX
            val mutable public flags : VkObjectEntryUsageFlagsNVX
            val mutable public buffer : VkBuffer
    
            new(_type : VkObjectEntryTypeNVX
              , flags : VkObjectEntryUsageFlagsNVX
              , buffer : VkBuffer
              ) =
                {
                    _type = _type
                    flags = flags
                    buffer = buffer
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "_type = %A" x._type
                    sprintf "flags = %A" x.flags
                    sprintf "buffer = %A" x.buffer
                ] |> sprintf "VkObjectTableVertexBufferEntryNVX { %s }"
        end
    
    
    type VkAccessFlags with
         static member inline CommandProcessReadBitNvx = unbox<VkAccessFlags> 131072
         static member inline CommandProcessWriteBitNvx = unbox<VkAccessFlags> 262144
    type VkObjectType with
         static member inline ObjectTableNvx = unbox<VkObjectType> 1000086000
         static member inline IndirectCommandsLayoutNvx = unbox<VkObjectType> 1000086001
    type VkPipelineStageFlags with
         static member inline CommandProcessBitNvx = unbox<VkPipelineStageFlags> 131072
    type VkStructureType with
         static member inline ObjectTableCreateInfoNvx = unbox<VkStructureType> 1000086000
         static member inline IndirectCommandsLayoutCreateInfoNvx = unbox<VkStructureType> 1000086001
         static member inline CmdProcessCommandsInfoNvx = unbox<VkStructureType> 1000086002
         static member inline CmdReserveSpaceForCommandsInfoNvx = unbox<VkStructureType> 1000086003
         static member inline DeviceGeneratedCommandsLimitsNvx = unbox<VkStructureType> 1000086004
         static member inline DeviceGeneratedCommandsFeaturesNvx = unbox<VkStructureType> 1000086005
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdProcessCommandsNVXDel = delegate of VkCommandBuffer * nativeptr<VkCmdProcessCommandsInfoNVX> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdReserveSpaceForCommandsNVXDel = delegate of VkCommandBuffer * nativeptr<VkCmdReserveSpaceForCommandsInfoNVX> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateIndirectCommandsLayoutNVXDel = delegate of VkDevice * nativeptr<VkIndirectCommandsLayoutCreateInfoNVX> * nativeptr<VkAllocationCallbacks> * nativeptr<VkIndirectCommandsLayoutNVX> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkDestroyIndirectCommandsLayoutNVXDel = delegate of VkDevice * VkIndirectCommandsLayoutNVX * nativeptr<VkAllocationCallbacks> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateObjectTableNVXDel = delegate of VkDevice * nativeptr<VkObjectTableCreateInfoNVX> * nativeptr<VkAllocationCallbacks> * nativeptr<VkObjectTableNVX> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkDestroyObjectTableNVXDel = delegate of VkDevice * VkObjectTableNVX * nativeptr<VkAllocationCallbacks> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkRegisterObjectsNVXDel = delegate of VkDevice * VkObjectTableNVX * uint32 * nativeptr<nativeptr<VkObjectTableEntryNVX>> * nativeptr<uint32> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkUnregisterObjectsNVXDel = delegate of VkDevice * VkObjectTableNVX * uint32 * nativeptr<VkObjectEntryTypeNVX> * nativeptr<uint32> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceGeneratedCommandsPropertiesNVXDel = delegate of VkPhysicalDevice * nativeptr<VkDeviceGeneratedCommandsFeaturesNVX> * nativeptr<VkDeviceGeneratedCommandsLimitsNVX> -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_NVX_device_generated_commands")
            static let s_vkCmdProcessCommandsNVXDel = VkRaw.vkImportInstanceDelegate<VkCmdProcessCommandsNVXDel> "vkCmdProcessCommandsNVX"
            static let s_vkCmdReserveSpaceForCommandsNVXDel = VkRaw.vkImportInstanceDelegate<VkCmdReserveSpaceForCommandsNVXDel> "vkCmdReserveSpaceForCommandsNVX"
            static let s_vkCreateIndirectCommandsLayoutNVXDel = VkRaw.vkImportInstanceDelegate<VkCreateIndirectCommandsLayoutNVXDel> "vkCreateIndirectCommandsLayoutNVX"
            static let s_vkDestroyIndirectCommandsLayoutNVXDel = VkRaw.vkImportInstanceDelegate<VkDestroyIndirectCommandsLayoutNVXDel> "vkDestroyIndirectCommandsLayoutNVX"
            static let s_vkCreateObjectTableNVXDel = VkRaw.vkImportInstanceDelegate<VkCreateObjectTableNVXDel> "vkCreateObjectTableNVX"
            static let s_vkDestroyObjectTableNVXDel = VkRaw.vkImportInstanceDelegate<VkDestroyObjectTableNVXDel> "vkDestroyObjectTableNVX"
            static let s_vkRegisterObjectsNVXDel = VkRaw.vkImportInstanceDelegate<VkRegisterObjectsNVXDel> "vkRegisterObjectsNVX"
            static let s_vkUnregisterObjectsNVXDel = VkRaw.vkImportInstanceDelegate<VkUnregisterObjectsNVXDel> "vkUnregisterObjectsNVX"
            static let s_vkGetPhysicalDeviceGeneratedCommandsPropertiesNVXDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceGeneratedCommandsPropertiesNVXDel> "vkGetPhysicalDeviceGeneratedCommandsPropertiesNVX"
            static do Report.End(3) |> ignore
            static member vkCmdProcessCommandsNVX = s_vkCmdProcessCommandsNVXDel
            static member vkCmdReserveSpaceForCommandsNVX = s_vkCmdReserveSpaceForCommandsNVXDel
            static member vkCreateIndirectCommandsLayoutNVX = s_vkCreateIndirectCommandsLayoutNVXDel
            static member vkDestroyIndirectCommandsLayoutNVX = s_vkDestroyIndirectCommandsLayoutNVXDel
            static member vkCreateObjectTableNVX = s_vkCreateObjectTableNVXDel
            static member vkDestroyObjectTableNVX = s_vkDestroyObjectTableNVXDel
            static member vkRegisterObjectsNVX = s_vkRegisterObjectsNVXDel
            static member vkUnregisterObjectsNVX = s_vkUnregisterObjectsNVXDel
            static member vkGetPhysicalDeviceGeneratedCommandsPropertiesNVX = s_vkGetPhysicalDeviceGeneratedCommandsPropertiesNVXDel
        let vkCmdProcessCommandsNVX(commandBuffer : VkCommandBuffer, pProcessCommandsInfo : nativeptr<VkCmdProcessCommandsInfoNVX>) = Loader<unit>.vkCmdProcessCommandsNVX.Invoke(commandBuffer, pProcessCommandsInfo)
        let vkCmdReserveSpaceForCommandsNVX(commandBuffer : VkCommandBuffer, pReserveSpaceInfo : nativeptr<VkCmdReserveSpaceForCommandsInfoNVX>) = Loader<unit>.vkCmdReserveSpaceForCommandsNVX.Invoke(commandBuffer, pReserveSpaceInfo)
        let vkCreateIndirectCommandsLayoutNVX(device : VkDevice, pCreateInfo : nativeptr<VkIndirectCommandsLayoutCreateInfoNVX>, pAllocator : nativeptr<VkAllocationCallbacks>, pIndirectCommandsLayout : nativeptr<VkIndirectCommandsLayoutNVX>) = Loader<unit>.vkCreateIndirectCommandsLayoutNVX.Invoke(device, pCreateInfo, pAllocator, pIndirectCommandsLayout)
        let vkDestroyIndirectCommandsLayoutNVX(device : VkDevice, indirectCommandsLayout : VkIndirectCommandsLayoutNVX, pAllocator : nativeptr<VkAllocationCallbacks>) = Loader<unit>.vkDestroyIndirectCommandsLayoutNVX.Invoke(device, indirectCommandsLayout, pAllocator)
        let vkCreateObjectTableNVX(device : VkDevice, pCreateInfo : nativeptr<VkObjectTableCreateInfoNVX>, pAllocator : nativeptr<VkAllocationCallbacks>, pObjectTable : nativeptr<VkObjectTableNVX>) = Loader<unit>.vkCreateObjectTableNVX.Invoke(device, pCreateInfo, pAllocator, pObjectTable)
        let vkDestroyObjectTableNVX(device : VkDevice, objectTable : VkObjectTableNVX, pAllocator : nativeptr<VkAllocationCallbacks>) = Loader<unit>.vkDestroyObjectTableNVX.Invoke(device, objectTable, pAllocator)
        let vkRegisterObjectsNVX(device : VkDevice, objectTable : VkObjectTableNVX, objectCount : uint32, ppObjectTableEntries : nativeptr<nativeptr<VkObjectTableEntryNVX>>, pObjectIndices : nativeptr<uint32>) = Loader<unit>.vkRegisterObjectsNVX.Invoke(device, objectTable, objectCount, ppObjectTableEntries, pObjectIndices)
        let vkUnregisterObjectsNVX(device : VkDevice, objectTable : VkObjectTableNVX, objectCount : uint32, pObjectEntryTypes : nativeptr<VkObjectEntryTypeNVX>, pObjectIndices : nativeptr<uint32>) = Loader<unit>.vkUnregisterObjectsNVX.Invoke(device, objectTable, objectCount, pObjectEntryTypes, pObjectIndices)
        let vkGetPhysicalDeviceGeneratedCommandsPropertiesNVX(physicalDevice : VkPhysicalDevice, pFeatures : nativeptr<VkDeviceGeneratedCommandsFeaturesNVX>, pLimits : nativeptr<VkDeviceGeneratedCommandsLimitsNVX>) = Loader<unit>.vkGetPhysicalDeviceGeneratedCommandsPropertiesNVX.Invoke(physicalDevice, pFeatures, pLimits)

module NVXMultiviewPerViewAttributes =
    let Name = "VK_NVX_multiview_per_view_attributes"
    let Number = 98
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name; KHRMultiview.Name ]
    open KHRGetPhysicalDeviceProperties2
    open KHRMultiview
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceMultiviewPerViewAttributesPropertiesNVX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public perViewPositionAllComponents : VkBool32
    
            new(sType : VkStructureType
              , pNext : nativeint
              , perViewPositionAllComponents : VkBool32
              ) =
                {
                    sType = sType
                    pNext = pNext
                    perViewPositionAllComponents = perViewPositionAllComponents
                }
            override x.ToString() =
                String.concat "; " [
                    sprintf "sType = %A" x.sType
                    sprintf "pNext = %A" x.pNext
                    sprintf "perViewPositionAllComponents = %A" x.perViewPositionAllComponents
                ] |> sprintf "VkPhysicalDeviceMultiviewPerViewAttributesPropertiesNVX { %s }"
        end
    
    
    type VkStructureType with
         static member inline PhysicalDeviceMultiviewPerViewAttributesPropertiesNvx = unbox<VkStructureType> 1000097000
    type VkSubpassDescriptionFlags with
         static member inline PerViewAttributesBitNvx = unbox<VkSubpassDescriptionFlags> 1
         static member inline PerViewPositionXOnlyBitNvx = unbox<VkSubpassDescriptionFlags> 2
    
