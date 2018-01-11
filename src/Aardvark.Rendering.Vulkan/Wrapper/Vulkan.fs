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
type VkDisplayPlaneAlphaFlagsKHR = | MinValue = 0
type VkDisplaySurfaceCreateFlagsKHR = | MinValue = 0
type VkSwapchainCreateFlagsKHR = | MinValue = 0
type VkSwapchainCreateFlagBitsKHR = | MinValue = 0
type VkSurfaceTransformFlagsKHR = | MinValue = 0
type VkCompositeAlphaFlagsKHR = | MinValue = 0
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
type VkDebugReportFlagsEXT = | MinValue = 0
type PFN_vkDebugReportCallbackEXT = nativeint

type VkExternalMemoryHandleTypeFlagsNV = | MinValue = 0
type VkExternalMemoryFeatureFlagsNV = | MinValue = 0
type VkIndirectCommandsLayoutUsageFlagsNVX = | MinValue = 0
type VkObjectEntryUsageFlagsNVX = | MinValue = 0

type VkDescriptorUpdateTemplateCreateFlagsKHR = | MinValue = 0
type VkDeviceGroupPresentModeFlagsKHX = | MinValue = 0
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
type VkCommandPoolTrimFlagsKHR = | MinValue = 0
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
type VkDescriptorUpdateTemplateKHR = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkDescriptorUpdateTemplateKHR(0L)
        member x.IsNull = x.Handle = 0L
        member x.IsValid = x.Handle <> 0L
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSamplerYcbcrConversionKHR = 
    struct
        val mutable public Handle : int64
        new(h) = { Handle = h }
        static member Null = VkSamplerYcbcrConversionKHR(0L)
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


type VkSampleMask = uint32
type VkBool32 = uint32
type VkFlags = uint32
type VkDeviceSize = uint64
type VkImageLayout = 
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

type VkObjectType = 
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
    | GraphicsBit = 0x00000001
    | ComputeBit = 0x00000002
    | TransferBit = 0x00000004
    | SparseBindingBit = 0x00000008

[<Flags>]
type VkMemoryPropertyFlags = 
    | None = 0
    | DeviceLocalBit = 0x00000001
    | HostVisibleBit = 0x00000002
    | HostCoherentBit = 0x00000004
    | HostCachedBit = 0x00000008
    | LazilyAllocatedBit = 0x00000010

[<Flags>]
type VkMemoryHeapFlags = 
    | None = 0
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
    | SparseBindingBit = 0x00000001
    | SparseResidencyBit = 0x00000002
    | SparseAliasedBit = 0x00000004
    | MutableFormatBit = 0x00000008
    | CubeCompatibleBit = 0x00000010

[<Flags>]
type VkPipelineCreateFlags = 
    | None = 0
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
    | ByRegionBit = 0x00000001

type VkPresentModeKHR = 
    | VkPresentModeImmediateKhr = 0
    | VkPresentModeMailboxKhr = 1
    | VkPresentModeFifoKhr = 2
    | VkPresentModeFifoRelaxedKhr = 3

type VkColorSpaceKHR = 
    | VkColorSpaceSrgbNonlinearKhr = 0

[<Flags>]
type VkCompositeAlphaFlagBitsKHR = 
    | None = 0
    | VkCompositeAlphaOpaqueBitKhr = 0x00000001
    | VkCompositeAlphaPreMultipliedBitKhr = 0x00000002
    | VkCompositeAlphaPostMultipliedBitKhr = 0x00000004
    | VkCompositeAlphaInheritBitKhr = 0x00000008

[<Flags>]
type VkSurfaceTransformFlagBitsKHR = 
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
type VkDebugReportFlagBitsEXT = 
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
type VkDeviceGroupPresentModeFlagBitsKHX = 
    | None = 0
    | VkDeviceGroupPresentModeLocalBitKhx = 0x00000001
    | VkDeviceGroupPresentModeRemoteBitKhx = 0x00000002
    | VkDeviceGroupPresentModeSumBitKhx = 0x00000004
    | VkDeviceGroupPresentModeLocalMultiDeviceBitKhx = 0x00000008

type VkSamplerYcbcrModelConversionKHR = 
    | VkSamplerYcbcrModelConversionRgbIdentityKhr = 0
    | VkSamplerYcbcrModelConversionYcbcrIdentityKhr = 1
    | VkSamplerYcbcrModelConversionYcbcr709Khr = 2
    | VkSamplerYcbcrModelConversionYcbcr601Khr = 3
    | VkSamplerYcbcrModelConversionYcbcr2020Khr = 4

type VkSamplerYcbcrRangeKHR = 
    | VkSamplerYcbcrRangeItuFullKhr = 0
    | VkSamplerYcbcrRangeItuNarrowKhr = 1

type VkChromaLocationKHR = 
    | VkChromaLocationCositedEvenKhr = 0
    | VkChromaLocationMidpointKhr = 1

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

        new(pUserData : nativeint, pfnAllocation : PFN_vkAllocationFunction, pfnReallocation : PFN_vkReallocationFunction, pfnFree : PFN_vkFreeFunction, pfnInternalAllocation : PFN_vkInternalAllocationNotification, pfnInternalFree : PFN_vkInternalFreeNotification) = { pUserData = pUserData; pfnAllocation = pfnAllocation; pfnReallocation = pfnReallocation; pfnFree = pfnFree; pfnInternalAllocation = pfnInternalAllocation; pfnInternalFree = pfnInternalFree }
        override x.ToString() =
            sprintf "VkAllocationCallbacks { pUserData = %A; pfnAllocation = %A; pfnReallocation = %A; pfnFree = %A; pfnInternalAllocation = %A; pfnInternalFree = %A }" x.pUserData x.pfnAllocation x.pfnReallocation x.pfnFree x.pfnInternalAllocation x.pfnInternalFree
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

        new(sType : VkStructureType, pNext : nativeint, pApplicationName : cstr, applicationVersion : uint32, pEngineName : cstr, engineVersion : uint32, apiVersion : uint32) = { sType = sType; pNext = pNext; pApplicationName = pApplicationName; applicationVersion = applicationVersion; pEngineName = pEngineName; engineVersion = engineVersion; apiVersion = apiVersion }
        override x.ToString() =
            sprintf "VkApplicationInfo { sType = %A; pNext = %A; pApplicationName = %A; applicationVersion = %A; pEngineName = %A; engineVersion = %A; apiVersion = %A }" x.sType x.pNext x.pApplicationName x.applicationVersion x.pEngineName x.engineVersion x.apiVersion
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

        new(flags : VkAttachmentDescriptionFlags, format : VkFormat, samples : VkSampleCountFlags, loadOp : VkAttachmentLoadOp, storeOp : VkAttachmentStoreOp, stencilLoadOp : VkAttachmentLoadOp, stencilStoreOp : VkAttachmentStoreOp, initialLayout : VkImageLayout, finalLayout : VkImageLayout) = { flags = flags; format = format; samples = samples; loadOp = loadOp; storeOp = storeOp; stencilLoadOp = stencilLoadOp; stencilStoreOp = stencilStoreOp; initialLayout = initialLayout; finalLayout = finalLayout }
        override x.ToString() =
            sprintf "VkAttachmentDescription { flags = %A; format = %A; samples = %A; loadOp = %A; storeOp = %A; stencilLoadOp = %A; stencilStoreOp = %A; initialLayout = %A; finalLayout = %A }" x.flags x.format x.samples x.loadOp x.storeOp x.stencilLoadOp x.stencilStoreOp x.initialLayout x.finalLayout
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkAttachmentReference = 
    struct
        val mutable public attachment : uint32
        val mutable public layout : VkImageLayout

        new(attachment : uint32, layout : VkImageLayout) = { attachment = attachment; layout = layout }
        override x.ToString() =
            sprintf "VkAttachmentReference { attachment = %A; layout = %A }" x.attachment x.layout
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBindBufferMemoryDeviceGroupInfoKHX = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public deviceIndexCount : uint32
        val mutable public pDeviceIndices : nativeptr<uint32>

        new(sType : VkStructureType, pNext : nativeint, deviceIndexCount : uint32, pDeviceIndices : nativeptr<uint32>) = { sType = sType; pNext = pNext; deviceIndexCount = deviceIndexCount; pDeviceIndices = pDeviceIndices }
        override x.ToString() =
            sprintf "VkBindBufferMemoryDeviceGroupInfoKHX { sType = %A; pNext = %A; deviceIndexCount = %A; pDeviceIndices = %A }" x.sType x.pNext x.deviceIndexCount x.pDeviceIndices
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkOffset2D = 
    struct
        val mutable public x : int
        val mutable public y : int

        new(x : int, y : int) = { x = x; y = y }
        override x.ToString() =
            sprintf "VkOffset2D { x = %A; y = %A }" x.x x.y
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkExtent2D = 
    struct
        val mutable public width : uint32
        val mutable public height : uint32

        new(width : uint32, height : uint32) = { width = width; height = height }
        new(w : int, h : int) = VkExtent2D(uint32 w,uint32 h)
        override x.ToString() =
            sprintf "VkExtent2D { width = %A; height = %A }" x.width x.height
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkRect2D = 
    struct
        val mutable public offset : VkOffset2D
        val mutable public extent : VkExtent2D

        new(offset : VkOffset2D, extent : VkExtent2D) = { offset = offset; extent = extent }
        override x.ToString() =
            sprintf "VkRect2D { offset = %A; extent = %A }" x.offset x.extent
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBindImageMemoryDeviceGroupInfoKHX = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public deviceIndexCount : uint32
        val mutable public pDeviceIndices : nativeptr<uint32>
        val mutable public _SFRRectCount : uint32
        val mutable public pSFRRects : nativeptr<VkRect2D>

        new(sType : VkStructureType, pNext : nativeint, deviceIndexCount : uint32, pDeviceIndices : nativeptr<uint32>, _SFRRectCount : uint32, pSFRRects : nativeptr<VkRect2D>) = { sType = sType; pNext = pNext; deviceIndexCount = deviceIndexCount; pDeviceIndices = pDeviceIndices; _SFRRectCount = _SFRRectCount; pSFRRects = pSFRRects }
        override x.ToString() =
            sprintf "VkBindImageMemoryDeviceGroupInfoKHX { sType = %A; pNext = %A; deviceIndexCount = %A; pDeviceIndices = %A; _SFRRectCount = %A; pSFRRects = %A }" x.sType x.pNext x.deviceIndexCount x.pDeviceIndices x._SFRRectCount x.pSFRRects
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSparseMemoryBind = 
    struct
        val mutable public resourceOffset : VkDeviceSize
        val mutable public size : VkDeviceSize
        val mutable public memory : VkDeviceMemory
        val mutable public memoryOffset : VkDeviceSize
        val mutable public flags : VkSparseMemoryBindFlags

        new(resourceOffset : VkDeviceSize, size : VkDeviceSize, memory : VkDeviceMemory, memoryOffset : VkDeviceSize, flags : VkSparseMemoryBindFlags) = { resourceOffset = resourceOffset; size = size; memory = memory; memoryOffset = memoryOffset; flags = flags }
        override x.ToString() =
            sprintf "VkSparseMemoryBind { resourceOffset = %A; size = %A; memory = %A; memoryOffset = %A; flags = %A }" x.resourceOffset x.size x.memory x.memoryOffset x.flags
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSparseBufferMemoryBindInfo = 
    struct
        val mutable public buffer : VkBuffer
        val mutable public bindCount : uint32
        val mutable public pBinds : nativeptr<VkSparseMemoryBind>

        new(buffer : VkBuffer, bindCount : uint32, pBinds : nativeptr<VkSparseMemoryBind>) = { buffer = buffer; bindCount = bindCount; pBinds = pBinds }
        override x.ToString() =
            sprintf "VkSparseBufferMemoryBindInfo { buffer = %A; bindCount = %A; pBinds = %A }" x.buffer x.bindCount x.pBinds
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSparseImageOpaqueMemoryBindInfo = 
    struct
        val mutable public image : VkImage
        val mutable public bindCount : uint32
        val mutable public pBinds : nativeptr<VkSparseMemoryBind>

        new(image : VkImage, bindCount : uint32, pBinds : nativeptr<VkSparseMemoryBind>) = { image = image; bindCount = bindCount; pBinds = pBinds }
        override x.ToString() =
            sprintf "VkSparseImageOpaqueMemoryBindInfo { image = %A; bindCount = %A; pBinds = %A }" x.image x.bindCount x.pBinds
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageSubresource = 
    struct
        val mutable public aspectMask : VkImageAspectFlags
        val mutable public mipLevel : uint32
        val mutable public arrayLayer : uint32

        new(aspectMask : VkImageAspectFlags, mipLevel : uint32, arrayLayer : uint32) = { aspectMask = aspectMask; mipLevel = mipLevel; arrayLayer = arrayLayer }
        override x.ToString() =
            sprintf "VkImageSubresource { aspectMask = %A; mipLevel = %A; arrayLayer = %A }" x.aspectMask x.mipLevel x.arrayLayer
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkOffset3D = 
    struct
        val mutable public x : int
        val mutable public y : int
        val mutable public z : int

        new(x : int, y : int, z : int) = { x = x; y = y; z = z }
        override x.ToString() =
            sprintf "VkOffset3D { x = %A; y = %A; z = %A }" x.x x.y x.z
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

        new(width : uint32, height : uint32, depth : uint32) = { width = width; height = height; depth = depth }
        new(w : int, h : int, d : int) = VkExtent3D(uint32 w,uint32 h,uint32 d)
        override x.ToString() =
            sprintf "VkExtent3D { width = %A; height = %A; depth = %A }" x.width x.height x.depth
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

        new(subresource : VkImageSubresource, offset : VkOffset3D, extent : VkExtent3D, memory : VkDeviceMemory, memoryOffset : VkDeviceSize, flags : VkSparseMemoryBindFlags) = { subresource = subresource; offset = offset; extent = extent; memory = memory; memoryOffset = memoryOffset; flags = flags }
        override x.ToString() =
            sprintf "VkSparseImageMemoryBind { subresource = %A; offset = %A; extent = %A; memory = %A; memoryOffset = %A; flags = %A }" x.subresource x.offset x.extent x.memory x.memoryOffset x.flags
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSparseImageMemoryBindInfo = 
    struct
        val mutable public image : VkImage
        val mutable public bindCount : uint32
        val mutable public pBinds : nativeptr<VkSparseImageMemoryBind>

        new(image : VkImage, bindCount : uint32, pBinds : nativeptr<VkSparseImageMemoryBind>) = { image = image; bindCount = bindCount; pBinds = pBinds }
        override x.ToString() =
            sprintf "VkSparseImageMemoryBindInfo { image = %A; bindCount = %A; pBinds = %A }" x.image x.bindCount x.pBinds
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

        new(sType : VkStructureType, pNext : nativeint, waitSemaphoreCount : uint32, pWaitSemaphores : nativeptr<VkSemaphore>, bufferBindCount : uint32, pBufferBinds : nativeptr<VkSparseBufferMemoryBindInfo>, imageOpaqueBindCount : uint32, pImageOpaqueBinds : nativeptr<VkSparseImageOpaqueMemoryBindInfo>, imageBindCount : uint32, pImageBinds : nativeptr<VkSparseImageMemoryBindInfo>, signalSemaphoreCount : uint32, pSignalSemaphores : nativeptr<VkSemaphore>) = { sType = sType; pNext = pNext; waitSemaphoreCount = waitSemaphoreCount; pWaitSemaphores = pWaitSemaphores; bufferBindCount = bufferBindCount; pBufferBinds = pBufferBinds; imageOpaqueBindCount = imageOpaqueBindCount; pImageOpaqueBinds = pImageOpaqueBinds; imageBindCount = imageBindCount; pImageBinds = pImageBinds; signalSemaphoreCount = signalSemaphoreCount; pSignalSemaphores = pSignalSemaphores }
        override x.ToString() =
            sprintf "VkBindSparseInfo { sType = %A; pNext = %A; waitSemaphoreCount = %A; pWaitSemaphores = %A; bufferBindCount = %A; pBufferBinds = %A; imageOpaqueBindCount = %A; pImageOpaqueBinds = %A; imageBindCount = %A; pImageBinds = %A; signalSemaphoreCount = %A; pSignalSemaphores = %A }" x.sType x.pNext x.waitSemaphoreCount x.pWaitSemaphores x.bufferBindCount x.pBufferBinds x.imageOpaqueBindCount x.pImageOpaqueBinds x.imageBindCount x.pImageBinds x.signalSemaphoreCount x.pSignalSemaphores
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBufferCopy = 
    struct
        val mutable public srcOffset : VkDeviceSize
        val mutable public dstOffset : VkDeviceSize
        val mutable public size : VkDeviceSize

        new(srcOffset : VkDeviceSize, dstOffset : VkDeviceSize, size : VkDeviceSize) = { srcOffset = srcOffset; dstOffset = dstOffset; size = size }
        override x.ToString() =
            sprintf "VkBufferCopy { srcOffset = %A; dstOffset = %A; size = %A }" x.srcOffset x.dstOffset x.size
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkBufferCreateFlags, size : VkDeviceSize, usage : VkBufferUsageFlags, sharingMode : VkSharingMode, queueFamilyIndexCount : uint32, pQueueFamilyIndices : nativeptr<uint32>) = { sType = sType; pNext = pNext; flags = flags; size = size; usage = usage; sharingMode = sharingMode; queueFamilyIndexCount = queueFamilyIndexCount; pQueueFamilyIndices = pQueueFamilyIndices }
        override x.ToString() =
            sprintf "VkBufferCreateInfo { sType = %A; pNext = %A; flags = %A; size = %A; usage = %A; sharingMode = %A; queueFamilyIndexCount = %A; pQueueFamilyIndices = %A }" x.sType x.pNext x.flags x.size x.usage x.sharingMode x.queueFamilyIndexCount x.pQueueFamilyIndices
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageSubresourceLayers = 
    struct
        val mutable public aspectMask : VkImageAspectFlags
        val mutable public mipLevel : uint32
        val mutable public baseArrayLayer : uint32
        val mutable public layerCount : uint32

        new(aspectMask : VkImageAspectFlags, mipLevel : uint32, baseArrayLayer : uint32, layerCount : uint32) = { aspectMask = aspectMask; mipLevel = mipLevel; baseArrayLayer = baseArrayLayer; layerCount = layerCount }
        override x.ToString() =
            sprintf "VkImageSubresourceLayers { aspectMask = %A; mipLevel = %A; baseArrayLayer = %A; layerCount = %A }" x.aspectMask x.mipLevel x.baseArrayLayer x.layerCount
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

        new(bufferOffset : VkDeviceSize, bufferRowLength : uint32, bufferImageHeight : uint32, imageSubresource : VkImageSubresourceLayers, imageOffset : VkOffset3D, imageExtent : VkExtent3D) = { bufferOffset = bufferOffset; bufferRowLength = bufferRowLength; bufferImageHeight = bufferImageHeight; imageSubresource = imageSubresource; imageOffset = imageOffset; imageExtent = imageExtent }
        override x.ToString() =
            sprintf "VkBufferImageCopy { bufferOffset = %A; bufferRowLength = %A; bufferImageHeight = %A; imageSubresource = %A; imageOffset = %A; imageExtent = %A }" x.bufferOffset x.bufferRowLength x.bufferImageHeight x.imageSubresource x.imageOffset x.imageExtent
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

        new(sType : VkStructureType, pNext : nativeint, srcAccessMask : VkAccessFlags, dstAccessMask : VkAccessFlags, srcQueueFamilyIndex : uint32, dstQueueFamilyIndex : uint32, buffer : VkBuffer, offset : VkDeviceSize, size : VkDeviceSize) = { sType = sType; pNext = pNext; srcAccessMask = srcAccessMask; dstAccessMask = dstAccessMask; srcQueueFamilyIndex = srcQueueFamilyIndex; dstQueueFamilyIndex = dstQueueFamilyIndex; buffer = buffer; offset = offset; size = size }
        override x.ToString() =
            sprintf "VkBufferMemoryBarrier { sType = %A; pNext = %A; srcAccessMask = %A; dstAccessMask = %A; srcQueueFamilyIndex = %A; dstQueueFamilyIndex = %A; buffer = %A; offset = %A; size = %A }" x.sType x.pNext x.srcAccessMask x.dstAccessMask x.srcQueueFamilyIndex x.dstQueueFamilyIndex x.buffer x.offset x.size
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkBufferViewCreateFlags, buffer : VkBuffer, format : VkFormat, offset : VkDeviceSize, range : VkDeviceSize) = { sType = sType; pNext = pNext; flags = flags; buffer = buffer; format = format; offset = offset; range = range }
        override x.ToString() =
            sprintf "VkBufferViewCreateInfo { sType = %A; pNext = %A; flags = %A; buffer = %A; format = %A; offset = %A; range = %A }" x.sType x.pNext x.flags x.buffer x.format x.offset x.range
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
            sprintf "VkClearColorValue { float32 = %A; int32 = %A; uint32 = %A }" x.float32 x.int32 x.uint32
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkClearDepthStencilValue = 
    struct
        val mutable public depth : float32
        val mutable public stencil : uint32

        new(depth : float32, stencil : uint32) = { depth = depth; stencil = stencil }
        override x.ToString() =
            sprintf "VkClearDepthStencilValue { depth = %A; stencil = %A }" x.depth x.stencil
    end

