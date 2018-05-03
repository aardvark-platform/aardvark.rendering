namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Collections.Concurrent
open Aardvark.Base
open Microsoft.FSharp.NativeInterop
open EXTDebugUtils

type MessageSeverity =
    | Information           = 0x00000001
    | Warning               = 0x00000002
    | PerformanceWarning    = 0x00000004
    | Error                 = 0x00000008
    | Debug                 = 0x00000010

type ObjectType = 
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
    | SurfaceKhr = 26
    | SwapchainKhr = 27
    | DebugReport = 28

type DebugMessage = 
    { 
        id              : Guid
        performance     : bool
        severity        : MessageSeverity
        layerPrefix     : string
        message         : string
    }

[<AutoOpen>]
module private DebugReportHelpers =

    [<AutoOpen>]
    module EnumExtensions =
        type VkDebugReportFlagsEXT with
            static member All =
                VkDebugReportFlagsEXT.VkDebugReportDebugBitExt |||
                VkDebugReportFlagsEXT.VkDebugReportErrorBitExt |||
                VkDebugReportFlagsEXT.VkDebugReportInformationBitExt |||
                VkDebugReportFlagsEXT.VkDebugReportPerformanceWarningBitExt |||
                VkDebugReportFlagsEXT.VkDebugReportWarningBitExt

    //type VkDebugReportCallbackEXTDelegate = 
    //    delegate of 
    //        VkDebugReportFlagsEXT * VkDebugReportObjectTypeEXT * 
    //        uint64 * uint64 * int * cstr * cstr * nativeint -> uint32


    //typedef VkBool32 (VKAPI_PTR *PFN_vkDebugUtilsMessengerCallbackEXT)(
    //    VkDebugUtilsMessageSeverityFlagBitsEXT           messageSeverity,
    //    VkDebugUtilsMessageTypeFlagsEXT                  messageType,
    //    const VkDebugUtilsMessengerCallbackDataEXT*      pCallbackData,
    //    void*                                            pUserData);

    type VkDebugUtilsMessengerCallbackEXTDelegate =
        delegate of VkDebugUtilsMessageSeverityFlagsEXT * VkDebugUtilsMessageTypeFlagsEXT * nativeptr<VkDebugUtilsMessengerCallbackDataEXT> * nativeint -> int

    type VkDebugUtilsMessageSeverityFlagsEXT with
        static member All =
            VkDebugUtilsMessageSeverityFlagsEXT.VkDebugUtilsMessageSeverityErrorBitExt |||
            VkDebugUtilsMessageSeverityFlagsEXT.VkDebugUtilsMessageSeverityWarningBitExt |||
            VkDebugUtilsMessageSeverityFlagsEXT.VkDebugUtilsMessageSeverityInfoBitExt // |||
           // VkDebugUtilsMessageSeverityFlagsEXT.VkDebugUtilsMessageSeverityVerboseBitExt

    type DebugReportAdapter internal(instance : Instance) =
        let flags = VkDebugReportFlagsEXT.All
//        let load (name : string) : 'a =
//            let ptr = VkRaw.vkGetInstanceProcAddr(instance.Handle, name)
//            if ptr = 0n then failf "could not get %s" name
//            else Marshal.GetDelegateForFunctionPointer(ptr, typeof<'a>) |> unbox<'a>

        static let ignoreRx = System.Text.RegularExpressions.Regex @"vkBeginCommandBuffer\(\)[ \t]*:[ \t]*Secondary[ \t]+Command[ \t]+Buffers[ \t]+\(0x[0-9A-Fa-f]+\)[ \t]+may[ \t]+perform[ \t]+better[ \t]+if[ \t]+a[ \t]+valid[ \t]+framebuffer[ \t]+parameter[ \t]+is[ \t]+specified\."

        let mutable refCount = 0
        let mutable currentId = 0
        let observers = ConcurrentDictionary<int, IObserver<DebugMessage>>()

        let shutdown () = 
            for (KeyValue(_,obs)) in observers do
                obs.OnCompleted()

        let raise (message : DebugMessage) = 
            for (KeyValue(_,obs)) in observers do
                obs.OnNext message


        let toSeverity =
            LookupTable.lookupTable [
                VkDebugUtilsMessageSeverityFlagsEXT.VkDebugUtilsMessageSeverityErrorBitExt, MessageSeverity.Error  
                VkDebugUtilsMessageSeverityFlagsEXT.VkDebugUtilsMessageSeverityWarningBitExt, MessageSeverity.Warning  
                VkDebugUtilsMessageSeverityFlagsEXT.VkDebugUtilsMessageSeverityInfoBitExt, MessageSeverity.Information  
                VkDebugUtilsMessageSeverityFlagsEXT.VkDebugUtilsMessageSeverityVerboseBitExt, MessageSeverity.Debug   
            ]

