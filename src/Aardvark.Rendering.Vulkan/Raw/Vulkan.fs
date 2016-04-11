namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
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
type VkCmdBufferCreateFlags = uint32
type VkEventCreateFlags = uint32
type VkSemaphoreCreateFlags = uint32
type VkShaderCreateFlags = uint32
type VkShaderModuleCreateFlags = uint32
type VkMemoryMapFlags = uint32
type VkDisplayPlaneAlphaFlagsKHR = uint32
type VkDisplaySurfaceCreateFlagsKHR = uint32
type VkSwapchainCreateFlagsKHR = uint32
type VkSurfaceTransformFlagsKHR = uint32
type VkCompositeAlphaFlagsKHR = uint32
type VkPipelineLayoutCreateFlags = uint32
type VkBufferViewCreateFlags = uint32
type VkPipelineShaderStageCreateFlags = uint32
type VkDescriptorSetLayoutCreateFlags = uint32
type VkDeviceQueueCreateFlags = uint32
type VkInstanceCreateFlags = uint32
type VkImageViewCreateFlags = uint32
type VkDeviceCreateFlags = uint32
type VkFramebufferCreateFlags = uint32
type VkDescriptorPoolResetFlags = uint32
type VkPipelineVertexInputStateCreateFlags = uint32
type VkPipelineInputAssemblyStateCreateFlags = uint32
type VkPipelineTesselationStateCreateFlags = uint32
type VkPipelineViewportStateCreateFlags = uint32
type VkPipelineRasterizationStateCreateFlags = uint32
type VkPipelineMultisampleStateCreateFlags = uint32
type VkPipelineDepthStencilStateCreateFlags = uint32
type VkPipelineColorBlendStateCreateFlags = uint32
type VkPipelineDynamicStateCreateFlags = uint32
type VkPipelineCacheCreateFlags = uint32
type VkQueryPoolCreateFlags = uint32
type VkSubpassDescriptionFlags = uint32
type VkRenderPassCreateFlags = uint32
type VkSamplerCreateFlags = uint32

type VkAndroidSurfaceCreateFlagsKHR = uint32
type VkDisplayModeCreateFlagsKHR = uint32
type VkPipelineTessellationStateCreateFlags = uint32
type VkXcbSurfaceCreateFlagsKHR = uint32
type VkXlibSurfaceCreateFlagsKHR = uint32
type VkWin32SurfaceCreateFlagsKHR = uint32
type VkWaylandSurfaceCreateFlagsKHR = uint32
type VkMirSurfaceCreateFlagsKHR = uint32
type VkDebugReportFlagsEXT = uint32
type PFN_vkDebugReportCallbackEXT = nativeint