[<StructLayout(LayoutKind.Explicit)>]
type VkClearValue = 
    struct
        [<FieldOffset(0)>]
        val mutable public color : VkClearColorValue
        [<FieldOffset(0)>]
        val mutable public depthStencil : VkClearDepthStencilValue
        override x.ToString() =
            sprintf "VkClearValue { color = %A; depthStencil = %A }" x.color x.depthStencil
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkClearAttachment = 
    struct
        val mutable public aspectMask : VkImageAspectFlags
        val mutable public colorAttachment : uint32
        val mutable public clearValue : VkClearValue

        new(aspectMask : VkImageAspectFlags, colorAttachment : uint32, clearValue : VkClearValue) = { aspectMask = aspectMask; colorAttachment = colorAttachment; clearValue = clearValue }
        override x.ToString() =
            sprintf "VkClearAttachment { aspectMask = %A; colorAttachment = %A; clearValue = %A }" x.aspectMask x.colorAttachment x.clearValue
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkClearRect = 
    struct
        val mutable public rect : VkRect2D
        val mutable public baseArrayLayer : uint32
        val mutable public layerCount : uint32

        new(rect : VkRect2D, baseArrayLayer : uint32, layerCount : uint32) = { rect = rect; baseArrayLayer = baseArrayLayer; layerCount = layerCount }
        override x.ToString() =
            sprintf "VkClearRect { rect = %A; baseArrayLayer = %A; layerCount = %A }" x.rect x.baseArrayLayer x.layerCount
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkCommandBufferAllocateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public commandPool : VkCommandPool
        val mutable public level : VkCommandBufferLevel
        val mutable public commandBufferCount : uint32

        new(sType : VkStructureType, pNext : nativeint, commandPool : VkCommandPool, level : VkCommandBufferLevel, commandBufferCount : uint32) = { sType = sType; pNext = pNext; commandPool = commandPool; level = level; commandBufferCount = commandBufferCount }
        override x.ToString() =
            sprintf "VkCommandBufferAllocateInfo { sType = %A; pNext = %A; commandPool = %A; level = %A; commandBufferCount = %A }" x.sType x.pNext x.commandPool x.level x.commandBufferCount
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

        new(sType : VkStructureType, pNext : nativeint, renderPass : VkRenderPass, subpass : uint32, framebuffer : VkFramebuffer, occlusionQueryEnable : VkBool32, queryFlags : VkQueryControlFlags, pipelineStatistics : VkQueryPipelineStatisticFlags) = { sType = sType; pNext = pNext; renderPass = renderPass; subpass = subpass; framebuffer = framebuffer; occlusionQueryEnable = occlusionQueryEnable; queryFlags = queryFlags; pipelineStatistics = pipelineStatistics }
        override x.ToString() =
            sprintf "VkCommandBufferInheritanceInfo { sType = %A; pNext = %A; renderPass = %A; subpass = %A; framebuffer = %A; occlusionQueryEnable = %A; queryFlags = %A; pipelineStatistics = %A }" x.sType x.pNext x.renderPass x.subpass x.framebuffer x.occlusionQueryEnable x.queryFlags x.pipelineStatistics
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkCommandBufferBeginInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkCommandBufferUsageFlags
        val mutable public pInheritanceInfo : nativeptr<VkCommandBufferInheritanceInfo>

        new(sType : VkStructureType, pNext : nativeint, flags : VkCommandBufferUsageFlags, pInheritanceInfo : nativeptr<VkCommandBufferInheritanceInfo>) = { sType = sType; pNext = pNext; flags = flags; pInheritanceInfo = pInheritanceInfo }
        override x.ToString() =
            sprintf "VkCommandBufferBeginInfo { sType = %A; pNext = %A; flags = %A; pInheritanceInfo = %A }" x.sType x.pNext x.flags x.pInheritanceInfo
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkCommandPoolCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkCommandPoolCreateFlags
        val mutable public queueFamilyIndex : uint32

        new(sType : VkStructureType, pNext : nativeint, flags : VkCommandPoolCreateFlags, queueFamilyIndex : uint32) = { sType = sType; pNext = pNext; flags = flags; queueFamilyIndex = queueFamilyIndex }
        override x.ToString() =
            sprintf "VkCommandPoolCreateInfo { sType = %A; pNext = %A; flags = %A; queueFamilyIndex = %A }" x.sType x.pNext x.flags x.queueFamilyIndex
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkComponentMapping = 
    struct
        val mutable public r : VkComponentSwizzle
        val mutable public g : VkComponentSwizzle
        val mutable public b : VkComponentSwizzle
        val mutable public a : VkComponentSwizzle

        new(r : VkComponentSwizzle, g : VkComponentSwizzle, b : VkComponentSwizzle, a : VkComponentSwizzle) = { r = r; g = g; b = b; a = a }
        override x.ToString() =
            sprintf "VkComponentMapping { r = %A; g = %A; b = %A; a = %A }" x.r x.g x.b x.a
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSpecializationMapEntry = 
    struct
        val mutable public constantID : uint32
        val mutable public offset : uint32
        val mutable public size : uint64

        new(constantID : uint32, offset : uint32, size : uint64) = { constantID = constantID; offset = offset; size = size }
        override x.ToString() =
            sprintf "VkSpecializationMapEntry { constantID = %A; offset = %A; size = %A }" x.constantID x.offset x.size
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSpecializationInfo = 
    struct
        val mutable public mapEntryCount : uint32
        val mutable public pMapEntries : nativeptr<VkSpecializationMapEntry>
        val mutable public dataSize : uint64
        val mutable public pData : nativeint

        new(mapEntryCount : uint32, pMapEntries : nativeptr<VkSpecializationMapEntry>, dataSize : uint64, pData : nativeint) = { mapEntryCount = mapEntryCount; pMapEntries = pMapEntries; dataSize = dataSize; pData = pData }
        override x.ToString() =
            sprintf "VkSpecializationInfo { mapEntryCount = %A; pMapEntries = %A; dataSize = %A; pData = %A }" x.mapEntryCount x.pMapEntries x.dataSize x.pData
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineShaderStageCreateFlags, stage : VkShaderStageFlags, _module : VkShaderModule, pName : cstr, pSpecializationInfo : nativeptr<VkSpecializationInfo>) = { sType = sType; pNext = pNext; flags = flags; stage = stage; _module = _module; pName = pName; pSpecializationInfo = pSpecializationInfo }
        override x.ToString() =
            sprintf "VkPipelineShaderStageCreateInfo { sType = %A; pNext = %A; flags = %A; stage = %A; _module = %A; pName = %A; pSpecializationInfo = %A }" x.sType x.pNext x.flags x.stage x._module x.pName x.pSpecializationInfo
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineCreateFlags, stage : VkPipelineShaderStageCreateInfo, layout : VkPipelineLayout, basePipelineHandle : VkPipeline, basePipelineIndex : int) = { sType = sType; pNext = pNext; flags = flags; stage = stage; layout = layout; basePipelineHandle = basePipelineHandle; basePipelineIndex = basePipelineIndex }
        override x.ToString() =
            sprintf "VkComputePipelineCreateInfo { sType = %A; pNext = %A; flags = %A; stage = %A; layout = %A; basePipelineHandle = %A; basePipelineIndex = %A }" x.sType x.pNext x.flags x.stage x.layout x.basePipelineHandle x.basePipelineIndex
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

        new(sType : VkStructureType, pNext : nativeint, srcSet : VkDescriptorSet, srcBinding : uint32, srcArrayElement : uint32, dstSet : VkDescriptorSet, dstBinding : uint32, dstArrayElement : uint32, descriptorCount : uint32) = { sType = sType; pNext = pNext; srcSet = srcSet; srcBinding = srcBinding; srcArrayElement = srcArrayElement; dstSet = dstSet; dstBinding = dstBinding; dstArrayElement = dstArrayElement; descriptorCount = descriptorCount }
        override x.ToString() =
            sprintf "VkCopyDescriptorSet { sType = %A; pNext = %A; srcSet = %A; srcBinding = %A; srcArrayElement = %A; dstSet = %A; dstBinding = %A; dstArrayElement = %A; descriptorCount = %A }" x.sType x.pNext x.srcSet x.srcBinding x.srcArrayElement x.dstSet x.dstBinding x.dstArrayElement x.descriptorCount
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorBufferInfo = 
    struct
        val mutable public buffer : VkBuffer
        val mutable public offset : VkDeviceSize
        val mutable public range : VkDeviceSize

        new(buffer : VkBuffer, offset : VkDeviceSize, range : VkDeviceSize) = { buffer = buffer; offset = offset; range = range }
        override x.ToString() =
            sprintf "VkDescriptorBufferInfo { buffer = %A; offset = %A; range = %A }" x.buffer x.offset x.range
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorImageInfo = 
    struct
        val mutable public sampler : VkSampler
        val mutable public imageView : VkImageView
        val mutable public imageLayout : VkImageLayout

        new(sampler : VkSampler, imageView : VkImageView, imageLayout : VkImageLayout) = { sampler = sampler; imageView = imageView; imageLayout = imageLayout }
        override x.ToString() =
            sprintf "VkDescriptorImageInfo { sampler = %A; imageView = %A; imageLayout = %A }" x.sampler x.imageView x.imageLayout
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorPoolSize = 
    struct
        val mutable public _type : VkDescriptorType
        val mutable public descriptorCount : uint32

        new(_type : VkDescriptorType, descriptorCount : uint32) = { _type = _type; descriptorCount = descriptorCount }
        override x.ToString() =
            sprintf "VkDescriptorPoolSize { _type = %A; descriptorCount = %A }" x._type x.descriptorCount
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkDescriptorPoolCreateFlags, maxSets : uint32, poolSizeCount : uint32, pPoolSizes : nativeptr<VkDescriptorPoolSize>) = { sType = sType; pNext = pNext; flags = flags; maxSets = maxSets; poolSizeCount = poolSizeCount; pPoolSizes = pPoolSizes }
        override x.ToString() =
            sprintf "VkDescriptorPoolCreateInfo { sType = %A; pNext = %A; flags = %A; maxSets = %A; poolSizeCount = %A; pPoolSizes = %A }" x.sType x.pNext x.flags x.maxSets x.poolSizeCount x.pPoolSizes
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorSetAllocateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public descriptorPool : VkDescriptorPool
        val mutable public descriptorSetCount : uint32
        val mutable public pSetLayouts : nativeptr<VkDescriptorSetLayout>

        new(sType : VkStructureType, pNext : nativeint, descriptorPool : VkDescriptorPool, descriptorSetCount : uint32, pSetLayouts : nativeptr<VkDescriptorSetLayout>) = { sType = sType; pNext = pNext; descriptorPool = descriptorPool; descriptorSetCount = descriptorSetCount; pSetLayouts = pSetLayouts }
        override x.ToString() =
            sprintf "VkDescriptorSetAllocateInfo { sType = %A; pNext = %A; descriptorPool = %A; descriptorSetCount = %A; pSetLayouts = %A }" x.sType x.pNext x.descriptorPool x.descriptorSetCount x.pSetLayouts
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorSetLayoutBinding = 
    struct
        val mutable public binding : uint32
        val mutable public descriptorType : VkDescriptorType
        val mutable public descriptorCount : uint32
        val mutable public stageFlags : VkShaderStageFlags
        val mutable public pImmutableSamplers : nativeptr<VkSampler>

        new(binding : uint32, descriptorType : VkDescriptorType, descriptorCount : uint32, stageFlags : VkShaderStageFlags, pImmutableSamplers : nativeptr<VkSampler>) = { binding = binding; descriptorType = descriptorType; descriptorCount = descriptorCount; stageFlags = stageFlags; pImmutableSamplers = pImmutableSamplers }
        override x.ToString() =
            sprintf "VkDescriptorSetLayoutBinding { binding = %A; descriptorType = %A; descriptorCount = %A; stageFlags = %A; pImmutableSamplers = %A }" x.binding x.descriptorType x.descriptorCount x.stageFlags x.pImmutableSamplers
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorSetLayoutCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkDescriptorSetLayoutCreateFlags
        val mutable public bindingCount : uint32
        val mutable public pBindings : nativeptr<VkDescriptorSetLayoutBinding>

        new(sType : VkStructureType, pNext : nativeint, flags : VkDescriptorSetLayoutCreateFlags, bindingCount : uint32, pBindings : nativeptr<VkDescriptorSetLayoutBinding>) = { sType = sType; pNext = pNext; flags = flags; bindingCount = bindingCount; pBindings = pBindings }
        override x.ToString() =
            sprintf "VkDescriptorSetLayoutCreateInfo { sType = %A; pNext = %A; flags = %A; bindingCount = %A; pBindings = %A }" x.sType x.pNext x.flags x.bindingCount x.pBindings
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkDeviceQueueCreateFlags, queueFamilyIndex : uint32, queueCount : uint32, pQueuePriorities : nativeptr<float32>) = { sType = sType; pNext = pNext; flags = flags; queueFamilyIndex = queueFamilyIndex; queueCount = queueCount; pQueuePriorities = pQueuePriorities }
        override x.ToString() =
            sprintf "VkDeviceQueueCreateInfo { sType = %A; pNext = %A; flags = %A; queueFamilyIndex = %A; queueCount = %A; pQueuePriorities = %A }" x.sType x.pNext x.flags x.queueFamilyIndex x.queueCount x.pQueuePriorities
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

        new(robustBufferAccess : VkBool32, fullDrawIndexUint32 : VkBool32, imageCubeArray : VkBool32, independentBlend : VkBool32, geometryShader : VkBool32, tessellationShader : VkBool32, sampleRateShading : VkBool32, dualSrcBlend : VkBool32, logicOp : VkBool32, multiDrawIndirect : VkBool32, drawIndirectFirstInstance : VkBool32, depthClamp : VkBool32, depthBiasClamp : VkBool32, fillModeNonSolid : VkBool32, depthBounds : VkBool32, wideLines : VkBool32, largePoints : VkBool32, alphaToOne : VkBool32, multiViewport : VkBool32, samplerAnisotropy : VkBool32, textureCompressionETC2 : VkBool32, textureCompressionASTC_LDR : VkBool32, textureCompressionBC : VkBool32, occlusionQueryPrecise : VkBool32, pipelineStatisticsQuery : VkBool32, vertexPipelineStoresAndAtomics : VkBool32, fragmentStoresAndAtomics : VkBool32, shaderTessellationAndGeometryPointSize : VkBool32, shaderImageGatherExtended : VkBool32, shaderStorageImageExtendedFormats : VkBool32, shaderStorageImageMultisample : VkBool32, shaderStorageImageReadWithoutFormat : VkBool32, shaderStorageImageWriteWithoutFormat : VkBool32, shaderUniformBufferArrayDynamicIndexing : VkBool32, shaderSampledImageArrayDynamicIndexing : VkBool32, shaderStorageBufferArrayDynamicIndexing : VkBool32, shaderStorageImageArrayDynamicIndexing : VkBool32, shaderClipDistance : VkBool32, shaderCullDistance : VkBool32, shaderFloat64 : VkBool32, shaderInt64 : VkBool32, shaderInt16 : VkBool32, shaderResourceResidency : VkBool32, shaderResourceMinLod : VkBool32, sparseBinding : VkBool32, sparseResidencyBuffer : VkBool32, sparseResidencyImage2D : VkBool32, sparseResidencyImage3D : VkBool32, sparseResidency2Samples : VkBool32, sparseResidency4Samples : VkBool32, sparseResidency8Samples : VkBool32, sparseResidency16Samples : VkBool32, sparseResidencyAliased : VkBool32, variableMultisampleRate : VkBool32, inheritedQueries : VkBool32) = { robustBufferAccess = robustBufferAccess; fullDrawIndexUint32 = fullDrawIndexUint32; imageCubeArray = imageCubeArray; independentBlend = independentBlend; geometryShader = geometryShader; tessellationShader = tessellationShader; sampleRateShading = sampleRateShading; dualSrcBlend = dualSrcBlend; logicOp = logicOp; multiDrawIndirect = multiDrawIndirect; drawIndirectFirstInstance = drawIndirectFirstInstance; depthClamp = depthClamp; depthBiasClamp = depthBiasClamp; fillModeNonSolid = fillModeNonSolid; depthBounds = depthBounds; wideLines = wideLines; largePoints = largePoints; alphaToOne = alphaToOne; multiViewport = multiViewport; samplerAnisotropy = samplerAnisotropy; textureCompressionETC2 = textureCompressionETC2; textureCompressionASTC_LDR = textureCompressionASTC_LDR; textureCompressionBC = textureCompressionBC; occlusionQueryPrecise = occlusionQueryPrecise; pipelineStatisticsQuery = pipelineStatisticsQuery; vertexPipelineStoresAndAtomics = vertexPipelineStoresAndAtomics; fragmentStoresAndAtomics = fragmentStoresAndAtomics; shaderTessellationAndGeometryPointSize = shaderTessellationAndGeometryPointSize; shaderImageGatherExtended = shaderImageGatherExtended; shaderStorageImageExtendedFormats = shaderStorageImageExtendedFormats; shaderStorageImageMultisample = shaderStorageImageMultisample; shaderStorageImageReadWithoutFormat = shaderStorageImageReadWithoutFormat; shaderStorageImageWriteWithoutFormat = shaderStorageImageWriteWithoutFormat; shaderUniformBufferArrayDynamicIndexing = shaderUniformBufferArrayDynamicIndexing; shaderSampledImageArrayDynamicIndexing = shaderSampledImageArrayDynamicIndexing; shaderStorageBufferArrayDynamicIndexing = shaderStorageBufferArrayDynamicIndexing; shaderStorageImageArrayDynamicIndexing = shaderStorageImageArrayDynamicIndexing; shaderClipDistance = shaderClipDistance; shaderCullDistance = shaderCullDistance; shaderFloat64 = shaderFloat64; shaderInt64 = shaderInt64; shaderInt16 = shaderInt16; shaderResourceResidency = shaderResourceResidency; shaderResourceMinLod = shaderResourceMinLod; sparseBinding = sparseBinding; sparseResidencyBuffer = sparseResidencyBuffer; sparseResidencyImage2D = sparseResidencyImage2D; sparseResidencyImage3D = sparseResidencyImage3D; sparseResidency2Samples = sparseResidency2Samples; sparseResidency4Samples = sparseResidency4Samples; sparseResidency8Samples = sparseResidency8Samples; sparseResidency16Samples = sparseResidency16Samples; sparseResidencyAliased = sparseResidencyAliased; variableMultisampleRate = variableMultisampleRate; inheritedQueries = inheritedQueries }
        override x.ToString() =
            sprintf "VkPhysicalDeviceFeatures { robustBufferAccess = %A; fullDrawIndexUint32 = %A; imageCubeArray = %A; independentBlend = %A; geometryShader = %A; tessellationShader = %A; sampleRateShading = %A; dualSrcBlend = %A; logicOp = %A; multiDrawIndirect = %A; drawIndirectFirstInstance = %A; depthClamp = %A; depthBiasClamp = %A; fillModeNonSolid = %A; depthBounds = %A; wideLines = %A; largePoints = %A; alphaToOne = %A; multiViewport = %A; samplerAnisotropy = %A; textureCompressionETC2 = %A; textureCompressionASTC_LDR = %A; textureCompressionBC = %A; occlusionQueryPrecise = %A; pipelineStatisticsQuery = %A; vertexPipelineStoresAndAtomics = %A; fragmentStoresAndAtomics = %A; shaderTessellationAndGeometryPointSize = %A; shaderImageGatherExtended = %A; shaderStorageImageExtendedFormats = %A; shaderStorageImageMultisample = %A; shaderStorageImageReadWithoutFormat = %A; shaderStorageImageWriteWithoutFormat = %A; shaderUniformBufferArrayDynamicIndexing = %A; shaderSampledImageArrayDynamicIndexing = %A; shaderStorageBufferArrayDynamicIndexing = %A; shaderStorageImageArrayDynamicIndexing = %A; shaderClipDistance = %A; shaderCullDistance = %A; shaderFloat64 = %A; shaderInt64 = %A; shaderInt16 = %A; shaderResourceResidency = %A; shaderResourceMinLod = %A; sparseBinding = %A; sparseResidencyBuffer = %A; sparseResidencyImage2D = %A; sparseResidencyImage3D = %A; sparseResidency2Samples = %A; sparseResidency4Samples = %A; sparseResidency8Samples = %A; sparseResidency16Samples = %A; sparseResidencyAliased = %A; variableMultisampleRate = %A; inheritedQueries = %A }" x.robustBufferAccess x.fullDrawIndexUint32 x.imageCubeArray x.independentBlend x.geometryShader x.tessellationShader x.sampleRateShading x.dualSrcBlend x.logicOp x.multiDrawIndirect x.drawIndirectFirstInstance x.depthClamp x.depthBiasClamp x.fillModeNonSolid x.depthBounds x.wideLines x.largePoints x.alphaToOne x.multiViewport x.samplerAnisotropy x.textureCompressionETC2 x.textureCompressionASTC_LDR x.textureCompressionBC x.occlusionQueryPrecise x.pipelineStatisticsQuery x.vertexPipelineStoresAndAtomics x.fragmentStoresAndAtomics x.shaderTessellationAndGeometryPointSize x.shaderImageGatherExtended x.shaderStorageImageExtendedFormats x.shaderStorageImageMultisample x.shaderStorageImageReadWithoutFormat x.shaderStorageImageWriteWithoutFormat x.shaderUniformBufferArrayDynamicIndexing x.shaderSampledImageArrayDynamicIndexing x.shaderStorageBufferArrayDynamicIndexing x.shaderStorageImageArrayDynamicIndexing x.shaderClipDistance x.shaderCullDistance x.shaderFloat64 x.shaderInt64 x.shaderInt16 x.shaderResourceResidency x.shaderResourceMinLod x.sparseBinding x.sparseResidencyBuffer x.sparseResidencyImage2D x.sparseResidencyImage3D x.sparseResidency2Samples x.sparseResidency4Samples x.sparseResidency8Samples x.sparseResidency16Samples x.sparseResidencyAliased x.variableMultisampleRate x.inheritedQueries
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkDeviceCreateFlags, queueCreateInfoCount : uint32, pQueueCreateInfos : nativeptr<VkDeviceQueueCreateInfo>, enabledLayerCount : uint32, ppEnabledLayerNames : nativeptr<cstr>, enabledExtensionCount : uint32, ppEnabledExtensionNames : nativeptr<cstr>, pEnabledFeatures : nativeptr<VkPhysicalDeviceFeatures>) = { sType = sType; pNext = pNext; flags = flags; queueCreateInfoCount = queueCreateInfoCount; pQueueCreateInfos = pQueueCreateInfos; enabledLayerCount = enabledLayerCount; ppEnabledLayerNames = ppEnabledLayerNames; enabledExtensionCount = enabledExtensionCount; ppEnabledExtensionNames = ppEnabledExtensionNames; pEnabledFeatures = pEnabledFeatures }
        override x.ToString() =
            sprintf "VkDeviceCreateInfo { sType = %A; pNext = %A; flags = %A; queueCreateInfoCount = %A; pQueueCreateInfos = %A; enabledLayerCount = %A; ppEnabledLayerNames = %A; enabledExtensionCount = %A; ppEnabledExtensionNames = %A; pEnabledFeatures = %A }" x.sType x.pNext x.flags x.queueCreateInfoCount x.pQueueCreateInfos x.enabledLayerCount x.ppEnabledLayerNames x.enabledExtensionCount x.ppEnabledExtensionNames x.pEnabledFeatures
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDeviceGroupPresentCapabilitiesKHX = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public presentMask : uint32_32
        val mutable public modes : VkDeviceGroupPresentModeFlagsKHX

        new(sType : VkStructureType, pNext : nativeint, presentMask : uint32_32, modes : VkDeviceGroupPresentModeFlagsKHX) = { sType = sType; pNext = pNext; presentMask = presentMask; modes = modes }
        override x.ToString() =
            sprintf "VkDeviceGroupPresentCapabilitiesKHX { sType = %A; pNext = %A; presentMask = %A; modes = %A }" x.sType x.pNext x.presentMask x.modes
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDispatchIndirectCommand = 
    struct
        val mutable public x : uint32
        val mutable public y : uint32
        val mutable public z : uint32

        new(x : uint32, y : uint32, z : uint32) = { x = x; y = y; z = z }
        override x.ToString() =
            sprintf "VkDispatchIndirectCommand { x = %A; y = %A; z = %A }" x.x x.y x.z
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDrawIndexedIndirectCommand = 
    struct
        val mutable public indexCount : uint32
        val mutable public instanceCount : uint32
        val mutable public firstIndex : uint32
        val mutable public vertexOffset : int
        val mutable public firstInstance : uint32

        new(indexCount : uint32, instanceCount : uint32, firstIndex : uint32, vertexOffset : int, firstInstance : uint32) = { indexCount = indexCount; instanceCount = instanceCount; firstIndex = firstIndex; vertexOffset = vertexOffset; firstInstance = firstInstance }
        override x.ToString() =
            sprintf "VkDrawIndexedIndirectCommand { indexCount = %A; instanceCount = %A; firstIndex = %A; vertexOffset = %A; firstInstance = %A }" x.indexCount x.instanceCount x.firstIndex x.vertexOffset x.firstInstance
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDrawIndirectCommand = 
    struct
        val mutable public vertexCount : uint32
        val mutable public instanceCount : uint32
        val mutable public firstVertex : uint32
        val mutable public firstInstance : uint32

        new(vertexCount : uint32, instanceCount : uint32, firstVertex : uint32, firstInstance : uint32) = { vertexCount = vertexCount; instanceCount = instanceCount; firstVertex = firstVertex; firstInstance = firstInstance }
        override x.ToString() =
            sprintf "VkDrawIndirectCommand { vertexCount = %A; instanceCount = %A; firstVertex = %A; firstInstance = %A }" x.vertexCount x.instanceCount x.firstVertex x.firstInstance
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkEventCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkEventCreateFlags

        new(sType : VkStructureType, pNext : nativeint, flags : VkEventCreateFlags) = { sType = sType; pNext = pNext; flags = flags }
        override x.ToString() =
            sprintf "VkEventCreateInfo { sType = %A; pNext = %A; flags = %A }" x.sType x.pNext x.flags
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkExtensionProperties = 
    struct
        val mutable public extensionName : String256
        val mutable public specVersion : uint32

        new(extensionName : String256, specVersion : uint32) = { extensionName = extensionName; specVersion = specVersion }
        override x.ToString() =
            sprintf "VkExtensionProperties { extensionName = %A; specVersion = %A }" x.extensionName x.specVersion
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkFenceCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkFenceCreateFlags

        new(sType : VkStructureType, pNext : nativeint, flags : VkFenceCreateFlags) = { sType = sType; pNext = pNext; flags = flags }
        override x.ToString() =
            sprintf "VkFenceCreateInfo { sType = %A; pNext = %A; flags = %A }" x.sType x.pNext x.flags
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkFormatProperties = 
    struct
        val mutable public linearTilingFeatures : VkFormatFeatureFlags
        val mutable public optimalTilingFeatures : VkFormatFeatureFlags
        val mutable public bufferFeatures : VkFormatFeatureFlags

        new(linearTilingFeatures : VkFormatFeatureFlags, optimalTilingFeatures : VkFormatFeatureFlags, bufferFeatures : VkFormatFeatureFlags) = { linearTilingFeatures = linearTilingFeatures; optimalTilingFeatures = optimalTilingFeatures; bufferFeatures = bufferFeatures }
        override x.ToString() =
            sprintf "VkFormatProperties { linearTilingFeatures = %A; optimalTilingFeatures = %A; bufferFeatures = %A }" x.linearTilingFeatures x.optimalTilingFeatures x.bufferFeatures
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkFramebufferCreateFlags, renderPass : VkRenderPass, attachmentCount : uint32, pAttachments : nativeptr<VkImageView>, width : uint32, height : uint32, layers : uint32) = { sType = sType; pNext = pNext; flags = flags; renderPass = renderPass; attachmentCount = attachmentCount; pAttachments = pAttachments; width = width; height = height; layers = layers }
        override x.ToString() =
            sprintf "VkFramebufferCreateInfo { sType = %A; pNext = %A; flags = %A; renderPass = %A; attachmentCount = %A; pAttachments = %A; width = %A; height = %A; layers = %A }" x.sType x.pNext x.flags x.renderPass x.attachmentCount x.pAttachments x.width x.height x.layers
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkVertexInputBindingDescription = 
    struct
        val mutable public binding : uint32
        val mutable public stride : uint32
        val mutable public inputRate : VkVertexInputRate

        new(binding : uint32, stride : uint32, inputRate : VkVertexInputRate) = { binding = binding; stride = stride; inputRate = inputRate }
        override x.ToString() =
            sprintf "VkVertexInputBindingDescription { binding = %A; stride = %A; inputRate = %A }" x.binding x.stride x.inputRate
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkVertexInputAttributeDescription = 
    struct
        val mutable public location : uint32
        val mutable public binding : uint32
        val mutable public format : VkFormat
        val mutable public offset : uint32

        new(location : uint32, binding : uint32, format : VkFormat, offset : uint32) = { location = location; binding = binding; format = format; offset = offset }
        override x.ToString() =
            sprintf "VkVertexInputAttributeDescription { location = %A; binding = %A; format = %A; offset = %A }" x.location x.binding x.format x.offset
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineVertexInputStateCreateFlags, vertexBindingDescriptionCount : uint32, pVertexBindingDescriptions : nativeptr<VkVertexInputBindingDescription>, vertexAttributeDescriptionCount : uint32, pVertexAttributeDescriptions : nativeptr<VkVertexInputAttributeDescription>) = { sType = sType; pNext = pNext; flags = flags; vertexBindingDescriptionCount = vertexBindingDescriptionCount; pVertexBindingDescriptions = pVertexBindingDescriptions; vertexAttributeDescriptionCount = vertexAttributeDescriptionCount; pVertexAttributeDescriptions = pVertexAttributeDescriptions }
        override x.ToString() =
            sprintf "VkPipelineVertexInputStateCreateInfo { sType = %A; pNext = %A; flags = %A; vertexBindingDescriptionCount = %A; pVertexBindingDescriptions = %A; vertexAttributeDescriptionCount = %A; pVertexAttributeDescriptions = %A }" x.sType x.pNext x.flags x.vertexBindingDescriptionCount x.pVertexBindingDescriptions x.vertexAttributeDescriptionCount x.pVertexAttributeDescriptions
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineInputAssemblyStateCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineInputAssemblyStateCreateFlags
        val mutable public topology : VkPrimitiveTopology
        val mutable public primitiveRestartEnable : VkBool32

        new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineInputAssemblyStateCreateFlags, topology : VkPrimitiveTopology, primitiveRestartEnable : VkBool32) = { sType = sType; pNext = pNext; flags = flags; topology = topology; primitiveRestartEnable = primitiveRestartEnable }
        override x.ToString() =
            sprintf "VkPipelineInputAssemblyStateCreateInfo { sType = %A; pNext = %A; flags = %A; topology = %A; primitiveRestartEnable = %A }" x.sType x.pNext x.flags x.topology x.primitiveRestartEnable
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineTessellationStateCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineTessellationStateCreateFlags
        val mutable public patchControlPoints : uint32

        new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineTessellationStateCreateFlags, patchControlPoints : uint32) = { sType = sType; pNext = pNext; flags = flags; patchControlPoints = patchControlPoints }
        override x.ToString() =
            sprintf "VkPipelineTessellationStateCreateInfo { sType = %A; pNext = %A; flags = %A; patchControlPoints = %A }" x.sType x.pNext x.flags x.patchControlPoints
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

        new(x : float32, y : float32, width : float32, height : float32, minDepth : float32, maxDepth : float32) = { x = x; y = y; width = width; height = height; minDepth = minDepth; maxDepth = maxDepth }
        override x.ToString() =
            sprintf "VkViewport { x = %A; y = %A; width = %A; height = %A; minDepth = %A; maxDepth = %A }" x.x x.y x.width x.height x.minDepth x.maxDepth
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineViewportStateCreateFlags, viewportCount : uint32, pViewports : nativeptr<VkViewport>, scissorCount : uint32, pScissors : nativeptr<VkRect2D>) = { sType = sType; pNext = pNext; flags = flags; viewportCount = viewportCount; pViewports = pViewports; scissorCount = scissorCount; pScissors = pScissors }
        override x.ToString() =
            sprintf "VkPipelineViewportStateCreateInfo { sType = %A; pNext = %A; flags = %A; viewportCount = %A; pViewports = %A; scissorCount = %A; pScissors = %A }" x.sType x.pNext x.flags x.viewportCount x.pViewports x.scissorCount x.pScissors
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineRasterizationStateCreateFlags, depthClampEnable : VkBool32, rasterizerDiscardEnable : VkBool32, polygonMode : VkPolygonMode, cullMode : VkCullModeFlags, frontFace : VkFrontFace, depthBiasEnable : VkBool32, depthBiasConstantFactor : float32, depthBiasClamp : float32, depthBiasSlopeFactor : float32, lineWidth : float32) = { sType = sType; pNext = pNext; flags = flags; depthClampEnable = depthClampEnable; rasterizerDiscardEnable = rasterizerDiscardEnable; polygonMode = polygonMode; cullMode = cullMode; frontFace = frontFace; depthBiasEnable = depthBiasEnable; depthBiasConstantFactor = depthBiasConstantFactor; depthBiasClamp = depthBiasClamp; depthBiasSlopeFactor = depthBiasSlopeFactor; lineWidth = lineWidth }
        override x.ToString() =
            sprintf "VkPipelineRasterizationStateCreateInfo { sType = %A; pNext = %A; flags = %A; depthClampEnable = %A; rasterizerDiscardEnable = %A; polygonMode = %A; cullMode = %A; frontFace = %A; depthBiasEnable = %A; depthBiasConstantFactor = %A; depthBiasClamp = %A; depthBiasSlopeFactor = %A; lineWidth = %A }" x.sType x.pNext x.flags x.depthClampEnable x.rasterizerDiscardEnable x.polygonMode x.cullMode x.frontFace x.depthBiasEnable x.depthBiasConstantFactor x.depthBiasClamp x.depthBiasSlopeFactor x.lineWidth
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineMultisampleStateCreateFlags, rasterizationSamples : VkSampleCountFlags, sampleShadingEnable : VkBool32, minSampleShading : float32, pSampleMask : nativeptr<VkSampleMask>, alphaToCoverageEnable : VkBool32, alphaToOneEnable : VkBool32) = { sType = sType; pNext = pNext; flags = flags; rasterizationSamples = rasterizationSamples; sampleShadingEnable = sampleShadingEnable; minSampleShading = minSampleShading; pSampleMask = pSampleMask; alphaToCoverageEnable = alphaToCoverageEnable; alphaToOneEnable = alphaToOneEnable }
        override x.ToString() =
            sprintf "VkPipelineMultisampleStateCreateInfo { sType = %A; pNext = %A; flags = %A; rasterizationSamples = %A; sampleShadingEnable = %A; minSampleShading = %A; pSampleMask = %A; alphaToCoverageEnable = %A; alphaToOneEnable = %A }" x.sType x.pNext x.flags x.rasterizationSamples x.sampleShadingEnable x.minSampleShading x.pSampleMask x.alphaToCoverageEnable x.alphaToOneEnable
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

        new(failOp : VkStencilOp, passOp : VkStencilOp, depthFailOp : VkStencilOp, compareOp : VkCompareOp, compareMask : uint32, writeMask : uint32, reference : uint32) = { failOp = failOp; passOp = passOp; depthFailOp = depthFailOp; compareOp = compareOp; compareMask = compareMask; writeMask = writeMask; reference = reference }
        override x.ToString() =
            sprintf "VkStencilOpState { failOp = %A; passOp = %A; depthFailOp = %A; compareOp = %A; compareMask = %A; writeMask = %A; reference = %A }" x.failOp x.passOp x.depthFailOp x.compareOp x.compareMask x.writeMask x.reference
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineDepthStencilStateCreateFlags, depthTestEnable : VkBool32, depthWriteEnable : VkBool32, depthCompareOp : VkCompareOp, depthBoundsTestEnable : VkBool32, stencilTestEnable : VkBool32, front : VkStencilOpState, back : VkStencilOpState, minDepthBounds : float32, maxDepthBounds : float32) = { sType = sType; pNext = pNext; flags = flags; depthTestEnable = depthTestEnable; depthWriteEnable = depthWriteEnable; depthCompareOp = depthCompareOp; depthBoundsTestEnable = depthBoundsTestEnable; stencilTestEnable = stencilTestEnable; front = front; back = back; minDepthBounds = minDepthBounds; maxDepthBounds = maxDepthBounds }
        override x.ToString() =
            sprintf "VkPipelineDepthStencilStateCreateInfo { sType = %A; pNext = %A; flags = %A; depthTestEnable = %A; depthWriteEnable = %A; depthCompareOp = %A; depthBoundsTestEnable = %A; stencilTestEnable = %A; front = %A; back = %A; minDepthBounds = %A; maxDepthBounds = %A }" x.sType x.pNext x.flags x.depthTestEnable x.depthWriteEnable x.depthCompareOp x.depthBoundsTestEnable x.stencilTestEnable x.front x.back x.minDepthBounds x.maxDepthBounds
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

        new(blendEnable : VkBool32, srcColorBlendFactor : VkBlendFactor, dstColorBlendFactor : VkBlendFactor, colorBlendOp : VkBlendOp, srcAlphaBlendFactor : VkBlendFactor, dstAlphaBlendFactor : VkBlendFactor, alphaBlendOp : VkBlendOp, colorWriteMask : VkColorComponentFlags) = { blendEnable = blendEnable; srcColorBlendFactor = srcColorBlendFactor; dstColorBlendFactor = dstColorBlendFactor; colorBlendOp = colorBlendOp; srcAlphaBlendFactor = srcAlphaBlendFactor; dstAlphaBlendFactor = dstAlphaBlendFactor; alphaBlendOp = alphaBlendOp; colorWriteMask = colorWriteMask }
        override x.ToString() =
            sprintf "VkPipelineColorBlendAttachmentState { blendEnable = %A; srcColorBlendFactor = %A; dstColorBlendFactor = %A; colorBlendOp = %A; srcAlphaBlendFactor = %A; dstAlphaBlendFactor = %A; alphaBlendOp = %A; colorWriteMask = %A }" x.blendEnable x.srcColorBlendFactor x.dstColorBlendFactor x.colorBlendOp x.srcAlphaBlendFactor x.dstAlphaBlendFactor x.alphaBlendOp x.colorWriteMask
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineColorBlendStateCreateFlags, logicOpEnable : VkBool32, logicOp : VkLogicOp, attachmentCount : uint32, pAttachments : nativeptr<VkPipelineColorBlendAttachmentState>, blendConstants : V4f) = { sType = sType; pNext = pNext; flags = flags; logicOpEnable = logicOpEnable; logicOp = logicOp; attachmentCount = attachmentCount; pAttachments = pAttachments; blendConstants = blendConstants }
        override x.ToString() =
            sprintf "VkPipelineColorBlendStateCreateInfo { sType = %A; pNext = %A; flags = %A; logicOpEnable = %A; logicOp = %A; attachmentCount = %A; pAttachments = %A; blendConstants = %A }" x.sType x.pNext x.flags x.logicOpEnable x.logicOp x.attachmentCount x.pAttachments x.blendConstants
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineDynamicStateCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineDynamicStateCreateFlags
        val mutable public dynamicStateCount : uint32
        val mutable public pDynamicStates : nativeptr<VkDynamicState>

        new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineDynamicStateCreateFlags, dynamicStateCount : uint32, pDynamicStates : nativeptr<VkDynamicState>) = { sType = sType; pNext = pNext; flags = flags; dynamicStateCount = dynamicStateCount; pDynamicStates = pDynamicStates }
        override x.ToString() =
            sprintf "VkPipelineDynamicStateCreateInfo { sType = %A; pNext = %A; flags = %A; dynamicStateCount = %A; pDynamicStates = %A }" x.sType x.pNext x.flags x.dynamicStateCount x.pDynamicStates
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineCreateFlags, stageCount : uint32, pStages : nativeptr<VkPipelineShaderStageCreateInfo>, pVertexInputState : nativeptr<VkPipelineVertexInputStateCreateInfo>, pInputAssemblyState : nativeptr<VkPipelineInputAssemblyStateCreateInfo>, pTessellationState : nativeptr<VkPipelineTessellationStateCreateInfo>, pViewportState : nativeptr<VkPipelineViewportStateCreateInfo>, pRasterizationState : nativeptr<VkPipelineRasterizationStateCreateInfo>, pMultisampleState : nativeptr<VkPipelineMultisampleStateCreateInfo>, pDepthStencilState : nativeptr<VkPipelineDepthStencilStateCreateInfo>, pColorBlendState : nativeptr<VkPipelineColorBlendStateCreateInfo>, pDynamicState : nativeptr<VkPipelineDynamicStateCreateInfo>, layout : VkPipelineLayout, renderPass : VkRenderPass, subpass : uint32, basePipelineHandle : VkPipeline, basePipelineIndex : int) = { sType = sType; pNext = pNext; flags = flags; stageCount = stageCount; pStages = pStages; pVertexInputState = pVertexInputState; pInputAssemblyState = pInputAssemblyState; pTessellationState = pTessellationState; pViewportState = pViewportState; pRasterizationState = pRasterizationState; pMultisampleState = pMultisampleState; pDepthStencilState = pDepthStencilState; pColorBlendState = pColorBlendState; pDynamicState = pDynamicState; layout = layout; renderPass = renderPass; subpass = subpass; basePipelineHandle = basePipelineHandle; basePipelineIndex = basePipelineIndex }
        override x.ToString() =
            sprintf "VkGraphicsPipelineCreateInfo { sType = %A; pNext = %A; flags = %A; stageCount = %A; pStages = %A; pVertexInputState = %A; pInputAssemblyState = %A; pTessellationState = %A; pViewportState = %A; pRasterizationState = %A; pMultisampleState = %A; pDepthStencilState = %A; pColorBlendState = %A; pDynamicState = %A; layout = %A; renderPass = %A; subpass = %A; basePipelineHandle = %A; basePipelineIndex = %A }" x.sType x.pNext x.flags x.stageCount x.pStages x.pVertexInputState x.pInputAssemblyState x.pTessellationState x.pViewportState x.pRasterizationState x.pMultisampleState x.pDepthStencilState x.pColorBlendState x.pDynamicState x.layout x.renderPass x.subpass x.basePipelineHandle x.basePipelineIndex
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageBlit = 
    struct
        val mutable public srcSubresource : VkImageSubresourceLayers
        val mutable public srcOffsets : VkOffset3D_2
        val mutable public dstSubresource : VkImageSubresourceLayers
        val mutable public dstOffsets : VkOffset3D_2

        new(srcSubresource : VkImageSubresourceLayers, srcOffsets : VkOffset3D_2, dstSubresource : VkImageSubresourceLayers, dstOffsets : VkOffset3D_2) = { srcSubresource = srcSubresource; srcOffsets = srcOffsets; dstSubresource = dstSubresource; dstOffsets = dstOffsets }
        override x.ToString() =
            sprintf "VkImageBlit { srcSubresource = %A; srcOffsets = %A; dstSubresource = %A; dstOffsets = %A }" x.srcSubresource x.srcOffsets x.dstSubresource x.dstOffsets
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageCopy = 
    struct
        val mutable public srcSubresource : VkImageSubresourceLayers
        val mutable public srcOffset : VkOffset3D
        val mutable public dstSubresource : VkImageSubresourceLayers
        val mutable public dstOffset : VkOffset3D
        val mutable public extent : VkExtent3D

        new(srcSubresource : VkImageSubresourceLayers, srcOffset : VkOffset3D, dstSubresource : VkImageSubresourceLayers, dstOffset : VkOffset3D, extent : VkExtent3D) = { srcSubresource = srcSubresource; srcOffset = srcOffset; dstSubresource = dstSubresource; dstOffset = dstOffset; extent = extent }
        override x.ToString() =
            sprintf "VkImageCopy { srcSubresource = %A; srcOffset = %A; dstSubresource = %A; dstOffset = %A; extent = %A }" x.srcSubresource x.srcOffset x.dstSubresource x.dstOffset x.extent
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkImageCreateFlags, imageType : VkImageType, format : VkFormat, extent : VkExtent3D, mipLevels : uint32, arrayLayers : uint32, samples : VkSampleCountFlags, tiling : VkImageTiling, usage : VkImageUsageFlags, sharingMode : VkSharingMode, queueFamilyIndexCount : uint32, pQueueFamilyIndices : nativeptr<uint32>, initialLayout : VkImageLayout) = { sType = sType; pNext = pNext; flags = flags; imageType = imageType; format = format; extent = extent; mipLevels = mipLevels; arrayLayers = arrayLayers; samples = samples; tiling = tiling; usage = usage; sharingMode = sharingMode; queueFamilyIndexCount = queueFamilyIndexCount; pQueueFamilyIndices = pQueueFamilyIndices; initialLayout = initialLayout }
        override x.ToString() =
            sprintf "VkImageCreateInfo { sType = %A; pNext = %A; flags = %A; imageType = %A; format = %A; extent = %A; mipLevels = %A; arrayLayers = %A; samples = %A; tiling = %A; usage = %A; sharingMode = %A; queueFamilyIndexCount = %A; pQueueFamilyIndices = %A; initialLayout = %A }" x.sType x.pNext x.flags x.imageType x.format x.extent x.mipLevels x.arrayLayers x.samples x.tiling x.usage x.sharingMode x.queueFamilyIndexCount x.pQueueFamilyIndices x.initialLayout
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageFormatProperties = 
    struct
        val mutable public maxExtent : VkExtent3D
        val mutable public maxMipLevels : uint32
        val mutable public maxArrayLayers : uint32
        val mutable public sampleCounts : VkSampleCountFlags
        val mutable public maxResourceSize : VkDeviceSize

        new(maxExtent : VkExtent3D, maxMipLevels : uint32, maxArrayLayers : uint32, sampleCounts : VkSampleCountFlags, maxResourceSize : VkDeviceSize) = { maxExtent = maxExtent; maxMipLevels = maxMipLevels; maxArrayLayers = maxArrayLayers; sampleCounts = sampleCounts; maxResourceSize = maxResourceSize }
        override x.ToString() =
            sprintf "VkImageFormatProperties { maxExtent = %A; maxMipLevels = %A; maxArrayLayers = %A; sampleCounts = %A; maxResourceSize = %A }" x.maxExtent x.maxMipLevels x.maxArrayLayers x.sampleCounts x.maxResourceSize
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageSubresourceRange = 
    struct
        val mutable public aspectMask : VkImageAspectFlags
        val mutable public baseMipLevel : uint32
        val mutable public levelCount : uint32
        val mutable public baseArrayLayer : uint32
        val mutable public layerCount : uint32

        new(aspectMask : VkImageAspectFlags, baseMipLevel : uint32, levelCount : uint32, baseArrayLayer : uint32, layerCount : uint32) = { aspectMask = aspectMask; baseMipLevel = baseMipLevel; levelCount = levelCount; baseArrayLayer = baseArrayLayer; layerCount = layerCount }
        override x.ToString() =
            sprintf "VkImageSubresourceRange { aspectMask = %A; baseMipLevel = %A; levelCount = %A; baseArrayLayer = %A; layerCount = %A }" x.aspectMask x.baseMipLevel x.levelCount x.baseArrayLayer x.layerCount
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

        new(sType : VkStructureType, pNext : nativeint, srcAccessMask : VkAccessFlags, dstAccessMask : VkAccessFlags, oldLayout : VkImageLayout, newLayout : VkImageLayout, srcQueueFamilyIndex : uint32, dstQueueFamilyIndex : uint32, image : VkImage, subresourceRange : VkImageSubresourceRange) = { sType = sType; pNext = pNext; srcAccessMask = srcAccessMask; dstAccessMask = dstAccessMask; oldLayout = oldLayout; newLayout = newLayout; srcQueueFamilyIndex = srcQueueFamilyIndex; dstQueueFamilyIndex = dstQueueFamilyIndex; image = image; subresourceRange = subresourceRange }
        override x.ToString() =
            sprintf "VkImageMemoryBarrier { sType = %A; pNext = %A; srcAccessMask = %A; dstAccessMask = %A; oldLayout = %A; newLayout = %A; srcQueueFamilyIndex = %A; dstQueueFamilyIndex = %A; image = %A; subresourceRange = %A }" x.sType x.pNext x.srcAccessMask x.dstAccessMask x.oldLayout x.newLayout x.srcQueueFamilyIndex x.dstQueueFamilyIndex x.image x.subresourceRange
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageResolve = 
    struct
        val mutable public srcSubresource : VkImageSubresourceLayers
        val mutable public srcOffset : VkOffset3D
        val mutable public dstSubresource : VkImageSubresourceLayers
        val mutable public dstOffset : VkOffset3D
        val mutable public extent : VkExtent3D

        new(srcSubresource : VkImageSubresourceLayers, srcOffset : VkOffset3D, dstSubresource : VkImageSubresourceLayers, dstOffset : VkOffset3D, extent : VkExtent3D) = { srcSubresource = srcSubresource; srcOffset = srcOffset; dstSubresource = dstSubresource; dstOffset = dstOffset; extent = extent }
        override x.ToString() =
            sprintf "VkImageResolve { srcSubresource = %A; srcOffset = %A; dstSubresource = %A; dstOffset = %A; extent = %A }" x.srcSubresource x.srcOffset x.dstSubresource x.dstOffset x.extent
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkImageViewCreateFlags, image : VkImage, viewType : VkImageViewType, format : VkFormat, components : VkComponentMapping, subresourceRange : VkImageSubresourceRange) = { sType = sType; pNext = pNext; flags = flags; image = image; viewType = viewType; format = format; components = components; subresourceRange = subresourceRange }
        override x.ToString() =
            sprintf "VkImageViewCreateInfo { sType = %A; pNext = %A; flags = %A; image = %A; viewType = %A; format = %A; components = %A; subresourceRange = %A }" x.sType x.pNext x.flags x.image x.viewType x.format x.components x.subresourceRange
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkInstanceCreateFlags, pApplicationInfo : nativeptr<VkApplicationInfo>, enabledLayerCount : uint32, ppEnabledLayerNames : nativeptr<cstr>, enabledExtensionCount : uint32, ppEnabledExtensionNames : nativeptr<cstr>) = { sType = sType; pNext = pNext; flags = flags; pApplicationInfo = pApplicationInfo; enabledLayerCount = enabledLayerCount; ppEnabledLayerNames = ppEnabledLayerNames; enabledExtensionCount = enabledExtensionCount; ppEnabledExtensionNames = ppEnabledExtensionNames }
        override x.ToString() =
            sprintf "VkInstanceCreateInfo { sType = %A; pNext = %A; flags = %A; pApplicationInfo = %A; enabledLayerCount = %A; ppEnabledLayerNames = %A; enabledExtensionCount = %A; ppEnabledExtensionNames = %A }" x.sType x.pNext x.flags x.pApplicationInfo x.enabledLayerCount x.ppEnabledLayerNames x.enabledExtensionCount x.ppEnabledExtensionNames
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkLayerProperties = 
    struct
        val mutable public layerName : String256
        val mutable public specVersion : uint32
        val mutable public implementationVersion : uint32
        val mutable public description : String256

        new(layerName : String256, specVersion : uint32, implementationVersion : uint32, description : String256) = { layerName = layerName; specVersion = specVersion; implementationVersion = implementationVersion; description = description }
        override x.ToString() =
            sprintf "VkLayerProperties { layerName = %A; specVersion = %A; implementationVersion = %A; description = %A }" x.layerName x.specVersion x.implementationVersion x.description
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkMappedMemoryRange = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public memory : VkDeviceMemory
        val mutable public offset : VkDeviceSize
        val mutable public size : VkDeviceSize

        new(sType : VkStructureType, pNext : nativeint, memory : VkDeviceMemory, offset : VkDeviceSize, size : VkDeviceSize) = { sType = sType; pNext = pNext; memory = memory; offset = offset; size = size }
        override x.ToString() =
            sprintf "VkMappedMemoryRange { sType = %A; pNext = %A; memory = %A; offset = %A; size = %A }" x.sType x.pNext x.memory x.offset x.size
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkMemoryAllocateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public allocationSize : VkDeviceSize
        val mutable public memoryTypeIndex : uint32

        new(sType : VkStructureType, pNext : nativeint, allocationSize : VkDeviceSize, memoryTypeIndex : uint32) = { sType = sType; pNext = pNext; allocationSize = allocationSize; memoryTypeIndex = memoryTypeIndex }
        override x.ToString() =
            sprintf "VkMemoryAllocateInfo { sType = %A; pNext = %A; allocationSize = %A; memoryTypeIndex = %A }" x.sType x.pNext x.allocationSize x.memoryTypeIndex
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkMemoryBarrier = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public srcAccessMask : VkAccessFlags
        val mutable public dstAccessMask : VkAccessFlags

        new(sType : VkStructureType, pNext : nativeint, srcAccessMask : VkAccessFlags, dstAccessMask : VkAccessFlags) = { sType = sType; pNext = pNext; srcAccessMask = srcAccessMask; dstAccessMask = dstAccessMask }
        override x.ToString() =
            sprintf "VkMemoryBarrier { sType = %A; pNext = %A; srcAccessMask = %A; dstAccessMask = %A }" x.sType x.pNext x.srcAccessMask x.dstAccessMask
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkMemoryHeap = 
    struct
        val mutable public size : VkDeviceSize
        val mutable public flags : VkMemoryHeapFlags

        new(size : VkDeviceSize, flags : VkMemoryHeapFlags) = { size = size; flags = flags }
        override x.ToString() =
            sprintf "VkMemoryHeap { size = %A; flags = %A }" x.size x.flags
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

        new(size : VkDeviceSize, alignment : VkDeviceSize, memoryTypeBits : uint32) = { size = size; alignment = alignment; memoryTypeBits = memoryTypeBits }
        override x.ToString() =
            sprintf "VkMemoryRequirements { size = %A; alignment = %A; memoryTypeBits = %A }" x.size x.alignment x.memoryTypeBits
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkMemoryType = 
    struct
        val mutable public propertyFlags : VkMemoryPropertyFlags
        val mutable public heapIndex : uint32

        new(propertyFlags : VkMemoryPropertyFlags, heapIndex : uint32) = { propertyFlags = propertyFlags; heapIndex = heapIndex }
        override x.ToString() =
            sprintf "VkMemoryType { propertyFlags = %A; heapIndex = %A }" x.propertyFlags x.heapIndex
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

        new(maxImageDimension1D : uint32, maxImageDimension2D : uint32, maxImageDimension3D : uint32, maxImageDimensionCube : uint32, maxImageArrayLayers : uint32, maxTexelBufferElements : uint32, maxUniformBufferRange : uint32, maxStorageBufferRange : uint32, maxPushConstantsSize : uint32, maxMemoryAllocationCount : uint32, maxSamplerAllocationCount : uint32, bufferImageGranularity : VkDeviceSize, sparseAddressSpaceSize : VkDeviceSize, maxBoundDescriptorSets : uint32, maxPerStageDescriptorSamplers : uint32, maxPerStageDescriptorUniformBuffers : uint32, maxPerStageDescriptorStorageBuffers : uint32, maxPerStageDescriptorSampledImages : uint32, maxPerStageDescriptorStorageImages : uint32, maxPerStageDescriptorInputAttachments : uint32, maxPerStageResources : uint32, maxDescriptorSetSamplers : uint32, maxDescriptorSetUniformBuffers : uint32, maxDescriptorSetUniformBuffersDynamic : uint32, maxDescriptorSetStorageBuffers : uint32, maxDescriptorSetStorageBuffersDynamic : uint32, maxDescriptorSetSampledImages : uint32, maxDescriptorSetStorageImages : uint32, maxDescriptorSetInputAttachments : uint32, maxVertexInputAttributes : uint32, maxVertexInputBindings : uint32, maxVertexInputAttributeOffset : uint32, maxVertexInputBindingStride : uint32, maxVertexOutputComponents : uint32, maxTessellationGenerationLevel : uint32, maxTessellationPatchSize : uint32, maxTessellationControlPerVertexInputComponents : uint32, maxTessellationControlPerVertexOutputComponents : uint32, maxTessellationControlPerPatchOutputComponents : uint32, maxTessellationControlTotalOutputComponents : uint32, maxTessellationEvaluationInputComponents : uint32, maxTessellationEvaluationOutputComponents : uint32, maxGeometryShaderInvocations : uint32, maxGeometryInputComponents : uint32, maxGeometryOutputComponents : uint32, maxGeometryOutputVertices : uint32, maxGeometryTotalOutputComponents : uint32, maxFragmentInputComponents : uint32, maxFragmentOutputAttachments : uint32, maxFragmentDualSrcAttachments : uint32, maxFragmentCombinedOutputResources : uint32, maxComputeSharedMemorySize : uint32, maxComputeWorkGroupCount : V3ui, maxComputeWorkGroupInvocations : uint32, maxComputeWorkGroupSize : V3ui, subPixelPrecisionBits : uint32, subTexelPrecisionBits : uint32, mipmapPrecisionBits : uint32, maxDrawIndexedIndexValue : uint32, maxDrawIndirectCount : uint32, maxSamplerLodBias : float32, maxSamplerAnisotropy : float32, maxViewports : uint32, maxViewportDimensions : V2ui, viewportBoundsRange : V2f, viewportSubPixelBits : uint32, minMemoryMapAlignment : uint64, minTexelBufferOffsetAlignment : VkDeviceSize, minUniformBufferOffsetAlignment : VkDeviceSize, minStorageBufferOffsetAlignment : VkDeviceSize, minTexelOffset : int, maxTexelOffset : uint32, minTexelGatherOffset : int, maxTexelGatherOffset : uint32, minInterpolationOffset : float32, maxInterpolationOffset : float32, subPixelInterpolationOffsetBits : uint32, maxFramebufferWidth : uint32, maxFramebufferHeight : uint32, maxFramebufferLayers : uint32, framebufferColorSampleCounts : VkSampleCountFlags, framebufferDepthSampleCounts : VkSampleCountFlags, framebufferStencilSampleCounts : VkSampleCountFlags, framebufferNoAttachmentsSampleCounts : VkSampleCountFlags, maxColorAttachments : uint32, sampledImageColorSampleCounts : VkSampleCountFlags, sampledImageIntegerSampleCounts : VkSampleCountFlags, sampledImageDepthSampleCounts : VkSampleCountFlags, sampledImageStencilSampleCounts : VkSampleCountFlags, storageImageSampleCounts : VkSampleCountFlags, maxSampleMaskWords : uint32, timestampComputeAndGraphics : VkBool32, timestampPeriod : float32, maxClipDistances : uint32, maxCullDistances : uint32, maxCombinedClipAndCullDistances : uint32, discreteQueuePriorities : uint32, pointSizeRange : V2f, lineWidthRange : V2f, pointSizeGranularity : float32, lineWidthGranularity : float32, strictLines : VkBool32, standardSampleLocations : VkBool32, optimalBufferCopyOffsetAlignment : VkDeviceSize, optimalBufferCopyRowPitchAlignment : VkDeviceSize, nonCoherentAtomSize : VkDeviceSize) = { maxImageDimension1D = maxImageDimension1D; maxImageDimension2D = maxImageDimension2D; maxImageDimension3D = maxImageDimension3D; maxImageDimensionCube = maxImageDimensionCube; maxImageArrayLayers = maxImageArrayLayers; maxTexelBufferElements = maxTexelBufferElements; maxUniformBufferRange = maxUniformBufferRange; maxStorageBufferRange = maxStorageBufferRange; maxPushConstantsSize = maxPushConstantsSize; maxMemoryAllocationCount = maxMemoryAllocationCount; maxSamplerAllocationCount = maxSamplerAllocationCount; bufferImageGranularity = bufferImageGranularity; sparseAddressSpaceSize = sparseAddressSpaceSize; maxBoundDescriptorSets = maxBoundDescriptorSets; maxPerStageDescriptorSamplers = maxPerStageDescriptorSamplers; maxPerStageDescriptorUniformBuffers = maxPerStageDescriptorUniformBuffers; maxPerStageDescriptorStorageBuffers = maxPerStageDescriptorStorageBuffers; maxPerStageDescriptorSampledImages = maxPerStageDescriptorSampledImages; maxPerStageDescriptorStorageImages = maxPerStageDescriptorStorageImages; maxPerStageDescriptorInputAttachments = maxPerStageDescriptorInputAttachments; maxPerStageResources = maxPerStageResources; maxDescriptorSetSamplers = maxDescriptorSetSamplers; maxDescriptorSetUniformBuffers = maxDescriptorSetUniformBuffers; maxDescriptorSetUniformBuffersDynamic = maxDescriptorSetUniformBuffersDynamic; maxDescriptorSetStorageBuffers = maxDescriptorSetStorageBuffers; maxDescriptorSetStorageBuffersDynamic = maxDescriptorSetStorageBuffersDynamic; maxDescriptorSetSampledImages = maxDescriptorSetSampledImages; maxDescriptorSetStorageImages = maxDescriptorSetStorageImages; maxDescriptorSetInputAttachments = maxDescriptorSetInputAttachments; maxVertexInputAttributes = maxVertexInputAttributes; maxVertexInputBindings = maxVertexInputBindings; maxVertexInputAttributeOffset = maxVertexInputAttributeOffset; maxVertexInputBindingStride = maxVertexInputBindingStride; maxVertexOutputComponents = maxVertexOutputComponents; maxTessellationGenerationLevel = maxTessellationGenerationLevel; maxTessellationPatchSize = maxTessellationPatchSize; maxTessellationControlPerVertexInputComponents = maxTessellationControlPerVertexInputComponents; maxTessellationControlPerVertexOutputComponents = maxTessellationControlPerVertexOutputComponents; maxTessellationControlPerPatchOutputComponents = maxTessellationControlPerPatchOutputComponents; maxTessellationControlTotalOutputComponents = maxTessellationControlTotalOutputComponents; maxTessellationEvaluationInputComponents = maxTessellationEvaluationInputComponents; maxTessellationEvaluationOutputComponents = maxTessellationEvaluationOutputComponents; maxGeometryShaderInvocations = maxGeometryShaderInvocations; maxGeometryInputComponents = maxGeometryInputComponents; maxGeometryOutputComponents = maxGeometryOutputComponents; maxGeometryOutputVertices = maxGeometryOutputVertices; maxGeometryTotalOutputComponents = maxGeometryTotalOutputComponents; maxFragmentInputComponents = maxFragmentInputComponents; maxFragmentOutputAttachments = maxFragmentOutputAttachments; maxFragmentDualSrcAttachments = maxFragmentDualSrcAttachments; maxFragmentCombinedOutputResources = maxFragmentCombinedOutputResources; maxComputeSharedMemorySize = maxComputeSharedMemorySize; maxComputeWorkGroupCount = maxComputeWorkGroupCount; maxComputeWorkGroupInvocations = maxComputeWorkGroupInvocations; maxComputeWorkGroupSize = maxComputeWorkGroupSize; subPixelPrecisionBits = subPixelPrecisionBits; subTexelPrecisionBits = subTexelPrecisionBits; mipmapPrecisionBits = mipmapPrecisionBits; maxDrawIndexedIndexValue = maxDrawIndexedIndexValue; maxDrawIndirectCount = maxDrawIndirectCount; maxSamplerLodBias = maxSamplerLodBias; maxSamplerAnisotropy = maxSamplerAnisotropy; maxViewports = maxViewports; maxViewportDimensions = maxViewportDimensions; viewportBoundsRange = viewportBoundsRange; viewportSubPixelBits = viewportSubPixelBits; minMemoryMapAlignment = minMemoryMapAlignment; minTexelBufferOffsetAlignment = minTexelBufferOffsetAlignment; minUniformBufferOffsetAlignment = minUniformBufferOffsetAlignment; minStorageBufferOffsetAlignment = minStorageBufferOffsetAlignment; minTexelOffset = minTexelOffset; maxTexelOffset = maxTexelOffset; minTexelGatherOffset = minTexelGatherOffset; maxTexelGatherOffset = maxTexelGatherOffset; minInterpolationOffset = minInterpolationOffset; maxInterpolationOffset = maxInterpolationOffset; subPixelInterpolationOffsetBits = subPixelInterpolationOffsetBits; maxFramebufferWidth = maxFramebufferWidth; maxFramebufferHeight = maxFramebufferHeight; maxFramebufferLayers = maxFramebufferLayers; framebufferColorSampleCounts = framebufferColorSampleCounts; framebufferDepthSampleCounts = framebufferDepthSampleCounts; framebufferStencilSampleCounts = framebufferStencilSampleCounts; framebufferNoAttachmentsSampleCounts = framebufferNoAttachmentsSampleCounts; maxColorAttachments = maxColorAttachments; sampledImageColorSampleCounts = sampledImageColorSampleCounts; sampledImageIntegerSampleCounts = sampledImageIntegerSampleCounts; sampledImageDepthSampleCounts = sampledImageDepthSampleCounts; sampledImageStencilSampleCounts = sampledImageStencilSampleCounts; storageImageSampleCounts = storageImageSampleCounts; maxSampleMaskWords = maxSampleMaskWords; timestampComputeAndGraphics = timestampComputeAndGraphics; timestampPeriod = timestampPeriod; maxClipDistances = maxClipDistances; maxCullDistances = maxCullDistances; maxCombinedClipAndCullDistances = maxCombinedClipAndCullDistances; discreteQueuePriorities = discreteQueuePriorities; pointSizeRange = pointSizeRange; lineWidthRange = lineWidthRange; pointSizeGranularity = pointSizeGranularity; lineWidthGranularity = lineWidthGranularity; strictLines = strictLines; standardSampleLocations = standardSampleLocations; optimalBufferCopyOffsetAlignment = optimalBufferCopyOffsetAlignment; optimalBufferCopyRowPitchAlignment = optimalBufferCopyRowPitchAlignment; nonCoherentAtomSize = nonCoherentAtomSize }
        override x.ToString() =
            sprintf "VkPhysicalDeviceLimits { maxImageDimension1D = %A; maxImageDimension2D = %A; maxImageDimension3D = %A; maxImageDimensionCube = %A; maxImageArrayLayers = %A; maxTexelBufferElements = %A; maxUniformBufferRange = %A; maxStorageBufferRange = %A; maxPushConstantsSize = %A; maxMemoryAllocationCount = %A; maxSamplerAllocationCount = %A; bufferImageGranularity = %A; sparseAddressSpaceSize = %A; maxBoundDescriptorSets = %A; maxPerStageDescriptorSamplers = %A; maxPerStageDescriptorUniformBuffers = %A; maxPerStageDescriptorStorageBuffers = %A; maxPerStageDescriptorSampledImages = %A; maxPerStageDescriptorStorageImages = %A; maxPerStageDescriptorInputAttachments = %A; maxPerStageResources = %A; maxDescriptorSetSamplers = %A; maxDescriptorSetUniformBuffers = %A; maxDescriptorSetUniformBuffersDynamic = %A; maxDescriptorSetStorageBuffers = %A; maxDescriptorSetStorageBuffersDynamic = %A; maxDescriptorSetSampledImages = %A; maxDescriptorSetStorageImages = %A; maxDescriptorSetInputAttachments = %A; maxVertexInputAttributes = %A; maxVertexInputBindings = %A; maxVertexInputAttributeOffset = %A; maxVertexInputBindingStride = %A; maxVertexOutputComponents = %A; maxTessellationGenerationLevel = %A; maxTessellationPatchSize = %A; maxTessellationControlPerVertexInputComponents = %A; maxTessellationControlPerVertexOutputComponents = %A; maxTessellationControlPerPatchOutputComponents = %A; maxTessellationControlTotalOutputComponents = %A; maxTessellationEvaluationInputComponents = %A; maxTessellationEvaluationOutputComponents = %A; maxGeometryShaderInvocations = %A; maxGeometryInputComponents = %A; maxGeometryOutputComponents = %A; maxGeometryOutputVertices = %A; maxGeometryTotalOutputComponents = %A; maxFragmentInputComponents = %A; maxFragmentOutputAttachments = %A; maxFragmentDualSrcAttachments = %A; maxFragmentCombinedOutputResources = %A; maxComputeSharedMemorySize = %A; maxComputeWorkGroupCount = %A; maxComputeWorkGroupInvocations = %A; maxComputeWorkGroupSize = %A; subPixelPrecisionBits = %A; subTexelPrecisionBits = %A; mipmapPrecisionBits = %A; maxDrawIndexedIndexValue = %A; maxDrawIndirectCount = %A; maxSamplerLodBias = %A; maxSamplerAnisotropy = %A; maxViewports = %A; maxViewportDimensions = %A; viewportBoundsRange = %A; viewportSubPixelBits = %A; minMemoryMapAlignment = %A; minTexelBufferOffsetAlignment = %A; minUniformBufferOffsetAlignment = %A; minStorageBufferOffsetAlignment = %A; minTexelOffset = %A; maxTexelOffset = %A; minTexelGatherOffset = %A; maxTexelGatherOffset = %A; minInterpolationOffset = %A; maxInterpolationOffset = %A; subPixelInterpolationOffsetBits = %A; maxFramebufferWidth = %A; maxFramebufferHeight = %A; maxFramebufferLayers = %A; framebufferColorSampleCounts = %A; framebufferDepthSampleCounts = %A; framebufferStencilSampleCounts = %A; framebufferNoAttachmentsSampleCounts = %A; maxColorAttachments = %A; sampledImageColorSampleCounts = %A; sampledImageIntegerSampleCounts = %A; sampledImageDepthSampleCounts = %A; sampledImageStencilSampleCounts = %A; storageImageSampleCounts = %A; maxSampleMaskWords = %A; timestampComputeAndGraphics = %A; timestampPeriod = %A; maxClipDistances = %A; maxCullDistances = %A; maxCombinedClipAndCullDistances = %A; discreteQueuePriorities = %A; pointSizeRange = %A; lineWidthRange = %A; pointSizeGranularity = %A; lineWidthGranularity = %A; strictLines = %A; standardSampleLocations = %A; optimalBufferCopyOffsetAlignment = %A; optimalBufferCopyRowPitchAlignment = %A; nonCoherentAtomSize = %A }" x.maxImageDimension1D x.maxImageDimension2D x.maxImageDimension3D x.maxImageDimensionCube x.maxImageArrayLayers x.maxTexelBufferElements x.maxUniformBufferRange x.maxStorageBufferRange x.maxPushConstantsSize x.maxMemoryAllocationCount x.maxSamplerAllocationCount x.bufferImageGranularity x.sparseAddressSpaceSize x.maxBoundDescriptorSets x.maxPerStageDescriptorSamplers x.maxPerStageDescriptorUniformBuffers x.maxPerStageDescriptorStorageBuffers x.maxPerStageDescriptorSampledImages x.maxPerStageDescriptorStorageImages x.maxPerStageDescriptorInputAttachments x.maxPerStageResources x.maxDescriptorSetSamplers x.maxDescriptorSetUniformBuffers x.maxDescriptorSetUniformBuffersDynamic x.maxDescriptorSetStorageBuffers x.maxDescriptorSetStorageBuffersDynamic x.maxDescriptorSetSampledImages x.maxDescriptorSetStorageImages x.maxDescriptorSetInputAttachments x.maxVertexInputAttributes x.maxVertexInputBindings x.maxVertexInputAttributeOffset x.maxVertexInputBindingStride x.maxVertexOutputComponents x.maxTessellationGenerationLevel x.maxTessellationPatchSize x.maxTessellationControlPerVertexInputComponents x.maxTessellationControlPerVertexOutputComponents x.maxTessellationControlPerPatchOutputComponents x.maxTessellationControlTotalOutputComponents x.maxTessellationEvaluationInputComponents x.maxTessellationEvaluationOutputComponents x.maxGeometryShaderInvocations x.maxGeometryInputComponents x.maxGeometryOutputComponents x.maxGeometryOutputVertices x.maxGeometryTotalOutputComponents x.maxFragmentInputComponents x.maxFragmentOutputAttachments x.maxFragmentDualSrcAttachments x.maxFragmentCombinedOutputResources x.maxComputeSharedMemorySize x.maxComputeWorkGroupCount x.maxComputeWorkGroupInvocations x.maxComputeWorkGroupSize x.subPixelPrecisionBits x.subTexelPrecisionBits x.mipmapPrecisionBits x.maxDrawIndexedIndexValue x.maxDrawIndirectCount x.maxSamplerLodBias x.maxSamplerAnisotropy x.maxViewports x.maxViewportDimensions x.viewportBoundsRange x.viewportSubPixelBits x.minMemoryMapAlignment x.minTexelBufferOffsetAlignment x.minUniformBufferOffsetAlignment x.minStorageBufferOffsetAlignment x.minTexelOffset x.maxTexelOffset x.minTexelGatherOffset x.maxTexelGatherOffset x.minInterpolationOffset x.maxInterpolationOffset x.subPixelInterpolationOffsetBits x.maxFramebufferWidth x.maxFramebufferHeight x.maxFramebufferLayers x.framebufferColorSampleCounts x.framebufferDepthSampleCounts x.framebufferStencilSampleCounts x.framebufferNoAttachmentsSampleCounts x.maxColorAttachments x.sampledImageColorSampleCounts x.sampledImageIntegerSampleCounts x.sampledImageDepthSampleCounts x.sampledImageStencilSampleCounts x.storageImageSampleCounts x.maxSampleMaskWords x.timestampComputeAndGraphics x.timestampPeriod x.maxClipDistances x.maxCullDistances x.maxCombinedClipAndCullDistances x.discreteQueuePriorities x.pointSizeRange x.lineWidthRange x.pointSizeGranularity x.lineWidthGranularity x.strictLines x.standardSampleLocations x.optimalBufferCopyOffsetAlignment x.optimalBufferCopyRowPitchAlignment x.nonCoherentAtomSize
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceMemoryProperties = 
    struct
        val mutable public memoryTypeCount : uint32
        val mutable public memoryTypes : VkMemoryType_32
        val mutable public memoryHeapCount : uint32
        val mutable public memoryHeaps : VkMemoryHeap_16

        new(memoryTypeCount : uint32, memoryTypes : VkMemoryType_32, memoryHeapCount : uint32, memoryHeaps : VkMemoryHeap_16) = { memoryTypeCount = memoryTypeCount; memoryTypes = memoryTypes; memoryHeapCount = memoryHeapCount; memoryHeaps = memoryHeaps }
        override x.ToString() =
            sprintf "VkPhysicalDeviceMemoryProperties { memoryTypeCount = %A; memoryTypes = %A; memoryHeapCount = %A; memoryHeaps = %A }" x.memoryTypeCount x.memoryTypes x.memoryHeapCount x.memoryHeaps
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPhysicalDeviceSparseProperties = 
    struct
        val mutable public residencyStandard2DBlockShape : VkBool32
        val mutable public residencyStandard2DMultisampleBlockShape : VkBool32
        val mutable public residencyStandard3DBlockShape : VkBool32
        val mutable public residencyAlignedMipSize : VkBool32
        val mutable public residencyNonResidentStrict : VkBool32

        new(residencyStandard2DBlockShape : VkBool32, residencyStandard2DMultisampleBlockShape : VkBool32, residencyStandard3DBlockShape : VkBool32, residencyAlignedMipSize : VkBool32, residencyNonResidentStrict : VkBool32) = { residencyStandard2DBlockShape = residencyStandard2DBlockShape; residencyStandard2DMultisampleBlockShape = residencyStandard2DMultisampleBlockShape; residencyStandard3DBlockShape = residencyStandard3DBlockShape; residencyAlignedMipSize = residencyAlignedMipSize; residencyNonResidentStrict = residencyNonResidentStrict }
        override x.ToString() =
            sprintf "VkPhysicalDeviceSparseProperties { residencyStandard2DBlockShape = %A; residencyStandard2DMultisampleBlockShape = %A; residencyStandard3DBlockShape = %A; residencyAlignedMipSize = %A; residencyNonResidentStrict = %A }" x.residencyStandard2DBlockShape x.residencyStandard2DMultisampleBlockShape x.residencyStandard3DBlockShape x.residencyAlignedMipSize x.residencyNonResidentStrict
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

        new(apiVersion : uint32, driverVersion : uint32, vendorID : uint32, deviceID : uint32, deviceType : VkPhysicalDeviceType, deviceName : String256, pipelineCacheUUID : Guid, limits : VkPhysicalDeviceLimits, sparseProperties : VkPhysicalDeviceSparseProperties) = { apiVersion = apiVersion; driverVersion = driverVersion; vendorID = vendorID; deviceID = deviceID; deviceType = deviceType; deviceName = deviceName; pipelineCacheUUID = pipelineCacheUUID; limits = limits; sparseProperties = sparseProperties }
        override x.ToString() =
            sprintf "VkPhysicalDeviceProperties { apiVersion = %A; driverVersion = %A; vendorID = %A; deviceID = %A; deviceType = %A; deviceName = %A; pipelineCacheUUID = %A; limits = %A; sparseProperties = %A }" x.apiVersion x.driverVersion x.vendorID x.deviceID x.deviceType x.deviceName x.pipelineCacheUUID x.limits x.sparseProperties
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineCacheCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkPipelineCacheCreateFlags
        val mutable public initialDataSize : uint64
        val mutable public pInitialData : nativeint

        new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineCacheCreateFlags, initialDataSize : uint64, pInitialData : nativeint) = { sType = sType; pNext = pNext; flags = flags; initialDataSize = initialDataSize; pInitialData = pInitialData }
        override x.ToString() =
            sprintf "VkPipelineCacheCreateInfo { sType = %A; pNext = %A; flags = %A; initialDataSize = %A; pInitialData = %A }" x.sType x.pNext x.flags x.initialDataSize x.pInitialData
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPushConstantRange = 
    struct
        val mutable public stageFlags : VkShaderStageFlags
        val mutable public offset : uint32
        val mutable public size : uint32

        new(stageFlags : VkShaderStageFlags, offset : uint32, size : uint32) = { stageFlags = stageFlags; offset = offset; size = size }
        override x.ToString() =
            sprintf "VkPushConstantRange { stageFlags = %A; offset = %A; size = %A }" x.stageFlags x.offset x.size
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineLayoutCreateFlags, setLayoutCount : uint32, pSetLayouts : nativeptr<VkDescriptorSetLayout>, pushConstantRangeCount : uint32, pPushConstantRanges : nativeptr<VkPushConstantRange>) = { sType = sType; pNext = pNext; flags = flags; setLayoutCount = setLayoutCount; pSetLayouts = pSetLayouts; pushConstantRangeCount = pushConstantRangeCount; pPushConstantRanges = pPushConstantRanges }
        override x.ToString() =
            sprintf "VkPipelineLayoutCreateInfo { sType = %A; pNext = %A; flags = %A; setLayoutCount = %A; pSetLayouts = %A; pushConstantRangeCount = %A; pPushConstantRanges = %A }" x.sType x.pNext x.flags x.setLayoutCount x.pSetLayouts x.pushConstantRangeCount x.pPushConstantRanges
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

        new(sType : VkStructureType, pNext : nativeint, waitSemaphoreCount : uint32, pWaitSemaphores : nativeptr<VkSemaphore>, swapchainCount : uint32, pSwapchains : nativeptr<VkSwapchainKHR>, pImageIndices : nativeptr<uint32>, pResults : nativeptr<VkResult>) = { sType = sType; pNext = pNext; waitSemaphoreCount = waitSemaphoreCount; pWaitSemaphores = pWaitSemaphores; swapchainCount = swapchainCount; pSwapchains = pSwapchains; pImageIndices = pImageIndices; pResults = pResults }
        override x.ToString() =
            sprintf "VkPresentInfoKHR { sType = %A; pNext = %A; waitSemaphoreCount = %A; pWaitSemaphores = %A; swapchainCount = %A; pSwapchains = %A; pImageIndices = %A; pResults = %A }" x.sType x.pNext x.waitSemaphoreCount x.pWaitSemaphores x.swapchainCount x.pSwapchains x.pImageIndices x.pResults
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkQueryPoolCreateFlags, queryType : VkQueryType, queryCount : uint32, pipelineStatistics : VkQueryPipelineStatisticFlags) = { sType = sType; pNext = pNext; flags = flags; queryType = queryType; queryCount = queryCount; pipelineStatistics = pipelineStatistics }
        override x.ToString() =
            sprintf "VkQueryPoolCreateInfo { sType = %A; pNext = %A; flags = %A; queryType = %A; queryCount = %A; pipelineStatistics = %A }" x.sType x.pNext x.flags x.queryType x.queryCount x.pipelineStatistics
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkQueueFamilyProperties = 
    struct
        val mutable public queueFlags : VkQueueFlags
        val mutable public queueCount : uint32
        val mutable public timestampValidBits : uint32
        val mutable public minImageTransferGranularity : VkExtent3D

        new(queueFlags : VkQueueFlags, queueCount : uint32, timestampValidBits : uint32, minImageTransferGranularity : VkExtent3D) = { queueFlags = queueFlags; queueCount = queueCount; timestampValidBits = timestampValidBits; minImageTransferGranularity = minImageTransferGranularity }
        override x.ToString() =
            sprintf "VkQueueFamilyProperties { queueFlags = %A; queueCount = %A; timestampValidBits = %A; minImageTransferGranularity = %A }" x.queueFlags x.queueCount x.timestampValidBits x.minImageTransferGranularity
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

        new(sType : VkStructureType, pNext : nativeint, renderPass : VkRenderPass, framebuffer : VkFramebuffer, renderArea : VkRect2D, clearValueCount : uint32, pClearValues : nativeptr<VkClearValue>) = { sType = sType; pNext = pNext; renderPass = renderPass; framebuffer = framebuffer; renderArea = renderArea; clearValueCount = clearValueCount; pClearValues = pClearValues }
        override x.ToString() =
            sprintf "VkRenderPassBeginInfo { sType = %A; pNext = %A; renderPass = %A; framebuffer = %A; renderArea = %A; clearValueCount = %A; pClearValues = %A }" x.sType x.pNext x.renderPass x.framebuffer x.renderArea x.clearValueCount x.pClearValues
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

        new(flags : VkSubpassDescriptionFlags, pipelineBindPoint : VkPipelineBindPoint, inputAttachmentCount : uint32, pInputAttachments : nativeptr<VkAttachmentReference>, colorAttachmentCount : uint32, pColorAttachments : nativeptr<VkAttachmentReference>, pResolveAttachments : nativeptr<VkAttachmentReference>, pDepthStencilAttachment : nativeptr<VkAttachmentReference>, preserveAttachmentCount : uint32, pPreserveAttachments : nativeptr<uint32>) = { flags = flags; pipelineBindPoint = pipelineBindPoint; inputAttachmentCount = inputAttachmentCount; pInputAttachments = pInputAttachments; colorAttachmentCount = colorAttachmentCount; pColorAttachments = pColorAttachments; pResolveAttachments = pResolveAttachments; pDepthStencilAttachment = pDepthStencilAttachment; preserveAttachmentCount = preserveAttachmentCount; pPreserveAttachments = pPreserveAttachments }
        override x.ToString() =
            sprintf "VkSubpassDescription { flags = %A; pipelineBindPoint = %A; inputAttachmentCount = %A; pInputAttachments = %A; colorAttachmentCount = %A; pColorAttachments = %A; pResolveAttachments = %A; pDepthStencilAttachment = %A; preserveAttachmentCount = %A; pPreserveAttachments = %A }" x.flags x.pipelineBindPoint x.inputAttachmentCount x.pInputAttachments x.colorAttachmentCount x.pColorAttachments x.pResolveAttachments x.pDepthStencilAttachment x.preserveAttachmentCount x.pPreserveAttachments
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

        new(srcSubpass : uint32, dstSubpass : uint32, srcStageMask : VkPipelineStageFlags, dstStageMask : VkPipelineStageFlags, srcAccessMask : VkAccessFlags, dstAccessMask : VkAccessFlags, dependencyFlags : VkDependencyFlags) = { srcSubpass = srcSubpass; dstSubpass = dstSubpass; srcStageMask = srcStageMask; dstStageMask = dstStageMask; srcAccessMask = srcAccessMask; dstAccessMask = dstAccessMask; dependencyFlags = dependencyFlags }
        override x.ToString() =
            sprintf "VkSubpassDependency { srcSubpass = %A; dstSubpass = %A; srcStageMask = %A; dstStageMask = %A; srcAccessMask = %A; dstAccessMask = %A; dependencyFlags = %A }" x.srcSubpass x.dstSubpass x.srcStageMask x.dstStageMask x.srcAccessMask x.dstAccessMask x.dependencyFlags
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkRenderPassCreateFlags, attachmentCount : uint32, pAttachments : nativeptr<VkAttachmentDescription>, subpassCount : uint32, pSubpasses : nativeptr<VkSubpassDescription>, dependencyCount : uint32, pDependencies : nativeptr<VkSubpassDependency>) = { sType = sType; pNext = pNext; flags = flags; attachmentCount = attachmentCount; pAttachments = pAttachments; subpassCount = subpassCount; pSubpasses = pSubpasses; dependencyCount = dependencyCount; pDependencies = pDependencies }
        override x.ToString() =
            sprintf "VkRenderPassCreateInfo { sType = %A; pNext = %A; flags = %A; attachmentCount = %A; pAttachments = %A; subpassCount = %A; pSubpasses = %A; dependencyCount = %A; pDependencies = %A }" x.sType x.pNext x.flags x.attachmentCount x.pAttachments x.subpassCount x.pSubpasses x.dependencyCount x.pDependencies
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

        new(sType : VkStructureType, pNext : nativeint, flags : VkSamplerCreateFlags, magFilter : VkFilter, minFilter : VkFilter, mipmapMode : VkSamplerMipmapMode, addressModeU : VkSamplerAddressMode, addressModeV : VkSamplerAddressMode, addressModeW : VkSamplerAddressMode, mipLodBias : float32, anisotropyEnable : VkBool32, maxAnisotropy : float32, compareEnable : VkBool32, compareOp : VkCompareOp, minLod : float32, maxLod : float32, borderColor : VkBorderColor, unnormalizedCoordinates : VkBool32) = { sType = sType; pNext = pNext; flags = flags; magFilter = magFilter; minFilter = minFilter; mipmapMode = mipmapMode; addressModeU = addressModeU; addressModeV = addressModeV; addressModeW = addressModeW; mipLodBias = mipLodBias; anisotropyEnable = anisotropyEnable; maxAnisotropy = maxAnisotropy; compareEnable = compareEnable; compareOp = compareOp; minLod = minLod; maxLod = maxLod; borderColor = borderColor; unnormalizedCoordinates = unnormalizedCoordinates }
        override x.ToString() =
            sprintf "VkSamplerCreateInfo { sType = %A; pNext = %A; flags = %A; magFilter = %A; minFilter = %A; mipmapMode = %A; addressModeU = %A; addressModeV = %A; addressModeW = %A; mipLodBias = %A; anisotropyEnable = %A; maxAnisotropy = %A; compareEnable = %A; compareOp = %A; minLod = %A; maxLod = %A; borderColor = %A; unnormalizedCoordinates = %A }" x.sType x.pNext x.flags x.magFilter x.minFilter x.mipmapMode x.addressModeU x.addressModeV x.addressModeW x.mipLodBias x.anisotropyEnable x.maxAnisotropy x.compareEnable x.compareOp x.minLod x.maxLod x.borderColor x.unnormalizedCoordinates
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSemaphoreCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkSemaphoreCreateFlags

        new(sType : VkStructureType, pNext : nativeint, flags : VkSemaphoreCreateFlags) = { sType = sType; pNext = pNext; flags = flags }
        override x.ToString() =
            sprintf "VkSemaphoreCreateInfo { sType = %A; pNext = %A; flags = %A }" x.sType x.pNext x.flags
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkShaderModuleCreateInfo = 
    struct
        val mutable public sType : VkStructureType
        val mutable public pNext : nativeint
        val mutable public flags : VkShaderModuleCreateFlags
        val mutable public codeSize : uint64
        val mutable public pCode : nativeptr<uint32>

        new(sType : VkStructureType, pNext : nativeint, flags : VkShaderModuleCreateFlags, codeSize : uint64, pCode : nativeptr<uint32>) = { sType = sType; pNext = pNext; flags = flags; codeSize = codeSize; pCode = pCode }
        override x.ToString() =
            sprintf "VkShaderModuleCreateInfo { sType = %A; pNext = %A; flags = %A; codeSize = %A; pCode = %A }" x.sType x.pNext x.flags x.codeSize x.pCode
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSparseImageFormatProperties = 
    struct
        val mutable public aspectMask : VkImageAspectFlags
        val mutable public imageGranularity : VkExtent3D
        val mutable public flags : VkSparseImageFormatFlags

        new(aspectMask : VkImageAspectFlags, imageGranularity : VkExtent3D, flags : VkSparseImageFormatFlags) = { aspectMask = aspectMask; imageGranularity = imageGranularity; flags = flags }
        override x.ToString() =
            sprintf "VkSparseImageFormatProperties { aspectMask = %A; imageGranularity = %A; flags = %A }" x.aspectMask x.imageGranularity x.flags
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSparseImageMemoryRequirements = 
    struct
        val mutable public formatProperties : VkSparseImageFormatProperties
        val mutable public imageMipTailFirstLod : uint32
        val mutable public imageMipTailSize : VkDeviceSize
        val mutable public imageMipTailOffset : VkDeviceSize
        val mutable public imageMipTailStride : VkDeviceSize

        new(formatProperties : VkSparseImageFormatProperties, imageMipTailFirstLod : uint32, imageMipTailSize : VkDeviceSize, imageMipTailOffset : VkDeviceSize, imageMipTailStride : VkDeviceSize) = { formatProperties = formatProperties; imageMipTailFirstLod = imageMipTailFirstLod; imageMipTailSize = imageMipTailSize; imageMipTailOffset = imageMipTailOffset; imageMipTailStride = imageMipTailStride }
        override x.ToString() =
            sprintf "VkSparseImageMemoryRequirements { formatProperties = %A; imageMipTailFirstLod = %A; imageMipTailSize = %A; imageMipTailOffset = %A; imageMipTailStride = %A }" x.formatProperties x.imageMipTailFirstLod x.imageMipTailSize x.imageMipTailOffset x.imageMipTailStride
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

        new(sType : VkStructureType, pNext : nativeint, waitSemaphoreCount : uint32, pWaitSemaphores : nativeptr<VkSemaphore>, pWaitDstStageMask : nativeptr<VkPipelineStageFlags>, commandBufferCount : uint32, pCommandBuffers : nativeptr<VkCommandBuffer>, signalSemaphoreCount : uint32, pSignalSemaphores : nativeptr<VkSemaphore>) = { sType = sType; pNext = pNext; waitSemaphoreCount = waitSemaphoreCount; pWaitSemaphores = pWaitSemaphores; pWaitDstStageMask = pWaitDstStageMask; commandBufferCount = commandBufferCount; pCommandBuffers = pCommandBuffers; signalSemaphoreCount = signalSemaphoreCount; pSignalSemaphores = pSignalSemaphores }
        override x.ToString() =
            sprintf "VkSubmitInfo { sType = %A; pNext = %A; waitSemaphoreCount = %A; pWaitSemaphores = %A; pWaitDstStageMask = %A; commandBufferCount = %A; pCommandBuffers = %A; signalSemaphoreCount = %A; pSignalSemaphores = %A }" x.sType x.pNext x.waitSemaphoreCount x.pWaitSemaphores x.pWaitDstStageMask x.commandBufferCount x.pCommandBuffers x.signalSemaphoreCount x.pSignalSemaphores
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSubresourceLayout = 
    struct
        val mutable public offset : VkDeviceSize
        val mutable public size : VkDeviceSize
        val mutable public rowPitch : VkDeviceSize
        val mutable public arrayPitch : VkDeviceSize
        val mutable public depthPitch : VkDeviceSize

        new(offset : VkDeviceSize, size : VkDeviceSize, rowPitch : VkDeviceSize, arrayPitch : VkDeviceSize, depthPitch : VkDeviceSize) = { offset = offset; size = size; rowPitch = rowPitch; arrayPitch = arrayPitch; depthPitch = depthPitch }
        override x.ToString() =
            sprintf "VkSubresourceLayout { offset = %A; size = %A; rowPitch = %A; arrayPitch = %A; depthPitch = %A }" x.offset x.size x.rowPitch x.arrayPitch x.depthPitch
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
        val mutable public currentTransform : VkSurfaceTransformFlagBitsKHR
        val mutable public supportedCompositeAlpha : VkCompositeAlphaFlagsKHR
        val mutable public supportedUsageFlags : VkImageUsageFlags

        new(minImageCount : uint32, maxImageCount : uint32, currentExtent : VkExtent2D, minImageExtent : VkExtent2D, maxImageExtent : VkExtent2D, maxImageArrayLayers : uint32, supportedTransforms : VkSurfaceTransformFlagsKHR, currentTransform : VkSurfaceTransformFlagBitsKHR, supportedCompositeAlpha : VkCompositeAlphaFlagsKHR, supportedUsageFlags : VkImageUsageFlags) = { minImageCount = minImageCount; maxImageCount = maxImageCount; currentExtent = currentExtent; minImageExtent = minImageExtent; maxImageExtent = maxImageExtent; maxImageArrayLayers = maxImageArrayLayers; supportedTransforms = supportedTransforms; currentTransform = currentTransform; supportedCompositeAlpha = supportedCompositeAlpha; supportedUsageFlags = supportedUsageFlags }
        override x.ToString() =
            sprintf "VkSurfaceCapabilitiesKHR { minImageCount = %A; maxImageCount = %A; currentExtent = %A; minImageExtent = %A; maxImageExtent = %A; maxImageArrayLayers = %A; supportedTransforms = %A; currentTransform = %A; supportedCompositeAlpha = %A; supportedUsageFlags = %A }" x.minImageCount x.maxImageCount x.currentExtent x.minImageExtent x.maxImageExtent x.maxImageArrayLayers x.supportedTransforms x.currentTransform x.supportedCompositeAlpha x.supportedUsageFlags
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSurfaceFormatKHR = 
    struct
        val mutable public format : VkFormat
        val mutable public colorSpace : VkColorSpaceKHR

        new(format : VkFormat, colorSpace : VkColorSpaceKHR) = { format = format; colorSpace = colorSpace }
        override x.ToString() =
            sprintf "VkSurfaceFormatKHR { format = %A; colorSpace = %A }" x.format x.colorSpace
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
        val mutable public preTransform : VkSurfaceTransformFlagBitsKHR
        val mutable public compositeAlpha : VkCompositeAlphaFlagBitsKHR
        val mutable public presentMode : VkPresentModeKHR
        val mutable public clipped : VkBool32
        val mutable public oldSwapchain : VkSwapchainKHR

        new(sType : VkStructureType, pNext : nativeint, flags : VkSwapchainCreateFlagsKHR, surface : VkSurfaceKHR, minImageCount : uint32, imageFormat : VkFormat, imageColorSpace : VkColorSpaceKHR, imageExtent : VkExtent2D, imageArrayLayers : uint32, imageUsage : VkImageUsageFlags, imageSharingMode : VkSharingMode, queueFamilyIndexCount : uint32, pQueueFamilyIndices : nativeptr<uint32>, preTransform : VkSurfaceTransformFlagBitsKHR, compositeAlpha : VkCompositeAlphaFlagBitsKHR, presentMode : VkPresentModeKHR, clipped : VkBool32, oldSwapchain : VkSwapchainKHR) = { sType = sType; pNext = pNext; flags = flags; surface = surface; minImageCount = minImageCount; imageFormat = imageFormat; imageColorSpace = imageColorSpace; imageExtent = imageExtent; imageArrayLayers = imageArrayLayers; imageUsage = imageUsage; imageSharingMode = imageSharingMode; queueFamilyIndexCount = queueFamilyIndexCount; pQueueFamilyIndices = pQueueFamilyIndices; preTransform = preTransform; compositeAlpha = compositeAlpha; presentMode = presentMode; clipped = clipped; oldSwapchain = oldSwapchain }
        override x.ToString() =
            sprintf "VkSwapchainCreateInfoKHR { sType = %A; pNext = %A; flags = %A; surface = %A; minImageCount = %A; imageFormat = %A; imageColorSpace = %A; imageExtent = %A; imageArrayLayers = %A; imageUsage = %A; imageSharingMode = %A; queueFamilyIndexCount = %A; pQueueFamilyIndices = %A; preTransform = %A; compositeAlpha = %A; presentMode = %A; clipped = %A; oldSwapchain = %A }" x.sType x.pNext x.flags x.surface x.minImageCount x.imageFormat x.imageColorSpace x.imageExtent x.imageArrayLayers x.imageUsage x.imageSharingMode x.queueFamilyIndexCount x.pQueueFamilyIndices x.preTransform x.compositeAlpha x.presentMode x.clipped x.oldSwapchain
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

        new(sType : VkStructureType, pNext : nativeint, dstSet : VkDescriptorSet, dstBinding : uint32, dstArrayElement : uint32, descriptorCount : uint32, descriptorType : VkDescriptorType, pImageInfo : nativeptr<VkDescriptorImageInfo>, pBufferInfo : nativeptr<VkDescriptorBufferInfo>, pTexelBufferView : nativeptr<VkBufferView>) = { sType = sType; pNext = pNext; dstSet = dstSet; dstBinding = dstBinding; dstArrayElement = dstArrayElement; descriptorCount = descriptorCount; descriptorType = descriptorType; pImageInfo = pImageInfo; pBufferInfo = pBufferInfo; pTexelBufferView = pTexelBufferView }
        override x.ToString() =
            sprintf "VkWriteDescriptorSet { sType = %A; pNext = %A; dstSet = %A; dstBinding = %A; dstArrayElement = %A; descriptorCount = %A; descriptorType = %A; pImageInfo = %A; pBufferInfo = %A; pTexelBufferView = %A }" x.sType x.pNext x.dstSet x.dstBinding x.dstArrayElement x.descriptorCount x.descriptorType x.pImageInfo x.pBufferInfo x.pTexelBufferView
    end