//
//        let computeHash (f : BinaryWriter -> unit) =
//            use ms = new MemoryStream()
//            f (new BinaryWriter(ms, Text.Encoding.UTF8, true))
//            ms.ToArray() |> md5.ComputeHash |> Guid
//


        let callback (severity : VkDebugUtilsMessageSeverityFlagsEXT) (messageType : VkDebugUtilsMessageTypeFlagsEXT) (data : nativeptr<VkDebugUtilsMessengerCallbackDataEXT>) (userData : nativeint) =
            
            let data = NativePtr.read data
            
            let messageIdName =
                if data.pMessageIdName <> NativePtr.zero then
                    data.pMessageIdName |> CStr.toString
                else
                    ""
                    
            let msg = data.pMessage |> CStr.toString

            let hash = Guid.Empty
//                computeHash (fun w ->
//                    w.Write (int flags)
//                    w.Write (int objType)
//                    w.Write msgCode
//                    w.Write layerPrefix
//                    w.Write location
//                )

            if not (ignoreRx.IsMatch msg) then
                raise {
                    id              = hash
                    performance     = (messageType &&& VkDebugUtilsMessageTypeFlagsEXT.VkDebugUtilsMessageTypePerformanceBitExt) <> VkDebugUtilsMessageTypeFlagsEXT.None
                    severity        = toSeverity severity
                    layerPrefix     = messageIdName
                    message         = msg
                }

            0

        let callbackDelegate = VkDebugUtilsMessengerCallbackEXTDelegate(callback)
        let mutable gc = Unchecked.defaultof<GCHandle>
        let mutable callback = VkDebugUtilsMessengerEXT.Null

        let destroy() =
            let o = Interlocked.Exchange(&refCount, 0)
            if o <> 0 then
                shutdown()
                gc.Free()

                VkRaw.vkDestroyDebugUtilsMessengerEXT(instance.Handle, callback, NativePtr.zero)

                callback <- VkDebugUtilsMessengerEXT.Null
                observers.Clear()
                currentId <- 0

        let instanceDisposedHandler = Handler<unit>(fun _ () -> destroy())

        let add (obs : IObserver<DebugMessage>) =
            let id = Interlocked.Increment(&currentId)
            observers.TryAdd(id, obs) |> ignore
            let n = Interlocked.Increment(&refCount)
            if n = 1 then
                gc <- GCHandle.Alloc(callbackDelegate)
                let ptr = Marshal.GetFunctionPointerForDelegate(callbackDelegate)
                //let mutable info =
                //    VkDebugReportCallbackCreateInfoEXT(
                //        VkStructureType.DebugReportCallbackCreateInfoExt, 0n,
                //        flags,
                //        ptr,
                //        0n
                //    )

                let mutable info =
                    VkDebugUtilsMessengerCreateInfoEXT(
                        VkStructureType.DebugUtilsMessengerCreateInfoExt, 0n,
                        VkDebugUtilsMessengerCreateFlagsEXT.MinValue,
                        VkDebugUtilsMessageSeverityFlagsEXT.All,

                        VkDebugUtilsMessageTypeFlagsEXT.VkDebugUtilsMessageTypeGeneralBitExt ||| 
                        VkDebugUtilsMessageTypeFlagsEXT.VkDebugUtilsMessageTypeValidationBitExt ||| 
                        VkDebugUtilsMessageTypeFlagsEXT.VkDebugUtilsMessageTypePerformanceBitExt,

                        ptr, 123n
                    )


                VkRaw.vkCreateDebugUtilsMessengerEXT(instance.Handle, &&info, NativePtr.zero, &&callback)
                    |> check "vkCreateDebugUtilsMessengerEXT"

                //VkRaw.vkCreateDebugReportCallbackEXT(instance.Handle, &&info, NativePtr.zero, &&callback)
                //    |> check "vkDbgCreateMsgCallback"

                instance.BeforeDispose.AddHandler(instanceDisposedHandler)

            id

        let remove (id : int) =
            match observers.TryRemove id with
                | (true, obs) ->
                    let n = Interlocked.Decrement(&refCount)
                    if n = 0 then
                        gc.Free()
                        VkRaw.vkDestroyDebugUtilsMessengerEXT(instance.Handle, callback, NativePtr.zero)
                        callback <- VkDebugUtilsMessengerEXT.Null
                        instance.BeforeDispose.RemoveHandler(instanceDisposedHandler)
                | _ ->
                    Report.Warn "[Vulkan] DebugReport Observer removed which was never added"

        let layer = CStr.malloc "DebugReport"

        interface IObservable<DebugMessage> with
            member x.Subscribe (observer : IObserver<DebugMessage>) =
                let id = add observer
                { new IDisposable with
                    member x.Dispose() = remove id
                }
                
        member x.Raise(severity : MessageSeverity, msg : string) =
            let flags =
                match severity with
                    | MessageSeverity.Debug -> VkDebugUtilsMessageSeverityFlagsEXT.VkDebugUtilsMessageSeverityVerboseBitExt 
                    | MessageSeverity.Information -> VkDebugUtilsMessageSeverityFlagsEXT.VkDebugUtilsMessageSeverityInfoBitExt
                    | MessageSeverity.PerformanceWarning -> VkDebugUtilsMessageSeverityFlagsEXT.VkDebugUtilsMessageSeverityWarningBitExt
                    | MessageSeverity.Warning -> VkDebugUtilsMessageSeverityFlagsEXT.VkDebugUtilsMessageSeverityWarningBitExt
                    | MessageSeverity.Error -> VkDebugUtilsMessageSeverityFlagsEXT.VkDebugUtilsMessageSeverityErrorBitExt
                    | _ -> VkDebugUtilsMessageSeverityFlagsEXT.None

            msg |> CStr.suse (fun str -> 

                let mutable objectname =
                    VkDebugUtilsObjectNameInfoEXT(
                        VkStructureType.DebugUtilsObjectNameInfoExt, 0n,
                        VkObjectType.Instance, uint64 instance.Handle,
                        NativePtr.zero
                    )

                let mutable info = 
                    VkDebugUtilsMessengerCallbackDataEXT(
                        VkStructureType.DebugUtilsMessengerCallbackDataExt, 0n,
                        VkDebugUtilsMessengerCallbackDataFlagsEXT.MinValue,
                        layer, 0,
                        str, 
                        0u, NativePtr.zero,
                        0u, NativePtr.zero,
                        1u, &&objectname
                    )

                VkRaw.vkSubmitDebugUtilsMessageEXT(
                    instance.Handle,
                    flags,
                    VkDebugUtilsMessageTypeFlagsEXT.VkDebugUtilsMessageTypeGeneralBitExt,
                    &&info
                )
                //VkRaw.vkDebugReportMessageEXT(
                //    instance.Handle,
                //    unbox (int flags),
                //    VkDebugReportObjectTypeEXT.VkDebugReportObjectTypeUnknownExt,
                //    0UL, 0UL, 0,
                //    layer,
                //    str
                //)
            )
            
