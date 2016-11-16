namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Runtime.InteropServices

module DebugReport =

    [<AutoOpen>]
    module EnumExtensions =
        type VkResult with
            static member ErrorValidationFailed = -5000 |> unbox<VkResult>



    type Message = 
        { 
            messageFlags : VkDebugReportFlagBitsEXT
            objectType : VkDebugReportObjectTypeEXT 
            sourceObject : uint64 
            location : uint64
            messageCode : int 
            layerPrefix : string
            message : string
        }

    let startMessage =
        {
            messageFlags = VkDebugReportFlagBitsEXT.VkDebugReportInformationBitExt
            objectType = VkDebugReportObjectTypeEXT.VkDebugReportObjectTypeInstanceExt
            sourceObject = 0UL
            location = 0UL
            messageCode = 0
            layerPrefix = "Instance"
            message = "Debug Report started"
        }

    let stopMessage =
        {
            messageFlags = VkDebugReportFlagBitsEXT.VkDebugReportInformationBitExt
            objectType = VkDebugReportObjectTypeEXT.VkDebugReportObjectTypeInstanceExt
            sourceObject = 0UL
            location = 0UL
            messageCode = 0
            layerPrefix = "Instance"
            message = "Debug Report stopped"
        }

    type VkDebugReportCallbackEXTDelegate = 
        delegate of 
            VkDebugReportFlagBitsEXT * VkDebugReportObjectTypeEXT * 
            uint64 * uint64 * int * cstr * cstr * nativeint -> uint32

    type VkCreateDebugReportCallbackEXTDelegate = delegate of VkInstance * nativeptr<VkDebugReportCallbackCreateInfoEXT> * nativeptr<VkAllocationCallbacks> * nativeptr<VkDebugReportCallbackEXT> -> VkResult
    type VkDestroyDebugReportCallbackEXTDelegate = delegate of VkInstance * VkDebugReportCallbackEXT * nativeptr<VkAllocationCallbacks> -> unit


    type Adapter(instance : VkInstance, flags : VkDebugReportFlagBitsEXT) =
        let load (name : string) : 'a =
            let ptr = VkRaw.vkGetInstanceProcAddr(instance, name)
            if ptr = 0n then failwithf "could not get %s" name
            else Marshal.GetDelegateForFunctionPointer(ptr, typeof<'a>) |> unbox<'a>

        let onMessage = Event<_>()

        let callback (flags : VkDebugReportFlagBitsEXT) (objType : VkDebugReportObjectTypeEXT) (srcObject : uint64) (location : uint64) (msgCode : int) (layerPrefix : cstr) (msg : cstr) (userData : nativeint) =
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


        let cbd = VkDebugReportCallbackEXTDelegate(callback)
        let gc = GCHandle.Alloc(cbd)
        let ptr = Marshal.GetFunctionPointerForDelegate(cbd)
        let mutable callback = VkDebugReportCallbackEXT.Null

        let vkCreateDebugReportCallbackEXT : VkCreateDebugReportCallbackEXTDelegate = load "vkCreateDebugReportCallbackEXT"
        let vkDestroyDebugReportCallbackEXT : VkDestroyDebugReportCallbackEXTDelegate = load "vkDestroyDebugReportCallbackEXT"

        [<CLIEvent>]
        member x.OnMessage = onMessage.Publish

        member x.RaiseMessage (message : Message) = 
            onMessage.Trigger(message)


        member x.Start() =
            if callback.IsNull then
                let mutable info =
                    VkDebugReportCallbackCreateInfoEXT(
                        unbox 1000011000, 0n,
                        uint32 flags,
                        ptr,
                        0n
                    )
                vkCreateDebugReportCallbackEXT.Invoke(instance, &&info, NativePtr.zero, &&callback)
                    |> check "vkDbgCreateMsgCallback"

        member x.Stop() =
            if callback.IsValid then
                vkDestroyDebugReportCallbackEXT.Invoke(instance, callback, NativePtr.zero)
                callback <- VkDebugReportCallbackEXT.Null

        member x.Dispose() =
            x.Stop()
            gc.Free()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

[<AutoOpen>]
module Exts =
    type VkDebugReportFlagBitsEXT with
        static member All =
            VkDebugReportFlagBitsEXT.VkDebugReportDebugBitExt |||
            VkDebugReportFlagBitsEXT.VkDebugReportErrorBitExt |||
            VkDebugReportFlagBitsEXT.VkDebugReportInformationBitExt |||
            VkDebugReportFlagBitsEXT.VkDebugReportPerformanceWarningBitExt |||
            VkDebugReportFlagBitsEXT.VkDebugReportWarningBitExt