module VkRaw = 
    [<CompilerMessage("activeInstance is for internal use only", 1337, IsError=false, IsHidden=true)>]
    let mutable internal activeInstance : VkInstance = 0n
    [<Literal>]
    let lib = "vulkan-1.dll"

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
    extern VkResult vkGetDeviceGroupPresentCapabilitiesKHX(VkDevice device, VkDeviceGroupPresentCapabilitiesKHX* pDeviceGroupPresentCapabilities)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkGetDeviceGroupSurfacePresentModesKHX(VkDevice device, VkSurfaceKHR surface, VkDeviceGroupPresentModeFlagsKHX* pModes)
    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
    extern VkResult vkGetPhysicalDevicePresentRectanglesKHX(VkPhysicalDevice physicalDevice, VkSurfaceKHR surface, uint32* pRectCount, VkRect2D* pRects)
    
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
        | VkDebugReportObjectTypeValidationCacheExt = 33
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDebugReportCallbackCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkDebugReportFlagsEXT
            val mutable public pfnCallback : PFN_vkDebugReportCallbackEXT
            val mutable public pUserData : nativeint
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkDebugReportFlagsEXT, pfnCallback : PFN_vkDebugReportCallbackEXT, pUserData : nativeint) = { sType = sType; pNext = pNext; flags = flags; pfnCallback = pfnCallback; pUserData = pUserData }
            override x.ToString() =
                sprintf "VkDebugReportCallbackCreateInfoEXT { sType = %A; pNext = %A; flags = %A; pfnCallback = %A; pUserData = %A }" x.sType x.pNext x.flags x.pfnCallback x.pUserData
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
    
            new(sType : VkStructureType, pNext : nativeint, rasterizationOrder : VkRasterizationOrderAMD) = { sType = sType; pNext = pNext; rasterizationOrder = rasterizationOrder }
            override x.ToString() =
                sprintf "VkPipelineRasterizationStateRasterizationOrderAMD { sType = %A; pNext = %A; rasterizationOrder = %A }" x.sType x.pNext x.rasterizationOrder
        end
    
    
    type VkStructureType with
         static member inline PipelineRasterizationStateRasterizationOrderAmd = unbox<VkStructureType> 1000018000
    