[<AbstractClass; Sealed; Extension>]
type InstanceExtensions private() =
    static let table = new ConditionalWeakTable<Instance, Option<DebugReportAdapter>>()

    static let notEnabledObservable =
        { new IObservable<DebugMessage> with
            member x.Subscribe (obs : IObserver<DebugMessage>) =
                obs.OnNext {
                    id              = Guid.Empty
                    severity        = MessageSeverity.Warning
                    performance     = false
                    layerPrefix     = "DR"
                    message         = "could not subscribe to DebugMessages since the instance does not provide the needed Extension"
                }
                obs.OnCompleted()
                { new IDisposable with member x.Dispose() = () }
        }

    static let getAdapter (instance : Instance) =
        lock table (fun () ->
            match table.TryGetValue instance with
                | (true, adapter) -> adapter
                | _ ->
                    if List.contains Instance.Extensions.DebugUtils instance.EnabledExtensions then
                        let adapter = new DebugReportAdapter(instance)
                        table.Add(instance, Some adapter)
                        Some adapter
                    else
                       None 
        )

    [<Extension>]
    static member GetDebugMessageObservable(this : Instance) =
        match getAdapter this with
            | Some a -> a :> IObservable<_>
            | _ -> notEnabledObservable

    [<Extension>]
    static member RaiseDebugMessage(this : Instance, severity : MessageSeverity, msg : string) =
        match getAdapter this with
            | Some a -> a.Raise(severity, msg)
            | _ -> ()

[<AutoOpen>]
module ``FSharp Style Debug Extensions`` =
    type Instance with
        member x.DebugMessages = x.GetDebugMessageObservable()
