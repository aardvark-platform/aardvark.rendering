namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Runtime.InteropServices

module DebugReport =

    type VkDbgObjectType =
        | Instance = 0
        | PhysicalDevice = 1
        | Device = 2
        | Queue = 3
        | CommandBuffer = 4
        | DeviceMemory = 5
        | Buffer = 6
        | BufferView = 7
        | Image = 8
        | ImageView = 9
        | AttachmentView = 10
        | ShaderModule = 12
        | Shader = 13
        | Pipeline = 14
        | PipelineLayout = 15
        | Sampler = 16
        | DescriptorSet = 17
        | DescriptorSetLayout = 18
        | DescriptorPool = 19
        | DynamicViewportState = 20
        | DynamicLineWidthState = 21
        | DynamicDepthBiasState = 22
        | DynamicBlendState = 23
        | DynamicDepthBoundsState = 24
        | DynamicStencilState = 25
        | Fence = 26
        | Semaphore = 27
        | Event = 28
        | QueryPool = 29
        | Framebuffer = 30
        | RenderPass = 31
        | PipelineCache = 32
        | SwapchainKHR = 33
        | CmdPool = 34
        | MessageCallback = -5000

    [<Flags>]
    type VkDbgReportFlags =
        | None          = 0x0000u
        | InfoBit       = 0x0001u
        | WarnBit       = 0x0002u
        | PerfWarnBit   = 0x0004u
        | ErrorBit      = 0x0008u
        | DebugBit      = 0x0010u
        | All           = 0x001Fu

    [<AutoOpen>]
    module EnumExtensions =
        type VkResult with
            static member ErrorValidationFailed = -5000 |> unbox<VkResult>


    type Message = 
        { 
            messageFlags : VkDbgReportFlags
            objectType : VkDbgObjectType 
            sourceObject : uint64 
            location : uint64
            messageCode : int 
            layerPrefix : string
            message : string
        }


    [<StructLayout(LayoutKind.Sequential)>]
    type VkDbgMsgCallback = 
        struct
            val mutable public Handle : uint64
            new(h) = { Handle = h }
            static member Null = VkDbgMsgCallback(0UL)
            member x.IsNull = x.Handle = 0UL
            member x.IsValid = x.Handle <> 0UL
        end


    type VkDbgMsgCallbackDelegate = delegate of VkDbgReportFlags * VkDbgObjectType * uint64 * uint64 * int * cstr * cstr * nativeint -> uint32
    type VkDbgCreateMsgCallbackDelegate = delegate of instance : VkInstance * msgFlags : VkFlags * pfnMsgCallback : nativeint * pUserData : nativeint * nativeptr<VkDbgMsgCallback> -> VkResult
    type VkDbgDestroyMsgCallbackDelegate = delegate of instance : VkInstance * msgCallback : VkDbgMsgCallback -> VkResult


    type Adapter(instance : VkInstance, flags : VkDbgReportFlags) =
        let load (name : string) : 'a =
            let ptr = VkRaw.vkGetInstanceProcAddr(instance, name)
            if ptr = 0n then failwithf "could not get %s" name
            else Marshal.GetDelegateForFunctionPointer(ptr, typeof<'a>) |> unbox<'a>

        let vkDbgCreateMsgCallback_ : VkDbgCreateMsgCallbackDelegate = load "vkDbgCreateMsgCallback"
        let vkDbgDestroyMsgCallback_ : VkDbgDestroyMsgCallbackDelegate = load "vkDbgDestroyMsgCallback"

        let onMessage = Event<_>()

        let callback (flags : VkDbgReportFlags) (objType : VkDbgObjectType) (srcObject : uint64) (location : uint64) (msgCode : int) (layerPrefix : cstr) (msg : cstr) (userData : nativeint) =
            let layerPrefix = layerPrefix |> CStr.toString
            let msg = msg |> CStr.toString


            let debugMessage =
                {
                    messageFlags = flags
                    objectType = objType
                    sourceObject = srcObject
                    location = location
                    messageCode = msgCode
                    layerPrefix = layerPrefix
                    message = msg
                }

            onMessage.Trigger(debugMessage)

            0u


        let cbd = VkDbgMsgCallbackDelegate(callback)
        let gc = GCHandle.Alloc(cbd)
        let ptr = Marshal.GetFunctionPointerForDelegate(cbd)
        let mutable callback = VkDbgMsgCallback.Null


        [<CLIEvent>]
        member x.OnMessage = onMessage.Publish

        member x.OnMessageEvent = onMessage


        member x.Start() =
            if callback.IsNull then
                vkDbgCreateMsgCallback_.Invoke(instance, uint32 flags, ptr, 0n, &&callback) |> check "vkDbgCreateMsgCallback"

        member x.Stop() =
            if callback.IsValid then
                vkDbgDestroyMsgCallback_.Invoke(instance, callback) |> check "vkDbgDestroyMsgCallback"
                callback <- VkDbgMsgCallback.Null