module AMDShaderBallot =
    let Name = "VK_AMD_shader_ballot"
    let Number = 38
    
    open EXTDebugReport
    
    
    
    

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
    
            new(numUsedVgprs : uint32, numUsedSgprs : uint32, ldsSizePerLocalWorkGroup : uint32, ldsUsageSizeInBytes : uint64, scratchMemUsageInBytes : uint64) = { numUsedVgprs = numUsedVgprs; numUsedSgprs = numUsedSgprs; ldsSizePerLocalWorkGroup = ldsSizePerLocalWorkGroup; ldsUsageSizeInBytes = ldsUsageSizeInBytes; scratchMemUsageInBytes = scratchMemUsageInBytes }
            override x.ToString() =
                sprintf "VkShaderResourceUsageAMD { numUsedVgprs = %A; numUsedSgprs = %A; ldsSizePerLocalWorkGroup = %A; ldsUsageSizeInBytes = %A; scratchMemUsageInBytes = %A }" x.numUsedVgprs x.numUsedSgprs x.ldsSizePerLocalWorkGroup x.ldsUsageSizeInBytes x.scratchMemUsageInBytes
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
    
            new(shaderStageMask : VkShaderStageFlags, resourceUsage : VkShaderResourceUsageAMD, numPhysicalVgprs : uint32, numPhysicalSgprs : uint32, numAvailableVgprs : uint32, numAvailableSgprs : uint32, computeWorkGroupSize : V3ui) = { shaderStageMask = shaderStageMask; resourceUsage = resourceUsage; numPhysicalVgprs = numPhysicalVgprs; numPhysicalSgprs = numPhysicalSgprs; numAvailableVgprs = numAvailableVgprs; numAvailableSgprs = numAvailableSgprs; computeWorkGroupSize = computeWorkGroupSize }
            override x.ToString() =
                sprintf "VkShaderStatisticsInfoAMD { shaderStageMask = %A; resourceUsage = %A; numPhysicalVgprs = %A; numPhysicalSgprs = %A; numAvailableVgprs = %A; numAvailableSgprs = %A; computeWorkGroupSize = %A }" x.shaderStageMask x.resourceUsage x.numPhysicalVgprs x.numPhysicalSgprs x.numAvailableVgprs x.numAvailableSgprs x.computeWorkGroupSize
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
    
    
    
    

module KHRGetPhysicalDeviceProperties2 =
    let Name = "VK_KHR_get_physical_device_properties2"
    let Number = 60
    
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkFormatProperties2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public formatProperties : VkFormatProperties
    
            new(sType : VkStructureType, pNext : nativeint, formatProperties : VkFormatProperties) = { sType = sType; pNext = pNext; formatProperties = formatProperties }
            override x.ToString() =
                sprintf "VkFormatProperties2KHR { sType = %A; pNext = %A; formatProperties = %A }" x.sType x.pNext x.formatProperties
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImageFormatProperties2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public imageFormatProperties : VkImageFormatProperties
    
            new(sType : VkStructureType, pNext : nativeint, imageFormatProperties : VkImageFormatProperties) = { sType = sType; pNext = pNext; imageFormatProperties = imageFormatProperties }
            override x.ToString() =
                sprintf "VkImageFormatProperties2KHR { sType = %A; pNext = %A; imageFormatProperties = %A }" x.sType x.pNext x.imageFormatProperties
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceFeatures2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public features : VkPhysicalDeviceFeatures
    
            new(sType : VkStructureType, pNext : nativeint, features : VkPhysicalDeviceFeatures) = { sType = sType; pNext = pNext; features = features }
            override x.ToString() =
                sprintf "VkPhysicalDeviceFeatures2KHR { sType = %A; pNext = %A; features = %A }" x.sType x.pNext x.features
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceImageFormatInfo2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public format : VkFormat
            val mutable public _type : VkImageType
            val mutable public tiling : VkImageTiling
            val mutable public usage : VkImageUsageFlags
            val mutable public flags : VkImageCreateFlags
    
            new(sType : VkStructureType, pNext : nativeint, format : VkFormat, _type : VkImageType, tiling : VkImageTiling, usage : VkImageUsageFlags, flags : VkImageCreateFlags) = { sType = sType; pNext = pNext; format = format; _type = _type; tiling = tiling; usage = usage; flags = flags }
            override x.ToString() =
                sprintf "VkPhysicalDeviceImageFormatInfo2KHR { sType = %A; pNext = %A; format = %A; _type = %A; tiling = %A; usage = %A; flags = %A }" x.sType x.pNext x.format x._type x.tiling x.usage x.flags
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceMemoryProperties2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public memoryProperties : VkPhysicalDeviceMemoryProperties
    
            new(sType : VkStructureType, pNext : nativeint, memoryProperties : VkPhysicalDeviceMemoryProperties) = { sType = sType; pNext = pNext; memoryProperties = memoryProperties }
            override x.ToString() =
                sprintf "VkPhysicalDeviceMemoryProperties2KHR { sType = %A; pNext = %A; memoryProperties = %A }" x.sType x.pNext x.memoryProperties
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceProperties2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public properties : VkPhysicalDeviceProperties
    
            new(sType : VkStructureType, pNext : nativeint, properties : VkPhysicalDeviceProperties) = { sType = sType; pNext = pNext; properties = properties }
            override x.ToString() =
                sprintf "VkPhysicalDeviceProperties2KHR { sType = %A; pNext = %A; properties = %A }" x.sType x.pNext x.properties
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceSparseImageFormatInfo2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public format : VkFormat
            val mutable public _type : VkImageType
            val mutable public samples : VkSampleCountFlags
            val mutable public usage : VkImageUsageFlags
            val mutable public tiling : VkImageTiling
    
            new(sType : VkStructureType, pNext : nativeint, format : VkFormat, _type : VkImageType, samples : VkSampleCountFlags, usage : VkImageUsageFlags, tiling : VkImageTiling) = { sType = sType; pNext = pNext; format = format; _type = _type; samples = samples; usage = usage; tiling = tiling }
            override x.ToString() =
                sprintf "VkPhysicalDeviceSparseImageFormatInfo2KHR { sType = %A; pNext = %A; format = %A; _type = %A; samples = %A; usage = %A; tiling = %A }" x.sType x.pNext x.format x._type x.samples x.usage x.tiling
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkQueueFamilyProperties2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public queueFamilyProperties : VkQueueFamilyProperties
    
            new(sType : VkStructureType, pNext : nativeint, queueFamilyProperties : VkQueueFamilyProperties) = { sType = sType; pNext = pNext; queueFamilyProperties = queueFamilyProperties }
            override x.ToString() =
                sprintf "VkQueueFamilyProperties2KHR { sType = %A; pNext = %A; queueFamilyProperties = %A }" x.sType x.pNext x.queueFamilyProperties
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSparseImageFormatProperties2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public properties : VkSparseImageFormatProperties
    
            new(sType : VkStructureType, pNext : nativeint, properties : VkSparseImageFormatProperties) = { sType = sType; pNext = pNext; properties = properties }
            override x.ToString() =
                sprintf "VkSparseImageFormatProperties2KHR { sType = %A; pNext = %A; properties = %A }" x.sType x.pNext x.properties
        end
    
    
    type VkStructureType with
         static member inline PhysicalDeviceFeatures2Khr = unbox<VkStructureType> 1000059000
         static member inline PhysicalDeviceProperties2Khr = unbox<VkStructureType> 1000059001
         static member inline FormatProperties2Khr = unbox<VkStructureType> 1000059002
         static member inline ImageFormatProperties2Khr = unbox<VkStructureType> 1000059003
         static member inline PhysicalDeviceImageFormatInfo2Khr = unbox<VkStructureType> 1000059004
         static member inline QueueFamilyProperties2Khr = unbox<VkStructureType> 1000059005
         static member inline PhysicalDeviceMemoryProperties2Khr = unbox<VkStructureType> 1000059006
         static member inline SparseImageFormatProperties2Khr = unbox<VkStructureType> 1000059007
         static member inline PhysicalDeviceSparseImageFormatInfo2Khr = unbox<VkStructureType> 1000059008
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceFeatures2KHRDel = delegate of VkPhysicalDevice * nativeptr<VkPhysicalDeviceFeatures2KHR> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceProperties2KHRDel = delegate of VkPhysicalDevice * nativeptr<VkPhysicalDeviceProperties2KHR> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceFormatProperties2KHRDel = delegate of VkPhysicalDevice * VkFormat * nativeptr<VkFormatProperties2KHR> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceImageFormatProperties2KHRDel = delegate of VkPhysicalDevice * nativeptr<VkPhysicalDeviceImageFormatInfo2KHR> * nativeptr<VkImageFormatProperties2KHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceQueueFamilyProperties2KHRDel = delegate of VkPhysicalDevice * nativeptr<uint32> * nativeptr<VkQueueFamilyProperties2KHR> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceMemoryProperties2KHRDel = delegate of VkPhysicalDevice * nativeptr<VkPhysicalDeviceMemoryProperties2KHR> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceSparseImageFormatProperties2KHRDel = delegate of VkPhysicalDevice * nativeptr<VkPhysicalDeviceSparseImageFormatInfo2KHR> * nativeptr<uint32> * nativeptr<VkSparseImageFormatProperties2KHR> -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_get_physical_device_properties2")
            static let s_vkGetPhysicalDeviceFeatures2KHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceFeatures2KHRDel> "vkGetPhysicalDeviceFeatures2KHR"
            static let s_vkGetPhysicalDeviceProperties2KHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceProperties2KHRDel> "vkGetPhysicalDeviceProperties2KHR"
            static let s_vkGetPhysicalDeviceFormatProperties2KHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceFormatProperties2KHRDel> "vkGetPhysicalDeviceFormatProperties2KHR"
            static let s_vkGetPhysicalDeviceImageFormatProperties2KHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceImageFormatProperties2KHRDel> "vkGetPhysicalDeviceImageFormatProperties2KHR"
            static let s_vkGetPhysicalDeviceQueueFamilyProperties2KHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceQueueFamilyProperties2KHRDel> "vkGetPhysicalDeviceQueueFamilyProperties2KHR"
            static let s_vkGetPhysicalDeviceMemoryProperties2KHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceMemoryProperties2KHRDel> "vkGetPhysicalDeviceMemoryProperties2KHR"
            static let s_vkGetPhysicalDeviceSparseImageFormatProperties2KHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceSparseImageFormatProperties2KHRDel> "vkGetPhysicalDeviceSparseImageFormatProperties2KHR"
            static do Report.End(3) |> ignore
            static member vkGetPhysicalDeviceFeatures2KHR = s_vkGetPhysicalDeviceFeatures2KHRDel
            static member vkGetPhysicalDeviceProperties2KHR = s_vkGetPhysicalDeviceProperties2KHRDel
            static member vkGetPhysicalDeviceFormatProperties2KHR = s_vkGetPhysicalDeviceFormatProperties2KHRDel
            static member vkGetPhysicalDeviceImageFormatProperties2KHR = s_vkGetPhysicalDeviceImageFormatProperties2KHRDel
            static member vkGetPhysicalDeviceQueueFamilyProperties2KHR = s_vkGetPhysicalDeviceQueueFamilyProperties2KHRDel
            static member vkGetPhysicalDeviceMemoryProperties2KHR = s_vkGetPhysicalDeviceMemoryProperties2KHRDel
            static member vkGetPhysicalDeviceSparseImageFormatProperties2KHR = s_vkGetPhysicalDeviceSparseImageFormatProperties2KHRDel
        let vkGetPhysicalDeviceFeatures2KHR(physicalDevice : VkPhysicalDevice, pFeatures : nativeptr<VkPhysicalDeviceFeatures2KHR>) = Loader<unit>.vkGetPhysicalDeviceFeatures2KHR.Invoke(physicalDevice, pFeatures)
        let vkGetPhysicalDeviceProperties2KHR(physicalDevice : VkPhysicalDevice, pProperties : nativeptr<VkPhysicalDeviceProperties2KHR>) = Loader<unit>.vkGetPhysicalDeviceProperties2KHR.Invoke(physicalDevice, pProperties)
        let vkGetPhysicalDeviceFormatProperties2KHR(physicalDevice : VkPhysicalDevice, format : VkFormat, pFormatProperties : nativeptr<VkFormatProperties2KHR>) = Loader<unit>.vkGetPhysicalDeviceFormatProperties2KHR.Invoke(physicalDevice, format, pFormatProperties)
        let vkGetPhysicalDeviceImageFormatProperties2KHR(physicalDevice : VkPhysicalDevice, pImageFormatInfo : nativeptr<VkPhysicalDeviceImageFormatInfo2KHR>, pImageFormatProperties : nativeptr<VkImageFormatProperties2KHR>) = Loader<unit>.vkGetPhysicalDeviceImageFormatProperties2KHR.Invoke(physicalDevice, pImageFormatInfo, pImageFormatProperties)
        let vkGetPhysicalDeviceQueueFamilyProperties2KHR(physicalDevice : VkPhysicalDevice, pQueueFamilyPropertyCount : nativeptr<uint32>, pQueueFamilyProperties : nativeptr<VkQueueFamilyProperties2KHR>) = Loader<unit>.vkGetPhysicalDeviceQueueFamilyProperties2KHR.Invoke(physicalDevice, pQueueFamilyPropertyCount, pQueueFamilyProperties)
        let vkGetPhysicalDeviceMemoryProperties2KHR(physicalDevice : VkPhysicalDevice, pMemoryProperties : nativeptr<VkPhysicalDeviceMemoryProperties2KHR>) = Loader<unit>.vkGetPhysicalDeviceMemoryProperties2KHR.Invoke(physicalDevice, pMemoryProperties)
        let vkGetPhysicalDeviceSparseImageFormatProperties2KHR(physicalDevice : VkPhysicalDevice, pFormatInfo : nativeptr<VkPhysicalDeviceSparseImageFormatInfo2KHR>, pPropertyCount : nativeptr<uint32>, pProperties : nativeptr<VkSparseImageFormatProperties2KHR>) = Loader<unit>.vkGetPhysicalDeviceSparseImageFormatProperties2KHR.Invoke(physicalDevice, pFormatInfo, pPropertyCount, pProperties)

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
    
            new(sType : VkStructureType, pNext : nativeint, supportsTextureGatherLODBiasAMD : VkBool32) = { sType = sType; pNext = pNext; supportsTextureGatherLODBiasAMD = supportsTextureGatherLODBiasAMD }
            override x.ToString() =
                sprintf "VkTextureLODGatherFormatPropertiesAMD { sType = %A; pNext = %A; supportsTextureGatherLODBiasAMD = %A }" x.sType x.pNext x.supportsTextureGatherLODBiasAMD
        end
    
    
    type VkStructureType with
         static member inline TextureLodGatherFormatPropertiesAmd = unbox<VkStructureType> 1000041000
    

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
    
            new(sType : VkStructureType, pNext : nativeint, handle : nativeint, stride : int, format : int, usage : int) = { sType = sType; pNext = pNext; handle = handle; stride = stride; format = format; usage = usage }
            override x.ToString() =
                sprintf "VkNativeBufferANDROID { sType = %A; pNext = %A; handle = %A; stride = %A; format = %A; usage = %A }" x.sType x.pNext x.handle x.stride x.format x.usage
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
    type VkDisplayPlaneAlphaFlagBitsKHR = 
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
    
            new(visibleRegion : VkExtent2D, refreshRate : uint32) = { visibleRegion = visibleRegion; refreshRate = refreshRate }
            override x.ToString() =
                sprintf "VkDisplayModeParametersKHR { visibleRegion = %A; refreshRate = %A }" x.visibleRegion x.refreshRate
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDisplayModeCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkDisplayModeCreateFlagsKHR
            val mutable public parameters : VkDisplayModeParametersKHR
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkDisplayModeCreateFlagsKHR, parameters : VkDisplayModeParametersKHR) = { sType = sType; pNext = pNext; flags = flags; parameters = parameters }
            override x.ToString() =
                sprintf "VkDisplayModeCreateInfoKHR { sType = %A; pNext = %A; flags = %A; parameters = %A }" x.sType x.pNext x.flags x.parameters
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDisplayModePropertiesKHR = 
        struct
            val mutable public displayMode : VkDisplayModeKHR
            val mutable public parameters : VkDisplayModeParametersKHR
    
            new(displayMode : VkDisplayModeKHR, parameters : VkDisplayModeParametersKHR) = { displayMode = displayMode; parameters = parameters }
            override x.ToString() =
                sprintf "VkDisplayModePropertiesKHR { displayMode = %A; parameters = %A }" x.displayMode x.parameters
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
    
            new(supportedAlpha : VkDisplayPlaneAlphaFlagsKHR, minSrcPosition : VkOffset2D, maxSrcPosition : VkOffset2D, minSrcExtent : VkExtent2D, maxSrcExtent : VkExtent2D, minDstPosition : VkOffset2D, maxDstPosition : VkOffset2D, minDstExtent : VkExtent2D, maxDstExtent : VkExtent2D) = { supportedAlpha = supportedAlpha; minSrcPosition = minSrcPosition; maxSrcPosition = maxSrcPosition; minSrcExtent = minSrcExtent; maxSrcExtent = maxSrcExtent; minDstPosition = minDstPosition; maxDstPosition = maxDstPosition; minDstExtent = minDstExtent; maxDstExtent = maxDstExtent }
            override x.ToString() =
                sprintf "VkDisplayPlaneCapabilitiesKHR { supportedAlpha = %A; minSrcPosition = %A; maxSrcPosition = %A; minSrcExtent = %A; maxSrcExtent = %A; minDstPosition = %A; maxDstPosition = %A; minDstExtent = %A; maxDstExtent = %A }" x.supportedAlpha x.minSrcPosition x.maxSrcPosition x.minSrcExtent x.maxSrcExtent x.minDstPosition x.maxDstPosition x.minDstExtent x.maxDstExtent
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDisplayPlanePropertiesKHR = 
        struct
            val mutable public currentDisplay : VkDisplayKHR
            val mutable public currentStackIndex : uint32
    
            new(currentDisplay : VkDisplayKHR, currentStackIndex : uint32) = { currentDisplay = currentDisplay; currentStackIndex = currentStackIndex }
            override x.ToString() =
                sprintf "VkDisplayPlanePropertiesKHR { currentDisplay = %A; currentStackIndex = %A }" x.currentDisplay x.currentStackIndex
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
    
            new(display : VkDisplayKHR, displayName : cstr, physicalDimensions : VkExtent2D, physicalResolution : VkExtent2D, supportedTransforms : VkSurfaceTransformFlagsKHR, planeReorderPossible : VkBool32, persistentContent : VkBool32) = { display = display; displayName = displayName; physicalDimensions = physicalDimensions; physicalResolution = physicalResolution; supportedTransforms = supportedTransforms; planeReorderPossible = planeReorderPossible; persistentContent = persistentContent }
            override x.ToString() =
                sprintf "VkDisplayPropertiesKHR { display = %A; displayName = %A; physicalDimensions = %A; physicalResolution = %A; supportedTransforms = %A; planeReorderPossible = %A; persistentContent = %A }" x.display x.displayName x.physicalDimensions x.physicalResolution x.supportedTransforms x.planeReorderPossible x.persistentContent
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
            val mutable public transform : VkSurfaceTransformFlagBitsKHR
            val mutable public globalAlpha : float32
            val mutable public alphaMode : VkDisplayPlaneAlphaFlagBitsKHR
            val mutable public imageExtent : VkExtent2D
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkDisplaySurfaceCreateFlagsKHR, displayMode : VkDisplayModeKHR, planeIndex : uint32, planeStackIndex : uint32, transform : VkSurfaceTransformFlagBitsKHR, globalAlpha : float32, alphaMode : VkDisplayPlaneAlphaFlagBitsKHR, imageExtent : VkExtent2D) = { sType = sType; pNext = pNext; flags = flags; displayMode = displayMode; planeIndex = planeIndex; planeStackIndex = planeStackIndex; transform = transform; globalAlpha = globalAlpha; alphaMode = alphaMode; imageExtent = imageExtent }
            override x.ToString() =
                sprintf "VkDisplaySurfaceCreateInfoKHR { sType = %A; pNext = %A; flags = %A; displayMode = %A; planeIndex = %A; planeStackIndex = %A; transform = %A; globalAlpha = %A; alphaMode = %A; imageExtent = %A }" x.sType x.pNext x.flags x.displayMode x.planeIndex x.planeStackIndex x.transform x.globalAlpha x.alphaMode x.imageExtent
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
    
            new(sType : VkStructureType, pNext : nativeint, advancedBlendCoherentOperations : VkBool32) = { sType = sType; pNext = pNext; advancedBlendCoherentOperations = advancedBlendCoherentOperations }
            override x.ToString() =
                sprintf "VkPhysicalDeviceBlendOperationAdvancedFeaturesEXT { sType = %A; pNext = %A; advancedBlendCoherentOperations = %A }" x.sType x.pNext x.advancedBlendCoherentOperations
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
    
            new(sType : VkStructureType, pNext : nativeint, advancedBlendMaxColorAttachments : uint32, advancedBlendIndependentBlend : VkBool32, advancedBlendNonPremultipliedSrcColor : VkBool32, advancedBlendNonPremultipliedDstColor : VkBool32, advancedBlendCorrelatedOverlap : VkBool32, advancedBlendAllOperations : VkBool32) = { sType = sType; pNext = pNext; advancedBlendMaxColorAttachments = advancedBlendMaxColorAttachments; advancedBlendIndependentBlend = advancedBlendIndependentBlend; advancedBlendNonPremultipliedSrcColor = advancedBlendNonPremultipliedSrcColor; advancedBlendNonPremultipliedDstColor = advancedBlendNonPremultipliedDstColor; advancedBlendCorrelatedOverlap = advancedBlendCorrelatedOverlap; advancedBlendAllOperations = advancedBlendAllOperations }
            override x.ToString() =
                sprintf "VkPhysicalDeviceBlendOperationAdvancedPropertiesEXT { sType = %A; pNext = %A; advancedBlendMaxColorAttachments = %A; advancedBlendIndependentBlend = %A; advancedBlendNonPremultipliedSrcColor = %A; advancedBlendNonPremultipliedDstColor = %A; advancedBlendCorrelatedOverlap = %A; advancedBlendAllOperations = %A }" x.sType x.pNext x.advancedBlendMaxColorAttachments x.advancedBlendIndependentBlend x.advancedBlendNonPremultipliedSrcColor x.advancedBlendNonPremultipliedDstColor x.advancedBlendCorrelatedOverlap x.advancedBlendAllOperations
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPipelineColorBlendAdvancedStateCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public srcPremultiplied : VkBool32
            val mutable public dstPremultiplied : VkBool32
            val mutable public blendOverlap : VkBlendOverlapEXT
    
            new(sType : VkStructureType, pNext : nativeint, srcPremultiplied : VkBool32, dstPremultiplied : VkBool32, blendOverlap : VkBlendOverlapEXT) = { sType = sType; pNext = pNext; srcPremultiplied = srcPremultiplied; dstPremultiplied = dstPremultiplied; blendOverlap = blendOverlap }
            override x.ToString() =
                sprintf "VkPipelineColorBlendAdvancedStateCreateInfoEXT { sType = %A; pNext = %A; srcPremultiplied = %A; dstPremultiplied = %A; blendOverlap = %A }" x.sType x.pNext x.srcPremultiplied x.dstPremultiplied x.blendOverlap
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
    
            new(sType : VkStructureType, pNext : nativeint, pMarkerName : cstr, color : V4f) = { sType = sType; pNext = pNext; pMarkerName = pMarkerName; color = color }
            override x.ToString() =
                sprintf "VkDebugMarkerMarkerInfoEXT { sType = %A; pNext = %A; pMarkerName = %A; color = %A }" x.sType x.pNext x.pMarkerName x.color
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDebugMarkerObjectNameInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public objectType : VkDebugReportObjectTypeEXT
            val mutable public _object : uint64
            val mutable public pObjectName : cstr
    
            new(sType : VkStructureType, pNext : nativeint, objectType : VkDebugReportObjectTypeEXT, _object : uint64, pObjectName : cstr) = { sType = sType; pNext = pNext; objectType = objectType; _object = _object; pObjectName = pObjectName }
            override x.ToString() =
                sprintf "VkDebugMarkerObjectNameInfoEXT { sType = %A; pNext = %A; objectType = %A; _object = %A; pObjectName = %A }" x.sType x.pNext x.objectType x._object x.pObjectName
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
    
            new(sType : VkStructureType, pNext : nativeint, objectType : VkDebugReportObjectTypeEXT, _object : uint64, tagName : uint64, tagSize : uint64, pTag : nativeint) = { sType = sType; pNext = pNext; objectType = objectType; _object = _object; tagName = tagName; tagSize = tagSize; pTag = pTag }
            override x.ToString() =
                sprintf "VkDebugMarkerObjectTagInfoEXT { sType = %A; pNext = %A; objectType = %A; _object = %A; tagName = %A; tagSize = %A; pTag = %A }" x.sType x.pNext x.objectType x._object x.tagName x.tagSize x.pTag
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

module EXTDepthRangeUnrestricted =
    let Name = "VK_EXT_depth_range_unrestricted"
    let Number = 14
    
    open EXTDebugReport
    
    
    
    

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
    
            new(sType : VkStructureType, pNext : nativeint, maxDiscardRectangles : uint32) = { sType = sType; pNext = pNext; maxDiscardRectangles = maxDiscardRectangles }
            override x.ToString() =
                sprintf "VkPhysicalDeviceDiscardRectanglePropertiesEXT { sType = %A; pNext = %A; maxDiscardRectangles = %A }" x.sType x.pNext x.maxDiscardRectangles
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
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineDiscardRectangleStateCreateFlagsEXT, discardRectangleMode : VkDiscardRectangleModeEXT, discardRectangleCount : uint32, pDiscardRectangles : nativeptr<VkRect2D>) = { sType = sType; pNext = pNext; flags = flags; discardRectangleMode = discardRectangleMode; discardRectangleCount = discardRectangleCount; pDiscardRectangles = pDiscardRectangles }
            override x.ToString() =
                sprintf "VkPipelineDiscardRectangleStateCreateInfoEXT { sType = %A; pNext = %A; flags = %A; discardRectangleMode = %A; discardRectangleCount = %A; pDiscardRectangles = %A }" x.sType x.pNext x.flags x.discardRectangleMode x.discardRectangleCount x.pDiscardRectangles
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
    type VkSurfaceCounterFlagBitsEXT = 
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
            val mutable public currentTransform : VkSurfaceTransformFlagBitsKHR
            val mutable public supportedCompositeAlpha : VkCompositeAlphaFlagsKHR
            val mutable public supportedUsageFlags : VkImageUsageFlags
            val mutable public supportedSurfaceCounters : VkSurfaceCounterFlagsEXT
    
            new(sType : VkStructureType, pNext : nativeint, minImageCount : uint32, maxImageCount : uint32, currentExtent : VkExtent2D, minImageExtent : VkExtent2D, maxImageExtent : VkExtent2D, maxImageArrayLayers : uint32, supportedTransforms : VkSurfaceTransformFlagsKHR, currentTransform : VkSurfaceTransformFlagBitsKHR, supportedCompositeAlpha : VkCompositeAlphaFlagsKHR, supportedUsageFlags : VkImageUsageFlags, supportedSurfaceCounters : VkSurfaceCounterFlagsEXT) = { sType = sType; pNext = pNext; minImageCount = minImageCount; maxImageCount = maxImageCount; currentExtent = currentExtent; minImageExtent = minImageExtent; maxImageExtent = maxImageExtent; maxImageArrayLayers = maxImageArrayLayers; supportedTransforms = supportedTransforms; currentTransform = currentTransform; supportedCompositeAlpha = supportedCompositeAlpha; supportedUsageFlags = supportedUsageFlags; supportedSurfaceCounters = supportedSurfaceCounters }
            override x.ToString() =
                sprintf "VkSurfaceCapabilities2EXT { sType = %A; pNext = %A; minImageCount = %A; maxImageCount = %A; currentExtent = %A; minImageExtent = %A; maxImageExtent = %A; maxImageArrayLayers = %A; supportedTransforms = %A; currentTransform = %A; supportedCompositeAlpha = %A; supportedUsageFlags = %A; supportedSurfaceCounters = %A }" x.sType x.pNext x.minImageCount x.maxImageCount x.currentExtent x.minImageExtent x.maxImageExtent x.maxImageArrayLayers x.supportedTransforms x.currentTransform x.supportedCompositeAlpha x.supportedUsageFlags x.supportedSurfaceCounters
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
    
            new(sType : VkStructureType, pNext : nativeint, deviceEvent : VkDeviceEventTypeEXT) = { sType = sType; pNext = pNext; deviceEvent = deviceEvent }
            override x.ToString() =
                sprintf "VkDeviceEventInfoEXT { sType = %A; pNext = %A; deviceEvent = %A }" x.sType x.pNext x.deviceEvent
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDisplayEventInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public displayEvent : VkDisplayEventTypeEXT
    
            new(sType : VkStructureType, pNext : nativeint, displayEvent : VkDisplayEventTypeEXT) = { sType = sType; pNext = pNext; displayEvent = displayEvent }
            override x.ToString() =
                sprintf "VkDisplayEventInfoEXT { sType = %A; pNext = %A; displayEvent = %A }" x.sType x.pNext x.displayEvent
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDisplayPowerInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public powerState : VkDisplayPowerStateEXT
    
            new(sType : VkStructureType, pNext : nativeint, powerState : VkDisplayPowerStateEXT) = { sType = sType; pNext = pNext; powerState = powerState }
            override x.ToString() =
                sprintf "VkDisplayPowerInfoEXT { sType = %A; pNext = %A; powerState = %A }" x.sType x.pNext x.powerState
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSwapchainCounterCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public surfaceCounters : VkSurfaceCounterFlagsEXT
    
            new(sType : VkStructureType, pNext : nativeint, surfaceCounters : VkSurfaceCounterFlagsEXT) = { sType = sType; pNext = pNext; surfaceCounters = surfaceCounters }
            override x.ToString() =
                sprintf "VkSwapchainCounterCreateInfoEXT { sType = %A; pNext = %A; surfaceCounters = %A }" x.sType x.pNext x.surfaceCounters
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
        type VkGetSwapchainCounterEXTDel = delegate of VkDevice * VkSwapchainKHR * VkSurfaceCounterFlagBitsEXT * nativeptr<uint64> -> VkResult
        
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
        let vkGetSwapchainCounterEXT(device : VkDevice, swapchain : VkSwapchainKHR, counter : VkSurfaceCounterFlagBitsEXT, pCounterValue : nativeptr<uint64>) = Loader<unit>.vkGetSwapchainCounterEXT.Invoke(device, swapchain, counter, pCounterValue)