type VkInstance = nativeint
type VkPhysicalDevice = nativeint
type VkDevice = nativeint
type VkQueue = nativeint
type VkCommandBuffer = nativeint
[<StructLayout(LayoutKind.Sequential)>]
type VkDeviceMemory = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkDeviceMemory(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkCommandPool = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkCommandPool(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBuffer = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkBuffer(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkBufferView = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkBufferView(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImage = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkImage(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkImageView = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkImageView(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkShaderModule = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkShaderModule(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipeline = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkPipeline(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineLayout = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkPipelineLayout(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSampler = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkSampler(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorSet = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkDescriptorSet(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorSetLayout = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkDescriptorSetLayout(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDescriptorPool = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkDescriptorPool(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkFence = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkFence(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSemaphore = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkSemaphore(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkEvent = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkEvent(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkQueryPool = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkQueryPool(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkFramebuffer = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkFramebuffer(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkRenderPass = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkRenderPass(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkPipelineCache = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkPipelineCache(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDisplayKHR = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkDisplayKHR(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDisplayModeKHR = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkDisplayModeKHR(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSurfaceKHR = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkSurfaceKHR(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkSwapchainKHR = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkSwapchainKHR(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
    end

[<StructLayout(LayoutKind.Sequential)>]
type VkDebugReportCallbackEXT = 
    struct
        val mutable public Handle : uint64
        new(h) = { Handle = h }
        static member Null = VkDebugReportCallbackEXT(0UL)
        member x.IsNull = x.Handle = 0UL
        member x.IsValid = x.Handle <> 0UL
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
    | MirrorClampToEdge = 4

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
    | VkColorspaceSrgbNonlinearKhr = 0

[<Flags>]
type VkDisplayPlaneAlphaFlagBitsKHR = 
    | None = 0
    | VkDisplayPlaneAlphaOpaqueBitKhr = 0x00000001
    | VkDisplayPlaneAlphaGlobalBitKhr = 0x00000002
    | VkDisplayPlaneAlphaPerPixelBitKhr = 0x00000004
    | VkDisplayPlaneAlphaPerPixelPremultipliedBitKhr = 0x00000008

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
    | VkDebugReportObjectTypeDebugReportExt = 28

type VkDebugReportErrorEXT = 
    | VkDebugReportErrorNoneExt = 0
    | VkDebugReportErrorCallbackRefExt = 1

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
type VkRect3D = 
    struct
        val mutable public offset : VkOffset3D
        val mutable public extent : VkExtent3D

        new(offset : VkOffset3D, extent : VkExtent3D) = { offset = offset; extent = extent }
        override x.ToString() =
            sprintf "VkRect3D { offset = %A; extent = %A }" x.offset x.extent
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

[<AutoOpen>]
module WSIEnums = 
    type VkStructureType with
        static member XLibSurfaceCreateInfo = unbox<VkStructureType> 1000004000
        static member XcbSurfaceCreateInfo = unbox<VkStructureType> 1000005000
        static member WaylandSurfaceCreateInfo = unbox<VkStructureType> 1000006000
        static member MirSurfaceCreateInfo = unbox<VkStructureType> 1000007000
        static member AndroidSurfaceCreateInfo = unbox<VkStructureType> 1000008000
        static member Win32SurfaceCreateInfo = unbox<VkStructureType> 1000009000
module VkRaw = 
    [<Literal>]
    let lib = "vulkan-1.dll"

    [<DllImport(lib)>]
    extern VkResult vkCreateInstance(VkInstanceCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkInstance* pInstance)
    [<DllImport(lib)>]
    extern void vkDestroyInstance(VkInstance instance, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkEnumeratePhysicalDevices(VkInstance instance, uint32* pPhysicalDeviceCount, VkPhysicalDevice* pPhysicalDevices)
    [<DllImport(lib)>]
    extern PFN_vkVoidFunction vkGetDeviceProcAddr(VkDevice device, string pName)
    [<DllImport(lib)>]
    extern PFN_vkVoidFunction vkGetInstanceProcAddr(VkInstance instance, string pName)
    [<DllImport(lib)>]
    extern void vkGetPhysicalDeviceProperties(VkPhysicalDevice physicalDevice, VkPhysicalDeviceProperties* pProperties)
    [<DllImport(lib)>]
    extern void vkGetPhysicalDeviceQueueFamilyProperties(VkPhysicalDevice physicalDevice, uint32* pQueueFamilyPropertyCount, VkQueueFamilyProperties* pQueueFamilyProperties)
    [<DllImport(lib)>]
    extern void vkGetPhysicalDeviceMemoryProperties(VkPhysicalDevice physicalDevice, VkPhysicalDeviceMemoryProperties* pMemoryProperties)
    [<DllImport(lib)>]
    extern void vkGetPhysicalDeviceFeatures(VkPhysicalDevice physicalDevice, VkPhysicalDeviceFeatures* pFeatures)
    [<DllImport(lib)>]
    extern void vkGetPhysicalDeviceFormatProperties(VkPhysicalDevice physicalDevice, VkFormat format, VkFormatProperties* pFormatProperties)
    [<DllImport(lib)>]
    extern VkResult vkGetPhysicalDeviceImageFormatProperties(VkPhysicalDevice physicalDevice, VkFormat format, VkImageType _type, VkImageTiling tiling, VkImageUsageFlags usage, VkImageCreateFlags flags, VkImageFormatProperties* pImageFormatProperties)
    [<DllImport(lib)>]
    extern VkResult vkCreateDevice(VkPhysicalDevice physicalDevice, VkDeviceCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkDevice* pDevice)
    [<DllImport(lib)>]
    extern void vkDestroyDevice(VkDevice device, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkEnumerateInstanceLayerProperties(uint32* pPropertyCount, VkLayerProperties* pProperties)
    [<DllImport(lib)>]
    extern VkResult vkEnumerateInstanceExtensionProperties(string pLayerName, uint32* pPropertyCount, VkExtensionProperties* pProperties)
    [<DllImport(lib)>]
    extern VkResult vkEnumerateDeviceLayerProperties(VkPhysicalDevice physicalDevice, uint32* pPropertyCount, VkLayerProperties* pProperties)
    [<DllImport(lib)>]
    extern VkResult vkEnumerateDeviceExtensionProperties(VkPhysicalDevice physicalDevice, string pLayerName, uint32* pPropertyCount, VkExtensionProperties* pProperties)
    [<DllImport(lib)>]
    extern void vkGetDeviceQueue(VkDevice device, uint32 queueFamilyIndex, uint32 queueIndex, VkQueue* pQueue)
    [<DllImport(lib)>]
    extern VkResult vkQueueSubmit(VkQueue queue, uint32 submitCount, VkSubmitInfo* pSubmits, VkFence fence)
    [<DllImport(lib)>]
    extern VkResult vkQueueWaitIdle(VkQueue queue)
    [<DllImport(lib)>]
    extern VkResult vkDeviceWaitIdle(VkDevice device)
    [<DllImport(lib)>]
    extern VkResult vkAllocateMemory(VkDevice device, VkMemoryAllocateInfo* pAllocateInfo, VkAllocationCallbacks* pAllocator, VkDeviceMemory* pMemory)
    [<DllImport(lib)>]
    extern void vkFreeMemory(VkDevice device, VkDeviceMemory memory, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkMapMemory(VkDevice device, VkDeviceMemory memory, VkDeviceSize offset, VkDeviceSize size, VkMemoryMapFlags flags, nativeint* ppData)
    [<DllImport(lib)>]
    extern void vkUnmapMemory(VkDevice device, VkDeviceMemory memory)
    [<DllImport(lib)>]
    extern VkResult vkFlushMappedMemoryRanges(VkDevice device, uint32 memoryRangeCount, VkMappedMemoryRange* pMemoryRanges)
    [<DllImport(lib)>]
    extern VkResult vkInvalidateMappedMemoryRanges(VkDevice device, uint32 memoryRangeCount, VkMappedMemoryRange* pMemoryRanges)
    [<DllImport(lib)>]
    extern void vkGetDeviceMemoryCommitment(VkDevice device, VkDeviceMemory memory, VkDeviceSize* pCommittedMemoryInBytes)
    [<DllImport(lib)>]
    extern void vkGetBufferMemoryRequirements(VkDevice device, VkBuffer buffer, VkMemoryRequirements* pMemoryRequirements)
    [<DllImport(lib)>]
    extern VkResult vkBindBufferMemory(VkDevice device, VkBuffer buffer, VkDeviceMemory memory, VkDeviceSize memoryOffset)
    [<DllImport(lib)>]
    extern void vkGetImageMemoryRequirements(VkDevice device, VkImage image, VkMemoryRequirements* pMemoryRequirements)
    [<DllImport(lib)>]
    extern VkResult vkBindImageMemory(VkDevice device, VkImage image, VkDeviceMemory memory, VkDeviceSize memoryOffset)
    [<DllImport(lib)>]
    extern void vkGetImageSparseMemoryRequirements(VkDevice device, VkImage image, uint32* pSparseMemoryRequirementCount, VkSparseImageMemoryRequirements* pSparseMemoryRequirements)
    [<DllImport(lib)>]
    extern void vkGetPhysicalDeviceSparseImageFormatProperties(VkPhysicalDevice physicalDevice, VkFormat format, VkImageType _type, VkSampleCountFlags samples, VkImageUsageFlags usage, VkImageTiling tiling, uint32* pPropertyCount, VkSparseImageFormatProperties* pProperties)
    [<DllImport(lib)>]
    extern VkResult vkQueueBindSparse(VkQueue queue, uint32 bindInfoCount, VkBindSparseInfo* pBindInfo, VkFence fence)
    [<DllImport(lib)>]
    extern VkResult vkCreateFence(VkDevice device, VkFenceCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkFence* pFence)
    [<DllImport(lib)>]
    extern void vkDestroyFence(VkDevice device, VkFence fence, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkResetFences(VkDevice device, uint32 fenceCount, VkFence* pFences)
    [<DllImport(lib)>]
    extern VkResult vkGetFenceStatus(VkDevice device, VkFence fence)
    [<DllImport(lib)>]
    extern VkResult vkWaitForFences(VkDevice device, uint32 fenceCount, VkFence* pFences, VkBool32 waitAll, uint64 timeout)
    [<DllImport(lib)>]
    extern VkResult vkCreateSemaphore(VkDevice device, VkSemaphoreCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkSemaphore* pSemaphore)
    [<DllImport(lib)>]
    extern void vkDestroySemaphore(VkDevice device, VkSemaphore semaphore, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkCreateEvent(VkDevice device, VkEventCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkEvent* pEvent)
    [<DllImport(lib)>]
    extern void vkDestroyEvent(VkDevice device, VkEvent event, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkGetEventStatus(VkDevice device, VkEvent event)
    [<DllImport(lib)>]
    extern VkResult vkSetEvent(VkDevice device, VkEvent event)
    [<DllImport(lib)>]
    extern VkResult vkResetEvent(VkDevice device, VkEvent event)
    [<DllImport(lib)>]
    extern VkResult vkCreateQueryPool(VkDevice device, VkQueryPoolCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkQueryPool* pQueryPool)
    [<DllImport(lib)>]
    extern void vkDestroyQueryPool(VkDevice device, VkQueryPool queryPool, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkGetQueryPoolResults(VkDevice device, VkQueryPool queryPool, uint32 firstQuery, uint32 queryCount, uint64 dataSize, nativeint pData, VkDeviceSize stride, VkQueryResultFlags flags)
    [<DllImport(lib)>]
    extern VkResult vkCreateBuffer(VkDevice device, VkBufferCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkBuffer* pBuffer)
    [<DllImport(lib)>]
    extern void vkDestroyBuffer(VkDevice device, VkBuffer buffer, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkCreateBufferView(VkDevice device, VkBufferViewCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkBufferView* pView)
    [<DllImport(lib)>]
    extern void vkDestroyBufferView(VkDevice device, VkBufferView bufferView, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkCreateImage(VkDevice device, VkImageCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkImage* pImage)
    [<DllImport(lib)>]
    extern void vkDestroyImage(VkDevice device, VkImage image, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern void vkGetImageSubresourceLayout(VkDevice device, VkImage image, VkImageSubresource* pSubresource, VkSubresourceLayout* pLayout)
    [<DllImport(lib)>]
    extern VkResult vkCreateImageView(VkDevice device, VkImageViewCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkImageView* pView)
    [<DllImport(lib)>]
    extern void vkDestroyImageView(VkDevice device, VkImageView imageView, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkCreateShaderModule(VkDevice device, VkShaderModuleCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkShaderModule* pShaderModule)
    [<DllImport(lib)>]
    extern void vkDestroyShaderModule(VkDevice device, VkShaderModule shaderModule, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkCreatePipelineCache(VkDevice device, VkPipelineCacheCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkPipelineCache* pPipelineCache)
    [<DllImport(lib)>]
    extern void vkDestroyPipelineCache(VkDevice device, VkPipelineCache pipelineCache, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkGetPipelineCacheData(VkDevice device, VkPipelineCache pipelineCache, uint64* pDataSize, nativeint pData)
    [<DllImport(lib)>]
    extern VkResult vkMergePipelineCaches(VkDevice device, VkPipelineCache dstCache, uint32 srcCacheCount, VkPipelineCache* pSrcCaches)
    [<DllImport(lib)>]
    extern VkResult vkCreateGraphicsPipelines(VkDevice device, VkPipelineCache pipelineCache, uint32 createInfoCount, VkGraphicsPipelineCreateInfo* pCreateInfos, VkAllocationCallbacks* pAllocator, VkPipeline* pPipelines)
    [<DllImport(lib)>]
    extern VkResult vkCreateComputePipelines(VkDevice device, VkPipelineCache pipelineCache, uint32 createInfoCount, VkComputePipelineCreateInfo* pCreateInfos, VkAllocationCallbacks* pAllocator, VkPipeline* pPipelines)
    [<DllImport(lib)>]
    extern void vkDestroyPipeline(VkDevice device, VkPipeline pipeline, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkCreatePipelineLayout(VkDevice device, VkPipelineLayoutCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkPipelineLayout* pPipelineLayout)
    [<DllImport(lib)>]
    extern void vkDestroyPipelineLayout(VkDevice device, VkPipelineLayout pipelineLayout, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkCreateSampler(VkDevice device, VkSamplerCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkSampler* pSampler)
    [<DllImport(lib)>]
    extern void vkDestroySampler(VkDevice device, VkSampler sampler, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkCreateDescriptorSetLayout(VkDevice device, VkDescriptorSetLayoutCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkDescriptorSetLayout* pSetLayout)
    [<DllImport(lib)>]
    extern void vkDestroyDescriptorSetLayout(VkDevice device, VkDescriptorSetLayout descriptorSetLayout, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkCreateDescriptorPool(VkDevice device, VkDescriptorPoolCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkDescriptorPool* pDescriptorPool)
    [<DllImport(lib)>]
    extern void vkDestroyDescriptorPool(VkDevice device, VkDescriptorPool descriptorPool, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkResetDescriptorPool(VkDevice device, VkDescriptorPool descriptorPool, VkDescriptorPoolResetFlags flags)
    [<DllImport(lib)>]
    extern VkResult vkAllocateDescriptorSets(VkDevice device, VkDescriptorSetAllocateInfo* pAllocateInfo, VkDescriptorSet* pDescriptorSets)
    [<DllImport(lib)>]
    extern VkResult vkFreeDescriptorSets(VkDevice device, VkDescriptorPool descriptorPool, uint32 descriptorSetCount, VkDescriptorSet* pDescriptorSets)
    [<DllImport(lib)>]
    extern void vkUpdateDescriptorSets(VkDevice device, uint32 descriptorWriteCount, VkWriteDescriptorSet* pDescriptorWrites, uint32 descriptorCopyCount, VkCopyDescriptorSet* pDescriptorCopies)
    [<DllImport(lib)>]
    extern VkResult vkCreateFramebuffer(VkDevice device, VkFramebufferCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkFramebuffer* pFramebuffer)
    [<DllImport(lib)>]
    extern void vkDestroyFramebuffer(VkDevice device, VkFramebuffer framebuffer, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkCreateRenderPass(VkDevice device, VkRenderPassCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkRenderPass* pRenderPass)
    [<DllImport(lib)>]
    extern void vkDestroyRenderPass(VkDevice device, VkRenderPass renderPass, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern void vkGetRenderAreaGranularity(VkDevice device, VkRenderPass renderPass, VkExtent2D* pGranularity)
    [<DllImport(lib)>]
    extern VkResult vkCreateCommandPool(VkDevice device, VkCommandPoolCreateInfo* pCreateInfo, VkAllocationCallbacks* pAllocator, VkCommandPool* pCommandPool)
    [<DllImport(lib)>]
    extern void vkDestroyCommandPool(VkDevice device, VkCommandPool commandPool, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkResetCommandPool(VkDevice device, VkCommandPool commandPool, VkCommandPoolResetFlags flags)
    [<DllImport(lib)>]
    extern VkResult vkAllocateCommandBuffers(VkDevice device, VkCommandBufferAllocateInfo* pAllocateInfo, VkCommandBuffer* pCommandBuffers)
    [<DllImport(lib)>]
    extern void vkFreeCommandBuffers(VkDevice device, VkCommandPool commandPool, uint32 commandBufferCount, VkCommandBuffer* pCommandBuffers)
    [<DllImport(lib)>]
    extern VkResult vkBeginCommandBuffer(VkCommandBuffer commandBuffer, VkCommandBufferBeginInfo* pBeginInfo)
    [<DllImport(lib)>]
    extern VkResult vkEndCommandBuffer(VkCommandBuffer commandBuffer)
    [<DllImport(lib)>]
    extern VkResult vkResetCommandBuffer(VkCommandBuffer commandBuffer, VkCommandBufferResetFlags flags)
    [<DllImport(lib)>]
    extern void vkCmdBindPipeline(VkCommandBuffer commandBuffer, VkPipelineBindPoint pipelineBindPoint, VkPipeline pipeline)
    [<DllImport(lib)>]
    extern void vkCmdSetViewport(VkCommandBuffer commandBuffer, uint32 firstViewport, uint32 viewportCount, VkViewport* pViewports)
    [<DllImport(lib)>]
    extern void vkCmdSetScissor(VkCommandBuffer commandBuffer, uint32 firstScissor, uint32 scissorCount, VkRect2D* pScissors)
    [<DllImport(lib)>]
    extern void vkCmdSetLineWidth(VkCommandBuffer commandBuffer, float32 lineWidth)
    [<DllImport(lib)>]
    extern void vkCmdSetDepthBias(VkCommandBuffer commandBuffer, float32 depthBiasConstantFactor, float32 depthBiasClamp, float32 depthBiasSlopeFactor)
    [<DllImport(lib)>]
    extern void vkCmdSetBlendConstants(VkCommandBuffer commandBuffer, V4f blendConstants)
    [<DllImport(lib)>]
    extern void vkCmdSetDepthBounds(VkCommandBuffer commandBuffer, float32 minDepthBounds, float32 maxDepthBounds)
    [<DllImport(lib)>]
    extern void vkCmdSetStencilCompareMask(VkCommandBuffer commandBuffer, VkStencilFaceFlags faceMask, uint32 compareMask)
    [<DllImport(lib)>]
    extern void vkCmdSetStencilWriteMask(VkCommandBuffer commandBuffer, VkStencilFaceFlags faceMask, uint32 writeMask)
    [<DllImport(lib)>]
    extern void vkCmdSetStencilReference(VkCommandBuffer commandBuffer, VkStencilFaceFlags faceMask, uint32 reference)
    [<DllImport(lib)>]
    extern void vkCmdBindDescriptorSets(VkCommandBuffer commandBuffer, VkPipelineBindPoint pipelineBindPoint, VkPipelineLayout layout, uint32 firstSet, uint32 descriptorSetCount, VkDescriptorSet* pDescriptorSets, uint32 dynamicOffsetCount, uint32* pDynamicOffsets)
    [<DllImport(lib)>]
    extern void vkCmdBindIndexBuffer(VkCommandBuffer commandBuffer, VkBuffer buffer, VkDeviceSize offset, VkIndexType indexType)
    [<DllImport(lib)>]
    extern void vkCmdBindVertexBuffers(VkCommandBuffer commandBuffer, uint32 firstBinding, uint32 bindingCount, VkBuffer* pBuffers, VkDeviceSize* pOffsets)
    [<DllImport(lib)>]
    extern void vkCmdDraw(VkCommandBuffer commandBuffer, uint32 vertexCount, uint32 instanceCount, uint32 firstVertex, uint32 firstInstance)
    [<DllImport(lib)>]
    extern void vkCmdDrawIndexed(VkCommandBuffer commandBuffer, uint32 indexCount, uint32 instanceCount, uint32 firstIndex, int vertexOffset, uint32 firstInstance)
    [<DllImport(lib)>]
    extern void vkCmdDrawIndirect(VkCommandBuffer commandBuffer, VkBuffer buffer, VkDeviceSize offset, uint32 drawCount, uint32 stride)
    [<DllImport(lib)>]
    extern void vkCmdDrawIndexedIndirect(VkCommandBuffer commandBuffer, VkBuffer buffer, VkDeviceSize offset, uint32 drawCount, uint32 stride)
    [<DllImport(lib)>]
    extern void vkCmdDispatch(VkCommandBuffer commandBuffer, uint32 x, uint32 y, uint32 z)
    [<DllImport(lib)>]
    extern void vkCmdDispatchIndirect(VkCommandBuffer commandBuffer, VkBuffer buffer, VkDeviceSize offset)
    [<DllImport(lib)>]
    extern void vkCmdCopyBuffer(VkCommandBuffer commandBuffer, VkBuffer srcBuffer, VkBuffer dstBuffer, uint32 regionCount, VkBufferCopy* pRegions)
    [<DllImport(lib)>]
    extern void vkCmdCopyImage(VkCommandBuffer commandBuffer, VkImage srcImage, VkImageLayout srcImageLayout, VkImage dstImage, VkImageLayout dstImageLayout, uint32 regionCount, VkImageCopy* pRegions)
    [<DllImport(lib)>]
    extern void vkCmdBlitImage(VkCommandBuffer commandBuffer, VkImage srcImage, VkImageLayout srcImageLayout, VkImage dstImage, VkImageLayout dstImageLayout, uint32 regionCount, VkImageBlit* pRegions, VkFilter filter)
    [<DllImport(lib)>]
    extern void vkCmdCopyBufferToImage(VkCommandBuffer commandBuffer, VkBuffer srcBuffer, VkImage dstImage, VkImageLayout dstImageLayout, uint32 regionCount, VkBufferImageCopy* pRegions)
    [<DllImport(lib)>]
    extern void vkCmdCopyImageToBuffer(VkCommandBuffer commandBuffer, VkImage srcImage, VkImageLayout srcImageLayout, VkBuffer dstBuffer, uint32 regionCount, VkBufferImageCopy* pRegions)
    [<DllImport(lib)>]
    extern void vkCmdUpdateBuffer(VkCommandBuffer commandBuffer, VkBuffer dstBuffer, VkDeviceSize dstOffset, VkDeviceSize dataSize, uint32* pData)
    [<DllImport(lib)>]
    extern void vkCmdFillBuffer(VkCommandBuffer commandBuffer, VkBuffer dstBuffer, VkDeviceSize dstOffset, VkDeviceSize size, uint32 data)
    [<DllImport(lib)>]
    extern void vkCmdClearColorImage(VkCommandBuffer commandBuffer, VkImage image, VkImageLayout imageLayout, VkClearColorValue* pColor, uint32 rangeCount, VkImageSubresourceRange* pRanges)
    [<DllImport(lib)>]
    extern void vkCmdClearDepthStencilImage(VkCommandBuffer commandBuffer, VkImage image, VkImageLayout imageLayout, VkClearDepthStencilValue* pDepthStencil, uint32 rangeCount, VkImageSubresourceRange* pRanges)
    [<DllImport(lib)>]
    extern void vkCmdClearAttachments(VkCommandBuffer commandBuffer, uint32 attachmentCount, VkClearAttachment* pAttachments, uint32 rectCount, VkClearRect* pRects)
    [<DllImport(lib)>]
    extern void vkCmdResolveImage(VkCommandBuffer commandBuffer, VkImage srcImage, VkImageLayout srcImageLayout, VkImage dstImage, VkImageLayout dstImageLayout, uint32 regionCount, VkImageResolve* pRegions)
    [<DllImport(lib)>]
    extern void vkCmdSetEvent(VkCommandBuffer commandBuffer, VkEvent event, VkPipelineStageFlags stageMask)
    [<DllImport(lib)>]
    extern void vkCmdResetEvent(VkCommandBuffer commandBuffer, VkEvent event, VkPipelineStageFlags stageMask)
    [<DllImport(lib)>]
    extern void vkCmdWaitEvents(VkCommandBuffer commandBuffer, uint32 eventCount, VkEvent* pEvents, VkPipelineStageFlags srcStageMask, VkPipelineStageFlags dstStageMask, uint32 memoryBarrierCount, VkMemoryBarrier* pMemoryBarriers, uint32 bufferMemoryBarrierCount, VkBufferMemoryBarrier* pBufferMemoryBarriers, uint32 imageMemoryBarrierCount, VkImageMemoryBarrier* pImageMemoryBarriers)
    [<DllImport(lib)>]
    extern void vkCmdPipelineBarrier(VkCommandBuffer commandBuffer, VkPipelineStageFlags srcStageMask, VkPipelineStageFlags dstStageMask, VkDependencyFlags dependencyFlags, uint32 memoryBarrierCount, VkMemoryBarrier* pMemoryBarriers, uint32 bufferMemoryBarrierCount, VkBufferMemoryBarrier* pBufferMemoryBarriers, uint32 imageMemoryBarrierCount, VkImageMemoryBarrier* pImageMemoryBarriers)
    [<DllImport(lib)>]
    extern void vkCmdBeginQuery(VkCommandBuffer commandBuffer, VkQueryPool queryPool, uint32 query, VkQueryControlFlags flags)
    [<DllImport(lib)>]
    extern void vkCmdEndQuery(VkCommandBuffer commandBuffer, VkQueryPool queryPool, uint32 query)
    [<DllImport(lib)>]
    extern void vkCmdResetQueryPool(VkCommandBuffer commandBuffer, VkQueryPool queryPool, uint32 firstQuery, uint32 queryCount)
    [<DllImport(lib)>]
    extern void vkCmdWriteTimestamp(VkCommandBuffer commandBuffer, VkPipelineStageFlags pipelineStage, VkQueryPool queryPool, uint32 query)
    [<DllImport(lib)>]
    extern void vkCmdCopyQueryPoolResults(VkCommandBuffer commandBuffer, VkQueryPool queryPool, uint32 firstQuery, uint32 queryCount, VkBuffer dstBuffer, VkDeviceSize dstOffset, VkDeviceSize stride, VkQueryResultFlags flags)
    [<DllImport(lib)>]
    extern void vkCmdPushConstants(VkCommandBuffer commandBuffer, VkPipelineLayout layout, VkShaderStageFlags stageFlags, uint32 offset, uint32 size, nativeint pValues)
    [<DllImport(lib)>]
    extern void vkCmdBeginRenderPass(VkCommandBuffer commandBuffer, VkRenderPassBeginInfo* pRenderPassBegin, VkSubpassContents contents)
    [<DllImport(lib)>]
    extern void vkCmdNextSubpass(VkCommandBuffer commandBuffer, VkSubpassContents contents)
    [<DllImport(lib)>]
    extern void vkCmdEndRenderPass(VkCommandBuffer commandBuffer)
    [<DllImport(lib)>]
    extern void vkCmdExecuteCommands(VkCommandBuffer commandBuffer, uint32 commandBufferCount, VkCommandBuffer* pCommandBuffers)
    [<DllImport(lib)>]
    extern VkResult vkCreateAndroidSurfaceKHR(VkInstance instance, VkAndroidSurfaceCreateInfoKHR* pCreateInfo, VkAllocationCallbacks* pAllocator, VkSurfaceKHR* pSurface)
    [<DllImport(lib)>]
    extern VkResult vkGetPhysicalDeviceDisplayPropertiesKHR(VkPhysicalDevice physicalDevice, uint32* pPropertyCount, VkDisplayPropertiesKHR* pProperties)
    [<DllImport(lib)>]
    extern VkResult vkGetPhysicalDeviceDisplayPlanePropertiesKHR(VkPhysicalDevice physicalDevice, uint32* pPropertyCount, VkDisplayPlanePropertiesKHR* pProperties)
    [<DllImport(lib)>]
    extern VkResult vkGetDisplayPlaneSupportedDisplaysKHR(VkPhysicalDevice physicalDevice, uint32 planeIndex, uint32* pDisplayCount, VkDisplayKHR* pDisplays)
    [<DllImport(lib)>]
    extern VkResult vkGetDisplayModePropertiesKHR(VkPhysicalDevice physicalDevice, VkDisplayKHR display, uint32* pPropertyCount, VkDisplayModePropertiesKHR* pProperties)
    [<DllImport(lib)>]
    extern VkResult vkCreateDisplayModeKHR(VkPhysicalDevice physicalDevice, VkDisplayKHR display, VkDisplayModeCreateInfoKHR* pCreateInfo, VkAllocationCallbacks* pAllocator, VkDisplayModeKHR* pMode)
    [<DllImport(lib)>]
    extern VkResult vkGetDisplayPlaneCapabilitiesKHR(VkPhysicalDevice physicalDevice, VkDisplayModeKHR mode, uint32 planeIndex, VkDisplayPlaneCapabilitiesKHR* pCapabilities)
    [<DllImport(lib)>]
    extern VkResult vkCreateDisplayPlaneSurfaceKHR(VkInstance instance, VkDisplaySurfaceCreateInfoKHR* pCreateInfo, VkAllocationCallbacks* pAllocator, VkSurfaceKHR* pSurface)
    [<DllImport(lib)>]
    extern VkResult vkCreateSharedSwapchainsKHR(VkDevice device, uint32 swapchainCount, VkSwapchainCreateInfoKHR* pCreateInfos, VkAllocationCallbacks* pAllocator, VkSwapchainKHR* pSwapchains)
    [<DllImport(lib)>]
    extern VkResult vkCreateMirSurfaceKHR(VkInstance instance, VkMirSurfaceCreateInfoKHR* pCreateInfo, VkAllocationCallbacks* pAllocator, VkSurfaceKHR* pSurface)
    [<DllImport(lib)>]
    extern VkBool32 vkGetPhysicalDeviceMirPresentationSupportKHR(VkPhysicalDevice physicalDevice, uint32 queueFamilyIndex, nativeint* connection)
    [<DllImport(lib)>]
    extern void vkDestroySurfaceKHR(VkInstance instance, VkSurfaceKHR surface, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkGetPhysicalDeviceSurfaceSupportKHR(VkPhysicalDevice physicalDevice, uint32 queueFamilyIndex, VkSurfaceKHR surface, VkBool32* pSupported)
    [<DllImport(lib)>]
    extern VkResult vkGetPhysicalDeviceSurfaceCapabilitiesKHR(VkPhysicalDevice physicalDevice, VkSurfaceKHR surface, VkSurfaceCapabilitiesKHR* pSurfaceCapabilities)
    [<DllImport(lib)>]
    extern VkResult vkGetPhysicalDeviceSurfaceFormatsKHR(VkPhysicalDevice physicalDevice, VkSurfaceKHR surface, uint32* pSurfaceFormatCount, VkSurfaceFormatKHR* pSurfaceFormats)
    [<DllImport(lib)>]
    extern VkResult vkGetPhysicalDeviceSurfacePresentModesKHR(VkPhysicalDevice physicalDevice, VkSurfaceKHR surface, uint32* pPresentModeCount, VkPresentModeKHR* pPresentModes)
    [<DllImport(lib)>]
    extern VkResult vkCreateSwapchainKHR(VkDevice device, VkSwapchainCreateInfoKHR* pCreateInfo, VkAllocationCallbacks* pAllocator, VkSwapchainKHR* pSwapchain)
    [<DllImport(lib)>]
    extern void vkDestroySwapchainKHR(VkDevice device, VkSwapchainKHR swapchain, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern VkResult vkGetSwapchainImagesKHR(VkDevice device, VkSwapchainKHR swapchain, uint32* pSwapchainImageCount, VkImage* pSwapchainImages)
    [<DllImport(lib)>]
    extern VkResult vkAcquireNextImageKHR(VkDevice device, VkSwapchainKHR swapchain, uint64 timeout, VkSemaphore semaphore, VkFence fence, uint32* pImageIndex)
    [<DllImport(lib)>]
    extern VkResult vkQueuePresentKHR(VkQueue queue, VkPresentInfoKHR* pPresentInfo)
    [<DllImport(lib)>]
    extern VkResult vkCreateWaylandSurfaceKHR(VkInstance instance, VkWaylandSurfaceCreateInfoKHR* pCreateInfo, VkAllocationCallbacks* pAllocator, VkSurfaceKHR* pSurface)
    [<DllImport(lib)>]
    extern VkBool32 vkGetPhysicalDeviceWaylandPresentationSupportKHR(VkPhysicalDevice physicalDevice, uint32 queueFamilyIndex, nativeint* display)
    [<DllImport(lib)>]
    extern VkResult vkCreateWin32SurfaceKHR(VkInstance instance, VkWin32SurfaceCreateInfoKHR* pCreateInfo, VkAllocationCallbacks* pAllocator, VkSurfaceKHR* pSurface)
    [<DllImport(lib)>]
    extern VkBool32 vkGetPhysicalDeviceWin32PresentationSupportKHR(VkPhysicalDevice physicalDevice, uint32 queueFamilyIndex)
    [<DllImport(lib)>]
    extern VkResult vkCreateXlibSurfaceKHR(VkInstance instance, VkXlibSurfaceCreateInfoKHR* pCreateInfo, VkAllocationCallbacks* pAllocator, VkSurfaceKHR* pSurface)
    [<DllImport(lib)>]
    extern VkBool32 vkGetPhysicalDeviceXlibPresentationSupportKHR(VkPhysicalDevice physicalDevice, uint32 queueFamilyIndex, nativeint* dpy, nativeint visualID)
    [<DllImport(lib)>]
    extern VkResult vkCreateXcbSurfaceKHR(VkInstance instance, VkXcbSurfaceCreateInfoKHR* pCreateInfo, VkAllocationCallbacks* pAllocator, VkSurfaceKHR* pSurface)
    [<DllImport(lib)>]
    extern VkBool32 vkGetPhysicalDeviceXcbPresentationSupportKHR(VkPhysicalDevice physicalDevice, uint32 queueFamilyIndex, nativeint* connection, nativeint visual_id)
    [<DllImport(lib)>]
    extern VkResult vkCreateDebugReportCallbackEXT(VkInstance instance, VkDebugReportCallbackCreateInfoEXT* pCreateInfo, VkAllocationCallbacks* pAllocator, VkDebugReportCallbackEXT* pCallback)
    [<DllImport(lib)>]
    extern void vkDestroyDebugReportCallbackEXT(VkInstance instance, VkDebugReportCallbackEXT callback, VkAllocationCallbacks* pAllocator)
    [<DllImport(lib)>]
    extern void vkDebugReportMessageEXT(VkInstance instance, VkDebugReportFlagsEXT flags, VkDebugReportObjectTypeEXT objectType, uint64 _object, uint64 location, int messageCode, string pLayerPrefix, string pMessage)