module KHRExternalMemoryCapabilities =
    let Name = "VK_KHR_external_memory_capabilities"
    let Number = 72
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name ]
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    [<Flags>]
    type VkExternalMemoryHandleTypeFlagBitsKHR = 
        | None = 0
        | VkExternalMemoryHandleTypeOpaqueFdBitKhr = 0x00000001
        | VkExternalMemoryHandleTypeOpaqueWin32BitKhr = 0x00000002
        | VkExternalMemoryHandleTypeOpaqueWin32KmtBitKhr = 0x00000004
        | VkExternalMemoryHandleTypeD3d11TextureBitKhr = 0x00000008
        | VkExternalMemoryHandleTypeD3d11TextureKmtBitKhr = 0x00000010
        | VkExternalMemoryHandleTypeD3d12HeapBitKhr = 0x00000020
        | VkExternalMemoryHandleTypeD3d12ResourceBitKhr = 0x00000040
    
    [<Flags>]
    type VkExternalMemoryFeatureFlagBitsKHR = 
        | None = 0
        | VkExternalMemoryFeatureDedicatedOnlyBitKhr = 0x00000001
        | VkExternalMemoryFeatureExportableBitKhr = 0x00000002
        | VkExternalMemoryFeatureImportableBitKhr = 0x00000004
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExternalMemoryPropertiesKHR = 
        struct
            val mutable public externalMemoryFeatures : VkExternalMemoryFeatureFlagsKHR
            val mutable public exportFromImportedHandleTypes : VkExternalMemoryHandleTypeFlagsKHR
            val mutable public compatibleHandleTypes : VkExternalMemoryHandleTypeFlagsKHR
    
            new(externalMemoryFeatures : VkExternalMemoryFeatureFlagsKHR, exportFromImportedHandleTypes : VkExternalMemoryHandleTypeFlagsKHR, compatibleHandleTypes : VkExternalMemoryHandleTypeFlagsKHR) = { externalMemoryFeatures = externalMemoryFeatures; exportFromImportedHandleTypes = exportFromImportedHandleTypes; compatibleHandleTypes = compatibleHandleTypes }
            override x.ToString() =
                sprintf "VkExternalMemoryPropertiesKHR { externalMemoryFeatures = %A; exportFromImportedHandleTypes = %A; compatibleHandleTypes = %A }" x.externalMemoryFeatures x.exportFromImportedHandleTypes x.compatibleHandleTypes
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExternalBufferPropertiesKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public externalMemoryProperties : VkExternalMemoryPropertiesKHR
    
            new(sType : VkStructureType, pNext : nativeint, externalMemoryProperties : VkExternalMemoryPropertiesKHR) = { sType = sType; pNext = pNext; externalMemoryProperties = externalMemoryProperties }
            override x.ToString() =
                sprintf "VkExternalBufferPropertiesKHR { sType = %A; pNext = %A; externalMemoryProperties = %A }" x.sType x.pNext x.externalMemoryProperties
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExternalImageFormatPropertiesKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public externalMemoryProperties : VkExternalMemoryPropertiesKHR
    
            new(sType : VkStructureType, pNext : nativeint, externalMemoryProperties : VkExternalMemoryPropertiesKHR) = { sType = sType; pNext = pNext; externalMemoryProperties = externalMemoryProperties }
            override x.ToString() =
                sprintf "VkExternalImageFormatPropertiesKHR { sType = %A; pNext = %A; externalMemoryProperties = %A }" x.sType x.pNext x.externalMemoryProperties
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceExternalBufferInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkBufferCreateFlags
            val mutable public usage : VkBufferUsageFlags
            val mutable public handleType : VkExternalMemoryHandleTypeFlagBitsKHR
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkBufferCreateFlags, usage : VkBufferUsageFlags, handleType : VkExternalMemoryHandleTypeFlagBitsKHR) = { sType = sType; pNext = pNext; flags = flags; usage = usage; handleType = handleType }
            override x.ToString() =
                sprintf "VkPhysicalDeviceExternalBufferInfoKHR { sType = %A; pNext = %A; flags = %A; usage = %A; handleType = %A }" x.sType x.pNext x.flags x.usage x.handleType
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceExternalImageFormatInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleType : VkExternalMemoryHandleTypeFlagBitsKHR
    
            new(sType : VkStructureType, pNext : nativeint, handleType : VkExternalMemoryHandleTypeFlagBitsKHR) = { sType = sType; pNext = pNext; handleType = handleType }
            override x.ToString() =
                sprintf "VkPhysicalDeviceExternalImageFormatInfoKHR { sType = %A; pNext = %A; handleType = %A }" x.sType x.pNext x.handleType
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceIDPropertiesKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public deviceUUID : Guid
            val mutable public driverUUID : Guid
            val mutable public deviceLUID : byte_8
            val mutable public deviceNodeMask : uint32
            val mutable public deviceLUIDValid : VkBool32
    
            new(sType : VkStructureType, pNext : nativeint, deviceUUID : Guid, driverUUID : Guid, deviceLUID : byte_8, deviceNodeMask : uint32, deviceLUIDValid : VkBool32) = { sType = sType; pNext = pNext; deviceUUID = deviceUUID; driverUUID = driverUUID; deviceLUID = deviceLUID; deviceNodeMask = deviceNodeMask; deviceLUIDValid = deviceLUIDValid }
            override x.ToString() =
                sprintf "VkPhysicalDeviceIDPropertiesKHR { sType = %A; pNext = %A; deviceUUID = %A; driverUUID = %A; deviceLUID = %A; deviceNodeMask = %A; deviceLUIDValid = %A }" x.sType x.pNext x.deviceUUID x.driverUUID x.deviceLUID x.deviceNodeMask x.deviceLUIDValid
        end
    
    
    type VkStructureType with
         static member inline PhysicalDeviceExternalImageFormatInfoKhr = unbox<VkStructureType> 1000071000
         static member inline ExternalImageFormatPropertiesKhr = unbox<VkStructureType> 1000071001
         static member inline PhysicalDeviceExternalBufferInfoKhr = unbox<VkStructureType> 1000071002
         static member inline ExternalBufferPropertiesKhr = unbox<VkStructureType> 1000071003
         static member inline PhysicalDeviceIdPropertiesKhr = unbox<VkStructureType> 1000071004
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceExternalBufferPropertiesKHRDel = delegate of VkPhysicalDevice * nativeptr<VkPhysicalDeviceExternalBufferInfoKHR> * nativeptr<VkExternalBufferPropertiesKHR> -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_external_memory_capabilities")
            static let s_vkGetPhysicalDeviceExternalBufferPropertiesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceExternalBufferPropertiesKHRDel> "vkGetPhysicalDeviceExternalBufferPropertiesKHR"
            static do Report.End(3) |> ignore
            static member vkGetPhysicalDeviceExternalBufferPropertiesKHR = s_vkGetPhysicalDeviceExternalBufferPropertiesKHRDel
        let vkGetPhysicalDeviceExternalBufferPropertiesKHR(physicalDevice : VkPhysicalDevice, pExternalBufferInfo : nativeptr<VkPhysicalDeviceExternalBufferInfoKHR>, pExternalBufferProperties : nativeptr<VkExternalBufferPropertiesKHR>) = Loader<unit>.vkGetPhysicalDeviceExternalBufferPropertiesKHR.Invoke(physicalDevice, pExternalBufferInfo, pExternalBufferProperties)

module KHRExternalMemory =
    let Name = "VK_KHR_external_memory"
    let Number = 73
    
    let Required = [ KHRExternalMemoryCapabilities.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalMemoryCapabilities
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExportMemoryAllocateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleTypes : VkExternalMemoryHandleTypeFlagsKHR
    
            new(sType : VkStructureType, pNext : nativeint, handleTypes : VkExternalMemoryHandleTypeFlagsKHR) = { sType = sType; pNext = pNext; handleTypes = handleTypes }
            override x.ToString() =
                sprintf "VkExportMemoryAllocateInfoKHR { sType = %A; pNext = %A; handleTypes = %A }" x.sType x.pNext x.handleTypes
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExternalMemoryBufferCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleTypes : VkExternalMemoryHandleTypeFlagsKHR
    
            new(sType : VkStructureType, pNext : nativeint, handleTypes : VkExternalMemoryHandleTypeFlagsKHR) = { sType = sType; pNext = pNext; handleTypes = handleTypes }
            override x.ToString() =
                sprintf "VkExternalMemoryBufferCreateInfoKHR { sType = %A; pNext = %A; handleTypes = %A }" x.sType x.pNext x.handleTypes
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExternalMemoryImageCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleTypes : VkExternalMemoryHandleTypeFlagsKHR
    
            new(sType : VkStructureType, pNext : nativeint, handleTypes : VkExternalMemoryHandleTypeFlagsKHR) = { sType = sType; pNext = pNext; handleTypes = handleTypes }
            override x.ToString() =
                sprintf "VkExternalMemoryImageCreateInfoKHR { sType = %A; pNext = %A; handleTypes = %A }" x.sType x.pNext x.handleTypes
        end
    
    
    type VkResult with
         static member inline VkErrorInvalidExternalHandleKhr = unbox<VkResult> -1000072003
    type VkStructureType with
         static member inline ExternalMemoryBufferCreateInfoKhr = unbox<VkStructureType> 1000072000
         static member inline ExternalMemoryImageCreateInfoKhr = unbox<VkStructureType> 1000072001
         static member inline ExportMemoryAllocateInfoKhr = unbox<VkStructureType> 1000072002
    

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
            val mutable public handleType : VkExternalMemoryHandleTypeFlagBitsKHR
            val mutable public fd : int
    
            new(sType : VkStructureType, pNext : nativeint, handleType : VkExternalMemoryHandleTypeFlagBitsKHR, fd : int) = { sType = sType; pNext = pNext; handleType = handleType; fd = fd }
            override x.ToString() =
                sprintf "VkImportMemoryFdInfoKHR { sType = %A; pNext = %A; handleType = %A; fd = %A }" x.sType x.pNext x.handleType x.fd
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMemoryFdPropertiesKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public memoryTypeBits : uint32
    
            new(sType : VkStructureType, pNext : nativeint, memoryTypeBits : uint32) = { sType = sType; pNext = pNext; memoryTypeBits = memoryTypeBits }
            override x.ToString() =
                sprintf "VkMemoryFdPropertiesKHR { sType = %A; pNext = %A; memoryTypeBits = %A }" x.sType x.pNext x.memoryTypeBits
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMemoryGetFdInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public memory : VkDeviceMemory
            val mutable public handleType : VkExternalMemoryHandleTypeFlagBitsKHR
    
            new(sType : VkStructureType, pNext : nativeint, memory : VkDeviceMemory, handleType : VkExternalMemoryHandleTypeFlagBitsKHR) = { sType = sType; pNext = pNext; memory = memory; handleType = handleType }
            override x.ToString() =
                sprintf "VkMemoryGetFdInfoKHR { sType = %A; pNext = %A; memory = %A; handleType = %A }" x.sType x.pNext x.memory x.handleType
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
        type VkGetMemoryFdPropertiesKHRDel = delegate of VkDevice * VkExternalMemoryHandleTypeFlagBitsKHR * int * nativeptr<VkMemoryFdPropertiesKHR> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_external_memory_fd")
            static let s_vkGetMemoryFdKHRDel = VkRaw.vkImportInstanceDelegate<VkGetMemoryFdKHRDel> "vkGetMemoryFdKHR"
            static let s_vkGetMemoryFdPropertiesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetMemoryFdPropertiesKHRDel> "vkGetMemoryFdPropertiesKHR"
            static do Report.End(3) |> ignore
            static member vkGetMemoryFdKHR = s_vkGetMemoryFdKHRDel
            static member vkGetMemoryFdPropertiesKHR = s_vkGetMemoryFdPropertiesKHRDel
        let vkGetMemoryFdKHR(device : VkDevice, pGetFdInfo : nativeptr<VkMemoryGetFdInfoKHR>, pFd : nativeptr<int>) = Loader<unit>.vkGetMemoryFdKHR.Invoke(device, pGetFdInfo, pFd)
        let vkGetMemoryFdPropertiesKHR(device : VkDevice, handleType : VkExternalMemoryHandleTypeFlagBitsKHR, fd : int, pMemoryFdProperties : nativeptr<VkMemoryFdPropertiesKHR>) = Loader<unit>.vkGetMemoryFdPropertiesKHR.Invoke(device, handleType, fd, pMemoryFdProperties)

module EXTExternalMemoryDmaBuf =
    let Name = "VK_EXT_external_memory_dma_buf"
    let Number = 126
    
    let Required = [ KHRExternalMemory.Name; KHRExternalMemoryCapabilities.Name; KHRExternalMemoryFd.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalMemory
    open KHRExternalMemoryCapabilities
    open KHRExternalMemoryFd
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    
    type VkExternalMemoryHandleTypeFlagBitsKHR with
         static member inline VkExternalMemoryHandleTypeDmaBufBitExt = unbox<VkExternalMemoryHandleTypeFlagBitsKHR> 512
    

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
            val mutable public handleType : VkExternalMemoryHandleTypeFlagBitsKHR
            val mutable public pHostPointer : nativeint
    
            new(sType : VkStructureType, pNext : nativeint, handleType : VkExternalMemoryHandleTypeFlagBitsKHR, pHostPointer : nativeint) = { sType = sType; pNext = pNext; handleType = handleType; pHostPointer = pHostPointer }
            override x.ToString() =
                sprintf "VkImportMemoryHostPointerInfoEXT { sType = %A; pNext = %A; handleType = %A; pHostPointer = %A }" x.sType x.pNext x.handleType x.pHostPointer
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMemoryHostPointerPropertiesEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public memoryTypeBits : uint32
    
            new(sType : VkStructureType, pNext : nativeint, memoryTypeBits : uint32) = { sType = sType; pNext = pNext; memoryTypeBits = memoryTypeBits }
            override x.ToString() =
                sprintf "VkMemoryHostPointerPropertiesEXT { sType = %A; pNext = %A; memoryTypeBits = %A }" x.sType x.pNext x.memoryTypeBits
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceExternalMemoryHostPropertiesEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public minImportedHostPointerAlignment : VkDeviceSize
    
            new(sType : VkStructureType, pNext : nativeint, minImportedHostPointerAlignment : VkDeviceSize) = { sType = sType; pNext = pNext; minImportedHostPointerAlignment = minImportedHostPointerAlignment }
            override x.ToString() =
                sprintf "VkPhysicalDeviceExternalMemoryHostPropertiesEXT { sType = %A; pNext = %A; minImportedHostPointerAlignment = %A }" x.sType x.pNext x.minImportedHostPointerAlignment
        end
    
    
    type VkExternalMemoryHandleTypeFlagBitsKHR with
         static member inline VkExternalMemoryHandleTypeHostAllocationBitExt = unbox<VkExternalMemoryHandleTypeFlagBitsKHR> 128
         static member inline VkExternalMemoryHandleTypeHostMappedForeignMemoryBitExt = unbox<VkExternalMemoryHandleTypeFlagBitsKHR> 256
    type VkStructureType with
         static member inline ImportMemoryHostPointerInfoExt = unbox<VkStructureType> 1000178000
         static member inline MemoryHostPointerPropertiesExt = unbox<VkStructureType> 1000178001
         static member inline PhysicalDeviceExternalMemoryHostPropertiesExt = unbox<VkStructureType> 1000178002
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetMemoryHostPointerPropertiesEXTDel = delegate of VkDevice * VkExternalMemoryHandleTypeFlagBitsKHR * nativeint * nativeptr<VkMemoryHostPointerPropertiesEXT> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_EXT_external_memory_host")
            static let s_vkGetMemoryHostPointerPropertiesEXTDel = VkRaw.vkImportInstanceDelegate<VkGetMemoryHostPointerPropertiesEXTDel> "vkGetMemoryHostPointerPropertiesEXT"
            static do Report.End(3) |> ignore
            static member vkGetMemoryHostPointerPropertiesEXT = s_vkGetMemoryHostPointerPropertiesEXTDel
        let vkGetMemoryHostPointerPropertiesEXT(device : VkDevice, handleType : VkExternalMemoryHandleTypeFlagBitsKHR, pHostPointer : nativeint, pMemoryHostPointerProperties : nativeptr<VkMemoryHostPointerPropertiesEXT>) = Loader<unit>.vkGetMemoryHostPointerPropertiesEXT.Invoke(device, handleType, pHostPointer, pMemoryHostPointerProperties)

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
    
            new(sType : VkStructureType, pNext : nativeint, globalPriority : VkQueueGlobalPriorityEXT) = { sType = sType; pNext = pNext; globalPriority = globalPriority }
            override x.ToString() =
                sprintf "VkDeviceQueueGlobalPriorityCreateInfoEXT { sType = %A; pNext = %A; globalPriority = %A }" x.sType x.pNext x.globalPriority
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
    
            new(x : float32, y : float32) = { x = x; y = y }
            override x.ToString() =
                sprintf "VkXYColorEXT { x = %A; y = %A }" x.x x.y
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
    
            new(sType : VkStructureType, pNext : nativeint, displayPrimaryRed : VkXYColorEXT, displayPrimaryGreen : VkXYColorEXT, displayPrimaryBlue : VkXYColorEXT, whitePoint : VkXYColorEXT, maxLuminance : float32, minLuminance : float32, maxContentLightLevel : float32, maxFrameAverageLightLevel : float32) = { sType = sType; pNext = pNext; displayPrimaryRed = displayPrimaryRed; displayPrimaryGreen = displayPrimaryGreen; displayPrimaryBlue = displayPrimaryBlue; whitePoint = whitePoint; maxLuminance = maxLuminance; minLuminance = minLuminance; maxContentLightLevel = maxContentLightLevel; maxFrameAverageLightLevel = maxFrameAverageLightLevel }
            override x.ToString() =
                sprintf "VkHdrMetadataEXT { sType = %A; pNext = %A; displayPrimaryRed = %A; displayPrimaryGreen = %A; displayPrimaryBlue = %A; whitePoint = %A; maxLuminance = %A; minLuminance = %A; maxContentLightLevel = %A; maxFrameAverageLightLevel = %A }" x.sType x.pNext x.displayPrimaryRed x.displayPrimaryGreen x.displayPrimaryBlue x.whitePoint x.maxLuminance x.minLuminance x.maxContentLightLevel x.maxFrameAverageLightLevel
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
    
    
    
    

module EXTQueueFamilyForeign =
    let Name = "VK_EXT_queue_family_foreign"
    let Number = 127
    
    let Required = [ KHRExternalMemory.Name; KHRExternalMemoryCapabilities.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalMemory
    open KHRExternalMemoryCapabilities
    open KHRGetPhysicalDeviceProperties2
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
    
            new(x : float32, y : float32) = { x = x; y = y }
            override x.ToString() =
                sprintf "VkSampleLocationEXT { x = %A; y = %A }" x.x x.y
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
    
            new(sType : VkStructureType, pNext : nativeint, sampleLocationsPerPixel : VkSampleCountFlags, sampleLocationGridSize : VkExtent2D, sampleLocationsCount : uint32, pSampleLocations : nativeptr<VkSampleLocationEXT>) = { sType = sType; pNext = pNext; sampleLocationsPerPixel = sampleLocationsPerPixel; sampleLocationGridSize = sampleLocationGridSize; sampleLocationsCount = sampleLocationsCount; pSampleLocations = pSampleLocations }
            override x.ToString() =
                sprintf "VkSampleLocationsInfoEXT { sType = %A; pNext = %A; sampleLocationsPerPixel = %A; sampleLocationGridSize = %A; sampleLocationsCount = %A; pSampleLocations = %A }" x.sType x.pNext x.sampleLocationsPerPixel x.sampleLocationGridSize x.sampleLocationsCount x.pSampleLocations
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkAttachmentSampleLocationsEXT = 
        struct
            val mutable public attachmentIndex : uint32
            val mutable public sampleLocationsInfo : VkSampleLocationsInfoEXT
    
            new(attachmentIndex : uint32, sampleLocationsInfo : VkSampleLocationsInfoEXT) = { attachmentIndex = attachmentIndex; sampleLocationsInfo = sampleLocationsInfo }
            override x.ToString() =
                sprintf "VkAttachmentSampleLocationsEXT { attachmentIndex = %A; sampleLocationsInfo = %A }" x.attachmentIndex x.sampleLocationsInfo
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMultisamplePropertiesEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public maxSampleLocationGridSize : VkExtent2D
    
            new(sType : VkStructureType, pNext : nativeint, maxSampleLocationGridSize : VkExtent2D) = { sType = sType; pNext = pNext; maxSampleLocationGridSize = maxSampleLocationGridSize }
            override x.ToString() =
                sprintf "VkMultisamplePropertiesEXT { sType = %A; pNext = %A; maxSampleLocationGridSize = %A }" x.sType x.pNext x.maxSampleLocationGridSize
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
    
            new(sType : VkStructureType, pNext : nativeint, sampleLocationSampleCounts : VkSampleCountFlags, maxSampleLocationGridSize : VkExtent2D, sampleLocationCoordinateRange : V2f, sampleLocationSubPixelBits : uint32, variableSampleLocations : VkBool32) = { sType = sType; pNext = pNext; sampleLocationSampleCounts = sampleLocationSampleCounts; maxSampleLocationGridSize = maxSampleLocationGridSize; sampleLocationCoordinateRange = sampleLocationCoordinateRange; sampleLocationSubPixelBits = sampleLocationSubPixelBits; variableSampleLocations = variableSampleLocations }
            override x.ToString() =
                sprintf "VkPhysicalDeviceSampleLocationsPropertiesEXT { sType = %A; pNext = %A; sampleLocationSampleCounts = %A; maxSampleLocationGridSize = %A; sampleLocationCoordinateRange = %A; sampleLocationSubPixelBits = %A; variableSampleLocations = %A }" x.sType x.pNext x.sampleLocationSampleCounts x.maxSampleLocationGridSize x.sampleLocationCoordinateRange x.sampleLocationSubPixelBits x.variableSampleLocations
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPipelineSampleLocationsStateCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public sampleLocationsEnable : VkBool32
            val mutable public sampleLocationsInfo : VkSampleLocationsInfoEXT
    
            new(sType : VkStructureType, pNext : nativeint, sampleLocationsEnable : VkBool32, sampleLocationsInfo : VkSampleLocationsInfoEXT) = { sType = sType; pNext = pNext; sampleLocationsEnable = sampleLocationsEnable; sampleLocationsInfo = sampleLocationsInfo }
            override x.ToString() =
                sprintf "VkPipelineSampleLocationsStateCreateInfoEXT { sType = %A; pNext = %A; sampleLocationsEnable = %A; sampleLocationsInfo = %A }" x.sType x.pNext x.sampleLocationsEnable x.sampleLocationsInfo
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSubpassSampleLocationsEXT = 
        struct
            val mutable public subpassIndex : uint32
            val mutable public sampleLocationsInfo : VkSampleLocationsInfoEXT
    
            new(subpassIndex : uint32, sampleLocationsInfo : VkSampleLocationsInfoEXT) = { subpassIndex = subpassIndex; sampleLocationsInfo = sampleLocationsInfo }
            override x.ToString() =
                sprintf "VkSubpassSampleLocationsEXT { subpassIndex = %A; sampleLocationsInfo = %A }" x.subpassIndex x.sampleLocationsInfo
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
    
            new(sType : VkStructureType, pNext : nativeint, attachmentInitialSampleLocationsCount : uint32, pAttachmentInitialSampleLocations : nativeptr<VkAttachmentSampleLocationsEXT>, postSubpassSampleLocationsCount : uint32, pPostSubpassSampleLocations : nativeptr<VkSubpassSampleLocationsEXT>) = { sType = sType; pNext = pNext; attachmentInitialSampleLocationsCount = attachmentInitialSampleLocationsCount; pAttachmentInitialSampleLocations = pAttachmentInitialSampleLocations; postSubpassSampleLocationsCount = postSubpassSampleLocationsCount; pPostSubpassSampleLocations = pPostSubpassSampleLocations }
            override x.ToString() =
                sprintf "VkRenderPassSampleLocationsBeginInfoEXT { sType = %A; pNext = %A; attachmentInitialSampleLocationsCount = %A; pAttachmentInitialSampleLocations = %A; postSubpassSampleLocationsCount = %A; pPostSubpassSampleLocations = %A }" x.sType x.pNext x.attachmentInitialSampleLocationsCount x.pAttachmentInitialSampleLocations x.postSubpassSampleLocationsCount x.pPostSubpassSampleLocations
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
    
            new(sType : VkStructureType, pNext : nativeint, filterMinmaxSingleComponentFormats : VkBool32, filterMinmaxImageComponentMapping : VkBool32) = { sType = sType; pNext = pNext; filterMinmaxSingleComponentFormats = filterMinmaxSingleComponentFormats; filterMinmaxImageComponentMapping = filterMinmaxImageComponentMapping }
            override x.ToString() =
                sprintf "VkPhysicalDeviceSamplerFilterMinmaxPropertiesEXT { sType = %A; pNext = %A; filterMinmaxSingleComponentFormats = %A; filterMinmaxImageComponentMapping = %A }" x.sType x.pNext x.filterMinmaxSingleComponentFormats x.filterMinmaxImageComponentMapping
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSamplerReductionModeCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public reductionMode : VkSamplerReductionModeEXT
    
            new(sType : VkStructureType, pNext : nativeint, reductionMode : VkSamplerReductionModeEXT) = { sType = sType; pNext = pNext; reductionMode = reductionMode }
            override x.ToString() =
                sprintf "VkSamplerReductionModeCreateInfoEXT { sType = %A; pNext = %A; reductionMode = %A }" x.sType x.pNext x.reductionMode
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
    
            new(sType : VkStructureType, pNext : nativeint, validationCache : VkValidationCacheEXT) = { sType = sType; pNext = pNext; validationCache = validationCache }
            override x.ToString() =
                sprintf "VkShaderModuleValidationCacheCreateInfoEXT { sType = %A; pNext = %A; validationCache = %A }" x.sType x.pNext x.validationCache
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkValidationCacheCreateInfoEXT = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkValidationCacheCreateFlagsEXT
            val mutable public initialDataSize : uint64
            val mutable public pInitialData : nativeint
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkValidationCacheCreateFlagsEXT, initialDataSize : uint64, pInitialData : nativeint) = { sType = sType; pNext = pNext; flags = flags; initialDataSize = initialDataSize; pInitialData = pInitialData }
            override x.ToString() =
                sprintf "VkValidationCacheCreateInfoEXT { sType = %A; pNext = %A; flags = %A; initialDataSize = %A; pInitialData = %A }" x.sType x.pNext x.flags x.initialDataSize x.pInitialData
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
    
            new(sType : VkStructureType, pNext : nativeint, disabledValidationCheckCount : uint32, pDisabledValidationChecks : nativeptr<VkValidationCheckEXT>) = { sType = sType; pNext = pNext; disabledValidationCheckCount = disabledValidationCheckCount; pDisabledValidationChecks = pDisabledValidationChecks }
            override x.ToString() =
                sprintf "VkValidationFlagsEXT { sType = %A; pNext = %A; disabledValidationCheckCount = %A; pDisabledValidationChecks = %A }" x.sType x.pNext x.disabledValidationCheckCount x.pDisabledValidationChecks
        end
    
    
    type VkStructureType with
         static member inline ValidationFlagsExt = unbox<VkStructureType> 1000061000
    

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
    
            new(presentID : uint32, desiredPresentTime : uint64, actualPresentTime : uint64, earliestPresentTime : uint64, presentMargin : uint64) = { presentID = presentID; desiredPresentTime = desiredPresentTime; actualPresentTime = actualPresentTime; earliestPresentTime = earliestPresentTime; presentMargin = presentMargin }
            override x.ToString() =
                sprintf "VkPastPresentationTimingGOOGLE { presentID = %A; desiredPresentTime = %A; actualPresentTime = %A; earliestPresentTime = %A; presentMargin = %A }" x.presentID x.desiredPresentTime x.actualPresentTime x.earliestPresentTime x.presentMargin
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPresentTimeGOOGLE = 
        struct
            val mutable public presentID : uint32
            val mutable public desiredPresentTime : uint64
    
            new(presentID : uint32, desiredPresentTime : uint64) = { presentID = presentID; desiredPresentTime = desiredPresentTime }
            override x.ToString() =
                sprintf "VkPresentTimeGOOGLE { presentID = %A; desiredPresentTime = %A }" x.presentID x.desiredPresentTime
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPresentTimesInfoGOOGLE = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public swapchainCount : uint32
            val mutable public pTimes : nativeptr<VkPresentTimeGOOGLE>
    
            new(sType : VkStructureType, pNext : nativeint, swapchainCount : uint32, pTimes : nativeptr<VkPresentTimeGOOGLE>) = { sType = sType; pNext = pNext; swapchainCount = swapchainCount; pTimes = pTimes }
            override x.ToString() =
                sprintf "VkPresentTimesInfoGOOGLE { sType = %A; pNext = %A; swapchainCount = %A; pTimes = %A }" x.sType x.pNext x.swapchainCount x.pTimes
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkRefreshCycleDurationGOOGLE = 
        struct
            val mutable public refreshDuration : uint64
    
            new(refreshDuration : uint64) = { refreshDuration = refreshDuration }
            override x.ToString() =
                sprintf "VkRefreshCycleDurationGOOGLE { refreshDuration = %A }" x.refreshDuration
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
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDevice16BitStorageFeaturesKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public storageBuffer16BitAccess : VkBool32
            val mutable public uniformAndStorageBuffer16BitAccess : VkBool32
            val mutable public storagePushConstant16 : VkBool32
            val mutable public storageInputOutput16 : VkBool32
    
            new(sType : VkStructureType, pNext : nativeint, storageBuffer16BitAccess : VkBool32, uniformAndStorageBuffer16BitAccess : VkBool32, storagePushConstant16 : VkBool32, storageInputOutput16 : VkBool32) = { sType = sType; pNext = pNext; storageBuffer16BitAccess = storageBuffer16BitAccess; uniformAndStorageBuffer16BitAccess = uniformAndStorageBuffer16BitAccess; storagePushConstant16 = storagePushConstant16; storageInputOutput16 = storageInputOutput16 }
            override x.ToString() =
                sprintf "VkPhysicalDevice16BitStorageFeaturesKHR { sType = %A; pNext = %A; storageBuffer16BitAccess = %A; uniformAndStorageBuffer16BitAccess = %A; storagePushConstant16 = %A; storageInputOutput16 = %A }" x.sType x.pNext x.storageBuffer16BitAccess x.uniformAndStorageBuffer16BitAccess x.storagePushConstant16 x.storageInputOutput16
        end
    
    
    type VkStructureType with
         static member inline PhysicalDevice16bitStorageFeaturesKhr = unbox<VkStructureType> 1000083000
    

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
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkAndroidSurfaceCreateFlagsKHR, window : nativeptr<nativeint>) = { sType = sType; pNext = pNext; flags = flags; window = window }
            override x.ToString() =
                sprintf "VkAndroidSurfaceCreateInfoKHR { sType = %A; pNext = %A; flags = %A; window = %A }" x.sType x.pNext x.flags x.window
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

module KHRBindMemory2 =
    let Name = "VK_KHR_bind_memory2"
    let Number = 158
    
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkBindBufferMemoryInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public buffer : VkBuffer
            val mutable public memory : VkDeviceMemory
            val mutable public memoryOffset : VkDeviceSize
    
            new(sType : VkStructureType, pNext : nativeint, buffer : VkBuffer, memory : VkDeviceMemory, memoryOffset : VkDeviceSize) = { sType = sType; pNext = pNext; buffer = buffer; memory = memory; memoryOffset = memoryOffset }
            override x.ToString() =
                sprintf "VkBindBufferMemoryInfoKHR { sType = %A; pNext = %A; buffer = %A; memory = %A; memoryOffset = %A }" x.sType x.pNext x.buffer x.memory x.memoryOffset
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkBindImageMemoryInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public image : VkImage
            val mutable public memory : VkDeviceMemory
            val mutable public memoryOffset : VkDeviceSize
    
            new(sType : VkStructureType, pNext : nativeint, image : VkImage, memory : VkDeviceMemory, memoryOffset : VkDeviceSize) = { sType = sType; pNext = pNext; image = image; memory = memory; memoryOffset = memoryOffset }
            override x.ToString() =
                sprintf "VkBindImageMemoryInfoKHR { sType = %A; pNext = %A; image = %A; memory = %A; memoryOffset = %A }" x.sType x.pNext x.image x.memory x.memoryOffset
        end
    
    
    type VkImageCreateFlags with
         static member inline AliasBitKhr = unbox<VkImageCreateFlags> 1024
    type VkStructureType with
         static member inline BindBufferMemoryInfoKhr = unbox<VkStructureType> 1000157000
         static member inline BindImageMemoryInfoKhr = unbox<VkStructureType> 1000157001
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkBindBufferMemory2KHRDel = delegate of VkDevice * uint32 * nativeptr<VkBindBufferMemoryInfoKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkBindImageMemory2KHRDel = delegate of VkDevice * uint32 * nativeptr<VkBindImageMemoryInfoKHR> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_bind_memory2")
            static let s_vkBindBufferMemory2KHRDel = VkRaw.vkImportInstanceDelegate<VkBindBufferMemory2KHRDel> "vkBindBufferMemory2KHR"
            static let s_vkBindImageMemory2KHRDel = VkRaw.vkImportInstanceDelegate<VkBindImageMemory2KHRDel> "vkBindImageMemory2KHR"
            static do Report.End(3) |> ignore
            static member vkBindBufferMemory2KHR = s_vkBindBufferMemory2KHRDel
            static member vkBindImageMemory2KHR = s_vkBindImageMemory2KHRDel
        let vkBindBufferMemory2KHR(device : VkDevice, bindInfoCount : uint32, pBindInfos : nativeptr<VkBindBufferMemoryInfoKHR>) = Loader<unit>.vkBindBufferMemory2KHR.Invoke(device, bindInfoCount, pBindInfos)
        let vkBindImageMemory2KHR(device : VkDevice, bindInfoCount : uint32, pBindInfos : nativeptr<VkBindImageMemoryInfoKHR>) = Loader<unit>.vkBindImageMemory2KHR.Invoke(device, bindInfoCount, pBindInfos)

module KHRGetMemoryRequirements2 =
    let Name = "VK_KHR_get_memory_requirements2"
    let Number = 147
    
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkBufferMemoryRequirementsInfo2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public buffer : VkBuffer
    
            new(sType : VkStructureType, pNext : nativeint, buffer : VkBuffer) = { sType = sType; pNext = pNext; buffer = buffer }
            override x.ToString() =
                sprintf "VkBufferMemoryRequirementsInfo2KHR { sType = %A; pNext = %A; buffer = %A }" x.sType x.pNext x.buffer
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImageMemoryRequirementsInfo2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public image : VkImage
    
            new(sType : VkStructureType, pNext : nativeint, image : VkImage) = { sType = sType; pNext = pNext; image = image }
            override x.ToString() =
                sprintf "VkImageMemoryRequirementsInfo2KHR { sType = %A; pNext = %A; image = %A }" x.sType x.pNext x.image
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImageSparseMemoryRequirementsInfo2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public image : VkImage
    
            new(sType : VkStructureType, pNext : nativeint, image : VkImage) = { sType = sType; pNext = pNext; image = image }
            override x.ToString() =
                sprintf "VkImageSparseMemoryRequirementsInfo2KHR { sType = %A; pNext = %A; image = %A }" x.sType x.pNext x.image
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMemoryRequirements2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public memoryRequirements : VkMemoryRequirements
    
            new(sType : VkStructureType, pNext : nativeint, memoryRequirements : VkMemoryRequirements) = { sType = sType; pNext = pNext; memoryRequirements = memoryRequirements }
            override x.ToString() =
                sprintf "VkMemoryRequirements2KHR { sType = %A; pNext = %A; memoryRequirements = %A }" x.sType x.pNext x.memoryRequirements
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSparseImageMemoryRequirements2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public memoryRequirements : VkSparseImageMemoryRequirements
    
            new(sType : VkStructureType, pNext : nativeint, memoryRequirements : VkSparseImageMemoryRequirements) = { sType = sType; pNext = pNext; memoryRequirements = memoryRequirements }
            override x.ToString() =
                sprintf "VkSparseImageMemoryRequirements2KHR { sType = %A; pNext = %A; memoryRequirements = %A }" x.sType x.pNext x.memoryRequirements
        end
    
    
    type VkStructureType with
         static member inline BufferMemoryRequirementsInfo2Khr = unbox<VkStructureType> 1000146000
         static member inline ImageMemoryRequirementsInfo2Khr = unbox<VkStructureType> 1000146001
         static member inline ImageSparseMemoryRequirementsInfo2Khr = unbox<VkStructureType> 1000146002
         static member inline MemoryRequirements2Khr = unbox<VkStructureType> 1000146003
         static member inline SparseImageMemoryRequirements2Khr = unbox<VkStructureType> 1000146004
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetImageMemoryRequirements2KHRDel = delegate of VkDevice * nativeptr<VkImageMemoryRequirementsInfo2KHR> * nativeptr<VkMemoryRequirements2KHR> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetBufferMemoryRequirements2KHRDel = delegate of VkDevice * nativeptr<VkBufferMemoryRequirementsInfo2KHR> * nativeptr<VkMemoryRequirements2KHR> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetImageSparseMemoryRequirements2KHRDel = delegate of VkDevice * nativeptr<VkImageSparseMemoryRequirementsInfo2KHR> * nativeptr<uint32> * nativeptr<VkSparseImageMemoryRequirements2KHR> -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_get_memory_requirements2")
            static let s_vkGetImageMemoryRequirements2KHRDel = VkRaw.vkImportInstanceDelegate<VkGetImageMemoryRequirements2KHRDel> "vkGetImageMemoryRequirements2KHR"
            static let s_vkGetBufferMemoryRequirements2KHRDel = VkRaw.vkImportInstanceDelegate<VkGetBufferMemoryRequirements2KHRDel> "vkGetBufferMemoryRequirements2KHR"
            static let s_vkGetImageSparseMemoryRequirements2KHRDel = VkRaw.vkImportInstanceDelegate<VkGetImageSparseMemoryRequirements2KHRDel> "vkGetImageSparseMemoryRequirements2KHR"
            static do Report.End(3) |> ignore
            static member vkGetImageMemoryRequirements2KHR = s_vkGetImageMemoryRequirements2KHRDel
            static member vkGetBufferMemoryRequirements2KHR = s_vkGetBufferMemoryRequirements2KHRDel
            static member vkGetImageSparseMemoryRequirements2KHR = s_vkGetImageSparseMemoryRequirements2KHRDel
        let vkGetImageMemoryRequirements2KHR(device : VkDevice, pInfo : nativeptr<VkImageMemoryRequirementsInfo2KHR>, pMemoryRequirements : nativeptr<VkMemoryRequirements2KHR>) = Loader<unit>.vkGetImageMemoryRequirements2KHR.Invoke(device, pInfo, pMemoryRequirements)
        let vkGetBufferMemoryRequirements2KHR(device : VkDevice, pInfo : nativeptr<VkBufferMemoryRequirementsInfo2KHR>, pMemoryRequirements : nativeptr<VkMemoryRequirements2KHR>) = Loader<unit>.vkGetBufferMemoryRequirements2KHR.Invoke(device, pInfo, pMemoryRequirements)
        let vkGetImageSparseMemoryRequirements2KHR(device : VkDevice, pInfo : nativeptr<VkImageSparseMemoryRequirementsInfo2KHR>, pSparseMemoryRequirementCount : nativeptr<uint32>, pSparseMemoryRequirements : nativeptr<VkSparseImageMemoryRequirements2KHR>) = Loader<unit>.vkGetImageSparseMemoryRequirements2KHR.Invoke(device, pInfo, pSparseMemoryRequirementCount, pSparseMemoryRequirements)

module KHRDedicatedAllocation =
    let Name = "VK_KHR_dedicated_allocation"
    let Number = 128
    
    let Required = [ KHRGetMemoryRequirements2.Name ]
    open KHRGetMemoryRequirements2
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMemoryDedicatedAllocateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public image : VkImage
            val mutable public buffer : VkBuffer
    
            new(sType : VkStructureType, pNext : nativeint, image : VkImage, buffer : VkBuffer) = { sType = sType; pNext = pNext; image = image; buffer = buffer }
            override x.ToString() =
                sprintf "VkMemoryDedicatedAllocateInfoKHR { sType = %A; pNext = %A; image = %A; buffer = %A }" x.sType x.pNext x.image x.buffer
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMemoryDedicatedRequirementsKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public prefersDedicatedAllocation : VkBool32
            val mutable public requiresDedicatedAllocation : VkBool32
    
            new(sType : VkStructureType, pNext : nativeint, prefersDedicatedAllocation : VkBool32, requiresDedicatedAllocation : VkBool32) = { sType = sType; pNext = pNext; prefersDedicatedAllocation = prefersDedicatedAllocation; requiresDedicatedAllocation = requiresDedicatedAllocation }
            override x.ToString() =
                sprintf "VkMemoryDedicatedRequirementsKHR { sType = %A; pNext = %A; prefersDedicatedAllocation = %A; requiresDedicatedAllocation = %A }" x.sType x.pNext x.prefersDedicatedAllocation x.requiresDedicatedAllocation
        end
    
    
    type VkStructureType with
         static member inline MemoryDedicatedRequirementsKhr = unbox<VkStructureType> 1000127000
         static member inline MemoryDedicatedAllocateInfoKhr = unbox<VkStructureType> 1000127001
    

module KHRDescriptorUpdateTemplate =
    let Name = "VK_KHR_descriptor_update_template"
    let Number = 86
    
    open EXTDebugReport
    
    type VkDescriptorUpdateTemplateTypeKHR = 
        | VkDescriptorUpdateTemplateTypeDescriptorSetKhr = 0
        | VkDescriptorUpdateTemplateTypePushDescriptorsKhr = 1
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDescriptorUpdateTemplateEntryKHR = 
        struct
            val mutable public dstBinding : uint32
            val mutable public dstArrayElement : uint32
            val mutable public descriptorCount : uint32
            val mutable public descriptorType : VkDescriptorType
            val mutable public offset : uint64
            val mutable public stride : uint64
    
            new(dstBinding : uint32, dstArrayElement : uint32, descriptorCount : uint32, descriptorType : VkDescriptorType, offset : uint64, stride : uint64) = { dstBinding = dstBinding; dstArrayElement = dstArrayElement; descriptorCount = descriptorCount; descriptorType = descriptorType; offset = offset; stride = stride }
            override x.ToString() =
                sprintf "VkDescriptorUpdateTemplateEntryKHR { dstBinding = %A; dstArrayElement = %A; descriptorCount = %A; descriptorType = %A; offset = %A; stride = %A }" x.dstBinding x.dstArrayElement x.descriptorCount x.descriptorType x.offset x.stride
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDescriptorUpdateTemplateCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkDescriptorUpdateTemplateCreateFlagsKHR
            val mutable public descriptorUpdateEntryCount : uint32
            val mutable public pDescriptorUpdateEntries : nativeptr<VkDescriptorUpdateTemplateEntryKHR>
            val mutable public templateType : VkDescriptorUpdateTemplateTypeKHR
            val mutable public descriptorSetLayout : VkDescriptorSetLayout
            val mutable public pipelineBindPoint : VkPipelineBindPoint
            val mutable public pipelineLayout : VkPipelineLayout
            val mutable public set : uint32
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkDescriptorUpdateTemplateCreateFlagsKHR, descriptorUpdateEntryCount : uint32, pDescriptorUpdateEntries : nativeptr<VkDescriptorUpdateTemplateEntryKHR>, templateType : VkDescriptorUpdateTemplateTypeKHR, descriptorSetLayout : VkDescriptorSetLayout, pipelineBindPoint : VkPipelineBindPoint, pipelineLayout : VkPipelineLayout, set : uint32) = { sType = sType; pNext = pNext; flags = flags; descriptorUpdateEntryCount = descriptorUpdateEntryCount; pDescriptorUpdateEntries = pDescriptorUpdateEntries; templateType = templateType; descriptorSetLayout = descriptorSetLayout; pipelineBindPoint = pipelineBindPoint; pipelineLayout = pipelineLayout; set = set }
            override x.ToString() =
                sprintf "VkDescriptorUpdateTemplateCreateInfoKHR { sType = %A; pNext = %A; flags = %A; descriptorUpdateEntryCount = %A; pDescriptorUpdateEntries = %A; templateType = %A; descriptorSetLayout = %A; pipelineBindPoint = %A; pipelineLayout = %A; set = %A }" x.sType x.pNext x.flags x.descriptorUpdateEntryCount x.pDescriptorUpdateEntries x.templateType x.descriptorSetLayout x.pipelineBindPoint x.pipelineLayout x.set
        end
    
    
    type VkDebugReportObjectTypeEXT with
         static member inline VkDebugReportObjectTypeDescriptorUpdateTemplateKhrExt = unbox<VkDebugReportObjectTypeEXT> 1000085000
    type VkObjectType with
         static member inline DescriptorUpdateTemplateKhr = unbox<VkObjectType> 1000085000
    type VkStructureType with
         static member inline DescriptorUpdateTemplateCreateInfoKhr = unbox<VkStructureType> 1000085000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateDescriptorUpdateTemplateKHRDel = delegate of VkDevice * nativeptr<VkDescriptorUpdateTemplateCreateInfoKHR> * nativeptr<VkAllocationCallbacks> * nativeptr<VkDescriptorUpdateTemplateKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkDestroyDescriptorUpdateTemplateKHRDel = delegate of VkDevice * VkDescriptorUpdateTemplateKHR * nativeptr<VkAllocationCallbacks> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkUpdateDescriptorSetWithTemplateKHRDel = delegate of VkDevice * VkDescriptorSet * VkDescriptorUpdateTemplateKHR * nativeint -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdPushDescriptorSetWithTemplateKHRDel = delegate of VkCommandBuffer * VkDescriptorUpdateTemplateKHR * VkPipelineLayout * uint32 * nativeint -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_descriptor_update_template")
            static let s_vkCreateDescriptorUpdateTemplateKHRDel = VkRaw.vkImportInstanceDelegate<VkCreateDescriptorUpdateTemplateKHRDel> "vkCreateDescriptorUpdateTemplateKHR"
            static let s_vkDestroyDescriptorUpdateTemplateKHRDel = VkRaw.vkImportInstanceDelegate<VkDestroyDescriptorUpdateTemplateKHRDel> "vkDestroyDescriptorUpdateTemplateKHR"
            static let s_vkUpdateDescriptorSetWithTemplateKHRDel = VkRaw.vkImportInstanceDelegate<VkUpdateDescriptorSetWithTemplateKHRDel> "vkUpdateDescriptorSetWithTemplateKHR"
            static let s_vkCmdPushDescriptorSetWithTemplateKHRDel = VkRaw.vkImportInstanceDelegate<VkCmdPushDescriptorSetWithTemplateKHRDel> "vkCmdPushDescriptorSetWithTemplateKHR"
            static do Report.End(3) |> ignore
            static member vkCreateDescriptorUpdateTemplateKHR = s_vkCreateDescriptorUpdateTemplateKHRDel
            static member vkDestroyDescriptorUpdateTemplateKHR = s_vkDestroyDescriptorUpdateTemplateKHRDel
            static member vkUpdateDescriptorSetWithTemplateKHR = s_vkUpdateDescriptorSetWithTemplateKHRDel
            static member vkCmdPushDescriptorSetWithTemplateKHR = s_vkCmdPushDescriptorSetWithTemplateKHRDel
        let vkCreateDescriptorUpdateTemplateKHR(device : VkDevice, pCreateInfo : nativeptr<VkDescriptorUpdateTemplateCreateInfoKHR>, pAllocator : nativeptr<VkAllocationCallbacks>, pDescriptorUpdateTemplate : nativeptr<VkDescriptorUpdateTemplateKHR>) = Loader<unit>.vkCreateDescriptorUpdateTemplateKHR.Invoke(device, pCreateInfo, pAllocator, pDescriptorUpdateTemplate)
        let vkDestroyDescriptorUpdateTemplateKHR(device : VkDevice, descriptorUpdateTemplate : VkDescriptorUpdateTemplateKHR, pAllocator : nativeptr<VkAllocationCallbacks>) = Loader<unit>.vkDestroyDescriptorUpdateTemplateKHR.Invoke(device, descriptorUpdateTemplate, pAllocator)
        let vkUpdateDescriptorSetWithTemplateKHR(device : VkDevice, descriptorSet : VkDescriptorSet, descriptorUpdateTemplate : VkDescriptorUpdateTemplateKHR, pData : nativeint) = Loader<unit>.vkUpdateDescriptorSetWithTemplateKHR.Invoke(device, descriptorSet, descriptorUpdateTemplate, pData)
        let vkCmdPushDescriptorSetWithTemplateKHR(commandBuffer : VkCommandBuffer, descriptorUpdateTemplate : VkDescriptorUpdateTemplateKHR, layout : VkPipelineLayout, set : uint32, pData : nativeint) = Loader<unit>.vkCmdPushDescriptorSetWithTemplateKHR.Invoke(commandBuffer, descriptorUpdateTemplate, layout, set, pData)

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
    
            new(sType : VkStructureType, pNext : nativeint, srcRect : VkRect2D, dstRect : VkRect2D, persistent : VkBool32) = { sType = sType; pNext = pNext; srcRect = srcRect; dstRect = dstRect; persistent = persistent }
            override x.ToString() =
                sprintf "VkDisplayPresentInfoKHR { sType = %A; pNext = %A; srcRect = %A; dstRect = %A; persistent = %A }" x.sType x.pNext x.srcRect x.dstRect x.persistent
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
    
    [<Flags>]
    type VkExternalFenceHandleTypeFlagBitsKHR = 
        | None = 0
        | VkExternalFenceHandleTypeOpaqueFdBitKhr = 0x00000001
        | VkExternalFenceHandleTypeOpaqueWin32BitKhr = 0x00000002
        | VkExternalFenceHandleTypeOpaqueWin32KmtBitKhr = 0x00000004
        | VkExternalFenceHandleTypeSyncFdBitKhr = 0x00000008
    
    [<Flags>]
    type VkExternalFenceFeatureFlagBitsKHR = 
        | None = 0
        | VkExternalFenceFeatureExportableBitKhr = 0x00000001
        | VkExternalFenceFeatureImportableBitKhr = 0x00000002
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExternalFencePropertiesKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public exportFromImportedHandleTypes : VkExternalFenceHandleTypeFlagsKHR
            val mutable public compatibleHandleTypes : VkExternalFenceHandleTypeFlagsKHR
            val mutable public externalFenceFeatures : VkExternalFenceFeatureFlagsKHR
    
            new(sType : VkStructureType, pNext : nativeint, exportFromImportedHandleTypes : VkExternalFenceHandleTypeFlagsKHR, compatibleHandleTypes : VkExternalFenceHandleTypeFlagsKHR, externalFenceFeatures : VkExternalFenceFeatureFlagsKHR) = { sType = sType; pNext = pNext; exportFromImportedHandleTypes = exportFromImportedHandleTypes; compatibleHandleTypes = compatibleHandleTypes; externalFenceFeatures = externalFenceFeatures }
            override x.ToString() =
                sprintf "VkExternalFencePropertiesKHR { sType = %A; pNext = %A; exportFromImportedHandleTypes = %A; compatibleHandleTypes = %A; externalFenceFeatures = %A }" x.sType x.pNext x.exportFromImportedHandleTypes x.compatibleHandleTypes x.externalFenceFeatures
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceExternalFenceInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleType : VkExternalFenceHandleTypeFlagBitsKHR
    
            new(sType : VkStructureType, pNext : nativeint, handleType : VkExternalFenceHandleTypeFlagBitsKHR) = { sType = sType; pNext = pNext; handleType = handleType }
            override x.ToString() =
                sprintf "VkPhysicalDeviceExternalFenceInfoKHR { sType = %A; pNext = %A; handleType = %A }" x.sType x.pNext x.handleType
        end
    
    
    type VkStructureType with
         static member inline PhysicalDeviceExternalFenceInfoKhr = unbox<VkStructureType> 1000112000
         static member inline ExternalFencePropertiesKhr = unbox<VkStructureType> 1000112001
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceExternalFencePropertiesKHRDel = delegate of VkPhysicalDevice * nativeptr<VkPhysicalDeviceExternalFenceInfoKHR> * nativeptr<VkExternalFencePropertiesKHR> -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_external_fence_capabilities")
            static let s_vkGetPhysicalDeviceExternalFencePropertiesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceExternalFencePropertiesKHRDel> "vkGetPhysicalDeviceExternalFencePropertiesKHR"
            static do Report.End(3) |> ignore
            static member vkGetPhysicalDeviceExternalFencePropertiesKHR = s_vkGetPhysicalDeviceExternalFencePropertiesKHRDel
        let vkGetPhysicalDeviceExternalFencePropertiesKHR(physicalDevice : VkPhysicalDevice, pExternalFenceInfo : nativeptr<VkPhysicalDeviceExternalFenceInfoKHR>, pExternalFenceProperties : nativeptr<VkExternalFencePropertiesKHR>) = Loader<unit>.vkGetPhysicalDeviceExternalFencePropertiesKHR.Invoke(physicalDevice, pExternalFenceInfo, pExternalFenceProperties)

module KHRExternalFence =
    let Name = "VK_KHR_external_fence"
    let Number = 114
    
    let Required = [ KHRExternalFenceCapabilities.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalFenceCapabilities
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    [<Flags>]
    type VkFenceImportFlagBitsKHR = 
        | None = 0
        | VkFenceImportTemporaryBitKhr = 0x00000001
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExportFenceCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleTypes : VkExternalFenceHandleTypeFlagsKHR
    
            new(sType : VkStructureType, pNext : nativeint, handleTypes : VkExternalFenceHandleTypeFlagsKHR) = { sType = sType; pNext = pNext; handleTypes = handleTypes }
            override x.ToString() =
                sprintf "VkExportFenceCreateInfoKHR { sType = %A; pNext = %A; handleTypes = %A }" x.sType x.pNext x.handleTypes
        end
    
    
    type VkStructureType with
         static member inline ExportFenceCreateInfoKhr = unbox<VkStructureType> 1000113000
    

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
            val mutable public handleType : VkExternalFenceHandleTypeFlagBitsKHR
    
            new(sType : VkStructureType, pNext : nativeint, fence : VkFence, handleType : VkExternalFenceHandleTypeFlagBitsKHR) = { sType = sType; pNext = pNext; fence = fence; handleType = handleType }
            override x.ToString() =
                sprintf "VkFenceGetFdInfoKHR { sType = %A; pNext = %A; fence = %A; handleType = %A }" x.sType x.pNext x.fence x.handleType
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImportFenceFdInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public fence : VkFence
            val mutable public flags : VkFenceImportFlagsKHR
            val mutable public handleType : VkExternalFenceHandleTypeFlagBitsKHR
            val mutable public fd : int
    
            new(sType : VkStructureType, pNext : nativeint, fence : VkFence, flags : VkFenceImportFlagsKHR, handleType : VkExternalFenceHandleTypeFlagBitsKHR, fd : int) = { sType = sType; pNext = pNext; fence = fence; flags = flags; handleType = handleType; fd = fd }
            override x.ToString() =
                sprintf "VkImportFenceFdInfoKHR { sType = %A; pNext = %A; fence = %A; flags = %A; handleType = %A; fd = %A }" x.sType x.pNext x.fence x.flags x.handleType x.fd
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
    
            new(sType : VkStructureType, pNext : nativeint, pAttributes : nativeptr<nativeint>, dwAccess : uint32, name : cstr) = { sType = sType; pNext = pNext; pAttributes = pAttributes; dwAccess = dwAccess; name = name }
            override x.ToString() =
                sprintf "VkExportFenceWin32HandleInfoKHR { sType = %A; pNext = %A; pAttributes = %A; dwAccess = %A; name = %A }" x.sType x.pNext x.pAttributes x.dwAccess x.name
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkFenceGetWin32HandleInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public fence : VkFence
            val mutable public handleType : VkExternalFenceHandleTypeFlagBitsKHR
    
            new(sType : VkStructureType, pNext : nativeint, fence : VkFence, handleType : VkExternalFenceHandleTypeFlagBitsKHR) = { sType = sType; pNext = pNext; fence = fence; handleType = handleType }
            override x.ToString() =
                sprintf "VkFenceGetWin32HandleInfoKHR { sType = %A; pNext = %A; fence = %A; handleType = %A }" x.sType x.pNext x.fence x.handleType
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImportFenceWin32HandleInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public fence : VkFence
            val mutable public flags : VkFenceImportFlagsKHR
            val mutable public handleType : VkExternalFenceHandleTypeFlagBitsKHR
            val mutable public handle : nativeint
            val mutable public name : cstr
    
            new(sType : VkStructureType, pNext : nativeint, fence : VkFence, flags : VkFenceImportFlagsKHR, handleType : VkExternalFenceHandleTypeFlagBitsKHR, handle : nativeint, name : cstr) = { sType = sType; pNext = pNext; fence = fence; flags = flags; handleType = handleType; handle = handle; name = name }
            override x.ToString() =
                sprintf "VkImportFenceWin32HandleInfoKHR { sType = %A; pNext = %A; fence = %A; flags = %A; handleType = %A; handle = %A; name = %A }" x.sType x.pNext x.fence x.flags x.handleType x.handle x.name
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
    
            new(sType : VkStructureType, pNext : nativeint, pAttributes : nativeptr<nativeint>, dwAccess : uint32, name : cstr) = { sType = sType; pNext = pNext; pAttributes = pAttributes; dwAccess = dwAccess; name = name }
            override x.ToString() =
                sprintf "VkExportMemoryWin32HandleInfoKHR { sType = %A; pNext = %A; pAttributes = %A; dwAccess = %A; name = %A }" x.sType x.pNext x.pAttributes x.dwAccess x.name
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImportMemoryWin32HandleInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleType : VkExternalMemoryHandleTypeFlagBitsKHR
            val mutable public handle : nativeint
            val mutable public name : cstr
    
            new(sType : VkStructureType, pNext : nativeint, handleType : VkExternalMemoryHandleTypeFlagBitsKHR, handle : nativeint, name : cstr) = { sType = sType; pNext = pNext; handleType = handleType; handle = handle; name = name }
            override x.ToString() =
                sprintf "VkImportMemoryWin32HandleInfoKHR { sType = %A; pNext = %A; handleType = %A; handle = %A; name = %A }" x.sType x.pNext x.handleType x.handle x.name
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMemoryGetWin32HandleInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public memory : VkDeviceMemory
            val mutable public handleType : VkExternalMemoryHandleTypeFlagBitsKHR
    
            new(sType : VkStructureType, pNext : nativeint, memory : VkDeviceMemory, handleType : VkExternalMemoryHandleTypeFlagBitsKHR) = { sType = sType; pNext = pNext; memory = memory; handleType = handleType }
            override x.ToString() =
                sprintf "VkMemoryGetWin32HandleInfoKHR { sType = %A; pNext = %A; memory = %A; handleType = %A }" x.sType x.pNext x.memory x.handleType
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMemoryWin32HandlePropertiesKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public memoryTypeBits : uint32
    
            new(sType : VkStructureType, pNext : nativeint, memoryTypeBits : uint32) = { sType = sType; pNext = pNext; memoryTypeBits = memoryTypeBits }
            override x.ToString() =
                sprintf "VkMemoryWin32HandlePropertiesKHR { sType = %A; pNext = %A; memoryTypeBits = %A }" x.sType x.pNext x.memoryTypeBits
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
        type VkGetMemoryWin32HandlePropertiesKHRDel = delegate of VkDevice * VkExternalMemoryHandleTypeFlagBitsKHR * nativeint * nativeptr<VkMemoryWin32HandlePropertiesKHR> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_external_memory_win32")
            static let s_vkGetMemoryWin32HandleKHRDel = VkRaw.vkImportInstanceDelegate<VkGetMemoryWin32HandleKHRDel> "vkGetMemoryWin32HandleKHR"
            static let s_vkGetMemoryWin32HandlePropertiesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetMemoryWin32HandlePropertiesKHRDel> "vkGetMemoryWin32HandlePropertiesKHR"
            static do Report.End(3) |> ignore
            static member vkGetMemoryWin32HandleKHR = s_vkGetMemoryWin32HandleKHRDel
            static member vkGetMemoryWin32HandlePropertiesKHR = s_vkGetMemoryWin32HandlePropertiesKHRDel
        let vkGetMemoryWin32HandleKHR(device : VkDevice, pGetWin32HandleInfo : nativeptr<VkMemoryGetWin32HandleInfoKHR>, pHandle : nativeptr<nativeint>) = Loader<unit>.vkGetMemoryWin32HandleKHR.Invoke(device, pGetWin32HandleInfo, pHandle)
        let vkGetMemoryWin32HandlePropertiesKHR(device : VkDevice, handleType : VkExternalMemoryHandleTypeFlagBitsKHR, handle : nativeint, pMemoryWin32HandleProperties : nativeptr<VkMemoryWin32HandlePropertiesKHR>) = Loader<unit>.vkGetMemoryWin32HandlePropertiesKHR.Invoke(device, handleType, handle, pMemoryWin32HandleProperties)

module KHRExternalSemaphoreCapabilities =
    let Name = "VK_KHR_external_semaphore_capabilities"
    let Number = 77
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name ]
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    [<Flags>]
    type VkExternalSemaphoreHandleTypeFlagBitsKHR = 
        | None = 0
        | VkExternalSemaphoreHandleTypeOpaqueFdBitKhr = 0x00000001
        | VkExternalSemaphoreHandleTypeOpaqueWin32BitKhr = 0x00000002
        | VkExternalSemaphoreHandleTypeOpaqueWin32KmtBitKhr = 0x00000004
        | VkExternalSemaphoreHandleTypeD3d12FenceBitKhr = 0x00000008
        | VkExternalSemaphoreHandleTypeSyncFdBitKhr = 0x00000010
    
    [<Flags>]
    type VkExternalSemaphoreFeatureFlagBitsKHR = 
        | None = 0
        | VkExternalSemaphoreFeatureExportableBitKhr = 0x00000001
        | VkExternalSemaphoreFeatureImportableBitKhr = 0x00000002
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExternalSemaphorePropertiesKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public exportFromImportedHandleTypes : VkExternalSemaphoreHandleTypeFlagsKHR
            val mutable public compatibleHandleTypes : VkExternalSemaphoreHandleTypeFlagsKHR
            val mutable public externalSemaphoreFeatures : VkExternalSemaphoreFeatureFlagsKHR
    
            new(sType : VkStructureType, pNext : nativeint, exportFromImportedHandleTypes : VkExternalSemaphoreHandleTypeFlagsKHR, compatibleHandleTypes : VkExternalSemaphoreHandleTypeFlagsKHR, externalSemaphoreFeatures : VkExternalSemaphoreFeatureFlagsKHR) = { sType = sType; pNext = pNext; exportFromImportedHandleTypes = exportFromImportedHandleTypes; compatibleHandleTypes = compatibleHandleTypes; externalSemaphoreFeatures = externalSemaphoreFeatures }
            override x.ToString() =
                sprintf "VkExternalSemaphorePropertiesKHR { sType = %A; pNext = %A; exportFromImportedHandleTypes = %A; compatibleHandleTypes = %A; externalSemaphoreFeatures = %A }" x.sType x.pNext x.exportFromImportedHandleTypes x.compatibleHandleTypes x.externalSemaphoreFeatures
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceExternalSemaphoreInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleType : VkExternalSemaphoreHandleTypeFlagBitsKHR
    
            new(sType : VkStructureType, pNext : nativeint, handleType : VkExternalSemaphoreHandleTypeFlagBitsKHR) = { sType = sType; pNext = pNext; handleType = handleType }
            override x.ToString() =
                sprintf "VkPhysicalDeviceExternalSemaphoreInfoKHR { sType = %A; pNext = %A; handleType = %A }" x.sType x.pNext x.handleType
        end
    
    
    type VkStructureType with
         static member inline PhysicalDeviceExternalSemaphoreInfoKhr = unbox<VkStructureType> 1000076000
         static member inline ExternalSemaphorePropertiesKhr = unbox<VkStructureType> 1000076001
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceExternalSemaphorePropertiesKHRDel = delegate of VkPhysicalDevice * nativeptr<VkPhysicalDeviceExternalSemaphoreInfoKHR> * nativeptr<VkExternalSemaphorePropertiesKHR> -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_external_semaphore_capabilities")
            static let s_vkGetPhysicalDeviceExternalSemaphorePropertiesKHRDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceExternalSemaphorePropertiesKHRDel> "vkGetPhysicalDeviceExternalSemaphorePropertiesKHR"
            static do Report.End(3) |> ignore
            static member vkGetPhysicalDeviceExternalSemaphorePropertiesKHR = s_vkGetPhysicalDeviceExternalSemaphorePropertiesKHRDel
        let vkGetPhysicalDeviceExternalSemaphorePropertiesKHR(physicalDevice : VkPhysicalDevice, pExternalSemaphoreInfo : nativeptr<VkPhysicalDeviceExternalSemaphoreInfoKHR>, pExternalSemaphoreProperties : nativeptr<VkExternalSemaphorePropertiesKHR>) = Loader<unit>.vkGetPhysicalDeviceExternalSemaphorePropertiesKHR.Invoke(physicalDevice, pExternalSemaphoreInfo, pExternalSemaphoreProperties)

module KHRExternalSemaphore =
    let Name = "VK_KHR_external_semaphore"
    let Number = 78
    
    let Required = [ KHRExternalSemaphoreCapabilities.Name; KHRGetPhysicalDeviceProperties2.Name ]
    open KHRExternalSemaphoreCapabilities
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    [<Flags>]
    type VkSemaphoreImportFlagBitsKHR = 
        | None = 0
        | VkSemaphoreImportTemporaryBitKhr = 0x00000001
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExportSemaphoreCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleTypes : VkExternalSemaphoreHandleTypeFlagsKHR
    
            new(sType : VkStructureType, pNext : nativeint, handleTypes : VkExternalSemaphoreHandleTypeFlagsKHR) = { sType = sType; pNext = pNext; handleTypes = handleTypes }
            override x.ToString() =
                sprintf "VkExportSemaphoreCreateInfoKHR { sType = %A; pNext = %A; handleTypes = %A }" x.sType x.pNext x.handleTypes
        end
    
    
    type VkStructureType with
         static member inline ExportSemaphoreCreateInfoKhr = unbox<VkStructureType> 1000077000
    

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
            val mutable public flags : VkSemaphoreImportFlagsKHR
            val mutable public handleType : VkExternalSemaphoreHandleTypeFlagBitsKHR
            val mutable public fd : int
    
            new(sType : VkStructureType, pNext : nativeint, semaphore : VkSemaphore, flags : VkSemaphoreImportFlagsKHR, handleType : VkExternalSemaphoreHandleTypeFlagBitsKHR, fd : int) = { sType = sType; pNext = pNext; semaphore = semaphore; flags = flags; handleType = handleType; fd = fd }
            override x.ToString() =
                sprintf "VkImportSemaphoreFdInfoKHR { sType = %A; pNext = %A; semaphore = %A; flags = %A; handleType = %A; fd = %A }" x.sType x.pNext x.semaphore x.flags x.handleType x.fd
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSemaphoreGetFdInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public semaphore : VkSemaphore
            val mutable public handleType : VkExternalSemaphoreHandleTypeFlagBitsKHR
    
            new(sType : VkStructureType, pNext : nativeint, semaphore : VkSemaphore, handleType : VkExternalSemaphoreHandleTypeFlagBitsKHR) = { sType = sType; pNext = pNext; semaphore = semaphore; handleType = handleType }
            override x.ToString() =
                sprintf "VkSemaphoreGetFdInfoKHR { sType = %A; pNext = %A; semaphore = %A; handleType = %A }" x.sType x.pNext x.semaphore x.handleType
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
    
            new(sType : VkStructureType, pNext : nativeint, waitSemaphoreValuesCount : uint32, pWaitSemaphoreValues : nativeptr<uint64>, signalSemaphoreValuesCount : uint32, pSignalSemaphoreValues : nativeptr<uint64>) = { sType = sType; pNext = pNext; waitSemaphoreValuesCount = waitSemaphoreValuesCount; pWaitSemaphoreValues = pWaitSemaphoreValues; signalSemaphoreValuesCount = signalSemaphoreValuesCount; pSignalSemaphoreValues = pSignalSemaphoreValues }
            override x.ToString() =
                sprintf "VkD3D12FenceSubmitInfoKHR { sType = %A; pNext = %A; waitSemaphoreValuesCount = %A; pWaitSemaphoreValues = %A; signalSemaphoreValuesCount = %A; pSignalSemaphoreValues = %A }" x.sType x.pNext x.waitSemaphoreValuesCount x.pWaitSemaphoreValues x.signalSemaphoreValuesCount x.pSignalSemaphoreValues
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExportSemaphoreWin32HandleInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public pAttributes : nativeptr<nativeint>
            val mutable public dwAccess : uint32
            val mutable public name : cstr
    
            new(sType : VkStructureType, pNext : nativeint, pAttributes : nativeptr<nativeint>, dwAccess : uint32, name : cstr) = { sType = sType; pNext = pNext; pAttributes = pAttributes; dwAccess = dwAccess; name = name }
            override x.ToString() =
                sprintf "VkExportSemaphoreWin32HandleInfoKHR { sType = %A; pNext = %A; pAttributes = %A; dwAccess = %A; name = %A }" x.sType x.pNext x.pAttributes x.dwAccess x.name
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImportSemaphoreWin32HandleInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public semaphore : VkSemaphore
            val mutable public flags : VkSemaphoreImportFlagsKHR
            val mutable public handleType : VkExternalSemaphoreHandleTypeFlagBitsKHR
            val mutable public handle : nativeint
            val mutable public name : cstr
    
            new(sType : VkStructureType, pNext : nativeint, semaphore : VkSemaphore, flags : VkSemaphoreImportFlagsKHR, handleType : VkExternalSemaphoreHandleTypeFlagBitsKHR, handle : nativeint, name : cstr) = { sType = sType; pNext = pNext; semaphore = semaphore; flags = flags; handleType = handleType; handle = handle; name = name }
            override x.ToString() =
                sprintf "VkImportSemaphoreWin32HandleInfoKHR { sType = %A; pNext = %A; semaphore = %A; flags = %A; handleType = %A; handle = %A; name = %A }" x.sType x.pNext x.semaphore x.flags x.handleType x.handle x.name
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSemaphoreGetWin32HandleInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public semaphore : VkSemaphore
            val mutable public handleType : VkExternalSemaphoreHandleTypeFlagBitsKHR
    
            new(sType : VkStructureType, pNext : nativeint, semaphore : VkSemaphore, handleType : VkExternalSemaphoreHandleTypeFlagBitsKHR) = { sType = sType; pNext = pNext; semaphore = semaphore; handleType = handleType }
            override x.ToString() =
                sprintf "VkSemaphoreGetWin32HandleInfoKHR { sType = %A; pNext = %A; semaphore = %A; handleType = %A }" x.sType x.pNext x.semaphore x.handleType
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
    
            new(sType : VkStructureType, pNext : nativeint, surface : VkSurfaceKHR) = { sType = sType; pNext = pNext; surface = surface }
            override x.ToString() =
                sprintf "VkPhysicalDeviceSurfaceInfo2KHR { sType = %A; pNext = %A; surface = %A }" x.sType x.pNext x.surface
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSurfaceCapabilities2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public surfaceCapabilities : VkSurfaceCapabilitiesKHR
    
            new(sType : VkStructureType, pNext : nativeint, surfaceCapabilities : VkSurfaceCapabilitiesKHR) = { sType = sType; pNext = pNext; surfaceCapabilities = surfaceCapabilities }
            override x.ToString() =
                sprintf "VkSurfaceCapabilities2KHR { sType = %A; pNext = %A; surfaceCapabilities = %A }" x.sType x.pNext x.surfaceCapabilities
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSurfaceFormat2KHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public surfaceFormat : VkSurfaceFormatKHR
    
            new(sType : VkStructureType, pNext : nativeint, surfaceFormat : VkSurfaceFormatKHR) = { sType = sType; pNext = pNext; surfaceFormat = surfaceFormat }
            override x.ToString() =
                sprintf "VkSurfaceFormat2KHR { sType = %A; pNext = %A; surfaceFormat = %A }" x.sType x.pNext x.surfaceFormat
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
    
            new(sType : VkStructureType, pNext : nativeint, viewFormatCount : uint32, pViewFormats : nativeptr<VkFormat>) = { sType = sType; pNext = pNext; viewFormatCount = viewFormatCount; pViewFormats = pViewFormats }
            override x.ToString() =
                sprintf "VkImageFormatListCreateInfoKHR { sType = %A; pNext = %A; viewFormatCount = %A; pViewFormats = %A }" x.sType x.pNext x.viewFormatCount x.pViewFormats
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
    
            new(offset : VkOffset2D, extent : VkExtent2D, layer : uint32) = { offset = offset; extent = extent; layer = layer }
            override x.ToString() =
                sprintf "VkRectLayerKHR { offset = %A; extent = %A; layer = %A }" x.offset x.extent x.layer
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPresentRegionKHR = 
        struct
            val mutable public rectangleCount : uint32
            val mutable public pRectangles : nativeptr<VkRectLayerKHR>
    
            new(rectangleCount : uint32, pRectangles : nativeptr<VkRectLayerKHR>) = { rectangleCount = rectangleCount; pRectangles = pRectangles }
            override x.ToString() =
                sprintf "VkPresentRegionKHR { rectangleCount = %A; pRectangles = %A }" x.rectangleCount x.pRectangles
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPresentRegionsKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public swapchainCount : uint32
            val mutable public pRegions : nativeptr<VkPresentRegionKHR>
    
            new(sType : VkStructureType, pNext : nativeint, swapchainCount : uint32, pRegions : nativeptr<VkPresentRegionKHR>) = { sType = sType; pNext = pNext; swapchainCount = swapchainCount; pRegions = pRegions }
            override x.ToString() =
                sprintf "VkPresentRegionsKHR { sType = %A; pNext = %A; swapchainCount = %A; pRegions = %A }" x.sType x.pNext x.swapchainCount x.pRegions
        end
    
    
    type VkStructureType with
         static member inline PresentRegionsKhr = unbox<VkStructureType> 1000084000
    

module KHRMaintenance1 =
    let Name = "VK_KHR_maintenance1"
    let Number = 70
    
    open EXTDebugReport
    
    
    
    type VkFormatFeatureFlags with
         static member inline TransferSrcBitKhr = unbox<VkFormatFeatureFlags> 16384
         static member inline TransferDstBitKhr = unbox<VkFormatFeatureFlags> 32768
    type VkImageCreateFlags with
         static member inline D2dArrayCompatibleBitKhr = unbox<VkImageCreateFlags> 32
    type VkResult with
         static member inline VkErrorOutOfPoolMemoryKhr = unbox<VkResult> -1000069000
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkTrimCommandPoolKHRDel = delegate of VkDevice * VkCommandPool * VkCommandPoolTrimFlagsKHR -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_maintenance1")
            static let s_vkTrimCommandPoolKHRDel = VkRaw.vkImportInstanceDelegate<VkTrimCommandPoolKHRDel> "vkTrimCommandPoolKHR"
            static do Report.End(3) |> ignore
            static member vkTrimCommandPoolKHR = s_vkTrimCommandPoolKHRDel
        let vkTrimCommandPoolKHR(device : VkDevice, commandPool : VkCommandPool, flags : VkCommandPoolTrimFlagsKHR) = Loader<unit>.vkTrimCommandPoolKHR.Invoke(device, commandPool, flags)

module KHRMaintenance2 =
    let Name = "VK_KHR_maintenance2"
    let Number = 118
    
    open EXTDebugReport
    
    type VkPointClippingBehaviorKHR = 
        | VkPointClippingBehaviorAllClipPlanesKhr = 0
        | VkPointClippingBehaviorUserClipPlanesOnlyKhr = 1
    
    type VkTessellationDomainOriginKHR = 
        | VkTessellationDomainOriginUpperLeftKhr = 0
        | VkTessellationDomainOriginLowerLeftKhr = 1
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImageViewUsageCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public usage : VkImageUsageFlags
    
            new(sType : VkStructureType, pNext : nativeint, usage : VkImageUsageFlags) = { sType = sType; pNext = pNext; usage = usage }
            override x.ToString() =
                sprintf "VkImageViewUsageCreateInfoKHR { sType = %A; pNext = %A; usage = %A }" x.sType x.pNext x.usage
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkInputAttachmentAspectReferenceKHR = 
        struct
            val mutable public subpass : uint32
            val mutable public inputAttachmentIndex : uint32
            val mutable public aspectMask : VkImageAspectFlags
    
            new(subpass : uint32, inputAttachmentIndex : uint32, aspectMask : VkImageAspectFlags) = { subpass = subpass; inputAttachmentIndex = inputAttachmentIndex; aspectMask = aspectMask }
            override x.ToString() =
                sprintf "VkInputAttachmentAspectReferenceKHR { subpass = %A; inputAttachmentIndex = %A; aspectMask = %A }" x.subpass x.inputAttachmentIndex x.aspectMask
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDevicePointClippingPropertiesKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public pointClippingBehavior : VkPointClippingBehaviorKHR
    
            new(sType : VkStructureType, pNext : nativeint, pointClippingBehavior : VkPointClippingBehaviorKHR) = { sType = sType; pNext = pNext; pointClippingBehavior = pointClippingBehavior }
            override x.ToString() =
                sprintf "VkPhysicalDevicePointClippingPropertiesKHR { sType = %A; pNext = %A; pointClippingBehavior = %A }" x.sType x.pNext x.pointClippingBehavior
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPipelineTessellationDomainOriginStateCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public domainOrigin : VkTessellationDomainOriginKHR
    
            new(sType : VkStructureType, pNext : nativeint, domainOrigin : VkTessellationDomainOriginKHR) = { sType = sType; pNext = pNext; domainOrigin = domainOrigin }
            override x.ToString() =
                sprintf "VkPipelineTessellationDomainOriginStateCreateInfoKHR { sType = %A; pNext = %A; domainOrigin = %A }" x.sType x.pNext x.domainOrigin
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkRenderPassInputAttachmentAspectCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public aspectReferenceCount : uint32
            val mutable public pAspectReferences : nativeptr<VkInputAttachmentAspectReferenceKHR>
    
            new(sType : VkStructureType, pNext : nativeint, aspectReferenceCount : uint32, pAspectReferences : nativeptr<VkInputAttachmentAspectReferenceKHR>) = { sType = sType; pNext = pNext; aspectReferenceCount = aspectReferenceCount; pAspectReferences = pAspectReferences }
            override x.ToString() =
                sprintf "VkRenderPassInputAttachmentAspectCreateInfoKHR { sType = %A; pNext = %A; aspectReferenceCount = %A; pAspectReferences = %A }" x.sType x.pNext x.aspectReferenceCount x.pAspectReferences
        end
    
    
    type VkImageCreateFlags with
         static member inline BlockTexelViewCompatibleBitKhr = unbox<VkImageCreateFlags> 128
         static member inline ExtendedUsageBitKhr = unbox<VkImageCreateFlags> 256
    type VkImageLayout with
         static member inline DepthReadOnlyStencilAttachmentOptimalKhr = unbox<VkImageLayout> 1000117000
         static member inline DepthAttachmentStencilReadOnlyOptimalKhr = unbox<VkImageLayout> 1000117001
    type VkStructureType with
         static member inline PhysicalDevicePointClippingPropertiesKhr = unbox<VkStructureType> 1000117000
         static member inline RenderPassInputAttachmentAspectCreateInfoKhr = unbox<VkStructureType> 1000117001
         static member inline ImageViewUsageCreateInfoKhr = unbox<VkStructureType> 1000117002
         static member inline PipelineTessellationDomainOriginStateCreateInfoKhr = unbox<VkStructureType> 1000117003
    

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
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkMirSurfaceCreateFlagsKHR, connection : nativeptr<nativeint>, mirSurface : nativeptr<nativeint>) = { sType = sType; pNext = pNext; flags = flags; connection = connection; mirSurface = mirSurface }
            override x.ToString() =
                sprintf "VkMirSurfaceCreateInfoKHR { sType = %A; pNext = %A; flags = %A; connection = %A; mirSurface = %A }" x.sType x.pNext x.flags x.connection x.mirSurface
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
    
            new(sType : VkStructureType, pNext : nativeint, maxPushDescriptors : uint32) = { sType = sType; pNext = pNext; maxPushDescriptors = maxPushDescriptors }
            override x.ToString() =
                sprintf "VkPhysicalDevicePushDescriptorPropertiesKHR { sType = %A; pNext = %A; maxPushDescriptors = %A }" x.sType x.pNext x.maxPushDescriptors
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
    
    
    
    

module KHRSamplerYcbcrConversion =
    let Name = "VK_KHR_sampler_ycbcr_conversion"
    let Number = 157
    
    let Required = [ KHRBindMemory2.Name; KHRGetMemoryRequirements2.Name; KHRGetPhysicalDeviceProperties2.Name; KHRMaintenance1.Name ]
    open KHRBindMemory2
    open KHRGetMemoryRequirements2
    open KHRGetPhysicalDeviceProperties2
    open KHRMaintenance1
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkBindImagePlaneMemoryInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public planeAspect : VkImageAspectFlags
    
            new(sType : VkStructureType, pNext : nativeint, planeAspect : VkImageAspectFlags) = { sType = sType; pNext = pNext; planeAspect = planeAspect }
            override x.ToString() =
                sprintf "VkBindImagePlaneMemoryInfoKHR { sType = %A; pNext = %A; planeAspect = %A }" x.sType x.pNext x.planeAspect
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImagePlaneMemoryRequirementsInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public planeAspect : VkImageAspectFlags
    
            new(sType : VkStructureType, pNext : nativeint, planeAspect : VkImageAspectFlags) = { sType = sType; pNext = pNext; planeAspect = planeAspect }
            override x.ToString() =
                sprintf "VkImagePlaneMemoryRequirementsInfoKHR { sType = %A; pNext = %A; planeAspect = %A }" x.sType x.pNext x.planeAspect
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceSamplerYcbcrConversionFeaturesKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public samplerYcbcrConversion : VkBool32
    
            new(sType : VkStructureType, pNext : nativeint, samplerYcbcrConversion : VkBool32) = { sType = sType; pNext = pNext; samplerYcbcrConversion = samplerYcbcrConversion }
            override x.ToString() =
                sprintf "VkPhysicalDeviceSamplerYcbcrConversionFeaturesKHR { sType = %A; pNext = %A; samplerYcbcrConversion = %A }" x.sType x.pNext x.samplerYcbcrConversion
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSamplerYcbcrConversionCreateInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public format : VkFormat
            val mutable public ycbcrModel : VkSamplerYcbcrModelConversionKHR
            val mutable public ycbcrRange : VkSamplerYcbcrRangeKHR
            val mutable public components : VkComponentMapping
            val mutable public xChromaOffset : VkChromaLocationKHR
            val mutable public yChromaOffset : VkChromaLocationKHR
            val mutable public chromaFilter : VkFilter
            val mutable public forceExplicitReconstruction : VkBool32
    
            new(sType : VkStructureType, pNext : nativeint, format : VkFormat, ycbcrModel : VkSamplerYcbcrModelConversionKHR, ycbcrRange : VkSamplerYcbcrRangeKHR, components : VkComponentMapping, xChromaOffset : VkChromaLocationKHR, yChromaOffset : VkChromaLocationKHR, chromaFilter : VkFilter, forceExplicitReconstruction : VkBool32) = { sType = sType; pNext = pNext; format = format; ycbcrModel = ycbcrModel; ycbcrRange = ycbcrRange; components = components; xChromaOffset = xChromaOffset; yChromaOffset = yChromaOffset; chromaFilter = chromaFilter; forceExplicitReconstruction = forceExplicitReconstruction }
            override x.ToString() =
                sprintf "VkSamplerYcbcrConversionCreateInfoKHR { sType = %A; pNext = %A; format = %A; ycbcrModel = %A; ycbcrRange = %A; components = %A; xChromaOffset = %A; yChromaOffset = %A; chromaFilter = %A; forceExplicitReconstruction = %A }" x.sType x.pNext x.format x.ycbcrModel x.ycbcrRange x.components x.xChromaOffset x.yChromaOffset x.chromaFilter x.forceExplicitReconstruction
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSamplerYcbcrConversionImageFormatPropertiesKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public combinedImageSamplerDescriptorCount : uint32
    
            new(sType : VkStructureType, pNext : nativeint, combinedImageSamplerDescriptorCount : uint32) = { sType = sType; pNext = pNext; combinedImageSamplerDescriptorCount = combinedImageSamplerDescriptorCount }
            override x.ToString() =
                sprintf "VkSamplerYcbcrConversionImageFormatPropertiesKHR { sType = %A; pNext = %A; combinedImageSamplerDescriptorCount = %A }" x.sType x.pNext x.combinedImageSamplerDescriptorCount
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkSamplerYcbcrConversionInfoKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public conversion : VkSamplerYcbcrConversionKHR
    
            new(sType : VkStructureType, pNext : nativeint, conversion : VkSamplerYcbcrConversionKHR) = { sType = sType; pNext = pNext; conversion = conversion }
            override x.ToString() =
                sprintf "VkSamplerYcbcrConversionInfoKHR { sType = %A; pNext = %A; conversion = %A }" x.sType x.pNext x.conversion
        end
    
    
    type VkDebugReportObjectTypeEXT with
         static member inline VkDebugReportObjectTypeSamplerYcbcrConversionKhrExt = unbox<VkDebugReportObjectTypeEXT> 1000156000
    type VkFormat with
         static member inline G8b8g8r8422UnormKhr = unbox<VkFormat> 1000156000
         static member inline B8g8r8g8422UnormKhr = unbox<VkFormat> 1000156001
         static member inline G8B8R83plane420UnormKhr = unbox<VkFormat> 1000156002
         static member inline G8B8r82plane420UnormKhr = unbox<VkFormat> 1000156003
         static member inline G8B8R83plane422UnormKhr = unbox<VkFormat> 1000156004
         static member inline G8B8r82plane422UnormKhr = unbox<VkFormat> 1000156005
         static member inline G8B8R83plane444UnormKhr = unbox<VkFormat> 1000156006
         static member inline R10x6UnormPack16Khr = unbox<VkFormat> 1000156007
         static member inline R10x6g10x6Unorm2pack16Khr = unbox<VkFormat> 1000156008
         static member inline R10x6g10x6b10x6a10x6Unorm4pack16Khr = unbox<VkFormat> 1000156009
         static member inline G10x6b10x6g10x6r10x6422Unorm4pack16Khr = unbox<VkFormat> 1000156010
         static member inline B10x6g10x6r10x6g10x6422Unorm4pack16Khr = unbox<VkFormat> 1000156011
         static member inline G10x6B10x6R10x63plane420Unorm3pack16Khr = unbox<VkFormat> 1000156012
         static member inline G10x6B10x6r10x62plane420Unorm3pack16Khr = unbox<VkFormat> 1000156013
         static member inline G10x6B10x6R10x63plane422Unorm3pack16Khr = unbox<VkFormat> 1000156014
         static member inline G10x6B10x6r10x62plane422Unorm3pack16Khr = unbox<VkFormat> 1000156015
         static member inline G10x6B10x6R10x63plane444Unorm3pack16Khr = unbox<VkFormat> 1000156016
         static member inline R12x4UnormPack16Khr = unbox<VkFormat> 1000156017
         static member inline R12x4g12x4Unorm2pack16Khr = unbox<VkFormat> 1000156018
         static member inline R12x4g12x4b12x4a12x4Unorm4pack16Khr = unbox<VkFormat> 1000156019
         static member inline G12x4b12x4g12x4r12x4422Unorm4pack16Khr = unbox<VkFormat> 1000156020
         static member inline B12x4g12x4r12x4g12x4422Unorm4pack16Khr = unbox<VkFormat> 1000156021
         static member inline G12x4B12x4R12x43plane420Unorm3pack16Khr = unbox<VkFormat> 1000156022
         static member inline G12x4B12x4r12x42plane420Unorm3pack16Khr = unbox<VkFormat> 1000156023
         static member inline G12x4B12x4R12x43plane422Unorm3pack16Khr = unbox<VkFormat> 1000156024
         static member inline G12x4B12x4r12x42plane422Unorm3pack16Khr = unbox<VkFormat> 1000156025
         static member inline G12x4B12x4R12x43plane444Unorm3pack16Khr = unbox<VkFormat> 1000156026
         static member inline G16b16g16r16422UnormKhr = unbox<VkFormat> 1000156027
         static member inline B16g16r16g16422UnormKhr = unbox<VkFormat> 1000156028
         static member inline G16B16R163plane420UnormKhr = unbox<VkFormat> 1000156029
         static member inline G16B16r162plane420UnormKhr = unbox<VkFormat> 1000156030
         static member inline G16B16R163plane422UnormKhr = unbox<VkFormat> 1000156031
         static member inline G16B16r162plane422UnormKhr = unbox<VkFormat> 1000156032
         static member inline G16B16R163plane444UnormKhr = unbox<VkFormat> 1000156033
    type VkFormatFeatureFlags with
         static member inline MidpointChromaSamplesBitKhr = unbox<VkFormatFeatureFlags> 131072
         static member inline SampledImageYcbcrConversionLinearFilterBitKhr = unbox<VkFormatFeatureFlags> 262144
         static member inline SampledImageYcbcrConversionSeparateReconstructionFilterBitKhr = unbox<VkFormatFeatureFlags> 524288
         static member inline SampledImageYcbcrConversionChromaReconstructionExplicitBitKhr = unbox<VkFormatFeatureFlags> 1048576
         static member inline SampledImageYcbcrConversionChromaReconstructionExplicitForceableBitKhr = unbox<VkFormatFeatureFlags> 2097152
         static member inline DisjointBitKhr = unbox<VkFormatFeatureFlags> 4194304
         static member inline CositedChromaSamplesBitKhr = unbox<VkFormatFeatureFlags> 8388608
    type VkImageAspectFlags with
         static member inline Plane0BitKhr = unbox<VkImageAspectFlags> 16
         static member inline Plane1BitKhr = unbox<VkImageAspectFlags> 32
         static member inline Plane2BitKhr = unbox<VkImageAspectFlags> 64
    type VkImageCreateFlags with
         static member inline DisjointBitKhr = unbox<VkImageCreateFlags> 512
    type VkObjectType with
         static member inline SamplerYcbcrConversionKhr = unbox<VkObjectType> 1000156000
    type VkStructureType with
         static member inline SamplerYcbcrConversionCreateInfoKhr = unbox<VkStructureType> 1000156000
         static member inline SamplerYcbcrConversionInfoKhr = unbox<VkStructureType> 1000156001
         static member inline BindImagePlaneMemoryInfoKhr = unbox<VkStructureType> 1000156002
         static member inline ImagePlaneMemoryRequirementsInfoKhr = unbox<VkStructureType> 1000156003
         static member inline PhysicalDeviceSamplerYcbcrConversionFeaturesKhr = unbox<VkStructureType> 1000156004
         static member inline SamplerYcbcrConversionImageFormatPropertiesKhr = unbox<VkStructureType> 1000156005
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkCreateSamplerYcbcrConversionKHRDel = delegate of VkDevice * nativeptr<VkSamplerYcbcrConversionCreateInfoKHR> * nativeptr<VkAllocationCallbacks> * nativeptr<VkSamplerYcbcrConversionKHR> -> VkResult
        [<SuppressUnmanagedCodeSecurity>]
        type VkDestroySamplerYcbcrConversionKHRDel = delegate of VkDevice * VkSamplerYcbcrConversionKHR * nativeptr<VkAllocationCallbacks> -> unit
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHR_sampler_ycbcr_conversion")
            static let s_vkCreateSamplerYcbcrConversionKHRDel = VkRaw.vkImportInstanceDelegate<VkCreateSamplerYcbcrConversionKHRDel> "vkCreateSamplerYcbcrConversionKHR"
            static let s_vkDestroySamplerYcbcrConversionKHRDel = VkRaw.vkImportInstanceDelegate<VkDestroySamplerYcbcrConversionKHRDel> "vkDestroySamplerYcbcrConversionKHR"
            static do Report.End(3) |> ignore
            static member vkCreateSamplerYcbcrConversionKHR = s_vkCreateSamplerYcbcrConversionKHRDel
            static member vkDestroySamplerYcbcrConversionKHR = s_vkDestroySamplerYcbcrConversionKHRDel
        let vkCreateSamplerYcbcrConversionKHR(device : VkDevice, pCreateInfo : nativeptr<VkSamplerYcbcrConversionCreateInfoKHR>, pAllocator : nativeptr<VkAllocationCallbacks>, pYcbcrConversion : nativeptr<VkSamplerYcbcrConversionKHR>) = Loader<unit>.vkCreateSamplerYcbcrConversionKHR.Invoke(device, pCreateInfo, pAllocator, pYcbcrConversion)
        let vkDestroySamplerYcbcrConversionKHR(device : VkDevice, ycbcrConversion : VkSamplerYcbcrConversionKHR, pAllocator : nativeptr<VkAllocationCallbacks>) = Loader<unit>.vkDestroySamplerYcbcrConversionKHR.Invoke(device, ycbcrConversion, pAllocator)

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
    
            new(sType : VkStructureType, pNext : nativeint, sharedPresentSupportedUsageFlags : VkImageUsageFlags) = { sType = sType; pNext = pNext; sharedPresentSupportedUsageFlags = sharedPresentSupportedUsageFlags }
            override x.ToString() =
                sprintf "VkSharedPresentSurfaceCapabilitiesKHR { sType = %A; pNext = %A; sharedPresentSupportedUsageFlags = %A }" x.sType x.pNext x.sharedPresentSupportedUsageFlags
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
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceVariablePointerFeaturesKHR = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public variablePointersStorageBuffer : VkBool32
            val mutable public variablePointers : VkBool32
    
            new(sType : VkStructureType, pNext : nativeint, variablePointersStorageBuffer : VkBool32, variablePointers : VkBool32) = { sType = sType; pNext = pNext; variablePointersStorageBuffer = variablePointersStorageBuffer; variablePointers = variablePointers }
            override x.ToString() =
                sprintf "VkPhysicalDeviceVariablePointerFeaturesKHR { sType = %A; pNext = %A; variablePointersStorageBuffer = %A; variablePointers = %A }" x.sType x.pNext x.variablePointersStorageBuffer x.variablePointers
        end
    
    
    type VkStructureType with
         static member inline PhysicalDeviceVariablePointerFeaturesKhr = unbox<VkStructureType> 1000120000
    

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
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkWaylandSurfaceCreateFlagsKHR, display : nativeptr<nativeint>, surface : nativeptr<nativeint>) = { sType = sType; pNext = pNext; flags = flags; display = display; surface = surface }
            override x.ToString() =
                sprintf "VkWaylandSurfaceCreateInfoKHR { sType = %A; pNext = %A; flags = %A; display = %A; surface = %A }" x.sType x.pNext x.flags x.display x.surface
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
    
            new(sType : VkStructureType, pNext : nativeint, acquireCount : uint32, pAcquireSyncs : nativeptr<VkDeviceMemory>, pAcquireKeys : nativeptr<uint64>, pAcquireTimeouts : nativeptr<uint32>, releaseCount : uint32, pReleaseSyncs : nativeptr<VkDeviceMemory>, pReleaseKeys : nativeptr<uint64>) = { sType = sType; pNext = pNext; acquireCount = acquireCount; pAcquireSyncs = pAcquireSyncs; pAcquireKeys = pAcquireKeys; pAcquireTimeouts = pAcquireTimeouts; releaseCount = releaseCount; pReleaseSyncs = pReleaseSyncs; pReleaseKeys = pReleaseKeys }
            override x.ToString() =
                sprintf "VkWin32KeyedMutexAcquireReleaseInfoKHR { sType = %A; pNext = %A; acquireCount = %A; pAcquireSyncs = %A; pAcquireKeys = %A; pAcquireTimeouts = %A; releaseCount = %A; pReleaseSyncs = %A; pReleaseKeys = %A }" x.sType x.pNext x.acquireCount x.pAcquireSyncs x.pAcquireKeys x.pAcquireTimeouts x.releaseCount x.pReleaseSyncs x.pReleaseKeys
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
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkWin32SurfaceCreateFlagsKHR, hinstance : nativeint, hwnd : nativeint) = { sType = sType; pNext = pNext; flags = flags; hinstance = hinstance; hwnd = hwnd }
            override x.ToString() =
                sprintf "VkWin32SurfaceCreateInfoKHR { sType = %A; pNext = %A; flags = %A; hinstance = %A; hwnd = %A }" x.sType x.pNext x.flags x.hinstance x.hwnd
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
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkXcbSurfaceCreateFlagsKHR, connection : nativeptr<nativeint>, window : nativeint) = { sType = sType; pNext = pNext; flags = flags; connection = connection; window = window }
            override x.ToString() =
                sprintf "VkXcbSurfaceCreateInfoKHR { sType = %A; pNext = %A; flags = %A; connection = %A; window = %A }" x.sType x.pNext x.flags x.connection x.window
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
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkXlibSurfaceCreateFlagsKHR, dpy : nativeptr<nativeint>, window : nativeint) = { sType = sType; pNext = pNext; flags = flags; dpy = dpy; window = window }
            override x.ToString() =
                sprintf "VkXlibSurfaceCreateInfoKHR { sType = %A; pNext = %A; flags = %A; dpy = %A; window = %A }" x.sType x.pNext x.flags x.dpy x.window
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

module KHXDeviceGroupCreation =
    let Name = "VK_KHX_device_group_creation"
    let Number = 71
    
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDeviceGroupDeviceCreateInfoKHX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public physicalDeviceCount : uint32
            val mutable public pPhysicalDevices : nativeptr<VkPhysicalDevice>
    
            new(sType : VkStructureType, pNext : nativeint, physicalDeviceCount : uint32, pPhysicalDevices : nativeptr<VkPhysicalDevice>) = { sType = sType; pNext = pNext; physicalDeviceCount = physicalDeviceCount; pPhysicalDevices = pPhysicalDevices }
            override x.ToString() =
                sprintf "VkDeviceGroupDeviceCreateInfoKHX { sType = %A; pNext = %A; physicalDeviceCount = %A; pPhysicalDevices = %A }" x.sType x.pNext x.physicalDeviceCount x.pPhysicalDevices
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceGroupPropertiesKHX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public physicalDeviceCount : uint32
            val mutable public physicalDevices : VkPhysicalDevice_32
            val mutable public subsetAllocation : VkBool32
    
            new(sType : VkStructureType, pNext : nativeint, physicalDeviceCount : uint32, physicalDevices : VkPhysicalDevice_32, subsetAllocation : VkBool32) = { sType = sType; pNext = pNext; physicalDeviceCount = physicalDeviceCount; physicalDevices = physicalDevices; subsetAllocation = subsetAllocation }
            override x.ToString() =
                sprintf "VkPhysicalDeviceGroupPropertiesKHX { sType = %A; pNext = %A; physicalDeviceCount = %A; physicalDevices = %A; subsetAllocation = %A }" x.sType x.pNext x.physicalDeviceCount x.physicalDevices x.subsetAllocation
        end
    
    
    type VkMemoryHeapFlags with
         static member inline MultiInstanceBitKhx = unbox<VkMemoryHeapFlags> 2
    type VkStructureType with
         static member inline PhysicalDeviceGroupPropertiesKhx = unbox<VkStructureType> 1000070000
         static member inline DeviceGroupDeviceCreateInfoKhx = unbox<VkStructureType> 1000070001
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkEnumeratePhysicalDeviceGroupsKHXDel = delegate of VkInstance * nativeptr<uint32> * nativeptr<VkPhysicalDeviceGroupPropertiesKHX> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHX_device_group_creation")
            static let s_vkEnumeratePhysicalDeviceGroupsKHXDel = VkRaw.vkImportInstanceDelegate<VkEnumeratePhysicalDeviceGroupsKHXDel> "vkEnumeratePhysicalDeviceGroupsKHX"
            static do Report.End(3) |> ignore
            static member vkEnumeratePhysicalDeviceGroupsKHX = s_vkEnumeratePhysicalDeviceGroupsKHXDel
        let vkEnumeratePhysicalDeviceGroupsKHX(instance : VkInstance, pPhysicalDeviceGroupCount : nativeptr<uint32>, pPhysicalDeviceGroupProperties : nativeptr<VkPhysicalDeviceGroupPropertiesKHX>) = Loader<unit>.vkEnumeratePhysicalDeviceGroupsKHX.Invoke(instance, pPhysicalDeviceGroupCount, pPhysicalDeviceGroupProperties)

module KHXDeviceGroup =
    let Name = "VK_KHX_device_group"
    let Number = 61
    
    let Required = [ KHXDeviceGroupCreation.Name ]
    open KHXDeviceGroupCreation
    open EXTDebugReport
    
    [<Flags>]
    type VkPeerMemoryFeatureFlagBitsKHX = 
        | None = 0
        | VkPeerMemoryFeatureCopySrcBitKhx = 0x00000001
        | VkPeerMemoryFeatureCopyDstBitKhx = 0x00000002
        | VkPeerMemoryFeatureGenericSrcBitKhx = 0x00000004
        | VkPeerMemoryFeatureGenericDstBitKhx = 0x00000008
    
    [<Flags>]
    type VkMemoryAllocateFlagBitsKHX = 
        | None = 0
        | VkMemoryAllocateDeviceMaskBitKhx = 0x00000001
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkBindImageMemoryInfoKHX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public image : VkImage
            val mutable public memory : VkDeviceMemory
            val mutable public memoryOffset : VkDeviceSize
            val mutable public deviceIndexCount : uint32
            val mutable public pDeviceIndices : nativeptr<uint32>
            val mutable public _SFRRectCount : uint32
            val mutable public pSFRRects : nativeptr<VkRect2D>
    
            new(sType : VkStructureType, pNext : nativeint, image : VkImage, memory : VkDeviceMemory, memoryOffset : VkDeviceSize, deviceIndexCount : uint32, pDeviceIndices : nativeptr<uint32>, _SFRRectCount : uint32, pSFRRects : nativeptr<VkRect2D>) = { sType = sType; pNext = pNext; image = image; memory = memory; memoryOffset = memoryOffset; deviceIndexCount = deviceIndexCount; pDeviceIndices = pDeviceIndices; _SFRRectCount = _SFRRectCount; pSFRRects = pSFRRects }
            override x.ToString() =
                sprintf "VkBindImageMemoryInfoKHX { sType = %A; pNext = %A; image = %A; memory = %A; memoryOffset = %A; deviceIndexCount = %A; pDeviceIndices = %A; _SFRRectCount = %A; pSFRRects = %A }" x.sType x.pNext x.image x.memory x.memoryOffset x.deviceIndexCount x.pDeviceIndices x._SFRRectCount x.pSFRRects
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDeviceGroupBindSparseInfoKHX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public resourceDeviceIndex : uint32
            val mutable public memoryDeviceIndex : uint32
    
            new(sType : VkStructureType, pNext : nativeint, resourceDeviceIndex : uint32, memoryDeviceIndex : uint32) = { sType = sType; pNext = pNext; resourceDeviceIndex = resourceDeviceIndex; memoryDeviceIndex = memoryDeviceIndex }
            override x.ToString() =
                sprintf "VkDeviceGroupBindSparseInfoKHX { sType = %A; pNext = %A; resourceDeviceIndex = %A; memoryDeviceIndex = %A }" x.sType x.pNext x.resourceDeviceIndex x.memoryDeviceIndex
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDeviceGroupCommandBufferBeginInfoKHX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public deviceMask : uint32
    
            new(sType : VkStructureType, pNext : nativeint, deviceMask : uint32) = { sType = sType; pNext = pNext; deviceMask = deviceMask }
            override x.ToString() =
                sprintf "VkDeviceGroupCommandBufferBeginInfoKHX { sType = %A; pNext = %A; deviceMask = %A }" x.sType x.pNext x.deviceMask
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDeviceGroupRenderPassBeginInfoKHX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public deviceMask : uint32
            val mutable public deviceRenderAreaCount : uint32
            val mutable public pDeviceRenderAreas : nativeptr<VkRect2D>
    
            new(sType : VkStructureType, pNext : nativeint, deviceMask : uint32, deviceRenderAreaCount : uint32, pDeviceRenderAreas : nativeptr<VkRect2D>) = { sType = sType; pNext = pNext; deviceMask = deviceMask; deviceRenderAreaCount = deviceRenderAreaCount; pDeviceRenderAreas = pDeviceRenderAreas }
            override x.ToString() =
                sprintf "VkDeviceGroupRenderPassBeginInfoKHX { sType = %A; pNext = %A; deviceMask = %A; deviceRenderAreaCount = %A; pDeviceRenderAreas = %A }" x.sType x.pNext x.deviceMask x.deviceRenderAreaCount x.pDeviceRenderAreas
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDeviceGroupSubmitInfoKHX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public waitSemaphoreCount : uint32
            val mutable public pWaitSemaphoreDeviceIndices : nativeptr<uint32>
            val mutable public commandBufferCount : uint32
            val mutable public pCommandBufferDeviceMasks : nativeptr<uint32>
            val mutable public signalSemaphoreCount : uint32
            val mutable public pSignalSemaphoreDeviceIndices : nativeptr<uint32>
    
            new(sType : VkStructureType, pNext : nativeint, waitSemaphoreCount : uint32, pWaitSemaphoreDeviceIndices : nativeptr<uint32>, commandBufferCount : uint32, pCommandBufferDeviceMasks : nativeptr<uint32>, signalSemaphoreCount : uint32, pSignalSemaphoreDeviceIndices : nativeptr<uint32>) = { sType = sType; pNext = pNext; waitSemaphoreCount = waitSemaphoreCount; pWaitSemaphoreDeviceIndices = pWaitSemaphoreDeviceIndices; commandBufferCount = commandBufferCount; pCommandBufferDeviceMasks = pCommandBufferDeviceMasks; signalSemaphoreCount = signalSemaphoreCount; pSignalSemaphoreDeviceIndices = pSignalSemaphoreDeviceIndices }
            override x.ToString() =
                sprintf "VkDeviceGroupSubmitInfoKHX { sType = %A; pNext = %A; waitSemaphoreCount = %A; pWaitSemaphoreDeviceIndices = %A; commandBufferCount = %A; pCommandBufferDeviceMasks = %A; signalSemaphoreCount = %A; pSignalSemaphoreDeviceIndices = %A }" x.sType x.pNext x.waitSemaphoreCount x.pWaitSemaphoreDeviceIndices x.commandBufferCount x.pCommandBufferDeviceMasks x.signalSemaphoreCount x.pSignalSemaphoreDeviceIndices
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkMemoryAllocateFlagsInfoKHX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkMemoryAllocateFlagsKHX
            val mutable public deviceMask : uint32
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkMemoryAllocateFlagsKHX, deviceMask : uint32) = { sType = sType; pNext = pNext; flags = flags; deviceMask = deviceMask }
            override x.ToString() =
                sprintf "VkMemoryAllocateFlagsInfoKHX { sType = %A; pNext = %A; flags = %A; deviceMask = %A }" x.sType x.pNext x.flags x.deviceMask
        end
    
    
    type VkDependencyFlags with
         static member inline DeviceGroupBitKhx = unbox<VkDependencyFlags> 4
    type VkPipelineCreateFlags with
         static member inline ViewIndexFromDeviceIndexBitKhx = unbox<VkPipelineCreateFlags> 8
         static member inline DispatchBaseKhx = unbox<VkPipelineCreateFlags> 16
    type VkStructureType with
         static member inline MemoryAllocateFlagsInfoKhx = unbox<VkStructureType> 1000060000
         static member inline BindBufferMemoryInfoKhx = unbox<VkStructureType> 1000060001
         static member inline BindImageMemoryInfoKhx = unbox<VkStructureType> 1000060002
         static member inline DeviceGroupRenderPassBeginInfoKhx = unbox<VkStructureType> 1000060003
         static member inline DeviceGroupCommandBufferBeginInfoKhx = unbox<VkStructureType> 1000060004
         static member inline DeviceGroupSubmitInfoKhx = unbox<VkStructureType> 1000060005
         static member inline DeviceGroupBindSparseInfoKhx = unbox<VkStructureType> 1000060006
         static member inline AcquireNextImageInfoKhx = unbox<VkStructureType> 1000060010
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetDeviceGroupPeerMemoryFeaturesKHXDel = delegate of VkDevice * uint32 * uint32 * uint32 * nativeptr<VkPeerMemoryFeatureFlagsKHX> -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdSetDeviceMaskKHXDel = delegate of VkCommandBuffer * uint32 -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkCmdDispatchBaseKHXDel = delegate of VkCommandBuffer * uint32 * uint32 * uint32 * uint32 * uint32 * uint32 -> unit
        [<SuppressUnmanagedCodeSecurity>]
        type VkBindImageMemory2KHXDel = delegate of VkDevice * uint32 * nativeptr<VkBindImageMemoryInfoKHX> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_KHX_device_group")
            static let s_vkGetDeviceGroupPeerMemoryFeaturesKHXDel = VkRaw.vkImportInstanceDelegate<VkGetDeviceGroupPeerMemoryFeaturesKHXDel> "vkGetDeviceGroupPeerMemoryFeaturesKHX"
            static let s_vkCmdSetDeviceMaskKHXDel = VkRaw.vkImportInstanceDelegate<VkCmdSetDeviceMaskKHXDel> "vkCmdSetDeviceMaskKHX"
            static let s_vkCmdDispatchBaseKHXDel = VkRaw.vkImportInstanceDelegate<VkCmdDispatchBaseKHXDel> "vkCmdDispatchBaseKHX"
            static let s_vkBindImageMemory2KHXDel = VkRaw.vkImportInstanceDelegate<VkBindImageMemory2KHXDel> "vkBindImageMemory2KHX"
            static do Report.End(3) |> ignore
            static member vkGetDeviceGroupPeerMemoryFeaturesKHX = s_vkGetDeviceGroupPeerMemoryFeaturesKHXDel
            static member vkCmdSetDeviceMaskKHX = s_vkCmdSetDeviceMaskKHXDel
            static member vkCmdDispatchBaseKHX = s_vkCmdDispatchBaseKHXDel
            static member vkBindImageMemory2KHX = s_vkBindImageMemory2KHXDel
        let vkGetDeviceGroupPeerMemoryFeaturesKHX(device : VkDevice, heapIndex : uint32, localDeviceIndex : uint32, remoteDeviceIndex : uint32, pPeerMemoryFeatures : nativeptr<VkPeerMemoryFeatureFlagsKHX>) = Loader<unit>.vkGetDeviceGroupPeerMemoryFeaturesKHX.Invoke(device, heapIndex, localDeviceIndex, remoteDeviceIndex, pPeerMemoryFeatures)
        let vkCmdSetDeviceMaskKHX(commandBuffer : VkCommandBuffer, deviceMask : uint32) = Loader<unit>.vkCmdSetDeviceMaskKHX.Invoke(commandBuffer, deviceMask)
        let vkCmdDispatchBaseKHX(commandBuffer : VkCommandBuffer, baseGroupX : uint32, baseGroupY : uint32, baseGroupZ : uint32, groupCountX : uint32, groupCountY : uint32, groupCountZ : uint32) = Loader<unit>.vkCmdDispatchBaseKHX.Invoke(commandBuffer, baseGroupX, baseGroupY, baseGroupZ, groupCountX, groupCountY, groupCountZ)
        let vkBindImageMemory2KHX(device : VkDevice, bindInfoCount : uint32, pBindInfos : nativeptr<VkBindImageMemoryInfoKHX>) = Loader<unit>.vkBindImageMemory2KHX.Invoke(device, bindInfoCount, pBindInfos)
    
    module KHRBindMemory2 =
        open EXTDebugReport
        
        
        [<StructLayout(LayoutKind.Sequential)>]
        type VkBindBufferMemoryDeviceGroupInfoKHX = 
            struct
                val mutable public sType : VkStructureType
                val mutable public pNext : nativeint
                val mutable public deviceIndexCount : uint32
                val mutable public pDeviceIndices : nativeptr<uint32>
        
                new(sType : VkStructureType, pNext : nativeint, deviceIndexCount : uint32, pDeviceIndices : nativeptr<uint32>) = { sType = sType; pNext = pNext; deviceIndexCount = deviceIndexCount; pDeviceIndices = pDeviceIndices }
                override x.ToString() =
                    sprintf "VkBindBufferMemoryDeviceGroupInfoKHX { sType = %A; pNext = %A; deviceIndexCount = %A; pDeviceIndices = %A }" x.sType x.pNext x.deviceIndexCount x.pDeviceIndices
            end
        
        [<StructLayout(LayoutKind.Sequential)>]
        type VkBindImageMemoryDeviceGroupInfoKHX = 
            struct
                val mutable public sType : VkStructureType
                val mutable public pNext : nativeint
                val mutable public deviceIndexCount : uint32
                val mutable public pDeviceIndices : nativeptr<uint32>
                val mutable public _SFRRectCount : uint32
                val mutable public pSFRRects : nativeptr<VkRect2D>
        
                new(sType : VkStructureType, pNext : nativeint, deviceIndexCount : uint32, pDeviceIndices : nativeptr<uint32>, _SFRRectCount : uint32, pSFRRects : nativeptr<VkRect2D>) = { sType = sType; pNext = pNext; deviceIndexCount = deviceIndexCount; pDeviceIndices = pDeviceIndices; _SFRRectCount = _SFRRectCount; pSFRRects = pSFRRects }
                override x.ToString() =
                    sprintf "VkBindImageMemoryDeviceGroupInfoKHX { sType = %A; pNext = %A; deviceIndexCount = %A; pDeviceIndices = %A; _SFRRectCount = %A; pSFRRects = %A }" x.sType x.pNext x.deviceIndexCount x.pDeviceIndices x._SFRRectCount x.pSFRRects
            end
        
        
        type VkImageCreateFlags with
             static member inline BindSfrBitKhx = unbox<VkImageCreateFlags> 64
        type VkStructureType with
             static member inline BindBufferMemoryDeviceGroupInfoKhx = unbox<VkStructureType> 999998013
             static member inline BindImageMemoryDeviceGroupInfoKhx = unbox<VkStructureType> 999998014
        
    
    module KHRSurface =
        open EXTDebugReport
        
        [<Flags>]
        type VkDeviceGroupPresentModeFlagBitsKHX = 
            | None = 0
            | VkDeviceGroupPresentModeLocalBitKhx = 0x00000001
            | VkDeviceGroupPresentModeRemoteBitKhx = 0x00000002
            | VkDeviceGroupPresentModeSumBitKhx = 0x00000004
            | VkDeviceGroupPresentModeLocalMultiDeviceBitKhx = 0x00000008
        
        
        [<StructLayout(LayoutKind.Sequential)>]
        type VkDeviceGroupPresentCapabilitiesKHX = 
            struct
                val mutable public sType : VkStructureType
                val mutable public pNext : nativeint
                val mutable public presentMask : uint32_32
                val mutable public modes : VkDeviceGroupPresentModeFlagsKHX
        
                new(sType : VkStructureType, pNext : nativeint, presentMask : uint32_32, modes : VkDeviceGroupPresentModeFlagsKHX) = { sType = sType; pNext = pNext; presentMask = presentMask; modes = modes }
                override x.ToString() =
                    sprintf "VkDeviceGroupPresentCapabilitiesKHX { sType = %A; pNext = %A; presentMask = %A; modes = %A }" x.sType x.pNext x.presentMask x.modes
            end
        
        
        type VkStructureType with
             static member inline DeviceGroupPresentCapabilitiesKhx = unbox<VkStructureType> 999998007
        
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module VkRaw =
            [<SuppressUnmanagedCodeSecurity>]
            type VkGetDeviceGroupPresentCapabilitiesKHXDel = delegate of VkDevice * nativeptr<VkDeviceGroupPresentCapabilitiesKHX> -> VkResult
            [<SuppressUnmanagedCodeSecurity>]
            type VkGetDeviceGroupSurfacePresentModesKHXDel = delegate of VkDevice * VkSurfaceKHR * nativeptr<VkDeviceGroupPresentModeFlagsKHX> -> VkResult
            [<SuppressUnmanagedCodeSecurity>]
            type VkGetPhysicalDevicePresentRectanglesKHXDel = delegate of VkPhysicalDevice * VkSurfaceKHR * nativeptr<uint32> * nativeptr<VkRect2D> -> VkResult
            
            [<AbstractClass; Sealed>]
            type private Loader<'d> private() =
                static do Report.Begin(3, "[Vulkan] loading VK_KHR_surface")
                static let s_vkGetDeviceGroupPresentCapabilitiesKHXDel = VkRaw.vkImportInstanceDelegate<VkGetDeviceGroupPresentCapabilitiesKHXDel> "vkGetDeviceGroupPresentCapabilitiesKHX"
                static let s_vkGetDeviceGroupSurfacePresentModesKHXDel = VkRaw.vkImportInstanceDelegate<VkGetDeviceGroupSurfacePresentModesKHXDel> "vkGetDeviceGroupSurfacePresentModesKHX"
                static let s_vkGetPhysicalDevicePresentRectanglesKHXDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDevicePresentRectanglesKHXDel> "vkGetPhysicalDevicePresentRectanglesKHX"
                static do Report.End(3) |> ignore
                static member vkGetDeviceGroupPresentCapabilitiesKHX = s_vkGetDeviceGroupPresentCapabilitiesKHXDel
                static member vkGetDeviceGroupSurfacePresentModesKHX = s_vkGetDeviceGroupSurfacePresentModesKHXDel
                static member vkGetPhysicalDevicePresentRectanglesKHX = s_vkGetPhysicalDevicePresentRectanglesKHXDel
            let vkGetDeviceGroupPresentCapabilitiesKHX(device : VkDevice, pDeviceGroupPresentCapabilities : nativeptr<VkDeviceGroupPresentCapabilitiesKHX>) = Loader<unit>.vkGetDeviceGroupPresentCapabilitiesKHX.Invoke(device, pDeviceGroupPresentCapabilities)
            let vkGetDeviceGroupSurfacePresentModesKHX(device : VkDevice, surface : VkSurfaceKHR, pModes : nativeptr<VkDeviceGroupPresentModeFlagsKHX>) = Loader<unit>.vkGetDeviceGroupSurfacePresentModesKHX.Invoke(device, surface, pModes)
            let vkGetPhysicalDevicePresentRectanglesKHX(physicalDevice : VkPhysicalDevice, surface : VkSurfaceKHR, pRectCount : nativeptr<uint32>, pRects : nativeptr<VkRect2D>) = Loader<unit>.vkGetPhysicalDevicePresentRectanglesKHX.Invoke(physicalDevice, surface, pRectCount, pRects)
    
    module KHRSwapchain =
        open KHRSurface
        open EXTDebugReport
        
        
        [<StructLayout(LayoutKind.Sequential)>]
        type VkAcquireNextImageInfoKHX = 
            struct
                val mutable public sType : VkStructureType
                val mutable public pNext : nativeint
                val mutable public swapchain : VkSwapchainKHR
                val mutable public timeout : uint64
                val mutable public semaphore : VkSemaphore
                val mutable public fence : VkFence
                val mutable public deviceMask : uint32
        
                new(sType : VkStructureType, pNext : nativeint, swapchain : VkSwapchainKHR, timeout : uint64, semaphore : VkSemaphore, fence : VkFence, deviceMask : uint32) = { sType = sType; pNext = pNext; swapchain = swapchain; timeout = timeout; semaphore = semaphore; fence = fence; deviceMask = deviceMask }
                override x.ToString() =
                    sprintf "VkAcquireNextImageInfoKHX { sType = %A; pNext = %A; swapchain = %A; timeout = %A; semaphore = %A; fence = %A; deviceMask = %A }" x.sType x.pNext x.swapchain x.timeout x.semaphore x.fence x.deviceMask
            end
        
        [<StructLayout(LayoutKind.Sequential)>]
        type VkBindImageMemorySwapchainInfoKHX = 
            struct
                val mutable public sType : VkStructureType
                val mutable public pNext : nativeint
                val mutable public swapchain : VkSwapchainKHR
                val mutable public imageIndex : uint32
        
                new(sType : VkStructureType, pNext : nativeint, swapchain : VkSwapchainKHR, imageIndex : uint32) = { sType = sType; pNext = pNext; swapchain = swapchain; imageIndex = imageIndex }
                override x.ToString() =
                    sprintf "VkBindImageMemorySwapchainInfoKHX { sType = %A; pNext = %A; swapchain = %A; imageIndex = %A }" x.sType x.pNext x.swapchain x.imageIndex
            end
        
        [<StructLayout(LayoutKind.Sequential)>]
        type VkDeviceGroupPresentInfoKHX = 
            struct
                val mutable public sType : VkStructureType
                val mutable public pNext : nativeint
                val mutable public swapchainCount : uint32
                val mutable public pDeviceMasks : nativeptr<uint32>
                val mutable public mode : VkDeviceGroupPresentModeFlagBitsKHX
        
                new(sType : VkStructureType, pNext : nativeint, swapchainCount : uint32, pDeviceMasks : nativeptr<uint32>, mode : VkDeviceGroupPresentModeFlagBitsKHX) = { sType = sType; pNext = pNext; swapchainCount = swapchainCount; pDeviceMasks = pDeviceMasks; mode = mode }
                override x.ToString() =
                    sprintf "VkDeviceGroupPresentInfoKHX { sType = %A; pNext = %A; swapchainCount = %A; pDeviceMasks = %A; mode = %A }" x.sType x.pNext x.swapchainCount x.pDeviceMasks x.mode
            end
        
        [<StructLayout(LayoutKind.Sequential)>]
        type VkDeviceGroupSwapchainCreateInfoKHX = 
            struct
                val mutable public sType : VkStructureType
                val mutable public pNext : nativeint
                val mutable public modes : VkDeviceGroupPresentModeFlagsKHX
        
                new(sType : VkStructureType, pNext : nativeint, modes : VkDeviceGroupPresentModeFlagsKHX) = { sType = sType; pNext = pNext; modes = modes }
                override x.ToString() =
                    sprintf "VkDeviceGroupSwapchainCreateInfoKHX { sType = %A; pNext = %A; modes = %A }" x.sType x.pNext x.modes
            end
        
        [<StructLayout(LayoutKind.Sequential)>]
        type VkImageSwapchainCreateInfoKHX = 
            struct
                val mutable public sType : VkStructureType
                val mutable public pNext : nativeint
                val mutable public swapchain : VkSwapchainKHR
        
                new(sType : VkStructureType, pNext : nativeint, swapchain : VkSwapchainKHR) = { sType = sType; pNext = pNext; swapchain = swapchain }
                override x.ToString() =
                    sprintf "VkImageSwapchainCreateInfoKHX { sType = %A; pNext = %A; swapchain = %A }" x.sType x.pNext x.swapchain
            end
        
        
        type VkStructureType with
             static member inline ImageSwapchainCreateInfoKhx = unbox<VkStructureType> 999998008
             static member inline BindImageMemorySwapchainInfoKhx = unbox<VkStructureType> 999998009
             static member inline DeviceGroupPresentInfoKhx = unbox<VkStructureType> 999998011
             static member inline DeviceGroupSwapchainCreateInfoKhx = unbox<VkStructureType> 999998012
        type VkSwapchainCreateFlagBitsKHR with
             static member inline VkSwapchainCreateBindSfrBitKhx = unbox<VkSwapchainCreateFlagBitsKHR> 1
        
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module VkRaw =
            [<SuppressUnmanagedCodeSecurity>]
            type VkAcquireNextImage2KHXDel = delegate of VkDevice * nativeptr<VkAcquireNextImageInfoKHX> * nativeptr<uint32> -> VkResult
            
            [<AbstractClass; Sealed>]
            type private Loader<'d> private() =
                static do Report.Begin(3, "[Vulkan] loading VK_KHR_swapchain")
                static let s_vkAcquireNextImage2KHXDel = VkRaw.vkImportInstanceDelegate<VkAcquireNextImage2KHXDel> "vkAcquireNextImage2KHX"
                static do Report.End(3) |> ignore
                static member vkAcquireNextImage2KHX = s_vkAcquireNextImage2KHXDel
            let vkAcquireNextImage2KHX(device : VkDevice, pAcquireInfo : nativeptr<VkAcquireNextImageInfoKHX>, pImageIndex : nativeptr<uint32>) = Loader<unit>.vkAcquireNextImage2KHX.Invoke(device, pAcquireInfo, pImageIndex)

module KHXMultiview =
    let Name = "VK_KHX_multiview"
    let Number = 54
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name ]
    open KHRGetPhysicalDeviceProperties2
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceMultiviewFeaturesKHX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public multiview : VkBool32
            val mutable public multiviewGeometryShader : VkBool32
            val mutable public multiviewTessellationShader : VkBool32
    
            new(sType : VkStructureType, pNext : nativeint, multiview : VkBool32, multiviewGeometryShader : VkBool32, multiviewTessellationShader : VkBool32) = { sType = sType; pNext = pNext; multiview = multiview; multiviewGeometryShader = multiviewGeometryShader; multiviewTessellationShader = multiviewTessellationShader }
            override x.ToString() =
                sprintf "VkPhysicalDeviceMultiviewFeaturesKHX { sType = %A; pNext = %A; multiview = %A; multiviewGeometryShader = %A; multiviewTessellationShader = %A }" x.sType x.pNext x.multiview x.multiviewGeometryShader x.multiviewTessellationShader
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceMultiviewPropertiesKHX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public maxMultiviewViewCount : uint32
            val mutable public maxMultiviewInstanceIndex : uint32
    
            new(sType : VkStructureType, pNext : nativeint, maxMultiviewViewCount : uint32, maxMultiviewInstanceIndex : uint32) = { sType = sType; pNext = pNext; maxMultiviewViewCount = maxMultiviewViewCount; maxMultiviewInstanceIndex = maxMultiviewInstanceIndex }
            override x.ToString() =
                sprintf "VkPhysicalDeviceMultiviewPropertiesKHX { sType = %A; pNext = %A; maxMultiviewViewCount = %A; maxMultiviewInstanceIndex = %A }" x.sType x.pNext x.maxMultiviewViewCount x.maxMultiviewInstanceIndex
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkRenderPassMultiviewCreateInfoKHX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public subpassCount : uint32
            val mutable public pViewMasks : nativeptr<uint32>
            val mutable public dependencyCount : uint32
            val mutable public pViewOffsets : nativeptr<int>
            val mutable public correlationMaskCount : uint32
            val mutable public pCorrelationMasks : nativeptr<uint32>
    
            new(sType : VkStructureType, pNext : nativeint, subpassCount : uint32, pViewMasks : nativeptr<uint32>, dependencyCount : uint32, pViewOffsets : nativeptr<int>, correlationMaskCount : uint32, pCorrelationMasks : nativeptr<uint32>) = { sType = sType; pNext = pNext; subpassCount = subpassCount; pViewMasks = pViewMasks; dependencyCount = dependencyCount; pViewOffsets = pViewOffsets; correlationMaskCount = correlationMaskCount; pCorrelationMasks = pCorrelationMasks }
            override x.ToString() =
                sprintf "VkRenderPassMultiviewCreateInfoKHX { sType = %A; pNext = %A; subpassCount = %A; pViewMasks = %A; dependencyCount = %A; pViewOffsets = %A; correlationMaskCount = %A; pCorrelationMasks = %A }" x.sType x.pNext x.subpassCount x.pViewMasks x.dependencyCount x.pViewOffsets x.correlationMaskCount x.pCorrelationMasks
        end
    
    
    type VkDependencyFlags with
         static member inline ViewLocalBitKhx = unbox<VkDependencyFlags> 2
    type VkStructureType with
         static member inline RenderPassMultiviewCreateInfoKhx = unbox<VkStructureType> 1000053000
         static member inline PhysicalDeviceMultiviewFeaturesKhx = unbox<VkStructureType> 1000053001
         static member inline PhysicalDeviceMultiviewPropertiesKhx = unbox<VkStructureType> 1000053002
    

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
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkIOSSurfaceCreateFlagsMVK, pView : nativeint) = { sType = sType; pNext = pNext; flags = flags; pView = pView }
            override x.ToString() =
                sprintf "VkIOSSurfaceCreateInfoMVK { sType = %A; pNext = %A; flags = %A; pView = %A }" x.sType x.pNext x.flags x.pView
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
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkMacOSSurfaceCreateFlagsMVK, pView : nativeint) = { sType = sType; pNext = pNext; flags = flags; pView = pView }
            override x.ToString() =
                sprintf "VkMacOSSurfaceCreateInfoMVK { sType = %A; pNext = %A; flags = %A; pView = %A }" x.sType x.pNext x.flags x.pView
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
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkViSurfaceCreateFlagsNN, window : nativeint) = { sType = sType; pNext = pNext; flags = flags; window = window }
            override x.ToString() =
                sprintf "VkViSurfaceCreateInfoNN { sType = %A; pNext = %A; flags = %A; window = %A }" x.sType x.pNext x.flags x.window
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
    
            new(xcoeff : float32, ycoeff : float32) = { xcoeff = xcoeff; ycoeff = ycoeff }
            override x.ToString() =
                sprintf "VkViewportWScalingNV { xcoeff = %A; ycoeff = %A }" x.xcoeff x.ycoeff
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPipelineViewportWScalingStateCreateInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public viewportWScalingEnable : VkBool32
            val mutable public viewportCount : uint32
            val mutable public pViewportWScalings : nativeptr<VkViewportWScalingNV>
    
            new(sType : VkStructureType, pNext : nativeint, viewportWScalingEnable : VkBool32, viewportCount : uint32, pViewportWScalings : nativeptr<VkViewportWScalingNV>) = { sType = sType; pNext = pNext; viewportWScalingEnable = viewportWScalingEnable; viewportCount = viewportCount; pViewportWScalings = pViewportWScalings }
            override x.ToString() =
                sprintf "VkPipelineViewportWScalingStateCreateInfoNV { sType = %A; pNext = %A; viewportWScalingEnable = %A; viewportCount = %A; pViewportWScalings = %A }" x.sType x.pNext x.viewportWScalingEnable x.viewportCount x.pViewportWScalings
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
    
            new(sType : VkStructureType, pNext : nativeint, dedicatedAllocation : VkBool32) = { sType = sType; pNext = pNext; dedicatedAllocation = dedicatedAllocation }
            override x.ToString() =
                sprintf "VkDedicatedAllocationBufferCreateInfoNV { sType = %A; pNext = %A; dedicatedAllocation = %A }" x.sType x.pNext x.dedicatedAllocation
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDedicatedAllocationImageCreateInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public dedicatedAllocation : VkBool32
    
            new(sType : VkStructureType, pNext : nativeint, dedicatedAllocation : VkBool32) = { sType = sType; pNext = pNext; dedicatedAllocation = dedicatedAllocation }
            override x.ToString() =
                sprintf "VkDedicatedAllocationImageCreateInfoNV { sType = %A; pNext = %A; dedicatedAllocation = %A }" x.sType x.pNext x.dedicatedAllocation
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDedicatedAllocationMemoryAllocateInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public image : VkImage
            val mutable public buffer : VkBuffer
    
            new(sType : VkStructureType, pNext : nativeint, image : VkImage, buffer : VkBuffer) = { sType = sType; pNext = pNext; image = image; buffer = buffer }
            override x.ToString() =
                sprintf "VkDedicatedAllocationMemoryAllocateInfoNV { sType = %A; pNext = %A; image = %A; buffer = %A }" x.sType x.pNext x.image x.buffer
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
    type VkExternalMemoryHandleTypeFlagBitsNV = 
        | None = 0
        | VkExternalMemoryHandleTypeOpaqueWin32BitNv = 0x00000001
        | VkExternalMemoryHandleTypeOpaqueWin32KmtBitNv = 0x00000002
        | VkExternalMemoryHandleTypeD3d11ImageBitNv = 0x00000004
        | VkExternalMemoryHandleTypeD3d11ImageKmtBitNv = 0x00000008
    
    [<Flags>]
    type VkExternalMemoryFeatureFlagBitsNV = 
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
    
            new(imageFormatProperties : VkImageFormatProperties, externalMemoryFeatures : VkExternalMemoryFeatureFlagsNV, exportFromImportedHandleTypes : VkExternalMemoryHandleTypeFlagsNV, compatibleHandleTypes : VkExternalMemoryHandleTypeFlagsNV) = { imageFormatProperties = imageFormatProperties; externalMemoryFeatures = externalMemoryFeatures; exportFromImportedHandleTypes = exportFromImportedHandleTypes; compatibleHandleTypes = compatibleHandleTypes }
            override x.ToString() =
                sprintf "VkExternalImageFormatPropertiesNV { imageFormatProperties = %A; externalMemoryFeatures = %A; exportFromImportedHandleTypes = %A; compatibleHandleTypes = %A }" x.imageFormatProperties x.externalMemoryFeatures x.exportFromImportedHandleTypes x.compatibleHandleTypes
        end
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VkRaw =
        [<SuppressUnmanagedCodeSecurity>]
        type VkGetPhysicalDeviceExternalImageFormatPropertiesNVDel = delegate of VkPhysicalDevice * VkFormat * VkImageType * VkImageTiling * VkImageUsageFlags * VkImageCreateFlags * VkExternalMemoryHandleTypeFlagsNV * nativeptr<VkExternalImageFormatPropertiesNV> -> VkResult
        
        [<AbstractClass; Sealed>]
        type private Loader<'d> private() =
            static do Report.Begin(3, "[Vulkan] loading VK_NV_external_memory_capabilities")
            static let s_vkGetPhysicalDeviceExternalImageFormatPropertiesNVDel = VkRaw.vkImportInstanceDelegate<VkGetPhysicalDeviceExternalImageFormatPropertiesNVDel> "vkGetPhysicalDeviceExternalImageFormatPropertiesNV"
            static do Report.End(3) |> ignore
            static member vkGetPhysicalDeviceExternalImageFormatPropertiesNV = s_vkGetPhysicalDeviceExternalImageFormatPropertiesNVDel
        let vkGetPhysicalDeviceExternalImageFormatPropertiesNV(physicalDevice : VkPhysicalDevice, format : VkFormat, _type : VkImageType, tiling : VkImageTiling, usage : VkImageUsageFlags, flags : VkImageCreateFlags, externalHandleType : VkExternalMemoryHandleTypeFlagsNV, pExternalImageFormatProperties : nativeptr<VkExternalImageFormatPropertiesNV>) = Loader<unit>.vkGetPhysicalDeviceExternalImageFormatPropertiesNV.Invoke(physicalDevice, format, _type, tiling, usage, flags, externalHandleType, pExternalImageFormatProperties)

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
    
            new(sType : VkStructureType, pNext : nativeint, handleTypes : VkExternalMemoryHandleTypeFlagsNV) = { sType = sType; pNext = pNext; handleTypes = handleTypes }
            override x.ToString() =
                sprintf "VkExportMemoryAllocateInfoNV { sType = %A; pNext = %A; handleTypes = %A }" x.sType x.pNext x.handleTypes
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkExternalMemoryImageCreateInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleTypes : VkExternalMemoryHandleTypeFlagsNV
    
            new(sType : VkStructureType, pNext : nativeint, handleTypes : VkExternalMemoryHandleTypeFlagsNV) = { sType = sType; pNext = pNext; handleTypes = handleTypes }
            override x.ToString() =
                sprintf "VkExternalMemoryImageCreateInfoNV { sType = %A; pNext = %A; handleTypes = %A }" x.sType x.pNext x.handleTypes
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
    
            new(sType : VkStructureType, pNext : nativeint, pAttributes : nativeptr<nativeint>, dwAccess : uint32) = { sType = sType; pNext = pNext; pAttributes = pAttributes; dwAccess = dwAccess }
            override x.ToString() =
                sprintf "VkExportMemoryWin32HandleInfoNV { sType = %A; pNext = %A; pAttributes = %A; dwAccess = %A }" x.sType x.pNext x.pAttributes x.dwAccess
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkImportMemoryWin32HandleInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleType : VkExternalMemoryHandleTypeFlagsNV
            val mutable public handle : nativeint
    
            new(sType : VkStructureType, pNext : nativeint, handleType : VkExternalMemoryHandleTypeFlagsNV, handle : nativeint) = { sType = sType; pNext = pNext; handleType = handleType; handle = handle }
            override x.ToString() =
                sprintf "VkImportMemoryWin32HandleInfoNV { sType = %A; pNext = %A; handleType = %A; handle = %A }" x.sType x.pNext x.handleType x.handle
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
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineCoverageToColorStateCreateFlagsNV, coverageToColorEnable : VkBool32, coverageToColorLocation : uint32) = { sType = sType; pNext = pNext; flags = flags; coverageToColorEnable = coverageToColorEnable; coverageToColorLocation = coverageToColorLocation }
            override x.ToString() =
                sprintf "VkPipelineCoverageToColorStateCreateInfoNV { sType = %A; pNext = %A; flags = %A; coverageToColorEnable = %A; coverageToColorLocation = %A }" x.sType x.pNext x.flags x.coverageToColorEnable x.coverageToColorLocation
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
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineCoverageModulationStateCreateFlagsNV, coverageModulationMode : VkCoverageModulationModeNV, coverageModulationTableEnable : VkBool32, coverageModulationTableCount : uint32, pCoverageModulationTable : nativeptr<float32>) = { sType = sType; pNext = pNext; flags = flags; coverageModulationMode = coverageModulationMode; coverageModulationTableEnable = coverageModulationTableEnable; coverageModulationTableCount = coverageModulationTableCount; pCoverageModulationTable = pCoverageModulationTable }
            override x.ToString() =
                sprintf "VkPipelineCoverageModulationStateCreateInfoNV { sType = %A; pNext = %A; flags = %A; coverageModulationMode = %A; coverageModulationTableEnable = %A; coverageModulationTableCount = %A; pCoverageModulationTable = %A }" x.sType x.pNext x.flags x.coverageModulationMode x.coverageModulationTableEnable x.coverageModulationTableCount x.pCoverageModulationTable
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
    
            new(x : VkViewportCoordinateSwizzleNV, y : VkViewportCoordinateSwizzleNV, z : VkViewportCoordinateSwizzleNV, w : VkViewportCoordinateSwizzleNV) = { x = x; y = y; z = z; w = w }
            override x.ToString() =
                sprintf "VkViewportSwizzleNV { x = %A; y = %A; z = %A; w = %A }" x.x x.y x.z x.w
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPipelineViewportSwizzleStateCreateInfoNV = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public flags : VkPipelineViewportSwizzleStateCreateFlagsNV
            val mutable public viewportCount : uint32
            val mutable public pViewportSwizzles : nativeptr<VkViewportSwizzleNV>
    
            new(sType : VkStructureType, pNext : nativeint, flags : VkPipelineViewportSwizzleStateCreateFlagsNV, viewportCount : uint32, pViewportSwizzles : nativeptr<VkViewportSwizzleNV>) = { sType = sType; pNext = pNext; flags = flags; viewportCount = viewportCount; pViewportSwizzles = pViewportSwizzles }
            override x.ToString() =
                sprintf "VkPipelineViewportSwizzleStateCreateInfoNV { sType = %A; pNext = %A; flags = %A; viewportCount = %A; pViewportSwizzles = %A }" x.sType x.pNext x.flags x.viewportCount x.pViewportSwizzles
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
    
            new(sType : VkStructureType, pNext : nativeint, acquireCount : uint32, pAcquireSyncs : nativeptr<VkDeviceMemory>, pAcquireKeys : nativeptr<uint64>, pAcquireTimeoutMilliseconds : nativeptr<uint32>, releaseCount : uint32, pReleaseSyncs : nativeptr<VkDeviceMemory>, pReleaseKeys : nativeptr<uint64>) = { sType = sType; pNext = pNext; acquireCount = acquireCount; pAcquireSyncs = pAcquireSyncs; pAcquireKeys = pAcquireKeys; pAcquireTimeoutMilliseconds = pAcquireTimeoutMilliseconds; releaseCount = releaseCount; pReleaseSyncs = pReleaseSyncs; pReleaseKeys = pReleaseKeys }
            override x.ToString() =
                sprintf "VkWin32KeyedMutexAcquireReleaseInfoNV { sType = %A; pNext = %A; acquireCount = %A; pAcquireSyncs = %A; pAcquireKeys = %A; pAcquireTimeoutMilliseconds = %A; releaseCount = %A; pReleaseSyncs = %A; pReleaseKeys = %A }" x.sType x.pNext x.acquireCount x.pAcquireSyncs x.pAcquireKeys x.pAcquireTimeoutMilliseconds x.releaseCount x.pReleaseSyncs x.pReleaseKeys
        end
    
    
    type VkStructureType with
         static member inline Win32KeyedMutexAcquireReleaseInfoNv = unbox<VkStructureType> 1000058000
    

module NVXDeviceGeneratedCommands =
    let Name = "VK_NVX_device_generated_commands"
    let Number = 87
    
    open EXTDebugReport
    
    [<Flags>]
    type VkIndirectCommandsLayoutUsageFlagBitsNVX = 
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
    type VkObjectEntryUsageFlagBitsNVX = 
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
    
            new(tokenType : VkIndirectCommandsTokenTypeNVX, buffer : VkBuffer, offset : VkDeviceSize) = { tokenType = tokenType; buffer = buffer; offset = offset }
            override x.ToString() =
                sprintf "VkIndirectCommandsTokenNVX { tokenType = %A; buffer = %A; offset = %A }" x.tokenType x.buffer x.offset
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
    
            new(sType : VkStructureType, pNext : nativeint, objectTable : VkObjectTableNVX, indirectCommandsLayout : VkIndirectCommandsLayoutNVX, indirectCommandsTokenCount : uint32, pIndirectCommandsTokens : nativeptr<VkIndirectCommandsTokenNVX>, maxSequencesCount : uint32, targetCommandBuffer : VkCommandBuffer, sequencesCountBuffer : VkBuffer, sequencesCountOffset : VkDeviceSize, sequencesIndexBuffer : VkBuffer, sequencesIndexOffset : VkDeviceSize) = { sType = sType; pNext = pNext; objectTable = objectTable; indirectCommandsLayout = indirectCommandsLayout; indirectCommandsTokenCount = indirectCommandsTokenCount; pIndirectCommandsTokens = pIndirectCommandsTokens; maxSequencesCount = maxSequencesCount; targetCommandBuffer = targetCommandBuffer; sequencesCountBuffer = sequencesCountBuffer; sequencesCountOffset = sequencesCountOffset; sequencesIndexBuffer = sequencesIndexBuffer; sequencesIndexOffset = sequencesIndexOffset }
            override x.ToString() =
                sprintf "VkCmdProcessCommandsInfoNVX { sType = %A; pNext = %A; objectTable = %A; indirectCommandsLayout = %A; indirectCommandsTokenCount = %A; pIndirectCommandsTokens = %A; maxSequencesCount = %A; targetCommandBuffer = %A; sequencesCountBuffer = %A; sequencesCountOffset = %A; sequencesIndexBuffer = %A; sequencesIndexOffset = %A }" x.sType x.pNext x.objectTable x.indirectCommandsLayout x.indirectCommandsTokenCount x.pIndirectCommandsTokens x.maxSequencesCount x.targetCommandBuffer x.sequencesCountBuffer x.sequencesCountOffset x.sequencesIndexBuffer x.sequencesIndexOffset
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkCmdReserveSpaceForCommandsInfoNVX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public objectTable : VkObjectTableNVX
            val mutable public indirectCommandsLayout : VkIndirectCommandsLayoutNVX
            val mutable public maxSequencesCount : uint32
    
            new(sType : VkStructureType, pNext : nativeint, objectTable : VkObjectTableNVX, indirectCommandsLayout : VkIndirectCommandsLayoutNVX, maxSequencesCount : uint32) = { sType = sType; pNext = pNext; objectTable = objectTable; indirectCommandsLayout = indirectCommandsLayout; maxSequencesCount = maxSequencesCount }
            override x.ToString() =
                sprintf "VkCmdReserveSpaceForCommandsInfoNVX { sType = %A; pNext = %A; objectTable = %A; indirectCommandsLayout = %A; maxSequencesCount = %A }" x.sType x.pNext x.objectTable x.indirectCommandsLayout x.maxSequencesCount
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkDeviceGeneratedCommandsFeaturesNVX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public computeBindingPointSupport : VkBool32
    
            new(sType : VkStructureType, pNext : nativeint, computeBindingPointSupport : VkBool32) = { sType = sType; pNext = pNext; computeBindingPointSupport = computeBindingPointSupport }
            override x.ToString() =
                sprintf "VkDeviceGeneratedCommandsFeaturesNVX { sType = %A; pNext = %A; computeBindingPointSupport = %A }" x.sType x.pNext x.computeBindingPointSupport
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
    
            new(sType : VkStructureType, pNext : nativeint, maxIndirectCommandsLayoutTokenCount : uint32, maxObjectEntryCounts : uint32, minSequenceCountBufferOffsetAlignment : uint32, minSequenceIndexBufferOffsetAlignment : uint32, minCommandsTokenBufferOffsetAlignment : uint32) = { sType = sType; pNext = pNext; maxIndirectCommandsLayoutTokenCount = maxIndirectCommandsLayoutTokenCount; maxObjectEntryCounts = maxObjectEntryCounts; minSequenceCountBufferOffsetAlignment = minSequenceCountBufferOffsetAlignment; minSequenceIndexBufferOffsetAlignment = minSequenceIndexBufferOffsetAlignment; minCommandsTokenBufferOffsetAlignment = minCommandsTokenBufferOffsetAlignment }
            override x.ToString() =
                sprintf "VkDeviceGeneratedCommandsLimitsNVX { sType = %A; pNext = %A; maxIndirectCommandsLayoutTokenCount = %A; maxObjectEntryCounts = %A; minSequenceCountBufferOffsetAlignment = %A; minSequenceIndexBufferOffsetAlignment = %A; minCommandsTokenBufferOffsetAlignment = %A }" x.sType x.pNext x.maxIndirectCommandsLayoutTokenCount x.maxObjectEntryCounts x.minSequenceCountBufferOffsetAlignment x.minSequenceIndexBufferOffsetAlignment x.minCommandsTokenBufferOffsetAlignment
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkIndirectCommandsLayoutTokenNVX = 
        struct
            val mutable public tokenType : VkIndirectCommandsTokenTypeNVX
            val mutable public bindingUnit : uint32
            val mutable public dynamicCount : uint32
            val mutable public divisor : uint32
    
            new(tokenType : VkIndirectCommandsTokenTypeNVX, bindingUnit : uint32, dynamicCount : uint32, divisor : uint32) = { tokenType = tokenType; bindingUnit = bindingUnit; dynamicCount = dynamicCount; divisor = divisor }
            override x.ToString() =
                sprintf "VkIndirectCommandsLayoutTokenNVX { tokenType = %A; bindingUnit = %A; dynamicCount = %A; divisor = %A }" x.tokenType x.bindingUnit x.dynamicCount x.divisor
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
    
            new(sType : VkStructureType, pNext : nativeint, pipelineBindPoint : VkPipelineBindPoint, flags : VkIndirectCommandsLayoutUsageFlagsNVX, tokenCount : uint32, pTokens : nativeptr<VkIndirectCommandsLayoutTokenNVX>) = { sType = sType; pNext = pNext; pipelineBindPoint = pipelineBindPoint; flags = flags; tokenCount = tokenCount; pTokens = pTokens }
            override x.ToString() =
                sprintf "VkIndirectCommandsLayoutCreateInfoNVX { sType = %A; pNext = %A; pipelineBindPoint = %A; flags = %A; tokenCount = %A; pTokens = %A }" x.sType x.pNext x.pipelineBindPoint x.flags x.tokenCount x.pTokens
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
    
            new(sType : VkStructureType, pNext : nativeint, objectCount : uint32, pObjectEntryTypes : nativeptr<VkObjectEntryTypeNVX>, pObjectEntryCounts : nativeptr<uint32>, pObjectEntryUsageFlags : nativeptr<VkObjectEntryUsageFlagsNVX>, maxUniformBuffersPerDescriptor : uint32, maxStorageBuffersPerDescriptor : uint32, maxStorageImagesPerDescriptor : uint32, maxSampledImagesPerDescriptor : uint32, maxPipelineLayouts : uint32) = { sType = sType; pNext = pNext; objectCount = objectCount; pObjectEntryTypes = pObjectEntryTypes; pObjectEntryCounts = pObjectEntryCounts; pObjectEntryUsageFlags = pObjectEntryUsageFlags; maxUniformBuffersPerDescriptor = maxUniformBuffersPerDescriptor; maxStorageBuffersPerDescriptor = maxStorageBuffersPerDescriptor; maxStorageImagesPerDescriptor = maxStorageImagesPerDescriptor; maxSampledImagesPerDescriptor = maxSampledImagesPerDescriptor; maxPipelineLayouts = maxPipelineLayouts }
            override x.ToString() =
                sprintf "VkObjectTableCreateInfoNVX { sType = %A; pNext = %A; objectCount = %A; pObjectEntryTypes = %A; pObjectEntryCounts = %A; pObjectEntryUsageFlags = %A; maxUniformBuffersPerDescriptor = %A; maxStorageBuffersPerDescriptor = %A; maxStorageImagesPerDescriptor = %A; maxSampledImagesPerDescriptor = %A; maxPipelineLayouts = %A }" x.sType x.pNext x.objectCount x.pObjectEntryTypes x.pObjectEntryCounts x.pObjectEntryUsageFlags x.maxUniformBuffersPerDescriptor x.maxStorageBuffersPerDescriptor x.maxStorageImagesPerDescriptor x.maxSampledImagesPerDescriptor x.maxPipelineLayouts
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkObjectTableDescriptorSetEntryNVX = 
        struct
            val mutable public _type : VkObjectEntryTypeNVX
            val mutable public flags : VkObjectEntryUsageFlagsNVX
            val mutable public pipelineLayout : VkPipelineLayout
            val mutable public descriptorSet : VkDescriptorSet
    
            new(_type : VkObjectEntryTypeNVX, flags : VkObjectEntryUsageFlagsNVX, pipelineLayout : VkPipelineLayout, descriptorSet : VkDescriptorSet) = { _type = _type; flags = flags; pipelineLayout = pipelineLayout; descriptorSet = descriptorSet }
            override x.ToString() =
                sprintf "VkObjectTableDescriptorSetEntryNVX { _type = %A; flags = %A; pipelineLayout = %A; descriptorSet = %A }" x._type x.flags x.pipelineLayout x.descriptorSet
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkObjectTableEntryNVX = 
        struct
            val mutable public _type : VkObjectEntryTypeNVX
            val mutable public flags : VkObjectEntryUsageFlagsNVX
    
            new(_type : VkObjectEntryTypeNVX, flags : VkObjectEntryUsageFlagsNVX) = { _type = _type; flags = flags }
            override x.ToString() =
                sprintf "VkObjectTableEntryNVX { _type = %A; flags = %A }" x._type x.flags
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkObjectTableIndexBufferEntryNVX = 
        struct
            val mutable public _type : VkObjectEntryTypeNVX
            val mutable public flags : VkObjectEntryUsageFlagsNVX
            val mutable public buffer : VkBuffer
            val mutable public indexType : VkIndexType
    
            new(_type : VkObjectEntryTypeNVX, flags : VkObjectEntryUsageFlagsNVX, buffer : VkBuffer, indexType : VkIndexType) = { _type = _type; flags = flags; buffer = buffer; indexType = indexType }
            override x.ToString() =
                sprintf "VkObjectTableIndexBufferEntryNVX { _type = %A; flags = %A; buffer = %A; indexType = %A }" x._type x.flags x.buffer x.indexType
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkObjectTablePipelineEntryNVX = 
        struct
            val mutable public _type : VkObjectEntryTypeNVX
            val mutable public flags : VkObjectEntryUsageFlagsNVX
            val mutable public pipeline : VkPipeline
    
            new(_type : VkObjectEntryTypeNVX, flags : VkObjectEntryUsageFlagsNVX, pipeline : VkPipeline) = { _type = _type; flags = flags; pipeline = pipeline }
            override x.ToString() =
                sprintf "VkObjectTablePipelineEntryNVX { _type = %A; flags = %A; pipeline = %A }" x._type x.flags x.pipeline
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkObjectTablePushConstantEntryNVX = 
        struct
            val mutable public _type : VkObjectEntryTypeNVX
            val mutable public flags : VkObjectEntryUsageFlagsNVX
            val mutable public pipelineLayout : VkPipelineLayout
            val mutable public stageFlags : VkShaderStageFlags
    
            new(_type : VkObjectEntryTypeNVX, flags : VkObjectEntryUsageFlagsNVX, pipelineLayout : VkPipelineLayout, stageFlags : VkShaderStageFlags) = { _type = _type; flags = flags; pipelineLayout = pipelineLayout; stageFlags = stageFlags }
            override x.ToString() =
                sprintf "VkObjectTablePushConstantEntryNVX { _type = %A; flags = %A; pipelineLayout = %A; stageFlags = %A }" x._type x.flags x.pipelineLayout x.stageFlags
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkObjectTableVertexBufferEntryNVX = 
        struct
            val mutable public _type : VkObjectEntryTypeNVX
            val mutable public flags : VkObjectEntryUsageFlagsNVX
            val mutable public buffer : VkBuffer
    
            new(_type : VkObjectEntryTypeNVX, flags : VkObjectEntryUsageFlagsNVX, buffer : VkBuffer) = { _type = _type; flags = flags; buffer = buffer }
            override x.ToString() =
                sprintf "VkObjectTableVertexBufferEntryNVX { _type = %A; flags = %A; buffer = %A }" x._type x.flags x.buffer
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
    
    let Required = [ KHRGetPhysicalDeviceProperties2.Name; KHXMultiview.Name ]
    open KHRGetPhysicalDeviceProperties2
    open KHXMultiview
    open EXTDebugReport
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type VkPhysicalDeviceMultiviewPerViewAttributesPropertiesNVX = 
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public perViewPositionAllComponents : VkBool32
    
            new(sType : VkStructureType, pNext : nativeint, perViewPositionAllComponents : VkBool32) = { sType = sType; pNext = pNext; perViewPositionAllComponents = perViewPositionAllComponents }
            override x.ToString() =
                sprintf "VkPhysicalDeviceMultiviewPerViewAttributesPropertiesNVX { sType = %A; pNext = %A; perViewPositionAllComponents = %A }" x.sType x.pNext x.perViewPositionAllComponents
        end
    
    
    type VkStructureType with
         static member inline PhysicalDeviceMultiviewPerViewAttributesPropertiesNvx = unbox<VkStructureType> 1000097000
    type VkSubpassDescriptionFlags with
         static member inline PerViewAttributesBitNvx = unbox<VkSubpassDescriptionFlags> 1
         static member inline PerViewPositionXOnlyBitNvx = unbox<VkSubpassDescriptionFlags> 2
    
